#nullable enable
using System;

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

        // ------------------------------------------------------------------ the whole panel

        /// <summary>How wide the panel is for a given number of columns.</summary>
        public static int WidthFor(int columns) => LeftColumn + columns * Pitch;

        /// <summary>How tall it is for a given number of rows, grid only — no header, no legend.</summary>
        public static int GridHeightFor(int rows) => HeaderBand + rows * RowHeight;
    }
}
