#nullable enable
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Draws the world: one instanced call per (chunk, mesh, material), for the layers the slice
    /// asks for and no others.
    ///
    /// There is no <c>GameObject</c> per cell and there never will be — a single layer of the
    /// scale-target map is 62,500 cells. Geometry is meshed per chunk into matrix arrays and
    /// submitted with <see cref="Graphics.RenderMeshInstanced"/>; a chunk is re-meshed only when
    /// the mirror says it changed, and the slice decides which chunks are submitted at all rather
    /// than submitting everything and clipping it.
    ///
    /// Depth shading and ghosting are chosen at draw time from the same buckets, so moving the
    /// slice up or down does no meshing work whatsoever.
    /// </summary>
    public sealed class ChunkRenderer
    {
        /// <summary>
        /// Instances per call. <c>RenderMeshInstanced</c> takes up to 1023, but the ceiling halves
        /// when the pipeline also needs previous-frame matrices, so the conservative figure is
        /// used and the cost of the extra calls is nil.
        /// </summary>
        public const int MaxInstancesPerCall = 511;

        readonly WorldRenderModel _model;
        readonly ChunkMesher _mesher;
        readonly MaterialCache _materials = new MaterialCache();
        readonly ChunkBatch?[] _batches;

        public ChunkRenderer(WorldRenderModel model)
        {
            _model = model;
            _mesher = new ChunkMesher(model);
            _batches = new ChunkBatch?[model.Chunks.Count];
        }

        /// <summary>Unity layer the geometry is drawn on. Set before the first frame.</summary>
        public int GameObjectLayer { get; set; }

        public bool CastShadows { get; set; } = true;

        /// <summary>
        /// Off, everything happens except the submission itself. That makes the draw-call and
        /// instance counts measurable in a headless editor run with no graphics device, which is
        /// the only way to get real numbers into a milestone report from CI.
        /// </summary>
        public bool SubmitToGpu { get; set; } = true;

        // ---- last-frame measurements, for the milestone report and the on-screen readout ----

        public int DrawCalls { get; private set; }
        public int InstancesDrawn { get; private set; }
        public int ChunksDrawn { get; private set; }
        public int ChunksMeshedThisFrame { get; private set; }
        public int MaterialCount => _materials.MaterialCount;

        /// <summary>Total chunks meshed since start. A large number in steady state is a bug.</summary>
        public int TotalChunksMeshed { get; private set; }

        public void Render(int activeLayer, SliceSettings slice)
        {
            DrawCalls = 0;
            InstancesDrawn = 0;
            ChunksDrawn = 0;
            ChunksMeshedThisFrame = 0;

            var size = _model.Size;
            int lowest = Mathf.Max(0, slice.LowestDrawnLayer(activeLayer));
            int highest = Mathf.Min(size.SizeY - 1, slice.HighestDrawnLayer(activeLayer, size.SizeY));
            int chunksPerLayer = _model.Chunks.ChunksX * _model.Chunks.ChunksZ;

            for (int layer = lowest; layer <= highest; layer++)
            {
                int steps = layer - activeLayer;
                bool above = steps > 0;
                bool ghost = above && slice.GhostsAbove;
                float alpha = ghost ? slice.AlphaAbove(steps) : 1f;
                if (ghost && alpha < 0.012f) continue;

                // The layer immediately below the active one is the floor being stood on, not a
                // storey beneath it, so it is lit as part of the active layer. Dimming it was
                // costing the ground a third of its brightness before a single light was applied,
                // and because the tint is a colour the shader reads in linear space, a 0.68
                // multiplier landed nearer 0.42 in practice. That alone made a healthy green
                // texture render as dark olive.
                float shade = above ? 1f : slice.ShadeBelow(-steps - 1);
                if (ghost) shade *= 1.15f; // translucent geometry reads darker than it is

                // The active layer's ceiling is the slab of the layer above it. Dropping it is
                // what makes interiors visible, and it is also exactly what roofs-off mode wants
                // for every layer it draws.
                bool drawRoof = true;
                if (slice.above != AboveMode.Full)
                {
                    if (above && slice.above == AboveMode.RoofsOff) drawRoof = false;
                    else if (steps == 1 && slice.suppressActiveCeiling) drawRoof = false;
                }

                int first = layer * chunksPerLayer;
                for (int i = 0; i < chunksPerLayer; i++)
                {
                    ChunkBatch batch = BatchFor(first + i);
                    if (batch.InstanceCount == 0) continue;
                    ChunksDrawn++;
                    DrawBuckets(batch, batch.Body, shade, ghost, alpha);
                    if (drawRoof) DrawBuckets(batch, batch.Roof, shade, ghost, alpha);
                }
            }
        }

        ChunkBatch BatchFor(int chunkIndex)
        {
            ChunkBatch? batch = _batches[chunkIndex];
            if (batch == null)
            {
                batch = new ChunkBatch();
                _batches[chunkIndex] = batch;
            }
            if (batch.Version != _model.Version)
            {
                _mesher.Mesh(batch, chunkIndex);
                ChunksMeshedThisFrame++;
                TotalChunksMeshed++;
            }
            return batch;
        }

        void DrawBuckets(ChunkBatch batch, System.Collections.Generic.List<InstanceBucket> buckets,
            float shade, bool ghost, float alpha)
        {
            for (int b = 0; b < buckets.Count; b++)
            {
                InstanceBucket bucket = buckets[b];
                if (bucket.Count == 0) continue;

                ModulePart part = _model.Library[bucket.Module].Parts[bucket.Part];
                ResolveColour(bucket.Tint, part.IsFallback, shade, out Color tint, out Color emission);
                Material material = _materials.Get(part.Material, tint, emission, ghost, alpha);

                // Terrain receives shadows but never casts them, and that is not a saving so much
                // as a correctness fix. Ground is a contiguous mass of cell-sized boxes; letting
                // each box cast meant the surface shadowed itself, and with a shadow map stretched
                // over a 300 m board the texel is far larger than a cell, so every ground tile
                // acned against its neighbours. The result was a dark cross-hatch over the whole
                // map that read as filth on the grass rather than as light.
                //
                // Nothing worth seeing is lost: a colonist, a wall or a tree still casts onto the
                // ground, which is what actually tells the eye where something is standing. It
                // also takes 14,400 instances per layer out of the shadow pass.
                bool terrain = TintCode.IsTerrain(bucket.Tint);
                var rp = new RenderParams(material)
                {
                    worldBounds = batch.Bounds,
                    layer = GameObjectLayer,
                    receiveShadows = !ghost,
                    shadowCastingMode = ghost || !CastShadows || terrain
                        ? ShadowCastingMode.Off
                        : ShadowCastingMode.On,
                };

                int drawn = 0;
                while (drawn < bucket.Count)
                {
                    int n = Mathf.Min(MaxInstancesPerCall, bucket.Count - drawn);
                    if (SubmitToGpu)
                        Graphics.RenderMeshInstanced(rp, part.Mesh, part.Submesh, bucket.Matrices, n, drawn);
                    drawn += n;
                    DrawCalls++;
                }
                InstancesDrawn += bucket.Count;
            }
        }

        static void ResolveColour(int tintCode, bool fallback, float shade, out Color tint, out Color emission)
        {
            int value = TintCode.Value(tintCode);
            if (TintCode.IsTerrain(tintCode))
            {
                // Over a primitive the tint is the only colour there is. Over a texture it grades
                // what the texture already says, which for grass means lifting a muted meadow
                // olive to the saturated green the reference art uses.
                tint = fallback ? StuffPalette.TerrainSolid(value) : StuffPalette.TerrainTint(value);
                emission = StuffPalette.TerrainEmissive(value);
            }
            else
            {
                tint = StuffPalette.For(value, overArt: !fallback);
                emission = Color.black;
            }

            tint = new Color(tint.r * shade, tint.g * shade, tint.b * shade, 1f);
            emission = new Color(emission.r * shade, emission.g * shade, emission.b * shade, 1f);
        }

        /// <summary>
        /// Draw the pawns and things the published snapshot contains, culled to the drawn layers.
        ///
        /// M1 has no pawns yet, so this normally draws nothing. It is here because it is the one
        /// place the renderer consumes <c>world.Views.Current</c> directly, and having it wired
        /// means the first pawn to exist appears without a rendering change.
        /// </summary>
        /// <param name="tickAlpha">How far this frame sits between two ticks, 0 to 1.</param>
        /// <param name="movePerTick">Cost units a pawn retires per tick, so a partial tick can be extrapolated.</param>
        /// <param name="drawnAsFigures">
        /// Pawn ids that already have a live animated figure in the scene, which must not also be
        /// drawn here. Without it a walking colonist renders twice — the figure and its own baked
        /// stand-in occupying the same cell — which reads as one colonist with a shadow problem
        /// rather than as two overlapping draws.
        /// </param>
        public void RenderActors(
            WorldSnapshot snapshot, int activeLayer, SliceSettings slice, Material material,
            float tickAlpha = 0f, int movePerTick = 0,
            System.Collections.Generic.HashSet<int>? drawnAsFigures = null)
        {
            if (snapshot.PawnCount == 0 && snapshot.ThingCount == 0) return;
            int lowest = Mathf.Max(0, slice.LowestDrawnLayer(activeLayer));

            ResolvedModule colonist = ColonistModule();
            bool hasFigure = colonist.UsesArt && !colonist.IsEmpty;
            int drawn = 0;

            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                var cell = pawns[i].Cell;
                if (cell.Y < lowest || cell.Y > activeLayer) continue;
                if (drawnAsFigures != null && drawnAsFigures.Contains(pawns[i].Id.Value)) continue;

                // Glide between cells rather than snapping. The simulation is discrete and
                // integer, which determinism requires; this is a presentation facade over it,
                // and it is shared with the animated figures so the two cannot disagree.
                Vector3 position = PawnPose.Of(pawns[i], tickAlpha, movePerTick, out Vector3 heading);

                if (!hasFigure)
                {
                    // No licensed art: the stand-in is a body and a beacon, both deliberately
                    // larger than life, because a true-to-scale figure is a few pixels once the
                    // camera pulls back. That is how five colonists managed to be invisible.
                    Vector3 drift = position - CellMetrics.FloorCentre(cell);
                    DrawMarker(material, cell, new Vector3(1.4f, 2.6f, 1.4f), 1.3f, drift);
                    DrawMarker(material, cell, new Vector3(0.7f, 0.7f, 0.7f), 3.6f, drift);
                    continue;
                }

                EnsureActorCapacity(pawns.Length);
                _actorPlacements[drawn++] = Matrix4x4.TRS(
                    position,
                    Quaternion.Euler(0f, FacingOf(pawns[i].Id, heading), 0f),
                    Vector3.one);

                // No beacon over a real figure. It was there to make a grey box findable, and
                // against the same orange the item markers use it read as one more piece of
                // clutter rather than as a colonist. Finding people at a glance is a job for the
                // interface, not for a cube floating over their heads.
            }

            if (drawn > 0) SubmitInstances(colonist, _actorPlacements, drawn, ref _actorMatrices);

            RenderThings(snapshot.Things, lowest, activeLayer, material);
        }

        // ---- loose items ----------------------------------------------------------------------

        int[] _itemModules = System.Array.Empty<int>();
        Matrix4x4[][] _itemPlacements = System.Array.Empty<Matrix4x4[]>();
        int[] _itemCounts = System.Array.Empty<int>();
        Matrix4x4[] _itemMatrices = new Matrix4x4[16];

        /// <summary>
        /// Draw the items lying on the ground, one instanced submission per item kind.
        ///
        /// Items are grouped by def before anything is submitted, because the alternative — a
        /// draw per item, which is what the stand-in marker did — costs a call for every ration
        /// crate on a map that will eventually hold thousands of them.
        /// </summary>
        void RenderThings(System.ReadOnlySpan<ThingView> things, int lowest, int activeLayer, Material fallback)
        {
            if (things.Length == 0) return;
            EnsureItemModules();
            System.Array.Clear(_itemCounts, 0, _itemCounts.Length);

            for (int i = 0; i < things.Length; i++)
            {
                CellRef cell = things[i].Cell;
                if (cell.Y < lowest || cell.Y > activeLayer) continue;

                int def = things[i].DefIndex;
                ResolvedModule? module = ItemModule(def);
                if (module == null || module.IsEmpty || !module.UsesArt)
                {
                    // No art for this kind — either the packs are absent or the def is newer than
                    // the catalogue. The stand-in marker is deliberately ugly so the gap shows.
                    DrawMarker(fallback, cell, new Vector3(1.2f, 0.8f, 1.2f), 0.4f);
                    continue;
                }

                AppendItem(def, Matrix4x4.TRS(
                    CellMetrics.FloorCentre(cell),
                    Quaternion.Euler(0f, YawOf(things[i].Id), 0f),
                    Vector3.one));
            }

            for (int def = 0; def < _itemCounts.Length; def++)
                if (_itemCounts[def] > 0)
                    SubmitInstances(_model.Library[_itemModules[def]], _itemPlacements[def],
                        _itemCounts[def], ref _itemMatrices);
        }

        void EnsureItemModules()
        {
            if (_itemModules.Length == ModuleIds.ItemModuleCount) return;

            _itemModules = new int[ModuleIds.ItemModuleCount];
            _itemCounts = new int[ModuleIds.ItemModuleCount];
            _itemPlacements = new Matrix4x4[ModuleIds.ItemModuleCount][];
            for (int i = 0; i < _itemModules.Length; i++)
            {
                // Pillar is only the shape of the primitive the library invents when the catalogue
                // has no row at all; in that case UsesArt is false and the marker is drawn
                // instead, so the invented shape is never what reaches the screen.
                _itemModules[i] = _model.Library.Resolve(ModuleIds.Item(i), ModuleShape.Pillar);
                _itemPlacements[i] = new Matrix4x4[16];
            }
        }

        ResolvedModule? ItemModule(int defIndex) =>
            defIndex >= 0 && defIndex < _itemModules.Length ? _model.Library[_itemModules[defIndex]] : null;

        void AppendItem(int def, in Matrix4x4 placement)
        {
            Matrix4x4[] into = _itemPlacements[def];
            if (_itemCounts[def] == into.Length)
            {
                System.Array.Resize(ref into, into.Length * 2);
                _itemPlacements[def] = into;
            }
            into[_itemCounts[def]++] = placement;
        }

        /// <summary>
        /// A stable bearing per item, so eight heaps of scrap are not eight copies of one heap.
        ///
        /// Derived from the id rather than drawn from a generator: presentation must not consume
        /// simulation randomness, and the same item has to face the same way after a reload.
        /// </summary>
        static float YawOf(ThingId id) => (id.Value * 137) % 360;

        // ---- colonist figures ---------------------------------------------------------------

        int _colonistModule = -1;
        Matrix4x4[] _actorPlacements = new Matrix4x4[16];
        Matrix4x4[] _actorMatrices = new Matrix4x4[16];
        readonly System.Collections.Generic.Dictionary<int, float> _facing =
            new System.Collections.Generic.Dictionary<int, float>();

        ResolvedModule ColonistModule()
        {
            if (_colonistModule < 0)
                _colonistModule = _model.Library.Resolve(ModuleIds.Colonist, ModuleShape.Pillar);
            return _model.Library[_colonistModule];
        }

        void EnsureActorCapacity(int count)
        {
            if (_actorPlacements.Length >= count) return;
            _actorPlacements = new Matrix4x4[count];
            _actorMatrices = new Matrix4x4[count];
        }

        /// <summary>
        /// Which way a colonist is turned, in degrees.
        ///
        /// Remembered per pawn rather than derived per frame, because the snapshot only says where
        /// someone is heading while they are actually moving. Deriving it fresh each frame would
        /// snap everyone back to a default bearing the instant they stopped, so a colonist would
        /// pivot on arrival every single time. Keeping the last real heading means they stand
        /// facing wherever they walked in from, which is what a person does.
        /// </summary>
        float FacingOf(PawnId id, Vector3 heading)
        {
            if (heading.sqrMagnitude > 1e-4f)
            {
                float yaw = PawnPose.YawOf(heading);
                _facing[id.Value] = yaw;
                return yaw;
            }
            return _facing.TryGetValue(id.Value, out float last) ? last : 0f;
        }

        /// <summary>
        /// Submit <paramref name="count"/> copies of a module, one instanced call per part.
        ///
        /// Shared by colonists and by loose items: both are placements published in a snapshot
        /// rather than cells in the mirror, and both want the art drawn the same way the world is.
        /// <paramref name="scratch"/> is the caller's own buffer for placement × part, grown here
        /// so a caller never has to size it against a part count it cannot see.
        /// </summary>
        void SubmitInstances(ResolvedModule module, Matrix4x4[] placements, int count,
            ref Matrix4x4[] scratch)
        {
            if (count == 0) return;
            if (scratch.Length < count) scratch = new Matrix4x4[count];

            var parts = module.Parts;
            for (int p = 0; p < parts.Length; p++)
            {
                ModulePart part = parts[p];
                for (int i = 0; i < count; i++)
                    scratch[i] = placements[i] * part.Local;

                // Through the cache, not the pack material directly: the clone is what carries
                // GPU instancing, and a pack material does not have it switched on.
                Material material = _materials.Get(
                    part.Material, Color.white, Color.black, ghost: false, alpha: 1f);

                var rp = new RenderParams(material)
                {
                    layer = GameObjectLayer,
                    shadowCastingMode = CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true,
                };

                int sent = 0;
                while (sent < count)
                {
                    int n = Mathf.Min(MaxInstancesPerCall, count - sent);
                    if (SubmitToGpu)
                        Graphics.RenderMeshInstanced(rp, part.Mesh, part.Submesh, scratch, n, sent);
                    sent += n;
                    DrawCalls++;
                }
                InstancesDrawn += count;
            }
        }

        void DrawMarker(Material material, CellRef cell, Vector3 size, float height, Vector3 drift = default)
        {
            var rp = new RenderParams(material) { layer = GameObjectLayer };
            Matrix4x4 m = Matrix4x4.TRS(
                CellMetrics.FloorCentre(cell) + Vector3.up * height + drift,
                Quaternion.identity, size);
            Graphics.RenderMesh(rp, PrimitiveMeshes.UnitCube, 0, m);
            DrawCalls++;
        }

        /// <summary>A translucent box over one cell, for the hovered or selected cell.</summary>
        public void DrawCellHighlight(CellRef cell, Color colour)
        {
            Material material = _materials.Get(_model.Library.FallbackMaterial, colour, colour * 0.6f,
                ghost: true, alpha: colour.a);
            var rp = new RenderParams(material) { layer = GameObjectLayer, shadowCastingMode = ShadowCastingMode.Off };
            Matrix4x4 m = Matrix4x4.TRS(
                CellMetrics.Centre(cell.X, cell.Z, cell.Y),
                Quaternion.identity,
                new Vector3(CellMetrics.SizeXZ, CellMetrics.SizeY, CellMetrics.SizeXZ) * 0.98f);
            Graphics.RenderMesh(rp, PrimitiveMeshes.UnitCube, 0, m);
            DrawCalls++;
        }

        public void Dispose() => _materials.Dispose();
    }
}
