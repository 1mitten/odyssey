#nullable enable
using Odyssey.Hud;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Everything about the light at one moment of the day.
    ///
    /// <para>One struct rather than a dozen loose fields because these values are only ever
    /// correct <i>together</i>. A warm sun over a blue midday sky is not a time of day, it is a
    /// mistake, and the whole point of sampling a single state is that no caller can take half of
    /// one hour and half of another.</para>
    /// </summary>
    public readonly struct DaylightState
    {
        public DaylightState(float sunElevation, float sunAzimuth, Color sunColour, float sunIntensity,
            float shadowStrength, Color ambientSky, Color ambientEquator, Color ambientGround,
            Color zenith, Color horizon, Color belowHorizon, float fogDensity)
        {
            SunElevation = sunElevation;
            SunAzimuth = sunAzimuth;
            SunColour = sunColour;
            SunIntensity = sunIntensity;
            ShadowStrength = shadowStrength;
            AmbientSky = ambientSky;
            AmbientEquator = ambientEquator;
            AmbientGround = ambientGround;
            Zenith = zenith;
            Horizon = horizon;
            BelowHorizon = belowHorizon;
            FogDensity = fogDensity;
        }

        public readonly float SunElevation, SunAzimuth, SunIntensity, ShadowStrength, FogDensity;
        public readonly Color SunColour, AmbientSky, AmbientEquator, AmbientGround;
        public readonly Color Zenith, Horizon, BelowHorizon;

        public static DaylightState Lerp(in DaylightState a, in DaylightState b, float t) =>
            new DaylightState(
                Mathf.Lerp(a.SunElevation, b.SunElevation, t),
                // Plain lerp, not LerpAngle. The sun's bearing is a monotone sweep from morning to
                // evening and never wraps within the day, so the shortest-arc rule would only ever
                // be a way to send it backwards across midnight.
                Mathf.Lerp(a.SunAzimuth, b.SunAzimuth, t),
                Color.Lerp(a.SunColour, b.SunColour, t),
                Mathf.Lerp(a.SunIntensity, b.SunIntensity, t),
                Mathf.Lerp(a.ShadowStrength, b.ShadowStrength, t),
                Color.Lerp(a.AmbientSky, b.AmbientSky, t),
                Color.Lerp(a.AmbientEquator, b.AmbientEquator, t),
                Color.Lerp(a.AmbientGround, b.AmbientGround, t),
                Color.Lerp(a.Zenith, b.Zenith, t),
                Color.Lerp(a.Horizon, b.Horizon, t),
                Color.Lerp(a.BelowHorizon, b.BelowHorizon, t),
                Mathf.Lerp(a.FogDensity, b.FogDensity, t));
    }

    /// <summary>
    /// The day's light as a keyed palette: blue by day, orange at either end, dark at night.
    ///
    /// <para><b>This replaces the fixed golden hour</b> that the look interview settled on
    /// (`docs/research/look-interview.md`, question 2). The owner saw the fixed hour lit and asked
    /// for the cycle instead, which is a change of mind rather than a misunderstanding and is
    /// recorded as one. What survives from the fixed version is the whole of its reasoning: the
    /// identity between the fog colour and the sky's horizon, shadows lifted rather than avoided,
    /// and a haze that crosses the board instead of starting past it. Every key below obeys all
    /// three; they are now twelve numbers each instead of twelve numbers once.</para>
    ///
    /// <para><b>Why a keyed table and not a formula.</b> A physical model would give the sun's
    /// elevation for an hour and a latitude, and it would be less useful: nothing in it can say
    /// that dawn should be orange for longer than dusk, or that the sky should stay bright a
    /// little past sunset because that is when a colony looks best. These are art directions, and
    /// a table is the honest shape for an art direction. It is also what the owner can edit.</para>
    ///
    /// <para><b>Nothing here is simulation.</b> The light is a pure function of the tick, so it is
    /// not saved, not hashed, and cannot be observed by a pawn: a colonist at midnight is not
    /// blind, because nothing in this file is readable from the simulation. If darkness is ever to
    /// matter to work or to sight, that is a simulation feature with its own grid and its own
    /// tests, and it would read the clock rather than these colours.</para>
    /// </summary>
    public static class Daylight
    {
        /// <summary>
        /// The keys, in ascending hour. The first and last are both midnight on purpose, so the
        /// wrap across the end of the day needs no special case anywhere.
        /// </summary>
        static readonly (float Hour, DaylightState State)[] Keys =
        {
            (0f, Night(0f)),
            (4f, Night(-8f)),

            // First light: the sky lifts well before the sun does anything useful, which is what
            // makes an early morning read as early rather than as a dim noon.
            (5.5f, new DaylightState(
                sunElevation: 1f, sunAzimuth: 88f,
                sunColour: new Color(1.00f, 0.62f, 0.42f), sunIntensity: 0.35f, shadowStrength: 0.35f,
                ambientSky: new Color(0.30f, 0.36f, 0.52f),
                ambientEquator: new Color(0.34f, 0.31f, 0.34f),
                ambientGround: new Color(0.20f, 0.16f, 0.16f),
                zenith: new Color(0.16f, 0.24f, 0.46f),
                horizon: new Color(0.76f, 0.50f, 0.42f),
                belowHorizon: new Color(0.52f, 0.36f, 0.34f),
                fogDensity: 0.0030f)),

            // Dawn proper: the orange the owner asked for, and the longest-held warm key.
            (7f, Warm(elevation: 12f, azimuth: 100f, intensity: 1.85f, density: 0.0026f)),

            // Morning: the warmth drains out of the light before the sky finishes turning blue.
            (10f, new DaylightState(
                sunElevation: 38f, sunAzimuth: 130f,
                sunColour: new Color(1.00f, 0.96f, 0.90f), sunIntensity: 2.05f, shadowStrength: 0.62f,
                ambientSky: new Color(0.52f, 0.63f, 0.84f),
                ambientEquator: new Color(0.60f, 0.61f, 0.63f),
                ambientGround: new Color(0.38f, 0.34f, 0.28f),
                zenith: new Color(0.24f, 0.46f, 0.82f),
                horizon: new Color(0.72f, 0.83f, 0.93f),
                belowHorizon: new Color(0.66f, 0.76f, 0.86f),
                fogDensity: 0.0019f)),

            (13f, Blue()),

            (16f, new DaylightState(
                sunElevation: 40f, sunAzimuth: 210f,
                sunColour: new Color(1.00f, 0.94f, 0.84f), sunIntensity: 2.05f, shadowStrength: 0.62f,
                ambientSky: new Color(0.52f, 0.62f, 0.82f),
                ambientEquator: new Color(0.60f, 0.60f, 0.60f),
                ambientGround: new Color(0.40f, 0.35f, 0.28f),
                zenith: new Color(0.26f, 0.47f, 0.81f),
                horizon: new Color(0.78f, 0.82f, 0.88f),
                belowHorizon: new Color(0.70f, 0.75f, 0.82f),
                fogDensity: 0.0020f)),

            // Dusk: the same warmth as dawn from the other side, and a touch denser, because the
            // air a day has been heating is the excuse for the haze being thickest here.
            (19f, Warm(elevation: 10f, azimuth: 248f, intensity: 1.75f, density: 0.0028f)),

            (20.5f, new DaylightState(
                sunElevation: 0f, sunAzimuth: 262f,
                sunColour: new Color(0.92f, 0.52f, 0.40f), sunIntensity: 0.30f, shadowStrength: 0.30f,
                ambientSky: new Color(0.28f, 0.33f, 0.50f),
                ambientEquator: new Color(0.31f, 0.29f, 0.33f),
                ambientGround: new Color(0.18f, 0.15f, 0.16f),
                zenith: new Color(0.14f, 0.21f, 0.44f),
                horizon: new Color(0.62f, 0.42f, 0.42f),
                belowHorizon: new Color(0.44f, 0.32f, 0.34f),
                fogDensity: 0.0032f)),

            (22f, Night(-8f)),
            (24f, Night(0f)),
        };

        /// <summary>
        /// Night, at a given sun bearing.
        ///
        /// <para><b>The sun is put below the horizon rather than switched off</b>, and something
        /// dim is left in the sky. A directional light at zero intensity flattens the world into
        /// unlit ambient, where every face of every object is the same value and the board reads
        /// as a paper cut-out. A weak cool key from below the horizon still separates a wall from
        /// the ground it stands on, which is the difference between night and nothing.</para>
        ///
        /// <para>It is also a readability floor, not realism. A colony sim is unplayable in real
        /// darkness and the reference games all cheat it; the honest description of this key is a
        /// moonlit blue that a player can still work in.</para>
        /// </summary>
        static DaylightState Night(float azimuthOffset) => new DaylightState(
            sunElevation: -12f, sunAzimuth: 180f + azimuthOffset,
            sunColour: new Color(0.62f, 0.72f, 1.00f), sunIntensity: 0.16f, shadowStrength: 0.20f,
            ambientSky: new Color(0.13f, 0.17f, 0.30f),
            ambientEquator: new Color(0.12f, 0.14f, 0.22f),
            ambientGround: new Color(0.07f, 0.08f, 0.12f),
            zenith: new Color(0.03f, 0.05f, 0.13f),
            horizon: new Color(0.11f, 0.15f, 0.29f),
            belowHorizon: new Color(0.08f, 0.11f, 0.22f),
            fogDensity: 0.0026f);

        /// <summary>Dawn and dusk share a palette and differ only in where the sun stands.</summary>
        static DaylightState Warm(float elevation, float azimuth, float intensity, float density) =>
            new DaylightState(
                sunElevation: elevation, sunAzimuth: azimuth,
                sunColour: new Color(1.00f, 0.80f, 0.58f), sunIntensity: intensity,
                shadowStrength: 0.58f,
                ambientSky: new Color(0.54f, 0.58f, 0.76f),
                ambientEquator: new Color(0.60f, 0.55f, 0.52f),
                ambientGround: new Color(0.44f, 0.34f, 0.26f),
                zenith: new Color(0.28f, 0.42f, 0.74f),
                horizon: new Color(0.97f, 0.76f, 0.52f),
                belowHorizon: new Color(0.90f, 0.71f, 0.52f),
                fogDensity: density);

        /// <summary>
        /// Noon: the blue the owner asked for.
        ///
        /// <para>The horizon stays pale rather than going blue with the zenith. A sky that is one
        /// flat blue from top to bottom has no distance in it, and this horizon is also the fog
        /// colour — so making it blue would tint the whole far board blue, which reads as cold
        /// weather rather than as depth.</para>
        /// </summary>
        static DaylightState Blue() => new DaylightState(
            sunElevation: 62f, sunAzimuth: 172f,
            sunColour: new Color(1.00f, 0.98f, 0.94f), sunIntensity: 2.15f, shadowStrength: 0.65f,
            ambientSky: new Color(0.55f, 0.67f, 0.88f),
            ambientEquator: new Color(0.62f, 0.64f, 0.66f),
            ambientGround: new Color(0.36f, 0.33f, 0.28f),
            zenith: new Color(0.20f, 0.44f, 0.86f),
            horizon: new Color(0.74f, 0.86f, 0.96f),
            belowHorizon: new Color(0.68f, 0.79f, 0.90f),
            fogDensity: 0.0017f);

        /// <summary>
        /// The hour the scene is baked at, and what a screenshot tool defaults to.
        ///
        /// <para>Noon, to match <c>OdysseyBootstrap.startHour</c>. They are two different things —
        /// one is what the scene file stores, the other is what tick the colony starts on — but if
        /// they disagree the first frame of a session visibly jumps from the baked sky to the
        /// clock's, which reads as a flash on load.</para>
        /// </summary>
        public const float DefaultHour = 12f;

        /// <summary>
        /// The tick as a continuous hour, 0 to 24.
        ///
        /// <para>Continuous, where <see cref="GameClock.HourOfDay"/> is a whole number, because the
        /// light has to move smoothly and a step once an hour would be a visible jolt in the sky
        /// twenty-four times a day. The clock stays the one place ticks become time; this only
        /// declines to round.</para>
        /// </summary>
        public static float HourOf(long tick)
        {
            long inDay = tick % GameClock.TicksPerDay;
            if (inDay < 0) inDay += GameClock.TicksPerDay;
            return (float)inDay / GameClock.TicksPerHour;
        }

        /// <summary>
        /// The light at an hour, interpolated between the two keys it falls between.
        ///
        /// <para>Wrapping is handled by the table rather than by arithmetic: midnight appears as
        /// both the first key and the last, so an hour past the final key interpolates towards a
        /// state identical to the one at zero, and no caller has to think about the seam.</para>
        /// </summary>
        public static DaylightState Sample(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);

            for (int i = Keys.Length - 1; i >= 0; i--)
            {
                if (hour < Keys[i].Hour) continue;
                if (i == Keys.Length - 1) return Keys[i].State;

                float span = Keys[i + 1].Hour - Keys[i].Hour;
                float t = span <= 0f ? 0f : (hour - Keys[i].Hour) / span;
                // Smoothed, because the keys are far apart and a straight ramp between two of them
                // puts a visible crease in the sky at each one — the light changes direction
                // abruptly at an hour boundary, which the eye picks up as a flicker.
                t = t * t * (3f - 2f * t);
                return DaylightState.Lerp(Keys[i].State, Keys[i + 1].State, t);
            }

            return Keys[0].State;
        }

        /// <summary>
        /// Whether the day is lit the way the Meadow Forest reference is (design 38 §17, the look
        /// pass). On by default: the owner chose the pack's own colours, and the pack's textures are
        /// painted for this light, not for ours — under our noon they read as dark olive.
        /// </summary>
        public static bool MeadowLight { get; set; } = true;

        /// <summary>
        /// A state moved towards the Meadow demo's lighting, by daylight only.
        ///
        /// <para><b>What the reference does.</b> The demo scene lights its meadow with an orange key
        /// at intensity 3 over a heavy trilight ambient at 1.6 — a blue sky term, a pale equator
        /// and a warm ground — so shade is cool and bright rather than dark, and the painted
        /// yellow-green of the terrain comes up to the colour in the screenshots. Our keys were
        /// tuned for a photographic golden hour with a far lighter ambient.</para>
        ///
        /// <para><b>By daylight only.</b> The weight follows the sun's elevation, so dawn, dusk
        /// and night keep the palettes the owner judged; midday takes the whole move. It is a
        /// transform of the sampled state, not new keys, so the table and its tests are
        /// untouched and switching it off gives exactly the old light.</para>
        /// </summary>
        public static DaylightState Meadow(in DaylightState s)
        {
            float day = Mathf.Clamp01(s.SunElevation / 30f);
            if (day <= 0f) return s;

            // The demo's own terms, its ambient intensity of 1.6 folded in, since our trilight has
            // no separate intensity to carry it.
            Color warmKey = new Color(1.00f, 0.80f, 0.60f);
            Color sky = new Color(0.62f, 0.84f, 1.30f) * MeadowAmbientScale;
            Color equator = new Color(0.71f, 0.84f, 0.88f) * MeadowAmbientScale;
            Color ground = new Color(0.69f, 0.60f, 0.42f) * MeadowAmbientScale;

            return new DaylightState(
                s.SunElevation, s.SunAzimuth,
                Color.Lerp(s.SunColour, warmKey, 0.55f * day),
                s.SunIntensity * Mathf.Lerp(1f, MeadowSunScale, day),
                s.ShadowStrength,
                Color.Lerp(s.AmbientSky, sky, 0.85f * day),
                Color.Lerp(s.AmbientEquator, equator, 0.85f * day),
                Color.Lerp(s.AmbientGround, ground, 0.85f * day),
                s.Zenith, s.Horizon, s.BelowHorizon, s.FogDensity);
        }

        /// <summary>How much stronger the Meadow key is at full daylight.</summary>
        public static float MeadowSunScale { get; set; } = 1.35f;

        /// <summary>The demo's ambient intensity, applied to its three trilight colours.</summary>
        public static float MeadowAmbientScale { get; set; } = 1.5f;

        /// <summary>How many keys the table holds. For the tests, which walk it.</summary>
        public static int KeyCount => Keys.Length;

        /// <summary>The hour of one key, in order. For the tests.</summary>
        public static float KeyHour(int index) => Keys[index].Hour;
    }
}
