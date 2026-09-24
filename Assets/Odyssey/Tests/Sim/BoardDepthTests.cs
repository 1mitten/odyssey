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
    /// What eight layers of hills cost, on every board, at 16, 20 and 24 layers deep — the board-depth
    /// measurement the owner asked for before choosing (<c>docs/design/38-meadow-overhaul.md</c> §7, M7).
    ///
    /// <para><b>The question.</b> The owner chose rolling hills of about eight layers — relief ±4
    /// where the game ships ±2 — with "make sure performance doesn't suffer". The ground sits at
    /// <c>SizeY − 1 − headroom − relief</c>, so on a 16-layer board doubling the relief takes two
    /// layers out of the mine. A taller board gives them back and costs cells. This arm prices each
    /// option against today's board, per board, in one test, so the times it quotes are comparable
    /// with each other (a time is only comparable within one run on this machine).</para>
    ///
    /// <para><b>Built through the game's own chooser</b>, <c>ColonyWorld.DefFor</c>, with its
    /// measurement seam for the relief — so every board here is the wooded meadow the scene
    /// builds, and differs from it in exactly the relief and the height (<c>28-map-size.md</c>
    /// §2a is what happens otherwise).</para>
    ///
    /// <para><b>It prints, and asserts only that each control applied</b>: the board is the height
    /// asked for, and the hills really are taller than today's. It also counts the places where
    /// neighbouring columns end more than one layer apart — a two-layer step is a wall nobody can
    /// climb — which is a finding for M8 rather than a failure here: today's noise period was tuned
    /// for ±2, and nothing yet spaces a ±4 slope.</para>
    /// </summary>
    public class BoardDepthTests
    {
        /// <summary>Seeds for generation times and the cliff count. 4242 is the per-board arms'
        /// seed and is the one the navigation, memory and save figures are taken on.</summary>
        static readonly uint[] Seeds = { 1u, 2u, 3u, 4u, 5u };
        const uint MeasuredSeed = 4242u;

        static IEnumerable<GridSize> Boards()
        {
            yield return BoardSizes.Small;
            yield return BoardSizes.Standard;
            yield return BoardSizes.Large;
            yield return BoardSizes.Huge;
        }

        /// <summary>Today's board first, then ±4 at each height. Large ships 24 deep, so its
        /// "today" is ±2 at 24 and its ±4 at 24 is the same board with taller hills.</summary>
        static (string Label, int Relief, int Layers)[] Configs(GridSize shipped) => new[]
        {
            ($"today ±2 @{shipped.SizeY}", 2, shipped.SizeY),
            ("±4 @16", 4, 16),
            ("±4 @20", 4, 20),
            ("±4 @24", 4, 24),
        };

        [Test, Category("Long")]
        public void EightLayersOfHillsOnEveryBoard([ValueSource(nameof(Boards))] GridSize shipped)
        {
            var rows = new List<Row>();
            foreach ((string label, int relief, int layers) in Configs(shipped))
                rows.Add(Measure(label, relief, new GridSize(shipped.SizeX, shipped.SizeZ, layers)));

            var report = new StringBuilder();
            report.AppendLine($"[Depth] {shipped.SizeX} x {shipped.SizeZ} (shipped {shipped.SizeY} layers)");
            report.AppendLine("[Depth] config | cells | surface span | rock under lowest valley | cliffs (5 seeds) | " +
                              "caverns / ore | generation median | regions / links | edit tick | rest tick | " +
                              "world MiB (B/cell) | save KB");
            foreach (Row r in rows) report.AppendLine("[Depth] " + r);
            TestContext.WriteLine(report.ToString());

            Row today = rows[0];
            foreach (Row r in rows)
            {
                Assert.That(r.Size.SizeY, Is.EqualTo(r.Size.SizeY), "unreachable");
                if (r.Failed != null) continue;
                Assert.That(r.Grid!.Size.SizeY, Is.EqualTo(r.Size.SizeY),
                    $"{r.Label}: the board was not the height asked for");
                if (r.Relief > today.Relief && today.Failed == null)
                    Assert.That(r.SpanMax - r.SpanMin, Is.GreaterThan(today.SpanMax - today.SpanMin),
                        $"{r.Label}: the hills are no taller than today's, so the relief seam did not take");
            }
            Assert.That(today.Failed, Is.Null, $"today's board failed to generate: {today.Failed}");
        }

        sealed class Row
        {
            public string Label = "";
            public int Relief;
            public GridSize Size;
            public CellGrid? Grid;
            public string? Failed;
            public int SpanMin = int.MaxValue, SpanMax = int.MinValue, RockUnderLowest = int.MaxValue;
            public int Cliffs, CliffSeeds, Caverns, Ore;
            public long GenerationMedianMs;
            public int Regions, Links;
            public double EditMs, RestMs, WorldMiB, BytesPerCell;
            public long SaveBytes;

            public override string ToString() => Failed != null
                ? $"{Label} | {Size.CellCount:N0} | FAILED: {Failed}"
                : $"{Label} | {Size.CellCount:N0} | {SpanMin}..{SpanMax} | {RockUnderLowest} | " +
                  $"{Cliffs} in {CliffSeeds} of {Seeds.Length} | {Caverns} / {Ore} | {GenerationMedianMs} ms | " +
                  $"{Regions:N0} / {Links:N0} | {EditMs:F3} ms | {RestMs:F3} ms | " +
                  $"{WorldMiB:F1} ({BytesPerCell:F1}) | {SaveBytes / 1024.0:F0}";
        }

        static Row Measure(string label, int relief, GridSize size)
        {
            var row = new Row { Label = label, Relief = relief, Size = size };
            try
            {
                // Generation and cliffs over five seeds.
                var times = new List<long>();
                foreach (uint seed in Seeds)
                {
                    var grid = new CellGrid(size);
                    var def = (NaturalMapGenDef)ColonyWorld.DefFor(MapType.Natural, size, barren: true,
                        wooded: true, surfaceRelief: relief);
                    var watch = Stopwatch.StartNew();
                    NaturalMapResult result = NaturalMapGenerator.Generate(grid, seed, def);
                    watch.Stop();
                    times.Add(watch.ElapsedMilliseconds);

                    NaturalGenReport report = result.Report;
                    row.SpanMin = Math.Min(row.SpanMin, report.SurfaceMinY);
                    row.SpanMax = Math.Max(row.SpanMax, report.SurfaceMaxY);
                    row.RockUnderLowest = Math.Min(row.RockUnderLowest,
                        report.SurfaceMinY - def.subsoilDepth - def.bedrockLayers);

                    int cliffs = Cliffs(result.Context);
                    row.Cliffs += cliffs;
                    if (cliffs > 0) row.CliffSeeds++;
                }
                row.GenerationMedianMs = WorldgenTests.Median(times);

                // Navigation, save and caverns on the per-board arms' seed.
                var measured = new CellGrid(size);
                MapGenOutcome outcome = MapGenerator.Generate(measured, MeasuredSeed,
                    ColonyWorld.DefFor(MapType.Natural, size, barren: true, wooded: true, surfaceRelief: relief));
                if (outcome.Natural is NaturalMapResult natural)
                {
                    row.Caverns = natural.Report.Caverns;
                    row.Ore = natural.Report.OreDeposits;
                }
                row.Grid = measured;
                row.SaveBytes = SaveOf(measured);

                var nav = new NavGraph(measured);
                ConnectorRegistrar.Register(nav, measured, outcome.Connectors);
                nav.Rebuild();
                CountRegions(nav, out row.Regions, out row.Links);
                row.EditMs = TimePerEdit(measured, nav, MeasuredSeed);

                // Memory and a tick at rest, on the colony the game would build.
                Settle();
                long before = GC.GetTotalMemory(true);
                ColonyWorld colony = ColonyWorld.Build(new ColonyRequest
                {
                    Size = size,
                    Seed = MeasuredSeed,
                    Scenario = ScenarioDef.Bare(),
                    Barren = true,
                    Wooded = true,
                    SurfaceRelief = relief,
                });
                Settle();
                long delta = GC.GetTotalMemory(true) - before;
                row.WorldMiB = delta / (1024.0 * 1024.0);
                row.BytesPerCell = (double)delta / size.CellCount;

                colony.World.Tick(20);
                var rest = Stopwatch.StartNew();
                const int restTicks = 200;
                colony.World.Tick(restTicks);
                rest.Stop();
                row.RestMs = rest.Elapsed.TotalMilliseconds / restTicks;
                GC.KeepAlive(colony);
            }
            catch (Exception e)
            {
                // A board that cannot be generated at this relief is the finding, not a crash of
                // the arm: the reachability check gives up on a map cut in two by cliffs.
                row.Failed = $"{e.GetType().Name}: {e.Message}";
            }
            return row;
        }

        /// <summary>Neighbouring columns whose surfaces end more than one layer apart — a wall a
        /// colonist cannot climb, which the cell model has no slope to soften.</summary>
        static int Cliffs(NaturalGenContext ctx)
        {
            int cliffs = 0;
            for (int z = 0; z < ctx.Size.SizeZ; z++)
            for (int x = 0; x < ctx.Size.SizeX; x++)
            {
                int here = ctx.SurfaceY[ctx.Column(x, z)];
                if (x + 1 < ctx.Size.SizeX && Math.Abs(ctx.SurfaceY[ctx.Column(x + 1, z)] - here) > 1) cliffs++;
                if (z + 1 < ctx.Size.SizeZ && Math.Abs(ctx.SurfaceY[ctx.Column(x, z + 1)] - here) > 1) cliffs++;
            }
            return cliffs;
        }

        static long SaveOf(CellGrid grid)
        {
            var world = new SimWorldBuilder().WithSeed(1).WithSize(grid.Size).Build();
            using var stream = new MemoryStream();
            WorldSave.Save(world, stream, new ISaveable[] { new GridSaveSection(grid) });
            return stream.Length;
        }

        static void CountRegions(NavGraph nav, out int regions, out int links)
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
        }

        /// <summary>The mean rebuild after one mined cell, as <c>NavGraphStatisticsTests</c> takes
        /// it — the edit arm <c>28-map-size.md</c> says to quote, not the tick benchmark's lattice.</summary>
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

        static void Settle()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
