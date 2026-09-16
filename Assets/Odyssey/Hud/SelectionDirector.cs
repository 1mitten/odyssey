#nullable enable
using System;
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
    /// A subject that stops being published is dropped after a one-frame grace (§2.3): the frame
    /// in which a colonist dies still shows them, so the pane can tombstone rather than blank.
    /// </summary>
    public sealed class SelectionDirector
    {
        /// <summary>Frames a subject may be absent from the snapshot before the selection drops it.</summary>
        public const int GraceFrames = 1;

        int _missingFrames;

        public PawnId Pawn { get; private set; } = PawnId.None;

        public ThingId Thing { get; private set; } = ThingId.None;

        /// <summary>The item def of <see cref="Thing"/>, for the presenter to draw it, or -1.</summary>
        public int ThingDef { get; private set; } = -1;

        /// <summary>The cell the last world pick landed on, or none. Never above the active layer.</summary>
        public CellRef? Cell { get; private set; }

        public bool HasPawn => Pawn.IsValid;
        public bool HasThing => Thing.IsValid;
        public bool IsEmpty => !HasPawn && !HasThing && !Cell.HasValue;

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
        public void Pick(CellRef? cell, PawnId pawnUnderPointer, WorldSnapshot snapshot)
        {
            Cell = cell;
            Pawn = cell.HasValue ? pawnUnderPointer : PawnId.None;
            Thing = ThingId.None;
            ThingDef = -1;
            if (cell.HasValue && !Pawn.IsValid)
            {
                ThingAt(snapshot, cell.Value, out ThingId thing, out int def);
                Thing = thing;
                ThingDef = def;
            }
            _missingFrames = 0;
            Changed?.Invoke(SelectionChange.Picked);
        }

        /// <summary>Select a colonist outright, without a pick: the roster, an alert, a jump.</summary>
        public void Choose(PawnId id)
        {
            Pawn = id;
            Thing = ThingId.None;
            ThingDef = -1;
            Cell = null;
            _missingFrames = 0;
            Changed?.Invoke(SelectionChange.Chosen);
        }

        public void Clear(SelectionChange reason = SelectionChange.Cleared)
        {
            bool was = !IsEmpty;
            Pawn = PawnId.None;
            Thing = ThingId.None;
            ThingDef = -1;
            Cell = null;
            _missingFrames = 0;
            if (was) Changed?.Invoke(reason);
        }

        /// <summary>The slice moved: whatever was selected is on another layer now, out of view.</summary>
        public void OnLayerChanged() => Clear(SelectionChange.LayerChanged);

        /// <summary>
        /// Once per interface frame with the current snapshot. A selected subject that the frame
        /// no longer carries survives <see cref="GraceFrames"/> frames, then the selection drops
        /// it with <see cref="SelectionChange.Died"/>, so no listener is ever left holding a
        /// handle the simulation has forgotten.
        /// </summary>
        public void Refresh(WorldSnapshot snapshot)
        {
            bool present = true;
            if (HasPawn) present = snapshot.TryGetPawn(Pawn, out _);
            else if (HasThing) present = ThingPresent(snapshot, Thing);
            else return;

            if (present)
            {
                _missingFrames = 0;
                return;
            }

            _missingFrames++;
            if (_missingFrames > GraceFrames) Clear(SelectionChange.Died);
        }

        static bool ThingPresent(WorldSnapshot snapshot, ThingId id)
        {
            var things = snapshot.Things;
            for (int i = 0; i < things.Length; i++)
                if (things[i].Id == id) return true;
            return false;
        }

        static void ThingAt(WorldSnapshot snapshot, CellRef cell, out ThingId id, out int def)
        {
            var things = snapshot.Things;
            for (int i = 0; i < things.Length; i++)
            {
                if (things[i].Cell != cell) continue;
                id = things[i].Id;
                def = things[i].DefIndex;
                return;
            }
            id = ThingId.None;
            def = -1;
        }
    }
}
