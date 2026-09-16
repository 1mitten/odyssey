#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A marked cell of rock becomes a hole and a pile of stone, and the pile becomes stock.
    ///
    /// Sixteen layers, not the eight the felling tests use: with eight the heightfield clamp
    /// leaves a single layer of rock and there is nothing to mine into.
    /// </summary>
    public class MineJobTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Board(uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: true);
        }

        /// <summary>The nearest cell of plain rock that a colonist could actually get at.</summary>
        static int NearestRock(ColonyWorld colony, ushort terrain = NaturalContent.TerrainRock)
        {
            CellRef start = colony.Start;
            Pawn pawn = colony.Pawns.Pawns.All[0];
            int best = -1, bestDistance = int.MaxValue;

            for (int y = 0; y < Size.SizeY; y++)
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
            {
                int index = Size.Index(x, z, y);
                if (colony.Grid.Terrain[index] != terrain) continue;
                if (!colony.Designations.CanMine(index)) continue;
                if (MineWorkGiver.StandToMine(colony.Pawns, pawn, index) < 0) continue;

                int distance = System.Math.Abs(x - start.X) + System.Math.Abs(z - start.Z)
                             + System.Math.Abs(y - start.Y) * 4;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = index;
            }

            return best;
        }

        static int OnTheGround(ColonyWorld colony, int item)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == item) total += items[i].Stack;
            return total;
        }

        [Test]
        public void AMarkedCellIsDugOutAndTheOrderIsCleared()
        {
            ColonyWorld colony = Board();
            int rock = NearestRock(colony);
            Assume.That(rock, Is.GreaterThanOrEqualTo(0), "the board has reachable rock");

            colony.World.Intents.Submit(
                new Intent(IntentKind.Designate, Size.FromIndex(rock), (int)DesignationKind.Mine));
            colony.World.Tick();
            Assert.That(colony.World.Intents.Rejected, Is.Empty);

            int dugAt = -1;
            for (int tick = 0; tick < 12_000 && dugAt < 0; tick++)
            {
                colony.World.Tick();
                if (!colony.Grid.IsSolidTerrain(rock)) dugAt = colony.World.CurrentTick;
            }

            Assert.That(dugAt, Is.GreaterThan(0), "the cell was never dug out");
            Assert.That(colony.Grid.Terrain[rock], Is.EqualTo(NaturalContent.TerrainAir));
            Assert.That(colony.Designations.At(rock), Is.EqualTo(DesignationKind.None),
                "the order is cleared once carried out");
        }

        [Test]
        public void DiggingRevealsWhatTheWallsAreMadeOf()
        {
            ColonyWorld colony = Board();
            int rock = NearestRock(colony);
            Assume.That(rock, Is.GreaterThanOrEqualTo(0));

            Assert.That(colony.Grid.IsDiscovered(rock - 1), Is.False, "something was known before the dig");

            colony.World.Intents.Submit(
                new Intent(IntentKind.Designate, Size.FromIndex(rock), (int)DesignationKind.Mine));
            for (int tick = 0; tick < 12_000 && colony.Grid.IsSolidTerrain(rock); tick++) colony.World.Tick();
            Assume.That(colony.Grid.IsSolidTerrain(rock), Is.False, "the cell was never dug out");

            int revealed = 0;
            foreach (int neighbour in Around(rock))
                if (colony.Grid.IsDiscovered(neighbour)) revealed++;

            Assert.That(revealed, Is.GreaterThan(0), "the dig revealed none of its own walls");
        }

        static IEnumerable<int> Around(int cell)
        {
            yield return cell - 1;
            yield return cell + 1;
            yield return cell - Size.SizeX;
            yield return cell + Size.SizeX;
            yield return cell - Size.LayerStride;
            yield return cell + Size.LayerStride;
        }

        [Test]
        public void EveryCutCellGivesUpWhatItIsMadeOf()
        {
            // Called directly rather than mined, because the point is the rule and not the walk.
            //
            // It used to be "ore always, rock one time in four", and four was tuned against the
            // wrong denominator: the board holds eighty thousand cells of rock, but a player mines
            // the tens of cells they mark, and a dozen orders produced three piles of stone
            // against five hundred wood from the trees beside them. A playtest said the colony got
            // nothing out of the rock, and it was right.
            ColonyWorld colony = Board();
            PawnContext ctx = colony.Pawns;
            int stone = 0, iron = 0;

            for (int i = 0; i < 20; i++)
            {
                int cell = Size.Index(10 + i, 10, 4);

                Assert.That(MineJobDriver.Yield(ctx, cell, NaturalContent.TerrainRock, out int item, out int count),
                    Is.True, "a cell of rock gave up nothing");
                Assert.That(item, Is.EqualTo(ItemIndex.Stone));
                Assert.That(count, Is.EqualTo(ctx.Content.StonePerRock));
                stone++;

                Assert.That(MineJobDriver.Yield(ctx, cell, NaturalContent.TerrainIronOre, out int ore, out int oreCount),
                    Is.True, "an iron seam gave up nothing");
                Assert.That(ore, Is.EqualTo(ItemIndex.IronOre));
                Assert.That(oreCount, Is.EqualTo(ctx.Content.OrePerCell));
                iron++;
            }

            Assert.That(stone, Is.EqualTo(20));
            Assert.That(iron, Is.EqualTo(20), "a seam is never empty-handed");
        }

        [Test]
        public void ThePartialYieldDialStillWorksAndIsDecidedByTheCell()
        {
            // The dial was kept rather than deleted when the rate went to one-in-one, because the
            // machinery behind it is the part worth having: the roll is a pure function of (world
            // seed, cell index), so a partial yield can come back without reopening how it is
            // decided. A dial nothing exercises is a dial that has quietly stopped working.
            ColonyWorld colony = Board();
            PawnContext ctx = colony.Pawns;
            ctx.Content.StoneChanceOneIn = 4;

            // One tick first. PawnContext.Seed is filled in when a pawn system syncs, so before
            // the world has ticked at all it is still zero — and a roll taken against seed 0 and
            // then against the world's real seed disagrees for reasons that have nothing to do
            // with the cell. That is a trap for any test that asks the simulation a question
            // before running it.
            colony.World.Tick();

            int yielded = 0;
            for (int i = 0; i < 200; i++)
                if (MineJobDriver.Yield(ctx, Size.Index(10 + i % 40, 12 + i / 40, 4),
                        NaturalContent.TerrainRock, out _, out _))
                    yielded++;

            Assert.That(yielded, Is.InRange(20, 80), $"{yielded} of 200 is not about a quarter");

            // And still the cell's answer rather than the moment's.
            int cell = Size.Index(15, 15, 4);
            bool first = MineJobDriver.Yield(ctx, cell, NaturalContent.TerrainRock, out _, out _);
            colony.World.Tick(9);
            Assert.That(MineJobDriver.Yield(ctx, cell, NaturalContent.TerrainRock, out _, out _),
                Is.EqualTo(first), "the same cell answered differently a moment later");
        }

        [Test]
        public void SubsoilIsDugThroughAndLeavesNothing()
        {
            ColonyWorld colony = Board();
            int cell = Size.Index(10, 10, 4);
            Assert.That(MineJobDriver.Yield(colony.Pawns, cell, NaturalContent.TerrainSubsoil, out _, out _), Is.False);
            Assert.That(MineJobDriver.Yield(colony.Pawns, cell, NaturalContent.TerrainGrass, out _, out _), Is.False);
        }

        [Test]
        public void TheStoneRollBelongsToTheCellAndNotToTheMoment()
        {
            // The determinism property that matters: the same cell answers the same way whenever
            // it is asked, so a save, a reload or a re-ordering of the colony's work cannot
            // reroll it. A live stream would have made the yield depend on what else rolled dice
            // first that tick, and that surfaces as a resume divergence days later.
            ColonyWorld colony = Board();
            PawnContext ctx = colony.Pawns;

            for (int i = 0; i < 50; i++)
            {
                int cell = Size.Index(20 + i, 20, 5);
                bool first = MineJobDriver.Yield(ctx, cell, NaturalContent.TerrainRock, out _, out _);
                colony.World.Tick(7);
                bool later = MineJobDriver.Yield(ctx, cell, NaturalContent.TerrainRock, out _, out _);
                Assert.That(later, Is.EqualTo(first), $"cell {cell} answered differently seven ticks later");
            }
        }

        [Test]
        public void TheMinerWorksFromBesideTheCellWhereItCan()
        {
            ColonyWorld colony = Board();
            int rock = NearestRock(colony);
            Assume.That(rock, Is.GreaterThanOrEqualTo(0));
            CellRef at = Size.FromIndex(rock);

            colony.World.Intents.Submit(new Intent(IntentKind.Designate, at, (int)DesignationKind.Mine));

            Pawn? miner = null;
            for (int tick = 0; tick < 6_000 && miner == null; tick++)
            {
                colony.World.Tick();
                foreach (Pawn pawn in colony.Pawns.Pawns.All)
                    if (pawn.CurrentJob != null && pawn.CurrentJob.DefIndex == JobIndex.Mine) miner = pawn;
            }

            Assert.That(miner, Is.Not.Null, "nobody took the mining order");
            Job job = miner!.CurrentJob!;
            Assert.That(job.DestCell, Is.EqualTo(rock), "the rock is the destination");
            Assert.That(job.TargetCell, Is.Not.EqualTo(rock), "the stand is the cell being cut");

            CellRef stand = Size.FromIndex(job.TargetCell);
            Assert.That(System.Math.Abs(stand.X - at.X), Is.LessThanOrEqualTo(1));
            Assert.That(System.Math.Abs(stand.Z - at.Z), Is.LessThanOrEqualTo(1));
            Assert.That(stand.Y - at.Y, Is.InRange(0, 1), "the stand is neither below the cell nor two layers up");
        }

        /// <summary>
        /// Square on to the face, not round the corner, whenever a face is available at all
        /// (owner, 2026-09-16: "they should place themselves in front of the block").
        ///
        /// <para>A miner is <i>drawn</i> stepping in towards what it is cutting, and on a diagonal
        /// that step goes towards the block's corner — into the two cells sharing it, which when
        /// cutting a face are the ones most likely to be solid stone. Measured on the played board
        /// before the ring was ordered: 1,500 of 1,980 stances were diagonal and 961 of those had a
        /// face available and reachable anyway, lost only because a corner is often one step
        /// nearer. Afterwards: 539 diagonal, and <b>none</b> of them with a face going spare.</para>
        ///
        /// <para>This asserts the rule on a fixture that leaves every approach open, where the old
        /// nearest-first ranking would take whichever corner the walk happened to reach first.</para>
        /// </summary>
        [Test]
        public void AMinerStandsSquareOnToTheFaceWhenItCan()
        {
            ColonyWorld colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];

            int checkedCells = 0;
            for (int i = 0; i < Size.CellCount; i++)
            {
                if (!colony.Designations.CanMine(i)) continue;
                int stand = MineWorkGiver.StandToMine(colony.Pawns, pawn, i);
                if (stand < 0) continue;

                CellRef rock = Size.FromIndex(i);
                CellRef at = Size.FromIndex(stand);
                if (at.Y != rock.Y) continue;           // the rim and reaching-up stances
                if (at.X == rock.X || at.Z == rock.Z) continue;   // already square on

                // A corner was taken. Then no face may have been available on that layer.
                checkedCells++;
                foreach ((int dx, int dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    if (!Size.Contains(rock.X + dx, rock.Z + dz, rock.Y)) continue;
                    int face = Size.Index(rock.X + dx, rock.Z + dz, rock.Y);
                    if (!colony.Grid.IsWalkable(face)) continue;
                    Assert.That(
                        colony.Pawns.Nav.Reachable(pawn.Cell, face, Odyssey.Sim.Pathing.TraverseMode.Colonist),
                        Is.False,
                        $"the miner took the corner {at} to cut {rock} while the face {Size.FromIndex(face)} " +
                        "was walkable and reachable — it will step into whatever is beside the block");
                }
            }

            Assert.That(checkedCells, Is.GreaterThan(0),
                "no corner stance was taken anywhere on this board, so the rule was not exercised");
        }

        [Test]
        public void ADiggerDoesNotEndUpStandingOnNothing()
        {
            // Cutting downward means standing on the cell being cut away; the answer is to step
            // down into the hole. Whatever happens, no colonist may be left in a cell it could
            // not have walked into.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int under = Size.Index(start.X, start.Z, start.Y - 1);
            Assume.That(colony.Designations.CanMine(under), Is.True, "the ground under the start is minable");

            colony.World.Intents.Submit(
                new Intent(IntentKind.Designate, Size.FromIndex(under), (int)DesignationKind.Mine));
            colony.World.Tick(12_000);

            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                Assert.That(colony.Grid.IsWalkable(pawn.Cell), Is.True,
                    $"a colonist is standing in {Size.FromIndex(pawn.Cell)}, which cannot be stood in");
        }

        [Test]
        public void MiningIsDeterministic()
        {
            ColonyWorld first = Board(seed: 5u);
            ColonyWorld second = Board(seed: 5u);
            int rock = NearestRock(first);
            Assume.That(rock, Is.GreaterThanOrEqualTo(0));
            CellRef at = Size.FromIndex(rock);

            first.World.Intents.Submit(new Intent(IntentKind.Designate, at, (int)DesignationKind.Mine));
            second.World.Intents.Submit(new Intent(IntentKind.Designate, at, (int)DesignationKind.Mine));
            first.World.Tick(12_000);
            second.World.Tick(12_000);

            Assert.That(first.World.ComputeStateHash().Value, Is.EqualTo(second.World.ComputeStateHash().Value));
            Assert.That(first.Grid.IsSolidTerrain(rock), Is.False, "nothing was actually mined");
        }

        [Test]
        public void AMarkedStackIsWorkedFromTheTopDown()
        {
            // Mining the bottom of a marked stack first leaves the rock above it hanging in the
            // air: the generator's column check runs at generation only, and collapse is U29's
            // work. The starting order marks a whole outcrop, so this is the first thing a
            // playtest would see, not an edge case.
            // The played board rather than the 60-cell fixture: it carries 23 outcrops against
            // the fixture's 5, and an outcrop only makes a stack when it is more than one cell
            // tall. On the small board this test could find nothing to examine and skip itself
            // silently, which is worse than failing.
            //
            // **Across seeds, not on one.** The shape wanted here is rarer than it used to be:
            // a cell with solid rock on top of it can only be worked from a stance on its own
            // layer, because the rim stance refuses a rock whose ceiling is closed and the on-top
            // stance needs the cell above to be open. Seed 1 of the played board no longer offers
            // one, which says nothing at all about the rule. Walking a handful of seeds keeps the
            // test about the invariant rather than about a board.
            var size = new GridSize(120, 120, 16);
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;

            ColonyWorld? colony = null;
            int lower = -1, upper = -1;
            for (uint seed = 1; seed <= 8 && lower < 0; seed++)
            {
                ColonyWorld board = ColonyWorld.Build(size, seed, scenario, barren: true, wooded: true);
                Pawn pawn = board.Pawns.Pawns.All[0];

                for (int y = 0; y < size.SizeY - 1 && lower < 0; y++)
                for (int z = 0; z < size.SizeZ && lower < 0; z++)
                for (int x = 0; x < size.SizeX && lower < 0; x++)
                {
                    int cell = size.Index(x, z, y);
                    int above = cell + size.LayerStride;
                    // The shape that matters: a lower cell a colonist can get at *while the cell
                    // above it is still there*. A tapering outcrop never produces one — the ring
                    // below a peak has rock on every side until the peak goes, so a mound is
                    // worked top-down by its own geometry. A terrace step does produce one, and
                    // that is the case where the bottom can be cut out from the side and leave
                    // the top hanging.
                    if (!board.Designations.CanMine(cell)) continue;
                    if (!board.Grid.IsSolidTerrain(above)) continue;
                    if (!board.Designations.CanMine(above)) continue;
                    if (MineWorkGiver.StandToMine(board.Pawns, pawn, cell) < 0) continue;
                    lower = cell;
                    upper = above;
                    colony = board;
                }
            }

            Assert.That(lower, Is.GreaterThanOrEqualTo(0),
                "no reachable cell with solid rock directly above it on any of eight played boards, " +
                "so this proved nothing");
            Assert.That(colony, Is.Not.Null);

            colony!.Designations.Designate(size.FromIndex(lower), DesignationKind.Mine);
            colony.Designations.Designate(size.FromIndex(upper), DesignationKind.Mine);

            // The invariant, stated so it holds whether or not the top is ever reachable: the
            // bottom is never cut out while the top is still standing on it. If the top cannot be
            // got at, both orders simply wait, which is the right answer and not a floating rock.
            for (int tick = 0; tick < 12_000; tick++)
            {
                colony.World.Tick();
                if (!colony.Grid.IsSolidTerrain(lower) && colony.Grid.IsSolidTerrain(upper))
                    Assert.Fail($"the cell under {size.FromIndex(upper)} was cut away while it was still there");
                if (!colony.Grid.IsSolidTerrain(upper)) break;
            }
        }

        [Test]
        public void AMinerTellsPresentationWhatItIsSwingingAt()
        {
            // This was missing and nothing failed. A miner published Working = false, so the
            // figure stood at the rock face with its arms down through the whole job — no swing,
            // no chips — while every test here passed, because the simulation was perfectly
            // correct and only the half of the contract that presentation reads was absent.
            // It cost a playtest to notice, which is what a pinned contract is for.
            ColonyWorld colony = Board();
            int rock = NearestRock(colony);
            Assume.That(rock, Is.GreaterThanOrEqualTo(0));

            colony.World.Intents.Submit(
                new Intent(IntentKind.Designate, Size.FromIndex(rock), (int)DesignationKind.Mine));

            Pawn? miner = null;
            bool sawWalkingEmptyHanded = false;
            bool sawWorkingTheRock = false;

            for (int tick = 0; tick < 12_000 && !sawWorkingTheRock; tick++)
            {
                colony.World.Tick();
                foreach (Pawn pawn in colony.Pawns.Pawns.All)
                {
                    if (pawn.CurrentJob == null || pawn.CurrentJob.DefIndex != JobIndex.Mine) continue;
                    miner = pawn;

                    int focus = pawn.Driver!.WorkFocus;
                    if (focus < 0) sawWalkingEmptyHanded = true;
                    else
                    {
                        Assert.That(focus, Is.EqualTo(rock),
                            "the miner is facing something other than the cell it is cutting");
                        sawWorkingTheRock = true;
                    }
                }
            }

            Assert.That(miner, Is.Not.Null, "nobody took the mining order");
            Assert.That(sawWalkingEmptyHanded, Is.True, "a colonist crossing the map should carry nothing");
            Assert.That(sawWorkingTheRock, Is.True, "the miner never told presentation it was working");
        }

        [Test, Category("Long")]
        public void ADayOfMiningLeavesAWorkingColony()
        {
            // A dozen orders prove the mechanism; a day proves it does not rot. The faults this
            // is for take hours rather than seconds — a reservation held by a job that ended, a
            // colonist that walked into a shaft and stopped being part of the colony, an order
            // nobody can ever take because its stand was dug away.
            ColonyWorld colony = Board();
            Pawn first = colony.Pawns.Pawns.All[0];
            int marked = 0;

            for (int y = 0; y < Size.SizeY && marked < 80; y++)
            for (int z = 0; z < Size.SizeZ && marked < 80; z++)
            for (int x = 0; x < Size.SizeX && marked < 80; x++)
            {
                int index = Size.Index(x, z, y);
                if (colony.Grid.Terrain[index] != NaturalContent.TerrainRock) continue;
                if (!colony.Designations.CanMine(index)) continue;
                if (MineWorkGiver.StandToMine(colony.Pawns, first, index) < 0) continue;
                if (colony.Designations.Designate(Size.FromIndex(index), DesignationKind.Mine) == IntentRejection.None)
                    marked++;
            }

            Assume.That(marked, Is.GreaterThan(20), "not enough reachable rock to be worth a day");
            colony.World.Tick(60_000);

            Assert.That(colony.Jobs.CompletedOf(JobIndex.Mine), Is.GreaterThan(0), "not one mining job completed");

            // A day is not long enough to finish: three colonists against 65 orders got 56 of them
            // out, and the rest came out on the second day. So the day's assertion is the one this
            // test is actually for — that no order has ROTTED — and finishing is asserted after the
            // second day, where it belongs.
            //
            // A rotted order is one nobody can ever take again: its stand was dug away, or the cut
            // became one the miner could not get out of. It is indistinguishable from unfinished
            // work by counting, and perfectly distinguishable by asking whether a colonist could
            // still be given it.
            foreach (int cell in colony.Designations.Cells)
            {
                if (colony.Designations.At(cell) != DesignationKind.Mine) continue;
                Assert.That(colony.Designations.CanMine(cell), Is.True,
                    $"the order at {Size.FromIndex(cell)} can no longer be carried out at all");
                Assert.That(MineWorkGiver.StandToMine(colony.Pawns, first, cell),
                    Is.GreaterThanOrEqualTo(0),
                    $"the order at {Size.FromIndex(cell)} has nowhere left to stand: it has rotted");
            }

            colony.World.Tick(60_000);
            Assert.That(colony.Designations.Count, Is.Zero, "orders were left standing after two days");

            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            {
                Assert.That(colony.Grid.IsWalkable(pawn.Cell), Is.True,
                    $"a colonist is standing in {Size.FromIndex(pawn.Cell)}, which cannot be stood in");
                Assert.That(pawn.HeldReservations.Count,
                    pawn.CurrentJob == null ? Is.EqualTo(0) : Is.GreaterThanOrEqualTo(0),
                    "a colonist with no job is still holding a claim");
            }

            Assert.That(OnTheGround(colony, ItemIndex.Stone), Is.GreaterThan(0), "a day of mining left no stone");
        }

        [Test]
        public void MinedStoneIsHauledToTheStockpile()
        {
            ColonyWorld colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            int marked = 0;

            // A seam of orders rather than one, because three cells in four leave nothing and a
            // single order would make this test a dice roll.
            for (int y = 0; y < Size.SizeY && marked < 12; y++)
            for (int z = 0; z < Size.SizeZ && marked < 12; z++)
            for (int x = 0; x < Size.SizeX && marked < 12; x++)
            {
                int index = Size.Index(x, z, y);
                if (colony.Grid.Terrain[index] != NaturalContent.TerrainRock) continue;
                if (!colony.Designations.CanMine(index)) continue;
                if (MineWorkGiver.StandToMine(colony.Pawns, pawn, index) < 0) continue;
                if (colony.Designations.Designate(Size.FromIndex(index), DesignationKind.Mine) == IntentRejection.None)
                    marked++;
            }

            Assume.That(marked, Is.GreaterThan(0), "no reachable rock to mark");
            colony.World.Tick(60_000);

            Assert.That(OnTheGround(colony, ItemIndex.Stone), Is.GreaterThan(0), "a dozen cells of rock left no stone");

            int stocked = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Despawned || item.DefIndex != ItemIndex.Stone || item.Cell < 0) continue;
                if (colony.Pawns.Items.IsStockpileCell(item.Cell)) stocked++;
            }

            Assert.That(stocked, Is.GreaterThan(0), "mined stone never reached the stockpile");
        }
    }
}
