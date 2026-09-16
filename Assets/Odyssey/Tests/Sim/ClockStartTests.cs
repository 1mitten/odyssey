#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The colony's first day does not have to begin at midnight.
    ///
    /// <para>Written because it did, and the board is lit for midday whatever the clock says: a
    /// player saw noon and heard the night ambience, and both halves were behaving correctly.
    /// The composition root now starts the clock at noon.</para>
    ///
    /// <para>The guard matters more than the feature. <see cref="SimWorld.CurrentTick"/> is in
    /// the state hash and seeds the per-tick random stream, so moving it after the world has run
    /// would be moving the world under everything that has already happened in it. The first
    /// version of the change sat after the composition root's setup tick, and this guard is what
    /// turned that into eight loud failures rather than a day that quietly started an hour
    /// late.</para>
    /// </summary>
    public class ClockStartTests
    {
        static SimWorld World() =>
            new SimWorldBuilder().WithSize(new GridSize(8, 8, 4)).WithSeed(11u).Build();

        [Test]
        public void AColonyCanBeginItsDayAtNoon()
        {
            SimWorld world = World();
            Assume.That(world.CurrentTick, Is.Zero, "a fresh world is at midnight");

            world.StartAtTick(12 * 2_500);

            Assert.That(world.CurrentTick, Is.EqualTo(30_000));

            world.Tick();
            Assert.That(world.CurrentTick, Is.EqualTo(30_001), "and runs on from there");
        }

        [Test]
        public void TheClockCannotBeMovedOnceTheWorldHasRun()
        {
            SimWorld world = World();
            world.Tick();

            Assert.Throws<InvalidOperationException>(() => world.StartAtTick(30_000),
                "the tick is in the hash and seeds the random stream; moving it later moves the " +
                "world under what has already happened in it");
        }

        [Test]
        public void AClockDoesNotStartBeforeZero()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => World().StartAtTick(-1));
        }

        [Test]
        public void StartingLateDoesNotDisturbDeterminism()
        {
            // The same seed and the same starting hour give the same world: a start tick is part
            // of the state like any other, not a nudge applied outside it.
            SimWorld a = World();
            SimWorld b = World();
            a.StartAtTick(30_000);
            b.StartAtTick(30_000);

            for (int i = 0; i < 40; i++)
            {
                a.Tick();
                b.Tick();
            }

            Assert.That(b.ComputeStateHash(), Is.EqualTo(a.ComputeStateHash()));
        }
    }
}
