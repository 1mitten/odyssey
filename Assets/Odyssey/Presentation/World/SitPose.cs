#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// A colonist sitting down by a fire (design 31 §18d–§18e): how quickly the figure goes down
    /// into the row's seated idle and comes back up out of it.
    ///
    /// <para><b>Dormant today.</b> No colonist row carries a seated clip: the locomotion pack's
    /// crouching idle was the first one tried and the owner read it as sneaking, not sitting
    /// (2026-09-24). A seated figure therefore stands, facing the fire. The blend below is the seam
    /// a real seated clip arrives through — one field on the catalogue row, no code.</para>
    ///
    /// <para><b>An authored clip, not a computed pose, and that is the whole decision.</b> Every
    /// other stance here — sleep, swim, the carry, the swing — is angles laid over the standing
    /// idle, and a sit cannot be: it needs knees bent past anything the rig will take from code,
    /// and lowering an upright figure instead puts its feet through the floor. An authored,
    /// grounded seated idle needs neither, so the figure simply blends into it as a second idle.
    /// Foot planting stays on, because such a clip's feet are already on the floor and on a slope
    /// planting is what keeps them there.</para>
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
