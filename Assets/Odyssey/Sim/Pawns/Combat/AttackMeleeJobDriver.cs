#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <c>Job_AttackMelee</c> (design 33 §3): close on the target, re-planning every
    /// <see cref="CombatDef.chaseRepathTicks"/>, and swing on <see cref="Pawn.NextSwingTick"/>,
    /// reporting <see cref="Contracts.PawnGesture.Strike"/> as the wind-up starts. The target is
    /// <see cref="Pawn.CombatTarget"/>, or the job's <see cref="Job.TargetCell"/> for a building
    /// (C6). <b>Lane A's file</b> (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>A stub from the contracts step: it fails.</b> The handle, the def and the pooled
    /// instance are real so the save contract is fixed; nothing starts this job until lane A
    /// writes it, and one that did would end on its first tick rather than stand there.</para>
    /// </summary>
    public class AttackMeleeJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;

        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }
}
