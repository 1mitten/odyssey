#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    // A colonist's response to danger (design 33 §18): the setting, and the per-tick notice that
    // lets it act while she is working. In the job system for the reason HandleForceJob gives —
    // the notice ends jobs, and ending jobs is what the pipeline is.
    public sealed partial class JobSystem
    {
        /// <summary>
        /// <c>SetHostilityResponse(A = pawn, B = response)</c> (design 33 §18c).
        ///
        /// <para>Refused for a pawn that is not a colonist and for a value that is not a response;
        /// <c>AlreadyInThatState</c> for a no-op. Any colonist may be given one — drafted, downed
        /// or broken — because it is a standing setting, not an order to act; the draft overrides
        /// it for as long as it lasts.</para>
        ///
        /// <para><b>A new setting answers at once.</b> An undrafted colonist on a fight or a flight
        /// the new response would not have started (<see cref="HostilityResponses.Started"/>) is
        /// interrupted, keeping her step, and thinks again on her next tick.</para>
        /// </summary>
        public IntentRejection HandleSetHostilityResponse(Intent intent)
        {
            Pawn? pawn = _ctx.Pawns.Get(new PawnId(intent.A));
            if (pawn == null || !pawn.IsColonist) return IntentRejection.NotPermitted;
            if (intent.B < 0 || intent.B >= HostilityResponses.Count) return IntentRejection.NotPermitted;

            var want = (HostilityResponse)intent.B;
            if (pawn.Response == want) return IntentRejection.AlreadyInThatState;
            pawn.Response = want;

            if (!pawn.Drafted && !pawn.Downed && pawn.CurrentJob != null
                && !HostilityResponses.Started(want, pawn.CurrentJob))
                Interrupt(pawn, JobStatus.Failed);
            return IntentRejection.None;
        }

        // Whether anything hostile is about this tick (Melee.AnythingHostile): found by the first
        // colonist whose notice needs it, and forgotten when the next tick begins (Tick). Forgotten
        // rather than keyed on the tick number, which a load of an earlier save into the same world
        // would match with a stale answer.
        bool _hostilityKnown, _hostility;

        /// <summary>
        /// Is her response to act now, ending the job in hand (design 33 §18d)? One byte comparison
        /// for a colonist at the default; for the rest, nothing while nothing is hostile, and the
        /// response's own scan while something is.
        /// </summary>
        bool ResponseActs(Pawn pawn)
        {
            if (pawn.Response == HostilityResponse.FightBack || pawn.CurrentJob == null) return false;
            if (!_hostilityKnown)
            {
                _hostility = Melee.AnythingHostile(_ctx);
                _hostilityKnown = true;
            }
            return _hostility && HostilityResponses.Notices(_ctx, pawn);
        }
    }
}
