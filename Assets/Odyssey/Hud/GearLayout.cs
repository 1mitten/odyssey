#nullable enable

namespace Odyssey.Hud
{
    /// <summary>
    /// Every measurement of the Gear tab (design 47 §2), taken from Claude Design's specification
    /// of 2026-09-25 and nowhere else. Presentation reads these and writes no number of its own.
    ///
    /// <para><b>The body is 244 high and every tab now sits in it</b>, because the pane is docked
    /// to its bottom edge and grows upward: a Gear tab taller than the rest would move the header
    /// under the pointer every time it was chosen, which is the fault the fixed body was built to
    /// end (design 14). <see cref="HudLayout.InspectTabBody"/> takes the tallest of the three live
    /// bodies, and this one is the tallest.</para>
    ///
    /// <para><b>Down the body</b>: 12 of padding, the doll (three slot rows), 12, the kit row, 12,
    /// the footer — 12 + 138 + 12 + 40 + 12 + 30 = 244, held by <c>GearLayoutTests</c>. <b>Across
    /// it</b> there is no side padding of its own: the specification's 12 either side is the
    /// pane's own padding, which already stands round every tab (the one place this departs from
    /// the numbers as written, design 47 §5).</para>
    /// </summary>
    public static class GearLayout
    {
        /// <summary>The whole tab body, the same for every tab on the pane.</summary>
        public const int TabBody = 244;

        /// <summary>Above the doll, and between the doll, the kit row and the footer.</summary>
        public const int Pad = 12;
        public const int SectionGap = 12;

        // ---- the doll ---------------------------------------------------------------------------

        public const int SlotRows = 3;
        public const int SlotRow = 40;
        public const int SlotGap = 9;

        /// <summary>Three slot rows and the two gaps between them.</summary>
        public const int DollHeight = SlotRows * SlotRow + (SlotRows - 1) * SlotGap;

        /// <summary>The figure in the middle column; the doll is <c>1fr 96 1fr</c> with 12 between.</summary>
        public const int FigureWidth = 96;
        public const int DollColumnGap = 12;

        /// <summary>A slot's tile, the item's icon inside it, and the gap from the tile to the words.</summary>
        public const int SlotTile = 40;
        public const int Icon = 32;
        public const int SlotTextGap = 9;

        /// <summary>From the item's name down to its meta line, and between the meta words.</summary>
        public const int MetaLineGap = 5;
        public const int MetaWordGap = 6;

        // ---- the kit row --------------------------------------------------------------------------

        public const int KitRow = 40;
        public const int KitTile = 40;
        public const int KitGap = 9;
        public const int KitLabelWidth = 40;
        public const int KitBelt = 2;
        public const int KitPack = 4;
        public const int KitSlots = KitBelt + KitPack;

        /// <summary>The count on a filled kit tile: 14 high, at least 14 wide, inset 1 from the corner.</summary>
        public const int Badge = 14;
        public const int BadgeInset = 1;
        public const int BadgePad = 2;

        /// <summary>The lock on a pack slot, and its stroke in path units.</summary>
        public const int Lock = 16;
        public const float LockStroke = 2f;

        /// <summary>The hint after the tiles, when there is no pack.</summary>
        public const int HintGap = 3;

        // ---- the footer ---------------------------------------------------------------------------

        public const int Footer = 30;
        public const int EffectGap = 18;
        public const int EffectValueGap = 6;
        public const int LoadoutLabelGap = 9;
        public const int LoadoutCell = 22;
        public const int LoadoutPadLeft = 8, LoadoutPadRight = 6;
        public const int LoadoutChevron = 10;
        public const int LoadoutChevronGap = 6;

        /// <summary>Down the body, exactly: the padding, the doll, the kit row, the footer and their gaps.</summary>
        public const int BodyHeight = Pad + DollHeight + SectionGap + KitRow + SectionGap + Footer;

        // ---- the popovers ------------------------------------------------------------------------

        /// <summary>A popover opens outside the pane on its right, this far from its edge.</summary>
        public const int PopoverGap = 9;

        public const int ItemPopoverWidth = 260;
        public const int ItemPopoverPad = 12;
        public const int ItemPopoverGap = 9;
        public const int ButtonHeight = 30;

        public const int PickWidth = 340;
        public const int PickHeader = 34;
        public const int PickRow = 40;
        public const int PickMaxRows = 8;

        /// <summary>A disabled control on a downed colonist's tab: 40 % (the specification's 21f).</summary>
        public const float DisabledOpacity = 0.4f;

        /// <summary>The jumpsuit's icon, shown faint: it is worn, and it is not an item.</summary>
        public const float JumpsuitIconOpacity = 0.45f;

        /// <summary>Where a popover's left edge falls: the pane's right edge and the gap.</summary>
        public static int PopoverLeft(int paneLeft, int paneWidth) => paneLeft + paneWidth + PopoverGap;
    }
}
