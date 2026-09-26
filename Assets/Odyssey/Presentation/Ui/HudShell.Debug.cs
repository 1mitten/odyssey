#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pawns;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The debug menu: backtick's own panel since 2026-09-17, replacing what used to be a direct
    /// toggle of the developer overlay. Built the same way <c>HudShell.Bar.cs</c> builds Settings —
    /// a <see cref="Window"/>, a tab strip, <c>.settings__row</c>s, the same pip and disabled-row
    /// idioms — so a second panel in this shell does not invent a second visual language.
    ///
    /// <para><b>Not a modal.</b> Like Settings, there is no scrim: the world keeps running while it
    /// is open, because a debug menu that froze the game to use it would defeat most of what it is
    /// for.</para>
    ///
    /// <para><b>Four tabs</b> (owner, 2026-09-20, 2026-09-22 and, for Weather, 2026-09-24). <b>Cheats</b>: the developer-overlay toggle,
    /// moved here wholesale from Settings' Interface tab rather than duplicated
    /// (<c>EveryLiveToolIsDrawnSomewhere</c>), and the grants that wrap sim APIs that already exist.
    /// <b>Events</b>: one row per incident the content declares, each fired through the same door
    /// a storyteller will use (design 23 §3), built from the open colony's content when the panel
    /// opens so a second Def appears by existing. <b>Spawn</b>: one row per kind of pawn —
    /// the colonist, the animals and the bandit — and one per weapon, placed near the camera
    /// (<see cref="DebugDirector.SpawnRows"/>).</para>
    /// </summary>
    public sealed partial class HudShell
    {
        VisualElement _debugDeveloperRow = null!;
        VisualElement _debugCheats = null!;
        VisualElement _debugEvents = null!;
        VisualElement _debugSpawn = null!;
        VisualElement _debugWeather = null!;
        readonly List<VisualElement> _debugWeatherRows = new();
        VisualElement? _debugParticlesRow;
        VisualElement? _debugGlossRow;
        readonly Dictionary<DebugTab, Label> _debugTabs = new();

        /// <summary>The content the event rows were last built from, so a new colony rebuilds them and a reopen does not.</summary>
        IncidentContent? _debugEventsFrom;

        /// <summary>Under the Raid row: why the last raid asked for was refused, hidden while none was.</summary>
        Label? _raidNote;

        void BuildDebug()
        {
            _debugPanel = Window("debug", Registry.Label(DebugDirector.PanelKey),
                () => _directors?.Debug.SetOpen(false), "debug");

            // The tab strip, in Settings' idiom: nothing new is invented for a third use of it.
            var tabs = new VisualElement();
            tabs.AddToClassList("settings__tabs");
            foreach (DebugTab tab in new[] { DebugTab.Cheats, DebugTab.Spawn, DebugTab.Events, DebugTab.Weather, DebugTab.Faces })
            {
                Label chip = HudText.Make(Registry.Label(DebugDirector.TabKey(tab)), HudTextRole.Body, ussClass: "tab");
                DebugTab captured = tab;
                chip.RegisterCallback<ClickEvent>(_ => _directors?.Debug.SetTab(captured));
                _debugTabs[tab] = chip;
                tabs.Add(chip);
            }
            _debugPanel.Add(tabs);

            _debugCheats = new VisualElement();
            _debugCheats.AddToClassList("settings__body");
            _debugDeveloperRow = DebugToggleRow(SettingsDirector.DeveloperKey,
                "The frame-time readout. Also the ` key, and kept between sessions",
                () => _directors?.Settings.SetDeveloperOverlay(!_directors.Settings.DeveloperOverlay));
            _debugCheats.Add(_debugDeveloperRow);
            // The three grants moved to the Spawn tab's Items heading (design 33 §9i): they put
            // things on the board, which is what that tab is for.
            _debugCheats.Add(DebugActionRow(DebugDirector.SkipDayKey,
                "Spends one whole game day of ticks at once (about a fifth of a second). "
                    + "The crop's stage changes arrive at the same hour each press; works while paused",
                SkipDay));
            _debugCheats.Add(DebugActionRow(DebugDirector.SkipMonthKey,
                "Spends a whole game month of ticks at once - twelve days, so a couple of seconds "
                    + "of standing still. Six presses walk the year: Wash is mild, Glare is warm, "
                    + "and Rime is the season the campfire exists for",
                SkipMonth));
            _debugCheats.Add(DebugActionRow(DebugDirector.SkipMorningKey,
                "Skips the night and hands back the clock at dawn, with a whole watchable day "
                    + "ahead: the harvest happens on screen, not inside the skip",
                () => _boot!.DebugSkipToMorning()));
            _debugCheats.Add(DebugActionRow(DebugDirector.SkipNightKey,
                "Skips to ten at night, fully dark, with seven hours of night ahead: the "
                    + "butterflies' glow at its brightest (design 52)",
                () => _boot!.DebugSkipToNight()));
            _debugCheats.Add(DebugActionRow(DebugDirector.RipenCropsKey,
                "Brings every standing crop to ripeness at once, daylight window and all - "
                    + "the harvest half without the four-day wait",
                RipenCrops));
            _debugCheats.Add(DebugActionRow(DebugDirector.FinishResearchKey,
                "Completes the project being researched and starts the next in the queue - the "
                    + "only way a project becomes done until research is a mechanism",
                FinishResearch));
            _debugTraceRow = DebugToggleRow(DebugDirector.TraceKey,
                "Stops or starts this session's performance trace. Off, then a second session on, "
                    + "is how the tracer itself gets ruled out of a report about stutter",
                ToggleTrace);
            _debugCheats.Add(_debugTraceRow);
            _debugJumpsFailRow = DebugToggleRow(DebugDirector.JumpsFailKey,
                "Every jump over a one-cell stream falls short into the water, and the colonist "
                    + "climbs out on the far side. Off by default; a new colony starts with it off",
                ToggleJumpsFail);
            _debugCheats.Add(_debugJumpsFailRow);
            _debugCheats.Add(DebugActionRow(DebugDirector.MarkTraceKey,
                "Writes a marker into this session's performance trace, so the seconds around "
                    + "this moment can be found afterwards - press it when something felt wrong",
                MarkTrace));
            _debugPanel.Add(_debugCheats);

            // Who and what can be put on the board (owner, 2026-09-22: a tab of its own rather
            // than three rows among the grants): the colonist first, the animals, the bandit and
            // one of each weapon (design 33 §1). The rows and what each sends are
            // DebugDirector.SpawnRows, held by the fast tier; this only lays them out.
            // Grouped under a heading each (design 33 §9i; owner, 2026-09-24: "a category for each
            // type of spawn"), in two columns now the window is wide enough for them: who (colonists,
            // hostiles, animals) on the left, what (weapons, items) on the right. The headings are
            // the Keys tab's .settings__section, so nothing new is invented for them.
            _debugSpawn = new VisualElement();
            _debugSpawn.AddToClassList("settings__body");
            var spawnColumns = new VisualElement();
            spawnColumns.AddToClassList("settings__columns");
            var who = new VisualElement();
            who.AddToClassList("settings__column");
            var what = new VisualElement();
            what.AddToClassList("settings__column");
            spawnColumns.Add(who);
            spawnColumns.Add(what);
            foreach (string group in DebugDirector.SpawnGroups)
            {
                VisualElement column = group == DebugDirector.GroupWeaponsKey || group == DebugDirector.GroupItemsKey
                    ? what : who;
                column.Add(HudText.Make(Registry.Label(group), HudTextRole.Meta, ussClass: "settings__section"));
                foreach (DebugDirector.SpawnRow spawn in DebugDirector.SpawnRows)
                {
                    if (spawn.Group != group) continue;
                    DebugDirector.SpawnRow captured = spawn;
                    column.Add(DebugActionRow(spawn.Key, spawn.Tooltip, () => Spawn(captured)));
                }
            }
            _debugSpawn.Add(spawnColumns);
            _debugPanel.Add(_debugSpawn);

            // Filled when the panel opens, from the colony that is open: the content is the
            // colony's, and there is no colony when the shell is built.
            _debugEvents = new VisualElement();
            _debugEvents.AddToClassList("settings__body");
            _debugPanel.Add(_debugEvents);

            // The sky set by hand (owner, 2026-09-24): one row per preset, the set one lit with
            // Settings' pip, and the particle control as a toggle beneath them. Drawing only — the
            // bootstrap reads DebugDirector.CurrentWeather each frame and nothing reaches the
            // simulation, which has no weather yet (design 43 §8).
            _debugWeather = new VisualElement();
            _debugWeather.AddToClassList("settings__body");
            // Each row commands the weather system (design 43 §8): the sky blends in over a few
            // seconds, runs its rolled spell, and the season takes over again. The row lit is the
            // kind the published sky holds, so a row never claims a sky the game has moved on from.
            for (int i = 0; i < DebugDirector.WeatherPresets.Length; i++)
            {
                DebugDirector.WeatherPreset preset = DebugDirector.WeatherPresets[i];
                VisualElement row = DebugToggleRow(preset.Key, preset.Tooltip, () => SetWeather(preset));
                _debugWeatherRows.Add(row);
                _debugWeather.Add(row);
            }
            _debugParticlesRow = DebugToggleRow(DebugDirector.RainParticlesKey,
                "Draws the rain with CPU particles, as the weather design first wrote it, so the two "
                    + "can be compared moving. Wet ground stays on either way",
                () => _directors?.Debug.SetRainAsParticles(!_directors.Debug.RainAsParticles));
            _debugWeather.Add(_debugParticlesRow);
            _debugGlossRow = DebugToggleRow(DebugDirector.WetGlossKey,
                "Draws wet ground as shine and puddles only, rather than richer and a little darker - "
                    + "the two looks being chosen between by eye",
                () => _directors?.Debug.SetWetGlossOnly(!_directors.Debug.WetGlossOnly));
            _debugWeather.Add(_debugGlossRow);
            _debugPanel.Add(_debugWeather);

            // Faces and talking (design 59 §7): drawing only, straight to the figure director. The
            // rows and what each holds are DebugDirector.FaceRows, held by the fast tier.
            _debugFaces = new VisualElement();
            _debugFaces.AddToClassList("settings__body");
            _debugTalkRow = DebugToggleRow(DebugDirector.TalkKey, DebugDirector.TalkTooltip, ToggleTalk);
            _debugFaces.Add(_debugTalkRow);
            _debugAutoFaceRow = DebugToggleRow(DebugDirector.AutoFaceKey, DebugDirector.AutoFaceTooltip, () => SetFace(null));
            _debugFaces.Add(_debugAutoFaceRow);
            foreach (DebugDirector.FaceRow face in DebugDirector.FaceRows)
            {
                FaceExpression expression = face.Expression;
                VisualElement row = DebugToggleRow(face.Key, face.Tooltip, () => SetFace(expression));
                _debugFaceRows.Add(row);
                _debugFaces.Add(row);
            }
            // A conversation ends by itself, so the Talk pip is asked again twice a second rather
            // than only when clicked. Cheap, and it does nothing while the panel is shut.
            _debugFaces.schedule.Execute(RefreshDebugFaces).Every(500);
            _debugPanel.Add(_debugFaces);

            OnDebugTabChanged(DebugTab.Cheats);
            _hud.Add(_debugPanel);
        }

        /// <summary>A toggle row: the pip idiom Settings already uses for the graphics options and
        /// used to use for this exact row, before it moved here.</summary>
        /// <summary>
        /// Mark this moment in the trace.
        ///
        /// <para>Nothing is said back here, and nothing needs to be: the developer overlay prints
        /// the trace file and its marker count, so a mark that landed is visible and a mark that
        /// had nowhere to land is visible too — the overlay says the trace is off.</para>
        /// </summary>
        void MarkTrace() => _boot?.MarkTrace("debug menu");

        VisualElement? _debugTraceRow;

        VisualElement? _debugJumpsFailRow;

        /// <summary>The colony the jumps switch was last turned on in. The switch lives on that
        /// colony's pawn context, unsaved, so a new colony starts with it off and the row has to
        /// say so rather than remember a switch that no longer exists.</summary>
        object? _debugJumpsFailColony;

        bool DebugJumpsFailOn => _debugJumpsFailColony != null && ReferenceEquals(_debugJumpsFailColony, _boot?.Colony);

        /// <summary>
        /// Jumps always fail (design 46 §6): flips the switch on the pawn context through its
        /// intent, which applies while paused, and shows which it is.
        /// </summary>
        void ToggleJumpsFail()
        {
            var world = _boot?.World;
            if (world == null) return;
            bool on = !DebugJumpsFailOn;
            world.Intents.Submit(new Intent(IntentKind.DebugJumpsFail, default, on ? 1 : 0));
            _debugJumpsFailColony = on ? _boot!.Colony : null;
            RefreshJumpsFailRow();
        }

        void RefreshJumpsFailRow() => _debugJumpsFailRow?.EnableInClassList("settings__row--on", DebugJumpsFailOn);

        /// <summary>
        /// Turn tracing off or on, and show which it is.
        ///
        /// <para>Off takes effect at once — the file is closed and the phase sink detached. On
        /// takes effect on the next frame, which opens a <em>new</em> file rather than reopening
        /// the old one: two halves of one session in one file would be indistinguishable from a
        /// single session, and the whole point of the switch is to tell the two apart.</para>
        /// </summary>
        void ToggleTrace()
        {
            OdysseyBootstrap.TraceEnabled = !OdysseyBootstrap.TraceEnabled;
            if (!OdysseyBootstrap.TraceEnabled) _boot?.StopTrace();
            RefreshTraceRow();
        }

        void RefreshTraceRow() => _debugTraceRow?.EnableInClassList(
            "settings__row--on", OdysseyBootstrap.TraceEnabled);

        VisualElement DebugToggleRow(string key, string tooltip, System.Action onClick)
        {
            var row = new VisualElement();
            row.AddToClassList("settings__row");
            var icon = new IconBadge(key, IconBadge.RowSize);
            icon.Inherit(HudTokens.TextMeta);
            row.Add(icon);
            row.Add(HudText.Make(Registry.Label(key), HudTextRole.Row, ussClass: "settings__label"));

            var pip = new VisualElement { pickingMode = PickingMode.Ignore };
            pip.AddToClassList("settings__pip");
            row.Add(pip);

            row.tooltip = tooltip;
            row.RegisterCallback<ClickEvent>(_ => onClick());
            return row;
        }

        /// <summary>A one-shot row: the same idiom a session command uses (Save, Reset keys), fired
        /// once on click rather than toggled. <paramref name="onClick"/> is null for a row that is
        /// visible but inert.</summary>
        VisualElement DebugActionRow(string key, string tooltip, System.Action? onClick)
        {
            var row = new VisualElement();
            row.AddToClassList("settings__row");
            var icon = new IconBadge(key, IconBadge.RowSize);
            icon.Inherit(HudTokens.TextMeta);
            row.Add(icon);
            row.Add(HudText.Make(Registry.Label(key), HudTextRole.Row, ussClass: "settings__label"));
            row.tooltip = tooltip;
            if (onClick != null) row.RegisterCallback<ClickEvent>(_ => onClick());
            return row;
        }

        /// <summary>
        /// One row per incident, from the open colony's content. Rebuilt only when the content
        /// is a different object — a new colony — so reopening the panel costs nothing and a
        /// second Def appears without this file learning its name.
        /// </summary>
        void RefreshDebugEvents()
        {
            IncidentContent? content = _boot?.Colony?.Incidents.Content;
            if (ReferenceEquals(content, _debugEventsFrom)) return;
            _debugEventsFrom = content;
            _debugEvents.Clear();

            if (content == null)
            {
                _debugEvents.Add(HudText.Make("No colony is open.", HudTextRole.Meta, ussClass: "settings__note"));
                return;
            }

            for (int i = 0; i < content.Count; i++)
            {
                // An incident the world writes down when it happens — a theft (design 33 §17) —
                // cannot be fired, so a row for it would do nothing.
                if (!content.Workers[i].Fireable) continue;
                int def = i;

                // A raid carries its two controls under its row (design 55 §9): how many and who.
                // The row sends what they hold; they hold it for the session.
                if (content.Workers[i] is RaidWorker)
                {
                    _debugEvents.Add(DebugActionRow(IncidentLabels.IconKey(i),
                        content.Defs[i].description ?? string.Empty, () => InvokeRaid(def)));
                    _debugEvents.Add(DebugRaidSizeRow());
                    _debugEvents.Add(DebugRaidMixRow());
                    _raidNote = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "settings__note");
                    _raidNote.style.display = DisplayStyle.None;
                    _debugEvents.Add(_raidNote);
                    continue;
                }

                _debugEvents.Add(DebugActionRow(IncidentLabels.IconKey(i),
                    content.Defs[i].description ?? string.Empty, () => InvokeIncident(def)));
            }
        }

        /// <summary>
        /// The raid's size (design 55 §9): the Settings window's own fader, whole numbers from 0 to
        /// <see cref="DebugDirector.RaidSizeMax"/>, the figure after it reading <i>Auto</i> at 0.
        /// </summary>
        VisualElement DebugRaidSizeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("settings__row");
            var icon = new IconBadge(DebugDirector.RaidSizeKey, IconBadge.RowSize);
            icon.Inherit(HudTokens.TextMeta);
            row.Add(icon);
            row.Add(HudText.Make(Registry.Label(DebugDirector.RaidSizeKey), HudTextRole.Row, ussClass: "settings__label"));
            row.tooltip = DebugDirector.RaidSizeTooltip;

            int start = _directors?.Debug.RaidSize ?? 0;
            var control = new VisualElement();
            control.AddToClassList("sw__slider");
            var slider = new SliderInt(0, DebugDirector.RaidSizeMax, SliderDirection.Horizontal);
            slider.AddToClassList("settings__fader");
            slider.SetValueWithoutNotify(start);
            VisualElement tracker = slider.Q(className: "unity-base-slider__tracker") ?? slider;
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("sw__fill");
            fill.style.width = Length.Percent(start * 100f / DebugDirector.RaidSizeMax);
            tracker.Add(fill);
            control.Add(slider);

            Label value = HudText.Make(DebugDirector.RaidSizeText(start), HudTextRole.Row,
                numeric: true, ussClass: "settings__value");
            control.Add(value);

            slider.RegisterValueChangedCallback(evt =>
            {
                if (_directors == null) return;
                _directors.Debug.SetRaidSize(evt.newValue);
                int size = _directors.Debug.RaidSize;
                value.text = DebugDirector.RaidSizeText(size);
                fill.style.width = Length.Percent(size * 100f / DebugDirector.RaidSizeMax);
            });
            row.Add(control);
            return row;
        }

        /// <summary>
        /// The raid's mix (design 55 §8): the Settings window's own select, one choice per mix in
        /// the content's order, named by <see cref="RaidMixLabels"/>.
        /// </summary>
        VisualElement DebugRaidMixRow()
        {
            var row = new VisualElement();
            row.AddToClassList("settings__row");
            var icon = new IconBadge(DebugDirector.RaidMixKey, IconBadge.RowSize);
            icon.Inherit(HudTokens.TextMeta);
            row.Add(icon);
            row.Add(HudText.Make(Registry.Label(DebugDirector.RaidMixKey), HudTextRole.Row, ussClass: "settings__label"));
            row.tooltip = DebugDirector.RaidMixTooltip;

            var choices = new List<string>(RaidMixLabels.Keys.Length);
            for (int m = 0; m < RaidMixLabels.Keys.Length; m++) choices.Add(RaidMixLabels.Label(m));
            int current = _directors?.Debug.RaidMix ?? RaidMixLabels.Default;

            var dropdown = new DropdownField(choices, current);
            dropdown.AddToClassList("sw__select");
            if (dropdown.labelElement != null) dropdown.labelElement.style.display = DisplayStyle.None;
            var textElem = dropdown.Q<TextElement>(className: "unity-base-popup-field__text");
            if (textElem != null) HudText.Apply(textElem, HudTextRole.Body);

            // The engine's arrow is a texture; the window draws its own marks.
            VisualElement? input = dropdown.Q(className: "unity-base-popup-field__input");
            VisualElement? arrow = dropdown.Q(className: "unity-base-popup-field__arrow");
            if (arrow != null) arrow.style.display = DisplayStyle.None;
            input?.Add(new PathGlyph(SettingsLayout.SelectArrow, 10f, 6f, Ink(HudTheme.TextMeta), 10f, fill: true));

            dropdown.RegisterValueChangedCallback(evt =>
            {
                int picked = choices.IndexOf(evt.newValue);
                if (picked >= 0) _directors?.Debug.SetRaidMix(picked);
            });
            row.Add(dropdown);
            return row;
        }

        void OnDebugTabChanged(DebugTab tab)
        {
            foreach (var entry in _debugTabs)
            {
                bool on = entry.Key == tab;
                entry.Value.EnableInClassList("tab--on", on);
                entry.Value.EnableInClassList("tab--off", !on);
            }
            _debugCheats.style.display = tab == DebugTab.Cheats ? DisplayStyle.Flex : DisplayStyle.None;
            _debugEvents.style.display = tab == DebugTab.Events ? DisplayStyle.Flex : DisplayStyle.None;
            _debugSpawn.style.display = tab == DebugTab.Spawn ? DisplayStyle.Flex : DisplayStyle.None;
            _debugWeather.style.display = tab == DebugTab.Weather ? DisplayStyle.Flex : DisplayStyle.None;
            _debugFaces.style.display = tab == DebugTab.Faces ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshDebugWeather();
            RefreshDebugFaces();
        }

        VisualElement _debugFaces = null!;
        VisualElement? _debugTalkRow;
        VisualElement? _debugAutoFaceRow;
        readonly List<VisualElement> _debugFaceRows = new();

        /// <summary>
        /// Talk (design 59 §7): the chosen colonist talks with the nearest colonist within reach, or
        /// to nobody; pressed while anybody is talking, every conversation stops.
        /// </summary>
        void ToggleTalk()
        {
            var figures = _boot?.Figures;
            var world = _boot?.World;
            if (figures == null || world == null) return;
            // Only what this row started: the colony's own chatter (design 59 §5c) is not its to stop.
            if (figures.AskedConversationCount > 0)
            {
                figures.EndAskedConversations();
                RefreshDebugFaces();
                return;
            }
            PawnId speaker = DebugFacePawn(world);
            if (!speaker.IsValid) return;
            PawnId listener = Odyssey.Presentation.World.PawnFigureDirector.NearestListener(
                world.Views.Current.Pawns, speaker);
            figures.StartConversation(speaker, listener, DebugDirector.TalkSeconds);
            RefreshDebugFaces();
        }

        /// <summary>
        /// An expression row: the selected colonist holds it, or, with nobody selected, the whole
        /// colony does — so the colony can be compared at a distance. Null is the From context row,
        /// which hands the face back to what the colonist is doing and feeling.
        /// </summary>
        void SetFace(FaceExpression? expression)
        {
            var figures = _boot?.Figures;
            if (figures == null) return;
            if (_directors != null && _directors.Selection.HasPawn) figures.SetExpression(_directors.Selection.Pawn, expression);
            else figures.SetEveryoneExpression(expression);
            RefreshDebugFaces();
        }

        /// <summary>Light Talk while anybody talks, and the expression the chosen face holds.</summary>
        void RefreshDebugFaces()
        {
            if (_debugPanel == null || _debugPanel.style.display == DisplayStyle.None) return;
            var figures = _boot?.Figures;
            _debugTalkRow?.EnableInClassList("settings__row--on", figures != null && figures.AskedConversationCount > 0);
            FaceExpression? forced = figures == null ? null
                : _directors != null && _directors.Selection.HasPawn ? figures.ForcedExpressionOf(_directors.Selection.Pawn)
                : figures.EveryoneExpression;
            _debugAutoFaceRow?.EnableInClassList("settings__row--on", forced == null);
            for (int i = 0; i < _debugFaceRows.Count; i++)
                _debugFaceRows[i].EnableInClassList("settings__row--on", DebugDirector.FaceRows[i].Expression == forced);
        }

        /// <summary>
        /// Who a Faces row acts on: the selected colonist if she can talk, else the colonist nearest
        /// where the camera is looking — <see cref="DebugAnchorCell"/>'s order, so every debug row
        /// means the same thing by "this colonist".
        /// </summary>
        PawnId DebugFacePawn(Odyssey.Sim.SimWorld world)
        {
            var snapshot = world.Views.Current;
            if (_directors != null && _directors.Selection.HasPawn &&
                snapshot.TryGetPawn(_directors.Selection.Pawn, out PawnView selected) &&
                Odyssey.Presentation.World.PawnFigureDirector.CanTalk(in selected))
                return selected.Id;

            CellRef at = DebugAnchorCell(world);
            PawnId best = PawnId.None;
            int bestSq = int.MaxValue;
            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                if (!Odyssey.Presentation.World.PawnFigureDirector.CanTalk(in pawns[i])) continue;
                int dx = pawns[i].Cell.X - at.X, dz = pawns[i].Cell.Z - at.Z, dy = pawns[i].Cell.Y - at.Y;
                int sq = dx * dx + dz * dz + 4 * dy * dy;
                if (sq < bestSq) { bestSq = sq; best = pawns[i].Id; }
            }
            return best;
        }

        /// <summary>Light the preset that is set, and the particle row if it is on.</summary>
        void RefreshDebugWeather()
        {
            // The last row sent, until the sky it asked for has arrived; then the kind the sky holds.
            WeatherKind kind = _boot?.World?.Views.Current.Weather.Kind ?? WeatherKind.Clear;
            for (int i = 0; i < _debugWeatherRows.Count; i++)
                _debugWeatherRows[i].EnableInClassList("settings__row--on",
                    i == _debugWeatherSent || (_debugWeatherSent < 0 && DebugDirector.WeatherPresets[i].Kind == kind
                        && FirstRowOf(kind) == i));
            _debugParticlesRow?.EnableInClassList("settings__row--on", _directors?.Debug.RainAsParticles ?? false);
            _debugGlossRow?.EnableInClassList("settings__row--on", _directors?.Debug.WetGlossOnly ?? false);
        }

        /// <summary>The row sent last, or -1: lit until the sky moves on.</summary>
        int _debugWeatherSent = -1;

        static int FirstRowOf(WeatherKind kind) =>
            System.Array.FindIndex(DebugDirector.WeatherPresets, p => p.Kind == kind);

        /// <summary>One Weather row: the command to the simulation, landing on the next tick.</summary>
        void SetWeather(DebugDirector.WeatherPreset preset)
        {
            var world = _boot?.World;
            if (world == null) return;
            world.Intents.Submit(preset.ToIntent());
            _debugWeatherSent = System.Array.IndexOf(DebugDirector.WeatherPresets, preset);
            RefreshDebugWeather();
        }

        void OnDeveloperOverlayChanged()
        {
            if (_directors == null) return;
            _debugDeveloperRow.EnableInClassList("settings__row--on", _directors.Settings.DeveloperOverlay);
        }

        void OnDebugChanged()
        {
            bool open = _directors != null && _directors.Debug.Open;
            _debugPanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open)
            {
                ToggleMenu(false);
                _directors?.Settings.SetOpen(false);
                RefreshDebugEvents();
                // Seeded on open rather than at build: tracing is a process-wide static that a
                // previous session, or a test, may have left either way round, and a pip showing
                // the opposite of the truth is worse than no pip.
                RefreshTraceRow();
                // And the jumps switch, which a new colony has quietly turned off.
                RefreshJumpsFailRow();
            }
        }

        /// <summary>
        /// One Spawn row's intent — a pawn of a kind (design 29 §7, design 33 §1) or one weapon —
        /// aimed at the column the player is looking at. What it sends is the row's own
        /// (<see cref="DebugDirector.SpawnRow.ToIntent"/>); only the anchor is found here.
        /// </summary>
        void Spawn(DebugDirector.SpawnRow row)
        {
            var world = _boot!.World;
            if (world == null || _directors == null) return;
            Intent intent = row.ToIntent(DebugAnchorCell(world));
            // A band of three is three intents at one column; the simulation spreads each onto its
            // own tile (design 33 §9h).
            for (int i = 0; i < row.Repeat; i++) world.Intents.Submit(intent);
        }

        /// <summary>Fire one incident regardless of its gates (design 23 §3). Lands on the next tick.</summary>
        void InvokeIncident(int def)
        {
            var world = _boot!.World;
            if (world == null || _directors == null) return;
            world.Intents.Submit(new Intent(IntentKind.InvokeIncident, default, def));
        }

        /// <summary>
        /// Fire a raid of the size and mix the two controls hold (design 55 §9). The incident's own
        /// door is asked first, so a refusal is said on the row rather than only in the console: a
        /// second band of 200 beside the first does not fit under the ceiling, and a press that
        /// does nothing visible reads as a broken button. The intent is sent either way; the
        /// simulation is the judge and this is only its answer read early.
        /// </summary>
        void InvokeRaid(int def)
        {
            var world = _boot!.World;
            if (world == null || _directors == null) return;
            Intent intent = _directors.Debug.RaidIntent(def);

            var colony = _boot.Colony;
            if (colony != null && _raidNote != null)
            {
                bool fires = colony.Incidents.CanFire(new IncidentParms(def, intent.B, null, intent.C - 1));
                RaidParams? p = colony.Incidents.Content.Defs[def].raid;
                string note = fires || p == null ? string.Empty
                    : DebugDirector.RaidRefusal(RaidWorker.SizeFor(colony.Pawns, p, intent.B, world.CurrentTick),
                        RaidWorker.Room(colony.Pawns));
                _raidNote.text = note;
                _raidNote.style.display = note.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            world.Intents.Submit(intent);
        }

        /// <summary>
        /// A day a press. The day's length is read from the content rather than written here, so
        /// a retuned calendar does not leave this row skipping some other amount.
        /// </summary>
        void SkipDay()
        {
            var colony = _boot!.Colony;
            if (colony == null) return;
            _boot.DebugSkipTicks(colony.Pawns.Content.DayTicks);
        }

        /// <summary>
        /// A month a press, so the year can be walked through and the season seen (design 28).
        ///
        /// <para>The day's own length times the calendar's <c>DaysPerMonth</c>, both read rather
        /// than written, for the reason <see cref="SkipDay"/> gives: two places that hold a
        /// month's length would be one more thing to keep in step with a retuned calendar.</para>
        /// </summary>
        void SkipMonth()
        {
            var colony = _boot!.Colony;
            if (colony == null) return;
            _boot.DebugSkipTicks(colony.Pawns.Content.DayTicks * Calendar.DaysPerMonth);
        }

        void RipenCrops()
        {
            var world = _boot!.World;
            if (world == null || _directors == null) return;
            world.Intents.Submit(new Intent(IntentKind.DebugRipen, DebugAnchorCell(world)));
        }


        /// Which <em>column</em> a debug spawn or grant is aimed at. Not which cell: the shell
        /// reads snapshots and never the cell grid, so it cannot know what is standable, and
        /// <see cref="Odyssey.Sim.World.CellGrid.NearestWalkableInColumn"/> resolves the layer on
        /// the simulation's side of the seam.
        ///
        /// <para>The order is the selected colonist, then the selected cell, then the camera's
        /// own focus — each one a place the player has already said they are looking at. The
        /// camera is the row's own promise ("near the camera"); the middle of the map, which is
        /// what this used to return, is a place nobody is looking at and was usually the air
        /// above a hillside three hundred metres from the colony.</para>
        /// </summary>
        CellRef DebugAnchorCell(Odyssey.Sim.SimWorld world)
        {
            int layer = _directors?.Slice.ActiveLayer ?? 0;

            if (_directors != null && _directors.Selection.HasPawn &&
                world.Views.Current.TryGetPawn(_directors.Selection.Pawn, out PawnView selectedPawn))
                return selectedPawn.Cell;

            if (_directors?.Selection.Cell is { } selectedCell) return selectedCell;

            int x = world.Size.SizeX / 2;
            int z = world.Size.SizeZ / 2;
            if (_rig != null)
            {
                Vector3 focus = _rig.Focus;
                x = Mathf.Clamp(Mathf.FloorToInt(focus.x / CellMetrics.SizeXZ), 0, world.Size.SizeX - 1);
                z = Mathf.Clamp(Mathf.FloorToInt(focus.z / CellMetrics.SizeXZ), 0, world.Size.SizeZ - 1);
            }
            return new CellRef(x, z, Mathf.Clamp(layer, 0, world.Size.SizeY - 1));
        }

    }
}
