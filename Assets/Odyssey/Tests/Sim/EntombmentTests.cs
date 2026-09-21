#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// <b>A colonist is never inside a wall.</b> The owner's report, 2026-09-21: "when colonists
    /// build a wall — sometimes they get stuck inside the wall itself. This should never and
    /// can't happen."
    ///
    /// <para>These are the three parts of the answer, in the order they act: the detour that keeps
    /// a passer-by out of the cell, the guard at the moment the building goes up, and the sweep
    /// that frees anybody already walled in — including in a save written before any of this
    /// existed. See <c>docs/design/28-nobody-in-a-wall.md</c>.</para>
    /// </summary>
    public class EntombmentTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 8);
        const uint Seed = 20260921;

        static ColonyWorld Fresh()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 0;
            return ColonyWorld.Build(Size, Seed, scenario);
        }

        /// <summary>A walkable cell near the start that will take a wall, with nobody in it.</summary>
        static int FreeCellNearTheStart(ColonyWorld colony, int skip = 0,
            byte building = BuildingHandle.Wall)
        {
            GridSize size = colony.Grid.Size;
            CellRef start = colony.Start;
            for (int radius = 1; radius < 8; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!size.Contains(x, z, start.Y)) continue;

                int cell = size.Index(x, z, start.Y);
                if (!colony.Grid.IsWalkable(cell)) continue;
                if (!colony.Construction.Allows(cell, building)) continue;
                if (PawnEviction.Occupant(colony.Pawns, cell) != null) continue;
                if (skip-- > 0) continue;
                return cell;
            }

            throw new AssertionException("no free cell near the start will take that order");
        }

        /// <summary>Put the colony's one pawn in a cell, standing still.</summary>
        static Pawn StandIn(ColonyWorld colony, int cell)
        {
            Pawn pawn = colony.Pawns.Pawns.All[0];
            pawn.ClearPath();
            pawn.Cell = cell;
            return pawn;
        }

        // ---- the guard --------------------------------------------------------------------

        /// <summary>
        /// The reproduction of the bug, and the assertion that it cannot happen: the colonist is
        /// standing exactly where the wall is going, and the wall goes up.
        /// </summary>
        [Test]
        public void AColonistStandingWhereAWallGoesIsMovedAsideRatherThanBuiltAround()
        {
            ColonyWorld colony = Fresh();
            int cell = FreeCellNearTheStart(colony);
            Pawn pawn = StandIn(colony, cell);

            Assume.That(colony.Construction.Place(colony.Grid.Size.FromIndex(cell),
                BuildingHandle.Wall, StuffHandle.Wood), Is.EqualTo(IntentRejection.None));
            colony.Construction.Raise(colony.Pawns, cell);

            Assert.That(colony.Grid.IsBlockedByEdifice(cell), Is.True, "the wall stands");
            Assert.That(pawn.Cell, Is.Not.EqualTo(cell), "and the colonist is not inside it");
            Assert.That(colony.Pawns.Nav.Grid.CanEnter(pawn.Cell, pawn.Mode), Is.True,
                "they were put somewhere they can stand, not into the next wall along");
        }

        /// <summary>
        /// Somebody merely crossing the cell is waited for rather than shoved: the raise is
        /// refused, the site keeps its work and its material, and the wall goes up a moment later.
        /// </summary>
        [Test]
        public void AWallWaitsForSomebodyWalkingThroughRatherThanShovingThem()
        {
            ColonyWorld colony = Fresh();
            int cell = FreeCellNearTheStart(colony);

            Pawn pawn = StandIn(colony, cell);
            int goal = FreeCellNearTheStart(colony, skip: 3);
            colony.Pawns.Paths.Enqueue(new PathRequest(pawn.Id.Value, pawn.Cell, goal, pawn.Mode));
            for (int i = 0; i < 10 && !pawn.HasPath; i++) colony.World.Tick();
            Assert.That(pawn.HasPath, Is.True, "the fixture has a colonist on the move");
            pawn.Cell = cell;

            Assume.That(colony.Construction.Place(colony.Grid.Size.FromIndex(cell),
                BuildingHandle.Wall, StuffHandle.Wood), Is.EqualTo(IntentRejection.None));
            int delivered = colony.Construction.Delivered(cell);

            Assert.That(colony.Construction.CanRaiseNow(colony.Pawns, cell), Is.False,
                "the driver is told to hold its last blow");
            colony.Construction.Raise(colony.Pawns, cell);

            Assert.That(colony.Grid.IsBlockedByEdifice(cell), Is.False, "no wall went up");
            Assert.That(colony.Construction.At(cell), Is.EqualTo(BuildingHandle.Wall),
                "and the order is still there, to be finished when they have gone");
            Assert.That(colony.Construction.Delivered(cell), Is.EqualTo(delivered),
                "with its material untouched");
            Assert.That(pawn.Cell, Is.EqualTo(cell), "and nobody was moved");
        }

        /// <summary>
        /// And the wall goes up by itself once they have gone. The success roll happened once, in
        /// the tick the last blow landed; <c>RaiseWhenClear</c> is what carries its result into
        /// the world however many ticks the passer-by takes to leave.
        /// </summary>
        [Test]
        public void TheWallGoesUpOnItsOwnOnceThePasserByHasGone()
        {
            ColonyWorld colony = Fresh();
            int cell = FreeCellNearTheStart(colony);

            Pawn pawn = StandIn(colony, cell);
            int goal = FreeCellNearTheStart(colony, skip: 3);
            colony.Pawns.Paths.Enqueue(new PathRequest(pawn.Id.Value, pawn.Cell, goal, pawn.Mode));
            for (int i = 0; i < 10 && !pawn.HasPath; i++) colony.World.Tick();
            Assert.That(pawn.HasPath, Is.True, "the fixture has a colonist on the move");
            pawn.Cell = cell;

            Assume.That(colony.Construction.Place(colony.Grid.Size.FromIndex(cell),
                BuildingHandle.Wall, StuffHandle.Wood), Is.EqualTo(IntentRejection.None));

            // Exactly what the build driver does on its last stroke.
            colony.Pawns.Defer(_ => colony.Construction.RaiseWhenClear(
                colony.Pawns, cell, BuildingHandle.Wall, (byte)QualityHandle.Normal));
            colony.World.Tick();
            Assert.That(colony.Grid.IsBlockedByEdifice(cell), Is.False, "it waited");

            // They arrive, and the retry lands the wall without anybody rolling for it again.
            for (int i = 0; i < 400 && !colony.Grid.IsBlockedByEdifice(cell); i++) colony.World.Tick();

            Assert.That(colony.Grid.IsBlockedByEdifice(cell), Is.True,
                "the wall the colonist finished goes up once the cell is clear");
            Assert.That(colony.Construction.At(cell), Is.EqualTo(BuildingHandle.None),
                "and the order is gone with it");
        }

        // ---- the safety net ---------------------------------------------------------------

        /// <summary>
        /// The case no guard can answer: a colonist already inside a wall, as every save written
        /// before 2026-09-21 may hold. One tick frees them.
        /// </summary>
        [Test]
        public void TheSweepFreesAColonistAlreadyInsideAWall()
        {
            ColonyWorld colony = Fresh();
            int cell = FreeCellNearTheStart(colony);

            // The wall first and the colonist into it second, which is the state a save carries
            // whatever put them there.
            Assume.That(colony.Construction.Place(colony.Grid.Size.FromIndex(cell),
                BuildingHandle.Wall, StuffHandle.Wood), Is.EqualTo(IntentRejection.None));
            colony.Construction.Raise(colony.Pawns, cell);
            Assume.That(colony.Grid.IsBlockedByEdifice(cell), Is.True);

            Pawn pawn = StandIn(colony, cell);
            colony.World.Tick();

            Assert.That(pawn.Cell, Is.Not.EqualTo(cell), "the colonist is out of the wall");
            Assert.That(colony.Pawns.Nav.Grid.CanEnter(pawn.Cell, pawn.Mode), Is.True,
                "and standing somewhere they could have walked to");
        }

        /// <summary>
        /// And it leaves alone every colonist who is somewhere legal, which is the whole colony on
        /// every ordinary tick. A sweep that moved anybody else would be a teleport bug of its own.
        /// </summary>
        [Test]
        public void TheSweepLeavesAColonistStandingSomewhereLegalAlone()
        {
            ColonyWorld colony = Fresh();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            int cell = FreeCellNearTheStart(colony);
            StandIn(colony, cell);

            colony.World.Tick();

            Assert.That(pawn.Cell, Is.EqualTo(cell),
                "a colonist standing on open ground is where they were");
        }

        // ---- the detour -------------------------------------------------------------------

        /// <summary>
        /// A cell with a wall ordered in it costs more to walk into than the cell beside it, so a
        /// colonist with any other route takes the other route.
        /// </summary>
        [Test]
        public void AWallSiteIsDearerToWalkIntoThanTheGroundBesideIt()
        {
            ColonyWorld colony = Fresh();
            int cell = FreeCellNearTheStart(colony);
            int beside = FreeCellNearTheStart(colony, skip: 1);

            int before = colony.Pawns.Nav.Grid.EnterCost(cell, TraverseMode.Colonist);
            Assume.That(colony.Construction.Place(colony.Grid.Size.FromIndex(cell),
                BuildingHandle.Wall, StuffHandle.Wood), Is.EqualTo(IntentRejection.None));

            int after = colony.Pawns.Nav.Grid.EnterCost(cell, TraverseMode.Colonist);
            Assert.That(after - before, Is.EqualTo(MoveCost.SiteDetour));
            Assert.That(after, Is.GreaterThan(colony.Pawns.Nav.Grid.EnterCost(beside, TraverseMode.Colonist)));

            // And the cell is still crossable: a site is a deterrent, never a wall before its time.
            Assert.That(colony.Pawns.Nav.Grid.CanEnter(cell, TraverseMode.Colonist), Is.True);
        }

        /// <summary>
        /// Cancelling the order takes the detour off again. A flag left behind would quietly make
        /// a patch of ground dearer for ever, and nothing on the screen would say why.
        /// </summary>
        [Test]
        public void TheDetourGoesWhenTheOrderDoes()
        {
            ColonyWorld colony = Fresh();
            int cell = FreeCellNearTheStart(colony);
            int plain = colony.Pawns.Nav.Grid.EnterCost(cell, TraverseMode.Colonist);

            colony.Construction.Place(colony.Grid.Size.FromIndex(cell),
                BuildingHandle.Wall, StuffHandle.Wood);
            Assume.That(colony.Pawns.Nav.Grid.EnterCost(cell, TraverseMode.Colonist), Is.GreaterThan(plain));

            colony.Construction.Cancel(colony.Grid.Size.FromIndex(cell));
            Assert.That(colony.Pawns.Nav.Grid.EnterCost(cell, TraverseMode.Colonist), Is.EqualTo(plain));
        }

        /// <summary>
        /// <b>One price, two readers.</b> The mover prices a step with <c>NavGrid.EnterCost</c>
        /// and the region graph prices the same step with <c>NavGraph.StepCost</c>. They are
        /// mirrors of one another, and a disagreement makes the abstract search plan a route the
        /// walk then pays a different price for — the failure <c>HopPriceHasOneOwnerTests</c>
        /// exists to catch for the hop, silent in exactly the same way.
        /// </summary>
        [Test]
        public void TheSiteDetourIsPricedTheSameByBothReaders()
        {
            ColonyWorld colony = Fresh();
            int cell = FreeCellNearTheStart(colony);
            NavGrid grid = colony.Pawns.Nav.Grid;

            Assert.That(grid.EnterCost(cell, TraverseMode.Colonist),
                Is.EqualTo(colony.Pawns.Nav.StepCost(cell)), "before the order");
            Assert.That(grid.EnterCost(cell, TraverseMode.Colonist, diagonal: true),
                Is.EqualTo(colony.Pawns.Nav.StepCost(cell, diagonal: true)), "and diagonally");

            colony.Construction.Place(colony.Grid.Size.FromIndex(cell),
                BuildingHandle.Wall, StuffHandle.Wood);

            Assert.That(grid.EnterCost(cell, TraverseMode.Colonist),
                Is.EqualTo(colony.Pawns.Nav.StepCost(cell)), "and with the order on it");
            Assert.That(grid.EnterCost(cell, TraverseMode.Colonist, diagonal: true),
                Is.EqualTo(colony.Pawns.Nav.StepCost(cell, diagonal: true)),
                "including the diagonal rounding, which is written out twice");
        }

        /// <summary>
        /// A bed is ordered on open ground and blocks nobody — you walk over it to get into it —
        /// so it carries no detour. The distinction is what keeps the deterrent to the things it
        /// is about: a colony should not pay to walk across its own furniture orders.
        /// </summary>
        [Test]
        public void AnOrderForSomethingThatBlocksNobodyCostsNothingExtraToWalkOver()
        {
            ColonyWorld colony = Fresh();
            int cell = FreeCellNearTheStart(colony, building: BuildingHandle.Bed);
            int plain = colony.Pawns.Nav.Grid.EnterCost(cell, TraverseMode.Colonist);

            Assert.That(colony.Construction.Place(colony.Grid.Size.FromIndex(cell),
                BuildingHandle.Bed, StuffHandle.Wood), Is.EqualTo(IntentRejection.None));

            Assert.That(colony.Pawns.Nav.Grid.EnterCost(cell, TraverseMode.Colonist),
                Is.EqualTo(plain));
        }
    }
}
