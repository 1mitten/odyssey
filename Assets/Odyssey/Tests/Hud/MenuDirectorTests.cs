#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The main screen with no screen: which of its two screens is up, what each row asks for, and
    /// the ask-twice arming.
    ///
    /// <para>Every rule here is a promise the screen makes with the player watching — "that press
    /// armed the row", "backing out of the load list forgets the question you were asked" — and the
    /// director raises rather than performs precisely so this tier can hold it. The alternative is
    /// judging each one once per milestone with a finger on the mouse.</para>
    /// </summary>
    public class MenuDirectorTests
    {
        static MenuDirector Showing()
        {
            var menu = new MenuDirector();
            menu.Show();
            return menu;
        }

        static IReadOnlyList<SaveRow> TwoSaves() => new[]
        {
            new SaveRow("a", "Ashfall", 12, "Meadow"),
            new SaveRow("b", "Bellwether", 3, "City"),
        };

        [Test]
        public void ItStartsAwayAndOnTheRootScreen()
        {
            var menu = new MenuDirector();

            Assert.That(menu.Showing, Is.False, "the main screen exists only when no session is built");
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Root));
            Assert.That(menu.Armed, Is.Null);
            Assert.That(menu.Saves, Is.Empty);
        }

        [Test]
        public void TheRootScreenDrawsTheSharedTableRatherThanAListOfItsOwn()
        {
            IReadOnlyList<SessionCommand> rows = MenuDirector.Rows;
            IReadOnlyList<SessionCommand> table = SessionCommands.For(SessionContext.MainScreen);

            Assert.That(rows, Has.Count.EqualTo(table.Count));
            for (int i = 0; i < rows.Count; i++)
            {
                Assert.That(rows[i].Key, Is.EqualTo(table[i].Key));
                Assert.That(rows[i].Label, Is.EqualTo(table[i].Label));
                Assert.That(rows[i].AsksTwice, Is.EqualTo(table[i].AsksTwice));
            }
        }

        [Test]
        public void NothingCanBePressedWhileTheScreenIsAway()
        {
            var menu = new MenuDirector();
            int asked = 0;
            menu.NewGameRequested += () => asked++;

            Assert.That(menu.Choose(SessionCommands.NewGameKey), Is.False);
            Assert.That(asked, Is.Zero);
        }

        // ------------------------------------------------------------------ what a row asks for

        [Test]
        public void NewGameAsksForAColonyAndNothingElse()
        {
            MenuDirector menu = Showing();
            int newGame = 0, saves = 0, settings = 0, quit = 0;
            menu.NewGameRequested += () => newGame++;
            menu.SavesRequested += () => saves++;
            menu.SettingsRequested += () => settings++;
            menu.QuitRequested += () => quit++;

            Assert.That(menu.Choose(SessionCommands.NewGameKey), Is.True);

            Assert.That(newGame, Is.EqualTo(1), "exactly once, on one press");
            Assert.That(saves + settings + quit, Is.Zero);
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Root),
                "building a world is the bootstrap's; this screen does not navigate for it");
        }

        [Test]
        public void OptionsAsksForTheSettingsPanelAndNothingElse()
        {
            MenuDirector menu = Showing();
            int newGame = 0, saves = 0, settings = 0, quit = 0;
            menu.NewGameRequested += () => newGame++;
            menu.SavesRequested += () => saves++;
            menu.SettingsRequested += () => settings++;
            menu.QuitRequested += () => quit++;

            Assert.That(menu.Choose(SessionCommands.OptionsKey), Is.True);

            Assert.That(settings, Is.EqualTo(1));
            Assert.That(newGame + saves + quit, Is.Zero);
        }

        /// <summary>
        /// Settings takes the menu's place rather than appearing on top of it, and Back returns.
        ///
        /// <para>The owner played it and reported the two panels stacking: *"when I clicked on
        /// settings - it appeared below the menu - it would [be] cleaner if main menu disappeared
        /// and settings appeared but could navigate back to main menu"*. Both are centred, so one
        /// landed over the other. Settings is a screen of this menu now.</para>
        ///
        /// <para><b>The order inside <c>Perform</c> matters and is asserted:</b> the screen has to
        /// move <i>before</i> the panel is asked for, or there is a frame in which the settings
        /// panel is up and the menu is still under it — which is the stacked look, arrived at a
        /// different way.</para>
        /// </summary>
        [Test]
        public void SettingsStandsInTheMenusPlaceAndBackReturnsToIt()
        {
            MenuDirector menu = Showing();
            var order = new List<string>();
            menu.ScreenChanged += screen => order.Add("screen:" + screen);
            menu.SettingsRequested += () => order.Add("panel");
            menu.SettingsClosed += () => order.Add("closed");

            Assert.That(menu.Choose(SessionCommands.OptionsKey), Is.True);
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Settings));
            Assert.That(order, Is.EqualTo(new List<string> { "screen:Settings", "panel" }),
                "the panel was asked for before the screen moved, so the menu is briefly under it");

            Assert.That(menu.Back(), Is.True);
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Root));
            Assert.That(order[order.Count - 1], Is.EqualTo("closed"),
                "leaving the settings screen did not ask for the panel to close");
        }

        /// <summary>
        /// No root row can be pressed from the settings screen — the same rule the load screen
        /// already has, and for the same reason: the rows are not on screen, so a press that
        /// reached them would be a press at nothing.
        /// </summary>
        [Test]
        public void NoRootRowIsPressableFromTheSettingsScreen()
        {
            MenuDirector menu = Showing();
            menu.Choose(SessionCommands.OptionsKey);

            int anything = 0;
            menu.NewGameRequested += () => anything++;
            menu.SavesRequested += () => anything++;
            menu.QuitRequested += () => anything++;

            Assert.That(menu.Choose(SessionCommands.NewGameKey), Is.False);
            Assert.That(menu.Choose(SessionCommands.LoadKey), Is.False);
            Assert.That(menu.Choose(SessionCommands.QuitKey), Is.False);
            Assert.That(anything, Is.Zero);
        }

        /// <summary>
        /// Leaving the <i>load</i> screen does not claim the settings panel was closed.
        ///
        /// <para>The negative control for <c>SettingsClosed</c>: a `Back()` that raised it from
        /// every screen would ask the presenter to close a panel that was never open, which on the
        /// start screen is harmless and on the day something else listens is not.</para>
        /// </summary>
        [Test]
        public void BackingOutOfTheLoadScreenDoesNotSayTheSettingsPanelClosed()
        {
            MenuDirector menu = Showing();
            int closed = 0;
            menu.SettingsClosed += () => closed++;

            menu.ShowSaves(new[] { new SaveRow("a", "Ashford", 4, "Natural") });
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Load));
            Assert.That(menu.Back(), Is.True);

            Assert.That(closed, Is.Zero, "backing out of the load screen closed a settings panel");
        }

        [Test]
        public void ARowTheMainScreenDoesNotDrawDoesNothing()
        {
            MenuDirector menu = Showing();
            int anything = 0;
            menu.NewGameRequested += () => anything++;
            menu.SavesRequested += () => anything++;
            menu.SettingsRequested += () => anything++;
            menu.QuitRequested += () => anything++;

            Assert.That(menu.Choose(SessionCommands.SaveKey), Is.False, "there is no world to save");
            Assert.That(menu.Choose(SessionCommands.QuitToMenuKey), Is.False, "this is the main menu");
            Assert.That(menu.Choose("ui.nothing.of.the.kind"), Is.False);
            Assert.That(anything, Is.Zero);
        }

        // ------------------------------------------------------------------ navigation

        [Test]
        public void LoadAsksForTheListingAndTheListingIsWhatMovesTheScreen()
        {
            MenuDirector menu = Showing();
            int asked = 0;
            var screens = new List<MenuScreen>();
            menu.SavesRequested += () => asked++;
            menu.ScreenChanged += screens.Add;

            Assert.That(menu.Choose(SessionCommands.LoadKey), Is.True);
            Assert.That(asked, Is.EqualTo(1));
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Root),
                "the rows and the screen arrive together, so the row alone does not navigate");
            Assert.That(screens, Is.Empty);

            menu.ShowSaves(TwoSaves());

            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Load));
            Assert.That(menu.Saves.Count, Is.EqualTo(2));
            Assert.That(screens, Is.EqualTo(new[] { MenuScreen.Load }));
        }

        [Test]
        public void BackingOutOfTheListingReturnsToTheRootAndStopsThere()
        {
            MenuDirector menu = Showing();
            menu.ShowSaves(TwoSaves());
            var screens = new List<MenuScreen>();
            menu.ScreenChanged += screens.Add;

            Assert.That(menu.Back(), Is.True);
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Root));
            Assert.That(screens, Is.EqualTo(new[] { MenuScreen.Root }));

            Assert.That(menu.Back(), Is.False, "the root screen is the bottom of this navigation");
            Assert.That(screens, Has.Count.EqualTo(1), "a refused back raises nothing");
        }

        [Test]
        public void NoRootRowCanBePressedFromTheLoadScreen()
        {
            MenuDirector menu = Showing();
            menu.ShowSaves(TwoSaves());
            int anything = 0;
            menu.NewGameRequested += () => anything++;
            menu.QuitRequested += () => anything++;

            Assert.That(menu.Choose(SessionCommands.NewGameKey), Is.False);
            Assert.That(menu.Choose(SessionCommands.QuitKey), Is.False);
            Assert.That(anything, Is.Zero, "a row that is not drawn cannot be pressed");
        }

        [Test]
        public void PuttingTheScreenAwayForgetsWhereItWas()
        {
            MenuDirector menu = Showing();
            menu.ShowSaves(TwoSaves());

            menu.Hide();
            menu.Show();

            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Root),
                "a screen that came back where it was left would show last session's saves");
        }

        [Test]
        public void ALateListingIsIgnoredRatherThanShownOverAWorld()
        {
            var menu = new MenuDirector();

            menu.ShowSaves(TwoSaves());

            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Root));
            Assert.That(menu.Saves, Is.Empty);
        }

        // ------------------------------------------------------------------ choosing a save

        [Test]
        public void ChoosingASaveHandsBackTheRowThatWasOnScreen()
        {
            MenuDirector menu = Showing();
            menu.ShowSaves(TwoSaves());
            var chosen = new List<SaveRow>();
            menu.LoadRequested += chosen.Add;

            Assert.That(menu.ChooseSave(1), Is.True);

            Assert.That(chosen, Has.Count.EqualTo(1), "exactly once");
            Assert.That(chosen[0].Id, Is.EqualTo("b"));
            Assert.That(chosen[0].Name, Is.EqualTo("Bellwether"));
            Assert.That(chosen[0].Day, Is.EqualTo(3));
        }

        [Test]
        public void AnUnreadableSaveIsListedAndCannotBeChosen()
        {
            MenuDirector menu = Showing();
            menu.ShowSaves(new[]
            {
                new SaveRow("a", "Ashfall", 12, "Meadow"),
                new SaveRow("c", "Cinderhold", 0, "?", "written by a newer build"),
            });
            int loads = 0;
            menu.LoadRequested += _ => loads++;

            Assert.That(menu.Saves.Count, Is.EqualTo(2),
                "hiding a save is how a player concludes their colony is gone");
            Assert.That(menu.Saves[1].Readable, Is.False);
            Assert.That(menu.ChooseSave(1), Is.False);
            Assert.That(loads, Is.Zero);
        }

        [Test]
        public void NoSaveCanBeChosenOffTheListOrOffTheRootScreen()
        {
            MenuDirector menu = Showing();
            int loads = 0;
            menu.LoadRequested += _ => loads++;

            Assert.That(menu.ChooseSave(0), Is.False, "the root screen has no save rows");

            menu.ShowSaves(TwoSaves());
            Assert.That(menu.ChooseSave(-1), Is.False);
            Assert.That(menu.ChooseSave(2), Is.False);
            Assert.That(loads, Is.Zero);
        }

        // ------------------------------------------------------------------ asking twice

        [Test]
        public void QuitArmsOnTheFirstPressAndLeavesOnTheSecond()
        {
            MenuDirector menu = Showing();
            int quits = 0, armings = 0;
            menu.QuitRequested += () => quits++;
            menu.ArmedChanged += () => armings++;

            Assert.That(menu.Choose(SessionCommands.QuitKey), Is.False, "asked, not done");
            Assert.That(menu.Armed, Is.EqualTo(SessionCommands.QuitKey));
            Assert.That(quits, Is.Zero);
            Assert.That(armings, Is.EqualTo(1));

            Assert.That(menu.Choose(SessionCommands.QuitKey), Is.True);
            Assert.That(quits, Is.EqualTo(1), "exactly once, on the second press");
            Assert.That(menu.Armed, Is.Null, "the question is answered and the row stands down");
            Assert.That(armings, Is.EqualTo(2));
        }

        [Test]
        public void ARowThatDoesNotAskTwiceGoesOnTheFirstPress()
        {
            MenuDirector menu = Showing();
            int newGame = 0;
            menu.NewGameRequested += () => newGame++;

            Assert.That(menu.Choose(SessionCommands.NewGameKey), Is.True);
            Assert.That(newGame, Is.EqualTo(1));
            Assert.That(menu.Armed, Is.Null, "nothing was ever armed");
        }

        /// <summary>
        /// <b>The negative control.</b> An armed "quit?" that survived its own screen would be a
        /// trap armed across the whole interface — press Quit, go and look at your saves, come back
        /// and press Quit meaning to ask the question, and the game closes. It is the exact rule
        /// <see cref="SettingsDirector.SetOpen"/> keeps for its exit row, and it is the one thing
        /// here that would still look completely correct with the behaviour removed.
        ///
        /// <para><b>Confirmed to fail without it.</b> Deleting the <c>Disarm()</c> call from
        /// <c>MenuDirector.GoTo</c> and running <c>scripts/test-fast.sh</c> failed this test on the
        /// <c>Is.Null</c> line — "Expected: null, But was: \"ui.settings.exit\"" — and failed
        /// nothing else, so the assertion is load-bearing on its own.</para>
        /// </summary>
        [Test]
        public void AnArmedRowStandsDownWhenTheScreenChangesUnderIt()
        {
            MenuDirector menu = Showing();
            int quits = 0;
            menu.QuitRequested += () => quits++;

            menu.Choose(SessionCommands.QuitKey);
            Assert.That(menu.Armed, Is.EqualTo(SessionCommands.QuitKey));

            menu.ShowSaves(TwoSaves());
            Assert.That(menu.Armed, Is.Null, "the question belonged to the screen it was asked on");

            menu.Back();
            Assert.That(menu.Choose(SessionCommands.QuitKey), Is.False,
                "coming back asks again rather than answering");
            Assert.That(quits, Is.Zero);
        }

        [Test]
        public void AnArmedRowStandsDownWhenTheScreenIsPutAway()
        {
            MenuDirector menu = Showing();
            int quits = 0, armings = 0;
            menu.QuitRequested += () => quits++;
            menu.ArmedChanged += () => armings++;

            menu.Choose(SessionCommands.QuitKey);
            menu.Hide();

            Assert.That(menu.Armed, Is.Null);
            Assert.That(armings, Is.EqualTo(2), "the stand-down is announced, not silent");

            menu.Show();
            Assert.That(menu.Choose(SessionCommands.QuitKey), Is.False);
            Assert.That(quits, Is.Zero);
        }

        [Test]
        public void PressingAnotherRowStandsTheArmedOneDownRatherThanAnsweringIt()
        {
            MenuDirector menu = Showing();
            int quits = 0, newGame = 0;
            menu.QuitRequested += () => quits++;
            menu.NewGameRequested += () => newGame++;

            menu.Choose(SessionCommands.QuitKey);
            Assert.That(menu.Choose(SessionCommands.NewGameKey), Is.True);

            Assert.That(newGame, Is.EqualTo(1));
            Assert.That(quits, Is.Zero, "only the row that is asking can answer");
            Assert.That(menu.Armed, Is.Null);
        }
    }
}
