#nullable enable
using System;
using System.Collections;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    public partial class FrameTimeTests
    {
        /// <summary>
        /// The played meadow on Standard and on Huge, timed at the batch view and with the camera
        /// drawing into 3840 x 2160 — the reading design 45 §9 asks for when the scenery became
        /// real (bushes drawn from edifices, stones as items). It is written so the same file runs
        /// on the commit before (make the class partial there too) and the numbers are compared
        /// across two runs taken back to back, which is the best a machine that runs several
        /// editors supports. Tick is printed with the frame, for P12.
        ///
        /// <para>Explicit, and asserts only that each arm drew what it was asked to.</para>
        /// </summary>
        [UnityTest, Explicit("a measurement to compare across two commits, run by name")]
        public IEnumerator TheNatureAgainstTheFrame()
        {
            (string Label, int X, int Z, int Y)[] boards = { ("standard", 120, 120, 16), ("huge", 240, 240, 16) };
            var lines = new System.Collections.Generic.List<string>();
            foreach (var board in boards)
            {
                GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                    out OdysseyBootstrap boot, board.X, board.Z, board.Y);
                UnityEngine.Camera? cam = null;
                RenderTexture? previous = null, fourK = null;
                try
                {
                    float small = 0f, big = 0f, gpu = 0f;
                    double[] smallSplit = Array.Empty<double>(), bigSplit = Array.Empty<double>();
                    yield return TimeFrames($"nature/{board.Label}/batch", boot, WarmupFrames * 3,
                        m => small = m, p => smallSplit = p);
                    cam = boot.cameraRig!.Camera;
                    previous = cam.targetTexture;
                    fourK = new RenderTexture(3840, 2160, 24) { name = "nature-4k" };
                    cam.targetTexture = fourK;
                    yield return TimeFrames($"nature/{board.Label}/4k", boot, WarmupFrames,
                        m => big = m, p => bigSplit = p, g => gpu = g);
                    Assert.That(cam.pixelWidth, Is.EqualTo(3840));
                    Assert.That(boot.Renderer!.ChunksDrawn, Is.GreaterThan(0));
                    lines.Add($"{board.Label}: batch {small:0.00} ms (tick {boot.TickMs:0.000}); " +
                              $"4K {big:0.00} ms, gpu {gpu:0.00} ms, World {Section(bigSplit, OdysseyBootstrap.FrameSection.World):0.000}, " +
                              $"{boot.Renderer.DrawCalls} calls, {boot.Renderer.InstancesDrawn} instances");
                }
                finally
                {
                    if (cam != null) cam.targetTexture = previous;
                    if (fourK != null) fourK.Release();
                    UnityEngine.Object.Destroy(root);
                }
                yield return null;
                GC.Collect();
                yield return null;
            }
            foreach (string line in lines) Debug.Log("[FrameTime] nature " + line);
        }
    }
}
