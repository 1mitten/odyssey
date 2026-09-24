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
        readonly List<AvatarGlyph> _colonistFaces = new List<AvatarGlyph>();
        AvatarGlyph _detailFace = null!;
        Label _detailName = null!;
        Label _detailTrade = null!;
        Label _detailTraits = null!;
        VisualElement _detailSkills = null!;
        readonly List<SkillLineView> _detailSkillViews = new List<SkillLineView>();

        /// <summary>
        /// The box a colonist is renamed in — one, moved to whichever card is being edited.
        ///
        /// <para><b>One widget rather than three.</b> Three fields would be three things that can
        /// hold focus, three that can be left with a half-typed name in them, and three that the
        /// refresh has to keep in step with a reroll. Moving one is a single <c>Add</c> in UI
        /// Toolkit, and it makes "only one name is being typed at a time" true by construction
        /// instead of by care.</para>
        /// </summary>
        TextField _renameBox = null!;

        /// <summary>Which card the box is in, or -1 when nobody is being renamed.</summary>
        int _renameSlot = -1;

        /// <summary>What the slot was called when the edit began, so Escape can put it back.</summary>
        string? _renameWas;

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

            // Rolled, not Of: Of would answer from the name book, and slot 0's PawnId is the same
            // 1 the last colony's first colonist had — so a freshly dealt stranger would arrive
            // wearing the name somebody typed in a game that is already over.
            return new Candidate(seed, ColonistNames.Rolled(seed, id),
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

            // The title screen (design 40): a dock, full height and flush left, carrying the mark,
            // the wordmark, four buttons and the build line, with the starfield to its right. Still
            // the modal it was, so the names the tests find it by hold; the window's header — the
            // old card's "ODYSSEY" label — is taken off, because the dock carries its own.
            _startScreen = Modal("start", Registry.Label("ui.start.screen"), null, "startscreen");
            _startScreen.Panel.Q(className: "panel__hdr")?.RemoveFromHierarchy();
            VisualElement body = _startScreen.Panel;

            _titleLogo = BuildTitleLogo();
            body.Add(_titleLogo);

            var divider = new VisualElement();
            divider.AddToClassList("title__divider");
            body.Add(divider);

            _startRows = new VisualElement();
            _startRows.AddToClassList("title__menu");
            body.Add(_startRows);

            foreach (TitleLayout.Button button in TitleLayout.Buttons)
                _startRows.Add(TitleButton(button));

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
            _startBack.Add(HudText.Make(Registry.Label("ui.start.back"), HudTextRole.Row,
                ussClass: "settings__label"));
            _startBack.RegisterCallback<ClickEvent>(_ => _menu.Back());
            _startBack.style.display = DisplayStyle.None;
            body.Add(_startBack);

            body.Add(BuildTitleFooter());
            WireTitleKeyboard();

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
            // Exit game asks first, in the leave prompt's no-colony form (design 40).
            _menu.QuitRequested += _leave.AskToExit;
            _menu.LoadRequested += OnLoadSave;
            _boot.Preferences.Changed += OnPreferencesChanged;
        }

        /// <summary>
        /// One of the title screen's four buttons: an icon in the button's colour, the name and a
        /// line under it. Lit on hover and on focus in the Settings rail's treatment — the colour
        /// at 14%, a 3 px edge on the left, the name in the colour — which the stylesheet carries
        /// per colour (<c>.title__btn--good</c> and the rest).
        /// </summary>
        VisualElement TitleButton(TitleLayout.Button button)
        {
            var row = new VisualElement();
            row.AddToClassList("title__btn");
            row.AddToClassList("title__btn--" + ToneClass(button.Ink));
            row.focusable = true;
            row.tabIndex = 0;

            var edge = new VisualElement { pickingMode = PickingMode.Ignore };
            edge.AddToClassList("title__edge");
            row.Add(edge);

            row.Add(new PathGlyph(button.Icon, TitleLayout.ButtonIcon, HudTokens.Convert(button.Ink)));

            var words = new VisualElement { pickingMode = PickingMode.Ignore };
            words.AddToClassList("title__words");
            Label name = HudText.Make(Registry.Label(button.Key), HudTextRole.Name, ussClass: "title__name");
            name.pickingMode = PickingMode.Ignore;
            if (button.NameTakesInk) name.AddToClassList("title__name--ink");
            words.Add(name);
            Label description = HudText.Make(button.Description, HudTextRole.Meta, ussClass: "title__desc");
            description.pickingMode = PickingMode.Ignore;
            words.Add(description);
            row.Add(words);

            string key = button.Key;
            row.tooltip = SessionTooltip(new SessionCommand(key, Registry.Label(key), SessionContext.MainScreen, 0, false));
            row.RegisterCallback<ClickEvent>(_ => PressTitle(key));
            _startRowByKey[key] = row;
            return row;
        }

        /// <summary>Which of the four colour classes a button wears.</summary>
        static string ToneClass(Odyssey.Hud.HudColour ink) =>
            ink.Equals(HudTheme.Good) ? "good"
            : ink.Equals(HudTheme.Info) ? "info"
            : ink.Equals(HudTheme.Violet) ? "violet"
            : "bad";

        /// <summary>A press on the title screen, remembered so that coming back — from Settings, or
        /// from the load list — puts focus on the button that was pressed.</summary>
        void PressTitle(string key)
        {
            _titleReturnKey = key;
            _menu.Choose(key);
        }

        /// <summary>The mark and the wordmark on one row, the mark drawn as its five slabs.</summary>
        VisualElement BuildTitleLogo()
        {
            var logo = new VisualElement();
            logo.AddToClassList("title__logo");

            var mark = new VisualElement();
            mark.AddToClassList("title__mark");
            float sx = TitleLayout.MarkWidth / (float)TitleLayout.MarkBoxWidth;
            float sy = TitleLayout.MarkHeight / (float)TitleLayout.MarkBoxHeight;
            foreach (TitleLayout.Slab slab in TitleLayout.Mark)
            {
                var box = new VisualElement { pickingMode = PickingMode.Ignore };
                box.style.position = Position.Absolute;
                box.style.left = slab.X * sx;
                box.style.top = slab.Y * sy;
                box.style.width = slab.Width * sx;
                box.style.height = slab.Height * sy;
                box.style.backgroundColor = HudTokens.Convert(slab.Colour);
                mark.Add(box);
            }
            logo.Add(mark);

            _titleWordmark = HudText.Make("ODYSSEY", HudTextRole.Name, ussClass: "title__wordmark");
            _titleWordmark.style.fontSize = TitleLayout.WordmarkSize;
            _titleWordmark.style.letterSpacing = TitleLayout.WordmarkSize * TitleLayout.WordmarkTracking;
            logo.Add(_titleWordmark);
            return logo;
        }

        VisualElement BuildTitleFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("title__footer");
            Label version = HudText.Make(TitleLayout.VersionLine(TitleLayout.Version), HudTextRole.Meta,
                numeric: true, ussClass: "title__cap");
            version.style.fontSize = 11;
            footer.Add(version);
            return footer;
        }

        /// <summary>
        /// The dock's keyboard: Up and Down move between the buttons, Enter and Space press, and
        /// focus from a mouse press is given back on release so only the keyboard shows the ring.
        /// Escape is nobody's here: <c>SettingsDirector.Escape</c> already answers Nothing on the
        /// root screen.
        /// </summary>
        void WireTitleKeyboard()
        {
            VisualElement dock = _startScreen.Panel;

            _titleRing = new VisualElement { pickingMode = PickingMode.Ignore };
            _titleRing.AddToClassList("title__ring");
            _titleRing.style.display = DisplayStyle.None;
            dock.Add(_titleRing);

            dock.RegisterCallback<PointerDownEvent>(_ => _titlePointer = true, TrickleDown.TrickleDown);
            dock.RegisterCallback<PointerUpEvent>(_ => dock.schedule.Execute(() =>
            {
                _titlePointer = false;
                if (dock.focusController?.focusedElement is VisualElement f && dock.Contains(f)) f.Blur();
            }), TrickleDown.TrickleDown);

            dock.RegisterCallback<FocusInEvent>(evt =>
            {
                if (evt.target is VisualElement target && !_titlePointer && target.ClassListContains("title__btn"))
                {
                    Rect local = dock.WorldToLocal(target.worldBound);
                    float grow = SettingsLayout.FocusOffset + SettingsLayout.FocusWidth;
                    _titleRing.style.left = local.x - grow - HudTheme.BorderWidth;
                    _titleRing.style.top = local.y - grow - HudTheme.BorderWidth;
                    _titleRing.style.width = local.width + 2 * grow;
                    _titleRing.style.height = local.height + 2 * grow;
                    _titleRing.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _titleRing.style.display = DisplayStyle.None;
                }
            }, TrickleDown.TrickleDown);
            dock.RegisterCallback<FocusOutEvent>(_ => _titleRing.style.display = DisplayStyle.None,
                TrickleDown.TrickleDown);

            dock.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (!(dock.focusController?.focusedElement is VisualElement focused) ||
                    !focused.ClassListContains("title__btn")) return;
                int at = _startRows.IndexOf(focused);
                switch (evt.keyCode)
                {
                    case KeyCode.UpArrow:
                    case KeyCode.DownArrow:
                        int next = Mathf.Clamp(at + (evt.keyCode == KeyCode.UpArrow ? -1 : 1), 0,
                            _startRows.childCount - 1);
                        _startRows[next].Focus();
                        evt.StopPropagation();
                        break;
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                    case KeyCode.Space:
                        foreach (KeyValuePair<string, VisualElement> pair in _startRowByKey)
                        {
                            if (pair.Value != focused) continue;
                            PressTitle(pair.Key);
                            break;
                        }
                        evt.StopPropagation();
                        break;
                }
            });

            // The logo holds its place down the screen (18.5% of the height, never under 96 px), the
            // wordmark gives up tracking before it would overflow.
            dock.RegisterCallback<GeometryChangedEvent>(_ => LayOutTitle());
        }

        void LayOutTitle()
        {
            VisualElement dock = _startScreen.Panel;
            if (dock.layout.height <= 0f) return;
            _titleLogo.style.marginTop = TitleLayout.LogoTop(dock.layout.height);

            float word = _titleWordmark.layout.width;
            if (word > 0f && word + TitleLayout.MarkWidth + TitleLayout.MarkGap > TitleLayout.ContentWidth)
                _titleWordmark.style.letterSpacing = TitleLayout.WordmarkSize * TitleLayout.WordmarkTrackingTight;
        }

        /// <summary>Put focus on a title button a moment after the screen shows, when it can take
        /// it: New game the first time, the button that was pressed on the way back.</summary>
        void FocusTitleButton()
        {
            string key = _titleReturnKey ?? SessionCommands.NewGameKey;
            if (!_startRowByKey.TryGetValue(key, out VisualElement? button)) return;
            _startScreen.Panel.schedule.Execute(() =>
            {
                if (_menu.Showing && _menu.Screen == MenuScreen.Root && !_boot!.Preferences.Open) button.Focus();
            }).ExecuteLater(30);
        }

        VisualElement _titleLogo = null!;
        Label _titleWordmark = null!;
        VisualElement _titleRing = null!;
        bool _titlePointer;
        string? _titleReturnKey;
        MenuScreen _titlePrevScreen = MenuScreen.Root;

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
            // The same box the in-game menus are, not a new one (owner, 2026-09-18: "use the
            // same transparency/translucent as the in game menus, not to reinvent"). `.panel` and
            // `.window` are what the settings panel, the Menu popover and the start screen's own
            // panel are built from, so the fill, the border and the radius all come from the
            // tokens HudStyleSheetTests already pins; `.setup` only overrides where it sits and
            // how much air it keeps inside.
            var page = new VisualElement { name = "setup" };
            page.AddToClassList("panel");
            page.AddToClassList("window");
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

            // One control that cycles rather than a row each: there are few sizes and a player
            // picking one is cycling, not navigating.
            var size = new VisualElement();
            size.AddToClassList("setup__size");
            size.Add(HudText.Make(Registry.Label(SeedField.SizeKey), HudTextRole.PanelLabel,
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

            // Classed so the window rule can find it: this page carries no X, and the reason is
            // that it has a way out which says in words where it goes. HudGeometryTests asserts
            // the alternative exists rather than taking the exemption on trust.
            var back = SeedRow("ui.start.back", () => _menu.Back());
            back.AddToClassList("setup__inline");
            back.AddToClassList("setup__back");
            footer.Add(back);

            // Green, and the only green row in the game (owner, 2026-09-18: "make the start button
            // green at the button so it's easy to know what to click"). Back and Start sit side by
            // side and read identically otherwise, so the colour is what says which of the two is
            // the way in. HudTheme.Good, the token the interface already means "this is fine" by.
            _startCommit = SeedRow(SeedField.StartKey, () => _menu.Start());
            _startCommit.AddToClassList("setup__inline");
            _startCommit.AddToClassList("setup__commit");
            footer.Add(_startCommit);

            page.Add(footer);
            return page;
        }

        /// <summary>A text field of this interface's one restyled kind, named so a test can drive
        /// the control a player drives rather than the director behind it.</summary>
        TextField Field(string name, int maxLength, System.Action<string> typed,
            System.Action? escape = null)
        {
            var field = new TextField { name = name, isDelayed = false, maxLength = maxLength };
            field.AddToClassList("field");
            field.RegisterValueChangedCallback(change => typed(change.newValue));
            TakesTheKeyboard(field, escape);
            return field;
        }

        /// <summary>
        /// This field gets the keyboard while it has the focus, and the game's keys do not.
        ///
        /// <para><b>Every text field in the game goes through here</b> — the colony's name, the
        /// seed, a colonist's name, the name of a save. Unity's focus cannot do this by itself:
        /// the game's keys are <i>polled</i> from <c>Keyboard.current</c> in six components that
        /// never see a UI event, so a focused field and a camera that pans on W are two systems
        /// reading the same keyboard and neither knows about the other (owner, 2026-09-18).</para>
        ///
        /// <para>Escape goes with it. While a field has the keyboard, Escape leaves the field
        /// rather than unwinding the screen behind it — otherwise the one key a player reaches for
        /// to abandon a half-typed name would disarm their build tool instead.</para>
        /// </summary>
        /// <param name="escape">
        /// What Escape does, when leaving the field is not enough on its own. The save prompt
        /// passes its Cancel, because a player pressing Escape over a modal means the modal and
        /// not the cursor in it; the setup page's fields pass nothing, because there is nothing
        /// behind them to close.
        /// </param>
        void TakesTheKeyboard(TextField field, System.Action? escape = null)
        {
            field.RegisterCallback<FocusInEvent>(_ => Hotkeys().BeginTyping(field));
            field.RegisterCallback<FocusOutEvent>(_ => Hotkeys().EndTyping(field));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape) return;
                evt.StopPropagation();

                // Before the blur, not after. Leaving the field is what ends an edit, so a handler
                // that ran afterwards would be handed a field nobody is editing any more — which
                // is exactly how the colonist rename's Escape came to revert nothing at all.
                escape?.Invoke();
                field.Blur();
            });
        }

        /// <summary>A control with the quiet caption every figure in this interface is introduced
        /// by.</summary>
        /// <summary>
        /// A section heading on the setup page: bigger and bolder than the block under it, at a
        /// step of the existing scale rather than a rung added for this screen. Named from the
        /// registry, like every other word on this page.
        /// </summary>
        static Label SectionHeading(string key) =>
            HudText.Make(Registry.Label(key), HudTextRole.Name, ussClass: "setup__heading");

        /// <summary>What an empty section reads as. The traits block until M7 fills it.</summary>
        const string EmptySection = "—";

        static VisualElement Captioned(string key, VisualElement control)
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("setup__field");
            // PanelLabel: 11/600, upper and tracked — the caption the stores panel, the rail and
            // the alerts list are all introduced by (owner, 2026-09-18: "make all the headers
            // bolder for Colony name, seed, Board size"). Bolder than the meta line it replaces
            // and unmistakably a label rather than a value, which is what a caption over a text
            // field has to be.
            wrap.Add(HudText.Make(Registry.Label(key), HudTextRole.PanelLabel,
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

                // The face, then the three lines about the person whose face it is. A row wrapping
                // a column, which is the shape a roster card already is — the candidate row was a
                // plain column until avatars landed (docs/design/20-avatars.md §3).
                //
                // The third line is what the page is for: who these three are is a name and an
                // occupation, but which of them you want is the skills, and until 2026-09-18 they
                // were only in the detail pane and only for the card you had clicked.
                var card = new VisualElement();
                card.AddToClassList("colonist");

                var face = new AvatarGlyph(HudLayout.ColonistAvatar);
                face.AddToClassList("colonist__face");

                // Identity alone: who this is and what they used to be. The skills line this
                // carried for part of 2026-09-18 came off after the owner played it — the detail
                // pane beside the cards shows all thirteen now, so the card does not have to.
                // Both lines sit a step up the scale, because the page is read at leisure with no
                // world behind it.
                var lines = new VisualElement();
                lines.AddToClassList("colonist__lines");

                Label name = HudText.Make(string.Empty, HudTextRole.Name, ussClass: "colonist__name");

                // The name is the control that renames this person (owner, 2026-09-18: *"ability
                // to rename your colonist on creation by clicking on the name"*). Clicking it
                // shows the card as well, because a player editing a name is looking at that
                // person — and StopPropagation so the card below does not also take the click and
                // count it as a second gesture.
                name.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    _menu.Colonists!.Select(index);
                    BeginRename(index);
                });
                name.tooltip = "Click to name them yourself";

                lines.Add(name);
                lines.Add(HudText.Make(string.Empty, HudTextRole.Row, ussClass: "colonist__trade"));

                card.Add(face);
                card.Add(lines);
                card.RegisterCallback<ClickEvent>(_ => _menu.Colonists!.Select(index));
                _colonistCards.Add(card);
                _colonistFaces.Add(face);
            }

            // Built once and parked: it lives in whichever card is being edited and nowhere at
            // all the rest of the time. Field() is what gives it the interface's one restyled
            // look, its length limit, and — the part that matters here — the keyboard, so that
            // typing a name does not also drive the game behind the page.
            //
            // The name is committed on every keystroke rather than on the way out, so there is no
            // state in which the box and the card disagree about who this is — and no way to lose
            // a name by clicking the wrong thing next. Escape is what backs out, and it puts back
            // what the slot was called when the edit began.
            _renameBox = Field("colonistname", ColonistNameBook.MaxLength,
                text => _menu.Colonists?.Rename(_renameSlot, text),
                escape: RevertRename);
            _renameBox.AddToClassList("colonist__namebox");
            _renameBox.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                evt.StopPropagation();
                _renameBox.Blur(); // committing is what every keystroke already did
            });
            _renameBox.RegisterCallback<FocusOutEvent>(_ => EndRename());

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

            // The portrait and the record it belongs to, side by side. 64 px against 26 + 18 + 4 +
            // 18 of text, so the two end within two pixels of each other — HudLayout.DetailAvatar
            // says so where the number lives.
            _detailFace = new AvatarGlyph(HudLayout.DetailAvatar);
            _detailFace.AddToClassList("detail__face");

            var record = new VisualElement();
            record.AddToClassList("detail__lines");
            record.Add(_detailName);
            record.Add(_detailTrade);

            var portrait = new VisualElement();
            portrait.AddToClassList("detail__record");
            portrait.Add(_detailFace);
            portrait.Add(record);
            _colonistDetail.Add(portrait);

            // Sections, each under a heading of its own (owner, 2026-09-18). Traits used to sit
            // inside the record above, as a third line beside the name and the trade, where it
            // read as another fact about the person rather than as the block it will be once M7
            // fills it. It is a section now, under the skills, with a heading the same weight.
            _colonistDetail.Add(SectionHeading(SeedField.SkillsKey));

            // The same grid the inspect pane's Skills tab is, wearing a modifier: two columns
            // rather than however many fit the viewport, a taller and wider line, and a step up
            // the type scale (owner, 2026-09-18). Capping the container is what holds it to two —
            // it is a wrapping row in a pane that grows, so at 1920 it had been laying thirteen
            // skills out four across and filling the page edge to edge.
            _detailSkills = new VisualElement();
            _detailSkills.AddToClassList("skills");
            _detailSkills.AddToClassList("skills--setup");
            _colonistDetail.Add(_detailSkills);
            for (int i = 0; i < SkillCatalogue.ReadingOrder.Count; i++)
                _detailSkillViews.Add(SkillLine(_detailSkills, "skill--setup",
                    HudTextRole.Row, HudTextRole.Body));

            _colonistDetail.Add(SectionHeading(SeedField.TraitsKey));
            _colonistDetail.Add(_detailTraits);

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
        /// <summary>
        /// Put the box over this card's name, with the name in it and selected.
        ///
        /// <para><b>The box goes on the end of the line stack and the label is hidden, rather than
        /// the box being put where the label was.</b> <see cref="RefreshColonists"/> reaches for
        /// <c>lines[0]</c> and <c>lines[1]</c> by position — a box inserted at the front would
        /// shift both, and the refresh would write the name into the trade and cast the box to a
        /// Label on the next frame.</para>
        ///
        /// <para>Selected, not just focused: the name already in the box is the one being replaced
        /// nine times in ten, and a player who has to clear it first has been given a chore rather
        /// than a cursor.</para>
        /// </summary>
        void BeginRename(int slot)
        {
            ColonistSelect? select = _menu.Colonists;
            if (select == null || slot < 0 || slot >= _colonistCards.childCount) return;
            if (_renameSlot == slot) return;

            EndRename();

            _renameSlot = slot;
            _renameWas = select.GivenName(slot);

            VisualElement lines = _colonistCards[slot][1];
            lines[0].style.display = DisplayStyle.None;
            lines.Add(_renameBox);

            _renameBox.SetValueWithoutNotify(select.DisplayName(slot));
            _renameBox.Focus();
            _renameBox.SelectAll();
        }

        /// <summary>
        /// Take the box away and show the name again. Nothing is committed here — every keystroke
        /// already was — so this is only the undoing of <see cref="BeginRename"/>, and it is safe
        /// to call when nobody is being renamed.
        /// </summary>
        void EndRename()
        {
            if (_renameSlot < 0) return;

            int slot = _renameSlot;
            _renameSlot = -1;
            _renameWas = null;

            _renameBox.RemoveFromHierarchy();
            if (slot < _colonistCards.childCount)
                _colonistCards[slot][1][0].style.display = DisplayStyle.Flex;

            RefreshColonists();
        }

        /// <summary>
        /// Escape: put back what this slot was called before the edit, and leave the box.
        ///
        /// <para>Runs after the blur, so <see cref="EndRename"/> has already cleared the slot —
        /// which is why the slot is read here before anything else touches it. Renaming to what
        /// it was is how the revert is done, rather than a separate undo path, because
        /// <c>ColonistSelect.Rename</c> is where the rule about what a name may be lives.</para>
        /// </summary>
        void RevertRename()
        {
            int slot = _renameSlot;
            string? was = _renameWas;
            if (slot < 0) return;

            _menu.Colonists?.Rename(slot, was);
        }

        void RefreshColonists()
        {
            ColonistSelect? select = _menu.Colonists;
            if (select == null) return;

            for (int slot = 0; slot < ColonistSelect.Slots && slot < _colonistCards.childCount; slot++)
            {
                VisualElement card = _colonistCards[slot];
                Candidate who = select.Cards[slot];
                VisualElement lines = card[1];

                // The display name, not the card's: a colonist the player has renamed is called
                // what the player called them everywhere on this page.
                HudText.Set((Label)lines[0], select.DisplayNameAndAge(slot), HudTextRole.Name);
                HudText.Set((Label)lines[1], who.Occupation, HudTextRole.Row);

                // The seed is the candidate's own and the id is the one this slot will occupy, so
                // this is the face the colony goes on to give them — ColonistDraw.IdForSlot is
                // called rather than slot + 1 written out, which is the rule that file exists to
                // state once.
                PawnId willBe = ColonistDraw.IdForSlot(slot);
                _colonistFaces[slot].SetFace(ColonistFace.Of(who.Seed, willBe));
                _colonistFaces[slot].SetPortrait(_boot!.Portraits.For(who.Seed, willBe));
                card.EnableInClassList("row--armed", select.IsLocked(slot));
                card.EnableInClassList("colonist--on", select.Selected == slot);
                string called = select.DisplayName(slot);
                card.tooltip = select.IsLocked(slot)
                    ? called + " is kept through a reroll"
                    : "Show " + called;
            }

            Candidate current = select.Current;
            HudText.Set(_detailName, select.DisplayNameAndAge(select.Selected), HudTextRole.Name);
            HudText.Set(_detailTrade, current.Occupation, HudTextRole.Body);
            PawnId shown = ColonistDraw.IdForSlot(select.Selected);
            _detailFace.SetFace(ColonistFace.Of(current.Seed, shown));
            _detailFace.SetPortrait(_boot!.Portraits.For(current.Seed, shown));

            // Empty until M7, by the owner's decision. It says "—" rather than nothing, because a
            // section that is absent and one that is empty look identical and only one of them is
            // a promise. The word "Traits" is the heading's now, not this line's.
            HudText.Set(_detailTraits, EmptySection, HudTextRole.Body);

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
        /// <summary>
        /// The picture behind the main menu (owner, 2026-09-17).
        ///
        /// <para>Scaled to cover rather than stretched: the image is 16:9 and the viewport need not
        /// be, and a starfield squashed to fit would be the one thing a starfield cannot survive.
        /// Cropping loses edge sky, which is what the edges of this one are.</para>
        ///
        /// <para>It never picks. The start screen's own scrim is the pickable full-viewport element
        /// that makes the menu modal (`17-start-flow.md` §4); a backdrop that also took the pointer
        /// would be a second answer to the same question.</para>
        /// </summary>
        void BuildBackdrop()
        {
            _backdrop = new VisualElement { name = "backdrop", pickingMode = PickingMode.Ignore };
            _backdrop.AddToClassList("backdrop");
            _backdrop.style.display = DisplayStyle.None;

            var picture = Resources.Load<Texture2D>(MenuBackdropResource);
            if (picture != null)
            {
                _backdrop.style.backgroundImage = new StyleBackground(picture);
                _backdrop.style.unityBackgroundScaleMode =
                    new StyleEnum<ScaleMode>(ScaleMode.ScaleAndCrop);
            }

            _hud.Add(_backdrop);
        }

        void OnSessionChanged()
        {
            HudDirectors? live = _boot!.Directors;

            // The in-game interface belongs to a colony and goes away with it (owner, 2026-09-17).
            // Before this it was drawn behind the main menu's scrim — dimmed rather than absent,
            // which read as the game being open behind a dialog it was not open behind.
            bool playing = live != null;
            _worldUi.style.display = playing ? DisplayStyle.Flex : DisplayStyle.None;
            _backdrop.style.display = playing ? DisplayStyle.None : DisplayStyle.Flex;

            // The toast stack belongs to a colony and goes away with it (SK4). Not for the rows —
            // those expire on their own — but for the levels the watch is holding: nothing steps
            // it while there is no world, so without this the next colony's PawnId 1 is measured
            // against the last one's and announces a level she arrived with.
            _toasts.Clear();

            // A menu raised in the last colony names its colonists and things (design 33 §7a).
            CloseContextMenu();

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

            // Settings opens over the dock now (design 40), rather than in its place as it did over
            // the centred card (owner, 2026-09-17): the dock is flush left and the window centred,
            // and the window's own scrim dims the dock under it.
            _startScreen.Show(true);

            bool root = _menu.Screen == MenuScreen.Root || _menu.Screen == MenuScreen.Settings;
            bool load = _menu.Screen == MenuScreen.Load;

            _startRows.style.display = root ? DisplayStyle.Flex : DisplayStyle.None;
            _startList.style.display = load ? DisplayStyle.Flex : DisplayStyle.None;

            // The way out of every screen but the root, which has nowhere to go back to.
            _startBack.style.display = root ? DisplayStyle.None : DisplayStyle.Flex;

            if (load) FillSaveList();

            // Coming back to the buttons from a screen puts focus on the button that opened it,
            // however it was opened — a click, the keyboard, or the director itself.
            if (_titlePrevScreen == MenuScreen.Settings) _titleReturnKey = SessionCommands.OptionsKey;
            else if (_titlePrevScreen == MenuScreen.Load) _titleReturnKey = SessionCommands.LoadKey;
            _titlePrevScreen = _menu.Screen;
            if (_menu.Screen == MenuScreen.Root) FocusTitleButton();
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
            // A note on a session row answers one press. Opening or closing the panel is a new
            // visit, and a row still saying "No saved colonies" from last time would be stating
            // it about a folder nobody has looked in since.
            if (_boot!.Preferences.Open != _notesTakenWhileOpen)
            {
                _notesTakenWhileOpen = _boot.Preferences.Open;
                ClearSessionNotes();
            }

            if (_menu.Screen == MenuScreen.Settings && !_boot.Preferences.Open) _menu.Back();
        }

        /// <summary>Whether the panel was open last time <see cref="OnPreferencesChanged"/> ran —
        /// the edge this shell needs and the preference bus does not publish.</summary>
        bool _notesTakenWhileOpen;

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
                empty.Add(HudText.Make(Registry.Label("ui.start.empty"), HudTextRole.Name,
                    ussClass: "save__name"));
                _startList.Add(empty);
                return;
            }

            for (int i = 0; i < _menu.Saves.Count; i++)
            {
                SaveRow save = _menu.Saves[i];

                var row = new VisualElement();
                row.AddToClassList("save");
                // The title at the name step, 19/600 (owner, 2026-09-24: "make the title label
                // for the saved game bigger").
                row.Add(HudText.Make(save.Name, HudTextRole.Name, ussClass: "save__name"));

                // The colony, the day and when it was written — the line that tells two saves
                // apart once the title is a name the player chose (owner, 2026-09-17). Three
                // labels rather than one string, so the day and the date stand in columns
                // whatever the colony is called: the date was one string's tail, and it moved
                // with the length of the name in front of it (owner, 2026-09-24: "date is
                // misaligned"). The colony is a word, so the reading face; the day and the date
                // are figures, so mono.
                var meta = new VisualElement { pickingMode = PickingMode.Ignore };
                meta.AddToClassList("save__line");
                if (save.Readable)
                {
                    meta.Add(HudText.Make(save.Colony, HudTextRole.Row, ussClass: "save__meta"));
                    meta.Add(HudText.Make("Day " + save.Day, HudTextRole.Row, numeric: true, "save__day"));
                    meta.Add(HudText.Make(save.When, HudTextRole.Row, numeric: true, "save__when"));
                }
                else
                {
                    meta.Add(HudText.Make(save.Problem, HudTextRole.Row, ussClass: "save__meta"));
                }
                row.Add(meta);

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

            // After the build, because a name belongs to a colonist and there were none until the
            // line above. The seeds go with the names so the bootstrap can check it is naming the
            // person the player was looking at rather than whoever landed in that slot.
            _boot.NameColonists(choice.Names, choice.Colonists);
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

            // The keyboard is this field's while it has the focus, and Escape closes the prompt
            // rather than merely leaving the box: over a modal, Escape means the modal.
            TakesTheKeyboard(_promptField, () => _prompt.Cancel());

            // Return is the Confirm row, not a second route to saving: `SavePrompt.Confirm` holds
            // the whole rule — an unusable name does nothing, and a name that collides arms rather
            // than overwrites — so the key presses the button rather than deciding anything.
            _promptField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                evt.StopPropagation();
                _prompt.Confirm();
            });

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

        readonly LeavePrompt _leave = new LeavePrompt();
        HudModal _leaveModal = null!;
        Label _leaveTitle = null!;
        Label _leaveNote = null!;
        Label _leaveSaveLabel = null!;
        VisualElement? _leaveCancel;
        VisualElement _leaveSave = null!;
        Label _leaveLabel = null!;

        /// <summary>Whether the leave prompt is up, for whoever owns the Escape key.</summary>
        public bool LeavePromptOpen => _leave.Showing;

        /// <summary>Take the leave prompt down. The Escape half, called by <c>SettingsPresenter</c>.</summary>
        public void CancelLeavePrompt() => _leave.Cancel();

        /// <summary>
        /// The confirmation asked before a colony is put down (2026-09-21): <b>save and leave,
        /// leave without saving, or stay</b>.
        ///
        /// <para>Three answers, so it is a prompt rather than the arm-twice row it replaces — a
        /// second press on a red row can only mean "yes", and the thing the owner asked for is the
        /// offer to save. The note under the title names the file saving would write to, because
        /// "Save and leave" is a promise about a file and a player is entitled to know which.</para>
        ///
        /// <para>The same modal chrome as the naming prompt, built after it so that if both were
        /// ever up the later one wins — they cannot both be up today, because leaving is refused
        /// while the naming prompt holds the screen.</para>
        /// </summary>
        void BuildLeavePrompt()
        {
            _leaveModal = Modal("leaveprompt", Registry.Label(LeavePrompt.ToMenuTitleKey),
                () => _leave.Cancel(), "prompt");

            // The header's own label, so the title can say which of the two leavings this is.
            _leaveTitle = _leaveModal.Panel.Q<Label>(className: "panel__label");
            // A question at the page title's step (design 39 §7), not a panel label in capitals.
            HudText.Apply(_leaveTitle, HudTextRole.Name);

            _leaveNote = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "prompt__note");
            _leaveModal.Panel.Add(_leaveNote);

            var answers = new VisualElement();
            answers.AddToClassList("prompt__answers");

            var saveAndLeave = new VisualElement();
            _leaveSave = saveAndLeave;
            saveAndLeave.AddToClassList("prompt__answer");
            saveAndLeave.AddToClassList("prompt__answer--save");
            _leaveSaveLabel = HudText.Make(Registry.Label(LeavePrompt.SaveAndLeaveKey), HudTextRole.Row);
            saveAndLeave.Add(_leaveSaveLabel);
            saveAndLeave.RegisterCallback<ClickEvent>(_ => _leave.Choose(save: true));
            answers.Add(saveAndLeave);

            var leave = new VisualElement();
            leave.AddToClassList("prompt__answer");
            leave.AddToClassList("prompt__answer--leave");
            _leaveLabel = HudText.Make(Registry.Label(LeavePrompt.LeaveKey), HudTextRole.Row);
            leave.Add(_leaveLabel);
            leave.RegisterCallback<ClickEvent>(_ => _leave.Choose(save: false));
            answers.Add(leave);

            var cancel = new VisualElement();
            cancel.AddToClassList("prompt__answer");
            cancel.AddToClassList("prompt__answer--cancel");
            cancel.Add(HudText.Make(Registry.Label(LeavePrompt.CancelKey), HudTextRole.Row));
            cancel.RegisterCallback<ClickEvent>(_ => _leave.Cancel());
            // Focus lands on the answer that loses nothing, and Enter presses it.
            cancel.focusable = true;
            cancel.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                _leave.Cancel();
                evt.StopPropagation();
            });
            _leaveCancel = cancel;
            answers.Add(cancel);

            _leaveModal.Panel.Add(answers);

            _leave.Changed += RefreshLeavePrompt;
            _leave.Confirmed += OnLeaveChosen;
        }

        /// <summary>
        /// Ask whether to save before putting this colony down. The settings panel goes first, for
        /// the reason the naming prompt closes it: a question asked through two stacked windows is
        /// a question about which window.
        /// </summary>
        void AskToLeave(LeaveTo to)
        {
            if (_boot?.World == null) return;

            _boot.Preferences.SetOpen(false);
            _leave.Ask(to, _boot.SuggestedSaveName());
        }

        void RefreshLeavePrompt()
        {
            _leaveModal.Show(_leave.Showing);
            if (!_leave.Showing) return;

            HudText.Set(_leaveTitle, Registry.Label(_leave.TitleKey) + "?", HudTextRole.Name);

            // From the title screen there is nothing to save (design 40): no save answer, no note,
            // and the answer that goes is named for where it goes.
            _leaveSave.style.display = _leave.HasColony ? DisplayStyle.Flex : DisplayStyle.None;
            _leaveNote.style.display = _leave.HasColony ? DisplayStyle.Flex : DisplayStyle.None;
            HudText.Set(_leaveLabel,
                Registry.Label(_leave.HasColony ? LeavePrompt.LeaveKey : LeavePrompt.ToDesktopTitleKey),
                HudTextRole.Row);
            _leaveModal.Panel.schedule.Execute(() => _leaveCancel?.Focus());
            if (!_leave.HasColony) return;

            HudText.Set(_leaveNote,
                "Unsaved progress since the last save will be lost. " +
                (_boot?.BoundSavePath == null
                    ? "This colony has never been saved. Saving writes a new file, " + _leave.Target
                    : "Saving writes over " + _leave.Target),
                HudTextRole.Meta);
            _leaveModal.Panel.schedule.Execute(() => _leaveCancel?.Focus());
        }

        /// <summary>
        /// The player answered. Write first if they asked for it, then go where they said.
        ///
        /// <para><b>The save is over the colony's own file</b>, the same rule the Save row and the
        /// autosave both keep; a colony that has never been saved is named here the way the first
        /// autosave names one, rather than opening a second prompt over the answer to the first.
        /// </para>
        /// </summary>
        void OnLeaveChosen(LeaveTo to, bool save)
        {
            if (_boot == null) return;

            if (save)
            {
                string? path = _boot.SaveSession() ?? _boot.SaveSessionAs(_boot.SuggestedSaveName());
                Debug.Log($"[Odyssey] saved to {path} on the way out");
            }

            if (to == LeaveTo.Desktop) { Quit(); return; }

            _boot.TeardownSession();
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

            // The prompt has gone, so the keyboard comes back whether or not the field was ever
            // blurred. Hiding an element does not reliably raise a focus event, and a gate left
            // shut is a game that has quietly stopped answering its own keys — the worse half of
            // the bug this gate was built to fix.
            if (!_prompt.Showing)
            {
                Hotkeys().EndTyping(_promptField);
                return;
            }

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
                {
                    // **Look before the colony is thrown away.** The three steps below are
                    // deliberate — the panel closes, the world goes, the list arrives — and until
                    // 2026-09-20 they ran without anybody having asked whether the list would have
                    // anything on it. With an empty Saves folder the player pressed Load, watched
                    // their colony disappear, and arrived at "No saved colonies" with no way back
                    // to it (owner, 2026-09-20: "you can end up losing your current game as it
                    // goes back to the main menu").
                    //
                    // Readable, not merely present: a save this build cannot open is listed but
                    // cannot be chosen (`MenuDirector.ChooseSave`), so a folder holding only those
                    // is a folder with nothing to load, and tearing down for it would lose the
                    // colony exactly as an empty folder did.
                    if (!AnySaveCanBeOpened())
                    {
                        NoteOnSessionRow(SessionCommands.LoadKey, Registry.Label("ui.start.empty"));
                        break;
                    }

                    _boot!.Preferences.SetOpen(false);
                    _boot.TeardownSession();
                    _menu.Choose(SessionCommands.LoadKey);
                    break;
                }

                case SessionCommands.QuitToMenuKey:
                    // It used to tear the world down on the second press of an armed row. The
                    // prompt is the ask now, and it offers to save first (owner, 2026-09-21).
                    AskToLeave(LeaveTo.MainMenu);
                    break;

                case SettingsDirector.ExitKey:
                    // Leaving the application while a colony is running is the same question with
                    // a further destination. SettingsPresenter still owns the quit itself and
                    // stands aside while there is a session, so this is the only place that asks.
                    AskToLeave(LeaveTo.Desktop);
                    break;
            }
        }

        /// <summary>
        /// Is there a colony on disk this build could actually open?
        ///
        /// <para>The same question <see cref="FillSaveList"/> answers with a row of text and
        /// <c>MenuDirector.ChooseSave</c> answers by refusing, asked before anything irreversible
        /// happens. It reads the folder, which is disk work, and it is done once on a press rather
        /// than every frame.</para>
        /// </summary>
        static bool AnySaveCanBeOpened()
        {
            foreach (SaveEntry entry in SaveFiles.List())
                if (entry.IsReadable) return true;
            return false;
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
