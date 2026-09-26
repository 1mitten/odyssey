#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <b>The one owner of whose side a pawn is on</b> (design 59 §4b).
    ///
    /// <para>Before prisoners a side was a property of the kind: a bandit was hostile because its
    /// kind said so, and nothing saved or changed it. A prisoner is a bandit who is no longer
    /// fighting, and a recruit is a bandit who is one of ours — and the kind never changes, so a
    /// custody override on the pawn is what moves her. Every question about sides —
    /// <see cref="Pawn.Faction"/>, <see cref="Pawn.IsColonist"/>, <see cref="Pawn.IsHostile"/>,
    /// <see cref="Pawn.IsPrisoner"/>, <see cref="Pawn.OwnMode"/>, <see cref="Pawn.Motive"/> —
    /// delegates here, and nothing else reads a kind's faction to answer one. A second copy of this
    /// rule is how a recruit ends up treated as a bandit by one system and a colonist by
    /// another.</para>
    /// </summary>
    public static class Allegiance
    {
        /// <summary>Her side: the colony's once she has joined, else her kind's.</summary>
        public static Faction FactionOf(Pawn pawn) =>
            pawn.Prison != null && pawn.Prison.Joined ? Faction.Colony : pawn.Content.KindOf(pawn.Kind).faction;

        /// <summary>One of ours: a person of the colony's side whom nobody holds.</summary>
        public static bool IsColonist(Pawn pawn) =>
            pawn.Custody == PawnCustody.Free && pawn.IsPerson && FactionOf(pawn) == Faction.Colony;

        /// <summary>Held by the colony, or breaking out of it.</summary>
        public static bool IsPrisoner(Pawn pawn) =>
            pawn.Custody == PawnCustody.Prisoner || pawn.Custody == PawnCustody.Escaping;

        /// <summary>
        /// Fights the colony on sight: a hostile at large, or a prisoner breaking out — an arrested
        /// colonist included, while she runs. A held prisoner is nobody's enemy, so raiders pass
        /// her by and colonists do not strike her.
        /// </summary>
        public static bool IsHostile(Pawn pawn) =>
            pawn.Custody == PawnCustody.Escaping
            || (pawn.Custody == PawnCustody.Free && FactionOf(pawn) == Faction.Hostile);

        /// <summary>
        /// How she walks between jobs. A prisoner or escapee walks as a bandit does, which cannot
        /// open a door but can pass one held open (design 59 §6): that is the whole of what holds a
        /// cell. A pawn let go walks as a colonist does, out through the doors. Anybody else walks
        /// as her kind does — and a recruit as a colonist, whatever her kind, because she opens the
        /// colony's doors now.
        /// </summary>
        public static TraverseMode ModeOf(Pawn pawn)
        {
            if (IsPrisoner(pawn)) return TraverseMode.Bandit;
            // Let go (design 59 §10): the warden opened the door, and every door between her and
            // the edge is open to her on her way out.
            if (pawn.Custody == PawnCustody.Released) return TraverseMode.Colonist;
            if (pawn.Prison != null && pawn.Prison.Joined) return TraverseMode.Colonist;
            return pawn.Content.ModeOf(pawn.Kind);
        }

        /// <summary>What she came for: her kind's, while she is free and not ours. Nothing, otherwise.</summary>
        public static Motive MotiveOf(Pawn pawn) =>
            pawn.Custody == PawnCustody.Free && (pawn.Prison == null || !pawn.Prison.Joined)
                ? pawn.Content.MotiveOf(pawn.Kind)
                : Motive.None;
    }
}
