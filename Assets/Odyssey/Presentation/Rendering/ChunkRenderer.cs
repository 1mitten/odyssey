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
        public void RenderActors(
            WorldSnapshot snapshot, int activeLayer, SliceSettings slice, Material material,
            float tickAlpha = 0f, int movePerTick = 0)
        {
            if (snapshot.PawnCount == 0 && snapshot.ThingCount == 0) return;
            int lowest = Mathf.Max(0, slice.LowestDrawnLayer(activeLayer));

            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                var cell = pawns[i].Cell;
                if (cell.Y < lowest || cell.Y > activeLayer) continue;

                // Glide between cells rather than snapping. The simulation is discrete and
                // integer, which determinism requires; this is a presentation facade over it.
                Vector3 drift = Vector3.zero;
                if (pawns[i].MovePercent > 0)
                {
                    Vector3 from = CellMetrics.FloorCentre(cell);
                    Vector3 to = CellMetrics.FloorCentre(pawns[i].NextCell);
                    // Carry the pawn on through the part-tick this frame sits in, so the walk
                    // stays smooth when frames outpace ticks. Clamped so it never runs past the
                    // cell it is entering, which would look like a stumble.
                    float percent = pawns[i].MovePercent + movePerTick * tickAlpha;
                    drift = (to - from) * (Mathf.Clamp(percent, 0f, 100f) * 0.01f);
                }
                // Colonists are the thing the player is watching, so they are drawn deliberately
                // larger than life: a true-to-scale 0.9 m figure is a few pixels once the camera
                // pulls back, which is how five colonists managed to be invisible on a 60-cell map.
                //
                // Body plus a beacon floating above it. The beacon is what makes a colonist
                // findable at a glance: it sits clear of the terrain, so it reads against grass,
                // stone or a building roof without the player hunting for a shape among cells.
                DrawMarker(material, cell, new Vector3(1.4f, 2.6f, 1.4f), 1.3f, drift);
                DrawMarker(material, cell, new Vector3(0.7f, 0.7f, 0.7f), 3.6f, drift);
            }

            var things = snapshot.Things;
            for (int i = 0; i < things.Length; i++)
            {
                var cell = things[i].Cell;
                if (cell.Y < lowest || cell.Y > activeLayer) continue;
                DrawMarker(material, cell, new Vector3(1.2f, 0.8f, 1.2f), 0.4f);
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
