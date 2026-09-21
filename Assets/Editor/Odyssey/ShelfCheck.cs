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
using Odyssey.Sim.Storage;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Photograph a row of shelves filling up, from the four angles that can disagree.
    ///
    /// <para><b>What it is for.</b> The owner reported on 2026-09-21 that a shelf read as a
    /// storage unit rather than as shelving — <i>"maybe there should be 2 shelves and not 1 thick
    /// container"</i> — and sent a photograph of a timber garage rack. The answer was geometry:
    /// posts instead of a solid carcass, two decks instead of one, the upper deck half the depth
    /// of the lower. Every part of that is a look judgement, and the only person who can press
    /// Play is the person who raised the report. A sheet is the cheaper way round.</para>
    ///
    /// <para><b>The fill is the point, not the empty frame.</b> Four shelves holding nothing, two
    /// stacks, four and eight: two is half the lower deck, four fills it, and eight is both decks
    /// loaded. That progression is what says whether the goods read as the fill tell
    /// (<c>docs/design/30-shelves.md</c> §8b) or as a heap in a box, and it is the thing a
    /// geometry test cannot answer.</para>
    ///
    /// <para><b>The overhead shot is the one that settles the design decision.</b> The upper deck
    /// is set back and shallower precisely so that looking down at the play camera's 48° does not
    /// hide the lower deck's four stacks under it. <c>ShelfShapeTests</c> asserts the arithmetic;
    /// this is the picture of it, and if the front row is dark in <c>shelf-over.png</c> then the
    /// assertion is measuring the wrong clearance.</para>
    ///
    /// <para><b>Raised through <see cref="ConstructionGrid"/> rather than stamped into the
    /// grid</b>, for the reason <see cref="WallCheck"/> gives: <c>Raise</c> is what a colonist's
    /// last hammer stroke calls, and a harness that sets the cells itself photographs a shelf the
    /// game cannot build. The goods go in through <see cref="StorageUnits.PutIn"/>, which is the
    /// door a hauler uses, for the same reason.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.ShelfCheck.Run</c>. It needs a
    /// real graphics device, so not under <c>-nographics</c>.</para>
    /// </summary>
    public static class ShelfCheck
    {
        /// <summary>How many stacks each shelf in the row holds, left to right.</summary>
        static readonly int[] Fills = { 0, 2, 4, 8 };

        /// <summary>Cells between one shelf and the next, so neither crowds the other in shot.</summary>
        const int Apart = 2;

        [MenuItem("Odyssey/Presentation/Check a shelf")]
        public static void RunFromMenu() => Shoot(exitWhenDone: false);

        public static void Run() => Shoot(Application.isBatchMode);

        static void Shoot(bool exitWhenDone)
        {
            Execute(exitWhenDone);
            if (!exitWhenDone) ShotFolder.Reveal("shelf-*.png");
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

                // Bare, for WallCheck's reason: nobody walks into shot, and no job giver decides
                // to tidy the goods away while the sheet is being taken.
                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, ScenarioDef.Bare());

                CellRef start = result.StartCell;
                var raised = new int[Fills.Length];
                int standing = Raise(sites, pawns, size, start, raised);
                if (standing != Fills.Length)
                {
                    Debug.LogError($"[Shelf] only {standing} of {Fills.Length} shelves found room " +
                                   $"near {start}; nothing worth photographing.");
                    exitCode = 1;
                    return;
                }

                model.RefreshAll(grid, result.Edifices);

                int stacked = Fill(pawns, size, start, raised);
                world.Tick();

                Debug.Log($"[Shelf] raised {standing} shelves from {start} and put {stacked} stacks " +
                          $"on them ({string.Join(", ", Fills)}); deck tops at " +
                          $"{ShelfShape.LowerDeckTop:0.00} m and {ShelfShape.DeckTop:0.00} m, " +
                          $"top {ShelfShape.Top:0.00} m, front row clears the upper deck by " +
                          $"{ShelfShape.LowerSlotsClearTheUpperDeck:0.00} m.");

                lightingRoot = new GameObject("ShelfRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                int activeLayer = start.Y;
                slice.surfaceLayer = start.Y;

                cameraObject = new GameObject("ShelfCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);

                renderer = new ChunkRenderer(model);
                renderer.Skirt.Enabled = true;
                renderer.Skirt.Build();

                // **Prime, or photograph the sky.** A frame meshes eleven chunks and no more
                // (ChunkRenderer.MeshBudgetPerFrame), so a sheet of four shots meshes forty-four
                // of the board's several hundred and the camera looks out over almost nothing.
                // The composition root primes before its first drawn frame and a check script has
                // no composition root, so it owes the same call. Every *Check script in this
                // folder has been taking pictures of an empty sky since the budget landed.
                renderer.PrimeAll(start.Y, slice);

                var actorMaterial = new Material(library.FallbackMaterial) { name = "Odyssey/Actor" };
                actorMaterial.SetColor("_BaseColor", new Color(0.98f, 0.36f, 0.20f));

                ChunkRenderer active = renderer;
                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    active.ViewerPosition = rendering.transform.position;
                    active.Render(activeLayer, slice);
                    active.RenderActors(world.Views.Current, activeLayer, slice, actorMaterial);
                };
                RenderPipelineManager.beginCameraRendering += hook;

                Vector3 middle = Middle(start, raised, size);

                // The full one, for the close shots: an empty rack says whether the frame reads,
                // and only a loaded one says whether the goods do.
                Vector3 loaded = At(raised[raised.Length - 1], size);

                // A shelf's open front is its local +Z, so the camera has to stand on that side of
                // it or the sheet is four pictures of the back rail. Yaw 180 faces it squarely and
                // 210 is the three-quarter the game is actually looked at from.
                PlayScene.Shoot(camera, middle, 48f, 210f, 26f, "Logs/shelf-row-play.png");
                PlayScene.Shoot(camera, loaded, 48f, 210f, 7f, "Logs/shelf-row-close.png");

                // Low and square on, where the two decks have daylight between them against the
                // sky. This is the shot that says "rack" rather than "crate", and the one the
                // single-deck version could not have passed.
                PlayScene.Shoot(camera, middle, 14f, 180f, 12f, "Logs/shelf-front.png");

                // Nearly overhead, which is what decides whether the lower deck's four stacks
                // survive being looked down on. If the front row is in shadow under the upper
                // deck here, the clearance is wrong however green ShelfShapeTests is.
                PlayScene.Shoot(camera, middle, 75f, 210f, 13f, "Logs/shelf-over.png");

                RenderPipelineManager.beginCameraRendering -= hook;
                hook = null;

                Debug.Log("[Shelf] wrote Logs/shelf-{row-play,row-close,front,over}.png; " +
                          $"{renderer.DrawCalls} draw calls, {renderer.InstancesDrawn} instances");
            }
            catch (Exception error)
            {
                Debug.LogError($"[Shelf] {error}");
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
        /// Order and finish a row of wooden shelves, all facing the same way, and record the cell
        /// each one stands in. Returns how many went up.
        ///
        /// <para>The meadow has trees and streams in it, so a cell that will not take the order is
        /// stepped over rather than being fatal — but the row has to come out whole, because the
        /// sheet's whole argument is one shelf against the next.</para>
        /// </summary>
        static int Raise(ConstructionGrid sites, PawnContext pawns, GridSize size,
            CellRef start, int[] raised)
        {
            int standing = 0;
            for (int step = 0; step < 40 && standing < Fills.Length; step++)
            {
                int x = start.X + step * Apart;
                if (!size.Contains(x, start.Z, start.Y)) break;

                var cell = new CellRef(x, start.Z, start.Y);
                if (sites.Place(cell, BuildingHandle.Shelf, StuffHandle.Wood) != IntentRejection.None)
                    continue;

                int index = size.Index(x, start.Z, start.Y);
                sites.Raise(pawns, index);
                raised[standing++] = index;
            }

            return standing;
        }

        /// <summary>
        /// Put full stacks of wood on each shelf, as many as <see cref="Fills"/> asks for.
        ///
        /// <para>Spawned on a scratch cell and then moved in through <see cref="StorageUnits.PutIn"/>,
        /// which is the path a hauler takes. The scratch cell frees itself each time, because a
        /// contained thing has no cell at all.</para>
        /// </summary>
        static int Fill(PawnContext pawns, GridSize size, CellRef start, int[] raised)
        {
            StorageUnits units = pawns.StorageUnits!;
            ColonyItems items = pawns.Items;
            int wood = ItemIndex.Wood;
            int full = pawns.Content.Items[wood].stackLimit;

            int scratch = Scratch(pawns, size, start, raised);
            if (scratch < 0) return 0;

            int stacked = 0;
            for (int shelf = 0; shelf < raised.Length; shelf++)
            {
                StorageUnit? unit = units.AtCell(raised[shelf]);
                if (unit == null) continue;

                for (int stack = 0; stack < Fills[shelf]; stack++)
                {
                    if (!units.HasSpaceFor(unit, wood, full)) break;

                    ThingId id = items.Spawn(wood, scratch, full);
                    ColonyItem? item = items.Get(id);
                    if (item == null) break;

                    units.PutIn(unit, item);
                    stacked++;
                }
            }

            return stacked;
        }

        /// <summary>One walkable cell near the start that no shelf stands in, to spawn on.</summary>
        static int Scratch(PawnContext pawns, GridSize size, CellRef start, int[] raised)
        {
            for (int back = 1; back < 12; back++)
            {
                int z = start.Z + back;
                if (!size.Contains(start.X, z, start.Y)) break;

                int index = size.Index(start.X, z, start.Y);
                if (System.Array.IndexOf(raised, index) >= 0) continue;
                if (!pawns.Cells.IsWalkable(index)) continue;
                if (!pawns.Items.CellHasSpace(index)) continue;
                return index;
            }

            return -1;
        }

        /// <summary>One shelf's own middle in world metres, at the height of its upper deck.</summary>
        static Vector3 At(int index, GridSize size)
        {
            CellRef cell = size.FromIndex(index);
            Vector3 floor = CellMetrics.FloorCentre(cell.X, cell.Z, cell.Y);
            return GroundRelief.Lift(floor) + Vector3.up * (ShelfShape.Top * 0.5f);
        }

        /// <summary>The middle of the row in world metres, lifted onto the drawn ground.</summary>
        static Vector3 Middle(CellRef start, int[] raised, GridSize size)
        {
            CellRef first = size.FromIndex(raised[0]);
            CellRef last = size.FromIndex(raised[raised.Length - 1]);
            Vector3 floor = CellMetrics.FloorCentre((first.X + last.X) / 2, first.Z, first.Y);
            return GroundRelief.Lift(floor) + Vector3.up * (ShelfShape.Top * 0.5f);
        }
    }
}
