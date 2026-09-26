#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Expeditions;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Planet;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A planet and a home colony on it, for the expedition tests (design 64). Home stands on the
    /// planet's suggested tile, as a new game does, on a small board so a test is quick.
    /// </summary>
    public static class ExpeditionFixture
    {
        public static readonly GridSize HomeSize = new GridSize(48, 48, 12);

        static readonly Dictionary<uint, PlanetView> Planets = new Dictionary<uint, PlanetView>();

        /// <summary>The planet for a world seed, generated once per test run (about 12 ms each).</summary>
        public static PlanetView Planet(uint worldSeed)
        {
            if (!Planets.TryGetValue(worldSeed, out PlanetView? planet))
            {
                planet = PlanetGenerator.Generate(worldSeed, WorldContent.Planet, WorldContent.Biomes, WorldContent.Climate);
                Planets[worldSeed] = planet;
            }
            return planet;
        }

        public static ColonyRequest HomeRequest(uint worldSeed, int colonists = 3, GridSize? size = null)
        {
            PlanetView planet = Planet(worldSeed);
            SiteTile site = planet.Tile(planet.SuggestedTile);
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = colonists;
            scenario.stockpileCells = 9;
            return new ColonyRequest
            {
                Size = size ?? HomeSize,
                Seed = SiteRules.BoardSeed(worldSeed, site.TileIndex),
                Scenario = scenario,
                Barren = true,
                Wooded = true,
                Site = site,
                WorldSeed = worldSeed,
            };
        }

        /// <summary>A fresh campaign: home built on the planet's suggested tile, places seeded, home charted.</summary>
        public static Campaign Campaign(uint worldSeed, int colonists = 3, GridSize? size = null) =>
            new Campaign(ColonyWorld.Build(HomeRequest(worldSeed, colonists, size)), worldSeed, Planet(worldSeed));
    }
}
