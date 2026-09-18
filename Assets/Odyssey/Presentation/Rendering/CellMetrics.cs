#nullable enable
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The one place metres exist.
    ///
    /// The simulation speaks only in cells (x, z, layer); ADR 0002 fixes a cell at
    /// 2.5 x 2.5 m on the ground and 3.0 m tall. Every conversion between the two lives here, so
    /// there is exactly one definition of where a cell is in the world and no system can drift
    /// half a cell away from another.
    ///
    /// Axis convention: simulation z maps to world Z, simulation layer y maps to world Y. The
    /// corner of cell (x, z, y) is at <c>(x * 2.5, y * 3.0, z * 2.5)</c>.
    /// </summary>
    public static class CellMetrics
    {
        public const float SizeXZ = 2.5f;
        public const float SizeY = 3.0f;
        public const float HalfXZ = SizeXZ * 0.5f;

        /// <summary>The low corner of a cell: minimum x, minimum z, the cell's own floor level.</summary>
        public static Vector3 Corner(int x, int z, int y) =>
            new Vector3(x * SizeXZ, y * SizeY, z * SizeXZ);

        /// <summary>The centre of the cell's floor. Everything that stands on the floor is placed here.</summary>
        public static Vector3 FloorCentre(int x, int z, int y) =>
            new Vector3(x * SizeXZ + HalfXZ, y * SizeY, z * SizeXZ + HalfXZ);

        public static Vector3 FloorCentre(CellRef cell) => FloorCentre(cell.X, cell.Z, cell.Y);

        /// <summary>
        /// How far above its cell's floor plane a slab's walking surface sits: <b>8 mm of
        /// clearance, and it is load-bearing.</b>
        ///
        /// <para><see cref="FloorCentre"/> for cell <c>y</c> is at <c>y * SizeY</c>, which is
        /// exactly the <em>top face of the terrain block filling cell y-1</em>. So a slab whose top
        /// face sits on the plane is coplanar with the ground it is laid on, and paving — whose
        /// entire purpose is to be laid on ground that is already there — z-fights with it. That is
        /// not a theory: levelling the slabs on to the plane did it across the owner's board within
        /// the hour (2026-09-18, "there is all sorts of flickering happening to tiles now").</para>
        ///
        /// <para><b>8 mm because that is the number that already worked.</b> Before the slabs were
        /// levelled, the plank deck happened to land at +0.008 through its prefab's own pivot and
        /// had never flickered in any playtest; the street tile landed at +0.033 and was the 25 mm
        /// step the levelling was for. The clearance is kept and the disagreement removed.</para>
        ///
        /// <para>Small enough not to read as a kerb — 8 mm against a 3 m layer — and applied to
        /// every slab equally, so it cannot reintroduce the step it replaced.</para>
        /// </summary>
        public const float SlabLift = 0.008f;

        /// <summary>
        /// <b>A floor tile is drawn as a sheet, not as a plate</b>, and this is the thickness left
        /// of the plate: a thousandth of it, which is a tenth of a millimetre.
        ///
        /// <para><b>The fault it fixes.</b> The slab art is a plain box — measured, 2.5000 m across,
        /// its top face flat to the micrometre and the full width of the piece, 101 mm deep. Two of
        /// them side by side therefore meet exactly, and the top edge of one tile's <em>rim</em>
        /// lies exactly in the plane of its neighbour's top face. Equal depth, so the rasteriser
        /// keeps whichever fragment it likes, and a rim takes almost no light under a 72° sun: where
        /// it wins it draws a dot of wood at four tenths the brightness of the deck. Along a seam
        /// that is a dotted line; over a floor it is a dotted grid on the cell pitch. The owner,
        /// 2026-09-18, with two screenshots: <i>"these slabs leave small artifacts/lines or gaps
        /// that don't even up … you can notice this when you look at the ground from certain
        /// angles."</i></para>
        ///
        /// <para><b>Everything else was tried and measured not to be it</b>
        /// (<c>SlabFlushProbe</c>, <c>SeamProbe</c>, <c>SlabTopFaceProbe</c>). Not the relief: the
        /// flat board draws the same grid. Not the ground or a wall under the floor: a deck floating
        /// two layers up draws it too. Not the art's size, height or flatness: all three measured
        /// exact. Not the placement: growing every tile to overlap its neighbours by 6 mm and then by
        /// 200 mm left the grid untouched, and staggering alternate tiles by a millimetre made it no
        /// better, because a raised tile simply exposes its rim for real.</para>
        ///
        /// <para><b>Why a sheet works and a thin plate does not.</b> The tie is not decided by how
        /// tall the rim is — it is decided by the rim <em>existing</em>. A 5 mm rim still draws the
        /// line, and lets the grass through it besides. Squashed to a tenth of a millimetre the rim
        /// is degenerate on screen and generates no fragments to win with. Measured on the meadow
        /// with a wood floor laid on it, counting pixels a good deal darker than all four of their
        /// neighbours: at the play camera's 48°, <b>470 → 16</b>; at a grazing 25°, <b>884 → 35</b>;
        /// and on the floating deck at 14 m, <b>73 → 3</b>.</para>
        ///
        /// <para><b>What it costs, and it is a real cost.</b> A floor over open air — a balcony, the
        /// lip of a roof deck with no wall under it — loses the 101 mm of drawn thickness it had and
        /// reads as a sheet of paper seen edge-on. Nothing else changes: a floor laid on the ground
        /// had 93 of those millimetres buried in the block beneath it anyway, which is most floors.
        /// The fix that keeps the lip is to draw a floor's edge as a <em>fascia on the face</em>,
        /// exactly as a wall is drawn on faces rather than as a cell (see <c>ChunkMesher</c>'s class
        /// comment), and that wants a panel module of its own rather than a constant.</para>
        ///
        /// <para>Drawn and nothing else: no cell, no save, no hash. A pawn still stands on
        /// <see cref="FloorCentre"/> and the slab's walking surface is still
        /// <see cref="SlabLift"/> above the cell floor.</para>
        /// </summary>
        public const float FloorSheet = 0.001f;

        /// <summary>
        /// How far past its own cell a floor tile is grown so that it <b>knits into</b> its
        /// neighbours rather than abutting them: 3 mm on every side.
        ///
        /// <para>Second in line behind <see cref="FloorSheet"/> and much the smaller half. Two
        /// sheets that share an edge exactly are only watertight if the rasteriser agrees to a bit
        /// about where that edge is, and it does not — each tile is placed by its own matrix, so
        /// the edge is computed twice by different arithmetic and the sub-pixel snapping drops the
        /// odd pixel. Measured on top of the sheet: 25 → 16 at 48°, 47 → 35 at 25°.</para>
        ///
        /// <para>3 mm because an overlap only works while it projects to more than the roughly
        /// 1/256 of a pixel a rasteriser snaps to: a quarter of a pixel at the closest the camera
        /// comes, four times the snapping floor from 160 m up. What it costs is that two tiles now
        /// overlap by 6 mm with their faces at one height, so at a boundary between two materials a
        /// 6 mm strip takes the wrong one of them — half a pixel, and stable from frame to frame
        /// because the draw order is.</para>
        /// </summary>
        public const float FloorKnit = 0.003f;

        /// <summary>
        /// The placement every cell-sized floor tile takes, in its own coordinates: squashed to a
        /// sheet about its own top face, and grown by <see cref="FloorKnit"/> on every side.
        ///
        /// <para>Composed on the <em>right</em> of the drape, so it acts before the cell's tangent
        /// plane is applied — the tile is reshaped and then draped, which keeps it on the plane of
        /// the cell it belongs to rather than tilting it by its own extra width. The squash is about
        /// <see cref="SlabLift"/> rather than about zero because that, not the cell floor plane, is
        /// where a slab's walking surface sits; squashing about the wrong plane would drop every
        /// floor in the game by eight millimetres and put it back where it z-fights the ground.</para>
        /// </summary>
        public static readonly Matrix4x4 FloorTile =
            Matrix4x4.Scale(new Vector3(
                (SizeXZ + 2f * FloorKnit) / SizeXZ, 1f, (SizeXZ + 2f * FloorKnit) / SizeXZ))
            * Matrix4x4.Translate(new Vector3(0f, SlabLift, 0f))
            * Matrix4x4.Scale(new Vector3(1f, FloorSheet, 1f))
            * Matrix4x4.Translate(new Vector3(0f, -SlabLift, 0f));

        /// <summary>The centre of the cell volume. Used for bounds, never for placement.</summary>
        public static Vector3 Centre(int x, int z, int y) =>
            new Vector3(x * SizeXZ + HalfXZ, y * SizeY + SizeY * 0.5f, z * SizeXZ + HalfXZ);

        /// <summary>
        /// The centre of the vertical face a cell shares with its neighbour in <paramref name="dir"/>,
        /// at floor level. Wall, window and door panels are placed on faces, not in cell centres,
        /// so that a one-cell-thick wall reads as a wall rather than as a 2.5 m block.
        /// </summary>
        public static Vector3 FaceCentre(int x, int z, int y, int dir)
        {
            Vector3 c = FloorCentre(x, z, y);
            switch (dir)
            {
                case Directions.North: return new Vector3(c.x, c.y, c.z + HalfXZ);
                case Directions.East: return new Vector3(c.x + HalfXZ, c.y, c.z);
                case Directions.South: return new Vector3(c.x, c.y, c.z - HalfXZ);
                default: return new Vector3(c.x - HalfXZ, c.y, c.z);
            }
        }

        /// <summary>World-space bounds of a whole layer of a grid, for camera framing.</summary>
        public static Bounds LayerBounds(GridSize size, int layer)
        {
            var min = new Vector3(0f, layer * SizeY, 0f);
            var max = new Vector3(size.SizeX * SizeXZ, (layer + 1) * SizeY, size.SizeZ * SizeXZ);
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }
    }

    /// <summary>
    /// The four horizontal directions, in a fixed order. The order is part of the render
    /// contract: face panels are emitted in this order so that a rebuilt chunk produces the same
    /// instance list as the one it replaced.
    /// </summary>
    public static class Directions
    {
        public const int North = 0; // +z
        public const int East = 1;  // +x
        public const int South = 2; // -z
        public const int West = 3;  // -x
        public const int Count = 4;

        public static readonly int[] DeltaX = { 0, 1, 0, -1 };
        public static readonly int[] DeltaZ = { 1, 0, -1, 0 };

        /// <summary>Yaw in degrees that turns a module's local +Z to face outward in this direction.</summary>
        public static readonly float[] Yaw = { 0f, 90f, 180f, 270f };

        public static int Opposite(int dir) => (dir + 2) & 3;
    }
}
