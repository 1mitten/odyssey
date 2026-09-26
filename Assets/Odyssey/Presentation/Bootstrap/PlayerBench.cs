#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Odyssey.Presentation.Rendering;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Odyssey.Presentation.Bootstrap
{
    /// <summary>
    /// A GPU benchmark run inside a development player (<c>docs/design/38-meadow-overhaul.md</c>
    /// §18e). Started by <c>-odyssey-bench</c> beside <c>-odyssey-newgame</c>:
    ///
    /// <code>Build/Win64/Odyssey.exe -odyssey-newgame -odyssey-bench -screen-fullscreen 0
    ///     -screen-width 3840 -screen-height 2160 -logFile Logs/bench.log</code>
    ///
    /// <para><b>Why a player.</b> Every GPU figure the look work had was a frame-time stand-in:
    /// <c>FrameTimingManager</c> reads nothing in a batch editor on Direct3D 11, and a frame at 4K
    /// is only the GPU when the CPU is idle enough to be hidden. A development player reports the
    /// GPU's own time, so the arms here rank the remaining work by what it really costs.</para>
    ///
    /// <para>One world, one run, each arm settled (the board re-meshed if the arm changed what is
    /// meshed) and then timed for a few seconds: GPU, CPU submission, frame and draw calls, plus
    /// the GPU time of every URP render pass the profiler exposes a GPU recorder for. The table is
    /// written to the log with a <c>[Bench]</c> prefix, and the player quits. Nothing is saved.</para>
    /// </summary>
    public sealed class PlayerBench
    {
        public const string Argument = "-odyssey-bench";

        /// <summary>With <see cref="Argument"/>: the grass-at-distance arms of design 38 §21 instead
        /// of the look's (Full grass, the wide and farthest framings).</summary>
        public const string GrassArgument = "-odyssey-bench-grass";

        /// <summary>With <see cref="Argument"/>: the scenery drawn from GPU buffers against the chunk
        /// path (design 38 §22), Full grass, at the start, 70 m and 140 m.</summary>
        public const string SceneryArgument = "-odyssey-bench-scenery";

        /// <summary>With <see cref="Argument"/>: the trees grouped and simpler far away against the
        /// chunk path (design 38 §23), at the start, 70 m and 140 m.</summary>
        public const string TreesArgument = "-odyssey-bench-trees";

        /// <summary>With <see cref="Argument"/>: the shoreline against the square shore (design 38
        /// §24), framed on the water nearest the colony at 32, 70 and 140 m.</summary>
        public const string ShoreArgument = "-odyssey-bench-shore";

        /// <summary>With <see cref="Argument"/>: the clouds (design 63 §8) — the colony camera, where
        /// they must submit nothing, and an eye on the board looking at the horizon through the ride
        /// camera's lens, on, off and on again, with a picture of the eye's view beside the log.</summary>
        public const string CloudsArgument = "-odyssey-bench-clouds";

        /// <summary>
        /// With <see cref="Argument"/>: the first-use hitch tour of design 38 §25 instead of any
        /// timing arms. The world runs; the tour does, a few seconds apart, the first of each thing
        /// a new colony meets — a wall, a zone, a felled tree, the Work tab, night, a supply drop —
        /// and logs every slow frame beside the step it fell in and the shader variants the driver
        /// was handed meanwhile (<c>GraphicsSettings.logWhenShaderIsCompiled</c>). It is the
        /// measurement d-16 asks for before any warm-up is built.
        /// </summary>
        public const string HitchArgument = "-odyssey-bench-hitch";

        /// <summary>The hitch tour, requested. Asked before the session is built, so the shader
        /// log covers the load as well as the tour.</summary>
        public static bool HitchRequested() => Requested() && Has(HitchArgument);

        /// <summary>The tour's own limit: longer than the arms', and still a hard stop. The tour is
        /// about 150 s, but the limit counts from the player's start, and one run on a busy machine
        /// took 81 s to reach its thirtieth frame and was cut off at 240.</summary>
        public const float HitchLimitSeconds = 360f;

        static float Limit => Has(HitchArgument) ? HitchLimitSeconds : LimitSeconds;

        static bool Has(string wanted)
        {
            foreach (string argument in Environment.GetCommandLineArgs())
                if (argument == wanted) return true;
            return false;
        }
        // About 110 s for the whole run, well inside LimitSeconds.
        const float SettleSeconds = 1.5f;
        const float MeasureSeconds = 4f;

        /// <summary>
        /// The most the bench may run, whatever happens. **It takes the owner's screen**: the first
        /// run died on an exception about forty seconds in, the coroutine stopped, the player never
        /// quit, and it sat fullscreen at 4K over the owner's work for six minutes. So the run is
        /// guarded three ways: every step of it runs under a try/catch that logs and quits
        /// (<see cref="Guarded"/>); a watchdog quits at this limit; and a thread timer kills the
        /// process a little after it, in case <c>Application.Quit</c> itself does not take.
        /// </summary>
        public const float LimitSeconds = 180f;

        public static bool Requested()
        {
            foreach (string argument in Environment.GetCommandLineArgs())
                if (argument == Argument) return true;
            return false;
        }

        readonly OdysseyBootstrap _boot;
        readonly FrameTiming[] _timing = new FrameTiming[1];
        readonly List<(string Name, ProfilerRecorder Recorder)> _passes = new List<(string, ProfilerRecorder)>();
        readonly StringBuilder _table = new StringBuilder();

        public PlayerBench(OdysseyBootstrap boot) => _boot = boot;

        System.Threading.Timer? _killer;
        bool _quitting;

        /// <summary>The bench, guarded, with its watchdogs started. Start this, not <see cref="Body"/>.</summary>
        public IEnumerator Run()
        {
            _killer = new System.Threading.Timer(_ =>
            {
                try { Debug.LogError("[Bench] hard limit reached; killing the player"); } catch { }
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }, null, (int)((Limit + 20f) * 1000f), System.Threading.Timeout.Infinite);
            _boot.StartCoroutine(Watchdog());
            return Guarded(Body());
        }

        IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(Limit);
            Quit($"[Bench] watchdog: {Limit} s reached before the bench finished; quitting");
        }

        void Quit(string why)
        {
            if (_quitting) return;
            _quitting = true;
            Debug.Log(why);
            foreach (var pass in _passes) { try { pass.Recorder.Dispose(); } catch { } }
            Application.Quit();
        }

        /// <summary>
        /// Runs a coroutine one step at a time with each step under a try/catch, following nested
        /// enumerators itself so that an exception anywhere in the bench is caught here — logged,
        /// and the player quits — rather than silently ending the coroutine and leaving the player
        /// running.
        /// </summary>
        IEnumerator Guarded(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0 && !_quitting)
            {
                bool moved;
                object? current = null;
                try
                {
                    moved = stack.Peek().MoveNext();
                    if (moved) current = stack.Peek().Current;
                }
                catch (Exception e)
                {
                    Debug.LogError("[Bench] failed: " + e);
                    Quit("[Bench] quitting after a failure");
                    yield break;
                }
                if (!moved) { stack.Pop(); continue; }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
        }

        IEnumerator Body()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            if (Has(HitchArgument))
            {
                yield return HitchTour();
                Quit("[Hitch] done");
                yield break;
            }
            // Wait for the session and for the board to mesh out.
            while (_boot.Renderer == null || _boot.Model == null) yield return null;
            yield return Still();
            yield return Settle();

            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Log($"[Bench] header: {SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsDeviceType}, " +
                $"{Screen.width}x{Screen.height}, render scale {urp?.renderScale ?? -1f}, MSAA {urp?.msaaSampleCount ?? -1}, " +
                $"shadow distance {QualitySettings.shadowDistance} m, cascades {urp?.shadowCascadeCount ?? -1}, " +
                $"grass {_boot.Renderer!.ScatterDensity}, board {_boot.sizeX}x{_boot.sizeZ}x{_boot.layers}, " +
                $"gpu recorder {SystemInfo.supportsGpuRecorder}, development {Debug.isDebugBuild}");

            StartPassRecorders();
            _table.AppendLine("| arm | GPU ms | CPU submit ms | frame ms | draw calls |" + PassHeader());
            _table.AppendLine("|---|---|---|---|---|" + PassRule());

            ChunkRenderer renderer = _boot.Renderer!;
            if (Has(CloudsArgument))
            {
                yield return CloudArms();
                Log("[Bench] table:\n" + _table);
                Quit("[Bench] done");
                yield break;
            }
            if (Has(ShoreArgument))
            {
                yield return ShoreArms();
                Log("[Bench] table:\n" + _table);
                Quit("[Bench] done");
                yield break;
            }
            if (Has(TreesArgument))
            {
                yield return TreeArms(renderer);
                Log("[Bench] table:\n" + _table);
                Quit("[Bench] done");
                yield break;
            }
            if (Has(SceneryArgument))
            {
                yield return SceneryArms(renderer);
                Log("[Bench] table:\n" + _table);
                Quit("[Bench] done");
                yield break;
            }
            if (Has(GrassArgument))
            {
                yield return GrassArms(renderer);
                Log("[Bench] table:\n" + _table);
                Quit("[Bench] done");
                yield break;
            }
            yield return Arm("look as shipped", null, null);

            // Design 38 §18c and §18f, before and after in this run: each change taken back alone,
            // then all of them together.
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            int cascades = pipeline != null ? pipeline.shadowCascadeCount : 0;
            yield return Arm("trees cast from the level before the card (the old proxy)",
                () => renderer.TreeShadowFromSimplest = false, () => renderer.TreeShadowFromSimplest = true);
            yield return Arm("four shadow cascades",
                () => { if (pipeline != null) pipeline.shadowCascadeCount = 4; },
                () => { if (pipeline != null) pipeline.shadowCascadeCount = cascades; });
            yield return Arm("before 18c and 18f (finest dressing, old proxy, four cascades)",
                () =>
                {
                    renderer.DressingLevels = false; renderer.BushLodBias = 1e6f; renderer.TreeShadowFromSimplest = false;
                    if (pipeline != null) pipeline.shadowCascadeCount = 4;
                },
                () =>
                {
                    renderer.DressingLevels = true; renderer.BushLodBias = 1.5f; renderer.TreeShadowFromSimplest = true;
                    if (pipeline != null) pipeline.shadowCascadeCount = cascades;
                });
            yield return Arm("no shadow casters", () => renderer.CastShadows = false, () => renderer.CastShadows = true);
            yield return Arm("no dressing", () => { renderer.Dressing = false; _boot.Model!.Remesh(); },
                () => { renderer.Dressing = true; _boot.Model!.Remesh(); });
            yield return Arm("no tufts", () => { renderer.Tufts = false; _boot.Model!.Remesh(); },
                () => { renderer.Tufts = true; _boot.Model!.Remesh(); });
            yield return Arm("no dressing or tufts",
                () => { renderer.Dressing = false; renderer.Tufts = false; _boot.Model!.Remesh(); },
                () => { renderer.Dressing = true; renderer.Tufts = true; _boot.Model!.Remesh(); });
            // Before and after §18c in the same run: every level the finest, then the dressing at
            // its coarsest, against the look as shipped (which carries §18c's tuned biases).
            bool levelsWas = renderer.DressingLevels;
            float dressingWas = renderer.DressingLodBias, bushWas = renderer.BushLodBias;
            yield return Arm("dressing at its finest level (no dressing levels)",
                () => { renderer.DressingLevels = false; renderer.BushLodBias = 1e6f; },
                () => { renderer.DressingLevels = levelsWas; renderer.BushLodBias = bushWas; });
            yield return Arm("dressing and tufts at their coarsest level",
                () => { renderer.UseLods = true; renderer.LodBias = 1e-4f; renderer.DressingLevels = true;
                        renderer.DressingLodBias = 1e-4f; renderer.BushLodBias = 1e-4f; },
                () => { renderer.UseLods = false; renderer.LodBias = 1f; renderer.DressingLevels = levelsWas;
                        renderer.DressingLodBias = dressingWas; renderer.BushLodBias = bushWas; });

            var volumes = new List<Volume>(UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None));
            yield return Arm("golden grade off", () => { foreach (Volume v in volumes) v.enabled = false; },
                () => { foreach (Volume v in volumes) v.enabled = true; });

            yield return Arm("look again (drift control)", null, null);

            // The ground is chosen when a session's library resolves it, so these two are new
            // sessions of the same seed, and they are compared with each other.
            MeadowLook.GroundEnabled = false;
            _boot.TeardownSession();
            yield return null;
            _boot.BuildSession();
            while (_boot.Renderer == null || _boot.Model == null) yield return null;
            yield return Still();
            yield return Settle();
            yield return Arm("stock ground (new session)", null, null);
            MeadowLook.GroundEnabled = true;
            _boot.TeardownSession();
            yield return null;
            _boot.BuildSession();
            while (_boot.Renderer == null || _boot.Model == null) yield return null;
            yield return Still();
            yield return Settle();
            yield return Arm("look (new session)", null, null);

            Log("[Bench] table:\n" + _table);
            Quit("[Bench] done");
        }

        /// <summary>
        /// Full grass at the wide and farthest framings (design 38 §21): as shipped, then each of
        /// the distance techniques taken back, then the tufts, the dressing and all grass taken away,
        /// so the GPU's own time says where Full's cost at distance is and what §21 bought.
        /// </summary>
        IEnumerator GrassArms(ChunkRenderer renderer)
        {
            renderer.ScatterDensity = GroundScatter.MaxPerCell * 100;
            _boot.Model!.Remesh();
            yield return Settle();
            // A span cannot live in an iterator, so the focus cell is read out first.
            Odyssey.Sim.Contracts.CellRef? focus = FirstPawnCell();
            foreach (float framing in new[] { 70f, 140f })
            {
                if (focus.HasValue && _boot.cameraRig != null) _boot.cameraRig.FocusOn(focus.Value, framing);
                yield return Settle();
                string at = $"@{framing:0} m";
                yield return Arm($"full {at}, as shipped", null, null);
                yield return Arm($"full {at}, no thinning", () => renderer.ThinGrass = false, () => renderer.ThinGrass = true);
                yield return Arm($"full {at}, no thinning or tuft levels",
                    () => { renderer.ThinGrass = false; renderer.TuftLevels = false; },
                    () => { renderer.ThinGrass = true; renderer.TuftLevels = true; });
                yield return Arm($"full {at}, no tufts", () => { renderer.Tufts = false; _boot.Model!.Remesh(); },
                    () => { renderer.Tufts = true; _boot.Model!.Remesh(); });
                yield return Arm($"full {at}, no dressing", () => { renderer.Dressing = false; _boot.Model!.Remesh(); },
                    () => { renderer.Dressing = true; _boot.Model!.Remesh(); });
                yield return Arm($"{at}, no grass", () => { renderer.ScatterDensity = 0; _boot.Model!.Remesh(); },
                    () => { renderer.ScatterDensity = GroundScatter.MaxPerCell * 100; _boot.Model!.Remesh(); });
            }
        }

        /// <summary>
        /// The scenery drawn from GPU buffers against the chunk path (design 38 §22), Full grass, at
        /// the start, 70 m and 140 m — where the owner saw the drop. Each pair is taken back to back,
        /// and each row says how many of its calls were indirect, so the table itself shows the path
        /// really ran on this machine's API.
        /// </summary>
        IEnumerator SceneryArms(ChunkRenderer renderer)
        {
            renderer.ScatterDensity = GroundScatter.MaxPerCell * 100;
            _boot.Model!.Remesh();
            yield return Settle();
            Odyssey.Sim.Contracts.CellRef? focus = FirstPawnCell();
            foreach (float framing in new[] { 32f, 70f, 140f })
            {
                if (focus.HasValue && _boot.cameraRig != null) _boot.cameraRig.FocusOn(focus.Value, framing);
                yield return Settle();
                string at = $"@{framing:0} m";
                yield return Arm($"full {at}, scenery from GPU buffers", () => renderer.UseIndirectScenery = true, null);
                Log($"[Bench] {at}: {renderer.IndirectDrawCalls} indirect calls, {renderer.IndirectInstances} instances in the buffers");
                yield return Arm($"full {at}, chunk by chunk", () => renderer.UseIndirectScenery = false,
                    () => renderer.UseIndirectScenery = true);
            }
        }

        /// <summary>
        /// The trees grouped and simpler far away against the chunk path (design 38 §23), at the
        /// start, 70 m and 140 m: as shipped, both off, grouping alone, and no trees at all, so the
        /// table carries what the trees cost as well as what the two changes recovered of it.
        /// </summary>
        IEnumerator TreeArms(ChunkRenderer renderer)
        {
            Odyssey.Sim.Contracts.CellRef? focus = FirstPawnCell();
            foreach (float framing in new[] { 32f, 70f, 140f })
            {
                if (focus.HasValue && _boot.cameraRig != null) _boot.cameraRig.FocusOn(focus.Value, framing);
                yield return Settle();
                string at = $"@{framing:0} m";
                yield return Arm($"trees {at}, as shipped (grouped, simpler far)", null, null);
                yield return Arm($"trees {at}, chunk by chunk, all fine",
                    () => { renderer.GroupTrees = false; renderer.SimplerFarTrees = false; },
                    () => { renderer.GroupTrees = true; renderer.SimplerFarTrees = true; });
                yield return Arm($"trees {at}, grouped, all fine", () => renderer.SimplerFarTrees = false,
                    () => renderer.SimplerFarTrees = true);
                yield return Arm($"{at}, no trees", () => renderer.DrawTrees = false, () => renderer.DrawTrees = true);
            }
        }

        /// <summary>
        /// The shoreline against the square shore (design 38 §24), framed on the water nearest the
        /// colony. The square arm re-meshes the board with <see cref="WaterShore.Enabled"/> off and
        /// back; marsh keeps the painted material it resolved with, so the arm is the geometry, the
        /// water over the banks and the field-driven shading, not the marsh tiles.
        /// </summary>
        IEnumerator ShoreArms()
        {
            Odyssey.Sim.Contracts.CellRef? focus = NearestWater() ?? FirstPawnCell();
            Log($"[Bench] shore framed on {focus?.ToString() ?? "nothing"}");
            foreach (float framing in new[] { 32f, 70f, 140f })
            {
                if (focus.HasValue && _boot.cameraRig != null) _boot.cameraRig.FocusOn(focus.Value, framing);
                yield return Settle();
                string at = $"@{framing:0} m";
                yield return Arm($"shore {at}, as shipped (shoreline)", null, null);
                yield return Arm($"shore {at}, square shore",
                    () => { WaterShore.Enabled = false; _boot.Model!.Remesh(); },
                    () => { WaterShore.Enabled = true; _boot.Model!.Remesh(); });
            }
        }

        /// <summary>
        /// The clouds (design 63 §8). The rig is stopped for the eye so it cannot put the camera
        /// back, and handed back after; the eye is two metres over the board's top at its middle.
        /// </summary>
        IEnumerator CloudArms()
        {
            Odyssey.Presentation.World.CloudDirector? clouds = _boot.Clouds;
            Log($"[Bench] clouds: {(clouds != null && clouds.Available ? "the art resolved" : "NO CLOUD ART in this player")}");
            if (clouds == null || !clouds.Available || _boot.cameraRig == null) yield break;

            yield return Arm("clouds on, colony camera", null, null);
            Log($"[Bench] clouds, colony camera: {clouds.LastDrawCalls} calls, seen {clouds.LastVisible}");
            yield return Arm("clouds off, colony camera", () => clouds.Enabled = false, () => clouds.Enabled = true);

            Camera camera = _boot.cameraRig.Camera;
            float fieldOfView = camera.fieldOfView;
            _boot.cameraRig.enabled = false;
            camera.fieldOfView = Odyssey.Hud.RideCamera.FieldOfView;
            camera.transform.SetPositionAndRotation(
                new Vector3(_boot.sizeX * CellMetrics.SizeXZ * 0.5f, _boot.layers * CellMetrics.SizeY + 2f,
                    _boot.sizeZ * CellMetrics.SizeXZ * 0.5f),
                Quaternion.Euler(4f, 30f, 0f));

            yield return Arm("clouds on, eye at the horizon", null, null);
            Log($"[Bench] clouds, eye: {clouds.LastDrawCalls} calls, seen {clouds.LastVisible}");
            // Beside the -logFile, absolute, for CaptureMidWake's reason.
            string? logDir = string.IsNullOrEmpty(Application.consoleLogPath)
                ? null : System.IO.Path.GetDirectoryName(Application.consoleLogPath);
            string path = System.IO.Path.Combine(
                string.IsNullOrEmpty(logDir) ? Application.persistentDataPath : logDir!, "clouds-eye.png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            Log($"[Bench] clouds: captured {path}");
            yield return Arm("clouds off, eye at the horizon", () => clouds.Enabled = false, () => clouds.Enabled = true);
            yield return Arm("clouds on, eye at the horizon (drift control)", null, null);

            camera.fieldOfView = fieldOfView;
            _boot.cameraRig.enabled = true;
        }

        // ------------------------------------------------------------------ the hitch tour

        /// <summary>A frame this long is a hitch a player notices (two frames at 60 Hz).</summary>
        const float HitchMs = 33f;

        /// <summary>A frame this long is a stall.</summary>
        const float StallMs = 50f;

        string _step = "load";
        Odyssey.Presentation.Ui.HudShell? _shell;
        bool _behindCurtain;
        int _compiles;
        readonly StringBuilder _hitchTable = new StringBuilder();

        void CountCompile(string message, string stack, LogType type)
        {
            if (message == null) return;
            if (message.IndexOf("shader", StringComparison.OrdinalIgnoreCase) < 0) return;
            if (message.IndexOf("ompiled", StringComparison.Ordinal) < 0
                && message.IndexOf("Uploaded", StringComparison.Ordinal) < 0
                && message.IndexOf("variant", StringComparison.OrdinalIgnoreCase) < 0) return;
            System.Threading.Interlocked.Increment(ref _compiles);
        }

        /// <summary>
        /// The first of each thing a new colony meets, a few seconds apart, with every slow frame
        /// logged beside the step it fell in (design 38 §25, d-16 step 1). Each step's own action is
        /// guarded so one that cannot apply (no tree in reach, a panel that will not open) is logged
        /// and the tour goes on; the run as a whole stays under <see cref="Guarded"/> and the
        /// watchdog. The settings it touches are put back, so the owner's saved preferences are as
        /// they were.
        /// </summary>
        IEnumerator HitchTour()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            GraphicsSettings.logWhenShaderIsCompiled = true;
            Application.logMessageReceivedThreaded += CountCompile;
            _hitchTable.AppendLine("| step | frames | worst ms | over 33 ms | over 50 ms | shader variants |");
            _hitchTable.AppendLine("|---|---|---|---|---|---|");

            // Started without -odyssey-newgame, the tour begins where a player does: on the title
            // screen, the process already warm from drawing it. New game is then pressed the way
            // the menu presses it, so the step's slow frame is the wait a player sits through and
            // the frames after it are the first the new world draws.
            if (_boot.Renderer == null)
            {
                for (int i = 0; i < 30; i++) yield return null;
                yield return Step("the title screen (the control)", null, 3f);
                _shell ??= UnityEngine.Object.FindFirstObjectByType<Odyssey.Presentation.Ui.HudShell>();
                if (_shell != null)
                {
                    // Through the wake (design 56), as the menu does it: the fade, the build behind
                    // the black and five seconds of dream, so the step is the passage a player sits
                    // through — and a picture from the middle of it, which is the one proof that
                    // the blur survived a player build's shader stripping.
                    Odyssey.Presentation.Ui.HudShell shell = _shell;
                    _boot.StartCoroutine(CaptureMidWake(shell));
                    yield return Step("New game pressed: the fade, the build behind the black, and the wake",
                        () => shell.EnterWorld(() => _boot.BuildSession()), 7f);
                }
                else
                {
                    yield return Step("New game pressed: the session built, and its first frames",
                        () => _boot.BuildSession(), 3f);
                }
            }
            while (_boot.Renderer == null || _boot.Model == null) yield return null;

            // What a player sees the moment the load lifts, before anything has settled.
            yield return Step("the first 3 s after the load", null, 3f);
            yield return Settle();

            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Log($"[Hitch] header: {SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsDeviceType}, " +
                $"{Screen.width}x{Screen.height}, fullscreen {Screen.fullScreen}, render scale {urp?.renderScale ?? -1f}, " +
                $"board {_boot.sizeX}x{_boot.sizeZ}x{_boot.layers}, development {Debug.isDebugBuild}");

            var world = _boot.World!;
            Odyssey.Sim.Contracts.CellRef home = FirstPawnCell() ?? default;
            var size = world.Size;
            int y = home.Y;
            Odyssey.Sim.Contracts.CellRef At(int dx, int dz) =>
                new Odyssey.Sim.Contracts.CellRef(Math.Clamp(home.X + dx, 1, size.SizeX - 2),
                    Math.Clamp(home.Z + dz, 1, size.SizeZ - 2), y);
            void Submit(Odyssey.Sim.Contracts.IntentKind kind, Odyssey.Sim.Contracts.CellRef cell, int a = 0, int b = 0, int c = 0) =>
                world.Intents.Submit(new Odyssey.Sim.Contracts.Intent(kind, cell, a, b, c));

            // The world runs, and quickly, so the orders below are carried out inside the tour.
            for (int i = 0; i < 60 && world.GameSpeed != 3; i++)
            {
                Submit(Odyssey.Sim.Contracts.IntentKind.SetGameSpeed, default, 3);
                yield return null;
            }
            yield return Step("steady play, nothing new (the control)", null, 5f);

            yield return Step("build orders: a wall, a floor, a door", () =>
            {
                for (int i = 0; i < 5; i++)
                    Submit(Odyssey.Sim.Contracts.IntentKind.PlaceBuilding, At(5 + i, 5),
                        Odyssey.Sim.Contracts.BuildingHandle.Wall, Odyssey.Sim.Contracts.StuffHandle.Wood);
                Submit(Odyssey.Sim.Contracts.IntentKind.PlaceBuilding, At(10, 5),
                    Odyssey.Sim.Contracts.BuildingHandle.Door, Odyssey.Sim.Contracts.StuffHandle.Wood);
                for (int dz = 0; dz < 3; dz++)
                for (int dx = 0; dx < 3; dx++)
                    Submit(Odyssey.Sim.Contracts.IntentKind.PlaceBuilding, At(-8 + dx, 5 + dz),
                        Odyssey.Sim.Contracts.BuildingHandle.Floor, Odyssey.Sim.Contracts.StuffHandle.Wood);
            }, 12f);

            yield return Step("zones: a stockpile and a growing field", () =>
            {
                int anchor = size.Index(At(5, -8));
                for (int dz = 0; dz < 3; dz++)
                for (int dx = 0; dx < 3; dx++)
                {
                    Submit(Odyssey.Sim.Contracts.IntentKind.DesignateStorage, At(5 + dx, -8 + dz),
                        anchor, (int)Odyssey.Sim.Storage.StoragePreset.Everything);
                    Submit(Odyssey.Sim.Contracts.IntentKind.DesignateZone, At(-8 + dx, -8 + dz),
                        Odyssey.Sim.Contracts.PlantHandle.Carrot + 1);
                }
            }, 6f);

            yield return Step("an item dropped", () =>
                Submit(Odyssey.Sim.Contracts.IntentKind.GiveResource, At(2, 2), Odyssey.Sim.Pawns.ItemIndex.Wood, 30), 6f);

            yield return Step("trees marked for felling (and felled)", () =>
            {
                for (int dz = -6; dz <= 6; dz++)
                for (int dx = -6; dx <= 6; dx++)
                    Submit(Odyssey.Sim.Contracts.IntentKind.Designate, At(dx, dz),
                        (int)Odyssey.Sim.Designations.DesignationKind.Fell);
            }, 15f);

            var directors = _boot.Directors;
            yield return Step("the Work tab", () => directors?.Work.Toggle(), 3f);
            yield return Step("the Research tab", () => { directors?.Work.Toggle(); directors?.Research.Toggle(); }, 3f);
            yield return Step("the Inventory tab", () => { directors?.Research.Toggle(); directors?.Inventory.Toggle(); }, 3f);
            yield return Step("Settings, the Graphics tab", () =>
            {
                directors?.Inventory.Toggle();
                directors?.Settings.SetOpen(true);
                directors?.Settings.SetTab(Odyssey.Hud.SettingsTab.Graphics);
            }, 3f);

            int density = directors?.Settings.Value(Odyssey.Hud.GraphicsLadder.VegetationDensity) ?? 0;
            yield return Step("a setting changed: shadows off", () =>
                directors?.Settings.Toggle(Odyssey.Hud.GraphicsOption.Shadows), 4f);
            yield return Step("a setting changed: shadows back on", () =>
                directors?.Settings.Toggle(Odyssey.Hud.GraphicsOption.Shadows), 4f);
            yield return Step("a setting changed: grass to Full", () =>
                directors?.Settings.SetValue(Odyssey.Hud.GraphicsLadder.VegetationDensity, 300), 5f);
            yield return Step("a setting changed: grass back", () =>
            {
                directors?.Settings.SetValue(Odyssey.Hud.GraphicsLadder.VegetationDensity, density);
                directors?.Settings.SetOpen(false);
            }, 5f);

            yield return Step("a supply drop falls", () =>
                Submit(Odyssey.Sim.Contracts.IntentKind.InvokeIncident, default,
                    Odyssey.Sim.Contracts.IncidentHandle.SupplyDrop), 9f);

            yield return Step("the see-through fade", () =>
            {
                if (_boot.Renderer != null) _boot.Renderer.FadeEveryTreeForAPhotograph = true;
            }, 3f);
            yield return Step("the fade taken off", () =>
            {
                if (_boot.Renderer != null) _boot.Renderer.FadeEveryTreeForAPhotograph = false;
            }, 2f);

            yield return Step("dusk (19.5 h)", () => _boot.DaylightHourOverride = 19.5f, 4f);
            yield return Step("night (23 h)", () => _boot.DaylightHourOverride = 23f, 4f);
            yield return Step("day again", () => _boot.DaylightHourOverride = null, 3f);

            Odyssey.Sim.Contracts.CellRef? water = NearestWater();
            yield return Step("water and a fall brought into view", () =>
            {
                if (water.HasValue && _boot.cameraRig != null) _boot.cameraRig.FocusOn(water.Value, 30f);
            }, 5f);
            yield return Step("pulled back to 140 m", () =>
            {
                if (_boot.cameraRig != null) _boot.cameraRig.FocusOn(home, 140f);
            }, 6f);
            yield return Step("back to the colony", () =>
            {
                if (_boot.cameraRig != null) _boot.cameraRig.FocusOn(home, 32f);
            }, 4f);
            yield return Step("steady play again (the control)", null, 5f);

            Application.logMessageReceivedThreaded -= CountCompile;
            Log("[Hitch] table:\n" + _hitchTable);
        }

        /// <summary>
        /// A second into the dream, a screenshot beside the log and a line saying whether the blur
        /// was drawn. A player build strips any shader nothing names, and the wake's blur is found
        /// by name; this is where a stripped one would show as a sharp picture and "available False".
        /// </summary>
        IEnumerator CaptureMidWake(Odyssey.Presentation.Ui.HudShell shell)
        {
            float giveUp = Time.realtimeSinceStartup + 15f;
            while (shell.Wake.Phase != Odyssey.Hud.WakePhase.Waking && Time.realtimeSinceStartup < giveUp)
                yield return null;
            if (shell.Wake.Phase != Odyssey.Hud.WakePhase.Waking)
            {
                Log("[Hitch] wake: never reached the dream (setting off, or skipped)");
                yield break;
            }
            float at = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < at) yield return null;
            // Beside the -logFile, absolute. A relative path is not resolved against the working
            // directory in a player, and a capture that cannot be written fails without a word:
            // "Logs/wake-mid.png" was logged as captured and never existed (2026-09-26).
            string? logDir = string.IsNullOrEmpty(Application.consoleLogPath)
                ? null : System.IO.Path.GetDirectoryName(Application.consoleLogPath);
            string path = System.IO.Path.Combine(
                string.IsNullOrEmpty(logDir) ? Application.persistentDataPath : logDir!, "wake-mid.png");
            ScreenCapture.CaptureScreenshot(path);
            Odyssey.Presentation.Rendering.WakeBlur? blur = shell.WakeBlurNow;
            Log($"[Hitch] wake: captured {path} at blur {shell.Wake.Look.Blur:0.00}, haze {shell.Wake.Look.Haze:0.00}; " +
                $"blur shader available {blur?.Available ?? false}, pass enqueued {blur?.Enqueued ?? 0} times");
        }

        /// <summary>
        /// One step: apply it (guarded — a step that cannot apply is logged and the tour goes on),
        /// then watch the frames for <paramref name="seconds"/>, logging every one over
        /// <see cref="HitchMs"/> with the step it fell in.
        /// </summary>
        IEnumerator Step(string name, Action? apply, float seconds)
        {
            _step = name;
            System.Threading.Interlocked.Exchange(ref _compiles, 0);
            Log($"[Hitch] step: {name} (frame {Time.frameCount}, t {Time.realtimeSinceStartup:0.0} s)");
            try { apply?.Invoke(); }
            catch (Exception e) { Log($"[Hitch] step '{name}' could not apply: {e.GetType().Name}: {e.Message}"); }
            // The frame the step applied in is drawn after this, so its curtain state is the one now.
            _shell ??= UnityEngine.Object.FindFirstObjectByType<Odyssey.Presentation.Ui.HudShell>();
            _behindCurtain = _shell != null && _shell.CurtainUp;

            int frames = 0, over33 = 0, over50 = 0;
            float worst = 0f;
            var first = new StringBuilder();
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                frames++;
                float ms = Time.unscaledDeltaTime * 1000f;
                if (ms > worst) worst = ms;
                if (frames <= 8)
                {
                    // A frame drawn behind the start screen is marked: the player never sees it.
                    _shell ??= UnityEngine.Object.FindFirstObjectByType<Odyssey.Presentation.Ui.HudShell>();
                    first.Append(ms.ToString("0.0")).Append(_behindCurtain ? "* " : " ");
                }
                _behindCurtain = _shell != null && _shell.CurtainUp;
                if (ms > HitchMs)
                {
                    over33++;
                    if (ms > StallMs) over50++;
                    Log($"[Hitch] slow frame {Time.frameCount}: {ms:0.0} ms in '{name}' " +
                        $"(submit {_boot.SubmitMs:0.00} ms, tick {_boot.TickMs:0.00} ms, " +
                        $"meshed {_boot.Renderer?.ChunksMeshedThisFrame ?? 0} chunks, " +
                        $"indirect regather {_boot.Renderer?.IndirectRegatherMs ?? 0:0.00} ms, gc {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}, " +
                        $"shader variants so far this step {_compiles}; split {SectionSplit()})");
                }
            }
            _hitchTable.AppendLine($"| {name} | {frames} | {worst:0.0} | {over33} | {over50} | {_compiles} |");
            Log($"[Hitch] {name}: the first eight frames, ms (* drawn behind the start screen): {first}");
            Log($"[Hitch] {name}: worst {worst:0.0} ms over {frames} frames, {over33} over {HitchMs:0} ms, " +
                $"{over50} over {StallMs:0} ms, {_compiles} shader variants");
        }

        /// <summary>The last frame's submit, section by section, largest first. Not in an iterator,
        /// because the split is a span.</summary>
        string SectionSplit()
        {
            ReadOnlySpan<double> split = _boot.FrameSectionMs;
            var parts = new List<(double Ms, string Name)>();
            for (int i = 0; i < split.Length && i < OdysseyBootstrap.SectionNames.Length; i++)
                if (split[i] >= 0.5) parts.Add((split[i], OdysseyBootstrap.SectionNames[i]));
            parts.Sort((a, b) => b.Ms.CompareTo(a.Ms));
            var sb = new StringBuilder();
            foreach (var p in parts) sb.Append(p.Name).Append(' ').Append(p.Ms.ToString("0.0")).Append(", ");
            return sb.Length > 0 ? sb.ToString(0, sb.Length - 2) : "nothing over 0.5 ms";
        }

        Odyssey.Sim.Contracts.CellRef? NearestWater()
        {
            var model = _boot.Model;
            Odyssey.Sim.Contracts.CellRef? from = FirstPawnCell();
            if (model == null || !from.HasValue) return null;
            var size = model.Size;
            Odyssey.Sim.Contracts.CellRef? best = null;
            int bestDistance = int.MaxValue;
            for (int y = 0; y < size.SizeY; y++)
            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
            {
                if (!Odyssey.Sim.Worldgen.Natural.NaturalContent.IsWater(model.Terrain(size.Index(x, z, y)))) continue;
                int d = Math.Abs(x - from.Value.X) + Math.Abs(z - from.Value.Z);
                if (d < bestDistance) { bestDistance = d; best = new Odyssey.Sim.Contracts.CellRef(x, z, y); }
            }
            return best;
        }

        Odyssey.Sim.Contracts.CellRef? FirstPawnCell()
        {
            var pawns = _boot.World!.Views.Current.Pawns;
            return pawns.Length > 0 ? pawns[0].Cell : (Odyssey.Sim.Contracts.CellRef?)null;
        }

        /// <summary>
        /// The world paused and the day held at noon for the whole run. The second run without this
        /// drifted 6.6 → 9.3 ms between two identical "look" arms a minute apart, and its calls
        /// rose 1,169 → 1,366, because colonists went on felling and the sun went on moving under
        /// the arms: nothing after the first four arms could be compared (design 38 §18e).
        /// </summary>
        IEnumerator Still()
        {
            _boot.DaylightHourOverride = 12f;
            for (int i = 0; i < 120 && _boot.World != null && _boot.World.GameSpeed != 0; i++)
            {
                _boot.World.Intents.Submit(new Odyssey.Sim.Contracts.Intent(Odyssey.Sim.Contracts.IntentKind.SetGameSpeed, default, 0));
                yield return null;
            }
            Log($"[Bench] world paused: {(_boot.World != null && _boot.World.GameSpeed == 0)}, hour held at 12");
        }

        IEnumerator Settle()
        {
            float until = Time.realtimeSinceStartup + SettleSeconds;
            int quiet = 0;
            while (Time.realtimeSinceStartup < until || quiet < 30)
            {
                yield return null;
                ChunkRenderer? r = _boot.Renderer;
                quiet = r != null && r.ChunksMeshDeferred == 0 && r.ChunksMeshedThisFrame == 0 ? quiet + 1 : 0;
            }
        }

        IEnumerator Arm(string name, Action? apply, Action? undo)
        {
            apply?.Invoke();
            yield return Settle();

            double gpu = 0d, submit = 0d, frame = 0d, calls = 0d;
            int frames = 0, gpuFrames = 0;
            var passTotals = new double[_passes.Count];
            float until = Time.realtimeSinceStartup + MeasureSeconds;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                frames++;
                frame += Time.unscaledDeltaTime * 1000d;
                submit += _boot.SubmitMs;
                calls += _boot.Renderer?.DrawCalls ?? 0;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, _timing) > 0 && _timing[0].gpuFrameTime > 0d)
                {
                    gpu += _timing[0].gpuFrameTime;
                    gpuFrames++;
                }
                for (int p = 0; p < _passes.Count; p++)
                    passTotals[p] += _passes[p].Recorder.LastValueAsDouble * 1e-6; // ns -> ms
            }

            string gpuText = gpuFrames > 0 ? $"{gpu / gpuFrames:0.00}" : "n/a";
            var row = new StringBuilder($"| {name} | {gpuText} | {submit / frames:0.00} | {frame / frames:0.00} | {calls / frames:0} |");
            for (int p = 0; p < _passes.Count; p++) row.Append($" {passTotals[p] / frames:0.00} |");
            _table.AppendLine(row.ToString());
            Log($"[Bench] {name}: gpu {gpuText} ms, submit {submit / frames:0.00} ms, frame {frame / frames:0.00} ms, " +
                $"{calls / frames:0} calls over {frames} frames");

            // No settle here: the next arm settles after applying itself, which covers this undo.
            undo?.Invoke();
        }

        /// <summary>
        /// A GPU recorder for every render marker whose name looks like a URP pass worth ranking.
        /// The names are whatever this Unity's URP registers; only markers that exist are asked
        /// for, so a renamed pass is simply absent from the table rather than an error.
        /// </summary>
        void StartPassRecorders()
        {
            if (!SystemInfo.supportsGpuRecorder) return;
            string[] wanted =
            {
                "Shadow", "Opaque", "Transparent", "SSAO", "Ambient Occlusion", "DepthNormal", "Depth Prepass",
                "Copy Depth", "Skybox", "Post", "Uber", "Bloom", "Outline", "Final Blit", "Render Loop",
            };
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var seen = new HashSet<string>();
            foreach (ProfilerRecorderHandle handle in handles)
            {
                ProfilerRecorderDescription d = ProfilerRecorderHandle.GetDescription(handle);
                if (d.Category != ProfilerCategory.Render) continue;
                bool match = false;
                foreach (string w in wanted)
                    if (d.Name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) { match = true; break; }
                if (!match || !seen.Add(d.Name)) continue;
                // A marker's GPU time needs a recorder that sums the frame's samples; a marker that
                // will not give one drops its column, never the run.
                try
                {
                    ProfilerRecorder recorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, d.Name, 1,
                        ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.GpuRecorder);
                    if (recorder.Valid) _passes.Add((d.Name, recorder));
                    else recorder.Dispose();
                }
                catch (Exception e)
                {
                    Log($"[Bench] no GPU recorder for '{d.Name}': {e.GetType().Name}");
                }
                if (_passes.Count >= 24) break;
            }
            Log($"[Bench] GPU pass recorders: {_passes.Count} ({string.Join(", ", _passes.ConvertAll(p => p.Name))})");
        }

        string PassHeader()
        {
            var s = new StringBuilder();
            foreach (var pass in _passes) s.Append($" {pass.Name} |");
            return s.ToString();
        }

        string PassRule()
        {
            var s = new StringBuilder();
            foreach (var _ in _passes) s.Append("---|");
            return s.ToString();
        }

        static void Log(string line) => Debug.Log(line);
    }
}
