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
        public void OnlyWhatAMineLeavesIsDrawnAsAHeap()
        {
            // Rations come in a crate and wood comes in a bundle. A heap is for lumps.
            Assert.That(ItemHeap.IsHeap(ItemIndex.Meal), Is.False);
            Assert.That(ItemHeap.IsHeap(ItemIndex.Wood), Is.False);

            Assert.That(ItemHeap.IsHeap(ItemIndex.Stone), Is.True);
            Assert.That(ItemHeap.IsHeap(ItemIndex.IronOre), Is.True);
            Assert.That(ItemHeap.IsHeap(ItemIndex.Coal), Is.True);
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
    }
}
