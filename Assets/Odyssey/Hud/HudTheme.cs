#nullable enable
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// A colour, as the HUD spells one: eight-bit channels and an alpha, with the exact CSS-style
    /// text the stylesheet carries.
    ///
    /// <para>A value rather than <c>UnityEngine.Color</c> because this assembly is compiled
    /// without UnityEngine (ADR 0003) and the whole point of <see cref="HudTheme"/> is that the
    /// tokens live where the fast tier can read them. The Presentation side converts once.</para>
    /// </summary>
    public readonly struct HudColour
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;

        /// <summary>Opacity, 0 to 1.</summary>
        public readonly float A;

        public HudColour(byte r, byte g, byte b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        /// <summary>
        /// <c>#rrggbb</c> or <c>#rrggbbaa</c>, the form USS takes and the form
        /// <see cref="HudStyleSheetTests"/> matches against the authored sheet. Alpha is rounded to
        /// the nearest byte, which is the resolution USS has.
        /// </summary>
        public string Hex
        {
            get
            {
                int alpha = (int)(A * 255f + 0.5f);
                return alpha >= 255
                    ? $"#{R:x2}{G:x2}{B:x2}"
                    : $"#{R:x2}{G:x2}{B:x2}{alpha:x2}";
            }
        }

        public HudColour WithAlpha(float a) => new HudColour(R, G, B, a);

        public override string ToString() => Hex;
    }

    /// <summary>
    /// What a thing on screen <i>is</i>, for the purpose of colouring its icon stroke.
    ///
    /// <para>The rule from the interface spec is narrow on purpose: <b>category colour lives in
    /// the icon stroke, never in a filled background, and only in stores and the command
    /// bar.</b> Everywhere else an icon inherits the colour of the text beside it. A category is
    /// therefore not decoration hung on every key — it is a small, closed set, and
    /// <see cref="HudTheme.CategoryOf"/> is total so that no key can quietly fall through to a
    /// colour nobody chose.</para>
    ///
    /// <para>Every colour a category resolves to is already a palette token. Nothing on this
    /// screen may introduce a hue of its own: eight categories sharing five tokens is the point,
    /// because a screen with a colour per commodity is a screen with no colour code at all.</para>
    /// </summary>
    public enum HudCategory
    {
        /// <summary>No category: the icon takes the colour of the text beside it.</summary>
        Neutral,

        /// <summary>Food and anything eaten.</summary>
        Sustenance,

        /// <summary>Wood, cloth, anything grown or cut.</summary>
        Organic,

        /// <summary>Scrap, alloy, anything smelted or salvaged.</summary>
        Metal,

        /// <summary>Stone, concrete, anything quarried.</summary>
        Mineral,

        /// <summary>Water and other fluids.</summary>
        Fluid,

        /// <summary>Medicine and anything that treats a body.</summary>
        Medical,

        /// <summary>Placing, digging, ordering: the things the player does to the world.</summary>
        Work,

        /// <summary>People, and the panels about people.</summary>
        People,

        /// <summary>Records, ledgers, factions: the panels about information.</summary>
        Record,
    }

    /// <summary>
    /// The HUD's design tokens, in one place, in the assembly the fast tier can read.
    ///
    /// <para><b>Why this exists.</b> Before this pass every colour and every offset in the
    /// interface was a literal, written once in <c>Hud.uss</c> and again in C# wherever an inline
    /// style needed it, and the stylesheet's own header argued for that: "colours are hardcoded
    /// per class rather than var(--x): the sheet stays legible as plain text". That reasoning
    /// holds for the sheet and fails for the screen, because the acceptance criteria this pass was
    /// given — no two panels overlap, total coverage under eighteen per cent, body text over
    /// 4.5:1 — are statements about numbers, and a number that exists in two files is a number
    /// nobody can test.</para>
    ///
    /// <para>So the tokens live here, the stylesheet is still authored by hand and still legible
    /// as plain text, and <c>HudStyleSheetTests</c> parses the sheet's token block and fails the
    /// fast tier the moment the two disagree. The sheet keeps its literals; it just cannot keep a
    /// literal that nobody agreed to.</para>
    ///
    /// <para><b>Nothing here is simulation.</b> No token is a cell, a save key or a hash bit, and
    /// nothing in <c>Odyssey.Sim</c> can see this assembly at all.</para>
    /// </summary>
    public static class HudTheme
    {
        // ------------------------------------------------------------------ surfaces

        /// <summary>Panel fill. Translucent over the world and blurred behind, so a panel reads as
        /// glass laid on the board rather than as a hole cut in it.</summary>
        public static readonly HudColour PanelFill = new HudColour(12, 16, 20, 0.86f);

        /// <summary>The command bar's fill: the same colour, a little more opaque, because the
        /// bar is always on screen and always carries text.</summary>
        public static readonly HudColour BarFill = new HudColour(12, 16, 20, 0.90f);

        public static readonly HudColour PanelBorder = new HudColour(255, 255, 255, 0.13f);
        public static readonly HudColour Divider = new HudColour(255, 255, 255, 0.09f);

        /// <summary>The blur behind a panel, in pixels.</summary>
        public const int PanelBlur = 6;

        // ------------------------------------------------------------------ ink

        public static readonly HudColour TextPrimary = new HudColour(0xee, 0xf3, 0xf6);
        public static readonly HudColour TextMeta = new HudColour(255, 255, 255, 0.66f);
        public static readonly HudColour TextDim = new HudColour(255, 255, 255, 0.50f);
        public static readonly HudColour TextFaint = new HudColour(255, 255, 255, 0.35f);

        // ------------------------------------------------------------------ signal

        public static readonly HudColour Accent = new HudColour(0x6f, 0xd3, 0xe3);

        /// <summary>The ink laid on top of <see cref="Accent"/> when it is used as a fill.</summary>
        public static readonly HudColour OnAccent = new HudColour(0x0b, 0x11, 0x16);

        public static readonly HudColour Warn = new HudColour(0xe8, 0xb5, 0x5c);
        public static readonly HudColour Bad = new HudColour(0xe0, 0x6a, 0x5c);
        public static readonly HudColour Good = new HudColour(0x7f, 0xc9, 0x8c);
        public static readonly HudColour Info = new HudColour(0x8f, 0xd0, 0xe3);

        /// <summary>The tint laid over a stores row whose stock is falling.</summary>
        public static readonly HudColour FallingRow = Warn.WithAlpha(0.09f);

        /// <summary>The fill behind the active tab of the inspect pane.</summary>
        public static readonly HudColour ActiveTabFill = Accent.WithAlpha(0.12f);

        /// <summary>The ring drawn around the selected colonist's card, outside its border.</summary>
        public static readonly HudColour SelectedRing = Accent.WithAlpha(0.35f);

        // ------------------------------------------------------------------ scrims

        /// <summary>
        /// The colour both scrims fade from. They are always on, and they are the reason the
        /// panels can stay small: text contrast is carried by the scrim rather than by an opaque
        /// panel behind every word (see <see cref="HudContrast"/>).
        /// </summary>
        public static readonly HudColour ScrimInk = new HudColour(8, 11, 14);

        public const float TopScrimAlpha = 0.72f;
        public const float BottomScrimAlpha = 0.78f;
        public const int TopScrimHeight = 170;
        public const int BottomScrimHeight = 200;

        // ------------------------------------------------------------------ geometry

        public const int PanelRadius = 5;
        public const int BarRadius = 6;
        public const int ControlRadius = 4;
        public const int ChipRadius = 3;
        public const int BorderWidth = 1;

        /// <summary>The width of the neutral square drawn where a real glyph does not exist yet.</summary>
        public const float PlaceholderStroke = 1.6f;

        // ------------------------------------------------------------------ categories

        /// <summary>
        /// The stroke colour of an icon in this category. Total by construction: every category
        /// resolves, and <see cref="HudCategory.Neutral"/> resolves to the meta ink, which is what
        /// "inherit the row's colour" means for an icon drawn on its own.
        /// </summary>
        public static HudColour ColourOf(HudCategory category) => category switch
        {
            HudCategory.Sustenance => Good,
            HudCategory.Organic => Warn,
            HudCategory.Metal => Info,
            HudCategory.Mineral => TextMeta,
            HudCategory.Fluid => Accent,
            HudCategory.Medical => Bad,
            HudCategory.Work => Accent,
            HudCategory.People => Good,
            HudCategory.Record => Info,
            _ => TextMeta,
        };

        static readonly Dictionary<string, HudCategory> Categories = new Dictionary<string, HudCategory>
        {
            // stores
            { "ui.res.meal", HudCategory.Sustenance },
            { "ui.res.meat", HudCategory.Sustenance },
            { "ui.res.grain", HudCategory.Sustenance },
            { "ui.res.wood", HudCategory.Organic },
            { "ui.res.fabric", HudCategory.Organic },
            { "ui.res.scrap", HudCategory.Metal },
            { "ui.res.ironore", HudCategory.Metal },
            { "ui.res.stone", HudCategory.Mineral },
            { "ui.res.coal", HudCategory.Mineral },
            { "ui.res.medkit", HudCategory.Medical },

            // command bar
            { "ui.tab.build", HudCategory.Work },
            { "ui.tab.work", HudCategory.Work },
            { "ui.tab.schedule", HudCategory.Work },
            { "ui.tab.research", HudCategory.Record },
            { "ui.tab.colonists", HudCategory.People },
            { "ui.tab.animals", HudCategory.People },
            { "ui.tab.wildlife", HudCategory.People },
            { "ui.tab.bills", HudCategory.Work },
            { "ui.tab.factions", HudCategory.Record },
            { "ui.tab.archive", HudCategory.Record },
            { "ui.tab.menu", HudCategory.Neutral },
        };

        /// <summary>
        /// The category a symbolic icon key belongs to, or <see cref="HudCategory.Neutral"/> for
        /// every key that has not been given one — which is most of them, and correct: only stores
        /// and the command bar are allowed to carry a category colour at all.
        /// </summary>
        public static HudCategory CategoryOf(string key) =>
            key != null && Categories.TryGetValue(key, out HudCategory category)
                ? category
                : HudCategory.Neutral;

        /// <summary>Every key that has been given a category, so a test can hold each to the
        /// naming registry the way the ledger and the settings panel are already held.</summary>
        public static IEnumerable<string> CategorisedKeys => Categories.Keys;
    }
}
