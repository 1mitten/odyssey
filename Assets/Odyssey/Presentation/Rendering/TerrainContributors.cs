#nullable enable
using Odyssey.Presentation.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// One terrain cell, with everything a contributor needs to draw it and nothing it would have
    /// to reach back into the mesher for.
    ///
    /// <para><b>Why the tint and the drape are computed before anyone is asked.</b> In the chain
    /// this replaced, water was decided *before* those two lines ran and everything else after, so
    /// a reader had to know where in the method they were to know what was available. Both are
    /// pure functions of the cell, so computing them up front costs one tint lookup on a water
    /// cell and buys a context that means the same thing to every contributor.</para>
    /// </summary>
    public readonly struct TerrainCell
    {
        readonly ChunkMesher _mesher;

        public readonly WorldRenderModel Model;
        public readonly int Index;
        public readonly int X;
        public readonly int Z;
        public readonly int Y;

        public readonly ushort Terrain;

        /// <summary>The module the terrain table names for this cell. Never zero: the mesher drops those.</summary>
        public readonly int Module;

        public readonly int Tint;

        /// <summary>The cell tilted onto the tangent plane of the relief — ground is draped, not lifted.</summary>
        public readonly Matrix4x4 Drape;

        public readonly bool Solid;

        /// <summary>
        /// Solid, and with at least one face something could see. Computed once and only for solid
        /// cells, which is exactly when the chain used to compute it: asking three contributors in
        /// turn would otherwise pay for it three times on the mesher's hot path.
        /// </summary>
        public readonly bool ShowsAFace;

        internal TerrainCell(ChunkMesher mesher, WorldRenderModel model, int index, int x, int z, int y,
            ushort terrain, int module, int tint, in Matrix4x4 drape, bool solid, bool showsAFace)
        {
            _mesher = mesher;
            Model = model;
            Index = index;
            X = x;
            Z = z;
            Y = y;
            Terrain = terrain;
            Module = module;
            Tint = tint;
            Drape = drape;
            Solid = solid;
            ShowsAFace = showsAFace;
        }

        /// <summary>
        /// Which of the four same-layer neighbours are open, as a bit per direction. On demand
        /// rather than in the constructor: one contributor wants it and the rest would be paying
        /// for a neighbour scan they never read.
        /// </summary>
        public int ExposedSides() => _mesher.ExposedSidesOf(X, Z, Y);
    }

    /// <summary>Where a contributor puts what it decided to draw.</summary>
    public readonly struct MeshSink
    {
        readonly ChunkMesher _mesher;
        readonly ChunkBatch _batch;

        internal MeshSink(ChunkMesher mesher, ChunkBatch batch)
        {
            _mesher = mesher;
            _batch = batch;
        }

        /// <summary>Walls, lumps, blocks — anything the cut-away keeps.</summary>
        public void Body(int module, int tint, in Matrix4x4 at) => _mesher.SinkBody(_batch, module, tint, at);

        /// <summary>Ground, slabs and water — everything a storey above the slice drops.</summary>
        public void Roof(int module, int tint, in Matrix4x4 at) => _mesher.SinkRoof(_batch, module, tint, at);

        /// <summary>Whether this earth cell's top belongs in the ground skin (see <see cref="GroundSkin"/>).</summary>
        public bool SkinsTop(in TerrainCell cell) => _mesher.SinkSkinsTop(cell);

        /// <summary>The flat top of an earth cell, drawn as skin in this module's material and tint.</summary>
        public void SkinTop(int module, int tint, int x, int z, int y) =>
            _mesher.SinkSkin(_batch, module, tint, x, z, y);
    }

    /// <summary>
    /// A kind of terrain that knows how to draw itself.
    ///
    /// <para><b>Why this exists (OQ-46).</b> Drawing a terrain cell used to be a chain of tests in
    /// one method — water, then surface, then an exposure gate, then stone, then earth, then a
    /// fallback — so every new terrain feature was an edit to <see cref="ChunkMesher"/>. That file
    /// is on the queue's do-not-touch list, and both the water line and the mining line had to
    /// touch it anyway, which is the definition of a rule the design leaves no way to obey. A
    /// contributor joins by being registered; the mesher asks each in turn and the first to answer
    /// owns the cell.</para>
    ///
    /// <para><b>Order is meaning, not convenience.</b> The list is walked in registration order and
    /// the first contributor to return true ends the cell, which is what makes this a faithful
    /// replacement for a chain of early returns rather than a set of independent opinions. A
    /// contributor states its own condition — that is the whole point — so two that both claim a
    /// cell are a content error, and the first one wins in a defined way rather than an
    /// accidental one.</para>
    /// </summary>
    public interface ITerrainContributor
    {
        /// <summary>Draw this cell and return true, or decline it and return false.</summary>
        bool Emit(in TerrainCell cell, in MeshSink sink);
    }

    /// <summary>
    /// Water: a surface draped like the ground it lies in and raised inside its cell to
    /// <see cref="ChunkMesher.WaterSurface"/>, plus a sheet on every side that nothing holds in.
    ///
    /// <para>Both go in the roof list with the rest of the ground, so a storey above the slice
    /// drops its water along with its floor, and both carry a water tint code so the renderer
    /// hands them <c>Odyssey/Water</c> rather than tinting a ground tile blue. No catalogue entry
    /// is wanted and none is looked for: water has a shader of its own and meshes of its own
    /// (<see cref="WaterMesh"/>), so a clone without the licensed packs draws exactly the same
    /// water as a machine with them.</para>
    ///
    /// <para><b>Draped, not lifted</b>, and this was got wrong twice. A lifted tile is flat and
    /// takes its height from its own centre, so two neighbours sit at heights differing by the
    /// first-order slope of the relief across a whole cell, and the seam between them is visible
    /// on a near-horizontal surface precisely because the surface is near-horizontal. Water is
    /// level; a tile standing in for water is not.</para>
    /// </summary>
    public sealed class WaterContributor : ITerrainContributor
    {
        public bool Emit(in TerrainCell cell, in MeshSink sink)
        {
            if (!NaturalContent.IsWater(cell.Terrain)) return false;

            float lift = CellMetrics.SizeY * ChunkMesher.WaterSurface;
            Vector3 centre = CellMetrics.FloorCentre(cell.X, cell.Z, cell.Y) + Vector3.up * lift;
            int tint = TintCode.Daylit(
                TintCode.Water(cell.Terrain), cell.Model.OpenToTheSky(cell.Index, cell.Y));

            sink.Roof(cell.Module, tint, GroundRelief.Drape(centre));
            EmitFalls(cell, sink, tint, lift);
            return true;
        }

        /// <summary>
        /// The sheet of water on every side of this cell that nothing holds in.
        ///
        /// <para><b>Why water needed a side at all</b> (owner, 2026-09-17, four screenshots of a
        /// stream stepping down the terraced board: water "in mid air", gaps, water that "can't
        /// handle being at height"). The surface was the only thing drawn, so a water cell was a
        /// lid hanging 2.16 m over its own bed with open air between and nothing on any side.
        /// Measured on the board the scene loads, over three seeds: not one water cell floats and
        /// the stream never drops more than one layer between neighbouring columns, so the
        /// generator was never at fault — <c>WaterFillPass</c>'s own comment had it right that
        /// "depth is a rendering problem, not a geometry problem". What the player saw was the
        /// void under the lid, in plain view wherever the neighbour was lower.</para>
        ///
        /// <para><b>One sheet, two reasons, and that is why this is not two features.</b> A face
        /// goes on a side when the thing beside it is not water at the same level. Where there is
        /// nothing there, it closes the channel and the lid stops hanging in the air; where the
        /// water in the next cell is one layer down, the same sheet spans the step and the water
        /// falls. The two differ only in how far down the sheet reaches.</para>
        ///
        /// <para><b>Never between two water cells</b>, and this is the constraint the whole shape
        /// of the thing is built around. Water writes no depth, so two coincident faces each add
        /// their own alpha — which is exactly what ruled the board into dark squares along every
        /// cell boundary when water was a slab with sides, and what the shader's old top-face clip
        /// was there to remove. An interior face would bring that straight back.</para>
        ///
        /// <para>The world boundary holds water in, like <c>ChunkMesher.ExposedSides</c> and for
        /// its reason: it is not an exposed face, and treating it as one would hang a curtain of
        /// water round the whole rim of the map.</para>
        /// </summary>
        /// <summary>
        /// How far a falling sheet stands out from the face it pours over, in metres.
        ///
        /// <para>Big enough to win the depth test at the far plane and far too small to see: three
        /// centimetres against a 2.5 m cell is a hundredth of a cell, and the sheet is translucent
        /// besides. Smaller was not tried in the belief that it would do — the depth range here is
        /// 0.3 m to 2 km, which is where a 24-bit buffer has the least precision to spare.</para>
        /// </summary>
        const float StandOff = 0.015f;

        /// <summary>
        /// How far the sheet is tucked up behind its own surface, in metres.
        ///
        /// <para>Standing the sheet off the rock opens a slot of exactly <see cref="StandOff"/>
        /// between the two, and at the lip that slot is a line of sight past the top edge of the
        /// sheet onto the terrace behind it — a hairline of lit grass along the brow of every fall.
        /// Starting the sheet fractionally above the surface it hangs from closes it, and costs
        /// nothing, because the overlap is inside the water it is already continuous with.</para>
        /// </summary>
        const float Tuck = 0.04f;

        static void EmitFalls(in TerrainCell cell, in MeshSink sink, int tint, float lift)
        {
            var model = cell.Model;
            var size = model.Size;

            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = cell.X + Directions.DeltaX[dir], nz = cell.Z + Directions.DeltaZ[dir];
                if (!size.Contains(nx, nz, cell.Y)) continue;

                int neighbour = size.Index(nx, nz, cell.Y);
                if (model.IsSolid(neighbour)) continue;
                if (NaturalContent.IsWater(model.Terrain(neighbour))) continue;

                // How far the sheet falls. To the surface of the water one layer down if there is
                // any — a cascade step, where the drop between two surfaces is exactly one cell
                // because both stand at the same height inside their own — otherwise only as far
                // as this cell's own floor, which is the bed it is lying on.
                bool cascades = cell.Y > 0 &&
                                NaturalContent.IsWater(
                                    model.Terrain(size.Index(nx, nz, cell.Y - 1)));
                float drop = cascades ? CellMetrics.SizeY : lift;

                // **Stood off the face by a few millimetres, and without this most of the sheet
                // is not there.** A face centre is exactly the plane the terrace riser below it
                // occupies, so the sheet and the rock it pours over are coplanar — and two
                // coplanar surfaces under `ZTest LEqual` are two candidates for the same pixel
                // with nothing to separate them. The depth buffer then picks whichever rounds
                // higher, which it does per triangle, so a fall came out as a **triangular sliver
                // of its top corner** with the rest of it losing the test to the rock. It reads as
                // a small pale wedge rather than as a missing feature, which is why it survived a
                // contact sheet: proved by tinting the sheet by its own UV, where every visible
                // fragment came back at the very top of the mesh.
                //
                // The same fix and the same reason as `ChunkMesher.CoreRecess`, in the other axis.
                Vector3 stand = new Vector3(
                    Directions.DeltaX[dir] * StandOff, 0f, Directions.DeltaZ[dir] * StandOff);

                sink.Roof(
                    model.WaterFallModule, tint,
                    GroundRelief.Drape(CellMetrics.FaceCentre(cell.X, cell.Z, cell.Y, dir)
                                       + Vector3.up * (lift + Tuck) + stand)
                    * Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[dir], 0f))
                    * Matrix4x4.Scale(new Vector3(1f, drop + Tuck, 1f)));
            }
        }
    }

    /// <summary>
    /// A surface material — pavement, soil, rubble. One tile on the cell floor, in the roof list
    /// so that a storey above the slice can drop its ground.
    ///
    /// <para><b>A surface and a floor can share a cell, and a collapse guarantees it.</b>
    /// <c>SupportSystem.Rubble</c> writes rubble into <c>FirstFloorAtOrBelow</c> — deliberately, so
    /// that debris lands on something rather than in mid-air, and deliberately not solid so it
    /// buries nothing. The cell it picks therefore <em>has a floor by definition</em>, and both are
    /// drawn: the slab by <c>ChunkMesher.EmitFloor</c> and the surface tile here, at the same cell's
    /// floor plane. Two coplanar surfaces in one cell.</para>
    ///
    /// <para><b>The owner met it as a grey tile in a wooden deck</b> (2026-09-18) and it took three
    /// sessions, because every explanation offered was about the <i>floor</i> — the pane read
    /// <c>FloorStuff</c> and honestly said "Wood floor", and the save holds no stone slab anywhere
    /// on the board. The grey was never a floor at all. It is `SM_Env_Ground_Tile_Half_03`, the
    /// rubble <em>terrain</em>, drawn over the deck the player built.</para>
    ///
    /// <para><b>Rubble on a floor is lifted to sit on it</b>, rather than hidden. Hiding it was the
    /// shorter fix and the wrong one: rubble refuses to be built on until it is cleared
    /// (<c>TerrainDef.buildable</c>), so a cell that silently rejects orders with nothing on screen
    /// to explain why is the "command that does nothing and says nothing" this build keeps
    /// apologising for. It is a heap lying on the deck, so it is drawn as one.</para>
    /// </summary>
    public sealed class SurfaceContributor : ITerrainContributor
    {
        public bool Emit(in TerrainCell cell, in MeshSink sink)
        {
            if (cell.Solid) return false;

            // The same clearance a slab takes over the ground, taken again over the slab, and for
            // the same reason: two things at one height is the fault, not which of them is on top.
            // Drawn as a sheet, for the same reason a built floor is: paving is one cell-sized plate
            // per cell too, so its rim ties with its neighbour's top face and draws the same dotted
            // grid across a pavement. CellMetrics.FloorTile owns it.
            Matrix4x4 at = cell.Model.Floor(cell.Index) != CoreContent.SlabNone
                ? Matrix4x4.Translate(Vector3.up * CellMetrics.SlabLift) * cell.Drape
                : cell.Drape;

            sink.Roof(cell.Module, cell.Tint, at * CellMetrics.FloorTile);
            return true;
        }
    }

    /// <summary>
    /// Stone: one of several chipped lumps, turned to one of four bearings, both drawn from a hash
    /// of the cell so a cliff face does not rearrange itself every time somebody digs a cell in
    /// the same chunk.
    ///
    /// <para>The turn is composed on the left of the module's own local transform, which is a
    /// vertical shift and a scale equal in x and z; a rotation about the vertical commutes with
    /// both, so the lump turns about its own axis and still fills its cell exactly. Three quarters
    /// of the variety for no extra mesh and no extra draw.</para>
    /// </summary>
    public sealed class StoneContributor : ITerrainContributor
    {
        public bool Emit(in TerrainCell cell, in MeshSink sink)
        {
            if (!cell.ShowsAFace || !cell.Model.IsStone(cell.Index)) return false;

            int variant = RockLook.Variant(cell.X, cell.Z, cell.Y);
            Matrix4x4 turned = cell.Drape * Matrix4x4.Rotate(
                Quaternion.Euler(0f, RockLook.Yaw(cell.X, cell.Z, cell.Y), 0f));

            sink.Body(cell.Model.StoneModule(cell.Index, variant), cell.Tint, turned);
            return true;
        }
    }

    /// <summary>
    /// Earth: a block with an uneven top, and — only where a side of it can be seen — coursed
    /// walls as well.
    ///
    /// <para>The two are different meshes and so different buckets, so the question is asked per
    /// cell rather than paid for everywhere: on a flat board a surface cell's four same-layer
    /// neighbours are solid too and nothing but its top is ever visible. Sides appear at terrace
    /// risers and at the walls of a cutting, which are a small fraction of what is drawn and
    /// exactly the places the ruled 3 m rectangle was the fault.</para>
    ///
    /// <para>A cell with nothing to show takes the cheap block and a free bearing; a cell with a
    /// face takes the mesh cut for its own pattern of exposed sides, and spends the bearing
    /// turning that pattern onto the sides that are really open. Five meshes then cover all
    /// sixteen possibilities, which is the whole reason the chamfer is affordable.</para>
    /// </summary>
    public sealed class EarthContributor : ITerrainContributor
    {
        /// <summary>Off, and the plain block is drawn instead. Kept as a switch while the look is being judged.</summary>
        public bool Enabled { get; set; } = true;

        public bool Emit(in TerrainCell cell, in MeshSink sink)
        {
            // Keyed on the terrain the cell CARRIES - the drawn terrain, not the grid's - so a
            // zoned cell's ground resolves the earth of the dirt it is drawn as, not the grass
            // it still is underneath.
            if (!Enabled || !cell.ShowsAFace || !GroundLook.IsEarth(cell.Terrain)) return false;

            int variant = GroundLook.Variant(cell.X, cell.Z, cell.Y);

            // A stream or pond bank takes a sheet of water over it at the water line (design 38 §24):
            // the bank slopes down through that line (BankLayout.ShoreFan), so the water shows
            // exactly where the ground has gone below it, and the shore is the line where the two
            // cross.
            if (WaterShore.Enabled && BankLayout.BankDips(cell.Model, cell.X, cell.Z, cell.Y, out _))
                EmitShoreWater(cell, sink);

            // Nothing but its top can be seen — or its only open sides are water, a stream bank —
            // and nothing built stands on it: it goes into the chunk's ground skin rather than a box
            // (design 38 §20).
            if (sink.SkinsTop(cell))
            {
                ushort skinTerrain = cell.Terrain;
                int skinTint = cell.Tint;
                // A bed that rises to meet its bank is the bank carried on down under the water,
                // and wears its grass: in its own sand, the tip where a pond's corner is rounded
                // off stood pale above the water line (design 38 §24).
                if (WaterShore.Enabled && BankLayout.BedRises(cell.Model, cell.X, cell.Z, cell.Y, out _))
                {
                    skinTerrain = NaturalContent.TerrainGrass;
                    skinTint = TintCode.Daylit(TintCode.Terrain(skinTerrain),
                        cell.Model.OpenToTheSky(cell.Index, cell.Y));
                }
                sink.SkinTop(cell.Model.EarthModule(skinTerrain, variant, showsAFace: false),
                    skinTint, cell.X, cell.Z, cell.Y);
                return true;
            }

            int exposed = cell.ExposedSides();

            float yaw;
            int earth;
            if (exposed == 0)
            {
                earth = cell.Model.EarthModule(cell.Terrain, variant, showsAFace: false);
                yaw = GroundLook.Yaw(cell.X, cell.Z, cell.Y);
            }
            else
            {
                int canonical = GroundMesh.CanonicalExposure(exposed, out int rotation);
                yaw = 90f * rotation;
                earth = cell.Model.EarthFaceModule(cell.Terrain, variant, canonical);
            }

            sink.Body(earth, cell.Tint, cell.Drape * Matrix4x4.Rotate(Quaternion.Euler(0f, yaw, 0f)));
            return true;
        }

        /// <summary>
        /// The water sheet over a bank: the same surface, at the same height and draped the same
        /// way as the water beside it, so the two are one level sheet with no seam between them.
        ///
        /// <para>In the roof list, like the water it continues, and shallow: the depth the shader
        /// shades by comes from the ground field, not from this cell's own class
        /// (<see cref="GroundField"/>). Water writes no depth, so where the bank is above it the
        /// ground simply hides it.</para>
        /// </summary>
        static void EmitShoreWater(in TerrainCell cell, in MeshSink sink)
        {
            var model = cell.Model;
            int module = model.ModuleForTerrain(Odyssey.Sim.Worldgen.Natural.NaturalContent.TerrainShallowWater);
            if (module == 0) return;

            float lift = CellMetrics.SizeY * ChunkMesher.WaterSurface;
            Vector3 centre = CellMetrics.FloorCentre(cell.X, cell.Z, cell.Y) + Vector3.up * lift;
            int above = cell.Index + model.Size.LayerStride;
            int tint = TintCode.Daylit(
                TintCode.Water(Odyssey.Sim.Worldgen.Natural.NaturalContent.TerrainShallowWater),
                model.OpenToTheSky(above, cell.Y + 1));
            sink.Roof(module, tint, GroundRelief.Drape(centre));
        }
    }

    /// <summary>
    /// Anything solid that no other contributor claimed: the module the terrain table names, in
    /// its cell, draped.
    ///
    /// <para>Registered last and declining nothing that reaches it, so it is the end of the chain
    /// rather than a participant in it. A solid cell with no exposed face reaches here and is
    /// declined, which is how "buried strata are not drawn" survives the move.</para>
    /// </summary>
    public sealed class SolidBlockContributor : ITerrainContributor
    {
        public bool Emit(in TerrainCell cell, in MeshSink sink)
        {
            if (!cell.ShowsAFace) return false;

            sink.Body(cell.Module, cell.Tint, cell.Drape);
            return true;
        }
    }
}
