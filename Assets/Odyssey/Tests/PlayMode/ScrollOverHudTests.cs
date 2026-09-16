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
    /// The scroll-over-the-HUD guard, which until now shipped on inspection alone.
    ///
    /// <para>Input case 8 of design 09 §6: a wheel over a panel scrolls the panel, a wheel over
    /// the world zooms the camera. Before the guard the wheel did both at once, and a scroll down
    /// the ledger hauled the camera in behind it. The fix was one condition, the owner confirmed
    /// it by hand, and it had no test — because mouse input could not be driven in a PlayMode
    /// test at all. That is what <see cref="MouseHarness"/> now makes possible.</para>
    ///
    /// <para>The pair below is the whole argument: the same wheel notch, the same world, the same
    /// number of frames, differing only in where the pointer is. Neither half means anything
    /// without the other — "the camera did not move" is what a broken harness produces too, which
    /// is exactly how three earlier tests passed while proving nothing.</para>
    /// </summary>
    public class ScrollOverHudTests
    {
        const int SettleFrames = 8;
        const float ZoomAtLeast = 0.5f;

        MouseHarness _mouse = null!;

        [SetUp]
        public void AddAMouse() => _mouse = new MouseHarness();

        [TearDown]
        public void RemoveTheMouse() => _mouse.Dispose();

        [UnityTest]
        public IEnumerator AWheelOverThePanelDoesNotZoomTheCamera()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap _, out SliceCameraRig rig,
                out HudShell shell);
            try
            {
                yield return RigWorld.WarmUp();
                yield return RigWorld.SettleCamera(rig);

                Vector2 overPanel = FindPointOverTheHud(shell);
                float before = rig.TargetDistance;

                yield return _mouse.Scroll(+1f, overPanel);
                for (int i = 0; i < SettleFrames; i++) yield return null;

                Assert.That(rig.TargetDistance, Is.EqualTo(before).Within(0.0001f),
                    $"a wheel notch at {overPanel} — over the HUD — moved the zoom target " +
                    $"{before:F3} -> {rig.TargetDistance:F3}. The guard is not holding.");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// The other half, in the same world with the same HUD: over the world the wheel must
        /// still zoom. Without this the guard could be "the wheel never works", which passes the
        /// test above and ruins the game.
        /// </summary>
        [UnityTest]
        public IEnumerator AWheelOverTheWorldStillZoomsWithTheHudUp()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap _, out SliceCameraRig rig,
                out HudShell shell);
            try
            {
                yield return RigWorld.WarmUp();
                yield return RigWorld.SettleCamera(rig);

                Vector2 overWorld = FindPointOverTheWorld(shell);
                float before = rig.TargetDistance;

                yield return _mouse.Scroll(+1f, overWorld);
                for (int i = 0; i < SettleFrames; i++) yield return null;

                Assert.That(Mathf.Abs(rig.TargetDistance - before), Is.GreaterThan(ZoomAtLeast),
                    $"a wheel notch at {overWorld} — over open world, with the HUD up — did not " +
                    $"zoom: the target went {before:F3} -> {rig.TargetDistance:F3}.");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// A screen point the HUD covers, found by asking the shell's own hit test over a coarse
        /// grid.
        ///
        /// <para>Asking the guard where the panels are is not circular: it decides the
        /// <i>precondition</i>, and what is asserted afterwards is what the camera did. Hard-coding
        /// a corner would be worse — the panel scales with the screen, so a point that is over the
        /// ledger at one resolution is over open world at another, and the test would quietly stop
        /// testing anything.</para>
        /// </summary>
        static Vector2 FindPointOverTheHud(HudShell shell)
        {
            for (int y = 0; y < 20; y++)
            for (int x = 0; x < 20; x++)
            {
                var point = new Vector2(Screen.width * (x + 0.5f) / 20f, Screen.height * (y + 0.5f) / 20f);
                if (shell.PointOverUi(point)) return point;
            }

            Assert.Fail("the HUD covers no point on a 20x20 grid over the screen, so there is " +
                        "nothing here to scroll over. Did the shell build any regions?");
            return default;
        }

        static Vector2 FindPointOverTheWorld(HudShell shell)
        {
            var middle = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (!shell.PointOverUi(middle)) return middle;

            for (int y = 0; y < 20; y++)
            for (int x = 0; x < 20; x++)
            {
                var point = new Vector2(Screen.width * (x + 0.5f) / 20f, Screen.height * (y + 0.5f) / 20f);
                if (!shell.PointOverUi(point)) return point;
            }

            Assert.Fail("the HUD covers the whole screen, so there is no world left to scroll over");
            return default;
        }
    }
}
