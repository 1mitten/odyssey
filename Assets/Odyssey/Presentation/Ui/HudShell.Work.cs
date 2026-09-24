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
        VisualElement _workLeftRows = null!;
        VisualElement _workGridRows = null!;
        VisualElement _workLegend = null!;
        readonly Dictionary<WorkGridMode, Label> _workModeChips = new();
        readonly List<WorkRowView> _workRows = new List<WorkRowView>();

        /// <summary>The schedule half's rows, and the line that says which hour it is.</summary>
        VisualElement _workHourRows = null!;
        VisualElement _workNowLine = null!;
        int _workNowHour = -1;

        /// <summary>The header band, rebuilt when the column page turns. Eleven heads, never more.</summary>
        VisualElement _workHeaderBand = null!;

        /// <summary>This page's eleven column heads, so a sorted one can be marked.</summary>
        readonly List<VisualElement> _workHeads = new List<VisualElement>();

        /// <summary>Back to the roster's order; shown only while a sort is on.</summary>
        VisualElement _workSortReset = null!;

        /// <summary>The key's two halves, and the schedule half's armable buttons.</summary>
        VisualElement _workKeyWork = null!;
        VisualElement _workKeySchedule = null!;
        readonly Dictionary<int, VisualElement> _workBlockButtons = new();

        /// <summary>The two pagers, built in the roster's idiom and wearing its classes.</summary>
        VisualElement _workColumnPager = null!;
        Label _workColumnPageLabel = null!;
        VisualElement _workColumnPrev = null!;
        VisualElement _workColumnNext = null!;

        VisualElement _workRowPager = null!;
        Label _workRowPageLabel = null!;
        VisualElement _workRowPrev = null!;
        VisualElement _workRowNext = null!;

        /// <summary>What the pagers last drew, so they are only retexted when they change.</summary>
        int _workDrawnColumnPage = -1;
        int _workDrawnRowPage = -1;
        int _workDrawnRowPageCount = -1;

        /// <summary>
        /// Who was selected when the page was last brought to them. A selection that has not
        /// changed must not drag the page back every refresh while the player is reading another.
        /// </summary>
        PawnId _workFollowed;

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

            /// <summary>The twenty-four bands. One element each, no glyph, no border.</summary>
            public VisualElement Hours = null!;
            public readonly List<VisualElement> Blocks = new List<VisualElement>();
        }

        /// <summary>
        /// One cell: the box that carries the border and fill, the glyph, and the flames.
        ///
        /// <para><see cref="Column"/> is the <i>catalogue</i> index this slot is currently showing,
        /// and it moves when the column page turns — the eleven cells are built once and re-aimed,
        /// rather than twenty-two being built and eleven hidden.</para>
        /// </summary>
        sealed class WorkCellView
        {
            /// <summary>The pitch-wide holder. It carries the unbuilt column's wash, which moves
            /// with the page, so it is kept rather than found again.</summary>
            public VisualElement Slot = null!;

            public VisualElement Box = null!;
            public Label Glyph = null!;

            /// <summary>
            /// Simple mode's tick or cross, <b>drawn rather than typed</b>: neither font this HUD
            /// ships has U+2715 and only one has U+2713, so a label was two blanks (design 27
            /// §6.5). Held beside the label rather than replacing it, because Detailed's digit and
            /// the incapable em-dash are text and should stay text.
            /// </summary>
            public VisualElement Mark = null!;

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
            // **A constant width, and nothing in the panel may make it otherwise.** It is the
            // frozen names, one page of eleven work columns, the seam and the whole day: 1,383 and
            // two for the frame. There is no scroller and no cap — the owner's call on
            // 2026-09-20, and the right one, because a scrollbar made the panel's shape depend on
            // the window and built all twenty-two columns for every colonist to do it.
            // PanelOuterWidth, not PanelWidth: .panel's 12px padding and 1px border are inside a
            // UI Toolkit width, and setting the inner number here is what sent the schedule half
            // 26px out over the world.
            _workPanel.style.width = WorkGridLayout.PanelOuterWidth;

            BuildWorkHeaderExtras();

            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;

            // The frozen column: the names, and the pager that moves the colony through them.
            var left = new VisualElement();
            left.style.width = WorkGridLayout.LeftColumn;
            left.style.flexShrink = 0;
            left.style.borderRightWidth = HudTheme.BorderWidth;
            left.style.borderRightColor = HudTokens.PanelBorder;
            left.Add(BuildWorkNameHeader());

            _workLeftRows = new VisualElement();
            left.Add(_workLeftRows);
            grid.Add(left);

            // Both halves, side by side, at their own fixed widths. This used to be a ScrollView.
            var cols = new VisualElement();
            cols.style.flexShrink = 0;
            _workHeaderBand = new VisualElement();
            cols.Add(_workHeaderBand);
            RebuildWorkHeaderBand();
            _workGridRows = new VisualElement();
            cols.Add(_workGridRows);
            grid.Add(cols);

            // The seam. Two halves of one row still want a rule, or the last work column and
            // midnight read as neighbours.
            var seam = new VisualElement();
            seam.style.width = WorkGridLayout.SectionDivider;
            seam.style.flexShrink = 0;
            seam.style.backgroundColor = HudTokens.PanelBorder;
            grid.Add(seam);

            grid.Add(BuildScheduleHalf());
            _workPanel.Add(grid);

            // A wheel turns the pages, which is the gesture the scrollbar took with it. Down the
            // colony by default because that is what a wheel over a list of people means; shift
            // for the columns, which is the platform's own horizontal modifier.
            _workPanel.RegisterCallback<WheelEvent>(OnWorkWheel);

            // The key, in two halves that line up with the two halves of the table above it, and
            // divided by the same rule (owner, 2026-09-20: the keys "need to align with the
            // respective control as they are just sat all on one line").
            _workLegend = new VisualElement();
            _workLegend.style.flexDirection = FlexDirection.Row;
            _workLegend.style.borderTopWidth = HudTheme.BorderWidth;
            _workLegend.style.borderTopColor = HudTokens.PanelBorder;

            _workKeyWork = KeyHalf(WorkGridLayout.KeyWorkWidth);
            _workLegend.Add(_workKeyWork);

            // The same seam as the grid's, on the same x, so the two rules read as one line down
            // the panel rather than as two edges that nearly agree.
            var keySeam = new VisualElement();
            keySeam.style.width = WorkGridLayout.SectionDivider;
            keySeam.style.flexShrink = 0;
            keySeam.style.backgroundColor = HudTokens.PanelBorder;
            _workLegend.Add(keySeam);

            _workKeySchedule = KeyHalf(WorkGridLayout.KeyScheduleWidth);
            _workLegend.Add(_workKeySchedule);

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

            header.style.height = 30;
            header.style.marginBottom = 8;

            Label titleLabel = header.Q<Label>(className: "panel__label");
            VisualElement close = header.Q(className: "panel__close");
            header.Clear();

            // Work half: Colonist column (width 192) + Work columns (width 374) = 566px
            var colonistHdr = new VisualElement();
            colonistHdr.style.width = WorkGridLayout.LeftColumn;
            colonistHdr.style.height = Length.Percent(100);
            colonistHdr.style.flexShrink = 0;
            colonistHdr.style.flexDirection = FlexDirection.Row;
            colonistHdr.style.alignItems = Align.Center;
            if (titleLabel != null) colonistHdr.Add(titleLabel);
            header.Add(colonistHdr);

            // Over the work columns: central controls with generous spacing
            var workColsHdr = new VisualElement();
            workColsHdr.style.width = WorkGridLayout.ColumnsPerPage * WorkGridLayout.Pitch;
            workColsHdr.style.height = Length.Percent(100);
            workColsHdr.style.flexShrink = 0;
            workColsHdr.style.flexDirection = FlexDirection.Row;
            workColsHdr.style.alignItems = Align.Center;
            workColsHdr.style.justifyContent = Justify.Center;

            var central = new VisualElement();
            central.style.flexDirection = FlexDirection.Row;
            central.style.alignItems = Align.Center;

            Label priorities = HudText.Make("Priorities", HudTextRole.Meta);
            priorities.style.marginRight = 10;
            central.Add(priorities);

            var seg = new VisualElement();
            seg.AddToClassList("settings__tabs");
            seg.style.marginTop = 0;
            seg.style.marginBottom = 0;
            seg.style.borderBottomWidth = 0;
            seg.style.height = 24;
            seg.style.alignItems = Align.Center;
            foreach (WorkGridMode mode in new[] { WorkGridMode.Simple, WorkGridMode.Detailed })
            {
                Label chip = HudText.Make(mode == WorkGridMode.Simple ? "Simple" : "Detailed",
                    HudTextRole.Body, ussClass: "tab");
                chip.style.height = 20;
                chip.style.paddingLeft = 8;
                chip.style.paddingRight = 8;
                chip.style.paddingTop = 0;
                chip.style.paddingBottom = 0;
                chip.style.unityTextAlign = TextAnchor.MiddleCenter;
                chip.style.justifyContent = Justify.Center;
                WorkGridMode captured = mode;
                chip.RegisterCallback<ClickEvent>(_ => _directors?.Work.SetMode(captured));
                _workModeChips[mode] = chip;
                seg.Add(chip);
            }
            central.Add(seg);
            workColsHdr.Add(central);
            header.Add(workColsHdr);

            // The seam divider matching the grid below (1px)
            var hdrSeam = new VisualElement();
            hdrSeam.style.width = WorkGridLayout.SectionDivider;
            hdrSeam.style.height = Length.Percent(100);
            hdrSeam.style.flexShrink = 0;
            hdrSeam.style.backgroundColor = HudTokens.PanelBorder;
            header.Add(hdrSeam);

            // Schedule section of the header: aligned with the Schedule container at x = 567px
            var schedHdr = new VisualElement();
            schedHdr.style.height = Length.Percent(100);
            schedHdr.style.flexDirection = FlexDirection.Row;
            schedHdr.style.alignItems = Align.Center;
            schedHdr.style.flexGrow = 1;

            Label schedTitle = HudText.Make(Registry.Label("ui.tab.schedule"), HudTextRole.PanelLabel, ussClass: "panel__label");
            schedTitle.style.marginLeft = 6;
            schedHdr.Add(schedTitle);

            var schedSpacer = new VisualElement { pickingMode = PickingMode.Ignore };
            schedSpacer.style.flexGrow = 1;
            schedHdr.Add(schedSpacer);

            if (close != null) schedHdr.Add(close);
            header.Add(schedHdr);

            SetWorkModeChips(WorkGridMode.Simple);
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
        /// The frozen column's own header: the word, and the pager that moves the colony through
        /// the rows beneath it.
        ///
        /// <para>The pager for the names sits over the names. It is the roster's pager, wearing the
        /// roster's classes — there is one way this game turns a page and this is not the place to
        /// invent a second.</para>
        /// </summary>
        VisualElement BuildWorkNameHeader()
        {
            var head = new VisualElement();
            head.style.height = WorkGridLayout.HeaderBand;
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.FlexEnd;
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.paddingLeft = WorkGridLayout.LeftPad;
            head.style.paddingRight = WorkGridLayout.LeftPad;
            head.style.paddingBottom = 5;

            Label colonist = HudText.Make("Colonist", HudTextRole.PanelLabel);
            colonist.style.paddingBottom = 2;
            head.Add(colonist);

            var right = new VisualElement();
            right.style.flexDirection = FlexDirection.Row;
            right.style.alignItems = Align.Center;

            // Back to the roster's order. It sits with the row pager because both are about the
            // order of the rows, and it is the control that undoes what a header click did.
            _workSortReset = new VisualElement();
            _workSortReset.AddToClassList("roster-pager__btn");
            _workSortReset.style.display = DisplayStyle.None;
            _workSortReset.style.marginRight = 4;
            _workSortReset.tooltip = "Back to the roster's order";
            _workSortReset.Add(new HudGlyph(HudGlyphKind.Refresh, 12f, HudTokens.Accent));
            _workSortReset.RegisterCallback<ClickEvent>(evt =>
            {
                _work.ClearSort();
                RefreshWork();
                evt.StopPropagation();
            });
            right.Add(_workSortReset);

            _workRowPager = Pager(
                () => { _work.SetRowPage(_work.RowPage - 1); RefreshWork(); },
                () => { _work.SetRowPage(_work.RowPage + 1); RefreshWork(); },
                out _workRowPrev, out _workRowNext, out _workRowPageLabel);
            right.Add(_workRowPager);

            head.Add(right);
            return head;
        }

        /// <summary>
        /// A pager in the roster's idiom: a chevron, a count, a chevron, hidden on one page.
        /// <c>HudShell.Panels.cs</c> builds the roster's by hand; this is the same thing said once
        /// for the two the Work tab needs.
        /// </summary>
        static VisualElement Pager(System.Action back, System.Action forward,
            out VisualElement prev, out VisualElement next, out Label label)
        {
            var pager = new VisualElement();
            pager.AddToClassList("roster-pager");
            pager.style.display = DisplayStyle.None;

            prev = new VisualElement();
            prev.AddToClassList("roster-pager__btn");
            prev.Add(new HudGlyph(HudGlyphKind.ChevronLeft, 10f, HudTokens.TextDim));
            prev.RegisterCallback<ClickEvent>(evt => { back(); evt.StopPropagation(); });
            pager.Add(prev);

            label = HudText.Make("1 / 1", HudTextRole.Row, ussClass: "roster-pager__label");
            pager.Add(label);

            next = new VisualElement();
            next.AddToClassList("roster-pager__btn");
            next.Add(new HudGlyph(HudGlyphKind.ChevronRight, 10f, HudTokens.TextDim));
            next.RegisterCallback<ClickEvent>(evt => { forward(); evt.StopPropagation(); });
            pager.Add(next);
            return pager;
        }

        /// <summary>
        /// The header band: <b>eleven</b> boxes, each a rotated label above an icon tile, for the
        /// columns on this page.
        ///
        /// <para><b>Rebuilt when the page turns and at no other time.</b> The columns are content
        /// rather than colony state, so nothing about them changes while the game runs — but which
        /// eleven of them are showing does, and an icon tile cannot be re-aimed at another key
        /// without being remade. Eleven elements a page turn is not a cost worth pooling.</para>
        /// </summary>
        void RebuildWorkHeaderBand()
        {
            _workHeaderBand.Clear();
            _workHeads.Clear();
            _workHeaderBand.style.height = WorkGridLayout.HeaderBand;

            // The work half's own title strip, holding its own pager. Every control in this band
            // carries its name or its pager over itself and nothing over a neighbour.
            var strip = new VisualElement();
            strip.style.height = WorkGridLayout.TitleStrip;
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.alignItems = Align.Center;

            _workColumnPager = Pager(
                () => { _work.SetColumnPage(_work.ColumnPage - 1); OnWorkColumnPageChanged(); },
                () => { _work.SetColumnPage(_work.ColumnPage + 1); OnWorkColumnPageChanged(); },
                out _workColumnPrev, out _workColumnNext, out _workColumnPageLabel);
            _workColumnPager.style.marginTop = 0;
            _workColumnPager.style.marginBottom = 0;
            _workColumnPager.style.marginLeft = 8;
            strip.Add(_workColumnPager);
            _workHeaderBand.Add(strip);

            var labels = new VisualElement();
            labels.style.flexDirection = FlexDirection.Row;
            labels.style.height = WorkGridLayout.LabelBand;

            for (int slot = 0; slot < WorkGridLayout.ColumnsPerPage; slot++)
            {
                int c = _work.ColumnAt(slot);

                // A page that does not divide leaves empty slots rather than a short band, so the
                // seam and the day stay where the player left them. Twenty-two divides by eleven,
                // so this is insurance against the twenty-third work type and nothing else.
                if (c < 0)
                {
                    var blank = new VisualElement();
                    blank.style.width = WorkGridLayout.Pitch;
                    blank.style.flexShrink = 0;
                    labels.Add(blank);
                    continue;
                }

                WorkCatalogue.Entry entry = WorkGridModel.Columns[c];

                var head = new VisualElement();
                head.style.width = WorkGridLayout.Pitch;
                head.style.flexShrink = 0;
                head.style.height = WorkGridLayout.LabelBand;
                head.tooltip = entry.Live
                    ? entry.Label + " — click to sort by it, highest first"
                    : entry.Label + " — " + entry.Reason;

                // A live header sorts the colony by its column. Registered on the head rather than
                // on the label, because the label is rotated and its picking box is rotated with
                // it, which makes a 78px diagonal sliver of a 34px column to aim at.
                if (entry.Live)
                {
                    int sortColumn = c;
                    head.RegisterCallback<ClickEvent>(evt =>
                    {
                        if (_work.SortBy(sortColumn)) RefreshWork();
                        evt.StopPropagation();
                    });
                }

                // An unbuilt column is washed top to bottom so it reads as one absent thing
                // rather than as ten separately broken cells (design 27 §4a).
                if (!entry.Live) head.style.backgroundColor = HudTokens.Convert(WorkBands.UnbuiltWash);

                Label label = HudText.Make(entry.Label, HudTextRole.Body);
                label.style.position = Position.Absolute;
                label.style.left = Length.Percent(50);

                // Down where the icon tile used to be (owner, 2026-09-20). The square is gone and
                // the word takes its place, so the band is shorter and the label meets the first
                // row of cells with one gap rather than a gap, an icon and another gap.
                label.style.bottom = WorkGridLayout.LabelGap;
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
                _workHeads.Add(head);
                labels.Add(head);
            }

            _workHeaderBand.Add(labels);
        }

        /// <summary>
        /// The schedule half: twenty-four hour numbers over twenty-four rows of solid bands, with
        /// the now-line laid over all of it.
        ///
        /// <para><b>Colour is the entire signal.</b> No text, no icons, no borders, no gaps — a
        /// row of the day has to read as one unbroken band from across the desk, and anything
        /// drawn inside a block breaks that at exactly the distance the panel is useful.</para>
        /// </summary>
        VisualElement BuildScheduleHalf()
        {
            var half = new VisualElement();
            half.style.width = WorkGridLayout.ScheduleWidth;
            half.style.flexShrink = 0;
            // The now-line is absolutely positioned against this, which is what keeps it on the
            // right hour: measured from the panel it would be one frozen name column out.
            half.style.position = Position.Relative;

            var band = new VisualElement();
            band.style.height = WorkGridLayout.HeaderBand;

            // Spacer strip matching the work half's TitleStrip height, so the hours ruler aligns
            // with the base of the rotated work labels. The Schedule title itself now lives in the
            // window's header bar, aligned with the Work title.
            var strip = new VisualElement();
            strip.style.height = WorkGridLayout.TitleStrip;
            band.Add(strip);

            var hours = new VisualElement();
            hours.style.flexDirection = FlexDirection.Row;
            hours.style.height = WorkGridLayout.LabelBand;
            for (int h = 0; h < WorkGridLayout.Hours; h++)
            {
                var slot = new VisualElement();
                slot.style.width = WorkGridLayout.HourPitch;
                slot.style.flexShrink = 0;
                slot.style.justifyContent = Justify.FlexEnd;
                slot.style.alignItems = Align.Center;
                slot.style.paddingBottom = 7;

                // Deliberately the lightest text on the panel: the hours are a ruler, and a ruler
                // that competes with what it measures is a worse ruler.
                Label number = HudText.Make(h.ToString("00"), HudTextRole.Meta, numeric: true);
                number.style.color = HudTokens.TextFaint;
                slot.Add(number);
                hours.Add(slot);
            }
            band.Add(hours);
            half.Add(band);

            _workHourRows = new VisualElement();
            half.Add(_workHourRows);

            _workNowLine = new VisualElement();
            _workNowLine.style.position = Position.Absolute;
            _workNowLine.style.top = 0;
            _workNowLine.style.bottom = 0;
            _workNowLine.style.width = WorkGridLayout.NowLineWidth;
            _workNowLine.style.backgroundColor = HudTokens.Accent;
            _workNowLine.pickingMode = PickingMode.Ignore;
            _workNowLine.style.display = DisplayStyle.None;
            half.Add(_workNowLine);

            return half;
        }

        /// <summary>One colonist's day: twenty-four bands, packed edge to edge.</summary>
        VisualElement BuildScheduleRow(WorkRowView view, int rowIndex)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.height = WorkGridLayout.RowHeight;
            row.style.borderTopWidth = HudTheme.BorderWidth;
            row.style.borderTopColor = HudTokens.Divider;

            for (int h = 0; h < WorkGridLayout.Hours; h++)
            {
                var block = new VisualElement();
                block.style.width = WorkGridLayout.HourPitch;
                block.style.height = WorkGridLayout.RowHeight;
                block.style.flexShrink = 0;

                int capturedRow = rowIndex;
                int capturedHour = h;
                block.RegisterCallback<PointerDownEvent>(evt =>
                    OnScheduleBlockPressed(evt, capturedRow, capturedHour));

                view.Blocks.Add(block);
                row.Add(block);
            }
            return row;
        }

        /// <summary>
        /// Left cycles the six blocks forward, right cycles back, shift paints the hour down the
        /// whole colony — the same three gestures the work half answers to, so one row is one
        /// vocabulary rather than two.
        /// </summary>
        void OnScheduleBlockPressed(PointerDownEvent evt, int row, int hour)
        {
            if (_directors == null || row >= _work.Rows.Count) return;
            evt.StopPropagation();

            if (!_work.TryClickHour(row, hour, evt.button == 1, out Intent intent)) return;

            if (evt.shiftKey)
            {
                for (int r = 0; r < _work.Rows.Count; r++)
                    Submit(WorkGridModel.SetSchedule(_work.Rows[r].Id, hour, intent.C));
            }
            else
            {
                Submit(intent);
            }
            RefreshWork();
        }

        /// <summary>
        /// Put the now-line on the centre of the current hour's column, and show it only when
        /// there is a clock to ask.
        /// </summary>
        void RefreshNowLine(WorldSnapshot frame)
        {
            int hour = WorkGridModel.NowHour(frame);
            if (hour == _workNowHour) return;
            _workNowHour = hour;

            if (hour < 0 || hour >= WorkGridLayout.Hours)
            {
                _workNowLine.style.display = DisplayStyle.None;
                return;
            }
            _workNowLine.style.display = DisplayStyle.Flex;
            _workNowLine.style.left =
                WorkGridLayout.NowLineCentre(hour) - WorkGridLayout.NowLineWidth / 2f;
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

            // A selection made elsewhere brings its page up, once, on the frame it changes. Every
            // frame would drag the page back while the player is reading another one.
            FollowWorkSelection(frame);

            if (RosterChanged()) RebuildWorkRows();
            RefreshWorkPagers();
            MarkSortedColumn();

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

                for (int slot = 0; slot < view.Boxes.Count; slot++)
                {
                    WorkCellView box = view.Boxes[slot];
                    int column = box.Column;

                    // A slot past the end of the catalogue draws nothing at all. It cannot happen
                    // at twenty-two columns over eleven; it is what the twenty-third would meet.
                    if (column < 0 || column >= row.Cells.Count)
                    {
                        box.Slot.style.display = DisplayStyle.None;
                        continue;
                    }

                    box.Slot.style.display = DisplayStyle.Flex;
                    PaintWorkCell(box, row.Cells[column], _work.Describe(r, column));
                    PaintWorkMark(box, row.Cells[column]);
                }

                for (int h = 0; h < view.Blocks.Count && h < row.Hours.Count; h++)
                    view.Blocks[h].style.backgroundColor =
                        HudTokens.Convert(ScheduleCatalogue.ColourOf(row.Hours[h]));
            }

            RefreshNowLine(frame);
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
            _workHourRows.Clear();
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

                // A page of slots, not a catalogue of columns: eleven cells whatever the catalogue
                // grows to, re-aimed when the page turns rather than built twice.
                for (int slot = 0; slot < WorkGridLayout.ColumnsPerPage; slot++)
                    view.Cells.Add(BuildWorkCell(view, r, slot));

                _workGridRows.Add(view.Cells);

                view.Hours = BuildScheduleRow(view, r);
                _workHourRows.Add(view.Hours);

                _workRows.Add(view);
            }
        }

        VisualElement BuildWorkCell(WorkRowView view, int rowIndex, int slotIndex)
        {
            var slot = new VisualElement();
            slot.style.width = WorkGridLayout.Pitch;
            slot.style.height = WorkGridLayout.RowHeight;
            slot.style.flexShrink = 0;
            slot.style.alignItems = Align.Center;
            slot.style.justifyContent = Justify.Center;

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

            // Pinned to all four edges rather than left to find its own place: an absolutely
            // positioned child with every offset auto lands on its static position, which is where
            // the label already is, and the mark would sit beside the digit's box instead of over
            // the cell. Filled and centred, it is the cell.
            var mark = new VisualElement();
            mark.style.position = Position.Absolute;
            mark.style.top = 0;
            mark.style.left = 0;
            mark.style.right = 0;
            mark.style.bottom = 0;
            mark.style.alignItems = Align.Center;
            mark.style.justifyContent = Justify.Center;
            mark.pickingMode = PickingMode.Ignore;
            mark.style.display = DisplayStyle.None;
            box.Add(mark);

            var flames = new VisualElement();
            flames.style.position = Position.Absolute;
            flames.style.top = 1;
            flames.style.right = 1;
            flames.style.flexDirection = FlexDirection.Row;
            flames.pickingMode = PickingMode.Ignore;
            box.Add(flames);

            var cellView = new WorkCellView
            {
                Slot = slot, Box = box, Glyph = glyph, Mark = mark, Flames = flames,
                Column = _work.ColumnAt(slotIndex),
            };
            view.Boxes.Add(cellView);

            // The press carries the *slot*; which column that is now is asked of the view when it
            // fires. A captured column index would be the page the cell was built on for ever.
            int capturedRow = rowIndex;
            int capturedSlot = slotIndex;
            box.RegisterCallback<PointerDownEvent>(evt =>
                OnWorkCellPressed(evt, capturedRow, capturedSlot));

            slot.Add(box);
            return slot;
        }

        /// <summary>
        /// Paint one cell from the model. <b>Everything it draws, the model has already decided</b>
        /// — an incapable cell arrives with no priority and no passion to hide, which is design 27
        /// §6.1's rule and the reason it is enforced there rather than here.
        /// </summary>
        void PaintWorkCell(WorkCellView view, in WorkCell cell, string description)
        {
            HudText.Set(view.Glyph, cell.Glyph(_work.Mode), HudTextRole.Name);
            view.Flames.Clear();

            // The wash down an unbuilt column is the slot's, not the box's, and a slot shows a
            // different work type after a page turn — so it is painted every time rather than set
            // once when the cell was built.
            view.Slot.style.backgroundColor = cell.Built
                ? Color.clear
                : HudTokens.Convert(WorkBands.UnbuiltWash);

            // The four signals in words. WorkGridModel.Describe has said this sentence since the
            // model was written and nothing asked it for it: the border is a skill band, the ink
            // is a priority and the flames are a passion, and none of the three is labelled
            // anywhere but the legend at the foot of the panel.
            view.Box.tooltip = description;

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

            view.Glyph.style.color = HudTokens.Convert(WorkBands.InkOf(cell.Priority));

            for (int f = 0; f < cell.Passion; f++) view.Flames.Add(WorkFlame());
        }

        /// <summary>
        /// Simple mode's tick or cross, or nothing at all in Detailed.
        ///
        /// <para>Rebuilt rather than recoloured because a <see cref="HudGlyph"/> carries its
        /// colour into a generated mesh; two elements kept and swapped would be the same
        /// allocation with a second thing to keep in step. Only cells that draw one pay for it,
        /// and only while Simple is the mode.</para>
        /// </summary>
        void PaintWorkMark(WorkCellView view, in WorkCell cell)
        {
            view.Mark.Clear();

            // Which mark, if any, is the model's decision — the shell only knows how to draw one.
            WorkMark mark = cell.Mark(_work.Mode);
            view.Mark.style.display = mark == WorkMark.None ? DisplayStyle.None : DisplayStyle.Flex;
            if (mark == WorkMark.None) return;

            bool will = mark == WorkMark.Will;
            view.Mark.Add(new HudGlyph(will ? HudGlyphKind.Check : HudGlyphKind.Cross,
                WorkGridLayout.MarkSize,
                HudTokens.Convert(will ? WorkBands.WillDo : WorkBands.WontDo)));
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
        void OnWorkCellPressed(PointerDownEvent evt, int row, int slot)
        {
            if (_directors == null) return;
            if (row < 0 || row >= _workRows.Count) return;
            if (slot < 0 || slot >= _workRows[row].Boxes.Count) return;

            // Which work type this slot is showing right now, rather than the one it was built on.
            int column = _workRows[row].Boxes[slot].Column;
            if (column < 0) return;

            // The model decides whether this cell answers a click and what the click makes it.
            // This method used to decide both for itself, which meant the rule the fast tier held
            // and the rule the panel ran were two rules that happened to agree.
            if (!_work.TryClick(row, column, back: evt.button == 1, out Intent intent)) return;

            evt.StopPropagation();
            int handle = WorkGridModel.Columns[column].Handle;

            if (evt.shiftKey)
            {
                // The column as drawn, which is this page of colonists. Reaching colonists the
                // player cannot see would be a gesture whose result is off-screen; the page is
                // both what they are looking at and what they can check afterwards.
                for (int r = 0; r < _work.Rows.Count; r++)
                    if (_work.CellIsInteractive(r, column))
                        Submit(WorkGridModel.SetPriority(_work.Rows[r].Id, handle, intent.C));
            }
            else
            {
                Submit(intent);
            }

            // Repaint now rather than waiting out the refresh bucket: a grid that answers a click
            // a quarter of a second later reads as a grid that missed it.
            RefreshWork();
        }

        /// <summary>
        /// The column page turned: re-aim every cell, remake the eleven header boxes, and repaint.
        ///
        /// <para><b>Nothing is built per colonist here.</b> The rows and their cells already exist;
        /// only which work type each cell is pointed at changes, which is why turning a page costs
        /// eleven header boxes and no cells at all.</para>
        /// </summary>
        void OnWorkColumnPageChanged()
        {
            RebuildWorkHeaderBand();
            for (int r = 0; r < _workRows.Count; r++)
            {
                WorkRowView view = _workRows[r];
                for (int slot = 0; slot < view.Boxes.Count; slot++)
                    view.Boxes[slot].Column = _work.ColumnAt(slot);
            }

            // The band was rebuilt, so the accent under the sorted header went with it. The sort
            // itself did not change; only the heads that could show it did.
            RefreshWork();
        }

        /// <summary>
        /// Bring the page holding the selected colonist up, on the frame the selection changes and
        /// not afterwards.
        /// </summary>
        void FollowWorkSelection(WorldSnapshot frame)
        {
            if (_directors == null) return;
            var selected = _directors.Selection.Pawns;
            PawnId one = selected != null && selected.Count == 1 ? selected[0] : default;

            if (one == _workFollowed) return;
            _workFollowed = one;
            if (!one.IsValid) return;

            if (_work.EnsureRowPageFor(one))
                _work.Refresh(frame, _roster.CustomOrder, selected);
        }

        /// <summary>
        /// Both pagers: shown only when there is more than one page, retexted only when the
        /// numbers move, and their arrows dimmed at the ends. The roster's rules.
        /// </summary>
        void RefreshWorkPagers()
        {
            // BuildWorkHeaderExtras gives up if the window has no header row to hang things on,
            // and the column pager is one of those things. The row pager is in the grid itself and
            // is always there.
            if (_workColumnPager == null) return;

            bool manyColumns = _work.ColumnPageCount > 1;
            _workColumnPager.style.display = manyColumns ? DisplayStyle.Flex : DisplayStyle.None;
            if (manyColumns)
            {
                if (_workDrawnColumnPage != _work.ColumnPage)
                {
                    _workDrawnColumnPage = _work.ColumnPage;
                    HudText.Set(_workColumnPageLabel,
                        $"{_work.ColumnPage + 1} / {_work.ColumnPageCount}", HudTextRole.Row);
                }
                _workColumnPrev.SetEnabled(_work.ColumnPage > 0);
                _workColumnNext.SetEnabled(_work.ColumnPage < _work.ColumnPageCount - 1);
            }

            bool manyRows = _work.RowPageCount > 1;
            _workRowPager.style.display = manyRows ? DisplayStyle.Flex : DisplayStyle.None;
            if (!manyRows) return;

            if (_workDrawnRowPage != _work.RowPage || _workDrawnRowPageCount != _work.RowPageCount)
            {
                _workDrawnRowPage = _work.RowPage;
                _workDrawnRowPageCount = _work.RowPageCount;
                HudText.Set(_workRowPageLabel,
                    $"{_work.RowPage + 1} / {_work.RowPageCount}", HudTextRole.Row);
            }
            _workRowPrev.SetEnabled(_work.RowPage > 0);
            _workRowNext.SetEnabled(_work.RowPage < _work.RowPageCount - 1);
        }

        /// <summary>
        /// The wheel turns pages: down the colony plain, across the columns with shift.
        ///
        /// <para>This is the gesture the scrollbar took with it, and it is put back on the axis a
        /// player expects it on. Shift for the horizontal is the platform's own convention rather
        /// than this panel's invention.</para>
        /// </summary>
        void OnWorkWheel(WheelEvent evt)
        {
            int delta = evt.delta.y > 0 ? 1 : (evt.delta.y < 0 ? -1 : 0);
            if (delta == 0) return;

            if (evt.shiftKey)
            {
                if (_work.ColumnPageCount <= 1) return;
                _work.SetColumnPage(_work.ColumnPage + delta);
                OnWorkColumnPageChanged();
            }
            else
            {
                if (_work.RowPageCount <= 1) return;
                _work.SetRowPage(_work.RowPage + delta);
                RefreshWork();
            }
            evt.StopPropagation();
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
        /// <summary>
        /// The key, in two halves under the two halves of the table.
        ///
        /// <para><b>Both subtexts are gone</b> (owner, 2026-09-20). The Simple-mode note said the
        /// border still showed skill and the flames still showed passion, and the footnote said a
        /// row was one colonist's whole day — two sentences explaining a picture that had to
        /// explain itself. What is left is swatches and words.</para>
        ///
        /// <para><b>The schedule half's entries are buttons.</b> Pressing one arms that block and
        /// lights it; hours then take it instead of cycling. Pressing it again disarms it. So the
        /// key is a palette and you can see which colour is in your hand.</para>
        /// </summary>
        void RefreshWorkLegend()
        {
            _workKeyWork.Clear();
            _workKeySchedule.Clear();
            _workBlockButtons.Clear();

            if (_work.Mode == WorkGridMode.Detailed)
            {
                AddLegendSwatch(_workKeyWork, WorkBands.Novice, "0–3", WorkBands.BorderWidth);
                AddLegendSwatch(_workKeyWork, WorkBands.Apprentice, "4–6", WorkBands.BorderWidth);
                AddLegendSwatch(_workKeyWork, WorkBands.Competent, "7–10", WorkBands.BorderWidth);
                AddLegendSwatch(_workKeyWork, WorkBands.Skilled, "11–14", WorkBands.BorderWidth);
                AddLegendSwatch(_workKeyWork, WorkBands.Master, "15–20", WorkBands.BorderWidth);
            }
            else
            {
                AddLegendGlyph(_workKeyWork, HudGlyphKind.Check, WorkBands.WillDo, "will do");
                AddLegendGlyph(_workKeyWork, HudGlyphKind.Cross, WorkBands.WontDo, "won't do");
            }

            AddLegendFlames(_workKeyWork, 1, "interested");
            AddLegendFlames(_workKeyWork, 2, "passion");
            AddLegendSwatch(_workKeyWork, WorkBands.IncapableBorder, "incapable", HudTheme.BorderWidth);

            // The day's six, in the order the click cycles them, each one a button that arms it.
            foreach (ScheduleCatalogue.Entry block in ScheduleCatalogue.All)
                AddBlockButton(block);

            MarkArmedBlock();
        }

        /// <summary>One half of the key: a fixed width, so it sits under its own control.</summary>
        VisualElement KeyHalf(int width)
        {
            var half = new VisualElement();
            half.style.width = width;
            half.style.flexShrink = 0;
            half.style.flexDirection = FlexDirection.Row;
            half.style.flexWrap = Wrap.Wrap;
            half.style.alignItems = Align.Center;
            half.style.paddingLeft = WorkGridLayout.LeftPad;
            half.style.paddingRight = WorkGridLayout.LeftPad;
            half.style.paddingTop = 6;
            half.style.paddingBottom = 6;
            return half;
        }

        /// <summary>
        /// One schedule block, as a button: the band's colour and its name, pressable together.
        /// </summary>
        void AddBlockButton(ScheduleCatalogue.Entry block)
        {
            var button = new VisualElement();
            button.AddToClassList("workkey__btn");
            button.tooltip = block.Label + " — click to paint it, click again to stop";

            var box = new VisualElement();
            box.AddToClassList("workkey__swatch");
            box.style.backgroundColor = HudTokens.Convert(block.Colour);
            button.Add(box);

            Label name = HudText.Make(block.Label, HudTextRole.Meta, ussClass: "workkey__name");
            button.Add(name);

            int handle = block.Handle;
            button.RegisterCallback<ClickEvent>(evt =>
            {
                _work.ArmBlock(handle);
                MarkArmedBlock();
                evt.StopPropagation();
            });

            _workBlockButtons[handle] = button;
            _workKeySchedule.Add(button);
        }

        /// <summary>Light the armed block and dim the rest, so the colour in your hand is visible.</summary>
        void MarkArmedBlock()
        {
            foreach (var entry in _workBlockButtons)
                entry.Value.EnableInClassList("workkey__btn--on", entry.Key == _work.ArmedBlock);
        }

        /// <summary>A group in one half of the key. The half is passed, not assumed.</summary>
        static VisualElement LegendGroup(VisualElement half)
        {
            var group = new VisualElement();
            group.style.flexDirection = FlexDirection.Row;
            group.style.alignItems = Align.Center;
            group.style.marginRight = 12;
            half.Add(group);
            return group;
        }

        static void AddLegendSwatch(VisualElement half, HudColour border, string text, float width)
        {
            VisualElement group = LegendGroup(half);
            var box = new VisualElement();
            box.style.width = 15;
            box.style.height = 15;
            box.style.marginRight = 6;
            box.style.backgroundColor = HudTokens.Convert(WorkBands.AssignedFill);
            SetBorder(box, width, HudTokens.Convert(border));
            group.Add(box);
            group.Add(HudText.Make(text, HudTextRole.Meta));
        }

        static void AddLegendGlyph(VisualElement half, HudGlyphKind kind, HudColour ink, string text)
        {
            VisualElement group = LegendGroup(half);
            var mark = new HudGlyph(kind, WorkGridLayout.MarkSize, HudTokens.Convert(ink));
            mark.style.marginRight = 6;
            group.Add(mark);
            group.Add(HudText.Make(text, HudTextRole.Meta));
        }

        static void AddLegendFlames(VisualElement half, int count, string text)
        {
            VisualElement group = LegendGroup(half);
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

            if (!open)
            {
                // Closing is what "leaving the control" means (owner, 2026-09-20), so the sort and
                // the armed block are dropped here and the panel opens on the roster's order with
                // nothing in hand. Both are view state and neither is saved.
                _work.ClearSort();
                _work.DisarmBlock();
                MarkArmedBlock();
                return;
            }

            // One panel at a time down here: the Build palette occupies the same corner, and two
            // things docked bottom-left would draw over each other.
            SetBuildPalette(false);
            ToggleMenu(false);
            _directors?.Animals.SetOpen(false);
            _directors?.Inventory.SetOpen(false);
            _directors?.Research.SetOpen(false);
            RefreshWork();
        }

        /// <summary>
        /// Mark the header the colony is sorted by, and show the reset beside the names.
        ///
        /// <para>The sorted column is the only header with an accent under it, because a sort is a
        /// thing you did rather than a thing about the work, and the row order alone does not say
        /// which of eleven columns produced it.</para>
        /// </summary>
        void MarkSortedColumn()
        {
            for (int slot = 0; slot < _workHeads.Count; slot++)
            {
                bool sorted = _work.SortColumn != WorkGridModel.NoSort &&
                              _work.ColumnAt(slot) == _work.SortColumn;
                _workHeads[slot].style.borderBottomWidth = sorted ? 2 : 0;
                _workHeads[slot].style.borderBottomColor = HudTokens.Accent;
            }

            _workSortReset.style.display = _work.SortColumn != WorkGridModel.NoSort
                ? DisplayStyle.Flex
                : DisplayStyle.None;
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
