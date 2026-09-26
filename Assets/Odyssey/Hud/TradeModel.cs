#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>Which half of the deal the trade window shows (design 57 §6, mockups 28a and 28b).</summary>
    public enum TradeMode
    {
        /// <summary>What the colony sells: its stock, at what the trader pays.</summary>
        Sell = 0,

        /// <summary>What the colony buys: the trader's stock, at what it charges.</summary>
        Buy = 1,
    }

    /// <summary>One line of the trade window: a category heading, or an item.</summary>
    public sealed class TradeLine
    {
        /// <summary>A category heading rather than an item.</summary>
        public bool IsHeading;

        /// <summary>The storage category, for the heading's hue and glyph and the icon's edge.</summary>
        public int Category;

        /// <summary>The heading's name, or the item's.</summary>
        public string Name = string.Empty;

        /// <summary>The item def. Meaningless on a heading.</summary>
        public int Item;

        /// <summary>The quality tier of what would move, a <see cref="QualityHandle"/> value, and its name. Blank for none.</summary>
        public int Quality;
        public string QualityLabel = string.Empty;

        /// <summary>How many this mode's side holds, as a number and as printed.</summary>
        public int InStock;
        public string InStockText = string.Empty;

        /// <summary>The price of one, from the simulation, as a number and as printed.</summary>
        public int Price;
        public string PriceText = string.Empty;

        /// <summary>How many are moving, as a number and as printed.</summary>
        public int Quantity;
        public string QuantityText = string.Empty;

        /// <summary>Quantity times price, printed; blank at nought.</summary>
        public string TotalText = string.Empty;

        /// <summary>Anything moving: the accent figure, fill and rail.</summary>
        public bool Moving;

        /// <summary>All of it is moving: the All button's label turns accent.</summary>
        public bool AllMoving;

        public bool CanLess, CanMore;
    }

    /// <summary>
    /// The trade window (design 57 §6; the owner's mockups 28a Sell and 28b Buy), as data the shell
    /// draws and the intents its buttons send. No UnityEngine, so every rule is proven in the fast
    /// tier; the view only lays the lines out.
    ///
    /// <para><b>It opens itself.</b> Each refresh reads the published <see cref="TradeView"/>s; a
    /// ready session it has not opened for yet opens the window on <see cref="TradeMode.Sell"/>
    /// with nothing moving. The window closes when that session ends — a Cancel, a draft, the
    /// trader leaving — and never reopens for the same session.</para>
    ///
    /// <para><b>The quantities are the window's</b>, one per item per mode, and the deal spans both
    /// modes: switching loses nothing. They are clamped to what the side holds on every refresh.
    /// Nothing is sent until Confirm, which sends every moving row as a <c>TradeLine</c>, the
    /// <c>TradeCommit</c> with the balance shown, and the <c>TradeCancel</c> that ends the session —
    /// the simulation re-checks the whole deal and applies it whole or not at all.</para>
    ///
    /// <para><b>Prices are the simulation's</b> (<see cref="TradeRowView"/>). This class multiplies
    /// and adds them; it never works one out.</para>
    /// </summary>
    public sealed class TradeModel
    {
        // ---- the registry's words (design 57 §6) -------------------------------------------------
        public const string TitleKey = "ui.tab.trade";
        public const string SellKey = "ui.trade.sell", BuyKey = "ui.trade.buy";
        public const string ItemKey = "ui.trade.col.item", QualityKey = "ui.trade.col.quality",
            InStockKey = "ui.trade.col.instock", PriceKey = "ui.trade.col.price",
            QuantityKey = "ui.trade.col.quantity", TotalKey = "ui.trade.col.total";
        public const string AllKey = "ui.trade.all";
        public const string SellingForKey = "ui.trade.sellingfor", BuyingForKey = "ui.trade.buyingfor";
        public const string YouPayKey = "ui.trade.youpay", YouReceiveKey = "ui.trade.youreceive",
            BalanceKey = "ui.trade.balance";
        public const string GoldKey = "ui.res.gold", YouKey = "ui.trade.you", TraderWordKey = "ui.trade.trader";
        public const string LeavesInKey = "ui.trade.leavesin", NegotiatingKey = "ui.trade.negotiating";
        public const string NothingKey = "ui.trade.reason.nothing", NoGoldKey = "ui.trade.reason.gold",
            NoPurseKey = "ui.trade.reason.purse";
        public const string ResetKey = "ui.trade.reset", ConfirmKey = "ui.trade.confirm", CancelKey = "ui.menu.cancel";
        public const string TraderKindKey = "ui.pawn.trader";
        public const string PaneLeavesKey = "ui.trade.pane.leaves", PaneCarryingKey = "ui.trade.pane.carrying";

        /// <summary>Every key the window puts on screen, for <c>RegistryTests</c>.</summary>
        public static readonly string[] IconKeys =
        {
            TitleKey, SellKey, BuyKey, ItemKey, QualityKey, InStockKey, PriceKey, QuantityKey, TotalKey, AllKey,
            SellingForKey, BuyingForKey, YouPayKey, YouReceiveKey, BalanceKey, GoldKey, YouKey, TraderWordKey,
            LeavesInKey, NegotiatingKey, NothingKey, NoGoldKey, NoPurseKey, ResetKey, ConfirmKey, CancelKey,
            TraderKindKey, PaneLeavesKey, PaneCarryingKey,
        };

        readonly int[] _sell = new int[ItemHandle.Count];
        readonly int[] _buy = new int[ItemHandle.Count];
        readonly TradeRowView?[] _rows = new TradeRowView?[ItemHandle.Count];
        readonly List<TradeLine> _lines = new List<TradeLine>();
        readonly List<TradeLine> _pool = new List<TradeLine>();

        int _openedVisit = -1, _openedSession = -1;

        /// <summary>The window is up.</summary>
        public bool Showing { get; private set; }

        public TradeMode Mode { get; private set; } = TradeMode.Sell;

        /// <summary>The trader and the negotiator of the open session.</summary>
        public PawnId Trader { get; private set; }
        public PawnId Negotiator { get; private set; }

        // ---- the header -------------------------------------------------------------------------
        public string TraderName { get; private set; } = string.Empty;

        /// <summary>"Trader · leaves in", "14 h", "· Tom Hale negotiating": three parts so the hours can be mono; the view spaces them.</summary>
        public string MetaLead { get; private set; } = string.Empty;
        public string MetaHours { get; private set; } = string.Empty;
        public string MetaTail { get; private set; } = string.Empty;

        // ---- the mode switch --------------------------------------------------------------------
        /// <summary>How many items are moving in each mode.</summary>
        public int SellCount { get; private set; }
        public int BuyCount { get; private set; }

        // ---- the foot ---------------------------------------------------------------------------
        public int SellingFor { get; private set; }
        public int BuyingFor { get; private set; }

        /// <summary>Buying for, less selling for: above nought the colony pays, below it the colony receives.</summary>
        public int Balance => BuyingFor - SellingFor;

        /// <summary>The balance's label key: You pay, You receive, or Balance at nought.</summary>
        public string BalanceLabelKey => Balance > 0 ? YouPayKey : Balance < 0 ? YouReceiveKey : BalanceKey;

        /// <summary>The balance as printed: its size, never its sign — the label says which way.</summary>
        public string BalanceText => Math.Abs(Balance).ToString(CultureInfo.InvariantCulture);

        public int ColonyGold { get; private set; }
        public int Purse { get; private set; }

        /// <summary>The colony cannot cover what it owes: the balance and "you" gold turn Bad.</summary>
        public bool ColonyShort => Balance > 0 && Balance > ColonyGold;

        /// <summary>The trader cannot cover what it owes: the balance and "trader" gold turn Bad.</summary>
        public bool TraderShort => Balance < 0 && -Balance > Purse;

        // ---- the buttons ------------------------------------------------------------------------
        /// <summary>The key of why Confirm is off, or empty while it is on.</summary>
        public string DisabledReasonKey =>
            SellCount + BuyCount == 0 ? NothingKey
            : ColonyShort ? NoGoldKey
            : TraderShort ? NoPurseKey
            : string.Empty;

        public bool CanConfirm => DisabledReasonKey.Length == 0;

        // ---- the list ---------------------------------------------------------------------------
        /// <summary>Headings and items of the current mode, in the game's category order, on the current page.</summary>
        public IReadOnlyList<TradeLine> Lines => _lines;

        /// <summary>Rows a page holds, or nought while the list does not page. Set by the view from the screen's height.</summary>
        public int RowsPerPage { get; set; }

        public int Page { get; private set; }
        public int PageCount { get; private set; } = 1;

        /// <summary>Anything about the window changed since the view last drew it.</summary>
        public int Version { get; private set; }

        /// <summary>
        /// Read the frame: open for a new ready session, close for one that has ended, and rebuild
        /// the lines. <paramref name="categoryOf"/> gives an item def's storage category, which this
        /// assembly cannot read from the content. Returns true while the window is up.
        /// </summary>
        public bool Refresh(WorldSnapshot frame, Func<int, int> categoryOf)
        {
            ReadOnlySpan<TradeView> trades = frame.Trades;
            int ready = -1;
            for (int i = 0; i < trades.Length; i++)
                if (trades[i].Ready && trades[i].Negotiator.IsValid) { ready = i; break; }

            if (Showing)
            {
                bool still = ready >= 0 && trades[ready].Visit == _openedVisit && trades[ready].Session == _openedSession;
                if (!still)
                {
                    Close();
                    return false;
                }
            }
            else
            {
                if (ready < 0) return false;
                TradeView view = trades[ready];
                if (view.Visit == _openedVisit && view.Session == _openedSession) return false;
                Open(view);
            }

            TradeView trade = trades[ready];
            Read(frame, trade, categoryOf);
            return true;
        }

        void Open(in TradeView view)
        {
            Showing = true;
            _openedVisit = view.Visit;
            _openedSession = view.Session;
            Trader = view.Trader;
            Negotiator = view.Negotiator;
            Mode = TradeMode.Sell;
            Page = 0;
            Array.Clear(_sell, 0, _sell.Length);
            Array.Clear(_buy, 0, _buy.Length);
            Version++;
        }

        /// <summary>Put the window away with nothing sent. The session's end, seen from here.</summary>
        public void Close()
        {
            if (!Showing) return;
            Showing = false;
            _lines.Clear();
            Version++;
        }

        void Read(WorldSnapshot frame, in TradeView trade, Func<int, int> categoryOf)
        {
            Array.Clear(_rows, 0, _rows.Length);
            ReadOnlySpan<TradeRowView> rows = frame.TradeRows;
            for (int i = 0; i < rows.Length; i++)
            {
                TradeRowView row = rows[i];
                if (row.Visit != trade.Visit || (uint)row.Item >= (uint)_rows.Length) continue;
                _rows[row.Item] = row;
            }

            ColonyGold = trade.ColonyGold;
            Purse = trade.Purse;
            TraderName = ColonistNames.Of(frame, trade.Trader);
            MetaLead = Registry.Label(TraderKindKey) + " · " + Registry.Label(LeavesInKey);
            MetaHours = Hours(trade.StayLeftTicks).ToString(CultureInfo.InvariantCulture) + " h";
            MetaTail = "· " + ColonistNames.Of(frame, trade.Negotiator) + " " + Registry.Label(NegotiatingKey);

            Rebuild(categoryOf);
        }

        /// <summary>Whole hours of a stay, rounded up, so the last minutes read "1 h" and not "0 h".</summary>
        public static int Hours(int ticks) =>
            ticks <= 0 ? 0 : (ticks + Odyssey.Sim.Contracts.Calendar.TicksPerHour - 1) / Odyssey.Sim.Contracts.Calendar.TicksPerHour;

        Func<int, int>? _categoryOf;

        void Rebuild(Func<int, int> categoryOf)
        {
            _categoryOf = categoryOf;
            SellingFor = BuyingFor = 0;
            SellCount = BuyCount = 0;
            for (int item = 0; item < _rows.Length; item++)
            {
                TradeRowView? row = _rows[item];
                if (row == null)
                {
                    _sell[item] = _buy[item] = 0;
                    continue;
                }
                TradeRowView r = row.Value;
                _sell[item] = Clamp(_sell[item], r.ColonyCount);
                _buy[item] = Clamp(_buy[item], r.TraderCount);
                SellingFor += _sell[item] * r.BuyPrice;
                BuyingFor += _buy[item] * r.SellPrice;
                if (_sell[item] > 0) SellCount++;
                if (_buy[item] > 0) BuyCount++;
            }

            // Every line of the mode, headings between; then the page of it.
            var all = new List<TradeLine>();
            int used = 0;
            for (int category = 0; category < StorageSettingsModel.CategoryKeys.Length; category++)
            {
                bool headed = false;
                for (int item = 0; item < _rows.Length; item++)
                {
                    TradeRowView? row = _rows[item];
                    if (row == null || categoryOf(item) != category) continue;
                    TradeRowView r = row.Value;
                    bool sell = Mode == TradeMode.Sell;
                    int stock = sell ? r.ColonyCount : r.TraderCount;
                    if (stock <= 0) continue;
                    if (!headed)
                    {
                        headed = true;
                        TradeLine heading = Take(used++);
                        heading.IsHeading = true;
                        heading.Category = category;
                        heading.Name = Registry.Label(StorageSettingsModel.CategoryKeys[category]);
                        all.Add(heading);
                    }

                    TradeLine line = Take(used++);
                    line.IsHeading = false;
                    line.Category = category;
                    line.Item = item;
                    line.Name = ItemLabels.Label(item);
                    line.Quality = sell ? r.ColonyQuality : r.TraderQuality;
                    line.QualityLabel = line.Quality == 0 ? string.Empty : Registry.Label(QualityLabels.Key(line.Quality));
                    line.InStock = stock;
                    line.InStockText = stock.ToString(CultureInfo.InvariantCulture);
                    line.Price = sell ? r.BuyPrice : r.SellPrice;
                    line.PriceText = line.Price.ToString(CultureInfo.InvariantCulture);
                    line.Quantity = sell ? _sell[item] : _buy[item];
                    line.QuantityText = line.Quantity.ToString(CultureInfo.InvariantCulture);
                    line.TotalText = line.Quantity == 0 ? string.Empty
                        : ((long)line.Quantity * line.Price).ToString(CultureInfo.InvariantCulture);
                    line.Moving = line.Quantity > 0;
                    line.AllMoving = line.Quantity > 0 && line.Quantity == stock;
                    line.CanLess = line.Quantity > 0;
                    line.CanMore = line.Quantity < stock;
                    all.Add(line);
                }
            }

            int perPage = RowsPerPage > 0 ? RowsPerPage : Math.Max(1, all.Count);
            PageCount = Math.Max(1, (all.Count + perPage - 1) / perPage);
            if (Page >= PageCount) Page = PageCount - 1;
            _lines.Clear();
            for (int i = Page * perPage; i < all.Count && i < (Page + 1) * perPage; i++) _lines.Add(all[i]);
            Version++;
        }

        TradeLine Take(int index)
        {
            while (_pool.Count <= index) _pool.Add(new TradeLine());
            return _pool[index];
        }

        static int Clamp(int n, int max) => n < 0 ? 0 : n > max ? max : n;

        // ---- what the player does ---------------------------------------------------------------

        /// <summary>Show the other half of the deal. What is moving in either half stays.</summary>
        public void SetMode(TradeMode mode)
        {
            if (Mode == mode) return;
            Mode = mode;
            Page = 0;
            if (_categoryOf != null) Rebuild(_categoryOf);
        }

        /// <summary>Tab: the other mode.</summary>
        public void ToggleMode() => SetMode(Mode == TradeMode.Sell ? TradeMode.Buy : TradeMode.Sell);

        /// <summary>Minus or plus on an item's row: one, or ten with Shift, clamped to nought and what the side holds.</summary>
        public void Step(int item, int direction, bool shift)
        {
            if ((uint)item >= (uint)_rows.Length || _rows[item] == null) return;
            int by = (shift ? TradeLayout.ShiftStep : TradeLayout.Step) * Math.Sign(direction);
            Set(item, Current(item) + by);
        }

        /// <summary>All: the figure becomes the whole of what the side holds.</summary>
        public void All(int item)
        {
            if ((uint)item >= (uint)_rows.Length || _rows[item] == null) return;
            Set(item, int.MaxValue);
        }

        /// <summary>Reset: nothing moving in either mode.</summary>
        public void Reset()
        {
            Array.Clear(_sell, 0, _sell.Length);
            Array.Clear(_buy, 0, _buy.Length);
            if (_categoryOf != null) Rebuild(_categoryOf);
        }

        public void NextPage(int delta)
        {
            int page = Math.Max(0, Math.Min(PageCount - 1, Page + delta));
            if (page == Page) return;
            Page = page;
            if (_categoryOf != null) Rebuild(_categoryOf);
        }

        int Current(int item) => Mode == TradeMode.Sell ? _sell[item] : _buy[item];

        void Set(int item, int value)
        {
            TradeRowView r = _rows[item]!.Value;
            if (Mode == TradeMode.Sell) _sell[item] = Clamp(value, r.ColonyCount);
            else _buy[item] = Clamp(value, r.TraderCount);
            if (_categoryOf != null) Rebuild(_categoryOf);
        }

        /// <summary>How many of an item are moving in a mode. For the tests and nothing else.</summary>
        public int QuantityOf(TradeMode mode, int item) => mode == TradeMode.Sell ? _sell[item] : _buy[item];

        /// <summary>
        /// Confirm: every moving row as a <c>TradeLine</c> (sold negative, bought positive), the
        /// <c>TradeCommit</c> with the balance shown, and the <c>TradeCancel</c> that ends the
        /// session; then the window closes. Nothing is sent while Confirm is off.
        /// </summary>
        public bool Confirm(List<Intent> into)
        {
            if (!Showing || !CanConfirm) return false;
            int trader = Trader.Value;
            for (int item = 0; item < _sell.Length; item++)
            {
                if (_sell[item] > 0) into.Add(new Intent(IntentKind.TradeLine, default, trader, item, -_sell[item]));
                if (_buy[item] > 0) into.Add(new Intent(IntentKind.TradeLine, default, trader, item, _buy[item]));
            }
            into.Add(new Intent(IntentKind.TradeCommit, default, trader, Balance));
            into.Add(new Intent(IntentKind.TradeCancel, default, trader));
            Close();
            return true;
        }

        /// <summary>Cancel, the close box or Escape: the session ends and the negotiator walks away.</summary>
        public void Cancel(List<Intent> into)
        {
            if (!Showing) return;
            into.Add(new Intent(IntentKind.TradeCancel, default, Trader.Value));
            Close();
        }
    }
}
