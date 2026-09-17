#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// <see cref="HudShell"/>: B18, the start screen (U38).
    ///
    /// <para>The screen before the game — the only one that exists when no world is built. It is
    /// the project's first true modal, and it is deliberately small: four rows drawn from
    /// <see cref="SessionCommands"/>, a list of saves read from their headers, and nothing that
    /// the rest of the interface does not already have. Design and the owner's decisions are
    /// <c>docs/design/17-start-flow.md</c>.</para>
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
        readonly MenuDirector _menu = new MenuDirector();

        /// <summary>
        /// The start screen's own director, which the shell owns rather than the session does —
        /// this screen outlives every colony, and exists exactly when none is running.
        /// </summary>
        public MenuDirector Menu => _menu;

        HudModal _startScreen = null!;
        VisualElement _startRows = null!;
        ScrollView _startList = null!;
        VisualElement _startBack = null!;
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

            _menu.ShowingChanged += RefreshStartScreen;
            _menu.ScreenChanged += _ => RefreshStartScreen();
            _menu.ArmedChanged += RefreshStartArming;
            _menu.NewGameRequested += OnNewGame;
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
            _startRows.style.display = root ? DisplayStyle.Flex : DisplayStyle.None;
            _startList.style.display = root ? DisplayStyle.None : DisplayStyle.Flex;
            _startBack.style.display = root ? DisplayStyle.None : DisplayStyle.Flex;

            if (!root) FillSaveList();
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
                row.Add(HudText.Make(save.Colony, HudTextRole.Row, ussClass: "save__name"));

                // Day, board and when it was written — the line that tells two saves of the same
                // colony apart, which is what a folder of them mostly contains. Asked for by the
                // owner after playing it (2026-09-17). A figure, so mono, like every other figure
                // on this screen.
                row.Add(HudText.Make(
                    save.Readable ? $"Day {save.Day} · {save.Map} · {save.When}" : save.Problem,
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
                        when: SaveFiles.WhenOf(entry))
                    : new SaveRow(entry.Path, SaveFiles.TitleOf(entry), 0, string.Empty,
                        entry.Problem ?? Registry.Label("ui.start.unreadable"),
                        when: SaveFiles.WhenOf(entry)));
            }

            _menu.ShowSaves(rows);
        }

        /// <summary>
        /// A new colony, on a drawn seed.
        ///
        /// <para>Everything else about the request stays a default on the bootstrap — size, map
        /// type and scenario — which is what the plan's U39 row asks for: the seed is the only
        /// knob exposed, so the rest stay tunable later without new interface. The screen where
        /// you read and reroll that seed is U39; <c>SeedEntry</c>, which draws it, landed early
        /// and is already tested.</para>
        /// </summary>
        void OnNewGame() => _boot!.BuildSession(SeedEntry.Draw(), null);

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
                    // Closed first, so the panel is not still open over a colony whose file has
                    // already been written — and so the player sees that something happened.
                    _boot!.Preferences.SetOpen(false);
                    Debug.Log($"[Odyssey] saved to {_boot.SaveSession()}");
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
