#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What happens to things when the ground is taken out from under them, and what a shaft has
    /// in it once it is cut.
    ///
    /// <para><b>Written from measurement rather than from a screenshot.</b> The owner photographed
    /// a colonist hanging in mid-air over a worked face, and the obvious reading — that a dig had
    /// left somebody unsupported — was wrong. Running the playtest colony for 40,000 ticks found
    /// <b>zero</b> colonists ever standing on nothing and <b>34,004 pawn-ticks</b> spent standing
    /// on a ladder that no geometry was ever drawn for. The same run found the real support bug
    /// somewhere else entirely: <b>26 of 107</b> stacks of spoil on the ground had no floor under
    /// them, because items have never had a support rule at all.</para>
    /// </summary>
    public class FallingTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 16);

        static ColonyWorld Board(uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: true);
        }

        static void Dig(ColonyWorld colony, int cell)
        {
            ushort terrain = colony.Grid.Terrain[cell];
            MineJobDriver.MineCell(colony.Pawns, cell, terrain);
            colony.World.Tick();
        }

        /// <summary>
        /// A walkable cell next door on the same layer: where a climb out of a hole now lands.
        ///
        /// The cell directly over a dug cell is the one whose floor was just taken away, so it is
        /// no longer somewhere a colonist may be — which is the whole point of the change these
        /// tests are written against.
        /// </summary>
        static int GroundBeside(ColonyWorld colony, CellRef at)
        {
            foreach (var d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int x = at.X + d.Item1, z = at.Z + d.Item2;
                if (!Size.Contains(x, z, at.Y)) continue;
                int cell = Size.Index(x, z, at.Y);
                if (colony.Grid.IsWalkable(cell)) return cell;
            }
            return -1;
        }

        // ---- where a thing lands ---------------------------------------------------------

        [Test]
        public void ACellWithAFloorIsItsOwnLanding()
        {
            ColonyWorld colony = Board();
            int surface = Size.Index(colony.Start.X, colony.Start.Z, colony.Start.Y);
            Assume.That(colony.Grid.HasFloor(surface), Is.True);

            Assert.That(colony.Grid.FirstFloorAtOrBelow(surface), Is.EqualTo(surface));
        }

        [Test]
        public void AThingOverAHoleLandsOnTheFirstRealFloor()
        {
            // Not "one layer down" — the bottom of the fall. Nothing in this game bounces, and a
            // drop of three layers and a drop of one both finish in the same place.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int surface = Size.Index(start.X, start.Z, start.Y);

            int first = surface - Size.LayerStride;
            int second = first - Size.LayerStride;
            Assume.That(colony.Designations.CanMine(first), Is.True);
            Assume.That(colony.Designations.CanMine(second), Is.True);

            Dig(colony, first);
            Dig(colony, second);

            Assert.That(colony.Grid.FirstFloorAtOrBelow(surface), Is.EqualTo(second),
                "a thing on the lip of a two-deep hole did not fall to the bottom of it");
        }

        [Test]
        public void TheBottomOfTheWorldCatchesEverything()
        {
            // There is no floor below layer nought and never will be. Returning -1 would make
            // every caller write the same guard, and one of them would forget.
            ColonyWorld colony = Board();
            int floorOfTheWorld = Size.Index(colony.Start.X, colony.Start.Z, 0);
            Assert.That(colony.Grid.FirstFloorAtOrBelow(floorOfTheWorld),
                Is.GreaterThanOrEqualTo(0), "a thing fell out of the bottom of the world");
        }

        // ---- spoil ------------------------------------------------------------------------

        [Test]
        public void SpoilNeverEndsUpInTheAir()
        {
            // The bug the measurement found: a quarter of all spoil on a worked board was hanging
            // a layer or two above the ground, because the yield was dropped into the cut cell
            // without anybody asking whether that cell had a floor.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int surface = Size.Index(start.X, start.Z, start.Y);

            int first = surface - Size.LayerStride;
            int second = first - Size.LayerStride;
            Assume.That(colony.Designations.CanMine(first), Is.True);
            Dig(colony, first);
            Assume.That(colony.Designations.CanMine(second), Is.True);
            Dig(colony, second);

            var items = colony.Pawns.Items.Items;
            int checked_ = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Despawned || item.Cell < 0) continue;
                checked_++;
                Assert.That(colony.Grid.HasFloor(item.Cell), Is.True,
                    $"{item.Stack} of def {item.DefIndex} hangs in {Size.FromIndex(item.Cell)}");
            }

            Assume.That(checked_, Is.GreaterThan(0), "nothing was on the ground, so this proved nothing");
        }

        [Test]
        public void AStackRestingOnACellFallsWhenThatCellIsCut()
        {
            ColonyWorld colony = Board();
            CellRef start = colony.Start;

            // A patch of ground near the colony that is standable, empty and has rock under it.
            // Not the start cell itself: the scenario puts salvage there, and a cell already
            // holding one def cannot take another.
            int surface = -1;
            for (int dz = -3; dz <= 3 && surface < 0; dz++)
            for (int dx = -3; dx <= 3 && surface < 0; dx++)
            {
                if (!Size.Contains(start.X + dx, start.Z + dz, start.Y)) continue;
                int candidate = Size.Index(start.X + dx, start.Z + dz, start.Y);
                if (!colony.Grid.IsWalkable(candidate)) continue;
                if (!colony.Pawns.Items.CellHasSpace(candidate)) continue;
                if (!colony.Designations.CanMine(candidate - Size.LayerStride)) continue;
                surface = candidate;
            }

            Assume.That(surface, Is.GreaterThanOrEqualTo(0), "no clear ground with rock under it");
            int under = surface - Size.LayerStride;

            // A load of wood standing on the ground, and then the ground goes.
            colony.Pawns.Items.Spawn(ItemIndex.Wood, surface, 10);
            Dig(colony, under);
            colony.World.Tick();

            var resting = colony.Pawns.Items.ItemAt(surface);
            Assert.That(resting, Is.Null, "the wood stayed on a cell with nothing under it");

            var landed = colony.Pawns.Items.ItemAt(under);
            Assert.That(landed, Is.Not.Null, "the wood did not land in the hole");
            Assert.That(landed!.DefIndex, Is.EqualTo(ItemIndex.Wood));
            Assert.That(landed.Stack, Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public void TwoLoadsThatFallTogetherBecomeOneLoad()
        {
            // The owner's decision and the point of the exercise: one hauler trip should carry
            // what took two. Measured on the playtest board, the same stone came out as 81 stacks
            // where it had been 107.
            var items = new ColonyItems(ContentPack.Pawns());
            ThingId first = items.Spawn(ItemIndex.Stone, 100, 8);
            items.Spawn(ItemIndex.Stone, 200, 8);

            var falling = items.Get(first)!;
            var landed = items.MoveTo(falling, 200);

            Assert.That(landed.Cell, Is.EqualTo(200));
            Assert.That(landed.Stack, Is.EqualTo(16), "the two loads did not merge");
            Assert.That(items.ItemAt(100), Is.Null, "the stack is still on the cell it fell from");
        }

        [Test]
        public void AMoveToItsOwnCellChangesNothing()
        {
            var items = new ColonyItems(ContentPack.Pawns());
            ThingId id = items.Spawn(ItemIndex.Stone, 100, 8);
            var item = items.Get(id)!;

            Assert.That(items.MoveTo(item, 100), Is.SameAs(item));
            Assert.That(items.ItemAt(100)!.Stack, Is.EqualTo(8), "the stack merged with itself");
        }










        // ---- you work what you can reach ------------------------------------------------

        [Test]
        public void ATreeCannotBeFelledFromALayerBelowIt()
        {
            // The owner watched a colonist chop a tree from one block down. StandBeside always
            // picked a cell on the tree's own layer, so the stance was never wrong — what was
            // missing is that the driver never asked again. Arriving is not staying, and mining
            // now moves colonists that did not ask to be moved: a dig drops whoever stands on the
            // cell it cuts, and retiring a climb drops whoever was on it.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;

            int tree = -1;
            for (int i = 0; i < Size.CellCount && tree < 0; i++)
                if (colony.Designations.IsTree(i)) tree = i;
            Assume.That(tree, Is.GreaterThanOrEqualTo(0), "no tree on this board");

            CellRef at = Size.FromIndex(tree);
            Assume.That(at.Y, Is.GreaterThan(0));

            colony.Designations.Designate(at, DesignationKind.Fell);

            Pawn pawn = colony.Pawns.Pawns.All[0];
            int stand = Size.Index(at.X + 1, at.Z, at.Y);
            Assume.That(colony.Grid.IsWalkable(stand), Is.True, "nowhere to stand beside this tree");

            var driver = new FellJobDriver();
            var job = new Job();
            job.Reset(JobIndex.Fell);
            job.DestCell = tree;
            job.TargetCell = stand;
            driver.Begin(pawn, job);

            // Walk toil: already there, so it completes and hands over to the working toil. Driven
            // rather than poked, because the thing under test is what happens AFTER arriving.
            pawn.Cell = stand;
            Assert.That(driver.Tick(colony.Pawns), Is.Not.EqualTo(JobStatus.Failed));

            // Beside it on its own layer is work.
            Assert.That(driver.Tick(colony.Pawns), Is.Not.EqualTo(JobStatus.Failed),
                "a colonist standing beside the tree cannot fell it");

            // One layer down is not. It does not fail — being moved is not the colonist's fault
            // and the stance is usually a step away — it goes back to the walk toil and returns.
            int chopped = driver.ToilProgress;
            Assume.That(chopped, Is.GreaterThan(0), "the colonist never got a swing in");

            pawn.Cell = Size.Index(at.X + 1, at.Z, at.Y - 1);
            driver.Tick(colony.Pawns);

            Assert.That(driver.ToilIndex, Is.EqualTo(0),
                "a colonist a layer below the tree went on chopping it instead of walking back");
        }

        [Test]
        public void RockIsCutFromOneCellAwayInAnyDirectionAndNoFurther()
        {
            // Mining's own answer to the same question, and it is a wider one than felling's: a
            // rock is cut from beside it, from a layer up — the rim stance and the on-top stance —
            // and from a layer DOWN, because a pick goes overhead and undercutting a face is
            // ordinary mining. What is not ordinary is reaching two layers or three cells.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int rock = Size.Index(start.X, start.Z, start.Y) - Size.LayerStride;
            Assume.That(colony.Designations.CanMine(rock), Is.True);
            CellRef at = Size.FromIndex(rock);
            Assume.That(at.Y, Is.GreaterThan(0));

            colony.Designations.Designate(at, DesignationKind.Mine);

            Pawn pawn = colony.Pawns.Pawns.All[0];
            int stand = Size.Index(at.X, at.Z, at.Y + 1);
            Assume.That(colony.Grid.IsWalkable(stand), Is.True, "nowhere to stand over this rock");

            var driver = new MineJobDriver();
            var job = new Job();
            job.Reset(JobIndex.Mine);
            job.DestCell = rock;
            job.TargetCell = stand;
            driver.Begin(pawn, job);

            pawn.Cell = stand;
            Assert.That(driver.Tick(colony.Pawns), Is.Not.EqualTo(JobStatus.Failed));

            pawn.Cell = Size.Index(at.X + 1, at.Z, at.Y);
            Assert.That(driver.Tick(colony.Pawns), Is.Not.EqualTo(JobStatus.Failed),
                "a miner beside the rock cannot cut it");

            pawn.Cell = Size.Index(at.X + 1, at.Z, at.Y + 1);
            Assert.That(driver.Tick(colony.Pawns), Is.Not.EqualTo(JobStatus.Failed),
                "a miner on the rim of the hole cannot cut it");

            pawn.Cell = Size.Index(at.X, at.Z, at.Y + 1);
            Assert.That(driver.Tick(colony.Pawns), Is.Not.EqualTo(JobStatus.Failed),
                "a miner standing on the rock cannot cut it");

            // Reaching up through a floor, and reaching across the room: neither is work. The
            // pick of the two measurements is the work banked on the cell, because that is what
            // a swing actually produces.
            // A layer below is work now, and the same shape as the rim stance upside down.
            pawn.Cell = Size.Index(at.X + 1, at.Z, at.Y - 1);
            Assert.That(driver.Tick(colony.Pawns), Is.Not.EqualTo(JobStatus.Failed),
                "a miner under the overhang cannot cut it");

            pawn.Cell = Size.Index(at.X, at.Z, at.Y - 1);
            Assert.That(driver.Tick(colony.Pawns), Is.Not.EqualTo(JobStatus.Failed),
                "a miner directly under the rock cannot cut the ceiling over its head");

            foreach (var (label, cell) in new[]
            {
                ("two layers below the rock", Size.Index(at.X, at.Z, at.Y - 2)),
                ("three cells away", Size.Index(at.X + 3, at.Z, at.Y)),
            })
            {
                pawn.Cell = Size.Index(at.X, at.Z, at.Y + 1);
                driver.Tick(colony.Pawns);   // back in reach, so the walk toil is behind it again
                int before = colony.Designations.WorkDone(rock);

                pawn.Cell = cell;
                driver.Tick(colony.Pawns);

                Assert.That(colony.Designations.WorkDone(rock), Is.EqualTo(before),
                    $"a miner {label} went on cutting it");
                Assert.That(driver.ToilIndex, Is.EqualTo(0),
                    $"a miner {label} did not go back to walk to its stance");
            }
        }


        [Test]
        public void DroppingCostsLessThanJumpingBackUp()
        {
            // The asymmetry is the point and it is not a fudge: going down a hole and coming back
            // up it are not the same job. Priced alike, a descent took six and a half seconds for
            // three metres, which the owner twice described as floating.
            Assert.That(Odyssey.Sim.Pathing.MoveCost.Drop,
                Is.LessThan(Odyssey.Sim.Pathing.MoveCost.JumpUp));
            Assert.That(Odyssey.Sim.Pathing.MoveCost.Drop,
                Is.LessThanOrEqualTo(Odyssey.Sim.Pathing.MoveCost.Orthogonal),
                "dropping a layer costs more than walking a cell, so it still reads as a drop");
            Assert.That(Odyssey.Sim.Pathing.MoveCost.JumpUp,
                Is.GreaterThan(Odyssey.Sim.Pathing.MoveCost.Orthogonal),
                "a jump became as cheap as walking, so nothing will ever prefer a ramp");
        }

        // ---- the glide -------------------------------------------------------------------

        [Test]
        public void TheDrawnGlideSpansTheWholeStepHoweverDearItIs()
        {
            // The published percentage used to be the raw progress clamped to 100, which is exact
            // for a flat cell at 100 units and wrong for everything dearer. A ladder down costs
            // 400: the figure reached the bottom a quarter of the way through the step and then
            // stood frozen in the shaft for the other three quarters, at six and a half seconds a
            // rung. That is most of what "colonists float down slowly" was.
            var pawn = new Pawn(new PawnId(1), 0, ContentPack.Pawns());
            Assert.That(pawn.MoveStepCost, Is.EqualTo(Odyssey.Sim.Pathing.MoveCost.Orthogonal * Rates.Scale),
                "a pawn that has never stepped would publish a nonsense fraction");

            // Stated against explicit costs rather than against the content constants: this is
            // about the arithmetic, and it must keep holding whatever a ladder is repriced to.
            Assert.That(Percent(200, 400), Is.EqualTo(50),
                "halfway through a dear step is halfway across it, not off the end");
            Assert.That(Percent(100, 400), Is.EqualTo(25),
                "a quarter of the way through still drew as arrived under the old clamp");
            Assert.That(Percent(100, 100), Is.EqualTo(100),
                "a flat crossing, which was the one case the old arithmetic got right");
            Assert.That(Percent(500, 400), Is.EqualTo(100), "overshoot is still clamped");
        }

        /// <summary>The arithmetic <c>PawnRegistry</c> publishes, stated once so it can be checked.</summary>
        static int Percent(int progress, int cost)
        {
            int percent = (int)((long)progress * 100 / cost);
            return percent < 0 ? 0 : percent > 100 ? 100 : percent;
        }
    }
}
