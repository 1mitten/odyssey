#nullable enable
using System;

namespace Odyssey.Sim.Diagnostics
{
    /// <summary>
    /// Something that wants the state hash at every tick boundary. Attached to
    /// <see cref="SimWorld.HashSink"/>, which is null in every ordinary run.
    ///
    /// The contract is one-way and read-only: a sink is handed a number after the tick has
    /// finished and must not touch the world. A sink that mutated anything would be changing the
    /// thing it is there to measure, and the divergence it then found would be its own.
    /// </summary>
    public interface ITickHashSink
    {
        /// <summary>The state after <paramref name="tick"/> finished running.</summary>
        void Record(int tick, ulong hash);
    }

    /// <summary>
    /// A recording of (tick, hash) pairs, and the tool that finds where two of them part.
    ///
    /// <para><b>What this is for.</b> A determinism failure is reported as "the hashes differ
    /// after 60,000 ticks", which says nothing about the cause. The tick where two runs first
    /// disagree is the one piece of information that turns that into a debuggable question,
    /// because the divergence is in something that ran on that tick. Recording a hash every tick
    /// is not free — see the cost printed by <c>HashTraceTests</c> — so this is off by default and
    /// switched on by the person doing the bisecting.</para>
    ///
    /// <para><b>Determinism.</b> Attaching a trace cannot change a run: it reads
    /// <see cref="SimWorld.ComputeStateHash"/>, which is a pure function of registered state, and
    /// writes only into its own arrays. Two same-seed worlds produce identical traces, which is
    /// itself one of the tests.</para>
    /// </summary>
    public sealed class HashTrace : ITickHashSink
    {
        /// <summary>Returned by <see cref="FirstDivergence"/> when two traces agree throughout.</summary>
        public const int NoDivergence = -1;

        int[] _ticks;
        ulong[] _hashes;

        public HashTrace(int capacity = 1024)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _ticks = new int[capacity];
            _hashes = new ulong[capacity];
        }

        public int Count { get; private set; }

        public int TickAt(int index)
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            return _ticks[index];
        }

        public ulong HashAt(int index)
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            return _hashes[index];
        }

        public void Record(int tick, ulong hash)
        {
            if (Count == _ticks.Length)
            {
                Array.Resize(ref _ticks, _ticks.Length * 2);
                Array.Resize(ref _hashes, _hashes.Length * 2);
            }
            _ticks[Count] = tick;
            _hashes[Count] = hash;
            Count++;
        }

        public void Clear() => Count = 0;

        /// <summary>
        /// The hash recorded for a tick, or false if the trace does not cover it. A linear scan
        /// from the end, because the caller is almost always asking about a recent tick and the
        /// alternative is a dictionary, which this deliberately does not have.
        /// </summary>
        public bool TryHashForTick(int tick, out ulong hash)
        {
            for (int i = Count - 1; i >= 0; i--)
            {
                if (_ticks[i] != tick) continue;
                hash = _hashes[i];
                return true;
            }
            hash = 0;
            return false;
        }

        /// <summary>
        /// The first tick at which two traces disagree, or <see cref="NoDivergence"/> if they
        /// agree everywhere both recorded.
        ///
        /// <para>Traces are walked in parallel and compared by tick, so two traces that started
        /// recording at different points still line up. A tick only one of them recorded proves
        /// nothing and is skipped: the answer is the first tick they both saw and saw
        /// differently.</para>
        ///
        /// <para>The tick returned is the one whose <i>execution</i> produced the difference, so
        /// it is the tick to re-run under a debugger, not the one after it.</para>
        /// </summary>
        public static int FirstDivergence(HashTrace a, HashTrace b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            int i = 0, j = 0;
            while (i < a.Count && j < b.Count)
            {
                int ta = a._ticks[i], tb = b._ticks[j];
                if (ta < tb) { i++; continue; }
                if (tb < ta) { j++; continue; }
                if (a._hashes[i] != b._hashes[j]) return ta;
                i++;
                j++;
            }

            return NoDivergence;
        }

        /// <summary>Do two traces agree on every tick they both recorded?</summary>
        public static bool Agree(HashTrace a, HashTrace b) => FirstDivergence(a, b) == NoDivergence;

        public override string ToString() =>
            Count == 0
                ? "trace (empty)"
                : $"trace of {Count} ticks, {_ticks[0]}..{_ticks[Count - 1]}, last hash {_hashes[Count - 1]:x16}";
    }
}
