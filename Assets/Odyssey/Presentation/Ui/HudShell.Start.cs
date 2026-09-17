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

        // The setup page: the board across the top, the people below it. Full-viewport, not a
        // panel — `docs/design/19-world-setup.md` §1 says why.
        VisualElement _setupPage = null!;
        TextField _seedBox = null!;
        TextField _colonyBox = null!;
        Label _sizeLabel = null!;
        VisualElement _startCommit = null!;
        VisualElement _startColonists = null!;
        VisualElement _colonistCards = null!;
        VisualElement _colonistReroll = null!;
        VisualElement _colonistKeep = null!;
        VisualElement _colonistDetail = null!;
        Label _detailName = null!;
        Label _detailTrade = null!;
        Label _detailTraits = null!;
        VisualElement _detailSkills = null!;
        readonly List<SkillLineView> _detailSkillViews = new List<SkillLineView>();

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
            PawnId id = ColonistDraw.IdForSlot(slot);

            // Every skill, in the same reading order the inspect pane's Skills tab uses, built into
            // the same SkillRow that pane is drawn from — so one grid draws both. The nine nothing
            // simulates are present and greyed, which is what the owner asked for and what stops
            // the card hiding that the rest exist.
            IReadOnlyList<SkillCatalogue.Entry> order = SkillCatalogue.ReadingOrder;
            var rows = new List<SkillRow>(order.Count);

            foreach (SkillCatalogue.Entry entry in order)
            {
                var row = new SkillRow
                {
                    IconKey = entry.Key,
                    Name = Registry.Label(entry.Key),
                    Live = entry.Live,
                    Reason = entry.Reason,
                    Note = entry.Note,
                };

                if (entry.Live)
                {
                    int index = System.Array.IndexOf(SkillIndex.Names, entry.Skill);
                    if (index >= 0)
                    {
                        row.Level = rolled.SkillLevel(index);
                        row.Passion = rolled.Passions[index];
                        row.Experience = rolled.Skills[index];
                    }
                }

                rows.Add(row);
            }

            return new Candidate(seed, ColonistNames.Of(seed, id),
                ColonistIdentity.Age(seed, id), ColonistIdentity.Occupation(seed, id), rows);
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

            _setupPage = BuildSetupPage();
            _hud.Add(_setupPage);

            _menu.Changed += RefreshSetupPage;
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
        /// <summary>
        /// The setup page: everything about a new game, on one full-viewport screen.
        ///
        /// <para><b>Not a panel, and that is the whole shape of this unit.</b> The owner asked for
        /// the inspect pane's entire skills grid on a candidate; that grid is nine hundred pixels
        /// wide, and three candidates plus a seed plus a detail beside them is simply not something
        /// a 420 × 384 box can hold. So New game leaves the menu box rather than stretching it, and
        /// the box is left exactly as it is for Load and Settings — nothing the owner has already
        /// approved moves.</para>
        /// </summary>
        VisualElement BuildSetupPage()
        {
            var page = new VisualElement { name = "setup" };
            page.AddToClassList("setup");
            page.style.display = DisplayStyle.None;

            page.Add(HudText.Make(Registry.Label(SeedField.TitleKey), HudTextRole.Name,
                ussClass: "setup__title"));

            // ---- the board -------------------------------------------------------------
            var board = new VisualElement();
            board.AddToClassList("setup__board");

            board.Add(Captioned(SeedField.ColonyKey, _colonyBox = Field("colony", 32,
                text => _menu.TypeColonyName(text))));
            board.Add(Captioned(SeedField.SeedKey, _seedBox = Field("seed", SeedEntry.MaxDigits,
                text => _menu.Seed.Type(text))));

            var reroll = SeedRow(SeedField.RerollKey, () => _menu.Seed.Reroll());
            reroll.AddToClassList("setup__inline");
            board.Add(reroll);

            // One control that cycles rather than three rows: there are three sizes and a player
            // picking one is cycling, not navigating.
            var size = new VisualElement();
            size.AddToClassList("setup__size");
            size.Add(HudText.Make(Registry.Label(SeedField.SizeKey), HudTextRole.Meta,
                ussClass: "startscreen__seedcap"));
            _sizeLabel = HudText.Make(string.Empty, HudTextRole.Row, ussClass: "setup__sizevalue");
            size.Add(_sizeLabel);
            size.RegisterCallback<ClickEvent>(_ => _menu.NextSize());
            size.tooltip = "How much ground the colony has. Click to change";
            board.Add(size);

            page.Add(board);

            // ---- the people ------------------------------------------------------------
            _startColonists = BuildColonistScreen();
            _startColonists.style.display = DisplayStyle.Flex;
            page.Add(_startColonists);

            // ---- the way out and the way in --------------------------------------------
            var footer = new VisualElement();
            footer.AddToClassList("setup__footer");

            var back = SeedRow("ui.start.back", () => _menu.Back());
            back.AddToClassList("setup__inline");
            footer.Add(back);

            _startCommit = SeedRow(SeedField.StartKey, () => _menu.Start());
            _startCommit.AddToClassList("setup__inline");
            footer.Add(_startCommit);

            page.Add(footer);
            return page;
        }

        /// <summary>A text field of this interface's one restyled kind, named so a test can drive
        /// the control a player drives rather than the director behind it.</summary>
        static TextField Field(string name, int maxLength, System.Action<string> typed)
        {
            var field = new TextField { name = name, isDelayed = false, maxLength = maxLength };
            field.AddToClassList("field");
            field.RegisterValueChangedCallback(change => typed(change.newValue));
            return field;
        }

        /// <summary>A control with the quiet caption every figure in this interface is introduced
        /// by.</summary>
        static VisualElement Captioned(string key, VisualElement control)
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("setup__field");
            wrap.Add(HudText.Make(Registry.Label(key), HudTextRole.Meta,
                ussClass: "startscreen__seedcap"));
            wrap.Add(control);
            return wrap;
        }

        /// <summary>
        /// The colonist screen: three candidates down the left, the selected one's whole record on
        /// the right. Design is <c>docs/design/19-world-setup.md</c>.
        ///
        /// <para><b>Clicking a candidate shows them; a separate Keep holds them.</b> Two gestures,
        /// because on this page a click already means "show me this one" and one gesture cannot
        /// mean two things. `18`'s little panel had a card mean only "keep", which was right there
        /// and is wrong here.</para>
        /// </summary>
        VisualElement BuildColonistScreen()
        {
            var screen = new VisualElement();
            screen.AddToClassList("setup__people");
            screen.style.display = DisplayStyle.None;

            _colonistCards = new VisualElement();
            _colonistCards.AddToClassList("colonists");
            screen.Add(_colonistCards);

            for (int slot = 0; slot < ColonistSelect.Slots; slot++)
            {
                int index = slot;

                var card = new VisualElement();
                card.AddToClassList("colonist");
                card.Add(HudText.Make(string.Empty, HudTextRole.Row, ussClass: "colonist__name"));
                card.Add(HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "colonist__trade"));
                card.RegisterCallback<ClickEvent>(_ => _menu.Colonists!.Select(index));
                _colonistCards.Add(card);
            }

            var rows = new VisualElement();
            rows.AddToClassList("colonists__rows");

            _colonistKeep = SeedRow(ColonistSelect.LockKey,
                () => _menu.Colonists!.ToggleLock(_menu.Colonists.Selected));
            rows.Add(_colonistKeep);

            _colonistReroll = SeedRow(ColonistSelect.RerollKey,
                () => _menu.Colonists!.Reroll(SeedEntry.Draw));
            rows.Add(_colonistReroll);

            _colonistCards.Add(rows);

            // The detail: name and age, trade, the traits row that M7 will fill, and the whole
            // skills grid — the inspect pane's own, built by the same method.
            _colonistDetail = new VisualElement();
            _colonistDetail.AddToClassList("setup__detail");

            _detailName = HudText.Make(string.Empty, HudTextRole.Name, ussClass: "detail__name");
            _detailTrade = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "detail__trade");
            _detailTraits = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "detail__traits");
            _colonistDetail.Add(_detailName);
            _colonistDetail.Add(_detailTrade);
            _colonistDetail.Add(_detailTraits);

            _detailSkills = new VisualElement();
            _detailSkills.AddToClassList("skills");
            _colonistDetail.Add(_detailSkills);
            for (int i = 0; i < SkillCatalogue.ReadingOrder.Count; i++)
                _detailSkillViews.Add(SkillLine(_detailSkills));

            screen.Add(_colonistDetail);
            return screen;
        }

        /// <summary>
        /// Draw the three, which one is showing, which are kept, and the selected one's record.
        ///
        /// <para>A kept card wears <c>row--armed</c>, the same lit treatment every "this one is on"
        /// in the interface uses — the palette's armed chip, the settings panel's asking row. The
        /// one being <i>read</i> wears <c>colonist--on</c>, which is a different question and needs
        /// a different mark.</para>
        /// </summary>
        void RefreshColonists()
        {
            ColonistSelect? select = _menu.Colonists;
            if (select == null) return;

            for (int slot = 0; slot < ColonistSelect.Slots && slot < _colonistCards.childCount; slot++)
            {
                VisualElement card = _colonistCards[slot];
                Candidate who = select.Cards[slot];

                HudText.Set((Label)card[0], who.NameAndAge, HudTextRole.Row);
                HudText.Set((Label)card[1], who.Occupation, HudTextRole.Meta);
                card.EnableInClassList("row--armed", select.IsLocked(slot));
                card.EnableInClassList("colonist--on", select.Selected == slot);
                card.tooltip = select.IsLocked(slot)
                    ? who.Name + " is kept through a reroll"
                    : "Show " + who.Name;
            }

            Candidate current = select.Current;
            HudText.Set(_detailName, current.NameAndAge, HudTextRole.Name);
            HudText.Set(_detailTrade, current.Occupation, HudTextRole.Meta);

            // Drawn now and empty until M7, by the owner's decision. It says "—" rather than
            // nothing, because a row that is absent and a row that is empty look identical and
            // only one of them is a promise.
            HudText.Set(_detailTraits, "Traits  —", HudTextRole.Meta);

            for (int i = 0; i < current.Skills.Count && i < _detailSkillViews.Count; i++)
                SetSkillLine(_detailSkillViews, i, current.Skills[i]);

            // Inert when there is nothing left to reroll, rather than accepting the press and
            // sitting there — the same answer the Start row gives an unusable seed.
            _colonistReroll.EnableInClassList("settings__row--off", !select.CanReroll);
            _colonistKeep.EnableInClassList("row--armed", select.IsLocked(select.Selected));
        }

        /// <summary>
        /// Draw what the seed field is holding.
        ///
        /// <para><c>SetValueWithoutNotify</c>, and only when the text has actually moved: writing
        /// the box's own value back into it through the notifying setter would re-enter
        /// <see cref="SeedField.Type"/> on every keystroke, and the guard that makes that harmless
        /// is a guard rather than a reason to lean on it.</para>
        /// </summary>
        /// <summary>Draw the parts of the page that are neither the seed nor the people: the
        /// colony's name and the board size.</summary>
        void RefreshSetupPage()
        {
            if (_colonyBox.value != _menu.ColonyName)
                _colonyBox.SetValueWithoutNotify(_menu.ColonyName);

            HudText.Set(_sizeLabel, MapSizes.At(_menu.Size).Label, HudTextRole.Row);
        }

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
            // The setup page is not a screen of the panel; it stands in its place, full viewport,
            // and the panel goes away entirely while it is up.
            bool setup = _menu.Showing && _menu.Screen == MenuScreen.NewGame;
            _setupPage.style.display = setup ? DisplayStyle.Flex : DisplayStyle.None;
            if (setup)
            {
                _startScreen.ShowScrimOnly();
                RefreshSetupPage();
                RefreshSeed();
                RefreshColonists();
                return;
            }

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
            bool load = _menu.Screen == MenuScreen.Load;

            _startRows.style.display = root ? DisplayStyle.Flex : DisplayStyle.None;
            _startList.style.display = load ? DisplayStyle.Flex : DisplayStyle.None;

            // The way out of every screen but the root, which has nowhere to go back to.
            _startBack.style.display = root ? DisplayStyle.None : DisplayStyle.Flex;

            if (load) FillSaveList();
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
        void OnStartNewGame(NewGameChoice choice)
        {
            MapSizes.Choice size = MapSizes.At(choice.Size);
            _boot!.BuildSession(choice.Seed, null, choice.Colonists, choice.Name,
                new GridSize(size.X, size.Z, size.Y));
        }

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
