#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The health bar's flicker (design 33 §8a), the first candidate: is the bar <i>owed</i> on
    /// some publishes and not others? The interface draws a bar exactly where the frame carries
    /// <c>odyssey.pawn.hp</c> beside a pool, so a flicker born in the simulation would show here as
    /// that pair appearing and vanishing between consecutive ticks while nothing about the pawn
    /// changed. A real brawl, read every tick.
    /// </summary>
    public class HealthBarPublishingTests
    {
        /// <summary>Whether the frame owes a bar over the pawn: hit points and a pool beside them.</summary>
        static bool Owed(WorldSnapshot frame, Pawn pawn) =>
            frame.TryGetPawnAspect(pawn.Id, CombatAspects.Hp, out _)
            && frame.TryGetPawnAspect(pawn.Id, CombatAspects.HpMax, out int max) && max > 0;

        /// <summary>What the pawn's own state says: hurt, downed or drafted.</summary>
        static bool Rule(Pawn pawn) => pawn.HpMilli < pawn.HpMaxMilli || pawn.Downed || pawn.Drafted;

        /// <summary>
        /// Two drafted colonists sent at two marauders, a third colonist left alone, fought for
        /// 3,000 ticks with the shipped rules. On every tick every living pawn's bar is owed
        /// exactly when its state says, and the bar only ever changes on a tick the state changed —
        /// so the bar does not blink on the publishing side. Measured 2026-09-23: see §8a.
        /// </summary>
        [Test]
        public void ABarIsOwedOnEveryTickItsStateSaysAndBlinksOnNone()
        {
            var colony = Board(colonists: 3);
            colony.World.Tick();
            var all = colony.Pawns.Pawns.All;
            Pawn a = all[0], b = all[1], idle = all[2];
            Stand(colony, a, Near(colony, 0, 0));
            Stand(colony, b, Near(colony, 1, 0));
            Stand(colony, idle, Near(colony, -6, -6));
            Pawn m1 = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 4, 0));
            Pawn m2 = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 4, 2));
            Assert.That(Draft(colony, a), Is.EqualTo(IntentRejection.None));
            Assert.That(Draft(colony, b), Is.EqualTo(IntentRejection.None));
            Assert.That(Attack(colony, a, m1), Is.EqualTo(IntentRejection.None));
            Assert.That(Attack(colony, b, m2), Is.EqualTo(IntentRejection.None));

            var pawns = new List<Pawn> { a, b, idle, m1, m2 };
            var lastOwed = new Dictionary<int, bool>();
            var lastRule = new Dictionary<int, bool>();
            var owedChanges = new Dictionary<int, int>();
            var ruleChanges = new Dictionary<int, int>();
            int ticksOwed = 0, hurtTicks = 0;

            for (int t = 0; t < 3_000; t++)
            {
                colony.World.Tick();
                WorldSnapshot frame = colony.World.Views.Current;
                foreach (Pawn p in pawns)
                {
                    if (!frame.TryGetPawn(p.Id, out _)) continue; // dead: a corpse, not a bar

                    bool owed = Owed(frame, p), rule = Rule(p);
                    Assert.That(owed, Is.EqualTo(rule),
                        $"tick {frame.Tick}: pawn {p.Id.Value} owed {owed}, state says {rule} (hp {p.HpMilli}/{p.HpMaxMilli})");
                    if (owed) ticksOwed++;
                    if (p.HpMilli < p.HpMaxMilli) hurtTicks++;

                    if (lastOwed.TryGetValue(p.Id.Value, out bool wasOwed) && wasOwed != owed)
                        owedChanges[p.Id.Value] = owedChanges.GetValueOrDefault(p.Id.Value) + 1;
                    if (lastRule.TryGetValue(p.Id.Value, out bool wasRule) && wasRule != rule)
                        ruleChanges[p.Id.Value] = ruleChanges.GetValueOrDefault(p.Id.Value) + 1;
                    lastOwed[p.Id.Value] = owed;
                    lastRule[p.Id.Value] = rule;
                }
            }

            TestContext.WriteLine($"ticks a bar was owed {ticksOwed}, pawn-ticks hurt {hurtTicks}");
            foreach (Pawn p in pawns)
                TestContext.WriteLine($"pawn {p.Id.Value}: bar changed {owedChanges.GetValueOrDefault(p.Id.Value)} times, state {ruleChanges.GetValueOrDefault(p.Id.Value)}");

            Assume.That(hurtTicks, Is.GreaterThan(0), "nobody was hurt: the brawl measured nothing");
            foreach (Pawn p in pawns)
            {
                Assert.That(owedChanges.GetValueOrDefault(p.Id.Value), Is.EqualTo(ruleChanges.GetValueOrDefault(p.Id.Value)),
                    $"pawn {p.Id.Value}'s bar changed on a tick its state did not");
                Assert.That(owedChanges.GetValueOrDefault(p.Id.Value), Is.LessThanOrEqualTo(2),
                    $"pawn {p.Id.Value}'s bar came and went {owedChanges.GetValueOrDefault(p.Id.Value)} times in one fight");
            }
        }
    }
}
