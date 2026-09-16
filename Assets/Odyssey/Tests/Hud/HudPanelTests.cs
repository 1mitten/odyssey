#nullable enable
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

            Assert.That(alerts.Rows, Has.Count.EqualTo(1));
            Assert.That(alerts.Rows[0].Key, Is.EqualTo(AlertModel.StarveKey));
            Assert.That(alerts.Rows[0].Severity, Is.EqualTo(AlertSeverity.Danger));
            Assert.That(alerts.Rows[0].Lead, Is.EqualTo("A colonist is starving"));
            Assert.That(alerts.Rows[0].Detail, Is.Not.Empty, "the trailing detail is what the lead leaves out");
            Assert.That(alerts.Rows[0].Count, Is.EqualTo(1));
        }

        [Test]
        public void SeveralSubjectsBecomeOneLineWithACount()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(Colonist(1, food: 10));
            snapshot.AddPawn(Colonist(2, food: 10));
            snapshot.AddPawn(Colonist(3, food: 10));

            var alerts = new AlertModel();
            alerts.Refresh(snapshot, 0.0);

            Assert.That(alerts.Rows, Has.Count.EqualTo(1), "three starving colonists are one alert");
            Assert.That(alerts.Rows[0].Lead, Is.EqualTo("3 colonists are starving"));
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
            Assert.That(ledger.Rows.Find(r => r.IconKey == "ui.res.water").Zero, Is.True);
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
