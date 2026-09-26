#nullable enable
using System.Globalization;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The interface's half of the kit (design 54): the aspect names it reads, the three orders it
    /// sends, and which things fit a kit — every builder in one place, so the right-click menu, the
    /// Gear tab and Pick from stores can never send different orders for the same click.
    ///
    /// <para><b>The caps are a mirror.</b> The interface cannot read the Defs, so
    /// <see cref="Cap"/> repeats <c>ItemDef.kitCap</c> — five medical supplies, three rations —
    /// held to the item table by <c>KitOrdersTests</c> on this side and by
    /// <c>KitTests.OnlyMedicalSuppliesAndRationsFitAKit</c> on the simulation's, as
    /// <see cref="CombatOrders.IsWeapon"/> is. The order is refused at the tick if the mirror is
    /// ever wrong; the mirror only decides what the menu offers.</para>
    /// </summary>
    public static class KitOrders
    {
        /// <summary>Kit slots a colonist has: the belt's two (<see cref="GearLayout.KitBelt"/>).</summary>
        public const int Slots = 2;

        public const string Prefix = "odyssey.pawn.kit.";

        public const string TakeKey = "ui.command.takeintokit";
        public const string UseCommandKey = "ui.command.use";
        public const string KitFullKey = "ui.gear.reason.kitfull";
        public const string NotHurtKey = "ui.gear.reason.nothurt";
        public const string NotHungryKey = "ui.gear.reason.nothungry";

        static readonly AspectKey[] DefKeys = Keys(string.Empty);
        static readonly AspectKey[] CountKeys = Keys(".count");
        static readonly AspectKey[] UseKeys = Keys(".use");

        static AspectKey[] Keys(string suffix)
        {
            var keys = new AspectKey[Slots];
            for (int slot = 0; slot < Slots; slot++)
                keys[slot] = AspectKey.Of(Prefix + slot.ToString(CultureInfo.InvariantCulture) + suffix);
            return keys;
        }

        /// <summary><c>odyssey.pawn.kit.&lt;slot&gt;</c>: the def in that slot.</summary>
        public static AspectKey DefKey(int slot) => DefKeys[slot];

        /// <summary><c>odyssey.pawn.kit.&lt;slot&gt;.count</c>: how many.</summary>
        public static AspectKey CountKey(int slot) => CountKeys[slot];

        /// <summary><c>odyssey.pawn.kit.&lt;slot&gt;.use</c>: a <see cref="KitUseHandle"/>, absent for no use.</summary>
        public static AspectKey UseKey(int slot) => UseKeys[slot];

        /// <summary>How many of <paramref name="itemDef"/> one kit slot holds, or 0 for a thing that never goes in a kit.</summary>
        public static int Cap(int itemDef) => itemDef switch
        {
            ItemHandle.MedicalSupplies => 5,
            ItemHandle.Meal => 3,
            _ => 0,
        };

        /// <summary>Does anything of this kind go in a kit.</summary>
        public static bool Fits(int itemDef) => Cap(itemDef) > 0;

        /// <summary>
        /// How many of <paramref name="itemDef"/> one take would lift for <paramref name="pawn"/>, as
        /// the simulation's <c>Kit.TakeRoom</c> answers it: the room in the slots of that kind, or
        /// one empty slot's cap when those are full; nought for no room.
        /// </summary>
        public static int TakeRoom(WorldSnapshot snapshot, PawnId pawn, int itemDef)
        {
            int cap = Cap(itemDef), topUp = 0;
            if (cap <= 0) return 0;
            bool empty = false;
            for (int slot = 0; slot < Slots; slot++)
            {
                if (!snapshot.TryGetPawnAspect(pawn, DefKeys[slot], out int def))
                {
                    empty = true;
                    continue;
                }
                if (def != itemDef) continue;
                snapshot.TryGetPawnAspect(pawn, CountKeys[slot], out int count);
                if (count < cap) topUp += cap - count;
            }
            return topUp > 0 ? topUp : empty ? cap : 0;
        }

        /// <summary><c>OrderTakeIntoKit</c>: walk to <paramref name="thing"/> and take one slot's worth.</summary>
        public static Intent Take(PawnId colonist, in ThingView thing) =>
            new Intent(IntentKind.OrderTakeIntoKit, thing.Cell, colonist.Value, thing.Id.Value);

        /// <summary>
        /// <c>OrderKitDrop</c>: lay a slot's stack at her feet — <b>Remove</b>, left for the haulers,
        /// or <b>Drop</b> (<paramref name="leaveHere"/>), forbidden where it lies.
        /// </summary>
        public static Intent Drop(PawnId colonist, int slot, bool leaveHere) =>
            new Intent(IntentKind.OrderKitDrop, default, colonist.Value, slot, leaveHere ? 1 : 0);

        /// <summary><c>OrderUseKit</c>: use what is in the slot now.</summary>
        public static Intent Use(PawnId colonist, int slot) =>
            new Intent(IntentKind.OrderUseKit, default, colonist.Value, slot);

        /// <summary>Why Use is greyed, from the registry, or empty when it is not.</summary>
        public static string UseReason(int use) => use switch
        {
            KitUseHandle.NotHurt => Registry.Label(NotHurtKey),
            KitUseHandle.NotHungry => Registry.Label(NotHungryKey),
            _ => string.Empty,
        };

        /// <summary>The right-click row's words: "Take into kit · medical supplies".</summary>
        public static string TakeLabel(int itemDef) =>
            Registry.Label(TakeKey) + " · " + ItemLabels.Label(itemDef).ToLowerInvariant();
    }
}
