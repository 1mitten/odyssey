#nullable enable
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim.Events
{
    /// <summary>
    /// The storyteller in a real headless colony (design 59 §9): one season after grace per
    /// storyteller, raids and drops really landing, the invariants held every game hour, and the
    /// big threats counted from the ledger and printed beside what the harness predicts. If the two
    /// disagree, a gate or <c>CanFireNow</c> is refusing fires the harness assumed possible — the
    /// first thing to look at.
    /// </summary>
    [Category("Long")]
    public class StorytellerSoakTests
    {
        static ColonyWorld Colony(uint seed)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 5;
            scenario.beds = 5;
            scenario.mealPiles = 6;
            ColonyWorld colony = ColonyWorld.Build(new GridSize(60, 60, 16), seed, scenario, barren: true, wooded: false);
            colony.World.Tick(30);
            return colony;
        }

        [TestCase(StorytellerHandle.Jacob, 3u)]
        [TestCase(StorytellerHandle.Trent, 4u)]
        [TestCase(StorytellerHandle.Kano, 5u)]
        public void ASeasonTellsItsStory(int teller, uint seed)
        {
            ColonyWorld colony = Colony(seed);
            colony.World.Intents.Submit(new Intent(IntentKind.SetStoryteller, default, teller));
            colony.World.Tick();
            Storyteller story = colony.Pawns.Storyteller!;
            Assert.That(story.Index, Is.EqualTo(teller));

            int end = story.GraceEndTick + 24 * Calendar.TicksPerDay;
            var clock = Stopwatch.StartNew();
            int bigBeforeGrace = 0;
            while (colony.World.CurrentTick < end)
            {
                colony.World.Tick(Calendar.TicksPerHour);
                Assert.That(story.TensionPerMille, Is.InRange(Storyteller.TensionMin, Storyteller.TensionMax));
                StorytellerView view = colony.World.Views.Current.Storyteller;
                Assert.That(view.Storyteller, Is.EqualTo(teller));
                Assert.That(view.Band, Is.InRange(0, 4));
                if (colony.World.CurrentTick < story.GraceEndTick) bigBeforeGrace = colony.Incidents.Ledger.Fires(IncidentHandle.Raid);
            }

            IncidentLedger ledger = colony.Incidents.Ledger;
            int raids = ledger.Fires(IncidentHandle.Raid);
            int drops = ledger.Fires(IncidentHandle.SupplyDrop) + ledger.Fires(IncidentHandle.ScrapDrop) + ledger.Fires(IncidentHandle.MedicalDrop);
            TestContext.Out.WriteLine(
                $"{StorytellerContent.Order[teller]}: {raids} raids and {drops} drops in the season after a {story.GraceEndTick / Calendar.TicksPerDay}-day grace; " +
                $"tension {story.TensionPerMille} ({story.Cause}); {clock.Elapsed.TotalSeconds:0.0} s");

            Assert.That(bigBeforeGrace, Is.Zero, "a raid inside the grace");
            Assert.That(drops, Is.GreaterThan(0), "no good event in a season and its grace");
            if (teller != StorytellerHandle.Kano)
                Assert.That(raids, Is.GreaterThan(0), "no raid in a whole season after grace");
        }
    }
}
