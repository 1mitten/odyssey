#nullable enable

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Which of the catalogue's colonist faces a given pawn wears.
    ///
    /// It lives on its own because **two** separate things draw colonists and they must agree.
    /// The pawns on screen get live animated figures; everyone past the figure cap, and everyone
    /// on a machine that cannot make figures at all, gets the baked instanced form. If those two
    /// picked independently, a colonist would change identity the moment the colony grew past the
    /// cap or the camera moved — which would look like a bug in the simulation rather than in the
    /// renderer, and would be hunted there.
    ///
    /// Keyed on the pawn's id and nothing else, so a face survives a save, a slice change and a
    /// trip through the figure pool.
    /// </summary>
    public static class ColonistLook
    {
        /// <summary>The face index for a pawn, or 0 when there is only one to choose from.</summary>
        public static int For(int pawnId, int variants)
        {
            if (variants <= 1) return 0;

            // Mixed before the modulus. Pawn ids are consecutive in the normal case, and a plain
            // remainder would deal the first five colonists the first five rows of the catalogue —
            // an order chosen for readability, which would come out as the whole starting colony
            // being office workers.
            unchecked
            {
                uint h = (uint)pawnId * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (int)(h % (uint)variants);
            }
        }
    }
}
