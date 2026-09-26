#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// The butterflies, photographed (design 52): the played meadow through the real chunk renderer
    /// and the real day, with the butterfly director drawing into it — by day, at dusk and at night,
    /// from a few metres to the camera's full reach.
    ///
    /// <para><b>Why a sheet.</b> The tests prove the draw is one call, the habitat is grass and the
    /// palette survives colour blindness; none of them has seen a wing. Whether the pattern reads,
    /// whether a lit wing reads as a lit butterfly, and whether a far night is coloured specks or
    /// noise are questions about pictures, and the owner looks.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.ButterflyCheck.Run</c>. Writes
    /// <c>Logs/butterflies-&lt;hour&gt;-&lt;framing&gt;.png</c>.</para>
    /// </summary>
    public static class ButterflyCheck
    {
        readonly struct Sky
        {
            public readonly string Name;
            public readonly float Hour;

            public Sky(string name, float hour)
            {
                Name = name;
                Hour = hour;
            }
        }

        static readonly Sky[] Skies =
        {
            new Sky("day", 10.5f),
            new Sky("dusk", 20.8f),
            new Sky("night", 23f),
        };

        readonly struct Framing
        {
            public readonly string Name;
            public readonly float Pitch, Distance;

            public Framing(string name, float pitch, float distance)
            {
                Name = name;
                Pitch = pitch;
                Distance = distance;
            }
        }

        static readonly Framing[] Framings =
        {
            new Framing("close", 40f, 7f),
            new Framing("near", 44f, 18f),
            new Framing("play", 48f, 48f),
            new Framing("far", 48f, 110f),
            new Framing("max", 48f, 160f),
        };

        [MenuItem("Odyssey/Presentation/Check the butterflies")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            GameObject? lightingRoot = null;
            GameObject? cameraObject = null;
            ChunkRenderer? renderer = null;
            ModuleLibrary? library = null;
            DaylightDirector? daylight = null;
            WindDirector? wind = null;
            ButterflyDirector? butterflies = null;
            Action<ScriptableRenderContext, Camera>? hook = null;
            bool fullSeason = ButterflyMeadow.FullSeason;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                var size = new GridSize(PlayScene.PlaySizeXZ, PlayScene.PlaySizeXZ, PlayScene.PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
                gen.MakeWooded();   // the board the scene loads
                var grid = new CellGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);
                int activeLayer = result.StartCell.Y;
                var edifices = new List<PlacedEdifice>(result.Natural!.Context.Edifices);

                lightingRoot = new GameObject("ButterflyRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);
                Light? sun = lightingRoot.GetComponentInChildren<Light>();
                if (sun == null) throw new InvalidOperationException("the lighting rig built no sun");
                daylight = new DaylightDirector(sun, RenderSettings.skybox);

                library = new ModuleLibrary(catalogue);
                var chunks = new ChunkGrid(size);
                var model = new WorldRenderModel(size, chunks, library);
                model.RefreshAll(grid, edifices);
                renderer = new ChunkRenderer(model);
                var slice = new SliceSettings { surfaceLayer = activeLayer };
                renderer.PrimeAll(activeLayer, slice);

                wind = new WindDirector();
                wind.Apply(5_000);

                butterflies = new ButterflyDirector(model, 1u) { Capacity = SettingsDirector.ShippedButterflies };
                if (!butterflies.Available) throw new InvalidOperationException("Odyssey/Butterfly did not load");
                ButterflyMeadow.FullSeason = true;

                Vector3 focus = CellMetrics.FloorCentre(result.StartCell);

                // Six seconds of meadow before the shutter, stepped directly rather than through the
                // director's Sync: a Sync submits a draw, and draws submitted with no camera rendering
                // would all land on the first picture.
                long tick = Calendar.TicksPerDay * 6;
                var sky = new ButterflyConditions(tick, 0f, 0f, 0f, 0f);
                float radius = ButterflyMeadow.RadiusFor(48f);
                butterflies.Habitat.SyncDirty();
                for (int f = 0; f < 360; f++)
                    butterflies.Meadow.Step(1f / 60f, sky, butterflies.Habitat, focus.x, focus.z, radius,
                        ReadOnlySpan<float>.Empty);

                cameraObject = new GameObject("ButterflyCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;

                Sky current = Skies[0];
                float shotDistance = 48f;
                ChunkRenderer active = renderer;
                ButterflyDirector drawer = butterflies;
                int lowest = Math.Max(0, slice.LowestDrawnLayer(activeLayer, model.LowestOutdoorLayer));
                int highest = slice.HighestVisibleLayer(activeLayer, size.SizeY);
                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    active.ViewerPosition = rendering.transform.position;
                    active.Render(activeLayer, slice);
                    // Nought seconds: the meadow holds still for the picture and is only drawn.
                    drawer.Sync(0f, tick, current.Hour, 0f, 0f, null, focus, shotDistance, rendering.transform.position,
                        lowest, highest, false);
                };
                RenderPipelineManager.beginCameraRendering += hook;

                foreach (Sky s in Skies)
                {
                    current = s;
                    daylight.ApplyHour(s.Hour);
                    foreach (Framing framing in Framings)
                    {
                        shotDistance = framing.Distance;
                        string path = $"Logs/butterflies-{s.Name}-{framing.Name}.png";
                        PlayScene.Shoot(camera, focus, framing.Pitch, 38f, framing.Distance, path);
                        Debug.Log($"[Butterflies] {path}: {butterflies.LastDrawn} drawn of {butterflies.Meadow.Live} live " +
                                  $"(target {butterflies.Meadow.Target}), {butterflies.LastDrawCalls} calls, night {butterflies.LastNight:0.00}");
                    }
                }

                Debug.Log($"[Butterflies] wrote {Skies.Length * Framings.Length} pictures to Logs/butterflies-*.png");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Butterflies] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                ButterflyMeadow.FullSeason = fullSeason;
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                butterflies?.Dispose();
                wind?.Dispose();
                daylight?.Dispose();
                renderer?.Dispose();
                library?.Dispose();
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }
    }
}
