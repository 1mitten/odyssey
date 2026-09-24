#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The grass tufts drawn from GPU buffers with a compute cull, instead of one instanced call per
    /// chunk and kind (<c>docs/design/38-meadow-overhaul.md</c> §18, <c>docs/research/d-19</c>).
    ///
    /// <para><b>What it replaces.</b> Every chunk's tuft buckets were a <c>RenderMeshInstanced</c>
    /// call each — hundreds a frame, each costing Unity about three quarters of a microsecond of CPU
    /// whatever it held (d-19 §1). Here the tufts of the whole board are gathered into one buffer per
    /// (module, tint, layer), a compute pass keeps the instances inside the frustum and the draw
    /// distance, and one indirect draw per part draws what it kept: a few dozen calls for the board.
    /// </para>
    ///
    /// <para><b>What it keeps.</b> The same material the chunk path would have used for that layer's
    /// shade (cloned once with the <c>ODYSSEY_INDIRECT</c> keyword), the same meshes, the same
    /// matrices, the same clearance field and wind — the shader reads its matrix from the buffer and
    /// does everything else as before. Tufts never fade and never cast, so neither the sight fade nor
    /// the shadow pass is involved. The draw distance is judged per clump rather than per chunk.</para>
    ///
    /// <para><b>When it rebuilds.</b> Whenever a chunk is re-meshed, the buffers are regathered from
    /// every meshed chunk at the end of that frame's walk: simple, and cheap against the meshing that
    /// caused it. Per-chunk ranges are the upgrade if a measurement ever finds the regather.</para>
    /// </summary>
    public sealed class IndirectFoliage : IDisposable
    {
        const int ThreadGroup = 64;
        static readonly int InstancesId = Shader.PropertyToID("_Instances");
        static readonly int VisibleId = Shader.PropertyToID("_Visible");
        static readonly int PlanesId = Shader.PropertyToID("_Planes");
        static readonly int CentreRadiusId = Shader.PropertyToID("_CentreRadius");
        static readonly int ViewerId = Shader.PropertyToID("_Viewer");
        static readonly int CountId = Shader.PropertyToID("_Count");
        static readonly int ShaderInstancesId = Shader.PropertyToID("_OdysseyInstances");
        static readonly int ShaderVisibleId = Shader.PropertyToID("_OdysseyVisible");
        const string Keyword = "ODYSSEY_INDIRECT";

        sealed class Group : IDisposable
        {
            public int Module, Tint, Layer;
            public readonly List<Matrix4x4> Matrices = new List<Matrix4x4>();
            public GraphicsBuffer? Instances, Visible, Args;
            public int Capacity;
            public ModulePart[] Parts = Array.Empty<ModulePart>();
            public Vector4 CentreRadius;
            public MaterialPropertyBlock Props = new MaterialPropertyBlock();

            public void Dispose()
            {
                Instances?.Release(); Visible?.Release(); Args?.Release();
                Instances = Visible = Args = null;
                Capacity = 0;
            }
        }

        readonly ComputeShader? _cull;
        readonly int _kernel = -1;
        readonly Dictionary<long, Group> _groups = new Dictionary<long, Group>();
        readonly List<Group> _ordered = new List<Group>();
        readonly Dictionary<Material, Material> _indirectMaterials = new Dictionary<Material, Material>();
        readonly Vector4[] _planes = new Vector4[6];
        readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _argScratch = new GraphicsBuffer.IndirectDrawIndexedArgs[8];

        public IndirectFoliage()
        {
            _cull = Resources.Load<ComputeShader>("OdysseyCompute/OdysseyIndirectCull");
            if (_cull != null && _cull.HasKernel("Cull")) _kernel = _cull.FindKernel("Cull");
        }

        /// <summary>
        /// Whether this machine can draw this way at all: compute shaders, indirect arguments, a
        /// real device, the cull shader found, and the foliage shader present to read the buffers.
        /// Everything falls back to the chunk path when not.
        /// </summary>
        public bool Available =>
            _kernel >= 0
            && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null
            && SystemInfo.supportsComputeShaders
            && SystemInfo.supportsIndirectArgumentsBuffer
            && Shader.Find("Odyssey/Foliage") != null;

        /// <summary>A chunk was re-meshed this frame, so the buffers are regathered before drawing.</summary>
        public bool Dirty { get; set; } = true;

        /// <summary>Tufts in the buffers after the last regather.</summary>
        public int InstanceCount { get; private set; }

        /// <summary>Groups in the buffers after the last regather: at most one draw per part each.</summary>
        public int GroupCount => _ordered.Count;

        static long Key(int module, int tint, int layer) =>
            ((long)(uint)module << 40) ^ ((long)(uint)tint << 8) ^ (uint)layer;

        /// <summary>
        /// Gather every meshed chunk's tuft buckets into the group lists and upload them. The
        /// matrices are the ones the chunk path would have drawn, in chunk order, so the same
        /// clumps are drawn in the same places.
        /// </summary>
        public void Rebuild(ChunkBatch?[] batches, ModuleLibrary library, Func<InstanceBucket, bool> isTuft)
        {
            Dirty = false;
            foreach (Group g in _ordered) g.Matrices.Clear();

            for (int c = 0; c < batches.Length; c++)
            {
                ChunkBatch? batch = batches[c];
                if (batch == null || batch.InstanceCount == 0) continue;
                List<InstanceBucket> body = batch.Body;
                for (int b = 0; b < body.Count; b++)
                {
                    InstanceBucket bucket = body[b];
                    if (bucket.Count == 0 || !isTuft(bucket)) continue;
                    long key = Key(bucket.Module, bucket.Tint, batch.Layer);
                    if (!_groups.TryGetValue(key, out Group? group))
                    {
                        group = new Group { Module = bucket.Module, Tint = bucket.Tint, Layer = batch.Layer };
                        ResolvedModule resolved = library[bucket.Module];
                        group.Parts = resolved.DrawsByLevel ? resolved.Lods[0].Parts : new[] { resolved.Parts[bucket.Part] };
                        group.CentreRadius = SphereOf(group.Parts);
                        _groups.Add(key, group);
                        _ordered.Add(group);
                    }
                    for (int i = 0; i < bucket.Count; i++) group.Matrices.Add(bucket.Matrices[i]);
                }
            }

            int total = 0;
            foreach (Group g in _ordered)
            {
                total += g.Matrices.Count;
                if (g.Matrices.Count == 0) continue;
                Upload(g);
            }
            InstanceCount = total;
        }

        /// <summary>The mesh's own bounds as a sphere in object space, plus half a metre for the
        /// wind's bow, which leans a blade past its rest-pose box.</summary>
        static Vector4 SphereOf(ModulePart[] parts)
        {
            var bounds = new Bounds();
            bool any = false;
            foreach (ModulePart part in parts)
            {
                if (part.Mesh == null) continue;
                if (!any) { bounds = part.Mesh.bounds; any = true; }
                else bounds.Encapsulate(part.Mesh.bounds);
            }
            return new Vector4(bounds.center.x, bounds.center.y, bounds.center.z, bounds.extents.magnitude + 0.5f);
        }

        void Upload(Group g)
        {
            int count = g.Matrices.Count;
            if (g.Capacity < count)
            {
                g.Dispose();
                g.Capacity = Mathf.NextPowerOfTwo(count);
                g.Instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, g.Capacity, 64);
                g.Visible = new GraphicsBuffer(GraphicsBuffer.Target.Append, g.Capacity, sizeof(uint));
                g.Args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, g.Parts.Length,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size);

                int parts = Mathf.Min(g.Parts.Length, _argScratch.Length);
                for (int k = 0; k < parts; k++)
                {
                    ModulePart part = g.Parts[k];
                    _argScratch[k] = new GraphicsBuffer.IndirectDrawIndexedArgs
                    {
                        indexCountPerInstance = part.Mesh.GetIndexCount(part.Submesh),
                        instanceCount = 0,
                        startIndex = part.Mesh.GetIndexStart(part.Submesh),
                        baseVertexIndex = part.Mesh.GetBaseVertex(part.Submesh),
                        startInstance = 0,
                    };
                }
                g.Args.SetData(_argScratch, 0, 0, parts);
                g.Props.SetBuffer(ShaderInstancesId, g.Instances);
                g.Props.SetBuffer(ShaderVisibleId, g.Visible);
            }
            g.Instances!.SetData(g.Matrices, 0, 0, count);
        }

        /// <summary>
        /// Cull and draw the groups on one layer. <paramref name="materialFor"/> is the chunk path's
        /// own choice of material for that layer's shade; this clones it once with the indirect
        /// keyword. Returns the draw calls issued, and the clumps offered to the cull (the GPU decides
        /// how many of them are drawn; that count never comes back to the CPU).
        /// </summary>
        public int DrawLayer(int layer, Plane[] frustum, Vector3? viewer, float drawDistance,
            Func<ModulePart, int, Material> materialFor, int gameObjectLayer, Bounds worldBounds, bool submit,
            out int instances)
        {
            instances = 0;
            foreach (Group g in _ordered) if (g.Layer == layer) instances += g.Matrices.Count;
            if (!submit) return 0;
            for (int p = 0; p < 6 && p < frustum.Length; p++)
                _planes[p] = new Vector4(frustum[p].normal.x, frustum[p].normal.y, frustum[p].normal.z, frustum[p].distance);

            bool limited = viewer.HasValue && !float.IsInfinity(drawDistance);
            Vector4 viewerParam = limited
                ? new Vector4(viewer!.Value.x, viewer.Value.y, viewer.Value.z, drawDistance * drawDistance)
                : new Vector4(0f, 0f, 0f, -1f);

            int calls = 0;
            foreach (Group g in _ordered)
            {
                if (g.Layer != layer || g.Matrices.Count == 0 || g.Instances == null) continue;

                g.Visible!.SetCounterValue(0);
                _cull!.SetBuffer(_kernel, InstancesId, g.Instances);
                _cull.SetBuffer(_kernel, VisibleId, g.Visible);
                _cull.SetVectorArray(PlanesId, _planes);
                _cull.SetVector(CentreRadiusId, g.CentreRadius);
                _cull.SetVector(ViewerId, viewerParam);
                _cull.SetInt(CountId, g.Matrices.Count);
                _cull.Dispatch(_kernel, (g.Matrices.Count + ThreadGroup - 1) / ThreadGroup, 1, 1);

                int parts = Mathf.Min(g.Parts.Length, _argScratch.Length);
                for (int k = 0; k < parts; k++)
                {
                    // The kept count lands in this part's command's instanceCount (the second uint).
                    GraphicsBuffer.CopyCount(g.Visible, g.Args!, k * GraphicsBuffer.IndirectDrawIndexedArgs.size + sizeof(uint));
                    ModulePart part = g.Parts[k];
                    var rp = new RenderParams(IndirectMaterial(materialFor(part, g.Tint)))
                    {
                        worldBounds = worldBounds,
                        layer = gameObjectLayer,
                        receiveShadows = true,
                        shadowCastingMode = ShadowCastingMode.Off,
                        matProps = g.Props,
                    };
                    Graphics.RenderMeshIndirect(rp, part.Mesh, g.Args!, 1, k);
                    calls++;
                }
            }
            return calls;
        }

        Material IndirectMaterial(Material source)
        {
            if (_indirectMaterials.TryGetValue(source, out Material? made)) return made;
            made = new Material(source) { name = source.name + " (indirect)", hideFlags = HideFlags.DontSave };
            made.EnableKeyword(Keyword);
            _indirectMaterials.Add(source, made);
            return made;
        }

        /// <summary>Whether any group holds tufts on this layer.</summary>
        public bool HasLayer(int layer)
        {
            foreach (Group g in _ordered) if (g.Layer == layer && g.Matrices.Count > 0) return true;
            return false;
        }

        public void Dispose()
        {
            foreach (Group g in _ordered) g.Dispose();
            _groups.Clear();
            _ordered.Clear();
            foreach (Material m in _indirectMaterials.Values)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(m);
                else UnityEngine.Object.DestroyImmediate(m);
            }
            _indirectMaterials.Clear();
        }
    }
}
