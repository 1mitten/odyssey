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
    /// The bill list on a cooking station's pane (design 48 §5): a heading, a line of state, and up
    /// to five rows, each saying what it makes, how it counts and how far it has got, with the
    /// buttons that change it. Everything it decides is <see cref="BillsModel"/>'s; this lays the
    /// rows out and hands the model's intents to the world.
    ///
    /// <para><b>One height whatever the list holds.</b> The block is laid out for
    /// <see cref="BillsModel.MaxRows"/> rows from the start and the unused ones are hidden without
    /// giving their space back, so adding a bill never moves anything under the pointer. The
    /// inspect pane is docked at the bottom and grows upward; a list that grew by a row would lift
    /// every control above it, which is the storage pane's lesson (design 26 §12).</para>
    /// </summary>
    public sealed partial class HudShell
    {
        readonly BillsModel _bills = new BillsModel();

        VisualElement? _billsBlock;
        Label? _billsStatus;
        Label? _billsAdd;
        readonly List<BillRowView> _billRows = new List<BillRowView>();
        int _billsSignature = int.MinValue;

        const int BillRowHeight = 30;
        const int BillHeaderHeight = 34;
        const int BillStatusHeight = 22;

        public const string BillsBlockClass = "bills__block";

        sealed class BillRowView
        {
            public VisualElement Root = null!;
            public Label Recipe = null!;
            public Label Mode = null!;
            public Label Target = null!;
            public Label Progress = null!;
            public Label State = null!;
            public VisualElement Less = null!, More = null!, Pause = null!, Up = null!, Down = null!, Remove = null!;
            public HudGlyph PauseGlyph = null!;
            public BillRow? Row;
        }

        /// <summary>
        /// Build the block under a station's header, once per subject. Hidden entirely for anything
        /// that is not a station, and shown by <see cref="SyncBills"/> the moment the answer says
        /// one stands there.
        /// </summary>
        void BuildBills(VisualElement into)
        {
            _billRows.Clear();
            _billsSignature = int.MinValue;

            _billsBlock = new VisualElement();
            _billsBlock.AddToClassList(BillsBlockClass);
            _billsBlock.style.flexShrink = 0;
            _billsBlock.style.paddingLeft = 14;
            _billsBlock.style.paddingRight = 14;
            _billsBlock.style.paddingBottom = 8;
            _billsBlock.style.height = BillHeaderHeight + BillStatusHeight + BillsModel.MaxRows * BillRowHeight + 8;
            _billsBlock.style.display = DisplayStyle.None;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.height = BillHeaderHeight;
            Label heading = HudText.Make(Registry.Label("ui.bill.heading").ToUpperInvariant(), HudTextRole.Meta);
            heading.style.letterSpacing = 2;
            heading.style.flexGrow = 1;
            header.Add(heading);
            _billsAdd = HudText.Make(Registry.Label("ui.bill.add"), HudTextRole.Body);
            _billsAdd.style.color = HudTokens.Convert(HudTheme.Accent);
            _billsAdd.tooltip = Registry.Label("ui.bill.add");
            _billsAdd.RegisterCallback<ClickEvent>(_ =>
            {
                if (_bills.PressAdd(out Intent add)) SendBill(add);
            });
            header.Add(_billsAdd);
            _billsBlock.Add(header);

            _billsStatus = HudText.Make(string.Empty, HudTextRole.Meta);
            _billsStatus.style.height = BillStatusHeight;
            _billsBlock.Add(_billsStatus);

            for (int i = 0; i < BillsModel.MaxRows; i++) _billRows.Add(BillRowElement(_billsBlock));

            into.Add(_billsBlock);
        }

        BillRowView BillRowElement(VisualElement into)
        {
            var view = new BillRowView();
            view.Root = new VisualElement();
            view.Root.style.flexDirection = FlexDirection.Row;
            view.Root.style.alignItems = Align.Center;
            view.Root.style.height = BillRowHeight;
            view.Root.style.flexShrink = 0;

            view.Recipe = HudText.Make(string.Empty, HudTextRole.Body);
            view.Recipe.style.width = 96;
            view.Root.Add(view.Recipe);

            // The mode is a button that goes round: until you have, make, forever.
            view.Mode = HudText.Make(string.Empty, HudTextRole.Meta);
            view.Mode.style.width = 92;
            view.Mode.style.color = HudTokens.Convert(HudTheme.Accent);
            view.Mode.RegisterCallback<ClickEvent>(_ =>
            {
                if (view.Row != null) SendBill(_bills.PressMode(view.Row));
            });
            view.Root.Add(view.Mode);

            view.Less = GlyphButton(HudGlyphKind.ChevronLeft, e =>
            {
                if (view.Row != null && _bills.PressNudge(view.Row, e.shiftKey ? -10 : -1, out Intent c)) SendBill(c);
            });
            view.Root.Add(view.Less);
            view.Target = HudText.Make(string.Empty, HudTextRole.Row, numeric: true);
            view.Target.style.width = 30;
            view.Target.style.unityTextAlign = TextAnchor.MiddleCenter;
            view.Root.Add(view.Target);
            view.More = GlyphButton(HudGlyphKind.ChevronRight, e =>
            {
                if (view.Row != null && _bills.PressNudge(view.Row, e.shiftKey ? 10 : 1, out Intent c)) SendBill(c);
            });
            view.Root.Add(view.More);

            view.Progress = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true);
            view.Progress.style.width = 56;
            view.Progress.style.unityTextAlign = TextAnchor.MiddleRight;
            view.Root.Add(view.Progress);

            view.State = HudText.Make(string.Empty, HudTextRole.Meta);
            view.State.style.flexGrow = 1;
            view.State.style.marginLeft = 8;
            view.Root.Add(view.State);

            view.Pause = GlyphButton(HudGlyphKind.Pause, _ =>
            {
                if (view.Row != null) SendBill(_bills.PressSuspend(view.Row));
            });
            view.PauseGlyph = (HudGlyph)view.Pause[0];
            view.Root.Add(view.Pause);
            view.Up = GlyphButton(HudGlyphKind.ChevronUp, _ =>
            {
                if (view.Row != null && _bills.PressUp(view.Row, out Intent c)) SendBill(c);
            });
            view.Root.Add(view.Up);
            view.Down = GlyphButton(HudGlyphKind.ChevronDown, _ =>
            {
                if (view.Row != null && _bills.PressDown(view.Row, out Intent c)) SendBill(c);
            });
            view.Root.Add(view.Down);
            view.Remove = GlyphButton(HudGlyphKind.Close, _ =>
            {
                if (view.Row != null) SendBill(_bills.PressRemove(view.Row));
            });
            view.Root.Add(view.Remove);

            // Hidden but still taking its space: see the class comment.
            view.Root.style.visibility = Visibility.Hidden;
            into.Add(view.Root);
            return view;
        }

        static VisualElement GlyphButton(HudGlyphKind kind, Action<ClickEvent> press)
        {
            var box = new VisualElement();
            box.style.width = 20;
            box.style.height = 20;
            box.style.alignItems = Align.Center;
            box.style.justifyContent = Justify.Center;
            box.Add(new HudGlyph(kind, 11f, HudTokens.TextDim));
            box.RegisterCallback<ClickEvent>(e => press(e));
            return box;
        }

        /// <summary>
        /// Bring the block to what the frame says, rebuilding the words only when the station's
        /// bills or its readiness have changed — the pane refreshes fifteen times a second.
        /// </summary>
        void SyncBills(WorldSnapshot frame)
        {
            if (_billsBlock == null) return;

            CellRef cell = _inspect.Cell;
            _bills.Refresh(frame, cell, _inspect.TileCellIndex, _inspect.TileEdifice);
            _billsBlock.style.display = _bills.Showing ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_bills.Showing) return;

            int signature = BillsSignature();
            if (signature == _billsSignature) return;
            _billsSignature = signature;

            HudText.Set(_billsStatus!, _bills.Status, HudTextRole.Meta);
            _billsStatus!.style.color = HudTokens.Convert(_bills.Ready ? HudTheme.TextMeta : HudTheme.Warn);
            _billsAdd!.style.opacity = _bills.Full ? 0.35f : 1f;

            for (int i = 0; i < _billRows.Count; i++)
            {
                BillRowView view = _billRows[i];
                if (i >= _bills.Rows.Count)
                {
                    view.Row = null;
                    view.Root.style.visibility = Visibility.Hidden;
                    continue;
                }

                BillRow row = _bills.Rows[i];
                view.Row = row;
                view.Root.style.visibility = Visibility.Visible;
                view.Root.style.opacity = row.Suspended ? 0.55f : 1f;
                HudText.Set(view.Recipe, row.Recipe, HudTextRole.Body);
                HudText.Set(view.Mode, row.ModeLabel, HudTextRole.Meta);
                view.Mode.style.color = HudTokens.Convert(HudTheme.Accent);
                HudText.Set(view.Target, row.Target, HudTextRole.Row);
                HudText.Set(view.Progress, row.Progress, HudTextRole.Meta);
                HudText.Set(view.State, row.State, HudTextRole.Meta);
                bool counts = row.Mode != BillModeHandle.Forever;
                view.Less.style.visibility = counts ? Visibility.Visible : Visibility.Hidden;
                view.More.style.visibility = counts ? Visibility.Visible : Visibility.Hidden;
                view.Up.style.opacity = row.IsFirst ? 0.3f : 1f;
                view.Down.style.opacity = row.IsLast ? 0.3f : 1f;
                view.PauseGlyph.Kind = row.Suspended ? HudGlyphKind.Play : HudGlyphKind.Pause;
                view.Pause.tooltip = Registry.Label("ui.bill.suspended");
            }
        }

        int BillsSignature()
        {
            unchecked
            {
                int hash = _bills.Ready ? 17 : 19;
                hash = hash * 31 + _bills.Rows.Count;
                for (int i = 0; i < _bills.Rows.Count; i++)
                {
                    BillRow row = _bills.Rows[i];
                    hash = hash * 31 + row.Mode;
                    hash = hash * 31 + row.Target.GetHashCode();
                    hash = hash * 31 + row.Progress.GetHashCode();
                    hash = hash * 31 + (row.Suspended ? 1 : 0) + (row.Satisfied ? 2 : 0);
                }
                return hash;
            }
        }

        /// <summary>An intent like every command, applied at once while paused (design 48 §5).</summary>
        void SendBill(Intent intent) => _boot?.World?.Intents.Submit(intent);
    }
}
