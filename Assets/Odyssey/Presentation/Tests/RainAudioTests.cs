#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Audio;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Weather;
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The rain's sound (design 43 §7): the light bed for light rain, the heavy bed for heavy rain,
    /// one continuous sound between them, a storm heavier and louder, the birds stepping back, and
    /// nothing underground or on a dry day. The mixing rule is tested against the shipped weather
    /// table, so a retuned sky is heard the way this file says it should be.
    /// </summary>
    public class RainAudioTests
    {
        static WeatherDef Def(WeatherKind kind) => WorldContent.Weathers[(int)kind];

        static WeatherView Sky(WeatherKind kind, int intensity) => WeatherSystem.Terms(Def(kind), intensity);

        static RainLevels Mix(WeatherKind kind, int intensity, bool outdoors = true) =>
            RainMix.Of(Sky(kind, intensity), outdoors);

        // ---- the rule --------------------------------------------------------------------

        [Test]
        public void ADrySkyIsSilentAndHushesNothing()
        {
            foreach (WeatherKind kind in new[] { WeatherKind.Clear, WeatherKind.Cloudy })
            {
                RainLevels mix = Mix(kind, 1000);
                Assert.That(mix.Light, Is.Zero, $"{kind}");
                Assert.That(mix.Heavy, Is.Zero, $"{kind}");
                Assert.That(mix.OutdoorGain, Is.EqualTo(1f), $"{kind} hushed the birds");
            }
            Assert.That(RainMix.Of(WeatherView.None, true).Light, Is.Zero, "a world with no weather");
        }

        [Test]
        public void TheLightestRainTheSkyRollsIsTheLightBedAlone()
        {
            RainLevels drizzle = Mix(WeatherKind.Rain, Def(WeatherKind.Rain).minIntensity);
            Assert.That(drizzle.Light, Is.GreaterThan(0.3f), "a drizzle is heard");
            Assert.That(drizzle.Heavy, Is.Zero, "a drizzle has no roar in it");
            Assert.That(drizzle.OutdoorGain, Is.InRange(0.7f, 0.95f), "the birds step back a little, not away");
        }

        [Test]
        public void ADownpourIsTheHeavyBedWithTheDripsKept()
        {
            RainLevels downpour = Mix(WeatherKind.Rain, 1000);
            Assert.That(downpour.Heavy, Is.EqualTo(1f).Within(0.001f));
            Assert.That(downpour.Light, Is.EqualTo(RainMix.LightUnderHeavy).Within(0.001f),
                "the close drips stay under the roar");
            Assert.That(downpour.OutdoorGain, Is.LessThan(0.5f));
        }

        [Test]
        public void RainHeardFromDrizzleToDownpourNeverDipsOrSteps()
        {
            // Equal power, so the total can only rise with the rain, and no 1-per-mille step moves
            // either bed by more than a hair: the crossfade is one sound, not a switch.
            float lastPower = 0f, lastLight = 0f, lastHeavy = 0f;
            for (int i = 0; i <= 1000; i++)
            {
                RainLevels mix = Mix(WeatherKind.Rain, i);
                float power = mix.Light * mix.Light + mix.Heavy * mix.Heavy;
                Assert.That(power, Is.GreaterThanOrEqualTo(lastPower - 1e-5f), $"the rain got quieter at {i}");
                if (i > 0)
                {
                    Assert.That(Mathf.Abs(mix.Light - lastLight), Is.LessThan(0.02f), $"the light bed stepped at {i}");
                    Assert.That(Mathf.Abs(mix.Heavy - lastHeavy), Is.LessThan(0.02f), $"the heavy bed stepped at {i}");
                }
                lastPower = power;
                lastLight = mix.Light;
                lastHeavy = mix.Heavy;
            }
        }

        [Test]
        public void AStormIsHeavierAndLouderThanRainAsHard()
        {
            int intensity = Def(WeatherKind.Storm).minIntensity;
            RainLevels storm = Mix(WeatherKind.Storm, intensity);
            RainLevels rain = Mix(WeatherKind.Rain, intensity);
            Assert.That(storm.Heavy, Is.GreaterThan(rain.Heavy), "the storm is the roar");
            Assert.That(storm.Heavy, Is.GreaterThan(1f), "and louder than any rain");
            Assert.That(storm.OutdoorGain, Is.LessThan(rain.OutdoorGain), "and the birds go quieter still");
        }

        [Test]
        public void UndergroundThereIsNoRainToHear()
        {
            RainLevels below = Mix(WeatherKind.Storm, 1000, outdoors: false);
            Assert.That(below.Light, Is.Zero);
            Assert.That(below.Heavy, Is.Zero);
            Assert.That(below.OutdoorGain, Is.EqualTo(1f));
        }

        // ---- the director ----------------------------------------------------------------

        GameObject _root = null!;
        AudioCatalogue _catalogue = null!;
        AudioClip _light = null!, _heavy = null!, _outdoor = null!;

        /// <summary>Seven in the morning: the day bed plays.</summary>
        const long DayTick = 7 * 2500;

        const int Surface = 1;

        [SetUp]
        public void Build()
        {
            _root = new GameObject("rain audio root");
            _light = AudioClip.Create("rain-light", 44_100, 2, 44_100, false);
            _heavy = AudioClip.Create("rain-heavy", 44_100, 2, 44_100, false);
            _outdoor = AudioClip.Create("outdoor-day", 44_100, 2, 44_100, false);
            _catalogue = ScriptableObject.CreateInstance<AudioCatalogue>();
            _catalogue.Ambience.Add(new AudioCatalogue.AmbienceDef
                { Id = SoundIds.AmbienceRainLight, Clip = _light, Volume = 0.6f, FadeSeconds = 1f });
            _catalogue.Ambience.Add(new AudioCatalogue.AmbienceDef
                { Id = SoundIds.AmbienceRainHeavy, Clip = _heavy, Volume = 0.6f, FadeSeconds = 1f });
            _catalogue.Outdoor.Add(new AudioCatalogue.PhaseTrackDef
                { Phase = MusicPhase.Day, Clip = _outdoor, Volume = 0.3f, FadeSeconds = 0.5f });
        }

        [TearDown]
        public void Clear()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_catalogue);
        }

        AudioDirector Make() => new(_catalogue, null, new GridSize(8, 8, 2), _root.transform, 0, Surface);

        static WorldSnapshot Frame(in WeatherView sky)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite((int)DayTick, new GridSize(8, 8, 2), 0);
            snapshot.SetWeather(sky);
            return snapshot;
        }

        static void Advance(AudioDirector audio, in WeatherView sky, float seconds, int layer = Surface)
        {
            for (float t = 0f; t < seconds; t += 0.05f)
                audio.Sync(0.05f, Frame(sky), Vector3.zero, Vector3.zero, layer);
        }

        [Test]
        public void RainStartsBothBedsAndADrySkyStopsThem()
        {
            using AudioDirector audio = Make();
            Advance(audio, WeatherView.None, 1f);
            Assert.That(audio.RainSounding, Is.False, "a dry sky runs no rain voice");

            Advance(audio, Sky(WeatherKind.Rain, 650), 8f);
            Assert.That(audio.RainSounding, Is.True);
            Assert.That(audio.RainLightVolume, Is.GreaterThan(0f), "rain in the middle is both beds");
            Assert.That(audio.RainHeavyVolume, Is.GreaterThan(0f));

            Advance(audio, WeatherView.None, 30f);
            Assert.That(audio.RainSounding, Is.False, "the rain ended and the voices kept running");
        }

        [Test]
        public void AForcedChangeOfSkySwellsRatherThanSteps()
        {
            using AudioDirector audio = Make();
            Advance(audio, Sky(WeatherKind.Rain, 300), 8f);
            float before = audio.RainHeavyLevel;
            audio.Sync(0.05f, Frame(Sky(WeatherKind.Storm, 1000)), Vector3.zero, Vector3.zero, Surface);
            Assert.That(audio.RainHeavyLevel - before, Is.LessThan(0.1f), "the storm arrived in one frame");
            Advance(audio, Sky(WeatherKind.Storm, 1000), 8f);
            Assert.That(audio.RainHeavyLevel, Is.GreaterThan(1f), "and did arrive");
        }

        [Test]
        public void TheBirdsStepBackUnderADownpour()
        {
            AudioDirector dry = Make();
            Advance(dry, WeatherView.None, 8f);
            float birds = dry.OutdoorLevel;
            Assume.That(birds, Is.GreaterThan(0f), "the day bed plays");
            dry.Dispose();

            using AudioDirector wet = Make();
            Advance(wet, Sky(WeatherKind.Rain, 1000), 8f);
            Assert.That(wet.OutdoorLevel, Is.LessThan(birds * 0.5f));
        }

        [Test]
        public void TheSliceUnderTheSurfaceHearsNoRain()
        {
            using AudioDirector audio = Make();
            Advance(audio, Sky(WeatherKind.Storm, 1000), 8f, layer: 0);
            Assert.That(audio.RainSounding, Is.False);
        }
    }
}
