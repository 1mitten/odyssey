#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Storage;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A colony that actually hauls into and out of a shelf.
    ///
    /// <para><c>StorageUnitTests</c> asks what a shelf <i>is</i>; this asks whether the colony can
    /// use one. The difference matters because every part of the destination rule was generalised
    /// rather than copied — one walk returns a cell or a store — and a generalisation that compiles
    /// is not a generalisation that works.</para>
    ///
    /// <para><b>The evidence that it did not fork</b> is elsewhere and worth naming here:
    /// <c>StockpileTests</c> and <c>StorageZoneTests</c> pass unedited. If shelves had been given a
    /// path of their own, those would still be green and this file would still be green, and the
    /// two rules would drift apart from the next commit onwards.</para>
    /// </summary>
    public class ShelfHaulTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 8);
        const uint Seed = 20260921;
        const int Wood = ItemIndex.Wood;

        static ColonyWorld Fresh(int colonists = 1)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = 0;
            return ColonyWorld.Build(Size, Seed, scenario);
        }

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

        static StorageUnit RaiseShelf(ColonyWorld colony, int cell)
        {
            Assume.That(colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Shelf, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None));
            colony.Construction.Raise(colony.Pawns, cell);
            colony.RebuildDerived();

            StorageUnit? unit = colony.Pawns.StorageUnits!.AtCell(cell);
            Assume.That(unit, Is.Not.Null);
            return unit!;
        }

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

        static void Run(ColonyWorld colony, int ticks)
        {
            for (int i = 0; i < ticks; i++) colony.World.Tick();
        }

        [Test]
        public void AHaulerPutsALooseStackOnAShelf()
        {
            // The whole point of the unit, in one assertion: a colony with a shelf and something on
            // the floor puts the one into the other, through the same giver, the same driver and
            // the same lift a stockpile has always used.
            ColonyWorld colony = Fresh();
            int shelf = OpenCell(colony);
            Assume.That(shelf, Is.GreaterThanOrEqualTo(0));
            StorageUnit unit = RaiseShelf(colony, shelf);

            int ground = FreeGround(colony, 0);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));
            colony.Pawns.Items.Spawn(Wood, ground, 20);

            Run(colony, 4_000);

            // About the wood this test put down, not about how full the shelf is: a Bare scenario
            // still places a starting kit, and a colonist will cheerfully shelve that too.
            ColonyItem? shelved = colony.Pawns.Items.ResidentIn(
                StorageUnits.ContainerIdOf(unit.Edifice), Wood);
            Assert.That(shelved, Is.Not.Null, "the wood is on the shelf");
            Assert.That(shelved!.Stack, Is.EqualTo(20), "all of it, and none of it lost on the way");
            Assert.That(colony.Pawns.Items.ItemAt(ground), Is.Null, "and none of it left behind");
        }

        [Test]
        public void AShelfBeatsANearerZoneBecauseItIsPreferred()
        {
            // Priority dominates distance across *both kinds of store*, which is the thing that
            // would break first if the two had separate walks: the bands are walked high to low and
            // the first that yields wins, so a Preferred shelf takes the load off a Normal zone
            // standing right beside the stack.
            ColonyWorld colony = Fresh();
            int shelf = OpenCell(colony, skip: 6);
            Assume.That(shelf, Is.GreaterThanOrEqualTo(0));
            StorageUnit unit = RaiseShelf(colony, shelf);

            int ground = FreeGround(colony, 0);
            int nearZone = FreeGround(colony, 1);
            Assume.That(nearZone, Is.GreaterThanOrEqualTo(0));
            Assume.That(colony.Pawns.Storage!.Designate(Size.FromIndex(nearZone), nearZone, StoragePreset.Everything),
                Is.EqualTo(IntentRejection.None));
            Assume.That(colony.Pawns.Storage!.PriorityAt(nearZone), Is.EqualTo(StoragePriority.Normal));

            colony.Pawns.Items.Spawn(Wood, ground, 20);
            Run(colony, 4_000);

            Assert.That(colony.Pawns.Items.ResidentIn(StorageUnits.ContainerIdOf(unit.Edifice), Wood),
                Is.Not.Null, "the further Preferred shelf wins over the nearer Normal zone");

            ColonyItem? inTheZone = colony.Pawns.Items.ItemAt(nearZone);
            Assert.That(inTheZone == null || inTheZone.DefIndex != Wood, Is.True,
                "and the wood did not stop at the nearer Normal cell on the way past");
        }

        [Test]
        public void AnEmptyingShelfGivesUpItsContents()
        {
            // And it does so without the haul giver knowing what emptying means: an emptying shelf
            // ranks its contents below every real store, so every band beats them and the ordinary
            // re-stow rule carries them out. Here there is nowhere better, so they go on the floor.
            ColonyWorld colony = Fresh();
            int shelf = OpenCell(colony);
            Assume.That(shelf, Is.GreaterThanOrEqualTo(0));
            StorageUnit unit = RaiseShelf(colony, shelf);

            ColonyItems items = colony.Pawns.Items;
            ColonyItem wood = items.Get(items.Spawn(Wood, FreeGround(colony, 0), 20))!;
            items.PickUp(wood, new PawnId(1));
            items.PutIn(wood, StorageUnits.ContainerIdOf(unit.Edifice));
            Assume.That(colony.Pawns.StorageUnits!.StacksIn(unit), Is.EqualTo(1));

            Assume.That(colony.Pawns.Designations!.Designate(Size.FromIndex(shelf), DesignationKind.Deconstruct),
                Is.EqualTo(IntentRejection.None));

            int before = LiveStackOf(colony, Wood);

            // Watched tick by tick, and the assertion is that the shelf is empty **while it is
            // still standing**. Run blind to the end and this test passes either way: the colonist
            // goes on to finish the deconstruct, and Dissolve spills whatever is left — so a haul
            // path that did not work at all would look exactly like one that did.
            bool emptiedWhileStanding = false;
            for (int i = 0; i < 4_000 && !emptiedWhileStanding; i++)
            {
                colony.World.Tick();
                emptiedWhileStanding =
                    colony.Pawns.StorageUnits!.AtCell(shelf) != null
                    && colony.Pawns.Items.ResidentIn(StorageUnits.ContainerIdOf(unit.Edifice), Wood) == null;
            }

            Assert.That(emptiedWhileStanding, Is.True,
                "the shelf was emptied by ordinary hauling before it came down");
            // Not equal: the colonist goes on to finish the deconstruct, and a shelf is made of
            // wood, so the colony ends the run with its own timber back. What matters is that the
            // twenty that were on the shelf are still in the world.
            Assert.That(LiveStackOf(colony, Wood), Is.GreaterThanOrEqualTo(before),
                "nothing was lost emptying it");
        }

        [Test]
        public void AShelfIsNotADestinationWhileItIsEmptying()
        {
            // Or a shelf ordered taken apart would re-stow into itself for ever, and the colony
            // would look busy while nothing happened.
            ColonyWorld colony = Fresh();
            int shelf = OpenCell(colony);
            Assume.That(shelf, Is.GreaterThanOrEqualTo(0));
            StorageUnit unit = RaiseShelf(colony, shelf);
            Assume.That(colony.Pawns.Designations!.Designate(Size.FromIndex(shelf), DesignationKind.Deconstruct),
                Is.EqualTo(IntentRejection.None));

            int ground = FreeGround(colony, 0);
            colony.Pawns.Items.Spawn(Wood, ground, 20);

            Run(colony, 3_000);

            Assert.That(colony.Pawns.StorageUnits!.StacksIn(unit), Is.Zero,
                "nothing is hauled into a shelf that is being emptied");
        }

        [Test]
        public void ANearlyFullShelfStillTakesAWholeLoadOrNone()
        {
            // The shelf's cell refuses loose stacks, so the load cannot quietly end up on the floor
            // underneath it either: a hauler that cannot put a whole load in goes somewhere else.
            ColonyWorld colony = Fresh();
            int shelf = OpenCell(colony);
            Assume.That(shelf, Is.GreaterThanOrEqualTo(0));
            StorageUnit unit = RaiseShelf(colony, shelf);
            unit.Slots = 1;

            ColonyItems items = colony.Pawns.Items;
            int limit = colony.Pawns.Content.Items[Wood].stackLimit;
            ColonyItem seed = items.Get(items.Spawn(Wood, FreeGround(colony, 0), limit))!;
            items.PickUp(seed, new PawnId(1));
            items.PutIn(seed, StorageUnits.ContainerIdOf(unit.Edifice));

            int ground = FreeGround(colony, 0);
            items.Spawn(ItemIndex.Meal, ground, 2);

            Run(colony, 3_000);

            Assert.That(colony.Pawns.StorageUnits!.StacksIn(unit), Is.EqualTo(1),
                "a full shelf takes nothing else");
            Assert.That(items.CellHasSpace(shelf), Is.False, "and nothing lands on the floor under it");
        }

        [Test]
        public void AFullShelfIsNeverOfferedToADeconstructor()
        {
            // With nobody willing to haul, a shelf ordered taken apart simply stands there full.
            // That is the gate doing its job, and the reason it exists: the deconstruct work is
            // banked on the cell, so a colonist allowed to start and then refused at the last tick
            // would be handed the same site again on every think, for ever, and the fault would
            // read as idleness rather than as a shelf that cannot be emptied.
            ColonyWorld colony = Fresh();
            int shelf = OpenCell(colony);
            Assume.That(shelf, Is.GreaterThanOrEqualTo(0));
            StorageUnit unit = RaiseShelf(colony, shelf);

            ColonyItems items = colony.Pawns.Items;
            ColonyItem wood = items.Get(items.Spawn(Wood, FreeGround(colony, 0), 20))!;
            items.PickUp(wood, new PawnId(1));
            items.PutIn(wood, StorageUnits.ContainerIdOf(unit.Edifice));

            // Nobody hauls, so nothing can empty it.
            for (int i = 0; i < colony.Pawns.Pawns.All.Count; i++)
                colony.Pawns.Pawns.All[i].WorkPriorities[WorkTypeIndex.Haul] = 0;

            Assume.That(colony.Pawns.Designations!.Designate(Size.FromIndex(shelf), DesignationKind.Deconstruct),
                Is.EqualTo(IntentRejection.None));

            Run(colony, 4_000);

            Assert.That(colony.Pawns.StorageUnits!.AtCell(shelf), Is.Not.Null,
                "the shelf still stands");
            Assert.That(items.ResidentIn(StorageUnits.ContainerIdOf(unit.Edifice), Wood)!.Stack,
                Is.EqualTo(20), "with everything still on it");
            Assert.That(colony.Pawns.Designations!.At(shelf),
                Is.EqualTo(DesignationKind.Deconstruct), "and the order still standing");
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
