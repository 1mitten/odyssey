#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>The moments a creature with a voice calls out in a fight (design 62 §8d).</summary>
    public enum VoiceCue : byte
    {
        None = 0,

        /// <summary>Its own swing starting: a grunt as it winds up. The quietest.</summary>
        Strike = 1,

        /// <summary>A blow landing on it: a squeal.</summary>
        Hurt = 2,

        /// <summary>It threw somebody — a fling or a slam: a bellow. The loudest.</summary>
        Fling = 3,

        /// <summary>It went down or died.</summary>
        Down = 4,
    }

    /// <summary>
    /// Which moment of a fight a voiced creature calls out on, and whether it is the swinger or the
    /// struck (design 62 §8d; owner, 2026-09-26: "loud when he knocks people back or even when
    /// hit"). Pure: the event's kind and who in it has a voice, nothing else. How loud each moment
    /// is lives in the bake and the catalogue, not here.
    /// </summary>
    public static class CreatureVoice
    {
        /// <summary>
        /// The moment <paramref name="kind"/> is for a voiced attacker or a voiced target, or
        /// <see cref="VoiceCue.None"/>. <paramref name="fromAttacker"/> says whose voice it is.
        /// </summary>
        public static VoiceCue For(CombatEventKind kind, bool attackerVoiced, bool targetVoiced, out bool fromAttacker)
        {
            fromAttacker = false;
            switch (kind)
            {
                case CombatEventKind.Swing:
                case CombatEventKind.SwingCritical:
                case CombatEventKind.Shot: // a thrown rock (design 62 §7a) is a strike too
                    fromAttacker = true;
                    return attackerVoiced ? VoiceCue.Strike : VoiceCue.None;
                case CombatEventKind.KnockedBack:
                case CombatEventKind.Slam:
                    fromAttacker = true;
                    return attackerVoiced ? VoiceCue.Fling : VoiceCue.None;
                case CombatEventKind.Hit:
                    return targetVoiced ? VoiceCue.Hurt : VoiceCue.None;
                case CombatEventKind.Downed:
                case CombatEventKind.Died:
                    return targetVoiced ? VoiceCue.Down : VoiceCue.None;
                default:
                    return VoiceCue.None;
            }
        }

        /// <summary>The last part of a voice's sound id for a moment: <c>strike</c>, <c>hurt</c>, <c>fling</c>, <c>down</c>.</summary>
        public static string Suffix(VoiceCue cue) => cue switch
        {
            VoiceCue.Strike => "strike",
            VoiceCue.Hurt => "hurt",
            VoiceCue.Fling => "fling",
            VoiceCue.Down => "down",
            _ => string.Empty,
        };
    }
}
