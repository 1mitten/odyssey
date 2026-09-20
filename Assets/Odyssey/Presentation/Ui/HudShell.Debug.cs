#nullable enable
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The debug menu: backtick's own panel since 2026-09-17, replacing what used to be a direct
    /// toggle of the developer overlay. Built the same way <c>HudShell.Bar.cs</c> builds Settings —
    /// a <see cref="Window"/>, <c>.settings__row</c>s, the same pip and disabled-row idioms — so a
    /// second panel in this shell does not invent a second visual language.
    ///
    /// <para><b>Not a modal.</b> Like Settings, there is no scrim: the world keeps running while it
    /// is open, because a debug menu that froze the game to use it would defeat most of what it is
    /// for.</para>
    ///
    /// <para><b>What it holds today.</b> The developer-overlay toggle, moved here wholesale from
    /// Settings' Interface tab rather than duplicated (<c>EveryLiveToolIsDrawnSomewhere</c> — a
    /// control drawn in two places has already cost this project twice). Two cheats that wrap sim
    /// APIs that already exist and touch no new mechanics: spawn a colonist, and give a fixed amount
    /// of a common resource. And "Invoke event", which fires the supply drop (design 23) through
    /// the same door a storyteller will use: the row that stood disabled for three days while the
    /// event system it needed was a feature and not a debug shortcut.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        VisualElement _debugDeveloperRow = null!;

        void BuildDebug()
        {
            _debugPanel = Window("debug", Registry.Label(DebugPanelKey),
                () => _directors?.Debug.SetOpen(false), "debug");

            _debugDeveloperRow = DebugToggleRow(SettingsDirector.DeveloperKey,
                "The frame-time readout. Also the ` key, and kept between sessions",
                () => _directors?.Settings.SetDeveloperOverlay(!_directors.Settings.DeveloperOverlay));
            _debugPanel.Add(_debugDeveloperRow);

            _debugPanel.Add(DebugActionRow(SpawnPawnKey,
                "Adds a colonist near the camera, with no scenario and no starting kit",
                SpawnPawn));
            _debugPanel.Add(DebugActionRow(GiveWoodKey, "Adds 50 wood near the camera",
                () => GiveResource(ItemIndex.Wood)));
            _debugPanel.Add(DebugActionRow(GiveStoneKey, "Adds 50 stone near the camera",
                () => GiveResource(ItemIndex.Stone)));
            _debugPanel.Add(DebugActionRow(GiveFoodKey, "Adds 50 meals near the camera",
                () => GiveResource(ItemIndex.Meal)));

            // The one incident there is, fired regardless of its gates (design 23 §3). It lands
            // on the next tick, like the two grants above, so a click while paused shows nothing
            // until the clock runs; the tooltip says so.
            _debugPanel.Add(DebugActionRow(InvokeEventKey,
                "Drops a stack of meals from the sky somewhere on the board. Lands on the next tick, so unpause to see it",
                InvokeSupplyDrop));

            _hud.Add(_debugPanel);
        }

        /// <summary>A toggle row: the pip idiom Settings already uses for the graphics options and
        /// used to use for this exact row, before it moved here.</summary>
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
        /// visible but inert, such as the placeholder event row.</summary>
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
            }
        }

        void SpawnPawn()
        {
            var world = _boot!.World;
            if (world == null || _directors == null) return;
            world.Intents.Submit(new Intent(IntentKind.SpawnPawn, DebugAnchorCell(world)));
        }

        void InvokeSupplyDrop()
        {
            var world = _boot!.World;
            if (world == null || _directors == null) return;
            world.Intents.Submit(new Intent(IntentKind.InvokeIncident, default, IncidentHandle.SupplyDrop));
        }

        void GiveResource(int itemIndex)
        {
            var world = _boot!.World;
            if (world == null || _directors == null) return;
            world.Intents.Submit(new Intent(IntentKind.GiveResource, DebugAnchorCell(world),
                itemIndex, DebugGiveAmount));
        }

        /// <summary>
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

        const string DebugPanelKey = "ui.debug.panel";
        const string SpawnPawnKey = "ui.debug.spawnpawn";
        const string GiveWoodKey = "ui.debug.givewood";
        const string GiveStoneKey = "ui.debug.givestone";
        const string GiveFoodKey = "ui.debug.givefood";
        const string InvokeEventKey = "ui.debug.invokeevent";
    }
}
