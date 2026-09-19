#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    /// <summary>The debug menu's two intents: spawn a colonist, and grant a resource.</summary>
    public class DebugIntentTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 6);

        /// <summary>Deep enough that there is real air above the surface, which is the whole
        /// point of the two tests that aim a debug command at the sky.</summary>
        static readonly GridSize Tall = new GridSize(40, 40, 16);

        static ScenarioDef Colony(int colonists)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = colonists;
            return scenario;
        }

        [Test]
        public void SpawnPawnAddsAColonistAtAWalkableCellAndRefusesOutOfBounds()
        {
            ColonyWorld colony = ColonyWorld.Build(Size, seed: 3u, Colony(1));
            int before = colony.Pawns.Pawns.Count;
            CellRef at = Size.FromIndex(colony.Pawns.Pawns.All[0].Cell);

            colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, at));
            colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, new CellRef(-1, 0, 0)));
            colony.World.Tick();

            Assert.That(colony.Pawns.Pawns.Count, Is.EqualTo(before + 1));
            Assert.That(colony.World.Intents.Rejected, Has.Count.EqualTo(1));
            Assert.That(colony.World.Intents.Rejected[0].Reason, Is.EqualTo(IntentRejection.OutOfBounds));
        }

        /// <summary>
        /// The bug the crowd playtest hit: the debug menu aims at the camera's own layer, which
        /// over open ground is the air above the terrain, and every spawn came back refused.
        /// </summary>
        [Test]
        public void SpawnPawnAimedAtTheAirFallsToTheGroundInThatColumn()
        {
            ColonyWorld colony = ColonyWorld.Build(Tall, seed: 6u, Colony(1));
            int before = colony.Pawns.Pawns.Count;
            CellRef standing = Tall.FromIndex(colony.Pawns.Pawns.All[0].Cell);
            var inTheAir = new CellRef(standing.X, standing.Z, Tall.SizeY - 1);
            Assert.That(colony.Grid.IsWalkable(Tall.Index(inTheAir)), Is.False,
                "the fixture no longer puts air above the colonist, so this proves nothing");

            colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, inTheAir));
            colony.World.Tick();

            Assert.That(colony.World.Intents.Rejected, Is.Empty);
            Assert.That(colony.Pawns.Pawns.Count, Is.EqualTo(before + 1));
            CellRef landed = Tall.FromIndex(colony.Pawns.Pawns.All[before].Cell);
            Assert.That(landed.X, Is.EqualTo(standing.X));
            Assert.That(landed.Z, Is.EqualTo(standing.Z));
            Assert.That(colony.Grid.IsWalkable(Tall.Index(landed)), Is.True);
        }

        /// <summary>
        /// A column with nowhere to stand is refused as <c>NotPermitted</c> and not as
        /// <c>OutOfBounds</c>, which is what it used to say for a cell plainly inside the map.
        /// That lie cost a debugging session.
        /// </summary>
        [Test]
        public void SpawnPawnRefusesAColumnWithNowhereToStandAndSaysSoTruthfully()
        {
            var size = new GridSize(4, 4, 4);
            var grid = new CellGrid(size);
            Assert.That(grid.NearestWalkableInColumn(1, 1, 3), Is.EqualTo(-1));

            grid.Floor[size.Index(1, 1, 2)] = 1;
            Assert.That(grid.NearestWalkableInColumn(1, 1, 3), Is.EqualTo(size.Index(1, 1, 2)));
            Assert.That(grid.NearestWalkableInColumn(1, 1, 0), Is.EqualTo(size.Index(1, 1, 2)),
                "the search reaches upward as well, for a spawn aimed below the ground");
            Assert.That(grid.NearestWalkableInColumn(9, 9, 0), Is.EqualTo(-1),
                "a column off the board is not a column");
        }

        /// <summary>A grant aimed at the air lands on the first floor under it.</summary>
        [Test]
        public void GiveResourceAimedAtTheAirLandsOnTheFloorBelow()
        {
            ColonyWorld colony = ColonyWorld.Build(Tall, seed: 7u, Colony(1));
            CellRef standing = Tall.FromIndex(colony.Pawns.Pawns.All[0].Cell);
            var inTheAir = new CellRef(standing.X, standing.Z, Tall.SizeY - 1);
            int before = WoodOnBoard(colony);

            colony.World.Intents.Submit(new Intent(IntentKind.GiveResource, inTheAir, ItemIndex.Wood, 50));
            colony.World.Tick();

            Assert.That(colony.World.Intents.Rejected, Is.Empty);
            Assert.That(WoodOnBoard(colony), Is.EqualTo(before + 50));
        }

        [Test]
        public void SpawnedPawnsAreDistinctColonistsNotOneReusedId()
        {
            ColonyWorld colony = ColonyWorld.Build(Size, seed: 4u, Colony(1));
            CellRef at = Size.FromIndex(colony.Pawns.Pawns.All[0].Cell);

            colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, at));
            colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, at));
            colony.World.Tick();

            var ids = colony.Pawns.Pawns.All;
            Assert.That(ids[ids.Count - 1].Id.Value, Is.Not.EqualTo(ids[ids.Count - 2].Id.Value));
        }

        [Test]
        public void GiveResourceAddsStockAndRefusesABadDefOrANonPositiveAmount()
        {
            ColonyWorld colony = ColonyWorld.Build(Size, seed: 5u, Colony(1));
            CellRef at = Size.FromIndex(colony.Pawns.Pawns.All[0].Cell);
            int before = WoodOnBoard(colony);

            colony.World.Intents.Submit(new Intent(IntentKind.GiveResource, at, ItemIndex.Wood, 50));
            colony.World.Intents.Submit(new Intent(IntentKind.GiveResource, at, 999_999, 50));
            colony.World.Intents.Submit(new Intent(IntentKind.GiveResource, at, ItemIndex.Wood, 0));
            colony.World.Tick();

            Assert.That(WoodOnBoard(colony), Is.EqualTo(before + 50));
            Assert.That(colony.World.Intents.Rejected, Has.Count.EqualTo(2));
            Assert.That(colony.World.Intents.Rejected[0].Reason, Is.EqualTo(IntentRejection.OutOfBounds));
            Assert.That(colony.World.Intents.Rejected[1].Reason, Is.EqualTo(IntentRejection.NotPermitted));
        }

        static int WoodOnBoard(ColonyWorld colony)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == ItemIndex.Wood) total += items[i].Stack;
            return total;
        }
    }
}
