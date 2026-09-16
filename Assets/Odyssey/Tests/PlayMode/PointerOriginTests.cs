#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Ui;
using UnityEngine;
using UnityEngine.TestTools;

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
    /// <para>So the assertion below is anchored in something the coordinate transform cannot
    /// influence: <b>this HUD is bottom-heavy</b>. The bottom bar runs the full width of the
    /// screen and is two rows tall once every tab carries its name; the top edge holds only the
    /// clock, in one corner. The left and right columns run vertically and contribute to both
    /// halves about equally. So the HUD must claim more of the lower half of the screen than the
    /// upper half — and a vertical mirror is exactly the fault that reverses that.</para>
    ///
    /// <para>Deliberately an inequality rather than named points. A test that asserted "the HUD
    /// covers the bottom centre" would break the first time somebody moved a region for a good
    /// reason; this one only breaks if the HUD stops being bottom-heavy, which would be a design
    /// change worth failing a test over, or if the axis flips again, which is the fault.</para>
    /// </summary>
    public class PointerOriginTests
    {
        /// <summary>Samples across the screen. Odd so no row lands exactly on the midline.</summary>
        const int Columns = 21;
        const int Rows = 21;

        [UnityTest]
        public IEnumerator TheHudClaimsMoreOfTheBottomOfTheScreenThanTheTop()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap _, out SliceCameraRig rig,
                out HudShell shell);
            try
            {
                yield return RigWorld.WarmUp();
                yield return RigWorld.SettleCamera(rig);

                int bottom = 0, top = 0;
                for (int row = 0; row < Rows; row++)
                for (int col = 0; col < Columns; col++)
                {
                    float y = Screen.height * (row + 0.5f) / Rows;
                    var point = new Vector2(Screen.width * (col + 0.5f) / Columns, y);
                    if (!shell.PointOverUi(point)) continue;

                    if (y < Screen.height * 0.5f) bottom++;
                    else top++;
                }

                // A HUD that claims nothing at all would pass an inequality by accident, and that
                // is a real possibility here: the shell builds its regions over several frames.
                Assert.That(bottom + top, Is.GreaterThan(0),
                    "the HUD claims no point anywhere on the screen, so this test proves nothing. " +
                    "Either the shell built no regions or PointOverUi is answering false for " +
                    "everything.");

                Assert.That(bottom, Is.GreaterThan(top),
                    $"the HUD claims {top} sampled points in the TOP half of the screen and only " +
                    $"{bottom} in the bottom half. This HUD is bottom-heavy — a full-width bar two " +
                    "rows tall along the bottom, a clock in one top corner — so that is the wrong " +
                    "way round, and a vertical mirror is what reverses it. A mouse position is " +
                    "bottom-left origin and a panel is top-left origin; RuntimePanelUtils." +
                    "ScreenToPanel does not turn the axis over. See HudShell.ToPanel.");
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
