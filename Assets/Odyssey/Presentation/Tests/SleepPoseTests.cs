#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
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
        /// <summary>
        /// A colonist's drawn height, sole to crown, which is what a sleeper is now laid down by.
        ///
        /// <para><b>Measured, not chosen</b> — <c>scripts/unity.sh exec
        /// Odyssey.EditorTools.SleepProbe.Run</c>, 2026-09-20: the body skin of every character in
        /// the cast runs 0.000 m to 2.488 m at the catalogue's 1.4 scale, with the hair a couple of
        /// centimetres above that. The fixture used to hold a 0.95 m <c>Hip</c> here, which is a
        /// perfectly reasonable hip for a person and was never the number the director passed.</para>
        /// </summary>
        const float Body = 2.49f;

        /// <summary>Laid with its head at the origin, so every assertion reads against a known point.</summary>
        static void Place(int pawnId, Vector3 along, float surfaceY, out Vector3 position, out Quaternion rotation) =>
            SleepPose.Place(
                SleepPose.PostureFor(pawnId), headAt: Vector3.zero, along, surfaceY, Body,
                alongSlope: 0f, weight: 1f,
                standingPosition: Vector3.zero, standingRotation: Quaternion.identity,
                out position, out rotation);

        /// <summary>
        /// The head ends up at the far end of the bed from the root — on the pillow, which is the
        /// one part of this a player will actually check.
        /// </summary>
        /// <summary>
        /// <b>The head lands where it was asked to, and the feet follow.</b>
        ///
        /// <para>The point of the whole placement: the head is the end that has to be exact,
        /// because it goes on the pillow. Centring the body on the bed instead left the head two
        /// thirds of a metre short of it, adrift in the middle of the mattress — which is what the
        /// owner photographed.</para>
        /// </summary>
        [Test]
        public void TheHeadLandsWhereItIsAskedForAndTheFeetFollow()
        {
            Place(1, Vector3.forward, 0.7f, out Vector3 position, out Quaternion rotation);

            // The root is the feet, and the body extends behind it once laid back.
            Vector3 head = position + rotation * Vector3.up * SleepPose.BodyLength(Body);

            Assert.That(head.x, Is.EqualTo(0f).Within(0.01f), "the head is where it was placed");
            Assert.That(head.z, Is.EqualTo(0f).Within(0.01f));
            Assert.That(position.z, Is.EqualTo(SleepPose.BodyLength(Body)).Within(0.01f),
                "and the feet are one body-length along from it");
            Assert.That(head.y, Is.EqualTo(position.y).Within(0.05f),
                "a sleeper is level: the head is not propped up or buried");
        }

        /// <summary>
        /// A side sleeper floats on its <b>width</b>, not its thickness, so it does not sink into
        /// the mattress it is lying on (owner, 2026-09-18: "sunk").
        /// </summary>
        [Test]
        public void ASideSleeperIsLiftedByItsWidthRatherThanItsThickness()
        {
            SleepPose.Posture back = SleepPose.Postures[0];
            SleepPose.Posture side = SleepPose.Postures[2];

            Assume.That(back.Roll, Is.EqualTo(0f), "the first posture is the flat one");
            Assume.That(Mathf.Abs(side.Roll), Is.GreaterThan(45f), "the third is on its side");

            Assert.That(SleepPose.Lift(side, Body), Is.GreaterThan(SleepPose.Lift(back, Body)),
                "a body on its side needs more clearance than one on its back");
            Assert.That(SleepPose.Lift(back, Body),
                Is.EqualTo(Body * SleepPose.ThicknessPerBody).Within(0.001f),
                "and one on its back needs exactly half its thickness");

            foreach (SleepPose.Posture posture in SleepPose.Postures)
                Assert.That(SleepPose.Lift(posture, Body), Is.GreaterThan(0f),
                    $"{posture.Name} would lie inside whatever it is on");

            // And enough of it to matter. The old lift was a fraction of a 0.2 m clamp - 32 mm on
            // a colonist whose torso is a fifth of a metre thick - so the whole body lay inside
            // the bedding. A tenth of a metre is well under anything anybody would tune to and
            // well over what a collapsed measurement can produce.
            Assert.That(SleepPose.Lift(back, Body), Is.GreaterThan(0.1f),
                "a sleeper this flat is inside the mattress rather than on it");
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
            Assert.That(onBed.y - 0.7f, Is.LessThan(Body * 0.25f), "and lying on it, not floating");
            Assert.That(onBed.y - onFloor.y, Is.EqualTo(0.7f).Within(0.001f),
                "the two differ by exactly the height of the bed");
        }

        /// <summary>
        /// <b>A sleeper laid in a bed is inside the bed's own two cells, head to foot</b> (owner,
        /// 2026-09-20: <i>"the bed spans two tiles and the body needs rest within those tiles and
        /// not off them"</i>).
        ///
        /// <para><b>Asserted rather than reasoned.</b> The arithmetic does say so — the head goes
        /// on the pillow at −1.55 m and a 0.95 m hip gives a 1.81 m body, so the feet land at
        /// +0.26 m of a footprint running ±2.50 m — and reasoning it through is exactly what was
        /// done when the owner reported sleepers hanging off beds. It was right, and the real
        /// fault was elsewhere (the scenario's phantom bed cells, <c>20-beds.md</c> §7a). A
        /// measurement that had been written down would have said so in a second instead of an
        /// hour, which is the whole argument for this test.</para>
        ///
        /// <para><b>And it failed for a second time, the same day it was written</b> (owner,
        /// 2026-09-20: <i>"colonists are resting in the centre and hanging off the bed and
        /// sometimes even off the bed"</i>). The test above was not wrong; it was asked the wrong
        /// question. It took a range of plausible <i>hip heights</i> and checked the body each one
        /// implies, and every one of them fitted — while the number the director actually passed
        /// was 0.2 m, the floor of a clamp on a measurement that returns nought on every rig in
        /// the cast. A range that starts at a hip of 0.70 m can never reach it. So this walks the
        /// <b>body length</b> instead, which is the quantity the placement is a function of, and
        /// it walks it from a length far too short to a length far too long — because the fault
        /// both times was a body of the wrong size, and a bound on only one side would have caught
        /// only one of them. <c>docs/design/20-beds.md</c> §7b.</para>
        ///
        /// <para><b>Over a range of builds, not one.</b> Sixty-one characters have sixty-one sets
        /// of proportions and the director scales them besides. The cast measures 2.49 m
        /// (<see cref="Body"/>); from 1.5 m to 3.2 m — well either side of anything in the packs —
        /// the sleeper stays on the bed.</para>
        /// </summary>
        [Test]
        public void ASleeperLiesWithinTheBedsOwnTwoCells()
        {
            // The bed's own numbers, in bed-local Z: the footprint is two cells centred on the
            // origin, and the head rests on the pillow.
            float halfSpan = CellMetrics.SizeXZ;
            float head = BedShape.HeadRestAlong;

            foreach (float body in new[] { 1.5f, 2.0f, Body, 2.8f, 3.2f })
            {
                float feet = head + SleepPose.BodyLength(body);

                Assert.That(head, Is.GreaterThanOrEqualTo(-halfSpan),
                    $"at a body of {body:0.00} m the head is off the head end of the bed");
                Assert.That(feet, Is.LessThanOrEqualTo(halfSpan),
                    $"at a body of {body:0.00} m the feet are {feet - halfSpan:0.00} m past the foot end");
            }
        }

        /// <summary>
        /// <b>And a body length that is not one puts the sleeper back on the bed rather than on
        /// the floor beside it.</b>
        ///
        /// <para>The guard is the point. A measurement can collapse — this one did, to the 0.2 m
        /// floor of its own clamp — and the failure was silent, because 0.38 m is a number and
        /// every line of arithmetic downstream of it went on working.
        /// <see cref="SleepPose.BodyLength"/> refuses anything that is plainly not a person and
        /// lays a colonist at the drawn scale instead, so the worst a broken rig can now do is
        /// make one character the wrong size rather than hang every colonist in the colony off the
        /// end of a bed.</para>
        /// </summary>
        [Test]
        public void ABodyLengthThatIsNotOneFallsBackOnAColonist()
        {
            float halfSpan = CellMetrics.SizeXZ;

            foreach (float nonsense in new[] { 0f, 0.2f, 0.38f, -1f, 12f, float.NaN })
            {
                float length = SleepPose.BodyLength(nonsense);

                Assert.That(length, Is.GreaterThan(1f).And.LessThan(5f),
                    $"{nonsense} was taken for a body length");
                Assert.That(BedShape.HeadRestAlong + length, Is.LessThanOrEqualTo(halfSpan),
                    $"the fallback for {nonsense} still hangs off the foot of the bed");
            }

            Assert.That(SleepPose.BodyLength(Body), Is.EqualTo(Body).Within(0.001f),
                "and a real measurement is passed through untouched");
        }

        /// <summary>
        /// And the placement agrees with the arithmetic above, through the same call the figure
        /// director makes — so a change to <c>Place</c> cannot pass the span check while moving
        /// the body.
        /// </summary>
        [Test]
        public void ThePlacedBodyIsWhereTheSpanCheckSaysItIs()
        {
            var headAt = new Vector3(0f, 0f, BedShape.HeadRestAlong);
            SleepPose.Place(
                SleepPose.PostureFor(1), headAt, Vector3.forward, surfaceY: BedShape.MattressTop,
                Body, alongSlope: 0f, weight: 1f,
                standingPosition: Vector3.zero, standingRotation: Quaternion.identity,
                out Vector3 feet, out _);

            Assert.That(feet.z, Is.EqualTo(BedShape.HeadRestAlong + SleepPose.BodyLength(Body)).Within(0.001f));
            Assert.That(feet.z, Is.LessThanOrEqualTo(CellMetrics.SizeXZ),
                "the feet are off the end of the bed");
        }

        /// <summary>
        /// Flat on a level bed. Whatever the posture, a sleeper's body lies along the bed: the "up"
        /// it stands on becomes a direction along the mattress, which is the whole of what lying
        /// down is.
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
        /// <b>And along the bed rather than level across it, when the bed is not level.</b>
        ///
        /// <para>Everything fixed to the grid is draped: <c>BedShape.Root</c> shears a bed's 4.6 m
        /// along the ground's tangent plane, while the body used to be laid flat on one sampled
        /// height. At the relief's steepest — 2.0 m over a 150 m period, 0.136 rise per metre —
        /// that is 0.21 m of disagreement at the pillow, so a colonist on a slope was buried in the
        /// mattress at one end and floating above it at the other.</para>
        ///
        /// <para>Asserted as the two halves of one plane: the body's own axis takes the gradient it
        /// was given, and the feet end up that much higher than the head. Either alone would pass
        /// while the sleeper hovered over a bed she was parallel to.</para>
        /// </summary>
        [Test]
        public void ASleeperOnASlopeLiesAlongItRatherThanLevelAcrossIt()
        {
            foreach (float slope in new[] { -0.136f, -0.05f, 0.05f, 0.136f })
            {
                SleepPose.Place(
                    SleepPose.Postures[0], headAt: Vector3.zero, along: Vector3.forward,
                    surfaceY: 0f, bodyLength: Body, alongSlope: slope, weight: 1f,
                    standingPosition: Vector3.zero, standingRotation: Quaternion.identity,
                    out Vector3 feet, out Quaternion rotation);

                // The spine runs feet to head, so it descends where the bed rises: the gradient
                // it carries is the bed's, negated.
                Vector3 spine = rotation * Vector3.up;
                float gradient = -spine.y / new Vector2(spine.x, spine.z).magnitude;
                Assert.That(gradient, Is.EqualTo(slope).Within(0.005f),
                    $"at a slope of {slope:0.000} the body lies at {gradient:0.000}");

                // And it is on the plane, not merely parallel to it: the feet stand one
                // body-length's worth of rise above the head end.
                float lift = SleepPose.Lift(SleepPose.Postures[0], Body);
                Assert.That(feet.y - lift, Is.EqualTo(slope * SleepPose.BodyLength(Body)).Within(0.01f),
                    $"at a slope of {slope:0.000} the feet are off the mattress");
            }
        }

        /// <summary>
        /// A level bed is exactly what it always was. The slope arrived as a new argument to
        /// <see cref="SleepPose.Place"/>, and the cheapest way for it to go wrong is to change the
        /// answer when it is nought.
        /// </summary>
        [Test]
        public void NoSlopeIsTheLevelPlacementUntouched()
        {
            foreach (SleepPose.Posture posture in SleepPose.Postures)
            {
                SleepPose.Place(
                    posture, headAt: Vector3.zero, along: Vector3.forward, surfaceY: 0.7f,
                    bodyLength: Body, alongSlope: 0f, weight: 1f,
                    standingPosition: Vector3.zero, standingRotation: Quaternion.identity,
                    out Vector3 feet, out Quaternion rotation);

                Assert.That(feet.y, Is.EqualTo(0.7f + SleepPose.Lift(posture, Body)).Within(0.001f),
                    $"{posture.Name} does not rest on a level mattress");
                Assert.That(feet.z, Is.EqualTo(SleepPose.BodyLength(Body)).Within(0.001f),
                    $"{posture.Name} is not one body-length along");
                Assert.That(Mathf.Abs((rotation * Vector3.up).y), Is.LessThan(0.08f),
                    $"{posture.Name} is not level on a level bed");
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
        /// <b>No supine posture pitches an arm downward.</b>
        ///
        /// <para>A pitch here is taken about the figure's own lateral axis, so on a sleeper lying
        /// on its back a <i>positive</i> arm angle swings the arm down, through the mattress, and a
        /// negative one raises it. The first cut of the table had it the other way about: "back,
        /// arms up" was authored at +118° with a +58° elbow and drove both forearms 0.54 m through
        /// the bedding and out past the head of the bed, on a quarter of the colony, for two days.
        /// Nobody could see it while the whole colonist was still hanging off the end of the
        /// bed.</para>
        ///
        /// <para><b>This is a guard against the sign, not a check of the pose.</b> Whether a
        /// posture reads as somebody asleep is a look and belongs to the owner and to
        /// <c>scripts/unity.sh exec Odyssey.EditorTools.SleepProbe.Run</c>, which prints every
        /// posture's clearance against a real rig. What a test can hold is the direction: measured
        /// across the whole arc, a supine arm is clear of the bedding from about −170° up to
        /// about +10° and through it beyond that, so anything authored past +10° is the sign
        /// mistake coming back. Rolled postures are exempt because a body on its side presents a
        /// different plane and both of ours were measured in the same pass and were already
        /// right.</para>
        /// </summary>
        [Test]
        public void NoSupinePostureSwingsAnArmIntoTheBedding()
        {
            foreach (SleepPose.Posture posture in SleepPose.Postures)
            {
                if (Mathf.Abs(posture.Roll) > 20f) continue; // on its side; a different plane

                Assert.That(posture.RightArm, Is.LessThanOrEqualTo(10f),
                    $"{posture.Name} pitches its right arm down through the mattress");
                Assert.That(posture.LeftArm, Is.LessThanOrEqualTo(10f),
                    $"{posture.Name} pitches its left arm down through the mattress");
            }
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
                SleepPose.PostureFor(1), headAt: Vector3.zero, along: Vector3.forward, surfaceY: 0f,
                bodyLength: Body, alongSlope: 0f, weight: 0f,
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
