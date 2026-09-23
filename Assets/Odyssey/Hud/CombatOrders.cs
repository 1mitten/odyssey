#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The fight's half of a right-click (design 33 §1, §2f): which clicks are orders about a pawn
    /// or a thing rather than moves to a cell. <b>Lane C's file</b>
    /// (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para>The rules to write here, the owner's (design 33 §1): a right-click on an animal, a
    /// hostile or a building <b>attacks</b> (<see cref="IntentKind.OrderAttack"/>);
    /// <b>Ctrl</b>+right-click on a colonist attacks it; a right-click on a downed colonist
    /// <b>rescues</b> (<see cref="IntentKind.OrderRescue"/>); a right-click on a weapon
    /// <b>equips</b> (<see cref="IntentKind.OrderEquip"/>). Anything it does not claim falls
    /// through to <see cref="OrderModel.RightClick"/>'s move, which is why it answers a bool.</para>
    ///
    /// <para><b>A stub from the contracts step: it claims nothing</b>, so every right-click is the
    /// move it was in C1 until lane C writes the rules.</para>
    /// </summary>
    public static class CombatOrders
    {
        /// <summary>
        /// Add the intents for a right-click that is a fight order and answer true, or add nothing
        /// and answer false so the caller moves. The arguments are <see cref="OrderModel.RightClick"/>'s.
        /// </summary>
        public static bool Route(IReadOnlyList<PawnId> selection, WorldSnapshot snapshot,
            CellRef? cell, PawnId under, bool ctrl, List<Intent> into) => false;
    }
}
