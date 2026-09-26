#nullable enable
using System.Globalization;

namespace Odyssey.Hud
{
    /// <summary>What last moved the tension gauge (design 59 §5).</summary>
    public enum TensionCause
    {
        /// <summary>Nothing yet: a colony that has lost nobody and has not yet had a quiet day counted.</summary>
        None = 0,
        Died = 1,
        Downed = 2,
        Quiet = 3,
    }

    /// <summary>
    /// The tension gauge on the clock (design 59 §5a, Claude Design's mockup 25g): five bands, told
    /// apart by how many of four columns are filled and only secondly by their hue.
    ///
    /// <para><b>No number and no forecast, ever.</b> The band and the last cause are all a player
    /// reads; the per-mille value behind them stays in the simulation (owner, 2026-09-26: bands and
    /// a cause, not the multiplier, which would invite optimising a number rather than playing).</para>
    ///
    /// <para><b>The Bad red is never a band's colour</b>: the gauge is read, not obeyed, and red is
    /// what an alert is. Reeling and Easing are <c>Info</c>, Even is the text colour, Building and
    /// Peak are <c>Warn</c>, and the filled count is what tells them apart for a player who cannot
    /// tell the hues apart.</para>
    ///
    /// <para>Unity-free (ADR 0003); the presenter paints <see cref="Columns"/> and shows
    /// <see cref="Tooltip"/>.</para>
    /// </summary>
    public static class TensionModel
    {
        public const string GaugeKey = "ui.tension.gauge";
        public const int BandCount = 5;

        /// <summary>No band: the gauge is not drawn (no storyteller, or no preview picked).</summary>
        public const int NoBand = -1;

        public const string DiedKey = "ui.tension.cause.died";
        public const string DownedKey = "ui.tension.cause.downed";
        public const string QuietKey = "ui.tension.cause.quiet";
        public const string QuietOneKey = "ui.tension.cause.quietone";
        public const string NoneKey = "ui.tension.cause.none";
        public const string TodayKey = "ui.tension.ago.today";
        public const string DayAgoKey = "ui.tension.ago.day";
        public const string DaysAgoKey = "ui.tension.ago.days";

        static readonly string[] BandKeys =
        {
            "ui.tension.band.0", "ui.tension.band.1", "ui.tension.band.2", "ui.tension.band.3", "ui.tension.band.4",
        };

        /// <summary>The glyph's box, 16 x 16 (mockup 25g).</summary>
        public const float Box = 16f;

        /// <summary>Four columns, three wide, standing on y 15.5.</summary>
        public static readonly float[] ColumnX = { 0.5f, 4.5f, 8.5f, 12.5f };
        public static readonly float[] ColumnHeight = { 6f, 9f, 12f, 15f };
        public const float ColumnWidth = 3f;
        public const float Bottom = 15.5f;

        /// <summary>An unfilled column is a stub this tall, so the gauge keeps its shape at Reeling.</summary>
        public const float Stub = 2f;

        public static bool IsBand(int band) => band >= 0 && band < BandCount;

        public static string KeyOf(int band) => BandKeys[IsBand(band) ? band : 2];

        public static string LabelOf(int band) => Registry.Label(KeyOf(band));

        /// <summary>How many of the four columns stand full: the band's own number.</summary>
        public static int FilledOf(int band) => IsBand(band) ? band : 0;

        public static HudColour TintOf(int band) => band switch
        {
            0 or 1 => HudTheme.Info,
            3 or 4 => HudTheme.Warn,
            _ => HudTheme.TextPrimary,
        };

        /// <summary>The height column <paramref name="column"/> stands at for <paramref name="band"/>.</summary>
        public static float HeightOf(int band, int column) =>
            column < FilledOf(band) ? ColumnHeight[column] : Stub;

        /// <summary>
        /// The band as one SVG path, <c>M{x} {15.5-h}h3v{h}h-3z</c> per column — the mockup's own
        /// form, kept so the drawing can be checked against 25g and parsed by <c>SvgPath</c>.
        /// </summary>
        public static string PathOf(int band) => Paths[IsBand(band) ? band : 2];

        static readonly string[] Paths = BuildPaths();

        static string[] BuildPaths()
        {
            var paths = new string[BandCount];
            for (int b = 0; b < BandCount; b++)
            {
                var d = new System.Text.StringBuilder();
                for (int c = 0; c < ColumnX.Length; c++)
                {
                    float h = HeightOf(b, c);
                    d.Append('M').Append(F(ColumnX[c])).Append(' ').Append(F(Bottom - h))
                        .Append("h3v").Append(F(h)).Append("h-3z");
                }
                paths[b] = d.ToString();
            }
            return paths;
        }

        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>
        /// The tooltip, two lines: the band's name, then what moved it and when. "A colonist died,
        /// 3 days ago"; "Nine quiet days" is written with a figure, "9 quiet days", because the
        /// count is a count.
        /// </summary>
        public static string Tooltip(int band, TensionCause cause, int days) =>
            LabelOf(band) + "\n" + CauseLine(cause, days);

        public static string CauseLine(TensionCause cause, int days)
        {
            switch (cause)
            {
                case TensionCause.Died: return Registry.Label(DiedKey).Replace("{ago}", Ago(days));
                case TensionCause.Downed: return Registry.Label(DownedKey).Replace("{ago}", Ago(days));
                case TensionCause.Quiet:
                    return days == 1
                        ? Registry.Label(QuietOneKey)
                        : Registry.Label(QuietKey).Replace("{n}", days.ToString(CultureInfo.InvariantCulture));
                default: return Registry.Label(NoneKey);
            }
        }

        static string Ago(int days) =>
            days <= 0 ? Registry.Label(TodayKey)
            : days == 1 ? Registry.Label(DayAgoKey)
            : Registry.Label(DaysAgoKey).Replace("{n}", days.ToString(CultureInfo.InvariantCulture));

        public static readonly string[] IconKeys =
        {
            GaugeKey, BandKeys[0], BandKeys[1], BandKeys[2], BandKeys[3], BandKeys[4],
            DiedKey, DownedKey, QuietKey, QuietOneKey, NoneKey, TodayKey, DayAgoKey, DaysAgoKey,
        };
    }

    /// <summary>
    /// The tooltip string, rebuilt only when what it says changes, so the clock can ask every
    /// refresh and allocate nothing while the gauge is still (ADR 0003).
    /// </summary>
    public sealed class TensionTip
    {
        int _band = int.MinValue;
        TensionCause _cause;
        int _days = int.MinValue;
        string _text = string.Empty;

        public string For(int band, TensionCause cause, int days)
        {
            if (band != _band || cause != _cause || days != _days)
            {
                _band = band;
                _cause = cause;
                _days = days;
                _text = TensionModel.Tooltip(band, cause, days);
            }
            return _text;
        }
    }
}
