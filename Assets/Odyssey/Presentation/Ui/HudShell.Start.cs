#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// <see cref="HudShell"/>: B18, the start screen (U38, and its New game screen U39).
    ///
    /// <para>The screen before the game — the only one that exists when no world is built. It is
    /// the project's first true modal, and it is deliberately small: four rows drawn from
    /// <see cref="SessionCommands"/>, a list of saves read from their headers, a seed you can read
    /// and reroll, and nothing that the rest of the interface does not already have. Design and the
    /// owner's decisions are <c>docs/design/17-start-flow.md</c>; §11 is the seed.</para>
    ///
    /// <para><b>Everything here goes through a seam that already existed.</b> The panel is
    /// <c>Modal()</c>, which is <c>Window()</c> which is <c>Panel()</c>; the rows are
    /// <c>.settings__row</c>, the same row the stores panel and the Menu popover are built from;
    /// the words come from <c>Registry.Label</c> and the CSV behind it; the type comes from
    /// <c>HudText</c>; the geometry is <c>HudLayout</c>, where the fast tier can measure it. The
    /// screen introduces exactly one thing the interface did not have: the scrim, which is one
    /// token in <c>HudTheme</c>.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        /// <summary>
        /// The main screen's director, with the colonist select the presenter has to supply
        /// (U40) — rolling a candidate needs <c>Odyssey.Sim</c>, which <c>Odyssey.Hud</c> cannot
        /// reference.
        /// </summary>
        readonly MenuDirector _menu = new MenuDirector(new SeedField(), new ColonistSelect(RollCandidate));

        /// <summary>
        /// The start screen's own director, which the shell owns rather than the session does —
        /// this screen outlives every colony, and exists exactly when none is running.
        /// </summary>
        public MenuDirector Menu => _menu;

        HudModal _startScreen = null!;
        VisualElement _startRows = null!;
        ScrollView _startList = null!;
        VisualElement _startBack = null!;

        // The New game screen (U39): the seed, the reroll and the row that goes on.
        VisualElement _startNewGame = null!;
        TextField _seedBox = null!;
        VisualElement _startCommit = null!;

        // The colonist screen (U40): three cards, and the row that commits.
        VisualElement _startColonists = null!;
        VisualElement _colonistCards = null!;
        VisualElement _colonistReroll = null!;

        /// <summary>
        /// Roll one candidate for the select screen — the presenter's half of U40's seam.
        ///
        /// <para>It calls <c>ColonistDraw</c>, which is the same roll the colony runs, so the card
        /// and the colonist cannot come to disagree. What it adds is the interface's own two
        /// questions: what this person is called, and which of their skills are worth the space.
        /// </para>
        ///
        /// <para><b>Best first and cut short.</b> Thirteen skills times three cards is a screen of
        /// numbers nobody reads, and the question a player is answering is "what are these three
        /// good at" — so the card carries the top few. The count comes from what fits, in
        /// <c>HudLayout</c>, rather than from a literal here.</para>
        /// </summary>
        static Candidate RollCandidate(uint seed, int slot)
        {
            Pawn rolled = ColonistDraw.Roll(seed, slot);

            var best = new List<CandidateSkill>();
            foreach (SkillCatalogue.Entry entry in SkillCatalogue.All)
            {
                // Only the skills the simulation actually trains. The catalogue carries thirteen
                // against four that are live, and a candidate whose card promised Cooking would be
                // promising something no colonist can do yet.
                if (entry.Skill == SkillCatalogue.NotSimulated) continue;

                int index = System.Array.IndexOf(SkillIndex.Names, entry.Skill);
                if (index < 0) continue;

                best.Add(new CandidateSkill(entry.Key, Registry.Label(entry.Key), rolled.SkillLevel(index)));
            }

            best.Sort((a, b) => b.Level.CompareTo(a.Level));
            if (best.Count > HudLayout.ColonistCardSkills)
                best.RemoveRange(HudLayout.ColonistCardSkills, best.Count - HudLayout.ColonistCardSkills);

            return new Candidate(seed, ColonistNames.Of(seed, ColonistDraw.IdForSlot(slot)), best);
        }
        readonly Dictionary<string, VisualElement> _startRowByKey = new Dictionary<string, VisualElement>();

        /// <summary>
        /// Which file each row of the load list stands for.
        ///
        /// <para><c>MenuDirector</c> hands a <c>SaveRow</c> back when one is chosen and that row
        /// carries no path, on purpose: <c>Odyssey.Hud</c> is compiled without UnityEngine and
        /// knows nothing about folders. So the presenter keeps the mapping, and the row's id is
        /// the path it was built from.</para>
        /// </summary>
        readonly Dictionary<string, string> _savePathById = new Dictionary<string, string>();

        /// <summary>
        /// The directors the HUD runs on while no session exists.
        ///
        /// <para><b>Why a whole set rather than a special case.</b> The settings panel, the key
        /// bindings and every control in this shell reach their state through
        /// <c>_directors</c>, in forty-odd places. With no session there is no
        /// <c>HudDirectors</c>, and the alternative to this was forty-odd null checks and two
        /// different ways for the panel to work depending on whether a colony happened to be
        /// running. So the shell holds a set of its own, built over the <i>same</i>
        /// <c>SettingsDirector</c> and <c>HotkeyDirector</c> the composition root hands to every
        /// session — so the preferences are one object throughout, and only the world-shaped
        /// directors beside them are a stand-in. Nothing reads those while the start screen is
        /// up: <c>Update</c> returns as soon as it finds no world.</para>
        /// </summary>
        HudDirectors? _screenDirectors;

        void BuildStartScreen()
        {
            _screenDirectors = new HudDirectors(1, 0, _boot!.Preferences, _boot.Keys);

            // No X: this window has nothing behind it to close to, and an X that does nothing is
            // worse than no X at all. It is the one window in the game that is allowed to have
            // none, and Window() takes a null for exactly this.
            _startScreen = Modal("start", Registry.Label("ui.start.screen"), null, "startscreen");

            // One body of a fixed height, whatever screen is inside it (owner, 2026-09-17). Every
            // screen's content goes in here rather than straight into the panel, so the panel's
            // height is a constant rather than a consequence of what is showing.
            var body = new VisualElement();
            body.AddToClassList("startscreen__body");
            _startScreen.Panel.Add(body);

            _startRows = new VisualElement();
            _startRows.AddToClassList("startscreen__rows");
            body.Add(_startRows);

            foreach (SessionCommand command in SessionCommands.For(SessionContext.MainScreen))
                _startRows.Add(StartRow(command));

            _startNewGame = BuildNewGameScreen();
            body.Add(_startNewGame);

            _startColonists = BuildColonistScreen();
            body.Add(_startColonists);

            // The saves, when the load screen is showing. A scroll view because a folder can hold
            // any number of colonies, wearing the Build palette's scroller class so there is one
            // scrollbar in the project rather than two.
            _startList = new ScrollView(ScrollViewMode.Vertical);
            _startList.AddToClassList("startscreen__list");
            _startList.AddToClassList("build__scroll");
            _startList.style.display = DisplayStyle.None;
            body.Add(_startList);

            _startBack = new VisualElement();
            _startBack.AddToClassList("settings__row");
            _startBack.AddToClassList("startscreen__back");
            var backIcon = new IconBadge("ui.start.back", IconBadge.RowSize);
            backIcon.Inherit(HudTokens.TextMeta);
            _startBack.Add(backIcon);
            _startBack.Add(HudText.Make(Registry.Label("ui.start.back"), HudTextRole.Row,
                ussClass: "settings__label"));
            _startBack.RegisterCallback<ClickEvent>(_ => _menu.Back());
            _startBack.style.display = DisplayStyle.None;
            body.Add(_startBack);

            _menu.ShowingChanged += RefreshStartScreen;
            _menu.ScreenChanged += _ => RefreshStartScreen();
            _menu.ArmedChanged += RefreshStartArming;
            _menu.Seed.Changed += RefreshSeed;
            _menu.Colonists!.Changed += RefreshColonists;
            _menu.StartRequested += OnStartNewGame;
            _menu.SavesRequested += OnListSaves;
            _menu.SettingsRequested += () => _boot!.Preferences.SetOpen(true);
            _menu.SettingsClosed += () => _boot!.Preferences.SetOpen(false);
            _menu.QuitRequested += Quit;
            _menu.LoadRequested += OnLoadSave;
            _boot.Preferences.Changed += OnPreferencesChanged;
        }

        /// <summary>One row of the root screen, in the idiom every list row in this HUD uses.</summary>
        VisualElement StartRow(SessionCommand command)
        {
            var row = new VisualElement();
            row.AddToClassList("settings__row");

            var icon = new IconBadge(command.Key, IconBadge.RowSize);
            icon.Inherit(HudTokens.TextMeta);
            row.Add(icon);
            row.Add(HudText.Make(command.Label, HudTextRole.Row, ussClass: "settings__label"));
            row.tooltip = SessionTooltip(command);
            row.RegisterCallback<ClickEvent>(_ => _menu.Choose(command.Key));

            _startRowByKey[command.Key] = row;
            return row;
        }

        /// <summary>
        /// A plain row of the New game screen: the same icon-and-label row as every other, with an
        /// action of its own rather than a <see cref="SessionCommand"/> behind it.
        /// </summary>
        static VisualElement SeedRow(string key, System.Action pressed)
        {
            var row = new VisualElement();
            row.AddToClassList("settings__row");

            var icon = new IconBadge(key, IconBadge.RowSize);
            icon.Inherit(HudTokens.TextMeta);
            row.Add(icon);
            row.Add(HudText.Make(Registry.Label(key), HudTextRole.Row, ussClass: "settings__label"));
            row.RegisterCallback<ClickEvent>(_ => pressed());
            return row;
        }

        /// <summary>
        /// The New game screen (U39): the seed you can read, type and reroll, and the row that
        /// builds a world from it. Design is <c>docs/design/17-start-flow.md</c> §11.
        ///
        /// <para><b>It introduces no control.</b> The caption is a <c>HudText</c> meta line, the box
        /// is the <c>.field</c> the naming prompt restyled before it, and the two rows are
        /// <c>.settings__row</c> like every other row on this screen — which is the bargain §3 makes
        /// and the reason this unit is small.</para>
        ///
        /// <para><b>The field is not focused on the way in</b>, unlike the naming prompt's. There
        /// the whole act is typing; here the common act is pressing Start on the number the game
        /// dealt, and a focused field would put a text cursor and a swallowed keystroke between the
        /// player and that.</para>
        /// </summary>
        VisualElement BuildNewGameScreen()
        {
            var screen = new VisualElement();
            screen.style.display = DisplayStyle.None;

            screen.Add(HudText.Make(Registry.Label(SeedField.SeedKey), HudTextRole.Meta,
                ussClass: "startscreen__seedcap"));

            // Named so a PlayMode test can drive the control the player drives rather than the
            // director behind it — this box is the only place the seam between the two is real.
            _seedBox = new TextField { name = "seed", isDelayed = false, maxLength = SeedEntry.MaxDigits };
            _seedBox.AddToClassList("field");
            _seedBox.RegisterValueChangedCallback(change => _menu.Seed.Type(change.newValue));
            screen.Add(_seedBox);

            var rows = new VisualElement();
            rows.AddToClassList("startscreen__seedrows");
            rows.Add(SeedRow(SeedField.RerollKey, () => _menu.Seed.Reroll()));

            // Next since U40, not Start: the commit moved to the colonist screen after it.
            _startCommit = SeedRow(SeedField.NextKey, () => _menu.Next());
            rows.Add(_startCommit);

            screen.Add(rows);
            return screen;
        }

        /// <summary>
        /// The colonist screen (U40): three cards, a reroll and the row that commits. Design is
        /// <c>docs/design/18-colonist-select.md</c>.
        ///
        /// <para><b>A card is clicked to keep it.</b> There is no separate lock control, because a
        /// per-card reroll and a lock are the same thing said twice — keeping the two you like and
        /// pressing Reroll is the same act as rerolling the third, with one rule to learn instead
        /// of two.</para>
        /// </summary>
        VisualElement BuildColonistScreen()
        {
            var screen = new VisualElement();
            screen.style.display = DisplayStyle.None;

            screen.Add(HudText.Make(Registry.Label(ColonistSelect.TitleKey), HudTextRole.Meta,
                ussClass: "startscreen__seedcap"));

            _colonistCards = new VisualElement();
            _colonistCards.AddToClassList("colonists");
            screen.Add(_colonistCards);

            for (int slot = 0; slot < ColonistSelect.Slots; slot++)
            {
                int index = slot;

                var card = new VisualElement();
                card.AddToClassList("colonist");
                card.Add(HudText.Make(string.Empty, HudTextRole.Row, ussClass: "colonist__name"));
                card.Add(HudText.Make(string.Empty, HudTextRole.Meta, numeric: true,
                    ussClass: "colonist__skills"));
                card.RegisterCallback<ClickEvent>(_ => _menu.Colonists!.ToggleLock(index));
                _colonistCards.Add(card);
            }

            var rows = new VisualElement();
            rows.AddToClassList("startscreen__seedrows");

            _colonistReroll = SeedRow(ColonistSelect.RerollKey, () => _menu.Colonists!.Reroll(SeedEntry.Draw));
            rows.Add(_colonistReroll);
            rows.Add(SeedRow(SeedField.StartKey, () => _menu.Start()));

            screen.Add(rows);
            return screen;
        }

        /// <summary>
        /// Draw the three candidates, and which of them the player has kept.
        ///
        /// <para>A kept card wears <c>row--armed</c>, the same lit treatment every "this one is on"
        /// in the interface uses — the palette's armed chip, the settings panel's asking row. One
        /// idiom, so a player who has seen it anywhere has seen it here.</para>
        /// </summary>
        void RefreshColonists()
        {
            ColonistSelect? select = _menu.Colonists;
            if (select == null) return;

            for (int slot = 0; slot < ColonistSelect.Slots && slot < _colonistCards.childCount; slot++)
            {
                VisualElement card = _colonistCards[slot];
                Candidate who = select.Cards[slot];

                HudText.Set((Label)card[0], who.Name, HudTextRole.Row);
                HudText.Set((Label)card[1], SkillLine(who), HudTextRole.Meta);
                card.EnableInClassList("row--armed", select.IsLocked(slot));
                card.tooltip = select.IsLocked(slot)
                    ? who.Name + " stays through a reroll. Click to let them go"
                    : "Click to keep " + who.Name + " through a reroll";
            }

            // Inert when there is nothing left to reroll, rather than accepting the press and
            // sitting there — the same answer the Start row gives an unusable seed.
            _colonistReroll.EnableInClassList("settings__row--off", !select.CanReroll);
        }

        /// <summary>
        /// A candidate's skills on one line — "Mining 6 · Cutting 3".
        ///
        /// <para>One line rather than one per skill because three cards of two lines each did not
        /// fit the fixed box: 296 against 284, measured by <c>HudLayoutTests</c> rather than
        /// discovered on screen. It also reads better, as one fact about a person.</para>
        /// </summary>
        static string SkillLine(Candidate who)
        {
            var line = new System.Text.StringBuilder();
            for (int i = 0; i < who.Skills.Count; i++)
            {
                if (line.Length > 0) line.Append(" · ");
                line.Append(who.Skills[i].Label).Append(' ').Append(who.Skills[i].Level);
            }
            return line.ToString();
        }

        /// <summary>
        /// Draw what the seed field is holding.
        ///
        /// <para><c>SetValueWithoutNotify</c>, and only when the text has actually moved: writing
        /// the box's own value back into it through the notifying setter would re-enter
        /// <see cref="SeedField.Type"/> on every keystroke, and the guard that makes that harmless
        /// is a guard rather than a reason to lean on it.</para>
        /// </summary>
        void RefreshSeed()
        {
            if (_seedBox.value != _menu.Seed.Text) _seedBox.SetValueWithoutNotify(_menu.Seed.Text);

            // Drawn faint and inert rather than hidden: a Start that disappears while you are
            // mid-edit reads as a broken screen. MenuDirector.Start refuses as well — this is the
            // half the player can see, not the half that enforces it.
            _startCommit.EnableInClassList("settings__row--off", !_menu.Seed.Usable);
        }

        /// <summary>
        /// A session was built or torn down. The start screen is on screen for exactly the second
        /// of those, so this is the one thing that decides whether it shows.
        ///
        /// <para>It also swaps which directors the shell is driven by. The pair that matters —
        /// settings and hotkeys — is the same object either way, so the unsubscribe and resubscribe
        /// either side of the swap cancel out for those and only re-point the world-shaped ones.
        /// </para>
        /// </summary>
        void OnSessionChanged()
        {
            HudDirectors? live = _boot!.Directors;

            if (!ReferenceEquals(_directors, live ?? _screenDirectors))
            {
                Detach();
                Attach(live ?? _screenDirectors!);
            }

            _menu.SetShowing(live == null);
        }

        void RefreshStartScreen()
        {
            if (!_menu.Showing)
            {
                _startScreen.Show(false);
                return;
            }

            // The settings screen is the settings panel standing where this one was, not on top of
            // it (owner, 2026-09-17). The scrim stays, because the state is still modal and there
            // is still no world behind any of it.
            if (_menu.Screen == MenuScreen.Settings)
            {
                _startScreen.ShowScrimOnly();
                return;
            }

            _startScreen.Show(true);

            bool root = _menu.Screen == MenuScreen.Root;
            bool newGame = _menu.Screen == MenuScreen.NewGame;
            bool load = _menu.Screen == MenuScreen.Load;
            bool colonists = _menu.Screen == MenuScreen.Colonists;

            _startRows.style.display = root ? DisplayStyle.Flex : DisplayStyle.None;
            _startNewGame.style.display = newGame ? DisplayStyle.Flex : DisplayStyle.None;
            _startColonists.style.display = colonists ? DisplayStyle.Flex : DisplayStyle.None;
            _startList.style.display = load ? DisplayStyle.Flex : DisplayStyle.None;

            // The way out of every screen but the root, which has nowhere to go back to.
            _startBack.style.display = root ? DisplayStyle.None : DisplayStyle.Flex;

            if (load) FillSaveList();
            if (newGame) RefreshSeed();
            if (colonists) RefreshColonists();
            RefreshStartArming();
        }

        /// <summary>
        /// The settings panel was closed — by its X, by Escape, or by anything else — so the start
        /// screen comes back.
        ///
        /// <para>Driven by the panel rather than by the row that opened it, because the ways out of
        /// that panel already exist and this must be all of them. <see cref="MenuDirector.Back"/>
        /// raises <c>SettingsClosed</c>, which asks the panel to close again; that is a no-op, since
        /// <c>SetOpen</c> returns early when nothing changes.</para>
        /// </summary>
        void OnPreferencesChanged()
        {
            if (_menu.Screen == MenuScreen.Settings && !_boot!.Preferences.Open) _menu.Back();
        }

        void RefreshStartArming()
        {
            foreach (KeyValuePair<string, VisualElement> row in _startRowByKey)
                row.Value.EnableInClassList("row--armed", _menu.Armed == row.Key);
        }

        /// <summary>
        /// Draw the listing the director is holding. An unreadable save is drawn and not
        /// clickable, with the reason where the day would be — §5's rule, and the reason for it is
        /// that a colony which disappears from this list is a colony the player concludes is gone.
        /// </summary>
        void FillSaveList()
        {
            _startList.Clear();

            if (_menu.Saves.Count == 0)
            {
                var empty = new VisualElement();
                empty.AddToClassList("save");
                empty.Add(HudText.Make(Registry.Label("ui.start.empty"), HudTextRole.Row,
                    ussClass: "save__name"));
                _startList.Add(empty);
                return;
            }

            for (int i = 0; i < _menu.Saves.Count; i++)
            {
                SaveRow save = _menu.Saves[i];

                var row = new VisualElement();
                row.AddToClassList("save");
                row.Add(HudText.Make(save.Name, HudTextRole.Row, ussClass: "save__name"));

                // The colony, the day and when it was written — the line that tells two saves
                // apart once the title is a name the player chose. Asked for by the owner after
                // playing it (2026-09-17). A figure, so mono, like every other figure on this
                // screen.
                row.Add(HudText.Make(
                    save.Readable ? $"{save.Colony} · Day {save.Day} · {save.When}" : save.Problem,
                    HudTextRole.Meta, numeric: save.Readable, "save__meta"));

                if (save.Readable)
                {
                    int index = i;
                    row.tooltip = "Open " + save.Colony;
                    row.RegisterCallback<ClickEvent>(_ => _menu.ChooseSave(index));
                }
                else
                {
                    row.AddToClassList("save--bad");
                    row.tooltip = "This build cannot open this save. It is listed rather than " +
                                  "hidden, so a colony never simply disappears";
                }

                _startList.Add(row);
            }
        }

        /// <summary>
        /// Read the folder and hand the director what it found.
        ///
        /// <para>The director navigates when the rows arrive rather than when Load is pressed, so
        /// there is never a frame showing the previous listing — and so the rows it holds are the
        /// rows the player was looking at when they clicked one.</para>
        /// </summary>
        void OnListSaves()
        {
            _savePathById.Clear();
            var rows = new List<SaveRow>();

            foreach (SaveEntry entry in SaveFiles.List())
            {
                _savePathById[entry.Path] = entry.Path;
                rows.Add(entry.IsReadable
                    ? new SaveRow(entry.Path, SaveFiles.TitleOf(entry), entry.Header!.Recipe.Day,
                        entry.Header.Recipe.Map.ToString(), problem: string.Empty,
                        when: SaveFiles.WhenOf(entry), colony: SaveFiles.ColonyOf(entry))
                    : new SaveRow(entry.Path, SaveFiles.TitleOf(entry), 0, string.Empty,
                        entry.Problem ?? Registry.Label("ui.start.unreadable"),
                        when: SaveFiles.WhenOf(entry)));
            }

            _menu.ShowSaves(rows);
        }

        /// <summary>
        /// A new colony, on the seed the player was shown (U39).
        ///
        /// <para>The seed arrives with the request rather than being read back off the field here,
        /// which is the one thing <c>MenuDirector.StartRequested</c> carries an argument for: a
        /// presenter that fetched the number separately could fetch a different one, or fetch it
        /// without the guard that says the box names a seed at all.</para>
        ///
        /// <para>Everything else about the request stays a default on the bootstrap — size, map
        /// type and scenario — which is what the plan's U39 row asks for: the seed is the only knob
        /// exposed, so the rest stay tunable later without new interface.</para>
        /// </summary>
        void OnStartNewGame(NewGameChoice choice) =>
            _boot!.BuildSession(choice.Seed, null, choice.Colonists);

        // ============================================================ naming a save

        readonly SavePrompt _prompt = new SavePrompt();

        HudModal _promptModal = null!;
        TextField _promptField = null!;
        VisualElement _promptConfirm = null!;
        Label _promptConfirmLabel = null!;
        Label _promptNote = null!;

        /// <summary>The naming prompt, for a test that wants to drive it without a pointer.</summary>
        public SavePrompt Prompt => _prompt;

        /// <summary>
        /// The prompt that names a save (owner, 2026-09-17).
        ///
        /// <para><b>Two firsts in one small box.</b> It is the project's first text field — Unity's
        /// default is a pale control with a blue focus ring, so it is restyled in the sheet the way
        /// the palette's scroller and the volume slider were before it — and the first modal raised
        /// over a <i>running</i> colony, the start screen's having nothing behind it. The world
        /// carries on underneath, which is the owner's standing answer for what a modal does.</para>
        /// </summary>
        void BuildSavePrompt()
        {
            _promptModal = Modal("saveprompt", Registry.Label(SavePrompt.TitleKey),
                () => _prompt.Cancel(), "prompt");

            _promptField = new TextField { isDelayed = false };
            _promptField.AddToClassList("field");
            _promptField.RegisterValueChangedCallback(change => OnNameTyped(change.newValue));
            _promptModal.Panel.Add(_promptField);

            _promptNote = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "prompt__note");
            _promptModal.Panel.Add(_promptNote);

            var answers = new VisualElement();
            answers.AddToClassList("prompt__answers");

            _promptConfirm = new VisualElement();
            _promptConfirm.AddToClassList("prompt__answer");
            _promptConfirmLabel = HudText.Make(Registry.Label(SavePrompt.ConfirmKey), HudTextRole.Row);
            _promptConfirm.Add(_promptConfirmLabel);
            _promptConfirm.RegisterCallback<ClickEvent>(_ => _prompt.Confirm());
            answers.Add(_promptConfirm);

            var cancel = new VisualElement();
            cancel.AddToClassList("prompt__answer");
            cancel.Add(HudText.Make(Registry.Label(SavePrompt.CancelKey), HudTextRole.Row));
            cancel.RegisterCallback<ClickEvent>(_ => _prompt.Cancel());
            answers.Add(cancel);

            _promptModal.Panel.Add(answers);

            _prompt.Changed += RefreshSavePrompt;
            _prompt.Confirmed += OnSaveNamed;
        }

        /// <summary>
        /// What the folder makes of a name. The prompt cannot look at a disk — it is compiled
        /// without one — so the presenter answers, on every keystroke.
        /// </summary>
        /// <remarks>
        /// <b>Nothing is excused from the collision.</b> <c>SaveCatalogue.NameIsTaken</c> can be
        /// told to ignore the file a session is bound to, which is what stops plain Save asking to
        /// overwrite its own save — but plain Save never reaches this prompt when it is bound, it
        /// writes straight through. Everything that gets here is either a colony with no file yet
        /// or a deliberate Save as, and in both of those a name already in use really is an
        /// overwrite and deserves the question.
        /// </remarks>
        SaveNameStatus StatusOf(string? name)
        {
            if (!SaveCatalogue.IsUsableName(name)) return SaveNameStatus.Unusable;

            return SaveFiles.NameIsTaken(name!) ? SaveNameStatus.Taken : SaveNameStatus.Free;
        }

        void OnNameTyped(string typed) => _prompt.Type(typed, StatusOf(typed));

        /// <summary>
        /// Open the prompt, offering a name: this session's own if it has one, the colony's
        /// otherwise. The settings panel goes first, so the player is not naming a save through
        /// two stacked windows.
        /// </summary>
        void OpenSavePrompt()
        {
            _boot!.Preferences.SetOpen(false);

            string suggested = _boot.SuggestedSaveName();
            _prompt.Show(suggested, StatusOf(suggested));

            // SetValueWithoutNotify, or the field's own change event would call Type again with
            // the name the director was just given and stand down any arming that came with it.
            _promptField.SetValueWithoutNotify(_prompt.Name);
            _promptField.Focus();
        }

        void RefreshSavePrompt()
        {
            _promptModal.Show(_prompt.Showing);
            if (!_prompt.Showing) return;

            HudText.Set(_promptConfirmLabel, Registry.Label(_prompt.ActionKey), HudTextRole.Row);
            _promptConfirm.EnableInClassList("prompt__answer--off", !_prompt.CanConfirm);
            _promptConfirm.EnableInClassList("row--armed", _prompt.Armed);

            _promptNote.EnableInClassList("prompt__note--warn", _prompt.Status != SaveNameStatus.Free);
            HudText.Set(_promptNote, NoteFor(), HudTextRole.Meta);
        }

        /// <summary>
        /// What is about to happen, in words, before it happens — the only warning there is, since
        /// the write itself is instant and silent.
        /// </summary>
        string NoteFor() => _prompt.Status switch
        {
            SaveNameStatus.Unusable => "That name cannot be a file. Try letters and digits.",
            SaveNameStatus.Taken when _prompt.Armed => "This will replace the save of that name.",
            SaveNameStatus.Taken => "A save of that name already exists.",
            _ => "Saved under this name. Saving again will write over it.",
        };

        void OnSaveNamed(string name)
        {
            string path = _boot!.SaveSessionAs(name);
            Debug.Log($"[Odyssey] saved to {path}");
        }

        /// <summary>
        /// A session row of the settings panel was confirmed (U38).
        ///
        /// <para><b>Load in game goes through the start screen rather than opening a file
        /// directly</b>, and that is the point rather than a shortcut: the colony on screen is
        /// about to be thrown away, so the player sees the panel close, the world go, and the list
        /// of saves appear — the same three steps as Quit to main menu followed by Load, because
        /// that is exactly what it is. The row asks twice before any of it.</para>
        /// </summary>
        void OnSessionRow(string key)
        {
            switch (key)
            {
                case SessionCommands.SaveKey:
                {
                    // Over the file this session is bound to. Nothing bound means this colony has
                    // never been named, so the prompt asks — which is also the only time Save and
                    // Save as do the same thing.
                    string? path = _boot!.SaveSession();
                    if (path == null) { OpenSavePrompt(); break; }

                    // Closed after the write, so the panel is not still open over a colony whose
                    // file has already changed — and so the player sees that something happened.
                    _boot.Preferences.SetOpen(false);
                    Debug.Log($"[Odyssey] saved to {path}");
                    break;
                }

                case SessionCommands.SaveAsKey:
                    OpenSavePrompt();
                    break;

                case SessionCommands.LoadKey:
                    _boot!.Preferences.SetOpen(false);
                    _boot.TeardownSession();
                    _menu.Choose(SessionCommands.LoadKey);
                    break;

                case SessionCommands.QuitToMenuKey:
                    _boot!.Preferences.SetOpen(false);
                    _boot.TeardownSession();
                    break;

                // The exit row keeps its own path: SettingsPresenter has listened to
                // ExitRequested since before this panel had any other session row.
            }
        }

        void OnLoadSave(SaveRow row)
        {
            if (!_savePathById.TryGetValue(row.Id, out string? path)) return;
            _boot!.LoadSession(path);
        }

        /// <summary>
        /// Leave. The same two lines <c>SettingsPresenter</c> already uses for the exit row, and
        /// the editor case matters as much as the player one: <c>Application.Quit</c> does nothing
        /// in the editor, so without it the row would silently do nothing on the only machine
        /// anybody is testing on.
        /// </summary>
        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            Application.Quit();
        }
    }
}
