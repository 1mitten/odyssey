namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Named random purposes for the prison (design 58 §6, §9, §10), each draw its own stream:
    /// SHA-256's round constants K28 to K31, the next four after the raid's K24 to K27. The raids
    /// review found a merge with no conflict marker putting a raid's edge and a shot's cover roll on
    /// one stream (design 55 §15), so <c>PrisonPurposeTests</c> holds these apart from every other
    /// purpose constant in the simulation. Grep the constant before taking the next one.
    /// </summary>
    public static class PrisonPurpose
    {
        /// <summary>The hourly escape roll (design 58 §9b).</summary>
        public const uint Escape = 0xC6E0_0BF3;

        /// <summary>A badly hurt raider's one chance to yield (design 58 §10).</summary>
        public const uint Surrender = 0xD5A7_9147;

        /// <summary>Whether a colonist being arrested resists (design 58 §10).</summary>
        public const uint ArrestResist = 0x06CA_6351;

        /// <summary>Which cell of her room a prisoner walks to next (design 58 §6).</summary>
        public const uint CellWander = 0x1429_2967;
    }
}
