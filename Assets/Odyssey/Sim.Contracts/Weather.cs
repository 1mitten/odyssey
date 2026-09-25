#nullable enable

namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// The map-wide sky (design 43 §1). One kind at a time, blended into the next over a couple
    /// of game hours. The order is the content's: <c>WeatherDef.kind</c> names one of these, and
    /// the debug intent and the HUD's label table read the same numbers.
    /// </summary>
    public enum WeatherKind
    {
        /// <summary>No cloud, no rain: the day as the clock has it, a shade warmer.</summary>
        Clear = 0,

        /// <summary>A grey day with no rain: dimmed and drained, a little cooler.</summary>
        Cloudy = 1,

        /// <summary>Rain in colour: the light dims and the ground wets; drizzle to downpour by intensity.</summary>
        Rain = 2,

        /// <summary>The rarer dim day: heavy rain, the colour drained, the wind up (owner, 2026-09-25).</summary>
        Storm = 3,
    }

    /// <summary>
    /// The sky as published this frame (design 43 §5): the kind a reader names and the blended
    /// terms a drawing needs, every term in per-mille so the seam stays integer.
    ///
    /// <para><b>The terms are the blend, not the kind.</b> While one spell hands over to the next
    /// both contribute in proportion, so a rainy spell arriving under cloud darkens as one
    /// movement. <see cref="Kind"/> is whichever holds the larger share, which is what the HUD
    /// word shows — it changes once, at the half, rather than flickering.</para>
    /// </summary>
    public readonly struct WeatherView
    {
        public WeatherView(WeatherKind kind, int intensityPerMille, int cloudPerMille, int gloomPerMille,
            int rainPerMille, int windPerMille, int tempOffsetC)
        {
            Kind = kind;
            IntensityPerMille = intensityPerMille;
            CloudPerMille = cloudPerMille;
            GloomPerMille = gloomPerMille;
            RainPerMille = rainPerMille;
            WindPerMille = windPerMille;
            TempOffsetC = tempOffsetC;
        }

        /// <summary>The kind holding the larger share of the blend.</summary>
        public readonly WeatherKind Kind;

        /// <summary>That kind's own intensity: for rain, drizzle (low) to downpour (1000).</summary>
        public readonly int IntensityPerMille;

        /// <summary>How far cloud dims the day. Keeps the colour (<c>Overcast</c>'s cover).</summary>
        public readonly int CloudPerMille;

        /// <summary>How far the day is drained towards grey. Only Cloudy and Storm carry it.</summary>
        public readonly int GloomPerMille;

        /// <summary>How hard it is raining, 0 to 1000.</summary>
        public readonly int RainPerMille;

        /// <summary>The wind's strength against ordinary, 1000 being ordinary.</summary>
        public readonly int WindPerMille;

        /// <summary>What the weather adds to the outdoor temperature, in centi-degrees.</summary>
        public readonly int TempOffsetC;

        /// <summary>A still, clear sky: what a snapshot with no weather system publishes.</summary>
        public static WeatherView None => new WeatherView(WeatherKind.Clear, 0, 0, 0, 0, 1000, 0);
    }
}
