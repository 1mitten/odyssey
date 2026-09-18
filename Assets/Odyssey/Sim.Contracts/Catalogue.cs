#nullable enable
namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// Def-index constants that published views carry. <see cref="PawnView.JobDef"/> and
    /// <see cref="ThingView.DefIndex"/> are indices into the simulation's job and item tables,
    /// and an index is only a contract if both sides agree what it counts. These constants are
    /// that agreement: the simulation's tables are defined in this order, and interface code
    /// (which may not reference the simulation at all) turns the indices into words against
    /// these numbers and no others.
    /// </summary>
    public static class JobHandle
    {
        public const int Haul = 0;
        public const int Eat = 1;
        public const int Sleep = 2;
        public const int Wander = 3;
        public const int Wait = 4;
        public const int Fell = 5;
        public const int Mine = 6;

        /// <summary>Carry a load of material to a building site that is waiting for it.</summary>
        public const int Deliver = 7;

        /// <summary>Work at a site that has its materials, until the thing stands.</summary>
        public const int Build = 8;

        /// <summary>Take one of our own buildings apart, for half of what it cost.</summary>
        public const int Deconstruct = 9;

        /// <summary>Break the ground of an unplanted zone cell and put a seed in it.</summary>
        public const int Sow = 10;

        /// <summary>Cut a ripe crop and gather what it yields.</summary>
        public const int Harvest = 11;

        public const int Count = 12;
    }

    /// <summary>
    /// See <see cref="JobHandle"/>: plant species, as the zone and plant channels carry them.
    /// <para>Like every table here it is append-only: a zone record stores its plant as one of
    /// these numbers, so the order a save depends on never shifts.</para>
    /// </summary>
    public static class PlantHandle
    {
        public const int Carrot = 0;
        public const int Count = 1;
    }

    /// <summary>See <see cref="JobHandle"/>: item def indices as <see cref="ThingView"/> carries them.</summary>
    public static class ItemHandle
    {
        public const int Meal = 0;
        public const int Salvage = 1;
        public const int Wood = 2;

        /// <summary>Broken rock. Plentiful, heavy, and not yet good for anything but a pile.</summary>
        public const int Stone = 3;

        public const int IronOre = 4;
        public const int Coal = 5;

        /// <summary>
        /// The first raw food. Eaten straight from the field or the pile — the eater scans for
        /// nutrition and does not care that it is not a meal (docs/design/22-growing.md §5).
        /// Six, not two: the item table grew five commodities before the carrot existed, and
        /// handle order is the save contract.
        /// </summary>
        public const int Carrots = 6;
        public const int Count = 7;
    }

    /// <summary>
    /// See <see cref="JobHandle"/>: terrain kinds, as <see cref="CellDetail"/> carries them.
    ///
    /// <para>The order is <c>WorldContent.TerrainOrder</c> — the one list that decides which name
    /// is which index, and the one a save and a state hash already depend on. These constants are
    /// that order restated here so the interface can turn an index into a word without referencing
    /// the simulation, exactly as <see cref="JobHandle"/> does for jobs. A Sim-side test holds the
    /// two tables to each other, because nothing in either assembly can.</para>
    /// </summary>
    public static class TerrainHandle
    {
        public const int Air = 0;
        public const int Pavement = 1;
        public const int CrackedPavement = 2;
        public const int Rubble = 3;
        public const int Soil = 4;
        public const int Gravel = 5;
        public const int EngineeredFill = 6;
        public const int Rock = 7;
        public const int BuriedSeam = 8;
        public const int Salvage = 9;

        /// <summary>The wilderness half, continuing the city's numbering.</summary>
        public const int Grass = 10;
        public const int BareEarth = 11;
        public const int PackedGravel = 12;
        public const int Sand = 13;
        public const int Subsoil = 14;
        public const int Bedrock = 15;
        public const int IronOre = 16;
        public const int CoalSeam = 17;
        public const int ShallowWater = 18;
        public const int DeepWater = 19;
        public const int Marsh = 20;

        public const int Count = 21;
    }

    /// <summary>
    /// See <see cref="JobHandle"/>: what can stand in a cell, as <see cref="CellDetail"/> carries
    /// it. The order is the simulation's own edifice numbering — <c>CoreContent</c>'s ids 0 to 9,
    /// the trees continuing from 10 — restated here for the interface to name.
    /// </summary>
    public static class EdificeHandle
    {
        public const int None = 0;
        public const int Wall = 1;
        public const int Door = 2;
        public const int Window = 3;
        public const int Pillar = 4;
        public const int StairLower = 5;
        public const int StairUpper = 6;
        public const int Ladder = 7;
        public const int VaultWall = 8;
        public const int UtilityTap = 9;

        public const int TreeConifer = 10;
        public const int TreeBroadleaf = 11;

        /// <summary>
        /// The bed, and the first edifice id the interface names that no generator stamps: 12,
        /// after the trees' ten and eleven. See <c>CoreContent.EdificeBed</c> for why it is a
        /// literal and not an offset.
        /// </summary>
        public const int Bed = 12;

        public const int Count = 13;
    }

    /// <summary>
    /// See <see cref="JobHandle"/>: buildable things, as <c>SiteView</c> and the
    /// <c>PlaceBuilding</c> intent carry them. 0 is "nothing", matching the grid default.
    /// </summary>
    public static class BuildingHandle
    {
        public const int None = 0;
        public const int Wall = 1;

        /// <summary>
        /// A slab at the cell's lower boundary: the floor you stand on and the roof of whatever is
        /// beneath it. One thing, of a material — see docs/design/17-floors-and-collapse.md.
        /// </summary>
        public const int Floor = 2;

        /// <summary>
        /// A floor covering laid on ground that is already there (U42). A slab like
        /// <see cref="Floor"/>, and the opposite of it about what must be underneath: this one
        /// wants a floor already and never asks the support rule, because it cannot fall.
        /// </summary>
        public const int DeckPlate = 3;

        /// <summary>
        /// A ladder: the first thing a colony can build that goes <b>up</b>. One cell, joining the
        /// floor it stands on to the floor directly above it.
        ///
        /// <para>Until U43 a second storey was decorative — measured, every slab came back
        /// walkable and unreachable — because vertical movement goes through a
        /// <c>Pathing.Connector</c> and connectors only ever came from worldgen.</para>
        /// </summary>
        public const int Ladder = 4;

        /// <summary>
        /// The first furniture: two cells, passable, rotatable, finished at a rolled quality
        /// (docs/design/20-beds.md).
        ///
        /// <para><b>Five, not two.</b> The bed was written against a table that ended at the wall
        /// and took the next number; U29's floor, U42's paving and U43's ladder reached main first
        /// and took 2, 3 and 4. Handle order is the save contract and positions are append-only,
        /// so the later branch is the one that moves — which is only safe because no save written
        /// with a bed in it has ever left this branch.</para>
        /// </summary>
        public const int Bed = 5;
        public const int Count = 6;
    }

    /// <summary>
    /// The tier a quality-bearing thing finished at, as <see cref="Pawns.PlacedEdifice.Quality"/>
    /// carries it. 0 is "takes no quality at all" — every wall, for ever. The five names are the
    /// owner's (docs/design/20-beds.md §2).
    /// </summary>
    public static class QualityHandle
    {
        public const int None = 0;
        public const int Poor = 1;
        public const int Normal = 2;
        public const int Decent = 3;
        public const int Uber = 4;
        public const int Epic = 5;
        public const int Count = 6;
    }

    /// <summary>
    /// What a built thing is made of. These are <c>CoreContent.Stuff*</c> values: the same
    /// numbers the ruined city stamps into <c>PlacedEdifice.Stuff</c>, so a wall a colonist
    /// builds and a wall the generator laid are the same kind of record.
    /// </summary>
    public static class StuffHandle
    {
        public const int None = 0;
        public const int Concrete = 1;
        public const int Steel = 2;
        public const int Composite = 3;
        public const int Wood = 4;
        public const int Stone = 5;
        public const int Count = 6;
    }
}
