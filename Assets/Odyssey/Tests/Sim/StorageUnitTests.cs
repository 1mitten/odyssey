#nullable enable
using System.IO;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Storage;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The shelf as a <b>store</b>: what it accepts, how much it holds, and what happens to what is
    /// in it when it goes.
    ///
    /// <para>The shelf as a <i>building</i> — that it can be ordered, costs wood, and is refused on
    /// a cell holding a stack — rides on the pipeline every other buildable uses and is covered
    /// where that pipeline is. What is new, and what these tests are for, is that a raised building
    /// can be a place to put things.</para>
    /// </summary>
    public class StorageUnitTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 8);
        const uint Seed = 20260921;

        const int Wood = ItemIndex.Wood;
        const int Meal = ItemIndex.Meal;

        static ColonyWorld Fresh()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 0;
            return ColonyWorld.Build(Size, Seed, scenario);
        }

        /// <summary>A cell near the start that a shelf may stand in, with nothing claiming it.</summary>
        static int OpenCell(ColonyWorld colony, int skip = 0)
        {
            CellRef start = colony.Start;
            for (int radius = 1; radius < 12; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!Size.Contains(x, z, start.Y)) continue;

                int cell = Size.Index(x, z, start.Y);
                if (!colony.Construction.Allows(cell, BuildingHandle.Shelf)) continue;
                if (skip-- > 0) continue;
                return cell;
            }

            return -1;
        }

        /// <summary>Order a shelf and finish it in one go — the pipeline is not what is under test.</summary>
        static StorageUnit RaiseShelf(ColonyWorld colony, int cell)
        {
            Assume.That(colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Shelf, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None));
            colony.Construction.Raise(colony.Pawns, cell);

            StorageUnit? unit = colony.Pawns.StorageUnits!.AtCell(cell);
            Assert.That(unit, Is.Not.Null, "a raised shelf is a store");
            return unit!;
        }

        /// <summary>Put a stack straight into a shelf, through the route a hauler takes.</summary>
        static void Stow(ColonyWorld colony, StorageUnit unit, int defIndex, int stack, int from)
        {
            ColonyItems items = colony.Pawns.Items;
            ColonyItem thing = items.Get(items.Spawn(defIndex, from, stack))!;
            items.PickUp(thing, new PawnId(1));
            items.PutIn(thing, StorageUnits.ContainerIdOf(unit.Edifice));
        }

        // ---------------------------------------------------------------- what it is

        [Test]
        public void ARaisedShelfIsAStoreAtPreferredAcceptingEverything()
        {
            // Preferred and not Normal, and it is a decision rather than a default: a zone is
            // Normal, two stores at one rung never re-stow between them, and a shelf built inside
            // a warehouse that did nothing until its priority was raised by hand would read, quite
            // reasonably, as a shelf that does not work.
            ColonyWorld colony = Fresh();
            int cell = OpenCell(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            StorageUnit unit = RaiseShelf(colony, cell);
            StorageUnits units = colony.Pawns.StorageUnits!;

            Assert.That(units.PriorityOf(unit), Is.EqualTo(StoragePriority.Preferred));
            Assert.That(units.Accepts(unit, Wood), Is.True);
            Assert.That(units.Accepts(unit, Meal), Is.True);
            Assert.That(units.SettingsOf(unit).AllowUnknown, Is.True,
                "Everything means a commodity added later too");
            Assert.That(unit.Slots, Is.EqualTo(8), "off the def, and copied rather than read back live");
            Assert.That(units.IsEmpty(unit), Is.True);
        }

        [Test]
        public void AShelfHoldsItsSlotsAndRefusesTheNext()
        {
            ColonyWorld colony = Fresh();
            int cell = OpenCell(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));
            StorageUnit unit = RaiseShelf(colony, cell);
            StorageUnits units = colony.Pawns.StorageUnits!;

            // Every def the colony has, which is fewer than eight, then the rest of the slots with
            // nothing left to fill them: what is under test is the slot count, so the last word is
            // the refusal once the slots are gone.
            int loaded = 0;
            for (int def = 0; def < colony.Pawns.Content.Items.Length && loaded < unit.Slots; def++)
            {
                if (!units.HasSpaceFor(unit, def, 1)) continue;
                Stow(colony, unit, def, 1, FreeGround(colony, loaded));
                loaded++;
            }

            Assert.That(units.StacksIn(unit), Is.EqualTo(loaded));
            Assert.That(loaded, Is.LessThanOrEqualTo(unit.Slots));
            Assert.That(units.HasSpaceFor(unit, Wood, 1), Is.True,
                "wood is already in there, so it merges rather than wanting a slot");
        }

        [Test]
        public void AFullSlotSetRefusesADefItDoesNotAlreadyHold()
        {
            ColonyWorld colony = Fresh();
            int cell = OpenCell(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));
            StorageUnit unit = RaiseShelf(colony, cell);
            StorageUnits units = colony.Pawns.StorageUnits!;

            // One slot, so the shelf is full after one stack and the question is sharp.
            unit.Slots = 1;
            Stow(colony, unit, Wood, 10, FreeGround(colony, 0));

            Assert.That(units.HasSpaceFor(unit, Wood, 10), Is.True, "it merges");
            Assert.That(units.HasSpaceFor(unit, Meal, 1), Is.False, "and there is no slot for anything else");
        }

        [Test]
        public void AShelfTakesAWholeLoadOrNothing()
        {
            ColonyWorld colony = Fresh();
            int cell = OpenCell(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));
            StorageUnit unit = RaiseShelf(colony, cell);
            StorageUnits units = colony.Pawns.StorageUnits!;

            int limit = colony.Pawns.Content.Items[Wood].stackLimit;
            unit.Slots = 1;
            Stow(colony, unit, Wood, limit - 5, FreeGround(colony, 0));

            Assert.That(units.HasSpaceFor(unit, Wood, 5), Is.True, "exactly the room left");
            Assert.That(units.HasSpaceFor(unit, Wood, 6), Is.False,
                "a hauler never splits a stack, so a partial fit is not space");
        }

        // ---------------------------------------------------------------- where it sits

        [Test]
        public void AShelfsCellLeavesWhateverZoneHeldIt()
        {
            // No cell is ever in two stores. A shelf carries its own filter and its own rung, and a
            // cell with two answers to "what goes here" is exactly the fault the zones' own anchor
            // rule exists to prevent. The zone gets a notch, as it already does around a tree.
            ColonyWorld colony = Fresh();
            int cell = OpenCell(colony);
            int neighbour = OpenCell(colony, skip: 1);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));
            Assume.That(neighbour, Is.GreaterThanOrEqualTo(0));

            StorageZones zones = colony.Pawns.Storage!;
            Assume.That(zones.Designate(Size.FromIndex(cell), cell, StoragePreset.Everything),
                Is.EqualTo(IntentRejection.None));
            Assume.That(zones.Designate(Size.FromIndex(neighbour), cell, StoragePreset.Everything),
                Is.EqualTo(IntentRejection.None));
            Assume.That(zones.ZoneAt(cell), Is.GreaterThanOrEqualTo(0));

            RaiseShelf(colony, cell);

            Assert.That(zones.ZoneAt(cell), Is.EqualTo(-1), "the shelf's cell is out of the zone");
            Assert.That(zones.ZoneAt(neighbour), Is.GreaterThanOrEqualTo(0), "and the rest of it stands");
        }

        [Test]
        public void AZoneCannotBePaintedOverAShelf()
        {
            // The other direction, and it was already closed: SiteAllows refuses a cell with an
            // edifice in it. Asserted rather than assumed, because the two halves together are what
            // make "no cell is in two stores" true by construction rather than by vigilance.
            ColonyWorld colony = Fresh();
            int cell = OpenCell(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));
            RaiseShelf(colony, cell);

            Assert.That(colony.Pawns.Storage!.SiteAllows(cell), Is.False);
        }

        [Test]
        public void AShelfsCellRefusesALooseStack()
        {
            // What is at a shelf's cell is in the shelf. The same needsClearCell rule a bed uses,
            // and the reason a shelf never has to argue with the one-stack-per-cell rule.
            ColonyWorld colony = Fresh();
            int cell = OpenCell(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));
            RaiseShelf(colony, cell);

            Assert.That(colony.Pawns.Items.CellHasSpace(cell), Is.False);
            Assert.That(colony.Pawns.Items.CellHasSpace(cell, Wood, 1), Is.False);
        }

        // ---------------------------------------------------------------- the one control

        [Test]
        public void TheStorageSettingsResolverAnswersForAShelfAsWellAsAZone()
        {
            // This is what makes the priority ladder and the filter the player already knows drive
            // a shelf with no second control anywhere: both storage intents resolve a cell through
            // SettingsAt, and SettingsAt now answers for either kind of store.
            ColonyWorld colony = Fresh();
            int cell = OpenCell(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));
            StorageUnit unit = RaiseShelf(colony, cell);

            StorageZones zones = colony.Pawns.Storage!;
            StorageSettings? settings = zones.SettingsAt(cell);

            Assert.That(settings, Is.Not.Null, "a shelf's cell is a store");
            Assert.That(settings, Is.SameAs(colony.Pawns.StorageUnits!.SettingsOf(unit)),
                "and it is the shelf's own record, not a copy");
        }

        [Test]
        public void AShelfIsEmptyingOnlyWhileItIsOrderedTakenApart()
        {
            // Derived from the standing order rather than kept as a flag, so it cannot disagree
            // with the order the player can see — and cancelling un-empties it for free.
            ColonyWorld colony = Fresh();
            int cell = OpenCell(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));
            StorageUnit unit = RaiseShelf(colony, cell);
            StorageUnits units = colony.Pawns.StorageUnits!;
            DesignationGrid orders = colony.Pawns.Designations!;

            Assert.That(units.IsEmptying(unit), Is.False);

            Assume.That(orders.Designate(Size.FromIndex(cell), DesignationKind.Deconstruct),
                Is.EqualTo(IntentRejection.None));
            Assert.That(units.IsEmptying(unit), Is.True);

            orders.Clear(cell);
            Assert.That(units.IsEmptying(unit), Is.False, "cancelling the order un-empties it");
        }

        // ---------------------------------------------------------------- coming apart

        [Test]
        public void DemolishingAShelfSpillsWhatTheBoardWillTake()
        {
            ColonyWorld colony = Fresh();
            int cell = OpenCell(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));
            StorageUnit unit = RaiseShelf(colony, cell);
            Stow(colony, unit, Wood, 30, FreeGround(colony, 0));
            Stow(colony, unit, Meal, 4, FreeGround(colony, 1));

            Assume.That(colony.Pawns.StorageUnits!.CanSpillAll(colony.Pawns, unit), Is.True,
                "an open board has somewhere to put two stacks");

            colony.Construction.Demolish(colony.Pawns, cell, out _);

            Assert.That(colony.Pawns.Items.ContainedItems, Is.Empty, "nothing is left in a shelf that is gone");
            Assert.That(LiveStackOf(colony, Wood), Is.EqualTo(30), "and nothing was lost with it");
            Assert.That(LiveStackOf(colony, Meal), Is.GreaterThanOrEqualTo(4));
            Assert.That(colony.Pawns.StorageUnits!.AtCell(cell), Is.Null, "and the store is gone with it");
        }

        [Test]
        public void AShelfAndItsContentsComeBackFromASave()
        {
            ColonyWorld colony = Fresh();
            int cell = OpenCell(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));
            StorageUnit unit = RaiseShelf(colony, cell);
            colony.Pawns.StorageUnits!.SettingsOf(unit).Priority = StoragePriority.Urgent;
            colony.Pawns.StorageUnits!.SettingsOf(unit).SetDef(Meal, false);
            Stow(colony, unit, Wood, 30, FreeGround(colony, 0));

            // Settle the save side the way a load settles the other one. Raising an edifice marks
            // support dirty and leaves the solve to the next tick, so a colony hashed the instant
            // after a hand-raised shelf is carrying stale support that a loaded colony would not —
            // a difference in the fixture, not in the file.
            colony.RebuildDerived();
            ulong before = colony.World.ComputeStateHash().Value;

            using var stream = new MemoryStream();
            WorldSave.Save(colony.World, stream, colony.SaveComponents);
            stream.Position = 0;

            ColonyWorld fresh = Fresh();
            WorldSave.Load(fresh.World, stream, fresh.SaveComponents);
            fresh.RebuildDerived();

            Assert.That(fresh.World.ComputeStateHash().Value, Is.EqualTo(before),
                "a shelf and what is in it are the same state on both sides of a file");

            StorageUnit? back = fresh.Pawns.StorageUnits!.AtCell(cell);
            Assert.That(back, Is.Not.Null, "the shelf is still there");
            Assert.That(back!.Slots, Is.EqualTo(8));
            Assert.That(fresh.Pawns.StorageUnits!.PriorityOf(back), Is.EqualTo(StoragePriority.Urgent),
                "with the rung it was set to");
            Assert.That(fresh.Pawns.StorageUnits!.Accepts(back, Meal), Is.False, "and the filter it was given");
            Assert.That(fresh.Pawns.Items.ResidentIn(StorageUnits.ContainerIdOf(back.Edifice), Wood)!.Stack,
                Is.EqualTo(30), "and the wood is still on it");
        }

        // ---------------------------------------------------------------- helpers

        static int FreeGround(ColonyWorld colony, int nth)
        {
            CellRef start = colony.Start;
            int seen = 0;
            for (int radius = 1; radius < 16; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!Size.Contains(x, z, start.Y)) continue;

                int cell = Size.Index(x, z, start.Y);
                if (!colony.Pawns.Cells.IsWalkable(cell)) continue;
                if (!colony.Pawns.Items.CellHasSpace(cell)) continue;
                if (seen++ != nth) continue;
                return cell;
            }

            return -1;
        }

        static int LiveStackOf(ColonyWorld colony, int defIndex)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == defIndex) total += items[i].Stack;
            return total;
        }
    }
}
