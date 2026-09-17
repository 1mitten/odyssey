#nullable enable
using System;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Photograph a finished room in wood and the same room in stone.
    ///
    /// <para><b>What it is for.</b> Three questions about a built wall arrived together on
    /// 2026-09-17 and none of them can be settled by a test. Does a draped wall follow the ground
    /// or sag along it? Does the cap read as a top, or as a lid on a box? And is a wooden wall
    /// brown enough to be wood without going to mud? All three are look judgements, and the owner
    /// was being asked to press Play and form them from memory. A pair of pictures of the same
    /// room in the two materials a colony can actually build with is the cheaper way.</para>
    ///
    /// <para><b>The room is raised through <see cref="ConstructionGrid"/> rather than stamped into
    /// the grid.</b> <c>Raise</c> is what a colonist's last hammer stroke calls, and it does five
    /// things — the edifice record, the blocking flag, the chunk marks, the nav dirt and the
    /// clear-down — of which the mesher reads two. A harness that set the cells itself would be
    /// photographing a wall the game cannot build, which is the fault <see cref="QuarryCheck"/>
    /// records for mining and the same one in a different coat.</para>
    ///
    /// <para><b>Both rooms stand on the same ground, side by side.</b> The relief is a smooth
    /// field, so a room here and a room fifty metres away sit on different slopes; putting them
    /// four cells apart means the only difference in the pair is the material. The wooded board
    /// is the one the game loads, at the board amplitude, because a wall on a flat board cannot
    /// answer the first question at all.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.WallCheck.Run</c>. It needs a
    /// real graphics device, so not under <c>-nographics</c>.</para>
    /// </summary>
    public static class WallCheck
    {
        /// <summary>The room's footprint, in cells. Big enough to have a middle to look into.</summary>
        const int Wide = 6;
        const int Deep = 4;

        /// <summary>The gap between the two rooms, in cells.</summary>
        const int Apart = 3;

        [MenuItem("Odyssey/Presentation/Check a built wall")]
        public static void RunFromMenu() => Shoot(exitWhenDone: false);

        public static void Run() => Shoot(Application.isBatchMode);

        static void Shoot(bool exitWhenDone)
        {
            Execute(exitWhenDone);
            if (!exitWhenDone) ShotFolder.Reveal("wall-*.png");
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
                model.RefreshAll(grid, result.Natural!.Context.Edifices);

                var nav = new NavGraph(grid);
                nav.Rebuild();
                var pawns = new PawnContext(
                    grid, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns())
                    { Chunks = chunks };
                var support = new SupportSystem(grid, new SupportSolver(grid), chunks);
                var mirror = new GridMirrorContributor(grid, result.Edifices, model);
                var designations = new Odyssey.Sim.Designations.DesignationGrid(grid, result.Edifices);

                SimWorld world = new SimWorldBuilder()
                    .WithSeed(1u)
                    .WithSize(size)
                    .AddSnapshotContributor(mirror)
                    .AddColony(pawns, designations, support, nav, result.Placements, out ConstructionGrid sites)
                    .Build();

                // Bare: nobody is placed, so nothing walks into shot and no job giver decides to
                // build something of its own while the sheet is being taken. What is photographed
                // is the wall, not the colony that would have raised it.
                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, ScenarioDef.Bare());

                CellRef start = result.StartCell;
                int builtWood = Build(sites, pawns, size, start, 0, StuffHandle.Wood);
                int builtStone = Build(sites, pawns, size, start, Wide + Apart, StuffHandle.Stone);
                model.RefreshAll(grid, result.Edifices);

                Debug.Log($"[Wall] raised {builtWood} wood cells and {builtStone} stone cells " +
                          $"from {start}; relief amplitude {GroundRelief.Amplitude} m over " +
                          $"{GroundRelief.Period} m.");
                if (builtWood == 0 || builtStone == 0)
                {
                    Debug.LogError("[Wall] one of the rooms did not go up; nothing to compare.");
                    exitCode = 1;
                    return;
                }

                lightingRoot = new GameObject("WallRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                int activeLayer = start.Y;
                slice.surfaceLayer = start.Y;

                cameraObject = new GameObject("WallCamera");
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

                Vector3 woodMiddle = Middle(start, 0);
                Vector3 stoneMiddle = Middle(start, Wide + Apart);
                Vector3 both = (woodMiddle + stoneMiddle) * 0.5f;

                // The view the game is judged in: the two rooms side by side, which is the only
                // shot that answers "is the wood brown enough" — a colour is judged against
                // something, and stone is the something.
                PlayScene.Shoot(camera, both, 48f, 45f, 46f, "Logs/wall-pair-play.png");

                // Each material close enough that one wall fills the frame and the tint can be
                // read off the face rather than inferred from a thumbnail.
                PlayScene.Shoot(camera, woodMiddle, 40f, 45f, 20f, "Logs/wall-wood-play.png");
                PlayScene.Shoot(camera, stoneMiddle, 40f, 45f, 20f, "Logs/wall-stone-play.png");

                // Low and along the run, where the head of the wall has a profile against the sky
                // and a step at a cell join would be unmissable. This is the shot the drape fix
                // has to survive.
                PlayScene.Shoot(camera, woodMiddle, 12f, 90f, 26f, "Logs/wall-wood-low.png");

                // Nearly overhead, which is the one that cannot be argued with about the cap: if
                // the cell were still hollow there would be a black slot down every wall.
                PlayScene.Shoot(camera, woodMiddle, 78f, 45f, 24f, "Logs/wall-wood-over.png");

                RenderPipelineManager.beginCameraRendering -= hook;
                hook = null;

                Debug.Log($"[Wall] wrote Logs/wall-{{pair-play,wood-play,stone-play,wood-low,wood-over}}.png; " +
                          $"{renderer.DrawCalls} draw calls, {renderer.InstancesDrawn} instances");
            }
            catch (Exception error)
            {
                Debug.LogError($"[Wall] {error}");
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

        /// <summary>
        /// Order and finish a rectangular room, and report how many cells went up.
        ///
        /// <para>Through <see cref="ConstructionGrid.Place"/> and <see cref="ConstructionGrid.Raise"/>,
        /// which are the two ends of the pipeline a colonist drives: the order is the one the
        /// Build palette sends, and the raise is what the last hammer stroke calls. Nothing here
        /// writes a cell itself, so a room in this sheet is the room the game builds.</para>
        ///
        /// <para>A cell that will not take an order is skipped rather than fatal — the meadow has
        /// trees and streams in it, and a room with a gap where a trunk stood is still a room. The
        /// count is what says whether enough of it stood up to be worth photographing.</para>
        /// </summary>
        static int Build(ConstructionGrid sites, PawnContext pawns, GridSize size,
            CellRef start, int offsetX, int stuff)
        {
            int raised = 0;
            for (int i = 0; i < Wide; i++)
            for (int j = 0; j < Deep; j++)
            {
                bool edge = i == 0 || j == 0 || i == Wide - 1 || j == Deep - 1;
                if (!edge) continue;

                int x = start.X + offsetX + i;
                int z = start.Z + j;
                if (!size.Contains(x, z, start.Y)) continue;

                var cell = new CellRef(x, z, start.Y);
                if (sites.Place(cell, BuildingHandle.Wall, stuff) != IntentRejection.None) continue;

                sites.Raise(pawns, size.Index(x, z, start.Y));
                raised++;
            }

            return raised;
        }

        /// <summary>The middle of a room's floor in world metres, lifted onto the drawn ground.</summary>
        static Vector3 Middle(CellRef start, int offsetX)
        {
            Vector3 floor = CellMetrics.FloorCentre(
                start.X + offsetX + Wide / 2, start.Z + Deep / 2, start.Y);
            return GroundRelief.Lift(floor);
        }
    }
}
