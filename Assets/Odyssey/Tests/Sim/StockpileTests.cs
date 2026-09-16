#nullable enable
using System.IO;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;

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

        /// <summary>A pile that takes only the defs named, at the cells given.</summary>
        static Stockpile Pile(Colony colony, int priority, int[] cells, params int[] accepts)
        {
            var allow = new bool[ItemIndex.Count];
            for (int i = 0; i < accepts.Length; i++) allow[accepts[i]] = true;
            var pile = new Stockpile(priority, cells, allow);
            colony.Ctx.Items.AddStockpile(pile);
            return pile;
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

            Pile(colony, 9, new[] { filtered }, Meal);
            Pile(colony, 1, new[] { nearest }, Wood);
            Pile(colony, 5, new[] { full }, Wood);
            Pile(colony, 3, new[] { winner }, Wood);
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

            using var stream = new MemoryStream();
            WorldSave.Save(colony.World, stream, new ISaveable[] { colony.Ctx.Items });
            stream.Position = 0;

            var fresh = Colony.Build();
            WorldSave.Load(fresh.World, stream, new ISaveable[] { fresh.Ctx.Items });

            Assert.That(fresh.Ctx.Items.StoredItems, Is.EqualTo(colony.Ctx.Items.StoredItems));
            Assert.That(fresh.Ctx.Items.LooseItems, Is.EqualTo(colony.Ctx.Items.LooseItems));
            Assert.That(fresh.Ctx.Items.ItemAt(store)!.Stack, Is.EqualTo(30));
        }
    }
}
