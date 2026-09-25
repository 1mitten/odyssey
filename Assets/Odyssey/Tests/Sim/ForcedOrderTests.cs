#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Forced orders: the player naming one colonist, one job and one target, and the scan being
    /// overruled for exactly that long.
    ///
    /// <para><b>The claim is "past the nearer one", and nothing less will do.</b> A test that only
    /// asserted the colonist started building would pass with the whole feature deleted, because
    /// the work scan would have started a build anyway. So every test here that says a forced job
    /// was taken has a <em>second site nearer the colonist</em> standing in it as the control, and
    /// <see cref="TheScanChoosesTheNearerSiteWhenNobodyForcesAnything"/> is the measurement that the
    /// control really is the one the colonist would otherwise have picked.</para>
    ///
    /// <para>The board is bare rather than wooded: a forced build is being timed against an
    /// unforced one, and a colonist that stops to fell a tree is a colonist whose reasons are no
    /// longer only the ones under test.</para>
    /// </summary>
    public class ForcedOrderTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Board(int colonists = 1, uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = colonists;
            scenario.startingFellRadius = 0;
            // About which site is built first, not about who does it: no traits, so a colonist dealt Ham-fisted or
            // Tireless by her seed cannot make this test about traits (design 44 §5f).
            scenario.traits = false;
            ColonyWorld colony = ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: false);
            // A forced-order test times one build against another, and a botch would put a thumb
            // on the scales: the nearer wall could fail for reasons nothing here is about. The
            // builders in these tests never botch; the roll has its own tests in
            // `ConstructionTests`.
            //
            // The element is replaced rather than written through, because the Defs a record's
            // arrays point at are shared by every record in the process (ContentPack's rule) —
            // writing `…WorkTypes[i].successBasePerMille = 1_000` would retune every other test
            // that ran after this one.
            WorkTypeDef shipped = colony.Pawns.Content.WorkTypes[WorkTypeIndex.Construction];
            colony.Pawns.Content.WorkTypes[WorkTypeIndex.Construction] = new WorkTypeDef
            {
                defName = shipped.defName,
                label = shipped.label,
                order = shipped.order,
                successBasePerMille = 1_000,
                successSlopePerLevel = 0,
            };
            return colony;
        }

        static Pawn TheColonist(ColonyWorld colony) => colony.Pawns.Pawns.All[0];

        /// <summary>
        /// A cell exactly <paramref name="radius"/> cells out from the colonist that a wall could
        /// legally stand in and be worked at, or -1. The ring rather than the disc, so that "near"
        /// and "far" are distances and not accidents of scan order.
        /// </summary>
        static int SiteAtRange(ColonyWorld colony, Pawn pawn, int radius)
        {
            CellRef at = Size.FromIndex(pawn.Cell);
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = at.X + dx, z = at.Z + dz;
                if (!Size.Contains(x, z, at.Y)) continue;

                int cell = Size.Index(x, z, at.Y);
                if (!colony.Construction.Allows(cell)) continue;
                if (FellJobDriver.StandBeside(colony.Pawns, pawn, cell) < 0) continue;
                return cell;
            }

            return -1;
        }

        /// <summary>An ordered wall with all five units of wood already in it: a frame, ready to work.</summary>
        static void Frame(ColonyWorld colony, int cell)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Wall, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None));
            colony.Construction.Deliver(cell, 5);
            Assume.That(colony.Construction.IsFrame(cell), Is.True);
        }

        /// <summary>
        /// Submit a forced order the way the interface will and let the world consume it. Returns
        /// the rejection, or <see cref="IntentRejection.None"/> when it was applied.
        /// </summary>
        static IntentRejection Force(ColonyWorld colony, Pawn pawn, int cell, int job = JobIndex.Build)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(new Intent(
                IntentKind.ForceJob, Size.FromIndex(cell), job, pawn.Id.Value));
            colony.World.Tick();

            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        static long CellKey(int cell) => ReservationManager.Key(ReservationTargetKind.Cell, cell);

        /// <summary>
        /// Wall a site and the one cell beside it off from the rest of the board: a site a colonist
        /// can see and can never get to.
        ///
        /// <para>Two cells rather than one, deliberately. Sealing the site alone would leave it with
        /// no walkable neighbour at all, which is "nowhere to stand" — a different refusal wearing
        /// the same answer. With a stance inside the pocket the only thing wrong is that nobody can
        /// walk to it, which is the case this fixture is for.</para>
        ///
        /// <para>The layer above is sealed too, or the pocket is open from the roof: a colonist
        /// walking over the wall would be one block up from the stance, and one block down is a
        /// drop the mover prices and the planner allows.</para>
        /// </summary>
        static void SealOff(ColonyWorld colony, int site, int stance)
        {
            CellRef at = Size.FromIndex(site);
            for (int dy = 0; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 2; dx++)
            {
                int x = at.X + dx, z = at.Z + dz, y = at.Y + dy;
                if (!Size.Contains(x, z, y)) continue;

                int cell = Size.Index(x, z, y);
                if (cell == site || cell == stance) continue;

                // The flag is what walkability reads; the terrain def is what it is drawn as, and
                // nothing here draws anything.
                colony.Grid.Flags[cell] |= CellFlags.SolidTerrain;

                // Every edit says so, exactly as mining and building do. The rebuild below walks
                // the dirty blocks and nothing else, so an unannounced edit leaves the region graph
                // certain the wall is not there — which is a silently reachable pocket and a test
                // that proves nothing.
                colony.Pawns.Nav.MarkDirty(cell);
            }

            // Support and the region graph are derived, so they have to be recomputed: a pocket
            // nobody can reach is a district of its own, and districts are what reachability reads.
            colony.RebuildDerived();
        }

        // ---- the composition ---------------------------------------------------------------

        /// <summary>
        /// The command reaches the simulation at all. <c>ConstructionTests</c> asserts this over
        /// every intent kind there is; this is the one-line version that names the new one, so a
        /// handler that goes missing says which command stopped working.
        /// </summary>
        [Test]
        public void TheColonyAnswersAForcedOrder()
        {
            ColonyWorld colony = Board();
            Assert.That(colony.World.HandlesIntent(IntentKind.ForceJob), Is.True);
        }

        // ---- the claim, and its control ------------------------------------------------------

        /// <summary>
        /// The control that gives the test below its meaning: unforced, this colonist takes the
        /// <em>nearer</em> of two identical sites. Without this, "the forced colonist built the far
        /// one" could be the scan's own choice and the feature could be doing nothing.
        /// </summary>
        [Test]
        public void TheScanChoosesTheNearerSiteWhenNobodyForcesAnything()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);

            int near = SiteAtRange(colony, pawn, 2);
            int far = SiteAtRange(colony, pawn, 12);
            Assume.That(near, Is.GreaterThanOrEqualTo(0));
            Assume.That(far, Is.GreaterThanOrEqualTo(0));
            Assume.That(colony.Pawns.Distance(pawn.Cell, far),
                Is.GreaterThan(colony.Pawns.Distance(pawn.Cell, near)), "the far site is the far one");

            Frame(colony, near);
            Frame(colony, far);
            colony.World.Tick();

            Assert.That(pawn.CurrentJob, Is.Not.Null, "the scan offered it a wall to build");
            Assert.That(pawn.CurrentJob!.DefIndex, Is.EqualTo(JobIndex.Build));
            Assert.That(pawn.CurrentJob.DestCell, Is.EqualTo(near), "the nearer site is the one it chose");
            Assert.That(pawn.CurrentJob.PlayerForced, Is.False, "nobody forced anything");
        }

        /// <summary>
        /// The whole claim of this unit: a forced order sends the named colonist to the target the
        /// player named, past the site it had just been proved to prefer, and the far wall is the
        /// one that goes up first.
        /// </summary>
        [Test]
        public void AForcedOrderSendsTheColonistPastTheNearerSite()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);

            int near = SiteAtRange(colony, pawn, 2);
            int far = SiteAtRange(colony, pawn, 12);
            Assume.That(near, Is.GreaterThanOrEqualTo(0));
            Assume.That(far, Is.GreaterThanOrEqualTo(0));

            Frame(colony, near);
            Frame(colony, far);

            Assert.That(Force(colony, pawn, far), Is.EqualTo(IntentRejection.None));

            Assert.That(pawn.CurrentJob, Is.Not.Null);
            Assert.That(pawn.CurrentJob!.DefIndex, Is.EqualTo(JobIndex.Build));
            Assert.That(pawn.CurrentJob.DestCell, Is.EqualTo(far), "the site the player named");
            Assert.That(pawn.CurrentJob.PlayerForced, Is.True, "and it knows it was told to");
            Assert.That(colony.Pawns.Reservations.IsReservedBy(pawn.Id, CellKey(far)), Is.True,
                "the claim is taken there and then, rather than waiting for a scan that is not coming");

            // Not "a wall went up" — which one, and in which order. The colonist builds the near
            // one afterwards, of its own accord, and that is the point: the order was overruled for
            // one job and not for ever.
            int farAt = -1, nearAt = -1;
            for (int tick = 0; tick < 20_000 && farAt < 0; tick++)
            {
                colony.World.Tick();
                if (farAt < 0 && colony.Grid.Edifice[far] >= 0) farAt = tick;
                if (nearAt < 0 && colony.Grid.Edifice[near] >= 0) nearAt = tick;
            }

            Assert.That(farAt, Is.GreaterThanOrEqualTo(0), "the wall the player pointed at was built");
            Assert.That(nearAt, Is.LessThan(0), "and the nearer one was still standing when it was");
        }

        // ---- a forced job is an ordinary job, and fails like one --------------------------------

        /// <summary>
        /// A forced job whose site is cancelled under it fails and gives back what it held. Forcing
        /// buys a place in the queue, not an exemption from the world changing.
        /// </summary>
        [Test]
        public void AForcedJobThatBecomesIllegalFailsAndReleasesItsClaim()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);

            int site = SiteAtRange(colony, pawn, 6);
            Assume.That(site, Is.GreaterThanOrEqualTo(0));
            Frame(colony, site);

            Assert.That(Force(colony, pawn, site), Is.EqualTo(IntentRejection.None));
            Assume.That(colony.Pawns.Reservations.IsReservedBy(pawn.Id, CellKey(site)), Is.True);

            colony.World.Intents.Submit(new Intent(IntentKind.CancelBuilding, Size.FromIndex(site)));
            colony.World.Tick();

            Assert.That(pawn.CurrentJob?.DestCell, Is.Not.EqualTo(site), "it is not still swinging at nothing");
            Assert.That(colony.Jobs.FailedOf(JobIndex.Build), Is.EqualTo(1), "it failed, like any other job");

            // The claim, not merely the job: a released job that kept its reservation is the leak a
            // ten-day run surfaces and a two-minute test does not.
            Assert.That(colony.Pawns.Reservations.IsReservedByAnyone(CellKey(site)), Is.False,
                "the cell is nobody's again");
        }

        // ---- the legality query: three refusals, each on its own ---------------------------------

        [Test]
        public void ASiteTheColonistCannotReachCannotBeForced()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);

            int site = SiteAtRange(colony, pawn, 10);
            Assume.That(site, Is.GreaterThanOrEqualTo(0));
            int stance = site + 1;
            Assert.That(colony.Grid.IsWalkable(stance), Is.True, "the pocket has somewhere to stand in it");

            Frame(colony, site);
            SealOff(colony, site, stance);
            Assert.That(colony.Pawns.Reachable(pawn, stance), Is.False, "the pocket really is sealed");
            Assert.That(colony.Construction.IsFrame(site), Is.True, "and the site is otherwise ready");

            Assert.That(JobSystem.CanForce(pawn, colony.Pawns, JobIndex.Build, site), Is.False);
            AssertNothingWasTakenOrStarted(colony, pawn, site);

            Assert.That(Force(colony, pawn, site), Is.EqualTo(IntentRejection.NotPermitted),
                "and the command says so rather than doing nothing in silence");
            Assert.That(colony.Pawns.Reservations.IsReservedByAnyone(CellKey(site)), Is.False);
        }

        [Test]
        public void ASiteAnotherColonistHasClaimedCannotBeForced()
        {
            ColonyWorld colony = Board(colonists: 2);
            Pawn pawn = TheColonist(colony);
            Pawn other = colony.Pawns.Pawns.All[1];

            int site = SiteAtRange(colony, pawn, 6);
            Assume.That(site, Is.GreaterThanOrEqualTo(0));
            Frame(colony, site);

            Assume.That(colony.Pawns.Reservations.Reserve(other.Id, CellKey(site)), Is.True);
            other.HeldReservations.Add(CellKey(site));

            Assert.That(JobSystem.CanForce(pawn, colony.Pawns, JobIndex.Build, site), Is.False);
            Assert.That(colony.Pawns.Reservations.IsReservedBy(pawn.Id, CellKey(site)), Is.False,
                "and asking did not take it off the colonist who had it");
            Assert.That(pawn.CurrentJob, Is.Null);

            Assert.That(Force(colony, pawn, site), Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(colony.Pawns.Reservations.IsReservedBy(other.Id, CellKey(site)), Is.True,
                "the claim is still the colonist's who took it");
            Assert.That(pawn.CurrentJob?.DestCell, Is.Not.EqualTo(site));
        }

        [Test]
        public void ACellWithNoSiteOnItCannotBeForced()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);

            int empty = SiteAtRange(colony, pawn, 6);
            Assume.That(empty, Is.GreaterThanOrEqualTo(0));
            Assume.That(colony.Construction.At(empty), Is.EqualTo(BuildingHandle.None),
                "a cell a wall could stand in, with no order on it");

            Assert.That(JobSystem.CanForce(pawn, colony.Pawns, JobIndex.Build, empty), Is.False);
            AssertNothingWasTakenOrStarted(colony, pawn, empty);

            Assert.That(Force(colony, pawn, empty), Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(colony.Pawns.Reservations.IsReservedByAnyone(CellKey(empty)), Is.False);
            Assert.That(pawn.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.Build));
        }

        /// <summary>
        /// A job nobody has decided the forced meaning of is refused rather than half-supported.
        /// Hauling is the example because it is the one most obviously wanted next, and because its
        /// forced form needs a second target — which load — that this intent cannot carry.
        /// </summary>
        [Test]
        public void OnlyBuildingCanBeForcedToday()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);

            int site = SiteAtRange(colony, pawn, 6);
            Assume.That(site, Is.GreaterThanOrEqualTo(0));
            Frame(colony, site);

            Assert.That(JobSystem.CanForce(pawn, colony.Pawns, JobIndex.Haul, site), Is.False);
            Assert.That(Force(colony, pawn, site, JobIndex.Haul), Is.EqualTo(IntentRejection.NotPermitted));

            // The scan may perfectly well have offered that same wall in the same tick, and should
            // have: a refused command changes nothing, and "changes nothing" includes not stopping
            // the colony working. What must not exist is a *forced* job.
            Assert.That(pawn.CurrentJob?.PlayerForced ?? false, Is.False, "nothing was forced onto it");
        }

        // ---- the query is a question, not an instruction ------------------------------------------

        /// <summary>
        /// <b>The split this unit exists for.</b> Asking whether a colonist could build something
        /// must cost the world nothing — no claim, no job, no field moved — because the context
        /// menu and the A10 command grid have to ask it for every command they are about to draw,
        /// including the ones the player never picks.
        /// </summary>
        [Test]
        public void AskingWhetherAColonistCouldBuildChangesNothing()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);

            int site = SiteAtRange(colony, pawn, 6);
            Assume.That(site, Is.GreaterThanOrEqualTo(0));
            Frame(colony, site);

            ReservationManager claims = colony.Pawns.Reservations;
            int activeBefore = claims.ActiveClaims;
            int targetsBefore = claims.ClaimedTargets;
            ulong hashBefore = colony.World.ComputeStateHash().Value;

            // Twice, because a query that claimed on the first call would answer itself no on the
            // second and the difference would be the only visible symptom.
            Assert.That(JobSystem.CanForce(pawn, colony.Pawns, JobIndex.Build, site, out int stand), Is.True);
            Assert.That(JobSystem.CanForce(pawn, colony.Pawns, JobIndex.Build, site), Is.True);

            Assert.That(stand, Is.GreaterThanOrEqualTo(0), "it says where the colonist would stand");
            Assert.That(claims.ActiveClaims, Is.EqualTo(activeBefore), "nothing was claimed");
            Assert.That(claims.ClaimedTargets, Is.EqualTo(targetsBefore));
            Assert.That(claims.IsReservedByAnyone(CellKey(site)), Is.False);
            Assert.That(pawn.CurrentJob, Is.Null, "and nothing was started");
            Assert.That(colony.World.ComputeStateHash().Value, Is.EqualTo(hashBefore),
                "the world is the same world it was before the question");
        }

        static void AssertNothingWasTakenOrStarted(ColonyWorld colony, Pawn pawn, int cell)
        {
            Assert.That(colony.Pawns.Reservations.IsReservedByAnyone(CellKey(cell)), Is.False,
                "a refusal claims nothing");
            Assert.That(pawn.CurrentJob, Is.Null, "and starts nothing");
        }
    }
}
