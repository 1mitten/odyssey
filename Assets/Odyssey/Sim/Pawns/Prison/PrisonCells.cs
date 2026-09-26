#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Whether a cell lies inside a prison cell (design 59 §5b, §14): the one question the food
    /// rules ask, so a colonist does not eat a prisoner's meal and a hauler does not carry it back
    /// out through the door. Free on a board with no prison bed.
    /// </summary>
    public static class PrisonCells
    {
        public static bool Holds(PawnContext ctx, int cell)
        {
            BedPurposes? purposes = ctx.BedPurposes;
            if (purposes == null || !purposes.Any || ctx.Enclosure == null || cell < 0) return false;
            return purposes.IsCell(ctx.Enclosure.RoomAt(cell));
        }
    }
}
