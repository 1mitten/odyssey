#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// How a figure answers the ground it is drawn on.
    ///
    /// <para>GroundRelief lifts everything that stands on the board and shears only the board
    /// itself — "a person standing on a hillside stands up". That is right about a position and
    /// was never the whole answer: lifted onto a slope and left bolt upright, a figure meets it on
    /// one heel with the downhill foot in the air and the uphill one buried to the ankle.</para>
    ///
    /// <para>Everything here is arithmetic on a slope, a height or a pair of numbers, which is the
    /// point: whether a colonist looks right on a hillside is a judgement for a screenshot, but
    /// whether the foot is on the surface is a number, and numbers get tests.</para>
    /// </summary>
    public class FootingTests
    {
        [SetUp]
        public void Setup()
        {
            Footing.Reset();
            GroundRelief.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            Footing.Reset();
            GroundRelief.Reset();
        }

        [Test]
        public void FlatGroundAsksForNoLeanAtAll()
        {
            // The control that matters most: the bare board and the barren test map are flat, and
            // every golden, screenshot and frame-time figure on record was taken on one. A lean
            // that was not exactly zero there would move all of them for no reason.
            Vector3 normal = Footing.GroundNormal(0f, 0f);
            Assert.That(normal, Is.EqualTo(Vector3.up).Using(new Vec3Comparer()));
            Assert.That(Quaternion.Angle(Footing.LeanTo(normal), Quaternion.identity),
                Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void TheNormalLeansAwayFromTheRise()
        {
            // Ground rising towards +x has a normal tipped towards -x. Getting this backwards
            // produces a figure leaning into the hill instead of standing on it, which looks
            // deliberate and is the kind of sign error that survives a screenshot.
            Vector3 normal = Footing.GroundNormal(0.5f, 0f);
            Assert.That(normal.x, Is.LessThan(0f), "the normal tipped into the slope rather than off it");
            Assert.That(normal.y, Is.GreaterThan(0f), "the normal is not pointing up at all");
            Assert.That(normal.magnitude, Is.EqualTo(1f).Within(1e-4f));

            Vector3 other = Footing.GroundNormal(0f, 0.5f);
            Assert.That(other.z, Is.LessThan(0f));
        }

        [Test]
        public void AFigureTakesSomeOfTheSlopeButNeverAllOfIt()
        {
            // A person on a hillside holds their head up and their weight over their feet; only
            // the ankles really follow the ground. Rotated the full amount the figure reads as a
            // cut-out pasted onto the hill, which is the fault arriving from the other side.
            Vector3 normal = Footing.GroundNormal(0.2f, 0f);
            float ground = Vector3.Angle(Vector3.up, normal);
            float lean = Quaternion.Angle(Quaternion.identity, Footing.LeanTo(normal));

            Assert.That(ground, Is.GreaterThan(1f), "the test slope is too gentle to measure");
            Assert.That(lean, Is.LessThan(ground), "the figure lies along the slope like the ground does");
            Assert.That(lean, Is.GreaterThan(0f), "the figure ignores the slope entirely");
            Assert.That(lean, Is.EqualTo(ground * Footing.LeanFraction).Within(0.5f));
        }

        [Test]
        public void TheLeanIsCappedHoweverSteepTheGroundGets()
        {
            // The board's own field cannot reach the cap — 2 m over 150 m is 7.7 degrees at its
            // steepest — but the surround's hills are far steeper, and a cap that is not currently
            // binding is still what stops a future tuning pass laying a colonist on its side.
            foreach (float slope in new[] { 1f, 4f, 20f })
            {
                float lean = Quaternion.Angle(Quaternion.identity,
                    Footing.LeanTo(Footing.GroundNormal(slope, slope)));
                Assert.That(lean, Is.LessThanOrEqualTo(Footing.MaxLeanDegrees + 1e-2f),
                    $"a slope of {slope} leaned the figure {lean} degrees");
            }
        }

        [Test]
        public void TheBoardsOwnSteepestSlopeIsAModestLean()
        {
            // Measured against the real field rather than an invented one, because the number that
            // matters is the one the player sees. MaxSlope bounds every wave's contribution, so
            // this is the worst the shipped board can do anywhere on it.
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;
            float steepest = GroundRelief.MaxSlope(GroundRelief.Amplitude);

            Assert.That(steepest, Is.GreaterThan(0.1f).And.LessThan(0.2f),
                "the board's slope is no longer what the lean was sized against");

            float lean = Quaternion.Angle(Quaternion.identity,
                Footing.LeanTo(Footing.GroundNormal(steepest, 0f)));
            Assert.That(lean, Is.GreaterThan(1f), "the lean is too small to see on the steepest ground there is");
            Assert.That(lean, Is.LessThan(Footing.MaxLeanDegrees), "the board alone saturates the cap");
        }

        [Test]
        public void TheLeanEasesRatherThanSnapping()
        {
            // A pawn crossing a ridge would otherwise change attitude between one frame and the
            // next, which at this camera height reads as the figure flinching. Same argument as
            // TurnDegreesPerSecond, and the same fix.
            Quaternion target = Footing.LeanTo(Footing.GroundNormal(0.4f, 0f));
            Footing.LeanDegreesPerSecond = 60f;

            Quaternion one = Footing.Settle(Quaternion.identity, target, 1f / 60f);
            Assert.That(Quaternion.Angle(Quaternion.identity, one), Is.LessThan(Quaternion.Angle(Quaternion.identity, target)),
                "a single frame arrived at the whole lean");

            Quaternion at = Quaternion.identity;
            for (int frame = 0; frame < 600; frame++) at = Footing.Settle(at, target, 1f / 60f);
            Assert.That(Quaternion.Angle(at, target), Is.LessThan(0.1f), "the lean never gets there");
        }

        [Test]
        public void AFootOnTheGroundIsLeftAlone()
        {
            Assert.That(Footing.Correction(4f, 4f), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void AFootNearTheGroundIsMovedOntoItExactly()
        {
            // Within half the reach the correction is the whole gap, so a foot that should be
            // planted is planted rather than approximately planted.
            float gap = Footing.ReachMetres * 0.25f;
            Assert.That(Footing.Correction(4f, 4f + gap), Is.EqualTo(gap).Within(1e-5f));
            Assert.That(Footing.Correction(4f, 4f - gap), Is.EqualTo(-gap).Within(1e-5f));
        }

        [Test]
        public void AFootDeliberatelyInTheAirIsLeftInTheAir()
        {
            // This is why a run still looks like a run. A walk cycle lifts the swing foot well
            // clear on purpose; a solver that dragged every foot down would plant both and the
            // colonist would skate.
            Assert.That(Footing.Correction(4f, 4f + Footing.ReachMetres * 2f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(Footing.Correction(4f, 4f - Footing.ReachMetres * 2f), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void TheCorrectionFadesWithoutACorner()
        {
            // A linear fade has a corner in it, and a corner in a correction is a visible tick as
            // the foot passes through it. Sampled across the whole band and required to be smooth:
            // no step between neighbouring samples bigger than the band's own resolution allows.
            float previous = Footing.Correction(0f, 0f);
            float biggestStep = 0f;

            for (float gap = 0f; gap <= Footing.ReachMetres * 1.2f; gap += 0.002f)
            {
                float here = Footing.Correction(0f, gap);
                biggestStep = Mathf.Max(biggestStep, Mathf.Abs(here - previous));
                previous = here;
            }

            Assert.That(biggestStep, Is.LessThan(0.01f), "the correction jumps somewhere in its band");

            // And it really does reach zero by the edge of the band rather than being cut off there.
            Assert.That(Footing.Correction(0f, Footing.ReachMetres * 0.999f), Is.EqualTo(0f).Within(2e-3f));
        }

        [Test]
        public void TheHipsDropToTheDeepestFootAndNeverRise()
        {
            // The single thing that makes slope footing read. Solve two legs independently on a
            // hillside and the downhill one straightens, locks and still falls short, because a
            // leg is only so long.
            Assert.That(Footing.HipDrop(-0.2f, -0.05f), Is.EqualTo(-0.2f).Within(1e-5f));
            Assert.That(Footing.HipDrop(-0.05f, -0.2f), Is.EqualTo(-0.2f).Within(1e-5f));

            // Never up: raising the hips to meet a foot that needs lifting takes the other foot
            // off the ground with it, which trades one floating foot for another and adds a bob to
            // every step across a slope.
            Assert.That(Footing.HipDrop(0.2f, 0.1f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(Footing.HipDrop(0.2f, -0.1f), Is.EqualTo(-0.1f).Within(1e-5f));
            Assert.That(Footing.HipDrop(0f, 0f), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void TheAnkleTakesWhatTheBodyLeftAndNoMore()
        {
            // The body has already taken LeanFraction of the slope, so asking the foot for the
            // whole tilt again tips it past the ground.
            Vector3 normal = Footing.GroundNormal(0.25f, 0f);
            Quaternion lean = Footing.LeanTo(normal);

            Quaternion ankle = Footing.AnkleLevel(normal, lean);
            float full = Vector3.Angle(Vector3.up, normal);
            float taken = Quaternion.Angle(Quaternion.identity, lean);
            float left = Quaternion.Angle(Quaternion.identity, ankle);

            Assert.That(left, Is.EqualTo(full - taken).Within(0.5f),
                "the ankle is not taking exactly the remainder");

            // Body and ankle together lay the foot along the ground.
            Assert.That(Quaternion.Angle(ankle * lean, Quaternion.FromToRotation(Vector3.up, normal)),
                Is.LessThan(0.5f), "the foot does not end up flat on the ground");
        }

        [Test]
        public void AnAnkleHasALimit()
        {
            // An ankle that exceeds its limit reads as a broken one. The surround's hills are
            // steep enough to ask.
            Quaternion ankle = Footing.AnkleLevel(Footing.GroundNormal(3f, 3f), Quaternion.identity);
            Assert.That(Quaternion.Angle(Quaternion.identity, ankle),
                Is.LessThanOrEqualTo(Footing.MaxAnkleDegrees + 1e-2f));
        }

        [Test]
        public void FlatGroundLeavesTheAnkleAlone()
        {
            Assert.That(Quaternion.Angle(Footing.AnkleLevel(Vector3.up, Quaternion.identity), Quaternion.identity),
                Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void ADegenerateNormalIsNotACrash()
        {
            // Nothing should ever hand these over, and a figure snapping to a random attitude
            // because something upstream returned a zero vector is a worse failure than a
            // colonist standing up straight.
            Assert.That(Footing.LeanTo(Vector3.zero), Is.EqualTo(Quaternion.identity));
            Assert.That(Footing.AnkleLevel(Vector3.zero, Quaternion.identity), Is.EqualTo(Quaternion.identity));
        }

        sealed class Vec3Comparer : System.Collections.IComparer
        {
            public int Compare(object? a, object? b) =>
                Vector3.Distance((Vector3)a!, (Vector3)b!) < 1e-4f ? 0 : 1;
        }
    }
}
