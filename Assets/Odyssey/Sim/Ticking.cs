#nullable enable
using System;

namespace Odyssey.Sim
{
    /// <summary>
    /// How often a thing ticks. The value <em>is</em> the interval in ticks, so callers never
    /// need a switch to convert.
    ///
    /// The model is RimWorld's, and the reason it works is worth stating: simulation cost scales
    /// with the number of <em>ticking</em> things, not with the number of cells. A 2.5-million
    /// cell map is cheap; twenty thousand things all ticking every tick would not be. Almost
    /// everything built into the world ticks <see cref="Never"/>.
    /// </summary>
    public enum TickGroup
    {
        Never = 0,
        Normal = 1,
        Rare = 250,
        Long = 2000,
    }

    /// <summary>Anything the world ticks.</summary>
    public interface ITickable
    {
        TickGroup TickGroup { get; }

        /// <summary>
        /// A stable per-thing value used to spread work across ticks. Two things in the same
        /// group with different offsets tick on different ticks, so a population of 20,000 rare
        /// tickers costs 80 per tick rather than 20,000 every 250th tick. It must be stable
        /// across a save and load, or the load would shift every thing into a different phase.
        /// </summary>
        int TickPhaseOffset { get; }

        void Tick(SimWorld world);
    }

    /// <summary>Contributes to the world state hash. Caches and derived data must not implement this.</summary>
    public interface IStateHashable
    {
        void ContributeTo(ref Contracts.StateHash hash);
    }
}
