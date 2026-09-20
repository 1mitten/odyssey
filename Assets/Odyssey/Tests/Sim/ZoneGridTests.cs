#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The zone container on its own: which cell is in which slot, which cells a slot owns, and
    /// the whole-world list every scan walks.
    ///
    /// <para><b>Why it is tested apart from the things that use it.</b> Two of its rules cost a
    /// bug each inside <c>GrowingZones</c> — the fold that left a slot's cells unsorted, and the
    /// dissolve that shifted instead of swapping — and neither was reachable from the growing
    /// tests that existed at the time. One was found by a PlayMode benchmark and the other by
    /// re-reading the file on the day its branch opened a PR. A container asserted directly, with
    /// a random walk that checks every invariant after every single edit, is what stops the third
    /// one taking as long.</para>
    ///
    /// <para>The container carries no policy, so these tests never mention plants, filters or
    /// eight-neighbour joins: they say what must be true of the bookkeeping whoever is joining
    /// whom.</para>
    /// </summary>
    public class ZoneGridTests
    {
        const int Cells = 64;

        /// <summary>
        /// Everything that must be true after any edit. Named rather than inlined because the
        /// random walk below asserts it after every step, and a failure should say which rule
        /// broke rather than which line of the walk it broke on.
        /// </summary>
        static void AssertInvariants(ZoneGrid zones)
        {
            var seen = new HashSet<int>();

            for (int slot = 0; slot < zones.Count; slot++)
            {
                IReadOnlyList<int> cells = zones.CellsOf(slot);
                Assert.That(cells.Count, Is.GreaterThan(0), $"slot {slot} is empty and should have dissolved");

                for (int i = 0; i < cells.Count; i++)
                {
                    if (i > 0)
                        Assert.That(cells[i], Is.GreaterThan(cells[i - 1]),
                            $"slot {slot} is not ascending, so a binary search on it will answer negatively");

                    Assert.That(zones.SlotAt(cells[i]), Is.EqualTo(slot),
                        $"cell {cells[i]} is owned by slot {slot} but points at {zones.SlotAt(cells[i])}");
                    Assert.That(seen.Add(cells[i]), Is.True, $"cell {cells[i]} is in two slots at once");
                }
            }

            IReadOnlyList<int> all = zones.Cells;
            Assert.That(all.Count, Is.EqualTo(seen.Count), "the whole-world list and the slots disagree on how many cells are zoned");
            for (int i = 0; i < all.Count; i++)
            {
                if (i > 0) Assert.That(all[i], Is.GreaterThan(all[i - 1]), "the whole-world list is not ascending");
                Assert.That(seen.Contains(all[i]), Is.True, $"cell {all[i]} is listed but belongs to no slot");
            }
        }

        [Test]
        public void AFoundedZoneHoldsTheCellsItIsGiven()
        {
            var zones = new ZoneGrid(Cells);
            int slot = zones.Found(7);

            zones.Join(slot, 20);
            zones.Join(slot, 10);

            Assert.That(zones.Count, Is.EqualTo(1));
            Assert.That(zones.TagOf(slot), Is.EqualTo(7));
            Assert.That(zones.SlotAt(10), Is.EqualTo(slot));
            Assert.That(zones.SlotAt(11), Is.EqualTo(-1), "an unzoned cell answers -1");
            Assert.That(zones.CellsOf(slot), Is.EqualTo(new[] { 10, 20 }), "a slot's cells are ascending however they arrived");
            AssertInvariants(zones);
        }

        [Test]
        public void ASlotDissolvesWhenItsLastCellLeaves()
        {
            var zones = new ZoneGrid(Cells);
            int slot = zones.Found(1);
            zones.Join(slot, 5);

            Assert.That(zones.Leave(5), Is.True);
            Assert.That(zones.Count, Is.EqualTo(0), "a zone reduced to nothing is deleted");
            Assert.That(zones.SlotAt(5), Is.EqualTo(-1));
            Assert.That(zones.Leave(5), Is.False, "leaving a cell that is in no zone is an answer, not a failure");
            AssertInvariants(zones);
        }

        /// <summary>
        /// The bug the PlayMode field benchmark found: a dissolve in the middle of the list must
        /// move the last slot into the gap and take its cells with it, or every later slot wears
        /// an index nothing agrees with.
        /// </summary>
        [Test]
        public void DissolvingASlotInTheMiddleRepointsTheOneThatMovesIntoIt()
        {
            var zones = new ZoneGrid(Cells);
            int first = zones.Found(1);
            int middle = zones.Found(2);
            int last = zones.Found(3);

            zones.Join(first, 1);
            zones.Join(middle, 2);
            zones.Join(last, 3);

            zones.Leave(2);

            Assert.That(zones.Count, Is.EqualTo(2));
            Assert.That(zones.SlotAt(1), Is.EqualTo(first), "the slot before the gap does not move");
            Assert.That(zones.TagOf(zones.SlotAt(3)), Is.EqualTo(3),
                "the slot that moved into the gap keeps its own tag and its own cells");
            AssertInvariants(zones);
        }

        /// <summary>
        /// The bug found by re-reading the fold: two sorted runs appended are not one sorted list,
        /// and every read of a slot's cells binary-searches them.
        /// </summary>
        [Test]
        public void AFoldLeavesTheSurvivorAscendingEvenWhenTheAbsorbedCellsAreSmaller()
        {
            var zones = new ZoneGrid(Cells);
            int high = zones.Found(1);
            int low = zones.Found(1);

            zones.Join(high, 30);
            zones.Join(high, 31);
            zones.Join(low, 10);
            zones.Join(low, 11);

            int survivor = zones.MergeInto(high, low);

            Assert.That(zones.Count, Is.EqualTo(1));
            Assert.That(zones.CellsOf(survivor), Is.EqualTo(new[] { 10, 11, 30, 31 }));
            Assert.That(zones.SlotAt(10), Is.EqualTo(survivor), "an absorbed cell points at the survivor");
            AssertInvariants(zones);

            // And the search that would have thrown before the sort.
            Assert.That(zones.Leave(10), Is.True);
            AssertInvariants(zones);
        }

        /// <summary>
        /// The half of the fold that is easy to miss: dissolving the absorbed slot moves the last
        /// slot into its place, and the last slot can be the survivor itself — so the index the
        /// caller was holding is stale and <see cref="ZoneGrid.MergeInto"/> has to answer with the
        /// new one.
        /// </summary>
        [Test]
        public void AFoldAnswersWithTheSurvivorsNewIndexWhenTheSurvivorItselfMoves()
        {
            var zones = new ZoneGrid(Cells);
            int absorbed = zones.Found(1);   // slot 0
            int survivor = zones.Found(1);   // slot 1, the last

            zones.Join(absorbed, 5);
            zones.Join(survivor, 6);

            int now = zones.MergeInto(survivor, absorbed);

            Assert.That(now, Is.EqualTo(0), "the survivor moved into the gap the absorbed slot left");
            Assert.That(zones.Count, Is.EqualTo(1));
            Assert.That(zones.CellsOf(now), Is.EqualTo(new[] { 5, 6 }));
            AssertInvariants(zones);
        }

        [Test]
        public void AFoldOfASlotIntoItselfChangesNothing()
        {
            var zones = new ZoneGrid(Cells);
            int slot = zones.Found(1);
            zones.Join(slot, 5);

            Assert.That(zones.MergeInto(slot, slot), Is.EqualTo(slot));
            Assert.That(zones.CellsOf(slot), Is.EqualTo(new[] { 5 }));
            AssertInvariants(zones);
        }

        [Test]
        public void AppendAndRestoreOrderSortEverythingTheLoadPathLeftUnsorted()
        {
            var zones = new ZoneGrid(Cells);
            int a = zones.Found(1);
            int b = zones.Found(2);

            // Zone records arrive in file order, so the whole-world list is interleaved.
            zones.Append(a, 30);
            zones.Append(a, 31);
            zones.Append(b, 10);
            zones.Append(b, 11);
            zones.RestoreOrder();

            Assert.That(zones.Cells, Is.EqualTo(new[] { 10, 11, 30, 31 }));
            AssertInvariants(zones);
        }

        [Test]
        public void ClearForgetsEverything()
        {
            var zones = new ZoneGrid(Cells);
            int slot = zones.Found(1);
            zones.Join(slot, 5);

            zones.Clear();

            Assert.That(zones.Count, Is.EqualTo(0));
            Assert.That(zones.Cells.Count, Is.EqualTo(0));
            Assert.That(zones.SlotAt(5), Is.EqualTo(-1), "a cleared grid must not leave a cell pointing at a slot that has gone");
            AssertInvariants(zones);
        }

        /// <summary>
        /// The one that earns the file: a long random sequence of joins, leaves and folds with the
        /// invariants checked after every step. Both historical bugs fail it within a few hundred
        /// steps, and neither was reachable from the growing tests that existed when they landed.
        ///
        /// <para>Seeded from the project's own deterministic generator, so a failure is
        /// reproducible from the seed printed with it rather than from a machine.</para>
        /// </summary>
        [Test]
        public void ARandomWalkOfJoinsLeavesAndFoldsKeepsEveryInvariant()
        {
            for (uint seed = 1; seed <= 20; seed++)
            {
                var zones = new ZoneGrid(Cells);
                var rng = DeterministicRandom.ForTick(seed, 0, 0);

                for (int step = 0; step < 400; step++)
                {
                    int cell = rng.NextInt(Cells);
                    switch (rng.NextInt(3))
                    {
                        case 0: // join: an existing slot chosen at random, or a new one
                            if (zones.SlotAt(cell) >= 0) break;
                            int slot = zones.Count > 0 && rng.NextInt(2) == 0
                                ? rng.NextInt(zones.Count)
                                : zones.Found(rng.NextInt(4));
                            zones.Join(slot, cell);
                            break;

                        case 1:
                            zones.Leave(cell);
                            break;

                        default: // fold two slots together
                            if (zones.Count < 2) break;
                            int home = rng.NextInt(zones.Count);
                            int other = rng.NextInt(zones.Count);
                            if (home == other) break;
                            zones.MergeInto(home, other);
                            break;
                    }

                    AssertInvariants(zones);
                }
            }
        }
    }
}
