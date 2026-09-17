#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// Which of the main screen's two screens is showing.
    ///
    /// <para>Two, and that is the whole of the navigation (`17-start-flow.md` §4): the root column
    /// of rows, and the load screen listing what is in the saves folder. There is no third, because
    /// Options opens the settings panel that already exists rather than a screen of its own, and
    /// the New game screen with its seed and its candidate colonists is U39 to U41 and hangs off
    /// the root row, not off this enum.</para>
    /// </summary>
    public enum MenuScreen
    {
        Root,
        Load,
    }

    /// <summary>
    /// One line of the load screen, already prepared by whoever can see the disk.
    ///
    /// <para><b>This assembly does not know what a save file is</b> — no path, no
    /// <c>SaveHeader</c>, no <c>SaveRecipe</c>, and no reference to the catalogue in
    /// <c>Odyssey.Sim</c> that produces them. It knows a row has an identifier it can hand back, a
    /// few words to draw, and possibly a reason it cannot be opened. That is the whole of the
    /// contract, and it is why the navigation is a fast-tier test rather than something only a run
    /// with a filesystem could check.</para>
    ///
    /// <para><see cref="Problem"/> carries `17-start-flow.md` §5's rule: a file this build cannot
    /// open is <i>listed with its reason</i> rather than hidden, because silently omitting a save
    /// is how a player concludes their colony is gone. An unreadable row is drawn and cannot be
    /// chosen.</para>
    /// </summary>
    public readonly struct SaveRow
    {
        /// <summary>What the presenter calls this save. Opaque here — it is handed straight back
        /// in <see cref="MenuDirector.LoadRequested"/> and never parsed.</summary>
        public readonly string Id;

        /// <summary>The colony's name, from the save's own header.</summary>
        public readonly string Colony;

        /// <summary>Which day the colony had reached.</summary>
        public readonly int Day;

        /// <summary>What kind of board it is, in words the player reads.</summary>
        public readonly string Map;

        /// <summary>Why this file cannot be opened, or empty when it can.</summary>
        public readonly string Problem;

        public SaveRow(string id, string colony, int day, string map, string problem = "")
        {
            Id = id ?? string.Empty;
            Colony = colony ?? string.Empty;
            Day = day;
            Map = map ?? string.Empty;
            Problem = problem ?? string.Empty;
        }

        /// <summary>Whether this row can be chosen.</summary>
        public bool Readable => Problem.Length == 0;
    }

    /// <summary>
    /// The main screen: whether it is showing, which of its two screens is up, which destructive
    /// row is armed, and what the player has asked for.
    ///
    /// <para><b>It raises and never performs.</b> New game builds a world, Load reads a file, Quit
    /// closes the application and Options opens a panel — every one of those needs Unity, a
    /// filesystem or an engine, and none of them belongs in an assembly compiled without
    /// UnityEngine (ADR 0003). So this director decides <i>what was asked for</i> and says so
    /// through an event, exactly as <see cref="SettingsDirector"/> does with
    /// <see cref="SettingsDirector.ExitRequested"/>, and the presenter does the doing. That is what
    /// makes every rule below a fast-tier test rather than something judged once per milestone with
    /// a finger on the mouse.</para>
    ///
    /// <para><b>The load screen's rows are handed in, not fetched.</b> <see cref="ShowSaves"/>
    /// installs a listing and moves to the load screen in one call, and the root screen's Load row
    /// does not navigate by itself — it raises <see cref="SavesRequested"/> and the presenter
    /// answers with <see cref="ShowSaves"/>. Two reasons for that shape over a callback the
    /// director holds and invokes. First, the screen and the rows arrive together, so there is no
    /// frame in which the load screen is showing last time's listing while this time's is being
    /// read off a disk. Second, the director then holds the listing that was on screen when the
    /// player clicked, so a click cannot resolve against a folder that has changed underneath it.
    /// It also makes every root row uniform: all four raise and none performs, which is one rule
    /// rather than three and an exception.</para>
    ///
    /// <para><b>Showing is driven from outside.</b> The main screen exists exactly when no session
    /// is built (`17-start-flow.md` §2 decision 5), and whether a session is built is the
    /// bootstrap's fact, not this director's. The presenter sets it; this type only guarantees that
    /// putting the screen away resets it, so it never comes back mid-navigation or mid-question.
    /// </para>
    ///
    /// <para>Unity-free by construction (ADR 0003): everything here runs in the fast tier.</para>
    /// </summary>
    public sealed class MenuDirector
    {
        static readonly SaveRow[] NoSaves = Array.Empty<SaveRow>();

        SaveRow[] _saves = NoSaves;

        /// <summary>Whether the main screen is on screen at all.</summary>
        public bool Showing { get; private set; }

        /// <summary>Which screen is up. Always <see cref="MenuScreen.Root"/> when the main screen
        /// has just been shown.</summary>
        public MenuScreen Screen { get; private set; } = MenuScreen.Root;

        /// <summary>
        /// The key of the destructive row that has been pressed once and is asking to be sure, or
        /// null when nothing is armed.
        ///
        /// <para>A key rather than a bool, because this screen has a table of rows behind it rather
        /// than one hard-coded exit row: <see cref="SessionCommands.AsksTwice"/> decides which of
        /// them arm, so the director must be able to say <i>which one</i> is currently asking.
        /// </para>
        /// </summary>
        public string? Armed { get; private set; }

        /// <summary>The listing the load screen is drawing, as it was handed in.</summary>
        public IReadOnlyList<SaveRow> Saves => _saves;

        /// <summary>The root screen's rows, top to bottom. One place, shared with the settings
        /// panel, so the two surfaces cannot drift (<see cref="SessionCommands"/>).</summary>
        public static IReadOnlyList<SessionCommand> Rows =>
            SessionCommands.For(SessionContext.MainScreen);

        /// <summary>Raised when the main screen appears or goes away.</summary>
        public event Action? ShowingChanged;

        /// <summary>Raised when the showing screen changes, with the screen now up.</summary>
        public event Action<MenuScreen>? ScreenChanged;

        /// <summary>Raised when a destructive row arms or stands down.</summary>
        public event Action? ArmedChanged;

        /// <summary>Raised when the player has asked for a new colony. Building one is the
        /// bootstrap's job (U35's <c>BuildSession</c>), not this type's.</summary>
        public event Action? NewGameRequested;

        /// <summary>Raised when the player has asked to see their saves. The presenter answers by
        /// listing the folder and calling <see cref="ShowSaves"/>.</summary>
        public event Action? SavesRequested;

        /// <summary>Raised when one save has been chosen, with the row as it was handed in. Only
        /// ever a readable row.</summary>
        public event Action<SaveRow>? LoadRequested;

        /// <summary>Raised when the settings panel should be opened over the main screen. The same
        /// panel as in game, unchanged, which is the point of reusing it.</summary>
        public event Action? SettingsRequested;

        /// <summary>Raised when the player has asked twice to leave. Not performed here, for the
        /// reason <see cref="SettingsDirector.ExitRequested"/> is not: quitting is the engine's to
        /// do, and "the screen asked the game to leave" is a sentence the fast tier can assert
        /// without one.</summary>
        public event Action? QuitRequested;

        /// <summary>
        /// Show or put away the main screen.
        ///
        /// <para>Either direction returns it to <see cref="MenuScreen.Root"/> and stands any armed
        /// row down. A screen that came back where it was left would show a stale save listing, and
        /// an armed "quit?" that survived its own screen would be a trap armed across the whole
        /// interface — the same rule <see cref="SettingsDirector.SetOpen"/> keeps for its exit
        /// row.</para>
        /// </summary>
        public void SetShowing(bool showing)
        {
            if (Showing == showing) return;
            Showing = showing;
            Disarm();
            GoTo(MenuScreen.Root);
            ShowingChanged?.Invoke();
        }

        public void Show() => SetShowing(true);

        public void Hide() => SetShowing(false);

        /// <summary>
        /// Install a save listing and show it. One call rather than two because the rows and the
        /// screen belong together; see the type's note on why they are handed in rather than
        /// fetched.
        ///
        /// <para>Ignored when the main screen is not showing. <see cref="SavesRequested"/> is the
        /// only thing that asks for a listing and it only fires while the screen is up, so a
        /// listing arriving at any other time is a late answer to a question the player has already
        /// walked away from — and obeying it would put the load screen behind a built world.</para>
        /// </summary>
        public void ShowSaves(IReadOnlyList<SaveRow> saves)
        {
            if (!Showing) return;

            var rows = new SaveRow[saves?.Count ?? 0];
            for (int i = 0; i < rows.Length; i++) rows[i] = saves![i];
            _saves = rows;
            GoTo(MenuScreen.Load);
        }

        /// <summary>
        /// Back out of the load screen to the root. The one navigation this director performs by
        /// itself, because it is the one that needs nothing from outside. False on the root screen,
        /// which is where a presenter learns that Escape has nothing left to unwind here.
        /// </summary>
        public bool Back()
        {
            if (Screen == MenuScreen.Root) return false;
            GoTo(MenuScreen.Root);
            return true;
        }

        /// <summary>
        /// Press one of the root screen's rows.
        ///
        /// <para>Returns true when the press did the thing, false when it did not — because the
        /// screen is not showing, because this row is not drawn here, or because the row asks twice
        /// and this was the first press. A presenter that redraws on
        /// <see cref="ArmedChanged"/> needs no other answer.</para>
        ///
        /// <para>Pressing a different row while one is armed stands the first one down rather than
        /// acting on it. Only the row that is asking can answer.</para>
        /// </summary>
        public bool Choose(string key)
        {
            if (!Showing) return false;
            if (Screen != MenuScreen.Root) return false;
            if (!SessionCommands.IsDrawn(key, SessionContext.MainScreen)) return false;

            if (Armed != key && SessionCommands.AsksTwice(key, SessionContext.MainScreen))
            {
                Armed = key;
                ArmedChanged?.Invoke();
                return false;
            }

            Disarm();
            Perform(key);
            return true;
        }

        /// <summary>
        /// Choose one of the listed saves, by its place in the listing that is on screen.
        ///
        /// <para>False for anything out of range and for an unreadable row: a save the build cannot
        /// open is drawn so the player knows it is there (§5), and choosing it must do nothing
        /// rather than start a load that fails halfway into a torn-down world.</para>
        /// </summary>
        public bool ChooseSave(int index)
        {
            if (!Showing || Screen != MenuScreen.Load) return false;
            if (index < 0 || index >= _saves.Length) return false;

            SaveRow row = _saves[index];
            if (!row.Readable) return false;

            LoadRequested?.Invoke(row);
            return true;
        }

        void Perform(string key)
        {
            if (key == SessionCommands.NewGameKey) NewGameRequested?.Invoke();
            else if (key == SessionCommands.LoadKey) SavesRequested?.Invoke();
            else if (key == SessionCommands.OptionsKey) SettingsRequested?.Invoke();
            else if (key == SessionCommands.QuitKey) QuitRequested?.Invoke();
        }

        void GoTo(MenuScreen screen)
        {
            if (Screen == screen) return;
            Screen = screen;
            // An armed row belongs to the screen it was pressed on. Carrying it across would leave
            // a "quit?" waiting behind a screen the player cannot see it on.
            Disarm();
            ScreenChanged?.Invoke(Screen);
        }

        void Disarm()
        {
            if (Armed is null) return;
            Armed = null;
            ArmedChanged?.Invoke();
        }
    }
}
