#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.World;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The solve that puts a colonist's second fist on the haft of the axe her first fist is
    /// holding.
    ///
    /// Everything it can get wrong is silent. A hand that lands a few centimetres off the haft
    /// reads as a person holding an axe badly; an elbow that folds the wrong way reads as a broken
    /// person; a chain that drifts because each frame solves from the last one's answer reads as a
    /// person slowly turning inside out. None of them throws, and at board-camera distance the
    /// first is invisible until somebody zooms in, by which time the numbers it was tuned against
    /// have moved on.
    /// </summary>
    public class TwoBoneIkTests
    {
        GameObject _root = null!;
        Transform _shoulder = null!;
        Transform _elbow = null!;
        Transform _hand = null!;

        /// <summary>A left arm at roughly a colonist's proportions: 30 cm and 25 cm, hanging down.</summary>
        [SetUp]
        public void BuildAnArm()
        {
            _root = new GameObject("arm");
            _shoulder = _root.transform;
            _elbow = new GameObject("elbow").transform;
            _hand = new GameObject("hand").transform;

            _elbow.SetParent(_shoulder);
            _hand.SetParent(_elbow);

            _shoulder.position = new Vector3(0f, 1.5f, 0f);
            _elbow.position = new Vector3(0f, 1.2f, 0f);
            _hand.position = new Vector3(0f, 0.95f, 0f);
        }

        [TearDown]
        public void Clear() => Object.DestroyImmediate(_root);

        float Span => Vector3.Distance(_shoulder.position, _elbow.position)
                      + Vector3.Distance(_elbow.position, _hand.position);

        [Test]
        public void TheHandArrivesOnTheTarget()
        {
            var target = new Vector3(0.25f, 1.25f, 0.35f);
            TwoBoneIk.Reach(_shoulder, _elbow, _hand, target, _shoulder.position - Vector3.forward);

            Assert.That(Vector3.Distance(_hand.position, target), Is.LessThan(0.01f));
        }

        [Test]
        public void TheArmStillBendsAtTheElbow()
        {
            // Reaching a point well inside its own span, the arm must fold rather than stretch at
            // it. An arm that arrives straight has had its shoulder angle solved as zero, which is
            // what a law-of-cosines term with the wrong sign produces.
            var target = _shoulder.position + new Vector3(0.2f, 0f, 0.1f);
            TwoBoneIk.Reach(_shoulder, _elbow, _hand, target, _shoulder.position - Vector3.forward);

            float straightness = Vector3.Angle(_elbow.position - _shoulder.position,
                _hand.position - _elbow.position);
            Assert.That(straightness, Is.GreaterThan(20f), "the elbow is locked straight");
        }

        [Test]
        public void ATargetOutOfReachStraightensTheArmTowardsIt()
        {
            // Not an error and not reported as one: an arm that cannot get there points at it and
            // stops, which is what an arm does. Throwing, or refusing to pose, would leave the
            // off hand in whatever the idle had it doing.
            Vector3 target = _shoulder.position + Vector3.forward * (Span * 3f);
            TwoBoneIk.Reach(_shoulder, _elbow, _hand, target, _shoulder.position - Vector3.up);

            Vector3 arm = _hand.position - _shoulder.position;
            Assert.That(Vector3.Angle(arm, target - _shoulder.position), Is.LessThan(5f));
            Assert.That(arm.magnitude, Is.EqualTo(Span).Within(0.02f));
        }

        [Test]
        public void SolvingTwiceGivesTheSameAnswer()
        {
            // The solve reads the arm's current pose to work from, so a small error would compound
            // frame on frame. Two identical calls landing in two different places is that error;
            // it would show up as a figure whose arm crept as long as it held anything.
            var target = new Vector3(0.2f, 1.3f, 0.3f);
            var hint = _shoulder.position - Vector3.forward;

            TwoBoneIk.Reach(_shoulder, _elbow, _hand, target, hint);
            Vector3 once = _hand.position;
            Quaternion elbowOnce = _elbow.rotation;

            TwoBoneIk.Reach(_shoulder, _elbow, _hand, target, hint);

            Assert.That(Vector3.Distance(_hand.position, once), Is.LessThan(0.005f));
            Assert.That(Quaternion.Angle(_elbow.rotation, elbowOnce), Is.LessThan(2f));
        }

        [Test]
        public void TheElbowGoesTowardsThePoleAndNotThroughTheChest()
        {
            // The fault this solve shipped with. Reaching across the body, the elbow was sent to
            // the far side of the shoulder-to-target line and folded through the ribs; which side
            // that is depends on a handedness convention, so the solve now tries both and keeps
            // whichever is actually nearer the pole. Stated as a property, it is simply: the elbow
            // ends up on the pole's side of the line.
            var target = new Vector3(0.3f, 1.3f, 0.25f);
            var pole = _shoulder.position + new Vector3(-0.9f, -0.7f, 0f);

            TwoBoneIk.Reach(_shoulder, _elbow, _hand, target, pole);

            Vector3 line = (target - _shoulder.position).normalized;
            Vector3 toElbow = Vector3.ProjectOnPlane(_elbow.position - _shoulder.position, line);
            Vector3 toPole = Vector3.ProjectOnPlane(pole - _shoulder.position, line);

            Assert.That(Vector3.Dot(toElbow.normalized, toPole.normalized), Is.GreaterThan(0f),
                "the elbow came out on the far side from its pole, which is through the body");
        }

        [Test]
        public void ThePoleHintDecidesWhichWayTheElbowPoints()
        {
            // The one degree of freedom the law of cosines leaves open. Without a hint the arm
            // spins about the shoulder-to-target line to wherever the last pose left it, and an
            // elbow that flicks from one side to the other between frames is the most visible
            // thing a figure can do.
            var target = new Vector3(0f, 1.3f, 0.3f);

            TwoBoneIk.Reach(_shoulder, _elbow, _hand, target, _shoulder.position + Vector3.right);
            Vector3 fromOneSide = _elbow.position;

            BuildAgain();
            TwoBoneIk.Reach(_shoulder, _elbow, _hand, target, _shoulder.position - Vector3.right);
            Vector3 fromTheOther = _elbow.position;

            Assert.That(Vector3.Distance(fromOneSide, fromTheOther), Is.GreaterThan(0.05f));
        }

        void BuildAgain()
        {
            Object.DestroyImmediate(_root);
            BuildAnArm();
        }
    }
}
