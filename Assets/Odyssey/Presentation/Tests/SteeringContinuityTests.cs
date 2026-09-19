#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// **The sidestep must be a continuous function of everything it reads.**
    ///
    /// <para>The owner reported colonists that "vibrate quickly - as if it's fighting something or
    /// a indecision" (2026-09-19). Measured on a ticking colony of twenty over fifty seconds of
    /// play, the lateral offset moved by more than five centimetres in a single tick <b>85
    /// times</b>, the worst of them the full <c>0.600 m</c> envelope inside one sixtieth of a
    /// second. Every one of them came from a threshold: an oncoming test that switched the whole
    /// sidestep on at <c>dot &lt; -0.5</c>; a set of cell-sharing tests that forced the weight to
    /// 1.0 whatever the distance; a distance measured in x and z alone, so a colonist a storey up
    /// was nought metres away; and a choice between candidate offsets by whichever was longest,
    /// which swaps winner — and therefore sign — on any tick where two of them are close.</para>
    ///
    /// <para>These tests pin the shape rather than the numbers: each one fails if a threshold is
    /// put back. The kinematics are <see cref="SteeringCurveTests"/>, and what the sidestep is
    /// <i>for</i> is <see cref="PawnPassingTests"/> and <see cref="ObstacleSteeringTests"/>.</para>
    /// </summary>
    public class SteeringContinuityTests
    {
        static PawnView Walking(int id, CellRef cell, CellRef next, int perMille) =>
            new PawnView(new PawnId(id), cell, 100, 100, 50, -1, next, 0, movePerMille: perMille);

        static PawnView Standing(int id, CellRef cell) =>
            new PawnView(new PawnId(id), cell, 100, 100, 50, -1, cell, 0);

        static float Sidestep(in PawnView subject, params PawnView[] everyone)
        {
            PawnPose.Of(in subject, 0f, 0, out Vector3 heading, null, everyone, out Vector3 steer);
            return Vector3.Dot(steer, SteeringCurve.LateralRight(heading));
        }

        /// <summary>
        /// An oncoming colonist closing from several metres away moves the sidestep smoothly the
        /// whole way in. The hard 3.0 m cut-off this replaces put 0.6 m into one tick.
        /// </summary>
        [Test]
        public void AnApproachMovesTheSidestepSmoothlyTheWholeWayIn()
        {
            var mine = new CellRef(0, 0, 0);
            var ahead = new CellRef(0, 1, 0);
            var theirs = new CellRef(0, 2, 0);

            float previous = 0f;
            float worst = 0f;
            for (int perMille = 0; perMille <= 1000; perMille += 10)
            {
                // I walk north through my cell; they walk south towards me through theirs.
                PawnView me = Walking(1, mine, ahead, perMille);
                PawnView them = Walking(2, theirs, ahead, perMille);
                float now = Sidestep(in me, me, them);
                if (perMille > 0) worst = Mathf.Max(worst, Mathf.Abs(now - previous));
                previous = now;
            }

            // A hundredth of a step is about 2.5 cm of travel. Nothing should move the sidestep
            // further in a tick than the colonist moves.
            Assert.That(worst, Is.LessThan(0.03f),
                $"the sidestep jumped {worst:F3} m in a hundredth of a step - a threshold is back");
        }

        /// <summary>
        /// Two colonists bound for the same cell but still metres apart are given room in
        /// proportion to how close they are, not the whole envelope at once. This pins the deleted
        /// <c>sharingNext</c> override, which forced the weight to 1.0 regardless of distance.
        /// </summary>
        [Test]
        public void SharingADestinationDoesNotSnapTheSidestepToItsFullWidth()
        {
            var mine = new CellRef(0, 0, 0);
            var shared = new CellRef(0, 1, 0);
            var theirs = new CellRef(0, 2, 0);

            // Both barely into their steps, so the two of them are still nearly 5 m apart.
            PawnView me = Walking(1, mine, shared, 20);
            PawnView them = Walking(2, theirs, shared, 20);

            Assert.That(Sidestep(in me, me, them), Is.LessThan(0.10f),
                "a shared destination is not on its own a reason to leap sideways");
        }

        /// <summary>
        /// Distance is three-dimensional. On a board whose surface is 3 m terrace risers this is
        /// not an edge case: measured in x and z alone, a colonist on the terrace above was nought
        /// metres away and got the full sidestep.
        /// </summary>
        [Test]
        public void AColonistOnTheStoreyAboveIsNotInTheWay()
        {
            var mine = new CellRef(0, 0, 0);
            var ahead = new CellRef(0, 1, 0);

            PawnView me = Walking(1, mine, ahead, 500);
            PawnView upstairs = Standing(2, new CellRef(0, 1, 1));
            PawnView alongside = Standing(3, new CellRef(0, 1, 0));

            Assert.That(Sidestep(in me, me, upstairs), Is.EqualTo(0f).Within(1e-4f),
                "a colonist a storey up is three metres away, not none");
            Assert.That(Sidestep(in me, me, alongside), Is.GreaterThan(0.5f),
                "the same cell on my own storey is still very much in the way");
        }

        /// <summary>
        /// Crossing traffic is given room. It was ignored outright - the oncoming test wanted
        /// headings more than 120 degrees apart - and crossing is the case a player is most likely
        /// to be looking at, two colonists converging on one doorway.
        /// </summary>
        [Test]
        public void CrossingTrafficIsGivenRoomRatherThanIgnored()
        {
            var mine = new CellRef(1, 0, 0);
            var ahead = new CellRef(1, 1, 0);

            PawnView me = Walking(1, mine, ahead, 500);
            PawnView crossing = Walking(2, new CellRef(0, 1, 0), new CellRef(1, 1, 0), 500);

            Assert.That(Sidestep(in me, me, crossing), Is.GreaterThan(0.05f),
                "a colonist crossing my path is in the way even though we are not head-on");
        }

        /// <summary>
        /// The steer handed back is exactly what the pose added, so the figure director can take
        /// it out again and solve the gait from walking rather than from swerving.
        /// </summary>
        [Test]
        public void TheSteerHandedBackIsExactlyWhatThePoseAdded()
        {
            var mine = new CellRef(0, 0, 0);
            var ahead = new CellRef(0, 1, 0);
            PawnView me = Walking(1, mine, ahead, 500);
            PawnView blocking = Standing(2, ahead);
            var both = new[] { me, blocking };

            Vector3 steered = PawnPose.Of(in me, 0f, 0, out _, null, both, out Vector3 steer);
            Vector3 alone = PawnPose.Of(in me, 0f, 0, out _, null);

            Assert.That(steer.sqrMagnitude, Is.GreaterThan(0.01f), "this fixture should be steering");
            Assert.That(Vector3.Distance(steered - steer, alone), Is.LessThan(1e-4f),
                "position minus steer must be the pose the colonist would have walked unimpeded");
        }

        /// <summary>
        /// Offsets from several sources add and are then clamped, rather than the longest winning.
        /// A winner can be swapped by a twitch in a distance, and swapping between two candidates
        /// that point opposite ways moves the figure the full width of the envelope and back,
        /// which is the vibration itself.
        /// </summary>
        [Test]
        public void OffsetsFromSeveralSourcesNeverExceedTheHardClamp()
        {
            var mine = new CellRef(1, 1, 0);
            var ahead = new CellRef(1, 2, 0);

            PawnView me = Walking(1, mine, ahead, 500);
            var crowd = new[]
            {
                me,
                Standing(2, ahead),
                Standing(3, new CellRef(0, 2, 0)),
                Standing(4, new CellRef(2, 2, 0)),
                Standing(5, new CellRef(1, 1, 0)),
            };

            Assert.That(Mathf.Abs(Sidestep(in me, crowd)),
                Is.LessThanOrEqualTo(SteeringCurve.HardClampedMax + 1e-4f),
                "a crowd on every side must not push the figure out of its own tile");
        }
    }
}
