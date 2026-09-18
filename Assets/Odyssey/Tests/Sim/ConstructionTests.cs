#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

// DefLoaderTests declares its own fixture StuffDef in this namespace, which wins over a using-alias
// of the same name — the same clash WorldContentDefTests aliases around (as SimTerrainDef) for
// TerrainDef. This file's tests spell the real type ConstructionStuffDef instead.
using ConstructionStuffDef = Odyssey.Sim.Construction.StuffDef;

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

        // ---- the composition --------------------------------------------------------------

        /// <summary>
        /// Every command the interface can send is answered by the colony the game builds.
        ///
        /// <para><b>This is the test that was missing when the build pipeline shipped unreachable.</b>
        /// The construction grid arrived as an optional argument to <c>AddColony</c> defaulting to
        /// null, so all twelve call sites went on compiling and the play scene — one of the twelve
        /// — built a colony with no handler for <c>PlaceBuilding</c>, no sites and both work givers
        /// answering no for ever. Every test passed, because every test goes through
        /// <see cref="ColonyWorld"/>. The owner dragged a wall across the meadow, watched the
        /// preview draw and watched nothing be built.</para>
        ///
        /// <para>The signature is the real fix — a required parameter cannot be forgotten — and
        /// this is the standing guard for the next command, which will arrive the same way: as an
        /// enum value somebody adds and a handler somebody means to attach.</para>
        /// </summary>
        [Test]
        public void EveryIntentTheInterfaceCanSendIsAnsweredByTheColony()
        {
            ColonyWorld colony = Board();

            foreach (IntentKind kind in System.Enum.GetValues(typeof(IntentKind)))
            {
                // The four the world answers out of its own switch rather than through a
                // registered component: they are about the view of the world, not about anything
                // in it — how fast it runs, which layer is shown, and which cell is being asked
                // about all belong to the world itself.
                if (kind == IntentKind.None
                    || kind == IntentKind.SetGameSpeed
                    || kind == IntentKind.SetSliceLayer
                    || kind == IntentKind.QueryCell) continue;

                Assert.That(colony.World.HandlesIntent(kind), Is.True,
                    $"nothing in the colony handles {kind}, so a player sending it gets silence");
            }
        }

        /// <summary>
        /// The negative control for the guard above: a rejected command really does come back as
        /// <see cref="IntentRejection.UnknownIntent"/>, which is the silence being guarded against.
        /// Without this the guard could be asserting that a method returns true.
        /// </summary>
        [Test]
        public void AnIntentNobodyHandlesIsRejectedAsUnknown()
        {
            ColonyWorld colony = Board();
            var stranger = (IntentKind)9999;

            Assert.That(colony.World.HandlesIntent(stranger), Is.False);

            colony.World.Intents.Submit(new Intent(stranger, colony.Start));
            colony.World.Tick();

            Assert.That(colony.World.Intents.Rejected.Count, Is.EqualTo(1));
            Assert.That(colony.World.Intents.Rejected[0].Reason, Is.EqualTo(IntentRejection.UnknownIntent));
        }

        // ---- the tables ---------------------------------------------------------------------

        [Test]
        public void AStoneWallIsMoreWorkThanAWoodenOne()
        {
            // The one number that makes the choice of material a decision rather than a colour.
            int wood = ConstructionContent.WorkFor(BuildingHandle.Wall, StuffHandle.Wood);
            int stone = ConstructionContent.WorkFor(BuildingHandle.Wall, StuffHandle.Stone);

            Assert.That(wood, Is.EqualTo(135), "wood's factor is 1000 per mille and its offset is 0");
            Assert.That(stone, Is.EqualTo(244), "135 x 1.7 = 229 (rounded down), + a 15-tick offset");
            Assert.That(stone, Is.GreaterThan(wood));
        }

        /// <summary>
        /// U27: <c>stat = base x factor + offset</c>. This is the offset's own test, isolated from
        /// the factor — without it, <see cref="AStoneWallIsMoreWorkThanAWoodenOne"/> could pass with
        /// the offset silently dropped, because a bigger factor alone would still make stone cost
        /// more than wood.
        /// </summary>
        [Test]
        public void TheOffsetIsAddedAfterTheFactorNotFoldedIntoIt()
        {
            var stuff = new ConstructionStuffDef { workFactorPerMille = 2000, workOffsetTicks = 50 };
            var building = new BuildingDef { workToBuild = 100 };

            // 100 x 2000 / 1000 = 200, then +50 = 250. Not 100 x (2000 + 50) / 1000 = 205.
            Assert.That(ConstructionContent.WorkFor(building, stuff), Is.EqualTo(250));
        }

        /// <summary>
        /// The floor of one tick (see <see cref="ConstructionContent.WorkFor(BuildingDef, StuffDef)"/>)
        /// applies to the whole expression, factor and offset together, not to the factored term
        /// alone.
        /// </summary>
        [Test]
        public void AZeroFactorWithNoOffsetStillFloorsToOneTick()
        {
            var stuff = new ConstructionStuffDef { workFactorPerMille = 0, workOffsetTicks = 0 };
            var building = new BuildingDef { workToBuild = 100 };

            Assert.That(ConstructionContent.WorkFor(building, stuff), Is.EqualTo(1));
        }

        /// <summary>A negative offset is not on the menu today, but the floor must still catch it.</summary>
        [Test]
        public void AnOffsetThatWouldGoNegativeStillFloorsToOneTick()
        {
            var stuff = new ConstructionStuffDef { workFactorPerMille = 1000, workOffsetTicks = -500 };
            var building = new BuildingDef { workToBuild = 100 };

            Assert.That(ConstructionContent.WorkFor(building, stuff), Is.EqualTo(1));
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

        /// <summary>
        /// A click on open grass names the ground <i>block</i>, not the air above it, and a wall
        /// goes in the air. The order has to be lifted or every cell of a wall dragged across a
        /// meadow is refused in silence.
        ///
        /// <para><b>This is the case the whole feature is used through and it had no test.</b> Every
        /// other test here picks its cell with <c>Allows</c>, which answers about air cells — so the
        /// path a player actually takes was the one path never exercised. The owner reported
        /// "dragging over grass and nothing appears" and the lift turned out to be working; that it
        /// was working was luck, because nothing would have caught it breaking.</para>
        /// </summary>
        [Test]
        public void AnOrderNamedAtTheGroundIsPlacedOnTheCellAboveIt()
        {
            ColonyWorld colony = Board();
            int site = SiteBesideTheStart(colony);
            Assume.That(site, Is.GreaterThanOrEqualTo(0));

            int ground = site - Size.LayerStride;
            Assume.That(colony.Grid.IsSolidTerrain(ground), Is.True, "the cell under a site is the ground");

            // Named at the ground, exactly as the picker hands it to the designate tool.
            Assert.That(Order(colony, ground), Is.EqualTo(IntentRejection.None));

            Assert.That(colony.Construction.At(ground), Is.EqualTo(BuildingHandle.None),
                "nothing is built inside the ground");
            Assert.That(colony.Construction.At(site), Is.EqualTo(BuildingHandle.Wall),
                "the order landed on the cell standing on it");
        }

        /// <summary>The mirror: naming the ground takes the site above it off again.</summary>
        [Test]
        public void CancellingAtTheGroundTakesOffTheSiteStandingOnIt()
        {
            ColonyWorld colony = Board();
            int site = SiteBesideTheStart(colony);
            Assume.That(site, Is.GreaterThanOrEqualTo(0));
            Order(colony, site);

            int ground = site - Size.LayerStride;
            colony.World.Intents.Submit(new Intent(IntentKind.CancelBuilding, Size.FromIndex(ground)));
            colony.World.Tick();

            Assert.That(colony.Construction.At(site), Is.EqualTo(BuildingHandle.None));
        }

        /// <summary>
        /// The negative control for the lift: solid rock with more solid rock above it is still a
        /// refusal, not an order placed a layer away from where it was asked for.
        /// </summary>
        [Test]
        public void TheLiftDoesNotInventASiteInsideAMassOfRock()
        {
            ColonyWorld colony = Board();

            int buried = -1;
            for (int i = 0; i < Size.CellCount - Size.LayerStride && buried < 0; i++)
                if (colony.Grid.IsSolidTerrain(i) && colony.Grid.IsSolidTerrain(i + Size.LayerStride))
                    buried = i;

            Assume.That(buried, Is.GreaterThanOrEqualTo(0), "the board has rock with rock above it");

            Assert.That(Order(colony, buried), Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(colony.Construction.Count, Is.Zero);
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

            // Real counts and real ticks, not a quantised fraction: the inspect pane is asked "how
            // much wood is missing" and "how much longer", and neither can be recovered from a byte.
            Assert.That(view.Delivered, Is.EqualTo(5));
            Assert.That(view.Cost, Is.EqualTo(5));
            Assert.That(view.IsFrame, Is.True, "all five arrived");
            Assert.That(view.WorkTotal, Is.EqualTo(135), "a wooden wall");
            Assert.That(view.WorkDone, Is.Zero);
            Assert.That(view.Progress, Is.Zero);
        }

        /// <summary>
        /// A stone wall publishes its own, dearer, work total — so the interface's "about N seconds
        /// left" is the material's answer and not the thing's.
        /// </summary>
        [Test]
        public void TheWorkPublishedIsTheMaterialsOwn()
        {
            ColonyWorld colony = Board();
            int cell = SiteBesideTheStart(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            Order(colony, cell, StuffHandle.Stone);
            colony.Construction.Deliver(cell, 5);
            colony.World.Tick();

            SiteView view = colony.World.Views.Current.Sites[0];
            Assert.That(view.WorkTotal, Is.EqualTo(244), "135 x 1.7 = 229, + stone's 15-tick offset");
        }

        // ---- the whole journey -------------------------------------------------------------------

        [Test]
        public void AnOrderedWallIsFedWorkedAndRaised()
        {
            ColonyWorld colony = Board();
            // This test is about the journey — order, delivery, work, a wall — and its wood
            // arithmetic is exact, so the builder here never botches: the roll has its own tests
            // below, and a wall in this file's journey raising on the first attempt is not one of
            // the claims being made.
            SuccessChance(colony, 1_000, 0);
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

        // ---- the success roll -----------------------------------------------------------------
        //
        // U26's last line: a completed build rolls for success against the finishing builder's
        // skill, and a failure — a botch — throws the work away and some of the material with it.
        // The reference's shape (a-04 §4: 75% at skill 0 rising to a certain 100%), re-anchored on
        // our own colonists the way every reference curve in this project is: its never-fail level
        // sits at 8, where its colonists actually are, and ours sits at 3, just above the level our
        // starting roll averages (1.16). The two integers are content, in WorkTypes.xml.

        /// <summary>
        /// Point this world's own record at a construction chance the test can reason about.
        ///
        /// <para><b>The element is replaced, never written through.</b> A record's arrays are its
        /// own, but the Defs they point at are shared by every record in the process —
        /// <c>ContentPack</c>'s own rule — so writing
        /// <c>colony.Pawns.Content.WorkTypes[i].successBasePerMille = …</c> edits the database
        /// itself and with it every test that runs afterwards. That is how this file's first
        /// version failed: three tests, three different symptoms, one shared def somebody had
        /// quietly retuned. A fresh def in this world's own array changes one world.</para>
        /// </summary>
        static void SuccessChance(ColonyWorld colony, int basePerMille, int slopePerLevel)
        {
            WorkTypeDef shipped = colony.Pawns.Content.WorkTypes[WorkTypeIndex.Construction];
            colony.Pawns.Content.WorkTypes[WorkTypeIndex.Construction] = new WorkTypeDef
            {
                defName = shipped.defName,
                label = shipped.label,
                order = shipped.order,
                successBasePerMille = basePerMille,
                successSlopePerLevel = slopePerLevel,
            };
        }

        /// <summary>
        /// The curve's shape, without pinning its numbers a second time (the content fingerprint
        /// owns the literals): a novice can fail, skill only ever helps, and a builder a colony can
        /// reasonably grow never fails at all.
        /// </summary>
        [Test]
        public void TheChanceRisesWithSkillAndStopsAtCertainty()
        {
            WorkTypeDef construction = Board().Pawns.Content.WorkTypes[WorkTypeIndex.Construction];

            Assert.That(construction.SuccessPerMille(0), Is.LessThan(1_000),
                "a novice can botch, which is the reason a roll exists");
            Assert.That(construction.SuccessPerMille(0), Is.GreaterThan(0),
                "and a novice is not certain to botch either");

            int previous = 0;
            for (int level = 0; level <= 20; level++)
            {
                int chance = construction.SuccessPerMille(level);
                Assert.That(chance, Is.GreaterThanOrEqualTo(previous),
                    $"level {level} is no worse a builder than the level below it");
                previous = chance;
            }

            int neverFails = -1;
            for (int level = 0; level <= 20; level++)
                if (construction.SuccessPerMille(level) >= 1_000) { neverFails = level; break; }
            Assert.That(neverFails, Is.GreaterThan(0).And.LessThanOrEqualTo(5),
                "certainty arrives within reach of a starting colony");
            Assert.That(construction.SuccessPerMille(20), Is.EqualTo(1_000), "and a master is certain");
        }

        /// <summary>
        /// The whole failure, seen from the site: work banked on the cell is thrown away, half the
        /// material is lost with the odd unit decided by a seeded flip, no wall appears, and the
        /// order stands waiting to be fed again. Forced to certainty of failure, because the claim
        /// is what a botch does and not how often.
        /// </summary>
        [Test]
        public void AFrameCanBeBotched()
        {
            ColonyWorld colony = Board();
            SuccessChance(colony, 0, 0);
            int cell = SiteBesideTheStart(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            int pile = colony.Pawns.Items.NearestCellWithSpace(
                colony.Grid, cell, ItemIndex.Wood, 20, JobDriver.DropSearchRadius);
            colony.Pawns.Items.Spawn(ItemIndex.Wood, pile, 20);
            Assert.That(Order(colony, cell), Is.EqualTo(IntentRejection.None));

            bool wasFed = false, botched = false, fedAgain = false;
            int kept = -1;
            for (int tick = 0; tick < 20_000; tick++)
            {
                colony.World.Tick();

                if (colony.Grid.Edifice[cell] >= 0)
                    Assert.Fail("a botched frame raised a wall anyway");

                // Order matters: a site that is a frame *after* a botch is the re-feed, so it has
                // to be tested before the plain "it is a frame" case, or the re-feed reads as the
                // first delivery and nothing ever records it.
                bool frame = colony.Construction.IsFrame(cell);
                if (frame && botched) fedAgain = true;
                else if (frame) wasFed = true;
                else if (wasFed && !botched)
                {
                    // The frame had all five; without a botch nothing but a wall can end that.
                    botched = true;
                    kept = colony.Construction.Delivered(cell);
                    Assert.That(colony.Construction.WorkDone(cell), Is.Zero,
                        "a botch throws the banked work away");
                }
            }

            Assert.That(wasFed, Is.True, "the wood was carried and the frame worked");
            Assert.That(botched, Is.True, "the completion was rolled and failed");
            Assert.That(fedAgain, Is.True, "the site is fed again afterwards — nothing wedged");
            Assert.That(colony.Construction.At(cell), Is.EqualTo(BuildingHandle.Wall),
                "the order still stands");
            Assert.That(kept, Is.EqualTo(2).Or.EqualTo(3),
                "half of five, the odd unit by the flip");
            Assert.That(OnTheGround(colony, ItemIndex.Wood), Is.LessThan(20),
                "the lost units are gone, not on the floor");
        }

        /// <summary>
        /// The roll is a pure function of (world seed, cell, tick), so the same seed botches the
        /// same way twice — the property that makes a botch survive a replay, a save and a reload.
        /// </summary>
        [Test]
        public void TheSameSeedBotchesTheSameWay()
        {
            string EventsOf(uint seed)
            {
                ColonyWorld colony = Board(seed);
                SuccessChance(colony, 500, 0);
                int cell = SiteBesideTheStart(colony);
                Assume.That(cell, Is.GreaterThanOrEqualTo(0));

                int pile = colony.Pawns.Items.NearestCellWithSpace(
                    colony.Grid, cell, ItemIndex.Wood, 20, JobDriver.DropSearchRadius);
                colony.Pawns.Items.Spawn(ItemIndex.Wood, pile, 20);
                Order(colony, cell);

                var events = new System.Text.StringBuilder();
                bool wasFrame = false;
                for (int tick = 0; tick < 12_000; tick++)
                {
                    colony.World.Tick();
                    bool frame = colony.Construction.IsFrame(cell);
                    if (!wasFrame && frame) events.Append($"fed@{tick};");
                    if (wasFrame && !frame && colony.Grid.Edifice[cell] < 0)
                        events.Append($"botched@{tick},kept{colony.Construction.Delivered(cell)};");
                    if (colony.Grid.Edifice[cell] >= 0) { events.Append($"raised@{tick};"); break; }
                    wasFrame = frame;
                }
                return events.ToString();
            }

            Assert.That(EventsOf(7u), Is.EqualTo(EventsOf(7u)),
                "one seed, one history of botches");
            Assert.That(EventsOf(7u), Does.Contain("fed@").Or.Contain("botched@"),
                "the window saw a completion roll at all");
        }

        /// <summary>
        /// On the shipped curve, with real content and no overriding: a colony that botches still
        /// raises its wall, because the site survives its own botch and the work simply starts
        /// again. The journey test above pins the never-botching arithmetic; this one proves the
        /// retry is not a wedge.
        /// </summary>
        [Test]
        public void ABotchingColonyStillRaisesItsWall()
        {
            ColonyWorld colony = Board();
            int cell = SiteBesideTheStart(colony);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            int pile = colony.Pawns.Items.NearestCellWithSpace(
                colony.Grid, cell, ItemIndex.Wood, 20, JobDriver.DropSearchRadius);
            colony.Pawns.Items.Spawn(ItemIndex.Wood, pile, 20);
            Assert.That(Order(colony, cell), Is.EqualTo(IntentRejection.None));

            int raisedAt = -1;
            for (int tick = 0; tick < 20_000 && raisedAt < 0; tick++)
            {
                colony.World.Tick();
                if (colony.Grid.Edifice[cell] >= 0) raisedAt = tick;
            }

            Assert.That(raisedAt, Is.GreaterThanOrEqualTo(0),
                "the wall went up, whatever was botched on the way");
            Assert.That(OnTheGround(colony, ItemIndex.Wood), Is.LessThanOrEqualTo(15),
                "five went into the wall and any botch took more");
        }
    }
}
