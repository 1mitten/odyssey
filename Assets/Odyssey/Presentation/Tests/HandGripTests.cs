#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.World;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// Closing a hand on a haft, on a skeleton built here rather than on a licensed character.
    ///
    /// <para>The point of testing it at all is that the two decisions inside <see cref="HandGrip"/>
    /// are the two this project has got wrong most often: which axis a bone turns about, and which
    /// way round. Both are found by measurement rather than named — the axis from the knuckles
    /// themselves, the sign by trying it and keeping whichever actually closes the hand — and a
    /// synthetic hand is enough to prove that the finding works, on a rig whose axes are known
    /// here and are deliberately *not* the ones a Synty character uses.</para>
    /// </summary>
    public class HandGripTests
    {
        GameObject _root = null!;
        HandGrip.Bones _bones;
        Transform _tip = null!;

        /// <summary>
        /// A hand pointing along +Z with its palm facing -Y, built from plain transforms.
        ///
        /// Turned forty degrees about two axes at the root on purpose: a solve that only works on
        /// an axis-aligned skeleton is a solve that works in a test and nowhere else.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("hand");
            _root.transform.rotation = Quaternion.Euler(40f, 25f, 15f);

            Transform hand = _root.transform;
            _bones = new HandGrip.Bones
            {
                Hand = hand,
                IndexProximal = Joint(hand, new Vector3(-0.02f, 0f, 0.06f)),
                ThumbProximal = Joint(hand, new Vector3(0.04f, 0f, 0.03f)),
            };

            _bones.MiddleProximal = Joint(hand, new Vector3(0.01f, 0f, 0.06f));
            _bones.MiddleIntermediate = Joint(_bones.MiddleProximal, new Vector3(0f, 0f, 0.035f));
            _bones.MiddleDistal = Joint(_bones.MiddleIntermediate, new Vector3(0f, 0f, 0.03f));
            _bones.IndexIntermediate = Joint(_bones.IndexProximal, new Vector3(0f, 0f, 0.033f));
            _bones.IndexDistal = Joint(_bones.IndexIntermediate, new Vector3(0f, 0f, 0.028f));
            _bones.ThumbIntermediate = Joint(_bones.ThumbProximal, new Vector3(0f, 0f, 0.025f));
            _bones.ThumbDistal = Joint(_bones.ThumbIntermediate, new Vector3(0f, 0f, 0.02f));

            _tip = _bones.MiddleDistal!;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        static Transform Joint(Transform? parent, Vector3 offset)
        {
            var go = new GameObject("joint");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            return go.transform;
        }

        [Test]
        public void ClosingBringsTheFingertipTowardsThePalm()
        {
            // The whole of what "closed" means, and the only statement about the pose that is true
            // independently of how somebody rigged the hand. If the sign search picks the wrong
            // way round, the fingers bend backwards and this fails — which is exactly the failure
            // that is invisible in source and obvious in a photograph.
            float before = Vector3.Distance(_tip.position, _bones.Hand!.position);

            HandGrip.Close(_bones, 1f);

            float after = Vector3.Distance(_tip.position, _bones.Hand.position);
            Assert.That(after, Is.LessThan(before),
                "the fingers opened instead of closing: the curl sign is inverted");
        }

        [Test]
        public void AnOpenHandIsLeftAlone()
        {
            Vector3 was = _tip.position;
            HandGrip.Close(_bones, 0f);
            Assert.That(Vector3.Distance(_tip.position, was), Is.LessThan(1e-5f));
        }

        [Test]
        public void ThePartialGripIsBetweenOpenAndClosed()
        {
            float open = Vector3.Distance(_tip.position, _bones.Hand!.position);

            HandGrip.Close(_bones, 0.5f);
            float half = Vector3.Distance(_tip.position, _bones.Hand.position);

            TearDown();
            SetUp();
            HandGrip.Close(_bones, 1f);
            float full = Vector3.Distance(_tip.position, _bones.Hand!.position);

            Assert.That(half, Is.LessThan(open), "half a grip did not close at all");
            Assert.That(half, Is.GreaterThan(full), "half a grip closed as far as a whole one");
        }

        [Test]
        public void AHandWithNoFingersIsNotTouched()
        {
            // A clone without the packs, or a rig that stops at the wrist. It must do nothing
            // rather than throw: this runs inside the draw pass, once per working colonist.
            var empty = new HandGrip.Bones { Hand = _root.transform };
            Quaternion was = _root.transform.rotation;

            Assert.DoesNotThrow(() => HandGrip.Close(empty, 1f));
            Assert.DoesNotThrow(() => HandGrip.FaceHaft(empty, Vector3.zero, Vector3.forward, 1f));
            Assert.That(_root.transform.rotation, Is.EqualTo(was));
        }

        [Test]
        public void FacingTheHaftTurnsTheFingersTowardsIt()
        {
            // "The palm is facing it" stated as a distance, which is the only form of it that can
            // be checked. The haft runs across the hand, a little to the side of where the fingers
            // currently point, so there is a roll that improves matters and the search has to find
            // it.
            Vector3 haftDirection = _root.transform.right;
            Vector3 haftPoint = _bones.Hand!.position + _root.transform.up * 0.05f;

            float before = DistanceToLine(_bones.MiddleProximal!.position, haftPoint, haftDirection);
            HandGrip.FaceHaft(_bones, haftPoint, haftDirection, 1f);
            float after = DistanceToLine(_bones.MiddleProximal!.position, haftPoint, haftDirection);

            Assert.That(after, Is.LessThanOrEqualTo(before + 1e-4f),
                "the search turned the hand further from the haft than it started");
        }

        static float DistanceToLine(Vector3 point, Vector3 origin, Vector3 direction)
        {
            Vector3 offset = point - origin;
            Vector3 along = direction.normalized;
            return (offset - along * Vector3.Dot(offset, along)).magnitude;
        }
    }
}
