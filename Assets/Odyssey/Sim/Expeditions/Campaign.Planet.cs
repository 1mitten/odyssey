#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.Expeditions
{
    // The planet-level state (design 64 §5): the places seeded at the start and the chart of what
    // has been seen. Both are campaign state, saved in odyssey.campaign and hashed by ComputeHash.
    public sealed partial class Campaign
    {
        /// <summary>How far round home the colony knows the planet at the start (design 64 §5).</summary>
        public const int HomeChartRadius = 3;

        readonly List<Place> _places = new List<Place>();

        /// <summary>Every place seeded on the planet, found or not, in the order they were seeded.</summary>
        public IReadOnlyList<Place> Places => _places;

        /// <summary>What has been seen; null for a campaign with no planet (a colony built without a site).</summary>
        public Chart? Chart { get; private set; }

        /// <summary>Home's tile on the planet, or -1 with no planet.</summary>
        public int HomeTile { get; private set; } = -1;

        void SeedPlanet(ColonyWorld home)
        {
            if (Planet == null || !(home.Request.Site is SiteTile site)) return;
            HomeTile = site.TileIndex;
            Chart = new Chart(Planet.Width, Planet.Height);
            _places.AddRange(PlaceSeeder.Seed(Planet, WorldSeed, WorldContent.Situations, HomeTile));
            Reveal(HomeTile, HomeChartRadius);
        }

        /// <summary>
        /// Chart everything within <paramref name="radius"/> of a tile, and mark every place it
        /// uncovers as discovered. Returns how many places were new.
        /// </summary>
        public int Reveal(int tile, int radius)
        {
            if (Chart == null) return 0;
            Chart.Reveal(tile, radius);
            int found = 0;
            for (int i = 0; i < _places.Count; i++)
            {
                if (_places[i].Has(PlaceState.Discovered) || !Chart.Knows(_places[i].Tile)) continue;
                _places[i].State |= PlaceState.Discovered;
                found++;
            }
            return found;
        }

        /// <summary>The index of the place on a tile, or -1.</summary>
        public int PlaceAt(int tile)
        {
            for (int i = 0; i < _places.Count; i++) if (_places[i].Tile == tile) return i;
            return -1;
        }

        partial void ContributeState(ref StateHash hash)
        {
            hash.Add(HomeTile);
            hash.Add(_places.Count);
            for (int i = 0; i < _places.Count; i++) _places[i].ContributeTo(ref hash);
            if (Chart != null) Chart.ContributeTo(ref hash);
            ContributeRoad(ref hash);
        }

        // The road's own state (design 64 §6c), hashed after the planet's; Campaign.Road.cs fills it.
        partial void ContributeRoad(ref StateHash hash);

        internal void RestorePlanet(int homeTile, byte[]? chart, List<Place> places)
        {
            HomeTile = homeTile;
            _places.Clear();
            _places.AddRange(places);
            if (Chart != null && chart != null) Chart.Unpack(chart);
        }

        internal void RestoreCounters(int nextSlot, int nextId)
        {
            _nextSlot = nextSlot;
            Ids.Reset(nextId);
        }
    }
}
