#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// What a player's hand means for the colonists they have selected (design 33 §2f): the draft
    /// key, and a right-click on the world with no tool armed.
    ///
    /// <para><b>Unity-free, so the fast tier owns the rule.</b> The presenter that hears the click
    /// and the key cannot be tested — the PlayMode harness still cannot press a button — so the
    /// whole decision lives here and the presenter only carries its answer to the world. It
    /// returns intents rather than sending them for the same reason.</para>
    ///
    /// <para><b>A selection is a set.</b> Box and shift selection were there before combat, so the
    /// draft key drafts every selected colonist and a right-click moves every selected drafted
    /// one; the simulation spreads colonists sent to one cell (design 33 §2d).</para>
    /// </summary>
    public static class OrderModel
    {
        /// <summary>
        /// The names the simulation publishes the draft under. String literals and not a shared
        /// constant, on the bargain <see cref="JobLabels.CarryingAspect"/> makes: this assembly
        /// cannot reference <c>Odyssey.Sim</c>, and tests on both sides hold the spellings together.
        /// </summary>
        public const string DraftedAspect = "odyssey.pawn.drafted";

        /// <summary>The cell index a drafted colonist is walking to. See <see cref="DraftedAspect"/>.</summary>
        public const string OrderCellAspect = "odyssey.pawn.order.cell";

        static readonly AspectKey DraftedKey = AspectKey.Of(DraftedAspect);
        static readonly AspectKey OrderCellKey = AspectKey.Of(OrderCellAspect);

        /// <summary>
        /// Is this pawn a colonist — the only thing the draft applies to? Asked of the view's
        /// flags (design 33 §5), not of its kind: a marauder is kind 3 and a person, and "kind 0"
        /// would have been right for the wrong reason until the first hostile arrived.
        /// </summary>
        public static bool IsColonist(WorldSnapshot snapshot, PawnId pawn) =>
            snapshot.TryGetPawn(pawn, out PawnView view) && view.IsColonist;

        public static bool IsDrafted(WorldSnapshot snapshot, PawnId pawn) =>
            snapshot.TryGetPawnAspect(pawn, DraftedKey, out int drafted) && drafted != 0;

        /// <summary>The cell index the colonist is walking to under orders, or -1.</summary>
        public static int OrderCell(WorldSnapshot snapshot, PawnId pawn) =>
            snapshot.TryGetPawnAspect(pawn, OrderCellKey, out int cell) ? cell : -1;

        /// <summary>
        /// The draft key, or the pane's button: <b>if any selected colonist is undrafted, draft the
        /// undrafted ones; otherwise release them all.</b> The reference's rule for a mixed
        /// selection, and the one that makes the key safe to press twice. Animals in the selection
        /// are passed over. Adds nothing when there is no colonist to act on.
        /// </summary>
        public static void ToggleDraft(IReadOnlyList<PawnId> selection, WorldSnapshot snapshot, List<Intent> into)
        {
            bool anyUndrafted = false;
            for (int i = 0; i < selection.Count; i++)
                if (IsColonist(snapshot, selection[i]) && !IsDrafted(snapshot, selection[i])) anyUndrafted = true;

            for (int i = 0; i < selection.Count; i++)
            {
                PawnId pawn = selection[i];
                if (!IsColonist(snapshot, pawn)) continue;
                if (anyUndrafted == IsDrafted(snapshot, pawn)) continue;
                into.Add(new Intent(IntentKind.SetDrafted, default, pawn.Value, anyUndrafted ? 1 : 0));
            }
        }

        /// <summary>
        /// A right-click on the world with no tool in hand. <paramref name="cell"/> is the cell the
        /// pick resolved to, or null for sky; <paramref name="under"/> is the pawn under the
        /// pointer, if any. In C1 every click on the world is a move to the cell it resolved to,
        /// <b>a pawn under the pointer included</b>: a colonist's hit box is 1.15 by 2.7 metres,
        /// which at the play camera covers most of the cell behind her, so ignoring a click that
        /// touched a pawn made every order just behind your own squad do nothing (review,
        /// 2026-09-23). The attack and rescue orders will claim the pawns they are about — an
        /// animal, a hostile, a downed colonist, a colonist under Ctrl — and leave the rest as
        /// moves.
        /// </summary>
        public static void RightClick(IReadOnlyList<PawnId> selection, WorldSnapshot snapshot,
            CellRef? cell, PawnId under, bool ctrl, List<Intent> into)
        {
            // The fight's orders claim the clicks they are about first (design 33 §5): attack,
            // rescue, equip. CombatOrders is lane C's file and answers no until lane C writes it,
            // so until then every click is the move it was in C1.
            if (CombatOrders.Route(selection, snapshot, cell, under, ctrl, into)) return;

            if (cell == null) return;

            for (int i = 0; i < selection.Count; i++)
            {
                PawnId pawn = selection[i];
                if (!IsColonist(snapshot, pawn) || !IsDrafted(snapshot, pawn)) continue;
                into.Add(new Intent(IntentKind.OrderMove, cell.Value, pawn.Value));
            }
        }

        /// <summary>One drafted colonist as the board draws it: who, and where it is walking to, or -1.</summary>
        public readonly struct DraftedMark
        {
            public readonly PawnId Pawn;
            public readonly int OrderCell;

            public DraftedMark(PawnId pawn, int orderCell)
            {
                Pawn = pawn;
                OrderCell = orderCell;
            }
        }

        /// <summary>
        /// Every drafted colonist in the frame, with its order cell, in one walk of the published
        /// aspects (design 33 §2g). The board draws a marker a frame for each, and asking
        /// <see cref="IsDrafted"/> per colonist would be a scan of every aspect per colonist, every
        /// frame. The simulation publishes a colonist's order cell straight after its drafted row,
        /// which is what lets the second attach to the first without a search.
        /// </summary>
        public static void CollectDrafted(WorldSnapshot snapshot, List<DraftedMark> into)
        {
            into.Clear();
            ReadOnlySpan<PawnAspect> aspects = snapshot.PawnAspects;
            for (int i = 0; i < aspects.Length; i++)
            {
                PawnAspect aspect = aspects[i];
                if (aspect.Key == DraftedKey)
                {
                    if (aspect.Value != 0) into.Add(new DraftedMark(aspect.Pawn, -1));
                }
                else if (aspect.Key == OrderCellKey && into.Count > 0 && into[into.Count - 1].Pawn == aspect.Pawn)
                {
                    into[into.Count - 1] = new DraftedMark(aspect.Pawn, aspect.Value);
                }
            }
        }

        /// <summary>Does anything in the selection take orders — is a right-click worth hearing?</summary>
        public static bool AnyDrafted(IReadOnlyList<PawnId> selection, WorldSnapshot snapshot)
        {
            for (int i = 0; i < selection.Count; i++)
                if (IsColonist(snapshot, selection[i]) && IsDrafted(snapshot, selection[i])) return true;
            return false;
        }
    }
}
