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
    /// <b>Does a floor laid on the ground fight the ground it is laid on?</b>
    ///
    /// <para>`18-paving.md` §3 names this as U42's one real risk and says to measure it before a
    /// line of the unit is written. A covering is a slab written into a cell that <em>already</em>
    /// has solid terrain beneath it, so the drawn slab and the top face of that ground block are
    /// coplanar, and two coplanar surfaces z-fight — a shimmering, distance-dependent mess that no
    /// test can see and that would be discovered late and blamed on something else.</para>
    ///
    /// <para><b>It needs no part of U42 to exist.</b> A covering's geometry is entirely decided by
    /// where <c>ChunkMesher.EmitFloor</c> puts a slab, and that does not care which kind of slab it
    /// is — so writing <c>Floor[]</c> and <c>FloorStuff[]</c> directly over grass produces exactly
    /// the pixels U42 would produce. Nothing in the simulation is touched and nothing here is a
    /// model of the feature; it is the feature's drawing, borrowed early.</para>
    ///
    /// <para><b>Shot at three ranges, because z-fighting is a depth-precision artefact and gets
    /// worse with distance.</b> A close shot can be clean while the playing camera shimmers, so a
    /// close-only answer would be worse than none.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.PavingProbe.Run</c>. Needs a
    /// real graphics device, so not under <c>-nographics</c>.</para>
    /// </summary>
    public static class PavingProbe
    {
        /// <summary>How big a patch to pave, in cells. Big enough to fill a frame at 120 m.</summary>
        const int Patch = 14;

        [MenuItem("Odyssey/Presentation/Probe paving over ground")]
        public static void RunFromMenu() => Shoot(exitWhenDone: false);

        public static void Run() => Shoot(Application.isBatchMode);

        static void Shoot(bool exitWhenDone)
        {
            Execute(exitWhenDone);
            if (!exitWhenDone) ShotFolder.Reveal("paving-*.png");
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
            float periodWas = GroundRelief.Period;

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

                GroundRelief.Amplitude = GroundRelief.BoardAmplitude;
                GroundRelief.Period = 150f;

                library = new ModuleLibrary(catalogue);
                var model = new WorldRenderModel(size, chunks, library);
                model.RefreshAll(grid, result.Edifices);

                // Pave a patch of open grass: a slab in a cell that already has solid ground under
                // it, which is precisely what U42 would write and precisely the coplanar case.
                CellRef start = result.StartCell;
                int laid = 0, skipped = 0;
                for (int j = 0; j < Patch; j++)
                for (int i = 0; i < Patch; i++)
                {
                    int x = start.X + i - Patch / 2, z = start.Z + j - Patch / 2;
                    if (!size.Contains(x, z, start.Y)) continue;

                    int cell = size.Index(x, z, start.Y);

                    // Only where a covering could legally go: open air with ground beneath. Cells
                    // with a tree or a stream in them are skipped rather than paved over, so the
                    // picture shows the case U42 would actually produce.
                    if (grid.IsSolidTerrain(cell) || grid.Edifice[cell] >= 0
                        || NaturalContent.IsWater(grid.Terrain[cell]) || !grid.HasFloor(cell))
                    {
                        skipped++;
                        continue;
                    }

                    grid.Floor[cell] = CoreContent.SlabBuilt;
                    grid.FloorStuff[cell] = CoreContent.StuffSteel;
                    laid++;
                }

                model.RefreshAll(grid, result.Edifices);
                Debug.Log($"[Paving] laid {laid} covering cells over grass, skipped {skipped}. " +
                          "If the shots shimmer or show grass bleeding through in speckles, " +
                          "EmitFloor needs a lift for coverings — 18-paving.md section 3.");

                if (laid == 0)
                {
                    Debug.LogError("[Paving] nothing was laid, so the shots answer nothing.");
                    exitCode = 1;
                    return;
                }

                lightingRoot = new GameObject("PavingRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                slice.surfaceLayer = start.Y;
                int activeLayer = start.Y;

                cameraObject = new GameObject("PavingCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);

                renderer = new ChunkRenderer(model);
                renderer.Skirt.Enabled = true;
                renderer.Skirt.Build();

                ChunkRenderer active = renderer;
                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    active.ViewerPosition = rendering.transform.position;
                    active.Render(activeLayer, slice);
                };
                RenderPipelineManager.beginCameraRendering += hook;

                Vector3 middle = GroundRelief.Lift(CellMetrics.FloorCentre(start.X, start.Z, start.Y));

                // Three ranges. The far one is the important one: z-fighting is a depth-precision
                // artefact, so a patch that is clean at 20 m can shimmer at 150 m, and 32–160 m is
                // where the slice camera actually sits.
                PlayScene.Shoot(camera, middle, 40f, 45f, 18f, "Logs/paving-near.png");
                PlayScene.Shoot(camera, middle, 45f, 45f, 60f, "Logs/paving-mid.png");
                PlayScene.Shoot(camera, middle, 50f, 45f, 150f, "Logs/paving-far.png");

                // And one nearly edge-on, where two coplanar surfaces are at their worst: a
                // grazing angle gives the depth buffer the least to separate them with.
                PlayScene.Shoot(camera, middle, 6f, 45f, 40f, "Logs/paving-grazing.png");

                RenderPipelineManager.beginCameraRendering -= hook;
                hook = null;

                Debug.Log("[Paving] wrote Logs/paving-{near,mid,far,grazing}.png; " +
                          $"{renderer.DrawCalls} draw calls, {renderer.InstancesDrawn} instances");
            }
            catch (Exception error)
            {
                Debug.LogError($"[Paving] {error}");
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                renderer?.Dispose();
                library?.Dispose();
                GroundRelief.Amplitude = amplitudeWas;
                GroundRelief.Period = periodWas;
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }
    }
}
