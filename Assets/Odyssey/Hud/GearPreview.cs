#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>One made-up thing the preview can dress a colonist in (design 47 §4).</summary>
    public readonly struct PreviewItem
    {
        public readonly GearSlot Slot;
        public readonly string Key;

        /// <summary>A <see cref="QualityHandle"/> tier: 1 Poor … 5 Epic.</summary>
        public readonly int Quality;

        /// <summary>Per cent of a blow turned aside.</summary>
        public readonly int Armour;

        /// <summary>How far it moves the comfortable range, in centi-degrees: the low end down, the high end up.</summary>
        public readonly int WarmthLow, WarmthHigh;

        /// <summary>Per cent of the rain's slowdown bought back.</summary>
        public readonly int Rain;

        public PreviewItem(GearSlot slot, string key, int quality, int armour = 0, int warmthLow = 0,
            int warmthHigh = 0, int rain = 0)
        {
            Slot = slot;
            Key = key;
            Quality = quality;
            Armour = armour;
            WarmthLow = warmthLow;
            WarmthHigh = warmthHigh;
            Rain = rain;
        }
    }

    /// <summary>One made-up stack in the preview's kit.</summary>
    public readonly struct PreviewKit
    {
        public readonly string Key;
        public readonly int Count;

        public PreviewKit(string key, int count)
        {
            Key = key;
            Count = count;
        }
    }

    /// <summary>
    /// The Gear tab's preview (design 47 §4, owner 2026-09-25: <i>"tab + weapon real, rest
    /// preview"</i>): a debug-menu switch that dresses every colonist's Gear tab in a made-up kit —
    /// the specification's 21c — so the tab can be judged in the running game before a single
    /// garment exists in the simulation.
    ///
    /// <para><b>Interface state and nothing else</b>, the way the Research tab's placeholder state
    /// is (design 34): never saved, never hashed, never sent to the simulation, and off at the
    /// start of every session. Nothing is worn: a colonist in a "Field coat" is exactly as cold as
    /// one without. <b>The weapon is never made up</b> — the hand is real, and it is the one slot
    /// whose buttons reach the world.</para>
    ///
    /// <para>Remove and Drop on a made-up thing edit the preview, so the popovers can be tried;
    /// the edits are per colonist and forgotten when the preview goes off.</para>
    /// </summary>
    public sealed class GearPreview
    {
        /// <summary>
        /// Everything the preview can put on, in the order Pick from stores lists it. The first of
        /// each slot marked <c>dressed</c> below is what a colonist starts the preview wearing.
        /// </summary>
        public static readonly PreviewItem[] Catalogue =
        {
            new PreviewItem(GearSlot.Head, "ui.item.woolcap", 2, warmthLow: -200),
            new PreviewItem(GearSlot.Head, "ui.item.helmet", 3, armour: 10),
            new PreviewItem(GearSlot.Face, "ui.item.gasmask", 3),
            new PreviewItem(GearSlot.Face, "ui.item.mask", 1),
            new PreviewItem(GearSlot.Body, "ui.item.fieldcoat", 4, armour: 6, warmthLow: -1000, rain: 50),
            new PreviewItem(GearSlot.Body, "ui.item.jacket", 2, warmthLow: -500),
            new PreviewItem(GearSlot.Back, "ui.item.pack", 1),
            new PreviewItem(GearSlot.Armour, "ui.item.paddedvest", 2, armour: 18),
            new PreviewItem(GearSlot.Armour, "ui.item.vest", 3, armour: 12),
        };

        /// <summary>The specification's 21c: what every colonist wears while the preview is on, until edited.</summary>
        static readonly int[] Dressed = { 0, 2, 4, 6, 7 };

        /// <summary>The specification's 21c kit: medical supplies 4, rations 2, a torch, a canteen.</summary>
        public static readonly PreviewKit[] StartingKit =
        {
            new PreviewKit("ui.res.medkit", 4),
            new PreviewKit("ui.res.rations", 2),
            new PreviewKit("ui.item.flashlight", 1),
            new PreviewKit("ui.item.canteen", 1),
        };

        /// <summary>The loadouts the preview offers, after None: index 1 Doctor, 2 Winter.</summary>
        public static readonly string[] LoadoutKeys =
        {
            "ui.gear.loadout.none", "ui.gear.loadout.doctor", "ui.gear.loadout.winter",
        };

        sealed class Dress
        {
            /// <summary>Per <see cref="GearSlot"/>: an index into <see cref="Catalogue"/>, or −1 for nothing.</summary>
            public readonly int[] Worn = new int[GearModel.SlotCount];
            public readonly List<PreviewKit> Kit = new List<PreviewKit>(StartingKit);
            public int Loadout = 1;

            public Dress()
            {
                for (int i = 0; i < Worn.Length; i++) Worn[i] = -1;
                foreach (int index in Dressed) Worn[(int)Catalogue[index].Slot] = index;
            }
        }

        readonly Dictionary<int, Dress> _dress = new Dictionary<int, Dress>();

        /// <summary>Is the preview on?</summary>
        public bool On { get; private set; }

        /// <summary>Moves on every change, so a model built from the preview knows to build again.</summary>
        public int Version { get; private set; }

        /// <summary>Switch it on or off. Either way every edit is forgotten: each switch-on starts at 21c.</summary>
        public void Set(bool on)
        {
            if (On == on) return;
            On = on;
            _dress.Clear();
            Version++;
        }

        /// <summary>A new session: off, and nothing remembered.</summary>
        public void Reset() => Set(false);

        Dress For(PawnId pawn)
        {
            if (!_dress.TryGetValue(pawn.Value, out Dress? dress))
            {
                dress = new Dress();
                _dress.Add(pawn.Value, dress);
            }
            return dress;
        }

        /// <summary>What the colonist wears in <paramref name="slot"/> while the preview is on, if anything.</summary>
        public bool TryWorn(PawnId pawn, GearSlot slot, out PreviewItem item)
        {
            item = default;
            if (!On || slot == GearSlot.Weapon) return false;
            int index = For(pawn).Worn[(int)slot];
            if (index < 0) return false;
            item = Catalogue[index];
            return true;
        }

        /// <summary>The preview's kit for this colonist, in slot order. Empty while the preview is off.</summary>
        public IReadOnlyList<PreviewKit> Kit(PawnId pawn) =>
            On ? For(pawn).Kit : (IReadOnlyList<PreviewKit>)System.Array.Empty<PreviewKit>();

        /// <summary>An index into <see cref="LoadoutKeys"/>: 0 none while the preview is off.</summary>
        public int Loadout(PawnId pawn) => On ? For(pawn).Loadout : 0;

        /// <summary>Take a made-up thing off. Taking off the pack loses the four kit slots it gave, and what was in them.</summary>
        public void Remove(PawnId pawn, GearSlot slot)
        {
            if (!On || slot == GearSlot.Weapon) return;
            Dress dress = For(pawn);
            if (dress.Worn[(int)slot] < 0) return;
            dress.Worn[(int)slot] = -1;
            if (slot == GearSlot.Back && dress.Kit.Count > GearLayout.KitBelt)
                dress.Kit.RemoveRange(GearLayout.KitBelt, dress.Kit.Count - GearLayout.KitBelt);
            Version++;
        }

        /// <summary>Put on a thing from <see cref="Catalogue"/> — the preview's answer to a Pick from stores row.</summary>
        public void Wear(PawnId pawn, int catalogueIndex)
        {
            if (!On || (uint)catalogueIndex >= (uint)Catalogue.Length) return;
            Dress dress = For(pawn);
            int slot = (int)Catalogue[catalogueIndex].Slot;
            if (dress.Worn[slot] == catalogueIndex) return;
            dress.Worn[slot] = catalogueIndex;
            Version++;
        }

        /// <summary>Take one stack out of the kit.</summary>
        public void RemoveKit(PawnId pawn, int index)
        {
            if (!On) return;
            Dress dress = For(pawn);
            if ((uint)index >= (uint)dress.Kit.Count) return;
            dress.Kit.RemoveAt(index);
            Version++;
        }

        /// <summary>Choose a loadout by its index in <see cref="LoadoutKeys"/>.</summary>
        public void SetLoadout(PawnId pawn, int loadout)
        {
            if (!On || (uint)loadout >= (uint)LoadoutKeys.Length) return;
            Dress dress = For(pawn);
            if (dress.Loadout == loadout) return;
            dress.Loadout = loadout;
            Version++;
        }
    }
}
