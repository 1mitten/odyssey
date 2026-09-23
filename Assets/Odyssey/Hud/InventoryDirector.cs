#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>
    /// Whether the Inventory tab is open (design 35). Session state, for the reason the Work
    /// tab's director is: not saved, and not the simulation's. What the tab lists is read off
    /// the published frame every time, by <see cref="InventoryModel"/>.
    /// </summary>
    public sealed class InventoryDirector
    {
        public const string PanelKey = "ui.tab.inventory";

        public const string SearchKey = "ui.inventory.hud.search";
        public const string ItemKey = "ui.inventory.hud.item";
        public const string PlacesKey = "ui.inventory.hud.places";
        public const string TotalKey = "ui.inventory.hud.total";
        public const string WhereKey = "ui.inventory.hud.where";
        public const string QtyKey = "ui.inventory.hud.qty";
        public const string GoKey = "ui.inventory.hud.go";
        public const string GoToKey = "ui.inventory.hud.goto";
        public const string InPlacesKey = "ui.inventory.hud.inplaces";
        public const string InPlaceKey = "ui.inventory.hud.inplace";
        public const string HintKey = "ui.inventory.hud.hint";
        public const string EmptyKey = "ui.inventory.hud.empty";
        public const string NoMatchKey = "ui.inventory.hud.nomatch";

        /// <summary>Every key the panel puts on screen that is its own, for <c>RegistryTests</c>.</summary>
        public static readonly string[] IconKeys =
        {
            PanelKey, SearchKey, ItemKey, PlacesKey, TotalKey, WhereKey, QtyKey, GoKey, GoToKey,
            InPlacesKey, InPlaceKey, HintKey, EmptyKey, NoMatchKey,
        };

        public bool Open { get; private set; }

        public event Action? Changed;

        public void Toggle() => SetOpen(!Open);

        public void SetOpen(bool open)
        {
            if (Open == open) return;
            Open = open;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// The Inventory tab's fixed geometry, one owner, from the owner's spec of 2026-09-23
    /// (design 35 §3). Every number here is asserted against the tree the panel builds.
    /// </summary>
    public static class InventoryLayout
    {
        /// <summary>The window's outer width. A UI Toolkit width is a border box.</summary>
        public const int Width = 1100;

        public const int HeaderHeight = 34;
        public const int ToolbarHeight = 38;
        public const int BodyHeight = 420;

        /// <summary>The window's outer height, constant whatever the colony holds.</summary>
        public const int Height = HeaderHeight + ToolbarHeight + BodyHeight + 2 * HudTheme.BorderWidth;

        public const int SearchWidth = 260;
        public const int SearchHeight = 26;
        public const int SearchInnerPad = 9;
        public const int SearchGlyph = 12;

        public const int TableWidth = 640;

        /// <summary>What is left for the where-it-is pane.</summary>
        public const int DetailWidth = Width - 2 * HudTheme.BorderWidth - TableWidth;

        public const int PlacesColumn = 80;
        public const int TotalColumn = 70;
        public const int QtyColumn = 70;
        public const int GoColumn = 52;

        /// <summary>A row, its 1 px rule inside it.</summary>
        public const int RowHeight = 30;

        /// <summary>The table's rows under its heading, when everything fits.</summary>
        public const int RowsPerPage = (BodyHeight - RowHeight) / RowHeight;

        /// <summary>When the list pages, the foot takes one row's height.</summary>
        public const int RowsPerPagedPage = RowsPerPage - 1;

        public const int SidePad = 12;
        public const int Gap = 9;
        public const int ItemIndent = 9;
        public const int ItemTile = 18;
        public const int ItemTileEdge = 2;
        public const int DetailTile = 32;
        public const int DetailTileEdge = 3;
        public const int CategoryMark = 8;
        public const int SelectedRail = 2;

        public const int SortMarkWidth = 7;
        public const int SortMarkHeight = 5;
        public const int SortMarkGap = 6;

        public const int GoButtonHeight = 22;
        public const int GoButtonPad = 9;
        public const int PrimaryHeight = 30;
        public const int PagerButton = 22;

        /// <summary>The header's total sits this far after the title.</summary>
        public const int TotalGap = 6;
    }
}
