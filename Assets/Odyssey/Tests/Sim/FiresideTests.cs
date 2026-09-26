#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Colonists are drawn to a fire: a bedless sleeper lies beside one, and an idler drifts to
    /// one instead of wandering (design 33 §2, owner 2026-09-23).
    ///
    /// <para><b>Why this is worth more than the picture.</b> Rest recovers at the bed's own
    /// effectiveness scaled by the air the sleeper is in (design 28 §8), and radiance makes the
    /// ring round a fire warmer than the field (design 32). On a cold night that is the difference
    /// between the band where hypothermia builds while you sleep and the band where it does not —
    /// so "sleeps by the fire" is a survival behaviour, not set dressing.</para>
    /// </summary>
    public class FiresideTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 8);

        static ColonyWorld Board(int colonists, int beds)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = beds;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, 1u, scenario, barren: true, wooded: false);
        }

        /// <summary>Raise a campfire somewhere that will take one, and hand back its cell.</summary>
        static int Fire(ColonyWorld colony, int x, int z)
        {
            for (int step = 0; step < 64; step++)
            {
                int cx = x + step % 8;
                int cz = z + step / 8;
                if (cx >= Size.SizeX - 2 || cz >= Size.SizeZ - 2) continue;

                int air = colony.Grid.NearestWalkableInColumn(cx, cz, Size.SizeY - 2);
                if (air < 0) continue;

                CellRef at = Size.FromIndex(air);
                if (colony.Construction.Place(at, BuildingHandle.Campfire, StuffHandle.Wood)
                    != IntentRejection.None) continue;

                colony.Construction.RaiseWhenClear(colony.Pawns, air, BuildingHandle.Campfire);
                colony.World.Tick();
                return air;
            }

            Assert.Fail("no column on this board would take a campfire");
            return -1;
        }

        static bool Adjacent(int a, int b)
        {
            CellRef p = Size.FromIndex(a);
            CellRef q = Size.FromIndex(b);
            if (p.Y != q.Y) return false;
            int dx = p.X > q.X ? p.X - q.X : q.X - p.X;
            int dz = p.Z > q.Z ? p.Z - q.Z : q.Z - p.Z;
            return dx <= 1 && dz <= 1 && (dx | dz) != 0;
        }

        /// <summary>
        /// A colonist with no bed and a fire on the board is sent to lie beside the fire, rather
        /// than where she happens to be standing.
        /// </summary>
        [Test]
        public void ABedlessSleeperIsSentToTheFireside()
        {
            ColonyWorld colony = Board(colonists: 1, beds: 0);
            int fire = Fire(colony, 12, 12);

            // A pass, so the thermal system has gathered the heat sources the chooser reads.
            colony.World.Tick(Odyssey.Sim.Temperature.TemperatureSystem.IntervalTicks * 2);

            Pawn pawn = colony.Pawns.Pawns.All[0];
            var job = new Job();

            Assert.That(CriticalNeedsThinkNode.TrySleep(pawn, colony.Pawns, job), Is.True);
            Assert.That(job.TargetCell, Is.Not.EqualTo(-1),
                "a bedless colonist was told to lie where she stood with a fire on the board");
            Assert.That(Adjacent(job.TargetCell, fire), Is.True,
                $"she was sent to {job.TargetCell}, which is not beside the fire at {fire}");
        }

        /// <summary>
        /// And never <b>into</b> the fire. A campfire is blocking and wants a clear cell, so its
        /// own tile is the one nobody can occupy — which is also the right picture: people sit
        /// round a fire, not in it.
        /// </summary>
        [Test]
        public void SheIsNeverSentIntoTheFireItself()
        {
            ColonyWorld colony = Board(colonists: 1, beds: 0);
            int fire = Fire(colony, 12, 12);
            colony.World.Tick(Odyssey.Sim.Temperature.TemperatureSystem.IntervalTicks * 2);

            Pawn pawn = colony.Pawns.Pawns.All[0];
            var job = new Job();
            CriticalNeedsThinkNode.TrySleep(pawn, colony.Pawns, job);

            Assert.That(job.TargetCell, Is.Not.EqualTo(fire),
                "a colonist was told to sleep in the campfire");
        }

        /// <summary>
        /// A board with no fire behaves exactly as it did: she lies where she stands.
        ///
        /// <para>The control, and the reason no golden moved — every golden board is this one.
        /// </para>
        /// </summary>
        [Test]
        public void WithNoFireSheStillLiesWhereSheStands()
        {
            ColonyWorld colony = Board(colonists: 1, beds: 0);
            colony.World.Tick(Odyssey.Sim.Temperature.TemperatureSystem.IntervalTicks * 2);

            Pawn pawn = colony.Pawns.Pawns.All[0];
            var job = new Job();

            Assert.That(CriticalNeedsThinkNode.TrySleep(pawn, colony.Pawns, job), Is.True);
            Assert.That(job.TargetCell, Is.EqualTo(-1),
                "with no fire on the board a bedless colonist should lie down where she is, " +
                "which is what every golden world does");
        }

        /// <summary>
        /// A bed still wins. The fireside is what a colonist does <b>instead of the rubble</b>,
        /// not instead of a bed — a bed is warmer, softer and hers.
        /// </summary>
        [Test]
        public void ABedStillBeatsTheFireside()
        {
            ColonyWorld colony = Board(colonists: 1, beds: 1);
            int fire = Fire(colony, 12, 12);
            colony.World.Tick(Odyssey.Sim.Temperature.TemperatureSystem.IntervalTicks * 2);

            Pawn pawn = colony.Pawns.Pawns.All[0];
            var job = new Job();
            Assert.That(CriticalNeedsThinkNode.TrySleep(pawn, colony.Pawns, job), Is.True);

            Assert.That(Adjacent(job.TargetCell, fire), Is.False,
                "a colonist with a bed was sent to the fireside instead of to her bed");
        }


        /// <summary>
        /// A colonist standing at the hearth <b>stays</b> there.
        ///
        /// <para><b>The regression this exists for.</b> <c>FiresideTarget.Find</c> answers with
        /// the pawn's own cell when she is already beside a fire, and the first version of the
        /// idle node then failed its own <c>!= pawn.Cell</c> guard and fell through to the
        /// wander — so arriving at the fire guaranteed walking away from it on the next think.
        /// The owner saw exactly that and reported it as *"they are just walking about when
        /// idle"* (2026-09-23).</para>
        ///
        /// <para>Asserted over many ticks rather than one, because the fault was never that a
        /// single decision was wrong: each one was reasonable and the sequence was a colonist
        /// pacing back and forth for ever.</para>
        /// </summary>
        [Test]
        public void AnIdlerAtTheHearthStaysThere()
        {
            ColonyWorld colony = Board(colonists: 1, beds: 0);
            int fire = Fire(colony, 12, 12);
            colony.World.Tick(Odyssey.Sim.Temperature.TemperatureSystem.IntervalTicks * 2);

            Pawn pawn = colony.Pawns.Pawns.All[0];

            // Put her beside the fire and ask what she would do, over a long stretch of ticks.
            int ring = FiresideRing(fire);
            pawn.Cell = ring;

            int wandersAway = 0;
            for (int i = 0; i < 400; i++)
            {
                colony.World.Tick();

                // Held at the hearth each time round: the question is what an idler who IS
                // there decides, and a tick moves her wherever her real job takes her.
                pawn.Cell = ring;

                var job = new Job();
                if (!new IdleThinkNode().TryGiveJob(pawn, colony.Pawns, job)) continue;

                if (colony.Pawns.Content.Jobs[job.DefIndex].driver != JobIndex.Wander) continue;
                if (!Adjacent(job.TargetCell, fire) && job.TargetCell != fire) wandersAway++;
            }

            Assert.That(wandersAway, Is.Zero,
                $"an idler beside the fire chose to walk away from it {wandersAway} times in 400 " +
                "ticks — she should settle, or at most shift to another place in the ring");
        }

        /// <summary>
        /// And she does settle rather than merely not-leaving: the idle node hands back a wait,
        /// which is what stops her thinking again every tick.
        /// </summary>
        [Test]
        public void SettlingAtTheHearthIsAWaitAndNotAPace()
        {
            ColonyWorld colony = Board(colonists: 1, beds: 0);
            int fire = Fire(colony, 12, 12);
            colony.World.Tick(Odyssey.Sim.Temperature.TemperatureSystem.IntervalTicks * 2);

            Pawn pawn = colony.Pawns.Pawns.All[0];
            int ring = FiresideRing(fire);
            int waits = 0;
            for (int i = 0; i < 200; i++)
            {
                colony.World.Tick();
                pawn.Cell = ring;
                var job = new Job();
                if (!new IdleThinkNode().TryGiveJob(pawn, colony.Pawns, job)) continue;
                if (colony.Pawns.Content.Jobs[job.DefIndex].driver == JobIndex.Wait) waits++;
            }

            Assert.That(waits, Is.GreaterThan(100),
                "an idler at the hearth almost never settles, so the ring will reshuffle " +
                "constantly and read as the milling this was written to stop");
        }

        /// <summary>
        /// The settles an idler makes at the hearth over a stretch of ticks, held in the ring.
        /// Only the waits: a shuffle is a walk, and is what the tests above are about.
        /// </summary>
        static System.Collections.Generic.List<Job> Settles(ColonyWorld colony, Pawn pawn, int ring, int ticks)
        {
            var settles = new System.Collections.Generic.List<Job>();
            for (int i = 0; i < ticks; i++)
            {
                colony.World.Tick();
                pawn.Cell = ring;
                var job = new Job();
                if (!new IdleThinkNode().TryGiveJob(pawn, colony.Pawns, job)) continue;
                if (colony.Pawns.Content.Jobs[job.DefIndex].driver == JobIndex.Wait) settles.Add(job);
            }
            return settles;
        }

        /// <summary>
        /// Some settles are seated and some stand, so a ring of idlers alternates between the two
        /// (owner, 2026-09-23: *"they sit by the fire or stand by the fire for a bit and then sit
        /// down and vice versa — for variation so you can tell easily who is idle"*).
        ///
        /// <para>Both halves are asserted to be a real share rather than merely present. A ring
        /// where one colonist in fifty ever sits is a ring that stands, and the test should say
        /// so rather than pass on a single lucky roll.</para>
        /// </summary>
        [Test]
        public void AtTheHearthSomeSettlesSitAndSomeStand()
        {
            ColonyWorld colony = Board(colonists: 1, beds: 0);
            int fire = Fire(colony, 12, 12);
            colony.World.Tick(Odyssey.Sim.Temperature.TemperatureSystem.IntervalTicks * 2);

            Pawn pawn = colony.Pawns.Pawns.All[0];
            var settles = Settles(colony, pawn, FiresideRing(fire), 400);

            int seated = 0;
            foreach (Job job in settles) if (job.Seated) seated++;
            int standing = settles.Count - seated;

            Assert.That(settles.Count, Is.GreaterThan(100), "she hardly settled at all");
            Assert.That(seated, Is.GreaterThan(settles.Count / 4),
                $"only {seated} of {settles.Count} settles at the hearth were seated");
            Assert.That(standing, Is.GreaterThan(settles.Count / 4),
                $"only {standing} of {settles.Count} settles at the hearth stood");
        }

        /// <summary>
        /// A seat names the fire it is beside, which is what the figure turns to face. Without it
        /// a colonist who walked in from the far side would sit with her back to the flames — the
        /// same reason <c>WorkCell</c> exists for an axe and a tree.
        /// </summary>
        [Test]
        public void ASeatFacesTheFireItIsBeside()
        {
            ColonyWorld colony = Board(colonists: 1, beds: 0);
            int fire = Fire(colony, 12, 12);
            colony.World.Tick(Odyssey.Sim.Temperature.TemperatureSystem.IntervalTicks * 2);

            Pawn pawn = colony.Pawns.Pawns.All[0];
            int seats = 0;
            foreach (Job job in Settles(colony, pawn, FiresideRing(fire), 200))
            {
                if (!job.Seated) continue;
                seats++;
                Assert.That(job.DestCell, Is.EqualTo(fire), "a seat faces somewhere other than the fire");
            }
            Assert.That(seats, Is.GreaterThan(0), "nobody sat, so nothing here was asked");
        }

        /// <summary>
        /// Sitting down is a longer stay than standing about. Every seated settle outlasts every
        /// standing one, so the alternation reads as <i>stand for a bit, then sit</i> rather than
        /// as bobbing up and down.
        /// </summary>
        [Test]
        public void ASeatedSettleOutlastsAStandingOne()
        {
            ColonyWorld colony = Board(colonists: 1, beds: 0);
            int fire = Fire(colony, 12, 12);
            colony.World.Tick(Odyssey.Sim.Temperature.TemperatureSystem.IntervalTicks * 2);

            Pawn pawn = colony.Pawns.Pawns.All[0];
            int shortestSeat = int.MaxValue, longestStand = 0;
            foreach (Job job in Settles(colony, pawn, FiresideRing(fire), 400))
            {
                if (job.Seated) shortestSeat = System.Math.Min(shortestSeat, job.WorkTicks);
                else longestStand = System.Math.Max(longestStand, job.WorkTicks);
            }

            Assert.That(shortestSeat, Is.Not.EqualTo(int.MaxValue), "nobody sat, so nothing here was asked");
            Assert.That(shortestSeat, Is.GreaterThan(longestStand),
                $"a seat of {shortestSeat} ticks is no longer than a stand of {longestStand}");
        }

        /// <summary>
        /// Nobody sits anywhere but at a fire. A wait with nothing to face — the stand-down, the
        /// idler with nowhere to wander — is a stand, and on a board with no fire every wait is
        /// one. The control, and with the golden boards all fireless, the reason none moved.
        /// </summary>
        [Test]
        public void WithNoFireNobodySits()
        {
            ColonyWorld colony = Board(colonists: 1, beds: 0);
            colony.World.Tick(Odyssey.Sim.Temperature.TemperatureSystem.IntervalTicks * 2);

            Pawn pawn = colony.Pawns.Pawns.All[0];
            foreach (Job job in Settles(colony, pawn, pawn.Cell, 400))
                Assert.That(job.Seated, Is.False, "a colonist sat down with no fire on the board");

            var standDown = new Job();
            standDown.Reset(JobIndex.Wait);
            Assert.That(standDown.Seated, Is.False, "a bare wait counts as a seat");
        }

        /// <summary>
        /// The view carries it, left to run with nobody holding her anywhere: somewhere in a few
        /// idle hours she walks to the fire and sits, and on every tick the view says she is
        /// seated she is in the ring and facing the fire's own cell.
        /// </summary>
        [Test]
        public void TheViewSaysSheIsSeatedAndFacesTheFire()
        {
            ColonyWorld colony = Board(colonists: 1, beds: 0);
            int fire = Fire(colony, 12, 12);
            CellRef fireCell = Size.FromIndex(fire);

            int seatedTicks = 0;
            for (int i = 0; i < 6_000; i++)
            {
                colony.World.Tick();
                foreach (PawnView view in colony.World.Views.Current.Pawns)
                {
                    if (!view.Seated) continue;
                    seatedTicks++;
                    Assert.That(Adjacent(Size.Index(view.Cell.X, view.Cell.Z, view.Cell.Y), fire), Is.True,
                        $"the view says she is seated at {view.Cell}, which is not beside the fire");
                    Assert.That(view.WorkCell, Is.EqualTo(fireCell), "a seated view faces somewhere else");
                    Assert.That(view.Working, Is.False, "sitting by the fire is not work");
                }
            }

            Assert.That(seatedTicks, Is.GreaterThan(0), "in 6,000 idle ticks she never sat at the fire");
        }

        /// <summary>
        /// A seat is part of the job, so it is saved and hashed with it and a colonist loaded
        /// mid-sit is still sitting. Asserted because the obvious place to add a flag would have
        /// been a field the save does not carry.
        /// </summary>
        [Test]
        public void ASeatSurvivesASave()
        {
            ColonyWorld colony = Board(colonists: 1, beds: 0);
            int fire = Fire(colony, 12, 12);

            Pawn pawn = colony.Pawns.Pawns.All[0];
            for (int i = 0; i < 6_000 && !(pawn.CurrentJob?.Seated ?? false); i++) colony.World.Tick();
            Assume.That(pawn.CurrentJob?.Seated ?? false, Is.True, "she never sat, which the test above reports");

            ColonyWorld restored = Board(colonists: 1, beds: 0);
            restored.Load(colony.Save());
            Job? job = restored.Pawns.Pawns.All[0].CurrentJob;

            Assert.That(job?.Seated ?? false, Is.True, "a colonist loaded mid-sit is standing");
            Assert.That(job!.DestCell, Is.EqualTo(fire));
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));
        }

        /// <summary>
        /// A bandit does not settle at the colony's fire. The idle node is also the last node of
        /// the hostile mind (design 33 §5), so without a guard a raider with nobody to hunt walked
        /// to the hearth and sat facing the flames among the people it came to kill — which came
        /// in with the merge of combat, where neither branch alone could have shown it.
        /// </summary>
        [Test]
        public void ABanditDoesNotSettleAtTheColonysFire()
        {
            ColonyWorld colony = Board(colonists: 1, beds: 0);
            int fire = Fire(colony, 12, 12);
            colony.World.Tick(Odyssey.Sim.Temperature.TemperatureSystem.IntervalTicks * 2);

            int ring = FiresideRing(fire);
            Pawn bandit = colony.Pawns.Pawns.Spawn(ring, PawnKindIndex.Bandit);
            Assume.That(bandit.IsHostile, Is.True);

            var settles = Settles(colony, bandit, ring, 400);
            int seated = 0;
            foreach (Job job in settles) if (job.Seated) seated++;

            Assert.That(seated, Is.Zero, "a bandit sat down at the colony's fire");
            Assert.That(settles.Count, Is.LessThan(40),
                $"a bandit beside the fire settled there {settles.Count} times in 400 ticks; " +
                "it should wander off as it did before there were fires");
        }

        /// <summary>
        /// Everybody who is standing still or lying down, by cell: the number of cells with two or
        /// more in them, and the number of colonists within two cells of the fire.
        /// </summary>
        static (int shared, int atHearth) Crowding(ColonyWorld colony, int fire)
        {
            var seen = new System.Collections.Generic.Dictionary<int, int>();
            int atHearth = 0;
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            {
                if (pawn.HasPath || pawn.FinishingStepTo >= 0) continue;
                seen.TryGetValue(pawn.Cell, out int n);
                seen[pawn.Cell] = n + 1;
                CellRef p = Size.FromIndex(pawn.Cell), f = Size.FromIndex(fire);
                if (p.Y == f.Y && System.Math.Max(System.Math.Abs(p.X - f.X), System.Math.Abs(p.Z - f.Z)) <= 2) atHearth++;
            }
            int shared = 0;
            foreach (var pair in seen) if (pair.Value > 1) shared++;
            return (shared, atHearth);
        }

        /// <summary>
        /// Six idlers round one fire stand in six different cells, on every tick (owner,
        /// 2026-09-25: *"colonists stand around the campfire in the same tile … first separate
        /// tiles. Don't have them exactly over each other — that should never happen in any
        /// scenario"*).
        ///
        /// <para>Asserted over thousands of ticks and over everybody standing still or lying down,
        /// not over one decision: the fault was two idlers choosing on the same tick, or one
        /// choosing a cell somebody else was already walking to, and either happens only now and
        /// then. With the claim test switched off this counts shared cells within the first few
        /// hundred ticks.</para>
        /// </summary>
        [Test]
        public void SixIdlersAtAFireStandInSixDifferentCells()
        {
            ColonyWorld colony = Board(colonists: 6, beds: 0);
            int fire = Fire(colony, 12, 12);

            int worst = 0, gathered = 0;
            for (int i = 0; i < 6_000; i++)
            {
                colony.World.Tick();
                (int shared, int atHearth) = Crowding(colony, fire);
                worst = System.Math.Max(worst, shared);
                gathered = System.Math.Max(gathered, atHearth);
            }

            Assert.That(gathered, Is.GreaterThanOrEqualTo(4), "the idlers never gathered at the fire, so nothing was asked");
            Assert.That(worst, Is.Zero, "two colonists stood or lay in one cell");
        }

        /// <summary>
        /// More idlers than the eight beside the fire: the rest stand a step further out, and still
        /// nobody shares. Twelve, so the second ring is certainly used.
        /// </summary>
        [Test]
        public void ACrowdLargerThanTheRingStandsBehindItAndNobodyShares()
        {
            ColonyWorld colony = Board(colonists: 12, beds: 0);
            int fire = Fire(colony, 12, 12);

            int worst = 0, gathered = 0;
            for (int i = 0; i < 6_000; i++)
            {
                colony.World.Tick();
                (int shared, int atHearth) = Crowding(colony, fire);
                worst = System.Math.Max(worst, shared);
                gathered = System.Math.Max(gathered, atHearth);
            }

            Assert.That(gathered, Is.GreaterThan(8), "the crowd never spilled past the first ring, so nothing was asked");
            Assert.That(worst, Is.Zero, "two colonists stood or lay in one cell");
        }

        /// <summary>
        /// The chooser's own contract, one decision at a time: a ring cell somebody is standing on,
        /// or walking to, is not offered to anyone else, and a colonist sharing her cell is sent
        /// on rather than told she has arrived.
        /// </summary>
        [Test]
        public void ARingCellSomebodyHoldsIsNotOffered()
        {
            ColonyWorld colony = Board(colonists: 2, beds: 0);
            int fire = Fire(colony, 12, 12);
            colony.World.Tick(Odyssey.Sim.Temperature.TemperatureSystem.IntervalTicks * 2);

            var pawns = colony.Pawns.Pawns.All;
            Pawn first = pawns[0], second = pawns[1];
            int ring = FiresideRing(fire);

            // Both standing in the one ring cell: neither is "already there".
            first.Cell = ring; first.ClearPath();
            second.Cell = ring; second.ClearPath();
            var job = new Job();
            Assume.That(new IdleThinkNode().TryGiveJob(second, colony.Pawns, job), Is.True);
            if (colony.Pawns.Content.Jobs[job.DefIndex].driver == JobIndex.Wander)
                Assert.That(job.TargetCell, Is.Not.EqualTo(ring), "she was sent to the cell she shares");
            Assert.That(colony.Pawns.Pawns.IsClaimedByOther(second, ring), Is.True);

            // One walking to it: it is held, by her job's target, before her path exists.
            first.Cell = colony.Grid.NearestWalkableInColumn(30, 30, Size.SizeY - 1);
            second.Cell = colony.Grid.NearestWalkableInColumn(32, 30, Size.SizeY - 1);
            second.CurrentJob = null;
            var walk = new Job();
            walk.Reset(JobIndex.Wander);
            walk.TargetCell = ring;
            first.CurrentJob = walk;
            Assert.That(colony.Pawns.Pawns.IsClaimedByOther(second, ring), Is.True,
                "a cell somebody is walking to was not held");
            Assert.That(colony.Pawns.Pawns.IsClaimedByOther(first, ring), Is.False,
                "her own destination counted against her");
        }

        /// <summary>The first free cell of a fire's ring — where a colonist at the hearth stands.</summary>
        static int FiresideRing(int fire)
        {
            CellRef at = Size.FromIndex(fire);
            return Size.Index(at.X + 1, at.Z, at.Y);
        }

        /// <summary>
        /// Two bedless colonists do not lie in the same cell. The sleeper's target is reserved
        /// exactly as a bed is, for the same reason.
        /// </summary>
        [Test]
        public void TwoSleepersDoNotShareOneCell()
        {
            ColonyWorld colony = Board(colonists: 2, beds: 0);
            Fire(colony, 12, 12);
            colony.World.Tick(Odyssey.Sim.Temperature.TemperatureSystem.IntervalTicks * 2);

            var pawns = colony.Pawns.Pawns.All;
            var first = new Job();
            Assert.That(CriticalNeedsThinkNode.TrySleep(pawns[0], colony.Pawns, first), Is.True);

            // Hold it the way the job system does once a sleeper has claimed her spot.
            colony.Pawns.Reservations.Reserve(pawns[0].Id,
                ReservationManager.Key(ReservationTargetKind.Cell, first.TargetCell));

            var second = new Job();
            Assert.That(CriticalNeedsThinkNode.TrySleep(pawns[1], colony.Pawns, second), Is.True);
            Assert.That(second.TargetCell, Is.Not.EqualTo(first.TargetCell),
                "two colonists were sent to lie down in the same cell");
        }
    }
}
