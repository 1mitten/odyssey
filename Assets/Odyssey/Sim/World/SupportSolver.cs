#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.World
{
    /// <summary>
    /// Structural support and collapse — the mechanic the vertical slice exists to prove.
    /// Specified by docs/design/02-world-and-layers.md section 4.
    ///
    /// <para><b>The rule.</b> Support is a small integer per cell, computed bottom-up, layer by
    /// layer:</para>
    /// <list type="bullet">
    /// <item>a slab resting directly on solid ground, natural rock, or a wall or pillar in the
    /// cell below has support <c>S_max</c>;</item>
    /// <item>otherwise it takes the highest support among its four horizontal neighbours on the
    /// same boundary, minus one, floored at zero;</item>
    /// <item>a slab at support 0 is unsupported and collapses.</item>
    /// </list>
    ///
    /// <para><b>Layers are independent.</b> Layer <c>y+1</c> never reads a support <i>value</i>
    /// from layer <c>y</c>; it only asks whether something solid stands in the cell beneath. That
    /// is what makes the solve a sequence of independent 2D problems rather than one 3D one, and
    /// it is why an edit on layer <c>y</c> can only ever disturb layers <c>y</c> and
    /// <c>y+1</c>.</para>
    ///
    /// <para><b>Where it runs.</b> Nowhere inline. Support is resolved once per tick from the
    /// world-systems phase, and the collapses it reports are applied through
    /// <see cref="SimWorld.Defer"/> — mutating the world while a scan is walking it is the
    /// classic source of both crashes and desyncs.</para>
    ///
    /// <para><b>Determinism.</b> Every queue is a flat array walked front to back, every scan is
    /// in ascending cell-index order, and neighbours are always visited -x, +x, -z, +z. There is
    /// no dictionary, no set, no LINQ and no allocation on any solve path after construction.</para>
    /// </summary>
    public class SupportSolver
    {
        /// <summary>S_max for early materials. A material constant in the end; a parameter now.</summary>
        public const int DefaultMaxSupport = 4;

        readonly CellGrid _grid;
        readonly GridSize _size;
        readonly int _sizeX;
        readonly int _stride;
        readonly int _cellCount;
        readonly int _sMax;

        /// <summary>
        /// Slabs that worldgen stamped already standing. Held here rather than in
        /// <see cref="CellFlags"/> so that the cell record stays the authoritative, saved state
        /// and this stays what it is: an input to a derived calculation.
        /// </summary>
        readonly bool[] _byConstruction;

        // --- dirty tracking -------------------------------------------------------------
        int[] _dirty = new int[256];
        int _dirtyCount;
        int[] _touched = new int[512];
        int _touchedCount;

        // --- per-layer scratch, allocated once ------------------------------------------
        readonly bool[] _erasedMark;     // layer-local: support erased during this pass
        readonly bool[] _sourceMark;     // layer-local: kept its value, will re-seed propagation
        readonly int[] _eraseIdx;        // erase wave, FIFO; a cell is erased at most once
        readonly int[] _eraseVal;        // the value each erased cell held before erasure
        int _eraseCount;
        readonly int[] _sources;
        int _sourceCount;
        int[] _collapseScratch = new int[64];
        int _collapseScratchCount;

        // A bucket queue keyed by support value. Because a cell at value v can only ever hand
        // v-1 to a neighbour, processing buckets from S_max downwards finalises every cell on
        // first visit: it is Dijkstra with unit weights and a tiny integer range, which is the
        // O(n) form of "iterate the layer until values stop changing".
        readonly int[][] _bucket;
        readonly int[] _bucketCount;

        readonly List<CellRef> _collapsed = new List<CellRef>();

        public SupportSolver(CellGrid grid, int maxSupport = DefaultMaxSupport)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (maxSupport < 1 || maxSupport > byte.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(maxSupport));

            _size = grid.Size;
            _sizeX = _size.SizeX;
            _stride = _size.LayerStride;
            _cellCount = _size.CellCount;
            _sMax = maxSupport;

            _byConstruction = new bool[_cellCount];
            _erasedMark = new bool[_stride];
            _sourceMark = new bool[_stride];
            _eraseIdx = new int[_stride];
            _eraseVal = new int[_stride];
            _sources = new int[_stride];

            _bucket = new int[_sMax + 1][];
            _bucketCount = new int[_sMax + 1];
            for (int level = 0; level <= _sMax; level++) _bucket[level] = new int[256];
        }

        /// <summary>The support a slab gets when something solid holds it up directly.</summary>
        public int MaxSupport => _sMax;

        /// <summary>Cells awaiting an incremental solve.</summary>
        public int DirtyCount => _dirtyCount;

        /// <summary>
        /// What support a slab would have if one were built here — asked before it is, and
        /// changing nothing.
        ///
        /// <para><b>This is what makes the support rule playable rather than punitive.</b> Without
        /// it a colony can order a slab anywhere, carry the material across the map, build it, and
        /// watch it fall on the tick it is finished. With it the order is refused where it could
        /// not stand, which is the argument <c>ConstructionGrid.Allows</c> already makes about a
        /// wall hanging in the air: the player never gives an order that cannot be carried out.</para>
        ///
        /// <para>The rule, unchanged from the solver's own: full support over something solid,
        /// otherwise the best of the four horizontal neighbours on this boundary minus one. So a
        /// colonist bridges out from a wall for <see cref="MaxSupport"/> minus one cells and the
        /// next one is refused — the overhang is not a number anybody tuned, it is the rule seen
        /// from the side.</para>
        ///
        /// <para>Reads <see cref="CellGrid.Support"/> for the neighbours, which is the settled
        /// value from the last solve. That is the right thing to read and not a shortcut: support
        /// is resolved once per tick, so during a tick every neighbour's value is the one the
        /// world agrees on.</para>
        /// </summary>
        public int SupportIfSlabAt(int index)
        {
            if ((uint)index >= (uint)_cellCount) return 0;
            if (IsGrounded(index)) return _sMax;

            GridSize size = _grid.Size;
            CellRef at = size.FromIndex(index);
            int best = 0;

            if (at.X > 0) best = Higher(best, index - 1);
            if (at.X < size.SizeX - 1) best = Higher(best, index + 1);
            if (at.Z > 0) best = Higher(best, index - size.SizeX);
            if (at.Z < size.SizeZ - 1) best = Higher(best, index + size.SizeX);

            return best > 0 ? best - 1 : 0;
        }

        int Higher(int best, int neighbour)
        {
            int value = _grid.Support[neighbour];
            return value > best ? value : best;
        }

        /// <summary>
        /// What the last solve brought down, in ascending cell index order. The list is reused:
        /// consume it before the next solve. Collapse clears the slab and nothing else — rubble,
        /// falling things and fall damage belong to the caller, applied deferred.
        /// </summary>
        public IReadOnlyList<CellRef> Collapsed => _collapsed;

        // ------------------------------------------------------------------------------------
        // The rule, in one place.
        // ------------------------------------------------------------------------------------
        //
        // These three predicates are deliberately not virtual. The convention is "virtual where
        // cheap"; at the scale target they are called roughly ten million times per full solve,
        // so here it is not cheap. Subclasses override SolveFull/SolveIncremental instead.

        /// <summary>
        /// Does the cell below hold this one up? Below the bottom layer is bedrock: the world has
        /// a floor, and assuming otherwise would collapse it on the first tick.
        /// </summary>
        protected bool IsGrounded(int index)
        {
            int below = index - _stride;
            if (below < 0) return true;
            var flags = _grid.Flags[below];
            if ((flags & CellFlags.SolidTerrain) != 0) return true;
            if ((flags & CellFlags.BlockingEdifice) != 0) return true;
            return _grid.Edifice[below] >= 0;
        }

        /// <summary>A cell that is a source of full support: something solid is directly beneath
        /// it, or worldgen stamped its slab already standing.</summary>
        protected bool IsBase(int index) =>
            IsGrounded(index) || (_byConstruction[index] && _grid.Floor[index] != 0);

        /// <summary>
        /// Can load cross this cell? A slab can, natural rock can, and so can open ground — the
        /// top of a rock column or a wall carries load sideways to the slab beside it. A hole
        /// cannot, which is why support never spans a missing slab.
        /// </summary>
        protected bool IsMedium(int index)
        {
            if (_grid.Floor[index] != 0) return true;
            if ((_grid.Flags[index] & CellFlags.SolidTerrain) != 0) return true;
            return IsGrounded(index);
        }

        // ------------------------------------------------------------------------------------
        // Marking
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Record that a cell's structure changed: a slab built or breached, a wall mined, rock
        /// removed. Marking is how the incremental path stays proportional to the edit rather
        /// than to the map.
        ///
        /// Marking also <b>revokes the supported-by-construction trust</b> on the cell and on the
        /// cell above it, which is the design's "validated by the ordinary rule the moment
        /// anything changes beneath them". A pre-war slab keeps standing until someone disturbs
        /// what is under it; then it has to earn its support like anything else.
        /// </summary>
        public void MarkDirty(int index)
        {
            if ((uint)index >= (uint)_cellCount) throw new ArgumentOutOfRangeException(nameof(index));
            _byConstruction[index] = false;
            int above = index + _stride;
            if (above < _cellCount) _byConstruction[above] = false;
            Enqueue(index);
        }

        public void MarkDirty(CellRef cell) => MarkDirty(_grid.Index(cell));

        public void MarkDirty(int x, int z, int y) => MarkDirty(_grid.Index(x, z, y));

        /// <summary>
        /// Seed a slab that worldgen stamped already standing. It becomes a full-support source
        /// exactly like solid ground, so a shell can be stamped in any order without the
        /// half-built state collapsing, and so a pre-war long span keeps standing until someone
        /// touches what holds it up.
        ///
        /// Worldgen's consistency check is <see cref="ClearAllConstructionMarks"/> followed by
        /// <see cref="SolveFull"/>: a template that collapses with no trust left is a content bug.
        /// </summary>
        public void MarkSupportedByConstruction(int index, bool supported = true)
        {
            if ((uint)index >= (uint)_cellCount) throw new ArgumentOutOfRangeException(nameof(index));
            _byConstruction[index] = supported;
            // Enqueued without revoking: the mark itself is the change here.
            Enqueue(index);
        }

        public void MarkSupportedByConstruction(CellRef cell, bool supported = true) =>
            MarkSupportedByConstruction(_grid.Index(cell), supported);

        public void MarkSupportedByConstruction(int x, int z, int y, bool supported = true) =>
            MarkSupportedByConstruction(_grid.Index(x, z, y), supported);

        public bool IsSupportedByConstruction(int index) => _byConstruction[index];

        public bool IsSupportedByConstruction(CellRef cell) => _byConstruction[_grid.Index(cell)];

        /// <summary>Drop every stamped-by-construction trust. The next solve judges the map on
        /// the ordinary rule alone.</summary>
        public void ClearAllConstructionMarks() => Array.Clear(_byConstruction, 0, _byConstruction.Length);

        /// <summary>
        /// Rebuild the dirty list from <see cref="CellFlags.SupportDirty"/> across the whole map,
        /// for the load path and for systems that set the flag without going through
        /// <see cref="MarkDirty"/>. It is a full scan, so it is not for every tick.
        ///
        /// A flag found here means the same thing as a call to <see cref="MarkDirty"/> and is
        /// treated identically, revocation included. Two entry points that mean subtly different
        /// things is how this kind of cache goes quietly wrong.
        /// </summary>
        public void ScanForDirty()
        {
            _dirtyCount = 0;
            for (int i = 0; i < _cellCount; i++)
            {
                if ((_grid.Flags[i] & CellFlags.SupportDirty) == 0) continue;
                _byConstruction[i] = false;
                int above = i + _stride;
                if (above < _cellCount) _byConstruction[above] = false;
                Append(i);
            }
        }

        void Enqueue(int index)
        {
            if ((_grid.Flags[index] & CellFlags.SupportDirty) != 0) return; // already queued
            _grid.Flags[index] |= CellFlags.SupportDirty;
            Append(index);
        }

        void Append(int index)
        {
            if (_dirtyCount == _dirty.Length) Array.Resize(ref _dirty, _dirty.Length * 2);
            _dirty[_dirtyCount++] = index;
        }

        // ------------------------------------------------------------------------------------
        // Full solve — the oracle
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Recompute support for every cell, bottom-up. Used on load, after worldgen, and as the
        /// oracle the incremental path is tested against; the two must agree exactly for any
        /// state, which is the single property that keeps the fast path honest.
        /// </summary>
        public virtual IReadOnlyList<CellRef> SolveFull()
        {
            _collapsed.Clear();
            _dirtyCount = 0;

            for (int y = 0; y < _size.SizeY; y++)
            {
                int layerBase = y * _stride;
                ResetBuckets();
                Array.Clear(_grid.Support, layerBase, _stride);

                for (int local = 0; local < _stride; local++)
                {
                    int i = layerBase + local;
                    _grid.Flags[i] &= ~CellFlags.SupportDirty;
                    if (!IsBase(i)) continue;
                    _grid.Support[i] = (byte)_sMax;
                    BucketPush(_sMax, i);
                }

                Propagate(layerBase);

                // A slab that no source reached is unsupported and comes down. There is no
                // cascade loop to run: a cell at 0 would hand -1 to a neighbour, so it was
                // holding nothing up, and clearing its slab cannot change any other value.
                // The cascade people see is the wave above, which strands a whole region at once.
                for (int local = 0; local < _stride; local++)
                {
                    int i = layerBase + local;
                    if (_grid.Floor[i] != 0 && _grid.Support[i] == 0) Collapse(i);
                }
            }

            return _collapsed;
        }

        // ------------------------------------------------------------------------------------
        // Incremental solve
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Re-solve only what the marked edits can have changed, and return what collapsed.
        /// Produces byte-identical results to <see cref="SolveFull"/> for the same world state.
        ///
        /// Two waves per affected layer. The first erases the support of every cell that could
        /// have derived from an edit, collecting the untouched cells at the boundary as they are
        /// met. The second re-propagates from those boundary cells and from any erased cell that
        /// is a source in its own right. Whatever is left at zero with a slab on it collapses.
        /// </summary>
        public virtual IReadOnlyList<CellRef> SolveIncremental()
        {
            _collapsed.Clear();
            if (_dirtyCount == 0) return _collapsed;

            BuildTouched();

            for (int k = 0; k < _dirtyCount; k++) _grid.Flags[_dirty[k]] &= ~CellFlags.SupportDirty;
            _dirtyCount = 0;

            // Ascending index order groups the touched cells by layer for free, and layers must
            // be solved bottom-up because layer y+1's sources depend on what stands on layer y.
            int start = 0;
            while (start < _touchedCount)
            {
                int layerBase = (_touched[start] / _stride) * _stride;
                int end = start;
                int layerEnd = layerBase + _stride;
                while (end < _touchedCount && _touched[end] < layerEnd) end++;
                SolveLayerIncremental(layerBase, start, end);
                start = end;
            }

            return _collapsed;
        }

        /// <summary>
        /// The affected set: each edited cell, plus the cell above it, whose source status
        /// depends on whether anything still stands beneath it. Sorted and de-duplicated so that
        /// every later step runs in index order.
        /// </summary>
        void BuildTouched()
        {
            int needed = _dirtyCount * 2;
            if (_touched.Length < needed) _touched = new int[Math.Max(needed, _touched.Length * 2)];

            _touchedCount = 0;
            for (int k = 0; k < _dirtyCount; k++)
            {
                int i = _dirty[k];
                _touched[_touchedCount++] = i;
                int above = i + _stride;
                if (above < _cellCount) _touched[_touchedCount++] = above;
            }

            Array.Sort(_touched, 0, _touchedCount);

            int write = 0;
            for (int read = 0; read < _touchedCount; read++)
            {
                if (read > 0 && _touched[read] == _touched[read - 1]) continue;
                _touched[write++] = _touched[read];
            }
            _touchedCount = write;
        }

        void SolveLayerIncremental(int layerBase, int from, int to)
        {
            ResetBuckets();
            _eraseCount = 0;
            _sourceCount = 0;

            // Wave one: erase. Seed with every touched cell, then walk outwards through cells
            // whose value is exactly one less than the cell that reached them — those are the
            // ones that may have derived their support from it. Anything holding a higher value
            // cannot have, so it survives and becomes a re-propagation source.
            for (int k = from; k < to; k++)
            {
                int i = _touched[k];
                int local = i - layerBase;
                if (_erasedMark[local]) continue;
                _erasedMark[local] = true;
                _eraseIdx[_eraseCount] = i;
                _eraseVal[_eraseCount] = _grid.Support[i];
                _eraseCount++;
                _grid.Support[i] = 0;
            }

            for (int head = 0; head < _eraseCount; head++)
            {
                int i = _eraseIdx[head];
                // The value this cell was handing to its neighbours. Neighbours holding exactly
                // this may have derived it here and are erased in turn; anything higher survives
                // and becomes a source. The walk happens even when the cell was handing out
                // nothing (child <= 0), because collecting the surviving neighbours is how an
                // erased cell gets its support back.
                int child = _eraseVal[head] - 1;

                int local = i - layerBase;
                int lx = local % _sizeX;
                if (lx > 0) Erase(i - 1, child, layerBase);
                if (lx < _sizeX - 1) Erase(i + 1, child, layerBase);
                if (local >= _sizeX) Erase(i - _sizeX, child, layerBase);
                if (local < _stride - _sizeX) Erase(i + _sizeX, child, layerBase);
            }

            // Wave two: re-seed and propagate. Erased cells that are sources in their own right
            // come back at full support; surviving boundary cells push their value back inwards.
            for (int k = 0; k < _eraseCount; k++)
            {
                int i = _eraseIdx[k];
                if (!IsBase(i)) continue;
                _grid.Support[i] = (byte)_sMax;
                BucketPush(_sMax, i);
            }

            for (int k = 0; k < _sourceCount; k++)
            {
                int s = _sources[k];
                int v = _grid.Support[s];
                if (v > 0) BucketPush(v, s);
            }

            Propagate(layerBase);

            // Only erased cells can have lost support, so only they can collapse. Sorted, so the
            // report is in the same index order a full solve would have produced.
            _collapseScratchCount = 0;
            for (int k = 0; k < _eraseCount; k++)
            {
                int i = _eraseIdx[k];
                if (_grid.Floor[i] == 0 || _grid.Support[i] != 0) continue;
                if (_collapseScratchCount == _collapseScratch.Length)
                    Array.Resize(ref _collapseScratch, _collapseScratch.Length * 2);
                _collapseScratch[_collapseScratchCount++] = i;
            }
            Array.Sort(_collapseScratch, 0, _collapseScratchCount);
            for (int k = 0; k < _collapseScratchCount; k++) Collapse(_collapseScratch[k]);

            for (int k = 0; k < _eraseCount; k++) _erasedMark[_eraseIdx[k] - layerBase] = false;
            for (int k = 0; k < _sourceCount; k++) _sourceMark[_sources[k] - layerBase] = false;
        }

        void Erase(int n, int child, int layerBase)
        {
            int local = n - layerBase;
            if (_erasedMark[local]) return;
            if (!IsMedium(n)) return;

            int value = _grid.Support[n];
            if (value == 0) return;

            if (value == child)
            {
                _erasedMark[local] = true;
                _eraseIdx[_eraseCount] = n;
                _eraseVal[_eraseCount] = value;
                _eraseCount++;
                _grid.Support[n] = 0;
                return;
            }

            if (_sourceMark[local]) return;
            _sourceMark[local] = true;
            _sources[_sourceCount++] = n;
        }

        // ------------------------------------------------------------------------------------
        // Shared propagation
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Drain the bucket queue from S_max downwards. Every cell is finalised the first time it
        /// is reached, because a cell at v can only ever offer v-1 and buckets are drained in
        /// descending order; a stale entry is recognised by its value no longer matching.
        /// </summary>
        void Propagate(int layerBase)
        {
            for (int level = _sMax; level >= 2; level--)
            {
                int count = _bucketCount[level];
                if (count == 0) continue;
                var bucket = _bucket[level];
                int child = level - 1;

                for (int k = 0; k < count; k++)
                {
                    int i = bucket[k];
                    if (_grid.Support[i] != level) continue;

                    int local = i - layerBase;
                    int lx = local % _sizeX;
                    if (lx > 0) Relax(i - 1, child);
                    if (lx < _sizeX - 1) Relax(i + 1, child);
                    if (local >= _sizeX) Relax(i - _sizeX, child);
                    if (local < _stride - _sizeX) Relax(i + _sizeX, child);
                }
            }
        }

        void Relax(int n, int value)
        {
            if (_grid.Support[n] >= value) return;
            if (!IsMedium(n)) return;
            _grid.Support[n] = (byte)value;
            BucketPush(value, n);
        }

        void ResetBuckets()
        {
            for (int level = 0; level <= _sMax; level++) _bucketCount[level] = 0;
        }

        void BucketPush(int level, int index)
        {
            var arr = _bucket[level];
            int n = _bucketCount[level];
            if (n == arr.Length)
            {
                Array.Resize(ref arr, arr.Length * 2);
                _bucket[level] = arr;
            }
            arr[n] = index;
            _bucketCount[level] = n + 1;
        }

        void Collapse(int index)
        {
            // A floor of ours falling is the colony's footprint shrinking (design 43 §3a); a
            // cavern's natural floor is not, and must not cost the home a rebuild.
            if (Construction.ConstructionContent.IsOurs(_grid.Floor[index])) _grid.Footprint.Touch(index);
            _grid.Floor[index] = 0;
            _grid.FloorStuff[index] = 0;
            _byConstruction[index] = false;
            _collapsed.Add(_size.FromIndex(index));
        }

        // ------------------------------------------------------------------------------------
        // Queries
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// What support a slab built here would have. This is the build preview's question, and
        /// it is deliberately not the same as <see cref="CellGrid.Support"/>: a cell with no slab
        /// and no ground beneath it stores 0, because load cannot cross a hole, but the answer to
        /// "could I build here" still has to look at the neighbours. 0 means it cannot be built.
        /// </summary>
        public int SupportIfBuilt(int index)
        {
            if (IsGrounded(index)) return _sMax;

            int layerBase = (index / _stride) * _stride;
            int local = index - layerBase;
            int lx = local % _sizeX;

            int best = 0;
            if (lx > 0) best = Max(best, _grid.Support[index - 1]);
            if (lx < _sizeX - 1) best = Max(best, _grid.Support[index + 1]);
            if (local >= _sizeX) best = Max(best, _grid.Support[index - _sizeX]);
            if (local < _stride - _sizeX) best = Max(best, _grid.Support[index + _sizeX]);

            return best > 0 ? best - 1 : 0;
        }

        public int SupportIfBuilt(CellRef cell) => SupportIfBuilt(_grid.Index(cell));

        static int Max(int a, int b) => a > b ? a : b;
    }
}
