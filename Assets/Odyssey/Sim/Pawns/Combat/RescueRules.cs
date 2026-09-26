#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <b>Who needs carrying, and to which bed</b> (design 33 §11b). One owner for the questions
    /// the order, the giver, the driver and the published "no bed" all ask, so the four cannot
    /// disagree about whether a rescue is possible.
    /// </summary>
    public static class RescueRules
    {
        /// <summary>
        /// The mode a rescue walks in: a hauler's, because a colonist with a body in her arms does
        /// not climb a ladder, any more than one with a log.
        /// </summary>
        public const TraverseMode Mode = TraverseMode.Hauler;

        /// <summary>The reservation that says somebody is already coming for this patient.</summary>
        public static long PatientKey(Pawn patient) => ReservationManager.Key(ReservationTargetKind.Pawn, patient.Id.Value);

        /// <summary>The reservation on a bed's head cell: the sleeper's own key, so a sleeper and a rescuer see each other.</summary>
        public static long BedKey(int cell) => ReservationManager.Key(ReservationTargetKind.Cell, cell);

        /// <summary>
        /// A colonist lying where she fell: downed, in nobody's arms, and not already in a bed.
        /// An animal recovers where it lies and a downed bandit stays down (§11b). Asks
        /// <see cref="Pawn.Downed"/> first, so a colony nobody has hurt pays one flag a pawn.
        /// </summary>
        public static bool NeedsRescue(Pawn pawn, PawnContext ctx) =>
            pawn.Downed && pawn.IsColonist && pawn.CarriedBy == 0 && !Melee.IsDead(pawn) && !IsBed(ctx, pawn.Cell);

        /// <summary>Whether a cell is the head of a bed: the cell a sleeper lies on and a patient heals on.</summary>
        public static bool IsBed(PawnContext ctx, int cell)
        {
            var beds = ctx.Items.Beds;
            int low = 0, high = beds.Count - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                int value = beds[mid];
                if (value == cell) return true;
                if (value < cell) low = mid + 1;
                else high = mid - 1;
            }
            return false;
        }

        /// <summary>
        /// The bed <paramref name="patient"/> should be carried to by <paramref name="claimant"/>,
        /// or -1: her own bed if it is free, else the nearest free bed nobody owns, measured from
        /// her; never somebody else's (owner, §1). <i>Free</i> is a bed whose head cell the claimant
        /// could reserve and nobody lies on — a patient already in one holds its reservation (§11c)
        /// — and that she could be carried to. A walk over the beds, asked once per order or scan.
        ///
        /// <para><paramref name="asUser"/> asks from a pool other than her own: the capture asks as a
        /// prisoner for a raider who is not one until she is laid down (design 60 §7). One chooser
        /// for both carries, so a fix to either is a fix to both (review 2026-09-26).</para>
        /// </summary>
        public static int BedFor(Pawn patient, Pawn claimant, PawnContext ctx, BedUser? asUser = null)
        {
            var beds = ctx.Items.Beds;
            int best = -1, bestDistance = int.MaxValue;
            int me = patient.Id.Value;
            BedUser user = asUser ?? BedRules.UserOf(patient);
            for (int i = 0; i < beds.Count; i++)
            {
                int cell = beds[i];
                int owner = BedRules.OwnerAt(ctx, cell);
                if (!BedRule.MayUse(user, me, BedRules.PurposeAt(ctx, cell), owner)) continue;
                if (!ctx.Reservations.CanReserve(claimant.Id, BedKey(cell))) continue;
                if (SomebodyLiesIn(ctx, cell, patient)) continue;
                if (!ctx.CanTravel(patient, cell, Mode)) continue;
                if (owner == me) return cell;

                int distance = ctx.Distance(patient.Cell, cell);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = cell;
            }
            return best;
        }

        /// <summary>Somebody other than <paramref name="except"/> lying on this cell, asleep or down.</summary>
        static bool SomebodyLiesIn(PawnContext ctx, int cell, Pawn except)
        {
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == except || pawn.Cell != cell || pawn.CarriedBy != 0) continue;
                if (pawn.Downed || pawn.Asleep) return true;
            }
            return false;
        }

        /// <summary>
        /// Put a carried patient down where her carrier stands, or on the nearest cell she may lie
        /// on if that one will not take her, and let go of her. Every way a rescue ends that is not
        /// the lay goes through here (§11a): nobody is left in a pair of arms.
        /// </summary>
        public static void SetDown(PawnContext ctx, Pawn patient, int at)
        {
            patient.CarriedBy = 0;
            patient.ClearPath();
            patient.Destination = -1;
            if (at < 0) return;
            if (!ctx.Nav.Grid.CanEnter(at, patient.Mode))
            {
                int nearest = PawnEviction.NearestStandable(ctx, at, patient.Mode);
                if (nearest >= 0) at = nearest;
            }
            patient.Cell = at;
        }
    }
}
