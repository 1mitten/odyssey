#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// A clouded sky, as a move on the day's light. Two terms, because the owner asked for two
    /// kinds of wet day (2026-09-25, <c>docs/research/rain-look-interview.md</c>):
    ///
    /// <list type="bullet">
    /// <item><b>Cover</b> dims without draining: the sun lower, the shadows softer, the sky nudged
    /// cool, a little haze. <i>"We want to be colourful when it rains"</i> — ordinary rain keeps
    /// the meadow's colour, and this term alone is ordinary rain.</item>
    /// <item><b>Gloom</b> drains: the sun and the ambient towards a cool grey, the sky towards
    /// cloud, the haze thick. The storm, and the grey day; the rarer kind.</item>
    /// </list>
    ///
    /// <para><b>Prototype (claude/rain-look).</b> A pure function of a <see cref="DaylightState"/>,
    /// applied by <see cref="DaylightDirector"/> after the hour's own state, so the time of day
    /// still shows through: a rainy dusk is still dusk.</para>
    ///
    /// <para><b>Shadows fade rather than switch off.</b> A grey day has soft, faint shadows, not
    /// none. Whether heavy gloom should also stop <em>rendering</em> the shadow map — the largest
    /// term in a 4K frame (d-19) — is a measurement for the visuals PR, not a guess made here.</para>
    /// </summary>
    public static class Overcast
    {
        /// <summary>The sun at full cover and no gloom, as a fraction of clear: bright rain's light.</summary>
        public static float SunAtFullCover { get; set; } = 0.7f;

        /// <summary>Shadow strength at full cover and no gloom.</summary>
        public static float ShadowAtFullCover { get; set; } = 0.55f;

        /// <summary>How much of the sky cover moves towards a cool blue-grey, without draining it.</summary>
        public static float CoverSkyShift { get; set; } = 0.3f;

        /// <summary>Haze at full cover and no gloom.</summary>
        public static float FogAtFullCover { get; set; } = 1.5f;

        /// <summary>The sun at full gloom, on top of the cover's dimming.</summary>
        public static float SunAtFullGloom { get; set; } = 0.35f;

        /// <summary>Shadow strength at full gloom, on top of the cover's.</summary>
        public static float ShadowAtFullGloom { get; set; } = 0.45f;

        /// <summary>
        /// The ambient at full gloom. Below one: the Meadow light carries the scene on a heavy
        /// ambient (<see cref="Daylight.Meadow"/>), so a grey day that only dimmed the sun barely
        /// darkened at all — the first sheet showed exactly that.
        /// </summary>
        public static float AmbientAtFullGloom { get; set; } = 0.72f;

        /// <summary>Haze at full gloom, on top of the cover's.</summary>
        public static float FogAtFullGloom { get; set; } = 1.6f;

        static readonly Color CloudGrey = new Color(0.66f, 0.70f, 0.75f);
        static readonly Color RainBlue = new Color(0.80f, 0.88f, 1.0f);

        /// <summary>Cover alone: the bright rainy day.</summary>
        public static DaylightState Grade(in DaylightState s, float cover) => Grade(s, cover, 0f);

        public static DaylightState Grade(in DaylightState s, float cover, float gloom)
        {
            float c = Mathf.Clamp01(cover);
            float g = Mathf.Clamp01(gloom);
            if (c <= 0f && g <= 0f) return s;

            Color sunColour = Color.Lerp(s.SunColour, Grey(s.SunColour) * new Color(0.95f, 0.98f, 1.03f), 0.7f * g);
            float ambient = Mathf.Lerp(1f, AmbientAtFullGloom, g);

            return new DaylightState(
                s.SunElevation,
                s.SunAzimuth,
                sunColour,
                s.SunIntensity * Mathf.Lerp(1f, SunAtFullCover, c) * Mathf.Lerp(1f, SunAtFullGloom, g),
                s.ShadowStrength * Mathf.Lerp(1f, ShadowAtFullCover, c) * Mathf.Lerp(1f, ShadowAtFullGloom, g),
                Wash(s.AmbientSky, 0.75f * g) * ambient,
                Wash(s.AmbientEquator, 0.75f * g) * ambient,
                Wash(s.AmbientGround, 0.6f * g),
                Sky(s.Zenith, c, g),
                Sky(s.Horizon, c, g),
                Sky(s.BelowHorizon, c, g),
                s.FogDensity * Mathf.Lerp(1f, FogAtFullCover, c) * Mathf.Lerp(1f, FogAtFullGloom, g));
        }

        /// <summary>A colour's own brightness as a grey.</summary>
        static Color Grey(Color c)
        {
            float l = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
            return new Color(l, l, l, c.a);
        }

        /// <summary>Drain a colour towards its own grey, cooled a little.</summary>
        static Color Wash(Color c, float t) =>
            Color.Lerp(c, Grey(c) * new Color(0.96f, 0.99f, 1.04f), t);

        /// <summary>
        /// The sky under cover and gloom: cover nudges it towards a cool blue-grey at its own
        /// brightness and keeps it a colour; gloom takes it to cloud grey.
        /// </summary>
        static Color Sky(Color c, float cover, float gloom)
        {
            float l = Grey(c).r;
            Color rain = RainBlue * Mathf.Clamp(l * 1.1f, 0.02f, 1.1f);
            Color cloud = CloudGrey * Mathf.Clamp(l * 1.25f, 0.02f, 1.1f);
            Color covered = Color.Lerp(c, rain, CoverSkyShift * cover);
            return Color.Lerp(covered, cloud, 0.85f * gloom);
        }
    }
}
