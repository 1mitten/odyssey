#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The main screen with no screen: which of its screens is up, what each row asks for, the
    /// ask-twice arming, and the seed the New game screen commits (U39).
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

        /// <summary>
        /// A showing menu whose seed is dealt rather than drawn, for the New game screen (U39). The
        /// last number is repeated forever, so a test never runs the source dry.
        /// </summary>
        static MenuDirector ShowingWithSeeds(params uint[] numbers)
        {
            int next = 0;
            var menu = new MenuDirector(
                new SeedField(() => numbers[next < numbers.Length ? next++ : numbers.Length - 1]));
            menu.Show();
            return menu;
        }

        /// <summary>On the New game screen, on the first dealt seed.</summary>
        static MenuDirector OnNewGame(params uint[] numbers)
        {
            MenuDirector menu = ShowingWithSeeds(numbers);
            menu.Choose(SessionCommands.NewGameKey);
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
            int started = 0;
            menu.StartRequested += _ => started++;

            Assert.That(menu.Choose(SessionCommands.NewGameKey), Is.False);
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Root), "it navigated with no screen up");
            Assert.That(menu.Start(), Is.False);
            Assert.That(started, Is.Zero);
        }

        // ------------------------------------------------------------------ what a row asks for

        /// <summary>
        /// <b>The behaviour U39 changes.</b> U38's New game row built a world on a seed nobody ever
        /// saw; it opens the screen that shows the seed instead, and only the row on that screen
        /// commits.
        /// </summary>
        [Test]
        public void NewGameOpensItsOwnScreenRatherThanBuildingAWorld()
        {
            MenuDirector menu = ShowingWithSeeds(4242u);
            int started = 0, saves = 0, settings = 0, quit = 0;
            menu.StartRequested += _ => started++;
            menu.SavesRequested += () => saves++;
            menu.SettingsRequested += () => settings++;
            menu.QuitRequested += () => quit++;

            Assert.That(menu.Choose(SessionCommands.NewGameKey), Is.True);

            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.NewGame));
            Assert.That(started, Is.Zero, "pressing New game must not build anything by itself");
            Assert.That(saves + settings + quit, Is.Zero);
            Assert.That(menu.Seed.Usable, Is.True, "the screen opened with no seed in the box");
            Assert.That(menu.Seed.Seed, Is.EqualTo(4242u));
        }

        [Test]
        public void OptionsAsksForTheSettingsPanelAndNothingElse()
        {
            MenuDirector menu = Showing();
            int started = 0, saves = 0, settings = 0, quit = 0;
            menu.StartRequested += _ => started++;
            menu.SavesRequested += () => saves++;
            menu.SettingsRequested += () => settings++;
            menu.QuitRequested += () => quit++;

            Assert.That(menu.Choose(SessionCommands.OptionsKey), Is.True);

            Assert.That(settings, Is.EqualTo(1));
            Assert.That(started + saves + quit, Is.Zero);
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
            menu.StartRequested += _ => anything++;
            menu.SavesRequested += () => anything++;
            menu.QuitRequested += () => anything++;

            Assert.That(menu.Choose(SessionCommands.NewGameKey), Is.False);
            Assert.That(menu.Choose(SessionCommands.LoadKey), Is.False);
            Assert.That(menu.Choose(SessionCommands.QuitKey), Is.False);
            Assert.That(anything, Is.Zero);
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Settings), "a row that is not drawn navigated");
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
            menu.StartRequested += _ => anything++;
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
            menu.StartRequested += _ => anything++;
            menu.QuitRequested += () => anything++;

            Assert.That(menu.Choose(SessionCommands.NewGameKey), Is.False);
            Assert.That(menu.Choose(SessionCommands.QuitKey), Is.False);
            Assert.That(anything, Is.Zero, "a row that is not drawn cannot be pressed");
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Load), "and it cannot navigate either");
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

            Assert.That(menu.Choose(SessionCommands.NewGameKey), Is.True);
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.NewGame));
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
            int quits = 0;
            menu.QuitRequested += () => quits++;

            menu.Choose(SessionCommands.QuitKey);
            Assert.That(menu.Choose(SessionCommands.NewGameKey), Is.True);

            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.NewGame));
            Assert.That(quits, Is.Zero, "only the row that is asking can answer");
            Assert.That(menu.Armed, Is.Null);
        }

        // ------------------------------------------------------------- the New game screen (U39)

        [Test]
        public void StartBuildsTheWorldTheBoxIsShowing()
        {
            MenuDirector menu = OnNewGame(4242u);
            uint built = 0;
            int starts = 0;
            menu.StartRequested += choice => { built = choice.Seed; starts++; };

            Assert.That(menu.Start(), Is.True);

            Assert.That(starts, Is.EqualTo(1), "exactly once, on one press");
            Assert.That(built, Is.EqualTo(4242u));
        }

        [Test]
        public void StartBuildsATypedSeedRatherThanTheDrawnOne()
        {
            MenuDirector menu = OnNewGame(4242u);
            uint built = 0;
            menu.StartRequested += choice => built = choice.Seed;

            menu.Seed.Type("77");
            Assert.That(menu.Start(), Is.True);

            Assert.That(built, Is.EqualTo(77u), "the world came from a number the player never chose");
        }

        [Test]
        public void StartBuildsARerolledSeed()
        {
            MenuDirector menu = OnNewGame(1u, 2u);
            uint built = 0;
            menu.StartRequested += choice => built = choice.Seed;

            menu.Seed.Reroll();
            Assert.That(menu.Start(), Is.True);

            Assert.That(built, Is.EqualTo(2u));
        }

        /// <summary>
        /// <b>The rule the unit is built around</b> (§11.2 decision 2). A press that started the
        /// last good seed while the box read <c>twelve</c> would be this screen lying about the one
        /// number it exists to show — and a player who typed a seed deliberately would be given a
        /// different world with no way to tell.
        /// </summary>
        [Test]
        public void StartRefusesABoxThatNamesNoSeed()
        {
            MenuDirector menu = OnNewGame(4242u);
            int starts = 0;
            menu.StartRequested += _ => starts++;

            menu.Seed.Type("twelve");

            Assert.That(menu.Start(), Is.False);
            Assert.That(starts, Is.Zero, "it built 4242 while the box said 'twelve'");
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.NewGame), "and it left the screen too");
        }

        [Test]
        public void StartDoesNothingFromAnyOtherScreen()
        {
            MenuDirector menu = Showing();
            int starts = 0;
            menu.StartRequested += _ => starts++;

            Assert.That(menu.Start(), Is.False, "from the root");

            menu.ShowSaves(TwoSaves());
            Assert.That(menu.Start(), Is.False, "from the load list");

            menu.Back();
            menu.Choose(SessionCommands.OptionsKey);
            Assert.That(menu.Start(), Is.False, "from settings");

            Assert.That(starts, Is.Zero);
        }

        [Test]
        public void BackLeavesTheNewGameScreenForTheRoot()
        {
            MenuDirector menu = OnNewGame(4242u);
            int closed = 0;
            menu.SettingsClosed += () => closed++;

            Assert.That(menu.Back(), Is.True);

            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Root));
            Assert.That(closed, Is.Zero, "backing out of New game closed a settings panel");
        }

        /// <summary>
        /// §11.2 decision 3. Coming back to this screen and being dealt the world you have just
        /// walked away from reads as a reroll that does not work, and nothing on screen could tell
        /// the player otherwise.
        /// </summary>
        [Test]
        public void EnteringTheScreenDealsAFreshWorldEveryTime()
        {
            MenuDirector menu = ShowingWithSeeds(1u, 2u);

            menu.Choose(SessionCommands.NewGameKey);
            Assert.That(menu.Seed.Seed, Is.EqualTo(1u));

            menu.Back();
            menu.Choose(SessionCommands.NewGameKey);

            Assert.That(menu.Seed.Seed, Is.EqualTo(2u));
        }

        // ------------------------------------------------ the setup page (world setup)

        /// <summary>A menu with the whole New game flow: a seed, and three people to choose.</summary>
        static MenuDirector WithColonists(params uint[] seeds)
        {
            int next = 0;
            var select = new ColonistSelect(
                (seed, slot) => new Candidate(seed, "person-" + seed, 30 + slot, "Scrapper",
                    System.Array.Empty<SkillRow>()));
            var menu = new MenuDirector(
                new SeedField(() => seeds[next < seeds.Length ? next++ : seeds.Length - 1]), select);
            menu.Show();
            return menu;
        }

        /// <summary>
        /// <b>One page, not two.</b> U40 gave the colonists a screen of their own and the seed
        /// screen a Next row; the owner then asked for the whole skills grid on a candidate, which
        /// settled it the other way — the content is far past what a panel holds, so New game is a
        /// full page and everything is on it. Pressing New game therefore deals a seed *and* three
        /// people, and the next press is Start.
        /// </summary>
        [Test]
        public void NewGameDealsTheBoardAndThePeopleTogether()
        {
            MenuDirector menu = WithColonists(4242u);

            Assert.That(menu.Choose(SessionCommands.NewGameKey), Is.True);

            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.NewGame));
            Assert.That(menu.Seed.Seed, Is.EqualTo(4242u));
            Assert.That(menu.Colonists!.Cards.Count, Is.EqualTo(ColonistSelect.Slots));
            Assert.That(menu.Colonists.Selected, Is.Zero, "nobody's record is showing");
        }

        [Test]
        public void StartCarriesTheBoardThePeopleTheNameAndTheSize()
        {
            MenuDirector menu = WithColonists(4242u);
            menu.Choose(SessionCommands.NewGameKey);
            menu.TypeColonyName("Ashford");
            menu.NextSize();

            NewGameChoice chosen = default;
            int starts = 0;
            menu.StartRequested += choice => { chosen = choice; starts++; };

            Assert.That(menu.Start(), Is.True);

            Assert.That(starts, Is.EqualTo(1));
            Assert.That(chosen.Seed, Is.EqualTo(4242u));
            Assert.That(chosen.Colonists, Is.EqualTo(menu.Colonists!.ChosenSeeds()),
                "the colony would be built from people the player never saw");
            Assert.That(chosen.Name, Is.EqualTo("Ashford"));
            Assert.That(chosen.Size, Is.EqualTo(menu.Size));
        }

        [Test]
        public void StartStillRefusesABoxThatNamesNoSeed()
        {
            MenuDirector menu = WithColonists(4242u);
            menu.Choose(SessionCommands.NewGameKey);
            menu.Seed.Type("twelve");

            int starts = 0;
            menu.StartRequested += _ => starts++;

            Assert.That(menu.Start(), Is.False);
            Assert.That(starts, Is.Zero);
            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.NewGame), "and it left the page too");
        }

        [Test]
        public void EnteringThePageDealsFreshPeopleEveryTime()
        {
            MenuDirector menu = WithColonists(1u, 2u);

            menu.Choose(SessionCommands.NewGameKey);
            uint[] first = menu.Colonists!.ChosenSeeds();

            menu.Back();
            menu.Choose(SessionCommands.NewGameKey);

            Assert.That(menu.Colonists.ChosenSeeds(), Is.Not.EqualTo(first),
                "the page came back holding the people the player walked away from");
        }

        // ------------------------------------------------ the colony's name and the board size

        /// <summary>
        /// The field arrives already holding the default (owner, 2026-09-17), so a player who wants
        /// it types nothing — and the word comes from the naming CSV rather than from a literal
        /// here, which is what stops the page and the bootstrap disagreeing about what an unnamed
        /// colony is called.
        /// </summary>
        [Test]
        public void ItStartsOnTheDefaultNameAndTheStandardBoard()
        {
            MenuDirector menu = WithColonists(1u);

            Assert.That(menu.ColonyName, Is.EqualTo("The Lost Buckets"));
            Assert.That(menu.ColonyName,
                Is.EqualTo(Registry.Label(SeedField.DefaultColonyKey)),
                "the default name is written somewhere other than the naming CSV");

            Assert.That(menu.Size, Is.EqualTo(MapSizes.Default));
            Assert.That(MapSizes.At(menu.Size).Label, Is.EqualTo("Standard"));
        }

        [Test]
        public void TheNameIsWhatWasTypedAndTypingNothingNewChangesNothing()
        {
            MenuDirector menu = WithColonists(1u);
            int changes = 0;
            menu.Changed += () => changes++;

            menu.TypeColonyName("Ashford");
            Assert.That(menu.ColonyName, Is.EqualTo("Ashford"));
            Assert.That(changes, Is.EqualTo(1));

            menu.TypeColonyName("Ashford");
            Assert.That(changes, Is.EqualTo(1), "an echoed keystroke redrew the page");
        }

        /// <summary>
        /// The size control cycles rather than navigating: three sizes, one press each, and it
        /// comes back round rather than stopping at the end.
        /// </summary>
        [Test]
        public void TheSizeCyclesThroughAllThreeAndWraps()
        {
            MenuDirector menu = WithColonists(1u);
            var seen = new List<int>();

            for (int press = 0; press < MapSizes.All.Count; press++)
            {
                seen.Add(menu.Size);
                menu.NextSize();
            }

            Assert.That(seen, Has.Count.EqualTo(MapSizes.All.Count));
            Assert.That(menu.Size, Is.EqualTo(MapSizes.Default), "it did not come back round");
            for (int i = 1; i < seen.Count; i++)
                Assert.That(seen[i], Is.Not.EqualTo(seen[i - 1]));
        }

        [Test]
        public void EverySizeIsARegisteredNameAndADifferentBoard()
        {
            var boards = new HashSet<string>();
            foreach (MapSizes.Choice size in MapSizes.All)
            {
                Assert.That(Registry.Labels, Does.ContainKey(size.Key), size.Key + " is not named");
                Assert.That(boards.Add($"{size.X}x{size.Z}x{size.Y}"), Is.True,
                    "two sizes are the same board with different words");
                Assert.That(size.X, Is.GreaterThan(0));
                Assert.That(size.Y, Is.GreaterThan(0));
            }

            Assert.That(MapSizes.At(-1).Key, Is.EqualTo(MapSizes.All[MapSizes.Default].Key),
                "an index off the end does not fall back to the standard board");
            Assert.That(MapSizes.At(99).Key, Is.EqualTo(MapSizes.All[MapSizes.Default].Key));
        }

        // ------------------------------------------------ choosing which one to read

        [Test]
        public void SelectingACandidateShowsThatOne()
        {
            MenuDirector menu = WithColonists(1u);
            menu.Choose(SessionCommands.NewGameKey);

            Assert.That(menu.Colonists!.Select(2), Is.True);
            Assert.That(menu.Colonists.Selected, Is.EqualTo(2));
            Assert.That(menu.Colonists.Current.Seed, Is.EqualTo(menu.Colonists.Cards[2].Seed));
        }

        [Test]
        public void ASlotThatIsNotOnScreenCannotBeShown()
        {
            MenuDirector menu = WithColonists(1u);
            menu.Choose(SessionCommands.NewGameKey);

            Assert.That(menu.Colonists!.Select(-1), Is.False);
            Assert.That(menu.Colonists.Select(ColonistSelect.Slots), Is.False);
            Assert.That(menu.Colonists.Selected, Is.Zero);
        }

        [Test]
        public void PuttingTheScreenAwayLeavesTheSetupPageToo()
        {
            MenuDirector menu = WithColonists(4242u);
            menu.Choose(SessionCommands.NewGameKey);

            menu.Hide();

            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Root),
                "a menu that came back mid-navigation would show the last colony's half-made game");
        }
    }
}
