#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// <see cref="HudShell"/>: the context menu (design 33 §7a) — a small panel at the pointer
    /// raised by a right-click on a thing with more than one answer, a weapon today.
    ///
    /// <para><b>Every row is the model's.</b> Which rows, their words, whether each can be chosen,
    /// why not and exactly what each sends are <see cref="ContextMenuModel"/>'s, tested in the fast
    /// tier; this file draws them and, on a click, hands the row back through
    /// <see cref="ContextMenuModel.Choose"/> — the one door from a row to the world, which sends
    /// nothing for Cancel or a disabled row whatever the view does.</para>
    ///
    /// <para><b>Four things close it</b> (owner, 2026-09-23): Escape (the top rung of
    /// <c>SettingsDirector.Escape</c>, <see cref="EscapeAction.CloseContextMenu"/>),
    /// any mouse press outside it, the camera turning, and a change of selection. A new right-click
    /// elsewhere is a press outside it, so it closes this one and opens its own on the release.</para>
    ///
    /// <para><b>Its width is the stylesheet's</b> (<c>.ctxmenu</c>'s <c>min-width</c>) and never
    /// written from code, so the border-box trap a panel that sets its own width falls into
    /// (CLAUDE.md, <c>WorkGridLayout.PanelOuterWidth</c>) cannot happen here. Its <i>position</i>
    /// is written from code, because only the pointer knows it: <see cref="HudLayout.ContextMenuLeft"/>
    /// and <see cref="HudLayout.ContextMenuTop"/> turn it at the right and bottom edges.</para>
    ///
    /// <para>Built on first use, as the bed picker is, so the smoke test's list of framed regions
    /// the shell builds at start is unchanged.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        VisualElement? _contextMenu;

        /// <summary>The rows the menu on screen was built from, in the order they are drawn.</summary>
        readonly List<ContextMenuRow> _contextRows = new List<ContextMenuRow>();

        /// <summary>Scratch for a chosen row's orders: filled and emptied inside one click.</summary>
        readonly List<Intent> _contextOrders = new List<Intent>();

        /// <summary>The pointer the menu was raised at, in panel coordinates.</summary>
        Vector2 _contextAt;

        /// <summary>The camera's facing when the menu opened; turning it away closes the menu.</summary>
        Quaternion _contextFacing;

        /// <summary>
        /// How far the camera may turn, in degrees, before the menu closes. Above the yaw a
        /// right-click that did not travel can still add while the button is down (a few pixels
        /// at the orbit's quarter-degree a pixel), below the smallest orbit anybody means.
        /// </summary>
        const float ContextMenuOrbitDegrees = 5f;

        /// <summary>Is the context menu on screen? Read by <c>SettingsPresenter</c> for Escape.</summary>
        public bool ContextMenuOpen =>
            _contextMenu != null && _contextMenu.style.display.value == DisplayStyle.Flex;

        /// <summary>
        /// Raise the menu at <paramref name="screenPosition"/> (the mouse's own, bottom-left
        /// origin) with these rows, replacing any menu already up. Called by
        /// <c>SelectionPresenter.Order</c> when <see cref="OrderModel.RightClick"/> answers with
        /// rows rather than orders.
        /// </summary>
        public void OpenContextMenu(IReadOnlyList<ContextMenuRow> rows, Vector2 screenPosition)
        {
            if (_hud == null || _hud.panel == null || rows.Count == 0) return;

            if (_contextMenu == null)
            {
                _contextMenu = Panel("contextmenu", "ctxmenu");
                _contextMenu.style.display = DisplayStyle.None;
                _worldUi.Add(_contextMenu);

                // Placed again when its size changes, which is the only moment its size is
                // knowable: a panel shown this frame has not been laid out (PlacePopover's lesson).
                _contextMenu.RegisterCallback<GeometryChangedEvent>(_ => PlaceContextMenu());
            }

            // The panels a right-click could have left open over the board go first, so the
            // menu is never drawn under the Build palette or the bed picker.
            CloseMenusOverTheBoard();

            _contextRows.Clear();
            _contextRows.AddRange(rows);
            _contextMenu.Clear();
            for (int i = 0; i < _contextRows.Count; i++) _contextMenu.Add(ContextMenuRowView(_contextRows[i]));

            _contextAt = ToPanel(screenPosition);
            _contextFacing = _rig != null ? _rig.transform.rotation : Quaternion.identity;
            _contextMenu.style.display = DisplayStyle.Flex;
            _contextMenu.BringToFront();
            PlaceContextMenu();
        }

        /// <summary>Put the menu away. Safe to call when it is not up, or was never built.</summary>
        public void CloseContextMenu()
        {
            if (_contextMenu == null) return;
            _contextMenu.style.display = DisplayStyle.None;
            _contextRows.Clear();
        }

        VisualElement ContextMenuRowView(ContextMenuRow row)
        {
            var view = new VisualElement();
            view.AddToClassList("ctxmenu__row");
            if (!row.Enabled) view.AddToClassList("ctxmenu__row--off");
            if (row.IsCancel) view.AddToClassList("ctxmenu__row--cancel");

            view.Add(HudText.Make(row.Label, HudTextRole.Row, ussClass: "ctxmenu__label"));
            if (!row.Enabled && row.Reason.Length > 0)
                view.Add(HudText.Make(row.Reason, HudTextRole.Meta, ussClass: "ctxmenu__reason"));

            view.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.button != 0) return;
                ChooseContextRow(row);
            });
            return view;
        }

        /// <summary>
        /// A row was clicked: its orders to the world and the menu away. A disabled row does
        /// nothing and leaves the menu up, so the reason beside it can still be read.
        /// </summary>
        void ChooseContextRow(ContextMenuRow row)
        {
            if (!row.Enabled) return;

            var world = _boot!.World;
            if (world != null)
            {
                _contextOrders.Clear();
                ContextMenuModel.Choose(row, _contextOrders);
                for (int i = 0; i < _contextOrders.Count; i++) world.Intents.Submit(_contextOrders[i]);
                _contextOrders.Clear();
            }
            CloseContextMenu();
        }

        /// <summary>
        /// Put the menu at the pointer, turned at the screen's edges. Once it has a size; until
        /// then it sits at the pointer, which is where it belongs unless it is near an edge.
        /// </summary>
        void PlaceContextMenu()
        {
            if (!ContextMenuOpen) return;

            float screenWidth = _hud.resolvedStyle.width;
            float screenHeight = _hud.resolvedStyle.height;
            float width = _contextMenu!.resolvedStyle.width;
            float height = _contextMenu.resolvedStyle.height;

            bool sized = !float.IsNaN(width) && width > 1f && !float.IsNaN(height) && height > 1f
                && !float.IsNaN(screenWidth) && screenWidth > 1f && !float.IsNaN(screenHeight) && screenHeight > 1f;
            if (!sized)
            {
                _contextMenu.style.left = _contextAt.x + HudLayout.ContextMenuNudge;
                _contextMenu.style.top = _contextAt.y + HudLayout.ContextMenuNudge;
                return;
            }

            _contextMenu.style.left = HudLayout.ContextMenuLeft(_contextAt.x, width, screenWidth);
            _contextMenu.style.top = HudLayout.ContextMenuTop(_contextAt.y, height, screenHeight);
        }

        /// <summary>
        /// Once a frame, from <see cref="Update"/>: close on a press outside the menu or on the
        /// camera turning. A press inside is the row's own, and its click chooses.
        /// </summary>
        void UpdateContextMenu()
        {
            if (!ContextMenuOpen) return;

            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null
                && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame
                    || mouse.middleButton.wasPressedThisFrame)
                && !_contextMenu!.worldBound.Contains(ToPanel(mouse.position.ReadValue())))
            {
                CloseContextMenu();
                return;
            }

            if (_rig != null && Quaternion.Angle(_rig.transform.rotation, _contextFacing) > ContextMenuOrbitDegrees)
                CloseContextMenu();
        }
    }
}
