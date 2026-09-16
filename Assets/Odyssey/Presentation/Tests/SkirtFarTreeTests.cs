#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The wood on the background hills.
    ///
    /// <para>What is worth holding here is not the tree count, which is a taste decision that will
    /// move, but the rules that make it safe to have at all: it never trespasses on the board or
    /// on the near wood, it stops where the fog would hide it, it thins with distance, and the
    /// same board grows the same wood every session. The last matters more than it sounds —
    /// every scatter in this renderer is deterministic so that two screenshots can be compared,
    /// and a wood that reshuffled itself each run would quietly break that for the whole
    /// surround.</para>
    /// </summary>
    public class SkirtFarTreeTests
    {
        static readonly GridSize Board = new GridSize(120, 16, 120);

        static List<SkirtLayout.FarTree> Wood(float densityScale = 1f, int variants = 4)
        {
            var into = new List<SkirtLayout.FarTree>();
            SkirtLayout.BuildFarTrees(Board, variants, densityScale, into);
            return into;
        }

        [Test]
        public void TheFarWoodStandsBetweenTheNearWoodAndTheFog()
        {
            SkirtLayout.SkirtRect board = SkirtLayout.Board(Board);
            List<SkirtLayout.FarTree> wood = Wood();

            Assert.That(wood, Is.Not.Empty, "a wooded board should raise a wood on its hills");

            foreach (SkirtLayout.FarTree tree in wood)
            {
                float distance = board.DistanceOutside(tree.X, tree.Z);
                Assert.That(distance, Is.GreaterThan(SkirtLayout.TreeRangeMetres),
                    "the near wood owns everything inside its own range, and the board owns itself");
                Assert.That(distance, Is.LessThanOrEqualTo(SkirtLayout.FarTreeRangeMetres),
                    "past this the fog is opaque, so a tree there is work nobody can see");
            }
        }

        [Test]
        public void TheSameBoardGrowsTheSameWoodEveryTime()
        {
            List<SkirtLayout.FarTree> first = Wood();
            List<SkirtLayout.FarTree> second = Wood();

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].X, Is.EqualTo(first[i].X));
                Assert.That(second[i].Z, Is.EqualTo(first[i].Z));
                Assert.That(second[i].Variant, Is.EqualTo(first[i].Variant),
                    "the kind of tree is hashed from the lattice too, so it is stable as well");
            }
        }

        [Test]
        public void TheWoodThinsWithDistance()
        {
            SkirtLayout.SkirtRect board = SkirtLayout.Board(Board);
            List<SkirtLayout.FarTree> wood = Wood();

            // Counted per square metre rather than per tree, because the far half of the band is
            // far larger than the near half and a raw count would thin on area alone.
            float mid = (SkirtLayout.TreeRangeMetres + SkirtLayout.FarTreeRangeMetres) * 0.5f;
            int near = 0, far = 0;
            foreach (SkirtLayout.FarTree tree in wood)
            {
                if (board.DistanceOutside(tree.X, tree.Z) < mid) near++;
                else far++;
            }

            float nearArea = Area(board, SkirtLayout.TreeRangeMetres, mid);
            float farArea = Area(board, mid, SkirtLayout.FarTreeRangeMetres);

            Assert.That(near / nearArea, Is.GreaterThan(far / farArea),
                "trees are spent where they can still be seen");
            Assert.That(far, Is.GreaterThan(0), "thinning is not the same as stopping");
        }

        [Test]
        public void ADensityOfNothingGrowsNothing()
        {
            Assert.That(Wood(densityScale: 0f), Is.Empty,
                "the surround's density lever must be able to switch the hill wood off with it");
            Assert.That(Wood(variants: 0), Is.Empty,
                "a board with no kind of tree on it has none to lend the hills");
        }

        [Test]
        public void EveryTreeIsOneOfTheKindsOffered()
        {
            foreach (SkirtLayout.FarTree tree in Wood(variants: 3))
                Assert.That(tree.Variant, Is.InRange(0, 2),
                    "the far wood is capped to fewer kinds than the near one, and must respect the cap");
        }

        [Test]
        public void TreesDoNotStandInRanks()
        {
            // The scatter is a lattice with a jitter, and the jitter is the only thing between it
            // and an orchard. If it were ever dropped the trees would share a handful of exact
            // coordinates, which reads as planted rows along every hillside.
            var columns = new HashSet<float>();
            List<SkirtLayout.FarTree> wood = Wood();
            foreach (SkirtLayout.FarTree tree in wood) columns.Add(tree.X);

            Assert.That(columns.Count, Is.GreaterThan(wood.Count / 2),
                "most trees should stand on their own line, not share one with a neighbour");
        }

        static float Area(SkirtLayout.SkirtRect board, float from, float to)
        {
            SkirtLayout.SkirtRect inner = board.Expand(from);
            SkirtLayout.SkirtRect outer = board.Expand(to);
            return outer.Width * outer.Depth - inner.Width * inner.Depth;
        }
    }
}
