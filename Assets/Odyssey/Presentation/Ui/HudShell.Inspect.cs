#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Sim.Contracts;
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

        void BuildRail()
        {
            VisualElement rail = Panel("rail", "rail");
            Header(rail, "Depth", out _);

            _railCells = new VisualElement();
            _railCells.AddToClassList("rail__cells");
            rail.Add(_railCells);

            _railHint = HudText.Make("R / F", HudTextRole.Meta, numeric: false, "rail__hint");
            _railHint.tooltip = "R and F move the slice up and down. Home recentres.";
            rail.Add(_railHint);

            _hud.Add(rail);
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
            _hud.Add(_inspectPanel);
        }

        void RefreshInspect()
        {
            var world = _boot!.World;
            if (world == null || _inspectBody == null) return;
            _inspect.Refresh(world.Views.Current);

            // The structure is rebuilt only when the subject changes; values update in place, so a
            // refresh allocates nothing but the few strings it shows.
            string signature =
                _inspect.Subject + ":" +
                (_inspect.Subject == InspectSubject.Colonist ? _inspect.Pawn.ToString()
                 : _inspect.Subject == InspectSubject.Item ? _inspect.Thing.ToString()
                 : _inspect.Position);
            if (signature != _inspectBuiltFor)
            {
                BuildInspectBody();
                _inspectBuiltFor = signature;
            }

            if (_inspect.Subject == InspectSubject.None) return;

            _inspectPanel.EnableInClassList("inspect--tomb", _inspect.Tombstoned);
            _tombReason.style.display = _inspect.Tombstoned ? DisplayStyle.Flex : DisplayStyle.None;

            HudText.Set(_inspectTitle, _inspect.Title, HudTextRole.Name);

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
            }
            if (!ReferenceEquals(_stateJob, _inspect.Job) || !ReferenceEquals(_stateBand, band) ||
                _stateSelected != selected)
            {
                _stateJob = _inspect.Job;
                _stateBand = band;
                _stateSelected = selected;
                HudText.Set(_inspectState, StateLine(), HudTextRole.Meta);
            }

            if (_inspect.Subject != InspectSubject.Colonist || _inspect.Tombstoned) return;

            SetNeed(0, _inspect.Food);
            SetNeed(1, _inspect.Rest);
            SetNeed(2, _inspect.Mood);
        }

        void SetNeed(int index, int thousandths)
        {
            if (index >= _needs.Count) return;
            NeedView view = _needs[index];

            float percent = Percent(thousandths);
            view.Fill.style.width = Length.Percent(percent);
            Color band = HudTokens.NeedBand(thousandths);
            view.Fill.style.backgroundColor = band;
            view.Swatch.style.backgroundColor = band;

            // A need moves by fractions of a per cent between refreshes, so the label is rebuilt
            // only when the whole number it prints has actually changed.
            int whole = Mathf.RoundToInt(percent);
            if (view.LastPercent == whole) return;
            view.LastPercent = whole;
            HudText.Set(view.Value, whole.ToString("0") + "%", HudTextRole.Meta);
        }

        /// <summary>The line beside the name: what it is, which layer, where.</summary>
        string MetaLine() => _inspect.Layer >= 0
            ? $"{_inspect.Subtitle} · L{_inspect.Layer} · {Coordinates()}"
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
                    return "item on the ground";
                case InspectSubject.Cell:
                    return "cell readout arrives with cell inspection";
                default:
                    return string.Empty;
            }
        }

        void BuildInspectBody()
        {
            _inspectBody.Clear();
            _needs.Clear();
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

            // ---- header: avatar, name and its two lines, then the actions on the right
            var header = new VisualElement();
            header.AddToClassList("inspect__hdr");

            _inspectAvatar = new IconBadge(
                _inspect.Subject == InspectSubject.Colonist ? "ui.pawn.colonist"
                : _inspect.Subject == InspectSubject.Item ? "ui.res.meal"
                : "ui.overlay.zones",
                IconBadge.AvatarSize);
            _inspectAvatar.Inherit(HudTokens.TextPrimary);
            header.Add(_inspectAvatar);

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
            if (_inspect.Subject == InspectSubject.Colonist)
                foreach (InspectCommand command in _inspect.Commands)
                {
                    // Two of the three: the pane's header carries the commands a player reaches
                    // for, and Inspect is not one of them when the pane is already open.
                    if (command.Label == "Inspect") continue;
                    actions.Add(ActionButton(command));
                }

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
                foreach (InspectTab tab in _inspect.Tabs)
                {
                    Label chip = HudText.Make(tab.Name, HudTextRole.Body, ussClass: "tab");
                    chip.EnableInClassList("tab--on", tab.Enabled);
                    chip.EnableInClassList("tab--off", !tab.Enabled);
                    chip.tooltip = tab.Enabled ? "Needs" : tab.Name + " — " + tab.Reason;
                    strip.Add(chip);
                }
                _inspectBody.Add(strip);

                var grid = new VisualElement();
                grid.AddToClassList("needs");
                _needs.Add(Need(grid, "Food"));
                _needs.Add(Need(grid, "Rest"));
                _needs.Add(Need(grid, "Mood"));
                _needRows = (_needs.Count + 1) / 2;
                _inspectBody.Add(grid);
            }

            _tombReason = HudText.Make("no longer present — the pane keeps last-known values",
                HudTextRole.Meta, ussClass: "inspect__reason");
            _tombReason.style.display = DisplayStyle.None;
            _inspectBody.Add(_tombReason);
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

        static NeedView Need(VisualElement grid, string name)
        {
            var view = new NeedView();

            view.Root = new VisualElement();
            view.Root.AddToClassList("need");

            var line = new VisualElement();
            line.AddToClassList("need__line");
            view.Swatch = new VisualElement();
            view.Swatch.AddToClassList("need__swatch");
            view.Name = HudText.Make(name, HudTextRole.Body, ussClass: "need__name");
            view.Value = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true, "need__value");
            line.Add(view.Swatch);
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
    }
}
