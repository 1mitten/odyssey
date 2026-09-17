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
            _hud.Add(_barRow);

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
            if (command.Primary) icon.Inherit(HudTokens.OnAccent);
            item.Add(icon);

            Label label = HudText.Make(command.Label, HudTextRole.Row, ussClass: "cmd__label");
            if (command.Primary) label.AddToClassList("cmd__label--primary");
            item.Add(label);

            // Every item shows its key. An acceptance criterion, and the reason the bar is set in
            // a narrow face: eleven labelled items with their caps have to cross the screen.
            item.Add(HudText.Make(command.Hotkey, HudTextRole.Hotkey, ussClass: "cmd__key"));

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
        /// The two hotkeys on the bar that are live. The rest are legends on controls whose
        /// systems do not exist, and they deliberately avoid every key the game already uses —
        /// M, C and X arm the designate tools, R and F move the slice, V cycles visibility,
        /// Space and 1 to 3 are the clock and Home recentres.
        /// </summary>
        void ReadBarKeys()
        {
            var keys = UnityEngine.InputSystem.Keyboard.current;
            if (keys == null) return;
            if (keys.bKey.wasPressedThisFrame) SetBuildPalette(!BuildPaletteOpen);
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
            settings.tooltip = "Graphics settings. None of it is in the save.";
            settings.RegisterCallback<ClickEvent>(_ =>
            {
                ToggleMenu(false);
                _directors?.Settings.SetOpen(true);
            });
            _menuPopup.Add(settings);

            _hud.Add(_menuPopup);
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

        // ============================================================ A7 build palette

        /// <summary>
        /// Categories in catalogue order, each with a few of its tools. Every icon key exists in
        /// the registry; every control is display-only until placement tools land in M3.
        /// </summary>
        static readonly (string key, string label, string[] tools)[] BuildCategories =
        {
            ("ui.arch.category.structure", "Structure", new[] { "ui.arch.tool.wall", "ui.arch.tool.door", "ui.arch.tool.stair", "ui.arch.tool.ladder", "ui.arch.tool.roof", "ui.arch.tool.reclaim" }),
            ("ui.arch.category.orders", "Orders", new[] { "ui.arch.tool.mine", "ui.arch.tool.deconstruct", "ui.arch.tool.harvest", "ui.arch.tool.forbid", "ui.arch.tool.clearrubble" }),
            ("ui.arch.category.zones", "Zones", new[] { "ui.arch.tool.stockpile", "ui.arch.tool.growzone", "ui.arch.tool.dumping" }),
            ("ui.arch.category.production", "Production", new[] { "ui.arch.tool.fabricator", "ui.arch.tool.galley", "ui.arch.tool.reclaimer", "ui.arch.tool.bench" }),
            ("ui.arch.category.furniture", "Furniture", new[] { "ui.arch.tool.bunk", "ui.arch.tool.table", "ui.arch.tool.lamp", "ui.arch.tool.shelf" }),
            ("ui.arch.category.power", "Power", new[] { "ui.arch.tool.conduit", "ui.arch.tool.battery", "ui.arch.tool.generator", "ui.arch.tool.reactor" }),
            ("ui.arch.category.security", "Security", new[] { "ui.arch.tool.turret", "ui.arch.tool.trap", "ui.arch.tool.barricade" }),
            ("ui.arch.category.salvage", "Salvage", new[] { "ui.arch.tool.salvage", "ui.arch.tool.deconstruct", "ui.arch.tool.reclaim" }),
            ("ui.arch.category.floors", "Floors", new[] { "ui.arch.tool.deckplate", "ui.arch.tool.grating", "ui.arch.tool.tile" }),
            ("ui.arch.category.recreation", "Recreation", new[] { "ui.arch.tool.gamestable", "ui.arch.tool.viewscreen", "ui.arch.tool.planter" }),
        };

        /// <summary>
        /// The palette, opened by the Build command and closed by Escape.
        ///
        /// <para>It used to be a column pinned to the left edge and permanently open, competing
        /// with the stores panel for a column that could not hold both. As a transient panel over
        /// the board it costs nothing when it is shut, which is most of the time.</para>
        /// </summary>
        void BuildPalette()
        {
            _buildPanel = Popover("build", "Build", () => SetBuildPalette(false), "build");

            var cats = new VisualElement();
            cats.AddToClassList("build__cats");

            // The categories scroll: ten categories and their tools will outgrow any panel that
            // has to share a screen, and a list too long for its panel should scroll rather than
            // quietly lose its end.
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("build__scroll");
            for (int i = 0; i < BuildCategories.Length; i++)
            {
                var (key, label, _) = BuildCategories[i];
                int index = i;
                VisualElement chip = PaletteChip(key, label);
                chip.tooltip = label + " — placement tools arrive with M3";
                chip.RegisterCallback<ClickEvent>(_ => SelectBuildCategory(index));
                cats.Add(chip);
            }
            scroll.Add(cats);
            _buildPanel.Add(scroll);

            _buildTools = new VisualElement();
            _buildTools.AddToClassList("build__tools");
            _buildPanel.Add(_buildTools);

            // Open on the first category rather than on an empty second group. The palette's whole
            // shape is two groups, and one of them showing nothing until the player guesses that
            // the chips above are clickable is a panel that has to be explained.
            SelectBuildCategory(0);

            _hud.Add(_buildPanel);
        }

        static VisualElement PaletteChip(string key, string label)
        {
            var chip = new VisualElement();
            chip.AddToClassList("chip");
            var icon = new IconBadge(key, IconBadge.BarSize);
            icon.Inherit(HudTokens.TextMeta);
            chip.Add(icon);
            chip.Add(HudText.Make(label, HudTextRole.Row, ussClass: "chip__label"));
            return chip;
        }

        void SetBuildPalette(bool open)
        {
            // One popover at a time. Two raised from the same bar would overlap each other over
            // the buttons that raised them, and the player would have no way to tell which of the
            // two the Escape they are about to press belongs to.
            if (open) ToggleMenu(false);

            _buildPanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (_barItems.Count > 0)
            {
                _barItems[0].EnableInClassList("cmd--on", open);
                if (open) PlacePopover(_buildPanel, _barItems[0]);
            }
        }

        void SelectBuildCategory(int index)
        {
            if (_buildCategory == index) return;
            _buildCategory = index;

            _buildTools.Clear();
            foreach (string tool in BuildCategories[index].tools)
            {
                // The label is the registry's, never a two-letter sigil derived from the key: the
                // acceptance criteria strike out every three-letter placeholder on the screen.
                VisualElement chip = PaletteChip(tool, Registry.Label(tool));
                chip.AddToClassList("chip--off");
                chip.tooltip = Registry.Label(tool) + " — placement tools arrive with M3";
                _buildTools.Add(chip);
            }
        }

        // ============================================================ B17 settings

        /// <summary>
        /// The settings panel: two sections behind a tab strip, opened with Escape or from Menu.
        ///
        /// <para><b>It is not a modal.</b> There is no scrim and nothing is blocked: the world
        /// runs, the camera orbits and the clock ticks while it is open, because the only reason
        /// to have the panel is to watch the board change as a lever moves. Clicks that land on it
        /// already stop at the panel edge through <see cref="PointOverUi"/>.</para>
        ///
        /// <para><b>Interface before Graphics</b>, because the first thing a player wants from a
        /// settings panel on a large monitor is to make the type bigger, and because that is the
        /// one setting here that changes the panel they are looking at while they look at it.</para>
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
            foreach (SettingsTab tab in new[] { SettingsTab.Interface, SettingsTab.Graphics })
            {
                string key = tab == SettingsTab.Interface
                    ? SettingsDirector.InterfaceKey
                    : SettingsDirector.GraphicsKey;
                Label chip = HudText.Make(Registry.Label(key), HudTextRole.Body, ussClass: "tab");
                SettingsTab captured = tab;
                chip.RegisterCallback<ClickEvent>(_ => _directors?.Settings.SetTab(captured));
                _settingTabs[tab] = chip;
                tabs.Add(chip);
            }
            _settingsPanel.Add(tabs);

            BuildInterfaceSection();
            BuildGraphicsSection();

            // The director opens on Interface, and the shell may never attach to a director at all
            // in a harness that builds no world. Showing both sections at once is not a state
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
        void BuildInterfaceSection()
        {
            _interfaceSection = new VisualElement();
            _interfaceSection.AddToClassList("settings__body");

            var row = new VisualElement();
            row.AddToClassList("settings__row");
            row.AddToClassList("settings__row--static");
            var icon = new IconBadge(SettingsDirector.UiScaleKey, IconBadge.RowSize);
            icon.Inherit(HudTokens.TextMeta);
            row.Add(icon);
            row.Add(HudText.Make(Registry.Label(SettingsDirector.UiScaleKey), HudTextRole.Row,
                ussClass: "settings__label"));
            _interfaceSection.Add(row);

            var ladder = new VisualElement();
            ladder.AddToClassList("settings__ladder");
            foreach (int percent in SettingsDirector.UiScales)
            {
                // A percentage is a figure, so it is set in the mono face like every other figure
                // on this screen.
                Label rung = HudText.Make(percent + "%", HudTextRole.Body, numeric: true, "rung");
                rung.tooltip = percent == 100
                    ? "The size the interface is designed at"
                    : percent < 100
                        ? "Smaller type, less of the board hidden"
                        : "Larger type, more of the board hidden";
                int captured = percent;
                rung.RegisterCallback<ClickEvent>(_ => _directors?.Settings.SetUiScale(captured));
                _scaleRungs[percent] = rung;
                ladder.Add(rung);
            }
            _interfaceSection.Add(ladder);
            _settingsPanel.Add(_interfaceSection);
        }

        void BuildGraphicsSection()
        {
            _graphicsSection = new VisualElement();
            _graphicsSection.AddToClassList("settings__body");

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
                _graphicsSection.Add(row);
            }

            _settingsPanel.Add(_graphicsSection);
        }

        void OnSettingsTabChanged(SettingsTab tab)
        {
            foreach (var entry in _settingTabs)
            {
                bool on = entry.Key == tab;
                entry.Value.EnableInClassList("tab--on", on);
                entry.Value.EnableInClassList("tab--off", !on);
            }
            _interfaceSection.style.display =
                tab == SettingsTab.Interface ? DisplayStyle.Flex : DisplayStyle.None;
            _graphicsSection.style.display =
                tab == SettingsTab.Graphics ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void OnUiScaleChanged(int percent)
        {
            foreach (var entry in _scaleRungs)
                entry.Value.EnableInClassList("rung--on", entry.Key == percent);
            ApplyUiScale(percent);
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
            if (open) ToggleMenu(false);
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
