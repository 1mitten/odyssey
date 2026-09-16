#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Ui;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// Does the HUD think it is where it is drawn?
    ///
    /// <para><b>The bug this exists for.</b> A mouse position is screen space — origin at the
    /// bottom left, y climbing upward. A UI Toolkit panel has its origin at the top left with y
    /// climbing downward. <c>RuntimePanelUtils.ScreenToPanel</c> resolves the panel's own scaling,
    /// which is why the conversion cannot simply be divided out by hand, but it does <b>not</b>
    /// turn the axis over. Handed a mouse position directly it mirrors everything about the middle
    /// of the screen.</para>
    ///
    /// <para>It shipped that way in two places. The drag marquee was reported by the owner — "the
    /// selectable box does not come from the mouse cursor but actually quite below it some
    /// distance... like the exact opposite side of the screen" — and <c>PointOverUi</c>, which
    /// decides whether a click belongs to the HUD or to the world, had the same line and therefore
    /// the same fault, silently. Both go through one <c>ToPanel</c> helper now, so this covers
    /// both.</para>
    ///
    /// <para><b>Why <see cref="ScrollOverHudTests"/> could not catch it, and this can.</b> Those
    /// tests find a point over the HUD by asking <c>PointOverUi</c> where the HUD is. That is
    /// self-consistent with whatever the function does: mirror it and the search mirrors with it,
    /// the guard fires exactly where the search said, and the test passes. They prove the guard
    /// holds <i>wherever the function claims the HUD is</i>. They cannot prove the claim.</para>
    ///
    /// <para><b>Rewritten 2026-09-16, because the HUD rebuild falsified the old premise.</b> This
    /// used to sample a grid and assert that more of it landed in the lower half of the screen than
    /// the upper, on the grounds that the HUD was bottom-heavy — a full-width bar two rows tall
    /// along the bottom against a clock in one top corner. The rebuilt HUD is the other way round:
    /// the bar is one row and may not wrap, the empty inspect pane is a single line, and the depth
    /// rail is four hundred pixels down the top right. The old test failed, correctly, and its own
    /// remarks had predicted exactly that — "it only breaks if the HUD stops being bottom-heavy,
    /// which would be a design change worth failing a test over".</para>
    ///
    /// <para><b>What replaces it is stronger and needs no argument about balance:</b> the panel's
    /// own hit-test is the ground truth. <c>panel.Pick</c> takes a point already in panel
    /// coordinates and never touches the screen axis at all, so comparing it against
    /// <c>PointOverUi</c> over a grid asks precisely the question — does the screen-space path
    /// agree with the layout about where the HUD is? A missing flip disagrees at every sampled
    /// point that is not on the midline, so the test fails loudly rather than by one count.</para>
    ///
    /// <para>The round trip is not circular, and it is worth saying why. <c>ToScreen</c> below is
    /// written out here with its flip stated explicitly, so it is the inverse of a <i>correct</i>
    /// <c>ToPanel</c> rather than of whatever <c>ToPanel</c> happens to do. Drop the flip from the
    /// shell and the two stop being inverses, which is the whole of the fault.</para>
    /// </summary>
    public class PointerOriginTests
    {
        /// <summary>Samples across the canvas. Odd, so no row lands exactly on the midline.</summary>
        const int Columns = 31;
        const int Rows = 31;

        [UnityTest]
        public IEnumerator TheHudIsClickableExactlyWhereTheLayoutPutIt()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap _, out SliceCameraRig rig,
                out HudShell shell);
            try
            {
                yield return RigWorld.WarmUp();
                yield return RigWorld.SettleCamera(rig);

                var doc = root.GetComponentInChildren<UIDocument>();
                Assert.That(doc, Is.Not.Null, "the harness built no HUD document");

                VisualElement root2 = doc!.rootVisualElement;
                Rect canvas = root2.worldBound;
                Assert.That(canvas.width, Is.GreaterThan(0f), "the panel has no size");

                // Panel units to screen pixels. The panel fills the game view in this harness, so
                // one scale covers both axes — and the y axis is turned over, once, here.
                float scale = Screen.width / canvas.width;
                Vector2 ToScreen(Vector2 panel) =>
                    new Vector2(panel.x * scale, Screen.height - panel.y * scale);

                int over = 0, clear = 0, disagreements = 0;
                Vector2 firstDisagreement = default;
                string firstDetail = string.Empty;

                for (int row = 0; row < Rows; row++)
                for (int col = 0; col < Columns; col++)
                {
                    var point = new Vector2(
                        canvas.width * (col + 0.5f) / Columns,
                        canvas.height * (row + 0.5f) / Rows);

                    VisualElement? hit = root2.panel.Pick(point);
                    bool truth = hit != null && hit.name != "hud" && hit != root2;
                    bool claimed = shell.PointOverUi(ToScreen(point));

                    if (truth) over++; else clear++;
                    if (truth == claimed) continue;

                    if (disagreements == 0)
                    {
                        firstDisagreement = point;
                        firstDetail = truth
                            ? $"the layout puts '{hit!.name}' ({string.Join(".", hit.GetClasses())}) there " +
                              "and the pointer guard says the world"
                            : "nothing is drawn there and the pointer guard says the HUD";
                    }
                    disagreements++;
                }

                Assert.That(over, Is.GreaterThan(0),
                    "the HUD claims no point anywhere on the canvas, so this test proves nothing. " +
                    "Either the shell built no regions, or every region is non-pickable.");
                Assert.That(clear, Is.GreaterThan(0),
                    "the HUD covers every sampled point, which would be a coverage regression long " +
                    "before it was a pointer one");

                Assert.That(disagreements, Is.Zero,
                    $"{disagreements} of {Rows * Columns} sampled points disagree about whether they " +
                    $"are over the HUD. The first is {firstDisagreement}, where {firstDetail}. " +
                    "A mouse position is bottom-left origin and a panel is top-left origin; " +
                    "RuntimePanelUtils.ScreenToPanel does not turn the axis over. See " +
                    "HudShell.ToPanel.");
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
