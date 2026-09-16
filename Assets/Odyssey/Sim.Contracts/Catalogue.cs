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
        public const int Count = 7;
    }

    /// <summary>
    /// See <see cref="JobHandle"/>: skill def indices as <see cref="SkillView"/> carries them.
    ///
    /// <para><b>These are the simulation's three, not the design's thirteen.</b> The interface
    /// lists every skill the design names and draws the ones it has no simulation for as
    /// unavailable, which is the idiom it already uses for a tab or a command that does not exist
    /// yet. What this table counts is what a colonist can actually gain experience in, and it
    /// grows a row at a time as the systems land.</para>
    /// </summary>
    public static class SkillHandle
    {
        public const int Hauling = 0;
        public const int Cutting = 1;
        public const int Mining = 2;
        public const int Count = 3;
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
}
