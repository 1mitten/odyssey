#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The Work tab: every colonist against every work type, one small cell per pair carrying four
    /// independent signals. <c>docs/design/27-work-tab.md</c> holds the decisions and the
    /// arithmetic; <c>docs/reference/mockups/work-v1.html</c> is what it should look like.
    ///
    /// <para><b>Built the way <see cref="BuildDebug"/> builds the debug menu</b> — a
    /// <see cref="Window"/>, the existing tab chips for the mode switch, the same close X — so a
    /// fourth panel in this shell does not invent a fourth visual language.</para>
    ///
    /// <para><b>Not a modal.</b> The world keeps running behind it, like Settings and the debug
    /// menu. A player opens this to decide who does what next, and freezing the colony to do it
    /// would make the decision harder rather than easier. Every click lands while paused too:
    /// <c>SetWorkPriority</c> is in <see cref="PausedIntents"/>.</para>
    ///
    /// <para><b>Why almost every style here is inline rather than in <c>Hud.uss</c>.</b> Half of
    /// this panel's appearance <i>is</i> data — the border is the skill band, the ink is the
    /// priority, the fill is whether the cell is assigned — so it could never live in a
    /// stylesheet. Writing the other half there would split one cell's appearance across two
    /// files. The colours are still never literals: they come from <see cref="WorkBands"/> and
    /// <see cref="HudTokens"/>, which is the rule that actually matters.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        readonly WorkGridModel _work = new WorkGridModel();

        VisualElement _workPanel = null!;
        Label _workSubtitle = null!;
        VisualElement _workLeftRows = null!;
        VisualElement _workGridRows = null!;
        VisualElement _workLegend = null!;
        readonly Dictionary<WorkGridMode, Label> _workModeChips = new();
        readonly List<WorkRowView> _workRows = new List<WorkRowView>();

        /// <summary>
        /// The roster the rows were last built from. Rebuilding twenty-two cells per colonist on
        /// every refresh would allocate for as long as the panel is open, which is ADR 0003's flip
        /// condition F1; the rows are rebuilt only when <em>who</em> is in the colony changes.
        /// </summary>
        readonly List<PawnId> _workBuiltFor = new List<PawnId>();

        /// <summary>One colonist's row: the frozen half, the cells, and the portrait to fill.</summary>
        sealed class WorkRowView
        {
            public PawnId Id;
            public VisualElement Left = null!;
            public VisualElement Cells = null!;
            public AvatarGlyph Avatar = null!;
            public Label Name = null!;
            public readonly List<WorkCellView> Boxes = new List<WorkCellView>();
        }

        /// <summary>One cell: the box that carries the border and fill, the glyph, and the flames.</summary>
        sealed class WorkCellView
        {
            public VisualElement Box = null!;
            public Label Glyph = null!;
            public VisualElement Flames = null!;
            public int Column;
        }

        // ============================================================ build

        void BuildWork()
        {
            _workPanel = Window("work", Registry.Label(WorkDirector.PanelKey),
                () => _directors?.Work.SetOpen(false), "work");

            // Docked bottom-left, sitting on the command bar. The mockup asked for it flush with
            // no gap and no left or bottom border; we keep our own panel frame, because the one
            // panel in the game shaped differently from the others reads as a bug rather than as
            // a decision (design 27 §9).
            _workPanel.style.position = Position.Absolute;
            _workPanel.style.left = HudLayout.Edge;
            _workPanel.style.bottom = HudCommands.ItemHeight + HudCommands.BarPad * 2;
            _workPanel.style.width = WorkGridLayout.WidthFor(WorkGridModel.Columns.Count) + 2;
            _workPanel.style.maxHeight = Length.Percent(80);

            BuildWorkHeaderExtras();

            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.overflow = Overflow.Hidden;
            grid.style.flexShrink = 1;

            // The frozen column: outside the scroller, so it stays put when the grid scrolls.
            var left = new VisualElement();
            left.style.width = WorkGridLayout.LeftColumn;
            left.style.flexShrink = 0;
            left.style.borderRightWidth = HudTheme.BorderWidth;
            left.style.borderRightColor = HudTokens.PanelBorder;

            Label colonist = HudText.Make("Colonist", HudTextRole.PanelLabel);
            colonist.style.height = WorkGridLayout.HeaderBand;
            colonist.style.unityTextAlign = TextAnchor.LowerLeft;
            colonist.style.paddingLeft = WorkGridLayout.LeftPad;
            colonist.style.paddingBottom = 7;
            left.Add(colonist);

            _workLeftRows = new VisualElement();
            left.Add(_workLeftRows);
            grid.Add(left);

            var scroller = new ScrollView(ScrollViewMode.Horizontal);
            scroller.style.flexGrow = 1;
            var cols = new VisualElement();
            cols.Add(BuildWorkHeaderBand());
            _workGridRows = new VisualElement();
            cols.Add(_workGridRows);
            scroller.Add(cols);
            grid.Add(scroller);

            _workPanel.Add(grid);

            _workLegend = new VisualElement();
            _workLegend.style.flexDirection = FlexDirection.Row;
            _workLegend.style.flexWrap = Wrap.Wrap;
            _workLegend.style.alignItems = Align.Center;
            _workLegend.style.paddingLeft = WorkGridLayout.LeftPad;
            _workLegend.style.paddingRight = WorkGridLayout.LeftPad;
            _workLegend.style.paddingTop = 6;
            _workLegend.style.paddingBottom = 6;
            _workLegend.style.borderTopWidth = HudTheme.BorderWidth;
            _workLegend.style.borderTopColor = HudTokens.PanelBorder;
            _workPanel.Add(_workLegend);

            RefreshWorkLegend();
            _hud.Add(_workPanel);
        }

        /// <summary>The subtitle, the word "Priorities", the two-state switch and nothing else.
        /// They ride the window's own header row, beside the close X that is already there.</summary>
        void BuildWorkHeaderExtras()
        {
            VisualElement header = _workPanel.Q(className: "panel__hdr");
            if (header == null) return;

            _workSubtitle = HudText.Make(string.Empty, HudTextRole.Meta);
            _workSubtitle.style.marginLeft = 10;
            // Before the spacer the close button already inserted, so it sits beside the title.
            header.Insert(1, _workSubtitle);

            Label priorities = HudText.Make("Priorities", HudTextRole.Meta);
            priorities.style.marginRight = 8;
            header.Insert(header.childCount - 1, priorities);

            var seg = new VisualElement();
            seg.AddToClassList("settings__tabs");
            seg.style.marginRight = 8;
            foreach (WorkGridMode mode in new[] { WorkGridMode.Simple, WorkGridMode.Detailed })
            {
                Label chip = HudText.Make(mode == WorkGridMode.Simple ? "Simple" : "Detailed",
                    HudTextRole.Body, ussClass: "tab");
                WorkGridMode captured = mode;
                chip.RegisterCallback<ClickEvent>(_ => _directors?.Work.SetMode(captured));
                _workModeChips[mode] = chip;
                seg.Add(chip);
            }
            header.Insert(header.childCount - 1, seg);

            // The chips only. Not OnWorkModeChanged: that redraws the legend, which does not
            // exist until BuildWork has finished with this.
            SetWorkModeChips(WorkGridMode.Detailed);
        }

        void SetWorkModeChips(WorkGridMode mode)
        {
            foreach (var entry in _workModeChips)
            {
                bool on = entry.Key == mode;
                entry.Value.EnableInClassList("tab--on", on);
                entry.Value.EnableInClassList("tab--off", !on);
            }
        }

        /// <summary>
        /// The header band: twenty-two boxes, each a rotated label above an icon tile.
        ///
        /// <para>Built once — the columns are content, not colony state, so nothing about them
        /// changes while the game runs.</para>
        /// </summary>
        VisualElement BuildWorkHeaderBand()
        {
            var band = new VisualElement();
            band.style.flexDirection = FlexDirection.Row;
            band.style.height = WorkGridLayout.HeaderBand;

            for (int c = 0; c < WorkGridModel.Columns.Count; c++)
            {
                WorkCatalogue.Entry entry = WorkGridModel.Columns[c];

                var head = new VisualElement();
                head.style.width = WorkGridLayout.Pitch;
                head.style.flexShrink = 0;
                head.style.height = WorkGridLayout.HeaderBand;
                head.tooltip = entry.Live ? entry.Label : entry.Label + " — " + entry.Reason;

                // An unbuilt column is washed top to bottom so it reads as one absent thing
                // rather than as ten separately broken cells (design 27 §4a).
                if (!entry.Live) head.style.backgroundColor = HudTokens.Convert(WorkBands.UnbuiltWash);

                Label label = HudText.Make(entry.Label, HudTextRole.Body);
                label.style.position = Position.Absolute;
                label.style.left = Length.Percent(50);
                label.style.bottom = WorkGridLayout.IconTile + WorkGridLayout.LabelGap;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                label.style.color = entry.Live ? HudTokens.TextMeta : HudTokens.TextFaint;
                // Anchored bottom-left and turned, so the labels fan up to the left and each one
                // clears its neighbour by the arithmetic in WorkGridLayout — which the fast tier
                // asserts, because this is the fault that is invisible in a screenshot of the
                // right-hand columns.
                label.style.transformOrigin =
                    new TransformOrigin(Length.Percent(0), Length.Percent(100));
                label.style.rotate = new Rotate(
                    new Angle(WorkGridLayout.LabelAngleDegrees, AngleUnit.Degree));
                head.Add(label);

                var tile = new VisualElement();
                tile.style.position = Position.Absolute;
                tile.style.bottom = 2;
                tile.style.left = (WorkGridLayout.Pitch - WorkGridLayout.IconTile) / 2f;
                tile.style.width = WorkGridLayout.IconTile;
                tile.style.height = WorkGridLayout.IconTile;
                tile.style.alignItems = Align.Center;
                tile.style.justifyContent = Justify.Center;
                var icon = new IconBadge(entry.Key, IconBadge.RowSize, categorised: true);
                if (!entry.Live) icon.Inherit(HudTokens.TextFaint);
                tile.Add(icon);
                head.Add(tile);

                band.Add(head);
            }
            return band;
        }

        // ============================================================ refresh

        void RefreshWork()
        {
            if (_directors == null || !_directors.Work.Open) return;
            var world = _boot?.World;
            if (world == null) return;

            WorldSnapshot frame = world.Views.Current;
            _work.Mode = _directors.Work.Mode;
            _work.Refresh(frame, _roster.CustomOrder, _directors.Selection.Pawns);

            HudText.Set(_workSubtitle, _work.Subtitle(), HudTextRole.Meta);

            if (RosterChanged()) RebuildWorkRows();

            for (int r = 0; r < _workRows.Count && r < _work.Rows.Count; r++)
            {
                WorkRowView view = _workRows[r];
                WorkRow row = _work.Rows[r];

                HudText.Set(view.Name, row.Name, HudTextRole.Row);
                view.Left.style.backgroundColor = row.Selected
                    ? HudTokens.Convert(HudTheme.SelectedRing)
                    : Color.clear;
                view.Avatar.SetFace(ColonistFace.Of(frame, row.Id));
                if (_boot != null) view.Avatar.SetPortrait(_boot.Portraits.For(frame, row.Id));

                for (int c = 0; c < view.Boxes.Count && c < row.Cells.Count; c++)
                    PaintWorkCell(view.Boxes[c], row.Cells[c]);
            }
        }

        /// <summary>Whether the colony is a different set of people from the one the rows were
        /// built for. Order counts: the strip can be dragged into a new order and this follows.</summary>
        bool RosterChanged()
        {
            if (_workBuiltFor.Count != _work.Rows.Count) return true;
            for (int i = 0; i < _workBuiltFor.Count; i++)
                if (_workBuiltFor[i] != _work.Rows[i].Id) return true;
            return false;
        }

        void RebuildWorkRows()
        {
            _workLeftRows.Clear();
            _workGridRows.Clear();
            _workRows.Clear();
            _workBuiltFor.Clear();

            for (int r = 0; r < _work.Rows.Count; r++)
            {
                WorkRow row = _work.Rows[r];
                var view = new WorkRowView { Id = row.Id };
                _workBuiltFor.Add(row.Id);

                view.Left = new VisualElement();
                view.Left.style.flexDirection = FlexDirection.Row;
                view.Left.style.alignItems = Align.Center;
                view.Left.style.height = WorkGridLayout.RowHeight;
                view.Left.style.paddingLeft = WorkGridLayout.LeftPad;
                view.Left.style.borderTopWidth = HudTheme.BorderWidth;
                view.Left.style.borderTopColor = HudTokens.Divider;

                view.Avatar = new AvatarGlyph(WorkGridLayout.Portrait);
                view.Left.Add(view.Avatar);

                view.Name = HudText.Make(row.Name, HudTextRole.Row);
                view.Name.style.marginLeft = WorkGridLayout.PortraitGap;
                view.Name.style.width = WorkGridLayout.NameBudget;
                view.Name.style.overflow = Overflow.Hidden;
                view.Name.style.whiteSpace = WhiteSpace.NoWrap;
                view.Left.Add(view.Name);

                // A row is a colonist, so clicking one goes to them — the same path the roster
                // card takes, rather than a second answer to "select this person".
                PawnId captured = row.Id;
                view.Left.RegisterCallback<ClickEvent>(_ => ChooseWorkColonist(captured));
                _workLeftRows.Add(view.Left);

                view.Cells = new VisualElement();
                view.Cells.style.flexDirection = FlexDirection.Row;
                view.Cells.style.height = WorkGridLayout.RowHeight;
                view.Cells.style.borderTopWidth = HudTheme.BorderWidth;
                view.Cells.style.borderTopColor = HudTokens.Divider;

                for (int c = 0; c < row.Cells.Count; c++)
                    view.Cells.Add(BuildWorkCell(view, r, c));

                _workGridRows.Add(view.Cells);
                _workRows.Add(view);
            }
        }

        VisualElement BuildWorkCell(WorkRowView view, int rowIndex, int column)
        {
            var slot = new VisualElement();
            slot.style.width = WorkGridLayout.Pitch;
            slot.style.height = WorkGridLayout.RowHeight;
            slot.style.flexShrink = 0;
            slot.style.alignItems = Align.Center;
            slot.style.justifyContent = Justify.Center;

            if (!WorkGridModel.Columns[column].Live)
                slot.style.backgroundColor = HudTokens.Convert(WorkBands.UnbuiltWash);

            var box = new VisualElement();
            box.style.width = WorkGridLayout.Cell;
            box.style.height = WorkGridLayout.Cell;
            box.style.alignItems = Align.Center;
            box.style.justifyContent = Justify.Center;
            box.style.borderTopLeftRadius = HudTheme.ChipRadius;
            box.style.borderTopRightRadius = HudTheme.ChipRadius;
            box.style.borderBottomLeftRadius = HudTheme.ChipRadius;
            box.style.borderBottomRightRadius = HudTheme.ChipRadius;

            Label glyph = HudText.Make(string.Empty, HudTextRole.Name, numeric: true);
            box.Add(glyph);

            var flames = new VisualElement();
            flames.style.position = Position.Absolute;
            flames.style.top = 1;
            flames.style.right = 1;
            flames.style.flexDirection = FlexDirection.Row;
            flames.pickingMode = PickingMode.Ignore;
            box.Add(flames);

            var cellView = new WorkCellView { Box = box, Glyph = glyph, Flames = flames, Column = column };
            view.Boxes.Add(cellView);

            int capturedRow = rowIndex;
            int capturedCol = column;
            box.RegisterCallback<PointerDownEvent>(evt => OnWorkCellPressed(evt, capturedRow, capturedCol));

            slot.Add(box);
            return slot;
        }

        /// <summary>
        /// Paint one cell from the model. <b>Everything it draws, the model has already decided</b>
        /// — an incapable cell arrives with no priority and no passion to hide, which is design 27
        /// §6.1's rule and the reason it is enforced there rather than here.
        /// </summary>
        void PaintWorkCell(WorkCellView view, in WorkCell cell)
        {
            HudText.Set(view.Glyph, cell.Glyph(_work.Mode), HudTextRole.Name);
            view.Flames.Clear();

            if (!cell.Built)
            {
                view.Box.style.backgroundColor = Color.clear;
                SetBorder(view.Box, 0, Color.clear);
                return;
            }

            if (!cell.Capable)
            {
                view.Box.style.backgroundColor = HudTokens.Convert(WorkBands.IncapableFill);
                SetBorder(view.Box, HudTheme.BorderWidth, HudTokens.Convert(WorkBands.IncapableBorder));
                view.Glyph.style.color = HudTokens.Convert(WorkBands.IncapableInk);
                return;
            }

            view.Box.style.backgroundColor = HudTokens.Convert(
                cell.Priority > WorkGridModel.Never ? WorkBands.AssignedFill : WorkBands.BlankFill);

            // The border is the only carrier of skill, and hauling has none, so it takes a hairline
            // instead of a ramp colour rather than being given one it has not earned (§6.2).
            bool ramped = cell.Band != ProficiencyBand.None;
            SetBorder(view.Box,
                ramped ? WorkBands.BorderWidth : WorkBands.NoSkillBorderWidth,
                HudTokens.Convert(WorkBands.ColourOf(cell.Band)));

            view.Glyph.style.color = _work.Mode == WorkGridMode.Simple
                ? HudTokens.Convert(cell.Priority > WorkGridModel.Never ? WorkBands.WillDo : WorkBands.WontDo)
                : HudTokens.Convert(WorkBands.InkOf(cell.Priority));

            for (int f = 0; f < cell.Passion; f++) view.Flames.Add(WorkFlame());
        }

        /// <summary>A passion flame: two of them means the job is loved, one that it is liked.
        /// Deliberately independent of the border — a colonist can love a job they are bad at.</summary>
        static VisualElement WorkFlame()
        {
            var flame = new HudGlyph(HudGlyphKind.Flame, WorkBands.FlameSize,
                HudTokens.Convert(WorkBands.Flame));
            flame.style.marginLeft = 1;
            return flame;
        }

        static void SetBorder(VisualElement box, float width, Color colour)
        {
            box.style.borderTopWidth = width;
            box.style.borderRightWidth = width;
            box.style.borderBottomWidth = width;
            box.style.borderLeftWidth = width;
            box.style.borderTopColor = colour;
            box.style.borderRightColor = colour;
            box.style.borderBottomColor = colour;
            box.style.borderLeftColor = colour;
        }

        // ============================================================ the gestures

        /// <summary>
        /// Left cycles forward, right cycles back, shift sets the whole column.
        ///
        /// <para>The backward cycle exists because a five-state ring you can only walk one way is
        /// four clicks to undo one mistake. Shift is the catalogue's own "shift-click sets a
        /// column" (§B2).</para>
        /// </summary>
        void OnWorkCellPressed(PointerDownEvent evt, int row, int column)
        {
            if (_directors == null || row >= _work.Rows.Count) return;
            WorkCell cell = _work.Rows[row].Cells[column];
            if (!cell.Interactive) return;

            evt.StopPropagation();

            int next = evt.button == 1 ? _work.CycleBack(cell.Priority) : _work.Cycle(cell.Priority);
            int handle = WorkGridModel.Columns[column].Handle;

            if (evt.shiftKey)
            {
                for (int r = 0; r < _work.Rows.Count; r++)
                    if (_work.Rows[r].Cells[column].Interactive)
                        Submit(WorkGridModel.SetPriority(_work.Rows[r].Id, handle, next));
            }
            else
            {
                Submit(WorkGridModel.SetPriority(_work.Rows[row].Id, handle, next));
            }

            // Repaint now rather than waiting out the refresh bucket: a grid that answers a click
            // a quarter of a second later reads as a grid that missed it.
            RefreshWork();
        }

        void Submit(Intent intent) => _boot?.World?.Intents.Submit(intent);

        void ChooseWorkColonist(PawnId id)
        {
            var world = _boot?.World;
            if (world == null || _directors == null) return;
            _directors.ChooseColonist(id, world.Views.Current);
        }

        // ============================================================ the legend

        /// <summary>
        /// The five swatches, the two flame states, the incapable box and the footnote — and in
        /// Simple the first group becomes the tick and the cross, plus the sentence saying the
        /// other two signals have not gone anywhere, which is the whole risk of a simple mode.
        /// </summary>
        void RefreshWorkLegend()
        {
            _workLegend.Clear();

            if (_work.Mode == WorkGridMode.Detailed)
            {
                AddLegendSwatch(WorkBands.Novice, "0–3", WorkBands.BorderWidth);
                AddLegendSwatch(WorkBands.Apprentice, "4–6", WorkBands.BorderWidth);
                AddLegendSwatch(WorkBands.Competent, "7–10", WorkBands.BorderWidth);
                AddLegendSwatch(WorkBands.Skilled, "11–14", WorkBands.BorderWidth);
                AddLegendSwatch(WorkBands.Master, "15–20", WorkBands.BorderWidth);
            }
            else
            {
                AddLegendGlyph("✓", WorkBands.WillDo, "will do");
                AddLegendGlyph("✕", WorkBands.WontDo, "won't do");
            }

            AddLegendFlames(1, "interested");
            AddLegendFlames(2, "passion");
            AddLegendSwatch(WorkBands.IncapableBorder, "incapable", HudTheme.BorderWidth);

            Label note = HudText.Make(_work.Mode == WorkGridMode.Simple
                    ? "border colour still shows skill · flames still show passion"
                    : "hauling has no skill, so no border and no flame",
                HudTextRole.Meta);
            note.style.color = HudTokens.TextDim;
            note.style.marginLeft = 6;
            _workLegend.Add(note);

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            _workLegend.Add(spacer);

            Label foot = HudText.Make(_work.Mode == WorkGridMode.Simple
                    ? "a tick is priority 3 — the default"
                    : "1 highest · 4 lowest · blank means never",
                HudTextRole.Meta);
            foot.style.color = HudTokens.TextDim;
            _workLegend.Add(foot);
        }

        VisualElement LegendGroup()
        {
            var group = new VisualElement();
            group.style.flexDirection = FlexDirection.Row;
            group.style.alignItems = Align.Center;
            group.style.marginRight = 12;
            _workLegend.Add(group);
            return group;
        }

        void AddLegendSwatch(HudColour border, string text, float width)
        {
            VisualElement group = LegendGroup();
            var box = new VisualElement();
            box.style.width = 15;
            box.style.height = 15;
            box.style.marginRight = 6;
            box.style.backgroundColor = HudTokens.Convert(WorkBands.AssignedFill);
            SetBorder(box, width, HudTokens.Convert(border));
            group.Add(box);
            group.Add(HudText.Make(text, HudTextRole.Meta));
        }

        void AddLegendGlyph(string glyph, HudColour ink, string text)
        {
            VisualElement group = LegendGroup();
            Label label = HudText.Make(glyph, HudTextRole.Body);
            label.style.width = 15;
            label.style.marginRight = 6;
            label.style.color = HudTokens.Convert(ink);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            group.Add(label);
            group.Add(HudText.Make(text, HudTextRole.Meta));
        }

        void AddLegendFlames(int count, string text)
        {
            VisualElement group = LegendGroup();
            var holder = new VisualElement();
            holder.style.flexDirection = FlexDirection.Row;
            holder.style.marginRight = 6;
            for (int i = 0; i < count; i++) holder.Add(WorkFlame());
            group.Add(holder);
            group.Add(HudText.Make(text, HudTextRole.Meta));
        }

        // ============================================================ the director's two events

        void OnWorkChanged()
        {
            bool open = _directors != null && _directors.Work.Open;
            _workPanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open) return;

            // One panel at a time down here: the Build palette occupies the same corner, and two
            // things docked bottom-left would draw over each other.
            SetBuildPalette(false);
            ToggleMenu(false);
            RefreshWork();
        }

        void OnWorkModeChanged(WorkGridMode mode)
        {
            SetWorkModeChips(mode);
            _work.Mode = mode;
            RefreshWorkLegend();
            RefreshWork();
        }
    }
}
