#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// U44: the way up that carries something.
    ///
    /// <para><b>The unit's whole argument is a mode bit.</b> A ladder's connector excludes
    /// <see cref="TraverseMode.Hauler"/> and a stair's carries <c>AllMask</c>, so a stair is the
    /// first thing a colony can build that lets material reach an upper storey — which is what
    /// makes a second storey somewhere to build rather than merely somewhere to stand.</para>
    ///
    /// <para>Written in <c>LadderTests</c>'s shape on purpose: <b>reachability rather than
    /// edifice-existence, with the control measured first</b>, because a board where everything
    /// happens to be reachable would pass a badly written version of every test here without the
    /// feature existing at all. Design: <c>docs/design/28-stairs.md</c>.</para>
    /// </summary>
    public class StairTests
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

        static void RaiseNow(ColonyWorld colony, int cell, int building, int facing = 0,
            int stuff = StuffHandle.Wood)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), building, stuff, facing),
                Is.EqualTo(IntentRejection.None), $"the order for {cell} was refused");
            colony.Construction.Raise(colony.Pawns, cell);
        }

        static int Above(int cell) => cell + Size.LayerStride;

        /// <summary>A walkable cell near the start: open air with ground beneath it.</summary>
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
        /// A storey to climb to, and the two cells a stair will stand in.
        ///
        /// <para>The shape is the ladder's with one more cell: a wall with a slab on top of it as
        /// the landing, and the stair's two cells in the column beside it, nothing above them. The
        /// stair's halves are <b>side by side on one layer</b> — the upper one is drawn 1.5 m up
        /// inside its own cell — so this is a footprint like the bed's, not a vertical one.</para>
        /// </summary>
        static void AStoreyWithNothingLeadingToIt(
            ColonyWorld colony, out int head, out int second, out int landing)
        {
            head = GroundNear(colony, 3);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));

            // Facing 1 is +X, so the far half is the next cell along X.
            second = head + 1;
            int wall = head + 2;

            Assume.That(colony.Construction.Allows(wall, BuildingHandle.Wall), Is.True);
            RaiseNow(colony, wall, BuildingHandle.Wall);
            colony.World.Tick();

            landing = Above(wall);
            RaiseNow(colony, landing, BuildingHandle.Floor);
            colony.World.Tick();
        }

        // ---- the thesis -------------------------------------------------------------------------

        /// <summary>
        /// <b>The whole unit in one test.</b> A ladder opens the storey to a colonist and not to a
        /// hauler; a stair opens it to both. The ladder is measured in the same test so the claim
        /// cannot quietly become "a stair works and nothing was ever wrong".
        /// </summary>
        [Test]
        public void AStairCarriesAHaulerWhereALadderCannot()
        {
            // The ladder goes in the stair's FAR cell, not its near one: a landing has to be
            // orthogonally beside the top of the shaft, and only the far half is beside the wall.
            ColonyWorld ladderBoard = Board();
            AStoreyWithNothingLeadingToIt(ladderBoard, out _, out int lShaft, out int lLanding);
            RaiseNow(ladderBoard, lShaft, BuildingHandle.Ladder);
            ladderBoard.World.Tick();

            Pawn onLadder = TheColonist(ladderBoard);
            Assume.That(ladderBoard.Pawns.Reachable(onLadder, lLanding, TraverseMode.Colonist), Is.True,
                "the control: a ladder does open the storey to a colonist");
            Assert.That(ladderBoard.Pawns.Reachable(onLadder, lLanding, TraverseMode.Hauler), Is.False,
                "and closes it to a hauler, which is the whole reason this unit exists");

            ColonyWorld colony = Board();
            AStoreyWithNothingLeadingToIt(colony, out int head, out _, out int landing);
            Pawn pawn = TheColonist(colony);

            Assume.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Colonist), Is.False,
                "the control: nothing reaches the storey before the stair goes in");

            RaiseNow(colony, head, BuildingHandle.Stair, facing: 1);
            colony.World.Tick();

            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Colonist), Is.True);
            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Hauler), Is.True,
                "a hauler climbs a stair, so material can reach an upper storey at last");
        }

        /// <summary>
        /// And an animal too: a stair carries <c>AllMask</c>, so every mode uses it. Stated as its
        /// own claim because it is the half of the mask a hauler test cannot see.
        /// </summary>
        [Test]
        public void EveryModeMayUseAStair()
        {
            ColonyWorld colony = Board();
            AStoreyWithNothingLeadingToIt(colony, out int head, out _, out int landing);
            RaiseNow(colony, head, BuildingHandle.Stair, facing: 1);
            colony.World.Tick();

            Pawn pawn = TheColonist(colony);
            foreach (TraverseMode mode in new[]
                     {
                         TraverseMode.Colonist, TraverseMode.Hauler,
                         TraverseMode.Animal, TraverseMode.IgnoreDoors,
                     })
                Assert.That(colony.Pawns.Reachable(pawn, landing, mode), Is.True, $"{mode} may climb a stair");
        }

        // ---- two cells, two records -------------------------------------------------------------

        /// <summary>
        /// A stair finishes as <b>two</b> edifice values, which is unique to it and is exactly what
        /// worldgen stamps — so the mesher's partner scan, the labels and the render mirror all keep
        /// working untouched (28-stairs.md §4).
        /// </summary>
        [Test]
        public void AStairIsALowerHalfAndAnUpperHalfInTwoCells()
        {
            ColonyWorld colony = Board();
            AStoreyWithNothingLeadingToIt(colony, out int head, out int second, out _);
            RaiseNow(colony, head, BuildingHandle.Stair, facing: 1);
            colony.World.Tick();

            Assert.That(colony.Grid.Edifice[head], Is.GreaterThanOrEqualTo(0));
            Assert.That(colony.Grid.Edifice[second], Is.GreaterThanOrEqualTo(0));
            Assert.That(colony.Grid.Edifice[second], Is.Not.EqualTo(colony.Grid.Edifice[head]),
                "two records, not one pointed at twice — a bed's shape would draw both halves alike");
        }

        /// <summary>
        /// Taken apart by naming <b>either</b> half, and neither half is left behind. The far half
        /// stores the opposite facing precisely so this is symmetric; a record left standing would
        /// hand back a connector for a stair that is not there on the next load.
        /// </summary>
        [Test]
        public void EitherHalfTakesTheWholeStairApart([Values(true, false)] bool byTheFarHalf)
        {
            ColonyWorld colony = Board();
            AStoreyWithNothingLeadingToIt(colony, out int head, out int second, out int landing);
            RaiseNow(colony, head, BuildingHandle.Stair, facing: 1);
            colony.World.Tick();

            Pawn pawn = TheColonist(colony);
            Assume.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Hauler), Is.True);

            Assert.That(colony.Construction.Demolish(colony.Pawns, byTheFarHalf ? second : head, out _),
                Is.True);
            colony.World.Tick();

            Assert.That(colony.Grid.Edifice[head], Is.LessThan(0), "the near half is gone");
            Assert.That(colony.Grid.Edifice[second], Is.LessThan(0), "and so is the far one");
            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Hauler), Is.False,
                "the portal must not outlive the stair");
        }

        /// <summary>
        /// <b>And a player takes one apart by marking it, not by calling Demolish.</b>
        ///
        /// <para>The test above proves the <em>rule</em> — either half names the whole stair — by
        /// calling <c>Demolish</c> straight. That is one step short of what the owner asked about
        /// on 2026-09-21: a deconstruct order is a designation, a work giver has to see it, a
        /// colonist has to walk to it and finish it, and the mark on the <em>other</em> half has to
        /// go when the stair does. A stale designation over an empty cell is a job that can never
        /// be filled and is handed out for ever, which is exactly what an unreachable crop cost
        /// PR #119 — 159 failed jobs in 2,000 ticks.</para>
        ///
        /// <para>Marked by each half in turn, because the far half is a record of its own and the
        /// two are not symmetric in the code even though they are to the player.</para>
        /// </summary>
        [Test]
        public void AMarkedStairIsPulledDownWholeByAColonist([Values(true, false)] bool byTheFarHalf)
        {
            ColonyWorld colony = Board();
            AStoreyWithNothingLeadingToIt(colony, out int head, out int second, out int landing);
            RaiseNow(colony, head, BuildingHandle.Stair, facing: 1);
            colony.World.Tick();

            int marked = byTheFarHalf ? second : head;
            Assume.That(colony.Designations.CanDeconstruct(marked), Is.True,
                "the half that was clicked has to be something the deconstruct tool will take");
            Assert.That(
                colony.Designations.Designate(Size.FromIndex(marked), DesignationKind.Deconstruct),
                Is.EqualTo(IntentRejection.None), "the order was refused");

            for (int tick = 0; tick < 20_000 && colony.Grid.Edifice[head] >= 0; tick++)
                colony.World.Tick();

            // Assert rather than Assume, for the reason DeconstructTests records against its own
            // stone wall: a precondition that fails is reported as green.
            Assert.That(colony.Grid.Edifice[head], Is.LessThan(0), "nobody pulled the stair down");
            Assert.That(colony.Grid.Edifice[second], Is.LessThan(0),
                "half a stair was left standing, which is a portal whose far end is a hole");

            Assert.That(colony.Designations.At(head), Is.EqualTo(DesignationKind.None),
                "a mark outlived the thing it was on");
            Assert.That(colony.Designations.At(second), Is.EqualTo(DesignationKind.None),
                "the mark on the other half outlived the stair");

            // **The portal, and not reachability.** The obvious assertion — that the landing is
            // no longer reachable — is the wrong one for this fixture and passes or fails for the
            // wrong reason: the landing is the top of a single wall, so a colonist can get on to
            // it with a one-block hop whether or not a stair was ever there. Measured, 2026-09-21:
            // the connector is correctly gone and the landing is still reachable. The claim worth
            // making is that the stair took its portal with it.
            Assert.That(colony.Pawns.Nav.TwoCellConnectorTouching(head), Is.LessThan(0),
                "the stair is gone and its portal is still registered at the near half");
            Assert.That(colony.Pawns.Nav.TwoCellConnectorTouching(second), Is.LessThan(0),
                "...and at the far half");
        }

        // ---- where one may be ordered -----------------------------------------------------------

        /// <summary>Both halves need something to stand on; half a stair over air is refused.</summary>
        [Test]
        public void AStairNeedsAFootingUnderBothOfItsCells()
        {
            ColonyWorld colony = Board();
            int head = GroundNear(colony, 3);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));

            // One layer up, where only the near cell has anything under it.
            int up = Above(head);
            RaiseNow(colony, head, BuildingHandle.Wall);
            colony.World.Tick();

            Assert.That(colony.Construction.Place(Size.FromIndex(up), BuildingHandle.Stair, StuffHandle.Wood, 1),
                Is.Not.EqualTo(IntentRejection.None),
                "the far half would be standing on air");
        }

        /// <summary>
        /// The shaft rule, from the stair's side: a stair under a finished floor is refused, because
        /// it would be a stair a colonist climbs through the deck.
        /// </summary>
        [Test]
        public void AStairIsRefusedUnderASlab()
        {
            ColonyWorld colony = Board();
            AStoreyWithNothingLeadingToIt(colony, out int head, out int second, out _);

            RaiseNow(colony, Above(second), BuildingHandle.Floor);
            colony.World.Tick();

            Assert.That(colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Stair, StuffHandle.Wood, 1),
                Is.Not.EqualTo(IntentRejection.None),
                "the far half's own shaft is capped, and the far half is asked too");
        }

        /// <summary>
        /// And from the other side, or the rule is walked around in two moves: build the stair
        /// first, pour the floor over it afterwards.
        /// </summary>
        [Test]
        public void ASlabIsRefusedDirectlyOverAStair()
        {
            ColonyWorld colony = Board();
            AStoreyWithNothingLeadingToIt(colony, out int head, out int second, out _);

            Assume.That(colony.Construction.Allows(Above(second), BuildingHandle.Floor), Is.True,
                "the control: that slab is legal while no stair is under it");

            RaiseNow(colony, head, BuildingHandle.Stair, facing: 1);
            colony.World.Tick();

            Assert.That(colony.Construction.Allows(Above(head), BuildingHandle.Floor), Is.False);
            Assert.That(colony.Construction.Allows(Above(second), BuildingHandle.Floor), Is.False,
                "over either half, because either one is the stair");
        }

        /// <summary>
        /// **And a blueprint counts.** Two individually legal orders must not combine into the
        /// arrangement the rule exists to forbid — the fault a playtest found in the ladder's
        /// version of this rule, pinned here before anyone can find it again in the stair's.
        /// </summary>
        [Test]
        public void TwoOrdersCannotCombineIntoAStairUnderAFloor([Values(true, false)] bool stairFirst)
        {
            ColonyWorld colony = Board();
            AStoreyWithNothingLeadingToIt(colony, out int head, out int second, out _);

            CellRef stair = Size.FromIndex(head);
            CellRef floor = Size.FromIndex(Above(second));

            if (stairFirst)
            {
                Assume.That(colony.Construction.Place(stair, BuildingHandle.Stair, StuffHandle.Wood, 1),
                    Is.EqualTo(IntentRejection.None));
                Assert.That(colony.Construction.Place(floor, BuildingHandle.Floor, StuffHandle.Wood),
                    Is.Not.EqualTo(IntentRejection.None));
            }
            else
            {
                Assume.That(colony.Construction.Place(floor, BuildingHandle.Floor, StuffHandle.Wood),
                    Is.EqualTo(IntentRejection.None));
                Assert.That(colony.Construction.Place(stair, BuildingHandle.Stair, StuffHandle.Wood, 1),
                    Is.Not.EqualTo(IntentRejection.None));
            }
        }

        // ---- the generator's stairwells are not ours ---------------------------------------------

        /// <summary>
        /// <b>A colony edit beside a stamped stairwell must not take it out</b> — the fault this
        /// unit shipped into the Long tier before it was caught.
        ///
        /// <para>A generated stair carries no facing: the generator had nowhere to put one, and the
        /// mesher works it out by scanning for the partner half. So deriving the far cell from
        /// <c>Facing</c> 0 names the wrong cell, the refresh decides no connector is wanted, and it
        /// removes the one <c>ConnectorRegistrar</c> put there. Measured on the M2 demo: nine stair
        /// steps in a day fell to three, and a colonist starved two storeys beneath its food.</para>
        ///
        /// <para>Here rather than only in that day-long run, because a test that takes a quarter of
        /// a second is one somebody will actually run.</para>
        /// </summary>
        [Test]
        public void AColonyEditDoesNotDisturbTheGeneratorsOwnStairwells()
        {
            var scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            ColonyWorld colony = ColonyWorld.Build(
                new GridSize(60, 60, 5), 9u, scenario, barren: false, chunks: null,
                mapType: MapType.RuinedCity);

            int before = StairConnectors(colony);
            Assume.That(before, Is.GreaterThan(0), "the city must have stairwells to preserve");

            // Any colony edit at all: every one of them runs MarkNavAround, which is what calls
            // the stair refresh.
            int edits = 0;
            var grid = colony.Grid;
            for (int cell = 0; cell < grid.Size.CellCount && edits < 40; cell++)
            {
                if (!colony.Construction.Allows(cell, BuildingHandle.Wall)) continue;
                colony.Construction.Place(grid.Size.FromIndex(cell), BuildingHandle.Wall, StuffHandle.Wood);
                colony.Construction.Raise(colony.Pawns, cell);
                edits++;
            }

            Assume.That(edits, Is.GreaterThan(0), "at least one edit has to have happened");
            colony.World.Tick();

            Assert.That(StairConnectors(colony), Is.EqualTo(before),
                $"{edits} walls went up and the generator's stairwells must all still be there");
        }

        static int StairConnectors(ColonyWorld colony)
        {
            int found = 0;
            for (int id = 0; id < 4096; id++)
            {
                Connector? con = colony.Pawns.Nav.GetConnector(id);
                if (con != null && con.Kind == ConnectorKind.Stair) found++;
            }
            return found;
        }

        // ---- the connector is derived -----------------------------------------------------------

        /// <summary>
        /// A stair is saved as an edifice and its connector is not — it is re-derived on load, the
        /// same argument support and the region graph make, and the reason this unit costs no save
        /// format at all.
        /// </summary>
        [Test]
        public void AWayUpSurvivesASaveAndALoad()
        {
            ColonyWorld colony = Board();
            AStoreyWithNothingLeadingToIt(colony, out int head, out _, out int landing);
            RaiseNow(colony, head, BuildingHandle.Stair, facing: 1);
            colony.World.Tick();

            Pawn before = TheColonist(colony);
            Assume.That(colony.Pawns.Reachable(before, landing, TraverseMode.Hauler), Is.True);

            byte[] saved = colony.Save();
            ColonyWorld reloaded = Board();
            reloaded.Load(saved);

            Assert.That(reloaded.Pawns.Reachable(TheColonist(reloaded), landing, TraverseMode.Hauler),
                Is.True, "the connector came back with the world, derived from the edifice list");
        }
    }
}
