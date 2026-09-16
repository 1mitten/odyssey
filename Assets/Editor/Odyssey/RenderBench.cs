#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    /// Renders real frames on a real GPU and times them, one stylisation option at a time.
    ///
    /// **Why this is separate from <c>PlayScene.Measure</c>.** That one runs headless with no
    /// graphics device, so it can report draw calls and instance counts — which are exact — and
    /// deliberately reports no frame time at all, because a frame time without a GPU would be
    /// fiction. Every question about how much the grass or the outline costs is a question about
    /// the GPU, so it needs a device, which means it belongs with the screenshot harness rather
    /// than with the headless one.
    ///
    /// **What the numbers are worth.** This machine is not the target — that is a 2022 mid-range
    /// laptop — so the absolute figures mean little. The comparisons mean a great deal: each
    /// variant differs from its neighbour by exactly one decision, so the column that matters is
    /// the delta, and a decision that costs nothing measurable is a decision that can be made on
    /// looks alone.
    ///
    /// Run with <c>scripts/unity.sh shot Odyssey.EditorTools.RenderBench.Run</c> — it needs a
    /// graphics device, like everything else here that touches a frame.
    /// </summary>
    public static class RenderBench
    {
        const int PlaySizeXZ = 120;
        const int PlayLayers = 16;
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";

        /// <summary>Frames thrown away before timing, so shader compilation is not in the figure.</summary>
        const int WarmupFrames = 8;

        /// <summary>Frames timed per variant. Enough to average out driver hitches.</summary>
        const int TimedFrames = 40;

        [MenuItem("Odyssey/Presentation/Benchmark the renderer")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        /// <summary>One row of the table: what was switched on, and what it cost.</summary>
        readonly struct Variant
        {
            public readonly string Name;
            public readonly int Scatter;
            public readonly bool FoliageShadows;
            public readonly bool Outline;

            public Variant(string name, int scatter, bool foliageShadows, bool outline)
            {
                Name = name;
                Scatter = scatter;
                FoliageShadows = foliageShadows;
                Outline = outline;
            }
        }

        static readonly Variant[] Variants =
        {
            // A ladder, each rung one decision away from the last, so every delta is attributable.
            new Variant("bare ground, no outline",       0,   false, false),
            new Variant("grass, no grass shadows",       120, false, false),
            new Variant("grass, grass casts shadows",    120, true,  false),
            new Variant("grass + outline",               120, false, true),
            new Variant("dense grass (240) + outline",   240, false, true),
        };

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            OutlineFeature? outline = FindOutlineFeature();
            bool outlineWas = outline != null && outline.isActive;
            GameObject? lighting = null;
            GameObject? cameraObject = null;
            RenderTexture? target = null;

            try
            {
                var size = new GridSize(PlaySizeXZ, PlaySizeXZ, PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
                gen.MakeBarren();
                var grid = new CellGrid(size);
                var chunks = new ChunkGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);

                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(
                    "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
                var library = new ModuleLibrary(catalogue);
                var model = new Odyssey.Presentation.World.WorldRenderModel(size, chunks, library);
                model.RefreshAll(grid, result.Natural!.Context.Edifices);

                lighting = new GameObject("BenchLighting");
                PlayScene.BuildSheetLighting(lighting.transform);

                cameraObject = new GameObject("BenchCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);
                camera.enabled = false;

                target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;

                int activeLayer = result.StartCell.Y;
                var focus = new Vector3(
                    result.StartCell.X * CellMetrics.SizeXZ,
                    activeLayer * CellMetrics.SizeY,
                    result.StartCell.Z * CellMetrics.SizeXZ);

                var report = new StringBuilder();
                report.AppendLine($"[Bench] {PlaySizeXZ}x{PlaySizeXZ}x{PlayLayers} barren, " +
                                  $"1920x1080, {TimedFrames} frames after {WarmupFrames} warm-up, " +
                                  $"layer {activeLayer}. This machine is not the target; read the deltas.");

                // Two framings, because the two costs pull in opposite directions: close up the
                // outline covers more of a pixel budget it barely uses, while zoomed out there is
                // far more geometry on screen and the grass is what bites.
                var framings = new (string name, float pitch, float distance)[]
                {
                    ("board", 48f, 48f),
                    ("close", 42f, 18f),
                };

                double bareBoard = 0d;
                foreach ((string framing, float pitch, float distance) in framings)
                {
                    report.AppendLine();
                    report.AppendLine($"  {framing} camera (pitch {pitch}, {distance} m)");

                    double previous = 0d;
                    for (int v = 0; v < Variants.Length; v++)
                    {
                        Variant variant = Variants[v];
                        if (outline != null) outline.SetActive(variant.Outline);

                        // A scatter change is a meshing change, so the batches have to go: the
                        // renderer caches a meshed chunk until the model says it changed, and a
                        // benchmark that quietly re-used last variant's grass would report the
                        // same number twice and look like a very convincing null result.
                        using var renderer = new ChunkRenderer(model)
                        {
                            ScatterDensity = variant.Scatter,
                            FoliageCastsShadows = variant.FoliageShadows,
                        };

                        double ms = TimeFrames(camera, renderer, target, focus, pitch, distance,
                            activeLayer);

                        string delta = v == 0
                            ? string.Empty
                            : $"  ({ms - previous:+0.00;-0.00;0.00} ms)";
                        previous = ms;
                        if (v == 0 && framing == "board") bareBoard = ms;

                        report.AppendLine(
                            $"    {variant.Name,-30} {ms,7:0.00} ms/frame{delta,-14}" +
                            $"  {renderer.DrawCalls,5} calls  {renderer.InstancesDrawn,7} instances");
                    }
                }

                report.AppendLine();
                report.AppendLine($"  bare board frame: {bareBoard:0.00} ms — the floor everything " +
                                  $"else is measured against.");
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
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lighting != null) UnityEngine.Object.DestroyImmediate(lighting);
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// Time a run of frames, with the world submitted exactly as the game submits it.
        ///
        /// The GPU is left to run ahead and is forced to catch up once, at the end, rather than
        /// after every frame. Syncing per frame would serialise the two and measure their sum;
        /// syncing once measures the throughput of the slower of them, which is the number that
        /// decides a frame rate.
        /// </summary>
        static double TimeFrames(Camera camera, ChunkRenderer renderer, RenderTexture target,
            Vector3 focus, float pitch, float distance, int activeLayer)
        {
            var slice = new SliceSettings();
            var rotation = Quaternion.Euler(pitch, 45f, 0f);
            camera.transform.SetPositionAndRotation(
                focus - rotation * Vector3.forward * distance, rotation);

            Action<ScriptableRenderContext, Camera> hook = (context, rendering) =>
            {
                if (rendering != camera) return;
                renderer.Render(activeLayer, slice);
            };

            RenderPipelineManager.beginCameraRendering += hook;
            try
            {
                for (int i = 0; i < WarmupFrames; i++) camera.Render();
                Sync(target);

                var clock = Stopwatch.StartNew();
                for (int i = 0; i < TimedFrames; i++) camera.Render();
                Sync(target);
                clock.Stop();

                return clock.Elapsed.TotalMilliseconds / TimedFrames;
            }
            finally
            {
                RenderPipelineManager.beginCameraRendering -= hook;
            }
        }

        /// <summary>
        /// Block until the GPU has finished everything queued, so the clock is honest.
        ///
        /// A one-pixel read-back rather than <c>AsyncGPUReadback.WaitForCompletion</c>: that call
        /// wants a pump to service it and there is no player loop in a batch run, so it waits for
        /// a completion that will never be signalled. Reading a pixel forces the same stall
        /// through a path that does not need one.
        /// </summary>
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
            if (data == null) return null;
            return data.rendererFeatures.OfType<OutlineFeature>().FirstOrDefault();
        }
    }
}
