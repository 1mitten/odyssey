#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The automatic rescue (design 33 §1, §4, C4): a colonist whose Rescue priority is set carries
    /// a downed colonist to their own bed, else the nearest free one. An <b>emergency</b> giver, so
    /// it scans ahead of every ordinary one at the same priority. <b>The C4 lane's file</b>
    /// (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>A stub from the contracts step: it answers no.</b> It exists so the work type has a
    /// giver and the Work tab's column is a live one; discovered by existing, like every giver in
    /// this assembly (<see cref="WorkGiverRegistry"/>).</para>
    /// </summary>
    public sealed class RescueWorkGiver : WorkGiver
    {
        public override string Name => "Rescue";

        public override int WorkType => WorkTypeIndex.Rescue;

        public override bool Emergency => true;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job) => false;
    }
}
