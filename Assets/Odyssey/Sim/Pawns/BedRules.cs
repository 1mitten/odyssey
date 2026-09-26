using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The simulation's side of <see cref="BedRule"/> (design 58 §5a): which pool a live pawn
    /// sleeps from, and whether a given bed is one it may use. Every bed chooser — the sleep's
    /// (<c>CriticalNeedsThinkNode.TrySleep</c>), the patient's (<see cref="Medical.BedFor"/>), the
    /// rescue's (<c>RescueRules.BedFor</c>), the sleeper's claim
    /// (<c>ConstructionGrid.TryClaimForSleeper</c>) and the wrong-bed sweep — asks here, so a bed
    /// that stops being somebody's kind of bed stops being chosen everywhere at once.
    /// </summary>
    public static class BedRules
    {
        /// <summary>
        /// The pool this pawn sleeps from. A colonist sleeps in colony beds and a prisoner in prison
        /// beds — an escapee too, so the bed she owns survives a sweep while she is out and is the
        /// one she is carried back to (review 2026-09-26); everybody else (an animal, a bandit at
        /// large, a pawn walking off the board) in none. The interface's answer is <see cref="BedRule.UserOf"/>, and the
        /// two are held to agree.
        /// </summary>
        public static BedUser UserOf(Pawn pawn) =>
            pawn.IsColonist ? BedUser.Colonist
            : pawn.Custody == PawnCustody.Prisoner || pawn.Custody == PawnCustody.Escaping ? BedUser.Prisoner
            : BedUser.None;

        /// <summary>What the bed at this cell is for; a colony bed where there is no construction grid.</summary>
        public static BedPurpose PurposeAt(PawnContext ctx, int cell) =>
            ctx.Construction?.BedPurposeAt(cell) ?? BedPurpose.Colony;

        /// <summary>Who owns the bed at this cell, 0 for nobody or no grid.</summary>
        public static int OwnerAt(PawnContext ctx, int cell) => ctx.Construction?.BedOwnerAt(cell) ?? 0;

        /// <summary>
        /// Whether <paramref name="pawn"/> may sleep, heal or be laid in the bed at
        /// <paramref name="cell"/>: the right kind of bed, and hers or nobody's. Reservations,
        /// reachability and distance stay the chooser's.
        /// </summary>
        public static bool CanUse(Pawn pawn, int cell, PawnContext ctx) =>
            BedRule.MayUse(UserOf(pawn), pawn.Id.Value, PurposeAt(ctx, cell), OwnerAt(ctx, cell));

        /// <summary>Whether two pawns sleep from the same pool, so that one's claim can leave the other without a bed.</summary>
        public static bool SharesPool(Pawn a, Pawn b)
        {
            BedUser user = UserOf(a);
            return user != BedUser.None && user == UserOf(b);
        }
    }
}
