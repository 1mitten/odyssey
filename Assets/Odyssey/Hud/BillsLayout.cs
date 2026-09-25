#nullable enable

namespace Odyssey.Hud
{
    /// <summary>
    /// Every number in the bill list (design 49): the workbench pane's width, its section strips,
    /// the bill row's columns and its controls. The view (<c>BillList</c>) reads these and writes
    /// none of its own, so <c>BillsLayoutTests</c> can hold the row to the pane in the fast tier.
    ///
    /// <para><b>The row has a width budget.</b> The columns are fixed except the name, which takes
    /// what is left; <see cref="NameColumn"/> is that remainder, and a test fails if widening any
    /// column squeezes it under <see cref="NameColumnMin"/>.</para>
    /// </summary>
    public static class BillsLayout
    {
        /// <summary>A station's pane: wider than a store's 560, because a bill row carries seven columns.</summary>
        public const int PaneWidth = 680;

        /// <summary>The status strip, shown only when the station has a problem.</summary>
        public const int StatusHeight = 48;

        /// <summary>A section's header strip: BILLS, and its count.</summary>
        public const int SectionHeadHeight = 34;

        /// <summary>The inset bar down a strip's left edge, in the section's colour.</summary>
        public const int SectionBar = 3;

        /// <summary>A strip's left padding: the side padding plus the 3 px bar it stands beside.</summary>
        public const int StripPadLeft = 15;

        public const int SidePad = 12;

        public const int RowHeight = 48;

        /// <summary>The gap between two columns of a row.</summary>
        public const int ColumnGap = 9;

        /// <summary>Every square control: a stepper button, pause, delete, the reorder pair.</summary>
        public const int Control = 28;

        /// <summary>The gap between two action buttons.</summary>
        public const int ActionGap = 4;

        public const int IndexColumn = 12;
        public const int IconColumn = Control;
        public const int ModeColumn = 132;
        public const int StepperColumn = 92;
        public const int ProgressColumn = 64;

        /// <summary>
        /// Reorder, pause, delete. The design's actions were two buttons; the game can also move
        /// a bill up and down (design 48 §14), so the pair stands first, stacked in one 28 box.
        /// </summary>
        public const int ActionsColumn = 3 * Control + 2 * ActionGap;

        /// <summary>The product tile's inset bottom edge, in the product's category hue.</summary>
        public const int IconEdge = 2;

        /// <summary>The gap from a bill's name down to its status line.</summary>
        public const int StatusLineGap = 5;

        public const int ProgressTrack = 4;
        public const int ProgressGap = 6;

        public const int AddHeight = 36;
        public const int AddPadY = 9;
        public const int AddPadX = 12;

        /// <summary>Glyph sizes, in pixels.</summary>
        public const int StatusGlyph = 16;
        public const int SwitchGlyph = 13;
        public const int ModeGlyph = 11;
        public const int StepGlyph = 10;
        public const int ActionGlyph = 12;
        public const int AddGlyph = 14;
        public const int ReorderGlyph = 9;

        /// <summary>The Switch button in the status strip.</summary>
        public const int SwitchHeight = 28;

        /// <summary>What a paused bill's middle columns are drawn at. The actions stay whole, so it can be resumed.</summary>
        public const float PausedOpacity = 0.55f;

        /// <summary>A control that cannot be pressed right now.</summary>
        public const float DisabledOpacity = 0.40f;

        /// <summary>The fills behind the stepper's two buttons, and the strip washes.</summary>
        public const float StepFill = 0.14f;
        public const float StepBorder = 0.60f;
        public const float WarnWash = 0.10f;
        public const float AccentWash = 0.08f;

        /// <summary>The room a row's columns have: the pane less its border and the row's side padding.</summary>
        public const int RowContentWidth = PaneWidth - 2 * HudTheme.BorderWidth - 2 * SidePad;

        /// <summary>The name column: everything the fixed columns and the six gaps leave.</summary>
        public const int NameColumn = RowContentWidth
            - (IndexColumn + IconColumn + ModeColumn + StepperColumn + ProgressColumn + ActionsColumn)
            - 6 * ColumnGap;

        /// <summary>The narrowest the name may be: "Cook a meal" at 14 px, with room for a longer recipe.</summary>
        public const int NameColumnMin = 140;
    }
}
