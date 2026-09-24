#nullable enable
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
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

        /// <summary>
        /// What showing the power lines costs (design 32 §11): two thousand lines on the ground
        /// across the drawn band, the frame with them hidden against the frame with the overlay
        /// on, seconds apart in one session so that whatever the machine is doing cancels out.
        ///
        /// <para>The draw-call half is the one that is a gate: hidden, the pass submits nothing;
        /// shown, it submits in colours — a handful of calls — never one per line
        /// (<c>docs/bug-patterns.md</c> P10). The milliseconds are logged for design 32 §11 and
        /// asserted against nothing, for the reason <c>CLAUDE.md</c> gives about every frame number
        /// on a machine running several editors at once.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ThePowerLinesCostWhatTheySubmit()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                out OdysseyBootstrap boot);
            try
            {
                yield return null;
                Assert.That(boot.Colony, Is.Not.Null, "the bootstrap never built a colony");
                var colony = boot.Colony!;
                var power = colony.Pawns.Power!;
                var grid = colony.Grid;
                var size = grid.Size;

                // Straight runs along X on the surface, one row in every two, so the rows stay
                // separate nets and the links are real: two thousand cells.
                int laid = 0;
                for (int z = 2; z < size.SizeZ - 2 && laid < PowerLines; z += 2)
                for (int x = 2; x < size.SizeX - 2 && laid < PowerLines; x++)
                {
                    int top = -1;
                    for (int y = size.SizeY - 2; y >= 0; y--)
                        if ((grid.Flags[size.Index(x, z, y)] & CellFlags.SolidTerrain) != 0) { top = y; break; }
                    if (top < 0 || top + 1 >= size.SizeY) continue;
                    power.AddLine(size.Index(x, z, top + 1));
                    laid++;
                }
                boot.World!.Tick();

                float hidden = 0f;
                yield return TimeFrames("power/hidden", boot, WarmupFrames, x => hidden = x);
                Assert.That(boot.PowerLineDrawCalls, Is.Zero, "hidden lines are not submitted");

                boot.Directors!.Overlays.SetPower(true);
                // The watch goes out on the next frame and is answered on the tick after it.
                for (int i = 0; i < 4; i++) { yield return null; boot.World!.Tick(); }

                float shown = 0f;
                yield return TimeFrames("power/shown", boot, WarmupFrames, x => shown = x);
                int calls = boot.PowerLineDrawCalls;

                Debug.Log($"[FrameTime] power lines: {laid} lines, hidden {hidden:0.00} ms, " +
                          $"shown {shown:0.00} ms (+{shown - hidden:0.00}), {calls} draw calls");

                Assert.That(calls, Is.GreaterThan(0), "the overlay drew nothing, so this measured nothing");
                Assert.That(calls, Is.LessThanOrEqualTo(24), "two thousand lines must cost draws in colours, not in lines");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        const int PowerLines = 2_000;

        /// <summary>
        /// What a warehouse costs to draw: forty shelves holding eight stacks each, against the
        /// same three hundred and twenty stacks lying on the floor, in one world.
        ///
        /// <para><b>The measurement <c>30-shelves.md</c> §8b owed.</b> A shelf's goods are drawn
        /// by the loose-pile path — the same ramp, the same spiral, tightened to a slot — so by
        /// construction they add matrices and not submissions. That is an argument, and this is
        /// the number: the frame with the stacks on the floor, then the frame with the same
        /// stacks on shelves, seconds apart in one session so that whatever the machine is doing
        /// cancels out. The difference is what the shelf path itself costs over the floor path
        /// it was copied from — the draped root, the turned slot and the second facing lookup,
        /// paid once per stack rather than once per shelf.</para>
        ///
        /// <para>Every stack is forbidden and so is the starting kit, because the colony is
        /// running underneath this and a Preferred store that accepts everything would otherwise
        /// be filled with the colonists' own belongings while the frame was being timed.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheWarehouseCostsWhatItHolds()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                out OdysseyBootstrap boot);
            try
            {
                yield return null;
                Assert.That(boot.Colony, Is.Not.Null, "the bootstrap never built a colony");
                var colony = boot.Colony!;
                Assert.That(colony.Pawns.StorageUnits, Is.Not.Null, "the session has no built stores");

                // **Whether the art resolved, not whether there is a catalogue** — CLAUDE.md, and
                // the reason FigureCapTests asks PawnFigureDirector.Enabled. Without Assets/Synty
                // every stack takes ChunkRenderer's stand-in marker path, which costs a draw call
                // and no instance, and the whole contained branch of RenderThings is never
                // reached. There is nothing to measure there, and the instance control at the foot
                // of this test would fail for the one reason that is not a regression — which is
                // what turned the runner red on 2026-09-21 at 34,827 instances against 35,027.
                if (!boot.Renderer!.ItemArtResolved(Odyssey.Sim.Pawns.ItemIndex.Wood))
                    Assert.Ignore("wood resolved to no art (no Assets/Synty), so every stack " +
                                  "draws as the stand-in marker and the shelf draw path is " +
                                  "never reached");

                ForbidWhatIsLying(colony);

                float bare = 0f;
                yield return TimeFrames("warehouse/none", boot, WarmupFrames, x => bare = x);
                int bareInstances = boot.Renderer!.InstancesDrawn;

                // The shelves first, empty, so the piles are put down on cells no shelf will want.
                var shelves = new List<int>();
                RaiseShelves(colony, WarehouseShelves, shelves);
                Assert.That(shelves.Count, Is.EqualTo(WarehouseShelves),
                    $"only {shelves.Count} shelves found room near the start");

                var stacks = new List<ThingId>();
                SpawnPiles(colony, WarehouseShelves * ShelfShape.Slots, shelves, stacks);
                Assert.That(stacks.Count, Is.EqualTo(WarehouseShelves * ShelfShape.Slots),
                    $"only {stacks.Count} piles found ground near the start");
                colony.World.Tick();

                float floor = 0f;
                yield return TimeFrames("warehouse/floor", boot, WarmupFrames, x => floor = x);
                int floorInstances = boot.Renderer!.InstancesDrawn;
                int floorCalls = boot.Renderer!.DrawCalls;

                int shelved = Shelve(colony, shelves, stacks);
                Assert.That(shelved, Is.EqualTo(stacks.Count), "not every pile fitted on a shelf");
                colony.World.Tick();

                float onShelves = 0f;
                yield return TimeFrames("warehouse/shelved", boot, WarmupFrames, x => onShelves = x);
                int shelvedInstances = boot.Renderer!.InstancesDrawn;
                int shelvedCalls = boot.Renderer!.DrawCalls;

                Debug.Log($"[FrameTime] warehouse of {shelves.Count} shelves, {stacks.Count} stacks: " +
                          $"bare {bare:0.00} ms, on the floor {floor:0.00} ms (+{floor - bare:0.00}), " +
                          $"on shelves {onShelves:0.00} ms (+{onShelves - bare:0.00}); " +
                          $"shelf path over floor path {onShelves - floor:0.00} ms; " +
                          $"instances {bareInstances} -> {floorInstances} -> {shelvedInstances}, " +
                          $"draw calls {floorCalls} -> {shelvedCalls}");

                // The goods were drawn, or this measured an empty warehouse. Each stack is at
                // least one instance whether it lies on the floor or stands on a deck.
                Assert.That(shelvedInstances, Is.GreaterThanOrEqualTo(bareInstances + stacks.Count),
                    "the shelved goods were not drawn, so the shelf path was not measured");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>A warehouse at the size the design document reasons about (§8b).</summary>
        const int WarehouseShelves = 40;

        /// <summary>The player's own veto, so the colony underneath leaves the fixture alone.</summary>
        static void ForbidWhatIsLying(Odyssey.Sim.Pawns.ColonyWorld colony)
        {
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned) items[i].Forbidden = true;
        }

        /// <summary>Cells on the start's layer, nearest first, in the order a spiral visits them.</summary>
        static IEnumerable<int> AroundTheStart(Odyssey.Sim.Pawns.ColonyWorld colony, int maxRadius)
        {
            CellRef start = colony.Start;
            GridSize size = colony.Grid.Size;
            for (int radius = 1; radius < maxRadius; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (Math.Abs(dx) != radius && Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!size.Contains(x, z, start.Y)) continue;
                yield return size.Index(x, z, start.Y);
            }
        }

        /// <summary>Raise this many wooden shelves, finished, on the nearest cells that allow one.</summary>
        static void RaiseShelves(Odyssey.Sim.Pawns.ColonyWorld colony, int wanted, List<int> shelves)
        {
            GridSize size = colony.Grid.Size;
            foreach (int cell in AroundTheStart(colony, 30))
            {
                if (shelves.Count >= wanted) break;
                if (!colony.Construction.Allows(cell, BuildingHandle.Shelf)) continue;
                if (colony.Construction.Place(size.FromIndex(cell), BuildingHandle.Shelf, StuffHandle.Wood)
                    != IntentRejection.None) continue;
                colony.Construction.Raise(colony.Pawns, cell);
                shelves.Add(cell);
            }
            colony.RebuildDerived();
        }

        /// <summary>Full stacks of wood on the nearest open ground that is not a shelf, forbidden.</summary>
        static void SpawnPiles(Odyssey.Sim.Pawns.ColonyWorld colony, int wanted, List<int> shelves,
            List<ThingId> stacks)
        {
            var items = colony.Pawns.Items;
            int wood = Odyssey.Sim.Pawns.ItemIndex.Wood;
            int full = colony.Pawns.Content.Items[wood].stackLimit;
            foreach (int cell in AroundTheStart(colony, 40))
            {
                if (stacks.Count >= wanted) break;
                if (shelves.Contains(cell)) continue;
                if (colony.Grid.Edifice[cell] >= 0) continue;
                if (!colony.Pawns.Cells.IsWalkable(cell)) continue;
                if (!items.CellHasSpace(cell)) continue;

                ThingId id = items.Spawn(wood, cell, full);
                items.Get(id)!.Forbidden = true;
                stacks.Add(id);
            }
        }

        /// <summary>Move the piles on to the shelves, eight to a shelf. Returns how many went.</summary>
        static int Shelve(Odyssey.Sim.Pawns.ColonyWorld colony, List<int> shelves, List<ThingId> stacks)
        {
            var units = colony.Pawns.StorageUnits!;
            var items = colony.Pawns.Items;
            int put = 0;
            for (int i = 0; i < stacks.Count; i++)
            {
                Odyssey.Sim.Storage.StorageUnit? unit = units.AtCell(shelves[i / ShelfShape.Slots]);
                Odyssey.Sim.Pawns.ColonyItem? item = items.Get(stacks[i]);
                if (unit == null || item == null || item.Despawned) continue;
                if (!units.HasSpaceFor(unit, item.DefIndex, item.Stack)) continue;
                units.PutIn(unit, item);
                put++;
            }
            return put;
        }

        /// <summary>How many cells the order case marks. A wood is hundreds; this is the round
        /// number above it, and it is the same order of magnitude as the field's 2,000 so the two
        /// can be read against each other.</summary>
        const int OrderCells = 1_000;

        /// <summary>
        /// How long the trace arm will wait for its rows before giving up, in frames.
        ///
        /// <para>Generous: a row is a second of wall clock and the arm wants two, so on a machine
        /// drawing this world at four hundred frames a second that is about eight hundred frames.
        /// The cap exists so a tracer that has stopped fails the test in seconds rather than
        /// hanging the tier.</para>
        /// </summary>
        const int MaxFramesWaitingForRows = 5_000;

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
        /// <summary>
        /// What the hair and beards actually cost, against the same colony with them switched off
        /// (<c>docs/design/29-modular-colonists.md</c> §13).
        ///
        /// <para><b>Two colony sizes, because they exercise different code.</b> At 64 everyone is a
        /// live figure and the cost is two extra rigid renderers each — which is the case a real
        /// colony is in, since the figure cap is 64 and the audit's scale target is fifty. At 192
        /// everyone past the cap is in the baked far form instead, where the cost is instanced
        /// buckets keyed on the piece rather than the person.</para>
        ///
        /// <para><b>On and off in the same run, twice each, alternating.</b> §6c.1: this machine's
        /// frame numbers drift by more between runs than most passes cost — the city canary moved
        /// from 2.01 to 4.01 ms in an afternoon on what a sibling worktree was doing. A number from
        /// a different run is not a control. Alternating catches a drift that happens to fall
        /// between the two halves.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheAttachmentsCostWhatTheyDraw()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                out OdysseyBootstrap boot);
            try
            {
                yield return null;
                Assert.That(boot.World, Is.Not.Null, "the bootstrap never built a world");

                foreach (int size in new[] { 64, 192 })
                {
                    yield return GrowColonyTo(boot, size);
                    int pawns = boot.World!.Views.Current.Pawns.Length;

                    float onA = 0f, offA = 0f, onB = 0f, offB = 0f;

                    ColonistAttachments.Enabled = true;
                    yield return TimeFrames($"attach/{pawns}/on", boot, 30, x => onA = x);
                    ColonistAttachments.Enabled = false;
                    yield return TimeFrames($"attach/{pawns}/off", boot, 30, x => offA = x);
                    ColonistAttachments.Enabled = true;
                    yield return TimeFrames($"attach/{pawns}/on", boot, 30, x => onB = x);
                    ColonistAttachments.Enabled = false;
                    yield return TimeFrames($"attach/{pawns}/off", boot, 30, x => offB = x);
                    ColonistAttachments.Enabled = true;

                    float on = (onA + onB) * 0.5f;
                    float off = (offA + offB) * 0.5f;
                    Debug.Log($"[FrameTime] attachments at {pawns} pawns, " +
                              $"{boot.Figures?.FigureCount ?? 0} figures: " +
                              $"on {on:0.000} ms (runs {onA:0.000}/{onB:0.000}), " +
                              $"off {off:0.000} ms (runs {offA:0.000}/{offB:0.000}), " +
                              $"cost {on - off:+0.000;-0.000} ms");
                }
            }
            finally
            {
                ColonistAttachments.Enabled = true;
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>
        /// What the crowd scan costs, and how much of it was the quadratic rather than the
        /// constant factor under it (<c>docs/design/25-pawn-steering.md</c>, "Making the scan
        /// stop walking the colony").
        ///
        /// <para><b>Three arms, because the plan warned against two.</b>
        /// <c>docs/plans/pf-crowd-scan.md</c> named hoisting <c>WhereItIsNow</c> out of the inner
        /// loop as the cheap candidate and said to measure it alone before building a spatial
        /// index on top of an unmeasured constant factor. <c>CrowdScan.Cached</c> is exactly that
        /// hoist and nothing else; <c>Bucketed</c> adds the cull. Measuring all three in one run
        /// is the only way to say which of the two bought the frame back.</para>
        ///
        /// <para><b>Alternating, twice each, in one run</b> — the shape
        /// <c>TheAttachmentsCostWhatTheyDraw</c> established. This machine's frame numbers drift
        /// by more between runs than most passes cost, so a reading from another run is not a
        /// control, and a drift that happens to land between two halves would otherwise be read
        /// as the pass.</para>
        ///
        /// <para><b>Three colony sizes spanning the figure ceiling.</b> At 64 everybody is a live
        /// figure, so <c>Actors</c> scans nobody and whatever <c>Figures</c> costs is the 64 x N
        /// linear scan on its own — which is the open question the plan asked to answer on the
        /// way. At 192 and 384 the quadratic term is what is being measured.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheCrowdScanCostsWhatItVisits()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                out OdysseyBootstrap boot);
            try
            {
                yield return null;
                Assert.That(boot.World, Is.Not.Null, "the bootstrap never built a world");

                foreach (int size in new[] { 64, 192, 384 })
                {
                    yield return GrowColonyTo(boot, size);
                    int pawns = boot.World!.Views.Current.Pawns.Length;

                    foreach (CrowdScan mode in new[] { CrowdScan.Span, CrowdScan.Cached, CrowdScan.Bucketed })
                    {
                        float a = 0f, b = 0f;
                        double[] splitA = System.Array.Empty<double>();
                        double[] splitB = System.Array.Empty<double>();

                        PawnCrowdIndex.Mode = mode;
                        yield return TimeFrames($"crowd/{pawns}/{mode}", boot, 30,
                            x => a = x, s => splitA = s);
                        yield return TimeFrames($"crowd/{pawns}/{mode}", boot, 30,
                            x => b = x, s => splitB = s);

                        double Section(OdysseyBootstrap.FrameSection section) =>
                            (splitA[(int)section] + splitB[(int)section]) * 0.5;

                        Debug.Log($"[FrameTime] crowd {mode} at {pawns} pawns, " +
                                  $"{boot.Figures?.FigureCount ?? 0} figures: " +
                                  $"frame {(a + b) * 0.5f:0.000} ms (runs {a:0.000}/{b:0.000}), " +
                                  $"Figures {Section(OdysseyBootstrap.FrameSection.Figures):0.000} ms, " +
                                  $"Actors {Section(OdysseyBootstrap.FrameSection.Actors):0.000} ms, " +
                                  $"Crowd {Section(OdysseyBootstrap.FrameSection.Crowd):0.000} ms, " +
                                  $"{boot.Renderer?.DrawCalls ?? 0} draw calls");
                    }
                }
            }
            finally
            {
                PawnCrowdIndex.Mode = CrowdScan.Bucketed;
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>
        /// What the aspect lookup costs, against the scan it replaced, in one run.
        ///
        /// <para><b>The second O(N squared) found by fixing the first</b>
        /// (<c>docs/design/31-aspect-lookup.md</c>, out of <c>25-pawn-steering.md</c> §9d). A
        /// colonist publishes 57 aspect rows every tick, so the published set is 57 × colonists;
        /// <c>TryGetPawnAspect</c> scanned it, and the far-form renderer called it once per
        /// colonist it drew. <c>WorldSnapshot.IndexAspects</c> is the control — false is the scan,
        /// true is the lazy index — and <c>AspectScaleTests.TheIndexAndTheScanAgreeRowForRow</c>
        /// is what makes it a control rather than two games.</para>
        ///
        /// <para>Alternating, twice each, at three colony sizes spanning the figure ceiling — the
        /// shape <c>TheAttachmentsCostWhatTheyDraw</c> established, because this machine drifts by
        /// more between runs than most passes cost.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheAspectLookupCostsWhatItScans()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                out OdysseyBootstrap boot);
            try
            {
                yield return null;
                Assert.That(boot.World, Is.Not.Null, "the bootstrap never built a world");

                foreach (int size in new[] { 64, 192, 384 })
                {
                    yield return GrowColonyTo(boot, size);
                    int pawns = boot.World!.Views.Current.Pawns.Length;
                    int rows = boot.World!.Views.Current.AspectCount;

                    float scanA = 0f, indexA = 0f, scanB = 0f, indexB = 0f;
                    double[] scanSplit = System.Array.Empty<double>();
                    double[] indexSplit = System.Array.Empty<double>();

                    WorldSnapshot.IndexAspects = false;
                    yield return TimeFrames($"aspect/{pawns}/scan", boot, 30, x => scanA = x, s => scanSplit = s);
                    WorldSnapshot.IndexAspects = true;
                    yield return TimeFrames($"aspect/{pawns}/index", boot, 30, x => indexA = x, s => indexSplit = s);
                    WorldSnapshot.IndexAspects = false;
                    yield return TimeFrames($"aspect/{pawns}/scan", boot, 30, x => scanB = x);
                    WorldSnapshot.IndexAspects = true;
                    yield return TimeFrames($"aspect/{pawns}/index", boot, 30, x => indexB = x);

                    double Part(double[] split, OdysseyBootstrap.FrameSection section) =>
                        split.Length > (int)section ? split[(int)section] : 0d;

                    Debug.Log($"[FrameTime] aspects at {pawns} pawns ({rows} rows, " +
                              $"{(pawns > 0 ? rows / pawns : 0)} a colonist), " +
                              $"{boot.Figures?.FigureCount ?? 0} figures: " +
                              $"scan {(scanA + scanB) * 0.5f:0.000} ms ({scanA:0.000}/{scanB:0.000}), " +
                              $"index {(indexA + indexB) * 0.5f:0.000} ms ({indexA:0.000}/{indexB:0.000}); " +
                              $"Actors {Part(scanSplit, OdysseyBootstrap.FrameSection.Actors):0.000} -> " +
                              $"{Part(indexSplit, OdysseyBootstrap.FrameSection.Actors):0.000} ms, " +
                              $"Figures {Part(scanSplit, OdysseyBootstrap.FrameSection.Figures):0.000} -> " +
                              $"{Part(indexSplit, OdysseyBootstrap.FrameSection.Figures):0.000} ms");
                }
            }
            finally
            {
                WorldSnapshot.IndexAspects = true;
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>
        /// What finding the campfires costs, against the sweep it replaced, in one run.
        ///
        /// <para><b>The owner reported the frame going from 1.5 to 4.5 ms and thought the machine
        /// might have been under load.</b> Both can be true, and only a control inside one run can
        /// separate them — this machine drifted the city canary from 2.01 to 4.01 ms in an
        /// afternoon on what a sibling worktree was doing (CLAUDE.md).</para>
        ///
        /// <para><b>The bug the control exists to price.</b> <c>FireDirector.RefreshCells</c>
        /// caches which cells hold a fire against <c>WorldRenderModel.Version</c>, and its comment
        /// claimed the board was therefore swept "once per structural change rather than once a
        /// frame". <c>RefreshDirty</c> bumps that version whenever <b>any chunk remeshes</b>, so
        /// in a colony doing anything the sweep ran most frames — 230,400 cells on the played
        /// board, to find at most a handful of fires. <c>Rescans</c> against the frame count is
        /// the tell, and it is logged.</para>
        ///
        /// <para><b>It is paid with no campfire on the board at all</b>, which is why this arm
        /// does not build one: the sweep is unconditional, so a colony that has never seen a fire
        /// was paying for looking for one.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheCampfireSweepCostsWhatItVisits()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: false,
                out OdysseyBootstrap boot);
            try
            {
                yield return null;
                Assert.That(boot.World, Is.Not.Null, "the bootstrap never built a world");
                Assert.That(boot.Fires, Is.Not.Null, "the bootstrap never built a fire director");

                int cells = boot.World!.Views.Current.Size.CellCount;

                // No fire, one fire, then eight, each measured twice with the two lookup modes
                // alternating. The zero row is the control the other two are read against, and it
                // is in the same run because this machine moves more between runs than the pass
                // costs.
                foreach (int fires in new[] { 0, 1, 8 })
                {
                    Light(boot, fires);

                    foreach (FireDirector.Find mode in new[]
                             { FireDirector.Find.Edifices, FireDirector.Find.Cells })
                    {
                        FireDirector.Mode = mode;

                        int rescansBefore = boot.Fires!.Rescans;
                        long visitsBefore = boot.Fires.RescanVisits;

                        float ms = 0f;
                        var split = System.Array.Empty<double>();
                        yield return TimeFrames($"campfire/{fires}/{mode}", boot, 30,
                            x => ms = x, s => split = s);

                        int rescans = boot.Fires.Rescans - rescansBefore;
                        long visits = boot.Fires.RescanVisits - visitsBefore;

                        Debug.Log($"[FrameTime] campfire {fires} lit ({boot.Fires.LitFires} drawn), " +
                                  $"{mode}: frame {ms:0.000} ms, " +
                                  $"{rescans} rescans over {TimedFrames} frames, " +
                                  $"{visits:N0} records visited " +
                                  $"({(rescans > 0 ? visits / rescans : 0):N0} a rescan, " +
                                  $"board {cells:N0} cells), " +
                                  $"{boot.Renderer?.DrawCalls ?? 0} draw calls");
                    }
                }
            }
            finally
            {
                FireDirector.Mode = FireDirector.Find.Edifices;
                UnityEngine.Object.Destroy(root);
            }
        }

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
                    // Draw calls beside the frame, because the colonist passes either cost draws
                    // per person or they do not, and the sweep spans the figure cap -- so the
                    // control for "what the hair and beard pass costs" is the same run at 64
                    // figures, where nobody is drawn in the far form at all
                    // (docs/design/29-modular-colonists.md section 13).
                    Debug.Log($"[FrameTime] colony {pawns} pawns, " +
                              $"{boot.Figures?.FigureCount ?? 0} figures: {mean:0.00} ms, " +
                              $"{boot.Renderer?.DrawCalls ?? 0} draw calls");
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>
        /// The frame with a fight in view (design 33, the C2/C3 integration): ten colonists at the
        /// start timed at peace, then ten marauders spawned among them and the same colony timed
        /// again once the swinging has started — one run, so the difference is the fight and not
        /// the machine (the rule in this class's other sweeps).
        ///
        /// <para>What a fight adds to a frame is the clip layer on every fighting figure, the
        /// computed poses, the health bars and markers (two or three submissions per marked pawn),
        /// the floating words and the combat event reader, and on the simulation side the swings
        /// and the chase re-plans. It asserts that a fight really was in view — combat events in
        /// the timed window, marauders on the board — because a brawl that never started reports a
        /// beautifully cheap frame; about time it asserts only the class's 30 Hz ceiling.</para>
        ///
        /// <para>The brawl is ticked once a frame by the test on top of the bootstrap's own
        /// real-time ticks. Without it the window's sim time was the machine's frame rate: 180
        /// frames at 2.5 ms here are 27 ticks and caught four swings, and at 1.2 ms on the CI
        /// runner they were about twelve and caught none (2026-09-24, PR #180). A tick of this
        /// colony is 0.005 ms, so the frame it adds is noise; a peace it lets through is not.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheFrameWithAFightInView()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                out OdysseyBootstrap boot);
            try
            {
                yield return null;
                Assert.That(boot.World, Is.Not.Null, "the bootstrap never built a world");
                Assert.That(boot.Colony, Is.Not.Null, "the bootstrap never built a colony");
                CellRef start = boot.Colony!.Start;
                int top = boot.Colony.Grid.Size.SizeY - 2;

                // Ten colonists about the start, where the camera is.
                for (int i = 0; boot.World!.Views.Current.Pawns.Length < 10 && i < 40; i++)
                {
                    boot.World.Intents.Submit(new Intent(IntentKind.SpawnPawn,
                        new CellRef(start.X - 2 + i % 5, start.Z - 1 + i / 5, top), 0));
                    boot.World.Tick();
                }
                yield return null;

                float peace = 0f;
                yield return TimeFrames("fight/peace", boot, WarmupFrames, x => peace = x);
                int peaceDraws = boot.Renderer?.DrawCalls ?? 0;

                // Ten marauders a few cells off, each hunting the nearest colonist standing.
                int before = boot.World.Views.Current.Pawns.Length;
                for (int i = 0; boot.World.Views.Current.Pawns.Length < before + 10 && i < 40; i++)
                {
                    boot.World.Intents.Submit(new Intent(IntentKind.SpawnPawn,
                        new CellRef(start.X - 2 + i % 5, start.Z + 4 + i / 5, top), 3));
                    boot.World.Tick();
                }

                // Let them close and start swinging before the clock starts.
                for (int i = 0; i < 240; i++)
                {
                    boot.World.Tick();
                    if (i % 20 == 0) yield return null;
                }

                int eventsBefore = LastCombatEvent(boot);
                long ticksBefore = boot.World.Views.Current.Tick;
                bool brawling = true;
                IEnumerator TickEachFrame()
                {
                    while (brawling)
                    {
                        boot.World!.Tick();
                        yield return null;
                    }
                }

                Coroutine ticker = boot.StartCoroutine(TickEachFrame());
                float fight = 0f;
                try
                {
                    yield return TimeFrames("fight/brawl", boot, 30, x => fight = x);
                }
                finally
                {
                    brawling = false;
                    boot.StopCoroutine(ticker);
                }
                int events = LastCombatEvent(boot) - eventsBefore;
                long windowTicks = boot.World.Views.Current.Tick - ticksBefore;

                WorldSnapshot frame = boot.World.Views.Current;
                int hostiles = Hostiles(frame);
                Debug.Log($"[FrameTime] fight: peace {peace:0.00} ms ({peaceDraws} draw calls), " +
                          $"brawl {fight:0.00} ms ({boot.Renderer?.DrawCalls ?? 0} draw calls), " +
                          $"{frame.Pawns.Length} pawns ({hostiles} hostile), {boot.Figures?.FigureCount ?? 0} figures, " +
                          $"{events} combat events in {windowTicks} ticks of the window, {frame.Corpses.Length} corpses");

                Assert.That(hostiles + frame.Corpses.Length, Is.GreaterThan(0), "no marauder was ever spawned");
                Assert.That(events, Is.GreaterThan(0), "nothing fought in the timed window: this timed a peace");
                Assert.That(fight, Is.LessThan(CeilingMs), "a fight of ten against ten takes longer than a 30 Hz frame");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>How many hostiles the frame holds. Its own method: a span cannot live in an iterator.</summary>
        static int Hostiles(WorldSnapshot frame)
        {
            int count = 0;
            foreach (PawnView pawn in frame.Pawns) if (pawn.IsHostile) count++;
            return count;
        }

        /// <summary>The id of the newest combat moment in the frame, 0 before the first.</summary>
        static int LastCombatEvent(OdysseyBootstrap boot)
        {
            ReadOnlySpan<CombatEventView> events = boot.World!.Views.Current.CombatEvents;
            return events.Length > 0 ? events[events.Length - 1].Id : 0;
        }

        /// <summary>
        /// Spawn colonists until the colony is this big, spread over the middle of the board so
        /// they do not all arrive in one column and stand on each other.
        /// </summary>

        /// <summary>
        /// Put <paramref name="wanted"/> campfires on the board, near the middle where the camera
        /// is, raised directly rather than ordered — a site has to be walked to and built, and
        /// this arm is measuring the drawn fire rather than the colony's willingness to make one.
        /// </summary>
        static void Light(OdysseyBootstrap boot, int wanted)
        {
            GridSize size = boot.Colony!.Grid.Size;
            var ctx = boot.Colony.Pawns;

            int placed = 0;
            for (int i = 0; i < wanted * 40 && placed < wanted; i++)
            {
                int x = size.SizeX / 2 + (i % 8) * 2;
                int z = size.SizeZ / 2 + (i / 8) * 2;
                if (x >= size.SizeX - 1 || z >= size.SizeZ - 1) break;

                int cell = boot.Colony.Grid.NearestWalkableInColumn(x, z, size.SizeY - 2);
                if (cell < 0) continue;

                // Place THEN raise: RaiseWhenClear returns at once unless a site for that
                // building is already queued at the cell, which Place is what queues.
                if (boot.Colony.Construction.Place(size.FromIndex(cell),
                        BuildingHandle.Campfire, StuffHandle.Wood) != IntentRejection.None) continue;

                boot.Colony.Construction.RaiseWhenClear(ctx, cell, BuildingHandle.Campfire);
                placed++;
            }

            // A tick to let the raise reach the mirror, and a frame to let the director see it.
            boot.World!.Tick();
        }

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

        /// <summary>
        /// What frustum culling is worth, measured with a control inside one run, on the board
        /// where it matters.
        ///
        /// <para><b>The same world timed twice, seconds apart</b>, with
        /// <c>ChunkRenderer.CullToFrustum</c> the only thing that changes between the readings —
        /// the shape <c>TheMarkPassCostsWhatItSubmits</c> established, and the only comparison a
        /// machine running several editors supports. The off arm also reports
        /// <c>ChunksOutsideFrustum</c>, so the saving can be predicted from the chunk count and
        /// then checked against the clock rather than inferred from it.</para>
        ///
        /// <para>Huge, because that is where the cost is: the board is 600 m across and the camera
        /// reaches 160 m, so most of what the band admits cannot be on screen. On Standard the
        /// whole board is nearly in view and the honest expectation is that this buys little —
        /// which is the point, and why the standard reading is taken too.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator WhatFrustumCullingIsWorth()
        {
            foreach ((string label, int x, int z, int y) in new[]
                     { ("standard", 120, 120, 16), ("huge", 240, 240, 16) })
            {
                GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: false,
                    out OdysseyBootstrap boot, x, z, y);
                try
                {
                    yield return SeedOrders(boot);

                    float margin = boot.Renderer!.ShadowCasterMarginMetres;

                    boot.Renderer!.CullToFrustum = false;
                    float off = 0f;
                    yield return TimeFrames($"cull/{label}/off", boot, WarmupFrames, m => off = m);

                    int drawn = boot.Renderer!.ChunksDrawn;
                    int outside = boot.Renderer!.ChunksOutsideFrustum;
                    int callsOff = boot.Renderer!.DrawCalls;

                    // As it would actually ship: the margin keeps off-screen shadow casters.
                    boot.Renderer!.CullToFrustum = true;
                    float on = 0f;
                    yield return TimeFrames($"cull/{label}/on", boot, WarmupFrames, m => on = m);
                    int callsOn = boot.Renderer!.DrawCalls;

                    // What the top shadow rung costs, which is the player-facing version of the
                    // same question: a longer shadow distance means a wider margin, so fewer
                    // chunks are culled AND more of them cast. Driven through the setting rather
                    // than through the renderer, because the bootstrap re-derives the margin from
                    // QualitySettings every frame — an earlier version of this arm set the margin
                    // directly, was silently overwritten, and reported two identical readings with
                    // the same draw-call count as though they were a comparison.
                    float wasShadowDistance = QualitySettings.shadowDistance;
                    float farShadows = 0f;
                    int callsFarShadows;
                    int outsideFarShadows;
                    try
                    {
                        QualitySettings.shadowDistance = 120f;   // the top rung the settings offer
                        yield return TimeFrames($"cull/{label}/shadows120", boot, WarmupFrames, m => farShadows = m);
                        callsFarShadows = boot.Renderer!.DrawCalls;
                        outsideFarShadows = boot.Renderer!.ChunksOutsideFrustum;
                    }
                    finally
                    {
                        QualitySettings.shadowDistance = wasShadowDistance;
                    }

                    Debug.Log($"[FrameTime] cull {label} {x}x{z}x{y}: " +
                              $"{outside} of {drawn} chunks outside the frustum " +
                              $"({(drawn > 0 ? 100f * outside / drawn : 0f):0.0}%) at a {margin:0} m " +
                              $"shadow margin; frame {off:0.00} -> {on:0.00} ms " +
                              $"(saves {off - on:0.00}); draw calls {callsOff} -> {callsOn}. " +
                              $"At a 120 m shadow distance {farShadows:0.00} ms, " +
                              $"{outsideFarShadows} culled, {callsFarShadows} calls");

                    Assert.That(margin, Is.GreaterThan(0f),
                        "the shadow margin is zero, so this arm measured a cull that would drop " +
                        "off-screen shadow casters and is not the one that would ship");

                    // The control. If the test never rejected a chunk it measured the same thing
                    // twice and the difference is this machine's mood, not the cull.
                    Assert.That(outside, Is.GreaterThan(0),
                        $"{label}: no chunk was outside the frustum, so the two readings are the " +
                        "same submission and the comparison is meaningless");
                    Assert.That(callsOn, Is.LessThan(callsOff),
                        $"{label}: culling did not reduce draw calls, so CullToFrustum is not " +
                        "reaching the submission path");
                }
                finally
                {
                    UnityEngine.Object.Destroy(root);
                }

                yield return null;
                GC.Collect();
                yield return null;
            }
        }

        /// <summary>
        /// Culling changes what is submitted and not what is seen — proved against pixels, with
        /// **two** controls: one that must show a difference, and one that must not.
        ///
        /// <para><b>Why this test has to exist.</b> The saving is enormous — most of a Huge
        /// board's chunks are outside the frustum — and a broken frustum that rejected everything
        /// would report exactly the same triumph. Nothing else here looks at the picture:
        /// <c>FrameTimeTests</c> times frames, the Unity tier asserts no pixels, and the fault
        /// this guards against is invisible in a still and only shows as shadows and geometry
        /// popping at the screen edge while panning.</para>
        ///
        /// <para><b>Its first two versions both proved nothing, and the reasons are the whole
        /// value of this comment.</b></para>
        ///
        /// <para><i>One — the positive control did not apply.</i> "A frustum admitting nothing"
        /// was imposed by assigning <c>ChunkRenderer.Frustum</c>, which the composition root
        /// rewrites every frame, so the blind shot was simply a second copy of the culled one.
        /// Run on 2026-09-23, the two reported <b>identical</b> counts — 126 chunks, 57,818
        /// instances, 1,744 calls — where the blind one should have submitted nothing whatever.
        /// It now goes through <c>ChunkRenderer.FrustumOverride</c>, which the root does not
        /// touch. <b>This is the second time in this one file that a test set a field the root
        /// re-derives per frame</b>; the first was <c>ShadowCasterMarginMetres</c>, and both are
        /// <c>P18</c> in <c>docs/bug-patterns.md</c>.</para>
        ///
        /// <para><i>Two — the scene was moving underneath it.</i> The shots were taken seconds
        /// apart on a live colony, so colonists walked and the light drifted between them, and
        /// <b>2 to 3 per cent of pixels moved whatever was being compared</b>. Culling's own
        /// difference is supposed to be nought, and it was being asked to stand out against a
        /// noise floor several times its own size. The world is paused for the captures now, and
        /// the noise floor is no longer assumed — the test takes a <i>repeat</i> of the identical
        /// configuration and asserts on that too.</para>
        ///
        /// <para>So: <b>repeat</b> must match (the instrument is quiet), <b>blind</b> must not
        /// (the instrument can see), and only then does <b>culled</b> matching mean anything.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator CullingDoesNotChangeThePicture()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: false,
                out OdysseyBootstrap boot, 240, 240, 16);
            var target = new RenderTexture(320, 240, 24) { name = "cull-proof" };
            try
            {
                yield return SeedOrders(boot);

                // **Stop the world before photographing it.** A walking colonist and a drifting
                // sun move more pixels than the thing being measured; see the remarks above.
                // **Submitted until it takes, not submitted once and hoped for.** An intent goes
                // on a bus with a capacity and is drained on a tick boundary, and the thousand
                // designations `SeedOrders` has just queued can still be going through. Submitted
                // once and waited four frames, this passed on this machine and **failed on the CI
                // runner**, where the speed was still 1 — a machine-dependent flake in a test whose
                // whole job is to be believed.
                for (int i = 0; i < 120 && boot.World!.GameSpeed != 0; i++)
                {
                    boot.World!.Intents.Submit(new Intent(IntentKind.SetGameSpeed, default, 0));
                    yield return null;
                }

                Assert.That(boot.World!.GameSpeed, Is.Zero, "the world would not pause, so the " +
                    "shots below are of a moving scene and cannot measure a still difference");

                // **And stop the clock presentation draws on, which pausing the simulation does
                // not.** Pausing stops the ticks, so nobody walks — but the water still scrolls
                // its streaks and foam, the figures still advance their animation graphs, and the
                // daylight rig still moves, because all of those run on `Time.deltaTime` and the
                // shaders on `_Time`. Measured: with the simulation paused and this left alone,
                // two captures of the identical configuration still differed by **1.29%** of
                // pixels, which is most of the way to culling's own 2.13% and made the two
                // impossible to tell apart. `timeScale` is what `_Time` is derived from, so this
                // one line stills the shaders as well as the scripts.
                float previousScale = Time.timeScale;
                Time.timeScale = 0f;

                // **Let it settle before the first shot, generously.** Run on its own the floor
                // below is 0.00%; run inside the whole PlayMode tier it was 0.77%, on the same
                // commit. A busy run is still finishing things off — shader variants, texture
                // streaming, the post stack's first frames — and eight frames between captures is
                // not enough for that to be over. This wait is once, before anything is compared.
                for (int i = 0; i < 120; i++) yield return null;

                UnityEngine.Camera cam = boot.cameraRig!.Camera;
                RenderTexture previousTarget = cam.targetTexture;
                cam.targetTexture = target;
                try
                {
                    boot.Renderer!.CullToFrustum = false;
                    Color32[] off = null!;
                    yield return Shoot("off", boot, target, p => off = p);

                    // The negative control: the same configuration again. Whatever this moves is
                    // the instrument's own noise, and every other number is read against it.
                    Color32[] again = null!;
                    yield return Shoot("again", boot, target, p => again = p);

                    boot.Renderer!.CullToFrustum = true;
                    Color32[] on = null!;
                    yield return Shoot("on", boot, target, p => on = p);

                    // The positive control: a frustum nothing can be inside, through the seam the
                    // root does not overwrite.
                    var nowhere = new Plane[6];
                    for (int i = 0; i < nowhere.Length; i++)
                        nowhere[i] = new Plane(Vector3.up, -1e6f);
                    boot.Renderer!.FrustumOverride = nowhere;
                    Color32[] blind = null!;
                    yield return Shoot("blind", boot, target, p => blind = p);
                    boot.Renderer!.FrustumOverride = null;

                    float noise = Difference(off, again);
                    float culled = Difference(off, on);
                    float blinded = Difference(off, blind);
                    Debug.Log($"[FrameTime] cull proof: the same shot twice moved {noise * 100f:0.00}%, " +
                              $"culling moved {culled * 100f:0.00}%, " +
                              $"a frustum admitting nothing moved {blinded * 100f:0.00}%");

                    // **The floor has to be small enough to conclude anything from**, but it is
                    // not required to be nought: see the settle above. Two per cent still leaves
                    // the blind control fifty times clear of it.
                    Assert.That(noise, Is.LessThan(0.02f),
                        $"two captures of the identical configuration differ by {noise * 100f:0.00}% " +
                        "of pixels, so this comparison has no floor to measure against. The world " +
                        "is meant to be paused and the clock stopped for these shots - check " +
                        "Logs/cull-off.png against Logs/cull-again.png for a colonist who moved, " +
                        "water that scrolled or a sun that drifted");

                    Assert.That(blinded, Is.GreaterThan(0.05f),
                        $"rejecting every chunk moved only {blinded * 100f:0.00}% of pixels, so this " +
                        "comparison cannot see a difference and its other assertion proves " +
                        "nothing. Compare the chunk counts logged during each capture: equal counts " +
                        "for 'on' and 'blind' mean the override is not reaching the submission " +
                        "path, which is exactly how this test failed on 2026-09-23");

                    // **Judged against the floor measured in this same run, not against a constant.**
                    // The claim is that culling is indistinguishable from doing nothing, and the
                    // repeat shot is precisely what "doing nothing" costs on this machine, in this
                    // run, at this moment. A fixed tolerance would be a guess at that, and would
                    // either fail honestly-quiet runs or pass noisy ones - it did the first of
                    // those inside the full tier on 2026-09-23 while passing alone.
                    float allowed = noise + 0.002f;
                    Assert.That(culled, Is.LessThanOrEqualTo(allowed),
                        $"culling moved {culled * 100f:0.00}% of pixels where doing nothing twice " +
                        $"moved {noise * 100f:0.00}%: it is not only skipping submissions the " +
                        "camera could not see. The blind control moved " +
                        $"{blinded * 100f:0.00}%, so the instrument can certainly see a real change");

                    // **And again under a low sun** (design 38 §18). The shadow margin sweeps
                    // towards the sun, and a sun near the horizon throws the longest shadows the
                    // day has, from the furthest casters — the case a sweep that was too short
                    // would get wrong. Evening, about nine degrees up, held through the root's
                    // seam because the root re-applies the hour every frame (P18).
                    boot.DaylightHourOverride = 19.5f;
                    for (int i = 0; i < 60; i++) yield return null;
                    boot.Renderer!.CullToFrustum = false;
                    Color32[] lowOff = null!, lowAgain = null!, lowOn = null!;
                    yield return Shoot("low-off", boot, target, p => lowOff = p);
                    int lowShellChunks = boot.Renderer!.ChunksDrawn;
                    yield return Shoot("low-again", boot, target, p => lowAgain = p);
                    boot.Renderer!.CullToFrustum = true;
                    yield return Shoot("low-on", boot, target, p => lowOn = p);
                    int lowSweptChunks = boot.Renderer!.ChunksDrawn;
                    boot.DaylightHourOverride = null;

                    float lowNoise = Difference(lowOff, lowAgain);
                    float lowCulled = Difference(lowOff, lowOn);
                    Debug.Log($"[FrameTime] cull proof, low sun (19.5 h): the same shot twice moved " +
                              $"{lowNoise * 100f:0.00}%, culling moved {lowCulled * 100f:0.00}%; " +
                              $"{lowShellChunks} chunks unculled, {lowSweptChunks} culled");
                    Assert.That(lowNoise, Is.LessThan(0.02f), "the low-sun shots have no floor to measure against");
                    Assert.That(lowCulled, Is.LessThanOrEqualTo(lowNoise + 0.002f),
                        $"under a low sun culling moved {lowCulled * 100f:0.00}% of pixels against a floor of " +
                        $"{lowNoise * 100f:0.00}%: the sun-ward sweep is dropping a caster whose long shadow " +
                        "reaches the view");
                }
                finally
                {
                    Time.timeScale = previousScale;
                    cam.targetTexture = previousTarget;
                    boot.Renderer!.FrustumOverride = null;
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
        }

        /// <summary>
        /// Let the normal loop draw into the target, read it back, and say what the renderer did
        /// while it was drawing.
        ///
        /// <para><b>The counters and the file are why this is not a guessing game.</b> The first
        /// run of <see cref="CullingDoesNotChangeThePicture"/> failed on its own control: a
        /// frustum admitting nothing moved 3.22% of pixels, which is not a difference between two
        /// pictures of a world — it is what two pictures of *nearly nothing* look like. Chunk
        /// counts taken during the capture separate "the cull is wrong" from "the capture never
        /// saw the board", and the written frame lets a person settle it in one look, which is
        /// what this project does with anything that is about how something appears.</para>
        /// </summary>
        IEnumerator Shoot(string name, OdysseyBootstrap boot, RenderTexture target, Action<Color32[]> pixels)
        {
            // Several frames: the submission is rebuilt every frame and the post stack settles.
            for (int i = 0; i < 8; i++) yield return null;

            int chunks = boot.Renderer?.ChunksDrawn ?? -1;
            int instances = boot.Renderer?.InstancesDrawn ?? -1;
            int calls = boot.Renderer?.DrawCalls ?? -1;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            Color32[] read = image.GetPixels32();
            long sum = 0;
            for (int i = 0; i < read.Length; i++) sum += read[i].r + read[i].g + read[i].b;

            Directory.CreateDirectory(Path.GetFullPath("Logs"));
            File.WriteAllBytes(Path.GetFullPath($"Logs/cull-{name}.png"), image.EncodeToPNG());
            Debug.Log($"[FrameTime] cull shot {name}: {chunks} chunks, {instances} instances, " +
                      $"{calls} calls while capturing; mean channel " +
                      $"{(read.Length > 0 ? sum / (double)(read.Length * 3) : 0):0.0} " +
                      $"-> Logs/cull-{name}.png");

            pixels(read);
            UnityEngine.Object.Destroy(image);
        }

        /// <summary>The fraction of pixels that differ by more than a channel of noise.</summary>
        static float Difference(Color32[] a, Color32[] b)
        {
            if (a.Length != b.Length) return 1f;
            int moved = 0;
            for (int i = 0; i < a.Length; i++)
            {
                int dr = Mathf.Abs(a[i].r - b[i].r);
                int dg = Mathf.Abs(a[i].g - b[i].g);
                int db = Mathf.Abs(a[i].b - b[i].b);
                if (dr + dg + db > 12) moved++;
            }

            return a.Length == 0 ? 0f : (float)moved / a.Length;
        }

        static double Section(double[] split, OdysseyBootstrap.FrameSection section) =>
            split.Length > (int)section ? split[(int)section] : 0d;

        /// <summary>
        /// What the meshing budget is worth, measured against itself on one board in one run.
        ///
        /// <para><b>This is the negative control for §6c.7.</b> The same world is made to re-mesh
        /// twice — once with the budget off, once with it on — and the worst frame of each is
        /// quoted. No absolute threshold, because this machine runs several editors at once and a
        /// frame number is only comparable with one taken in the same run (§6c); what is asserted
        /// is the difference, which is the thing the unit claims.</para>
        ///
        /// <para><c>Model.Remesh()</c> is exactly what a graphics toggle does, and §6c.6 caught it
        /// happening sixty-one times in a two-minute player session, each one meshing 900 chunks in
        /// a single frame for about 165 ms.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheMeshBudgetKeepsAWholeBoardRemeshOutOfOneFrame()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: false,
                out OdysseyBootstrap boot, 240, 240, 16);
            try
            {
                for (int i = 0; i < WarmupFrames; i++) yield return null;

                ChunkRenderer renderer = boot.Renderer!;
                int budget = renderer.MeshBudgetPerFrame;
                Assert.That(budget, Is.GreaterThan(0), "the shipped budget is off, so this proves nothing");

                float unbudgeted = 0f, budgeted = 0f;
                int unbudgetedChunks = 0, budgetedChunks = 0;

                // The fault, reproduced: no budget, one Remesh, the whole board in one frame.
                renderer.MeshBudgetPerFrame = 0;
                boot.Model!.Remesh();
                yield return null;
                unbudgeted = Time.unscaledDeltaTime * 1000f;
                unbudgetedChunks = renderer.ChunksMeshedThisFrame;

                for (int i = 0; i < 30; i++) yield return null;

                // And the fix, on the same board, the same Remesh, in the same run.
                renderer.MeshBudgetPerFrame = budget;
                boot.Model!.Remesh();
                yield return null;
                budgeted = Time.unscaledDeltaTime * 1000f;
                budgetedChunks = renderer.ChunksMeshedThisFrame;

                Debug.Log($"[FrameTime] mesh budget: unbudgeted {unbudgeted:0.0} ms " +
                          $"({unbudgetedChunks} chunks), budgeted {budgeted:0.0} ms " +
                          $"({budgetedChunks} chunks, cap {budget}); " +
                          $"{renderer.ChunksMeshDeferred} deferred to later frames");

                Assert.That(unbudgetedChunks, Is.GreaterThan(budget * 4),
                    "the unbudgeted pass meshed too little to be the fault this guards against");
                Assert.That(budgetedChunks, Is.LessThanOrEqualTo(budget),
                    "the budget did not hold on a real board");
                Assert.That(renderer.ChunksMeshDeferred, Is.GreaterThan(0),
                    "nothing was deferred, so the budget never actually bit");

                // The measurement the unit exists for. Half is a wide margin on purpose — the
                // point is the class of change, not a tuned ratio.
                Assert.That(budgeted, Is.LessThan(unbudgeted * 0.5d),
                    $"the budgeted re-mesh cost {budgeted:0.0} ms against {unbudgeted:0.0} ms " +
                    "unbudgeted, so spreading the work bought nothing measurable");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>
        /// The performance trace agrees with the arm that timed the same frames.
        ///
        /// <para><b>This test exists because of what happened on 2026-09-21.</b> A
        /// <c>CpuFrameMs</c> field was added to the developer overlay, looked entirely plausible in
        /// a batch run at 640 x 480, and was wrong on screen in its first real session — 16.81 ms,
        /// then 296.32, then 17,898.04 — with nothing in the project able to tell. The lesson
        /// written down at the time was that <b>a number the platform hands you is not a
        /// measurement until it has been seen beside a number taken independently</b>. This is that
        /// sentence as a test: the trace and <see cref="TimeFrames"/> watch the same frames through
        /// different clocks, and their answers have to meet.
        /// </para>
        ///
        /// <para><b>The band is deliberately wide, and a narrow one would be wrong.</b> One figure
        /// is a mean over 180 frames and the other a median of per-second medians; they are not the
        /// same statistic and are not meant to be equal. What is being caught is a tracer reading a
        /// different quantity, a different unit, or nothing at all — and a factor of two either way
        /// catches every one of those while surviving a machine running three editors, which this
        /// one does.</para>
        ///
        /// <para>It also proves the parts with no other proof: a file written where
        /// <c>PerfTraceFiles</c> says, a header describing this session rather than a default, and
        /// a marker that lands.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheTraceAgreesWithTheArmThatTimedIt()
        {
            bool tracing = OdysseyBootstrap.TraceEnabled;
            OdysseyBootstrap.TraceEnabled = true;

            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: false,
                out OdysseyBootstrap boot);

            float armMean = 0f;
            string? path = null;
            int rows = 0;
            try
            {
                yield return TimeFrames("trace/agreement", boot, WarmupFrames, m => armMean = m);

                Odyssey.Presentation.Diagnostics.PerfTracer? tracer = boot.Trace;
                Assert.That(tracer, Is.Not.Null, "no trace was opened for a traced session");
                Assert.That(tracer!.Active, Is.True, $"tracing stopped: {tracer.Fault}");
                Assert.That(boot.MarkTrace("from the test"), Is.EqualTo(1), "the marker did not land");

                // **Frames are not seconds, and the first draft of this test assumed they were.**
                // A row covers one second of wall clock; 180 frames on this machine is 0.39 s, so
                // the arm asserted on a trace that had correctly written nothing yet. Wait for real
                // time instead, and for two rows rather than one — a single row could be produced
                // by a tracer that emits on its first sample and never again.
                float waited = 0f;
                for (int frame = 0; frame < MaxFramesWaitingForRows && tracer.Rows < 2; frame++)
                {
                    yield return null;
                    waited += Time.unscaledDeltaTime;
                }

                path = tracer.Path;
                rows = tracer.Rows;

                Assert.That(rows, Is.GreaterThanOrEqualTo(2),
                    $"{waited:0.0}s of frames went by and the trace wrote {rows} row(s), so it is " +
                    "not sampling on the clock it claims to");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
                OdysseyBootstrap.TraceEnabled = tracing;
            }

            // After the teardown, so the file is closed and complete — which is also the only state
            // the reader tool ever sees one in.
            yield return null;

            Assert.That(System.IO.File.Exists(path!), Is.True, $"no trace at {path}");
            string[] lines = System.IO.File.ReadAllLines(path!);
            Assert.That(lines.Length, Is.GreaterThan(1), "the trace has a header and nothing else");

            Assert.That(lines[0], Does.Contain(@"""kind"":""header"""));
            Assert.That(lines[0], Does.Contain(@"""board"":""120x120x16"""),
                "the header does not describe the board this session actually built");
            Assert.That(lines[0], Does.Contain($@"""screen"":""{Screen.width}x{Screen.height}"""),
                "the header does not describe the resolution it was taken at");

            var traced = new List<double>();
            bool marked = false;
            foreach (string line in lines)
            {
                if (line.Contains(@"""kind"":""marker""")) marked = true;
                if (line.Contains(@"""kind"":""row""")) traced.Add(Field(line, "frame_p50"));
            }

            Assert.That(marked, Is.True, "the marker never reached the file");
            Assert.That(traced.Count, Is.EqualTo(rows),
                "the file and the tracer disagree about how many rows there are");

            traced.Sort();
            double tracedP50 = traced[traced.Count / 2];

            Debug.Log($"[FrameTime] trace: {traced.Count} rows, trace p50 {tracedP50:0.00} ms " +
                      $"against arm mean {armMean:0.00} ms, file {System.IO.Path.GetFileName(path!)}");

            Assert.That(tracedP50, Is.GreaterThan(0d), "the trace recorded a zero frame time");
            Assert.That(tracedP50, Is.InRange(armMean * 0.5d, armMean * 2d),
                $"the trace says {tracedP50:0.00} ms and the arm that watched the same frames says " +
                $"{armMean:0.00} ms. They are different statistics and need not be equal, but a " +
                "factor of two apart means one of them is not measuring a frame.");
        }

        /// <summary>Pull one number out of a JSONL record, without putting a JSON parser in a test.</summary>
        static double Field(string line, string key)
        {
            int at = line.IndexOf("\"" + key + "\":", StringComparison.Ordinal);
            if (at < 0) return 0d;
            int from = at + key.Length + 3;
            int to = from;
            while (to < line.Length && (char.IsDigit(line[to]) || line[to] == '.' || line[to] == '-')) to++;
            return double.Parse(line.Substring(from, to - from),
                System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// What the surround's sector size is worth, swept over one built world.
        ///
        /// <para><b>Why a sweep and not another judged number.</b> §6c chose 400 m by hand, found
        /// the cost tracked the batch count rather than the tree count, and recorded that past
        /// 400 m it "saturates … the floor being the variants, themes, mute steps and parts, which
        /// no sector size can merge". The census taken on 2026-09-21 says that floor was not
        /// reached: the wood was <b>230 batches over 115 sectors</b>, mean 17 trees a call, with
        /// 192 of the 230 holding fewer than 32 — and only <b>4 mute steps, 2 tints and 1 part</b>
        /// in the whole key. The space was still doing the splitting. A hand-picked constant could
        /// not have shown that; a sweep with the batch count printed beside the frame does.</para>
        ///
        /// <para><b>And the sector ladder saturates almost at once</b>, which is why the variant
        /// count is swept beside it: 400 → 800 m took the wood 230 → 194 batches and the surround
        /// 1.08 → 0.92 ms, and 1600 m and a single 100 km sector both changed nothing further. The
        /// reason is in <c>SectorOf</c>, which folds the variant into the sector number, so a
        /// sixteen-kind wood cannot fall below sixteen batches per spatial cell however coarse the
        /// cells are. Space was the cheap half and it is spent.</para>
        ///
        /// <para>Same built world throughout, rebuilt only in the skirt, in one run — the rule
        /// every frame reading on this machine is subject to (§6c). Read the differences.</para>
        ///
        /// <para>It asserts no time, for the reason the rest of this file gives, and it restores
        /// the shipped sizes in a <c>finally</c> because they are process-wide statics and a test
        /// that leaked one would silently retune every arm that ran afterwards.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheSurroundSectorSweep()
        {
            (string Label, float Near, float Far, int Variants)[] sizes =
            {
                ("400/800 x16 (shipped to 2026-09-21)", 400f, 800f, 16),
                ("800/1600 x16", 800f, 1600f, 16),
                ("1600/3200 x16", 1600f, 3200f, 16),
                ("800/1600 x8 (shipped)", 800f, 1600f, 8),
                ("800/1600 x6", 800f, 1600f, 6),
                ("800/1600 x4", 800f, 1600f, 4),
            };

            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: false,
                out OdysseyBootstrap boot);
            try
            {
                float baseline = 0f;
                yield return TimeFrames("sector/warm", boot, WarmupFrames, m => baseline = m);

                ChunkRenderer renderer = boot.Renderer!;

                for (int i = 0; i < sizes.Length; i++)
                {
                    (string label, float near, float far, int variants) = sizes[i];
                    TerrainSkirt.TreeSectorMetres = near;
                    TerrainSkirt.FarTreeSectorMetres = far;
                    TerrainSkirt.TreeVariantSlots = variants;
                    renderer.Skirt.Build();
                    yield return null;

                    float mean = 0f;
                    double[] split = Array.Empty<double>();
                    yield return TimeFrames($"sector/{label}", boot, WarmupFrames,
                        m => mean = m, s => split = s);

                    TerrainSkirt.Census trees = renderer.Skirt.CensusOf(TerrainSkirt.SkirtPart.Trees);
                    Debug.Log($"[FrameTime] sector {label}: frame {mean:0.00} ms, " +
                              $"Surround {Section(split, OdysseyBootstrap.FrameSection.Surround):0.000} ms, " +
                              $"{renderer.Skirt.BatchesDrawn} batches drawn; trees {trees}; " +
                              $"{renderer.Skirt.KeySpreadOf(TerrainSkirt.SkirtPart.Trees)}");

                    Assert.That(trees.Instances, Is.GreaterThan(0),
                        $"{label} built no wood, so this reading is of an empty surround");
                }
            }
            finally
            {
                // Process-wide statics. A leaked value would retune every arm that runs after this
                // one, and the leak would read as a performance change rather than as a test fault.
                TerrainSkirt.TreeSectorMetres = TerrainSkirt.DefaultTreeSectorMetres;
                TerrainSkirt.FarTreeSectorMetres = TerrainSkirt.DefaultFarTreeSectorMetres;
                TerrainSkirt.TreeVariantSlots = TerrainSkirt.DefaultTreeVariantSlots;
                UnityEngine.Object.Destroy(root);
            }
        }

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

                // Where the surround's batches actually go. §6c cut the count by coarsening the
                // spatial half of the key and said the floor was "the variants, themes, mute
                // steps and parts"; this is the first reading that says whether those remaining
                // batches are full or nearly empty, which is the whole of what to do next.
                Debug.Log($"[FrameTime] surround census: " +
                          $"ground {renderer.Skirt.CensusOf(TerrainSkirt.SkirtPart.Ground)}; " +
                          $"trees {renderer.Skirt.CensusOf(TerrainSkirt.SkirtPart.Trees)}; " +
                          $"tufts {renderer.Skirt.CensusOf(TerrainSkirt.SkirtPart.Tufts)}");
                Debug.Log($"[FrameTime] surround tree key: " +
                          renderer.Skirt.KeySpreadOf(TerrainSkirt.SkirtPart.Trees));
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>
        /// What grass costs at the play resolution as well as the batch one: the first unit of the
        /// Meadow overhaul (<c>docs/design/38-meadow-overhaul.md</c> §11, M1), taken before any
        /// art moves.
        ///
        /// <para><b>Why it is not the arm the design first asked for.</b> d-18 predicted that
        /// every foliage instance is drawn up to six times a frame — the SSAO DepthNormals
        /// prepass, the forward pass and four shadow cascades — and proposed depth priming. Read
        /// against the code it does not hold for grass: <c>ChunkRenderer.FoliageCastsShadows</c>
        /// is off, and foliage is drawn in <see cref="MaterialCache.DefaultFoliageQueue"/>, just
        /// past the opaque range so the outline never inks it, which also keeps it out of the
        /// opaque-only depth prepass. Grass is drawn once, and depth priming cannot reach it.</para>
        ///
        /// <para><b>What the queue does cost is the order.</b> 2501 is in URP's transparent range,
        /// which is sorted back to front — the worst order for alpha-clipped cards over
        /// alpha-clipped cards, since the far clumps are shaded first and then covered. The
        /// alpha-test queue (2450) is opaque, sorted front to back, but joins the DepthNormals
        /// prepass and is inked by the outline. The fourth arm of each resolution prices that
        /// trade; it is a measurement, not a proposal to change the look.</para>
        ///
        /// <para><b>One world, eight readings.</b> None, the shipped density, full cover
        /// (<see cref="GroundScatter.MaxPerCell"/> tufts on every grass cell, the most the scatter
        /// can place today) and full cover in the alpha-test queue — at the batch game view and
        /// with the camera drawing into a 3840 x 2160 target, which is the owner's resolution and
        /// the only one at which fill is honestly priced. Only differences inside this run are
        /// quoted (§6c). The GPU figure is <c>OdysseyBootstrap.GpuFrameMs</c> and is reported as
        /// unavailable rather than as zero where the platform will not say.</para>
        ///
        /// <para>It asserts no times. It asserts that each control applied: the density really
        /// moved the instance count, the queue arm really moved a material, the 4K arm really drew
        /// at 4K, and no reading was taken while the board was still re-meshing.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheGrassAgainstTheFrame()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                out OdysseyBootstrap boot);
            int previousQueue = MaterialCache.FoliageQueue;
            UnityEngine.Camera? cam = null;
            RenderTexture? previousTarget = null;
            RenderTexture? fourK = null;
            try
            {
                yield return TimeFrames("grass/warm", boot, WarmupFrames, _ => { });

                ChunkRenderer renderer = boot.Renderer!;
                int shipped = renderer.ScatterDensity;
                Assert.That(shipped, Is.GreaterThan(0), "this board strews no grass, so there is nothing to price");
                int full = GroundScatter.MaxPerCell * 100;

                // Asked of the art, not of the catalogue: a clone without the packs resolves every
                // tuft to a primitive, the scatter drops those, and no foliage material is ever made
                // — so there is no grass to price, and the arm says so rather than failing on it.
                // The CI runner is that machine (CLAUDE.md, "ask whether the art resolved").
                if (renderer.RequeueFoliage(MaterialCache.DefaultFoliageQueue) == 0)
                    Assert.Ignore("no grass art resolved on this machine, so there is no grass to price");

                cam = boot.cameraRig!.Camera;
                previousTarget = cam.targetTexture;
                fourK = new RenderTexture(3840, 2160, 24) { name = "grass-4k" };

                var arms = new (string Name, int Density, int Queue)[]
                {
                    ("none", 0, MaterialCache.DefaultFoliageQueue),
                    ("shipped", shipped, MaterialCache.DefaultFoliageQueue),
                    ("full", full, MaterialCache.DefaultFoliageQueue),
                    ("full, alpha-test queue", full, (int)RenderQueue.AlphaTest),
                };
                var lines = new List<string>();
                var instances = new Dictionary<string, int>();

                foreach (bool big in new[] { false, true })
                {
                    cam.targetTexture = big ? fourK : previousTarget;
                    string resolution = big ? "3840x2160" : $"{Screen.width}x{Screen.height}";
                    foreach (var arm in arms)
                    {
                        // Density is meshed into the chunks, so moving it is a re-mesh; the
                        // warm-up inside TimeFrames outlasts the meshing budget's instalments.
                        if (renderer.ScatterDensity != arm.Density)
                        {
                            renderer.ScatterDensity = arm.Density;
                            boot.Model!.Remesh();
                        }
                        int moved = renderer.RequeueFoliage(arm.Queue);

                        float ms = 0f, gpu = 0f;
                        yield return TimeFrames($"grass/{resolution}/{arm.Name}", boot, WarmupFrames,
                            m => ms = m, gpu: g => gpu = g);

                        Assert.That(renderer.ChunksMeshDeferred, Is.Zero,
                            $"{resolution} {arm.Name} was timed while the board was still re-meshing");
                        if (big)
                            Assert.That(cam.pixelWidth, Is.EqualTo(3840),
                                "the camera was not drawing at 4K, so this arm measured the batch view");
                        if (arm.Density > 0)
                            Assert.That(moved, Is.GreaterThan(0),
                                "no foliage material was re-queued, so the queue arm compared a queue with itself");

                        instances[$"{resolution}/{arm.Name}"] = renderer.InstancesDrawn;
                        lines.Add($"{resolution} {arm.Name}: frame {ms:0.00} ms, gpu " +
                                  (gpu > 0f ? $"{gpu:0.00} ms" : "unavailable") +
                                  $", {renderer.DrawCalls} calls, {renderer.InstancesDrawn} instances");
                    }
                }

                Debug.Log($"[FrameTime] grass (shipped density {shipped}, full {full}, " +
                          $"{SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsDeviceType}): " +
                          string.Join("; ", lines));

                string small = $"{Screen.width}x{Screen.height}";
                Assert.That(instances[$"{small}/shipped"], Is.GreaterThan(instances[$"{small}/none"]),
                    "the shipped density drew no more than none, so the grass never appeared");
                Assert.That(instances[$"{small}/full"], Is.GreaterThan(instances[$"{small}/shipped"]),
                    "full cover drew no more than the shipped density, so the full arm measured nothing new");
            }
            finally
            {
                // The queue is a static every later MaterialCache reads, so it goes back whatever
                // happened; so does the camera's target, before the world is destroyed.
                MaterialCache.FoliageQueue = previousQueue;
                if (cam != null) cam.targetTexture = previousTarget;
                if (fourK != null) fourK.Release();
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>
        /// What <c>Odyssey/Foliage</c> costs against the pack's own foliage shader, on the same grass
        /// in the same run (<c>docs/design/38-meadow-overhaul.md</c> §4, M3).
        ///
        /// <para>Two shaders over the same meshes and textures, at the shipped density and at full
        /// cover, at the batch view and at 3840 x 2160. Ours adds a clearance fetch and a rotation per
        /// vertex and drops the pack's noise colouring; whether that is cheaper or dearer is the
        /// question, and M1's arm (§13) is the scale it is read against. The switch is
        /// <c>MaterialCache.OwnFoliageShader</c> with the clones dropped between arms, and the arm
        /// asserts the drop reached something, so it cannot compare a shader with itself (P18).</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheFoliageShaderAgainstThePacks()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                out OdysseyBootstrap boot);
            UnityEngine.Camera? cam = null;
            RenderTexture? previousTarget = null;
            RenderTexture? fourK = null;
            try
            {
                yield return TimeFrames("foliage/warm", boot, WarmupFrames, _ => { });

                ChunkRenderer renderer = boot.Renderer!;
                if (renderer.RequeueFoliage(MaterialCache.DefaultFoliageQueue) == 0)
                    Assert.Ignore("no grass art resolved on this machine, so there is no foliage to price");

                int shipped = renderer.ScatterDensity;
                int full = GroundScatter.MaxPerCell * 100;
                cam = boot.cameraRig!.Camera;
                previousTarget = cam.targetTexture;
                fourK = new RenderTexture(3840, 2160, 24) { name = "foliage-4k" };

                var lines = new List<string>();
                foreach (bool big in new[] { false, true })
                {
                    cam.targetTexture = big ? fourK : previousTarget;
                    string resolution = big ? "3840x2160" : $"{Screen.width}x{Screen.height}";
                    foreach (int density in new[] { shipped, full })
                    {
                        if (renderer.ScatterDensity != density)
                        {
                            renderer.ScatterDensity = density;
                            boot.Model!.Remesh();
                        }
                        foreach (bool ours in new[] { false, true })
                        {
                            MaterialCache.OwnFoliageShader = ours;
                            int dropped = renderer.ForgetFoliageMaterials();
                            float ms = 0f;
                            yield return TimeFrames($"foliage/{resolution}/{density}/{(ours ? "ours" : "pack")}",
                                boot, WarmupFrames, m => ms = m);
                            Assert.That(dropped, Is.GreaterThan(0),
                                "no foliage clone was dropped, so this arm drew the previous shader again");
                            Assert.That(renderer.ChunksMeshDeferred, Is.Zero, "timed while still re-meshing");
                            lines.Add($"{resolution} density {density} {(ours ? "ours" : "pack")}: " +
                                      $"frame {ms:0.00} ms, {renderer.DrawCalls} calls, {renderer.InstancesDrawn} instances");
                        }
                    }
                }

                Debug.Log("[FrameTime] foliage shader (" + SystemInfo.graphicsDeviceName + "): " +
                          string.Join("; ", lines));
            }
            finally
            {
                MaterialCache.OwnFoliageShader = true;
                if (cam != null) cam.targetTexture = previousTarget;
                if (fourK != null) fourK.Release();
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>
        /// What levels of detail do to the frame on the played meadow, with the pack's own switch
        /// heights (<c>docs/design/38-meadow-overhaul.md</c> §3, M2).
        ///
        /// <para><b>A control for the mechanism, not the saving it exists for.</b> The saving is
        /// the Meadow trees (M5), which are 5,000 to 45,000 triangles each; today's board has
        /// PolygonGeneric trees with no levels, and the only art with levels is the grass, which is
        /// cheap already (§13). What this proves is that the levels really are chosen and drawn in a
        /// running world — <c>InstancesAtCoarserLevels</c> above zero with them on and at zero with
        /// them off — and what the pack's numbers do at this camera, which is the reason
        /// <c>UseLods</c> ships off.</para>
        ///
        /// <para>Ignored where no drawn module has levels, which is a clone without the packs: the
        /// catalogue resolves to primitives there, and a primitive has one level.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheLevelsOfDetailAgainstTheFrame()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                out OdysseyBootstrap boot);
            UnityEngine.Camera? cam = null;
            RenderTexture? previousTarget = null;
            RenderTexture? fourK = null;
            try
            {
                yield return TimeFrames("lod/warm", boot, WarmupFrames, _ => { });

                ChunkRenderer renderer = boot.Renderer!;
                ModuleLibrary library = boot.Model!.Library;
                // Trees choose their level by a switch of their own since the look pass (design
                // 38 §17); this arm prices the general one, so the trees' is off for its control.

                renderer.TreeLevels = false;
                // And the dressing's own levels (design 38 §18c), on by default since, so that "off"
                // is every module at its finest and the control means what it says.
                renderer.DressingLevels = false;
                int withLevels = 0;
                for (int i = 0; i < library.Count; i++)
                    if (library[i].DrawsByLevel) withLevels++;
                if (withLevels == 0)
                    Assert.Ignore("no module on this board resolved with levels of detail — no art on this machine");

                cam = boot.cameraRig!.Camera;
                previousTarget = cam.targetTexture;
                fourK = new RenderTexture(3840, 2160, 24) { name = "lod-4k" };

                var lines = new List<string>();
                int coarserWhenOn = 0, coarserWhenOff = -1;
                foreach (bool big in new[] { false, true })
                {
                    cam.targetTexture = big ? fourK : previousTarget;
                    string resolution = big ? "3840x2160" : $"{Screen.width}x{Screen.height}";
                    foreach (bool on in new[] { false, true })
                    {
                        renderer.UseLods = on;
                        float ms = 0f;
                        yield return TimeFrames($"lod/{resolution}/{(on ? "on" : "off")}", boot, WarmupFrames,
                            m => ms = m);
                        int coarser = renderer.InstancesAtCoarserLevels;
                        if (on) coarserWhenOn = Math.Max(coarserWhenOn, coarser);
                        else coarserWhenOff = Math.Max(coarserWhenOff, coarser);
                        lines.Add($"{resolution} levels {(on ? "on" : "off")}: frame {ms:0.00} ms, " +
                                  $"{renderer.DrawCalls} calls, {renderer.InstancesDrawn} instances, " +
                                  $"{coarser} at a coarser level");
                    }
                }

                Debug.Log($"[FrameTime] levels of detail ({withLevels} modules with levels, bias " +
                          $"{renderer.LodBias}, fov {renderer.ViewerFieldOfView:0}): " + string.Join("; ", lines));

                Assert.That(coarserWhenOff, Is.Zero, "levels were chosen with UseLods off");
                Assert.That(coarserWhenOn, Is.GreaterThan(0),
                    "levels were on and nothing was drawn at a coarser level, so the pick never ran");
            }
            finally
            {
                if (boot.Renderer != null)
                {
                    boot.Renderer.UseLods = false; boot.Renderer.TreeLevels = true; boot.Renderer.DressingLevels = true;
                }
                if (cam != null) cam.targetTexture = previousTarget;
                if (fourK != null) fourK.Release();
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>
        /// What the Meadow look costs (the look pass, design 38 §17): the played meadow with its
        /// Meadow trees, at 3840 x 2160, on Standard and Huge, with the grass ladder at Off (no
        /// tufts, no dressing), Meadow (the shipped rung, High) and Full (Ultra). One world per
        /// board, three readings each, only the differences quoted.
        ///
        /// <para>It asserts that the controls applied — the dressing resolved to art, the instance
        /// count rose with the rung, the camera drew at 4K, and nothing was timed mid-re-mesh —
        /// and ignores itself where no dressing art resolved, which is a clone without the packs.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheDressingAgainstTheFrame()
        {
            var lines = new List<string>();
            foreach ((string board, int side) in new[] { ("standard", 120), ("huge", 240) })
            {
                GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                    out OdysseyBootstrap boot, side, side, 16);
                UnityEngine.Camera? cam = null;
                RenderTexture? previousTarget = null;
                RenderTexture? fourK = null;
                try
                {
                    yield return TimeFrames($"dressing/{board}/warm", boot, WarmupFrames, _ => { });
                    ChunkRenderer renderer = boot.Renderer!;
                    if (renderer.DressingFamiliesWithArt == 0)
                        Assert.Ignore("no Meadow dressing resolved to art on this machine, so there is none to price");

                    cam = boot.cameraRig!.Camera;
                    previousTarget = cam.targetTexture;
                    fourK = new RenderTexture(3840, 2160, 24) { name = "dressing-4k" };
                    cam.targetTexture = fourK;

                    int shipped = renderer.ScatterDensity;
                    int previousInstances = -1;
                    foreach ((string rung, int density) in new[] { ("off", 0), ("meadow", shipped), ("full", GroundScatter.MaxPerCell * 100) })
                    {
                        if (renderer.ScatterDensity != density)
                        {
                            renderer.ScatterDensity = density;
                            boot.Model!.Remesh();
                        }
                        float ms = 0f;
                        yield return TimeFrames($"dressing/{board}/{rung}", boot, WarmupFrames, m => ms = m);
                        Assert.That(renderer.ChunksMeshDeferred, Is.Zero, $"{board} {rung} was timed mid-re-mesh");
                        Assert.That(cam.pixelWidth, Is.EqualTo(3840), "the camera was not drawing at 4K");
                        Assert.That(renderer.InstancesDrawn, Is.GreaterThan(previousInstances),
                            $"{board} {rung} drew no more than the rung below it, so the dressing never changed");
                        previousInstances = renderer.InstancesDrawn;
                        lines.Add($"{board} {rung}: frame {ms:0.00} ms, {renderer.DrawCalls} calls, " +
                                  $"{renderer.InstancesDrawn} instances, {renderer.InstancesAtCoarserLevels} at a coarser level");

                        // Where the Meadow rung's cost is, on the Standard board: the trees' level
                        // of detail and the shadow casters, each taken away in turn.
                        if (board == "standard" && rung == "meadow")
                        {
                            renderer.TreeShadowProxy = false;
                            float fullShadows = 0f;
                            yield return TimeFrames("dressing/standard/meadow, no shadow proxy", boot, WarmupFrames, m => fullShadows = m);
                            lines.Add($"standard meadow casting from the finest level: frame {fullShadows:0.00} ms, {renderer.DrawCalls} calls");
                            renderer.TreeShadowProxy = true;

                            renderer.DressingCastsShadows = true;
                            float bushShadows = 0f;
                            yield return TimeFrames("dressing/standard/meadow, bushes casting", boot, WarmupFrames, m => bushShadows = m);
                            lines.Add($"standard meadow with the bushes casting: frame {bushShadows:0.00} ms, {renderer.DrawCalls} calls");
                            renderer.DressingCastsShadows = false;

                            bool casts = renderer.CastShadows;
                            renderer.CastShadows = false;
                            float noShadows = 0f;
                            yield return TimeFrames("dressing/standard/meadow, no casters", boot, WarmupFrames, m => noShadows = m);
                            lines.Add($"standard meadow with no shadow casters: frame {noShadows:0.00} ms, {renderer.DrawCalls} calls");
                            renderer.CastShadows = casts;
                        }
                    }
                }
                finally
                {
                    if (cam != null) cam.targetTexture = previousTarget;
                    if (fourK != null) fourK.Release();
                    UnityEngine.Object.Destroy(root);
                }
                yield return null;
            }
            Debug.Log($"[FrameTime] dressing at 3840x2160 ({SystemInfo.graphicsDeviceName}): " + string.Join("; ", lines));
        }

        /// <summary>
        /// What the painted Meadow ground and the demo's grade cost at the owner's resolution
        /// (<c>docs/design/38-meadow-overhaul.md</c> §17, the look pass — ground and light).
        ///
        /// <para>Two worlds, because the ground material is chosen when a session's module library
        /// resolves the grass terrain: the stock tiled texture, then <c>Odyssey/MeadowGround</c>.
        /// On the second, the Play scene's golden-hour volume and then the Meadow demo's own. Each
        /// at 640 x 480 and into a 3840 x 2160 target. Ground is most of the pixels at the play
        /// camera, so this is the arm that says whether five texture samples and four noise fields
        /// a pixel are affordable; only differences inside the run are quoted (§6c).</para>
        ///
        /// <para>Ignored where the look did not resolve — a clone without the packs, the runner.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheMeadowGroundAgainstTheFrame()
        {
            if (MeadowLook.Loaded == null || !MeadowLook.Loaded.HasGround)
                Assert.Ignore("the Meadow look's textures did not resolve on this machine");

            bool groundWas = MeadowLook.GroundEnabled;
            var lines = new List<string>();
            try
            {
                foreach (bool painted in new[] { false, true })
                {
                    MeadowLook.GroundEnabled = painted;
                    GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                        out OdysseyBootstrap boot);
                    UnityEngine.Camera? cam = null;
                    RenderTexture? previousTarget = null;
                    RenderTexture? fourK = null;
                    Volume? grade = null;
                    try
                    {
                        yield return TimeFrames($"meadow-ground/{(painted ? "painted" : "stock")}/warm", boot,
                            WarmupFrames, _ => { });
                        Assert.That(MeadowLook.GroundActive, Is.EqualTo(painted),
                            "the ground switch did not reach the session, so the two arms are one ground");

#if UNITY_EDITOR
                        var golden = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                            "Assets/Settings/OdysseyGoldenHour.asset");
                        if (golden != null)
                        {
                            grade = new GameObject("Grade").AddComponent<Volume>();
                            grade.transform.SetParent(root.transform, false);
                            grade.isGlobal = true;
                            grade.sharedProfile = golden;
                        }
#endif
                        cam = boot.cameraRig!.Camera;
                        previousTarget = cam.targetTexture;
                        fourK = new RenderTexture(3840, 2160, 24) { name = "meadow-ground-4k" };

                        var grades = new List<(string Name, VolumeProfile? Profile)> { ("golden", grade?.sharedProfile) };
                        if (painted && grade != null && MeadowLook.Loaded!.grade != null)
                            grades.Add(("meadow grade", MeadowLook.Loaded!.grade));

                        foreach (var g in grades)
                        {
                            if (grade != null && g.Profile != null) grade.sharedProfile = g.Profile;
                            foreach (bool big in new[] { false, true })
                            {
                                cam.targetTexture = big ? fourK : previousTarget;
                                string resolution = big ? "3840x2160" : $"{Screen.width}x{Screen.height}";
                                float ms = 0f;
                                yield return TimeFrames(
                                    $"meadow-ground/{(painted ? "painted" : "stock")}/{g.Name}/{resolution}",
                                    boot, WarmupFrames, m => ms = m);
                                lines.Add($"{resolution} {(painted ? "painted" : "stock")} ground, {g.Name}: " +
                                          $"frame {ms:0.00} ms, {boot.Renderer?.DrawCalls ?? -1} calls");
                            }
                        }
                    }
                    finally
                    {
                        if (cam != null) cam.targetTexture = previousTarget;
                        if (fourK != null) fourK.Release();
                        UnityEngine.Object.Destroy(root);
                    }
                    yield return null;
                }
            }
            finally
            {
                MeadowLook.GroundEnabled = groundWas;
            }

            Debug.Log($"[FrameTime] meadow ground ({SystemInfo.graphicsDeviceName}): " + string.Join("; ", lines));
        }

        /// <summary>
        /// Where the look pass's frame goes: research for d-19 (the owner, 2026-09-24: "the compute
        /// is up to 5 ms", against ~1.5 before the look pass). Explicit: a measurement, not a test.
        ///
        /// <para>One played meadow (Standard), five readings at the batch view and five with the
        /// camera drawing into 3840 x 2160: the look as shipped; the same with
        /// <c>SubmitToGpu</c> off, so the renderer does all of its own bookkeeping and hands Unity
        /// nothing — the difference is what Unity spends drawing the submissions; the dressing and
        /// tufts off (<c>ScatterDensity</c> 0, the nearest in-run stand-in for the board before the
        /// pass); that with <c>SubmitToGpu</c> off; and the look with no shadow casters. Plus a
        /// census of the meshed buckets by kind, which is what a draw call is here.</para>
        /// </summary>
        [UnityTest, Explicit("a measurement for docs/research/d-19, not a test")]
        public IEnumerator TheLookAgainstTheSubmission()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                out OdysseyBootstrap boot);
            UnityEngine.Camera? cam = null;
            RenderTexture? previousTarget = null;
            RenderTexture? fourK = null;
            try
            {
                yield return TimeFrames("submission/warm", boot, WarmupFrames, _ => { });
                ChunkRenderer renderer = boot.Renderer!;
                int shipped = renderer.ScatterDensity;
                Debug.Log("[FrameTime] submission census: " + Census(renderer));

                cam = boot.cameraRig!.Camera;
                previousTarget = cam.targetTexture;
                fourK = new RenderTexture(3840, 2160, 24) { name = "submission-4k" };

                // Every arm but the last two draws the tufts the chunk way, so the rows stay
                // comparable with d-19's; the last two are design 38 §18's tie-breaker.
                var arms = new (string Name, int Density, bool Submit, bool Shadows, float ShadowMetres, bool Indirect)[]
                {
                    ("look", shipped, true, true, 0f, false),
                    ("look, nothing handed to Unity", shipped, false, true, 0f, false),
                    ("no dressing or tufts", 0, true, true, 0f, false),
                    ("no dressing or tufts, nothing handed to Unity", 0, false, true, 0f, false),
                    ("look, no shadow casters", shipped, true, false, 0f, false),
                    ("look, shadow margin as a shell", shipped, true, true, -1f, false),
                    // The High preset's shadow distance, on a runtime copy of the pipeline asset so
                    // the committed one is never dirtied (DisplaySettingsApplier's rule).
                    ("look, 120 m shadows", shipped, true, true, 120f, false),
                    ("no dressing or tufts, 120 m shadows", 0, true, true, 120f, false),
                    ("look, tufts indirect", shipped, true, true, 0f, true),
                    ("look again, tufts chunk by chunk", shipped, true, true, 0f, false),
                };
                var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                RenderPipelineAsset? qualityWas = QualitySettings.renderPipeline;
                RenderPipelineAsset? defaultWas = GraphicsSettings.defaultRenderPipeline;
                float shadowWas = QualitySettings.shadowDistance;
                UniversalRenderPipelineAsset? copy = pipeline != null ? UnityEngine.Object.Instantiate(pipeline) : null;
                var lines = new List<string>();
                try
                {
                foreach (bool big in new[] { false, true })
                {
                    cam.targetTexture = big ? fourK : previousTarget;
                    string resolution = big ? "3840x2160" : $"{Screen.width}x{Screen.height}";
                    foreach (var arm in arms)
                    {
                        if (renderer.ScatterDensity != arm.Density)
                        {
                            renderer.ScatterDensity = arm.Density;
                            boot.Model!.Remesh();
                        }
                        renderer.SubmitToGpu = arm.Submit;
                        renderer.CastShadows = arm.Shadows;
                        renderer.IndirectTufts = arm.Indirect;
                        // -1 marks the arm that measures the old margin: a shell in every direction.
                        renderer.SweepShadowMargin = arm.ShadowMetres >= 0f;
                        if (copy != null)
                        {
                            float metres = arm.ShadowMetres > 0f ? arm.ShadowMetres : pipeline!.shadowDistance;
                            copy.shadowDistance = metres;
                            QualitySettings.shadowDistance = metres;
                            if (QualitySettings.renderPipeline != null) QualitySettings.renderPipeline = copy;
                            else GraphicsSettings.defaultRenderPipeline = copy;
                        }
                        float ms = 0f;
                        double[] split = Array.Empty<double>();
                        yield return TimeFrames($"submission/{resolution}/{arm.Name}", boot, WarmupFrames,
                            m => ms = m, p => split = p);
                        Assert.That(renderer.ChunksMeshDeferred, Is.Zero, $"{arm.Name} was timed mid-re-mesh");
                        lines.Add($"{resolution} {arm.Name}: frame {ms:0.00} ms, submit {boot.SubmitMs:0.00}, " +
                                  $"World {Section(split, OdysseyBootstrap.FrameSection.World):0.000}, " +
                                  $"Surround {Section(split, OdysseyBootstrap.FrameSection.Surround):0.000}, " +
                                  $"Figures {Section(split, OdysseyBootstrap.FrameSection.Figures):0.000}, " +
                                  $"{renderer.DrawCalls} calls ({renderer.IndirectDrawCalls} indirect), " +
                                  $"{renderer.InstancesDrawn} instances, {renderer.ChunksDrawn} chunks");
                    }
                }
                }
                finally
                {
                    QualitySettings.renderPipeline = qualityWas;
                    GraphicsSettings.defaultRenderPipeline = defaultWas;
                    QualitySettings.shadowDistance = shadowWas;
                    if (copy != null) UnityEngine.Object.Destroy(copy);
                }
                Debug.Log($"[FrameTime] submission split (pipeline shadow distance {pipeline?.shadowDistance ?? -1f} m, " +
                          $"{pipeline?.shadowCascadeCount ?? -1} cascades): " + string.Join("; ", lines));
            }
            finally
            {
                if (boot.Renderer != null)
                {
                    boot.Renderer.SubmitToGpu = true;
                    boot.Renderer.CastShadows = true;
                    boot.Renderer.SweepShadowMargin = true;
                    boot.Renderer.IndirectTufts = false;
                }
                if (cam != null) cam.targetTexture = previousTarget;
                if (fourK != null) fourK.Release();
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>
        /// The tufts drawn from GPU buffers look exactly as the tufts drawn chunk by chunk
        /// (design 38 §18). Stilled, settled, three shots: the chunk path twice (the floor), then the
        /// indirect path; and the grass taken away altogether as the positive control, so a pass that
        /// drew no grass at all could not read as "no change".
        /// </summary>
        [UnityTest]
        public IEnumerator TheIndirectTuftsDoNotChangeThePicture()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                out OdysseyBootstrap boot);
            var target = new RenderTexture(320, 240, 24) { name = "indirect-proof" };
            float previousScale = Time.timeScale;
            UnityEngine.Camera? cam = null;
            RenderTexture? previousTarget = null;
            bool indirectWas = false;
            try
            {
                yield return null;
                indirectWas = boot.Renderer!.IndirectTufts;
                for (int i = 0; i < 120 && boot.World!.GameSpeed != 0; i++)
                {
                    boot.World!.Intents.Submit(new Intent(IntentKind.SetGameSpeed, default, 0));
                    yield return null;
                }
                // The wind is phased off the tick, so a world still ticking waves every blade
                // between two shots and the floor is noise everywhere (4.9% on the first attempt).
                Assert.That(boot.World!.GameSpeed, Is.Zero, "the world would not pause, so the shots cannot be still");
                Time.timeScale = 0f;
                ChunkRenderer renderer = boot.Renderer!;
                // Settled means the board has finished meshing in, not a fixed count of frames:
                // inside the full tier two shots of one configuration differed by 3.8% at 120
                // frames, against 0.00% alone, because chunks were still arriving under the budget.
                for (int i = 0, quiet = 0; i < 1200 && (i < 120 || quiet < 30); i++)
                {
                    yield return null;
                    quiet = renderer.ChunksMeshDeferred == 0 && renderer.ChunksMeshedThisFrame == 0 ? quiet + 1 : 0;
                }

                renderer.IndirectTufts = true;
                yield return null;
                if (renderer.IndirectDrawCalls == 0)
                    Assert.Ignore("the indirect path did not draw on this machine (no compute, no grass art, " +
                                  "or no foliage shader), so there is nothing to compare");

                cam = boot.cameraRig!.Camera;
                previousTarget = cam.targetTexture;
                cam.targetTexture = target;

                renderer.IndirectTufts = false;
                long tickBefore = boot.World!.CurrentTick;
                Color32[] chunk = null!, again = null!, indirect = null!, bare = null!;
                // **Still means two shots agree, not a count of frames.** With the world paused and
                // the tick unchanged, the full tier still read a 3.8-6.5% floor after a fixed wait
                // where a run alone read 0.00% — something (the camera easing in, a first-use
                // upload) was still settling. So pairs are shot until two agree, and the pair that
                // did is the floor.
                int chunkCalls = 0;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    yield return Shoot("indirect-off", boot, target, p => chunk = p);
                    chunkCalls = renderer.DrawCalls;
                    yield return Shoot("indirect-off-again", boot, target, p => again = p);
                    if (Difference(chunk, again) < 0.005f) break;
                    for (int i = 0; i < 60; i++) yield return null;
                }
                renderer.IndirectTufts = true;
                yield return Shoot("indirect-on", boot, target, p => indirect = p);
                int indirectCalls = renderer.DrawCalls;
                int indirectOnly = renderer.IndirectDrawCalls;
                Assert.That(boot.World!.CurrentTick, Is.EqualTo(tickBefore),
                    "the world ticked between the shots, so the wind moved every blade and nothing can be compared");

                int shipped = renderer.ScatterDensity;
                renderer.ScatterDensity = 0;
                boot.Model!.Remesh();
                for (int i = 0; i < 60; i++) yield return null;
                yield return Shoot("indirect-bare", boot, target, p => bare = p);
                renderer.ScatterDensity = shipped;
                boot.Model!.Remesh();

                float noise = Difference(chunk, again);
                float moved = Difference(chunk, indirect);
                float grass = Difference(chunk, bare);
                Debug.Log($"[FrameTime] indirect proof: the same shot twice moved {noise * 100f:0.00}%, " +
                          $"the indirect tufts moved {moved * 100f:0.00}%, taking the grass away moved " +
                          $"{grass * 100f:0.00}%; {chunkCalls} calls chunk by chunk, {indirectCalls} with " +
                          $"{indirectOnly} indirect");

                Assert.That(noise, Is.LessThan(0.02f), "the repeated shot has no floor to measure against");
                Assert.That(grass, Is.GreaterThan(0.01f),
                    "taking the grass away changed nothing, so the grass is not in these shots and the " +
                    "comparison proves nothing");
                Assert.That(indirectCalls, Is.LessThan(chunkCalls),
                    "the indirect path did not reduce the calls, so the chunk walk is still drawing the tufts");
                Assert.That(moved, Is.LessThanOrEqualTo(noise + 0.002f),
                    $"the indirect tufts moved {moved * 100f:0.00}% of pixels against a floor of " +
                    $"{noise * 100f:0.00}%: they are not drawing what the chunk path drew");
            }
            finally
            {
                Time.timeScale = previousScale;
                if (cam != null) cam.targetTexture = previousTarget;
                if (boot.Renderer != null) boot.Renderer.IndirectTufts = indirectWas;
                UnityEngine.Object.Destroy(root);
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
        }

        /// <summary>The meshed buckets by kind: how many, how full, and how many calls they cost.</summary>
        static string Census(ChunkRenderer renderer)
        {
            var batches = (ChunkBatch?[])typeof(ChunkRenderer)
                .GetField("_batches", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(renderer)!;
            var counts = new SortedDictionary<string, (int Buckets, long Instances, int Chunks)>();
            int meshed = 0;
            foreach (ChunkBatch? batch in batches)
            {
                if (batch == null || batch.InstanceCount == 0) continue;
                meshed++;
                var seen = new HashSet<string>();
                foreach (var list in new[] { batch.Body, batch.Roof })
                    foreach (InstanceBucket bucket in list)
                    {
                        if (bucket.Count == 0) continue;
                        string kind = TintCode.IsTree(bucket.Tint) ? "tree"
                            : TintCode.IsDressing(bucket.Tint) ? "dressing"
                            : TintCode.IsFoliage(bucket.Tint) ? "tufts"
                            : TintCode.IsWater(bucket.Tint) ? "water"
                            : TintCode.IsTerrain(bucket.Tint) ? "terrain"
                            : "other";
                        (int Buckets, long Instances, int Chunks) c =
                            counts.TryGetValue(kind, out var v) ? v : (0, 0L, 0);
                        c.Buckets++; c.Instances += bucket.Count;
                        if (seen.Add(kind)) c.Chunks++;
                        counts[kind] = c;
                    }
            }
            var parts = new List<string> { $"{meshed} meshed chunks" };
            foreach (var kv in counts)
                parts.Add($"{kv.Key}: {kv.Value.Buckets} buckets in {kv.Value.Chunks} chunks " +
                          $"({(kv.Value.Chunks > 0 ? kv.Value.Buckets / (double)kv.Value.Chunks : 0):0.0} a chunk), " +
                          $"{kv.Value.Instances} instances ({(kv.Value.Buckets > 0 ? kv.Value.Instances / (double)kv.Value.Buckets : 0):0.0} a bucket)");
            return string.Join("; ", parts);
        }

        /// <summary>
        /// Photographs the played meadow from the play camera, for judging the look against the
        /// Meadow Forest reference (owner, 2026-09-24: screenshot #13, design 38 §17). Explicit:
        /// never part of a tier, run by name.
        ///
        /// <para>Two framings at 1920 x 1080 — the camera as a new game opens it, and closer in on
        /// the first colonist — written to <c>Logs/look/start.png</c> and <c>Logs/look/close.png</c>,
        /// with the draw counts beside them in the log. It asserts nothing about the picture; it
        /// exists so that the person or agent changing the look can see what they changed without
        /// pressing Play, which until now only the owner could do.</para>
        /// </summary>
        [UnityTest, Explicit("a photograph for judging the look, not a test")]
        public IEnumerator TheLookAtThePlayCamera()
        {
            // ODYSSEY_LOOK_STOCK=1 photographs the stock ground instead of the painted one, so a
            // before and an after can be taken under identical conditions (design 38 §17).
            bool stock = Environment.GetEnvironmentVariable("ODYSSEY_LOOK_STOCK") == "1";
            bool groundWas = MeadowLook.GroundEnabled;
            MeadowLook.GroundEnabled = !stock;
            string prefix = stock ? "stock-" : string.Empty;

            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                out OdysseyBootstrap boot);
            RenderTexture? target = null;
            UnityEngine.Camera? cam = null;
            RenderTexture? previousTarget = null;
            try
            {
                // Long enough for the board to mesh out under the budget and the post stack to settle.
                for (int i = 0; i < 180; i++) yield return null;

                cam = boot.cameraRig!.Camera;
                previousTarget = cam.targetTexture;
                target = new RenderTexture(1920, 1080, 24) { name = "look" };
                cam.targetTexture = target;
                Directory.CreateDirectory(Path.GetFullPath("Logs/look"));

                // **The grade the Play scene carries.** Play.unity has a global Volume holding the
                // golden-hour profile; the rig this file builds has none, so without this every
                // photograph was of an ungraded frame nobody plays.
                Volume? grade = null;
#if UNITY_EDITOR
                var golden = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                    "Assets/Settings/OdysseyGoldenHour.asset");
                if (golden != null)
                {
                    grade = new GameObject("Grade").AddComponent<Volume>();
                    grade.transform.SetParent(root.transform, false);
                    grade.isGlobal = true;
                    grade.sharedProfile = golden;
                }
#endif
                for (int i = 0; i < 8; i++) yield return null;

                yield return Photograph(prefix + "start", boot, target);

                WorldSnapshot frame = boot.World!.Views.Current;
                if (frame.Pawns.Length > 0)
                {
                    boot.cameraRig!.FocusOn(frame.Pawns[0].Cell, 28f);
                    for (int i = 0; i < 120; i++) yield return null;
                    yield return Photograph(prefix + "close", boot, target);

                    // Pulled back to about the reference screenshot's own framing, which is the
                    // view the composition of the meadow is judged at.
                    boot.cameraRig!.FocusOn(frame.Pawns[0].Cell, 70f);
                    for (int i = 0; i < 150; i++) yield return null;
                    yield return Photograph(prefix + "wide", boot, target);

                    // The same framing with every tree and bush drawn as though it stood between the
                    // camera and a colonist, so the fade can be judged by looking (design 38 §17c).
                    boot.Renderer!.FadeEveryTreeForAPhotograph = true;
                    yield return Photograph(prefix + "wide-faded", boot, target);
                    boot.Renderer!.FadeEveryTreeForAPhotograph = false;

                    // design 38 §19: a stack dropped beside a bush and a colonist lying in the
                    // grass, before (the old 0.55 m ring, no body ring, no bush fade) and after.
                    yield return PhotographTheGround(boot, target, prefix);
                    // And an unselected colonist standing behind a tree, with the see-through for
                    // selected colonists only (before) and for every colonist (after).
                    yield return PhotographBehindATree(boot, target, prefix);

                    // The surround at the camera's farthest pull, from the rim of the board looking
                    // out (design 38 §19): the land beyond should read wooded, not bare.
                    var size = boot.World!.Size;
                    boot.cameraRig!.FocusOn(new CellRef(size.SizeX / 2, 2, frame.Pawns[0].Cell.Y),
                        boot.cameraRig.maxDistance);
                    for (int i = 0; i < 150; i++) yield return null;
                    yield return Photograph(prefix + "horizon", boot, target);
                }

                // And the wide framing again under the Meadow demo's own grade, where it resolved.
                VolumeProfile? meadow = MeadowLook.Loaded != null ? MeadowLook.Loaded.grade : null;
                if (grade != null && meadow != null)
                {
                    grade.sharedProfile = meadow;
                    yield return Photograph(prefix + "wide-meadowgrade", boot, target);
                }
            }
            finally
            {
                MeadowLook.GroundEnabled = groundWas;
                if (cam != null) cam.targetTexture = previousTarget;
                if (target != null) target.Release();
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>
        /// Photographs the dressing's levels of detail at a range of biases, for tuning them by eye
        /// (design 38 §18c). Explicit: run by name. The world is paused and the clock stilled, so
        /// what moves between two shots is the levels alone. At each framing — the start, close in,
        /// and the reference's wide one — the levels off, then the grass stands and flowers at each
        /// bias with the bushes at their finest, then the bushes at each bias with the grass at its
        /// finest. Each shot is written to <c>Logs/look/lod/</c> and logged with the pixels it moved
        /// against the levels-off shot, the instances drawn at a coarser level and the draw calls.
        /// </summary>
        [UnityTest, Explicit("photographs for tuning the dressing's levels, not a test")]
        public IEnumerator TheDressingLevelsAtThePlayCamera()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                out OdysseyBootstrap boot);
            RenderTexture? target = null;
            UnityEngine.Camera? cam = null;
            RenderTexture? previousTarget = null;
            float previousScale = Time.timeScale;
            ChunkRenderer renderer = null!;
            bool levelsWas = false;
            float dressingWas = 1f, bushWas = 3f;
            try
            {
                yield return null;
                renderer = boot.Renderer!;
                levelsWas = renderer.DressingLevels; dressingWas = renderer.DressingLodBias; bushWas = renderer.BushLodBias;
                for (int i = 0; i < 120 && boot.World!.GameSpeed != 0; i++)
                {
                    boot.World!.Intents.Submit(new Intent(IntentKind.SetGameSpeed, default, 0));
                    yield return null;
                }
                Assert.That(boot.World!.GameSpeed, Is.Zero, "the world would not pause");
                Time.timeScale = 0f;

                cam = boot.cameraRig!.Camera;
                previousTarget = cam.targetTexture;
                target = new RenderTexture(1920, 1080, 24) { name = "lod-look" };
                cam.targetTexture = target;
                Directory.CreateDirectory(Path.GetFullPath("Logs/look/lod"));
#if UNITY_EDITOR
                var golden = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/OdysseyGoldenHour.asset");
                if (golden != null)
                {
                    var grade = new GameObject("Grade").AddComponent<Volume>();
                    grade.transform.SetParent(root.transform, false);
                    grade.isGlobal = true;
                    grade.sharedProfile = golden;
                }
#endif
                WorldSnapshot frame = boot.World!.Views.Current;
                var framings = new List<(string Name, float Distance)> { ("start", -1f) };
                if (frame.Pawns.Length > 0) { framings.Add(("close", 28f)); framings.Add(("wide", 70f)); }
                float[] dressingBiases = { 8f, 4f, 2f, 1f, 0.5f };
                float[] bushBiases = { 6f, 3f, 1.5f, 0.75f };
                var lines = new List<string>();

                foreach (var framing in framings)
                {
                    if (framing.Distance > 0f) boot.cameraRig!.FocusOn(frame.Pawns[0].Cell, framing.Distance);
                    for (int i = 0, quiet = 0; i < 1200 && (i < 60 || quiet < 30); i++)
                    {
                        yield return null;
                        quiet = renderer.ChunksMeshDeferred == 0 && renderer.ChunksMeshedThisFrame == 0 ? quiet + 1 : 0;
                    }

                    renderer.DressingLevels = false; renderer.BushLodBias = 1e6f;
                    Color32[] off = null!;
                    yield return Snap($"{framing.Name}-off", boot, target, p => off = p);
                    lines.Add($"{framing.Name} off: {renderer.DrawCalls} calls");

                    foreach (float bias in dressingBiases)
                    {
                        renderer.DressingLevels = true; renderer.DressingLodBias = bias; renderer.BushLodBias = 1e6f;
                        Color32[] shot = null!;
                        yield return Snap($"{framing.Name}-grass-{bias:0.##}", boot, target, p => shot = p);
                        lines.Add($"{framing.Name} grass bias {bias:0.##}: moved {Difference(off, shot) * 100f:0.00}%, " +
                                  $"{renderer.InstancesAtCoarserLevels} coarser, {renderer.DrawCalls} calls");
                    }
                    foreach (float bias in bushBiases)
                    {
                        renderer.DressingLevels = false; renderer.BushLodBias = bias;
                        Color32[] shot = null!;
                        yield return Snap($"{framing.Name}-bush-{bias:0.##}", boot, target, p => shot = p);
                        lines.Add($"{framing.Name} bush bias {bias:0.##}: moved {Difference(off, shot) * 100f:0.00}%, " +
                                  $"{renderer.InstancesAtCoarserLevels} coarser, {renderer.DrawCalls} calls");
                    }
                }
                Debug.Log("[Look] dressing levels: " + string.Join("; ", lines));

                // What the library made of the dressing: which kinds draw by level, and at what size.
                var modules = new List<string>();
                ModuleLibrary library = boot.Model!.Library;
                for (int i = 0; i < library.Count; i++)
                {
                    ResolvedModule m = library[i];
                    if (m.Id.IndexOf("dress", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    modules.Add($"{m.Id}: levels {m.Lods.Length}, by level {m.DrawsByLevel}, size {m.LodSize:0.0} m, " +
                                $"heights {string.Join("/", Array.ConvertAll(m.Lods, l => l.ScreenHeight.ToString("0.###")))}" +
                                (m.LevelNote.Length > 0 ? $", finest only: {m.LevelNote}" : string.Empty));
                }
                Debug.Log("[Look] dressing modules: " + string.Join("; ", modules));
            }
            finally
            {
                Time.timeScale = previousScale;
                if (renderer != null)
                {
                    renderer.DressingLevels = levelsWas; renderer.DressingLodBias = dressingWas; renderer.BushLodBias = bushWas;
                }
                if (cam != null) cam.targetTexture = previousTarget;
                if (target != null) target.Release();
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>
        /// Photographs the shadow changes of design 38 §18f for the owner to judge: noon and a low
        /// evening sun (long shadows show a cascade seam), at the start and the reference's wide
        /// framing, each as shipped, with the trees casting from the older proxy level, and with four
        /// cascades. Explicit: run by name; written to <c>Logs/look/shadow/</c>. The world is paused
        /// and the clock stilled; the cascade count is set on a runtime copy of the pipeline asset.
        /// </summary>
        [UnityTest, Explicit("photographs for judging the shadow changes, not a test")]
        public IEnumerator TheShadowChangesAtThePlayCamera()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                out OdysseyBootstrap boot);
            RenderTexture? target = null;
            UnityEngine.Camera? cam = null;
            RenderTexture? previousTarget = null;
            float previousScale = Time.timeScale;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            RenderPipelineAsset? qualityWas = QualitySettings.renderPipeline;
            RenderPipelineAsset? defaultWas = GraphicsSettings.defaultRenderPipeline;
            UniversalRenderPipelineAsset? copy = pipeline != null ? UnityEngine.Object.Instantiate(pipeline) : null;
            try
            {
                yield return null;
                ChunkRenderer renderer = boot.Renderer!;
                for (int i = 0; i < 120 && boot.World!.GameSpeed != 0; i++)
                {
                    boot.World!.Intents.Submit(new Intent(IntentKind.SetGameSpeed, default, 0));
                    yield return null;
                }
                Assert.That(boot.World!.GameSpeed, Is.Zero, "the world would not pause");
                Time.timeScale = 0f;
                if (copy != null)
                {
                    if (QualitySettings.renderPipeline != null) QualitySettings.renderPipeline = copy;
                    else GraphicsSettings.defaultRenderPipeline = copy;
                }

                cam = boot.cameraRig!.Camera;
                previousTarget = cam.targetTexture;
                target = new RenderTexture(1920, 1080, 24) { name = "shadow-look" };
                cam.targetTexture = target;
                Directory.CreateDirectory(Path.GetFullPath("Logs/look/shadow"));
#if UNITY_EDITOR
                var golden = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/OdysseyGoldenHour.asset");
                if (golden != null)
                {
                    var grade = new GameObject("Grade").AddComponent<Volume>();
                    grade.transform.SetParent(root.transform, false);
                    grade.isGlobal = true;
                    grade.sharedProfile = golden;
                }
#endif
                WorldSnapshot frame = boot.World!.Views.Current;
                var framings = new List<(string Name, float Distance)> { ("start", -1f) };
                if (frame.Pawns.Length > 0) framings.Add(("wide", 70f));
                var lines = new List<string>();
                foreach (var framing in framings)
                {
                    if (framing.Distance > 0f) boot.cameraRig!.FocusOn(frame.Pawns[0].Cell, framing.Distance);
                    foreach ((string hourName, float hour) in new[] { ("noon", 12f), ("evening", 19.5f) })
                    {
                        boot.DaylightHourOverride = hour;
                        for (int i = 0, quiet = 0; i < 1200 && (i < 60 || quiet < 30); i++)
                        {
                            yield return null;
                            quiet = renderer.ChunksMeshDeferred == 0 && renderer.ChunksMeshedThisFrame == 0 ? quiet + 1 : 0;
                        }
                        var arms = new (string Name, bool Simplest, int Cascades)[]
                        {
                            ("shipped", true, 2), ("old-proxy", false, 2), ("four-cascades", true, 4), ("before", false, 4),
                        };
                        Color32[] shipped = null!;
                        foreach (var arm in arms)
                        {
                            renderer.TreeShadowFromSimplest = arm.Simplest;
                            if (copy != null) copy.shadowCascadeCount = arm.Cascades;
                            Color32[] shot = null!;
                            yield return Snap($"../shadow/{framing.Name}-{hourName}-{arm.Name}", boot, target, p => shot = p);
                            if (arm.Name == "shipped") shipped = shot;
                            else lines.Add($"{framing.Name} {hourName} {arm.Name}: differs from shipped by {Difference(shipped, shot) * 100f:0.00}%");
                        }
                    }
                }
                boot.DaylightHourOverride = null;
                renderer.TreeShadowFromSimplest = true;
                Debug.Log("[Look] shadow changes: " + string.Join("; ", lines));
            }
            finally
            {
                Time.timeScale = previousScale;
                boot.DaylightHourOverride = null;
                QualitySettings.renderPipeline = qualityWas;
                GraphicsSettings.defaultRenderPipeline = defaultWas;
                if (copy != null) UnityEngine.Object.Destroy(copy);
                if (cam != null) cam.targetTexture = previousTarget;
                if (target != null) target.Release();
                UnityEngine.Object.Destroy(root);
            }
        }

        IEnumerator Snap(string name, OdysseyBootstrap boot, RenderTexture target, Action<Color32[]> pixels)
        {
            for (int i = 0; i < 8; i++) yield return null;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.GetFullPath($"Logs/look/lod/{name}.png"), image.EncodeToPNG());
            pixels(image.GetPixels32());
            UnityEngine.Object.Destroy(image);
        }

        IEnumerator PhotographTheGround(OdysseyBootstrap boot, RenderTexture target, string prefix)
        {
            ChunkRenderer renderer = boot.Renderer!;
            WorldSnapshot frame = boot.World!.Views.Current;
            PawnView first = frame.Pawns[0];
            Vector3 feet = CellMetrics.FloorCentre(first.Cell);
            if (!renderer.TryNearestBush(feet, first.Cell.Y - 1, out Vector3 bush))
            {
                Debug.Log("[Look] ground: no bush meshed near the colony, shot skipped");
                yield break;
            }

            // A stack of wood at the bush, and the colony paused so nobody hauls it away.
            var cell = new CellRef(Mathf.FloorToInt(bush.x / CellMetrics.SizeXZ),
                Mathf.FloorToInt(bush.z / CellMetrics.SizeXZ), first.Cell.Y);
            boot.World.Intents.Submit(new Intent(IntentKind.GiveResource, cell,
                Odyssey.Sim.Pawns.ItemIndex.Wood, 30));
            boot.World.Tick();
            boot.World.Intents.Submit(new Intent(IntentKind.SetGameSpeed, default, 0));
            boot.World.Tick();

            boot.cameraRig!.FocusOn(cell, 22f);
            renderer.ForceLyingForAPhotograph = true;
            if (boot.Figures != null) boot.Figures.ForceSleep = 1f;
            float margin = renderer.ItemMargin, lying = renderer.LyingClearance;
            try
            {
                renderer.ItemMargin = 0f;
                renderer.LyingClearance = 0f;
                boot.seeThroughToGround = false;
                for (int i = 0; i < 120; i++) yield return null;
                yield return Photograph(prefix + "ground-before", boot, target);

                renderer.ItemMargin = margin;
                renderer.LyingClearance = lying;
                boot.seeThroughToGround = true;
                for (int i = 0; i < 30; i++) yield return null;
                yield return Photograph(prefix + "ground-after", boot, target);
            }
            finally
            {
                renderer.ItemMargin = margin;
                renderer.LyingClearance = lying;
                boot.seeThroughToGround = true;
                renderer.ForceLyingForAPhotograph = false;
                if (boot.Figures != null) boot.Figures.ForceSleep = null;
            }
        }

        IEnumerator PhotographBehindATree(OdysseyBootstrap boot, RenderTexture target, string prefix)
        {
            var colony = boot.Colony!;
            var size = colony.Grid.Size;
            WorldSnapshot frame = boot.World!.Views.Current;
            CellRef start = frame.Pawns[0].Cell;
            Vector3 forward = boot.cameraRig!.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            // A tree near the colony, and a cell two beyond it along the camera's view: a colonist
            // there stands behind the crown from where the camera looks.
            for (int r = 2; r < 18; r++)
            for (int dz = -r; dz <= r; dz++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!size.Contains(x, z, start.Y)) continue;
                ushort edifice = boot.Model!.EdificeDef(size.Index(x, z, start.Y));
                if (!Odyssey.Sim.Worldgen.Natural.NaturalContent.IsTree(edifice)) continue;

                var behind = new CellRef(x + Mathf.RoundToInt(forward.x * 2f),
                    z + Mathf.RoundToInt(forward.z * 2f), start.Y);
                if (!size.Contains(behind.X, behind.Z, behind.Y)) continue;

                boot.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, behind, 0));
                boot.World.Tick();
                boot.World.Intents.Submit(new Intent(IntentKind.SetGameSpeed, default, 0));
                boot.World.Tick();
                boot.Directors?.Selection?.Clear();
                boot.cameraRig!.FocusOn(behind, 26f);

                try
                {
                    boot.seeThroughToEveryColonist = false;
                    for (int i = 0; i < 120; i++) yield return null;
                    yield return Photograph(prefix + "tree-before", boot, target);
                    boot.seeThroughToEveryColonist = true;
                    for (int i = 0; i < 30; i++) yield return null;
                    yield return Photograph(prefix + "tree-after", boot, target);
                    Debug.Log($"[Look] tree: {boot.SightLinesLastFrame} sight lines, " +
                              $"{boot.Renderer!.InstancesFaded} instances faded");
                }
                finally
                {
                    boot.seeThroughToEveryColonist = true;
                }
                yield break;
            }
            Debug.Log("[Look] tree: no tree near the colony, shot skipped");
        }

        IEnumerator Photograph(string name, OdysseyBootstrap boot, RenderTexture target)
        {
            for (int i = 0; i < 8; i++) yield return null;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            string path = Path.GetFullPath($"Logs/look/{name}.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            SliceCameraRig rig = boot.cameraRig!;
            Debug.Log($"[Look] {name}: {path}; distance {rig.TargetDistance:0.0} m, focus {rig.Focus}; " +
                      $"{boot.Renderer?.DrawCalls ?? -1} calls, {boot.Renderer?.InstancesDrawn ?? -1} instances, " +
                      $"{boot.Renderer?.ChunksDrawn ?? -1} chunks; surround " +
                      $"{boot.Renderer?.Skirt.TreeInstances ?? -1} near + {boot.Renderer?.Skirt.FarTreeInstances ?? -1} far trees, " +
                      $"{boot.Renderer?.Skirt.DrawCalls ?? -1} calls");
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
                               Action<double[]>? sections = null, Action<float>? gpu = null)
        {
            for (int i = 0; i < warmup; i++) yield return null;

            float total = 0f, worst = 0f;
            double tick = 0d, submit = 0d, gpuTotal = 0d;
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
                // Smoothed by the bootstrap over ~20 frames, which the warm-up absorbs; 0 where
                // the platform will not say, and the caller reports that rather than a zero.
                gpuTotal += boot.GpuFrameMs;
                System.ReadOnlySpan<double> split = boot.FrameSectionMs;
                for (int k = 0; k < sectionTotals.Length && k < split.Length; k++) sectionTotals[k] += split[k];
            }

            float meanMs = total / TimedFrames;
            mean(meanMs);
            gpu?.Invoke((float)(gpuTotal / TimedFrames));

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
