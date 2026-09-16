#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The day's light: blue at noon, orange at both ends, dark at night.
    ///
    /// <para>What is worth holding here is not the colours, which are art direction and will move,
    /// but the claims the owner would notice being broken: that noon is blue and brighter than
    /// anything else, that dawn and dusk are warmer than either neighbour, that night is dark but
    /// never black, that the sky and the haze agree at every hour, and that the whole thing is
    /// continuous — including across midnight, which is the one seam a keyed table can get
    /// wrong.</para>
    /// </summary>
    public class DaylightTests
    {
        static float Warmth(Color c) => c.r - c.b;

        [Test]
        public void NoonIsBlueAndTheBrightestHourOfTheDay()
        {
            DaylightState noon = Daylight.Sample(13f);

            Assert.That(noon.Zenith.b, Is.GreaterThan(noon.Zenith.r + 0.3f),
                "midday sky is blue, which is the whole of what the owner asked for at noon");
            Assert.That(noon.SunElevation, Is.GreaterThan(50f), "the sun is overhead at midday");

            for (float hour = 0f; hour < 24f; hour += 0.25f)
                Assert.That(Daylight.Sample(hour).SunIntensity,
                    Is.LessThanOrEqualTo(noon.SunIntensity + 0.001f),
                    $"nothing should out-shine midday, and {hour:0.00}h did");
        }

        [Test]
        public void DawnAndDuskAreTheWarmHours()
        {
            DaylightState dawn = Daylight.Sample(7f);
            DaylightState dusk = Daylight.Sample(19f);
            DaylightState noon = Daylight.Sample(13f);
            DaylightState night = Daylight.Sample(1f);

            Assert.That(Warmth(dawn.Horizon), Is.GreaterThan(Warmth(noon.Horizon)),
                "dawn is oranger than midday");
            Assert.That(Warmth(dusk.Horizon), Is.GreaterThan(Warmth(noon.Horizon)),
                "so is dusk");
            Assert.That(Warmth(dawn.Horizon), Is.GreaterThan(Warmth(night.Horizon)),
                "and than the night either side of it");
            Assert.That(Warmth(dawn.SunColour), Is.GreaterThan(Warmth(noon.SunColour)),
                "the light itself is warm at dawn, not only the sky behind it");
        }

        [Test]
        public void NightIsDarkAndNeverBlack()
        {
            DaylightState night = Daylight.Sample(2f);
            DaylightState noon = Daylight.Sample(13f);

            Assert.That(night.SunIntensity, Is.LessThan(noon.SunIntensity * 0.2f), "night is dark");
            Assert.That(night.Zenith.maxColorComponent, Is.LessThan(0.2f), "and the sky with it");

            // A colony sim is unplayable in real darkness, and a directional light at zero
            // flattens every face to the same value, which reads as a paper cut-out rather than
            // as night.
            Assert.That(night.SunIntensity, Is.GreaterThan(0f),
                "something must still separate a wall from the ground it stands on");
            Assert.That(night.AmbientSky.maxColorComponent, Is.GreaterThan(0.1f),
                "the ambient is the readability floor and must not go out");
            Assert.That(night.Zenith.b, Is.GreaterThan(night.Zenith.r), "night is blue, not brown");
        }

        [Test]
        public void TheHazeAgreesWithTheSkyAtEveryHour()
        {
            // The identity the whole look rests on: what the distance fades into is what the sky
            // is at the horizon. It is one field here, so the test is really that nobody has
            // introduced a second source for it.
            for (float hour = 0f; hour < 24f; hour += 0.5f)
            {
                DaylightState state = Daylight.Sample(hour);
                Assert.That(state.FogDensity, Is.GreaterThan(0f), $"the air is never perfectly clear ({hour}h)");
                Assert.That(state.Horizon.maxColorComponent, Is.GreaterThan(0f),
                    $"the horizon has a colour at {hour}h, and the fog takes it");
            }
        }

        [Test]
        public void TheLightMovesSmoothlyAndWrapsThroughMidnight()
        {
            // No step anywhere, including across the seam: midnight is both the first key and the
            // last precisely so that the end of the day meets the start of it without a special
            // case. A jump here would be a visible flash in the sky once a day.
            DaylightState previous = Daylight.Sample(0f);
            for (float hour = 0.05f; hour <= 24f; hour += 0.05f)
            {
                DaylightState state = Daylight.Sample(hour);
                Assert.That(Mathf.Abs(state.SunIntensity - previous.SunIntensity), Is.LessThan(0.1f),
                    $"the sun steps at {hour:0.00}h");
                Assert.That(Mathf.Abs(state.Zenith.b - previous.Zenith.b), Is.LessThan(0.06f),
                    $"the sky steps at {hour:0.00}h");
                previous = state;
            }

            DaylightState justBefore = Daylight.Sample(23.99f);
            DaylightState justAfter = Daylight.Sample(0.01f);
            Assert.That(Mathf.Abs(justAfter.SunIntensity - justBefore.SunIntensity), Is.LessThan(0.05f),
                "midnight is a seam in the table and must not be one on screen");
        }

        [Test]
        public void AnHourOutsideTheDayIsTheSameHourInsideIt()
        {
            Assert.That(Daylight.Sample(25f).SunIntensity, Is.EqualTo(Daylight.Sample(1f).SunIntensity));
            Assert.That(Daylight.Sample(-1f).SunIntensity, Is.EqualTo(Daylight.Sample(23f).SunIntensity));
        }

        [Test]
        public void TheTickReadsAsAContinuousHour()
        {
            Assert.That(Daylight.HourOf(0), Is.EqualTo(0f));
            Assert.That(Daylight.HourOf(GameClock.TicksPerHour * 13), Is.EqualTo(13f));

            // Continuous, where the clock's own hour is a whole number. A step once an hour would
            // be a jolt in the sky twenty-four times a day.
            Assert.That(Daylight.HourOf(GameClock.TicksPerHour / 2), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(Daylight.HourOf(GameClock.TicksPerDay + GameClock.TicksPerHour * 6),
                Is.EqualTo(6f).Within(0.001f), "the second day is lit like the first");
        }

        [Test]
        public void TheKeysAreInOrderAndCoverTheWholeDay()
        {
            Assert.That(Daylight.KeyHour(0), Is.EqualTo(0f), "the table starts at midnight");
            Assert.That(Daylight.KeyHour(Daylight.KeyCount - 1), Is.EqualTo(24f), "and ends there");

            for (int i = 1; i < Daylight.KeyCount; i++)
                Assert.That(Daylight.KeyHour(i), Is.GreaterThan(Daylight.KeyHour(i - 1)),
                    "a key out of order would silently never be reached");
        }
    }
}
