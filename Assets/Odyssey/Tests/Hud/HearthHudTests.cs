#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Design 43 §3f on the interface: a campfire's pane says whether home is centred on it, and
    /// any other offers to be; the alerts say when somebody kept home is kept nowhere, and when the
    /// hearth is ordered down.
    /// </summary>
    public class HearthHudTests
    {
        static readonly GridSize Size = new GridSize(10, 10, 4);
        static readonly CellRef At = new CellRef(3, 4, 1);
        static readonly PawnId Ada = new PawnId(1), Bo = new PawnId(2);

        /// <summary>The tile at <see cref="At"/> holding this edifice; the hearth where it is said to be.</summary>
        static WorldSnapshot Tile(byte edifice, int hearth)
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(tick: 0, Size, sliceLayer: 1);
            frame.AddCellDetail(new CellDetail(Size.Index(At), TerrainHandle.Grass, edifice, StuffHandle.Wood, 0, 1000, 0));
            frame.SetHearthCell(hearth);
            return frame;
        }

        static InspectModel Looking(WorldSnapshot frame)
        {
            var model = new InspectModel();
            model.SetCell(At);
            model.Refresh(frame);
            return model;
        }

        static bool AnyRowSays(InspectModel model, string key) =>
            model.CellRows.Any(r => r.Value == Registry.Label(key));

        [Test]
        public void ACampfireThatIsNotTheHearthOffersToBeAndStaysPressable()
        {
            var model = Looking(Tile((byte)EdificeHandle.Campfire, hearth: -1));
            Assert.That(model.IsCampfire, Is.True);
            Assert.That(model.OffersHearth, Is.True);
            Assert.That(model.IsHearth, Is.False);

            // The second refresh takes the rows' early return; the offer must survive it.
            model.Refresh(Tile((byte)EdificeHandle.Campfire, hearth: -1));
            Assert.That(model.OffersHearth, Is.True, "the offer died on the second refresh");
        }

        [Test]
        public void TheHearthSaysSoAndOffersNothing()
        {
            var model = Looking(Tile((byte)EdificeHandle.Campfire, hearth: Size.Index(At)));
            Assert.That(model.IsHearth, Is.True);
            Assert.That(model.OffersHearth, Is.False, "the hearth offered to become the hearth");
        }

        /// <summary>The two are header facts now (design 43 §6), not a row of the tile's readout.</summary>
        [Test]
        public void NeitherIsARowAnyMore()
        {
            var offered = Looking(Tile((byte)EdificeHandle.Campfire, hearth: -1));
            var hearth = Looking(Tile((byte)EdificeHandle.Campfire, hearth: Size.Index(At)));
            Assert.That(AnyRowSays(offered, InspectModel.MakeHearthKey), Is.False, "the offer is still a row");
            Assert.That(AnyRowSays(hearth, InspectModel.HearthKey), Is.False, "the hearth is still a row");
            Assert.That(Registry.Label(InspectModel.HearthKey), Is.EqualTo("Hearth"));
        }

        [Test]
        public void TheHeaderFollowsTheHearthWhileThePaneIsHeld()
        {
            var model = Looking(Tile((byte)EdificeHandle.Campfire, hearth: -1));
            model.Refresh(Tile((byte)EdificeHandle.Campfire, hearth: Size.Index(At)));
            Assert.That(model.IsHearth, Is.True, "the pane kept offering after this campfire became the hearth");
            Assert.That(model.OffersHearth, Is.False);

            // And back: the hearth moved to another fire.
            model.Refresh(Tile((byte)EdificeHandle.Campfire, hearth: Size.Index(new CellRef(7, 7, 1))));
            Assert.That(model.OffersHearth, Is.True);
        }

        /// <summary>A campfire's pane is wide, as a store's is; a wall's is the narrow column.</summary>
        [Test]
        public void ACampfiresPaneIsWideAndAWallsIsNot()
        {
            Assert.That(Looking(Tile((byte)EdificeHandle.Campfire, hearth: -1)).IsWide, Is.True);
            Assert.That(Looking(Tile((byte)EdificeHandle.Campfire, hearth: Size.Index(At))).IsWide, Is.True);
            var wall = Looking(Tile((byte)EdificeHandle.Wall, hearth: -1));
            Assert.That(wall.IsWide, Is.False);
            Assert.That(wall.IsCampfire || wall.OffersHearth || wall.IsHearth, Is.False);
        }

        // ---- the alerts ----------------------------------------------------------------------

        static WorldSnapshot Colony(int hearth, bool keptHome, bool orderedDown = false, bool boKeptHome = false)
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 800, 800, 800, JobHandle.Wait, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Bo, new CellRef(2, 1, 1), 800, 800, 800, JobHandle.Wait, flags: PawnFlags.Person));
            if (keptHome) frame.AddPawnAspect(new PawnAspect(Ada, AreaAspectNames.AreaKey, 1));
            if (boKeptHome) frame.AddPawnAspect(new PawnAspect(Bo, AreaAspectNames.AreaKey, 1));
            frame.SetHearthCell(hearth);
            if (orderedDown && hearth >= 0) frame.AddOrder(new OrderView(hearth, AlertModel.DeconstructOrderKind, 0));
            return frame;
        }

        static bool Raised(AlertModel alerts, string key) => alerts.Rows.Any(r => r.Key == key);

        [Test]
        public void NoHearthIsSaidOnlyWhileSomebodyIsKeptHome()
        {
            var alerts = new AlertModel();
            alerts.Refresh(Colony(hearth: -1, keptHome: true), 0.0);
            Assert.That(Raised(alerts, AlertModel.NoHearthKey), Is.True);

            alerts.Refresh(Colony(hearth: -1, keptHome: false), 0.0);
            Assert.That(Raised(alerts, AlertModel.NoHearthKey), Is.False, "said with nobody kept home");

            alerts.Refresh(Colony(hearth: 5, keptHome: true), 0.0);
            Assert.That(Raised(alerts, AlertModel.NoHearthKey), Is.False, "said with a hearth");
        }

        [Test]
        public void TheHearthOrderedDownIsSaidAndGoesToIt()
        {
            var alerts = new AlertModel();
            int hearth = new GridSize(10, 10, 4).Index(new CellRef(4, 4, 1));
            alerts.Refresh(Colony(hearth, keptHome: false, orderedDown: true), 0.0);
            AlertRow row = alerts.Rows.Single(r => r.Key == AlertModel.HearthDownKey);
            Assert.That(row.Cell, Is.EqualTo(new CellRef(4, 4, 1)), "a click on it must go to the hearth");

            alerts.Refresh(Colony(hearth, keptHome: false, orderedDown: false), 0.0);
            Assert.That(Raised(alerts, AlertModel.HearthDownKey), Is.False, "the warning outlived the order");
        }

        [Test]
        public void ADismissedHearthWarningStaysDismissedUntilTheOrderGoes()
        {
            var alerts = new AlertModel();
            int hearth = new GridSize(10, 10, 4).Index(new CellRef(4, 4, 1));
            alerts.Refresh(Colony(hearth, keptHome: false, orderedDown: true), 0.0);
            alerts.Dismiss(alerts.Rows.Single(r => r.Key == AlertModel.HearthDownKey).DismissKey);
            alerts.Refresh(Colony(hearth, keptHome: false, orderedDown: true), 1.0);
            Assert.That(Raised(alerts, AlertModel.HearthDownKey), Is.False, "the dismissal did not stick");

            alerts.Refresh(Colony(hearth, keptHome: false, orderedDown: false), 2.0);
            alerts.Refresh(Colony(hearth, keptHome: false, orderedDown: true), 3.0);
            Assert.That(Raised(alerts, AlertModel.HearthDownKey), Is.True, "a new order was kept dismissed");
        }

        /// <summary>
        /// The rows follow what they say, not only whether they are up. The panel skips its
        /// rebuild when nothing it watches moved, and it watched the two conditions as yes/no —
        /// so a second colonist kept home left "No hearth" counting one, and the hearth moving to
        /// another marked campfire left the warning pointing at the old one.
        /// </summary>
        [Test]
        public void TheRowsFollowTheCountAndTheCellNotOnlyWhetherTheyAreUp()
        {
            var alerts = new AlertModel();
            alerts.Refresh(Colony(hearth: -1, keptHome: true), 0.0);
            Assert.That(alerts.Rows.Single(r => r.Key == AlertModel.NoHearthKey).Count, Is.EqualTo(1));
            alerts.Refresh(Colony(hearth: -1, keptHome: true, boKeptHome: true), 1.0);
            Assert.That(alerts.Rows.Single(r => r.Key == AlertModel.NoHearthKey).Count, Is.EqualTo(2),
                "a second colonist kept home did not move the count");

            var size = new GridSize(10, 10, 4);
            int first = size.Index(new CellRef(4, 4, 1)), second = size.Index(new CellRef(7, 2, 1));
            alerts.Refresh(Colony(first, keptHome: false, orderedDown: true), 2.0);
            alerts.Refresh(Colony(second, keptHome: false, orderedDown: true), 3.0);
            Assert.That(alerts.Rows.Single(r => r.Key == AlertModel.HearthDownKey).Cell, Is.EqualTo(new CellRef(7, 2, 1)),
                "the hearth moved to another marked campfire and the warning still points at the old one");
        }

        [Test]
        public void TheWordsAreInTheRegistry()
        {
            Assert.That(AlertModel.IconKeys, Has.Member(AlertModel.NoHearthKey));
            Assert.That(AlertModel.IconKeys, Has.Member(AlertModel.HearthDownKey));
            Assert.That(Registry.Label(AlertModel.NoHearthKey), Is.EqualTo("No hearth"));
            Assert.That(Registry.Label(InspectModel.MakeHearthKey), Is.EqualTo("Make this the hearth"));
            Assert.That(Registry.Label(InspectModel.HearthKey), Is.EqualTo("Hearth"));
        }

        [Test]
        public void TheAreaAspectIsSpelledAsTheSimulationPublishesIt() =>
            Assert.That(AreaAspectNames.Area, Is.EqualTo("odyssey.pawn.area"));
    }
}
