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
        /// <b>The whole unit in one test: build a roof, build a ladder, stand on the roof.</b>
        ///
        /// <para>The control comes first and is not optional — the roof must be measured
        /// unreachable <em>before</em> the ladder goes in, or a board where everything happens to
        /// be reachable would pass this without the feature existing at all.</para>
        /// </summary>
        [Test]
        public void ALadderMakesARoofSomewhereAColonistCanGo()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);

            int ground = GroundNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            // A wall beside it, and a slab over the ground cell: a one-cell roof with open air
            // under it, which is the shape a ladder is for.
            int wall = ground + 1;
            Assume.That(colony.Construction.Allows(wall, BuildingHandle.Wall), Is.True);
            RaiseNow(colony, wall, BuildingHandle.Wall);
            colony.World.Tick();

            int roof = ground + Size.LayerStride;
            RaiseNow(colony, roof, BuildingHandle.Floor);
            colony.World.Tick();

            Assume.That(colony.Grid.IsWalkable(roof), Is.True, "the roof is a floor");
            Assert.That(colony.Pawns.Reachable(pawn, roof, TraverseMode.Colonist), Is.False,
                "the control: before the ladder there is no way up, which is what U43 exists for");

            // The ladder, in the cell under the roof.
            RaiseNow(colony, ground, BuildingHandle.Ladder);
            colony.World.Tick();

            Assert.That(colony.Grid.IsWalkable(ground), Is.True,
                "a ladder must be a cell you can stand in, or it is a decoration");
            Assert.That(colony.Pawns.Reachable(pawn, roof, TraverseMode.Colonist), Is.True,
                "and now a colonist can get onto the roof");
        }

        /// <summary>
        /// The order the player builds in must not matter. A ladder put up before there is anything
        /// above it registers nothing and is simply a thing standing there; the floor arriving over
        /// it is what completes the pair.
        /// </summary>
        [Test]
        public void ItDoesNotMatterWhetherTheLadderOrTheFloorComesFirst()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);

            int ground = GroundNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            int wall = ground + 1;
            RaiseNow(colony, wall, BuildingHandle.Wall);
            colony.World.Tick();

            // Ladder first, into open air.
            RaiseNow(colony, ground, BuildingHandle.Ladder);
            colony.World.Tick();

            int roof = ground + Size.LayerStride;
            Assert.That(colony.Pawns.Reachable(pawn, roof, TraverseMode.Colonist), Is.False,
                "a ladder to nowhere opens nothing");

            // Then the floor over it.
            RaiseNow(colony, roof, BuildingHandle.Floor);
            colony.World.Tick();

            Assert.That(colony.Pawns.Reachable(pawn, roof, TraverseMode.Colonist), Is.True,
                "the floor arriving is what completes the pair");
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

            int ground = GroundNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            RaiseNow(colony, ground + 1, BuildingHandle.Wall);
            colony.World.Tick();
            int roof = ground + Size.LayerStride;
            RaiseNow(colony, roof, BuildingHandle.Floor);
            RaiseNow(colony, ground, BuildingHandle.Ladder);
            colony.World.Tick();
            Assume.That(colony.Pawns.Reachable(pawn, roof, TraverseMode.Colonist), Is.True);

            Assert.That(colony.Construction.Demolish(colony.Pawns, ground, out _), Is.True);
            colony.World.Tick();

            Assert.That(colony.Pawns.Reachable(pawn, roof, TraverseMode.Colonist), Is.False,
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

            int ground = GroundNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            RaiseNow(colony, ground + 1, BuildingHandle.Wall);
            colony.World.Tick();
            int roof = ground + Size.LayerStride;
            RaiseNow(colony, roof, BuildingHandle.Floor);
            RaiseNow(colony, ground, BuildingHandle.Ladder);
            colony.World.Tick();

            Assume.That(colony.Pawns.Reachable(pawn, roof, TraverseMode.Colonist), Is.True,
                "the way up is open to a colonist");
            Assert.That(colony.Pawns.Reachable(pawn, roof, TraverseMode.Hauler), Is.False,
                "and closed to a hauler, so a ladder alone cannot supply an upper storey");
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
            int ground = GroundNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            RaiseNow(colony, ground + 1, BuildingHandle.Wall);
            colony.World.Tick();
            int roof = ground + Size.LayerStride;
            RaiseNow(colony, roof, BuildingHandle.Floor);
            RaiseNow(colony, ground, BuildingHandle.Ladder);
            colony.World.Tick();
            Assume.That(colony.Pawns.Reachable(TheColonist(colony), roof, TraverseMode.Colonist), Is.True);

            byte[] saved = colony.Save();

            ColonyWorld loaded = Board();
            loaded.Load(saved);

            Assert.That(loaded.Grid.Edifice[ground], Is.GreaterThanOrEqualTo(0), "the ladder came back");
            Assert.That(loaded.Pawns.Reachable(TheColonist(loaded), roof, TraverseMode.Colonist), Is.True,
                "and so did the way up it opens");
        }
    }
}
