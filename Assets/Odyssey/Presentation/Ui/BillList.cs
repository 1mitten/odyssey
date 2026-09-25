#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The bill list: one control for every station that is told to make things (design 49) —
    /// the electric cooker and the campfire today, a crafting bench tomorrow. It draws a
    /// <see cref="BillsModel"/> and sends that model's intents; it decides nothing itself, and knows
    /// nothing about cooking.
    ///
    /// <para><b>Three sections and no gaps between them.</b> A status strip (only while the
    /// station has a problem: no power, with its switch), the Bills strip and its rows, and the
    /// full-width Add a bill. Each section opens on a strip with a 3 px bar in its colour and is
    /// closed by a 1 px rule, so the pane reads as stacked bands rather than as boxes floating in
    /// it.</para>
    ///
    /// <para><b>Its height is its content.</b> The pane is docked at the bottom and grows upward,
    /// so Add a bill and everything under it stay where they are while bills are added; the rows
    /// above it take the growth. The earlier list kept five rows' height whatever it held, which
    /// at 48 px a row was 240 px of empty pane under one bill.</para>
    ///
    /// <para>Rows are built once, <see cref="BillsModel.MaxRows"/> of them, and shown or hidden:
    /// a refresh rewrites words only when <see cref="BillsModel.Signature"/> moves, and never
    /// builds an element.</para>
    /// </summary>
    public sealed class BillList : VisualElement
    {
        public const string RootClass = "bills";
        public const string ControlClass = "bills__ctl";

        readonly BillsModel _model;
        readonly Action<Intent> _send;

        readonly VisualElement _status;
        readonly Label _statusWord;
        readonly VisualElement _switch;
        readonly Label _switchLabel;
        readonly Label _count;
        readonly VisualElement _recipeLine;
        readonly Label _needs;
        readonly Label _supply;
        readonly VisualElement _empty;
        readonly VisualElement _add;
        readonly List<RowView> _rows = new List<RowView>();
        int _signature = int.MinValue;

        sealed class RowView
        {
            public VisualElement Root = null!;
            public Label Number = null!;
            public VisualElement Icon = null!;
            public HudGlyph IconGlyph = null!;
            public VisualElement NameColumn = null!;
            public Label Name = null!;
            public Label State = null!;
            public VisualElement Mode = null!;
            public Label ModeLabel = null!;
            public VisualElement Stepper = null!;
            public VisualElement Less = null!, More = null!;
            public Label Target = null!;
            public VisualElement ProgressColumn = null!;
            public Label Progress = null!;
            public VisualElement Fill = null!;
            public VisualElement Up = null!, Down = null!;
            public VisualElement Pause = null!;
            public PathGlyph PauseBars = null!, PlayTriangle = null!;
            public BillRow? Row;
        }

        /// <param name="model">The list to draw. The host refreshes it; <see cref="Sync"/> draws it.</param>
        /// <param name="send">Where every button's intent goes.</param>
        public BillList(BillsModel model, Action<Intent> send)
        {
            _model = model;
            _send = send;
            AddToClassList(RootClass);
            style.flexShrink = 0;
            style.borderTopWidth = HudTheme.BorderWidth;
            style.borderTopColor = HudTokens.PanelBorder;

            // ---- the status strip: warn, "No power", and the station's switch
            _status = Strip(BillsLayout.StatusHeight, HudTheme.Warn.WithAlpha(BillsLayout.WarnWash), HudTokens.Warn);
            _status.Add(new PathGlyph(HudIcons.NoPower, BillsLayout.StatusGlyph, HudTokens.Warn));
            _statusWord = HudText.Make(string.Empty, HudTextRole.Row);
            Bold(_statusWord);
            _statusWord.style.color = HudTokens.Warn;
            _statusWord.style.marginLeft = HudLayout.RowIconGap;
            _statusWord.style.flexGrow = 1;
            _status.Add(_statusWord);

            _switch = new VisualElement();
            _switch.style.flexDirection = FlexDirection.Row;
            _switch.style.alignItems = Align.Center;
            _switch.style.height = BillsLayout.SwitchHeight;
            _switch.style.paddingLeft = 8;
            _switch.style.paddingRight = 10;
            _switch.style.backgroundColor = HudTokens.PanelFill;
            Border(_switch, HudTokens.Convert(HudTheme.Good.WithAlpha(BillsLayout.StepBorder)));
            _switch.Add(new PathGlyph(HudIcons.Power, BillsLayout.SwitchGlyph, HudTokens.Good));
            _switchLabel = HudText.Make(string.Empty, HudTextRole.Row);
            _switchLabel.style.color = HudTokens.Good;
            _switchLabel.style.marginLeft = 7;
            _switch.Add(_switchLabel);
            _switch.tooltip = "Switch it on or off - at once, nobody is sent";
            _switch.RegisterCallback<ClickEvent>(e =>
            {
                if (_model.PressSwitch(out Intent command)) _send(command);
                e.StopPropagation();
            });
            _status.Add(_switch);
            Add(_status);

            // ---- the Bills strip: the heading in the accent and the count beside it
            VisualElement head = Strip(BillsLayout.SectionHeadHeight, HudTheme.Accent.WithAlpha(BillsLayout.AccentWash), HudTokens.Accent);
            Label heading = HudText.Make(Registry.Label("ui.bill.heading"), HudTextRole.PanelLabel);
            heading.style.color = HudTokens.Accent;
            head.Add(heading);
            _count = HudText.Make(string.Empty, HudTextRole.Hotkey, numeric: true);
            _count.RemoveFromClassList(HudText.KeyCapClass);
            _count.style.color = HudTokens.TextMeta;
            _count.style.marginLeft = 6;
            head.Add(_count);
            Add(head);

            // ---- what one product takes, and what the map holds of it (design 48 §14; owner,
            // 2026-09-25: "I didn't know what ingredients I needed"). One line under the strip:
            // the recipe's needs on the left, the supply on the right, in warn when there is none.
            _recipeLine = new VisualElement();
            _recipeLine.style.flexDirection = FlexDirection.Row;
            _recipeLine.style.alignItems = Align.Center;
            _recipeLine.style.flexShrink = 0;
            _recipeLine.style.height = BillsLayout.RecipeLineHeight;
            _recipeLine.style.paddingLeft = BillsLayout.StripPadLeft;
            _recipeLine.style.paddingRight = BillsLayout.SidePad;
            _recipeLine.style.borderBottomWidth = HudTheme.BorderWidth;
            _recipeLine.style.borderBottomColor = HudTokens.Convert(HudTheme.RowRule);
            _needs = HudText.Make(string.Empty, HudTextRole.Meta);
            _needs.style.color = HudTokens.TextMeta;
            _needs.style.flexGrow = 1;
            _needs.style.whiteSpace = WhiteSpace.NoWrap;
            _recipeLine.Add(_needs);
            _supply = HudText.Make(string.Empty, HudTextRole.Meta);
            _supply.style.whiteSpace = WhiteSpace.NoWrap;
            _supply.style.marginLeft = BillsLayout.ColumnGap;
            _recipeLine.Add(_supply);
            Add(_recipeLine);

            // ---- the rows, and the one line an empty list shows
            _empty = new VisualElement();
            _empty.style.height = BillsLayout.RowHeight;
            _empty.style.justifyContent = Justify.Center;
            _empty.style.paddingLeft = BillsLayout.StripPadLeft;
            _empty.style.borderBottomWidth = HudTheme.BorderWidth;
            _empty.style.borderBottomColor = HudTokens.Convert(HudTheme.RowRule);
            Label none = HudText.Make(Registry.Label("ui.bill.none"), HudTextRole.Body);
            none.style.color = HudTokens.TextMeta;
            _empty.Add(none);
            Add(_empty);

            for (int i = 0; i < BillsModel.MaxRows; i++)
            {
                RowView row = BuildRow();
                _rows.Add(row);
                Add(row.Root);
            }

            // ---- Add a bill: the only filled accent control on the pane
            var addWrap = new VisualElement();
            addWrap.style.paddingTop = BillsLayout.AddPadY;
            addWrap.style.paddingBottom = BillsLayout.AddPadY;
            addWrap.style.paddingLeft = BillsLayout.AddPadX;
            addWrap.style.paddingRight = BillsLayout.AddPadX;
            addWrap.style.borderBottomWidth = HudTheme.BorderWidth;
            addWrap.style.borderBottomColor = HudTokens.PanelBorder;

            _add = new VisualElement();
            _add.AddToClassList("bills__add");
            _add.style.height = BillsLayout.AddHeight;
            _add.style.flexDirection = FlexDirection.Row;
            _add.style.alignItems = Align.Center;
            _add.style.justifyContent = Justify.Center;
            _add.style.backgroundColor = HudTokens.Accent;
            _add.Add(new PathGlyph(HudIcons.Plus, BillsLayout.AddGlyph, HudTokens.OnAccent, stroke: HudIcons.AddStroke));
            Label addLabel = HudText.Make(Registry.Label("ui.bill.add"), HudTextRole.Row);
            Bold(addLabel);
            addLabel.style.color = HudTokens.OnAccent;
            addLabel.style.marginLeft = 8;
            _add.Add(addLabel);
            _add.RegisterCallback<ClickEvent>(e =>
            {
                if (_model.PressAdd(out Intent command)) _send(command);
                e.StopPropagation();
            });
            addWrap.Add(_add);
            Add(addWrap);
        }

        RowView BuildRow()
        {
            var view = new RowView();
            view.Root = new VisualElement();
            view.Root.style.flexDirection = FlexDirection.Row;
            view.Root.style.alignItems = Align.Center;
            view.Root.style.height = BillsLayout.RowHeight;
            view.Root.style.flexShrink = 0;
            view.Root.style.paddingLeft = BillsLayout.SidePad;
            view.Root.style.paddingRight = BillsLayout.SidePad;
            view.Root.style.borderBottomWidth = HudTheme.BorderWidth;
            view.Root.style.borderBottomColor = HudTokens.Convert(HudTheme.RowRule);

            // 1. its place in the list
            view.Number = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true);
            view.Number.style.width = BillsLayout.IndexColumn;
            view.Number.style.flexShrink = 0;
            view.Number.style.color = HudTokens.TextFaint;
            view.Number.style.unityTextAlign = TextAnchor.MiddleCenter;
            view.Root.Add(view.Number);

            // 2. what it makes: a tile with the category's edge under it
            view.Icon = new VisualElement();
            view.Icon.style.width = BillsLayout.IconColumn;
            view.Icon.style.height = BillsLayout.Control;
            view.Icon.style.flexShrink = 0;
            view.Icon.style.marginLeft = BillsLayout.ColumnGap;
            view.Icon.style.alignItems = Align.Center;
            view.Icon.style.justifyContent = Justify.Center;
            view.Icon.style.backgroundColor = HudTokens.PanelBorder;
            view.Icon.style.borderBottomWidth = BillsLayout.IconEdge;
            view.IconGlyph = new HudGlyph(HudGlyphKind.CategoryFood, 16f, HudTokens.TextMeta);
            view.Icon.Add(view.IconGlyph);
            view.Root.Add(view.Icon);

            // 3. its name, and the line under it
            view.NameColumn = new VisualElement();
            view.NameColumn.style.flexGrow = 1;
            view.NameColumn.style.flexShrink = 1;
            view.NameColumn.style.minWidth = 0;
            view.NameColumn.style.marginLeft = BillsLayout.ColumnGap;
            view.NameColumn.style.justifyContent = Justify.Center;
            view.Name = HudText.Make(string.Empty, HudTextRole.Row);
            view.Name.style.color = HudTokens.TextPrimary;
            view.Name.style.overflow = Overflow.Hidden;
            view.Name.style.textOverflow = TextOverflow.Ellipsis;
            view.Name.style.whiteSpace = WhiteSpace.NoWrap;
            view.Name.style.unityTextAlign = TextAnchor.MiddleLeft;
            view.NameColumn.Add(view.Name);
            view.State = HudText.Make(string.Empty, HudTextRole.Meta);
            view.State.style.marginTop = BillsLayout.StatusLineGap;
            view.State.style.whiteSpace = WhiteSpace.NoWrap;
            view.State.style.unityTextAlign = TextAnchor.MiddleLeft;
            view.NameColumn.Add(view.State);
            view.Root.Add(view.NameColumn);

            // 4. how it counts: press to go round
            view.Mode = new VisualElement();
            view.Mode.AddToClassList(ControlClass);
            view.Mode.style.width = BillsLayout.ModeColumn;
            view.Mode.style.height = BillsLayout.Control;
            view.Mode.style.flexShrink = 0;
            view.Mode.style.marginLeft = BillsLayout.ColumnGap;
            view.Mode.style.flexDirection = FlexDirection.Row;
            view.Mode.style.alignItems = Align.Center;
            view.Mode.style.justifyContent = Justify.SpaceBetween;
            view.Mode.style.paddingLeft = 9;
            view.Mode.style.paddingRight = 8;
            view.ModeLabel = HudText.Make(string.Empty, HudTextRole.Body);
            view.ModeLabel.style.color = HudTokens.TextPrimary;
            view.ModeLabel.style.whiteSpace = WhiteSpace.NoWrap;
            view.Mode.Add(view.ModeLabel);
            view.Mode.Add(new PathGlyph(HudIcons.Cycle, BillsLayout.ModeGlyph, HudTokens.TextDim, stroke: HudIcons.CycleStroke));
            view.Mode.tooltip = "Press for the next way of counting";
            view.Mode.RegisterCallback<ClickEvent>(e =>
            {
                if (view.Row != null) _send(_model.PressMode(view.Row));
                e.StopPropagation();
            });
            view.Root.Add(view.Mode);

            // 5. the target: minus, the number, plus (shift for ten)
            view.Stepper = new VisualElement();
            view.Stepper.style.width = BillsLayout.StepperColumn;
            view.Stepper.style.flexShrink = 0;
            view.Stepper.style.marginLeft = BillsLayout.ColumnGap;
            view.Stepper.style.flexDirection = FlexDirection.Row;
            view.Stepper.style.alignItems = Align.Center;
            view.Less = StepButton(HudIcons.Minus, HudTheme.Bad, e =>
            {
                if (view.Row != null && _model.PressNudge(view.Row, e.shiftKey ? -10 : -1, out Intent c)) _send(c);
            });
            view.Stepper.Add(view.Less);
            view.Target = HudText.Make(string.Empty, HudTextRole.Row, numeric: true);
            view.Target.style.flexGrow = 1;
            view.Target.style.color = HudTokens.TextPrimary;
            view.Target.style.unityTextAlign = TextAnchor.MiddleCenter;
            view.Stepper.Add(view.Target);
            view.More = StepButton(HudIcons.Plus, HudTheme.Good, e =>
            {
                if (view.Row != null && _model.PressNudge(view.Row, e.shiftKey ? 10 : 1, out Intent c)) _send(c);
            });
            view.Stepper.Add(view.More);
            view.Less.tooltip = "Fewer (shift for ten)";
            view.More.tooltip = "More (shift for ten)";
            view.Root.Add(view.Stepper);

            // 6. how far it has got: the count over a track
            view.ProgressColumn = new VisualElement();
            view.ProgressColumn.style.width = BillsLayout.ProgressColumn;
            view.ProgressColumn.style.flexShrink = 0;
            view.ProgressColumn.style.marginLeft = BillsLayout.ColumnGap;
            view.Progress = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true);
            view.Progress.style.color = HudTokens.TextMeta;
            view.Progress.style.whiteSpace = WhiteSpace.NoWrap;
            view.Progress.style.unityTextAlign = TextAnchor.MiddleCenter;
            view.ProgressColumn.Add(view.Progress);
            var track = new VisualElement();
            track.style.height = BillsLayout.ProgressTrack;
            track.style.marginTop = BillsLayout.ProgressGap;
            track.style.backgroundColor = HudTokens.PanelBorder;
            view.Fill = new VisualElement();
            view.Fill.style.position = Position.Absolute;
            view.Fill.style.left = 0;
            view.Fill.style.top = 0;
            view.Fill.style.bottom = 0;
            view.Fill.style.backgroundColor = HudTokens.Accent;
            track.Add(view.Fill);
            view.ProgressColumn.Add(track);
            view.Root.Add(view.ProgressColumn);

            // 7. the actions: move, pause, remove
            var actions = new VisualElement();
            actions.style.width = BillsLayout.ActionsColumn;
            actions.style.flexShrink = 0;
            actions.style.marginLeft = BillsLayout.ColumnGap;
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.justifyContent = Justify.FlexEnd;

            var order = new VisualElement();
            order.AddToClassList(ControlClass);
            order.style.width = BillsLayout.Control;
            order.style.height = BillsLayout.Control;
            view.Up = HalfButton(HudIcons.ChevronUp, _ =>
            {
                if (view.Row != null && _model.PressUp(view.Row, out Intent c)) _send(c);
            });
            view.Up.tooltip = "Move up: the top bill is worked first";
            view.Up.style.borderBottomWidth = HudTheme.BorderWidth;
            view.Up.style.borderBottomColor = HudTokens.Convert(HudTheme.RowRule);
            view.Down = HalfButton(HudIcons.ChevronDown, _ =>
            {
                if (view.Row != null && _model.PressDown(view.Row, out Intent c)) _send(c);
            });
            view.Down.tooltip = "Move down";
            order.Add(view.Up);
            order.Add(view.Down);
            actions.Add(order);

            view.Pause = ActionButton(_ =>
            {
                if (view.Row != null) _send(_model.PressSuspend(view.Row));
            });
            view.PauseBars = new PathGlyph(HudIcons.PauseBars, BillsLayout.ActionGlyph, HudTokens.TextPrimary, stroke: 2.2f);
            view.PlayTriangle = new PathGlyph(HudIcons.PlayTriangle, BillsLayout.ActionGlyph, HudTokens.TextPrimary, fill: true);
            view.Pause.Add(view.PauseBars);
            view.Pause.Add(view.PlayTriangle);
            actions.Add(view.Pause);

            VisualElement remove = ActionButton(_ =>
            {
                if (view.Row != null) _send(_model.PressRemove(view.Row));
            });
            remove.Add(new PathGlyph(HudIcons.Bin, BillsLayout.ActionGlyph, HudTokens.TextMeta));
            remove.tooltip = "Remove this bill";
            actions.Add(remove);
            view.Root.Add(actions);

            view.Root.style.display = DisplayStyle.None;
            return view;
        }

        /// <summary>
        /// Bring the control to what the model says. The host calls this after refreshing the
        /// model; words are rewritten only when the model's signature has moved.
        /// </summary>
        public void Sync()
        {
            style.display = _model.Showing ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_model.Showing) return;

            int signature = _model.Signature();
            if (signature == _signature) return;
            _signature = signature;

            _status.style.display = _model.HasProblem ? DisplayStyle.Flex : DisplayStyle.None;
            HudText.Set(_statusWord, _model.ProblemLabel, HudTextRole.Row);
            _switch.style.display = _model.HasSwitch ? DisplayStyle.Flex : DisplayStyle.None;
            HudText.Set(_switchLabel, _model.SwitchLabel, HudTextRole.Row);

            HudText.Set(_count, _model.CountLabel, HudTextRole.Hotkey);
            _recipeLine.style.display = _model.Needs.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            HudText.Set(_needs, _model.Needs, HudTextRole.Meta);
            HudText.Set(_supply, _model.Supply, HudTextRole.Meta);
            _supply.style.color = _model.NoSupply ? HudTokens.Warn : HudTokens.TextMeta;
            _supply.tooltip = _model.NoSupply ? "Grow carrots, or use the debug menu's Give carrots" : null;
            _empty.style.display = _model.Rows.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _add.style.opacity = _model.Full ? BillsLayout.DisabledOpacity : 1f;
            _add.tooltip = _model.Full ? "This station holds " + BillsModel.MaxRows + " bills" : Registry.Label("ui.bill.add");

            for (int i = 0; i < _rows.Count; i++)
            {
                RowView view = _rows[i];
                if (i >= _model.Rows.Count)
                {
                    view.Row = null;
                    view.Root.style.display = DisplayStyle.None;
                    continue;
                }
                Draw(view, _model.Rows[i]);
            }
        }

        static void Draw(RowView view, BillRow row)
        {
            view.Row = row;
            view.Root.style.display = DisplayStyle.Flex;

            HudText.Set(view.Number, row.Number, HudTextRole.Meta);

            Color hue = HudTokens.Convert(HudTheme.ItemCategoryHue(row.ProductCategory));
            view.Icon.style.borderBottomColor = hue;
            view.IconGlyph.Kind = HudShell.CategoryGlyph(row.ProductCategory);

            HudText.Set(view.Name, row.Recipe, HudTextRole.Row);
            view.Name.tooltip = row.Recipe;
            HudText.Set(view.State, row.State, HudTextRole.Meta);
            view.State.style.display = row.State.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            view.State.style.color = row.Tone switch
            {
                BillTone.Warn => HudTokens.Warn,
                BillTone.Dim => HudTokens.TextDim,
                _ => HudTokens.TextMeta,
            };

            HudText.Set(view.ModeLabel, row.ModeLabel, HudTextRole.Body);
            HudText.Set(view.Target, row.Target, HudTextRole.Row);
            view.Less.style.opacity = row.CanLess ? 1f : BillsLayout.DisabledOpacity;
            view.More.style.opacity = row.CanMore ? 1f : BillsLayout.DisabledOpacity;
            HudText.Set(view.Progress, row.Progress, HudTextRole.Meta);
            view.Fill.style.width = Length.Percent(row.ProgressPerMille / 10f);

            view.Up.style.opacity = row.IsFirst ? BillsLayout.DisabledOpacity : 1f;
            view.Down.style.opacity = row.IsLast ? BillsLayout.DisabledOpacity : 1f;
            view.PauseBars.style.display = row.Suspended ? DisplayStyle.None : DisplayStyle.Flex;
            view.PlayTriangle.style.display = row.Suspended ? DisplayStyle.Flex : DisplayStyle.None;
            view.Pause.tooltip = row.Suspended ? "Resume" : "Pause";

            // A paused bill fades its middle; its actions stay whole so it can be resumed.
            float middle = row.Suspended ? BillsLayout.PausedOpacity : 1f;
            view.Icon.style.opacity = middle;
            view.NameColumn.style.opacity = middle;
            view.Mode.style.opacity = middle;
            view.Stepper.style.opacity = middle;
            view.ProgressColumn.style.opacity = middle;
        }

        // ---- pieces ------------------------------------------------------------------------------

        /// <summary>A section's opening strip: a wash, a 3 px bar down its left and a rule under it.</summary>
        static VisualElement Strip(int height, HudColour wash, Color bar)
        {
            var strip = new VisualElement();
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.alignItems = Align.Center;
            strip.style.flexShrink = 0;
            strip.style.height = height;
            strip.style.paddingLeft = BillsLayout.StripPadLeft - BillsLayout.SectionBar;
            strip.style.paddingRight = BillsLayout.SidePad;
            strip.style.backgroundColor = HudTokens.Convert(wash);
            strip.style.borderLeftWidth = BillsLayout.SectionBar;
            strip.style.borderLeftColor = bar;
            strip.style.borderBottomWidth = HudTheme.BorderWidth;
            strip.style.borderBottomColor = HudTokens.PanelBorder;
            return strip;
        }

        /// <summary>The stepper's minus and plus: a wash and a border in the one hue, the mark in it.</summary>
        static VisualElement StepButton(string path, HudColour hue, Action<ClickEvent> press)
        {
            var box = Square();
            box.style.backgroundColor = HudTokens.Convert(hue.WithAlpha(BillsLayout.StepFill));
            Border(box, HudTokens.Convert(hue.WithAlpha(BillsLayout.StepBorder)));
            box.Add(new PathGlyph(path, BillsLayout.StepGlyph, HudTokens.Convert(hue), stroke: HudIcons.StepStroke));
            box.RegisterCallback<ClickEvent>(e =>
            {
                press(e);
                e.StopPropagation();
            });
            return box;
        }

        /// <summary>Pause or remove: a 28 px square in the control border, four from the one before.</summary>
        static VisualElement ActionButton(Action<ClickEvent> press)
        {
            var box = Square();
            box.AddToClassList(ControlClass);
            box.style.marginLeft = BillsLayout.ActionGap;
            box.RegisterCallback<ClickEvent>(e =>
            {
                press(e);
                e.StopPropagation();
            });
            return box;
        }

        /// <summary>One half of the reorder pair: the full width of the box, half its height.</summary>
        static VisualElement HalfButton(string path, Action<ClickEvent> press)
        {
            var half = new VisualElement();
            half.style.flexGrow = 1;
            half.style.alignItems = Align.Center;
            half.style.justifyContent = Justify.Center;
            half.Add(new PathGlyph(path, BillsLayout.ReorderGlyph, HudTokens.TextMeta, stroke: 2.4f));
            half.RegisterCallback<ClickEvent>(e =>
            {
                press(e);
                e.StopPropagation();
            });
            return half;
        }

        static VisualElement Square()
        {
            var box = new VisualElement();
            box.style.width = BillsLayout.Control;
            box.style.height = BillsLayout.Control;
            box.style.flexShrink = 0;
            box.style.alignItems = Align.Center;
            box.style.justifyContent = Justify.Center;
            return box;
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

        /// <summary>The design's 600 weight on a 14 px word: the renderer's bold, as <see cref="HudType.BoldFrom"/> says.</summary>
        static void Bold(Label label) => label.style.unityFontStyleAndWeight = FontStyle.Bold;
    }
}
