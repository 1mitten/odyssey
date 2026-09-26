#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>Why a pawn is leaving the board (design 64 §6b).</summary>
    public enum DespawnReason
    {
        /// <summary>Gone for good from this board: a death, an animal off the edge, a raider leaving.</summary>
        Removed,

        /// <summary>
        /// A colonist setting out on an expedition. She keeps her bed, and her weapon has already
        /// been packed, so neither is let go.
        /// </summary>
        Departed,
    }
}
