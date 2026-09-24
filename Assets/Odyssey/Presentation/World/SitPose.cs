#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// A colonist sitting down by a fire (design 31 §18d): how quickly the figure goes down into
    /// the row's settled idle and comes back up out of it.
    ///
    /// <para><b>An authored clip, not a computed pose, and that is the whole decision.</b> Every
    /// other stance here — sleep, swim, the carry, the swing — is angles laid over the standing
    /// idle, and a sit cannot be: it needs knees bent past anything the rig will take from code,
    /// and lowering an upright figure instead puts its feet through the floor. The locomotion
    /// pack's crouching idle is authored, grounded and settled, so the figure simply blends into
    /// it as a second idle. Foot planting stays on, because the clip's feet are already on the
    /// floor and on a slope planting is what keeps them there.</para>
    ///
    /// <para>No pack ships a seated clip; the crouch is the lowest authored rest there is. A log
    /// seat under a sitter would want a seated clip and a place to put the seat, and is a unit of
    /// its own rather than a tuning of this one.</para>
    /// </summary>
    public static class SitPose
    {
        /// <summary>
        /// Seconds to go all the way down, or all the way up. Slower than lying down
        /// (<see cref="SleepPose.SettleSeconds"/>), because somebody lowering themselves to a
        /// fire does it unhurriedly, and a blend between two grounded idles has nothing in it that
        /// could look wrong for being slow. INVENTED; judge it on the board.
        /// </summary>
        public static float SettleSeconds { get; set; } = 0.8f;

        /// <summary>Ease the weight towards where it should be, at <see cref="SettleSeconds"/>.</summary>
        public static float Settle(float current, float target, float deltaTime)
        {
            if (deltaTime <= 0f) return current;
            float step = SettleSeconds > 1e-3f ? deltaTime / SettleSeconds : 1f;
            return Mathf.MoveTowards(current, target, step);
        }
    }
}
