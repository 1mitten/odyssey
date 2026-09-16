#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// Can mouse input be driven in a PlayMode test at all? (OQ-40, the half that blocks the
    /// other half.)
    ///
    /// <para>Nothing about pointer behaviour can be tested until this works, and an earlier
    /// attempt wrote three tests that passed without the input ever arriving — they were deleted
    /// rather than kept. So the rule for this file is that <b>every capability test has a control
    /// beside it that withholds the input and requires the same assertion to fail</b>.</para>
    ///
    /// <para>The subject is the camera zoom, because it is the simplest thing in the game that a
    /// mouse can move: one wheel notch changes <c>SliceCameraRig.distance</c> and nothing else
    /// does.</para>
    /// </summary>
    public class InputHarnessTests
    {
        /// <summary>Frames to let the rig read input and smooth toward its target.</summary>
        const int SettleFrames = 8;

        /// <summary>
        /// How far the camera may creep on its own over <see cref="SettleFrames"/>, and how far a
        /// wheel notch must move it to count.
        ///
        /// <para>The rig smooths toward its target exponentially, so it approaches and never
        /// arrives: measured drift after settling is about 0.005 over eight frames. Asserting
        /// "did not move at all" therefore fails on a camera nobody touched — the control caught
        /// exactly that. One notch moves it by <c>zoomSpeed</c> scaled by distance, which is
        /// several units, so there are two orders of magnitude between the signal and the
        /// creep and the two numbers below sit in the gap.</para>
        /// </summary>
        const float DriftAtMost = 0.05f;
        const float ZoomAtLeast = 0.5f;

        Mouse? _mouse;

        /// <summary>
        /// <b>There is no mouse in a batch PlayMode run.</b> `Mouse.current` is null — the player
        /// has no window, no operating-system pointer and therefore no device — and that, not the
        /// queueing, is why the earlier attempt could not drive input: it queued state at a device
        /// that did not exist, and <c>SliceCameraRig.ReadMouse</c> returns immediately when
        /// <c>Mouse.current</c> is null, so nothing could ever have arrived.
        ///
        /// <para>Adding one makes a real <c>Mouse</c> out of the input system's own layout, with
        /// no hardware behind it. Events queued at it go through the ordinary player-loop update,
        /// so what is being tested is the game's real input path rather than a stub of it.</para>
        /// </summary>
        [SetUp]
        public void AddAMouse()
        {
            _mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
        }

        [TearDown]
        public void RemoveTheMouse()
        {
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            _mouse = null;
        }

        [UnityTest]
        public IEnumerator AWheelNotchZoomsTheCamera()
        {
            GameObject root = RigWorld.Build(out OdysseyBootstrap boot, out SliceCameraRig rig);
            try
            {
                yield return RigWorld.WarmUp();
                yield return RigWorld.SettleCamera(rig);
                float before = rig.distance;

                yield return Scroll(+1f);
                for (int i = 0; i < SettleFrames; i++) yield return null;

                Assert.That(Mathf.Abs(rig.distance - before), Is.GreaterThan(ZoomAtLeast),
                    $"the wheel did not reach the camera: distance went {before:F3} -> {rig.distance:F3}. " +
                    "Mouse input is not being delivered to the player loop in this test.");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// The control. Identical to the test above but for the scroll, and it must hold: if the
        /// camera drifts on its own then the test above proves nothing about input.
        /// </summary>
        [UnityTest]
        public IEnumerator WithoutAWheelNotchTheCameraStaysWhereItIs()
        {
            GameObject root = RigWorld.Build(out OdysseyBootstrap boot, out SliceCameraRig rig);
            try
            {
                yield return RigWorld.WarmUp();
                yield return RigWorld.SettleCamera(rig);
                float before = rig.distance;

                for (int i = 0; i < SettleFrames; i++) yield return null;

                Assert.That(Mathf.Abs(rig.distance - before), Is.LessThan(DriftAtMost),
                    $"the camera moved {before:F3} -> {rig.distance:F3} with no input at all, " +
                    "so any zoom the test above sees is not the wheel");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// One wheel notch over the world, the way the earlier attempt drove it: queue a mouse
        /// state and let the player loop pick it up.
        /// </summary>
        static IEnumerator Scroll(float notches)
        {
            Mouse? mouse = Mouse.current;
            Assert.That(mouse, Is.Not.Null, "there is no mouse device at all in this test run");

            InputSystem.QueueStateEvent(mouse!, new MouseState
            {
                position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                scroll = new Vector2(0f, notches),
            });
            yield return null;
        }
    }
}
