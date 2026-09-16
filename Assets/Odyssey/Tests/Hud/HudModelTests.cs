#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// A frame writer for tests, standing in for the simulation's publish phase. The models
    /// under test read <see cref="WorldSnapshot"/> and nothing else, so the fixture is exactly
    /// the contract: pawns, things and one slice of cells.
    /// </summary>
    static class Frame
    {
        public static WorldSnapshot Write(int layers = 4, int sliceLayer = 1, int tick = 0)
        {
            var size = new GridSize(10, 10, layers);
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(tick, size, sliceLayer);
            return snapshot;
        }
    }

    public class GameClockTests
    {
        [Test]
        public void StartOfTimeIsFirstHourOfFirstDayOfLarkspur()
        {
            Assert.That(GameClock.HourOfDay(0), Is.Zero);
            Assert.That(GameClock.DayOfMonth(0), Is.EqualTo(1));
            Assert.That(GameClock.Describe(0), Is.EqualTo("00h · Day 1 · Larkspur · Wash"));
        }

        [Test]
        public void BoundariesRollOverInOrder()
        {
            long day = GameClock.TicksPerDay;
            Assert.That(GameClock.DayOfMonth(day), Is.EqualTo(2));

            long month = GameClock.TicksPerMonth;
            Assert.That(GameClock.MonthName(month), Is.EqualTo("Tansy"));
            Assert.That(GameClock.DayOfMonth(month), Is.EqualTo(1));
            Assert.That(GameClock.SeasonName(month), Is.EqualTo("Wash"));

            long year = GameClock.TicksPerMonth * 6;
            Assert.That(GameClock.MonthName(year), Is.EqualTo("Larkspur"));
            Assert.That(GameClock.SeasonName(GameClock.TicksPerMonth * 2), Is.EqualTo("Glare"));
        }
    }

    public class ColonistNamesTests
    {
        [Test]
        public void NamesAreStableAndStartWithThePromotedPool()
        {
            Assert.That(ColonistNames.Of(new PawnId(1)), Is.EqualTo("Wrenn"));
            Assert.That(ColonistNames.Of(new PawnId(8)), Is.EqualTo("Nyx"));
            Assert.That(ColonistNames.Of(new PawnId(1)), Is.EqualTo(ColonistNames.Of(new PawnId(1))),
                "the same id must always answer the same name");
        }

        [Test]
        public void PastThePoolTheCycleNumberIsAppendedNotInvented()
        {
            Assert.That(ColonistNames.Of(new PawnId(9)), Is.EqualTo("Wrenn 2"));
            Assert.That(ColonistNames.Of(new PawnId(0)), Is.EqualTo("nobody"));
        }
    }

    public class RosterModelTests
    {
        [Test]
        public void CardsFollowTheFrameAndMarkTheSelection()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(new PawnView(new PawnId(1), new CellRef(1, 1, 2), 400, 900, 80, JobHandle.Haul));
            snapshot.AddPawn(new PawnView(new PawnId(2), new CellRef(2, 2, 0), 950, 100, 30, JobHandle.Sleep));

            var roster = new RosterModel();
            roster.Refresh(snapshot, selected: new PawnId(2));

            Assert.That(roster.Cards.Count, Is.EqualTo(2));
            Assert.That(roster.Cards[0].Name, Is.EqualTo("Wrenn"));
            Assert.That(roster.Cards[0].Layer, Is.EqualTo(2));
            Assert.That(roster.Cards[0].Selected, Is.False);
            Assert.That(roster.Cards[1].Name, Is.EqualTo("Odile"));
            Assert.That(roster.Cards[1].Selected, Is.True);
            Assert.That(MoodBands.Band(roster.Cards[1].Mood), Is.EqualTo("breaking"));
        }
    }

    public class InspectModelTests
    {
        [Test]
        public void ColonistPaneShowsNeedsAndDisabledTabsAndCommands()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(new PawnView(new PawnId(3), new CellRef(4, 5, 1), 620, 710, 72, JobHandle.Eat));

            var pane = new InspectModel();
            pane.SetColonist(new PawnId(3));
            pane.Refresh(snapshot);

            Assert.That(pane.Subject, Is.EqualTo(InspectSubject.Colonist));
            Assert.That(pane.Title, Is.EqualTo("Kester"));
            Assert.That(pane.Job, Is.EqualTo("Eating"), "the registry's word for ui.status.eating");
            Assert.That(pane.Layer, Is.EqualTo(1));

            Assert.That(pane.Tabs.Count, Is.EqualTo(7));
            Assert.That(pane.Tabs[0].Name, Is.EqualTo("Needs"));
            Assert.That(pane.Tabs[0].Enabled, Is.True);
            Assert.That(pane.Tabs.Count(t => t.Enabled), Is.EqualTo(1),
                "only Needs is live in the slice; the rest are visible with reasons");

            Assert.That(pane.Commands, Is.Not.Empty);
            Assert.That(pane.Commands.Count(c => c.Enabled), Is.Zero,
                "no colonist command is wired yet, and none may pretend to be");
        }

        [Test]
        public void ColonistGoneFromTheFrameTombstonesInsteadOfClosing()
        {
            var withPawn = Frame.Write();
            withPawn.AddPawn(new PawnView(new PawnId(3), new CellRef(4, 5, 1), 620, 710, 72));

            var pane = new InspectModel();
            pane.SetColonist(new PawnId(3));
            pane.Refresh(withPawn);
            string lastName = pane.Title;

            pane.Refresh(Frame.Write()); // next frame has no such pawn

            Assert.That(pane.Tombstoned, Is.True);
            Assert.That(pane.Title, Is.EqualTo(lastName), "last-known values stay visible, greyed");
            Assert.That(pane.Commands.Count(c => c.Enabled), Is.Zero);
        }

        [Test]
        public void NoSelectionSummarisesTheColony()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(new PawnView(new PawnId(1), new CellRef(1, 1, 1), 500, 500, 50, JobHandle.Haul));
            snapshot.AddPawn(new PawnView(new PawnId(2), new CellRef(2, 2, 1), 500, 500, 50, JobHandle.Haul));

            var pane = new InspectModel();
            pane.ClearSelection();
            pane.Refresh(snapshot);

            Assert.That(pane.Subject, Is.EqualTo(InspectSubject.None));
            Assert.That(pane.ColonySize, Is.EqualTo(2));
            Assert.That(pane.JobCounts[JobHandle.Haul], Is.EqualTo(2));
            Assert.That(pane.JobCounts[JobHandle.Sleep], Is.Zero);
        }
    }

    public class LayerRulerModelTests
    {
        [Test]
        public void RowsRunTopFirstAndCountPawnsPerLayer()
        {
            var snapshot = Frame.Write(layers: 4, sliceLayer: 1);
            snapshot.AddPawn(new PawnView(new PawnId(1), new CellRef(1, 1, 3), 0, 0, 50));
            snapshot.AddPawn(new PawnView(new PawnId(2), new CellRef(2, 2, 3), 0, 0, 50));
            snapshot.AddPawn(new PawnView(new PawnId(3), new CellRef(3, 3, 1), 0, 0, 50));

            var ruler = new LayerRulerModel();
            ruler.Refresh(snapshot, activeLayer: 1, surfaceLayer: 1);

            Assert.That(ruler.Rows.Count, Is.EqualTo(4));
            Assert.That(ruler.Rows[0].Layer, Is.EqualTo(3), "top layer first, so up is up");
            Assert.That(ruler.Rows[0].Pawns, Is.EqualTo(2));
            Assert.That(ruler.Rows[2].Layer, Is.EqualTo(1));
            Assert.That(ruler.Rows[2].Active, Is.True);
            Assert.That(ruler.Rows[2].Surface, Is.True);
        }

        [Test]
        public void OccupancyIsKnownOnlyForThePublishedSlice()
        {
            var snapshot = Frame.Write(layers: 3, sliceLayer: 2);
            var slice = snapshot.BeginSlice(10 * 10);
            for (int i = 0; i < slice.Length; i++) slice[i] = i < 25 ? (byte)1 : (byte)0;

            var ruler = new LayerRulerModel();
            ruler.Refresh(snapshot, activeLayer: 2, surfaceLayer: 2);

            Assert.That(ruler.Rows[0].Active, Is.True);          // layer 2, top of the list
            Assert.That(ruler.Rows[0].Occupancy, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(ruler.Rows[2].Occupancy, Is.EqualTo(-1f),
                "a layer whose cells the frame does not carry must not claim to be empty");
        }
    }

    public class LedgerModelTests
    {
        [Test]
        public void RealRowsCountStacksAndPlannedRowsStayGreyed()
        {
            // Two piles of meals, twenty and four, are twenty-four meals, not two; the ledger is
            // the number the player decides on, and a pile is not a number.
            var snapshot = Frame.Write();
            snapshot.AddThing(new ThingView(new ThingId(1), new CellRef(1, 1, 1), ItemHandle.Meal, 0, stack: 20));
            snapshot.AddThing(new ThingView(new ThingId(2), new CellRef(2, 2, 1), ItemHandle.Meal, 0, stack: 4));
            snapshot.AddThing(new ThingView(new ThingId(3), new CellRef(3, 3, 1), ItemHandle.Salvage, 0));
            snapshot.AddThing(new ThingView(new ThingId(4), new CellRef(4, 4, 1), ItemHandle.Wood, 0, stack: 20));

            var ledger = new LedgerModel();
            ledger.Refresh(snapshot);

            var meals = ledger.Rows.Find(r => r.Name == "Meal");
            Assert.That(meals.Real, Is.True, "the row is named as the registry names ui.res.meal");
            Assert.That(meals.Quantity, Is.EqualTo(24));
            Assert.That(ledger.Rows.Find(r => r.Name == "Wood").Quantity, Is.EqualTo(20), "felled wood is a real row");

            var scrap = ledger.Rows.FindAll(r => r.IconKey == "ui.res.scrap");
            Assert.That(scrap.Count, Is.EqualTo(1), "a commodity has one row, never a real and a planned one");
            Assert.That(scrap[0].Real, Is.True);
            Assert.That(ledger.Rows.Find(r => r.Name == "Alloy").Real, Is.False);
            Assert.That(ledger.Rows.Find(r => r.Name == "Alloy").Quantity, Is.Zero);
        }
    }
}
