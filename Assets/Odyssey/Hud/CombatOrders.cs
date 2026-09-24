#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The fight's half of a right-click (design 33 §1, §2f, §5j): which clicks are orders about a
    /// pawn rather than moves to a cell. <b>Lane C's file</b>
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
    /// </list>
    /// <para><b>A weapon is not here any more</b> (design 33 §7a, owner 2026-09-23: picking one up
    /// "wasn't clear"). A right-click on one opens the context menu —
    /// <see cref="ContextMenuModel"/>, asked by <see cref="OrderModel.RightClick"/> after this —
    /// whose <i>Equip</i> row sends the same <see cref="IntentKind.OrderEquip"/> for the same
    /// primary colonist, drafted or not.</para>
    /// <para><b>Anything else falls through</b> to the context menu, then to the building half
    /// (<see cref="RouteBuilding"/>, C6: an edifice that occupies the cell and has hit points,
    /// never a floor or a slab), and then to <see cref="OrderModel.RightClick"/>'s move. A pawn under the pointer wins over
    /// the cell it stands in, because the pawn is what the player pointed at; a colonist under the
    /// pointer with no Ctrl claims nothing, so the click is the move it was in C1.</para>
    ///
    /// <para><b>What it scales with:</b> once per right-click, never per frame or per tick —
    /// the selection and a <see cref="WorldSnapshot.TryGetPawn"/> scan per selected pawn.</para>
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

            return false;
        }

        /// <summary>
        /// The building half of a right-click (C6, design 33 §5j, §13i): with <b>no pawn under the
        /// pointer</b>, a cell whose standing edifice has hit points is attacked by every selected
        /// drafted colonist — <see cref="IntentKind.OrderAttack"/> with <c>B = 0</c> and the cell
        /// clicked. Asked by <see cref="OrderModel.RightClick"/> <b>after</b> <see cref="Route"/>
        /// and the context menu, so a pawn on a bed is the pawn, and a weapon on a shelf opens the
        /// menu to be equipped rather than the shelf being smashed; and before the move.
        ///
        /// <para><paramref name="edifice"/> is what stands in the clicked cell, an
        /// <see cref="EdificeHandle"/> value the presenter reads off its render mirror, as it reads
        /// the pawn under the pointer — this assembly cannot see the grid. <b>Which edifices are
        /// targets is the simulation's</b>, published as
        /// <see cref="WorldSnapshot.EdificeHitPoints"/>: a wall, a door, a bed, a shelf, a ladder,
        /// a campfire, a generator, a heater, and never a tree. A floor is not an edifice at all,
        /// so the presenter reports none and the click moves.</para>
        /// </summary>
        public static bool RouteBuilding(IReadOnlyList<PawnId> selection, WorldSnapshot snapshot,
            CellRef? cell, PawnId under, int edifice, List<Intent> into)
        {
            if (cell == null || under.IsValid || edifice == EdificeHandle.None) return false;
            if (snapshot.EdificeHitPoints(edifice) <= 0) return false;

            bool any = false;
            for (int i = 0; i < selection.Count; i++)
            {
                PawnId pawn = selection[i];
                if (!CanFight(snapshot, pawn, out _)) continue;
                into.Add(new Intent(IntentKind.OrderAttack, cell.Value, pawn.Value, 0));
                any = true;
            }
            return any;
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
    }
}
