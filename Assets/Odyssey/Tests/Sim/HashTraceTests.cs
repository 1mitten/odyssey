#nullable enable
using System;
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Diagnostics;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A tickable whose contribution to the state hash changes from a chosen tick onward, so a
    /// test can make two same-seed worlds part company at an exact, known tick.
    /// </summary>
    sealed class PerturbableTickable : ITickable, IStateHashable
    {
        readonly int _perturbAtTick;
        int _counter;

        public PerturbableTickable(int perturbAtTick = int.MaxValue) => _perturbAtTick = perturbAtTick;

        public TickGroup TickGroup => TickGroup.Normal;
        public int TickPhaseOffset => 0;

        public void Tick(SimWorld world)
        {
            _counter++;
            if (world.CurrentTick >= _perturbAtTick) _counter++;
        }

        public void ContributeTo(ref StateHash hash) => hash.Add(_counter);
    }

    public class HashTraceTests
    {
        static SimWorld Build(uint seed, int perturbAtTick = int.MaxValue) =>
            new SimWorldBuilder()
                .WithSeed(seed)
                .WithSize(new GridSize(8, 8, 2))
                .AddTickable(_ => new PerturbableTickable(perturbAtTick))
                .Build();

        [Test]
        public void SinkIsOffByDefault()
        {
            var world = Build(seed: 1);
            Assert.That(world.HashSink, Is.Null);
            Assert.DoesNotThrow(() => world.Tick(10));
        }

        [Test]
        public void SameSeedTracesAreIdenticalOverFiveThousandTicks()
        {
            var traceA = new HashTrace();
            var traceB = new HashTrace();
            var a = Build(seed: 11);
            var b = Build(seed: 11);
            a.HashSink = traceA.Record;
            b.HashSink = traceB.Record;

            a.Tick(5000);
            b.Tick(5000);

            Assert.That(traceA.Hashes, Is.EqualTo(traceB.Hashes));
            Assert.That(HashTrace.FirstDivergence(traceA, traceB), Is.Null);
        }

        [Test]
        public void FirstDivergenceFindsTheExactTickTwoTracesPart()
        {
            const int perturbAtTick = 37;
            var traceA = new HashTrace();
            var traceB = new HashTrace();
            var a = Build(seed: 5);
            var b = Build(seed: 5, perturbAtTick);
            a.HashSink = traceA.Record;
            b.HashSink = traceB.Record;

            a.Tick(100);
            b.Tick(100);

            Assert.That(HashTrace.FirstDivergence(traceA, traceB), Is.EqualTo(perturbAtTick));
        }

        [Test]
        public void RecordingOutOfOrderThrows()
        {
            var trace = new HashTrace();
            trace.Record(0, StateHash.New());
            Assert.Throws<ArgumentOutOfRangeException>(() => trace.Record(2, StateHash.New()));
        }

        [Test]
        public void PerTickComputeStateHashCostIsPrinted()
        {
            var world = Build(seed: 3);
            var trace = new HashTrace();
            world.HashSink = trace.Record;

            const int ticks = 5000;
            var watch = Stopwatch.StartNew();
            world.Tick(ticks);
            watch.Stop();

            double microsPerTick = watch.Elapsed.TotalMilliseconds * 1000.0 / ticks;
            TestContext.WriteLine(
                $"HashTrace: {ticks} ticks, {watch.ElapsedMilliseconds} ms total, " +
                $"{microsPerTick:F2} us/tick (ComputeStateHash + Record)");

            Assert.That(trace.Hashes.Count, Is.EqualTo(ticks));
        }
    }
}
