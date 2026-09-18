#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.World;
using UnityEngine;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// The per-colonist walk dials: build, stride and the sideways bow.
    ///
    /// <para>Arithmetic only — no figure, no rig, no Unity scene. Whether a colony now reads as
    /// people rather than copies is the owner's to judge and nothing here can say it.</para>
    /// </summary>
    public class WalkVarianceTests
    {
        [SetUp]
        public void Reset() => WalkVariance.Reset();

        [TearDown]
        public void Restore() => WalkVariance.Reset();

        // ---- build and stride ------------------------------------------------------------

        [Test]
        public void EveryColonistIsWithinTheBand()
        {
            for (int id = 0; id < 500; id++)
            {
                float scale = WalkVariance.StrideScale(id);
                Assert.That(scale, Is.InRange(1f - WalkVariance.Build, 1f + WalkVariance.Build),
                    $"pawn {id} is outside the band");
            }
        }

        [Test]
        public void TheSameColonistIsAlwaysTheSameSize()
        {
            for (int id = 0; id < 50; id++)
                Assert.That(WalkVariance.StrideScale(id), Is.EqualTo(WalkVariance.StrideScale(id)),
                    "a size that re-rolled would have the colony pulsing as figures were pooled");
        }

        /// <summary>
        /// The dial turned off puts every colonist back to exactly the drawn standard, which is
        /// what the cast looked like before any of this existed. The owner's escape hatch.
        /// </summary>
        [Test]
        public void ZeroVarianceIsTheOldBehaviourExactly()
        {
            WalkVariance.Build = 0f;
            for (int id = 0; id < 100; id++)
                Assert.That(WalkVariance.StrideScale(id), Is.EqualTo(1f));
        }

        [Test]
        public void AColonyIsNotAllOneSize()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int id = 0; id < 40; id++) seen.Add(Mathf.RoundToInt(WalkVariance.StrideScale(id) * 1000f));

            Assert.That(seen.Count, Is.GreaterThan(25),
                "forty colonists should not share a handful of sizes");
        }

        // ---- the bow ---------------------------------------------------------------------

        [Test]
        public void TheBowNeverLeavesTheCell()
        {
            // 1.25 m is half a cell; the argument for not asking the world about walls is that the
            // figure plus its own width stays well inside that. See WalkVariance.Bow.
            for (int id = 0; id < 60; id++)
            for (float d = 0f; d < 40f; d += 0.05f)
                Assert.That(Mathf.Abs(WalkVariance.BowOffset(id, d)), Is.LessThanOrEqualTo(WalkVariance.Bow + 1e-4f),
                    $"pawn {id} at {d} m is outside the cap");
        }

        /// <summary>
        /// The bow is a function of distance, so it cannot jump between two frames of walking.
        ///
        /// <para>The yardstick is <c>WalkOnReliefTests</c>'s: 25 mm is what an honest frame of
        /// walking carries a colonist, and the 81.9 mm snap that test was written for is what a
        /// discontinuity looks like. A frame of walking is about 0.03 m at these speeds.</para>
        /// </summary>
        [Test]
        public void TheBowMovesNoFasterThanTheWalkDoes()
        {
            const float frame = 0.035f;

            for (int id = 0; id < 60; id++)
            {
                float previous = WalkVariance.BowOffset(id, 0f);
                for (float d = frame; d < 60f; d += frame)
                {
                    float now = WalkVariance.BowOffset(id, d);
                    Assert.That(Mathf.Abs(now - previous), Is.LessThan(0.025f),
                        $"pawn {id} jumped sideways at {d} m");
                    previous = now;
                }
            }
        }

        [Test]
        public void AStandingColonistDoesNotDrift()
        {
            // The phase is distance, not time. Ask twice at the same distance and nothing moved.
            for (int id = 0; id < 30; id++)
                Assert.That(WalkVariance.BowOffset(id, 12.5f), Is.EqualTo(WalkVariance.BowOffset(id, 12.5f)));
        }

        [Test]
        public void ZeroBowIsTheOldStraightLineExactly()
        {
            WalkVariance.Bow = 0f;
            for (int id = 0; id < 30; id++)
            for (float d = 0f; d < 20f; d += 0.5f)
                Assert.That(WalkVariance.BowOffset(id, d), Is.EqualTo(0f));
        }

        /// <summary>
        /// Two colonists walking the same route are not in step. This is the whole point: single
        /// file down one line is what the bow exists to break up.
        /// </summary>
        [Test]
        public void TwoColonistsOnTheSameRouteDoNotBowTogether()
        {
            int apart = 0;
            for (float d = 0f; d < 30f; d += 0.25f)
                if (Mathf.Abs(WalkVariance.BowOffset(1, d) - WalkVariance.BowOffset(2, d)) > 0.05f) apart++;

            Assert.That(apart, Is.GreaterThan(60), "two colonists traced nearly the same line");
        }

        // ---- the streams -----------------------------------------------------------------

        /// <summary>
        /// Build must not predict the bow. One hash taken three ways correlates the slots — the
        /// tall colonists would all wander the same way — and <c>ColonistAppearance</c> already
        /// learned this about colour, where everyone with red hair also wore red.
        /// </summary>
        [Test]
        public void TheDialsAreIndependentOfEachOther()
        {
            int agree = 0;
            const int n = 400;

            for (int id = 0; id < n; id++)
            {
                bool big = WalkVariance.StrideScale(id) > 1f;
                bool right = WalkVariance.BowOffset(id, 0.7f) > 0f;
                if (big == right) agree++;
            }

            // Independent streams land near half. A shared one would sit at nought or at n.
            Assert.That(agree, Is.InRange(n * 0.40f, n * 0.60f),
                $"build and bow agreed {agree} times in {n}, which is not independence");
        }
    }
}
