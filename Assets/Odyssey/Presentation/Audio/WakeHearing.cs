#nullable enable
using System;
using UnityEngine;

namespace Odyssey.Presentation.Audio
{
    /// <summary>
    /// The dream's sound (<c>docs/design/56-wake-up.md</c> §5): the whole mix heard through a
    /// closing low-pass filter, a little room and less level — "closed, muted and distant" — and
    /// opened again as the player wakes.
    ///
    /// <para><b>On the listener, not on a mixer.</b> An audio filter on the listener's game object
    /// processes everything the listener hears, which is every source in the game (none sets
    /// <c>bypassListenerEffects</c>). ADR 0010 keeps the buses in code and defers a mixer asset
    /// until a bus needs its own DSP; a filter over the whole mix for five seconds is not that,
    /// and its amendment of 2026-09-26 says so.</para>
    ///
    /// <para><b>Everything it changes it puts back.</b> Both filters are components it added and
    /// destroys, and the listener's volume — a global nothing else in the game touches — goes back
    /// to 1 in <see cref="Dispose"/>, which the shell calls at the end of the wake, on an abort and
    /// when it is itself disabled.</para>
    /// </summary>
    public sealed class WakeHearing : IDisposable
    {
        readonly AudioLowPassFilter? _lowPass;
        readonly AudioReverbFilter? _reverb;
        bool _disposed;

        public WakeHearing(AudioListener? listener)
        {
            if (listener == null) return;
            GameObject host = listener.gameObject;

            _lowPass = host.AddComponent<AudioLowPassFilter>();
            _lowPass.hideFlags = HideFlags.DontSave;
            _lowPass.lowpassResonanceQ = 1f;
            _lowPass.cutoffFrequency = 22000f;

            _reverb = host.AddComponent<AudioReverbFilter>();
            _reverb.hideFlags = HideFlags.DontSave;
            _reverb.reverbPreset = AudioReverbPreset.User;
            // The dry signal kept whole: the filter above is what closes it. The room is added
            // under it, and its level is the one number that moves.
            _reverb.dryLevel = 0f;
            _reverb.room = -10000f;
            _reverb.roomHF = -1500f;
            _reverb.decayTime = 2.2f;
            _reverb.decayHFRatio = 0.5f;
            _reverb.reflectionsLevel = -10000f;
            _reverb.reverbLevel = 0f;
            _reverb.diffusion = 100f;
            _reverb.density = 100f;
        }

        /// <summary>Whether there was a listener to filter.</summary>
        public bool Present => _lowPass != null;

        /// <summary>Set the three numbers the wake derives from its muffle.</summary>
        public void Apply(float cutoffHz, float gain, float roomMb)
        {
            if (_disposed) return;
            if (_lowPass != null) _lowPass.cutoffFrequency = Mathf.Clamp(cutoffHz, 10f, 22000f);
            if (_reverb != null) _reverb.room = Mathf.Clamp(roomMb, -10000f, 0f);
            AudioListener.volume = Mathf.Clamp01(gain);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            AudioListener.volume = 1f;
            Destroy(_lowPass);
            Destroy(_reverb);
        }

        static void Destroy(UnityEngine.Object? o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
