#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>What a figure's weapon does this frame: nothing, come out of the sheath, or go back into it.</summary>
    public enum SheathChange : byte
    {
        None = 0,
        Draw = 1,
        Sheathe = 2,
    }

    /// <summary>
    /// One figure's memory of its weapon, for <see cref="WeaponSheath.Step"/>. Presentation state:
    /// held on the figure, forgotten on a lease, never saved.
    /// </summary>
    public struct SheathClock
    {
        /// <summary>The weapon is out, or on its way out; false for at the hip, or on its way there.</summary>
        public bool Out;

        /// <summary>The last tick the simulation published a reason to have it out.</summary>
        public int LastReasonTick;

        /// <summary>Whether the pawn was drafted on the last step, so a release is seen on its edge.</summary>
        public bool WasDrafted;

        /// <summary>False until the first step: a figure lent mid-fight takes the weapon as it finds it.</summary>
        public bool Seen;
    }

    /// <summary>
    /// When a weapon is put away (design 33 §8b, owner 2026-09-23): <b>about two seconds after the
    /// last reason to have it out ends, or at once on release from the draft</b>. Whether there is a
    /// reason at all is the simulation's (<see cref="PawnFlags.Drawn"/>); this is only the hold
    /// after it, which is a matter of how the put-away reads and changes nothing a colonist does.
    ///
    /// <para><b>Here, and not in the simulation</b>, because the simulation has no saved tick the
    /// last reason ended on: a new one would be state that decides nothing and still has to be
    /// saved, or a flag that reads differently after a load. In the Hud assembly, beside
    /// <see cref="CombatFeedbackModel"/>, so the fast tier holds the timing without Unity.</para>
    ///
    /// <para><b>Counted in simulation ticks, off the frame's tick</b>, so a pause holds a weapon
    /// out exactly as long as the fight it is paused in, and fast-forward puts it away sooner in
    /// wall time and at the same moment in game time.</para>
    /// </summary>
    public static class WeaponSheath
    {
        /// <summary>
        /// The hold, in ticks: about two seconds (owner) at the composition root's sixty ticks a
        /// second at normal speed. INVENTED past "about 2 s".
        /// </summary>
        public const int HoldTicks = 120;

        /// <summary>Step one figure's clock with the pawn as this frame publishes it.</summary>
        public static SheathChange Step(ref SheathClock clock, in PawnView pawn, int tick) =>
            Step(ref clock, pawn.IsWeaponDrawn, pawn.IsDrafted, tick);

        /// <summary>
        /// Step one figure's clock: <paramref name="drawn"/> is the simulation's answer this frame,
        /// <paramref name="drafted"/> the draft. Returns the edge to animate, if any. A first
        /// sighting takes the published state as it is and animates nothing — somebody scrolled to
        /// a bandit who has had its blade out all along.
        /// </summary>
        public static SheathChange Step(ref SheathClock clock, bool drawn, bool drafted, int tick)
        {
            if (!clock.Seen)
            {
                clock = new SheathClock { Seen = true, Out = drawn, LastReasonTick = tick, WasDrafted = drafted };
                return SheathChange.None;
            }

            bool released = clock.WasDrafted && !drafted;
            clock.WasDrafted = drafted;
            // A load rewinds the tick; the hold counts from where the world now is.
            if (tick < clock.LastReasonTick) clock.LastReasonTick = tick;

            if (drawn)
            {
                clock.LastReasonTick = tick;
                if (clock.Out) return SheathChange.None;
                clock.Out = true;
                return SheathChange.Draw;
            }

            if (!clock.Out) return SheathChange.None;
            if (!released && tick - clock.LastReasonTick < HoldTicks) return SheathChange.None;
            clock.Out = false;
            return SheathChange.Sheathe;
        }
    }
}
