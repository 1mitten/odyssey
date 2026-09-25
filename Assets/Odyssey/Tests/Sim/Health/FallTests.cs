#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Falls (design 43 §7, a-02:91-99): fifteen times the layers to the one and a half, in two to
    /// four blunt hits on the bottom-facing regions, the worst a fracture from two layers — one
    /// layer bruises, three down, five kill. Through the one owner of damage, so a death is
    /// mourned. The step a miner takes into her own dig is not a fall.
    /// </summary>
    public class FallTests
    {
        const int Head = 0, ArmLeft = 2, ArmRight = 3;

        static (ColonyWorld colony, Pawn pawn) One()
        {
            var colony = Board(colonists: 2, beds: 0);
            colony.World.Tick(5);
            foreach (Pawn p in colony.Pawns.Pawns.All)
                for (int w = 0; w < p.WorkPriorities.Length; w++) p.WorkPriorities[w] = 0;
            return (colony, colony.Pawns.Pawns.All[0]);
        }

        static int Loss(Pawn pawn) => pawn.HpMaxMilli - pawn.HpMilli;

        [Test]
        public void OneLayerBruisesTheLegsAndTorsoAndWalksAway()
        {
            var (colony, pawn) = One();
            colony.Pawns.Combat!.Fall(pawn, 1, colony.World.CurrentTick);
            Assert.That(Loss(pawn), Is.InRange(12_000, 18_000));
            Assert.That(pawn.Downed, Is.False);
            PawnHealth health = pawn.Health!;
            Assert.That(health.Count, Is.GreaterThan(0));
            for (int i = 0; i < health.Count; i++)
            {
                Assert.That(health[i].Kind, Is.EqualTo(AfflictionKind.Bruise), "a one-layer fall broke something");
                Assert.That(health[i].Region, Is.Not.EqualTo(Head).And.Not.EqualTo(ArmLeft).And.Not.EqualTo(ArmRight),
                    "a fall landed on a region that does not face the ground");
            }
        }

        [Test]
        public void TwoLayersBreakSomething()
        {
            var (colony, pawn) = One();
            colony.Pawns.Combat!.Fall(pawn, 2, colony.World.CurrentTick);
            Assert.That(Loss(pawn), Is.InRange(33_000, 51_000), "two layers is about 42");
            bool fracture = false;
            for (int i = 0; i < pawn.Health!.Count; i++) fracture |= pawn.Health[i].Kind == AfflictionKind.Fracture;
            Assert.That(fracture, Is.True, "two layers and nothing broken");
        }

        [Test]
        public void ThreeLayersDown()
        {
            var (colony, pawn) = One();
            colony.Pawns.Combat!.Fall(pawn, 3, colony.World.CurrentTick);
            Assert.That(pawn.Downed, Is.True, "about 78 points, past pain shock, and standing");
            colony.World.Tick(60);
            Assert.That(colony.Pawns.Pawns.Get(pawn.Id), Is.SameAs(pawn), "three layers killed");
        }

        [Test]
        public void FiveLayersKillAndTheColonyMourns()
        {
            var (colony, pawn) = One();
            Pawn other = colony.Pawns.Pawns.All[1];
            colony.Pawns.Combat!.Fall(pawn, 5, colony.World.CurrentTick);
            colony.World.Tick();
            Assert.That(colony.Pawns.Pawns.Get(pawn.Id), Is.Null, "five layers and alive");
            Assert.That(colony.Pawns.Corpses.Count, Is.EqualTo(1));
            Assert.That(other.Memories.Exists(m => m.ThoughtIndex == ThoughtIndex.ColonistDied), Is.True,
                "a fatal fall went unmourned: it did not raise Died");
        }

        [Test]
        public void AnAnimalFallsOnItsPool()
        {
            var (colony, _) = One();
            Pawn rat = Spawn(colony, PawnKindIndex.DuctRat, Near(colony, 3, 3));
            colony.Pawns.Combat!.Fall(rat, 1, colony.World.CurrentTick);
            Assert.That(rat.Health, Is.Null);
            Assert.That(Loss(rat), Is.InRange(1_800, 2_700), "fifteen points scaled by a rat's fifteen against a hundred");
        }

        [Test]
        public void TheSameFallTwiceIsNotTheSameInjuries()
        {
            var (c1, p1) = One();
            var (c2, p2) = One();
            c1.Pawns.Combat!.Fall(p1, 2, 100);
            c2.Pawns.Combat!.Fall(p2, 2, 101);
            bool differ = Loss(p1) != Loss(p2) || p1.Health!.Count != p2.Health!.Count;
            for (int i = 0; !differ && i < p1.Health!.Count; i++)
                differ = p1.Health[i].Region != p2.Health![i].Region || p1.Health[i].SeverityMilli != p2.Health[i].SeverityMilli;
            Assert.That(differ, Is.True, "two falls a tick apart landed identically");
        }

        /// <summary>
        /// The spread moves points between a fall's hits and never its total (review, 2026-09-25):
        /// spread independently, a five-layer fall's 168 ranged 134 to 201 and now and then left
        /// somebody alive past the design's line. Two layers on many ticks, every one the same total.
        /// </summary>
        [Test]
        public void AFallsHitsAlwaysSumToItsTotal()
        {
            var (colony, pawn) = One();
            int total = pawn.Body!.FallDamageMilli(2);
            for (int tick = 1_000; tick < 1_060; tick++)
            {
                MakeWhole(pawn);
                colony.Pawns.Combat!.Fall(pawn, 2, tick);
                Assert.That(Loss(pawn), Is.EqualTo(total), $"tick {tick}: the hits did not sum to fifteen times two to the one and a half");
                Assert.That(pawn.Health!.TotalSeverityMilli, Is.EqualTo(Loss(pawn)));
            }
        }

        /// <summary>
        /// A fall kills inside the deferred phase — a collapse, a dig, a deconstruction — and the
        /// death it raises must land in the same tick (design 43 §15e). Deferred to the next, she
        /// stood, thought and walked for a tick past the death line, and a save in between wrote
        /// her out alive with her removal lost.
        /// </summary>
        [Test]
        public void AFatalFallInsideTheDeferredPhaseIsGoneTheSameTick()
        {
            var (colony, pawn) = One();
            colony.World.Defer(_ => colony.Pawns.Combat!.Fall(pawn, 5, colony.World.CurrentTick));
            colony.World.Tick();
            Assert.That(colony.Pawns.Pawns.Get(pawn.Id), Is.Null, "a fall's death waited for the next tick");
            Assert.That(colony.Pawns.Corpses.Count, Is.EqualTo(1));

            // The control: anything else deferred from inside the phase still belongs to the next tick.
            bool ran = false;
            colony.World.Defer(w => w.Defer(_ => ran = true));
            colony.World.Tick();
            Assert.That(ran, Is.False, "Defer's own rule moved: work deferred from the deferred phase ran this tick");
            colony.World.Tick();
            Assert.That(ran, Is.True);
        }
    }
}
