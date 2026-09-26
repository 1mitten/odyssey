#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Construction
{
    /// <summary>
    /// Where a built thing's cells are, derived rather than stored.
    ///
    /// <para>A one-cell thing occupies its own cell. A two-cell thing — the bed, until something
    /// else wants a line of cells — occupies its own cell and the next one along its facing, and
    /// that second cell is <b>computed here and nowhere else</b>: the record carries the facing
    /// because placement is where the answer is known, and the def's own
    /// <see cref="BuildingDef.footprint"/> says whether there is a second cell at all, so no
    /// stored index can ever disagree with the facing it was derived from. Both cells'
    /// <c>CellGrid.Edifice</c> slots point at the one record — that is where "one bed, two cells"
    /// actually lives (docs/design/20-beds.md §4).</para>
    ///
    /// <para>The one question this shape cannot answer by itself is the reverse lookup — which
    /// site or record a click on the <i>second</i> cell means — and the grids answer it with the
    /// pointer they already hold: a built thing's second cell points straight at the record, and
    /// <see cref="ConstructionGrid.SiteAt"/> asks this class which head would claim a cell.</para>
    /// </summary>
    public static class EdificeFootprint
    {
        /// <summary>
        /// The cells this edifice value occupies: 2 for the bed, 1 for everything else. Asked of
        /// the content rather than the record, so a def that grows a footprint changes one row and
        /// every record old and new follows.
        /// </summary>
        public static int Cells(ushort edifice)
        {
            int building = ConstructionContent.BuildingForEdifice(edifice);
            return building == BuildingHandle.None ? 1 : ConstructionContent.BuildingAt(building).footprint;
        }

        /// <summary>
        /// The second cell of a thing stood at <paramref name="head"/> with this facing, or -1
        /// when the thing is one cell or the second would leave the board. North is +Z, east +X,
        /// south −Z, west −X — the same directions <c>Directions.Yaw</c> draws, so what is stored
        /// and what is drawn cannot disagree about which way a bed faces.
        /// </summary>
        public static int SecondCell(int head, ushort edifice, int facing, GridSize size)
        {
            if (Cells(edifice) < 2) return -1;

            CellRef at = size.FromIndex(head);
            int f = facing & 3;
            int x = at.X + (f == 1 ? 1 : f == 3 ? -1 : 0);
            int z = at.Z + (f == 0 ? 1 : f == 2 ? -1 : 0);
            return size.Contains(x, z, at.Y) ? size.Index(x, z, at.Y) : -1;
        }

        /// <summary>
        /// Whether <paramref name="cell"/> is the second cell of a two-cell thing whose head is
        /// <paramref name="head"/> — the question "does this site claim the cell beside it", asked
        /// by placement (so a wall cannot be ordered into a bed's second cell) and by
        /// <c>SiteAt</c> (so clicking either half finds the one site).
        /// </summary>
        public static bool Claims(int head, ushort edifice, int facing, int cell, GridSize size) =>
            SecondCell(head, edifice, facing, size) == cell;
    }
}
