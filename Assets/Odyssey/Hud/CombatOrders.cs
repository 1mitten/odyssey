#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The fight's half of a right-click (design 33 §1, §2f, §5j): which clicks are orders about a
    /// pawn or a thing rather than moves to a cell. <b>Lane C's file</b>
    /// (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>The rules, the owner's (design 33 §1), as the seam review fixed them (§5j):</b></para>
    /// <list type="bullet">
    /// <item>an <b>animal</b> or a <b>hostile</b> under the pointer: every selected drafted
    /// colonist attacks it (<see cref="IntentKind.OrderAttack"/>);</item>
    /// <item><b>Ctrl</b> and a colonist under the pointer: every selected drafted colonist but that
    /// one attacks it — Ctrl wins over the rescue below, being the one gesture that says "hit this
    /// one of ours" in so many words;</item>
    /// <item>a <b>downed colonist</b>: the <b>nearest</b> selected drafted colonist rescues it
    /// (<see cref="IntentKind.OrderRescue"/>) — one body, one carrier, so the others are not sent
    /// on a walk the reservation would refuse at the end of;</item>
    /// <item>a <b>weapon</b> lying in the clicked cell: the <b>primary</b> colonist — the first
    /// colonist in the selection — fetches it, <b>drafted or not</b>
    /// (<see cref="IntentKind.OrderEquip"/>). A fetch, not a fight, and one weapon fills one
    /// hand.</item>
    /// </list>
    /// <para><b>Anything else falls through</b> to <see cref="OrderModel.RightClick"/>'s move, and
    /// that includes a click on a building: routing one to an attack is C6's, and then only for an
    /// edifice that occupies the cell, never a floor or a slab. A pawn under the pointer wins over
    /// the cell it stands in, because the pawn is what the player pointed at; a colonist under the
    /// pointer with no Ctrl claims nothing, so the click is the move it was in C1.</para>
    ///
    /// <para><b>What it scales with:</b> once per right-click, never per frame or per tick —
    /// the selection, a <see cref="WorldSnapshot.TryGetPawn"/> scan per selected pawn, and one walk
    /// of <see cref="WorldSnapshot.Things"/> when no pawn claims the click.</para>
    /// </summary>
    public static class CombatOrders
    {
        /// <summary>
        /// Add the intents for a right-click that is a fight order and answer true, or add nothing
        /// and answer false so the caller moves. The arguments are <see cref="OrderModel.RightClick"/>'s.
        ///
        /// <para>A click about a pawn that sends nothing — an undrafted selection pointing at a
        /// hog — answers false and falls through, and the move sends nothing either, because a
        /// move is for the drafted too. So "no draft, no fight" costs no rule of its own here.</para>
        /// </summary>
        public static bool Route(IReadOnlyList<PawnId> selection, WorldSnapshot snapshot,
            CellRef? cell, PawnId under, bool ctrl, List<Intent> into)
        {
            if (under.IsValid && snapshot.TryGetPawn(under, out PawnView target))
            {
                if (target.IsAnimal || target.IsHostile || (ctrl && target.IsColonist))
                    return Attack(selection, snapshot, target, into);

                if (target.IsColonist && target.IsDowned)
                    return Rescue(selection, snapshot, target, into);
            }

            if (cell.HasValue && WeaponAt(snapshot, cell.Value, out ThingView weapon))
                return Equip(selection, snapshot, weapon, into);

            return false;
        }

        /// <summary>
        /// Is this item def a weapon — one of the four melee weapons of design 33 §1? Parallel to
        /// the item table's <c>Weapons</c> category, which this assembly cannot read: the four are
        /// appended together in <see cref="ItemHandle"/> and a test walks the whole table.
        /// </summary>
        public static bool IsWeapon(int itemDef) =>
            itemDef == ItemHandle.Bat || itemDef == ItemHandle.Crowbar
            || itemDef == ItemHandle.Machete || itemDef == ItemHandle.ArcBlade;

        /// <summary>A selected colonist the player may give a fight order to: drafted and standing.</summary>
        static bool CanFight(WorldSnapshot snapshot, PawnId pawn, out PawnView view) =>
            snapshot.TryGetPawn(pawn, out view) && view.IsColonist && !view.IsDowned
            && OrderModel.IsDrafted(snapshot, pawn);

        static bool Attack(IReadOnlyList<PawnId> selection, WorldSnapshot snapshot, in PawnView target,
            List<Intent> into)
        {
            bool any = false;
            for (int i = 0; i < selection.Count; i++)
            {
                PawnId pawn = selection[i];
                if (pawn == target.Id || !CanFight(snapshot, pawn, out _)) continue;
                into.Add(new Intent(IntentKind.OrderAttack, target.Cell, pawn.Value, target.Id.Value));
                any = true;
            }
            return any;
        }

        static bool Rescue(IReadOnlyList<PawnId> selection, WorldSnapshot snapshot, in PawnView patient,
            List<Intent> into)
        {
            // The nearest by straight-line cells, the first in the selection on a tie, so the answer
            // is the same every time for the same frame.
            PawnId best = PawnId.None;
            long bestDistance = long.MaxValue;
            for (int i = 0; i < selection.Count; i++)
            {
                PawnId pawn = selection[i];
                if (pawn == patient.Id || !CanFight(snapshot, pawn, out PawnView rescuer)) continue;
                long dx = rescuer.Cell.X - patient.Cell.X;
                long dy = rescuer.Cell.Y - patient.Cell.Y;
                long dz = rescuer.Cell.Z - patient.Cell.Z;
                long distance = dx * dx + dy * dy + dz * dz;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = pawn;
            }
            if (!best.IsValid) return false;
            into.Add(new Intent(IntentKind.OrderRescue, patient.Cell, best.Value, patient.Id.Value));
            return true;
        }

        static bool Equip(IReadOnlyList<PawnId> selection, WorldSnapshot snapshot, in ThingView weapon,
            List<Intent> into)
        {
            // The primary colonist: the first colonist in the selection. An animal or a marauder
            // ahead of her in a stale selection is passed over; a downed one would be refused.
            for (int i = 0; i < selection.Count; i++)
            {
                if (!snapshot.TryGetPawn(selection[i], out PawnView view) || !view.IsColonist || view.IsDowned)
                    continue;
                into.Add(new Intent(IntentKind.OrderEquip, weapon.Cell, view.Id.Value, weapon.Id.Value));
                return true;
            }
            return false;
        }

        /// <summary>
        /// A weapon lying loose in the cell, or in the one above it — the pick names the block a
        /// thing lies on as often as the air it lies in, which is the rule
        /// <c>SelectionDirector.ThingAt</c> already selects a pile by. A weapon on a shelf is the
        /// shelf's, as every contained thing is.
        /// </summary>
        static bool WeaponAt(WorldSnapshot snapshot, CellRef cell, out ThingView weapon)
        {
            if (WeaponIn(snapshot, cell, out weapon)) return true;
            return cell.Y + 1 < snapshot.Size.SizeY && WeaponIn(snapshot, cell.Above, out weapon);
        }

        static bool WeaponIn(WorldSnapshot snapshot, CellRef cell, out ThingView weapon)
        {
            var things = snapshot.Things;
            for (int i = 0; i < things.Length; i++)
            {
                if (things[i].Contained || things[i].Cell != cell || !IsWeapon(things[i].DefIndex)) continue;
                weapon = things[i];
                return true;
            }
            weapon = default;
            return false;
        }
    }
}
