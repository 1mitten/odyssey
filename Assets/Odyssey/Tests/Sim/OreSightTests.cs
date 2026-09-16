#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What the colony knows about the rock it has not cut into: nothing.
    ///
    /// A seam is discovered by exposing a face of it and never any other way, which is what makes
    /// a tunnel worth cutting and a chamber worth breaking into
    /// (<c>docs/research/mining-interview.md</c>, answer 8).
    /// </summary>
    public class OreSightTests
    {
        static CellGrid Board(out MapGenOutcome outcome, uint seed = 1u)
        {
            var size = new GridSize(120, 120, 16);
            var grid = new CellGrid(size);
            outcome = MapGenerator.Generate(grid, seed, NaturalMapGenDef.For(size).MakeWooded());
            return grid;
        }

        [Test]
        public void AFreshBoardHasDiscoveredNothing()
        {
            // Not one cell, and in particular not the ore lining a cavern wall: a chamber nobody
            // has been inside is not a face anybody has exposed. This is the assertion that stops
            // the sealed caverns quietly becoming a treasure map.
            CellGrid grid = Board(out MapGenOutcome outcome);
            int discovered = 0, ore = 0;

            for (int i = 0; i < grid.Terrain.Length; i++)
            {
                if (NaturalContent.IsOre(grid.Terrain[i])) ore++;
                if (grid.IsDiscovered(i)) discovered++;
            }

            Assert.That(ore, Is.GreaterThan(0), "the board has no ore to be ignorant of");
            Assert.That(discovered, Is.Zero, $"{discovered} cells were known before anybody dug");
            Assert.That(outcome.Natural!.Caverns.Count, Is.GreaterThan(0), "no caverns on the board to test against");
        }

        [Test]
        public void OpeningACellRevealsTheSixFacesAroundIt()
        {
            var size = new GridSize(5, 5, 5);
            var grid = new CellGrid(size);
            for (int i = 0; i < grid.Terrain.Length; i++)
            {
                grid.Terrain[i] = NaturalContent.TerrainRock;
                grid.Flags[i] |= CellFlags.SolidTerrain;
            }

            int middle = size.Index(2, 2, 2);
            grid.Terrain[middle] = NaturalContent.TerrainAir;
            grid.Flags[middle] &= ~CellFlags.SolidTerrain;
            grid.RevealAround(middle);

            Assert.That(grid.IsDiscovered(size.Index(1, 2, 2)), Is.True);
            Assert.That(grid.IsDiscovered(size.Index(3, 2, 2)), Is.True);
            Assert.That(grid.IsDiscovered(size.Index(2, 1, 2)), Is.True);
            Assert.That(grid.IsDiscovered(size.Index(2, 3, 2)), Is.True);
            Assert.That(grid.IsDiscovered(size.Index(2, 2, 1)), Is.True);
            Assert.That(grid.IsDiscovered(size.Index(2, 2, 3)), Is.True);

            // Six faces, not twenty-six: a shaft cut past the corner of a seam has not seen into
            // it, and counting diagonals would reveal ore through an edge nothing was cut in.
            Assert.That(grid.IsDiscovered(size.Index(1, 1, 2)), Is.False, "a diagonal neighbour was revealed");
            Assert.That(grid.IsDiscovered(size.Index(3, 3, 3)), Is.False, "a corner neighbour was revealed");
            Assert.That(grid.IsDiscovered(size.Index(0, 2, 2)), Is.False, "a cell two steps away was revealed");
        }

        [Test]
        public void TheEdgeOfTheMapIsNotAFace()
        {
            var size = new GridSize(3, 3, 3);
            var grid = new CellGrid(size);
            for (int i = 0; i < grid.Terrain.Length; i++)
            {
                grid.Terrain[i] = NaturalContent.TerrainRock;
                grid.Flags[i] |= CellFlags.SolidTerrain;
            }

            // A corner cell has three of its six neighbours off the board. Walking off the end of
            // the array would either throw or, worse, reveal a cell on the far side of the map.
            Assert.DoesNotThrow(() => grid.RevealAround(size.Index(0, 0, 0)));
            Assert.That(grid.IsDiscovered(size.Index(2, 0, 0)), Is.False, "the reveal wrapped around the x axis");
            Assert.That(grid.IsDiscovered(size.Index(1, 0, 0)), Is.True);
            Assert.That(grid.IsDiscovered(size.Index(0, 1, 0)), Is.True);
            Assert.That(grid.IsDiscovered(size.Index(0, 0, 1)), Is.True);
        }

        [Test]
        public void KnowledgeIsALatchAndSurvivesTheHoleBeingFilled()
        {
            // The reason this is saved, hashed state and not a derived bit: a seam the colony has
            // seen and then walled back up is still a seam the colony knows about.
            var size = new GridSize(3, 3, 3);
            var grid = new CellGrid(size);
            for (int i = 0; i < grid.Terrain.Length; i++)
            {
                grid.Terrain[i] = NaturalContent.TerrainRock;
                grid.Flags[i] |= CellFlags.SolidTerrain;
            }

            int middle = size.Index(1, 1, 1);
            int seam = size.Index(0, 1, 1);
            grid.Terrain[middle] = NaturalContent.TerrainAir;
            grid.Flags[middle] &= ~CellFlags.SolidTerrain;
            grid.RevealAround(middle);
            Assert.That(grid.IsDiscovered(seam), Is.True);

            grid.Terrain[middle] = NaturalContent.TerrainRock;
            grid.Flags[middle] |= CellFlags.SolidTerrain;
            Assert.That(grid.IsDiscovered(seam), Is.True, "filling the hole back in unlearned the seam");
        }
    }
}
