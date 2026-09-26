#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The storyteller's interface (design 59 §12, Claude Design's mockups 25a–25h): the catalogue,
    /// the choice and its one rule for what a press does, the tension gauge, the pause switch, the
    /// clock's date and the Gameplay tab's fit. What the Presentation tier draws from these is the
    /// PlayMode tier's; everything a model can answer is here.
    /// </summary>
    public class StoryTests
    {
        // ------------------------------------------------------------------ the catalogue

        [Test]
        public void TheStorytellersAreJacobTrentAndKanoInThatOrder()
        {
            string[] keys = { "ui.storyteller.jacob", "ui.storyteller.trent", "ui.storyteller.kano" };
            Assert.That(StoryCatalogue.Tellers.Count, Is.EqualTo(keys.Length));
            for (int i = 0; i < keys.Length; i++)
                Assert.That(StoryCatalogue.Tellers[i].Key, Is.EqualTo(keys[i]));
            Assert.That(StoryCatalogue.Tellers[0].Label, Is.EqualTo("Jacob"));
        }

        [Test]
        public void EveryStorytellerHasABlurbTheCardCanRead()
        {
            foreach (StoryCatalogue.Teller t in StoryCatalogue.Tellers)
            {
                Assert.That(t.Blurb, Is.Not.Empty, $"{t.Key} would draw a card with no blurb");
                Assert.That(t.Blurb, Is.Not.EqualTo(t.Key), $"{t.Key}'s description is not generated");
            }
        }

        [Test]
        public void TheDefaultsAreJacobAtNormal()
        {
            StoryChoice d = StoryChoice.Default;
            Assert.That(StoryCatalogue.TellerAt(d.Teller).Label, Is.EqualTo("Jacob"));
            Assert.That(StoryCatalogue.RungAt(d.Rung).Label, Is.EqualTo("Normal"));
            Assert.That(d.ThreatPercent, Is.EqualTo(100));
            Assert.That(d.BigThreats, Is.True);
        }

        [Test]
        public void TheLadderIsSevenRungsEndingInCustom()
        {
            string[] labels = { "Peaceful", "Gentle", "Easy", "Normal", "Hard", "Brutal", "Custom" };
            Assert.That(StoryCatalogue.Rungs.Count, Is.EqualTo(labels.Length));
            for (int i = 0; i < labels.Length; i++)
                Assert.That(StoryCatalogue.Rungs[i].Label, Is.EqualTo(labels[i]));
            Assert.That(StoryCatalogue.CustomRung, Is.EqualTo(labels.Length - 1));
            Assert.That(StoryCatalogue.Rungs[0].BigThreats, Is.False, "Peaceful sends no big threats");
        }

        [Test]
        public void EveryEmblemParsesAndStaysInsideItsBox()
        {
            foreach (StoryCatalogue.Teller t in StoryCatalogue.Tellers)
            {
                IReadOnlyList<SvgPath.Subpath> paths = SvgPath.Parse(t.Emblem);
                Assert.That(paths.Count, Is.GreaterThan(0), t.Key);
                foreach (SvgPath.Subpath p in paths)
                    for (int i = 0; i < p.Count; i++)
                    {
                        Assert.That(p.Points[2 * i], Is.InRange(0f, 24f), $"{t.Key} leaves its 24 box");
                        Assert.That(p.Points[2 * i + 1], Is.InRange(0f, 24f), $"{t.Key} leaves its 24 box");
                    }
            }
        }

        [Test]
        public void EveryRhythmMarkFitsTheStrip()
        {
            foreach (StoryCatalogue.Teller t in StoryCatalogue.Tellers)
                foreach (StoryCatalogue.Mark m in t.Marks)
                {
                    Assert.That(m.Day, Is.InRange(0, 24), t.Key);
                    float x = StoryCatalogue.MarkX(m.Day);
                    Assert.That(x, Is.InRange(StoryCatalogue.BaselineStart, StoryCatalogue.BaselineEnd), t.Key);
                    Assert.That(StoryCatalogue.BaselineY - m.Size, Is.GreaterThanOrEqualTo(0f),
                        $"{t.Key}'s day {m.Day} mark would stand out of the top of the strip");
                }
        }

        [Test]
        public void TheStretchReadsAsTheMockupWritesIt()
        {
            Assert.That(StoryCatalogue.Stretch(50), Is.EqualTo("x0.5"));
            Assert.That(StoryCatalogue.Stretch(100), Is.EqualTo("x1"));
            Assert.That(StoryCatalogue.Stretch(125), Is.EqualTo("x1.25"));
            Assert.That(StoryCatalogue.Stretch(85), Is.EqualTo("x0.85"));
            Assert.That(StoryCatalogue.Stretch(200), Is.EqualTo("x2"));
        }

        // ------------------------------------------------------------------ the choice

        [Test]
        public void ARungSetsItsFourLeversAndCustomKeepsWhatTheyRead()
        {
            StoryChoice hard = StoryChoice.Default.WithRung(4);
            Assert.That(hard.ThreatPercent, Is.EqualTo(150));
            Assert.That(hard.AdaptationPercent, Is.EqualTo(75));
            Assert.That(hard.GraceHundredths, Is.EqualTo(85));

            StoryChoice custom = hard.WithRung(StoryCatalogue.CustomRung);
            Assert.That(custom.ThreatPercent, Is.EqualTo(150), "Custom starts from the rung you were looking at");
            Assert.That(custom.GraceHundredths, Is.EqualTo(85));
        }

        [Test]
        public void ALeverMovesOnlyOnCustom()
        {
            StoryChoice normal = StoryChoice.Default;
            Assert.That(normal.WithThreat(300), Is.EqualTo(normal), "the dimmed block is not pressable");
            Assert.That(normal.WithBigThreats(false), Is.EqualTo(normal));

            StoryChoice custom = normal.WithRung(StoryCatalogue.CustomRung);
            Assert.That(custom.WithThreat(300).ThreatPercent, Is.EqualTo(300));
            Assert.That(custom.WithThreat(9999).ThreatPercent, Is.EqualTo(StoryCatalogue.ThreatMax));
            Assert.That(custom.WithAdaptation(-5).AdaptationPercent, Is.EqualTo(StoryCatalogue.AdaptationMin));
            Assert.That(custom.WithGrace(10).GraceHundredths, Is.EqualTo(StoryCatalogue.GraceMin));
            Assert.That(custom.WithBigThreats(false).BigThreats, Is.False);
        }

        [Test]
        public void TheCustomNoteNamesTheRungThatSetTheLevers()
        {
            Assert.That(StoryCatalogue.CustomNote(4), Is.EqualTo("Set by Hard. Pick Custom to change."));
            Assert.That(StoryCatalogue.CustomNote(StoryCatalogue.CustomRung), Is.EqualTo("Your own settings"));
        }

        [Test]
        public void StartCarriesTheStory()
        {
            var select = new ColonistSelect(
                (seed, slot) => new Candidate(seed, "person-" + seed, 30, "Scrapper", System.Array.Empty<SkillRow>()));
            var menu = new MenuDirector(new SeedField(() => 4242u), select);
            menu.Show();
            menu.Choose(SessionCommands.NewGameKey);

            Assert.That(menu.Story, Is.EqualTo(StoryChoice.Default));
            int changes = 0;
            menu.Changed += () => changes++;
            menu.ChooseStoryteller(2);
            menu.ChooseRung(StoryCatalogue.CustomRung);
            menu.SetThreat(250);
            Assert.That(changes, Is.EqualTo(3));

            NewGameChoice chosen = default;
            menu.StartRequested += c => chosen = c;
            Assert.That(menu.Start(), Is.True);
            Assert.That(chosen.Story.Teller, Is.EqualTo(2));
            Assert.That(chosen.Story.IsCustom, Is.True);
            Assert.That(chosen.Story.ThreatPercent, Is.EqualTo(250));
        }

        [Test]
        public void TheSessionsStoryIsLiveOnlyWithAColony()
        {
            var story = new StoryDirector();
            Assert.That(story.Choice.HasTeller, Is.False, "a session starts with no storyteller");
            Assert.That(story.ChooseStoryteller(1), Is.False, "nothing to change with no colony open");

            story.Begin(StoryChoice.Default);
            Assert.That(story.ChooseStoryteller(1), Is.True);
            Assert.That(story.Choice.Teller, Is.EqualTo(1));

            story.End();
            Assert.That(story.HasColony, Is.False);
            Assert.That(story.Choice.HasTeller, Is.False);
        }

        // ------------------------------------------------------------------ the gauge

        [Test]
        public void TheBandsDifferByHowManyColumnsAreFilled()
        {
            for (int band = 0; band < TensionModel.BandCount; band++)
                Assert.That(TensionModel.FilledOf(band), Is.EqualTo(band));
            string[] names = { "Reeling", "Easing", "Even", "Building", "Peak" };
            for (int band = 0; band < names.Length; band++)
                Assert.That(TensionModel.LabelOf(band), Is.EqualTo(names[band]));
        }

        [Test]
        public void NoBandIsEverTheAlertRed()
        {
            for (int band = 0; band < TensionModel.BandCount; band++)
                Assert.That(TensionModel.TintOf(band), Is.Not.EqualTo(HudTheme.Bad),
                    $"band {band} would read as an alert");
        }

        [Test]
        public void EveryBandsPathParsesAndStandsOnTheBaseline()
        {
            for (int band = 0; band < TensionModel.BandCount; band++)
            {
                IReadOnlyList<SvgPath.Subpath> columns = SvgPath.Parse(TensionModel.PathOf(band));
                Assert.That(columns.Count, Is.EqualTo(4));
                for (int c = 0; c < columns.Count; c++)
                {
                    float lowest = 0f, highest = TensionModel.Box;
                    SvgPath.Subpath p = columns[c];
                    for (int i = 0; i < p.Count; i++)
                    {
                        lowest = System.Math.Max(lowest, p.Points[2 * i + 1]);
                        highest = System.Math.Min(highest, p.Points[2 * i + 1]);
                    }
                    Assert.That(lowest, Is.EqualTo(TensionModel.Bottom).Within(0.001f));
                    float expected = c < band ? TensionModel.ColumnHeight[c] : TensionModel.Stub;
                    Assert.That(lowest - highest, Is.EqualTo(expected).Within(0.001f),
                        $"band {band} column {c}");
                }
            }
        }

        [Test]
        public void TheTooltipIsTheBandThenTheCause()
        {
            Assert.That(TensionModel.Tooltip(1, TensionCause.Died, 3), Is.EqualTo("Easing\nA colonist died, 3 days ago"));
            Assert.That(TensionModel.CauseLine(TensionCause.Downed, 1), Is.EqualTo("A colonist was downed, 1 day ago"));
            Assert.That(TensionModel.CauseLine(TensionCause.Quiet, 9), Is.EqualTo("9 quiet days"));
            Assert.That(TensionModel.CauseLine(TensionCause.Quiet, 1), Is.EqualTo("One quiet day"));
            Assert.That(TensionModel.CauseLine(TensionCause.Died, 0), Is.EqualTo("A colonist died, today"));
        }

        [Test]
        public void TheTooltipIsBuiltOnlyWhenItChanges()
        {
            var tip = new TensionTip();
            string first = tip.For(2, TensionCause.Quiet, 4);
            Assert.That(tip.For(2, TensionCause.Quiet, 4), Is.SameAs(first));
            Assert.That(tip.For(3, TensionCause.Quiet, 4), Is.Not.SameAs(first));
        }

        [Test]
        public void TheGaugeShowsOnlyWithAStorytellerAndABand()
        {
            var story = new StoryDirector();
            story.SetTensionPreview(2);
            Assert.That(story.GaugeShows, Is.False, "no storyteller, no gauge (an old save)");

            story.Begin(StoryChoice.Default);
            Assert.That(story.GaugeShows, Is.True);

            story.NextTensionPreview();
            story.NextTensionPreview();
            story.NextTensionPreview();
            Assert.That(story.TensionPreview, Is.EqualTo(TensionModel.NoBand), "Peak steps back to Off");
            Assert.That(story.GaugeShows, Is.False);
        }

        // ------------------------------------------------------------------ the pause switch

        [Test]
        public void PauseOnBigThreatsIsOnByDefaultStoredAndResetWithGameplay()
        {
            var settings = new SettingsDirector();
            Assert.That(settings.PauseOnBigThreats, Is.True);

            var store = new FakeSettingsStore();
            store.Preset(SettingsDirector.PauseOnBigThreatsKey, false);
            settings.UseStore(store);
            Assert.That(settings.PauseOnBigThreats, Is.False, "the machine's choice is read back");

            settings.ResetTab(SettingsTab.Gameplay);
            Assert.That(settings.PauseOnBigThreats, Is.True);
            Assert.That(store.Read(SettingsDirector.PauseOnBigThreatsKey), Is.True, "and the reset is written");
        }

        // ------------------------------------------------------------------ the clock

        [Test]
        public void TheDateDropsTheSeasonAndTheTooltipsCarryIt()
        {
            long tick = 11L * GameClock.TicksPerDay;
            string season = GameClock.SeasonName(tick);
            Assert.That(GameClock.DateLine(tick), Is.EqualTo("Day 12 · " + GameClock.MonthName(tick)));
            Assert.That(GameClock.DateLine(tick), Does.Not.Contain(season));
            Assert.That(GameClock.FullDate(tick), Does.EndWith(" · " + season));
            Assert.That(GameClock.WeatherTip("Rain", tick), Is.EqualTo("Rain · " + season));
            Assert.That(GameClock.WeatherTip(string.Empty, tick), Is.EqualTo(season));
        }

        // ------------------------------------------------------------------ the Gameplay tab

        [Test]
        public void TheGameplayTabFitsWithCustomOpen()
        {
            // Left: Saving (autosave) and Pausing (the switch). Right: Story, with the storyteller,
            // the difficulty and Custom's four levers under it.
            int left = SettingsLayout.ColumnHeight(new[] { 1, 1 });
            int right = SettingsLayout.ColumnHeight(new[] { 2 + 4 });
            Assert.That(left, Is.LessThanOrEqualTo(SettingsLayout.ColumnsHeight));
            Assert.That(right, Is.LessThanOrEqualTo(SettingsLayout.ColumnsHeight),
                $"the Story column is {right} px against {SettingsLayout.ColumnsHeight} px of room");
        }

        [Test]
        public void TheFullNewGamePageLeavesTheStoryBlockRoomForThreeCards()
        {
            // At 100% scale the page's content is 1920 less its 24 inset and 40 padding each side.
            int content = 1920 - 2 * 24 - 2 * 40;
            Assert.That(content, Is.GreaterThanOrEqualTo(HudLayout.SetupCompactBelow),
                "the page would go compact at the scale it is designed at");
            int story = content - HudLayout.ColonistColumnWidth - HudLayout.SetupSkillsWidth - 2 * HudLayout.SetupColumnGap;
            Assert.That(story, Is.EqualTo(660), "mockup 25a: the third column is about 660 wide");
        }

        [Test]
        public void EveryStoryKeyIsARegisteredName()
        {
            foreach (string key in StoryCatalogue.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
            foreach (string key in TensionModel.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
        }
    }
}
