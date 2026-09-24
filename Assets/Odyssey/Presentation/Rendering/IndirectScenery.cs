#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The scenery — grass tufts, tall-grass stands, flowers, ground cover and bushes — drawn from GPU
    /// buffers with a compute cull, instead of one instanced call per chunk, kind and level
    /// (<c>docs/design/38-meadow-overhaul.md</c> §22; the tufts-only first cut was §18).
    ///
    /// <para><b>Why.</b> The owner's biggest frame drop with grass on, especially zoomed out, was the
    /// scenery: every chunk submitted every kind, so each kind cost 100–170 draw calls at far zoom
    /// (§21). Here each kind's instances on a layer live in one buffer, and one indirect draw per level
    /// part draws what the cull kept — a few calls per kind for the whole board.</para>
    ///
    /// <para><b>Nothing the chunk path decides moves.</b> The instances are gathered from the chunk
    /// buckets in chunk order, one <i>segment</i> per (chunk, bucket). Every frame the renderer
    /// decides, per segment and exactly as the chunk path decides per bucket, whether the chunk is
    /// drawn at all (frustum, draw distance), which level of detail it takes, and how many of its
    /// rank-sorted clumps survive the distance thinning. The compute pass only applies those
    /// decisions per instance, adds a per-instance frustum test (these kinds cast no shadows, so an
    /// instance off screen can never be seen), and sorts the survivors into one list per level. Wind,
    /// clearance, the per-clump shrink and the stand colour are the shader's, as before.</para>
    ///
    /// <para><b>When it regathers — per chunk, in place.</b> Each group gives every chunk a slot with
    /// room to spare. A chunk re-meshed (a dig, a build, a crop that grew) rewrites only its own slots
    /// and uploads only those ranges; a slot that outgrows its room moves to the end of the group's
    /// buffer and the old one becomes dead space, compacted away once it is half the buffer. Measured
    /// on Huge, regathering the whole surface layer for one chunk cost 3.9 ms; this is what replaced
    /// it (design 38 §22).</para>
    /// </summary>
    public sealed class IndirectScenery : IDisposable
    {
        public const int MaxLevels = 4;
        public const int MaxPartsPerLevel = 8;
        const int ThreadGroup = 64;

        static readonly int InstancesId = Shader.PropertyToID("_Instances");
        static readonly int InstanceSegmentId = Shader.PropertyToID("_InstanceSegment");
        static readonly int SegmentsId = Shader.PropertyToID("_Segments");
        static readonly int[] VisibleIds =
        {
            Shader.PropertyToID("_Visible0"), Shader.PropertyToID("_Visible1"),
            Shader.PropertyToID("_Visible2"), Shader.PropertyToID("_Visible3"),
        };
        static readonly int PlanesId = Shader.PropertyToID("_Planes");
        static readonly int CentreRadiusId = Shader.PropertyToID("_CentreRadius");
        static readonly int CountId = Shader.PropertyToID("_Count");
        static readonly int ShaderInstancesId = Shader.PropertyToID("_OdysseyInstances");
        static readonly int ShaderVisibleId = Shader.PropertyToID("_OdysseyVisible");
        const string Keyword = "ODYSSEY_INDIRECT";

        /// <summary>One chunk's bucket inside a group: where its instances start, how many there are.</summary>
        public struct Segment
        {
            public int ChunkIndex;
            public int Start;
            /// <summary>Live instances in the slot; 0 when the chunk no longer holds this kind.</summary>
            public int Count;
            /// <summary>Room in the slot; the rest past <see cref="Count"/> is never drawn.</summary>
            public int Capacity;
        }

        /// <summary>The per-segment parameters the cull reads: where the segment's instances start,
        /// how many survive, and the level they are drawn at.</summary>
        internal struct SegmentParam
        {
            public int Start, Submitted, Level, Pad;
        }

        /// <summary>The per-frame decision for one segment, made by the renderer.</summary>
        public struct Decision
        {
            /// <summary>Whether the chunk path would have drawn the segment's chunk at all.</summary>
            public bool Drawn;
            /// <summary>Instances of the segment that survive: 0 when the chunk is not drawn.</summary>
            public int Submitted;
            public int Level;
        }

        public sealed class Group : IDisposable
        {
            public int Module, Part, Tint, Layer;
            public ModulePart[][] Levels = Array.Empty<ModulePart[]>();
            public bool Thinned;
            public Matrix4x4[] Matrices = Array.Empty<Matrix4x4>();
            public float[] Ranks = Array.Empty<float>();
            public uint[] SegmentOf = Array.Empty<uint>();
            public readonly List<Segment> Segments = new List<Segment>();
            public readonly Dictionary<int, int> SlotOf = new Dictionary<int, int>();
            /// <summary>Entries in use, live or dead: what the cull is dispatched over.</summary>
            public int Used;
            /// <summary>Live instances across every slot.</summary>
            public int Live;
            public int Dead;
            internal bool FullUpload = true;
            internal readonly List<(int Start, int Count)> DirtyRanges = new List<(int, int)>();

            internal void EnsureLength(int length)
            {
                if (Matrices.Length >= length) return;
                int grown = Mathf.NextPowerOfTwo(Mathf.Max(length, 64));
                Array.Resize(ref Matrices, grown);
                Array.Resize(ref Ranks, grown);
                Array.Resize(ref SegmentOf, grown);
                FullUpload = true;
            }

            /// <summary>The chunk no longer holds this kind (until it writes again this regather).</summary>
            public void ZeroChunk(int chunk)
            {
                if (!SlotOf.TryGetValue(chunk, out int slot)) return;
                Segment seg = Segments[slot];
                Live -= seg.Count;
                seg.Count = 0;
                Segments[slot] = seg;
            }

            /// <summary>Write a chunk's bucket into its slot, moving the slot to the end if it has outgrown it.</summary>
            public void Write(int chunk, Matrix4x4[] source, int count)
            {
                int slot;
                if (SlotOf.TryGetValue(chunk, out slot) && Segments[slot].Capacity < count)
                {
                    Segment old = Segments[slot];
                    Live -= old.Count;
                    Dead += old.Capacity;
                    old.Count = 0;
                    old.ChunkIndex = -1;
                    Segments[slot] = old;
                    SlotOf.Remove(chunk);
                }
                if (!SlotOf.TryGetValue(chunk, out slot))
                {
                    int capacity = Mathf.Max(4, Mathf.NextPowerOfTwo(count + count / 4));
                    EnsureLength(Used + capacity);
                    slot = Segments.Count;
                    Segments.Add(new Segment { ChunkIndex = chunk, Start = Used, Count = 0, Capacity = capacity });
                    SlotOf[chunk] = slot;
                    for (int i = 0; i < capacity; i++) SegmentOf[Used + i] = (uint)slot;
                    Used += capacity;
                }
                Segment seg = Segments[slot];
                Live -= seg.Count;
                Array.Copy(source, 0, Matrices, seg.Start, count);
                if (Thinned)
                    for (int i = 0; i < count; i++) Ranks[seg.Start + i] = GrassThinning.Rank(in source[i]);
                seg.Count = count;
                Segments[slot] = seg;
                Live += count;
                DirtyRanges.Add((seg.Start, seg.Capacity));
            }

            /// <summary>Drop the dead space: live slots copied to the front, in slot order.</summary>
            public void Compact()
            {
                var matrices = new Matrix4x4[Matrices.Length];
                var ranks = new float[Ranks.Length];
                var segmentOf = new uint[SegmentOf.Length];
                var segments = new List<Segment>();
                SlotOf.Clear();
                int used = 0;
                foreach (Segment seg in Segments)
                {
                    if (seg.ChunkIndex < 0) continue;
                    int slot = segments.Count;
                    Array.Copy(Matrices, seg.Start, matrices, used, seg.Capacity);
                    Array.Copy(Ranks, seg.Start, ranks, used, seg.Capacity);
                    for (int i = 0; i < seg.Capacity; i++) segmentOf[used + i] = (uint)slot;
                    segments.Add(new Segment { ChunkIndex = seg.ChunkIndex, Start = used, Count = seg.Count, Capacity = seg.Capacity });
                    SlotOf[seg.ChunkIndex] = slot;
                    used += seg.Capacity;
                }
                Matrices = matrices; Ranks = ranks; SegmentOf = segmentOf;
                Segments.Clear(); Segments.AddRange(segments);
                Used = used; Dead = 0;
                FullUpload = true;
            }

            internal GraphicsBuffer? Instances, InstanceSegment, SegmentParams, Args;
            internal readonly GraphicsBuffer?[] Visible = new GraphicsBuffer?[MaxLevels];
            internal readonly MaterialPropertyBlock[] Props =
            {
                new MaterialPropertyBlock(), new MaterialPropertyBlock(),
                new MaterialPropertyBlock(), new MaterialPropertyBlock(),
            };
            internal int Capacity, SegmentCapacity;
            internal int[] ArgOffset = Array.Empty<int>();
            internal Vector4 CentreRadius;
            internal SegmentParam[] SegmentScratch = Array.Empty<SegmentParam>();

            public void Dispose()
            {
                Instances?.Release(); InstanceSegment?.Release(); SegmentParams?.Release(); Args?.Release();
                Instances = InstanceSegment = SegmentParams = Args = null;
                for (int l = 0; l < MaxLevels; l++) { Visible[l]?.Release(); Visible[l] = null; }
                Capacity = SegmentCapacity = 0;
            }
        }

        readonly ComputeShader? _cull;
        readonly int _kernel = -1;
        readonly Dictionary<(int, int, int, int), Group> _groups = new Dictionary<(int, int, int, int), Group>();
        readonly List<Group> _ordered = new List<Group>();
        readonly HashSet<int> _dirtyChunks = new HashSet<int>();
        readonly Dictionary<int, List<Group>> _groupsByChunk = new Dictionary<int, List<Group>>();
        readonly HashSet<Group> _changed = new HashSet<Group>();
        readonly Dictionary<Material, Material> _indirectMaterials = new Dictionary<Material, Material>();
        readonly Vector4[] _planes = new Vector4[6];
        readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _argScratch =
            new GraphicsBuffer.IndirectDrawIndexedArgs[MaxLevels * MaxPartsPerLevel];

        public IndirectScenery()
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

        /// <summary>Instances in the buffers after the last regather.</summary>
        public int InstanceCount { get; private set; }

        /// <summary>Groups held: at most one draw per level part each, per frame.</summary>
        public int GroupCount => _ordered.Count;

        /// <summary>Groups regathered last frame (for measurement).</summary>
        public int GroupsRegathered { get; private set; }

        public IReadOnlyList<Group> Groups => _ordered;

        /// <summary>A chunk was re-meshed: its slots are rewritten before drawing.</summary>
        public void MarkDirty(int chunkIndex) => _dirtyChunks.Add(chunkIndex);

        /// <summary>Every layer's groups are regathered (a new board, or the kinds changed).</summary>
        public void MarkAllDirty() => _allDirty = true;

        bool _allDirty = true;

        /// <summary>
        /// Rewrite the slots of every re-meshed chunk from its buckets (every chunk, when everything is
        /// dirty). <paramref name="eligible"/> says which buckets go this way; <paramref name="levelsOf"/>
        /// gives a bucket's levels, finest first; <paramref name="thinned"/> whether its kind is
        /// rank-thinned with distance.
        /// </summary>
        public void Rebuild(ChunkBatch?[] batches, Func<InstanceBucket, bool> eligible,
            Func<InstanceBucket, ModulePart[][]> levelsOf, Func<InstanceBucket, bool> thinned)
        {
            GroupsRegathered = 0;
            if (!_allDirty && _dirtyChunks.Count == 0) return;

            if (_allDirty)
            {
                foreach (Group g in _ordered) g.Dispose();
                _groups.Clear();
                _ordered.Clear();
                _groupsByChunk.Clear();
                _dirtyChunks.Clear();
                for (int c = 0; c < batches.Length; c++) _dirtyChunks.Add(c);
            }

            _changed.Clear();
            foreach (int c in _dirtyChunks)
            {
                if (_groupsByChunk.TryGetValue(c, out List<Group>? had))
                    foreach (Group g in had) { g.ZeroChunk(c); _changed.Add(g); }

                ChunkBatch? batch = batches[c];
                if (batch == null || batch.InstanceCount == 0) continue;
                List<InstanceBucket> body = batch.Body;
                for (int b = 0; b < body.Count; b++)
                {
                    InstanceBucket bucket = body[b];
                    if (bucket.Count == 0 || !eligible(bucket)) continue;
                    var key = (bucket.Module, bucket.Part, bucket.Tint, batch.Layer);
                    if (!_groups.TryGetValue(key, out Group? group))
                    {
                        group = new Group
                        {
                            Module = bucket.Module, Part = bucket.Part, Tint = bucket.Tint, Layer = batch.Layer,
                            Levels = levelsOf(bucket), Thinned = thinned(bucket),
                        };
                        group.CentreRadius = SphereOf(group.Levels);
                        _groups.Add(key, group);
                        _ordered.Add(group);
                    }
                    group.Write(c, bucket.Matrices, bucket.Count);
                    _changed.Add(group);
                    if (!_groupsByChunk.TryGetValue(c, out List<Group>? list))
                    {
                        list = new List<Group>();
                        _groupsByChunk.Add(c, list);
                    }
                    if (!list.Contains(group)) list.Add(group);
                }
            }

            foreach (Group g in _changed)
            {
                if (g.Dead > 1024 && g.Dead * 2 > g.Used) g.Compact();
                if (g.Used > 0) Upload(g);
            }
            GroupsRegathered = _changed.Count;

            int total = 0;
            foreach (Group g in _ordered) total += g.Live;
            InstanceCount = total;
            _allDirty = false;
            _dirtyChunks.Clear();
        }

        /// <summary>
        /// How many of a thinned segment's rank-sorted clumps have a rank below <paramref name="keep"/>:
        /// the prefix the chunk path submits (<see cref="GrassThinning.CountBelow"/>), from the ranks
        /// taken at the regather rather than recomputed per probe.
        /// </summary>
        public static int CountBelow(IReadOnlyList<float> ranks, int start, int count, float keep)
        {
            if (keep >= 1f) return count;
            int lo = 0, hi = count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (ranks[start + mid] < keep) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }

        /// <summary>The finest level's bounds as a sphere in object space, plus half a metre for the
        /// wind's bow, which leans a blade past its rest-pose box. Coarser levels sit inside it.</summary>
        static Vector4 SphereOf(ModulePart[][] levels)
        {
            var bounds = new Bounds();
            bool any = false;
            foreach (ModulePart[] level in levels)
                foreach (ModulePart part in level)
                {
                    // The instance matrix already carries the part's local transform (the bucket
                    // holds placement × Local), so the mesh's own bounds are the object space.
                    if (part.Mesh == null) continue;
                    if (!any) { bounds = part.Mesh.bounds; any = true; }
                    else bounds.Encapsulate(part.Mesh.bounds);
                }
            return new Vector4(bounds.center.x, bounds.center.y, bounds.center.z, bounds.extents.magnitude + 0.5f);
        }

        void Upload(Group g)
        {
            if (g.Capacity < g.Matrices.Length || g.Instances == null)
            {
                g.Dispose();
                g.Capacity = g.Matrices.Length;
                g.Instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, g.Capacity, 64);
                g.InstanceSegment = new GraphicsBuffer(GraphicsBuffer.Target.Structured, g.Capacity, sizeof(uint));
                for (int l = 0; l < MaxLevels; l++)
                    g.Visible[l] = new GraphicsBuffer(GraphicsBuffer.Target.Append, g.Capacity, sizeof(uint));

                int entries = 0;
                g.ArgOffset = new int[g.Levels.Length];
                for (int l = 0; l < g.Levels.Length; l++)
                {
                    g.ArgOffset[l] = entries;
                    foreach (ModulePart part in g.Levels[l])
                    {
                        _argScratch[entries++] = new GraphicsBuffer.IndirectDrawIndexedArgs
                        {
                            indexCountPerInstance = part.Mesh.GetIndexCount(part.Submesh),
                            instanceCount = 0,
                            startIndex = part.Mesh.GetIndexStart(part.Submesh),
                            baseVertexIndex = part.Mesh.GetBaseVertex(part.Submesh),
                            startInstance = 0,
                        };
                    }
                }
                g.Args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, Mathf.Max(1, entries),
                    GraphicsBuffer.IndirectDrawIndexedArgs.size);
                g.Args.SetData(_argScratch, 0, 0, entries);
                for (int l = 0; l < MaxLevels; l++)
                {
                    g.Props[l].SetBuffer(ShaderInstancesId, g.Instances);
                    g.Props[l].SetBuffer(ShaderVisibleId, g.Visible[l]);
                }
                g.FullUpload = true;
            }

            if (g.FullUpload)
            {
                g.Instances!.SetData(g.Matrices, 0, 0, g.Used);
                g.InstanceSegment!.SetData(g.SegmentOf, 0, 0, g.Used);
            }
            else
            {
                foreach ((int start, int count) in g.DirtyRanges)
                {
                    g.Instances!.SetData(g.Matrices, start, start, count);
                    g.InstanceSegment!.SetData(g.SegmentOf, start, start, count);
                }
            }
            g.FullUpload = false;
            g.DirtyRanges.Clear();
        }

        /// <summary>
        /// Cull and draw the groups on one layer. <paramref name="decide"/> is the renderer's per
        /// segment decision (drawn at all, level, survivors); <paramref name="materialFor"/> the
        /// material the chunk path would have used for a part of this group at this layer's shade,
        /// cloned once here with the indirect keyword. Returns the draw calls issued; the instances
        /// the renderer offered the cull come back in <paramref name="offered"/> (the GPU decides how
        /// many are drawn, and that count never comes back to the CPU).
        /// </summary>
        public int DrawLayer(int layer, Plane[] frustum, Func<Group, Segment, Decision> decide,
            Func<Group, ModulePart, Material?> materialFor, int gameObjectLayer, Bounds worldBounds, bool submit,
            out int offered, out int atCoarserLevels, out int thinnedAway)
        {
            offered = atCoarserLevels = thinnedAway = 0;
            for (int p = 0; p < 6 && p < frustum.Length; p++)
                _planes[p] = new Vector4(frustum[p].normal.x, frustum[p].normal.y, frustum[p].normal.z, frustum[p].distance);

            int calls = 0;
            foreach (Group g in _ordered)
            {
                if (g.Layer != layer || g.Live == 0 || g.Instances == null) continue;

                int segments = g.Segments.Count;
                if (g.SegmentScratch.Length < segments) g.SegmentScratch = new SegmentParam[Mathf.NextPowerOfTwo(segments)];
                int any = 0;
                for (int s = 0; s < segments; s++)
                {
                    Segment segment = g.Segments[s];
                    // An empty or dead slot draws nothing and is not the renderer's to decide.
                    Decision d = segment.Count == 0 || segment.ChunkIndex < 0 ? default : decide(g, segment);
                    int level = Mathf.Clamp(d.Level, 0, g.Levels.Length - 1);
                    g.SegmentScratch[s] = new SegmentParam { Start = segment.Start, Submitted = d.Submitted, Level = level };
                    any += d.Submitted;
                    offered += d.Submitted * g.Levels[level].Length;
                    if (level > 0) atCoarserLevels += d.Submitted;
                    if (g.Thinned && d.Drawn) thinnedAway += segment.Count - d.Submitted;
                }
                if (any == 0 || !submit) continue;

                if (g.SegmentCapacity < segments)
                {
                    g.SegmentParams?.Release();
                    g.SegmentCapacity = Mathf.NextPowerOfTwo(segments);
                    g.SegmentParams = new GraphicsBuffer(GraphicsBuffer.Target.Structured, g.SegmentCapacity, 16);
                }
                g.SegmentParams!.SetData(g.SegmentScratch, 0, 0, segments);

                for (int l = 0; l < MaxLevels; l++) g.Visible[l]!.SetCounterValue(0);
                _cull!.SetBuffer(_kernel, InstancesId, g.Instances);
                _cull.SetBuffer(_kernel, InstanceSegmentId, g.InstanceSegment!);
                _cull.SetBuffer(_kernel, SegmentsId, g.SegmentParams);
                for (int l = 0; l < MaxLevels; l++) _cull.SetBuffer(_kernel, VisibleIds[l], g.Visible[l]!);
                _cull.SetVectorArray(PlanesId, _planes);
                _cull.SetVector(CentreRadiusId, g.CentreRadius);
                _cull.SetInt(CountId, g.Used);
                _cull.Dispatch(_kernel, (g.Used + ThreadGroup - 1) / ThreadGroup, 1, 1);

                for (int l = 0; l < g.Levels.Length; l++)
                {
                    ModulePart[] parts = g.Levels[l];
                    for (int k = 0; k < parts.Length; k++)
                    {
                        int entry = g.ArgOffset[l] + k;
                        // The kept count lands in this part's command's instanceCount (the second uint).
                        GraphicsBuffer.CopyCount(g.Visible[l]!, g.Args!,
                            entry * GraphicsBuffer.IndirectDrawIndexedArgs.size + sizeof(uint));
                        ModulePart part = parts[k];
                        // A material the chunk path could not give (its shader switched away mid-frame)
                        // draws nothing rather than throwing; the next frame's eligibility check sends
                        // the kind back to the chunk path.
                        Material? source = materialFor(g, part);
                        if (source == null) continue;
                        var rp = new RenderParams(IndirectMaterial(source))
                        {
                            worldBounds = worldBounds,
                            layer = gameObjectLayer,
                            receiveShadows = true,
                            shadowCastingMode = ShadowCastingMode.Off,
                            matProps = g.Props[l],
                        };
                        Graphics.RenderMeshIndirect(rp, part.Mesh, g.Args!, 1, entry);
                        calls++;
                    }
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

        /// <summary>Whether any group holds instances on this layer.</summary>
        public bool HasLayer(int layer)
        {
            foreach (Group g in _ordered) if (g.Layer == layer && g.Live > 0) return true;
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
