#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// Which of the interface's fixed set of text styles a piece of text is drawn in.
    ///
    /// <para><b>The set is closed and that is the point.</b> The HUD before this pass used
    /// fourteen font sizes between 7 px and 16 px, none of them chosen against the others, because
    /// every region picked its own as it was written. A scale is only a scale if nothing may sit
    /// between its steps, so <see cref="HudType.Of"/> is the only way this HUD is allowed to set a
    /// font size at all and <c>HudTypeTests</c> fails the fast tier if a seventh step appears.</para>
    ///
    /// <para><b>Numbers are a variant, not a step.</b> Every figure on screen — a stores count, the
    /// clock, a coordinate, a percentage, a seed, a hotkey cap — is set in IBM Plex Mono at weight
    /// 500 with tabular figures, so that a column of numbers is a column and a value that changes
    /// does not shuffle the text beside it. That is a change of family at the same step, which is
    /// why it is a flag on <see cref="HudType.Of"/> rather than six more roles.</para>
    /// </summary>
    public enum HudTextRole
    {
        /// <summary>11 / 600, letter-spaced and upper-cased: "STORES", "ALERTS", "DEPTH".</summary>
        PanelLabel,

        /// <summary>12 / 400: the small qualifying line — "colonist · L12 · 78, 59".</summary>
        Meta,

        /// <summary>13 / 400: body text and alert text.</summary>
        Body,

        /// <summary>14 / 500: list rows and command-bar labels.</summary>
        Row,

        /// <summary>19 / 600: the name of the selected thing, and nothing else.</summary>
        Name,

        /// <summary>24 / 500, always mono: the clock, and nothing else.</summary>
        Clock,

        /// <summary>
        /// 11 mono at <see cref="HudTheme.TextFaint"/>: a hotkey cap.
        ///
        /// <para>Named separately by the type spec rather than folded into the six, because it is
        /// not a step in a reading scale — it is a legend, and it is deliberately quiet enough
        /// that it does not compete with the label it belongs to.</para>
        /// </summary>
        Hotkey,

        /// <summary>
        /// 14 / 600, letter-spaced and upper-cased: the heading of a group inside a list, as the
        /// six item categories are in the storage pane.
        ///
        /// <para>A second tracked, upper-cased step above <see cref="PanelLabel"/> rather than a
        /// reuse of it, because the two do different jobs: a panel label titles the whole panel
        /// from outside the content, and this one is <i>in</i> the list, carrying a count and a
        /// box, and has to out-rank the <see cref="Body"/> rows it opens onto. At 11 px it read
        /// as a quieter thing than its own children.</para>
        /// </summary>
        ListHeading,
    }

    /// <summary>How a piece of text is set: a size, a weight, a family and two typographic flags.</summary>
    public readonly struct HudTextStyle
    {
        public readonly int Size;

        /// <summary>CSS weight, 400 to 700. The UI face ships as one variable file, so the
        /// Presentation side resolves anything at or above <see cref="BoldFrom"/> to the
        /// renderer's bold; see <see cref="HudType"/> for why.</summary>
        public readonly int Weight;

        /// <summary>True when this text is set in IBM Plex Mono with tabular figures.</summary>
        public readonly bool Mono;

        /// <summary>Extra tracking in pixels, already resolved from the spec's em value.</summary>
        public readonly float LetterSpacing;

        public readonly bool Uppercase;

        public HudTextStyle(int size, int weight, bool mono, float letterSpacing, bool uppercase)
        {
            Size = size;
            Weight = weight;
            Mono = mono;
            LetterSpacing = letterSpacing;
            Uppercase = uppercase;
        }
    }

    /// <summary>
    /// The interface's type scale: six steps, one legend style, two families.
    ///
    /// <para><b>Faces.</b> Archivo Narrow for words and IBM Plex Mono for figures, both under the
    /// SIL Open Font Licence and both committed under <c>Assets/Odyssey/Presentation/Ui/Fonts/</c>
    /// with their licences beside them. A narrow face is not a style preference here: the command
    /// bar carries eleven labelled items across the bottom of the screen and the acceptance
    /// criteria forbid both an abbreviation and an item running off the edge, so the words have to
    /// be narrow or there is no bar.</para>
    ///
    /// <para><b>Weights are approximated, and it is worth knowing where.</b> Google ships Archivo
    /// Narrow only as a variable font, and Unity's TrueType importer takes the default instance —
    /// weight 400. So 400 and 500 both draw as the regular face and 600 and 700 are handed to the
    /// text renderer as bold, which synthesises them. The alternative was four static files this
    /// project cannot obtain from the upstream repository. <see cref="BoldFrom"/> is the single
    /// place that split is decided, so a real set of static faces later changes one constant and
    /// one loader.</para>
    ///
    /// <para><b>Where this departs from the written spec, and why.</b> The spec's type section
    /// fixes six sizes and says "no others"; its own layout section then asks for 15/600 colonist
    /// names, 11/400 job lines, 10/400 rail hints and a 9 px rail number. Those four are snapped
    /// to the nearest step of the scale — 14/500, 12/400, 12/400 and mono 12 — because a scale
    /// with four exceptions in it is not being enforced by anything, and the differences are
    /// between one and two pixels. The one size named outside the table by the type section
    /// itself, the 11 px mono hotkey cap, is kept, because it was specified there rather than
    /// drawn incidentally.</para>
    /// </summary>
    public static class HudType
    {
        /// <summary>Words.</summary>
        public const string UiFamily = "Archivo Narrow";

        /// <summary>Figures.</summary>
        public const string MonoFamily = "IBM Plex Mono";

        /// <summary>The weight at and above which the renderer is asked for bold.</summary>
        public const int BoldFrom = 600;

        /// <summary>The weight every figure is set in, whatever step it sits at.</summary>
        public const int MonoWeight = 500;

        /// <summary>The spec's .14em tracking on the 11 px panel label, resolved to pixels.</summary>
        public const float PanelLabelTracking = 11f * 0.14f;

        /// <summary>
        /// The tracking on a list heading, .08em at 14 px.
        ///
        /// <para>Deliberately tighter as a ratio than <see cref="PanelLabelTracking"/>: tracking
        /// buys separation in capitals and the need for it falls as the step rises, so .14em at
        /// 14 px would set the word nearly two pixels apart per letter and read as a gap rather
        /// than a heading.</para>
        /// </summary>
        public const float ListHeadingTracking = 14f * 0.08f;

        static readonly Dictionary<HudTextRole, HudTextStyle> Scale =
            new Dictionary<HudTextRole, HudTextStyle>
            {
                { HudTextRole.PanelLabel, new HudTextStyle(11, 600, false, PanelLabelTracking, true) },
                { HudTextRole.Meta, new HudTextStyle(12, 400, false, 0f, false) },
                { HudTextRole.Body, new HudTextStyle(13, 400, false, 0f, false) },
                { HudTextRole.Row, new HudTextStyle(14, 500, false, 0f, false) },
                { HudTextRole.Name, new HudTextStyle(19, 600, false, 0f, false) },
                { HudTextRole.Clock, new HudTextStyle(24, 500, true, 0f, false) },
                { HudTextRole.Hotkey, new HudTextStyle(11, MonoWeight, true, 0f, false) },
                { HudTextRole.ListHeading, new HudTextStyle(14, 600, false, ListHeadingTracking, true) },
            };

        /// <summary>
        /// The style for a role. <paramref name="numeric"/> switches the family to the mono face
        /// at weight 500 without moving the step, which is what "all numbers are mono" means.
        /// </summary>
        public static HudTextStyle Of(HudTextRole role, bool numeric = false)
        {
            if (!Scale.TryGetValue(role, out HudTextStyle style))
                throw new ArgumentOutOfRangeException(nameof(role), role, "no such text role");

            if (!numeric || style.Mono) return style;
            return new HudTextStyle(style.Size, MonoWeight, true, style.LetterSpacing, style.Uppercase);
        }

        /// <summary>Every role, so a test can walk the whole scale.</summary>
        public static IEnumerable<HudTextRole> Roles => Scale.Keys;

        /// <summary>
        /// The six sizes of the reading scale, plus the hotkey legend's eleven. A size that is not
        /// in this set must not reach the screen.
        /// </summary>
        public static IEnumerable<int> Sizes
        {
            get
            {
                var seen = new List<int>();
                foreach (HudTextStyle style in Scale.Values)
                    if (!seen.Contains(style.Size)) seen.Add(style.Size);
                return seen;
            }
        }
    }

    /// <summary>
    /// Contrast arithmetic, so that "body text is legible over the brightest terrain in the game"
    /// is a test rather than an opinion.
    ///
    /// <para><b>The worst case is taken as pure white</b>, not as a sampled terrain colour. Every
    /// terrain in the game is darker than white, so a ratio that holds against white holds against
    /// all of them, and the test cannot go stale when a new biome lands. It is also the only
    /// version of this check that can live in an assembly with no renderer in it.</para>
    ///
    /// <para>WCAG 2 relative luminance, which is the same arithmetic the interface docs quote when
    /// they argue about a grey — <c>Hud.uss</c> already carries one such argument in a comment
    /// about a planned ledger row at "about 3.5:1", decided by hand. This makes that sort of claim
    /// checkable.</para>
    /// </summary>
    public static class HudContrast
    {
        /// <summary>The floor the acceptance criteria set for body text.</summary>
        public const double BodyMinimum = 4.5;

        /// <summary>
        /// The contrast ratio of ink over a panel that is itself laid over the brightest thing the
        /// world can draw. Both compositions are done in sRGB space, which is where the alpha
        /// blend actually happens on screen.
        /// </summary>
        public static double OverBrightestTerrain(HudColour ink, HudColour panel)
        {
            HudColour background = Over(panel, new HudColour(255, 255, 255));
            return Ratio(Over(ink, background), background);
        }

        /// <summary>Composite <paramref name="top"/> over an opaque <paramref name="under"/>.</summary>
        public static HudColour Over(HudColour top, HudColour under)
        {
            byte Mix(byte a, byte b) => (byte)(a * top.A + b * (1f - top.A) + 0.5f);
            return new HudColour(Mix(top.R, under.R), Mix(top.G, under.G), Mix(top.B, under.B));
        }

        /// <summary>WCAG 2 contrast ratio between two opaque colours.</summary>
        public static double Ratio(HudColour a, HudColour b)
        {
            double la = Luminance(a);
            double lb = Luminance(b);
            double hi = Math.Max(la, lb);
            double lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }

        public static double Luminance(HudColour colour) =>
            0.2126 * Linear(colour.R) + 0.7152 * Linear(colour.G) + 0.0722 * Linear(colour.B);

        static double Linear(byte channel)
        {
            double c = channel / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }
}
