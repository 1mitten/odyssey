#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The land outside the board, checked by arithmetic rather than by screenshot.
    ///
    /// The faults this guards against all render perfectly and are only visible as a flicker or a
    /// hairline: rings that gap, rings that overlap and z-fight, a seam whose first tile is not a
    /// cell, a surround that moves between sessions.
    /// </summary>
    public class SkirtLayoutTests
    {
        static readonly GridSize Board = new GridSize(120, 120, 16);

        static List<SkirtLayout.SkirtTile> Tiles(GridSize size)
        {
            var tiles = new List<SkirtLayout.SkirtTile>();
            SkirtLayout.BuildTiles(size, tiles);
            return tiles;
        }

        [Test]
        public void TheRingsCoverExactlyTheGroundOutsideTheBoardAndNoneOfTheBoard()
        {
            var tiles = Tiles(Board);

            SkirtLayout.SkirtRect board = SkirtLayout.Board(Board);
            SkirtLayout.SkirtRect outer = SkirtLayout.Outer(Board);

            double covered = 0d;
            foreach (var tile in tiles) covered += (double)tile.SizeX * tile.SizeZ;

            double expected = (double)outer.Width * outer.Depth - (double)board.Width * board.Depth;

            // Exact area, not an approximation: a shortfall is a gap the sky shows through and an
            // excess is coplanar ground z-fighting with itself.
            Assert.That(covered, Is.EqualTo(expected).Within(expected * 1e-6),
                "the rings must tile the surround exactly once");
        }

        [Test]
        public void NoTileStraddlesTheBoardsEdge()
        {
            SkirtLayout.SkirtRect board = SkirtLayout.Board(Board);

            foreach (var tile in Tiles(Board))
            {
                float minX = tile.CentreX - tile.SizeX * 0.5f, maxX = tile.CentreX + tile.SizeX * 0.5f;
                float minZ = tile.CentreZ - tile.SizeZ * 0.5f, maxZ = tile.CentreZ + tile.SizeZ * 0.5f;

                bool outside = maxX <= board.MinX + 1e-3f || minX >= board.MaxX - 1e-3f
                               || maxZ <= board.MinZ + 1e-3f || minZ >= board.MaxZ - 1e-3f;
                Assert.That(outside, Is.True,
                    $"a tile at ({tile.CentreX}, {tile.CentreZ}) overlaps the board");
            }
        }

        [Test]
        public void TheFirstRingIsExactlyOneCellPerTile()
        {
            // This is what makes the seam invisible: at the rim the surround is drawn from the
            // same geometry, at the same size, as the board's own outermost cells.
            foreach (var tile in Tiles(Board))
            {
                if (tile.Band != 0) continue;
                Assert.That(tile.SizeX, Is.EqualTo(CellMetrics.SizeXZ).Within(1e-4f));
                Assert.That(tile.SizeZ, Is.EqualTo(CellMetrics.SizeXZ).Within(1e-4f));
            }
        }

        [Test]
        public void TilesCoarsenWithDistanceSoTheFarGroundIsCheap()
        {
            var tiles = Tiles(Board);
            var largest = new float[SkirtLayout.Bands.Length];
            foreach (var tile in tiles)
                largest[tile.Band] = Mathf.Max(largest[tile.Band], tile.SizeX);

            for (int band = 1; band < largest.Length; band++)
                Assert.That(largest[band], Is.GreaterThan(largest[band - 1]),
                    "each ring out uses larger tiles than the one inside it");

            // The whole surround costs less than the board's own ground does.
            Assert.That(tiles.Count, Is.LessThan(Board.SizeX * Board.SizeZ));
        }

        [Test]
        public void TheSurroundReachesPastTheDistanceFogHasAlreadyClosed()
        {
            // Fog is opaque at 1,100 m (PlayScene.BuildLighting). A surround that stopped inside
            // that would end in a visible arc, which is the fault this whole feature exists to fix.
            Assert.That(SkirtLayout.TotalDepthMetres, Is.GreaterThan(1100f));
        }

        [Test]
        public void TheMutingStartsAtNothingOnTheRim()
        {
            Assert.That(SkirtLayout.MuteStepAt(0f), Is.Zero);
            Assert.That(SkirtLayout.Mute(Color.green, 0), Is.EqualTo(Color.green));

            // ...and reaches its full strength inside the first ring, so the change reads as haze
            // rather than as a line drawn round the playable area.
            Assert.That(SkirtLayout.MuteStepAt(SkirtLayout.MuteRampMetres),
                Is.EqualTo(SkirtLayout.MuteSteps - 1));
            Assert.That(SkirtLayout.MuteRampMetres, Is.LessThan(SkirtLayout.Bands[0].DepthMetres));
        }

        [Test]
        public void MutingDesaturatesWithoutChangingTheMaterial()
        {
            var grass = new Color(0.30f, 0.62f, 0.26f);
            Color muted = SkirtLayout.Mute(grass, SkirtLayout.MuteSteps - 1);

            float before = Mathf.Max(grass.r, Mathf.Max(grass.g, grass.b))
                           - Mathf.Min(grass.r, Mathf.Min(grass.g, grass.b));
            float after = Mathf.Max(muted.r, Mathf.Max(muted.g, muted.b))
                          - Mathf.Min(muted.r, Mathf.Min(muted.g, muted.b));

            Assert.That(after, Is.LessThan(before), "the surround is the less saturated of the two");
            Assert.That(after, Is.GreaterThan(before * 0.5f),
                "but only slightly: a surround that is visibly grey is a border by another name");
        }

        [Test]
        public void TuftsFadeOutOverTheFirstRingRatherThanStoppingAtTheRim()
        {
            // Tufts that simply stopped at the boundary drew a straight line, three hundred metres
            // long, between a field of grass and bare ground.
            Assert.That(SkirtLayout.TuftDensityScale(0f), Is.EqualTo(1f));
            Assert.That(SkirtLayout.TuftDensityScale(SkirtLayout.Bands[0].DepthMetres * 0.5f),
                Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(SkirtLayout.TuftDensityScale(SkirtLayout.Bands[0].DepthMetres), Is.Zero);
            Assert.That(SkirtLayout.TuftDensityScale(500f), Is.Zero);
        }

        [Test]
        public void ABareBoardGetsABareSurround()
        {
            var trees = new List<SkirtLayout.SkirtTree>();
            SkirtLayout.BuildTrees(Board, densityPerMille: 0, variants: 2, densityScale: 1f, trees);
            Assert.That(trees, Is.Empty, "nothing outside the board that is not on it");
        }

        [Test]
        public void TreesStandOutsideTheBoardOnlyAndThinWithDistance()
        {
            var trees = new List<SkirtLayout.SkirtTree>();
            SkirtLayout.BuildTrees(Board, densityPerMille: 260, variants: 2, densityScale: 1f, trees);

            Assert.That(trees, Is.Not.Empty);

            SkirtLayout.SkirtRect board = SkirtLayout.Board(Board);
            int near = 0, far = 0;
            foreach (var tree in trees)
            {
                Assert.That(tree.CellX < 0 || tree.CellX >= Board.SizeX
                            || tree.CellZ < 0 || tree.CellZ >= Board.SizeZ, Is.True,
                    "the simulation owns every tree on the board");

                float x = tree.CellX * CellMetrics.SizeXZ + CellMetrics.HalfXZ;
                float z = tree.CellZ * CellMetrics.SizeXZ + CellMetrics.HalfXZ;
                float distance = board.DistanceOutside(x, z);
                Assert.That(distance, Is.LessThanOrEqualTo(SkirtLayout.TreeRangeMetres + 0.01f));

                if (distance <= SkirtLayout.TreeFullDensityMetres) near++;
                else if (distance >= SkirtLayout.TreeRangeMetres - 15f) far++;
            }

            // Compared per unit area, not per tree: the far band is much the larger of the two.
            float nearCells = (float)near / CellsWithin(board, 0f, SkirtLayout.TreeFullDensityMetres);
            float farCells = (float)far
                             / CellsWithin(board, SkirtLayout.TreeRangeMetres - 15f, SkirtLayout.TreeRangeMetres);
            Assert.That(farCells, Is.LessThan(nearCells),
                "the wood thins outwards, so the cost is paid where it can be seen");
        }

        static int CellsWithin(SkirtLayout.SkirtRect board, float from, float to)
        {
            int reach = Mathf.CeilToInt(SkirtLayout.TreeRangeMetres / CellMetrics.SizeXZ) + 1;
            int count = 0;
            for (int z = -reach; z < Board.SizeZ + reach; z++)
            for (int x = -reach; x < Board.SizeX + reach; x++)
            {
                if (x >= 0 && x < Board.SizeX && z >= 0 && z < Board.SizeZ) continue;
                float distance = board.DistanceOutside(
                    x * CellMetrics.SizeXZ + CellMetrics.HalfXZ,
                    z * CellMetrics.SizeXZ + CellMetrics.HalfXZ);
                if (distance > from && distance <= to) count++;
            }
            return count;
        }

        [Test]
        public void TheSameBoardGivesTheSameSurroundEveryTime()
        {
            var first = new List<SkirtLayout.SkirtTree>();
            var second = new List<SkirtLayout.SkirtTree>();
            SkirtLayout.BuildTrees(Board, 260, 2, 1f, first);
            SkirtLayout.BuildTrees(Board, 260, 2, 1f, second);

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].CellX, Is.EqualTo(first[i].CellX));
                Assert.That(second[i].CellZ, Is.EqualTo(first[i].CellZ));
                Assert.That(second[i].Variant, Is.EqualTo(first[i].Variant));
            }
        }

        [Test]
        public void OnlyTheTreesNearestTheRimAreInTheShadowPass()
        {
            var trees = new List<SkirtLayout.SkirtTree>();
            SkirtLayout.BuildTrees(Board, 260, 2, 1f, trees);

            int casting = 0;
            foreach (var tree in trees) if (tree.CastsShadow) casting++;

            Assert.That(casting, Is.GreaterThan(0), "a tree just off the rim shadows the board");
            Assert.That(casting, Is.LessThan(trees.Count / 2),
                "and the rest of the wood stays out of the shadow pass");
        }

        [Test]
        public void ANarrowBoardIsSurroundedWithoutGapsToo()
        {
            // The rings are laid out per strip rather than on one grid precisely so that the
            // board's dimensions need not divide anything.
            var odd = new GridSize(37, 113, 8);
            var tiles = Tiles(odd);

            SkirtLayout.SkirtRect board = SkirtLayout.Board(odd);
            SkirtLayout.SkirtRect outer = SkirtLayout.Outer(odd);

            double covered = 0d;
            foreach (var tile in tiles) covered += (double)tile.SizeX * tile.SizeZ;
            double expected = (double)outer.Width * outer.Depth - (double)board.Width * board.Depth;

            Assert.That(covered, Is.EqualTo(expected).Within(expected * 1e-6));
        }
    }
}
