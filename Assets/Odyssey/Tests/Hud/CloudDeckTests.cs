#nullable enable
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The cloud rings' placement (design 63): how cover stands them up, how they drift, and when a
    /// camera cannot see them and nothing need be submitted.
    /// </summary>
    public class CloudDeckTests
    {
        // ------------------------------------------------------------------ cover

        [Test]
        public void ClearIsTheLowBandAndOvercastIsTheDemosBanks()
        {
            Assert.That(CloudDeck.HeightScale(0f, CloudDeck.LowHeightClear, CloudDeck.LowHeightFull),
                Is.EqualTo(CloudDeck.LowHeightClear));
            Assert.That(CloudDeck.HeightScale(1f, CloudDeck.LowHeightClear, CloudDeck.LowHeightFull),
                Is.EqualTo(CloudDeck.LowHeightFull));
        }

        [Test]
        public void CoverOutsideItsRangeIsHeldToIt()
        {
            Assert.That(CloudDeck.HeightScale(-3f, 2f, 7f), Is.EqualTo(2f));
            Assert.That(CloudDeck.HeightScale(9f, 2f, 7f), Is.EqualTo(7f));
        }

        [Test]
        public void MoreCoverNeverMakesTheRingShorter()
        {
            float last = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float h = CloudDeck.HeightScale(i / 100f, CloudDeck.LowHeightClear, CloudDeck.LowHeightFull);
                Assert.That(h, Is.GreaterThanOrEqualTo(last), $"at cover {i}%");
                last = h;
            }
        }

        [Test]
        public void EveryClearRingIsStillAboveNothing()
        {
            // The owner's report was that the clear sky looks bare, so clear must still draw.
            Assert.That(CloudDeck.LowHeightClear, Is.GreaterThan(0f));
            Assert.That(CloudDeck.HighHeightClear, Is.GreaterThan(0f));
        }

        // ------------------------------------------------------------------ the night

        [Test]
        public void TheCloudsAreThereAllDayAndGoneAllNight()
        {
            foreach (float hour in new[] { 6.5f, 9f, 13f, 17f, 19f, 19.5f })
                Assert.That(CloudDeck.Presence(hour), Is.EqualTo(1f), $"at {hour}h");
            foreach (float hour in new[] { 21f, 22f, 0f, 3f, 4.99f })
                Assert.That(CloudDeck.Presence(hour), Is.EqualTo(0f), $"at {hour}h");
        }

        [Test]
        public void TheGoldenHourKeepsItsClouds()
        {
            // Dusk's warm key is at 19:00 and dawn's at 07:00 (Daylight): both whole.
            Assert.That(CloudDeck.Presence(19f), Is.EqualTo(1f));
            Assert.That(CloudDeck.Presence(7f), Is.EqualTo(1f));
        }

        [Test]
        public void TheFadeIsGradualAndOneWayAtEachEnd()
        {
            float last = 1f;
            for (float h = 19.5f; h <= 21f; h += 0.05f)
            {
                float p = CloudDeck.Presence(h);
                Assert.That(p, Is.LessThanOrEqualTo(last + 1e-6f), $"dusk rose at {h}h");
                Assert.That(last - p, Is.LessThan(0.1f), $"dusk stepped at {h}h");
                last = p;
            }
            last = 0f;
            for (float h = 5f; h <= 6.5f; h += 0.05f)
            {
                float p = CloudDeck.Presence(h);
                Assert.That(p, Is.GreaterThanOrEqualTo(last - 1e-6f), $"dawn fell at {h}h");
                Assert.That(p - last, Is.LessThan(0.1f), $"dawn stepped at {h}h");
                last = p;
            }
        }

        [Test]
        public void TheHourWrapsRoundMidnight()
        {
            Assert.That(CloudDeck.Presence(-11f), Is.EqualTo(CloudDeck.Presence(13f)));
            Assert.That(CloudDeck.Presence(37f), Is.EqualTo(CloudDeck.Presence(13f)));
        }

        [Test]
        public void AFadingRingFlattensToNothing()
        {
            // A quarter of its height was left at first, and that band snapped off at 21:00.
            Assert.That(CloudDeck.Sink(1f), Is.EqualTo(1f));
            Assert.That(CloudDeck.Sink(0f), Is.Zero);
            Assert.That(CloudDeck.Sink(0.5f), Is.EqualTo(0.5f));
        }

        [Test]
        public void AFadingRingSinksToTheEyeLine()
        {
            Assert.That(CloudDeck.BaseAt(68f, 30f, 1f), Is.EqualTo(68f));
            Assert.That(CloudDeck.BaseAt(68f, 30f, 0f), Is.EqualTo(30f), "edge-on at eye level: no thickness left to see");
            Assert.That(CloudDeck.BaseAt(68f, 30f, 0.5f), Is.EqualTo(49f));
        }

        [Test]
        public void AJumpInTheHourFadesInsteadOfSnapping()
        {
            float shown = 0f;
            shown = CloudDeck.EasePresence(shown, 1f, 1f);
            Assert.That(shown, Is.EqualTo(1f / CloudDeck.JumpFadeSeconds).Within(1e-5f));
            for (int i = 0; i < 10; i++) shown = CloudDeck.EasePresence(shown, 1f, 1f);
            Assert.That(shown, Is.EqualTo(1f), "and it arrives");
            for (int i = 0; i < 10; i++) shown = CloudDeck.EasePresence(shown, 0f, 1f);
            Assert.That(shown, Is.Zero, "and goes the same way");
        }

        [Test]
        public void APausedFrameDoesNotMoveTheFade()
        {
            Assert.That(CloudDeck.EasePresence(0.3f, 1f, 0f), Is.EqualTo(0.3f));
        }

        [Test]
        public void TheLimitNeverSlowsAnOrdinaryDuskEvenAtSpeedThree()
        {
            // The dusk fade's steepest rate, per real second at speed 3, must be under the limit's.
            double realSecondsPerHour = Odyssey.Sim.Contracts.Calendar.TicksPerHour / 60.0 / 3.0;
            float steepest = 0f;
            for (float h = CloudDeck.FadeOutStart; h < CloudDeck.FadeOutEnd; h += 0.01f)
                steepest = System.Math.Max(steepest, (CloudDeck.Presence(h) - CloudDeck.Presence(h + 0.01f)) / 0.01f);
            double perRealSecond = steepest / realSecondsPerHour;
            Assert.That(perRealSecond, Is.LessThan(1.0 / CloudDeck.JumpFadeSeconds));
        }

        // ------------------------------------------------------------------ rain

        [Test]
        public void RainStandsTheRingsUpBeyondTheirCover()
        {
            Assert.That(CloudDeck.StandingCover(0.5f, 0f), Is.EqualTo(0.5f));
            Assert.That(CloudDeck.StandingCover(0.5f, 1f), Is.GreaterThan(0.5f));
            Assert.That(CloudDeck.StandingCover(0.9f, 1f), Is.EqualTo(1f), "never past full");
        }

        // ------------------------------------------------------------------ drift

        [Test]
        public void APausedWorldHoldsTheSky()
        {
            Assert.That(CloudDeck.Advance(37f, 0f, 1f, CloudDeck.DriftDegreesPerSecond), Is.EqualTo(37f));
        }

        [Test]
        public void SpeedThreeDriftsThreeTimesAsFarAsSpeedOne()
        {
            float one = CloudDeck.Advance(0f, 10f, 1f, CloudDeck.DriftDegreesPerSecond);
            float three = CloudDeck.Advance(0f, 30f, 1f, CloudDeck.DriftDegreesPerSecond);
            Assert.That(three, Is.EqualTo(3f * one).Within(1e-4f));
        }

        [Test]
        public void TheStormDrivesTheCloudsHarder()
        {
            float calm = CloudDeck.Advance(0f, 10f, 1f, CloudDeck.DriftDegreesPerSecond);
            float storm = CloudDeck.Advance(0f, 10f, 2.5f, CloudDeck.DriftDegreesPerSecond);
            Assert.That(storm, Is.EqualTo(2.5f * calm).Within(1e-4f));
        }

        [Test]
        public void TheTurnWrapsRatherThanGrowing()
        {
            float yaw = CloudDeck.Advance(359f, 100f, 1f, 0.05f);
            Assert.That(yaw, Is.EqualTo(4f).Within(1e-3f));
            Assert.That(yaw, Is.InRange(0f, 360f));
        }

        [Test]
        public void TheHighRingTurnsSlowerThanTheLow()
        {
            Assert.That(CloudDeck.HighDriftRatio, Is.InRange(0.1f, 0.99f));
        }

        // ------------------------------------------------------------------ what a camera sees

        [Test]
        public void ALevelCameraSeesHalfItsFieldAboveTheHorizon()
        {
            Assert.That(CloudDeck.HighestElevation(0f, 60f, 16f / 9f), Is.EqualTo(30f).Within(1e-3f));
        }

        [Test]
        public void TheColonyCameraAtItsUsualPitchSeesNoSky()
        {
            // 48 degrees down through a 40-degree field: the top edge is still 28 degrees down.
            float highest = CloudDeck.HighestElevation(48f, 40f, 16f / 9f);
            Assert.That(highest, Is.LessThan(-20f));
        }

        [Test]
        public void TheColonyCameraAtItsLowestPitchJustReachesTheHorizon()
        {
            // 20 degrees down through a 40-degree field puts the top edge level.
            Assert.That(CloudDeck.HighestElevation(20f, 40f, 16f / 9f), Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void BelowTheHorizonTheCornersSeeHigherThanTheMiddle()
        {
            // The rule the check rests on: the corners, not the top-centre, bound the view.
            float wide = CloudDeck.HighestElevation(40f, 40f, 16f / 9f);
            float narrow = CloudDeck.HighestElevation(40f, 40f, 0.01f);
            Assert.That(wide, Is.GreaterThan(narrow));
        }

        [Test]
        public void AboveTheHorizonTheMiddleOfTheTopEdgeIsHighest()
        {
            float wide = CloudDeck.HighestElevation(-10f, 60f, 16f / 9f);
            Assert.That(wide, Is.EqualTo(40f).Within(1e-3f));
        }

        [Test]
        public void ABaseAboveTheEyeIsLowestAtTheFarSide()
        {
            float lowest = CloudDeck.LowestElevation(1500f, 150f, 0f);
            Assert.That(lowest, Is.EqualTo((float)(System.Math.Atan2(150, 1500) * 180 / System.Math.PI)).Within(1e-3f));
        }

        [Test]
        public void ABaseBelowTheEyeIsLowestOnTheInside()
        {
            float lowest = CloudDeck.LowestElevation(1500f, 0f, 150f);
            float inside = (float)(System.Math.Atan2(-150, 1500 * CloudDeck.InnerFraction) * 180 / System.Math.PI);
            Assert.That(lowest, Is.EqualTo(inside).Within(1e-3f));
        }

        [Test]
        public void TheUsualColonyViewSkipsTheRings()
        {
            float highest = CloudDeck.HighestElevation(48f, 40f, 16f / 9f);
            float lowest = CloudDeck.LowestElevation(1540f, 68f, 30f + 160f * 0.74f);
            Assert.That(CloudDeck.CanBeSeen(highest, lowest), Is.False);
        }

        [Test]
        public void AColonistsEyeLookingAheadSeesThem()
        {
            float highest = CloudDeck.HighestElevation(4f, 60f, 16f / 9f);
            float lowest = CloudDeck.LowestElevation(1540f, 68f, 30f);
            Assert.That(CloudDeck.CanBeSeen(highest, lowest), Is.True);
        }

        [Test]
        public void TheMarginDrawsARingJustOutOfView()
        {
            Assert.That(CloudDeck.CanBeSeen(-1f, 0.5f), Is.True);
            Assert.That(CloudDeck.CanBeSeen(-3f, 0.5f), Is.False);
        }
    }
}
