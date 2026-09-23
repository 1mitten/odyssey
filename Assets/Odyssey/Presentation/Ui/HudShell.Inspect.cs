#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Storage;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// <see cref="HudShell"/>: the depth rail and the inspect pane.
    ///
    /// <para>A11, the rail down the right, and A3, the pane above the command bar that answers
    /// the current selection. Split out of the shell on 2026-09-16 because the one file had
    /// reached 1,947 lines.</para>
    ///
    /// <para>The rail is the one region the <em>world</em> sizes, so it is the one that gives: its
    /// cells shrink in proportion rather than the rail overflowing, and above all rather than
    /// silently losing its last layers, which was a playtest report.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        // ============================================================ A11 depth rail

        /// <summary>
        /// The rail, into the right-hand gutter it shares with the orders strip.
        /// </summary>
        void BuildRail(VisualElement gutter)
        {
            VisualElement rail = Panel("rail", "rail");
            Header(rail, "Depth", out _);

            _railCells = new VisualElement();
            _railCells.AddToClassList("rail__cells");
            rail.Add(_railCells);

            _railHint = HudText.Make("R / F", HudTextRole.Meta, numeric: false, "rail__hint");
            _railHint.tooltip = "R and F move the slice up and down. Home recentres.";
            rail.Add(_railHint);

            gutter.Add(rail);
        }

        /// <summary>
        /// One cell per layer, the top cell the top layer, so the rail reads downwards like depth
        /// does. Above ground is pale and below ground is dark, which is the one thing a depth
        /// readout has to say at a glance; the live layer is filled and carries its own number.
        /// </summary>
        void RefreshRail()
        {
            if (_directors == null) return;
            var world = _boot!.World;
            if (world == null) return;
            _ruler.Refresh(world.Views.Current, _directors.Slice.ActiveLayer, _surfaceLayer);

            while (_rail.Count < _ruler.Rows.Count)
            {
                // Rows come out of the model top layer first and the rail is a column, so the
                // first cell built is the top cell and cell i is Rows[i]. This used to index from
                // the far end, which built the bar upside down: clicking low on the rail took you
                // high, reported from a playtest on 2026-09-16.
                int layer = _ruler.Rows[_rail.Count].Layer;

                var cell = new VisualElement();
                cell.AddToClassList("rail__cell");
                cell.AddToClassList("ruler__tick");   // the name the playmode gate knows it by

                Label number = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true, "rail__number");
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.AddToClassList("rail__dot");
                cell.Add(number);
                cell.Add(dot);

                int clicked = layer;
                cell.RegisterCallback<ClickEvent>(_ => _directors?.Slice.SetLayer(clicked));

                // What the cell will DO, hung on the element so a test can read it. Without it the
                // only observable is the lit cell, and the lit cell cannot tell two bugs apart:
                // build the rail upside down, read it with a mirrored index, and the highlight
                // lands correctly while the click still goes elsewhere.
                cell.userData = clicked;
                _railCells.Add(cell);
                _rail.Add(new RailCellView { Root = cell, Number = number, Dot = dot, Layer = layer });
                _railPitch = -1f;   // a rail that has grown has to be measured again
            }
            if (_rail.Count != _ruler.Rows.Count) return;
            FitRail();

            for (int i = 0; i < _rail.Count; i++)
            {
                RailCellView view = _rail[i];

                // By index, not by layer number. Rows[i].Layer is layers-1-i, so indexing the list
                // with a layer number reads a different row for every layer but the middle one.
                LayerRow model = _ruler.Rows[i];

                view.Root.EnableInClassList("rail__cell--active", model.Active);
                view.Root.EnableInClassList("rail__cell--above", model.Layer > _surfaceLayer);
                view.Root.EnableInClassList("rail__cell--surface", model.Surface);
                view.Root.EnableInClassList("ruler__tick--active", model.Active);

                HudText.Set(view.Number, model.Active ? model.Layer.ToString() : string.Empty,
                    HudTextRole.Meta);
                view.Dot.style.display =
                    model.Pawns > 0 && !model.Active ? DisplayStyle.Flex : DisplayStyle.None;

                // Rebuilt only when it would read differently. The occupancy is compared as the
                // whole percent the tooltip prints rather than as a float, so a figure drifting
                // in the fourth decimal does not rebuild a sentence once a second for ever.
                int occupancyPercent = model.Occupancy >= 0f ? Mathf.RoundToInt(model.Occupancy * 100f) : -1;
                if (view.LastPawns != model.Pawns || view.LastOccupancy != occupancyPercent)
                {
                    view.LastPawns = model.Pawns;
                    view.LastOccupancy = occupancyPercent;

                    string surface = model.Surface ? " (surface)" : string.Empty;
                    string occupancy = occupancyPercent >= 0
                        ? $"{occupancyPercent}% built"
                        : "occupancy publishes for the active slice only";
                    view.Root.tooltip =
                        $"Layer {model.Layer}{surface} — {model.Pawns} colonists, {occupancy}. Click to move the slice.";
                }
            }
        }

        // ============================================================ A9/A10 inspect

        void BuildInspect()
        {
            _inspectPanel = Panel("inspect", "inspect");
            _inspectBody = new VisualElement();
            _inspectBody.AddToClassList("inspect__body");
            _inspectPanel.Add(_inspectBody);

            // Hidden from the first frame. Nothing is selected when a game starts, and a pane that
            // appeared for one frame before the first refresh took it away would be the kind of
            // flicker nobody can reproduce on demand.
            _inspectPanel.style.display = DisplayStyle.None;
            _worldUi.Add(_inspectPanel);
        }

        void RefreshInspect()
        {
            var world = _boot!.World;
            if (world == null || _inspectBody == null) return;
            _inspect.Refresh(world.Views.Current);

            // The structure is rebuilt only when the subject changes; values update in place, so a
            // refresh allocates nothing but the few strings it shows. A cell's signature carries
            // its layer as well as its position, because "at 78, 59" names a column and a click
            // can reach several cells of it.
            string signature =
                _inspect.Subject + ":" +
                (_inspect.Subject == InspectSubject.Colonist ? _inspect.Pawn.ToString()
                 : _inspect.Subject == InspectSubject.Item ? _inspect.Thing.ToString()
                 : _inspect.Position + ":" + _inspect.Layer);
            if (signature != _inspectBuiltFor)
            {
                BuildInspectBody();
                _inspectBuiltFor = signature;
            }

            if (_inspect.Subject == InspectSubject.None) return;

            SyncStoragePanel();

            _inspectPanel.EnableInClassList("inspect--tomb", _inspect.Tombstoned);
            _tombReason.style.display = _inspect.Tombstoned ? DisplayStyle.Flex : DisplayStyle.None;

            HudText.Set(_inspectTitle, _inspect.Title, HudTextRole.Name);

            // The avatar follows the answer rather than the click: a tile whose face is mined
            // through, or a pile that changes hands, swaps its icon without a rebuild.
            bool colonist = _inspect.Subject == InspectSubject.Colonist;
            string avatarKey = _inspect.Subject == InspectSubject.Item ? _inspect.ItemIconKey
                : _inspect.Subject == InspectSubject.Cell ? _inspect.CellIconKey
                : "ui.pawn.colonist";
            if (avatarKey != _inspectAvatarKey)
            {
                _inspectAvatarKey = avatarKey;
                _inspectAvatar.SetKey(avatarKey);
            }

            // Whoever is in the slot, only one of the two is in it. The badge is not hidden for a
            // colonist and left keyed at ui.pawn.colonist — it is hidden and *unkeyed by the line
            // above*, so the one key in the registry that has never had art is no longer asked
            // for by anything on this screen.
            _inspectAvatar.style.display = colonist ? DisplayStyle.None : DisplayStyle.Flex;
            _inspectFace.style.display = colonist ? DisplayStyle.Flex : DisplayStyle.None;
            if (colonist && _boot?.World != null)
            {
                WorldSnapshot frame = _boot.World.Views.Current;
                _inspectFace.SetFace(ColonistFace.Of(frame, _inspect.Pawn));
                _inspectFace.SetPortrait(_boot.Portraits.For(frame, _inspect.Pawn));
            }

            // The two header lines are interpolated, and the pane refreshes fifteen times a
            // second, so they are rebuilt only when one of the values they quote has moved. The
            // cell is the thing that moves most; the mood band and the selection size change
            // rarely and the job hardly at all.
            int layer = _inspect.Layer;
            int selected = _directors != null ? _directors.Selection.Pawns.Count : 0;
            string band = _inspect.Subject == InspectSubject.Colonist
                ? MoodBands.Band(_inspect.Mood)
                : string.Empty;

            if (_metaLayer != layer || !ReferenceEquals(_metaPosition, _inspect.Position))
            {
                _metaLayer = layer;
                _metaPosition = _inspect.Position;
                HudText.Set(_inspectMeta, MetaLine(), HudTextRole.Meta);
                if (_locationValue != null) HudText.Set(_locationValue, Coordinates(), HudTextRole.Meta);
            }
            if (!ReferenceEquals(_stateJob, _inspect.Job) || !ReferenceEquals(_stateBand, band) ||
                _stateSelected != selected || !ReferenceEquals(_stateSite, _inspect.Site) ||
                _stateStack != _inspect.Stack)
            {
                _stateSite = _inspect.Site;
                _stateJob = _inspect.Job;
                _stateBand = band;
                _stateSelected = selected;
                _stateStack = _inspect.Stack;
                HudText.Set(_inspectState, StateLine(), HudTextRole.Meta);
            }

            if (_inspect.Subject == InspectSubject.Cell || _inspect.Subject == InspectSubject.Item)
                SyncCellRows();

            if (_inspect.Subject != InspectSubject.Colonist || _inspect.Tombstoned) return;

            SetNeed(0, _inspect.Food);
            SetNeed(1, _inspect.Rest);
            SetNeed(2, _inspect.Mood);

            for (int i = 0; i < _inspect.Skills.Count; i++)
            {
                SkillRow row = _inspect.Skills[i];
                SetSkill(i, row);
            }
        }

        /// <summary>
        /// Show the tab the model says is active, and mark its chip.
        ///
        /// <para>The bodies are built once and hidden, not built on demand: a tab that rebuilds
        /// its tree on every click churns thirteen rows of elements for a control the player
        /// flicks between, and the pane's own rule is that structure is rebuilt when the subject
        /// changes and never for a value.</para>
        /// </summary>
        void ShowActiveTab()
        {
            string active = _inspect.ActiveTabName;
            bool skills = active == "Skills";

            if (_needsGrid != null)
                _needsGrid.style.display = skills ? DisplayStyle.None : DisplayStyle.Flex;
            if (_skillsGrid != null)
                _skillsGrid.style.display = skills ? DisplayStyle.Flex : DisplayStyle.None;

            // A store's two tabs stand in the same box and one of them is drawn, exactly as the
            // colonist's needs and skills do — so changing tab changes which rows are shown and
            // nothing about the pane's size.
            bool tile = active == Registry.Label(InspectModel.TabTile);
            if (_storagePane != null)
                _storagePane.style.display = tile ? DisplayStyle.None : DisplayStyle.Flex;
            if (_inspect.IsStore && _cellRowsGrid != null)
                _cellRowsGrid.style.display = tile ? DisplayStyle.Flex : DisplayStyle.None;

            for (int i = 0; i < _storageTabUnderlines.Count && i < _inspect.Tabs.Count; i++)
            {
                bool live = i == _inspect.ActiveTab;
                _storageTabUnderlines[i].style.display = live ? DisplayStyle.Flex : DisplayStyle.None;
                if (i < _tabChips.Count)
                    _tabChips[i].style.color = live
                        ? HudTokens.Convert(HudTheme.Accent)
                        : new Color(1f, 1f, 1f, 0.55f);
            }

            for (int i = 0; i < _tabChips.Count && i < _inspect.Tabs.Count; i++)
            {
                bool on = _inspect.Tabs[i].Enabled && i == _inspect.ActiveTab;
                _tabChips[i].EnableInClassList("tab--on", on);
                _tabChips[i].EnableInClassList("tab--off", !_inspect.Tabs[i].Enabled);
            }
        }

        /// <summary>
        /// One line of the Skills tab. The passion mark is two lozenges rather than a word,
        /// because thirteen rows of "major"/"minor" is a column of text nobody reads and the
        /// thing the player wants is the shape of the list at a glance.
        /// </summary>
        /// <summary>
        /// One line of a skills grid.
        ///
        /// <para><paramref name="modifier"/> and the two roles are how the setup page gets a
        /// bigger, wider line out of the same builder (owner, 2026-09-18: *"make the fonts a bit
        /// bigger in general on the screen"*). Defaulted to what the inspect pane has always used,
        /// so the pane over a running world is untouched — a screen read at leisure and a pane
        /// glanced at over a colony are allowed to sit at different steps of one scale, and that is
        /// not a reason for two builders.</para>
        /// </summary>
        static SkillLineView SkillLine(VisualElement grid, string? modifier = null,
            HudTextRole nameRole = HudTextRole.Body, HudTextRole levelRole = HudTextRole.Meta,
            bool withBar = false)
        {
            var view = new SkillLineView();

            view.Root = new VisualElement();
            view.Root.AddToClassList("skill");
            if (!string.IsNullOrEmpty(modifier)) view.Root.AddToClassList(modifier);

            view.Icon = new IconBadge(string.Empty, IconBadge.RowSize);
            view.Icon.Inherit(HudTokens.TextMeta);
            view.NameRole = nameRole;
            view.LevelRole = levelRole;
            view.Name = HudText.Make(string.Empty, nameRole, ussClass: "skill__name");
            view.Value = HudText.Make(string.Empty, levelRole, numeric: true, "skill__level");

            view.Passion = new VisualElement();
            view.Passion.AddToClassList("skill__passion");
            for (int i = 0; i < 2; i++)
            {
                var pip = new VisualElement();
                pip.AddToClassList("skill__pip");
                view.Passion.Add(pip);
            }

            view.Root.Add(view.Icon);
            view.Root.Add(view.Name);

            // The experience bar (SK3), on the inspect pane only. The setup page's grid shows a
            // candidate who has not started working, so a part-filled bar there would be a
            // progress reading for progress nobody has made.
            //
            // It goes in HERE, between the name and the level, because that is where the owner
            // put it (2026-09-21: "the experience bar needs to sit between the skill label and
            // the skill value"). It was a bar drawn along the row's bottom edge and is a column
            // of the row now, so the order of these Add calls is the order on screen and is the
            // whole of the change. Hud.uss carries why that costs the row no height.
            if (withBar)
            {
                view.Track = new VisualElement();
                view.Track.AddToClassList("skill__track");

                view.Fill = new VisualElement();
                view.Fill.AddToClassList("skill__fill");
                view.Fill.style.backgroundColor = ExperienceInk;

                view.Track.Add(view.Fill);
                view.Root.Add(view.Track);
            }

            view.Root.Add(view.Value);
            view.Root.Add(view.Passion);

            grid.Add(view.Root);
            return view;
        }

        void SetSkill(int index, in SkillRow row) => SetSkillLine(_skills, index, row);

        /// <summary>
        /// Draw one line of a skills grid — <b>any</b> skills grid.
        ///
        /// <para>Taking the view list rather than reaching for <c>_skills</c> is what lets the
        /// setup page's candidate detail be the same control as this pane's Skills tab rather than
        /// a second one that looks like it. The two are read side by side by a player comparing a
        /// candidate with a colonist they already have, so a difference in ordering, greying or
        /// pip placement would read as a bug in the simulation rather than in a stylesheet.</para>
        /// </summary>
        static void SetSkillLine(List<SkillLineView> views, int index, in SkillRow row)
        {
            if (index >= views.Count) return;
            SkillLineView view = views[index];

            if (view.LastKey != row.IconKey)
            {
                view.LastKey = row.IconKey;
                view.Icon.SetKey(row.IconKey);
                HudText.Set(view.Name, row.Name, view.NameRole);
                view.Root.tooltip = row.Live
                    ? (row.Note.Length > 0 ? row.Name + " — " + row.Note : row.Name)
                    : row.Name + " — " + row.Reason;
            }

            if (view.LastLive != row.Live)
            {
                view.LastLive = row.Live;
                view.Root.EnableInClassList("skill--off", !row.Live);
                view.Icon.Inherit(row.Live ? HudTokens.TextMeta : HudTokens.TextFaint);

                // The bar belongs to the rows that have a simulation behind them, and the
                // stylesheet hides the track by default so a dead row needs nothing done to it —
                // which matters because this block does not run for a row that was born dead and
                // stayed dead, LastLive being false to begin with.
                if (view.Track != null)
                    view.Track.style.display = row.Live ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // A level moves once in a working day, so the string is built on the change and not
            // fifteen times a second for as long as somebody is selected.
            if (view.LastLevel != row.Level || !row.Live)
            {
                view.LastLevel = row.Level;
                HudText.Set(view.Value, row.Live ? row.Level.ToString("0") : "—", view.LevelRole);
            }

            if (view.LastPassion != row.Passion)
            {
                view.LastPassion = row.Passion;
                for (int i = 0; i < view.Passion.childCount; i++)
                    view.Passion[i].style.display =
                        row.Live && row.Passion > i ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // ---- the bar. Guarded on its own value like everything above it, but the guard is
            // doing a different job here and the difference is worth knowing before tuning it.
            //
            // Every other field on this row changes about once in a working day, so its guard is
            // almost always taken and the write almost never happens. This one moves roughly seven
            // per mille a second at a minor passion, so the guard is taken about half the time at a
            // 15 Hz refresh and the write is the normal case — which is the point, because watching
            // it creep is the whole reason it exists. Only the width is written; no string is
            // built, so a selected colonist still costs no allocation.
            if (view.Fill == null || !row.Live) return;
            if (view.LastProgress == row.Progress) return;
            view.LastProgress = row.Progress;

            // Per mille in, per cent out. A width rather than a scale so the bar's left edge stays
            // put and only its right edge moves.
            view.Fill.style.width = Length.Percent(row.Progress / 10f);
        }

        /// <summary>
        /// The experience bar's colour (SK3): the needs' own green, the one a food bar is drawn
        /// in. Owner, 2026-09-21: <i>"it should also be using the same green as used for the rest,
        /// food bars"</i>.
        ///
        /// <para><b>One colour, not a band.</b> <see cref="HudTokens.NeedBand"/> runs good to bad
        /// because a need has a bad end and a player has to be told about it. A skill has no bad
        /// end — a bar three tenths along is not a warning — so it takes the good end of that
        /// scale and stays there. Asking <c>NeedBand</c> for it would paint a new colonist's
        /// skills red for being new.</para>
        ///
        /// <para><b>It was tinted by passion until the owner's first look</b>, on the argument
        /// that the fill was the only place the ×0.35/×1.0/×1.5 learning rate was visible while it
        /// was happening. That argument was wrong about which question the bar answers: a player
        /// reading this row wants <i>how far along is she</i>, which is what a food bar answers
        /// and deserves the same colour. The passion is still on the row, in the pips, which is
        /// where it was always the more legible of the two.</para>
        ///
        /// <para>Taken from the token rather than written as a literal, so this row and a food bar
        /// cannot drift apart — and set once at build rather than per refresh, because unlike a
        /// need it never changes.</para>
        /// </summary>
        static Color ExperienceInk => HudTokens.Good;

        void SetNeed(int index, int thousandths)
        {
            if (index >= _needs.Count) return;
            NeedView view = _needs[index];

            float percent = Percent(thousandths);
            view.Fill.style.width = Length.Percent(percent);
            view.Fill.style.backgroundColor = HudTokens.NeedBand(thousandths);

            // A need moves by fractions of a per cent between refreshes, so the label is rebuilt
            // only when the whole number it prints has actually changed.
            int whole = Mathf.RoundToInt(percent);
            if (view.LastPercent == whole) return;
            view.LastPercent = whole;
            HudText.Set(view.Value, whole.ToString("0") + "%", HudTextRole.Meta);
        }

        /// <summary>The line beside the name: what it is, which layer.</summary>
        string MetaLine() =>
            // A store says what it is, how big it is and where — "zone · 24 cells · L11" — because
            // its name is an ordinal and the extent is the only other thing that tells two of them
            // apart until stores can be named.
            _inspect.IsStore ? StorageSettingsModel.Extent(_inspect.StoreCells, _inspect.Layer)
            : _inspect.Layer >= 0 ? $"{_inspect.Subtitle} · L{_inspect.Layer}"
            : _inspect.Subtitle;

        string Coordinates()
        {
            // InspectModel says "at 78, 59"; the spec's header says "78, 59". The words belong to
            // the model, so they are trimmed here rather than changed there.
            string position = _inspect.Position;
            return position.StartsWith("at ", StringComparison.Ordinal) ? position.Substring(3) : position;
        }

        /// <summary>The line under the name: what they are doing, and how they are.</summary>
        string StateLine()
        {
            switch (_inspect.Subject)
            {
                case InspectSubject.Colonist:
                    {
                        // A multi-selection shows the primary colonist in full, with the size of
                        // the set said out loud: "3 selected" is the whole of what a pane can add
                        // to several brackets until commands arrive (A10).
                        string count = _directors != null && _directors.Selection.HasMultiple
                            ? $"{_directors.Selection.Pawns.Count} selected · "
                            : string.Empty;
                        return count + $"{_inspect.Job} · mood {MoodBands.Band(_inspect.Mood)}";
                    }
                case InspectSubject.Item:
                    // The count used to be said here — "27 in the pile" — and it was missed
                    // (owner, 2026-09-19). It moved into the title, where the eye lands, and
                    // saying it twice would only teach the pane to be skimmed. So this line
                    // stopped counting and went back to saying where the thing is.
                    return _inspect.Stack > 1 ? "a pile on the ground" : "item on the ground";
                case InspectSubject.Cell:
                    // A site is the one thing a cell still says in a sentence — what it is
                    // waiting for, or how much longer. Every other fact the tile has is a row
                    // below in its own column, and the state line stays empty rather than
                    // repeating any of them.
                    return _inspect.Site;
                default:
                    return string.Empty;
            }
        }

        void BuildInspectBody()
        {
            _inspectBody.Clear();
            _needs.Clear();
            _skills.Clear();
            _tabChips.Clear();
            _cellRows.Clear();
            _needsGrid = null;
            _skillsGrid = null;
            _cellRowsGrid = null;
            _locationRow = null;
            _locationValue = null;
            _needRows = 0;

            // Nothing selected: no panel at all (owner, 2026-09-16), and this is the HUD's resting
            // state. It was a 41 px strip reading "Nothing selected", itself already a cut-down of
            // a three-sentence empty state; both were the interface talking about itself, and a
            // panel whose only content is the news that it has none earns less than the gap.
            // display:none rather than zero opacity, so it leaves the layout, leaves the measured
            // region set, and cannot take a click.
            if (_inspect.Subject == InspectSubject.None)
            {
                _inspectPanel.style.display = DisplayStyle.None;
                return;
            }

            _inspectPanel.style.display = DisplayStyle.Flex;

            // The tile readout and a selected pile take a column; a colonist takes a band. The
            // pane is the same panel either way — one class says which shape it is standing in
            // (owner, 2026-09-17: the tile window at half width, with its facts in rows).
            // A store is never narrow. 280 px is the bare-tile variant and the accepts list does not
            // fit in it; the settings belong to the zone, and a pane that shrank with the zone would
            // imply otherwise (design brief, 2026-09-21, state 8).
            _inspectPanel.EnableInClassList("inspect--narrow",
                !_inspect.IsStore
                && (_inspect.Subject == InspectSubject.Cell || _inspect.Subject == InspectSubject.Item));

            // ---- header: avatar, name and its two lines, then the actions on the right
            var header = new VisualElement();
            header.AddToClassList("inspect__hdr");

            // Two elements, one slot, and only ever one of them showing. A colonist gets their own
            // face (docs/design/20-avatars.md); a tile or a pile keeps the keyed badge, which is
            // the only kind of thing an icon key can describe. They are both built here rather
            // than swapped in on selection, because the header is rebuilt on a change of *shape*
            // and a colonist replacing a rock is not one.
            _inspectAvatarKey = _inspect.Subject == InspectSubject.Item ? _inspect.ItemIconKey
                : _inspect.Subject == InspectSubject.Cell ? _inspect.CellIconKey
                : "ui.pawn.colonist";
            _inspectAvatar = new IconBadge(_inspectAvatarKey, IconBadge.AvatarSize);
            _inspectAvatar.Inherit(HudTokens.TextPrimary);
            _inspectFace = new AvatarGlyph(HudLayout.Avatar);
            _inspectFace.AddToClassList("inspect__face");

            // A store gets a swatch rather than a badge: a hollow square in the zone's own colour,
            // which is the thing the board is tinted with. An icon would have to be the stockpile
            // key, and a placeholder square in the corner of the pane says less than a colour that
            // matches what the player is looking at.
            if (_inspect.IsStore)
            {
                var swatch = new VisualElement();
                swatch.style.width = 28;
                swatch.style.height = 28;
                swatch.style.flexShrink = 0;
                swatch.style.backgroundColor = new Color(0.30f, 0.44f, 0.50f, 0.35f);
                SetBorder(swatch, HudTokens.Convert(OrderColours.StoreHue), 1);
                header.Add(swatch);
            }
            else
            {
                header.Add(_inspectAvatar);
                header.Add(_inspectFace);
            }

            var titles = new VisualElement();
            titles.AddToClassList("inspect__titles");

            var nameLine = new VisualElement();
            nameLine.AddToClassList("inspect__nameline");
            _inspectTitle = HudText.Make(string.Empty, HudTextRole.Name, ussClass: "inspect__title");
            _inspectMeta = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "inspect__meta");
            nameLine.Add(_inspectTitle);
            nameLine.Add(_inspectMeta);

            _inspectState = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "inspect__state");
            titles.Add(nameLine);
            titles.Add(_inspectState);
            header.Add(titles);

            var actions = new VisualElement();
            actions.AddToClassList("inspect__actions");

            // A store used to add two buttons of its own here: a disabled Rename, drawn as a
            // placeholder square to hold a place for named stores, and a Close. Both are gone
            // (owner, 2026-09-21: "the x button appears twice in the control — keep the one in
            // the very top right, the square icon next to it does nothing"). Every pane already
            // ends in the same two, Almanac and Close, and a second Close six pixels from the
            // first is a choice the player has to make between two identical things. **An
            // affordance for something that does not exist yet is worse than a gap**: the square
            // said its story in a tooltip nobody hovers, and read as a broken button. Named
            // stores bring their own control when they bring the name (26-storage.md §9a, SZ4).

            if (_inspect.Subject == InspectSubject.Colonist)
                foreach (InspectCommand command in _inspect.Commands)
                {
                    // Two of the three: the pane's header carries the commands a player reaches
                    // for, and Inspect is not one of them when the pane is already open.
                    if (command.Label == "Inspect") continue;
                    actions.Add(ActionButton(command));
                }

            var info = new VisualElement();
            info.AddToClassList("inspect__info");
            info.Add(new HudGlyph(HudGlyphKind.Info, 14f, HudTokens.TextDim));
            info.tooltip = "Almanac entry";
            info.RegisterCallback<ClickEvent>(_ => OpenAlmanacForSelection());
            actions.Add(info);

            var close = new VisualElement();
            close.AddToClassList("inspect__close");
            close.Add(new HudGlyph(HudGlyphKind.Close, 14f, HudTokens.TextDim));
            close.tooltip = "Clear the selection";
            close.RegisterCallback<ClickEvent>(_ => _directors?.Selection.Clear());
            actions.Add(close);
            header.Add(actions);
            _inspectBody.Add(header);

            if (_inspect.Subject == InspectSubject.Colonist)
            {
                var strip = new VisualElement();
                strip.AddToClassList("inspect__tabs");
                for (int i = 0; i < _inspect.Tabs.Count; i++)
                {
                    InspectTab tab = _inspect.Tabs[i];
                    Label chip = HudText.Make(tab.Name, HudTextRole.Body, ussClass: "tab");
                    chip.tooltip = tab.Enabled ? tab.Name : tab.Name + " — " + tab.Reason;
                    if (tab.Enabled)
                    {
                        int index = i;
                        chip.RegisterCallback<PointerDownEvent>(_ =>
                        {
                            _inspect.ShowTab(index);
                            ShowActiveTab();
                        });
                    }
                    _tabChips.Add(chip);
                    strip.Add(chip);
                }
                _inspectBody.Add(strip);

                // Both tabs' grids stand in one box of a fixed height, so changing tab changes
                // which rows are drawn and nothing else. The pane grows upward from a docked
                // bottom edge, so without this its header and tab strip moved under the pointer
                // every time (owner, 2026-09-18). The height is HudLayout.InspectTabBody, which
                // the stylesheet also carries and HudStyleSheetTests holds to it.
                var tabBody = new VisualElement();
                tabBody.AddToClassList("inspect__tabbody");

                var grid = new VisualElement();
                grid.AddToClassList("needs");
                _needs.Add(Need(grid, "ui.need.food"));
                _needs.Add(Need(grid, "ui.need.rest"));
                _needs.Add(Need(grid, "ui.need.mood"));
                _needRows = (_needs.Count + 1) / 2;
                _needsGrid = grid;
                tabBody.Add(grid);

                _skillsGrid = new VisualElement();
                _skillsGrid.AddToClassList("skills");
                for (int i = 0; i < _inspect.Skills.Count; i++)
                    _skills.Add(SkillLine(_skillsGrid, withBar: true));
                tabBody.Add(_skillsGrid);

                _inspectBody.Add(tabBody);

                ShowActiveTab();
            }

            // ---- a store: the tab strip, then the settings and the tile's facts in one box
            if (_inspect.IsStore)
            {
                // The strip is built here rather than shared with the colonist's, because a store's
                // active tab is an underline and a colonist's is the `tab--on` pill the stylesheet
                // draws. Two surfaces, two answers, and the one that is laid out inline is the one
                // that cannot inherit a position it did not ask for.
                var strip = new VisualElement();
                strip.style.flexDirection = FlexDirection.Row;
                strip.style.paddingLeft = 14;
                strip.style.paddingRight = 14;
                strip.style.borderBottomWidth = 1;
                strip.style.borderBottomColor = new Color(1f, 1f, 1f, 0.20f);
                _storageTabUnderlines.Clear();

                for (int i = 0; i < _inspect.Tabs.Count; i++)
                {
                    InspectTab tab = _inspect.Tabs[i];
                    int index = i;

                    var column = new VisualElement();
                    column.style.marginRight = 22;

                    Label chip = HudText.Make(tab.Name, HudTextRole.Body);
                    chip.style.paddingTop = 8;
                    chip.style.paddingBottom = 7;
                    column.Add(chip);

                    // A 3 px rule under the live tab, not a filled pill: the pane is dark and a
                    // pill reads as a button that has been pressed rather than as a place you are.
                    var underline = new VisualElement();
                    underline.name = StoreTabUnderlineName;
                    underline.style.height = 3;
                    underline.style.backgroundColor = HudTokens.Convert(HudTheme.Accent);
                    column.Add(underline);

                    column.RegisterCallback<PointerDownEvent>(_ =>
                    {
                        _inspect.ShowTab(index);
                        ShowActiveTab();
                    });

                    _tabChips.Add(chip);
                    _storageTabUnderlines.Add(underline);
                    strip.Add(column);
                }

                _inspectBody.Add(strip);
                BuildStoragePane();

                // **The store's branch owed this and did not pay it**, while the colonist's
                // branch a few lines up has always called it. Nothing else establishes which tab
                // is live, so a freshly built strip drew *both* underlines — they are created
                // visible — and the pane's own display carried over from whatever the last
                // subject had left it on: select a store, look at its Tile tab, select another,
                // and the Storage tab came up blank until a tab was clicked. Reported by the
                // owner on 2026-09-21, as two separate faults that were one missing call.
                ShowActiveTab();
            }

            if (_inspect.Subject == InspectSubject.Cell || _inspect.Subject == InspectSubject.Item)
            {
                // The tile's facts, one row each. Rows are added by SyncCellRows as the answer
                // arrives and the facts change, so the pane never rebuilds its tree for a value.
                // Items too, since 2026-09-19: a pile lying in a field carries the field's
                // growing row, so the tile answers wherever on it the click lands.
                _cellRowsGrid = new VisualElement();
                _cellRowsGrid.AddToClassList("inspect__rows");

                _locationRow = new VisualElement();
                _locationRow.AddToClassList("inspect__row");
                _locationRow.Add(HudText.Make("location", HudTextRole.Meta, ussClass: "inspect__rowname"));
                _locationValue = HudText.Make(Coordinates(), HudTextRole.Meta, ussClass: "inspect__rowvalue");
                _locationRow.Add(_locationValue);
                _cellRowsGrid.Add(_locationRow);

                _inspectBody.Add(_cellRowsGrid);
            }

            _tombReason = HudText.Make("no longer present — the pane keeps last-known values",
                HudTextRole.Meta, ussClass: "inspect__reason");
            _tombReason.style.display = DisplayStyle.None;
            _inspectBody.Add(_tombReason);
        }

        /// <summary>
        /// Bring the readout rows to what the model holds: one element per fact, its label in the
        /// fixed column and its value beside it, written only when the words have moved.
        ///
        /// <para>Rows are added and removed rather than rebuilt — a tile held while a face is cut
        /// changes one number once a second, and the pane's own rule is that structure is built
        /// when the subject changes and never for a value.</para>
        /// </summary>
        void SyncCellRows()
        {
            if (_cellRowsGrid == null) return;

            if (_locationValue != null)
                HudText.Set(_locationValue, Coordinates(), HudTextRole.Meta);

            while (_cellRows.Count < _inspect.CellRows.Count)
            {
                var view = new CellRowView();

                view.Root = new VisualElement();
                view.Root.AddToClassList("inspect__row");

                view.Name = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "inspect__rowname");
                view.Value = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "inspect__rowvalue");
                // The value sits in a box with a glyph before it and a chevron after, and the box
                // is styled as a button on the one row that is pickable. Built for every row and
                // shown only where it means something, because rows are reused across facts and
                // the affordance has to travel with the row's current meaning rather than being
                // built into an element.
                //
                // Two rounds of this were too quiet to find (owner, 2026-09-17 and again after):
                // a pointer cursor, then a hover brighten and a border. A row that is a control
                // has to look like one with the pointer somewhere else entirely, which means an
                // icon and weight, not a treatment that only appears once you are already on it.
                view.PickBox = new VisualElement();
                view.PickBox.AddToClassList("inspect__rowbox");

                view.Glyph = new HudGlyph(HudGlyphKind.ToolBunk, 13f, HudTokens.Accent);
                view.Glyph.AddToClassList("inspect__rowglyph");
                view.Glyph.style.display = DisplayStyle.None;

                view.Chevron = HudText.Make("›", HudTextRole.Meta, ussClass: "inspect__rowchevron");
                view.Chevron.style.display = DisplayStyle.None;

                view.PickBox.Add(view.Glyph);
                view.PickBox.Add(view.Value);
                view.PickBox.Add(view.Chevron);

                view.Root.Add(view.Name);
                view.Root.Add(view.PickBox);

                // Registered once for every row and armed per tile by IsPick below: the pane's
                // rows are reused across facts, so the one row that does something is the one
                // whose fact it is holding, decided on the model's word rather than the element's.
                CellRowView captured = view;
                view.Root.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!captured.IsPick) return;
                    if (captured.IsSwitch) ThrowPowerSwitch();
                    else if (captured.IsOrderAction) ActOnOrder();
                    else ToggleBedPicker(captured.Root);
                });

                _cellRowsGrid.Add(view.Root);
                _cellRows.Add(view);
            }
            while (_cellRows.Count > _inspect.CellRows.Count)
            {
                _cellRowsGrid.Remove(_cellRows[_cellRows.Count - 1].Root);
                _cellRows.RemoveAt(_cellRows.Count - 1);
            }

            for (int i = 0; i < _cellRows.Count; i++)
            {
                CellRowView view = _cellRows[i];
                InspectRow row = _inspect.CellRows[i];
                if (view.LastName != row.Name)
                {
                    view.LastName = row.Name;
                    HudText.Set(view.Name, row.Name, HudTextRole.Meta);
                }

                // The owner row is pickable exactly while the tile says it is a bed's (the model
                // clears the flag every refresh, so the affordance cannot outlive the bed).
                // The storage row is a fact again, not a control: the settings are a tab of their
                // own now, so a row that opened a popover would be a second way in to the same
                // thing and the one a player found by accident.
                bool switchPick = row.Name == InspectModel.PowerSwitchRow && _inspect.PowerSwitchUnderPane;
                bool linePick = row.Name == InspectModel.OrderActionRow && _inspect.OrderActionUnderPane;
                bool pick = (row.Name == "owner" && _inspect.BedUnderPane) || switchPick || linePick;
                // The pickable row's value is set in the heavier Row role, which is where weight
                // lives: the stylesheet may not set type (TheSheetSetsNoTypeAtAll), so "make the
                // assign button bolder" is a role here rather than a font-style there.
                HudTextRole role = pick ? HudTextRole.Row : HudTextRole.Meta;
                if (view.LastValue != row.Value || view.LastRole != role)
                {
                    view.LastValue = row.Value;
                    view.LastRole = role;
                    HudText.Set(view.Value, row.Value, role);
                    // HudText.Set writes the role's own colour, so a tint that was applied before
                    // has just been overwritten and has to be laid on again.
                    view.LastTint = null;
                }

                // The value's colour, where the fact carries one — a quality tier, and nothing
                // else so far. Null means the row keeps the colour the stylesheet gives it, which
                // is what "Normal: no change" asks for, so the style is cleared rather than set to
                // a colour of our own.
                if (view.LastTint?.Hex != row.Tint?.Hex)
                {
                    view.LastTint = row.Tint;
                    if (row.Tint is HudColour tint) view.Value.style.color = HudTokens.Convert(tint);
                    else view.Value.style.color = StyleKeyword.Null;
                }

                if (view.IsPick != pick || view.IsSwitch != switchPick || view.IsOrderAction != linePick)
                {
                    view.IsPick = pick;
                    view.IsSwitch = switchPick;
                    view.IsOrderAction = linePick;
                    view.Root.EnableInClassList("inspect__row--pick", pick);
                    view.Chevron.style.display = pick ? DisplayStyle.Flex : DisplayStyle.None;
                    // The bed's glyph is a bed: the switch and line rows wear the chevron alone.
                    view.Glyph.style.display = pick && !switchPick && !linePick ? DisplayStyle.Flex : DisplayStyle.None;
                    view.Root.tooltip = switchPick ? "Switch it on or off — at once, nobody is sent"
                        : linePick ? row.Value + " — at once, nobody is sent"
                        : pick ? "Choose whose bed this is" : null;
                }
            }
        }

        /// <summary>
        /// The order under the pane's own action (design 32 §14): cancel a building order, cancel a
        /// line's order or its removal mark, or mark a laid line to come up. An intent like every
        /// command, applied while paused.
        /// </summary>
        void ActOnOrder()
        {
            _boot?.World?.Intents.Submit(new Intent(_inspect.OrderAction, _inspect.Cell, _inspect.OrderActionA));
        }

        /// <summary>
        /// Throw the switch of the power building under the pane (design 32 §5): an intent, like
        /// every command, applied while paused and at once — no colonist walks over to do it.
        /// </summary>
        void ThrowPowerSwitch()
        {
            _boot?.World?.Intents.Submit(new Intent(IntentKind.SetPowerSwitch, _inspect.Cell,
                _inspect.PowerSwitchOn ? 0 : 1));
        }

        // ---- the bed's owner picker: the pane's first interactive fact ------------------------

        VisualElement? _bedPicker;
        VisualElement? _bedPickerRows;

        /// <summary>The row the picker was raised by, so a re-place after layout knows where to go.</summary>
        VisualElement? _bedPickerAnchor;

        /// <summary>
        /// Raise the colonist picker over the owner row, or put it down if it is already up.
        ///
        /// <para>Built once, filled on every open: who is alive to be given a bed changes more
        /// often than the popover is built and less often than it is opened, and a picker that
        /// listed a dead colonist is the kind of stale a rebuild-on-open cannot be.</para>
        ///
        /// <para><b>The pick is an intent, not a write.</b> The pane never mutates the world; it
        /// names the cell and the colonist and the simulation decides, at a tick boundary, the
        /// way every command in the game does — so an assignment made while the clock is paused
        /// lands on unpause, exactly as a build order does.</para>
        /// </summary>
        void ToggleBedPicker(VisualElement anchor)
        {
            var world = _boot?.World;
            if (world == null) return;

            if (_bedPicker == null)
            {
                _bedPicker = Popover("bedowner", "Give this bed to", CloseBedPicker, "bedowner");
                _bedPickerRows = new VisualElement();
                _bedPickerRows.AddToClassList("bedowner__rows");
                _bedPicker.Add(_bedPickerRows);
                _hud.Add(_bedPicker);

                // Placed again whenever its size changes, which is the only moment its height is
                // knowable: a popover shown this frame has not been laid out, so the placement
                // arithmetic has nothing to work with and the first attempt does nothing.
                _bedPicker.RegisterCallback<GeometryChangedEvent>(_ => PlaceBedPicker());
            }

            if (_bedPicker.style.display == DisplayStyle.Flex)
            {
                CloseBedPicker();
                return;
            }

            _bedPickerRows!.Clear();
            _bedPickerRows.Add(BedPickerRow("No owner", -1, BedPickerMark.None));
            var frame = world.Views.Current;
            var pawns = frame.Pawns;

            // Who sleeps where, asked of the one place that knows. The picker used to be a list
            // of bare names, and the owner had to remember who he had already housed to use it
            // (2026-09-19: "the sub menu should be clear who is already assigned a bed and who is
            // unassigned"). The grid is the single owner of the edifice list, so it is asked
            // rather than a second tally being kept here and going stale.
            var sites = _boot?.Colony?.Construction;
            int here = sites != null ? sites.BedOwnerAt(_inspect.Cell) : 0;

            for (int i = 0; i < pawns.Length; i++)
            {
                int id = pawns[i].Id.Value;
                BedPickerMark mark =
                    id == here && here != 0 ? BedPickerMark.ThisBed
                    : sites != null && sites.PawnOwnsABed(id) ? BedPickerMark.AnotherBed
                    : BedPickerMark.None;
                _bedPickerRows.Add(BedPickerRow(ColonistNames.Of(frame, pawns[i].Id), id, mark));
            }

            _bedPickerAnchor = anchor;
            _bedPicker.style.display = DisplayStyle.Flex;
            PlaceBedPicker();
        }

        /// <summary>
        /// Put the picker against the row that raised it, as far as the current layout allows.
        ///
        /// <para>Called on open and again on every <c>GeometryChangedEvent</c>, because on the
        /// frame it opens the popover has no height and the placement cannot be computed at all.
        /// It is safe to call repeatedly: the arithmetic is a pure function of the two rects, so
        /// once the size settles the answer stops changing and the event stops firing.</para>
        /// </summary>
        void PlaceBedPicker()
        {
            if (_bedPicker == null || _bedPickerAnchor == null) return;
            if (_bedPicker.style.display.value != DisplayStyle.Flex) return;
            PlacePopover(_bedPicker, _bedPickerAnchor, onTheBar: false);
        }

        void CloseBedPicker()
        {
            if (_bedPicker != null) _bedPicker.style.display = DisplayStyle.None;
        }

        // ---- the store's settings: what goes in, and how much it matters ----------------------
        //
        // **Laid out inline, borrowing no class from anywhere else in the pane**, and that is a
        // correction rather than a style. The first version reused `bedowner__row` from the
        // colonist picker and `inspect__rowname` / `inspect__rowvalue` from the tile readout, and
        // those two carry a two-column geometry of their own — a fixed-width name against a
        // right-aligned value. Put a section header, two text buttons and a five-button ladder
        // through them and the text lands on top of itself (owner, 2026-09-21: "the text was
        // overlapping and all over the place in game"). Nothing here inherits a position it did
        // not ask for.

        readonly List<VisualElement> _storageTabUnderlines = new List<VisualElement>();
        VisualElement? _storagePane;

        /// <summary>
        /// Element names the store pane's own tests find it by.
        ///
        /// <para>Named rather than reached through the shell's fields, because the alternative is
        /// making a dozen private elements internal for one test. These three are the ones whose
        /// <em>visibility</em> is the assertion — which is a thing neither tier could see before
        /// and which the owner has now had to report twice.</para>
        /// </summary>
        public const string StoreTabUnderlineName = "store-tab-underline";
        public const string StoreHoldingName = "store-holding";
        public const string StoreHoldingRowName = "store-holding-row";

        /// <summary>The Holding group: its header summary, its rows, and the pool they come from.</summary>
        VisualElement? _storeHoldingGroup;
        VisualElement? _storeHoldingList;
        Label? _storeHoldingSummary;
        readonly List<VisualElement> _storeHoldingRows = new List<VisualElement>();

        /// <summary>The contents signature the Holding rows on screen were built from.</summary>
        int _storeHoldingFilledFor = int.MinValue;
        VisualElement? _storageRows;
        ScrollView? _storageList;
        VisualElement? _storageWarning;
        Label? _storageWarningLead;
        Label? _storageWarningHint;
        VisualElement? _storageRungs;
        TextField? _storageSearch;
        Label? _storageRungName;
        Label? _storageStatus;
        Label? _storageAllow;
        Label? _storageClear;
        readonly StorageSettingsModel _storageSettings = new StorageSettingsModel();

        const int StorageRowHeight = 32;
        const int StorageCommodityRowHeight = 28;
        const int StorageBox = 16;
        const int StorageCaretColumn = 12;
        const int StorageGlyph = 18;
        const int StorageIndent = 38;
        const int StorageListHeight = 320;

        /// <summary>
        /// The height the warning band takes, and the height the list gives up to make room for
        /// it, so that <b>the pane is the same height whether the band is there or not</b>.
        ///
        /// <para>This is the whole of why "put the message below the control" was not enough on
        /// its own. The inspect panel is anchored to the <em>bottom</em> of the screen and grows
        /// upward, so anything added at the end of it pushes everything above it up the screen —
        /// measured at 90 px on the first attempt, which is worse than the fault being fixed,
        /// because the rows moved further and in the less expected direction. Taking the band's
        /// height out of the scrolling list instead keeps the pane's total height fixed: the
        /// list's top edge does not move, the rows inside it do not move, and the only thing that
        /// changes is how much of the list you can see at once — which is what a scroll view is
        /// for.</para>
        ///
        /// <para>A constant rather than a measured height, because a height that depends on how
        /// the sentence wraps is a height that moves the rows when somebody rewords the sentence.
        /// <c>ZoneInspectTests.ClearingEveryCategoryDoesNotMoveTheRowsThatDidIt</c> asserts that
        /// the band's contents actually fit inside it, so the constant cannot quietly become a
        /// clip.</para>
        /// </summary>
        /// <para><b>92, measured rather than reckoned.</b> The two sentences and the rule above
        /// them come to 77.1 px at the pane's width, and the band's own padding is another 12.
        /// A guess of 64 clipped the hint, and the test below is what said so.</para>
        const int StorageWarningHeight = 92;

        // Named so that a PlayMode test can find the three parts whose *order* is the rule: the
        // list must sit at a fixed offset from the top of the pane, and the warning must come
        // after it. Nothing else can check that — the fast tier has no visual tree and the
        // model does not know where anything is drawn.
        public const string StoragePaneClass = "storage__pane";
        public const string StorageListClass = "storage__list";
        public const string StorageWarningClass = "storage__warning";
        public const string StorageCategoryClass = "storage__category";
        public const string StorageAllowAllClass = "storage__allowall";
        public const string StorageClearAllClass = "storage__clearall";

        /// <summary>
        /// The store's settings, as a tab of the inspect pane: a priority ladder, the two header
        /// buttons, the preset chips, a search that appears when it has something to do, and the
        /// list itself — which is the only part that scrolls.
        ///
        /// <para><b>Every press is an intent about a cell.</b> The pane never writes to the world:
        /// it names the cell it is describing and the simulation resolves it to whatever store
        /// covers it — which is what lets the same control drive a crate the day crates exist, and
        /// what makes a filter changed while the clock is paused land at once rather than on
        /// unpause.</para>
        /// </summary>
        void BuildStoragePane()
        {
            _storagePane = new VisualElement();
            _storagePane.AddToClassList(StoragePaneClass);
            _storagePane.style.flexDirection = FlexDirection.Column;

            // ---- holding: what is actually in there, which is the question a player clicking a
            // store is most often asking. It leads the tab because the filter below it answers
            // "what will it take", which is set once, while this changes all day.
            _storeHoldingGroup = new VisualElement();
            _storeHoldingGroup.name = StoreHoldingName;
            _storeHoldingGroup.style.flexDirection = FlexDirection.Column;

            VisualElement holdingHeader = StorageHeaderRow();
            holdingHeader.Add(StorageSectionLabel("Holding"));
            _storeHoldingSummary = HudText.Make(string.Empty, HudTextRole.Body);
            _storeHoldingSummary.style.unityTextAlign = TextAnchor.MiddleRight;
            _storeHoldingSummary.style.flexGrow = 1;
            holdingHeader.Add(_storeHoldingSummary);
            _storeHoldingGroup.Add(holdingHeader);

            _storeHoldingList = new VisualElement();
            _storeHoldingList.style.flexDirection = FlexDirection.Column;
            _storeHoldingList.style.paddingLeft = 14;
            _storeHoldingList.style.paddingRight = 14;
            _storeHoldingList.style.paddingBottom = 10;
            _storeHoldingGroup.Add(_storeHoldingList);

            _storeHoldingGroup.Add(StorageDivider(0.14f));
            _storagePane.Add(_storeHoldingGroup);

            // **The pool belongs to the tree that has just been thrown away.** Every element
            // above is new, so the rows remembered from the last build are orphans with no parent
            // — and the fill loop below would dutifully write text into them while the list on
            // screen stayed empty. The signature goes with them, or the first sync decides the
            // rows are already right and returns without adding any. This is the same fault as
            // the tab underlines above, which is why both lists near them are cleared on build.
            _storeHoldingRows.Clear();
            _storeHoldingFilledFor = int.MinValue;

            // ---- priority: the section label, and the rung it is on, on one line
            VisualElement priorityHeader = StorageHeaderRow();
            priorityHeader.Add(StorageSectionLabel("Priority"));
            _storageRungName = HudText.Make(string.Empty, HudTextRole.Body);
            _storageRungName.style.unityTextAlign = TextAnchor.MiddleRight;
            _storageRungName.style.flexGrow = 1;
            priorityHeader.Add(_storageRungName);
            _storagePane.Add(priorityHeader);

            // Five buttons across, not five rows down: the ladder is one thing with five places on
            // it, and a column of rows reads as five separate switches.
            _storageRungs = new VisualElement();
            _storageRungs.style.flexDirection = FlexDirection.Row;
            _storageRungs.style.paddingLeft = 14;
            _storageRungs.style.paddingRight = 14;
            _storageRungs.style.paddingBottom = 10;
            _storagePane.Add(_storageRungs);

            _storagePane.Add(StorageDivider(0.14f));

            // ---- accepts: the section label, and the two buttons that act on the whole list
            VisualElement acceptsHeader = StorageHeaderRow();
            acceptsHeader.Add(StorageSectionLabel("Accepts"));

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.flexGrow = 1;
            buttons.style.justifyContent = Justify.FlexEnd;

            _storageAllow = StorageTextButton("Allow all", () =>
            {
                if (_storageSettings.PressAllowAll(out var c)) SendStorageCommand(IntentKind.SetStorageFilter, c);
            });
            _storageClear = StorageTextButton("Clear all", () =>
            {
                if (_storageSettings.PressClearAll(out var c)) SendStorageCommand(IntentKind.SetStorageFilter, c);
            });
            _storageAllow.AddToClassList(StorageAllowAllClass);
            _storageClear.AddToClassList(StorageClearAllClass);
            _storageClear.style.marginLeft = 14;
            buttons.Add(_storageAllow);
            buttons.Add(_storageClear);
            acceptsHeader.Add(buttons);
            _storagePane.Add(acceptsHeader);

            // ---- the line that says how much of the list is showing
            //
            // **The Everything and Nothing chips that used to sit on this line are gone** (owner,
            // 2026-09-21: "nothing and everything is the same as allow all and clear all — so
            // remove nothing and everything"). They called the same two model methods as the
            // header buttons — the code said so in a comment and kept both anyway — so the pane
            // offered one action under two names, in two shapes, eight pixels apart. §9b Q1 had
            // already decided this and the build did not follow it. `StoragePreset` and
            // `StorageSettingsModel.PresetKeys` stay: a zone is still *founded* at Everything and
            // the sim still names the presets; nothing draws them.
            var statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;
            statusRow.style.paddingLeft = 14;
            statusRow.style.paddingRight = 14;
            statusRow.style.paddingBottom = 8;

            _storageStatus = HudText.Make(string.Empty, HudTextRole.Meta);
            _storageStatus.style.flexGrow = 1;
            statusRow.Add(_storageStatus);
            _storagePane.Add(statusRow);

            // The search field exists only above the threshold, and the model decides that from
            // the data — so it turns itself on as commodities land, with no toggle to find.
            _storageSearch = new TextField();
            _storageSearch.style.marginLeft = 14;
            _storageSearch.style.marginRight = 14;
            _storageSearch.style.marginBottom = 8;
            _storageSearch.style.height = 30;
            _storageSearch.RegisterValueChangedCallback(e =>
            {
                if (_storageSettings.SetSearch(e.newValue)) FillStoragePanel();
            });
            _storagePane.Add(_storageSearch);

            // Only the list scrolls. Priority, the buttons, the status line and the search stay
            // put — and the list itself keeps its height, so nothing above it can move.
            _storageList = new ScrollView(ScrollViewMode.Vertical);
            _storageList.AddToClassList(StorageListClass);
            _storageList.style.height = StorageListHeight;
            _storageList.style.flexShrink = 0;
            _storageRows = _storageList.contentContainer;
            _storagePane.Add(_storageList);

            // **The warning lives under the whole control, not at the top of the list** (owner,
            // 2026-09-21: "a message appeared about colonists ignoring the zone — but this moved
            // the controls/components — these should stay fixed — make the error message appear
            // below the stockpile component").
            //
            // It used to be two notes pushed in as the first children of the scroll content, so
            // the moment the last category was unticked every row below jumped down by the height
            // of the band — under a cursor that was in the middle of clicking them. A message
            // about what you just did must never move what you did it to. Below the list it can
            // appear and vanish freely: everything above it is at a fixed offset from the top of
            // the pane, and the only thing that changes is how far the pane reaches down.
            _storageWarning = new VisualElement();
            _storageWarning.AddToClassList(StorageWarningClass);
            _storageWarning.style.flexDirection = FlexDirection.Column;
            _storageWarning.style.flexShrink = 0;
            _storageWarning.style.height = StorageWarningHeight;
            _storageWarning.style.overflow = Overflow.Hidden;
            _storageWarning.style.paddingTop = 8;
            _storageWarning.style.paddingBottom = 4;
            _storageWarning.style.display = DisplayStyle.None;
            _storageWarning.Add(StorageDivider(0.14f));
            _storageWarningLead = StorageNoteLabel(HudTheme.Warn);
            _storageWarningHint = StorageNoteLabel(HudTheme.TextMeta);
            _storageWarning.Add(_storageWarningLead.parent);
            _storageWarning.Add(_storageWarningHint.parent);
            _storagePane.Add(_storageWarning);

            _inspectBody.Add(_storagePane);
            FillStoragePanel();
        }

        /// <summary>A section header: 13 px of padding, and whatever the caller puts on the line.</summary>
        static VisualElement StorageHeaderRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 14;
            row.style.paddingRight = 14;
            row.style.paddingTop = 13;
            row.style.paddingBottom = 9;
            return row;
        }

        static Label StorageSectionLabel(string text)
        {
            Label label = HudText.Make(text.ToUpperInvariant(), HudTextRole.Meta);
            label.style.letterSpacing = 2;
            return label;
        }

        static VisualElement StorageDivider(float alpha)
        {
            var line = new VisualElement();
            line.style.height = 1;
            line.style.flexShrink = 0;
            line.style.backgroundColor = new Color(1f, 1f, 1f, alpha);
            return line;
        }

        Label StorageTextButton(string text, Action press)
        {
            Label label = HudText.Make(text, HudTextRole.Body);
            label.style.color = HudTokens.Convert(HudTheme.Accent);
            label.RegisterCallback<ClickEvent>(_ => press());
            return label;
        }

        /// <summary>
        /// Rebuild the store's rows when the store has actually changed, and not otherwise.
        ///
        /// <para><b>The pane used to refresh only when it was the thing that changed the store</b>
        /// — <see cref="SendStorageCommand"/> submits an intent and then refills on the spot, on
        /// the strength of a comment saying a storage intent "applies while paused, so the answer
        /// is already true by the time the next frame draws". It is true while paused and false
        /// the rest of the time: unpaused, the intent queues for the next tick and the synchronous
        /// refill reads the state the player has just changed *away* from. Measured on
        /// 2026-09-21 by asking both sides after a press of Clear all with the game running: the
        /// simulation accepted 0 of 7 commodities and the pane was still showing all seven ticked
        /// and no warning. Every press was one action stale, and a press that made no visible
        /// difference is indistinguishable from a button that does not work.</para>
        ///
        /// <para><b>A signature rather than an unconditional refill</b>, exactly as the rest of
        /// this refresh works: the pane ticks fifteen times a second and rebuilding thirteen
        /// elements each time is thirteen elements of garbage a frame for a panel that changes
        /// when a person presses something. The signature is what the rows are drawn from — the
        /// zone, its rung, and its filter — so a change the pane cannot see cannot exist.</para>
        /// </summary>
        void SyncStoragePanel()
        {
            if (_storagePane == null || !_inspect.IsStore) return;
            SyncStoreHolding();
            if (!TryStoreUnderPane(out _, out _, out _, out int signature)) return;
            if (signature != _storageFilledFor) FillStoragePanel();
        }

        /// <summary>
        /// Show what the store is holding, rebuilding the rows only when they have changed.
        ///
        /// <para><b>A signature of its own, not the filter's.</b> The filter changes when a person
        /// presses something; the contents change whenever a hauler arrives, which the filter's
        /// signature cannot see. Sharing one would have left the list frozen at whatever was in
        /// there when the store was selected — the same class of fault as the panel that was one
        /// action stale, and just as invisible.</para>
        ///
        /// <para>Hidden outright over a painted zone: a stockpile's contents are lying on the
        /// board in front of you, and a list of them would be a second, worse view of something
        /// already on screen.</para>
        /// </summary>
        void SyncStoreHolding()
        {
            if (_storeHoldingGroup == null || _storeHoldingList == null) return;

            _storeHoldingGroup.style.display =
                _inspect.IsBuiltStore ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_inspect.IsBuiltStore) return;

            int signature = _inspect.StoreContentsSignature;
            if (signature == _storeHoldingFilledFor) return;
            _storeHoldingFilledFor = signature;

            if (_storeHoldingSummary != null) _storeHoldingSummary.text = _inspect.StoreSummary;

            // One row per kind, plus one line saying so when there are none. Pooled, because this
            // is a panel that ticks fifteen times a second and a store being loaded is a stream of
            // small changes rather than one.
            int wanted = Math.Max(1, _inspect.StoreContents.Count);
            while (_storeHoldingRows.Count < wanted)
            {
                var row = new VisualElement();
                row.name = StoreHoldingRowName;
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingTop = 2;
                row.style.paddingBottom = 2;

                Label rowName = HudText.Make(string.Empty, HudTextRole.Body);
                Label rowAmount = HudText.Make(string.Empty, HudTextRole.Body, numeric: true);
                rowAmount.style.unityTextAlign = TextAnchor.MiddleRight;
                rowAmount.style.flexGrow = 1;

                row.Add(rowName);
                row.Add(rowAmount);
                _storeHoldingList.Add(row);
                _storeHoldingRows.Add(row);
            }

            for (int i = 0; i < _storeHoldingRows.Count; i++)
            {
                VisualElement row = _storeHoldingRows[i];
                bool used = i < wanted;
                row.style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                if (!used) continue;

                var rowName = (Label)row[0];
                var rowAmount = (Label)row[1];

                if (_inspect.StoreContents.Count == 0)
                {
                    rowName.text = "nothing yet";
                    rowName.style.color = new Color(1f, 1f, 1f, 0.55f);
                    rowAmount.text = string.Empty;
                    continue;
                }

                StoreContentRow content = _inspect.StoreContents[i];
                rowName.text = content.Name;
                rowName.style.color = new Color(1f, 1f, 1f, 0.92f);
                // The bay count only where it adds something: 150 in two bays is a fact about the
                // store; 40 in one is just the amount, and the number would be noise.
                rowAmount.text = content.Stacks > 1
                    ? content.Units + "  (" + content.Stacks + " bays)"
                    : content.Units.ToString();
            }
        }

        /// <summary>
        /// The store the pane is about — a painted zone, or a shelf — and everything the rows are
        /// drawn from.
        ///
        /// <para><b>One owner, because the panel and its refresh both have to answer it.</b> Two
        /// copies of the zone-or-shelf branch is two chances for the rows on screen and the
        /// signature they are compared against to disagree, which would show as a panel that stops
        /// updating or one that rebuilds every frame.</para>
        /// </summary>
        bool TryStoreUnderPane(out Odyssey.Sim.Storage.StorageSettings? settings,
            out int identity, out int size, out int signature)
        {
            settings = null;
            identity = 0;
            size = 0;
            signature = 0;

            var storage = _boot?.Colony?.Pawns.Storage;
            if (storage == null) return false;

            int store = storage.StoreCellOf(_boot!.Colony!.Grid.Index(_inspect.Cell));

            StorageUnit? unit = _boot.Colony.Pawns.StorageUnits?.AtCell(store);
            if (unit != null)
            {
                settings = _boot.Colony.Pawns.StorageUnits!.SettingsOf(unit);
                // Negative, so a shelf's identity can never collide with a zone's slot index. The
                // model resets its remembered categories and its search when this changes, so two
                // stores sharing a number would carry one's search over to the other.
                identity = -(unit.Edifice + 1);
                size = unit.Slots;
                signature = StorageSignature(identity, settings, size);
                return true;
            }

            int slot = storage.ZoneAt(store);
            if (slot < 0) return false;

            settings = storage.SettingsOf(slot);
            identity = slot;
            size = storage.CellsOf(slot).Count;
            signature = StorageSignature(identity, settings, size);
            return true;
        }

        /// <summary>Everything the rows are drawn from, in one int: the store, its rung, its filter.</summary>
        static int StorageSignature(int identity, Odyssey.Sim.Storage.StorageSettings settings, int size)
        {
            bool[] allow = settings.Allow;

            unchecked
            {
                int signature = identity * 397 + settings.Priority;
                signature = signature * 31 + size;
                for (int i = 0; i < allow.Length; i++) signature = signature * 31 + (allow[i] ? 1 : 0);
                return signature;
            }
        }

        /// <summary>The signature the rows on screen were built from. See <see cref="SyncStoragePanel"/>.</summary>
        int _storageFilledFor;

        /// <summary>Build the rows from the store under the pane. Called on build, after every press, and when the store changes under it.</summary>
        void FillStoragePanel()
        {
            var storage = _boot?.Colony?.Pawns.Storage;
            if (_storageRows == null || storage == null) return;

            int cell = _boot!.Colony!.Grid.Index(_inspect.Cell);

            // **Either kind of store.** The intents this panel sends already resolve a cell to
            // whichever store covers it, painted or built; asking here for a *zone* would find none
            // over a shelf and leave the tab blank — the panel doing nothing, visibly, which is the
            // fault design 20 §8 records under "why Assign did nothing, three times".
            if (!TryStoreUnderPane(out Odyssey.Sim.Storage.StorageSettings? settings,
                    out int identity, out int size, out int signature))
                return;

            _storageFilledFor = signature;
            var content = _boot.Colony.Pawns.Content;
            var keys = new List<string>(content.Items.Length);
            for (int i = 0; i < content.Items.Length; i++) keys.Add(ItemLabels.IconKey(i));

            _storageSettings.Show(cell, identity, hasStore: true, settings!.Priority,
                size, _inspect.Title,
                keys, settings.Accepts, i => (int)content.Items[i].category, Registry.Label);

            // ---- the ladder
            if (_storageRungs != null)
            {
                _storageRungs.Clear();
                for (int i = 0; i < StorageSettingsModel.PriorityKeys.Length; i++)
                {
                    int rung = i;
                    bool held = rung == _storageSettings.Priority;
                    HudColour hue = HudTheme.StoragePriorityHue(rung);
                    _storageRungs.Add(StorageRungButton(
                        Registry.Label(StorageSettingsModel.PriorityKeys[i]), hue, held,
                        last: i == StorageSettingsModel.PriorityKeys.Length - 1,
                        press: () =>
                        {
                            if (_storageSettings.PressPriority(rung, out var c))
                                SendStorageCommand(IntentKind.SetStoragePriority, c);
                        }));
                }
            }

            if (_storageRungName != null)
            {
                HudText.Set(_storageRungName,
                    Registry.Label(StorageSettingsModel.PriorityKeys[_storageSettings.Priority]),
                    HudTextRole.Body);
                _storageRungName.style.color =
                    HudTokens.Convert(HudTheme.StoragePriorityHue(_storageSettings.Priority));
            }

            if (_storageClear != null)
                _storageClear.style.color = HudTokens.Convert(
                    _storageSettings.ClearAllEnabled ? HudTheme.Accent : HudTheme.TextFaint);

            if (_storageStatus != null)
                HudText.Set(_storageStatus, _storageSettings.StatusText, HudTextRole.Meta);

            if (_storageSearch != null)
                _storageSearch.style.display = _storageSettings.ShowSearch
                    ? DisplayStyle.Flex : DisplayStyle.None;

            // A store that takes nothing is a legal state, so this is a warning and not an error:
            // it says what will happen and what to press. Under the control, never in the list.
            if (_storageWarning != null && _storageWarningLead != null && _storageWarningHint != null)
            {
                bool nothing = _storageSettings.NothingAccepted;
                _storageWarning.style.display = nothing ? DisplayStyle.Flex : DisplayStyle.None;
                if (nothing)
                {
                    HudText.Set(_storageWarningLead, StorageSettingsModel.NothingAcceptedLead, HudTextRole.Meta);
                    HudText.Set(_storageWarningHint, StorageSettingsModel.NothingAcceptedHint, HudTextRole.Meta);
                }

                // The band's height comes out of the list, not out of the screen. See
                // StorageWarningHeight.
                if (_storageList != null)
                    _storageList.style.height =
                        StorageListHeight - (nothing ? StorageWarningHeight : 0);
            }

            // ---- the list
            _storageRows.Clear();

            // A search that matches nothing says what was typed rather than "no results", and the
            // list keeps its height so the pane does not jump as you type.
            if (_storageSettings.Searching && _storageSettings.Rows.Count == 0)
            {
                _storageRows.Add(StorageNote(_storageSettings.NoMatchesLead, HudTheme.TextMeta));
                _storageRows.Add(StorageNote(_storageSettings.NoMatchesHint, HudTheme.TextFaint));
                return;
            }

            foreach (StorageSettingsModel.Row row in _storageSettings.Rows)
            {
                StorageSettingsModel.Row captured = row;
                if (row.Kind == StorageSettingsModel.RowKind.Commodity)
                {
                    _storageRows.Add(StorageCommodityRow(row, () =>
                    {
                        if (_storageSettings.PressDef(captured.DefIndex, out var c))
                            SendStorageCommand(IntentKind.SetStorageFilter, c);
                    }));
                    continue;
                }

                _storageRows.Add(StorageCategoryRow(row,
                    press: () =>
                    {
                        var commands = new List<StorageSettingsModel.Command>();
                        if (!_storageSettings.PressCategory(captured.Category, commands)) return;
                        foreach (StorageSettingsModel.Command c in commands)
                            SendStorageCommand(IntentKind.SetStorageFilter, c);
                    },
                    pressCaret: () =>
                    {
                        if (_storageSettings.ToggleExpanded(captured.Category)) FillStoragePanel();
                    }));
            }
        }

        /// <summary>One rung of the ladder: a colour square and a name, in a box that fills when held.</summary>
        VisualElement StorageRungButton(string label, HudColour hue, bool held, bool last, Action press)
        {
            var button = new VisualElement();
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.flexGrow = 1;
            button.style.height = 34;
            if (!last) button.style.marginRight = 6;

            button.style.backgroundColor = held
                ? new Color(hue.R / 255f, hue.G / 255f, hue.B / 255f, 0.22f)
                : new Color(0f, 0f, 0f, 0.40f);
            SetBorder(button, held ? HudTokens.Convert(hue) : new Color(1f, 1f, 1f, 0.18f), 1);

            var swatch = new VisualElement();
            swatch.style.width = 8;
            swatch.style.height = 8;
            swatch.style.flexShrink = 0;
            swatch.style.marginRight = 7;
            swatch.style.backgroundColor = HudTokens.Convert(hue);
            button.Add(swatch);

            Label text = HudText.Make(label, HudTextRole.Body);
            text.style.color = held ? HudTokens.Convert(hue) : new Color(1f, 1f, 1f, 0.72f);
            button.Add(text);

            button.RegisterCallback<ClickEvent>(_ => press());
            return button;
        }

        /// <summary>
        /// A category row: caret, box, glyph, name in the category's hue, and the member count
        /// right-aligned. The row is washed with its own hue at a tenth, so six blocks stay
        /// findable while scrolling — and the commodity rows below stay plain, because the colour
        /// marks the group and not every line.
        /// </summary>
        VisualElement StorageCategoryRow(StorageSettingsModel.Row row, Action press, Action pressCaret)
        {
            HudColour hue = HudTheme.ItemCategoryHue(row.Category);
            bool empty = row.IsEmpty;

            var element = new VisualElement();
            element.AddToClassList(StorageCategoryClass);
            element.style.flexDirection = FlexDirection.Row;
            element.style.alignItems = Align.Center;
            element.style.height = StorageRowHeight;
            element.style.flexShrink = 0;
            element.style.paddingLeft = 10;
            element.style.paddingRight = 10;
            element.style.backgroundColor =
                new Color(hue.R / 255f, hue.G / 255f, hue.B / 255f, empty ? 0.045f : 0.09f);

            // The caret column exists whether or not this row has a caret, so every box below it
            // starts at the same x. An empty category has none at all, which is how it says it
            // will not open.
            var caret = new VisualElement();
            caret.style.width = StorageCaretColumn;
            caret.style.flexShrink = 0;
            if (!empty)
            {
                var glyph = new HudGlyph(
                    row.Expanded ? HudGlyphKind.ChevronDown : HudGlyphKind.ChevronRight,
                    StorageCaretColumn, HudTokens.Convert(hue));
                glyph.pickingMode = PickingMode.Position;
                glyph.RegisterCallback<ClickEvent>(e => { pressCaret(); e.StopPropagation(); });
                caret.Add(glyph);
            }

            element.Add(caret);
            element.Add(StorageCheckbox(row.State, hue, 10));

            var icon = new HudGlyph(CategoryGlyph(row.Category), StorageGlyph,
                HudTokens.Convert(empty ? hue.WithAlpha(0.45f) : hue));
            icon.style.marginLeft = 10;
            element.Add(icon);

            // A category is a heading over the commodities it opens onto, so it is set in the
            // heading role: a step above the rows beneath it, tracked, and upper-cased by
            // HudText.Set rather than by a ToUpper here — the case belongs to the role, or the
            // next heading somewhere else is capitalised by hand and the two drift apart.
            Label text = HudText.Make(row.Label, HudTextRole.ListHeading);
            text.style.marginLeft = 10;
            text.style.color = HudTokens.Convert(empty ? hue.WithAlpha(0.45f) : hue);
            element.Add(text);

            // The member count, a step up and set as a figure (owner, 2026-09-21: "make the
            // numbers bigger in the stock control component"). It was Meta 12 in the UI face,
            // which is the size of a qualifying aside and the smallest thing in the pane — and
            // it sat beside a 14/600 heading, so the one number on the row read as a footnote to
            // its own row. Row is 14/500, the same step as the heading it answers to, and
            // `numeric` swaps it to the mono face with tabular figures, so a column of counts
            // lines up whatever the digits are. Both are the shared scale rather than a size
            // written here: HudType owns the steps.
            Label count = HudText.Make(row.Members.ToString(), HudTextRole.Row, numeric: true);
            count.style.flexGrow = 1;
            count.style.unityTextAlign = TextAnchor.MiddleRight;
            if (empty) count.style.color = new Color(1f, 1f, 1f, 0.30f);
            element.Add(count);

            element.RegisterCallback<ClickEvent>(_ => press());
            return element;
        }

        /// <summary>
        /// A commodity row: indented past the caret column, a box, and a plain name.
        ///
        /// <b>No icon column is reserved</b>, because most commodities have no art and a row of
        /// placeholder squares says less than nothing. The day a sheet covers them the badge goes
        /// in front of the label and nothing else moves.
        /// </summary>
        VisualElement StorageCommodityRow(StorageSettingsModel.Row row, Action press)
        {
            var element = new VisualElement();
            element.style.flexDirection = FlexDirection.Row;
            element.style.alignItems = Align.Center;
            element.style.height = StorageCommodityRowHeight;
            element.style.flexShrink = 0;
            element.style.paddingLeft = StorageIndent;
            element.style.paddingRight = 10;

            element.Add(StorageCheckbox(row.State, HudTheme.ItemCategoryHue(row.Category), 0));

            Label text = HudText.Make(row.Label, HudTextRole.Body);
            text.style.marginLeft = 10;
            element.Add(text);

            // The matched run, marked where it really is. Drawn as a band behind the whole label
            // rather than as a slice of it, because a Label is one text element and cutting it into
            // three would give three baselines to keep in line — the exact failure this rebuild is
            // fixing.
            if (row.MatchStart >= 0)
                text.style.backgroundColor = new Color(1f, 1f, 1f, 0.14f);

            element.RegisterCallback<ClickEvent>(_ => press());
            return element;
        }

        /// <summary>
        /// The box: 16 px square, square corners, a 1 px border. Off is hollow, mixed carries the
        /// tri-state bar in the hue, on is a filled hue with the tick in panel ink.
        /// </summary>
        VisualElement StorageCheckbox(int state, HudColour hue, int marginLeft)
        {
            var box = new VisualElement();
            box.style.width = StorageBox;
            box.style.height = StorageBox;
            box.style.flexShrink = 0;
            box.style.alignItems = Align.Center;
            box.style.justifyContent = Justify.Center;
            if (marginLeft > 0) box.style.marginLeft = marginLeft;

            if (state == StorageSettingsModel.CategoryOn)
            {
                box.style.backgroundColor = HudTokens.Convert(hue);
                SetBorder(box, HudTokens.Convert(hue), 1);
                box.Add(new HudGlyph(HudGlyphKind.Check, StorageBox - 2,
                    HudTokens.Convert(HudTheme.OnAccent)));
            }
            else if (state == StorageSettingsModel.CategoryMixed)
            {
                box.style.backgroundColor =
                    new Color(hue.R / 255f, hue.G / 255f, hue.B / 255f, 0.18f);
                SetBorder(box, HudTokens.Convert(hue), 1);
                box.Add(new HudGlyph(HudGlyphKind.TriState, StorageBox - 2, HudTokens.Convert(hue)));
            }
            else
            {
                box.style.backgroundColor = Color.clear;
                SetBorder(box, new Color(1f, 1f, 1f, 0.28f), 1);
            }

            return box;
        }

        /// <summary>A line of copy in the list: a warning, or the reason a search found nothing.</summary>
        /// <summary>
        /// A note's label inside its own padded box, for a note that is written again rather than
        /// rebuilt. The caller adds <c>label.parent</c>; <see cref="StorageNote"/> is the one-shot
        /// version of the same thing and the two share their padding by sharing this.
        /// </summary>
        Label StorageNoteLabel(HudColour ink)
        {
            var element = new VisualElement();
            element.style.paddingLeft = 14;
            element.style.paddingRight = 14;
            element.style.paddingTop = 4;
            element.style.paddingBottom = 4;
            element.style.flexShrink = 0;

            Label label = HudText.Make(string.Empty, HudTextRole.Meta);
            label.style.color = HudTokens.Convert(ink);
            label.style.whiteSpace = WhiteSpace.Normal;
            element.Add(label);
            return label;
        }

        VisualElement StorageNote(string text, HudColour ink)
        {
            var element = new VisualElement();
            element.style.paddingLeft = 14;
            element.style.paddingRight = 14;
            element.style.paddingTop = 4;
            element.style.paddingBottom = 4;
            element.style.flexShrink = 0;

            Label label = HudText.Make(text, HudTextRole.Meta);
            label.style.color = HudTokens.Convert(ink);
            label.style.whiteSpace = WhiteSpace.Normal;
            element.Add(label);
            return element;
        }

        static HudGlyphKind CategoryGlyph(int category) => category switch
        {
            0 => HudGlyphKind.CategoryFood,
            1 => HudGlyphKind.CategoryMedicine,
            2 => HudGlyphKind.CategoryMaterials,
            3 => HudGlyphKind.CategoryBooks,
            4 => HudGlyphKind.CategoryItems,
            _ => HudGlyphKind.CategoryWeapons,
        };

        /// <summary>A 26 px square in the title row: rename, and close. Disabled when there is no action.</summary>
        static void SetBorder(VisualElement element, Color colour, int width)
        {
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopColor = colour;
            element.style.borderBottomColor = colour;
            element.style.borderLeftColor = colour;
            element.style.borderRightColor = colour;
        }

        /// <summary>
        /// Submit one of the store's commands about the cell the pane is describing, then rebuild
        /// the rows.
        ///
        /// <para>The refill here is the <em>paused</em> case, and it used to be the only one: a
        /// storage intent applies immediately while paused, so the answer really is already true
        /// by the time the next frame draws — and while the game is running it is not, because
        /// the intent queues for the next tick. <see cref="SyncStoragePanel"/> is what covers the
        /// other half. This call stays because it costs nothing and it is what makes a press feel
        /// instant on a paused board, where no tick is coming to catch it.</para>
        /// </summary>
        void SendStorageCommand(IntentKind kind, StorageSettingsModel.Command command)
        {
            var world = _boot?.World;
            if (world == null) return;
            world.Intents.Submit(new Intent(kind, _inspect.Cell, command.A, command.B, command.C));
            FillStoragePanel();
        }


        /// <summary>
        /// What a name in the picker carries beside it. No words: the owner asked for "just a
        /// tick next to their name and also indicate the others already have a bed assigned"
        /// (2026-09-19), and a column of "has a bed" / "no bed" would be three times the reading
        /// for the same fact. A tick is this bed, a dot is a bed somewhere else, and a blank is
        /// a colonist with nowhere of her own — which is the row the eye is hunting for, so it
        /// is the one with nothing on it.
        /// </summary>
        enum BedPickerMark { None, ThisBed, AnotherBed }

        /// <summary>
        /// The drawn tick, and <b>exactly the 14px <c>.bedowner__mark</c> column</b>, because a
        /// HudGlyph writes its own width inline and an inline width beats the stylesheet's. A
        /// smaller number here would draw a smaller tick and start the name two pixels to the
        /// left of every other row's, which is the one thing that column exists to prevent.
        /// </summary>
        const int BedPickerMarkSize = 14;

        VisualElement BedPickerRow(string label, int pawnId, BedPickerMark mark)
        {
            var row = new VisualElement();
            row.AddToClassList("bedowner__row");

            // The mark sits in its own fixed-width column rather than in front of the name, so
            // every name in the list starts at the same x and the column can be read down.
            //
            // The tick is drawn and the dot is typed, and that asymmetry is measured rather than
            // stylistic: Archivo Narrow's cmap has U+2022 and does not have U+2713, so this row's
            // "this bed" mark has been an empty column since it was written. Found by
            // HudFontTests, which reads both .ttf files, after the same fault turned up in the
            // Work tab's Simple mode. Neither tier could see it — the fast tier has no text
            // engine and the Unity tier asserts no pixels.
            VisualElement flag;
            if (mark == BedPickerMark.ThisBed)
            {
                var tick = new HudGlyph(HudGlyphKind.Check, BedPickerMarkSize,
                    HudTokens.Convert(HudTheme.TextPrimary));
                tick.AddToClassList("bedowner__mark");

                // Not bedowner__mark--this: that rule is a text colour and a drawn glyph takes
                // its colour as a tint. The tint above is what that rule was asking for.
                flag = tick;
            }
            else
            {
                flag = HudText.Make(
                    mark == BedPickerMark.AnotherBed ? "•" : string.Empty,
                    HudTextRole.Body, ussClass: "bedowner__mark");
            }
            row.Add(flag);

            row.Add(HudText.Make(label, HudTextRole.Body, ussClass: "bedowner__name"));

            CellRef cell = _inspect.Cell;
            int pick = pawnId;
            row.tooltip = pick < 0
                ? "Leave the bed unowned"
                : mark switch
                {
                    BedPickerMark.ThisBed => label + " sleeps here already",
                    BedPickerMark.AnotherBed => "Move " + label + " here from another bed",
                    _ => "Give this bed to " + label,
                };
            row.RegisterCallback<ClickEvent>(_ =>
            {
                _boot?.World?.Intents.Submit(new Intent(IntentKind.AssignBedOwner, cell, pick));
                CloseBedPicker();
            });
            return row;
        }

        VisualElement ActionButton(InspectCommand command)
        {
            var button = new VisualElement();
            button.AddToClassList("action");
            if (!command.Enabled) button.AddToClassList("action--off");
            var icon = new IconBadge(command.IconKey, IconBadge.BarSize);
            icon.Inherit(HudTokens.TextMeta);
            button.Add(icon);
            button.Add(HudText.Make(command.Label, HudTextRole.Meta, ussClass: "action__label"));
            button.tooltip = command.Label + " — " + command.Reason;
            return button;
        }

        /// <summary>
        /// One need row. The name comes from the registry rather than from the caller, which is
        /// what stops the screen and the wiki disagreeing: the three literals that used to be
        /// passed in here were fine until storage added <c>ui.res.category.food</c>, at which
        /// point "Food" was a registry name written in C# and
        /// <c>RegistryTests.NoPlayerFacingNameIsWrittenInCSharp</c> said so. The answer to that
        /// test is never to reword the literal.
        /// </summary>
        static NeedView Need(VisualElement grid, string iconKey)
        {
            string name = Registry.Label(iconKey);
            var view = new NeedView();

            view.Root = new VisualElement();
            view.Root.AddToClassList("need");

            var line = new VisualElement();
            line.AddToClassList("need__line");
            view.Icon = new IconBadge(iconKey, IconBadge.RowSize);
            view.Icon.Inherit(HudTokens.TextMeta);
            view.Name = HudText.Make(name, HudTextRole.Body, ussClass: "need__name");
            view.Value = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true, "need__value");
            line.Add(view.Icon);
            line.Add(view.Name);
            line.Add(view.Value);
            view.Root.Add(line);

            var bar = new VisualElement();
            bar.AddToClassList("bar");
            bar.AddToClassList("bar--need");
            view.Fill = new VisualElement();
            view.Fill.AddToClassList("bar__fill");
            bar.Add(view.Fill);
            view.Root.Add(bar);

            grid.Add(view.Root);
            return view;
        }

        void OpenAlmanacForSelection()
        {
            if (_directors?.Almanac == null) return;
            if (!_directors.Almanac.OpenForSelection(_inspect))
            {
                ToggleAlmanac(true);
            }
        }
    }
}
