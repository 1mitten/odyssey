#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Trade
{
    /// <summary>
    /// <c>Job_Trade</c> (design 65 §6): walk to the stance beside the trader (0), then negotiate (1)
    /// for as long as the session lasts. <see cref="Job.WorkTicks"/> carries the trader's pawn id,
    /// as the doctor's job carries the patient's.
    ///
    /// <para>Arriving makes the session ready — the trade window opens on the next publish — and
    /// the job ends of its own accord when the session does: the window's Cancel, a raid, the stay
    /// running out. However else the job ends — a draft, a fall, another order — its cleanup ends
    /// the session, so a negotiation can never outlive its negotiator.</para>
    ///
    /// <para>The trader is claimed for the length of the job (a pawn reservation), so two colonists
    /// cannot both be sent to haggle with it.</para>
    /// </summary>
    public class TradeJobDriver : JobDriver
    {
        /// <summary>How many times she re-aims at a trader that stepped on after she was sent.</summary>
        public const int MaxReaims = 3;

        public override bool TryMakeReservations(PawnContext ctx)
        {
            long key = ReservationManager.Key(ReservationTargetKind.Pawn, Job.WorkTicks);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            Visit? visit = ctx.Trade?.VisitOf(Job.WorkTicks);
            Pawn? trader = ctx.Pawns.Get(new PawnId(Job.WorkTicks));
            if (visit == null || trader == null || trader.Leaving || trader.Downed) return JobStatus.Failed;
            // Somebody else's session, or none: this job is over.
            if (visit.Negotiator != Pawn.Id.Value) return ToilIndex == 0 ? JobStatus.Failed : JobStatus.Succeeded;

            switch (ToilIndex)
            {
                case 0:
                {
                    JobStatus walk = GotoCell(ctx, Job.TargetCell);
                    if (walk == JobStatus.Failed) return JobStatus.Failed;
                    if (walk != JobStatus.Succeeded) return JobStatus.Ongoing;
                    // The trader finished the step it was taking when she was sent: re-aim, a few
                    // times at most, rather than trade from two cells off.
                    if (!TradeStance.IsBeside(ctx.Size, Pawn.Cell, trader.Cell))
                    {
                        ToilProgress += Rates.Scale;
                        if (ToilProgress > MaxReaims * Rates.Scale) return JobStatus.Failed;
                        Job.TargetCell = TradeStance.Beside(ctx, Pawn, trader.Cell);
                        return Job.TargetCell < 0 ? JobStatus.Failed : JobStatus.Ongoing;
                    }
                    Job.DestCell = trader.Cell;
                    visit.Ready = true;
                    visit.Session++;
                    NextToil();
                    return JobStatus.Ongoing;
                }

                default:
                    // Negotiating: she stands at the trader until the session ends.
                    return JobStatus.Ongoing;
            }
        }

        public override int WorkFocus => ToilIndex == 1 ? TraderCell : -1;

        int TraderCell => Job.DestCell;

        public override void Cleanup(PawnContext ctx, JobStatus status)
        {
            Visit? visit = ctx.Trade?.VisitOf(Job.WorkTicks);
            if (visit != null && visit.Negotiator == Pawn.Id.Value) ctx.Trade!.EndSession(visit);
        }
    }
}
