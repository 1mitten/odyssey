#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The build pipeline: an order becomes a site, material is carried to it, work is applied, and
    /// a wall stands where it was.
    ///
    /// <para>The end-to-end test at the bottom is the one that matters. Everything above it checks
    /// a rule in isolation, which is cheap and catches the wrong thing being refused; only the
    /// whole journey catches the case where every part works and no colonist ever walks.</para>
    /// </summary>
    public class ConstructionTests
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

        /// <summary>A walkable, empty cell beside the start that a wall could legally stand in.</summary>
        static int SiteBesideTheStart(ColonyWorld colony)
        {
            CellRef start = colony.Start;
            for (int radius = 1; radius < 8; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!Size.Contains(x, z, start.Y)) continue;
                int index = Size.Index(x, z, start.Y);
                if (!colony.Construction.Allows(index)) continue;
                // Somewhere to stand to build it, or nobody ever will.
                if (FellJobDriver.StandBeside(colony.Pawns, colony.Pawns.Pawns.All[0], index) < 0) continue;
                return index;
            }

            return -1;
        }

        static int OnTheGround(ColonyWorld colony, int item)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == item) total += items[i].Stack;
            return total;
        }

        static IntentRejection Order(ColonyWorld colony, int cell, int stuff = StuffHandle.Wood)
        {
            colony.World.Intents.Submit(new Intent(
                IntentKind.PlaceBuilding, Size.FromIndex(cell), BuildingHandle.Wall, stuff));
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        // ---- the tables ---------------------------------------------------------------------

        [Test]
        public void AStoneWallIsMoreWorkThanAWoodenOne()
        {
            // The one number that makes the choice of material a decision rather than a colour.
            int wood = ConstructionContent.WorkFor(BuildingHandle.Wall, StuffHandle.Wood);
            int stone = ConstructionContent.WorkFor(BuildingHandle.Wall, StuffHandle.Stone);

            Assert.That(wood, Is.EqualTo(135));
            Assert.That(stone, Is.EqualTo(229), "135 x 1.7, rounded down by integer division");
            Assert.That(stone, Is.GreaterThan(wood));
        }

        [Test]
        public void OnlyWhatAColonistCanCarryIsBuildableWith()
        {
            Assert.That(ConstructionContent.IsBuildable(StuffHandle.Wood), Is.True);
            Assert.That(ConstructionContent.IsBuildable(StuffHandle.Stone), Is.True);

            // The three the generator stamps: nothing produces them and nobody can pick one up, so
            // an order made of one could never be filled.
            Assert.That(ConstructionContent.IsBuildable(StuffHandle.Concrete), Is.False);
            Assert.That(ConstructionContent.IsBuildable(StuffHandle.Steel), Is.False);
            Assert.That(ConstructionContent.IsBuildable(StuffHandle.Composite), Is.False);
            Assert.That(ConstructionContent.IsBuildable(StuffHandle.None), Is.False);
        }

        // ---- placing and refusing -------------------------------------------------------------

        [Test]
        public void AnOrderedWallBecomesASiteWaitingForItsMaterial()
        {
            ColonyWorld colony = Board();
            int cell = SiteBesideTheStart(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0), "the board has somewhere to put a wall");

            Assert.That(Order(colony, cell), Is.EqualTo(IntentRejection.None));

            ConstructionGrid sites = colony.Construction;
            Assert.That(sites.At(cell), Is.EqualTo(BuildingHandle.Wall));
            Assert.That(sites.StuffAt(cell), Is.EqualTo(StuffHandle.Wood));
            Assert.That(sites.Outstanding(cell), Is.EqualTo(5), "a wall wants five wood");
            Assert.That(sites.IsFrame(cell), Is.False, "nothing has been delivered yet");
            Assert.That(sites.Sites, Does.Contain(cell));
        }

        [Test]
        public void AMaterialTheColonyCannotCarryIsRefused()
        {
            ColonyWorld colony = Board();
            int cell = SiteBesideTheStart(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            Assert.That(Order(colony, cell, StuffHandle.Concrete), Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(colony.Construction.At(cell), Is.EqualTo(BuildingHandle.None));
        }

        [Test]
        public void NothingIsBuiltInSolidGroundInWaterOrWhereATreeStands()
        {
            ColonyWorld colony = Board();
            CellGrid grid = colony.Grid;
            ConstructionGrid sites = colony.Construction;

            int solid = -1, water = -1, tree = -1, midAir = -1;
            for (int i = 0; i < Size.CellCount && (solid < 0 || water < 0 || tree < 0 || midAir < 0); i++)
            {
                if (solid < 0 && grid.IsSolidTerrain(i)) solid = i;
                if (water < 0 && NaturalContent.IsWater(grid.Terrain[i])) water = i;
                if (tree < 0 && grid.Edifice[i] >= 0) tree = i;
                if (midAir < 0 && !grid.IsSolidTerrain(i) && !grid.HasFloor(i) && grid.Edifice[i] < 0) midAir = i;
            }

            Assume.That(solid, Is.GreaterThanOrEqualTo(0));
            Assume.That(tree, Is.GreaterThanOrEqualTo(0), "the wooded board has trees on it");
            Assume.That(midAir, Is.GreaterThanOrEqualTo(0));

            Assert.That(sites.Allows(solid), Is.False, "you cannot build inside the ground");
            Assert.That(sites.Allows(tree), Is.False, "fell it first, as the ground under it cannot be mined first");
            Assert.That(sites.Allows(midAir), Is.False, "a wall needs a floor under it");
            if (water >= 0)
                Assert.That(sites.Allows(water), Is.False, "a cell you can wade through is not one you can wall");
        }

        [Test]
        public void TheSiteRuleAcceptsSomething()
        {
            // The negative control for the test above: a rule that refused everything would pass
            // all four of those assertions and be worthless.
            ColonyWorld colony = Board();
            Assert.That(SiteBesideTheStart(colony), Is.GreaterThanOrEqualTo(0));
        }

        // ---- cancelling ------------------------------------------------------------------------

        [Test]
        public void CancellingASiteGivesBackWhatWasCarriedToIt()
        {
            ColonyWorld colony = Board();
            int cell = SiteBesideTheStart(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));
            Order(colony, cell);

            colony.Construction.Deliver(cell, 3);
            int before = OnTheGround(colony, ItemIndex.Wood);

            colony.World.Intents.Submit(new Intent(IntentKind.CancelBuilding, Size.FromIndex(cell)));
            colony.World.Tick();

            Assert.That(colony.Construction.At(cell), Is.EqualTo(BuildingHandle.None));
            Assert.That(OnTheGround(colony, ItemIndex.Wood), Is.EqualTo(before + 3),
                "a player changing their mind does not burn the colony's wood");
        }

        // ---- state ------------------------------------------------------------------------------

        [Test]
        public void ASiteIsHashedAndSurvivesASaveAndReload()
        {
            ColonyWorld colony = Board();
            int cell = SiteBesideTheStart(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            ulong bare = colony.World.ComputeStateHash().Value;
            Order(colony, cell);
            colony.Construction.Deliver(cell, 2);
            colony.Construction.AddWork(cell, 40);
            ulong ordered = colony.World.ComputeStateHash().Value;

            Assert.That(ordered, Is.Not.EqualTo(bare), "a site the player ordered is state");

            byte[] saved = colony.Save();
            ColonyWorld reloaded = Board();
            reloaded.Load(saved);

            Assert.That(reloaded.Construction.At(cell), Is.EqualTo(BuildingHandle.Wall));
            Assert.That(reloaded.Construction.StuffAt(cell), Is.EqualTo(StuffHandle.Wood));
            Assert.That(reloaded.Construction.Delivered(cell), Is.EqualTo(2), "the wood already carried survives");
            Assert.That(reloaded.Construction.WorkDone(cell), Is.EqualTo(40), "a half-built wall survives");
            Assert.That(reloaded.World.ComputeStateHash().Value, Is.EqualTo(ordered));
        }

        [Test]
        public void ASiteIsPublishedToTheInterface()
        {
            ColonyWorld colony = Board();
            int cell = SiteBesideTheStart(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));
            Order(colony, cell);

            colony.Construction.Deliver(cell, 5);
            colony.World.Tick();

            WorldSnapshot frame = colony.World.Views.Current;
            Assert.That(frame.SiteCount, Is.EqualTo(1));
            SiteView view = frame.Sites[0];
            Assert.That(view.CellIndex, Is.EqualTo(cell));
            Assert.That(view.Building, Is.EqualTo((byte)BuildingHandle.Wall));
            Assert.That(view.Stuff, Is.EqualTo((byte)StuffHandle.Wood));
            Assert.That(view.Delivered, Is.EqualTo(255), "all five arrived");
        }

        // ---- the whole journey -------------------------------------------------------------------

        [Test]
        public void AnOrderedWallIsFedWorkedAndRaised()
        {
            ColonyWorld colony = Board();
            int cell = SiteBesideTheStart(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            // Wood on the ground near the start, as felling would have left it. Twenty, so the
            // deliverer carries more than the wall wants and has to put the rest down.
            int pile = colony.Pawns.Items.NearestCellWithSpace(
                colony.Grid, cell, ItemIndex.Wood, 20, JobDriver.DropSearchRadius);
            Assume.That(pile, Is.GreaterThanOrEqualTo(0));
            colony.Pawns.Items.Spawn(ItemIndex.Wood, pile, 20);

            Assert.That(Order(colony, cell), Is.EqualTo(IntentRejection.None));

            bool fed = false;
            int raisedAt = -1;
            for (int tick = 0; tick < 20_000 && raisedAt < 0; tick++)
            {
                colony.World.Tick();
                if (colony.Construction.IsFrame(cell)) fed = true;
                if (colony.Grid.Edifice[cell] >= 0) raisedAt = tick;
            }

            Assert.That(fed, Is.True, "the wood was carried to the site");
            Assert.That(raisedAt, Is.GreaterThanOrEqualTo(0), "a wall stands where the order was");

            Assert.That(colony.Construction.At(cell), Is.EqualTo(BuildingHandle.None), "the site is spent");
            Assert.That(colony.Grid.IsBlockedByEdifice(cell), Is.True, "a wall is something to walk round");

            var placed = colony.Outcome.Edifices[colony.Grid.Edifice[cell]];
            Assert.That(placed.Def, Is.EqualTo(CoreContent.EdificeWall));
            Assert.That(placed.Stuff, Is.EqualTo(NaturalContent.StuffWood), "it remembers what it was made of");
            Assert.That(placed.CellIndex, Is.EqualTo(cell));

            // Five went into the wall; the other fifteen are still the colony's.
            Assert.That(OnTheGround(colony, ItemIndex.Wood), Is.EqualTo(15));
        }

        [Test]
        public void AWallIsNotBuiltOutOfNothing()
        {
            // The negative control for the journey above. Same order, same board, no wood: the site
            // must stay a blueprint for ever rather than a wall appearing out of good intentions.
            ColonyWorld colony = Board();
            int cell = SiteBesideTheStart(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));
            Assume.That(OnTheGround(colony, ItemIndex.Wood), Is.EqualTo(0), "the bare scenario starts with no wood");

            Order(colony, cell);
            for (int tick = 0; tick < 20_000; tick++) colony.World.Tick();

            Assert.That(colony.Grid.Edifice[cell], Is.LessThan(0), "nothing was built");
            Assert.That(colony.Construction.At(cell), Is.EqualTo(BuildingHandle.Wall), "the order still stands");
            Assert.That(colony.Construction.Delivered(cell), Is.EqualTo(0));
            Assert.That(colony.Construction.WorkDone(cell), Is.EqualTo(0), "and nobody worked at it");
        }
    }
}
