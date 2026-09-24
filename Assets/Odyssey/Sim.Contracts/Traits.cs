#nullable enable
namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// Which tables a colonist's starting skills, passions, traits and pace were drawn from
    /// (design 41 §4.4). One per pawn, saved and hashed.
    /// </summary>
    public enum RollProfile : byte
    {
        /// <summary>
        /// The roll from before the draw existed: independent skills, independent passions, no
        /// traits. Only a save written before design 41 loads as this, so it rolls exactly as it
        /// did — pace is re-derived from the seed on load, and any other range would quietly
        /// retune an old colonist's walk. Zero, so a pawn nobody has classified is one.
        /// </summary>
        Legacy = 0,

        /// <summary>Everyone averages out: the same skill total, one of each passion, one good and
        /// one bad trait. What the game gives every colonist it makes itself.</summary>
        Standard = 1,

        /// <summary>One pull, far wider tables, better on average, and a real chance of a dud.</summary>
        Gamble = 2,
    }

    /// <summary>
    /// Trait def indices, and the registry key each is named by (design 41 §3). The simulation's
    /// trait table is built in exactly this order and the interface names a published trait
    /// against it, so this list is the one owner of both. <b>Appended, never inserted</b>: the
    /// index is what a save stores.
    /// </summary>
    public static class TraitHandle
    {
        public const int Diligent = 0;
        public const int Idle = 1;
        public const int QuickStudy = 2;
        public const int SlowStudy = 3;
        public const int LongStride = 4;
        public const int ShortStride = 5;
        public const int Sunny = 6;
        public const int Dour = 7;
        public const int LightEater = 8;
        public const int BigAppetite = 9;
        public const int Scrapper = 10;
        public const int SoftHands = 11;
        public const int Prodigy = 12;
        public const int Wreck = 13;
        public const int Tireless = 14;
        public const int Bottomless = 15;
        public const int Unshakeable = 16;
        public const int Butterfingers = 17;
        public const int Count = 18;

        /// <summary>No trait. What an empty slot publishes as, less one.</summary>
        public const int None = -1;

        /// <summary>The most traits one colonist carries: a gamble deals up to three.</summary>
        public const int MaxPerPawn = 3;

        /// <summary>The Defs, in handle order.</summary>
        public static readonly string[] DefNames =
        {
            "Trait_Diligent", "Trait_Idle", "Trait_QuickStudy", "Trait_SlowStudy",
            "Trait_LongStride", "Trait_ShortStride", "Trait_Sunny", "Trait_Dour",
            "Trait_LightEater", "Trait_BigAppetite", "Trait_Scrapper", "Trait_SoftHands",
            "Trait_Prodigy", "Trait_Wreck", "Trait_Tireless", "Trait_Bottomless",
            "Trait_Unshakeable", "Trait_Butterfingers",
        };

        /// <summary>The registry key each is named by, in handle order.</summary>
        public static readonly string[] Keys =
        {
            "ui.trait.diligent", "ui.trait.idle", "ui.trait.quickstudy", "ui.trait.slowstudy",
            "ui.trait.longstride", "ui.trait.shortstride", "ui.trait.sunny", "ui.trait.dour",
            "ui.trait.lighteater", "ui.trait.bigappetite", "ui.trait.scrapper", "ui.trait.softhands",
            "ui.trait.prodigy", "ui.trait.wreck", "ui.trait.tireless", "ui.trait.bottomless",
            "ui.trait.unshakeable", "ui.trait.butterfingers",
        };

        /// <summary>
        /// The aspect each of a colonist's trait slots is published under: the handle plus one,
        /// so zero is an empty slot and an aspect that is absent reads the same as one that is
        /// empty.
        /// </summary>
        public static readonly AspectKey[] Slot =
        {
            AspectKey.Of("odyssey.pawn.trait.0"),
            AspectKey.Of("odyssey.pawn.trait.1"),
            AspectKey.Of("odyssey.pawn.trait.2"),
        };

        /// <summary>The key for a handle, or empty for <see cref="None"/> or anything out of range.</summary>
        public static string KeyOf(int handle) =>
            handle >= 0 && handle < Keys.Length ? Keys[handle] : string.Empty;
    }
}
