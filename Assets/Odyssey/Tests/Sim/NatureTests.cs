#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The scenery made real (design 45, M5): trees by species, with their own wood and work, and
    /// bushes that are walked through slowly and cleared before anything is built on them.
    /// </summary>
    public class NatureTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 8);

        static ColonyWorld Wooded(uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: true);
        }

        static NaturalMapResult Generate(uint seed, GridSize size)
        {
            var grid = new CellGrid(size);
            var gen = NaturalMapGenDef.For(size).MakeWooded();
            return NaturalMapGenerator.Generate(grid, seed, gen);
        }

        /// <summary>The nearest cell to the start on the start layer that the predicate accepts, or -1.</summary>
        static int Nearest(ColonyWorld colony, System.Func<int, bool> wanted)
        {
            CellRef start = colony.Start;
            int best = -1, bestDistance = int.MaxValue;
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
            for (int y = 0; y < Size.SizeY; y++)
            {
                int index = Size.Index(x, z, y);
                if (!wanted(index)) continue;
                int distance = System.Math.Abs(x - start.X) + System.Math.Abs(z - start.Z) + System.Math.Abs(y - start.Y) * 4;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = index;
            }
            return best;
        }

        static int Count(ColonyWorld colony, int itemDef)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == itemDef) total += items[i].Stack;
            return total;
        }

        static int FellAndWait(ColonyWorld colony, int cell, int ticks = 12_000)
        {
            colony.World.Intents.Submit(new Intent(IntentKind.Designate, Size.FromIndex(cell), (int)DesignationKind.Fell));
            colony.World.Tick();
            Assert.That(colony.World.Intents.Rejected, Is.Empty, "the order was accepted");
            for (int tick = 0; tick < ticks; tick++)
            {
                colony.World.Tick();
                if (colony.Grid.Edifice[cell] < 0) return colony.World.CurrentTick;
            }
            return -1;
        }

        // ---------------------------------------------------------------- the Defs

        [Test]
        public void EveryWildEdificeHasADefAndTheSpeciesScaleWithSize()
        {
            WildPlantDef birch = NaturalContent.WildPlantAt(NaturalContent.EdificeTreeBirch)!;
            WildPlantDef meadow = NaturalContent.WildPlantAt(NaturalContent.EdificeTreeMeadow)!;
            WildPlantDef fruit = NaturalContent.WildPlantAt(NaturalContent.EdificeTreeFruit)!;
            WildPlantDef giant = NaturalContent.WildPlantAt(NaturalContent.EdificeTreeGiant)!;
            Assert.That(NaturalContent.WildPlantAt(NaturalContent.EdificeBush), Is.Not.Null);
            Assert.That(NaturalContent.WildPlantAt(NaturalContent.EdificeBerryBush), Is.Not.Null);
            Assert.That(NaturalContent.WildPlantAt(NaturalContent.EdificeBerryBushPicked),
                Is.SameAs(NaturalContent.WildPlantAt(NaturalContent.EdificeBerryBush)),
                "a picked berry bush is the same plant with its berries off");
            Assert.That(NaturalContent.WildPlantAt(CoreContent.EdificeBed), Is.Null, "a bed is not wild");

            // The owner's ordering (2026-09-25): birch small and quick, the giant a great deal
            // of both, the meadow and fruit trees between.
            Assert.That(birch.clearWorkTicks, Is.LessThan(fruit.clearWorkTicks));
            Assert.That(fruit.clearWorkTicks, Is.LessThanOrEqualTo(meadow.clearWorkTicks));
            Assert.That(meadow.clearWorkTicks, Is.LessThan(giant.clearWorkTicks));
            Assert.That(birch.clearYieldCount, Is.LessThan(fruit.clearYieldCount));
            Assert.That(meadow.clearYieldCount, Is.LessThan(giant.clearYieldCount));
            foreach (WildPlantDef tree in new[] { birch, meadow, fruit, giant })
                Assert.That(tree.clearYields, Is.EqualTo("Item_Wood"), tree.defName + " yields wood");
        }

        [Test]
        public void TheNaturalIdsNeverCollideWithABuilding()
        {
            for (ushort id = 0; id < NaturalContent.EdificeLimit; id++)
            {
                bool natural = NaturalContent.IsNatural(id);
                bool building = id == CoreContent.EdificeBed || id == CoreContent.EdificeShelf
                    || id == CoreContent.EdificeCampfire || id == CoreContent.EdificeGenerator
                    || id == CoreContent.EdificeHeater || id < NaturalContent.FirstEdifice;
                Assert.That(natural && building, Is.False, $"edifice {id} is claimed twice");
                Assert.That(NaturalContent.IsTree(id) && NaturalContent.IsBush(id), Is.False);
            }
            Assert.That(EdificeHandle.Count, Is.EqualTo(NaturalContent.EdificeLimit));
        }

        // ---------------------------------------------------------------- species

        [Test]
        public void TheSpeciesRollIsTheOneTheTreePassAlwaysDrew()
        {
            const int broadleaf = 420;
            Assert.That(TreePass.SpeciesOf(420, broadleaf), Is.EqualTo(NaturalContent.EdificeTreeBirch));
            Assert.That(TreePass.SpeciesOf(999, broadleaf), Is.EqualTo(NaturalContent.EdificeTreeBirch));
            Assert.That(TreePass.SpeciesOf(0, broadleaf), Is.EqualTo(NaturalContent.EdificeTreeGiant));

            int giants = 0, meadows = 0, fruits = 0;
            for (int roll = 0; roll < broadleaf; roll++)
            {
                ushort def = TreePass.SpeciesOf(roll, broadleaf);
                if (def == NaturalContent.EdificeTreeGiant) giants++;
                else if (def == NaturalContent.EdificeTreeMeadow) meadows++;
                else if (def == NaturalContent.EdificeTreeFruit) fruits++;
                else Assert.Fail($"roll {roll} is below the broadleaf chance and came out {def}");
            }
            Assert.That(giants, Is.InRange(9, 12), "one broadleaf in forty is a giant");
            Assert.That(meadows * 3, Is.InRange(fruits - 30, fruits + 30), "one meadow tree to three fruit trees");
        }

        [Test]
        public void AWoodedBoardGrowsEverySpecies()
        {
            NaturalMapResult map = Generate(7u, new GridSize(120, 120, 16));
            NaturalGenReport report = map.Report;
            Assert.That(report.Conifers, Is.GreaterThan(0), "birches");
            Assert.That(report.MeadowTrees, Is.GreaterThan(0));
            Assert.That(report.FruitTrees, Is.GreaterThan(0));
            Assert.That(report.GiantTrees, Is.GreaterThan(0), "a Standard board has a giant or two");
            Assert.That(report.GiantTrees, Is.LessThan(report.MeadowTrees), "and giants are rare");
            Assert.That(report.MeadowTrees + report.FruitTrees + report.GiantTrees, Is.EqualTo(report.Broadleaves));

            foreach (TreePlacement tree in map.Trees)
                Assert.That(NaturalContent.IsTree(tree.Def), Is.True);
        }

        [Test]
        public void EachSpeciesFallsForItsOwnWood()
        {
            ColonyWorld colony = Wooded();
            int birch = Nearest(colony, i => TreeOf(colony, i) == NaturalContent.EdificeTreeBirch);
            Assume.That(birch, Is.GreaterThanOrEqualTo(0), "a birch stands on the board");

            Assert.That(FellAndWait(colony, birch), Is.GreaterThan(0), "the birch was felled");
            Assert.That(Count(colony, ItemIndex.Wood),
                Is.EqualTo(NaturalContent.WildPlantAt(NaturalContent.EdificeTreeBirch)!.clearYieldCount));
        }

        [Test]
        public void ABirchComesDownFasterThanAMeadowTree()
        {
            // The same colonist, the same board, one tree each: the work is the species'.
            ColonyWorld first = Wooded();
            int birch = Nearest(first, i => TreeOf(first, i) == NaturalContent.EdificeTreeBirch);
            ColonyWorld second = Wooded();
            int meadow = Nearest(second, i => TreeOf(second, i) == NaturalContent.EdificeTreeMeadow);
            Assume.That(birch, Is.GreaterThanOrEqualTo(0));
            Assume.That(meadow, Is.GreaterThanOrEqualTo(0));

            int birchSwings = SwingTicks(first, birch);
            int meadowSwings = SwingTicks(second, meadow);
            Assert.That(birchSwings, Is.LessThan(meadowSwings),
                $"a birch took {birchSwings} ticks of swinging and a meadow tree {meadowSwings}");
        }

        /// <summary>Ticks spent swinging at one marked tree, from the first swing to its fall.</summary>
        static int SwingTicks(ColonyWorld colony, int tree)
        {
            colony.World.Intents.Submit(new Intent(IntentKind.Designate, Size.FromIndex(tree), (int)DesignationKind.Fell));
            int swinging = 0;
            for (int tick = 0; tick < 12_000 && colony.Grid.Edifice[tree] >= 0; tick++)
            {
                colony.World.Tick();
                foreach (Pawn pawn in colony.Pawns.Pawns.All)
                    if (pawn.Driver is FellJobDriver fell && fell.WorkFocus == tree) swinging++;
            }
            Assert.That(colony.Grid.Edifice[tree], Is.LessThan(0), "the tree was felled");
            return swinging;
        }

        static ushort TreeOf(ColonyWorld colony, int index) =>
            colony.Designations.TryEdifice(index, out PlacedEdifice placed) && NaturalContent.IsTree(placed.Def)
                ? placed.Def : CoreContent.EdificeNone;

        // ---------------------------------------------------------------- bushes

        [Test]
        public void BushesGrowOnOpenGrassAwayFromTheStartAndNeverAtATerraceFoot()
        {
            GridSize size = new GridSize(120, 120, 16);
            NaturalMapResult map = Generate(7u, size);
            CellGrid grid = map.Context.Grid;
            CellRef start = map.StartCell;
            int bushes = 0, berries = 0;

            foreach (PlacedEdifice placed in map.Context.Edifices)
            {
                if (placed.Removed || !NaturalContent.IsBush(placed.Def)) continue;
                bushes++;
                if (placed.Def == NaturalContent.EdificeBerryBush) berries++;
                int cell = placed.CellIndex;
                CellRef at = size.FromIndex(cell);
                Assert.That(grid.Terrain[cell - size.LayerStride], Is.EqualTo(NaturalContent.TerrainGrass), "a bush roots in grass");
                Assert.That(grid.IsUndergrowth(cell), Is.True, "a bush carries its flag");
                Assert.That(grid.IsWalkable(cell), Is.True, "a bush is walked through");
                Assert.That(TerraceFoot.IsFoot(grid, cell), Is.False, "the bank fills that cell");
                int dx = at.X - start.X, dz = at.Z - start.Z;
                Assert.That(dx * dx + dz * dz, Is.GreaterThan(16), "the landing site is clear");
                Assert.That((at.X & 1) == 0 && (at.Z & 1) == 0, Is.True, "on the dressing's lattice");
            }

            Assert.That(bushes, Is.EqualTo(map.Report.Bushes));
            Assert.That(bushes, Is.InRange(80, 1200), "the dressing's density, give or take the seed");
            Assert.That(berries, Is.GreaterThan(0).And.LessThan(bushes / 3), "a berry bush is one bush in six");

            // Every flagged cell is a bush, and nothing else carries the flag.
            int flagged = 0;
            for (int i = 0; i < size.CellCount; i++) if (grid.IsUndergrowth(i)) flagged++;
            Assert.That(flagged, Is.EqualTo(bushes));
        }

        [Test]
        public void TheBushesAreTheSeedsAndTheSameSeedGrowsTheSameOnes()
        {
            GridSize size = new GridSize(60, 60, 8);
            NaturalMapResult a = Generate(11u, size);
            NaturalMapResult b = Generate(11u, size);
            NaturalMapResult c = Generate(12u, size);
            Assert.That(a.GridHash, Is.EqualTo(b.GridHash));
            Assert.That(a.Report.Bushes, Is.EqualTo(b.Report.Bushes));
            Assert.That(a.Report.Bushes, Is.GreaterThan(0));
            Assert.That(a.GridHash, Is.Not.EqualTo(c.GridHash));
        }

        [Test]
        public void TheBareBoardHasNoBushes()
        {
            GridSize size = new GridSize(60, 60, 8);
            var grid = new CellGrid(size);
            NaturalMapResult map = NaturalMapGenerator.Generate(grid, 3u, NaturalMapGenDef.For(size).MakeBarren());
            Assert.That(map.Report.Bushes, Is.Zero, "anything on the bare board that is not grass is a bug");
        }

        [Test]
        public void ABushCostsMoreToCrossThanGrassAndClearingItGivesTheCellBack()
        {
            ColonyWorld colony = Wooded();
            int bush = Nearest(colony, i => colony.Grid.IsUndergrowth(i));
            Assume.That(bush, Is.GreaterThanOrEqualTo(0), "a bush grows on the board");
            NavGrid nav = colony.Pawns.Nav.Grid;

            Assert.That(nav.CostClass[bush], Is.EqualTo(NaturalContent.CostClassBush));
            Assert.That(nav.ExtraCost(bush), Is.EqualTo(NaturalContent.BushExtraCost));
            Assert.That(colony.Designations.IsFellable(bush), Is.True);
            Assert.That(colony.Designations.IsTree(bush), Is.False);

            int wood = Count(colony, ItemIndex.Wood);
            Assert.That(FellAndWait(colony, bush), Is.GreaterThan(0), "the bush was cleared");
            colony.World.Tick();

            Assert.That(colony.Grid.IsUndergrowth(bush), Is.False, "the flag went with the bush");
            Assert.That(nav.CostClass[bush], Is.EqualTo(NaturalContent.CostClassClear), "the cell is grass again");
            Assert.That(Count(colony, ItemIndex.Wood), Is.EqualTo(wood), "clearing a bush yields nothing");
        }

        [Test]
        public void NothingIsBuiltOrZonedOnABushUntilItIsCleared()
        {
            ColonyWorld colony = Wooded();
            int bush = Nearest(colony, i => colony.Grid.IsUndergrowth(i));
            Assume.That(bush, Is.GreaterThanOrEqualTo(0), "a bush grows on the board");

            Assert.That(colony.Construction.Allows(bush), Is.False, "a wall waits for the bush to go");
            Assume.That(colony.Growing, Is.Not.Null);
            Assert.That(colony.Growing!.Designate(Size.FromIndex(bush), 0), Is.EqualTo(IntentRejection.NotPermitted),
                "so does a field");

            FellAndWait(colony, bush);
            colony.World.Tick();
            Assert.That(colony.Construction.Allows(bush), Is.True, "and once it has gone the cell is open");
        }

        [Test]
        public void AFellOrderOnTheGroundUnderABushLiftsToTheBush()
        {
            ColonyWorld colony = Wooded();
            int bush = Nearest(colony, i => colony.Grid.IsUndergrowth(i));
            Assume.That(bush, Is.GreaterThanOrEqualTo(0));
            CellRef ground = Size.FromIndex(bush - Size.LayerStride);

            Assert.That(colony.Designations.Designate(ground, DesignationKind.Fell), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Designations.At(bush), Is.EqualTo(DesignationKind.Fell),
                "a drag over the grass names the bush standing on it, as it does a tree");
            Assert.That(colony.Designations.CanMine(bush - Size.LayerStride), Is.False,
                "and the ground under a bush is not dug while it stands");
        }
    }
}
