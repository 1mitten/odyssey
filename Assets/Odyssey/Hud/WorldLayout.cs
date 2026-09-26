#nullable enable

namespace Odyssey.Hud
{
    /// <summary>
    /// The World screen's measurements, from Claude Design's specification
    /// (<c>docs/reference/mockups/world-screen-spec.md</c>, "Constants"). One place, so the page, the
    /// painter and the tests read the same numbers — and a test holds the ones that meet a stylesheet
    /// to it, the rule the Work tab's border-box fault made.
    /// </summary>
    public static class WorldLayout
    {
        // ---- the frame -------------------------------------------------------------------------

        /// <summary>The page's inset from every screen edge.</summary>
        public const int Inset = 24;

        /// <summary>Padding inside the frame: 32 top and bottom, 40 either side.</summary>
        public const int PadVertical = 32;
        public const int PadHorizontal = 40;

        /// <summary>The stats panel's width, and the gap between it and the map.</summary>
        public const int StatsWidth = 340;
        public const int BodyGap = 24;

        /// <summary>Controls on the top row and in the footer are this tall; footer buttons at least this wide.</summary>
        public const int ControlHeight = 36;
        public const int FooterButtonMinWidth = 160;
        public const int SeedFieldWidth = 200;

        // ---- the map ---------------------------------------------------------------------------

        public const int MapColumns = 64;
        public const int MapRows = 32;

        /// <summary>A hex, flat side to flat side, at 1×.</summary>
        public const float HexWidth = 18.4f;

        /// <summary>Rows sit this fraction of a hex's height apart.</summary>
        public const float HexRowStep = 0.75f;

        /// <summary>
        /// The texture is painted at this multiple of its 1× size (design 59 §9a), so a 4× zoom is not
        /// a blur. The painter's cost is measured in design 59 §9d; 1.5 is the fall-back.
        /// </summary>
        public const float PaintScale = 2f;

        /// <summary>The selection: a 4 px dark underline under a 2.2 px accent line, 1.5 px outside the hex.</summary>
        public const float SelectUnderline = 4f;
        public const float SelectRing = 2.2f;
        public const float SelectGrow = 1.5f;

        /// <summary>The dashed accent ring round the selection: 7 px out, 1.5 px wide, dashes of 3 and 3.</summary>
        public const float SelectDashGrow = 7f;
        public const float SelectDashWidth = 1.5f;
        public const float SelectDash = 3f;

        /// <summary>The hover: 1.5 px at 80 % text ink, half a pixel inside the hex.</summary>
        public const float HoverRing = 1.5f;
        public const float HoverGrow = -0.5f;

        // ---- zoom ------------------------------------------------------------------------------

        public const float ZoomMin = 1f;
        public const float ZoomMax = 4f;
        public const float ZoomStep = 1.5f;

        /// <summary>Each change of zoom or pan eases out over this long.</summary>
        public const float ZoomEaseSeconds = 0.18f;

        public const int ZoomButton = 32;
        public const int ZoomReadoutHeight = 22;

        /// <summary>The zoom stack and the hint chip sit this far in from the map box's corners.</summary>
        public const int MapInset = 12;

        // ---- labels ----------------------------------------------------------------------------

        public const int LandLabelsMax = 7;
        public const int LandLabelMinTiles = 14;
        public const int SeaLabelsMax = 5;

        /// <summary>Sea labels keep at least this far apart, in 1× map pixels.</summary>
        public const float SeaLabelSpacing = 230f;

        /// <summary>No sea label within this many rows of either pole.</summary>
        public const int SeaLabelPolarRows = 4;

        // ---- legend and stats ------------------------------------------------------------------

        public const int LegendSwatch = 16;
        public const int LegendHill = 20;
        public const int LegendGap = 18;
        public const int StatsSwatch = 44;
        public const int StatsRowHeight = 30;
    }
}
