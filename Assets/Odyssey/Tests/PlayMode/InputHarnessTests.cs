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
        /// How far a wheel notch must move the camera's <b>target</b> to count as delivered.
        ///
        /// <para><b>The target, not the drawn distance, and that took two failures to learn.</b>
        /// The rig smooths toward its target exponentially, so the drawn value approaches and
        /// never arrives — and how far it gets in a fixed number of frames depends on the frame
        /// rate. A batch player runs frames in about a millisecond, so eight of them advance the
        /// smoothing by a few per cent: a notch that moved the target by six units moved the drawn
        /// distance by 0.457, which read as "the wheel did not reach the camera" when it plainly
        /// had. On the owner's machine at sixty frames a second the same test would have passed.
        /// That is a frame-rate-dependent assertion, which is a flaky test waiting to happen.</para>
        ///
        /// <para>The target moves the instant the wheel is read and does not drift afterwards, so
        /// both the assertion and its control are exact.</para>
        /// </summary>
        const float ZoomAtLeast = 0.5f;

        MouseHarness _mouse = null!;

        /// <summary>See <see cref="MouseHarness"/>: without it there is no device, and with a
        /// device but no focus there is a device that drops everything.</summary>
        [SetUp]
        public void AddAMouse() => _mouse = new MouseHarness();

        [TearDown]
        public void RemoveTheMouse() => _mouse.Dispose();

        [UnityTest]
        public IEnumerator AWheelNotchZoomsTheCamera()
        {
            GameObject root = RigWorld.Build(out OdysseyBootstrap boot, out SliceCameraRig rig);
            try
            {
                yield return RigWorld.WarmUp();
                yield return RigWorld.SettleCamera(rig);
                float before = rig.TargetDistance;

                yield return _mouse.Scroll(+1f, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
                for (int i = 0; i < SettleFrames; i++) yield return null;

                Assert.That(Mathf.Abs(rig.TargetDistance - before), Is.GreaterThan(ZoomAtLeast),
                    $"the wheel did not reach the camera: the zoom target went {before:F3} -> " +
                    $"{rig.TargetDistance:F3}. Mouse input is not reaching the game in this test.");
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
                float before = rig.TargetDistance;

                for (int i = 0; i < SettleFrames; i++) yield return null;

                Assert.That(rig.TargetDistance, Is.EqualTo(before).Within(0.0001f),
                    $"the zoom target moved {before:F3} -> {rig.TargetDistance:F3} with no input at " +
                    "all, so any zoom the test above sees is not the wheel");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

    }
}
