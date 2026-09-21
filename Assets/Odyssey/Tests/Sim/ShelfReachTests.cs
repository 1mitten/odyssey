#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Storage;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Reaching <b>into</b> a shelf: eating out of one, and building out of one.
    ///
    /// <para><b>These two are not polish.</b> Every scan in the simulation that looks for a thing
    /// in the world reads <c>item.Cell &lt; 0</c> and, until shelves, could read it as "in
    /// somebody's hands, so not available". Two of those scans walk the item list raw rather than
    /// a lister, and both are load-bearing: a colonist who cannot see into a store starves beside a
    /// full pantry, and a builder who cannot reach into one is a colony that tidies its timber away
    /// and then cannot build with it.</para>
    ///
    /// <para>Both tests are written so that failure is <b>loud</b> — nobody eats at all, the wall
    /// never rises — rather than a number being slightly off. That is the right shape for a control
    /// whose job is to prove a whole path exists.</para>
    /// </summary>
    public class ShelfReachTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 8);
        const uint Seed = 20260921;

        static ColonyWorld Fresh(int colonists)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = 0;
            return ColonyWorld.Build(Size, Seed, scenario);
        }

        static int OpenCell(ColonyWorld colony, int building, int skip = 0)
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
                if (!colony.Construction.Allows(cell, building)) continue;
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
            return colony.Pawns.StorageUnits!.AtCell(cell)!;
        }

        /// <summary>
        /// Sweep every loose stack of a def off the floor and into the shelf, forbidding anything
        /// that will not fit.
        ///
        /// <para>The scenario scatters meals in several stacks and a slot holds one stack's worth,
        /// so a shelf cannot always swallow the lot. What matters to these tests is that nothing
        /// edible is <b>reachable</b> on the floor, and forbidding is how the game already says
        /// that — it keeps the colony's totals honest, where despawning the surplus would quietly
        /// change the thing the test then measures.</para>
        /// </summary>
        static int SweepInto(ColonyWorld colony, StorageUnit unit, int defIndex)
        {
            ColonyItems items = colony.Pawns.Items;
            StorageUnits units = colony.Pawns.StorageUnits!;
            int container = StorageUnits.ContainerIdOf(unit.Edifice);
            int moved = 0;

            for (int i = 0; i < items.Items.Count; i++)
            {
                ColonyItem thing = items.Items[i];
                if (thing.Despawned || thing.Cell < 0 || thing.DefIndex != defIndex) continue;

                if (!units.HasSpaceFor(unit, thing.DefIndex, thing.Stack))
                {
                    thing.Forbidden = true;
                    continue;
                }

                items.PickUp(thing, new PawnId(1));
                items.PutIn(thing, container);
                moved++;
            }

            return moved;
        }

        static int LiveStackOf(ColonyWorld colony, int defIndex)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == defIndex) total += items[i].Stack;
            return total;
        }

        static void Run(ColonyWorld colony, int ticks)
        {
            for (int i = 0; i < ticks; i++) colony.World.Tick();
        }

        [Test]
        public void HungryColonistsEatOutOfAShelf()
        {
            // Nothing edible is on the floor. Without the eat scan reaching into a store, every one
            // of them starves with a full pantry two paces away — which is a loud failure, and the
            // right kind.
            ColonyWorld colony = Fresh(colonists: 3);
            int cell = OpenCell(colony, BuildingHandle.Shelf);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));
            StorageUnit unit = RaiseShelf(colony, cell);

            int meals = SweepInto(colony, unit, ItemIndex.Meal);
            Assume.That(meals, Is.GreaterThan(0), "the scenario puts meals on the board");
            Assume.That(colony.Pawns.Items.ResidentIn(
                StorageUnits.ContainerIdOf(unit.Edifice), ItemIndex.Meal), Is.Not.Null);

            // Hungry enough to go looking, without waiting a day for it.
            int seek = colony.Pawns.Content.Needs[NeedIndex.Food].seekThreshold;
            for (int i = 0; i < colony.Pawns.Pawns.All.Count; i++)
                colony.Pawns.Pawns.All[i].Needs[NeedIndex.Food] = seek - 1;

            int before = LiveStackOf(colony, ItemIndex.Meal);
            Run(colony, 4_000);

            Assert.That(LiveStackOf(colony, ItemIndex.Meal), Is.LessThan(before),
                "a meal was taken out of the shelf and eaten");

            int fed = 0;
            for (int i = 0; i < colony.Pawns.Pawns.All.Count; i++)
                if (colony.Pawns.Pawns.All[i].Needs[NeedIndex.Food] > seek) fed++;

            Assert.That(fed, Is.GreaterThan(0), "and somebody is no longer hungry");
        }

        [Test]
        public void ABuilderTakesItsMaterialOutOfAShelf()
        {
            // The trap the interview ruled out before it was built: a colony that tidies its timber
            // on to a shelf and can then never build with it. The wall below has no other wood to
            // draw on, so either the delivery scan reaches into the shelf or the wall never rises.
            ColonyWorld colony = Fresh(colonists: 2);
            int shelfCell = OpenCell(colony, BuildingHandle.Shelf);
            Assume.That(shelfCell, Is.GreaterThanOrEqualTo(0));
            StorageUnit unit = RaiseShelf(colony, shelfCell);

            // Wood, and only in the shelf.
            ColonyItems items = colony.Pawns.Items;
            int wood = OpenCell(colony, BuildingHandle.Shelf, skip: 3);
            Assume.That(wood, Is.GreaterThanOrEqualTo(0));
            ColonyItem timber = items.Get(items.Spawn(ItemIndex.Wood, wood, 40))!;
            items.PickUp(timber, new PawnId(1));
            items.PutIn(timber, StorageUnits.ContainerIdOf(unit.Edifice));
            Assume.That(SweepInto(colony, unit, ItemIndex.Wood), Is.Zero, "the only wood is already on the shelf");

            int wall = OpenCell(colony, BuildingHandle.Wall, skip: 5);
            Assume.That(wall, Is.GreaterThanOrEqualTo(0));
            Assume.That(colony.Construction.Place(Size.FromIndex(wall), BuildingHandle.Wall, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None));

            int onTheShelf = colony.Pawns.Items.ResidentIn(
                StorageUnits.ContainerIdOf(unit.Edifice), ItemIndex.Wood)!.Stack;

            Run(colony, 8_000);

            // End to end, and the wall is the evidence rather than a counter: a finished site is
            // cleared, so Delivered() reads zero again once the thing is standing.
            Assert.That(colony.Pawns.Cells.Edifice[wall], Is.GreaterThanOrEqualTo(0),
                "the wall went up, and the only material in the colony was on a shelf");

            int left = colony.Pawns.Items.ResidentIn(
                StorageUnits.ContainerIdOf(unit.Edifice), ItemIndex.Wood)?.Stack ?? 0;
            Assert.That(left, Is.LessThan(onTheShelf), "and the wood it cost came off the shelf");
        }
    }
}
