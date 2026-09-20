#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// RF1: roofing a building, and the pillar that makes a hall roofable.
    ///
    /// <para><b>A roof is a floor is a slab</b> and has been since U29, so nothing here is about a
    /// new pipeline. These are about the two things that stopped roofing being something a player
    /// could do: an order named inside the room you are standing in, and a span wider than a hut.
    /// <c>docs/design/27-roofs.md</c>.</para>
    ///
    /// <para><b>The numbers in these tests were measured, not derived.</b> A first pass reasoned
    /// that support decaying one per cell from a wall put the limit at a six-cell interior; the
    /// probe said holes start at six and reach twenty-five by ten, because a cell ordered early in
    /// a sweep supports the cells ordered after it and the arithmetic does not see that. Where a
    /// count appears below it came from a run.</para>
    /// </summary>
    public class RoofsTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 16);

        static ColonyWorld Board()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;

            // **No bed, and that is load-bearing.** AHall is centred on `colony.Start`, and since
            // a starting bed became a real edifice (main, 2026-09-20) the scenario's bed stands
            // two cells out from the start — inside the wall line of a six-wide hall, where it
            // refuses the wall order for `Edifice[index] >= 0`. Nothing here sleeps; the bed was
            // only ever scenario boilerplate.
            scenario.beds = 0;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, 1u, scenario, barren: true, wooded: false);
        }

        static void RaiseNow(ColonyWorld colony, int cell, int building, int stuff = StuffHandle.Wood)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), building, stuff),
                Is.EqualTo(IntentRejection.None), $"the order for {cell} was refused");
            colony.Construction.Raise(colony.Pawns, cell);
        }

        static int Above(int cell) => cell + Size.LayerStride;

        /// <summary>A hollow square of walls, `wide` cells on a side, on the start layer.</summary>
        static void AHall(ColonyWorld colony, int wide, out int x0, out int z0, out int y)
        {
            CellRef start = colony.Start;
            y = start.Y;
            x0 = start.X - wide / 2;
            z0 = start.Z - wide / 2;

            for (int dz = 0; dz < wide; dz++)
            for (int dx = 0; dx < wide; dx++)
            {
                if (dx != 0 && dz != 0 && dx != wide - 1 && dz != wide - 1) continue;
                int cell = Size.Index(x0 + dx, z0 + dz, y);
                RaiseNow(colony, cell, BuildingHandle.Wall);
                colony.World.Tick();
            }
        }

        /// <summary>Sweep the roof on as a drag does, cell by cell, and count what refused.</summary>
        static int HolesRoofing(ColonyWorld colony, int wide, int x0, int z0, int y)
        {
            int refused = 0;
            for (int dz = 0; dz < wide; dz++)
            for (int dx = 0; dx < wide; dx++)
            {
                var at = new CellRef(x0 + dx, z0 + dz, y + 1);
                if (colony.Construction.Place(at, BuildingHandle.Floor, StuffHandle.Wood)
                    != IntentRejection.None) refused++;
            }
            return refused;
        }

        /// <summary>Roof a room, so its roof becomes the storey above's floor.</summary>
        static void RoofIt(ColonyWorld colony, int wide, int x0, int z0, int y)
        {
            for (int dz = 0; dz < wide; dz++)
            for (int dx = 0; dx < wide; dx++)
            {
                var at = new CellRef(x0 + dx, z0 + dz, y + 1);
                Assume.That(colony.Construction.Place(at, BuildingHandle.Floor, StuffHandle.Wood),
                    Is.EqualTo(IntentRejection.None));
                colony.Construction.Raise(colony.Pawns, Size.Index(at));
                colony.World.Tick();
            }
        }

        // ---- where a slab order lands (RF1a) ---------------------------------------------------

        /// <summary>
        /// <b>The fault, and the fix.</b> Stand on an upper storey and point at the floor under
        /// your feet: the picker answers with that floor's own cell, because a pointer names a
        /// surface. Before RF1 the order was refused for having a floor already — silently, which
        /// is the whole complaint — so roofing the storey you were standing on meant raising the
        /// depth rail first.
        /// </summary>
        [Test]
        public void TheFloorOfAnUpperStoreyIsRoofedByPointingAtIt()
        {
            ColonyWorld colony = Board();
            AHall(colony, 6, out int x0, out int z0, out int y);
            RoofIt(colony, 6, x0, z0, y);

            var inside = new CellRef(x0 + 2, z0 + 2, y + 1);
            Assume.That(colony.Grid.Floor[Size.Index(inside)], Is.EqualTo(CoreContent.SlabBuilt),
                "the cell a click lands on is the slab itself");

            // **The lift is conditional, and deliberately so.** With no second storey there is
            // nothing to hold a roof up, the target refuses the order, and StandingOver leaves the
            // cell where it was - so the refusal is still reported where the player clicked. That
            // guard is shared with the clause U29 wrote and is half of why the sky-slab case below
            // stays safe.
            Assert.That(
                Size.FromIndex(colony.Construction.WhereItWouldLand(
                    Size.Index(inside), BuildingHandle.Floor)).Y,
                Is.EqualTo(y + 1),
                "with nothing to hold a roof up the order does not move off the cell clicked");

            // Walls for the second storey, so the roof it is asking for can actually stand.
            for (int dz = 0; dz < 6; dz++)
            for (int dx = 0; dx < 6; dx++)
            {
                if (dx != 0 && dz != 0 && dx != 5 && dz != 5) continue;
                RaiseNow(colony, Size.Index(x0 + dx, z0 + dz, y + 1), BuildingHandle.Wall);
                colony.World.Tick();
            }

            Assert.That(
                Size.FromIndex(colony.Construction.WhereItWouldLand(
                    Size.Index(inside), BuildingHandle.Floor)).Y,
                Is.EqualTo(y + 2),
                "now the order means the boundary above the slab, not the slab's own cell");

            Assert.That(colony.Construction.Place(inside, BuildingHandle.Floor, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None),
                "and it is taken, from the storey the player is standing on");
            Assert.That(colony.Construction.SiteAt(new CellRef(x0 + 2, z0 + 2, y + 2)),
                Is.Not.EqualTo(BuildingHandle.None), "the site is one layer up");
        }

        /// <summary>
        /// <b>The control that shaped the rule, and it is the important one.</b> The clause asks
        /// <c>Floor[index]</c> and not <c>HasFloor</c>, and the difference is a slab in the sky.
        ///
        /// <para>Measured before the rule was written: on open meadow the air cell over the ground
        /// answers <c>HasFloor</c> true, and one layer above <em>that</em> the support rule returns
        /// 3 whenever a wall stands beside it — because the slab over the wall's head is grounded.
        /// So the looser test would have turned a click on grass into a slab one cell above the
        /// wall top, accepted and built. It must stay refused.</para>
        /// </summary>
        [Test]
        public void BareGrassStillRefusesAndNeverPutsASlabInTheSky()
        {
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int wall = Size.Index(start.X + 3, start.Z + 3, start.Y);
            RaiseNow(colony, wall, BuildingHandle.Wall);
            colony.World.Tick();

            var beside = new CellRef(start.X + 4, start.Z + 3, start.Y);
            int here = Size.Index(beside);

            Assume.That(colony.Grid.HasFloor(here), Is.True, "this is the cell HasFloor would admit");
            Assume.That(colony.Pawns.Support!.SupportIfSlabAt(Above(here)), Is.GreaterThan(0),
                "and the support rule would have accepted the sky cell above it");

            Assert.That(colony.Construction.WhereItWouldLand(here, BuildingHandle.Floor), Is.EqualTo(here),
                "the lift does not move a cell that holds no slab");
            Assert.That(colony.Construction.Place(beside, BuildingHandle.Floor, StuffHandle.Wood),
                Is.Not.EqualTo(IntentRejection.None),
                "a click on grass beside a wall is still refused, and refused where it was clicked");
            Assert.That(colony.Grid.Floor[Above(here)], Is.EqualTo(CoreContent.SlabNone));
        }

        /// <summary>
        /// One step, always. Ordering a slab where a roof already stands lifts onto that roof and
        /// is refused there — it cannot walk up a column putting storeys on.
        /// </summary>
        [Test]
        public void AnAlreadyRoofedCellRefusesRatherThanRoofingTheStoreyAbove()
        {
            ColonyWorld colony = Board();
            AHall(colony, 6, out int x0, out int z0, out int y);
            RoofIt(colony, 6, x0, z0, y);

            // No second storey walls, so the boundary above the roof has nothing to hold it.
            var inside = new CellRef(x0 + 2, z0 + 2, y + 1);
            Assert.That(colony.Construction.Place(inside, BuildingHandle.Floor, StuffHandle.Wood),
                Is.Not.EqualTo(IntentRejection.None));
            Assert.That(colony.Grid.Floor[Size.Index(x0 + 2, z0 + 2, y + 2)],
                Is.EqualTo(CoreContent.SlabNone), "nothing was ordered two layers up");
        }

        /// <summary>
        /// A wall top still takes the slab on top of it — U29's row of the matrix, unchanged, and
        /// the negative control that stops the new clause being mistaken for the old one.
        /// </summary>
        [Test]
        public void AWallTopStillTakesTheSlabOnTopOfIt()
        {
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int wall = Size.Index(start.X + 3, start.Z + 3, start.Y);
            RaiseNow(colony, wall, BuildingHandle.Wall);
            colony.World.Tick();

            Assert.That(colony.Construction.WhereItWouldLand(wall, BuildingHandle.Floor),
                Is.EqualTo(Above(wall)));
        }

        /// <summary>
        /// Paving is a covering, so it goes through <c>StandingOn</c> and never meets this rule at
        /// all. Ordering it on a floored cell still means that cell, and is still refused for
        /// having something laid already (18-paving.md).
        /// </summary>
        [Test]
        public void PavingIsUntouchedByTheSlabLift()
        {
            ColonyWorld colony = Board();
            AHall(colony, 6, out int x0, out int z0, out int y);
            RoofIt(colony, 6, x0, z0, y);

            var on = new CellRef(x0 + 2, z0 + 2, y + 1);
            int here = Size.Index(on);
            Assume.That(colony.Grid.Floor[here], Is.EqualTo(CoreContent.SlabBuilt));

            Assert.That(colony.Construction.WhereItWouldLand(here, BuildingHandle.DeckPlate),
                Is.EqualTo(here), "a covering names the cell it was clicked on");
        }

        // ---- the pillar ------------------------------------------------------------------------

        /// <summary>
        /// <b>The solver has trusted a pillar since M1 and nothing could build one.</b>
        /// <c>SupportSolver.IsGrounded</c> ends at <c>Edifice[below] >= 0</c>, so the slab over a
        /// pillar is grounded exactly as the slab over a wall is. This is the claim that says the
        /// def is all the unit needed.
        /// </summary>
        [Test]
        public void APillarGroundsTheSlabAboveItAsFullyAsAWallDoes()
        {
            ColonyWorld colony = Board();
            CellRef start = colony.Start;

            int pillar = Size.Index(start.X + 3, start.Z + 3, start.Y);
            int wall = Size.Index(start.X + 3, start.Z + 6, start.Y);

            RaiseNow(colony, pillar, BuildingHandle.Pillar);
            RaiseNow(colony, wall, BuildingHandle.Wall);
            colony.World.Tick();

            SupportSolver support = colony.Pawns.Support!;
            Assert.That(support.SupportIfSlabAt(Above(pillar)), Is.EqualTo(support.MaxSupport),
                "a slab over a pillar is grounded at S_max");
            Assert.That(support.SupportIfSlabAt(Above(pillar)),
                Is.EqualTo(support.SupportIfSlabAt(Above(wall))),
                "and it is grounded no differently from a slab over a wall");
        }

        /// <summary>
        /// <b>The measurement that put the pillar in RF1.</b> A hall wide enough to be worth
        /// calling one cannot be roofed: support decays one per cell from a wall, the middle runs
        /// out, and the player gets a roof with holes in it and no explanation.
        ///
        /// <para>Counts rather than a bare "it fails", because the shape of the failure is the
        /// argument: the holes go up faster than the room does.</para>
        /// </summary>
        [Test]
        public void AHallIsTooWideToRoofAndTheHolesGrowFasterThanTheRoom()
        {
            var holes = new Dictionary<int, int>();
            foreach (int wide in new[] { 6, 8, 10, 12 })
            {
                ColonyWorld colony = Board();
                AHall(colony, wide, out int x0, out int z0, out int y);
                holes[wide] = HolesRoofing(colony, wide, x0, z0, y);
            }

            Assert.That(holes[6], Is.Zero, "a hut roofs completely");
            Assert.That(holes[8], Is.GreaterThan(0), "a hall does not");
            Assert.That(holes[10], Is.GreaterThan(holes[8]));
            Assert.That(holes[12], Is.GreaterThan(holes[10]),
                $"the holes grow faster than the room: {holes[6]}, {holes[8]}, "
                + $"{holes[10]}, {holes[12]} for 6, 8, 10 and 12 cells on a side");
        }

        /// <summary>
        /// And the pillar is the answer: the same hall, one column in the middle, roofs whole.
        /// This is the negative control's other half — without the pillar the assertion below
        /// fails, which is what makes the first line of the test worth having.
        /// </summary>
        [Test]
        public void AHallTooWideToRoofIsRoofedWholeOnceAPillarStandsInIt()
        {
            ColonyWorld bare = Board();
            AHall(bare, 10, out int bx, out int bz, out int by);
            int without = HolesRoofing(bare, 10, bx, bz, by);
            Assume.That(without, Is.GreaterThan(0), "the hall must be unroofable to begin with");

            ColonyWorld colony = Board();
            AHall(colony, 10, out int x0, out int z0, out int y);
            RaiseNow(colony, Size.Index(x0 + 5, z0 + 5, y), BuildingHandle.Pillar);
            colony.World.Tick();

            Assert.That(HolesRoofing(colony, 10, x0, z0, y), Is.Zero,
                $"one pillar closes all {without} holes");
        }

        /// <summary>
        /// Take the column out and what it was carrying comes down. The pillar is load-bearing in
        /// the simulation, not a decoration that happens to satisfy the order rule.
        /// </summary>
        [Test]
        public void TakingThePillarOutBringsDownWhatItWasHolding()
        {
            ColonyWorld colony = Board();
            AHall(colony, 10, out int x0, out int z0, out int y);

            int pillar = Size.Index(x0 + 5, z0 + 5, y);
            RaiseNow(colony, pillar, BuildingHandle.Pillar);
            colony.World.Tick();

            for (int dz = 0; dz < 10; dz++)
            for (int dx = 0; dx < 10; dx++)
            {
                var at = new CellRef(x0 + dx, z0 + dz, y + 1);
                colony.Construction.Place(at, BuildingHandle.Floor, StuffHandle.Wood);
                colony.Construction.Raise(colony.Pawns, Size.Index(at));
                colony.World.Tick();
            }

            int middle = Above(pillar);
            Assume.That(colony.Grid.Floor[middle], Is.EqualTo(CoreContent.SlabBuilt));

            Assert.That(colony.Construction.Demolish(colony.Pawns, pillar, out _), Is.True);
            colony.World.Tick();

            Assert.That(colony.Grid.Floor[middle], Is.EqualTo(CoreContent.SlabNone),
                "the slab the pillar was carrying has come down");
        }

        /// <summary>
        /// A pillar fills its cell, which is the decision that makes span cost floor space
        /// (27-roofs.md §5). Nothing else goes in there and nobody walks through it.
        /// </summary>
        [Test]
        public void APillarFillsItsCellSoNothingElseGoesThere()
        {
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int cell = Size.Index(start.X + 3, start.Z + 3, start.Y);

            RaiseNow(colony, cell, BuildingHandle.Pillar);
            colony.World.Tick();

            Assert.That(colony.Grid.IsWalkable(cell), Is.False, "you cannot walk through a column");
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Wall, StuffHandle.Wood),
                Is.Not.EqualTo(IntentRejection.None), "and nothing else stands in its cell");
        }

        /// <summary>
        /// It is state: it moves the hash and it comes back off a save. The handle is appended to
        /// <c>BuildingOrder</c>, so a save written before RF1 still reads.
        /// </summary>
        [Test]
        public void APillarIsHashedAndSurvivesASaveAndReload()
        {
            ColonyWorld colony = Board();
            CellRef start = colony.Start;
            int cell = Size.Index(start.X + 3, start.Z + 3, start.Y);

            ulong bare = colony.World.ComputeStateHash().Value;
            RaiseNow(colony, cell, BuildingHandle.Pillar);
            colony.World.Tick();

            ulong built = colony.World.ComputeStateHash().Value;
            Assert.That(built, Is.Not.EqualTo(bare), "a pillar the colony built is state");

            byte[] saved = colony.Save();
            ColonyWorld reloaded = Board();
            reloaded.Load(saved);

            Assert.That(reloaded.Grid.Edifice[cell], Is.GreaterThanOrEqualTo(0));
            Assert.That(reloaded.World.ComputeStateHash().Value, Is.EqualTo(built));
        }
    }
}
