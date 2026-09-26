#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Expeditions
{
    /// <summary>
    /// What the colony knows of the planet (design 64 §5): one bit per tile, set when somebody has
    /// seen it. It hides <i>places</i>, never land — the player chose their site from the whole
    /// planet on the World screen, so the biomes are known from the start.
    /// </summary>
    public sealed class Chart
    {
        readonly bool[] _known;
        readonly List<int> _scratch = new List<int>();

        public int Width { get; }
        public int Height { get; }

        public Chart(int width, int height)
        {
            Width = width;
            Height = height;
            _known = new bool[width * height];
        }

        public bool Knows(int tile) => (uint)tile < (uint)_known.Length && _known[tile];

        /// <summary>How many tiles are charted.</summary>
        public int Count
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _known.Length; i++) if (_known[i]) n++;
                return n;
            }
        }

        /// <summary>Chart every tile within <paramref name="radius"/> of <paramref name="centre"/>. Returns how many were new.</summary>
        public int Reveal(int centre, int radius)
        {
            HexMath.Within(centre, radius, Width, Height, _scratch);
            int fresh = 0;
            for (int i = 0; i < _scratch.Count; i++)
            {
                if (_known[_scratch[i]]) continue;
                _known[_scratch[i]] = true;
                fresh++;
            }
            return fresh;
        }

        /// <summary>The bits packed eight to a byte, tile 0 in the lowest bit, for a save.</summary>
        public byte[] Pack()
        {
            var bytes = new byte[(_known.Length + 7) / 8];
            for (int i = 0; i < _known.Length; i++) if (_known[i]) bytes[i >> 3] |= (byte)(1 << (i & 7));
            return bytes;
        }

        /// <summary>The reader of <see cref="Pack"/>.</summary>
        public void Unpack(byte[] bytes)
        {
            for (int i = 0; i < _known.Length; i++)
                _known[i] = (i >> 3) < bytes.Length && (bytes[i >> 3] & (1 << (i & 7))) != 0;
        }

        internal void ContributeTo(ref StateHash hash)
        {
            hash.Add(Count);
            for (int i = 0; i < _known.Length; i++) if (_known[i]) hash.Add(i);
        }
    }
}
