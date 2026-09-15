#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Sim
{
    /// <summary>
    /// Where in the tick a system runs. The values are the phase numbers from
    /// docs/design/01-architecture.md section 3, and that order is part of the determinism
    /// contract rather than an implementation detail.
    ///
    /// Phases 1 and 6 (intents in, snapshot out) are not open to systems: they are the seam, and
    /// exactly one thing owns each end.
    /// </summary>
    public enum TickPhase
    {
        /// <summary>2. Grid propagation, support solving, region rebuild. The world settling.</summary>
        WorldSystems = 2,

        /// <summary>3. Things, by tick group. Owned by the tick-group dispatcher.</summary>
        Things = 3,

        /// <summary>4. Pawns: needs, think tree, job execution, movement.</summary>
        Pawns = 4,
    }

    /// <summary>
    /// A simulation subsystem: something that runs every tick, in a known phase, in a known order.
    ///
    /// This exists because the alternative is what the project had until now — a tick method with
    /// comments where the systems should be, and subsystems written as libraries that nothing
    /// called. Registration makes the architecture real: the phase order is declared rather than
    /// implied, ordering within a phase is explicit rather than incidental, and a new system
    /// cannot be added without saying when it runs.
    ///
    /// Note on vocabulary: the simulation has **systems**; the interface layer has **directors**
    /// (`SliceDirector`, `ToolDirector` and the rest, owned by the UI line of work). They are not
    /// the same pattern and deliberately do not share a name — a director coordinates presentation
    /// state and reads a snapshot, a system mutates the world inside a tick.
    /// </summary>
    public interface IWorldSystem
    {
        /// <summary>Stable name, used for ordering ties and for error messages.</summary>
        string Name { get; }

        TickPhase Phase { get; }

        /// <summary>
        /// Order within the phase, ascending. Systems that must observe a settled world run
        /// later. Ties break on <see cref="Name"/>, so ordering never depends on registration
        /// accident or on dictionary iteration.
        /// </summary>
        int Order { get; }

        void Tick(SimWorld world);
    }

    /// <summary>
    /// Systems for one phase, frozen in a deterministic order at world construction.
    ///
    /// Sorting once at build time rather than per tick is deliberate: the tick has roughly 5.5 ms
    /// on the target machine and must not spend any of it deciding what to run.
    /// </summary>
    public sealed class WorldSystemSchedule
    {
        readonly IWorldSystem[] _worldSystems;
        readonly IWorldSystem[] _pawnSystems;

        internal WorldSystemSchedule(IEnumerable<IWorldSystem> systems)
        {
            var world = new List<IWorldSystem>();
            var pawns = new List<IWorldSystem>();

            foreach (var system in systems)
            {
                switch (system.Phase)
                {
                    case TickPhase.WorldSystems: world.Add(system); break;
                    case TickPhase.Pawns: pawns.Add(system); break;
                    case TickPhase.Things:
                        throw new ArgumentException(
                            $"System '{system.Name}' asked for the Things phase, which belongs to the " +
                            "tick-group dispatcher. Register an ITickable instead.");
                    default:
                        throw new ArgumentOutOfRangeException(nameof(systems), $"Unknown phase for '{system.Name}'.");
                }
            }

            Sort(world);
            Sort(pawns);
            _worldSystems = world.ToArray();
            _pawnSystems = pawns.ToArray();
        }

        static void Sort(List<IWorldSystem> systems) =>
            systems.Sort((a, b) =>
            {
                int byOrder = a.Order.CompareTo(b.Order);
                return byOrder != 0 ? byOrder : string.CompareOrdinal(a.Name, b.Name);
            });

        public IReadOnlyList<IWorldSystem> WorldSystems => _worldSystems;
        public IReadOnlyList<IWorldSystem> PawnSystems => _pawnSystems;

        internal void RunWorldSystems(SimWorld world)
        {
            for (int i = 0; i < _worldSystems.Length; i++) _worldSystems[i].Tick(world);
        }

        internal void RunPawnSystems(SimWorld world)
        {
            for (int i = 0; i < _pawnSystems.Length; i++) _pawnSystems[i].Tick(world);
        }

        public static WorldSystemSchedule Empty { get; } = new WorldSystemSchedule(Array.Empty<IWorldSystem>());
    }
}
