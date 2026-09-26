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
            OfferCapture(selection, snapshot, under, into);
            OfferTend(selection, snapshot, under, into);
            OfferArrest(selection, snapshot, under, into);

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
        /// a selection with no colonist at all gets no row, so no menu: an animal or a bandit in
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

        /// <summary>The Tend row's verb (design 43 §11): "Tend", "Treat this patient".</summary>
        public const string TendKey = "ui.command.tend";

        /// <summary>The two rows on a downed enemy (design 59 §7): bring her in, or kill her.</summary>
        public const string CaptureKey = "ui.command.capture", FinishOffKey = "ui.command.finishoff";

        /// <summary>Why Finish off is dim: nobody selected is drafted, and only the drafted fight.</summary>
        public const string NeedsDraftKey = "ui.menu.needsdraft";

        /// <summary>
        /// Capture, then Finish off, on a downed person who is not ours (design 59 §7, the owner's
        /// ruling of 2026-09-26: "Menu: Capture / Finish off"). The right-click that used to kill a
        /// downed bandit outright opens this instead, so nothing happens by accident.
        ///
        /// <para>Capture sends the primary colonist — the first standing one, drafted or not — with
        /// <see cref="IntentKind.OrderCapture"/>, which marks the target first, so an order refused
        /// for want of a free prison bed still leaves a warden to bring her in once there is one.
        /// Finish off is the old attack, from every drafted colonist selected; dim, with its reason,
        /// when nobody selected is drafted. A downed prisoner lying outside her bed is offered
        /// Capture alone — she is brought back, never finished off from a menu.</para>
        /// </summary>
        static void OfferCapture(IReadOnlyList<PawnId> selection, WorldSnapshot snapshot, PawnId under,
            List<ContextMenuRow> into)
        {
            if (!under.IsValid || !snapshot.TryGetPawn(under, out PawnView target)) return;
            if (!target.IsPerson || !target.IsDowned || target.IsColonist) return;
            if (!target.IsHostile && target.Custody != PawnCustody.Prisoner) return;
            // A prisoner only when she is to be brought back (design 59 §16 H3): one lying in her
            // own prison bed was offered a Capture the simulation refused in silence, and a
            // drafted right-click on her cell opened the menu instead of moving there.
            if (target.Custody == PawnCustody.Prisoner && !snapshot.TryGetPawnAspect(under, PrisonAspectNames.StrayKey, out _))
                return;

            bool anyColonist = false;
            PawnId primary = PawnId.None;
            for (int i = 0; i < selection.Count && !primary.IsValid; i++)
            {
                if (!snapshot.TryGetPawn(selection[i], out PawnView view) || !view.IsColonist) continue;
                anyColonist = true;
                if (!view.IsDowned) primary = view.Id;
            }
            if (!anyColonist) return;

            string capture = Registry.Label(CaptureKey);
            if (!primary.IsValid)
                into.Add(new ContextMenuRow(CaptureKey, capture, enabled: false, Registry.Label(DownedReasonKey), Nothing));
            else
                into.Add(new ContextMenuRow(CaptureKey, capture, enabled: true, string.Empty,
                    new[] { new Intent(IntentKind.OrderCapture, target.Cell, primary.Value, under.Value) }));

            if (!target.IsHostile) return;
            var attack = new List<Intent>();
            bool any = CombatOrders.Attack(selection, snapshot, target, attack);
            string finish = Registry.Label(FinishOffKey);
            into.Add(any
                ? new ContextMenuRow(FinishOffKey, finish, enabled: true, string.Empty, attack.ToArray())
                : new ContextMenuRow(FinishOffKey, finish, enabled: false, Registry.Label(NeedsDraftKey), Nothing));
        }

        /// <summary>
        /// Tend, on a colonist under the pointer who has an injury nobody has tended (design 43
        /// §11), read off the body's sparse aspects. The primary colonist of the selection — the
        /// first standing one, drafted or not — is sent; a selection whose every colonist is down
        /// gets the row disabled, reason "Downed"; a patient in the selection is not sent to
        /// herself. No colonist selected, no row.
        /// </summary>
        static void OfferTend(IReadOnlyList<PawnId> selection, WorldSnapshot snapshot, PawnId under,
            List<ContextMenuRow> into)
        {
            if (!under.IsValid || !snapshot.TryGetPawn(under, out PawnView patient) || !patient.IsColonist) return;
            if (!snapshot.TryGetPawnAspect(under, HealthAspectNames.InjuriesKey, out int injuries) || injuries <= 0) return;
            snapshot.TryGetPawnAspect(under, HealthAspectNames.TendedKey, out int tended);
            if (tended >= injuries) return;

            bool anyColonist = false;
            PawnId doctor = PawnId.None;
            for (int i = 0; i < selection.Count && !doctor.IsValid; i++)
            {
                if (selection[i] == under) continue;
                if (!snapshot.TryGetPawn(selection[i], out PawnView view) || !view.IsColonist) continue;
                anyColonist = true;
                if (!view.IsDowned) doctor = view.Id;
            }
            if (!anyColonist) return;

            string label = Registry.Label(TendKey);
            if (!doctor.IsValid)
            {
                into.Add(new ContextMenuRow(TendKey, label, enabled: false, Registry.Label(DownedReasonKey), Nothing));
                return;
            }
            var order = new[] { new Intent(IntentKind.OrderTend, patient.Cell, doctor.Value, under.Value) };
            into.Add(new ContextMenuRow(TendKey, label, enabled: true, string.Empty, order));
        }

        /// <summary>The verb on a colonist's Arrest row (design 59 §10).</summary>
        public const string ArrestKey = "ui.command.arrest";

        /// <summary>
        /// <b>Arrest</b>, on a colonist under the pointer who is on her feet (design 59 §10, owner's
        /// ruling 2026-09-26 at the second review): a row here rather than a button in her pane's
        /// header, which with Draft, the response and First Person left 17 px for her name
        /// (design 59 §16 H1). <b>Only while nobody selected is drafted</b>, so a drafted
        /// right-click that touches a colonist is still a move (design 33 §2f). The first standing
        /// colonist of the selection other than her is sent; with none, the nearest who can reach
        /// her (<c>A = 0</c>). Dim with its reason when the simulation would refuse it
        /// (<see cref="ArrestRefusal"/>). No colonist selected, no row.
        /// </summary>
        static void OfferArrest(IReadOnlyList<PawnId> selection, WorldSnapshot snapshot, PawnId under,
            List<ContextMenuRow> into)
        {
            if (!under.IsValid || !snapshot.TryGetPawn(under, out PawnView target) || !target.IsColonist || target.IsDowned)
                return;

            bool anyColonist = false;
            PawnId arrester = PawnId.None;
            for (int i = 0; i < selection.Count; i++)
            {
                if (!snapshot.TryGetPawn(selection[i], out PawnView view) || !view.IsColonist) continue;
                if (OrderModel.IsDrafted(snapshot, view.Id)) return;
                anyColonist = true;
                if (!arrester.IsValid && view.Id != under && !view.IsDowned) arrester = view.Id;
            }
            if (!anyColonist) return;

            string label = Registry.Label(ArrestKey);
            string? refusal = ArrestRefusal(snapshot, under);
            if (refusal != null)
            {
                into.Add(new ContextMenuRow(ArrestKey, label, enabled: false, refusal, Nothing));
                return;
            }
            var order = new[] { new Intent(IntentKind.OrderArrest, target.Cell, arrester.IsValid ? arrester.Value : 0, under.Value) };
            into.Add(new ContextMenuRow(ArrestKey, label, enabled: true, string.Empty, order));
        }

        /// <summary>
        /// Why an arrest of <paramref name="target"/> would be refused, in the menu's words, or null
        /// when it would be sent (design 59 §16 H2). Read off what the simulation publishes — her
        /// state, the colony's, whether a prison bed stands free — the three refusals a player can
        /// see coming. The simulation still decides; this only stops the press doing nothing.
        /// </summary>
        public static string? ArrestRefusal(WorldSnapshot snapshot, PawnId target)
        {
            if (!snapshot.TryGetPawn(target, out PawnView her) || her.IsDowned) return "she is down; she can only be captured";
            if (!snapshot.PrisonBedFree) return "no free prison bed";
            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
                if (pawns[i].Id != target && pawns[i].IsColonist && !pawns[i].IsDowned) return null;
            return "nobody else is on their feet to take her";
        }

        /// <summary>
        /// "Equip machete", "Equip arc blade": the verb and the weapon's name lower-cased, the way
        /// the pane writes "Corpse of a midden hog" — both words from the registry, neither here.
        /// </summary>
        public static string EquipLabel(int weaponDef) =>
            Registry.Label(EquipKey) + " " + ItemLabels.Label(weaponDef).ToLowerInvariant();
    }
}
