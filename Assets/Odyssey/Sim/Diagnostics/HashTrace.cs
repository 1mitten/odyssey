#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Diagnostics
{
    /// <summary>
    /// Records the world's per-tick state hash for later comparison against another run. Off by
    /// default: nothing writes to one unless a caller wires <see cref="SimWorld.HashSink"/> to
    /// <see cref="Record"/>, so the normal path never pays for it.
    /// </summary>
    public sealed class HashTrace
    {
        readonly List<ulong> _hashes = new List<ulong>();

        public IReadOnlyList<ulong> Hashes => _hashes;

        /// <summary>Appends the hash for the given tick. Ticks must arrive in order from zero.</summary>
        public void Record(int tick, StateHash hash)
        {
            if (tick != _hashes.Count)
                throw new ArgumentOutOfRangeException(nameof(tick),
                    $"expected tick {_hashes.Count} next, got {tick}; a trace only records a contiguous run from tick 0");
            _hashes.Add(hash.Value);
        }

        /// <summary>
        /// The first tick at which two traces disagree, or null if every tick common to both
        /// agrees. A difference in length alone is not a divergence: it means one run stopped
        /// ticking, not that the two states parted company.
        /// </summary>
        public static int? FirstDivergence(HashTrace a, HashTrace b)
        {
            if (a is null) throw new ArgumentNullException(nameof(a));
            if (b is null) throw new ArgumentNullException(nameof(b));

            int shared = Math.Min(a._hashes.Count, b._hashes.Count);
            for (int tick = 0; tick < shared; tick++)
                if (a._hashes[tick] != b._hashes[tick]) return tick;
            return null;
        }
    }
}
