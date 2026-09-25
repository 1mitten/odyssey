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
        /// <summary>The scale target from ADR 0002, and the board the three original arms
        /// measure. Every arm now takes its board, so a figure is only comparable with another
        /// taken on the same one — which is why the edit arms print all three in one run.</summary>
        static readonly GridSize Scale = GridSize.ScaleTarget;
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

        /// <summary>
        /// The colony at rest with a cell question standing — the inspect pane's steady state
        /// while a tile is selected, which on the scale target is a 2.5-million-cell world
        /// answering with one O(1) row. This arm exists so that claim is a measurement: the
        /// snapshot phase it reports is the same publish a click spends on
        /// <c>RepublishViews</c>, so it prices both the per-tick answer and the per-click
        /// republish at once.
        /// </summary>
        [Test, Explicit, Category("Benchmark")]
        public void TheColonyAnsweringACellQuestion() =>
            Measure("colony answering a cell question", replanPressure: false, standingQuestion: true);

        /// <summary>
        /// The same fifty colonists, on each board the menu offers, with one cell mined every
        /// tick. <b>This is the arm the board size is bought with.</b>
        ///
        /// <para>The three arms above hold the world still, and a still world is the one state in
        /// which the navigation rebuild costs nothing (see <see cref="MineOneCell"/>). Under edits
        /// it runs four passes over every region and link in the world for a change confined to
        /// one 10 x 10 block, so <b>it is the only cost this project has measured that grows with
        /// the board rather than with what is happening on it</b>. Making it local is HT1; this is
        /// the number HT1 has to move, and the number that says what a bigger board costs today.
        /// </para>
        ///
        /// <para><b>All three boards in one method on purpose.</b> This machine runs several
        /// editors at once and a figure taken in one run is only comparable with another taken in
        /// the same one — the city frame canary drifted 2.01 to 4.01 ms in an afternoon purely on
        /// what a sibling worktree was doing. Three separate arms would let a noisy minute be read
        /// as a board-size effect. Everything else about the workload is held identical, so the
        /// only thing that varies between the three reports is the board.</para>
        ///
        /// <para><b>Read these against the played board, not as it.</b> <see cref="BuildColony"/>
        /// lays an 11 x 11 room lattice with rubble scattered through it, which fragments every
        /// block: 19,606 regions at 120 x 120 x 16 against a generated map's 2,110, and 207,293 at
        /// the scale target against 24,141. The rebuild's cost tracks the region count almost
        /// exactly, so this arm reports roughly an order of magnitude more than the same edit
        /// costs in the game. That is the right stress world to hold a floor under — it is the
        /// shape a heavily built colony tends towards — but the figure a board size is bought
        /// with is <c>NavGraphStatisticsTests.EveryOfferedBoard</c>'s, taken on a board the
        /// generator made.</para>
        /// </summary>
        [Test, Explicit, Category("Benchmark")]
        public void TheEditTickOnEveryOfferedBoard()
        {
            Measure("edit tick, standard", replanPressure: false, board: BoardSizes.Standard, edits: true);
            Measure("edit tick, large", replanPressure: false, board: BoardSizes.Large, edits: true);
            Measure("edit tick, huge", replanPressure: false, board: BoardSizes.Huge, edits: true);
            Measure("edit tick, scale target", replanPressure: false, board: Scale, edits: true);
        }

        /// <summary>
        /// The same four boards with the world held still, so the edit arm above has a control on
        /// each of them. Without this the difference between two boards and the difference between
        /// resting and editing are the same number read twice.
        /// </summary>
        [Test, Explicit, Category("Benchmark")]
        public void TheRestingTickOnEveryOfferedBoard()
        {
            Measure("rest, standard", replanPressure: false, board: BoardSizes.Standard);
            Measure("rest, large", replanPressure: false, board: BoardSizes.Large);
            Measure("rest, huge", replanPressure: false, board: BoardSizes.Huge);
            Measure("rest, scale target", replanPressure: false, board: Scale);
        }

        void Measure(string label, bool replanPressure, bool standingQuestion = false,
                     GridSize? board = null, bool edits = false)
        {
            GridSize size = board ?? Scale;
            var setup = Stopwatch.StartNew();
            Colony colony = BuildColony(seed: 12345u, replanPressure, size, edits);
            setup.Stop();

            // A question submitted before the warm-up drains on the warm-up's first tick and
            // then stands for every tick of the measured window — answered sixty times a second,
            // the way it is while a player holds a tile selected.
            if (standingQuestion)
            {
                colony.World.Intents.Submit(new Intent(IntentKind.QueryCell, new CellRef(10, 10, 2)));
                Assert.That(colony.World.Intents.PendingCount, Is.EqualTo(1),
                    "the fixture is wrong: the question never queued");
            }

            // Warm up first: the JIT compiles the think tree, the job drivers and the pathfinder
            // on their first pass, and the A-star scratch arrays are allocated once. Measuring
            // that would be measuring startup.
            colony.World.Tick(WarmUp);

            if (standingQuestion)
                Assert.That(colony.World.Views.Current.CellDetailCount, Is.EqualTo(1),
                    "the fixture is wrong: the question was never answered, so the window " +
                    "measured an ordinary tick");

            var trace = new PhaseTrace(Ticks);
            colony.World.PhaseSink = trace;
            colony.Nav.ResetRebuildTimes();

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
            report.AppendLine($"--- {label}: {size.SizeX} x {size.SizeZ} x {size.SizeY} " +
                              $"({size.CellCount:N0} cells), {colony.Spawned} pawns, {Ticks} ticks ---");
            report.AppendLine($"lattice world: {colony.Nav.RegionCapacity:N0} regions — a generated " +
                              "board of this size carries roughly a ninth of that, so an edit here " +
                              "costs about an order of magnitude more than one in the game " +
                              "(NavGraphStatisticsTests.EveryOfferedBoard has the played figure)");
            if (standingQuestion)
                report.AppendLine("a cell question stood for every tick; the Snapshot line below is " +
                                  "the answered publish, and is also what one click's RepublishViews costs");
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
            if (colony.Miner != null)
                report.AppendLine($"cells mined over the window {colony.Miner.Mined} " +
                                  $"({(double)colony.Miner.Mined / (Ticks + WarmUp):F2} per tick)");
            if (colony.Pressure != null)
            {
                int issued = colony.Pressure.Issued - issuedBefore;
                // Pending is what the queue still holds. Near zero means every request was served
                // inside the window rather than piling up unanswered, which is the difference
                // between measuring a pathfinder and measuring a list.
                report.AppendLine($"path requests issued over the window {issued} " +
                                  $"({(double)issued / Ticks:F2} per tick), left pending {colony.Paths.Pending}");
            }

            if (colony.Nav.RebuildsTimed > 0)
            {
                // Where the nav rebuilds in the window went (HT1, design 05 §7a).
                var split = new List<string>();
                foreach (NavGraph.RebuildSegment segment in Enum.GetValues(typeof(NavGraph.RebuildSegment)))
                    split.Add($"{segment} {colony.Nav.RebuildTicks[(int)segment] * 1000.0 / Stopwatch.Frequency / colony.Nav.RebuildsTimed:F3}");
                report.AppendLine($"nav rebuilds {colony.Nav.RebuildsTimed}, per rebuild: {string.Join(", ", split)} ms");
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

            // The negative control for the edit arms. A fixture that quietly stopped finding solid
            // cells to clear would report a resting tick under an editing label, which is exactly
            // the failure the arm exists to rule out.
            if (edits)
                Assert.That(colony.Miner!.Mined, Is.EqualTo(Ticks + WarmUp),
                    "the edit arm did not edit on every tick, so it measured a world at rest");
        }

        /// <summary>
        /// Twenty colonists against twenty bandits on the played board (design 33 §6A, lane A):
        /// what a fight costs the tick, beside the same colony at peace in the same run. The
        /// combat pass walks every pawn with one branch each and does its real work only for the
        /// swings landing and the hurt healing, and the hunt and the self-defence each scan the
        /// pawns once per think — so the row that matters is the Pawns phase, fight against peace.
        ///
        /// <para>Explicit, like every arm here. The assertion is only that the fight was real:
        /// swings were resolved in the window.</para>
        /// </summary>
        [Test, Explicit, Category("Benchmark")]
        public void TwentyAgainstTwenty()
        {
            var report = new StringBuilder();
            double peace = MeasureFight(report, "twenty colonists at peace", bandits: 0, out _);
            double fight = MeasureFight(report, "twenty against twenty", bandits: 20, out int swings);
            report.AppendLine($"the fight costs {fight - peace:F3} ms a tick over the colony at peace ({fight / Math.Max(peace, 1e-9):F2}x)");
            // Every colonist drafted (design 33 §15): each one on her hold scans the pawns every
            // tick for a threat beside her or a fight to join, and joins the ones nearby.
            double drafted = MeasureFight(report, "twenty drafted against twenty", bandits: 20, out _, drafted: true);
            report.AppendLine($"drafted, the fight costs {drafted - peace:F3} ms a tick over the colony at peace");
            TestContext.WriteLine(report.ToString());
            Assert.That(swings, Is.GreaterThan(50), "the measured window held no fight");
        }

        /// <summary>
        /// Fifty colonists and ten bandits on the scale target and on the Huge board (C7, the
        /// combat gate's benchmark rows), each beside the same colony at peace and drafted, in one
        /// run. <b>Not the lattice world</b>: these colonies are built by <see cref="ColonyWorld.Build"/>
        /// on the map the game generates (barren and wooded, <see cref="ColonyWorld.DefFor"/>), so
        /// the region count is a played board's, and the figure is comparable with the game rather
        /// than with the rest of this class. The fight is the colony's, not the board's: the
        /// combat pass and the hunt both scale with the pawns, so the row that matters is the Pawns
        /// phase against peace on the same board.
        /// </summary>
        [Test, Explicit, Category("Benchmark")]
        public void FiftyAgainstTenOnTheBigBoards()
        {
            var report = new StringBuilder();
            int swings = 0;
            foreach ((string name, GridSize size) in new[] { ("scale target", Scale), ("huge", BoardSizes.Huge) })
            {
                double peace = MeasureFight(report, $"fifty at peace, {name}", 0, out _, size: size, colonists: 50, wooded: true);
                double fight = MeasureFight(report, $"fifty against ten, {name}", 10, out int here, size: size, colonists: 50, wooded: true);
                double drafted = MeasureFight(report, $"fifty drafted against ten, {name}", 10, out _, drafted: true, size: size, colonists: 50, wooded: true);
                report.AppendLine($"{name}: the fight costs {fight - peace:F3} ms a tick over peace ({fight / Math.Max(peace, 1e-9):F2}x), " +
                                  $"drafted {drafted - peace:F3} ms");
                report.AppendLine();
                swings = Math.Min(swings == 0 ? here : swings, here);
            }
            TestContext.WriteLine(report.ToString());
            Assert.That(swings, Is.GreaterThan(20), "a measured window held no fight");
        }

        static double MeasureFight(StringBuilder report, string label, int bandits, out int swings, bool drafted = false,
                                   GridSize? size = null, int colonists = 20, bool wooded = false)
        {
            GridSize board = size ?? new GridSize(120, 120, 16);
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = colonists;
            scenario.stockpileCells = 9;
            var setup = Stopwatch.StartNew();
            ColonyWorld colony = ColonyWorld.Build(board, 12345u, scenario, wooded: wooded);
            setup.Stop();
            var rules = new CombatFixture.RecordingRules();
            colony.Pawns.MeleeRules = rules;

            CellRef start = colony.Start;
            for (int i = 0; i < bandits; i++)
            {
                int cell = colony.Pawns.Cells.NearestWalkableInColumn(start.X + 12 + i % 5, start.Z - 2 + i / 5, start.Y);
                if (cell >= 0) colony.Pawns.Pawns.Spawn(cell, PawnKindIndex.Bandit);
            }
            if (drafted)
                foreach (Pawn pawn in new System.Collections.Generic.List<Pawn>(colony.Pawns.Pawns.All))
                    if (pawn.IsColonist) CombatFixture.Draft(colony, pawn);

            // Past the approach, into the thick of it, before the window opens.
            colony.World.Tick(600);
            int before = rules.Swings.Count;

            var trace = new PhaseTrace(Ticks);
            colony.World.PhaseSink = trace;
            var wall = Stopwatch.StartNew();
            colony.World.Tick(Ticks);
            wall.Stop();
            colony.World.PhaseSink = null;
            swings = rules.Swings.Count - before;

            int downed = 0, people = 0;
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            {
                if (pawn.Downed) downed++;
                if (pawn.IsColonist) people++;
            }
            double tick = trace.MeanTickMs();
            report.AppendLine($"--- {label}: {colony.Pawns.Pawns.Count} pawns ({people} colonists, the rest the board's animals and the bandits) on {board.SizeX} x {board.SizeZ} x {board.SizeY} " +
                              $"({(wooded ? "the played map" : "bare")}, {colony.Pawns.Nav.RegionCapacity:N0} regions), {Ticks} ticks, " +
                              $"setup {setup.ElapsedMilliseconds} ms ---");
            report.AppendLine($"tick {tick:F3} ms mean, {trace.P95TickMs():F3} p95; Pawns phase {trace.MeanMs(TickSegment.Pawns):F3} ms mean, " +
                              $"{trace.P95Ms(TickSegment.Pawns):F3} p95; {swings} swings resolved in the window, {downed} down at its end");
            return tick;
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

        /// <summary>
        /// Attribution for the edit arm: what one <c>NavGraph.Rebuild</c> costs on its own, with
        /// nothing else in the tick, and how that moves as the board is dug.
        ///
        /// <para><b>Temporary diagnostic.</b> The edit arm reports its whole WorldSystems phase,
        /// and four systems register there. This isolates the one that was expected to dominate,
        /// so a number can be attributed rather than assumed.</para>
        /// </summary>
        [Test, Explicit, Category("Benchmark")]
        public void WhatOneRebuildCosts()
        {
            foreach (GridSize size in new[] { BoardSizes.Standard, BoardSizes.Large, BoardSizes.Huge, Scale })
            {
                Colony colony = BuildColony(seed: 12345u, replanPressure: false, size, edits: false);
                NavGraph nav = colony.Nav;
                uint s = 99u;

                // A fresh board: mark one cell, rebuild, repeat. The dirty set is re-marked each
                // time, so every call does the work rather than returning on the first line.
                double first = TimeRebuilds(nav, colony, ref s, 200);
                int regionsAfterFirst = nav.RegionCapacity;

                // Then dig two thousand more and ask again, because the edit arm digs for 1,700
                // ticks and the question is whether the cost is a constant or a drift.
                double later = TimeRebuilds(nav, colony, ref s, 2_000);

                TestContext.WriteLine(
                    $"[Rebuild] {size}: {regionsAfterFirst} regions, one rebuild after one dirty cell " +
                    $"= {first:F3} ms; after 2,000 more edits ({nav.RegionCapacity} regions) = {later:F3} ms");
            }
        }

        static double TimeRebuilds(NavGraph nav, Colony colony, ref uint s, int count)
        {
            CellGrid cells = colony.Cells;
            var watch = new Stopwatch();
            int done = 0;
            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 64; attempt++)
                {
                    int idx = (int)(Next(ref s) % (uint)cells.Size.CellCount);
                    if ((cells.Flags[idx] & CellFlags.SolidTerrain) == 0) continue;
                    cells.Flags[idx] &= ~CellFlags.SolidTerrain;
                    nav.MarkDirty(idx);
                    break;
                }

                watch.Start();
                nav.Rebuild();
                watch.Stop();
                done++;
            }

            return watch.Elapsed.TotalMilliseconds / done;
        }

        // ---------------------------------------------------------------- the workload

        readonly struct Colony
        {
            public readonly SimWorld World;
            public readonly int Spawned;
            public readonly ReplanPressure? Pressure;
            public readonly PathService Paths;

            /// <summary>Null unless this arm edits the world; see <see cref="MineOneCell"/>.</summary>
            public readonly MineOneCell? Miner;

            public readonly CellGrid Cells;
            public readonly NavGraph Nav;

            public Colony(SimWorld world, int spawned, ReplanPressure? pressure, PathService paths,
                          MineOneCell? miner, CellGrid cells, NavGraph nav)
            {
                World = world;
                Spawned = spawned;
                Pressure = pressure;
                Paths = paths;
                Miner = miner;
                Cells = cells;
                Nav = nav;
            }
        }

        static Colony BuildColony(uint seed, bool replanPressure, GridSize size, bool edits)
        {
            uint s = seed;
            var cells = new CellGrid(size);
            for (int i = 0; i < size.CellCount; i++) cells.Floor[i] = 1;

            // 11 x 11 rooms on a wall lattice, one doorway per wall segment placed by a hash of
            // the segment, and light rubble inside the rooms.
            for (int y = 0; y < size.SizeY; y++)
            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
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
                int x = (int)(Next(ref s) % size.SizeX);
                int z = (int)(Next(ref s) % size.SizeZ);
                int y = (int)(Next(ref s) % (size.SizeY - 1));
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

            MineOneCell? miner = null;
            if (edits)
            {
                // Registered as a thing for the same reason ReplanPressure is: it runs in phase 3,
                // so the dirty block is waiting when NavigationSystem rebuilds in phase 2 of the
                // *next* tick. The cost therefore lands on WorldSystems, which is where a
                // colonist's own mined cell would land it.
                miner = new MineOneCell(cells, nav, s, pawns.Enclosure);
                world.Register(miner);
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

            return new Colony(world, spawned, pressure, pawns.Paths, miner, cells, nav);
        }

        /// <summary>
        /// One cell mined per tick, somewhere new each time.
        ///
        /// <para><b>This is the workload every benchmark in this file was missing.</b> The three
        /// original arms hold the world perfectly still, and a still world is the one state in
        /// which <c>NavGraph.Rebuild</c> does nothing at all — it returns on the first line when
        /// no block is dirty. A colony that is mining, felling and building edits the world on
        /// most ticks, so the number those arms report is the cost of the one thing a colony
        /// never does (<c>docs/audit/2026-09-19-baseline.md</c> section 1, which had to measure
        /// this outside the repository for want of this class).</para>
        ///
        /// <para><b>It walks the board rather than working one corner.</b> If the mined cells
        /// stayed inside one block, <c>CollectAffectedZones</c> would see the same zones every
        /// tick and the flood would warm into nothing, leaving only the four global passes — which
        /// would flatter the result in the wrong direction, by hiding the part that is already
        /// local behind the part that is not.</para>
        ///
        /// <para>It edits the grid directly rather than going through a designation and a
        /// colonist, because the question is what a <i>rebuild</i> costs, not what mining costs.
        /// Nothing here is saved or hashed and the class lives in the test assembly.</para>
        /// </summary>
        public sealed class MineOneCell : ITickable
        {
            readonly CellGrid _cells;
            readonly NavGraph _nav;
            readonly EnclosureGrid? _enclosure;
            uint _s;

            /// <param name="enclosure">The colony's enclosure, so the edit marks what a real
            /// mined cell marks. Until 2026-09-21 this arm marked nav alone, and the enclosure
            /// solve — the largest cost of a real edit on Huge — was never in the number
            /// (`docs/lessons.md`).</param>
            public MineOneCell(CellGrid cells, NavGraph nav, uint seed, EnclosureGrid? enclosure = null)
            {
                _cells = cells;
                _nav = nav;
                _enclosure = enclosure;
                _s = seed == 0 ? 1u : seed;
            }

            /// <summary>Cells actually cleared. The arm asserts this, because a fixture that
            /// stopped editing would report an idle tick as though it were a busy one.</summary>
            public int Mined { get; private set; }

            public TickGroup TickGroup => TickGroup.Normal;
            public int TickPhaseOffset => 0;

            public void Tick(SimWorld world)
            {
                GridSize size = _cells.Size;
                for (int attempt = 0; attempt < 64; attempt++)
                {
                    int idx = (int)(Next(ref _s) % (uint)size.CellCount);
                    if ((_cells.Flags[idx] & CellFlags.SolidTerrain) == 0) continue;

                    _cells.Flags[idx] &= ~CellFlags.SolidTerrain;
                    _nav.MarkDirty(idx);
                    _enclosure?.MarkDirty(idx);
                    Mined++;
                    return;
                }
            }
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
