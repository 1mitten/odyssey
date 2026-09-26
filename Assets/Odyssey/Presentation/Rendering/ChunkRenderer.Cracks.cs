#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Cracks (design 57): a struck wall and a face being mined drawn broken, in three stages, by
    /// drawing the cell's own meshes a second time in <c>Odyssey/Crack</c> — a multiply over what is
    /// already on screen, so the wall keeps its material, its light and its colour and only gains
    /// the damage. Drawn only: nothing of it is in a cell, a save or the hash.
    ///
    /// <para><b>The thing as drawn, never a copy of it.</b> What is cracked is exactly what
    /// <see cref="ChunkMesher.MeshCell"/> emits for the cell — the selection highlight's route
    /// (design 44 §3) — so a wall's core and panels, its walls-down stump and a rock's boulder are
    /// all cracked by one rule, and nothing here can drift from the chunk the day either changes.</para>
    ///
    /// <para><b>Meshed once, not once a frame.</b> A cracked cell keeps its own scratch batch and
    /// meshes it again only when its chunk's version moves — a neighbour built, the cell itself
    /// changed — or when it turns from a wall into ground. A cell that stops being cracked gives its
    /// batch back the same frame.</para>
    ///
    /// <para><b>What it costs</b> (P10): the instances are gathered by mesh, submesh and stage and
    /// go out as one instanced call each, so a fight's worth of cracked wall is a handful of calls
    /// however many cells; a mined face's ground skin, where it has one, is one call a cell, and a
    /// colony mines a few cells at once. Nothing cracked is nothing submitted.</para>
    /// </summary>
    public sealed partial class ChunkRenderer
    {
        /// <summary>Whether cracks are drawn. A bench turns it off to time the frame without them.</summary>
        public bool Cracks { get; set; } = true;

        /// <summary>Cells drawn cracked last frame. A measurement.</summary>
        public int CrackedCellsDrawn { get; private set; }

        /// <summary>Instanced calls the cracks took last frame. A measurement; also counted in <see cref="DrawCalls"/>.</summary>
        public int CrackDrawCalls { get; private set; }

        /// <summary>Meshes drawn cracked last frame, across every call. A measurement.</summary>
        public int CrackInstancesDrawn { get; private set; }

        /// <summary>Cracked cells meshed again last frame — new, or their chunk changed. A measurement.</summary>
        public int CrackCellsMeshed { get; private set; }

        /// <summary>Cells holding a scratch batch now. Exposed for tests: it must fall to nought.</summary>
        public int CrackCellsHeld => _crackEntries.Count;

        /// <summary>
        /// Whether the crack shader is present, so a caller can keep its older sign of progress
        /// (the mining cut slab) for a player whose build stripped it. Asked once.
        /// </summary>
        public bool CracksAvailable => Cracks && CrackMaterial(1) != null;

        sealed class CrackEntry
        {
            public readonly ChunkBatch Batch = new ChunkBatch();
            public int Version = int.MinValue;
            public bool Ground;
            public bool Seen;
        }

        sealed class CrackGroup
        {
            public Mesh Mesh = null!;
            public int Submesh;
            public int Stage;
            public Matrix4x4[] Matrices = new Matrix4x4[16];
            public int Count;
        }

        readonly Dictionary<int, CrackEntry> _crackEntries = new Dictionary<int, CrackEntry>();
        readonly List<int> _crackGone = new List<int>();
        readonly Dictionary<long, CrackGroup> _crackGroupIndex = new Dictionary<long, CrackGroup>();
        readonly List<CrackGroup> _crackGroups = new List<CrackGroup>();
        readonly Material?[] _crackMaterials = new Material?[CrackModel.Stages + 1];
        Shader? _crackShader;
        bool _crackLooked;

        static readonly int CoverageId = Shader.PropertyToID("_Coverage");
        static readonly int WidthId = Shader.PropertyToID("_Width");
        static readonly int DarknessId = Shader.PropertyToID("_Darkness");
        static readonly int GrimeId = Shader.PropertyToID("_Grime");
        static readonly int FineId = Shader.PropertyToID("_Fine");

        /// <summary>
        /// Each stage's look (design 57 §4), index 1 to 3: how much of the crack network shows, how
        /// wide a crack is in metres, how dark a crack is (the multiply at its centre), how much the
        /// whole surface is darkened, and how much of the finer second network shows. Tuned by eye
        /// against the owner's first look; the stage thresholds themselves are
        /// <see cref="CrackModel"/>'s.
        /// </summary>
        static readonly Vector4[] StageLook =
        {
            Vector4.zero,
            // coverage, width (m), darkness, grime
            new Vector4(0.35f, 0.012f, 0.45f, 0.00f),
            new Vector4(0.70f, 0.022f, 0.30f, 0.08f),
            new Vector4(1.00f, 0.035f, 0.18f, 0.16f),
        };

        /// <summary>How much of the finer network each stage shows: none, then a little, then all.</summary>
        static readonly float[] StageFine = { 0f, 0f, 0.4f, 1f };

        Material? CrackMaterial(int stage)
        {
            if (!_crackLooked)
            {
                _crackLooked = true;
                _crackShader = Shader.Find("Odyssey/Crack");
                if (_crackShader == null)
                    Debug.LogWarning("Odyssey/Crack shader not found; mining keeps its cut slab and walls do not crack.");
            }
            if (_crackShader == null || stage < 1 || stage > CrackModel.Stages) return null;

            Material? material = _crackMaterials[stage];
            if (material != null) return material;

            Vector4 look = StageLook[stage];
            material = new Material(_crackShader)
            {
                name = "Odyssey/Crack#" + stage,
                enableInstancing = true,
            };
            material.SetFloat(CoverageId, look.x);
            material.SetFloat(WidthId, look.y);
            material.SetFloat(DarknessId, look.z);
            material.SetFloat(GrimeId, look.w);
            material.SetFloat(FineId, StageFine[stage]);
            _crackMaterials[stage] = material;
            return material;
        }

        /// <summary>
        /// Draw every cell <see cref="CrackModel.Gather"/> listed, after <see cref="Render"/> has
        /// set the slice this frame draws with. Call once a frame, with an empty list when nothing is
        /// cracked, so cells that healed or came down give their batches back.
        /// </summary>
        public void DrawCracks(List<CrackedCell> cells)
        {
            CrackedCellsDrawn = 0;
            CrackDrawCalls = 0;
            CrackInstancesDrawn = 0;
            CrackCellsMeshed = 0;

            foreach (CrackEntry held in _crackEntries.Values) held.Seen = false;

            if (Cracks && cells.Count > 0 && CrackMaterial(1) != null)
            {
                for (int g = 0; g < _crackGroups.Count; g++) _crackGroups[g].Count = 0;
                GridSize size = _model.Size;
                bool cull = CullToFrustum && ActiveFrustum != null;

                for (int i = 0; i < cells.Count; i++)
                {
                    CrackedCell cracked = cells[i];
                    int index = cracked.CellIndex;
                    if ((uint)index >= (uint)size.CellCount) continue;

                    if (!_crackEntries.TryGetValue(index, out CrackEntry? entry))
                    {
                        entry = new CrackEntry();
                        _crackEntries[index] = entry;
                    }
                    entry.Seen = true;

                    CellRef cell = size.FromIndex(index);
                    int version = _model.ChunkVersion(_model.Chunks.ChunkIndexOfCell(cell));
                    if (entry.Version != version || entry.Ground != cracked.Ground)
                    {
                        _mesher.MeshCell(entry.Batch, index, cracked.Ground);
                        entry.Version = version;
                        entry.Ground = cracked.Ground;
                        CrackCellsMeshed++;
                    }

                    if (cull && !GeometryUtility.TestPlanesAABB(ActiveFrustum!, entry.Batch.Bounds)) continue;

                    bool lowered = _drawnSlice != null && _drawnSlice.LowersWallsOn(_drawnLayer, cell.Y);
                    bool hideStacked = _drawnSlice != null && _drawnSlice.HidesStackedOn(_drawnLayer, cell.Y);
                    int stage = cracked.Stage;

                    GatherCracks(entry.Batch.Body, stage, hideStacked);
                    GatherCracks(entry.Batch.Roof, stage, hideStacked);
                    GatherCracks(lowered ? entry.Batch.Stumps : entry.Batch.Walls, stage, hideStacked);

                    // A mined face's ground skin is its own mesh, one per cell: drawn as it stands.
                    GroundSkinMesh skin = entry.Batch.Skin;
                    if (cracked.Ground && skin.Mesh != null)
                    {
                        var rp = CrackParams(stage);
                        for (int g = 0; g < skin.GroupCount; g++)
                        {
                            if (SubmitToGpu) Graphics.RenderMesh(rp, skin.Mesh, g, Matrix4x4.identity);
                            DrawCalls++;
                            CrackDrawCalls++;
                            CrackInstancesDrawn++;
                        }
                    }

                    CrackedCellsDrawn++;
                }

                SubmitCrackGroups();
            }

            // Give back the batches of cells no longer cracked: repaired, cleared or come down.
            _crackGone.Clear();
            foreach (KeyValuePair<int, CrackEntry> pair in _crackEntries)
                if (!pair.Value.Seen) _crackGone.Add(pair.Key);
            for (int i = 0; i < _crackGone.Count; i++)
            {
                _crackEntries[_crackGone[i]].Batch.Dispose();
                _crackEntries.Remove(_crackGone[i]);
            }
        }

        /// <summary>
        /// Every instance of every part in these buckets, into its (mesh, submesh, stage) group —
        /// the same walk as the selection highlight's <c>CollectBuckets</c>, so the parts and the
        /// matrices are the ones the chunk drew.
        /// </summary>
        void GatherCracks(List<InstanceBucket> buckets, int stage, bool hideStacked)
        {
            for (int b = 0; b < buckets.Count; b++)
            {
                InstanceBucket bucket = buckets[b];
                if (bucket.Count == 0) continue;
                if (hideStacked && bucket.Stacked) continue;
                ResolvedModule resolved = _model.Library[bucket.Module];
                ModulePart[] parts = resolved.DrawsByLevel ? resolved.Lods[0].Parts : resolved.Parts;
                int first = resolved.DrawsByLevel ? 0 : bucket.Part;
                int last = resolved.DrawsByLevel ? parts.Length : bucket.Part + 1;
                for (int p = first; p < last && p < parts.Length; p++)
                {
                    Mesh mesh = parts[p].Mesh;
                    if (mesh == null) continue;
                    CrackGroup group = CrackGroupFor(mesh, parts[p].Submesh, stage);
                    for (int k = 0; k < bucket.Count; k++)
                    {
                        if (group.Count == group.Matrices.Length)
                            System.Array.Resize(ref group.Matrices, group.Matrices.Length * 2);
                        group.Matrices[group.Count++] = bucket.Matrices[k];
                    }
                }
            }
        }

        CrackGroup CrackGroupFor(Mesh mesh, int submesh, int stage)
        {
            long key = ((long)mesh.GetInstanceID() << 16) ^ ((long)submesh << 4) ^ stage;
            if (_crackGroupIndex.TryGetValue(key, out CrackGroup? group)
                && group.Mesh == mesh && group.Submesh == submesh && group.Stage == stage)
                return group;

            group = new CrackGroup { Mesh = mesh, Submesh = submesh, Stage = stage };
            _crackGroupIndex[key] = group;
            _crackGroups.Add(group);
            return group;
        }

        void SubmitCrackGroups()
        {
            for (int g = 0; g < _crackGroups.Count; g++)
            {
                CrackGroup group = _crackGroups[g];
                if (group.Count == 0) continue;
                var rp = CrackParams(group.Stage);
                for (int sent = 0; sent < group.Count; sent += MaxInstancesPerCall)
                {
                    int n = Mathf.Min(MaxInstancesPerCall, group.Count - sent);
                    if (SubmitToGpu) Graphics.RenderMeshInstanced(rp, group.Mesh, group.Submesh, group.Matrices, n, sent);
                    DrawCalls++;
                    CrackDrawCalls++;
                    InstancesDrawn += n;
                    CrackInstancesDrawn += n;
                }
            }
        }

        RenderParams CrackParams(int stage) => new RenderParams(CrackMaterial(stage)!)
        {
            layer = GameObjectLayer,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
        };

        void DisposeCracks()
        {
            foreach (CrackEntry entry in _crackEntries.Values) entry.Batch.Dispose();
            _crackEntries.Clear();
            _crackGroupIndex.Clear();
            _crackGroups.Clear();
            for (int i = 0; i < _crackMaterials.Length; i++)
            {
                Material? material = _crackMaterials[i];
                if (material == null) continue;
                if (Application.isPlaying) Object.Destroy(material);
                else Object.DestroyImmediate(material);
                _crackMaterials[i] = null;
            }
        }
    }
}
