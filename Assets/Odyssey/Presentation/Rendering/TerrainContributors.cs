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
    /// Water: one tile, draped like the ground it lies in and raised inside its cell to
    /// <see cref="ChunkMesher.WaterSurface"/>.
    ///
    /// <para>It goes in the roof list with the rest of the ground, so a storey above the slice
    /// drops its water along with its floor, and it carries a water tint code so the renderer
    /// hands it <c>Odyssey/Water</c> rather than tinting a ground tile blue. No catalogue entry is
    /// wanted and none is looked for: water has a shader of its own, so a clone without the
    /// licensed packs draws exactly the same water as a machine with them.</para>
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

            Vector3 centre = CellMetrics.FloorCentre(cell.X, cell.Z, cell.Y)
                             + Vector3.up * (CellMetrics.SizeY * ChunkMesher.WaterSurface);

            sink.Roof(
                cell.Module,
                TintCode.Daylit(TintCode.Water(cell.Terrain), cell.Model.OpenToTheSky(cell.Index, cell.Y)),
                GroundRelief.Drape(centre));
            return true;
        }
    }

    /// <summary>
    /// A surface material — pavement, soil, rubble. One tile on the cell floor, in the roof list
    /// so that a storey above the slice can drop its ground.
    /// </summary>
    public sealed class SurfaceContributor : ITerrainContributor
    {
        public bool Emit(in TerrainCell cell, in MeshSink sink)
        {
            if (cell.Solid) return false;

            sink.Roof(cell.Module, cell.Tint, cell.Drape);
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
            if (!Enabled || !cell.ShowsAFace || !cell.Model.IsEarth(cell.Index)) return false;

            int variant = GroundLook.Variant(cell.X, cell.Z, cell.Y);
            int exposed = cell.ExposedSides();

            float yaw;
            int earth;
            if (exposed == 0)
            {
                yaw = GroundLook.Yaw(cell.X, cell.Z, cell.Y);
                earth = cell.Model.EarthModule(cell.Index, variant, showsAFace: false);
            }
            else
            {
                int canonical = GroundMesh.CanonicalExposure(exposed, out int rotation);
                yaw = 90f * rotation;
                earth = cell.Model.EarthFaceModule(cell.Index, variant, canonical);
            }

            sink.Body(earth, cell.Tint, cell.Drape * Matrix4x4.Rotate(Quaternion.Euler(0f, yaw, 0f)));
            return true;
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
