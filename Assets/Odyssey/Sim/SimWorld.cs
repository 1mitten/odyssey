#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim
{
    /// <summary>
    /// One simulated world: a seed, a tick counter, and everything that ticks.
    ///
    /// Deliberately a plain C# object with no Unity dependency (ADR 0005), so it can be built and
    /// ticked from a test with no scene, no GameObject and no editor. Construct it through
    /// <see cref="SimWorldBuilder"/> rather than by hand, so that there is exactly one place
    /// where a world comes into being.
    ///
    /// The tick phase order below is part of the determinism contract, not an implementation
    /// detail. See docs/design/01-architecture.md section 3.
    /// </summary>
    public sealed class SimWorld
    {
        readonly List<ITickable> _tickables = new List<ITickable>();
        IStateHashable[] _hashables = Array.Empty<IStateHashable>();
        readonly List<ITickable>[] _byGroup;
        readonly List<Action<SimWorld>> _deferred = new List<Action<SimWorld>>();
        readonly Dictionary<IntentKind, Func<Intent, IntentRejection>> _intentHandlers =
            new Dictionary<IntentKind, Func<Intent, IntentRejection>>();
        ISnapshotContributor[] _contributors = Array.Empty<ISnapshotContributor>();

        /// <summary>
        /// The intent handler, held as a delegate rather than converted from a method group at
        /// every tick.
        ///
        /// <para><b>This was the whole of the tick's at-rest allocation.</b> `Intents.Drain` takes
        /// a <see cref="Func{T, TResult}"/>, and writing `Drain(HandleIntent)` builds a fresh
        /// delegate object each call — 64 bytes on a 64-bit runtime, paid by every tick of every
        /// game whether or not a single intent was submitted. Measured at 67.4 bytes a tick on an
        /// *empty* world, with a colony adding nothing, which is what pointed here: the cost
        /// scaled with neither pawns nor systems nor contributors, so it could not be any of
        /// them.</para>
        /// </summary>
        readonly Func<Intent, IntentRejection> _handleIntent;

        /// <summary>Held for the same reason as <see cref="_handleIntent"/>: a lambda written at
        /// the call site is a fresh allocation every call, and this one runs on every paused
        /// frame the player is doing anything at all.</summary>
        readonly Func<Intent, bool> _appliesWhilePaused;

        internal SimWorld(uint seed, GridSize size)
        {
            Seed = seed;
            Size = size;
            CurrentTick = 0;
            _handleIntent = HandleIntent;
            _appliesWhilePaused = i => PausedIntents.AppliesWhilePaused(i.Kind);
            _byGroup = new[]
            {
                new List<ITickable>(), // Normal
                new List<ITickable>(), // Rare
                new List<ITickable>(), // Long
            };
        }

        public uint Seed { get; }
        public GridSize Size { get; }

        /// <summary>Player commands in, consumed at the start of each tick.</summary>
        public IntentBus Intents { get; } = new IntentBus();

        /// <summary>World state out, published at the end of each tick.</summary>
        public WorldViewStore Views { get; } = new WorldViewStore();

        /// <summary>Subsystems, frozen in phase and order at construction.</summary>
        public WorldSystemSchedule Systems { get; private set; } = WorldSystemSchedule.Empty;

        /// <summary>0 paused, 1 normal, 2 fast, 3 very fast. A tick-rate multiplier, never a delta.</summary>
        public int GameSpeed { get; private set; } = 1;

        /// <summary>Ticks elapsed. Starts at 0 and is part of the state hash.</summary>
        public int CurrentTick { get; private set; }

        /// <summary>
        /// Optional, and null in every ordinary run: something that wants the state hash at every
        /// tick boundary, for binary-searching the first tick two runs disagree on.
        /// <see cref="Diagnostics.HashTrace"/> is the implementation. Attaching one costs a full
        /// <see cref="ComputeStateHash"/> per tick and cannot change what the world does.
        /// </summary>
        public Diagnostics.ITickHashSink? HashSink { get; set; }

        /// <summary>
        /// Optional, and null in every ordinary run: something that wants the duration of each
        /// phase of each tick. <see cref="Diagnostics.PhaseTrace"/> is the implementation.
        ///
        /// <para>It lives here because the phase order lives here and
        /// <see cref="WorldSystemSchedule"/>'s run methods are internal, so a caller that timed
        /// the phases itself would be keeping a second copy of the order. When null the whole
        /// mechanism is one branch per phase; when attached it is two timestamp reads per phase,
        /// which is small against the phases but not nothing, and is why it is opt-in.</para>
        /// </summary>
        public Diagnostics.ITickPhaseSink? PhaseSink { get; set; }

        public IReadOnlyList<ITickable> Tickables => _tickables;

        /// <summary>
        /// Register something that ticks. Registration order is part of the determinism contract:
        /// two worlds built from the same seed must register in the same order, which is why
        /// world construction goes through a builder rather than happening ad hoc.
        /// </summary>
        public void Register(ITickable tickable)
        {
            if (tickable == null) throw new ArgumentNullException(nameof(tickable));
            _tickables.Add(tickable);
            switch (tickable.TickGroup)
            {
                case TickGroup.Never: break;
                case TickGroup.Normal: _byGroup[0].Add(tickable); break;
                case TickGroup.Rare: _byGroup[1].Add(tickable); break;
                case TickGroup.Long: _byGroup[2].Add(tickable); break;
                default: throw new ArgumentOutOfRangeException(nameof(tickable));
            }
        }

        /// <summary>
        /// Queue work to run at the structural-events phase of this tick. Collapses, spawns and
        /// removals go here rather than happening inline: mutating the world while a scan is
        /// walking it is the classic source of both crashes and desyncs.
        /// </summary>
        public void Defer(Action<SimWorld> action) => _deferred.Add(action);

        /// <summary>
        /// <see cref="Defer"/>, except that work queued <i>while the deferred phase is running</i>
        /// runs in it, after the batch that queued it, rather than next tick. For a removal that
        /// must not outlive its tick: a pawn killed by a fall is killed inside a collapse, a dig or
        /// a deconstruction — all deferred — and left to the next tick she stood, thought and walked
        /// for one tick past the death line, and a save between the two wrote her out alive with
        /// her removal lost (design 43 §15e). Everything else keeps <see cref="Defer"/>'s rule.
        /// </summary>
        public void DeferThisTick(Action<SimWorld> action)
        {
            if (_inDeferredPhase) _sameTick.Add(action);
            else _deferred.Add(action);
        }

        readonly List<Action<SimWorld>> _sameTick = new List<Action<SimWorld>>();
        bool _inDeferredPhase;

        /// <summary>Advance exactly one tick, in the fixed phase order.</summary>
        public void Tick()
        {
            // Null in an ordinary run, in which case Mark below does nothing at all. Read once so
            // that a sink attached mid-tick cannot time half the phases.
            Diagnostics.ITickPhaseSink? phases = PhaseSink;
            long mark = phases != null ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;

            // 1. Intents from the UI, in submission order.
            Intents.Drain(_handleIntent);
            Mark(phases, Diagnostics.TickSegment.Intents, ref mark);

            // 2. World systems: grid propagation, support solving, region rebuild.
            Systems.RunWorldSystems(this);
            Mark(phases, Diagnostics.TickSegment.WorldSystems, ref mark);

            // 3. Things, by tick group.
            TickGroupMembers(_byGroup[0], (int)TickGroup.Normal);
            TickGroupMembers(_byGroup[1], (int)TickGroup.Rare);
            TickGroupMembers(_byGroup[2], (int)TickGroup.Long);
            Mark(phases, Diagnostics.TickSegment.Things, ref mark);

            // 4. Pawns: needs, think tree, job execution, movement.
            Systems.RunPawnSystems(this);
            Mark(phases, Diagnostics.TickSegment.Pawns, ref mark);

            // 5. Deferred structural events, applied at one point.
            if (_deferred.Count > 0)
            {
                // Snapshot first: an action may defer more work, which belongs to the next tick.
                var toRun = _deferred.ToArray();
                _deferred.Clear();
                _inDeferredPhase = true;
                try
                {
                    foreach (var action in toRun) action(this);
                    // What the batch queued for this tick (DeferThisTick): a death in a fall. Bounded,
                    // because a removal queues nothing further of its kind.
                    for (int round = 0; round < 8 && _sameTick.Count > 0; round++)
                    {
                        var more = _sameTick.ToArray();
                        _sameTick.Clear();
                        foreach (var action in more) action(this);
                    }
                    _deferred.AddRange(_sameTick);
                    _sameTick.Clear();
                }
                finally
                {
                    _inDeferredPhase = false;
                }
            }
            Mark(phases, Diagnostics.TickSegment.Deferred, ref mark);

            // 6. Publish the snapshot, after every system has finished mutating the world.
            Views.Publish(this, _contributors);
            Mark(phases, Diagnostics.TickSegment.Snapshot, ref mark);

            // 7. The hash trace, when one is attached. Recorded before the counter moves, so the
            // entry is labelled with the tick that produced the state — which is the tick to
            // re-run when two traces part here.
            HashSink?.Record(CurrentTick, ComputeStateHash().Value);
            Mark(phases, Diagnostics.TickSegment.Hash, ref mark);

            CurrentTick++;
        }

        /// <summary>
        /// Hand one phase's duration to the sink and start the next one. Kept to a single
        /// null-check when nothing is attached, which is every ordinary run.
        /// </summary>
        void Mark(Diagnostics.ITickPhaseSink? sink, Diagnostics.TickSegment phase, ref long since)
        {
            if (sink == null) return;

            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            sink.Record(CurrentTick, phase, now - since);
            since = now;
        }

        /// <summary>
        /// Apply one player command, or say why not. Intents that a milestone has not implemented
        /// yet are rejected explicitly rather than ignored, so a command never silently does
        /// nothing.
        /// </summary>
        IntentRejection HandleIntent(Intent intent)
        {
            // A component that owns a kind of command handles it; the switch below is only for
            // what the world itself owns. One handler per kind, registered at construction, so
            // two components can never both claim a command and disagree about it.
            if (_intentHandlers.TryGetValue(intent.Kind, out var handler)) return handler(intent);

            switch (intent.Kind)
            {
                case IntentKind.SetSliceLayer:
                    if (intent.A < 0 || intent.A >= Size.SizeY) return IntentRejection.OutOfBounds;
                    if (Views.SliceLayer == intent.A) return IntentRejection.AlreadyInThatState;
                    Views.SliceLayer = intent.A;
                    return IntentRejection.None;

                case IntentKind.SetGameSpeed:
                    if (intent.A < 0 || intent.A > 3) return IntentRejection.OutOfBounds;
                    if (GameSpeed == intent.A) return IntentRejection.AlreadyInThatState;
                    GameSpeed = intent.A;
                    return IntentRejection.None;

                case IntentKind.WatchPower:
                    // A question, like QueryCell below: whether the built lines are published.
                    Views.WatchPower = intent.A != 0;
                    return IntentRejection.None;

                case IntentKind.WatchHome:
                    // The same question about the home's border (design 43 §5c).
                    Views.WatchHome = intent.A != 0;
                    return IntentRejection.None;

                case IntentKind.QueryCell:
                    // A question, not a command: it touches nothing but the view, so it has no
                    // state worth rejecting against. Re-asking the cell already asked, and asking
                    // a cell off the board, are quietly no-ops — a re-click is ordinary play and a
                    // pick that resolved off-board never had a subject to show.
                    if (intent.A < 0)
                    {
                        Views.QueryCell = -1;
                    }
                    else if (Size.Contains(intent.Cell))
                    {
                        Views.QueryCell = Size.Index(intent.Cell);
                    }
                    return IntentRejection.None;

                default:
                    return IntentRejection.UnknownIntent;
            }
        }

        /// <summary>
        /// Answer view questions and republish, without advancing the world by a single tick.
        ///
        /// <para><b>This exists because a paused world never reaches a tick boundary.</b> The
        /// player inspects and orders a stopped world more than a running one, so anything queued
        /// as an intent would wait exactly when it is most likely to be sent.</para>
        ///
        /// <para><b>It used to drain questions only</b>, on the argument that a command must wait
        /// for a tick boundary. That was too strict and the owner found the hole in it on
        /// 2026-09-17: a slab laid while paused produced no site at all until the clock started.
        /// <see cref="PausedIntents.AppliesWhilePaused"/> now names the set and carries the
        /// reasoning — briefly, while the clock is stopped nothing else runs, so applying a
        /// player's order at once gives exactly the state the next tick's drain would have given,
        /// and the boundary the hash is taken at is unchanged.</para>
        ///
        /// <para>The publish is the same one <see cref="Tick"/> ends with, over the same settled
        /// world, so the frame it produces is indistinguishable from a tick's apart from the
        /// question it now answers. Nothing is hashed and the tick counter does not move.</para>
        /// </summary>
        public void RepublishViews()
        {
            Intents.DrainWhere(_appliesWhilePaused, _handleIntent);
            Views.Publish(this, _contributors);
        }

        public void Tick(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            for (int i = 0; i < count; i++) Tick();
        }

        void TickGroupMembers(List<ITickable> members, int interval)
        {
            if (interval <= 1)
            {
                for (int i = 0; i < members.Count; i++) members[i].Tick(this);
                return;
            }

            // Hash-offset phase spreading: a member ticks on the tick where its offset lands.
            // Over any window of `interval` ticks each member ticks exactly once, and the
            // population is spread evenly rather than all firing on the same tick.
            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                int offset = member.TickPhaseOffset % interval;
                if (offset < 0) offset += interval;
                if ((CurrentTick + offset) % interval == 0) member.Tick(this);
            }
        }

        /// <summary>
        /// The world state hash. Only registration-ordered, hashable state contributes —
        /// never caches, never derived data, never anything floating point.
        /// </summary>
        public StateHash ComputeStateHash()
        {
            var hash = StateHash.New();
            hash.Add(Seed);
            hash.Add(CurrentTick);
            hash.Add(Size.SizeX);
            hash.Add(Size.SizeZ);
            hash.Add(Size.SizeY);

            // State that is neither a tickable nor a system, of which the cell grid is the whole
            // point: the world itself is not a thing that ticks, and until 2026-09-17 that meant
            // it was in no hash at all. Registered first and in registration order, so it lands
            // ahead of anything derived from it.
            for (int i = 0; i < _hashables.Length; i++) _hashables[i].ContributeTo(ref hash);

            for (int i = 0; i < _tickables.Count; i++)
                if (_tickables[i] is IStateHashable hashable)
                    hashable.ContributeTo(ref hash);

            // Subsystems too, in schedule order. They were left out until a system first had
            // state worth pinning — the job counters — and the omission was the sort that shows
            // up as a save that resumes wrongly rather than as anything obvious. Schedule order
            // is sorted and fixed at construction, so this is as deterministic as the tickables.
            Contribute(Systems.WorldSystems, ref hash);
            Contribute(Systems.PawnSystems, ref hash);
            return hash;
        }

        static void Contribute(IReadOnlyList<IWorldSystem> systems, ref StateHash hash)
        {
            for (int i = 0; i < systems.Count; i++)
                if (systems[i] is IStateHashable hashable)
                    hashable.ContributeTo(ref hash);
        }

        internal void SetSnapshotContributors(ISnapshotContributor[] contributors) => _contributors = contributors;

        internal void SetHashables(IStateHashable[] hashables) => _hashables = hashables;

        internal void SetIntentHandler(IntentKind kind, Func<Intent, IntentRejection> handler)
        {
            if (kind == IntentKind.None) throw new ArgumentOutOfRangeException(nameof(kind));
            if (_intentHandlers.ContainsKey(kind))
                throw new InvalidOperationException($"Intent {kind} already has a handler.");
            _intentHandlers[kind] = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>Whether a component has claimed this kind of command.</summary>
        public bool HandlesIntent(IntentKind kind) => _intentHandlers.ContainsKey(kind);

        internal void SetSystems(WorldSystemSchedule schedule) => Systems = schedule;

        /// <summary>Restore the tick counter when loading a save. Not for any other use.</summary>
        internal void RestoreTick(int tick) => CurrentTick = tick;

        /// <summary>
        /// Put the clock somewhere other than midnight before the world runs.
        ///
        /// <para>A colony that starts at tick 0 starts at 00:00, which is the middle of the
        /// night. That is fine for a headless run and wrong for a person opening the game: the
        /// board is lit for midday whatever the clock says, so a player sees noon and hears —
        /// correctly, and confusingly — the middle of the night.</para>
        ///
        /// <para><b>Only before the first tick.</b> <see cref="CurrentTick"/> is in the state
        /// hash and seeds the per-tick random stream, so moving it later would be moving the
        /// world under everything that has already happened in it. Tests and the headless day
        /// leave it alone and still begin at zero, so nothing baked moves.</para>
        /// </summary>
        public void StartAtTick(int tick)
        {
            if (CurrentTick != 0)
                throw new InvalidOperationException(
                    $"the clock has already run to {CurrentTick}; it can only be set before the first tick");
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "a clock does not start before zero");

            CurrentTick = tick;
        }

        /// <summary>The random stream for this tick and a named purpose.</summary>
        public DeterministicRandom RandomForTick(uint purpose) =>
            DeterministicRandom.ForTick(Seed, CurrentTick, purpose);
    }

    /// <summary>
    /// The composition root. One place builds a world, so a test builds the same world the game
    /// does, and registration order — which the determinism contract depends on — is defined in
    /// exactly one place.
    /// </summary>
    public sealed class SimWorldBuilder
    {
        readonly List<Func<SimWorld, ITickable>> _factories = new List<Func<SimWorld, ITickable>>();
        readonly List<ISnapshotContributor> _contributors = new List<ISnapshotContributor>();
        readonly List<Func<SimWorld, IWorldSystem>> _systemFactories = new List<Func<SimWorld, IWorldSystem>>();
        readonly List<(IntentKind kind, Func<Intent, IntentRejection> handler)> _intentHandlers =
            new List<(IntentKind, Func<Intent, IntentRejection>)>();
        readonly List<Pawns.WorkGiver> _workGivers = new List<Pawns.WorkGiver>();
        readonly List<IStateHashable> _hashables = new List<IStateHashable>();
        uint _seed = 1;
        GridSize _size = GridSize.ScaleTarget;

        public SimWorldBuilder WithSeed(uint seed)
        {
            _seed = seed;
            return this;
        }

        public SimWorldBuilder WithSize(GridSize size)
        {
            _size = size;
            return this;
        }

        public SimWorldBuilder AddTickable(Func<SimWorld, ITickable> factory)
        {
            _factories.Add(factory ?? throw new ArgumentNullException(nameof(factory)));
            return this;
        }

        /// <summary>
        /// Add something that writes into the published snapshot. Contributors run in the order
        /// added, which keeps the published frame byte-identical for a given world state.
        /// </summary>
        public SimWorldBuilder AddSnapshotContributor(ISnapshotContributor contributor)
        {
            _contributors.Add(contributor ?? throw new ArgumentNullException(nameof(contributor)));
            return this;
        }

        /// <summary>
        /// Add state that belongs in the world hash but neither ticks nor is a system.
        ///
        /// <para>The cell grid is why this exists. Every other hashable reaches
        /// <see cref="SimWorld.ComputeStateHash"/> by also being an <c>ITickable</c> or an
        /// <c>IWorldSystem</c>, and the world itself is neither — so the terrain, floors, edifices
        /// and flags were in no hash at all until this was added. Registration order is part of
        /// the determinism contract, as it is for the other two lists.</para>
        /// </summary>
        public SimWorldBuilder AddHashable(IStateHashable hashable)
        {
            _hashables.Add(hashable ?? throw new ArgumentNullException(nameof(hashable)));
            return this;
        }

        /// <summary>
        /// Add a simulation subsystem. It declares its own phase and order, so the sequence of
        /// these calls cannot change behaviour — which is what makes a composition root safe to
        /// extend from several places.
        /// </summary>
        public SimWorldBuilder AddSystem(Func<SimWorld, IWorldSystem> factory)
        {
            _systemFactories.Add(factory ?? throw new ArgumentNullException(nameof(factory)));
            return this;
        }

        /// <summary>
        /// Let a component own one kind of player command. The handler returns
        /// <see cref="IntentRejection.None"/> when it applied the intent, or the reason it did not.
        /// Handlers are consulted before the world's own switch, and a kind may be claimed once.
        /// </summary>
        public SimWorldBuilder AddIntentHandler(IntentKind kind, Func<Intent, IntentRejection> handler)
        {
            _intentHandlers.Add((kind, handler ?? throw new ArgumentNullException(nameof(handler))));
            return this;
        }

        /// <summary>
        /// Let something outside the simulation assembly scan for work. Givers that live *inside*
        /// it need no call at all: <see cref="Pawns.WorkGiverRegistry"/> finds them, which is the
        /// point of that class. This is for the rest — a test's giver, an editor tool's, one day a
        /// mod's.
        ///
        /// <para>The call order does not matter. The colony reads this list inside
        /// <see cref="Build"/>, after every registration has been made, and the job pipeline sorts
        /// the whole set by work type; a giver added last still scans where the Defs put it.</para>
        /// </summary>
        public SimWorldBuilder AddWorkGiver(Pawns.WorkGiver giver)
        {
            _workGivers.Add(giver ?? throw new ArgumentNullException(nameof(giver)));
            return this;
        }

        /// <summary>The givers registered so far. Read by the colony composition at build time.</summary>
        public IReadOnlyList<Pawns.WorkGiver> WorkGivers => _workGivers;

        public SimWorld Build()
        {
            var world = new SimWorld(_seed, _size);
            foreach (var (kind, handler) in _intentHandlers) world.SetIntentHandler(kind, handler);
            foreach (var factory in _factories) world.Register(factory(world));
            world.SetSnapshotContributors(_contributors.ToArray());
            world.SetHashables(_hashables.ToArray());

            var systems = new List<IWorldSystem>();
            foreach (var factory in _systemFactories) systems.Add(factory(world));
            world.SetSystems(new WorldSystemSchedule(systems));
            return world;
        }
    }
}
