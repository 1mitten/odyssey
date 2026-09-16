#nullable enable
using System;
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
    /// Photograph a colonist standing behind a tree, with the see-through fade off and then on.
    ///
    /// <para><b>The fault this exists to show.</b> The board is a wood and the camera orbits
    /// rather than cuts, so a colonist the player has selected walks under a canopy and is simply
    /// gone — and the only remedy was to spin the camera until a gap opened, losing the bearing
    /// and having to be done again the moment they moved (owner, 2026-09-16). The fade draws
    /// whatever stands on the line from the eye to a selected figure as a ghost.</para>
    ///
    /// <para><b>Why a photograph as well as a test.</b> <c>SightLineTests</c> can prove that the
    /// right instances are partitioned into the ghost draw, and does. It cannot say whether the
    /// result <i>reads</i> — whether a ghosted conifer is a helpful hint of what is there or a
    /// green smear that is worse than the tree was, and whether <see cref="ChunkRenderer.SightFadeAlpha"/>
    /// is anywhere near right. That is the owner's judgement and it needs a picture.</para>
    ///
    /// <para><b>The subject is found, not staged.</b> Every drawn colonist is measured against the
    /// camera this sheet will actually use, and the one with the most geometry in the way is
    /// photographed. A tree planted in front of a colonist on purpose would photograph the
    /// harness; the point is that the wood the generator grew does this by itself. If nobody on
    /// the board is behind anything, the sheet says so rather than quietly shooting a clear view
    /// and looking like a success.</para>
    ///
    /// <para><b>Two bearings, because occlusion is a bearing.</b> Which trees are in the way is
    /// entirely a fact about where the camera is standing, so one yaw would be one accident. The
    /// second is the control in the strongest sense available: the same colonist, the same wood,
    /// and a different set of trees doing the hiding.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.SeeThroughCheck.Run</c>. It
    /// needs a real graphics device, so not under <c>-nographics</c>.</para>
    /// </summary>
    public static class SeeThroughCheck
    {
        const float FrameSeconds = 1f / 60f;

        /// <summary>The view the game is judged in: the board camera's own pitch, close in.</summary>
        const float Pitch = 48f;
        const float Distance = 30f;

        /// <summary>Where a colonist's bracket is centred, which is what the sight line aims at.</summary>
        const float ChestHeight = 1.35f;

        [MenuItem("Odyssey/Presentation/Check the see-through fade")]
        public static void RunFromMenu() => Shoot(exitWhenDone: false);

        public static void Run() => Shoot(Application.isBatchMode);

        static void Shoot(bool exitWhenDone)
        {
            Execute(exitWhenDone);
            if (!exitWhenDone) ShotFolder.Reveal("seethrough-*.png");
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

                // Bare, so nobody walks off to a job mid-sheet and the two conditions are the same
                // picture with one lever moved.
                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, ScenarioDef.Bare());

                lightingRoot = new GameObject("SeeThroughRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                figures = new PawnFigureDirector(catalogue, lightingRoot.transform, 0) { World = model };
                if (!figures.Enabled)
                    Debug.LogWarning("[SeeThrough] no live figures: no character art or no gait clips. " +
                                     "The pictures will show the baked meshes and prove little.");

                int activeLayer = result.StartCell.Y;
                slice.surfaceLayer = result.StartCell.Y;
                int movePerTick = PawnContent.Core().Movement.movePerTick;

                // Settle, and warm the character material while settling: it draws flat yellow on
                // the first frame it is touched, which is the trap ClimbCheck records.
                for (int tick = 0; tick < 120; tick++)
                {
                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                }

                renderer = new ChunkRenderer(model);
                renderer.Skirt.Enabled = true;
                renderer.Skirt.Build();

                var sight = new SightLines();
                renderer.Sight = sight;

                // Who is behind the most, at which bearing? Measured against the camera this sheet
                // will use, with nothing submitted — the counts come out of the renderer whether or
                // not there is a frame, which is what makes choosing the subject a measurement.
                renderer.SubmitToGpu = false;
                PawnId subject = PawnId.None;
                float bestYaw = 45f;
                Vector3 bestChest = Vector3.zero;
                int most = 0;

                foreach (PawnView pawn in world.Views.Current.Pawns)
                {
                    if (!figures.Drawn.Contains(pawn.Id.Value)) continue;
                    if (!figures.TryGetFeet(pawn.Id, out Vector3 feet)) continue;
                    Vector3 chest = feet + Vector3.up * ChestHeight;

                    foreach (float yaw in new[] { 45f, 135f, 225f, 315f })
                    {
                        sight.Clear();
                        sight.Add(EyeFor(chest, Pitch, yaw, Distance), chest);
                        renderer.Render(activeLayer, slice);
                        if (renderer.InstancesFaded <= most) continue;

                        most = renderer.InstancesFaded;
                        subject = pawn.Id;
                        bestYaw = yaw;
                        bestChest = chest;
                    }
                }

                renderer.SubmitToGpu = true;

                if (!subject.IsValid)
                {
                    Debug.LogError("[SeeThrough] no colonist is being drawn; nothing to photograph.");
                    exitCode = 1;
                    return;
                }

                if (most == 0)
                    Debug.LogWarning("[SeeThrough] nobody on this board is standing behind anything " +
                                     "at any of the four bearings. The pictures below are of a clear " +
                                     "view and prove nothing about the fade.");

                Debug.Log($"[SeeThrough] colonist {subject.Value} at yaw {bestYaw}: " +
                          $"{most} instances in the way");

                cameraObject = new GameObject("SeeThroughCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);

                bool fade = false;
                ChunkRenderer active = renderer;
                Vector3 chestOf = bestChest;

                // The eye is read off the camera rather than recomputed, which is the whole reason
                // this hangs on the render callback: what fades has to be decided from where the
                // camera really is, exactly as the game decides it in LateUpdate.
                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    active.ViewerPosition = rendering.transform.position;
                    sight.Clear();
                    if (fade) sight.Add(rendering.transform.position, chestOf);
                    active.Render(activeLayer, slice);
                };
                RenderPipelineManager.beginCameraRendering += hook;

                figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                figures.Evaluate(FrameSeconds);

                foreach (bool on in new[] { false, true })
                {
                    fade = on;
                    string name = on ? "on" : "off";

                    // The bearing the subject was chosen at, and the one opposite it. Occlusion is
                    // a bearing, so a single yaw would be a single accident.
                    PlayScene.Shoot(camera, chestOf, Pitch, bestYaw, Distance,
                        $"Logs/seethrough-{name}-near.png");
                    Debug.Log($"[SeeThrough] {name} near: {renderer.InstancesFaded} faded of " +
                              $"{renderer.InstancesDrawn} instances, {renderer.DrawCalls} draw calls, " +
                              $"{renderer.ChunksSightTested} chunks tested");

                    PlayScene.Shoot(camera, chestOf, Pitch, bestYaw + 180f, Distance,
                        $"Logs/seethrough-{name}-far.png");
                    Debug.Log($"[SeeThrough] {name} far: {renderer.InstancesFaded} faded of " +
                              $"{renderer.InstancesDrawn} instances, {renderer.DrawCalls} draw calls");

                    // Shallow, where the eye passes through more wood to reach the same person and
                    // the fade has the most to do. It is also the pitch at which fading the ground
                    // would show, if the beam ever caught it.
                    PlayScene.Shoot(camera, chestOf, 22f, bestYaw, Distance,
                        $"Logs/seethrough-{name}-low.png");
                    Debug.Log($"[SeeThrough] {name} low: {renderer.InstancesFaded} faded of " +
                              $"{renderer.InstancesDrawn} instances");
                }

                RenderPipelineManager.beginCameraRendering -= hook;
                hook = null;

                Debug.Log("[SeeThrough] wrote Logs/seethrough-{off,on}-{near,far,low}.png");
            }
            catch (Exception error)
            {
                Debug.LogError($"[SeeThrough] {error}");
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
        /// Where <see cref="PlayScene.Shoot"/> will put the camera, computed ahead of it.
        ///
        /// It has to be the same arithmetic, or the subject is chosen against one camera and
        /// photographed by another — which would look exactly like the fade being unreliable.
        /// </summary>
        static Vector3 EyeFor(Vector3 focus, float pitch, float yaw, float distance) =>
            focus - Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward * distance;
    }
}
