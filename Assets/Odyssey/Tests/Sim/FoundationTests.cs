#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Sim
{
    /// <summary>A tickable that only counts, so tick scheduling can be asserted exactly.</summary>
    sealed class CountingTickable : ITickable, IStateHashable
    {
        public CountingTickable(TickGroup group, int phaseOffset)
        {
            TickGroup = group;
            TickPhaseOffset = phaseOffset;
        }

        public TickGroup TickGroup { get; }
        public int TickPhaseOffset { get; }
        public int Ticks { get; private set; }
        public int LastTick { get; private set; } = -1;

        public void Tick(SimWorld world)
        {
            Ticks++;
            LastTick = world.CurrentTick;
        }

        public void ContributeTo(ref StateHash hash) => hash.Add(Ticks);
    }

    public class GridSizeTests
    {
        [Test]
        public void IndexMatchesTheDocumentedFormula()
        {
            // docs/design/02-world-and-layers.md fixes this as index = (y * SizeZ + z) * SizeX + x.
            var size = GridSize.ScaleTarget;
            Assert.That(size.Index(1, 2, 3), Is.EqualTo((3 * 250 + 2) * 250 + 1));
            Assert.That(size.CellCount, Is.EqualTo(2_500_000));
            Assert.That(size.LayerStride, Is.EqualTo(62_500));
        }

        [Test]
        public void IndexRoundTripsForEveryCell()
        {
            var size = new GridSize(7, 5, 3);
            var seen = new bool[size.CellCount];

            for (int y = 0; y < size.SizeY; y++)
            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
            {
                int index = size.Index(x, z, y);
                Assert.That(index, Is.InRange(0, size.CellCount - 1), $"index out of range for ({x},{z},{y})");
                Assert.That(seen[index], Is.False, $"index {index} produced twice");
                seen[index] = true;

                var cell = size.FromIndex(index);
                Assert.That(cell, Is.EqualTo(new CellRef(x, z, y)));
            }

            Assert.That(seen.All(v => v), Is.True, "some indices were never produced");
        }

        [Test]
        public void OneLayerIsContiguous()
        {
            // The whole point of the index convention: a layer is a single span, because almost
            // every hot loop works within one layer.
            var size = GridSize.ScaleTarget;
            int first = size.Index(0, 0, 7);
            int last = size.Index(size.SizeX - 1, size.SizeZ - 1, 7);
            Assert.That(last - first, Is.EqualTo(size.LayerStride - 1));
        }

        [Test]
        public void ContainsRejectsOutOfBounds()
        {
            var size = new GridSize(4, 4, 2);
            Assert.That(size.Contains(0, 0, 0), Is.True);
            Assert.That(size.Contains(3, 3, 1), Is.True);
            Assert.That(size.Contains(-1, 0, 0), Is.False);
            Assert.That(size.Contains(0, 0, 2), Is.False);
            Assert.That(size.Contains(4, 0, 0), Is.False);
        }
    }

    public class TickLoopTests
    {
        static SimWorld WorldWith(params ITickable[] tickables)
        {
            var builder = new SimWorldBuilder().WithSeed(42).WithSize(new GridSize(8, 8, 2));
            foreach (var t in tickables) builder.AddTickable(_ => t);
            return builder.Build();
        }

        [Test]
        public void NormalGroupTicksEveryTick()
        {
            var t = new CountingTickable(TickGroup.Normal, 0);
            var world = WorldWith(t);
            world.Tick(100);
            Assert.That(t.Ticks, Is.EqualTo(100));
        }

        [Test]
        public void NeverGroupNeverTicks()
        {
            var t = new CountingTickable(TickGroup.Never, 0);
            var world = WorldWith(t);
            world.Tick(5000);
            Assert.That(t.Ticks, Is.Zero);
        }

        [Test]
        public void RareGroupTicksExactlyOncePerInterval()
        {
            var t = new CountingTickable(TickGroup.Rare, 0);
            var world = WorldWith(t);
            world.Tick(250);
            Assert.That(t.Ticks, Is.EqualTo(1));

            world.Tick(2250);
            Assert.That(t.Ticks, Is.EqualTo(10), "ten intervals should have elapsed in 2500 ticks");
        }

        [Test]
        public void LongGroupTicksExactlyOncePerInterval()
        {
            var t = new CountingTickable(TickGroup.Long, 0);
            var world = WorldWith(t);
            world.Tick(2000);
            Assert.That(t.Ticks, Is.EqualTo(1));
        }

        [Test]
        public void PhaseOffsetSpreadsThePopulationEvenly()
        {
            // This is the property that makes the cadence model affordable: 250 rare tickers
            // cost one tick of work per tick, not 250 every 250th tick.
            const int interval = (int)TickGroup.Rare;
            var members = Enumerable.Range(0, interval)
                .Select(i => new CountingTickable(TickGroup.Rare, i))
                .ToArray();
            var world = WorldWith(members.Cast<ITickable>().ToArray());

            for (int tick = 0; tick < interval; tick++)
            {
                int before = members.Sum(m => m.Ticks);
                world.Tick();
                int after = members.Sum(m => m.Ticks);
                Assert.That(after - before, Is.EqualTo(1), $"exactly one member should tick on tick {tick}");
            }

            Assert.That(members.All(m => m.Ticks == 1), Is.True,
                "every member should have ticked exactly once across one full interval");
        }

        [Test]
        public void NegativePhaseOffsetIsHandled()
        {
            var t = new CountingTickable(TickGroup.Rare, -3);
            var world = WorldWith(t);
            world.Tick(250);
            Assert.That(t.Ticks, Is.EqualTo(1));
        }

        [Test]
        public void DeferredWorkRunsInTheSameTickAndOnlyOnce()
        {
            var world = WorldWith();
            int runs = 0;
            world.Defer(_ => runs++);
            world.Tick();
            Assert.That(runs, Is.EqualTo(1));
            world.Tick();
            Assert.That(runs, Is.EqualTo(1), "deferred work must not run again on later ticks");
        }

        [Test]
        public void WorkDeferredFromDeferredWorkRunsOnTheNextTick()
        {
            // Cascading collapse relies on this: a collapse may cause another, and the second
            // must be a separate, observable step rather than unbounded recursion inside one.
            var world = WorldWith();
            int outer = 0, inner = 0;
            world.Defer(w =>
            {
                outer++;
                w.Defer(_ => inner++);
            });

            world.Tick();
            Assert.That(outer, Is.EqualTo(1));
            Assert.That(inner, Is.Zero, "nested deferral should not run within the same tick");

            world.Tick();
            Assert.That(inner, Is.EqualTo(1));
        }

        [Test]
        public void CurrentTickAdvancesByExactlyOne()
        {
            var world = WorldWith();
            Assert.That(world.CurrentTick, Is.Zero);
            world.Tick();
            Assert.That(world.CurrentTick, Is.EqualTo(1));
            world.Tick(9);
            Assert.That(world.CurrentTick, Is.EqualTo(10));
        }
    }

    public class DeterminismTests
    {
        static SimWorld Build(uint seed) =>
            new SimWorldBuilder()
                .WithSeed(seed)
                .WithSize(new GridSize(16, 16, 4))
                .AddTickable(_ => new CountingTickable(TickGroup.Normal, 0))
                .AddTickable(_ => new CountingTickable(TickGroup.Rare, 7))
                .AddTickable(_ => new CountingTickable(TickGroup.Long, 13))
                .Build();

        [Test]
        public void SameSeedProducesTheSameHash()
        {
            var a = Build(12345);
            var b = Build(12345);
            a.Tick(10_000);
            b.Tick(10_000);
            Assert.That(a.ComputeStateHash().Value, Is.EqualTo(b.ComputeStateHash().Value));
        }

        [Test]
        public void DifferentSeedsProduceDifferentHashes()
        {
            var a = Build(1);
            var b = Build(2);
            a.Tick(100);
            b.Tick(100);
            Assert.That(a.ComputeStateHash().Value, Is.Not.EqualTo(b.ComputeStateHash().Value));
        }

        [Test]
        public void TheHashMovesAsTheWorldTicks()
        {
            var world = Build(99);
            ulong before = world.ComputeStateHash().Value;
            world.Tick(500);
            Assert.That(world.ComputeStateHash().Value, Is.Not.EqualTo(before));
        }

        [Test]
        public void HashingIsPureAndRepeatable()
        {
            var world = Build(7);
            world.Tick(123);
            Assert.That(world.ComputeStateHash().Value, Is.EqualTo(world.ComputeStateHash().Value));
        }

        [Test]
        public void PerTickStreamsAreReproducibleAndDifferPerTick()
        {
            var first = DeterministicRandom.ForTick(555, 10);
            var again = DeterministicRandom.ForTick(555, 10);
            var next = DeterministicRandom.ForTick(555, 11);

            Assert.That(first.NextUInt(), Is.EqualTo(again.NextUInt()));
            Assert.That(DeterministicRandom.ForTick(555, 10).NextUInt(),
                Is.Not.EqualTo(next.NextUInt()),
                "adjacent ticks must not produce correlated streams");
        }

        [Test]
        public void PurposeSeparatesStreams()
        {
            // Adding a new consumer of randomness must not shift the draws an existing one sees.
            var pathing = DeterministicRandom.ForTick(77, 4, purpose: 1);
            var weather = DeterministicRandom.ForTick(77, 4, purpose: 2);
            Assert.That(pathing.NextUInt(), Is.Not.EqualTo(weather.NextUInt()));
        }

        [Test]
        public void StateHashFoldsIntegersStably()
        {
            var a = StateHash.New();
            a.Add(1);
            a.Add(2);

            var b = StateHash.New();
            b.Add(1);
            b.Add(2);

            var c = StateHash.New();
            c.Add(2);
            c.Add(1);

            Assert.That(a.Value, Is.EqualTo(b.Value));
            Assert.That(a.Value, Is.Not.EqualTo(c.Value), "order must matter, or reordering would hide a desync");
            Assert.That(a.ToString().Length, Is.EqualTo(16));
        }
    }

    public class AssemblyBoundaryTests
    {
        [Test]
        public void SimAssembliesDoNotReferenceUnity()
        {
            // The rule from ADR 0005 and docs/design/01-architecture.md, enforced rather than
            // trusted: the simulation must stay runnable and testable with no Unity at all.
            // asmdef noEngineReferences already makes this a compile error; this test is the
            // guard against someone quietly turning that flag off.
            foreach (var assembly in new[] { typeof(SimWorld).Assembly, typeof(GridSize).Assembly })
            {
                var offenders = assembly.GetReferencedAssemblies()
                    .Select(a => a.Name ?? string.Empty)
                    .Where(n => n.StartsWith("UnityEngine") || n.StartsWith("UnityEditor"))
                    .ToArray();

                Assert.That(offenders, Is.Empty,
                    $"{assembly.GetName().Name} references {string.Join(", ", offenders)}");
            }
        }

        [Test]
        public void SimDoesNotDependOnSyntyContent()
        {
            // A clone without the licensed packs must still build and pass every Sim test.
            // Nothing in Sim may name an asset path.
            var assembly = typeof(SimWorld).Assembly;
            Assert.That(assembly.GetReferencedAssemblies().Select(a => a.Name),
                Has.No.Member("Assembly-CSharp"));
        }
    }
}
