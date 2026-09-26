#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Where a colonist may work (design 43 §4): anywhere, or only inside the colony's home. A
    /// standing setting the player chooses on the Assign tab, not an order. The draft overrides
    /// it, and an empty home restricts nobody (§4d).
    /// </summary>
    public enum PawnArea : byte
    {
        /// <summary>The default: she goes where the work is.</summary>
        Anywhere = 0,

        /// <summary>She takes no work outside home, and walks back in when idle outside it.</summary>
        Home = 1,
    }

    public static class PawnAreas
    {
        /// <summary>How many settings there are; an intent's value must be below this.</summary>
        public const int Count = 2;
    }
}
