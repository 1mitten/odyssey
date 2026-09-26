#nullable enable
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The one place a number's colour is decided (design 59): the scale's arithmetic, the ramp's
    /// ends and middle, and that every surface the owner named reads its colour from here.
    /// </summary>
    public sealed class StatInksTests
    {
        [TearDown]
        public void RestoreTheStyle() => StatInks.Blend = true;

        [Test]
        public void AScaleScoresItsThreeAnchorsAndClampsPastTheEnds()
        {
            var rising = new StatScale(0, 20, 40);
            Assert.That(rising.Score(-5), Is.EqualTo(0));
            Assert.That(rising.Score(0), Is.EqualTo(0));
            Assert.That(rising.Score(10), Is.EqualTo(250));
            Assert.That(rising.Score(20), Is.EqualTo(500));
            Assert.That(rising.Score(30), Is.EqualTo(750));
            Assert.That(rising.Score(40), Is.EqualTo(1000));
            Assert.That(rising.Score(90), Is.EqualTo(1000));
        }

        /// <summary>Pain is good low and bad high: a scale may run downwards.</summary>
        [Test]
        public void AScaleMayRunDownwards()
        {
            Assert.That(StatInks.Pain.Score(0), Is.EqualTo(1000));
            Assert.That(StatInks.Pain.Score(300), Is.EqualTo(500));
            Assert.That(StatInks.Pain.Score(800), Is.EqualTo(0));
        }

        [Test]
        public void TheRampRunsRedAmberGreen()
        {
            Assert.That(StatInks.Ink(0), Is.EqualTo(HudTheme.Bad));
            Assert.That(StatInks.Ink(500), Is.EqualTo(HudTheme.Warn));
            Assert.That(StatInks.Ink(1000), Is.EqualTo(HudTheme.Good));
            Assert.That(StatInks.Ink(0, StatPalette.World), Is.EqualTo(StatInks.WorldBad));
            Assert.That(StatInks.Ink(1000, StatPalette.World), Is.EqualTo(StatInks.WorldGood));
        }

        /// <summary>Blended, a figure getting worse gets redder; banded, it is the nearest anchor's colour.</summary>
        [Test]
        public void TheOneSwitchChoosesBlendOrBands()
        {
            HudColour blended = StatInks.Ink(250);
            Assert.That(blended, Is.Not.EqualTo(HudTheme.Bad).And.Not.EqualTo(HudTheme.Warn));
            Assert.That(blended.G, Is.InRange(HudTheme.Bad.G, HudTheme.Warn.G));

            StatInks.Blend = false;
            Assert.That(StatInks.Ink(200), Is.EqualTo(HudTheme.Bad));
            Assert.That(StatInks.Ink(600), Is.EqualTo(HudTheme.Warn));
            Assert.That(StatInks.Ink(800), Is.EqualTo(HudTheme.Good));
        }

        /// <summary>The owner's words: "0% armour should be red".</summary>
        [Test]
        public void NoArmourIsRedAndFortyPerCentIsGreen()
        {
            Assert.That(StatInks.Ink(StatInks.Armour, 0), Is.EqualTo(HudTheme.Bad));
            Assert.That(StatInks.Ink(StatInks.Armour, 20), Is.EqualTo(HudTheme.Warn));
            Assert.That(StatInks.Ink(StatInks.Armour, 40), Is.EqualTo(HudTheme.Good));
        }

        /// <summary>"Good temperature is green, amber if questionable and more towards red if not good."</summary>
        [Test]
        public void ATemperatureIsGreenInsideTheComfortableRangeAndReddensOutsideIt()
        {
            Assert.That(StatInks.Temperature(2_000), Is.EqualTo(HudTheme.Good), "20 °C");
            Assert.That(StatInks.Temperature(1_000), Is.EqualTo(HudTheme.Warn), "10 °C, 6 below");
            Assert.That(StatInks.Temperature(3_200), Is.EqualTo(HudTheme.Warn), "32 °C, 6 above");
            Assert.That(StatInks.Temperature(400), Is.EqualTo(HudTheme.Bad), "4 °C, 12 below");
            Assert.That(StatInks.Temperature(-2_000), Is.EqualTo(HudTheme.Bad), "Rime");
            Assert.That(StatInks.Comfort(StatInks.ComfortLow, StatInks.ComfortHigh, 3_600),
                Is.LessThan(StatInks.Comfort(StatInks.ComfortLow, StatInks.ComfortHigh, 3_000)),
                "hotter is worse past the range");
        }

        /// <summary>A coat that keeps her warm to 4 °C makes 4 °C outside green: the range decides.</summary>
        [Test]
        public void WarmthIsJudgedAgainstTheWeatherOutside()
        {
            Assert.That(GearModel.Ink(GearEffectKind.Warmth, 0, 400, 2_600, 400), Is.EqualTo(HudTheme.Good));
            Assert.That(GearModel.Ink(GearEffectKind.Warmth, 0, 1_600, 2_600, 400), Is.EqualTo(HudTheme.Bad),
                "the control: the bare jumpsuit at 4 °C");
            Assert.That(GearModel.Ink(GearEffectKind.Warmth, 0, 1_600, 2_600, null), Is.EqualTo(HudTheme.TextPrimary),
                "no reading, no judgement");
        }

        /// <summary>The owner: "keep rain a neutral bright white". The kit is a count, not a verdict.</summary>
        [Test]
        public void RainAndTheKitAreNeverJudged()
        {
            Assert.That(GearModel.Ink(GearEffectKind.Rain, 0, 0, 0, -2_000), Is.EqualTo(HudTheme.TextPrimary));
            Assert.That(GearModel.Ink(GearEffectKind.Kit, 0, 0, 0, -2_000), Is.EqualTo(HudTheme.TextPrimary));
        }

        /// <summary>The health bar, the need bars' scale and the world's deeper inks, from the one table.</summary>
        [Test]
        public void TheHealthBarReadsTheTable()
        {
            Assert.That(CombatFeedbackModel.HealthBarColour(1_000, 1_000), Is.EqualTo(StatInks.WorldGood));
            Assert.That(CombatFeedbackModel.HealthBarColour(400, 1_000), Is.EqualTo(StatInks.WorldQuestionable));
            Assert.That(CombatFeedbackModel.HealthBarColour(0, 1_000), Is.EqualTo(StatInks.WorldBad));
            Assert.That(CombatFeedbackModel.HealthGood, Is.EqualTo(StatInks.WorldGood), "one copy of the ink");
        }
    }
}
