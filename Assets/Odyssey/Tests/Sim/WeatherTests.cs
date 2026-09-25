#nullable enable
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Temperature;
using Odyssey.Sim.Weather;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The sky (design 43 §10): rolled by season, deterministic by seed, blended integer-linear,
    /// written into the outdoor temperature by its one writer, saved and hashed, and forced by the
    /// debug menu through the system rather than around it.
    /// </summary>
    public class WeatherTests
    {
        sealed class Fixture
        {
            public readonly PawnContext Ctx;
            public readonly TemperatureSystem Temperature;
            public readonly WeatherSystem Weather;
            public readonly SimWorld World;

            public Fixture(uint seed = 1, WeatherDef[]? defs = null)
            {
                var size = new GridSize(8, 8, 2);
                var cells = new CellGrid(size);
                var nav = new NavGraph(cells);
                var edifices = new List<PlacedEdifice>();
                var enclosure = new EnclosureGrid(cells, edifices);
                Ctx = new PawnContext(cells, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns())
                {
                    Enclosure = enclosure,
                };
                Ctx.Temperature = Temperature = new TemperatureSystem(Ctx, edifices, new ClimateDef());
                Ctx.Weather = Weather = new WeatherSystem(Ctx, defs ?? WorldContent.Weathers);
                World = new SimWorldBuilder().WithSeed(seed).WithSize(size)
                    .AddSystem(_ => enclosure)
                    .AddSystem(_ => Weather)
                    .AddSystem(_ => Temperature)
                    .AddSnapshotContributor(Weather)
                    .AddIntentHandler(IntentKind.DebugSetWeather, Weather.HandleForce)
                    .Build();
            }

            /// <summary>The kinds the sky rolls over this many game days, one sample a game hour.</summary>
            public List<WeatherKind> Days(int days)
            {
                var seen = new List<WeatherKind>();
                for (int h = 0; h < days * 24; h++)
                {
                    World.Tick(Calendar.TicksPerHour);
                    seen.Add(Weather.Kind);
                }
                return seen;
            }
        }

        [Test]
        public void TheSameSeedRollsTheSameSkyAndAnotherSeedDoesNot()
        {
            List<WeatherKind> a = new Fixture(1).Days(20);
            List<WeatherKind> b = new Fixture(1).Days(20);
            List<WeatherKind> c = new Fixture(99).Days(20);
            Assert.That(a, Is.EqualTo(b), "the same seed must give the same sky, tick for tick");
            Assert.That(c, Is.Not.EqualTo(a), "a different seed rolled the identical twenty days");
            Assert.That(new HashSet<WeatherKind>(a).Count, Is.GreaterThan(1), "twenty days of one kind is not weather");
        }

        [Test]
        public void EachSeasonRollsItsOwnWeights()
        {
            // The pure pick, over every draw a season can make: the counts are the weights exactly,
            // so this holds the table to its own numbers without a statistical tolerance.
            WeatherDef[] defs = WorldContent.Weathers;
            for (int season = 0; season < 3; season++)
            {
                int total = WeatherSystem.TotalWeight(defs, season);
                Assert.That(total, Is.EqualTo(10_000), $"season {season}'s weights are per 10,000");
                var counts = new int[defs.Length];
                for (int draw = 0; draw < total; draw++) counts[WeatherSystem.PickKind(defs, season, draw)]++;
                for (int k = 0; k < defs.Length; k++)
                    Assert.That(counts[k], Is.EqualTo(defs[k].seasonWeights[season]), $"{defs[k].defName} in season {season}");
            }
            // And the shape the owner asked for: showery spring, bright summer, a grey winter.
            Assert.That(defs[(int)WeatherKind.Rain].seasonWeights[0], Is.GreaterThan(defs[(int)WeatherKind.Rain].seasonWeights[2]),
                "Wash is the wet season, not Rime");
            Assert.That(defs[(int)WeatherKind.Clear].seasonWeights[1], Is.GreaterThan(defs[(int)WeatherKind.Clear].seasonWeights[0]),
                "Glare is the sunny season");
        }

        [Test]
        public void RainKeepsItsColourAndOnlyTheGreyDaysDrainIt()
        {
            // Owner, 2026-09-25: "we want to be colourful when it rains ... then have dim days".
            WeatherDef[] defs = WorldContent.Weathers;
            Assert.That(defs[(int)WeatherKind.Rain].gloomPerMille, Is.Zero, "rain drains the colour");
            Assert.That(defs[(int)WeatherKind.Clear].gloomPerMille, Is.Zero);
            Assert.That(defs[(int)WeatherKind.Storm].gloomPerMille, Is.GreaterThan(0), "a storm is the grey day");
            Assert.That(defs[(int)WeatherKind.Cloudy].gloomPerMille, Is.GreaterThan(0));
            Assert.That(defs[(int)WeatherKind.Storm].windPerMille, Is.GreaterThan(1000), "a storm with ordinary wind is a downpour");
            Assert.That(defs[(int)WeatherKind.Storm].rainPerMille, Is.EqualTo(1000));
            for (int s = 0; s < 3; s++)
                Assert.That(defs[(int)WeatherKind.Storm].seasonWeights[s], Is.LessThan(defs[(int)WeatherKind.Rain].seasonWeights[s]),
                    $"season {s}: storm is the rarer kind of wet day");
        }

        [Test]
        public void AHandOverIsMonotoneAndLandsExactlyOnTheNewSky()
        {
            var f = new Fixture();
            f.World.Tick(WeatherSystem.IntervalTicks);   // the first pass starts a sky
            Assert.That(f.World.Intents.Submit(new Intent(IntentKind.DebugSetWeather, default,
                (int)WeatherKind.Clear, 1000, 1)), Is.True);
            f.World.Tick(WeatherSystem.QuickBlendTicks + WeatherSystem.IntervalTicks);
            Assert.That(f.Weather.ViewAt(f.World.CurrentTick).RainPerMille, Is.Zero, "the clear sky never arrived");

            f.World.Intents.Submit(new Intent(IntentKind.DebugSetWeather, default, (int)WeatherKind.Storm, 1000, 0));
            f.World.Tick(1);
            int start = f.World.CurrentTick;
            int last = -1;
            for (int t = start; t <= start + WeatherSystem.BlendTicks + 10; t += 50)
            {
                WeatherView v = f.Weather.ViewAt(t);
                Assert.That(v.RainPerMille, Is.GreaterThanOrEqualTo(last), $"the rain fell back at tick {t}");
                last = v.RainPerMille;
            }
            WeatherView landed = f.Weather.ViewAt(start + WeatherSystem.BlendTicks + 10);
            WeatherView storm = WeatherSystem.Terms(WorldContent.Weathers[(int)WeatherKind.Storm], 1000);
            Assert.That(landed.RainPerMille, Is.EqualTo(storm.RainPerMille));
            Assert.That(landed.GloomPerMille, Is.EqualTo(storm.GloomPerMille));
            Assert.That(landed.TempOffsetC, Is.EqualTo(storm.TempOffsetC));
            Assert.That(landed.Kind, Is.EqualTo(WeatherKind.Storm));
        }

        [Test]
        public void TheSkyMovesTheThermometerByExactlyItsOffset()
        {
            var f = new Fixture();
            f.World.Tick(WeatherSystem.IntervalTicks);
            f.World.Intents.Submit(new Intent(IntentKind.DebugSetWeather, default, (int)WeatherKind.Storm, 1000, 1));
            f.World.Tick(WeatherSystem.QuickBlendTicks + 2 * WeatherSystem.IntervalTicks);

            int tick = f.World.CurrentTick;
            int expected = WeatherSystem.Terms(WorldContent.Weathers[(int)WeatherKind.Storm], 1000).TempOffsetC;
            Assert.That(f.Temperature.WeatherOffsetC, Is.EqualTo(expected), "the one writer did not write the storm's offset");

            // The negative control: the same sky with the offset taken away is the climate alone,
            // so the reading above is the weather and not something else moving the curve.
            int with = f.Temperature.OutdoorTempC(tick);
            f.Temperature.WeatherOffsetC = 0;
            Assert.That(with - f.Temperature.OutdoorTempC(tick), Is.EqualTo(expected));
            Assert.That(expected, Is.LessThan(0), "a storm that warms is not the storm the table means");
        }

        [Test]
        public void TheSkyIsPublishedForTheDrawingAndTheClock()
        {
            var f = new Fixture();
            f.World.Tick(WeatherSystem.IntervalTicks);
            f.World.Intents.Submit(new Intent(IntentKind.DebugSetWeather, default, (int)WeatherKind.Rain, 700, 1));
            f.World.Tick(WeatherSystem.QuickBlendTicks + WeatherSystem.IntervalTicks);
            WeatherView published = f.World.Views.Current.Weather;
            Assert.That(published.Kind, Is.EqualTo(WeatherKind.Rain));
            Assert.That(published.RainPerMille, Is.EqualTo(700));
            Assert.That(published.GloomPerMille, Is.Zero, "rain in colour");
        }

        [Test]
        public void ASaveMidHandOverResumesItAndHashesTheSame()
        {
            var f = new Fixture(seed: 7);
            f.World.Tick(WeatherSystem.IntervalTicks);
            f.World.Intents.Submit(new Intent(IntentKind.DebugSetWeather, default, (int)WeatherKind.Rain, 900, 0));
            f.World.Tick(WeatherSystem.BlendTicks / 3);   // a third of the way through the hand-over

            using var stream = new MemoryStream();
            WorldSave.Save(f.World, stream, new ISaveable[] { f.Weather });
            stream.Position = 0;
            var g = new Fixture(seed: 7);
            WorldSave.Load(g.World, stream, new ISaveable[] { g.Weather });

            var a = StateHash.New();
            f.Weather.ContributeTo(ref a);
            var b = StateHash.New();
            g.Weather.ContributeTo(ref b);
            Assert.That(b.Value, Is.EqualTo(a.Value), "the loaded sky hashes differently from the saved one");
            int tick = f.World.CurrentTick;
            Assert.That(g.Weather.ViewAt(tick + 100).RainPerMille, Is.EqualTo(f.Weather.ViewAt(tick + 100).RainPerMille),
                "the loaded hand-over does not resume where it was");
            Assert.That(f.Weather.ViewAt(tick).RainPerMille, Is.InRange(1, 899), "the save was not mid-blend, so it proved nothing");
        }

        [Test]
        public void AForcedSkyRunsItsSpellAndThenTheSeasonTakesOver()
        {
            var f = new Fixture();
            f.World.Tick(WeatherSystem.IntervalTicks);
            f.World.Intents.Submit(new Intent(IntentKind.DebugSetWeather, default, (int)WeatherKind.Storm, 1000, 1));
            f.World.Tick(1);
            Assert.That(f.Weather.Kind, Is.EqualTo(WeatherKind.Storm));
            int end = f.Weather.SpellEndTick;
            WeatherDef storm = WorldContent.Weathers[(int)WeatherKind.Storm];
            Assert.That(end - f.World.CurrentTick, Is.InRange(storm.minHours * Calendar.TicksPerHour - 2,
                storm.maxHours * Calendar.TicksPerHour), "the forced spell's length is not the storm's own");

            f.World.Tick(end - f.World.CurrentTick + WeatherSystem.IntervalTicks);
            Assert.That(f.Weather.SpellEndTick, Is.GreaterThan(end), "the season never rolled the next spell");
            Assert.That(f.World.Intents.Submit(new Intent(IntentKind.DebugSetWeather, default, 9, 0, 0)), Is.True);
        }
    }
}
