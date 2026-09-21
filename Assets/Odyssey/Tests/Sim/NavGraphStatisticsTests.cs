#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What the region graph actually costs on a full-size board (OQ-18).
    ///
    /// <para><b>Why this exists.</b> <c>docs/research/d-04-pathfinding.md</c> made two quantitative
    /// claims about forty layers and could measure neither, because when it was written there was
    /// no map generator to measure. Its own "Could not be determined" list names the gap: *the real
    /// distribution of live (non-uniform) chunks in a stamped ruined-city map at our dimensions —
    /// needed to turn the region-count estimate from an upper bound into a budget.* Both maps and
    /// the graph exist now, so the estimate can stop being an estimate.</para>
    ///
    /// <para><b>One of d-04's two claims survived contact and the other did not</b>, which is the
    /// reason to run it rather than to reason about it. The budget — "a live region count in the
    /// low tens of thousands, not the theoretical 50 k-plus" — holds on both maps. The mechanism
    /// d-04 credited for it does not: "Most chunks in a ruined-city column are all-air or
    /// all-solid; those allocate no regions at all" is true of the city (65% of blocks are
    /// uniform) and **false of the natural map**, where only 7.9% are. All-solid is exactly the
    /// case that *does* allocate here, because <see cref="RegionKind.Impassable"/> regions are
    /// kept deliberately so rooms and atmosphere have a substrate. Underground rock therefore
    /// fills every block with one region, and the natural map's 24,141 regions are very nearly its
    /// 23,031 live blocks.</para>
    ///
    /// <para>So the uniformity figure is <b>recorded and not asserted</b>: it is a property of
    /// what the generator made, it differs by a factor of eight between two maps that are both
    /// correct, and an assertion on it would fail the day someone tunes cavern density. What is
    /// asserted is what must not change whatever the generator does.</para>
    ///
    /// <para><b>These are assertions and not just print statements</b>, because a measurement
    /// nobody checks is a measurement that silently rots — and two of them are the structural
    /// guarantees the whole layered design rests on rather than budget lines: a region never spans
    /// two layers, and a region never outgrows its block. Both are checked over every cell of a
    /// full-size generated board, which is the only place they can be checked honestly.</para>
    ///
    /// <para><b>Nothing here widens <see cref="NavGraph"/>.</b> There is no public link enumerator,
    /// so links are counted by walking each live region's adjacency and de-duplicating by link id —
    /// every link appears in both of its regions. Adding an accessor for the convenience of a test
    /// would put a measurement's needs into a shared file, which is the habit the seam work exists
    /// to break.</para>
    /// </summary>
    public class NavGraphStatisticsTests
    {
        /// <summary>250 x 250 x 40, the scale target from ADR 0002 and the size d-04 reasoned about.</summary>
        static readonly GridSize Scale = GridSize.ScaleTarget;

        /// <summary>
        /// d-04's budget: "low tens of thousands, not the theoretical 50 k-plus". Asserted as the
        /// upper bound the sentence rules out rather than as the figure measured, so the test
        /// fails on a density regression and not on a generator that plants a different number of
        /// trees.
        /// </summary>
        const int RegionBudget = 50_000;

        [Test, Category("Long")]
        public void TheNaturalMapAtTheScaleTarget() => Measure(MapType.Natural, "natural", seed: 4242u, Scale);

        [Test, Category("Long")]
        public void TheRuinedCityAtTheScaleTarget() => Measure(MapType.RuinedCity, "ruined city", seed: 4242u, Scale);

        /// <summary>
        /// The same walk over each board the menu offers.
        ///
        /// <para><b>The region count is the number the edit tick is priced in.</b>
        /// <c>NavGraph.Rebuild</c> floods only the dirty blocks and then runs four passes over
        /// every region and link in the world, so what an edit costs tracks the board's live
        /// region count and nothing else about it. That count is a property of what the generator
        /// made, not arithmetic on the dimensions, so it has to be measured before any
        /// per-board edit-tick figure means anything. This arm is where it comes from, and
        /// <c>docs/design/28-map-size.md</c> quotes it beside the tick benchmark's.</para>
        ///
        /// <para>Large is here because it ships and nothing had ever run it, and because it is
        /// the control that separates layers from columns: it has fewer cells than Huge and more
        /// layers, so if depth drives regions rather than ground area, this pair says so.</para>
        /// </summary>
        [Test, Category("Long")]
        public void EveryOfferedBoard(
            [ValueSource(nameof(OfferedBoards))] GridSize size)
            => Measure(MapType.Natural, "natural", seed: 4242u, size);

        static IEnumerable<GridSize> OfferedBoards()
        {
            yield return BoardSizes.Standard;
            yield return BoardSizes.Large;
            yield return BoardSizes.Huge;
        }

        static void Measure(MapType type, string label, uint seed, GridSize size)
        {
            var grid = new CellGrid(size);
            var generation = Stopwatch.StartNew();
            MapGenOutcome outcome = MapGenerator.Generate(grid, seed, type);
            generation.Stop();

            var nav = new NavGraph(grid);

            // Before the first rebuild, exactly as ColonyWorld.Build does it: a connector added
            // afterwards needs another rebuild to reach the region graph.
            ConnectorRegistrar.Result connectors = ConnectorRegistrar.Register(nav, grid, outcome.Connectors);

            var rebuild = Stopwatch.StartNew();
            nav.Rebuild();
            rebuild.Stop();

            Stats s = Gather(nav, size);

            // What one mined cell costs, on the board a player is actually given. Taken after
            // Gather, because it digs.
            double perEdit = TimePerEdit(grid, nav, seed);

            var report = new StringBuilder();
            report.AppendLine($"--- {label} {size.SizeX} x {size.SizeZ} x {size.SizeY} (seed {seed}) ---");
            report.AppendLine($"generated in {generation.ElapsedMilliseconds} ms; full nav rebuild {rebuild.ElapsedMilliseconds} ms");
            report.AppendLine($"rebuild after one mined cell: {perEdit:F3} ms mean of 200");
            report.AppendLine($"regions {s.Regions} live of {nav.RegionCapacity} allocated; links {s.Links}");
            report.AppendLine($"cells in regions {s.CellsInRegions} of {size.CellCount} ({Percent(s.CellsInRegions, size.CellCount)}); " +
                              $"mean region {Mean(s.CellsInRegions, s.Regions)} cells, largest {s.LargestRegion}");
            report.AppendLine($"blocks {s.LiveBlocks} live of {nav.BlockCount} " +
                              $"({Percent(s.LiveBlocks, nav.BlockCount)} live, {Percent(nav.BlockCount - s.LiveBlocks, nav.BlockCount)} uniform)");
            report.AppendLine($"connectors: {connectors}");
            report.AppendLine($"links by kind: {s.LinkKinds()}");
            report.AppendLine($"regions by kind: {s.RegionKinds()}");

            foreach (TraverseMode mode in Enum.GetValues(typeof(TraverseMode)))
                report.AppendLine($"districts ({mode}): {nav.DistrictCount(mode)}");

            report.AppendLine("layer: regions / links / vertical links / live blocks");
            for (int y = 0; y < size.SizeY; y++)
            {
                if (s.RegionsPerLayer[y] == 0 && s.LiveBlocksPerLayer[y] == 0) continue;
                report.AppendLine($"  {y,2}: {s.RegionsPerLayer[y],6} / {s.LinksPerLayer[y],6} / " +
                                  $"{s.VerticalLinksPerLayer[y],5} / {s.LiveBlocksPerLayer[y],5}");
            }

            TestContext.WriteLine(report.ToString());

            Assert.That(s.Regions, Is.GreaterThan(0), "a generated board with no regions is a broken board, not a cheap one");
            Assert.That(s.Regions, Is.LessThan(RegionBudget),
                $"d-04 budgets the live region count in the low tens of thousands; {label} has {s.Regions}");

            // "Regions are strictly single-layer. Vertical connectivity exists only as portal
            // links." That is d-04's structural guarantee and the reason a layer can rebuild
            // without touching its neighbours; if it ever stopped holding, every claim built on
            // top of it would be quietly wrong. Checked over all 2.5 million cells, because the
            // first region to span two layers would be one region on one board.
            Assert.That(s.SpanningRegions, Is.Empty,
                $"regions must never span layers; {s.SpanningRegions.Count} do on the {label} map");

            // A region is a connected part of one 10 x 10 block, so it cannot hold more cells than
            // the block does. This is the bound that makes the budget above a ceiling rather than
            // a hope: the worst case is one region per block per layer, which is BlocksX x BlocksZ
            // x layers, and both maps land just under it for that reason.
            Assert.That(s.LargestRegion, Is.LessThanOrEqualTo(NavGraph.BlockSize * NavGraph.BlockSize),
                $"a region outgrew its block on the {label} map");

            // Not asserted, deliberately: s.LiveBlocks. See the class comment — the uniformity
            // short-circuit d-04 credits for the budget is real on the city and absent on the
            // natural map, and both are correct.
        }

        /// <summary>
        /// One pass over the live regions and their adjacency. Links are de-duplicated by id
        /// because the adjacency lists an undirected link from both of its ends.
        /// </summary>
        /// <summary>
        /// The mean cost of <see cref="NavGraph.Rebuild"/> after a single cell is mined, on this
        /// generated board.
        ///
        /// <para><b>This is the figure a board size is bought with, and it belongs here rather
        /// than in the tick benchmark, because it has to be taken on a board the generator made.</b>
        /// <c>TickBenchmarkTests</c> builds a synthetic lattice of rooms with rubble scattered
        /// through it, which fragments every block and carries roughly nine times the regions a
        /// generated map of the same size does — 19,606 against 2,110 at 120 x 120 x 16. That is
        /// a fair stress world and a poor answer to "what does the board the player is given
        /// cost", and the rebuild's cost tracks the region count almost exactly, so the two differ
        /// by about an order of magnitude.</para>
        ///
        /// <para>The dirty cell is re-marked before every call. <c>Rebuild</c> returns on its
        /// first line when no block is dirty, so timing repeated calls after a single mark would
        /// measure an early return one hundred and ninety-nine times.</para>
        /// </summary>
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

        static Stats Gather(NavGraph nav, GridSize size)
        {
            var s = new Stats(size.SizeY);
            var seenLinks = new HashSet<int>();
            var liveBlocks = new HashSet<int>();

            // Which layer each region was first seen on, so a region appearing on a second one can
            // be named. A layer is contiguous in memory, so this walks each layer's span in order
            // and never computes a cell index by hand.
            var regionLayer = new int[nav.RegionCapacity];
            for (int r = 0; r < regionLayer.Length; r++) regionLayer[r] = -1;

            for (int y = 0; y < size.SizeY; y++)
            {
                int start = y * size.LayerStride;
                for (int i = 0; i < size.LayerStride; i++)
                {
                    int region = nav.RegionOfCell(start + i);
                    if (region < 0) continue;

                    if (regionLayer[region] < 0) regionLayer[region] = y;
                    else if (regionLayer[region] != y) s.SpanningRegions.Add(region);
                }
            }

            for (int r = 0; r < nav.RegionCapacity; r++)
            {
                if (!nav.IsRegionAlive(r)) continue;

                int y = size.FromIndex(nav.RegionMinCell(r)).Y;
                int cells = nav.RegionCellCount(r);

                s.Regions++;
                s.RegionsPerLayer[y]++;
                s.CellsInRegions += cells;
                if (cells > s.LargestRegion) s.LargestRegion = cells;
                s.ByRegionKind[(int)nav.KindOfRegion(r)]++;

                int block = nav.RegionBlock(r);
                if (liveBlocks.Add(block)) s.LiveBlocksPerLayer[y]++;

                int start = nav.AdjacencyStart(r);
                int count = nav.AdjacencyCount(r);
                for (int i = 0; i < count; i++)
                {
                    int link = nav.AdjacencyLink(start + i);
                    if (!seenLinks.Add(link)) continue;

                    s.Links++;
                    s.ByLinkKind[(int)nav.KindOfLink(link)]++;

                    // A link is filed under the layer of its lower end, so a stair counts once,
                    // on the floor somebody climbs from.
                    int ya = size.FromIndex(nav.LinkCellA(link)).Y;
                    int yb = size.FromIndex(nav.LinkCellB(link)).Y;
                    int lower = ya < yb ? ya : yb;
                    s.LinksPerLayer[lower]++;
                    if (ya != yb) s.VerticalLinksPerLayer[lower]++;
                }
            }

            s.LiveBlocks = liveBlocks.Count;
            return s;
        }

        static string Percent(int part, int whole) => whole == 0 ? "n/a" : $"{100.0 * part / whole:F1}%";

        static string Mean(int total, int count) => count == 0 ? "n/a" : $"{(double)total / count:F1}";

        sealed class Stats
        {
            public int Regions;
            public int Links;
            public int CellsInRegions;
            public int LargestRegion;
            public int LiveBlocks;

            /// <summary>Regions found on more than one layer. Must stay empty.</summary>
            public readonly HashSet<int> SpanningRegions = new HashSet<int>();

            public readonly int[] RegionsPerLayer;
            public readonly int[] LinksPerLayer;
            public readonly int[] VerticalLinksPerLayer;
            public readonly int[] LiveBlocksPerLayer;

            public readonly int[] ByRegionKind = new int[Enum.GetValues(typeof(RegionKind)).Length];
            public readonly int[] ByLinkKind = new int[Enum.GetValues(typeof(LinkKind)).Length];

            public Stats(int layers)
            {
                RegionsPerLayer = new int[layers];
                LinksPerLayer = new int[layers];
                VerticalLinksPerLayer = new int[layers];
                LiveBlocksPerLayer = new int[layers];
            }

            public string RegionKinds() => Tally<RegionKind>(ByRegionKind);
            public string LinkKinds() => Tally<LinkKind>(ByLinkKind);

            static string Tally<T>(int[] counts) where T : Enum
            {
                var parts = new List<string>();
                foreach (T value in Enum.GetValues(typeof(T)))
                {
                    int i = Convert.ToInt32(value);
                    if (i >= 0 && i < counts.Length && counts[i] > 0) parts.Add($"{value} {counts[i]}");
                }
                return parts.Count == 0 ? "none" : string.Join(", ", parts);
            }
        }
    }
}
