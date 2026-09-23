#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <c>Job_Flee</c> (design 33 §1, §3): run up to <see cref="CombatDef.fleeCells"/> from whatever
    /// hurt it — the animal that did not roll its revenge. <b>Lane A's file</b>
    /// (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>A stub from the contracts step: it fails.</b> See <see cref="AttackMeleeJobDriver"/>.</para>
    /// </summary>
    public class FleeJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;

        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }
}
