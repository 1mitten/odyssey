#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Expeditions
{
    /// <summary>
    /// Scatters places over a planet at the start of a campaign (design 64 §5): the same places for
    /// the same world seed and home, every time.
    ///
    /// <para><b>Only where a colony could settle today.</b> A site board is built from its tile the
    /// way a colony's own board is (design 59 §5), and until the next art pack every board is drawn
    /// as meadow; a place on the steppe would build a meadow and lie about where it was. Situations
    /// name their biomes; the seeder also refuses water and sheer tiles, as settling does.</para>
    ///
    /// <para><b>The start always has one known place</b>, two or three hexes out, so the first
    /// expedition has somewhere to go however the dice fell elsewhere.</para>
    /// </summary>
    public static class PlaceSeeder
    {
        /// <summary>No two places closer than this.</summary>
        public const int Spacing = 3;

        /// <summary>The ring the guaranteed first place is drawn from.</summary>
        public const int NearMin = 2, NearMax = 3;

        const uint PlacePurpose = 0x9_1A_CE_5Du;

        public static List<Place> Seed(PlanetView planet, uint worldSeed, SituationDef[] situations, int homeTile)
        {
            var places = new List<Place>();
            if (situations.Length == 0) return places;

            // The guaranteed near place first, so the spacing rule works around it.
            var near = new List<int>();
            var ring = new List<int>();
            HexMath.Within(homeTile, NearMax, planet.Width, planet.Height, ring);
            for (int i = 0; i < ring.Count; i++)
            {
                int d = HexMath.Distance(homeTile, ring[i], planet.Width);
                if (d >= NearMin && d <= NearMax && Eligible(planet, ring[i]) && Pick(planet, situations, ring[i], homeTile, 0u) >= 0)
                    near.Add(ring[i]);
            }
            if (near.Count > 0)
            {
                var rng = DeterministicRandom.ForTick(worldSeed, homeTile, PlacePurpose);
                int tile = near[rng.NextInt(near.Count)];
                places.Add(Make(planet, worldSeed, situations, tile, homeTile));
            }

            for (int tile = 0; tile < planet.TileCount; tile++)
            {
                if (tile == homeTile || !Eligible(planet, tile)) continue;
                var rng = DeterministicRandom.ForTick(worldSeed, tile, PlacePurpose + 1u);
                int situation = Pick(planet, situations, tile, homeTile, rng.NextUInt());
                if (situation < 0) continue;
                if (rng.NextInt(1000) >= situations[situation].perThousandTiles) continue;
                if (TooClose(places, tile, planet.Width)) continue;
                places.Add(new Place(tile, situation, SiteRules.BoardSeed(worldSeed, tile), planet.Tile(tile)));
            }
            return places;
        }

        static Place Make(PlanetView planet, uint worldSeed, SituationDef[] situations, int tile, int homeTile)
        {
            var rng = DeterministicRandom.ForTick(worldSeed, tile, PlacePurpose + 1u);
            int situation = Pick(planet, situations, tile, homeTile, rng.NextUInt());
            return new Place(tile, situation, SiteRules.BoardSeed(worldSeed, tile), planet.Tile(tile));
        }

        static bool Eligible(PlanetView planet, int tile) => planet.Verdict(tile) == SettleVerdict.Settleable;

        static bool TooClose(List<Place> places, int tile, int width)
        {
            for (int i = 0; i < places.Count; i++)
                if (HexMath.Distance(places[i].Tile, tile, width) < Spacing) return true;
            return false;
        }

        /// <summary>A situation for a tile, by weight among those that stand there, or -1.</summary>
        static int Pick(PlanetView planet, SituationDef[] situations, int tile, int homeTile, uint roll)
        {
            string biome = planet.BiomeAt(tile).DefName;
            int distance = HexMath.Distance(homeTile, tile, planet.Width);
            int total = 0;
            for (int i = 0; i < situations.Length; i++)
                if (situations[i].StandsIn(biome) && distance >= situations[i].minHexesFromHome) total += situations[i].weight;
            if (total <= 0) return -1;
            int pick = (int)(roll % (uint)total);
            for (int i = 0; i < situations.Length; i++)
            {
                if (!situations[i].StandsIn(biome) || distance < situations[i].minHexesFromHome) continue;
                pick -= situations[i].weight;
                if (pick < 0) return i;
            }
            return -1;
        }
    }
}
