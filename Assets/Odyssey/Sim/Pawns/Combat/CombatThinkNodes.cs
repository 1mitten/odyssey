#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    // The fight's four places in the three minds (design 33 §3, §6A). Where each sits is
    // JobSystem's decision and is written there; what each does is lane A's and is written here.
    // Every node declines for a pawn at peace — nobody down, nobody struck, no hostile about — so a
    // colony that has never fought thinks exactly as it did before combat, and no golden moved.

    /// <summary>
    /// First in every tree: a downed pawn lies where it fell (<c>Job_Downed</c>) and does nothing
    /// else — no break, no draft, no meal. One branch for anybody standing.
    /// </summary>
    public class DownedThinkNode : ThinkNode
    {
        public override string Name => "Downed";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (!pawn.Downed) return false;
            job.Reset(JobIndex.Downed);
            job.Mode = pawn.Species.traverseMode;
            return true;
        }
    }

    /// <summary>
    /// Fill <paramref name="job"/> with a melee attack on <paramref name="target"/>, and name the
    /// target on the pawn, where the driver reads it. Shared by every node that fights.
    /// </summary>
    static class AttackJob
    {
        public static bool Fill(Pawn pawn, Pawn target, Job job, TraverseMode mode)
        {
            job.Reset(JobIndex.AttackMelee);
            job.TargetCell = target.Cell;
            job.Mode = mode;
            pawn.CombatTarget = target.Id.Value;
            return true;
        }
    }

    /// <summary>
    /// A colonist's answer to being struck (design 33 §1): fight back against a colonist who hit
    /// her while <see cref="Pawn.RetaliateAgainst"/> holds, and hit a threat beside her — a
    /// hostile, or anybody attacking her (<see cref="Melee.AdjacentThreat"/>). Below the draft — a
    /// drafted colonist's hold does its own fighting — and above the needs, because being hit
    /// outranks being hungry. A blow interrupts whatever she was doing
    /// (<c>CombatSystem.React</c>), which is what brings her here. <b>Scales with the pawns on
    /// the board</b> per think, for the threat scan.
    /// </summary>
    public class SelfDefenceThinkNode : ThinkNode
    {
        public override string Name => "SelfDefence";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (!pawn.IsColonist || pawn.Downed || pawn.IsBroken) return false;

            if (pawn.RetaliateAgainst != 0 && ctx.CurrentTick < pawn.RetaliateUntilTick)
            {
                Pawn? foe = ctx.Pawns.Get(new PawnId(pawn.RetaliateAgainst));
                if (foe != null && Melee.IsStanding(foe) && ctx.Reachable(pawn, foe.Cell, TraverseMode.Colonist))
                    return AttackJob.Fill(pawn, foe, job, TraverseMode.Colonist);
            }

            Pawn? threat = Melee.AdjacentThreat(ctx, pawn);
            return threat != null && AttackJob.Fill(pawn, threat, job, TraverseMode.Colonist);
        }
    }

    /// <summary>
    /// A marauder's whole purpose (design 33 §1): hunt the nearest reachable colonist who is
    /// standing, and attack. A downed colonist is not hunted; a marauder with nobody left to
    /// hunt falls through to idling. The attack it starts re-chooses after
    /// <see cref="AttackMeleeJobDriver.RechooseTicks"/>, so a nearer colonist is noticed.
    /// <b>Scales with the pawns on the board</b> per think: one pass, a reachability test (two
    /// array reads) for each standing colonist nearer than the best so far.
    /// </summary>
    public class HostileThinkNode : ThinkNode
    {
        public override string Name => "Hostile";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (pawn.Downed) return false;
            TraverseMode mode = pawn.Species.traverseMode;

            Pawn? best = null;
            int bestDistance = int.MaxValue;
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (!other.IsColonist || !Melee.IsStanding(other)) continue;
                int distance = ctx.Distance(pawn.Cell, other.Cell);
                if (distance >= bestDistance) continue;
                if (!ctx.Reachable(pawn, other.Cell, mode)) continue;
                best = other;
                bestDistance = distance;
            }

            return best != null && AttackJob.Fill(pawn, best, job, mode);
        }
    }

    /// <summary>
    /// An animal that has been hurt (design 33 §1): turn on its attacker while
    /// <see cref="Pawn.RetaliateAgainst"/> holds and the attacker is standing and reachable. The
    /// revenge is rolled at the blow (<c>CombatSystem.React</c>, on <see cref="PawnPurpose.Revenge"/>),
    /// and so is the flight, which starts <c>Job_Flee</c> there and then; this node only carries a
    /// revenge on across thinks. Ahead of the animal's idle mind; one branch for an animal nobody
    /// has hurt.
    /// </summary>
    public class AnimalCombatThinkNode : ThinkNode
    {
        public override string Name => "AnimalCombat";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (pawn.RetaliateAgainst == 0 || pawn.Downed || ctx.CurrentTick >= pawn.RetaliateUntilTick) return false;

            Pawn? foe = ctx.Pawns.Get(new PawnId(pawn.RetaliateAgainst));
            if (foe == null || !Melee.IsStanding(foe)) return false;

            TraverseMode mode = pawn.Species.traverseMode;
            if (!ctx.Reachable(pawn, foe.Cell, mode)) return false;
            return AttackJob.Fill(pawn, foe, job, mode);
        }
    }
}
