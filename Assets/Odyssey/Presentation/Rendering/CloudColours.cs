#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>The three colours the pack's cloud shader takes, for one moment of the day.</summary>
    public readonly struct CloudColourSet
    {
        public CloudColourSet(Color top, Color under, Color rim)
        {
            Top = top;
            Under = under;
            Rim = rim;
        }

        /// <summary>The faces turned to the sun: the shader's <c>_Top_Color</c>.</summary>
        public readonly Color Top;

        /// <summary>The faces turned away: <c>_Base_Color</c>.</summary>
        public readonly Color Under;

        /// <summary>The silhouette's edge: <c>_Fresnel_Color</c>.</summary>
        public readonly Color Rim;
    }

    /// <summary>
    /// <b>What colour a cloud is</b> (design 63 §4): the pack's cloud material is painted for one
    /// pink sunset, and ours has a whole day, so its colours are worked out from the day's light.
    ///
    /// <para>From the <see cref="DaylightState"/> the rest of the frame is lit by, <i>after</i>
    /// <see cref="Overcast"/> has graded it. So a grey day greys the clouds with everything else and
    /// nothing here has to know about weather beyond the storm's own underside.</para>
    ///
    /// <list type="bullet">
    /// <item>The <b>top</b> is lit by the sun: near white at noon, the sun's own warmth low in the
    /// sky, moonlit grey as the light goes.</item>
    /// <item>The <b>underside</b> is the sky's own colour, leaning towards the zenith: blue-grey at
    /// noon, mauve at dusk. Never lighter than the top.</item>
    /// <item><b>Rain</b> dims both and keeps their hue; a <b>storm</b> takes both to cool greys set by
    /// the sky's own brightness, so the deck is darker than the sky (design 63 §4d).</item>
    /// <item>By <b>night</b> every colour converges on the sky behind the band (§4c).</item>
    /// </list>
    /// </summary>
    public static class CloudColours
    {
        /// <summary>The sun's intensity at which a cloud is fully sunlit. Morning and afternoon are 2.05.</summary>
        public const float FullLight = 1.8f;

        /// <summary>The sun's elevation, in degrees, above which it is day and the tops are nearly white.</summary>
        public const float DayElevation = 30f;

        /// <summary>How much lighter than the horizon a cloud's top is at night.</summary>
        public const float NightLift = 1.3f;

        /// <summary>How far the underside leans from the horizon's colour towards the zenith's.</summary>
        public const float UnderZenith = 0.35f;

        /// <summary>
        /// How far a cloud is taken towards the horizon's colour by the air in front of it. A
        /// constant, because a ring stands at one range; the pack's own fog term does this per
        /// pixel and overshoots in our haze (design 63 §4b).
        /// </summary>
        public const float Haze = 0.3f;

        /// <summary>How far a night cloud's top is drained towards grey: moonlight, not a blue lamp.</summary>
        public const float NightDrain = 0.5f;

        /// <summary>
        /// Where the sky behind the clouds sits between the horizon's colour and the zenith's. The
        /// band's middle is about 7° up, and <c>Odyssey/GradientSky</c> weights the zenith there by
        /// sin(7°)^(1/2.2) = 0.38 with the sky material's falloff of 2.2.
        /// </summary>
        public const float SkyBehind = 0.38f;

        /// <summary>A downpour's underside and top, as fractions of a dry one's brightness. The hue is kept: rain stays colourful.</summary>
        public const float RainUnder = 0.55f, RainTop = 0.75f;

        /// <summary>A storm's underside, as a fraction of the sky's own brightness behind it: slate, darker than the sky.</summary>
        public const float StormSlate = 0.58f;

        /// <summary>
        /// A storm's top, as a fraction of the sky's brightness. Below one: the faces a player sees
        /// with the sun behind them are the lit ones, so a storm whose tops stayed light read as
        /// light cloud whatever its bellies did (design 63 §4d).
        /// </summary>
        public const float StormTop = 0.85f;

        /// <summary>
        /// The colours for one moment. <paramref name="gloom"/> and <paramref name="rain"/> are the
        /// weather as eased for drawing; <paramref name="presence"/> is how much of the clouds there
        /// is at this hour (<c>CloudDeck.Presence</c>), and at nought every colour is the sky's own.
        /// </summary>
        public static CloudColourSet For(in DaylightState s, float gloom, float rain = 0f, float presence = 1f)
        {
            float light = Mathf.Clamp01(s.SunIntensity / FullLight);
            float day = Mathf.Clamp01(s.SunElevation / DayElevation);
            float g = Mathf.Clamp01(gloom);
            float r = Mathf.Clamp01(rain);
            Color sky = SkyAt(s);

            // Low sun tints the tops with its own colour; a high sun leaves them nearly white.
            Color sunlit = Color.Lerp(Color.white, s.SunColour, Mathf.Lerp(0.85f, 0.35f, day));
            Color moonlit = Color.Lerp(s.Horizon, Grey(s.Horizon), NightDrain) * NightLift;
            // Rain dims the whole cloud and keeps its hue; the storm takes it to cool greys set by the
            // sky's own brightness, so the deck is darker than the sky behind it whatever the hour.
            Color cool = new Color(0.94f, 0.98f, 1.08f);
            Color top = Color.Lerp(moonlit, sunlit, light) * Mathf.Lerp(1f, RainTop, r);
            top = Color.Lerp(top, Grey(sky) * StormTop * cool, g);
            Color under = Color.Lerp(s.Horizon, s.Zenith, UnderZenith) * Mathf.Lerp(1f, RainUnder, r);
            under = Color.Lerp(under, Grey(sky) * StormSlate * cool, g);
            under = Darker(under, top);

            // The air in front: less of it holds in a storm, or the greys haze back up to the sky
            // they are meant to stand against. One amount for both, so the underside stays the darker.
            float haze = Haze * (1f - 0.8f * g);
            top = Color.Lerp(top, s.Horizon, haze);
            under = Color.Lerp(under, s.Horizon, haze);
            // A calm rim catches the light; a storm's goes the way of its belly.
            Color rim = Color.Lerp(Color.Lerp(top, s.Horizon, 0.5f), Color.Lerp(top, under, 0.5f), g);

            // By night the clouds dissolve into the sky behind them (owner, 2026-09-26). They sink to
            // the horizon as they go (CloudDeck.BaseAt), so what is behind them slides from the band's
            // sky down to the horizon's own colour with them.
            float p = Mathf.Clamp01(presence);
            Color behind = Color.Lerp(s.Horizon, sky, p);
            return new CloudColourSet(
                Opaque(Color.Lerp(behind, top, p)), Opaque(Color.Lerp(behind, under, p)), Opaque(Color.Lerp(behind, rim, p)));
        }

        /// <summary>The sky's colour behind the band of cloud (<see cref="SkyBehind"/>).</summary>
        public static Color SkyAt(in DaylightState s) => Color.Lerp(s.Horizon, s.Zenith, SkyBehind);

        static Color Grey(Color c)
        {
            float l = Luma(c);
            return new Color(l, l, l, c.a);
        }

        /// <summary><paramref name="c"/>, dimmed where needed so it is no brighter than <paramref name="than"/>.</summary>
        static Color Darker(Color c, Color than)
        {
            float lc = Luma(c), lt = Luma(than);
            return lc <= lt || lc <= 0f ? c : c * (lt / lc);
        }

        public static float Luma(Color c) => c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;

        static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);
    }
}
