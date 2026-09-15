#nullable enable
using System;

namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// FNV-1a 64-bit, the project's state hash. Every milestone gate compares one of these:
    /// same seed, same hash after N ticks; and save, load, resume must match an unbroken run.
    ///
    /// Integers are folded little-endian, so a hash is stable across machines. Floats are
    /// deliberately NOT supported: if a value cannot be hashed as an integer it should not be in
    /// simulation state. That rule is the cheapest defence against the float-accumulation class
    /// of desync, and making it a compile error is better than making it a review note.
    /// </summary>
    public struct StateHash
    {
        const ulong Offset = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;

        ulong _value;

        public static StateHash New() => new StateHash { _value = Offset };

        public ulong Value => _value;

        public void Add(byte b)
        {
            unchecked
            {
                _value ^= b;
                _value *= Prime;
            }
        }

        public void Add(bool b) => Add(b ? (byte)1 : (byte)0);

        public void Add(int v) => Add(unchecked((uint)v));

        public void Add(uint v)
        {
            Add((byte)(v & 0xFF));
            Add((byte)((v >> 8) & 0xFF));
            Add((byte)((v >> 16) & 0xFF));
            Add((byte)((v >> 24) & 0xFF));
        }

        public void Add(long v) => Add(unchecked((ulong)v));

        public void Add(ulong v)
        {
            Add((uint)(v & 0xFFFFFFFF));
            Add((uint)(v >> 32));
        }

        public void Add(CellRef cell)
        {
            Add(cell.X);
            Add(cell.Z);
            Add(cell.Y);
        }

        /// <summary>Sixteen lowercase hex digits, the form used in logs, reports and ADRs.</summary>
        public override string ToString() => _value.ToString("x16");
    }

    /// <summary>
    /// xorshift32. Small, fast, and identical on every platform because it is pure integer
    /// arithmetic — which is the only property that matters here.
    ///
    /// The simulation never holds a long-lived random stream. Streams are derived per tick from
    /// (world seed, tick) via <see cref="ForTick"/>, so no RNG state has to be saved, and a save
    /// taken mid-run resumes with exactly the stream an unbroken run would have had. This is the
    /// same trick RimWorld uses, and it removes a whole category of save/load desync.
    /// </summary>
    public struct DeterministicRandom
    {
        uint _state;

        public DeterministicRandom(uint seed)
        {
            _state = seed == 0u ? 1u : seed;
        }

        public uint State => _state;

        public uint NextUInt()
        {
            unchecked
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return _state;
            }
        }

        /// <summary>Uniform in [0, exclusiveMax).</summary>
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            return (int)(NextUInt() % (uint)exclusiveMax);
        }

        /// <summary>Uniform in [inclusiveMin, exclusiveMax).</summary>
        public int NextInt(int inclusiveMin, int exclusiveMax)
        {
            if (exclusiveMax <= inclusiveMin) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            return inclusiveMin + NextInt(exclusiveMax - inclusiveMin);
        }

        /// <summary>
        /// The stream for one tick. Mixing is a 32-bit avalanche so that adjacent ticks produce
        /// unrelated streams; without it, tick N and tick N+1 would start close together and the
        /// first few draws would correlate.
        /// </summary>
        public static DeterministicRandom ForTick(uint worldSeed, int tick)
        {
            unchecked
            {
                uint h = worldSeed ^ 2166136261u;
                h = (h ^ (uint)tick) * 16777619u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return new DeterministicRandom(h);
            }
        }

        /// <summary>
        /// A stream for one tick and one named purpose, so that adding a new consumer cannot
        /// shift the draws an existing one sees. Without this, inserting a system that draws
        /// random numbers silently changes every later system's results.
        /// </summary>
        public static DeterministicRandom ForTick(uint worldSeed, int tick, uint purpose)
        {
            unchecked
            {
                var r = ForTick(worldSeed ^ (purpose * 2654435761u), tick);
                return r;
            }
        }
    }
}
