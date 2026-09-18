#nullable enable
using System;
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
    /// <b>Slabs laid on the ground do not sit flush with one another.</b>
    ///
    /// <para>The owner, 2026-09-18, two screenshots: <i>"these slabs leave small artifacts/lines or
    /// gaps that don't even up … you can notice this when you look at the ground from certain
    /// angles."</i> Their shots are of a wood slab field laid <b>on the meadow</b>, at night, at the
    /// play camera's pitch.</para>
    ///
    /// <para><see cref="SeamProbe"/> already answered the same question for a deck with <b>nothing
    /// underneath it</b> and found no difference worth seeing between relief on, relief off and the
    /// walls taken away. This probe changes the one thing that probe held fixed: the slabs sit on
    /// the board's own ground, which is the case the owner is photographing and the case where a
    /// tile has a second surface eight millimetres beneath it
    /// (<see cref="CellMetrics.SlabLift"/>).</para>
    ///
    /// <para>Three variables, one at a time. <b>Relief</b> on and off, because two neighbours
    /// sheared onto their own tangent planes part company at their shared corners by up to 15 mm —
    /// which is a tenth of a pixel at 30 m and two pixels at 12. <b>Pitch</b>, because the report
    /// says <i>from certain angles</i> and a step is widest on screen when the camera is lowest.
    /// <b>Range</b>, because a depth fight grows with distance and a geometric step does not.</para>
    ///
    /// <para><c>scripts/unity.sh shot Odyssey.EditorTools.SlabFlushProbe.Run</c></para>
    /// </summary>
    public static class SlabFlushProbe
    {
        const int Patch = 8;

        [MenuItem("Odyssey/Presentation/Probe a slab field laid on the ground")]
        public static void RunFromMenu() => Shoot(exitWhenDone: false);

        public static void Run() => Shoot(Application.isBatchMode);

        static void Shoot(bool exitWhenDone)
        {
            Execute(exitWhenDone);
            if (!exitWhenDone) ShotFolder.Reveal("flush-*.png");
        }

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            ChunkRenderer? renderer = null;
            ModuleLibrary? library = null;
            GameObject? lightingRoot = null;
            GameObject? cameraObject = null;
            Action<ScriptableRenderContext, Camera>? hook = null;

            float amplitudeWas = GroundRelief.Amplitude;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                var size = new GridSize(PlayScene.PlaySizeXZ, PlayScene.PlaySizeXZ, PlayScene.PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
                gen.MakeWooded();

                var grid = new CellGrid(size);
                var chunks = new ChunkGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);
                var slice = new SliceSettings();

                library = new ModuleLibrary(catalogue);
                var model = new WorldRenderModel(size, chunks, library);

                // A patch of wood slabs laid straight on the meadow: every cell of it stands on
                // solid ground, which is the whole difference between this and SeamProbe's deck.
                CellRef start = result.StartCell;
                int laid = 0;
                for (int j = 0; j < Patch; j++)
                for (int i = 0; i < Patch; i++)
                {
                    int x = start.X + i - Patch / 2, z = start.Z + j - Patch / 2;
                    if (!size.Contains(x, z, start.Y)) continue;

                    int cell = size.Index(x, z, start.Y);
                    if (grid.IsSolidTerrain(cell)) continue;
                    if (start.Y == 0 || !grid.IsSolidTerrain(size.Index(x, z, start.Y - 1))) continue;

                    grid.Floor[cell] = CoreContent.SlabBuilt;
                    grid.FloorStuff[cell] = NaturalContent.StuffWood;
                    laid++;
                }

                if (laid == 0)
                {
                    Debug.LogError("[Flush] no slab was laid on the ground.");
                    exitCode = 1;
                    return;
                }

                lightingRoot = new GameObject("FlushRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                slice.surfaceLayer = start.Y;
                int activeLayer = start.Y;

                cameraObject = new GameObject("FlushCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);

                renderer = new ChunkRenderer(model);

                ChunkRenderer active = renderer;
                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    active.ViewerPosition = rendering.transform.position;
                    active.Render(activeLayer, slice);
                };
                RenderPipelineManager.beginCameraRendering += hook;

                Vector3 middle = CellMetrics.FloorCentre(start.X, start.Z, start.Y);

                GroundRelief.Amplitude = GroundRelief.BoardAmplitude;
                model.RefreshAll(grid, result.Edifices);
                Vector3 focus = GroundRelief.Lift(middle);
                PlayScene.Shoot(camera, focus, 48f, 45f, 12f, "Logs/flush-relief-48deg-12m.png");
                PlayScene.Shoot(camera, focus, 25f, 45f, 12f, "Logs/flush-relief-25deg-12m.png");
                PlayScene.Shoot(camera, focus, 48f, 45f, 30f, "Logs/flush-relief-48deg-30m.png");

                GroundRelief.Amplitude = 0f;
                model.RefreshAll(grid, result.Edifices);
                PlayScene.Shoot(camera, middle, 48f, 45f, 12f, "Logs/flush-flat-48deg-12m.png");
                PlayScene.Shoot(camera, middle, 25f, 45f, 12f, "Logs/flush-flat-25deg-12m.png");
                PlayScene.Shoot(camera, middle, 48f, 45f, 30f, "Logs/flush-flat-30m.png");

                // And the same flat board with every shadow switched off. Neither growing the tiles
                // nor staggering them by a millimetre moved the dotted grid one pixel, so the next
                // question is whether the line is geometry at all: a tile that shadow-maps its own
                // neighbour draws exactly this, on exactly the cell pitch, and is not moved by
                // anything done to the tile's placement at these magnitudes.
                var lights = lightingRoot.GetComponentsInChildren<Light>();
                var shadowsWere = new LightShadows[lights.Length];
                for (int i = 0; i < lights.Length; i++)
                {
                    shadowsWere[i] = lights[i].shadows;
                    lights[i].shadows = LightShadows.None;
                }

                PlayScene.Shoot(camera, middle, 25f, 45f, 12f, "Logs/flush-noshadow-25deg-12m.png");

                for (int i = 0; i < lights.Length; i++) lights[i].shadows = shadowsWere[i];

                // And a seam from close enough to see what it is made of. Everything above argues
                // from a dotted line one pixel wide, which is the one width at which a rim, a gap
                // and a depth tie all look the same. Three metres away and square on to the seam,
                // a millimetre is a third of a pixel and a centimetre is three, so whatever is
                // there has to show its width.
                Vector3 seam = middle + new Vector3(CellMetrics.HalfXZ, 0f, 0f);
                PlayScene.Shoot(camera, seam, 10f, 0f, 3f, "Logs/flush-seam-close-10deg-3m.png");
                PlayScene.Shoot(camera, seam, 30f, 0f, 3f, "Logs/flush-seam-close-30deg-3m.png");

                RenderPipelineManager.beginCameraRendering -= hook;
                hook = null;

                Debug.Log($"[Flush] {laid} wood slabs laid on the ground at L{start.Y}. Wrote " +
                          "Logs/flush-{relief,flat}-{48deg,25deg}-{12m,30m}.png. " +
                          "Lines on the relief shots and not the flat ones = the drape's shear; " +
                          "lines on both = the slab over the ground beneath it.");
            }
            catch (Exception error)
            {
                Debug.LogError($"[Flush] {error}");
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                renderer?.Dispose();
                library?.Dispose();
                GroundRelief.Amplitude = amplitudeWas;
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }
    }
}
