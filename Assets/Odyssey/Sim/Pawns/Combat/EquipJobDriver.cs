#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <c>Job_Equip</c> (design 33 §4, C3): walk to a weapon, take it into the hand
    /// (<see cref="Pawn.EquippedItem"/>), and put down whatever was there. <b>Lane D's file</b>
    /// (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>A stub from the contracts step: it fails.</b> See <see cref="AttackMeleeJobDriver"/>.</para>
    /// </summary>
    public class EquipJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;

        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }
}
