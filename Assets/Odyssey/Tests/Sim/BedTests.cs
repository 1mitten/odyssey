#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The bed, as a thing that is ordered, stands across two cells and comes back down
    /// (docs/design/20-beds.md §4). The one-record rule is what these tests hold: both cells'
    /// <c>Edifice[]</c> point at the same record, either half names the site, and taking the bed
    /// apart by clicking either half leaves neither half behind.
    /// </summary>
    public class BedTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);
        const uint Seed = 20260917;

        static ColonyWorld Fresh()
        {
            // Three colonists for the ownership tests, and no scenario beds: this file's questions
            // are about the beds the colony builds, and the chooser's list should contain nothing
            // but what these tests raise.
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 0;
            return ColonyWorld.Build(Size, Seed, scenario);
        }

        /// <summary>
        /// A cell beside the start whose whole north-facing footprint can take a bed: the head and
        /// the cell it would claim both open. Returns the head, with the second cell in
        /// <paramref name="second"/>.
        /// </summary>
        static int OpenFootprint(ColonyWorld colony, out int second)
        {
            CellRef start = colony.Start;
            for (int radius = 1; radius < 8; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!Size.Contains(x, z, start.Y)) continue;

                int head = Size.Index(x, z, start.Y);
                if (!colony.Construction.Allows(head)) continue;

                int foot = EdificeFootprint.SecondCell(head, CoreContent.EdificeBed, 0, Size);
                if (foot >= 0 && colony.Construction.Allows(foot))
                {
                    second = foot;
                    return head;
                }
            }

            second = -1;
            return -1;
        }

        [Test]
        public void ABedOrderIsOneSiteAndEitherHalfNamesIt()
        {
            ColonyWorld colony = Fresh();
            int head = OpenFootprint(colony, out int second);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));

            Assert.That(
                colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, facing: 0),
                Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.Count, Is.EqualTo(1), "a bed is one order, not two");

            Assert.That(colony.Construction.SiteAt(Size.FromIndex(head)), Is.EqualTo(head));
            Assert.That(colony.Construction.SiteAt(Size.FromIndex(second)), Is.EqualTo(head),
                "clicking the far cell names the same site, because there is only one");
        }

        [Test]
        public void CancellingByTheFarCellTakesTheWholeOrderOff()
        {
            ColonyWorld colony = Fresh();
            int head = OpenFootprint(colony, out int second);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));
            Assume.That(colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, 0),
                Is.EqualTo(IntentRejection.None));

            Assert.That(colony.Construction.Cancel(Size.FromIndex(second)), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.Count, Is.EqualTo(0));
            Assert.That(colony.Construction.SiteAt(Size.FromIndex(head)), Is.EqualTo(-1));
        }

        [Test]
        public void AWallCannotBeOrderedIntoABedsSecondCell()
        {
            ColonyWorld colony = Fresh();
            int head = OpenFootprint(colony, out int second);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));
            Assume.That(colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, 0),
                Is.EqualTo(IntentRejection.None));

            Assert.That(colony.Construction.Place(Size.FromIndex(second), BuildingHandle.Wall, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.NotPermitted),
                "two sites would overlap the moment both finished");
        }

        [Test]
        public void ABedsSecondCellMustBeAbleToTakeItToo()
        {
            ColonyWorld colony = Fresh();
            int head = OpenFootprint(colony, out int second);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));

            // A standing wall in the cell this bed's north end would claim: the order must be
            // refused — and turning the bed away from the wall must make the same order legal,
            // which is the point of rotation.
            Assume.That(colony.Construction.Place(Size.FromIndex(second), BuildingHandle.Wall, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None));
            colony.Construction.Raise(colony.Pawns, second);

            Assert.That(
                colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, facing: 0),
                Is.EqualTo(IntentRejection.NotPermitted));

            // Facing east instead: a different second cell, if that one is open. If the east
            // neighbour happens to be taken the board has grown a hedge around the start, and the
            // assume below says so rather than passing vacuously.
            int east = EdificeFootprint.SecondCell(head, CoreContent.EdificeBed, 1, Size);
            Assume.That(east, Is.GreaterThanOrEqualTo(0));
            Assume.That(colony.Construction.Allows(east), Is.True);
            Assert.That(
                colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, facing: 1),
                Is.EqualTo(IntentRejection.None));
        }

        [Test]
        public void ABedOrderIntoSolidGroundIsRefusedNotLifted()
        {
            ColonyWorld colony = Fresh();

            // A solid cell with open air above it: exactly the cell a wall order names when it is
            // lifted by StandingOn. A bed must refuse it instead, because lifting one end of a
            // two-cell thing is an order whose shape the player cannot see.
            CellRef start = colony.Start;
            for (int radius = 1; radius < 8; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!Size.Contains(x, z, start.Y)) continue;

                int index = Size.Index(x, z, start.Y);
                if (!colony.Grid.IsSolidTerrain(index)) continue;
                int above = index + Size.LayerStride;
                if (above >= Size.CellCount || !colony.Construction.Allows(above)) continue;

                Assert.That(
                    colony.Construction.Place(Size.FromIndex(index), BuildingHandle.Bed, StuffHandle.Wood, 0),
                    Is.EqualTo(IntentRejection.NotPermitted),
                    "a two-cell order is never lifted onto the cell above the ground it named");
                Assert.That(
                    colony.Construction.Place(Size.FromIndex(index), BuildingHandle.Wall, StuffHandle.Wood),
                    Is.EqualTo(IntentRejection.None),
                    "the control: a wall order on the same ground is lifted, as it always was");
                return;
            }

            Assert.Ignore("no solid cell with open air above it near the start on this board");
        }

        [Test]
        public void ARaisedBedIsOneRecordBehindTwoCellsAndSurvivesASave()
        {
            ColonyWorld original = Fresh();
            int head = OpenFootprint(original, out int second);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));
            Assume.That(original.Construction.Place(Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, 0),
                Is.EqualTo(IntentRejection.None));
            original.Construction.Raise(original.Pawns, head, quality: 3);

            int handle = original.Grid.Edifice[head];
            Assert.That(handle, Is.GreaterThanOrEqualTo(0));
            Assert.That(original.Grid.Edifice[second], Is.EqualTo(handle),
                "both cells point at the one record — the invariant the whole bed design stands on");
            Assert.That(original.Grid.IsBlockedByEdifice(head), Is.False, "a bed is walked over, not around");

            PlacedEdifice before = original.Outcome.Edifices[handle];
            Assert.That(before.Def, Is.EqualTo(CoreContent.EdificeBed));
            Assert.That(before.Facing, Is.EqualTo(0));
            Assert.That(before.Quality, Is.EqualTo(3));
            Assert.That(before.Owner, Is.EqualTo(0), "0 is nobody: pawn ids are 1-based");

            ColonyWorld restored = Fresh();
            restored.Load(original.Save());

            int restoredHandle = restored.Grid.Edifice[head];
            Assert.That(restoredHandle, Is.GreaterThanOrEqualTo(0), "the head still says a bed stands there");
            Assert.That(restored.Grid.Edifice[second], Is.EqualTo(restoredHandle),
                "the far cell still points at the same record after a save and a load");
            PlacedEdifice after = restored.Outcome.Edifices[restoredHandle];
            Assert.That(after.Def, Is.EqualTo(CoreContent.EdificeBed));
            Assert.That(after.Facing, Is.EqualTo(before.Facing));
            Assert.That(after.Quality, Is.EqualTo(before.Quality));
            Assert.That(after.Owner, Is.EqualTo(before.Owner));
        }

        [Test]
        public void ABedsFacingIsInTheStateHash()
        {
            ColonyWorld north = Fresh();
            ColonyWorld east = Fresh();
            int northHead = OpenFootprint(north, out _);
            int eastHead = OpenFootprint(east, out _);
            Assume.That(northHead, Is.GreaterThanOrEqualTo(0));
            Assume.That(eastHead, Is.EqualTo(northHead), "same seed, same board, same cells");

            Assume.That(north.Construction.Place(Size.FromIndex(northHead), BuildingHandle.Bed, StuffHandle.Wood, 0),
                Is.EqualTo(IntentRejection.None));
            Assume.That(east.Construction.Place(Size.FromIndex(eastHead), BuildingHandle.Bed, StuffHandle.Wood, 1),
                Is.EqualTo(IntentRejection.None));
            north.Construction.Raise(north.Pawns, northHead);
            east.Construction.Raise(east.Pawns, eastHead);

            Assert.That(north.World.ComputeStateHash().Value,
                Is.Not.EqualTo(east.World.ComputeStateHash().Value),
                "the same bed facing two ways must not hash alike — which way a thing was placed " +
                "is world state, not drawing");
        }

        [Test]
        public void ADemolishedBedLeavesNeitherHalfBehind()
        {
            ColonyWorld colony = Fresh();
            int head = OpenFootprint(colony, out int second);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));
            Assume.That(colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, 0),
                Is.EqualTo(IntentRejection.None));
            colony.Construction.Raise(colony.Pawns, head);
            Assume.That(colony.Pawns.Items.Beds.Contains(head), Is.True,
                "a finished bed's head cell joins the list the sleep chooser scans");

            // Taken apart by naming the far cell: the half the player clicked is not the half the
            // record calls home, and both must still go.
            int handle = colony.Grid.Edifice[head];
            Assert.That(colony.Construction.Demolish(colony.Pawns, second, out PlacedEdifice was), Is.True);
            Assert.That(was.CellIndex, Is.EqualTo(head));
            Assert.That(colony.Grid.Edifice[head], Is.EqualTo(-1));
            Assert.That(colony.Grid.Edifice[second], Is.EqualTo(-1), "the far half went with it");
            Assert.That(colony.Outcome.Edifices[handle].Removed, Is.True,
                "the record is marked out rather than dropped, so every other handle stays valid");
            Assert.That(colony.Pawns.Items.Beds.Contains(head), Is.False,
                "a demolished bed is not slept in");
        }

        // ---- ownership (design 20 section 7) --------------------------------------------------

        /// <summary>A second open footprint, distinct from the first, for the two-bed tests.</summary>
        static int AnotherOpenFootprint(ColonyWorld colony, int notHead, out int second)
        {
            CellRef start = colony.Start;
            for (int radius = 1; radius < 12; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!Size.Contains(x, z, start.Y)) continue;

                int head = Size.Index(x, z, start.Y);
                if (head == notHead || !colony.Construction.Allows(head)) continue;

                int foot = EdificeFootprint.SecondCell(head, CoreContent.EdificeBed, 0, Size);
                if (foot >= 0 && colony.Construction.Allows(foot))
                {
                    second = foot;
                    return head;
                }
            }

            second = -1;
            return -1;
        }

        /// <summary>
        /// Put a bed up at this cell: order it, then finish it at the given tier.
        ///
        /// <para><b>The order is why this helper exists.</b> <c>Raise</c> reads the site out of
        /// <c>_building[cell]</c> and returns at once when there is none, so calling it without
        /// placing first is a no-op that raises nothing and says nothing. Four tests did exactly
        /// that and every one of them ended on an <c>Assume</c> that a bed they had never ordered
        /// could be assigned an owner — which reports Inconclusive, not a failure, so the fast
        /// tier stayed green and the whole of the ownership feature went untested.</para>
        ///
        /// <para>The assertion at the end is the guard against that happening again: a helper
        /// named "raise a bed" now fails loudly when no bed is standing afterwards.</para>
        /// </summary>
        static void RaiseABed(ColonyWorld colony, int head, byte quality = (byte)QualityHandle.Normal)
        {
            // Place unless the caller already did: an identical order answers AlreadyInThatState,
            // which is a success for our purposes and not worth making every caller branch on.
            IntentRejection placed = colony.Construction.Place(
                Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, facing: 0);
            Assume.That(placed, Is.AnyOf(IntentRejection.None, IntentRejection.AlreadyInThatState),
                "the bed could be ordered at this cell");

            colony.Construction.Raise(colony.Pawns, head, quality);

            int handle = colony.Grid.Edifice[head];
            Assert.That(handle, Is.GreaterThanOrEqualTo(0), "a bed stands at the cell it was raised at");
            Assert.That(colony.Construction.Edifices.Records[handle].Def,
                Is.EqualTo(CoreContent.EdificeBed), "and the thing standing there is a bed");
        }

        static IntentRejection Assign(ColonyWorld colony, int anyBedCell, int pawnId)
        {
            // Rejections accumulate until taken; clear the last assert's away so this one reads
            // only its own answer.
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(new Intent(
                IntentKind.AssignBedOwner, Size.FromIndex(anyBedCell), pawnId));
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        /// <summary>As <see cref="Assign"/>, for a cell the test already holds as a reference.</summary>
        static IntentRejection AssignCell(ColonyWorld colony, CellRef cell, int pawnId)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(new Intent(IntentKind.AssignBedOwner, cell, pawnId));
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        [Test]
        public void ABedCanBeGivenToOneColonistAndTakenBack()
        {
            ColonyWorld colony = Fresh();
            var pawn = colony.Pawns.Pawns.All[0];
            int head = OpenFootprint(colony, out int second);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));
            Assume.That(colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, 0),
                Is.EqualTo(IntentRejection.None));
            RaiseABed(colony, head);

            // Either half names the bed, because both point at the one record.
            Assert.That(Assign(colony, second, pawn.Id.Value), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.BedOwnerAt(head), Is.EqualTo(pawn.Id.Value));
            Assert.That(colony.Construction.BedOwnerAt(second), Is.EqualTo(pawn.Id.Value),
                "the far cell answers the same owner, not a second opinion");

            Assert.That(Assign(colony, head, pawn.Id.Value), Is.EqualTo(IntentRejection.AlreadyInThatState));

            Assert.That(Assign(colony, head, -1), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.BedOwnerAt(head), Is.EqualTo(0), "taken back: 0 is nobody");
        }

        [Test]
        public void AColonistWhoTakesANewBedReleasesTheOldOne()
        {
            ColonyWorld colony = Fresh();
            var pawn = colony.Pawns.Pawns.All[0];
            int first = OpenFootprint(colony, out _);
            int secondBed = AnotherOpenFootprint(colony, first, out _);
            Assume.That(first, Is.GreaterThanOrEqualTo(0));
            Assume.That(secondBed, Is.GreaterThanOrEqualTo(0));
            RaiseABed(colony, first);
            RaiseABed(colony, secondBed);

            Assert.That(Assign(colony, first, pawn.Id.Value), Is.EqualTo(IntentRejection.None));
            Assert.That(Assign(colony, secondBed, pawn.Id.Value), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.BedOwnerAt(first), Is.EqualTo(0),
                "one bed per colonist, kept by the handler rather than hoped for by the interface");
            Assert.That(colony.Construction.BedOwnerAt(secondBed), Is.EqualTo(pawn.Id.Value));
        }

        [Test]
        public void OwnershipIsRefusedWhereThereIsNoBedToOwn()
        {
            ColonyWorld colony = Fresh();
            var pawn = colony.Pawns.Pawns.All[0];

            Assert.That(AssignCell(colony, colony.Start, pawn.Id.Value),
                Is.EqualTo(IntentRejection.NotPermitted), "bare ground is nobody's bed");

            Assert.That(AssignCell(colony, colony.Start, 9_999),
                Is.EqualTo(IntentRejection.NotPermitted), "a pawn who does not exist cannot own one either");

            int head = OpenFootprint(colony, out int second);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));
            Assume.That(colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Wall, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None));
            colony.Construction.Raise(colony.Pawns, head);
            Assert.That(Assign(colony, head, pawn.Id.Value),
                Is.EqualTo(IntentRejection.NotPermitted), "a wall is not a bed, whoever finishes it");
        }

        [Test]
        public void AnOwnerSleepsInTheirOwnBedAndNobodyElseDoes()
        {
            ColonyWorld colony = Fresh();
            var owner = colony.Pawns.Pawns.All[0];
            var other = colony.Pawns.Pawns.All[1];

            // Two beds. The owner's is the one further from where either of them stands: own-bed
            // preference is only observable if distance argues the other way.
            int near = OpenFootprint(colony, out int nearFoot);
            int far = AnotherOpenFootprint(colony, near, out int farFoot);
            Assume.That(near, Is.GreaterThanOrEqualTo(0));
            Assume.That(far, Is.GreaterThanOrEqualTo(0));
            RaiseABed(colony, near);
            RaiseABed(colony, far);
            Assert.That(Assign(colony, far, owner.Id.Value), Is.EqualTo(IntentRejection.None));

            owner.Needs[NeedIndex.Rest] = 40;
            for (int i = 0; i < 6_000 && !owner.Asleep; i++) colony.World.Tick();
            Assert.That(owner.Asleep, Is.True, "the owner found somewhere to sleep");
            Assert.That(owner.Cell, Is.EqualTo(far),
                "a colonist walks past a nearer unowned bed to sleep in their own");

            other.Needs[NeedIndex.Rest] = 40;
            for (int i = 0; i < 6_000 && !other.Asleep; i++) colony.World.Tick();
            Assert.That(other.Asleep, Is.True);
            Assert.That(other.Cell, Is.EqualTo(near),
                "the unowned bed is the free bed: nobody checks into somebody else's");
        }

        // ---- a bed and the floors: above, below, and one storey up (the owner's use case) ------

        /// <summary>
        /// Lay a slab the way the building line's floor half will lay it when it lands: straight
        /// onto the grid, at the cell's lower boundary. Until then this is the same rig
        /// <c>SupportSolverTests</c> uses, and the bed's claim is about what it stands on, not who
        /// put it there.
        /// </summary>
        static void LaySlab(ColonyWorld colony, int cell) =>
            colony.Grid.Floor[cell] = CoreContent.SlabStructural;

        /// <summary>
        /// A bed ordered a storey above the ground, on slabs laid over open air: both cells of the
        /// footprint must accept it, because a slab is a floor - the one rule <c>Allows</c> has
        /// always made about underfoot, now asked of a two-cell thing on a storey nothing else
        /// occupies (owner, 2026-09-17: beds must build on floors above).
        /// </summary>
        [Test]
        public void ABedCanBeOrderedOnSlabsAStoreyAboveTheGround()
        {
            ColonyWorld colony = Fresh();
            CellRef start = colony.Start;

            // Open air one storey over the start's floor, with open air beneath it too: a true
            // second storey, not the surface cell renamed. The board is flat here, so the pair a
            // step up over the start's own clearing is the honest example.
            int head = Size.Index(start.X, start.Z, start.Y + 1);
            int foot = EdificeFootprint.SecondCell(head, CoreContent.EdificeBed, 0, Size);
            Assume.That(foot, Is.GreaterThanOrEqualTo(0));
            Assume.That(colony.Grid.IsSolidTerrain(head), Is.False);
            Assume.That(colony.Grid.HasFloor(head), Is.False,
                "the storey beneath is open air, so the slab - not the ground - must be the floor");

            LaySlab(colony, head);
            LaySlab(colony, foot);

            Assert.That(
                colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, 0),
                Is.EqualTo(IntentRejection.None),
                "a slab is a floor, and the bed's footprint accepts both of its cells on slabs");
            colony.Construction.Raise(colony.Pawns, head, (byte)QualityHandle.Normal);

            Assert.That(colony.Grid.Edifice[foot], Is.EqualTo(colony.Grid.Edifice[head]),
                "the record stands behind both slab cells");
            Assert.That(colony.Pawns.Items.Beds, Has.Member(head));
            Assert.That(colony.Construction.BedQualityAt(head), Is.EqualTo(QualityHandle.Normal),
                "the questions all work a storey up: what tier, and whose");
        }

        /// <summary>
        /// The control for the one above: the same order with no slab and no ground beneath either
        /// cell is refused, so the pass above is the slab and not thin air.
        /// </summary>
        [Test]
        public void ABedAStoreyUpWithNoFloorAtAllIsRefused()
        {
            ColonyWorld colony = Fresh();
            CellRef start = colony.Start;
            int head = Size.Index(start.X, start.Z, start.Y + 1);
            Assume.That(colony.Grid.IsSolidTerrain(head), Is.False);
            Assume.That(colony.Grid.HasFloor(head), Is.False,
                "air over air: nothing to stand on until a slab is laid");

            Assert.That(
                colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, 0),
                Is.EqualTo(IntentRejection.NotPermitted));
        }

        /// <summary>
        /// A bed under a floor - a slab laid over its head, the cell above the bed - changes
        /// nothing about ordering, raising, owning or being slept in. Nothing in the bed's rules
        /// looks up, and this is the test that says so on purpose rather than by silence.
        /// </summary>
        [Test]
        public void ABedCanBeOrderedAndOwnedUnderAFloorLaidOverIt()
        {
            ColonyWorld colony = Fresh();
            int head = OpenFootprint(colony, out int second);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));
            Assume.That(colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, 0),
                Is.EqualTo(IntentRejection.None));

            // The slab goes on after the order and before the raise, which is the awkward half of
            // the question: the world moved over the site while the wood was being fetched.
            LaySlab(colony, head + Size.LayerStride);
            colony.Construction.Raise(colony.Pawns, head, (byte)QualityHandle.Normal);

            var pawn = colony.Pawns.Pawns.All[0];
            Assert.That(Assign(colony, head, pawn.Id.Value), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.BedOwnerAt(head), Is.EqualTo(pawn.Id.Value),
                "a ceiling over the bed does not unmake it");
        }

        /// <summary>
        /// The whole use case on one board, a storey up and walked to: on the terraced meadow a
        /// bed stands on higher ground than the colony's start, its owner climbs the riser to it
        /// and sleeps there at the tier's own rate. Reachability is the terraced board's own - a
        /// one-block hop - because the built-stair line has not landed, and this test is the
        /// record of what a bed on a floor above costs today.
        /// </summary>
        [Test]
        public void AnOwnerClimbsToABedOnHigherGroundAndSleepsThere()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 0;
            ColonyWorld colony = ColonyWorld.Build(Size, Seed, scenario, barren: false, wooded: true);
            var owner = colony.Pawns.Pawns.All[0];

            // A footprint standing higher than the start: both cells floor themselves on the
            // terrace's own ground, which is what a floor above is when the floor is the land.
            int high = -1;
            for (int radius = 1; radius < 14 && high < 0; radius++)
            for (int dz = -radius; dz <= radius && high < 0; dz++)
            for (int dx = -radius; dx <= radius && high < 0; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = colony.Start.X + dx, z = colony.Start.Z + dz;
                for (int y = colony.Start.Y + 1; y < Size.SizeY - 1 && high < 0; y++)
                {
                    int head = Size.Index(x, z, y);
                    if (!colony.Construction.Allows(head)) continue;
                    int foot = EdificeFootprint.SecondCell(head, CoreContent.EdificeBed, 0, Size);
                    if (foot < 0 || !colony.Construction.Allows(foot)) continue;

                    // Higher than the start, and the walk to it exists: the chooser's own
                    // reachability is the oracle, asked once here rather than assumed.
                    if (colony.Pawns.Reachable(owner, head)) high = head;
                }
            }
            Assume.That(high, Is.GreaterThanOrEqualTo(0),
                "the terraced board has a reachable footprint above the start");

            RaiseABed(colony, high, (byte)QualityHandle.Epic);
            Assert.That(Assign(colony, high, owner.Id.Value), Is.EqualTo(IntentRejection.None));

            owner.Needs[NeedIndex.Rest] = 60;
            for (int i = 0; i < 8_000 && !owner.Asleep; i++) colony.World.Tick();
            Assert.That(owner.Asleep, Is.True, "the owner found somewhere to sleep");
            Assert.That(owner.Cell, Is.EqualTo(high),
                "the owner climbed to their own bed a storey up and slept in it");

            int before = owner.Needs[NeedIndex.Rest];
            colony.World.Tick(1_500);
            Assert.That(owner.Needs[NeedIndex.Rest] - before, Is.GreaterThan(70),
                "an Epic bed restores at 140 per cent, a storey up as on the ground");
        }

        /// <summary>
        /// The whole pipeline on one board, a storey up: the bed is ordered as a player orders
        /// it (an intent), the wood is carried up the terrace, the work is done, and the thing
        /// that lands is one record behind two cells finished at a rolled tier - the driver's own
        /// roll, which no other test reaches (the rest raise beds directly, bypassing the
        /// completion branch the way a unit test is allowed to and an end-to-end one is for).
        /// </summary>
        [Test]
        public void ABedAStoreyUpIsFedWorkedAndFinishedAtAQualityTier()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 0;
            ColonyWorld colony = ColonyWorld.Build(Size, Seed, scenario, barren: false, wooded: true);

            int high = -1;
            for (int radius = 1; radius < 14 && high < 0; radius++)
            for (int dz = -radius; dz <= radius && high < 0; dz++)
            for (int dx = -radius; dx <= radius && high < 0; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = colony.Start.X + dx, z = colony.Start.Z + dz;
                for (int y = colony.Start.Y + 1; y < Size.SizeY - 1 && high < 0; y++)
                {
                    int head = Size.Index(x, z, y);
                    if (!colony.Construction.Allows(head)) continue;
                    int foot = EdificeFootprint.SecondCell(head, CoreContent.EdificeBed, 0, Size);
                    if (foot < 0 || !colony.Construction.Allows(foot)) continue;
                    if (!colony.Pawns.Reachable(colony.Pawns.Pawns.All[0], head)) continue;

                    // Somewhere to stand and build it from, or nobody ever will - and a place
                    // nearby for the wood pile the delivery needs.
                    if (FellJobDriver.StandBeside(colony.Pawns, colony.Pawns.Pawns.All[0], head) >= 0) high = head;
                }
            }
            Assume.That(high, Is.GreaterThanOrEqualTo(0),
                "the terraced board has a buildable, reachable footprint above the start");

            int foot2 = EdificeFootprint.SecondCell(high, CoreContent.EdificeBed, 0, Size);
            int pile = colony.Pawns.Items.NearestCellWithSpace(
                colony.Grid, high, ItemIndex.Wood, 20, JobDriver.DropSearchRadius);
            Assume.That(pile, Is.GreaterThanOrEqualTo(0));
            colony.Pawns.Items.Spawn(ItemIndex.Wood, pile, 20);

            colony.World.Intents.Submit(new Intent(
                IntentKind.PlaceBuilding, Size.FromIndex(high), BuildingHandle.Bed, StuffHandle.Wood, 0));
            colony.World.Tick();
            Assume.That(colony.World.Intents.Rejected.Count, Is.EqualTo(0));

            bool raised = false;
            for (int tick = 0; tick < 30_000 && !raised; tick++)
            {
                colony.World.Tick();
                raised = colony.Grid.Edifice[high] >= 0;
            }

            Assert.That(raised, Is.True, "the bed went up a storey above the start");
            Assert.That(colony.Grid.Edifice[foot2], Is.EqualTo(colony.Grid.Edifice[high]));

            PlacedEdifice bed = colony.Outcome.Edifices[colony.Grid.Edifice[high]];
            Assert.That(bed.Def, Is.EqualTo(CoreContent.EdificeBed));
            Assert.That(bed.Quality, Is.InRange(1, 5),
                "the finisher rolled one of the five tiers - the success roll, landed by the driver");
            Assert.That(colony.Pawns.Items.Beds, Has.Member(high),
                "and the finished bed joined the list the sleep chooser scans");
        }

        [Test]
        public void AnOwnedBedSurvivesASaveAndItsOwnerWithIt()
        {
            ColonyWorld original = Fresh();
            var pawn = original.Pawns.Pawns.All[0];
            int head = OpenFootprint(original, out _);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));
            RaiseABed(original, head);
            Assert.That(Assign(original, head, pawn.Id.Value), Is.EqualTo(IntentRejection.None));

            ColonyWorld restored = Fresh();
            restored.Load(original.Save());

            Assert.That(restored.Construction.BedOwnerAt(head), Is.EqualTo(pawn.Id.Value),
                "ownership rides the edifice list into the save and back");
            Assert.That(restored.Construction.BedQualityAt(head), Is.EqualTo(QualityHandle.Normal),
                "and so does the tier the finisher rolled");
        }
    }
}
