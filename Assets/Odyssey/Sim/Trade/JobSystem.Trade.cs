#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Trade;

namespace Odyssey.Sim.Pawns
{
    public partial class JobSystem
    {
        /// <summary>
        /// The right-click's Trade (design 57 §6): one colonist sent to negotiate with a trader,
        /// drafted or not, as Tend is. The trader is held where it stands from this moment (the
        /// session is open but not yet ready), and the window opens when she arrives beside it.
        ///
        /// <para>Refused for a negotiator who is not a colonist, is down or broken; for a pawn that is
        /// no trader, or one leaving or down; and while somebody else is negotiating. Sending the same
        /// colonist again is <see cref="IntentRejection.AlreadyInThatState"/>.</para>
        /// </summary>
        public IntentRejection HandleOrderTrade(Intent intent)
        {
            Pawn? negotiator = _ctx.Pawns.Get(new PawnId(intent.A));
            if (negotiator == null || !negotiator.IsColonist || negotiator.Downed || negotiator.IsBroken)
                return IntentRejection.NotPermitted;

            Pawn? trader = intent.B == 0 ? null : _ctx.Pawns.Get(new PawnId(intent.B));
            Visit? visit = trader == null ? null : _ctx.Trade?.VisitOf(trader.Id.Value);
            if (trader == null || visit == null || trader.Leaving || trader.Downed) return IntentRejection.NotPermitted;
            if (visit.Negotiator == negotiator.Id.Value) return IntentRejection.AlreadyInThatState;
            if (visit.Negotiator != 0) return IntentRejection.NotPermitted;

            int stance = TradeStance.Beside(_ctx, negotiator, trader.Cell);
            if (stance < 0) return IntentRejection.NotPermitted;

            int tick = IntentTick;
            if (negotiator.Drafted) negotiator.DraftQuietSinceTick = tick;
            Interrupt(negotiator, JobStatus.Failed);

            Job job = negotiator.JobBuffer;
            job.Reset(JobIndex.Trade);
            job.WorkTicks = trader.Id.Value;
            job.TargetCell = stance;
            job.DestCell = trader.Cell;
            job.PlayerForced = true;
            visit.Negotiator = negotiator.Id.Value;
            visit.Ready = false;
            if (StartJob(negotiator, job, tick))
            {
                // The trader stops where it is: its walk is interrupted (the step in hand kept), and
                // its next think holds it for the session. The negotiator re-aims if that step
                // carried it past the stance she was given.
                if (!trader.Downed && trader.CurrentJob != null) Interrupt(trader, JobStatus.Failed);
                return IntentRejection.None;
            }
            _ctx.Trade!.EndSession(visit);
            return IntentRejection.NotPermitted;
        }
    }
}
