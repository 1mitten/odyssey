#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The drawn half of a supply drop (design 23 §6): the thing starts above the camera, comes
    /// down at one speed, and is on the floor exactly when the simulation says it landed.
    /// </summary>
    public class FallArcTests
    {
        [Test]
        public void TheFallStartsAboveTheCameraWhateverLayerItLandsOn()
        {
            float ground = FallArc.StartHeight(worldLayers: 16, landingLayer: 0);
            float roof = FallArc.StartHeight(worldLayers: 16, landingLayer: 5);

            Assert.That(ground, Is.EqualTo(FallArc.DropHeight));
            Assert.That(roof, Is.EqualTo(FallArc.DropHeight),
                "a rooftop drop starts the same distance over its roof as a meadow drop does over the meadow");
            Assert.That(FallArc.DropHeight, Is.GreaterThan(16 * CellMetrics.SizeY),
                "the play camera never sits above the drop's start");
        }

        [Test]
        public void AWorldTallerThanTheDropHeightStillStartsAboveItsTop()
        {
            float start = FallArc.StartHeight(worldLayers: 60, landingLayer: 0);
            Assert.That(start, Is.EqualTo(60 * CellMetrics.SizeY + FallArc.Clearance));
        }

        [Test]
        public void ProgressIsZeroAtLaunchOneAtLandingAndCarriedOnByTheFrame()
        {
            Assert.That(FallArc.Progress(tick: 100, tickAlpha: 0f, launchTick: 100, landTick: 460), Is.EqualTo(0f));
            Assert.That(FallArc.Progress(tick: 460, tickAlpha: 0f, launchTick: 100, landTick: 460), Is.EqualTo(1f));
            Assert.That(FallArc.Progress(tick: 280, tickAlpha: 0f, launchTick: 100, landTick: 460), Is.EqualTo(0.5f));
            Assert.That(FallArc.Progress(tick: 280, tickAlpha: 0.5f, launchTick: 100, landTick: 460),
                Is.GreaterThan(0.5f), "a frame between two ticks is drawn further along than the tick");
            Assert.That(FallArc.Progress(tick: 900, tickAlpha: 0.9f, launchTick: 100, landTick: 460), Is.EqualTo(1f),
                "never past the floor");
            Assert.That(FallArc.Progress(tick: 100, tickAlpha: 0f, launchTick: 100, landTick: 100), Is.EqualTo(1f),
                "a fall of no ticks is already over");
        }

        [Test]
        public void TheHeightGoesFromTheStartToTheFloorAtOneSpeed()
        {
            const float start = 120f;
            Assert.That(FallArc.HeightAt(start, 0f), Is.EqualTo(start));
            Assert.That(FallArc.HeightAt(start, 1f), Is.EqualTo(0f));

            float step = FallArc.HeightAt(start, 0f) - FallArc.HeightAt(start, 0.05f);
            for (int i = 1; i <= 20; i++)
            {
                float drop = FallArc.HeightAt(start, (i - 1) / 20f) - FallArc.HeightAt(start, i / 20f);
                Assert.That(drop, Is.EqualTo(step).Within(1e-4f),
                    $"step {i} covered a different distance from the first: a crate under a chute does not gather speed");
            }
        }

        [Test]
        public void APausedWorldHoldsTheThingStill()
        {
            float a = FallArc.HeightAbove(16, 0, tick: 150, tickAlpha: 0.25f, launchTick: 100, landTick: 460);
            float b = FallArc.HeightAbove(16, 0, tick: 150, tickAlpha: 0.25f, launchTick: 100, landTick: 460);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a, Is.GreaterThan(0f).And.LessThan(FallArc.StartHeight(16, 0)));
        }
    }
}
