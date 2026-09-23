#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <c>Job_Downed</c> (design 33 §1, §6A): lie where you fell. Claims nothing, walks nowhere,
    /// reports no work. <b>Lane A's file</b> (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>It does not end itself on recovery.</b> Getting up is <see cref="CombatSystem"/>'s,
    /// on the heal that lifts the pawn past <see cref="CombatDef.downedRecoverAtPerMille"/>, and it
    /// ends this job as a success there; death ends it from the deferred removal. So the only
    /// thing this tick decides is that a pawn standing up has no business in it: a
    /// <c>Job_Downed</c> on a pawn that is not down fails on its first tick, the contract the
    /// stub made (<c>CombatContractTests.AStubDriverFailsOnItsFirstTick</c>).</para>
    ///
    /// <para>Never interruptible by a think — the downed node comes first in every tree — nor by
    /// a forced order (<c>JobSystem.CanForce</c>), a draft (<c>HandleSetDrafted</c>) or a break
    /// (going down sets <see cref="Pawn.BreakTicksLeft"/> to nought).</para>
    /// </summary>
    public class DownedJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;

        public override JobStatus Tick(PawnContext ctx) => Pawn.Downed ? JobStatus.Ongoing : JobStatus.Failed;
    }
}
