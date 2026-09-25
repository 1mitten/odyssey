#nullable enable

namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// One cell on the border of the colony's home (design 43 §5c), published only while the
    /// interface is showing the home. Only cells a colonist could stand in are published — open
    /// air over the ground or a floor — so the one layer of margin above and below a base does not
    /// draw an outline buried in the earth and another in the air.
    ///
    /// <para><see cref="Edges"/> says which of the four sides border a cell on the same layer that
    /// is not home: 1 west (-x), 2 east (+x), 4 south (-z), 8 north (+z). The board's edge counts
    /// as not home. Presentation draws a strip along each set side and works nothing out.</para>
    /// </summary>
    public readonly struct HomeCellView
    {
        public readonly int CellIndex;
        public readonly byte Edges;

        public HomeCellView(int cellIndex, byte edges)
        {
            CellIndex = cellIndex;
            Edges = edges;
        }
    }
}
