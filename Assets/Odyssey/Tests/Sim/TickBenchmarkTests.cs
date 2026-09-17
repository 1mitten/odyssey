#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Diagnostics;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The full tick, measured with the real pathfinder inside it (OQ-19).
    ///
    /// <para><b>What this replaces.</b> ADR 0005's margin table — the most important number in
    /// the benchmark, by its own description — comes from the D1 spike, which mirrored the tick
    /// rather than being it, and its post-hierarchical figure of ~0.88 ms per tick is an
    /// *estimate*: the 2.4× pathfinder improvement applied by hand to the spike's phase 3. The
    /// ADR says so and asks for this run in as many words: "This estimate should be replaced by a
    /// re-run of the full benchmark once the pathfinder is wired into the tick."</para>
    ///
    /// <para><b>Explicit, and a benchmark rather than a gate.</b> It takes seconds and it measures
    /// a machine as much as a program, so it must never decide whether a pull request lands. The
    /// numbers it prints go into the ADR by hand, with the machine and the runtime beside them,
    /// which is the only form in which a timing is worth keeping.</para>
    ///
    /// <para><b>The world is frozen here on purpose.</b> It is the same shape
    /// <c>PathingBenchmark.AStructuredWorldOfRoomsAndDoorways</c> uses — 11 × 11 rooms on a wall
    /// lattice, one doorway per segment, light rubble — and it is built here rather than shared
    /// with it. A benchmark's workload has to stay still to be comparable with its own past runs;
    /// the pathing sweep's world is free to change when that sweep needs it to. Sharing them would
    /// couple two sets of numbers that are read years apart.</para>
    /// </summary>
    public class TickBenchmarkTests
    {
        const int SizeX = 250, SizeZ = 250, SizeY = 40;
        const int Portals = 200;
        const int Pawns = 50;
        const int Ticks = 1_500;
        const int WarmUp = 200;

        /// <summary>
        /// A colony of fifty going about its business. This is what the tick costs when nobody is
        /// making the pathfinder work hard.
        /// </summary>
        [Test, Explicit, Category("Benchmark")]
        public void TheStructuredWorldWithFiftyPawns() => Measure("colony at rest", replanPressure: false);

        /// <summary>
        /// The same world and the same fifty pawns, with D1's replan rate laid over the top: one
        /// long-range path request per tick, drawn the way <c>WORKLOAD.md</c> draws them — ±40
        /// cells and ±3 layers from a pawn's own position — and served by the same
        /// <see cref="PathService"/> the pawns use, inside <see cref="MovementSystem"/>, inside
        /// the real tick.
        ///
        /// <para><b>Why both arms exist.</b> Measuring only the first would have replaced ADR
        /// 0005's estimate with a number 40× more optimistic, and the reason would have been a
        /// change of workload rather than a change of cost — the D1 spike replans constantly,
        /// while a colony mostly walks a path it already has. The ADR's margin table is built on
        /// the stress case, so the stress case is what a replacement figure has to be comparable
        /// with. The two arms bracket the truth: a real colony under load sits between them.</para>
        /// </summary>
        [Test, Explicit, Category("Benchmark")]
        public void TheSameWorldAtTheD1ReplanRate() => Measure("D1 replan rate", replanPressure: true);

        void Measure(string label, bool replanPressure)
        {
            var setup = Stopwatch.StartNew();
            Colony colony = BuildColony(seed: 12345u, replanPressure);
            setup.Stop();

            // Warm up first: the JIT compiles the think tree, the job drivers and the pathfinder
            // on their first pass, and the A-star scratch arrays are allocated once. Measuring
            // that would be measuring startup.
            colony.World.Tick(WarmUp);

            var trace = new PhaseTrace(Ticks);
            colony.World.PhaseSink = trace;

            // The allocation question the row asks, with the instrument the row settled on:
            // GC.GetTotalAllocatedBytes does not exist in this runtime, so heap growth is read
            // with GetTotalMemory(false) and the collection counters are carried beside it. No
            // collection over the window means the byte figure is the allocation figure; a
            // collection means allocation certainly happened whatever the bytes say.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long heapBefore = GC.GetTotalMemory(false);
            int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);

            int issuedBefore = colony.Pressure?.Issued ?? 0;

            var wall = Stopwatch.StartNew();
            colony.World.Tick(Ticks);
            wall.Stop();

            long heapAfter = GC.GetTotalMemory(false);
            int gc0After = GC.CollectionCount(0), gc1After = GC.CollectionCount(1), gc2After = GC.CollectionCount(2);
            colony.World.PhaseSink = null;

            var report = new StringBuilder();
            report.AppendLine($"--- {label}: {SizeX} x {SizeZ} x {SizeY}, {colony.Spawned} pawns, {Ticks} ticks ---");
            report.AppendLine($"setup {setup.ElapsedMilliseconds} ms (generate, nav rebuild, spawn); " +
                              $"warm-up {WarmUp} ticks discarded");
            report.AppendLine($"wall clock {wall.Elapsed.TotalMilliseconds:F1} ms for {Ticks} ticks " +
                              $"= {wall.Elapsed.TotalMilliseconds / Ticks:F3} ms per tick");
            report.AppendLine();
            report.AppendLine("phase             mean      p95      max    share");

            double meanTick = trace.MeanTickMs();
            foreach (TickSegment phase in Enum.GetValues(typeof(TickSegment)))
            {
                double mean = trace.MeanMs(phase);
                report.AppendLine($"{phase,-14} {mean,8:F3} {trace.P95Ms(phase),8:F3} {trace.MaxMs(phase),8:F3} " +
                                  $"{(meanTick <= 0 ? 0 : 100 * mean / meanTick),7:F1}%");
            }

            report.AppendLine($"{"TICK",-14} {meanTick,8:F3} {trace.P95TickMs(),8:F3}");
            report.AppendLine();

            long heapGrowth = heapAfter - heapBefore;
            report.AppendLine($"heap {heapBefore:N0} -> {heapAfter:N0} bytes, growth {heapGrowth:N0} " +
                              $"= {(double)heapGrowth / Ticks:F1} bytes per tick");
            report.AppendLine($"collections over the window: gen0 {gc0After - gc0}, gen1 {gc1After - gc1}, " +
                              $"gen2 {gc2After - gc2}" +
                              (gc0After == gc0 ? " — none, so the growth figure is the allocation figure"
                                               : " — a collection ran, so allocation exceeded the growth figure"));
            if (colony.Pressure != null)
            {
                int issued = colony.Pressure.Issued - issuedBefore;
                // Pending is what the queue still holds. Near zero means every request was served
                // inside the window rather than piling up unanswered, which is the difference
                // between measuring a pathfinder and measuring a list.
                report.AppendLine($"path requests issued over the window {issued} " +
                                  $"({(double)issued / Ticks:F2} per tick), left pending {colony.Paths.Pending}");
            }

            report.AppendLine();
            report.AppendLine(Margin(meanTick));

            TestContext.WriteLine(report.ToString());

            // The one assertion. Not a budget — a benchmark that fails on a busy machine teaches
            // people to ignore it — but a check that the run was real: every phase was timed on
            // every tick, and the pawns did something.
            foreach (TickSegment phase in Enum.GetValues(typeof(TickSegment)))
                Assert.That(trace.Count(phase), Is.EqualTo(Ticks), $"{phase} was not timed on every tick");
            Assert.That(colony.Spawned, Is.EqualTo(Pawns), "the benchmark did not get the pawns it asked for");
        }

        /// <summary>
        /// The two enumerations that describe the order of a tick must not come to disagree about
        /// it. <see cref="TickPhase"/> names the three phases a system may register in;
        /// <see cref="TickSegment"/> names all seven, for timing. Where they overlap the numbers
        /// are the same, and this is what stops someone renumbering one of them alone.
        /// </summary>
        [Test]
        public void TheTwoDescriptionsOfATickAgreeAboutItsOrder()
        {
            Assert.That((int)TickSegment.WorldSystems, Is.EqualTo((int)TickPhase.WorldSystems));
            Assert.That((int)TickSegment.Things, Is.EqualTo((int)TickPhase.Things));
            Assert.That((int)TickSegment.Pawns, Is.EqualTo((int)TickPhase.Pawns));

            // And every phase a system can register in is a segment that gets timed, so no
            // registered system can run in a stretch of the tick nothing is measuring.
            foreach (TickPhase phase in Enum.GetValues(typeof(TickPhase)))
                Assert.That(Enum.IsDefined(typeof(TickSegment), (int)phase), Is.True,
                    $"{phase} is a phase a system may join, but no segment times it");
        }

        /// <summary>
        /// The seam itself, in the fast tier, because the benchmark above never runs in CI and a
        /// hook nothing exercises is a hook that quietly stops working. Small world, few ticks.
        /// </summary>
        [Test]
        public void ThePhaseSinkTimesEveryPhaseOfEveryTick()
        {
            var size = new GridSize(16, 16, 3);
            var cells = new CellGrid(size);
            for (int i = 0; i < size.CellCount; i++) cells.Floor[i] = 1;

            var nav = new NavGraph(cells);
            nav.Rebuild();
            SimWorld world = Compose(cells, nav, seed: 7u);

            var trace = new PhaseTrace();
            world.PhaseSink = trace;
            world.Tick(10);
            world.PhaseSink = null;

            foreach (TickSegment phase in Enum.GetValues(typeof(TickSegment)))
                Assert.That(trace.Count(phase), Is.EqualTo(10), $"{phase} should be timed once per tick");

            // Detaching stops it: the next ten ticks add nothing.
            world.Tick(10);
            Assert.That(trace.Count(TickSegment.Pawns), Is.EqualTo(10), "a detached sink went on recording");

            // A phase that did nothing still costs a measurable slice of nothing rather than a
            // negative number, which is what a mis-ordered timestamp pair would produce.
            foreach (TickSegment phase in Enum.GetValues(typeof(TickSegment)))
                Assert.That(trace.MeanMs(phase), Is.GreaterThanOrEqualTo(0), $"{phase} timed negative");
        }

        /// <summary>
        /// Attaching a sink must not change what the world does. The hash is the same question
        /// asked of <see cref="HashTrace"/>, and it has the same answer for the same reason: a
        /// sink is handed numbers and touches nothing.
        /// </summary>
        [Test]
        public void TimingATickCannotChangeIt()
        {
            var size = new GridSize(16, 16, 3);

            ulong Run(bool timed)
            {
                var cells = new CellGrid(size);
                for (int i = 0; i < size.CellCount; i++) cells.Floor[i] = 1;
                var nav = new NavGraph(cells);
                nav.Rebuild();

                SimWorld world = Compose(cells, nav, seed: 7u);
                if (timed) world.PhaseSink = new PhaseTrace();
                world.Tick(50);
                return world.ComputeStateHash().Value;
            }

            Assert.That(Run(timed: true), Is.EqualTo(Run(timed: false)),
                "a world that was being timed ended up in a different state from one that was not");
        }

        // ---------------------------------------------------------------- the workload

        readonly struct Colony
        {
            public readonly SimWorld World;
            public readonly int Spawned;
            public readonly ReplanPressure? Pressure;
            public readonly PathService Paths;

            public Colony(SimWorld world, int spawned, ReplanPressure? pressure, PathService paths)
            {
                World = world;
                Spawned = spawned;
                Pressure = pressure;
                Paths = paths;
            }
        }

        static Colony BuildColony(uint seed, bool replanPressure)
        {
            uint s = seed;
            var size = new GridSize(SizeX, SizeZ, SizeY);
            var cells = new CellGrid(size);
            for (int i = 0; i < size.CellCount; i++) cells.Floor[i] = 1;

            // 11 x 11 rooms on a wall lattice, one doorway per wall segment placed by a hash of
            // the segment, and light rubble inside the rooms.
            for (int y = 0; y < SizeY; y++)
            for (int z = 0; z < SizeZ; z++)
            for (int x = 0; x < SizeX; x++)
            {
                int idx = size.Index(x, z, y);
                bool wall = x % 12 == 0 || z % 12 == 0;
                if (wall)
                {
                    int seg = x % 12 == 0 ? Mix(x, z / 12, y) : Mix(x / 12, z, y);
                    int offset = 1 + seg % 11;
                    bool doorway = (x % 12 == 0 && z % 12 == offset)
                                   || (z % 12 == 0 && x % 12 == offset);
                    if (!doorway) cells.Flags[idx] |= CellFlags.SolidTerrain;
                }
                else if (Next(ref s) % 100 < 8)
                {
                    cells.Flags[idx] |= CellFlags.SolidTerrain;
                }
            }

            var nav = new NavGraph(cells);

            // Stairs, so the board is one place rather than forty. Same count and the same
            // acceptance rule the pathing sweep uses.
            int accepted = 0;
            while (accepted < Portals)
            {
                int x = (int)(Next(ref s) % SizeX);
                int z = (int)(Next(ref s) % SizeZ);
                int y = (int)(Next(ref s) % (SizeY - 1));
                int a = size.Index(x, z, y);
                int b = a + size.LayerStride;
                if ((cells.Flags[a] & CellFlags.SolidTerrain) != 0) continue;
                if ((cells.Flags[b] & CellFlags.SolidTerrain) != 0) continue;
                nav.AddConnector(ConnectorKind.Stair, new[] { a }, new[] { b });
                accepted++;
            }

            nav.MarkAllDirty();
            nav.Rebuild();

            SimWorld world = Compose(cells, nav, seed, out PawnContext pawns);

            int spawned = 0;
            int cellCount = size.CellCount;
            for (int p = 0; p < Pawns; p++)
            {
                int idx;
                int guard = 0;
                do
                {
                    idx = (int)(Next(ref s) % cellCount);
                    if (++guard > 10_000) throw new InvalidOperationException("nowhere to stand");
                } while ((cells.Flags[idx] & CellFlags.SolidTerrain) != 0);

                pawns.Pawns.Spawn(idx);
                spawned++;
            }

            ReplanPressure? pressure = null;
            if (replanPressure)
            {
                // Registered as a thing, so it runs in phase 3 and its requests are waiting when
                // MovementSystem serves the queue in phase 4. The cost therefore lands on Pawns,
                // where a pawn's own replan would land, rather than in a phase of its own.
                pressure = new ReplanPressure(cells, pawns, s);
                world.Register(pressure);
            }

            return new Colony(world, spawned, pressure, pawns.Paths);
        }

        /// <summary>
        /// D1's request stream, issued inside the tick: one path per tick for the pawn whose turn
        /// it is, to a cell ±40 across and ±3 layers away. It asks for paths and does nothing with
        /// the answers, which is the point — the question is what the search costs the tick, not
        /// what a pawn would do with it.
        /// </summary>
        sealed class ReplanPressure : ITickable
        {
            readonly CellGrid _cells;
            readonly PawnContext _pawns;
            uint _s;

            public ReplanPressure(CellGrid cells, PawnContext pawns, uint seed)
            {
                _cells = cells;
                _pawns = pawns;
                _s = seed == 0 ? 1u : seed;
            }

            public int Issued { get; private set; }

            public TickGroup TickGroup => TickGroup.Normal;
            public int TickPhaseOffset => 0;

            public void Tick(SimWorld world)
            {
                IReadOnlyList<Pawn> all = _pawns.Pawns.All;
                if (all.Count == 0) return;

                Pawn pawn = all[world.CurrentTick % all.Count];
                GridSize size = _cells.Size;
                CellRef here = size.FromIndex(pawn.Cell);

                for (int attempt = 0; attempt < 50; attempt++)
                {
                    int dx = (int)(Next(ref _s) % 81) - 40;
                    int dz = (int)(Next(ref _s) % 81) - 40;
                    int dy = (int)(Next(ref _s) % 7) - 3;
                    int goal = size.Index(Clamp(here.X + dx, size.SizeX), Clamp(here.Z + dz, size.SizeZ),
                        Clamp(here.Y + dy, size.SizeY));

                    if ((_cells.Flags[goal] & CellFlags.SolidTerrain) != 0 || goal == pawn.Cell) continue;

                    _pawns.Paths.Enqueue(new PathRequest(pawn.Id.Value, pawn.Cell, goal, TraverseMode.Colonist));
                    Issued++;
                    return;
                }
            }

            static int Clamp(int v, int max) => v < 0 ? 0 : v >= max ? max - 1 : v;
        }

        static SimWorld Compose(CellGrid cells, NavGraph nav, uint seed) => Compose(cells, nav, seed, out _);

        static SimWorld Compose(CellGrid cells, NavGraph nav, uint seed, out PawnContext pawns)
        {
            pawns = new PawnContext(cells, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns());
            var solver = new SupportSolver(cells);
            var support = new SupportSystem(cells, solver, chunks: null);
            var edifices = new List<PlacedEdifice>();
            var designations = new DesignationGrid(cells, edifices);

            return new SimWorldBuilder()
                .WithSeed(seed)
                .WithSize(cells.Size)
                .AddColony(pawns, designations, support, nav, edifices, out _)
                .Build();
        }

        /// <summary>
        /// The ADR's margin question, restated against whatever was just measured: three ticks
        /// must fit one 16.6 ms frame alongside rendering, and the target machine is not this one.
        /// </summary>
        static string Margin(double meanTickMs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("margin against the target hardware (three ticks per 16.6 ms frame)");
            sb.AppendLine("                 per tick   three ticks   frame left for rendering");
            foreach ((string name, double factor) in new[]
                     { ("measured", 1.0), ("discounted 3x", 3.0), ("discounted 4x", 4.0) })
            {
                double tick = meanTickMs * factor;
                double three = tick * 3;
                sb.AppendLine($"  {name,-14} {tick,8:F2} ms {three,10:F2} ms {16.6 - three,12:F2} ms");
            }
            return sb.ToString();
        }

        // The pathing sweep's PRNG and hash, so the world is the one it describes.
        static uint Next(ref uint s)
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return s;
        }

        static int Mix(int a, int b, int c)
        {
            unchecked
            {
                int h = a * 73856093 ^ b * 19349663 ^ c * 83492791;
                return h < 0 ? -h : h;
            }
        }
    }
}
