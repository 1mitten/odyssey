#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <c>Job_Downed</c> (design 33 §1): lie where you fell until your hit points reach
    /// <see cref="CombatDef.downedRecoverAtPerMille"/> of the pool, or somebody carries you (C4),
    /// or you die. Never interruptible by a think. <b>Lane A's file</b>
    /// (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>A stub from the contracts step: it fails.</b> See <see cref="AttackMeleeJobDriver"/>.</para>
    /// </summary>
    public class DownedJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;

        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }
}
