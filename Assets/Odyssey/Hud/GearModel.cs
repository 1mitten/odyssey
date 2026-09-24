#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>The place on a body a row of gear is held in. One today; apparel and a pack are the later rows.</summary>
    public enum GearSlot
    {
        /// <summary>The weapon in the hand, or the bare hands.</summary>
        Weapon,
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

    /// <summary>One row of what a pawn holds: the slot, the thing in it, how it is carried.</summary>
    public struct GearRow
    {
        public GearSlot Slot;

        /// <summary>The slot's word, from the registry: "Weapon".</summary>
        public string SlotName;

        /// <summary>The item def index of what is held, or −1 for nothing (the bare hands).</summary>
        public int ItemDef;

        /// <summary>What is held, from the registry: the weapon's name, or "Bare hands".</summary>
        public string Name;

        /// <summary>The item's icon key (<see cref="ItemLabels.IconKey"/>), or empty for the bare hands.</summary>
        public string IconKey;

        public GearCarry Carry;

        /// <summary>How it is carried, from the registry: "Drawn" or "At the hip"; empty for the bare hands.</summary>
        public string CarryWord;
    }

    /// <summary>
    /// What a pawn holds (design 33 §9d, owner: <i>"We'll make a entry for gear later to include
    /// equipped weapon (seam for later)"</i>). <b>A seam and nothing more</b>: the Gear tab stays
    /// disabled with its reason, and nothing draws this yet. It exists so the tab, when it is
    /// built, is built from a model the fast tier already holds, rather than from a second reading
    /// of the aspects written into the view.
    ///
    /// <para>Today it lists one row: the weapon in the hand from <c>odyssey.pawn.weapon</c> (its
    /// name through <see cref="ItemLabels"/> and the registry), drawn or at the hip from
    /// <see cref="PawnFlags.Drawn"/>, or the bare hands when the aspect is absent. The Health tab's
    /// weapon row (<see cref="InspectModel.HealthRows"/>) reads the same aspect and says the same
    /// name, and <c>GearModelTests</c> holds the two together.</para>
    ///
    /// <para>Unity-free; the rows are written into a reused list and every word is a registry
    /// string, so a refresh allocates nothing.</para>
    /// </summary>
    public sealed class GearModel
    {
        public const string WeaponKey = "ui.combat.weapon";
        public const string BareHandsKey = "ui.combat.barehands";
        public const string DrawnKey = "ui.combat.drawn";
        public const string AtHipKey = "ui.combat.athip";

        /// <summary>The rows, in slot order. Empty for a pawn not in the frame and for an animal, which holds nothing.</summary>
        public readonly List<GearRow> Rows = new List<GearRow>();

        /// <summary>
        /// Refill <see cref="Rows"/> for <paramref name="pawn"/>. False, and no rows, when the frame
        /// no longer carries that pawn; true and no rows for an animal; true and one row for a
        /// person — a colonist or a marauder.
        /// </summary>
        public bool Refresh(WorldSnapshot snapshot, PawnId pawn)
        {
            Rows.Clear();
            if (!snapshot.TryGetPawn(pawn, out PawnView view)) return false;
            if (!view.IsPerson) return true;

            bool armed = snapshot.TryGetPawnAspect(pawn, CombatAspectNames.WeaponKey, out int def) && def >= 0;
            GearCarry carry = !armed ? GearCarry.None : view.IsWeaponDrawn ? GearCarry.Drawn : GearCarry.AtHip;

            Rows.Add(new GearRow
            {
                Slot = GearSlot.Weapon,
                SlotName = Registry.Label(WeaponKey),
                ItemDef = armed ? def : -1,
                Name = armed ? ItemLabels.Label(def) : Registry.Label(BareHandsKey),
                IconKey = armed ? ItemLabels.IconKey(def) : string.Empty,
                Carry = carry,
                CarryWord = carry switch
                {
                    GearCarry.Drawn => Registry.Label(DrawnKey),
                    GearCarry.AtHip => Registry.Label(AtHipKey),
                    _ => string.Empty,
                },
            });
            return true;
        }
    }
}
