#nullable enable

namespace Odyssey.Hud
{
    /// <summary>
    /// The trade window's measurements (design 65 §6, mockups 28a Sell and 28b Buy). Every number
    /// here is the owner's spec's, named once so the view writes none of them.
    ///
    /// <para><b>The width includes the chrome</b>: UI Toolkit's <c>width</c> is a border box, and
    /// <see cref="PanelOuterWidth"/> is <see cref="Width"/> exactly because the window draws its own
    /// padding inside each band rather than on the panel (the Work tab's lesson).</para>
    /// </summary>
    public static class TradeLayout
    {
        public const int Width = 820;
        public const int HeaderHeight = 64;
        public const int ModeHeight = 52;
        public const int SegmentMinWidth = 110;
        public const int SegmentHeight = 32;
        public const int ColumnHeaderHeight = 26;
        public const int CategoryRowHeight = 28;
        public const int RowHeight = 34;
        public const int FootHeight = 72;
        public const int ButtonsHeight = 56;
        public const int ConfirmMinWidth = 140;
        public const int CancelMinWidth = 120;

        /// <summary>The side padding of every band.</summary>
        public const int SidePad = 16;

        /// <summary>The header's right padding, beside the close box.</summary>
        public const int HeaderRightPad = 12;

        public const int Face = 40;
        public const int CloseBox = 28;

        // The row grid: icon, item, quality, in stock, price, quantity, total; a 12 px gap.
        public const int ColIcon = 28;
        public const int ColQuality = 72;
        public const int ColInStock = 72;
        public const int ColPrice = 64;
        public const int ColQuantity = 164;
        public const int ColTotal = 64;
        public const int ColumnGap = 12;

        // The quantity cell: minus, figure, plus, All; a 4 px gap.
        public const int QtyButton = 28;
        public const int QtyAllWidth = 44;
        public const int QtyGap = 4;

        /// <summary>One press moves one; Shift moves ten.</summary>
        public const int Step = 1;
        public const int ShiftStep = 10;

        /// <summary>Below this screen height, in panel pixels, the list pages rather than growing.</summary>
        public const int PagingBelowHeight = 1000;

        /// <summary>Rows a page holds when the list pages, headings included.</summary>
        public const int RowsPerPage = 20;

        /// <summary>The panel's outer width: the whole of it, since every band pads itself.</summary>
        public const int PanelOuterWidth = Width;

        /// <summary>
        /// The item column's width: what is left of the band after the fixed columns and six gaps.
        /// </summary>
        public const int ColItem = Width - 2 * SidePad
            - (ColIcon + ColQuality + ColInStock + ColPrice + ColQuantity + ColTotal) - 6 * ColumnGap;

        /// <summary>The quantity cell's figure: what is left after minus, plus and All and three gaps.</summary>
        public const int QtyFigure = ColQuantity - 2 * QtyButton - QtyAllWidth - 3 * QtyGap;

        /// <summary>The fixed bands, without the list.</summary>
        public const int ChromeHeight = HeaderHeight + ModeHeight + ColumnHeaderHeight + FootHeight + ButtonsHeight;
    }
}
