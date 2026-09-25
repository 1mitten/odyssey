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

        /// <summary>The Events panel (A6): incidents that happened, under the alerts in the same column.</summary>
        Bulletins,

        /// <summary>The transient toast stack, at the foot of that same column (SK4).</summary>
        Toasts,

        DepthRail,
        OrdersStrip,
        Inspect,
        CommandBar,

        /// <summary>The views strip: switches for what the board shows, under the orders (design 32 §14).</summary>
        ViewsStrip,
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

        /// <summary>
        /// Rows of the Events panel: incidents that happened and are not yet dismissed. Zero hides
        /// the panel outright, exactly as <see cref="Alerts"/> does, so a colony nothing has
        /// happened to pays nothing for the region.
        /// </summary>
        public readonly int Bulletins;

        /// <summary>
        /// Toasts on screen right now (SK4). Zero hides the stack outright, which is its resting
        /// state: a toast lasts six seconds and nothing raises one most of the time, so this is
        /// zero whenever the coverage criterion is measured.
        /// </summary>
        public readonly int Toasts;

        public HudContent(int colonists, int storeRows, int alerts, int layers, int needRows,
                          int skillRows = 0, int cellRows = 0, int bulletins = 0, int toasts = 0)
        {
            Colonists = Math.Max(0, colonists);
            StoreRows = Math.Max(0, storeRows);
            Alerts = Math.Max(0, alerts);
            Layers = Math.Max(1, layers);
            NeedRows = Math.Max(0, needRows);
            SkillRows = Math.Max(0, skillRows);
            CellRows = Math.Max(0, cellRows);
            Bulletins = Math.Max(0, bulletins);
            Toasts = Math.Max(0, toasts);
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
        /// <para>Widest name row: the avatar plus its gap plus the longest name the pool
        /// can produce — which is a fact about <c>docs/design/colonist-names.csv</c>, so adding a
        /// long name to that file is a change to this number. Widest activity row: the icon (17) plus its gap (6) plus the longest word
        /// in <c>ui.status</c>. Both plus padding on each side. The figures are not taken on
        /// trust — <c>TheCardIsWideEnoughForItsRowsAndNoWider</c> asks the text engine what those
        /// strings really draw in the real face at the real size, and fails on either side with a
        /// number attached, exactly as the stores panel's own width test does.</para>
        ///
        /// <para>The name is the one thing on this screen allowed an ellipsis, so a future name
        /// longer than the pool's does not break the card — but it does mean a card whose name is
        /// cut short, which is why the test's lower bound exists rather than only its upper.</para>
        /// </summary>
        /// <para><b>96 x 89 since 2026-09-18</b> (owner: name below portrait, activity word removed
        /// and icon badged on portrait, card narrowed to reclaim bar width). Stacking the name below
        /// the portrait leaves the full width of the card for the colonist's name minus horizontal
        /// padding, widening the name budget from 50 to 80 px (a 60% increase) while fitting ~30%
        /// more colonists per row on the top bar.</para>
        public const int CardWidth = 96;

        /// <summary>
        /// The room a card leaves for a colonist's name: the card less its padding on each side.
        ///
        /// <para>With the name placed below the portrait and the activity icon badged onto the
        /// portrait corner, the name row spans the full interior width of the card.</para>
        /// </summary>
        public const int CardNameBudget = CardWidth - 2 * CardPad;

        /// <summary>
        /// How tall a roster card is: top padding, the 52 px avatar, name gap, 19 px name row, the
        /// health bar and its gap, bottom padding.
        ///
        /// <para><b>89 until 2026-09-23, 94 since</b> (design 33 §9f, owner: <i>"Their health needs
        /// to be also displayed on their colony stats as it appears above them"</i>). The card grew
        /// by exactly <see cref="CardHealthGap"/> and <see cref="CardHealthBar"/> and by nothing
        /// else, and is written as its parts so the next row added to it moves this number rather
        /// than being squeezed into it.</para>
        ///
        /// <para><b>Five pixels is what the coverage ceiling had left.</b> A full one-row strip at
        /// 1280 x 720 is 480.7 px wide, and the resting HUD there was 19.69% with 89 px cards
        /// against <see cref="CoverageCeiling"/>'s 20%: 0.31% of that canvas is 2,857 px², which is
        /// 5.9 px of card height. A first cut at 102 (a 10 px bar under a 3 px gap) measured
        /// <b>20.37%</b> and failed <c>TheStripIsAlwaysOneRowAndNoFurther</c>; the owner declined
        /// raising the ceiling for the name pool on 2026-09-18, so the bar took the room there was
        /// rather than the room it wanted. At 94 the same HUD is <b>19.95%</b>, so <b>the roster
        /// card now spends the last of the ceiling</b> and the next pixel added to any resting
        /// region has to be paid for.</para>
        /// </summary>
        public const int CardHeight = CardPad + CardAvatar + CardNameGap + CardNameRow + CardHealthGap + CardHealthBar + CardPad;

        /// <summary>Portrait to name on a card.</summary>
        public const int CardNameGap = 2;

        /// <summary>The name's line on a card: the 14 px row face with its leading.</summary>
        public const int CardNameRow = 19;

        /// <summary>Name to health bar on a card: one pixel, because the name's line already ends in its own leading.</summary>
        public const int CardHealthGap = 1;

        /// <summary>
        /// The health bar on a card (design 33 §9f): the full width inside the padding and 4 px
        /// tall, the inspect pane's need bar's thickness — as thick as the coverage ceiling allows
        /// (<see cref="CardHeight"/>). Its colour is the reading; "Downed" is written across the
        /// foot of the portrait rather than over a bar this thin.
        /// </summary>
        public const int CardHealthBar = 4;

        public const int CardGap = 7;

        /// <summary>
        /// Width of the compact pagination control docked on the right side of the roster bar
        /// (<c>[ &lt; ] 1 / 3 [ &gt; ]</c>).
        /// </summary>
        public const int PagerWidth = 72;
        public const int PagerGap = 4;
        public const int PagerBtnWidth = 18;
        public const int PagerHeight = 24;

        /// <summary>
        /// Maximum number of colonist cards shown across the one row of the strip before pagination engages
        /// (owner, 2026-09-18: "Should be one row with 6 on an more (no 2 rows or anthing - always one)").
        /// </summary>
        public const int StripCardsCap = 6;

        /// <summary>
        /// How far the colonist strip sits from the top of the screen, and the command bar from
        /// the bottom: **nothing at all** (owner, 2026-09-17, "directly at the bottom of the
        /// screen, no spacing, padding, to maximise viewing space — dock it to the bottom and
        /// apply the same to the roster bar").
        ///
        /// <para>Both are deliberately <see cref="Edge"/> no longer. The stores panel, the clock
        /// and the depth rail keep their margin, because they are panels that sit *in* the view;
        /// these two are bars that bound it, and a bar with a strip of world under it reads as
        /// floating rather than as the edge of the screen.</para>
        /// </summary>
        public const int StripTop = 0;

        /// <inheritdoc cref="StripTop"/>
        public const int BarBottom = 0;

        /// <summary>
        /// How many rows of cards the strip may run to.
        ///
        /// <para>Always exactly one row (owner, 2026-09-18: "Should be one row with 6 on an more
        /// (no 2 rows or anthing - always one)"). Capacity beyond 6 cards is handled by pagination.</para>
        /// </summary>
        public const int StripRows = 1;

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
        /// <para><b>0.14 until 2026-09-18</b>, when the avatar doubled and took the card from 63 to
        /// 89. The arithmetic that forced this: two rows need <c>2 x (CardHeight + CardGap)</c>, so
        /// at 1080 the old budget of 151 px held two 63 px cards and holds only one 89 px card —
        /// **the strip would have quietly halved the roster at the commonest resolution**, and
        /// nothing would have said so except a colony whose back row had vanished. 0.18 is
        /// 194 px at 1080, which is two of the new cards with two to spare, and 130 px at 720,
        /// which is still deliberately one.</para>
        public const float StripHeightShare = 0.18f;

        /// <summary>
        /// The avatar tile on a card, and the selected-thing avatar in the inspect pane's header,
        /// which are the same size by specification.
        ///
        /// <para><b>Doubled on 2026-09-18</b>, 30 → 60, at the owner's instruction: *"can we make
        /// the in-game avatar profile twice as big as it's hard to see"*. It is a face now
        /// (<c>docs/design/20-avatars.md</c> §10) rather than a coloured tile, and a 128 px render
        /// shrunk to thirty was giving back almost none of what was rendered.</para>
        /// </summary>
        public const int Avatar = 60;

        /// <summary>
        /// The avatar tile on a roster card, which is smaller than the inspect one. Doubled with
        /// it, 26 → 52; <see cref="CardWidth"/> and <see cref="CardHeight"/> are re-derived rather
        /// than nudged, and <see cref="StripHeightShare"/> had to move with them.
        /// </summary>
        public const int CardAvatar = 52;

        /// <summary>Avatar to name on a card's identity row.</summary>
        public const int CardAvatarGap = 8;

        /// <summary>
        /// The avatar on the world-setup page's detail pane — the one place in the game with room
        /// for a portrait, and the one moment a player is choosing between people rather than
        /// glancing at them (<c>docs/design/20-avatars.md</c> §3).
        ///
        /// <para><b>Sixty-four is not a new number:</b> ADR 0007 names 32 and 64 as the only two
        /// sizes interface art may be drawn at, and this is the larger. It is also almost exactly
        /// the height of the three lines beside it — the name at 26, the trade at 18, the traits
        /// row at 18 over a 4 px gap, which is 66 — so the portrait and the record it belongs to
        /// end together.</para>
        /// </summary>
        public const int DetailAvatar = 64;

        /// <summary>Portrait to record on the detail pane, at the page's own scale rather than a
        /// card's: the pane is a thousand pixels wide and an 8 px gap there reads as a mistake.</summary>
        public const int DetailAvatarGap = 18;

        /// <summary>
        /// Avatar to name on a candidate row of the world-setup page. The card gap, because it is
        /// the same relationship at the same distance — a face and the name of the person whose
        /// face it is.
        /// </summary>
        public const int ColonistAvatarGap = CardAvatarGap;

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

        /// <summary>
        /// The width of the whole right-hand column: the clock, and the alerts, bulletins and
        /// toasts stacked under it, which all take their width from here so the column reads as
        /// one edge rather than three.
        ///
        /// <para><b>266 -> 296 on 2026-09-23</b>, to give the clock room for the outdoor
        /// temperature beside the date (owner: widen it "respecting the layout of the ui
        /// (including events - widen this up to match)"). The events panels come with it for
        /// free, which is what "match" means here.</para>
        ///
        /// <para><b>296 and not more, because the coverage budget is what is left.</b> Every
        /// pixel of width here is 91 px of area in the resting HUD, and
        /// <see cref="CoverageCeiling"/> had about 0.32% of a 1280x720 canvas spare — 32 px.
        /// 300 came to 20.02% and failed. Raising the ceiling is the obvious alternative and is
        /// deliberately not taken: this number's own history records that it "is the owner's to
        /// reverse", and that the colonist name pool nearly took it to 0.21 and the owner
        /// declined. Going wider than this is that decision again, not a tweak.</para>
        ///
        /// <para><b>This number is written twice.</b> <c>.column-right</c> in <c>Hud.uss</c>
        /// carries the same width, because the model here is what the fast tier reasons about and
        /// the stylesheet is what the panel is actually laid out by. Nothing checks that the two
        /// agree — the hazard CLAUDE.md names — so change them together.
        /// <c>HudGeometryTests.TheModelDescribesTheScreenTheShellActuallyBuilds</c> is the test
        /// that would eventually notice, and it is a PlayMode test rather than a rule.</para>
        /// <para><b>296 -> 271 on 2026-09-24</b>, on the merge with combat, which spent the same
        /// 0.3% on a health bar under every roster card (<see cref="CardHeight"/>). Together they came
        /// to 20.20%. The owner chose to hand the width back rather than raise the ceiling: 271 is
        /// the widest the two can share. Whether the outdoor temperature still clears the speed
        /// controls at 271 is a look, not a test.</para>
        /// </summary>
        public const int ClockWidth = 271;

        /// <summary>
        /// The clock's one row: the time, the date and the outdoor temperature side by side.
        ///
        /// <para><b>One row, and it was nearly two.</b> The temperature was appended to the date
        /// string and pushed this row over the controls beside it (owner, 2026-09-23). A row of
        /// its own was the obvious fix and cost 20 px of screen the HUD does not have:
        /// <c>HudLayoutTests.TheStripIsAlwaysOneRowAndNoFurther</c> caps the resting interface at
        /// 20% of the viewport and it came to 20.27% at 1280x720. Dropping the word "outdoors",
        /// which is what the owner asked for, bought back more width than the reading needs, so
        /// the reading is a third element on this row instead and the height is unchanged.</para>
        /// </summary>
        public const int ClockRow = 28;

        /// <summary>One of the four speed buttons.</summary>
        public const int SpeedButton = 26;

        /// <summary>Clock row to speed row.</summary>
        public const int ClockGap = 9;

        /// <summary>
        /// One line of 13 px body text.
        ///
        /// <para>Twenty-six rather than the seventeen the point size suggests, because UI
        /// Toolkit's line box for 13 px type measures about 26 px rather than 17 — measured on the
        /// inspect pane's one-line empty state.</para>
        /// </summary>
        public const int AlertHeight = 26;

        public const int AlertGap = 4;

        /// <summary>The alerts panel's own label row and the gap under it.</summary>
        public const int AlertHeaderBlock = HeaderHeight + HeaderGap;

        /// <summary>
        /// One row of the Events panel: the same line of body text an alert is, because the two
        /// panels share a column and a row idiom, and two heights would be two things to drift.
        /// </summary>
        public const int BulletinHeight = AlertHeight;

        public const int BulletinGap = AlertGap;

        // ------------------------------------------------------------------ depth rail

        public const int RailWidth = 44;
        public const int RailCellWidth = 26;
        public const int RailCellHeight = 16;
        public const int RailSurfaceCellHeight = 32;
        public const int RailCellGap = 6;

        /// <summary>The walls-down switch under the cells (design 42 §7): as wide as a rail cell.
        /// It took the place of the "R / F" hint, whose keys are a tooltip now.</summary>
        public const int RailToggle = 22;

        /// <summary>The gap between the last cell and the walls-down switch.</summary>
        public const int RailToggleGap = 4;

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

        // ------------------------------------------------------------------ orders strip

        /*
         * The orders strip: the four verbs a player applies to what is already on the board —
         * chop, mine, deconstruct, cancel — as a column of icon buttons in the right-hand gutter,
         * directly under the depth rail (owner, 2026-09-17: "a vertical button strip that sits
         * below the depth control … to the right hand side of the screen very close to the screen
         * border … this enables us to quickly give orders without having to click the build
         * button").
         *
         * They were four 26 px buttons in the Build palette's header until then, which made every
         * order cost opening the palette first. The strip is the same gutter and the same width as
         * the rail, so the right edge of the screen reads as one column rather than as two panels
         * that happen to be near each other; it is what OrdersCount rows of it are tall, and it
         * grows downwards as more orders are added, which is why the count is a length here rather
         * than a written-down number.
         */

        /// <summary>The strip shares the rail's gutter, so the right edge is one column.</summary>
        public const int OrdersWidth = RailWidth;

        /// <summary><inheritdoc cref="RailSidePad"/></summary>
        public const int OrdersSidePad = RailSidePad;

        /// <summary>One order button: a square, larger than the 26 px it was in the palette header
        /// because the gutter has the width for it and a 36 px gutter would otherwise be mostly
        /// padding.</summary>
        public const int OrderButton = 34;

        /// <summary>Between two order buttons.</summary>
        public const int OrderGap = BuildTileGap;

        /// <summary>
        /// The strip's own padding above its first button.
        ///
        /// <para>Not <see cref="Pad"/>: the strip carries no label and no text, so twelve either
        /// end would be a quarter of its height spent on air. And it is the top only — the last
        /// button's own <see cref="OrderGap"/> is the bottom padding, because UI Toolkit has no
        /// <c>:last-child</c> to cancel that gap with and a padding that double-counts it is how a
        /// model and a stylesheet come to disagree by six pixels.</para>
        /// </summary>
        public const int OrdersPadTop = 6;

        /// <summary>Rail to strip, the ordinary gap between two panels in a column.</summary>
        public const int RailToOrders = Gap;

        /// <summary>
        /// How many orders the strip draws.
        ///
        /// <para>Read off <see cref="PaletteTools.Pinned"/> rather than written down, because the
        /// whole point of the strip is that a fifth order is one row of that table: the ceiling
        /// the palette header imposed — four buttons beside a switcher and a way out — is what
        /// moving them here lifts.</para>
        /// </summary>
        public static int OrdersCount => PaletteTools.Pinned.Length;

        /// <summary>The strip's height, for <see cref="OrdersCount"/> buttons — each of which
        /// carries its own gap under it, including the last.</summary>
        public static float OrdersHeight =>
            Frame + OrdersPadTop + OrdersCount * (OrderButton + OrderGap);

        /// <summary>
        /// The whole right-hand gutter under the rail: the gap and the orders strip, then the gap
        /// and the views strip under it (owner, 2026-09-23). Both are fixed buttons; the rail is the
        /// region that gives, here as everywhere.
        /// </summary>
        public static float OrdersBlock => RailToOrders + OrdersHeight + OrdersToViews + ViewsHeight;

        /// <summary>Orders strip to views strip: the ordinary gap between two panels in a column.</summary>
        public const int OrdersToViews = Gap;

        /// <summary>How many views the strip draws — <see cref="HudViews.Keys"/>, read rather than written down.</summary>
        public static int ViewsCount => HudViews.Keys.Length;

        /// <summary>The views strip's height: the orders strip's buttons and padding, for <see cref="ViewsCount"/>.</summary>
        public static float ViewsHeight =>
            Frame + OrdersPadTop + ViewsCount * (OrderButton + OrderGap);

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

        /// <summary>The header's way out. It was this and the four pinned tools until they left
        /// for the orders strip on 2026-09-17, and the close button kept the size rather than
        /// being resized for the sake of it — it is the same 26 px box every window's X is.</summary>
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

        /// <summary>The pane's clearance over the command bar: flush (0 px), maximising visible board space above.</summary>
        public const int InspectToBar = 0;

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
        /// under it — 38 px of text.
        ///
        /// <para><b>The avatar is what sets this now, not the text</b> (2026-09-18). It used to be
        /// the other way round, the two lines standing taller than the 30 px tile beside them; at
        /// 60 the portrait is the taller of the two and the header is its height. The text still
        /// aligns to the top of the block rather than centring on the picture, so the name sits
        /// where it always has and only the block below it grew.</para>
        /// </summary>
        public const int InspectHeader = Avatar;

        /// <summary>The name line's height in the header (<c>.inspect__nameline</c>).</summary>
        public const int InspectNameLine = 22;

        /// <summary>
        /// One of the header's smaller lines (<c>.inspect__state</c>, <c>.inspect__pace</c>): the
        /// activity line, and under it a colonist's pace (design 17 §5a).
        /// </summary>
        public const int InspectTextLine = 16;

        /// <summary>
        /// The header's text, all three lines: 54 px, inside the <see cref="InspectHeader"/> the
        /// portrait sets. <b>The pace line was added into that slack</b> rather than growing the
        /// pane, and <c>HudLayoutTests</c> fails the day a fourth line would not fit.
        /// </summary>
        public const int InspectHeaderText = InspectNameLine + 2 * InspectTextLine;

        /// <summary>
        /// The same header when the subject is a tile or a pile rather than a colonist.
        ///
        /// <para><b>It stayed at 38, and the split is the point.</b> A tile's slot holds an
        /// <see cref="IconBadge"/> on a <c>ui.*</c> key, and the interface draws icons at 17, 16
        /// and 30 and no other size (<c>14-hud-layout.md</c> §4). Growing this with the colonist's
        /// portrait would have made a fourth icon size out of a rule with three, and stood a
        /// five-fact readout up on a header two thirds the height of its own body. Only a
        /// photograph of a person earned the extra pixels.</para>
        /// </summary>
        public const int InspectHeaderNarrow = 38;
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

        /// <summary>One skill line's width in the inspect pane's grid, pinned here so the setup
        /// page's own grid can be derived from it rather than from a second literal.</summary>
        public const int SkillRowWidth = 256;

        // ---------------------------------- the skill row's width budget (owner, 2026-09-21)
        //
        // The experience bar moved out of the row's bottom edge and INTO the row, between the name
        // and the level, on the owner's first look at it: *"the experience bar needs to sit between
        // the skill label and the skill value"*. That turns a decoration drawn over the row into a
        // fifth column of it, so the row now has a width budget and these are it. `Hud.uss` is
        // written from these numbers and `HudLayoutTests.TheSkillRowsPartsFitTheRow` is what stops
        // the two drifting.
        //
        // <b>The bar is a fixed width and the NAME is what flexes</b>, which is the decision worth
        // keeping. Everything to the bar's right is a fixed width, so a fixed bar sits at a fixed
        // offset from the row's right edge and every bar in the grid lines up on both edges without
        // anything measuring text. Flex the bar instead and its left edge tracks the name beside
        // it, so "Construction" and "Mining" would start their bars in different places and the
        // column would read as ragged.

        /// <summary>The icon badge at the head of a skill row (<c>IconBadge.RowSize</c>).</summary>
        public const int SkillIconWidth = 17;

        /// <summary>Between the icon and the name.</summary>
        public const int SkillIconGap = 9;

        /// <summary>
        /// The experience bar's track (SK3). Fixed, so every bar in the grid aligns — see the note
        /// above.
        /// </summary>
        public const int SkillBarWidth = 72;

        /// <summary>The air either side of the bar, so it is not crowded by the name or the
        /// number (owner, 2026-09-21: <i>"with good spacing"</i>).</summary>
        public const int SkillBarGap = 8;

        /// <summary>
        /// How thick the bar is drawn. Six rather than the three it shipped at, on the owner's
        /// first look: <i>"can we make the bars slightly thicker if space allows"</i>.
        ///
        /// <para><b>The row does not grow for it and must not.</b> <see cref="SkillRow"/> is 19 and
        /// the colonist pane is one fixed height across every tab on purpose, so the bar is only
        /// ever allowed the height it can take inside a row that is already that tall. Six leaves
        /// six and a half either side of it, centred. This is the same constraint the absolutely
        /// positioned version was protecting, met by staying short rather than by leaving the
        /// flow.</para>
        /// </summary>
        public const int SkillBarHeight = 6;

        /// <summary>The level number's column.</summary>
        public const int SkillLevelWidth = 22;

        /// <summary>The passion pips, and the air before them.</summary>
        public const int SkillPassionWidth = 18;

        public const int SkillPassionGap = 7;

        /// <summary>
        /// What is left for the name once every fixed part of the row has taken its width. The
        /// name is the only thing that flexes, so this is the number that shrinks when somebody
        /// widens anything else — which is why it is derived here and asserted rather than written
        /// into the stylesheet as a literal nobody would recompute.
        /// </summary>
        public const int SkillNameWidth =
            SkillRowWidth - SkillIconWidth - SkillIconGap
            - SkillBarGap - SkillBarWidth - SkillBarGap
            - SkillLevelWidth - SkillPassionGap - SkillPassionWidth;

        /// <summary>Between two columns of skills.</summary>
        public const int SkillColumnGap = 18;

        // ---------------------------------- the setup page's own skills grid (owner, 2026-09-18)

        /// <summary>
        /// How many columns of skills the setup page's detail pane shows.
        ///
        /// <para><b>Two, by the owner's instruction</b> — *"use the space to make 2 columns of
        /// skills rather than 4 as we'll likely have to bring something in in the future"*. The
        /// grid is a wrapping row in a pane that grows, so at 1920 it had been laying thirteen
        /// skills out four across and filling the screen edge to edge; capping the container is
        /// what holds it to two and leaves the right-hand half of the page for whatever comes.
        /// The count is the owner's decision about the page, not an arithmetic consequence of
        /// what fits, so it is a constant rather than a division.</para>
        /// </summary>
        public const int SetupSkillColumns = 2;

        /// <summary>A skill line on the setup page: wider and taller than the inspect pane's,
        /// because this is a screen read at leisure rather than glanced at over a running
        /// world.</summary>
        public const int SetupSkillRowWidth = 300;

        /// <summary>The row's height at the setup page's larger type step.</summary>
        public const int SetupSkillRow = 24;

        /// <summary>
        /// The grid's ceiling, derived rather than written down: the columns the owner asked for,
        /// each a row wide plus the gap after it. The trailing gap is absorbed by the grid's own
        /// negative right margin, exactly as the inspect pane's is.
        /// </summary>
        public const int SetupSkillsWidth =
            SetupSkillColumns * (SetupSkillRowWidth + SkillColumnGap);

        /// <summary>
        /// A section heading on the setup page — "Skills", "Traits" — at
        /// <see cref="HudTextRole.Name"/>, which is the one step of the scale that is both bigger
        /// and bolder than the body under it (owner, 2026-09-18: *"a bigger bolder heading"*).
        ///
        /// <para>A step of the existing scale rather than a rung added for this page. The
        /// interface's other heading idiom — <see cref="HudTextRole.PanelLabel"/>, 11/600 upper
        /// and tracked — is bolder but smaller, which is right over a panel glanced at beside a
        /// running world and wrong on a full screen read at leisure. Field captions on this page
        /// keep that idiom; sections get this one, so the two do not compete.</para>
        /// </summary>
        public const int SetupHeading = 26;

        /// <summary>Above a section heading, so a heading belongs to what is under it rather than
        /// floating between two blocks.</summary>
        public const int SetupHeadingGap = 22;

        /// <summary>
        /// The portrait and its record, down to the skills under them.
        ///
        /// <para>Owner, 2026-09-18: *"have more spacing on the main screen from the profile to the
        /// skills below it as it looks too close and untidy"*. The record's three lines ended
        /// flush against the first row of the grid, so the name, the trade, the traits and
        /// thirteen skills read as one undifferentiated stack rather than as a heading over a
        /// table.</para>
        /// </summary>
        public const int SetupSkillsGap = 24;

        /// <summary>
        /// How many needs the colonist body draws: Food, Rest and Mood, the three the model
        /// carries and the three <c>HudShell.SetNeed</c> fills by index.
        /// </summary>
        public const int InspectNeeds = 3;

        /// <summary>The needs grid's rows, two to a row like the skills.</summary>
        public static int InspectNeedRows => (InspectNeeds + 1) / 2;

        /// <summary>
        /// The colonist pane's body: <b>one height, whatever tab is showing</b> (owner,
        /// 2026-09-18 — "it resizes every time … it needs to be at least a fixed size").
        ///
        /// <para>The pane is docked to its bottom edge and grows upward, so a body sized to its
        /// own tab moved the header, the tab strip and every row under the pointer each time the
        /// player changed tab: Needs is two rows of 25 and Skills is seven of 19, ninety-eight
        /// pixels apart. The tallest live tab wins and the shorter ones sit at the top of it with
        /// the slack below (owner's choice of the three offered), so the only thing that changes
        /// between tabs is which rows are drawn.</para>
        ///
        /// <para>Derived rather than written down, so that a fourteenth skill or a fourth need
        /// moves it. The live tabs are measured and the disabled ones are not: Gear, Social and Log
        /// have no content to measure, and a guess at them would be empty space bought against a
        /// design nobody has written. Since 2026-09-25 the Thoughts tab is the tallest.</para>
        /// </summary>
        public static int InspectTabBody
        {
            get
            {
                int needs = InspectNeedRows * NeedRow + (InspectNeedRows - 1) * NeedRowGap;
                int skills = SkillCatalogue.Rows * SkillRow + (SkillCatalogue.Rows - 1) * SkillRowGap;
                // The Thoughts tab (design 51 §10, mockup 23b) is the tallest now: its meter,
                // breakdown, table and traits strip are drawn at the mockup's sizes, and every
                // tab takes its height so switching tabs moves nothing.
                return Math.Max(Math.Max(needs, skills), ThoughtsLayout.TabBody);
            }
        }

        /// <summary>
        /// The needs grid's height: <see cref="InspectNeedRows"/> rows of <see cref="NeedRow"/> with
        /// a <see cref="NeedRowGap"/> between each. What the traits block sits under (design 51 §5f).
        /// </summary>
        public static int InspectNeedsHeight => InspectNeedRows * NeedRow + (InspectNeedRows - 1) * NeedRowGap;

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
        public const int StartSaveRow = 64;

        public const int StartSaveGap = 10;

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

        // ------------------------------------------------ the colonist screen (U40)

        /// <summary>
        /// The face on a candidate card. The inspect header's size by specification
        /// (<c>docs/design/20-avatars.md</c> §3), named here rather than reached for directly so
        /// that <see cref="ColonistCard"/> and the presenter read one number and
        /// <c>HudLayoutTests</c> can hold the card to it.
        /// </summary>
        public const int ColonistAvatar = Avatar;

        /// <summary>
        /// A candidate's card: the name, the occupation, and what this person is good at.
        ///
        /// <para><b>Re-derived from its own rows on 2026-09-18, the way the roster card was</b>
        /// (<c>docs/design/20-avatars.md</c> §10.6) — 29 for the name, 18 for the trade and 18 for
        /// the skills. Sixty-five, and it must not fall below <see cref="ColonistAvatar"/>, which
        /// is the check whose absence caused this.</para>
        ///
        /// <para><b>§10.6 doubled the avatar and missed this card.</b> Its table re-derived the
        /// roster card 106 × 63 → 126 × 89 and the inspect header 38 → 60, and moved the strip
        /// share, the top scrim and the coverage ceiling with them. The candidate card was not on
        /// it, so it kept the 47 it was given when <see cref="Avatar"/> was 30 — and then carried a
        /// 60 px face in it, thirteen pixels taller than its own box at a 53 px pitch. On
        /// `Logs/setup-page.png` the three faces visibly ran into one another and into the
        /// selection outline. The skills line had gone the same way, squeezed out to make room for
        /// the trade, and the constants describing that line were left behind reading by
        /// nothing — which is how the loss was attributable rather than merely visible.</para>
        ///
        /// <para><b>The old comment's arithmetic was right and no longer applies.</b> Three lines
        /// came to 296 against the fixed body's 284 — true while the candidates had a screen of
        /// their own inside the start panel. They do not: they are part of the full-viewport setup
        /// page, beside the seed and the board size, so the ceiling is the canvas and there are
        /// some seven hundred spare pixels under the cards at 1080p.</para>
        /// </summary>
        /// <summary>
        /// Breathing room inside a candidate card, all four sides.
        ///
        /// <para><b>The selection outline's, really</b> (owner, 2026-09-18: *"there needs to be
        /// spacing with the selection cursor as it sits directly around the mini profile"*). A
        /// card sized to exactly its contents draws <c>.colonist--on</c>'s border hard against the
        /// face, which reads as the outline belonging to the portrait rather than to the card. The
        /// pad is what puts daylight between them, and
        /// <c>HudLayoutTests.EveryCardIsAtLeastAsTallAsTheFaceItCarries</c> holds the card to
        /// <see cref="ColonistAvatar"/> <i>plus two of these</i> rather than merely to the face.</para>
        /// </summary>
        public const int ColonistCardPad = 8;

        /// <summary>The name and age, at <see cref="HudTextRole.Name"/> — the detail pane's own
        /// name line, so the card and the record it opens are set the same way.</summary>
        public const int ColonistNameLine = 26;

        /// <summary>
        /// The occupation, at <see cref="HudTextRole.Row"/>.
        ///
        /// <para>A step up and four pixels taller since the card stopped carrying skills (owner,
        /// 2026-09-18: *"no need to display any skills there — Name, Age, Occupation, and resize
        /// occupation accordingly to a bigger size"*). Two lines beside a 60 px face leave room
        /// that a 13 px line does not use.</para>
        /// </summary>
        public const int ColonistTradeLine = 20;

        /// <summary>
        /// A candidate's card: the name, the occupation, and what this person is good at.
        ///
        /// <para><b>The face governs the height, and that is the point of writing it this
        /// way.</b> Two lines of text come to 46 and the portrait to 60, so the card is the
        /// portrait plus <see cref="ColonistCardPad"/> on both sides — 76 — and it cannot go back
        /// to being shorter than the thing inside it however the text changes.</para>
        ///
        /// <para><b>It carried a third line for part of 2026-09-18</b>, the two best skills, put
        /// back after the owner reported that the roll looked broken because nothing on a card
        /// varied by ability. Playing that, they took it off again — *"from the left hand panels,
        /// no need to display any skills there"* — because the detail pane beside the cards now
        /// shows all thirteen in two columns with room to read them. **A later session should not
        /// restore it as a fix for the original report**: the report was answered by the detail
        /// pane, and the card is deliberately identity alone.</para>
        ///
        /// <para><b>§10.6 doubled the avatar and missed this card.</b> Its table re-derived the
        /// roster card 106 × 63 → 126 × 89 and the inspect header 38 → 60, and moved the strip
        /// share, the top scrim and the coverage ceiling with them. The candidate card was not on
        /// it, so it kept the 47 it was given when <see cref="Avatar"/> was 30 — and then carried a
        /// 60 px face in it, thirteen pixels taller than its own box at a 53 px pitch. On
        /// `Logs/setup-page.png` the three faces visibly ran into one another and into the
        /// selection outline. The skills line had gone the same way, squeezed out to make room for
        /// the trade, and the constants describing that line were left behind reading by
        /// nothing — which is how the loss was attributable rather than merely visible.</para>
        ///
        /// <para><b>The old comment's arithmetic was right and no longer applies.</b> Three lines
        /// came to 296 against the fixed body's 284 — true while the candidates had a screen of
        /// their own inside the start panel. They do not: they are part of the full-viewport setup
        /// page, beside the seed and the board size, so the ceiling is the canvas and there are
        /// some seven hundred spare pixels under the cards at 1080p.</para>
        /// </summary>
        public const int ColonistCard = ColonistAvatar + 2 * ColonistCardPad;

        /// <summary>Between two candidate cards. Ten rather than six since the cards gained their
        /// own padding: a card with air inside it wants air around it, or the two runs of
        /// whitespace read as one and the cards stop being separate objects.</summary>
        public const int ColonistCardGap = 10;

        /// <summary>
        /// The column the three candidates stand in, beside the detail pane.
        ///
        /// <para><b>260 until 2026-09-18, and widened for the skills line.</b> The text column is
        /// this less the card's own padding, the face and the gap after it — 300 − 16 − 60 − 8 =
        /// 216, where 260 left 176. §3 of the avatar design sized it when the face was 30 and the
        /// card carried two lines; a 60 px face took 30 px off the text and a third line put a
        /// longer string on it. The page is the full viewport, not the fixed box, so the column is
        /// free to grow — which is what that design said the lever was.</para>
        ///
        /// <para>340 since the same day's type step: the text column is this less the card's own
        /// padding on both sides, the face and the gap after it — 340 − 16 − 60 − 8 = 256, which
        /// is where a name at <see cref="HudTextRole.Name"/> and a skills line at
        /// <see cref="HudTextRole.Row"/> want to be rather than where a 12 px meta line was
        /// comfortable.</para>
        /// </summary>
        public const int ColonistColumnWidth = 340;

        /// <summary>
        /// The three cards and the two rows under them — Keep, then Reroll.
        ///
        /// <para><b>No caption, because none is drawn.</b> This used to carry
        /// <c>StartSeedCaption + StartRowGap</c> for a "Your colonists" line and be measured
        /// against <see cref="StartListMax"/>, both of which described U40's standalone colonist
        /// screen. That screen is gone — <c>BuildSetupPage</c> draws the title, the board, the
        /// people and the footer as one full-viewport page — so the fast tier was asserting that a
        /// model of a screen nobody draws fitted a box it does not sit in. Whether the column
        /// really fits is a question for the layout engine, and <c>StartScreenTests</c> asks it of
        /// the laid-out elements.</para>
        /// </summary>
        public const float ColonistColumnHeight =
            3 * ColonistCard + 2 * ColonistCardGap + Gap                        // the three
            + StartRow + StartRow;                                              // Keep, Reroll

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

            // ---- the orders strip, in the same gutter directly under it. Its top is the rail's
            // bottom rather than a figure of its own: the rail is the one region the world sizes,
            // so anything below it in that column has to be placed from where it actually ended.
            boxes[HudRegion.OrdersStrip] = new HudRect(
                width - Edge - OrdersWidth, Edge + railHeight + RailToOrders,
                OrdersWidth, OrdersHeight);

            // ---- the views strip, under the orders in the same gutter, for the same reason
            boxes[HudRegion.ViewsStrip] = new HudRect(
                width - Edge - OrdersWidth, Edge + railHeight + RailToOrders + OrdersHeight + OrdersToViews,
                OrdersWidth, ViewsHeight);

            // ---- clock, just inside the rail
            float clockX = width - Edge - RailWidth - RailToClock - ClockWidth;
            boxes[HudRegion.Clock] = new HudRect(clockX, Edge, ClockWidth, ClockHeight);

            // ---- alerts, under the clock in the same column, hidden when there are none
            float alertsTop = Edge + ClockHeight + Gap;
            float alertsHeight = AlertsHeight(content.Alerts);
            boxes[HudRegion.Alerts] = content.Alerts <= 0
                ? default
                : new HudRect(clockX, alertsTop, ClockWidth, alertsHeight);

            // ---- events, under the alerts in the same column, or under the clock when there are
            // none, because the column is a flex column and a hidden panel takes no room in it.
            float bulletinsTop = alertsTop + (content.Alerts > 0 ? alertsHeight + Gap : 0f);
            float bulletinsHeight = BulletinsHeight(content.Bulletins);
            boxes[HudRegion.Bulletins] = content.Bulletins <= 0
                ? default
                : new HudRect(clockX, bulletinsTop, ClockWidth, bulletinsHeight);

            // ---- toasts, at the foot of that same column (SK4). Panel A6 puts the bulletin
            // stack "right edge, below the alerts" and a toast is the passing member of that
            // family, so it takes the same column and the same width. It is hidden when there are
            // none, which is most of the time — so the coverage criterion, which is stated with
            // nothing selected and no alerts, never sees it.
            //
            // <b>It is last in the column, under the Events panel, and that is the whole reason
            // the order is this way round.</b> A toast arrives every couple of minutes and leaves
            // six seconds later; anything below it in a stacked column would step down and back
            // up each time. Nothing is below it, so nothing moves.
            boxes[HudRegion.Toasts] = content.Toasts <= 0
                ? default
                : new HudRect(clockX,
                    bulletinsTop + (content.Bulletins <= 0 ? 0f : bulletinsHeight + Gap),
                    ClockWidth, ToastsHeight(content.Toasts));

            // ---- colonist strip, centred in what is left between the two top corners
            int cards = VisibleCards(width, height, content.Colonists);
            if (cards <= 0) boxes[HudRegion.ColonistStrip] = default;
            else
            {
                int perRow = Math.Max(1, CardsPerRow(width));
                bool hasPager = content.Colonists > perRow * StripRowsAllowed(height);
                int widest = hasPager ? perRow : Math.Min(cards, perRow);
                int rows = StripRowsUsed(width, height, cards);
                float cardsWidth = widest * CardWidth + (widest - 1) * CardGap;
                float stripWidth = cardsWidth + (hasPager ? PagerGap + PagerWidth : 0);
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

        /// <summary>The Events panel is built exactly as the alerts panel is, so it is as tall.</summary>
        public static float BulletinsHeight(int bulletins) =>
            bulletins <= 0 ? 0f : Frame + Pad + AlertHeaderBlock + bulletins * BulletinHeight +
                                  (bulletins - 1) * BulletinGap + Pad;

        /// <summary>
        /// The toast stack's height (SK4). One row per toast at the alerts' own row height, and
        /// <b>no header block</b>: an alerts panel earns a heading because it is a standing list a
        /// player returns to, and a toast is a line that is already leaving. A heading over it
        /// would also be the tallest thing in the stack for most of its life.
        /// </summary>
        public static float ToastsHeight(int toasts) =>
            toasts <= 0 ? 0f : Frame + Pad + toasts * AlertHeight + (toasts - 1) * AlertGap + Pad;

        /// <summary>
        /// The rail's chrome: everything that is not a cell.
        /// </summary>
        public const float RailChrome = Frame + Pad + RailLabel + RailToggleGap + RailToggle + Pad;

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
            //
            // And since 2026-09-17 the orders strip stands between the two, in the same gutter,
            // so the rail gives that room up as well. The rail is the region that gives, here as
            // everywhere: the strip is four fixed buttons and cannot be squeezed into fewer.
            // With the surface cell twice as tall (2x cell height), an N-layer board occupies
            // (N + 1) units of cell height, so room is apportioned over (layers + 1).
            float room = viewportHeight - Edge - RailChrome - RailCellGap
                         - (BarBottom + HudCommands.BarHeight + Frame + Gap)
                         - OrdersBlock;
            return Math.Max(MinRailPitch, Math.Min(ideal, room / (layers + 1)));
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
            return RailChrome + (layers + 1) * pitch;
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
        ///
        /// <para><b>The row counts no longer set a colonist pane's height</b> (2026-09-18). They
        /// say <i>whether</i> there is a body, and the body is <see cref="InspectTabBody"/>
        /// whichever tab is showing; see that member for why. They still size a tile's readout,
        /// which has no tab strip and so cannot resize under the player's hand.</para>
        /// </summary>
        public static float InspectHeight(int needRows, int skillRows, int cellRows = 0)
        {
            if (cellRows > 0)
            {
                // One row more than the facts: the shell prepends a location row to the readout
                // (owner, 2026-09-20 — coordinates moved out of the header's meta line).
                float rows = (cellRows + 1) * CellRow + cellRows * CellRowGap;

                // A tile keeps the shorter header: its slot holds an icon, not a portrait.
                return Frame + Pad + InspectHeaderNarrow + InspectHeaderGap + rows + Pad;
            }

            if (skillRows <= 0 && needRows <= 0) return 0f;

            return Frame + Pad + InspectHeader + InspectHeaderGap + InspectTabs + InspectTabGap +
                   InspectTabBody + Pad;
        }

        // ---------------------------------------------------------------- fitting

        /// <summary>
        /// Clearance between the colonist strip and the corner panels (stores on the left, clock
        /// on the right) so the strip sits centred with breathing room and keeps the resting HUD
        /// within <see cref="CoverageCeiling"/> at small viewports.
        /// </summary>
        public const int StripClearance = 32;

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
            float freeLeft = Edge + StoresWidth + StripClearance;
            float freeRight = width - (Edge + RailWidth + RailToClock + ClockWidth) - StripClearance;
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
            int fit = Math.Max(0, (int)Math.Floor((room - PagerWidth - PagerGap + CardGap) / (CardWidth + CardGap)));
            return Math.Min(StripCardsCap, fit);
        }

        /// <summary>How many pages a given colony size requires at this resolution.</summary>
        public static int PageCount(float width, float height, int colonists)
        {
            int capacity = VisibleCards(width, height, int.MaxValue / 2);
            return capacity <= 0 || colonists <= 0 ? 1 : Math.Max(1, (colonists + capacity - 1) / capacity);
        }

        /// <summary>
        /// How many rows this screen is tall enough to spend on the strip.
        /// Always 1 row (owner, 2026-09-18: "Should be one row with 6 on an more (no 2 rows or anthing - always one)").
        /// </summary>
        public static int StripRowsAllowed(float height) => 1;

        /// <summary>How many rows that many cards actually occupy, 0 to 1.</summary>
        public static int StripRowsUsed(float width, float height, int cards)
        {
            int perRow = CardsPerRow(width);
            if (cards <= 0 || perRow <= 0) return 0;
            return 1;
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
        /// Where the armed banner sits: one ordinary panel gap above the command bar (owner,
        /// 2026-09-17: <i>"lower that dialog closer to the toolbar at the bottom"</i>).
        ///
        /// <para>It was 92 px, which is 43 px of clear board above a 49 px bar — enough for the
        /// banner to read as floating in the middle of nothing rather than as belonging to the row
        /// of controls it is about. Derived from the bar rather than picked, so it follows the bar
        /// if that ever moves, and one <see cref="Gap"/> rather than flush because the banner is
        /// not docked on the bar the way a popover is: it is a thing over the board that has come
        /// down to meet it.</para>
        /// </summary>
        public static float ArmedBottom => PopoverBottom + Gap;

        /// <summary>The armed banner's padding, across. Its own, not <see cref="Pad"/>: the banner
        /// is a pill over the board rather than a panel with rows in it.</summary>
        public const int ArmedPadX = 14;

        /// <summary>
        /// The width the armed banner never drops below: the longest order it can name (owner,
        /// 2026-09-17: <i>"make the dialog a fixed predictable width — as the longest order …
        /// make it at least this size until further notice (and let it extend beyond that)"</i>).
        ///
        /// <para><b>A floor, not a width</b>, which is the owner's own "let it extend beyond
        /// that": an armed build reads "Building wall of wood" and is wider than any order. What
        /// the floor buys is that the four orders — the things a player picks up and puts down
        /// over and over — all draw the same box, so the banner stops resizing under the eye every
        /// time the mode changes.</para>
        ///
        /// <para><b>Computed from the names rather than written down.</b> The owner's guess was
        /// that "Chopping" would be the longest; it is "Deconstruct", which is the sort of thing
        /// that is only true until somebody renames something in <c>icon-keys.csv</c>. Walking
        /// <see cref="PaletteTools.Pinned"/> through <see cref="PaletteTools.OrderWord"/> means a
        /// rename moves this number with it. The estimate is <see cref="HudCommands.UiAdvance"/>,
        /// the same deliberately generous advance the command bar's overflow is modelled with —
        /// generous is the safe direction for a minimum.</para>
        /// </summary>
        public static float ArmedWidth
        {
            get
            {
                float widest = 0f;
                float size = HudType.Of(HudTextRole.Name).Size;
                foreach (string key in PaletteTools.Pinned)
                {
                    float width = PaletteTools.OrderWord(key).Length * size * HudCommands.UiAdvance;
                    if (width > widest) widest = width;
                }
                return widest + 2 * ArmedPadX + 2 * HudTheme.ArmedBorderWidth;
            }
        }

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

        /// <summary>
        /// Where a popover's bottom edge goes when it is raised by <b>a row inside a panel</b>
        /// rather than by a button on the command bar: sitting on top of that row.
        ///
        /// <para><b>Why the bar's own <see cref="PopoverBottom"/> is wrong for it.</b> That
        /// constant pins a popover flush to the command bar, which is right for every popover the
        /// bar raises and exactly wrong for one raised 300 px up the screen — the panel opens at
        /// the bottom of the window, detached from the control that asked for it, and on this
        /// screen it lands on top of the very pane the row is in. The owner clicked Assign twice
        /// over two sessions and reported nothing happening; a popover that opens somewhere you
        /// are not looking and covers what you clicked is indistinguishable from one that does not
        /// open at all.</para>
        ///
        /// <para>Above the row rather than below it, because a popover below would cover the rows
        /// under the one being answered — and those rows are the readout the choice is about.
        /// Flipped under the row only when there is not the height for it above, and clamped so
        /// that a long list never hangs off the top of the screen.</para>
        ///
        /// <para>All four arguments are measured downward from the top, the way a laid-out panel
        /// reports itself; the answer is measured upward from the bottom, the way USS wants it.
        /// That inversion is the whole reason this is arithmetic in a testable place rather than
        /// two lines at the call site.</para>
        /// </summary>
        public static float PopoverBottomFor(
            float anchorTop, float anchorHeight, float popoverHeight, float screenHeight)
        {
            if (screenHeight <= 0f) return 0f;

            float above = screenHeight - anchorTop;
            float below = screenHeight - (anchorTop + anchorHeight) - popoverHeight;

            // Above unless the top of the screen is in the way, and never below zero: a popover
            // pushed off the bottom is as lost as one pushed off the top.
            float wanted = above + popoverHeight <= screenHeight ? above : below;
            float highest = Math.Max(0f, screenHeight - popoverHeight);
            return Math.Max(0f, Math.Min(wanted, highest));
        }

        /// <summary>
        /// How far the context menu's corner stands off the pointer (design 33 §7a), in panel
        /// pixels: enough that the pointer's own arrow does not sit on the first row's text, and
        /// no more, so the menu still reads as raised by the click.
        /// </summary>
        public const int ContextMenuNudge = 2;

        /// <summary>
        /// Where the context menu's left edge goes: just right of the pointer, or — when that
        /// would run off the right of the screen — just left of it, the way a desktop's menus
        /// turn; and never off either edge. Measured in panel pixels, like everything here.
        /// </summary>
        public static float ContextMenuLeft(float pointerX, float menuWidth, float screenWidth) =>
            ContextMenuEdge(pointerX, menuWidth, screenWidth);

        /// <summary>
        /// Where the context menu's top edge goes: just below the pointer, or just above it when
        /// the rows would run off the bottom — a right-click low on the board opens upward, over
        /// the board rather than under the command bar. Measured down from the top.
        /// </summary>
        public static float ContextMenuTop(float pointerY, float menuHeight, float screenHeight) =>
            ContextMenuEdge(pointerY, menuHeight, screenHeight);

        static float ContextMenuEdge(float pointer, float size, float screen)
        {
            float edge = pointer + ContextMenuNudge;
            if (edge + size > screen) edge = pointer - ContextMenuNudge - size;
            float widest = Math.Max(0f, screen - size);
            return Math.Max(0f, Math.Min(edge, widest));
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

        /// <summary>
        /// The ceiling the acceptance criteria put on <see cref="Coverage"/> with nothing
        /// selected. The HUD this replaces measured about 0.31.
        ///
        /// <para><b>Nineteen per cent since 2026-09-17, and the one per cent is the orders strip.</b>
        /// It was eighteen, which is the figure the interface specification names. The strip is
        /// four always-on buttons in the right-hand gutter — the owner's answer to giving an order
        /// costing a trip through the Build palette first — and it is <b>0.80%</b> of a 1280 x 720
        /// canvas, which took the resting HUD there from 17.75% to <b>18.55%</b>.</para>
        ///
        /// <para><b>Why the ceiling moved rather than the strip.</b> The precedent in this file is
        /// the opposite one: two rows of colonist cards were 8.1% at 720p and
        /// <see cref="StripHeightShare"/> clamped the region rather than spending the budget. That
        /// worked because the strip had a variable height to clamp. This one has four buttons, and
        /// even at the 26 px squares it wore in the palette header it measures 0.65% — so the
        /// choice here is the control or the number, not a cheaper version of the control. The
        /// smallest canvas the game draws is the only one affected: 1280 x 720 is what 150 per
        /// cent interface scale gives on a 1080p monitor, and at the reference canvas the resting
        /// HUD is well under either figure.</para>
        ///
        /// <para>Per region at 1280 x 720, with the strip clamped to the one row it gets there:
        /// command bar 6.81%, colonist strip 3.81%, stores 2.59%, clock 2.57%, depth rail 1.97%,
        /// orders 0.80%. <b>The bar is the largest single spend</b> and it is a full-width docked
        /// bar by the owner's instruction, where the specification drew a centred pill; that is
        /// where to look first if this ever has to come back down.</para>
        ///
        /// <para><b>Twenty per cent since 2026-09-18, and the one per cent is the doubled avatar</b>
        /// (owner: *"can we make the in-game avatar profile twice as big as it's hard to see"*).
        /// A roster card went from 106 x 63 to 126 x 89 to hold a 52 px face, which takes a
        /// <b>two-row</b> strip at 1280 x 720 from 3.81% to <b>5.07%</b> and the whole HUD to
        /// <b>19.80%</b>. Two things about that number. It is the forced worst case rather than
        /// what the game draws: at 1280 x 720 the strip is allowed **one** row, and the resting
        /// measurement there is still under the old 19% — <c>CoverageWithNothingSelected-
        /// IsUnderTheCeiling</c> never failed through any of this. And the usual lever is not
        /// available: the previous two occasions clamped the region instead of raising the
        /// ceiling, but clamping here means showing fewer colonists, and the card is the size it
        /// is because the owner asked for the face in it to be legible.</para>
        ///
        /// <para><b>It is the owner's to reverse</b>, exactly as the orders strip's one per cent
        /// is. The cheapest reversal is the avatar: every pixel of it is four pixels of card area
        /// and the card is what the strip is made of.</para>
        ///
        /// <para><b>The name pool nearly took it to 0.21 and the owner declined</b>, 2026-09-18.
        /// Sizing the card to the widest of 244 owner-supplied names would have put the forced
        /// two-row strip at 20.19%; the answer was *"remove any longer names for now"*, so the
        /// card kept its width and the pool lost the names that did not fit
        /// (<see cref="CardNameBudget"/>). Worth keeping in view if the pool is ever the thing
        /// that matters more than the ceiling: it is one line here and one in
        /// <see cref="CardWidth"/>.</para>
        /// </summary>
        public const float CoverageCeiling = 0.20f;
    }
}
