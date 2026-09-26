#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Expeditions
{
    /// <summary>What has happened at a place (design 64 §7b). Flags, so a place can be both cleared and looted.</summary>
    [Flags]
    public enum PlaceState
    {
        None = 0,

        /// <summary>Charted: the World tab shows it and an expedition may be sent there.</summary>
        Discovered = 1,

        /// <summary>Somebody has stood on its board at least once.</summary>
        Visited = 2,

        /// <summary>Every guard is down, dead or gone. A revisit places none.</summary>
        Cleared = 4,

        /// <summary>Every cache stack has been taken. A revisit places no cache.</summary>
        Looted = 8,
    }

    /// <summary>
    /// A place on the planet (design 64 §5, §7b): a tile, the situation it holds, the seed its board
    /// is built from, and what has happened there.
    ///
    /// <para><b>Saved whole</b>, the tile's facts included, for the reason a colony's own site is
    /// (design 59 §8): the planet is regenerated from its seed whenever it is wanted, and a retune
    /// of the generator must not move a place the player has already found, or change the board
    /// they fought over.</para>
    /// </summary>
    public sealed class Place
    {
        public int Tile { get; }

        /// <summary>An index into <c>WorldContent.Situations</c>.</summary>
        public int Situation { get; }

        /// <summary>The board's seed: the same board every visit, with <see cref="State"/> applied.</summary>
        public uint Seed { get; }

        /// <summary>The tile as it was when the place was seeded.</summary>
        public SiteTile Site { get; }

        public PlaceState State { get; internal set; }

        /// <summary>How many times a party has arrived here.</summary>
        public int Visits { get; internal set; }

        public bool Has(PlaceState flag) => (State & flag) == flag;

        public Place(int tile, int situation, uint seed, SiteTile site, PlaceState state = PlaceState.None, int visits = 0)
        {
            Tile = tile;
            Situation = situation;
            Seed = seed;
            Site = site;
            State = state;
            Visits = visits;
        }

        internal void ContributeTo(ref StateHash hash)
        {
            hash.Add(Tile);
            hash.Add(Situation);
            hash.Add(Seed);
            hash.Add((int)State);
            hash.Add(Visits);
        }
    }
}
