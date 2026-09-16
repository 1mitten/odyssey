#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The play scene's own world, run headless for a long stretch, as the standing gate asks.
    ///
    /// The milestone gate wants a full day — 60,000 ticks — with zero errors; that is the batch
    /// runner's job (<c>scripts/unity.sh exec Odyssey.EditorTools.OneDay.Run</c>) because it
    /// takes the better part of a minute. This is the same run cut to a sixth, so that the fast
    /// tier catches a regression on every save rather than once a night. Two things have to
    /// hold: nothing throws across ten thousand ticks of five colonists hauling, eating and
    /// sleeping, and two runs from the same seed end in the same state hash — the determinism
    /// the whole architecture is built on, checked over a real workload rather than an empty
    /// world.
    /// </summary>
    public class HeadlessRunTests
    {
        const int Ticks = 10_000;
        static readonly GridSize PlaySize = new GridSize(120, 120, 16);

        [Test]
        public void ASixthOfADayRunsWithoutThrowing()
        {
            ColonyWorld colony = ColonyWorld.Build(PlaySize, seed: 1u, ScenarioDef.Bare());
            Assert.That(colony.Placement.Colonists, Is.EqualTo(5), colony.Placement.ToString());

            Assert.DoesNotThrow(() => colony.World.Tick(Ticks));

            Assert.That(colony.World.CurrentTick, Is.EqualTo(Ticks));

            // The snapshot is published inside the tick, before the counter advances, so it
            // carries the number of the tick that built it: one behind. That is the convention,
            // not a lost tick, and this test learned it the hard way.
            var snapshot = colony.World.Views.Current;
            Assert.That(snapshot.Tick, Is.EqualTo(Ticks - 1));
            Assert.That(snapshot.PawnCount, Is.EqualTo(5),
                "a colonist vanished from the published snapshot during the run");
        }

        [Test]
        public void TheSameSeedGivesTheSameWorldAfterASixthOfADay()
        {
            ColonyWorld first = ColonyWorld.Build(PlaySize, seed: 7u, ScenarioDef.Bare());
            ColonyWorld second = ColonyWorld.Build(PlaySize, seed: 7u, ScenarioDef.Bare());

            first.World.Tick(Ticks);
            second.World.Tick(Ticks);

            StateHash a = first.World.ComputeStateHash();
            StateHash b = second.World.ComputeStateHash();
            Assert.That(a, Is.EqualTo(b), "two runs from one seed diverged — something is non-deterministic");
        }

        [Test]
        public void DifferentSeedsGiveDifferentWorlds()
        {
            // Guards the test above against passing vacuously: a hash that ignored the world
            // would make any two runs "agree".
            ColonyWorld first = ColonyWorld.Build(PlaySize, seed: 7u, ScenarioDef.Bare());
            ColonyWorld second = ColonyWorld.Build(PlaySize, seed: 8u, ScenarioDef.Bare());
            first.World.Tick(600);
            second.World.Tick(600);

            Assert.That(first.World.ComputeStateHash(), Is.Not.EqualTo(second.World.ComputeStateHash()));
        }
    }
}
