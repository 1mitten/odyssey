#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.World;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The arithmetic that decides whether a colonist's feet grip the ground or skate over it.
    ///
    /// Every way of getting this wrong is silent. A gait pair chosen one index out makes a
    /// colonist run on the spot; a playback rate out by the figure's scale makes the feet slide;
    /// a weight of zero on the only gait there is makes the figure collapse into its bind pose.
    /// None of them throws and none of them logs, and all three look from the outside like the
    /// animation being bad rather than like a number being wrong.
    ///
    /// The speeds used here are the ones actually measured from the pack's root-motion clips,
    /// scaled by the 1.4 the colonist is drawn at: a walk that covers 2.04 m/s and a run that
    /// covers 3.63. The pawn's own pace is 1.5 m/s — a 2.5 m cell every hundred ticks at sixty
    /// ticks a second — which sits below the walk, so at ordinary game speed the run clip must
    /// carry no weight at all. It did once: at the old 3 m/s the blend was six parts run to
    /// four parts walk, and the owner reported that colonists ran everywhere by default.
    /// </summary>
    public class GaitBlendTests
    {
        static readonly float[] Colonist = { 0f, 2.04f, 3.63f };

        /// <summary>The pace the simulation moves a colonist at: one cost unit a tick against
        /// a hundred-unit cell, sixty ticks a second, 2.5 m cells.</summary>
        const float ColonistPace = 1.5f;

        [Test]
        public void StandingStillIsAllIdle()
        {
            GaitBlend blend = GaitBlend.Solve(Colonist, 0f);

            Assert.That(blend.WeightOf(0), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(blend.WeightOf(1), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(blend.WeightOf(2), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void AColonistsOwnPaceIsAWalkAndNeverARun()
        {
            // The speed the simulation actually moves a pawn at. It sits below the walk clip's
            // own stride, so the walk carries the figure and the run stays out of it; and the
            // walk plays at its authored rate, with the idle taking up the slack, so the stance
            // foot travels back at the ground's own speed and grips rather than skates.
            GaitBlend blend = GaitBlend.Solve(Colonist, ColonistPace);

            Assert.That(blend.Lower, Is.EqualTo(0), "idle");
            Assert.That(blend.Upper, Is.EqualTo(1), "walk");
            Assert.That(blend.WeightOf(2), Is.EqualTo(0f).Within(1e-4f), "the run is out of it entirely");
            Assert.That(blend.WeightOf(1), Is.GreaterThan(0.7f), "and it is mostly walk");
            Assert.That(blend.WeightOf(0) + blend.WeightOf(1), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(blend.Rate, Is.EqualTo(1f).Within(1e-4f),
                "inside the range the gaits cover, the authored rate is already the right one");
        }

        [Test]
        public void FasterThanTheWalkBlendsWalkIntoRun()
        {
            // 3 m/s is what a colonist covers at double game speed, and what the simulation used
            // to move them at by default. It sits between the two moving gaits, which is the
            // reason a run clip is catalogued as well as a walk.
            GaitBlend blend = GaitBlend.Solve(Colonist, 3.0f);

            Assert.That(blend.Lower, Is.EqualTo(1), "walk");
            Assert.That(blend.Upper, Is.EqualTo(2), "run");
            Assert.That(blend.WeightOf(0), Is.EqualTo(0f).Within(1e-4f), "idle is out of it entirely");
            Assert.That(blend.WeightOf(1) + blend.WeightOf(2), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(blend.Rate, Is.EqualTo(1f).Within(1e-4f),
                "inside the range the gaits cover, the authored rate is already the right one");
        }

        [Test]
        public void WeightsAlwaysSumToOne()
        {
            for (float speed = 0f; speed <= 6f; speed += 0.13f)
            {
                GaitBlend blend = GaitBlend.Solve(Colonist, speed);
                float sum = blend.WeightOf(0) + blend.WeightOf(1) + blend.WeightOf(2);
                Assert.That(sum, Is.EqualTo(1f).Within(1e-4f), $"at {speed:0.00} m/s");
            }
        }

        [Test]
        public void HalfwayBetweenTwoGaitsIsHalfAndHalf()
        {
            GaitBlend blend = GaitBlend.Solve(Colonist, 1.02f);

            Assert.That(blend.WeightOf(0), Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(blend.WeightOf(1), Is.EqualTo(0.5f).Within(1e-3f));
        }

        [Test]
        public void OutrunningEveryGaitSpeedsTheFastestUpRatherThanSliding()
        {
            GaitBlend blend = GaitBlend.Solve(Colonist, 7.26f);

            Assert.That(blend.WeightOf(2), Is.EqualTo(1f).Within(1e-4f), "the run, alone");
            Assert.That(blend.Rate, Is.EqualTo(2f).Within(1e-3f), "twice the run's own pace");
        }

        [Test]
        public void TheStretchIsCapped()
        {
            // Past a point a sped-up run reads as a cartoon. Some slide is the lesser fault.
            GaitBlend blend = GaitBlend.Solve(Colonist, 100f);
            Assert.That(blend.Rate, Is.EqualTo(2.5f).Within(1e-4f));
        }

        [Test]
        public void ASingleGaitCarriesFullWeight()
        {
            // A pack that ships an idle and nothing else. The failure this guards against is a
            // weight of zero on the only clip there is, which drops the figure into its bind
            // pose — arms straight out — with nothing at all to say why.
            GaitBlend blend = GaitBlend.Solve(new[] { 0f }, 3f);

            Assert.That(blend.WeightOf(0), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(blend.Rate, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void NoGaitsAtAllIsSurvivable()
        {
            Assert.DoesNotThrow(() => GaitBlend.Solve(new float[0], 2f));
        }
    }
}
