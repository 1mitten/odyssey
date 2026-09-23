#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// What the interface says about a fight (design 33 §1): whether a health bar is drawn over a
    /// pawn and how full, what a floating number says and in what ink, whether a pawn wears the
    /// hostile marker, and the words the corpse pane uses. Unity-free, so the fast tier owns the
    /// rules; presentation only draws the answers. <b>Lane C's file</b>
    /// (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>The seam lane B draws through.</b> Lane B (the fight, drawn) owns the health bars,
    /// the floating text and the marker in the world and calls these four to know what to draw;
    /// lane C owns the answers. Both lanes work at once, so the signatures were fixed by the
    /// contracts step and the bodies answer "draw nothing" until lane C writes them.</para>
    ///
    /// <para>The inputs are the snapshot's and nothing else: <see cref="PawnView.Flags"/>, the
    /// aspects in <see cref="CombatAspectNames"/>, <see cref="WorldSnapshot.CombatEvents"/> and
    /// <see cref="WorldSnapshot.Corpses"/>. Every word goes through <see cref="Registry"/>
    /// (<c>ui.combat.*</c>, <c>ui.pawn.corpse</c>).</para>
    /// </summary>
    public static class CombatFeedbackModel
    {
        /// <summary>
        /// Should a health bar stand over this pawn, and how full is it? The owner's rule is "over
        /// hurt and drafted pawns" (design 33 §1). Answers false until lane C writes it.
        /// </summary>
        public static bool HealthBar(WorldSnapshot snapshot, in PawnView pawn, out int hpMilli, out int hpMaxMilli)
        {
            hpMilli = 0;
            hpMaxMilli = 0;
            return false;
        }

        /// <summary>
        /// The words that float up from one moment of a fight — "miss", "dodge", the damage — or
        /// empty for a moment that floats nothing (a swing starting). Empty until lane C writes it.
        /// </summary>
        public static string FloatingText(in CombatEventView combatEvent) => string.Empty;

        /// <summary>The ink those words are drawn in. Transparent until lane C writes it.</summary>
        public static HudColour FloatingColour(in CombatEventView combatEvent) => default;

        /// <summary>Does this pawn wear the hostile marker (design 33 §1: a red marker)? False until lane C writes it.</summary>
        public static bool HostileMarker(in PawnView pawn) => false;
    }
}
