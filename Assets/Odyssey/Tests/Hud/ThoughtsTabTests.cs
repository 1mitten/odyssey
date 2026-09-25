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

        static void Lines(WorldSnapshot snapshot, int minor = 350, int major = 200, int baseMood = 500)
        {
            Aspect(snapshot, "odyssey.pawn.mood.base", baseMood);
            Aspect(snapshot, "odyssey.pawn.mood.line.minor", minor);
            Aspect(snapshot, "odyssey.pawn.mood.line.major", major);
        }

        static void Trait(WorldSnapshot snapshot, int slot, int handle, int mood = 0)
        {
            Aspect(snapshot, "odyssey.pawn.trait." + slot, handle);
            if (mood != 0) Aspect(snapshot, "odyssey.pawn.trait." + slot + ".mood", mood);
        }

        [Test]
        public void TheFriendlyFireMemoryIsNamedWithItsSourceItsTimeLeftAndItsWorth()
        {
            // The case that asked for this tab (design 33 §12b): a colonist struck by another loses
            // eight points for a day and nothing on screen said why.
            WorldSnapshot snapshot = Frame(mood: 500);
            Lines(snapshot);
            Aspect(snapshot, MindAspectNames.Target, 420);
            Memory(snapshot, ThoughtHandle.AttackedByColonist, -80, left: 18 * GameClock.TicksPerDay / 24);

            ThoughtsTab tab = Pane(snapshot).Thoughts;

            ThoughtLine line = tab.Lines.Single(r => r.Name == Registry.Label("ui.thought.attackedbycolonist"));
            Assert.That(line.Value, Is.EqualTo("-8"), "thousandths shown as the owner's points");
            Assert.That(line.Source, Is.EqualTo(Registry.Label("ui.mind.source.memory")));
            Assert.That(line.Lasts, Is.EqualTo("18 " + Registry.Label("ui.mind.hours")));
            Assert.That(line.Ink, Is.EqualTo(HudTheme.Bad), "the value and the rail are bad");
            Assert.That(line.Tip, Is.EqualTo(Registry.Describe("ui.thought.attackedbycolonist")),
                "the tooltip is the wiki's description, one owner");
            Assert.That(tab.MoodText, Is.EqualTo("50"));
            Assert.That(tab.TargetWord, Is.EqualTo(Registry.Label("ui.mind.falling")));
            Assert.That(tab.TargetText, Is.EqualTo("42"));

            // The control: the same colonist with nothing published has nothing listed.
            ThoughtsTab bare = Pane(Frame()).Thoughts;
            Assert.That(bare.Lines, Is.Empty);
            Assert.That(bare.Shown, Is.Empty);
        }

        [Test]
        public void NeedsAndConditionsComeBeforeMemoriesAndEachIsLargestFirst()
        {
            WorldSnapshot snapshot = Frame();
            Lines(snapshot);
            Aspect(snapshot, "odyssey.pawn.mood.need.joy", 100);
            Aspect(snapshot, "odyssey.pawn.mood.need.food", -60);
            Aspect(snapshot, "odyssey.pawn.mood.temperature", -120);
            Memory(snapshot, ThoughtHandle.AteMeal, 35, left: 1000, count: 2);
            Memory(snapshot, ThoughtHandle.SleptOnGround, -40, left: 9000);

            ThoughtLine[] lines = Pane(snapshot).Thoughts.Lines.ToArray();
            string[] names = lines.Select(l => l.Name).ToArray();
            Assert.That(names, Is.EqualTo(new[]
            {
                Registry.Label("ui.thought.temperature"),
                Registry.Label("ui.thought.recreation"),
                Registry.Label("ui.thought.hunger"),
                Registry.Label("ui.thought.sleptonground"),
                Registry.Label("ui.thought.atemeal") + " x2",
            }), "current first, then memories, each by size whatever its sign");
            Assert.That(lines[0].Source, Is.EqualTo(Registry.Label("ui.mind.source.condition")));
            Assert.That(lines[1].Source, Is.EqualTo(Registry.Label("ui.mind.source.need")));
            Assert.That(lines[0].Lasts, Is.Empty, "a condition lasts as long as its cause");
            Assert.That(lines[1].Ink, Is.EqualTo(HudTheme.Good));
        }

        [Test]
        public void TheBreakdownSumsToTheTargetAsDrawn()
        {
            WorldSnapshot snapshot = Frame(mood: 610);
            Lines(snapshot);
            Aspect(snapshot, "odyssey.pawn.mood.need.joy", 100);
            Aspect(snapshot, "odyssey.pawn.mood.need.food", -60);
            Aspect(snapshot, "odyssey.pawn.mood.temperature", -40);
            Memory(snapshot, ThoughtHandle.AteMeal, 87, left: 1000, count: 2);   // 8.7 points: rounding
            Trait(snapshot, 0, TraitHandle.Cheerful, mood: 60);
            Aspect(snapshot, MindAspectNames.Target, 500 + 100 - 60 - 40 + 87 + 60);

            ThoughtsTab tab = Pane(snapshot).Thoughts;
            int sum = tab.Breakdown.Sum(b => b.Points);
            Assert.That(sum, Is.EqualTo(int.Parse(tab.TargetText)), "the four rows add up to where she is heading");
            Assert.That(tab.Breakdown.Select(b => b.Label), Is.EqualTo(new[]
            {
                Registry.Label("ui.mind.base"), Registry.Label("ui.mind.thoughts"),
                Registry.Label("ui.mind.traits"), Registry.Label("ui.mind.needs"),
            }));
            Assert.That(tab.Breakdown[0].Value, Is.EqualTo("50"));
            Assert.That(tab.Breakdown[2].Value, Is.EqualTo("+6"));
            Assert.That(tab.Breakdown[3].Value, Is.EqualTo("+4"), "joy +10 and hunger -6");
            Assert.That(tab.Breakdown[1].Value, Is.EqualTo("+5"), "the conditions and memories, -4 and +8.7");
            Assert.That(tab.Breakdown[3].Ink, Is.EqualTo(HudTheme.Good));
            Assert.That(tab.TargetWord, Is.EqualTo(Registry.Label("ui.mind.rising")), "61 heading for 65");

            // A zero part is written +0 in the quiet ink.
            WorldSnapshot calm = Frame();
            Lines(calm);
            Aspect(calm, MindAspectNames.Target, 500);
            ThoughtsTab steady = Pane(calm).Thoughts;
            Assert.That(steady.Breakdown[1].Value, Is.EqualTo("+0"));
            Assert.That(steady.Breakdown[1].Ink, Is.EqualTo(HudTheme.TextMeta));
            Assert.That(steady.TargetWord, Is.EqualTo(Registry.Label("ui.mind.steady")));
        }

        [Test]
        public void HerMoodIsColouredByHerOwnLines()
        {
            // The mockup's 35 and 20 are the untraited lines; a Jumpy colonist's are higher, and the
            // same mood reads warn for her and good for anybody else.
            Assert.That(ThoughtsTab.InkFor(350, 350, 200), Is.EqualTo(HudTheme.Good), "at the minor line is good");
            Assert.That(ThoughtsTab.InkFor(349, 350, 200), Is.EqualTo(HudTheme.Warn));
            Assert.That(ThoughtsTab.InkFor(200, 350, 200), Is.EqualTo(HudTheme.Warn));
            Assert.That(ThoughtsTab.InkFor(199, 350, 200), Is.EqualTo(HudTheme.Bad));

            WorldSnapshot jumpy = Frame(mood: 400);
            Lines(jumpy, minor: 430, major: 245);
            WorldSnapshot plain = Frame(mood: 400);
            Lines(plain);
            Assert.That(Pane(jumpy).Thoughts.MoodInk, Is.EqualTo(HudTheme.Warn));
            Assert.That(Pane(plain).Thoughts.MoodInk, Is.EqualTo(HudTheme.Good));
            Assert.That(Pane(plain).MoodInk, Is.EqualTo(HudTheme.Good), "the Needs tab's bar asks the same");
        }

        [Test]
        public void MoreThanFiveThoughtsPageFourAtATimeAndNeverScroll()
        {
            WorldSnapshot snapshot = Frame();
            Lines(snapshot);
            Aspect(snapshot, "odyssey.pawn.mood.need.food", -60);
            Aspect(snapshot, "odyssey.pawn.mood.need.rest", -50);
            Aspect(snapshot, "odyssey.pawn.mood.need.joy", 40);
            Aspect(snapshot, "odyssey.pawn.mood.temperature", -120);
            Memory(snapshot, ThoughtHandle.AteMeal, 35, left: 1000);
            Memory(snapshot, ThoughtHandle.SleptOnGround, -40, left: 9000);
            Memory(snapshot, ThoughtHandle.Fell, -150, left: 9000);

            InspectModel pane = Pane(snapshot);
            ThoughtsTab tab = pane.Thoughts;
            Assert.That(tab.Lines.Count, Is.EqualTo(7));
            Assert.That(tab.Paged, Is.True);
            Assert.That(tab.Shown.Count, Is.EqualTo(ThoughtsLayout.RowsPerPage - 1), "the fifth row is the pager");
            Assert.That(tab.PageText, Is.EqualTo("1 / 2"));

            Assert.That(tab.SetPage(1), Is.True);
            Assert.That(tab.Shown.Count, Is.EqualTo(3));
            Assert.That(tab.PageText, Is.EqualTo("2 / 2"));
            Assert.That(tab.SetPage(5), Is.False, "clamped to the last page");
            pane.Refresh(snapshot);
            Assert.That(tab.Page, Is.EqualTo(1), "a refresh keeps the page");

            // The control: five fit without a pager.
            WorldSnapshot five = Frame();
            Lines(five);
            Aspect(five, "odyssey.pawn.mood.need.food", -60);
            Aspect(five, "odyssey.pawn.mood.need.rest", -50);
            Aspect(five, "odyssey.pawn.mood.need.joy", 40);
            Aspect(five, "odyssey.pawn.mood.temperature", -120);
            Memory(five, ThoughtHandle.Fell, -150, left: 9000);
            ThoughtsTab fits = Pane(five).Thoughts;
            Assert.That(fits.Paged, Is.False);
            Assert.That(fits.Shown.Count, Is.EqualTo(5));
        }

        [Test]
        public void TraitsAreChipsInTheFootAndNeverThoughts()
        {
            WorldSnapshot snapshot = Frame();
            Lines(snapshot);
            Trait(snapshot, 0, TraitHandle.Cheerful, mood: 60);
            Trait(snapshot, 1, TraitHandle.Gloomy, mood: -60);
            Trait(snapshot, 2, TraitHandle.Diligent);

            ThoughtsTab tab = Pane(snapshot).Thoughts;
            Assert.That(tab.Lines, Is.Empty, "who she is is not a thought");
            Assert.That(tab.Chips.Select(c => c.Value), Is.EqualTo(new[] { "+6", "-6", "--" }));
            Assert.That(tab.Chips.Select(c => c.Tone), Is.EqualTo(new[] { ChipTone.Good, ChipTone.Bad, ChipTone.Neutral }));
            Assert.That(tab.Chips[0].Name, Is.EqualTo(Registry.Label("ui.trait.cheerful")));
            Assert.That(tab.Chips[2].Tip, Does.StartWith(Registry.Describe("ui.trait.diligent")),
                "the description, then what it does");
            Assert.That(tab.Breakdown[2].Value, Is.EqualTo("+0"), "their moods cancel in the breakdown");
        }

        [Test]
        public void ChipsThatDoNotFitFoldIntoMore()
        {
            WorldSnapshot snapshot = Frame();
            Lines(snapshot);
            Trait(snapshot, 0, TraitHandle.Cheerful, mood: 60);
            Trait(snapshot, 1, TraitHandle.Gloomy, mood: -60);
            Trait(snapshot, 2, TraitHandle.QuickStudy);

            InspectModel pane = new InspectModel();
            pane.Thoughts.ChipRoom = 200;
            pane.SetColonist(Ada);
            pane.Refresh(snapshot);
            TraitChip[] chips = pane.Thoughts.Chips.ToArray();

            Assert.That(chips.Length, Is.EqualTo(2));
            Assert.That(chips[1].Name, Is.EqualTo("+2 " + Registry.Label("ui.mind.more")));
            Assert.That(chips[1].Tone, Is.EqualTo(ChipTone.Neutral));
            Assert.That(chips[1].Tip, Is.EqualTo(Registry.Label("ui.trait.gloomy") + "\n" + Registry.Label("ui.trait.quickstudy")),
                "its tooltip lists the rest");

            // The control: at the pane's own width all three fit.
            Assert.That(Pane(snapshot).Thoughts.Chips.Count, Is.EqualTo(3));
        }

        [Test]
        public void TheTabFitsItsBodyAndSetsItForEveryTab()
        {
            Assert.That(ThoughtsLayout.LeftContent, Is.LessThanOrEqualTo(ThoughtsLayout.Upper),
                "the meter, the target line and the breakdown fit above the traits strip");
            Assert.That(ThoughtsLayout.TableContent, Is.LessThanOrEqualTo(ThoughtsLayout.Upper),
                "the header and five rows fit above the traits strip");
            Assert.That(HudLayout.InspectTabBody, Is.EqualTo(ThoughtsLayout.TabBody),
                "one body for every tab, so switching to Thoughts moves nothing");
            Assert.That(ThoughtsLayout.ChipRoom, Is.GreaterThan(3 * ThoughtsLayout.ChipWidth("Quick study", "+12")),
                "three traits fit at the pane's width, so +N more is the exception");
        }

        [Test]
        public void PointsAndTimesAreWrittenTheWayTheScreenCanDrawThem()
        {
            Assert.That(MindCatalogue.Points(-80), Is.EqualTo("-8"));
            Assert.That(MindCatalogue.Points(35), Is.EqualTo("+4"), "half away from nought");
            Assert.That(MindCatalogue.Points(-138), Is.EqualTo("-14"));
            Assert.That(MindCatalogue.Points(4), Is.EqualTo("0"));
            Assert.That(MindCatalogue.Lasts(1), Is.EqualTo("1 " + Registry.Label("ui.mind.hour")),
                "a memory with a tick left reads an hour, not nought");
            Assert.That(MindCatalogue.Lasts(GameClock.TicksPerDay), Is.EqualTo("1 " + Registry.Label("ui.mind.day")));
            Assert.That(MindCatalogue.Lasts(GameClock.TicksPerDay * 3), Is.EqualTo("3 " + Registry.Label("ui.mind.days")));
            foreach (char c in MindCatalogue.Points(-80) + MindCatalogue.Lasts(5000) + MindCatalogue.Lasts(GameClock.TicksPerDay * 2))
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
