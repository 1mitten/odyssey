#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Expeditions;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>EX4 (design 64 §5, §12): places, the chart, and the campaign in a save.</summary>
    public class CampaignPlanetTests
    {
        [Test]
        public void TheSamePlanetAndHomeSeedTheSamePlaces()
        {
            Campaign a = ExpeditionFixture.Campaign(101u);
            Campaign b = ExpeditionFixture.Campaign(101u);
            Assert.That(a.Places.Count, Is.GreaterThan(1), "a planet has places on it");
            Assert.That(Describe(a), Is.EqualTo(Describe(b)));
            Assert.That(a.ComputeHash(), Is.EqualTo(b.ComputeHash()));
        }

        [TestCase(101u)]
        [TestCase(202u)]
        [TestCase(303u)]
        public void PlacesStandOnlyWhereAColonyCouldAndNeverCrowdEachOther(uint seed)
        {
            Campaign campaign = ExpeditionFixture.Campaign(seed);
            PlanetView planet = campaign.Planet!;
            foreach (Place place in campaign.Places)
            {
                Assert.That(planet.Verdict(place.Tile), Is.EqualTo(SettleVerdict.Settleable), $"place on tile {place.Tile}");
                Assert.That(place.Tile, Is.Not.EqualTo(campaign.HomeTile));
                Assert.That(place.Seed, Is.EqualTo(SiteRules.BoardSeed(seed, place.Tile)), "the board's seed is the tile's");
                foreach (Place other in campaign.Places)
                    if (!ReferenceEquals(other, place))
                        Assert.That(HexMath.Distance(place.Tile, other.Tile, planet.Width), Is.GreaterThanOrEqualTo(PlaceSeeder.Spacing));
            }
        }

        [TestCase(101u)]
        [TestCase(202u)]
        [TestCase(303u)]
        public void TheStartAlwaysKnowsOnePlaceTwoOrThreeHexesOut(uint seed)
        {
            Campaign campaign = ExpeditionFixture.Campaign(seed);
            int near = 0;
            foreach (Place place in campaign.Places)
            {
                int d = HexMath.Distance(campaign.HomeTile, place.Tile, campaign.Planet!.Width);
                if (d >= PlaceSeeder.NearMin && d <= PlaceSeeder.NearMax && place.Has(PlaceState.Discovered)) near++;
                if (place.Has(PlaceState.Discovered))
                    Assert.That(d, Is.LessThanOrEqualTo(Campaign.HomeChartRadius), "only the ring round home is charted at the start");
            }
            Assert.That(near, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void ARevealChartsHexRingsAndAWrapGoesTheShortWayRound()
        {
            var chart = new Chart(128, 64);
            int centre = HexGrid.Index(10, 30, 128);
            Assert.That(chart.Reveal(centre, 0), Is.EqualTo(1));
            Assert.That(chart.Reveal(centre, 1), Is.EqualTo(6), "six neighbours");
            Assert.That(chart.Reveal(centre, 2), Is.EqualTo(12), "twelve more");
            Assert.That(chart.Count, Is.EqualTo(19));

            int west = HexGrid.Index(0, 30, 128), east = HexGrid.Index(127, 30, 128);
            Assert.That(HexMath.Distance(west, east, 128), Is.EqualTo(1), "the planet wraps east to west");
        }

        [Test]
        public void TheCampaignRoundTripsThroughASave()
        {
            Campaign original = ExpeditionFixture.Campaign(101u);
            original.Step(300);
            Place far = original.Places[original.Places.Count - 1];
            original.Reveal(far.Tile, 1);
            byte[] bytes = original.Save(original.Home.Recipe(1));

            Campaign loaded = ExpeditionFixture.Campaign(101u);
            loaded.Load(bytes);
            Assert.That(Describe(loaded), Is.EqualTo(Describe(original)));
            Assert.That(loaded.Chart!.Count, Is.EqualTo(original.Chart!.Count));
            Assert.That(loaded.Ids.Peek, Is.EqualTo(original.Ids.Peek));
            Assert.That(loaded.ComputeHash(), Is.EqualTo(original.ComputeHash()));

            loaded.Step(200);
            original.Step(200);
            Assert.That(loaded.ComputeHash(), Is.EqualTo(original.ComputeHash()), "and goes on the same");
        }

        [Test]
        public void AFormatElevenSaveLoadsAsACampaignWithNothingOnTheRoad()
        {
            Campaign original = ExpeditionFixture.Campaign(101u);
            original.Step(100);
            byte[] plain = original.Home.Save(original.Home.Recipe(1));
            Assume.That(System.BitConverter.ToInt32(plain, 8), Is.EqualTo(WorldSave.CurrentFormatVersion));
            byte[] eleven = SaveFixtures.AsFormat(plain, 11);

            Campaign loaded = ExpeditionFixture.Campaign(101u);
            SaveHeader header = loaded.Load(eleven);
            Assert.That(header.FormatVersion, Is.EqualTo(11));
            Assert.That(loaded.Boards.Count, Is.EqualTo(1));
            Assert.That(Describe(loaded), Is.EqualTo(Describe(ExpeditionFixture.Campaign(101u))),
                "a colony from before expeditions starts with the places a new campaign seeds");
            Assert.That(loaded.Home.World.ComputeStateHash().Value, Is.EqualTo(original.Home.World.ComputeStateHash().Value));
        }

        static string Describe(Campaign campaign)
        {
            var parts = new List<string>();
            foreach (Place p in campaign.Places)
                parts.Add($"{p.Tile}:{p.Situation}:{p.Seed}:{(int)p.State}:{p.Visits}:{p.Site.BiomeDefName}");
            return string.Join(" ", parts);
        }
    }
}
