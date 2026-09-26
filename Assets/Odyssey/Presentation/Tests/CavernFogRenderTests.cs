#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// A chamber nobody has opened is rock in the mirror, and air the moment a cut breaks in
    /// (design 62 §6). The lie is told once, in <c>WorldRenderModel.CopyCell</c>, so the mesher,
    /// the picker and the landscape measure all read rock without learning there is a cavern.
    /// </summary>
    public class CavernFogRenderTests
    {
        /// <summary>
        /// A column of rock at x = 2 and, beside it at x = 4, the same column with its middle two
        /// cells hollowed into an unseen chamber, capped with rock.
        /// </summary>
        static RenderTestWorld Chamber()
        {
            var world = new RenderTestWorld(8, 8, 5);
            for (int y = 0; y < 4; y++) world.Solid(2, 2, y);
            for (int y = 0; y < 4; y++) world.Solid(4, 2, y);
            for (int y = 1; y <= 2; y++)
            {
                int cell = world.Index(4, 2, y);
                world.Grid.Terrain[cell] = CoreContent.TerrainAir;
                world.Grid.Flags[cell] &= ~CellFlags.SolidTerrain;
                world.Grid.Unseen.Add(cell);
            }
            return world.Publish();
        }

        [Test]
        public void AnUnseenChamberIsSolidRockInTheMirror()
        {
            var world = Chamber();
            int rock = world.Index(2, 2, 1);
            int hidden = world.Index(4, 2, 1);

            Assert.That(world.Model.IsSolid(hidden), Is.True, "the mirror drew the chamber's air");
            Assert.That(world.Model.Terrain(hidden), Is.EqualTo(NaturalContent.TerrainRock),
                "the mirror named something other than rock");
            Assert.That(world.Model.TerrainModule(hidden), Is.EqualTo(world.Model.TerrainModule(rock)),
                "the chamber resolves to a different module from the rock beside it");
            Assert.That(world.Grid.IsSolidTerrain(hidden), Is.False,
                "the simulation forgot that the chamber is air");
        }

        [Test]
        public void ABreachCopiesTheAirOnTheNextPublish()
        {
            var world = Chamber();
            int top = world.Index(4, 2, 3);
            int hidden = world.Index(4, 2, 2);

            // The cut, as MineJob.MineCell makes it: the rock cap goes, its walls are known, and
            // the chamber under it is revealed and its chunks marked — CavernBreach's work.
            world.Mine(4, 2, 3);
            world.Chunks.MarkDirty(world.Grid.FromIndex(top));
            var revealed = new List<int>();
            world.Grid.Unseen.RevealFrom(world.Grid, top, revealed);
            foreach (int cell in revealed) world.Chunks.MarkDirty(world.Grid.FromIndex(cell));
            world.PublishEdits();

            Assert.That(revealed.Count, Is.EqualTo(2), "the breach did not reveal the whole chamber");
            Assert.That(world.Model.IsSolid(hidden), Is.False, "the mirror kept the chamber as rock after the breach");
            Assert.That(world.Model.Terrain(hidden), Is.EqualTo(CoreContent.TerrainAir));
        }
    }
}
