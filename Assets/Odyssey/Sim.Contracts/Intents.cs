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

        /// <summary>
        /// Take the orders off a cell, refunding whatever was delivered to them: the building site
        /// and any power line order or removal mark (design 32 §3). <c>A</c> = 1 takes the building
        /// site alone — the building pane's Cancel, which names one order rather than a cell.
        /// </summary>
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

        /// <summary>
        /// Put a storage-zone cell down: <c>A</c> is the <b>anchor cell index</b> of the drag this
        /// cell belongs to, and <c>B</c> a <c>StoragePreset</c> for the zone a drag founds.
        ///
        /// <para><b>Why the anchor rides along.</b> A growing zone joins whatever same-plant
        /// ground touches it and folds two zones into one, which is sound because a growing zone
        /// is identified by its crop and two touching carrot fields are interchangeable. A storage
        /// zone carries a configuration, so a fold would silently destroy one of two filters and
        /// "extend the zone you touched" would silently adopt a stranger's. The anchor decides
        /// instead: a drag that begins inside a zone extends <em>that</em> zone, and one that
        /// begins outside founds a new one and takes from any zone it crosses. Two existing zones
        /// never merge, which is also what makes overlap impossible rather than merely
        /// discouraged (docs/design/26-storage.md, and docs/plans/storage.md §4).</para>
        ///
        /// <para>The anchor is a cell index and not a minted id, because it is data with a meaning:
        /// the handler resolves it against the zone grid, and a drag replayed or split across a
        /// tick boundary degrades into two zones rather than into a corrupt one.</para>
        /// </summary>
        DesignateStorage,

        /// <summary>
        /// Take a cell back out of its storage zone. A zone reduced to nothing is deleted, exactly
        /// as a growing zone is; anything lying in the cell becomes loose again and the haul scan
        /// picks it up on the next think.
        /// </summary>
        CancelStorage,

        /// <summary>
        /// Set the priority of the zone under <see cref="Intent.Cell"/>: <c>A</c> is a
        /// <c>StoragePriority</c> value, 0 to 4. Named for the cell rather than the zone because
        /// the same intent has to serve a crate the day one exists, and a cell is the one address
        /// both of them have.
        /// </summary>
        SetStoragePriority,

        /// <summary>
        /// Change one bit of the filter of the zone under <see cref="Intent.Cell"/>: <c>A</c> is
        /// the scope — 0 one item def, 1 a whole category, 2 a preset — <c>B</c> the index within
        /// that scope, and <c>C</c> is 0 for off and 1 for on. A preset ignores <c>C</c>, because
        /// a preset is not a switch.
        /// </summary>
        SetStorageFilter,

        /// <summary>
        /// Draft or release one colonist (design 33 §2d): <c>A</c> is a <c>PawnId</c> value and
        /// <c>B</c> is 1 to draft, 0 to release. Drafting ends the job in hand — keeping the step
        /// in progress — and holds the colonist where it stands.
        /// </summary>
        SetDrafted,

        /// <summary>
        /// Send one drafted colonist to a cell (design 33 §2d): <see cref="Intent.Cell"/> is the
        /// cell the player clicked, lifted to where a colonist stands in that column, and <c>A</c>
        /// a <c>PawnId</c> value. Refused for a colonist who is not drafted.
        /// </summary>
        OrderMove,

        /// <summary>
        /// Mark the built line in <see cref="Intent.Cell"/> for a colonist to take up (design 32
        /// §3). Its own kind rather than a <see cref="Designate"/>, because a designation is one
        /// byte per cell and a cell with a line in it very often has a wall or a floor in it too,
        /// which the deconstruct designation already names. Named at the ground, the line
        /// standing on it.
        /// </summary>
        RemoveConduit,

        /// <summary>
        /// Switch the power building in <see cref="Intent.Cell"/> on (<c>A</c> = 1) or off
        /// (<c>A</c> = 0). Either cell of a two-cell building names it. Applied at once, with no
        /// colonist sent to do it (design 32 §5).
        /// </summary>
        SetPowerSwitch,

        /// <summary>
        /// Start (<c>A</c> = 1) or stop (<c>A</c> = 0) publishing the built power lines — the
        /// interface is showing them (design 32 §9). <b>A question, not a command</b>, exactly as
        /// <see cref="QueryCell"/> is: it changes no state the simulation owns, nothing saved and
        /// nothing hashed, so a paused world answers it at once.
        /// </summary>
        WatchPower,

        /// <summary>
        /// Take back the line order or the removal mark in <see cref="Intent.Cell"/>, and nothing
        /// else in the cell (design 32 §14). The pane's Cancel for a line; narrower than
        /// <see cref="CancelBuilding"/>, which also takes a building order standing in the same
        /// cell — right for a cancel drag, wrong for a button that names one thing.
        /// </summary>
        CancelConduit,

        // The combat line's three orders (design 33 §5), claimed together by the contracts step.
        // Each has had a handler from that commit, filled in by the lane that owned it. They
        // follow power's four because power reached main first (merge of 2026-09-24).

        /// <summary>
        /// Send one drafted colonist to attack (design 33 §1, C2): <c>A</c> is the attacker's
        /// <c>PawnId</c> value and <c>B</c> the target's. A target of 0 names no pawn, and then
        /// <see cref="Intent.Cell"/> is a building to strike (C6). Handler:
        /// <c>JobSystem.HandleOrderAttack</c>.
        /// </summary>
        OrderAttack,

        /// <summary>
        /// Send one colonist to pick a weapon up and hold it (design 33 §1, C3): <c>A</c> is the
        /// colonist's <c>PawnId</c> value and <c>B</c> the weapon's <c>ThingId</c> value;
        /// <see cref="Intent.Cell"/> is where it was clicked. Handler:
        /// <c>JobSystem.HandleOrderEquip</c>.
        /// </summary>
        OrderEquip,

        /// <summary>
        /// Send one drafted colonist to carry a downed one to a bed (design 33 §1, C4): <c>A</c>
        /// is the rescuer's <c>PawnId</c> value and <c>B</c> the patient's. Handler:
        /// <c>JobSystem.HandleOrderRescue</c>.
        /// </summary>
        OrderRescue,

        /// <summary>
        /// Debug-menu-only (design 33 §9i): every colonist standing with nothing in her hand takes a
        /// random melee weapon into it at once — made beside her and taken straight up, as a
        /// marauder is armed at spawn. Colonists already holding one keep it. No arguments. Not
        /// player content; not applied while paused, like the other debug spawns.
        /// </summary>
        DebugArmColonists,
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
            // And the storage zone, for the same reason and one more: its filter and its priority
            // are settings a player opens a panel to change, and a panel is a thing you open while
            // paused. A tick-boundary filter would leave the popover reading one thing and the
            // world doing another until you pressed play.
            IntentKind.DesignateStorage => true,
            IntentKind.CancelStorage => true,
            IntentKind.SetStoragePriority => true,
            IntentKind.SetStorageFilter => true,
            // The draft and its orders (design 33 §2d): a player's order over a colonist, which is
            // the thing a player pauses to give — a fight is planned with the clock stopped.
            IntentKind.SetDrafted => true,
            IntentKind.OrderMove => true,
            // Taking a line up and throwing a switch are orders over a cell like any other: the
            // player authored them and nothing needs to run to make them true (design 32).
            IntentKind.RemoveConduit => true,
            IntentKind.SetPowerSwitch => true,
            // A view question, like QueryCell: the lines appear the moment the tool is armed,
            // paused or not.
            IntentKind.WatchPower => true,
            IntentKind.CancelConduit => true,

            // The fight's orders, on the same test (design 33 §5): a player's order over a
            // colonist, written by the player and finished by no system — the job it starts is
            // the next tick's business, exactly as a move's walk is.
            IntentKind.OrderAttack => true,
            IntentKind.OrderEquip => true,
            IntentKind.OrderRescue => true,
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
