#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The stylesheet says what the tokens say.
    ///
    /// <para><b>Why this test exists.</b> <c>Hud.uss</c> writes its colours as literals rather
    /// than as custom properties, and that is a deliberate, long-standing position in this project
    /// — the sheet stays legible as plain text and there are no variable-resolution surprises
    /// across panel copies. The cost of that position is drift: the same colour lives in the sheet
    /// and in <see cref="HudTheme"/>, which is where the acceptance criteria are measured, and
    /// nothing has ever compared the two. So this reads the sheet and compares it. The literals
    /// stay; what cannot happen any more is a literal nobody agreed to.</para>
    ///
    /// <para>The same goes for the anchors. <see cref="HudLayout"/> is the arithmetic that decides
    /// whether two panels overlap, and the sheet is what actually places them, so if <c>left:20</c>
    /// and <c>HudLayout.Edge</c> ever disagree the overlap test is answering a question about a
    /// screen nobody is looking at.</para>
    ///
    /// <para>It runs in both tiers. Under Unity the working directory is the project root; in the
    /// fast tier it is a build output several levels down, so the file is found by walking up.</para>
    /// </summary>
    public class HudStyleSheetTests
    {
        const string SheetPath = "Assets/Odyssey/Presentation/Ui/Hud.uss";

        static Dictionary<string, Dictionary<string, string>>? _rules;

        static Dictionary<string, Dictionary<string, string>> Rules => _rules ??= Parse(Read());

        // ------------------------------------------------------------------ colour parity

        static readonly (string Selector, string Property, Func<HudColour> Token, string Name)[] Colours =
        {
            (".panel", "background-color", () => HudTheme.PanelFill, "panel fill"),
            (".panel", "border-color", () => HudTheme.PanelBorder, "panel border"),
            (".card", "background-color", () => HudTheme.PanelFill, "panel fill"),
            (".card", "border-color", () => HudTheme.PanelBorder, "panel border"),
            (".commandbar", "background-color", () => HudTheme.BarFill, "bar fill"),
            (".commandbar", "border-color", () => HudTheme.PanelBorder, "panel border"),
            (".window", "background-color", () => HudTheme.PopoverFill, "popover fill"),

            (".stores__name", "color", () => HudTheme.TextPrimary, "text primary"),
            (".stores__value", "color", () => HudTheme.TextPrimary, "text primary"),
            (".inspect__title", "color", () => HudTheme.TextPrimary, "text primary"),
            (".panel__label", "color", () => HudTheme.TextMeta, "text meta"),
            (".inspect__meta", "color", () => HudTheme.TextMeta, "text meta"),
            (".stores__count", "color", () => HudTheme.TextDim, "text dim"),
            (".inspect__state", "color", () => HudTheme.TextDim, "text dim"),
            (".cmd__key", "color", () => HudTheme.TextFaint, "text faint"),
            (".menu__key", "color", () => HudTheme.TextFaint, "text faint"),

            (".card--sel", "border-color", () => HudTheme.Accent, "accent"),
            (".cmd--primary", "background-color", () => HudTheme.Accent, "accent"),
            (".speed__btn--on", "background-color", () => HudTheme.Accent, "accent"),
            (".rail__cell--active", "background-color", () => HudTheme.Accent, "accent"),
            (".cmd__label--primary", "color", () => HudTheme.OnAccent, "on-accent ink"),
            (".rail__number", "color", () => HudTheme.OnAccent, "on-accent ink"),
            (".card__initial", "color", () => HudTheme.OnAccent, "on-accent ink"),

            (".bar__fill", "background-color", () => HudTheme.Good, "good"),
            (".skill__pip", "background-color", () => HudTheme.Warn, "warn"),
            (".skill__name", "color", () => HudTheme.TextPrimary, "text primary"),
            (".skill__level", "color", () => HudTheme.TextMeta, "text meta"),
            (".rail__dot", "background-color", () => HudTheme.Warn, "warn"),
            (".inspect__reason", "color", () => HudTheme.Warn, "warn"),

            (".stores__row--falling", "background-color", () => HudTheme.FallingRow, "falling row tint"),
            (".tab--on", "background-color", () => HudTheme.ActiveTabFill, "active tab fill"),
            (".card__ring", "border-color", () => HudTheme.SelectedRing, "selection ring"),
            (".inspect__tabs", "border-bottom-color", () => HudTheme.Divider, "divider"),
            (".commandbar__divider", "background-color", () => HudTheme.Divider, "divider"),
        };

        [Test]
        public void EveryColourInTheSheetIsATokenFromTheTheme()
        {
            foreach ((string selector, string property, Func<HudColour> token, string name) in Colours)
            {
                string? declared = Declaration(selector, property);
                Assert.That(declared, Is.Not.Null,
                    $"{SheetPath} has no {property} on {selector}, which the theme expects to be the {name}");

                HudColour expected = token();
                HudColour actual = ParseColour(declared!);
                Assert.That(Same(actual, expected), Is.True,
                    $"{selector} {{ {property}: {declared} }} is not the {name} ({expected.Hex}). " +
                    "The stylesheet and HudTheme have drifted apart, and HudTheme is the one the " +
                    "acceptance criteria are measured against.");
            }
        }

        // ------------------------------------------------------------------ anchor parity

        static readonly (string Selector, string Property, Func<float> Token, string Name)[] Lengths =
        {
            (".stores", "left", () => HudLayout.Edge, "screen edge margin"),
            (".stores", "top", () => HudLayout.Edge, "screen edge margin"),
            (".stores", "width", () => HudLayout.StoresWidth, "stores width"),

            (".rail", "right", () => HudLayout.Edge, "screen edge margin"),
            (".rail", "top", () => HudLayout.Edge, "screen edge margin"),
            (".rail", "width", () => HudLayout.RailWidth, "rail width"),
            (".rail", "padding-left", () => HudLayout.RailSidePad, "rail side padding"),
            (".rail__cell", "width", () => HudLayout.RailCellWidth, "rail cell"),
            (".rail__cell", "height", () => HudLayout.RailCellHeight, "rail cell"),
            (".rail__cell", "margin-bottom", () => HudLayout.RailCellGap, "rail cell gap"),

            (".column-right", "right", () => HudLayout.Edge + HudLayout.RailWidth + HudLayout.RailToClock,
                "the rail's width plus its gap, measured from the right edge"),
            (".column-right", "top", () => HudLayout.Edge, "screen edge margin"),
            (".column-right", "width", () => HudLayout.ClockWidth, "clock width"),
            (".alerts", "margin-top", () => HudLayout.Gap, "gap between panels"),
            (".clock__line", "height", () => HudLayout.ClockRow, "clock row"),
            (".speed", "height", () => HudLayout.SpeedButton, "speed button"),
            (".speed", "margin-top", () => HudLayout.ClockGap, "clock to speed"),
            (".speed__btn", "height", () => HudLayout.SpeedButton, "speed button"),

            (".build", "width", () => HudLayout.BuildWidth, "build palette width"),
            (".build__scroll", "max-height", () => HudLayout.BuildCatHeight, "category group height"),
            (".chip", "height", () => HudLayout.BuildChip, "a palette chip"),
            (".chip", "margin", () => HudLayout.BuildChipMargin, "chip margin"),

            (".inspect", "left", () => HudLayout.Edge, "screen edge margin"),
            (".inspect", "bottom", () => HudLayout.InspectBottom, "inspect bottom offset"),
            (".inspect", "width", () => HudLayout.InspectWidth, "inspect width"),
            (".inspect__hdr", "height", () => HudLayout.InspectHeader, "inspect header"),
            (".inspect__tabs", "height", () => HudLayout.InspectTabs, "inspect tab strip"),
            (".inspect__tabs", "margin-top", () => HudLayout.InspectHeaderGap, "header to tabs"),
            (".needs", "margin-top", () => HudLayout.InspectTabGap, "tabs to needs"),
            (".need", "margin-bottom", () => HudLayout.NeedRowGap, "need row gap"),
            (".skill", "height", () => HudLayout.SkillRow, "a skill line"),
            (".skill", "margin-bottom", () => HudLayout.SkillRowGap, "skill row gap"),

            (".alert", "min-height", () => HudLayout.AlertHeight, "an alert row"),
            (".alerts__rows", "margin-top", () => HudLayout.HeaderGap, "header to first alert"),

            (".strip", "top", () => HudLayout.StripTop, "the strip docked to the top"),
            (".card", "width", () => HudLayout.CardWidth, "card width"),
            (".card", "height", () => HudLayout.CardHeight, "card height"),
            (".card__avatar", "width", () => HudLayout.CardAvatar, "card avatar"),
            (".card__jobrow", "height", () => HudLayout.CardJobRow, "card activity line"),
            (".card__job", "margin-left", () => HudLayout.CardIconGap, "card icon to word"),
            (".card__names", "margin-left", () => HudLayout.CardAvatarGap, "card avatar to name"),
            (".card", "padding", () => HudLayout.CardPad, "card padding"),

            (".bar-row", "bottom", () => HudLayout.BarBottom, "the command bar docked to the bottom"),
            (".commandbar", "padding", () => HudCommands.BarPad, "command bar padding"),
            (".cmd", "height", () => HudCommands.ItemHeight, "command item height"),
            (".cmd", "margin-right", () => HudCommands.ItemGap, "command item gap"),
            (".commandbar__divider", "height", () => HudCommands.DividerHeight, "divider height"),
            (".commandbar__divider", "width", () => HudCommands.DividerWidth, "divider width"),

            (".panel", "padding", () => HudLayout.Pad, "panel padding"),
            (".panel", "border-radius", () => HudTheme.PanelRadius, "panel radius"),
            (".panel", "border-width", () => HudTheme.BorderWidth, "panel border"),
            (".commandbar", "border-radius", () => HudTheme.BarRadius, "command bar radius"),
            (".panel__hdr", "height", () => HudLayout.HeaderHeight, "panel header"),
            (".stores__rows", "margin-top", () => HudLayout.HeaderGap, "header to first row"),
            (".stores__row", "height", () => HudLayout.RowHeight, "list row"),
            (".stores__name", "margin-left", () => HudLayout.RowIconGap, "icon to label"),
        };

        [Test]
        public void EveryAnchorInTheSheetIsTheNumberTheLayoutModelUses()
        {
            foreach ((string selector, string property, Func<float> token, string name) in Lengths)
            {
                string? declared = Declaration(selector, property);
                Assert.That(declared, Is.Not.Null,
                    $"{SheetPath} has no {property} on {selector}, which the layout model expects " +
                    $"to be the {name}");

                float expected = token();
                float actual = ParseLength(declared!);
                Assert.That(actual, Is.EqualTo(expected).Within(0.01f),
                    $"{selector} {{ {property}: {declared} }} against a modelled {name} of " +
                    $"{expected}. The model is what HudLayoutTests proves does not overlap; the " +
                    "sheet is what actually places the panel, so they have to be the same number.");
            }
        }

        [Test]
        public void TheSheetSetsNoTypeAtAll()
        {
            // Type comes from HudType through HudText, because "six sizes and no others" is an
            // acceptance criterion and a size written in the sheet could only be tested by parsing
            // it. Two sources for one decision is how the HUD this replaces came to have fourteen
            // font sizes in it.
            string sheet = StripComments(Read());
            foreach (string forbidden in new[] { "font-size", "-unity-font-style", "letter-spacing" })
                Assert.That(sheet, Does.Not.Contain(forbidden),
                    $"{SheetPath} sets {forbidden}. Type belongs to HudType, which the fast tier " +
                    "can read; the sheet owns colour, space and state.");
        }

        [Test]
        public void TheScrimsAreTheHeightsTheThemeDeclares()
        {
            // Not in the sheet: the two scrims are sized from HudTheme in code, because their ramp
            // textures are generated there too and a gradient cannot be written in USS at all. The
            // check that matters is that they are still big enough to do their job — carrying text
            // contrast under the panels at the top and bottom of the screen.
            Assert.That(HudTheme.TopScrimHeight, Is.GreaterThanOrEqualTo(
                HudLayout.StripTop + HudLayout.StripHeight(HudLayout.StripRows)),
                "the top scrim has to reach under the colonist strip at its full height");
            Assert.That(HudTheme.BottomScrimHeight, Is.GreaterThanOrEqualTo(
                HudLayout.BarBottom + HudCommands.BarHeight),
                "the bottom scrim has to reach under the command bar");
        }

        // ------------------------------------------------------------------ the little parser

        static string? Declaration(string selector, string property) =>
            Rules.TryGetValue(selector, out Dictionary<string, string>? block) &&
            block.TryGetValue(property, out string? value)
                ? value
                : null;

        static string Read()
        {
            string? path = Find(SheetPath);
            if (path == null)
                Assert.Fail($"could not find {SheetPath} from {Directory.GetCurrentDirectory()} " +
                            $"or above {AppContext.BaseDirectory}");
            return File.ReadAllText(path!);
        }

        /// <summary>
        /// The project root differs between the two tiers: Unity runs with it as the working
        /// directory, the fast tier runs from a build output several levels below it. Walking up
        /// from both covers the pair without either needing to know about the other.
        /// </summary>
        static string? Find(string relative)
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var directory = new DirectoryInfo(start);
                while (directory != null)
                {
                    string candidate = Path.Combine(directory.FullName, relative);
                    if (File.Exists(candidate)) return candidate;
                    directory = directory.Parent;
                }
            }
            return null;
        }

        static string StripComments(string text) =>
            Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        static Dictionary<string, Dictionary<string, string>> Parse(string text)
        {
            var rules = new Dictionary<string, Dictionary<string, string>>();

            foreach (Match rule in Regex.Matches(StripComments(text), @"([^{}]+)\{([^{}]*)\}"))
            {
                string selector = rule.Groups[1].Value.Trim();
                if (!rules.TryGetValue(selector, out Dictionary<string, string>? block))
                    rules[selector] = block = new Dictionary<string, string>();

                foreach (string statement in rule.Groups[2].Value.Split(';'))
                {
                    int colon = statement.IndexOf(':');
                    if (colon <= 0) continue;
                    block[statement.Substring(0, colon).Trim()] = statement.Substring(colon + 1).Trim();
                }
            }

            return rules;
        }

        static float ParseLength(string value)
        {
            // Shorthands are only compared where every side is the same, which is how the sheet
            // writes the two that are checked (panel padding, bar padding).
            string[] parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
                Assert.That(part, Is.EqualTo(parts[0]),
                    $"'{value}' is a shorthand with different sides, which this test does not compare");

            string first = parts[0];
            if (first.EndsWith("px", StringComparison.Ordinal)) first = first.Substring(0, first.Length - 2);
            return float.Parse(first, CultureInfo.InvariantCulture);
        }

        static HudColour ParseColour(string value)
        {
            value = value.Trim();

            if (value.StartsWith("#", StringComparison.Ordinal))
            {
                string hex = value.Substring(1);
                byte Channel(int index) =>
                    byte.Parse(hex.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                float alpha = hex.Length >= 8 ? Channel(3) / 255f : 1f;
                return new HudColour(Channel(0), Channel(1), Channel(2), alpha);
            }

            Match rgba = Regex.Match(value, @"rgba?\(([^)]*)\)");
            Assert.That(rgba.Success, Is.True, $"'{value}' is not a colour this test can read");
            string[] parts = rgba.Groups[1].Value.Split(',');

            float Component(int index) =>
                float.Parse(parts[index].Trim(), CultureInfo.InvariantCulture);

            return new HudColour(
                (byte)Component(0), (byte)Component(1), (byte)Component(2),
                parts.Length > 3 ? Component(3) : 1f);
        }

        /// <summary>Equal to the resolution USS has: eight bits a channel, and an alpha written to
        /// two decimal places.</summary>
        static bool Same(HudColour a, HudColour b) =>
            a.R == b.R && a.G == b.G && a.B == b.B && Math.Abs(a.A - b.A) < 0.006f;
    }
}
