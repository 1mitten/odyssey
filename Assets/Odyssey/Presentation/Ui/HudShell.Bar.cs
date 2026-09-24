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

            // The Inventory and Research items are washed while their tabs are open, as Build is
            // while the palette is.
            if (command.Key == HudCommands.InventoryKey) _inventoryItem = item;
            if (command.Key == HudCommands.ResearchKey) _researchItem = item;

            // Build is the one cap on the bar that names a binding rather than a promise: it
            // follows the binding map when the player moves the key.
            // The Animals item is washed while its tab is open, as Build is while the palette is.
            if (command.Key == HudCommands.AnimalsKey) _animalsItem = item;

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
            else if (key == HudCommands.WorkKey) _directors?.Work.Toggle();
            else if (key == HudCommands.InventoryKey) _directors?.Inventory.Toggle();
            else if (key == HudCommands.ResearchKey) _directors?.Research.Toggle();
            else if (key == HudCommands.AnimalsKey) _directors?.Animals.Toggle();
            else if (key == HudCommands.AlmanacKey) ToggleAlmanac();
            else if (key == HudCommands.MenuKey) ToggleMenu();
        }

        /// <summary>
        /// The hotkeys on the bar that are live — B for the palette, <b>F1 for the Work
        /// tab</b>, and <b>F9 for the Almanac</b> — read through the binding map so the panel that
        /// rebinds one and the key that opens it can never disagree.
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

            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.WorkTab))
                _directors?.Work.Toggle();

            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.AnimalsTab))
                _directors?.Animals.Toggle();
            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.InventoryTab))
                _directors?.Inventory.Toggle();

            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.ResearchTab))
                _directors?.Research.Toggle();

            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.Almanac))
                ToggleAlmanac();

            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.Draft))
                ToggleDraft();
        }

        /// <summary>
        /// Draft or release the selection (design 33 §2f): the key and the pane's button both come
        /// here, and <see cref="OrderModel.ToggleDraft"/> is the rule. Submitted rather than
        /// applied, and the bootstrap lands a paused world's orders the same frame, so a fight can
        /// be set up with the clock stopped.
        /// </summary>
        void ToggleDraft()
        {
            var world = _boot?.World;
            if (world == null || _directors == null) return;

            _draftOrders.Clear();
            OrderModel.ToggleDraft(_directors.Selection.Pawns, world.Views.Current, _draftOrders);
            for (int i = 0; i < _draftOrders.Count; i++) world.Intents.Submit(_draftOrders[i]);
            _draftOrders.Clear();
        }

        // Scratch for ToggleDraft, emptied inside the call.
        readonly System.Collections.Generic.List<Intent> _draftOrders = new System.Collections.Generic.List<Intent>();

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

        const string PowerOverlayKey = "ui.overlay.power";

        /// <summary>The Menu's power row, lit while the overlay is on.</summary>
        VisualElement? _powerOverlayRow;

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
                var icon = new IconBadge(key, IconBadge.BarSize);
                icon.Inherit(HudTokens.TextMeta);
                overlay.Add(icon);
                overlay.Add(HudText.Make(Registry.Label(key), HudTextRole.Row, ussClass: "menu__label"));

                // Power is the first channel that renders (design 32 §9): every conduit, shown
                // whatever is armed, until the row is pressed again. The rest stay disabled with
                // their reason, which is the catalogue's rule for a control that is not ready.
                if (key == PowerOverlayKey)
                {
                    overlay.tooltip = Registry.Label(key) + " — show every conduit, whatever is armed";
                    _powerOverlayRow = overlay;
                    overlay.RegisterCallback<ClickEvent>(_ =>
                    {
                        if (_directors == null) return;
                        _directors.Overlays.TogglePower();
                        overlay.EnableInClassList("menu__row--on", _directors.Overlays.PowerVisible);
                        MarkViews();
                    });
                }
                else
                {
                    overlay.AddToClassList("menu__row--off");
                    overlay.tooltip = Registry.Label(key) + " — overlay channels arrive with M4";
                }
                _menuPopup.Add(overlay);
            }

            _menuPopup.Add(HudText.Make("Game", HudTextRole.PanelLabel, ussClass: "menu__section"));

            var settings = new VisualElement();
            settings.AddToClassList("menu__row");
            settings.Add(HudText.Make("Settings", HudTextRole.Row, ussClass: "menu__label"));
            settings.Add(HudText.Make("Esc", HudTextRole.Hotkey, ussClass: "menu__key"));
            settings.tooltip = "Settings — Esc";
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

        // ============================================================ B17 settings (the rest is HudShell.Settings.cs)

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

        // ============================================================ formatting

        /// <summary>Thousandths to a percentage of a bar's width.</summary>
        static float Percent(int thousandths) => Mathf.Clamp(thousandths, 0, 1000) / 10f;
    }
}
