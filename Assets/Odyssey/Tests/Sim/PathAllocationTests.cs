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

        static double BytesPerTick(int ticks, bool withColony)
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
            }
            else
            {
                world = new SimWorldBuilder().WithSeed(7u).WithSize(size).Build();
            }

            world.Tick(200);

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
