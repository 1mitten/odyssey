#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// One line of the context menu (design 33 §7a): what it says, whether it can be chosen, why
    /// not, and the orders it sends. A value the model builds once per right-click; the view draws
    /// it and hands it back through <see cref="ContextMenuModel.Choose"/>.
    /// </summary>
    public sealed class ContextMenuRow
    {
        /// <summary>The registry key of the verb — <c>ui.command.equip</c>, <c>ui.menu.cancel</c>.</summary>
        public readonly string Key;

        /// <summary>The whole line as the player reads it: "Equip machete", "Cancel".</summary>
        public readonly string Label;

        /// <summary>Can the row be chosen? A disabled row is drawn dim, with its reason beside it.</summary>
        public readonly bool Enabled;

        /// <summary>Why the row cannot be chosen, in the registry's words; empty while it can.</summary>
        public readonly string Reason;

        /// <summary>What choosing the row sends. Empty for Cancel and for every disabled row.</summary>
        public readonly IReadOnlyList<Intent> Intents;

        public ContextMenuRow(string key, string label, bool enabled, string reason, IReadOnlyList<Intent> intents)
        {
            Key = key;
            Label = label;
            Enabled = enabled;
            Reason = reason;
            Intents = intents;
        }

        /// <summary>Is this the row that closes the menu and orders nothing?</summary>
        public bool IsCancel => Key == ContextMenuModel.CancelKey;
    }

    /// <summary>
    /// The forced-order context menu (design 33 §7a; reserved by <c>15-building.md</c> §8): a
    /// right-click on a <b>thing</b> that has more than one sensible answer opens a small menu at
    /// the pointer instead of acting at once.
    ///
    /// <para><b>What opens it and what does not</b> (owner, 2026-09-23). A weapon lying on the
    /// ground or in a store opens it, with <i>Equip &lt;weapon&gt;</i> and <i>Cancel</i>. Bare
    /// ground stays an instant move for the drafted, and an enemy stays an instant attack: an enemy
    /// has one sensible order, so it takes one click, and the menu is for things with several.
    /// The order of questions is <see cref="OrderModel.RightClick"/>'s: the pawn under the pointer
    /// first (<see cref="CombatOrders.Route"/>), then this, then the move.</para>
    ///
    /// <para><b>Built to grow a row at a time.</b> Each target is one <c>Offer…</c> method that
    /// appends its rows; Cancel is added last by <see cref="Build"/> whenever anything was offered.
    /// <i>Rescue</i> on a downed colonist and <i>Build this now</i> on a site are each one more
    /// method here, one more key in <c>icon-keys.csv</c>, and nothing in the view. The rescue is
    /// <b>not</b> moved in yet: today it is an instant order for the nearest drafted colonist
    /// (<see cref="CombatOrders.Route"/>), the owner has played it that way, and folding it into a
    /// menu turns one click into two — a change to decide on at the keyboard, not by tidying.</para>
    ///
    /// <para><b>Unity-free</b>, so the fast tier owns the rule: which rows, which are enabled, and
    /// exactly what each sends. The presenter only supplies the pawn under the pointer and draws
    /// the answer. <b>Scales with</b> nothing per frame: one walk of
    /// <see cref="WorldSnapshot.Things"/> per right-click.</para>
    /// </summary>
    public static class ContextMenuModel
    {
        /// <summary>The verb on the weapon's row. The registry's own command, "Equip".</summary>
        public const string EquipKey = "ui.command.equip";

        /// <summary>The row that closes the menu and sends nothing.</summary>
        public const string CancelKey = "ui.menu.cancel";

        /// <summary>Why a selection of downed colonists cannot take a weapon up: "Downed".</summary>
        public const string DownedReasonKey = "ui.status.downed";

        /// <summary>
        /// The most weapon rows one menu offers — a shelf full of bats is one row per <i>kind</i>
        /// already, and there are four kinds; this is the guard against a longer list arriving
        /// with the next weapon rather than a limit anybody should meet.
        /// </summary>
        public const int MaxEquipRows = 6;

        static readonly Intent[] Nothing = Array.Empty<Intent>();

        /// <summary>
        /// The rows a right-click on this target offers, Cancel last, into <paramref name="into"/>
        /// (cleared first). Answers false, with the list empty, when the click is not a menu's —
        /// bare ground, a pile of wood, a building — so the caller moves.
        ///
        /// <para><paramref name="under"/> is the pawn under the pointer, which no row reads yet:
        /// it is the target <i>Rescue</i> will be offered on.</para>
        /// </summary>
        public static bool Build(IReadOnlyList<PawnId> selection, WorldSnapshot snapshot, CellRef? cell,
            PawnId under, List<ContextMenuRow> into)
        {
            into.Clear();
            if (cell.HasValue) OfferEquip(selection, snapshot, cell.Value, into);

            if (into.Count == 0) return false;
            into.Add(new ContextMenuRow(CancelKey, Registry.Label(CancelKey), enabled: true, string.Empty, Nothing));
            return true;
        }

        /// <summary>
        /// The row the player chose: its orders into <paramref name="into"/>, or nothing for Cancel
        /// or a disabled row. The one door from a click on a row to the world, so the view cannot
        /// send a disabled row's orders by forgetting to look.
        /// </summary>
        public static void Choose(ContextMenuRow row, List<Intent> into)
        {
            if (!row.Enabled) return;
            for (int i = 0; i < row.Intents.Count; i++) into.Add(row.Intents[i]);
        }

        /// <summary>
        /// Equip, one row per kind of weapon at the click: lying loose in the cell or the one above
        /// it (the pick names the block a thing lies on as often as the air it lies in — the rule a
        /// left click selects a pile by), or held in a store there. The primary colonist — the
        /// first standing colonist in the selection, drafted or not (design 33 §5j) — is the one
        /// sent. A selection whose every colonist is down gets the row disabled, reason "Downed";
        /// a selection with no colonist at all gets no row, so no menu: an animal or a marauder in
        /// a stale selection takes no orders, and a menu of one Cancel is a menu that says nothing.
        /// </summary>
        static void OfferEquip(IReadOnlyList<PawnId> selection, WorldSnapshot snapshot, CellRef cell,
            List<ContextMenuRow> into)
        {
            bool anyColonist = false;
            PawnId equipper = PawnId.None;
            for (int i = 0; i < selection.Count && !equipper.IsValid; i++)
            {
                if (!snapshot.TryGetPawn(selection[i], out PawnView view) || !view.IsColonist) continue;
                anyColonist = true;
                if (!view.IsDowned) equipper = view.Id;
            }
            if (!anyColonist) return;

            int first = into.Count;
            CollectWeapons(snapshot, cell, selection, equipper, first, into);
            if (cell.Y + 1 < snapshot.Size.SizeY)
                CollectWeapons(snapshot, cell.Above, selection, equipper, first, into);
        }

        static void CollectWeapons(WorldSnapshot snapshot, CellRef cell, IReadOnlyList<PawnId> selection,
            PawnId equipper, int first, List<ContextMenuRow> into)
        {
            ReadOnlySpan<ThingView> things = snapshot.Things;
            for (int i = 0; i < things.Length; i++)
            {
                ThingView thing = things[i];
                if (thing.Cell != cell || !CombatOrders.IsWeapon(thing.DefIndex)) continue;
                if (into.Count - first >= MaxEquipRows) return;
                if (Offered(into, first, thing.DefIndex)) continue;
                into.Add(EquipRow(thing, equipper));
            }
        }

        /// <summary>Is a row for this kind of weapon already in the menu? One row per kind.</summary>
        static bool Offered(List<ContextMenuRow> into, int first, int def)
        {
            string label = EquipLabel(def);
            for (int i = first; i < into.Count; i++)
                if (into[i].Label == label) return true;
            return false;
        }

        static ContextMenuRow EquipRow(in ThingView weapon, PawnId equipper)
        {
            string label = EquipLabel(weapon.DefIndex);
            if (!equipper.IsValid)
                return new ContextMenuRow(EquipKey, label, enabled: false, Registry.Label(DownedReasonKey), Nothing);

            var order = new[] { new Intent(IntentKind.OrderEquip, weapon.Cell, equipper.Value, weapon.Id.Value) };
            return new ContextMenuRow(EquipKey, label, enabled: true, string.Empty, order);
        }

        /// <summary>
        /// "Equip machete", "Equip arc blade": the verb and the weapon's name lower-cased, the way
        /// the pane writes "Corpse of a midden hog" — both words from the registry, neither here.
        /// </summary>
        public static string EquipLabel(int weaponDef) =>
            Registry.Label(EquipKey) + " " + ItemLabels.Label(weaponDef).ToLowerInvariant();
    }
}
