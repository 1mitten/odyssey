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

            // **The measure is how much is still lying loose, not how much salvage reached the
            // zone.** It counted stocked salvage until 2026-09-18, and that made it a race for
            // scarce shelf space rather than a test of forbidding: measured on this very fixture,
            // the stockpile has nine cells of which **three** are free, and only **two** loose
            // salvage exist — so the assertion turned on whether salvage or rations happened to
            // win those three slots. Eight-connected movement changed `PawnContext.Distance`,
            // rations became the nearer haul, they took all three cells, and the test failed with
            // the colony hauling perfectly well and rather more of it. Hauling was traced to be
            // sure before this was touched: jobs given, items carried for 719 ticks, the driver
            // reaching its last toil, and not one path dropped.
            int before = Loose(colony);
            Assume.That(before, Is.GreaterThan(0), "something has to be lying about for this to mean anything");

            // Forbid every loose thing, then wait: nothing may be picked up at all.
            for (int i = 0; i < items.Count; i++)
                colony.World.Intents.Submit(new Intent(IntentKind.SetForbidden, a: items[i].Id.Value, b: 1));
            colony.World.Tick(3_000);
            Assert.That(Loose(colony), Is.EqualTo(before), "a forbidden thing stays where it lies");

            for (int i = 0; i < items.Count; i++)
                colony.World.Intents.Submit(new Intent(IntentKind.SetForbidden, a: items[i].Id.Value, b: 0));
            colony.World.Tick(6_000);
            Assert.That(Loose(colony), Is.LessThan(before), "allowed again, it is hauled");
        }

        /// <summary>
        /// Things lying outside a stockpile. Falls when anything is hauled, whatever its kind and
        /// whichever shelf it lands on, which is the property this file is about.
        /// </summary>
        static int Loose(ColonyWorld colony)
        {
            int loose = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].Cell >= 0 &&
                    !colony.Pawns.Items.IsStockpileCell(items[i].Cell)) loose++;
            return loose;
        }
    }
}
