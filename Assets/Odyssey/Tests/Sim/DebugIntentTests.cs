#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>The debug menu's two intents: spawn a colonist, and grant a resource.</summary>
    public class DebugIntentTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 6);

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
