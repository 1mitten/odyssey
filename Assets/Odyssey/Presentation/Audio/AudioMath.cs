#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Audio
{
    /// <summary>
    /// The arithmetic of faders: linear amplitude, decibels, and the slider taper between them.
    ///
    /// Unity's mixer works in decibels, and so does every audio engineer: equal steps of dB are
    /// equal steps of perceived loudness, where equal steps of linear amplitude pile up at the
    /// bottom of the range where nothing is audible anyway. This project has no mixer asset (ADR
    /// 0010), but the buses keep the same units so the mixer can replace the code gains without
    /// any stored setting changing meaning: a volume is a dB value everywhere it is kept, and is
    /// only ever linear for the moment it is multiplied into an <see cref="AudioSource"/>.
    /// </summary>
    public static class AudioMath
    {
        /// <summary>
        /// Where a fader reads as silence. Unity's own mixers bottom out around −80 dB, which is
        /// roughly 1/10,000th amplitude: for this game's purposes that is silence, and pinning the
        /// floor means a stored volume can never decay towards an asymptote that still makes sound.
        /// </summary>
        public const float SilenceDb = -80f;

        /// <summary>
        /// The fader's default, in dB: unity — neither attenuated nor boosted. The settings panel
        /// seats it at the centre of its track, so the default is a place the thumb can find its
        /// way back to.
        /// </summary>
        public const float UnityDb = 0f;

        /// <summary>
        /// The most a bus may be boosted above unity, in dB: +12, four times the amplitude.
        ///
        /// <para>The owner asked on 2026-09-17 for faders that raise as well as lower. Twelve is
        /// enough lift to make a quiet mix comfortable, and the ceiling exists because boosting
        /// amplifies rather than passes through — above unity a gain multiplies the author's own
        /// volume and can clip, and a bounded place to stop is kinder to the player than an
        /// unbounded one to discover that in.</para>
        /// </summary>
        public const float BoostDb = 12f;

        /// <summary>Linear amplitude to dB, clamped to the fader's range. Zero maps to silence.</summary>
        public static float LinearToDb(float linear) =>
            linear <= 0.0001f
                ? SilenceDb
                : Mathf.Clamp(20f * Mathf.Log10(linear), SilenceDb, UnityDb);

        /// <summary>dB back to linear amplitude. The silence floor maps to exactly zero.</summary>
        public static float DbToLinear(float db) =>
            db <= SilenceDb ? 0f : Mathf.Pow(10f, db / 20f);

        /// <summary>
        /// A slider position, 0 to 1, to a gain in dB.
        ///
        /// The position is squared before conversion, which is the taper every DAW fader uses: it
        /// spends the top of the travel where the hearing is, so "a little quieter" is a move the
        /// wrist can make. Without it the top half of the slider covers a 6 dB range and the whole
        /// bottom half is various flavours of off.
        /// </summary>
        public static float SliderToDb(float position) =>
            LinearToDb(Mathf.Clamp01(position) * Mathf.Clamp01(position));

        /// <summary>
        /// The combined gain of a stack of bus volumes, in dB. Buses multiply in linear amplitude
        /// and therefore add in dB; doing it here rather than at each <see cref="AudioSource"/>
        /// keeps one place that knows the range and can clamp it. The sum may boost since the
        /// faders may, but never past <see cref="BoostDb"/> — master and bus both pushed to their
        /// tops still meet one ceiling.
        /// </summary>
        public static float StackDb(float masterDb, float busDb) =>
            Mathf.Clamp(masterDb + busDb, SilenceDb, BoostDb);
    }
}
