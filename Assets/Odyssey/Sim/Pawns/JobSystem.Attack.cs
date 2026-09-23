#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    // OrderAttack (design 33 §2d, §5). Its own partial file so the lane that writes it edits no
    // file another lane owns: lane A (docs/plans/combat-contracts.md). On the job system for the
    // reason HandleForceJob gives — starting and ending jobs is what the pipeline is.
    public sealed partial class JobSystem
    {
        /// <summary>
        /// <c>OrderAttack(cell, A = attacker, B = target pawn, or 0 and the cell a building)</c>.
        /// A drafted colonist closes on the target and swings until one of them goes down; the job
        /// is forced, like a move (design 33 §2c).
        ///
        /// <para><b>Refuses everything until lane A writes it</b>, which is the honest answer to
        /// an order nothing can yet carry out.</para>
        /// </summary>
        public IntentRejection HandleOrderAttack(Intent intent) => IntentRejection.NotPermitted;
    }
}
