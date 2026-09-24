#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// A cloudy sky, as a move on the day's light: the sun dimmed and softened, its shadows
    /// faded, the sky and the ambient drawn towards a cool grey, the haze thickened.
    ///
    /// <para><b>Prototype (claude/rain-look).</b> A pure function of a <see cref="DaylightState"/>
    /// and a cloud cover from 0 to 1, applied by <see cref="DaylightDirector"/> after the hour's
    /// own state, so the time of day still shows through a grey day — a cloudy dusk is still
    /// dusk. The weather design's overcast is exactly this term with the cover read from the
    /// simulation's sky state.</para>
    ///
    /// <para><b>Shadows fade rather than switch off.</b> A grey day has soft, faint shadows, not
    /// none, and a shadow that vanished outright would take the ground's modelling with it.
    /// Whether heavy cloud should also stop <em>rendering</em> the shadow map — the largest term in
    /// a 4K frame (d-19) — is a measurement for the visuals PR, not a guess made here.</para>
    /// </summary>
    public static class Overcast
    {
        /// <summary>How far full cover dims the sun: to this fraction of its clear-sky intensity.</summary>
        public static float SunAtFullCover { get; set; } = 0.22f;

        /// <summary>Shadow strength at full cover, as a fraction of the clear-sky strength.</summary>
        public static float ShadowAtFullCover { get; set; } = 0.25f;

        /// <summary>
        /// What full cover does to the ambient. Below one, not above: the Meadow light already
        /// carries the scene on a heavy ambient (<see cref="Daylight.Meadow"/>), so a grey day
        /// that only dimmed the sun barely darkened at all — the first sheet showed exactly that.
        /// </summary>
        public static float AmbientLift { get; set; } = 0.72f;

        /// <summary>How much denser the haze is under full cover.</summary>
        public static float FogAtFullCover { get; set; } = 2.4f;

        static readonly Color CloudGrey = new Color(0.66f, 0.70f, 0.75f);

        public static DaylightState Grade(in DaylightState s, float cover)
        {
            float c = Mathf.Clamp01(cover);
            if (c <= 0f) return s;

            Color sunColour = Color.Lerp(s.SunColour, Grey(s.SunColour) * new Color(0.95f, 0.98f, 1.03f), 0.7f * c);

            return new DaylightState(
                s.SunElevation,
                s.SunAzimuth,
                sunColour,
                s.SunIntensity * Mathf.Lerp(1f, SunAtFullCover, c),
                s.ShadowStrength * Mathf.Lerp(1f, ShadowAtFullCover, c),
                Wash(s.AmbientSky, 0.75f * c) * Mathf.Lerp(1f, AmbientLift, c),
                Wash(s.AmbientEquator, 0.75f * c) * Mathf.Lerp(1f, AmbientLift, c),
                Wash(s.AmbientGround, 0.6f * c),
                Sky(s.Zenith, c),
                Sky(s.Horizon, c),
                Sky(s.BelowHorizon, c),
                s.FogDensity * Mathf.Lerp(1f, FogAtFullCover, c));
        }

        /// <summary>A colour's own brightness as a grey, a shade cool.</summary>
        static Color Grey(Color c)
        {
            float l = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
            return new Color(l, l, l, c.a);
        }

        /// <summary>Drain a colour towards its own grey, cooled a little.</summary>
        static Color Wash(Color c, float t) =>
            Color.Lerp(c, Grey(c) * new Color(0.96f, 0.99f, 1.04f), t);

        /// <summary>The sky towards cloud: grey, and no brighter than the day allows.</summary>
        static Color Sky(Color c, float t)
        {
            float l = Grey(c).r;
            Color cloud = CloudGrey * Mathf.Clamp(l * 1.25f, 0.02f, 1.1f);
            return Color.Lerp(c, cloud, 0.85f * t);
        }
    }
}
