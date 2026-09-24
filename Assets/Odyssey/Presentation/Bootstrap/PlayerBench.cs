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
            }, null, (int)((LimitSeconds + 20f) * 1000f), System.Threading.Timeout.Infinite);
            _boot.StartCoroutine(Watchdog());
            return Guarded(Body());
        }

        IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(LimitSeconds);
            Quit($"[Bench] watchdog: {LimitSeconds} s reached before the bench finished; quitting");
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
