#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Weather;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using Odyssey.Sim.Worldgen.Planet;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The planet site's seam (design 59 §3–§8): a site shapes the board's hills, its climate and
    /// its rain, and **no site changes nothing**. That last sentence is the one every golden hangs
    /// on, so it is asserted field for field rather than trusted.
    /// </summary>
    public class SiteSeamTests
    {
        static readonly GridSize Standard = new GridSize(120, 120, 16);

        static SiteTile Site(HillBand hills, int latitude = SiteRules.ReferenceLatitudePerMille,
            int meanC = SiteRules.ReferenceMeanTempC, int rainMm = SiteRules.ReferenceRainfallMm) =>
            new SiteTile(100, 36, 1, "Biome_Meadow", hills, latitude, meanC, rainMm, 200, 0, false);

        static NaturalMapGenDef DefFor(GridSize size, SiteTile? site) =>
            (NaturalMapGenDef)ColonyWorld.DefFor(MapType.Natural, size, barren: true, wooded: true, site: site);

        // ---- the board ------------------------------------------------------------------------

        [Test]
        public void NoSiteIsThePlayedBoard()
        {
            List<string> differences = DefComparison.Differences(PlayedMap.Def(Standard), DefFor(Standard, null), "Def");
            Assert.That(differences, Is.Empty, string.Join("\n", differences));
        }

        /// <summary>Rolling is today's board by construction (design 59 §5): relief 2 and ×1 of every density.</summary>
        [Test]
        public void ARollingSiteIsThePlayedBoard()
        {
            List<string> differences = DefComparison.Differences(PlayedMap.Def(Standard),
                DefFor(Standard, Site(HillBand.Rolling)), "Def");
            Assert.That(differences, Is.Empty, string.Join("\n", differences));
        }

        /// <summary>The control for the two above: a comparison that finds nothing must be able to find something.</summary>
        [Test]
        public void AHillySiteIsNotThePlayedBoard()
        {
            Assert.That(DefComparison.Differences(PlayedMap.Def(Standard), DefFor(Standard, Site(HillBand.Hilly)), "Def"),
                Is.Not.Empty);
        }

        [Test]
        public void TheBandsClimbInOrder()
        {
            HillBand[] order = { HillBand.Flat, HillBand.Rolling, HillBand.Hilly, HillBand.Mountainous };
            for (int i = 1; i < order.Length; i++)
            {
                NaturalMapGenDef lower = DefFor(Standard, Site(order[i - 1]));
                NaturalMapGenDef higher = DefFor(Standard, Site(order[i]));
                Assert.That(higher.surfaceRelief, Is.GreaterThan(lower.surfaceRelief), $"{order[i]} relief");
                Assert.That(higher.outcropsPer10000Columns, Is.GreaterThan(lower.outcropsPer10000Columns), $"{order[i]} outcrops");
                Assert.That(higher.cavernsPer10000Columns, Is.GreaterThanOrEqualTo(lower.cavernsPer10000Columns), $"{order[i]} caverns");
            }
        }

        /// <summary>
        /// Design 59 §5's table in its own numbers, against the played board. The order test above
        /// passed a Hilly board with Rolling's three caverns, because ×1.333 of 3 truncated to 3.
        /// </summary>
        [TestCase(HillBand.Flat, 1, 8, 3)]
        [TestCase(HillBand.Rolling, 2, 16, 3)]
        [TestCase(HillBand.Hilly, 3, 24, 4)]
        [TestCase(HillBand.Mountainous, 4, 40, 6)]
        public void EachBandIsTheTablesNumbers(HillBand band, int relief, int outcrops, int caverns)
        {
            NaturalMapGenDef gen = DefFor(Standard, Site(band));
            Assert.That(gen.surfaceRelief, Is.EqualTo(relief), "relief");
            Assert.That(gen.outcropsPer10000Columns, Is.EqualTo(outcrops), "outcrops per 10,000 columns");
            Assert.That(gen.cavernsPer10000Columns, Is.EqualTo(caverns), "caverns per 10,000 columns");
        }

        /// <summary>A band scales the preset's densities rather than overwriting them, so a bare board stays bare.</summary>
        [Test]
        public void AHillBandScalesTheBareBoardsNothingToNothing()
        {
            var bare = (NaturalMapGenDef)ColonyWorld.DefFor(MapType.Natural, Standard, barren: true, wooded: false,
                site: Site(HillBand.Mountainous));
            Assert.That(bare.outcropsPer10000Columns, Is.Zero);
            Assert.That(bare.cavernsPer10000Columns, Is.Zero);
        }

        [Test]
        public void OnlyAMountainousSiteDeepensTheBoard()
        {
            // 32 since every offered board went to 32 (design 62, DM2): a mountain is never
            // shallower than the rest, so on the boards offered today it deepens nothing.
            Assert.That(SiteRules.BoardLayers(16, HillBand.Mountainous), Is.EqualTo(32));
            Assert.That(SiteRules.BoardLayers(GridSize.OfferedLayers, HillBand.Mountainous), Is.EqualTo(GridSize.OfferedLayers));
            Assert.That(SiteRules.BoardLayers(40, HillBand.Mountainous), Is.EqualTo(40), "never shallower than chosen");
            foreach (HillBand band in new[] { HillBand.Flat, HillBand.Rolling, HillBand.Hilly })
                Assert.That(SiteRules.BoardLayers(16, band), Is.EqualTo(16), band.ToString());
        }

        /// <summary>
        /// Why a mountainous site is deep (design 38 §13, 28-map-size §11): at 16 layers relief 4
        /// leaves no rock under the lowest valley, and at 24 it left some; it is 32 now, with every
        /// board (design 62, DM2). The 16-layer board is the control — if it had rock too, the
        /// depth would be buying nothing.
        /// </summary>
        [Test]
        public void AMountainousSiteKeepsRockUnderItsValleys()
        {
            var deep = new GridSize(120, 120, SiteRules.BoardLayers(16, HillBand.Mountainous));
            Assert.That(RockUnderLowest(deep), Is.GreaterThan(0), $"{deep.SizeY} layers");
            Assert.That(RockUnderLowest(Standard), Is.LessThanOrEqualTo(0), "the control: 16 layers at relief 4");
        }

        static int RockUnderLowest(GridSize size)
        {
            int least = int.MaxValue;
            foreach (uint seed in new[] { 1u, 2u, 3u })
            {
                NaturalMapGenDef def = DefFor(size, Site(HillBand.Mountainous));
                NaturalMapResult result = NaturalMapGenerator.Generate(new Odyssey.Sim.World.CellGrid(size), seed, def);
                least = Math.Min(least, result.Report.SurfaceMinY - def.subsoilDepth - def.bedrockLayers);
            }
            return least;
        }

        // ---- the climate ----------------------------------------------------------------------

        /// <summary>
        /// The reference site (53°, 9 °C, 1,000 mm) is today's temperate curve to the centi-degree
        /// (design 59 §7), so the curve is scaled round what has been played and tuned.
        /// </summary>
        [Test]
        public void TheReferenceSiteIsTodaysClimate()
        {
            ClimateDef today = WorldContent.Climate;
            ClimateDef site = SiteClimate.For(Site(HillBand.Rolling), today);

            Assert.That(site.annualMeanC, Is.EqualTo(today.annualMeanC));
            Assert.That(site.monthlyOffsetC, Is.EqualTo(today.monthlyOffsetC));
            Assert.That(site.dailyAmplitudeC, Is.EqualTo(today.dailyAmplitudeC));
            Assert.That(site.groundOneLayerDampingPerMille, Is.EqualTo(today.groundOneLayerDampingPerMille));
        }

        /// <summary>The control: move the site and the curve must move.</summary>
        [Test]
        public void AColderSiteFurtherNorthIsAnotherClimate()
        {
            ClimateDef today = WorldContent.Climate;
            ClimateDef site = SiteClimate.For(Site(HillBand.Rolling, latitude: 700, meanC: 400), today);
            Assert.That(site.annualMeanC, Is.EqualTo(400));
            Assert.That(site.monthlyOffsetC, Is.Not.EqualTo(today.monthlyOffsetC));
        }

        [Test]
        public void TheSeasonsBiteHarderAwayFromTheEquator()
        {
            ClimateDef today = WorldContent.Climate;
            int Swing(int latitude)
            {
                List<int> offsets = SiteClimate.For(Site(HillBand.Rolling, latitude), today).monthlyOffsetC;
                int min = int.MaxValue, max = int.MinValue;
                foreach (int o in offsets) { min = Math.Min(min, o); max = Math.Max(max, o); }
                return max - min;
            }

            Assert.That(Swing(0), Is.LessThan(Swing(SiteRules.ReferenceLatitudePerMille)));
            Assert.That(Swing(SiteRules.ReferenceLatitudePerMille), Is.LessThan(Swing(-900)), "the south bites the same");
            Assert.That(SiteRules.SeasonalityPerMille(0), Is.EqualTo(150));
            Assert.That(SiteRules.SeasonalityPerMille(SiteRules.ReferenceLatitudePerMille), Is.EqualTo(1000));
            Assert.That(SiteRules.SeasonalityPerMille(1000), Is.EqualTo(1590));
        }

        [Test]
        public void DryAirSwingsMoreBetweenNoonAndNight()
        {
            Assert.That(SiteRules.DailyAmplitudeC(500, 400), Is.GreaterThan(SiteRules.DailyAmplitudeC(500, 1000)));
            Assert.That(SiteRules.DailyAmplitudeC(500, 1000), Is.EqualTo(500));
            Assert.That(SiteRules.DailyAmplitudeC(500, 1800), Is.LessThan(500));
        }

        /// <summary>The shared Def is never written through: a second colony must not inherit the first's climate.</summary>
        [Test]
        public void ASiteNeverRetunesTheSharedClimate()
        {
            ClimateDef today = WorldContent.Climate;
            ulong before = DefComparison.Fingerprint(today, "Climate");
            SiteClimate.For(Site(HillBand.Rolling, latitude: 950, meanC: -1800, rainMm: 200), today);
            Assert.That(DefComparison.Fingerprint(today, "Climate"), Is.EqualTo(before));
        }

        // ---- the weather ----------------------------------------------------------------------

        /// <summary>No site is 1000, and 1000 is today's table to the draw.</summary>
        [Test]
        public void TheTablesOwnWetnessRollsExactlyAsBefore()
        {
            WeatherDef[] defs = WorldContent.Weathers;
            for (int season = 0; season < 3; season++)
            {
                int raw = 0;
                foreach (WeatherDef def in defs) raw += Math.Max(0, def.seasonWeights[season]);
                Assert.That(WeatherSystem.TotalWeight(defs, season, 1000), Is.EqualTo(raw));
                for (int draw = 0; draw < raw; draw += 97)
                    Assert.That(WeatherSystem.PickKind(defs, season, draw, 1000),
                        Is.EqualTo(WeatherSystem.PickKind(defs, season, draw)));
            }
        }

        [Test]
        public void AWetSiteRainsMoreOftenAndADryOneLess()
        {
            WeatherDef[] defs = WorldContent.Weathers;
            int Rainy(int wet)
            {
                int total = 0;
                foreach (WeatherDef def in defs)
                    if (def.rainPerMille > 0) total += WeatherSystem.Weight(def, 0, wet);
                return total * 10000 / WeatherSystem.TotalWeight(defs, 0, wet);
            }

            Assert.That(Rainy(1600), Is.GreaterThan(Rainy(1000)));
            Assert.That(Rainy(400), Is.LessThan(Rainy(1000)));
            Assert.That(SiteRules.WetPerMille(SiteRules.ReferenceRainfallMm), Is.EqualTo(1000));
        }

        // ---- the colony -----------------------------------------------------------------------

        [Test]
        public void AColonyWithNoSiteLivesInTheContentsClimate()
        {
            ColonyWorld colony = Build(site: null, new GridSize(60, 60, 16));
            Assert.That(colony.Pawns.Temperature!.Climate, Is.SameAs(WorldContent.Climate));
            Assert.That(colony.Pawns.Weather!.WetPerMille, Is.EqualTo(1000));
        }

        [Test]
        public void AColonyOnASiteLivesInTheSitesClimate()
        {
            SiteTile site = Site(HillBand.Hilly, latitude: 300, meanC: 1500, rainMm: 1400);
            ColonyWorld colony = Build(site, new GridSize(60, 60, 16));
            Assert.That(colony.Pawns.Temperature!.Climate.annualMeanC, Is.EqualTo(1500));
            Assert.That(colony.Pawns.Weather!.WetPerMille, Is.EqualTo(1400));
        }

        [Test]
        public void TheBoardSeedIsTheTilesOfTheWorld()
        {
            Assert.That(SiteRules.BoardSeed(7u, 100), Is.EqualTo(SiteRules.BoardSeed(7u, 100)));
            var seen = new HashSet<uint>();
            for (int tile = 0; tile < 2048; tile++) seen.Add(SiteRules.BoardSeed(7u, tile));
            Assert.That(seen.Count, Is.EqualTo(2048), "every tile of one world its own board");
            Assert.That(SiteRules.BoardSeed(8u, 100), Is.Not.EqualTo(SiteRules.BoardSeed(7u, 100)));
        }

        // ---- the save -------------------------------------------------------------------------

        [Test]
        public void TheSiteRoundTripsThroughTheHeader()
        {
            SiteTile site = Site(HillBand.Hilly, latitude: -420, meanC: 1234, rainMm: 777);
            ColonyWorld colony = Build(site, new GridSize(60, 60, 16), worldSeed: 424242u);
            byte[] bytes = colony.Save(colony.Recipe(3));

            SaveHeader header = WorldSave.ReadHeaderOnly(new MemoryStream(bytes));
            Assert.That(header.FormatVersion, Is.EqualTo(11));
            Assert.That(header.Recipe.Site, Is.EqualTo(site));
            Assert.That(header.Recipe.WorldSeed, Is.EqualTo(424242u));
        }

        /// <summary>A loaded sited colony rebuilds the board it was saved on and hashes the same.</summary>
        [Test]
        public void ASitedColonyReloadsIntoTheSameState()
        {
            SiteTile site = Site(HillBand.Flat, latitude: 200, meanC: 1600, rainMm: 1300);
            var size = new GridSize(60, 60, 16);
            ColonyWorld colony = Build(site, size);
            colony.World.Tick(600);
            ulong before = colony.World.ComputeStateHash().Value;
            byte[] bytes = colony.Save(colony.Recipe(1));

            SaveHeader header = WorldSave.ReadHeaderOnly(new MemoryStream(bytes));
            ColonyWorld again = ColonyWorld.Build(new ColonyRequest
            {
                Size = header.Size,
                Seed = header.Seed,
                Barren = header.Recipe.Barren,
                Wooded = header.Recipe.Wooded,
                Site = header.Recipe.Site,
                WorldSeed = header.Recipe.WorldSeed,
                Scenario = ScenarioDef.Bare(),
            });
            again.Load(new MemoryStream(bytes));
            Assert.That(again.World.ComputeStateHash().Value, Is.EqualTo(before));
            Assert.That(again.Pawns.Temperature!.Climate.annualMeanC, Is.EqualTo(1600));
        }

        [Test]
        public void ASaveWithNoSiteSaysSo()
        {
            ColonyWorld colony = Build(site: null, new GridSize(60, 60, 16));
            SaveHeader header = WorldSave.ReadHeaderOnly(new MemoryStream(colony.Save(colony.Recipe(1))));
            Assert.That(header.Recipe.Site, Is.Null);
        }

        /// <summary>
        /// The bytes a build at format 10 wrote: the header up to the board flags and no site. Written
        /// by hand, as <c>SaveFormatV2Tests</c>' fixtures are, because the current writer never will.
        /// </summary>
        [Test]
        public void AFormatTenSaveReadsWithNoSite()
        {
            const ulong magic = 0x59455353594451;
            using var stream = new MemoryStream();
            using (var binary = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                binary.Write(magic);
                binary.Write(10);
                binary.Write(99u);
                binary.Write(60);
                binary.Write(60);
                binary.Write(16);
                binary.Write(0);
                binary.Write((int)MapType.Natural);
                binary.Write(0); // scenario ""
                binary.Write(0); // colony ""
                binary.Write(4); // day
                binary.Write(true);
                binary.Write(true);
                binary.Write(0); // no sections
            }
            stream.Position = 0;
            SaveHeader header = WorldSave.ReadHeaderOnly(stream);
            Assert.That(header.FormatVersion, Is.EqualTo(10));
            Assert.That(header.Recipe.Site, Is.Null);
            Assert.That(header.Recipe.Wooded, Is.True, "the fields before the site still read");
        }

        static ColonyWorld Build(SiteTile? site, GridSize size, uint worldSeed = 5u) => ColonyWorld.Build(new ColonyRequest
        {
            Size = size,
            Seed = 20260926u,
            Scenario = ScenarioDef.Bare(),
            Barren = true,
            Wooded = true,
            Site = site,
            WorldSeed = worldSeed,
        });
    }
}
