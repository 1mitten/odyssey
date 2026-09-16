#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A marked tree becomes wood on the ground, and the wood becomes stock. The whole line the
    /// colony's first work runs along, on the wooded board the scene loads.
    /// </summary>
    public class FellJobTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 8);

        static ColonyWorld Wooded(uint seed = 1u, int fellRadius = 0) =>
            ColonyWorld.Build(Size, seed, colonists: 3, barren: true, wooded: true, fellRadius: fellRadius);

        /// <summary>The nearest tree to the start on the start layer, as a cell index, or -1.</summary>
        static int NearestTree(ColonyWorld colony)
        {
            CellRef start = colony.Start;
            int best = -1, bestDistance = int.MaxValue;
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
            {
                int index = Size.Index(x, z, start.Y);
                if (!colony.Designations.IsTree(index)) continue;
                int distance = System.Math.Abs(x - start.X) + System.Math.Abs(z - start.Z);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = index;
            }
            return best;
        }

        static int WoodOnTheGround(ColonyWorld colony)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == ItemIndex.Wood) total += items[i].Stack;
            return total;
        }

        [Test]
        public void AMarkedTreeIsFelledAndLeavesWood()
        {
            ColonyWorld colony = Wooded();
            int tree = NearestTree(colony);
            Assume.That(tree, Is.GreaterThanOrEqualTo(0), "the wooded board has a tree to fell");
            CellRef cell = Size.FromIndex(tree);

            colony.World.Intents.Submit(new Intent(IntentKind.Designate, cell, (int)DesignationKind.Fell));
            colony.World.Tick();
            Assert.That(colony.World.Intents.Rejected, Is.Empty);
            Assert.That(colony.Designations.At(tree), Is.EqualTo(DesignationKind.Fell));

            int felledAt = -1;
            for (int tick = 0; tick < 6_000 && felledAt < 0; tick++)
            {
                colony.World.Tick();
                if (colony.Grid.Edifice[tree] < 0) felledAt = colony.World.CurrentTick;
            }

            Assert.That(felledAt, Is.GreaterThan(0), "the tree was never felled");
            Assert.That(colony.Designations.At(tree), Is.EqualTo(DesignationKind.None), "the order is cleared once carried out");
            Assert.That(WoodOnTheGround(colony), Is.EqualTo(colony.Pawns.Pawns.All[0].Content.WoodPerTree), "one tree, one stack of wood");
            Assert.That(colony.Grid.IsWalkable(tree), Is.True, "the cell is open ground afterwards");
        }

        [Test]
        public void TheWoodIsHauledToTheStockpile()
        {
            ColonyWorld colony = Wooded(fellRadius: 6);
            Assume.That(colony.Designations.Count, Is.GreaterThan(0), "trees stand within six cells of the start");
            int marked = colony.Designations.Count;

            colony.World.Tick(20_000);

            Assert.That(colony.Designations.Count, Is.LessThan(marked), "some orders were carried out");
            Assert.That(WoodOnTheGround(colony), Is.GreaterThan(0));

            int stocked = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Despawned || item.DefIndex != ItemIndex.Wood || item.Cell < 0) continue;
                if (colony.Pawns.Items.IsStockpileCell(item.Cell)) stocked++;
            }
            Assert.That(stocked, Is.GreaterThan(0), "felled wood ends up in the stockpile");
        }

        [Test]
        public void FellingIsDeterministic()
        {
            ColonyWorld first = Wooded(seed: 3u, fellRadius: 8);
            ColonyWorld second = Wooded(seed: 3u, fellRadius: 8);
            first.World.Tick(12_000);
            second.World.Tick(12_000);
            Assert.That(first.World.ComputeStateHash().Value, Is.EqualTo(second.World.ComputeStateHash().Value));
            Assert.That(WoodOnTheGround(first), Is.GreaterThan(0));
        }

        [Test]
        public void ACancelledOrderStopsTheJob()
        {
            ColonyWorld colony = Wooded();
            int tree = NearestTree(colony);
            Assume.That(tree, Is.GreaterThanOrEqualTo(0));
            CellRef cell = Size.FromIndex(tree);

            colony.World.Intents.Submit(new Intent(IntentKind.Designate, cell, (int)DesignationKind.Fell));
            colony.World.Tick(200);
            colony.World.Intents.Submit(new Intent(IntentKind.CancelDesignation, cell));
            colony.World.Tick(3_000);

            Assert.That(colony.Grid.Edifice[tree], Is.GreaterThanOrEqualTo(0), "the tree still stands");
            Assert.That(WoodOnTheGround(colony), Is.Zero);
            Assert.That(colony.Pawns.Reservations.ActiveClaims, Is.Zero, "no claim outlives the cancelled job");
        }

        [Test]
        public void TheDefaultWorkPrioritiesLetAColonistCut()
        {
            ColonyWorld colony = Wooded();
            var pawn = colony.Pawns.Pawns.All[0];
            Assert.That(pawn.WorkPriorities.Length, Is.EqualTo(WorkTypeIndex.Count));
            Assert.That(pawn.WorkPriority(WorkTypeIndex.Cutting), Is.InRange(1, 4));
        }
    }
}
