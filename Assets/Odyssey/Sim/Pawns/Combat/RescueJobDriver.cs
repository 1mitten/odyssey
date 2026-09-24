#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <c>Job_Rescue</c> (design 33 §4, C4): walk to a downed colonist, lift them
    /// (<see cref="Pawn.CarriedBy"/>), carry them to their own bed or the nearest free one, and lay
    /// them in it. <b>The C4 lane's file</b> (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>A stub from the contracts step: it fails.</b> See <see cref="AttackMeleeJobDriver"/>.</para>
    /// </summary>
    public class RescueJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;

        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }
}
