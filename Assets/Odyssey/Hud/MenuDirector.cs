#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// Which of the main screen's screens is showing.
    ///
    /// <para>The whole of the navigation (`17-start-flow.md` §4 and §11): the root column of rows,
    /// the load screen listing what is in the saves folder, the settings panel standing in this
    /// screen's place, and the New game screen where the seed is read and rerolled. Every one of
    /// them is the same box — <c>HudLayout.StartBody</c> — so moving between them moves no row
    /// under the pointer.</para>
    /// </summary>
    public enum MenuScreen
    {
        Root,
        Load,

        /// <summary>
        /// New game: the whole setup page — the board's seed, name and size, and the three people
        /// who will live on it.
        ///
        /// <para>The root's New game row lands here rather than building a world, which is the one
        /// behaviour U38 shipped that U39 changed. A seed that is drawn, used and never shown gives
        /// a world nobody can ask for twice.</para>
        ///
        /// <para><b>One screen, not two</b> (owner, 2026-09-17). U40 gave the colonists a screen of
        /// their own because three cards would not fit the menu's fixed box beside the seed; the
        /// owner then asked for the whole skills grid on a candidate, which settles it the other
        /// way — the content is far past what a 420 × 384 panel can hold, so this is not a panel at
        /// all. It is drawn full-viewport and the menu box is left exactly as it is for Load and
        /// Settings. <c>docs/design/19-world-setup.md</c>.</para>
        /// </summary>
        NewGame,


        /// <summary>
        /// The settings panel, standing in the start screen's place rather than on top of it.
        ///
        /// <para><b>Added after the owner played it</b> (2026-09-17): *"when I clicked on settings
        /// it appeared below the menu — it would [be] cleaner if main menu disappeared and settings
        /// appeared but could navigate back to main menu"*. Both panels are centred, so one landed
        /// over the other and the pair read as a stack of two things rather than as one screen
        /// showing what you asked for. It is a screen of this menu now, and
        /// <see cref="MenuDirector.Back"/> is the way out of it, the same way out the load screen
        /// already had.</para>
        /// </summary>
        Settings,

        /// <summary>
        /// The planet (design 59 §9): the seed, the map and the site the colony lands on, between
        /// the root's New game and the setup page. Present only when a <see cref="WorldChoice"/> was
        /// handed in; without one, New game goes straight to the setup page as it always did.
        ///
        /// <para>Last in the list rather than beside <see cref="NewGame"/>, so no screen already
        /// named by number moves.</para>
        /// </summary>
        World,
    }

    /// <summary>
    /// Everything the player settled on before pressing Start: the board, and the people (U40).
    ///
    /// <para><b>One value rather than two arguments</b>, for the reason
    /// <see cref="MenuDirector.StartRequested"/> carries the seed at all: a presenter that fetched
    /// either of these separately could fetch one that had moved since the press, or one that never
    /// passed the guard saying it was usable. They were chosen together and they travel together.
    /// </para>
    /// </summary>
    public readonly struct NewGameChoice
    {
        /// <summary>The world's seed, as the box showed it.</summary>
        public readonly uint Seed;

        /// <summary>
        /// One roll seed per chosen colonist, in the order they were shown — or null when no
        /// colonist select was in the flow, which is a colony the world populates for itself.
        /// </summary>
        public readonly uint[]? Colonists;

        /// <summary>
        /// What the player called each chosen colonist, in the same order as
        /// <see cref="Colonists"/> — null where they kept the name they were dealt, and null
        /// altogether when no colonist select was in the flow.
        ///
        /// <para>Beside the seeds rather than derived from them, because a typed name is the one
        /// thing on that screen a seed cannot produce.</para>
        /// </summary>
        public readonly string?[]? Names;

        /// <summary>What the player called the colony, or empty to take the request's default.</summary>
        public readonly string Name;

        /// <summary>Which board size, as an index into <see cref="MapSizes.All"/>.</summary>
        public readonly int Size;

        /// <summary>
        /// Who tells the colony's story and how hard (design 68). Handed to the session's
        /// <see cref="StoryDirector"/>, which submits it as the colony's first two intents.
        /// </summary>
        public readonly StoryChoice Story;

        /// <summary>
        /// The planet tile the colony lands on (design 59), or null when the flow had no World
        /// screen. With a site, <see cref="Seed"/> is the <b>world's</b> seed and the board's own is
        /// derived from it and the tile (<c>SiteRules.BoardSeed</c>).
        /// </summary>
        public readonly SiteTile? Site;

        public NewGameChoice(uint seed, uint[]? colonists, string? name, int size,
            string?[]? names = null, SiteTile? site = null, StoryChoice? story = null)
        {
            Seed = seed;
            Colonists = colonists;
            Names = names;
            Name = name ?? string.Empty;
            Size = size;
            Story = story ?? StoryChoice.Default;
            Site = site;
        }
    }

    /// <summary>
    /// The board sizes the setup page offers (world setup, 2026-09-17).
    ///
    /// <para><b>Named rather than numeric.</b> "120 × 120 × 16" is a fact about an array;
    /// "Standard" is a choice about a game. The numbers are here because the request needs them,
    /// and the word is what the player picks.</para>
    ///
    /// <para>Unity-free, and deliberately not in <c>Odyssey.Sim</c>: a size is three integers the
    /// simulation will take from anybody, and which three a *menu* offers is an interface
    /// question.</para>
    /// </summary>
    public static class MapSizes
    {
        public readonly struct Choice
        {
            public readonly string Key;
            public readonly int X;
            public readonly int Z;
            public readonly int Y;

            public Choice(string key, int x, int z, int y)
            {
                Key = key;
                X = x;
                Z = z;
                Y = y;
            }

            /// <summary>The word the player reads, from the naming registry.</summary>
            public string Label => Registry.Label(Key);
        }

        public static readonly IReadOnlyList<Choice> All = new[]
        {
            new Choice("ui.newgame.size.small", 80, 80, 16),
            new Choice("ui.newgame.size.standard", 120, 120, 16),
            new Choice("ui.newgame.size.large", 180, 180, 24),
            new Choice("ui.newgame.size.huge", 240, 240, 16),
        };

        /// <summary>Standard: the board every measurement on record was taken on, and the one the
        /// scene has always loaded.</summary>
        public const int Default = 1;

        public static readonly string[] IconKeys = BuildKeys();

        static string[] BuildKeys()
        {
            var keys = new string[All.Count];
            for (int i = 0; i < All.Count; i++) keys[i] = All[i].Key;
            return keys;
        }

        public static Choice At(int index) =>
            All[index < 0 || index >= All.Count ? Default : index];
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

        /// <summary>
        /// What the player called this save — its title in the list.
        ///
        /// <para><b>Separate from <see cref="Colony"/> since saves could be named</b> (owner,
        /// 2026-09-17). A folder is mostly repeated attempts at one colony, so a list titled by the
        /// colony said the same word on every row and hid the one thing that told them apart.</para>
        /// </summary>
        public readonly string Name;

        /// <summary>The colony's name, from the save's own header. The line under the title, beside
        /// the day and the hour — still worth saying, because a name the player typed need not
        /// mention the colony at all.</summary>
        public readonly string Colony;

        /// <summary>Which day the colony had reached.</summary>
        public readonly int Day;

        /// <summary>What kind of board it is, in words the player reads.</summary>
        public readonly string Map;

        /// <summary>Why this file cannot be opened, or empty when it can.</summary>
        public readonly string Problem;

        /// <summary>
        /// When the file was written, already formatted by whoever could see the disk.
        ///
        /// <para><b>A string rather than a <c>DateTime</c>, deliberately.</b> Formatting a date is a
        /// question about the player's machine — their locale, their clock, their idea of what
        /// "yesterday" means — and this assembly is compiled without any of that in mind. The
        /// presenter that read the file's timestamp is the one that knows; here it is a few words
        /// to draw, like the colony's name beside it.</para>
        ///
        /// <para>Asked for by the owner after playing it (2026-09-17): a list of colonies all
        /// called the same thing, on days that read alike, is a list you cannot choose from. The
        /// day says how far the colony got; this says which attempt it was.</para>
        /// </summary>
        public readonly string When;

        public SaveRow(string id, string name, int day, string map, string problem = "",
            string when = "", string colony = "")
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Colony = colony ?? string.Empty;
            Day = day;
            Map = map ?? string.Empty;
            Problem = problem ?? string.Empty;
            When = when ?? string.Empty;
        }

        /// <summary>Whether this row can be chosen.</summary>
        public bool Readable => Problem.Length == 0;
    }

    /// <summary>
    /// The main screen: whether it is showing, which of its screens is up, which destructive row is
    /// armed, and what the player has asked for.
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

        /// <summary>
        /// The seed the New game screen is holding (U39).
        ///
        /// <para><b>Exposed rather than wrapped</b>, because typing and rerolling change a box and
        /// nothing else: there is no world to protect from them, and three forwarding methods here
        /// would only be three more places for the two to disagree. The act that has a consequence
        /// — <see cref="Start"/> — is this director's, and it is the one that is guarded.</para>
        /// </summary>
        public SeedField Seed { get; }

        /// <summary>
        /// The three colonists the New game flow ends on (U40), or null when nobody has supplied a
        /// way to roll one.
        ///
        /// <para><b>Optional because rolling a candidate needs <c>Odyssey.Sim</c></b>, which this
        /// assembly cannot see: the presenter hands one in. A director without it still navigates —
        /// <see cref="Next"/> simply has nowhere to go — so every existing test and every rig that
        /// only cares about the seed builds one line at a time, as before.</para>
        /// </summary>
        public ColonistSelect? Colonists { get; }

        public MenuDirector() : this(new SeedField(), null) { }

        /// <summary>The seam a test drives: the seed's randomness handed in.</summary>
        public MenuDirector(SeedField seed) : this(seed, null) { }

        public MenuDirector(SeedField seed, ColonistSelect? colonists) : this(seed, colonists, null) { }

        public MenuDirector(SeedField seed, ColonistSelect? colonists, WorldChoice? world)
        {
            Seed = seed ?? throw new ArgumentNullException(nameof(seed));
            Colonists = colonists;
            World = world;
        }

        /// <summary>
        /// The World screen's model (design 59 §9), or null for a flow with no planet — every rig and
        /// test written before it, which keep going straight from New game to the setup page.
        /// Optional for the reason <see cref="Colonists"/> is: it needs the simulation's generator,
        /// which the presenter hands in.
        /// </summary>
        public WorldChoice? World { get; }

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

        /// <summary>
        /// Raised when the player has committed to a new colony, with the seed to build it from
        /// (U39). Building one is the bootstrap's job (U35's <c>BuildSession</c>), not this type's.
        ///
        /// <para><b>It carries the seed rather than leaving the presenter to read it back</b>, and
        /// that is the whole difference between this and the <c>NewGameRequested</c> it replaces.
        /// A presenter that fetched the number separately could fetch a different one — the field
        /// having moved between the press and the read, or having been read before the guard that
        /// says it is readable at all. Here the number that passed <see cref="SeedField.Usable"/>
        /// is the number handed over, in one act.</para>
        /// </summary>
        public event Action<NewGameChoice>? StartRequested;

        /// <summary>Raised when the player has asked to see their saves. The presenter answers by
        /// listing the folder and calling <see cref="ShowSaves"/>.</summary>
        public event Action? SavesRequested;

        /// <summary>Raised when one save has been chosen, with the row as it was handed in. Only
        /// ever a readable row.</summary>
        public event Action<SaveRow>? LoadRequested;

        /// <summary>Raised when the settings panel should be opened over the main screen. The same
        /// panel as in game, unchanged, which is the point of reusing it.</summary>
        public event Action? SettingsRequested;

        /// <summary>
        /// The settings screen was left for the root. The presenter closes the panel; this director
        /// only ever says which screen is showing.
        /// </summary>
        public event Action? SettingsClosed;

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
        /// Back out of the load, settings or New game screen to the root. The one navigation this
        /// director performs by itself, because it is the one that needs nothing from outside.
        /// False on the root screen, which is where a presenter learns that Escape has nothing left
        /// to unwind here.
        ///
        /// <para>Leaving the settings screen also raises <see cref="SettingsClosed"/>, because the
        /// panel it stands in for is somebody else's to close: this director owns which screen is
        /// showing and knows nothing about the panel itself.</para>
        /// </summary>
        public bool Back()
        {
            if (Screen == MenuScreen.Root) return false;

            // One level at a time (design 59 §9): the setup page backs out to the planet it was
            // reached from, with the seed, the site and the three people all kept.
            if (Screen == MenuScreen.NewGame && World != null)
            {
                GoTo(MenuScreen.World);
                return true;
            }

            bool leavingSettings = Screen == MenuScreen.Settings;

            GoTo(MenuScreen.Root);

            if (leavingSettings) SettingsClosed?.Invoke();
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

        /// <summary>
        /// Press Start on the New game screen: build a world from the seed in the box (U39).
        ///
        /// <para>Returns true when it asked for a world, false when it did not — because the screen
        /// is away, because this is not the New game screen, or because the box does not name a
        /// seed. <b>The last of those is the rule this unit is built around</b>: a press that
        /// started seed 4242 while the field read <c>twelve</c> would be the screen lying about the
        /// one number it exists to show. The presenter draws the row disabled from
        /// <see cref="SeedField.Usable"/>, and this refuses as well, because a rule kept only by
        /// whoever draws it is a rule the next caller does not have.</para>
        /// </summary>
        public bool Start()
        {
            if (!Showing || Screen != MenuScreen.NewGame) return false;
            if (!Seed.Usable) return false;
            // With a planet, only a site a colony can land on — the rule the World screen's Next
            // keeps, kept again here because a rule kept only by whoever draws it is not kept.
            if (World != null && !World.CanGoNext) return false;

            StartRequested?.Invoke(
                new NewGameChoice(Seed.Seed, Colonists?.ChosenSeeds(), ColonyName, Size,
                    Colonists?.ChosenNames(), World?.Site, Story));
            return true;
        }

        /// <summary>
        /// Press Next on the World screen: on to the setup page with the site picked (design 59 §9).
        /// False anywhere else, and on a site a colony cannot land on — which the page draws disabled
        /// and this refuses as well.
        /// </summary>
        public bool NextFromWorld()
        {
            if (!Showing || Screen != MenuScreen.World || World == null) return false;
            if (!World.CanGoNext) return false;
            GoTo(MenuScreen.NewGame);
            return true;
        }

        /// <summary>
        /// What the colony will be called.
        ///
        /// <para><b>Starts at the default rather than empty</b> (owner, 2026-09-17), so the field
        /// arrives with "The Lost Buckets" in it and a player who wants that types nothing. Clearing
        /// it is allowed and is not a failure — the bootstrap falls back to the same name, so an
        /// empty box and the default box give the same colony.</para>
        /// </summary>
        public string ColonyName { get; private set; } = Registry.Label(SeedField.DefaultColonyKey);

        public void TypeColonyName(string? name)
        {
            string typed = name ?? string.Empty;
            if (typed == ColonyName) return;
            ColonyName = typed;
            Changed?.Invoke();
        }

        /// <summary>Which board size the page is offering. Index into <see cref="MapSizes.All"/>.</summary>
        public int Size { get; private set; } = MapSizes.Default;

        /// <summary>
        /// Step to the next size, wrapping. One control rather than a row each: there are
        /// few sizes and a player picking one is cycling, not navigating.
        /// </summary>
        public void NextSize()
        {
            Size = (Size + 1) % MapSizes.All.Count;
            Changed?.Invoke();
        }

        /// <summary>
        /// The storyteller and difficulty the page is offering (design 68 §12): Jacob at Normal
        /// until the player picks. Kept across visits to the page, unlike the seed and the people,
        /// because it is a preference about how to play rather than a draw.
        /// </summary>
        public StoryChoice Story { get; private set; } = StoryChoice.Default;

        public void ChooseStoryteller(int teller) => SetStory(Story.WithTeller(teller));
        public void ChooseRung(int rung) => SetStory(Story.WithRung(rung));
        public void SetThreat(int percent) => SetStory(Story.WithThreat(percent));
        public void SetBigThreats(bool on) => SetStory(Story.WithBigThreats(on));
        public void SetAdaptation(int percent) => SetStory(Story.WithAdaptation(percent));
        public void SetGrace(int hundredths) => SetStory(Story.WithGrace(hundredths));

        void SetStory(StoryChoice next)
        {
            if (next.Equals(Story)) return;
            Story = next;
            Changed?.Invoke();
        }

        /// <summary>Raised when something on the setup page that is not the seed or the people has
        /// changed — the colony's name, the board size, or the story.</summary>
        public event Action? Changed;

        void Perform(string key)
        {
            if (key == SessionCommands.NewGameKey)
            {
                // A fresh world and fresh people every time the page is entered (17 §11.2 decision
                // 3). Both are drawn before the navigation, so the page is never shown for a frame
                // holding the game somebody decided against.
                Seed.Draw();
                Colonists?.Deal(SeedEntry.Draw);
                // With a planet, the seed makes it (the World model hears the draw) and the player
                // picks a site first; without one, straight to the setup page as before.
                GoTo(World != null ? MenuScreen.World : MenuScreen.NewGame);
            }
            else if (key == SessionCommands.LoadKey) SavesRequested?.Invoke();
            else if (key == SessionCommands.QuitKey) QuitRequested?.Invoke();
            else if (key == SessionCommands.OptionsKey)
            {
                // The screen moves first, then the panel is asked for. The other order would show
                // the settings panel for one frame with the menu still under it, which is the
                // stacked look this screen exists to avoid.
                GoTo(MenuScreen.Settings);
                SettingsRequested?.Invoke();
            }
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
