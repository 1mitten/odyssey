#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The Thoughts tab's model (design 51 §5b, TM2): what is on a colonist's mind, grouped
    /// <i>Now</i> then <i>Memories</i>, worst first, in points out of a hundred, capped so the pane
    /// stays one height. Everything here is read from aspects the simulation publishes; the
    /// control in each case is the same frame without them.
    /// </summary>
    public class ThoughtsTabTests
    {
        static readonly PawnId Ada = new PawnId(1);

        static WorldSnapshot Frame(int mood = 500)
        {
            WorldSnapshot snapshot = global::Odyssey.Tests.Hud.Frame.Write();
            snapshot.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 800, 800, mood, JobHandle.Wait,
                flags: PawnFlags.Person));
            return snapshot;
        }

        static void Aspect(WorldSnapshot snapshot, string name, int value) =>
            snapshot.AddPawnAspect(new PawnAspect(Ada, AspectKey.Of(name), value));

        static void Memory(WorldSnapshot snapshot, int thought, int worth, int left, int count = 1)
        {
            string name = MindCatalogue.ThoughtPrefix + ThoughtHandle.Names[thought];
            Aspect(snapshot, name, worth);
            Aspect(snapshot, name + ".left", left);
            Aspect(snapshot, name + ".count", count);
        }

        static InspectModel Pane(WorldSnapshot snapshot)
        {
            var pane = new InspectModel();
            pane.SetColonist(Ada);
            pane.Refresh(snapshot);
            return pane;
        }

        [Test]
        public void TheFriendlyFireMemoryIsNamedWithItsWorthAndItsTimeLeft()
        {
            // The case that asked for this tab (design 33 §12b): a colonist struck by another loses
            // eight points for a day and nothing on screen said why.
            WorldSnapshot snapshot = Frame();
            Aspect(snapshot, MindAspectNames.Target, 420);
            Memory(snapshot, ThoughtHandle.AttackedByColonist, -80, left: 18 * GameClock.TicksPerDay / 24);

            InspectModel pane = Pane(snapshot);

            InspectRow row = pane.ThoughtRows.Single(r => r.Name == Registry.Label("ui.thought.attackedbycolonist"));
            Assert.That(row.Value, Does.StartWith("-8 "), "thousandths shown as the owner's points");
            Assert.That(row.Value, Does.Contain("18h"));
            Assert.That(row.Tint, Is.EqualTo(HudTheme.Bad));
            Assert.That(row.Tooltip, Is.EqualTo(Registry.Describe("ui.thought.attackedbycolonist")),
                "the tooltip is the wiki's description, one owner");
            Assert.That(pane.ThoughtHeading, Does.Contain("50").And.Contain("42"),
                "the heading says where she is and where she is going");

            // The control: the same colonist with nothing published has nothing named.
            InspectModel bare = Pane(Frame());
            Assert.That(bare.ThoughtRows.Any(r => r.Name == Registry.Label("ui.thought.attackedbycolonist")), Is.False);
            Assert.That(bare.ThoughtRows.Single().Name, Is.EqualTo(Registry.Label("ui.mind.nothing")));
        }

        [Test]
        public void NowComesBeforeMemoriesAndEachIsWorstFirst()
        {
            WorldSnapshot snapshot = Frame();
            Aspect(snapshot, "odyssey.pawn.mood.need.food", -60);
            Aspect(snapshot, "odyssey.pawn.mood.temperature", -120);
            Memory(snapshot, ThoughtHandle.AteMeal, 35, left: 1000, count: 2);
            Memory(snapshot, ThoughtHandle.SleptOnGround, -40, left: 9000);

            string[] names = Pane(snapshot).ThoughtRows.Select(r => r.Name).ToArray();

            Assert.That(names, Is.EqualTo(new[]
            {
                Registry.Label("ui.mind.now"),
                Registry.Label("ui.thought.temperature"),
                Registry.Label("ui.thought.hunger"),
                Registry.Label("ui.mind.memories"),
                Registry.Label("ui.thought.sleptonground"),
                Registry.Label("ui.thought.atemeal") + " x2",
            }), "the answer to \"why is she breaking\" is the first row of its group");
        }

        [Test]
        public void ALongNowNeverHidesEveryMemory()
        {
            WorldSnapshot snapshot = Frame();
            Aspect(snapshot, "odyssey.pawn.mood.need.food", -60);
            Aspect(snapshot, "odyssey.pawn.mood.need.rest", -60);
            Aspect(snapshot, "odyssey.pawn.mood.need.joy", -50);
            Aspect(snapshot, "odyssey.pawn.mood.temperature", -120);
            Memory(snapshot, ThoughtHandle.ColonistDied, -60, left: 90_000);
            Memory(snapshot, ThoughtHandle.SleptOnGround, -40, left: 9000);

            string[] names = Pane(snapshot).ThoughtRows.Select(r => r.Name).ToArray();

            Assert.That(names.Length, Is.LessThanOrEqualTo(HudLayout.ThoughtRows));
            Assert.That(names, Does.Contain(Registry.Label("ui.mind.memories")));
            Assert.That(names, Does.Contain(Registry.Label("ui.thought.colonistdied")),
                "the worst memory is shown even when the needs alone would fill the tab");
            Assert.That(names[names.Length - 1], Does.EndWith(Registry.Label("ui.mind.more")));
        }

        [Test]
        public void ALongListIsCappedAndTheRestCounted()
        {
            WorldSnapshot snapshot = Frame();
            for (int t = 0; t < ThoughtHandle.Count; t++) Memory(snapshot, t, -10 * (t + 1), left: 1000);

            InspectModel pane = Pane(snapshot);

            Assert.That(pane.ThoughtRows.Count, Is.LessThanOrEqualTo(HudLayout.ThoughtRows),
                "the pane is one height whatever tab is showing, so the list may not grow it");
            InspectRow last = pane.ThoughtRows[pane.ThoughtRows.Count - 1];
            Assert.That(last.Name, Does.StartWith("+").And.EndWith(Registry.Label("ui.mind.more")));
            int shown = pane.ThoughtRows.Count - 2;   // less the heading and the count
            Assert.That(last.Name, Does.StartWith("+" + (ThoughtHandle.Count - shown) + " "));
        }

        [Test]
        public void TheTabHasRoomForItsHeadingAndRowsInsideTheFixedBody()
        {
            Assert.That(HudLayout.ThoughtRows, Is.GreaterThanOrEqualTo(5),
                "two groups with a heading and two rows each, and the count, is the least useful list");
            Assert.That((HudLayout.ThoughtRows + 1) * HudLayout.CellRow, Is.LessThanOrEqualTo(HudLayout.InspectTabBody));
        }

        [Test]
        public void PointsAndTimesAreWrittenTheWayTheScreenCanDrawThem()
        {
            Assert.That(MindCatalogue.Points(-80), Is.EqualTo("-8"));
            Assert.That(MindCatalogue.Points(35), Is.EqualTo("+4"), "half away from nought");
            Assert.That(MindCatalogue.Points(-138), Is.EqualTo("-14"));
            Assert.That(MindCatalogue.Points(4), Is.EqualTo("0"));
            Assert.That(MindCatalogue.Left(1), Is.EqualTo("1h"), "a memory with a tick left reads an hour, not nought");
            Assert.That(MindCatalogue.Left(GameClock.TicksPerDay * 3), Is.EqualTo("3d"));
            foreach (char c in MindCatalogue.Points(-80) + MindCatalogue.Left(5000))
                Assert.That(c, Is.LessThan((char)128), "ASCII only: a sign neither font has is a blank (P13)");
        }

        [Test]
        public void ABreakIsNamedOnThePaneAndOnTheEventsPanel()
        {
            WorldSnapshot snapshot = Frame();
            Aspect(snapshot, MindAspectNames.Band, MoodBand.Broken);
            Aspect(snapshot, MindAspectNames.Break, BreakHandle.Tantrum);

            InspectModel pane = Pane(snapshot);
            Assert.That(pane.MoodWord, Is.EqualTo(MoodBands.Word(MoodBand.Broken) + " ("
                + Registry.Label("ui.break.tantrum").ToLowerInvariant() + ")"));

            Assert.That(BulletinModel.BreakTitle("ui.bulletin.mentalbreak", BreakHandle.Berserk),
                Is.EqualTo(Registry.Label("ui.bulletin.mentalbreak") + " · " + Registry.Label("ui.break.berserk")),
                "the row names the break; the subject is the break, not an item");
            Assert.That(IncidentLabels.IconKey(IncidentHandle.MentalBreak), Is.EqualTo("ui.bulletin.mentalbreak"));

            // The control: a colonist not in a break is named by her band alone.
            WorldSnapshot calm = Frame();
            Aspect(calm, MindAspectNames.Band, MoodBand.Strained);
            Assert.That(Pane(calm).MoodWord, Is.EqualTo(MoodBands.Word(MoodBand.Strained)));
        }

        [Test]
        public void EveryBreakHasANameAndADescription()
        {
            for (int k = 0; k < BreakHandle.Count; k++)
            {
                string key = "ui.break." + BreakHandle.Names[k];
                Assert.That(Registry.Label(key), Is.Not.EqualTo(key), key + " is not in the registry");
                Assert.That(Registry.Describe(key), Is.Not.Empty, key + " has no description");
            }
        }

        [Test]
        public void EveryThoughtHasANameAndADescription()
        {
            foreach (MindCatalogue.Source source in MindCatalogue.Memories.Concat(MindCatalogue.Situational))
            {
                Assert.That(Registry.Label(source.Key), Is.Not.EqualTo(source.Key), source.Key + " is not in the registry");
                Assert.That(Registry.Describe(source.Key), Is.Not.Empty, source.Key + " has no description");
            }
        }
    }
}
