#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Shelter has one owner (design 43 §6, §10): the column rule, asked of the whole column and
    /// not the cell above, with a canopy derived from the trees standing now — so a tree that goes
    /// by either removal path takes its shade with it.
    /// </summary>
    public class SkyColumnsTests
    {
        /// <summary>
        /// A bare slab of rock one layer thick, open sky over it. Edits go through the chunk grid
        /// exactly as the game's own edit paths do, because that is how the map hears about them.
        /// </summary>
        sealed class Board
        {
            public readonly GridSize Size;
            public readonly CellGrid Cells;
            public readonly List<PlacedEdifice> Edifices = new List<PlacedEdifice>();
            public readonly ChunkGrid Chunks;
            public readonly SkyColumns Sky;

            public Board(int sx = 12, int sz = 12, int sy = 6)
            {
                Size = new GridSize(sx, sz, sy);
                Cells = new CellGrid(Size);
                Chunks = new ChunkGrid(Size);
                for (int z = 0; z < sz; z++)
                for (int x = 0; x < sx; x++)
                    SetSolid(Size.Index(x, z, 0));
                Sky = new SkyColumns(Cells, Edifices, Chunks);
            }

            void SetSolid(int index)
            {
                Cells.Terrain[index] = CoreContent.TerrainRock;
                Cells.Flags[index] |= CellFlags.SolidTerrain;
            }

            public int Cell(int x, int z, int y) => Size.Index(x, z, y);

            public void Slab(int x, int z, int y)
            {
                Cells.Floor[Cell(x, z, y)] = CoreContent.SlabBuilt;
                Chunks.MarkDirty(x, z, y);
            }

            public void Unslab(int x, int z, int y)
            {
                Cells.Floor[Cell(x, z, y)] = CoreContent.SlabNone;
                Chunks.MarkDirty(x, z, y);
            }

            public void Rock(int x, int z, int y)
            {
                SetSolid(Cell(x, z, y));
                Chunks.MarkDirty(x, z, y);
            }

            public void Water(int x, int z, int y)
            {
                Cells.Terrain[Cell(x, z, y)] = NaturalContent.TerrainShallowWater;
                Chunks.MarkDirty(x, z, y);
            }

            public void Tree(int x, int z, int y)
            {
                int c = Cell(x, z, y);
                Edifices.Add(new PlacedEdifice { CellIndex = c, Def = NaturalContent.EdificeTreeConifer, Stuff = NaturalContent.StuffWood });
                Cells.Edifice[c] = Edifices.Count - 1;
                Chunks.MarkDirty(x, z, y);
            }

            public void Fell(int x, int z, int y)
            {
                Cells.RemoveEdifice(Cell(x, z, y));
                Chunks.MarkDirty(x, z, y);
            }

            public bool Dry(int x, int z, int y) => Sky.ShelteredFromSky(Cell(x, z, y));
        }

        [Test]
        public void ARoofBuiltKeepsTheRainOffAndTakingItAwayLetsItBackIn()
        {
            var b = new Board();
            Assert.That(b.Dry(5, 5, 1), Is.False, "open ground under an empty sky is wet");

            b.Slab(5, 5, 2);
            Assert.That(b.Dry(5, 5, 1), Is.True, "the cell under the roof");
            Assert.That(b.Dry(5, 5, 2), Is.False, "standing on the roof is standing in the rain");
            Assert.That(b.Dry(6, 5, 1), Is.False, "the cell beside the roof");

            b.Unslab(5, 5, 2);
            Assert.That(b.Dry(5, 5, 1), Is.False, "the roof came down and the rain came back");
        }

        [Test]
        public void ARoofTwoLayersUpOverATallRoomStillKeepsItDry()
        {
            // The case the one-layer rules get wrong: IsRoofed asks only about the cell above.
            var b = new Board();
            b.Slab(5, 5, 3);
            int floor = b.Cell(5, 5, 1);
            Assert.That(b.Cells.IsRoofed(floor), Is.False, "the control: the one-layer rule calls this wet");
            Assert.That(b.Dry(5, 5, 1), Is.True, "a hall two storeys high is dry under its roof");
            Assert.That(b.Dry(5, 5, 2), Is.True);
        }

        [Test]
        public void ACaveMouthUnderAnOverhangIsDry()
        {
            var b = new Board();
            // A lip of rock two layers thick over an open mouth, with the sky beside it.
            b.Rock(5, 5, 3);
            b.Rock(5, 5, 4);
            Assert.That(b.Dry(5, 5, 1), Is.True, "under the overhang");
            Assert.That(b.Dry(5, 5, 2), Is.True);
            Assert.That(b.Dry(5, 5, 5), Is.False, "on top of the rock is out in the rain");
            Assert.That(b.Dry(6, 5, 1), Is.False, "a step out of the mouth");
        }

        [Test]
        public void ATreeKeepsTheRainOffItsThreeByThree()
        {
            var b = new Board();
            b.Tree(5, 5, 1);
            for (int z = 4; z <= 6; z++)
            for (int x = 4; x <= 6; x++)
                Assert.That(b.Dry(x, z, 1), Is.True, $"({x},{z}) is under the canopy");
            Assert.That(b.Dry(7, 5, 1), Is.False, "one column past the crown");
            Assert.That(b.Dry(5, 7, 1), Is.False);
            Assert.That(b.Dry(5, 5, 2), Is.True, "the crown is in the layer above the trunk");
            Assert.That(b.Dry(5, 5, 3), Is.False, "and not the one above that");
            Assert.That(b.Sky.ColumnAt(4, 4).Kind, Is.EqualTo(SkyStop.Canopy));
        }

        [Test]
        public void AFelledTreeTakesItsShadeWithIt()
        {
            var b = new Board();
            b.Tree(5, 5, 1);
            Assume.That(b.Dry(4, 4, 1), Is.True);
            b.Fell(5, 5, 1);
            for (int z = 4; z <= 6; z++)
            for (int x = 4; x <= 6; x++)
                Assert.That(b.Dry(x, z, 1), Is.False, $"({x},{z}) kept the shade of a tree that is gone");
        }

        [Test]
        public void ARoofOverTheCanopyWinsAndACanopyOverALowRoofWins()
        {
            var b = new Board();
            b.Tree(5, 5, 1);
            b.Slab(6, 5, 4);           // a high roof over one of the crown's columns
            Assert.That(b.Sky.ColumnAt(6, 5), Is.EqualTo(new SkyColumn(4, SkyStop.Built)));
            b.Slab(4, 5, 1);           // a slab on the ground beside the trunk: the canopy is higher
            Assert.That(b.Sky.ColumnAt(4, 5), Is.EqualTo(new SkyColumn(1 + SkyColumnRule.CanopyLayers, SkyStop.Canopy)));
        }

        [Test]
        public void ASwimmerIsInTheRainAndTheBedOfThePondIsNot()
        {
            var b = new Board();
            b.Water(5, 5, 1);
            Assert.That(b.Dry(5, 5, 1), Is.False, "rain lands on the water, and on whoever is in it");
            Assert.That(b.Sky.ColumnAt(5, 5), Is.EqualTo(new SkyColumn(1, SkyStop.Water)));
        }

        [Test]
        public void TheMapHearsOnlyWhatTheChunkGridIsTold()
        {
            // The negative control for the notification contract: an edit written behind the chunk
            // grid's back is not seen until somebody says the whole map is stale. Every edit path
            // in the game tells the chunk grid, so the drawing re-meshes; that is the notice.
            var b = new Board();
            Assume.That(b.Dry(5, 5, 1), Is.False);
            b.Cells.Floor[b.Cell(5, 5, 2)] = CoreContent.SlabBuilt;
            Assert.That(b.Dry(5, 5, 1), Is.False, "the map saw an edit nobody reported");
            b.Sky.MarkAllDirty();
            Assert.That(b.Dry(5, 5, 1), Is.True);
        }

        [Test]
        public void AnEditRecomputesTheColumnsItTouchedAndTheCanopysReachAndNoMore()
        {
            var b = new Board();
            b.Sky.Sync();
            Assume.That(b.Sky.LastRecomputed, Is.EqualTo(b.Size.LayerStride), "the first question builds the board");
            b.Slab(5, 5, 2);
            b.Sky.Sync();
            Assert.That(b.Sky.LastRecomputed, Is.EqualTo(9), "one column and the eight a trunk there could shade");
        }

        [Test]
        public void EditsAppliedOneAtATimeLandWhereABoardWideRebuildDoes()
        {
            // Incremental against whole: a seeded jumble of roofs, rock, water, trees and fellings,
            // asked after every edit, must end exactly where a fresh map of the same grid starts.
            var b = new Board(16, 16, 7);
            var rng = new System.Random(43);
            var trees = new List<CellRef>();
            for (int step = 0; step < 400; step++)
            {
                int x = rng.Next(16), z = rng.Next(16), y = 1 + rng.Next(5);
                switch (rng.Next(6))
                {
                    case 0: b.Slab(x, z, y); break;
                    case 1: b.Unslab(x, z, y); break;
                    case 2: b.Rock(x, z, y); break;
                    case 3: b.Water(x, z, y); break;
                    case 4: b.Tree(x, z, y); trees.Add(new CellRef(x, z, y)); break;
                    default:
                        if (trees.Count == 0) break;
                        CellRef t = trees[rng.Next(trees.Count)];
                        b.Fell(t.X, t.Z, t.Y);
                        break;
                }
                b.Sky.Sync();
            }

            var fresh = new SkyColumns(b.Cells, b.Edifices, new ChunkGrid(b.Size));
            var source = new GridSkySource(b.Cells, b.Edifices);
            for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            {
                Assert.That(b.Sky.ColumnAt(x, z), Is.EqualTo(fresh.ColumnAt(x, z)), $"column ({x},{z})");
                Assert.That(b.Sky.ColumnAt(x, z), Is.EqualTo(SkyColumnRule.Compute(source, x, z)), $"column ({x},{z}) against the rule");
            }
        }

        // -------------------------------------------------------------- the two removal paths

        static readonly GridSize WoodSize = new GridSize(60, 60, 8);

        static ColonyWorld Wooded()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(WoodSize, 1u, scenario, barren: true, wooded: true);
        }

        /// <summary>
        /// The tree nearest the start whose whole 3 × 3 is covered by it and by nothing else: no
        /// other trunk within two columns, and every column's own landing below the canopy.
        /// </summary>
        static int LoneTree(ColonyWorld colony)
        {
            CellRef start = colony.Start;
            var source = new GridSkySource(colony.Grid, colony.Pawns.Construction!.Edifices.Records);
            int best = -1, bestDistance = int.MaxValue;
            for (int i = WoodSize.LayerStride; i < WoodSize.CellCount; i++)
            {
                if (!colony.Designations.IsTree(i)) continue;
                CellRef at = WoodSize.FromIndex(i);
                if (!Lone(colony, source, at)) continue;
                int distance = System.Math.Abs(at.X - start.X) + System.Math.Abs(at.Z - start.Z);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        static bool Lone(ColonyWorld colony, GridSkySource source, CellRef at)
        {
            for (int dz = -2; dz <= 2; dz++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int x = at.X + dx, z = at.Z + dz;
                if (x < 0 || z < 0 || x >= WoodSize.SizeX || z >= WoodSize.SizeZ) return false;
                if (dx == 0 && dz == 0) continue;
                SkyColumnRule.Walk(source, x, z, out int trunk);
                if (trunk >= 0) return false;
            }
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                if (colony.Pawns.Sky!.ColumnAt(at.X + dx, at.Z + dz).Kind != SkyStop.Canopy) return false;
            return true;
        }

        static void AssertNoShadeAround(ColonyWorld colony, CellRef at, string how)
        {
            SkyColumns sky = colony.Pawns.Sky!;
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                Assert.That(sky.ColumnAt(at.X + dx, at.Z + dz).Kind, Is.Not.EqualTo(SkyStop.Canopy),
                    $"({at.X + dx},{at.Z + dz}) is still shaded by a tree {how}");
            Assert.That(sky.ShelteredFromSky(WoodSize.Index(at)), Is.False, $"the trunk's own cell is dry after the tree was {how}");
        }

        [Test]
        public void ATreeFelledByAWoodcutterLeavesItsThreeByThreeInTheRain()
        {
            ColonyWorld colony = Wooded();
            int tree = LoneTree(colony);
            Assume.That(tree, Is.GreaterThanOrEqualTo(0), "the wooded board has a lone tree");
            CellRef at = WoodSize.FromIndex(tree);
            Assert.That(colony.Pawns.Sky!.ShelteredFromSky(tree), Is.True, "the control: under its own crown");

            colony.World.Intents.Submit(new Intent(IntentKind.Designate, at, (int)DesignationKind.Fell));
            for (int tick = 0; tick < 8_000 && colony.Grid.Edifice[tree] >= 0; tick++) colony.World.Tick();
            Assume.That(colony.Grid.Edifice[tree], Is.LessThan(0), "the tree was never felled");

            AssertNoShadeAround(colony, at, "felled");
        }

        [Test]
        public void ATreeDroppedThroughTheGroundUnderItLeavesItsThreeByThreeInTheRain()
        {
            // The second removal path, Falling.TreesOutOf, which neither marks the record removed
            // nor goes anywhere near the felling job: the ground under the trunk is taken away and
            // the collapse carries the tree with it (FallingTests' own shape).
            ColonyWorld colony = Wooded();
            int tree = LoneTree(colony);
            Assume.That(tree, Is.GreaterThanOrEqualTo(0), "the wooded board has a lone tree");
            CellRef at = WoodSize.FromIndex(tree);
            Assert.That(colony.Pawns.Sky!.ShelteredFromSky(tree), Is.True, "the control: under its own crown");

            int ground = tree - WoodSize.LayerStride;
            colony.Grid.Terrain[ground] = CoreContent.TerrainAir;
            colony.Grid.Flags[ground] &= ~CellFlags.SolidTerrain;
            Falling.OutOf(colony.Pawns, tree);
            Assume.That(colony.Designations.IsTree(tree), Is.False, "the collapse did not take the tree");

            AssertNoShadeAround(colony, at, "dropped");
        }
    }
}
