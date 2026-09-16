#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.CameraRig;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using Odyssey.Sim.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Renders frames on a real GPU and times them, one rendering decision at a time — and
    /// **its absolute numbers must not be believed.** Read this before reading its output.
    ///
    /// A <c>camera.Render()</c> loop has no frame boundary: nothing Presents, and the render
    /// pipeline is never told a frame ended, so every render carries the cost of every render
    /// before it. Measured: the same empty render cost 2.65 ms as the first row and 464 ms as
    /// the last, and a sync per frame did not help. Every row reads higher than the row above it
    /// whatever it draws. This tool is kept as the record of that failure and for questions that
    /// can be answered *within one row*; frame time is measured by <c>FrameTimeTests</c> under
    /// the real player loop, and nowhere else. See <c>docs/lessons.md</c>, "Benchmarking".
    ///
    /// **Why this is separate from <c>PlayScene.Measure</c>.** That one runs headless with no
    /// graphics device, so it can report draw calls and instance counts — which are exact — and
    /// deliberately reports no frame time at all, because a frame time without a GPU would be
    /// fiction. Every question about how much something costs to draw is a question about the
    /// GPU, so it needs a device, which means it belongs with the screenshot harness.
    ///
    /// **The rows are an experiment, not a list.** A *floor* row submits nothing, so what it
    /// costs is the harness. *Direct* rows draw a fixed set of instances straight through
    /// <c>Graphics.RenderMeshInstanced</c> with none of our code in the way, varying only the
    /// mesh or the material — so a slow row names its own cause. The *renderer* rows drive the
    /// real <see cref="ChunkRenderer"/>. Reading down the table answers, in order: is it the
    /// harness, is it the API, is it their shader, is it our code.
    ///
    /// **Counters are the editor's own.** <c>UnityEditor.UnityStats</c> is what the Game view's
    /// Stats box reads — GPU draw calls, batches, SetPass calls — collected without the profiler,
    /// which matters: the profiler's recorders were tried first and their mere presence made
    /// every row ten times slower, which is a measurement that measures itself. Our code counts
    /// what it *asked* for; these count what the GPU *got*, and the gap between them is where
    /// instancing silently failing would show.
    ///
    /// Run with <c>scripts/unity.sh shot Odyssey.EditorTools.RenderBench.Run</c>.
    /// </summary>
    public static class RenderBench
    {
        const int PlaySizeXZ = 120;
        const int PlayLayers = 16;
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const int WarmupFrames = 8;
        const int TimedFrames = 40;

        [MenuItem("Odyssey/Presentation/Benchmark the renderer")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        /// <summary>One row: either a direct submission of (mesh, material) or the real renderer.</summary>
        sealed class Row
        {
            public string Name = string.Empty;
            public Action Submit = () => { };
            public Func<string> Asked = () => string.Empty;
        }

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            OutlineFeature? outline = FindOutlineFeature();
            bool outlineWas = outline != null && outline.isActive;
            var owned = new List<UnityEngine.Object>();

            try
            {
                if (outline != null) outline.SetActive(false);

                var size = new GridSize(PlaySizeXZ, PlaySizeXZ, PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
                gen.MakeBarren();
                var grid = new CellGrid(size);
                var chunks = new ChunkGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);

                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(
                    "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
                using var library = new ModuleLibrary(catalogue);
                var model = new Odyssey.Presentation.World.WorldRenderModel(size, chunks, library);
                model.RefreshAll(grid, result.Natural!.Context.Edifices);

                var lighting = new GameObject("BenchLighting");
                owned.Add(lighting);
                PlayScene.BuildSheetLighting(lighting.transform);

                var cameraObject = new GameObject("BenchCamera");
                owned.Add(cameraObject);
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.enabled = false;

                var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                owned.Add(target);
                camera.targetTexture = target;

                int activeLayer = result.StartCell.Y;
                var focus = new Vector3(
                    result.StartCell.X * CellMetrics.SizeXZ,
                    activeLayer * CellMetrics.SizeY,
                    result.StartCell.Z * CellMetrics.SizeXZ);

                // ---- the materials and meshes under test --------------------------------------

                var plainLit = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    name = "Bench/PlainLit", enableInstancing = true,
                };
                owned.Add(plainLit);

                // Exactly what the renderer wears for ground: the terrain module's own part
                // material, which is the cached clone with the tint applied — not the pack asset.
                ResolvedModule ground = library[library.Resolve(
                    ModuleIds.Terrain("Grass"), ModuleShape.SolidBlock)];
                Material? groundMaterial = ground.IsEmpty ? null : ground.Parts[0].Material;
                Mesh groundMesh = ground.IsEmpty ? PrimitiveMeshes.UnitCube : ground.Parts[0].Mesh;

                ResolvedModule tuft = library[library.Resolve(ModuleIds.GrassTuftA, ModuleShape.Pillar)];
                Mesh? tuftMesh = tuft.IsEmpty ? null : tuft.Parts[0].Mesh;
                // Cloned with instancing on, as the renderer's material cache does. The raw pack
                // material has it off, and RenderMeshInstanced throws rather than falling back —
                // which drew nothing and clocked 0.25 ms, faster than the empty floor.
                Material? tuftMaterial = null;
                if (!tuft.IsEmpty)
                {
                    tuftMaterial = new Material(tuft.Parts[0].Material) { enableInstancing = true };
                    owned.Add(tuftMaterial);
                }

                Matrix4x4[] cubes = DirectMatrices(size, activeLayer - 1,
                    new Vector3(CellMetrics.SizeXZ, CellMetrics.SizeY, CellMetrics.SizeXZ), 0f);
                Matrix4x4[] tufts = DirectMatrices(size, activeLayer, Vector3.one, -CellMetrics.SizeY * 0.5f);

                var report = new StringBuilder();
                report.AppendLine($"[Bench] {PlaySizeXZ}x{PlaySizeXZ}x{PlayLayers} barren, 1920x1080, " +
                                  $"{TimedFrames} frames after {WarmupFrames} warm-up, " +
                                  $"{SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsDeviceType}.");
                report.AppendLine("[Bench] materials under test:");
                report.AppendLine("    " + DescribeMaterial(plainLit));
                if (groundMaterial != null) report.AppendLine("    " + DescribeMaterial(groundMaterial));
                if (tuftMaterial != null) report.AppendLine("    " + DescribeMaterial(tuftMaterial));

                // ---- the rows ------------------------------------------------------------------

                var rows = new List<Row>
                {
                    new Row { Name = "floor: nothing submitted" },
                    new Row
                    {
                        Name = "direct: 14,400 cubes, plain Lit",
                        Submit = () => Direct(cubes, PrimitiveMeshes.UnitCube, plainLit),
                        Asked = () => $"{Calls(cubes.Length)} calls, {cubes.Length} instances",
                    },
                };
                if (groundMaterial != null)
                    rows.Add(new Row
                    {
                        Name = "direct: 14,400 cubes, our ground material",
                        Submit = () => Direct(cubes, groundMesh, groundMaterial),
                        Asked = () => $"{Calls(cubes.Length)} calls, {cubes.Length} instances",
                    });
                if (tuftMesh != null && tuftMaterial != null)
                {
                    rows.Add(new Row
                    {
                        Name = "direct: 14,400 tufts, plain Lit",
                        Submit = () => Direct(tufts, tuftMesh, plainLit),
                        Asked = () => $"{Calls(tufts.Length)} calls, {tufts.Length} instances, {tuftMesh.triangles.Length / 3} tris each",
                    });
                    rows.Add(new Row
                    {
                        Name = "direct: 14,400 tufts, our grass material",
                        Submit = () => Direct(tufts, tuftMesh, tuftMaterial),
                        Asked = () => $"{Calls(tufts.Length)} calls, {tufts.Length} instances",
                    });
                }

                ChunkRenderer? renderer = null;
                var slice = new SliceSettings();
                // Bare ground first and last. If the two disagree, the table is measuring its own
                // history and every number between them is suspect.
                foreach (int density in new[] { 0, 60, 0 })
                {
                    int captured = density;
                    rows.Add(new Row
                    {
                        Name = captured == 0 ? "renderer: bare ground" : $"renderer: grass at {captured}/100",
                        Submit = () =>
                        {
                            if (renderer == null || renderer.ScatterDensity != captured)
                            {
                                renderer?.Dispose();
                                renderer = new ChunkRenderer(model) { ScatterDensity = captured };
                            }
                            renderer.Render(activeLayer, slice);
                        },
                        Asked = () => renderer == null
                            ? string.Empty
                            : $"{renderer.DrawCalls} calls, {renderer.InstancesDrawn} instances",
                    });
                }

                // An empty render again, at the very end. It is the same nothing as the first row;
                // if it costs a hundred times more here, the harness is measuring its own history
                // and no row in between is worth reading.
                rows.Add(new Row { Name = "floor again: nothing submitted, last" });

                report.AppendLine();
                report.AppendLine("  board camera (pitch 48, 48 m)");
                double previous = 0d;
                for (int i = 0; i < rows.Count; i++)
                {
                    double ms = TimeFrames(camera, rows[i].Submit, target, focus, 48f, 48f);
                    string delta = i == 0 ? string.Empty : $"({ms - previous:+0.00;-0.00;0.00})";
                    previous = ms;
                    report.AppendLine(
                        $"    {rows[i].Name,-44} {ms,8:0.00} ms {delta,-11} asked: {rows[i].Asked(),-40} " +
                        $"got: {EditorStats()}");
                }
                renderer?.Dispose();

                Debug.Log(report.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"[Bench] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (outline != null) outline.SetActive(outlineWas);
                foreach (UnityEngine.Object o in owned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        static int Calls(int instances) => (instances + ChunkRenderer.MaxInstancesPerCall - 1) / ChunkRenderer.MaxInstancesPerCall;

        /// <summary>One instance per column at the given layer, with a uniform scale and a lift.</summary>
        static Matrix4x4[] DirectMatrices(GridSize size, int layer, Vector3 scale, float lift)
        {
            var matrices = new Matrix4x4[size.SizeX * size.SizeZ];
            int n = 0;
            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
                matrices[n++] = Matrix4x4.TRS(
                    CellMetrics.Centre(x, z, layer) + Vector3.up * lift, Quaternion.identity, scale);
            return matrices;
        }

        /// <summary>The same submission the renderer makes, with none of the renderer.</summary>
        static void Direct(Matrix4x4[] matrices, Mesh mesh, Material material)
        {
            var rp = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 2000f),
            };
            for (int start = 0; start < matrices.Length; start += ChunkRenderer.MaxInstancesPerCall)
            {
                int n = Mathf.Min(ChunkRenderer.MaxInstancesPerCall, matrices.Length - start);
                Graphics.RenderMeshInstanced(rp, mesh, 0, matrices, n, start);
            }
        }

        /// <summary>
        /// Name, shader, and whether the shader can instance at all.
        ///
        /// <c>Material.enableInstancing</c> is a request, not a fact: it does nothing for a shader
        /// compiled without instancing support, and a submission through
        /// <c>RenderMeshInstanced</c> then quietly becomes one draw per instance. Whether the
        /// shader supports it is an editor-only question (<c>ShaderUtil.HasInstancing</c>), asked
        /// by reflection because the method's visibility has moved between versions.
        /// </summary>
        static string DescribeMaterial(Material material)
        {
            MethodInfo? has = typeof(ShaderUtil).GetMethod("HasInstancing",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            string instancing = has != null
                ? (has.Invoke(null, new object[] { material.shader }) is bool b ? (b ? "yes" : "NO") : "?")
                : "unknown";
            return $"{material.name}: shader '{material.shader.name}', enableInstancing {material.enableInstancing}, " +
                   $"shader supports instancing: {instancing}";
        }

        static readonly Type? Stats = typeof(EditorWindow).Assembly.GetType("UnityEditor.UnityStats");

        /// <summary>GPU draw calls, batches and SetPass calls for the frame just rendered.</summary>
        static string EditorStats()
        {
            if (Stats == null) return "UnityStats n/a";
            string Read(string name)
            {
                PropertyInfo? p = Stats.GetProperty(name,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                return p?.GetValue(null)?.ToString() ?? "?";
            }
            return $"draws {Read("drawCalls")}, batches {Read("batches")}, setpass {Read("setPassCalls")}, " +
                   $"tris {Read("triangles")}";
        }

        static double TimeFrames(Camera camera, Action submit, RenderTexture target,
            Vector3 focus, float pitch, float distance)
        {
            var rotation = Quaternion.Euler(pitch, 45f, 0f);
            camera.transform.SetPositionAndRotation(
                focus - rotation * Vector3.forward * distance, rotation);

            Action<ScriptableRenderContext, Camera> hook = (context, rendering) =>
            {
                if (rendering != camera) return;
                submit();
            };

            RenderPipelineManager.beginCameraRendering += hook;
            try
            {
                for (int i = 0; i < WarmupFrames; i++) camera.Render();
                Sync(target);

                // Synced every frame, on purpose, and this is the opposite of the first design.
                //
                // Syncing once per run let the GPU run ahead, which is the honest way to measure
                // throughput in a game — but there is no Present in a camera.Render() loop, so
                // nothing ever told the driver a frame was over. Forty frames of discarded
                // per-call constant buffers piled up before each sync and the driver stalled harder
                // the more had been queued: the same row cost 16 ms early in a table and 307 ms
                // late in it, and a row of 26-triangle tufts cost five times a row of cubes. The
                // numbers tracked position, not content. A sync per frame is an upper bound —
                // it serialises CPU and GPU — but it is the same upper bound for every row.
                var clock = Stopwatch.StartNew();
                for (int i = 0; i < TimedFrames; i++)
                {
                    camera.Render();
                    Sync(target);
                }
                clock.Stop();
                return clock.Elapsed.TotalMilliseconds / TimedFrames;
            }
            finally
            {
                RenderPipelineManager.beginCameraRendering -= hook;
            }
        }

        /// <summary>Block until the GPU has finished everything queued. See the earlier note on
        /// why a one-pixel read-back and not AsyncGPUReadback in a batch run.</summary>
        static void Sync(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixel.ReadPixels(new Rect(0f, 0f, 1f, 1f), 0, 0);
            pixel.Apply();
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(pixel);
        }

        static OutlineFeature? FindOutlineFeature()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            return data == null ? null : data.rendererFeatures.OfType<OutlineFeature>().FirstOrDefault();
        }
    }
}
