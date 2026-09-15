#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Can a colony actually start where the generator says it should?
    ///
    /// Written after the play scene reported colonists that could not be seen. The spawn logic
    /// lived in a Unity MonoBehaviour and so was never covered by a test, which is exactly how a
    /// bug like this survives: every piece works, and the join between them does not.
    /// </summary>
    public class ColonyStartTests
    {
        /// <summary>The same search the composition root uses to place the starting colony.</summary>
        static List<int> FindStartSpots(CellGrid grid, CellRef startCell, int wanted, int maxRadius)
        {
            var size = grid.Size;
            var spots = new List<int>();
            for (int radius = 0; radius < maxRadius && spots.Count < wanted; radius++)
            {
                for (int dz = -radius; dz <= radius && spots.Count < wanted; dz++)
                for (int dx = -radius; dx <= radius && spots.Count < wanted; dx++)
                {
                    int ax = dx < 0 ? -dx : dx, az = dz < 0 ? -dz : dz;
                    if ((ax > az ? ax : az) != radius) continue;
                    int x = startCell.X + dx, z = startCell.Z + dz;
                    if (!size.Contains(x, z, startCell.Y)) continue;
                    int index = size.Index(x, z, startCell.Y);
                    if (grid.IsWalkable(index)) spots.Add(index);
                }
            }
            return spots;
        }

        [TestCase(60, 60, 5)]
        [TestCase(60, 60, 16)]
        [TestCase(120, 120, 24)]
        public void TheStartCellIsWalkable(int x, int z, int y)
        {
            var size = new GridSize(x, z, y);
            var grid = new CellGrid(size);
            var outcome = MapGenerator.Generate(grid, 1u, MapType.Natural);

            int index = size.Index(outcome.StartCell);
            Assert.That(grid.IsWalkable(index), Is.True,
                $"the generator reported {outcome.StartCell} as the start but a colonist cannot stand there");
        }

        [TestCase(60, 60, 5)]
        [TestCase(60, 60, 16)]
        [TestCase(120, 120, 24)]
        public void EnoughRoomExistsAroundTheStartForAColony(int x, int z, int y)
        {
            // The scene needs five colonists, a food store, beds and a stockpile: about thirty
            // cells. If the surface is terraced the neighbours may sit on a different layer, and
            // a search confined to one layer can come up nearly empty.
            var size = new GridSize(x, z, y);
            var grid = new CellGrid(size);
            var outcome = MapGenerator.Generate(grid, 1u, MapType.Natural);

            var spots = FindStartSpots(grid, outcome.StartCell, wanted: 40, maxRadius: 24);
            Assert.That(spots.Count, Is.GreaterThanOrEqualTo(30),
                $"only {spots.Count} walkable cells within 24 of {outcome.StartCell} on layer {outcome.StartCell.Y}");
        }

        [Test]
        public void TheStartIsWalkableAcrossManySeeds()
        {
            var size = new GridSize(60, 60, 16);
            for (uint seed = 1; seed <= 25; seed++)
            {
                var grid = new CellGrid(size);
                var outcome = MapGenerator.Generate(grid, seed, MapType.Natural);
                int index = size.Index(outcome.StartCell);
                Assert.That(grid.IsWalkable(index), Is.True, $"seed {seed}: start {outcome.StartCell} not walkable");

                var spots = FindStartSpots(grid, outcome.StartCell, wanted: 40, maxRadius: 24);
                Assert.That(spots.Count, Is.GreaterThanOrEqualTo(30),
                    $"seed {seed}: only {spots.Count} spots near {outcome.StartCell}");
            }
        }
    }
}
