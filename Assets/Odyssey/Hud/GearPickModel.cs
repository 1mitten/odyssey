#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>One row of Pick from stores: a thing that fits the slot, and where it is kept.</summary>
    public struct GearPickRow
    {
        public string IconKey;
        public string Name;
        public int Quality;
        public string QualityWord;

        /// <summary>The store's name, "Stockpile 3" — or, for a made-up thing, the preview's word.</summary>
        public string Place;

        /// <summary>The real thing, for the equip order; <c>default</c> for a made-up one.</summary>
        public ThingView Thing;

        /// <summary>An index into <see cref="GearPreview.Catalogue"/> for a made-up thing, else −1.</summary>
        public int PreviewIndex;
    }

    /// <summary>
    /// Pick from stores (design 47 §3, the specification's 21e): what the colony's stores hold that
    /// fits one empty slot, a row each, at most <see cref="GearLayout.PickMaxRows"/> a page.
    ///
    /// <para><b>For the hand it is real</b>: every weapon lying in a stockpile or on a shelf, named
    /// by the store that holds it in the one form the pane and the Inventory tab write
    /// (<see cref="InventoryPlace.NameOf"/>), and a row chosen is the same <c>OrderEquip</c> the
    /// right-click menu sends (<see cref="CombatOrders.Equip"/>). A weapon loose on the ground is
    /// not in the stores and is not listed; the right-click menu reaches it. <b>For a worn slot</b>
    /// nothing exists yet, so the list is empty — unless the preview is on, when it lists the
    /// preview's made-up things for that slot, and choosing one dresses the preview.</para>
    ///
    /// <para>The interface does not know what is forbidden, reserved or reachable; the order is
    /// refused at the tick if it cannot be done, as the right-click Equip row's is.</para>
    /// </summary>
    public sealed class GearPickModel
    {
        public const string TitleKey = "ui.gear.pick.title";
        public const string HintKey = "ui.gear.pick.hint";
        public const string EmptyKey = "ui.gear.pick.empty";
        public const string PreviewKey = "ui.gear.preview";

        readonly List<GearPickRow> _all = new List<GearPickRow>();
        readonly Dictionary<int, int> _zoneOrdinalAt = new Dictionary<int, int>();
        readonly Dictionary<int, int> _shelfOrdinalAt = new Dictionary<int, int>();

        /// <summary>The slot being filled.</summary>
        public GearSlot Slot { get; private set; }

        /// <summary>The rows on the current page, at most <see cref="GearLayout.PickMaxRows"/>.</summary>
        public readonly List<GearPickRow> Rows = new List<GearPickRow>();

        public int Page { get; private set; }
        public int PageCount => Math.Max(1, (_all.Count + GearLayout.PickMaxRows - 1) / GearLayout.PickMaxRows);

        /// <summary>Every row across every page.</summary>
        public int Total => _all.Count;

        /// <summary>The slot's word, for the header: "HEAD".</summary>
        public string SlotName => GearModel.SlotLabel(Slot);

        /// <summary>List what fits <paramref name="slot"/>, and go to the first page.</summary>
        public void Build(WorldSnapshot snapshot, GearSlot slot, GearPreview? preview)
        {
            Slot = slot;
            _all.Clear();
            if (slot == GearSlot.Weapon) ReadStoredWeapons(snapshot);
            else if (preview != null && preview.On)
            {
                for (int i = 0; i < GearPreview.Catalogue.Length; i++)
                {
                    PreviewItem item = GearPreview.Catalogue[i];
                    if (item.Slot != slot) continue;
                    _all.Add(new GearPickRow
                    {
                        IconKey = item.Key, Name = Registry.Label(item.Key), Quality = item.Quality,
                        QualityWord = GearModel.QualityWord(item.Quality), Place = Registry.Label(PreviewKey),
                        PreviewIndex = i,
                    });
                }
            }
            Page = 0;
            FillPage();
        }

        void ReadStoredWeapons(WorldSnapshot snapshot)
        {
            _zoneOrdinalAt.Clear();
            ReadOnlySpan<StoreView> stores = snapshot.Stores;
            for (int i = 0; i < stores.Length; i++) _zoneOrdinalAt[stores[i].CellIndex] = stores[i].Ordinal;
            _shelfOrdinalAt.Clear();
            ReadOnlySpan<StorageUnitView> units = snapshot.StorageUnits;
            for (int i = 0; i < units.Length; i++) _shelfOrdinalAt[units[i].CellIndex] = units[i].Ordinal;

            GridSize size = snapshot.Size;
            ReadOnlySpan<ThingView> things = snapshot.Things;
            for (int i = 0; i < things.Length; i++)
            {
                ThingView thing = things[i];
                if (!CombatOrders.IsWeapon(thing.DefIndex) || thing.Stack <= 0 || !size.Contains(thing.Cell)) continue;
                int cell = size.Index(thing.Cell.X, thing.Cell.Z, thing.Cell.Y);
                int ordinal;
                if (thing.Contained) { if (!_shelfOrdinalAt.TryGetValue(cell, out ordinal)) continue; }
                else if (!_zoneOrdinalAt.TryGetValue(cell, out ordinal)) continue;

                _all.Add(new GearPickRow
                {
                    IconKey = ItemLabels.IconKey(thing.DefIndex), Name = ItemLabels.Label(thing.DefIndex),
                    QualityWord = string.Empty, Place = InventoryPlace.NameOf(thing.Contained, ordinal),
                    Thing = thing, PreviewIndex = -1,
                });
            }

            // By name, then by store, then by thing: the same list in the same order every time it opens.
            _all.Sort((a, b) =>
            {
                int by = string.CompareOrdinal(a.Name, b.Name);
                if (by != 0) return by;
                by = string.CompareOrdinal(a.Place, b.Place);
                return by != 0 ? by : a.Thing.Id.Value.CompareTo(b.Thing.Id.Value);
            });
        }

        void FillPage()
        {
            Rows.Clear();
            int first = Page * GearLayout.PickMaxRows;
            for (int i = first; i < _all.Count && i < first + GearLayout.PickMaxRows; i++) Rows.Add(_all[i]);
        }

        public void NextPage()
        {
            if (Page + 1 >= PageCount) return;
            Page++;
            FillPage();
        }

        public void PreviousPage()
        {
            if (Page == 0) return;
            Page--;
            FillPage();
        }

        /// <summary>
        /// Choose a row for <paramref name="colonist"/>: the equip order for a real weapon (true, with
        /// the intent to send), or the preview dressed and nothing sent (false).
        /// </summary>
        public bool Choose(int row, PawnId colonist, GearPreview? preview, out Intent order)
        {
            order = default;
            if ((uint)row >= (uint)Rows.Count) return false;
            GearPickRow pick = Rows[row];
            if (pick.PreviewIndex >= 0)
            {
                preview?.Wear(colonist, pick.PreviewIndex);
                return false;
            }
            order = CombatOrders.Equip(colonist, pick.Thing);
            return true;
        }
    }
}
