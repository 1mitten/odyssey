#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Growing;
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
        [Test]
        public void RipenBringsEveryStandingCropToRipeAndSaysSoWhenNothingStands()
        {
            ColonyWorld colony = ColonyWorld.Build(Size, seed: 6u, Colony(1));
            var zones = colony.Growing!;
            CellRef at = new CellRef(colony.Start.X, colony.Start.Z, colony.Start.Y);

            // Nothing stands yet: the row must be refused for the reason it gives, not succeed
            // quietly, or a tester on an empty board cannot tell a no-op from a bug.
            colony.World.Intents.Submit(new Intent(IntentKind.DebugRipen, at));
            colony.World.Tick();
            Assert.That(colony.World.Intents.Rejected[0].Reason,
                Is.EqualTo(IntentRejection.AlreadyInThatState));

            // A painted, sown cell, then the ripen: the same end state the growth system would
            // have reached — ripeness — without the four days of daylight windows.
            Assert.That(zones.Designate(at, PlantHandle.Carrot), Is.EqualTo(IntentRejection.None));
            int index = Size.Index(at);
            zones.Sow(index);
            colony.World.Intents.Submit(new Intent(IntentKind.DebugRipen, at));
            colony.World.Tick();
            Assert.That(colony.World.Intents.Rejected, Has.Count.EqualTo(1),
                "the empty-board refusal above is still the only rejection: the ripen itself landed");
            Assert.That(zones.IsRipe(index),
                "the debug ripen writes the growth system's own end state");
        }
    }
}
