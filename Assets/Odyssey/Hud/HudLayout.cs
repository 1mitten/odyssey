#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>A box on the HUD's canvas, top-left origin, in reference pixels.</summary>
    public readonly struct HudRect
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Width;
        public readonly float Height;

        public HudRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float Right => X + Width;
        public float Bottom => Y + Height;
        public float Area => Width <= 0f || Height <= 0f ? 0f : Width * Height;
        public bool Empty => Width <= 0f || Height <= 0f;

        /// <summary>
        /// True when two boxes share any area at all. A shared <i>edge</i> is not an overlap:
        /// panels are laid out to sit flush in a column and the strict comparison is what lets a
        /// zero-gap stack still pass.
        /// </summary>
        public bool Overlaps(HudRect other) =>
            !Empty && !other.Empty &&
            X < other.Right && other.X < Right &&
            Y < other.Bottom && other.Y < Bottom;

        public override string ToString() =>
            $"({X:0.#}, {Y:0.#}) {Width:0.#} x {Height:0.#}";
    }

    /// <summary>The regions the layout places. Debug names only — a player never sees one.</summary>
    public enum HudRegion
    {
        Stores,
        ColonistStrip,
        Clock,
        Alerts,
        DepthRail,
        Inspect,
        CommandBar,
    }

    /// <summary>
    /// What the HUD has to show right now — the few numbers a region's height depends on.
    ///
    /// <para>The layout is solved from this rather than from the live world so that the fast tier
    /// can place the whole screen at three resolutions without a Unity panel, which is the only
    /// way "no two HUD panels overlap at 1280x720, 1920x1080 or 2560x1440" becomes a test that
    /// runs in under two seconds. The shell feeds it the same numbers it is about to draw, and the
    /// PlayMode gate then measures the realised boxes and compares — so the model cannot quietly
    /// describe a screen the shell is not building.</para>
    /// </summary>
    public readonly struct HudContent
    {
        /// <summary>Colonists with a card in the top strip.</summary>
        public readonly int Colonists;

        /// <summary>Rows the stores panel is showing, after the disclosure has hidden the
        /// zero-stock ones.</summary>
        public readonly int StoreRows;

        /// <summary>Alerts raised. Zero hides the panel outright rather than printing a line
        /// saying there are none.</summary>
        public readonly int Alerts;

        /// <summary>Layers in the world, which is the depth rail's length.</summary>
        public readonly int Layers;

        /// <summary>Rows of needs in the inspect pane's two-column grid, or zero when nothing is
        /// selected and the pane is one dim line.</summary>
        public readonly int NeedRows;

        public HudContent(int colonists, int storeRows, int alerts, int layers, int needRows)
        {
            Colonists = Math.Max(0, colonists);
            StoreRows = Math.Max(0, storeRows);
            Alerts = Math.Max(0, alerts);
            Layers = Math.Max(1, layers);
            NeedRows = Math.Max(0, needRows);
        }

        /// <summary>The state the coverage criterion is stated against: a colony running, nothing
        /// selected.</summary>
        public static HudContent NothingSelected(int colonists, int storeRows, int layers) =>
            new HudContent(colonists, storeRows, alerts: 0, layers, needRows: 0);
    }

    /// <summary>
    /// Where every HUD region sits, solved from the viewport and the little that region heights
    /// depend on.
    ///
    /// <para><b>Why the geometry left the stylesheet.</b> Three times in this file's history two
    /// regions were given hand-picked offsets in the same corner and one grew into the other: the
    /// alerts panel was pinned 122 px from the top and the clock above it grew past that and
    /// buried the speed buttons; the overlay strip sat on the tab bar's right end and ate the last
    /// tab; the inspect pane cleared a bar of two rows by assuming a height for it. Each was
    /// fixed by a column, and each fix was correct and local. What none of them could do was
    /// answer the question "do any two panels overlap at this resolution", because the answer
    /// lived in a layout engine that only runs inside Unity with a panel attached.</para>
    ///
    /// <para>So the anchors are arithmetic here, the shell applies them, and
    /// <c>HudLayoutTests</c> asks that question directly at every resolution the acceptance
    /// criteria name. <b>Heights are still content's business</b> — the model is told how many
    /// rows a region has and computes the height the same way the stylesheet's padding and row
    /// sizes will produce, rather than dictating a height to a region that then has to be clipped
    /// to fit it.</para>
    ///
    /// <para><b>Anchored to edges, not to the canvas.</b> Left-hand regions are placed from the
    /// left, right-hand ones from the right, the bar and the strip from the centre, so the sheet
    /// is correct at any aspect and only the middle stretches.</para>
    /// </summary>
    public static class HudLayout
    {
        // ------------------------------------------------------------------ the canvas

        /// <summary>
        /// The canvas every number below is authored in.
        ///
        /// <para>The interface panel scales with the screen against this, so one reference pixel
        /// is one real pixel on a 1080p monitor and two on a 4K one. It is also what the
        /// <b>interface scale</b> setting divides: at 125 per cent the panel is told its reference
        /// is 1536 x 864, so everything drawn against these numbers comes out an eighth larger and
        /// the layout adapts around it — fewer colonist cards fit the strip, and the command bar
        /// moves its tail into Menu. Both of those are already tested at a literal 1280 x 720,
        /// which is exactly the canvas 150 per cent produces.</para>
        /// </summary>
        public const int ReferenceWidth = 1920;

        public const int ReferenceHeight = 1080;

        /// <summary>
        /// The reference resolution a panel should be given for an interface scale, as a
        /// percentage. Larger scale, smaller canvas, bigger everything.
        /// </summary>
        public static (int Width, int Height) ReferenceFor(int scalePercent)
        {
            int percent = Math.Max(10, scalePercent);
            return ((int)Math.Round(ReferenceWidth * 100.0 / percent),
                    (int)Math.Round(ReferenceHeight * 100.0 / percent));
        }

        // ------------------------------------------------------------------ space

        /// <summary>Screen edge to panel.</summary>
        public const int Edge = 20;

        /// <summary>Between two panels in the same column.</summary>
        public const int Gap = 9;

        /// <summary>Inside a panel, all four sides.</summary>
        public const int Pad = 12;

        /// <summary>A row in a list.</summary>
        public const int RowHeight = 29;

        /// <summary>An icon in a list row.</summary>
        public const int RowIcon = 17;

        /// <summary>Icon to label in a list row.</summary>
        public const int RowIconGap = 9;

        /// <summary>The header strip at the top of a panel: its label, and whatever sits opposite.</summary>
        public const int HeaderHeight = 20;

        /// <summary>Header to first row.</summary>
        public const int HeaderGap = 9;

        // ------------------------------------------------------------------ stores

        public const int StoresWidth = 288;

        // ------------------------------------------------------------------ colonist strip

        public const int CardWidth = 132;
        public const int CardHeight = 86;
        public const int CardGap = 7;

        /// <summary>The avatar tile on a card, and the selected-thing avatar in the inspect
        /// pane's header, which are the same size by specification.</summary>
        public const int Avatar = 30;

        /// <summary>The avatar tile on a roster card, which is smaller than the inspect one.</summary>
        public const int CardAvatar = 26;

        /// <summary>One of the three need bars stacked on a card.</summary>
        public const int CardBar = 3;

        // ------------------------------------------------------------------ clock and alerts

        public const int ClockWidth = 266;

        /// <summary>The clock's own baseline row: 24 px of mono with room to sit in.</summary>
        public const int ClockRow = 28;

        /// <summary>One of the four speed buttons.</summary>
        public const int SpeedButton = 26;

        /// <summary>Clock row to speed row.</summary>
        public const int ClockGap = 9;

        /// <summary>
        /// Two lines of 13 px body text.
        ///
        /// <para>Fifty-two rather than the thirty-four the point size suggests, because UI
        /// Toolkit's line box for 13 px type measures about 26 px rather than 17 — measured on the
        /// inspect pane's one-line empty state, which was the only text in the HUD left to size
        /// itself, and which came out nine pixels taller than the model had allowed the whole
        /// pane. Every other row in this layout declares its own height, so this is the only place
        /// the renderer's metrics reach the arithmetic.</para>
        /// </summary>
        public const int AlertHeight = 52;

        public const int AlertGap = 7;

        /// <summary>The alerts panel's own label row and the gap under it.</summary>
        public const int AlertHeaderBlock = HeaderHeight + HeaderGap;

        // ------------------------------------------------------------------ depth rail

        public const int RailWidth = 44;
        public const int RailCellWidth = 26;
        public const int RailCellHeight = 16;
        public const int RailCellGap = 6;

        /// <summary>The "R / F" hint under the rail.</summary>
        public const int RailHint = 14;

        /// <summary>The rail's label row, which is shorter than a panel header because it carries
        /// no control opposite it.</summary>
        public const int RailLabel = 14;

        /// <summary>Rail to the clock column, which is what puts the clock column at right:76.</summary>
        public const int RailToClock = 12;

        /// <summary>
        /// The rail's side padding, which is not <see cref="Pad"/>.
        ///
        /// <para>The spec asks for a 44 px rail holding 26 px cells. Twelve either side would
        /// leave twenty, so the cell would not fit its own panel. Four either side leaves
        /// thirty-six, the cells sit centred with five px of air, and the label is allowed its
        /// natural width inside the gutter rather than being clipped — the acceptance criteria
        /// forbid clipped text, and a hairline bleed into padding is not clipping.</para>
        /// </summary>
        public const int RailSidePad = 4;

        // ------------------------------------------------------------------ inspect

        public const int InspectWidth = 560;

        /// <summary>The pane's bottom edge, measured from the bottom of the screen. Sixteen pixels
        /// clear of the command bar, which cannot grow taller than one row.</summary>
        public const int InspectBottom = 84;

        /// <summary>
        /// The pane's header block: the 19 px name on one line and the 12 px job-and-state line
        /// under it, which together stand taller than the 30 px avatar beside them.
        /// </summary>
        public const int InspectHeader = 38;
        public const int InspectHeaderGap = 6;
        public const int InspectTabs = 26;
        public const int InspectTabGap = 9;

        /// <summary>One row of the two-column needs grid: its line of text and the bar under it.</summary>
        public const int NeedRow = 25;

        public const int NeedRowGap = 9;

        /// <summary>The single dim line shown when nothing is selected.</summary>
        public const int InspectEmptyLine = 17;

        // ================================================================== solve

        /// <summary>
        /// Place every region. Regions with nothing to show come back empty (zero width and
        /// height) rather than absent, so a caller can walk the whole enum without special cases
        /// and <see cref="Coverage"/> counts them as nothing.
        /// </summary>
        public static Dictionary<HudRegion, HudRect> Solve(float width, float height, HudContent content)
        {
            var boxes = new Dictionary<HudRegion, HudRect>();

            // ---- stores, top left
            float storesHeight = StoresHeight(content.StoreRows);
            boxes[HudRegion.Stores] = new HudRect(Edge, Edge, StoresWidth, storesHeight);

            // ---- depth rail, the outermost right-hand column
            float railHeight = RailHeight(height, content.Layers);
            boxes[HudRegion.DepthRail] =
                new HudRect(width - Edge - RailWidth, Edge, RailWidth, railHeight);

            // ---- clock, just inside the rail
            float clockX = width - Edge - RailWidth - RailToClock - ClockWidth;
            boxes[HudRegion.Clock] = new HudRect(clockX, Edge, ClockWidth, ClockHeight);

            // ---- alerts, under the clock in the same column, hidden when there are none
            boxes[HudRegion.Alerts] = content.Alerts <= 0
                ? default
                : new HudRect(clockX, Edge + ClockHeight + Gap, ClockWidth, AlertsHeight(content.Alerts));

            // ---- colonist strip, centred in what is left between the two top corners
            int cards = VisibleCards(width, content.Colonists);
            if (cards <= 0) boxes[HudRegion.ColonistStrip] = default;
            else
            {
                float stripWidth = cards * CardWidth + (cards - 1) * CardGap;
                boxes[HudRegion.ColonistStrip] =
                    new HudRect((width - stripWidth) * 0.5f, Edge, stripWidth, CardHeight);
            }

            // ---- inspect, bottom left, clear of the bar
            float inspectHeight = InspectHeight(content.NeedRows);
            boxes[HudRegion.Inspect] =
                new HudRect(Edge, height - InspectBottom - inspectHeight, InspectWidth, inspectHeight);

            // ---- command bar, bottom centre
            float barWidth = HudCommands.BarWidth(HudCommands.ModelWidths(), FittedCommands(width));
            // Plus its own hairlines, which HudCommands.BarHeight does not carry: that constant is
            // the bar's content and padding, and it is what the overflow arithmetic works in.
            float barHeight = HudCommands.BarHeight + Frame;
            boxes[HudRegion.CommandBar] = new HudRect(
                (width - barWidth) * 0.5f,
                height - Edge - barHeight,
                barWidth,
                barHeight);

            return boxes;
        }

        // ---------------------------------------------------------------- heights

        /// <summary>
        /// A panel's own top and bottom hairlines. UI Toolkit sizes elements border-box, so a
        /// panel's declared width already contains them; its height, which is content-driven,
        /// does not. Two pixels a panel is small, and leaving them out would make the model
        /// disagree with the realised boxes by a constant — which is exactly the sort of drift
        /// the PlayMode comparison exists to catch, so it may as well be right.
        /// </summary>
        public const int Frame = 2 * HudTheme.BorderWidth;

        public static float StoresHeight(int rows) =>
            Frame + Pad + HeaderHeight + HeaderGap + Math.Max(0, rows) * RowHeight + Pad;

        public const float ClockHeight = Frame + Pad + ClockRow + ClockGap + SpeedButton + Pad;

        public static float AlertsHeight(int alerts) =>
            alerts <= 0 ? 0f : Frame + Pad + AlertHeaderBlock + alerts * AlertHeight +
                               (alerts - 1) * AlertGap + Pad;

        /// <summary>
        /// The rail's chrome: everything that is not a cell.
        /// </summary>
        public const float RailChrome = Frame + Pad + RailLabel + RailHint + Pad;

        /// <summary>
        /// The smallest a cell and its gap may be squeezed to. Four pixels of cell is still a
        /// thing a player can hit, which is what <c>HudSmokeTests.EveryLayerHasAStepThatCanBeHit</c>
        /// is there to insist on.
        /// </summary>
        public const float MinRailPitch = 6f;

        /// <summary>
        /// One cell plus its gap. The rail is the one region whose length is set by the world
        /// rather than by the interface — sixteen layers today, and the generator can be asked for
        /// more — so it is also the one that has to give. A sixteen-layer board at any resolution
        /// this game runs at gets the full 26x16 cells the specification asks for; a thirty-two
        /// layer board on a short screen gets proportionally smaller ones rather than a rail that
        /// runs off the bottom of the screen, or (worse) one that silently loses its last layers,
        /// which was a real playtest report on 2026-09-16.
        /// </summary>
        public static float RailPitch(float viewportHeight, int layers)
        {
            float ideal = RailCellHeight + RailCellGap;
            if (layers <= 0) return ideal;

            // The rail runs from the top margin down to the command bar, not to the bottom of
            // the screen. The bar is centred and wide enough at every resolution this game runs
            // at to reach under the right-hand column, so a rail measured against the screen edge
            // runs behind it — which is how a thirty-two layer board at 720p put its deepest
            // layers under the Build button.
            float room = viewportHeight - Edge - RailChrome - RailCellGap
                         - (Edge + HudCommands.BarHeight + Frame + Gap);
            return Math.Max(MinRailPitch, Math.Min(ideal, room / layers));
        }

        /// <summary>The gap between two cells at a given pitch, kept in the proportion the
        /// specification draws it at.</summary>
        public static float RailGap(float pitch) =>
            pitch * RailCellGap / (RailCellHeight + RailCellGap);

        /// <summary>The height of one cell at a given pitch.</summary>
        public static float RailCell(float pitch) => pitch - RailGap(pitch);

        public static float RailHeight(float viewportHeight, int layers)
        {
            float pitch = RailPitch(viewportHeight, layers);
            return RailChrome + layers * pitch + RailGap(pitch);
        }

        /// <summary>
        /// The pane's height. With nothing selected it is one dim line — the acceptance criteria
        /// replace the old three-sentence empty state, and an empty panel that is still 302 px
        /// tall is most of why the HUD covered a third of the screen.
        /// </summary>
        public static float InspectHeight(int needRows) =>
            needRows <= 0
                ? Frame + Pad + InspectEmptyLine + Pad
                : Frame + Pad + InspectHeader + InspectHeaderGap + InspectTabs + InspectTabGap +
                  needRows * NeedRow + (needRows - 1) * NeedRowGap + Pad;

        // ---------------------------------------------------------------- fitting

        /// <summary>
        /// The space the colonist strip may occupy.
        ///
        /// <para><b>It is not the gap between the two top corners, and that difference is a
        /// bug this test caught.</b> The strip is centred on the <i>screen</i>, while the panels
        /// either side of it are different widths — 288 of stores on the left against 322 of rail
        /// and clock on the right. So the free span's middle sits seventeen pixels left of the
        /// screen's middle at 1920, and a strip sized to the whole span and then centred pokes
        /// into the clock column by seventeen pixels however careful the arithmetic looked. What
        /// the strip may actually have is twice the smaller of its two clearances.</para>
        /// </summary>
        public static float StripRoom(float width)
        {
            float centre = width * 0.5f;
            float freeLeft = Edge + StoresWidth + Gap;
            float freeRight = width - (Edge + RailWidth + RailToClock + ClockWidth) - Gap;
            return 2f * Math.Min(centre - freeLeft, freeRight - centre);
        }

        /// <summary>
        /// How many cards are drawn. A colony larger than the strip can hold shows the ones that
        /// fit; the shell marks the last card with the remainder. Clamping here rather than
        /// letting the strip grow is what keeps "no two panels overlap" true by construction at
        /// any width, rather than true for the colony sizes somebody happened to test.
        /// </summary>
        public static int VisibleCards(float width, int colonists)
        {
            if (colonists <= 0) return 0;
            float room = StripRoom(width);
            int fits = (int)Math.Floor((room + CardGap) / (CardWidth + CardGap));
            return Math.Max(0, Math.Min(colonists, fits));
        }

        /// <summary>How many command-bar items fit at this width, by the modelled widths.</summary>
        public static int FittedCommands(float width) =>
            HudCommands.Fit(HudCommands.ModelWidths(), width - 2 * Edge);

        // ---------------------------------------------------------------- acceptance

        /// <summary>
        /// The first pair of regions that share any area, or null when none do. Returns the pair
        /// rather than a boolean because a failing test that cannot name the two panels is a test
        /// somebody has to debug by eye.
        /// </summary>
        public static (HudRegion A, HudRegion B)? FirstOverlap(IDictionary<HudRegion, HudRect> boxes)
        {
            var regions = new List<HudRegion>(boxes.Keys);
            regions.Sort();
            for (int i = 0; i < regions.Count; i++)
                for (int j = i + 1; j < regions.Count; j++)
                    if (boxes[regions[i]].Overlaps(boxes[regions[j]]))
                        return (regions[i], regions[j]);
            return null;
        }

        /// <summary>
        /// The fraction of the viewport the panels cover.
        ///
        /// <para><b>The scrims are not counted, and that is a decision rather than an
        /// oversight.</b> Both are always on and they are 370 rows between them — over a third of
        /// the screen — but they are transparent gradients whose whole job is to carry text
        /// contrast so that the panels can stay small. Counting them would make the target
        /// unreachable by construction and would be measuring the opposite of what the criterion
        /// is about, which is how much of the board the interface hides.</para>
        /// </summary>
        public static float Coverage(IDictionary<HudRegion, HudRect> boxes, float width, float height)
        {
            float area = 0f;
            foreach (HudRect box in boxes.Values) area += box.Area;
            return area / (width * height);
        }

        /// <summary>The ceiling the acceptance criteria put on <see cref="Coverage"/> with nothing
        /// selected. The HUD this replaces measured about 0.31.</summary>
        public const float CoverageCeiling = 0.18f;
    }
}
