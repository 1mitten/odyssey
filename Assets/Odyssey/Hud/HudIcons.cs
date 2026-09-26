#nullable enable

namespace Odyssey.Hud
{
    /// <summary>
    /// Icons the interface draws as SVG paths on the 24-unit grid (design 39 §4), for the ones that
    /// are not the settings window's. Registered by icon key, so a surface asks for "home" and never
    /// holds a copy of the path.
    ///
    /// <para>The home glyph is Claude Design's (design 43 §5, 2026-09-25), shipped beside the brief
    /// as <c>docs/reference/mockups/home-glyph.svg</c>: a house, filled, drawn at 17 px on the
    /// views strip, 20 px in the Menu, 28 px over the hearth, 14 and 12 px on a campfire's
    /// pane.</para>
    /// </summary>
    public static class HudIcons
    {
        /// <summary>The house: a roof, two walls and a door, one closed shape. Filled, never stroked.</summary>
        public const string Home = "M12 2.5 1.5 11.5H4.5V21.5H10V15.5H14V21.5H19.5V11.5H22.5Z";

        /// <summary>
        /// The cycle mark on an Assign tab cell (design 43 §6): an open circle with an arrowhead,
        /// stroked at <see cref="CycleStroke"/> rather than the settings window's width, because at
        /// 11 px the thinner line breaks up.
        /// </summary>
        public const string Cycle = "M20 12a8 8 0 1 1-2.3-5.7M20 4v5h-5";

        /// <summary>The cycle mark's stroke, in path units.</summary>
        public const float CycleStroke = 2.4f;

        /// <summary>A pager's two chevrons (design 43 §6), stroked.</summary>
        public const string ChevronLeft = "M15 5 8 12l7 7";
        public const string ChevronRight = "M9 5l7 7-7 7";
        public const string ChevronUp = "M5 15l7-7 7 7";
        public const string ChevronDown = "M5 9l7 7 7-7";

        // ---- the bill list (design 49 §3), stroked unless it says otherwise

        /// <summary>A bolt struck through: the status strip's "no power".</summary>
        public const string NoPower = "M13 3 6 13h5l-1 8 7-10h-5zM3 3l18 18";

        /// <summary>The power switch's symbol: a stem in an open ring.</summary>
        public const string Power = "M12 3v8M7 6.3a7 7 0 1 0 10 0";

        /// <summary>The stepper's two marks, stroked at <see cref="StepStroke"/>.</summary>
        public const string Minus = "M5 12h14";
        public const string Plus = "M12 5v14M5 12h14";
        public const float StepStroke = 1.8f;

        /// <summary>Pause is two bars; resume is a triangle, filled.</summary>
        public const string PauseBars = "M8 5v14M16 5v14";
        public const string PlayTriangle = "M8 5l11 7-11 7z";

        public const string Bin = "M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13";

        /// <summary>Add a bill's plus, heavier because it sits on the accent fill.</summary>
        public const float AddStroke = 2.6f;

        /// <summary>The path registered under an icon key, or null for a key with none.</summary>
        public static string? PathOf(string iconKey) => iconKey switch
        {
            "home" => Home,
            "cycle" => Cycle,
            _ => null,
        };
    }
}
