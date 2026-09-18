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
        /// Is this cell free of anybody's claim on it?
        ///
        /// <para><b>Asked because a bed on a reserved cell is a bed nobody can sleep in</b> —
        /// including its owner. <c>TrySleep</c> checks the reservation before it checks whose bed
        /// it is, so a hauler that has claimed the cell as a destination locks the owner out of
        /// their own bed and they lie down on the ground instead. Measured, not supposed: on the
        /// bare board all three colonists start out hauling, and the first pair of free footprints
        /// near the start are cells two of them have already claimed.</para>
        /// </summary>
        static bool Unclaimed(ColonyWorld colony, int cell) =>
            colony.Pawns.Reservations.CanReserve(
                colony.Pawns.Pawns.All[0].Id, ReservationManager.Key(ReservationTargetKind.Cell, cell));

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
                if (!colony.Construction.Allows(head, BuildingHandle.Bed)) continue;
                if (!Unclaimed(colony, head)) continue;

                int foot = EdificeFootprint.SecondCell(head, CoreContent.EdificeBed, 0, Size);
                if (foot >= 0 && Unclaimed(colony, foot)
                    && colony.Construction.Allows(foot, BuildingHandle.Bed))
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
        public void ABedOrderIntoSolidGroundIsLiftedExactlyAsAWallIs()
        {
            ColonyWorld colony = Fresh();

            // A solid cell with open air above it: exactly the cell a wall order names when it is
            // lifted by StandingOn, and exactly the cell the picker answers a click on grass with.
            // A bed is lifted the same way — see StandingOn for why refusing it, as the first cut
            // did, made the bed unorderable by pointing at anything.
            CellRef start = colony.Start;
            for (int radius = 1; radius < 8; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!Size.Contains(x, z, start.Y)) continue;

                // start.Y is the layer a colonist STANDS in, so the ground is the layer below it.
                // Searching at start.Y found nothing solid anywhere and the test ignored itself
                // on every run, which is how the rule below went unverified.
                if (start.Y == 0) continue;
                int index = Size.Index(x, z, start.Y - 1);
                if (!colony.Grid.IsSolidTerrain(index)) continue;
                int above = index + Size.LayerStride;
                if (above >= Size.CellCount) continue;
                if (!colony.Construction.Allows(above, BuildingHandle.Bed)) continue;

                // And the far cell, because a bed needs both and this test is about the lift
                // rather than about finding the one cell in the meadow that is short of room.
                int far = EdificeFootprint.SecondCell(above, CoreContent.EdificeBed, 0, Size);
                if (far < 0 || !colony.Construction.Allows(far, BuildingHandle.Bed)) continue;

                Assert.That(
                    colony.Construction.Place(Size.FromIndex(index), BuildingHandle.Bed, StuffHandle.Wood, 0),
                    Is.EqualTo(IntentRejection.None),
                    "a bed named at the ground is lifted onto it, or no bed can be ordered by "
                    + "pointing at grass — which is every bed a player will ever order");

                Assert.That(colony.Construction.At(above), Is.EqualTo(BuildingHandle.Bed),
                    "and it landed in the air cell, not in the block");

                // Both ends on one layer is the whole of why the lift is safe: the far cell is
                // derived from the head AFTER the lift, so a bed can never straddle two layers.
                int foot = EdificeFootprint.SecondCell(above, CoreContent.EdificeBed, 0, Size);
                Assert.That(foot, Is.GreaterThanOrEqualTo(0));
                Assert.That(Size.FromIndex(foot).Y, Is.EqualTo(Size.FromIndex(above).Y),
                    "both ends of the bed are on one layer");
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
        static int AnotherOpenFootprint(ColonyWorld colony, int notHead, int notFoot, out int second)
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
                if (head == notHead || head == notFoot) continue;
                if (!colony.Construction.Allows(head, BuildingHandle.Bed)) continue;
                if (!Unclaimed(colony, head)) continue;

                int foot = EdificeFootprint.SecondCell(head, CoreContent.EdificeBed, 0, Size);
                if (foot == notHead || foot == notFoot) continue;
                if (foot >= 0 && !Unclaimed(colony, foot)) continue;

                // Both cells of the first bed are excluded, not just its head. They are searched
                // before either is raised, so an overlap is not refused by the grid — the second
                // bed would simply be built in the first one's foot cell and the two tests about
                // whose bed is whose would be about one bed.
                if (foot >= 0 && colony.Construction.Allows(foot, BuildingHandle.Bed))
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
            // Not Is.AnyOf: Unity's NUnit is older than the fast tier's and has no such member,
            // which is a compile error and so aborts the whole batch (docs/lessons.md).
            Assume.That(placed,
                Is.EqualTo(IntentRejection.None).Or.EqualTo(IntentRejection.AlreadyInThatState),
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

        /// <summary>
        /// <b>Three colonists and three beds: everybody gets one.</b>
        ///
        /// <para>The owner's photograph after the first round of fixes: two colonists lying
        /// properly on their pillows and a third asleep on the grass beside an empty bed
        /// (2026-09-18). Nothing in the fixtures asked the obvious question — that a colony with a
        /// bed each puts everybody in one — because every ownership test used two.</para>
        /// </summary>
        [Test]
        public void EveryColonistWithABedToThemselvesSleepsInOne()
        {
            ColonyWorld colony = Fresh();

            var heads = new System.Collections.Generic.List<int>();
            int excludeHead = -1, excludeFoot = -1;
            for (int i = 0; i < 3; i++)
            {
                int head = heads.Count == 0
                    ? OpenFootprint(colony, out int foot)
                    : AnotherOpenFootprint(colony, excludeHead, excludeFoot, out foot);
                Assume.That(head, Is.GreaterThanOrEqualTo(0), $"no room for bed {i + 1}");

                RaiseABed(colony, head);
                heads.Add(head);
                excludeHead = head;
                excludeFoot = foot;
            }

            foreach (var pawn in colony.Pawns.Pawns.All) pawn.Needs[NeedIndex.Rest] = 40;

            for (int i = 0; i < 20_000; i++)
            {
                colony.World.Tick();
                bool all = true;
                foreach (var p in colony.Pawns.Pawns.All) if (!p.Asleep) all = false;
                if (all) break;
            }

            foreach (var pawn in colony.Pawns.Pawns.All)
            {
                Assert.That(pawn.Asleep, Is.True, "a colonist never got to sleep at all");
                Assert.That(heads, Does.Contain(pawn.Cell),
                    "a colonist slept somewhere that is not one of the three beds");
            }

            var used = new System.Collections.Generic.HashSet<int>();
            foreach (var pawn in colony.Pawns.Pawns.All) used.Add(pawn.Cell);
            Assert.That(used.Count, Is.EqualTo(3), "two colonists ended up in the same bed");
        }

        /// <summary>
        /// <b>Nothing can be put down on a bed, or on a bed that is still being built.</b>
        ///
        /// <para>Refusing the <i>order</i> on a cell that holds something was only the first half,
        /// and the owner's second photograph is the second half: a bed built with a log through it
        /// (2026-09-18). The order was placed on a clear cell, a colonist took a while to build it,
        /// and in between a hauler put a log down where the bed was going. So a site holds its
        /// cells from the moment it is ordered, not from the moment it stands.</para>
        /// </summary>
        [Test]
        public void ABedHoldsItsCellsAgainstItemsFromTheOrderNotFromTheRaise()
        {
            ColonyWorld colony = Fresh();
            int head = OpenFootprint(colony, out int foot);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));

            var items = colony.Pawns.Items;
            Assume.That(items.CellHasSpace(head), Is.True, "the cell starts empty");
            Assume.That(items.CellHasSpace(foot), Is.True);

            // Ordered, and not yet built.
            Assume.That(colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, 0),
                Is.EqualTo(IntentRejection.None));

            Assert.That(items.CellHasSpace(head), Is.False,
                "a waiting bed site must not be a place a hauler can put a log down");
            Assert.That(items.CellHasSpace(foot), Is.False, "and neither must its far cell");

            // Built: still held.
            colony.Construction.Raise(colony.Pawns, head, (byte)QualityHandle.Normal);
            Assert.That(items.CellHasSpace(head), Is.False, "a standing bed holds its cells too");
            Assert.That(items.CellHasSpace(foot), Is.False);

            // And a nearby cell is unaffected, so this is a hold and not a blanket ban.
            int elsewhere = AnotherOpenFootprint(colony, head, foot, out _);
            Assume.That(elsewhere, Is.GreaterThanOrEqualTo(0));
            Assert.That(items.CellHasSpace(elsewhere), Is.True, "only the bed's own cells are held");
        }

        /// <summary>A cancelled order gives its cells back; the ground is ordinary again.</summary>
        [Test]
        public void ACancelledBedGivesItsCellsBack()
        {
            ColonyWorld colony = Fresh();
            int head = OpenFootprint(colony, out int foot);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));

            Assume.That(colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, 0),
                Is.EqualTo(IntentRejection.None));
            Assume.That(colony.Pawns.Items.CellHasSpace(head), Is.False);

            Assert.That(colony.Construction.Cancel(Size.FromIndex(head)), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Pawns.Items.CellHasSpace(head), Is.True, "the head cell is ground again");
            Assert.That(colony.Pawns.Items.CellHasSpace(foot), Is.True, "and so is the far one");
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

        /// <summary>
        /// <b>A bed given an owner on a paused world has that owner, without a tick being spent.</b>
        ///
        /// <para>The pane that offers the choice is a thing you open while paused — pausing to plan
        /// is the genre's central interaction — so an assignment that sits in the queue leaves the
        /// popover picked and the row still reading "nobody" until the player presses play. That is
        /// the slab fault of the same day told again (<c>PausedIntents</c>), and it is why
        /// <c>AssignBedOwner</c> is on that list.</para>
        ///
        /// <para>Not a tick is spent here, deliberately: ticking would pass whether the intent were
        /// on the paused list or not.</para>
        /// </summary>
        [Test]
        public void ABedGivenAnOwnerOnAPausedWorldHasOneWithoutATick()
        {
            ColonyWorld colony = Fresh();
            var pawn = colony.Pawns.Pawns.All[0];
            int head = OpenFootprint(colony, out _);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));
            RaiseABed(colony, head);

            int before = colony.World.CurrentTick;
            colony.World.Intents.Submit(new Intent(
                IntentKind.AssignBedOwner, Size.FromIndex(head), pawn.Id.Value));
            colony.World.RepublishViews();

            Assert.That(colony.World.CurrentTick, Is.EqualTo(before), "a paused world spent a tick");
            Assert.That(colony.Construction.BedOwnerAt(head), Is.EqualTo(pawn.Id.Value),
                "the pick was taken, and did not wait for the clock to start");
        }

        [Test]
        public void AColonistWhoTakesANewBedReleasesTheOldOne()
        {
            ColonyWorld colony = Fresh();
            var pawn = colony.Pawns.Pawns.All[0];
            int first = OpenFootprint(colony, out int firstFoot);
            int secondBed = AnotherOpenFootprint(colony, first, firstFoot, out _);
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
            int far = AnotherOpenFootprint(colony, near, nearFoot, out int farFoot);
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
                    if (!colony.Construction.Allows(head, BuildingHandle.Bed)) continue;
                    int foot = EdificeFootprint.SecondCell(head, CoreContent.EdificeBed, 0, Size);
                    if (foot < 0 || !colony.Construction.Allows(foot, BuildingHandle.Bed)) continue;

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
                    if (!colony.Construction.Allows(head, BuildingHandle.Bed)) continue;
                    int foot = EdificeFootprint.SecondCell(head, CoreContent.EdificeBed, 0, Size);
                    if (foot < 0 || !colony.Construction.Allows(foot, BuildingHandle.Bed)) continue;
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

        /// <summary>
        /// **A two-cell thing may not be ordered into anything standing in its far cell**, for
        /// every facing, and this is the test that says so out loud.
        ///
        /// <para>Written to answer an owner report of a bed built through a wall (2026-09-18). The
        /// rule was already there and already worked — the far cell is derived from the facing and
        /// validated at the order, and all four facings refuse — so this pins it rather than fixing
        /// it. Worth keeping for that reason: a guard nothing tests is a guard that can be tidied
        /// away, and the check happens before the head cell's own, which is an order that looks
        /// arbitrary until you need it.</para>
        /// </summary>
        [Test]
        public void ABedIsRefusedWhenItsFarCellIsAWall()
        {
            for (int facing = 0; facing < 4; facing++)
            {
                ColonyWorld colony = Fresh();
                int head = OpenFootprint(colony, out _);
                Assume.That(head, Is.GreaterThanOrEqualTo(0));

                int second = EdificeFootprint.SecondCell(head, CoreContent.EdificeBed, facing, Size);
                if (second < 0 || !colony.Construction.Allows(second, BuildingHandle.Wall)) continue;

                Assume.That(colony.Construction.Place(
                    Size.FromIndex(second), BuildingHandle.Wall, StuffHandle.Wood),
                    Is.EqualTo(IntentRejection.None));
                colony.Construction.Raise(colony.Pawns, second);

                IntentRejection got = colony.Construction.Place(
                    Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, facing);
                
                Assert.That(got, Is.EqualTo(IntentRejection.NotPermitted),
                    $"facing {facing}: a bed whose far cell is a wall must be refused");
            }
        }

        /// <summary>
        /// **The facing the player chose has to reach the record**, and for a long time it did not.
        ///
        /// <para><c>Raise</c> derived the far cell from <c>_facing[cell]</c>, then called
        /// <c>Clear(cell)</c> — which zeroes the site, facing included — and then read the facing
        /// <i>again</i> for the record, out of the slot it had just wiped. Every rotatable thing was
        /// built facing north whatever was ordered.</para>
        ///
        /// <para><b>It hid because only the drawing was wrong.</b> The cells were derived before the
        /// clear and were always right, so the footprint guard still worked, nothing was ever built
        /// somewhere illegal, and no simulation test could see it. Even
        /// <see cref="ABedsFacingIsInTheStateHash"/> passed — on the difference between the two
        /// beds' <em>cells</em> rather than on the facings it was written to pin. What the owner
        /// saw was a bed ghost turned one way and a built bed at a quarter turn to it, lying
        /// through a wall it did not occupy (2026-09-18).</para>
        ///
        /// <para>Every facing, and that matters: north is 0 and a lost facing is also 0, so a test
        /// that only tried north would have passed throughout.</para>
        /// </summary>
        [Test]
        public void TheFacingItWasPlacedAtReachesTheRecord()
        {
            int tried = 0;
            for (int facing = 0; facing < 4; facing++)
            {
                ColonyWorld colony = Fresh();
                int head = OpenFootprint(colony, out _);
                Assume.That(head, Is.GreaterThanOrEqualTo(0));

                int second = EdificeFootprint.SecondCell(head, CoreContent.EdificeBed, facing, Size);
                if (second < 0 || !colony.Construction.Allows(second, BuildingHandle.Bed)) continue;

                Assume.That(colony.Construction.Place(
                    Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, facing),
                    Is.EqualTo(IntentRejection.None), $"the order for facing {facing} was refused");

                colony.Construction.Raise(colony.Pawns, head);
                PlacedEdifice placed = colony.Outcome.Edifices[colony.Grid.Edifice[head]];

                Assert.That((int)placed.Facing, Is.EqualTo(facing),
                    $"a bed ordered facing {facing} was built facing {placed.Facing}");
                Assert.That(colony.Grid.Edifice[second], Is.EqualTo(colony.Grid.Edifice[head]),
                    "and the far cell it claimed must be the one that facing derives");
                tried++;
            }

            Assert.That(tried, Is.GreaterThan(1),
                "the board gave only one usable facing, so this proved nothing");
        }
    }
}
