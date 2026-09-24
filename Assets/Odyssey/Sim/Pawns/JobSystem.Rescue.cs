#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    // OrderRescue (design 33 §4, C4). Its own partial file so the lane that writes it edits no
    // file another lane owns: the C4 lane (docs/plans/combat-contracts.md).
    public sealed partial class JobSystem
    {
        /// <summary>
        /// <c>OrderRescue(cell, A = rescuer, B = downed colonist)</c>: a drafted colonist carries a
        /// downed one to their own bed, else the nearest free one (design 33 §1).
        ///
        /// <para><b>Refuses everything until C4 writes it.</b></para>
        /// </summary>
        public IntentRejection HandleOrderRescue(Intent intent) => IntentRejection.NotPermitted;
    }
}
