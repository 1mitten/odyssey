#nullable enable
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.Audio
{
    /// <summary>
    /// How much of each rain bed to play for a sky, and how far the outdoor bed steps back under
    /// it (design 43 §7): one pure function, so the whole of "what rain sounds like" is here and
    /// testable, and the director only smooths and applies it.
    ///
    /// <para><b>Two recordings, one continuous sound.</b> The light bed (patter and drips) and the
    /// heavy bed (a steady roar) both run for as long as it rains and are weighted against each
    /// other, never switched: an equal-power crossfade on the rain's intensity, because two
    /// uncorrelated noises crossfaded linearly dip 3 dB at the midpoint. The clips are baked to
    /// the same loudness (<c>tools/audio/bake_rain.sh</c>), so these weights are loudness.</para>
    ///
    /// <para><b>Read off the published sky, which already blends.</b> Every input here is the
    /// <see cref="WeatherView"/>'s, which the simulation hands over across two game hours; and the
    /// director smooths again over seconds, so a debug-forced change swells rather than steps.
    /// Nothing reads <see cref="WeatherView.Kind"/>, which flips at the midpoint of a hand-over:
    /// the storm is recognised by its wind, which is continuous.</para>
    ///
    /// <para><b>By weather type</b>, with every number INVENTED for the owner to tune by ear:
    /// <list type="bullet">
    /// <item>Clear and Cloudy — silence. They carry no rain.</item>
    /// <item>Drizzle (rain at 250–450 per mille) — the light bed alone, quiet.</item>
    /// <item>Rain (450–850) — the heavy bed rises under the light one.</item>
    /// <item>Downpour (850 and over) — the heavy bed, with the light one kept at
    /// <see cref="LightUnderHeavy"/> for its close drips.</item>
    /// <item>Storm — a downpour whatever its intensity, a step louder, and the birds hushed further.</item>
    /// </list></para>
    /// </summary>
    public static class RainMix
    {
        /// <summary>Rain per mille over which the bed arrives from nothing, so the first drops do not click on.</summary>
        public const float ArriveBy = 0.15f;

        /// <summary>Where the heavy bed starts to rise under the light one, and where it has taken over.</summary>
        public const float HeavyFrom = 0.45f, HeavyBy = 0.85f;

        /// <summary>How loud the lightest rain is against a downpour, as a gain: 0.45 is about -7 dB.</summary>
        public const float QuietestRain = 0.45f;

        /// <summary>How much of the light bed stays under a full downpour: the drips on the near things.</summary>
        public const float LightUnderHeavy = 0.3f;

        /// <summary>How much louder a storm is than rain of the same intensity, as a gain: about +1.6 dB.</summary>
        public const float StormLift = 0.2f;

        /// <summary>How far the outdoor bed steps back under a downpour: to 0.4, about -8 dB.</summary>
        public const float OutdoorDuckAtFull = 0.6f;

        /// <summary>How much further a storm pushes it: a quarter again, to 0.25, about -12 dB.</summary>
        public const float StormDuckLift = 0.25f;

        /// <summary>
        /// The two beds' weights, 0 to about 1.2, and the outdoor bed's gain, 0 to 1, for a sky.
        /// <paramref name="outdoors"/> false (the slice under the surface) is silence and no duck,
        /// the outdoor bed's own rule: underground there is no sky to hear.
        /// </summary>
        public static RainLevels Of(in WeatherView sky, bool outdoors)
        {
            float rain = Mathf.Clamp01(sky.RainPerMille / 1000f);
            if (!outdoors || rain <= 0f) return RainLevels.Silent;

            // A storm is the one kind that blows harder than ordinary (Weather.xml: 1300 at full),
            // so its wind says how far in the storm is, continuously through a hand-over.
            float storm = Mathf.Clamp01((sky.WindPerMille - 1000) / 300f);

            float arrive = Mathf.SmoothStep(0f, 1f, rain / ArriveBy);
            float heavy = Mathf.Max(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HeavyFrom, HeavyBy, rain)), storm);
            float loud = Mathf.Lerp(QuietestRain, 1f, rain) * (1f + StormLift * storm) * arrive;

            float angle = heavy * Mathf.PI * 0.5f;
            float light = Mathf.Max(Mathf.Cos(angle), LightUnderHeavy * heavy) * loud;
            float roar = Mathf.Sin(angle) * loud;

            // The birds hush: a little under a drizzle, most of the way under a downpour, further
            // in a storm. Weighted by the heavy share, because a patter does not drown a song.
            float hush = OutdoorDuckAtFull * arrive * Mathf.Lerp(0.35f, 1f, Mathf.Max(heavy * rain, storm))
                * (1f + StormDuckLift * storm);
            return new RainLevels(light, roar, 1f - hush);
        }
    }

    /// <summary>What <see cref="RainMix.Of"/> asks of the mix.</summary>
    public readonly struct RainLevels
    {
        /// <summary>The light bed's weight, before its catalogue volume and the bus.</summary>
        public readonly float Light;

        /// <summary>The heavy bed's weight, the same.</summary>
        public readonly float Heavy;

        /// <summary>The gain the outdoor bed plays at, 1 on a dry day.</summary>
        public readonly float OutdoorGain;

        public RainLevels(float light, float heavy, float outdoorGain)
        {
            Light = light;
            Heavy = heavy;
            OutdoorGain = outdoorGain;
        }

        public static RainLevels Silent => new RainLevels(0f, 0f, 1f);
    }
}
