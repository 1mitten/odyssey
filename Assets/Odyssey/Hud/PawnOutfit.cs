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

        /// <summary>
        /// The prison jumpsuit (design 59 §11d): worn from the moment a prisoner is laid in a
        /// prison bed until she is free again. The issued suit, recoloured to
        /// <see cref="ColonistAppearance"/>'s prison cloth.
        /// </summary>
        Prisoner = 2,
    }

    /// <summary>
    /// <b>The one owner of which outfit a pawn wears.</b> The figures, the far form, the portraits,
    /// the corpses and the inspect pane all ask here, so a bandit cannot be dressed as a bandit by
    /// one drawer and as a colonist by another — which is the fault the owner reported
    /// (<i>"The marauders look like colonists"</i>) arriving through a different door.
    /// </summary>
    public static class PawnOutfits
    {
        /// <summary>A hostile person dresses as a bandit; everybody else as the colony does.</summary>
        public static PawnOutfit For(PawnFlags flags) =>
            (flags & (PawnFlags.Person | PawnFlags.Hostile)) == (PawnFlags.Person | PawnFlags.Hostile)
                ? PawnOutfit.Bandit
                : PawnOutfit.Issued;

        /// <summary>
        /// A published pawn: custody first (design 59 §11d) — a prisoner dressed for her cell wears
        /// the jumpsuit, escaping or not — then the flags' rule. A recruit is not hostile, so she
        /// wears the colony's suit without a rule of her own.
        ///
        /// <para><b>Held but not yet dressed, she wears what she came in</b> (review 2026-09-26):
        /// a raider's gang clothes, a colonist's issued suit. The flags cannot say which — custody
        /// clears a raider's Hostile flag, and sets it on a colonist resisting arrest — so her kind
        /// does. A surrendered raider walking to her cell never looks like one of ours.</para>
        /// </summary>
        public static PawnOutfit For(in PawnView pawn)
        {
            if (pawn.Custody == PawnCustody.Free) return For(pawn.Flags);
            if (pawn.Dressed) return PawnOutfit.Prisoner;
            return IsRaiderKind(pawn.Kind) ? PawnOutfit.Bandit : PawnOutfit.Issued;
        }

        /// <summary>A kind that arrives dressed as the gang: the bandit and the gunman (design 42, 55).</summary>
        static bool IsRaiderKind(int kind) => kind == PawnKindLabels.Bandit || kind == PawnKindLabels.Gunman;

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
