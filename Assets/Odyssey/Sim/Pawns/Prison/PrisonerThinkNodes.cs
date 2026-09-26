#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The minds of a pawn the colony holds (design 58 §6), chosen by custody ahead of the hostile
    /// tree: an escapee is hostile, but she is not a raider and must not ask a band what to do.
    /// Shared arrays; the nodes hold no state.
    /// </summary>
    public static class PrisonerTrees
    {
        /// <summary>A held prisoner: down, else stay put.</summary>
        static readonly ThinkNode[] Held = { new DownedThinkNode(), new PrisonerWaitThinkNode() };

        /// <summary>A prisoner breaking out. Until the escape is written she stays put like a held one.</summary>
        static readonly ThinkNode[] Escaping = { new DownedThinkNode(), new PrisonerWaitThinkNode() };

        /// <summary>A pawn let go. Until the walk off is written she stays put like a held one.</summary>
        static readonly ThinkNode[] Released = { new DownedThinkNode(), new PrisonerWaitThinkNode() };

        /// <summary>The tree for this custody. Never asked for <see cref="PawnCustody.Free"/>.</summary>
        public static ThinkNode[] For(PawnCustody custody) => custody switch
        {
            PawnCustody.Escaping => Escaping,
            PawnCustody.Released => Released,
            _ => Held,
        };

        /// <summary>The held prisoner's tree, in traversal order, so a test can assert it.</summary>
        public static System.Collections.Generic.IReadOnlyList<ThinkNode> HeldTree => Held;
    }

    /// <summary>A prisoner with nothing else to do stands where she is for a while, then thinks again.</summary>
    public sealed class PrisonerWaitThinkNode : ThinkNode
    {
        /// <summary>How long one wait lasts before the prisoner thinks again.</summary>
        public const int WaitTicks = 250;

        public override string Name => "PrisonerWait";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            job.Reset(JobIndex.Wait);
            job.WorkTicks = WaitTicks;
            return true;
        }
    }
}
