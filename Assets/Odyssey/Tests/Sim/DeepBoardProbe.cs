#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What a deeper board costs once unopened rock has no navigation region (design 62 §2c,
    /// §12; the deep-mining plan's DM1 and DM2 gates). The figures are in
    /// <c>docs/design/28-map-size.md</c> §12.
    ///
    /// <para><b>The control is in the same run.</b> <see cref="NavGrid.SolidTerrainHasRegions"/>
    /// restores the rule before DM1 on one graph instance, so every board is measured both ways,
    /// twice, interleaved — before, after, before, after — on identical grids (generation is
    /// deterministic and the edit arm's cell picks are seeded), and the repeat is the noise floor.
    /// A time taken on this machine compares only with a time taken beside it.</para>
    ///
    /// <para><b>The played map</b>, through <see cref="PlayedMap.Def"/>, which calls
    /// <c>ColonyWorld.DefFor</c> — never the unmodified default (<c>28-map-size.md</c> §2a). The edit
    /// arm is <c>NavGraphStatisticsTests</c>' and <c>BoardDepthTests</c>': the mean of 200 rebuilds,
    /// each after one random solid cell is cleared, the dirty mark re-made before each.</para>
    ///
    /// <para><c>[Explicit]</c>, as every probe here: it measures a machine as much as the code,
    /// asserts only that its controls took, and writes to <c>ODYSSEY_PROBE</c> (or the temp folder)
    /// because a runner's captured output is not reliably printed.</para>
    /// </summary>
    [Explicit("A measuring instrument. Run it by name: --filter FullyQualifiedName~DeepBoardProbe")]
    public class DeepBoardProbe
    {
        const uint Seed = 4242u;
        static readonly uint[] GenerationSeeds = { 1u, 2u, 3u, 4u, 5u };

        /// <summary>DM1: regions, links, the edit tick and memory at 16, 24 and 32 layers on
        /// Standard and Huge, with and without regions for solid ground.</summary>
        [Test]
        public void RockIsFreeAtDepth()
        {
            var report = new StringBuilder();
            report.AppendLine($"[DeepBoard] DM1 {Machine()}");
            report.AppendLine("[DeepBoard] board | rule | regions / links | full rebuild ms | edit tick ms | nav MiB | world MiB (B/cell)");
            // Standard and Huge at three depths, then the scale target (250 x 250 x 40), which is
            // where the audit's 1.15 ms edit tick came from.
            var sizes = new List<GridSize>();
            foreach (int side in new[] { 120, 240 })
            foreach (int layers in new[] { 16, 24, 32 })
                sizes.Add(new GridSize(side, side, layers));
            sizes.Add(GridSize.ScaleTarget);
            foreach (GridSize size in sizes)
            {
                int side = size.SizeX, layers = size.SizeY;
                double world = WorldMiB(size, out double perCell);
                for (int round = 0; round < 2; round++)
                {
                    foreach (bool before in new[] { true, false })
                    {
                        NavRow row = MeasureNav(size, before);
                        report.AppendLine($"[DeepBoard] {side}x{side}x{layers} | {(before ? "before (rock has regions)" : "after (rock has none)")} r{round + 1} | " +
                                          $"{row.Regions:N0} / {row.Links:N0} | {row.FullMs:F1} | {row.EditMs:F3} | {row.NavMiB:F2} | " +
                                          $"{world:F1} ({perCell:F1})");
                        if (before) Assert.That(row.SolidInRegions, Is.GreaterThan(0), "the control did not restore the old rule");
                        else Assert.That(row.SolidInRegions, Is.Zero, "the new rule left rock in a region");
                    }
                }
            }
            Write(report);
        }

        /// <summary>DM2: memory, generation and the navigation figures on all four boards at 32
        /// layers, beside today's depth for each.</summary>
        [Test]
        public void ThirtyTwoLayersOnEveryBoard()
        {
            var report = new StringBuilder();
            report.AppendLine($"[DeepBoard] DM2 {Machine()}");
            report.AppendLine("[DeepBoard] board | cells | generation median of 5 ms | regions / links | edit tick ms | world MiB (B/cell) | save KB | layer-change hint");
            var boards = new (string Name, int Side, int Today)[]
            {
                ("Small", 80, 16), ("Standard", 120, 16), ("Large", 180, 24), ("Huge", 240, 16),
            };
            foreach ((string name, int side, int today) in boards)
            foreach (int layers in today == 32 ? new[] { 32 } : new[] { today, 32 })
            {
                var size = new GridSize(side, side, layers);
                var times = new List<long>();
                foreach (uint seed in GenerationSeeds)
                {
                    var grid = new CellGrid(size);
                    var watch = Stopwatch.StartNew();
                    MapGenerator.Generate(grid, seed, PlayedMap.Def(size));
                    watch.Stop();
                    times.Add(watch.ElapsedMilliseconds);
                }
                NavRow row = MeasureNav(size, before: false);
                double world = WorldMiB(size, out double perCell);
                long save = SaveBytes(size);
                report.AppendLine($"[DeepBoard] {name} {side}x{side}x{layers} | {size.CellCount:N0} | {WorldgenTests.Median(times)} | " +
                                  $"{row.Regions:N0} / {row.Links:N0} | {row.EditMs:F3} | {world:F1} ({perCell:F1}) | {save / 1024.0:F0} | {row.LayerHint}");
            }
            Write(report);
        }

        struct NavRow
        {
            public int Regions, Links, SolidInRegions;
            public double FullMs, EditMs, NavMiB;
            public int LayerHint;
        }

        static NavRow MeasureNav(GridSize size, bool before)
        {
            var grid = new CellGrid(size);
            MapGenOutcome outcome = MapGenerator.Generate(grid, Seed, PlayedMap.Def(size));

            Settle();
            long heap = GC.GetTotalMemory(true);
            var nav = new NavGraph(grid);
            nav.Grid.SolidTerrainHasRegions = before;
            ConnectorRegistrar.Register(nav, grid, outcome.Connectors);
            var full = Stopwatch.StartNew();
            nav.Rebuild();
            full.Stop();
            Settle();
            long navBytes = GC.GetTotalMemory(true) - heap;

            var row = new NavRow { FullMs = full.Elapsed.TotalMilliseconds, NavMiB = navBytes / (1024.0 * 1024.0) };
            Count(nav, out row.Regions, out row.Links, out row.SolidInRegions);
            row.LayerHint = nav.EstimatedLayerChangeCost;
            row.EditMs = TimePerEdit(grid, nav, Seed);
            GC.KeepAlive(nav);
            return row;
        }

        /// <summary>The simulation's memory for the colony the game would build at this size — the
        /// rule after DM1, which is the only rule a colony is built with.</summary>
        static double WorldMiB(GridSize size, out double perCell)
        {
            Settle();
            long before = GC.GetTotalMemory(true);
            ColonyWorld colony = ColonyWorld.Build(size, Seed, ScenarioDef.Bare(), barren: true, wooded: true);
            Settle();
            long delta = GC.GetTotalMemory(true) - before;
            GC.KeepAlive(colony);
            perCell = (double)delta / size.CellCount;
            return delta / (1024.0 * 1024.0);
        }

        static long SaveBytes(GridSize size)
        {
            var grid = new CellGrid(size);
            MapGenerator.Generate(grid, Seed, PlayedMap.Def(size));
            var world = new SimWorldBuilder().WithSeed(1).WithSize(size).Build();
            using var stream = new MemoryStream();
            WorldSave.Save(world, stream, new ISaveable[] { new GridSaveSection(grid) });
            return stream.Length;
        }

        static void Count(NavGraph nav, out int regions, out int links, out int solidInRegions)
        {
            regions = 0;
            var seen = new HashSet<int>();
            for (int r = 0; r < nav.RegionCapacity; r++)
            {
                if (!nav.IsRegionAlive(r)) continue;
                regions++;
                int start = nav.AdjacencyStart(r);
                int count = nav.AdjacencyCount(r);
                for (int i = 0; i < count; i++) seen.Add(nav.AdjacencyLink(start + i));
            }
            links = seen.Count;

            solidInRegions = 0;
            NavFlags[] flags = nav.Grid.Flags;
            for (int c = 0; c < flags.Length; c++)
                if ((flags[c] & NavFlags.Solid) != 0 && (flags[c] & NavFlags.Blocked) == 0 && nav.RegionOfCell(c) >= 0)
                    solidInRegions++;
        }

        /// <summary>As <c>NavGraphStatisticsTests.TimePerEdit</c>, so the figures compare with §2 and §11.</summary>
        static double TimePerEdit(CellGrid grid, NavGraph nav, uint seed)
        {
            const int Samples = 200;
            uint s = seed == 0 ? 1u : seed;
            GridSize size = grid.Size;
            var watch = new Stopwatch();
            int taken = 0;
            for (int i = 0; i < Samples; i++)
            {
                bool dirtied = false;
                for (int attempt = 0; attempt < 128; attempt++)
                {
                    s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                    int idx = (int)(s % (uint)size.CellCount);
                    if ((grid.Flags[idx] & CellFlags.SolidTerrain) == 0) continue;
                    grid.Flags[idx] &= ~CellFlags.SolidTerrain;
                    nav.MarkDirty(idx);
                    dirtied = true;
                    break;
                }
                if (!dirtied) break;
                watch.Start();
                nav.Rebuild();
                watch.Stop();
                taken++;
            }
            return taken == 0 ? 0 : watch.Elapsed.TotalMilliseconds / taken;
        }

        static string Machine() =>
            $"{Environment.OSVersion}, {Environment.ProcessorCount} logical CPUs, " +
            $".NET {Environment.Version}, {(Debugger.IsAttached ? "debugger" : "no debugger")}, " +
            $"{DateTime.UtcNow:yyyy-MM-dd HH:mm}Z";

        static readonly object Lock = new object();

        static void Write(StringBuilder report)
        {
            string? into = Environment.GetEnvironmentVariable("ODYSSEY_PROBE");
            if (string.IsNullOrEmpty(into)) into = Path.Combine(Path.GetTempPath(), "odyssey-deep-board.txt");
            lock (Lock) File.AppendAllText(into, report.ToString());
            TestContext.WriteLine(report.ToString());
        }

        static void Settle()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
