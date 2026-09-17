#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The rules a wood's colours follow: stands rather than confetti, the same answer every time,
    /// a pine never in an oak's colours, and enough variety on a board to be worth the feature.
    /// </summary>
    public class TreeLookTests
    {
        const int Board = 200;

        [Test]
        public void TheSameCellIsAlwaysDealtTheSameTheme()
        {
            // The mesher re-meshes a whole chunk whenever anything in it changes, so a colour
            // that was not a pure function of the cell would reshuffle the wood every time a
            // colonist felled a tree twenty metres away.
            for (int z = 0; z < 40; z++)
            for (int x = 0; x < 40; x++)
            {
                Assert.That(TreeLook.Theme(x, z, TreeSpecies.Conifer),
                    Is.EqualTo(TreeLook.Theme(x, z, TreeSpecies.Conifer)));
                Assert.That(TreeLook.Stand(x, z), Is.EqualTo(TreeLook.Stand(x, z)));
            }
        }

        [Test]
        public void ATreeIsOnlyEverDealtItsOwnSpeciesColours()
        {
            for (int z = 0; z < Board; z += 3)
            for (int x = 0; x < Board; x += 3)
            {
                Assert.That(TreePalette.At(TreeLook.Theme(x, z, TreeSpecies.Conifer)).Species,
                    Is.EqualTo(TreeSpecies.Conifer), $"conifer at {x},{z}");
                Assert.That(TreePalette.At(TreeLook.Theme(x, z, TreeSpecies.Broadleaf)).Species,
                    Is.EqualTo(TreeSpecies.Broadleaf), $"broadleaf at {x},{z}");
            }
        }

        /// <summary>
        /// A stand has to actually be a stand: a cell's neighbours nearly always belong to the
        /// same one, because that is the whole performance argument — a chunk holds a couple of
        /// colours rather than the whole palette.
        /// </summary>
        [Test]
        public void NeighbouringCellsBelongToTheSameStand()
        {
            int same = 0, total = 0;
            for (int z = 1; z < Board; z++)
            for (int x = 1; x < Board; x++)
            {
                int here = TreeLook.Stand(x, z);
                if (TreeLook.Stand(x - 1, z) == here) same++;
                if (TreeLook.Stand(x, z - 1) == here) same++;
                total += 2;
            }

            float share = same / (float)total;
            TestContext.WriteLine($"neighbours in the same stand: {100f * share:0.0}%");
            // A stand about 20 cells across has a boundary on roughly one edge in ten.
            Assert.That(share, Is.GreaterThan(0.85f));
        }

        /// <summary>
        /// The stands must not be squares. Quantising a cell straight to a grid square is the
        /// obvious implementation and it draws ruler-straight colour boundaries running the whole
        /// width of the board, which reads at once as a bug. The jittered sites make the boundary
        /// wander, and what that means measurably is that the run of cells along a row between two
        /// stand changes is not always a multiple of the stand size.
        /// </summary>
        [Test]
        public void StandBoundariesWander()
        {
            var columns = new HashSet<int>();
            for (int z = 0; z < Board; z++)
            {
                int previous = TreeLook.Stand(0, z);
                for (int x = 1; x < Board; x++)
                {
                    int here = TreeLook.Stand(x, z);
                    if (here != previous) columns.Add(x);
                    previous = here;
                }
            }

            TestContext.WriteLine($"distinct columns a stand boundary crosses: {columns.Count}");
            // A grid would put every boundary on a multiple of StandCell: ten columns on this
            // board. Anything approaching the board's width is a boundary that wanders.
            Assert.That(columns.Count, Is.GreaterThan(Board / 4));
        }

        [Test]
        public void ABoardCarriesMostOfThePalette()
        {
            var seen = new HashSet<int>();
            for (int z = 0; z < Board; z += 2)
            for (int x = 0; x < Board; x += 2)
            {
                seen.Add(TreeLook.Theme(x, z, TreeSpecies.Conifer));
                seen.Add(TreeLook.Theme(x, z, TreeSpecies.Broadleaf));
            }

            TestContext.WriteLine($"themes on a {Board} x {Board} board: {seen.Count} of {TreePalette.Count}");
            // The point of the feature. A board that showed two or three of eleven would be the
            // dullness this replaced, wearing a longer table.
            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(TreePalette.Count - 1));
        }

        /// <summary>
        /// And on the board the game actually loads, which is smaller than the one above and is
        /// the only size anybody will judge this at.
        ///
        /// <para>It matters because the two halves of the design pull against each other: stands
        /// wide enough to be cheap are stands a small board has few of. 120 cells at
        /// <see cref="TreeLook.StandCell"/> is nine squares, and nine squares dealt a conifer
        /// theme and a broadleaf theme each is eighteen draws from a table of eleven — enough that
        /// most of the palette is on screen at once, but not so many that it is certain, which is
        /// why this is measured at the real size rather than inferred from the 200-cell case.</para>
        /// </summary>
        [Test]
        public void ThePlayedBoardCarriesAWoodWorthLookingAt()
        {
            const int Played = 120;
            var seen = new HashSet<int>();
            for (int z = 0; z < Played; z++)
            for (int x = 0; x < Played; x++)
            {
                seen.Add(TreeLook.Theme(x, z, TreeSpecies.Conifer));
                seen.Add(TreeLook.Theme(x, z, TreeSpecies.Broadleaf));
            }

            TestContext.WriteLine(
                $"themes on the played {Played} x {Played} board: {seen.Count} of {TreePalette.Count}");
            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(6),
                "the meadow shows too few tree colours to be worth the feature");
        }

        /// <summary>
        /// Negative coordinates are not a curiosity here: the surround strews trees past the rim
        /// by continuing the board's own cell coordinates, so cell (-3, 40) is a real question and
        /// integer division would put it in the same stand square as (3, 40).
        /// </summary>
        [Test]
        public void TheStandFieldReachesOutsideTheBoard()
        {
            Assert.That(TreeLook.Stand(-40, -40), Is.Not.EqualTo(TreeLook.Stand(40, 40)));

            int same = 0;
            for (int i = -60; i < -20; i++)
                if (TreeLook.Stand(i, 10) == TreeLook.Stand(i + 1, 10)) same++;
            Assert.That(same, Is.GreaterThan(25), "stands outside the board are still stands");
        }
    }

    /// <summary>
    /// The palette and the atlas rectangles it is painted into: both are tables of measured
    /// numbers, and both have invariants that fail silently when broken.
    /// </summary>
    public class TreePaletteTests
    {
        [Test]
        public void BothKindsOfTreeHaveSomethingToChooseBetween()
        {
            Assert.That(TreePalette.For(TreeSpecies.Conifer).Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(TreePalette.For(TreeSpecies.Broadleaf).Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(TreePalette.For(TreeSpecies.Conifer).Length +
                        TreePalette.For(TreeSpecies.Broadleaf).Length,
                Is.EqualTo(TreePalette.Count), "every theme belongs to exactly one species");
        }

        [Test]
        public void ThemeNamesAreDistinct()
        {
            var names = new HashSet<string>();
            for (int i = 0; i < TreePalette.Count; i++)
                Assert.That(names.Add(TreePalette.At(i).Name), $"duplicate theme name at {i}");
        }

        [Test]
        public void ACanopyIsNeverTheSameColourAsItsOwnBark()
        {
            for (int i = 0; i < TreePalette.Count; i++)
            {
                TreeTheme t = TreePalette.At(i);
                Assert.That(t.DeepCanopy.Packed, Is.Not.EqualTo(t.DeepBark.Packed), t.Name);
                Assert.That(t.FreshLeaf.Packed, Is.Not.EqualTo(t.DeepCanopy.Packed), t.Name);
                Assert.That(t.WarmTrunk.Packed, Is.Not.EqualTo(t.DeepBark.Packed), t.Name);
            }
        }

        /// <summary>
        /// The lighter of each pair really is lighter. The shader paints the fresh leaf on the
        /// cluster the probe found at the top of the tree and the deep canopy below it, so a theme
        /// whose "fresh" colour were the darker of the two would light the tree from underneath.
        /// </summary>
        [Test]
        public void TheHighlightIsLighterThanWhatItSitsOn()
        {
            for (int i = 0; i < TreePalette.Count; i++)
            {
                TreeTheme t = TreePalette.At(i);
                Assert.That(Luminance(t.FreshLeaf), Is.GreaterThan(Luminance(t.DeepCanopy)), t.Name + " leaf");
                Assert.That(Luminance(t.WarmTrunk), Is.GreaterThan(Luminance(t.DeepBark)), t.Name + " bark");
            }
        }

        static float Luminance(Rgb24 c) => 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;

        /// <summary>
        /// The rectangles must not overlap. The shader repaints in a fixed order and would hand a
        /// shared texel to whichever slot is painted last — a wrong colour with nothing to report
        /// it, which is the failure this whole file exists to make loud.
        /// </summary>
        [Test]
        public void TheAtlasCellsOfOneTreeDoNotOverlap()
        {
            Assert.That(TreeSwatches.Disjoint(TreeSwatches.Conifer), "conifer");
            Assert.That(TreeSwatches.Disjoint(TreeSwatches.Broadleaf), "broadleaf");
        }

        [Test]
        public void EveryCellIsInsideTheAtlas()
        {
            foreach (TreeCells cells in new[] { TreeSwatches.Conifer, TreeSwatches.Broadleaf })
            foreach (Rect[] slot in new[] { cells.BarkDeep, cells.BarkWarm, cells.LeafDeep, cells.LeafFresh })
            foreach (Rect r in slot)
            {
                Assert.That(r.xMin, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                Assert.That(r.yMin, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                Assert.That(r.xMax, Is.GreaterThan(r.xMin).And.LessThanOrEqualTo(1f));
                Assert.That(r.yMax, Is.GreaterThan(r.yMin).And.LessThanOrEqualTo(1f));
            }
        }

        /// <summary>
        /// Every slot a tree can wear has somewhere to be painted, except the broadleaf's second
        /// bark colour, which the art does not have — recorded here rather than left as a silent
        /// hole, so that adding the cell later is a test that changes rather than a discovery.
        /// </summary>
        [Test]
        public void EverySlotButOneHasACellToPaint()
        {
            Assert.That(TreeSwatches.Conifer.BarkDeep, Is.Not.Empty);
            Assert.That(TreeSwatches.Conifer.BarkWarm, Is.Not.Empty);
            Assert.That(TreeSwatches.Conifer.LeafDeep, Is.Not.Empty);
            Assert.That(TreeSwatches.Conifer.LeafFresh, Is.Not.Empty);

            Assert.That(TreeSwatches.Broadleaf.BarkDeep, Is.Not.Empty);
            Assert.That(TreeSwatches.Broadleaf.LeafDeep, Is.Not.Empty);
            Assert.That(TreeSwatches.Broadleaf.LeafFresh, Is.Not.Empty);
            Assert.That(TreeSwatches.Broadleaf.BarkWarm, Is.Empty,
                "the broadleaf meshes paint their trunk from one cell; see TreeSwatchProbe");
        }
    }
}
