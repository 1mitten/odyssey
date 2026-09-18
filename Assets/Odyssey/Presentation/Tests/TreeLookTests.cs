#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The rules a wood's colours follow: thoroughly mixed inside a stand, different from the wood
    /// over the hill, the same answer every time, a pine never in an oak's colours, and a bounded
    /// number of colours in any one chunk.
    /// </summary>
    public class TreeLookTests
    {
        const int Board = 200;

        [Test]
        public void TheSameCellIsAlwaysDealtTheSameTheme()
        {
            // The mesher re-meshes a whole chunk whenever anything in it changes, so a colour that
            // was not a pure function of the cell would reshuffle the wood every time a colonist
            // felled a tree twenty metres away.
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
        /// The owner's second note, as a test: <i>"but also really mix them in together"</i>.
        ///
        /// <para>The first version dealt one colour to a stand, so a tree and its neighbour were
        /// the same colour about nineteen times in twenty and a wood was a uniform patch. A stand
        /// now deals a handful and each tree picks from it, so neighbours should differ most of the
        /// time — with a handful of four, three times in four if the picks were independent.</para>
        /// </summary>
        [Test]
        public void NeighbouringTreesAreDifferentColours()
        {
            int differ = 0, total = 0;
            for (int z = 1; z < Board; z++)
            for (int x = 1; x < Board; x++)
            {
                int here = TreeLook.Theme(x, z, TreeSpecies.Broadleaf);
                if (TreeLook.Theme(x - 1, z, TreeSpecies.Broadleaf) != here) differ++;
                if (TreeLook.Theme(x, z - 1, TreeSpecies.Broadleaf) != here) differ++;
                total += 2;
            }

            float share = differ / (float)total;
            TestContext.WriteLine($"neighbouring trees of a different colour: {100f * share:0.0}%");
            Assert.That(share, Is.GreaterThan(0.6f),
                "a wood is not mixed; trees are taking their stand's colour rather than their own");
        }

        /// <summary>
        /// And the mixing must not have swallowed the stand. A wood is a *mixture*, and two woods
        /// should be different mixtures — otherwise every chunk carries the whole table, which is
        /// the bill <c>TreeLook</c> exists to avoid.
        /// </summary>
        [Test]
        public void TwoStandsAreDifferentMixtures()
        {
            var seen = new Dictionary<int, HashSet<int>>();
            for (int z = 0; z < Board; z++)
            for (int x = 0; x < Board; x++)
            {
                int stand = TreeLook.Stand(x, z);
                if (!seen.TryGetValue(stand, out HashSet<int>? themes))
                    seen[stand] = themes = new HashSet<int>();
                themes.Add(TreeLook.Theme(x, z, TreeSpecies.Broadleaf));
            }

            int identical = 0, pairs = 0;
            var stands = new List<HashSet<int>>(seen.Values);
            for (int a = 0; a < stands.Count; a++)
            for (int b = a + 1; b < stands.Count; b++)
            {
                pairs++;
                if (stands[a].SetEquals(stands[b])) identical++;
            }

            TestContext.WriteLine($"{stands.Count} stands, {identical} of {pairs} pairs share a mixture");
            foreach (HashSet<int> themes in stands)
                Assert.That(themes.Count, Is.LessThanOrEqualTo(TreeLook.ThemesPerStand),
                    "a stand dealt more colours than its handful");

            Assert.That(identical, Is.LessThan(pairs / 10),
                "stands are drawing the same handful as each other, so the board has no regions");
        }

        /// <summary>
        /// A stand has to actually be a stand: a cell's neighbours nearly always belong to the same
        /// one, because that is what bounds the colours in a chunk.
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
            Assert.That(share, Is.GreaterThan(0.9f));
        }

        /// <summary>
        /// The stands must not be squares. Quantising a cell straight to a grid square is the
        /// obvious implementation and it draws ruler-straight colour boundaries running the whole
        /// width of the board, which reads at once as a bug. The jittered sites make the boundary
        /// wander, and what that means measurably is that a boundary is crossed at many different
        /// columns rather than only at multiples of the stand size.
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
            // A grid would put every boundary on a multiple of StandCell: five columns on this
            // board. Anything approaching the board's width is a boundary that wanders.
            Assert.That(columns.Count, Is.GreaterThan(Board / 4));
        }

        /// <summary>
        /// A stand's handful is distinct: four independent hashes would hand the same colour out
        /// twice about one stand in ten, which narrows the mixing without anybody noticing.
        /// </summary>
        [Test]
        public void AStandsHandfulHasNoRepeats()
        {
            var handful = new int[TreeLook.ThemesPerStand];
            for (int z = 0; z < Board; z += 7)
            for (int x = 0; x < Board; x += 7)
            foreach (TreeSpecies species in new[] { TreeSpecies.Conifer, TreeSpecies.Broadleaf })
            {
                int n = TreeLook.ThemesOfStand(TreeLook.Stand(x, z), species, handful);
                Assert.That(n, Is.EqualTo(TreeLook.ThemesPerStand));
                var distinct = new HashSet<int>();
                for (int i = 0; i < n; i++)
                    Assert.That(distinct.Add(handful[i]),
                        $"{species} stand at {x},{z} was dealt theme {handful[i]} twice");
            }
        }

        /// <summary>
        /// Every wood carries a bright note, and that is a reserved slot rather than a probability.
        ///
        /// <para>Bright rows were added to the table first and it did not work: seven bright tones
        /// among twenty-one means a handful of four draws one on average and often draws none, so
        /// the board came back warmer and no brighter. Adding a colour to a table dilutes it; it
        /// does not lift it. This is the test that keeps the reservation, because a later session
        /// tidying <c>ThemesOfStand</c> would not otherwise know the slot was load-bearing.</para>
        /// </summary>
        [Test]
        public void EveryStandCarriesABrightLeaf()
        {
            var handful = new int[TreeLook.ThemesPerStand];
            int stands = 0;
            for (int z = 0; z < Board; z += 5)
            for (int x = 0; x < Board; x += 5)
            foreach (TreeSpecies species in new[] { TreeSpecies.Conifer, TreeSpecies.Broadleaf })
            {
                int n = TreeLook.ThemesOfStand(TreeLook.Stand(x, z), species, handful);
                bool bright = false;
                for (int i = 0; i < n; i++)
                    if (TreeToneRules.Luminance(TreePalette.At(handful[i]).Leaf.Lit)
                        >= TreeToneRules.BrightLeaf) bright = true;

                Assert.That(bright, $"{species} stand at {x},{z} has no bright leaf in its handful");
                stands++;
            }

            TestContext.WriteLine($"{stands} stand draws checked, every one carries a bright leaf");
        }

        [Test]
        public void ABoardCarriesAGreatManyOfTheThemes()
        {
            var seen = new HashSet<int>();
            for (int z = 0; z < Board; z++)
            for (int x = 0; x < Board; x++)
            {
                seen.Add(TreeLook.Theme(x, z, TreeSpecies.Conifer));
                seen.Add(TreeLook.Theme(x, z, TreeSpecies.Broadleaf));
            }

            TestContext.WriteLine($"themes on a {Board} x {Board} board: {seen.Count} of {TreePalette.Count}");
            // A board cannot show the whole table — it has only so many stands, and each deals a
            // handful — but it must show a great deal more than the eleven the first version had.
            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(60));
        }

        /// <summary>
        /// And on the board the game actually loads, which is smaller than the one above and is the
        /// only size anybody will judge this at. 120 cells is nine stands, each dealing four
        /// colours per species, so seventy-two draws from a table of 166.
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
            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(24),
                "the meadow shows too few tree colours to be worth the feature");
        }

        /// <summary>
        /// Negative coordinates are not a curiosity here: the surround strews trees past the rim by
        /// continuing the board's own cell coordinates, so cell (-3, 40) is a real question and
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

        /// <summary>
        /// With the stand field switched off — the "before" column of <c>TreeCheck</c> — a board
        /// carries exactly the two colours it carried before this feature existed. A before that is
        /// not a before reads exactly like a free feature, which is what the first contact sheet
        /// reported.
        /// </summary>
        [Test]
        public void WithStandsOffTheBoardIsTwoColours()
        {
            try
            {
                TreeLook.Stands = false;
                var seen = new HashSet<int>();
                for (int z = 0; z < 120; z += 3)
                for (int x = 0; x < 120; x += 3)
                {
                    seen.Add(TreeLook.Theme(x, z, TreeSpecies.Conifer));
                    seen.Add(TreeLook.Theme(x, z, TreeSpecies.Broadleaf));
                }

                Assert.That(seen.Count, Is.EqualTo(2));
            }
            finally
            {
                TreeLook.Stands = true;
            }
        }
    }

    /// <summary>
    /// The palette and the atlas rectangles it is painted into: both are tables of measured
    /// numbers, and both have invariants that fail silently when broken.
    /// </summary>
    public class TreePaletteTests
    {
        [Test]
        public void BothKindsOfTreeHaveAGreatDealToChooseBetween()
        {
            TestContext.WriteLine(
                $"conifer {TreePalette.Barks(TreeSpecies.Conifer).Length} barks x " +
                $"{TreePalette.Leaves(TreeSpecies.Conifer).Length} leaves = " +
                $"{TreePalette.For(TreeSpecies.Conifer).Length}; " +
                $"broadleaf {TreePalette.Barks(TreeSpecies.Broadleaf).Length} barks x " +
                $"{TreePalette.Leaves(TreeSpecies.Broadleaf).Length} leaves = " +
                $"{TreePalette.For(TreeSpecies.Broadleaf).Length}; " +
                $"{TreePalette.Count} themes in all");

            Assert.That(TreePalette.For(TreeSpecies.Conifer).Length, Is.GreaterThanOrEqualTo(48));
            Assert.That(TreePalette.For(TreeSpecies.Broadleaf).Length, Is.GreaterThanOrEqualTo(120));
            Assert.That(TreePalette.For(TreeSpecies.Conifer).Length +
                        TreePalette.For(TreeSpecies.Broadleaf).Length,
                Is.EqualTo(TreePalette.Count), "every theme belongs to exactly one species");
        }

        /// <summary>
        /// A tint code says which species a tree is and nothing else, and must disturb none of the
        /// markers that share the code.
        ///
        /// <para>It used to carry the theme, first in the low byte and then in twelve bits of its
        /// own — and carrying it at all is what made a coloured wood cost draw calls, because the
        /// code <em>is</em> the bucket key. The colours are per-instance data now and the palette
        /// is unbounded, so what is left to assert is that the two species codes are clean.</para>
        /// </summary>
        [Test]
        public void EveryThemeSurvivesTheTintCode()
        {
            Assert.That(TintCode.Tree(TreeSpecies.Conifer),
                Is.Not.EqualTo(TintCode.Tree(TreeSpecies.Broadleaf)),
                "the two trees must land in different buckets: they repaint different atlas cells");

            foreach (TreeSpecies species in new[] { TreeSpecies.Conifer, TreeSpecies.Broadleaf })
            {
                int code = TintCode.Tree(species);
                Assert.That(TintCode.TreeSpeciesOf(code), Is.EqualTo(species), "species round trip");
                Assert.That(TintCode.IsTree(code), Is.True);
                // The markers that share the code space. A value reaching down into them would
                // make a tree answer yes to IsTerrain, and the renderer resolves foliage and water
                // *before* it looks at trees.
                Assert.That(TintCode.IsTerrain(code), Is.False, $"{species} reads as terrain");
                Assert.That(TintCode.IsFoliage(code), Is.False, $"{species} reads as foliage");
                Assert.That(TintCode.IsWater(code), Is.False, $"{species} reads as water");
                Assert.That(TintCode.IsDaylit(code), Is.False, $"{species} reads as daylit");
            }
        }

        [Test]
        public void ToneNamesAreDistinctWithinTheirTable()
        {
            foreach (TreeSpecies species in new[] { TreeSpecies.Conifer, TreeSpecies.Broadleaf })
            foreach (TreeTone[] table in new[] { TreePalette.Barks(species), TreePalette.Leaves(species) })
            {
                var names = new HashSet<string>();
                foreach (TreeTone tone in table)
                    Assert.That(names.Add(tone.Name), $"duplicate tone name '{tone.Name}' in {species}");
            }
        }

        /// <summary>
        /// The fault the owner reported, as a test: <i>"a shorter tree that was white/pale leaves
        /// that looked odd"</i>.
        ///
        /// <para>The mechanism was that the lit face goes on the <b>larger</b> of the two canopy
        /// cells — <c>TreeSwatchProbe</c> measures the broadleaf's upper canopy at 49.9% of the
        /// mesh, and the broadleaf is the shorter tree at 6.15 m against the pine's 9.47 m — so a
        /// colour authored as a small bright highlight was painted over half a tree. The ceiling is
        /// the pack's own brightest canopy plus a margin, and the two entries that caused the
        /// complaint are well above it: Silver Birch's old highlight #8F9779 measures 145 and Mossy
        /// Birch's #9CAF88 measures 151, against a ceiling of 132.</para>
        /// </summary>
        [Test]
        public void NoCanopyIsPaleEnoughToReadAsWhite()
        {
            foreach (TreeSpecies species in new[] { TreeSpecies.Conifer, TreeSpecies.Broadleaf })
            foreach (TreeTone leaf in TreePalette.Leaves(species))
            {
                float lit = TreeToneRules.Luminance(leaf.Lit);
                float allowed = TreeToneRules.MaxLeafLit(leaf.Lit);
                Assert.That(lit, Is.LessThanOrEqualTo(allowed),
                    $"{species} leaf '{leaf.Name}' lit face is luminance {lit:0.0} at chroma " +
                    $"{TreeToneRules.Chroma(leaf.Lit)}, which allows {allowed:0.0} — over half a " +
                    "tree that reads as a pale, washed-out canopy");
            }

            foreach (TreeSpecies species in new[] { TreeSpecies.Conifer, TreeSpecies.Broadleaf })
            foreach (TreeTone bark in TreePalette.Barks(species))
            {
                float lit = TreeToneRules.Luminance(bark.Lit);
                Assert.That(lit, Is.LessThanOrEqualTo(TreeToneRules.MaxBarkLit),
                    $"{species} bark '{bark.Name}' lit face is luminance {lit:0.0}");
            }
        }

        /// <summary>
        /// The two faces of a tone must be a step apart, and the size of the step is taken from the
        /// art rather than from taste. Too small and the tree is a flat silhouette; too large and
        /// it is a dark tree wearing a bright cap, which is the shape of the reported fault.
        ///
        /// <para><b>Bark and leaf get different bands, and that was measured rather than decided.</b>
        /// Holding bark to the leaf band failed six entries including three of the owner's own, and
        /// the art sides with the owner: the pack's canopy cells are a step of 1.21 apart while its
        /// trunk and branch-stub cells are <b>1.68</b>. A trunk is a cylinder with a lit side; a
        /// canopy is a cloud of leaves and has no such thing.</para>
        /// </summary>
        [Test]
        public void TheLitFaceIsAStepAboveTheShadedOne()
        {
            foreach (TreeSpecies species in new[] { TreeSpecies.Conifer, TreeSpecies.Broadleaf })
            {
                foreach (TreeTone tone in TreePalette.Leaves(species))
                {
                    float step = TreeToneRules.Step(tone);
                    Assert.That(step, Is.GreaterThanOrEqualTo(TreeToneRules.MinStep)
                        .And.LessThanOrEqualTo(TreeToneRules.MaxLeafStep),
                        $"{species} leaf '{tone.Name}' has a step of {step:0.00}");
                }

                foreach (TreeTone tone in TreePalette.Barks(species))
                {
                    float step = TreeToneRules.Step(tone);
                    Assert.That(step, Is.GreaterThanOrEqualTo(TreeToneRules.MinStep)
                        .And.LessThanOrEqualTo(TreeToneRules.MaxBarkStep),
                        $"{species} bark '{tone.Name}' has a step of {step:0.00}");
                }
            }
        }

        /// <summary>
        /// A leaf must not be grey. The reported fault was not only about brightness — a sage
        /// highlight is pale <em>and</em> nearly colourless, and a colourless canopy reads as a dead
        /// tree rather than as a different species.
        /// </summary>
        [Test]
        public void NoLeafIsGrey()
        {
            foreach (TreeSpecies species in new[] { TreeSpecies.Conifer, TreeSpecies.Broadleaf })
            foreach (TreeTone leaf in TreePalette.Leaves(species))
            {
                Assert.That(TreeToneRules.Chroma(leaf.Lit), Is.GreaterThanOrEqualTo(24),
                    $"{species} leaf '{leaf.Name}' lit face is nearly grey");
                Assert.That(TreeToneRules.Chroma(leaf.Shaded), Is.GreaterThanOrEqualTo(20),
                    $"{species} leaf '{leaf.Name}' shaded face is nearly grey");
            }
        }

        /// <summary>
        /// The owner's third note, as a test: <i>"can we add some bright colours into the leaf — it
        /// seems a bit dull still"</i>.
        ///
        /// <para>Brightness is what the pale rule above constrains, so the two pull against each
        /// other and the table has to be held at both ends or a later session tuning one will
        /// quietly undo the other. The pack's own brightest canopy is luminance 110.9; this asks
        /// for a real spread of tones well above it, which the pale rule permits only because they
        /// are saturated.</para>
        /// </summary>
        [Test]
        public void ThereAreGenuinelyBrightLeavesToDrawFrom()
        {
            foreach (TreeSpecies species in new[] { TreeSpecies.Conifer, TreeSpecies.Broadleaf })
            {
                TreeTone[] leaves = TreePalette.Leaves(species);
                int bright = 0, brightest = 0;
                foreach (TreeTone leaf in leaves)
                {
                    int lit = (int)TreeToneRules.Luminance(leaf.Lit);
                    if (lit > 130) bright++;
                    if (lit > brightest) brightest = lit;
                }

                TestContext.WriteLine(
                    $"{species}: {bright} of {leaves.Length} leaf tones above luminance 130, " +
                    $"brightest {brightest}");
                Assert.That(bright, Is.GreaterThanOrEqualTo(3),
                    $"{species} has too few bright canopies for a wood to lift");
                Assert.That(brightest, Is.GreaterThanOrEqualTo(140),
                    $"{species}'s brightest canopy is still below the pack's own by much");
            }
        }

        [Test]
        public void ACanopyIsNeverTheSameColourAsItsOwnBark()
        {
            for (int i = 0; i < TreePalette.Count; i++)
            {
                TreeTheme t = TreePalette.At(i);
                Assert.That(t.Leaf.Lit.Packed, Is.Not.EqualTo(t.Bark.Lit.Packed), t.Name);
                Assert.That(t.Leaf.Shaded.Packed, Is.Not.EqualTo(t.Bark.Shaded.Packed), t.Name);
            }
        }

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
        /// Every slot a tree can wear has somewhere to be painted, except the broadleaf's lit bark,
        /// which the art does not have — recorded here rather than left as a silent hole, so that
        /// adding the cell later is a test that changes rather than a discovery.
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
