#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim.Events
{
    /// <summary>
    /// The storyteller in a live colony (design 68 §7–§8): nothing while none is chosen, the choice
    /// and its view, determinism and a save mid-season, the gates, and the two intents. The pacing
    /// itself is <see cref="StorytellerPacerTests"/>'s, on the pure pacer.
    /// </summary>
    public class StorytellerTests
    {
        static ColonyWorld Board(uint seed = 7u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            ColonyWorld colony = ColonyWorld.Build(new GridSize(60, 60, 16), seed, scenario, barren: true, wooded: false);
            colony.World.Tick(30);
            return colony;
        }

        static IntentRejection Send(ColonyWorld colony, Intent intent)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(intent);
            colony.World.Tick();
            var r = colony.World.Intents.Rejected;
            return r.Count == 0 ? IntentRejection.None : r[0].Reason;
        }

        static Intent Difficulty(int rung, int threat, int adaptation, int grace, bool big) =>
            new Intent(IntentKind.SetDifficulty, default, rung, threat | (adaptation << 16), grace | (big ? 1 << 16 : 0));

        static Storyteller Of(ColonyWorld colony) => colony.Pawns.Storyteller!;

        static ulong Hash(ColonyWorld colony) => colony.World.ComputeStateHash().Value;

        [Test]
        public void WithNoStorytellerItHashesAndPublishesNothing()
        {
            ColonyWorld colony = Board();
            Storyteller teller = Of(colony);
            Assert.That(teller.HasStoryteller, Is.False);

            StateHash empty = StateHash.New();
            StateHash touched = StateHash.New();
            teller.ContributeTo(ref touched);
            Assert.That(touched.Value, Is.EqualTo(empty.Value), "an unset storyteller moved the hash, so every golden would");

            colony.World.Tick(Calendar.TicksPerHour * 3);
            Assert.That(colony.World.Views.Current.Storyteller.HasStoryteller, Is.False);
            Assert.That(teller.Pacer.GeneratorCount, Is.Zero, "nothing was armed");
        }

        [Test]
        public void ChoosingOneArmsItAndPublishesIt()
        {
            ColonyWorld colony = Board();
            Assert.That(Send(colony, new Intent(IntentKind.SetStoryteller, default, StorytellerHandle.Kano)), Is.EqualTo(IntentRejection.None));
            Storyteller teller = Of(colony);
            Assert.That(teller.Index, Is.EqualTo(StorytellerHandle.Kano));
            Assert.That(teller.StartTick, Is.GreaterThanOrEqualTo(30));
            Assert.That(teller.Pacer.GeneratorCount, Is.EqualTo(2));
            Assert.That(teller.GraceEndTick, Is.EqualTo(teller.StartTick + 15 * Calendar.TicksPerDay));

            StorytellerView view = colony.World.Views.Current.Storyteller;
            Assert.That(view.Storyteller, Is.EqualTo(StorytellerHandle.Kano));
            Assert.That(view.Band, Is.EqualTo(2), "a new colony is Even");

            Assert.That(Send(colony, new Intent(IntentKind.SetStoryteller, default, StorytellerHandle.Kano)),
                Is.EqualTo(IntentRejection.AlreadyInThatState));
            Assert.That(Send(colony, new Intent(IntentKind.SetStoryteller, default, 9)), Is.EqualTo(IntentRejection.OutOfBounds));
        }

        [Test]
        public void TheDifficultyLeversAreStoredAndChecked()
        {
            ColonyWorld colony = Board();
            Send(colony, new Intent(IntentKind.SetStoryteller, default, StorytellerHandle.Jacob));
            Storyteller teller = Of(colony);
            int graceBefore = teller.GraceEndTick;

            Assert.That(Send(colony, Difficulty(6, 250, 150, 150, false)), Is.EqualTo(IntentRejection.None));
            Assert.That(teller.ThreatPercent, Is.EqualTo(250));
            Assert.That(teller.AdaptationPercent, Is.EqualTo(150));
            Assert.That(teller.BigThreats, Is.False);
            Assert.That(teller.GraceEndTick, Is.GreaterThan(graceBefore), "a longer stretch moves a grace still to come");

            Assert.That(Send(colony, Difficulty(6, 600, 100, 100, true)), Is.EqualTo(IntentRejection.OutOfBounds));
            Assert.That(Send(colony, Difficulty(6, 100, 100, 40, true)), Is.EqualTo(IntentRejection.OutOfBounds));
            StorytellerView view = colony.World.Views.Current.Storyteller;
            Assert.That(view.ThreatPercent, Is.EqualTo(250));
            Assert.That(view.Rung, Is.EqualTo(6));
        }

        [Test]
        public void TwoWorldsTellTheSameStory()
        {
            ColonyWorld a = Board(11u), b = Board(11u);
            Send(a, new Intent(IntentKind.SetStoryteller, default, StorytellerHandle.Trent));
            Send(b, new Intent(IntentKind.SetStoryteller, default, StorytellerHandle.Trent));
            a.World.Tick(Calendar.TicksPerDay);
            b.World.Tick(Calendar.TicksPerDay);
            Assert.That(Hash(b), Is.EqualTo(Hash(a)));
            Assert.That(Of(b).Pacer.NextTick(0), Is.EqualTo(Of(a).Pacer.NextTick(0)));
        }

        [Test]
        public void ASaveMidSeasonResumesTheSamePlan()
        {
            ColonyWorld colony = Board(5u);
            Send(colony, new Intent(IntentKind.SetStoryteller, default, StorytellerHandle.Jacob));
            Send(colony, Difficulty(4, 150, 75, 85, true));
            colony.World.Tick(Calendar.TicksPerDay / 2);

            ColonyWorld restored = Board(5u);
            restored.Load(colony.Save());
            Storyteller a = Of(colony), b = Of(restored);
            Assert.That(b.Index, Is.EqualTo(a.Index));
            Assert.That(b.GraceEndTick, Is.EqualTo(a.GraceEndTick));
            Assert.That(b.ThreatPercent, Is.EqualTo(150));
            for (int g = 0; g < a.Pacer.GeneratorCount; g++)
                Assert.That(b.Pacer.NextTick(g), Is.EqualTo(a.Pacer.NextTick(g)));
            Assert.That(Hash(restored), Is.EqualTo(Hash(colony)), "the loaded world is not the saved one");

            colony.World.Tick(Calendar.TicksPerDay / 2);
            restored.World.Tick(Calendar.TicksPerDay / 2);
            Assert.That(Hash(restored), Is.EqualTo(Hash(colony)), "the two parted after the load");
        }

        [Test]
        public void AColonySavedWithoutOneLoadsWithoutOne()
        {
            ColonyWorld colony = Board(3u);
            ColonyWorld restored = Board(3u);
            Send(restored, new Intent(IntentKind.SetStoryteller, default, StorytellerHandle.Jacob));
            restored.Load(colony.Save());
            Assert.That(Of(restored).HasStoryteller, Is.False, "the save said none and the load kept the old one");
        }

        [Test]
        public void TheGatesAreRead()
        {
            ColonyWorld colony = Board();
            IncidentContent content = colony.Incidents.Content;
            IncidentDef raid = content.Defs[IncidentHandle.Raid];
            IncidentLedger ledger = colony.Incidents.Ledger;
            int day = Calendar.TicksPerDay;

            Assert.That(Storyteller.GatesPass(raid, IncidentHandle.Raid, ledger, 2 * day, 3), Is.False, "before the earliest day");
            Assert.That(Storyteller.GatesPass(raid, IncidentHandle.Raid, ledger, 3 * day, 3), Is.True, "the control");
            Assert.That(Storyteller.GatesPass(raid, IncidentHandle.Raid, ledger, 3 * day, 0), Is.False, "no colonists");

            ledger.Record(IncidentHandle.Raid, -1, 3 * day);
            Assert.That(Storyteller.GatesPass(raid, IncidentHandle.Raid, ledger, 4 * day, 3), Is.False, "inside the refire");
            Assert.That(Storyteller.GatesPass(raid, IncidentHandle.Raid, ledger, 5 * day, 3), Is.True, "after the refire");
        }

        [Test]
        public void AnUnfireableIncidentIsNeverPicked()
        {
            ColonyWorld colony = Board();
            Send(colony, new Intent(IntentKind.SetStoryteller, default, StorytellerHandle.Trent));
            // The small threats today are a theft and a bandit leaving: records, never fired.
            Assert.That(Of(colony).TryFire(IncidentCategory.ThreatSmall, false, 1000), Is.False);
            Assert.That(Of(colony).TryFire(IncidentCategory.Arrival, false, 1000), Is.False);
            Assert.That(colony.Incidents.Ledger.Fires(IncidentHandle.Theft), Is.Zero);
        }

        [Test]
        public void AGoodEventFiresThroughTheOneDoor()
        {
            ColonyWorld colony = Board();
            Send(colony, new Intent(IntentKind.SetStoryteller, default, StorytellerHandle.Jacob));
            // The drops' earliest day is 1: on day 0 the gate refuses, which is the gate working.
            Assert.That(Of(colony).TryFire(IncidentCategory.Misc, true, 1000), Is.False, "the gate, on day 0");
            colony.World.Tick(Calendar.TicksPerDay - colony.World.CurrentTick);
            Assert.That(Of(colony).TryFire(IncidentCategory.Misc, true, 1000), Is.True);
            int drops = colony.Incidents.Ledger.Fires(IncidentHandle.SupplyDrop)
                        + colony.Incidents.Ledger.Fires(IncidentHandle.ScrapDrop)
                        + colony.Incidents.Ledger.Fires(IncidentHandle.MedicalDrop);
            Assert.That(drops, Is.EqualTo(1));
        }
    }
}
