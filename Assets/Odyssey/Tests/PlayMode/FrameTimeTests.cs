#nullable enable
using System;
using System.Collections;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// Frame time from a real player loop, which is the only place it means anything.
    ///
    /// **Why this exists, and why it is a PlayMode test.** The editor benchmark drove
    /// <c>camera.Render()</c> in a loop, and every number it produced tracked how many renders
    /// had gone before rather than what was being drawn — an empty render placed last cost
    /// hundreds of times the same empty render placed first. There is no frame boundary in such
    /// a loop: nothing Presents, and the render pipeline's per-frame bookkeeping is never told a
    /// frame has ended. A PlayMode test runs under the actual player loop, with a Present every
    /// frame, so <c>Time.unscaledDeltaTime</c> here is the figure the player would see.
    ///
    /// This is the first test in the PlayMode gate, which had passed vacuously until now.
    /// </summary>
    public class FrameTimeTests
    {
        const int WarmupFrames = 60;
        const int TimedFrames = 180;

        /// <summary>
        /// Loose on purpose: a regression gate against gross pathology on a dev machine, not the
        /// plan's laptop budget, which cannot be asserted on hardware this much faster.
        /// </summary>
        const float CeilingMs = 33f;

        /// <summary>The barren meadow the scene loads: the frame the player actually gets today.</summary>
        [UnityTest]
        public IEnumerator TheMeadowRendersInsideAFrame() =>
            Measure(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true, "meadow");

        /// <summary>
        /// The ruined city — the map the renderer was built for, and the one with walls in it. The
        /// plan's U14 validation asks for thousands of wall panels across several materials; a
        /// meadow cannot answer that and this can.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCityRendersInsideAFrame() =>
            Measure(Odyssey.Sim.Worldgen.Natural.MapType.RuinedCity, barren: false, "city");

        /// <summary>
        /// A field at the size a serious one actually is: over 2,000 growing-zone cells tinted
        /// every frame through the same span path the standing orders use, with crops in the
        /// ground across their stages — the mesher's per-species stage buckets at their fullest.
        ///
        /// <para>The growth pass itself is off the render frame — O(planted) every 250 ticks,
        /// simulation-side. What this measures is what the player sees, which is the tint and
        /// the crop meshes, and its log line is the number <c>22-growing.md</c> records
        /// against the frame budget.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ATwoThousandCellFieldRendersInsideAFrame() =>
            Measure(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true, "field", SeedField);

        /// <summary>
        /// A thousand standing orders on the board at once, which is what a player marking a
        /// wood or a quarry actually leaves behind.
        ///
        /// <para><b>Why this case had to exist before anything was optimised.</b> The meadow
        /// case is barren and has nothing designated, so the mark pass — one submission per
        /// designated cell, incrementing no counter — was invisible to every frame number this
        /// project has ever quoted (P10, <c>docs/bug-patterns.md</c>). A benchmark measures the
        /// world it builds; orders were not in any of them.</para>
        ///
        /// <para>Mine orders on the topmost solid cell of each column, because that is the one
        /// designation a barren meadow can carry — it needs neither a tree nor a building — and
        /// it lands on the surface, which is inside the drawn band the mark pass filters to.</para>
        ///
        /// <para><b>Paired, in one world, seconds apart.</b> A frame number is only comparable
        /// with one measured in the same session (<c>docs/lessons.md</c>), and this machine runs
        /// more than one Unity at a time — a sibling checkout's PlayMode run took the city canary
        /// from 2.01 ms to 4.01 on 2026-09-20 while this case was being written. Measuring the
        /// same meadow before and after the orders go down cancels all of that: the difference is
        /// the pass, whatever the machine is doing.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheMarkPassCostsWhatItSubmits()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                out OdysseyBootstrap boot);
            try
            {
                yield return null;
                float bare = 0f;
                yield return TimeFrames("orders/none", boot, WarmupFrames, x => bare = x);

                yield return SeedOrders(boot);

                // The control first: the same gathered plates submitted one at a time, which is
                // what the pass did until 2026-09-20.
                boot.Renderer!.InstanceCellPlates = false;
                float perCell = 0f;
                yield return TimeFrames("orders/per-cell", boot, WarmupFrames, x => perCell = x);

                boot.Renderer!.InstanceCellPlates = true;
                float instanced = 0f;
                yield return TimeFrames("orders/instanced", boot, WarmupFrames, x => instanced = x);

                int plates = boot.Renderer!.CellPlatesDrawn;
                Debug.Log($"[FrameTime] mark pass over {plates} cell plates: " +
                          $"bare {bare:0.00} ms, per-cell {perCell:0.00} ms " +
                          $"(+{perCell - bare:0.00}, {(plates > 0 ? (perCell - bare) * 1000f / plates : 0f):0.0} us each), " +
                          $"instanced {instanced:0.00} ms (+{instanced - bare:0.00}); " +
                          $"batching saves {perCell - instanced:0.00} ms");

                Assert.That(plates, Is.GreaterThan(0),
                    "no cell plate was drawn, so this measured nothing: the orders are outside " +
                    "the band DrawStandingOrders filters to");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>How many cells the order case marks. A wood is hundreds; this is the round
        /// number above it, and it is the same order of magnitude as the field's 2,000 so the two
        /// can be read against each other.</summary>
        const int OrderCells = 1_000;

        /// <summary>
        /// What a colony costs as it grows: the frame at eight colony sizes, in one world.
        ///
        /// <para><b>Written from a Play report, 2026-09-20.</b> The owner watched the overlay
        /// while spawning colonists and said the frame "seemed to hover 1.7 ms no matter the
        /// colony size but then frames dropped after so many colonists ... at pretty high
        /// numbers". Flat and then a knee is a specific shape and it has more than one cause —
        /// the figure ceiling is 64, so the renderer's crowd stops growing there while the
        /// simulation's does not; and a small linear term hides under a large constant until it
        /// does not. Neither is worth guessing at when the bootstrap already times both halves
        /// of the frame separately.</para>
        ///
        /// <para>Every step is measured in the same world seconds after the last, which is the
        /// only comparison this machine supports. The figure ceiling is deliberately left at its
        /// default: what is being measured is the game as it ships, not a hypothetical.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheFrameAgainstColonySize()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                out OdysseyBootstrap boot);
            try
            {
                yield return null;
                Assert.That(boot.World, Is.Not.Null, "the bootstrap never built a world");
                Assert.That(boot.Colony, Is.Not.Null, "the bootstrap never built a colony");

                // Past the 64-figure ceiling on both sides of it, and past the audit's scale
                // target of fifty, so the shape either side of each is visible rather than
                // inferred from two points.
                int[] sizes = { 8, 32, 64, 96, 128, 192, 256, 384 };

                foreach (int size in sizes)
                {
                    yield return GrowColonyTo(boot, size);

                    int pawns = boot.World!.Views.Current.Pawns.Length;
                    float mean = 0f;
                    yield return TimeFrames($"colony/{pawns}", boot, 30, x => mean = x);
                    Debug.Log($"[FrameTime] colony {pawns} pawns, " +
                              $"{boot.Figures?.FigureCount ?? 0} figures: {mean:0.00} ms");
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>
        /// Spawn colonists until the colony is this big, spread over the middle of the board so
        /// they do not all arrive in one column and stand on each other.
        /// </summary>
        IEnumerator GrowColonyTo(OdysseyBootstrap boot, int wanted)
        {
            GridSize size = boot.Colony!.Grid.Size;
            int side = Mathf.CeilToInt(Mathf.Sqrt(wanted)) + 1;
            int step = Mathf.Max(1, (size.SizeX / 2) / side);
            int at = 0;

            while (boot.World!.Views.Current.Pawns.Length < wanted)
            {
                int x = size.SizeX / 4 + (at % side) * step;
                int z = size.SizeZ / 4 + (at / side) * step;
                at++;
                if (at > wanted * 4) break;   // the board refused; measure what took
                if (x >= size.SizeX - 1 || z >= size.SizeZ - 1) { at = 0; continue; }

                boot.World!.Intents.Submit(new Intent(IntentKind.SpawnPawn,
                    new CellRef(x, z, size.SizeY - 2), 0));
                boot.World!.Tick();
            }

            yield return null;
        }

        /// <summary>
        /// What a bigger board costs the frame: the three boards the menu offers, each carrying
        /// the same standing orders, timed one after another inside one test.
        ///
        /// <para><b>One test rather than three arms, and that is the whole design of it.</b> This
        /// machine runs several editors at once and a frame number is only comparable with one
        /// taken in the same run — the city canary drifted from 2.01 ms to 4.01 in an afternoon
        /// purely on what a sibling worktree was doing. Three separate arms would let a noisy
        /// minute be read as a board-size effect, which is exactly the wrong conclusion to draw
        /// from this measurement.</para>
        ///
        /// <para><b>The colony is held still on purpose.</b> The largest open cost in the frame is
        /// not board-shaped: <c>PawnPose.Of</c> scans every other pawn for the crowd sidestep once
        /// per posed pawn, which is 13.3 ms of a 22.5 ms frame at 384 colonists and 0.02 at 64. If
        /// this arm varied the colony as well as the board, the board's signal would sit
        /// underneath a pawn-count signal an order of magnitude larger. Same scenario, same seed,
        /// same order count, one thing different.</para>
        ///
        /// <para><b>Read the split, not the total.</b> <c>FrameSection.World</c> is the chunk
        /// buckets — the term that actually scales with the board — <c>FrameSection.Surround</c>
        /// is the land beyond it, which scales with the ring rather than with the board, and
        /// <c>FrameSection.Doors</c> is <c>DoorDirector</c>, which scans every cell in the world
        /// for doors whenever anything has been edited. Those three are what a board size buys,
        /// and the rest of the frame should be flat across all three boards.</para>
        ///
        /// <para>It asserts nothing about time. Every number here is 640 x 480 on a development
        /// GPU and the target is a 2022 laptop, so a threshold would be a threshold on the wrong
        /// machine; what it asserts is that each board really was built and really was designated,
        /// because a world that failed to generate reports a beautifully fast frame.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheBoardSizeAgainstTheFrame()
        {
            (string Label, int X, int Z, int Y)[] boards =
            {
                ("standard", 120, 120, 16),
                ("large", 180, 180, 24),
                ("huge", 240, 240, 16),
            };

            var means = new float[boards.Length];
            var world = new double[boards.Length];
            var surround = new double[boards.Length];
            var doors = new double[boards.Length];
            var chunks = new int[boards.Length];

            for (int b = 0; b < boards.Length; b++)
            {
                (string label, int x, int z, int y) = boards[b];
                GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: false,
                    out OdysseyBootstrap boot, x, z, y);
                try
                {
                    yield return SeedOrders(boot);

                    float mean = 0f;
                    double[] split = Array.Empty<double>();
                    yield return TimeFrames($"board/{label}", boot, WarmupFrames,
                        m => mean = m, p => split = p);

                    means[b] = mean;
                    world[b] = Section(split, OdysseyBootstrap.FrameSection.World);
                    surround[b] = Section(split, OdysseyBootstrap.FrameSection.Surround);
                    doors[b] = Section(split, OdysseyBootstrap.FrameSection.Doors);
                    chunks[b] = boot.Renderer?.ChunksDrawn ?? 0;

                    Assert.That(boot.Colony, Is.Not.Null, $"{label} never built a colony");
                    Assert.That(chunks[b], Is.GreaterThan(0),
                        $"{label} drew no chunks, so this timed an empty frame rather than a board");
                }
                finally
                {
                    UnityEngine.Object.Destroy(root);
                }

                // Let the old world's arrays go before the next one is built, so a later board is
                // not timed against a heap still holding an earlier one.
                yield return null;
                GC.Collect();
                yield return null;
            }

            for (int b = 0; b < boards.Length; b++)
                Debug.Log($"[FrameTime] board {boards[b].Label} " +
                          $"{boards[b].X}x{boards[b].Z}x{boards[b].Y}: " +
                          $"frame {means[b]:0.00} ms, World {world[b]:0.000} ms, " +
                          $"Surround {surround[b]:0.000} ms, " +
                          $"Doors {doors[b]:0.000} ms, {chunks[b]} chunks drawn " +
                          $"(x{(means[0] > 0 ? means[b] / means[0] : 0):0.00} frame, " +
                          $"x{(world[0] > 0 ? world[b] / world[0] : 0):0.00} World against standard)");

            Assert.That(chunks[2], Is.GreaterThan(chunks[0]),
                "the huge board drew no more chunks than the standard one, so the size seam did " +
                "not take and all three readings are the same board");
        }

        static double Section(double[] split, OdysseyBootstrap.FrameSection section) =>
            split.Length > (int)section ? split[(int)section] : 0d;

        /// <summary>
        /// What the decoration costs: the grass tufts and the land beyond the board, measured
        /// against each other and against a board with neither.
        ///
        /// <para><b>Why this arm exists.</b> The owner reported (2026-09-21) that the tufts and
        /// the surround appeared to be costing frames, and the project had no way to answer
        /// except by opinion: the tufts are baked into the chunk mesh so they hide inside
        /// <c>FrameSection.World</c>, and until the same day the surround hid there too. Both are
        /// already player-facing switches (<c>GraphicsOption.GrassTufts</c>,
        /// <c>GraphicsOption.Surround</c>), so the question is not whether they can be turned off
        /// but what turning them off is worth.</para>
        ///
        /// <para><b>One world, four readings, and that is the whole method.</b> This machine runs
        /// several editors at once and a frame number taken in one run is not comparable with one
        /// taken in another — the city canary drifted 2.01 to 4.01 ms in an afternoon on nothing
        /// but a sibling worktree (§6c). So the same built world is timed with everything on,
        /// with the tufts off, with the surround off and with both off, in that order and without
        /// a rebuild between them. Only the differences are quoted.</para>
        ///
        /// <para><b>What it cannot see, and the reason it must be read beside a Play session.</b>
        /// Every number here is a stopwatch around CPU submission at 640 x 480. Alpha-tested
        /// foliage is exactly the geometry whose cost is nil at 307k pixels and dominant at
        /// 1080p, so a tuft reading of "free" here is a statement about submission and not about
        /// fill. <c>OdysseyBootstrap.GpuFrameMs</c> on the developer overlay is the other half.
        /// </para>
        ///
        /// <para>It asserts no times, for the reason every arm in this file gives: the budget is
        /// for a 2022 laptop and this is not one. What it asserts is that the world really had
        /// tufts and a surround to take away, because a board that generated neither reports a
        /// beautifully cheap frame and a difference of zero.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheDecorationAgainstTheFrame()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: false,
                out OdysseyBootstrap boot);
            try
            {
                float all = 0f, noTufts = 0f, noSurround = 0f, bare = 0f;
                double[] allSplit = Array.Empty<double>(), bareSplit = Array.Empty<double>();

                // The shipped case first, and the renderer read afterwards: the bootstrap builds
                // it on its own first Update, so there is nothing to ask before a frame has run.
                yield return TimeFrames("decoration/all", boot, WarmupFrames,
                    m => all = m, p => allSplit = p);

                ChunkRenderer renderer = boot.Renderer!;
                int shippedDensity = renderer.ScatterDensity;
                int surroundTrees = renderer.Skirt.TreeInstances + renderer.Skirt.FarTreeInstances;

                Assert.That(shippedDensity, Is.GreaterThan(0),
                    "this board strews no tufts, so there is nothing to take away");

                // The tufts are meshed into the chunks, so taking them away is a re-mesh and not
                // a flag read at submission — the same rule ScatterDensity's own comment states.
                renderer.ScatterDensity = 0;
                boot.Model!.Remesh();
                yield return null;
                yield return TimeFrames("decoration/no tufts", boot, WarmupFrames, m => noTufts = m);

                renderer.ScatterDensity = shippedDensity;
                boot.Model!.Remesh();
                renderer.Skirt.Enabled = false;
                yield return null;
                yield return TimeFrames("decoration/no surround", boot, WarmupFrames, m => noSurround = m);

                renderer.ScatterDensity = 0;
                boot.Model!.Remesh();
                yield return null;
                yield return TimeFrames("decoration/bare", boot, WarmupFrames,
                    m => bare = m, p => bareSplit = p);

                Assert.That(surroundTrees, Is.GreaterThan(0),
                    "no wood grew outside this board, so the surround reading is of empty ground");

                Debug.Log($"[FrameTime] decoration on {Screen.width}x{Screen.height}: " +
                          $"all {all:0.00} ms, no tufts {noTufts:0.00} ms " +
                          $"(-{all - noTufts:0.00}), no surround {noSurround:0.00} ms " +
                          $"(-{all - noSurround:0.00}), neither {bare:0.00} ms " +
                          $"(-{all - bare:0.00}); " +
                          $"Surround section {Section(allSplit, OdysseyBootstrap.FrameSection.Surround):0.000} " +
                          $"-> {Section(bareSplit, OdysseyBootstrap.FrameSection.Surround):0.000} ms, " +
                          $"World {Section(allSplit, OdysseyBootstrap.FrameSection.World):0.000} " +
                          $"-> {Section(bareSplit, OdysseyBootstrap.FrameSection.World):0.000} ms, " +
                          $"{surroundTrees} surround trees, tuft density {shippedDensity}");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>
        /// Designate the board row-major until a thousand orders stand, the same walk and the
        /// same draining <see cref="SeedField"/> uses and for the same reasons: the meadow
        /// refuses what stands on it, and the intent bus has a capacity.
        ///
        /// <para><b>Only inside the band the mark pass draws</b>, which is the whole measurement.
        /// <c>OdysseyBootstrap.DrawStandingOrders</c> filters every order to
        /// <c>LowestSelectableLayer .. HighestSelectableLayer</c>, and above the surface the
        /// highest is the active layer itself — so orders placed on a plateau north of the camera
        /// are published, counted and never drawn. The first version of this case walked from
        /// <c>z = 1</c> and put all 901 of its orders on ground the slice was not drawing: it
        /// measured 4.85 ms against the meadow's 4.86, which reads as "the pass is free" and was
        /// in fact "the pass did not run". The band is asked for here rather than assumed.</para>
        /// </summary>
        IEnumerator SeedOrders(OdysseyBootstrap boot)
        {
            yield return null;
            Assert.That(boot.World, Is.Not.Null, "the bootstrap never built a world");
            Assert.That(boot.Colony, Is.Not.Null, "the bootstrap never built a colony");
            Assert.That(boot.cameraRig, Is.Not.Null, "no camera rig, so no drawn band");

            var grid = boot.Colony!.Grid;
            var size = grid.Size;
            int lowest = Math.Max(0, boot.cameraRig!.LowestSelectableLayer);
            int highest = boot.cameraRig!.HighestSelectableLayer;

            int submitted = 0, placed = 0;
            for (int z = 1; z < size.SizeZ - 1 && placed < OrderCells; z++)
            for (int x = 1; x < size.SizeX - 1 && placed < OrderCells; x++)
            {
                int top = -1;
                for (int y = size.SizeY - 2; y >= 0; y--)
                    if ((grid.Flags[size.Index(x, z, y)] & CellFlags.SolidTerrain) != 0)
                    { top = y; break; }
                if (top < lowest || top > highest) continue;

                boot.World!.Intents.Submit(new Intent(IntentKind.Designate,
                    new CellRef(x, z, top), (int)Odyssey.Sim.Designations.DesignationKind.Mine));
                placed++;
                if (++submitted % 512 == 0) boot.World!.Tick();
            }
            boot.World!.Tick();

            int standing = boot.World!.Views.Current.Orders.Length;
            Debug.Log($"[FrameTime] orders: {standing} standing orders, drawn band {lowest}..{highest}");
            Assert.That(standing, Is.GreaterThanOrEqualTo(OrderCells * 8 / 10),
                $"only {standing} of {OrderCells} designations took inside the drawn band " +
                $"{lowest}..{highest}, so this is no longer the measure it names");
        }

        /// <summary>
        /// Paint the board's open ground until the zone passes two thousand cells, sown by the
        /// colony itself: the intents go in, the world ticks forward past four daylight
        /// windows, and what is measured is a field the pawns actually planted — not a mirror
        /// filled by hand.
        ///
        /// <para>The board is walked rather than a block painted because the meadow refuses
        /// what stands on it — water, trees, marsh — and a fixed 45×45 block over the start
        /// came up 860 of 2,025 on seed 1, which is not the measure this test names. Draining
        /// every few hundred submissions keeps the intent bus under its capacity, and the
        /// walking order is row-major, so the field is the band across the top of the map.</para>
        /// </summary>
        IEnumerator SeedField(OdysseyBootstrap boot)
        {
            // One frame for Start to have built the session.
            yield return null;
            Assert.That(boot.World, Is.Not.Null, "the bootstrap never built a world");
            Assert.That(boot.Colony, Is.Not.Null, "the bootstrap never built a colony");
            Assert.That(boot.Colony!.Growing, Is.Not.Null, "the session has no growing zones");

            var grid = boot.Colony.Grid;
            var size = grid.Size;
            int submitted = 0;
            for (int z = 1; z < size.SizeZ - 1 && boot.Colony.Growing.Cells.Count < 2_000; z++)
            for (int x = 1; x < size.SizeX - 1 && boot.Colony.Growing.Cells.Count < 2_000; x++)
            {
                // The air cell above the column's topmost solid ground, which is the cell a
                // zone lives in — the same lift the designate gesture applies.
                int top = -1;
                for (int y = size.SizeY - 2; y >= 0; y--)
                    if ((grid.Flags[size.Index(x, z, y)] & CellFlags.SolidTerrain) != 0)
                    { top = y; break; }
                if (top < 0 || top + 1 >= size.SizeY) continue;

                boot.World!.Intents.Submit(new Intent(IntentKind.DesignateZone,
                    new CellRef(x, z, top + 1), PlantHandle.Carrot + 1));
                if (++submitted % 512 == 0) boot.World!.Tick();
            }
            boot.World!.Tick();

            Assert.That(boot.Colony.Growing.Cells.Count, Is.GreaterThanOrEqualTo(2_000),
                $"only {boot.Colony.Growing.Cells.Count} field cells took, so this is no " +
                "longer a two-thousand-cell measure");

            // Past four daylight windows (~227,500 growth ticks) plus the sowing of the whole
            // zone: most of the field is ripe or already cut and re-sown, which is the
            // mixed-stage state a real field is measured in.
            boot.World!.Tick(600_000);
            LogFieldState(boot.Colony);
        }

        static void LogFieldState(Odyssey.Sim.Pawns.ColonyWorld colony)
        {
            int ripe = 0;
            foreach (int cell in colony.Growing!.Planted)
                if (colony.Growing.IsRipe(cell)) ripe++;
            Debug.Log($"[FrameTime] field: {colony.Growing.Cells.Count} zone cells, " +
                      $"{colony.Growing.Planted.Count} in the ground, {ripe} ripe at measure time");
        }

        IEnumerator Measure(Odyssey.Sim.Worldgen.Natural.MapType mapType, bool barren, string label,
                            Func<OdysseyBootstrap, IEnumerator>? seed = null)
        {
            GameObject root = Build(mapType, barren, out OdysseyBootstrap boot);

            try
            {
                if (seed != null) yield return seed(boot);

                float mean = 0f;
                yield return TimeFrames(label, boot, WarmupFrames, x => mean = x);

                Assert.That(mean, Is.LessThan(CeilingMs),
                    "the play world takes longer than a 30 Hz frame on a development machine");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>
        /// Warm up, time <see cref="TimedFrames"/> frames, print the line, hand back the mean.
        ///
        /// <para>Its own method so a test can time the same world twice and quote the
        /// difference, which is the only figure this machine can be trusted for.</para>
        /// </summary>
        IEnumerator TimeFrames(string label, OdysseyBootstrap boot, int warmup, Action<float> mean,
                               Action<double[]>? sections = null)
        {
            for (int i = 0; i < warmup; i++) yield return null;

            float total = 0f, worst = 0f;
            double tick = 0d, submit = 0d;
            var sectionTotals = new double[(int)OdysseyBootstrap.FrameSection.Count];
            for (int i = 0; i < TimedFrames; i++)
            {
                yield return null;
                float ms = Time.unscaledDeltaTime * 1000f;
                total += ms;
                if (ms > worst) worst = ms;
                // The two halves the bootstrap already times, so a difference between two
                // readings can be attributed rather than assumed. A board full of standing
                // orders costs the work givers as well as the renderer.
                tick += boot.TickMs;
                submit += boot.SubmitMs;
                System.ReadOnlySpan<double> split = boot.FrameSectionMs;
                for (int k = 0; k < sectionTotals.Length && k < split.Length; k++) sectionTotals[k] += split[k];
            }

            float meanMs = total / TimedFrames;
            mean(meanMs);

            ChunkRenderer? renderer = boot.Renderer;
            // Resolution matters to the reading: a fullscreen pass or a sky costs per pixel,
            // and the batch game view is not the player's monitor.
            Debug.Log($"[FrameTime] {label}: mean {meanMs:0.00} ms, worst {worst:0.00} ms over {TimedFrames} frames; " +
                      $"tick {tick / TimedFrames:0.000} ms, submit {submit / TimedFrames:0.000} ms; " +
                      $"{renderer?.DrawCalls ?? 0} draw calls, {renderer?.InstancesDrawn ?? 0} instances, " +
                      // The mark pass drew once per designated cell and counted none of it,
                      // so a case that designates has to print the pass's own number or the
                      // reading cannot be told apart from the pass not running at all.
                      $"{renderer?.CellPlatesDrawn ?? 0} cell plates, " +
                      $"{renderer?.ChunksDrawn ?? 0} chunks; " +
                      // The surround is built once and submitted whole, so its own counts are
                      // the only way to attribute a frame-time change to it rather than to the
                      // board. The hill wood in particular is a switch somebody will want to
                      // weigh, and a number beats an opinion about it.
                      $"surround {renderer?.Skirt.TreeInstances ?? 0} trees + " +
                      $"{renderer?.Skirt.FarTreeInstances ?? 0} on the hills, " +
                      $"{renderer?.Skirt.BatchesDrawn ?? 0} batches; " +
                      $"{Screen.width}x{Screen.height}, " +
                      $"{SystemInfo.graphicsDeviceName}");

            // Submit, split by what it was doing. A frame number that says "the renderer is
            // slow" without saying which part of it is slow only licences a guess.
            var parts = new System.Text.StringBuilder();
            for (int k = 0; k < sectionTotals.Length; k++)
            {
                if (k > 0) parts.Append(", ");
                parts.Append((OdysseyBootstrap.FrameSection)k).Append(' ')
                     .Append((sectionTotals[k] / TimedFrames).ToString("0.000"));
            }
            Debug.Log($"[FrameTime] {label} submit split: {parts}");

            if (sections != null)
            {
                var perFrame = new double[sectionTotals.Length];
                for (int k = 0; k < sectionTotals.Length; k++) perFrame[k] = sectionTotals[k] / TimedFrames;
                sections(perFrame);
            }
        }

        /// <summary>The play scene's objects, built by hand: a camera with the rig, a sun, the bootstrap.</summary>
        static GameObject Build(Odyssey.Sim.Worldgen.Natural.MapType mapType, bool barren,
                                out OdysseyBootstrap boot,
                                int sizeX = 120, int sizeZ = 120, int layers = 16)
        {
            var root = new GameObject("FrameTime");

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1800f; // as the play scene, so the surround is measured too
            var rig = cameraObject.AddComponent<SliceCameraRig>();

            // The grade and the anti-aliasing, as the play scene has them. **URP keeps post
            // per camera and defaults it to false**, so without these two lines this test
            // measures a frame the player never sees — and would have reported the golden hour
            // as free, which is the most misleading answer available. Post cost also scales with
            // pixels, and this runs at 640x480, so the figure is a floor and not the laptop's.
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root.transform, false);
            sun.type = LightType.Directional;
            // The golden hour's own numbers, and they are copied rather than referenced because
            // GoldenHour is editor tooling and this assembly is not. The duplication is deliberate
            // and it is load-bearing: a shadow's length is height over the tangent of the
            // elevation, so at 30 degrees the shadow volume is several times what it was at 72.
            // Measuring the old sun would understate the shadow pass by most of its cost.
            sun.intensity = 2.0f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.6f;
            sun.transform.rotation = Quaternion.Euler(30f, 135f, 0f);

            var bootObject = new GameObject("Bootstrap");
            bootObject.transform.SetParent(root.transform, false);
            bootObject.SetActive(false);   // so the fields land before Start runs
            boot = bootObject.AddComponent<OdysseyBootstrap>();
            // Explicitly, not by default: since U38 pressing Play lands on the start screen, and
            // what this rig is asserting is that a session exists.
            boot.buildOnPlay = true;
            boot.sizeX = sizeX;
            boot.sizeZ = sizeZ;
            boot.layers = layers;
            boot.seed = 1;
            boot.mapType = mapType;
            boot.barrenMap = barren;
            boot.grassScatter = 60;
            boot.cameraRig = rig;
#if UNITY_EDITOR
            // Real art when the packs are present, the same way the scene gets it. A clone without
            // them renders primitives, which is still a frame worth timing.
            boot.moduleCatalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(
                "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
#endif
            bootObject.SetActive(true);

            // The harness builds its own objects, so the scene's global volume is not here and
            // must be made. Without it the camera would render post-processing over an empty
            // stack, which costs almost nothing and proves almost nothing.
            var volume = new GameObject("Golden Hour").AddComponent<Volume>();
            volume.transform.SetParent(root.transform, false);
            volume.isGlobal = true;
#if UNITY_EDITOR
            volume.sharedProfile = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                "Assets/Settings/OdysseyGoldenHour.asset");
#endif
            return root;
        }
    }
}
