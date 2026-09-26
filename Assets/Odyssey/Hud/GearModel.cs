#nullable enable
using System.Collections.Generic;
using System.Globalization;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// A place on a body a row of gear is held in (design 47 §2). <see cref="Weapon"/> keeps the
    /// value it had when it was the only one.
    /// </summary>
    public enum GearSlot
    {
        /// <summary>The weapon in the hand, or the bare hands.</summary>
        Weapon,

        /// <summary>A cap, a hood or a helmet.</summary>
        Head,

        /// <summary>Glasses, a mask or a gas mask.</summary>
        Face,

        /// <summary>One garment; with nothing here, the issued jumpsuit.</summary>
        Body,

        /// <summary>A pack, which opens four more kit slots.</summary>
        Back,

        /// <summary>A vest worn over the garment.</summary>
        Armour,
    }

    /// <summary>How a held weapon is carried right now (design 33 §8b).</summary>
    public enum GearCarry
    {
        /// <summary>Nothing held: the bare hands.</summary>
        None,

        /// <summary>Sheathed at the left hip: held, and no reason to fight with it now.</summary>
        AtHip,

        /// <summary>Out, in the right hand: <see cref="PawnFlags.Drawn"/>.</summary>
        Drawn,
    }

    /// <summary>Which figure an effect on the Gear tab is, so it can be judged (<see cref="GearModel.Ink"/>).</summary>
    public enum GearEffectKind
    {
        Armour,
        Warmth,
        Rain,
        Kit,
    }

    /// <summary>What a slot tile shows (design 47 §2, the specification's slot states).</summary>
    public enum GearSlotState
    {
        /// <summary>Nothing there: a dashed tile, "Nothing worn" or "Bare hands".</summary>
        Empty,

        /// <summary>A thing in the slot.</summary>
        Filled,

        /// <summary>The empty Body slot: the issued jumpsuit, which is worn and is not an item.</summary>
        Jumpsuit,
    }

    /// <summary>One row of what a pawn holds: the slot, the thing in it, how it is carried.</summary>
    public struct GearRow
    {
        public GearSlot Slot;

        /// <summary>The slot's word, from the registry: "Head", "Weapon".</summary>
        public string SlotName;

        public GearSlotState State;

        /// <summary>The item def index of a real thing held, or −1 (the bare hands, and every made-up thing).</summary>
        public int ItemDef;

        /// <summary>What is there, from the registry: the thing's name, "Bare hands", "Nothing worn", "Issued jumpsuit".</summary>
        public string Name;

        /// <summary>The thing's icon key, or empty for nothing.</summary>
        public string IconKey;

        /// <summary>A <see cref="QualityHandle"/> tier, 0 for a thing that has none (every weapon today).</summary>
        public int Quality;

        /// <summary>The tier's word as the tile writes it — "Uber" — or, for the jumpsuit, "Always worn"; empty for none.</summary>
        public string QualityWord;

        public GearCarry Carry;

        /// <summary>How it is carried, from the registry: "Drawn" or "At the hip"; empty for the bare hands.</summary>
        public string CarryWord;

        /// <summary>Made up by the preview (design 47 §4): its buttons edit the preview, never the world.</summary>
        public bool Preview;

        /// <summary>Up to two effect lines for the popover: a word and a figure each; empty when it does nothing.</summary>
        public string EffectA, EffectAValue, EffectB, EffectBValue;

        /// <summary>Which figure each popover line is, so the view can judge it (<see cref="GearModel.Ink"/>).</summary>
        public GearEffectKind EffectAKind, EffectBKind;

        /// <summary>
        /// The thing's own armour, and the comfortable range she would have in it alone, in
        /// centi-degrees — what its popover lines write, kept as numbers so they can be judged.
        /// </summary>
        public int Armour, WarmthLow, WarmthHigh;
    }

    /// <summary>What a kit tile shows.</summary>
    public enum KitTileState
    {
        Empty,
        Filled,

        /// <summary>A pack slot with no pack on the back.</summary>
        Locked,
    }

    public struct KitTile
    {
        public KitTileState State;
        public string IconKey;
        public string Name;
        public int Count;

        /// <summary>The count as the badge writes it; empty when the tile holds nothing.</summary>
        public string CountText;

        /// <summary>Its index in the preview's kit, for Remove and Drop; −1 for a tile with nothing in it.</summary>
        public int PreviewIndex;

        /// <summary>
        /// The colonist's real kit slot this tile draws (design 54), for the orders; −1 for a
        /// locked tile and every tile while the preview is on.
        /// </summary>
        public int Slot;

        /// <summary>The item def of a real thing in the slot, or −1.</summary>
        public int ItemDef;

        /// <summary>Whether Use can be pressed, a <see cref="KitUseHandle"/>; <see cref="KitUseHandle.None"/> for a thing with no use.</summary>
        public int Use;

        /// <summary>Why Use is greyed — "Not hurt", "Not hungry" — or empty.</summary>
        public string UseReason;

        /// <summary>A real thing in a real slot: its buttons send orders rather than edit the preview.</summary>
        public bool Real => Slot >= 0 && ItemDef >= 0;
    }

    /// <summary>
    /// One figure on the effects line. Every figure is always on the line, so it never reflows;
    /// its colour is <see cref="GearModel.Ink"/>'s, from the numbers kept beside the words.
    /// </summary>
    public struct GearEffect
    {
        public string Label;
        public string Value;
        public GearEffectKind Kind;

        /// <summary>Armour in per cent (<see cref="GearEffectKind.Armour"/>).</summary>
        public int Armour;

        /// <summary>The comfortable range in centi-degrees (<see cref="GearEffectKind.Warmth"/>).</summary>
        public int WarmthLow, WarmthHigh;
    }

    /// <summary>
    /// What a pawn holds, wears and keeps on her — everything the Gear tab draws (design 47, from
    /// the gear seam of design 33 §9d). Unity-free; the view reads it and asks no aspect itself.
    ///
    /// <para><b>What is real</b>: the hand — the weapon from <c>odyssey.pawn.weapon</c>, drawn or at
    /// the hip from <see cref="PawnFlags.Drawn"/>, or the bare hands — whether she is downed, and
    /// since design 54 <b>the belt's two kit slots</b>, from <c>odyssey.pawn.kit.*</c>.
    /// <b>What is not yet</b>: no garment or loadout exists in the simulation, so a colonist wears
    /// the issued jumpsuit, the other four slots are empty, the pack slots are locked and every
    /// effect but the kit's is the bare default — unless the <see cref="GearPreview"/> is on, when
    /// the specification's 21c is laid over everything but the hand, the kit row included.</para>
    ///
    /// <para><b>A refresh builds nothing when nothing moved</b>: the pane refreshes fifteen times a
    /// second, so the rows are rebuilt only when the weapon, its carry, the downed flag, the
    /// subject or the preview changed, and <see cref="Version"/> moves when they are.</para>
    /// </summary>
    public sealed class GearModel
    {
        public const string WeaponKey = "ui.combat.weapon";
        public const string BareHandsKey = "ui.combat.barehands";
        public const string DrawnKey = "ui.combat.drawn";
        public const string AtHipKey = "ui.combat.athip";
        public const string NothingWornKey = "ui.gear.nothingworn";
        public const string JumpsuitKey = "ui.gear.jumpsuit";
        public const string AlwaysWornKey = "ui.gear.alwaysworn";
        public const string KitKey = "ui.gear.kit";
        public const string PackHintKey = "ui.gear.packhint";
        public const string KitCountKey = "ui.gear.kitcount";
        public const string LoadoutKey = "ui.gear.loadout";
        public const string DownedReasonKey = "ui.gear.downedreason";
        public const string ArmourKey = "ui.gear.effect.armour";
        public const string WarmthKey = "ui.gear.effect.warmth";
        public const string RainKey = "ui.gear.effect.rain";
        public const string KitEffectKey = "ui.gear.effect.kit";

        /// <summary>How many <see cref="GearSlot"/> values there are.</summary>
        public const int SlotCount = 6;

        /// <summary>
        /// The doll's order: the left column top to bottom, then the right. The weapon is lowest on
        /// the right, at the hand.
        /// </summary>
        public static readonly GearSlot[] Order =
        {
            GearSlot.Head, GearSlot.Face, GearSlot.Body, GearSlot.Back, GearSlot.Armour, GearSlot.Weapon,
        };

        /// <summary>
        /// The bare colonist's comfortable range in centi-degrees: the jumpsuit's, 16 to 26 °C
        /// (design 28, <c>Temperature.xml</c>'s comfort band). A copy the interface keeps only
        /// until worn things exist — G5 publishes the real range per colonist and this goes.
        /// </summary>
        public const int BareComfortLow = StatInks.ComfortLow, BareComfortHigh = StatInks.ComfortHigh;

        /// <summary>The rows, in <see cref="Order"/>. Empty for a pawn not in the frame and for an animal.</summary>
        public readonly List<GearRow> Rows = new List<GearRow>();

        /// <summary>Six kit tiles: the belt's two, then the pack's four.</summary>
        public readonly List<KitTile> Kit = new List<KitTile>();

        /// <summary>Armour, warmth, rain, kit — the effects line, left to right.</summary>
        public readonly List<GearEffect> Effects = new List<GearEffect>();

        /// <summary>No pack: the hint after the locked tiles is shown, once.</summary>
        public bool PackHint { get; private set; }

        /// <summary>She is downed: every control on the tab is disabled, with <see cref="DownedReason"/>.</summary>
        public bool Downed { get; private set; }

        public string DownedReason => Registry.Label(DownedReasonKey);

        /// <summary>The loadout's name, or "None".</summary>
        public string LoadoutName { get; private set; } = string.Empty;

        /// <summary>Is a loadout set? "None" is drawn dim.</summary>
        public bool HasLoadout { get; private set; }

        /// <summary>Moves every time the rows are rebuilt, so the view writes only on a change.</summary>
        public int Version { get; private set; }

        /// <summary>The preview this model lays over the real hand, if any.</summary>
        public GearPreview? Preview { get; set; }

        // What the rows were last built from.
        int _builtPawn = int.MinValue, _builtWeapon = int.MinValue, _builtTier = -1, _builtCarry = -1, _builtPreview = -1;
        int _builtKit = int.MinValue;

        // The real kit as the frame publishes it (design 54 §6), read every refresh into these and
        // folded into one signature, so a changed count rebuilds the row and an unchanged one does not.
        readonly int[] _kitDef = new int[KitOrders.Slots], _kitCount = new int[KitOrders.Slots], _kitUse = new int[KitOrders.Slots];
        bool _builtDowned, _builtPerson;

        /// <summary>The row for one slot. Only meaningful after a refresh that returned true for a person.</summary>
        public GearRow Row(GearSlot slot)
        {
            for (int i = 0; i < Rows.Count; i++)
                if (Rows[i].Slot == slot) return Rows[i];
            return default;
        }

        /// <summary>
        /// Refill the model for <paramref name="pawn"/>. False, and no rows, when the frame no longer
        /// carries that pawn; true and no rows for an animal; true and six rows for a person — a
        /// colonist or a bandit.
        /// </summary>
        public bool Refresh(WorldSnapshot snapshot, PawnId pawn)
        {
            if (!snapshot.TryGetPawn(pawn, out PawnView view))
            {
                Clear();
                return false;
            }
            if (!view.IsPerson)
            {
                Clear();
                return true;
            }

            bool armed = snapshot.TryGetPawnAspect(pawn, CombatAspectNames.WeaponKey, out int def) && def >= 0;
            int tier = snapshot.TryGetPawnAspect(pawn, CombatAspectNames.WeaponQualityKey, out int q) ? q : 0;
            GearCarry carry = !armed ? GearCarry.None : view.IsWeaponDrawn ? GearCarry.Drawn : GearCarry.AtHip;
            int weapon = armed ? def : -1;
            int preview = Preview != null ? (Preview.On ? 1 : 0) + (Preview.Version << 1) : -1;
            int kit = ReadKit(snapshot, pawn);

            if (_builtPerson && _builtPawn == pawn.Value && _builtWeapon == weapon && _builtTier == tier && _builtCarry == (int)carry
                && _builtDowned == view.IsDowned && _builtPreview == preview && _builtKit == kit)
                return true;

            _builtPerson = true;
            _builtPawn = pawn.Value;
            _builtWeapon = weapon;
            _builtTier = tier;
            _builtCarry = (int)carry;
            _builtDowned = view.IsDowned;
            _builtPreview = preview;
            _builtKit = kit;
            Build(pawn, weapon, tier, carry, view.IsDowned);
            return true;
        }

        /// <summary>Read her kit's slots out of the frame; returns their signature.</summary>
        int ReadKit(WorldSnapshot snapshot, PawnId pawn)
        {
            int signature = 17;
            for (int slot = 0; slot < KitOrders.Slots; slot++)
            {
                _kitDef[slot] = snapshot.TryGetPawnAspect(pawn, KitOrders.DefKey(slot), out int def) ? def : -1;
                _kitCount[slot] = _kitDef[slot] >= 0 && snapshot.TryGetPawnAspect(pawn, KitOrders.CountKey(slot), out int count) ? count : 0;
                _kitUse[slot] = _kitDef[slot] >= 0 && snapshot.TryGetPawnAspect(pawn, KitOrders.UseKey(slot), out int use) ? use : KitUseHandle.None;
                signature = unchecked(((signature * 31 + _kitDef[slot]) * 31 + _kitCount[slot]) * 31 + _kitUse[slot]);
            }
            return signature;
        }

        void Clear()
        {
            bool had = Rows.Count > 0 || Kit.Count > 0;
            Rows.Clear();
            Kit.Clear();
            Effects.Clear();
            _builtPerson = false;
            _builtPawn = int.MinValue;
            _builtKit = int.MinValue;
            if (had) Version++;
        }

        void Build(PawnId pawn, int weapon, int tier, GearCarry carry, bool downed)
        {
            Rows.Clear();
            Kit.Clear();
            Effects.Clear();
            Downed = downed;

            int armour = 0, low = BareComfortLow, high = BareComfortHigh, rain = 0;
            bool pack = false;

            foreach (GearSlot slot in Order)
            {
                if (slot == GearSlot.Weapon)
                {
                    Rows.Add(WeaponRow(weapon, tier, carry));
                    continue;
                }

                if (Preview != null && Preview.TryWorn(pawn, slot, out PreviewItem item))
                {
                    armour += item.Armour;
                    low += item.WarmthLow;
                    high += item.WarmthHigh;
                    rain += item.Rain;
                    if (slot == GearSlot.Back) pack = true;
                    Rows.Add(PreviewRow(slot, item));
                    continue;
                }

                Rows.Add(slot == GearSlot.Body
                    ? new GearRow
                    {
                        Slot = slot, SlotName = SlotLabel(slot), State = GearSlotState.Jumpsuit, ItemDef = -1,
                        Name = Registry.Label(JumpsuitKey), IconKey = JumpsuitKey, QualityWord = Registry.Label(AlwaysWornKey),
                        CarryWord = string.Empty, EffectA = string.Empty, EffectAValue = string.Empty,
                        EffectB = string.Empty, EffectBValue = string.Empty,
                    }
                    : Nothing(slot, Registry.Label(NothingWornKey)));
            }

            // The kit: two on the belt, four more with a pack. What is in them is her real kit (design
            // 54) — unless the preview is on, when the whole row is the preview's, as the worn slots are.
            bool previewKit = Preview != null && Preview.On;
            IReadOnlyList<PreviewKit> kit = previewKit
                ? Preview!.Kit(pawn)
                : (IReadOnlyList<PreviewKit>)System.Array.Empty<PreviewKit>();
            int capacity = GearLayout.KitBelt + (pack ? GearLayout.KitPack : 0);
            int used = 0;
            for (int i = 0; i < GearLayout.KitSlots; i++)
            {
                if (i >= capacity)
                {
                    Kit.Add(EmptyKit(KitTileState.Locked, -1));
                    continue;
                }
                if (previewKit && i < kit.Count)
                {
                    PreviewKit stack = kit[i];
                    used++;
                    KitTile tile = EmptyKit(KitTileState.Filled, -1);
                    tile.IconKey = stack.Key;
                    tile.Name = Registry.Label(stack.Key);
                    tile.Count = stack.Count;
                    tile.CountText = stack.Count.ToString(CultureInfo.InvariantCulture);
                    tile.PreviewIndex = i;
                    Kit.Add(tile);
                    continue;
                }
                if (!previewKit && i < KitOrders.Slots && _kitDef[i] >= 0)
                {
                    used++;
                    KitTile tile = EmptyKit(KitTileState.Filled, i);
                    tile.ItemDef = _kitDef[i];
                    tile.IconKey = ItemLabels.IconKey(_kitDef[i]);
                    tile.Name = ItemLabels.Label(_kitDef[i]);
                    tile.Count = _kitCount[i];
                    tile.CountText = _kitCount[i].ToString(CultureInfo.InvariantCulture);
                    tile.Use = _kitUse[i];
                    tile.UseReason = KitOrders.UseReason(_kitUse[i]);
                    Kit.Add(tile);
                    continue;
                }
                Kit.Add(EmptyKit(KitTileState.Empty, previewKit || i >= KitOrders.Slots ? -1 : i));
            }
            PackHint = !pack;

            if (rain > 100) rain = 100;
            Effects.Add(new GearEffect
            {
                Label = Registry.Label(ArmourKey), Value = Percent(armour), Kind = GearEffectKind.Armour, Armour = armour,
            });
            Effects.Add(new GearEffect
            {
                Label = Registry.Label(WarmthKey), Value = TemperatureLabels.Range(low, high),
                Kind = GearEffectKind.Warmth, WarmthLow = low, WarmthHigh = high,
            });
            Effects.Add(new GearEffect { Label = Registry.Label(RainKey), Value = Percent(rain), Kind = GearEffectKind.Rain });
            Effects.Add(new GearEffect
            {
                Label = Registry.Label(KitEffectKey), Value = KitCount(used, capacity), Kind = GearEffectKind.Kit,
            });

            int loadout = Preview != null ? Preview.Loadout(pawn) : 0;
            HasLoadout = loadout > 0;
            LoadoutName = Registry.Label(GearPreview.LoadoutKeys[loadout]);

            Version++;
        }

        /// <summary>
        /// The ink a Gear figure is drawn in (design 59; owner, 2026-09-26): armour on
        /// <see cref="StatInks.Armour"/>, so 0 % is red; warmth by <see cref="StatInks.Comfort"/>
        /// against <paramref name="outdoorCentiC"/>, the clock's reading, because a comfortable range
        /// is good or bad only for the weather she walks out into; rain and the kit never judged —
        /// rain the owner asked to stay "a neutral bright white", and the kit is a count. With no
        /// reading (no colony) warmth is neutral too.
        /// </summary>
        public static HudColour Ink(GearEffectKind kind, int armour, int warmthLow, int warmthHigh, int? outdoorCentiC) =>
            kind switch
            {
                GearEffectKind.Armour => StatInks.Ink(StatInks.Armour, armour),
                GearEffectKind.Warmth when outdoorCentiC.HasValue =>
                    StatInks.Ink(StatInks.Comfort(warmthLow, warmthHigh, outdoorCentiC.Value)),
                _ => HudTheme.TextPrimary,
            };

        /// <summary>The ink of one figure on the effects line.</summary>
        public static HudColour Ink(in GearEffect effect, int? outdoorCentiC) =>
            Ink(effect.Kind, effect.Armour, effect.WarmthLow, effect.WarmthHigh, outdoorCentiC);

        static KitTile EmptyKit(KitTileState state, int slot) => new KitTile
        {
            State = state, IconKey = string.Empty, Name = string.Empty, CountText = string.Empty,
            PreviewIndex = -1, Slot = slot, ItemDef = -1, UseReason = string.Empty,
        };

        /// <summary>"4 of 6": the registry's words round the two figures.</summary>
        public static string KitCount(int used, int capacity) =>
            Registry.Label(KitCountKey)
                .Replace("{used}", used.ToString(CultureInfo.InvariantCulture))
                .Replace("{capacity}", capacity.ToString(CultureInfo.InvariantCulture));

        static string Percent(int value) => value.ToString(CultureInfo.InvariantCulture) + "%";

        /// <summary>The slot's word from the registry: "Head", "Armour", "Weapon".</summary>
        public static string SlotLabel(GearSlot slot) => Registry.Label(slot switch
        {
            GearSlot.Head => "ui.gear.slot.head",
            GearSlot.Face => "ui.gear.slot.face",
            GearSlot.Body => "ui.gear.slot.body",
            GearSlot.Back => "ui.gear.slot.back",
            GearSlot.Armour => "ui.gear.slot.armour",
            _ => WeaponKey,
        });

        /// <summary>A quality tier as the tile writes it: "Uber", capitalised, beside the slot word.</summary>
        public static string QualityWord(int tier)
        {
            string key = QualityLabels.Key(tier);
            return key.Length == 0 ? string.Empty : Registry.Label(key);
        }

        static GearRow Nothing(GearSlot slot, string name) => new GearRow
        {
            Slot = slot, SlotName = SlotLabel(slot), State = GearSlotState.Empty, ItemDef = -1, Name = name,
            IconKey = string.Empty, QualityWord = string.Empty, CarryWord = string.Empty,
            EffectA = string.Empty, EffectAValue = string.Empty, EffectB = string.Empty, EffectBValue = string.Empty,
        };

        static GearRow WeaponRow(int weapon, int tier, GearCarry carry)
        {
            if (weapon < 0) return Nothing(GearSlot.Weapon, Registry.Label(BareHandsKey));
            GearRow row = Nothing(GearSlot.Weapon, ItemLabels.Label(weapon));
            row.State = GearSlotState.Filled;
            row.ItemDef = weapon;
            // How well it was made (design 47 §11, ranged combat) beside the slot word, as a worn
            // thing's is, rather than bracketed into the name as the inspect pane writes it.
            row.Quality = tier;
            row.QualityWord = QualityWord(tier);
            row.IconKey = ItemLabels.IconKey(weapon);
            row.Carry = carry;
            row.CarryWord = carry == GearCarry.Drawn ? Registry.Label(DrawnKey) : Registry.Label(AtHipKey);
            return row;
        }

        static GearRow PreviewRow(GearSlot slot, in PreviewItem item)
        {
            GearRow row = Nothing(slot, Registry.Label(item.Key));
            row.State = GearSlotState.Filled;
            row.IconKey = item.Key;
            row.Quality = item.Quality;
            row.QualityWord = QualityWord(item.Quality);
            row.Preview = true;

            // The popover's lines: what this one thing does, largest first, at most two.
            row.Armour = item.Armour;
            row.WarmthLow = BareComfortLow + item.WarmthLow;
            row.WarmthHigh = BareComfortHigh + item.WarmthHigh;
            int lines = 0;
            if (item.Armour != 0) Line(ref row, ref lines, GearEffectKind.Armour, ArmourKey, Percent(item.Armour));
            if (item.WarmthLow != 0 || item.WarmthHigh != 0)
                Line(ref row, ref lines, GearEffectKind.Warmth, WarmthKey, TemperatureLabels.Range(row.WarmthLow, row.WarmthHigh));
            if (item.Rain != 0) Line(ref row, ref lines, GearEffectKind.Rain, RainKey, Percent(item.Rain));
            return row;
        }

        static void Line(ref GearRow row, ref int lines, GearEffectKind kind, string key, string value)
        {
            if (lines == 0)
            {
                row.EffectA = Registry.Label(key);
                row.EffectAValue = value;
                row.EffectAKind = kind;
            }
            else if (lines == 1)
            {
                row.EffectB = Registry.Label(key);
                row.EffectBValue = value;
                row.EffectBKind = kind;
            }
            lines++;
        }
    }
}
