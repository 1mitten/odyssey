#nullable enable
using System;

namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// What the presentation layer knows about one pawn. A view is a value: it holds an id, never
    /// a reference to a simulation object.
    ///
    /// That is what makes the awkward case safe. A panel open on a pawn that dies mid-tick holds
    /// only a <see cref="PawnId"/>; the next snapshot simply lacks that id, and the panel closes
    /// with a "no longer present" state instead of dereferencing freed state.
    /// </summary>
    public readonly struct PawnView
    {
        public readonly PawnId Id;
        public readonly CellRef Cell;
        public readonly int Food;
        public readonly int Rest;
        public readonly int Mood;

        /// <summary>
        /// The job this pawn is running, as an integer handle into the job table, or -1 when idle.
        ///
        /// An index rather than a label on purpose: a string per pawn per tick would allocate in
        /// the publish phase, which is meant to be allocation-free in steady state. Presentation
        /// turns the index into words, which is also where localisation belongs.
        ///
        /// This field exists because a colonist walking to a meal and a colonist wandering because
        /// they are miserable looked identical on screen, and a simulation you cannot read is a
        /// simulation you cannot trust.
        /// </summary>
        public readonly int JobDef;

        /// <summary>
        /// The cell being walked into, or the current cell when standing still.
        ///
        /// This and <see cref="MovePercent"/> exist so presentation can draw a pawn gliding
        /// between cells rather than snapping from one to the next. The simulation stays discrete
        /// and integer, which determinism requires; the smoothing is a facade over it, and is
        /// exactly how the genre's reference points do it.
        /// </summary>
        public readonly CellRef NextCell;

        /// <summary>How far from <see cref="Cell"/> to <see cref="NextCell"/>, 0 to 100.</summary>
        public readonly int MovePercent;

        public PawnView(
            PawnId id, CellRef cell, int food, int rest, int mood,
            int jobDef = -1, CellRef nextCell = default, int movePercent = 0)
        {
            Id = id;
            Cell = cell;
            Food = food;
            Rest = rest;
            Mood = mood;
            JobDef = jobDef;
            NextCell = nextCell;
            MovePercent = movePercent;
        }
    }

    /// <summary>What the presentation layer knows about one thing.</summary>
    public readonly struct ThingView
    {
        public readonly ThingId Id;
        public readonly CellRef Cell;
        public readonly int DefIndex;
        public readonly int StuffIndex;

        public ThingView(ThingId id, CellRef cell, int defIndex, int stuffIndex)
        {
            Id = id;
            Cell = cell;
            DefIndex = defIndex;
            StuffIndex = stuffIndex;
        }
    }

    /// <summary>
    /// One published frame of world state: everything presentation may read, and nothing else.
    ///
    /// Buffers are pooled and reused, so a snapshot is only valid until the next publish. The
    /// renderer reads it within the frame and does not retain it. Capacity grows but never
    /// shrinks, so steady-state publishing allocates nothing, which the benchmark confirmed at
    /// 0.186 ms and 69 KB for a full 62,500-cell slice.
    /// </summary>
    public sealed class WorldSnapshot
    {
        PawnView[] _pawns = Array.Empty<PawnView>();
        ThingView[] _things = Array.Empty<ThingView>();
        byte[] _sliceCells = Array.Empty<byte>();

        public int Tick { get; private set; }
        public int SliceLayer { get; private set; }
        public GridSize Size { get; private set; }

        public int PawnCount { get; private set; }
        public int ThingCount { get; private set; }
        public int SliceCellCount { get; private set; }

        public ReadOnlySpan<PawnView> Pawns => new ReadOnlySpan<PawnView>(_pawns, 0, PawnCount);
        public ReadOnlySpan<ThingView> Things => new ReadOnlySpan<ThingView>(_things, 0, ThingCount);

        /// <summary>
        /// One byte per cell of the active layer, in index order. Presentation turns these into a
        /// chunk mesh; it is never one UI element per cell, because a layer is 62,500 cells.
        /// </summary>
        public ReadOnlySpan<byte> SliceCells => new ReadOnlySpan<byte>(_sliceCells, 0, SliceCellCount);

        /// <summary>Find a pawn by id. Returns false when it is gone, which callers must handle.</summary>
        public bool TryGetPawn(PawnId id, out PawnView view)
        {
            for (int i = 0; i < PawnCount; i++)
            {
                if (_pawns[i].Id != id) continue;
                view = _pawns[i];
                return true;
            }
            view = default;
            return false;
        }

        // ---- writing side, used only by the simulation while building the back buffer ----

        internal void BeginWrite(int tick, GridSize size, int sliceLayer)
        {
            Tick = tick;
            Size = size;
            SliceLayer = sliceLayer;
            PawnCount = 0;
            ThingCount = 0;
            SliceCellCount = 0;
        }

        internal void AddPawn(in PawnView view)
        {
            Grow(ref _pawns, PawnCount + 1);
            _pawns[PawnCount++] = view;
        }

        internal void AddThing(in ThingView view)
        {
            Grow(ref _things, ThingCount + 1);
            _things[ThingCount++] = view;
        }

        internal Span<byte> BeginSlice(int cellCount)
        {
            Grow(ref _sliceCells, cellCount);
            SliceCellCount = cellCount;
            return new Span<byte>(_sliceCells, 0, cellCount);
        }

        static void Grow<T>(ref T[] array, int needed)
        {
            if (array.Length >= needed) return;
            int capacity = array.Length == 0 ? 64 : array.Length;
            while (capacity < needed) capacity *= 2;
            Array.Resize(ref array, capacity);
        }
    }
}
