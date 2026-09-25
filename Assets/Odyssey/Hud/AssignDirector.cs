#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>
    /// Whether the Assign tab is open (design 43 §6). Session state, for the reason the Work tab's
    /// director is: not saved, and not the simulation's.
    ///
    /// <para><b>One row per colonist, two settings each</b>: where she may work (Anywhere or
    /// Home) and what she does about danger (Fight back, Defend, Flee). The panel catalogue's B4
    /// reservation and design 33 §18h's deferred rules panel, which waited for a second rule to
    /// hold; the grid is left open to the right for the next column.</para>
    /// </summary>
    public sealed class AssignDirector
    {
        public const string PanelKey = "ui.tab.assign";

        /// <summary>The three column headings.</summary>
        public const string ColonistKey = "ui.assign.colonist";
        public const string AreaKey = "ui.assign.area";
        public const string ResponseKey = "ui.assign.response";

        /// <summary>The two areas, as the cell says them.</summary>
        public const string AnywhereKey = "ui.assign.anywhere";
        public const string HomeKey = "ui.assign.home";

        /// <summary>The note beside the Area heading while there is no hearth, so Home keeps nobody in.</summary>
        public const string NoHearthKey = "ui.assign.nohearth";

        /// <summary>Every key the panel puts on screen that is its own, for <c>RegistryTests</c>.</summary>
        public static readonly string[] IconKeys =
            { PanelKey, ColonistKey, AreaKey, ResponseKey, AnywhereKey, HomeKey, NoHearthKey };

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
    /// The tab's fixed geometry, one owner, from Claude Design's specification of 2026-09-25
    /// (design 43 §6). Every number here was chosen there; the tests hold the arithmetic.
    /// </summary>
    public static class AssignLayout
    {
        /// <summary>The window's outer width, constant whatever the colony. A UI Toolkit width is a border box.</summary>
        public const int TabWidth = 536;

        public const int ColonistColumn = 192;
        public const int AreaColumn = 150;
        public const int ResponseColumn = 150;
        public const int ColumnGap = 9;

        /// <summary>The three columns and the two gaps between them.</summary>
        public const int GridWidth = ColonistColumn + ColumnGap + AreaColumn + ColumnGap + ResponseColumn;

        /// <summary>The window's content width is the grid: the spec's arithmetic, checked by a test.</summary>
        public const int ExpectedGridWidth = TabWidth - 2 * (HudLayout.Pad + HudTheme.BorderWidth);

        public const int HeaderHeight = 34;
        public const int ColumnHeaderHeight = 30;
        public const int RowHeight = 30;
        public const int RowsPerPage = 12;
        public const int PagerHeight = 34;

        /// <summary>The title's count, six after the word: "ASSIGN (14)".</summary>
        public const int CountGap = 6;

        /// <summary>"No hearth yet", nine after the Area heading.</summary>
        public const int NoHearthGap = 9;

        public const int CloseBox = 22;
        public const int CloseMark = 10;

        public const int Portrait = 20;
        public const int PortraitGap = 9;

        /// <summary>A setting cell: 24 high inside the 30 row, its text 9 in from the left and its mark 9 in from the right.</summary>
        public const int CellHeight = 24;
        public const int CellPad = 9;
        public const int CycleMark = 11;

        public const int PagerButton = 22;
        public const int PagerChevron = 10;
        public const int PagerGap = 9;

        /// <summary>A pager button's glyph on the first or last page, against the others'.</summary>
        public const float PagerDimmed = 0.40f;

        /// <summary>The window's height for a page of <paramref name="rows"/> rows, with or without the pager.</summary>
        public static int PanelHeight(int rows, bool paged) =>
            HeaderHeight + ColumnHeaderHeight + rows * RowHeight + (paged ? PagerHeight : 0) + 2 * HudTheme.BorderWidth;
    }
}
