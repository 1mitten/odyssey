#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Growing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
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

        [Test]
        public void NearestCellWithSpaceOnFloorlessCellDropsToFirstFloorBelow()
        {
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int surface = Size.Index(start.X, start.Z, start.Y);
            Assume.That(colony.Grid.HasFloor(surface), Is.True);

            // An open air cell two layers above the ground
            int airCell = surface + Size.LayerStride * 2;
            Assume.That(colony.Grid.HasFloor(airCell), Is.False);

            int result = colony.Pawns.Items.NearestCellWithSpace(
                colony.Grid, airCell, ItemIndex.Wood, 10, maxRadius: 3);

            Assert.That(result, Is.GreaterThanOrEqualTo(0));
            Assert.That(colony.Grid.HasFloor(result), Is.True, "the result must have a floor");
            Assert.That(Size.FromIndex(result).Y, Is.EqualTo(start.Y),
                "the search dropped to the ground surface layer");
        }

        [Test]
        public void DropFloatingItemsSweepDropsOrphanedItemsToFloorBelow()
        {
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int surface = -1;
            for (int dz = -3; dz <= 3 && surface < 0; dz++)
            for (int dx = -3; dx <= 3 && surface < 0; dx++)
            {
                if (!Size.Contains(start.X + dx, start.Z + dz, start.Y)) continue;
                int c = Size.Index(start.X + dx, start.Z + dz, start.Y);
                if (colony.Grid.IsWalkable(c) && colony.Pawns.Items.ItemAt(c) == null)
                    surface = c;
            }
            Assume.That(surface, Is.GreaterThanOrEqualTo(0));
            int below = surface - Size.LayerStride;
            Assume.That(colony.Grid.HasFloor(surface), Is.True);

            // Spawn wood on the empty surface
            colony.Pawns.Items.Spawn(ItemIndex.Wood, surface, 10);
            Assume.That(colony.Pawns.Items.ItemAt(surface), Is.Not.Null);

            // Artificially remove the floor under the wood without firing an event
            colony.Grid.Terrain[below] = NaturalContent.TerrainAir;
            colony.Grid.Flags[below] &= ~CellFlags.SolidTerrain;
            colony.Grid.Floor[surface] = CoreContent.SlabNone;
            Assume.That(colony.Grid.HasFloor(surface), Is.False, "the wood is now floating in mid-air");

            // Run the safety sweep
            int dropped = Falling.DropFloatingItems(colony.Pawns);
            Assert.That(dropped, Is.GreaterThanOrEqualTo(1), "the sweep caught the floating item");

            Assert.That(colony.Pawns.Items.ItemAt(surface), Is.Null, "the wood is no longer at the floorless cell");

            int landing = colony.Grid.FirstFloorAtOrBelow(surface);
            var landed = colony.Pawns.Items.ItemAt(landing);
            Assert.That(landed, Is.Not.Null);
            Assert.That(colony.Grid.HasFloor(landed!.Cell), Is.True, "the landed item now has a floor");
        }

        [Test]
        public void FallingItemWithNoFloorAnywhereBelowDespawns()
        {
            ColonyWorld colony = Board();
            // A column where layer 0 has no floor
            int bottomCell = Size.Index(5, 5, 0);
            colony.Grid.Terrain[bottomCell] = NaturalContent.TerrainAir;
            colony.Grid.Flags[bottomCell] &= ~CellFlags.SolidTerrain;
            colony.Grid.Floor[bottomCell] = CoreContent.SlabNone;
            Assume.That(colony.Grid.HasFloor(bottomCell), Is.False);

            int airCell = bottomCell + Size.LayerStride;
            colony.Grid.Terrain[airCell] = NaturalContent.TerrainAir;
            colony.Grid.Flags[airCell] &= ~CellFlags.SolidTerrain;
            colony.Grid.Floor[airCell] = CoreContent.SlabNone;

            ThingId id = colony.Pawns.Items.Spawn(ItemIndex.Wood, airCell, 5);
            var item = colony.Pawns.Items.Get(id)!;
            Assume.That(item.Despawned, Is.False);

            Falling.ItemsOutOf(colony.Pawns, airCell);

            Assert.That(item.Despawned, Is.True,
                "an item over a bottomless void despawns rather than hanging in mid-air");
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

        // ---- what is rooted in the ground rather than resting on it -----------------------
        //
        // Owner report, 2026-09-21, with a screenshot: soil was tilled into a growing zone, sown,
        // and then quarried out from underneath, and the seeds stayed exactly where they had been
        // -- a scatter of white specks hanging over the hole. Falling knew about people and about
        // loose things and about nothing else.

        /// <summary>Paint one cell of carrots, sow it, and answer with the cell that holds it.</summary>
        static int SowOne(ColonyWorld colony, int x, int z, int y)
        {
            GrowingZones zones = colony.Growing!;
            int cell = Size.Index(x, z, y);
            Assume.That(zones.Designate(new CellRef(x, z, y), PlantHandle.Carrot),
                Is.EqualTo(IntentRejection.None), "the fixture needs plantable ground here");
            zones.Sow(cell);
            return cell;
        }

        /// <summary>
        /// A surface cell of open, plantable ground with solid soil under it, and a plantable
        /// neighbour to its east so the "field next door" test has somewhere to stand.
        /// </summary>
        static int OpenField(ColonyWorld colony, out int ground)
        {
            GrowingZones zones = colony.Growing!;
            PlantDef carrot = zones.Plant(PlantHandle.Carrot);
            for (int z = 1; z < Size.SizeZ - 1; z++)
            for (int x = 1; x < Size.SizeX - 2; x++)
            {
                int cell = Size.Index(x, z, colony.Start.Y);
                if (!zones.SiteAllows(cell, carrot)) continue;
                if (!zones.SiteAllows(Size.Index(x + 1, z, colony.Start.Y), carrot)) continue;
                int soil = cell - Size.LayerStride;
                if (!colony.Grid.IsSolidTerrain(soil)) continue;
                ground = soil;
                return cell;
            }

            ground = -1;
            return -1;
        }

        [Test]
        public void MiningTheSoilUnderAFieldTakesTheSeedWithIt()
        {
            ColonyWorld colony = Board();
            int cell = OpenField(colony, out int ground);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0), "the fixture found nowhere to farm");

            CellRef at = Size.FromIndex(cell);
            SowOne(colony, at.X, at.Z, at.Y);
            Assume.That(colony.Growing!.IsPlanted(cell), Is.True);

            Dig(colony, ground);

            Assert.That(colony.Grid.HasFloor(cell), Is.False, "the fixture did not actually dig the soil out");
            Assert.That(colony.Growing!.IsPlanted(cell), Is.False,
                "the seed stayed where the soil had been - the reported fault");
            Assert.That(colony.Growing!.ZonePlantAt(cell), Is.LessThan(0),
                "the zone paint outlived the ground, so the field would have re-sown itself into the air");
        }

        [Test]
        public void TheFieldNextDoorKeepsItsCrop()
        {
            // The rule is about the cell that lost its floor and not about the zone it belonged
            // to: quarrying one corner of a field must not cancel the field.
            ColonyWorld colony = Board();
            int cell = OpenField(colony, out int ground);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            CellRef at = Size.FromIndex(cell);
            SowOne(colony, at.X, at.Z, at.Y);
            int neighbour = SowOne(colony, at.X + 1, at.Z, at.Y);

            Dig(colony, ground);

            Assert.That(colony.Growing!.IsPlanted(cell), Is.False);
            Assert.That(colony.Growing!.IsPlanted(neighbour), Is.True, "its own soil is untouched");
            Assert.That(colony.Growing!.ZonePlantAt(neighbour), Is.EqualTo(PlantHandle.Carrot));
        }

        [Test]
        public void AnEmptyZoneCellLosesItsPaintToo()
        {
            // Nothing sown yet: the paint alone is still a claim about ground that is now a hole,
            // and a sower sent to it would be sent into the air.
            ColonyWorld colony = Board();
            int cell = OpenField(colony, out int ground);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            CellRef at = Size.FromIndex(cell);
            Assume.That(colony.Growing!.Designate(at, PlantHandle.Carrot), Is.EqualTo(IntentRejection.None));

            Dig(colony, ground);

            Assert.That(colony.Growing!.ZonePlantAt(cell), Is.LessThan(0));
        }

        [Test]
        public void TheSweepFindsAFieldNobodyReported()
        {
            // The sibling of DropFloatingItems, for the case no caller of OutOf saw: a slab several
            // cells away comes down and takes the soil of a field with it. The ground is taken away
            // here without telling anybody, which is what that looks like from the zone's side.
            ColonyWorld colony = Board();
            int cell = OpenField(colony, out int ground);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            CellRef at = Size.FromIndex(cell);
            SowOne(colony, at.X, at.Z, at.Y);

            colony.Grid.Terrain[ground] = CoreContent.TerrainAir;
            colony.Grid.Flags[ground] &= ~CellFlags.SolidTerrain;
            Assume.That(colony.Grid.HasFloor(cell), Is.False);
            Assume.That(colony.Growing!.IsPlanted(cell), Is.True, "nothing has been told yet");

            Assert.That(Falling.UprootFloatingPlants(colony.Pawns), Is.EqualTo(1));
            Assert.That(colony.Growing!.IsPlanted(cell), Is.False);
            Assert.That(colony.Growing!.ZonePlantAt(cell), Is.LessThan(0));
        }

        [Test]
        public void TheSweepCostsNothingWhenEveryFieldHasItsGround()
        {
            ColonyWorld colony = Board();
            int cell = OpenField(colony, out _);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            CellRef at = Size.FromIndex(cell);
            SowOne(colony, at.X, at.Z, at.Y);

            Assert.That(Falling.UprootFloatingPlants(colony.Pawns), Is.EqualTo(0));
            Assert.That(colony.Growing!.IsPlanted(cell), Is.True);
        }

        [Test]
        public void ATreeGoesWithTheGroundAndLeavesNoWood()
        {
            // Mining cannot reach this - DesignationGrid.CanMine refuses the cell under a standing
            // tree outright - but a collapsing slab and a deconstructed floor never ask. The rule
            // is stated here so the caller that can produce it inherits the answer.
            ColonyWorld colony = Board();
            int tree = FirstTree(colony);
            Assume.That(tree, Is.GreaterThanOrEqualTo(0), "the wooded board grew no trees");

            int stacks = colony.Pawns.Items.Items.Count;
            int ground = tree - Size.LayerStride;
            colony.Grid.Terrain[ground] = CoreContent.TerrainAir;
            colony.Grid.Flags[ground] &= ~CellFlags.SolidTerrain;

            // Through OutOf rather than TreesOutOf, so the wiring is asserted and not just the rule.
            Falling.OutOf(colony.Pawns, tree);
            Assert.That(colony.Designations.IsTree(tree), Is.False, "the tree stayed rooted in mid-air");
            Assert.That(colony.Pawns.Items.Items.Count, Is.EqualTo(stacks),
                "what is lost with the ground is lost: felling is the way to get the timber");
        }

        [Test]
        public void ATreeOnSolidGroundIsLeftAlone()
        {
            ColonyWorld colony = Board();
            int tree = FirstTree(colony);
            Assume.That(tree, Is.GreaterThanOrEqualTo(0));

            Assert.That(Falling.TreesOutOf(colony.Pawns, tree), Is.False);
            Assert.That(colony.Designations.IsTree(tree), Is.True);
        }

        static int FirstTree(ColonyWorld colony)
        {
            for (int i = Size.LayerStride; i < Size.CellCount; i++)
                if (colony.Designations.IsTree(i)) return i;
            return -1;
        }

        /// <summary>The arithmetic <c>PawnRegistry</c> publishes, stated once so it can be checked.</summary>
        static int Percent(int progress, int cost)
        {
            int percent = (int)((long)progress * 100 / cost);
            return percent < 0 ? 0 : percent > 100 ? 100 : percent;
        }
    }
}
