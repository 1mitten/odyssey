#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
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
    /// Photograph a colonist standing in a pit she has just cut, with banks allowed inside the
    /// working and then forbidden.
    ///
    /// <para><b>The fault this exists to show.</b> Grass, bare earth and subsoil are all mineable,
    /// so a quarry sunk into the meadow is a hole whose walls are earth with open tops — every
    /// condition <c>ChunkMesher.EmitBank</c> asks of a terrace step. A bank therefore grew inside
    /// the cell that had just been cut, filling it from its floor to the rim with a stepped ramp,
    /// and the miner standing in it was drawn up to the chest in ground. The owner reported it as
    /// colonists disappearing wherever people were mining.</para>
    ///
    /// <para><b>Why a photograph as well as a test.</b> <c>BankMeshTests</c> can prove that no bank
    /// instance is emitted in a cut cell, and does. It cannot say whether the pit that is left
    /// reads as a working — sheer earth walls at the cell scale might have been the wrong picture
    /// even though they are the honest one — and that is a judgement only the owner can make. The
    /// two conditions differ by exactly one lever, <c>ChunkRenderer.BanksInWorkings</c>, so the
    /// pair is a comparison rather than a memory.</para>
    ///
    /// <para><b>The quarry is cut under the colonist rather than beside her.</b>
    /// <c>MineJobDriver.MineCell</c> steps whoever was standing on a cell down onto the floor it
    /// just cut, so mining the block under a colonist's feet puts her in the hole exactly as the
    /// game does. Anything else would be a pit with a figure posed next to it, which is a picture
    /// of a different question.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.QuarryCheck.Run</c>. It needs a
    /// real graphics device, so not under <c>-nographics</c>.</para>
    /// </summary>
    public static class QuarryCheck
    {
        const float FrameSeconds = 1f / 60f;

        /// <summary>How far the pit reaches from the colonist's own cell, in cells.</summary>
        const int Reach = 1;

        [MenuItem("Odyssey/Presentation/Check a quarry")]
        public static void RunFromMenu() => Shoot(exitWhenDone: false);

        public static void Run() => Shoot(Application.isBatchMode);

        static void Shoot(bool exitWhenDone)
        {
            Execute(exitWhenDone);
            if (!exitWhenDone) ShotFolder.Reveal("quarry-*.png");
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
                    grid, nav, new PathService(new PathFinder(nav)), PawnContent.Core())
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

                // Bare, so nothing is marked and nobody walks off to a job in the middle of the
                // sheet. The pit is cut by hand below; what is being photographed is the hole, not
                // the work giver that would have chosen it.
                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, ScenarioDef.Bare());

                lightingRoot = new GameObject("QuarryRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                figures = new PawnFigureDirector(catalogue, lightingRoot.transform, 0) { World = model };
                if (!figures.Enabled)
                    Debug.LogWarning("[Quarry] no live figures: no character art or no gait clips. " +
                                     "The pictures will show the baked meshes and prove little.");

                int activeLayer = result.StartCell.Y;
                int movePerTick = PawnContent.Core().Movement.movePerTick;

                // Settle, and warm the character material while settling: it draws flat yellow on
                // the first frame it is touched, which is the trap ClimbCheck records.
                for (int tick = 0; tick < 120; tick++)
                {
                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                }

                PawnView subject = FirstDrawn(world.Views.Current, figures);
                if (subject.Id.Value == 0)
                {
                    Debug.LogError("[Quarry] no colonist is being drawn; nothing to photograph.");
                    exitCode = 1;
                    return;
                }

                int cut = Excavate(pawns, size, subject.Cell);
                model.RefreshAll(grid, result.Edifices);

                // One tick to publish the pawn where the cut left her — MineCell steps whoever was
                // standing on the cell down onto the floor it made — and a few more for the figure
                // to stop moving. Kept short on purpose: a colonist in a one-layer pit can hop out
                // of it, and a sheet of an empty hole proves nothing.
                for (int tick = 0; tick < 6; tick++)
                {
                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                }

                PawnView inThePit = Find(world.Views.Current, subject.Id);
                activeLayer = inThePit.Cell.Y;
                slice.surfaceLayer = inThePit.Cell.Y;
                Debug.Log($"[Quarry] cut {cut} cells under {subject.Cell}; " +
                          $"colonist {subject.Id.Value} is now at {inThePit.Cell}, " +
                          $"active layer {activeLayer}");

                cameraObject = new GameObject("QuarryCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);

                foreach (bool banksInside in new[] { true, false })
                {
                    string name = banksInside ? "before" : "after";

                    // A renderer per condition, for the reason SlopeCheck gives: the choice is made
                    // at mesh time and baked into the buckets, so a renderer built under one
                    // setting keeps it however the lever is moved afterwards.
                    renderer = new ChunkRenderer(model) { BanksInWorkings = banksInside };
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

                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);

                    Vector3 chest = (figures.TryGetFeet(subject.Id, out Vector3 feet) ? feet : Vector3.zero) +
                                    Vector3.up * 1.0f;

                    // The view the game is judged in, close enough that one colonist fills it.
                    PlayScene.Shoot(camera, chest, 48f, 45f, 11f, $"Logs/quarry-{name}-play.png");
                    // Low and across, where the walls of the pit have a profile and a figure
                    // standing in one is either behind them or not.
                    //
                    // The pitch and the distance are a pair, and the first attempt got them wrong
                    // in exactly the way SlopeCheck records: 18 degrees at 9 m puts the camera
                    // 2.8 m up, which is inside a 3 m pit, and the picture was the floor and one
                    // enormous colonist. 30 degrees at 22 m clears the rim and still reads as a
                    // profile.
                    PlayScene.Shoot(camera, chest, 30f, 45f, 22f, $"Logs/quarry-{name}-low.png");
                    // Nearly straight down, which is the one shot that cannot be argued with: if
                    // the pit has filled in, the floor is a staircase and the colonist is under it.
                    PlayScene.Shoot(camera, chest, 80f, 45f, 14f, $"Logs/quarry-{name}-over.png");

                    RenderPipelineManager.beginCameraRendering -= hook;
                    hook = null;

                    Debug.Log($"[Quarry] {name}: banks inside the working {banksInside} — " +
                              $"{BanksInThePit(model, inThePit.Cell, banksInside)} bank instances in " +
                              $"its chunk, {Buried(model, world.Views.Current, banksInside)} of " +
                              $"{world.Views.Current.Pawns.Length} colonists standing in one, " +
                              $"{renderer.DrawCalls} draw calls, {renderer.InstancesDrawn} instances");

                    renderer.Dispose();
                    renderer = null;
                }

                Debug.Log("[Quarry] wrote Logs/quarry-{before,after}-{play,low,over}.png");
            }
            catch (Exception error)
            {
                Debug.LogError($"[Quarry] {error}");
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                figures?.Dispose();
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
        /// Cut the block under a colonist's feet and its neighbours, and report how many cells went.
        ///
        /// <para>Through <see cref="MineJobDriver.MineCell"/> rather than by clearing the terrain
        /// by hand, because the mark the mesher reads is one of the five things that routine does
        /// and a harness that set the world up its own way would be photographing a case the game
        /// cannot reach.</para>
        /// </summary>
        static int Excavate(PawnContext pawns, GridSize size, CellRef standing)
        {
            CellGrid grid = pawns.Cells;
            int floorY = standing.Y - 1;
            if (floorY < 1) return 0;

            // Outward from the middle, so the colonist's own block goes first and she is standing
            // on the pit floor while the walls around her are still being taken out.
            var order = new List<int>();
            for (int radius = 0; radius <= Reach; radius++)
            for (int dz = -Reach; dz <= Reach; dz++)
            for (int dx = -Reach; dx <= Reach; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius) continue;
                int x = standing.X + dx, z = standing.Z + dz;
                if (x < 1 || z < 1 || x >= size.SizeX - 1 || z >= size.SizeZ - 1) continue;
                order.Add(size.Index(x, z, floorY));
            }

            int cut = 0;
            foreach (int index in order)
            {
                if (!grid.IsSolidTerrain(index)) continue;
                ushort terrain = grid.Terrain[index];
                if (terrain == NaturalContent.TerrainBedrock) continue;

                MineJobDriver.MineCell(pawns, index, terrain);
                cut++;
            }

            return cut;
        }

        /// <summary>
        /// How many bank instances stand in the chunk holding the working, counted by meshing it
        /// rather than by trusting the picture.
        ///
        /// <para>The number the sheet is really about: before should be larger than after, after
        /// must have none in the pit, and a run where both are zero is a run of a pit that was
        /// never dug.</para>
        /// </summary>
        static int BanksInThePit(WorldRenderModel model, CellRef pit, bool banksInside)
        {
            var banks = new HashSet<int>();
            for (ushort terrain = 0; terrain < (ushort)NaturalContent.TerrainCount; terrain++)
            for (int kind = 0; kind < BankMesh.Kinds; kind++)
            {
                int module = model.BankModuleFor(terrain, kind);
                if (module != 0) banks.Add(module);
            }

            var batch = new ChunkBatch();
            var mesher = new ChunkMesher(model) { BanksInWorkings = banksInside };
            mesher.Mesh(batch, model.Chunks.ChunkIndexOfCell(pit.X, pit.Z, pit.Y));

            int n = 0;
            foreach (InstanceBucket bucket in batch.Body)
                if (banks.Contains(bucket.Module)) n += bucket.Count;
            return n;
        }

        /// <summary>
        /// How many colonists are standing in a cell that has a bank drawn in it.
        ///
        /// <para>The question the whole change is about, asked of the world rather than of the
        /// picture. A bank fills its cell from the floor to the rim, and a pawn is drawn standing
        /// at the middle of its cell, so a colonist in a bank cell is a colonist waist-deep in
        /// ground whether the bank is a quarry wall or a hillside.</para>
        ///
        /// <para>Matched by position rather than by asking the mesher, because which cell emitted
        /// an instance is not something a batch records: a bank's matrix is the drape of its own
        /// floor centre, so the translation column is the cell, exactly.</para>
        /// </summary>
        static int Buried(WorldRenderModel model, WorldSnapshot snapshot, bool banksInside)
        {
            var banks = new HashSet<int>();
            for (ushort terrain = 0; terrain < (ushort)NaturalContent.TerrainCount; terrain++)
            for (int kind = 0; kind < BankMesh.Kinds; kind++)
            {
                int module = model.BankModuleFor(terrain, kind);
                if (module != 0) banks.Add(module);
            }

            int buried = 0;
            foreach (PawnView pawn in snapshot.Pawns)
            {
                var batch = new ChunkBatch();
                var mesher = new ChunkMesher(model) { BanksInWorkings = banksInside };
                mesher.Mesh(batch, model.Chunks.ChunkIndexOfCell(pawn.Cell));

                if (!StandsInABank(batch, banks, pawn.Cell)) continue;
                buried++;
                Debug.Log($"[Quarry] colonist {pawn.Id.Value} at {pawn.Cell} is standing in a bank");
            }

            return buried;
        }

        /// <summary>
        /// Is one of these bank instances standing in this cell?
        ///
        /// <para>Matched on the ground plane alone, and the vertical is where the first attempt
        /// went wrong: an instance matrix is <c>placement * part.Local</c>, and a module's local
        /// transform puts the mesh's own origin at the middle of the cell rather than at its
        /// floor. So every bank sat 1.46 m above the floor centre it was draped on, an exact match
        /// found nothing, and the harness reported no colonist in a bank on a board where the
        /// photograph plainly showed two. A layer is 3 m and a bank is the only thing drawn in an
        /// empty cell, so a hit within half a layer is unambiguous.</para>
        /// </summary>
        static bool StandsInABank(ChunkBatch batch, HashSet<int> banks, CellRef cell)
        {
            Vector3 want = GroundRelief.Lift(CellMetrics.FloorCentre(cell));
            foreach (InstanceBucket bucket in batch.Body)
            {
                if (!banks.Contains(bucket.Module)) continue;
                for (int i = 0; i < bucket.Count; i++)
                {
                    Vector3 at = bucket.Matrices[i].GetColumn(3);
                    if (Mathf.Abs(at.x - want.x) > 0.01f || Mathf.Abs(at.z - want.z) > 0.01f) continue;
                    if (Mathf.Abs(at.y - want.y) <= CellMetrics.SizeY * 0.5f) return true;
                }
            }

            return false;
        }

        /// <summary>The first colonist the director is actually drawing, or a default view.</summary>
        static PawnView FirstDrawn(WorldSnapshot snapshot, PawnFigureDirector figures)
        {
            foreach (PawnView pawn in snapshot.Pawns)
                if (figures.Drawn.Contains(pawn.Id.Value)) return pawn;
            return default;
        }

        static PawnView Find(WorldSnapshot snapshot, PawnId id)
        {
            foreach (PawnView pawn in snapshot.Pawns)
                if (pawn.Id.Value == id.Value) return pawn;
            return default;
        }
    }
}
