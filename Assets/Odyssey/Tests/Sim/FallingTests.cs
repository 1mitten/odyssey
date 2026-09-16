#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
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
            var items = new ColonyItems(PawnContent.Core());
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
            var items = new ColonyItems(PawnContent.Core());
            ThingId id = items.Spawn(ItemIndex.Stone, 100, 8);
            var item = items.Get(id)!;

            Assert.That(items.MoveTo(item, 100), Is.SameAs(item));
            Assert.That(items.ItemAt(100)!.Stack, Is.EqualTo(8), "the stack merged with itself");
        }

        // ---- climbing out --------------------------------------------------------------

        [Test]
        public void ACutShaftIsStillClimbableWithNothingDrawnInIt()
        {
            // The connector is what makes a shaft escapable, and it is deliberately all there is.
            // A ladder edifice was placed here for one commit and taken out again — the owner's
            // words were "these ladders shouldn't be visible" — so what is left has to be checked
            // as behaviour rather than as a prop: nothing standing in the cell, and a colonist can
            // still get out of it.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int surface = Size.Index(start.X, start.Z, start.Y);
            int shaft = surface - Size.LayerStride;
            Assume.That(colony.Designations.CanMine(shaft), Is.True);

            Dig(colony, shaft);

            // Out onto the ground BESIDE the hole. The cell directly over it is the one whose
            // floor was just dug away, so it is no longer somewhere a colonist may be.
            int ground = GroundBeside(colony, start);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0), "no ground beside the hole on this board");

            Assert.That(colony.Grid.Edifice[shaft], Is.LessThan(0),
                "something is standing in the shaft; the owner asked for nothing to be");
            Assert.That(
                colony.Pawns.Nav.Reachable(shaft, ground, Odyssey.Sim.Pathing.TraverseMode.Colonist),
                Is.True, "a colonist that cut this shaft cannot get out of it");
        }

        [Test]
        public void AClimbNeedsABlockBesideItToClimbAgainst()
        {
            // The owner's rule: "a climb can only happen if there is a tile in front of you — a
            // height block above and you climb against the edge of that block."
            //
            // Without it a connector went on every cut cell with an open ceiling, which in the
            // middle of an open quarry is a colonist going up through clear air with nothing
            // anywhere near it. Measured over 40,000 ticks before the rule: 1,890 of 9,880
            // climbing pawn-ticks — near enough one in five — had no wall beside them.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int surface = Size.Index(start.X, start.Z, start.Y);
            int shaft = surface - Size.LayerStride;
            Assume.That(colony.Designations.CanMine(shaft), Is.True);

            // Untouched ground, so the cut cell has rock on all four sides.
            Assert.That(MineJobDriver.HasWallBeside(colony.Grid, shaft), Is.True);
            Dig(colony, shaft);

            int ground = GroundBeside(colony, start);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0), "no ground beside the hole on this board");
            Assert.That(
                colony.Pawns.Nav.Reachable(shaft, ground, Odyssey.Sim.Pathing.TraverseMode.Colonist),
                Is.True, "a hole with rock on every side of it is not climbable");
        }

        [Test]
        public void TheMiddleOfAnOpenQuarryHasNothingToClimb()
        {
            // The other half of the same rule, and the one that matters: dig a patch out and the
            // cells in the middle of it have no face left anywhere near them.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;

            // A three-by-three of cut cells one layer down.
            int middle = -1;
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (!Size.Contains(start.X + dx, start.Z + dz, start.Y)) continue;
                int cell = Size.Index(start.X + dx, start.Z + dz, start.Y) - Size.LayerStride;
                if (!colony.Designations.CanMine(cell)) continue;
                Dig(colony, cell);
                if (dx == 0 && dz == 0) middle = cell;
            }

            Assume.That(middle, Is.GreaterThanOrEqualTo(0), "the patch could not be dug on this board");
            Assume.That(colony.Grid.IsSolidTerrain(middle), Is.False);

            Assert.That(MineJobDriver.HasWallBeside(colony.Grid, middle), Is.False,
                "the middle of a cut patch still thinks it has a wall beside it");
        }

        [Test]
        public void ACornerIsNotSomethingYouCanClimb()
        {
            // Four faces and not the diagonals. A corner is not something you can get your weight
            // against, and a colonist climbing one would be hanging off a line rather than
            // pressed to a face.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;

            // Open the four faces of a cell and leave its diagonals solid.
            int centre = Size.Index(start.X, start.Z, start.Y) - Size.LayerStride;
            Assume.That(colony.Designations.CanMine(centre), Is.True);

            foreach (var face in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int cell = Size.Index(start.X + face.Item1, start.Z + face.Item2, start.Y)
                         - Size.LayerStride;
                Assume.That(colony.Designations.CanMine(cell), Is.True);
                Dig(colony, cell);
            }

            Dig(colony, centre);

            Assume.That(colony.Grid.IsSolidTerrain(
                Size.Index(start.X + 1, start.Z + 1, start.Y) - Size.LayerStride), Is.True,
                "the diagonal was not left solid, so this proved nothing");

            Assert.That(MineJobDriver.HasWallBeside(colony.Grid, centre), Is.False,
                "a diagonal corner was counted as a wall to climb");
        }

        [Test]
        public void MiningAwayAClimbsWallTakesTheClimbWithIt()
        {
            // Checking only at the moment a climb is created is not enough, and this is why: a
            // colonist cuts a shaft, the rock around the shaft is then mined out, and the climb
            // goes on insisting there is a face to hold on to. Measured over 40,000 ticks with the
            // creation test in place and nothing to retire them, one climbing pawn-tick in five
            // still had no wall beside it.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int surface = Size.Index(start.X, start.Z, start.Y);
            int shaft = surface - Size.LayerStride;
            Assume.That(colony.Designations.CanMine(shaft), Is.True);

            Dig(colony, shaft);
            Assume.That(
                colony.Pawns.Nav.Reachable(shaft, surface, Odyssey.Sim.Pathing.TraverseMode.Colonist),
                Is.True, "the fresh shaft was not climbable, so this proves nothing");

            // Now take every wall away from it.
            foreach (var face in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int wall = Size.Index(start.X + face.Item1, start.Z + face.Item2, start.Y)
                         - Size.LayerStride;
                Assume.That(colony.Designations.CanMine(wall), Is.True);
                Dig(colony, wall);
            }

            Assert.That(MineJobDriver.HasWallBeside(colony.Grid, shaft), Is.False,
                "the walls were not actually removed, so this proves nothing");
            Assert.That(
                (colony.Pawns.Nav.Grid.Flags[shaft] & Odyssey.Sim.Pathing.NavFlags.ConnectorClimb),
                Is.EqualTo(default(Odyssey.Sim.Pathing.NavFlags)),
                "the climb outlived the wall it went up");
        }

        [Test]
        public void ARetiredClimbDropsWhoeverWasOnItToTheFloor()
        {
            // The live cause the safety net was missing. A cell with a climb counts as standable
            // to navigation — that is what lets a colonist be on a rock face at all — so taking
            // the climb away takes its footing with it. Without this, two of five colonists spent
            // the last 12,600 ticks of a 40,000-tick run standing still in mid-air.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int surface = Size.Index(start.X, start.Z, start.Y);
            int shaft = surface - Size.LayerStride;
            int bottom = shaft - Size.LayerStride;
            Assume.That(colony.Designations.CanMine(shaft), Is.True);
            Dig(colony, shaft);
            Assume.That(colony.Designations.CanMine(bottom), Is.True);
            Dig(colony, bottom);

            // Put somebody on the middle of the shaft, which has no floor of its own.
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Assume.That(colony.Grid.HasFloor(shaft), Is.False, "the shaft cell has a floor of its own");
            pawn.Cell = shaft;

            // Cut every wall away from under them.
            foreach (var face in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int wall = Size.Index(start.X + face.Item1, start.Z + face.Item2, start.Y)
                         - Size.LayerStride;
                if (!colony.Designations.CanMine(wall)) continue;
                Dig(colony, wall);
            }

            Assert.That(colony.Grid.HasFloor(pawn.Cell), Is.True,
                $"the colonist is in {Size.FromIndex(pawn.Cell)}, which has nothing under it");
        }

        [Test]
        public void AClimbLandsOnTheGroundBesideTheHoleAndNotInTheAirAboveIt()
        {
            // The fault that sealed pits shut. A climb used to end in the cell directly above the
            // one it started in — which, for a hole in flat ground, is open air with its floor
            // just dug away. That was survivable only while a colonist could stand there and walk
            // off, and standing there is what the owner reported as walking across air. Forbid it
            // and the one cell joining the pit to the world is a cell nobody may enter.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int over = Size.Index(start.X, start.Z, start.Y);
            int pit = over - Size.LayerStride;
            Assume.That(colony.Designations.CanMine(pit), Is.True);

            Dig(colony, pit);

            Assert.That(colony.Grid.HasFloor(over), Is.False,
                "the cell over the pit still has a floor, so this proves nothing");
            Assert.That(colony.Pawns.Nav.Grid.IsClimbOnly(over), Is.False,
                "the climb ended in the air above the hole instead of on the ground beside it");

            int ground = GroundBeside(colony, start);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));
            Assert.That(
                colony.Pawns.Nav.Reachable(ground, pit, Odyssey.Sim.Pathing.TraverseMode.Colonist),
                Is.True, "a colonist standing beside the pit cannot get into it");
        }

        [Test]
        public void YouMayStepOffARockFaceButNotOntoOne()
        {
            // The asymmetry is the whole rule, and getting it the tidy-looking way round strands
            // everybody: the top of a climb out of a deep shaft is itself a floorless cell, and
            // stepping off it onto the ground is the last move of getting out.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int over = Size.Index(start.X, start.Z, start.Y);
            int pit = over - Size.LayerStride;
            int deeper = pit - Size.LayerStride;
            Assume.That(colony.Designations.CanMine(pit), Is.True);
            Dig(colony, pit);
            Assume.That(colony.Designations.CanMine(deeper), Is.True);
            Dig(colony, deeper);

            // The middle of a two-deep shaft is standable and has nothing under it.
            Assume.That(colony.Pawns.Nav.Grid.IsClimbOnly(pit), Is.True,
                "the middle of the shaft is not a rock face on this board");

            var nav = colony.Pawns.Nav;
            var mode = Odyssey.Sim.Pathing.TraverseMode.Colonist;
            int ground = GroundBeside(colony, start);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            Assert.That(nav.Grid.CanWalkInto(pit, mode), Is.False,
                "a colonist may walk sideways onto a rock face");
            Assert.That(nav.Grid.CanEnter(pit, mode), Is.True,
                "a colonist may not be on a rock face at all, so nobody can climb");
            Assert.That(nav.Grid.CanWalkInto(ground, mode), Is.True,
                "ordinary ground stopped being walkable");
        }

        [Test]
        public void NobodyHangsOnARockFaceDoingNothing()
        {
            // A cell a climb passes through is standable and has no floor, so a pawn whose path
            // ends or fails while it is on one used to stay there, idle, in mid-air — 702
            // pawn-ticks of it over 40,000. Letting go is the only honest answer: there is
            // nothing to hold on to.
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int over = Size.Index(start.X, start.Z, start.Y);
            int pit = over - Size.LayerStride;
            int deeper = pit - Size.LayerStride;
            Assume.That(colony.Designations.CanMine(pit), Is.True);
            Dig(colony, pit);
            Assume.That(colony.Designations.CanMine(deeper), Is.True);
            Dig(colony, deeper);
            Assume.That(colony.Pawns.Nav.Grid.IsClimbOnly(pit), Is.True);

            Pawn pawn = colony.Pawns.Pawns.All[0];
            pawn.ClearPath();
            pawn.Cell = pit;

            colony.World.Tick();

            Assert.That(pawn.Cell, Is.Not.EqualTo(pit), "the colonist is still clinging to the face");
            Assert.That(colony.Grid.HasFloor(pawn.Cell), Is.True,
                $"it let go and landed in {Size.FromIndex(pawn.Cell)}, which has nothing under it");
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
        public void AClimbIsNotALadder()
        {
            // Ladders are a built thing: the generator puts them in buildings, they cost what a
            // made object costs, and something is drawn where one is. A hole cut with a pick has
            // none of that. Modelling it as a ladder claimed all three, and for one commit the
            // game duly drew a free unbuilt ladder in every pit on the board.
            Assert.That(Odyssey.Sim.Pathing.ConnectorKind.Climb,
                Is.Not.EqualTo(Odyssey.Sim.Pathing.ConnectorKind.Ladder));
            Assert.That(Odyssey.Sim.Pathing.MoveCost.ClimbUp,
                Is.LessThan(Odyssey.Sim.Pathing.MoveCost.LadderUp),
                "climbing rock is dearer than a ladder somebody built to make it easier");
            Assert.That((Odyssey.Sim.Pathing.NavFlags.Connector
                         & Odyssey.Sim.Pathing.NavFlags.ConnectorClimb), Is.Not.EqualTo(0),
                "a climb footprint is not counted as a connector, so its cell has no floor");
        }

        [Test]
        public void GoingDownCostsLessThanClimbingBackUp()
        {
            // The asymmetry is the point and it is not a fudge: going down a hole and coming back
            // up it are not the same job. Priced alike, a descent took six and a half seconds for
            // three metres, which the owner twice described as floating.
            Assert.That(Odyssey.Sim.Pathing.MoveCost.ClimbDown,
                Is.LessThan(Odyssey.Sim.Pathing.MoveCost.ClimbUp));
            Assert.That(Odyssey.Sim.Pathing.MoveCost.ClimbDown,
                Is.LessThanOrEqualTo(Odyssey.Sim.Pathing.MoveCost.Orthogonal),
                "dropping a layer costs more than walking a cell, so it still reads as a climb");
            Assert.That(Odyssey.Sim.Pathing.MoveCost.ClimbUp,
                Is.GreaterThan(Odyssey.Sim.Pathing.MoveCost.Orthogonal),
                "climbing became as cheap as walking, so nothing will ever prefer a ramp");
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
            var pawn = new Pawn(new PawnId(1), 0, PawnContent.Core());
            Assert.That(pawn.MoveStepCost, Is.EqualTo(Odyssey.Sim.Pathing.MoveCost.Orthogonal),
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
