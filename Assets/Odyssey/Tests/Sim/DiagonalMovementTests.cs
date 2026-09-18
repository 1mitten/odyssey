#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The cell search went 8-connected on 2026-09-18, because the owner reported colonists
    /// looking "square in movement". See <c>docs/design/21-diagonal-movement.md</c>.
    ///
    /// <para><b>The corner rule is the whole of the risk and most of this file.</b> A diagonal is
    /// refused unless <em>both</em> flanking cells are enterable — stricter than
    /// <c>d-04-pathfinding.md</c>, which said "forbidden when both flanking cells block" and so
    /// would have let a colonist slip past a single wall corner. The owner overruled it on
    /// 2026-09-18.</para>
    ///
    /// <para><b>The region graph did not have to change at all</b>, and
    /// <see cref="NeitherReadingOfTheCornerRuleCanChangeWhatIsReachable"/> is the proof: a
    /// diagonal whose flank is open is a diagonal whose two ends were already joined by two
    /// orthogonal steps through that flank. So 8-connectivity makes routes cheaper and can never
    /// make an unreachable cell reachable. Regions, links, districts and zones are untouched, and
    /// all three golden <c>Generated</c> hashes — which fold in the region graph — held when this
    /// landed. If a later change ever makes a diagonal reachability-creating, that argument
    /// collapses and the region flood has to go 8-connected with it.</para>
    ///
    /// <para><b>Note what the corner rule does and does not buy</b>, because this file said it
    /// wrong first. It does not keep rooms sealed: the proof above holds for d-04 s reading too,
    /// so neither reading can unseal anything. It buys the look — a colonist is never drawn
    /// clipping through the corner of a wall — and that is reason enough, but it is not a
    /// connectivity guarantee.</para>
    /// </summary>
    public class DiagonalMovementTests
    {
        const TraverseMode Mode = TraverseMode.Colonist;

        static NavGraph Board(int size, out CellGrid cells)
        {
            cells = NavWorld.MakeCells(size, size, 1);
            var nav = new NavGraph(cells);
            nav.Rebuild();
            return nav;
        }

        /// <summary>
        /// A board with walls scattered through it.
        ///
        /// <para><b>The scatter happens before the graph is built, and that is not a style
        /// choice.</b> The first version of this helper wrote <c>cells.Flags</c> on a graph that
        /// already existed and then called <c>Rebuild</c>, which rebuilds dirty blocks — and
        /// nothing had been marked dirty, so not one wall reached the nav grid. Both tests below
        /// then ran on open ground and could not have failed: the count of permitted diagonals
        /// came back as 1,936, which is 22 x 22 x 4, every candidate on the board. Caught by
        /// printing that number rather than by reading the code.</para>
        /// </summary>
        static NavGraph ScatteredBoard(int size, uint seed, int percent, out CellGrid cells)
        {
            cells = NavWorld.MakeCells(size, size, 1);

            uint s = seed;
            for (int i = 0; i < cells.Size.CellCount; i++)
            {
                s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                if (s % 100 < (uint)percent) cells.Flags[i] |= CellFlags.SolidTerrain;
            }

            var nav = new NavGraph(cells);
            nav.Rebuild();
            return nav;
        }

        // ---- the corner rule -------------------------------------------------------------

        /// <summary>
        /// One wall corner, in each of the four orientations, refuses the diagonal that would cut
        /// past it. Four cases rather than one because the flanks are computed from x and z
        /// separately, and a sign error would show in exactly one of them.
        /// </summary>
        [TestCase(1, 1)]
        [TestCase(-1, 1)]
        [TestCase(1, -1)]
        [TestCase(-1, -1)]
        public void ASingleWallCornerRefusesTheDiagonalPastIt(int dx, int dz)
        {
            NavGraph nav = Board(12, out CellGrid cells);
            int fromX = 5, fromZ = 5;
            int from = cells.Index(fromX, fromZ, 0);
            int to = cells.Index(fromX + dx, fromZ + dz, 0);

            Assert.That(nav.IsLegalStep(from, to, Mode), Is.True, "open ground, the diagonal is legal");

            // Block one flank only. d-04's rule would still have allowed the step.
            NavWorld.SetSolid(cells, nav, cells.Index(fromX + dx, fromZ, 0), true);
            nav.Rebuild();

            Assert.That(nav.IsLegalStep(from, to, Mode), Is.False,
                "one blocked flank is enough: a colonist may not squeeze past a wall corner");
        }

        [Test]
        public void TheTwoWaysOfAskingTheCornerRuleAgree()
        {
            NavGraph nav = Board(12, out CellGrid cells);
            NavWorld.SetSolid(cells, nav, cells.Index(6, 5, 0), true);
            NavWorld.SetSolid(cells, nav, cells.Index(4, 6, 0), true);
            nav.Rebuild();

            for (int z = 1; z < 11; z++)
            for (int x = 1; x < 11; x++)
            for (int sz = -1; sz <= 1; sz += 2)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                int from = cells.Index(x, z, 0);
                int to = cells.Index(x + sx, z + sz, 0);

                bool viaFlanks = nav.Grid.DiagonalAllowed(
                    cells.Index(x + sx, z, 0), cells.Index(x, z + sz, 0), Mode);
                bool viaCells = nav.Grid.DiagonalAllowedBetween(from, to, Mode);

                Assert.That(viaCells, Is.EqualTo(viaFlanks),
                    $"the hot-path overload and the decomposing one disagree at ({x},{z}) + ({sx},{sz})");
            }
        }

        [Test]
        public void TheDecomposingOverloadRefusesAnythingThatIsNotOneDiagonalStep()
        {
            NavGraph nav = Board(12, out CellGrid cells);
            int from = cells.Index(5, 5, 0);

            Assert.That(nav.Grid.DiagonalAllowedBetween(from, cells.Index(6, 5, 0), Mode), Is.False, "orthogonal");
            Assert.That(nav.Grid.DiagonalAllowedBetween(from, cells.Index(7, 7, 0), Mode), Is.False, "two cells away");
            Assert.That(nav.Grid.DiagonalAllowedBetween(from, from, Mode), Is.False, "itself");
        }

        // ---- the price -------------------------------------------------------------------

        [Test]
        public void ADiagonalCostsOneFourOneAndNotTwoHundred()
        {
            NavGraph nav = Board(20, out CellGrid cells);
            var finder = new PathFinder(nav);

            PathResult r = finder.FindPath(cells.Index(4, 4, 0), cells.Index(5, 5, 0), Mode);

            Assert.That(r.Ok, Is.True);
            Assert.That(finder.PathToArray().Length, Is.EqualTo(2), "one step, not two");
            Assert.That(r.Cost, Is.EqualTo(MoveCost.Diagonal));
        }

        /// <summary>
        /// The seam the hop is the worked example of: the planner charges one number for a step,
        /// the mover charges another, nothing reports it, and a colonist stands still for ten
        /// thousand ticks. Both now come through <c>NavGrid.EnterCost(index, mode, diagonal)</c>,
        /// and this walks a real path around a wall asking both.
        /// </summary>
        [Test]
        public void ThePlannerAndTheMoverPriceEveryStepAlike()
        {
            NavGraph nav = Board(30, out CellGrid cells);
            for (int z = 6; z < 24; z++) NavWorld.SetSolid(cells, nav, cells.Index(15, z, 0), true);
            nav.Rebuild();

            var finder = new PathFinder(nav);
            PathResult r = finder.FindPath(cells.Index(3, 3, 0), cells.Index(27, 27, 0), Mode);
            Assert.That(r.Ok, Is.True);

            int[] path = finder.PathToArray();
            Assume.That(path.Length, Is.GreaterThan(4));

            int walked = 0;
            for (int i = 1; i < path.Length; i++)
            {
                Assert.That(nav.IsLegalStep(path[i - 1], path[i], Mode), Is.True,
                    $"the mover re-validates every step and refuses step {i}");

                int dx = System.Math.Abs(path[i] % cells.Size.SizeX - path[i - 1] % cells.Size.SizeX);
                int dz = System.Math.Abs(path[i] / cells.Size.SizeX - path[i - 1] / cells.Size.SizeX);
                walked += nav.Grid.EnterCost(path[i], Mode, diagonal: dx == 1 && dz == 1);
            }

            Assert.That(walked, Is.EqualTo(r.Cost),
                "the sum of what the mover will charge is what the planner quoted");
        }

        // ---- the structural claim --------------------------------------------------------

        /// <summary>
        /// A box whose wall runs meet only at their corners is still sealed. This is the case
        /// d-04's rule would have leaked through, and the one that matters once rooms,
        /// temperature and doors exist.
        /// </summary>
        [Test]
        public void ASealedRoomStaysSealed()
        {
            NavGraph nav = Board(16, out CellGrid cells);

            for (int i = 4; i <= 9; i++)
            {
                NavWorld.SetSolid(cells, nav, cells.Index(i, 4, 0), true);
                NavWorld.SetSolid(cells, nav, cells.Index(i, 9, 0), true);
                NavWorld.SetSolid(cells, nav, cells.Index(4, i, 0), true);
                NavWorld.SetSolid(cells, nav, cells.Index(9, i, 0), true);
            }
            nav.Rebuild();

            int inside = cells.Index(6, 6, 0);
            int outside = cells.Index(1, 1, 0);

            Assert.That(nav.Grid.CanEnter(inside, Mode), Is.True, "the room has a floor to stand on");
            Assert.That(nav.Reachable(inside, outside, Mode), Is.False, "districts say it is sealed");

            var finder = new PathFinder(nav);
            Assert.That(finder.FindPath(inside, outside, Mode).Ok, Is.False,
                "and no diagonal cuts a way out of it");
        }

        /// <summary>
        /// <b>The claim the whole design rests on, and it is stronger than the corner rule.</b>
        ///
        /// <para>Over a board with walls scattered through it, take every diagonal that
        /// <em>either</em> reading of the corner rule would permit — ours, which wants both flanks
        /// open, and d-04's, which only forbids the step when both flanks block. In every single
        /// case the two ends are already joined by two orthogonal steps through an open flank.
        /// That is why no region, link, district or zone had to change when the search went
        /// 8-connected, why all three golden <c>Generated</c> hashes held, and why the
        /// four-connected oracle in <c>NavWorld.FloodFromCell</c> is still a correct oracle.</para>
        ///
        /// <para><b>It also corrects the reason this file first gave for the strict rule.</b> The
        /// argument written down on the day was that only the strict reading keeps a walled room
        /// sealed. That is false, and the loop below is what falsified it: if one flank is open
        /// then the open flank <em>is</em> an orthogonal way round, so the lax reading cannot
        /// unseal anything either. What the owner's choice actually buys is what it looks like —
        /// a colonist never drawn clipping through the corner of a wall — and a route that is not
        /// quoted at 141 for a move that squeezes through masonry. Worth having, and not the
        /// reachability guarantee it was first sold as.</para>
        /// </summary>
        [Test]
        public void NeitherReadingOfTheCornerRuleCanChangeWhatIsReachable()
        {
            NavGraph nav = ScatteredBoard(24, 20260918u, 30, out CellGrid cells);

            int strict = 0, laxOnly = 0;
            for (int z = 1; z < 23; z++)
            for (int x = 1; x < 23; x++)
            for (int sz = -1; sz <= 1; sz += 2)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                int from = cells.Index(x, z, 0);
                int to = cells.Index(x + sx, z + sz, 0);
                if (!nav.Grid.CanEnter(from, Mode) || !nav.Grid.CanWalkInto(to, Mode)) continue;

                int flankX = cells.Index(x + sx, z, 0);
                int flankZ = cells.Index(x, z + sz, 0);
                bool openX = nav.Grid.CanWalkInto(flankX, Mode);
                bool openZ = nav.Grid.CanWalkInto(flankZ, Mode);

                // d-04's reading: forbidden only when both flanks block.
                if (!openX && !openZ) continue;

                if (openX && openZ) strict++; else laxOnly++;

                // Whichever reading let it through, an open flank is a way round.
                int via = openX ? flankX : flankZ;
                Assert.That(nav.IsLegalStep(from, via, Mode), Is.True, "step onto the open flank");
                Assert.That(nav.IsLegalStep(via, to, Mode), Is.True, "and off it to the far cell");
            }

            Assert.That(strict, Is.GreaterThan(200), "the board must contain diagonals we allow");
            Assert.That(laxOnly, Is.GreaterThan(50),
                "and corner-squeezes we refuse but d-04 would have allowed, or the comparison " +
                "above is vacuous — this is the count that caught the first version of this file " +
                "running on an empty board");
        }

        /// <summary>
        /// The four-connected oracle, the districts and the search all still agree now that the
        /// search is eight-connected. This is <c>ReachabilityTests</c>'s question asked again for
        /// the one change that could have broken it.
        /// </summary>
        [Test]
        public void TheFourConnectedOracleStillAgreesWithTheSearch()
        {
            NavGraph nav = ScatteredBoard(24, 4242u, 30, out CellGrid cells);

            int start = -1;
            for (int i = 0; i < cells.Size.CellCount && start < 0; i++)
                if (nav.Grid.CanEnter(i, Mode)) start = i;
            Assume.That(start, Is.GreaterThanOrEqualTo(0));

            HashSet<int> flooded = NavWorld.FloodFromCell(nav, start, Mode);
            var finder = new PathFinder(nav);

            for (int i = 0; i < cells.Size.CellCount; i++)
            {
                if (!nav.Grid.CanEnter(i, Mode)) continue;
                bool oracle = flooded.Contains(i);

                Assert.That(nav.Reachable(start, i, Mode), Is.EqualTo(oracle),
                    $"district and oracle disagree about cell {i}");

                if (i != start)
                    Assert.That(finder.FindPath(start, i, Mode).Ok, Is.EqualTo(oracle),
                        $"the search and the oracle disagree about cell {i}");
            }
        }
    }
}
