#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Pawns;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// How a dropped stack of rubble is laid out on a cell floor.
    ///
    /// None of this can be checked by eye from code, and all of it shows: rocks outside the cell
    /// hang over the neighbour's floor, rocks in the same place read as one bad rock, and a count
    /// that does not move with the stack throws away the only quantity cue the ground has. So the
    /// properties are stated here and the screenshot is left to judge taste.
    /// </summary>
    public class ItemHeapTests
    {
        static ItemHeap.Recipe StoneRecipe()
        {
            Assert.That(ItemHeap.TryRecipe(ItemIndex.Stone, out ItemHeap.Recipe recipe), Is.True,
                "stone is the item this whole class exists for");
            return recipe;
        }

        [Test]
        public void RationsAreACrateAndEverythingElseScatters()
        {
            // A crate holds whatever is in it and looks the same either way, so a meal is one
            // prop for ever. Everything a colony stocks by the armful says its amount by
            // spreading: wood joined the list on 2026-09-19, when a tile of 3 and a tile of 75
            // were the same picture.
            Assert.That(ItemHeap.IsHeap(ItemIndex.Meal), Is.False);

            Assert.That(ItemHeap.IsHeap(ItemIndex.Wood), Is.True);
            Assert.That(ItemHeap.IsHeap(ItemIndex.Stone), Is.True);
            Assert.That(ItemHeap.IsHeap(ItemIndex.IronOre), Is.True);
            Assert.That(ItemHeap.IsHeap(ItemIndex.Coal), Is.True);
            Assert.That(ItemHeap.IsHeap(ItemIndex.Carrots), Is.True);
        }

        [Test]
        public void AnItemWithNoRowIsNotAHeapAndDoesNotThrow()
        {
            Assert.That(ItemHeap.IsHeap(-1), Is.False);
            Assert.That(ItemHeap.IsHeap(1000), Is.False);
        }

        [Test]
        public void ABiggerStackIsMoreRocks()
        {
            ItemHeap.Recipe stone = StoneRecipe();

            int small = ItemHeap.RockCount(8, stone);       // one rock's worth of spoil
            int middling = ItemHeap.RockCount(35, stone);
            int full = ItemHeap.RockCount(stone.Full, stone);

            Assert.That(small, Is.LessThan(middling), "a fresh drop reads smaller than half a stack");
            Assert.That(middling, Is.LessThan(full), "and half a stack reads smaller than a full one");
            Assert.That(full, Is.EqualTo(stone.Biggest));
        }

        [Test]
        public void TheCountNeverLeavesItsBandHoweverOddTheStack()
        {
            ItemHeap.Recipe stone = StoneRecipe();

            // Zero should not reach the renderer, and a stack over the limit is a content change
            // away. Neither may draw nothing, and neither may draw more than the cap.
            foreach (int stack in new[] { 0, 1, 2, 74, 75, 400 })
            {
                int rocks = ItemHeap.RockCount(stack, stone);
                Assert.That(rocks, Is.GreaterThanOrEqualTo(1), $"stack {stack} drew nothing at all");
                Assert.That(rocks, Is.LessThanOrEqualTo(ItemHeap.Most), $"stack {stack} drew {rocks}");
            }
        }

        [Test]
        public void EveryRockLandsInsideItsOwnCell()
        {
            // The cap that matters. A rock placed further out than half a cell hangs over the
            // neighbour's floor, and at a stockpile that is every square bleeding into every other.
            for (int def = 0; def < 8; def++)
            {
                if (!ItemHeap.TryRecipe(def, out ItemHeap.Recipe recipe)) continue;

                var placements = new Matrix4x4[ItemHeap.Most];
                for (uint seed = 1; seed < 40; seed++)
                {
                    int rocks = ItemHeap.Place(recipe.Full, seed, Vector3.zero, recipe, placements);
                    for (int i = 0; i < rocks; i++)
                    {
                        Vector3 at = placements[i].GetColumn(3);
                        Assert.That(Mathf.Abs(at.x), Is.LessThan(CellMetrics.HalfXZ),
                            $"def {def} seed {seed} rock {i} hung over the edge in x");
                        Assert.That(Mathf.Abs(at.z), Is.LessThan(CellMetrics.HalfXZ),
                            $"def {def} seed {seed} rock {i} hung over the edge in z");
                    }
                }
            }
        }

        [Test]
        public void RocksSitOnTheFloorAndNotAboveIt()
        {
            ItemHeap.Recipe stone = StoneRecipe();
            var placements = new Matrix4x4[ItemHeap.Most];
            var floor = new Vector3(12.5f, 9f, 40f);

            int rocks = ItemHeap.Place(40, 7u, floor, stone, placements);
            for (int i = 0; i < rocks; i++)
                Assert.That(((Vector3)placements[i].GetColumn(3)).y, Is.EqualTo(floor.y).Within(1e-4f));
        }

        [Test]
        public void NoTwoRocksAreInTheSamePlace()
        {
            // The sunflower spacing earns its keep here. Independent random offsets pass the
            // in-the-cell test above and still put two boulders on top of each other often enough
            // to be seen, and two boulders in one place read as one boulder that is wrong.
            ItemHeap.Recipe stone = StoneRecipe();
            var placements = new Matrix4x4[ItemHeap.Most];

            for (uint seed = 1; seed < 60; seed++)
            {
                int rocks = ItemHeap.Place(stone.Full, seed, Vector3.zero, stone, placements);
                for (int i = 0; i < rocks; i++)
                for (int j = i + 1; j < rocks; j++)
                {
                    Vector3 a = placements[i].GetColumn(3);
                    Vector3 b = placements[j].GetColumn(3);
                    Assert.That(Vector3.Distance(a, b), Is.GreaterThan(0.2f),
                        $"seed {seed}: rocks {i} and {j} are the same rock twice");
                }
            }
        }

        [Test]
        public void TheSameItemIsLaidOutTheSameWayEveryTime()
        {
            // Presentation may not consume simulation randomness, and a heap has to come back the
            // same way round after a reload — the item's id is the only input it is allowed.
            ItemHeap.Recipe stone = StoneRecipe();
            var first = new Matrix4x4[ItemHeap.Most];
            var again = new Matrix4x4[ItemHeap.Most];

            int a = ItemHeap.Place(24, 9001u, Vector3.zero, stone, first);
            int b = ItemHeap.Place(24, 9001u, Vector3.zero, stone, again);

            Assert.That(b, Is.EqualTo(a));
            for (int i = 0; i < a; i++) Assert.That(again[i], Is.EqualTo(first[i]));
        }

        [Test]
        public void TwoPilesSideBySideAreNotTheSamePileTwice()
        {
            ItemHeap.Recipe stone = StoneRecipe();
            var left = new Matrix4x4[ItemHeap.Most];
            var right = new Matrix4x4[ItemHeap.Most];

            ItemHeap.Place(stone.Full, 4u, Vector3.zero, stone, left);
            ItemHeap.Place(stone.Full, 5u, Vector3.zero, stone, right);

            bool anyDifferent = false;
            for (int i = 0; i < stone.Biggest && !anyDifferent; i++) anyDifferent = left[i] != right[i];
            Assert.That(anyDifferent, Is.True, "a neighbouring heap is a copy of this one");
        }

        [Test]
        public void PlacingNeverWritesPastTheBufferItWasGiven()
        {
            // ChunkRenderer hands it a fixed array sized by ItemHeap.Most. A recipe edited to ask
            // for more must be clipped here rather than overrunning there.
            ItemHeap.Recipe stone = StoneRecipe();
            var cramped = new Matrix4x4[2];
            Assert.That(ItemHeap.Place(stone.Full, 3u, Vector3.zero, stone, cramped), Is.EqualTo(2));
        }

        [Test]
        public void AFreshCarrotHarvestDrawsItsOwnCount()
        {
            Assert.That(ItemHeap.TryRecipe(ItemIndex.Carrots, out ItemHeap.Recipe carrot), Is.True,
                "carrots are a heap - nothing contains a pulled harvest, and one prop where five "
                + "came out is the fault the owner watched (2026-09-19)");

            // Five plants stood on the plot, five carrots lie on the ground: the pile's count
            // says out loud what the plot just said. Up to a yield the ramp is the identity,
            // which is what a Full set to the yield count buys.
            Assert.That(ItemHeap.RockCount(1, carrot), Is.EqualTo(1));
            Assert.That(ItemHeap.RockCount(3, carrot), Is.EqualTo(3));
            Assert.That(ItemHeap.RockCount(5, carrot), Is.EqualTo(5),
                "a five-carrot harvest drew some other number of carrots");

            // And past it the cap holds, exactly as stone's does.
            int full = ItemHeap.RockCount(40, carrot);
            Assert.That(full, Is.EqualTo(carrot.Biggest));
            Assert.That(full, Is.LessThanOrEqualTo(ItemHeap.Most));
        }

        [Test]
        public void AWoodPileGrowsInThreeStepsAndNeverBecomesALogJam()
        {
            // The owner's "a third, two thirds, full". The prop is a bound log pile, which is
            // wide — three of them is what a 2.5 m cell holds before they read as a log jam, so
            // wood caps lower than rubble does and the cap is the point of this test.
            Assert.That(ItemHeap.TryRecipe(ItemIndex.Wood, out ItemHeap.Recipe wood), Is.True);

            Assert.That(ItemHeap.RockCount(1, wood), Is.EqualTo(1), "a single log is one bundle");
            Assert.That(ItemHeap.RockCount(wood.Full, wood), Is.EqualTo(3), "a full tile is three");
            Assert.That(ItemHeap.RockCount(wood.Full * 2, wood), Is.EqualTo(3), "and never a fourth");

            // Monotonic, or a pile that grew would sometimes look smaller.
            int last = 0;
            for (int stack = 1; stack <= wood.Full; stack++)
            {
                int now = ItemHeap.RockCount(stack, wood);
                Assert.That(now, Is.GreaterThanOrEqualTo(last), $"stack {stack} drew fewer than {stack - 1}");
                last = now;
            }
        }

        [Test]
        public void WoodIsCarriedAsOneBundleThoughItScattersOnTheFloor()
        {
            // The two questions are separate and only rubble answers both the same way. A load
            // of logs was tuned on the ground's own terms on 2026-09-19 ("when you turn a
            // direction the logs don't turn with you"); scattering it in the arms would undo
            // that look for the sake of a ramp that only matters where the wood is lying.
            Assert.That(ItemHeap.TryRecipe(ItemIndex.Wood, out ItemHeap.Recipe wood), Is.True);
            Assert.That(wood.CarriedAsHeap, Is.False);

            Assert.That(ItemHeap.TryRecipe(ItemIndex.Stone, out ItemHeap.Recipe stone), Is.True);
            Assert.That(stone.CarriedAsHeap, Is.True);
        }

    }
}
