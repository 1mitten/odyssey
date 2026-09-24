#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>Why the selection changed, carried on <see cref="SelectionDirector.Changed"/>.</summary>
    public enum SelectionChange
    {
        /// <summary>A click in the world resolved to something, or to bare ground.</summary>
        Picked,

        /// <summary>A colonist was chosen outright, from the roster or a jump target.</summary>
        Chosen,

        /// <summary>Cleared on request.</summary>
        Cleared,

        /// <summary>The slice moved to another layer, which leaves the old selection out of view.</summary>
        LayerChanged,

        /// <summary>The subject stopped being published and its grace expired.</summary>
        Died,

        /// <summary>A drag box chose a set of colonists at once.</summary>
        Boxed,

        /// <summary>Shift toggled colonists in or out of an existing selection.</summary>
        Toggled,

        /// <summary>A double click asked for everything of the same kind on screen.</summary>
        Similar,
    }

    /// <summary>
    /// The single source of truth for what is selected, per <c>09-ui-and-input.md</c> §3 row 5.
    ///
    /// It holds handles, never references, and it decides the one rule every click obeys: a
    /// colonist wins over an item, because a colonist standing on a crate is what you meant to
    /// click, and the crate is still there when they walk off it. The geometry of a pick — which
    /// colonist a ray passes through — is the presenter's job in the Unity assembly; what that
    /// answer *means* is decided here, where it can be tested without a scene.
    ///
    /// The selection is multi-select of one class: an ordered set of colonists, the first of
    /// which is the primary that the inspect pane and the cursor show. Items and cells stay
    /// single-subject, because a box drawn over the ground means "these people", not "this
    /// dirt and that dirt".
    ///
    /// A subject that stops being published is dropped after a one-frame grace (§2.3): the frame
    /// in which a colonist dies still shows them, so the pane can tombstone rather than blank.
    /// </summary>
    public sealed class SelectionDirector
    {
        /// <summary>Frames a subject may be absent from the snapshot before the selection drops it.</summary>
        public const int GraceFrames = 1;

        readonly List<PawnId> _pawns = new List<PawnId>();

        int _missingFrames;

        /// <summary>The selected colonists in selection order. The first is the primary.</summary>
        public IReadOnlyList<PawnId> Pawns => _pawns;

        /// <summary>The primary colonist — the one the inspect pane shows and the cursor hugs.</summary>
        public PawnId Pawn => _pawns.Count > 0 ? _pawns[0] : PawnId.None;

        public ThingId Thing { get; private set; } = ThingId.None;

        /// <summary>The item def of <see cref="Thing"/>, for the presenter to draw it, or -1.</summary>
        public int ThingDef { get; private set; } = -1;

        /// <summary>The cell the last world pick landed on, or none. Never above the active layer.</summary>
        public CellRef? Cell { get; private set; }

        /// <summary>
        /// The selected corpse, as a <see cref="CorpseView.Id"/>, or 0 for none (design 33 §5f). A
        /// single subject, like <see cref="Thing"/>: set only by <see cref="ChooseCorpse"/>, and
        /// cleared by every other choice. What lane B's cursor brackets and lane C's pane shows.
        /// </summary>
        public int Corpse { get; private set; }

        public bool HasPawn => _pawns.Count > 0;
        public bool HasThing => Thing.IsValid;
        public bool HasCorpse => Corpse != 0;

        /// <summary>True when several colonists are selected at once, for panes that summarise.</summary>
        public bool HasMultiple => _pawns.Count > 1;

        public bool IsEmpty => !HasPawn && !HasThing && !Cell.HasValue && !HasCorpse;

        /// <summary>
        /// Raised after every change, inside the call that made it, so the pane and the cursor
        /// answer in the same frame. Carries the reason; listeners read the properties for the rest.
        /// </summary>
        public event Action<SelectionChange>? Changed;

        /// <summary>
        /// A world pick. The presenter has already hit-tested the ray against the colonists it can
        /// see and passes the winner, or none; the thing in the cell is resolved here from the
        /// frame. A pick on bare ground selects the ground: an inspect pane that goes blank on a
        /// miss reads as the click being ignored.
        /// </summary>
        /// <param name="additive">Shift held: a colonist pick toggles them in or out instead of
        /// replacing the selection. A pick that lands on anything else replaces, because there is
        /// nothing sensible to toggle about a cell.</param>
        public void Pick(CellRef? cell, PawnId pawnUnderPointer, WorldSnapshot snapshot, bool additive = false)
        {
            // One pick, one subject. A pick that lands on a colonist selects the colonist, and the
            // cell they happen to be standing in is not part of the selection at all.
            //
            // It used to set both. The cursor preferred the pawn and drew the right bracket most of
            // the time, but the selection was two things at once underneath: anything reading
            // Cell saw the cell under their feet, and if the pawn lookup missed for any reason the
            // cursor fell through to the cell tier and bracketed the ground — or the tree — while
            // the inspect pane went on showing the colonist. Reported from a playtest on
            // 2026-09-16 as a highlight landing near a colonist who was chopping, and selecting
            // them anyway. Choose() already cleared the cell for a roster click; this makes a
            // world click behave the same way, so the fall-through has nothing to fall to.
            if (pawnUnderPointer.IsValid)
            {
                if (additive) Toggle(pawnUnderPointer);
                else
                {
                    _pawns.Clear();
                    _pawns.Add(pawnUnderPointer);
                    ClearCellTier();
                    _missingFrames = 0;
                    Changed?.Invoke(SelectionChange.Picked);
                }
                return;
            }

            // **Clicking the same cell again looks past what is lying in it.** A thing wins the
            // first click, because a pile of wood is what the player pointed at; but a stockpile
            // is wall-to-wall things, and before this there was no way at all to reach the tile
            // under one (owner, 2026-09-19: "it seems to be difficult to click on a tile with
            // wood in — always the item takes precedence"). So the second click on the cell that
            // is already showing its thing shows the cell instead, and a third goes back to the
            // thing. Two rungs and a loop, which is as deep as a repeated click can go before
            // the player has to count.
            //
            // The cycle is not a counter. It is read off the selection itself — same cell, and
            // the thing it holds is the thing already selected — so anything that clears or
            // moves the selection starts it over without a flag to remember to reset, and a
            // thing that is hauled away while the cell is held cannot leave the cycle stranded.
            bool wasShowingThisCellsThing = HasThing && cell.HasValue && Cell.HasValue
                && Cell.Value.Equals(cell.Value);
            ThingId shown = Thing;

            _pawns.Clear();
            Cell = cell;
            Thing = ThingId.None;
            ThingDef = -1;
            Corpse = 0;
            if (Cell.HasValue)
            {
                ThingAt(snapshot, Cell.Value, out ThingId thing, out int def);
                if (!(wasShowingThisCellsThing && thing == shown))
                {
                    Thing = thing;
                    ThingDef = def;
                }
            }
            _missingFrames = 0;
            Changed?.Invoke(SelectionChange.Picked);
        }

        /// <summary>
        /// A set of colonists chosen by geometry rather than a ray: a drag box, a select-similar.
        /// Without shift the set replaces the selection; with it, the new colonists join the ones
        /// already held, because the point of shift is to widen a selection in pieces.
        /// </summary>
        public void PickMany(IReadOnlyList<PawnId> pawns, bool additive, SelectionChange reason)
        {
            if (!additive) _pawns.Clear();
            for (int i = 0; i < pawns.Count; i++)
                if (!_pawns.Contains(pawns[i])) _pawns.Add(pawns[i]);
            ClearCellTier();
            _missingFrames = 0;
            Changed?.Invoke(reason);
        }

        /// <summary>Select a colonist outright, without a pick: the roster, an alert, a jump.</summary>
        public void Choose(PawnId id, bool additive = false)
        {
            if (additive)
            {
                Toggle(id);
                return;
            }
            _pawns.Clear();
            if (id.IsValid) _pawns.Add(id);
            ClearCellTier();
            _missingFrames = 0;
            Changed?.Invoke(SelectionChange.Chosen);
        }

        /// <summary>
        /// Select a cell outright, without a pick and without the thing lying in it: the Inventory
        /// tab's Go (design 35). A pick on a stockpile cell selects the pile first, because that
        /// is what a click there usually means; Go is asking for the <i>store</i>, and the pane
        /// leads with the store only when the subject is the cell.
        /// </summary>
        public void ChooseCell(CellRef cell)
        {
            _pawns.Clear();
            Cell = cell;
            Thing = ThingId.None;
            ThingDef = -1;
            _missingFrames = 0;
            Changed?.Invoke(SelectionChange.Chosen);
        }

        /// <summary>
        /// Shift-click: the colonist goes in if they were out and out if they were in. Removing
        /// the last colonist empties the selection — the same click that builds it has to be able
        /// to take it apart again.
        /// </summary>
        public void Toggle(PawnId id)
        {
            if (!id.IsValid) return;
            if (!_pawns.Remove(id)) _pawns.Add(id);
            ClearCellTier();
            _missingFrames = 0;
            Changed?.Invoke(SelectionChange.Toggled);
        }

        /// <summary>
        /// Select a corpse outright (design 33 §5f): lane B's hit-test finds it under the pointer,
        /// and <c>HudDirectors.ChooseCorpse</c> (lane C) checks it is in the frame and calls this.
        /// Replaces the whole selection — a corpse is a single subject, like a pile.
        /// </summary>
        public void ChooseCorpse(int corpseId)
        {
            _pawns.Clear();
            ClearCellTier();
            Corpse = corpseId > 0 ? corpseId : 0;
            _missingFrames = 0;
            Changed?.Invoke(SelectionChange.Chosen);
        }

        void ClearCellTier()
        {
            Cell = null;
            Thing = ThingId.None;
            ThingDef = -1;
            Corpse = 0;
        }

        public void Clear(SelectionChange reason = SelectionChange.Cleared)
        {
            bool was = !IsEmpty;
            _pawns.Clear();
            ClearCellTier();
            _missingFrames = 0;
            if (was) Changed?.Invoke(reason);
        }

        /// <summary>The slice moved: whatever was selected is on another layer now, out of view.</summary>
        public void OnLayerChanged() => Clear(SelectionChange.LayerChanged);

        /// <summary>
        /// Once per interface frame with the current snapshot. A selected subject that the frame
        /// no longer carries survives <see cref="GraceFrames"/> frames, then the selection drops
        /// it with <see cref="SelectionChange.Died"/>, so no listener is ever left holding a
        /// handle the simulation has forgotten. In a multi-selection only the missing handles are
        /// dropped: one death must not take the living with it.
        /// </summary>
        public void Refresh(WorldSnapshot snapshot)
        {
            if (HasPawn)
            {
                bool anyMissing = false;
                for (int i = _pawns.Count - 1; i >= 0; i--)
                {
                    if (snapshot.TryGetPawn(_pawns[i], out _)) continue;
                    anyMissing = true;
                    if (_missingFrames >= GraceFrames) _pawns.RemoveAt(i);
                }

                if (_pawns.Count == 0)
                {
                    if (_missingFrames >= GraceFrames)
                    {
                        // Announced by hand rather than through Clear(), which would see an
                        // already-empty selection and say nothing — the one handle the frame
                        // carried away took the announcement with it.
                        _missingFrames = 0;
                        Changed?.Invoke(SelectionChange.Died);
                    }
                    else _missingFrames++;
                    return;
                }
                _missingFrames = anyMissing ? _missingFrames + 1 : 0;
                return;
            }

            if (HasThing)
            {
                if (ThingPresent(snapshot, Thing))
                {
                    _missingFrames = 0;
                    return;
                }
                _missingFrames++;
                if (_missingFrames > GraceFrames) Clear(SelectionChange.Died);
                return;
            }

            // A corpse stays where it fell (the C2 default), so this is for the day something
            // takes one away — a load into another world, a haul — on the pile's terms.
            if (HasCorpse)
            {
                if (CorpsePresent(snapshot, Corpse))
                {
                    _missingFrames = 0;
                    return;
                }
                _missingFrames++;
                if (_missingFrames > GraceFrames) Clear(SelectionChange.Died);
            }
        }

        static bool CorpsePresent(WorldSnapshot snapshot, int id)
        {
            var corpses = snapshot.Corpses;
            for (int i = 0; i < corpses.Length; i++)
                if (corpses[i].Id == id) return true;
            return false;
        }

        static bool ThingPresent(WorldSnapshot snapshot, ThingId id)
        {
            var things = snapshot.Things;
            for (int i = 0; i < things.Length; i++)
                if (things[i].Id == id) return true;
            return false;
        }

        /// <summary>
        /// The thing a pick landed on, if any. The cell itself first, then the cell standing on it
        /// — which is where a pile on natural ground lives.
        ///
        /// <para><b>The second look is the fix, and it is a playtest report.</b> A pile resting on
        /// bare ground sits in the air cell above the solid block the picker resolves to (the
        /// owner's rule: the tile below it or not at all), so matching the picked cell alone
        /// missed every pile that was not on a built slab — the click fell through to the cell
        /// pane, which read as "a wood pile cannot be selected" (owner, 2026-09-17). A thing
        /// resting on the block the player clicked <i>is</i> the thing the player clicked.</para>
        /// </summary>
        static void ThingAt(WorldSnapshot snapshot, CellRef cell, out ThingId id, out int def)
        {
            // **Contained things are not click targets.** They are published at their store's own
            // cell so that every count of what the colony holds stays right without being taught
            // anything — but a click on a shelf means the shelf, not whichever of its eight stacks
            // happens to come first in id order, which is a choice no player made. The same rule a
            // bed follows: clicking one selects the bed and not the sleeper.
            var things = snapshot.Things;
            for (int i = 0; i < things.Length; i++)
            {
                if (things[i].Contained || things[i].Cell != cell) continue;
                id = things[i].Id;
                def = things[i].DefIndex;
                return;
            }

            if (cell.Y + 1 < snapshot.Size.SizeY)
            {
                CellRef above = cell.Above;
                for (int i = 0; i < things.Length; i++)
                {
                    if (things[i].Contained || things[i].Cell != above) continue;
                    id = things[i].Id;
                    def = things[i].DefIndex;
                    return;
                }
            }

            id = ThingId.None;
            def = -1;
        }
    }
}
