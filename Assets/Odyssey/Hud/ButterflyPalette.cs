#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>
    /// What a butterfly is coloured with, by day and by night (design 52 §5–§6): the species it is
    /// drawn as, and the hue it glows in once the sun is down.
    ///
    /// <para><b>The one owner of these colours.</b> The shader reads every one of them as a uniform
    /// array the director sets once (<c>ButterflyDirector</c>); nothing in <c>OdysseyButterfly.shader</c>
    /// writes a colour of its own. So the fast tier can hold the night palette to the colour-blind
    /// rule (<c>ButterflyTests</c>) and the picture cannot quietly disagree with the test (P1).</para>
    ///
    /// <para>In <c>Odyssey.Hud</c> for the reason <c>BirdSpecies</c> is: it is engine-free and the
    /// fast tier runs it.</para>
    /// </summary>
    public static class ButterflyPalette
    {
        /// <summary>
        /// One species as the shader draws it: four colours and the ground-plan switches (e-14 §1).
        /// The colours are sRGB bytes, as every <see cref="HudColour"/> is.
        /// </summary>
        public readonly struct Species
        {
            public Species(string name, HudColour ground, HudColour dark, HudColour accent, HudColour eye,
                float veins, float margin, float eyespots, float band, float marginSpots)
            {
                Name = name;
                Ground = ground;
                Dark = dark;
                Accent = accent;
                Eye = eye;
                Veins = veins;
                Margin = margin;
                Eyespots = eyespots;
                Band = band;
                MarginSpots = marginSpots;
            }

            /// <summary>For the design document and a test's message; never drawn.</summary>
            public readonly string Name;

            /// <summary>The wing's ground colour.</summary>
            public readonly HudColour Ground;

            /// <summary>Veins, the margin and an eyespot's outer ring.</summary>
            public readonly HudColour Dark;

            /// <summary>Marginal spots, an eyespot's focus, and the central band where it is not dark.</summary>
            public readonly HudColour Accent;

            /// <summary>An eyespot's coloured disc.</summary>
            public readonly HudColour Eye;

            /// <summary>How wide a vein is drawn, as a fraction of its wing cell. Zero draws none.</summary>
            public readonly float Veins;

            /// <summary>How wide the dark margin is, as a fraction of the wing's radius. Zero draws none.</summary>
            public readonly float Margin;

            /// <summary>The chance that one wing cell carries an eyespot.</summary>
            public readonly float Eyespots;

            /// <summary>The central band: 0 none, 1 dark, 2 in the accent colour.</summary>
            public readonly float Band;

            /// <summary>Whether the margin carries a row of pale spots, 0 or 1.</summary>
            public readonly float MarginSpots;
        }

        /// <summary>
        /// Six species from the table in e-14 §2. A butterfly is dealt one by its seed. The names are
        /// descriptive, never drawn, and none is a proper noun of the game's.
        /// </summary>
        public static readonly Species[] All =
        {
            new Species("monarch", new HudColour(0xE8, 0x75, 0x1A), new HudColour(0x1A, 0x1A, 0x1A),
                new HudColour(0xFF, 0xFF, 0xFF), new HudColour(0xE8, 0x75, 0x1A),
                veins: 0.10f, margin: 0.14f, eyespots: 0f, band: 0f, marginSpots: 1f),
            new Species("morpho", new HudColour(0x2F, 0x7C, 0xFF), new HudColour(0x11, 0x11, 0x11),
                new HudColour(0xFF, 0xFF, 0xFF), new HudColour(0x2F, 0x7C, 0xFF),
                veins: 0.03f, margin: 0.20f, eyespots: 0.15f, band: 0f, marginSpots: 1f),
            new Species("cabbage white", new HudColour(0xF4, 0xF1, 0xE4), new HudColour(0x3A, 0x3A, 0x3A),
                new HudColour(0x3A, 0x3A, 0x3A), new HudColour(0x3A, 0x3A, 0x3A),
                veins: 0.02f, margin: 0.08f, eyespots: 0.25f, band: 0f, marginSpots: 0f),
            new Species("swallowtail", new HudColour(0xF5, 0xD5, 0x31), new HudColour(0x15, 0x15, 0x15),
                new HudColour(0x3A, 0x7B, 0xD5), new HudColour(0xE0, 0x45, 0x2B),
                veins: 0.08f, margin: 0.16f, eyespots: 0.2f, band: 1f, marginSpots: 1f),
            new Species("red admiral", new HudColour(0x1C, 0x1A, 0x1A), new HudColour(0x12, 0x10, 0x10),
                new HudColour(0xD8, 0x43, 0x1E), new HudColour(0xFF, 0xFF, 0xFF),
                veins: 0f, margin: 0.10f, eyespots: 0f, band: 2f, marginSpots: 1f),
            new Species("peacock", new HudColour(0xB4, 0x26, 0x2E), new HudColour(0x1A, 0x1A, 0x1A),
                new HudColour(0xF2, 0xD2, 0x4B), new HudColour(0x2C, 0x5F, 0xD6),
                veins: 0.02f, margin: 0.12f, eyespots: 0.45f, band: 0f, marginSpots: 0f),
        };

        /// <summary>One hue a butterfly may glow in, how often it is dealt, and how bright it is.</summary>
        public readonly struct Glow
        {
            public Glow(string name, HudColour colour, float weight, float gain)
            {
                Name = name;
                Colour = colour;
                Weight = weight;
                Gain = gain;
            }

            public readonly string Name;

            /// <summary>The hue at full saturation, before any gain.</summary>
            public readonly HudColour Colour;

            /// <summary>The share of butterflies dealt it. The four sum to one.</summary>
            public readonly float Weight;

            /// <summary>
            /// How much brighter than the others it is drawn. Violet carries half again: it sits
            /// forty-five degrees from the night's own blue and contributes little luminance, so it
            /// has to earn its contrast through brightness rather than hue (e-14 §5).
            /// </summary>
            public readonly float Gain;
        }

        /// <summary>
        /// The night palette (owner, 2026-09-25: full spectrum, weighted to cyan, violet, magenta and
        /// amber). Weights and the violet gain are e-14's recommendation. The colours were chosen so
        /// that no two are told apart by hue alone under any dichromacy — they differ in lightness
        /// too — which <c>ButterflyTests.NoTwoGlowsLookAlikeToAColourBlindPlayer</c> holds.
        /// </summary>
        public static readonly Glow[] Glows =
        {
            new Glow("cyan", new HudColour(0x5C, 0xF2, 0xFF), weight: 0.30f, gain: 1.0f),
            new Glow("magenta", new HudColour(0xFF, 0x6E, 0xDC), weight: 0.25f, gain: 1.15f),
            new Glow("amber", new HudColour(0xFF, 0xA6, 0x1E), weight: 0.25f, gain: 1.0f),
            new Glow("violet", new HudColour(0x8A, 0x4C, 0xFF), weight: 0.20f, gain: 1.5f),
        };

        /// <summary>How far a glow's hue wanders either way, in degrees (e-14: ±25° over 30–60 s).</summary>
        public const float HueDriftDegrees = 25f;

        /// <summary>The slowest and quickest a hue completes one wander, in seconds.</summary>
        public const float HueDriftSlowest = 60f, HueDriftQuickest = 30f;

        /// <summary>The slowest and quickest breath, in cycles a second (e-14 §7: 0.15–0.25 Hz).</summary>
        public const float PulseSlowest = 0.15f, PulseQuickest = 0.25f;

        /// <summary>
        /// The dimmest a breath goes, as a fraction of its peak. A glow that goes dark reads as
        /// blinking, which is a signal; one that only dims reads as alive (e-14 §7).
        /// </summary>
        public const float PulseFloor = 0.55f;

        /// <summary>
        /// The brightest the wing itself is drawn at night, in linear HDR. At or under one on
        /// purpose: the wing is a few pixels across, and anything thinner than bloom's
        /// half-resolution prefilter that crosses the 1.1 threshold shimmers as it moves (d-24 §7).
        /// The spectacle is the halo, which the pass draws itself.
        /// </summary>
        public const float WingGlowCeiling = 1.0f;

        /// <summary>The brightest point of a halo, in linear HDR — over the bloom threshold, on a
        /// disc wide enough that the prefilter sees it whole.</summary>
        public const float HaloPeak = 1.6f;

        /// <summary>
        /// How far a butterfly's light reaches over the ground, colonists and walls around it, in
        /// metres (owner: "bloom plus a glow pool"). Drawn by the pass, never a URP light (d-24 §6).
        /// </summary>
        public const float LightRadius = 2.2f;

        /// <summary>How strong that light is where it is strongest, added to the lit scene.</summary>
        public const float LightGain = 0.45f;

        /// <summary>
        /// How lit-up the night is for a butterfly, 0 by day to 1 in full dark, from the sun's
        /// elevation in degrees — so the glow rises with the night grade rather than at a clock hour.
        /// Nothing until the sun is four degrees above the horizon; everything once it is eight
        /// below, which is the night key's own elevation band (<c>Daylight</c>: −12° at night).
        /// </summary>
        public static float NightFor(float sunElevationDegrees)
        {
            float t = Clamp01((GlowStartsAt - sunElevationDegrees) / (GlowStartsAt - GlowFullAt));
            return t * t * (3f - 2f * t);
        }

        /// <summary>The sun elevations the glow starts rising at and is full at, in degrees.</summary>
        public const float GlowStartsAt = 4f, GlowFullAt = -8f;

        /// <summary>Which glow a seed is dealt, by the weights: the shader deals the same way.</summary>
        public static int GlowFor(float unit)
        {
            float running = 0f;
            for (int i = 0; i < Glows.Length; i++)
            {
                running += Glows[i].Weight;
                if (unit < running) return i;
            }
            return Glows.Length - 1;
        }

        /// <summary>A byte channel as linear light, 0 to 1. The shader's colours are linear.</summary>
        public static float Linear(byte channel)
        {
            float c = channel / 255f;
            return c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
