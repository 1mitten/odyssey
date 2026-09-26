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
    /// <see cref="HudShell"/>: the trade window (design 65 §6; the owner's mockups 28a Sell and
    /// 28b Buy). A pausing modal over the settings window's scrim, 820 wide and centred: a header
    /// with the trader's face, name and stay; the Sell | Buy switch; the column heads; the list,
    /// category headings between items; the foot's four figures; and Reset, the reason, Cancel and
    /// Confirm.
    ///
    /// <para><b>Everything it decides is <see cref="TradeModel"/>'s</b>, in the fast tier: which
    /// lines, what they say, what is clamped and what Confirm sends. This file lays the model out
    /// and hands its intents on. Every measurement is <see cref="TradeLayout"/>'s and every colour a
    /// <see cref="HudTheme"/> token; the view writes neither.</para>
    ///
    /// <para><b>It opens itself</b> on the fast cadence, the frame a session becomes ready, and holds
    /// the colony's clock while it is up (<c>OdysseyBootstrap.ModalHeld</c>, a gate beside the
    /// wake's and never a speed, so nothing about it is saved).</para>
    /// </summary>
    public sealed partial class HudShell
    {
        readonly TradeModel _trade = new TradeModel();
        readonly List<Intent> _tradeOrders = new List<Intent>();
        readonly List<TradeRowUi> _tradeRows = new List<TradeRowUi>();
        HudModal _tradeModal = null!;
        VisualElement _tradeList = null!;
        Label _tradeName = null!, _tradeMetaLead = null!, _tradeMetaHours = null!, _tradeMetaTail = null!;
        VisualElement _tradeSell = null!, _tradeBuy = null!;
        Label _tradeSellLabel = null!, _tradeSellCount = null!, _tradeBuyLabel = null!, _tradeBuyCount = null!;
        Label _tradeSelling = null!, _tradeBuying = null!, _tradeBalanceLabel = null!, _tradeBalance = null!;
        Label _tradeGoldYou = null!, _tradeGoldTrader = null!, _tradeReason = null!;
        VisualElement _tradeConfirm = null!, _tradePager = null!;
        Label _tradeConfirmLabel = null!, _tradePage = null!;
        int _tradePainted = -1;

        /// <summary>Whether the trade window is up, for whoever owns the Escape key.</summary>
        public bool TradeOpen => _trade.Showing;

        /// <summary>Escape over the trade window: the window's Cancel (design 65 §6).</summary>
        public void CancelTrade()
        {
            _trade.Cancel(_tradeOrders);
            SubmitTradeOrders();
        }

        /// <summary>One item row's elements, pooled: a heading reuses the same row with the item parts hidden.</summary>
        sealed class TradeRowUi
        {
            public VisualElement Root = null!;
            public VisualElement Heading = null!, Item = null!;
            public HudGlyph HeadingGlyph = null!;
            public Label HeadingName = null!;
            public VisualElement Icon = null!;
            public Label Name = null!, Quality = null!, InStock = null!, Price = null!, Figure = null!, Total = null!, AllLabel = null!;
            public VisualElement Less = null!, More = null!, All = null!, FigureBox = null!;
            public int Item_ = -1;
        }

        void BuildTrade()
        {
            // The window: the settings window's scrim under it (design 39, the owner's token), a
            // panel with no padding of its own because every band pads itself.
            var scrim = new VisualElement { name = "trade-scrim" };
            scrim.AddToClassList("modal-scrim");
            scrim.style.backgroundColor = HudTokens.Convert(HudTheme.SettingsScrim);
            scrim.style.display = DisplayStyle.None;
            _hud.Add(scrim);

            VisualElement panel = Panel("trade", "window");
            panel.style.position = Position.Absolute;
            panel.style.width = TradeLayout.PanelOuterWidth;
            panel.style.left = Length.Percent(50);
            panel.style.top = Length.Percent(50);
            panel.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            panel.style.paddingLeft = panel.style.paddingRight = panel.style.paddingTop = panel.style.paddingBottom = 0;
            panel.style.backgroundColor = HudTokens.PanelFill;
            panel.style.display = DisplayStyle.None;
            _hud.Add(panel);
            _tradeModal = new HudModal(scrim, panel);

            panel.Add(BuildTradeHeader());
            panel.Add(BuildTradeModes());
            panel.Add(BuildTradeColumns());
            _tradeList = new VisualElement();
            _tradeList.style.flexShrink = 0;
            panel.Add(_tradeList);
            panel.Add(BuildTradePager());
            panel.Add(BuildTradeFoot());
            panel.Add(BuildTradeButtons());
        }

        // ---- the bands -------------------------------------------------------------------------

        VisualElement BuildTradeHeader()
        {
            var band = TradeBand(TradeLayout.HeaderHeight, TradeLayout.SidePad, TradeLayout.HeaderRightPad, HudTokens.PanelBorder);

            var face = new VisualElement();
            face.style.width = face.style.height = TradeLayout.Face;
            face.style.flexShrink = 0;
            face.style.backgroundColor = HudTokens.Convert(HudTheme.ControlBorder.WithAlpha(0.12f));
            TradeBorder(face, HudTokens.PanelBorder);
            band.Add(face);

            var text = new VisualElement();
            text.style.marginLeft = 12;
            text.style.flexGrow = 1;
            var line1 = new VisualElement();
            line1.style.flexDirection = FlexDirection.Row;
            line1.style.alignItems = Align.Center;
            Label title = HudText.Make(Registry.Label(TradeModel.TitleKey), HudTextRole.PanelLabel);
            title.style.color = HudTokens.TextDim;
            line1.Add(title);
            _tradeName = HudText.Make(string.Empty, HudTextRole.Name);
            _tradeName.style.marginLeft = 9;
            _tradeName.style.color = HudTokens.TextPrimary;
            line1.Add(_tradeName);
            text.Add(line1);

            var line2 = new VisualElement();
            line2.style.flexDirection = FlexDirection.Row;
            line2.style.marginTop = 7;
            _tradeMetaLead = TradeMeta(line2, false);
            _tradeMetaHours = TradeMeta(line2, true);
            _tradeMetaHours.style.marginLeft = 4;
            _tradeMetaTail = TradeMeta(line2, false);
            _tradeMetaTail.style.marginLeft = 4;
            text.Add(line2);
            band.Add(text);

            var close = new VisualElement();
            close.style.width = close.style.height = TradeLayout.CloseBox;
            close.style.alignItems = Align.Center;
            close.style.justifyContent = Justify.Center;
            TradeBorder(close, HudTokens.Convert(HudTheme.ControlBorder));
            close.Add(new HudGlyph(HudGlyphKind.Close, 14f, HudTokens.TextDim));
            close.tooltip = "Cancel the trade — Esc";
            close.RegisterCallback<ClickEvent>(_ => CancelTrade());
            band.Add(close);
            return band;
        }

        static Label TradeMeta(VisualElement into, bool mono)
        {
            Label label = HudText.Make(string.Empty, HudTextRole.Meta, numeric: mono);
            label.style.color = HudTokens.TextMeta;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            into.Add(label);
            return label;
        }

        VisualElement BuildTradeModes()
        {
            var band = TradeBand(TradeLayout.ModeHeight, TradeLayout.SidePad, TradeLayout.SidePad, HudTokens.PanelBorder);
            _tradeSell = TradeSegment(TradeMode.Sell, TradeModel.SellKey, out _tradeSellLabel, out _tradeSellCount);
            _tradeBuy = TradeSegment(TradeMode.Buy, TradeModel.BuyKey, out _tradeBuyLabel, out _tradeBuyCount);
            _tradeBuy.style.marginLeft = -1;
            band.Add(_tradeSell);
            band.Add(_tradeBuy);
            return band;
        }

        VisualElement TradeSegment(TradeMode mode, string key, out Label label, out Label count)
        {
            var segment = new VisualElement();
            segment.style.flexDirection = FlexDirection.Row;
            segment.style.alignItems = Align.Center;
            segment.style.justifyContent = Justify.Center;
            segment.style.height = TradeLayout.SegmentHeight;
            segment.style.minWidth = TradeLayout.SegmentMinWidth;
            segment.style.paddingLeft = segment.style.paddingRight = 16;
            TradeBorder(segment, HudTokens.Convert(HudTheme.ControlBorder));
            label = HudText.Make(Registry.Label(key), HudTextRole.Row);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            segment.Add(label);
            count = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true);
            count.style.marginLeft = 8;
            segment.Add(count);
            segment.RegisterCallback<ClickEvent>(_ =>
            {
                _trade.SetMode(mode);
                PaintTrade();
            });
            return segment;
        }

        VisualElement BuildTradeColumns()
        {
            var band = TradeBand(TradeLayout.ColumnHeaderHeight, TradeLayout.SidePad, TradeLayout.SidePad,
                HudTokens.Convert(HudTheme.RowRule));
            TradeColumn(band, string.Empty, TradeLayout.ColIcon, TextAnchor.MiddleLeft, first: true);
            TradeColumn(band, TradeModel.ItemKey, TradeLayout.ColItem, TextAnchor.MiddleLeft);
            TradeColumn(band, TradeModel.QualityKey, TradeLayout.ColQuality, TextAnchor.MiddleLeft);
            TradeColumn(band, TradeModel.InStockKey, TradeLayout.ColInStock, TextAnchor.MiddleRight);
            TradeColumn(band, TradeModel.PriceKey, TradeLayout.ColPrice, TextAnchor.MiddleRight);
            TradeColumn(band, TradeModel.QuantityKey, TradeLayout.ColQuantity, TextAnchor.MiddleCenter);
            TradeColumn(band, TradeModel.TotalKey, TradeLayout.ColTotal, TextAnchor.MiddleRight);
            return band;
        }

        static void TradeColumn(VisualElement band, string key, int width, TextAnchor align, bool first = false)
        {
            Label head = HudText.Make(key.Length == 0 ? string.Empty : Registry.Label(key), HudTextRole.PanelLabel);
            head.style.width = width;
            head.style.flexShrink = 0;
            head.style.marginLeft = first ? 0 : TradeLayout.ColumnGap;
            head.style.color = HudTokens.TextDim;
            head.style.unityTextAlign = align;
            band.Add(head);
        }

        VisualElement BuildTradePager()
        {
            _tradePager = TradeBand(TradeLayout.CategoryRowHeight, TradeLayout.SidePad, TradeLayout.SidePad,
                HudTokens.Convert(HudTheme.RowRule));
            _tradePager.style.justifyContent = Justify.Center;
            _tradePager.Add(TradePagerButton(HudIcons.ChevronLeft, -1));
            _tradePage = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true);
            _tradePage.style.marginLeft = _tradePage.style.marginRight = 12;
            _tradePage.style.color = HudTokens.TextMeta;
            _tradePager.Add(_tradePage);
            _tradePager.Add(TradePagerButton(HudIcons.ChevronRight, +1));
            _tradePager.style.display = DisplayStyle.None;
            return _tradePager;
        }

        VisualElement TradePagerButton(string path, int delta)
        {
            VisualElement box = TradeControlBox(TradeLayout.QtyButton);
            box.Add(new PathGlyph(path, 11f, HudTokens.TextMeta, stroke: 3f));
            box.RegisterCallback<ClickEvent>(_ =>
            {
                _trade.NextPage(delta);
                PaintTrade();
            });
            return box;
        }

        VisualElement BuildTradeFoot()
        {
            var band = TradeBand(TradeLayout.FootHeight, TradeLayout.SidePad, TradeLayout.SidePad, null);
            band.style.borderTopWidth = HudTheme.BorderWidth;
            band.style.borderTopColor = HudTokens.PanelBorder;
            _tradeSelling = TradeFootCell(band, TradeModel.SellingForKey, out _);
            _tradeBuying = TradeFootCell(band, TradeModel.BuyingForKey, out _);
            _tradeBalance = TradeFootCell(band, TradeModel.BalanceKey, out _tradeBalanceLabel);

            // Gold, right: a coin, then "312 you 640 trader".
            var cell = new VisualElement();
            cell.style.flexGrow = 1;
            cell.style.flexBasis = 0;
            cell.style.alignItems = Align.FlexEnd;
            Label head = HudText.Make(Registry.Label(TradeModel.GoldKey), HudTextRole.PanelLabel);
            head.style.color = HudTokens.TextDim;
            cell.Add(head);
            var figures = new VisualElement();
            figures.style.flexDirection = FlexDirection.Row;
            figures.style.alignItems = Align.Center;
            figures.style.marginTop = 5;
            var coin = new VisualElement();
            coin.style.width = coin.style.height = 14;
            coin.style.marginRight = 6;
            coin.style.backgroundColor = HudTokens.Convert(HudTheme.ItemCategoryHue(4));
            figures.Add(coin);
            _tradeGoldYou = TradeFigure(figures, HudTokens.TextPrimary);
            TradeWord(figures, TradeModel.YouKey);
            _tradeGoldTrader = TradeFigure(figures, HudTokens.TextPrimary);
            _tradeGoldTrader.style.marginLeft = 8;
            TradeWord(figures, TradeModel.TraderWordKey);
            cell.Add(figures);
            band.Add(cell);
            return band;
        }

        static Label TradeFootCell(VisualElement band, string key, out Label head)
        {
            var cell = new VisualElement();
            cell.style.flexGrow = 1;
            cell.style.flexBasis = 0;
            head = HudText.Make(Registry.Label(key), HudTextRole.PanelLabel);
            head.style.color = HudTokens.TextDim;
            cell.Add(head);
            Label value = HudText.Make(string.Empty, HudTextRole.Name, numeric: true);
            value.style.marginTop = 5;
            value.style.color = HudTokens.TextPrimary;
            cell.Add(value);
            band.Add(cell);
            return value;
        }

        static Label TradeFigure(VisualElement into, Color colour)
        {
            Label label = HudText.Make(string.Empty, HudTextRole.Row, numeric: true);
            label.style.color = colour;
            into.Add(label);
            return label;
        }

        static void TradeWord(VisualElement into, string key)
        {
            Label label = HudText.Make(Registry.Label(key), HudTextRole.Row);
            label.style.color = HudTokens.TextDim;
            label.style.marginLeft = 4;
            into.Add(label);
        }

        VisualElement BuildTradeButtons()
        {
            var band = TradeBand(TradeLayout.ButtonsHeight, TradeLayout.SidePad, TradeLayout.SidePad, null);
            band.style.borderTopWidth = HudTheme.BorderWidth;
            band.style.borderTopColor = HudTokens.Convert(HudTheme.RowRule);

            VisualElement reset = TradeButton(TradeModel.ResetKey, 0, out _);
            reset.style.paddingLeft = reset.style.paddingRight = 14;
            reset.RegisterCallback<ClickEvent>(_ =>
            {
                _trade.Reset();
                PaintTrade();
            });
            band.Add(reset);

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            band.Add(spacer);

            _tradeReason = HudText.Make(string.Empty, HudTextRole.Meta);
            _tradeReason.style.color = HudTokens.Warn;
            _tradeReason.style.marginRight = 12;
            band.Add(_tradeReason);

            VisualElement cancel = TradeButton(TradeModel.CancelKey, TradeLayout.CancelMinWidth, out _);
            cancel.RegisterCallback<ClickEvent>(_ => CancelTrade());
            band.Add(cancel);

            _tradeConfirm = TradeButton(TradeModel.ConfirmKey, TradeLayout.ConfirmMinWidth, out _tradeConfirmLabel);
            _tradeConfirm.style.marginLeft = 8;
            _tradeConfirmLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _tradeConfirm.RegisterCallback<ClickEvent>(_ =>
            {
                if (_trade.Confirm(_tradeOrders)) SubmitTradeOrders();
            });
            band.Add(_tradeConfirm);
            return band;
        }

        static VisualElement TradeButton(string key, int minWidth, out Label label)
        {
            var button = new VisualElement();
            button.style.height = 32;
            if (minWidth > 0) button.style.minWidth = minWidth;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius =
                button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = HudTheme.ControlRadius;
            TradeBorder(button, HudTokens.Convert(HudTheme.ControlBorder));
            label = HudText.Make(Registry.Label(key), HudTextRole.Row);
            label.style.color = HudTokens.TextPrimary;
            button.Add(label);
            return button;
        }

        /// <summary>A band of the window: a row of a fixed height, padded at the sides, with a rule under it.</summary>
        static VisualElement TradeBand(int height, int left, int right, Color? rule)
        {
            var band = new VisualElement();
            band.style.flexDirection = FlexDirection.Row;
            band.style.alignItems = Align.Center;
            band.style.flexShrink = 0;
            band.style.height = height;
            band.style.paddingLeft = left;
            band.style.paddingRight = right;
            if (rule.HasValue)
            {
                band.style.borderBottomWidth = HudTheme.BorderWidth;
                band.style.borderBottomColor = rule.Value;
            }
            return band;
        }

        /// <summary>A one-pixel frame in one colour on all four sides.</summary>
        static void TradeBorder(VisualElement element, Color colour)
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

        static VisualElement TradeControlBox(int size)
        {
            var box = new VisualElement();
            box.style.width = box.style.height = size;
            box.style.flexShrink = 0;
            box.style.alignItems = Align.Center;
            box.style.justifyContent = Justify.Center;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius =
                box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = HudTheme.ControlRadius;
            TradeBorder(box, HudTokens.Convert(HudTheme.ControlBorder));
            return box;
        }

        // ---- one row ---------------------------------------------------------------------------

        TradeRowUi MakeTradeRow()
        {
            var ui = new TradeRowUi { Root = new VisualElement() };
            ui.Root.style.flexShrink = 0;

            // The heading: a 3 px wash, the rail, the glyph and the name, all in the category's hue.
            ui.Heading = TradeBand(TradeLayout.CategoryRowHeight, TradeLayout.SidePad, TradeLayout.SidePad, null);
            ui.Heading.style.borderLeftWidth = 3;
            ui.Heading.style.paddingLeft = TradeLayout.SidePad - 3;
            ui.HeadingGlyph = new HudGlyph(HudGlyphKind.CategoryFood, 12f, HudTokens.TextDim);
            ui.Heading.Add(ui.HeadingGlyph);
            ui.HeadingName = HudText.Make(string.Empty, HudTextRole.PanelLabel);
            ui.HeadingName.style.marginLeft = 9;
            ui.Heading.Add(ui.HeadingName);
            ui.Root.Add(ui.Heading);

            // The item: the grid 28 / 1fr / 72 / 72 / 64 / 164 / 64, gap 12.
            ui.Item = TradeBand(TradeLayout.RowHeight, TradeLayout.SidePad, TradeLayout.SidePad, HudTokens.Convert(HudTheme.RowRule));
            ui.Item.style.borderLeftWidth = 2;
            ui.Item.style.paddingLeft = TradeLayout.SidePad - 2;

            ui.Icon = new VisualElement();
            ui.Icon.style.width = ui.Icon.style.height = TradeLayout.ColIcon;
            ui.Icon.style.flexShrink = 0;
            ui.Icon.style.backgroundColor = HudTokens.PanelBorder;
            ui.Icon.style.borderBottomWidth = 2;
            ui.Item.Add(ui.Icon);

            ui.Name = TradeCell(ui.Item, TradeLayout.ColItem, TextAnchor.MiddleLeft, mono: false, HudTokens.TextPrimary);
            ui.Quality = TradeCell(ui.Item, TradeLayout.ColQuality, TextAnchor.MiddleLeft, mono: false, HudTokens.TextMeta);
            ui.InStock = TradeCell(ui.Item, TradeLayout.ColInStock, TextAnchor.MiddleRight, mono: true, HudTokens.TextPrimary);
            ui.Price = TradeCell(ui.Item, TradeLayout.ColPrice, TextAnchor.MiddleRight, mono: true, HudTokens.TextMeta);

            var qty = new VisualElement();
            qty.style.width = TradeLayout.ColQuantity;
            qty.style.flexShrink = 0;
            qty.style.marginLeft = TradeLayout.ColumnGap;
            qty.style.flexDirection = FlexDirection.Row;
            qty.style.alignItems = Align.Center;

            // Minus and plus are neutral (design 65 §6): neither direction is good or bad.
            ui.Less = TradeControlBox(TradeLayout.QtyButton);
            ui.Less.Add(new PathGlyph(HudIcons.Minus, 11f, HudTokens.TextPrimary, stroke: 3f));
            ui.Less.RegisterCallback<ClickEvent>(e => StepTrade(ui, -1, e.shiftKey));
            qty.Add(ui.Less);

            ui.FigureBox = new VisualElement();
            ui.FigureBox.style.width = TradeLayout.QtyFigure;
            ui.FigureBox.style.height = TradeLayout.QtyButton;
            ui.FigureBox.style.marginLeft = TradeLayout.QtyGap;
            ui.FigureBox.style.justifyContent = Justify.Center;
            ui.FigureBox.style.borderTopLeftRadius = ui.FigureBox.style.borderTopRightRadius =
                ui.FigureBox.style.borderBottomLeftRadius = ui.FigureBox.style.borderBottomRightRadius = HudTheme.ControlRadius;
            ui.Figure = HudText.Make(string.Empty, HudTextRole.Row, numeric: true);
            ui.Figure.style.unityTextAlign = TextAnchor.MiddleCenter;
            ui.FigureBox.Add(ui.Figure);
            qty.Add(ui.FigureBox);

            ui.More = TradeControlBox(TradeLayout.QtyButton);
            ui.More.style.marginLeft = TradeLayout.QtyGap;
            ui.More.Add(new PathGlyph(HudIcons.Plus, 11f, HudTokens.TextPrimary, stroke: 3f));
            ui.More.RegisterCallback<ClickEvent>(e => StepTrade(ui, +1, e.shiftKey));
            qty.Add(ui.More);

            ui.All = new VisualElement();
            ui.All.style.width = TradeLayout.QtyAllWidth;
            ui.All.style.height = TradeLayout.QtyButton;
            ui.All.style.marginLeft = TradeLayout.QtyGap;
            ui.All.style.alignItems = Align.Center;
            ui.All.style.justifyContent = Justify.Center;
            ui.All.style.borderTopLeftRadius = ui.All.style.borderTopRightRadius =
                ui.All.style.borderBottomLeftRadius = ui.All.style.borderBottomRightRadius = HudTheme.ControlRadius;
            TradeBorder(ui.All, HudTokens.PanelBorder);
            ui.AllLabel = HudText.Make(Registry.Label(TradeModel.AllKey), HudTextRole.Meta);
            ui.All.Add(ui.AllLabel);
            ui.All.RegisterCallback<ClickEvent>(_ =>
            {
                if (ui.Item_ < 0) return;
                _trade.All(ui.Item_);
                PaintTrade();
            });
            qty.Add(ui.All);
            ui.Item.Add(qty);

            ui.Total = TradeCell(ui.Item, TradeLayout.ColTotal, TextAnchor.MiddleRight, mono: true, HudTokens.Accent);
            ui.Root.Add(ui.Item);
            return ui;
        }

        static Label TradeCell(VisualElement row, int width, TextAnchor align, bool mono, Color colour)
        {
            Label label = HudText.Make(string.Empty, HudTextRole.Row, numeric: mono);
            label.style.width = width;
            label.style.flexShrink = 0;
            label.style.marginLeft = TradeLayout.ColumnGap;
            label.style.unityTextAlign = align;
            label.style.color = colour;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Hidden;
            row.Add(label);
            return label;
        }

        void StepTrade(TradeRowUi ui, int direction, bool shift)
        {
            if (ui.Item_ < 0) return;
            _trade.Step(ui.Item_, direction, shift);
            PaintTrade();
        }

        // ---- refresh and paint -----------------------------------------------------------------

        /// <summary>
        /// The fast cadence: read the frame, open or close the window, hold the clock while it is up,
        /// and repaint when the model moved. A colony with no trader costs one span read.
        /// </summary>
        void RefreshTrade()
        {
            var world = _boot?.World;
            var content = _boot?.Colony?.Pawns.Content;
            if (world == null || content == null || _tradeModal == null) return;

            // Below 1,000 panel pixels of height the list pages, twenty lines a page (the owner's rule).
            float height = _hud.resolvedStyle.height;
            _trade.RowsPerPage = !float.IsNaN(height) && height > 0f && height < TradeLayout.PagingBelowHeight
                ? TradeLayout.RowsPerPage : 0;

            bool showing = _trade.Refresh(world.Views.Current,
                def => def >= 0 && def < content.Items.Length ? (int)content.Items[def].category : 0);
            if (showing != _tradeModal.Showing)
            {
                _tradeModal.Show(showing);
                if (showing) CloseContextMenu();
                HoldTradeKeys(showing);
            }
            _boot!.ModalHeld = showing;
            if (showing) PaintTrade();
        }

        /// <summary>Tab switches the mode while the window is up (design 65 §6).</summary>
        void ReadTradeKeys()
        {
            if (!_trade.Showing) return;
            var keys = UnityEngine.InputSystem.Keyboard.current;
            if (keys == null || !keys.tabKey.wasPressedThisFrame) return;
            _trade.ToggleMode();
            PaintTrade();
        }

        void SubmitTradeOrders()
        {
            var world = _boot?.World;
            if (world != null)
                for (int i = 0; i < _tradeOrders.Count; i++) world.Intents.Submit(_tradeOrders[i]);
            _tradeOrders.Clear();
            _tradeModal?.Show(false);
            HoldTradeKeys(false);
            if (_boot != null) _boot.ModalHeld = false;
        }

        bool _tradeHoldsKeys;

        /// <summary>
        /// While the window is up the game's keys are off (<see cref="HotkeyDirector.Suspended"/>),
        /// so a letter pressed over the ledger arms no tool and pans no camera. Only the hold this
        /// window took is let go: the wake sets the same flag for its own reasons.
        /// </summary>
        void HoldTradeKeys(bool on)
        {
            if (_directors == null || on == _tradeHoldsKeys) return;
            _tradeHoldsKeys = on;
            _directors.Hotkeys.Suspended = on;
        }

        void PaintTrade()
        {
            if (_tradePainted == _trade.Version) return;
            _tradePainted = _trade.Version;

            HudText.Set(_tradeName, _trade.TraderName, HudTextRole.Name);
            _tradeMetaLead.text = _trade.MetaLead;
            _tradeMetaHours.text = _trade.MetaHours;
            _tradeMetaTail.text = _trade.MetaTail;

            PaintSegment(_tradeSell, _tradeSellLabel, _tradeSellCount, _trade.Mode == TradeMode.Sell, _trade.SellCount);
            PaintSegment(_tradeBuy, _tradeBuyLabel, _tradeBuyCount, _trade.Mode == TradeMode.Buy, _trade.BuyCount);

            IReadOnlyList<TradeLine> lines = _trade.Lines;
            while (_tradeRows.Count < lines.Count)
            {
                TradeRowUi made = MakeTradeRow();
                _tradeRows.Add(made);
                _tradeList.Add(made.Root);
            }
            for (int i = 0; i < _tradeRows.Count; i++)
            {
                TradeRowUi ui = _tradeRows[i];
                bool used = i < lines.Count;
                ui.Root.style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                if (used) PaintTradeRow(ui, lines[i]);
                else ui.Item_ = -1;
            }

            _tradePager.style.display = _trade.PageCount > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            _tradePage.text = (_trade.Page + 1) + " / " + _trade.PageCount;

            _tradeSelling.text = _trade.SellingFor.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _tradeBuying.text = _trade.BuyingFor.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _tradeBalanceLabel.text = Registry.Label(_trade.BalanceLabelKey);
            bool bad = _trade.ColonyShort || _trade.TraderShort;
            bool zero = _trade.Balance == 0;
            _tradeBalance.text = _trade.BalanceText;
            _tradeBalance.style.color = bad ? HudTokens.Bad : zero ? HudTokens.TextDim : HudTokens.Accent;
            _tradeGoldYou.text = _trade.ColonyGold.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _tradeGoldYou.style.color = _trade.ColonyShort ? HudTokens.Bad : HudTokens.TextPrimary;
            _tradeGoldTrader.text = _trade.Purse.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _tradeGoldTrader.style.color = _trade.TraderShort ? HudTokens.Bad : HudTokens.TextPrimary;

            string reason = _trade.DisabledReasonKey;
            _tradeReason.text = reason.Length == 0 ? string.Empty : Registry.Label(reason);
            _tradeReason.style.display = reason.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            bool on = _trade.CanConfirm;
            _tradeConfirm.style.backgroundColor = on ? HudTokens.Accent : Color.clear;
            TradeBorder(_tradeConfirm, on ? HudTokens.Accent : HudTokens.PanelBorder);
            _tradeConfirmLabel.style.color = on ? HudTokens.OnAccent : HudTokens.TextDim;
        }

        static void PaintSegment(VisualElement segment, Label label, Label count, bool active, int moving)
        {
            segment.style.backgroundColor = active ? HudTokens.Accent : Color.clear;
            TradeBorder(segment, active ? HudTokens.Accent : HudTokens.Convert(HudTheme.ControlBorder));
            label.style.color = active ? HudTokens.OnAccent : HudTokens.TextPrimary;
            count.text = moving.ToString(System.Globalization.CultureInfo.InvariantCulture);
            count.style.color = active ? HudTokens.OnAccent : HudTokens.TextDim;
        }

        static void PaintTradeRow(TradeRowUi ui, TradeLine line)
        {
            ui.Heading.style.display = line.IsHeading ? DisplayStyle.Flex : DisplayStyle.None;
            ui.Item.style.display = line.IsHeading ? DisplayStyle.None : DisplayStyle.Flex;
            HudColour raw = HudTheme.ItemCategoryHue(line.Category);
            Color hue = HudTokens.Convert(raw);

            if (line.IsHeading)
            {
                ui.Item_ = -1;
                ui.Heading.style.backgroundColor = HudTokens.Convert(new HudColour(255, 255, 255, 0.03f));
                ui.Heading.style.borderLeftColor = hue;
                ui.HeadingGlyph.Kind = CategoryGlyph(line.Category);
                ui.HeadingGlyph.Tint = hue;
                ui.HeadingName.text = line.Name;
                ui.HeadingName.style.color = hue;
                return;
            }

            ui.Item_ = line.Item;
            ui.Icon.style.borderBottomColor = hue;
            ui.Name.text = line.Name;
            ui.Quality.text = line.QualityLabel;
            ui.Quality.style.color = HudTokens.Convert(HudTheme.QualityHue(line.Quality));
            ui.InStock.text = line.InStockText;
            ui.Price.text = line.PriceText;
            ui.Figure.text = line.QuantityText;
            ui.Total.text = line.TotalText;

            // Idle: the figure dim in a faint box. Moving: the figure in accent in an accent box, the
            // row washed and railed in accent.
            ui.Figure.style.color = line.Moving ? HudTokens.Accent : HudTokens.TextDim;
            TradeBorder(ui.FigureBox, line.Moving
                ? HudTokens.Convert(HudTheme.Accent.WithAlpha(0.60f))
                : HudTokens.PanelBorder);
            ui.Item.style.backgroundColor = line.Moving
                ? HudTokens.Convert(HudTheme.Accent.WithAlpha(0.06f)) : Color.clear;
            ui.Item.style.borderLeftColor = line.Moving ? HudTokens.Accent : Color.clear;
            ui.AllLabel.style.color = line.AllMoving ? HudTokens.Accent : HudTokens.TextPrimary;
            ui.Less.style.opacity = line.CanLess ? 1f : 0.3f;
            ui.Less.pickingMode = line.CanLess ? PickingMode.Position : PickingMode.Ignore;
            ui.More.style.opacity = line.CanMore ? 1f : 0.3f;
            ui.More.pickingMode = line.CanMore ? PickingMode.Position : PickingMode.Ignore;
        }
    }
}
