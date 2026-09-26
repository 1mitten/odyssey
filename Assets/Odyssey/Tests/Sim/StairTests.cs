#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Storage;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// U44: the stair, the way up a <b>hauler</b> can use (docs/design/63-stairs.md).
    ///
    /// <para>Shaped after <c>LadderTests</c> on purpose: the stair is the ladder's rules asked of a
    /// thing two cells long, and the assertion that matters is again <b>reachability</b> — above
    /// all in <see cref="TraverseMode.Hauler"/>, which a ladder is closed to — not that an edifice
    /// appeared.</para>
    ///
    /// <para><b>The fixture.</b> A row of five cells running east. Two walls at its east end carry
    /// the upper storey's floor — the landing — and one wall at its west end carries a single slab,
    /// so the cell over the foot can be floored for the refusal tests. The stair stands in the two
    /// cells between, climbing east: foot, then upper half, then the landing one layer up. Nothing
    /// on the upper storey stands on terrain, so a hop cannot reach it and the stair is the only
    /// way up.</para>
    /// </summary>
    public class StairTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 16);
        const int East = 1;

        static ColonyWorld Board()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, 1u, scenario, barren: true, wooded: false);
        }

        static Pawn TheColonist(ColonyWorld colony) => colony.Pawns.Pawns.All[0];

        static void RaiseNow(ColonyWorld colony, int cell, int building, int facing = 0)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), building, StuffHandle.Wood, facing),
                Is.EqualTo(IntentRejection.None), $"the order for {building} at {cell} was refused");
            colony.Construction.Raise(colony.Pawns, cell);
            colony.World.Tick();
        }

        static void Run(ColonyWorld colony, int ticks)
        {
            for (int i = 0; i < ticks; i++) colony.World.Tick();
        }

        /// <summary>The cells of the fixture, all named from the foot.</summary>
        struct Flight
        {
            public int West, Foot, Upper, EastA, EastB;
            public int OverWest, OverFoot, Top, Landing, Far;
        }

        /// <summary>Five walkable cells in a row running east, near the start, or -1.</summary>
        static int RowOfFive(ColonyWorld colony)
        {
            CellRef start = colony.Start;
            for (int radius = 2; radius < 12; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!Size.Contains(x, z, start.Y) || !Size.Contains(x + 4, z, start.Y)) continue;

                bool ok = true;
                for (int i = 0; i < 5 && ok; i++)
                {
                    int cell = Size.Index(x + i, z, start.Y);
                    ok = colony.Grid.IsWalkable(cell) && colony.Construction.Allows(cell)
                         && colony.Pawns.Items.CellHasSpace(cell);
                }

                if (ok) return Size.Index(x, z, start.Y);
            }

            return -1;
        }

        /// <summary>The walls and floors round a stair's two cells; the stair itself is the test's.</summary>
        static Flight APlatformToClimbTo(ColonyWorld colony)
        {
            int west = RowOfFive(colony);
            Assume.That(west, Is.GreaterThanOrEqualTo(0), "no row of five open cells near the start");

            int up = Size.LayerStride;
            var f = new Flight
            {
                West = west, Foot = west + 1, Upper = west + 2, EastA = west + 3, EastB = west + 4,
            };
            f.OverWest = f.West + up;
            f.OverFoot = f.Foot + up;
            f.Top = f.Upper + up;
            f.Landing = f.EastA + up;
            f.Far = f.EastB + up;

            RaiseNow(colony, f.EastA, BuildingHandle.Wall);
            RaiseNow(colony, f.EastB, BuildingHandle.Wall);
            RaiseNow(colony, f.West, BuildingHandle.Wall);
            RaiseNow(colony, f.Landing, BuildingHandle.Floor);
            RaiseNow(colony, f.Far, BuildingHandle.Floor);
            RaiseNow(colony, f.OverWest, BuildingHandle.Floor);
            return f;
        }

        // ---------------------------------------------------------------- the way up

        /// <summary>
        /// <b>The unit in one test: a stair makes the storey above somewhere a hauler can go.</b>
        /// The control comes first — unreachable in every mode before the stair — and then the one
        /// difference from a ladder, stated as an assertion: the <b>hauler</b> gets up too.
        /// </summary>
        [Test]
        public void AStairMakesTheStoreyAboveSomewhereAHaulerCanGo()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);
            Flight f = APlatformToClimbTo(colony);

            Assume.That(colony.Grid.IsWalkable(f.Landing), Is.True, "the landing is a floor");
            Assert.That(colony.Pawns.Reachable(pawn, f.Landing, TraverseMode.Colonist), Is.False,
                "the control: before the stair there is no way up at all");
            Assert.That(colony.Pawns.Reachable(pawn, f.Landing, TraverseMode.Hauler), Is.False);

            RaiseNow(colony, f.Foot, BuildingHandle.Stair, East);

            Assert.That(colony.Grid.Edifice[f.Upper], Is.EqualTo(colony.Grid.Edifice[f.Foot]),
                "one stair, one record, two cells");
            Assert.That(colony.Pawns.Reachable(pawn, f.Landing, TraverseMode.Colonist), Is.True,
                "a colonist can walk up");
            Assert.That(colony.Pawns.Reachable(pawn, f.Landing, TraverseMode.Hauler), Is.True,
                "and so can a hauler with a load, which is the whole reason a stair exists");
            Assert.That(colony.Grid.HasFloor(f.Top), Is.False,
                "the top of the flight is open: what makes it standable is the connector");
        }

        /// <summary>
        /// <b>A hauler carries a load up the stair to a stockpile on the storey above</b> — the
        /// giver, the driver, the path and the mover, end to end — and on the way pays the
        /// connector's price for the step up, because the price has one owner (design 63 §7).
        /// </summary>
        [Test]
        public void AHaulerCarriesALoadUpTheStairToAStockpileAbove()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);
            Flight f = APlatformToClimbTo(colony);
            RaiseNow(colony, f.Foot, BuildingHandle.Stair, East);

            // Everything the scenario laid out is forbidden, so the one stack this test puts down
            // is the only thing anybody has a reason to carry.
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].Cell >= 0) items[i].Forbidden = true;

            Assert.That(colony.Pawns.Storage!.Designate(Size.FromIndex(f.Far), f.Far, StoragePreset.Everything),
                Is.EqualTo(IntentRejection.None), "the stockpile on the upper storey");

            int ground = -1;
            CellRef start = colony.Start;
            for (int r = 1; r < 10 && ground < 0; r++)
            for (int dz = -r; dz <= r && ground < 0; dz++)
            for (int dx = -r; dx <= r && ground < 0; dx++)
            {
                if (!Size.Contains(start.X + dx, start.Z + dz, start.Y)) continue;
                int cell = Size.Index(start.X + dx, start.Z + dz, start.Y);
                if (cell == f.Foot || cell == f.Upper || cell == f.West) continue;
                if (colony.Grid.IsWalkable(cell) && colony.Pawns.Items.CellHasSpace(cell)
                    && colony.Grid.Edifice[cell] < 0)
                    ground = cell;
            }
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));
            colony.Pawns.Items.Spawn(ItemIndex.Wood, ground, 20);

            int upCharged = -1, downCharged = -1;
            bool stowed = false;
            for (int tick = 0; tick < 8_000 && !(stowed && downCharged >= 0); tick++)
            {
                colony.World.Tick();
                if (pawn.HasPath)
                {
                    int next = pawn.Path[pawn.PathIndex];
                    if (pawn.Cell == f.Foot && next == f.Top) upCharged = pawn.MoveStepCost;
                    if (pawn.Cell == f.Top && next == f.Foot) downCharged = pawn.MoveStepCost;
                }

                ColonyItem? there = colony.Pawns.Items.ItemAt(f.Far);
                stowed = there != null && there.DefIndex == ItemIndex.Wood;
            }

            ColonyItem? stored = colony.Pawns.Items.ItemAt(f.Far);
            Assert.That(stored, Is.Not.Null, "the wood reached the stockpile on the storey above");
            Assert.That(stored!.DefIndex, Is.EqualTo(ItemIndex.Wood));
            Assert.That(stored.Stack, Is.EqualTo(20), "all of it, carried up in one trip");
            Assert.That(colony.Pawns.Items.ItemAt(ground), Is.Null, "and none of it left behind");

            var connector = new Connector(0, ConnectorKind.Stair,
                new[] { f.Foot }, new[] { f.Top }, Size);
            Assert.That(upCharged, Is.EqualTo(connector.CostUp * Rates.Scale),
                "the mover charged the connector's price for the step up the stair");
            if (downCharged >= 0)
                Assert.That(downCharged, Is.EqualTo(connector.CostDown * Rates.Scale),
                    "and the connector's price for the step down");
        }

        /// <summary>
        /// The region graph's edge for a built stair carries the connector's price both ways,
        /// measured off the built graph rather than read off the source — the assertion that would
        /// still fail if the link were ever priced by a second rule (design 63 §7).
        /// </summary>
        [Test]
        public void TheRegionGraphPricesABuiltStairAtTheConnectorsPrice()
        {
            ColonyWorld colony = Board();
            Flight f = APlatformToClimbTo(colony);
            RaiseNow(colony, f.Foot, BuildingHandle.Stair, East);

            NavGraph nav = colony.Pawns.Nav;
            int footRegion = nav.RegionOfCell(f.Foot);
            int topRegion = nav.RegionOfCell(f.Top);
            Assume.That(footRegion, Is.Not.EqualTo(topRegion));

            int found = -1;
            int startAt = nav.AdjacencyStart(footRegion);
            for (int i = 0; i < nav.AdjacencyCount(footRegion); i++)
            {
                int link = nav.AdjacencyLink(startAt + i);
                if (nav.KindOfLink(link) != LinkKind.Portal) continue;
                if (nav.LinkOther(link, footRegion) != topRegion) continue;
                found = link;
                break;
            }

            Assert.That(found, Is.Not.EqualTo(-1), "no portal between the foot and the top");
            var connector = new Connector(0, ConnectorKind.Stair, new[] { f.Foot }, new[] { f.Top }, Size);
            Assert.That(nav.LinkCostFrom(found, footRegion), Is.EqualTo(connector.CostUp));
            Assert.That(nav.LinkCostFrom(found, topRegion), Is.EqualTo(connector.CostDown));
        }

        /// <summary>
        /// <b>The ladder's refresh must not touch a stair.</b> A built stair declares one cell at
        /// each end, which is what a ladder's connector looks like; before the lookup asked the
        /// kind, an edit that refreshed "the ladder" at a stair's foot found the stair's connector,
        /// saw no ladder there, and tore it out (design 63 §3). The stair's own refresh put it
        /// back a moment later, so reachability alone cannot see it: the evidence is that the
        /// connector is the <b>same one</b> afterwards, not a new registration — measured with the
        /// kind test removed, the id moved.
        ///
        /// <para>The edit is a floor laid on a wall beside the foot: the floor sits beside the cell
        /// over the foot, and a slab there is a landing for any ladder standing in the foot, so the
        /// ladder's fan-out refreshes the foot.</para>
        /// </summary>
        [Test]
        public void AnEditBesideTheFootLeavesTheStairWorking()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);
            Flight f = APlatformToClimbTo(colony);
            RaiseNow(colony, f.Foot, BuildingHandle.Stair, East);
            Assume.That(colony.Pawns.Reachable(pawn, f.Landing, TraverseMode.Hauler), Is.True);

            // A wall raised beside the foot, on the side away from the climb.
            CellRef foot = Size.FromIndex(f.Foot);
            int beside = -1;
            foreach (int dz in new[] { 1, -1 })
            {
                if (!Size.Contains(foot.X, foot.Z + dz, foot.Y)) continue;
                int cell = Size.Index(foot.X, foot.Z + dz, foot.Y);
                if (colony.Construction.Allows(cell)) { beside = cell; break; }
            }
            Assume.That(beside, Is.GreaterThanOrEqualTo(0));
            RaiseNow(colony, beside, BuildingHandle.Wall);

            int before = colony.Pawns.Nav.OneCellConnectorAt(f.Foot, ConnectorKind.Stair);
            Assume.That(before, Is.GreaterThanOrEqualTo(0), "the stair has its connector");
            Assert.That(colony.Pawns.Nav.OneCellConnectorAt(f.Foot), Is.EqualTo(-1),
                "and asked for a ladder, the foot of a stair has none");

            RaiseNow(colony, beside + Size.LayerStride, BuildingHandle.Floor);

            Assert.That(colony.Pawns.Nav.OneCellConnectorAt(f.Foot, ConnectorKind.Stair), Is.EqualTo(before),
                "the ladder's refresh at the foot left the stair's connector alone");
            Assert.That(colony.Pawns.Reachable(pawn, f.Landing, TraverseMode.Hauler), Is.True,
                "and the way up still works");
        }

        /// <summary>
        /// Taking the landing away strands the stair, as it strands a ladder: a slab removed from
        /// beside the top has to reach the stair's connector (design 63 §4) — through
        /// <c>RemoveSlab</c>, which refreshed only ladders until stairs existed.
        /// </summary>
        [Test]
        public void TakingTheLandingAwayClosesTheStair()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);
            Flight f = APlatformToClimbTo(colony);
            RaiseNow(colony, f.Foot, BuildingHandle.Stair, East);
            Assume.That(colony.Pawns.Reachable(pawn, f.Landing, TraverseMode.Hauler), Is.True);

            // The far slab first, so the one beside the top is not holding anything else up, then
            // the landing itself.
            Assert.That(colony.Construction.RemoveSlab(colony.Pawns, f.Far, out _), Is.True);
            Assert.That(colony.Construction.RemoveSlab(colony.Pawns, f.Landing, out _), Is.True);
            colony.World.Tick();

            Assert.That(colony.Pawns.Nav.OneCellConnectorAt(f.Foot, ConnectorKind.Stair), Is.EqualTo(-1),
                "a stair with nothing to step off on to opens nothing");
        }

        /// <summary>
        /// The player may build the stair first or the landing first (design 21 §4's argument): a
        /// stair to nowhere opens nothing, and the landing arriving is what completes it.
        /// </summary>
        [Test]
        public void ItDoesNotMatterWhetherTheStairOrTheLandingComesFirst()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);

            int west = RowOfFive(colony);
            Assume.That(west, Is.GreaterThanOrEqualTo(0));
            int foot = west + 1, eastA = west + 3;
            int landing = eastA + Size.LayerStride;

            RaiseNow(colony, eastA, BuildingHandle.Wall);
            RaiseNow(colony, foot, BuildingHandle.Stair, East);
            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Hauler), Is.False,
                "a stair with nothing to step off on to opens nothing");

            RaiseNow(colony, landing, BuildingHandle.Floor);
            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Hauler), Is.True,
                "the landing arriving is what completes it");
        }

        // ---------------------------------------------------------------- the stairwell

        /// <summary>
        /// <b>A stair is refused under a floor</b>, over either half of the flight, whether the
        /// floor is built or only ordered — the ladder's shaft rule two cells long (design 63 §4).
        /// And the cursor's question, <see cref="ConstructionGrid.AllowsFootprint"/>, agrees with
        /// the click, so the ghost turns red for the reason the order is refused.
        /// </summary>
        [Test]
        public void AStairIsRefusedUnderAFloor(
            [Values(true, false)] bool overTheFoot, [Values(true, false)] bool built)
        {
            ColonyWorld colony = Board();
            Flight f = APlatformToClimbTo(colony);

            Assume.That(colony.Construction.AllowsFootprint(f.Foot, BuildingHandle.Stair, East), Is.True,
                "the control: with the stairwell open, the stair is perfectly legal");

            int over = overTheFoot ? f.OverFoot : f.Top;
            Assert.That(colony.Construction.Place(Size.FromIndex(over), BuildingHandle.Floor, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None), "the fixture: that floor is supported and legal");
            if (built)
            {
                colony.Construction.Raise(colony.Pawns, over);
                colony.World.Tick();
            }

            Assert.That(colony.Construction.AllowsFootprint(f.Foot, BuildingHandle.Stair, East), Is.False,
                "the ghost is refused");
            Assert.That(colony.Construction.Place(Size.FromIndex(f.Foot), BuildingHandle.Stair, StuffHandle.Wood, East),
                Is.EqualTo(IntentRejection.NotPermitted),
                "and so is the order: a flight under a floor is climbed into the ceiling");
        }

        /// <summary>
        /// The same rule from the other side, or it is walked round in two moves: a floor is
        /// refused over either half of a stair, built or only ordered.
        /// </summary>
        [Test]
        public void AFloorIsRefusedOverEitherHalfOfAStair([Values(true, false)] bool built)
        {
            ColonyWorld colony = Board();
            Flight f = APlatformToClimbTo(colony);

            Assume.That(colony.Construction.Allows(f.OverFoot, BuildingHandle.Floor), Is.True,
                "the control: before the stair a floor over the foot is legal");
            Assume.That(colony.Construction.Allows(f.Top, BuildingHandle.Floor), Is.True,
                "and over the upper half");

            Assert.That(colony.Construction.Place(Size.FromIndex(f.Foot), BuildingHandle.Stair, StuffHandle.Wood, East),
                Is.EqualTo(IntentRejection.None));
            if (built)
            {
                colony.Construction.Raise(colony.Pawns, f.Foot);
                colony.World.Tick();
            }

            Assert.That(colony.Construction.Allows(f.OverFoot, BuildingHandle.Floor), Is.False,
                "no floor over the foot of a stair");
            Assert.That(colony.Construction.Allows(f.Top, BuildingHandle.Floor), Is.False,
                "and none over its upper half");
            Assert.That(colony.Construction.Place(Size.FromIndex(f.Top), BuildingHandle.Floor, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.NotPermitted));
        }

        /// <summary>
        /// Both halves are furniture's clear cells: a stack lying in the upper half refuses the
        /// stair exactly as one in the foot does, where a bed's far cell has only ever been asked
        /// the wall's question (design 63 §5).
        /// </summary>
        [Test]
        public void AStackInTheUpperHalfRefusesTheStair()
        {
            ColonyWorld colony = Board();
            Flight f = APlatformToClimbTo(colony);
            colony.Pawns.Items.Spawn(ItemIndex.Wood, f.Upper, 5);

            Assert.That(colony.Construction.AllowsFootprint(f.Foot, BuildingHandle.Stair, East), Is.False);
            Assert.That(colony.Construction.Place(Size.FromIndex(f.Foot), BuildingHandle.Stair, StuffHandle.Wood, East),
                Is.EqualTo(IntentRejection.NotPermitted));
        }

        // ---------------------------------------------------------------- taking it down

        /// <summary>
        /// <b>Deconstruction</b>: the ordinary order, named at the upper half, takes the whole stair
        /// — both cells from the one record — gives half its material back, and closes the way up.
        /// </summary>
        [Test]
        public void DeconstructingAStairTakesTheWayUpWithIt()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);
            Flight f = APlatformToClimbTo(colony);
            RaiseNow(colony, f.Foot, BuildingHandle.Stair, East);
            Assume.That(colony.Pawns.Reachable(pawn, f.Landing, TraverseMode.Hauler), Is.True);

            int woodBefore = WoodOnTheGround(colony);
            Assert.That(colony.Designations.Designate(Size.FromIndex(f.Upper), DesignationKind.Deconstruct),
                Is.EqualTo(IntentRejection.None), "either half names the stair");

            int downAt = -1;
            for (int tick = 0; tick < 20_000 && downAt < 0; tick++)
            {
                colony.World.Tick();
                if (colony.Grid.Edifice[f.Foot] < 0) downAt = tick;
            }

            Assert.That(downAt, Is.GreaterThanOrEqualTo(0), "nobody took the stair down");
            Assert.That(colony.Grid.Edifice[f.Upper], Is.LessThan(0), "and neither half is left behind");
            colony.World.Tick();
            Assert.That(colony.Pawns.Reachable(pawn, f.Landing, TraverseMode.Hauler), Is.False,
                "the way up went with it");
            Assert.That(colony.Pawns.Reachable(pawn, f.Landing, TraverseMode.Colonist), Is.False);
            Assert.That(WoodOnTheGround(colony) - woodBefore, Is.EqualTo(4),
                "half of an eight-wood stair comes back");
        }

        /// <summary>The portal must not outlive the stair, however the stair comes down.</summary>
        [Test]
        public void DemolishingAStairClosesTheWayUp()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);
            Flight f = APlatformToClimbTo(colony);
            RaiseNow(colony, f.Foot, BuildingHandle.Stair, East);
            Assume.That(colony.Pawns.Reachable(pawn, f.Landing, TraverseMode.Hauler), Is.True);

            Assert.That(colony.Construction.Demolish(colony.Pawns, f.Upper, out _), Is.True);
            colony.World.Tick();

            Assert.That(colony.Pawns.Reachable(pawn, f.Landing, TraverseMode.Hauler), Is.False,
                "a hauler walking up a stair that is not there");
        }

        // ---------------------------------------------------------------- a save

        /// <summary>
        /// <b>A stair survives a save.</b> Its connector is derived, not written down, so this is
        /// the test that the deriving happens — and that the facing came back with the record, or
        /// the loaded stair would climb the wrong way and arrive nowhere.
        /// </summary>
        [Test]
        public void AWayUpAStairSurvivesASaveAndALoad()
        {
            ColonyWorld colony = Board();
            Flight f = APlatformToClimbTo(colony);
            RaiseNow(colony, f.Foot, BuildingHandle.Stair, East);
            Assume.That(colony.Pawns.Reachable(TheColonist(colony), f.Landing, TraverseMode.Hauler), Is.True);

            byte[] saved = colony.Save();
            ColonyWorld loaded = Board();
            loaded.Load(saved);

            Assert.That(loaded.Grid.Edifice[f.Foot], Is.GreaterThanOrEqualTo(0), "the stair came back");
            Assert.That(loaded.Grid.Edifice[f.Upper], Is.EqualTo(loaded.Grid.Edifice[f.Foot]),
                "both halves of it, on one record");
            Assert.That(loaded.Pawns.Reachable(TheColonist(loaded), f.Landing, TraverseMode.Hauler), Is.True,
                "and so did the way up it opens");
        }

        static int WoodOnTheGround(ColonyWorld colony)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == ItemIndex.Wood && items[i].Cell >= 0)
                    total += items[i].Stack;
            return total;
        }
    }
}
