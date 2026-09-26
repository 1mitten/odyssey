#nullable enable

namespace Odyssey.Hud
{
    /// <summary>
    /// How good a number is, on one scale from bad to good, in the units the number is shown in
    /// (design 59). Three anchors: the value that is fully <see cref="Bad"/>, the one that is
    /// <see cref="Questionable"/>, and the one that is fully <see cref="Good"/>. They may run
    /// either way — pain is good low and bad high — and a value past an end is that end.
    /// </summary>
    public readonly struct StatScale
    {
        public readonly int Bad;
        public readonly int Questionable;
        public readonly int Good;

        public StatScale(int bad, int questionable, int good)
        {
            Bad = bad;
            Questionable = questionable;
            Good = good;
        }

        /// <summary>
        /// The value's score in per mille: nought at <see cref="Bad"/>, 500 at
        /// <see cref="Questionable"/>, a thousand at <see cref="Good"/>, linear between, clamped
        /// past either end.
        /// </summary>
        public int Score(int value)
        {
            bool rising = Good >= Bad;
            if (rising ? value <= Bad : value >= Bad) return 0;
            if (rising ? value >= Good : value <= Good) return 1000;
            if (rising ? value <= Questionable : value >= Questionable)
                return Span(value - Bad, Questionable - Bad, 0);
            return Span(value - Questionable, Good - Questionable, 500);
        }

        static int Span(int along, int length, int from) =>
            length == 0 ? from + 500 : from + (int)((long)along * 500 / length);
    }

    /// <summary>Which set of inks a judged number is drawn in.</summary>
    public enum StatPalette
    {
        /// <summary>Text and bars on the dark interface panels: the stat tokens.</summary>
        Panel,

        /// <summary>Bars drawn over the lit board: the same hues, deeper (design 33 §8a).</summary>
        World,
    }

    /// <summary>
    /// <b>The one place a number's colour is decided</b> (design 59; owner, 2026-09-26: <i>"0%
    /// armour should be red … good temperature is green, amber if questionable and more towards
    /// red if not good … apply this everywhere so we have consistency … use a central point to
    /// configure/style all of this"</i>).
    ///
    /// <para>Every figure the interface judges — a need, health, pain, blood loss, a capacity,
    /// armour, a temperature, a comfortable range, fuel, pace, a shot's chance — has its
    /// <see cref="StatScale"/> here and nowhere else, and every one is drawn through
    /// <see cref="Ink(int, StatPalette)"/>: one red–amber–green ramp in one of two palettes. To
    /// retune a threshold, change its scale; to restyle the lot, change the palette or
    /// <see cref="Blend"/>. A surface never writes a threshold or picks a colour itself.</para>
    ///
    /// <para><b>What is not judged</b>, deliberately: rain protection (the owner: "a neutral bright
    /// white"), the kit's "used of capacity" (a count, not a quality), skill levels (their own ramp,
    /// <see cref="WorkBands"/>, is a rank rather than a verdict), and quality tiers (a name, with
    /// its own colours in <see cref="HudTheme.Quality"/>).</para>
    /// </summary>
    public static class StatInks
    {
        // ---- the style ------------------------------------------------------------------------

        /// <summary>
        /// True: the colour blends continuously along the ramp, so a figure drifting worse drifts
        /// redder. False: three bands — the nearest anchor's colour, as the need bars were drawn
        /// before 2026-09-26. One switch for every judged number.
        /// </summary>
        public static bool Blend = true;

        /// <summary>The panel inks: the interface's own stat tokens.</summary>
        public static readonly HudColour PanelBad = HudTheme.Bad;
        public static readonly HudColour PanelQuestionable = HudTheme.Warn;
        public static readonly HudColour PanelGood = HudTheme.Good;

        // The owner, 2026-09-23 (design 33 §8a): "use a green like the one used in the colony stats -
        // more greener - deeper colours please". Each is its stat token's hue held to a degree —
        // HudTheme.Good 130.5°, Warn 38.1°, Bad 6.4° — with the saturation raised to about 0.7 to 0.8
        // and the value lowered, because a bar over the board is drawn through a translucent, glowing
        // material that lifts every colour towards white. HealthBarLayoutTests holds the hue, the
        // extra saturation and the depth.

        /// <summary>The world's green: <see cref="HudTheme.Good"/>'s hue, saturation 0.72, value 0.70.</summary>
        public static readonly HudColour WorldGood = new HudColour(0x32, 0xb3, 0x49);

        /// <summary>The world's amber: <see cref="HudTheme.Warn"/>'s hue, saturation 0.82, value 0.85.</summary>
        public static readonly HudColour WorldQuestionable = new HudColour(0xd9, 0x98, 0x27);

        /// <summary>The world's red: <see cref="HudTheme.Bad"/>'s hue, saturation 0.80, value 0.80.</summary>
        public static readonly HudColour WorldBad = new HudColour(0xcc, 0x3a, 0x29);

        // ---- the scales: every judged number in the game ---------------------------------------

        /// <summary>A need, in thousandths full: red at 20 %, amber at 40 %, green from 60 %.</summary>
        public static readonly StatScale Need = new StatScale(200, 400, 600);

        /// <summary>What is left of a pool or a region, in per mille: a need's scale.</summary>
        public static readonly StatScale Health = Need;

        /// <summary>A capacity (consciousness, moving, manipulation), in per mille: a need's scale.</summary>
        public static readonly StatScale Capacity = Need;

        /// <summary>Pain, in per mille, lower is better: green to 10 %, amber at 30 %, red from 60 %.</summary>
        public static readonly StatScale Pain = new StatScale(600, 300, 100);

        /// <summary>Blood lost, in per mille, lower is better: green to 5 %, amber at 15 %, red from 45 %.</summary>
        public static readonly StatScale BloodLoss = new StatScale(450, 150, 50);

        /// <summary>Armour, in per cent: red at none, amber at 20 %, green from 40 %.</summary>
        public static readonly StatScale Armour = new StatScale(0, 20, 40);

        /// <summary>A hopper's fuel, in per mille of its capacity: red empty, amber half, green full.</summary>
        public static readonly StatScale Fuel = new StatScale(0, 500, 1000);

        /// <summary>Pace against the standard walk, in per mille: red at half, amber at 80 %, green from 100 %.</summary>
        public static readonly StatScale Pace = new StatScale(500, 800, 1000);

        /// <summary>A shot's chance to hit, in per mille: red at 20 %, amber at 50 %, green from 80 %.</summary>
        public static readonly StatScale HitChance = new StatScale(200, 500, 800);

        /// <summary>
        /// The bare colonist's comfortable range, 16 to 26 °C in centi-degrees — the issued
        /// jumpsuit's, and design 28's comfort band. A temperature on its own is judged against it.
        /// </summary>
        public const int ComfortLow = 1_600, ComfortHigh = 2_600;

        /// <summary>
        /// How far outside a comfortable range a temperature is amber (6 °C) and fully red (12 °C).
        /// Round the bare range that is amber at 10 and 32 °C, red at 4 and 38 °C, so a
        /// campfire's own tile, past 35 °C, reads in the red half as the owner asked of it
        /// (<c>RadiantPaneTests</c>).
        /// </summary>
        public const int ComfortQuestionableOutside = 600, ComfortBadOutside = 1_200;

        // ---- judging ---------------------------------------------------------------------------

        /// <summary>
        /// A temperature against a comfortable range, all in centi-degrees: a thousand inside it,
        /// then down the same ramp on either side — amber <see cref="ComfortQuestionableOutside"/>
        /// out, red <see cref="ComfortBadOutside"/> out.
        /// </summary>
        public static int Comfort(int lowCentiC, int highCentiC, int centiC)
        {
            int outside = centiC < lowCentiC ? lowCentiC - centiC
                : centiC > highCentiC ? centiC - highCentiC
                : 0;
            return new StatScale(ComfortBadOutside, ComfortQuestionableOutside, 0).Score(outside);
        }

        /// <summary>The colour of a score in per mille, in <paramref name="palette"/>.</summary>
        public static HudColour Ink(int score, StatPalette palette = StatPalette.Panel)
        {
            HudColour bad = palette == StatPalette.World ? WorldBad : PanelBad;
            HudColour questionable = palette == StatPalette.World ? WorldQuestionable : PanelQuestionable;
            HudColour good = palette == StatPalette.World ? WorldGood : PanelGood;
            if (!Blend) return score < 250 ? bad : score < 750 ? questionable : good;
            if (score <= 0) return bad;
            if (score >= 1000) return good;
            return score <= 500 ? Mix(bad, questionable, score) : Mix(questionable, good, score - 500);
        }

        /// <summary>The colour of <paramref name="value"/> on <paramref name="scale"/>.</summary>
        public static HudColour Ink(in StatScale scale, int value, StatPalette palette = StatPalette.Panel) =>
            Ink(scale.Score(value), palette);

        /// <summary>A temperature on its own — a tile's, the outdoor reading — against the bare comfortable range.</summary>
        public static HudColour Temperature(int centiC) => Ink(Comfort(ComfortLow, ComfortHigh, centiC));

        /// <summary><paramref name="from"/> to <paramref name="to"/> by <paramref name="t"/> of 500, per channel.</summary>
        static HudColour Mix(HudColour from, HudColour to, int t) => new HudColour(
            (byte)((from.R * (500 - t) + to.R * t + 250) / 500),
            (byte)((from.G * (500 - t) + to.G * t + 250) / 500),
            (byte)((from.B * (500 - t) + to.B * t + 250) / 500));
    }
}
