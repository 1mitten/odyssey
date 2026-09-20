#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// <see cref="HudShell"/>: the command bar, the menu, the Build palette and settings.
    ///
    /// <para>A8 along the bottom, its overflow popup, A7 the Build palette above it, and B17
    /// settings. Split out of the shell on 2026-09-16 because the one file had reached 1,947
    /// lines.</para>
    ///
    /// <para>The bar <b>may not wrap and may not overflow</b>: anything that does not fit moves
    /// into Menu, from the right, and Menu is never dropped. The reflow measures widths UI Toolkit
    /// actually laid out, taken once while every item is present and <c>visibility: hidden</c> —
    /// a <c>display: none</c> element has no width to read.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        // ============================================================ A8 command bar

        void BuildBar()
        {
            _barRow = new VisualElement { name = "bar-row", pickingMode = PickingMode.Ignore };
            _barRow.AddToClassList("bar-row");

            _bar = new VisualElement { name = "bar" };
            _bar.AddToClassList("commandbar");
            _barRow.Add(_bar);
            _worldUi.Add(_barRow);

            IReadOnlyList<HudCommand> commands = HudCommands.All;
            for (int i = 0; i < commands.Count; i++)
            {
                HudCommand command = commands[i];
                bool isMenu = command.Key == HudCommands.MenuKey;

                if (isMenu)
                {
                    _barDivider = new VisualElement { pickingMode = PickingMode.Ignore };
                    _barDivider.AddToClassList("commandbar__divider");
                    _bar.Add(_barDivider);
                }

                VisualElement item = CommandItem(command);
                _bar.Add(item);
                if (isMenu) _menuItem = item;
                else _barItems.Add(item);

                // Laid out but invisible until the first reflow has measured them. Hiding with
                // visibility rather than display is what makes the measurement possible at all:
                // a display:none element has no width to read, so the bar would have to be drawn
                // overflowing for one frame to find out that it overflows.
                item.style.visibility = Visibility.Hidden;
            }

            _barRow.RegisterCallback<GeometryChangedEvent>(_ => ReflowBar(_hud.resolvedStyle.width));

            BuildMenuPopup();
        }

        VisualElement CommandItem(HudCommand command)
        {
            var item = new VisualElement();
            item.name = command.Key;
            item.AddToClassList("cmd");
            if (command.Primary) item.AddToClassList("cmd--primary");
            if (!command.Live) item.AddToClassList("cmd--off");

            // Categorised: the command bar is the second and last place the spec allows an icon to
            // carry a colour of its own.
            var icon = new IconBadge(command.Key, IconBadge.BarSize, categorised: !command.Primary);
            // The primary cap's ink flips with its fill — accent on the outlined resting state,
            // the dark on-accent ink once build mode fills it. MarkBuildMode does the flipping;
            // this is only the starting value.
            if (command.Primary) icon.Inherit(HudTokens.Accent);
            item.Add(icon);

            Label label = HudText.Make(command.Label, HudTextRole.Row, ussClass: "cmd__label");
            if (command.Primary) label.AddToClassList("cmd__label--primary");
            item.Add(label);

            // Every item shows its key. An acceptance criterion, and the reason the bar is set in
            // a narrow face: eleven labelled items with their caps have to cross the screen.
            Label capLabel = HudText.Make(command.Hotkey, HudTextRole.Hotkey, ussClass: "cmd__key");
            item.Add(capLabel);

            // Build is the one cap on the bar that names a binding rather than a promise: it
            // follows the binding map when the player moves the key.
            if (command.Key == HudCommands.BuildKey)
            {
                _buildCap = capLabel;
                _buildItem = item;
                _buildIcon = icon;
                _buildTooltipLabel = command.Label;
            }

            item.tooltip = command.Live
                ? command.Label + " — " + command.Hotkey
                : command.Label + " — " + command.Reason;

            // Only a live command listens. Nine of the eleven do nothing yet, and they were all
            // registering a handler that fell through OnCommand's two cases in silence — a button
            // that accepts the click and then declines to act. The inspect tabs already work this
            // way (`if (tab.Enabled)`), and the menu rows and palette chips register nothing at
            // all; this is the bar catching up with them.
            if (command.Live)
            {
                string key = command.Key;
                item.RegisterCallback<ClickEvent>(_ => OnCommand(key));
            }

            return item;
        }

        void OnCommand(string key)
        {
            if (key == HudCommands.BuildKey) SetBuildPalette(!BuildPaletteOpen);
            else if (key == HudCommands.MenuKey) ToggleMenu();
        }

        /// <summary>
        /// The one hotkey on the bar that is live, read through the binding map so the panel
        /// that rebinds it and the key that opens the palette can never disagree. The rest of
        /// the caps are legends on controls whose systems do not exist, and they deliberately
        /// avoid every action the game already has — camera, slice, clock, tools.
        /// </summary>
        void ReadBarKeys()
        {
            var keys = UnityEngine.InputSystem.Keyboard.current;
            if (keys == null) return;

            // The colony's map when there is one, defaults when there is not, so the key
            // works in a harness scene with no world behind the shell.
            HotkeyDirector hotkeys = Hotkeys();

            // A key offered to a slot in the settings panel belongs to the rebind, not to
            // the palette it might be being bound to — and a key typed into a text field
            // belongs to the field. One question, asked by every poller.
            if (!hotkeys.GameKeysLive) return;

            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.BuildPalette))
                SetBuildPalette(!BuildPaletteOpen);
        }

        /// <summary>
        /// Fit the bar to the width it has, and put whatever does not fit into Menu.
        ///
        /// <para><b>The bar may not wrap and may not overflow</b>, which is the acceptance
        /// criterion and also what lets the inspect pane above it stop guessing at its height. The
        /// widths are the ones UI Toolkit actually laid out rather than an estimate, measured once
        /// while every item is present and invisible; re-measuring after items have been hidden
        /// would measure the shortened bar and oscillate.</para>
        /// </summary>
        void ReflowBar(float screenWidth)
        {
            if (_barItems.Count == 0 || screenWidth <= 0f) return;

            if (_barWidths.Count == 0)
            {
                bool measured = true;
                foreach (VisualElement item in _barItems)
                {
                    float width = item.resolvedStyle.width;
                    if (float.IsNaN(width) || width <= 1f) { measured = false; break; }
                }
                float menuWidth = _menuItem.resolvedStyle.width;
                if (float.IsNaN(menuWidth) || menuWidth <= 1f) measured = false;
                if (!measured) return;

                foreach (VisualElement item in _barItems) _barWidths.Add(item.resolvedStyle.width);
                _barWidths.Add(menuWidth);
            }

            // The bar's own padding, not the screen margin: the bar runs edge to edge now, so the
            // room an item has is the screen less what the bar itself takes.
            float available = screenWidth - 2 * HudCommands.BarPad;
            if (Mathf.Approximately(available, _barMeasuredAt)) return;
            _barMeasuredAt = available;

            int shown = HudCommands.Fit(_barWidths, available);
            if (shown == _barShown) return;
            _barShown = shown;

            _menuOverflow.Clear();
            IReadOnlyList<HudCommand> commands = HudCommands.All;
            for (int i = 0; i < _barItems.Count; i++)
            {
                bool fits = i < shown;
                _barItems[i].style.display = fits ? DisplayStyle.Flex : DisplayStyle.None;
                _barItems[i].style.visibility = Visibility.Visible;

                // The last item before the divider carries no trailing gap, so the bar's realised
                // width is exactly what HudCommands.BarWidth says it is — which is what the
                // PlayMode gate compares against, and what "nothing is cut off at the right edge"
                // reduces to once the model is trusted.
                _barItems[i].style.marginRight = fits && i == shown - 1 ? 0 : HudCommands.ItemGap;

                if (!fits) _menuOverflow.Add(MenuRow(commands[i]));
            }
            _menuItem.style.marginRight = 0;
            _menuItem.style.visibility = Visibility.Visible;
            _barDivider.style.display = DisplayStyle.Flex;
        }

        static readonly string[] OverlayKeys =
        {
            "ui.overlay.temperature", "ui.overlay.light", "ui.overlay.beauty", "ui.overlay.cleanliness",
            "ui.overlay.roofs", "ui.overlay.zones", "ui.overlay.power", "ui.overlay.salvage",
            "ui.overlay.support", "ui.overlay.traffic",
        };

        void BuildMenuPopup()
        {
            _menuPopup = Popover("menu", "Menu", () => ToggleMenu(false), "menu");

            _menuOverflow = new VisualElement();
            _menuOverflow.AddToClassList("menu__rows");
            _menuPopup.Add(_menuOverflow);

            // A12, the overlay toggles. They used to be a strip of ten unlabelled icon buttons at
            // the right end of the bottom bar, where they sat on top of the tab row and ate its
            // last tab — and the acceptance criteria strike out "overflowing colour chips at the
            // right end of the command bar" by name. They are not information a player can act
            // on until a channel renders (M4), so they come here, labelled, rather than being
            // deleted: a control that exists and is disabled with its reason is the catalogue's
            // rule, and an unlabelled chip on the bar was neither.
            _menuPopup.Add(HudText.Make("Overlays", HudTextRole.PanelLabel, ussClass: "menu__section"));
            foreach (string key in OverlayKeys)
            {
                var overlay = new VisualElement();
                overlay.AddToClassList("menu__row");
                overlay.AddToClassList("menu__row--off");
                var icon = new IconBadge(key, IconBadge.BarSize);
                icon.Inherit(HudTokens.TextMeta);
                overlay.Add(icon);
                overlay.Add(HudText.Make(Registry.Label(key), HudTextRole.Row, ussClass: "menu__label"));
                overlay.tooltip = Registry.Label(key) + " — overlay channels arrive with M4";
                _menuPopup.Add(overlay);
            }

            _menuPopup.Add(HudText.Make("Game", HudTextRole.PanelLabel, ussClass: "menu__section"));

            var settings = new VisualElement();
            settings.AddToClassList("menu__row");
            settings.Add(HudText.Make("Settings", HudTextRole.Row, ussClass: "menu__label"));
            settings.Add(HudText.Make("Esc", HudTextRole.Hotkey, ussClass: "menu__key"));
            settings.tooltip = "Settings, and the way out. None of it is in the save.";
            settings.RegisterCallback<ClickEvent>(_ =>
            {
                ToggleMenu(false);
                _directors?.Settings.SetOpen(true);
            });
            _menuPopup.Add(settings);

            _worldUi.Add(_menuPopup);
        }

        VisualElement MenuRow(HudCommand command)
        {
            var row = new VisualElement();
            row.AddToClassList("menu__row");
            row.AddToClassList("menu__row--off");
            var icon = new IconBadge(command.Key, IconBadge.BarSize, categorised: true);
            row.Add(icon);
            row.Add(HudText.Make(command.Label, HudTextRole.Row, ussClass: "menu__label"));
            row.Add(HudText.Make(command.Hotkey, HudTextRole.Hotkey, ussClass: "menu__key"));
            row.tooltip = command.Label + " — " + command.Reason;
            return row;
        }

        void ToggleMenu() => ToggleMenu(_menuPopup.style.display == DisplayStyle.None);

        void ToggleMenu(bool open)
        {
            if (open) SetBuildPalette(false);

            _menuPopup.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _menuItem.EnableInClassList("cmd--on", open);
            if (open && _menuItem != null) PlacePopover(_menuPopup, _menuItem);
        }

        /// <summary>Whether the Menu popover is open, for whoever owns the Escape key.</summary>
        public bool MenuOpen => _menuPopup != null && _menuPopup.style.display == DisplayStyle.Flex;

        /// <summary>Close the Menu popover. The Escape half, called by <c>SettingsPresenter</c>.</summary>
        public void CloseMenu() => ToggleMenu(false);

        // ============================================================ B17 settings

        /// <summary>
        /// The settings panel: four sections behind a tab strip, opened with Escape or from
        /// Menu, with the way out pinned under all of them.
        ///
        /// <para><b>It is not a modal.</b> There is no scrim and nothing is blocked: the world
        /// runs, the camera orbits and the clock ticks while it is open, because the only reason
        /// to have the panel is to watch the board change as a lever moves. Clicks that land on it
        /// already stop at the panel edge through <see cref="PointOverUi"/>.</para>
        ///
        /// <para><b>Interface before Graphics</b>, because the first thing a player wants from a
        /// settings panel on a large monitor is to make the type bigger, and because that is the
        /// one setting here that changes the panel they are looking at while they look at it.
        /// Audio and Keys follow: the faders waited in their store for this panel, and the
        /// bindings waited for the map that could hold them.</para>
        /// </summary>
        void BuildSettings()
        {
            // A window but not a popover: it is reached from Menu and from Escape, so there is no
            // one button it belongs over, and it keeps the centring a settings panel wants.
            _settingsPanel = Window("settings", Registry.Label(SettingsDirector.PanelKey),
                () => _directors?.Settings.SetOpen(false), "settings");

            // The tab strip, in the same idiom as the inspect pane's: nothing new is invented for
            // a second use of a control the HUD already has.
            var tabs = new VisualElement();
            tabs.AddToClassList("settings__tabs");
            foreach (SettingsTab tab in new[]
                     { SettingsTab.Interface, SettingsTab.Graphics, SettingsTab.Audio, SettingsTab.Keys })
            {
                Label chip = HudText.Make(Registry.Label(SettingsDirector.TabKey(tab)),
                    HudTextRole.Body, ussClass: "tab");
                SettingsTab captured = tab;
                chip.RegisterCallback<ClickEvent>(_ => _directors?.Settings.SetTab(captured));
                _settingTabs[tab] = chip;
                tabs.Add(chip);
            }
            _settingsPanel.Add(tabs);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("settings__scroll");
            scroll.AddToClassList("build__scroll");

            BuildInterfaceSection(scroll);
            BuildGraphicsSection(scroll);
            BuildAudioSection(scroll);
            BuildKeysSection(scroll);
            _settingsPanel.Add(scroll);

            // The session rows across a 2-column grid at the bottom, so they take half the vertical
            // space and never push the panel off-screen.
            var sessionGrid = new VisualElement();
            sessionGrid.AddToClassList("settings__session-grid");
            var leftCol = new VisualElement();
            leftCol.AddToClassList("settings__session-col");
            var rightCol = new VisualElement();
            rightCol.AddToClassList("settings__session-col");
            sessionGrid.Add(leftCol);
            sessionGrid.Add(rightCol);

            foreach (SessionCommand command in SessionCommands.For(SessionContext.InGame))
            {
                VisualElement row = SessionRow(command, separated: false);
                if (command.Key == SessionCommands.QuitToMenuKey || command.Key == SessionCommands.QuitKey)
                    rightCol.Add(row);
                else
                    leftCol.Add(row);
            }
            _settingsPanel.Add(sessionGrid);

            // The director opens on Interface, and the shell may never attach to a director at all
            // in a harness that builds no world. Showing every section at once is not a state
            // anything asks for, so it is not a state the panel is ever in.
            OnSettingsTabChanged(SettingsTab.Interface);

            _settingsPanel.Add(HudText.Make("Escape closes. None of it is in the save.",
                HudTextRole.Meta, ussClass: "settings__note"));
            _hud.Add(_settingsPanel);
        }

        /// <summary>
        /// The Interface section: how large the HUD is drawn.
        ///
        /// <para>A ladder of percentages rather than a slider. The rungs are the director's, so
        /// the set is testable in the fast tier, and each says out loud what the trade is — larger
        /// type is easier to read and hides more of the board, which is the whole of the decision
        /// the player is making.</para>
        /// </summary>
        void BuildInterfaceSection(VisualElement parent)
        {
            _interfaceSection = new VisualElement();
            _interfaceSection.AddToClassList("settings__body");

            var columns = new VisualElement();
            columns.AddToClassList("settings__columns");

            var leftCol = new VisualElement();
            leftCol.AddToClassList("settings__column");
            var rightCol = new VisualElement();
            rightCol.AddToClassList("settings__column");
            columns.Add(leftCol);
            columns.Add(rightCol);

            // A percentage is a figure, so it is set in the mono face like every other figure on
            // this screen.
            BuildLadderRow(leftCol, SettingsDirector.UiScaleKey,
                SettingsDirector.UiScales,
                percent => percent + "%",
                percent => percent == 100
                    ? "The size the interface is designed at"
                    : percent < 100
                        ? "Smaller type, less of the board hidden"
                        : "Larger type, more of the board hidden",
                percent => _directors?.Settings.SetUiScale(percent),
                _scaleRungs);

            BuildCameraSpeedRow(rightCol);
            BuildLayoutRow(rightCol);

            _interfaceSection.Add(columns);
            parent.Add(_interfaceSection);
        }

        /// <summary>
        /// The camera-speed ladder: how fast the rig pans and zooms, as a share of the speed
        /// it was tuned at. The same ladder idiom as the interface scale, for the same
        /// reason — a handful of honest answers rather than a knob.
        /// </summary>
        void BuildCameraSpeedRow(VisualElement parent)
        {
            // A multiplier is a figure, so: mono, like every other figure on this screen.
            BuildLadderRow(parent, SettingsDirector.CamSpeedKey,
                SettingsDirector.CameraSpeeds,
                percent => $"{percent / 100f:0.#}×",
                percent => percent == 100
                    ? "The speed the camera was tuned at"
                    : percent < 100
                        ? "Slower, for fine placement"
                        : "Faster, for crossing the map",
                percent => _directors?.Settings.SetCameraSpeed(percent),
                _cameraRungs);
        }

        /// <summary>
        /// Which shape the Build palette takes.
        ///
        /// <para><b>The same control exists inside the palette's own header</b>, and both write
        /// the one preference on <see cref="SettingsDirector"/>. It is here as well because a
        /// three-icon switcher in a panel header is findable by someone who is already looking at
        /// the panel and invisible to everyone else, and the layout is exactly the sort of choice
        /// a player makes once, early, from the settings screen.</para>
        ///
        /// <para><b>A ladder, not the dropdown the specification asked for.</b> Three named
        /// answers is what this panel's other two multiple choices already are — the interface
        /// scale and the camera speed — and the argument written against those holds here: a
        /// handful of honest answers rather than a control that hides two of the three until it is
        /// opened.</para>
        /// </summary>
        void BuildLayoutRow(VisualElement parent)
        {
            // A name, not a figure, so this is the one ladder on the panel set in the reading
            // face rather than the mono one.
            BuildLadderRow(parent, SettingsDirector.BuildLayoutKey,
                BuildPaletteModel.Layouts,
                BuildPaletteModel.LayoutName,
                layout => layout switch
                {
                    BuildPaletteLayout.Rows => "Bands across the screen. The default",
                    BuildPaletteLayout.Rail => "Categories down a rail. Its height never changes",
                    BuildPaletteLayout.Bar => "Two dense rows of icons. The least of the board hidden",
                    _ => string.Empty,
                },
                layout => _directors?.Settings.SetBuildPaletteLayout(layout),
                _layoutRungs,
                numeric: false);
        }

        /// <summary>
        /// One labelled row with a rank of answers under it — the panel's one multiple-choice
        /// idiom, in one place.
        ///
        /// <para><b>There were three copies of this before the graphics ladders arrived</b>: the
        /// interface scale, the camera speed and the Build-palette layout each built the same row
        /// and the same rank with their own loop. Seven would have been seven, and they had
        /// already begun to differ — one set its rungs in the mono face, one in the reading face,
        /// and nothing said which was the rule. The face is now the caller's single
        /// <paramref name="numeric"/> flag and everything else is shared.</para>
        ///
        /// <para><b>Every rung's text is built here, once, as the row is constructed.</b> ADR
        /// 0003's flip condition F1 — asserted by <c>HudStressTests</c> — is that the HUD
        /// allocates nothing per frame in steady state, and <c>ToString</c>, interpolation and
        /// <c>+</c> all allocate. The change handlers that follow only flip a USS class on a
        /// label that already exists, so throwing one of these levers costs nothing after the
        /// frame it is thrown on.</para>
        /// </summary>
        /// <param name="numeric">Whether the rungs are figures, and so set in the mono face. A
        /// name — "Borderless", "Rail" — is set in the reading face instead.</param>
        LadderView BuildLadderRow<T>(VisualElement parent, string key, IReadOnlyList<T> rungs,
            Func<T, string> labelOf, Func<T, string> tooltipOf, Action<T> onPick,
            IDictionary<T, Label> into, bool numeric = true, string? rowTooltip = null)
        {
            var row = new VisualElement();
            row.AddToClassList("settings__row");
            row.AddToClassList("settings__row--static");
            var icon = new IconBadge(key, IconBadge.RowSize);
            icon.Inherit(HudTokens.TextMeta);
            row.Add(icon);
            row.Add(HudText.Make(Registry.Label(key), HudTextRole.Row, ussClass: "settings__label"));
            if (rowTooltip != null) row.tooltip = rowTooltip;
            parent.Add(row);

            var ladder = new VisualElement();
            ladder.AddToClassList("settings__ladder");
            foreach (T value in rungs)
            {
                Label rung = numeric
                    ? HudText.Make(labelOf(value), HudTextRole.Body, numeric: true, "rung")
                    : HudText.Make(labelOf(value), HudTextRole.Body, ussClass: "rung");
                rung.tooltip = tooltipOf(value);
                T captured = value;
                rung.RegisterCallback<ClickEvent>(_ => onPick(captured));
                into[value] = rung;
                ladder.Add(rung);
            }

            parent.Add(ladder);
            return new LadderView(row, ladder);
        }

        /// <summary>A ladder's two pieces, kept so a rule that makes one of them inert — the
        /// frame cap behind VSync, the display rows in the editor — can reach both.</summary>
        readonly struct LadderView
        {
            public LadderView(VisualElement row, VisualElement rungs)
            {
                Row = row;
                Rungs = rungs;
            }

            public VisualElement Row { get; }

            public VisualElement Rungs { get; }

            /// <summary>Grey the row and its answers together, in the look the panel already uses
            /// for a control that cannot be pressed (<c>.settings__row--off</c>).</summary>
            public void SetLive(bool live)
            {
                Row.EnableInClassList("settings__row--off", !live);
                Rungs.EnableInClassList("settings__row--off", !live);
                Rungs.SetEnabled(live);
            }
        }

        /// <summary>Light the rung that is standing, and put the rest out. The whole of what a
        /// ladder does when its value moves — no text is built, so nothing allocates.</summary>
        static void LightRung<T>(IDictionary<T, Label> rungs, T standing)
        {
            foreach (var entry in rungs)
                entry.Value.EnableInClassList("rung--on",
                    EqualityComparer<T>.Default.Equals(entry.Key, standing));
        }

        /// <summary>
        /// The Graphics tab, in two groups.
        ///
        /// <para><b>Display</b> is what the frame costs — how it is paced, how large it is drawn,
        /// what it is drawn with. <b>Detail</b> is the six older toggles, which are what the
        /// board is made of. They were one undifferentiated column until the display levers
        /// arrived, and mixing "grass tufts" with "VSync" in one list would have buried the row a
        /// player with a stuttering frame came here to find. The heading is
        /// <c>.settings__section</c>, the class the Keys tab already groups with.</para>
        /// </summary>
        void BuildGraphicsSection(VisualElement parent)
        {
            _graphicsSection = new VisualElement();
            _graphicsSection.AddToClassList("settings__body");

            var columns = new VisualElement();
            columns.AddToClassList("settings__columns");

            var leftCol = new VisualElement();
            leftCol.AddToClassList("settings__column");
            var rightCol = new VisualElement();
            rightCol.AddToClassList("settings__column");
            columns.Add(leftCol);
            columns.Add(rightCol);

            leftCol.Add(HudText.Make(Registry.Label(SettingsDirector.DisplayGroupKey),
                HudTextRole.Meta, ussClass: "settings__section"));

            foreach (GraphicsLadder ladder in SettingsDirector.AllLadders)
            {
                var rungs = new Dictionary<int, Label>();
                GraphicsLadder captured = ladder;
                _ladderRungs[ladder] = rungs;
                _ladderViews[ladder] = BuildLadderRow(leftCol,
                    SettingsDirector.KeyOf(ladder),
                    SettingsDirector.RungsOf(ladder),
                    rung => SettingsDirector.RungLabel(captured, rung),
                    rung => SettingsDirector.RungTooltip(captured, rung),
                    rung => _directors?.Settings.SetValue(captured, rung),
                    rungs,
                    numeric: true,
                    rowTooltip: RowCostOf(ladder));
            }

            // The display mode is the editor's other blind spot, for the same reason the
            // resolution is: the Game view is not a window the game owns.
            if (Application.isEditor &&
                _ladderViews.TryGetValue(GraphicsLadder.DisplayMode, out LadderView mode))
            {
                mode.Row.tooltip = OnlyInAPlayer;
                mode.SetLive(false);
            }

            // The resolution's dropdown row is built once the machine's sizes are known.
            _resolutionSlot = new VisualElement();
            leftCol.Add(_resolutionSlot);

            rightCol.Add(HudText.Make(Registry.Label(SettingsDirector.DetailGroupKey),
                HudTextRole.Meta, ussClass: "settings__section"));

            foreach (GraphicsOption option in SettingsDirector.All)
            {
                string key = SettingsDirector.KeyOf(option);
                var row = new VisualElement();
                row.AddToClassList("settings__row");
                var icon = new IconBadge(key, IconBadge.RowSize);
                icon.Inherit(HudTokens.TextMeta);
                row.Add(icon);
                row.Add(HudText.Make(Registry.Label(key), HudTextRole.Row, ussClass: "settings__label"));

                var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                pip.AddToClassList("settings__pip");
                row.Add(pip);

                // Said in the tooltip rather than on the row, because it is a fact about what the
                // toggle costs, not about what it does.
                row.tooltip = SettingsDirector.NeedsRedraw(option)
                    ? "Redraws the board when it changes"
                    : "Takes effect on the next frame";

                GraphicsOption captured = option;
                row.RegisterCallback<ClickEvent>(_ => _directors?.Settings.Toggle(captured));
                _settingRows[option] = row;
                rightCol.Add(row);
            }

            _graphicsSection.Add(columns);
            parent.Add(_graphicsSection);
        }

        /// <summary>
        /// What a row costs, said on hover rather than on the row.
        ///
        /// <para>The same bargain the toggles already strike with <c>NeedsRedraw</c>: it is a
        /// fact about what the lever costs, not about what it does, so it belongs in the tooltip.
        /// Render scale and anti-aliasing throw the frame buffers away and build new ones, which
        /// is a visible hitch on the frame they change and nothing afterwards.</para>
        /// </summary>
        static string RowCostOf(GraphicsLadder ladder) =>
            SettingsDirector.CostsAHitch(ladder)
                ? "Rebuilds the frame buffers when it changes, once"
                : "Takes effect on the next frame";

        /// <summary>
        /// The resolution dropdown row.
        ///
        /// <para><b>Built after the director has been seeded</b>, because its choices are the
        /// machine's: <c>Screen.resolutions</c>, de-duplicated by area. An unseeded list draws no
        /// row at all rather than an empty one.</para>
        ///
        /// <para><b>Inert in the editor</b>, with the display mode row, because
        /// <c>Screen.SetResolution</c> does not mean anything against the Game view and a control
        /// that silently does nothing is worse than one that says it cannot.</para>
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

            var row = new VisualElement();
            row.AddToClassList("settings__row");
            string key = SettingsDirector.ResolutionKey;
            var icon = new IconBadge(key, IconBadge.RowSize);
            icon.Inherit(HudTokens.TextMeta);
            row.Add(icon);
            row.Add(HudText.Make(Registry.Label(key), HudTextRole.Row, ussClass: "settings__label"));

            var choices = new List<string>(modes.Count);
            var modeMap = new Dictionary<string, SettingsDirector.Mode>(modes.Count);
            for (int i = 0; i < modes.Count; i++)
            {
                string text = modes[i].Width + "\u00d7" + modes[i].Height;
                choices.Add(text);
                modeMap[text] = modes[i];
            }

            SettingsDirector.Mode current = _directors?.Settings.Resolution ?? default;
            string initial = current.Width > 0 ? (current.Width + "\u00d7" + current.Height) : (choices.Count > 0 ? choices[0] : "");

            var dropdown = new DropdownField(choices, initial);
            dropdown.AddToClassList("settings__dropdown");
            if (dropdown.labelElement != null) dropdown.labelElement.style.display = DisplayStyle.None;
            var textElem = dropdown.Q<TextElement>(className: "unity-base-popup-field__text");
            if (textElem != null) HudText.Apply(textElem, HudTextRole.Body, numeric: true);

            dropdown.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != null && modeMap.TryGetValue(evt.newValue, out SettingsDirector.Mode picked))
                    _directors?.Settings.SetResolution(picked);
            });

            if (Application.isEditor)
            {
                row.tooltip = OnlyInAPlayer;
                row.AddToClassList("settings__row--off");
                dropdown.SetEnabled(false);
            }
            else
            {
                row.tooltip = "Resolution the game runs at";
            }

            row.Add(dropdown);
            _resolutionRow = row;
            _resolutionDropdown = dropdown;
            _resolutionSlot.Add(row);
        }

        /// <summary>Said on the two rows the editor cannot answer. The Game view is not a window
        /// the game owns, so neither the size nor the mode means anything until the player build
        /// runs.</summary>
        const string OnlyInAPlayer = "Only a built game can change this. The editor ignores it";

        /// <summary>
        /// The Audio section: one fader per bus, with the dB it rests at said beside it.
        ///
        /// <para>These faders have been in <c>AudioSettingsStore</c> since the sound work
        /// landed — persisted, applied at boot, and writable by nothing. This section is the
        /// panel they were waiting for, and the span is the director's so the bounds are
        /// testable in the fast tier like every other lever here.</para>
        ///
        /// <para><b>A slider, since the owner asked for one on 2026-09-17</b> — and later
        /// the same day, for <b>unity seated at the centre of the track</b>: dragging left
        /// lowers towards silence, dragging right boosts to the ceiling. The slider's own
        /// value is track position (−1 to +1; see <see cref="SettingsDirector.TrackOf"/> for
        /// why it is not dB), and the notch under the thumb marks where the default sits, so
        /// "put it back the way it shipped" is a place rather than a memory. It is the
        /// built-in <see cref="Slider"/> restyled rather than a hand-rolled thumb, because
        /// the engine's drag capture, track jumps and arrow keys are behaviour this HUD has
        /// no reason to re-derive — the Build palette's scroller already showed the way
        /// in.</para>
        /// </summary>
        void BuildAudioSection(VisualElement parent)
        {
            _audioSection = new VisualElement();
            _audioSection.AddToClassList("settings__body");

            var columns = new VisualElement();
            columns.AddToClassList("settings__columns");

            var leftCol = new VisualElement();
            leftCol.AddToClassList("settings__column");
            var rightCol = new VisualElement();
            rightCol.AddToClassList("settings__column");
            columns.Add(leftCol);
            columns.Add(rightCol);

            foreach (SettingsBus bus in SettingsDirector.Buses)
            {
                VisualElement targetCol =
                    (bus == SettingsBus.Master || bus == SettingsBus.Music || bus == SettingsBus.Ambience)
                        ? leftCol : rightCol;

                string key = SettingsDirector.VolumeKey(bus);
                var row = new VisualElement();
                row.AddToClassList("settings__row");
                row.AddToClassList("settings__row--static");
                var icon = new IconBadge(key, IconBadge.RowSize);
                icon.Inherit(HudTokens.TextMeta);
                row.Add(icon);
                row.Add(HudText.Make(Registry.Label(key), HudTextRole.Row, ussClass: "settings__label"));

                Label value = HudText.Make(VolumeText(SettingsDirector.UnityDb), HudTextRole.Body,
                    numeric: true, ussClass: "settings__value");
                row.Add(value);
                targetCol.Add(row);

                var fader = new Slider(-1f, 1f, SliderDirection.Horizontal);
                fader.AddToClassList("settings__fader");
                fader.SetValueWithoutNotify(SettingsDirector.TrackOf(SettingsDirector.UnityDb));
                fader.tooltip =
                    "Drag to set the volume — silence at the left, unity at the centre mark, boost at the right";

                // The centre mark, first child so the track and the thumb both draw over it:
                // where unity sits, findable after the thumb has been dragged away from it.
                var notch = new VisualElement { pickingMode = PickingMode.Ignore };
                notch.AddToClassList("settings__fader-notch");
                fader.Insert(0, notch);

                SettingsBus capturedBus = bus;
                fader.RegisterValueChangedCallback(evt =>
                {
                    // Whole decibels: a thumb resting between two of them is a position the
                    // readout cannot say and the store cannot keep. The guard keeps a drag
                    // to one write per dB, and one store save with it.
                    int db = SettingsDirector.DbOf(evt.newValue);
                    if (_directors != null && _directors.Settings.BusDb(capturedBus) != db)
                        _directors.Settings.SetBusDb(capturedBus, db);
                });
                targetCol.Add(fader);

                _busFaders[bus] = new FaderView { Fader = fader, Value = value };
            }

            _audioSection.Add(columns);
            parent.Add(_audioSection);
        }

        /// <summary>What one fader's readout says. Mute is a word because silence is not a
        /// number; a boost says its plus out loud, so the two sides of the centre mark read
        /// as the different promises they are; everything else is the decibels it
        /// is.</summary>
        static string VolumeText(int db) =>
            db <= SettingsDirector.SilenceDb ? "Mute"
            : db == SettingsDirector.UnityDb ? "0 dB"
            : db < SettingsDirector.UnityDb ? db + " dB"
            : "+" + db + " dB";

        /// <summary>
        /// The groups the binding list is drawn in. Layout is the shell's business — the
        /// actions, their defaults and their rules are the director's — but which ones share
        /// a heading is a question about the panel, not about the bindings.
        /// </summary>
        static readonly (string Header, HotkeyAction[] Actions)[] KeyGroups =
        {
            ("Camera", new[]
            {
                HotkeyAction.CameraForward, HotkeyAction.CameraBack,
                HotkeyAction.CameraRight, HotkeyAction.CameraLeft,
                HotkeyAction.CameraTurnLeft, HotkeyAction.CameraTurnRight,
            }),
            ("View", new[]
            {
                HotkeyAction.SliceUp, HotkeyAction.SliceDown,
                HotkeyAction.CycleAbove, HotkeyAction.FrameMap,
            }),
            ("Time", new[]
            {
                HotkeyAction.Pause, HotkeyAction.Speed1, HotkeyAction.Speed2, HotkeyAction.Speed3,
            }),
            ("Tools", new[]
            {
                HotkeyAction.ToolMine, HotkeyAction.ToolFell, HotkeyAction.ToolCancel,
            }),
            ("Interface", new[]
            {
                HotkeyAction.BuildPalette, HotkeyAction.DebugMenu,
            }),
        };

        /// <summary>
        /// The Keys section: every action the game reads a key for, grouped, one or two caps
        /// per row. Click a cap to change it; the next key pressed is offered to that slot,
        /// and Escape backs out of the wait without unwinding anything under it.
        /// </summary>
        void BuildKeysSection(VisualElement parent)
        {
            _keysSection = new VisualElement();
            _keysSection.AddToClassList("settings__body");

            var columns = new VisualElement();
            columns.AddToClassList("settings__columns");

            var col0 = new VisualElement();
            col0.AddToClassList("settings__column");
            var col1 = new VisualElement();
            col1.AddToClassList("settings__column");
            var col2 = new VisualElement();
            col2.AddToClassList("settings__column");
            columns.Add(col0);
            columns.Add(col1);
            columns.Add(col2);

            foreach ((string header, HotkeyAction[] actions) in KeyGroups)
            {
                VisualElement targetCol = (header == "Camera") ? col0
                    : (header == "View" || header == "Tools") ? col1
                    : col2;
                targetCol.Add(HudText.Make(header, HudTextRole.Meta, ussClass: "settings__section"));

                foreach (HotkeyAction action in actions)
                {
                    string key = HotkeyDirector.KeyOf(action);
                    var row = new VisualElement();
                    row.AddToClassList("settings__row");
                    var icon = new IconBadge(key, IconBadge.RowSize);
                    icon.Inherit(HudTokens.TextMeta);
                    row.Add(icon);
                    row.Add(HudText.Make(Registry.Label(key), HudTextRole.Row, ussClass: "settings__label"));

                    var view = new KeyRowView { Root = row };
                    for (int slot = 0; slot < HotkeyDirector.SlotCount; slot++)
                    {
                        HotkeyAction capturedAction = action;
                        int capturedSlot = slot;
                        Label cap = HudText.Make("—", HudTextRole.Hotkey, ussClass: "settings__keycap");
                        cap.RegisterCallback<ClickEvent>(_ =>
                            _directors?.Hotkeys.Listen(capturedAction, capturedSlot));
                        view.Caps[slot] = cap;
                        row.Add(cap);
                    }

                    _keyRows[action] = view;
                    targetCol.Add(row);
                }
            }

            Label reset = HudText.Make(Registry.Label(HotkeyDirector.ResetKey), HudTextRole.Body,
                ussClass: "rung settings__reset");
            reset.tooltip = "Every action goes back to the key it shipped with";
            reset.RegisterCallback<ClickEvent>(_ => _directors?.Hotkeys.ResetKeys());
            col2.Add(reset);

            _keysSection.Add(columns);
            parent.Add(_keysSection);
        }

        /// <summary>
        /// Redraw every cap from the binding map. Called on change only — a click, a capture,
        /// a reset — never per frame, so building the strings costs a click and not the
        /// frame budget.
        /// </summary>
        void RefreshKeyCaps()
        {
            if (_directors == null) return;
            HotkeyDirector hotkeys = _directors.Hotkeys;

            foreach (HotkeyAction action in HotkeyDirector.All)
            {
                if (!_keyRows.TryGetValue(action, out KeyRowView view)) continue;
                for (int slot = 0; slot < HotkeyDirector.SlotCount; slot++)
                {
                    HudKey key = hotkeys.Key(action, slot);
                    bool listening = hotkeys.Listening == (action, slot);
                    Label cap = view.Caps[slot];

                    cap.text = listening ? "…" : key == HudKey.None ? "—" : HotkeyDirector.Display(key);
                    cap.EnableInClassList("settings__keycap--listening", listening);

                    HudKey def = HotkeyDirector.DefaultKey(action, slot);
                    cap.tooltip = listening
                        ? "Press the key to bind. Escape cancels."
                        : def == HudKey.None
                            ? "Click, then press a key"
                            : "Click, then press a key. Default: " + HotkeyDirector.Display(def);
                }
            }

            // The orders strip names a key in every tooltip, read from the same binding map, so a
            // rebind that stopped here would leave four buttons promising a key that no longer
            // arms anything.
            RefreshOrderTooltips();
        }

        /// <summary>
        /// Say on the cap why the key it was offered did not take. The refusal itself is the
        /// director's; this is only its voice.
        /// </summary>
        void OnHotkeyConflict(HotkeyAction refused, HotkeyAction owner)
        {
            if (_directors == null || !_keyRows.TryGetValue(refused, out KeyRowView view)) return;
            (HotkeyAction Action, int Slot)? listening = _directors.Hotkeys.Listening;
            if (listening == null || listening.Value.Action != refused) return;
            view.Caps[listening.Value.Slot].tooltip =
                "In use — " + Registry.Label(HotkeyDirector.KeyOf(owner));
        }

        void OnSettingsTabChanged(SettingsTab tab)
        {
            foreach (var entry in _settingTabs)
            {
                bool on = entry.Key == tab;
                entry.Value.EnableInClassList("tab--on", on);
                entry.Value.EnableInClassList("tab--off", !on);
            }
            if (tab == SettingsTab.Graphics && _resolutionDropdown == null)
            {
                BuildResolutionRow();
                OnResolutionChanged();
            }
            _interfaceSection.style.display =
                tab == SettingsTab.Interface ? DisplayStyle.Flex : DisplayStyle.None;
            _graphicsSection.style.display =
                tab == SettingsTab.Graphics ? DisplayStyle.Flex : DisplayStyle.None;
            _audioSection.style.display =
                tab == SettingsTab.Audio ? DisplayStyle.Flex : DisplayStyle.None;
            _keysSection.style.display =
                tab == SettingsTab.Keys ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void OnUiScaleChanged(int percent)
        {
            LightRung(_scaleRungs, percent);
            ApplyUiScale(percent);
        }

        void OnCameraSpeedChanged(int percent) => LightRung(_cameraRungs, percent);

        /// <summary>
        /// A number ladder moved: light its rung, and re-answer the one question a ladder can ask
        /// of another.
        ///
        /// <para>No text is built here — every rung's label was composed once, as the row was
        /// constructed — so a lever costs nothing after the frame it is thrown on (ADR 0003, F1).</para>
        /// </summary>
        void OnLadderChanged(GraphicsLadder ladder)
        {
            if (_directors == null) return;
            if (_ladderRungs.TryGetValue(ladder, out Dictionary<int, Label>? rungs))
                LightRung(rungs, _directors.Settings.Value(ladder));

            if (ladder == GraphicsLadder.VSync) RefreshFrameCapRow();
        }

        /// <summary>
        /// The frame cap is dead behind VSync, and the panel says so rather than letting a player
        /// set 144 and get 60.
        ///
        /// <para>Unity ignores <c>Application.targetFrameRate</c> whenever <c>vSyncCount</c> is
        /// above zero. The rule itself is <c>SettingsDirector.FrameCapIsLive</c>, where the fast
        /// tier can hold it; this is only the greying.</para>
        /// </summary>
        void RefreshFrameCapRow()
        {
            if (_directors == null) return;
            if (!_ladderViews.TryGetValue(GraphicsLadder.FrameCap, out LadderView cap)) return;

            bool live = _directors.Settings.FrameCapIsLive;
            cap.SetLive(live);
            cap.Row.tooltip = live ? RowCostOf(GraphicsLadder.FrameCap) : "Paced by VSync";
        }

        void OnResolutionChanged()
        {
            if (_directors == null) return;
            if (_resolutionDropdown == null) BuildResolutionRow();
            SettingsDirector.Mode res = _directors.Settings.Resolution;
            if (res.Width > 0 && _resolutionDropdown != null)
                _resolutionDropdown.SetValueWithoutNotify(res.Width + "\u00d7" + res.Height);
        }

        void OnBuildLayoutChanged(BuildPaletteLayout layout) => LightRung(_layoutRungs, layout);

        void OnBusDbChanged(SettingsBus bus)
        {
            if (_directors == null || !_busFaders.TryGetValue(bus, out FaderView? view)) return;
            int at = _directors.Settings.BusDb(bus);

            // The thumb stands at the director's whole dB — on its seat on the track, which
            // is unity's centre when the value is unity — whether the move came from this
            // drag (the write-back lands on the rounded figure the drag already offered) or
            // from a seed laid in before the shell attached. The value-changed callback this
            // can raise finds nothing left to ask for and stops there.
            float seat = SettingsDirector.TrackOf(at);
            if (!Mathf.Approximately(view.Fader.value, seat)) view.Fader.value = seat;

            string text = VolumeText(at);
            // The decibel figures are figures and set in the mono face; "Mute" is a word,
            // and a word in the mono face is a word pretending to be a number.
            HudText.Apply(view.Value, HudTextRole.Body, numeric: text != "Mute");
            HudText.Set(view.Value, text, HudTextRole.Body);
        }

        /// <summary>
        /// One session row of the settings panel: Save, Load, Quit to main menu, Exit game.
        ///
        /// <para>Identical in construction to the start screen's rows, because they are rows of
        /// the same table — the only difference is which context <see cref="SessionCommands"/> was
        /// asked for. That is the whole of what "the two surfaces cannot drift apart" buys.</para>
        /// </summary>
        VisualElement SessionRow(SessionCommand command, bool separated)
        {
            var row = new VisualElement();
            row.AddToClassList("settings__row");
            if (separated) row.AddToClassList("settings__exit");

            var icon = new IconBadge(command.Key, IconBadge.RowSize);
            icon.Inherit(HudTokens.TextMeta);
            row.Add(icon);

            Label label = HudText.Make(command.Label, HudTextRole.Row, ussClass: "settings__label");
            row.Add(label);
            row.tooltip = SessionTooltip(command);
            row.RegisterCallback<ClickEvent>(_ => _directors?.Settings.Request(command.Key));

            _sessionRows[command.Key] = (row, label);
            if (command.Key == SettingsDirector.ExitKey)
            {
                _exitRow = row;
                _exitLabel = label;
            }
            return row;
        }

        readonly Dictionary<string, (VisualElement Row, Label Label)> _sessionRows =
            new Dictionary<string, (VisualElement, Label)>();

        /// <summary>
        /// A row that has something to say instead of its own name — today only Load, saying
        /// there is nothing to open.
        ///
        /// <para><b>Here rather than written straight into the label</b>, because
        /// <see cref="SettingsDirector.Request"/> raises <c>RowRequested</c> and <i>then</i>
        /// <c>ExitChanged</c>, so anything a row handler writes on to a label is overwritten by
        /// <see cref="OnExitChanged"/> one call later. A note is state the refresh knows about,
        /// which is the only kind that survives it.</para>
        ///
        /// <para>Cleared the moment any row is armed, and when the panel closes: a note is an
        /// answer to the press that produced it and it should not be sitting there next time the
        /// panel is opened.</para>
        /// </summary>
        readonly Dictionary<string, string> _sessionNotes = new Dictionary<string, string>();

        /// <summary>Say something on a session row in place of its name, until the next press.</summary>
        internal void NoteOnSessionRow(string key, string note)
        {
            _sessionNotes[key] = note;
            OnExitChanged();
        }

        /// <summary>Take every note down. Called when the settings panel opens or closes.</summary>
        internal void ClearSessionNotes()
        {
            if (_sessionNotes.Count == 0) return;
            _sessionNotes.Clear();
            OnExitChanged();
        }

        /// <summary>
        /// What a session row says on hover. A literal, like every other tooltip in this shell —
        /// the registry emits labels and not tooltips, and a tooltip is a sentence about what
        /// happens rather than a name the owner maintains in the CSV.
        /// </summary>
        internal static string SessionTooltip(SessionCommand command)
        {
            string what = command.Key switch
            {
                SessionCommands.SaveKey => "Writes this colony to a file under Saves",
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
        ///
        /// <para>A loop over all four rather than a line about the exit row, because the director
        /// holds one armed key across the whole panel: pressing Load while Quit is armed has to
        /// stand Quit down, and a refresh that only knew about one row would leave the other
        /// saying "Click again" about a question nobody is asking any more.</para>
        /// </summary>
        void OnExitChanged()
        {
            if (_directors == null) return;
            string? armed = _directors.Settings.ArmedRow;

            // Arming anything is a new question, so whatever a row was saying about the last one
            // goes. Before the loop, so the row being armed cannot keep its own note.
            if (armed != null) _sessionNotes.Clear();

            foreach (KeyValuePair<string, (VisualElement Row, Label Label)> pair in _sessionRows)
            {
                bool on = pair.Key == armed;
                pair.Value.Row.EnableInClassList("row--armed", on);

                string text = on ? "Click again to confirm"
                    : _sessionNotes.TryGetValue(pair.Key, out string? note) ? note
                    : Registry.Label(pair.Key);
                HudText.Set(pair.Value.Label, text, HudTextRole.Row);
            }
        }

        void OnBindingChanged(HotkeyAction action)
        {
            RefreshKeyCaps();

            // The command bar's Build cap is a legend of a real binding: if the player moves
            // it, the legend moves with it or it is a lie on the bar.
            if (action != HotkeyAction.BuildPalette || _directors == null || _buildCap == null) return;
            string cap = HotkeyDirector.Display(_directors.Hotkeys.Key(HotkeyAction.BuildPalette, 0));
            HudText.Set(_buildCap, cap, HudTextRole.Hotkey);
            if (_buildItem != null) _buildItem.tooltip = _buildTooltipLabel + " — " + cap;
        }

        /// <summary>
        /// Draw the HUD larger or smaller by telling its panel a different reference resolution.
        ///
        /// <para><b>On a copy of the asset, never the asset itself.</b> <c>PanelSettings</c> is a
        /// file on disk, and writing to it from play mode in the editor leaves the change behind
        /// after the session ends — permanently, and invisibly, in a committed asset. This project
        /// has met that exact trap once already with the sky material, which is copied before it
        /// is tinted for the same reason.</para>
        ///
        /// <para>Everything else follows for free: the panel re-lays-out, the shell's own
        /// <c>GeometryChangedEvent</c> fires, the colonist strip re-clamps to what fits and the
        /// command bar reflows its tail into Menu. The layout is anchored rather than sized, so a
        /// smaller canvas is a case it already handles and is already tested at.</para>
        /// </summary>
        public void ApplyUiScale(int percent)
        {
            if (_panelCopy == null) EnsurePanelCopy(GetComponent<UIDocument>());
            if (_panelCopy == null) return;

            (int width, int height) = HudLayout.ReferenceFor(percent);
            _panelCopy.referenceResolution = new Vector2Int(width, height);
        }

        void EnsurePanelCopy(UIDocument? doc)
        {
            if (_panelCopy != null || doc == null || doc.panelSettings == null) return;

            _panelCopy = Instantiate(doc.panelSettings);
            _panelCopy.name = doc.panelSettings.name + " (scaled)";
            _panelCopy.hideFlags = HideFlags.HideAndDontSave;
            doc.panelSettings = _panelCopy;
        }

        void OnSettingsChanged()
        {
            bool open = _directors != null && _directors.Settings.Open;
            _settingsPanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open)
            {
                ToggleMenu(false);
                _directors?.Debug.SetOpen(false);
                if (_resolutionDropdown == null)
                {
                    BuildResolutionRow();
                    OnResolutionChanged();
                }
            }
        }

        void OnSettingChanged(GraphicsOption option)
        {
            if (_directors == null) return;
            if (!_settingRows.TryGetValue(option, out VisualElement? row)) return;
            row.EnableInClassList("settings__row--on", _directors.Settings.IsOn(option));
        }

        // ============================================================ formatting

        /// <summary>Thousandths to a percentage of a bar's width.</summary>
        static float Percent(int thousandths) => Mathf.Clamp(thousandths, 0, 1000) / 10f;
    }
}
