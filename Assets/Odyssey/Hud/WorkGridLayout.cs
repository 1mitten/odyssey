#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The Work grid's geometry, and the one piece of it that is arithmetic rather than taste.
    ///
    /// <para><b>Nothing here is copied from the supplied mockup.</b> Its numbers were measured for
    /// 13-to-19px system sans and thirteen columns; ours are Archivo Narrow at
    /// <see cref="HudType"/>'s six steps and twenty-two columns, so every figure is re-derived.
    /// <c>docs/design/27-work-tab.md</c> §3 shows the working.</para>
    ///
    /// <para><b>Why the rotation angle is a test and not a style rule.</b> A rotated label's
    /// horizontal footprint is <c>width × cos θ</c>. The moment that exceeds the column pitch,
    /// adjacent labels overlap — and the overlap gets worse toward the left of the panel, so it is
    /// invisible in a screenshot of the right-hand columns and invisible again in a mockup with
    /// fewer columns than the game. The mockup's own −62° projects 36.6px at our pitch and
    /// collides. <see cref="LongestLabelFits"/> is the guard, and <c>WorkGridTests</c> runs it over
    /// the real <see cref="WorkCatalogue"/> in the fast tier.</para>
    /// </summary>
    public static class WorkGridLayout
    {
        // ------------------------------------------------------------------ the grid

        /// <summary>Centre-to-centre spacing of two columns.</summary>
        public const int Pitch = 34;

        /// <summary>The priority cell, square, centred in <see cref="Pitch"/>.</summary>
        public const int Cell = 28;

        /// <summary>
        /// A grid row, and deliberately one pixel taller than <see cref="HudLayout.RowHeight"/>.
        /// A 24px portrait wants an even 3px above and below it and 29 cannot give that. The one
        /// place this panel trades a hairline of consistency for arithmetic that closes.
        /// </summary>
        public const int RowHeight = 30;

        /// <summary>The cached appearance render in the frozen column.</summary>
        public const int Portrait = 24;

        // ------------------------------------------------------------------ the frozen column

        /// <summary>Pad, portrait, gap, name, pad — 10 + 24 + 8 + 140 + 10.</summary>
        public const int LeftColumn = 192;

        /// <summary>Room for the name. Twenty characters at <see cref="HudTextRole.Row"/>, which
        /// clears the longest entry in <c>colonist-names.csv</c> plus a surname.</summary>
        public const int NameBudget = 140;

        public const int LeftPad = 10;

        /// <summary>Portrait to name.</summary>
        public const int PortraitGap = 8;

        // ------------------------------------------------------------------ the header band

        /// <summary>
        /// The rotation, negative because the labels fan up and to the left. Pinned here and
        /// checked against <see cref="LongestLabelFits"/> rather than derived at runtime: a
        /// number a reader can see beats a number a reader has to run.
        /// </summary>
        public const int LabelAngleDegrees = -66;

        /// <summary>
        /// How tall the rotated labels stand: <c>sin 66° × 78 = 71.3</c>, rounded up. Recomputed
        /// by <see cref="LabelRise"/> and pinned by the tests, so a longer work type moves this
        /// number in the fast tier rather than on the screen.
        /// </summary>
        public const int LabelBand = 72;

        /// <summary>Label box to icon tile.</summary>
        public const int LabelGap = 4;

        /// <summary>The icon tile at the foot of the band, the same square as a cell.</summary>
        public const int IconTile = Cell;

        /// <summary>
        /// Simple mode's tick and cross, drawn rather than typed. Half the cell, so the mark sits
        /// inside the 28px box with the same air a digit has around it and still clears the
        /// passion flames in the top right corner.
        /// </summary>
        public const int MarkSize = 14;

        /// <summary>The whole header band: <see cref="LabelBand"/> + <see cref="LabelGap"/> + tile.</summary>
        public const int HeaderBand = LabelBand + LabelGap + IconTile;

        /// <summary>
        /// The clearance a label must keep from its neighbour. Two pixels of slack, because the
        /// advance model below is an estimate and an estimate that lands exactly on the pitch is
        /// an estimate that has already failed.
        /// </summary>
        public const int LabelMargin = 2;

        // ------------------------------------------------------------------ the arithmetic

        /// <summary>
        /// The modelled width of a header label, in pixels. The same estimate
        /// <see cref="HudCommands.Width"/> uses and for the same reason: the fast tier has no text
        /// engine, and an estimate that is generous can only ever make a layout claim pessimistic.
        /// </summary>
        public static float LabelWidth(string label) =>
            label.Length * HudType.Of(HudTextRole.Body).Size * HudCommands.UiAdvance;

        /// <summary>The horizontal footprint of a label rotated by <see cref="LabelAngleDegrees"/>.</summary>
        public static float Footprint(string label) =>
            LabelWidth(label) * (float)Math.Abs(Math.Cos(LabelAngleDegrees * Math.PI / 180.0));

        /// <summary>How tall that label stands once rotated.</summary>
        public static float LabelRise(string label) =>
            LabelWidth(label) * (float)Math.Abs(Math.Sin(LabelAngleDegrees * Math.PI / 180.0));

        /// <summary>
        /// The acceptance criterion, as a question: does the longest label in the catalogue clear
        /// its neighbour at this pitch?
        /// </summary>
        public static bool LongestLabelFits()
        {
            return Footprint(LongestLabel()) + LabelMargin <= Pitch;
        }

        /// <summary>The label the constraint is decided by. "Firefighting", twelve characters.</summary>
        public static string LongestLabel()
        {
            string longest = string.Empty;
            foreach (WorkCatalogue.Entry entry in WorkCatalogue.All)
            {
                string label = entry.Label;
                if (label.Length > longest.Length) longest = label;
            }
            return longest;
        }

        /// <summary>
        /// The shallowest angle this pitch permits, in degrees, for a label of the given width.
        /// Not used to draw anything — it is what a failing test prints, so the reader is told
        /// what to change rather than that something is wrong.
        /// </summary>
        public static double ShallowestAngleFor(float labelWidth)
        {
            if (labelWidth <= 0f) return 0.0;
            double room = Math.Min(1.0, (Pitch - LabelMargin) / (double)labelWidth);
            return Math.Acos(room) * 180.0 / Math.PI;
        }

        // ------------------------------------------------------------------ the schedule half

        /// <summary>
        /// An hour's column, and <b>equal to <see cref="Pitch"/> on purpose</b>.
        ///
        /// <para>This is the one number the combined table turns on. A priority cell and an hour
        /// block are the same click target and sit on the same rhythm, so the eye reads one
        /// continuous row rather than two tables that happen to be adjacent. A constant defined as
        /// the other constant rather than as 34, so that widening one half cannot silently
        /// desynchronise the two.</para>
        /// </summary>
        public const int HourPitch = Pitch;

        /// <summary>
        /// Hours in the day, <b>defined as</b> <see cref="ScheduleHandle.Hours"/> rather than as
        /// 24. The doc said it was that number and the code said 24, which is two owners for one
        /// fact — the pattern this project has met four times (docs/bug-patterns.md).
        /// </summary>
        public const int Hours = ScheduleHandle.Hours;

        /// <summary>
        /// The heavier rule between the work half and the schedule half. Two halves of one row
        /// still want a seam, or the last work column and the midnight hour read as neighbours.
        /// </summary>
        public const int SectionDivider = 1;

        /// <summary>The now-line, and the only saturated thing on the panel that is not a band.</summary>
        public const int NowLineWidth = 2;

        /// <summary>
        /// Where the now-line sits inside the schedule half: the centre of the current hour's
        /// column.
        ///
        /// <para><b>Relative to the schedule container, never to the panel.</b> The supplied spec
        /// warns about this and it is right — measuring from the panel puts the line one frozen
        /// name column out, which lands it on a different hour and looks like an off-by-one in the
        /// clock rather than in the layout.</para>
        /// </summary>
        public static float NowLineCentre(int hour) => hour * HourPitch + HourPitch / 2f;

        /// <summary>The schedule half's own width.</summary>
        public const int ScheduleWidth = Hours * HourPitch;

        // ------------------------------------------------------------------ the whole panel

        /// <summary>How wide the work half alone is for a given number of columns.</summary>
        public static int WidthFor(int columns) => LeftColumn + columns * Pitch;

        /// <summary>
        /// The combined table: the frozen name column, every work column, and the whole day.
        ///
        /// <para>At twenty-two work types this is <c>192 + 748 + 816 = 1756</c>, which is 91% of
        /// the 1920 reference — it fits, and only because our type is narrow. At the supplied
        /// spec's 40px pitch the same table is 2,026px and does not.</para>
        /// </summary>
        public static int CombinedWidthFor(int columns) =>
            WidthFor(columns) + SectionDivider + ScheduleWidth;

        /// <summary>How tall it is for a given number of rows, grid only — no header, no legend.</summary>
        public static int GridHeightFor(int rows) => HeaderBand + rows * RowHeight;

        // ------------------------------------------------------------------ the two pagers

        /// <summary>
        /// Work columns on one page. <b>Eleven, because twenty-two divides by it exactly</b> — two
        /// pages, both full, no ragged remainder to explain.
        ///
        /// <para><b>This replaced a horizontal scrollbar</b> (owner, 2026-09-20: <i>"remove the
        /// scroll bars — this isn't a good interface — replace with pagination similar to the
        /// roster pagination"</i>). The scroller was the wrong answer twice over: it made the
        /// panel's width depend on the window, so the control resized under the player, and it
        /// built every one of the twenty-two columns for every colonist whether or not any of them
        /// was on screen. A page builds what it shows and nothing else.</para>
        ///
        /// <para>The cost is that the four live columns split two and two across the pages, which
        /// is a fact about the order in <c>icon-keys.csv</c> rather than about this number — the
        /// alternative, eight per page, gathers all four on page two and leaves page one entirely
        /// dead. Eleven was the owner's call on that trade.</para>
        /// </summary>
        public const int ColumnsPerPage = 11;

        /// <summary>
        /// Colonist rows on one page. Twelve: the panel stands 544px tall at its fullest, which
        /// clears every screen, and the grid is capped at twelve rows however large the colony
        /// grows. Rows used to grow with the colony and clip, unreachable, at about twenty-four.
        /// </summary>
        public const int RowsPerPage = 12;

        /// <summary>How many pages a given number of columns comes to. Two, today.</summary>
        public static int ColumnPagesFor(int columns) =>
            columns <= 0 ? 1 : (columns + ColumnsPerPage - 1) / ColumnsPerPage;

        /// <summary>How many pages a given number of colonists comes to.</summary>
        public static int RowPagesFor(int rows) =>
            rows <= 0 ? 1 : (rows + RowsPerPage - 1) / RowsPerPage;

        /// <summary>
        /// The panel's width, and it is a constant now rather than a function of the colony or the
        /// window: names, one page of work columns, the seam, and the whole day. <b>192 + 374 + 1
        /// + 816 = 1,383.</b>
        ///
        /// <para>Nothing about the panel resizes any more. A wider colony pages, a longer catalogue
        /// pages, and the frame the player learned the position of stays where it was.</para>
        /// </summary>
        public const int PanelWidth =
            LeftColumn + ColumnsPerPage * Pitch + SectionDivider + ScheduleWidth;

        /// <summary>The grid's height at a full page — header band and twelve rows.</summary>
        public const int PanelGridHeight = HeaderBand + RowsPerPage * RowHeight;
    }
}
