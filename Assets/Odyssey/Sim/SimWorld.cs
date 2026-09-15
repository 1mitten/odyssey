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
        readonly List<ITickable>[] _byGroup;
        readonly List<Action<SimWorld>> _deferred = new List<Action<SimWorld>>();
        ISnapshotContributor[] _contributors = Array.Empty<ISnapshotContributor>();

        internal SimWorld(uint seed, GridSize size)
        {
            Seed = seed;
            Size = size;
            CurrentTick = 0;
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

        /// <summary>0 paused, 1 normal, 2 fast, 3 very fast. A tick-rate multiplier, never a delta.</summary>
        public int GameSpeed { get; private set; } = 1;

        /// <summary>Ticks elapsed. Starts at 0 and is part of the state hash.</summary>
        public int CurrentTick { get; private set; }

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

        /// <summary>Advance exactly one tick, in the fixed phase order.</summary>
        public void Tick()
        {
            // 1. Intents from the UI, in submission order.
            Intents.Drain(HandleIntent);

            // 2. World systems — grid propagation, support solving, region rebuild.
            // 3. Things, by tick group.
            TickGroupMembers(_byGroup[0], (int)TickGroup.Normal);
            TickGroupMembers(_byGroup[1], (int)TickGroup.Rare);
            TickGroupMembers(_byGroup[2], (int)TickGroup.Long);

            // 4. Pawns. (U19 onwards.)

            // 5. Deferred structural events, applied at one point.
            if (_deferred.Count > 0)
            {
                // Snapshot first: an action may defer more work, which belongs to the next tick.
                var toRun = _deferred.ToArray();
                _deferred.Clear();
                foreach (var action in toRun) action(this);
            }

            // 6. Publish the snapshot, after every system has finished mutating the world.
            Views.Publish(this, _contributors);

            CurrentTick++;
        }

        /// <summary>
        /// Apply one player command, or say why not. Intents that a milestone has not implemented
        /// yet are rejected explicitly rather than ignored, so a command never silently does
        /// nothing.
        /// </summary>
        IntentRejection HandleIntent(Intent intent)
        {
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

                default:
                    return IntentRejection.UnknownIntent;
            }
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
            for (int i = 0; i < _tickables.Count; i++)
                if (_tickables[i] is IStateHashable hashable)
                    hashable.ContributeTo(ref hash);
            return hash;
        }

        internal void SetSnapshotContributors(ISnapshotContributor[] contributors) => _contributors = contributors;

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

        public SimWorld Build()
        {
            var world = new SimWorld(_seed, _size);
            foreach (var factory in _factories) world.Register(factory(world));
            world.SetSnapshotContributors(_contributors.ToArray());
            return world;
        }
    }
}
