#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The scenery made real, as presentation draws it (design 45): the art follows the
    /// simulation's species, a bush is wild ground rather than a building and no obstacle, and a
    /// felled tree is noticed so it can be drawn going over.
    /// </summary>
    public class NatureRenderTests
    {
        [Test]
        public void EachSpeciesWearsItsOwnRows()
        {
            const int rows = 5;   // Meadow_02, three fruit trees, the giant
            var fruit = new HashSet<int>();
            var birch = new HashSet<int>();
            for (int z = 0; z < 40; z++)
            for (int x = 0; x < 40; x++)
            {
                Assert.That(TreeArt.VariantFor(NaturalContent.EdificeTreeMeadow, x, z, rows), Is.EqualTo(TreeArt.MeadowRow));
                Assert.That(TreeArt.VariantFor(NaturalContent.EdificeTreeGiant, x, z, rows), Is.EqualTo(TreeArt.GiantRow));
                int f = TreeArt.VariantFor(NaturalContent.EdificeTreeFruit, x, z, rows);
                Assert.That(f, Is.InRange(TreeArt.FirstFruitRow, TreeArt.LastFruitRow));
                fruit.Add(f);
                birch.Add(TreeArt.VariantFor(NaturalContent.EdificeTreeBirch, x, z, 3));
            }
            Assert.That(fruit.Count, Is.EqualTo(3), "every fruit tree row is worn somewhere");
            Assert.That(birch.Count, Is.EqualTo(3), "and every birch");
        }

        [Test]
        public void AFamilyShortOfRowsStillDrawsATree()
        {
            foreach (ushort def in new[] { NaturalContent.EdificeTreeMeadow, NaturalContent.EdificeTreeFruit,
                         NaturalContent.EdificeTreeGiant, NaturalContent.EdificeTreeBirch })
            for (int rows = 1; rows <= 5; rows++)
            for (int x = 0; x < 20; x++)
                Assert.That(TreeArt.VariantFor(def, x, 3, rows), Is.InRange(0, rows - 1), $"{def} with {rows} rows");
        }

        [Test]
        public void ABushIsWildGroundAndNoObstacle()
        {
            var world = new RenderTestWorld(8, 8, 3);
            world.Solid(2, 2, 0, NaturalContent.TerrainGrass).Solid(5, 5, 0, NaturalContent.TerrainGrass)
                .Edifice(2, 2, 1, NaturalContent.EdificeBush, blocking: false)
                .Edifice(5, 5, 1, NaturalContent.EdificeTreeGiant, blocking: false)
                .Publish();

            Assert.That(world.Model.HasObstacle(world.Index(2, 2, 1)), Is.False, "a colonist pushes through a bush");
            Assert.That(world.Model.HasObstacle(world.Index(5, 5, 1)), Is.True, "and steps round a trunk");
            Assert.That(world.Model.IsBuiltAt(world.Index(2, 2, 1)), Is.False, "a bush is not a building");
            Assert.That(world.Model.IsBuiltAt(world.Index(5, 5, 1)), Is.False);
        }

        [Test]
        public void AFelledTreeIsNoticedOnceAndALoadIsNot()
        {
            var world = new RenderTestWorld(8, 8, 3);
            world.Solid(3, 3, 0, NaturalContent.TerrainGrass).Solid(4, 4, 0, NaturalContent.TerrainGrass)
                .Edifice(3, 3, 1, NaturalContent.EdificeTreeBirch, blocking: false)
                .Edifice(4, 4, 1, NaturalContent.EdificeBush, blocking: false)
                .Publish();
            var felled = new List<WorldRenderModel.FelledTree>();
            world.Model.DrainFelled(felled);
            Assert.That(felled, Is.Empty, "a world arriving whole has no trees falling in it");

            world.Grid.RemoveEdifice(world.Index(3, 3, 1));
            world.Grid.RemoveEdifice(world.Index(4, 4, 1));
            world.Chunks.MarkDirty(3, 3, 1);
            world.PublishEdits();

            world.Model.DrainFelled(felled);
            Assert.That(felled.Count, Is.EqualTo(1), "the tree, and not the bush, is drawn going over");
            Assert.That(felled[0].Cell, Is.EqualTo(world.Index(3, 3, 1)));
            Assert.That(felled[0].Def, Is.EqualTo(NaturalContent.EdificeTreeBirch));

            felled.Clear();
            world.Model.DrainFelled(felled);
            Assert.That(felled, Is.Empty, "and only once");
        }
    }
}
