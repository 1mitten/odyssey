#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Cover is crossed and never stood on (design 50 §5): a pawn sent there stops beside it, a pawn
    /// found resting on it is stepped off, nobody is left inside one as it goes up, a ring of it
    /// seals nobody in, and a colony living inside a ring does not keep coming to rest on it.
    /// </summary>
    public class StandingTests
    {
        static int Offset(int cell, int dx, int dz = 0)
        {
            CellRef c = Size.FromIndex(cell);
            return Size.Index(c.X + dx, c.Z + dz, c.Y);
        }

        static void Raise(ColonyWorld colony, int cell)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Sandbags, StuffHandle.Stone, 0), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
        }

        static TrappedPawnSystem Sweep(ColonyWorld colony)
        {
            foreach (IWorldSystem system in colony.World.Systems.WorldSystems)
                if (system is TrappedPawnSystem trapped) return trapped;
            throw new AssertionException("no sweep");
        }

        [Test]
        public void ACellWithCoverMayBeCrossedAndNotStoodOn()
        {
            var colony = Board();
            int cell = Near(colony, -15, 12);
            Assert.That(Standing.CanStandAt(colony.Pawns, cell), Is.True, "the control: open ground");
            Raise(colony, cell);
            Assert.That(Standing.CanStandAt(colony.Pawns, cell), Is.False);
            Assert.That(colony.Pawns.Nav.Grid.CanEnter(cell, TraverseMode.Colonist), Is.True);
            int beside = Standing.Resolve(colony.Pawns, cell, TraverseMode.Colonist);
            Assert.That(beside, Is.Not.EqualTo(cell));
            Assert.That(Standing.CanStandAt(colony.Pawns, beside), Is.True);
            Assert.That(Standing.Resolve(colony.Pawns, cell, TraverseMode.Colonist), Is.EqualTo(beside), "the same answer every time");
        }

        /// <summary>A drafted colonist ordered onto sandbags walks to them and stops beside them.</summary>
        [Test]
        public void AMoveOrderOntoSandbagsStopsBesideThem()
        {
            var colony = Board();
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Assert.That(Draft(colony, pawn), Is.EqualTo(IntentRejection.None));
            int bags = Offset(pawn.Cell, 5);
            Raise(colony, bags);
            Send(colony, new Intent(IntentKind.OrderMove, Size.FromIndex(bags), pawn.Id.Value));
            for (int t = 0; t < 600; t++) colony.World.Tick();
            Assert.That(pawn.Cell, Is.Not.EqualTo(bags));
            CellRef at = Size.FromIndex(pawn.Cell), b = Size.FromIndex(bags);
            Assert.That(System.Math.Max(System.Math.Abs(at.X - b.X), System.Math.Abs(at.Z - b.Z)), Is.LessThanOrEqualTo(1), "beside them");
            Assert.That(Sweep(colony).SteppedOffCover, Is.EqualTo(0), "the walk stopped short, not the sweep");
        }

        [Test]
        public void APawnLeftStandingOnCoverIsSteppedOffAfterAMoment()
        {
            var colony = Board();
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Assert.That(Draft(colony, pawn), Is.EqualTo(IntentRejection.None));
            int bags = Near(colony, -15, 12);
            Raise(colony, bags);
            Stand(colony, pawn, bags);
            pawn.JobStartTick = colony.World.CurrentTick;
            colony.World.Tick();
            Assert.That(pawn.Cell, Is.EqualTo(bags), "not the instant she stops: a new job may walk on");
            for (int t = 0; t < TrappedPawnSystem.CoverRestGraceTicks + 5; t++) colony.World.Tick();
            Assert.That(pawn.Cell, Is.Not.EqualTo(bags));
            Assert.That(Standing.CanStandAt(colony.Pawns, pawn.Cell), Is.True);
            Assert.That(Sweep(colony).SteppedOffCover, Is.EqualTo(1));
        }

        [Test]
        public void NobodyIsLeftInsideSandbagsAsTheyGoUp()
        {
            var colony = Board();
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            int cell = Near(colony, -15, 12);
            Stand(colony, pawn, cell);
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Sandbags, StuffHandle.Stone, 0), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
            Assert.That(pawn.Cell, Is.Not.EqualTo(cell));
        }

        /// <summary>A closed ring of sandbags round a colonist still lets her out: it is crossed.</summary>
        [Test]
        public void ARingOfSandbagsSealsNobodyIn()
        {
            var colony = Board();
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            int centre = Near(colony, -15, 12);
            Stand(colony, pawn, centre);
            for (int dz = -2; dz <= 2; dz++)
            for (int dx = -2; dx <= 2; dx++)
                if (System.Math.Abs(dx) == 2 || System.Math.Abs(dz) == 2) Raise(colony, Offset(centre, dx, dz));
            colony.World.Tick();
            Assert.That(colony.Pawns.CanTravel(pawn, Offset(centre, 6), TraverseMode.Colonist), Is.True);
        }

        /// <summary>
        /// A colony lives a day inside a ring of sandbags round its start. The ring is crossed
        /// constantly, and a pawn coming to rest on it is something the pickers should almost never
        /// allow: the sweep that catches the misses is counted and held low, and nobody is ever
        /// found resting on cover longer than its grace.
        /// </summary>
        [Test, Category("Long")]
        public void AColonyInsideARingOfSandbagsDoesNotLiveOnThem()
        {
            var colony = Board(colonists: 3);
            colony.World.Tick();
            CellRef start = colony.Start;
            int centre = colony.Pawns.Cells.NearestWalkableInColumn(start.X, start.Z, start.Y);
            int placed = 0;
            for (int dz = -6; dz <= 6; dz++)
            for (int dx = -6; dx <= 6; dx++)
            {
                if (System.Math.Abs(dx) != 6 && System.Math.Abs(dz) != 6) continue;
                int cell = Offset(centre, dx, dz);
                if (colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Sandbags, StuffHandle.Stone, 0) != IntentRejection.None) continue;
                if (colony.Construction.Raise(colony.Pawns, cell)) placed++;
            }
            Assert.That(placed, Is.GreaterThan(30), "most of the ring stands");

            int longest = 0, onCover = 0;
            var since = new System.Collections.Generic.Dictionary<int, int>();
            for (int t = 0; t < 60_000; t++)
            {
                colony.World.Tick();
                foreach (Pawn pawn in colony.Pawns.Pawns.All)
                {
                    if (!Standing.CanStandAt(colony.Pawns, pawn.Cell)) onCover++;
                    bool resting = !Standing.CanStandAt(colony.Pawns, pawn.Cell) && !pawn.HasPath && !pawn.PathPending;
                    if (!resting) { since.Remove(pawn.Id.Value); continue; }
                    if (!since.TryGetValue(pawn.Id.Value, out int from)) since[pawn.Id.Value] = from = t;
                    if (t - from > longest) longest = t - from;
                }
            }
            int stepped = Sweep(colony).SteppedOffCover;
            TestContext.WriteLine($"a day in a ring of {placed} sandbags: {onCover} pawn-ticks on cover, stepped off {stepped} times, longest rest on cover {longest} ticks");
            Assert.That(onCover, Is.GreaterThan(0), "the ring was crossed, or this proves nothing");
            Assert.That(longest, Is.LessThanOrEqualTo(TrappedPawnSystem.CoverRestGraceTicks + 1));
            Assert.That(stepped, Is.LessThanOrEqualTo(10), "the pickers should keep people off cover, not the sweep");
        }
    }
}
