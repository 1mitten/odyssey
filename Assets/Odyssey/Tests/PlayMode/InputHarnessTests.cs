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

        /// <summary>
        /// Can the harness press a <b>button</b>? The wheel above proves only that state arrives;
        /// a button is a different question because <c>wasPressedThisFrame</c> is true for exactly
        /// one input update, and the pump runs its own update inside an <c>Update</c> rather than
        /// in the player loop. Every gesture in the game — pick, box-select, designate — is built
        /// on that one property, and nothing had ever asserted it.
        /// </summary>
        [UnityTest]
        public IEnumerator AClickIsSeenAsAPressAndARelease()
        {
            var probe = new GameObject("ButtonProbe").AddComponent<ButtonProbe>();
            try
            {
                yield return null;
                probe.Reset();

                yield return _mouse.Click(new Vector2(320f, 240f));
                yield return null;

                Assert.That(probe.Presses, Is.EqualTo(1),
                    $"an ordinary Update saw {probe.Presses} presses and {probe.Releases} releases " +
                    "from one Click. Every world gesture is built on wasPressedThisFrame, so if " +
                    "this is zero the harness cannot click and no gesture test below means " +
                    "anything. At the moment of delivery the pump itself saw: press edges " +
                    $"{_mouse.Pump.PressEdgesAtDelivery}, release edges " +
                    $"{_mouse.Pump.ReleaseEdgesAtDelivery}, deliveries with the button down " +
                    $"{_mouse.Pump.DeliveriesWithTheButtonDown}, deliveries the device counted as " +
                    $"this frame {_mouse.Pump.DeliveriesTheDeviceSawAsThisFrame} - which says " +
                    "whether the edge was " +
                    "never created or created and spent before an ordinary Update could see it. " +
                    $"The input system at that moment: {_mouse.Pump.StateAtDelivery}.");
                Assert.That(probe.Releases, Is.EqualTo(1),
                    $"the press arrived but the release did not ({probe.Releases}), so a gesture " +
                    "would begin and never end");
            }
            finally
            {
                Object.Destroy(probe.gameObject);
            }
        }

        /// <summary>The control: with no click, the probe must see nothing.</summary>
        [UnityTest]
        public IEnumerator WithoutAClickTheProbeSeesNoButton()
        {
            var probe = new GameObject("ButtonProbe").AddComponent<ButtonProbe>();
            try
            {
                yield return null;
                probe.Reset();

                for (int i = 0; i < 4; i++) yield return null;

                Assert.That(probe.Presses + probe.Releases, Is.EqualTo(0),
                    $"the probe saw {probe.Presses} presses and {probe.Releases} releases with no " +
                    "input at all, so it is not measuring the click");
            }
            finally
            {
                Object.Destroy(probe.gameObject);
            }
        }

        /// <summary>
        /// Reads the left button the way <c>SliceCameraRig</c> does: from an ordinary
        /// <c>Update</c>, at the default execution order, through <c>Mouse.current</c>.
        /// </summary>
        sealed class ButtonProbe : MonoBehaviour
        {
            public int Presses { get; private set; }

            public int Releases { get; private set; }

            public void Reset() => Presses = Releases = 0;

            void Update()
            {
                Mouse? mouse = Mouse.current;
                if (mouse == null) return;
                if (mouse.leftButton.wasPressedThisFrame) Presses++;
                if (mouse.leftButton.wasReleasedThisFrame) Releases++;
            }
        }

    }
}
