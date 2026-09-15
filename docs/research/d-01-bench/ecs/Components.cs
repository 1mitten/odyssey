using Unity.Collections;
using Unity.Entities;

namespace Bench
{
    // ---- world constants (contract section 1) ----
    public static class W
    {
        public const int X = 250;
        public const int Z = 250;
        public const int Y = 40;
        public const int LayerCells = X * Z;          // 62,500
        public const int CellCount = LayerCells * Y;  // 2,500,000

        public const int ThingCount = 20000;
        public const int PawnCount = 50;
        public const int PortalCount = 200;

        public const int FrontierCap = 8000;
        public const int FrontierTopUp = 2000;

        public const int PathCap = 64;
        public const int AStarPopBudget = 20000;

        public const int WarmupTicks = 300;
        public const int MeasuredTicks = 1500;
    }

    public struct HeapItem
    {
        public int F;
        public int Cell;
    }

    /// <summary>
    /// The singleton component: every cell array and every piece of shared simulation
    /// state lives here, held by one entity. Systems reach it with
    /// SystemAPI.GetSingletonRW&lt;SimData&gt;().
    /// </summary>
    public struct SimData : IComponentData
    {
        public uint Rng;
        public int Tick;

        // cell arrays (structure of arrays)
        public NativeArray<byte> Solid;
        public NativeArray<short> Temp;

        // phase 1 scratch
        public NativeArray<byte> Mark;
        public NativeList<int> FrontierCur;
        public NativeList<int> FrontierNext;

        // phase 3 A* scratch, persistent across searches
        public NativeArray<int> G;
        public NativeArray<int> CameFrom;
        public NativeArray<byte> Closed;
        public NativeList<int> Touched;
        public NativeList<HeapItem> Heap;
        public NativeList<int> PathScratch;

        // portals: cell -> singly linked list of partner cells, insertion order preserved
        public NativeHashMap<int, int> PortalHead;
        public NativeHashMap<int, int> PortalTail;
        public NativeList<int> PortalPartner;
        public NativeList<int> PortalNext;

        // entity handles held in explicit index order (0 .. N-1)
        public NativeArray<Entity> Things;
        public NativeArray<Entity> Pawns;

        // phase 4 double-buffered view
        public NativeArray<byte> SliceA;
        public NativeArray<byte> SliceB;
        public NativeArray<int> ViewA;
        public NativeArray<int> ViewB;
        public int FrontIsA;

        // diagnostics only, never part of the hash
        public int Replans;
        public int ReplanOk;

        // 0 = the 20,000 budget counts every heap pop (contract read literally)
        // 1 = it counts only pops that actually expand (lazy-deleted pops are free)
        public int BudgetMode;

        public long NativeBytes;
    }

    // ---- thing components ----
    public struct ThingIndex : IComponentData { public int Value; }
    public struct ThingCell : IComponentData { public int Value; }
    public struct ThingState : IComponentData { public int Value; }
    public struct ThingTicker : IComponentData { public byte Value; }

    // ---- pawn components ----
    public struct PawnIndex : IComponentData { public int Value; }
    public struct PawnCell : IComponentData { public int Value; }
    public struct PawnNeed : IComponentData { public int Value; }
    public struct PawnPathCursor : IComponentData { public int Step; }

    [InternalBufferCapacity(W.PathCap)]
    public struct PawnPathElem : IBufferElementData { public int Value; }
}
