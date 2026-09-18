#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.World;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    [TestFixture]
    public class HeadLookKinematicsTests
    {
        GameObject? _go;
        Transform? _refFrame;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestRefFrame");
            _refFrame = _go.transform;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void StraightAheadGivesZeroYawAndPitch()
        {
            _refFrame!.position = Vector3.zero;
            _refFrame.rotation = Quaternion.identity; // forward is (0, 0, 1), up is (0, 1, 0)
            Vector3 head = new Vector3(0f, 1.6f, 0f);
            Vector3 target = new Vector3(0f, 1.6f, 5f);

            bool ok = HeadLookKinematics.SolveAngles(target, _refFrame, head, out float yaw, out float pitch);

            Assert.That(ok, Is.True);
            Assert.That(yaw, Is.EqualTo(0f).Within(0.01f));
            Assert.That(pitch, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void TargetToTheRightGivesPositiveYaw()
        {
            _refFrame!.position = Vector3.zero;
            _refFrame.rotation = Quaternion.identity;
            Vector3 head = new Vector3(0f, 1.6f, 0f);
            Vector3 target = new Vector3(5f, 1.6f, 0f);

            bool ok = HeadLookKinematics.SolveAngles(target, _refFrame, head, out float yaw, out float pitch);

            Assert.That(ok, Is.True);
            Assert.That(yaw, Is.EqualTo(90f).Within(0.01f));
            Assert.That(pitch, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void TargetToTheLeftGivesNegativeYaw()
        {
            _refFrame!.position = Vector3.zero;
            _refFrame.rotation = Quaternion.identity;
            Vector3 head = new Vector3(0f, 1.6f, 0f);
            Vector3 target = new Vector3(-5f, 1.6f, 0f);

            bool ok = HeadLookKinematics.SolveAngles(target, _refFrame, head, out float yaw, out float pitch);

            Assert.That(ok, Is.True);
            Assert.That(yaw, Is.EqualTo(-90f).Within(0.01f));
            Assert.That(pitch, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void TargetAboveGivesPositivePitch()
        {
            _refFrame!.position = Vector3.zero;
            _refFrame.rotation = Quaternion.identity;
            Vector3 head = new Vector3(0f, 1.6f, 0f);
            Vector3 target = new Vector3(0f, 5f, 0f);

            bool ok = HeadLookKinematics.SolveAngles(target, _refFrame, head, out float yaw, out float pitch);

            Assert.That(ok, Is.True);
            Assert.That(pitch, Is.EqualTo(90f).Within(0.01f));
        }

        [Test]
        public void TargetBelowGivesNegativePitch()
        {
            _refFrame!.position = Vector3.zero;
            _refFrame.rotation = Quaternion.identity;
            Vector3 head = new Vector3(0f, 1.6f, 0f);
            Vector3 target = new Vector3(0f, -2f, 0f);

            bool ok = HeadLookKinematics.SolveAngles(target, _refFrame, head, out float yaw, out float pitch);

            Assert.That(ok, Is.True);
            Assert.That(pitch, Is.EqualTo(-90f).Within(0.01f));
        }

        [Test]
        public void ReferenceFrameRotationIsRespected()
        {
            _refFrame!.position = Vector3.zero;
            _refFrame.rotation = Quaternion.Euler(0f, 90f, 0f); // facing East (+X)
            Vector3 head = new Vector3(0f, 1.6f, 0f);
            Vector3 target = new Vector3(5f, 1.6f, 0f); // target is directly East (straight ahead in frame)

            bool ok = HeadLookKinematics.SolveAngles(target, _refFrame, head, out float yaw, out float pitch);

            Assert.That(ok, Is.True);
            Assert.That(yaw, Is.EqualTo(0f).Within(0.01f));
            Assert.That(pitch, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void RearWeightAttenuatesSmoothlyPastSixtyDegrees()
        {
            Assert.That(HeadLookKinematics.ComputeRearWeight(0f), Is.EqualTo(1.0f));
            Assert.That(HeadLookKinematics.ComputeRearWeight(45f), Is.EqualTo(1.0f));
            Assert.That(HeadLookKinematics.ComputeRearWeight(60f), Is.EqualTo(1.0f));

            float mid = HeadLookKinematics.ComputeRearWeight(77.5f);
            Assert.That(mid, Is.GreaterThan(0.1f));
            Assert.That(mid, Is.LessThan(0.9f));

            Assert.That(HeadLookKinematics.ComputeRearWeight(95f), Is.EqualTo(0.0f));
            Assert.That(HeadLookKinematics.ComputeRearWeight(120f), Is.EqualTo(0.0f));
        }

        [Test]
        public void DampedAnglesClampsToLimits()
        {
            _refFrame!.position = Vector3.zero;
            _refFrame.rotation = Quaternion.identity;
            Vector3 head = new Vector3(0f, 1.6f, 0f);

            var state = new LookGazeState
            {
                HasTarget = true,
                TargetWorldPosition = new Vector3(10f, 10f, 2f), // extreme up and right
                ActivePriority = GazePriority.WorkFocus,
            };

            // Step damping forward significantly to settle near target
            for (int i = 0; i < 30; i++)
            {
                HeadLookKinematics.UpdateDampedAngles(ref state, _refFrame, head, 0.05f);
            }

            Assert.That(state.CurrentAngles.y, Is.LessThanOrEqualTo(HeadLookKinematics.YawLimit));
            Assert.That(state.CurrentAngles.x, Is.LessThanOrEqualTo(HeadLookKinematics.PitchMax));
        }

        [Test]
        public void SleepLockClearsAnglesImmediately()
        {
            var state = new LookGazeState
            {
                CurrentAngles = new Vector2(20f, 35f),
                AngleVelocity = new Vector2(5f, 5f),
                GazeWeight = 1f,
                ActivePriority = GazePriority.SleepLock,
            };

            HeadLookKinematics.UpdateDampedAngles(ref state, _refFrame, Vector3.zero, 0.05f);

            Assert.That(state.CurrentAngles.x, Is.EqualTo(0f));
            Assert.That(state.CurrentAngles.y, Is.EqualTo(0f));
            Assert.That(state.GazeWeight, Is.EqualTo(0f));
        }

        [Test]
        public void AdditiveRotationRotatesHeadSatOnNeckWithoutRoll()
        {
            var neckObj = new GameObject("Neck");
            var headObj = new GameObject("Head");
            headObj.transform.parent = neckObj.transform;

            try
            {
                Vector3 yawAxis = Vector3.up;
                Vector3 pitchAxis = Vector3.right;

                // Pure yaw (50 deg): Neck remains static (0%), Head gets 100% (50 deg)
                Vector2 yawOnly = new Vector2(0f, 50f);
                HeadLookKinematics.ApplyAdditiveRotation(
                    neckObj.transform, headObj.transform, yawAxis, pitchAxis, yawOnly, 1f);

                // Neck was not rotated at all
                Assert.That(neckObj.transform.localRotation, Is.EqualTo(Quaternion.identity));

                // Head got full 50 deg yaw with 0 roll
                float headYaw = headObj.transform.localEulerAngles.y;
                if (headYaw > 180f) headYaw -= 360f;
                Assert.That(headYaw, Is.EqualTo(50f).Within(0.1f));

                float headRoll = headObj.transform.localEulerAngles.z;
                if (headRoll > 180f) headRoll -= 360f;
                Assert.That(headRoll, Is.EqualTo(0f).Within(0.01f), "yawing around neck axis produces zero ear-to-shoulder roll");

                // Reset transforms
                headObj.transform.localRotation = Quaternion.identity;

                // Pure pitch (-30 deg): Head gets 100% pitch down
                Vector2 pitchOnly = new Vector2(-30f, 0f);
                HeadLookKinematics.ApplyAdditiveRotation(
                    neckObj.transform, headObj.transform, yawAxis, pitchAxis, pitchOnly, 1f);

                Assert.That(neckObj.transform.localRotation, Is.EqualTo(Quaternion.identity));

                float headPitch = headObj.transform.localEulerAngles.x;
                if (headPitch > 180f) headPitch -= 360f;
                Assert.That(headPitch, Is.EqualTo(30f).Within(0.1f));

                float pitchRoll = headObj.transform.localEulerAngles.z;
                if (pitchRoll > 180f) pitchRoll -= 360f;
                Assert.That(pitchRoll, Is.EqualTo(0f).Within(0.01f), "pitching produces zero roll");
            }
            finally
            {
                Object.DestroyImmediate(neckObj);
            }
        }

        [Test]
        public void WorkFocusElevatedTargetLevelsGazeAtTreeTrunk()
        {
            _refFrame!.position = new Vector3(0f, 0f, 0f);
            _refFrame.rotation = Quaternion.identity; // facing +Z
            Vector3 colonistEye = new Vector3(0f, 1.4f, 0f);

            // Ground-level cell floor centre 2.5m ahead
            Vector3 floorCentre = new Vector3(0f, 0f, 2.5f);

            // Without elevation: aiming at floor centre causes steep downward pitch
            HeadLookKinematics.SolveAngles(floorCentre, _refFrame, colonistEye, out float rawYaw, out float groundPitch);
            Assert.That(groundPitch, Is.LessThan(-25f), "ground level target forces steep downward gaze");

            // With +1.30m elevation: aiming at tree trunk at chest/eye level yields near-level pitch
            Vector3 elevatedTrunk = floorCentre + Vector3.up * 1.30f;
            HeadLookKinematics.SolveAngles(elevatedTrunk, _refFrame, colonistEye, out rawYaw, out float trunkPitch);
            Assert.That(trunkPitch, Is.EqualTo(-2.3f).Within(1.0f), "elevated trunk target keeps gaze nearly level at tree trunk");
            Assert.That(rawYaw, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void LadderTraversalAscendingAnglesUp()
        {
            _refFrame!.position = Vector3.zero;
            _refFrame.rotation = Quaternion.identity;

            var state = new LookGazeState
            {
                IsGlancing = true,
                AmbientAngles = new Vector2(30f, 0f), // ascending ladder (+30 pitch)
                ActivePriority = GazePriority.LadderTraversal,
            };

            for (int i = 0; i < 30; i++)
            {
                HeadLookKinematics.UpdateDampedAngles(ref state, _refFrame, Vector3.zero, 0.05f);
            }

            Assert.That(state.CurrentAngles.x, Is.EqualTo(30f).Within(0.5f));
            Assert.That(state.CurrentAngles.y, Is.EqualTo(0f).Within(0.1f));
        }

        [Test]
        public void LadderTraversalDescendingAnglesDown()
        {
            _refFrame!.position = Vector3.zero;
            _refFrame.rotation = Quaternion.identity;

            var state = new LookGazeState
            {
                IsGlancing = true,
                AmbientAngles = new Vector2(-35f, 0f), // descending ladder (-35 pitch)
                ActivePriority = GazePriority.LadderTraversal,
            };

            for (int i = 0; i < 30; i++)
            {
                HeadLookKinematics.UpdateDampedAngles(ref state, _refFrame, Vector3.zero, 0.05f);
            }

            Assert.That(state.CurrentAngles.x, Is.EqualTo(-35f).Within(0.5f));
            Assert.That(state.CurrentAngles.y, Is.EqualTo(0f).Within(0.1f));
        }

        [Test]
        public void PosturePitchOffsetAppliesDownwardTilt()
        {
            _refFrame!.position = Vector3.zero;
            _refFrame.rotation = Quaternion.identity;

            // Hauling posture (-12 deg) while looking straight ahead
            var hauling = new LookGazeState
            {
                PosturePitchOffset = -12f,
                ActivePriority = GazePriority.PathForward,
            };

            for (int i = 0; i < 30; i++)
            {
                HeadLookKinematics.UpdateDampedAngles(ref hauling, _refFrame, Vector3.zero, 0.05f);
            }

            Assert.That(hauling.CurrentAngles.x, Is.EqualTo(-12f).Within(0.5f));
            Assert.That(hauling.CurrentAngles.y, Is.EqualTo(0f).Within(0.1f));

            // Eating posture (-25 deg)
            var eating = new LookGazeState
            {
                PosturePitchOffset = -25f,
                ActivePriority = GazePriority.PathForward,
            };

            for (int i = 0; i < 30; i++)
            {
                HeadLookKinematics.UpdateDampedAngles(ref eating, _refFrame, Vector3.zero, 0.05f);
            }

            Assert.That(eating.CurrentAngles.x, Is.EqualTo(-25f).Within(0.5f));
            Assert.That(eating.CurrentAngles.y, Is.EqualTo(0f).Within(0.1f));
        }

        [Test]
        public void GlancingReturnsSmoothlyToNeutral()
        {
            _refFrame!.position = Vector3.zero;
            _refFrame.rotation = Quaternion.identity;

            var state = new LookGazeState
            {
                IsGlancing = true,
                AmbientAngles = new Vector2(10f, 30f),
                ActivePriority = GazePriority.AmbientWander,
            };

            for (int i = 0; i < 30; i++)
            {
                HeadLookKinematics.UpdateDampedAngles(ref state, _refFrame, Vector3.zero, 0.05f);
            }

            Assert.That(state.CurrentAngles.y, Is.EqualTo(30f).Within(0.5f));

            // Now glance ends, returns to forward
            state.IsGlancing = false;
            state.ActivePriority = GazePriority.PathForward;

            for (int i = 0; i < 30; i++)
            {
                HeadLookKinematics.UpdateDampedAngles(ref state, _refFrame, Vector3.zero, 0.05f);
            }

            Assert.That(state.CurrentAngles.y, Is.EqualTo(0f).Within(0.1f));
            Assert.That(state.CurrentAngles.x, Is.EqualTo(0f).Within(0.1f));
        }
    }
}
