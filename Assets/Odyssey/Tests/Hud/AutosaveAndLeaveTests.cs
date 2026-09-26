#nullable enable
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The autosave's clock (2026-09-21).
    ///
    /// Owner: "a suggestion of a autosave would be good - whatever you recommend - and have it on
    /// by default saving to the same game."
    /// </summary>
    public class AutosaveClockTests
    {
        /// <summary>One game day in ticks, as <c>GameClock</c> counts it.</summary>
        const long Day = 60_000;

        [Test]
        public void ItIsOnEveryDayBeforeAnybodyTouchesIt()
        {
            Assert.That(AutosaveClock.DefaultDays, Is.EqualTo(1));
            Assert.That(new SettingsDirector().AutosaveDays, Is.EqualTo(1));
            Assert.That(AutosaveClock.IsRung(0), Is.True, "off is a rung, not a second control");
        }

        [Test]
        public void TheDayItArrivesOnCountsAsAlreadySaved()
        {
            // A save opened and left alone must not be written straight back over the file it came
            // out of: the colony has not moved, and the write would spend the previous copy.
            var clock = new AutosaveClock();
            clock.Begin(3 * Day + 500);

            Assert.That(clock.Due(3 * Day + 900, 1), Is.False);
            Assert.That(clock.Due(4 * Day, 1), Is.True, "the next day is a save");
        }

        [Test]
        public void AClockNobodyStartedPrimesItselfRatherThanSavingAtOnce()
        {
            var clock = new AutosaveClock();
            Assert.That(clock.LastDay, Is.Null);
            Assert.That(clock.Due(0, 1), Is.False, "the first frame of a colony is not a save");
            Assert.That(clock.LastDay, Is.EqualTo(1));
        }

        [Test]
        public void OneSaveABoundaryHoweverOftenItIsAsked()
        {
            // It is asked every frame, so answering yes twice on one day would be a write per
            // frame for a whole day.
            var clock = new AutosaveClock();
            clock.Begin(0);

            Assert.That(clock.Due(Day, 1), Is.True);
            for (int frame = 0; frame < 60; frame++)
                Assert.That(clock.Due(Day + frame * 10, 1), Is.False, "asked again on the same day");
        }

        [Test]
        public void ABoundaryCrossedInsideOneFrameIsStillABoundary()
        {
            // At 3x a frame retires several ticks, and a rule watching for the tick where the day
            // changed would miss the day a batch stepped over midnight.
            var clock = new AutosaveClock();
            clock.Begin(0);
            Assert.That(clock.Due(2 * Day + 17, 1), Is.True);
        }

        [Test]
        public void TwoDaysMeansTwoDays()
        {
            var clock = new AutosaveClock();
            clock.Begin(0);

            Assert.That(clock.Due(Day, 2), Is.False);
            Assert.That(clock.Due(2 * Day, 2), Is.True);
            Assert.That(clock.Due(3 * Day, 2), Is.False);
            Assert.That(clock.Due(4 * Day, 2), Is.True);
        }

        [Test]
        public void OffIsOffHoweverManyDaysPass()
        {
            var clock = new AutosaveClock();
            clock.Begin(0);
            Assert.That(clock.Due(40 * Day, 0), Is.False);
        }

        [Test]
        public void AClockThatWentBackwardsDoesNotSave()
        {
            // An older save opened into a running session. Nothing has been played, and writing
            // over it would spend the one previous copy for nothing.
            var clock = new AutosaveClock();
            clock.Begin(10 * Day);
            Assert.That(clock.Due(2 * Day, 1), Is.False);
        }

        [Test]
        public void ANewColonyStartsItsOwnCount()
        {
            var clock = new AutosaveClock();
            clock.Begin(10 * Day);
            clock.Forget();

            Assert.That(clock.LastDay, Is.Null);
            Assert.That(clock.Due(0, 1), Is.False, "the next colony primes rather than inheriting");
        }

        [Test]
        public void TheIntervalSnapsToARungAndIsRemembered()
        {
            var settings = new SettingsDirector();
            var store = new RecordingStore();
            settings.UseStore(store);

            settings.SetAutosaveDays(2);
            Assert.That(settings.AutosaveDays, Is.EqualTo(2));
            Assert.That(store.Ints[SettingsDirector.AutosaveKey], Is.EqualTo(2));

            settings.SetAutosaveDays(9);
            Assert.That(settings.AutosaveDays, Is.EqualTo(3), "snapped to the highest rung it offers");
        }

        sealed class RecordingStore : ISettingsStore
        {
            public readonly System.Collections.Generic.Dictionary<string, int> Ints = new();

            public bool? Read(string key) => null;
            public void Write(string key, bool value) { }
            public int? ReadInt(string key) => null;
            public void WriteInt(string key, int value) => Ints[key] = value;
            public string? ReadString(string key) => null;
            public void WriteString(string key, string value) { }
        }
    }

    /// <summary>
    /// The question asked on the way out of a colony (2026-09-21).
    ///
    /// Owner: "when you quit the game (to main menu or to desktop) it should confirm to save
    /// before you exit to be sure."
    /// </summary>
    public class LeavePromptTests
    {
        [Test]
        public void ItAsksWhereItIsGoingAndWhatSavingWouldWriteTo()
        {
            var prompt = new LeavePrompt();

            Assert.That(prompt.Ask(LeaveTo.Desktop, "ashford"), Is.True);
            Assert.That(prompt.Showing, Is.True);
            Assert.That(prompt.Destination, Is.EqualTo(LeaveTo.Desktop));
            Assert.That(prompt.Target, Is.EqualTo("ashford"));
            Assert.That(prompt.TitleKey, Is.EqualTo(LeavePrompt.ToDesktopTitleKey));
        }

        [Test]
        public void WithNothingToLoseThereIsNothingToAsk()
        {
            // Ask() is the colony form; the title screen asks through AskToExit instead.
            var prompt = new LeavePrompt();
            Assert.That(prompt.Ask(LeaveTo.MainMenu, null), Is.False);
            Assert.That(prompt.Ask(LeaveTo.MainMenu, "  "), Is.False);
            Assert.That(prompt.Showing, Is.False);
        }

        /// <summary>The title screen's Exit game (design 40): the same prompt with nothing to
        /// save. It goes to the desktop, and a press on the save answer cannot write anything.</summary>
        [Test]
        public void ExitingFromTheTitleScreenAsksWithNothingToSave()
        {
            var prompt = new LeavePrompt();
            LeaveTo? went = null;
            bool? saved = null;
            prompt.Confirmed += (to, save) => { went = to; saved = save; };

            prompt.AskToExit();
            Assert.That(prompt.Showing, Is.True);
            Assert.That(prompt.HasColony, Is.False);
            Assert.That(prompt.TitleKey, Is.EqualTo(LeavePrompt.ToDesktopTitleKey));

            prompt.Choose(save: true);
            Assert.That(went, Is.EqualTo(LeaveTo.Desktop));
            Assert.That(saved, Is.False, "there is no colony to write");

            Assert.That(prompt.Ask(LeaveTo.MainMenu, "ashford"), Is.True);
            Assert.That(prompt.HasColony, Is.True, "a colony question after an exit question forgot the colony");
        }

        [Test]
        public void EachOfTheThreeAnswersDoesOneThing()
        {
            var prompt = new LeavePrompt();
            LeaveTo? went = null;
            bool saved = false;
            int times = 0;
            prompt.Confirmed += (to, save) => { went = to; saved = save; times++; };

            prompt.Ask(LeaveTo.MainMenu, "ashford");
            prompt.Cancel();
            Assert.That(times, Is.Zero, "staying is not leaving");
            Assert.That(prompt.Showing, Is.False);

            prompt.Ask(LeaveTo.MainMenu, "ashford");
            prompt.Choose(save: false);
            Assert.That(times, Is.EqualTo(1));
            Assert.That(went, Is.EqualTo(LeaveTo.MainMenu));
            Assert.That(saved, Is.False);

            prompt.Ask(LeaveTo.Desktop, "ashford");
            prompt.Choose(save: true);
            Assert.That(times, Is.EqualTo(2));
            Assert.That(went, Is.EqualTo(LeaveTo.Desktop));
            Assert.That(saved, Is.True);
        }

        [Test]
        public void ItComesDownBeforeTheAnswerIsActedOn()
        {
            // The presenter tears the world down on this event, and a prompt still up would be
            // left hanging over the screen that replaces it.
            var prompt = new LeavePrompt();
            bool showingWhenHeard = true;
            prompt.Confirmed += (_, __) => showingWhenHeard = prompt.Showing;

            prompt.Ask(LeaveTo.MainMenu, "ashford");
            prompt.Choose(save: true);

            Assert.That(showingWhenHeard, Is.False);
        }

        [Test]
        public void AnswersMeanNothingWhileItIsNotUp()
        {
            var prompt = new LeavePrompt();
            int times = 0;
            prompt.Confirmed += (_, __) => times++;

            Assert.That(prompt.Choose(save: true), Is.False);
            Assert.That(times, Is.Zero);
        }

        [Test]
        public void EveryWordOnItComesOutOfTheRegistry()
        {
            foreach (string key in LeavePrompt.IconKeys)
                Assert.That(Registry.Label(key), Is.Not.EqualTo(key), $"{key} has no name");
        }
    }
}
