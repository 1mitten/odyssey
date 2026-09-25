#nullable enable
using System.Collections.Generic;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
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
    /// Photographs the selection highlight (<c>docs/design/44-selection-highlight.md</c>) on the
    /// played board: a colonist, a tree, an item and a tile lit at once, then each close. The look is
    /// the owner's to judge; this is what lets a session see it before asking them to.
    ///
    /// <para><c>scripts/unity.sh shot Odyssey.EditorTools.SelectionHighlightShot.Shoot</c> writes
    /// <c>Logs/highlight-*.png</c>. The board and the colony are <see cref="PlayScene"/>'s screenshot
    /// board, built the same way.</para>
    /// </summary>
    public static class SelectionHighlightShot
    {
        public static void Shoot()
        {
            int exitCode = 0;
            ChunkRenderer? renderer = null;
            Odyssey.Presentation.World.PawnFigureDirector? figures = null;
            System.Action<ScriptableRenderContext, Camera>? hook = null;
            var cleanup = new List<Object>();
            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                var size = new GridSize(PlayScene.PlaySizeXZ, PlayScene.PlaySizeXZ, PlayScene.PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
                gen.MakeWooded();
                var grid = new CellGrid(size);
                var chunks = new ChunkGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);

                using var library = new ModuleLibrary(catalogue);
                var model = new Odyssey.Presentation.World.WorldRenderModel(size, chunks, library);
                model.RefreshAll(grid, result.Natural!.Context.Edifices);
                renderer = new ChunkRenderer(model);
                int activeLayer = result.StartCell.Y;
                var slice = new SliceSettings { surfaceLayer = activeLayer };

                var root = new GameObject("HighlightShotRoot");
                cleanup.Add(root);
                PlayScene.BuildSheetLighting(root.transform);
                var cameraObject = new GameObject("HighlightCamera");
                cleanup.Add(cameraObject);
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;

                var nav = new NavGraph(grid);
                nav.Rebuild();
                var pawns = new PawnContext(grid, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns())
                    { Chunks = chunks };
                var support = new SupportSystem(grid, new SupportSolver(grid), chunks);
                var mirror = new Odyssey.Presentation.World.GridMirrorContributor(grid, result.Edifices, model);
                var designations = new Odyssey.Sim.Designations.DesignationGrid(grid, result.Edifices);
                SimWorld world = new SimWorldBuilder()
                    .WithSeed(1u)
                    .WithSize(size)
                    .AddSnapshotContributor(mirror)
                    .AddColony(pawns, designations, support, nav, result.Placements, out _)
                    .Build();
                ScenarioDef scenario = ScenarioDef.Playtest();
                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, scenario);
                for (int i = 0; i < 600; i++) world.Tick();

                var actorMaterial = new Material(library.FallbackMaterial) { name = "Odyssey/Actor" };
                cleanup.Add(actorMaterial);
                figures = new Odyssey.Presentation.World.PawnFigureDirector(catalogue, root.transform, 0) { World = model };
                int movePerTick = ContentPack.Pawns().Movement.movePerTick;
                for (int frame = 0; frame < 40; frame++)
                {
                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, 1f / 60f);
                    figures.Evaluate(1f / 60f);
                }

                WorldSnapshot shown = world.Views.Current;
                CellRef start = result.StartCell;

                // The subjects: the colonist nearest the start, the tree nearest it, the first item,
                // and an open tile two cells east.
                int colonist = -1, best = int.MaxValue;
                for (int p = 0; p < shown.Pawns.Length; p++)
                {
                    if (!shown.Pawns[p].IsColonist) continue;
                    int dx = shown.Pawns[p].Cell.X - start.X, dz = shown.Pawns[p].Cell.Z - start.Z;
                    if (dx * dx + dz * dz >= best) continue;
                    best = dx * dx + dz * dz;
                    colonist = shown.Pawns[p].Id.Value;
                }
                int tree = -1;
                for (int r = 1; r < 20 && tree < 0; r++)
                for (int dz = -r; dz <= r && tree < 0; dz++)
                for (int dx = -r; dx <= r && tree < 0; dx++)
                {
                    int x = start.X + dx, z = start.Z + dz;
                    if (!size.Contains(x, z, activeLayer)) continue;
                    int index = size.Index(x, z, activeLayer);
                    if (NaturalContent.IsTree(model.EdificeDef(index))) tree = index;
                }
                ThingId? item = shown.ThingCount > 0 ? shown.Things[0].Id : (ThingId?)null;
                var tile = new CellRef(start.X + 2, start.Z, activeLayer);

                SelectionHighlight.Camera = camera;
                SelectionHighlight.FeatureSeenFrame = int.MinValue;
                SelectionHighlight frameList = SelectionHighlight.Current;
                bool[] include = { true, true, true, true };

                ChunkRenderer active = renderer;
                Odyssey.Presentation.World.PawnFigureDirector walking = figures;
                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    frameList.Clear();
                    active.BeginHighlight(frameList);
                    if (include[1] && colonist >= 0) active.HighlightPawns[colonist] = SelectionHighlight.Primary;
                    if (include[2]) active.HighlightThing = item;

                    active.ViewerPosition = rendering.transform.position;
                    active.Render(activeLayer, slice);
                    active.RenderActors(world.Views.Current, activeLayer, slice, actorMaterial,
                        drawnAsFigures: walking.Drawn);

                    if (include[1] && colonist >= 0) frameList.AddRenderers(walking.FigureObject(colonist), SelectionHighlight.Primary);
                    if (include[0] && tree >= 0) active.CollectCell(tree, frameList, SelectionHighlight.Primary, terrain: false);
                    if (include[3])
                        active.CollectSurface(GroundRelief.Drape(CellMetrics.FloorCentre(tile)), frameList, SelectionHighlight.Primary);
                };
                renderer.PrimeAll(activeLayer, slice);
                RenderPipelineManager.beginCameraRendering += hook;

                Vector3 Centre(CellRef c) => CellMetrics.FloorCentre(c);
                var focus = Centre(start);
                PlayScene.Shoot(camera, focus, 48f, 40f, "Logs/highlight-play.png");

                void Only(int which)
                {
                    for (int i = 0; i < include.Length; i++) include[i] = i == which;
                }

                if (tree >= 0)
                {
                    Only(0);
                    PlayScene.Shoot(camera, Centre(size.FromIndex(tree)), 42f, 14f, "Logs/highlight-tree.png");
                }
                if (colonist >= 0 && shown.TryGetPawn(new PawnId(colonist), out PawnView who))
                {
                    Only(1);
                    PlayScene.Shoot(camera, Centre(who.Cell), 42f, 8f, "Logs/highlight-colonist.png");
                    GameObject? body = figures.FigureObject(colonist);
                    if (body != null)
                    {
                        PlayScene.Shoot(camera, body.transform.position + Vector3.up, 30f, 4.5f, "Logs/highlight-colonist-close.png");
                        // Low and from behind, among the others: whether the selected one is found at a glance.
                        PlayScene.Shoot(camera, body.transform.position + Vector3.up, 18f, 90f, 6f, "Logs/highlight-colonist-behind.png");
                        // The control: the same colonist from the same camera with nothing selected.
                        Only(-1);
                        PlayScene.Shoot(camera, body.transform.position + Vector3.up, 30f, 4.5f, "Logs/highlight-colonist-control.png");
                    }
                }
                if (item.HasValue)
                {
                    Only(2);
                    PlayScene.Shoot(camera, Centre(shown.Things[0].Cell), 48f, 8f, "Logs/highlight-item.png");
                }
                Only(3);
                PlayScene.Shoot(camera, Centre(tile), 48f, 12f, "Logs/highlight-tile.png");

                Debug.Log($"[HighlightShot] colonist {colonist}, tree {tree}, item {item?.Value ?? -1}, " +
                          $"figures {figures.FigureCount}, parts last frame {frameList.Count}, " +
                          $"feature {(SelectionHighlight.FeatureSeenFrame != int.MinValue ? "seen" : "NOT SEEN")}");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[HighlightShot] failed: " + e);
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                SelectionHighlight.Current.Clear();
                SelectionHighlight.Camera = null;
                figures?.Dispose();
                renderer?.Dispose();
                foreach (Object o in cleanup) if (o != null) Object.DestroyImmediate(o);
                if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            }
        }
    }
}
