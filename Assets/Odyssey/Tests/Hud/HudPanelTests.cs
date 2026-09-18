#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The alerts panel. It is hidden outright when it has nothing to say, which is only safe if
    /// something can reliably put a line in it and take the line away again.
    /// </summary>
    public class AlertModelTests
    {
        static PawnView Colonist(int id, int food = 800, int mood = 800, int job = 0) =>
            new PawnView(new PawnId(id), new CellRef(1, 1, 1), food, rest: 800, mood: mood, jobDef: job);

        [Test]
        public void AQuietColonyRaisesNothing()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(Colonist(1));
            snapshot.AddPawn(Colonist(2));

            var alerts = new AlertModel();
            alerts.Refresh(snapshot, 0.0);

            Assert.That(alerts.Rows, Is.Empty,
                "an empty alerts panel is hidden, so anything it prints at rest is something the " +
                "player has to learn to ignore");
        }

        [Test]
        public void StarvationLeadsWithTheActionableClause()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(Colonist(1, food: AlertModel.StarveAt - 1));
            snapshot.AddPawn(Colonist(2));

            var alerts = new AlertModel();
            alerts.Refresh(snapshot, 0.0);

            Assert.That(alerts.Rows.Count, Is.EqualTo(1));
            Assert.That(alerts.Rows[0].Key, Is.EqualTo(AlertModel.StarveKey));
            Assert.That(alerts.Rows[0].Severity, Is.EqualTo(AlertSeverity.Danger));
            Assert.That(alerts.Rows[0].TargetName, Is.EqualTo(ColonistNames.Of(snapshot, new PawnId(1))));
            Assert.That(alerts.Rows[0].Lead, Is.EqualTo(ColonistNames.Of(snapshot, new PawnId(1)) + " is starving"));
            Assert.That(alerts.Rows[0].Pawn, Is.EqualTo(new PawnId(1)));
            Assert.That(alerts.Rows[0].Count, Is.EqualTo(1));
        }

        [Test]
        public void SeveralSubjectsBecomeIndividualRowsWithTargets()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(Colonist(1, food: 10));
            snapshot.AddPawn(Colonist(2, food: 10));
            snapshot.AddPawn(Colonist(3, food: 10));

            var alerts = new AlertModel();
            alerts.Refresh(snapshot, 0.0);

            Assert.That(alerts.Rows.Count, Is.EqualTo(3), "three starving colonists become three alert rows");
            Assert.That(alerts.Rows[0].Lead, Is.EqualTo(ColonistNames.Of(snapshot, new PawnId(1)) + " is starving"));
            Assert.That(alerts.Rows[0].Pawn, Is.EqualTo(new PawnId(1)));
            Assert.That(alerts.Rows[1].Lead, Is.EqualTo(ColonistNames.Of(snapshot, new PawnId(2)) + " is starving"));
            Assert.That(alerts.Rows[1].Pawn, Is.EqualTo(new PawnId(2)));
            Assert.That(alerts.Rows[2].Lead, Is.EqualTo(ColonistNames.Of(snapshot, new PawnId(3)) + " is starving"));
            Assert.That(alerts.Rows[2].Pawn, Is.EqualTo(new PawnId(3)));
        }

        [Test]
        public void DismissingAnAlertSuppressesItUntilConditionClearsAndReoccurs()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(Colonist(1, food: 10));

            var alerts = new AlertModel();
            alerts.Refresh(snapshot, 0.0);
            Assert.That(alerts.Rows.Count, Is.EqualTo(1));

            int dismissKey = alerts.Rows[0].DismissKey;
            alerts.Dismiss(dismissKey);
            Assert.That(alerts.Rows, Is.Empty);

            // Stays suppressed on next refresh while still starving
            alerts.Refresh(snapshot, 1.0);
            Assert.That(alerts.Rows, Is.Empty);

            // Colonist is fed past threshold
            var fed = Frame.Write();
            fed.AddPawn(Colonist(1, food: AlertModel.StarveClearAt));
            alerts.Refresh(fed, 2.0);
            Assert.That(alerts.Rows, Is.Empty);

            // Later starves again -> alert re-appears!
            var starvingAgain = Frame.Write();
            starvingAgain.AddPawn(Colonist(1, food: 10));
            alerts.Refresh(starvingAgain, 3.0);
            Assert.That(alerts.Rows.Count, Is.EqualTo(1));
            Assert.That(alerts.Rows[0].Lead, Is.EqualTo(ColonistNames.Of(starvingAgain, new PawnId(1)) + " is starving"));
        }

        [Test]
        public void DismissAllClearsEveryActiveAlert()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(Colonist(1, food: 10, mood: 100));

            var alerts = new AlertModel();
            alerts.Refresh(snapshot, 0.0);
            Assert.That(alerts.Rows.Count, Is.EqualTo(2));

            alerts.DismissAll();
            Assert.That(alerts.Rows, Is.Empty);

            // Stays empty on subsequent refresh
            alerts.Refresh(snapshot, 1.0);
            Assert.That(alerts.Rows, Is.Empty);
        }

        /// <summary>
        /// The hysteresis, which is the whole reason this is a class rather than a predicate. A
        /// need sitting on its threshold flaps either side of it for hours of game time, and an
        /// alerts panel that appears and disappears with the flapping is worse than no panel.
        /// </summary>
        [Test]
        public void AnAlertStandsUntilTheNeedHasRecoveredPastAWiderBand()
        {
            var alerts = new AlertModel();

            var falling = Frame.Write();
            falling.AddPawn(Colonist(1, food: AlertModel.StarveAt));
            alerts.Refresh(falling, 0.0);
            Assert.That(alerts.Rows, Has.Count.EqualTo(1));

            // One mouthful past the threshold: still starving, because clearing here is what makes
            // the panel blink.
            var nibbled = Frame.Write();
            nibbled.AddPawn(Colonist(1, food: AlertModel.StarveAt + 1));
            alerts.Refresh(nibbled, 1.0);
            Assert.That(alerts.Rows, Has.Count.EqualTo(1), "the alert cleared inside its own re-arm band");

            var fed = Frame.Write();
            fed.AddPawn(Colonist(1, food: AlertModel.StarveClearAt));
            alerts.Refresh(fed, 2.0);
            Assert.That(alerts.Rows, Is.Empty, "a fed colonist should clear the alert");
        }

        [Test]
        public void AnIdleColonyHasToStayIdleBeforeItIsWorthSaying()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(Colonist(1, job: -1));
            snapshot.AddPawn(Colonist(2, job: -1));

            var alerts = new AlertModel();
            alerts.Refresh(snapshot, 0.0);
            Assert.That(alerts.Rows, Is.Empty, "a moment between jobs is not an alert");

            alerts.Refresh(snapshot, AlertModel.IdleSustain - 0.01);
            Assert.That(alerts.Rows, Is.Empty);

            alerts.Refresh(snapshot, AlertModel.IdleSustain);
            Assert.That(alerts.Rows, Has.Count.EqualTo(1));
            Assert.That(alerts.Rows[0].Key, Is.EqualTo(AlertModel.IdleKey));

            // And one colonist picking up a job clears it, because the alert is about the colony.
            var working = Frame.Write();
            working.AddPawn(Colonist(1, job: 0));
            working.AddPawn(Colonist(2, job: -1));
            alerts.Refresh(working, AlertModel.IdleSustain + 1.0);
            Assert.That(alerts.Rows, Is.Empty);
        }

        [Test]
        public void TheWorstLineIsFirst()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(Colonist(1, food: 10, mood: 100, job: -1));

            var alerts = new AlertModel();
            alerts.Refresh(snapshot, 0.0);
            alerts.Refresh(snapshot, AlertModel.IdleSustain + 1.0);

            Assert.That(alerts.Rows, Has.Count.EqualTo(3));
            Assert.That(alerts.Rows[0].Severity, Is.EqualTo(AlertSeverity.Danger));
            Assert.That(alerts.Rows[1].Severity, Is.EqualTo(AlertSeverity.Warning));
            Assert.That(alerts.Rows[2].Severity, Is.EqualTo(AlertSeverity.Notice));
        }

        /// <summary>
        /// The panel refreshes four times a second and every line it holds is an interpolated
        /// string, so the model hands back the same instance while nothing has changed. The view
        /// compares by reference to decide whether to rebuild its labels, so this is a contract
        /// rather than an optimisation.
        /// </summary>
        [Test]
        public void AStandingAlertKeepsTheSameStringInstance()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(Colonist(1, food: 10));

            var alerts = new AlertModel();
            alerts.Refresh(snapshot, 0.0);
            string first = alerts.Rows[0].Lead;

            alerts.Refresh(snapshot, 0.25);
            alerts.Refresh(snapshot, 0.5);

            Assert.That(ReferenceEquals(alerts.Rows[0].Lead, first), Is.True,
                "the lead was rebuilt although nothing about the alert changed, which allocates " +
                "once a refresh for as long as the alert stands (ADR 0003 flip condition F1)");
        }
    }

    /// <summary>
    /// The stores panel's falling-stock mark: an amber chevron and a row tint on anything that is
    /// draining. Measured against a baseline resampled every ten seconds rather than against the
    /// previous refresh, because a hauler picking a stack up momentarily lowers every count on
    /// screen and frame-to-frame comparison would make the whole panel flicker amber all day.
    /// </summary>
    public class LedgerTrendTests
    {
        static WorldSnapshot WithMeals(int meals)
        {
            var snapshot = Frame.Write();
            if (meals > 0)
                snapshot.AddThing(new ThingView(new ThingId(1), new CellRef(1, 1, 1), ItemHandle.Meal, 0, meals));
            return snapshot;
        }

        static LedgerRow Meals(LedgerModel ledger) => ledger.Rows.Find(r => r.IconKey == "ui.res.meal");

        [Test]
        public void NothingIsFallingOnTheFirstLook()
        {
            var ledger = new LedgerModel();
            ledger.Refresh(WithMeals(100), 0.0);
            Assert.That(Meals(ledger).Trend, Is.EqualTo(StockTrend.Steady));
        }

        [Test]
        public void AStockDrainingPastTheThresholdIsMarkedFalling()
        {
            var ledger = new LedgerModel();
            ledger.Refresh(WithMeals(100), 0.0);          // baseline taken at 100
            ledger.Refresh(WithMeals(80), 1.0);           // a fifth gone, well past a tenth

            Assert.That(Meals(ledger).Trend, Is.EqualTo(StockTrend.Falling));
        }

        [Test]
        public void AMomentaryDipIsNotATrend()
        {
            var ledger = new LedgerModel();
            ledger.Refresh(WithMeals(100), 0.0);
            ledger.Refresh(WithMeals(95), 0.25);          // one stack in a hauler's hands

            Assert.That(Meals(ledger).Trend, Is.EqualTo(StockTrend.Steady),
                "five per cent is a hauler mid-carry, not a colony running out of food");
        }

        [Test]
        public void TheBaselineMovesOnSoASteadyDeclineStopsBeingNews()
        {
            var ledger = new LedgerModel();
            ledger.Refresh(WithMeals(100), 0.0);
            ledger.Refresh(WithMeals(50), 1.0);
            Assert.That(Meals(ledger).Trend, Is.EqualTo(StockTrend.Falling));

            // Past the resample window with the stock now holding steady at its lower level.
            ledger.Refresh(WithMeals(50), LedgerModel.BaselineSeconds + 1.0);
            ledger.Refresh(WithMeals(50), LedgerModel.BaselineSeconds + 2.0);
            Assert.That(Meals(ledger).Trend, Is.EqualTo(StockTrend.Steady));
        }

        [Test]
        public void TheHeaderCountsWhatIsStockedAgainstWhatExists()
        {
            var ledger = new LedgerModel();
            ledger.Refresh(WithMeals(100));

            Assert.That(ledger.Stocked, Is.EqualTo(1), "one commodity has anything in it");
            Assert.That(ledger.Total, Is.GreaterThan(ledger.Stocked),
                "the rest are the economy to come, folded away behind the disclosure");
            Assert.That(Meals(ledger).Zero, Is.False);
            Assert.That(ledger.Rows.Find(r => r.IconKey == "ui.res.medkit").Zero, Is.True);
        }

        [Test]
        public void WithoutAClockNothingIsEverReportedAsFalling()
        {
            // The overload without a clock exists for callers and tests that only want the counts.
            // A trend inferred from "how many times has Refresh been called" would be a different
            // measurement wearing the same name.
            var ledger = new LedgerModel();
            ledger.Refresh(WithMeals(100));
            ledger.Refresh(WithMeals(10));
            Assert.That(Meals(ledger).Trend, Is.EqualTo(StockTrend.Steady));
        }
    }

    /// <summary>
    /// The interface scale: the setting the owner asked for on 2026-09-16, when the rebuilt HUD's
    /// type read too small on a 4K monitor.
    /// </summary>
    public class UiScaleTests
    {
        [Test]
        public void ALargerScaleMeansASmallerCanvasAndSoBiggerType()
        {
            (int wide, int tall) = HudLayout.ReferenceFor(100);
            Assert.That(wide, Is.EqualTo(HudLayout.ReferenceWidth), "100% is the canvas as authored");
            Assert.That(tall, Is.EqualTo(HudLayout.ReferenceHeight));

            (int bigger, _) = HudLayout.ReferenceFor(125);
            Assert.That(bigger, Is.EqualTo(1536),
                "at 125% the panel is told a smaller reference, so everything drawn against it " +
                "comes out an eighth larger");

            (int smaller, _) = HudLayout.ReferenceFor(80);
            Assert.That(smaller, Is.GreaterThan(HudLayout.ReferenceWidth));
        }

        [Test]
        public void TheLayoutStillHoldsAtEveryRungOfTheLadder()
        {
            // Turning the scale up shrinks the canvas, which is the same thing as running at a
            // lower resolution — so every rung has to survive the overlap check, not just the
            // default. 150% on a 16:9 screen is a 1280x720 canvas, which is why that size is in
            // HudLayoutTests as well.
            foreach (int percent in SettingsDirector.UiScales)
            {
                (int width, int height) = HudLayout.ReferenceFor(percent);
                var content = new HudContent(colonists: 3, storeRows: 6, alerts: 2, layers: 16, needRows: 2);
                var boxes = HudLayout.Solve(width, height, content);

                Assert.That(HudLayout.FirstOverlap(boxes), Is.Null,
                    $"two panels overlap at {percent}% (a {width}x{height} canvas)");
                TestContext.WriteLine(
                    $"{percent}%: {width}x{height}, " +
                    $"{HudLayout.Coverage(HudLayout.Solve(width, height, HudContent.NothingSelected(3, 3, 16)), width, height):P1} covered");
            }
        }

        [Test]
        public void TheDefaultIsBiggerOnABiggerScreen()
        {
            Assert.That(SettingsDirector.DefaultScaleFor(1080), Is.EqualTo(100),
                "the interface is authored in 1080p pixels, so 1080p is 100% by construction");
            Assert.That(SettingsDirector.DefaultScaleFor(1440), Is.GreaterThan(100));
            Assert.That(SettingsDirector.DefaultScaleFor(2160),
                Is.GreaterThanOrEqualTo(SettingsDirector.DefaultScaleFor(1440)),
                "a 4K screen does not start smaller than a 1440p one");

            foreach (int height in new[] { 720, 1080, 1440, 2160, 4320 })
                Assert.That(SettingsDirector.UiScales, Has.Member(SettingsDirector.DefaultScaleFor(height)),
                    $"the default for a {height}px screen is not a rung the panel can show");
        }

        [Test]
        public void TheDefaultAtTheDesignResolutionKeepsTheCoverageCeiling()
        {
            // The ceiling is stated at the size the interface is designed at. A player who turns
            // the scale up is choosing to hide more of the board, and the panel says so; what must
            // not happen is the *default* quietly breaking the criterion on a common monitor.
            foreach (int height in new[] { 1080, 1440, 2160 })
            {
                int percent = SettingsDirector.DefaultScaleFor(height);
                (int width, int canvasHeight) = HudLayout.ReferenceFor(percent);
                var boxes = HudLayout.Solve(width, canvasHeight, HudContent.NothingSelected(3, 3, 16));
                float coverage = HudLayout.Coverage(boxes, width, canvasHeight);

                Assert.That(coverage, Is.LessThanOrEqualTo(HudLayout.CoverageCeiling),
                    $"a {height}px screen starts at {percent}%, where the HUD covers {coverage:P1}");
            }
        }

        [Test]
        public void AnythingOffTheLadderSnapsOntoIt()
        {
            var settings = new SettingsDirector();
            settings.SetUiScale(117);
            Assert.That(SettingsDirector.UiScales, Has.Member(settings.UiScale),
                "a stored preference from an older ladder must not leave the panel showing a " +
                "value none of its own buttons can reproduce");

            settings.SetUiScale(10_000);
            Assert.That(settings.UiScale, Is.EqualTo(150), "the ladder has a top");
            settings.SetUiScale(0);
            Assert.That(settings.UiScale, Is.EqualTo(80), "and a bottom");
        }

        [Test]
        public void SteppingMovesOneRungAndStopsAtTheEnds()
        {
            var settings = new SettingsDirector();
            settings.SetUiScale(100);

            settings.StepUiScale(1);
            Assert.That(settings.UiScale, Is.EqualTo(110));
            settings.StepUiScale(-2);
            Assert.That(settings.UiScale, Is.EqualTo(90));

            for (int i = 0; i < 10; i++) settings.StepUiScale(1);
            Assert.That(settings.UiScale, Is.EqualTo(150));
            for (int i = 0; i < 10; i++) settings.StepUiScale(-1);
            Assert.That(settings.UiScale, Is.EqualTo(80));
        }

        [Test]
        public void TheScreenSeedsItAndTheMachineOverridesTheScreen()
        {
            var settings = new SettingsDirector();
            int raised = 0;
            settings.UiScaleChanged += _ => raised++;

            settings.SeedUiScale(125);
            Assert.That(settings.UiScale, Is.EqualTo(125));
            Assert.That(raised, Is.Zero, "seeding describes the screen; it does not announce a change");

            var store = new FakeSettingsStore();
            store.Preset(SettingsDirector.UiScaleKey, 90);
            settings.UseStore(store);

            Assert.That(settings.UiScale, Is.EqualTo(90),
                "a preference this machine has been told beats what its screen suggests");
            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void ChoosingAScaleIsRemembered()
        {
            var settings = new SettingsDirector();
            var store = new FakeSettingsStore();
            settings.UseStore(store);

            settings.SetUiScale(125);
            Assert.That(store.ReadInt(SettingsDirector.UiScaleKey), Is.EqualTo(125));
        }
    }

    /// <summary>The settings panel's two sections.</summary>
    public class SettingsTabTests
    {
        [Test]
        public void ItOpensOnInterface()
        {
            // The first thing a player wants from a settings panel on a large monitor is to make
            // the type bigger, and it is the one setting here that changes the panel they are
            // looking at while they look at it.
            Assert.That(new SettingsDirector().Tab, Is.EqualTo(SettingsTab.Interface));
        }

        [Test]
        public void ChangingSectionAnnouncesItselfOnceAndOnlyWhenItChanges()
        {
            var settings = new SettingsDirector();
            var seen = new List<SettingsTab>();
            settings.TabChanged += seen.Add;

            settings.SetTab(SettingsTab.Graphics);
            settings.SetTab(SettingsTab.Graphics);
            settings.SetTab(SettingsTab.Interface);

            Assert.That(seen, Is.EqualTo(new[] { SettingsTab.Graphics, SettingsTab.Interface }));
        }
    }

    /// <summary>
    /// Escape unwinds in one order, decided in one place: the tool in the hand first, then the
    /// panel opened over the board, then the menu.
    /// </summary>
    public class EscapeOrderTests
    {
        [Test]
        public void TheToolComesOffFirst()
        {
            var settings = new SettingsDirector();
            settings.SetOpen(true);
            Assert.That(settings.Escape(toolArmed: true, paletteOpen: true),
                Is.EqualTo(EscapeAction.DisarmTool));
        }

        [Test]
        public void ThenTheBuildPalette()
        {
            var settings = new SettingsDirector();
            settings.SetOpen(true);
            Assert.That(settings.Escape(toolArmed: false, paletteOpen: true),
                Is.EqualTo(EscapeAction.ClosePalette));
        }

        [Test]
        public void ThenTheSettingsPanel()
        {
            var settings = new SettingsDirector();
            settings.SetOpen(true);
            Assert.That(settings.Escape(toolArmed: false, paletteOpen: false),
                Is.EqualTo(EscapeAction.ClosePanel));
        }

        [Test]
        public void AndWithNothingOpenItIsTheWayIn()
        {
            var settings = new SettingsDirector();
            Assert.That(settings.Escape(toolArmed: false, paletteOpen: false),
                Is.EqualTo(EscapeAction.OpenPanel));
        }

        /// <summary>
        /// The Menu popover is in the order too (owner, 2026-09-17: every window can be escaped).
        /// It was the one window with no way out but the button that opened it.
        /// </summary>
        [Test]
        public void TheMenuPopoverUnwindsWithTheRest()
        {
            var settings = new SettingsDirector();

            Assert.That(settings.Escape(toolArmed: false, paletteOpen: false, menuOpen: true),
                Is.EqualTo(EscapeAction.CloseMenu));

            Assert.That(settings.Escape(toolArmed: true, paletteOpen: false, menuOpen: true),
                Is.EqualTo(EscapeAction.DisarmTool),
                "a tool in the hand still comes off first");

            settings.SetOpen(true);
            Assert.That(settings.Escape(toolArmed: false, paletteOpen: false, menuOpen: true),
                Is.EqualTo(EscapeAction.CloseMenu),
                "a popover raised a moment ago unwinds before the settings panel under it");
        }

        [Test]
        public void TheOlderOverloadStillMeansWhatItMeant()
        {
            var settings = new SettingsDirector();
            Assert.That(settings.Escape(toolArmed: false), Is.EqualTo(EscapeAction.OpenPanel));
            settings.SetOpen(true);
            Assert.That(settings.Escape(toolArmed: false), Is.EqualTo(EscapeAction.ClosePanel));
            Assert.That(settings.Escape(toolArmed: true), Is.EqualTo(EscapeAction.DisarmTool));
        }
    }
}
