#nullable enable
using System.IO;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The third answer to "where is a thing".
    ///
    /// <para>Until shelves, a thing was on the ground (<c>Cell &gt;= 0</c>) or in a pair of hands
    /// (<c>Cell &lt; 0</c>), and every reader of <c>Cell &lt; 0</c> could treat the second as
    /// "nowhere that matters". A contained thing is the third state, and it is deliberately the
    /// one already modelled — <c>Cell = -1</c>, no carrier, a container id — so that "not on the
    /// floor" stays one question rather than becoming two.</para>
    ///
    /// <para>These tests are about <see cref="ColonyItems"/> alone, so a container id here is a
    /// bare integer. What a shelf <i>is</i>, and how many slots it has, is
    /// <c>StorageUnits</c>' half; this class owns only where a thing is listed and what it merges
    /// with. That split is the point — a container id of 7 means nothing to the item store beyond
    /// "not 0".</para>
    /// </summary>
    public class ContainerItemTests
    {
        const int Wood = ItemIndex.Wood;
        const int Meal = ItemIndex.Meal;
        const int Shelf = 1;
        const int OtherShelf = 2;

        static readonly PawnId Hauler = new PawnId(1);

        /// <summary>
        /// Spawn a stack on the ground, lift it, and put it into a container — the route every
        /// real load takes, rather than writing the fields by hand.
        /// </summary>
        static ColonyItem Stow(Colony colony, int defIndex, int stack, int container, int from)
        {
            ColonyItems items = colony.Ctx.Items;
            ThingId id = items.Spawn(defIndex, from, stack);
            ColonyItem thing = items.Get(id)!;
            items.PickUp(thing, Hauler);
            return items.PutIn(thing, container);
        }

        /// <summary>
        /// The invariant the whole unit rests on: every live thing is in exactly one place, and
        /// every lister agrees about which. A missed <c>Unlist</c> shows up here and, until this
        /// existed, nowhere else until a colony had silently lost a stack.
        /// </summary>
        static bool Lists(System.Collections.Generic.IReadOnlyList<int> lister, int itemIndex)
        {
            for (int i = 0; i < lister.Count; i++) if (lister[i] == itemIndex) return true;
            return false;
        }

        static void AssertEveryThingIsInExactlyOnePlace(Colony colony)
        {
            ColonyItems items = colony.Ctx.Items;
            for (int i = 0; i < items.Items.Count; i++)
            {
                ColonyItem thing = items.Items[i];
                bool loose = Lists(items.LooseItems, i);
                bool stored = Lists(items.StoredItems, i);
                bool contained = Lists(items.ContainedItems, i);

                if (thing.Despawned)
                {
                    Assert.That(loose || stored || contained, Is.False,
                        $"thing {i} is despawned and still on a lister");
                    Assert.That(thing.ContainerId, Is.Zero, $"thing {i} is despawned and still names a container");
                    continue;
                }

                int homes = (thing.Cell >= 0 ? 1 : 0) + (thing.ContainerId != 0 ? 1 : 0);
                Assert.That(homes, Is.LessThanOrEqualTo(1),
                    $"thing {i} is in a cell and in a container at once");

                if (thing.ContainerId != 0)
                {
                    Assert.That(contained, Is.True, $"thing {i} is in a container and off the contained lister");
                    Assert.That(loose || stored, Is.False, $"thing {i} is in a container and on a ground lister");
                    Assert.That(Lists(items.ContentsOf(thing.ContainerId), i), Is.True,
                        $"thing {i} is not in its own container's contents");
                }
                else
                {
                    Assert.That(contained, Is.False, $"thing {i} names no container and is on the contained lister");
                }
            }
        }

        // ---------------------------------------------------------------- putting in

        [Test]
        public void AThingPutInAContainerIsInNoCellAndOnTheContainedLister()
        {
            var colony = Colony.Build();
            int floor = colony.Cell(3, 3, 0);
            ColonyItem wood = Stow(colony, Wood, 20, Shelf, floor);

            Assert.That(wood.Cell, Is.EqualTo(-1), "a contained thing is in no cell");
            Assert.That(wood.CarriedBy, Is.Zero, "and in nobody's hands");
            Assert.That(wood.ContainerId, Is.EqualTo(Shelf));
            Assert.That(colony.Ctx.Items.ContainedItems, Does.Contain(wood.Id.Value - 1));
            Assert.That(colony.Ctx.Items.LooseItems, Is.Empty, "and off the ground listers");
            Assert.That(colony.Ctx.Items.ItemAt(floor), Is.Null, "the cell it came from is clear");
            Assert.That(colony.Ctx.Items.StacksIn(Shelf), Is.EqualTo(1));
            AssertEveryThingIsInExactlyOnePlace(colony);
        }

        [Test]
        public void TwoDefsInOneContainerAreTwoSlots()
        {
            var colony = Colony.Build();
            Stow(colony, Wood, 20, Shelf, colony.Cell(3, 3, 0));
            Stow(colony, Meal, 5, Shelf, colony.Cell(4, 3, 0));

            Assert.That(colony.Ctx.Items.StacksIn(Shelf), Is.EqualTo(2),
                "a slot holds one kind of thing, exactly as a cell does");
            AssertEveryThingIsInExactlyOnePlace(colony);
        }

        [Test]
        public void ALoadOntoAStackAlreadyThereMergesAndDespawnsTheIncomingThing()
        {
            // Drop's rule, one level up: the resident grows because the resident is the one
            // something else may hold a claim on. A caller that keeps the reference it passed in
            // is holding a tombstone, which is why PutIn returns the thing that is now there.
            var colony = Colony.Build();
            ColonyItem first = Stow(colony, Wood, 20, Shelf, colony.Cell(3, 3, 0));

            ColonyItems items = colony.Ctx.Items;
            ThingId secondId = items.Spawn(Wood, colony.Cell(4, 3, 0), 15);
            ColonyItem second = items.Get(secondId)!;
            items.PickUp(second, Hauler);
            ColonyItem landed = items.PutIn(second, Shelf);

            Assert.That(landed.Id, Is.EqualTo(first.Id), "the resident is what is there afterwards");
            Assert.That(landed.Stack, Is.EqualTo(35));
            Assert.That(second.Despawned, Is.True, "and the incoming thing is gone");
            Assert.That(items.StacksIn(Shelf), Is.EqualTo(1), "one slot, not two");
            AssertEveryThingIsInExactlyOnePlace(colony);
        }

        [Test]
        public void AContainerStackHasRoomOnlyForAWholeLoad()
        {
            // Whole load or nothing, the rule CellHasSpace keeps for a cell. A shelf never splits
            // a load across two slots and never leaves a hauler holding a surplus.
            var colony = Colony.Build();
            int limit = colony.Ctx.Content.Items[Wood].stackLimit;
            Stow(colony, Wood, limit - 10, Shelf, colony.Cell(3, 3, 0));

            ColonyItems items = colony.Ctx.Items;
            Assert.That(items.ContainerStackHasRoom(Shelf, Wood, 10), Is.True, "exactly the room left");
            Assert.That(items.ContainerStackHasRoom(Shelf, Wood, 11), Is.False, "one more than there is");
            Assert.That(items.ContainerStackHasRoom(Shelf, Meal, 1), Is.False,
                "another def merges with nothing — a free slot is the container's question, not this one");
            Assert.That(items.ContainerStackHasRoom(OtherShelf, Wood, 1), Is.False,
                "and a container holding nothing has nothing to merge with");
        }

        // ---------------------------------------------------------------- taking out

        [Test]
        public void TakingAThingOutPutsItOnTheGroundAndFreesTheSlot()
        {
            var colony = Colony.Build();
            ColonyItem wood = Stow(colony, Wood, 20, Shelf, colony.Cell(3, 3, 0));
            int floor = colony.Cell(8, 8, 0);

            ColonyItem landed = colony.Ctx.Items.TakeOutTo(wood, floor);

            Assert.That(landed.Cell, Is.EqualTo(floor));
            Assert.That(landed.ContainerId, Is.Zero);
            Assert.That(colony.Ctx.Items.ContainedItems, Is.Empty);
            Assert.That(colony.Ctx.Items.StacksIn(Shelf), Is.Zero);
            Assert.That(colony.Ctx.Items.LooseItems, Does.Contain(landed.Id.Value - 1),
                "and it is haulable again");
            AssertEveryThingIsInExactlyOnePlace(colony);
        }

        [Test]
        public void LiftingOutOfAContainerIsTheSameOneDoorAsLiftingOffTheFloor()
        {
            // PickUp is the one door for all three homes, which is what keeps the lift toil a
            // single motion whether a colonist stoops to the floor or reaches into a shelf.
            var colony = Colony.Build();
            ColonyItem wood = Stow(colony, Wood, 20, Shelf, colony.Cell(3, 3, 0));

            colony.Ctx.Items.PickUp(wood, Hauler);

            Assert.That(wood.ContainerId, Is.Zero, "no longer in the shelf");
            Assert.That(wood.CarriedBy, Is.EqualTo(Hauler.Value), "in a pair of hands");
            Assert.That(wood.Cell, Is.EqualTo(-1));
            Assert.That(colony.Ctx.Items.ContainedItems, Is.Empty);
            Assert.That(colony.Ctx.Items.StacksIn(Shelf), Is.Zero, "and the slot is free");
            AssertEveryThingIsInExactlyOnePlace(colony);
        }

        [Test]
        public void EatingTheLastOfAStackInAContainerFreesItsSlot()
        {
            // The eat toil decrements the stack and despawns the record on the last one. If
            // Despawn did not unlist, a finished meal would leave a ghost in the shelf and a slot
            // nothing could ever use again — invisible until a shelf mysteriously stopped
            // accepting an eighth kind of thing.
            var colony = Colony.Build();
            ColonyItem meals = Stow(colony, Meal, 1, Shelf, colony.Cell(3, 3, 0));

            meals.Stack--;
            colony.Ctx.Items.Despawn(meals);

            Assert.That(colony.Ctx.Items.StacksIn(Shelf), Is.Zero);
            Assert.That(colony.Ctx.Items.ContainedItems, Is.Empty);
            Assert.That(meals.ContainerId, Is.Zero);
            AssertEveryThingIsInExactlyOnePlace(colony);
        }

        // ---------------------------------------------------------------- the guards

        [Test]
        public void AContainedThingCannotBeDroppedStraightOnToACell()
        {
            // It would land on the cell and stay listed in the container for ever — a stack in two
            // places, which is the one thing the three-homes model exists to make impossible.
            var colony = Colony.Build();
            ColonyItem wood = Stow(colony, Wood, 20, Shelf, colony.Cell(3, 3, 0));

            Assert.That(() => colony.Ctx.Items.Drop(wood, colony.Cell(9, 9, 0)),
                Throws.InvalidOperationException);
            Assert.That(() => colony.Ctx.Items.MoveTo(wood, colony.Cell(9, 9, 0)),
                Throws.InvalidOperationException);
        }

        [Test]
        public void AStackThatWillNotFitTheResidentIsRefusedRatherThanSplit()
        {
            var colony = Colony.Build();
            int limit = colony.Ctx.Content.Items[Wood].stackLimit;
            Stow(colony, Wood, limit, Shelf, colony.Cell(3, 3, 0));

            ColonyItems items = colony.Ctx.Items;
            ThingId id = items.Spawn(Wood, colony.Cell(4, 3, 0), 1);
            ColonyItem more = items.Get(id)!;
            items.PickUp(more, Hauler);

            Assert.That(() => items.PutIn(more, Shelf), Throws.InvalidOperationException,
                "a caller that has not asked whether it fits has a bug");
        }

        // ---------------------------------------------------------------- the save

        [Test]
        public void ContainedThingsComeBackInTheirContainers()
        {
            // The container lister is derived, like the two ground listers — but unlike them it is
            // rebuilt from the item's own record rather than from another section, so it needs no
            // second pass and cannot depend on the order the components are written in.
            var colony = Colony.Build();
            Stow(colony, Wood, 30, Shelf, colony.Cell(3, 3, 0));
            Stow(colony, Meal, 4, Shelf, colony.Cell(4, 3, 0));
            Stow(colony, Wood, 12, OtherShelf, colony.Cell(5, 3, 0));
            colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(7, 7, 0));

            using var stream = new MemoryStream();
            WorldSave.Save(colony.World, stream, colony.SaveComponents);
            stream.Position = 0;

            var fresh = Colony.Build();
            WorldSave.Load(fresh.World, stream, fresh.SaveComponents);
            fresh.Storage.RebucketAll();

            Assert.That(fresh.Ctx.Items.ContainedItems, Is.EqualTo(colony.Ctx.Items.ContainedItems));
            Assert.That(fresh.Ctx.Items.StacksIn(Shelf), Is.EqualTo(2));
            Assert.That(fresh.Ctx.Items.StacksIn(OtherShelf), Is.EqualTo(1));
            Assert.That(fresh.Ctx.Items.ResidentIn(Shelf, Wood)!.Stack, Is.EqualTo(30));
            Assert.That(fresh.Ctx.Items.ResidentIn(OtherShelf, Wood)!.Stack, Is.EqualTo(12));
            Assert.That(fresh.Ctx.Items.LooseItems, Has.Count.EqualTo(1), "and the salvage is still loose");
            AssertEveryThingIsInExactlyOnePlace(fresh);
        }
    }
}
