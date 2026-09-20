#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The drawn half of a supply drop (design 23 §6): the thing starts above the top of the
    /// world, comes down faster as it goes, and is on the floor exactly when the simulation says
    /// it landed.
    /// </summary>
    public class FallArcTests
    {
        [Test]
        public void TheFallStartsAboveTheTopOfTheWorldWhateverLayerItLandsOn()
        {
            float ground = FallArc.StartHeight(worldLayers: 16, landingLayer: 0);
            float roof = FallArc.StartHeight(worldLayers: 16, landingLayer: 5);

            Assert.That(ground, Is.EqualTo(16 * CellMetrics.SizeY + FallArc.Clearance));
            Assert.That(roof, Is.EqualTo(11 * CellMetrics.SizeY + FallArc.Clearance));
            Assert.That(ground - roof, Is.EqualTo(5 * CellMetrics.SizeY).Within(1e-4f),
                "both starts are the same height in the world, so they differ by the landing's own height");
        }

        [Test]
        public void ProgressIsZeroAtLaunchOneAtLandingAndCarriedOnByTheFrame()
        {
            Assert.That(FallArc.Progress(tick: 100, tickAlpha: 0f, launchTick: 100, landTick: 220), Is.EqualTo(0f));
            Assert.That(FallArc.Progress(tick: 220, tickAlpha: 0f, launchTick: 100, landTick: 220), Is.EqualTo(1f));
            Assert.That(FallArc.Progress(tick: 160, tickAlpha: 0f, launchTick: 100, landTick: 220), Is.EqualTo(0.5f));
            Assert.That(FallArc.Progress(tick: 160, tickAlpha: 0.5f, launchTick: 100, landTick: 220),
                Is.GreaterThan(0.5f), "a frame between two ticks is drawn further along than the tick");
            Assert.That(FallArc.Progress(tick: 500, tickAlpha: 0.9f, launchTick: 100, landTick: 220), Is.EqualTo(1f),
                "never past the floor");
            Assert.That(FallArc.Progress(tick: 100, tickAlpha: 0f, launchTick: 100, landTick: 100), Is.EqualTo(1f),
                "a fall of no ticks is already over");
        }

        [Test]
        public void TheHeightGoesFromTheStartToTheFloorAndOnlyEverDown()
        {
            const float start = 54f;
            Assert.That(FallArc.HeightAt(start, 0f), Is.EqualTo(start));
            Assert.That(FallArc.HeightAt(start, 1f), Is.EqualTo(0f));

            float previous = start;
            for (int i = 1; i <= 20; i++)
            {
                float height = FallArc.HeightAt(start, i / 20f);
                Assert.That(height, Is.LessThan(previous), $"rose between step {i - 1} and {i}");
                previous = height;
            }
        }

        [Test]
        public void TheSecondHalfOfTheFallCoversMoreGroundThanTheFirst()
        {
            const float start = 54f;
            float firstHalf = start - FallArc.HeightAt(start, 0.5f);
            float secondHalf = FallArc.HeightAt(start, 0.5f);

            Assert.That(secondHalf, Is.GreaterThan(firstHalf), "a fall that does not gather speed reads as a lift");
        }

        [Test]
        public void APausedWorldHoldsTheThingStill()
        {
            float a = FallArc.HeightAbove(16, 0, tick: 150, tickAlpha: 0.25f, launchTick: 100, landTick: 220);
            float b = FallArc.HeightAbove(16, 0, tick: 150, tickAlpha: 0.25f, launchTick: 100, landTick: 220);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a, Is.GreaterThan(0f).And.LessThan(FallArc.StartHeight(16, 0)));
        }
    }
}
