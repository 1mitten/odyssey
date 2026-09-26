#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Nobody lies down in a tree, and nothing is put down in one (owner, 2026-09-25:
    /// *"colonists sometimes sleep through trees, double check they don't"* and *"drops and spawned
    /// items shouldn't land exactly at a tree's trunk — around it or the next tile"*).
    ///
    /// <para><b>Why a tree is easy to get wrong.</b> It blocks nothing: the grid calls its cell
    /// walkable, pawns walk through the trunk, and every rule written as "a walkable cell with room"
    /// happily answered the tree's own cell. The two owners now are <c>FreeSpot</c> for a body
    /// (design 20 §14) and <c>ColonyItems.CellHasSpace</c> for a thing (design 23 §11).</para>
    /// </summary>
    public class TreeTrunkTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 8);

        static ColonyWorld Board(int colonists = 1, int beds = 0)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = beds;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, 1u, scenario, barren: true, wooded: false);
        }

        /// <summary>Stand a tree in a cell, as the generator would have.</summary>
        static int Tree(ColonyWorld colony, int cell)
        {
            var records = colony.Pawns.Construction!.Edifices.Records;
            records.Add(new PlacedEdifice { CellIndex = cell, Def = NaturalContent.EdificeTreeBirch, Stuff = NaturalContent.StuffWood });
            colony.Grid.Edifice[cell] = records.Count - 1;
            colony.Pawns.Chunks!.MarkDirty(Size.FromIndex(cell));
            return cell;
        }

        /// <summary>Trees on every cell within <paramref name="radius"/> of the centre, on its layer.</summary>
        static void Copse(ColonyWorld colony, int centre, int radius)
        {
            CellRef at = Size.FromIndex(centre);
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int cell = Size.Index(at.X + dx, at.Z + dz, at.Y);
                if (colony.Grid.IsWalkable(cell) && colony.Grid.Edifice[cell] < 0) Tree(colony, cell);
            }
        }

        static int Chebyshev(int a, int b)
        {
            CellRef p = Size.FromIndex(a), q = Size.FromIndex(b);
            return System.Math.Max(System.Math.Abs(p.X - q.X), System.Math.Abs(p.Z - q.Z));
        }

        static int Ground(ColonyWorld colony, int x, int z) => colony.Grid.NearestWalkableInColumn(x, z, Size.SizeY - 1);

        // ---- the fixture itself -------------------------------------------------------------

        /// <summary>
        /// The premise every test below rests on: a planted tree is a tree to both owners, and its
        /// cell is still walkable — which is the whole reason walkability could not say it.
        /// </summary>
        [Test]
        public void APlantedTreeIsATreeAndItsCellIsStillWalkable()
        {
            ColonyWorld colony = Board();
            int cell = Tree(colony, Ground(colony, 10, 10));
            Assert.That(colony.Designations.IsTree(cell), Is.True);
            Assert.That(colony.Pawns.TreeAt(cell), Is.True);
            Assert.That(colony.Pawns.Items.TreeAt(cell), Is.True);
            Assert.That(colony.Grid.IsWalkable(cell), Is.True, "a tree blocks nothing");
            Assert.That(colony.Pawns.TreeAt(Ground(colony, 12, 12)), Is.False, "a bare cell is a tree");
        }

        // ---- (a) nobody lies down in a tree ---------------------------------------------------

        /// <summary>
        /// A tired colonist with no bed and no fire, standing in a tree, walks out of it before she
        /// lies down — to a cell with no tree, beside the one she was in.
        /// </summary>
        [Test]
        public void ABedlessSleeperStandingInATreeStepsOutOfItFirst()
        {
            ColonyWorld colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Tree(colony, pawn.Cell);

            var job = new Job();
            Assert.That(CriticalNeedsThinkNode.TrySleep(pawn, colony.Pawns, job), Is.True);
            Assert.That(job.DefIndex, Is.EqualTo(JobIndex.Wander), "she lay down in the tree she was standing in");
            Assert.That(colony.Pawns.TreeAt(job.TargetCell), Is.False, "she was sent to another tree");
            Assert.That(Chebyshev(job.TargetCell, pawn.Cell), Is.EqualTo(1), "not the nearest open cell");
        }

        /// <summary>The control: out of the tree, she lies where she stands, exactly as before.</summary>
        [Test]
        public void WithNoTreeSheStillLiesWhereSheStands()
        {
            ColonyWorld colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];

            var job = new Job();
            Assert.That(CriticalNeedsThinkNode.TrySleep(pawn, colony.Pawns, job), Is.True);
            Assert.That(job.DefIndex, Is.EqualTo(JobIndex.Sleep));
            Assert.That(job.TargetCell, Is.EqualTo(-1));
        }

        /// <summary>
        /// Deep in a copse she walks as far as it takes — here three rings — and still to open
        /// ground rather than to the nearest trunk.
        /// </summary>
        [Test]
        public void InACopseSheWalksOutToOpenGround()
        {
            ColonyWorld colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Copse(colony, pawn.Cell, radius: 2);

            var job = new Job();
            Assert.That(CriticalNeedsThinkNode.TrySleep(pawn, colony.Pawns, job), Is.True);
            Assert.That(job.DefIndex, Is.EqualTo(JobIndex.Wander));
            Assert.That(colony.Pawns.TreeAt(job.TargetCell), Is.False);
            Assert.That(Chebyshev(job.TargetCell, pawn.Cell), Is.EqualTo(3));
        }

        /// <summary>
        /// Exhaustion is a stumble, not a walk: at zero rest she takes one step out of the tree and
        /// goes down there.
        /// </summary>
        [Test]
        public void ACollapseInATreeStumblesOneStepOutOfIt()
        {
            ColonyWorld colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Tree(colony, pawn.Cell);
            pawn.Needs[NeedIndex.Rest] = 0;

            var job = new Job();
            Assert.That(CriticalNeedsThinkNode.TrySleep(pawn, colony.Pawns, job), Is.True);
            Assert.That(job.DefIndex, Is.EqualTo(JobIndex.Wander), "she collapsed inside the tree");
            Assert.That(colony.Pawns.TreeAt(job.TargetCell), Is.False);
            Assert.That(Chebyshev(job.TargetCell, pawn.Cell), Is.EqualTo(1), "a collapse walked further than one step");
        }

        /// <summary>
        /// And when every cell beside her is a tree too, a collapse does not become a walk: she
        /// goes down where she is. Nothing better exists within a stumble, and a body at zero rest
        /// does not hike.
        /// </summary>
        [Test]
        public void ACollapseBoxedInByTrunksStillGoesDown()
        {
            ColonyWorld colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Copse(colony, pawn.Cell, radius: 1);
            pawn.Needs[NeedIndex.Rest] = 0;

            var job = new Job();
            Assert.That(CriticalNeedsThinkNode.TrySleep(pawn, colony.Pawns, job), Is.True);
            Assert.That(job.DefIndex, Is.EqualTo(JobIndex.Sleep));
            Assert.That(job.TargetCell, Is.EqualTo(-1));
        }

        /// <summary>
        /// End to end, with nobody holding her: a tired colonist in a copse is asleep a little
        /// later, and asleep in a cell with no tree in it.
        /// </summary>
        [Test]
        public void LeftToItATiredColonistInACopseSleepsOutsideIt()
        {
            ColonyWorld colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Copse(colony, pawn.Cell, radius: 2);
            pawn.Needs[NeedIndex.Rest] = colony.Pawns.Content.Needs[NeedIndex.Rest].seekThreshold - 1;

            for (int i = 0; i < 3_000 && !pawn.Asleep; i++) colony.World.Tick();

            Assert.That(pawn.Asleep, Is.True, "she never lay down");
            Assert.That(colony.Pawns.TreeAt(pawn.Cell), Is.False, "she is asleep inside a tree");
        }

        /// <summary>
        /// The fireside is never a tree either. With seven of the eight cells round a fire planted,
        /// the bedless sleeper is sent to the eighth.
        /// </summary>
        [Test]
        public void TheFiresideSkipsATreeInTheRing()
        {
            ColonyWorld colony = Board();
            int fire = FiresideFixture.Fire(colony, Size, 12, 12);
            CellRef f = Size.FromIndex(fire);
            int open = Size.Index(f.X + 1, f.Z + 1, f.Y);
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int cell = Size.Index(f.X + dx, f.Z + dz, f.Y);
                if (cell != fire && cell != open) Tree(colony, cell);
            }
            colony.World.Tick(Odyssey.Sim.Temperature.TemperatureSystem.IntervalTicks * 2);

            Pawn pawn = colony.Pawns.Pawns.All[0];
            var job = new Job();
            Assert.That(CriticalNeedsThinkNode.TrySleep(pawn, colony.Pawns, job), Is.True);
            Assert.That(job.TargetCell, Is.EqualTo(open), "the sleeper was sent into a tree beside the fire");
        }

        /// <summary>
        /// A sleeper whose rest runs out <b>on the way</b> to her bed, while she is passing through
        /// a trunk, does not go down inside it: the collapse waits for the first cell on her path
        /// that she may lie in.
        ///
        /// <para><b>The negative control is the first half.</b> The same walk with no trees has her
        /// go down on the very tick her rest reaches zero, which is the rule this does not change.</para>
        /// </summary>
        [Test]
        public void ACollapseOnTheWayToBedWaitsUntilSheIsOutOfTheTrees()
        {
            (int there, int slept, bool inTree) = CollapseOnTheWay(trees: false);
            Assert.That(slept, Is.EqualTo(there), "the control: with no trees she goes down where her rest ran out");

            (there, slept, inTree) = CollapseOnTheWay(trees: true);
            Assert.That(inTree, Is.False, "she collapsed inside a tree");
            Assert.That(slept, Is.Not.EqualTo(there));
            Assert.That(Chebyshev(slept, there), Is.LessThanOrEqualTo(2),
                "she walked on well past the trees rather than going down at the first open cell");
        }

        /// <summary>
        /// Send a tired colonist to a bed across a band of ground, run her rest out on the band, and
        /// say where her rest ran out, where she went down and whether that was a tree.
        /// </summary>
        static (int there, int slept, bool inTree) CollapseOnTheWay(bool trees)
        {
            ColonyWorld colony = Board(colonists: 1, beds: 1);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            int bed = colony.Pawns.Items.Beds[0];
            CellRef b = Size.FromIndex(bed);

            // Well away from the bed, with a band two cells deep across the whole board between
            // them, so every path to it crosses the band.
            int startZ = b.Z + 14 < Size.SizeZ - 1 ? b.Z + 14 : b.Z - 14;
            pawn.Cell = Ground(colony, b.X, startZ);
            pawn.ClearPath();
            int bandZ = (startZ + b.Z) / 2;
            int nearZ = startZ > b.Z ? bandZ + 1 : bandZ;   // the band's row she meets first
            if (trees)
                for (int x = 0; x < Size.SizeX; x++)
                for (int dz = 0; dz < 2; dz++)
                {
                    int cell = Ground(colony, x, bandZ + dz);
                    if (cell >= 0 && colony.Grid.Edifice[cell] < 0) Tree(colony, cell);
                }

            pawn.Needs[NeedIndex.Rest] = colony.Pawns.Content.Needs[NeedIndex.Rest].seekThreshold - 1;
            for (int i = 0; i < 400 && pawn.CurrentJob?.TargetCell != bed; i++) colony.World.Tick();
            Assume.That(pawn.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Sleep), "she never set off for her bed");
            Assume.That(pawn.CurrentJob!.TargetCell, Is.EqualTo(bed));

            for (int i = 0; i < 4_000 && Size.FromIndex(pawn.Cell).Z != nearZ; i++) colony.World.Tick();
            Assume.That(Size.FromIndex(pawn.Cell).Z, Is.EqualTo(nearZ), "she never reached the band");
            Assume.That(colony.Pawns.TreeAt(pawn.Cell), Is.EqualTo(trees));
            int there = pawn.Cell;

            for (int i = 0; i < 400 && !pawn.Asleep; i++)
            {
                pawn.Needs[NeedIndex.Rest] = 0;
                colony.World.Tick();
            }
            Assert.That(pawn.Asleep, Is.True, "she never went down");
            Assert.That(pawn.Cell, Is.Not.EqualTo(bed), "she reached her bed, so nothing collapsed");
            return (there, pawn.Cell, colony.Pawns.TreeAt(pawn.Cell));
        }

        // ---- (b) nothing is put down in a tree -------------------------------------------------

        /// <summary>
        /// The one owner: the space test refuses a tree's cell for any load, and the widening search
        /// every drop goes through lands the load beside it.
        /// </summary>
        [Test]
        public void TheSpaceTestRefusesATreeAndTheSearchLandsBesideIt()
        {
            ColonyWorld colony = Board();
            int cell = Tree(colony, Ground(colony, 10, 10));

            Assert.That(colony.Pawns.Items.CellHasSpace(cell), Is.False);
            Assert.That(colony.Pawns.Items.CellHasSpace(cell, ItemIndex.Wood, 1), Is.False);

            int at = colony.Pawns.Items.NearestCellWithSpace(colony.Grid, cell, ItemIndex.Wood, 5, maxRadius: 3);
            Assert.That(at, Is.Not.EqualTo(cell), "a load was put down at the trunk");
            Assert.That(Chebyshev(at, cell), Is.EqualTo(1), "not the next tile");
        }

        /// <summary>A debug grant aimed at a tree lands on the tile beside it.</summary>
        [Test]
        public void ADebugGrantAimedAtATreeLandsBesideIt()
        {
            ColonyWorld colony = Board();
            int cell = Tree(colony, Ground(colony, 10, 10));

            colony.World.Intents.Submit(new Intent(IntentKind.GiveResource, Size.FromIndex(cell), ItemIndex.Wood, 20));
            colony.World.Tick();
            Assert.That(colony.World.Intents.Rejected, Is.Empty);

            Assert.That(colony.Pawns.Items.ItemAt(cell), Is.Null, "the grant was put down at the trunk");
            ColonyItem? beside = null;
            foreach (ColonyItem item in colony.Pawns.Items.Items)
                if (!item.Despawned && item.DefIndex == ItemIndex.Wood && item.Cell >= 0) beside = item;
            Assert.That(beside, Is.Not.Null);
            Assert.That(Chebyshev(beside!.Cell, cell), Is.EqualTo(1));
        }

        /// <summary>
        /// A supply drop forced on to a tree's column lands in the next column instead; one forced
        /// on to open ground lands exactly where it was aimed, as it always did.
        /// </summary>
        [Test]
        public void ASupplyDropAimedAtATreeLandsInTheNextColumn()
        {
            ColonyWorld colony = Board();
            int open = Ground(colony, 14, 14);
            int cell = Tree(colony, Ground(colony, 10, 10));
            CellRef at = Size.FromIndex(cell);
            Assume.That(colony.Grid.SkyLanding(at.X, at.Z), Is.EqualTo(cell), "a canopy stops nothing: the column's landing is the tree's cell");

            Assert.That(colony.Incidents.TryFire(new IncidentParms(IncidentHandle.SupplyDrop, cell: at)), Is.True,
                "a drop aimed at a tree was refused rather than moved beside it");
            int landing = colony.Incidents.Skyfallers.InFlight[0].LandingCell;
            Assert.That(landing, Is.Not.EqualTo(cell));
            Assert.That(colony.Pawns.TreeAt(landing), Is.False);
            Assert.That(Chebyshev(landing, cell), Is.EqualTo(1));

            // The control: open ground is honoured to the cell.
            Assert.That(colony.Incidents.TryFire(new IncidentParms(IncidentHandle.SupplyDrop, cell: Size.FromIndex(open))), Is.True);
            Assert.That(colony.Incidents.Skyfallers.InFlight[1].LandingCell, Is.EqualTo(open));
        }

        /// <summary>
        /// A drop drawn anywhere on a board that is nearly all forest never lands in a tree, over
        /// many draws — and the board really does tempt it, since most columns are trees.
        /// </summary>
        [Test]
        public void ADropDrawnOverAForestNeverLandsInATree()
        {
            ColonyWorld colony = Board();
            int trees = 0, columns = 0;
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
            {
                int cell = Ground(colony, x, z);
                if (cell < 0) continue;
                columns++;
                // Three in four columns planted, in a fixed pattern.
                if (((x + 2 * z) & 3) != 0 && colony.Grid.Edifice[cell] < 0 && colony.Pawns.Items.ItemAt(cell) == null)
                {
                    Tree(colony, cell);
                    trees++;
                }
            }
            Assume.That(trees * 2, Is.GreaterThan(columns), "the forest is not most of the board");

            int fired = 0;
            for (int i = 0; i < 40; i++)
            {
                if (!colony.Incidents.TryFire(new IncidentParms(IncidentHandle.SupplyDrop))) continue;
                fired++;
                var flights = colony.Incidents.Skyfallers.InFlight;
                int landing = flights[flights.Count - 1].LandingCell;
                Assert.That(colony.Pawns.TreeAt(landing), Is.False, $"drop {fired} was aimed into a tree");
                colony.World.Tick(7);
            }
            Assert.That(fired, Is.GreaterThan(20), "hardly any drops fired, so nothing was asked");

            // And they land where they were aimed, so none is in a tree after the fall either.
            colony.World.Tick(2_000);
            foreach (ColonyItem item in colony.Pawns.Items.Items)
                if (!item.Despawned && item.Cell >= 0)
                    Assert.That(colony.Pawns.TreeAt(item.Cell), Is.False, $"a {item.DefIndex} lies in a tree");
        }

        /// <summary>
        /// Felled wood goes where the tree stood when it can, and when it cannot — here a stone lies
        /// at the stump and the tree's neighbours are trees — past the trunks to open ground, never
        /// into the next trunk along.
        /// </summary>
        [Test]
        public void FelledWoodNeverLandsInTheNextTree()
        {
            ColonyWorld colony = Board();
            int centre = Ground(colony, 30, 30);
            Assume.That(colony.Pawns.Items.ItemAt(centre), Is.Null, "the scenario put something here");
            colony.Pawns.Items.Spawn(ItemIndex.Stone, centre, 5);
            Copse(colony, centre, radius: 2);
            Assume.That(colony.Pawns.TreeAt(centre), Is.True);

            colony.World.Intents.Submit(new Intent(IntentKind.Designate, Size.FromIndex(centre), (int)DesignationKind.Fell));
            colony.World.Tick();
            Assume.That(colony.World.Intents.Rejected, Is.Empty);

            for (int i = 0; i < 8_000 && colony.Grid.Edifice[centre] >= 0; i++) colony.World.Tick();
            Assert.That(colony.Grid.Edifice[centre], Is.LessThan(0), "the tree was never felled");

            int woodCell = -1;
            foreach (ColonyItem item in colony.Pawns.Items.Items)
                if (!item.Despawned && item.DefIndex == ItemIndex.Wood && item.Cell >= 0) woodCell = item.Cell;
            Assert.That(woodCell, Is.GreaterThanOrEqualTo(0), "the wood was lost");
            Assert.That(colony.Pawns.TreeAt(woodCell), Is.False, "the wood was put down inside the next tree");
            Assert.That(Chebyshev(woodCell, centre), Is.EqualTo(3), "not the nearest open ground");
        }

        /// <summary>
        /// A load that falls through a hole on to the ground at a tree's foot comes to rest beside
        /// the trunk.
        /// </summary>
        [Test]
        public void ALoadFallingOntoATreeComesToRestBesideIt()
        {
            ColonyWorld colony = Board();
            int ground = Tree(colony, Ground(colony, 10, 10));
            int above = ground + Size.LayerStride;
            colony.Grid.Floor[above] = CoreContent.SlabBuilt;
            colony.Pawns.Items.Spawn(ItemIndex.Stone, above, 5);

            colony.Grid.Floor[above] = 0;
            Falling.ItemsOutOf(colony.Pawns, above);

            Assert.That(colony.Pawns.Items.ItemAt(above), Is.Null, "the load is still in the air");
            Assert.That(colony.Pawns.Items.ItemAt(ground), Is.Null, "the load came to rest at the trunk");
            int rested = -1;
            foreach (ColonyItem item in colony.Pawns.Items.Items)
                if (!item.Despawned && item.DefIndex == ItemIndex.Stone && item.Cell >= 0) rested = item.Cell;
            Assert.That(Chebyshev(rested, ground), Is.EqualTo(1));
        }
        /// <summary>
        /// On the board the game loads, nothing the world starts with lies in a tree: the kit, the
        /// wreckage, the loose stones and the first mushrooms. The generator and the scenario place
        /// them before a tick runs, so this is the check that the space test's rule holds from the
        /// first frame and not only for things that arrive later.
        /// </summary>
        [Test]
        public void OnThePlayedBoardNothingStartsInATree()
        {
            ColonyWorld colony = Golden.PlayedBoard.Build();
            int trees = 0;
            for (int i = 0; i < colony.Grid.Size.CellCount; i++) if (colony.Pawns.TreeAt(i)) trees++;
            Assume.That(trees, Is.GreaterThan(100), "the played board grew no woods, so nothing was asked");

            foreach (ColonyItem item in colony.Pawns.Items.Items)
                if (!item.Despawned && item.Cell >= 0)
                    Assert.That(colony.Pawns.TreeAt(item.Cell), Is.False, $"a stack of def {item.DefIndex} starts in a tree");
        }
    }


    /// <summary>Raising a campfire in a test, shared by the tree and fireside tests.</summary>
    static class FiresideFixture
    {
        public static int Fire(ColonyWorld colony, GridSize size, int x, int z)
        {
            for (int step = 0; step < 64; step++)
            {
                int cx = x + step % 8;
                int cz = z + step / 8;
                if (cx >= size.SizeX - 2 || cz >= size.SizeZ - 2) continue;

                int air = colony.Grid.NearestWalkableInColumn(cx, cz, size.SizeY - 2);
                if (air < 0) continue;

                CellRef at = size.FromIndex(air);
                if (colony.Construction.Place(at, BuildingHandle.Campfire, StuffHandle.Wood)
                    != IntentRejection.None) continue;

                colony.Construction.RaiseWhenClear(colony.Pawns, air, BuildingHandle.Campfire);
                colony.World.Tick();
                return air;
            }

            Assert.Fail("no column on this board would take a campfire");
            return -1;
        }
    }
}
