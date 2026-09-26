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
    /// feature existing at all. Design: <c>docs/design/60-stairs.md</c>.</para>
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

            // Raise refuses while a colonist is walking through a cell a blocking thing would fill
            // (design 30, nobody in a wall); wait for them to pass, and say so if they never do.
            for (int tick = 0; tick < 600 && !colony.Construction.Raise(colony.Pawns, cell); tick++)
                colony.World.Tick();
            Assert.That(colony.Construction.SiteAt(Size.FromIndex(cell)), Is.EqualTo(-1),
                $"{cell} was never raised: somebody stood in it for ten seconds");
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
        /// A storey to climb to, and the one cell a stair will stand in.
        ///
        /// <para><b>Exactly the ladder's shape, and since 2026-09-21 that is literal.</b> A wall
        /// one cell along, a slab on top of it as the landing, and the way up in the column beside
        /// it with nothing over it. The stair climbs the whole layer inside <paramref name="cell"/>
        /// and steps off sideways on to the landing — which is what a ladder does, and is why the
        /// two can share a fixture at last.</para>
        ///
        /// <para>The two-cell version of this had the stair's far half between the near half and
        /// the wall, so the landing was two cells from the cell a colonist started in. Several of
        /// the assertions below were written against that geometry and would now hold vacuously;
        /// they are stated against <paramref name="cell"/> alone.</para>
        /// </summary>
        static void AStoreyWithNothingLeadingToIt(
            ColonyWorld colony, out int cell, out int beside, out int landing)
        {
            cell = GroundNear(colony, 3);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            // Facing 1 is +X. The stair climbs in its own cell and arrives beside this wall's top.
            beside = cell + 1;

            Assume.That(colony.Construction.Allows(beside, BuildingHandle.Wall), Is.True);
            RaiseNow(colony, beside, BuildingHandle.Wall);
            colony.World.Tick();

            landing = Above(beside);
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
            // The ladder goes in the same cell the stair will: a landing has to be orthogonally
            // beside the top of the shaft, and that is now true of the one cell either of them
            // stands in. (The two-cell stair needed its FAR half here, which is the sentence this
            // comment used to carry.)
            ColonyWorld ladderBoard = Board();
            AStoreyWithNothingLeadingToIt(ladderBoard, out int lShaft, out _, out int lLanding);
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

            // Assert, not Assume. A failed Assume is reported by NUnit as SKIPPED, so a control
            // that stops holding takes the whole test green with it — which is exactly what the
            // two-cell fixture did to four tests in this file the day the footprint changed.
            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Colonist), Is.False,
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
            // Every mode there is, not a list of them: a mode added later is covered or fails here.
            foreach (TraverseMode mode in (TraverseMode[])System.Enum.GetValues(typeof(TraverseMode)))
                Assert.That(colony.Pawns.Reachable(pawn, landing, mode), Is.True, $"{mode} may climb a stair");
        }

        /// <summary>
        /// <b>A stairwell climbs more than one storey</b> (review, 2026-09-26): a second stair stands
        /// in the open shaft cell at the top of the first and carries a colonist on up. The order
        /// rule said so in a comment and refused it in fact, because a stair is non-blocking and the
        /// cell over it has no floor — the ladder chain's fault, met again.
        /// </summary>
        [Test]
        public void AStairStandsOnAStairAndTheStairwellClimbsTwoStoreys()
        {
            ColonyWorld colony = Board();
            AStoreyWithNothingLeadingToIt(colony, out int first, out int beside, out int landing);
            RaiseNow(colony, first, BuildingHandle.Stair, facing: 1);
            colony.World.Tick();

            // A second storey on the first landing: a wall standing on it, a slab on the wall.
            RaiseNow(colony, landing, BuildingHandle.Wall);
            colony.World.Tick();
            int upper = Above(Above(beside));
            RaiseNow(colony, upper, BuildingHandle.Floor);
            colony.World.Tick();

            Pawn pawn = TheColonist(colony);
            Assert.That(colony.Pawns.Reachable(pawn, upper, TraverseMode.Hauler), Is.False,
                "the control: one stair does not reach the second storey");

            int second = Above(first);
            RaiseNow(colony, second, BuildingHandle.Stair, facing: 1);
            colony.World.Tick();

            Assert.That(colony.Grid.Edifice[second], Is.GreaterThanOrEqualTo(0), "the second stair stands");
            Assert.That(colony.Pawns.Reachable(pawn, upper, TraverseMode.Hauler), Is.True,
                "and the stairwell carries a hauler up two storeys");
        }

        // ---- one cell, one record ---------------------------------------------------------------

        /// <summary>
        /// <b>A colony-built stair is one record in one cell, climbing a whole layer</b>
        /// (2026-09-21, <c>60-stairs.md</c> §10). The owner's words: <i>"It should be able to go up
        /// a flight in one square for ease."</i>
        ///
        /// <para>The neighbour is asserted empty in the same breath, because "one cell" is a claim
        /// about what is <em>not</em> there and a test that only looked at the head would have
        /// passed just as happily on the two-cell version.</para>
        ///
        /// <para>And the def is checked by name: it is <see cref="CoreContent.EdificeStairFull"/>
        /// and deliberately not worldgen's <c>EdificeStairLower</c>, which the stamped city still
        /// uses and which still means a half-flight rising 1.5 m.</para>
        /// </summary>
        [Test]
        public void AStairIsOneRecordInOneCell()
        {
            ColonyWorld colony = Board();
            AStoreyWithNothingLeadingToIt(colony, out int head, out _, out _);
            RaiseNow(colony, head, BuildingHandle.Stair, facing: 1);
            colony.World.Tick();

            int handle = colony.Grid.Edifice[head];
            Assert.That(handle, Is.GreaterThanOrEqualTo(0), "the stair is not standing in its cell");
            Assert.That(colony.Construction.Edifices.Records[handle].Def,
                Is.EqualTo(CoreContent.EdificeStairFull),
                "a built stair is the one-cell full flight, not one of worldgen's halves");

            // Every neighbour on the layer, so the claim does not depend on guessing which way a
            // vanished far half would have fallen.
            CellRef at = Size.FromIndex(head);
            for (int f = 0; f < 4; f++)
            {
                int x = at.X + (f == 1 ? 1 : f == 3 ? -1 : 0);
                int z = at.Z + (f == 0 ? 1 : f == 2 ? -1 : 0);
                if (!Size.Contains(x, z, at.Y)) continue;
                int neighbour = Size.Index(x, z, at.Y);
                if (neighbour == head) continue;

                Assert.That(colony.Grid.Edifice[neighbour], Is.Not.EqualTo(handle),
                    $"the stair claimed the cell at facing {f}; it occupies one cell and no more");
            }
        }

        /// <summary>
        /// Taken apart by naming its cell, and the portal goes with it. A record left standing
        /// would hand back a connector for a stair that is not there on the next load.
        /// </summary>
        [Test]
        public void DemolishingAStairTakesItsPortalWithIt()
        {
            ColonyWorld colony = Board();
            AStoreyWithNothingLeadingToIt(colony, out int head, out _, out int landing);
            RaiseNow(colony, head, BuildingHandle.Stair, facing: 1);
            colony.World.Tick();

            Pawn pawn = TheColonist(colony);
            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Hauler), Is.True,
                "the control: the stair opened the storey to a hauler in the first place");

            Assert.That(colony.Construction.Demolish(colony.Pawns, head, out _), Is.True);
            colony.World.Tick();

            Assert.That(colony.Grid.Edifice[head], Is.LessThan(0), "the stair is gone");
            Assert.That(colony.Pawns.Nav.OneCellConnectorAt(head, ConnectorKind.Stair), Is.LessThan(0),
                "the portal must not outlive the stair");
            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Hauler), Is.False,
                "and a hauler must not still be able to get up there");
        }

        /// <summary>
        /// <b>A stair the generator stamped does not take its neighbour's record down with it.</b>
        ///
        /// <para>U44 taught <c>EdificeFootprint.Cells()</c> to answer 2 for either stair half
        /// <em>whoever</em> stamped it, which is right — a stamped stair and a built one are
        /// deliberately the same thing to the mesher and the graph. But a stamped stair carries no
        /// facing (<c>RefreshStair</c> says so in as many words and guards itself against it), so
        /// <c>SecondCell</c> derived from Facing 0 names whatever happens to lie north of it.
        /// Without the partner comparison in <c>Demolish</c>, that neighbour's record is flagged
        /// <c>Removed</c> while the neighbour itself is still standing — a wall that exists in the
        /// grid and is gone from the save.</para>
        ///
        /// <para><b>Nothing reaches it through the gesture today</b>, because
        /// <c>DesignationGrid.CanDeconstruct</c> refuses anything the colony did not build. This
        /// test does not go through the gesture, deliberately: the point is that the guard belongs
        /// to <c>Demolish</c> rather than to a rule three files away that nothing ties to it. Same
        /// argument as the unchecked index this branch's own review found on the stair fan-out.</para>
        /// </summary>
        [Test]
        public void DemolishingAStampedStairLeavesTheNeighbourItPointsAtStanding()
        {
            ColonyWorld colony = Board();
            int stair = GroundNear(colony, 3);
            Assume.That(stair, Is.GreaterThanOrEqualTo(0));

            // Facing 0 is north, so a stamped stair's derived far cell is the one along +Z.
            int north = stair + Size.SizeX;
            Assume.That(colony.Construction.Allows(north, BuildingHandle.Wall), Is.True);
            RaiseNow(colony, north, BuildingHandle.Wall);
            colony.World.Tick();

            int wallHandle = colony.Grid.Edifice[north];
            Assume.That(wallHandle, Is.GreaterThanOrEqualTo(0));

            // Stamped, not built: a record with no facing, exactly as worldgen leaves one.
            var records = colony.Construction.Edifices.Records;
            records.Add(new PlacedEdifice
            {
                CellIndex = stair, Def = CoreContent.EdificeStairLower,
                Stuff = 0, Built = false, Facing = 0,
            });
            colony.Grid.Edifice[stair] = records.Count - 1;

            Assert.That(colony.Construction.Demolish(colony.Pawns, stair, out _), Is.True);

            Assert.That(records[wallHandle].Removed, Is.False,
                "the wall north of a stamped stair is not part of it and its record must survive");
            Assert.That(colony.Grid.Edifice[north], Is.EqualTo(wallHandle),
                "and the wall itself is still standing in its cell");
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
        /// <para><b>One cell since 2026-09-21</b>, so the "by either half" parameter this test used
        /// to carry is gone with the far half itself. What is left is still worth asserting, and is
        /// the part that was never about the footprint: the mark has to be seen, walked to,
        /// finished, and cleared.</para>
        /// </summary>
        [Test]
        public void AMarkedStairIsPulledDownByAColonist()
        {
            ColonyWorld colony = Board();
            AStoreyWithNothingLeadingToIt(colony, out int head, out _, out int landing);
            RaiseNow(colony, head, BuildingHandle.Stair, facing: 1);
            colony.World.Tick();

            Assert.That(colony.Designations.CanDeconstruct(head), Is.True,
                "the cell that was clicked has to be something the deconstruct tool will take");
            Assert.That(
                colony.Designations.Designate(Size.FromIndex(head), DesignationKind.Deconstruct),
                Is.EqualTo(IntentRejection.None), "the order was refused");

            for (int tick = 0; tick < 20_000 && colony.Grid.Edifice[head] >= 0; tick++)
                colony.World.Tick();

            // Assert rather than Assume, for the reason DeconstructTests records against its own
            // stone wall: a precondition that fails is reported as green.
            Assert.That(colony.Grid.Edifice[head], Is.LessThan(0), "nobody pulled the stair down");

            Assert.That(colony.Designations.At(head), Is.EqualTo(DesignationKind.None),
                "a mark outlived the thing it was on");

            // **The portal, and not reachability.** The obvious assertion — that the landing is
            // no longer reachable — is the wrong one for this fixture and passes or fails for the
            // wrong reason: the landing is the top of a single wall, so a colonist can get on to
            // it with a one-block hop whether or not a stair was ever there. Measured, 2026-09-21:
            // the connector is correctly gone and the landing is still reachable. The claim worth
            // making is that the stair took its portal with it.
            Assert.That(colony.Pawns.Nav.OneCellConnectorAt(head, ConnectorKind.Stair), Is.LessThan(0),
                "the stair is gone and its portal is still registered in its cell");
        }

        /// <summary>
        /// <b>A ladder's refresh must not carry off a stair's portal.</b>
        ///
        /// <para>Both are one-cell ways up now, so both are looked up through
        /// <c>NavGraph.OneCellConnectorAt</c> — and that method had no notion of <i>kind</i>, having
        /// been written when the ladder was the only one-cell connector there was. Every structure
        /// edit runs <c>RefreshLaddersAround</c>, a seven-cell fan-out; on a stair's cell it would
        /// have found the stair's portal, asked <c>IsLadder</c>, been told no, and removed it. A way
        /// up that disappears because somebody built a wall next door — <c>docs/bug-patterns.md</c>
        /// P1, one rule with two owners.</para>
        ///
        /// <para>Caught at the signature rather than here: <c>OneCellConnectorAt</c> takes a
        /// <see cref="ConnectorKind"/> and cannot hand a caller somebody else's. This test is what
        /// says the fan-out really does reach a stair's cell, which is the half a signature cannot
        /// assert — without it the guard is a plausible precaution rather than a fix.</para>
        /// </summary>
        [Test]
        public void AnEditBesideAStairLeavesItsPortalStanding()
        {
            ColonyWorld colony = Board();
            AStoreyWithNothingLeadingToIt(colony, out int head, out _, out int landing);
            RaiseNow(colony, head, BuildingHandle.Stair, facing: 1);
            colony.World.Tick();

            Pawn pawn = TheColonist(colony);
            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Hauler), Is.True,
                "the control: the stair opened the storey before anything was built beside it");

            // A wall on the far side of the stair, so the ladder fan-out is put over the stair's
            // own cell. Any structure edit would do; this is the nearest one.
            int behind = head - 1;
            Assert.That(colony.Construction.Allows(behind, BuildingHandle.Wall), Is.True);
            RaiseNow(colony, behind, BuildingHandle.Wall);
            colony.World.Tick();

            Assert.That(colony.Pawns.Nav.OneCellConnectorAt(head, ConnectorKind.Stair),
                Is.GreaterThanOrEqualTo(0),
                "a wall next door took the stair's portal out with it");
            Assert.That(colony.Pawns.Reachable(pawn, landing, TraverseMode.Hauler), Is.True,
                "and the storey stopped being reachable because of it");
        }

        // ---- where one may be ordered -----------------------------------------------------------

        /// <summary>
        /// A stair needs something to stand on; a flight hanging in air is refused.
        ///
        /// <para>Measured against a control in the same test, because "refused" is the answer this
        /// board gives to a great many orders and a test that only saw the refusal would pass
        /// without the footing rule existing at all: the cell one layer up over a <em>wall</em> is
        /// accepted and the cell one layer up over <em>nothing</em> is not, and the only difference
        /// between them is the footing.</para>
        /// </summary>
        [Test]
        public void AStairNeedsAFootingUnderIt()
        {
            ColonyWorld colony = Board();
            int onTheGround = GroundNear(colony, 3);
            Assert.That(onTheGround, Is.GreaterThanOrEqualTo(0));

            // A wall to stand the control on, and its neighbour left as open air.
            RaiseNow(colony, onTheGround, BuildingHandle.Wall);
            colony.World.Tick();

            Assert.That(
                colony.Construction.Allows(Above(onTheGround), BuildingHandle.Stair), Is.True,
                "the control: a stair may start from the top of a wall, which has a footing");

            int overAir = Above(onTheGround) + 1;
            Assert.That(
                colony.Construction.Place(Size.FromIndex(overAir), BuildingHandle.Stair, StuffHandle.Wood, 1),
                Is.Not.EqualTo(IntentRejection.None),
                "a stair with nothing under it would be a connector hanging in air");
        }

        /// <summary>
        /// The shaft rule, from the stair's side: a stair under a finished floor is refused, because
        /// it would be a stair a colonist climbs through the deck.
        /// </summary>
        [Test]
        public void AStairIsRefusedUnderASlab()
        {
            ColonyWorld colony = Board();
            AStoreyWithNothingLeadingToIt(colony, out int head, out _, out _);

            Assert.That(colony.Construction.Allows(head, BuildingHandle.Stair), Is.True,
                "the control: that cell takes a stair perfectly well while its shaft is open");

            RaiseNow(colony, Above(head), BuildingHandle.Floor);
            colony.World.Tick();

            Assert.That(colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Stair, StuffHandle.Wood, 1),
                Is.Not.EqualTo(IntentRejection.None),
                "the shaft is capped, so the flight would climb into the underside of a deck");
        }

        /// <summary>
        /// And from the other side, or the rule is walked around in two moves: build the stair
        /// first, pour the floor over it afterwards.
        /// </summary>
        [Test]
        public void ASlabIsRefusedDirectlyOverAStair()
        {
            ColonyWorld colony = Board();
            AStoreyWithNothingLeadingToIt(colony, out int head, out _, out _);

            Assert.That(colony.Construction.Allows(Above(head), BuildingHandle.Floor), Is.True,
                "the control: that slab is legal while no stair is under it");

            RaiseNow(colony, head, BuildingHandle.Stair, facing: 1);
            colony.World.Tick();

            Assert.That(colony.Construction.Allows(Above(head), BuildingHandle.Floor), Is.False,
                "a slab capping the flight is the same fault ordered the other way round");
            Assert.That(colony.Construction.Allows(Above(Above(head)), BuildingHandle.Floor), Is.False,
                "and a slab over the ARRIVAL cell roofs the way up just as finally - the two-cell "
                + "reach in ShaftRulePermits is about the arrival, not about a far half");
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
            AStoreyWithNothingLeadingToIt(colony, out int head, out _, out _);

            CellRef stair = Size.FromIndex(head);
            CellRef floor = Size.FromIndex(Above(head));

            // Assert on the FIRST order as well as the second: whichever goes down first has to be
            // accepted, or the test proves only that this board refuses everything.
            if (stairFirst)
            {
                Assert.That(colony.Construction.Place(stair, BuildingHandle.Stair, StuffHandle.Wood, 1),
                    Is.EqualTo(IntentRejection.None), "the stair order was refused on its own");
                Assert.That(colony.Construction.Place(floor, BuildingHandle.Floor, StuffHandle.Wood),
                    Is.Not.EqualTo(IntentRejection.None));
            }
            else
            {
                Assert.That(colony.Construction.Place(floor, BuildingHandle.Floor, StuffHandle.Wood),
                    Is.EqualTo(IntentRejection.None), "the floor order was refused on its own");
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
