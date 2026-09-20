#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>Forbidding takes a thing out of every scan without moving it, and allowing gives it back.</summary>
    public class ForbidIntentTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 6);

        static ScenarioDef Colony(int colonists)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = colonists;

            // **A zone with room to spare, because this test is about forbidding and not about
            // capacity.** The default nine cells against eight salvage and twelve meal piles is a
            // knife edge: whether a salvage crate can move depends on whether a meal got the last
            // free cell first, which is a race this test has no opinion about. It went over the
            // edge on 2026-09-20, when starting beds became real furniture and moved the
            // placement by a cell — the colony hauled perfectly well and filled its last three
            // cells with rations, and the assertion below read that as "forbidding is broken".
            scenario.stockpileCells = 24;
            return scenario;
        }

        [Test]
        public void ForbidFlipsTheFlagAndRefusesWhatMakesNoSense()
        {
            ColonyWorld colony = ColonyWorld.Build(Size, seed: 1u, Colony(2));
            var items = colony.Pawns.Items.Items;
            Assume.That(items.Count, Is.GreaterThan(0));
            ThingId thing = items[0].Id;

            colony.World.Intents.Submit(new Intent(IntentKind.SetForbidden, a: thing.Value, b: 1));
            colony.World.Intents.Submit(new Intent(IntentKind.SetForbidden, a: thing.Value, b: 1));
            colony.World.Intents.Submit(new Intent(IntentKind.SetForbidden, a: 999_999, b: 1));
            colony.World.Tick();

            Assert.That(items[0].Forbidden, Is.True);
            Assert.That(colony.World.Intents.Rejected, Has.Count.EqualTo(2));
            Assert.That(colony.World.Intents.Rejected[0].Reason, Is.EqualTo(IntentRejection.AlreadyInThatState));
            Assert.That(colony.World.Intents.Rejected[1].Reason, Is.EqualTo(IntentRejection.OutOfBounds));

            colony.World.Intents.Submit(new Intent(IntentKind.SetForbidden, a: thing.Value, b: 0));
            colony.World.Tick();
            Assert.That(items[0].Forbidden, Is.False);
        }

        [Test]
        public void AForbiddenThingIsNotHauledUntilAllowed()
        {
            ColonyWorld colony = ColonyWorld.Build(Size, seed: 2u, Colony(3));
            var items = colony.Pawns.Items.Items;
            // The scenario scatters salvage over the start spots, some of which are stockpile
            // cells, so the baseline is whatever already lies in the zone before anyone moves.
            int before = Stocked(colony);

            // Forbid every loose thing, then wait: no salvage may move into the stockpile. Salvage
            // is the measure because the scenario starts it loose, whereas rations start stocked.
            for (int i = 0; i < items.Count; i++)
                colony.World.Intents.Submit(new Intent(IntentKind.SetForbidden, a: items[i].Id.Value, b: 1));
            colony.World.Tick(3_000);
            Assert.That(Stocked(colony), Is.EqualTo(before), "a forbidden thing stays where it lies");

            for (int i = 0; i < items.Count; i++)
                colony.World.Intents.Submit(new Intent(IntentKind.SetForbidden, a: items[i].Id.Value, b: 0));
            colony.World.Tick(6_000);
            Assert.That(Stocked(colony), Is.GreaterThan(before), "allowed again, it is hauled");
        }

        static int Stocked(ColonyWorld colony)
        {
            int stocked = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == ItemIndex.Salvage && items[i].Cell >= 0 &&
                    colony.Pawns.Items.IsStockpileCell(items[i].Cell)) stocked++;
            return stocked;
        }
    }
}
