#nullable enable
using System;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
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
    /// Photograph a colonist standing at the foot of a terrace, in the bank and then on it.
    ///
    /// <para><b>The fault.</b> A bank fills the cell it stands in from the floor to the rim, and
    /// that cell is walkable — it is the cell at the foot of the terrace, which is the take-off
    /// cell for the hop the bank is a picture of. A figure drawn at its cell's floor is therefore
    /// waist-deep in the ramp, which is the same fault as the miner buried in a quarry with the
    /// opposite answer: there the bank should not have been drawn, here it should, and the figure
    /// has to come up to meet it.</para>
    ///
    /// <para><b>The step is built rather than found.</b> <c>SlopeCheck</c> hunts the board for its
    /// longest run of one-layer step, which is right when the terrace is the subject. Here the
    /// colonist is the subject and she has to be standing in the bank, so the ground is raised
    /// beside her instead — a block of earth laid on the neighbouring columns, which is a natural
    /// step and not a cut one, so it grows a bank exactly as a generated terrace does.</para>
    ///
    /// <para><b>What to look for, and it is two things.</b> Whether the boots are on the slope
    /// rather than through it — <c>MeasuredFootGap</c> is printed beside each picture and says so
    /// in metres. And whether a figure standing *up* on a fifty-degree ramp reads as standing on a
    /// hillside or as levitating: <c>GroundRelief</c>'s rule is that ground lies along the slope
    /// and people stand up on it, but a bank is far steeper than the rolling field that rule was
    /// written for. If it reads wrong, the answer is <c>Footing</c> — which already leans the root
    /// to the ground normal and plants both feet with <c>TwoBoneIk</c> — being given the bank's own
    /// gradient instead of only the relief field's.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.BankCheck.Run</c>. It needs a
    /// real graphics device, so not under <c>-nographics</c>.</para>
    /// </summary>
    public static class BankCheck
    {
        const float FrameSeconds = 1f / 60f;

        /// <summary>How far along the terrace the raised ground runs, in cells either way.</summary>
        const int Reach = 4;

        [MenuItem("Odyssey/Presentation/Check a bank underfoot")]
        public static void RunFromMenu() => Shoot(exitWhenDone: false);

        public static void Run() => Shoot(Application.isBatchMode);

        static void Shoot(bool exitWhenDone)
        {
            Execute(exitWhenDone);
            if (!exitWhenDone) ShotFolder.Reveal("bank-*.png");
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
                    .AddColony(pawns, designations, support, nav)
                    .Build();

                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, ScenarioDef.Bare());

                lightingRoot = new GameObject("BankRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                figures = new PawnFigureDirector(catalogue, lightingRoot.transform, 0) { World = model };
                if (!figures.Enabled)
                    Debug.LogWarning("[Bank] no live figures: no character art or no gait clips. " +
                                     "The pictures will show the baked meshes and prove little.");

                int activeLayer = result.StartCell.Y;
                int movePerTick = ContentPack.Pawns().Movement.movePerTick;

                for (int tick = 0; tick < 120; tick++)
                {
                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                }

                PawnView subject = FirstDrawn(world.Views.Current, figures);
                if (subject.Id.Value == 0)
                {
                    Debug.LogError("[Bank] no colonist is being drawn; nothing to photograph.");
                    exitCode = 1;
                    return;
                }

                int raised = RaiseTheGroundBeside(grid, size, subject.Cell);
                int felled = ClearTheTreesAround(grid, size, subject.Cell);
                model.RefreshAll(grid, result.Edifices);

                BankLayout.Bank bank = BankLayout.At(model, subject.Cell);
                Debug.Log($"[Bank] raised {raised} columns beside {subject.Cell}, " +
                          $"cleared {felled} trees around it; " +
                          $"bank there: {(bank.Exists ? $"{bank.Kind} at bearing {bank.Rotation}" : "NONE")}");
                if (!bank.Exists)
                {
                    Debug.LogError("[Bank] the colonist is not standing in a bank; the sheet would prove nothing.");
                    exitCode = 1;
                    return;
                }

                activeLayer = subject.Cell.Y;
                slice.surfaceLayer = subject.Cell.Y;

                cameraObject = new GameObject("BankCamera");
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

                // The bearing to look from: across the slope, so the profile of the ramp and the
                // figure on it are both in the picture. The bank climbs towards its step, and
                // Directions.Yaw is the bearing of that, so ninety degrees off it is side on.
                float sideOn = Directions.Yaw[bank.Rotation] + 90f;

                foreach (bool lift in new[] { false, true })
                {
                    string name = lift ? "on" : "in";
                    BankLayout.LiftFigures = lift;

                    // Two frames: the lift moves the figure, and the first Evaluate after a move is
                    // where the gait blend catches up with it.
                    for (int frame = 0; frame < 2; frame++)
                    {
                        figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                        figures.Evaluate(FrameSeconds);
                    }

                    figures.TryGetFeet(subject.Id, out Vector3 feet);
                    Vector3 chest = feet + Vector3.up * 1.0f;

                    // The number the sheet is about: how far the boots are from the surface they
                    // are meant to be on. Negative is through it.
                    //
                    // Measured through the overload that takes a bank already in hand, which is
                    // *not* gated on LiftFigures. The first version asked the gated one and so
                    // reported a perfect 0.000 m in both conditions — an instrument wired to the
                    // thing it was measuring, which is the most convincing way to measure nothing.
                    float surface = GroundRelief.Lift(CellMetrics.FloorCentre(subject.Cell)).y +
                                    BankLayout.RiseAt(bank, subject.Cell, feet.x, feet.z);

                    // Far enough out to clear the woodland: at 9 m the camera stood inside a tree
                    // trunk and the whole picture was bark. The trees are the board's, not the
                    // harness's, so the answer is distance rather than a clearing.
                    PlayScene.Shoot(camera, chest, 20f, sideOn, 16f, $"Logs/bank-{name}-sideon.png");
                    PlayScene.Shoot(camera, chest, 48f, 45f, 11f, $"Logs/bank-{name}-play.png");
                    PlayScene.Shoot(camera, chest, 30f, sideOn + 40f, 14f, $"Logs/bank-{name}-across.png");
                    // Close enough to read the boots, which is the one question the wider shots
                    // cannot answer: Footing plants both feet on the relief field's slope, and a
                    // bank is fifty degrees where that field is eight, so a stance 0.3 m across
                    // spans about 0.36 m of slope the feet know nothing about. Whether that shows
                    // decides whether the gradient has to be plumbed through as well.
                    PlayScene.Shoot(camera, feet + Vector3.up * 0.9f, 8f, sideOn, 4.5f,
                        $"Logs/bank-{name}-boots.png");

                    Debug.Log($"[Bank] {name}: lift {lift} — MeasuredFootGap " +
                              $"{feet.y - surface:0.000} m (0 is boots on the slope, negative is " +
                              $"through it), feet at {feet}");

                    // And the same of everyone else on the board, because one figure measured is
                    // one figure measured. A colonist can be behind a ramp as easily as inside
                    // one, and a photograph cannot tell the two apart from any bearing.
                    ReportEveryone(model, figures, world.Views.Current, name);
                }

                BankLayout.Reset();
                Debug.Log("[Bank] wrote Logs/bank-{in,on}-{sideon,play,across}.png");
            }
            catch (Exception error)
            {
                Debug.LogError($"[Bank] {error}");
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                figures?.Dispose();
                renderer?.Dispose();
                library?.Dispose();
                BankLayout.Reset();
                GroundRelief.Amplitude = amplitudeWas;
                GroundRelief.Period = periodWas;
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// Lay a block of earth on the columns beside the colonist, making a one-layer terrace step
        /// running past her, and report how many columns took one.
        ///
        /// <para>Laid rather than mined, and that is the whole point of the harness: a cut face
        /// stays sheer (<c>BankLayout.IsCutFace</c>), so a step made by digging grows no bank and
        /// there would be nothing to stand on. Grass on top because a bank is made of the terrain
        /// at the top of the step it climbs, and a subsoil ramp out of a meadow would read as a
        /// different fault.</para>
        /// </summary>
        static int RaiseTheGroundBeside(CellGrid grid, GridSize size, CellRef standing)
        {
            int raised = 0;
            for (int d = -Reach; d <= Reach; d++)
            {
                int x = standing.X - 1;
                int z = standing.Z + d;
                if (x < 1 || z < 1 || x >= size.SizeX - 1 || z >= size.SizeZ - 1) continue;

                int index = size.Index(x, z, standing.Y);
                if (grid.IsSolidTerrain(index)) continue;
                if (!grid.IsSolidTerrain(index - size.LayerStride)) continue;

                grid.Terrain[index] = NaturalContent.TerrainGrass;
                grid.Flags[index] |= CellFlags.SolidTerrain;
                raised++;
            }

            return raised;
        }

        /// <summary>
        /// Take the woodland out of the way, and say how many trees went.
        ///
        /// <para>Not fastidiousness. The board the scene loads is a wooded meadow at the natural
        /// generator's own density, and two sheets in a row put a trunk squarely between the camera
        /// and the colonist — from a bearing the harness has no freedom to choose, because side on
        /// to the bank is the only view in which a figure's boots and the slope under them are both
        /// legible. Backing the camera off cleared the trunk it was standing inside and found
        /// another one. The trees are the board's rather than the subject's, so they go.</para>
        /// </summary>
        static int ClearTheTreesAround(CellGrid grid, GridSize size, CellRef middle)
        {
            const int radius = 6;
            int felled = 0;

            for (int y = middle.Y - 1; y <= middle.Y + 1; y++)
            for (int z = middle.Z - radius; z <= middle.Z + radius; z++)
            for (int x = middle.X - radius; x <= middle.X + radius; x++)
            {
                if (!size.Contains(x, z, y)) continue;
                int index = size.Index(x, z, y);
                if (grid.Edifice[index] < 0) continue;
                grid.RemoveEdifice(index);
                felled++;
            }

            return felled;
        }

        /// <summary>
        /// Every drawn colonist who is standing in a bank cell, and how far their boots are from
        /// the slope they should be on.
        ///
        /// <para>The check that a photograph cannot make. A figure sunk into a ramp and a figure
        /// standing behind one look the same from every bearing, because a bank is opaque and
        /// nearly as tall as a person; only the arithmetic separates them.</para>
        /// </summary>
        static void ReportEveryone(WorldRenderModel model, PawnFigureDirector figures,
            WorldSnapshot snapshot, string name)
        {
            int inABank = 0;
            float worst = 0f;

            foreach (PawnView pawn in snapshot.Pawns)
            {
                BankLayout.Bank bank = BankLayout.At(model, pawn.Cell);
                if (!bank.Exists) continue;
                if (!figures.TryGetFeet(pawn.Id, out Vector3 feet)) continue;

                inABank++;
                float surface = GroundRelief.Lift(CellMetrics.FloorCentre(pawn.Cell)).y +
                                BankLayout.RiseAt(bank, pawn.Cell, feet.x, feet.z);
                float gap = feet.y - surface;
                if (Mathf.Abs(gap) > Mathf.Abs(worst)) worst = gap;

                Debug.Log($"[Bank] {name}: colonist {pawn.Id.Value} at {pawn.Cell} is in a " +
                          $"{bank.Kind} bank, gap {gap:0.000} m");
            }

            Debug.Log($"[Bank] {name}: {inABank} of {snapshot.Pawns.Length} colonists are standing " +
                      $"in a bank, worst gap {worst:0.000} m");
        }

        /// <summary>The first colonist the director is actually drawing, or a default view.</summary>
        static PawnView FirstDrawn(WorldSnapshot snapshot, PawnFigureDirector figures)
        {
            foreach (PawnView pawn in snapshot.Pawns)
                if (figures.Drawn.Contains(pawn.Id.Value)) return pawn;
            return default;
        }
    }
}
