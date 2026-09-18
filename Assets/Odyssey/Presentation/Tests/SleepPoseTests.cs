#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.World;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// A colonist laid down, as arithmetic — testable without a rig, a world or an animator, the
    /// same bargain <see cref="SwimPose"/> and <see cref="ClimbPose"/> make.
    ///
    /// <para><b>The fault this exists to prevent is not a wrong angle; it is no angle at all.</b>
    /// Nothing in presentation knew a pawn could be asleep, so a colonist who had walked to a bed
    /// and gone to sleep in it was drawn standing bolt upright in it — and the owner, watching
    /// that, reported that colonists would not use the beds and stood outside instead. The
    /// simulation was right the whole time. A pose that does not exist is indistinguishable from a
    /// simulation that does not work.</para>
    /// </summary>
    public class SleepPoseTests
    {
        const float Hip = 0.95f;

        static void Place(int pawnId, Vector3 along, float surfaceY, out Vector3 position, out Quaternion rotation) =>
            SleepPose.Place(
                SleepPose.PostureFor(pawnId), Vector3.zero, along, surfaceY, Hip, weight: 1f,
                standingPosition: Vector3.zero, standingRotation: Quaternion.identity,
                out position, out rotation);

        /// <summary>
        /// The head ends up at the far end of the bed from the root — on the pillow, which is the
        /// one part of this a player will actually check.
        /// </summary>
        [Test]
        public void TheHeadLiesAtTheFarEndFromTheFeet()
        {
            Place(1, Vector3.forward, 0.7f, out Vector3 position, out Quaternion rotation);

            // The root is the feet, and the body extends behind it once laid back.
            Vector3 head = position + rotation * Vector3.up * SleepPose.BodyLength(Hip);

            Assert.That(position.z, Is.GreaterThan(0f), "the feet are at the foot end");
            Assert.That(head.z, Is.LessThan(0f), "and the head at the other one");
            Assert.That(head.y, Is.EqualTo(position.y).Within(0.05f),
                "a sleeper is level: the head is not propped up or buried");
        }

        /// <summary>The body lies along the bed, not across it — so a turned bed turns its sleeper.</summary>
        [Test]
        public void TheBodyLiesAlongWhicheverWayItIsGiven()
        {
            Place(1, Vector3.forward, 0f, out Vector3 northFeet, out _);
            Place(1, Vector3.right, 0f, out Vector3 eastFeet, out _);

            Assert.That(Mathf.Abs(northFeet.z), Is.GreaterThan(Mathf.Abs(northFeet.x)),
                "lying north-south, the feet are offset in Z");
            Assert.That(Mathf.Abs(eastFeet.x), Is.GreaterThan(Mathf.Abs(eastFeet.z)),
                "lying east-west, in X");
        }

        /// <summary>
        /// The body rests on the surface it was given — a mattress top, or the ground — rather than
        /// sinking into it or hovering over it.
        /// </summary>
        [Test]
        public void TheBodyRestsOnTheSurfaceItWasGiven()
        {
            Place(1, Vector3.forward, 0.7f, out Vector3 onBed, out _);
            Place(1, Vector3.forward, 0f, out Vector3 onFloor, out _);

            Assert.That(onBed.y, Is.GreaterThan(0.7f), "above the mattress, not in it");
            Assert.That(onBed.y - 0.7f, Is.LessThan(Hip * 0.5f), "and lying on it, not floating");
            Assert.That(onBed.y - onFloor.y, Is.EqualTo(0.7f).Within(0.001f),
                "the two differ by exactly the height of the bed");
        }

        /// <summary>
        /// Flat. Whatever the posture, a sleeper's body is horizontal: the "up" it stands on
        /// becomes a direction along the bed, which is the whole of what lying down is.
        /// </summary>
        [Test]
        public void EveryPostureLiesFlat()
        {
            for (int id = 1; id <= 24; id++)
            {
                Place(id, Vector3.forward, 0f, out _, out Quaternion rotation);
                Vector3 spine = rotation * Vector3.up;
                Assert.That(Mathf.Abs(spine.y), Is.LessThan(0.08f),
                    $"pawn {id} ({SleepPose.PostureFor(id).Name}) is not lying flat");
            }
        }

        /// <summary>
        /// A colonist takes the same posture every night, and colonists next to each other do not
        /// all take the same one. Both halves matter: the first is what stops a sleeper twitching
        /// between shapes, the second is what stops a barracks reading as stamped.
        /// </summary>
        [Test]
        public void APostureIsStablePerColonistAndVariesBetweenThem()
        {
            for (int id = 1; id <= 50; id++)
                Assert.That(SleepPose.PostureFor(id).Name, Is.EqualTo(SleepPose.PostureFor(id).Name),
                    "a posture is a pure function of the id");

            // Consecutive ids, because that is what a colony actually has, and the bottom two
            // bits are all the posture choice ever reads. The first hash here failed this with
            // every colonist in posture three.
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int id = 1; id <= 12; id++) seen.Add(SleepPose.PostureFor(id).Name);

            Assert.That(seen.Count, Is.EqualTo(SleepPose.Postures.Length),
                "all four postures must show up among a dozen neighbouring colonists");
        }

        /// <summary>
        /// Weight zero is the standing pose untouched, so a colonist who is not asleep is not
        /// nudged by any of this — and the ease has somewhere honest to start from.
        /// </summary>
        [Test]
        public void NoWeightLeavesAStandingFigureAlone()
        {
            var standing = new Vector3(3f, 1f, 4f);
            Quaternion upright = Quaternion.Euler(0f, 37f, 0f);

            SleepPose.Place(
                SleepPose.PostureFor(1), Vector3.zero, Vector3.forward, 0f, Hip, weight: 0f,
                standing, upright, out Vector3 position, out Quaternion rotation);

            Assert.That(position, Is.EqualTo(standing));
            Assert.That(Quaternion.Angle(rotation, upright), Is.LessThan(0.01f));
        }

        /// <summary>
        /// <b>A sleeper is a position, not a motion</b> (owner, 2026-09-18: "when they are sleeping
        /// - they should be static and not animated. Still in that position").
        ///
        /// <para>Asked of the arithmetic rather than of a picture: the placement is a pure function
        /// of the posture, the bed and the weight, so the same figure asked twice gets the identical
        /// answer and there is nothing in here for a clock to drive. The other half of the claim —
        /// that the idle clip underneath is held on one frame — lives in
        /// <c>PawnFigureDirector.Evaluate</c> and needs a running rig to see.</para>
        /// </summary>
        [Test]
        public void ASleeperIsAPositionAndNotAMotion()
        {
            Place(1, Vector3.forward, 0.7f, out Vector3 first, out Quaternion firstTurn);
            Place(1, Vector3.forward, 0.7f, out Vector3 again, out Quaternion againTurn);

            Assert.That(again, Is.EqualTo(first), "the same sleeper twice is in the same place");
            Assert.That(Quaternion.Angle(againTurn, firstTurn), Is.EqualTo(0f).Within(0.0001f));

            // Nothing in the type takes a time, which is the structural half of "not animated":
            // a pose with no clock cannot drift however long it is held.
            foreach (var member in typeof(SleepPose).GetMembers())
                Assert.That(member.Name, Does.Not.Contain("Breath"),
                    "breathing was removed; a sleeper does not move at all");
        }
    }
}
