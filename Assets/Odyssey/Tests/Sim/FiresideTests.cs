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
