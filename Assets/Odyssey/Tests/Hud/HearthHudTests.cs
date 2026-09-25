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

        static InspectRow? HearthRow(InspectModel model) =>
            model.CellRows.Where(r => r.Name == InspectModel.HearthRow).Cast<InspectRow?>().FirstOrDefault();

        [Test]
        public void ACampfireThatIsNotTheHearthOffersToBeAndStaysPressable()
        {
            var model = Looking(Tile((byte)EdificeHandle.Campfire, hearth: -1));
            Assert.That(HearthRow(model)?.Value, Is.EqualTo(Registry.Label(InspectModel.MakeHearthKey)));
            Assert.That(model.HearthActionUnderPane, Is.True);

            // The second refresh takes the rows' early return; the press must survive it.
            model.Refresh(Tile((byte)EdificeHandle.Campfire, hearth: -1));
            Assert.That(model.HearthActionUnderPane, Is.True, "the press died on the second refresh");
        }

        [Test]
        public void TheHearthSaysSoAndOffersNothing()
        {
            var model = Looking(Tile((byte)EdificeHandle.Campfire, hearth: Size.Index(At)));
            Assert.That(HearthRow(model)?.Value, Is.EqualTo(Registry.Label(InspectModel.HearthHereKey)));
            Assert.That(model.HearthActionUnderPane, Is.False, "the hearth offered to become the hearth");
        }

        [Test]
        public void TheRowFollowsTheHearthWhileThePaneIsHeld()
        {
            var model = Looking(Tile((byte)EdificeHandle.Campfire, hearth: -1));
            model.Refresh(Tile((byte)EdificeHandle.Campfire, hearth: Size.Index(At)));
            Assert.That(HearthRow(model)?.Value, Is.EqualTo(Registry.Label(InspectModel.HearthHereKey)),
                "the row kept offering after this campfire became the hearth");
            Assert.That(model.HearthActionUnderPane, Is.False);
        }

        [Test]
        public void AWallHasNoHearthRow()
        {
            var model = Looking(Tile((byte)EdificeHandle.Wall, hearth: -1));
            Assert.That(HearthRow(model), Is.Null);
            Assert.That(model.HearthActionUnderPane, Is.False);
        }

        // ---- the alerts ----------------------------------------------------------------------

        static WorldSnapshot Colony(int hearth, bool keptHome, bool orderedDown = false)
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 800, 800, 800, JobHandle.Wait, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Bo, new CellRef(2, 1, 1), 800, 800, 800, JobHandle.Wait, flags: PawnFlags.Person));
            if (keptHome) frame.AddPawnAspect(new PawnAspect(Ada, AreaAspectNames.AreaKey, 1));
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

        [Test]
        public void TheWordsAreInTheRegistry()
        {
            Assert.That(AlertModel.IconKeys, Has.Member(AlertModel.NoHearthKey));
            Assert.That(AlertModel.IconKeys, Has.Member(AlertModel.HearthDownKey));
            Assert.That(Registry.Label(AlertModel.NoHearthKey), Is.EqualTo("No hearth"));
            Assert.That(Registry.Label(InspectModel.MakeHearthKey), Is.EqualTo("Make this the hearth"));
            Assert.That(Registry.Label(InspectModel.HearthHereKey), Is.EqualTo("Home is centred here"));
        }

        [Test]
        public void TheAreaAspectIsSpelledAsTheSimulationPublishesIt() =>
            Assert.That(AreaAspectNames.Area, Is.EqualTo("odyssey.pawn.area"));
    }
}
