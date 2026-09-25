#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.World
{
    /// <summary>
    /// What the rain stops at over a column (design 43 §6). <see cref="Open"/> is a column with
    /// nothing in it all the way down, which the drawing treats as ground at height nought.
    /// </summary>
    public enum SkyStop : byte
    {
        Open = 0,
        Ground = 1,
        Water = 2,
        Built = 3,
        Canopy = 4,
    }

    /// <summary>
    /// One column's answer: every cell on a layer below <see cref="StopLayer"/> is under cover,
    /// every cell at or above it is under the sky.
    ///
    /// <para>In layers rather than metres because the simulation has no metres. The drawing turns
    /// it into a height with its own constants (<c>SkyHeightMap</c>), which is why a canopy
    /// carries the layer its trunk stands in: the crown's height is measured from that floor.</para>
    /// </summary>
    public readonly struct SkyColumn : IEquatable<SkyColumn>
    {
        public readonly int StopLayer;
        public readonly SkyStop Kind;

        public SkyColumn(int stopLayer, SkyStop kind)
        {
            StopLayer = stopLayer;
            Kind = kind;
        }

        /// <summary>The layer the tree whose canopy this is stands in, for <see cref="SkyStop.Canopy"/>.</summary>
        public int TrunkLayer => StopLayer - SkyColumnRule.CanopyLayers;

        public bool Equals(SkyColumn other) => StopLayer == other.StopLayer && Kind == other.Kind;
        public override bool Equals(object? obj) => obj is SkyColumn other && Equals(other);
        public override int GetHashCode() => StopLayer * 8 + (int)Kind;
        public override string ToString() => $"{Kind}@{StopLayer}";
    }

    /// <summary>
    /// The four facts the column rule reads. The simulation answers them from the cell grid and
    /// the drawing from its render mirror, and both hand them to the same
    /// <see cref="SkyColumnRule.Compute{T}"/>, so the rule has one owner and the two readers can
    /// disagree only about their inputs — which is what <c>SkyAgreementTests</c> holds them to.
    /// </summary>
    public interface ISkyColumnSource
    {
        GridSize Size { get; }
        bool IsSolid(int index);
        bool IsWater(int index);
        bool HasSlab(int index);
        bool IsTree(int index);
    }

    /// <summary>
    /// The simulation's answers to <see cref="ISkyColumnSource"/>: the cell grid, and the placed
    /// edifice list to say which handles are trees.
    ///
    /// <para>A handle is read as the render mirror reads it (<c>WorldRenderModel.CopyCell</c>):
    /// whatever stands in the list at that slot, removed or not. A removal clears the grid's
    /// handle, so the two only differ on a list the grid no longer points at, and agreeing with
    /// the mirror is the point.</para>
    /// </summary>
    public readonly struct GridSkySource : ISkyColumnSource
    {
        readonly CellGrid _grid;
        readonly IReadOnlyList<PlacedEdifice> _edifices;

        public GridSkySource(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices)
        {
            _grid = grid;
            _edifices = edifices;
        }

        public GridSize Size => _grid.Size;
        public bool IsSolid(int index) => _grid.IsSolidTerrain(index);
        public bool IsWater(int index) => NaturalContent.IsWater(_grid.Terrain[index]);
        public bool HasSlab(int index) => _grid.Floor[index] != 0;

        public bool IsTree(int index)
        {
            int handle = _grid.Edifice[index];
            return handle >= 0 && handle < _edifices.Count && NaturalContent.IsTree(_edifices[handle].Def);
        }
    }

    /// <summary>
    /// Where the rain stops over a column: the one pure function both the simulation's map
    /// (<see cref="SkyColumns"/>) and the drawing's texture (<c>SkyHeightMap</c>) call (design 43
    /// §6, P1).
    ///
    /// <para><b>The column walk.</b> From the top of the world down, the first solid cell, water
    /// surface or slab met is where the rain lands. A solid cell covers its own layer and every
    /// one under it, so the sky reaches the layer above it; water and a slab are landed
    /// <em>on</em>, so the sky reaches their own layer — a colonist swimming or standing on a roof
    /// is in the rain, the one under the roof is not. This is the whole column, not the cell
    /// above: a roof two storeys up keeps a tall hall dry and an overhang keeps a cave mouth dry,
    /// which <see cref="CellGrid.IsRoofed"/> (one layer) gets wrong.</para>
    ///
    /// <para><b>The canopy is derived, never counted.</b> A tree met on the way down, before the
    /// rain's landing, lifts the columns within <see cref="CanopyReach"/> of its trunk to
    /// <see cref="CanopyLayers"/> above the layer it stands in: its own and the one over it. The
    /// drawing puts the crown 4.5 m above the trunk's floor, inside that second layer. Nothing
    /// here keeps a list of trees, so a tree that goes by any path — felled, or dropped through a
    /// collapsing floor, neither of which marks the edifice record removed — is gone from the
    /// next answer the moment its column is asked again.</para>
    ///
    /// <para><b>Cost</b>: one walk of this column and one of each within reach, each stopping at
    /// the first thing the rain meets — a few cells on the surface, so about fifty cell reads.
    /// Scales with nothing.</para>
    /// </summary>
    public static class SkyColumnRule
    {
        /// <summary>How many layers a tree's canopy covers, counting the one its trunk stands in.</summary>
        public const int CanopyLayers = 2;

        /// <summary>How many columns either side of a trunk its canopy covers. 1 is a 3 × 3.</summary>
        public const int CanopyReach = 1;

        public static SkyColumn Compute<T>(in T source, int x, int z) where T : struct, ISkyColumnSource
        {
            SkyColumn best = Walk(source, x, z, out _);

            GridSize size = source.Size;
            for (int dz = -CanopyReach; dz <= CanopyReach; dz++)
            for (int dx = -CanopyReach; dx <= CanopyReach; dx++)
            {
                int cx = x + dx, cz = z + dz;
                if (cx < 0 || cz < 0 || cx >= size.SizeX || cz >= size.SizeZ) continue;
                Walk(source, cx, cz, out int trunk);
                if (trunk < 0) continue;
                int canopy = trunk + CanopyLayers;
                // Strictly higher: a roof at the height the canopy would reach keeps the column,
                // as the drawing keeps a stop the crown does not rise above.
                if (canopy > best.StopLayer) best = new SkyColumn(canopy, SkyStop.Canopy);
            }

            return best;
        }

        /// <summary>
        /// <see cref="Compute{T}"/> for every column of the board at once, into
        /// <paramref name="into"/> (one per column, <c>z * SizeX + x</c>), with
        /// <paramref name="trunks"/> as scratch of the same length.
        ///
        /// <para><b>The same answer by a cheaper road.</b> <see cref="Compute{T}"/> walks a column
        /// and the eight around it, so asking it of every column walks the board nine times —
        /// 13.8 ms on Standard and 53 ms on Huge, measured
        /// (<c>TickBenchmarkTests.TheSkyColumnsCostWhatAnEditTouches</c>). Here each column is
        /// walked once, keeping its own landing and its trunk, and each trunk then lifts the
        /// columns within reach where its canopy is strictly higher — which is what
        /// <see cref="Compute{T}"/> takes the maximum of. <c>SkyColumnsTests</c> holds the two to
        /// the same answer on every column after a seeded jumble of edits.</para>
        /// </summary>
        public static void ComputeBoard<T>(in T source, SkyColumn[] into, int[] trunks) where T : struct, ISkyColumnSource
        {
            GridSize size = source.Size;
            int w = size.SizeX, d = size.SizeZ;
            if (into.Length < w * d || trunks.Length < w * d)
                throw new ArgumentException("one entry per column", nameof(into));

            for (int z = 0; z < d; z++)
            for (int x = 0; x < w; x++)
            {
                int c = z * w + x;
                into[c] = Walk(source, x, z, out int trunk);
                trunks[c] = trunk;
            }

            for (int z = 0; z < d; z++)
            for (int x = 0; x < w; x++)
            {
                int trunk = trunks[z * w + x];
                if (trunk < 0) continue;
                int canopy = trunk + CanopyLayers;
                for (int cz = Math.Max(0, z - CanopyReach); cz <= Math.Min(d - 1, z + CanopyReach); cz++)
                for (int cx = Math.Max(0, x - CanopyReach); cx <= Math.Min(w - 1, x + CanopyReach); cx++)
                {
                    int c = cz * w + cx;
                    if (canopy > into[c].StopLayer) into[c] = new SkyColumn(canopy, SkyStop.Canopy);
                }
            }
        }

        /// <summary>
        /// The column's own landing, ignoring every canopy, and the highest tree met above it.
        /// </summary>
        public static SkyColumn Walk<T>(in T source, int x, int z, out int trunk) where T : struct, ISkyColumnSource
        {
            GridSize size = source.Size;
            trunk = -1;
            for (int y = size.SizeY - 1; y >= 0; y--)
            {
                int index = size.Index(x, z, y);
                if (source.IsSolid(index)) return new SkyColumn(y + 1, SkyStop.Ground);
                if (source.IsWater(index)) return new SkyColumn(y, SkyStop.Water);
                if (source.HasSlab(index)) return new SkyColumn(y, SkyStop.Built);
                if (trunk < 0 && source.IsTree(index)) trunk = y;
            }
            return new SkyColumn(0, SkyStop.Open);
        }
    }

    /// <summary>
    /// The simulation's owner of shelter (design 43 §6): a rain-stop layer per column, and the one
    /// query every consumer asks — <see cref="ShelteredFromSky"/>. Pace, growth and an animal's
    /// search for cover all ask here, and nothing restates the rule.
    ///
    /// <para><b>Recomputed for the columns an edit touched, never the board.</b> It hears about
    /// edits through the chunk grid, which every edit path already tells so the drawing re-meshes
    /// (<see cref="ChunkGrid.TakeEditedColumns"/>): the same notice the render mirror refreshes
    /// on, so the two cannot be fresh about different edits. Each touched column is widened by the
    /// canopy's reach, because a trunk going changes the columns around it. The sync is lazy — on
    /// the first question after an edit — and is one integer comparison when nothing moved.</para>
    ///
    /// <para><b>Derived, so neither saved nor hashed.</b> It is a function of the grid and the
    /// edifice list, and a load rebuilds it whole (<see cref="MarkAllDirty"/>).</para>
    ///
    /// <para><b>Cost</b>: a board-wide rebuild walks each column once
    /// (<see cref="SkyColumnRule.ComputeBoard{T}"/>), during loading (<c>ColonyWorld</c> asks for
    /// it there, so the first tick does not pay it). An edit is one
    /// <see cref="SkyColumnRule.Compute{T}"/> per column within the canopy's reach of the columns
    /// it touched: nine for one cell, twenty-five for an order's 3 × 3 × 3 marking, about 9 us,
    /// the same on Standard as on Huge. Scales with the columns edited and never the board
    /// (<c>TickBenchmarkTests.TheSkyColumnsCostWhatAnEditTouches</c>).</para>
    /// </summary>
    public sealed class SkyColumns
    {
        readonly CellGrid _grid;
        readonly IReadOnlyList<PlacedEdifice> _edifices;
        readonly ChunkGrid _chunks;
        readonly SkyColumn[] _columns;
        readonly int[] _trunks;

        // Scratch for a sync: the columns an edit touched, and which of the widened set has been
        // recomputed this sync, by a token so it never needs clearing.
        readonly List<int> _edited = new List<int>();
        readonly int[] _seen;
        int _token;
        bool _all = true;

        public SkyColumns(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices, ChunkGrid chunks)
        {
            _grid = grid;
            _edifices = edifices;
            _chunks = chunks;
            int columns = grid.Size.LayerStride;
            _columns = new SkyColumn[columns];
            _trunks = new int[columns];
            _seen = new int[columns];
        }

        public GridSize Size => _grid.Size;

        /// <summary>How many columns the last sync recomputed. A measurement, for the benchmark.</summary>
        public int LastRecomputed { get; private set; }

        /// <summary>Forget everything and rebuild on the next question: a load, or a fixture that wrote the grid behind the chunk grid's back.</summary>
        public void MarkAllDirty() => _all = true;

        /// <summary>
        /// Is this cell under cover — its layer below its column's rain stop? A cell out of range
        /// is under nothing.
        /// </summary>
        public bool ShelteredFromSky(int cellIndex)
        {
            if ((uint)cellIndex >= (uint)_grid.Size.CellCount) return false;
            Sync();
            int stride = _grid.Size.LayerStride;
            return cellIndex / stride < _columns[cellIndex % stride].StopLayer;
        }

        /// <summary>The column's answer, for tests, the agreement check and the debug pane.</summary>
        public SkyColumn ColumnAt(int x, int z)
        {
            Sync();
            return _columns[z * _grid.Size.SizeX + x];
        }

        /// <summary>Bring every edited column up to date. One comparison when nothing has moved.</summary>
        public void Sync()
        {
            if (_all)
            {
                _all = false;
                _chunks.TakeEditedColumns(_edited);
                _edited.Clear();
                RebuildAll();
                return;
            }

            if (!_chunks.HasEditedColumns) return;
            _chunks.TakeEditedColumns(_edited);

            int w = _grid.Size.SizeX, d = _grid.Size.SizeZ, r = SkyColumnRule.CanopyReach;
            if (++_token == int.MaxValue)
            {
                Array.Clear(_seen, 0, _seen.Length);
                _token = 1;
            }

            var source = new GridSkySource(_grid, _edifices);
            int recomputed = 0;
            for (int i = 0; i < _edited.Count; i++)
            {
                int column = _edited[i];
                int x = column % w, z = column / w;
                for (int cz = Math.Max(0, z - r); cz <= Math.Min(d - 1, z + r); cz++)
                for (int cx = Math.Max(0, x - r); cx <= Math.Min(w - 1, x + r); cx++)
                {
                    int c = cz * w + cx;
                    if (_seen[c] == _token) continue;
                    _seen[c] = _token;
                    _columns[c] = SkyColumnRule.Compute(source, cx, cz);
                    recomputed++;
                }
            }
            _edited.Clear();
            LastRecomputed = recomputed;
        }

        void RebuildAll()
        {
            SkyColumnRule.ComputeBoard(new GridSkySource(_grid, _edifices), _columns, _trunks);
            LastRecomputed = _columns.Length;
        }
    }
}
