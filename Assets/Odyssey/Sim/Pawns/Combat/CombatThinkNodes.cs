#nullable enable

namespace Odyssey.Sim.Pawns
{
    // The fight's four places in the three minds (design 33 §3, §5), cut by the contracts step
    // and filled by lane A (docs/plans/combat-contracts.md). Each answers no until then, so the
    // trees behave exactly as before combat: a node that declines is a node the traversal walks
    // past. Where each sits is JobSystem's decision and is written there; what each does is lane
    // A's and is written here.

    /// <summary>
    /// First in every tree: a downed pawn lies where it fell (<c>Job_Downed</c>) and does nothing
    /// else — no break, no draft, no meal. <b>Lane A.</b> Declines until lane A writes it.
    /// </summary>
    public class DownedThinkNode : ThinkNode
    {
        public override string Name => "Downed";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job) => false;
    }

    /// <summary>
    /// A colonist's answer to being struck (design 33 §1): fight back against a colonist who hit
    /// her (<see cref="Pawn.RetaliateAgainst"/>), and hit a hostile on an adjacent cell. Below the
    /// draft — a drafted colonist's hold does its own fighting — and above the needs, because
    /// being hit outranks being hungry. <b>Lane A.</b> Declines until lane A writes it.
    /// </summary>
    public class SelfDefenceThinkNode : ThinkNode
    {
        public override string Name => "SelfDefence";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job) => false;
    }

    /// <summary>
    /// A marauder's whole purpose (design 33 §1): hunt the nearest reachable colonist who is
    /// standing, and attack. <b>Lane A.</b> Declines until lane A writes it, and the hostile tree
    /// then idles.
    /// </summary>
    public class HostileThinkNode : ThinkNode
    {
        public override string Name => "Hostile";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job) => false;
    }

    /// <summary>
    /// An animal that has been hurt (design 33 §1): turn on its attacker while
    /// <see cref="Pawn.RetaliateAgainst"/> holds, or run. Ahead of the animal's idle mind.
    /// <b>Lane A.</b> Declines until lane A writes it.
    /// </summary>
    public class AnimalCombatThinkNode : ThinkNode
    {
        public override string Name => "AnimalCombat";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job) => false;
    }
}
