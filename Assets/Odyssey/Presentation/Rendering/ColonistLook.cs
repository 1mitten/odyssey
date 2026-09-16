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
    /// Keyed on the pawn's id and the world seed, so a face survives a save, a slice change, a
    /// trip through the figure pool and a restart.
    ///
    /// <para>Callers do not use this directly any more: <see cref="ColonistAppearanceBook"/> is
    /// the thing both drawers hold, and it calls this for the body. The split is kept because the
    /// body lottery and the colour draw want different mixing and are tested separately.</para>
    /// </summary>
    public static class ColonistLook
    {
        /// <summary>
        /// The face index for a pawn, or 0 when there is only one to choose from.
        ///
        /// <paramref name="salt"/> is what makes one world's cast different from another's.
        /// Without it the starting colony is always pawns 1 to 5, and a pure hash of the id deals
        /// those five the same five faces every single time — so a cast of sixty-one looked like a
        /// cast of five. It is the <b>world seed</b> (see <see cref="ColonistAppearance"/>), which
        /// is why a given world deals itself the same people on every load while a new world deals
        /// new ones. It was a number rolled at startup once, which bought the variety at the price
        /// of a colonist who was somebody else after a reload.
        /// </summary>
        public static int For(int pawnId, int variants, uint salt = 0u)
        {
            if (variants <= 1) return 0;

            // Mixed before the modulus. Pawn ids are consecutive in the normal case, and a plain
            // remainder would deal the first five colonists the first five rows of the catalogue —
            // an order chosen for readability, which would come out as the whole starting colony
            // being office workers.
            unchecked
            {
                uint h = ((uint)pawnId ^ salt) * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (int)(h % (uint)variants);
            }
        }
    }
}
