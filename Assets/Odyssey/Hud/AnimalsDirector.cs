#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>
    /// Whether the Animals tab is open (design 30 §6). Session state, for the reason the Work
    /// tab's director is: it is not saved, and it is not the simulation's.
    ///
    /// <para><b>One tab, called Animals</b> (owner, 2026-09-23: "animals/wildlife under one tab -
    /// for now - just call it animals"). It lists the wild animals today and gains the tamed
    /// ones' columns when taming exists; the Wildlife item on F6 is a dead item again.</para>
    /// </summary>
    public sealed class AnimalsDirector
    {
        public const string PanelKey = "ui.tab.animals";

        /// <summary>The two column headings, and the line the empty table shows.</summary>
        public const string KindKey = "ui.animals.kind";
        public const string DoingKey = "ui.animals.doing";
        public const string EmptyKey = "ui.animals.empty";

        /// <summary>Every key the panel puts on screen that is its own, for <c>RegistryTests</c>.</summary>
        public static readonly string[] IconKeys = { PanelKey, KindKey, DoingKey, EmptyKey };

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
    /// The tab's fixed geometry, one owner, from the design brief of 2026-09-23
    /// (<c>docs/reference/mockups/animals-tab-brief.md</c>). Every number here was chosen there
    /// and is asserted against the tree the panel actually builds.
    /// </summary>
    public static class AnimalsLayout
    {
        /// <summary>The window's outer width, constant whatever the board carries. A UI Toolkit width is a border box.</summary>
        public const int TabWidth = 560;

        public const int PortraitColumn = 32;
        public const int PortraitTile = 22;
        public const int KindColumn = 250;
        public const int DoingColumn = 252;

        /// <summary>The three columns: the window less the panel's padding and border on both sides.</summary>
        public const int GridWidth = PortraitColumn + KindColumn + DoingColumn;

        public const int RowHeight = 30;
        public const int HeaderHeight = 34;
        public const int CountStripHeight = 30;
        public const int PagerHeight = 30;
        public const int RowsPerPage = 12;

        /// <summary>Between two entries of the count strip, and between a kind's word and its figure.</summary>
        public const int CountGap = 18;
        public const int CountFigureGap = 6;

        public const int DoingSquare = 12;
        public const int DoingGap = 9;

        public const int SortMarkWidth = 7;
        public const int SortMarkHeight = 5;
        public const int SortMarkGap = 6;

        public const int PagerButton = 22;
        public const int PagerGap = 9;

        /// <summary>The window's content width is the grid: the brief's arithmetic, checked by a test.</summary>
        public const int ExpectedGridWidth = TabWidth - 2 * (HudLayout.Pad + HudTheme.BorderWidth);
    }
}
