#nullable enable
using System.Collections.Generic;
using Odyssey.Presentation.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Turns one chunk of the cell mirror into instanced draw buckets.
    ///
    /// Everything interesting about how the world looks is decided here, and all of it is per
    /// cell arithmetic with no allocation in the steady state:
    ///
    /// - **Solid strata are face-culled.** A rock cell with six solid neighbours is not drawn.
    ///   Underground layers are mostly solid, so this is the difference between drawing a chunk
    ///   and drawing its surface.
    /// - **Walls are drawn on faces, not in cells.** A wall occupies a whole 2.5 m cell, so
    ///   drawing a wall-shaped box would give a 2.5 m thick slab. Instead each exposed vertical
    ///   face of a wall cell takes a panel, which is what makes a stamped shell read as a
    ///   building from outside and as a room from inside.
    /// - **Doors get one leaf, in the middle.** Two panels on opposite faces of the same cell
    ///   would read as an airlock.
    /// - **Stairs climb.** The lower half sits on the floor, the upper half 1.5 m up, both facing
    ///   the direction of travel — which is the kit's native geometry (<c>e-01</c>).
    /// </summary>
    public sealed class ChunkMesher
    {
        readonly Dictionary<long, int> _bodyIndex = new Dictionary<long, int>();
        readonly Dictionary<long, int> _roofIndex = new Dictionary<long, int>();
        readonly WorldRenderModel _model;

        public ChunkMesher(WorldRenderModel model) => _model = model;

        public void Mesh(ChunkBatch batch, int chunkIndex)
        {
            _model.ChunkBounds(chunkIndex, out int x0, out int z0, out int y, out int x1, out int z1);

            batch.ChunkIndex = chunkIndex;
            batch.Layer = y;
            batch.Bounds = ChunkWorldBounds(x0, z0, y, x1, z1);
            batch.Clear();
            _bodyIndex.Clear();
            _roofIndex.Clear();
            for (int i = 0; i < batch.Body.Count; i++)
                _bodyIndex[KeyOf(batch.Body[i])] = i;
            for (int i = 0; i < batch.Roof.Count; i++)
                _roofIndex[KeyOf(batch.Roof[i])] = i;

            var size = _model.Size;
            for (int z = z0; z < z1; z++)
            for (int x = x0; x < x1; x++)
            {
                int index = size.Index(x, z, y);
                EmitTerrain(batch, index, x, z, y);
                EmitScatter(batch, index, x, z, y);
                EmitFloor(batch, index, x, z, y);
                EmitEdifice(batch, index, x, z, y);
            }

            batch.Version = _model.Version;
        }

        /// <summary>
        /// The chunk's cell volume, padded.
        ///
        /// The padding is not cosmetic. <c>RenderParams.worldBounds</c> is what culling believes,
        /// and modules legitimately overhang their cells: a wall panel straddles the face it sits
        /// on, a pillar is 3.02 m in a 3.00 m layer, the upper half of a stair reaches 3.33 m. Too
        /// tight a box and whole chunks vanish at the screen edge, which is a horrible bug to
        /// track down because the geometry is provably correct.
        /// </summary>
        const float BoundsPadding = 2f;

        /// <summary>
        /// How far the relief can carry a cell out of its own layer, in metres.
        ///
        /// Two parts, and leaving out the second is the easy mistake: the cell is lifted by up to
        /// the amplitude, and then *tilted*, so its high corner rises a further half-diagonal times
        /// the steepest slope the field can reach. Derived rather than folded into the existing
        /// padding on the grounds that 2 m happens to cover it, because the day somebody raises the
        /// amplitude that coincidence becomes the culling bug this padding already exists to stop.
        /// Only Y moves: a shear shifts nothing horizontally.
        /// </summary>
        static float ReliefReach()
        {
            float amplitude = Mathf.Abs(GroundRelief.Amplitude);
            if (amplitude == 0f) return 0f;
            return amplitude + GroundRelief.MaxSlope(amplitude) * CellMetrics.SizeXZ;
        }

        static Bounds ChunkWorldBounds(int x0, int z0, int y, int x1, int z1)
        {
            float relief = ReliefReach();
            var bounds = new Bounds();
            bounds.SetMinMax(
                new Vector3(
                    x0 * CellMetrics.SizeXZ - BoundsPadding,
                    y * CellMetrics.SizeY - BoundsPadding - relief,
                    z0 * CellMetrics.SizeXZ - BoundsPadding),
                new Vector3(
                    x1 * CellMetrics.SizeXZ + BoundsPadding,
                    (y + 1) * CellMetrics.SizeY + BoundsPadding + relief,
                    z1 * CellMetrics.SizeXZ + BoundsPadding));
            return bounds;
        }

        // --------------------------------------------------------------- cells

        /// <summary>
        /// How far up its own cell a water surface is drawn, as a fraction of the cell height.
        ///
        /// Both depths use the same number, and that is the point: a body of water has one
        /// level. Shallow and deep sit side by side in the same pond, so drawing them at
        /// different heights would put a step in the middle of the surface. Depth is told by
        /// colour and opacity instead — which is also why the channel is one cell deep whatever
        /// the depth, since a two-layer deep core would put neighbouring surface cells two layers
        /// apart and break the invariant that keeps the board walkable.
        ///
        /// Just under a full cell, so the water nearly fills the channel it was cut into and a
        /// bank reads as a low bank rather than as the lip of a dry ditch.
        /// </summary>
        public static float WaterSurface { get; set; } = 0.72f;

        void EmitTerrain(ChunkBatch batch, int index, int x, int z, int y)
        {
            ushort terrain = _model.Terrain(index);
            if (terrain == CoreContent.TerrainAir) return;

            int module = _model.TerrainModule(index);
            if (module == 0) return;

            if (NaturalContent.IsWater(terrain))
            {
                EmitWater(batch, module, terrain, x, z, y);
                return;
            }

            int tint = TintCode.Daylit(TintCode.Terrain(terrain), OpenToTheSky(index, y));
            // Terrain is the ground, so it is the one thing that is draped rather than lifted: the
            // cell is tilted onto the tangent plane of the relief field so its top face follows
            // the slope. Everything built or standing on it is lifted instead - see GroundRelief.
            Matrix4x4 at = GroundRelief.Drape(CellMetrics.FloorCentre(x, z, y));

            if (!_model.IsSolid(index))
            {
                // A surface material: pavement, soil, rubble. One tile on the cell floor, and it
                // belongs to the roof list so that a storey above the slice can drop its ground.
                AddRoof(batch, module, tint, at);
                return;
            }

            if (!HasExposedFace(index, x, z, y)) return;

            if (_model.IsStone(index))
            {
                // Stone is drawn as one of several chipped lumps, turned to one of four bearings.
                // Both come from a hash of the cell, so a cliff face does not rearrange itself
                // every time somebody digs a cell in the same chunk — see RockLook.
                //
                // The turn is composed on the left of the module's own local transform, which is
                // a vertical shift and a scale equal in x and z; a rotation about the vertical
                // commutes with both, so the lump turns about its own axis and still fills its
                // cell exactly. Three quarters of the variety for no extra mesh and no extra draw.
                int variant = RockLook.Variant(x, z, y);
                Matrix4x4 turned = at * Matrix4x4.Rotate(Quaternion.Euler(0f, RockLook.Yaw(x, z, y), 0f));
                AddBody(batch, _model.StoneModule(index, variant), tint, turned);
                return;
            }

            if (_model.IsEarth(index))
            {
                // Earth is a block with an uneven top, and — only where a side of it can be seen —
                // coursed walls as well. The two are different meshes and so different buckets, so
                // the question is asked per cell rather than paid for everywhere: on a flat board
                // a surface cell's four same-layer neighbours are solid too and nothing but its
                // top is ever visible. Sides appear at terrace risers and at the walls of a
                // cutting, which are a small fraction of what is drawn and exactly the places the
                // ruled 3 m rectangle was the fault.
                //
                // The turn is composed on the left of the module's own local transform, for the
                // reason RockLook.Yaw gives: that local is a vertical shift and a scale equal in x
                // and z, and a rotation about the vertical commutes with both, so the block turns
                // about its own axis and still fills its cell exactly.
                int variant = GroundLook.Variant(x, z, y);
                Matrix4x4 turned = at * Matrix4x4.Rotate(Quaternion.Euler(0f, GroundLook.Yaw(x, z, y), 0f));
                AddBody(batch, _model.EarthModule(index, variant, ShowsAVerticalFace(x, z, y)), tint, turned);
                return;
            }

            AddBody(batch, module, tint, at);
        }

        /// <summary>
        /// A water surface: one tile, draped onto the relief like any other ground so that a pond
        /// on a rolling board does not cut across it, and raised inside its cell to
        /// <see cref="WaterSurface"/>.
        ///
        /// It goes in the roof list with the rest of the ground, so a storey above the slice
        /// drops its water along with its floor, and it carries a water tint code so the renderer
        /// hands it <c>Odyssey/Water</c> rather than tinting a ground tile blue. No catalogue
        /// entry is wanted and none is looked for: water has a shader of its own, so a clone
        /// without the licensed packs draws exactly the same water as a machine with them.
        /// </summary>
        void EmitWater(ChunkBatch batch, int module, ushort terrain, int x, int z, int y)
        {
            Vector3 centre = CellMetrics.FloorCentre(x, z, y) + Vector3.up * (CellMetrics.SizeY * WaterSurface);

            // **Draped, like the ground it lies in** — and this was got wrong twice, so the
            // reasoning is here rather than in a commit nobody will find.
            //
            // Water was first draped, then switched to a plain lift on the argument that a water
            // surface is level. True of water, false of *tiles*: a lifted tile is flat and takes
            // its height from its own centre, so two neighbours sit at heights differing by the
            // first-order slope of the relief across a whole cell. On a low, near-horizontal
            // camera those little steps open into slivers you can see the riverbed through, which
            // is what the board looked like after the change.
            //
            // A draped tile is sheared onto the tangent plane of the field, so neighbours
            // disagree only by the *curvature* over a cell — second order, and invisible. It is
            // the same argument `06-rendering-and-camera.md` makes for the surround, where tiles
            // had to be halved twice because the disagreement grows as the square of the width.
            //
            // The dark grid that prompted the switch was never the shear at all. It was the six
            // faces of the box this tile is drawn from, blending over each other at every shared
            // edge, and the shader clips all but the top one.
            AddRoof(batch, module,
                TintCode.Daylit(TintCode.Water(terrain), OpenToTheSky(_model.Index(x, z, y), y)),
                GroundRelief.Drape(centre));
        }

        // ------------------------------------------------------------- scatter

        /// <summary>
        /// Tufts of grass per hundred grass cells. 60 means six cells in ten get one tuft and the
        /// rest are bare — sparse enough that the meadow reads as a field with grass on it rather
        /// than as grass with a field somewhere underneath, which is what 120 did at board
        /// distance. Zero turns scatter off entirely.
        /// </summary>
        public int ScatterDensity { get; set; } = 60;

        int[] _scatterModules = System.Array.Empty<int>();
        bool _scatterResolved;

        /// <summary>
        /// Strew tufts over an exposed grass surface.
        ///
        /// They go through the ordinary bucket machinery, which is the point: a tuft is one more
        /// instance of one more module in the chunk it stands in, so it inherits chunk culling,
        /// the slice, the depth shade and the single instanced submission per mesh with no new
        /// code path and no per-blade object. The alternative — a particle system, a detail
        /// renderer, a GameObject per clump — would have been a second way of drawing the world.
        ///
        /// Only the top surface is dressed. A grass cell with something solid stacked on it is a
        /// cell nobody can see the top of, and grass growing inside a floor is the sort of fault
        /// that renders perfectly and is spotted a fortnight later.
        /// </summary>
        void EmitScatter(ChunkBatch batch, int index, int x, int z, int y)
        {
            if (ScatterDensity <= 0) return;

            ushort terrain = _model.Terrain(index);
            if (terrain != NaturalContent.TerrainGrass) return;
            if (!_model.IsSolid(index)) return;

            var size = _model.Size;
            if (y + 1 < size.SizeY && _model.IsSolid(index + size.LayerStride)) return;

            EnsureScatterModules();
            if (_scatterModules.Length == 0) return;

            int count = GroundScatter.CountFor(x, z, ScatterDensity);
            if (count == 0) return;

            // Tufts stand on top of the solid cell, not inside it.
            Vector3 surface = CellMetrics.FloorCentre(x, z, y) + Vector3.up * CellMetrics.SizeY;

            // Which top this cell wears, and which way round, so a tuft can be set on the surface
            // that is actually drawn rather than on the flat one that used to be. The cell's
            // ripple is at most 12 cm, which sounds ignorable and is not: a tuft is about half a
            // metre, so a quarter of one hanging in the air is plainly wrong, and the same again
            // buried reads as a bald patch.
            int groundVariant = GroundLook.Variant(x, z, y);
            Quaternion untwist = Quaternion.Euler(0f, -GroundLook.Yaw(x, z, y), 0f);

            // Foliage, not terrain. Tinting a tuft the way the ground beneath it is tinted turned
            // a meadow into dark teal reeds; TintCode.FoliageBase says why.
            //
            // Daylit on the same terms as the ground it stands in, and it has to be asked rather
            // than assumed: a tuft that kept dimming while the terrace under it stopped would be
            // the same fault, a layer smaller and much harder to see.
            int tint = TintCode.Daylit(TintCode.FoliageBase, OpenToTheSky(index, y));

            for (int slot = 0; slot < count; slot++)
            {
                GroundScatter.Placement(x, z, slot,
                    out float offsetX, out float offsetZ, out float yaw, out float scale);

                int module = _scatterModules[
                    GroundScatter.VariantFor(x, z, slot, _scatterModules.Length)];

                // Lifted at the tuft's own position rather than the cell's, because the cell is
                // tilted: a tuft near the low corner of a sloping cell would otherwise float, and
                // one at the high corner would be buried to its neck.
                //
                // And set on the cell's own ripple as well as on the board's roll — two separate
                // shapes, asked separately. The offset is turned back through the block's bearing
                // first, because the mesh is rotated by the instance matrix and its ripple turns
                // with it; sampling the unturned mesh at a turned position puts the tuft on the
                // wrong corner of the cell, which is a subtler wrong than being on no corner.
                Vector3 local = untwist * new Vector3(offsetX, 0f, offsetZ);
                float ripple = GroundMesh.HeightAtLocal(groundVariant, local.x, local.z) * CellMetrics.SizeY;

                Vector3 at = GroundRelief.Lift(
                    surface + new Vector3(offsetX * CellMetrics.SizeXZ, ripple, offsetZ * CellMetrics.SizeXZ));

                AddBody(batch, module, tint, Matrix4x4.TRS(
                    at, Quaternion.Euler(0f, yaw, 0f), new Vector3(scale, scale, scale)));
            }
        }

        /// <summary>
        /// Resolve the tuft modules once, and keep only the ones that found real art.
        ///
        /// Dropping the rest is the important half. Every other module in the world falls back to
        /// a tinted primitive when its art is missing, which is right for a wall — a box where a
        /// wall should be is still a wall. A box where a tuft of grass should be is fourteen
        /// thousand grey cubes strewn across a meadow, so a clone without the packs gets bare
        /// ground instead, which is what it had before any of this existed.
        /// </summary>
        void EnsureScatterModules()
        {
            if (_scatterResolved) return;
            _scatterResolved = true;

            var usable = new List<int>();
            for (int variant = 0; variant < ModuleIds.GrassTuftCount; variant++)
            {
                int module = _model.Library.Resolve(ModuleIds.GrassTuft(variant), ModuleShape.Pillar);
                ResolvedModule resolved = _model.Library[module];
                if (resolved.UsesArt && !resolved.IsEmpty) usable.Add(module);
            }
            _scatterModules = usable.ToArray();
        }

        /// <summary>
        /// A buried cell is invisible, and the world boundary counts as buried.
        ///
        /// The boundary used to count as open air, which meant every solid cell in the outermost
        /// ring drew its outward face and the map gained a cross-section wall around its whole
        /// perimeter, as many cells tall as the slice drew layers below the surface. Looking down
        /// at a flat meadow you saw a slab of ground with sides, not a field.
        ///
        /// Treating the boundary as solid is the honest answer rather than a cosmetic one: there
        /// is no outside of the map, so there is nowhere a face on that plane could be seen from.
        /// Nothing interior changes, because a cut into the ground still exposes its neighbours in
        /// the ordinary way — a pit dug against the map edge still shows all four of its walls.
        /// </summary>
        /// <summary>
        /// Is there nothing at all over this cell — no slab and no solid cell, all the way up?
        ///
        /// <para>What earns a cell the daylight bit, and so exemption from the depth shade. See
        /// <see cref="TintCode.DaylitBase"/> for why the landscape must not dim: the surface is
        /// terraced across five layers and only one of them is ever the active one.</para>
        ///
        /// <para>A slab is stored on the cell <em>above</em> the boundary it occupies, so the roof
        /// over this cell is the floor of the next one up — which is why the walk starts at
        /// <c>y + 1</c> and asks about that cell's own floor. A blocking edifice is deliberately
        /// not consulted: a wall standing beside you is not a roof over you, and neither is a
        /// tree, so grass in woodland stays lit like the grass beside it.</para>
        ///
        /// <para>The loop looks unbounded and is not. A buried cell answers on its first step,
        /// because the cell above it is solid; a surface cell walks the headroom, which the
        /// generator holds at three layers. Nothing here walks a full column in practice.</para>
        /// </summary>
        bool OpenToTheSky(int index, int y)
        {
            var size = _model.Size;
            int above = index + size.LayerStride;

            for (int layer = y + 1; layer < size.SizeY; layer++, above += size.LayerStride)
            {
                if (_model.Floor(above) != 0) return false;
                if (_model.IsSolid(above)) return false;
            }

            return true;
        }

        /// <summary>
        /// Can any of this cell's four vertical faces be seen — is it a terrace riser, the wall of
        /// a cutting, or the side of an outcrop?
        ///
        /// <para>The horizontal half of <see cref="HasExposedFace"/>, split out because earth pays
        /// for coursed walls only where it has a wall to show. The world boundary counts as solid
        /// here for the same reason it does there: it is not an exposed face, and treating it as
        /// one drew a cross-section wall round the whole perimeter of the map.</para>
        /// </summary>
        bool ShowsAVerticalFace(int x, int z, int y)
        {
            var size = _model.Size;
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (!size.Contains(nx, nz, y)) continue;
                if (!_model.IsSolid(size.Index(nx, nz, y))) return true;
            }
            return false;
        }

        bool HasExposedFace(int index, int x, int z, int y)
        {
            var size = _model.Size;
            if (ShowsAVerticalFace(x, z, y)) return true;
            if (y + 1 >= size.SizeY) return true;
            if (!_model.IsSolid(index + size.LayerStride)) return true;
            // Downwards only matters at the very bottom, which nothing can see.
            return y > 0 && !_model.IsSolid(index - size.LayerStride);
        }

        void EmitFloor(ChunkBatch batch, int index, int x, int z, int y)
        {
            if (_model.IsSolid(index)) return; // a slab inside rock is not visible
            int module = _model.FloorModule(index);
            if (module == 0) return;
            // A built floor is man-made and stays flat; it is lifted onto the ground, not laid
            // along it. Only terrain is draped.
            AddRoof(batch, module, TintCode.Stuff(_model.FloorStuff(index)),
                Matrix4x4.Translate(GroundRelief.Lift(CellMetrics.FloorCentre(x, z, y))));
        }

        void EmitEdifice(ChunkBatch batch, int index, int x, int z, int y)
        {
            ushort def = _model.EdificeDef(index);
            if (def == CoreContent.EdificeNone) return;
            int module = _model.EdificeModule(index);
            if (module == 0) return;

            int tint = TintCode.Stuff(_model.EdificeStuff(index));
            var shape = _model.Library[module].Shape;

            switch (def)
            {
                case CoreContent.EdificeDoor:
                    EmitDoor(batch, module, tint, index, x, z, y);
                    return;
                case CoreContent.EdificeStairLower:
                case CoreContent.EdificeStairUpper:
                    EmitStair(batch, module, tint, def, x, z, y);
                    return;
                case CoreContent.EdificeLadder:
                    EmitLadder(batch, module, tint, index, x, z, y);
                    return;
                case CoreContent.EdificePillar:
                case CoreContent.EdificeUtilityTap:
                    AddBody(batch, module, tint,
                        Matrix4x4.Translate(GroundRelief.Lift(CellMetrics.FloorCentre(x, z, y))));
                    return;
            }

            if (shape != ModuleShape.WallPanel)
            {
                AddBody(batch, module, tint,
                    Matrix4x4.Translate(GroundRelief.Lift(CellMetrics.FloorCentre(x, z, y))));
                return;
            }

            EmitFacePanels(batch, module, tint, index, x, z, y);
        }

        void EmitFacePanels(ChunkBatch batch, int module, int tint, int index, int x, int z, int y)
        {
            var size = _model.Size;
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (size.Contains(nx, nz, y) && _model.OccludesFace(size.Index(nx, nz, y))) continue;
                // Lifted at the face's own centre, not the cell's. Half a cell along a slope is
                // enough for a panel and the wall it belongs to to visibly disagree.
                AddBody(batch, module, tint, Matrix4x4.TRS(
                    GroundRelief.Lift(CellMetrics.FaceCentre(x, z, y, dir)),
                    Quaternion.Euler(0f, Directions.Yaw[dir], 0f),
                    Vector3.one));
            }
        }

        void EmitDoor(ChunkBatch batch, int module, int tint, int index, int x, int z, int y)
        {
            int dir = FirstOpenDirection(x, z, y);
            AddBody(batch, module, tint, Matrix4x4.TRS(
                GroundRelief.Lift(CellMetrics.FloorCentre(x, z, y)),
                Quaternion.Euler(0f, Directions.Yaw[dir], 0f),
                Vector3.one));
        }

        void EmitStair(ChunkBatch batch, int module, int tint, ushort def, int x, int z, int y)
        {
            ushort partner = def == CoreContent.EdificeStairLower
                ? CoreContent.EdificeStairUpper
                : CoreContent.EdificeStairLower;
            int dir = FindNeighbour(x, z, y, partner);
            if (dir < 0) dir = Directions.North;

            // The climb runs lower -> upper. Seen from the upper half, that is the way it came.
            int climb = def == CoreContent.EdificeStairLower ? dir : Directions.Opposite(dir);
            float rise = def == CoreContent.EdificeStairLower ? 0f : CellMetrics.SizeY * 0.5f;

            AddBody(batch, module, tint, Matrix4x4.TRS(
                GroundRelief.Lift(CellMetrics.FloorCentre(x, z, y)) + Vector3.up * rise,
                Quaternion.Euler(0f, Directions.Yaw[climb], 0f),
                Vector3.one));
        }

        void EmitLadder(ChunkBatch batch, int module, int tint, int index, int x, int z, int y)
        {
            int wall = FirstOccludingDirection(x, z, y);
            int facing = wall >= 0 ? Directions.Opposite(wall) : Directions.North;
            AddBody(batch, module, tint, Matrix4x4.TRS(
                GroundRelief.Lift(CellMetrics.FloorCentre(x, z, y)),
                Quaternion.Euler(0f, Directions.Yaw[facing], 0f),
                Vector3.one));
        }

        int FirstOpenDirection(int x, int z, int y)
        {
            var size = _model.Size;
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (!size.Contains(nx, nz, y)) return dir;
                if (!_model.OccludesFace(size.Index(nx, nz, y))) return dir;
            }
            return Directions.North;
        }

        int FirstOccludingDirection(int x, int z, int y)
        {
            var size = _model.Size;
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (size.Contains(nx, nz, y) && _model.OccludesFace(size.Index(nx, nz, y))) return dir;
            }
            return -1;
        }

        int FindNeighbour(int x, int z, int y, ushort edificeDef)
        {
            var size = _model.Size;
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (!size.Contains(nx, nz, y)) continue;
                if (_model.EdificeDef(size.Index(nx, nz, y)) == edificeDef) return dir;
            }
            return -1;
        }

        // ------------------------------------------------------------- buckets

        void AddBody(ChunkBatch batch, int module, int tint, in Matrix4x4 placement) =>
            Add(batch.Body, _bodyIndex, batch, module, tint, placement);

        void AddRoof(ChunkBatch batch, int module, int tint, in Matrix4x4 placement) =>
            Add(batch.Roof, _roofIndex, batch, module, tint, placement);

        void Add(List<InstanceBucket> list, Dictionary<long, int> lookup, ChunkBatch batch,
            int module, int tint, in Matrix4x4 placement)
        {
            var parts = _model.Library[module].Parts;
            for (int p = 0; p < parts.Length; p++)
            {
                long key = Key(module, p, tint);
                if (!lookup.TryGetValue(key, out int slot))
                {
                    slot = list.Count;
                    list.Add(new InstanceBucket { Module = module, Part = p, Tint = tint });
                    lookup.Add(key, slot);
                }
                list[slot].Add(placement * parts[p].Local);
                batch.InstanceCount++;
            }
        }

        static long Key(int module, int part, int tint) =>
            ((long)module << 40) | ((long)part << 20) | (uint)tint;

        static long KeyOf(InstanceBucket bucket) => Key(bucket.Module, bucket.Part, bucket.Tint);
    }
}
