#nullable enable
using System;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Growing;
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
    /// Photograph a growing field through its stages, and the pile a harvest leaves.
    ///
    /// <para><b>The fault this sheet exists to catch.</b> The plot's art draws one instance per
    /// carrot the harvest will yield, and a test pins the mesher's arithmetic — but every
    /// judgement about the carrots so far has come from the owner playing the game, and twice
    /// what he was looking at was not what the code draws (a stale catalogue, then a harvest
    /// pile that <c>ItemHeap</c> had no recipe for and drew as one prop). A sheet photographs
    /// the shipped catalogue through the shipped mesher with nothing in between.</para>
    ///
    /// <para><b>State is driven directly, not earned.</b> The zone is designated through the
    /// same intent the paint tool sends, but the sowing, the half-grown stage and the ripening
    /// are written straight into <see cref="GrowingZones"/> — a photograph does not need the
    /// four days and it must not depend on a colonist's timetable. The one rule that buys is
    /// honoured here and not hidden: <see cref="GrowingZones.Advance"/> leaves the re-mesh mark
    /// to the growth system that calls it, so this harness marks the chunk itself, or the
    /// field would photograph its previous stage forever.</para>
    ///
    /// <para>What to look for, in order: a ripe tile shows five carrots and not one; the five
    /// are inset as if standing in the soil; the pile beside the field shows five lying
    /// carrots and not one; and the stages read as sprout / leafy / ripe from the play
    /// bearing. Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.CropCheck.Run</c>. It
    /// needs a real graphics device, so not under <c>-nographics</c>.</para>
    /// </summary>
    public static class CropCheck
    {
        const float FrameSeconds = 1f / 60f;

        /// <summary>The field, in cells: three wide and two deep, beside the start cell.</summary>
        const int FieldWidth = 3, FieldDepth = 2;

        [MenuItem("Odyssey/Presentation/Check a growing field")]
        public static void RunFromMenu() => Shoot(exitWhenDone: false);

        public static void Run() => Shoot(Application.isBatchMode);

        static void Shoot(bool exitWhenDone)
        {
            Execute(exitWhenDone);
            if (!exitWhenDone) ShotFolder.Reveal("crop-*.png");
        }

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            ChunkRenderer? renderer = null;
            ModuleLibrary? library = null;
            PawnFigureDirector? figures = null;
            GameObject? lightingRoot = null;
            GameObject? cameraObject = null;
            Action<ScriptableRenderContext, Camera>? hook = null;

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

                // The played board's relief, not the flat default: the cover's seams are a
                // question about neighbouring drapes disagreeing, and a board with no amplitude
                // cannot ask it. The first version of this sheet left the field flat and its
                // "no seams" verdict was blind - the owner's screenshots showed the grid the
                // flat photo could not (2026-09-20).
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
                    .AddColony(pawns, designations, support, nav, result.Placements, out _)
                    .Build();

                // One colonist for scale and nothing else on the board: the scenario's piles and
                // stockpile would sit in the frame and read as part of the subject.
                var scenario = ScenarioDef.Bare();
                scenario.colonists = 1;
                scenario.mealPiles = 0; scenario.mealsPerPile = 0;
                scenario.stonePiles = 0; scenario.woodPiles = 0;
                scenario.salvage = 0; scenario.beds = 0; scenario.stockpileCells = 0;
                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, scenario);

                GrowingZones zones = pawns.Growing!;
                int y = result.StartCell.Y;

                // A tree on a field cell refuses its designation, and the woodland does not
                // clear itself for the camera: the same sweep BankCheck makes, generous enough
                // that the field and the pile cell are both open.
                for (int z = result.StartCell.Z - 1; z <= result.StartCell.Z + FieldDepth + 2; z++)
                for (int x = result.StartCell.X - 3; x <= result.StartCell.X + FieldDepth + 3; x++)
                {
                    if (!size.Contains(x, z, y)) continue;
                    int index = size.Index(x, z, y);
                    if (grid.Edifice[index] < 0) continue;
                    grid.RemoveEdifice(index);
                }
                model.RefreshAll(grid, result.Edifices);

                // Paint the field through the same gate the paint tool's intent enters.
                int fx = result.StartCell.X - 1, fz = result.StartCell.Z + 2;
                var field = new int[FieldWidth * FieldDepth];
                int planted = 0;
                for (int dz = 0; dz < FieldDepth; dz++)
                for (int dx = 0; dx < FieldWidth; dx++)
                {
                    var cell = new CellRef(fx + dx, fz + dz, y);
                    if (zones.Designate(cell, PlantHandle.Carrot) != IntentRejection.None)
                    {
                        Debug.LogWarning($"[Crop] refused a field cell at {cell}");
                        continue;
                    }
                    field[planted++] = size.Index(fx + dx, fz + dz, y);
                }
                if (planted == 0)
                {
                    Debug.LogError("[Crop] every field cell was refused; nothing to photograph.");
                    exitCode = 1;
                    return;
                }

                lightingRoot = new GameObject("CropRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);
                figures = new PawnFigureDirector(catalogue, lightingRoot.transform, 0) { World = model };

                cameraObject = new GameObject("CropCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);

                renderer = new ChunkRenderer(model);
                renderer.Skirt.Enabled = true;
                renderer.Skirt.Build();

                var actorMaterial = new Material(library.FallbackMaterial) { name = "Odyssey/Actor" };
                actorMaterial.SetColor("_BaseColor", new Color(0.98f, 0.36f, 0.20f));

                ChunkRenderer active = renderer;
                int[] shotLayer = { y };
                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    active.ViewerPosition = rendering.transform.position;
                    active.Render(shotLayer[0], slice);
                    active.RenderActors(world.Views.Current, shotLayer[0], slice, actorMaterial,
                        drawnAsFigures: figures!.Drawn);
                };
                RenderPipelineManager.beginCameraRendering += hook;

                int movePerTick = ContentPack.Pawns().Movement.movePerTick;

                // Stage one: sown this morning, a sprout on dark soil.
                for (int i = 0; i < planted; i++) zones.Sow(field[i]);
                SyncAndShoot(world, model, figures, active, camera, slice, y, movePerTick,
                    "crop-1-sown", close: false);

                // Stage two: the growth system's own arithmetic, written in one step. Advance
                // leaves the re-mesh to its caller, so the mark is made here — the growth system
                // makes it when the bucket changes, and a harness standing in for it owes the
                // same mark or the picture lies about the stage.
                int half = planted / 2;
                for (int i = 0; i < half; i++)
                {
                    int after = zones.Advance(field[i], 70_000);
                    if (after != 1) chunks.MarkDirty(size.FromIndex(field[i]));
                }
                SyncAndShoot(world, model, figures, active, camera, slice, y, movePerTick,
                    "crop-2-growing", close: false);

                // Ripe: every standing crop, the same edit the debug row makes.
                zones.RipenAll();
                SyncAndShoot(world, model, figures, active, camera, slice, y, movePerTick,
                    "crop-3-ripe", close: false);
                SyncAndShoot(world, model, figures, active, camera, slice, y, movePerTick,
                    "crop-3-close", close: true);

                // The pile: five carrots where a harvest would drop them, photographed on its
                // own - the field behind a pile of upright carrots is a mirror that answers
                // any question about orientation with more of the same shape.
                int pileCell = size.Index(fx + FieldWidth, fz, y);
                pawns.Items.Spawn(ItemIndex.Carrots, pileCell, 5);
                CellRef pileAt = size.FromIndex(pileCell);
                world.Tick();
                model.UpdateCrops(world.Views.Current.Plants);
                model.UpdateZones(world.Views.Current.Zones);
                figures.Sync(world.Views.Current, y, slice, 0f, movePerTick, FrameSeconds);
                figures.Evaluate(FrameSeconds);
                Vector3 pileFocus = GroundRelief.Lift(CellMetrics.FloorCentre(pileAt)) + Vector3.up * 0.2f;
                PlayScene.Shoot(camera, pileFocus, 50f, 45f, 4f, "Logs/crop-4-pile.png");

                // The arithmetic beside the picture: each drawn carrot's own up axis, which
                // must come back near horizontal now that the pile lies down.
                if (ItemHeap.TryRecipe(ItemIndex.Carrots, out ItemHeap.Recipe heap))
                {
                    var placements = new Matrix4x4[ItemHeap.Most];
                    int drawn = ItemHeap.Place(5, 1u, Vector3.zero, heap, placements);
                    var tilts = new System.Text.StringBuilder();
                    for (int i = 0; i < drawn; i++)
                        tilts.Append((placements[i].rotation * Vector3.up).y.ToString("0.00")).Append(' ');
                    Debug.Log($"[Crop] pile carrots' up-axis y: {tilts}(0 is lying, 1 is standing)");
                }

                // The border between tilled and grass, flat on: a clump overhanging the plot
                // reads from above as a green smear on dark soil, which is what the owner
                // called jarring - and what the pull-in has to have removed.
                var edgeCell = new CellRef(fx + FieldWidth - 1, fz, y);
                Vector3 edgeFocus = GroundRelief.Lift(CellMetrics.FloorCentre(edgeCell)) + Vector3.up * 0.25f;
                PlayScene.Shoot(camera, edgeFocus, 28f, 90f, 5f, "Logs/crop-5-edge.png");
                Debug.Log($"[Crop] ripe cell {field[0]} draws {model.CropCount(field[0])} carrot(s); " +
                          $"a five-stack pile draws " +
                          $"{(ItemHeap.TryRecipe(ItemIndex.Carrots, out var recipe) ? ItemHeap.RockCount(5, recipe).ToString() : "one prop (no recipe!)")}");
                Debug.Log("[Crop] wrote Logs/crop-{1-sown,2-growing,3-ripe,3-close,4-pile,5-edge}.png");
            }
            catch (Exception error)
            {
                Debug.LogError($"[Crop] {error}");
                exitCode = 1;
            }
            finally
            {
                GroundRelief.Amplitude = 0f;
                GroundRelief.Period = 0f;
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                figures?.Dispose();
                renderer?.Dispose();
                library?.Dispose();
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// One published snapshot, both mirrors fed in the order the frame loop feeds them, a
        /// beat for the figure director, then one photograph of the field — from the play
        /// bearing at a distance, or close enough that the carrots can be counted by eye.
        /// </summary>
        static void SyncAndShoot(SimWorld world, WorldRenderModel model, PawnFigureDirector figures,
            ChunkRenderer renderer, Camera camera, SliceSettings slice, int layer,
            int movePerTick, string name, bool close)
        {
            world.Tick();
            slice.surfaceLayer = layer;
            WorldSnapshot snapshot = world.Views.Current;
            model.UpdateCrops(snapshot.Plants);
            model.UpdateZones(snapshot.Zones);

            // The zone cover the composition root draws, drawn here for the same reason it is
            // drawn there: the cover is the ground's own mesh re-tinted, and only a photograph
            // can say whether it sits flush - which is the question this sheet exists to answer
            // for a board with real relief in it.
            for (int i = 0; i < snapshot.Zones.Length; i++)
            {
                CellRef zoned = snapshot.Size.FromIndex(snapshot.Zones[i].CellIndex);
                renderer.DrawZoneCover(zoned, Odyssey.Presentation.Bootstrap.OdysseyBootstrap.ZoneTintColour);
            }
            for (int frame = 0; frame < 2; frame++)
            {
                figures.Sync(snapshot, layer, slice, 0f, movePerTick, FrameSeconds);
                figures.Evaluate(FrameSeconds);
            }

            // The field itself, lifted off the relief the way the renderer lifts everything that
            // stands on the ground, so the camera aims where the carrots actually are.
            Vector3 fieldAt = GroundRelief.Lift(FieldCentre(snapshot, layer)) + Vector3.up * 0.35f;
            if (close) PlayScene.Shoot(camera, fieldAt, 30f, 20f, 5.5f, $"Logs/{name}.png");
            else PlayScene.Shoot(camera, fieldAt, 48f, 45f, 14f, $"Logs/{name}.png");
        }

        static Vector3 FieldCentre(WorldSnapshot snapshot, int layer)
        {
            // The first planted cell is the field's own corner; its centre plus half a tile is
            // the middle of the plot.
            System.ReadOnlySpan<PlantView> plants = snapshot.Plants;
            if (plants.Length == 0) return CellMetrics.FloorCentre(0, 0, layer);
            CellRef cell = snapshot.Size.FromIndex(plants[0].CellIndex);
            return CellMetrics.FloorCentre(cell.X + 1, cell.Z, layer);
        }
    }
}
