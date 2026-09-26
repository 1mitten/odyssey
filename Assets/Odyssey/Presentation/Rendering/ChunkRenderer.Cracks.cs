#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Cracks (design 58): a struck wall and a face being mined drawn broken, on a ladder of levels
    /// (three stages for a wall, six for rock), by drawing the cell's own meshes a second time in
    /// <c>Odyssey/Crack</c> — a multiply over what is
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
    /// <para><b>What it costs</b> (P10): the instances are gathered by mesh, submesh and level and
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

            /// <summary>The level it was last drawn at: how cracked its pieces are if it breaks.</summary>
            public int Level;

            /// <summary>What stood in the cell when it was meshed — solid rock, the building's def —
            /// so the break watch can tell it came down from its being repaired (§7).</summary>
            public bool Solid;
            public ushort Def;
        }

        sealed class CrackGroup
        {
            public Mesh Mesh = null!;
            public int Submesh;
            public int Level;
            public Matrix4x4[] Matrices = new Matrix4x4[16];
            public int Count;
        }

        readonly Dictionary<int, CrackEntry> _crackEntries = new Dictionary<int, CrackEntry>();
        readonly List<int> _crackGone = new List<int>();
        readonly Dictionary<long, CrackGroup> _crackGroupIndex = new Dictionary<long, CrackGroup>();
        readonly List<CrackGroup> _crackGroups = new List<CrackGroup>();
        readonly Material?[] _crackMaterials = new Material?[CrackModel.Levels + 1];
        Shader? _crackShader;
        bool _crackLooked;

        static readonly int SeverityId = Shader.PropertyToID("_Severity");

        /// <summary>
        /// How broken a level looks to the shader, 0 to 1: the level over the ladder's length. The
        /// whole look — how far the cracks reach, how many rays, how wide, when the branches web
        /// the face, the grime — is one curve in <c>OdysseyCrack.hlsl</c> driven by this number, so
        /// a level added to the ladder needs no row of tuning here (design 58 §4).
        /// </summary>
        public static float SeverityOf(int level) => Mathf.Clamp01(level / (float)CrackModel.Levels);

        Material? CrackMaterial(int level)
        {
            if (!_crackLooked)
            {
                _crackLooked = true;
                _crackShader = Shader.Find("Odyssey/Crack");
                if (_crackShader == null)
                    Debug.LogWarning("Odyssey/Crack shader not found; mining keeps its cut slab and walls do not crack.");
            }
            if (_crackShader == null || level < 1 || level > CrackModel.Levels) return null;

            Material? material = _crackMaterials[level];
            if (material != null) return material;

            material = new Material(_crackShader)
            {
                name = "Odyssey/Crack#" + level,
                enableInstancing = true,
            };
            material.SetFloat(SeverityId, SeverityOf(level));
            _crackMaterials[level] = material;
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
                        // Listed again while it was being watched for a break — an order put
                        // back on a face — takes its batch back rather than meshing a second.
                        entry = ReclaimWatched(index) ?? new CrackEntry();
                        _crackEntries[index] = entry;
                    }
                    entry.Seen = true;
                    entry.Level = cracked.Level;

                    // Already gone from the mirror while still listed — the snapshot's views a
                    // publish behind its cells — keeps the batch as it stood, rather than meshing
                    // it again as nothing, and goes to the break watch now.
                    bool meshed = entry.Version != int.MinValue;
                    if (meshed && (entry.Ground
                            ? entry.Solid && !_model.IsSolid(index)
                            : entry.Def != 0 && _model.EdificeDef(index) != entry.Def))
                    {
                        entry.Seen = false;
                        continue;
                    }

                    CellRef cell = size.FromIndex(index);
                    int version = _model.ChunkVersion(_model.Chunks.ChunkIndexOfCell(cell));
                    if (entry.Version != version || entry.Ground != cracked.Ground)
                    {
                        _mesher.MeshCell(entry.Batch, index, cracked.Ground);
                        entry.Version = version;
                        entry.Ground = cracked.Ground;
                        entry.Solid = _model.IsSolid(index);
                        entry.Def = _model.EdificeDef(index);
                        CrackCellsMeshed++;
                    }

                    if (cull && !GeometryUtility.TestPlanesAABB(ActiveFrustum!, entry.Batch.Bounds)) continue;

                    bool lowered = _drawnSlice != null && _drawnSlice.LowersWallsOn(_drawnLayer, cell.Y);
                    bool hideStacked = _drawnSlice != null && _drawnSlice.HidesStackedOn(_drawnLayer, cell.Y);
                    int level = cracked.Level;

                    GatherCracks(entry.Batch.Body, level, hideStacked);
                    GatherCracks(entry.Batch.Roof, level, hideStacked);
                    GatherCracks(lowered ? entry.Batch.Stumps : entry.Batch.Walls, level, hideStacked);

                    // A mined face's ground skin is its own mesh, one per cell: drawn as it stands.
                    GroundSkinMesh skin = entry.Batch.Skin;
                    if (cracked.Ground && skin.Mesh != null)
                    {
                        var rp = CrackParams(level);
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

            // A cell no longer cracked was repaired, cancelled, or came down. Which of those is not
            // known yet — the order can leave the snapshot a frame before the cell's new geometry
            // reaches the mirror — so its batch, the thing as it last stood, goes to the break
            // watch, which either breaks it apart or gives the batch back (§7).
            _crackGone.Clear();
            foreach (KeyValuePair<int, CrackEntry> pair in _crackEntries)
                if (!pair.Value.Seen) _crackGone.Add(pair.Key);
            for (int i = 0; i < _crackGone.Count; i++)
            {
                Watch(_crackGone[i], _crackEntries[_crackGone[i]]);
                _crackEntries.Remove(_crackGone[i]);
            }

            DrawBreaks();
        }

        /// <summary>
        /// Every instance of every part in these buckets, into its (mesh, submesh, level) group —
        /// the same walk as the selection highlight's <c>CollectBuckets</c>, so the parts and the
        /// matrices are the ones the chunk drew.
        /// </summary>
        void GatherCracks(List<InstanceBucket> buckets, int level, bool hideStacked)
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
                    CrackGroup group = CrackGroupFor(mesh, parts[p].Submesh, level);
                    for (int k = 0; k < bucket.Count; k++)
                    {
                        if (group.Count == group.Matrices.Length)
                            System.Array.Resize(ref group.Matrices, group.Matrices.Length * 2);
                        group.Matrices[group.Count++] = bucket.Matrices[k];
                    }
                }
            }
        }

        CrackGroup CrackGroupFor(Mesh mesh, int submesh, int level)
        {
            long key = ((long)mesh.GetInstanceID() << 16) ^ ((long)submesh << 4) ^ level;
            if (_crackGroupIndex.TryGetValue(key, out CrackGroup? group)
                && group.Mesh == mesh && group.Submesh == submesh && group.Level == level)
                return group;

            group = new CrackGroup { Mesh = mesh, Submesh = submesh, Level = level };
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
                var rp = CrackParams(group.Level);
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

        RenderParams CrackParams(int level) => new RenderParams(CrackMaterial(level)!)
        {
            layer = GameObjectLayer,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
        };

        void DisposeCracks()
        {
            foreach (CrackEntry entry in _crackEntries.Values) entry.Batch.Dispose();
            DisposeBreaks();
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
