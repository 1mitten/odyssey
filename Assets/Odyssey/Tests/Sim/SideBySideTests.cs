#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Side by side (design 33 §7c): several pawns attacking one target each stand on a different
    /// cell beside it — the free one nearest the attacker — and never on the same tile. All eight
    /// sides taken, the next waits one ring back until a side frees up. The target moving, they
    /// spread again round where it stopped.
    ///
    /// <para>Every fight here is fought with <see cref="Whiffs"/>, rules under which every swing
    /// misses, so nobody goes down and the positions are the only thing that changes. The target
    /// is a drafted colonist, whose hold answers her attackers from where she stands and never
    /// moves her.</para>
    /// </summary>
    public class SideBySideTests
    {
        /// <summary>The shipped rules with every swing a miss: a fight that lasts for ever.</summary>
        sealed class Whiffs : MeleeRules
        {
            public int Swings;

            public override SwingOutcome Resolve(Pawn attacker, Pawn defender, in Armament armament, PawnContext ctx, int tick)
            {
                Swings++;
                return new SwingOutcome(CombatEventKind.Miss);
            }
        }

        /// <summary>
        /// A board of <paramref name="attackers"/> drafted colonists and a drafted target at the
        /// start, each attacker stood where <paramref name="at"/> says, relative to the target.
        /// </summary>
        static (ColonyWorld colony, Pawn target, List<Pawn> attackers, Whiffs rules) Fight(
            int attackers, System.Func<int, (int dx, int dz)> at, bool order = true)
        {
            var colony = Board(colonists: attackers + 1);
            colony.World.Tick();
            var all = colony.Pawns.Pawns.All;
            Pawn target = all[0];
            Stand(colony, target, Near(colony, 0, 0));
            var list = new List<Pawn>();
            for (int i = 0; i < attackers; i++)
            {
                Pawn a = all[i + 1];
                var (dx, dz) = at(i);
                Stand(colony, a, Near(colony, dx, dz));
                list.Add(a);
            }

            var rules = new Whiffs();
            colony.Pawns.MeleeRules = rules;
            Assert.That(Draft(colony, target), Is.EqualTo(IntentRejection.None));
            foreach (Pawn a in list) Assert.That(Draft(colony, a), Is.EqualTo(IntentRejection.None));
            if (order)
                foreach (Pawn a in list) Assert.That(Attack(colony, a, target), Is.EqualTo(IntentRejection.None));
            return (colony, target, list, rules);
        }

        static int Chebyshev(GridSize size, int a, int b)
        {
            CellRef p = size.FromIndex(a), q = size.FromIndex(b);
            if (p.Y != q.Y) return int.MaxValue;
            return System.Math.Max(System.Math.Abs(p.X - q.X), System.Math.Abs(p.Z - q.Z));
        }

        /// <summary>Where an attacker means to stand: the cell she walks to, or the one she stands on.</summary>
        static int Claim(Pawn p) => p.Destination >= 0 ? p.Destination : p.Cell;

        static string Describe(Pawn p) =>
            $"[{p.Id.Value} cell {Size.FromIndex(p.Cell)} dest {p.Destination} move {p.MoveProgress} path {p.HasPath}/{p.PathPending} "
            + $"toil {p.Driver?.ToilIndex} job {p.CurrentJob?.DefIndex} tc {p.CurrentJob?.TargetCell} wt {p.CurrentJob?.WorkTicks} finishing {p.FinishingStepTo}]";

        /// <summary>
        /// No two attackers standing on one tile, and no two meaning to. Walkers may pass over one
        /// another's cells — the simulation has no collision — so the claim is what is compared.
        /// </summary>
        static void AssertApart(List<Pawn> attackers, int tick)
        {
            for (int i = 0; i < attackers.Count; i++)
            for (int j = i + 1; j < attackers.Count; j++)
            {
                Pawn a = attackers[i], b = attackers[j];
                if (a.CurrentJob?.DefIndex != JobIndex.AttackMelee || b.CurrentJob?.DefIndex != JobIndex.AttackMelee) continue;
                // Still landing the step the order interrupted: the job has not run yet (§2d).
                if (a.FinishingStepTo >= 0 || b.FinishingStepTo >= 0) continue;
                if (a.Destination < 0 && b.Destination < 0)
                    Assert.That(a.Cell, Is.Not.EqualTo(b.Cell), $"tick {tick}: {a.Id.Value} and {b.Id.Value} stand on one tile; {Describe(a)} {Describe(b)}");
                Assert.That(Claim(a), Is.Not.EqualTo(Claim(b)), $"tick {tick}: {a.Id.Value} and {b.Id.Value} claim one tile");
            }
        }

        static void Run(ColonyWorld colony, List<Pawn> attackers, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                colony.World.Tick();
                AssertApart(attackers, colony.World.CurrentTick);
            }
        }

        static bool Settled(ColonyWorld colony, Pawn target, List<Pawn> attackers) =>
            attackers.All(a => a.Destination < 0 && a.CurrentJob?.DefIndex == JobIndex.AttackMelee)
            && attackers.Count(a => Melee.InReach(colony.Pawns, a, target, TraverseMode.Colonist))
               == System.Math.Min(8, attackers.Count);

        /// <summary>
        /// Two attackers coming from the west: the first takes the west side, the second the cell
        /// beside it on the same flank, and both swing. Before §7c both chased the target's own
        /// cell and stopped at the first cell in reach — the same one.
        /// </summary>
        [Test]
        public void TwoFromTheSameSideStandSideBySide()
        {
            var (colony, target, attackers, rules) = Fight(2, i => (i == 0 ? -5 : -7, 0));
            Run(colony, attackers, 900);

            Pawn a = attackers[0], b = attackers[1];
            Assert.That(rules.Swings, Is.GreaterThan(4), "the control: a fight happened");
            Assert.That(Melee.InReach(colony.Pawns, a, target, TraverseMode.Colonist), Is.True, "the first is not beside the target");
            Assert.That(Melee.InReach(colony.Pawns, b, target, TraverseMode.Colonist), Is.True, "the second is not beside the target");
            Assert.That(a.Cell, Is.Not.EqualTo(b.Cell), "two attackers on one tile");
            Assert.That(a.Cell, Is.Not.EqualTo(target.Cell));
            Assert.That(b.Cell, Is.Not.EqualTo(target.Cell));

            // Both on the flank they came from: the nearest free side, not the far one.
            CellRef t = Size.FromIndex(target.Cell);
            Assert.That(Size.FromIndex(a.Cell).X, Is.EqualTo(t.X - 1), "the first went round");
            Assert.That(Size.FromIndex(b.Cell).X, Is.EqualTo(t.X - 1), "the second went round");
        }

        /// <summary>
        /// Nine on one: eight stand on the eight sides, and the ninth waits one ring back, not
        /// beside the target, on no other attacker's tile.
        /// </summary>
        [Test]
        public void NineOnOneFillsTheEightSidesAndTheNinthWaitsARingBack()
        {
            var (colony, target, attackers, rules) = Fight(9, i => (-6, i - 4));
            Run(colony, attackers, 1_500);

            Assert.That(rules.Swings, Is.GreaterThan(20), "the control: a fight happened");
            var cells = attackers.Select(a => a.Cell).ToList();
            Assert.That(cells.Distinct().Count(), Is.EqualTo(9), "two attackers on one tile");
            var rings = attackers.Select(a => Chebyshev(Size, a.Cell, target.Cell)).ToList();
            Assert.That(rings.Count(r => r == 1), Is.EqualTo(8), $"rings {string.Join(",", rings)}");
            Assert.That(rings.Count(r => r == 2), Is.EqualTo(1), $"rings {string.Join(",", rings)}");
            Pawn waiter = attackers[rings.IndexOf(2)];
            Assert.That(Melee.InReach(colony.Pawns, waiter, target, TraverseMode.Colonist), Is.False);
            Assert.That(attackers.Count(a => Melee.InReach(colony.Pawns, a, target, TraverseMode.Colonist)), Is.EqualTo(8));
        }

        /// <summary>
        /// A side frees up — one of the eight is sent away — and the one waiting a ring back takes
        /// it within the chase's own cadence.
        /// </summary>
        [Test]
        public void TheOneWaitingTakesASideThatFreesUp()
        {
            var (colony, target, attackers, _) = Fight(9, i => (-6, i - 4));
            Run(colony, attackers, 1_500);
            Pawn waiter = attackers.Single(a => Chebyshev(Size, a.Cell, target.Cell) == 2);
            Pawn leaver = attackers.First(a => a != waiter);

            // Sent away: a move order ends her attack.
            Assert.That(Send(colony, new Intent(IntentKind.OrderMove, Size.FromIndex(Near(colony, 12, 12)), leaver.Id.Value)),
                Is.EqualTo(IntentRejection.None));
            var stay = attackers.Where(a => a != leaver).ToList();
            Run(colony, stay, 400);

            Assert.That(Chebyshev(Size, waiter.Cell, target.Cell), Is.EqualTo(1), "the waiter never took the free side");
            Assert.That(Melee.InReach(colony.Pawns, waiter, target, TraverseMode.Colonist), Is.True);
            Assert.That(stay.Select(a => a.Cell).Distinct().Count(), Is.EqualTo(8));
        }

        /// <summary>
        /// The target walks off and stops: the attackers follow and spread again round where it
        /// stopped, apart at every tick of the way.
        /// </summary>
        [Test]
        public void ATargetThatMovesIsSurroundedAgainWhereItStops()
        {
            var (colony, target, attackers, _) = Fight(4, i => (-6, i - 2));
            Run(colony, attackers, 900);
            Assert.That(Settled(colony, target, attackers), Is.True, "the control: they spread before it moved");

            int away = Near(colony, 10, 6);
            Assert.That(Send(colony, new Intent(IntentKind.OrderMove, Size.FromIndex(away), target.Id.Value)),
                Is.EqualTo(IntentRejection.None));
            Run(colony, attackers, 1_200);

            Assert.That(target.Cell, Is.EqualTo(away), "the target never got there");
            Assert.That(attackers.All(a => Chebyshev(Size, a.Cell, target.Cell) == 1), Is.True,
                $"not all beside it: {string.Join(",", attackers.Select(a => Chebyshev(Size, a.Cell, target.Cell)))}");
            Assert.That(attackers.Select(a => a.Cell).Distinct().Count(), Is.EqualTo(4), "two on one tile");
        }

        /// <summary>
        /// Every melee attacker spreads, not only a drafted colonist under orders: three bandits
        /// hunting one colonist stand on three sides of her.
        /// </summary>
        [Test]
        public void BanditsHuntingOneColonistSpreadRoundHer()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick();
            Pawn target = colony.Pawns.Pawns.All[0];
            Stand(colony, target, Near(colony, 0, 0));
            var rules = new Whiffs();
            colony.Pawns.MeleeRules = rules;
            Assert.That(Draft(colony, target), Is.EqualTo(IntentRejection.None));
            var bandits = new List<Pawn>();
            for (int i = 0; i < 3; i++) bandits.Add(Spawn(colony, PawnKindIndex.Bandit, Near(colony, -8, i - 1)));

            Run(colony, bandits, 1_200);
            Assert.That(rules.Swings, Is.GreaterThan(4), "the control: a fight happened");
            Assert.That(bandits.All(m => m.CombatTarget == target.Id.Value), Is.True);
            Assert.That(bandits.All(m => Melee.InReach(colony.Pawns, m, target, TraverseMode.Colonist)), Is.True, "a bandit not beside her");
            Assert.That(bandits.All(m => m.Cell != target.Cell), Is.True, "a bandit on her tile");
            Assert.That(bandits.Select(m => m.Cell).Distinct().Count(), Is.EqualTo(3), "two bandits on one tile");
        }

        /// <summary>
        /// A save taken while the attackers are still closing resumes on the same hash, and the same
        /// 600 ticks on: a claim is read off the saved destination, cell and job, and nothing new is
        /// saved. The control is that the save really was taken mid-approach.
        /// </summary>
        [Test]
        public void ASaveTakenWhileTheyCloseResumesTheSame()
        {
            var (colony, target, attackers, _) = Fight(5, i => (-8, i - 2));
            colony.World.Tick(40);
            Assert.That(attackers.Count(a => a.Destination >= 0), Is.GreaterThan(1), "the control: nobody was still walking");

            byte[] saved = colony.Save();
            var restored = Board(colonists: 6);
            restored.Load(saved);
            restored.Pawns.MeleeRules = new Whiffs();
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));

            colony.World.Tick(600);
            restored.World.Tick(600);
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value),
                "the fight resumed differently");
            var back = attackers.Select(a => restored.Pawns.Pawns.Get(a.Id)!).ToList();
            Assert.That(back.Select(a => a.Cell), Is.EqualTo(attackers.Select(a => a.Cell)));
            Assert.That(back.Select(a => a.Cell).Distinct().Count(), Is.EqualTo(5));
        }
    }
}
