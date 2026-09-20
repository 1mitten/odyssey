#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Sim.Contracts
{
    /// <summary>What the player asked for. Extended as milestones add commands.</summary>
    public enum IntentKind
    {
        None = 0,
        SetGameSpeed,
        SetSliceLayer,
        Designate,
        CancelDesignation,
        SetForbidden,

        /// <summary>
        /// Put a building site on a cell: <c>A</c> is a <c>BuildingHandle</c>, <c>B</c> a
        /// <c>StuffHandle</c>, and <c>C</c> the facing 0–3 for a thing that rotates (0, and
        /// ignored, for everything else). Its own kind rather than a <c>Designate</c> with a third
        /// argument, because a designation is a verb applied to whatever is already there and this
        /// names a thing that is not there yet — it carries what to make and what to make it of,
        /// and the component that owns it is not the one that owns orders.
        /// </summary>
        PlaceBuilding,

        /// <summary>Take a building site off a cell, refunding whatever was delivered to it.</summary>
        CancelBuilding,

        /// <summary>
        /// Put a growing-zone cell down: <c>A</c> is a <c>PlantHandle</c>. Its own kind rather
        /// than a <see cref="Designate"/> with a payload, for the same reason
        /// <see cref="PlaceBuilding"/> is: a designation is a verb applied to whatever is already
        /// there, and this founds a thing that was not there — a zone carries its plant with it,
        /// and the component that owns zones is not the one that owns orders.
        /// </summary>
        DesignateZone,

        /// <summary>
        /// Take a cell back out of its growing zone, crop and all: a crop exists only inside its
        /// zone, so one intent removes field and planting together
        /// (docs/design/22-growing.md §4).
        /// </summary>
        CancelZone,

        /// <summary>
        /// Give one built bed to one colonist, or take it back: <see cref="Intent.Cell"/> is any
        /// cell of the bed and <c>A</c> the <c>PawnId</c>, or -1 to leave the bed unowned.
        ///
        /// <para><b>A persistent assignment, not a reservation.</b> Reservations are transient by
        /// design — all-or-nothing, released on job end — while ownership is state the world
        /// keeps: the bed sleeps its owner and nobody else until somebody says otherwise. One bed
        /// per colonist is the handler's rule, not the interface's hope: assigning a colonist who
        /// already has a bed releases the old one, so a pawn can never hold two and the sleep
        /// chooser never has to break a tie.</para>
        ///
        /// <para>A command, not a question: queued while the clock is paused and applied on
        /// unpause, exactly as a build order is.</para>
        /// </summary>
        AssignBedOwner,

        /// <summary>
        /// Put one job on one named colonist, now: <c>A</c> is a <c>JobIndex</c> value and
        /// <c>B</c> a <c>PawnId</c> value.
        ///
        /// <para><b>The only intent that names a pawn</b>, and it has to. Every other command here
        /// is about a cell and leaves the question of who answers it to the work scan; a forced
        /// order is the player overruling that scan for one colonist, so the colonist is half of
        /// what is being said. The job it names is an ordinary one — the same driver, the same
        /// toils, the same reservations — with the scan bypassed and
        /// <c>Job.PlayerForced</c> set.</para>
        /// </summary>
        ForceJob,

        /// <summary>
        /// Ask the world to publish detail about one cell: <see cref="Intent.Cell"/> is the cell,
        /// and <c>A</c> = -1 withdraws the question.
        ///
        /// <para><b>A question, not a command.</b> It changes no state the simulation owns — not
        /// a cell, not a pawn, nothing saved and nothing hashed — which is what lets a paused
        /// world answer it: the composition root can apply view intents and republish without
        /// spending a tick, where a real command must wait for a tick boundary. A re-ask of the
        /// cell already asked is quietly fine rather than a rejection; clicking the same ground
        /// twice is ordinary play, not an error to surface.</para>
        /// </summary>
        QueryCell,

        /// <summary>
        /// Debug-menu-only: put a fresh colonist at <see cref="Intent.Cell"/>. Not player content —
        /// no scenario, no starting kit — because the debug menu is testing the colony that already
        /// exists, not dealing a new one. <c>A</c> and <c>B</c> are unused.
        /// </summary>
        SpawnPawn,

        /// <summary>
        /// Debug-menu-only: grant <c>B</c> units of the item def indexed by <c>A</c> near
        /// <see cref="Intent.Cell"/>. Widens outward from the cell for one with room the way an
        /// ordinary drop does, so it behaves like any other item arriving rather than inventing a
        /// second way for one to appear.
        /// </summary>
        GiveResource,

        /// <summary>
        /// Fire the incident whose def index is <c>A</c>, now, whatever its gates say. The debug
        /// menu's row, and the seam a quest or a scripted beat would use later: the worker behind
        /// it is the same one a storyteller fires, so a forced event behaves exactly like an
        /// earned one. <see cref="Intent.Cell"/> and <c>B</c>, <c>C</c> are unused today and
        /// reserved for a forced landing cell.
        ///
        /// <para>Not applied while paused: it spawns things and needs a tick, exactly as
        /// <see cref="SpawnPawn"/> and <see cref="GiveResource"/> do. Refused as
        /// <see cref="IntentRejection.NotPermitted"/> when the worker says it cannot fire — for
        /// the supply drop, when no column on the board can take a landing.</para>
        /// </summary>
        InvokeIncident,

        /// <summary>
        /// Debug-menu-only: bring every standing crop to ripeness at once, daylight window and
        /// all. <see cref="Intent.Cell"/> and the payloads are unused — the ask is the whole
        /// field, because the menu is testing the harvest half and the four-day wait is the
        /// thing being skipped, not the thing being simulated. Refused with
        /// <see cref="IntentRejection.AlreadyInThatState"/> when nothing stands, so an empty
        /// board says "nothing to ripen" rather than quietly succeeding.
        /// </summary>
        DebugRipen,
        /// Set one colonist's priority for one work type: <c>A</c> is a <c>PawnId</c> value,
        /// <c>B</c> a <see cref="WorkHandle"/> and <c>C</c> the priority, 0 to 4, where <b>0 is
        /// never</b> and 1 is most urgent. <see cref="Intent.Cell"/> is unused — this is the second
        /// intent that names a pawn and the first that names nothing else.
        ///
        /// <para><b>The work type crosses as an index and the reason is in
        /// <c>Catalogue.cs</c>.</b> Skills reach the interface as named aspects and never touch
        /// this assembly, which is why there is no <c>SkillHandle</c>; a priority is written as
        /// well as read, and an intent carries integers, so the order has to be agreed somewhere
        /// and it is agreed there.</para>
        ///
        /// <para><b>Applied while paused.</b> Assigning work is exactly the thing a player pauses
        /// in order to do, and it meets the test the other paused intents meet: it writes state
        /// the player authored and needs no system to finish it. A priority that did not land
        /// until you pressed play would be the slab fault told a third time.</para>
        /// </summary>
        SetWorkPriority,

        /// <summary>
        /// Set one colonist's schedule for one hour: <c>A</c> is a <c>PawnId</c> value, <c>B</c>
        /// the hour 0–23 and <c>C</c> a <see cref="ScheduleHandle"/>.
        ///
        /// <para>The third intent that names a pawn, and it carries no cell for the same reason
        /// <see cref="SetWorkPriority"/> does not: a schedule is a fact about a person and a day,
        /// not about a place.</para>
        ///
        /// <para><b>It writes state nothing reads yet</b>, which is unusual and deliberate — see
        /// <see cref="ScheduleHandle"/>. That is also why the schedule is saved but <b>not
        /// hashed</b>: a value no system consults cannot affect a tick, which is the same test the
        /// saved view passes. The day the job system reads it, it enters the hash and the goldens
        /// move once, deliberately.</para>
        /// </summary>
        SetScheduleBlock,
    }

    /// <summary>
    /// Which intents a paused world may apply without spending a tick.
    ///
    /// <para><b>Why this exists.</b> A paused world never reaches a tick boundary, so it never
    /// drains its queue — and the player pauses in order to give orders. Until 2026-09-17 only
    /// <see cref="IntentKind.QueryCell"/> was let through, so laying a slab while paused put the
    /// order in the queue and nothing else: no site, no blueprint, nothing, until the clock was
    /// started and the queue drained (owner: *"if I pause the game, go up a depth and create a
    /// slab, I place the slab but then nothing appears until I press play"*). Pausing to plan is
    /// the genre's central interaction, so an order that does not land until you unpause is the
    /// one thing this cannot do.</para>
    ///
    /// <para><b>Why it is safe, which is the part the earlier comment was right to worry about.</b>
    /// While the clock is stopped nothing else runs, so applying a player's order the moment it is
    /// given produces exactly the state the next tick's drain would have produced. The hash is
    /// taken at tick boundaries and the boundary state is identical either way; the tick counter
    /// does not move, so a save written while paused correctly contains the order, which it did
    /// not before.</para>
    ///
    /// <para><b>What is deliberately not here.</b> <see cref="IntentKind.SetGameSpeed"/> keeps its
    /// own path — unpausing is what spends the tick, and routing it through here would be circular.
    /// <see cref="IntentKind.SetSliceLayer"/> is presentation state that the simulation need never
    /// hear about off-boundary. Everything else in this list is a player's order over a cell or a
    /// colonist: it writes state the player authored and needs no system to finish it, which is
    /// precisely the test for landing off-boundary.</para>
    /// </summary>
    public static class PausedIntents
    {
        public static bool AppliesWhilePaused(IntentKind kind) => kind switch
        {
            IntentKind.QueryCell => true,
            IntentKind.Designate => true,
            IntentKind.CancelDesignation => true,
            IntentKind.SetForbidden => true,
            IntentKind.PlaceBuilding => true,
            IntentKind.CancelBuilding => true,
            IntentKind.ForceJob => true,
            // Assigning a bed's owner is a player's order over a cell and meets the test above
            // exactly: it writes state the player authored and needs no system to finish it.
            // It matters more than most, because the pane that offers it is a thing you open
            // while paused — and a popover you pick a colonist from that leaves the row still
            // reading "nobody" until you press play is the slab fault told again.
            IntentKind.AssignBedOwner => true,
            // Painting a growing zone is the same act as designating: the player authored it,
            // nothing needs to run to make it true, and the brush is a thing you drag while
            // paused. Cancelling it likewise. Both write state the player owns outright.
            IntentKind.DesignateZone => true,
            IntentKind.CancelZone => true,
            // The Work tab is a panel you open while paused, and a grid that accepted twenty
            // clicks and applied none of them until you pressed play would be the worst version
            // of the fault this list was written to end.
            IntentKind.SetWorkPriority => true,
            // Same test and the same panel: a player pauses to plan the day, and half of that
            // panel is the day.
            IntentKind.SetScheduleBlock => true,
            _ => false,
        };
    }

    /// <summary>
    /// Why the simulation refused. A rejection always carries a reason, because a command that
    /// silently does nothing is the worst possible outcome for a player.
    /// </summary>
    public enum IntentRejection
    {
        None = 0,
        UnknownIntent,
        OutOfBounds,
        NotPermitted,
        AlreadyInThatState,
        QueueFull,
    }

    /// <summary>
    /// An immutable player command. The UI never mutates the simulation; it submits one of these
    /// and the simulation consumes it at a tick boundary.
    ///
    /// Being a value type with no references means an intent can be logged, replayed and hashed,
    /// which is what makes deterministic replay and designation undo fall out for free rather
    /// than needing separate machinery.
    /// </summary>
    public readonly struct Intent
    {
        public readonly IntentKind Kind;
        public readonly CellRef Cell;
        public readonly int A;
        public readonly int B;

        /// <summary>
        /// The third payload, so far used by one intent: <c>PlaceBuilding</c> carries the facing
        /// of a rotatable thing here. Zero for every intent that has no third thing to say, which
        /// is also what "facing 0" means, so an old caller that names none places north.
        /// </summary>
        public readonly int C;

        public Intent(IntentKind kind, CellRef cell = default, int a = 0, int b = 0, int c = 0)
        {
            Kind = kind;
            Cell = cell;
            A = a;
            B = b;
            C = c;
        }

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add((int)Kind);
            hash.Add(Cell);
            hash.Add(A);
            hash.Add(B);
            hash.Add(C);
        }

        public override string ToString() => $"{Kind}({Cell}, {A}, {B}, {C})";
    }

    /// <summary>An intent the simulation refused, and why, so the UI can say so.</summary>
    public readonly struct RejectedIntent
    {
        public readonly Intent Intent;
        public readonly IntentRejection Reason;

        public RejectedIntent(Intent intent, IntentRejection reason)
        {
            Intent = intent;
            Reason = reason;
        }

        public override string ToString() => $"{Intent} rejected: {Reason}";
    }

    /// <summary>
    /// The one-way channel from presentation into the simulation.
    ///
    /// Submissions are buffered and drained at a tick boundary, never applied immediately. That
    /// single rule buys four things at once: the tick can move off the main thread later without
    /// rewriting the UI, replay is deterministic because the intent log is the only input,
    /// designations become undoable, and a modder has an obvious place to intercept commands.
    ///
    /// Capacity is bounded so a runaway producer cannot grow the queue without limit; past the
    /// bound, submissions are rejected with <see cref="IntentRejection.QueueFull"/> rather than
    /// silently dropped.
    /// </summary>
    public sealed class IntentBus
    {
        readonly List<Intent> _pending;
        readonly List<RejectedIntent> _rejected = new List<RejectedIntent>();
        readonly int _capacity;

        public IntentBus(int capacity = 4096)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _pending = new List<Intent>(capacity);
        }

        public int PendingCount => _pending.Count;

        /// <summary>Called by presentation. Returns false when the queue is full.</summary>
        public bool Submit(in Intent intent)
        {
            if (_pending.Count >= _capacity)
            {
                _rejected.Add(new RejectedIntent(intent, IntentRejection.QueueFull));
                return false;
            }
            _pending.Add(intent);
            return true;
        }

        /// <summary>
        /// Called by the simulation at the start of a tick. The handler returns the rejection
        /// reason, or <see cref="IntentRejection.None"/> when it applied the intent.
        ///
        /// Order is submission order, always, because a determinism gate compares state hashes
        /// and any reordering here would be a desync that is very hard to trace.
        /// </summary>
        public void Drain(Func<Intent, IntentRejection> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            for (int i = 0; i < _pending.Count; i++)
            {
                var intent = _pending[i];
                var reason = handler(intent);
                if (reason != IntentRejection.None) _rejected.Add(new RejectedIntent(intent, reason));
            }
            _pending.Clear();
        }

        /// <summary>
        /// Is any intent waiting that this test accepts? The same scan as
        /// <see cref="HasPending(IntentKind)"/>, for a caller that cares about a set of kinds
        /// rather than one — which the paused frame does, and used to do by naming a single kind
        /// and quietly stranding every other order in the queue.
        /// </summary>
        public bool HasAnyPending(Func<IntentKind, bool> claim)
        {
            if (claim == null) throw new ArgumentNullException(nameof(claim));
            for (int i = 0; i < _pending.Count; i++)
                if (claim(_pending[i].Kind)) return true;
            return false;
        }

        /// <summary>Is any intent of this kind waiting? A scan of a list that is empty almost always.</summary>
        public bool HasPending(IntentKind kind)
        {
            for (int i = 0; i < _pending.Count; i++)
                if (_pending[i].Kind == kind) return true;
            return false;
        }

        /// <summary>
        /// Drain only the intents the predicate claims, leaving the rest pending in submission
        /// order.
        ///
        /// <para><b>This exists for view intents, and nothing else should use it.</b> A paused
        /// world never reaches a tick boundary, so a question queued as an intent would go
        /// unanswered exactly when the player is most likely to be asking it — inspecting a world
        /// they have stopped. A view intent changes no state the simulation owns, so applying it
        /// outside a tick cannot desync anything a state hash can see; a command must never come
        /// through here, because a command applied off-boundary is a replay that cannot be
        /// reproduced.</para>
        /// </summary>
        public void DrainWhere(Func<Intent, bool> claim, Func<Intent, IntentRejection> handler)
        {
            if (claim == null) throw new ArgumentNullException(nameof(claim));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            for (int i = 0; i < _pending.Count; i++)
            {
                if (!claim(_pending[i])) continue;
                var intent = _pending[i];
                _pending.RemoveAt(i);
                i--;
                var reason = handler(intent);
                if (reason != IntentRejection.None) _rejected.Add(new RejectedIntent(intent, reason));
            }
        }

        /// <summary>Rejections since the last time they were taken. Presentation surfaces these.</summary>
        public IReadOnlyList<RejectedIntent> Rejected => _rejected;

        public void ClearRejected() => _rejected.Clear();
    }
}
