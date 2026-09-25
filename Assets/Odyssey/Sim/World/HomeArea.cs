#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.World
{
    /// <summary>
    /// The colony's home (design 43 §3): every cell the colony has put something on, grown by
    /// <see cref="Perimeter"/> cells as a square on its own layer, and then one layer up and down —
    /// <b>and of that, only the piece joined to the hearth</b> (§3f). Two pieces are joined when
    /// their grown areas touch, face to face on a layer or one above the other, which is buildings
    /// about eleven cells apart; anything further is an outpost and is not home. No hearth, no
    /// home.
    ///
    /// <para><b>Derived, never painted, never saved, never hashed.</b> Every source is saved and
    /// hashed already — the floors, the edifice records, the build sites, the zones and the power
    /// lines — so the home is a pure function of the hashed world, and a golden cannot move because
    /// of it. That is also why this is not a world system: it has nothing to tick.</para>
    ///
    /// <para><b>Rebuilt lazily, per dirty layer</b> (§3c). A placement touches its layer in
    /// <see cref="ColonyFootprint"/>; the first question asked afterwards rebuilds that layer and
    /// the two beside it, then answers. A cadence was rejected: a rebuild every N ticks has a
    /// phase, so a world saved between a placement and the next rebuild would, once loaded, have a
    /// different home from its still-running twin until the cadence caught up. Answering from the
    /// world as it is at the moment of asking has no phase to disagree about.</para>
    ///
    /// <para><b>What it scales with.</b> At rest, a query is two array reads. A rebuild costs one
    /// pass over each dirty layer — the seed scan reads every cell's floor and edifice, because
    /// there is no sparse list of built floors — plus the colony's site, zone and line lists. It
    /// runs only after something was placed or removed, never on a clock, and felling, mining and
    /// walking never cause one. Measured by <c>TickBenchmarkTests.WhatOneHomeRebuildCosts</c>.</para>
    ///
    /// <para><b>The one owner of "what counts as placed"</b> is <see cref="SeedLayer"/>. Design 43
    /// §3a is the list and the reasons. Shelves are edifices and are counted as edifices; the
    /// ruined city's walls are edifices with <c>Built</c> false and never count.</para>
    /// </summary>
    public sealed class HomeArea
    {
        /// <summary>How far home reaches from anything placed, in cells, as a square (owner, 2026-09-24).</summary>
        public const int Perimeter = 5;

        readonly PawnContext _ctx;
        readonly GridSize _size;
        readonly ColonyFootprint _footprint;

        /// <summary>Per cell: grown from a seed on its own layer. The vertical margin is not in here.</summary>
        readonly bool[] _grown;

        /// <summary>Per cell: <see cref="_grown"/> on this layer or the one above or below — every piece.</summary>
        readonly bool[] _home;

        /// <summary>Per cell: home, the piece of <see cref="_home"/> joined to the hearth.</summary>
        readonly bool[] _joined;

        /// <summary>The cells of <see cref="_joined"/>, so the last answer is cleared in O(home) rather than O(board).</summary>
        readonly List<int> _joinedCells = new List<int>();

        /// <summary>Seeds found on each layer at its last rebuild.</summary>
        readonly int[] _seedsOn;

        /// <summary>Scratch for one layer: seeded, then grown along x.</summary>
        readonly bool[] _seed, _alongX;

        /// <summary>Scratch: which layers the current rebuild must recompute home on.</summary>
        readonly bool[] _redoHome;

        int _seedTotal;

        public HomeArea(PawnContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _size = ctx.Size;
            _footprint = ctx.Cells.Footprint;
            int count = _size.CellCount;
            _grown = new bool[count];
            _home = new bool[count];
            _joined = new bool[count];
            _seedsOn = new int[Math.Max(1, _size.SizeY)];
            _seed = new bool[_size.LayerStride];
            _alongX = new bool[_size.LayerStride];
            _redoHome = new bool[Math.Max(1, _size.SizeY)];
        }

        /// <summary>Bumped by every rebuild, so a publisher can tell a home it has already sent.</summary>
        public int Version { get; private set; }

        /// <summary>
        /// Nothing placed anywhere. An empty home restricts nobody (design 43 §4d): a new colony
        /// has no beds and no stockpile, and a colonist kept home on the first morning would
        /// otherwise have nothing she may do.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                Refresh();
                return _joinedCells.Count == 0;
            }
        }

        /// <summary>Is this cell home? Off the board is not.</summary>
        public bool Contains(int cell)
        {
            if ((uint)cell >= (uint)_size.CellCount) return false;
            Refresh();
            return _joined[cell];
        }

        /// <summary>How many cells are home, on every layer. For tests and the benchmark.</summary>
        public int CellCount
        {
            get
            {
                Refresh();
                return _joinedCells.Count;
            }
        }

        /// <summary>
        /// Which of this cell's four sides border a cell on the same layer that is not home, as
        /// bits: 1 west (-x), 2 east (+x), 4 south (-z), 8 north (+z). Nought for a cell that is
        /// not home or has home on every side. The board's own edge counts as not home.
        /// </summary>
        public byte EdgesOf(int cell)
        {
            if (!Contains(cell)) return 0;
            int stride = _size.LayerStride, sx = _size.SizeX;
            int inLayer = cell % stride;
            int x = inLayer % sx, z = inLayer / sx;
            byte edges = 0;
            if (x == 0 || !_joined[cell - 1]) edges |= 1;
            if (x == sx - 1 || !_joined[cell + 1]) edges |= 2;
            if (z == 0 || !_joined[cell - sx]) edges |= 4;
            if (z == _size.SizeZ - 1 || !_joined[cell + sx]) edges |= 8;
            return edges;
        }

        /// <summary>
        /// Rebuild whatever is dirty now and say how many layers were grown again. The queries do
        /// this themselves; this is for the benchmark and the tests that count the work.
        /// </summary>
        public int Rebuild()
        {
            if (!_footprint.AnyDirty) return 0;

            int layers = _size.SizeY, grown = 0;
            Array.Clear(_redoHome, 0, _redoHome.Length);
            for (int y = 0; y < layers; y++)
            {
                if (!_footprint.IsDirty(y)) continue;
                GrowLayer(y);
                grown++;
                for (int d = -1; d <= 1; d++)
                    if ((uint)(y + d) < (uint)layers) _redoHome[y + d] = true;
            }

            for (int y = 0; y < layers; y++)
                if (_redoHome[y]) ComposeLayer(y);

            _seedTotal = 0;
            for (int y = 0; y < layers; y++) _seedTotal += _seedsOn[y];

            JoinToHearth();

            _footprint.ClearDirty();
            Version++;
            return grown;
        }

        /// <summary>
        /// Keep only the piece of home the hearth stands in: a flood from its cell over
        /// <see cref="_home"/>, four ways on a layer and one up and one down. O(home cells), plus
        /// clearing the last answer through its own list.
        /// </summary>
        void JoinToHearth()
        {
            for (int i = 0; i < _joinedCells.Count; i++) _joined[_joinedCells[i]] = false;
            _joinedCells.Clear();

            int hearth = _ctx.Hearth?.Cell ?? -1;
            if ((uint)hearth >= (uint)_size.CellCount || !_home[hearth]) return;

            int stride = _size.LayerStride, sx = _size.SizeX, sz = _size.SizeZ, count = _size.CellCount;
            _joined[hearth] = true;
            _joinedCells.Add(hearth);
            for (int head = 0; head < _joinedCells.Count; head++)
            {
                int c = _joinedCells[head];
                int inLayer = c % stride;
                int x = inLayer % sx, z = inLayer / sx;
                if (x > 0) Join(c - 1);
                if (x < sx - 1) Join(c + 1);
                if (z > 0) Join(c - sx);
                if (z < sz - 1) Join(c + sx);
                if (c >= stride) Join(c - stride);
                if (c + stride < count) Join(c + stride);
            }
        }

        void Join(int c)
        {
            if (!_home[c] || _joined[c]) return;
            _joined[c] = true;
            _joinedCells.Add(c);
        }

        void Refresh()
        {
            if (_footprint.AnyDirty) Rebuild();
        }

        /// <summary>Seed one layer and grow it by the perimeter, square: along x, then along z.</summary>
        void GrowLayer(int y)
        {
            int stride = _size.LayerStride, sx = _size.SizeX, sz = _size.SizeZ;
            int baseCell = y * stride;

            _seedsOn[y] = SeedLayer(y, _seed);

            if (_seedsOn[y] == 0)
            {
                Array.Clear(_grown, baseCell, stride);
                return;
            }

            // Along x: a cell is reached if any seed in its row lies within Perimeter of it. A
            // running count over a window of 2 * Perimeter + 1, so the pass is linear in the row.
            for (int z = 0; z < sz; z++)
            {
                int row = z * sx, inWindow = 0;
                for (int x = 0; x < Math.Min(Perimeter, sx); x++) if (_seed[row + x]) inWindow++;
                for (int x = 0; x < sx; x++)
                {
                    int enter = x + Perimeter, leave = x - Perimeter - 1;
                    if (enter < sx && _seed[row + enter]) inWindow++;
                    if (leave >= 0 && _seed[row + leave]) inWindow--;
                    _alongX[row + x] = inWindow > 0;
                }
            }

            // Along z, over what the x pass reached: together a square of side 2 * Perimeter + 1.
            for (int x = 0; x < sx; x++)
            {
                int inWindow = 0;
                for (int z = 0; z < Math.Min(Perimeter, sz); z++) if (_alongX[z * sx + x]) inWindow++;
                for (int z = 0; z < sz; z++)
                {
                    int enter = z + Perimeter, leave = z - Perimeter - 1;
                    if (enter < sz && _alongX[enter * sx + x]) inWindow++;
                    if (leave >= 0 && _alongX[leave * sx + x]) inWindow--;
                    _grown[baseCell + z * sx + x] = inWindow > 0;
                }
            }
        }

        /// <summary>Home on a layer: grown on it, or on the layer directly above or below (§3b).</summary>
        void ComposeLayer(int y)
        {
            int stride = _size.LayerStride, baseCell = y * stride;
            bool below = y > 0, above = y < _size.SizeY - 1;
            for (int i = 0; i < stride; i++)
            {
                int c = baseCell + i;
                _home[c] = _grown[c]
                           || (below && _grown[c - stride])
                           || (above && _grown[c + stride]);
            }
        }

        /// <summary>
        /// Mark every cell on this layer the colony has placed something on, into
        /// <paramref name="seed"/> (indexed within the layer), and count them. <b>The one owner of
        /// what counts</b> (design 43 §3a).
        /// </summary>
        int SeedLayer(int y, bool[] seed)
        {
            Array.Clear(seed, 0, seed.Length);
            int stride = _size.LayerStride, baseCell = y * stride, count = 0;
            CellGrid cells = _ctx.Cells;

            // Our floors and paving, and our edifices, read off the grid rather than off the
            // record list: a collapse takes a floor without telling anybody, and the grid is what
            // is actually standing. Built is what separates ours from the ruined city's.
            IReadOnlyList<PlacedEdifice>? records = _ctx.Construction?.Edifices.Records;
            for (int i = 0; i < stride; i++)
            {
                int c = baseCell + i;
                bool placed = Construction.ConstructionContent.IsOurs(cells.Floor[c]);
                if (!placed && records != null)
                {
                    int handle = cells.Edifice[c];
                    if (handle >= 0 && handle < records.Count)
                    {
                        PlacedEdifice record = records[handle];
                        placed = record.Built && !record.Removed;
                    }
                }
                if (placed) { seed[i] = true; count++; }
            }

            // The lists: a site not yet raised, a stockpile or field cell, a line or line order.
            if (_ctx.Construction != null) count += SeedFrom(_ctx.Construction.Sites, baseCell, stride, seed);
            if (_ctx.Storage != null) count += SeedFrom(_ctx.Storage.Cells, baseCell, stride, seed);
            if (_ctx.Growing != null) count += SeedFrom(_ctx.Growing.Cells, baseCell, stride, seed);
            if (_ctx.Power != null)
            {
                count += SeedFrom(_ctx.Power.Lines, baseCell, stride, seed);
                count += SeedFrom(_ctx.Power.Sites, baseCell, stride, seed);
            }
            return count;
        }

        /// <summary>Seed the cells of a list that lie on this layer; count the ones not already seeded.</summary>
        static int SeedFrom(IReadOnlyList<int> list, int baseCell, int stride, bool[] seed)
        {
            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                int local = list[i] - baseCell;
                if ((uint)local >= (uint)stride || seed[local]) continue;
                seed[local] = true;
                count++;
            }
            return count;
        }
    }
}
