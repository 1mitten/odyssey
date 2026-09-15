#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Sim
{
    /// <summary>Publishes a pawn and a slice, so the seam can be exercised end to end.</summary>
    sealed class FakeContributor : ISnapshotContributor
    {
        public CellRef PawnCell = new CellRef(1, 2, 0);
        public int Contributions { get; private set; }

        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            Contributions++;
            writer.AddPawn(new PawnView(new PawnId(1), PawnCell, food: 900, rest: 800, mood: 50));
            writer.AddThing(new ThingView(new ThingId(7), new CellRef(3, 3, 0), defIndex: 2, stuffIndex: 1));

            var slice = writer.BeginSlice(world.Size.LayerStride);
            for (int i = 0; i < slice.Length; i++) slice[i] = (byte)(i & 0xFF);
        }
    }

    public class SeamTests
    {
        static SimWorld Build(FakeContributor? contributor = null)
        {
            var builder = new SimWorldBuilder().WithSeed(5).WithSize(new GridSize(8, 8, 4));
            if (contributor != null) builder.AddSnapshotContributor(contributor);
            return builder.Build();
        }

        [Test]
        public void PublishesOncePerTick()
        {
            var c = new FakeContributor();
            var world = Build(c);
            world.Tick(10);
            Assert.That(c.Contributions, Is.EqualTo(10));
            Assert.That(world.Views.PublishCount, Is.EqualTo(10));
        }

        [Test]
        public void SnapshotCarriesWhatWasWritten()
        {
            var c = new FakeContributor();
            var world = Build(c);
            world.Tick();

            var snap = world.Views.Current;
            Assert.That(snap.PawnCount, Is.EqualTo(1));
            Assert.That(snap.ThingCount, Is.EqualTo(1));
            Assert.That(snap.SliceCellCount, Is.EqualTo(world.Size.LayerStride));
            Assert.That(snap.Pawns[0].Cell, Is.EqualTo(new CellRef(1, 2, 0)));
            Assert.That(snap.Things[0].Id, Is.EqualTo(new ThingId(7)));
        }

        [Test]
        public void TheSnapshotIsDoubleBufferedSoAReaderNeverSeesAHalfWrittenFrame()
        {
            // Publishing must swap between two buffers, never rewrite the one being read.
            var c = new FakeContributor();
            var world = Build(c);
            world.Tick();
            var first = world.Views.Current;
            world.Tick();
            var second = world.Views.Current;
            world.Tick();
            var third = world.Views.Current;

            Assert.That(second, Is.Not.SameAs(first), "consecutive publishes must alternate buffers");
            Assert.That(third, Is.SameAs(first), "and alternate back, so exactly two buffers are in play");
        }

        [Test]
        public void SnapshotTickMatchesTheTickThatProducedIt()
        {
            var world = Build(new FakeContributor());
            world.Tick(3);
            Assert.That(world.Views.Current.Tick, Is.EqualTo(2),
                "the snapshot is published before the counter advances, so it describes the tick just simulated");
        }

        [Test]
        public void ADeadPawnSimplyDisappearsFromTheSnapshot()
        {
            // The case the whole handle design exists for: a panel holding an id must find
            // nothing rather than dereference freed state.
            var world = Build(new FakeContributor());
            world.Tick();
            Assert.That(world.Views.Current.TryGetPawn(new PawnId(1), out _), Is.True);
            Assert.That(world.Views.Current.TryGetPawn(new PawnId(999), out var missing), Is.False);
            Assert.That(missing, Is.EqualTo(default(PawnView)));
        }

        [Test]
        public void RepublishingDoesNotGrowBuffersOnceWarm()
        {
            // Steady-state publishing must not allocate. Capacity grows and is then reused.
            var world = Build(new FakeContributor());
            world.Tick(4);
            long before = System.GC.GetTotalMemory(true);
            world.Tick(200);
            long after = System.GC.GetTotalMemory(true);
            Assert.That(after - before, Is.LessThan(64 * 1024),
                "publishing should reuse pooled buffers rather than allocating per tick");
        }

        [Test]
        public void IntentsAreAppliedAtTheStartOfATickNotOnSubmission()
        {
            var world = Build();
            world.Intents.Submit(new Intent(IntentKind.SetSliceLayer, a: 2));
            Assert.That(world.Views.SliceLayer, Is.Zero, "submission alone must not mutate the world");

            world.Tick();
            Assert.That(world.Views.SliceLayer, Is.EqualTo(2));
        }

        [Test]
        public void IntentsAreAppliedInSubmissionOrder()
        {
            var world = Build();
            world.Intents.Submit(new Intent(IntentKind.SetSliceLayer, a: 1));
            world.Intents.Submit(new Intent(IntentKind.SetSliceLayer, a: 3));
            world.Tick();
            Assert.That(world.Views.SliceLayer, Is.EqualTo(3), "the later intent must win");
        }

        [Test]
        public void AnOutOfBoundsIntentIsRejectedWithAReason()
        {
            var world = Build();
            world.Intents.Submit(new Intent(IntentKind.SetSliceLayer, a: 99));
            world.Tick();

            Assert.That(world.Views.SliceLayer, Is.Zero);
            Assert.That(world.Intents.Rejected.Count, Is.EqualTo(1));
            Assert.That(world.Intents.Rejected[0].Reason, Is.EqualTo(IntentRejection.OutOfBounds));
        }

        [Test]
        public void AnUnimplementedIntentIsRejectedRatherThanIgnored()
        {
            // A command that silently does nothing is the worst outcome for a player.
            var world = Build();
            world.Intents.Submit(new Intent(IntentKind.Designate, new CellRef(1, 1, 0)));
            world.Tick();
            Assert.That(world.Intents.Rejected.Single().Reason, Is.EqualTo(IntentRejection.UnknownIntent));
        }

        [Test]
        public void GameSpeedIsSetThroughAnIntent()
        {
            var world = Build();
            Assert.That(world.GameSpeed, Is.EqualTo(1));
            world.Intents.Submit(new Intent(IntentKind.SetGameSpeed, a: 0));
            world.Tick();
            Assert.That(world.GameSpeed, Is.Zero);
        }

        [Test]
        public void TheQueueIsBoundedAndSaysSo()
        {
            var bus = new IntentBus(capacity: 2);
            Assert.That(bus.Submit(new Intent(IntentKind.SetGameSpeed)), Is.True);
            Assert.That(bus.Submit(new Intent(IntentKind.SetGameSpeed)), Is.True);
            Assert.That(bus.Submit(new Intent(IntentKind.SetGameSpeed)), Is.False,
                "a runaway producer must not grow the queue without limit");
            Assert.That(bus.Rejected.Single().Reason, Is.EqualTo(IntentRejection.QueueFull));
        }

        [Test]
        public void DrainingEmptiesTheQueue()
        {
            var bus = new IntentBus();
            bus.Submit(new Intent(IntentKind.SetGameSpeed, a: 2));
            Assert.That(bus.PendingCount, Is.EqualTo(1));
            bus.Drain(_ => IntentRejection.None);
            Assert.That(bus.PendingCount, Is.Zero);
            Assert.That(bus.Rejected, Is.Empty);
        }

        [Test]
        public void ViewSettingsDoNotAffectTheStateHash()
        {
            // Slice layer and game speed are what the player is looking at, not world state.
            // If they entered the hash, two players at different camera layers would "desync".
            var a = Build();
            var b = Build();
            a.Intents.Submit(new Intent(IntentKind.SetSliceLayer, a: 3));
            a.Intents.Submit(new Intent(IntentKind.SetGameSpeed, a: 2));
            a.Tick(50);
            b.Tick(50);
            Assert.That(a.ComputeStateHash().Value, Is.EqualTo(b.ComputeStateHash().Value));
        }

        [Test]
        public void AnIntentCanBeHashedForReplay()
        {
            var hash = StateHash.New();
            new Intent(IntentKind.Designate, new CellRef(1, 2, 3), 4, 5).ContributeTo(ref hash);
            var same = StateHash.New();
            new Intent(IntentKind.Designate, new CellRef(1, 2, 3), 4, 5).ContributeTo(ref same);
            Assert.That(hash.Value, Is.EqualTo(same.Value));
        }
    }
}
