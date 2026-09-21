#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
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
    /// <para><b>Two tabs</b> (owner, 2026-09-20). <b>Cheats</b>: the developer-overlay toggle,
    /// moved here wholesale from Settings' Interface tab rather than duplicated
    /// (<c>EveryLiveToolIsDrawnSomewhere</c>), and the grants that wrap sim APIs that already exist.
    /// <b>Events</b>: one row per incident the content declares, each fired through the same door
    /// a storyteller will use (design 23 §3), built from the open colony's content when the panel
    /// opens so a second Def appears by existing.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        VisualElement _debugDeveloperRow = null!;
        VisualElement _debugCheats = null!;
        VisualElement _debugEvents = null!;
        readonly Dictionary<DebugTab, Label> _debugTabs = new();

        /// <summary>The content the event rows were last built from, so a new colony rebuilds them and a reopen does not.</summary>
        IncidentContent? _debugEventsFrom;

        void BuildDebug()
        {
            _debugPanel = Window("debug", Registry.Label(DebugDirector.PanelKey),
                () => _directors?.Debug.SetOpen(false), "debug");

            // The tab strip, in Settings' idiom: nothing new is invented for a third use of it.
            var tabs = new VisualElement();
            tabs.AddToClassList("settings__tabs");
            foreach (DebugTab tab in new[] { DebugTab.Cheats, DebugTab.Events })
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
            _debugCheats.Add(DebugActionRow(DebugDirector.SpawnPawnKey,
                "Adds a colonist near the camera, with no scenario and no starting kit",
                SpawnPawn));
            _debugCheats.Add(DebugActionRow(DebugDirector.GiveWoodKey, "Adds 50 wood near the camera",
                () => GiveResource(ItemIndex.Wood)));
            _debugCheats.Add(DebugActionRow(DebugDirector.GiveStoneKey, "Adds 50 stone near the camera",
                () => GiveResource(ItemIndex.Stone)));
            _debugCheats.Add(DebugActionRow(DebugDirector.GiveFoodKey, "Adds 50 meals near the camera",
                () => GiveResource(ItemIndex.Meal)));
            _debugCheats.Add(DebugActionRow(DebugDirector.SkipDayKey,
                "Spends one whole game day of ticks at once (about a fifth of a second). "
                    + "The crop's stage changes arrive at the same hour each press; works while paused",
                SkipDay));
            _debugCheats.Add(DebugActionRow(DebugDirector.SkipMorningKey,
                "Skips the night and hands back the clock at dawn, with a whole watchable day "
                    + "ahead: the harvest happens on screen, not inside the skip",
                () => _boot!.DebugSkipToMorning()));
            _debugCheats.Add(DebugActionRow(DebugDirector.RipenCropsKey,
                "Brings every standing crop to ripeness at once, daylight window and all - "
                    + "the harvest half without the four-day wait",
                RipenCrops));
            _debugCheats.Add(DebugActionRow(DebugDirector.MarkTraceKey,
                "Writes a marker into this session's performance trace, so the seconds around "
                    + "this moment can be found afterwards - press it when something felt wrong",
                MarkTrace));
            _debugPanel.Add(_debugCheats);

            // Filled when the panel opens, from the colony that is open: the content is the
            // colony's, and there is no colony when the shell is built.
            _debugEvents = new VisualElement();
            _debugEvents.AddToClassList("settings__body");
            _debugPanel.Add(_debugEvents);

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
                int def = i;
                _debugEvents.Add(DebugActionRow(IncidentLabels.IconKey(i),
                    content.Defs[i].description ?? string.Empty, () => InvokeIncident(def)));
            }
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
            }
        }

        void SpawnPawn()
        {
            var world = _boot!.World;
            if (world == null || _directors == null) return;
            world.Intents.Submit(new Intent(IntentKind.SpawnPawn, DebugAnchorCell(world)));
        }

        /// <summary>Fire one incident regardless of its gates (design 23 §3). Lands on the next tick.</summary>
        void InvokeIncident(int def)
        {
            var world = _boot!.World;
            if (world == null || _directors == null) return;
            world.Intents.Submit(new Intent(IntentKind.InvokeIncident, default, def));
        }

        void GiveResource(int itemIndex)
        {
            var world = _boot!.World;
            if (world == null || _directors == null) return;
            world.Intents.Submit(new Intent(IntentKind.GiveResource, DebugAnchorCell(world),
                itemIndex, DebugGiveAmount));
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

        const int DebugGiveAmount = 50;
    }
}
