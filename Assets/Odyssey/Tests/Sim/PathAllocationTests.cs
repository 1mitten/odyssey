#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Where the tick's allocation comes from, measured rather than reasoned about (the open
    /// question OQ-19 left behind).
    ///
    /// <para><b>The loose end.</b> `TickBenchmarkTests` found the real tick growing the heap by
    /// 76.7 bytes at rest and 284.6 under replan pressure, with no collection of any generation
    /// across either window — so those are the allocation figures and not lower bounds. The delta
    /// divided to roughly 208 bytes per served path request. The ADR addendum said that *pointed
    /// at* the served path's cell array and that nobody had measured it. This measures it.</para>
    ///
    /// <para><b>The experiment is a bisection, not an inspection.</b> If the cost is the array of
    /// cells copied out for each successful path, allocation per request rises with the length of
    /// the path. If it is something charged once per request — a boxed struct, a list growth, a
    /// closure — it stays flat while the path gets longer. One number distinguishes them and
    /// neither requires an opinion about the code.</para>
    /// </summary>
    public class PathAllocationTests
    {
        /// <summary>
        /// Can this runtime's <see cref="GC.GetTotalMemory(bool)"/> actually see a small
        /// allocation, or does it report in chunks too coarse to attribute one?
        ///
        /// <para><b>Why the instrument gets calibrated before it is believed.</b> These tests
        /// passed on CoreCLR in the fast tier and failed under Mono in the Unity tier, on a
        /// difference in the *measuring device* rather than in the thing measured: Mono's
        /// collector hands out nursery space in blocks, so the reported total can sit still
        /// through thousands of small allocations and then jump. A per-request figure taken from
        /// that is not a smaller number, it is a meaningless one — and, worse, an under-reporting
        /// runtime would make the budget assertion below <i>pass</i> for the wrong reason.</para>
        ///
        /// <para>So: allocate a known quantity, and if the runtime cannot report it to within a
        /// factor of two, ignore the test rather than fail it or, worse, trust it. The measurement
        /// still runs everywhere the instrument works, which is where the figures in ADR 0005 came
        /// from.</para>
        /// </summary>
        static bool AccountingIsFineGrained()
        {
            // The same shape and roughly the same total as what the tests below measure — a few
            // thousand short-lived arrays of a couple of hundred bytes, allocated and dropped.
            // **Dropped, not kept**, deliberately: the allocations these tests care about are
            // discarded every tick, and a runtime that reuses that space rather than growing its
            // heap is precisely the case being detected. Retaining them would make the probe
            // easier to satisfy than the thing it stands in for, which would be worse than having
            // no probe.
            const int Count = 2_000;
            const int Length = 64;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetTotalMemory(false);
            int gen0 = GC.CollectionCount(0);

            for (int i = 0; i < Count; i++)
            {
                var tmp = new int[Length];
                GC.KeepAlive(tmp);
            }

            long reported = GC.GetTotalMemory(false) - before;
            if (GC.CollectionCount(0) != gen0) return false;

            // An int[64] is 256 bytes of payload plus a header; 24 is the usual 64-bit figure and
            // the exact value does not matter at a factor-of-two tolerance.
            long expected = (long)Count * (Length * sizeof(int) + 24);
            return reported > expected / 2 && reported < expected * 2;
        }

        /// <summary>
        /// A flat open board with a floor under every cell, so a path is limited only by the
        /// distance asked for and never by geometry.
        /// </summary>
        static (CellGrid grid, NavGraph nav) OpenBoard(int side)
        {
            var size = new GridSize(side, side, 2);
            var grid = new CellGrid(size);
            for (int i = 0; i < size.CellCount; i++) grid.Floor[i] = 1;

            var nav = new NavGraph(grid);
            nav.Rebuild();
            return (grid, nav);
        }

        /// <summary>
        /// Bytes of heap growth per served request, averaged over <paramref name="requests"/> of
        /// a path spanning <paramref name="span"/> cells. Returns -1 if a collection ran, because
        /// the figure would then be a lower bound and saying so is better than reporting it.
        /// </summary>
        static (double bytes, int cells) PerRequest(int side, int span, int requests)
        {
            (CellGrid grid, NavGraph nav) = OpenBoard(side);
            var service = new PathService(new PathFinder(nav));
            GridSize size = grid.Size;

            int start = size.Index(1, 1, 0);
            int goal = size.Index(1 + span, 1, 0);

            // Warm up: the finder's scratch arrays, the queue's backing store and the served list
            // are all allocated once on first use, and counting them would be counting startup.
            for (int i = 0; i < 64; i++)
            {
                service.Enqueue(new PathRequest(0, start, goal, TraverseMode.Colonist));
                service.Serve();
            }

            int cells = 0;
            foreach (ServedPath p in service.Served) cells = p.Cells.Length;
            Assert.That(cells, Is.GreaterThan(0), $"the fixture is wrong: no path of span {span}");

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetTotalMemory(false);
            int gen0 = GC.CollectionCount(0);

            for (int i = 0; i < requests; i++)
            {
                service.Enqueue(new PathRequest(0, start, goal, TraverseMode.Colonist));
                service.Serve();
            }

            long after = GC.GetTotalMemory(false);
            if (GC.CollectionCount(0) != gen0) return (-1, cells);

            return ((double)(after - before) / requests, cells);
        }

        [Test, Category("Long")]
        public void TheAllocationPerPathRequestIsMeasuredAndAttributed()
        {
            const int Requests = 2_000;

            if (!AccountingIsFineGrained())
                Assert.Ignore("this runtime reports heap growth too coarsely to attribute a " +
                              "per-request figure; the attribution in ADR 0005 was measured where " +
                              "it can be");

            (double shortBytes, int shortCells) = PerRequest(side: 64, span: 4, Requests);
            (double longBytes, int longCells) = PerRequest(side: 64, span: 40, Requests);

            TestContext.WriteLine(
                $"short path: {shortCells} cells, {shortBytes:F1} bytes per request\n" +
                $"long path:  {longCells} cells, {longBytes:F1} bytes per request");

            Assert.That(shortBytes, Is.GreaterThanOrEqualTo(0),
                "a collection ran during the short-path window; the figure would be a lower bound");
            Assert.That(longBytes, Is.GreaterThanOrEqualTo(0),
                "a collection ran during the long-path window; the figure would be a lower bound");

            double extraCells = longCells - shortCells;
            double extraBytes = longBytes - shortBytes;
            TestContext.WriteLine(
                $"{extraCells} more cells cost {extraBytes:F1} more bytes " +
                $"= {(extraCells <= 0 ? 0 : extraBytes / extraCells):F2} bytes per extra cell " +
                "(an int is 4)");

            // The attribution. A path's cells are copied into a fresh array for each successful
            // request, so the cost must rise with the length of the path at about four bytes a
            // cell. If this ever fails low, something started pooling the array and this test
            // should be replaced by one asserting that it did.
            Assert.That(extraBytes, Is.GreaterThan(extraCells * 3),
                "allocation did not rise with path length, so the served cell array is not where " +
                "the tick's bytes go — the ADR's attribution is wrong and wants re-opening");
        }

        /// <summary>
        /// A tick that nothing asked anything of must allocate next to nothing, and this is what
        /// keeps it that way.
        ///
        /// <para><b>What this found.</b> OQ-19 measured 76.7 bytes a tick at rest — unconditional,
        /// paid by every tick of every game whether or not anything happened, about 4.6 MB over a
        /// 60,000-tick day — and did not attribute it. Bracketing did: an <i>empty</i> world with
        /// no systems, no pawns and no contributors allocated 67.4 bytes a tick, and adding a
        /// whole colony added <b>nothing</b>. A cost that scales with neither pawns nor systems nor
        /// contributors cannot be any of them; it had to be the tick machinery itself.</para>
        ///
        /// <para>It was <c>Intents.Drain(HandleIntent)</c>. <c>Drain</c> takes a delegate, and a
        /// method group converts to a fresh one on every call — 64 bytes a tick, for a handler
        /// that never changes. Holding it in a field took the empty world to <b>1.6 bytes a
        /// tick</b> and the colony to 3.3.</para>
        ///
        /// <para><b>The threshold is deliberately loose.</b> It is not here to pin 1.6; it is here
        /// so a 64-byte-per-tick delegate cannot come back unnoticed, which is exactly how this
        /// arrived. A figure this small is also the one most easily lost in noise, so the test
        /// reports rather than asserts when a collection ran.</para>
        /// </summary>
        [Test, Category("Long")]
        public void ATickThatDoesNothingAllocatesNextToNothing()
        {
            const int Ticks = 5_000;

            // Generous against a measured 1.6 and 3.3, and still four times under the 64-byte
            // delegate this test exists to keep out.
            const double Budget = 16.0;

            // Without this the test would pass most loudly on exactly the runtimes that cannot
            // see the allocation it exists to forbid.
            if (!AccountingIsFineGrained())
                Assert.Ignore("this runtime reports heap growth too coarsely for a per-tick budget");

            double empty = BytesPerTick(Ticks, withColony: false);
            double colony = BytesPerTick(Ticks, withColony: true);

            TestContext.WriteLine(
                $"empty world:  {empty:F1} bytes per tick\n" +
                $"with colony:  {colony:F1} bytes per tick\n" +
                $"the colony adds {colony - empty:F1}");

            Assert.That(empty, Is.GreaterThanOrEqualTo(0), "a collection ran in the empty window");
            Assert.That(colony, Is.GreaterThanOrEqualTo(0), "a collection ran in the colony window");

            Assert.That(empty, Is.LessThan(Budget),
                "an empty world's tick started allocating again. Something in SimWorld.Tick is " +
                "building an object per tick — a method group converted to a delegate is how this " +
                "happened the first time.");
            Assert.That(colony, Is.LessThan(Budget),
                "a colony's idle tick started allocating. Look for a per-tick delegate, closure, " +
                "boxed struct or list growth in a system's Tick.");
        }

        /// <summary>
        /// A tick that is answering a cell question allocates nothing either — measured, not
        /// assumed, because the answered publish runs on <i>every</i> tick for as long as a tile
        /// is selected, which is most of a session someone is inspecting in.
        ///
        /// <para><b>What would fail this.</b> The row is a blittable struct written into a pooled
        /// array, so the publish adds no heap traffic. The day someone makes
        /// <c>CellDetail</c> a class, or the contributor builds a string or a boxed enum per
        /// publish, this is the test that says so — and at 60 ticks a second that is 60 objects a
        /// second that the GC would then own for exactly one frame each.</para>
        /// </summary>
        [Test, Category("Long")]
        public void AStandingQuestionCostsTheTickNothing()
        {
            const int Ticks = 5_000;
            const double Budget = 16.0;

            if (!AccountingIsFineGrained())
                Assert.Ignore("this runtime reports heap growth too coarsely for a per-tick budget");

            double plain = BytesPerTick(Ticks, withColony: true);
            double asked = BytesPerTick(Ticks, withColony: true, standingQuestion: true);

            TestContext.WriteLine(
                $"colony:            {plain:F1} bytes per tick\n" +
                $"colony, asked:     {asked:F1} bytes per tick\n" +
                $"the standing question adds {asked - plain:F1}");

            Assert.That(plain, Is.GreaterThanOrEqualTo(0), "a collection ran in the plain window");
            Assert.That(asked, Is.GreaterThanOrEqualTo(0), "a collection ran in the asked window");
            Assert.That(asked, Is.LessThan(Budget),
                "answering a cell question started allocating. The publish phase is allocation-free " +
                "in steady state; look for a per-publish string, box or list growth in " +
                "CellDetailContributor.");
        }

        static double BytesPerTick(int ticks, bool withColony, bool standingQuestion = false)
        {
            var size = new GridSize(32, 32, 3);
            var grid = new CellGrid(size);
            for (int i = 0; i < size.CellCount; i++) grid.Floor[i] = 1;

            var nav = new NavGraph(grid);
            nav.Rebuild();

            SimWorld world;
            if (withColony)
            {
                var pawns = new PawnContext(
                    grid, nav, new PathService(new PathFinder(nav)),
                    Odyssey.Sim.Defs.ContentPack.Pawns());
                var solver = new SupportSolver(grid);
                var support = new SupportSystem(grid, solver, chunks: null);
                var edifices = new System.Collections.Generic.List<Odyssey.Sim.Worldgen.PlacedEdifice>();
                var designations = new Odyssey.Sim.Designations.DesignationGrid(grid, edifices);

                world = new SimWorldBuilder()
                    .WithSeed(7u).WithSize(size)
                    .AddColony(pawns, designations, support, nav, edifices, out _)
                    .Build();

                for (int p = 0; p < 5; p++) pawns.Pawns.Spawn(size.Index(4 + p, 4, 1));

                if (standingQuestion)
                {
                    // Submitted before the warm-up, so the intent drains on the first warm-up tick
                    // and the question stands for the whole measured window. The row is asserted
                    // below so the measurement cannot silently measure a question nobody asked.
                    world.Intents.Submit(new Intent(IntentKind.QueryCell, new CellRef(4, 4, 1)));
                }
            }
            else
            {
                world = new SimWorldBuilder().WithSeed(7u).WithSize(size).Build();
            }

            world.Tick(200);

            if (standingQuestion)
                Assert.That(world.Views.Current.CellDetailCount, Is.EqualTo(1),
                    "the fixture is wrong: the question never stood, so the window measured nothing");

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetTotalMemory(false);
            int gen0 = GC.CollectionCount(0);

            world.Tick(ticks);

            long after = GC.GetTotalMemory(false);
            if (GC.CollectionCount(0) != gen0) return -1;

            return (double)(after - before) / ticks;
        }
    }
}
