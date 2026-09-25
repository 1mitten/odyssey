#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The interface acceptance criteria, as arithmetic.
    ///
    /// <para>Each of these was a sentence in a specification that nothing could check: "no two HUD
    /// panels overlap at 1280x720, 1920x1080 or 2560x1440", "total HUD coverage at or under the
    /// ceiling with nothing selected" (18% as specified, 19% since the orders strip — see
    /// <see cref="HudLayout.CoverageCeiling"/>), "every command-bar item has a visible hotkey; nothing
    /// is cut off at the right edge", "body text at 4.5:1 or better against the scrim over the
    /// brightest terrain in the game". They are testable here because the geometry, the palette
    /// and the bar's overflow rule all moved into the Unity-free assembly. The PlayMode gate then
    /// measures the realised boxes against the same model, so this cannot pass by describing a
    /// screen the shell is not building.</para>
    ///
    /// <para>The three resolutions the criteria name are all 16:9, and the panel scales with the
    /// screen against a 1920x1080 reference, so on a real monitor they resolve to one logical
    /// canvas. They are still run separately: the layout is anchored rather than scaled, so it has
    /// to be right at a literal 1280x720 as well — a host with a fixed-size panel, or a future
    /// decision to stop scaling, should not silently break it.</para>
    /// </summary>
    public class HudLayoutTests
    {
        static readonly (int W, int H)[] Resolutions = { (1280, 720), (1920, 1080), (2560, 1440) };

        /// <summary>A colony as the vertical slice ships it: three colonists, sixteen layers.</summary>
        const int Colonists = 3;
        const int Layers = 16;

        /// <summary>Every commodity the ledger knows, which is the expanded stores panel — the
        /// tallest that region ever gets.</summary>
        const int AllStoreRows = 6;

        /// <summary>
        /// The start screen stands entirely inside the canvas, at every resolution and every
        /// interface scale.
        ///
        /// <para>The question the overlap test asks of the playing HUD, asked the only way it can
        /// be asked of a screen with nothing beside it. <b>The smallest canvas this game draws is
        /// 1280 x 720</b>, which is what 150 per cent interface scale produces on a 1080p monitor —
        /// so it is checked at a literal 1280 x 720 <i>and</i> at every scale's reference canvas,
        /// because those are two different ways of arriving at the same small box and only one of
        /// them is obvious.</para>
        ///
        /// <para>A modal that runs off the top of a small canvas hides its own first row, and the
        /// row a start screen hides first is New game.</para>
        /// </summary>
        [Test]
        public void TheStartScreenFitsEveryCanvasThisGameDraws()
        {
            foreach ((int width, int height) in Resolutions)
                Assert.That(HudLayout.StartScreenFits(width, height), Is.True,
                    $"the start screen does not fit {width}x{height}");

            foreach (int percent in SettingsDirector.UiScales)
            {
                (int width, int height) = HudLayout.ReferenceFor(percent);
                Assert.That(HudLayout.StartScreenFits(width, height), Is.True,
                    $"the start screen does not fit the {width}x{height} canvas that {percent}% " +
                    "interface scale produces");
            }
        }

        /// <summary>
        /// The panel is the same box whatever screen is showing (owner, 2026-09-17: *"keep it fixed
        /// width and height because it becomes hard to read between loading and saving screens"*).
        ///
        /// <para>It is centred, so a panel that changed height would move every row under the
        /// pointer on the way from the root screen to the load screen. The box is a constant now,
        /// and the body inside it is what the screens share — so this test is really asking whether
        /// the body can still hold each screen without the panel having to grow.</para>
        /// </summary>
        [Test]
        public void EveryStartScreenFitsTheOneFixedBody()
        {
            int rootRows = 0;
            foreach (SessionCommand _ in SessionCommands.For(SessionContext.MainScreen)) rootRows++;

            Assert.That(HudLayout.StartRowsHeight(rootRows),
                Is.LessThanOrEqualTo(HudLayout.StartBody),
                "the root screen's own rows do not fit the fixed body");

            // The load screen is the list at its ceiling plus the row that goes back, which is how
            // StartBody was derived — so this is the arithmetic closing on itself, and it fails the
            // day somebody changes one of the two without the other.
            float load = HudLayout.StartListMax + HudLayout.StartRowGap + 1 + HudLayout.StartRow;
            Assert.That(load, Is.EqualTo((float)HudLayout.StartBody),
                "the load screen's list and its back row do not add up to the body they sit in");

            Assert.That(HudLayout.StartSavesBeforeScrolling, Is.GreaterThanOrEqualTo(5),
                "a load list that scrolls at four rows is a list, not a screen");
            Assert.That(HudLayout.StartListHeight(HudLayout.StartSavesBeforeScrolling),
                Is.LessThanOrEqualTo(HudLayout.StartListMax),
                "the number of saves said to fit does not fit");
            Assert.That(HudLayout.StartListHeight(HudLayout.StartSavesBeforeScrolling + 1),
                Is.GreaterThan(HudLayout.StartListMax),
                "one more save than the ceiling allows still fits, so the ceiling is not the ceiling");

            // The New game screen (U39) sits where the list sits, with the same row beneath it.
            // Four controls in a body sized for six save rows is obviously true right up until
            // somebody adds a fifth, which is exactly why it is asserted rather than eyeballed.
            Assert.That(HudLayout.StartNewGameHeight,
                Is.LessThanOrEqualTo(HudLayout.StartListMax),
                "the New game screen does not fit the region the load list already fits in");

            // The candidate cards are deliberately NOT asserted here any more. They left the fixed
            // box when U40's colonist screen became part of the full-viewport setup page, so
            // measuring them against the load list's ceiling was asking whether a screen nobody
            // draws fits a box it does not sit in. What they owe is the test below, and a measured
            // one in StartScreenTests.
        }

        /// <summary>
        /// A card is at least as tall as the face it carries.
        ///
        /// <para><b>The check whose absence cost the setup page a visible defect.</b> When the
        /// avatar doubled on 2026-09-18 (<c>docs/design/20-avatars.md</c> §10.6) every card that
        /// carries one was re-derived except the candidate card, which kept the 47 it was given
        /// while <see cref="HudLayout.Avatar"/> was 30 and then drew a 60 px face in it. Nothing
        /// failed: the sheet agreed with the model, the model agreed with itself, and the only
        /// witness was three overlapping faces in `Logs/setup-page.png`. One line of arithmetic
        /// that nobody thought to write down is the whole difference.</para>
        /// </summary>
        [Test]
        public void EveryCardIsAtLeastAsTallAsTheFaceItCarries()
        {
            // Plus the pad on both sides, since the owner played it: a card sized to exactly its
            // face draws the selection outline hard against the portrait, which reads as the
            // portrait's frame rather than the card's.
            Assert.That(HudLayout.ColonistCard,
                Is.GreaterThanOrEqualTo(HudLayout.ColonistAvatar + 2 * HudLayout.ColonistCardPad),
                "a candidate card does not clear its own avatar by the card's padding, so the " +
                "faces overlap each other and the selection outline sits on the portrait");

            Assert.That(HudLayout.CardHeight, Is.GreaterThanOrEqualTo(HudLayout.CardAvatar),
                "a roster card is shorter than its own avatar");

            Assert.That(HudLayout.InspectHeader, Is.GreaterThanOrEqualTo(HudLayout.Avatar),
                "the inspect header is shorter than the avatar it holds");

            // The name, the activity line and the pace stand beside the portrait in its height
            // (design 17 §5a). The header is fixed, so a line that does not fit is not a taller
            // pane — it is text drawn over the tabs.
            Assert.That(HudLayout.InspectHeaderText, Is.LessThanOrEqualTo(HudLayout.InspectHeader),
                "the header's lines of text no longer fit beside the portrait");
        }

        [Test]
        public void NoTwoPanelsOverlapAtAnyOfTheThreeResolutions()
        {
            foreach ((int width, int height) in Resolutions)
            foreach (HudContent content in Cases())
            {
                var boxes = HudLayout.Solve(width, height, content);
                (HudRegion A, HudRegion B)? clash = HudLayout.FirstOverlap(boxes);

                Assert.That(clash, Is.Null,
                    clash == null ? string.Empty
                        : $"at {width}x{height} the {clash.Value.A} panel {boxes[clash.Value.A]} " +
                          $"overlaps the {clash.Value.B} panel {boxes[clash.Value.B]}");
            }
        }

        [Test]
        public void EveryPanelStaysInsideTheViewport()
        {
            foreach ((int width, int height) in Resolutions)
            foreach (HudContent content in Cases())
            foreach (KeyValuePair<HudRegion, HudRect> entry in HudLayout.Solve(width, height, content))
            {
                HudRect box = entry.Value;
                if (box.Empty) continue;

                Assert.That(box.X, Is.GreaterThanOrEqualTo(0f), $"{entry.Key} starts off the left edge at {width}x{height}");
                Assert.That(box.Y, Is.GreaterThanOrEqualTo(0f), $"{entry.Key} starts above the top edge at {width}x{height}");
                Assert.That(box.Right, Is.LessThanOrEqualTo(width),
                    $"{entry.Key} runs {box.Right - width:0.#} px past the right edge at {width}x{height}");
                Assert.That(box.Bottom, Is.LessThanOrEqualTo(height),
                    $"{entry.Key} runs {box.Bottom - height:0.#} px past the bottom edge at {width}x{height}");
            }
        }

        /// <summary>
        /// The headline criterion: the HUD covers at most <see cref="HudLayout.CoverageCeiling"/>
        /// of the screen with nothing selected, down from about 31%. Asserted in the state the
        /// criterion names — a colony running, no selection, no alerts — and the stores panel
        /// collapsed, which is its own default.
        ///
        /// <para>The ceiling is 19% and was 18%, the specification's figure; the orders strip is
        /// what moved it, and <see cref="HudLayout.CoverageCeiling"/> carries the measurement and
        /// the argument.</para>
        ///
        /// <para><b>Measured on the logical canvas, and that is the honest place to measure it.</b>
        /// The panel scales with the screen against a 1920x1080 reference, so all three
        /// resolutions the criteria name are 16:9 and resolve to exactly this one canvas: a panel
        /// covers the same <i>fraction</i> of a 1280x720 monitor as of a 4K one, which is the whole
        /// point of scaling with the screen. Measuring a literal 1280x720 canvas would be measuring
        /// a configuration the game does not ship — the HUD would be drawn at 1080p sizes on a
        /// 720p canvas, which is not what a player ever sees.</para>
        ///
        /// <para>The second case is a 4:3 screen. Unity's MatchWidthOrHeight at a match of 0.5
        /// takes the geometric mean of the two scale factors, so the logical canvas keeps very
        /// nearly the same <i>area</i> whatever the aspect — 1663x1247 against 1920x1080 is 2.073
        /// million square pixels against 2.074 — and the coverage figure is therefore a property of
        /// the layout rather than of the monitor. That is worth pinning, because it is the reason
        /// one number can answer the criterion for every screen.</para>
        /// </summary>
        [Test]
        public void CoverageWithNothingSelectedIsUnderTheCeiling()
        {
            (int W, int H, string What)[] canvases =
            {
                (1920, 1080, "the 16:9 canvas every resolution the criteria name resolves to"),
                (1663, 1247, "the canvas a 4:3 screen resolves to at the same panel scale"),
            };

            foreach ((int width, int height, string what) in canvases)
            {
                HudContent content = HudContent.NothingSelected(Colonists, storeRows: 3, Layers);
                var boxes = HudLayout.Solve(width, height, content);
                float coverage = HudLayout.Coverage(boxes, width, height);

                TestContext.WriteLine($"{width}x{height} ({what}): {coverage:P1} covered — " +
                    string.Join(", ", boxes.Select(b => $"{b.Key} {b.Value.Area / (width * height):P1}")));

                Assert.That(coverage, Is.LessThanOrEqualTo(HudLayout.CoverageCeiling),
                    $"at {width}x{height} the HUD covers {coverage:P1} of the viewport, over the " +
                    $"{HudLayout.CoverageCeiling:P0} ceiling. Per region: " +
                    string.Join(", ", boxes.Select(b => $"{b.Key} {b.Value.Area / (width * height):P1}")));
            }
        }

        /// <summary>
        /// And the worst case is reported rather than asserted, because the criterion is stated
        /// about the resting screen. A colonist selected and the stores panel thrown open is the
        /// most the HUD ever shows, and it is worth knowing what that costs.
        /// </summary>
        [Test]
        public void TheBusiestScreenIsMeasuredAndReported()
        {
            var content = new HudContent(Colonists, AllStoreRows, alerts: 2, Layers, needRows: 2);
            var boxes = HudLayout.Solve(1920, 1080, content);
            float coverage = HudLayout.Coverage(boxes, 1920, 1080);

            TestContext.WriteLine($"Busiest screen at 1920x1080: {coverage:P1} covered — " +
                string.Join(", ", boxes.Select(b => $"{b.Key} {b.Value}")));

            // Not the resting ceiling, but it must still be less than the HUD it replaces, which
            // measured about 31% doing nothing at all.
            Assert.That(coverage, Is.LessThan(0.31f),
                "the busiest screen covers more than the old resting HUD did, which is not a rebuild");
        }

        [Test]
        public void TheColonistStripNeverRunsIntoThePanelsEitherSideOfIt()
        {
            // A colony far larger than the slice will ever have: the strip must clamp rather than
            // grow into the stores panel, which is what makes "no two panels overlap" true by
            // construction rather than true for the colony sizes somebody happened to try.
            foreach ((int width, int height) in Resolutions)
            {
                var content = new HudContent(colonists: 40, storeRows: AllStoreRows, alerts: 3,
                    layers: Layers, needRows: 2);
                var boxes = HudLayout.Solve(width, height, content);

                Assert.That(HudLayout.FirstOverlap(boxes), Is.Null,
                    $"a colony of forty overflows the strip at {width}x{height}");
                Assert.That(HudLayout.VisibleCards(width, height, 40), Is.GreaterThan(0),
                    $"no colonist card fits at all at {width}x{height}");
            }
        }

        /// <summary>
        /// The strip stands at exactly one row (owner, 2026-09-18: "Should be one row with 6 on an more (no 2 rows or anthing - always one)").
        /// </summary>
        [Test]
        public void TheStripIsAlwaysOneRowAndNoFurther()
        {
            foreach ((int width, int height) in Resolutions)
            {
                int perRow = HudLayout.CardsPerRow(width);
                int allowed = HudLayout.StripRowsAllowed(height);
                Assert.That(perRow, Is.GreaterThan(0), $"no card fits at all at {width}x{height}");
                Assert.That(allowed, Is.EqualTo(1), "the strip is strictly one row");

                Assert.That(HudLayout.StripRowsUsed(width, height, perRow), Is.EqualTo(1),
                    "a full first row occupies one row");
                Assert.That(HudLayout.StripRowsUsed(width, height, perRow + 1), Is.EqualTo(1),
                    "cards past a full row are paginated into the same one row");
                Assert.That(HudLayout.StripRowsUsed(width, height, perRow * 5), Is.EqualTo(1),
                    "the strip must stop at its 1-row cap however large the colony is");

                Assert.That(HudLayout.VisibleCards(width, height, 500),
                    Is.EqualTo(perRow * allowed),
                    "a colony past what the strip holds shows what fits and no more");

                // The box is as wide as the widest row, not as the whole colony laid end to end.
                var full = new HudContent(colonists: 500, storeRows: AllStoreRows, alerts: 3,
                    layers: Layers, needRows: 2);
                var boxes = HudLayout.Solve(width, height, full);
                HudRect strip = boxes[HudRegion.ColonistStrip];

                Assert.That(strip.Height,
                    Is.EqualTo(HudLayout.StripHeight(allowed)).Within(0.01f));
                Assert.That(strip.Height,
                    Is.LessThanOrEqualTo(height * HudLayout.StripHeightShare + 0.01f),
                    $"the strip stands in more than its share of a {width}x{height} screen");
                Assert.That(strip.Width, Is.LessThanOrEqualTo(HudLayout.StripRoom(width) + 0.01f),
                    $"the strip is wider than the room it may occupy at {width}x{height}");
                Assert.That(HudLayout.FirstOverlap(boxes), Is.Null,
                    $"strip runs into another region at {width}x{height}");

                // The ceiling is stated against the resting HUD, as the criteria are.
                var resting = HudContent.NothingSelected(colonists: 500, storeRows: 3, layers: Layers);
                var restingBoxes = HudLayout.Solve(width, height, resting);
                float coverage = HudLayout.Coverage(restingBoxes, width, height);
                TestContext.WriteLine($"Full one-row strip at {width}x{height}: {coverage:P2} — " +
                    string.Join(", ", restingBoxes.Select(b => $"{b.Key} {b.Value.Area / (width * height):P2}")));

                Assert.That(coverage, Is.LessThanOrEqualTo(HudLayout.CoverageCeiling),
                    $"a full one-row strip puts the resting HUD over its coverage ceiling at " +
                    $"{width}x{height}: {coverage:P2}. Per region: " +
                    string.Join(", ", restingBoxes.Select(b => $"{b.Key} {b.Value.Area / (width * height):P2}")));
            }
        }

        [Test]
        public void CardsPerRowIsCappedAtSixCards()
        {
            foreach ((int width, _) in Resolutions)
            {
                Assert.That(HudLayout.CardsPerRow(width), Is.LessThanOrEqualTo(HudLayout.StripCardsCap));
            }
            Assert.That(HudLayout.CardsPerRow(1920), Is.EqualTo(6));
            Assert.That(HudLayout.CardsPerRow(2560), Is.EqualTo(6));
        }

        [Test]
        public void PaginatedStripKeepsFixedBoundingBoxAcrossDifferentVisibleCardCounts()
        {
            // When total colonists exceed 1 row capacity (6), whether the current page shows 6 cards
            // or 1 card, the solved box keeps the same width and position flush next to the 6th slot.
            var fullPage = new HudContent(colonists: 10, storeRows: 3, alerts: 0, layers: 16, needRows: 0);
            var partialPage = new HudContent(colonists: 7, storeRows: 3, alerts: 0, layers: 16, needRows: 0);

            var fullBox = HudLayout.Solve(1920, 1080, fullPage)[HudRegion.ColonistStrip];
            var partialBox = HudLayout.Solve(1920, 1080, partialPage)[HudRegion.ColonistStrip];

            Assert.That(fullBox.Width, Is.EqualTo(partialBox.Width).Within(0.01f));
            Assert.That(fullBox.X, Is.EqualTo(partialBox.X).Within(0.01f));
        }

        /// <summary>
        /// The command bar is the full width of the screen and sits on its bottom edge (owner,
        /// 2026-09-17), and a popover raised from it lands on top of it with no gap.
        /// </summary>
        [Test]
        public void TheBarSpansTheScreenAndItsPopoversSitOnIt()
        {
            foreach ((int width, int height) in Resolutions)
            foreach (HudContent content in Cases())
            {
                var boxes = HudLayout.Solve(width, height, content);
                HudRect bar = boxes[HudRegion.CommandBar];

                Assert.That(bar.X, Is.EqualTo(0f).Within(0.01f), $"the bar starts inset at {width}x{height}");
                Assert.That(bar.Width, Is.EqualTo((float)width).Within(0.01f),
                    $"the bar is {bar.Width:0.#} px on a {width} px screen");
                Assert.That(bar.Bottom, Is.EqualTo((float)height).Within(0.01f),
                    "the bar is not on the bottom edge of the screen");

                // A popover's bottom is measured from the bottom of the screen, so it equals the
                // bar's height exactly when the two are flush.
                Assert.That(height - HudLayout.PopoverBottom, Is.EqualTo(bar.Y).Within(0.01f),
                    $"a popover would sit {bar.Y - (height - HudLayout.PopoverBottom):0.#} px " +
                    "away from the bar it was raised from");
            }
        }

        /// <summary>
        /// A popover raised by a <b>row inside a panel</b> sits on that row, not on the command
        /// bar at the bottom of the screen.
        ///
        /// <para>Written because the bed's owner picker used the bar's own constant. The row is
        /// most of the way up a 1080 px screen and the popover opened at the bottom of it —
        /// detached from the control that asked for it and lying on top of the pane the row is in.
        /// The owner clicked Assign across two sessions and reported nothing happening both times,
        /// which is what a popover you are not looking at is indistinguishable from.</para>
        /// </summary>
        [Test]
        public void APopoverRaisedByARowSitsOnThatRow()
        {
            const float screen = 1080f;

            // A row 700 px down the screen: its top is 380 px up from the bottom, and a popover
            // that tall or less sits there.
            Assert.That(HudLayout.PopoverBottomFor(700f, 20f, 150f, screen), Is.EqualTo(380f),
                "the popover's bottom edge meets the row's top edge");

            // Nothing about the answer is the command bar's, which is the whole point.
            Assert.That(HudLayout.PopoverBottomFor(700f, 20f, 150f, screen),
                Is.Not.EqualTo(HudLayout.PopoverBottom));

            // Too tall to fit above: it flips under the row rather than hanging off the top.
            Assert.That(HudLayout.PopoverBottomFor(100f, 20f, 600f, screen), Is.EqualTo(360f),
                "under the row, with its own height below the row's bottom edge");

            // And a popover taller than the screen starts at the bottom rather than below it.
            Assert.That(HudLayout.PopoverBottomFor(100f, 20f, 2000f, screen), Is.EqualTo(0f));
            Assert.That(HudLayout.PopoverBottomFor(700f, 20f, 150f, 0f), Is.EqualTo(0f),
                "a screen that has not been laid out yet answers zero rather than a negative");
        }

        /// <summary>
        /// The context menu opens at the pointer (design 33 §7a), turns to the pointer's other
        /// side where it would run off the right or the bottom, and never leaves the screen.
        /// </summary>
        [Test]
        public void TheContextMenuOpensAtThePointerAndTurnsAtTheEdges()
        {
            const float screen = 1920f, tall = 1080f, menu = 200f, rows = 90f;
            float nudge = HudLayout.ContextMenuNudge;

            Assert.That(HudLayout.ContextMenuLeft(500f, menu, screen), Is.EqualTo(500f + nudge),
                "in the open it hangs right of the pointer");
            Assert.That(HudLayout.ContextMenuTop(400f, rows, tall), Is.EqualTo(400f + nudge),
                "and below it");

            Assert.That(HudLayout.ContextMenuLeft(1850f, menu, screen), Is.EqualTo(1850f - nudge - menu),
                "near the right edge it turns to the pointer's left rather than being pushed under it");
            Assert.That(HudLayout.ContextMenuTop(1050f, rows, tall), Is.EqualTo(1050f - nudge - rows),
                "near the bottom it opens upward");

            Assert.That(HudLayout.ContextMenuLeft(100f, 3000f, screen), Is.EqualTo(0f),
                "a menu wider than the screen starts at the left edge");
            Assert.That(HudLayout.ContextMenuTop(50f, 2000f, tall), Is.EqualTo(0f),
                "and one taller than it at the top");
            Assert.That(HudLayout.ContextMenuLeft(1f, menu, 150f), Is.EqualTo(0f),
                "turned, it is still never off the left edge");
        }

        /// <summary>
        /// A popover lines up with the button that raised it, and is pushed back on to the screen
        /// rather than hanging off it.
        /// </summary>
        [Test]
        public void APopoverFollowsItsButtonAndStaysOnTheScreen()
        {
            const float screen = 1920f;
            const float popover = 420f;

            Assert.That(HudLayout.PopoverLeft(0f, popover, screen), Is.EqualTo(0f),
                "a popover raised by the leftmost button starts at the left edge");
            Assert.That(HudLayout.PopoverLeft(300f, popover, screen), Is.EqualTo(300f),
                "a popover in the middle of the bar lines up with its button");

            Assert.That(HudLayout.PopoverLeft(1800f, popover, screen), Is.EqualTo(screen - popover),
                "a popover raised near the right edge is pushed back rather than hanging off");
            Assert.That(HudLayout.PopoverLeft(1800f, popover, screen) + popover,
                Is.EqualTo(screen).Within(0.01f),
                "and lands flush with the right edge, like the bar under it");

            Assert.That(HudLayout.PopoverLeft(200f, 3000f, screen), Is.EqualTo(0f),
                "a popover wider than the screen starts at the left edge rather than negative");
            Assert.That(HudLayout.PopoverLeft(-50f, popover, screen), Is.EqualTo(0f),
                "and never off the left edge either");
        }

        [Test]
        public void TheInspectPaneNeverReachesTheCommandBar()
        {
            foreach ((int width, int height) in Resolutions)
            foreach (HudContent content in Cases())
            {
                var boxes = HudLayout.Solve(width, height, content);
                HudRect pane = boxes[HudRegion.Inspect];
                HudRect bar = boxes[HudRegion.CommandBar];

                Assert.That(pane.Bottom, Is.LessThanOrEqualTo(bar.Y),
                    $"the inspect pane ends at {pane.Bottom:0.#} and the command bar starts at " +
                    $"{bar.Y:0.#} at {width}x{height}. The pane's bottom offset assumes the bar is " +
                    "one row tall, which is only safe because the bar may not wrap.");
            }
        }

        /// <summary>
        /// The orders strip is in the right-hand gutter, under the depth rail, clear of the
        /// command bar — at every resolution and on every board, including the deep one where the
        /// rail is squeezed.
        ///
        /// <para><b>The last clause is the whole test.</b> The strip is a fixed four buttons and
        /// the rail is the region the world sizes, so the only way the two can come to disagree is
        /// the rail growing into the strip's room — which is exactly what would have happened had
        /// <see cref="HudLayout.RailPitch"/> gone on measuring down to the command bar as it did
        /// before the strip existed. The rail is the region that gives, here as everywhere.</para>
        /// </summary>
        [Test]
        public void TheOrdersStripStandsInTheGutterUnderTheRail()
        {
            foreach ((int width, int height) in Resolutions)
            foreach (HudContent content in Cases())
            {
                var boxes = HudLayout.Solve(width, height, content);
                HudRect rail = boxes[HudRegion.DepthRail];
                HudRect orders = boxes[HudRegion.OrdersStrip];
                HudRect bar = boxes[HudRegion.CommandBar];

                Assert.That(orders.Right, Is.EqualTo(rail.Right).Within(0.01f),
                    $"the strip and the rail are not in one gutter at {width}x{height}");
                Assert.That(orders.Right, Is.EqualTo(width - HudLayout.Edge).Within(0.01f),
                    "the strip is not against the right edge of the screen");
                Assert.That(orders.Y, Is.EqualTo(rail.Bottom + HudLayout.RailToOrders).Within(0.01f),
                    "the strip is not directly under the rail");
                Assert.That(orders.Bottom, Is.LessThanOrEqualTo(bar.Y + 0.01f),
                    $"the strip ends at {orders.Bottom:0.#} and the command bar starts at " +
                    $"{bar.Y:0.#} at {width}x{height} on a {content.Layers}-layer board");
            }
        }

        /// <summary>
        /// The views strip (design 32 §14) stands directly under the orders in the same gutter and
        /// still ends above the command bar — on the deep board at 720p too, which is the case the
        /// rail's squeeze now has to leave room for twice.
        /// </summary>
        [Test]
        public void TheViewsStripStandsUnderTheOrdersAndAboveTheBar()
        {
            foreach ((int width, int height) in Resolutions)
            foreach (HudContent content in Cases())
            {
                var boxes = HudLayout.Solve(width, height, content);
                HudRect orders = boxes[HudRegion.OrdersStrip];
                HudRect views = boxes[HudRegion.ViewsStrip];
                HudRect bar = boxes[HudRegion.CommandBar];

                Assert.That(views.Right, Is.EqualTo(orders.Right).Within(0.01f), "one gutter");
                Assert.That(views.Y, Is.EqualTo(orders.Bottom + HudLayout.OrdersToViews).Within(0.01f),
                    "the views strip is not directly under the orders");
                Assert.That(views.Height, Is.EqualTo(HudLayout.ViewsHeight).Within(0.01f));
                Assert.That(views.Bottom, Is.LessThanOrEqualTo(bar.Y + 0.01f),
                    $"the views strip ends at {views.Bottom:0.#} and the command bar starts at " +
                    $"{bar.Y:0.#} at {width}x{height} on a {content.Layers}-layer board");
            }
        }

        /// <summary>
        /// Every order has a button, and the strip is as tall as it has orders.
        ///
        /// <para>The point of moving them out of the palette header was that a fifth order costs
        /// one row of <see cref="PaletteTools.Pinned"/> rather than a redesign, so the height is
        /// read off that table here rather than compared against a number in the sheet.</para>
        /// </summary>
        [Test]
        public void TheStripIsAsTallAsThereAreOrders()
        {
            Assert.That(HudLayout.OrdersCount, Is.EqualTo(PaletteTools.Pinned.Length));
            Assert.That(HudLayout.OrdersHeight, Is.EqualTo(
                    HudLayout.Frame + HudLayout.OrdersPadTop +
                    PaletteTools.Pinned.Length * (HudLayout.OrderButton + HudLayout.OrderGap))
                .Within(0.01f));

            Assert.That(HudLayout.OrdersWidth, Is.EqualTo(HudLayout.RailWidth),
                "the strip and the rail share one gutter, so they share one width");
            Assert.That(HudLayout.OrderButton,
                Is.LessThanOrEqualTo(HudLayout.OrdersWidth - 2 * HudLayout.OrdersSidePad),
                "an order button is wider than the gutter it stands in");
        }

        /// <summary>
        /// SK4: the toast stack takes the alerts' column and sits under them, so the things the
        /// colony is telling the player are one column and not two.
        /// </summary>
        [Test]
        public void TheToastStackSitsUnderTheAlertsInTheSameColumn()
        {
            foreach ((int width, int height) in Resolutions)
            {
                var content = new HudContent(Colonists, AllStoreRows, 3, Layers, 2,
                                             toasts: ToastModel.MaxRows);
                var boxes = HudLayout.Solve(width, height, content);

                HudRect alerts = boxes[HudRegion.Alerts];
                HudRect toasts = boxes[HudRegion.Toasts];

                Assert.That(toasts.Empty, Is.False, $"the toast stack is missing at {width}x{height}");
                Assert.That(toasts.X, Is.EqualTo(alerts.X), "the two are not in one column");
                Assert.That(toasts.Width, Is.EqualTo(alerts.Width), "the two are not one width");
                Assert.That(toasts.Y, Is.GreaterThanOrEqualTo(alerts.Bottom),
                    "the toasts are not under the alerts");
            }
        }

        /// <summary>
        /// SK3, after the owner's first look (2026-09-21): the experience bar is a column of the
        /// skill row, between the name and the level, so the row now has a width budget and its
        /// parts have to come to exactly the row's width.
        ///
        /// <para><b>Why this test exists at all.</b> While the bar was absolutely positioned it
        /// cost the row no width and could not squeeze anything. In the flow it can: widen the bar
        /// or its margins and the only flexible part, the name, silently loses the difference until
        /// "Construction" clips. Nothing else would report that — the panel still lays out, no
        /// overlap case moves, and the fast tier has no text engine to notice the truncation.</para>
        ///
        /// <para><b>What this can and cannot prove.</b> It proves the arithmetic closes and that
        /// the name column has not been eaten. It cannot prove "Construction" fits in it, because
        /// that needs a text engine and a font; that question belongs to the Unity tier and
        /// ultimately to the eye. So the name width is also held to a floor: it is 95 px today, and
        /// the day somebody wants the bar wider they have to lower that number deliberately and
        /// look at the result, rather than discover it in a screenshot a week later.</para>
        /// </summary>
        [Test]
        public void TheSkillRowsPartsFitTheRow()
        {
            int fixedParts = HudLayout.SkillIconWidth + HudLayout.SkillIconGap
                           + HudLayout.SkillBarGap + HudLayout.SkillBarWidth + HudLayout.SkillBarGap
                           + HudLayout.SkillLevelWidth
                           + HudLayout.SkillPassionGap + HudLayout.SkillPassionWidth;

            Assert.That(fixedParts + HudLayout.SkillNameWidth,
                Is.EqualTo(HudLayout.SkillRowWidth),
                "the skill row's parts do not come to the row's width");

            Assert.That(HudLayout.SkillNameWidth, Is.GreaterThanOrEqualTo(95),
                "the name column has been squeezed; the longest label is \"Construction\" and "
                + "lowering this is a decision to be looked at, not a side effect of widening the bar");

            // The bar must stay shorter than the row. This is the constraint the absolutely
            // positioned version was protecting and the only one that survived the move into the
            // flow: the colonist pane is one fixed height across every tab, so a bar taller than
            // the row would grow all seven rows and move the pane's top edge on a change of tab.
            Assert.That(HudLayout.SkillBarHeight, Is.LessThan(HudLayout.SkillRow),
                "a bar at least as tall as its row grows the row, and the pane with it");
        }

        /// <summary>
        /// SK4 against EV: the toast stack is the LAST thing in that column, under the Events
        /// panel, and that order is the decision rather than an accident of who was written first.
        /// A toast arrives every couple of minutes and leaves six seconds later; anything below it
        /// in a stacked column would step down and back up each time it did. Nothing is below it.
        ///
        /// <para>Both features were written on branches that did not know about each other and
        /// both said "under the alerts", so on merging they solved to the same top and drew over
        /// one another. The overlap sweep in <see cref="Cases"/> catches that; this says why the
        /// resolution went this way round rather than the other.</para>
        /// </summary>
        [Test]
        public void TheToastStackIsTheLastThingInTheAlertsColumn()
        {
            foreach ((int width, int height) in Resolutions)
            {
                var content = new HudContent(Colonists, AllStoreRows, 3, Layers, 2,
                                             bulletins: BulletinModel.MaxRows,
                                             toasts: ToastModel.MaxRows);
                var boxes = HudLayout.Solve(width, height, content);

                HudRect alerts = boxes[HudRegion.Alerts];
                HudRect bulletins = boxes[HudRegion.Bulletins];
                HudRect toasts = boxes[HudRegion.Toasts];

                Assert.That(bulletins.Empty, Is.False, $"the Events panel is missing at {width}x{height}");
                Assert.That(toasts.Empty, Is.False, $"the toast stack is missing at {width}x{height}");

                Assert.That(bulletins.Y, Is.GreaterThanOrEqualTo(alerts.Bottom),
                    "the Events panel is not under the alerts");
                Assert.That(toasts.Y, Is.GreaterThanOrEqualTo(bulletins.Bottom),
                    "the toast stack is not under the Events panel");
                Assert.That(toasts.X, Is.EqualTo(alerts.X), "the three are not in one column");
                Assert.That(bulletins.X, Is.EqualTo(alerts.X), "the three are not in one column");
            }
        }

        /// <summary>
        /// With no alerts the stack rises to where they would have been rather than leaving their
        /// gap behind it — a toast on an otherwise quiet screen should not float in the middle of
        /// the gutter.
        /// </summary>
        [Test]
        public void TheToastStackClosesUpWhenThereAreNoAlerts()
        {
            var boxes = HudLayout.Solve(1920, 1080,
                new HudContent(Colonists, AllStoreRows, 0, Layers, 0, toasts: 1));

            HudRect clock = boxes[HudRegion.Clock];
            HudRect toasts = boxes[HudRegion.Toasts];

            Assert.That(boxes[HudRegion.Alerts].Empty, Is.True, "this case has no alerts");
            Assert.That(toasts.Y, Is.EqualTo(clock.Bottom + HudLayout.Gap),
                "with nothing above it the stack should sit straight under the clock");
        }

        /// <summary>
        /// A toast costs the coverage budget nothing, because the budget is measured on a resting
        /// screen and a toast lasts six seconds. This is what makes the stack affordable at all —
        /// the ceiling is the owner's to reverse and this work does not spend any of it.
        /// </summary>
        [Test]
        public void TheToastStackDoesNotSpendTheCoverageBudget()
        {
            foreach ((int width, int height) in Resolutions)
            {
                var resting = HudContent.NothingSelected(Colonists, 3, Layers);
                var boxes = HudLayout.Solve(width, height, resting);

                Assert.That(boxes[HudRegion.Toasts].Empty, Is.True,
                    "a resting screen is showing a toast, so the coverage figure includes one");
                Assert.That(HudLayout.Coverage(boxes, width, height),
                    Is.LessThanOrEqualTo(HudLayout.CoverageCeiling));
            }
        }

        /// <summary>
        /// The stack has no header block, unlike the alerts panel: one row of toast is one row tall
        /// plus its frame, and a heading would be the tallest thing in it for most of its life.
        /// </summary>
        [Test]
        public void AToastRowIsARowAndNotAPanelWithAHeading()
        {
            Assert.That(HudLayout.ToastsHeight(0), Is.Zero, "no toasts is no box at all");

            float one = HudLayout.ToastsHeight(1);
            float two = HudLayout.ToastsHeight(2);

            Assert.That(two - one, Is.EqualTo(HudLayout.AlertHeight + HudLayout.AlertGap),
                "a second row should cost exactly one row and one gap");
            Assert.That(one, Is.LessThan(HudLayout.AlertsHeight(1)),
                "a toast row is drawing the alerts panel's heading block");
        }

        static IEnumerable<HudContent> Cases()
        {
            yield return HudContent.NothingSelected(Colonists, 3, Layers);          // resting
            yield return new HudContent(Colonists, AllStoreRows, 0, Layers, 2);     // colonist selected
            yield return new HudContent(Colonists, AllStoreRows, 3, Layers, 2);     // and in trouble
            yield return new HudContent(Colonists, AllStoreRows, 3, Layers, 2, bulletins: 4); // and eventful
            yield return new HudContent(Colonists, AllStoreRows, 0, Layers, 0, bulletins: BulletinModel.MaxRows); // events, no alerts
            yield return new HudContent(0, 0, 0, Layers, 0);                        // nobody left
            yield return new HudContent(8, AllStoreRows, 1, 32, 4);                 // a deeper, fuller game

            // The Skills tab, which is the tallest body the pane has: seven rows of the design's
            // thirteen skills in two columns, against the needs tab's two.
            yield return new HudContent(Colonists, AllStoreRows, 0, Layers, needRows: 0,
                                        skillRows: SkillCatalogue.Rows);
            yield return new HudContent(8, AllStoreRows, 3, 32, needRows: 0,
                                        skillRows: SkillCatalogue.Rows);

            // A tile readout: five facts is the fullest the meadow offers (order, walk, floor,
            // support — one of order/minable), and the pane it stands in is the narrow one.
            yield return new HudContent(Colonists, AllStoreRows, 0, Layers, 0, cellRows: 5);

            // Toasts (SK4). The stack sits under the alerts in the same column, so the case that
            // matters is a full stack UNDER a full alerts panel — the tallest that column can get,
            // and the one that would run off the bottom of a 720p screen if it were going to.
            yield return new HudContent(Colonists, AllStoreRows, 0, Layers, 0,
                                        toasts: ToastModel.MaxRows);
            yield return new HudContent(Colonists, AllStoreRows, 3, Layers, 2,
                                        toasts: ToastModel.MaxRows);
            yield return new HudContent(8, AllStoreRows, 3, 32, needRows: 0,
                                        skillRows: SkillCatalogue.Rows, toasts: ToastModel.MaxRows);

            // The right-hand column at its very tallest: clock, a full alerts panel, a full Events
            // panel and a full toast stack, all at once. SK4 and EV were written on branches that
            // did not know about each other and both placed their panel "under the alerts", so
            // both solved to the same top and the toast stack drew over the Events panel. This is
            // the case that would have caught it.
            yield return new HudContent(Colonists, AllStoreRows, 3, Layers, 2,
                                        bulletins: BulletinModel.MaxRows, toasts: ToastModel.MaxRows);
            yield return new HudContent(8, AllStoreRows, 3, 32, needRows: 0,
                                        skillRows: SkillCatalogue.Rows,
                                        bulletins: BulletinModel.MaxRows, toasts: ToastModel.MaxRows);
        }

        /// <summary>
        /// The colonist pane is one size whatever tab is showing (owner, 2026-09-18: "it resizes
        /// every time … it needs to be at least a fixed size").
        ///
        /// <para>The pane is docked to its bottom edge and grows upward, so a body sized to its
        /// own tab moved the header, the tab strip and every row under the pointer on each change
        /// of tab — Needs' two rows of 25 against Skills' seven of 19, ninety-eight pixels apart.
        /// Both the height and the box the solver hands out are asserted, because the fix is only
        /// worth anything if the pane's <i>top</i> edge is what stays put.</para>
        /// </summary>
        [Test]
        public void TheColonistPaneIsTheSameHeightOnEveryTab()
        {
            float needs = HudLayout.InspectHeight(HudLayout.InspectNeedRows, 0);
            float skills = HudLayout.InspectHeight(0, SkillCatalogue.Rows);

            Assert.That(needs, Is.EqualTo(skills),
                "switching tab must not resize the pane");

            HudRect onNeeds = HudLayout.Solve(1920, 1080,
                new HudContent(Colonists, AllStoreRows, 0, Layers,
                               needRows: HudLayout.InspectNeedRows))[HudRegion.Inspect];
            HudRect onSkills = HudLayout.Solve(1920, 1080,
                new HudContent(Colonists, AllStoreRows, 0, Layers, needRows: 0,
                               skillRows: SkillCatalogue.Rows))[HudRegion.Inspect];

            Assert.That(onSkills.Y, Is.EqualTo(onNeeds.Y).Within(0.01f),
                "the top edge is what the player watches jump");
            Assert.That(onSkills.Height, Is.EqualTo(onNeeds.Height).Within(0.01f));
            Assert.That(onSkills.Width, Is.EqualTo(onNeeds.Width));
        }

        /// <summary>
        /// The fixed body is tall enough for every tab that has content, and is the taller of the
        /// two rather than a number somebody typed. A fourteenth skill or a fourth need moves it;
        /// clipping is what the acceptance criteria forbid.
        /// </summary>
        [Test]
        public void TheFixedBodyIsTheTallestLiveTab()
        {
            int needs = HudLayout.InspectNeedRows * HudLayout.NeedRow +
                        (HudLayout.InspectNeedRows - 1) * HudLayout.NeedRowGap;
            int skills = SkillCatalogue.Rows * HudLayout.SkillRow +
                         (SkillCatalogue.Rows - 1) * HudLayout.SkillRowGap;

            Assert.That(HudLayout.InspectTabBody, Is.GreaterThanOrEqualTo(needs),
                "the needs grid would clip");
            Assert.That(HudLayout.InspectTabBody, Is.GreaterThanOrEqualTo(skills),
                "the skills grid would clip");
            Assert.That(HudLayout.InspectTabBody, Is.EqualTo(System.Math.Max(needs, skills)),
                "no more slack than the tallest tab needs");
        }

        /// <summary>
        /// The shape the owner asked for on 2026-09-17: the tile readout stands in half the
        /// colonist pane's width, and its rows stand the pane up around three times the height
        /// of the header alone — a column of facts, not a band with a sentence in it.
        /// </summary>
        [Test]
        public void TheTileReadoutIsHalfAsWideAndRowsMakeItTall()
        {
            var boxes = HudLayout.Solve(1920, 1080,
                new HudContent(Colonists, AllStoreRows, 0, Layers, 0, cellRows: 5));
            HudRect pane = boxes[HudRegion.Inspect];

            Assert.That(pane.Width, Is.EqualTo(HudLayout.InspectNarrowWidth));
            Assert.That(HudLayout.InspectNarrowWidth * 2, Is.EqualTo(HudLayout.InspectWidth),
                "narrow is half, by definition, and the definition is load-bearing");

            // The narrow header: a tile's slot is an icon and stayed at 38 when the colonist's
            // became a 60 px portrait (2026-09-18).
            float chrome = HudLayout.Frame + HudLayout.Pad + HudLayout.InspectHeaderNarrow +
                           HudLayout.InspectHeaderGap + HudLayout.Pad;
            // Six rows for five facts: the shell prepends a location row to the readout
            // (owner, 2026-09-20 — coordinates moved out of the header's meta line).
            Assert.That(HudLayout.InspectHeight(0, 0, cellRows: 5),
                Is.EqualTo(chrome + 6 * HudLayout.CellRow + 5 * HudLayout.CellRowGap));
            Assert.That(HudLayout.InspectHeight(0, 0, cellRows: 5) / chrome, Is.GreaterThan(2.5f),
                "five facts stand the pane up around three times its header alone");
        }
    }

    /// <summary>
    /// The command bar's own criteria: every item shows a hotkey, nothing runs off the edge, and
    /// what does not fit is reachable in Menu rather than lost.
    /// </summary>
    public class HudCommandTests
    {
        [Test]
        public void EveryItemShowsAHotkeyAndCarriesARegisteredName()
        {
            foreach (HudCommand command in HudCommands.All)
            {
                Assert.That(command.Hotkey, Is.Not.Empty, $"{command.Key} has no hotkey cap");
                Assert.That(Registry.Labels, Does.ContainKey(command.Key),
                    $"{command.Key} is not in the naming registry");
                Assert.That(command.Label, Is.EqualTo(Registry.Label(command.Key)),
                    "a command-bar label is the registry's word, never one invented in C#");
            }
        }

        // Clashes with the keys the game already reads are HotkeyClashTests', which scans the
        // source for them rather than comparing against a list written from memory. The list this
        // test used to hold was missing five of them and passed anyway.

        [Test]
        public void TheBarNeverRunsPastTheEdgeAtAnyWidth()
        {
            List<float> widths = HudCommands.ModelWidths();

            for (int screen = 480; screen <= 3840; screen += 17)
            {
                float available = screen - 2 * HudLayout.Edge;
                int shown = HudCommands.Fit(widths, available);
                float bar = HudCommands.BarWidth(widths, shown);

                Assert.That(bar, Is.LessThanOrEqualTo(available + 0.01f),
                    $"at {screen} px wide the bar comes out {bar:0.#} px against {available:0.#} of room");
                Assert.That(shown, Is.InRange(0, widths.Count - 1));
            }
        }

        [Test]
        public void MenuIsNeverTheItemThatGetsDropped()
        {
            // Menu is where everything that did not fit goes, so a bar narrow enough to drop it
            // would strand every other item with it.
            List<float> widths = HudCommands.ModelWidths();
            Assert.That(HudCommands.Fit(widths, 1f), Is.Zero,
                "at no width at all the bar shows Menu and nothing else, rather than nothing");
            Assert.That(HudCommands.BarWidth(widths, 0), Is.GreaterThan(0f),
                "the bar with everything in Menu still has Menu in it");
        }

        [Test]
        public void TheWholeBarFitsOnACommonDesktop()
        {
            List<float> widths = HudCommands.ModelWidths();
            int shown = HudCommands.Fit(widths, 1920 - 2 * HudLayout.Edge);

            Assert.That(shown, Is.EqualTo(widths.Count - 1),
                $"only {shown} of {widths.Count - 1} command items fit at 1920 px wide, so the " +
                "bar is overflowing into Menu on the resolution it is designed for. The modelled " +
                "widths are deliberately generous, so this is a real squeeze rather than an " +
                "estimate being pessimistic.");
        }

        [Test]
        public void ExactlyOneItemIsPrimary()
        {
            int primaries = HudCommands.All.Count(c => c.Primary);
            Assert.That(primaries, Is.EqualTo(1), "the bar has one filled item: Build");
            Assert.That(HudCommands.All.Single(c => c.Primary).Key, Is.EqualTo(HudCommands.BuildKey));
        }
    }

    /// <summary>
    /// The type scale and the palette: six steps and no others, numbers always mono, and body text
    /// legible over the brightest thing the world can draw.
    /// </summary>
    public class HudTypeTests
    {
        [Test]
        public void TheScaleIsTheSixStepsAndTheHotkeyLegend()
        {
            var sizes = HudType.Sizes.OrderBy(s => s).ToArray();

            // Eleven appears twice — the panel label and the hotkey cap — so six roles and the
            // legend come to six distinct sizes.
            Assert.That(sizes, Is.EqualTo(new[] { 11, 12, 13, 14, 19, 24 }),
                "the interface may use these sizes and no others. A seventh is a design decision " +
                "and belongs in the specification before it belongs in the code.");
        }

        [Test]
        public void TheClockIsMonoAndSoIsAnythingAskedForAsANumber()
        {
            Assert.That(HudType.Of(HudTextRole.Clock).Mono, Is.True, "the clock is always mono");
            Assert.That(HudType.Of(HudTextRole.Hotkey).Mono, Is.True, "a hotkey cap is always mono");

            foreach (HudTextRole role in HudType.Roles)
            {
                HudTextStyle numeric = HudType.Of(role, numeric: true);
                Assert.That(numeric.Mono, Is.True, $"{role} as a number is not set in the mono face");
                Assert.That(numeric.Weight, Is.EqualTo(HudType.MonoWeight),
                    $"{role} as a number is not at the weight every figure is set in");
                Assert.That(numeric.Size, Is.EqualTo(HudType.Of(role).Size),
                    "asking for a number changes the family, never the step");
            }
        }

        [Test]
        public void OnlyTheTwoHeadingRolesAreTrackedAndUpperCased()
        {
            // Capitals and tracking travel together and mark a heading. Two roles carry
            // them: the label that titles a panel from outside its content, and the heading
            // of a group inside a list. Nothing else may, because a third would stop the
            // pair meaning "this is a heading" and start meaning "this is emphasis".
            foreach (HudTextRole role in HudType.Roles)
            {
                HudTextStyle style = HudType.Of(role);
                bool heading = role == HudTextRole.PanelLabel || role == HudTextRole.ListHeading;
                Assert.That(style.Uppercase, Is.EqualTo(heading), $"{role} upper-casing");
                Assert.That(style.LetterSpacing > 0f, Is.EqualTo(heading), $"{role} tracking");
            }

            // And the two are distinguishable: a heading inside a list out-ranks the body
            // rows it opens onto, which is the whole reason it is not the 11px panel label.
            Assert.That(HudType.Of(HudTextRole.ListHeading).Size,
                Is.GreaterThan(HudType.Of(HudTextRole.Body).Size),
                "a list heading that does not out-rank its own rows is not a heading");
            Assert.That(HudType.Of(HudTextRole.ListHeading).Size,
                Is.GreaterThan(HudType.Of(HudTextRole.PanelLabel).Size),
                "a list heading sits above the panel label, not at it");
        }

        [Test]
        public void BodyTextIsLegibleOverTheBrightestTerrainInTheGame()
        {
            // White is used as the terrain rather than a sampled colour: every terrain in the game
            // is darker than white, so a ratio that holds here holds against all of them and the
            // test cannot go stale when a new biome lands.
            (string name, HudColour ink)[] body =
            {
                ("primary", HudTheme.TextPrimary),
                ("meta", HudTheme.TextMeta),
                ("dim", HudTheme.TextDim),
            };

            foreach ((string name, HudColour ink) in body)
            {
                double ratio = HudContrast.OverBrightestTerrain(ink, HudTheme.PanelFill);
                TestContext.WriteLine($"{name} over a panel over white: {ratio:0.00}:1");
                Assert.That(ratio, Is.GreaterThanOrEqualTo(HudContrast.BodyMinimum),
                    $"{name} ink reads at {ratio:0.00}:1 against a panel laid over white terrain");
            }
        }

        [Test]
        public void TheAccentCarriesItsOwnInk()
        {
            // The one place the palette puts text on a filled block: the primary command and the
            // active speed button. The dark ink has to survive it.
            double ratio = HudContrast.Ratio(HudTheme.OnAccent, HudTheme.Accent);
            TestContext.WriteLine($"on-accent ink over accent: {ratio:0.00}:1");
            Assert.That(ratio, Is.GreaterThanOrEqualTo(HudContrast.BodyMinimum));
        }

        [Test]
        public void EveryCategoryColourIsAPaletteToken()
        {
            var palette = new[]
            {
                HudTheme.Accent.Hex, HudTheme.Warn.Hex, HudTheme.Bad.Hex, HudTheme.Good.Hex,
                HudTheme.Info.Hex, HudTheme.TextMeta.Hex,
            };

            foreach (HudCategory category in System.Enum.GetValues(typeof(HudCategory)))
                Assert.That(palette, Has.Member(HudTheme.ColourOf(category).Hex),
                    $"{category} resolves to a colour that is not in the palette. A screen with a " +
                    "hue per commodity is a screen with no colour code at all.");
        }

        [Test]
        public void EveryCategorisedKeyIsARegisteredName()
        {
            foreach (string key in HudTheme.CategorisedKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key),
                    $"{key} carries a category colour but is not a name the registry knows");
        }

        [Test]
        public void EveryAlertKeyIsARegisteredName()
        {
            foreach (string key in AlertModel.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
        }
    }
}
