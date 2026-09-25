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
            // The inherited ink. `color` cascades in UI Toolkit and nothing above a leaf set one,
            // so a label built without a style class fell through to the imported runtime theme's
            // dark ink and drew invisibly on a near-black panel — which is how the naming prompt's
            // two buttons and the palette's "MADE OF" came to be unreadable rather than merely
            // unstyled. Pinned because the absence of a default is not visible in a review.
            (".hud", "color", () => HudTheme.TextPrimary, "text primary"),

            (".panel", "background-color", () => HudTheme.PanelFill, "panel fill"),
            (".panel", "border-color", () => HudTheme.PanelBorder, "panel border"),
            (".card", "background-color", () => HudTheme.PanelFill, "panel fill"),
            (".card", "border-color", () => HudTheme.PanelBorder, "panel border"),
            (".commandbar", "background-color", () => HudTheme.BarFill, "bar fill"),
            (".commandbar", "border-color", () => HudTheme.PanelBorder, "panel border"),
            (".window", "background-color", () => HudTheme.PopoverFill, "popover fill"),
            // The context menu (design 33 §7a): a popover's fill, and the three inks every list
            // row already uses — its words, a disabled row's, and the reason beside one.
            (".ctxmenu", "background-color", () => HudTheme.PopoverFill, "popover fill"),
            (".ctxmenu__label", "color", () => HudTheme.TextPrimary, "text primary"),
            (".ctxmenu__row--off .ctxmenu__label", "color", () => HudTheme.TextFaint, "text faint"),
            (".ctxmenu__reason", "color", () => HudTheme.TextDim, "text dim"),
            (".ctxmenu__row--cancel", "border-top-color", () => HudTheme.Divider, "divider"),

            (".stores__name", "color", () => HudTheme.TextPrimary, "text primary"),
            (".stores__value", "color", () => HudTheme.TextPrimary, "text primary"),
            (".inspect__title", "color", () => HudTheme.TextPrimary, "text primary"),
            (".panel__label", "color", () => HudTheme.TextMeta, "text meta"),
            (".bp__mats-label", "color", () => HudTheme.TextDim, "text dim"),
            (".bp__pane-label", "color", () => HudTheme.TextDim, "text dim"),
            (".inspect__meta", "color", () => HudTheme.TextMeta, "text meta"),
            (".stores__count", "color", () => HudTheme.TextDim, "text dim"),
            (".inspect__state", "color", () => HudTheme.TextDim, "text dim"),
            (".inspect__pace", "color", () => HudTheme.TextDim, "text dim"),
            (".inspect__rowname", "color", () => HudTheme.TextDim, "text dim"),
            (".inspect__rowvalue", "color", () => HudTheme.TextMeta, "text meta"),
            (".cmd__key", "color", () => HudTheme.TextFaint, "text faint"),
            (".menu__key", "color", () => HudTheme.TextFaint, "text faint"),

            (".card--sel", "border-color", () => HudTheme.Accent, "accent"),
            (".cmd--primary.cmd--on", "background-color", () => HudTheme.Accent, "accent"),
            (".speed__btn--on", "background-color", () => HudTheme.Accent, "accent"),
            (".rail__cell--active", "background-color", () => HudTheme.Accent, "accent"),
            (".cmd--primary", "border-color", () => HudTheme.Accent, "accent"),
            (".cmd__label--primary", "color", () => HudTheme.Accent, "accent"),
            (".cmd--primary.cmd--on .cmd__label--primary", "color", () => HudTheme.OnAccent, "on-accent ink"),
            (".rail__number", "color", () => HudTheme.OnAccent, "on-accent ink"),

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
            // The settings window (design 39): its own tokens, every one a theme colour.
            (".sw", "background-color", () => HudTheme.PanelFill, "panel fill"),
            (".sw", "border-color", () => HudTheme.PanelBorder, "panel border"),
            (".sw__scrim", "background-color", () => HudTheme.SettingsScrim, "settings scrim"),
            (".sw__row", "border-bottom-color", () => HudTheme.RowRule, "row rule"),
            (".sw__heading-rule", "background-color", () => HudTheme.PanelBorder, "panel border"),
            (".sw__game-rule", "background-color", () => HudTheme.RowRule, "row rule"),
            (".sw__seg", "border-color", () => HudTheme.ControlBorder, "control border"),
            (".sw__seg--on", "background-color", () => HudTheme.Accent, "accent"),
            (".sw__seg--on", "color", () => HudTheme.OnAccent, "on-accent ink"),
            (".sw__track", "background-color", () => HudTheme.SwitchOffTrack, "switch off track"),
            (".sw__track", "border-color", () => HudTheme.ControlBorder, "control border"),
            (".sw__knob", "background-color", () => HudTheme.TextMeta, "text meta"),
            (".sw__switch-word", "color", () => HudTheme.TextDim, "text dim"),
            (".sw__switch--on .sw__track", "background-color", () => HudTheme.Accent, "accent"),
            (".sw__switch--on .sw__knob", "background-color", () => HudTheme.OnAccent, "on-accent ink"),
            (".sw__chip", "border-color", () => HudTheme.ControlBorder, "control border"),
            (".sw__chip--listening", "border-color", () => HudTheme.Accent, "accent"),
            (".sw__subtitle", "color", () => HudTheme.TextMeta, "text meta"),
            (".sw__note", "color", () => HudTheme.TextMeta, "text meta"),
            (".sw__game-label", "color", () => HudTheme.TextDim, "text dim"),
            (".sw__ring", "border-color", () => HudTheme.TextPrimary, "text primary"),
            (".sw__fill", "background-color", () => HudTheme.Accent, "accent"),
            (".settings__fader .unity-base-slider__dragger", "background-color", () => HudTheme.TextPrimary, "text primary"),
            (".row--armed", "background-color", () => HudTheme.ActiveTabFill, "active tab fill"),
            (".row--armed .settings__label", "color", () => HudTheme.Accent, "accent"),

            // B18, the start screen. The scrim is the only colour this screen introduces; every
            // other line here is the screen proving it introduced none.
            (".modal-scrim", "background-color", () => HudTheme.ModalScrim, "modal scrim"),
            (".panel.startscreen", "background-color", () => HudTheme.DockFill, "the dock's fill"),
            (".panel.startscreen", "border-right-color", () => HudTheme.PanelBorder, "panel border"),
            (".title__divider", "background-color", () => HudTheme.PanelBorder, "panel border"),
            (".title__wordmark", "color", () => HudTheme.TextPrimary, "text primary"),
            (".title__name", "color", () => HudTheme.TextPrimary, "text primary"),
            (".title__name--ink", "color", () => HudTheme.Bad, "bad"),
            (".title__desc", "color", () => HudTheme.TextMeta, "text meta"),
            (".title__cap", "color", () => HudTheme.TextFaint, "text faint"),
            (".title__ring", "border-color", () => HudTheme.TextPrimary, "text primary"),
            (".title__btn--good > .title__edge", "background-color", () => HudTheme.Good, "good"),
            (".title__btn--info > .title__edge", "background-color", () => HudTheme.Info, "info"),
            (".title__btn--violet > .title__edge", "background-color", () => HudTheme.Violet, "violet"),
            (".title__btn--bad > .title__edge", "background-color", () => HudTheme.Bad, "bad"),
            (".title__btn--good:active", "background-color", () => HudTheme.Good.WithAlpha(TitleLayout.PressedFill), "good, pressed"),
            (".title__btn--bad:active", "background-color", () => HudTheme.Bad.WithAlpha(TitleLayout.PressedFill), "bad, pressed"),
            (".save__name", "color", () => HudTheme.TextPrimary, "text primary"),
            (".save__meta", "color", () => HudTheme.TextMeta, "text meta"),
            (".startscreen__back", "border-top-color", () => HudTheme.Divider, "divider"),
            (".save--bad .save__meta", "color", () => HudTheme.Warn, "warn"),

            // The New game screen (U39), which introduces no colour of its own either.
            (".startscreen__seedcap", "color", () => HudTheme.TextMeta, "text meta"),

            // The colonist screen (U40). Same two inks as a save row, for the same reason: a name
            // over a line that tells it apart from the one beside it.
            (".colonist__name", "color", () => HudTheme.TextPrimary, "text primary"),
            (".colonist__trade", "color", () => HudTheme.TextMeta, "text meta"),
            (".setup__heading", "color", () => HudTheme.TextPrimary, "text primary"),

            // Every clickable row on the setup page is outlined, in the border the panels already
            // use, so nothing new is introduced to say "this can be pressed".
            (".setup .settings__row", "border-color", () => HudTheme.PanelBorder, "panel border"),

            // The naming prompt, and the project's first text field.
            (".field .unity-base-text-field__input", "color", () => HudTheme.TextPrimary, "text primary"),
            (".field .unity-base-text-field__input", "border-color", () => HudTheme.PanelBorder, "panel border"),
            (".field:focus .unity-base-text-field__input", "border-color", () => HudTheme.Accent, "accent"),
            (".prompt__answer", "border-color", () => HudTheme.PanelBorder, "panel border"),
            (".prompt__answer:hover", "border-color", () => HudTheme.Accent, "accent"),
            (".prompt__answer--armed", "background-color", () => HudTheme.ActiveTabFill, "active tab fill"),
            (".prompt__answer--armed", "color", () => HudTheme.Accent, "accent"),
            (".prompt__note", "color", () => HudTheme.TextDim, "text dim"),
            (".prompt__note--warn", "color", () => HudTheme.Warn, "warn"),

            // The way in. The only green row in the game, so that Back and Start — which read
            // identically otherwise — cannot be confused for one another (owner, 2026-09-18).
            (".setup .setup__commit", "background-color", () => HudTheme.Good.WithAlpha(0.15f), "good, filled"),
            (".setup .setup__commit", "border-color", () => HudTheme.Good.WithAlpha(0.45f), "good, outlined"),
            (".setup .setup__commit .settings__label", "color", () => HudTheme.Good, "good"),
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

            // The settings window (design 39): every number is SettingsLayout's.
            (".sw", "width", () => SettingsLayout.Width, "the settings window"),
            (".sw", "height", () => SettingsLayout.Height, "the settings window"),
            (".sw__header", "height", () => SettingsLayout.HeaderHeight, "the window header"),
            (".sw__rail", "width", () => SettingsLayout.RailWidth, "the rail"),
            (".sw__tab", "height", () => SettingsLayout.TabRowHeight, "a tab row"),
            (".sw__action", "height", () => SettingsLayout.ActionRowHeight, "an action row"),
            (".sw__titleband", "height", () => SettingsLayout.TitleHeight, "the title band"),
            (".sw__footer", "height", () => SettingsLayout.FooterHeight, "the footer"),
            (".sw__column", "margin-right", () => SettingsLayout.ColumnGap, "between columns"),
            (".sw__section", "margin-bottom", () => SettingsLayout.SectionGap, "between sections"),
            (".sw__heading", "height", () => SettingsLayout.HeadingHeight, "a section heading"),
            (".sw__heading", "margin-bottom", () => SettingsLayout.HeadingGap, "heading to row"),
            (".sw__row", "height", () => SettingsLayout.RowHeight, "a setting row"),
            (".sw__seg", "height", () => SettingsLayout.SegmentHeight, "a segment"),
            (".sw__track", "width", () => SettingsLayout.SwitchWidth, "a switch"),
            (".sw__track", "height", () => SettingsLayout.SwitchHeight, "a switch"),
            (".sw__knob", "width", () => SettingsLayout.SwitchKnob, "a switch knob"),
            (".sw__switch-word", "width", () => SettingsLayout.SwitchWordWidth, "a switch word"),
            (".settings__fader", "width", () => SettingsLayout.SliderWidth, "a slider"),
            (".settings__fader .unity-base-slider__tracker", "height", () => SettingsLayout.SliderTrack, "a slider track"),
            (".settings__fader .unity-base-slider__dragger", "width", () => SettingsLayout.SliderThumbWidth, "a slider thumb"),
            (".settings__fader .unity-base-slider__dragger", "height", () => SettingsLayout.SliderThumbHeight, "a slider thumb"),
            (".settings__value", "width", () => SettingsLayout.SliderValueWidth, "a slider readout"),
            (".sw__select", "width", () => SettingsLayout.SelectWidth, "a select"),
            (".sw__select", "height", () => SettingsLayout.SelectHeight, "a select"),
            (".sw__chip", "min-width", () => SettingsLayout.KeyChipMinWidth, "a key chip"),
            (".sw__chip", "height", () => SettingsLayout.KeyChipHeight, "a key chip"),
            (".sw__chip", "margin-left", () => SettingsLayout.KeyChipGap, "between key chips"),
            (".sw__close", "width", () => SettingsLayout.CloseButton, "the close button"),
            ("#leaveprompt", "width", () => SettingsLayout.ConfirmWidth, "the leave prompt"),

            // The right-hand gutter, and the two panels stacked in it. The anchor is the
            // gutter's, not the rail's, since 2026-09-17: the rail and the orders strip are in a
            // column so that the strip sits under a rail whose height the world decides.
            (".column-edge", "right", () => HudLayout.Edge, "screen edge margin"),
            (".column-edge", "top", () => HudLayout.Edge, "screen edge margin"),
            (".column-edge", "width", () => HudLayout.RailWidth, "rail width"),
            (".rail", "padding-left", () => HudLayout.RailSidePad, "rail side padding"),
            (".rail__cell", "width", () => HudLayout.RailCellWidth, "rail cell"),
            (".rail__cell", "height", () => HudLayout.RailCellHeight, "rail cell"),
            (".rail__cell--surface", "height", () => HudLayout.RailSurfaceCellHeight, "rail surface cell"),
            (".rail__cell", "margin-bottom", () => HudLayout.RailCellGap, "rail cell gap"),
            (".rail__walls", "width", () => HudLayout.RailCellWidth, "the walls switch is a cell wide"),
            (".rail__walls", "height", () => HudLayout.RailToggle, "the walls switch"),
            (".rail__walls", "margin-top", () => HudLayout.RailToggleGap, "last cell to walls switch"),

            (".orders", "margin-top", () => HudLayout.RailToOrders, "rail to orders strip"),
            (".orders", "padding-top", () => HudLayout.OrdersPadTop, "orders strip top padding"),
            (".orders", "padding-left", () => HudLayout.OrdersSidePad, "orders strip side padding"),
            (".ord__btn", "width", () => HudLayout.OrderButton, "an order button"),
            (".ord__btn", "height", () => HudLayout.OrderButton, "an order button"),
            (".ord__btn", "margin-bottom", () => HudLayout.OrderGap, "gap between orders"),

            (".column-right", "right", () => HudLayout.Edge + HudLayout.RailWidth + HudLayout.RailToClock,
                "the rail's width plus its gap, measured from the right edge"),
            (".column-right", "top", () => HudLayout.Edge, "screen edge margin"),
            (".column-right", "width", () => HudLayout.ClockWidth, "clock width"),
            (".alerts", "margin-top", () => HudLayout.Gap, "gap between panels"),
            (".clock__line", "height", () => HudLayout.ClockRow, "clock row"),
            (".speed", "height", () => HudLayout.SpeedButton, "speed button"),
            (".speed", "margin-top", () => HudLayout.ClockGap, "clock to speed"),
            (".speed__btn", "height", () => HudLayout.SpeedButton, "speed button"),

            // The Build palette, in three layouts. Rows and Bar take their left and right from
            // code, because they span the screen and their bottom depends on what is docked
            // under them, so what is checkable here is every fixed box in all three.
            (".bp--rows", "width", () => HudLayout.BuildRowsWidth, "rows layout width"),
            (".bp--rail", "width", () => HudLayout.BuildRailWidth, "rail layout width"),
            (".bp__rail", "width", () => HudLayout.BuildRailColumn, "rail category column"),
            (".bp__rail-row", "height", () => HudLayout.BuildRailRow, "a rail category row"),
            (".bp__rail-row", "border-left-width", () => HudLayout.BuildRailMark, "selected rail mark"),
            (".bp__sub-tile", "height", () => HudLayout.BuildRailSubTile, "a rail sub-type tile"),
            (".bp__sub-grid", "height", () => HudLayout.BuildRailSubGrid, "rail sub-type grid"),
            (".bp__mat-grid", "height", () => HudLayout.BuildRailMatGrid, "rail material grid"),
            (".bp__mat-tile", "height", () => HudLayout.BuildRailMatTile, "a rail material tile"),
            (".bp__cat", "height", () => HudLayout.BuildCatTile, "a category tile"),
            (".bp__sub", "height", () => HudLayout.BuildSubRow, "a sub-type button"),
            (".bp--rows .bp__subs", "height", () => HudLayout.BuildRowsSubBand, "rows sub-type band"),
            (".bp--rows .bp__mats-row", "height", () => HudLayout.BuildMatRow, "rows material row"),
            (".bp__mat", "height", () => HudLayout.BuildMatRow, "a material button"),
            (".bp__bar-cat", "width", () => HudLayout.BuildBarCat, "a bar category tile"),
            (".bp__bar-sub", "width", () => HudLayout.BuildBarSub, "a bar sub-type tile"),
            (".bp__action", "width", () => HudLayout.BuildAction, "the header's way out"),
            (".bp__switch-btn", "width", () => HudLayout.BuildSwitchButton, "a switcher button"),
            (".bp__material", "border-width", () => HudTheme.MaterialBorderWidth, "material border"),
            (".bp__material", "border-radius", () => HudTheme.MaterialRadius, "material radius"),

            // "The same value on every labelled button, no exceptions" (specification). Four
            // selectors, one number, and a fifth that drifted would fail here rather than read as
            // a tile that looks very slightly wrong.
            (".bp__tile-label", "margin-left", () => HudLayout.BuildIconGap, "icon to label"),
            (".bp__material-label", "margin-left", () => HudLayout.BuildIconGap, "icon to label"),
            (".bp__crumb", "margin-left", () => HudLayout.BuildIconGap, "icon to label"),
            (".bp__mode-icon", "margin-left", () => HudLayout.BuildIconGap, "icon to label"),

            (".inspect", "left", () => HudLayout.Edge, "screen edge margin"),
            (".inspect", "bottom", () => HudLayout.InspectBottom, "inspect bottom offset"),
            (".inspect", "width", () => HudLayout.InspectWidth, "inspect width"),
            (".inspect--narrow", "width", () => HudLayout.InspectNarrowWidth, "tile readout width"),
            (".inspect__hdr", "height", () => HudLayout.InspectHeader, "inspect header"),
            (".inspect__nameline", "height", () => HudLayout.InspectNameLine, "the header's name line"),
            (".inspect__state", "height", () => HudLayout.InspectTextLine, "the header's activity line"),
            (".inspect__pace", "height", () => HudLayout.InspectTextLine, "the header's pace line"),
            (".inspect__tabs", "height", () => HudLayout.InspectTabs, "inspect tab strip"),
            (".inspect__tabs", "margin-top", () => HudLayout.InspectHeaderGap, "header to tabs"),
            (".inspect__tabbody", "margin-top", () => HudLayout.InspectTabGap, "tabs to body"),
            (".inspect__tabbody", "height", () => HudLayout.InspectTabBody, "the fixed tab body"),
            (".need", "margin-bottom", () => HudLayout.NeedRowGap, "need row gap"),
            (".skill", "height", () => HudLayout.SkillRow, "a skill line"),
            (".skill", "margin-bottom", () => HudLayout.SkillRowGap, "skill row gap"),
            (".inspect__row", "height", () => HudLayout.CellRow, "a tile fact row"),
            (".inspect__rowname", "width", () => HudLayout.CellRowName, "tile fact label column"),

            (".alert", "min-height", () => HudLayout.AlertHeight, "an alert row"),
            (".alerts__rows", "margin-top", () => HudLayout.HeaderGap, "header to first alert"),
            (".bulletin", "min-height", () => HudLayout.BulletinHeight, "an event row"),
            (".bulletin", "margin-bottom", () => HudLayout.BulletinGap, "gap between events"),
            (".bulletins__rows", "margin-top", () => HudLayout.HeaderGap, "header to first event"),

            (".strip", "top", () => HudLayout.StripTop, "the strip docked to the top"),
            (".card", "width", () => HudLayout.CardWidth, "card width"),
            (".card", "height", () => HudLayout.CardHeight, "card height"),
            (".card__avatar-box", "width", () => HudLayout.CardAvatar, "card avatar box width"),
            (".card__avatar", "width", () => HudLayout.CardAvatar, "card avatar"),
            (".card__badge", "width", () => HudLayout.CardJobRow, "card badge width"),
            (".card__badge", "height", () => HudLayout.CardJobRow, "card badge height"),
            (".card", "padding", () => HudLayout.CardPad, "card padding"),
            (".card__name", "margin-top", () => HudLayout.CardNameGap, "portrait to name"),
            (".card__name", "height", () => HudLayout.CardNameRow, "the name's line"),
            (".card__health", "margin-top", () => HudLayout.CardHealthGap, "name to health bar"),
            (".card__health", "height", () => HudLayout.CardHealthBar, "the card's health bar"),

            (".bar-row", "bottom", () => HudLayout.BarBottom, "the command bar docked to the bottom"),
            (".commandbar", "padding", () => HudCommands.BarPad, "command bar padding"),
            (".cmd", "height", () => HudCommands.ItemHeight, "command item height"),
            (".cmd", "margin-right", () => HudCommands.ItemGap, "command item gap"),
            (".commandbar__divider", "height", () => HudCommands.DividerHeight, "divider height"),
            (".commandbar__divider", "width", () => HudCommands.DividerWidth, "divider width"),

            // B18, the start screen.
            // The title screen's dock (design 40): every number is TitleLayout's.
            (".panel.startscreen", "width", () => TitleLayout.DockWidth, "the dock"),
            (".panel.startscreen", "border-right-width", () => HudTheme.BorderWidth, "the dock's edge"),
            (".title__mark", "width", () => TitleLayout.MarkWidth, "the mark"),
            (".title__mark", "height", () => TitleLayout.MarkHeight, "the mark"),
            (".title__mark", "margin-right", () => TitleLayout.MarkGap, "mark to wordmark"),
            (".title__divider", "margin-top", () => TitleLayout.DividerAbove, "above the divider"),
            (".title__divider", "margin-bottom", () => TitleLayout.DividerBelow, "below the divider"),
            (".title__btn", "height", () => TitleLayout.ButtonHeight, "a title button"),
            (".title__btn", "margin-bottom", () => TitleLayout.ButtonGap, "between title buttons"),
            (".title__words", "margin-left", () => TitleLayout.IconTextGap, "icon to name"),
            (".title__desc", "margin-top", () => TitleLayout.DescriptionGap, "name to description"),
            (".title__name", "height", () => TitleLayout.NameLine, "a button's name line"),
            (".title__desc", "height", () => TitleLayout.DescriptionLine, "a button's description line"),
            (".title__edge", "width", () => TitleLayout.LitEdge, "the lit edge"),
            (".title__footer", "margin-bottom", () => TitleLayout.FooterBottom, "the footer"),
            (".save", "height", () => HudLayout.StartSaveRow, "a save row"),
            (".save", "margin-bottom", () => HudLayout.StartSaveGap, "save row gap"),
            (".startscreen__back", "margin-top", () => HudLayout.StartRowGap, "start screen row gap"),
            (".startscreen__seedcap", "height", () => HudLayout.StartSeedCaption, "the seed's caption"),
            (".startscreen__seedrows", "margin-top", () => HudLayout.Gap, "the box to the rows under it"),
            (".colonists", "width", () => HudLayout.ColonistColumnWidth, "the candidate column"),
            (".colonist", "height", () => HudLayout.ColonistCard, "a candidate's card"),
            (".colonist", "margin-bottom", () => HudLayout.ColonistCardGap, "card gap"),
            (".colonist", "padding", () => HudLayout.ColonistCardPad, "card padding"),
            (".colonist__name", "height", () => HudLayout.ColonistNameLine, "the name line"),
            (".colonist__trade", "height", () => HudLayout.ColonistTradeLine, "the trade line"),
            (".setup__heading", "height", () => HudLayout.SetupHeading, "a section heading"),
            (".setup__heading", "margin-top", () => HudLayout.SetupHeadingGap, "above a heading"),

            // The setup page's own skills grid: two columns, set clear of the record above it.
            (".skills--setup", "max-width", () => HudLayout.SetupSkillsWidth, "two columns of skills"),
            (".skills--setup", "margin-top", () => HudLayout.SetupSkillsGap, "record to skills"),
            (".skill--setup", "width", () => HudLayout.SetupSkillRowWidth, "a setup skill line"),
            (".skill--setup", "height", () => HudLayout.SetupSkillRow, "a setup skill line"),
            (".skill", "width", () => HudLayout.SkillRowWidth, "a skill line"),
            (".skill", "margin-right", () => HudLayout.SkillColumnGap, "skill column gap"),
            (".colonist__lines", "margin-left", () => HudLayout.ColonistAvatarGap, "face to name"),
            (".detail__lines", "margin-left", () => HudLayout.DetailAvatarGap, "portrait to record"),

            (".prompt", "width", () => HudLayout.PromptWidth, "naming prompt width"),
            (".field", "height", () => HudLayout.FieldHeight, "a text field"),
            (".prompt__answer", "height", () => HudLayout.RowHeight, "list row"),

            // The row the start screen reuses rather than reinventing. Pinned in both places it is
            // already used, so "the start screen is built from rows the interface already has"
            // fails here the moment one of the three drifts.
            (".settings__row", "height", () => HudLayout.StartRow, "list row"),
            (".menu__row", "height", () => HudLayout.StartRow, "list row"),

            // The armed banner: a thick border in the held order's colour, one gap above the
            // command bar. Both are the owner's, 2026-09-17, and both are numbers the sheet could
            // otherwise drift from — the colour is written from code and the border width is not.
            (".armed", "border-width", () => HudTheme.ArmedBorderWidth, "armed banner border"),
            (".armed", "bottom", () => HudLayout.ArmedBottom, "armed banner to command bar"),

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
