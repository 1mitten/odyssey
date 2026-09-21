#nullable enable
using System.IO;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Storage;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Stacking, "has space" and re-stowing, after <c>docs/research/a-14-bills-stockpiles-inventory.md</c>
    /// §3 and §4: a stack occupies one cell, a cell has space for a load only when the whole load
    /// fits, and the destination is chosen by filter, then space, then priority, then distance.
    /// </summary>
    public class StockpileTests
    {
        const int Wood = ItemIndex.Wood;
        const int Meal = ItemIndex.Meal;

        static int WoodLimit(Colony colony) => colony.Ctx.Content.Items[Wood].stackLimit;

        static int LiveStackOf(Colony colony, int defIndex)
        {
            int total = 0;
            var items = colony.Ctx.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == defIndex) total += items[i].Stack;
            return total;
        }

        static int LiveCountOf(Colony colony, int defIndex)
        {
            int count = 0;
            var items = colony.Ctx.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == defIndex) count++;
            return count;
        }

        /// <summary>A zone that takes only the defs named, at the cells given.</summary>
        static StorageSettings Pile(Colony colony, int priority, int[] cells, params int[] accepts)
        {
            StorageSettings settings = colony.Stockpile(priority, cells);
            settings.ApplyPreset(StoragePreset.Nothing);
            for (int i = 0; i < accepts.Length; i++) settings.SetDef(accepts[i], true);
            return settings;
        }

        // ---------------------------------------------------------------- has space

        [Test]
        public void AnEmptyCellIsSpaceAndACellHoldingAnotherDefIsNot()
        {
            var colony = Colony.Build();
            int cell = colony.Cell(5, 5, 0);
            Assert.That(colony.Ctx.Items.CellHasSpace(cell, Wood, 1), Is.True, "an empty cell takes anything");

            colony.Ctx.Items.Spawn(Meal, cell);

            Assert.That(colony.Ctx.Items.CellHasSpace(cell, Wood, 1), Is.False, "a meal is in the way of wood");
            Assert.That(colony.Ctx.Items.CellHasSpace(cell), Is.False, "the cell is not empty");
        }

        [Test]
        public void APartialStackIsSpaceOnlyWhenTheWholeLoadFits()
        {
            // The research leaves "space for a partial stack" open (a-14, could not be
            // determined). The rule here is the strict one: the load goes in whole or it does not
            // go, so a hauler never has to split a stack or drop a surplus.
            var colony = Colony.Build();
            int limit = WoodLimit(colony);
            Assume.That(limit, Is.GreaterThan(1), "wood must stack for this test to mean anything");

            int cell = colony.Cell(5, 5, 0);
            colony.Ctx.Items.Spawn(Wood, cell, stack: limit - 25);

            Assert.That(colony.Ctx.Items.CellHasSpace(cell, Wood, 25), Is.True, "exactly fills the stack");
            Assert.That(colony.Ctx.Items.CellHasSpace(cell, Wood, 26), Is.False, "one over the limit");
        }

        [Test]
        public void AFullStackIsNotSpace()
        {
            var colony = Colony.Build();
            int cell = colony.Cell(5, 5, 0);
            colony.Ctx.Items.Spawn(Wood, cell, stack: WoodLimit(colony));

            Assert.That(colony.Ctx.Items.CellHasSpace(cell, Wood, 1), Is.False);
        }

        [Test]
        public void SpawningOntoAStackOfTheSameDefMergesIntoIt()
        {
            // Two trees felled beside each other leave one pile of wood, not two things in one
            // cell. The second spawn returns the id of the stack it joined, and the cell index
            // still points at exactly one thing.
            var colony = Colony.Build();
            int cell = colony.Cell(5, 5, 0);
            ThingId first = colony.Ctx.Items.Spawn(Wood, cell, stack: 20);
            ThingId second = colony.Ctx.Items.Spawn(Wood, cell, stack: 20);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(colony.Ctx.Items.Get(first)!.Stack, Is.EqualTo(40));
            Assert.That(LiveCountOf(colony, Wood), Is.EqualTo(1));
        }

        [Test]
        public void SpawningOntoACellThatCannotTakeTheLoadIsAnError()
        {
            // Silently overwriting the cell index was how two things came to share a cell. A
            // caller that has not checked for space has a bug, and the bug should be loud.
            var colony = Colony.Build();
            int cell = colony.Cell(5, 5, 0);
            colony.Ctx.Items.Spawn(Meal, cell);

            Assert.Throws<System.InvalidOperationException>(() => colony.Ctx.Items.Spawn(Wood, cell, stack: 1));
            Assert.That(LiveCountOf(colony, Wood), Is.Zero);
        }

        [Test]
        public void MealsStackToAWholeStartingPile()
        {
            // A starting pile is one full stack, so the pantry a scenario lays out is legal
            // stock and a hauler moves a pile in one trip (a-14: one stack per trip). A limit
            // of 1 — the ItemDef default — would make every pile of twenty "full" for hauling
            // and no meal could ever be stowed beside another.
            var content = ContentPack.Pawns();
            Assert.That(content.Items[Meal].stackLimit, Is.GreaterThan(1));
            Assert.That(content.Items[Meal].stackLimit, Is.GreaterThanOrEqualTo(ScenarioDef.Bare().mealsPerPile));
            Assert.That(content.Items[Meal].stackLimit, Is.GreaterThanOrEqualTo(ScenarioDef.Playtest().mealsPerPile));
        }

        // ---------------------------------------------------------------- hauling into a stack

        [Test]
        public void HaulingMergesIntoAStackOfTheSameDef()
        {
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            int store = colony.Cell(12, 12, 0);
            colony.Stockpile(1, store);
            ThingId stack = colony.Ctx.Items.Spawn(Wood, store, stack: 30);
            ThingId loose = colony.Ctx.Items.Spawn(Wood, colony.Cell(6, 4, 0), stack: 20);

            for (int i = 0; i < 5_000 && colony.Ctx.Items.Get(loose) != null; i++) colony.World.Tick();

            Assert.That(colony.Ctx.Items.Get(loose), Is.Null, "the hauled thing joined the stack and is gone");
            Assert.That(colony.Ctx.Items.Get(stack)!.Stack, Is.EqualTo(50));
            Assert.That(colony.Ctx.Items.ItemAt(store)!.Id, Is.EqualTo(stack));
            Assert.That(LiveStackOf(colony, Wood), Is.EqualTo(50), "no wood was created or lost");
            Assert.That(LiveCountOf(colony, Wood), Is.EqualTo(1));
            Assert.That(colony.Ctx.Items.LooseItems, Is.Empty);
            Assert.That(pawn.HeldReservations, Is.Empty);
        }

        [Test]
        public void TheDestinationRuleIsFilterThenSpaceThenPriorityThenNearest()
        {
            // Four piles, arranged so that dropping any one of the four rules picks a different
            // pile. Only all four together choose the far, middling pile that accepts wood and
            // has room on its stack for the load. Both occupied piles hold partial stacks, so
            // the old rule — occupied means full — would send the load to the nearest empty
            // cell instead, and this test is what tells the two rules apart.
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            int origin = colony.Cell(4, 2, 0);
            int limit = WoodLimit(colony);
            const int Load = 20;

            int filtered = colony.Cell(5, 2, 0);   // nearest, highest priority, empty — but meals only
            int nearest = colony.Cell(6, 2, 0);    // accepts wood, empty, lowest priority
            int full = colony.Cell(8, 2, 0);       // accepts wood, higher priority, but the load does not fit
            int winner = colony.Cell(12, 12, 0);   // accepts wood, the load fits, farthest

            // The four rungs are named now, and the numbers matter only in their order: the
            // filtered pile outranks everything and refuses wood, the full one outranks the
            // winner and has no room, and the nearest is the worst rung there is.
            Pile(colony, StoragePriority.Urgent, new[] { filtered }, Meal);
            Pile(colony, StoragePriority.Last, new[] { nearest }, Wood);
            Pile(colony, StoragePriority.Preferred, new[] { full }, Wood);
            Pile(colony, StoragePriority.Normal, new[] { winner }, Wood);
            colony.Ctx.Items.Spawn(Wood, full, stack: limit - Load + 1);
            colony.Ctx.Items.Spawn(Wood, winner, stack: limit - Load);

            // The fixture checks its own premise, so a change to the distance measure cannot
            // quietly turn this into a test of nothing.
            int Distance(int to) => colony.Ctx.Distance(origin, to);
            Assert.That(Distance(filtered), Is.LessThan(Distance(nearest)));
            Assert.That(Distance(nearest), Is.LessThan(Distance(full)));
            Assert.That(Distance(full), Is.LessThan(Distance(winner)));

            colony.Ctx.Items.Spawn(Wood, origin, stack: Load);
            colony.World.Tick(3);

            Assert.That(PawnReservationTests.IsHauling(colony, pawn), Is.True);
            Assert.That(pawn.CurrentJob!.DestCell, Is.EqualTo(winner),
                "ignoring the filter picks the meals-only pile, ignoring space picks the full one, " +
                "ignoring priority picks the nearest, and nothing else is left");
        }

        // ---------------------------------------------------------------- re-stowing

        [Test]
        public void AHaulerRestowsFromALowerPriorityPileIntoAHigherOne()
        {
            // The "small Important pile beside the workbench, fed from the Normal warehouse"
            // behaviour a-14 calls the most important emergent behaviour in the system.
            var colony = Colony.Build();
            colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            int low = colony.Cell(6, 6, 0);
            int high = colony.Cell(10, 10, 0);
            colony.Stockpile(1, low);
            colony.Stockpile(3, high);
            ThingId scrap = colony.Ctx.Items.Spawn(ItemIndex.Salvage, low);
            Assert.That(colony.Ctx.Items.LooseItems, Is.Empty, "a thing in a pile is stored, not loose");

            for (int i = 0; i < 5_000 && colony.Ctx.Items.ItemAt(high) == null; i++) colony.World.Tick();

            Assert.That(colony.Ctx.Items.Get(scrap)!.Cell, Is.EqualTo(high));
            Assert.That(colony.Ctx.Items.ItemAt(low), Is.Null);
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Haul), Is.EqualTo(1));
        }

        [Test]
        public void NothingIsRestowedBetweenPilesOfEqualPriority()
        {
            // Two piles at the same priority are one warehouse in two places. Shuttling between
            // them would be the endless up-and-down-stairs failure a-14 warns of.
            var colony = Colony.Build();
            colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            int here = colony.Cell(6, 6, 0);
            int there = colony.Cell(10, 10, 0);
            colony.Stockpile(2, here);
            colony.Stockpile(2, there);
            ThingId scrap = colony.Ctx.Items.Spawn(ItemIndex.Salvage, here);

            colony.World.Tick(2_000);

            Assert.That(colony.Ctx.Items.Get(scrap)!.Cell, Is.EqualTo(here));
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Haul), Is.Zero);
        }

        [Test]
        public void LooseThingsAreHauledBeforeAnythingIsRestowed()
        {
            // Tidying is the lowest job there is (a-14 §3): a thing lying on the ground beats a
            // better home for a thing already stored, however far away the loose one is.
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            int low = colony.Cell(3, 2, 0);
            colony.Stockpile(1, low);
            colony.Stockpile(3, colony.Cell(14, 14, 0), colony.Cell(15, 15, 0));
            colony.Ctx.Items.Spawn(ItemIndex.Salvage, low);
            ThingId loose = colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(12, 2, 0));

            colony.World.Tick(3);

            Assert.That(PawnReservationTests.IsHauling(colony, pawn), Is.True);
            Assert.That(pawn.CurrentJob!.TargetItem, Is.EqualTo(loose));
        }

        // ---------------------------------------------------------------- what a store refuses

        // A store's filter used to be a rule about what could be carried *in*, and nothing at
        // all about what was already lying there. Paint a store over a rock, or narrow a store
        // that has one in it, and the rock stayed for ever: no store would accept it, so the
        // haul scan skipped it, and its cell was lost to the store for the rest of the game.
        //
        // The owner reported both halves of that on 2026-09-21 - "the colonists left the rocks
        // already there and left the meals not hauled" - and they are one fault, because a cell
        // holding the wrong thing has no space for the right one.

        [Test]
        public void AThingAStoreRefusesIsCarriedOutOfIt()
        {
            var colony = Colony.Build();
            colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            int a = colony.Cell(6, 6, 0);
            int b = colony.Cell(7, 6, 0);
            Pile(colony, StoragePriority.Normal, new[] { a, b }, Meal);

            ThingId rock = colony.Ctx.Items.Spawn(ItemIndex.Stone, a);

            colony.World.Tick(5_000);

            ColonyItem carried = colony.Ctx.Items.Get(rock)!;
            Assert.That(colony.Storage.IsStorage(carried.Cell), Is.False,
                "a meals-only store is meals or nothing; the rock is still sitting in it");
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Haul), Is.EqualTo(1));
        }

        [Test]
        public void AStoreClearedOfWhatItRefusesThenTakesWhatItWants()
        {
            // The compound report. Both cells of the store are held by rocks it will not take,
            // so there is nowhere for the meal to go until they are gone - which is why the two
            // symptoms arrived together and why one fix answers both.
            var colony = Colony.Build();
            colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            int a = colony.Cell(6, 6, 0);
            int b = colony.Cell(7, 6, 0);
            Pile(colony, StoragePriority.Normal, new[] { a, b }, Meal);

            ThingId first = colony.Ctx.Items.Spawn(ItemIndex.Stone, a);
            ThingId second = colony.Ctx.Items.Spawn(ItemIndex.Stone, b);
            ThingId meal = colony.Ctx.Items.Spawn(Meal, colony.Cell(12, 2, 0));

            colony.World.Tick(20_000);

            Assert.That(colony.Storage.IsStorage(colony.Ctx.Items.Get(first)!.Cell), Is.False);
            Assert.That(colony.Storage.IsStorage(colony.Ctx.Items.Get(second)!.Cell), Is.False);
            Assert.That(colony.Storage.IsStorage(colony.Ctx.Items.Get(meal)!.Cell), Is.True,
                "the meal never reached the store the player emptied for it");
        }

        [Test]
        public void AThingCarriedOutOfOneStoreIsNotCarriedIntoAnotherThatRefusesItToo()
        {
            // Where the load is set down is asked of the *thing*, not only of the ground. The
            // predicate behind this knew about growing zones alone until 2026-09-21, so a rock
            // lifted off a field could be put down inside a meals-only store - recreating the
            // fault above from the other end, and out of the same line of code.
            var colony = Colony.Build();
            colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            int home = colony.Cell(6, 6, 0);
            Pile(colony, StoragePriority.Normal, new[] { home }, Meal);

            // A second meals-only store wrapped right round the first, so every cell the rock
            // could be set down in nearby belongs to a store that will not have it.
            var ring = new System.Collections.Generic.List<int>();
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                if (dx != 0 || dz != 0) ring.Add(colony.Cell(6 + dx, 6 + dz, 0));
            Pile(colony, StoragePriority.Normal, ring.ToArray(), Meal);

            ThingId rock = colony.Ctx.Items.Spawn(ItemIndex.Stone, home);

            colony.World.Tick(5_000);

            ColonyItem carried = colony.Ctx.Items.Get(rock)!;
            Assert.That(colony.Storage.IsStorage(carried.Cell), Is.False,
                "carried out of one store and straight into the next");
        }

        [Test]
        public void AThingAStoreRefusesGoesToAStoreThatWantsItRatherThanToTheGround()
        {
            // Open ground is the last answer, not the first: a store that accepts the thing is
            // a home, and the destination scan finds it before the clearance fallback is
            // reached. A rock evicted onto the grass beside a rock store would be an obvious
            // silliness and is the easiest way to get this fix wrong.
            var colony = Colony.Build();
            colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            int meals = colony.Cell(6, 6, 0);
            int rocks = colony.Cell(12, 12, 0);
            Pile(colony, StoragePriority.Normal, new[] { meals }, Meal);
            Pile(colony, StoragePriority.Normal, new[] { rocks }, ItemIndex.Stone);

            ThingId rock = colony.Ctx.Items.Spawn(ItemIndex.Stone, meals);

            colony.World.Tick(10_000);

            Assert.That(colony.Ctx.Items.Get(rock)!.Cell, Is.EqualTo(rocks),
                "there was a store that wanted it and it was dumped on the ground instead");
        }

        [Test]
        public void ClearingAStoreOfWhatItRefusesBeatsTidyingAndThenStops()
        {
            // Two claims in one run, because the second only means anything if the first holds.
            //
            // A refused thing is scanned with the loose things and not with the re-stowing. That
            // is the whole of why the bug outlived its own diagnosis: the tidying pass runs only
            // when nothing loose is waiting, so in a colony that is doing anything at all the
            // rock's turn never comes. Here a salvage crate sits in a Last pile with a Preferred
            // pile waiting for it - a textbook re-stow - and the rock still goes first.
            //
            // And once the rock is out it stays out: it is loose on ground no store wants, so
            // nothing picks it up again. A fix that evicted a thing and then hauled it back
            // would pass the test above and shuttle for ever.
            var colony = Colony.Build();
            colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            int meals = colony.Cell(4, 4, 0);
            Pile(colony, StoragePriority.Normal, new[] { meals }, Meal);
            colony.Stockpile(StoragePriority.Last, colony.Cell(10, 10, 0));
            colony.Stockpile(StoragePriority.Preferred, colony.Cell(14, 14, 0));
            colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(10, 10, 0));

            ThingId rock = colony.Ctx.Items.Spawn(ItemIndex.Stone, meals);

            colony.World.Tick(3);
            Assert.That(PawnReservationTests.IsHauling(colony, colony.Ctx.Pawns.All[0]), Is.True);
            Assert.That(colony.Ctx.Pawns.All[0].CurrentJob!.TargetItem, Is.EqualTo(rock),
                "the tidying was taken first and the store left blocked");

            colony.World.Tick(20_000);

            int settled = colony.Ctx.Items.Get(rock)!.Cell;
            Assert.That(settled, Is.Not.EqualTo(meals), "still in the store that refuses it");
            Assert.That(colony.Storage.Accepts(settled, ItemIndex.Stone), Is.True,
                "wherever it came to rest is somewhere that will have it");

            int hauls = colony.Jobs.CompletedOf(JobIndex.Haul);
            colony.World.Tick(20_000);
            Assert.That(colony.Ctx.Items.Get(rock)!.Cell, Is.EqualTo(settled), "the rock is being shuttled");
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Haul), Is.EqualTo(hauls),
                "something is still being carried about with nowhere to put it");
        }

        [Test]
        public void NarrowingAStoresFilterIsWhatSetsItsContentsMoving()
        {
            // The player's actual gesture: a store that has been happily holding stone is told
            // to take meals only. Nothing in the simulation is notified - the haul scan asks the
            // filter afresh every think - and that is the point of asserting it.
            var colony = Colony.Build();
            colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            int a = colony.Cell(6, 6, 0);
            int b = colony.Cell(7, 6, 0);
            StorageSettings settings = colony.Stockpile(StoragePriority.Normal, a, b);

            ThingId rock = colony.Ctx.Items.Spawn(ItemIndex.Stone, a);
            colony.World.Tick(2_000);
            Assert.That(colony.Ctx.Items.Get(rock)!.Cell, Is.EqualTo(a), "nothing to do while the store takes everything");

            settings.ApplyPreset(StoragePreset.Nothing);
            settings.SetDef(Meal, true);

            colony.World.Tick(5_000);

            Assert.That(colony.Storage.IsStorage(colony.Ctx.Items.Get(rock)!.Cell), Is.False);
        }

        [Test]
        public void OpenGroundIsNotAStoreThatRefusesTheThingButMayBeOneThatWantsIt()
        {
            // The rule behind both clearance cases, pinned on its own. It is asked of the thing
            // and not only of the cell, which is the half that was missing: the predicate knew
            // about growing zones alone, so "somewhere out of the way" could be a stockpile that
            // would never accept what was being set down there.
            var colony = Colony.Build();
            int meals = colony.Cell(6, 6, 0);
            int rocks = colony.Cell(8, 8, 0);
            int grass = colony.Cell(3, 3, 0);
            Pile(colony, StoragePriority.Normal, new[] { meals }, Meal);
            Pile(colony, StoragePriority.Normal, new[] { rocks }, ItemIndex.Stone);

            var forStone = colony.Ctx.OpenGroundFor(ItemIndex.Stone);
            Assert.That(forStone(grass), Is.True, "bare ground takes anything");
            Assert.That(forStone(meals), Is.False, "a store that refuses it is not somewhere to leave it");
            Assert.That(forStone(rocks), Is.True, "a store that wants it is a home, not an obstruction");

            var forMeals = colony.Ctx.OpenGroundFor(Meal);
            Assert.That(forMeals(meals), Is.True, "the same cell, the other way round");
            Assert.That(forMeals(rocks), Is.False);
        }

        // ---------------------------------------------------------------- failure and saving

        [Test]
        public void AFailedHaulPutsTheLoadDownSomewhereReal()
        {
            // A job that fails mid-carry must leave the thing on a cell that can take it. It used
            // to be dropped at the cell it was carried from — which is -1 while it is carried —
            // and the thing then existed nowhere: not on the ground, not in a pile, not in hand.
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            colony.Stockpile(1, colony.Cell(12, 12, 0));
            ThingId scrap = colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(6, 4, 0));

            for (int i = 0; i < 2_000 && colony.Ctx.Items.Get(scrap)!.CarriedBy == 0; i++) colony.World.Tick();
            Assert.That(colony.Ctx.Items.Get(scrap)!.CarriedBy, Is.EqualTo(pawn.Id.Value), "picked up");

            // Something else is standing where the pawn is, so the drop has to look further.
            colony.Ctx.Items.Spawn(Meal, pawn.Cell);
            colony.Jobs.EndJob(pawn, JobStatus.Failed);

            var item = colony.Ctx.Items.Get(scrap)!;
            Assert.That(item.CarriedBy, Is.Zero);
            Assert.That(item.Cell, Is.GreaterThanOrEqualTo(0), "the thing is somewhere");
            Assert.That(item.Cell, Is.Not.EqualTo(pawn.Cell), "not on top of the meal");
            Assert.That(colony.Ctx.Items.ItemAt(item.Cell)!.Id, Is.EqualTo(scrap), "and the index knows it");
            Assert.That(colony.Ctx.Items.LooseItems, Does.Contain(scrap.Value - 1), "and it will be hauled again");
        }

        [Test]
        public void StoredThingsAreRebuiltOnLoad()
        {
            // The stored lister is derived, like the loose one, and comes back from the cell
            // index on load. A lister that came back empty would leave a loaded warehouse
            // invisible to re-stowing until something was dropped in it.
            var colony = Colony.Build();
            int store = colony.Cell(12, 12, 0);
            colony.Stockpile(1, store);
            colony.Ctx.Items.Spawn(Wood, store, stack: 30);
            colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(3, 3, 0));
            Assert.That(colony.Ctx.Items.StoredItems, Has.Count.EqualTo(1), "the wood is stored");
            Assert.That(colony.Ctx.Items.LooseItems, Has.Count.EqualTo(1), "the salvage is loose");

            // Both sections, because the zones are their own now and the listers are derived from
            // the two together. Saving only the items and expecting the stored lister back is
            // asking a file to remember something it was never given.
            using var stream = new MemoryStream();
            WorldSave.Save(colony.World, stream, colony.SaveComponents);
            stream.Position = 0;

            var fresh = Colony.Build();
            WorldSave.Load(fresh.World, stream, fresh.SaveComponents);
            fresh.Storage.RebucketAll();

            Assert.That(fresh.Ctx.Items.StoredItems, Is.EqualTo(colony.Ctx.Items.StoredItems));
            Assert.That(fresh.Ctx.Items.LooseItems, Is.EqualTo(colony.Ctx.Items.LooseItems));
            Assert.That(fresh.Ctx.Items.ItemAt(store)!.Stack, Is.EqualTo(30));
        }
    }
}
