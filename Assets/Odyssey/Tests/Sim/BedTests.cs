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

        static void RaiseABed(ColonyWorld colony, int head) => colony.Construction.Raise(
            colony.Pawns, head, (byte)QualityHandle.Normal);

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

            Assume.That(Assign(colony, first, pawn.Id.Value), Is.EqualTo(IntentRejection.None));
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
            Assume.That(Assign(colony, far, owner.Id.Value), Is.EqualTo(IntentRejection.None));

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

        [Test]
        public void AnOwnedBedSurvivesASaveAndItsOwnerWithIt()
        {
            ColonyWorld original = Fresh();
            var pawn = original.Pawns.Pawns.All[0];
            int head = OpenFootprint(original, out _);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));
            RaiseABed(original, head);
            Assume.That(Assign(original, head, pawn.Id.Value), Is.EqualTo(IntentRejection.None));

            ColonyWorld restored = Fresh();
            restored.Load(original.Save());

            Assert.That(restored.Construction.BedOwnerAt(head), Is.EqualTo(pawn.Id.Value),
                "ownership rides the edifice list into the save and back");
            Assert.That(restored.Construction.BedQualityAt(head), Is.EqualTo(QualityHandle.Normal),
                "and so does the tier the finisher rolled");
        }
    }
}
