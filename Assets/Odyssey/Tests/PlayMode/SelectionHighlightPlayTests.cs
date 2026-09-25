#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// The selection highlight under the real bootstrap and the real renderer asset
    /// (<c>docs/design/44-selection-highlight.md</c>): the feature is present, a selection reaches
    /// its list, the brackets rung takes it away, and nothing selected costs nothing.
    ///
    /// <para><b>What only these can say.</b> The EditMode tests hold the shape of what is collected;
    /// these hold that the renderer asset carries the feature (so the brackets are not silently
    /// standing in for it), that the capture points run in the order the composition root calls
    /// them, and what a render costs with a selection against the same world without one.</para>
    ///
    /// <para><b>The camera is rendered by hand, once a frame, into a texture.</b> A batch PlayMode
    /// run has no Game view, so no camera renders by itself — measured: the rig's camera rendered
    /// zero times in three frames — and a renderer feature is only ever asked for its passes by a
    /// render. The picture tests in <c>FrameTimeTests</c> drive <c>Camera.Render</c> for the same
    /// reason.</para>
    /// </summary>
    public class SelectionHighlightPlayTests
    {
        const int Width = 1280, Height = 720;

        /// <summary>Frames of the player loop, each followed by one render of the rig's camera.</summary>
        static IEnumerator Frames(int n, Camera camera, List<float>? renderMs = null)
        {
            for (int i = 0; i < n; i++)
            {
                yield return null;
                float t0 = Time.realtimeSinceStartup;
                camera.Render();
                renderMs?.Add((Time.realtimeSinceStartup - t0) * 1000f);
            }
        }

        static RenderTexture Target(Camera camera)
        {
            var target = new RenderTexture(Width, Height, 24) { name = "selection-highlight" };
            camera.targetTexture = target;
            return target;
        }

        static void Release(Camera? camera, RenderTexture? target)
        {
            if (camera != null) camera.targetTexture = null;
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }

        static CellRef OpenTileNear(ColonyWorld colony)
        {
            GridSize size = colony.Grid.Size;
            CellRef start = colony.Start;
            for (int dz = -3; dz <= 3; dz++)
            for (int dx = 3; dx >= -3; dx--)
            {
                int x = start.X + dx, z = start.Z + dz;
                if (!size.Contains(x, z, start.Y)) continue;
                int index = size.Index(x, z, start.Y);
                if (colony.Grid.IsBlockedByEdifice(index)) continue;
                return new CellRef(x, z, start.Y);
            }
            return start;
        }

        static List<PawnId> Colonists(OdysseyBootstrap boot, int most)
        {
            var ids = new List<PawnId>();
            ReadOnlySpan<PawnView> pawns = boot.World!.Views.Current.Pawns;
            for (int i = 0; i < pawns.Length && ids.Count < most; i++)
                if (pawns[i].IsColonist) ids.Add(pawns[i].Id);
            return ids;
        }

        [UnityTest]
        public IEnumerator ASelectionReachesTheHighlightAndTheBracketsRungTakesItAway()
        {
            GameObject root = RigWorld.Build(out OdysseyBootstrap boot, out SliceCameraRig rig);
            Camera camera = rig.Camera;
            RenderTexture? target = null;
            try
            {
                yield return RigWorld.WarmUp();
                yield return RigWorld.SettleCamera(rig);
                target = Target(camera);
                HudDirectors directors = boot.Directors!;
                directors.Settings.SetSelectionStyle(SelectionStyle.Highlight);

                yield return Frames(3, camera);
                Assert.That(SelectionHighlight.Camera, Is.SameAs(camera), "the highlight is bound to some other camera");
                Assert.That(SelectionHighlight.FeaturePresent, Is.True,
                    "the renderer asset has no selection highlight feature: run Odyssey > Presentation > " +
                    "Apply render setup. Without it every selection is quietly drawn as brackets");
                Assert.That(SelectionHighlight.Current.IsEmpty, Is.True, "nothing selected draws nothing");

                // A tile, which needs no licensed art and so runs on the runner too.
                directors.Selection.ChooseCell(OpenTileNear(boot.Colony!));
                yield return Frames(3, camera);
                Assert.That(SelectionHighlight.Current.IsEmpty, Is.False, "a selected tile was not lit");

                // A colonist, where there is art to draw one; the stand-in marker is the brackets' job.
                List<PawnId> colonists = Colonists(boot, 1);
                if (colonists.Count > 0 && boot.Figures != null && boot.Figures.CanDrawColonists)
                {
                    directors.Selection.Choose(colonists[0]);
                    yield return Frames(3, camera);
                    Assert.That(SelectionHighlight.Current.Count, Is.GreaterThan(0),
                        "a selected colonist was not lit at their own edges");
                }

                directors.Settings.SetSelectionStyle(SelectionStyle.Brackets);
                yield return Frames(2, camera);
                Assert.That(SelectionHighlight.Current.IsEmpty, Is.True,
                    "the Brackets rung still sent the selection to the highlight");

                directors.Settings.SetSelectionStyle(SelectionStyle.Highlight);
                directors.Selection.Clear();
                yield return Frames(2, camera);
                Assert.That(SelectionHighlight.Current.IsEmpty, Is.True, "a cleared selection left a highlight");
            }
            finally
            {
                Release(camera, target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// What the highlight costs a render, against the same world with nothing selected, in one run
        /// (design 44 §6c). Logged rather than gated, like the rest of the frame measurements: this
        /// machine runs several editors, and only a reading taken beside its own control means
        /// anything. It is the CPU side of a submitted render; the GPU side is the owner's overlay.
        /// The one assertion is the one that must always hold: nothing selected is no work.
        /// </summary>
        [UnityTest]
        [Category("Measurement")]
        public IEnumerator TheSelectionHighlightAgainstTheFrame()
        {
            GameObject root = RigWorld.Build(out OdysseyBootstrap boot, out SliceCameraRig rig);
            Camera camera = rig.Camera;
            RenderTexture? target = null;
            try
            {
                yield return RigWorld.WarmUp();
                yield return RigWorld.SettleCamera(rig);
                target = Target(camera);
                HudDirectors directors = boot.Directors!;
                directors.Settings.SetSelectionStyle(SelectionStyle.Highlight);
                const int Samples = 90;
                var line = new System.Text.StringBuilder("[selection-highlight]");

                IEnumerator Arm(string name, Action select)
                {
                    select();
                    yield return Frames(10, camera);
                    var ms = new List<float>(Samples);
                    yield return Frames(Samples, camera, ms);
                    ms.Sort();
                    line.Append($"  {name}: {ms[Samples / 2]:0.000} ms p50, {ms[Samples * 95 / 100]:0.000} p95, " +
                                $"{SelectionHighlight.Current.Count} parts");
                }

                yield return Arm("none", () => directors.Selection.Clear());
                Assert.That(SelectionHighlight.FeaturePresent, Is.True, "the arm measured a pipeline without the feature");
                Assert.That(SelectionHighlight.Current.IsEmpty, Is.True, "nothing selected must cost no pass at all");

                yield return Arm("tile", () => directors.Selection.ChooseCell(OpenTileNear(boot.Colony!)));

                List<PawnId> colonists = Colonists(boot, 12);
                if (colonists.Count > 0)
                {
                    yield return Arm("colonist", () => directors.Selection.Choose(colonists[0]));
                    yield return Arm($"box of {colonists.Count}",
                        () => directors.Selection.PickMany(colonists, false, SelectionChange.Boxed));
                }

                yield return Arm("none again", () => directors.Selection.Clear());

                line.Append($"  ({Width}x{Height}, art {(boot.Figures?.CanDrawColonists == true ? "present" : "absent")})");
                Debug.Log(line.ToString());
            }
            finally
            {
                Release(camera, target);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
