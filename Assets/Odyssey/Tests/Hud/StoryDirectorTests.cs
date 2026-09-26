#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The storyteller's interface as a read of the simulation (design 68 §7, §12): a press sends
    /// the intent the simulation's handler reads, the view decides what is shown, a refused press
    /// goes back, and the gauge follows the view unless the debug menu is previewing a band.
    /// </summary>
    public class StoryDirectorTests
    {
        static (StoryDirector story, List<Intent> sent) Open()
        {
            var sent = new List<Intent>();
            var story = new StoryDirector { Submit = sent.Add };
            story.Attach();
            return (story, sent);
        }

        static StorytellerView ViewOf(StoryChoice c, int band = 2,
            TensionCauseKind cause = TensionCauseKind.None, int days = 0) =>
            new StorytellerView(c.Teller, c.Rung, c.ThreatPercent, c.BigThreats,
                c.AdaptationPercent, c.GraceHundredths, band, cause, days);

        // ------------------------------------------------------------------ the intents

        [Test]
        public void TheDifficultyIntentPacksTheFourLeversAsTheHandlerUnpacksThem()
        {
            var choice = new StoryChoice(StorytellerHandle.Kano, StoryCatalogue.CustomRung, 370, false, 140, 65);
            Intent intent = StoryDirector.DifficultyIntent(choice);

            // The handler's own unpacking, written out: Storyteller.HandleSetDifficulty.
            Assert.That(intent.Kind, Is.EqualTo(IntentKind.SetDifficulty));
            Assert.That(intent.A, Is.EqualTo(StoryCatalogue.CustomRung));
            Assert.That(intent.B & 0xFFFF, Is.EqualTo(370));
            Assert.That((intent.B >> 16) & 0xFFFF, Is.EqualTo(140));
            Assert.That(intent.C & 0xFFFF, Is.EqualTo(65));
            Assert.That((intent.C >> 16) & 1, Is.EqualTo(0));

            Intent big = StoryDirector.DifficultyIntent(choice.WithBigThreats(true));
            Assert.That((big.C >> 16) & 1, Is.EqualTo(1), "and the control: big threats on sets the bit");
        }

        [Test]
        public void EveryRungFitsTheBoundsTheSimulationChecks()
        {
            // HandleSetDifficulty refuses a rung over 15, a threat over 500, an adaptation over 200
            // and a grace outside 50-200. A rung the ladder offers that it refused would be a row
            // that jumps back every time it is pressed.
            for (int r = 0; r < StoryCatalogue.Rungs.Count; r++)
            {
                StoryChoice c = StoryChoice.Default.WithRung(r);
                Assert.That(c.Rung, Is.InRange(0, 15));
                Assert.That(c.ThreatPercent, Is.InRange(0, 500));
                Assert.That(c.AdaptationPercent, Is.InRange(0, 200));
                Assert.That(c.GraceHundredths, Is.InRange(50, 200));
            }
            Assert.That(StoryCatalogue.ThreatMax, Is.LessThanOrEqualTo(500));
            Assert.That(StoryCatalogue.AdaptationMax, Is.LessThanOrEqualTo(200));
            Assert.That(StoryCatalogue.GraceMin, Is.GreaterThanOrEqualTo(50));
            Assert.That(StoryCatalogue.GraceMax, Is.LessThanOrEqualTo(200));
        }

        [Test]
        public void ANewGameSendsTheStorytellerThenItsDifficulty()
        {
            var sent = new List<Intent>();
            var story = new StoryDirector { Submit = sent.Add };
            StoryChoice choice = StoryChoice.Default.WithTeller(StorytellerHandle.Trent).WithRung(4);
            story.Begin(choice);

            Assert.That(sent.Count, Is.EqualTo(2));
            Assert.That(sent[0].Kind, Is.EqualTo(IntentKind.SetStoryteller));
            Assert.That(sent[0].A, Is.EqualTo(StorytellerHandle.Trent));
            Assert.That(sent[1].Kind, Is.EqualTo(IntentKind.SetDifficulty));
            Assert.That(sent[1].A, Is.EqualTo(4));
            Assert.That(story.Choice, Is.EqualTo(choice), "shown at once, before the view has it");
            Assert.That(story.HasColony, Is.True);
        }

        [Test]
        public void APressSendsOnlyWhatMoved()
        {
            var (story, sent) = Open();
            story.Sync(ViewOf(StoryChoice.Default));

            story.ChooseStoryteller(StorytellerHandle.Kano);
            Assert.That(sent.Count, Is.EqualTo(1));
            Assert.That(sent[0].Kind, Is.EqualTo(IntentKind.SetStoryteller));

            sent.Clear();
            story.ChooseRung(1);
            Assert.That(sent.Count, Is.EqualTo(1));
            Assert.That(sent[0].Kind, Is.EqualTo(IntentKind.SetDifficulty));

            sent.Clear();
            Assert.That(story.ChooseRung(1), Is.False, "the same rung again is not a press");
            Assert.That(sent, Is.Empty);
        }

        /// <summary>
        /// An old save has no storyteller, and the simulation publishes no difficulty without one:
        /// a rung picked then snapped back to Normal as if refused and came true, unannounced, when
        /// a storyteller was chosen. The difficulty waits for a storyteller instead.
        /// </summary>
        [Test]
        public void WithNoStorytellerTheDifficultyWaits()
        {
            var (story, sent) = Open();
            story.Sync(ViewOf(StoryChoice.Nobody));
            Assert.That(story.DifficultyLive, Is.False);
            Assert.That(story.ChooseRung(4), Is.False);
            Assert.That(story.SetThreat(250), Is.False);
            Assert.That(sent, Is.Empty, "a difficulty was sent to a colony with no storyteller");

            Assert.That(story.ChooseStoryteller(StorytellerHandle.Jacob), Is.True);
            Assert.That(sent.Count, Is.EqualTo(1), "choosing a storyteller sent a difficulty nobody picked");
            Assert.That(story.DifficultyLive, Is.True);
            Assert.That(story.ChooseRung(4), Is.True);
        }

        [Test]
        public void WithNoColonyAPressDoesNothing()
        {
            var sent = new List<Intent>();
            var story = new StoryDirector { Submit = sent.Add };
            Assert.That(story.ChooseStoryteller(StorytellerHandle.Jacob), Is.False);
            Assert.That(sent, Is.Empty);
            Assert.That(story.Choice.HasTeller, Is.False);
        }

        // ------------------------------------------------------------------ the view

        [Test]
        public void TheViewDecidesWhatIsShown()
        {
            var (story, _) = Open();
            StoryChoice saved = StoryChoice.Default.WithTeller(StorytellerHandle.Kano).WithRung(5);
            int changed = 0;
            story.Changed += () => changed++;

            story.Sync(ViewOf(saved));
            Assert.That(story.Choice, Is.EqualTo(saved));
            Assert.That(changed, Is.EqualTo(1));

            story.Sync(ViewOf(saved));
            Assert.That(changed, Is.EqualTo(1), "an unchanged view raises nothing");
        }

        [Test]
        public void APressIsShownUntilTheViewCatchesUpAndARefusedOneGoesBack()
        {
            var (story, _) = Open();
            StoryChoice before = StoryChoice.Default;
            story.Sync(ViewOf(before));

            story.ChooseRung(5);
            StoryChoice pressed = story.Choice;
            Assert.That(pressed.Rung, Is.EqualTo(5));

            // The view has not caught up yet: the press stays on screen rather than flickering.
            story.Sync(ViewOf(before));
            Assert.That(story.Choice, Is.EqualTo(pressed));

            // The view agrees: the press is settled, and the view is followed from here.
            story.Sync(ViewOf(pressed));
            Assert.That(story.Choice, Is.EqualTo(pressed));

            // A second press the simulation never takes goes back once the window has passed.
            story.ChooseRung(1);
            for (int i = 0; i <= StoryDirector.PendingSyncs; i++) story.Sync(ViewOf(pressed));
            Assert.That(story.Choice, Is.EqualTo(pressed));
        }

        [Test]
        public void ANoneViewReadsAsNoStoryteller()
        {
            var (story, _) = Open();
            story.Sync(StorytellerView.None);
            Assert.That(story.Choice.HasTeller, Is.False);
            Assert.That(story.Choice, Is.EqualTo(StoryChoice.Nobody),
                "the view's None and the interface's Nobody are the same Normal levers");
            Assert.That(story.GaugeShows, Is.False);
        }

        // ------------------------------------------------------------------ the old-save notice

        [Test]
        public void ALoadWithNoStorytellerIsToldOnce()
        {
            var (story, _) = Open();
            story.Sync(StorytellerView.None);
            Assert.That(story.TakeNoStorytellerNotice(), Is.True);
            story.Sync(StorytellerView.None);
            Assert.That(story.TakeNoStorytellerNotice(), Is.False, "once per colony");
        }

        [Test]
        public void ALoadWithAStorytellerOrANewGameIsNotTold()
        {
            var (loaded, _) = Open();
            loaded.Sync(ViewOf(StoryChoice.Default));
            Assert.That(loaded.TakeNoStorytellerNotice(), Is.False);

            // A new game: Attach from the session, then Begin, then the first sync arrives before
            // the intents have drained and the view still says none.
            var fresh = new StoryDirector { Submit = _ => { } };
            fresh.Attach();
            fresh.Begin(StoryChoice.Default);
            fresh.Sync(StorytellerView.None);
            Assert.That(fresh.TakeNoStorytellerNotice(), Is.False);
            Assert.That(fresh.Choice, Is.EqualTo(StoryChoice.Default), "and the choice is not undone");
        }

        [Test]
        public void TheNoticeKeyIsOnTheToastStacksList()
        {
            Assert.That(ToastModel.IconKeys, Does.Contain(ToastModel.NoStorytellerKey));
            Assert.That(Registry.Label(ToastModel.NoStorytellerKey), Does.Contain("Settings"));
        }

        // ------------------------------------------------------------------ the gauge

        [Test]
        public void TheGaugeFollowsTheViewsBandAndCause()
        {
            var (story, _) = Open();
            int repaints = 0;
            story.TensionChanged += () => repaints++;

            story.Sync(ViewOf(StoryChoice.Default, band: 0, cause: TensionCauseKind.Died, days: 2));
            Assert.That(story.GaugeShows, Is.True);
            Assert.That(story.ShownBand, Is.EqualTo(0));
            Assert.That(story.ShownCause, Is.EqualTo(TensionCause.Died));
            Assert.That(story.ShownCauseDays, Is.EqualTo(2));
            Assert.That(repaints, Is.EqualTo(1));

            story.Sync(ViewOf(StoryChoice.Default, band: 0, cause: TensionCauseKind.Died, days: 2));
            Assert.That(repaints, Is.EqualTo(1), "the same band does not repaint");
        }

        [Test]
        public void ThePreviewWinsOnlyWhileSet()
        {
            var (story, _) = Open();
            story.Sync(ViewOf(StoryChoice.Default, band: 1, cause: TensionCauseKind.Downed, days: 1));

            story.SetTensionPreview(4);
            Assert.That(story.ShownBand, Is.EqualTo(4));
            Assert.That(story.ShownCauseDays, Is.EqualTo(StoryDirector.PreviewDays));

            story.SetTensionPreview(TensionModel.NoBand);
            Assert.That(story.ShownBand, Is.EqualTo(1));
            Assert.That(story.ShownCause, Is.EqualTo(TensionCause.Downed));
        }

        [Test]
        public void TheCausesAreTheSameFourInTheSameOrder()
        {
            Assert.That((int)TensionCause.None, Is.EqualTo((int)TensionCauseKind.None));
            Assert.That((int)TensionCause.Died, Is.EqualTo((int)TensionCauseKind.Died));
            Assert.That((int)TensionCause.Downed, Is.EqualTo((int)TensionCauseKind.Downed));
            Assert.That((int)TensionCause.Quiet, Is.EqualTo((int)TensionCauseKind.Quiet));
        }
    }
}
