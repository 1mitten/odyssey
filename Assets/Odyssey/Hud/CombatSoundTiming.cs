#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>Which of the sounds of a blow a moment makes (design 33 §9g).</summary>
    public enum CombatCue : byte
    {
        None = 0,

        /// <summary>A weapon through the air: every swing with a weapon in the hand, hit or miss.</summary>
        Whoosh = 1,

        /// <summary>A sharp weapon's critical: played <b>instead of</b> the whoosh, its biggest moment on the impact.</summary>
        Slice = 2,

        /// <summary>A blow landing on somebody: every landed hit, any weapon, fists and bites too.</summary>
        Thud = 3,
    }

    /// <summary>What a scheduled cue should do this frame.</summary>
    public enum CueTiming : byte
    {
        /// <summary>Not yet: started now, its peak would land earlier than it should. Also the answer while paused.</summary>
        Wait = 0,

        /// <summary>Start it now.</summary>
        Play = 1,

        /// <summary>Too late to be heard in its place — a whoosh that would land on the thud — so it is let go.</summary>
        Drop = 2,
    }

    /// <summary>
    /// <b>When the sounds of a blow start</b> (design 33 §9g; owner, 2026-09-23: <i>"this all needs
    /// to be coordinated at the right times for effect"</i>). Unity-free, so the fast tier holds the
    /// rules; presentation's <c>CombatFeedback</c> asks, and plays what it is told where it is told.
    ///
    /// <para><b>The impact is known when the swing starts.</b> A <see cref="CombatEventKind.Swing"/>
    /// or <see cref="CombatEventKind.SwingCritical"/> is published at the tick its wind-up begins
    /// and carries the wind-up in ticks, and the blow lands at the one plus the other — the same
    /// arithmetic the drawn swing times its contact by. So a whoosh can be started <i>before</i>
    /// the blow, which is the only way its loudest moment can come before the thud.</para>
    ///
    /// <para><b>The rules, the owner's:</b> a weapon's whoosh peaks <see cref="WhooshLeadSeconds"/>
    /// before the impact, never earlier and never on top of the thud; a sharp weapon's critical
    /// plays the slice instead, peaking on the impact; the thud plays on the hit itself, trimmed so
    /// its transient is on its first sample. <b>Time is real time at the current speed</b>: at ×3
    /// the impact is three times nearer in seconds, and a paused world starts nothing.</para>
    ///
    /// <para><b>Never early, at most a frame late.</b> A cue starts on the first frame on which
    /// starting it lands its peak no earlier than it should; the frame before, it would have been
    /// early. So a whoosh peaks between 0.1 s and 0.1 s less a frame before the blow, and a slice
    /// between the impact and a frame after it.</para>
    /// </summary>
    public static class CombatSoundTiming
    {
        /// <summary>
        /// How long after its first sample each baked file is at its loudest: the middle of its
        /// loudest 10 ms, measured by <c>tools/audio/bake_combat.sh</c> and written in its header.
        /// <b>A re-bake that moves them changes these in the same commit.</b>
        /// </summary>
        public const float WhooshPeakSeconds = 0.040f, SlicePeakSeconds = 0.065f, ThudPeakSeconds = 0.013f;

        /// <summary>How long before the blow connects the whoosh is at its loudest (owner: <i>"~0.1 s before"</i>).</summary>
        public const float WhooshLeadSeconds = 0.1f;

        /// <summary>
        /// The least a whoosh may lead the blow by and still be played. A swing first seen so late
        /// that the whoosh would peak nearer the impact than this is let go: the owner's rule is
        /// <i>never on top of the thud</i>, and a whoosh smeared into the blow is heard as a
        /// flam, not as a blade. INVENTED; 40 ms is about where two onsets stop fusing.
        /// </summary>
        public const float WhooshClearanceSeconds = 0.04f;

        /// <summary>
        /// The most a slice may land after the impact and still be played. A critical is the
        /// headline of a fight and its body is 0.3 s of cut, so a little late is far better than
        /// silent; later than this it is a second sound after the blow. INVENTED.
        /// </summary>
        public const float SliceLateSeconds = 0.1f;

        /// <summary>
        /// What a swing schedules: a whoosh for every swing with a weapon in the hand; for a
        /// critical, the slice instead if the weapon is sharp and the whoosh still if it is blunt;
        /// nothing for fists or a natural attack, critical or not.
        /// </summary>
        public static CombatCue SwingCue(CombatEventKind kind, bool heldWeapon, bool sharp)
        {
            if (!heldWeapon) return CombatCue.None;
            return kind switch
            {
                CombatEventKind.Swing => CombatCue.Whoosh,
                CombatEventKind.SwingCritical => sharp ? CombatCue.Slice : CombatCue.Whoosh,
                _ => CombatCue.None,
            };
        }

        /// <summary>
        /// What sounds on the frame an event is read: the thud for a landed hit, whatever struck
        /// it and whether or not it was critical (under the slice), and nothing else. A miss and a
        /// dodge are the whoosh alone.
        /// </summary>
        public static CombatCue OnItsFrame(CombatEventKind kind) =>
            kind == CombatEventKind.Hit ? CombatCue.Thud : CombatCue.None;

        /// <summary>How long after it starts a cue is at its loudest.</summary>
        public static float PeakSeconds(CombatCue cue) => cue switch
        {
            CombatCue.Whoosh => WhooshPeakSeconds,
            CombatCue.Slice => SlicePeakSeconds,
            CombatCue.Thud => ThudPeakSeconds,
            _ => 0f,
        };

        /// <summary>How far before the impact a cue's peak belongs: the whoosh's lead, the slice's none.</summary>
        public static float LeadSeconds(CombatCue cue) => cue == CombatCue.Whoosh ? WhooshLeadSeconds : 0f;

        /// <summary>Ticks per real second at a game speed: the nominal rate times the speed, and 0 paused.</summary>
        public static float TicksPerRealSecond(int gameSpeed, float nominalTicksPerSecond) =>
            gameSpeed > 0 ? nominalTicksPerSecond * gameSpeed : 0f;

        /// <summary>
        /// Whether a cue for a blow landing at <paramref name="impactTick"/> starts now.
        /// <paramref name="nowTicks"/> is the last tick run plus how far the frame sits towards the
        /// next; <paramref name="ticksPerSecond"/> the rate at the current speed, 0 when paused.
        /// </summary>
        public static CueTiming Decide(CombatCue cue, int impactTick, double nowTicks, float ticksPerSecond)
        {
            if (cue == CombatCue.None) return CueTiming.Drop;
            if (ticksPerSecond <= 0f) return CueTiming.Wait;

            // Started now, how long before the impact its peak would land (negative: after it).
            double peakLead = (impactTick - nowTicks) / ticksPerSecond - PeakSeconds(cue);
            if (peakLead > LeadSeconds(cue)) return CueTiming.Wait;

            return cue switch
            {
                CombatCue.Whoosh => peakLead >= WhooshClearanceSeconds ? CueTiming.Play : CueTiming.Drop,
                CombatCue.Slice => peakLead >= -SliceLateSeconds ? CueTiming.Play : CueTiming.Drop,
                _ => CueTiming.Play,
            };
        }
    }

    /// <summary>A whoosh or a slice waiting for its moment: whose swing, where, and when it lands.</summary>
    public readonly struct PendingCue
    {
        public readonly CombatCue Cue;

        /// <summary>The tick the blow lands: the swing's tick plus its wind-up.</summary>
        public readonly int ImpactTick;

        /// <summary>Who is swinging; the sound is heard from them.</summary>
        public readonly PawnId Attacker;

        /// <summary>Where it was aimed, for when the swinger has left the frame by the time it plays.</summary>
        public readonly CellRef Cell;

        public PendingCue(CombatCue cue, int impactTick, PawnId attacker, CellRef cell)
        {
            Cue = cue;
            ImpactTick = impactTick;
            Attacker = attacker;
            Cell = cell;
        }
    }

    /// <summary>
    /// The swings whose sound has not started yet (design 33 §9g). <b>Allocation-free</b>: a fixed
    /// array, filled by <see cref="Hear"/> and emptied by <see cref="TryTakeDue"/>, both O(pending)
    /// — at most one per fighter — and never the colony. Cleared when the world changes, as the
    /// watermark is.
    /// </summary>
    public sealed class CombatSoundSchedule
    {
        /// <summary>
        /// How many swings can wait at once. One per fighter at most, since a swing is replaced by
        /// its swinger's next; twenty against twenty is forty. A swing past it is not heard.
        /// </summary>
        public const int Capacity = 64;

        readonly PendingCue[] _pending = new PendingCue[Capacity];
        int _count;

        /// <summary>How many are waiting.</summary>
        public int Count => _count;

        /// <summary>Swings let go because they were first seen too late to be heard in their place. For tests and the overlay.</summary>
        public int Dropped { get; private set; }

        /// <summary>Swings not scheduled because <see cref="Capacity"/> were already waiting.</summary>
        public int Overflowed { get; private set; }

        /// <summary>The one waiting at <paramref name="index"/>, for tests.</summary>
        public PendingCue this[int index] => _pending[index];

        /// <summary>
        /// One new moment of a fight. A swing schedules its whoosh or slice
        /// (<see cref="CombatSoundTiming.SwingCue"/>), replacing anything its swinger still had waiting — a new
        /// swing means the last one is over. A swinger downed, killed or knocked down loses the
        /// swing in the air, and its sound with it. Returns what sounds on this frame: the thud for
        /// a landed hit, else <see cref="CombatCue.None"/>.
        /// </summary>
        /// <param name="sides">Which weapons are held weapons, and which cut (<see cref="BloodSides"/>, read off the content).</param>
        /// <param name="attackerKind">The swinger's <c>PawnView.Kind</c>, or -1 when it is not in the frame.</param>
        public CombatCue Hear(in CombatEventView combatEvent, BloodSides sides, int attackerKind)
        {
            switch (combatEvent.Kind)
            {
                case CombatEventKind.Swing:
                case CombatEventKind.SwingCritical:
                {
                    Cancel(combatEvent.Attacker);
                    CombatCue cue = CombatSoundTiming.SwingCue(combatEvent.Kind,
                        sides.IsHeldWeapon(combatEvent.Weapon), sides.IsSharp(combatEvent.Weapon, attackerKind));
                    if (cue != CombatCue.None)
                        Add(new PendingCue(cue, combatEvent.Tick + combatEvent.Amount, combatEvent.Attacker, combatEvent.Cell));
                    return CombatCue.None;
                }

                case CombatEventKind.Downed:
                case CombatEventKind.Died:
                case CombatEventKind.KnockedBack:
                    Cancel(combatEvent.Target);
                    return CombatCue.None;
            }

            return CombatSoundTiming.OnItsFrame(combatEvent.Kind);
        }

        /// <summary>
        /// The next cue due to start this frame, if any. Call until it returns false. A cue first
        /// seen too late is let go on the way (<see cref="Dropped"/>); paused, nothing is due.
        /// </summary>
        public bool TryTakeDue(double nowTicks, float ticksPerSecond, out PendingCue due)
        {
            int i = 0;
            while (i < _count)
            {
                PendingCue cue = _pending[i];
                switch (CombatSoundTiming.Decide(cue.Cue, cue.ImpactTick, nowTicks, ticksPerSecond))
                {
                    case CueTiming.Play:
                        RemoveAt(i);
                        due = cue;
                        return true;
                    case CueTiming.Drop:
                        RemoveAt(i);
                        Dropped++;
                        continue;
                    default:
                        i++;
                        break;
                }
            }

            due = default;
            return false;
        }

        /// <summary>Forget everything waiting: the world changed.</summary>
        public void Clear()
        {
            _count = 0;
            Dropped = 0;
            Overflowed = 0;
        }

        void Add(in PendingCue cue)
        {
            if (_count == Capacity)
            {
                Overflowed++;
                return;
            }

            _pending[_count++] = cue;
        }

        void Cancel(PawnId attacker)
        {
            if (!attacker.IsValid) return;
            for (int i = _count - 1; i >= 0; i--)
                if (_pending[i].Attacker.Value == attacker.Value)
                    RemoveAt(i);
        }

        // Order does not matter — every waiting cue is asked every frame — so the last fills the gap.
        void RemoveAt(int index) => _pending[index] = _pending[--_count];
    }
}
