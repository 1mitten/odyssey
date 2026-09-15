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
    /// covers 3.63. The pawn's own speed of about 3 m/s — a 2.5 m cell every fifty ticks at sixty
    /// ticks a second — lands between them, which is the case that matters.
    /// </summary>
    public class GaitBlendTests
    {
        static readonly float[] Colonist = { 0f, 2.04f, 3.63f };

        [Test]
        public void StandingStillIsAllIdle()
        {
            GaitBlend blend = GaitBlend.Solve(Colonist, 0f);

            Assert.That(blend.WeightOf(0), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(blend.WeightOf(1), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(blend.WeightOf(2), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void AColonistsOwnPaceBlendsWalkIntoRun()
        {
            // The speed the simulation actually moves a pawn at. It sits between the two moving
            // gaits, which is the whole reason a run clip was catalogued as well as a walk.
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
