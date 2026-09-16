#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Worldgen.Natural
{
    /// <summary>
    /// The one entry point that knows about more than one kind of map.
    ///
    /// A caller that does not care which generator runs asks for a <see cref="MapType"/> and gets
    /// a <see cref="MapGenOutcome"/>; a caller that does care still calls
    /// <see cref="WorldGenerator"/> or <see cref="NaturalMapGenerator"/> directly, and the
    /// ruined-city path through here is a single delegating call with nothing added to it.
    ///
    /// The map type is read from the def, by <see cref="TypeOf"/>, which is the only place in the
    /// codebase that has to change when <c>mapType</c> moves up onto <see cref="MapGenDef"/> —
    /// see the note on <see cref="NaturalMapGenDef"/> for why it is not there yet.
    /// </summary>
    public static class MapGenerator
    {
        /// <summary>
        /// Which generator this def asks for. A plain <see cref="MapGenDef"/> means the
        /// ruined city, which keeps every existing caller and every existing save exactly as it
        /// was: the new behaviour is opt-in and the old behaviour is the default.
        /// </summary>
        public static MapType TypeOf(MapGenDef gen)
        {
            if (gen == null) throw new ArgumentNullException(nameof(gen));
            return gen is NaturalMapGenDef natural ? natural.mapType : MapType.RuinedCity;
        }

        /// <summary>Default parameters for a map type at a given size.</summary>
        public static MapGenDef DefaultDef(MapType type, GridSize size)
        {
            switch (type)
            {
                case MapType.Natural: return NaturalMapGenDef.For(size);
                case MapType.RuinedCity: return MapGenDef.For(size);
                default: throw new ArgumentOutOfRangeException(nameof(type), $"Unknown map type {type}.");
            }
        }

        /// <summary>Generate a map of the given type with the default parameters for the grid.</summary>
        public static MapGenOutcome Generate(CellGrid grid, uint seed, MapType type)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            return Generate(grid, seed, DefaultDef(type, grid.Size));
        }

        /// <summary>
        /// Generate the map this def describes. The ruined-city branch is
        /// <see cref="WorldGenerator.Generate(CellGrid, uint, MapGenDef)"/> and nothing else, so
        /// its behaviour, its templates and its hashes are unchanged.
        /// </summary>
        public static MapGenOutcome Generate(CellGrid grid, uint seed, MapGenDef gen)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (gen == null) throw new ArgumentNullException(nameof(gen));

            var type = TypeOf(gen);
            switch (type)
            {
                case MapType.Natural:
                    if (gen is NaturalMapGenDef natural)
                        return new MapGenOutcome(NaturalMapGenerator.Generate(grid, seed, natural));
                    throw new ArgumentException(
                        "A natural map needs a NaturalMapGenDef; a plain MapGenDef carries none of its parameters.",
                        nameof(gen));

                case MapType.RuinedCity:
                    return new MapGenOutcome(WorldGenerator.Generate(grid, seed, gen));

                default:
                    throw new ArgumentOutOfRangeException(nameof(gen), $"Unknown map type {type}.");
            }
        }
    }

    /// <summary>
    /// The result of generating a map of an unknown type: the few things every map has, plus the
    /// generator-specific result for a caller that knows which one it asked for.
    /// </summary>
    public sealed class MapGenOutcome
    {
        static readonly string[] NoModules = new string[0];

        internal MapGenOutcome(NaturalMapResult natural)
        {
            Type = MapType.Natural;
            Natural = natural;
            GridHash = natural.GridHash;
            StartCell = natural.StartCell;
            ModuleIds = natural.ModuleIds;
        }

        internal MapGenOutcome(WorldGenResult city)
        {
            Type = MapType.RuinedCity;
            City = city;
            GridHash = city.GridHash;
            StartCell = city.StartCell;
            // The city generator names its modules on the templates it stamps rather than in one
            // list, so there is nothing to report here; the catalogue already carries them.
            ModuleIds = NoModules;
        }

        public MapType Type { get; }

        /// <summary>Everything standing in a cell, whichever generator placed it. Handles in <c>CellGrid.Edifice</c> index this list.</summary>
        public System.Collections.Generic.IReadOnlyList<PlacedEdifice> Edifices =>
            City != null ? City.Context.Edifices : Natural!.Context.Edifices;

        /// <summary>Set when <see cref="Type"/> is <see cref="MapType.Natural"/>.</summary>
        public NaturalMapResult? Natural { get; }

        /// <summary>Set when <see cref="Type"/> is <see cref="MapType.RuinedCity"/>.</summary>
        public WorldGenResult? City { get; }

        public ulong GridHash { get; }
        public CellRef StartCell { get; }

        /// <summary>
        /// Stairs and ladders the generator declared, both ends, for
        /// <see cref="ConnectorRegistrar"/> to turn into portal edges. Empty for a natural map,
        /// which has no vertical connectors of its own — its layers are strata, and a colonist
        /// reaches them by digging, which builds connectors rather than finding them.
        /// </summary>
        public IReadOnlyList<StampedConnector> Connectors =>
            City != null ? City.Context.Connectors : (IReadOnlyList<StampedConnector>)NoConnectors;

        static readonly StampedConnector[] NoConnectors = new StampedConnector[0];

        /// <summary>Presentation module ids this map needs in the catalogue. Ids only, never modules.</summary>
        public IReadOnlyList<string> ModuleIds { get; }

        public override string ToString() => $"{Type} start {StartCell} hash {GridHash:x16}";
    }
}
