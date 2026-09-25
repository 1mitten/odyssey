#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// <see cref="HudShell"/>: the colonist pane's Thoughts tab (design 51 §10, mockup 23b) and the
    /// traits block under the Needs tab's bars (§5f). The tab is a mood column (the figure, a meter
    /// built on the Needs tab's own bar fill, where she is heading, and the breakdown), the thoughts
    /// table with the Animals tab's pager, and the traits strip. <b>Every word, number and colour is
    /// the model's</b> (<see cref="ThoughtsTab"/>, tested in the fast tier); this file draws them, and
    /// only when the model's version moved.
    /// </summary>
    public sealed partial class HudShell
    {
        // ---- the Thoughts tab (design 51 §10, mockup 23b) ---------------------------------------

        /// <summary>The Thoughts tab's body, built once per subject into the pane's tab box.</summary>
        VisualElement? _thoughtsBody;

        Label? _moodValue, _targetWord, _targetValue;
        VisualElement? _meterBad, _meterWarn, _meterFill, _meterTick;
        readonly List<(Label Label, Label Value)> _breakdown = new List<(Label, Label)>();
        readonly List<ThoughtSlot> _thoughtSlots = new List<ThoughtSlot>();
        VisualElement? _thoughtPager;
        HudGlyph? _thoughtPrevGlyph, _thoughtNextGlyph;
        Label? _thoughtPageLabel, _thoughtsEmpty;
        readonly List<ChipView> _traitChips = new List<ChipView>();
        int _thoughtsDrawn = -1;

        /// <summary>One row of the thoughts table: a rail, the name and its source, the time left, the worth.</summary>
        sealed class ThoughtSlot
        {
            public VisualElement Root = null!, Rail = null!, Cells = null!;
            public Label Name = null!, Source = null!, Lasts = null!, Value = null!;
        }

        /// <summary>One trait chip in the foot strip.</summary>
        sealed class ChipView
        {
            public VisualElement Root = null!;
            public Label Name = null!, Value = null!;
        }

        /// <summary>A row of the traits block on the Needs tab, which draws label-and-value rows.</summary>
        sealed class ThoughtRowView
        {
            public VisualElement Root = null!;
            public Label Name = null!;
            public Label Value = null!;
            public string LastName = string.Empty;
            public string LastValue = string.Empty;
            public HudColour? LastTint;
            public bool TintKnown;
            public string? LastTooltip;
            public bool LastHeading;
        }

        static bool SameColour(HudColour? a, HudColour? b) =>
            a.HasValue == b.HasValue && (!a.HasValue
                || (a.Value.R == b!.Value.R && a.Value.G == b.Value.G && a.Value.B == b.Value.B && a.Value.A == b.Value.A));

        /// <summary>
        /// Build the Thoughts tab's body into <paramref name="tabBody"/>, hidden until the tab is shown:
        /// the mood column on the left, the table on the right, the traits strip along the foot. Every
        /// size is <see cref="ThoughtsLayout"/>'s and every colour a <see cref="HudTokens"/> one.
        /// </summary>
        void BuildThoughtsTab(VisualElement tabBody)
        {
            _thoughtsBody = new VisualElement { name = "thoughts" };
            _thoughtsBody.style.display = DisplayStyle.None;
            _thoughtsBody.style.height = ThoughtsLayout.TabBody;
            _thoughtsBody.style.flexDirection = FlexDirection.Column;

            var upper = new VisualElement();
            upper.style.flexDirection = FlexDirection.Row;
            upper.style.height = ThoughtsLayout.Upper;
            upper.Add(BuildMoodColumn());
            upper.Add(BuildThoughtTable());
            _thoughtsBody.Add(upper);
            _thoughtsBody.Add(BuildTraitStrip());

            _thoughtsDrawn = -1;
            tabBody.Add(_thoughtsBody);
        }

        VisualElement BuildMoodColumn()
        {
            var column = new VisualElement { name = "thoughts-mood" };
            column.style.width = ThoughtsLayout.LeftWidth;
            column.style.flexShrink = 0;
            Padding(column, ThoughtsLayout.Pad, ThoughtsLayout.Pad);
            column.style.borderRightWidth = HudTheme.BorderWidth;
            column.style.borderRightColor = HudTokens.PanelBorder;

            var moodLine = new VisualElement();
            moodLine.style.flexDirection = FlexDirection.Row;
            moodLine.style.justifyContent = Justify.SpaceBetween;
            moodLine.style.alignItems = Align.Center;
            moodLine.style.height = ThoughtsLayout.MoodLine;
            moodLine.Add(SectionLabel("ui.mind.mood"));
            _moodValue = HudText.Make(string.Empty, HudTextRole.Name, numeric: true);
            moodLine.Add(_moodValue);
            column.Add(moodLine);

            // The meter: the need bar's own fill (.bar__fill, the Needs tab's) in a framed track, with
            // her two break bands under it and the target's tick over it.
            var meter = new VisualElement();
            meter.style.height = ThoughtsLayout.MoodBar;
            meter.style.marginTop = ThoughtsLayout.Gap;
            meter.style.backgroundColor = HudTokens.Convert(HudTheme.RowRule);
            Border(meter, HudTokens.PanelBorder);
            meter.style.overflow = Overflow.Visible;
            _meterBad = Absolute(meter, HudTokens.Convert(HudTheme.Bad.WithAlpha(0.22f)), 0, ThoughtsLayout.MoodBar - 2 * HudTheme.BorderWidth);
            _meterWarn = Absolute(meter, HudTokens.Convert(HudTheme.Warn.WithAlpha(0.18f)), 0, ThoughtsLayout.MoodBar - 2 * HudTheme.BorderWidth);
            _meterFill = Absolute(meter, HudTokens.Good, ThoughtsLayout.MoodFillInset - HudTheme.BorderWidth, ThoughtsLayout.MoodFill);
            _meterFill.AddToClassList("bar__fill");
            int inner = ThoughtsLayout.MoodBar - 2 * HudTheme.BorderWidth;
            _meterTick = Absolute(meter, HudTokens.TextPrimary, (inner - ThoughtsLayout.TickHeight) / 2, ThoughtsLayout.TickHeight);
            _meterTick.name = "thoughts-tick";
            _meterTick.style.width = ThoughtsLayout.TickWidth;
            _meterTick.style.marginLeft = -ThoughtsLayout.TickWidth / 2;
            column.Add(meter);

            var target = new VisualElement();
            target.style.flexDirection = FlexDirection.Row;
            target.style.alignItems = Align.Center;
            target.style.height = ThoughtsLayout.TargetLine;
            target.style.marginTop = ThoughtsLayout.Gap;
            _targetWord = HudText.Make(string.Empty, HudTextRole.Meta);
            _targetWord.style.color = HudTokens.TextMeta;
            _targetValue = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true);
            _targetValue.style.color = HudTokens.TextPrimary;
            _targetValue.style.marginLeft = 4;
            target.Add(_targetWord);
            target.Add(_targetValue);
            column.Add(target);

            var breakdown = new VisualElement();
            breakdown.style.marginTop = ThoughtsLayout.Gap + ThoughtsLayout.BreakdownTop;
            breakdown.style.borderTopWidth = HudTheme.BorderWidth;
            breakdown.style.borderTopColor = HudTokens.Convert(HudTheme.RowRule);
            _breakdown.Clear();
            for (int i = 0; i < ThoughtsLayout.BreakdownRows; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.alignItems = Align.Center;
                row.style.height = ThoughtsLayout.BreakdownRow;
                row.style.borderBottomWidth = HudTheme.BorderWidth;
                row.style.borderBottomColor = HudTokens.Convert(HudTheme.RowRule);
                Label label = HudText.Make(string.Empty, HudTextRole.Body);
                label.style.color = HudTokens.TextMeta;
                Label value = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true);
                row.Add(label);
                row.Add(value);
                breakdown.Add(row);
                _breakdown.Add((label, value));
            }
            column.Add(breakdown);
            return column;
        }

        VisualElement BuildThoughtTable()
        {
            var table = new VisualElement();
            table.style.flexGrow = 1;
            table.style.flexShrink = 1;
            table.style.minWidth = 0;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.height = ThoughtsLayout.Header;
            header.style.flexShrink = 0;
            Padding(header, 0, ThoughtsLayout.Pad);
            header.style.borderBottomWidth = HudTheme.BorderWidth;
            header.style.borderBottomColor = HudTokens.PanelBorder;
            Label thought = SectionLabel("ui.mind.thought");
            thought.style.flexGrow = 1;
            header.Add(thought);
            header.Add(Column(SectionLabel("ui.mind.lasts"), ThoughtsLayout.LastsColumn));
            header.Add(Column(SectionLabel("ui.mind.mood"), ThoughtsLayout.MoodColumn));
            table.Add(header);

            _thoughtSlots.Clear();
            for (int i = 0; i < ThoughtsLayout.RowsPerPage; i++)
            {
                var slot = new ThoughtSlot { Root = new VisualElement() };
                slot.Root.style.height = ThoughtsLayout.Row;
                slot.Root.style.flexShrink = 0;
                slot.Root.style.borderBottomWidth = HudTheme.BorderWidth;
                slot.Root.style.borderBottomColor = HudTokens.Convert(HudTheme.RowRule);

                slot.Rail = new VisualElement();
                slot.Rail.style.position = Position.Absolute;
                slot.Rail.style.left = 0;
                slot.Rail.style.top = 0;
                slot.Rail.style.bottom = 0;
                slot.Rail.style.width = ThoughtsLayout.Rail;
                slot.Root.Add(slot.Rail);

                slot.Cells = new VisualElement();
                slot.Cells.style.flexDirection = FlexDirection.Row;
                slot.Cells.style.alignItems = Align.Center;
                slot.Cells.style.flexGrow = 1;
                Padding(slot.Cells, 0, ThoughtsLayout.Pad);

                var nameCell = new VisualElement();
                nameCell.style.flexDirection = FlexDirection.Row;
                nameCell.style.alignItems = Align.Center;
                nameCell.style.flexGrow = 1;
                nameCell.style.flexShrink = 1;
                nameCell.style.minWidth = 0;
                nameCell.style.overflow = Overflow.Hidden;
                slot.Name = HudText.Make(string.Empty, HudTextRole.Row);
                slot.Name.style.color = HudTokens.TextPrimary;
                slot.Name.style.flexShrink = 1;
                slot.Name.style.minWidth = 0;
                slot.Name.style.overflow = Overflow.Hidden;
                slot.Name.style.whiteSpace = WhiteSpace.NoWrap;
                slot.Name.style.textOverflow = TextOverflow.Ellipsis;
                slot.Source = HudText.Make(string.Empty, HudTextRole.Meta);
                slot.Source.style.color = HudTokens.TextDim;
                slot.Source.style.marginLeft = 8;
                slot.Source.style.flexShrink = 0;
                nameCell.Add(slot.Name);
                nameCell.Add(slot.Source);
                slot.Cells.Add(nameCell);

                slot.Lasts = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true);
                slot.Lasts.style.color = HudTokens.TextDim;
                slot.Cells.Add(Column(slot.Lasts, ThoughtsLayout.LastsColumn));
                slot.Value = HudText.Make(string.Empty, HudTextRole.Row, numeric: true);
                slot.Cells.Add(Column(slot.Value, ThoughtsLayout.MoodColumn));
                slot.Root.Add(slot.Cells);

                table.Add(slot.Root);
                _thoughtSlots.Add(slot);
            }

            // "No thoughts right now", in the first row's place.
            _thoughtsEmpty = HudText.Make(Registry.Label("ui.mind.nothing"), HudTextRole.Body);
            _thoughtsEmpty.style.color = HudTokens.TextMeta;
            _thoughtsEmpty.style.alignSelf = Align.Center;
            _thoughtsEmpty.style.marginLeft = ThoughtsLayout.Pad;
            _thoughtsEmpty.tooltip = Registry.Describe("ui.mind.nothing");
            _thoughtSlots[0].Root.Add(_thoughtsEmpty);

            // The standard pager, the Animals tab's, in the last row's place when there are more.
            ThoughtSlot last = _thoughtSlots[_thoughtSlots.Count - 1];
            _thoughtPager = new VisualElement();
            _thoughtPager.style.flexDirection = FlexDirection.Row;
            _thoughtPager.style.alignItems = Align.Center;
            _thoughtPager.style.justifyContent = Justify.FlexEnd;
            _thoughtPager.style.flexGrow = 1;
            Padding(_thoughtPager, 0, ThoughtsLayout.Pad);
            _thoughtPager.Add(PagerButton(out HudGlyph prev, HudGlyphKind.ChevronLeft,
                () => { if (_inspect.Thoughts.SetPage(_inspect.Thoughts.Page - 1)) SyncThoughtsTab(); }));
            _thoughtPageLabel = HudText.Make("1 / 1", HudTextRole.Meta, numeric: true);
            _thoughtPageLabel.style.color = HudTokens.TextPrimary;
            _thoughtPageLabel.style.marginLeft = AnimalsLayout.PagerGap;
            _thoughtPageLabel.style.marginRight = AnimalsLayout.PagerGap;
            _thoughtPager.Add(_thoughtPageLabel);
            _thoughtPager.Add(PagerButton(out HudGlyph next, HudGlyphKind.ChevronRight,
                () => { if (_inspect.Thoughts.SetPage(_inspect.Thoughts.Page + 1)) SyncThoughtsTab(); }));
            _thoughtPrevGlyph = prev;
            _thoughtNextGlyph = next;
            last.Root.Add(_thoughtPager);
            return table;
        }

        VisualElement BuildTraitStrip()
        {
            var strip = new VisualElement();
            strip.style.height = ThoughtsLayout.TraitsStrip;
            strip.style.flexShrink = 0;
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.alignItems = Align.Center;
            strip.style.overflow = Overflow.Hidden;
            Padding(strip, 0, ThoughtsLayout.Pad);
            strip.style.borderTopWidth = HudTheme.BorderWidth;
            strip.style.borderTopColor = HudTokens.PanelBorder;

            Label label = SectionLabel("ui.mind.traits");
            label.style.marginRight = 3 + ThoughtsLayout.Gap;
            strip.Add(label);

            _traitChips.Clear();
            for (int i = 0; i <= Odyssey.Sim.Contracts.TraitHandle.MaxPerPawn; i++)
            {
                var chip = new ChipView { Root = new VisualElement { name = "thoughts-chip" } };
                chip.Root.style.flexDirection = FlexDirection.Row;
                chip.Root.style.alignItems = Align.Center;
                chip.Root.style.height = ThoughtsLayout.ChipHeight;
                chip.Root.style.flexShrink = 0;
                chip.Root.style.marginRight = ThoughtsLayout.Gap;
                Padding(chip.Root, 0, ThoughtsLayout.ChipPad);
                Border(chip.Root, HudTokens.Convert(HudTheme.ControlBorder));
                chip.Name = HudText.Make(string.Empty, HudTextRole.Row);
                chip.Name.style.color = HudTokens.TextPrimary;
                chip.Value = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true);
                chip.Value.style.marginLeft = ThoughtsLayout.ChipGap;
                chip.Root.Add(chip.Name);
                chip.Root.Add(chip.Value);
                chip.Root.style.display = DisplayStyle.None;
                strip.Add(chip.Root);
                _traitChips.Add(chip);
            }
            return strip;
        }

        static Label SectionLabel(string key)
        {
            Label label = HudText.Make(Registry.Label(key), HudTextRole.PanelLabel);
            label.style.color = HudTokens.TextDim;
            label.tooltip = Registry.Describe(key);
            return label;
        }

        /// <summary>A fixed-width, right-aligned figure column of the table, the header's and a row's alike.</summary>
        static VisualElement Column(Label label, int width)
        {
            label.style.width = width;
            label.style.flexShrink = 0;
            label.style.marginLeft = ThoughtsLayout.ColumnGap;
            label.style.unityTextAlign = TextAnchor.MiddleRight;
            return label;
        }

        static VisualElement Absolute(VisualElement parent, Color colour, int top, int height)
        {
            var element = new VisualElement();
            element.style.position = Position.Absolute;
            element.style.top = top;
            element.style.height = height;
            element.style.left = 0;
            element.style.width = 0;
            element.style.backgroundColor = colour;
            parent.Add(element);
            return element;
        }

        static void Padding(VisualElement element, int vertical, int horizontal)
        {
            element.style.paddingTop = vertical;
            element.style.paddingBottom = vertical;
            element.style.paddingLeft = horizontal;
            element.style.paddingRight = horizontal;
        }

        static void Border(VisualElement element, Color colour)
        {
            element.style.borderTopWidth = HudTheme.BorderWidth;
            element.style.borderBottomWidth = HudTheme.BorderWidth;
            element.style.borderLeftWidth = HudTheme.BorderWidth;
            element.style.borderRightWidth = HudTheme.BorderWidth;
            element.style.borderTopColor = colour;
            element.style.borderBottomColor = colour;
            element.style.borderLeftColor = colour;
            element.style.borderRightColor = colour;
        }

        /// <summary>The pane is being rebuilt for another subject: drop what was built.</summary>
        void ForgetThoughtsTab()
        {
            _thoughtsBody = null;
            _moodValue = _targetWord = _targetValue = _thoughtPageLabel = _thoughtsEmpty = null;
            _meterBad = _meterWarn = _meterFill = _meterTick = _thoughtPager = null;
            _thoughtPrevGlyph = _thoughtNextGlyph = null;
            _breakdown.Clear();
            _thoughtSlots.Clear();
            _traitChips.Clear();
            _thoughtsDrawn = -1;
        }

        /// <summary>Show the body while the Thoughts tab is the active one, hide it otherwise.</summary>
        void ShowThoughtsTab(bool shown)
        {
            if (_thoughtsBody != null)
                _thoughtsBody.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Draw the model. Called by <c>HudShell.Inspect</c> only for a colonist who is in the frame;
        /// redraws only when <see cref="ThoughtsTab.Version"/> moved, which is when a drawn value did.
        /// </summary>
        void SyncThoughtsTab()
        {
            if (_thoughtsBody == null || _moodValue == null || _targetWord == null || _targetValue == null
                || _meterBad == null || _meterWarn == null || _meterFill == null || _meterTick == null
                || _thoughtPager == null || _thoughtPageLabel == null || _thoughtsEmpty == null) return;
            ThoughtsTab tab = _inspect.Thoughts;
            if (tab.Version == _thoughtsDrawn) return;
            _thoughtsDrawn = tab.Version;

            Color ink = HudTokens.Convert(tab.MoodInk);
            HudText.Set(_moodValue, tab.MoodText, HudTextRole.Name);
            _moodValue.style.color = ink;

            // The bands are hers: nought to her major line, her major to her minor.
            bool lines = tab.Minor >= 0 && tab.Major >= 0;
            float major = Percent(tab.Major), minor = Percent(tab.Minor);
            _meterBad.style.display = lines ? DisplayStyle.Flex : DisplayStyle.None;
            _meterWarn.style.display = lines ? DisplayStyle.Flex : DisplayStyle.None;
            _meterBad.style.width = Length.Percent(major);
            _meterWarn.style.left = Length.Percent(major);
            _meterWarn.style.width = Length.Percent(Mathf.Max(0f, minor - major));
            _meterFill.style.width = Length.Percent(Percent(tab.Mood));
            _meterFill.style.backgroundColor = ink;
            _meterTick.style.left = Length.Percent(Percent(tab.Target));

            HudText.Set(_targetWord, tab.TargetWord, HudTextRole.Meta);
            _targetWord.tooltip = tab.TargetTip;
            HudText.Set(_targetValue, tab.TargetText, HudTextRole.Meta);

            for (int i = 0; i < _breakdown.Count && i < tab.Breakdown.Length; i++)
            {
                BreakdownLine line = tab.Breakdown[i];
                HudText.Set(_breakdown[i].Label, line.Label, HudTextRole.Body);
                _breakdown[i].Label.tooltip = line.Tip;
                HudText.Set(_breakdown[i].Value, line.Value, HudTextRole.Meta);
                _breakdown[i].Value.style.color = HudTokens.Convert(line.Ink);
            }

            bool empty = tab.Lines.Count == 0;
            _thoughtsEmpty.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
            _thoughtPager.style.display = tab.Paged ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < _thoughtSlots.Count; i++)
            {
                ThoughtSlot slot = _thoughtSlots[i];
                bool shown = i < tab.Shown.Count;
                slot.Cells.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                slot.Rail.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!shown) { slot.Root.tooltip = null; continue; }
                ThoughtLine line = tab.Shown[i];
                Color lineInk = HudTokens.Convert(line.Ink);
                HudText.Set(slot.Name, line.Name, HudTextRole.Row);
                HudText.Set(slot.Source, line.Source, HudTextRole.Meta);
                HudText.Set(slot.Lasts, line.Lasts, HudTextRole.Meta);
                HudText.Set(slot.Value, line.Value, HudTextRole.Row);
                slot.Value.style.color = lineInk;
                slot.Rail.style.backgroundColor = lineInk;
                slot.Root.tooltip = line.Tip;
            }
            HudText.Set(_thoughtPageLabel, tab.PageText, HudTextRole.Meta);
            if (_thoughtPrevGlyph != null) _thoughtPrevGlyph.Tint = tab.Page > 0 ? HudTokens.TextPrimary : HudTokens.TextFaint;
            if (_thoughtNextGlyph != null) _thoughtNextGlyph.Tint = tab.Page < tab.Pages - 1 ? HudTokens.TextPrimary : HudTokens.TextFaint;

            for (int i = 0; i < _traitChips.Count; i++)
            {
                ChipView chip = _traitChips[i];
                bool shown = i < tab.Chips.Count;
                chip.Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!shown) continue;
                TraitChip data = tab.Chips[i];
                HudText.Set(chip.Name, data.Name, HudTextRole.Row);
                HudText.Set(chip.Value, data.Value, HudTextRole.Meta);
                chip.Value.style.display = data.Value.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                chip.Root.tooltip = data.Tip;
                HudColour? tone = data.Tone == ChipTone.Good ? HudTheme.Good : data.Tone == ChipTone.Bad ? HudTheme.Bad : (HudColour?)null;
                Border(chip.Root, tone is HudColour t ? HudTokens.Convert(t.WithAlpha(0.6f)) : HudTokens.Convert(HudTheme.ControlBorder));
                chip.Root.style.backgroundColor = tone is HudColour f ? HudTokens.Convert(f.WithAlpha(0.14f)) : new StyleColor(StyleKeyword.Null);
                chip.Value.style.color = tone is HudColour v ? HudTokens.Convert(v) : HudTokens.TextDim;
            }
        }


        // ---- the traits, under the needs bars (design 51 §5f) -----------------------------

        /// <summary>The traits block: a heading and a row per trait, shown with the needs grid.</summary>
        VisualElement? _traitsBlock;
        Label? _traitsHeading;
        VisualElement? _traitsRowsGrid;
        readonly List<ThoughtRowView> _traitRows = new List<ThoughtRowView>();

        /// <summary>
        /// Build the traits block into <paramref name="tabBody"/>, straight after the needs grid so
        /// it sits under the bars. Two or three rows and a heading fit the slack the fixed body
        /// leaves under two rows of needs (<c>HudLayoutTests.TheTraitsFitUnderTheNeeds</c>).
        /// </summary>
        void BuildTraitsRows(VisualElement tabBody)
        {
            _traitsBlock = new VisualElement();
            _traitsHeading = HudText.Make(Registry.Label("ui.mind.traits"), HudTextRole.Row, ussClass: "inspect__rowname");
            _traitsHeading.style.width = StyleKeyword.Auto;
            _traitsHeading.style.height = HudLayout.CellRow;
            _traitsHeading.tooltip = Registry.Describe("ui.mind.traits");
            _traitsBlock.Add(_traitsHeading);
            _traitsRowsGrid = new VisualElement();
            _traitsBlock.Add(_traitsRowsGrid);
            _traitRows.Clear();
            tabBody.Add(_traitsBlock);
        }

        void ForgetTraitsRows()
        {
            _traitsBlock = null;
            _traitsHeading = null;
            _traitsRowsGrid = null;
            _traitRows.Clear();
        }

        /// <summary>Shown with the needs grid, and only when she has traits to show.</summary>
        void ShowTraitsRows(bool shown)
        {
            if (_traitsBlock == null) return;
            bool any = _inspect.TraitRows.Count > 0;
            _traitsBlock.style.display = shown && any ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void SyncTraitsRows(bool needsShowing)
        {
            if (_traitsBlock == null || _traitsRowsGrid == null) return;
            SyncRowList(_traitRows, _traitsRowsGrid, _inspect.TraitRows);
            ShowTraitsRows(needsShowing);
        }

        /// <summary>
        /// Make <paramref name="views"/> in <paramref name="grid"/> say what <paramref name="rows"/>
        /// says, adding and removing views to match and writing only what moved. A row whose value
        /// is empty and which carries a tooltip is a group heading, drawn in the heavier role.
        /// </summary>
        static void SyncRowList(List<ThoughtRowView> views, VisualElement grid, List<InspectRow> rows)
        {
            while (views.Count < rows.Count)
            {
                var view = new ThoughtRowView();
                view.Root = new VisualElement();
                view.Root.AddToClassList("inspect__row");
                view.Name = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "inspect__rowname");
                // A thought's or a trait's name is longer than a tile fact's, so the name takes
                // the row and the value keeps its own width at the right.
                view.Name.style.width = StyleKeyword.Auto;
                view.Name.style.flexGrow = 1;
                view.Value = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true, ussClass: "inspect__rowvalue");
                view.Value.style.flexGrow = 0;
                view.Root.Add(view.Name);
                view.Root.Add(view.Value);
                grid.Add(view.Root);
                views.Add(view);
            }
            while (views.Count > rows.Count)
            {
                grid.Remove(views[views.Count - 1].Root);
                views.RemoveAt(views.Count - 1);
            }

            for (int i = 0; i < rows.Count; i++)
            {
                ThoughtRowView view = views[i];
                InspectRow row = rows[i];

                bool heading = string.IsNullOrEmpty(row.Value) && row.Tooltip != null;
                if (view.LastName != row.Name || view.LastHeading != heading)
                {
                    view.LastName = row.Name;
                    view.LastHeading = heading;
                    // Apply as well as Set: Set only cases the text, and the step and weight are the
                    // role's, so a heading written with Set alone drew at the rows' own size.
                    HudTextRole role = heading ? HudTextRole.Row : HudTextRole.Meta;
                    HudText.Apply(view.Name, role);
                    HudText.Set(view.Name, row.Name, role);
                }
                if (view.LastValue != row.Value)
                {
                    view.LastValue = row.Value;
                    HudText.Set(view.Value, row.Value, HudTextRole.Meta);
                    view.TintKnown = false;
                }

                // Compared by its channels, not by Hex, which formats a string on every refresh.
                HudColour? tint = row.Tint;
                if (!view.TintKnown || !SameColour(view.LastTint, tint))
                {
                    view.TintKnown = true;
                    view.LastTint = tint;
                    if (row.Tint is HudColour colour) view.Value.style.color = HudTokens.Convert(colour);
                    else view.Value.style.color = StyleKeyword.Null;
                }

                if (view.LastTooltip != row.Tooltip)
                {
                    view.LastTooltip = row.Tooltip;
                    view.Root.tooltip = row.Tooltip;
                }
            }
        }
    }
}
