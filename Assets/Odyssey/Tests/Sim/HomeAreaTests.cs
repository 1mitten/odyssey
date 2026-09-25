#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Storage;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Design 43 §3: the home is everything the colony placed, grown by five cells as a square on
    /// its own layer, and one layer up and down. Derived, never saved, never hashed, rebuilt lazily
    /// per dirty layer.
    ///
    /// <para>Every claim has its control: a square is told from a diamond by the corner, the
    /// vertical margin from a column by the layer two away, "what counts" from "what is ordered" by
    /// a mining mark, and "ours" from "standing" by a wall the colony never built.</para>
    /// </summary>
    public class HomeAreaTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        /// <summary>A colony with nothing placed: no beds, no stockpile, one colonist to stand on the ground.</summary>
        static ColonyWorld Board(ScenarioDef? scenario = null)
        {
            scenario ??= ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 0;
            scenario.stockpileCells = 0;
            return ColonyWorld.Build(Size, 7u, scenario, barren: true, wooded: false);
        }

        static HomeArea Home(ColonyWorld colony) => colony.Pawns.Home!;

        /// <summary>The cell the colonist stands in: open ground, on the surface.</summary>
        static CellRef Standing(ColonyWorld colony) => Size.FromIndex(colony.Pawns.Pawns.All[0].Cell);

        static CellRef At(CellRef from, int dx, int dz, int dy = 0) =>
            new CellRef(from.X + dx, from.Z + dz, from.Y + dy);

        static bool IsHome(ColonyWorld colony, CellRef cell) => Home(colony).Contains(Size.Index(cell));

        /// <summary>Order a wall here and raise it at once; answers the cell it stands in.</summary>
        static int RaiseWall(ColonyWorld colony, CellRef cell)
        {
            Assume.That(colony.Construction.Place(cell, BuildingHandle.Wall, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None), "the wall could not be ordered");
            int site = colony.Construction.Sites[0];
            Assume.That(colony.Construction.Raise(colony.Pawns, site), Is.True, "the wall could not be raised");
            return site;
        }

        [Test]
        public void NothingPlacedMeansNoHome()
        {
            var colony = Board();
            Assert.That(Home(colony).IsEmpty, Is.True);
            Assert.That(Home(colony).CellCount, Is.Zero);
            Assert.That(IsHome(colony, Standing(colony)), Is.False);
        }

        /// <summary>
        /// The played scenario gives no beds and no stockpile (owner, 2026-09-20), so a new colony
        /// has no home at all — the reason an empty home must restrict nobody (design 43 §4d).
        /// </summary>
        [Test]
        public void ThePlayedScenarioStartsWithNoHome()
        {
            var colony = ColonyWorld.Build(Size, 7u, ScenarioDef.Playtest(), barren: true, wooded: false);
            Assert.That(Home(colony).IsEmpty, Is.True);

            // Control: the bare scenario's beds are placed, so it has one.
            ScenarioDef bare = ScenarioDef.Bare();
            bare.colonists = 2;
            bare.beds = 2;
            var withBeds = ColonyWorld.Build(Size, 7u, bare, barren: true, wooded: false);
            Assert.That(Home(withBeds).IsEmpty, Is.False, "two placed beds made no home");
        }

        [Test]
        public void AWallMakesAnElevenCellSquareOnItsLayer()
        {
            var colony = Board();
            CellRef wall = Size.FromIndex(RaiseWall(colony, At(Standing(colony), 10, 0)));

            for (int dx = -HomeArea.Perimeter; dx <= HomeArea.Perimeter; dx++)
            for (int dz = -HomeArea.Perimeter; dz <= HomeArea.Perimeter; dz++)
                Assert.That(IsHome(colony, At(wall, dx, dz)), Is.True, $"({dx}, {dz}) from the wall is not home");

            // The corner is what tells a square from a diamond; one further is outside either way.
            Assert.That(IsHome(colony, At(wall, 5, 5)), Is.True, "the corner is outside: the growth is not square");
            Assert.That(IsHome(colony, At(wall, 6, 0)), Is.False);
            Assert.That(IsHome(colony, At(wall, 0, -6)), Is.False);
            Assert.That(Home(colony).IsEmpty, Is.False);
        }

        [Test]
        public void TheLayersAboveAndBelowAreHomeAndTwoAwayIsNot()
        {
            var colony = Board();
            CellRef wall = Size.FromIndex(RaiseWall(colony, At(Standing(colony), 10, 0)));

            Assert.That(IsHome(colony, At(wall, 3, 3, +1)), Is.True, "the layer above is not home");
            Assert.That(IsHome(colony, At(wall, 3, 3, -1)), Is.True, "the layer below is not home");
            Assert.That(IsHome(colony, At(wall, 0, 0, +2)), Is.False, "two layers above is home: the margin is a column");
            Assert.That(IsHome(colony, At(wall, 0, 0, -2)), Is.False, "two layers below is home: the margin is a column");
        }

        [Test]
        public void DemolishingTheOnlyWallTakesTheHomeAway()
        {
            var colony = Board();
            int wall = RaiseWall(colony, At(Standing(colony), 10, 0));
            Assume.That(Home(colony).IsEmpty, Is.False);

            Assert.That(colony.Construction.Demolish(colony.Pawns, wall, out _), Is.True);
            Assert.That(Home(colony).IsEmpty, Is.True);
            Assert.That(Home(colony).Contains(wall), Is.False);
        }

        [Test]
        public void ASiteNotYetRaisedIsHome()
        {
            var colony = Board();
            Assume.That(colony.Construction.Place(At(Standing(colony), 10, 0), BuildingHandle.Wall, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None));
            int site = colony.Construction.Sites[0];
            Assert.That(Home(colony).Contains(site), Is.True);

            // Cancelling it takes it away again.
            Assert.That(colony.Construction.Cancel(Size.FromIndex(site)), Is.EqualTo(IntentRejection.None));
            Assert.That(Home(colony).IsEmpty, Is.True);
        }

        [Test]
        public void AStockpileCellIsHomeAndTakingItAwayShrinksHome()
        {
            var colony = Board();
            CellRef cell = At(Standing(colony), 10, 0);
            Assume.That(colony.Pawns.Storage!.Designate(cell, Size.Index(cell), StoragePreset.Everything),
                Is.EqualTo(IntentRejection.None));
            Assert.That(IsHome(colony, At(cell, 4, 4)), Is.True);

            Assert.That(colony.Pawns.Storage.Cancel(cell), Is.EqualTo(IntentRejection.None));
            Assert.That(Home(colony).IsEmpty, Is.True);
        }

        [Test]
        public void AGrowingZoneCellIsHome()
        {
            var colony = Board();
            CellRef cell = At(Standing(colony), 10, 0);
            Assume.That(colony.Pawns.Growing!.Designate(cell, 0), Is.EqualTo(IntentRejection.None),
                "the barren board would not take a field here");
            Assert.That(IsHome(colony, At(cell, -5, 0)), Is.True);
        }

        [Test]
        public void ALineOrderAndALaidLineAreHome()
        {
            var colony = Board();
            int cell = Size.Index(At(Standing(colony), 10, 0));
            var power = colony.Pawns.Power!;
            Assume.That(power.PlaceLine(cell), Is.EqualTo(IntentRejection.None));
            int ordered = power.Sites[0];
            Assert.That(Home(colony).Contains(ordered), Is.True, "a line order is not home");

            Assert.That(power.Lay(ordered), Is.True);
            Assert.That(Home(colony).Contains(ordered), Is.True, "a laid line is not home");
        }

        /// <summary>
        /// Felling and mining marks never count (owner, 2026-09-24): the work outside is where the
        /// danger is. Nor do they touch the footprint, so marking and digging cost the home nothing.
        /// </summary>
        [Test]
        public void AMiningMarkIsNotHomeAndDoesNotTouchTheFootprint()
        {
            var colony = Board();
            CellRef ground = At(Standing(colony), 10, 0, -1);
            int before = colony.Pawns.Cells.Footprint.Version;
            Home(colony).Rebuild();

            Assume.That(colony.Pawns.Designations!.Designate(ground, DesignationKind.Mine),
                Is.EqualTo(IntentRejection.None), "the ground would not take a mining mark");
            Assert.That(colony.Pawns.Cells.Footprint.Version, Is.EqualTo(before), "a mining mark touched the footprint");
            Assert.That(Home(colony).IsEmpty, Is.True, "a mining mark made a home");
        }

        /// <summary>A wall the colony never built — the ruined city's — stands but is not ours.</summary>
        [Test]
        public void AWallNobodyBuiltIsNotHome()
        {
            var colony = Board();
            int cell = Size.Index(At(Standing(colony), 10, 0));
            var records = colony.Construction.Edifices.Records;
            records.Add(new PlacedEdifice { CellIndex = cell, Def = CoreContent.EdificeWall, Built = false });
            colony.Pawns.Cells.Edifice[cell] = records.Count - 1;
            colony.Pawns.Cells.Footprint.Touch(cell);
            Assert.That(Home(colony).IsEmpty, Is.True, "a ruin's wall made a home");

            // Control: the same record, ours.
            PlacedEdifice ours = records[records.Count - 1];
            ours.Built = true;
            records[records.Count - 1] = ours;
            colony.Pawns.Cells.Footprint.Touch(cell);
            Assert.That(Home(colony).Contains(cell), Is.True);
        }

        [Test]
        public void OnlyTheTouchedLayerIsGrownAgain()
        {
            var colony = Board();
            Home(colony).Rebuild();
            Assert.That(Home(colony).Rebuild(), Is.Zero, "a rebuild with nothing dirty did work");

            RaiseWall(colony, At(Standing(colony), 10, 0));
            Assert.That(Home(colony).Rebuild(), Is.EqualTo(1), "a wall on one layer grew more than one");
            Assert.That(Home(colony).Rebuild(), Is.Zero);
        }

        [Test]
        public void TheHomeAfterALoadIsTheHomeBeforeIt()
        {
            var colony = Board();
            CellRef stand = Standing(colony);
            RaiseWall(colony, At(stand, 10, 0));
            colony.Pawns.Storage!.Designate(At(stand, -12, 3), Size.Index(At(stand, -12, 3)), StoragePreset.Everything);

            var restored = Board();
            restored.Load(colony.Save());

            int differ = 0;
            for (int i = 0; i < Size.CellCount; i++)
                if (Home(restored).Contains(i) != Home(colony).Contains(i)) differ++;
            Assert.That(differ, Is.Zero, "the loaded home differs from the saved one");
            Assert.That(Home(restored).CellCount, Is.EqualTo(Home(colony).CellCount));
            Assert.That(Home(restored).IsEmpty, Is.False);
        }

        /// <summary>Home is never hashed: a world with one is the same hash asked before and after a rebuild.</summary>
        [Test]
        public void AskingTheHomeDoesNotMoveTheHash()
        {
            var colony = Board();
            RaiseWall(colony, At(Standing(colony), 10, 0));
            ulong before = colony.World.ComputeStateHash().Value;
            Home(colony).Rebuild();
            _ = Home(colony).CellCount;
            Assert.That(colony.World.ComputeStateHash().Value, Is.EqualTo(before));
        }

        [Test]
        public void TheEdgesAreTheBorderOfHome()
        {
            var colony = Board();
            CellRef wall = Size.FromIndex(RaiseWall(colony, At(Standing(colony), 10, 0)));
            HomeArea home = Home(colony);

            Assert.That(home.EdgesOf(Size.Index(wall)), Is.Zero, "the middle of home has an edge");
            Assert.That(home.EdgesOf(Size.Index(At(wall, 5, 0))), Is.EqualTo(2), "the east edge is not east");
            Assert.That(home.EdgesOf(Size.Index(At(wall, -5, -5))), Is.EqualTo(1 | 4), "the south-west corner");
            Assert.That(home.EdgesOf(Size.Index(At(wall, 6, 0))), Is.Zero, "a cell outside home has an edge");
        }
    }
}
