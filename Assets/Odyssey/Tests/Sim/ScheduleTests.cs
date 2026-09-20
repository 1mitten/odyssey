#nullable enable
using System.IO;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The colonist's day (design 27 §12): the state, the command that writes it, the save that
    /// carries it, and the one thing it deliberately does <b>not</b> touch.
    ///
    /// <para><b>The schedule is authored, saved, published and editable — and read by nothing.</b>
    /// The hour a colonist sleeps is still decided by their rest need. That is why it is absent
    /// from the state hash, and <see cref="EditingTheDayDoesNotMoveTheStateHash"/> is the test that
    /// says so out loud: a value no system consults cannot affect a tick, which is the same test
    /// the saved view passes. The unit that makes the job system obey the schedule is the unit
    /// that puts it in the hash and re-bakes the goldens, once, with a sentence.</para>
    /// </summary>
    public class ScheduleTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 12);

        static ColonyWorld Board(uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 2;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: true);
        }

        static IntentRejection Set(ColonyWorld colony, int pawn, int hour, int block)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(
                new Intent(IntentKind.SetScheduleBlock, default, pawn, hour, block));
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        // ------------------------------------------------------------------ the day itself

        [Test]
        public void EveryColonistArrivesOnTheDefaultDay()
        {
            ColonyWorld colony = Board();

            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            {
                Assert.That(pawn.ScheduleHours.Length, Is.EqualTo(ScheduleHandle.Hours));
                Assert.That(pawn.ScheduleAt(0), Is.EqualTo(ScheduleHandle.Sleep));
                Assert.That(pawn.ScheduleAt(7), Is.EqualTo(ScheduleHandle.Anything));
                Assert.That(pawn.ScheduleAt(12), Is.EqualTo(ScheduleHandle.Work));
                Assert.That(pawn.ScheduleAt(20), Is.EqualTo(ScheduleHandle.Recreation));
                Assert.That(pawn.ScheduleAt(23), Is.EqualTo(ScheduleHandle.Sleep));
            }
        }

        [Test]
        public void AnHourOutsideTheDayReadsAsAnythingRatherThanThrowing()
        {
            Pawn pawn = Board().Pawns.Pawns.All[0];

            Assert.That(pawn.ScheduleAt(-1), Is.EqualTo(ScheduleHandle.Anything));
            Assert.That(pawn.ScheduleAt(ScheduleHandle.Hours), Is.EqualTo(ScheduleHandle.Anything));
        }

        // ------------------------------------------------------------------ the command

        [Test]
        public void TheCommandWritesOneHourOnOneColonist()
        {
            ColonyWorld colony = Board();
            Pawn subject = colony.Pawns.Pawns.All[0];
            Pawn other = colony.Pawns.Pawns.All[1];

            Assert.That(Set(colony, subject.Id.Value, 12, ScheduleHandle.Meditate),
                Is.EqualTo(IntentRejection.None));

            Assert.That(subject.ScheduleAt(12), Is.EqualTo(ScheduleHandle.Meditate));
            Assert.That(subject.ScheduleAt(11), Is.EqualTo(ScheduleHandle.Work), "one hour, not the day");
            Assert.That(other.ScheduleAt(12), Is.EqualTo(ScheduleHandle.Work), "one colonist, not the column");
        }

        [Test]
        public void AnOutOfRangeArgumentIsRefusedRatherThanClamped()
        {
            ColonyWorld colony = Board();
            Pawn subject = colony.Pawns.Pawns.All[0];

            Assert.That(Set(colony, subject.Id.Value, 24, ScheduleHandle.Work),
                Is.EqualTo(IntentRejection.NotPermitted), "an hour of 24 is a misunderstood day");
            Assert.That(Set(colony, subject.Id.Value, -1, ScheduleHandle.Work),
                Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(Set(colony, subject.Id.Value, 12, ScheduleHandle.Count),
                Is.EqualTo(IntentRejection.NotPermitted), "no such block");
            Assert.That(Set(colony, -1, 12, ScheduleHandle.Work),
                Is.EqualTo(IntentRejection.NotPermitted), "no such colonist");

            Assert.That(subject.ScheduleAt(12), Is.EqualTo(ScheduleHandle.Work),
                "nothing was written by any of the four refusals");
        }

        [Test]
        public void SettingTheBlockItAlreadyHasSaysSo()
        {
            ColonyWorld colony = Board();
            Pawn subject = colony.Pawns.Pawns.All[0];

            Assert.That(Set(colony, subject.Id.Value, 12, ScheduleHandle.Work),
                Is.EqualTo(IntentRejection.AlreadyInThatState));
        }

        [Test]
        public void TheCommandLandsWhilePaused()
        {
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.SetScheduleBlock), Is.True,
                "planning the day is exactly the thing a player pauses in order to do");
        }

        // ------------------------------------------------------------------ the hash

        [Test]
        public void EditingTheDayDoesNotMoveTheStateHash()
        {
            // The load-bearing test of this unit. The schedule is saved and published but read by
            // nothing, so it is deliberately outside the hash — which is what lets this land
            // without moving six golden numbers for a change that alters no behaviour. When the
            // job system starts obeying the schedule, this test is the one that has to change,
            // and changing it is the signal to re-bake the goldens.
            ColonyWorld colony = Board();
            Pawn subject = colony.Pawns.Pawns.All[0];
            colony.World.Tick();
            ulong before = colony.World.ComputeStateHash().Value;

            for (int h = 0; h < ScheduleHandle.Hours; h++)
                subject.ScheduleHours[h] = (byte)ScheduleHandle.Meditate;

            Assert.That(colony.World.ComputeStateHash().Value, Is.EqualTo(before),
                "the schedule entered the state hash. If that was deliberate, this test is the " +
                "place to say so — and the goldens need re-baking with ODYSSEY_REGOLDEN=1.");
        }

        // ------------------------------------------------------------------ the save

        [Test]
        public void TheDaySurvivesASaveAndLoad()
        {
            ColonyWorld colony = Board();
            Pawn subject = colony.Pawns.Pawns.All[0];
            subject.ScheduleHours[3] = (byte)ScheduleHandle.Eat;
            subject.ScheduleHours[17] = (byte)ScheduleHandle.Meditate;

            using var stream = new MemoryStream();
            colony.Save(stream);
            stream.Position = 0;

            ColonyWorld restored = Board();
            restored.Load(stream);

            Pawn back = restored.Pawns.Pawns.All[0];
            Assert.That(back.ScheduleAt(3), Is.EqualTo(ScheduleHandle.Eat));
            Assert.That(back.ScheduleAt(17), Is.EqualTo(ScheduleHandle.Meditate));
            Assert.That(back.ScheduleAt(12), Is.EqualTo(ScheduleHandle.Work), "and the rest of the day");
        }

        // ------------------------------------------------------------------ the publish

        [Test]
        public void TheDayIsPublishedHourByHourForEveryColonist()
        {
            ColonyWorld colony = Board();
            Pawn subject = colony.Pawns.Pawns.All[0];
            Set(colony, subject.Id.Value, 4, ScheduleHandle.Recreation);

            WorldSnapshot frame = colony.World.Views.Current;

            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            for (int h = 0; h < ScheduleHandle.Hours; h++)
            {
                Assert.That(frame.TryGetPawnAspect(pawn.Id, ScheduleAspects.Hour[h], out int block),
                    Is.True, "every colonist's every hour, not only the selected one");
                Assert.That(block, Is.EqualTo(pawn.ScheduleAt(h)));
            }

            Assert.That(frame.TryGetPawnAspect(subject.Id, ScheduleAspects.Hour[4], out int written),
                Is.True);
            Assert.That(written, Is.EqualTo(ScheduleHandle.Recreation));
        }

        [Test]
        public void TheAspectNamesAreTheOnesTheInterfaceSpells()
        {
            // The other end of the seam. Odyssey.Hud mints the same strings in ScheduleKeys and
            // cannot reference this assembly to check, so the agreement is asserted from here.
            Assert.That(ScheduleAspects.Name(0), Is.EqualTo("odyssey.pawn.schedule.h00"));
            Assert.That(ScheduleAspects.Name(23), Is.EqualTo("odyssey.pawn.schedule.h23"));
            Assert.That(ScheduleAspects.Hour.Length, Is.EqualTo(ScheduleHandle.Hours));
            Assert.That(ScheduleAspects.Hour[13],
                Is.EqualTo(AspectKey.Of("odyssey.pawn.schedule.h13")));
        }

        [Test]
        public void TheDayIsAsLongAsTheClocksDay()
        {
            // Two constants that must agree, in two assemblies that cannot see each other's reason
            // for holding one. GameClock is the interface's; ScheduleHandle is the contract's.
            Assert.That(ScheduleHandle.Hours, Is.EqualTo(24));
        }
    }
}
