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

        public const int Count = 9;
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
        public const int Count = 6;
    }

    /// <summary>
    /// See <see cref="JobHandle"/>: buildable things, as <c>SiteView</c> and the
    /// <c>PlaceBuilding</c> intent carry them. 0 is "nothing", matching the grid default.
    /// </summary>
    public static class BuildingHandle
    {
        public const int None = 0;
        public const int Wall = 1;
        public const int Count = 2;
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
