#nullable enable
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Diagnostics;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The divergence bisector (OQ-06). A determinism failure arrives as "the hashes differ after
    /// 60,000 ticks", which names no cause; the tick where two runs first disagree is what turns
    /// that into a question with an answer, because whatever diverged ran on that tick.
    ///
    /// These tests prove the instrument itself, which is the part that is easy to get subtly
    /// wrong: a bisector that reported the tick after the divergence would send every future
    /// investigation one tick past the code that caused it.
    /// </summary>
    public class HashTraceTests
    {
        /// <summary>
        /// A tickable that counts, and quietly miscounts once on a chosen tick. That is a
        /// divergence with a known cause on a known tick, which is the only way to prove a
        /// bisector reports the right one.
        /// </summary>
        sealed class DriftingTickable : ITickable, IStateHashable
        {
            readonly int _driftOn;

            public DriftingTickable(int driftOn = -1) => _driftOn = driftOn;

            public TickGroup TickGroup => TickGroup.Normal;
            public int TickPhaseOffset => 0;
            public int Counter { get; private set; }

            public void Tick(SimWorld world) => Counter += world.CurrentTick == _driftOn ? 2 : 1;

            public void ContributeTo(ref StateHash hash) => hash.Add(Counter);
        }

        static SimWorld Build(int driftOn = -1) =>
            new SimWorldBuilder()
                .WithSeed(4242)
                .WithSize(new GridSize(8, 8, 4))
                .AddTickable(_ => new DriftingTickable(driftOn))
                .Build();

        static HashTrace TraceOf(SimWorld world, int ticks)
        {
            var trace = new HashTrace();
            world.HashSink = trace;
            world.Tick(ticks);
            return trace;
        }

        [Test]
        public void NothingIsRecordedUntilASinkIsAttached()
        {
            // Off by default is the whole reason this can exist: it costs a full state hash per
            // tick, and an ordinary run must not pay for it.
            var world = Build();
            Assert.That(world.HashSink, Is.Null);

            world.Tick(100);

            var trace = new HashTrace();
            world.HashSink = trace;
            world.Tick(3);

            Assert.That(trace.Count, Is.EqualTo(3), "only ticks after attachment are recorded");
            Assert.That(trace.TickAt(0), Is.EqualTo(100));
        }

        [Test]
        public void AnEntryIsLabelledWithTheTickThatProducedIt()
        {
            var trace = TraceOf(Build(), 5);

            Assert.That(trace.Count, Is.EqualTo(5));
            for (int i = 0; i < trace.Count; i++)
                Assert.That(trace.TickAt(i), Is.EqualTo(i), "entry i is the state after tick i ran");
        }

        [Test]
        public void TwoSameSeedWorldsTraceIdenticallyOverFiveThousandTicks()
        {
            var a = TraceOf(Build(), 5_000);
            var b = TraceOf(Build(), 5_000);

            Assert.That(a.Count, Is.EqualTo(5_000));
            Assert.That(HashTrace.FirstDivergence(a, b), Is.EqualTo(HashTrace.NoDivergence));
            Assert.That(HashTrace.Agree(a, b), Is.True);
        }

        [Test]
        public void TheReportedTickIsTheOneThatDiverged()
        {
            // The perturbation happens during tick K, so K is the tick to re-run — not K+1, which
            // is merely the first tick that starts from a different world.
            foreach (int k in new[] { 0, 1, 137, 4_999 })
            {
                var clean = TraceOf(Build(), 5_000);
                var drifted = TraceOf(Build(k), 5_000);

                Assert.That(HashTrace.FirstDivergence(clean, drifted), Is.EqualTo(k),
                    $"a world that drifted on tick {k} was not reported as diverging there");
                Assert.That(HashTrace.FirstDivergence(drifted, clean), Is.EqualTo(k),
                    "the comparison is symmetric");
            }
        }

        [Test]
        public void TracesThatStartAtDifferentTicksStillLineUp()
        {
            // The realistic case: a long run is re-traced from a save, so one trace starts at
            // tick 0 and the other at tick 200. Only ticks both recorded can prove anything.
            var full = TraceOf(Build(), 400);

            var late = Build(300);
            late.Tick(200);
            var lateTrace = new HashTrace();
            late.HashSink = lateTrace;
            late.Tick(200);

            Assert.That(lateTrace.TickAt(0), Is.EqualTo(200));
            Assert.That(HashTrace.FirstDivergence(full, lateTrace), Is.EqualTo(300));
        }

        [Test]
        public void AHashCanBeLookedUpByTick()
        {
            var trace = TraceOf(Build(), 50);

            Assert.That(trace.TryHashForTick(17, out ulong at17), Is.True);
            Assert.That(at17, Is.EqualTo(trace.HashAt(17)));
            Assert.That(trace.TryHashForTick(50, out _), Is.False, "tick 50 has not run yet");
        }

        [Test]
        public void TheCostOfTracingIsMeasuredRatherThanAssumed()
        {
            // No assertion on the numbers: they belong to the machine, and the point of printing
            // them is that anyone switching tracing on during a soak knows what it costs.
            //
            // Both worlds are measured because the toy one on its own would mislead badly.
            // ComputeStateHash walks every registered hashable, so its cost is a property of the
            // world's contents, not of the trace: the 8x8x4 world with one tickable is the floor,
            // and the colony is the figure that matters.
            Measure("8x8x4, one tickable", 20_000, _ => Build());
            Measure("60x60x16 colony, 5 colonists", 2_000,
                _ => ColonyWorld.Build(new GridSize(60, 60, 16), 4242, colonists: 5).World);
        }

        static void Measure(string label, int ticks, System.Func<int, SimWorld> build)
        {
            build(0).Tick(200); // Warm the JIT so the first arm is not paying for both.

            var plain = build(0);
            var withoutWatch = Stopwatch.StartNew();
            plain.Tick(ticks);
            withoutWatch.Stop();

            var traced = build(0);
            traced.HashSink = new HashTrace(ticks);
            var withWatch = Stopwatch.StartNew();
            traced.Tick(ticks);
            withWatch.Stop();

            double without = withoutWatch.Elapsed.TotalMilliseconds / ticks;
            double with = withWatch.Elapsed.TotalMilliseconds / ticks;
            TestContext.WriteLine(
                $"{label}, {ticks} ticks: {without * 1000:F2} us/tick plain, {with * 1000:F2} us/tick traced " +
                $"— ComputeStateHash costs {(with - without) * 1000:F2} us/tick, " +
                $"{(without > 0 ? (with - without) / without * 100 : 0):F0}% on top.");

            Assert.That(traced.CurrentTick, Is.EqualTo(ticks));
        }
    }
}
