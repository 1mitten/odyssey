#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The one writer of the clock the water moves by (design 38 §24f): flow along the streams,
    /// ripples on still water, the breathing at the shore and the falls' streaks all read
    /// <c>_OdysseyWaterTime</c>, in seconds of game time.
    ///
    /// <para><b>The game's clock, not the wall's</b>, for the reason <see cref="WindDirector"/>
    /// gives: a paused world holds still, and at speed 3 the water runs three times as fast as
    /// everything else does. <see cref="MovesOnPause"/> is the one switch if the owner wants water
    /// that keeps moving through a pause; it then carries on in real time from where it was.</para>
    ///
    /// <para>Drawing only: not saved, not hashed. Unset is zero, which is still water — a picture
    /// taken without a bootstrap draws the water at rest rather than not at all.</para>
    /// </summary>
    public sealed class WaterDirector
    {
        static readonly int TimeId = Shader.PropertyToID("_OdysseyWaterTime");
        static readonly int MotionId = Shader.PropertyToID("_OdysseyWaterMotion");

        /// <summary>How much the level water moves: 1 as designed, 0 the still water of §24 (the
        /// measurement's control), up to 2.</summary>
        public static float Motion { get; set; } = 1f;

        /// <summary>Game ticks in a second of play at speed 1.</summary>
        public float TicksPerSecond { get; set; } = 60f;

        /// <summary>On, and the water keeps moving while the game is paused (off by the owner's
        /// rule for the wind).</summary>
        public static bool MovesOnPause { get; set; }

        /// <summary>
        /// The clock wraps here, in seconds, to keep the shader's float precise. A multiple of
        /// every period the shader uses would make the wrap invisible; an hour of play makes it
        /// rare enough not to matter.
        /// </summary>
        const double Wrap = 3600.0;

        double _seconds;
        long _lastTick = -1;

        /// <summary>The time last written, in seconds. For tests.</summary>
        public float Seconds => (float)_seconds;

        public void Apply(long tick, float realDeltaSeconds)
        {
            // A new session, or a load, starts the clock again from its own tick.
            if (_lastTick < 0 || tick < _lastTick) _seconds = tick / (double)TicksPerSecond;
            else if (tick != _lastTick) _seconds += (tick - _lastTick) / (double)TicksPerSecond;
            else if (MovesOnPause) _seconds += realDeltaSeconds;
            _lastTick = tick;
            _seconds %= Wrap;
            Shader.SetGlobalFloat(TimeId, (float)_seconds);
            Shader.SetGlobalFloat(MotionId, Motion);
        }

        /// <summary>Still water, put back as it was found.</summary>
        public void Dispose()
        {
            _lastTick = -1;
            Shader.SetGlobalFloat(TimeId, 0f);
            Shader.SetGlobalFloat(MotionId, 0f);
        }
    }
}
