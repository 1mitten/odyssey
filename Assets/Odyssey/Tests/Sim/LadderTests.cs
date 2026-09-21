#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// U43: the way up.
    ///
    /// <para><b>The measurement that produced this unit.</b> After U29 shipped floors, every slab
    /// in the game came back <c>walkable = true, reachable = false</c> — a lone slab on a wall, the
    /// corner of a roof, the middle of a roof. A colony could build a second storey, collapse it,
    /// and never once stand on it. Vertical movement goes through a <c>Pathing.Connector</c> and
    /// connectors only ever came out of worldgen, so nothing a player built could ever open one.
    /// </para>
    ///
    /// <para>So the assertion that matters here is <b>reachability</b>, not that an edifice
    /// appeared. An edifice appearing is what the old behaviour already did.</para>
    /// </summary>
    public class LadderTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 16);

        static ColonyWorld Board()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, 1u, scenario, barren: true, wooded: false);
        }

        static Pawn TheColonist(ColonyWorld colony) => colony.Pawns.Pawns.All[0];

        static void RaiseNow(ColonyWorld colony, int cell, int building)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), building, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None), $"the order for {cell} was refused");
            colony.Construction.Raise(colony.Pawns, cell);
        }

        /// <summary>
        /// A walkable cell near the start, and the cell above it, which is where a roof will go.
        /// </summary>
        static int GroundNear(ColonyWorld colony, int radius)
        {
            CellRef start = colony.Start;
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!Size.Contains(x, z, start.Y)) continue;

                int cell = Size.Index(x, z, start.Y);
                if (colony.Grid.IsWalkable(cell)) return cell;
            }

            return -1;
        }

        /// <summary>
        /// **A ladder goes up an open shaft and you step off sideways** (owner, 2026-09-18).
        ///
        /// <para>It used to go up into the slab, and that was not a slack rule, it was the
        /// <b>only</b> rule: a connector wanted both ends walkable and <c>CellGrid.IsWalkable</c>
        /// wants a floor, so the one ladder that ever worked was a ladder with a slab directly over
        /// it — and a colonist climbing it went through the deck. The owner reported exactly that.
        /// So the cell above a ladder is now left open, the ladder makes it standable, and the floor
        /// beside it is the landing you step out on to.</para>
        ///
        /// <para>The shape: a wall, a slab on top of the wall as the landing, and the ladder in the
        /// column beside it with nothing above it at all.</para>
        /// </summary>
        static void AShaftWithALandingBesideIt(
            ColonyWorld colony, out int ground, out int shaft, out int landing)
        {
            ground = GroundNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            int beside = ground + 1;
            Assume.That(colony.Construction.Allows(beside, BuildingHandle.Wall), Is.True);
            RaiseNow(colony, beside, BuildingHandle.Wall);
            colony.World.Tick();

            shaft = ground + Size.LayerStride;
            landing = beside + Size.LayerStride;
            RaiseNow(colony, landing, BuildingHandle.Floor);
            colony.World.Tick();
        }

        /// <summary>
        /// <b>The whole unit in one test: build a storey, build a ladder, stand on the storey.</b>
        ///
        /// <para>The control comes first and is not optional — the landing must be measured
        /// unreachable <em>before</em> the ladder goes in, or a board where everything happens to
        /// be reachable would pass this without the feature existing at all.</para>
        /// </summary>
        [Test]
        public void ALadderMakesAnUpperStoreySomewhereAColonistCanGo()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);

            AShaftWithALandingBesideIt(colony, out int ground, out int shaft, out int landing);

            Assume.That(colony.Grid.IsWalkable(landing), Is.True, "the landing is a floor");
            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Colonist), Is.False,
                "the control: before the ladder there is no way up, which is what U43 exists for");

            RaiseNow(colony, ground, BuildingHandle.Ladder);
            colony.World.Tick();

            Assert.That(colony.Grid.IsWalkable(ground), Is.True,
                "a ladder must be a cell you can stand in, or it is a decoration");
            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Colonist), Is.True,
                "and now a colonist can climb the shaft and step off on to the storey");

            // The shaft cell has no floor of its own and never will: what makes it standable is the
            // connector, which is NavGrid.RefreshFrom's rule that a connector is its own floor.
            Assert.That(colony.Grid.HasFloor(shaft), Is.False,
                "the top of the shaft is open — that is the whole point of the change");
        }

        /// <summary>
        /// The order the player builds in must not matter. A ladder put up before anything is beside
        /// its top registers nothing and is simply a thing standing there; the landing arriving is
        /// what completes the pair.
        ///
        /// <para>Before 2026-09-18 this said "the floor <i>over</i> it", and that arrangement is now
        /// refused outright. What is order-independent is the same rule seen from its new side.</para>
        /// </summary>
        [Test]
        public void ItDoesNotMatterWhetherTheLadderOrTheLandingComesFirst()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);

            int ground = GroundNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            int beside = ground + 1;
            RaiseNow(colony, beside, BuildingHandle.Wall);
            colony.World.Tick();

            // Ladder first, into open air with nothing beside its top.
            RaiseNow(colony, ground, BuildingHandle.Ladder);
            colony.World.Tick();

            int landing = beside + Size.LayerStride;
            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Colonist), Is.False,
                "a ladder to nowhere opens nothing");

            // Then the landing beside its top.
            RaiseNow(colony, landing, BuildingHandle.Floor);
            colony.World.Tick();

            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Colonist), Is.True,
                "the landing arriving is what completes the pair");
        }

        /// <summary>
        /// **The refusal the owner asked for**: <i>"a ladder shouldn't be able to be built if there
        /// is a slab directly above because colonists go through the floor"</i>.
        /// </summary>
        [Test]
        public void ALadderIsRefusedUnderASlab()
        {
            ColonyWorld colony = Board();

            int ground = GroundNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            RaiseNow(colony, ground + 1, BuildingHandle.Wall);
            colony.World.Tick();
            Assume.That(colony.Construction.Allows(ground, BuildingHandle.Ladder), Is.True,
                "the control: with the shaft open, the ladder is perfectly legal");

            RaiseNow(colony, ground + Size.LayerStride, BuildingHandle.Floor);
            colony.World.Tick();

            Assert.That(colony.Construction.Allows(ground, BuildingHandle.Ladder), Is.False,
                "a ladder under a slab is a ladder climbed through the floor");
            Assert.That(colony.Construction.Place(
                    Size.FromIndex(ground), BuildingHandle.Ladder, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.NotPermitted),
                "and the order itself is refused, not merely discouraged");
        }

        /// <summary>
        /// The same rule from the other side, and it is not optional: without it the refusal above
        /// is walked around in two moves — build the ladder first, pour the floor over it after.
        /// </summary>
        [Test]
        public void ASlabIsRefusedDirectlyOverALadder()
        {
            ColonyWorld colony = Board();

            int ground = GroundNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            RaiseNow(colony, ground + 1, BuildingHandle.Wall);
            colony.World.Tick();

            int shaft = ground + Size.LayerStride;
            Assume.That(colony.Construction.Allows(shaft, BuildingHandle.Floor), Is.True,
                "the control: that slab is legal while nothing is under it");

            RaiseNow(colony, ground, BuildingHandle.Ladder);
            colony.World.Tick();

            Assert.That(colony.Construction.Allows(shaft, BuildingHandle.Floor), Is.False,
                "capping a ladder with a floor is the same fault approached backwards");
        }

        /// <summary>
        /// <b>The refusal must see a blueprint, or two legal orders make an illegal building.</b>
        ///
        /// <para>This is the owner's third report of 2026-09-18 — *"sometimes the colonists climb up
        /// the ladder where there is wall or slab directly above"* — and "sometimes" was the tell:
        /// it depended on which job a colonist happened to pick up. The shaft rule asked the
        /// <i>built</i> world, so a ladder that was still a site was invisible to the floor's
        /// refusal and a floor that was still a site was invisible to the ladder's. Order both
        /// before either is carried out and each is permitted on its own.</para>
        ///
        /// <para>Both orders, because the fault was symmetrical and a test that only gives them one
        /// way round proves half of it.</para>
        /// </summary>
        [Test]
        public void TwoOrdersCannotCombineIntoALadderUnderAFloor([Values(true, false)] bool ladderFirst)
        {
            ColonyWorld colony = Board();

            int ground = GroundNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));
            int shaft = ground + Size.LayerStride;

            RaiseNow(colony, ground + 1, BuildingHandle.Wall);
            colony.World.Tick();

            CellRef ladderAt = Size.FromIndex(ground);
            CellRef floorAt = Size.FromIndex(shaft);

            IntentRejection first = ladderFirst
                ? colony.Construction.Place(ladderAt, BuildingHandle.Ladder, StuffHandle.Wood)
                : colony.Construction.Place(floorAt, BuildingHandle.Floor, StuffHandle.Wood);
            Assume.That(first, Is.EqualTo(IntentRejection.None),
                "the control: the first order is legal on its own, which is why this hid");

            IntentRejection second = ladderFirst
                ? colony.Construction.Place(floorAt, BuildingHandle.Floor, StuffHandle.Wood)
                : colony.Construction.Place(ladderAt, BuildingHandle.Ladder, StuffHandle.Wood);

            Assert.That(second, Is.EqualTo(IntentRejection.NotPermitted),
                "a waiting order is as much a ladder, or as much a floor, as a built one");
        }

        /// <summary>
        /// <b>A shaft may rise more than one storey.</b> A ladder is <c>blocking false</c> so that a
        /// colonist can stand in it, and <c>SomethingUnderfoot</c> wants a floor or a
        /// <i>blocking</i> edifice — so until 2026-09-18 the second ladder of a chain was refused,
        /// every shaft was exactly one storey, and <see cref="LadderTests"/> never noticed because
        /// every test here builds one ladder. Found by probe while hunting the report above.
        /// </summary>
        [Test]
        public void ALadderMayStandOnALadderSoAShaftCanRiseMoreThanOneStorey()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);

            int ground = GroundNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            // Two storeys of wall beside the shaft, and the landing on top of them.
            int beside = ground + 1;
            RaiseNow(colony, beside, BuildingHandle.Wall);
            colony.World.Tick();
            RaiseNow(colony, beside + Size.LayerStride, BuildingHandle.Wall);
            colony.World.Tick();

            RaiseNow(colony, ground, BuildingHandle.Ladder);
            colony.World.Tick();
            RaiseNow(colony, ground + Size.LayerStride, BuildingHandle.Ladder);
            colony.World.Tick();

            int landing = beside + 2 * Size.LayerStride;
            RaiseNow(colony, landing, BuildingHandle.Floor);
            colony.World.Tick();

            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Colonist), Is.True,
                "two ladders are a two-storey shaft, and the landing is at the top of it");
        }

        /// <summary>
        /// <b>Pull the bottom ladder out of a chain and the whole shaft closes</b>, not just its
        /// lower half. A ladder stands on the one below it now, so the fan-out has to reach
        /// <i>upwards</i> as well — and it did not, because until chains existed nothing above a
        /// cell could depend on it. Without that line the upper ladder keeps a connector whose foot
        /// is mid-air.
        /// </summary>
        [Test]
        public void PullingTheBottomOutOfAChainClosesTheWholeShaft()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);

            int ground = GroundNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            int beside = ground + 1;
            RaiseNow(colony, beside, BuildingHandle.Wall);
            colony.World.Tick();
            RaiseNow(colony, beside + Size.LayerStride, BuildingHandle.Wall);
            colony.World.Tick();
            RaiseNow(colony, ground, BuildingHandle.Ladder);
            colony.World.Tick();
            RaiseNow(colony, ground + Size.LayerStride, BuildingHandle.Ladder);
            colony.World.Tick();

            int landing = beside + 2 * Size.LayerStride;
            RaiseNow(colony, landing, BuildingHandle.Floor);
            colony.World.Tick();
            Assume.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Colonist), Is.True);

            Assert.That(colony.Construction.Demolish(colony.Pawns, ground, out _), Is.True);
            colony.World.Tick();

            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Colonist), Is.False,
                "the upper ladder's connector must not outlive the footing it stands on");
        }

        /// <summary>
        /// <b>Roofing over the top of a working shaft is refused</b>, and it used not to be: the
        /// slab went down, the ladder's top stopped being somewhere to arrive, and a way up closed
        /// with nothing said to the player at all. The owner's answer was to refuse it, for the
        /// reason the slab-over-a-ladder rule is refused — silently breaking what somebody built is
        /// worse than telling them no.
        /// </summary>
        [Test]
        public void TheTopOfAWorkingShaftMayNotBeRoofedOver()
        {
            ColonyWorld colony = Board();

            AShaftWithALandingBesideIt(colony, out int ground, out int shaft, out _);
            int cap = shaft + Size.LayerStride;
            RaiseNow(colony, ground + 1 + Size.LayerStride, BuildingHandle.Wall);
            colony.World.Tick();
            Assume.That(colony.Construction.Allows(cap, BuildingHandle.Floor), Is.True,
                "the control: that slab is supported and legal while no ladder is under the shaft");

            RaiseNow(colony, ground, BuildingHandle.Ladder);
            colony.World.Tick();

            Assert.That(colony.Construction.Allows(cap, BuildingHandle.Floor), Is.False,
                "a roof over the open top of a shaft closes the way up, so it is refused");
        }

        /// <summary>
        /// Take the ladder away and the way up goes with it. Without this the portal outlives the
        /// thing, which is a colonist walking up a ladder that is not there.
        /// </summary>
        [Test]
        public void PullingTheLadderOutClosesTheWayUp()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);

            AShaftWithALandingBesideIt(colony, out int ground, out _, out int landing);
            RaiseNow(colony, ground, BuildingHandle.Ladder);
            colony.World.Tick();
            Assume.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Colonist), Is.True);

            Assert.That(colony.Construction.Demolish(colony.Pawns, ground, out _), Is.True);
            colony.World.Tick();

            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Colonist), Is.False,
                "the portal must not outlive the ladder");
        }

        /// <summary>
        /// <b>A hauler cannot use a ladder, and this is the test that says so out loud.</b>
        ///
        /// <para><c>Connector</c> has always excluded haulers from a ladder — *"a hauler's bulky
        /// load and an animal's lack of hands both rule a ladder out"* — and the consequence only
        /// became visible when ladders became buildable: <b>a colonist can climb to an upper storey
        /// but cannot carry building material up there</b>, so nothing can be built on it. That is
        /// what stairs are for, and it is why they are the next unit rather than a maybe.</para>
        ///
        /// <para>It cost a wrong diagnosis to find. The first version of the test above asked
        /// <c>Reachable(pawn, roof)</c>, which uses the pawn's <em>current job's</em> mode; the
        /// colonist happened to be mid-haul, the answer came back false, and the feature looked
        /// broken when it was working. Pinning the rule here means the next person reads it rather
        /// than rediscovering it the same way.</para>
        /// </summary>
        [Test]
        public void AHaulerCannotClimbALadderSoNothingCanBeCarriedUpOne()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);

            AShaftWithALandingBesideIt(colony, out int ground, out _, out int landing);
            RaiseNow(colony, ground, BuildingHandle.Ladder);
            colony.World.Tick();

            Assume.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Colonist), Is.True,
                "the way up is open to a colonist");
            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Hauler), Is.False,
                "and closed to a hauler, so a ladder alone cannot supply an upper storey");
        }

        /// <summary>
        /// <b>And the storey a ladder reaches can still be built on</b> (owner, 2026-09-21:
        /// <i>"Ladders are fine as they are - we should be able to be build a storey as long as
        /// there is room above - this would make it much easier to stack ladders/platforms."</i>).
        ///
        /// <para><b>This is the test whose absence let a false claim stand in four documents and
        /// then let the opposite one break a playtest.</b> The test above proves a hauler is
        /// excluded from a ladder; it says nothing about which jobs use that mode, and
        /// <c>28-stairs.md</c> §3 records the general form: <i>a test that asserts a rule is not a
        /// test that asserts the rule is reached.</i> U44 then set
        /// <c>DeliverWorkGiver</c> to <c>Hauler</c> on the strength of the first sentence and made
        /// a ladder-only storey unbuildable — which is a bootstrap deadlock, because the stair
        /// meant to replace the ladder is itself a building order needing material delivered up
        /// there. Measured on the owner's save: the construction pocket around their stair order
        /// held 81 walkable cells for a colonist and 20 for a hauler.</para>
        ///
        /// <para>So the rule is stated from the end that matters — <b>a wall ordered on the landing
        /// actually gets built</b> — rather than by reading a mode constant back. A test on
        /// <c>DeliverWorkGiver.Mode</c> would pass on the broken version too.</para>
        /// </summary>
        [Test]
        public void ABuildingOrderOnALadderOnlyStoreyIsFedAndFinished()
        {
            ColonyWorld colony = Board();
            AShaftWithALandingBesideIt(colony, out int ground, out _, out int landing);
            RaiseNow(colony, ground, BuildingHandle.Ladder);
            colony.World.Tick();

            // **A wall on an upper deck, and it has to be a wall.** A slab can be built from
            // underneath — StandToBuild falls back to the cell below a slab and then to the cells
            // beside that one, so "a colonist floors over its own head" stays possible — which
            // means a deck plate up here is reachable from the ground and measures nothing at all.
            // The first version of this test ordered one and passed in both modes. A wall is only
            // ever built from beside it, on its own storey.
            RaiseNow(colony, landing + 1, BuildingHandle.Floor);
            colony.World.Tick();

            int site = landing + 1;
            Assert.That(colony.Construction.Allows(site, BuildingHandle.Wall), Is.True,
                "the fixture has to offer a cell up there that only an upper-storey stance reaches");

            // Asked of the LANDING and not of the site: the site is where the wall goes, and the
            // only cell a builder can stand in to raise it is the landing beside it.
            Pawn pawn = TheColonist(colony);
            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Hauler), Is.False,
                "the control: a hauler cannot get up there, so the storey is ladder-only");
            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Colonist), Is.True,
                "...and a colonist can, which is the whole difference this test turns on");

            // Wood on the ground below, where felling would have left it.
            int pile = colony.Pawns.Items.NearestCellWithSpace(
                colony.Grid, ground, ItemIndex.Wood, 20, JobDriver.DropSearchRadius);
            Assert.That(pile, Is.GreaterThanOrEqualTo(0));
            colony.Pawns.Items.Spawn(ItemIndex.Wood, pile, 20);
            Assert.That(colony.Pawns.Reachable(pawn, pile, TraverseMode.Hauler), Is.True,
                "and the wood starts somewhere a hauler could reach, so the only thing in the way "
                + "of delivering it is the climb");

            Assert.That(
                colony.Construction.Place(Size.FromIndex(site), BuildingHandle.Wall, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None));

            bool fed = false;
            for (int tick = 0; tick < 40_000 && colony.Grid.Edifice[site] < 0; tick++)
            {
                colony.World.Tick();
                if (colony.Construction.IsFrame(site)) fed = true;
            }

            Assert.That(fed, Is.True,
                "no wood ever reached a site one ladder up, so nothing can be built above ground");
            Assert.That(colony.Grid.Edifice[site], Is.GreaterThanOrEqualTo(0),
                "the wall on the ladder-only storey was never finished");
        }

        /// <summary>
        /// <b>A ladder survives a save.</b> Its connector does not go in the file — it is derived,
        /// like structural support and the region graph — so this is the test that the deriving
        /// actually happens. Without it a loaded colony keeps its ladders and loses every way up,
        /// which is the sort of fault that shows up three saves later.
        /// </summary>
        [Test]
        public void AWayUpSurvivesASaveAndALoad()
        {
            ColonyWorld colony = Board();
            AShaftWithALandingBesideIt(colony, out int ground, out _, out int landing);
            RaiseNow(colony, ground, BuildingHandle.Ladder);
            colony.World.Tick();
            Assume.That(colony.Pawns.Reachable(TheColonist(colony), landing, TraverseMode.Colonist), Is.True);

            byte[] saved = colony.Save();

            ColonyWorld loaded = Board();
            loaded.Load(saved);

            Assert.That(loaded.Grid.Edifice[ground], Is.GreaterThanOrEqualTo(0), "the ladder came back");
            Assert.That(loaded.Pawns.Reachable(TheColonist(loaded), landing, TraverseMode.Colonist), Is.True,
                "and so did the way up it opens");
        }
    }
}
