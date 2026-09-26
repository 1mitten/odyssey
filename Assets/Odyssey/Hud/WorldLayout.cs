#nullable enable

namespace Odyssey.Hud
{
    /// <summary>
    /// The World screen's measurements, from Claude Design's specification
    /// (<c>docs/reference/mockups/world-screen-spec.md</c>, "Constants"). One place, so the page, the
    /// painter and the tests read the same numbers — and <c>HudStyleSheetTests</c> holds every one that
    /// meets a stylesheet to it (its <c>Lengths</c> table), the rule the Work tab's border-box fault
    /// made. The frame's padding is the setup page's own <c>.setup</c> rule and is not restated here.
    /// </summary>
    public static class WorldLayout
    {
        // ---- the frame -------------------------------------------------------------------------

        /// <summary>The page title, "World": 30 px, bigger than the setup page's 19 (owner, 2026-09-26).</summary>
        public const int TitleSize = 30;
        public const int TitleHeight = 38;

        /// <summary>The page's inset from every screen edge.</summary>
        public const int Inset = 24;

        /// <summary>The stats panel's width, and the gap between it and the map.</summary>
        public const int StatsWidth = 340;
        public const int BodyGap = 24;

        /// <summary>Controls on the top row and in the footer are this tall; footer buttons at least this wide.</summary>
        public const int ControlHeight = 36;
        public const int FooterButtonMinWidth = 160;
        public const int SeedFieldWidth = 200;

        // ---- the map ---------------------------------------------------------------------------

        /// <summary>128 x 64, four times the first planet (owner, 2026-09-26: "it needs to increase at least 4x"; design 59 §4e).</summary>
        public const int MapColumns = 128;
        public const int MapRows = 64;

        /// <summary>A hex, flat side to flat side, at 1×.</summary>
        public const float HexWidth = 18.4f;

        /// <summary>Rows sit this fraction of a hex's height apart.</summary>
        public const float HexRowStep = 0.75f;

        /// <summary>
        /// The texture is painted at this multiple of its 1× size (design 59 §9a), so a deep zoom is not
        /// a blur. 1.5 since the planet went to 128 x 64 (§4e): at 2x the paint was 332 ms in a Debug
        /// build against 203 at 1.5, once per seed; measured in §9d.
        /// </summary>
        public const float PaintScale = 1.5f;

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
        /// <summary>Twice the first planet's 4x, because its hexes are half the size at the fit: a hex at 8x is the size it was at 4x.</summary>
        public const float ZoomMax = 8f;
        public const float ZoomStep = 1.5f;

        /// <summary>Each change of zoom or pan eases out over this long.</summary>
        public const float ZoomEaseSeconds = 0.18f;

        public const int ZoomButton = 32;
        public const int ZoomReadoutHeight = 22;

        /// <summary>The zoom stack and the hint chip sit this far in from the map box's corners.</summary>
        public const int MapInset = 12;

        // ---- labels ----------------------------------------------------------------------------

        public const int LandLabelsMax = 12;
        public const int LandLabelMinTiles = 40;
        public const int SeaLabelsMax = 8;

        /// <summary>Sea labels keep at least this far apart, in 1× map pixels.</summary>
        public const float SeaLabelSpacing = 380f;

        /// <summary>No sea label within this many rows of either pole.</summary>
        public const int SeaLabelPolarRows = 8;

        // ---- legend and stats ------------------------------------------------------------------

        public const int LegendSwatch = 16;
        public const int LegendHill = 20;
        public const int LegendGap = 18;
        public const int StatsSwatch = 44;
        public const int StatsRowHeight = 30;
    }
}
