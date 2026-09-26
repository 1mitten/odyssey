#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Who is to be brought in, and to which bed (design 60 §7). The capture's own questions, in the
    /// shape of <see cref="RescueRules"/>, whose carry, keys and set-down it shares.
    /// </summary>
    public static class CaptureRules
    {
        /// <summary>The mode a capture walks in: a hauler's, as a rescue's is.</summary>
        public const TraverseMode Mode = RescueRules.Mode;

        /// <summary>
        /// Somebody lying downed whom the colony means to hold: a person the player marked for
        /// capture, or a prisoner lying anywhere but a prison bed — downed breaking out, or
        /// never yet put in one. In nobody's arms and alive. Asks <see cref="Pawn.Downed"/> first,
        /// so a colony nobody hurt pays one flag a pawn.
        /// </summary>
        public static bool WantsCapture(Pawn pawn, PawnContext ctx)
        {
            if (!pawn.Downed || !pawn.IsPerson || pawn.CarriedBy != 0 || Melee.IsDead(pawn)) return false;
            if (pawn.Custody == PawnCustody.Free)
                return pawn.Prison != null && pawn.Prison.CaptureMark && !pawn.IsColonist;
            return pawn.IsPrisoner && !InAPrisonBed(pawn, ctx);
        }

        /// <summary>Lying on the head of a prison bed that is hers or nobody's.</summary>
        public static bool InAPrisonBed(Pawn pawn, PawnContext ctx) =>
            RescueRules.IsBed(ctx, pawn.Cell)
            && BedRules.PurposeAt(ctx, pawn.Cell) == BedPurpose.Prison
            && BedRule.MayUse(BedUser.Prisoner, pawn.Id.Value, BedPurpose.Prison, BedRules.OwnerAt(ctx, pawn.Cell));

        /// <summary>
        /// The prison bed <paramref name="target"/> should be carried to by
        /// <paramref name="claimant"/>, or -1: her own if it is free, else the nearest free prison
        /// bed nobody owns. Asked as if she were already a prisoner — a raider being brought in is
        /// not one until she is laid down — through the one bed rule, never a copy of it.
        /// </summary>
        public static int BedFor(Pawn target, Pawn claimant, PawnContext ctx) =>
            RescueRules.BedFor(target, claimant, ctx, BedUser.Prisoner);

        /// <summary>Whether any prison bed on the board is free for a new prisoner: the alert's question.</summary>
        public static bool AnyFreePrisonBed(PawnContext ctx)
        {
            var beds = ctx.Items.Beds;
            for (int i = 0; i < beds.Count; i++)
                if (BedRules.PurposeAt(ctx, beds[i]) == BedPurpose.Prison && BedRules.OwnerAt(ctx, beds[i]) == 0)
                    return true;
            return false;
        }
    }
}
