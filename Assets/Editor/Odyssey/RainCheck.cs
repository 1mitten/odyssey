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
    /// The rain look, photographed: the played meadow with a hut, a roof on posts and a paved path
    /// stamped beside the start, under each candidate way of drawing rain.
    ///
    /// <para><b>Why a sheet and not an argument.</b> The weather design's §7 draws rain with CPU
    /// particle systems; the review proposed GPU-procedural streaks and splashes, a cover map and a
    /// wet ground. Whether the difference reads from a 48° camera at 48 m is a question about
    /// pictures, so both are drawn here through the real chunk renderer and the real day, with the
    /// same cover map, and the owner looks.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.RainCheck.Run</c>. Writes
    /// <c>Logs/rain-&lt;variant&gt;-&lt;framing&gt;.png</c>.</para>
    /// </summary>
    public static class RainCheck
    {
        readonly struct Variant
        {
            public readonly string Name;
            public readonly string Preset;
            public readonly float Hour;
            public readonly bool GlossOnly;

            public Variant(string name, string preset, float hour = 10.5f, bool glossOnly = false)
            {
                Name = name;
                Preset = preset;
                Hour = hour;
                GlossOnly = glossOnly;
            }
        }

        /// <summary>
        /// The skies, each one of the Weather tab's own presets (<see cref="DebugDirector.WeatherPresets"/>),
        /// so the sheet and Play cannot disagree about what "Rain" looks like. Rain is drawn twice,
        /// once each way the owner is choosing between for wet ground (2026-09-25).
        /// </summary>
        static readonly Variant[] Variants =
        {
            new Variant("0-clear", DebugDirector.WeatherClearKey),
            new Variant("1-rain-richer", DebugDirector.WeatherRainKey),
            new Variant("2-rain-gloss", DebugDirector.WeatherRainKey, glossOnly: true),
            new Variant("3-drizzle", DebugDirector.WeatherDrizzleKey),
            new Variant("4-downpour", DebugDirector.WeatherDownpourKey),
            new Variant("5-storm", DebugDirector.WeatherStormKey),
            new Variant("6-overcast", DebugDirector.WeatherOvercastKey),
            new Variant("7-dusk-rain", DebugDirector.WeatherRainKey, hour: 19f),
        };

        readonly struct Framing
        {
            public readonly string Name;
            public readonly float Pitch, Distance;
            public readonly bool AtPond;

            public Framing(string name, float pitch, float distance, bool atPond = false)
            {
                Name = name;
                Pitch = pitch;
                Distance = distance;
                AtPond = atPond;
            }
        }

        /// <summary>The play camera's reach runs to 160 m; "max" is that, where the screen layer is all there is.</summary>
        static readonly Framing[] Framings =
        {
            new Framing("play", 48f, 48f),
            new Framing("close", 42f, 18f),
            new Framing("far", 48f, 120f),
            new Framing("max", 48f, 160f),
            new Framing("pond", 45f, 22f, atPond: true),
        };

        [MenuItem("Odyssey/Presentation/Check the rain")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        static DebugDirector.WeatherPreset PresetOf(string key) =>
            Array.Find(DebugDirector.WeatherPresets, p => p.Key == key);

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
            Action<ScriptableRenderContext, Camera>? hook = null;

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
                CellRef hutAt = StampShelters(grid, edifices, size, result.StartCell);
                CellRef pond = NearestWater(grid, size, result.StartCell);

                lightingRoot = new GameObject("RainRoot");
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
                // The whole board meshed before the first picture. The per-frame meshing budget
                // otherwise lets the world arrive eleven chunks a shot, and the first sheet's
                // opening pictures were of an empty sky.
                renderer.PrimeAll(activeLayer, slice);

                // The game's own weather object, driven the way the Weather tab drives it: one
                // preset snapped to rather than eased into, since a sheet has no game time.
                look = new WeatherLook(model, lightingRoot.transform);
                if (!look.Drawer.Available) throw new InvalidOperationException("Odyssey/Rain did not load");
                wind = new WindDirector();

                cameraObject = new GameObject("RainCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;

                // A fixed tick, so the drops are in the same places in every picture.
                const long Tick = 223;
                Variant current = Variants[0];
                Vector3 shotFocus = Vector3.zero;
                float shotDistance = 48f;

                ChunkRenderer active = renderer;
                WeatherLook weather = look;
                DaylightDirector light = daylight;
                WindDirector air = wind;
                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    active.ViewerPosition = rendering.transform.position;
                    active.Render(activeLayer, slice);
                    weather.Sync(PresetOf(current.Preset), false, light, rendering, shotFocus, shotDistance,
                        underground: false, Tick, 60, running: false, 0f, current.GlossOnly, air);
                };
                RenderPipelineManager.beginCameraRendering += hook;

                Vector3 hutFocus = CellMetrics.FloorCentre(hutAt);
                Vector3 pondFocus = CellMetrics.FloorCentre(pond);

                foreach (Variant variant in Variants)
                {
                    current = variant;
                    DebugDirector.WeatherPreset preset = PresetOf(variant.Preset);
                    look.Snap(preset);
                    // Once without a camera, so the light, the volume and the wind are set before
                    // the day is applied and the shutter opens.
                    look.Sync(preset, false, daylight, null, hutFocus, 48f, false, Tick, 60, false, 0f,
                        variant.GlossOnly, wind);
                    wind.Apply(5_000);
                    daylight.ApplyHour(variant.Hour);

                    foreach (Framing framing in Framings)
                    {
                        shotFocus = framing.AtPond ? pondFocus : hutFocus;
                        shotDistance = framing.Distance;
                        string path = $"Logs/rain-{variant.Name}-{framing.Name}.png";
                        PlayScene.Shoot(camera, shotFocus, framing.Pitch, 38f, framing.Distance, path);
                        Debug.Log($"[Rain] {path}: streaks {look.Drawer.LastStreaks} splashes {look.Drawer.LastSplashes} " +
                                  $"screen layer {look.Drawer.LastScreenLayer:0.00}, {look.Drawer.LastDrawCalls} rain calls; " +
                                  $"cloud {look.Cloud:0.00} gloom {look.Gloom:0.00}");
                    }
                }

                Debug.Log($"[Rain] wrote {Variants.Length * Framings.Length} pictures to Logs/rain-*.png");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rain] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
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

        /// <summary>
        /// A 4 × 4 hut with a doorway and a wooden roof, a 3 × 3 roof on four posts beside it, and a
        /// paved path between them, on the first patch of flat open ground found near the start.
        /// Returns the hut's corner cell.
        /// </summary>
        static CellRef StampShelters(CellGrid grid, List<PlacedEdifice> edifices, GridSize size, CellRef start)
        {
            int y = start.Y;
            for (int ring = 3; ring < 30; ring++)
            for (int dz = -ring; dz <= ring; dz++)
            for (int dx = -ring; dx <= ring; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring) continue;
                int x0 = start.X + dx, z0 = start.Z + dz;
                if (!Flat(grid, size, x0, z0, 10, 5, y)) continue;

                ushort wood = NaturalContent.StuffWood;
                // The hut: walls round a 4 × 4, a doorway in the middle of the south side.
                for (int z = z0; z < z0 + 4; z++)
                for (int x = x0; x < x0 + 4; x++)
                {
                    Clear(grid, edifices, size, x, z, y);
                    bool edge = x == x0 || x == x0 + 3 || z == z0 || z == z0 + 3;
                    bool door = z == z0 && x == x0 + 1;
                    if (edge && !door) Place(grid, edifices, size.Index(x, z, y), CoreContent.EdificeWall, wood, true);
                    Roof(grid, size.Index(x, z, y + 1), wood);
                }

                // The roof on posts, one cell east of the hut.
                int px = x0 + 5;
                for (int z = z0; z < z0 + 3; z++)
                for (int x = px; x < px + 3; x++)
                {
                    Clear(grid, edifices, size, x, z, y);
                    bool corner = (x == px || x == px + 2) && (z == z0 || z == z0 + 2);
                    if (corner) Place(grid, edifices, size.Index(x, z, y), CoreContent.EdificePillar, wood, true);
                    Roof(grid, size.Index(x, z, y + 1), wood);
                }

                // A paved path south of both, so splashes on a hard floor are in the picture.
                for (int x = x0; x < px + 3; x++)
                {
                    Clear(grid, edifices, size, x, z0 - 2, y);
                    int index = size.Index(x, z0 - 2, y);
                    grid.Floor[index] = CoreContent.SlabPaved;
                    grid.FloorStuff[index] = CoreContent.StuffConcrete;
                }

                return new CellRef(x0, z0, y);
            }

            throw new InvalidOperationException("no flat ground near the start for the shelters");
        }

        static bool Flat(CellGrid grid, GridSize size, int x0, int z0, int w, int d, int y)
        {
            for (int z = z0 - 2; z < z0 + d; z++)
            for (int x = x0; x < x0 + w; x++)
            {
                if (!size.Contains(x, z, y) || !size.Contains(x, z, y + 1) || y < 1) return false;
                if ((grid.Flags[size.Index(x, z, y - 1)] & CellFlags.SolidTerrain) == 0) return false;
                if ((grid.Flags[size.Index(x, z, y)] & CellFlags.SolidTerrain) != 0) return false;
                if (NaturalContent.IsWater(grid.Terrain[size.Index(x, z, y)])) return false;
            }
            return true;
        }

        static void Clear(CellGrid grid, List<PlacedEdifice> edifices, GridSize size, int x, int z, int y)
        {
            int index = size.Index(x, z, y);
            int handle = grid.Edifice[index];
            if (handle < 0) return;
            PlacedEdifice placed = edifices[handle];
            placed.Removed = true;
            edifices[handle] = placed;
            grid.Edifice[index] = -1;
            grid.Flags[index] &= ~CellFlags.BlockingEdifice;
        }

        static void Place(CellGrid grid, List<PlacedEdifice> edifices, int index, ushort def, ushort stuff, bool blocking)
        {
            edifices.Add(new PlacedEdifice { CellIndex = index, Def = def, Stuff = stuff });
            grid.Edifice[index] = edifices.Count - 1;
            if (blocking) grid.Flags[index] |= CellFlags.BlockingEdifice;
        }

        static void Roof(CellGrid grid, int above, ushort stuff)
        {
            grid.Floor[above] = CoreContent.SlabBuilt;
            grid.FloorStuff[above] = stuff;
        }

        static CellRef NearestWater(CellGrid grid, GridSize size, CellRef start)
        {
            CellRef best = start;
            int bestD = int.MaxValue;
            for (int y = 0; y < size.SizeY; y++)
            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
            {
                int index = size.Index(x, z, y);
                if (!NaturalContent.IsWater(grid.Terrain[index])) continue;
                int d = Math.Abs(x - start.X) + Math.Abs(z - start.Z);
                if (d >= bestD) continue;
                bestD = d;
                best = new CellRef(x, z, y);
            }
            return best;
        }
    }
}
