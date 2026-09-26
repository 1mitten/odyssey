#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// What a person is wearing over the person they were rolled as (<c>docs/design/42-bandits.md</c>
    /// §5). A colonist wears the colony's issued uniform; a bandit wears its gang's welding helmet,
    /// red vest and black trousers. The person underneath — sex, skin, hair, beard, name, age — is
    /// the same roll either way, so taking the outfit off gives back exactly that person.
    /// </summary>
    public enum PawnOutfit : byte
    {
        Issued = 0,
        Bandit = 1,

        /// <summary>A trader's travelling clothes (design 57 §5): the person as rolled, in an ochre coat.</summary>
        Trader = 2,
    }

    /// <summary>
    /// <b>The one owner of which outfit a pawn wears.</b> The figures, the far form, the portraits,
    /// the corpses and the inspect pane all ask here, so a bandit cannot be dressed as a bandit by
    /// one drawer and as a colonist by another — which is the fault the owner reported
    /// (<i>"The marauders look like colonists"</i>) arriving through a different door.
    /// </summary>
    public static class PawnOutfits
    {
        /// <summary>A hostile person dresses as a bandit, a visitor as a trader; everybody else as the colony does.</summary>
        public static PawnOutfit For(PawnFlags flags) =>
            (flags & (PawnFlags.Person | PawnFlags.Hostile)) == (PawnFlags.Person | PawnFlags.Hostile)
                ? PawnOutfit.Bandit
                : (flags & (PawnFlags.Person | PawnFlags.Visitor)) == (PawnFlags.Person | PawnFlags.Visitor)
                    ? PawnOutfit.Trader
                    : PawnOutfit.Issued;

        public static PawnOutfit For(in PawnView pawn) => For(pawn.Flags);

        public static PawnOutfit For(in CorpseView corpse) => For(corpse.Flags);

        /// <summary>
        /// One pawn, looked up in a frame. <b>A scan of the frame's pawns</b>, so for one pawn at a
        /// time — a portrait, the inspect pane — and never inside a loop over every pawn: a drawer
        /// that already holds the <see cref="PawnView"/> passes that instead (P12).
        /// </summary>
        public static PawnOutfit Of(WorldSnapshot snapshot, PawnId pawn) =>
            snapshot.TryGetPawn(pawn, out PawnView view) ? For(view) : PawnOutfit.Issued;
    }
}
