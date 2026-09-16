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
        /// <c>StuffHandle</c>. Its own kind rather than a <c>Designate</c> with a third argument,
        /// because a designation is a verb applied to whatever is already there and this names a
        /// thing that is not there yet — it carries what to make and what to make it of, and the
        /// component that owns it is not the one that owns orders.
        /// </summary>
        PlaceBuilding,

        /// <summary>Take a building site off a cell, refunding whatever was delivered to it.</summary>
        CancelBuilding,
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

        public Intent(IntentKind kind, CellRef cell = default, int a = 0, int b = 0)
        {
            Kind = kind;
            Cell = cell;
            A = a;
            B = b;
        }

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add((int)Kind);
            hash.Add(Cell);
            hash.Add(A);
            hash.Add(B);
        }

        public override string ToString() => $"{Kind}({Cell}, {A}, {B})";
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

        /// <summary>Rejections since the last time they were taken. Presentation surfaces these.</summary>
        public IReadOnlyList<RejectedIntent> Rejected => _rejected;

        public void ClearRejected() => _rejected.Clear();
    }
}
