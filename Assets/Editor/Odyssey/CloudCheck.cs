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
    /// The clouds, photographed (design 63): the played meadow from a colonist's eye, towards the
    /// sun and away from it and looking up, and from the colony camera at its lowest pitch, through
    /// the day and under each sky, with a clear noon drawn once without clouds as the control.
    ///
    /// <para>The same objects the game uses — the chunk renderer with its surround, the daylight,
    /// the weather and <see cref="CloudDirector"/> — driven the way <see cref="RainCheck"/> drives
    /// them, so the sheet and Play cannot disagree.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.CloudCheck.Run</c>. Writes
    /// <c>Logs/clouds-&lt;variant&gt;-&lt;framing&gt;.png</c>.</para>
    /// </summary>
    public static class CloudCheck
    {
        readonly struct Variant
        {
            public readonly string Name, Preset;
            public readonly float Hour;
            public readonly bool Clouds;

            public Variant(string name, string preset, float hour, bool clouds = true)
            {
                Name = name;
                Preset = preset;
                Hour = hour;
                Clouds = clouds;
            }
        }

        static readonly Variant[] Variants =
        {
            new Variant("00-noon-clear-off", DebugDirector.WeatherClearKey, 13f, clouds: false),
            new Variant("01-noon-clear", DebugDirector.WeatherClearKey, 13f),
            new Variant("02-dawn-clear", DebugDirector.WeatherClearKey, 7f),
            new Variant("03-morning-clear", DebugDirector.WeatherClearKey, 10.5f),
            new Variant("04-dusk-clear", DebugDirector.WeatherClearKey, 19f),
            new Variant("05-night-clear", DebugDirector.WeatherClearKey, 23f),
            new Variant("05a-fading-clear", DebugDirector.WeatherClearKey, 20.25f),
            new Variant("05b-returning-clear", DebugDirector.WeatherClearKey, 5.75f),
            // The last of the fade, drawn and not: the pair a pixel diff says whether anything is left to snap.
            new Variant("05c-almost-gone", DebugDirector.WeatherClearKey, 20.9f),
            new Variant("05c-almost-gone-off", DebugDirector.WeatherClearKey, 20.9f, clouds: false),
            new Variant("05d-halfway-in", DebugDirector.WeatherClearKey, 5.75f),
            new Variant("09-dusk-storm", DebugDirector.WeatherStormKey, 19f),
            new Variant("06-noon-overcast", DebugDirector.WeatherOvercastKey, 13f),
            new Variant("07-noon-rain", DebugDirector.WeatherRainKey, 13f),
            new Variant("08-noon-storm", DebugDirector.WeatherStormKey, 13f),
        };

        readonly struct Framing
        {
            public readonly string Name;
            public readonly float Pitch, Distance, Lift, YawFromSun;

            public Framing(string name, float pitch, float distance, float lift, float yawFromSun)
            {
                Name = name;
                Pitch = pitch;
                Distance = distance;
                Lift = lift;
                YawFromSun = yawFromSun;
            }
        }

        /// <summary>
        /// A colonist's eye is about 2 m up and looks a few degrees down (the ride camera's
        /// <c>EyePitch</c>); the colony camera's lowest pitch is 20° at 160 m. Two of the eye's
        /// pictures are shot with a wider lens, the ride camera's 60°.
        /// </summary>
        static readonly Framing[] Framings =
        {
            new Framing("eye-sun", 4f, 0.01f, 2.2f, 0f),
            new Framing("eye-away", 4f, 0.01f, 2.2f, 180f),
            new Framing("eye-up", -30f, 0.01f, 2.2f, 90f),
            new Framing("colony-low", 20f, 160f, 0f, 150f),
        };

        [MenuItem("Odyssey/Presentation/Check the clouds")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        /// <summary>A colour as 8-bit sRGB, to set beside a sampled pixel.</summary>
        static string Hex(Color c) => $"({(int)(c.r * 255f)},{(int)(c.g * 255f)},{(int)(c.b * 255f)})";

        static WeatherView SkyOf(string key)
        {
            DebugDirector.WeatherPreset preset = Array.Find(DebugDirector.WeatherPresets, p => p.Key == key);
            return Odyssey.Sim.Weather.WeatherSystem.Terms(WorldContent.Weathers[(int)preset.Kind], preset.IntensityPerMille);
        }

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            GameObject? lightingRoot = null;
            GameObject? cameraObject = null;
            ChunkRenderer? renderer = null;
            ModuleLibrary? library = null;
            DaylightDirector? daylight = null;
            WeatherLook? look = null;
            WindDirector? wind = null;
            CloudDirector? clouds = null;
            Action<ScriptableRenderContext, Camera>? hook = null;

            try
            {
                MeadowLook? art = MeadowLook.Loaded;
                if (art == null || !art.HasClouds)
                    throw new InvalidOperationException("the Meadow cloud art did not resolve; rebuild the meadow look on a machine with the packs");

                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                var size = new GridSize(PlayScene.PlaySizeXZ, PlayScene.PlaySizeXZ, PlayScene.PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
                gen.MakeWooded();
                var grid = new CellGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);
                int activeLayer = result.StartCell.Y;
                var edifices = new List<PlacedEdifice>(result.Natural!.Context.Edifices);

                lightingRoot = new GameObject("CloudRoot");
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

                look = new WeatherLook(model, lightingRoot.transform);
                wind = new WindDirector();
                clouds = new CloudDirector(model, art);

                cameraObject = new GameObject("CloudCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 1800f;
                camera.clearFlags = CameraClearFlags.Skybox;

                const long Tick = 223;
                Variant current = Variants[0];
                ChunkRenderer active = renderer;
                WeatherLook weather = look;
                DaylightDirector light = daylight;
                WindDirector air = wind;
                CloudDirector sky = clouds;
                Vector3 focus = CellMetrics.FloorCentre(result.StartCell);
                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    active.ViewerPosition = rendering.transform.position;
                    active.Render(activeLayer, slice);
                    weather.Sync(SkyOf(current.Preset), false, light, rendering, focus, 48f,
                        underground: false, Tick, 60, running: false, 0f, false, air);
                    sky.Enabled = current.Clouds;
                    sky.Sync(rendering, 0f, 100f, weather.Cloud, weather.Gloom, weather.Rain, weather.Wind, light, underground: false);
                };
                RenderPipelineManager.beginCameraRendering += hook;

                // One picture thrown away first. The first frame the pack's shader drew in a fresh
                // editor came out black on the shadowed faces and never again (design 63 §8b), so
                // without this the sheet's first cloud picture is of the editor, not the clouds.
                current = Variants[1];
                daylight.ApplyHour(current.Hour);
                camera.fieldOfView = 60f;
                PlayScene.Shoot(camera, focus + Vector3.up * 2.2f, 4f, daylight.State.SunAzimuth, 0.01f, "Logs/clouds-warmup.png");

                foreach (Variant variant in Variants)
                {
                    current = variant;
                    WeatherView preset = SkyOf(variant.Preset);
                    look.Snap(preset);
                    look.Sync(preset, false, daylight, null, focus, 48f, false, Tick, 60, false, 0f, false, wind);
                    wind.Apply(5_000);
                    daylight.ApplyHour(variant.Hour);
                    float sunYaw = daylight.State.SunAzimuth;

                    foreach (Framing framing in Framings)
                    {
                        bool eye = framing.Lift > 0f;
                        camera.fieldOfView = eye ? 60f : 40f;
                        Vector3 at = focus + Vector3.up * framing.Lift;
                        string path = $"Logs/clouds-{variant.Name}-{framing.Name}.png";
                        PlayScene.Shoot(camera, at, framing.Pitch, sunYaw + framing.YawFromSun, framing.Distance, path);
                        Material m = clouds.Material!;
                        Debug.Log($"[Clouds] {path}: {clouds.LastDrawCalls} calls, seen {clouds.LastVisible}, " +
                                  $"cloud {look.Cloud:0.00} gloom {look.Gloom:0.00} rain {look.Rain:0.00}, sun {sunYaw:0}°, " +
                                  $"presence {clouds.LastPresence:0.00}; top {Hex(m.GetColor("_Top_Color"))} " +
                                  $"under {Hex(m.GetColor("_Base_Color"))} rim {Hex(m.GetColor("_Fresnel_Color"))} " +
                                  $"sky behind {Hex(CloudColours.SkyAt(daylight.State))}");
                    }
                }

                Debug.Log($"[Clouds] wrote {Variants.Length * Framings.Length} pictures to Logs/clouds-*.png");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Clouds] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                clouds?.Dispose();
                look?.Dispose();
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
