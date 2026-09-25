#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Storage;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using static Odyssey.Tests.Sim.HomeFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Design 43 §3: the home is everything the colony placed, grown by five cells as a square on
    /// its own layer and one layer up and down, and of that only the piece joined to the hearth
    /// (§3f). Derived, never saved, never hashed, rebuilt lazily per dirty layer.
    ///
    /// <para>Every claim has its control: a square is told from a diamond by the corner, the
    /// vertical margin from a column by the layer two away, "what counts" from "what is ordered" by
    /// a mining mark, "ours" from "standing" by a wall the colony never built, and "joined" from
    /// "placed" by an outpost that joins once the base reaches it.</para>
    /// </summary>
    public class HomeAreaTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        /// <summary>A colony with nothing placed: no beds, no stockpile, one colonist to stand on the ground.</summary>
        static ColonyWorld Board()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
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

        /// <summary>A hearth ten cells east of the colonist; answers where it stands.</summary>
        static CellRef HearthAt(ColonyWorld colony) => Size.FromIndex(Campfire(colony, At(Standing(colony), 10, 0)));

        [Test]
        public void NothingPlacedMeansNoHome()
        {
            var colony = Board();
            Assert.That(Home(colony).IsEmpty, Is.True);
            Assert.That(Home(colony).CellCount, Is.Zero);
            Assert.That(IsHome(colony, Standing(colony)), Is.False);
        }

        /// <summary>
        /// The played scenario gives no beds, no stockpile and no campfire (owner, 2026-09-20), so a
        /// new colony has no home at all — the reason no hearth must restrict nobody (§4d).
        /// </summary>
        [Test]
        public void ANewColonyHasNoHomeUntilACampfireIsRaised()
        {
            var colony = ColonyWorld.Build(Size, 7u, ScenarioDef.Playtest(), barren: true, wooded: false);
            Assert.That(Home(colony).IsEmpty, Is.True);

            // Beds are placed, but a colony with beds and no hearth still has no home.
            ScenarioDef bare = ScenarioDef.Bare();
            bare.colonists = 2;
            bare.beds = 2;
            var withBeds = ColonyWorld.Build(Size, 7u, bare, barren: true, wooded: false);
            Assert.That(Home(withBeds).IsEmpty, Is.True, "beds with no hearth made a home");

            // Control: a campfire beside them, and there is one.
            Campfire(withBeds, At(Size.FromIndex(withBeds.Pawns.Pawns.All[0].Cell), 3, 3));
            Assert.That(Home(withBeds).IsEmpty, Is.False, "a campfire made no home");
        }

        [Test]
        public void WallsWithNoHearthMakeNoHome()
        {
            var colony = Board();
            CellRef stand = Standing(colony);
            int wall = Raise(colony, At(stand, 10, 0), BuildingHandle.Wall);
            Assert.That(Home(colony).IsEmpty, Is.True, "a wall with no hearth made a home");

            Campfire(colony, At(stand, 12, 0));
            Assert.That(Home(colony).Contains(wall), Is.True, "the wall beside the hearth is not home");
        }

        [Test]
        public void TheHearthMakesAnElevenCellSquareOnItsLayer()
        {
            var colony = Board();
            CellRef hearth = HearthAt(colony);

            for (int dx = -HomeArea.Perimeter; dx <= HomeArea.Perimeter; dx++)
            for (int dz = -HomeArea.Perimeter; dz <= HomeArea.Perimeter; dz++)
                Assert.That(IsHome(colony, At(hearth, dx, dz)), Is.True, $"({dx}, {dz}) from the hearth is not home");

            // The corner is what tells a square from a diamond; one further is outside either way.
            Assert.That(IsHome(colony, At(hearth, 5, 5)), Is.True, "the corner is outside: the growth is not square");
            Assert.That(IsHome(colony, At(hearth, 6, 0)), Is.False);
            Assert.That(IsHome(colony, At(hearth, 0, -6)), Is.False);
            Assert.That(Home(colony).CellCount, Is.EqualTo(121 * 3), "one square on the hearth's layer and each beside it");
        }

        [Test]
        public void TheLayersAboveAndBelowAreHomeAndTwoAwayIsNot()
        {
            var colony = Board();
            CellRef hearth = HearthAt(colony);

            Assert.That(IsHome(colony, At(hearth, 3, 3, +1)), Is.True, "the layer above is not home");
            Assert.That(IsHome(colony, At(hearth, 3, 3, -1)), Is.True, "the layer below is not home");
            Assert.That(IsHome(colony, At(hearth, 0, 0, +2)), Is.False, "two layers above is home: the margin is a column");
            Assert.That(IsHome(colony, At(hearth, 0, 0, -2)), Is.False, "two layers below is home: the margin is a column");
        }

        [Test]
        public void DemolishingTheHearthTakesTheHomeAwayAndTheWallsStay()
        {
            var colony = Board();
            CellRef hearth = HearthAt(colony);
            int wall = Raise(colony, At(hearth, 2, 0), BuildingHandle.Wall);
            Assume.That(Home(colony).Contains(wall), Is.True);

            Assert.That(colony.Construction.Demolish(colony.Pawns, Size.Index(hearth), out _), Is.True);
            Assert.That(Home(colony).IsEmpty, Is.True, "the home outlived its hearth");
            Assert.That(colony.Pawns.Cells.Edifice[wall], Is.GreaterThanOrEqualTo(0), "the wall went with the hearth");
        }

        /// <summary>An outpost is not home until the base grows out to meet it (§3f).</summary>
        [Test]
        public void AnOutpostJoinsOnlyWhenTheBaseReachesIt()
        {
            var colony = Board();
            CellRef hearth = HearthAt(colony);
            int outpost = Raise(colony, At(hearth, 16, 0), BuildingHandle.Wall);
            Assert.That(Home(colony).Contains(outpost), Is.False, "a wall sixteen cells out is home");

            // A wall eight out grows to thirteen; the outpost grows back to eleven. They touch.
            Raise(colony, At(hearth, 8, 0), BuildingHandle.Wall);
            Assert.That(Home(colony).Contains(outpost), Is.True, "the outpost did not join when the base reached it");
        }

        [Test]
        public void ASiteBesideTheBaseIsHomeAndOneFarOutIsNot()
        {
            var colony = Board();
            CellRef hearth = HearthAt(colony);
            int near = Order(colony, At(hearth, 8, 0), BuildingHandle.Wall);
            int far = Order(colony, At(hearth, -20, 0), BuildingHandle.Wall);
            Assert.That(Home(colony).Contains(near), Is.True);
            Assert.That(Home(colony).Contains(far), Is.False, "a site far out made itself home");

            Assert.That(colony.Construction.Cancel(Size.FromIndex(near)), Is.EqualTo(IntentRejection.None));
            Assert.That(IsHome(colony, At(hearth, 12, 0)), Is.False, "the cancelled site still reaches");
        }

        [Test]
        public void AStockpileBesideTheBaseIsHomeAndTakingItAwayShrinksHome()
        {
            var colony = Board();
            CellRef hearth = HearthAt(colony);
            CellRef cell = At(hearth, 8, 0);
            Assume.That(colony.Pawns.Storage!.Designate(cell, Size.Index(cell), StoragePreset.Everything),
                Is.EqualTo(IntentRejection.None));
            Assert.That(IsHome(colony, At(hearth, 12, 0)), Is.True);

            Assert.That(colony.Pawns.Storage.Cancel(cell), Is.EqualTo(IntentRejection.None));
            Assert.That(IsHome(colony, At(hearth, 12, 0)), Is.False);
        }

        [Test]
        public void AGrowingZoneBesideTheBaseIsHome()
        {
            var colony = Board();
            CellRef hearth = HearthAt(colony);
            Assume.That(colony.Pawns.Growing!.Designate(At(hearth, 8, 0), 0), Is.EqualTo(IntentRejection.None),
                "the barren board would not take a field here");
            Assert.That(IsHome(colony, At(hearth, 12, 0)), Is.True);
        }

        [Test]
        public void ALineOrderAndALaidLineBesideTheBaseAreHome()
        {
            var colony = Board();
            CellRef hearth = HearthAt(colony);
            var power = colony.Pawns.Power!;
            Assume.That(power.PlaceLine(Size.Index(At(hearth, 8, 0))), Is.EqualTo(IntentRejection.None));
            int ordered = power.Sites[0];
            Assert.That(IsHome(colony, At(Size.FromIndex(ordered), 4, 0)), Is.True, "a line order does not grow home");

            Assert.That(power.Lay(ordered), Is.True);
            Assert.That(IsHome(colony, At(Size.FromIndex(ordered), 4, 0)), Is.True, "a laid line does not grow home");
        }

        /// <summary>
        /// Felling and mining marks never count (owner, 2026-09-24): the work outside is where the
        /// danger is. Nor do they touch the footprint, so marking and digging cost the home nothing.
        /// </summary>
        [Test]
        public void AMiningMarkIsNotHomeAndDoesNotTouchTheFootprint()
        {
            var colony = Board();
            CellRef hearth = HearthAt(colony);
            CellRef ground = At(hearth, 8, 0, -1);
            Home(colony).Rebuild();
            int before = colony.Pawns.Cells.Footprint.Version;

            Assume.That(colony.Pawns.Designations!.Designate(ground, DesignationKind.Mine),
                Is.EqualTo(IntentRejection.None), "the ground would not take a mining mark");
            Assert.That(colony.Pawns.Cells.Footprint.Version, Is.EqualTo(before), "a mining mark touched the footprint");
            Assert.That(IsHome(colony, At(hearth, 12, 0)), Is.False, "a mining mark grew home");
        }

        /// <summary>A wall the colony never built — the ruined city's — stands but is not ours.</summary>
        [Test]
        public void AWallNobodyBuiltIsNotHome()
        {
            var colony = Board();
            CellRef hearth = HearthAt(colony);
            int cell = Size.Index(At(hearth, 8, 0));
            var records = colony.Construction.Edifices.Records;
            records.Add(new PlacedEdifice { CellIndex = cell, Def = CoreContent.EdificeWall, Built = false });
            colony.Pawns.Cells.Edifice[cell] = records.Count - 1;
            colony.Pawns.Cells.Footprint.Touch(cell);
            Assert.That(IsHome(colony, At(hearth, 12, 0)), Is.False, "a ruin's wall grew home");

            // Control: the same record, ours.
            PlacedEdifice ours = records[records.Count - 1];
            ours.Built = true;
            records[records.Count - 1] = ours;
            colony.Pawns.Cells.Footprint.Touch(cell);
            Assert.That(IsHome(colony, At(hearth, 12, 0)), Is.True);
        }

        [Test]
        public void OnlyTheTouchedLayerIsGrownAgain()
        {
            var colony = Board();
            CellRef hearth = HearthAt(colony);
            Home(colony).Rebuild();
            Assert.That(Home(colony).Rebuild(), Is.Zero, "a rebuild with nothing dirty did work");

            Raise(colony, At(hearth, 3, 0), BuildingHandle.Wall);
            Assert.That(Home(colony).Rebuild(), Is.EqualTo(1), "a wall on one layer grew more than one");
            Assert.That(Home(colony).Rebuild(), Is.Zero);
        }

        [Test]
        public void TheHomeAfterALoadIsTheHomeBeforeIt()
        {
            var colony = Board();
            CellRef hearth = HearthAt(colony);
            Raise(colony, At(hearth, 16, 0), BuildingHandle.Wall);
            colony.Pawns.Storage!.Designate(At(hearth, -8, 3), Size.Index(At(hearth, -8, 3)), StoragePreset.Everything);

            var restored = Board();
            restored.Load(colony.Save());

            int differ = 0;
            for (int i = 0; i < Size.CellCount; i++)
                if (Home(restored).Contains(i) != Home(colony).Contains(i)) differ++;
            Assert.That(differ, Is.Zero, "the loaded home differs from the saved one");
            Assert.That(Home(restored).CellCount, Is.EqualTo(Home(colony).CellCount));
            Assert.That(Home(restored).IsEmpty, Is.False);
        }

        /// <summary>Home is never hashed: asking it, and rebuilding it, leaves the hash where it was.</summary>
        [Test]
        public void AskingTheHomeDoesNotMoveTheHash()
        {
            var colony = Board();
            HearthAt(colony);
            ulong before = colony.World.ComputeStateHash().Value;
            colony.Pawns.Cells.Footprint.TouchAll();
            Home(colony).Rebuild();
            _ = Home(colony).CellCount;
            Assert.That(colony.World.ComputeStateHash().Value, Is.EqualTo(before));
        }

        [Test]
        public void TheEdgesAreTheBorderOfHome()
        {
            var colony = Board();
            CellRef hearth = HearthAt(colony);
            HomeArea home = Home(colony);

            Assert.That(home.EdgesOf(Size.Index(hearth)), Is.Zero, "the middle of home has an edge");
            Assert.That(home.EdgesOf(Size.Index(At(hearth, 5, 0))), Is.EqualTo(2), "the east edge is not east");
            Assert.That(home.EdgesOf(Size.Index(At(hearth, -5, -5))), Is.EqualTo(1 | 4), "the south-west corner");
            Assert.That(home.EdgesOf(Size.Index(At(hearth, 6, 0))), Is.Zero, "a cell outside home has an edge");
        }
    }
}
