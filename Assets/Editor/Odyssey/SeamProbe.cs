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
    /// <b>The white lines on a built deck: which of three things is it?</b>
    ///
    /// <para>The owner photographed thin pale dashes across a slab deck (2026-09-17) and the same
    /// artefact is in this project's own contact sheets — <c>floor-wood-over.png</c> has them in the
    /// middle of the planking, away from any wall. Three explanations were available and they want
    /// opposite fixes, so this is a bisect rather than a picture.</para>
    ///
    /// <list type="number">
    /// <item><b>Depth precision.</b> Two coplanar surfaces fighting, which gets worse with distance.
    /// Shot at three ranges from the same angle; if the lines grow with range, it is this.</item>
    /// <item><b>The drape's shear.</b> Neighbouring cells are sheared onto the ground's tangent at
    /// their own centres, so their shared edge can open. Shot again with the relief switched off
    /// entirely; if the lines vanish, it is this — and <em>only</em> this test can say so, because
    /// the arithmetic says adjacent cells agree to second order and the mismatch is about 2 mm,
    /// which is under a tenth of a pixel at the camera the owner was using. The picture is the
    /// check on that arithmetic.</item>
    /// <item><b>The slab art itself.</b> If the lines survive both, they are in the module's own
    /// geometry or texture and no amount of placement maths will move them.</item>
    /// </list>
    ///
    /// <para>The deck floats: slabs written at a layer with nothing underneath, so no wall, no
    /// ground and no cap can be blamed for what is seen. That is deliberate — the owner's deck sat
    /// on walls and the first guess was the wall top showing through, but the dashes in the sheet
    /// are over the <em>middle</em> of a room where there is no wall at all.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.SeamProbe.Run</c>.</para>
    /// </summary>
    public static class SeamProbe
    {
        const int Patch = 12;

        [MenuItem("Odyssey/Presentation/Probe the seams on a deck")]
        public static void RunFromMenu() => Shoot(exitWhenDone: false);

        public static void Run() => Shoot(Application.isBatchMode);

        static void Shoot(bool exitWhenDone)
        {
            Execute(exitWhenDone);
            if (!exitWhenDone) ShotFolder.Reveal("seam-*.png");
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

                library = new ModuleLibrary(catalogue);
                var model = new WorldRenderModel(size, chunks, library);

                // A deck two layers up with nothing under it: no wall, no ground, no cap to blame.
                CellRef start = result.StartCell;
                int deckLayer = Mathf.Min(start.Y + 2, size.SizeY - 1);
                int laid = 0;
                var walls = new System.Collections.Generic.List<int>();
                System.Collections.Generic.List<PlacedEdifice> edifices = result.Natural!.Context.Edifices;
                for (int j = 0; j < Patch; j++)
                for (int i = 0; i < Patch; i++)
                {
                    int x = start.X + i - Patch / 2, z = start.Z + j - Patch / 2;
                    if (!size.Contains(x, z, deckLayer)) continue;

                    int cell = size.Index(x, z, deckLayer);
                    grid.Floor[cell] = CoreContent.SlabBuilt;
                    grid.FloorStuff[cell] = NaturalContent.StuffWood;
                    laid++;

                    // Half the deck gets a wall directly beneath it. A wall's top face is the
                    // plane its slab is placed on, so the two are coplanar - and a floating deck
                    // came back clean at every range, which leaves this as the only difference
                    // between this probe and the contact sheet that shows the lines.
                    if (i < Patch / 2) walls.Add(size.Index(x, z, deckLayer - 1));
                }

                // The walls, stamped straight into the grid: this probe is about pixels, and what
                // put them there does not change one of them.
                for (int w = 0; w < walls.Count; w++)
                {
                    edifices.Add(new PlacedEdifice
                    {
                        CellIndex = walls[w], Def = CoreContent.EdificeWall,
                        Stuff = CoreContent.StuffConcrete, Built = true,
                    });
                    grid.Edifice[walls[w]] = edifices.Count - 1;
                    grid.Flags[walls[w]] |= CellFlags.BlockingEdifice;
                }

                if (laid == 0)
                {
                    Debug.LogError("[Seam] no deck was laid.");
                    exitCode = 1;
                    return;
                }

                lightingRoot = new GameObject("SeamRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                slice.surfaceLayer = start.Y;
                int activeLayer = deckLayer;

                cameraObject = new GameObject("SeamCamera");
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

                Vector3 middle = CellMetrics.FloorCentre(start.X, start.Z, deckLayer);

                // 1-3. The relief as the board plays it, at three ranges from one angle. Distance
                //      is the whole question here, so nothing else may change between them.
                GroundRelief.Amplitude = GroundRelief.BoardAmplitude;
                GroundRelief.Period = 150f;
                model.RefreshAll(grid, result.Edifices);
                PlayScene.Shoot(camera, GroundRelief.Lift(middle), 80f, 45f, 14f, "Logs/seam-walls-14m.png");
                PlayScene.Shoot(camera, GroundRelief.Lift(middle), 80f, 45f, 30f, "Logs/seam-walls-30m.png");
                PlayScene.Shoot(camera, GroundRelief.Lift(middle), 80f, 45f, 60f, "Logs/seam-walls-60m.png");

                // 4. The same deck with the relief switched off, so every cell is placed on one
                //    flat plane and no two of them are sheared differently. This is the test that
                //    the arithmetic cannot do for itself.
                GroundRelief.Amplitude = 0f;
                model.RefreshAll(grid, result.Edifices);
                PlayScene.Shoot(camera, middle, 80f, 45f, 30f, "Logs/seam-walls-flat-30m.png");

                RenderPipelineManager.beginCameraRendering -= hook;
                hook = null;

                Debug.Log($"[Seam] {laid} deck cells at L{deckLayer}; {walls.Count} of them over a wall, the rest floating. Wrote " +
                          "Logs/seam-{relief-14m,relief-30m,relief-60m,flat-30m}.png. " +
                          "Lines growing with range = depth precision; lines gone on the flat " +
                          "shot = the drape's shear; lines in all four = the module's own art.");
            }
            catch (Exception error)
            {
                Debug.LogError($"[Seam] {error}");
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
