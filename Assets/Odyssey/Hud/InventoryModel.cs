#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>Which column the item table is sorted by. Exactly one, always.</summary>
    public enum InventorySort
    {
        /// <summary>By name, A first.</summary>
        Item,

        /// <summary>By how many stores hold it, most first.</summary>
        Places,

        /// <summary>By how many the colony holds, most first. The default.</summary>
        Total,
    }

    /// <summary>One store holding an item: a stockpile or a shelf, and how many of the item are in it.</summary>
    public sealed class InventoryPlace
    {
        /// <summary>The store's number in the one series both kinds share: the "3" of "Shelf 3". Also its identity here.</summary>
        public int Ordinal { get; internal set; }

        public bool Shelf { get; internal set; }

        public int Quantity { get; internal set; }

        /// <summary>
        /// Where Go takes the camera: the cell of this store holding the largest stack of the item,
        /// so a warehouse-sized stockpile is entered where the thing is rather than at its corner.
        /// A shelf is one cell and this is it.
        /// </summary>
        public CellRef Cell { get; internal set; }

        internal int BestStack;

        /// <summary>"Stockpile 3", "Shelf 2": the names the inspect pane gives the same stores.</summary>
        public string Name => NameOf(Shelf, Ordinal);

        /// <summary>
        /// A store's name from its kind and number — the one form the pane, the Inventory tab and
        /// the Gear tab's Pick from stores all write, so none of them can call Stockpile 3 anything else.
        /// </summary>
        public static string NameOf(bool shelf, int ordinal) =>
            Registry.Label(shelf ? PaletteTools.Shelf : PaletteTools.Stockpile) + " "
            + ordinal.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>One commodity the colony's stores hold, and where.</summary>
    public sealed class InventoryItem
    {
        public int DefIndex { get; internal set; }
        public string Key { get; internal set; } = string.Empty;

        /// <summary>An index into <see cref="StorageSettingsModel.CategoryKeys"/>.</summary>
        public int Category { get; internal set; }

        public int Total { get; internal set; }

        /// <summary>The stores holding it, most first.</summary>
        public List<InventoryPlace> Places { get; } = new List<InventoryPlace>();

        /// <summary>The name, its first letter upper case, as the storage pane writes it.</summary>
        public string Name => StorageSettingsModel.Capitalise(Registry.Label(Key));

        /// <summary>The store holding the most, or null.</summary>
        public InventoryPlace? Largest => Places.Count > 0 ? Places[0] : null;
    }

    /// <summary>One row of the item table: a category's heading, or an item under it.</summary>
    public readonly struct InventoryRow
    {
        public readonly bool IsGroup;

        /// <summary>An index into <see cref="StorageSettingsModel.CategoryKeys"/>.</summary>
        public readonly int Category;

        /// <summary>How many kinds of item the category holds. 0 draws no count at all.</summary>
        public readonly int Kinds;

        public readonly InventoryItem? Item;

        InventoryRow(bool isGroup, int category, int kinds, InventoryItem? item)
        {
            IsGroup = isGroup;
            Category = category;
            Kinds = kinds;
            Item = item;
        }

        public static InventoryRow Group(int category, int kinds) => new InventoryRow(true, category, kinds, null);

        public static InventoryRow Of(InventoryItem item) => new InventoryRow(false, item.Category, 0, item);
    }

    /// <summary>
    /// The Inventory tab's content (design 35): every commodity in the colony's stores, grouped by
    /// the storage pane's six categories, and for the one selected, the stores holding it.
    ///
    /// <para><b>Only stored goods.</b> The owner's spec leaves loose and carried things out for now,
    /// so a thing counts when it is on a shelf or lies in a stockpile's cell, and not otherwise.
    /// Both are read straight off the published frame — a contained thing is published at its
    /// shelf's cell, and a zone publishes one row per cell — with each store named by the number
    /// the simulation publishes for it, so this tab and the inspect pane cannot call one store two
    /// things.</para>
    ///
    /// <para><b>Rebuilt only when the stock moves.</b> <see cref="Refresh"/> walks the frame for a
    /// signature every time it is asked and rebuilds only when that changes, so an open tab over a
    /// quiet colony allocates nothing.</para>
    /// </summary>
    public sealed class InventoryModel
    {
        readonly List<InventoryItem> _items = new List<InventoryItem>();
        readonly Dictionary<int, InventoryItem> _byDef = new Dictionary<int, InventoryItem>();
        readonly Dictionary<int, int> _zoneOrdinalAt = new Dictionary<int, int>();
        readonly Dictionary<int, int> _shelfOrdinalAt = new Dictionary<int, int>();
        readonly List<InventoryRow> _paged = new List<InventoryRow>();
        readonly List<int> _pageStarts = new List<int>();
        readonly List<InventoryRow> _rows = new List<InventoryRow>();
        readonly int[] _kinds = new int[StorageSettingsModel.CategoryKeys.Length];
        readonly int[] _matches = new int[StorageSettingsModel.CategoryKeys.Length];
        long _signature = long.MinValue;

        /// <summary>Every unit in every store: the header's figure.</summary>
        public int Total { get; private set; }

        /// <summary>Every item held, in no particular order.</summary>
        public IReadOnlyList<InventoryItem> Items => _items;

        public InventorySort Sort { get; private set; } = InventorySort.Total;

        public string Search { get; private set; } = string.Empty;

        public bool Searching => Search.Length > 0;

        /// <summary>The selected item's def, or -1 when the stores are empty or the search matches nothing.</summary>
        public int SelectedDef { get; private set; } = -1;

        /// <summary>The selected store's ordinal, or 0.</summary>
        public int SelectedPlace { get; private set; }

        public int Page { get; private set; }
        public int PageCount { get; private set; } = 1;
        public bool Paged => PageCount > 1;

        /// <summary>The table's rows on the current page.</summary>
        public IReadOnlyList<InventoryRow> Rows => _rows;

        public InventoryItem? Selected => SelectedDef >= 0 && _byDef.TryGetValue(SelectedDef, out var item) ? item : null;

        public InventoryPlace? SelectedPlaceOf
        {
            get
            {
                InventoryItem? item = Selected;
                if (item == null) return null;
                foreach (InventoryPlace place in item.Places)
                    if (place.Ordinal == SelectedPlace) return place;
                return item.Largest;
            }
        }

        // ------------------------------------------------------------------ reading the frame

        /// <summary>
        /// Read the frame. Returns true when the stock changed and the rows were rebuilt.
        /// </summary>
        /// <param name="categoryOf">A def index's category, as an index into <see cref="StorageSettingsModel.CategoryKeys"/>.</param>
        public bool Refresh(WorldSnapshot frame, Func<int, int> categoryOf)
        {
            long signature = SignatureOf(frame);
            if (signature == _signature) return false;
            _signature = signature;
            Read(frame, categoryOf);
            Rebuild(followSelection: false);
            return true;
        }

        /// <summary>Forget the last frame, so the next <see cref="Refresh"/> rebuilds whatever it finds.</summary>
        public void Invalidate() => _signature = long.MinValue;

        static long SignatureOf(WorldSnapshot frame)
        {
            unchecked
            {
                long hash = 17;
                ReadOnlySpan<ThingView> things = frame.Things;
                for (int i = 0; i < things.Length; i++)
                {
                    ThingView t = things[i];
                    hash = hash * 31 + t.DefIndex;
                    hash = hash * 31 + t.Stack;
                    hash = hash * 31 + t.Container;
                    hash = hash * 31 + ((t.Cell.Y * 4099 + t.Cell.Z) * 4099 + t.Cell.X);
                }
                ReadOnlySpan<StoreView> stores = frame.Stores;
                for (int i = 0; i < stores.Length; i++) hash = hash * 31 + stores[i].CellIndex * 131L + stores[i].Ordinal;
                ReadOnlySpan<StorageUnitView> units = frame.StorageUnits;
                for (int i = 0; i < units.Length; i++) hash = hash * 31 + units[i].CellIndex * 131L + units[i].Ordinal;
                return hash;
            }
        }

        void Read(WorldSnapshot frame, Func<int, int> categoryOf)
        {
            _zoneOrdinalAt.Clear();
            ReadOnlySpan<StoreView> stores = frame.Stores;
            for (int i = 0; i < stores.Length; i++) _zoneOrdinalAt[stores[i].CellIndex] = stores[i].Ordinal;

            _shelfOrdinalAt.Clear();
            ReadOnlySpan<StorageUnitView> units = frame.StorageUnits;
            for (int i = 0; i < units.Length; i++) _shelfOrdinalAt[units[i].CellIndex] = units[i].Ordinal;

            _items.Clear();
            _byDef.Clear();
            Total = 0;

            GridSize size = frame.Size;
            ReadOnlySpan<ThingView> things = frame.Things;
            for (int i = 0; i < things.Length; i++)
            {
                ThingView thing = things[i];
                if (thing.Stack <= 0 || !size.Contains(thing.Cell)) continue;
                int cell = size.Index(thing.Cell.X, thing.Cell.Z, thing.Cell.Y);

                // A contained thing is published at its shelf's cell; a loose one counts only when
                // it lies in a stockpile. Nothing else is stored, and the spec lists nothing else.
                bool shelf = thing.Contained;
                int ordinal;
                if (shelf) { if (!_shelfOrdinalAt.TryGetValue(cell, out ordinal)) continue; }
                else if (!_zoneOrdinalAt.TryGetValue(cell, out ordinal)) continue;

                if (!_byDef.TryGetValue(thing.DefIndex, out InventoryItem? item))
                {
                    int category = categoryOf(thing.DefIndex);
                    item = new InventoryItem
                    {
                        DefIndex = thing.DefIndex,
                        Key = ItemLabels.IconKey(thing.DefIndex),
                        Category = (uint)category < (uint)_kinds.Length ? category : _kinds.Length - 1,
                    };
                    _byDef.Add(thing.DefIndex, item);
                    _items.Add(item);
                }

                InventoryPlace? place = null;
                foreach (InventoryPlace p in item.Places)
                    if (p.Ordinal == ordinal && p.Shelf == shelf) { place = p; break; }
                if (place == null)
                {
                    place = new InventoryPlace { Ordinal = ordinal, Shelf = shelf, Cell = thing.Cell };
                    item.Places.Add(place);
                }

                place.Quantity += thing.Stack;
                if (thing.Stack > place.BestStack)
                {
                    place.BestStack = thing.Stack;
                    place.Cell = thing.Cell;
                }
                item.Total += thing.Stack;
                Total += thing.Stack;
            }

            // Most first; a tie goes to the lower-numbered store, so the order never flickers.
            foreach (InventoryItem item in _items)
                item.Places.Sort((a, b) =>
                {
                    int by = b.Quantity.CompareTo(a.Quantity);
                    return by != 0 ? by : a.Ordinal.CompareTo(b.Ordinal);
                });
        }

        // ------------------------------------------------------------------ what the player does

        /// <summary>The tab has just opened: the item the colony holds most of is selected, at its largest store.</summary>
        public void SelectLargest()
        {
            InventoryItem? best = null;
            foreach (InventoryItem item in _items)
                if (Matches(item) && (best == null || Before(item, best, InventorySort.Total))) best = item;
            Select(best);
            Rebuild(followSelection: true);
        }

        /// <summary>
        /// A click on an item row. The first selects it at its largest store; a second on the
        /// row already selected is a Go to that store, and returns it.
        /// </summary>
        public InventoryPlace? PressItem(int def)
        {
            if (!_byDef.TryGetValue(def, out InventoryItem? item)) return null;
            if (def == SelectedDef) return item.Largest;
            Select(item);
            Rebuild(followSelection: false);
            return null;
        }

        public void SelectPlace(int ordinal) => SelectedPlace = ordinal;

        /// <summary>Returns true when the order changed.</summary>
        public bool SortBy(InventorySort sort)
        {
            if (Sort == sort) return false;
            Sort = sort;
            Rebuild(followSelection: true);
            return true;
        }

        public void SetSearch(string text)
        {
            text ??= string.Empty;
            if (text == Search) return;
            Search = text;

            // One item is always selected while anything shows: a search that hides the selected
            // one moves the selection to the first match rather than leaving the detail pane about
            // a row the table no longer draws.
            // Rebuild picks the largest match when nothing is selected.
            InventoryItem? selected = Selected;
            if (selected == null || !Matches(selected)) Select(null);
            Page = 0;
            Rebuild(followSelection: true);
        }

        public void SetPage(int page)
        {
            Page = Math.Max(0, Math.Min(page, PageCount - 1));
            SlicePage();
        }

        void Select(InventoryItem? item)
        {
            SelectedDef = item?.DefIndex ?? -1;
            SelectedPlace = item?.Largest?.Ordinal ?? 0;
        }

        bool Matches(InventoryItem item) =>
            !Searching || item.Name.IndexOf(Search, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Whether <paramref name="a"/> sorts above <paramref name="b"/>.</summary>
        static bool Before(InventoryItem a, InventoryItem b, InventorySort sort) => Compare(a, b, sort) < 0;

        static int Compare(InventoryItem a, InventoryItem b, InventorySort sort)
        {
            int by = sort switch
            {
                InventorySort.Places => b.Places.Count.CompareTo(a.Places.Count),
                InventorySort.Total => b.Total.CompareTo(a.Total),
                _ => 0,
            };
            if (by != 0) return by;
            if (sort == InventorySort.Places)
            {
                by = b.Total.CompareTo(a.Total);
                if (by != 0) return by;
            }
            by = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            return by != 0 ? by : a.DefIndex.CompareTo(b.DefIndex);
        }

        // ------------------------------------------------------------------ the rows

        /// <summary>Rows per page for a flattened list of <paramref name="count"/> rows.</summary>
        public static int PageSize(int count) =>
            count > InventoryLayout.RowsPerPage ? InventoryLayout.RowsPerPagedPage : InventoryLayout.RowsPerPage;

        void Rebuild(bool followSelection)
        {
            // The selection survives a rebuild only while its item is still held.
            if (SelectedDef >= 0 && !_byDef.ContainsKey(SelectedDef)) Select(null);
            if (SelectedDef < 0)
            {
                InventoryItem? best = null;
                foreach (InventoryItem item in _items)
                    if (Matches(item) && (best == null || Before(item, best, InventorySort.Total))) best = item;
                Select(best);
                followSelection = true;
            }
            else if (Selected!.Largest != null && SelectedPlaceOf!.Ordinal != SelectedPlace)
            {
                SelectedPlace = Selected.Largest.Ordinal;
            }

            Array.Clear(_kinds, 0, _kinds.Length);
            Array.Clear(_matches, 0, _matches.Length);
            foreach (InventoryItem item in _items)
            {
                _kinds[item.Category]++;
                if (Matches(item)) _matches[item.Category]++;
            }

            // The flattened list: six headings in registry order, each with its items under it in
            // the active sort. A search hides the headings with nothing to show.
            var flat = new List<InventoryRow>(_items.Count + _kinds.Length);
            var members = new List<InventoryItem>();
            for (int category = 0; category < _kinds.Length; category++)
            {
                if (Searching && _matches[category] == 0) continue;
                flat.Add(InventoryRow.Group(category, _kinds[category]));
                members.Clear();
                foreach (InventoryItem item in _items)
                    if (item.Category == category && Matches(item)) members.Add(item);
                members.Sort((a, b) => Compare(a, b, Sort));
                foreach (InventoryItem item in members) flat.Add(InventoryRow.Of(item));
            }

            // Pages. A heading whose items run on to the next page is repeated at its top.
            _paged.Clear();
            _pageStarts.Clear();
            int size = PageSize(flat.Count);
            int onPage = 0;
            int selectedPage = -1;
            _pageStarts.Add(0);
            for (int i = 0; i < flat.Count; i++)
            {
                InventoryRow row = flat[i];
                if (onPage == size)
                {
                    _pageStarts.Add(_paged.Count);
                    onPage = 0;
                    if (!row.IsGroup)
                    {
                        _paged.Add(InventoryRow.Group(row.Category, _kinds[row.Category]));
                        onPage++;
                    }
                }
                if (!row.IsGroup && row.Item!.DefIndex == SelectedDef) selectedPage = _pageStarts.Count - 1;
                _paged.Add(row);
                onPage++;
            }

            PageCount = Math.Max(1, _pageStarts.Count);
            if (followSelection && selectedPage >= 0) Page = selectedPage;
            Page = Math.Max(0, Math.Min(Page, PageCount - 1));
            SlicePage();
        }

        void SlicePage()
        {
            _rows.Clear();
            if (_paged.Count == 0) return;
            int start = _pageStarts[Page];
            int end = Page + 1 < _pageStarts.Count ? _pageStarts[Page + 1] : _paged.Count;
            for (int i = start; i < end; i++) _rows.Add(_paged[i]);
        }

        // ------------------------------------------------------------------ words

        /// <summary>"1,026": the header's figure, with thousands separators.</summary>
        public static string Figure(int value) => value.ToString("#,0", CultureInfo.InvariantCulture);

        /// <summary>"Materials, in 3 places", or "in 1 place".</summary>
        public static string Meta(InventoryItem item)
        {
            string category = StorageSettingsModel.Capitalise(
                Registry.Label(StorageSettingsModel.CategoryKeys[item.Category]));
            return item.Places.Count == 1
                ? Registry.Label(InventoryDirector.InPlaceKey).Replace("{category}", category)
                : Registry.Label(InventoryDirector.InPlacesKey)
                    .Replace("{category}", category)
                    .Replace("{count}", item.Places.Count.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>"Go to Stockpile 3".</summary>
        public static string GoTo(InventoryPlace place) =>
            Registry.Label(InventoryDirector.GoToKey).Replace("{place}", place.Name);
    }
}
