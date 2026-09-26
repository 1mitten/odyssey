#nullable enable
using System;
using Odyssey.Hud;
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
    /// Two colonists talking, photographed through the real figure director (design 59 §5b): the
    /// talking hands are angles laid over a live clip, and this project tunes angles by picture.
    ///
    /// <para>Writes <c>Logs/talk-*.png</c> — the speaker each time a hand is up, from the front and a
    /// little to the side, and <c>talk-board.png</c> from the game's own camera angle — then runs a
    /// bare colony for ninety seconds and logs how many conversations it struck up by itself (§5c).
    /// Exits 1 if no hand was ever raised, so it is a check as well as a sheet.</para>
    ///
    /// <para><c>scripts/unity.sh shot Odyssey.EditorTools.TalkCheck.Shoot</c>.</para>
    /// </summary>
    public static class TalkCheck
    {
        const float FrameSeconds = 1f / 60f;
        const int MaxShots = 6;

        [MenuItem("Odyssey/Presentation/Shoot the talking check")]
        public static void ShootFromMenu()
        {
            Execute(false);
            ShotFolder.Reveal("talk-*.png");
        }

        public static void Shoot() => Execute(Application.isBatchMode);

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            ChunkRenderer? renderer = null;
            ModuleLibrary? library = null;
            PawnFigureDirector? figures = null;
            GameObject? lightingRoot = null;
            GameObject? cameraObject = null;
            Action<ScriptableRenderContext, Camera>? hook = null;
            bool ambientWas = PawnFigureDirector.AmbientConversations;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                var size = new GridSize(PlayScene.PlaySizeXZ, PlayScene.PlaySizeXZ, PlayScene.PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
                gen.MakeWooded();
                var grid = new CellGrid(size);
                var chunks = new ChunkGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);
                int activeLayer = result.StartCell.Y;
                var slice = new SliceSettings();

                library = new ModuleLibrary(catalogue);
                var model = new WorldRenderModel(size, chunks, library);
                model.RefreshAll(grid, result.Natural!.Context.Edifices);
                renderer = new ChunkRenderer(model);

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

                // Bare: nobody has work, so the colonists stand and wander — the leisure a
                // conversation needs, and hands with nothing in them.
                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, ScenarioDef.Bare());

                lightingRoot = new GameObject("TalkRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);
                figures = new PawnFigureDirector(catalogue, lightingRoot.transform, 0);
                if (!figures.CanDrawColonists)
                {
                    Debug.LogError("[Talk] no colonist art resolved: nothing to photograph.");
                    exitCode = 1;
                    return;
                }
                int movePerTick = ContentPack.Pawns().Movement.movePerTick;

                for (int tick = 0; tick < 400; tick++) Frame(world, figures, activeLayer, slice, movePerTick, tick: true);

                // ---- two colonists held in conversation, the world standing still so they do too.
                PawnFigureDirector.AmbientConversations = false;
                PawnId a = default, b = default;
                foreach (PawnView pawn in world.Views.Current.Pawns)
                {
                    if (!figures.Drawn.Contains(pawn.Id.Value) || !PawnFigureDirector.CanTalk(in pawn)) continue;
                    if (!a.IsValid) a = pawn.Id;
                    else if (!b.IsValid) { b = pawn.Id; break; }
                }
                if (!a.IsValid || !b.IsValid)
                {
                    Debug.LogError("[Talk] fewer than two colonists drawn and awake.");
                    exitCode = 1;
                    return;
                }
                figures.StartConversation(a, b, 600f);

                cameraObject = new GameObject("TalkCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                ChunkRenderer active = renderer;
                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    active.ViewerPosition = rendering.transform.position;
                    active.Render(activeLayer, slice);
                };
                RenderPipelineManager.beginCameraRendering += hook;

                // Warm the character materials somewhere nobody is looking (GestureCheck's lesson).
                for (int warm = 0; warm < 4; warm++)
                {
                    Frame(world, figures, activeLayer, slice, movePerTick, tick: false);
                    PlayScene.Shoot(camera, Waist(figures, a), 6f, 0f, 7.5f, "Logs/talk-warm.png");
                }

                int shots = 0, sinceShot = 999, framesWithHands = 0;
                bool board = false;
                var arms = new System.Collections.Generic.HashSet<string>();
                for (int f = 0; f < 60 * 60 && (shots < MaxShots || !board); f++)
                {
                    Frame(world, figures, activeLayer, slice, movePerTick, tick: false);
                    sinceShot++;
                    PawnId speaker = figures.RoleOf(a) == TalkRole.Speaking ? a : b;
                    if (!figures.TryGetFace(speaker.Value, out _, out _, out float nod, out _, out _, out float hands)) continue;
                    if (hands > 0.3f) framesWithHands++;
                    if (hands < 0.85f || sinceShot < 30) continue;

                    figures.TryGetFacing(speaker, out float facing);
                    if (shots < MaxShots)
                    {
                        string path = $"Logs/talk-{shots}.png";
                        PlayScene.Shoot(camera, Waist(figures, speaker), 6f, facing + 150f, 5.5f, path);
                        Debug.Log($"[Talk] {path}: speaker {speaker}, hands {hands:0.00}, nod {nod:0.0} deg");
                        shots++;
                    }
                    else
                    {
                        PlayScene.Shoot(camera, Waist(figures, speaker), 48f, facing + 150f, 16f, "Logs/talk-board.png");
                        board = true;
                    }
                    sinceShot = 0;
                }

                // ---- and the colony left to itself: how often does it fall to talking?
                figures.EndConversations();
                PawnFigureDirector.AmbientConversations = true;

                // The control first: the two who were talking, still standing side by side, the world
                // held so nobody walks off. A rule that works strikes up a conversation here.
                float struckAfter = -1f;
                for (int f = 0; f < 60 * 20 && struckAfter < 0f; f++)
                {
                    Frame(world, figures, activeLayer, slice, movePerTick, tick: false);
                    if (figures.AmbientConversationCount > 0) struckAfter = f / 60f;
                }
                Debug.Log(struckAfter >= 0f
                    ? $"[Talk] control: two colonists standing together struck up a conversation after {struckAfter:0.0} s"
                    : "[Talk] control: two colonists standing together for 20 s never struck up a conversation");
                if (struckAfter < 0f) exitCode = 1;
                figures.EndConversations();

                int started = 0, previous = 0, most = 0;
                for (int f = 0; f < 60 * 90; f++)
                {
                    Frame(world, figures, activeLayer, slice, movePerTick, tick: true);
                    int now = figures.AmbientConversationCount;
                    if (now > previous) started += now - previous;
                    previous = now;
                    most = Math.Max(most, now);

                    // Every ten seconds, why or why not: each colonist's job, whether she is at
                    // leisure, and how near the nearest other colonist is standing.
                    if (f % 600 == 0)
                    {
                        var line = new System.Text.StringBuilder($"[Talk] t={f / 60}s:");
                        var views = world.Views.Current.Pawns;
                        for (int i = 0; i < views.Length; i++)
                        {
                            if (!figures.TryGetFeet(views[i].Id, out Vector3 at)) continue;
                            float nearest = float.MaxValue;
                            for (int j = 0; j < views.Length; j++)
                            {
                                if (j == i || !figures.TryGetFeet(views[j].Id, out Vector3 other)) continue;
                                nearest = Math.Min(nearest, Vector3.Distance(at, other));
                            }
                            line.Append($" {views[i].Id}(job {views[i].JobDef}, working {views[i].Working}, " +
                                        $"chat {FaceContext.CanChat(in views[i])}, nearest {nearest:0.0} m)");
                        }
                        Debug.Log(line.ToString());
                    }
                }

                Debug.Log($"[Talk] {shots} photographs of a raised hand; hands up in {framesWithHands} frames. " +
                          $"Left alone for 90 s, {figures.FigureCount} figures drawn, the colony struck up " +
                          $"{started} conversations, at most {most} at once.");
                if (shots == 0)
                {
                    Debug.LogError("[Talk] no speaker ever raised a hand: the talking hands are not reaching the figure.");
                    exitCode = 1;
                }
            }
            catch (Exception error)
            {
                Debug.LogError($"[Talk] {error}");
                exitCode = 1;
            }
            finally
            {
                PawnFigureDirector.AmbientConversations = ambientWas;
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                figures?.Dispose();
                renderer?.Dispose();
                library?.Dispose();
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        static void Frame(SimWorld world, PawnFigureDirector figures, int layer, SliceSettings slice, int movePerTick, bool tick)
        {
            if (tick) world.Tick();
            figures.Sync(world.Views.Current, layer, slice, 0f, movePerTick, FrameSeconds);
            figures.Evaluate(FrameSeconds);
        }

        static Vector3 Waist(PawnFigureDirector figures, PawnId id) =>
            (figures.TryGetFeet(id, out Vector3 feet) ? feet : Vector3.zero) + Vector3.up * 1.6f;
    }
}
