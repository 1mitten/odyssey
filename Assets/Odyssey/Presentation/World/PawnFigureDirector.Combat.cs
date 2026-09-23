#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// The fight, on the figures (design 33 §1, §3): the swing started by a
    /// <see cref="PawnGesture.Strike"/> on the gesture serial, the reactions a
    /// <see cref="CombatEventView"/> asks for — hit, dodge, stun, going down, dying — and the
    /// downed and stunned loops read off <see cref="PawnView.Flags"/>. The Sword Combat pack's
    /// Polygon clips where the catalogue has their rows (<c>ModuleIds.CombatClip</c>), and
    /// <see cref="CombatPose"/> where it does not. <b>Lane B's file</b>
    /// (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>A seam from the contracts step</b>: the partial exists so lane B adds to the figure
    /// director without editing its other four files, and <see cref="OnCombatEvent"/> is the one
    /// entry point <c>CombatFeedback</c> calls. Nothing here does anything yet.</para>
    /// </summary>
    public sealed partial class PawnFigureDirector
    {
        /// <summary>A new moment of a fight, delivered once, in id order. Does nothing until lane B writes it.</summary>
        public void OnCombatEvent(in CombatEventView combatEvent)
        {
        }
    }
}
