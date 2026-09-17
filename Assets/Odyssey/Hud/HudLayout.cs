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

        /// <summary>
        /// Rows of skills in the same grid, when the pane is showing the Skills tab instead. One
        /// tab is on screen at a time, so at most one of this and <see cref="NeedRows"/> is ever
        /// non-zero — the pane is as tall as the body it is actually showing.
        /// </summary>
        public readonly int SkillRows;

        /// <summary>
        /// Rows of the cell readout, when a tile (or a pile on one) is selected instead of a
        /// colonist. The readout pane is narrower than the colonist pane — half its width — and
        /// one fact per row, so the same fact is always in the same column (owner, 2026-09-17:
        /// the joined line made every number hunt for its label).
        /// </summary>
        public readonly int CellRows;

        public HudContent(int colonists, int storeRows, int alerts, int layers, int needRows,
                          int skillRows = 0, int cellRows = 0)
        {
            Colonists = Math.Max(0, colonists);
            StoreRows = Math.Max(0, storeRows);
            Alerts = Math.Max(0, alerts);
            Layers = Math.Max(1, layers);
            NeedRows = Math.Max(0, needRows);
            SkillRows = Math.Max(0, skillRows);
            CellRows = Math.Max(0, cellRows);
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

        /// <summary>
        /// Screen edge to panel: **nothing** (owner, 2026-09-17, "make the time control align
        /// with the top alignment of the rostering so it's closer to the edge of the screen — the
        /// same goes to stores — less padding and spacing close to screen edge").
        ///
        /// <para>Twenty until then, and the last thing on the screen still holding a margin. The
        /// colonist strip and the command bar were docked to their edges the day before, so the
        /// clock sat twenty pixels below a strip beside it that started at zero — the two read as
        /// misaligned because they were. Docking the rest is what makes the strip's own docking
        /// look deliberate rather than like one panel that had slipped.</para>
        ///
        /// <para><b>The breathing room did not go anywhere; it moved inside.</b> A panel's
        /// <see cref="Pad"/> is still twelve on all four sides, so text is no nearer the screen
        /// edge than it was — what is gone is the strip of world between a panel's border and the
        /// edge, which was carrying no information and cost every corner of the screen twenty
        /// pixels in both directions.</para>
        ///
        /// <para><b>It is also forty pixels of strip room</b>, because <see cref="StripRoom"/>
        /// measures from the panels either side.</para>
        /// </summary>
        public const int Edge = 0;

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

        /// <summary>
        /// How wide the stores panel is.
        ///
        /// <para><b>288 was never measured</b> (owner, 2026-09-16: "far too wide"). The widest
        /// line in the panel is its own header — "STORES" against the "n / m" count — and
        /// <c>TheStoresPanelIsWideEnoughForItsRowsAndNoWider</c> measures it at <b>128 px</b> with
        /// the real face at the real size, against commodity names of four to six characters. So
        /// more than half the panel was empty column, and an empty column over bright terrain
        /// still has to be dark enough to hold text: it was paying the contrast cost of a panel
        /// while showing nothing.</para>
        ///
        /// <para><b>The forty px on top is for figures, not for comfort.</b> The measurement is
        /// taken against whatever the colony happens to hold, and a fresh board holds two-digit
        /// counts; a stocked one holds five-digit ones, which is about 32 px more of mono. The
        /// test's upper bound allows 48 px of headroom over what it measured, so a name long
        /// enough to need more than this will fail the lower bound and say so with a figure.</para>
        /// </summary>
        public const int StoresWidth = 168;

        // ------------------------------------------------------------------ colonist strip

        /// <summary>
        /// How wide a roster card is.
        ///
        /// <para><b>132 x 86 until 2026-09-17</b>, when the owner took the three need bars off it
        /// and asked for it tightened so the bar carries many more colonists. What is left is two
        /// rows — who this is, and what they are at — so the card is sized by the longer of them
        /// rather than by a number somebody liked.</para>
        ///
        /// <para>Widest name row: the avatar (26) plus its gap (8) plus the longest name the pool
        /// can produce. Widest activity row: the icon (17) plus its gap (6) plus the longest word
        /// in <c>ui.status</c>. Both plus padding on each side. The figures are not taken on
        /// trust — <c>TheCardIsWideEnoughForItsRowsAndNoWider</c> asks the text engine what those
        /// strings really draw in the real face at the real size, and fails on either side with a
        /// number attached, exactly as the stores panel's own width test does.</para>
        ///
        /// <para>The name is the one thing on this screen allowed an ellipsis, so a future name
        /// longer than the pool's does not break the card — but it does mean a card whose name is
        /// cut short, which is why the test's lower bound exists rather than only its upper.</para>
        /// </summary>
        public const int CardWidth = 106;

        /// <summary>
        /// How tall a roster card is: padding, the avatar row, the activity line, padding. The
        /// avatar row takes the slack, so the gap between the two rows is what is left rather
        /// than a fourth number to keep in step.
        /// </summary>
        public const int CardHeight = 63;

        public const int CardGap = 7;

        /// <summary>
        /// How far the colonist strip sits from the top of the screen, and the command bar from
        /// the bottom: **nothing at all** (owner, 2026-09-17, "directly at the bottom of the
        /// screen, no spacing, padding, to maximise viewing space — dock it to the bottom and
        /// apply the same to the roster bar").
        ///
        /// <para>Both are deliberately <see cref="Edge"/> no longer. The stores panel, the clock
        /// and the depth rail keep their margin, because they are panels that sit *in* the view;
        /// these two are bars that bound it, and a bar with a strip of world under it reads as
        /// floating rather than as the edge of the screen. Docking also buys back the one thing
        /// the strip is short of, which is room to grow a second row into.</para>
        /// </summary>
        public const int StripTop = 0;

        /// <inheritdoc cref="StripTop"/>
        public const int BarBottom = 0;

        /// <summary>
        /// How many rows of cards the strip may run to.
        ///
        /// <para>Two, not unbounded: the strip is the one region with no ceiling of its own — a
        /// colony of fifty would paper the screen — and the clamp is what keeps "no two regions
        /// overlap" and the coverage ceiling true by construction rather than true for the colony
        /// sizes somebody happened to test. A colony past what two rows hold shows the ones that
        /// fit, exactly as it did at one row.</para>
        /// </summary>
        public const int StripRows = 2;

        /// <summary>
        /// The most of the viewport's height the colonist strip may stand in.
        ///
        /// <para><b>A second row has to be earned, and this is the rule that earns it.</b> Two
        /// full rows of cards come to 8.1% of a 1280 x 720 screen against 6.5% of a 2560 x 1440
        /// one, and at 720p that took the resting HUD to <b>21.7%</b>, over the 18% ceiling the
        /// acceptance criteria set. A row cap that ignored the screen would have bought capacity
        /// on a small screen by spending the one budget the interface is actually held to.</para>
        ///
        /// <para>So the strip may grow while it stays inside this share of the height, which
        /// gives <b>one row at 720p and two at 1080p and above</b> — measured, not chosen by
        /// resolution: 0.14 of 720 is 101 px against the 133 two rows need, and 0.14 of 1080 is
        /// 151. A colony past what the strip may hold shows the ones that fit, exactly as it did
        /// when there was only ever one row.</para>
        /// </summary>
        public const float StripHeightShare = 0.14f;

        /// <summary>The avatar tile on a card, and the selected-thing avatar in the inspect
        /// pane's header, which are the same size by specification.</summary>
        public const int Avatar = 30;

        /// <summary>The avatar tile on a roster card, which is smaller than the inspect one.</summary>
        public const int CardAvatar = 26;

        /// <summary>Avatar to name on a card's identity row.</summary>
        public const int CardAvatarGap = 8;

        /// <summary>Inside a card, all four sides. Tighter than a panel's <see cref="Pad"/>.</summary>
        public const int CardPad = 8;

        /// <summary>
        /// The activity line on a card: a picture of what this colonist is doing, and the word
        /// for it. One pixel taller than the 16 px line of text it replaces, because the row is
        /// sized by the taller of the two things in it and that is now the icon.
        /// </summary>
        public const int CardJobRow = RowIcon;

        /// <summary>
        /// Icon to word on that line, tighter than a list row's <see cref="RowIconGap"/>. A card
        /// is 132 px wide against the stores panel's 168, and the word beside it has to be a
        /// whole word: an ellipsis is allowed on a colonist's name and on nothing else.
        /// </summary>
        public const int CardIconGap = 6;

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

        // ------------------------------------------------------------------ build palette

        /*
         * The Build palette, in three layouts (`docs/design/17-build-palette-layouts.md`).
         *
         * Every number below is from the owner's 4a/4b/4c specification, and the arrangement is
         * the one place the specification was overruled. It drew all three floating at a 28 px
         * margin with the panel's own top-left corner in the top-left of the screen — over a bare
         * board, with no HUD behind it. The real screen has the stores panel down the left edge
         * and the roster strip across the top, and a panel there covers both. The owner's answer,
         * asked directly: "tight and flush to other elements to enable full use of space". So the
         * margins are gone, Rows and Bar span the screen edge to edge, and all three dock flush on
         * whatever is under them, which is the rule the bar and the popovers already follow.
         */

        /// <summary>
        /// How wide Rail is, and the one layout that keeps a width of its own.
        ///
        /// <para>Rows and Bar span the screen, because their whole argument is that a row of seven
        /// tiles should use the horizontal space rather than wrap. Rail's argument is the opposite
        /// one — <i>its height never changes when you switch category, so nothing below it
        /// reflows</i> — and that only holds if the pane beside the rail is a fixed size.</para>
        /// </summary>
        public const int BuildRailWidth = 840;

        /// <summary>The 186 px column of category rows down Rail's left edge.</summary>
        public const int BuildRailColumn = 186;

        /// <summary>A category row in Rail: 34 px, an 18 px icon and a label.</summary>
        public const int BuildRailRow = 34;

        /// <summary>The inset bar in the selected Rail row's own hue, on its left edge.</summary>
        public const int BuildRailMark = 2;

        /// <summary>A sub-type tile in Rail's four-column grid.</summary>
        public const int BuildRailSubTile = 66;

        /// <summary>A material tile in Rail's four-column grid. The one place a material is drawn
        /// large enough for its name to be the loudest thing on the button.</summary>
        public const int BuildRailMatTile = 86;

        /// <summary>How many columns Rail's two grids are wide.</summary>
        public const int BuildRailColumns = 4;

        /// <summary>
        /// How many rows Rail's sub-type grid stands, whatever category is open.
        ///
        /// <para><b>This is the number that makes Rail worth having.</b> Its whole claim is that
        /// the panel's height never changes when you switch category, so nothing below it
        /// reflows — and the grid is the only part of it that would otherwise vary, because
        /// Structure has six sub-types and Security has three. Sized to the largest category and
        /// left ragged for the rest, so the promise is kept by the layout rather than by the
        /// contents happening to be even.</para>
        ///
        /// <para>It was not kept at first: the panel measured 376 px on Structure and 343 on
        /// Production, and the PlayMode test written for the claim is what said so.
        /// <see cref="BuildRailRowsNeeded"/> is checked against the real table in the fast tier,
        /// so a seventh tool in a category fails loudly rather than quietly reflowing.</para>
        /// </summary>
        public const int BuildRailSubRows = 2;

        /// <summary>How tall that grid stands.</summary>
        public const int BuildRailSubGrid =
            BuildRailSubRows * BuildRailSubTile + (BuildRailSubRows - 1) * BuildTileGap;

        /// <summary>
        /// How tall Rail's material grid stands, whether or not it has anything in it.
        ///
        /// <para><b>Reserved rather than collapsed, and this was the second half of the same
        /// bug.</b> Fixing the sub-type grid was not enough: a category that opens on a tool which
        /// is not built out of anything — which is five of the seven, because their tools are all
        /// still drawn-disabled — hides the material buttons, and a hidden row takes its height
        /// with it. Measured: the pane stood 317 px on Structure and 290 on Production, and the
        /// panel followed. So the material band keeps its row whether it is showing buttons or
        /// not.</para>
        ///
        /// <para>The cost is an empty band under the word MATERIAL while an order is armed. That
        /// is the honest price of the promise this layout is built on, and it is only visible in
        /// Rail: Rows and Bar let the band collapse, because neither of them ever claimed its
        /// height would hold still.</para>
        /// </summary>
        public const int BuildRailMatGrid = BuildRailMatTile + BuildTileGap;

        /// <summary>How many rows the largest category really needs, for the test that holds
        /// <see cref="BuildRailSubRows"/> to it.</summary>
        public static int BuildRailRowsNeeded(int largestCategory) =>
            (largestCategory + BuildRailColumns - 1) / BuildRailColumns;

        // --- Rows

        /// <summary>
        /// How wide the default layout is.
        ///
        /// <para><b>A column down the left, not a band across the screen</b> (owner, 2026-09-17:
        /// <i>"make the 1st group of buttons short width as possible but evenly sized … you could
        /// probably fit 4 on a row but increase the height and try to use the left hand side of
        /// the screen instead of the width"</i>). It spanned the full width first, which is what
        /// the mockup drew and what "use the horizontal space" had asked for while the palette was
        /// ten wrapping chips. Seven tiles stretched across 1920 are seven very wide tiles with a
        /// small icon adrift in each, and the board they hide is the board the player is aiming
        /// at.</para>
        ///
        /// <para>The number is the narrowest that holds four category tiles: the band's 14 px of
        /// padding each side, four tiles at 23% of what is left, and a 6 px gap after each.
        /// <c>Recreation</c> is the longest label and sets the floor; anything narrower clips it
        /// or drops to three across.</para>
        /// </summary>
        public const int BuildRowsWidth = 372;

        /// <summary>How many category tiles Rows fits across. Seven of them therefore stand two
        /// rows deep, which is where the height the owner asked for comes from.</summary>
        public const int BuildRowsColumns = 4;

        /// <summary>A category tile in the Rows band: a 20 px icon over a label, both centred.</summary>
        public const int BuildCatTile = 62;

        /// <summary>A sub-type button in the Rows band.</summary>
        public const int BuildSubRow = 34;

        /// <summary>
        /// How tall the Rows sub-type band stands, whatever category is open (owner, 2026-09-17:
        /// <i>"the height needs to stay fixed — ie as tall as the structure menu/selection goes so
        /// it can accommodate all of the menus"</i>).
        ///
        /// <para><b>Measured, not derived, and that is the difference from Rail.</b> Rail's grid is
        /// four columns, so its row count is arithmetic on the number of tools and a test can check
        /// the constant against the table. Rows wraps its sub-types by how wide their <i>words</i>
        /// are — "Roof and floor above" takes a line to itself — which is a fact about the text
        /// engine that no Unity-free assembly can compute. So this is the figure the band actually
        /// measured on the widest category, and <c>TheRowsLayoutKeepsItsHeightWhateverCategoryIsOpen</c>
        /// prints every category's band on every run so it can be re-derived from the output rather
        /// than guessed at a second time.</para>
        ///
        /// <para>Structure is the widest, at three rows: 24 px of band padding and three 40 px
        /// pitches. It is also the category the owner named.</para>
        /// </summary>
        public const int BuildRowsSubBand = 144;

        /// <summary>
        /// A material button in the Rows band — the same box as a sub-type button beside it.
        ///
        /// <para><b>40 px with a 19/600 label until 2026-09-17</b>, when the owner asked for the
        /// materials <i>"evenly sized in font and size as the other buttons but keep the style"</i>.
        /// The specification had made them the loudest thing in the panel, on the argument that a
        /// material is the terminal choice; in a narrow column that reads as two buttons of a
        /// different kind rather than as the last tier of one control. What carries "terminal" is
        /// the tint, the doubled border and the seated shadow — the style the owner kept — and none
        /// of those needed the extra eight pixels and the heavier type.</para>
        ///
        /// <para>Rail is deliberately not changed: its 86 px grid is the whole shape of that
        /// layout rather than a row in it.</para>
        /// </summary>
        public const int BuildMatRow = BuildSubRow;

        // --- Bar

        /// <summary>An icon-only category tile in Bar.</summary>
        public const int BuildBarCat = 36;

        /// <summary>An icon-only sub-type tile in Bar. Larger than the category above it, because
        /// it is the tier the player is actually aiming at.</summary>
        public const int BuildBarSub = 42;

        // --- shared

        /// <summary>The gap between two tiles, in every layout and every tier.</summary>
        public const int BuildTileGap = 6;

        /// <summary>A band's padding: 12 down, 14 across.</summary>
        public const int BuildBandPadY = 12;

        public const int BuildBandPadX = 14;

        /// <summary>A header action button — the four pinned tools and the way out.</summary>
        public const int BuildAction = 26;

        /// <summary>One button of the layout switcher.</summary>
        public const int BuildSwitchButton = 22;

        /// <summary>
        /// The gap between two icons and the label beside them, which the specification fixes at
        /// "the same value on every labelled button, no exceptions".
        ///
        /// <para>It is the same 9 px <see cref="RowIconGap"/> the rest of the HUD already uses, so
        /// the exception the specification was guarding against cannot arise: there is one number
        /// and it was already here.</para>
        /// </summary>
        public const int BuildIconGap = RowIconGap;

        // ------------------------------------------------------------------ inspect

        public const int InspectWidth = 560;

        /// <summary>
        /// The tile readout's width: half the colonist pane's. The tile pane says short facts in
        /// rows and needs no tabs, needs or skills, so it takes a column rather than a band
        /// (owner, 2026-09-17).
        /// </summary>
        public const int InspectNarrowWidth = InspectWidth / 2;

        /// <summary>One fact row of the cell readout: a label and its value on one line.</summary>
        public const int CellRow = 20;

        public const int CellRowGap = 4;

        /// <summary>
        /// The label column of the readout. A width rather than a gap so the values line up
        /// whatever the labels are — "walk speed" and "support" both start their values at the
        /// same x, which is what makes the column predictable.
        /// </summary>
        public const int CellRowName = 92;

        /// <summary>The pane's clearance over the command bar, which cannot grow taller than one row.</summary>
        public const int InspectToBar = 14;

        /// <summary>
        /// The pane's bottom edge, measured from the bottom of the screen.
        ///
        /// <para>Derived rather than written down, since the bar was docked: the pane sits a fixed
        /// clearance above whatever the bar's top edge is, and a hand-set 84 would have left it
        /// hanging twenty pixels over a bar that had moved down. That coupling was in the old
        /// stylesheet as a comment admitting it was one.</para>
        /// </summary>
        public const int InspectBottom = BarBottom + HudCommands.BarHeight + Frame + InspectToBar;

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

        /// <summary>
        /// One line of the Skills tab: an icon, a name, a level and a passion mark, on one row of
        /// the same two-column grid the needs use. Thirteen skills in one column would make a
        /// pane 280 px taller than the screen has to spare over the command bar, and would leave
        /// half of a 560 px pane empty while doing it.
        /// </summary>
        public const int SkillRow = 19;

        public const int SkillRowGap = 4;

        // ------------------------------------------------------------------ the start screen

        /// <summary>
        /// How wide the start screen's column is.
        ///
        /// <para>**420 since the owner played it** (2026-09-17), up from 320. A save row carries the
        /// colony's name over a line that has to distinguish it from every other save of the same
        /// colony — the day, the board, and now the date and time it was written — and at 320 that
        /// line had to be cut somewhere. The acceptance criteria allow an ellipsis on a colonist's
        /// name and on nothing else, so the panel widens rather than the words shortening.</para>
        /// </summary>
        public const int StartWidth = 420;

        /// <summary>
        /// The title block: the game's own name, set at <see cref="HudTextRole.Name"/>.
        ///
        /// <para>The type scale is closed at six steps and a seventh would fail
        /// <c>HudTypeTests</c>, so the largest word-face step in the interface is what a title
        /// gets. It is the same size as a colonist's name in the inspect pane, which is smaller
        /// than a title usually is and is the honest consequence of having a scale at all: a
        /// bigger one is a deliberate change to <see cref="HudType"/>, not a literal written
        /// here.</para>
        /// </summary>
        public const int StartTitle = 26;

        /// <summary>Title to the first row.</summary>
        public const int StartTitleGap = 14;

        /// <summary>
        /// One row of the start screen — deliberately <see cref="RowHeight"/>, the same row the
        /// stores panel, the Menu popover and the settings panel are all built from. The start
        /// screen introduces no new control; it arranges the ones the interface already has.
        /// </summary>
        public const int StartRow = RowHeight;

        public const int StartRowGap = 4;

        /// <summary>
        /// One row of the load list: a colony's name on one line and the line that identifies it —
        /// day, board, when it was written, and for a file this build cannot open, why — under it.
        /// Two lines, so it stands taller than the plain row above.
        /// </summary>
        public const int StartSaveRow = 44;

        public const int StartSaveGap = 4;

        /// <summary>
        /// The start screen's body: everything under the title, and <b>a fixed height whatever
        /// screen is showing</b>.
        ///
        /// <para><b>Fixed because the owner asked for it after playing</b> (2026-09-17): *"keep it
        /// fixed width and height because it becomes hard to read between loading and saving
        /// screens"*. The panel used to size itself to its content, so the root screen's four rows
        /// and the load screen's list gave two quite different boxes — and since the panel is
        /// centred, moving between them moved every row under the pointer. A menu whose items walk
        /// away as you navigate is a menu you have to re-find each time.</para>
        ///
        /// <para>The number is six save rows and the row that goes back, which is what the load
        /// screen needs before it scrolls; the root screen's four rows sit at the top of the same
        /// box and leave the rest as air. Air is the cheaper of the two mistakes — the alternative
        /// is a list that scrolls at four.</para>
        /// </summary>
        public const int StartBody =
            6 * StartSaveRow + 5 * StartSaveGap                     // the list before it scrolls
            + StartRowGap + HudTheme.BorderWidth + StartRow;        // the row that goes back

        /// <summary>The start screen's height: the same for every screen it shows.</summary>
        public const float StartPanelHeight =
            Frame + Pad + StartTitle + StartTitleGap + StartBody + Pad;

        // ------------------------------------------------------------------ the naming prompt

        /// <summary>
        /// The prompt that names a save. The start screen's width, because a save's name is as long
        /// as a save's row and the two are read one after the other.
        /// </summary>
        public const int PromptWidth = StartWidth;

        /// <summary>
        /// A text field's box — the first control of its kind in this interface.
        ///
        /// <para>Thirty rather than the <see cref="RowHeight"/> of twenty-nine, because a field is
        /// a thing you click into and type in rather than a line you read, and the one pixel is the
        /// border it carries that a row does not.</para>
        /// </summary>
        public const int FieldHeight = 30;

        /// <summary>
        /// How tall the load list may stand before it scrolls: whatever the fixed body leaves once
        /// the row that goes back has taken its share.
        ///
        /// <para><b>The first region in this interface with a real ceiling on it</b>, because it is
        /// the first whose length is set by the player rather than by the game: a folder can hold
        /// any number of saves. The Keys tab was already noted in <c>CLAUDE.md</c> as the panel
        /// that would want this first; the load list simply got here before it, and the scroller
        /// it uses is the one the Build palette already restyled.</para>
        ///
        /// <para>Derived rather than written down since the panel's height was fixed, because a
        /// ceiling that disagreed with the box it sits in is a list that either scrolls early or
        /// runs off the bottom.</para>
        /// </summary>
        public const int StartListMax = StartBody - StartRowGap - HudTheme.BorderWidth - StartRow;

        // ------------------------------------------------------------- the New game screen (U39)

        /// <summary>
        /// The caption over the seed box — the word "Seed", set at <see cref="HudTextRole.Meta"/>,
        /// the same quiet label every figure in this interface is introduced by.
        /// </summary>
        public const int StartSeedCaption = 18;

        /// <summary>
        /// The New game screen's content: a caption, the box the seed is typed in, and the two rows
        /// under it — Reroll, then Start.
        ///
        /// <para><b>Derived rather than written down, and measured against
        /// <see cref="StartListMax"/> rather than against the panel</b>, because this screen sits in
        /// exactly the region the load list sits in, with the row that goes back beneath it. The
        /// panel's height is fixed for every screen (§4), so what a new screen owes the fast tier is
        /// not "how tall am I" but "do I fit in the box that already exists" — and four controls in
        /// a body sized for six save rows is the kind of thing that is obviously true until somebody
        /// adds a fifth.</para>
        ///
        /// <para>The two gaps are <see cref="Gap"/> because that is what <c>.field</c> already
        /// carries above itself in the naming prompt, and a text field that stood closer to its
        /// caption here than there would be the same control at two spacings.</para>
        /// </summary>
        public const float StartNewGameHeight =
            StartSeedCaption + Gap                          // Seed
            + FieldHeight + Gap                             // the box
            + StartRow + StartRow;                          // Reroll, Start

        /// <summary>
        /// How tall the content of one screen <i>would</i> be, for a given number of rows — which
        /// is no longer the panel's height, and is kept because it is what decides whether a screen
        /// fits inside <see cref="StartBody"/> or has to scroll.
        /// </summary>
        public static float StartRowsHeight(int rows) =>
            Math.Max(0, rows) * StartRow + Math.Max(0, rows - 1) * StartRowGap;

        /// <summary>
        /// How tall a listing of this many saves would be, before the body's ceiling is applied.
        /// Zero saves still occupy a row: the "nothing here yet" line.
        /// </summary>
        public static float StartListHeight(int saves) =>
            saves <= 0 ? StartSaveRow : saves * StartSaveRow + (saves - 1) * StartSaveGap;

        /// <summary>
        /// How many saves the list shows before it scrolls. Derived from the fixed body rather
        /// than written down, so the two cannot disagree the day the body changes.
        /// </summary>
        public static int StartSavesBeforeScrolling =>
            (int)((StartBody - StartRowGap - HudTheme.BorderWidth - StartRow + StartSaveGap) /
                  (StartSaveRow + StartSaveGap));

        /// <summary>
        /// Where the start screen sits: centred, both axes.
        ///
        /// <para><b>Not part of <see cref="Solve"/>, and that is deliberate.</b> Solve places the
        /// regions of the playing HUD and its test asks whether any two of them overlap. The start
        /// screen is never on screen with any of them — it exists precisely when no session is
        /// built, so there is no stores panel, no roster and no command bar to overlap. Putting it
        /// in that dictionary would be asking a question about a screen nobody will ever see. What
        /// it does owe the fast tier is <see cref="StartScreenFits"/>.</para>
        /// </summary>
        public static HudRect StartScreen(float width, float height) =>
            new HudRect((width - StartWidth) * 0.5f, (height - StartPanelHeight) * 0.5f,
                StartWidth, StartPanelHeight);

        /// <summary>
        /// Whether the start screen stands entirely inside the canvas.
        ///
        /// <para>The question the overlap test answers for the playing HUD, asked the only way it
        /// can be asked of a screen with nothing beside it. A modal that runs off the top of a
        /// small canvas hides its own first row, and the row a start screen hides first is New
        /// game.</para>
        /// </summary>
        public static bool StartScreenFits(float width, float height)
        {
            HudRect box = StartScreen(width, height);
            return box.X >= 0f && box.Y >= 0f &&
                   box.X + box.Width <= width && box.Y + box.Height <= height;
        }


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
            int cards = VisibleCards(width, height, content.Colonists);
            if (cards <= 0) boxes[HudRegion.ColonistStrip] = default;
            else
            {
                // A second row is as wide as a full one, so the box is sized by the widest row
                // rather than by the count: six cards over two rows still occupies the span of
                // whatever the first row holds.
                int perRow = Math.Max(1, CardsPerRow(width));
                int widest = Math.Min(cards, perRow);
                int rows = StripRowsUsed(width, height, cards);
                float stripWidth = widest * CardWidth + (widest - 1) * CardGap;
                boxes[HudRegion.ColonistStrip] = new HudRect(
                    (width - stripWidth) * 0.5f, StripTop, stripWidth, StripHeight(rows));
            }

            // ---- inspect, bottom left, clear of the bar. Nothing selected, no pane: an empty
            // rect, which is how every other region says "I am not on screen" here.
            float inspectHeight = InspectHeight(content.NeedRows, content.SkillRows, content.CellRows);
            boxes[HudRegion.Inspect] = inspectHeight <= 0f
                ? new HudRect(Edge, height - InspectBottom, 0f, 0f)
                : new HudRect(Edge, height - InspectBottom - inspectHeight,
                              content.CellRows > 0 ? InspectNarrowWidth : InspectWidth, inspectHeight);

            // ---- command bar, the full width of the screen (owner, 2026-09-17)
            //
            // It was a centred pill as wide as its items. Full width for the same reason it is
            // docked: it is the edge of the screen rather than a panel floating near it, and a
            // popover raised from a button on it has somewhere definite to sit. Its own top
            // hairline is the only one left, which BarHeight does not carry — that constant is the
            // bar's content and padding, and it is what the overflow arithmetic works in.
            float barHeight = HudCommands.BarHeight + BarFrame;
            boxes[HudRegion.CommandBar] =
                new HudRect(0f, height - BarBottom - barHeight, width, barHeight);

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

        /// <summary>
        /// The command bar's own hairlines: one, not two. It runs the full width of the screen
        /// with its left, right and bottom edges off it, so only the top one is drawn — which is
        /// what a bar that is the edge of the screen looks like rather than a panel near it.
        /// </summary>
        public const int BarFrame = HudTheme.BorderWidth;

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
                         - (BarBottom + HudCommands.BarHeight + Frame + Gap);
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
        /// The pane's height, or zero when there is nothing to inspect.
        ///
        /// <para><b>With nothing selected there is no pane at all</b> (owner, 2026-09-16), and
        /// that is the resting state of the HUD. It was a 41 px strip reading "Nothing selected",
        /// itself a cut-down of a three-sentence empty state; both were the interface talking
        /// about itself, and a panel whose only job is to say it has no job is worse than the
        /// gap it fills. Zero here rather than a small number, because the shell takes the pane
        /// out of the tree entirely and a model that still reserved a strip would be describing
        /// a screen nobody builds.</para>
        ///
        /// <para><b>What the model still cannot say</b> is how tall an <i>item</i> or <i>cell</i>
        /// pane is: both have no need rows and a full header, so they land in the zero branch.
        /// That was equally true of the one-line version this replaces, and it is only sound
        /// because every criterion stated against this model — coverage, overlap, the clearance
        /// above the bar — is stated with nothing selected or a colonist selected.</para>
        /// </summary>
        public static float InspectHeight(int needRows) => InspectHeight(needRows, 0);

        /// <summary>
        /// The pane's height with a given tab showing. Exactly one of the three counts is
        /// non-zero when something is selected: a colonist shows one tab's body at a time, and a
        /// tile shows the readout rows instead — no tab strip, so its chrome is the header alone.
        /// </summary>
        public static float InspectHeight(int needRows, int skillRows, int cellRows = 0)
        {
            float body;
            if (cellRows > 0)
                body = cellRows * CellRow + (cellRows - 1) * CellRowGap;
            else if (skillRows > 0)
                body = skillRows * SkillRow + (skillRows - 1) * SkillRowGap;
            else if (needRows > 0)
                body = needRows * NeedRow + (needRows - 1) * NeedRowGap;
            else
                return 0f;

            return cellRows > 0
                ? Frame + Pad + InspectHeader + InspectHeaderGap + body + Pad
                : Frame + Pad + InspectHeader + InspectHeaderGap + InspectTabs + InspectTabGap +
                  body + Pad;
        }

        // ---------------------------------------------------------------- fitting

        /// <summary>
        /// The space the colonist strip may occupy.
        ///
        /// <para><b>It is not the gap between the two top corners, and that difference is a
        /// bug this test caught.</b> The strip is centred on the <i>screen</i>, while the panels
        /// either side of it are different widths — 168 of stores on the left against 322 of rail
        /// and clock on the right. So the free span's middle sits seventy-seven pixels left of the
        /// screen's middle at 1920, and a strip sized to the whole span and then centred pokes
        /// into the clock column by seventy-seven pixels however careful the arithmetic looked.
        /// What the strip may actually have is twice the smaller of its two clearances. (The
        /// figure was seventeen while stores was 288 px wide; narrowing the panel widened the
        /// mismatch rather than fixing it, which is the point of computing it here.)</para>
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
        public static int VisibleCards(float width, float height, int colonists)
        {
            if (colonists <= 0) return 0;
            return Math.Max(0, Math.Min(colonists, CardsPerRow(width) * StripRowsAllowed(height)));
        }

        /// <summary>How many cards fit across one row of the strip.</summary>
        public static int CardsPerRow(float width)
        {
            float room = StripRoom(width);
            return Math.Max(0, (int)Math.Floor((room + CardGap) / (CardWidth + CardGap)));
        }

        /// <summary>
        /// How many rows this screen is tall enough to spend on the strip, 1 to
        /// <see cref="StripRows"/>. Never zero: a screen too short for one row of cards would
        /// have no roster at all, which is worse than being over budget.
        /// </summary>
        public static int StripRowsAllowed(float height)
        {
            float budget = height * StripHeightShare;
            int rows = (int)Math.Floor((budget + CardGap) / (CardHeight + CardGap));
            return Math.Max(1, Math.Min(StripRows, rows));
        }

        /// <summary>How many rows that many cards actually occupy, 0 to what the screen allows.</summary>
        public static int StripRowsUsed(float width, float height, int cards)
        {
            int perRow = CardsPerRow(width);
            if (cards <= 0 || perRow <= 0) return 0;
            return Math.Min(StripRowsAllowed(height), (cards + perRow - 1) / perRow);
        }

        /// <summary>How tall the strip stands at a given number of rows.</summary>
        public static float StripHeight(int rows) =>
            rows <= 0 ? 0f : rows * CardHeight + (rows - 1) * CardGap;

        /// <summary>
        /// How many command-bar items fit at this width, by the modelled widths.
        ///
        /// <para>Against the bar's own padding rather than the screen margin, since the bar became
        /// full width: the room an item has is the screen less what the bar itself takes, and
        /// measuring against a margin the bar no longer has would put two items into Menu that
        /// would have fitted.</para>
        /// </summary>
        public static int FittedCommands(float width) =>
            HudCommands.Fit(HudCommands.ModelWidths(), width - 2 * HudCommands.BarPad);

        // ---------------------------------------------------------------- popovers

        /// <summary>
        /// Where a panel raised from the command bar sits, measured from the bottom of the screen.
        ///
        /// <para><b>Flush on the bar, no gap</b> (owner, 2026-09-17: "directly above the build
        /// button … no spacing and padding to ensure tight space"). A popover with a strip of
        /// world between it and the button that raised it reads as a separate window that happens
        /// to be nearby; sitting on the bar it reads as the button having grown upwards, which is
        /// what it is.</para>
        /// </summary>
        public static float PopoverBottom => BarBottom + HudCommands.BarHeight + BarFrame;

        /// <summary>
        /// Where a popover's left edge goes: under the button that raised it, pushed back on to
        /// the screen if that would hang it off an edge.
        ///
        /// <para>Left-aligned with the button rather than centred on it, because a menu whose
        /// left edge lines up with the control it belongs to reads as belonging to it, and
        /// because centring puts a wide popover off the screen for the leftmost button and then
        /// has to clamp anyway — at which point it is neither centred nor aligned.</para>
        ///
        /// <para>The clamp is to the screen, not to a margin: these panels are docked furniture
        /// like the bar under them, so a popover raised by the last button on the right ends
        /// flush with the right edge.</para>
        /// </summary>
        public static float PopoverLeft(float buttonLeft, float popoverWidth, float screenWidth)
        {
            float widest = Math.Max(0f, screenWidth - popoverWidth);
            if (widest <= 0f) return 0f;
            return Math.Max(0f, Math.Min(buttonLeft, widest));
        }

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
