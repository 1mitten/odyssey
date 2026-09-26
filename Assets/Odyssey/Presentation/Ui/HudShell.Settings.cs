#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// <see cref="HudShell"/>: B17, the settings window (design 39).
    ///
    /// <para><b>One fixed frame.</b> 1,240 by 720, centred by fixed offsets, whichever tab is
    /// open: a header, a rail of tabs with the game's actions pinned under them, and a content
    /// pane of a title band, equal columns and a footer. It used to size itself to the open tab
    /// and repeat Save, Load and the two ways out under every one, so every tab moved the box
    /// and the rows in it. Nothing a tab holds may change the frame now; a short tab leaves
    /// space. The arithmetic is <see cref="SettingsLayout"/>'s, where the fast tier holds it.</para>
    ///
    /// <para><b>It is a modal now.</b> A scrim sits behind it and the board shows through. It
    /// was deliberately not one while it was a narrow panel to watch the board through; the
    /// approved design puts a full-screen wash behind a window that covers most of the screen,
    /// and a window that large over a clickable board is a misclick waiting to happen.</para>
    ///
    /// <para><b>Every mark is drawn</b> — icons, the close X, the select's triangle, the dashed
    /// empty key slot — and every string is ASCII, so nothing on it depends on a font carrying
    /// a character (P13).</para>
    /// </summary>
    public partial class HudShell
    {
        VisualElement _settingsPanel = null!;
        VisualElement _settingsScrim = null!;
        VisualElement _settingsRail = null!;
        VisualElement _settingsFocusRing = null!;

        readonly Dictionary<SettingsTab, TabRowView> _settingTabs = new();
        readonly Dictionary<SettingsTab, VisualElement> _settingPages = new();
        readonly Dictionary<SettingsTab, PathGlyph> _titleGlyphs = new();
        readonly Dictionary<SettingsTab, string> _resetLabels = new();
        VisualElement _titleTile = null!;
        Label _titleName = null!;
        Label _titleSubtitle = null!;
        Label _resetLabel = null!;
        VisualElement _resetButton = null!;
        Label _keysHint = null!;

        /// <summary>What Enter and Space do on each focusable control that is ours. The native
        /// controls — the fader and the resolution select — answer the keyboard themselves.</summary>
        readonly Dictionary<VisualElement, Action> _activate = new();

        readonly Dictionary<int, Label> _autosaveRungs = new();
        readonly Dictionary<int, Label> _scaleRungs = new();
        readonly Dictionary<int, Label> _cameraRungs = new();
        readonly Dictionary<BuildPaletteLayout, Label> _layoutRungs = new();
        readonly Dictionary<SelectionStyle, Label> _selectionRungs = new();
        readonly Dictionary<bool, Label> _wakeRungs = new();
        readonly Dictionary<GraphicsOption, SwitchView> _settingRows = new();

        /// <summary>One rank of segments per number ladder, so a value that moves lights its own
        /// segment and nothing else is touched.</summary>
        readonly Dictionary<GraphicsLadder, Dictionary<int, Label>> _ladderRungs = new();

        /// <summary>Each ladder's row, kept so the frame cap can be greyed behind VSync and the
        /// display mode in the editor.</summary>
        readonly Dictionary<GraphicsLadder, RowView> _ladderViews = new();

        /// <summary>The quality row's segments, one per preset and Custom, lit by whichever the
        /// levers are on (<c>SettingsDirector.Preset</c>, design 38 §9).</summary>
        readonly Dictionary<QualityPreset, Label> _presetRungs = new();

        DropdownField? _resolutionDropdown;
        RowView? _resolutionRow;

        /// <summary>Where the resolution row goes once the machine's sizes are known. Empty until
        /// then, and an empty slot takes no height.</summary>
        VisualElement _resolutionSlot = null!;

        readonly Dictionary<SettingsBus, FaderView> _busFaders = new();
        readonly Dictionary<HotkeyAction, KeyRowView> _keyRows = new();

        /// <summary>The key chip a keyboard player opened, to be handed focus back when the
        /// rebind ends. A chip opened with the mouse is not refocused.</summary>
        VisualElement? _refocusChip;

        /// <summary>True between a pointer press on the window and the frame after it is
        /// released: focus that arrives then came from the mouse, and is not ringed.</summary>
        bool _settingsPointer;

        sealed class TabRowView
        {
            public VisualElement Row = null!;
            public VisualElement Bar = null!;
            public Label Label = null!;
        }

        /// <summary>A setting row: the row itself, and the note beside its label that a greyed row
        /// uses to say why.</summary>
        sealed class RowView
        {
            public VisualElement Row = null!;
            public Label Note = null!;
            public VisualElement Control = null!;

            public void SetLive(bool live, string? why)
            {
                Row.EnableInClassList("sw__row--off", !live);
                Control.SetEnabled(live);
                Note.text = live ? string.Empty : why ?? string.Empty;
                Note.style.display = live || string.IsNullOrEmpty(why) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        sealed class SwitchView
        {
            public VisualElement Control = null!;
            public Label Word = null!;
        }

        /// <summary>One volume row: its fader, its fill and the readout beside it, refreshed
        /// when the bus's value moves and never per frame.</summary>
        sealed class FaderView
        {
            public Slider Fader = null!;
            public VisualElement Fill = null!;
            public Label Value = null!;
        }

        /// <summary>One binding row: the chips of its two slots and the key written in each, for
        /// event-driven refresh.</summary>
        sealed class KeyRowView
        {
            public readonly VisualElement[] Caps = new VisualElement[HotkeyDirector.SlotCount];
            public readonly Label[] Keys = new Label[HotkeyDirector.SlotCount];
            public readonly DashedOutline[] Empty = new DashedOutline[HotkeyDirector.SlotCount];
        }

        static Color Ink(HudColour colour) => HudTokens.Convert(colour);

        // ============================================================ the frame

        void BuildSettings()
        {
            // The wash, under the window and over everything else. Pickable, which is the whole
            // of how a modal swallows the pointer here (see Modal()).
            _settingsScrim = new VisualElement { name = "settings-scrim" };
            _settingsScrim.AddToClassList("sw__scrim");
            _settingsScrim.style.display = DisplayStyle.None;
            _hud.Add(_settingsScrim);

            _settingsPanel = new VisualElement { name = "settings" };
            _settingsPanel.AddToClassList("sw");
            _settingsPanel.AddToClassList("region");   // the smoke test's name for a framed region
            _settingsPanel.style.display = DisplayStyle.None;

            _settingsPanel.Add(BuildSettingsHeader());

            var body = new VisualElement();
            body.AddToClassList("sw__body");
            body.Add(BuildSettingsRail());

            var content = new VisualElement();
            content.AddToClassList("sw__content");
            content.Add(BuildSettingsTitle());

            var pages = new VisualElement();
            pages.AddToClassList("sw__pages");
            BuildInterfaceSection(pages);
            BuildGraphicsSection(pages);
            BuildAudioSection(pages);
            BuildKeysSection(pages);
            BuildGameplaySection(pages);
            content.Add(pages);

            content.Add(BuildSettingsFooter());
            body.Add(content);
            _settingsPanel.Add(body);

            // Last child, so it draws over every control it may ring.
            _settingsFocusRing = new VisualElement { pickingMode = PickingMode.Ignore };
            _settingsFocusRing.AddToClassList("sw__ring");
            _settingsFocusRing.style.display = DisplayStyle.None;
            _settingsPanel.Add(_settingsFocusRing);

            WireSettingsKeyboard();

            // The director opens on Interface, and the shell may never attach to a director at
            // all in a harness that builds no world.
            OnSettingsTabChanged(SettingsTab.Interface);
            _hud.Add(_settingsPanel);
        }

        VisualElement BuildSettingsHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("sw__header");

            header.Add(new PathGlyph(SettingsLayout.GearIcon, 18f, Ink(HudTheme.Accent)));

            Label title = HudText.Make(Registry.Label(SettingsDirector.PanelKey), HudTextRole.PanelLabel,
                ussClass: "sw__title");
            title.style.fontSize = 13;
            title.style.letterSpacing = 13f * 0.16f;
            header.Add(title);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);

            var close = new VisualElement { tooltip = "Close" };
            close.AddToClassList("sw__close");
            close.Add(new PathGlyph(SettingsLayout.CloseIcon, 11f, Ink(HudTheme.TextMeta)));
            close.RegisterCallback<ClickEvent>(_ => CloseSettings());
            KeyStop(close, CloseSettings);
            header.Add(close);
            return header;
        }

        VisualElement BuildSettingsRail()
        {
            _settingsRail = new VisualElement();
            _settingsRail.AddToClassList("sw__rail");

            foreach (SettingsTab tab in SettingsLayout.Tabs)
            {
                HudColour hue = SettingsLayout.Hue(tab);
                var row = new VisualElement();
                row.AddToClassList("sw__tab");

                var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                bar.AddToClassList("sw__tab-bar");
                bar.style.backgroundColor = Ink(hue);
                row.Add(bar);

                row.Add(new PathGlyph(SettingsLayout.IconOf(tab), SettingsLayout.TabIconSize, Ink(hue)));
                Label label = HudText.Make(Registry.Label(SettingsDirector.TabKey(tab)), HudTextRole.Row,
                    ussClass: "sw__tab-label");
                row.Add(label);

                SettingsTab captured = tab;
                row.RegisterCallback<ClickEvent>(_ => _directors?.Settings.SetTab(captured));
                KeyStop(row, () => _directors?.Settings.SetTab(captured));
                _settingTabs[tab] = new TabRowView { Row = row, Bar = bar, Label = label };
                _settingsRail.Add(row);

                _resetLabels[tab] = SettingsLayout.ResetLabel(Registry.Label(SettingsDirector.TabKey(tab)));
            }

            // The game's actions, once, pinned to the foot of the rail. They were repeated under
            // every tab, which was most of why the window changed height.
            var game = new VisualElement();
            game.AddToClassList("sw__game");
            game.Add(HudText.Make(Registry.Label(SettingsLayout.GameGroupKey), HudTextRole.PanelLabel,
                ussClass: "sw__game-label"));

            foreach (SessionCommand command in SessionCommands.For(SessionContext.InGame))
            {
                if (SettingsLayout.RuleBefore(command.Key))
                {
                    var rule = new VisualElement();
                    rule.AddToClassList("sw__game-rule");
                    game.Add(rule);
                }
                game.Add(SessionRow(command));
            }

            _settingsRail.Add(game);
            return _settingsRail;
        }

        VisualElement BuildSettingsTitle()
        {
            var band = new VisualElement();
            band.AddToClassList("sw__titleband");

            _titleTile = new VisualElement();
            _titleTile.AddToClassList("sw__tile");
            foreach (SettingsTab tab in SettingsLayout.Tabs)
            {
                var glyph = new PathGlyph(SettingsLayout.IconOf(tab), 20f, Ink(SettingsLayout.Hue(tab)));
                _titleGlyphs[tab] = glyph;
                _titleTile.Add(glyph);
            }
            band.Add(_titleTile);

            var words = new VisualElement();
            words.AddToClassList("sw__titlewords");
            _titleName = HudText.Make(string.Empty, HudTextRole.Name, ussClass: "sw__pagename");
            _titleSubtitle = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "sw__subtitle");
            words.Add(_titleName);
            words.Add(_titleSubtitle);
            band.Add(words);
            return band;
        }

        VisualElement BuildSettingsFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("sw__footer");

            var reset = new VisualElement();
            reset.AddToClassList("sw__button");
            reset.Add(new PathGlyph(SettingsLayout.ResetIcon, 14f, Ink(HudTheme.TextMeta)));
            _resetLabel = HudText.Make(string.Empty, HudTextRole.Row, ussClass: "sw__button-label");
            reset.Add(_resetLabel);
            reset.RegisterCallback<ClickEvent>(_ => ResetOpenTab());
            KeyStop(reset, ResetOpenTab);
            _resetButton = reset;
            footer.Add(reset);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            footer.Add(spacer);

            _keysHint = HudText.Make(SettingsLayout.KeysHint, HudTextRole.Meta, ussClass: "sw__hint");
            footer.Add(_keysHint);
            return footer;
        }

        /// <summary>The footer's one button: the open tab back to where it shipped. Keys are the
        /// hotkey director's, which has had its own reset since the tab landed.</summary>
        void ResetOpenTab()
        {
            if (_directors == null) return;
            SettingsTab tab = _directors.Settings.Tab;
            if (tab == SettingsTab.Keys) _directors.Hotkeys.ResetKeys();
            else _directors.Settings.ResetTab(tab);
        }

        void CloseSettings() => _directors?.Settings.SetOpen(false);

        // ============================================================ building blocks

        /// <summary>
        /// A page with a full-width band above its columns, for a row that belongs to both of them:
        /// the Graphics tab's Quality, which sets levers in every group (design 38 §9).
        /// </summary>
        VisualElement[] PageWithBand(VisualElement parent, SettingsTab tab, int columns, out VisualElement band)
        {
            var page = new VisualElement();
            page.AddToClassList("sw__page");
            band = new VisualElement();
            band.AddToClassList("sw__band");
            page.Add(band);

            var row = new VisualElement();
            row.AddToClassList("sw__columns");
            row.AddToClassList("sw__columns--inner");
            var cols = new VisualElement[columns];
            for (int c = 0; c < columns; c++)
            {
                cols[c] = new VisualElement();
                cols[c].AddToClassList("sw__column");
                row.Add(cols[c]);
            }
            page.Add(row);
            _settingPages[tab] = page;
            parent.Add(page);
            return cols;
        }

        /// <summary>A page: the tab's equal columns.</summary>
        VisualElement[] Page(VisualElement parent, SettingsTab tab, int columns)
        {
            var page = new VisualElement();
            page.AddToClassList("sw__columns");
            var cols = new VisualElement[columns];
            for (int c = 0; c < columns; c++)
            {
                cols[c] = new VisualElement();
                cols[c].AddToClassList("sw__column");
                page.Add(cols[c]);
            }
            _settingPages[tab] = page;
            parent.Add(page);
            return cols;
        }

        /// <summary>A section: a square in the tab's hue, the name in capitals, a rule to the
        /// column's edge, and the rows under it.</summary>
        static VisualElement Section(VisualElement column, SettingsTab tab, string headingKey)
        {
            var section = new VisualElement();
            section.AddToClassList("sw__section");

            var heading = new VisualElement();
            heading.AddToClassList("sw__heading");
            var square = new VisualElement();
            square.AddToClassList("sw__square");
            square.style.backgroundColor = Ink(SettingsLayout.Hue(tab));
            heading.Add(square);
            heading.Add(HudText.Make(Registry.Label(headingKey), HudTextRole.PanelLabel,
                ussClass: "sw__heading-label"));
            var rule = new VisualElement();
            rule.AddToClassList("sw__heading-rule");
            heading.Add(rule);

            section.Add(heading);
            column.Add(section);
            return section;
        }

        /// <summary>One setting row: the label and an optional note on the left, the control on
        /// the right.</summary>
        static RowView Row(VisualElement section, string key, VisualElement control, string? tooltip = null)
        {
            var row = new VisualElement();
            row.AddToClassList("sw__row");
            if (tooltip != null) row.tooltip = tooltip;

            var words = new VisualElement();
            words.AddToClassList("sw__words");
            words.Add(HudText.Make(Registry.Label(key), HudTextRole.Row, ussClass: "sw__label"));
            Label note = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "sw__note");
            note.style.display = DisplayStyle.None;
            words.Add(note);
            row.Add(words);

            control.AddToClassList("sw__control");
            row.Add(control);
            section.Add(row);
            return new RowView { Row = row, Note = note, Control = control };
        }

        /// <summary>Make an element a keyboard stop: Tab reaches it, the ring finds it, and
        /// Enter and Space do what a click does.</summary>
        void KeyStop(VisualElement element, Action activate)
        {
            element.focusable = true;
            element.tabIndex = 0;
            element.AddToClassList("sw__focus");
            _activate[element] = activate;
        }

        /// <summary>
        /// A segmented control: joined buttons, one lit. The window's one multiple-choice idiom,
        /// in one place.
        ///
        /// <para><b>Every segment's text is built here, once, as the row is constructed.</b> ADR
        /// 0003's flip condition F1 is that the HUD allocates nothing per frame in steady state;
        /// the change handlers only flip classes on labels that already exist.</para>
        /// </summary>
        /// <param name="numeric">Whether the segments are figures, and so set in the mono face.</param>
        VisualElement Segmented<T>(IReadOnlyList<T> values, Func<T, string> labelOf, Func<T, string> tooltipOf,
            Action<T> onPick, IDictionary<T, Label> into, bool numeric)
        {
            var group = new VisualElement();
            group.AddToClassList("sw__segs");
            foreach (T value in values)
            {
                Label seg = HudText.Make(labelOf(value), HudTextRole.Body, numeric, "sw__seg");
                seg.tooltip = tooltipOf(value);
                T captured = value;
                seg.RegisterCallback<ClickEvent>(_ => onPick(captured));
                KeyStop(seg, () => onPick(captured));
                into[value] = seg;
                group.Add(seg);
            }
            return group;
        }

        /// <summary>Light the segment that is standing and put the rest out. Also marks the
        /// segment after a lit one, whose left border is the lit one's right edge: it takes the
        /// accent, which is how the selected segment "sits above" its neighbours without a
        /// z-order UI Toolkit does not give siblings.</summary>
        static void LightRung<T>(IDictionary<T, Label> rungs, T standing)
        {
            VisualElement? group = null;
            foreach (var entry in rungs)
            {
                bool on = EqualityComparer<T>.Default.Equals(entry.Key, standing);
                entry.Value.EnableInClassList("sw__seg--on", on);
                entry.Value.style.unityFontStyleAndWeight = on ? FontStyle.Bold : FontStyle.Normal;
                group ??= entry.Value.parent;
            }
            if (group == null) return;
            bool previousOn = false;
            foreach (VisualElement child in group.Children())
            {
                child.EnableInClassList("sw__seg--after-on", previousOn);
                previousOn = child.ClassListContains("sw__seg--on");
            }
        }

        // ============================================================ Interface

        void BuildInterfaceSection(VisualElement parent)
        {
            VisualElement[] cols = Page(parent, SettingsTab.Interface, 2);

            Row(Section(cols[0], SettingsTab.Interface, SettingsLayout.ScaleGroupKey), SettingsDirector.UiScaleKey,
                Segmented(SettingsDirector.UiScales,
                    percent => percent + "%",
                    percent => percent == 100
                        ? "The size the interface is designed at"
                        : percent < 100
                            ? "Smaller type, less of the board hidden"
                            : "Larger type, more of the board hidden",
                    percent => _directors?.Settings.SetUiScale(percent),
                    _scaleRungs, numeric: true));

            VisualElement cameraGroup = Section(cols[0], SettingsTab.Interface, SettingsLayout.CameraGroupKey);
            Row(cameraGroup, SettingsDirector.CamSpeedKey,
                Segmented(SettingsDirector.CameraSpeeds,
                    // "x" rather than the multiplication sign: the window is ASCII (design 39 §2).
                    percent => (percent / 100f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "x",
                    percent => percent == 100
                        ? "The speed the camera was tuned at"
                        : percent < 100
                            ? "Slower, for fine placement"
                            : "Faster, for crossing the map",
                    percent => _directors?.Settings.SetCameraSpeed(percent),
                    _cameraRungs, numeric: true));

            // How a colony is entered (design 56 §9): waking into it, or a plain fade. Under the
            // camera because the camera settling is half of what it is.
            Row(cameraGroup, SettingsDirector.WakeUpKey,
                Segmented(SettingsDirector.WakeUpRungs,
                    on => on ? "On" : "Off",
                    on => on
                        ? "Blurred, warm and muffled, clearing over five seconds. Any key wakes you. The default"
                        : "A plain fade from black",
                    on => _directors?.Settings.SetWakeUp(on),
                    _wakeRungs, numeric: false));

            // The same control exists inside the palette's own header, and both write the one
            // preference on SettingsDirector: a switcher in a panel header is findable only by
            // someone already looking at the panel.
            Row(Section(cols[1], SettingsTab.Interface, SettingsLayout.PaletteGroupKey), SettingsDirector.BuildLayoutKey,
                Segmented(BuildPaletteModel.Layouts,
                    BuildPaletteModel.LayoutName,
                    layout => layout switch
                    {
                        BuildPaletteLayout.Rows => "Bands across the screen. The default",
                        BuildPaletteLayout.Rail => "Categories down a rail. Its height never changes",
                        BuildPaletteLayout.Bar => "Two dense rows of icons. The least of the board hidden",
                        _ => string.Empty,
                    },
                    layout => _directors?.Settings.SetBuildPaletteLayout(layout),
                    _layoutRungs, numeric: false));

            // How the selected thing is marked (design 44): lit at its own edges, or the brackets.
            Row(Section(cols[1], SettingsTab.Interface, SettingsLayout.SelectionGroupKey), SettingsDirector.SelectionStyleKey,
                Segmented(SettingsDirector.SelectionStyles,
                    style => style == SelectionStyle.Highlight ? "Highlight" : "Brackets",
                    style => style == SelectionStyle.Highlight
                        ? "A line round the thing itself, and a wash over a tile. The default"
                        : "Corner brackets round the thing's box",
                    style => _directors?.Settings.SetSelectionStyle(style),
                    _selectionRungs, numeric: false));
        }

        // ============================================================ Graphics

        /// <summary>
        /// The Graphics tab: <b>Display</b> is how the frame is paced and presented,
        /// <b>Performance</b> what it costs, <b>Detail</b> what the board is made of.
        /// </summary>
        void BuildGraphicsSection(VisualElement parent)
        {
            VisualElement[] cols = PageWithBand(parent, SettingsTab.Graphics, 2, out VisualElement band);

            // Quality, across the top over both groups, because a preset sets levers in both
            // (design 38 §9). Custom is drawn and lit like any segment but picking it does nothing:
            // it is a statement about the levers, not a thing to choose.
            Row(band, SettingsDirector.QualityKey,
                Segmented(SettingsDirector.Presets, SettingsDirector.PresetLabel, SettingsDirector.PresetTooltip,
                    preset => _directors?.Settings.ApplyPreset(preset), _presetRungs, numeric: false),
                "Sets every lever it owns at once. Moving one by hand makes it Custom");

            VisualElement display = Section(cols[0], SettingsTab.Graphics, SettingsDirector.DisplayGroupKey);
            foreach (GraphicsLadder ladder in SettingsLayout.DisplayLadders)
            {
                LadderRow(display, ladder);
                // The resolution sits under the mode it depends on.
                if (ladder == GraphicsLadder.DisplayMode)
                {
                    _resolutionSlot = new VisualElement();
                    display.Add(_resolutionSlot);
                }
            }

            VisualElement performance = Section(cols[0], SettingsTab.Graphics, SettingsLayout.PerformanceGroupKey);
            foreach (GraphicsLadder ladder in SettingsLayout.PerformanceLadders) LadderRow(performance, ladder);

            // The display mode is the editor's blind spot: the Game view is not a window the
            // game owns.
            if (Application.isEditor && _ladderViews.TryGetValue(GraphicsLadder.DisplayMode, out RowView? mode))
                mode.SetLive(false, SettingsLayout.BuiltGameOnly);

            VisualElement detail = Section(cols[1], SettingsTab.Graphics, SettingsDirector.DetailGroupKey);

            // The grass ladders lead the Detail group: how much grass, and how far out.
            foreach (GraphicsLadder ladder in SettingsDirector.DetailLadders) LadderRow(detail, ladder);
            foreach (GraphicsOption option in SettingsDirector.All)
            {
                SwitchView view = Switch();
                GraphicsOption captured = option;
                void Flip() => _directors?.Settings.Toggle(captured);

                // Said in the tooltip rather than on the row, because it is a fact about what the
                // toggle costs, not about what it does.
                RowView row = Row(detail, SettingsDirector.KeyOf(option), view.Control,
                    SettingsDirector.NeedsRedraw(option)
                        ? "Redraws the board when it changes"
                        : "Takes effect on the next frame");
                row.Row.RegisterCallback<ClickEvent>(_ => Flip());
                KeyStop(view.Control, Flip);
                _settingRows[option] = view;
            }
        }

        void LadderRow(VisualElement section, GraphicsLadder ladder)
        {
            var rungs = new Dictionary<int, Label>();
            _ladderRungs[ladder] = rungs;
            _ladderViews[ladder] = Row(section, SettingsDirector.KeyOf(ladder),
                Segmented(SettingsDirector.RungsOf(ladder),
                    rung => SettingsDirector.RungLabel(ladder, rung),
                    rung => SettingsDirector.RungTooltip(ladder, rung),
                    rung => _directors?.Settings.SetValue(ladder, rung),
                    rungs,
                    // "Off", "On", "Half", "Uncapped" and the display modes are words; a segment
                    // is set in the mono face only when it is a figure.
                    numeric: ladder != GraphicsLadder.VSync && ladder != GraphicsLadder.DisplayMode),
                RowCostOf(ladder));

            // A word among figures ("Off" beside "2x", "Uncapped" beside "144") is still a word.
            foreach (var entry in rungs)
                if (!HasDigit(entry.Value.text)) HudText.Apply(entry.Value, HudTextRole.Body);
        }

        static bool HasDigit(string text)
        {
            foreach (char c in text) if (c >= '0' && c <= '9') return true;
            return false;
        }

        /// <summary>A switch: the state said in a word, then the track. The word is required —
        /// the state must not rest on colour alone.</summary>
        static SwitchView Switch()
        {
            var control = new VisualElement();
            control.AddToClassList("sw__switch");
            Label word = HudText.Make("Off", HudTextRole.Meta, numeric: true, "sw__switch-word");
            control.Add(word);
            var track = new VisualElement();
            track.AddToClassList("sw__track");
            var knob = new VisualElement();
            knob.AddToClassList("sw__knob");
            track.Add(knob);
            control.Add(track);
            return new SwitchView { Control = control, Word = word };
        }

        /// <summary>What a row costs, said on hover rather than on the row.</summary>
        static string RowCostOf(GraphicsLadder ladder) =>
            SettingsDirector.CostsAHitch(ladder)
                ? "Rebuilds the frame buffers when it changes, once"
                : SettingsDirector.NeedsRedraw(ladder)
                    ? "Redraws the board when it changes, over a few frames"
                    : "Takes effect on the next frame";

        /// <summary>Light the preset the levers are on, or Custom. Asked whenever a lever moves,
        /// never per frame.</summary>
        void RefreshPresetRow()
        {
            if (_directors == null) return;
            LightRung(_presetRungs, _directors.Settings.Preset);
        }

        /// <summary>
        /// The resolution row: a select of the machine's own sizes, built after the director has
        /// been seeded. Live only in a built game and only in Fullscreen, and a greyed row says
        /// which of those it is waiting for.
        /// </summary>
        void BuildResolutionRow()
        {
            if (_resolutionDropdown != null) return;

            IReadOnlyList<SettingsDirector.Mode> modes =
                _directors?.Settings.Resolutions ?? Array.Empty<SettingsDirector.Mode>();
            if (modes.Count == 0 && _directors != null && _directors.Settings.Resolution.Width > 0)
                modes = new[] { _directors.Settings.Resolution };
            if (modes.Count == 0) return;

            _resolutionSlot.Clear();

            var choices = new List<string>(modes.Count);
            var modeMap = new Dictionary<string, SettingsDirector.Mode>(modes.Count);
            for (int i = 0; i < modes.Count; i++)
            {
                string text = ResolutionText(modes[i]);
                choices.Add(text);
                modeMap[text] = modes[i];
            }

            SettingsDirector.Mode current = _directors?.Settings.Resolution ?? default;
            string initial = current.Width > 0 ? ResolutionText(current) : choices[0];
            // A window the monitor does not list as a display mode (a windowed player at a size of
            // its own) is still the resolution in use: offered as a choice rather than refused,
            // because DropdownField throws on a default that is not in its list and took the
            // settings panel down with it (found by the scenery smoke test at 960 x 540, design 38
            // §22).
            if (!choices.Contains(initial))
            {
                choices.Insert(0, initial);
                modeMap[initial] = current;
            }

            var dropdown = new DropdownField(choices, initial);
            dropdown.AddToClassList("sw__select");
            if (dropdown.labelElement != null) dropdown.labelElement.style.display = DisplayStyle.None;
            var textElem = dropdown.Q<TextElement>(className: "unity-base-popup-field__text");
            if (textElem != null) HudText.Apply(textElem, HudTextRole.Body, numeric: true);

            // The engine's arrow is a texture; the window draws its own marks.
            VisualElement? input = dropdown.Q(className: "unity-base-popup-field__input");
            VisualElement? arrow = dropdown.Q(className: "unity-base-popup-field__arrow");
            if (arrow != null) arrow.style.display = DisplayStyle.None;
            input?.Add(new PathGlyph(SettingsLayout.SelectArrow, 10f, 6f, Ink(HudTheme.TextMeta), 10f, fill: true));

            dropdown.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != null && modeMap.TryGetValue(evt.newValue, out SettingsDirector.Mode picked))
                    _directors?.Settings.SetResolution(picked);
            });
            dropdown.AddToClassList("sw__focus");

            _resolutionRow = Row(_resolutionSlot, SettingsDirector.ResolutionKey, dropdown,
                "Resolution the game runs at");
            _resolutionDropdown = dropdown;
            RefreshResolutionRow();
        }

        static string ResolutionText(SettingsDirector.Mode mode) => mode.Width + " x " + mode.Height;

        /// <summary>The resolution is live only in a built game, in Fullscreen.</summary>
        void RefreshResolutionRow()
        {
            if (_resolutionRow == null) return;
            bool fullscreen = _directors != null &&
                              _directors.Settings.Value(GraphicsLadder.DisplayMode) == SettingsDirector.ExclusiveFullScreen;
            if (Application.isEditor) _resolutionRow.SetLive(false, SettingsLayout.BuiltGameOnly);
            else _resolutionRow.SetLive(fullscreen, SettingsLayout.FullscreenOnly);
        }

        // ============================================================ Audio

        /// <summary>
        /// The Audio tab: one fader per bus. A fader holds a continuum rather than a choice, with
        /// unity seated at the centre of the track (owner, 2026-09-17) and the centre marked, so
        /// "put it back the way it shipped" is a place. The built-in <see cref="Slider"/>
        /// restyled, for its drag capture, track jumps and arrow keys.
        /// </summary>
        void BuildAudioSection(VisualElement parent)
        {
            VisualElement[] cols = Page(parent, SettingsTab.Audio, 2);
            VisualElement volume = Section(cols[0], SettingsTab.Audio, SettingsLayout.VolumeGroupKey);
            VisualElement cues = Section(cols[1], SettingsTab.Audio, SettingsLayout.CuesGroupKey);

            foreach (SettingsBus bus in SettingsDirector.Buses)
            {
                VisualElement section = Array.IndexOf(SettingsLayout.CueBuses, bus) >= 0 ? cues : volume;

                var control = new VisualElement();
                control.AddToClassList("sw__slider");

                var fader = new Slider(-1f, 1f, SliderDirection.Horizontal);
                fader.AddToClassList("settings__fader");
                fader.SetValueWithoutNotify(SettingsDirector.TrackOf(SettingsDirector.UnityDb));
                fader.tooltip =
                    "Drag to set the volume. Silence at the left, unity at the centre mark, boost at the right";
                fader.AddToClassList("sw__focus");

                // The fill and the centre mark, first children of the tracker so the thumb draws
                // over both.
                VisualElement tracker = fader.Q(className: "unity-base-slider__tracker") ?? fader;
                var fill = new VisualElement { pickingMode = PickingMode.Ignore };
                fill.AddToClassList("sw__fill");
                tracker.Add(fill);
                var notch = new VisualElement { pickingMode = PickingMode.Ignore };
                notch.AddToClassList("settings__fader-notch");
                tracker.Add(notch);
                control.Add(fader);

                Label value = HudText.Make(VolumeText(SettingsDirector.UnityDb), HudTextRole.Row,
                    numeric: true, ussClass: "settings__value");
                control.Add(value);

                SettingsBus capturedBus = bus;
                fader.RegisterValueChangedCallback(evt =>
                {
                    // Whole decibels: a thumb resting between two of them is a position the
                    // readout cannot say and the store cannot keep.
                    int db = SettingsDirector.DbOf(evt.newValue);
                    if (_directors != null && _directors.Settings.BusDb(capturedBus) != db)
                        _directors.Settings.SetBusDb(capturedBus, db);
                });

                Row(section, SettingsDirector.VolumeKey(bus), control);
                _busFaders[bus] = new FaderView { Fader = fader, Fill = fill, Value = value };
            }
        }

        /// <summary>What one fader's readout says. Mute is a word because silence is not a
        /// number; a boost says its plus out loud.</summary>
        static string VolumeText(int db) =>
            db <= SettingsDirector.SilenceDb ? "Mute"
            : db == SettingsDirector.UnityDb ? "0 dB"
            : db < SettingsDirector.UnityDb ? db + " dB"
            : "+" + db + " dB";

        // ============================================================ Keys

        /// <summary>
        /// The Keys tab: every action the game reads a key for, grouped, two chips per row. Click
        /// a chip and the next key pressed is offered to that slot; Backspace empties it; Escape
        /// backs out of the wait.
        /// </summary>
        void BuildKeysSection(VisualElement parent)
        {
            VisualElement[] cols = Page(parent, SettingsTab.Keys, SettingsLayout.KeyColumns.Length);
            for (int c = 0; c < SettingsLayout.KeyColumns.Length; c++)
            {
                foreach (SettingsLayout.KeyGroup group in SettingsLayout.KeyColumns[c])
                {
                    VisualElement section = Section(cols[c], SettingsTab.Keys, group.HeadingKey);
                    foreach (HotkeyAction action in group.Actions)
                    {
                        var chips = new VisualElement();
                        chips.AddToClassList("sw__chips");
                        var view = new KeyRowView();
                        for (int slot = 0; slot < HotkeyDirector.SlotCount; slot++)
                        {
                            HotkeyAction capturedAction = action;
                            int capturedSlot = slot;
                            // A box holding the key and the dashed outline side by side, not a
                            // label with the outline inside it: a label with a child is no longer
                            // sized by its text, and every chip shrank to its 36 px floor with
                            // "Space" and "PgUp" running into its edges.
                            var chip = new VisualElement();
                            chip.AddToClassList("sw__chip");
                            Label text = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true, "sw__chip-key");
                            text.AddToClassList(HudText.KeyCapClass);
                            text.pickingMode = PickingMode.Ignore;
                            chip.Add(text);
                            var empty = new DashedOutline();
                            chip.Add(empty);

                            chip.RegisterCallback<ClickEvent>(_ =>
                                _directors?.Hotkeys.Listen(capturedAction, capturedSlot));
                            KeyStop(chip, () => ListenFromKeyboard(chip, capturedAction, capturedSlot));
                            view.Caps[slot] = chip;
                            view.Keys[slot] = text;
                            view.Empty[slot] = empty;
                            chips.Add(chip);
                        }
                        _keyRows[action] = view;
                        Row(section, HotkeyDirector.KeyOf(action), chips);
                    }
                }
            }
        }

        /// <summary>
        /// A chip opened from the keyboard. The wait starts a moment later, so the Enter or Space
        /// that opened it is not also taken as the key to bind; and the chip gives up focus while
        /// it waits, so Escape reaches the rebind and not the window.
        /// </summary>
        void ListenFromKeyboard(VisualElement chip, HotkeyAction action, int slot)
        {
            _refocusChip = chip;
            chip.Blur();
            _settingsPanel.schedule.Execute(() => _directors?.Hotkeys.Listen(action, slot)).ExecuteLater(120);
        }

        /// <summary>
        /// Redraw every chip from the binding map. Called on change only — a click, a capture, a
        /// reset — never per frame.
        /// </summary>
        void RefreshKeyCaps()
        {
            if (_directors == null) return;
            HotkeyDirector hotkeys = _directors.Hotkeys;

            foreach (HotkeyAction action in HotkeyDirector.All)
            {
                if (!_keyRows.TryGetValue(action, out KeyRowView? view)) continue;
                for (int slot = 0; slot < HotkeyDirector.SlotCount; slot++)
                {
                    HudKey key = hotkeys.Key(action, slot);
                    bool listening = hotkeys.Listening == (action, slot);
                    bool empty = key == HudKey.None && !listening;
                    VisualElement cap = view.Caps[slot];

                    view.Keys[slot].text = listening ? "..." : HotkeyDirector.Display(key);
                    cap.EnableInClassList("sw__chip--listening", listening);
                    cap.EnableInClassList("sw__chip--empty", empty);
                    view.Empty[slot].style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;

                    HudKey def = HotkeyDirector.DefaultKey(action, slot);
                    cap.tooltip = listening
                        ? "Press the key to bind. Backspace clears it, Escape cancels."
                        : def == HudKey.None
                            ? "Click, then press a key"
                            : "Click, then press a key. Default: " + HotkeyDirector.Display(def);
                }
            }

            // A chip a keyboard player opened gets focus back once the wait is over, a moment
            // later so the key that ended it is not read by the window too.
            if (_refocusChip != null && hotkeys.Listening == null && _directors.Settings.Open)
            {
                VisualElement chip = _refocusChip;
                _refocusChip = null;
                _settingsPanel.schedule.Execute(() => chip.Focus()).ExecuteLater(120);
            }

            // The orders strip names a key in every tooltip, read from the same binding map.
            RefreshOrderTooltips();
        }

        /// <summary>Say on the chip why the key it was offered did not take.</summary>
        void OnHotkeyConflict(HotkeyAction refused, HotkeyAction owner)
        {
            if (_directors == null || !_keyRows.TryGetValue(refused, out KeyRowView? view)) return;
            (HotkeyAction Action, int Slot)? listening = _directors.Hotkeys.Listening;
            if (listening == null || listening.Value.Action != refused) return;
            view.Caps[listening.Value.Slot].tooltip =
                "In use by " + Registry.Label(HotkeyDirector.KeyOf(owner));
        }

        // ============================================================ Gameplay

        /// <summary>The Gameplay tab: the autosave, as one control where Off is a rung, so
        /// "whether" and "how often" cannot disagree. <c>AutosaveClock</c> owns the rungs.</summary>
        void BuildGameplaySection(VisualElement parent)
        {
            VisualElement[] cols = Page(parent, SettingsTab.Gameplay, 2);
            Row(Section(cols[0], SettingsTab.Gameplay, SettingsLayout.SavingGroupKey), SettingsDirector.AutosaveKey,
                Segmented(AutosaveClock.DayRungs,
                    AutosaveClock.RungLabel,
                    AutosaveClock.RungTooltip,
                    days => _directors?.Settings.SetAutosaveDays(days),
                    _autosaveRungs, numeric: false),
                "Written over this colony's own save, keeping one previous copy beside it");
        }

        // ============================================================ the rail's actions

        /// <summary>
        /// One of the game's actions in the rail: Save, Save as, Load, Quit to main menu, Exit
        /// game. Built from the same table as the start screen's rows, so the two cannot drift.
        /// </summary>
        VisualElement SessionRow(SessionCommand command)
        {
            HudColour ink = SettingsLayout.Ink(SettingsLayout.ToneOf(command.Key));
            var row = new VisualElement();
            row.AddToClassList("sw__action");
            row.Add(new PathGlyph(SettingsLayout.ActionIcon(command.Key), SettingsLayout.ActionIconSize, Ink(ink)));

            Label label = HudText.Make(command.Label, HudTextRole.Row, ussClass: "sw__action-label");
            if (SettingsLayout.LabelTakesInk(command.Key)) label.style.color = Ink(ink);
            row.Add(label);
            row.tooltip = SessionTooltip(command);

            string key = command.Key;
            row.RegisterCallback<ClickEvent>(_ => _directors?.Settings.Request(key));
            KeyStop(row, () => _directors?.Settings.Request(key));

            _sessionRows[command.Key] = (row, label);
            return row;
        }

        readonly Dictionary<string, (VisualElement Row, Label Label)> _sessionRows =
            new Dictionary<string, (VisualElement, Label)>();

        /// <summary>
        /// A row that has something to say instead of its own name — today only Load, saying
        /// there is nothing to open. State the refresh knows about, because
        /// <see cref="SettingsDirector.Request"/> raises <c>RowRequested</c> and <i>then</i>
        /// <c>ExitChanged</c>, and anything written straight on to a label is overwritten by
        /// <see cref="OnExitChanged"/> one call later.
        /// </summary>
        readonly Dictionary<string, string> _sessionNotes = new Dictionary<string, string>();

        /// <summary>Say something on a session row in place of its name, until the next press.</summary>
        internal void NoteOnSessionRow(string key, string note)
        {
            _sessionNotes[key] = note;
            OnExitChanged();
        }

        /// <summary>Take every note down. Called when the settings window opens or closes.</summary>
        internal void ClearSessionNotes()
        {
            if (_sessionNotes.Count == 0) return;
            _sessionNotes.Clear();
            OnExitChanged();
        }

        /// <summary>What a session row says on hover.</summary>
        internal static string SessionTooltip(SessionCommand command)
        {
            string what = command.Key switch
            {
                SessionCommands.SaveKey => "Writes this colony to a file under Saves",
                SessionCommands.SaveAsKey => "Writes this colony to a file under a new name",
                SessionCommands.LoadKey => "Opens another colony",
                SessionCommands.QuitToMenuKey => "Puts this colony down and goes back to the start screen",
                SessionCommands.QuitKey => "Leaves the game",
                SessionCommands.NewGameKey => "Starts a colony on a fresh board",
                _ => command.Label,
            };

            return command.AsksTwice ? what + ". It asks twice, because it cannot be undone" : what;
        }

        /// <summary>
        /// The armed row says what it is waiting for, and every other one goes back to its name.
        /// A loop over all of them, because the director holds one armed key across the window.
        /// </summary>
        void OnExitChanged()
        {
            if (_directors == null) return;
            string? armed = _directors.Settings.ArmedRow;
            if (armed != null) _sessionNotes.Clear();

            foreach (KeyValuePair<string, (VisualElement Row, Label Label)> pair in _sessionRows)
            {
                bool on = pair.Key == armed;
                pair.Value.Row.EnableInClassList("sw__action--armed", on);

                string text = on ? "Click again to confirm"
                    : _sessionNotes.TryGetValue(pair.Key, out string? note) ? note
                    : Registry.Label(pair.Key);
                HudText.Set(pair.Value.Label, text, HudTextRole.Row);
            }
        }

        // ============================================================ keyboard and focus

        /// <summary>
        /// Tab and the arrows walk the window; Enter and Space press; Escape closes.
        ///
        /// <para><b>While a control here has focus, the game's keys sit out</b> — the same gate a
        /// text field takes (<see cref="HotkeyDirector.BeginTyping"/>) — or Space on a switch
        /// would also pause the game and the arrows would also pan the camera. The window's
        /// root is the token, so focus moving between two of its controls never opens the gate
        /// in between. Focus that arrives from a mouse press is given back the moment the button
        /// is released, so a player who only clicks keeps the camera keys.</para>
        /// </summary>
        void WireSettingsKeyboard()
        {
            _settingsPanel.RegisterCallback<PointerDownEvent>(_ => _settingsPointer = true, TrickleDown.TrickleDown);
            _settingsPanel.RegisterCallback<PointerUpEvent>(_ =>
                _settingsPanel.schedule.Execute(() =>
                {
                    _settingsPointer = false;
                    if (FocusedInSettings() is VisualElement focused && focused is not DropdownField)
                        focused.Blur();
                }), TrickleDown.TrickleDown);

            _settingsPanel.RegisterCallback<FocusInEvent>(evt =>
            {
                Hotkeys().BeginTyping(_settingsPanel);
                if (evt.target is VisualElement target && !_settingsPointer) PlaceFocusRing(target);
                else _settingsFocusRing.style.display = DisplayStyle.None;
            }, TrickleDown.TrickleDown);

            _settingsPanel.RegisterCallback<FocusOutEvent>(evt =>
            {
                if (evt.relatedTarget is VisualElement next && _settingsPanel.Contains(next)) return;
                Hotkeys().EndTyping(_settingsPanel);
                _settingsFocusRing.style.display = DisplayStyle.None;
            }, TrickleDown.TrickleDown);

            _settingsPanel.RegisterCallback<KeyDownEvent>(OnSettingsKey);
        }

        VisualElement? FocusedInSettings() =>
            _settingsPanel.focusController?.focusedElement is VisualElement focused && _settingsPanel.Contains(focused)
                ? focused
                : null;

        void OnSettingsKey(KeyDownEvent evt)
        {
            // A slot waiting for its key owns the keyboard; the presenter answers it.
            if (Hotkeys().Listening != null) return;
            VisualElement? focused = FocusedInSettings();

            switch (evt.keyCode)
            {
                case KeyCode.Escape:
                    CloseSettings();
                    evt.StopPropagation();
                    return;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    if (focused != null && _activate.TryGetValue(focused, out Action? press))
                    {
                        press();
                        evt.StopPropagation();
                    }
                    return;

                case KeyCode.UpArrow:
                    MoveFocus(focused, -1);
                    evt.StopPropagation();
                    return;

                case KeyCode.DownArrow:
                    MoveFocus(focused, +1);
                    evt.StopPropagation();
                    return;

                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                {
                    int step = evt.keyCode == KeyCode.LeftArrow ? -1 : +1;
                    // A fader answers its own arrows; a segment picks its neighbour.
                    if (focused is Slider || focused?.parent is Slider) return;
                    if (focused != null && focused.ClassListContains("sw__seg"))
                    {
                        VisualElement group = focused.parent;
                        int at = group.IndexOf(focused) + step;
                        if (at >= 0 && at < group.childCount)
                        {
                            VisualElement next = group[at];
                            if (_activate.TryGetValue(next, out Action? pick)) pick();
                            next.Focus();
                        }
                    }
                    else
                    {
                        MoveFocus(focused, step);
                    }
                    evt.StopPropagation();
                    return;
                }
            }
        }

        /// <summary>
        /// Step to the previous or next control in the same part of the window — the rail, or
        /// the open page and its footer — in reading order. Nothing focused starts at the top of
        /// the page.
        /// </summary>
        void MoveFocus(VisualElement? from, int step)
        {
            if (_directors == null) return;
            bool inRail = from != null && _settingsRail.Contains(from);
            var stops = new List<VisualElement>();
            if (inRail)
            {
                _settingsRail.Query(className: "sw__focus").ForEach(stops.Add);
            }
            else if (_settingPages.TryGetValue(_directors.Settings.Tab, out VisualElement? page))
            {
                page.Query(className: "sw__focus").ForEach(e =>
                {
                    // One stop per segmented row — the lit segment — so Up and Down move between
                    // rows and Left and Right move along one.
                    if (e.ClassListContains("sw__seg") && !e.ClassListContains("sw__seg--on")) return;
                    stops.Add(e);
                });
                stops.Add(_resetButton);
            }
            stops.RemoveAll(e => !e.enabledInHierarchy);
            if (stops.Count == 0) return;

            int at = from == null ? -1 : stops.IndexOf(from);
            int next = at < 0 ? 0 : Mathf.Clamp(at + step, 0, stops.Count - 1);
            stops[next].Focus();
        }

        /// <summary>Stand the ring round a control: two pixels off it, two pixels thick, drawn
        /// last so no neighbour covers it. One ring for the window, rather than one per control,
        /// because UI Toolkit has no outline and a sibling drawn later would hide part of a
        /// ring that belonged to the control before it.</summary>
        void PlaceFocusRing(VisualElement target)
        {
            if (!target.ClassListContains("sw__focus"))
            {
                _settingsFocusRing.style.display = DisplayStyle.None;
                return;
            }
            Rect local = _settingsPanel.WorldToLocal(target.worldBound);
            float grow = SettingsLayout.FocusOffset + SettingsLayout.FocusWidth;
            _settingsFocusRing.style.left = local.x - grow - HudTheme.BorderWidth;
            _settingsFocusRing.style.top = local.y - grow - HudTheme.BorderWidth;
            _settingsFocusRing.style.width = local.width + 2 * grow;
            _settingsFocusRing.style.height = local.height + 2 * grow;
            _settingsFocusRing.style.display = DisplayStyle.Flex;
        }

        // ============================================================ refresh

        void OnSettingsTabChanged(SettingsTab tab)
        {
            foreach (KeyValuePair<SettingsTab, TabRowView> entry in _settingTabs)
            {
                bool on = entry.Key == tab;
                HudColour hue = SettingsLayout.Hue(entry.Key);
                entry.Value.Row.style.backgroundColor = on
                    ? Ink(hue.WithAlpha(SettingsLayout.SelectedFill))
                    : new StyleColor(StyleKeyword.Null);
                entry.Value.Bar.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                entry.Value.Label.style.color = on ? Ink(hue) : Ink(HudTheme.TextPrimary);
            }

            HudColour tabHue = SettingsLayout.Hue(tab);
            _titleTile.style.backgroundColor = Ink(tabHue.WithAlpha(SettingsLayout.TileFill));
            Color border = Ink(tabHue.WithAlpha(SettingsLayout.TileBorder));
            _titleTile.style.borderLeftColor = border;
            _titleTile.style.borderRightColor = border;
            _titleTile.style.borderTopColor = border;
            _titleTile.style.borderBottomColor = border;
            foreach (KeyValuePair<SettingsTab, PathGlyph> glyph in _titleGlyphs)
                glyph.Value.style.display = glyph.Key == tab ? DisplayStyle.Flex : DisplayStyle.None;

            HudText.Set(_titleName, Registry.Label(SettingsDirector.TabKey(tab)), HudTextRole.Name);
            HudText.Set(_titleSubtitle, SettingsLayout.Subtitle(tab), HudTextRole.Meta);
            HudText.Set(_resetLabel, _resetLabels[tab], HudTextRole.Row);
            _keysHint.style.display = tab == SettingsTab.Keys ? DisplayStyle.Flex : DisplayStyle.None;

            if (tab == SettingsTab.Graphics && _resolutionDropdown == null)
            {
                BuildResolutionRow();
                OnResolutionChanged();
            }

            foreach (KeyValuePair<SettingsTab, VisualElement> page in _settingPages)
                page.Value.style.display = page.Key == tab ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void OnSettingsChanged()
        {
            bool open = _directors != null && _directors.Settings.Open;
            _settingsPanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

            // Over the title screen too (design 40): its own scrim is clear now, and the window
            // opens over the dock, which this dims.
            _settingsScrim.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

            if (open)
            {
                // Raised over everything built after it — the main screen's scrim above all, which
                // is built later and pickable, and took every click meant for the window when
                // Settings was opened from the main screen (owner, 2026-09-24). The two prompts
                // that can be raised from the rail both close this window first, so raising it
                // never puts it over one of them.
                _settingsScrim.BringToFront();
                _settingsPanel.BringToFront();
                ToggleMenu(false);
                _directors?.Debug.SetOpen(false);
                if (_resolutionDropdown == null)
                {
                    BuildResolutionRow();
                    OnResolutionChanged();
                }
            }
            else
            {
                // A window put away with a control focused would keep the game's keys shut.
                FocusedInSettings()?.Blur();
                Hotkeys().EndTyping(_settingsPanel);
                _settingsFocusRing.style.display = DisplayStyle.None;
            }
        }

        void OnSettingChanged(GraphicsOption option)
        {
            if (_directors == null || !_settingRows.TryGetValue(option, out SwitchView? view)) return;
            bool on = _directors.Settings.IsOn(option);
            view.Control.EnableInClassList("sw__switch--on", on);
            view.Word.text = on ? "On" : "Off";
            RefreshPresetRow();
        }

        void OnAutosaveDaysChanged(int days) => LightRung(_autosaveRungs, days);

        void OnUiScaleChanged(int percent)
        {
            LightRung(_scaleRungs, percent);
            ApplyUiScale(percent);
        }

        void OnCameraSpeedChanged(int percent) => LightRung(_cameraRungs, percent);

        void OnBuildLayoutChanged(BuildPaletteLayout layout) => LightRung(_layoutRungs, layout);

        void OnSelectionStyleChanged(SelectionStyle style) => LightRung(_selectionRungs, style);

        void OnWakeUpChanged(bool on) => LightRung(_wakeRungs, on);

        /// <summary>A number ladder moved: light its segment, and re-answer the two questions one
        /// ladder asks of another — the frame cap behind VSync, the resolution behind the mode.</summary>
        void OnLadderChanged(GraphicsLadder ladder)
        {
            if (_directors == null) return;
            if (_ladderRungs.TryGetValue(ladder, out Dictionary<int, Label>? rungs))
                LightRung(rungs, _directors.Settings.Value(ladder));

            if (ladder == GraphicsLadder.VSync) RefreshFrameCapRow();
            if (ladder == GraphicsLadder.DisplayMode) RefreshResolutionRow();
            RefreshPresetRow();
        }

        /// <summary>The frame cap is dead behind VSync — Unity ignores the target rate whenever
        /// VSync is on — and the row says so rather than letting a player set 144 and get 60.</summary>
        void RefreshFrameCapRow()
        {
            if (_directors == null || !_ladderViews.TryGetValue(GraphicsLadder.FrameCap, out RowView? cap)) return;
            cap.SetLive(_directors.Settings.FrameCapIsLive, "Paced by VSync");
        }

        void OnResolutionChanged()
        {
            if (_directors == null) return;
            if (_resolutionDropdown == null) BuildResolutionRow();
            SettingsDirector.Mode res = _directors.Settings.Resolution;
            if (res.Width > 0 && _resolutionDropdown != null)
                _resolutionDropdown.SetValueWithoutNotify(ResolutionText(res));
        }

        void OnBusDbChanged(SettingsBus bus)
        {
            if (_directors == null || !_busFaders.TryGetValue(bus, out FaderView? view)) return;
            int at = _directors.Settings.BusDb(bus);

            float seat = SettingsDirector.TrackOf(at);
            if (!Mathf.Approximately(view.Fader.value, seat)) view.Fader.value = seat;
            view.Fill.style.width = Length.Percent((seat + 1f) * 50f);

            string text = VolumeText(at);
            // "Mute" is a word, and a word in the mono face is a word pretending to be a number.
            HudText.Apply(view.Value, HudTextRole.Row, numeric: text != "Mute");
            HudText.Set(view.Value, text, HudTextRole.Row);
        }
    }
}
