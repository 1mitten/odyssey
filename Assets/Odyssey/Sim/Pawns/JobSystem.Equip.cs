#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    // OrderEquip (design 33 §4, C3). Its own partial file so the lane that writes it edits no
    // file another lane owns: lane D (docs/plans/combat-contracts.md).
    public sealed partial class JobSystem
    {
        /// <summary>
        /// <c>OrderEquip(cell, A = colonist, B = weapon thing id)</c>: walk to it and take it into
        /// the hand. Asks <see cref="IWeaponRules.CanEquip"/> first and claims nothing on refusal.
        ///
        /// <para><b>Refuses everything until lane D writes it.</b></para>
        /// </summary>
        public IntentRejection HandleOrderEquip(Intent intent) => IntentRejection.NotPermitted;
    }
}
