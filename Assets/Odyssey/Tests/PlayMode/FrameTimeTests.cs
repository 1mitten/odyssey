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

                for (int i = 0; i < WarmupFrames; i++) yield return null;

                float total = 0f, worst = 0f;
                for (int i = 0; i < TimedFrames; i++)
                {
                    yield return null;
                    float ms = Time.unscaledDeltaTime * 1000f;
                    total += ms;
                    if (ms > worst) worst = ms;
                }

                float mean = total / TimedFrames;
                ChunkRenderer? renderer = boot.Renderer;
                // Resolution matters to the reading: a fullscreen pass or a sky costs per pixel,
                // and the batch game view is not the player's monitor.
                Debug.Log($"[FrameTime] {label}: mean {mean:0.00} ms, worst {worst:0.00} ms over {TimedFrames} frames; " +
                          $"{renderer?.DrawCalls ?? 0} draw calls, {renderer?.InstancesDrawn ?? 0} instances, " +
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

                Assert.That(mean, Is.LessThan(CeilingMs),
                    "the play world takes longer than a 30 Hz frame on a development machine");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
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
