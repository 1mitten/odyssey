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
            Label capLabel = HudText.Make(command.Hotkey, HudTextRole.Hotkey, ussClass: "cmd__key");
            item.Add(capLabel);

            // Build is the one cap on the bar that names a binding rather than a promise: it
            // follows the binding map when the player moves the key.
            if (command.Key == HudCommands.BuildKey)
            {
                _buildCap = capLabel;
                _buildItem = item;
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
            HotkeyDirector hotkeys = _directors?.Hotkeys ?? (_hotkeysFallback ??= new HotkeyDirector());

            // A key offered to a slot in the settings panel belongs to the rebind, not to
            // the palette it might be being bound to.
            if (hotkeys.Listening != null) return;

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
        /// Categories in catalogue order, each with a few of its tools — <see cref="PaletteTools"/>
        /// owns the table, for the reason <see cref="HudCommands"/> owns the command bar's: it is
        /// data, and the fast tier can see that assembly and cannot see this one.
        /// </summary>
        static (string key, string label, string[] tools)[] BuildCategories => PaletteTools.Categories;

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

            // Under the tools, and only while something made of a material is armed: a
            // material is a property of the order being given, not a category of its own.
            _buildMaterials = new VisualElement();
            _buildMaterials.AddToClassList("build__materials");
            _buildMaterials.style.display = DisplayStyle.None;
            _buildPanel.Add(_buildMaterials);

            // Under everything, always: the tools that belong to no category. Built once rather
            // than rebuilt with the category, because the whole point of them is that they do not
            // move when the category does.
            BuildPinnedRow();

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

        /// <summary>What a wall may be made of, in the order the player meets them.</summary>
        static readonly (int stuff, string key)[] BuildMaterials =
        {
            (StuffHandle.Wood, "ui.res.wood"),
            (StuffHandle.Stone, "ui.res.stone"),
        };

        /// <summary>
        /// The material row, shown only while a thing that is made of something is armed.
        /// </summary>
        void BuildMaterialRow()
        {
            _buildMaterials.Clear();
            bool wanted = _directors != null && _directors.Designate.Tool == DesignateTool.Build;
            _buildMaterials.style.display = wanted ? DisplayStyle.Flex : DisplayStyle.None;
            if (!wanted) return;

            // The class matters: without one this caption inherited nothing and took the imported
            // runtime theme's dark ink, so it had been invisible on the palette since the material
            // row was built. `.hud` now carries a default ink for exactly that reason, and this
            // says the quieter thing a caption should say beside the chips it labels.
            _buildMaterials.Add(HudText.Make("MADE OF", HudTextRole.Meta, ussClass: "panel__label"));
            foreach (var (stuff, key) in BuildMaterials)
            {
                VisualElement chip = PaletteChip(key, Registry.Label(key));
                chip.EnableInClassList("chip--on", _directors!.Designate.Stuff == stuff);
                int choice = stuff;
                chip.RegisterCallback<ClickEvent>(_ =>
                {
                    if (_directors == null) return;
                    _directors.Designate.ChooseStuff(choice);
                    BuildMaterialRow();
                });
                _buildMaterials.Add(chip);
            }
        }

        /// <summary>
        /// The row that does not change with the category — Cancel, today, and only Cancel.
        ///
        /// <para>A player wants it while they are holding another tool, which is exactly when the
        /// category row above is showing something else. See <see cref="PaletteTools.Pinned"/> for
        /// why it is here instead of filed under Orders.</para>
        /// </summary>
        void BuildPinnedRow()
        {
            _buildPinned = new VisualElement();
            _buildPinned.AddToClassList("build__pinned");

            foreach (string key in PaletteTools.Pinned)
            {
                if (!PaletteTools.TryGet(key, out PaletteTool live)) continue;
                VisualElement chip = PaletteChip(key, Registry.Label(key));
                chip.tooltip = Registry.Label(key) + " — drag a box over the world";
                chip.RegisterCallback<ClickEvent>(_ =>
                {
                    if (_directors == null) return;
                    live.Arm(_directors.Designate);
                    MarkArmedTool();
                    // Arming an order puts any build tool down, so the material row goes with it.
                    BuildMaterialRow();
                });
                _buildPinned.Add(chip);
            }

            _buildPanel.Add(_buildPinned);
        }

        /// <summary>
        /// Light the chip for whatever is armed, so the palette and the world agree.
        ///
        /// <para>Each chip asks its own row whether it is the one being held. This used to be a
        /// chain of key comparisons with one branch per live tool, written out a hundred lines away
        /// from the table that armed them — two lists of the same tools, and when they drifted the
        /// symptom was not a compile error but a chip that arms a tool and never lights, which
        /// reads to a player as the click having missed.</para>
        /// </summary>
        void MarkArmedTool()
        {
            if (_directors == null || _buildCategory < 0) return;
            DesignateDirector armed = _directors.Designate;

            int at = 0;
            foreach (string tool in BuildCategories[_buildCategory].tools)
            {
                bool on = PaletteTools.TryGet(tool, out PaletteTool live) && live.IsArmed(armed);
                if (at < _buildTools.childCount) _buildTools[at].EnableInClassList("chip--on", on);
                at++;
            }

            // The pinned row lights the same way. It is a separate walk because it is a separate
            // row, and not marking it would leave the one chip that is always visible as the one
            // chip that never says whether it is held.
            at = 0;
            foreach (string tool in PaletteTools.Pinned)
            {
                bool on = PaletteTools.TryGet(tool, out PaletteTool live) && live.IsArmed(armed);
                if (at < _buildPinned.childCount) _buildPinned[at].EnableInClassList("chip--on", on);
                at++;
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
                if (PaletteTools.TryGet(tool, out PaletteTool live))
                {
                    chip.tooltip = Registry.Label(tool) + " — drag a box over the world";
                    chip.RegisterCallback<ClickEvent>(_ =>
                    {
                        if (_directors == null) return;
                        live.Arm(_directors.Designate);
                        MarkArmedTool();
                        // Only a thing made of something has a material row to refresh; the row
                        // asks the table rather than this method knowing which tool that is.
                        if (live.WantsMaterial) BuildMaterialRow();
                    });
                }
                else
                {
                    chip.AddToClassList("chip--off");
                    chip.tooltip = Registry.Label(tool) + " — placement tools arrive with M3";
                }

                _buildTools.Add(chip);
            }

            BuildMaterialRow();
            MarkArmedTool();
        }

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

            BuildInterfaceSection();
            BuildGraphicsSection();
            BuildAudioSection();
            BuildKeysSection();

            // The way out, pinned under the tabs rather than living in one of them: it is not a
            // setting, and the game menu it will one day belong to (B18) does not exist yet.
            // Two clicks, because nothing is saved and a settings panel is a place a player
            // reaches past for the close button.
            // Every session row, from the one table the start screen also builds from
            // (SessionCommands). Four of them since U38: Save, Load, Quit to main menu, and the
            // exit row that has been here since this panel had a way out at all. The hairline that
            // sets them apart from the settings above belongs to the first of them, not to the
            // exit row it used to belong to.
            bool first = true;
            foreach (SessionCommand command in SessionCommands.For(SessionContext.InGame))
            {
                VisualElement row = SessionRow(command, separated: first);
                first = false;
                _settingsPanel.Add(row);
            }

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

            BuildCameraSpeedRow();
            BuildDeveloperRow();

            _settingsPanel.Add(_interfaceSection);
        }

        /// <summary>
        /// The camera-speed ladder: how fast the rig pans and zooms, as a share of the speed
        /// it was tuned at. The same ladder idiom as the interface scale, for the same
        /// reason — a handful of honest answers rather than a knob.
        /// </summary>
        void BuildCameraSpeedRow()
        {
            var row = new VisualElement();
            row.AddToClassList("settings__row");
            row.AddToClassList("settings__row--static");
            var icon = new IconBadge(SettingsDirector.CamSpeedKey, IconBadge.RowSize);
            icon.Inherit(HudTokens.TextMeta);
            row.Add(icon);
            row.Add(HudText.Make(Registry.Label(SettingsDirector.CamSpeedKey), HudTextRole.Row,
                ussClass: "settings__label"));
            _interfaceSection.Add(row);

            var ladder = new VisualElement();
            ladder.AddToClassList("settings__ladder");
            foreach (int percent in SettingsDirector.CameraSpeeds)
            {
                // A multiplier is a figure, so: mono, like every other figure on this screen.
                Label rung = HudText.Make($"{percent / 100f:0.#}×", HudTextRole.Body,
                    numeric: true, "rung");
                rung.tooltip = percent == 100
                    ? "The speed the camera was tuned at"
                    : percent < 100
                        ? "Slower, for fine placement"
                        : "Faster, for crossing the map";
                int captured = percent;
                rung.RegisterCallback<ClickEvent>(_ => _directors?.Settings.SetCameraSpeed(captured));
                _cameraRungs[percent] = rung;
                ladder.Add(rung);
            }
            _interfaceSection.Add(ladder);
        }

        /// <summary>
        /// The developer readout: the one toggle in Interface that is about the HUD's own
        /// drawing rather than the world. A pip row like the graphics ones, seeded from
        /// whatever state the backquote key left the overlay in.
        /// </summary>
        void BuildDeveloperRow()
        {
            _developerRow = new VisualElement();
            _developerRow.AddToClassList("settings__row");
            var icon = new IconBadge(SettingsDirector.DeveloperKey, IconBadge.RowSize);
            icon.Inherit(HudTokens.TextMeta);
            _developerRow.Add(icon);
            _developerRow.Add(HudText.Make(Registry.Label(SettingsDirector.DeveloperKey),
                HudTextRole.Row, ussClass: "settings__label"));

            var pip = new VisualElement { pickingMode = PickingMode.Ignore };
            pip.AddToClassList("settings__pip");
            _developerRow.Add(pip);

            _developerRow.tooltip = "The frame-time readout. Also the ` key, and kept between sessions";
            _developerRow.RegisterCallback<ClickEvent>(_ =>
                _directors?.Settings.SetDeveloperOverlay(!_directors.Settings.DeveloperOverlay));
            _interfaceSection.Add(_developerRow);
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
        void BuildAudioSection()
        {
            _audioSection = new VisualElement();
            _audioSection.AddToClassList("settings__body");

            foreach (SettingsBus bus in SettingsDirector.Buses)
            {
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
                _audioSection.Add(row);

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
                _audioSection.Add(fader);

                _busFaders[bus] = new FaderView { Fader = fader, Value = value };
            }

            _settingsPanel.Add(_audioSection);
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
                HotkeyAction.BuildPalette, HotkeyAction.DeveloperOverlay,
            }),
        };

        /// <summary>
        /// The Keys section: every action the game reads a key for, grouped, one or two caps
        /// per row. Click a cap to change it; the next key pressed is offered to that slot,
        /// and Escape backs out of the wait without unwinding anything under it.
        /// </summary>
        void BuildKeysSection()
        {
            _keysSection = new VisualElement();
            _keysSection.AddToClassList("settings__body");

            foreach ((string header, HotkeyAction[] actions) in KeyGroups)
            {
                _keysSection.Add(HudText.Make(header, HudTextRole.Meta, ussClass: "settings__section"));

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
                    _keysSection.Add(row);
                }
            }

            Label reset = HudText.Make(Registry.Label(HotkeyDirector.ResetKey), HudTextRole.Body,
                ussClass: "rung settings__reset");
            reset.tooltip = "Every action goes back to the key it shipped with";
            reset.RegisterCallback<ClickEvent>(_ => _directors?.Hotkeys.ResetKeys());
            _keysSection.Add(reset);

            _settingsPanel.Add(_keysSection);
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
            foreach (var entry in _scaleRungs)
                entry.Value.EnableInClassList("rung--on", entry.Key == percent);
            ApplyUiScale(percent);
        }

        void OnCameraSpeedChanged(int percent)
        {
            foreach (var entry in _cameraRungs)
                entry.Value.EnableInClassList("rung--on", entry.Key == percent);
        }

        void OnDeveloperOverlayChanged()
        {
            if (_directors == null) return;
            _developerRow.EnableInClassList("settings__row--on", _directors.Settings.DeveloperOverlay);
        }

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

            foreach (KeyValuePair<string, (VisualElement Row, Label Label)> pair in _sessionRows)
            {
                bool on = pair.Key == armed;
                pair.Value.Row.EnableInClassList("row--armed", on);
                HudText.Set(pair.Value.Label,
                    on ? "Click again to confirm" : Registry.Label(pair.Key), HudTextRole.Row);
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
