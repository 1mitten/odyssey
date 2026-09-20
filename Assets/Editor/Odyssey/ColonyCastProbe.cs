#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Odyssey.Hud;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Reproduce the owner's report — "when generating more colonists, some are not animated and
    /// their profile picture is black" — and measure it per pawn rather than per face.
    ///
    /// <para><see cref="CastProbe"/> already showed that every one of the sixty-one faces has art,
    /// three live gaits and a humanoid rig, and that every one of them photographs. So the fault
    /// is not a property of a face; it is a property of a <em>pawn</em>, or of how many there are.
    /// This builds a real colony, adds more colonists through the very intent the debug menu
    /// sends, and reports for each one whether it got a live figure and whether it got a
    /// picture.</para>
    ///
    /// <para><c>scripts/unity.sh shot Odyssey.EditorTools.ColonyCastProbe.Run</c> — the graphics
    /// device is for the portraits.</para>
    /// </summary>
    public static class ColonyCastProbe
    {
        const float FrameSeconds = 1f / 60f;

        /// <summary>The scenario's own five, then this many more through the debug intent.</summary>
        const int Extra = 80;

        [MenuItem("Odyssey/Presentation/Probe a growing colony")]
        public static void RunFromMenu() => Execute(false);

        public static void Run() => Execute(Application.isBatchMode);

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            PawnFigureDirector? figures = null;
            ColonistMaterials? materials = null;
            PortraitStudio? studio = null;
            GameObject? root = null;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                var size = new GridSize(PlayScene.PlaySizeXZ, PlayScene.PlaySizeXZ, PlayScene.PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
                gen.MakeWooded();
                var grid = new CellGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);
                int activeLayer = result.StartCell.Y;
                var slice = new SliceSettings();
                var chunks = new ChunkGrid(size);

                var nav = new NavGraph(grid);
                nav.Rebuild();
                var pawns = new PawnContext(
                    grid, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns())
                    { Chunks = chunks };
                var support = new SupportSystem(grid, new SupportSolver(grid), chunks);
                var designations = new Odyssey.Sim.Designations.DesignationGrid(grid, result.Edifices);

                SimWorld world = new SimWorldBuilder()
                    .WithSeed(1u)
                    .WithSize(size)
                    .AddColony(pawns, designations, support, nav, result.Placements, out _)
                    .Build();

                ScenarioDef scenario = ScenarioDef.Bare();
                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, scenario);

                root = new GameObject("ColonyCastRoot");
                PlayScene.BuildSheetLighting(root.transform);

                ColonistAppearanceBook appearances = AppearanceBooks.For(1u, catalogue);
                ColonistMaterials.AdoptInkFrom();
                materials = new ColonistMaterials();
                figures = new PawnFigureDirector(catalogue, root.transform, 0)
                {
                    Appearances = appearances,
                    Materials = materials,
                };
                studio = new PortraitStudio(catalogue, materials)
                {
                    Appearances = appearances,
                };

                int movePerTick = ContentPack.Pawns().Movement.movePerTick;
                void Frame()
                {
                    world.Tick();
                    figures!.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                }

                for (int tick = 0; tick < 30; tick++) Frame();

                int startingCount = world.Views.Current.Pawns.Length;

                // Exactly what the debug menu's Spawn colonist row sends, at the cell the
                // scenario started from.
                // Spread along a line away from the start, so "nearest the camera" is a question
                // with an answer: eighty-five colonists in one cell is all one distance and any
                // ordering looks the same.
                for (int i = 0; i < Extra; i++)
                {
                    var at = new CellRef(
                        Mathf.Clamp(result.StartCell.X + 1 + i % (size.SizeX - result.StartCell.X - 3),
                                    1, size.SizeX - 2),
                        Mathf.Clamp(result.StartCell.Z + i / (size.SizeX - result.StartCell.X - 3),
                                    1, size.SizeZ - 2),
                        result.StartCell.Y);
                    world.Intents.Submit(new Intent(IntentKind.SpawnPawn, at));
                    Frame();
                }

                // Stand the viewer over the first colonist, so "nearest the camera" means
                // something and the cap has a direction to choose along.
                // At the FAR end of the line the spawns run along, so the colonists nearest the
                // camera are the ones with the *highest* ids. Put the camera at the near end and
                // id order and distance order agree, and the old behaviour — take the first
                // sixty-four by id — would look identical to taking the nearest sixty-four.
                Vector3 eye = CellMetrics.FloorCentre(
                    new CellRef(size.SizeX - 2, size.SizeZ - 2, result.StartCell.Y))
                    + new Vector3(0f, 40f, 0f);
                figures.ViewerPosition = eye;

                for (int tick = 0; tick < 30; tick++) Frame();

                WorldSnapshot frame = world.Views.Current;
                var report = new StringBuilder();
                report.AppendLine($"=== a colony of {frame.Pawns.Length} " +
                                  $"({startingCount} placed, {Extra} spawned) ===");
                report.AppendLine($"looks {figures.LookCount}, usable {figures.UsableLookCount}, " +
                                  $"figures alive {figures.FigureCount}, drawn {figures.Drawn.Count}, " +
                                  $"cap {figures.MaxFigures}");
                report.AppendLine("pawn  layer  rollSeed    look  figure  portrait");

                int noFigure = 0, noPortrait = 0, emptyPortrait = 0;
                float farthestDrawn = 0f, nearestSkipped = float.MaxValue;
                var seen = new Dictionary<int, int>();

                foreach (PawnView pawn in frame.Pawns)
                {
                    uint rollSeed = ColonistNames.RollSeedOf(frame, pawn.Id);
                    int look = appearances.LookFor(frame, pawn.Id);
                    bool hasFigure = figures.HasFigureFor(pawn.Id.Value);
                    if (!hasFigure) noFigure++;
                    seen.TryGetValue(look, out int already);
                    seen[look] = already + 1;

                    Texture2D? shot = studio.For(frame, pawn.Id);
                    string picture;
                    if (shot == null)
                    {
                        picture = "NULL";
                        noPortrait++;
                    }
                    else
                    {
                        Color32[] pixels = shot.GetPixels32();
                        long opaque = 0;
                        for (int p = 0; p < pixels.Length; p++) if (pixels[p].a > 8) opaque++;
                        double percent = 100.0 * opaque / pixels.Length;
                        if (percent < 2.0) { picture = $"EMPTY {percent:F1}%"; emptyPortrait++; }
                        else picture = $"ok {percent:F1}%";
                    }

                    float range = Vector3.Distance(CellMetrics.FloorCentre(pawn.Cell), eye);
                    if (hasFigure) { if (range > farthestDrawn) farthestDrawn = range; }
                    else if (range < nearestSkipped) nearestSkipped = range;

                    report.AppendLine(
                        $"{pawn.Id.Value,4}  {pawn.Cell.Y,5}  {rollSeed,10}  {look,4}  " +
                        $"{(hasFigure ? "yes   " : "NO    ")}  {range,6:F1} m  {picture}");
                }

                report.AppendLine();
                report.AppendLine($"farthest animated {farthestDrawn:F1} m, " +
                                  $"nearest left baked {(nearestSkipped == float.MaxValue ? 0f : nearestSkipped):F1} m");
                report.AppendLine($"no figure {noFigure}, null portrait {noPortrait}, " +
                                  $"empty portrait {emptyPortrait}, distinct looks {seen.Count}");
                report.AppendLine($"portraits cached {studio.Portraits}, " +
                                  $"render textures {studio.LiveRenderTextures}");
                Debug.Log(report.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError("[ColonyCast] " + e);
                exitCode = 1;
            }
            finally
            {
                studio?.Dispose();
                figures?.Dispose();
                materials?.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }

            if (exitWhenDone) EditorApplication.Exit(exitCode);
        }
    }
}
