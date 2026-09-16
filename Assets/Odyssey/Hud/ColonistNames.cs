#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// Colonist given names, keyed by <see cref="PawnId"/>.
    ///
    /// The pool is the starter name pool proposed in <c>docs/design/proper-nouns.csv</c> and
    /// awaiting the owner's veto: given names only, no surnames, because a holding is small
    /// enough to be on first-name terms and a single short name fits the roster bar, the densest
    /// region in the interface. The simulation has no names and no opinion about names; this is
    /// an interface-side identity, stable for a pawn's whole life because <see cref="PawnId"/>
    /// is stable for a pawn's whole life.
    ///
    /// Deterministic on the id, never random: a colonist keeps one name across saves, sessions
    /// and screenshots, which is what makes a screenshot caption trustworthy.
    /// </summary>
    public static class ColonistNames
    {
        /// <summary>
        /// The eight promoted from the mockups. Extends to about forty at M2, when pawn
        /// generation needs a pool that does not repeat in a colony of fifty — until then the
        /// eighth name is never reached, and this comment is the promise.
        /// </summary>
        static readonly string[] Pool =
        {
            "Wrenn", "Odile", "Kester", "Sable", "Fen", "Ilma", "Torv", "Nyx",
        };

        /// <summary>
        /// The name a pawn goes by. Past the end of the pool the cycle number is appended rather
        /// than a second name invented quietly — naming is owner content, and a repeated name
        /// with a number is honest about being a placeholder.
        /// </summary>
        public static string Of(PawnId id)
        {
            if (!id.IsValid) return "nobody";
            int index = id.Value - 1;
            int cycle = index / Pool.Length;
            int within = index % Pool.Length;
            return cycle == 0 ? Pool[within] : Pool[within] + " " + (cycle + 1);
        }
    }
}
