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
        IEnumerator TimeFrames(string label, OdysseyBootstrap boot, int warmup, Action<float> mean)
        {
            for (int i = 0; i < warmup; i++) yield return null;

            float total = 0f, worst = 0f;
            double tick = 0d, submit = 0d;
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
        }

        /// <summary>The play scene's objects, built by hand: a camera with the rig, a sun, the bootstrap.</summary>
        static GameObject Build(Odyssey.Sim.Worldgen.Natural.MapType mapType, bool barren, out OdysseyBootstrap boot)
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
            boot.sizeX = 120;
            boot.sizeZ = 120;
            boot.layers = 16;
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
