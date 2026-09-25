#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using static Odyssey.Tests.Sim.HomeFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Design 43 §5c: the channel the Home view draws from. The border is published only while
    /// the view is on; only cells a colonist could stand in are published, so the layer of margin
    /// above and below a base draws no outline of its own; and the version moves exactly when the
    /// rows do, so a still colony costs the drawing pass nothing.
    /// </summary>
    public class WatchHomeTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Board()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 0;
            scenario.stockpileCells = 0;
            return ColonyWorld.Build(Size, 7u, scenario, barren: true, wooded: false);
        }

        static CellRef At(CellRef from, int dx, int dz, int dy = 0) =>
            new CellRef(from.X + dx, from.Z + dz, from.Y + dy);

        /// <summary>A hearth ten cells east of the colonist; answers where it stands.</summary>
        static CellRef HearthAt(ColonyWorld colony) =>
            Size.FromIndex(Campfire(colony, At(Size.FromIndex(colony.Pawns.Pawns.All[0].Cell), 10, 0)));

        static void Watch(ColonyWorld colony, bool on)
        {
            colony.World.Intents.Submit(new Intent(IntentKind.WatchHome, colony.Start, on ? 1 : 0));
            colony.World.RepublishViews();
        }

        static WorldSnapshot Frame(ColonyWorld colony) => colony.World.Views.Current;

        static byte EdgesAt(ColonyWorld colony, CellRef cell)
        {
            int index = Size.Index(cell);
            foreach (HomeCellView row in Frame(colony).HomeCells)
                if (row.CellIndex == index) return row.Edges;
            return 0;
        }

        static void Dig(ColonyWorld colony, CellRef cell)
        {
            int index = Size.Index(cell);
            MineJobDriver.MineCell(colony.Pawns, index, colony.Grid.Terrain[index]);
            colony.World.Tick();
        }

        [Test]
        public void NothingIsPublishedWhileNobodyWatches()
        {
            var colony = Board();
            HearthAt(colony);
            colony.World.Tick();
            Assert.That(Frame(colony).HomeCellCount, Is.Zero, "the border was published with the view off");

            // Control: the same colony, watched.
            Watch(colony, true);
            Assert.That(Frame(colony).HomeCellCount, Is.GreaterThan(0), "the watched border is empty");

            Watch(colony, false);
            Assert.That(Frame(colony).HomeCellCount, Is.Zero, "the border outlived the view");
        }

        /// <summary>
        /// The hearth's eleven-cell square has forty border cells on its own layer. The layers
        /// above and below are home too, but one is air over air and the other earth, so neither is
        /// published — the control for three outlines stacked a storey apart.
        /// </summary>
        [Test]
        public void OnlyTheLayerYouCanStandOnIsOutlined()
        {
            var colony = Board();
            CellRef hearth = HearthAt(colony);
            Assume.That(colony.Pawns.Home!.Contains(Size.Index(At(hearth, 0, 0, 1))), Is.True, "the layer above is not home");
            Assume.That(colony.Pawns.Home!.Contains(Size.Index(At(hearth, 0, 0, -1))), Is.True, "the layer below is not home");
            Watch(colony, true);

            Assert.That(Frame(colony).HomeCellCount, Is.EqualTo(40), "not one ring of forty");
            foreach (HomeCellView row in Frame(colony).HomeCells)
                Assert.That(Size.FromIndex(row.CellIndex).Y, Is.EqualTo(hearth.Y), "a margin layer was outlined");
        }

        [Test]
        public void EachBorderCellCarriesItsOwnSides()
        {
            var colony = Board();
            CellRef hearth = HearthAt(colony);
            Watch(colony, true);

            Assert.That(EdgesAt(colony, At(hearth, 5, 0)), Is.EqualTo(2), "east");
            Assert.That(EdgesAt(colony, At(hearth, 0, -5)), Is.EqualTo(4), "south");
            Assert.That(EdgesAt(colony, At(hearth, -5, 5)), Is.EqualTo(1 | 8), "the north-west corner");
            Assert.That(EdgesAt(colony, hearth), Is.Zero, "the middle of home was published");

            int previous = -1;
            foreach (HomeCellView row in Frame(colony).HomeCells)
            {
                Assert.That(row.CellIndex, Is.GreaterThan(previous), "the rows are not in cell order");
                previous = row.CellIndex;
            }
        }

        /// <summary>A still colony keeps its version; a wall that grows home moves it.</summary>
        [Test]
        public void TheVersionMovesWhenTheBorderDoesAndOnlyThen()
        {
            var colony = Board();
            CellRef hearth = HearthAt(colony);
            Watch(colony, true);
            int first = Frame(colony).HomeVersion;

            colony.World.Tick();
            colony.World.RepublishViews();
            Assert.That(Frame(colony).HomeVersion, Is.EqualTo(first), "a still colony moved the version");

            Raise(colony, At(hearth, 5, 0), BuildingHandle.Wall);
            colony.World.RepublishViews();
            Assert.That(Frame(colony).HomeVersion, Is.Not.EqualTo(first), "a wall that grew home moved nothing");
            Assert.That(EdgesAt(colony, At(hearth, 10, 0)), Is.EqualTo(2), "the border did not move east");
        }

        /// <summary>
        /// Digging moves the border without moving the home: the cell over a dug hole is no longer
        /// somewhere to stand. Dug outside home, or under the middle of it, the rows come out the
        /// same and the version stays — the control that it follows the rows, not every dig.
        /// </summary>
        [Test]
        public void DiggingUnderTheBorderMovesItAndDiggingElsewhereDoesNot()
        {
            var colony = Board();
            CellRef hearth = HearthAt(colony);
            Watch(colony, true);
            int first = Frame(colony).HomeVersion;

            Dig(colony, At(hearth, 15, 0, -1));
            Assert.That(Frame(colony).HomeVersion, Is.EqualTo(first), "a dig outside home moved the version");
            Dig(colony, At(hearth, 2, 2, -1));
            Assert.That(Frame(colony).HomeVersion, Is.EqualTo(first), "a dig under the middle of home moved the version");

            Dig(colony, At(hearth, 5, 0, -1));
            Assert.That(Frame(colony).HomeVersion, Is.Not.EqualTo(first), "a dig under the border moved nothing");
            Assert.That(EdgesAt(colony, At(hearth, 5, 0)), Is.Zero, "the cell over the hole is still outlined");
            Assert.That(EdgesAt(colony, At(hearth, 5, 0, -1)), Is.EqualTo(2), "the floor of the hole is not outlined");
        }

        [Test]
        public void APausedWorldStartsShowingTheHomeAtOnce()
        {
            var colony = Board();
            HearthAt(colony);
            colony.World.Tick();
            int tick = colony.World.CurrentTick;

            Watch(colony, true);
            Assert.That(colony.World.CurrentTick, Is.EqualTo(tick), "a tick was spent");
            Assert.That(Frame(colony).HomeCellCount, Is.EqualTo(40));
        }

        [Test]
        public void NoHearthPublishesNoBorder()
        {
            var colony = Board();
            Raise(colony, At(Size.FromIndex(colony.Pawns.Pawns.All[0].Cell), 6, 0), BuildingHandle.Wall);
            Watch(colony, true);
            Assert.That(Frame(colony).HomeCellCount, Is.Zero, "a wall with no hearth was outlined");
        }
    }
}
