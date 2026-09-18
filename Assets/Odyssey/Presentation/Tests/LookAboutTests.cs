#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.World;
using UnityEngine;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// The head-look: two angles over a phase, clamped to something a neck can do.
    ///
    /// <para>The same bargain every pose in this folder takes — the arithmetic is testable and the
    /// appearance is not, and treating the first as though it proved the second is the whole
    /// reason <c>MeasuredBladeGap</c> exists. <c>MeasuredLookYaw</c> is this pose's answer to it,
    /// and the contact sheet is the rest.</para>
    /// </summary>
    public class LookAboutTests
    {
        [SetUp]
        public void Reset() => LookAbout.Reset();

        [TearDown]
        public void Restore() => LookAbout.Reset();

        [Test]
        public void TheHeadStaysInsideWhatANeckCanDo()
        {
            for (float p = 0f; p <= 1f; p += 0.001f)
            {
                Vector2 turn = LookAbout.At(p);
                Assert.That(Mathf.Abs(turn.x), Is.LessThanOrEqualTo(LookAbout.YawDegrees + 1e-3f),
                    $"yaw out of range at phase {p}");
                Assert.That(Mathf.Abs(turn.y), Is.LessThanOrEqualTo(LookAbout.PitchDegrees + 1e-3f),
                    $"pitch out of range at phase {p}");
            }
        }

        /// <summary>
        /// The idiom from <c>WorkSwingTests.ThePoseNeverJumps</c> and
        /// <c>GesturePoseTests.ThePoseNeverJumps</c>. A head that teleports reads as a glitch, and
        /// at 0.11 cycles a second a frame is about 0.002 of the phase.
        /// </summary>
        [Test]
        public void ThePoseNeverJumps()
        {
            Vector2 previous = LookAbout.At(0f);
            for (float p = 0.002f; p <= 1f; p += 0.002f)
            {
                Vector2 now = LookAbout.At(p);
                Assert.That(Mathf.Abs(now.x - previous.x), Is.LessThan(1.5f), $"yaw jumped at {p}");
                Assert.That(Mathf.Abs(now.y - previous.y), Is.LessThan(1.5f), $"pitch jumped at {p}");
                previous = now;
            }
        }

        /// <summary>And it is a cycle, so the seam at the wrap is a frame like any other.</summary>
        [Test]
        public void TheCycleJoinsUpAtTheWrap()
        {
            Vector2 end = LookAbout.At(0.999f);
            Vector2 start = LookAbout.At(0f);

            Assert.That(Mathf.Abs(start.x - end.x), Is.LessThan(1.5f), "yaw snaps at the wrap");
            Assert.That(Mathf.Abs(start.y - end.y), Is.LessThan(1.5f), "pitch snaps at the wrap");
        }

        /// <summary>
        /// It must not trace a line or a circle. One sine term in each axis would, and it reads as
        /// clockwork rather than as attention.
        /// </summary>
        [Test]
        public void ItWandersRatherThanSweeping()
        {
            int turningPoints = 0;
            float previous = LookAbout.At(0.002f).x - LookAbout.At(0f).x;

            for (float p = 0.004f; p <= 1f; p += 0.002f)
            {
                float slope = LookAbout.At(p).x - LookAbout.At(p - 0.002f).x;
                if (Mathf.Sign(slope) != Mathf.Sign(previous)) turningPoints++;
                previous = slope;
            }

            Assert.That(turningPoints, Is.GreaterThan(2),
                "a single sine turns twice a cycle; this should be less regular than that");
        }

        [Test]
        public void ThePhaseIsACycleAndTheOffsetMovesIt()
        {
            Assert.That(LookAbout.Phase(0f, 1f, 0.25f), Is.EqualTo(0.25f).Within(1e-5f));

            float a = LookAbout.Phase(10f, 1f, 0f);
            float b = LookAbout.Phase(10f, 1f, 0.5f);
            Assert.That(a, Is.Not.EqualTo(b), "two colonists should not look about in unison");

            for (float s = 0f; s < 100f; s += 1.3f)
                Assert.That(LookAbout.Phase(s, 1.2f, 0.3f), Is.InRange(0f, 1f));
        }

        [Test]
        public void TheWeightEasesRatherThanSwitching()
        {
            float w = 0f;
            int frames = 0;
            while (w < 0.999f && frames < 1000) { w = LookAbout.Settle(w, 1f, 1f / 60f); frames++; }

            Assert.That(frames, Is.GreaterThan(10), "a pose that arrives in under ten frames snaps");
            Assert.That(frames, Is.LessThan(120), "and one that takes two seconds is not easing, it is lagging");
        }

        /// <summary>The escape hatch: zeroed angles leave the head exactly where the clip put it.</summary>
        [Test]
        public void ZeroedAnglesAreTheOldStillHeadExactly()
        {
            LookAbout.YawDegrees = 0f;
            LookAbout.PitchDegrees = 0f;

            for (float p = 0f; p <= 1f; p += 0.01f)
            {
                Assert.That(LookAbout.At(p).x, Is.EqualTo(0f));
                Assert.That(LookAbout.At(p).y, Is.EqualTo(0f));
            }
        }
    }
}
