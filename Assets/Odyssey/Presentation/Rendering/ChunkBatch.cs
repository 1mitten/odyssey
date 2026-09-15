#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Where a bucket's colour comes from. The simulation has two independent material tables —
    /// construction "stuff" and natural terrain — and a bucket is tinted from one of them.
    /// Packing the choice into the bucket key keeps a single integer as the whole tint identity.
    /// </summary>
    public static class TintCode
    {
        public const int TerrainBase = 256;

        public static int Stuff(int stuff) => stuff;
        public static int Terrain(int terrain) => TerrainBase + terrain;
        public static bool IsTerrain(int code) => code >= TerrainBase;
        public static int Value(int code) => code >= TerrainBase ? code - TerrainBase : code;
    }

    /// <summary>
    /// One instanced draw: every copy of one part of one module, in one tint, inside one chunk.
    ///
    /// The matrix array is kept and reused across rebuilds. A chunk that is remeshed because a
    /// wall fell reuses the arrays it already had, so steady play allocates nothing.
    /// </summary>
    public sealed class InstanceBucket
    {
        public int Module;
        public int Part;
        public int Tint;
        public Matrix4x4[] Matrices = new Matrix4x4[16];
        public int Count;

        public void Clear() => Count = 0;

        public void Add(in Matrix4x4 matrix)
        {
            if (Count == Matrices.Length) System.Array.Resize(ref Matrices, Matrices.Length * 2);
            Matrices[Count++] = matrix;
        }
    }

    /// <summary>
    /// The drawable form of one 25 x 25 chunk of one layer: the unit of dirty tracking from
    /// <c>02-world-and-layers.md</c> and the unit of drawing from <c>06-rendering-and-camera.md</c>,
    /// deliberately the same unit.
    ///
    /// Buckets are split into body and roof because the slice needs to drop the roof without
    /// rebuilding anything. The active layer is drawn with its ceiling suppressed, and
    /// <c>roofs-off</c> mode drops every roof above; both are "skip this list at draw time", not
    /// "mesh the chunk differently", so changing the slice costs nothing.
    /// </summary>
    public sealed class ChunkBatch
    {
        public int ChunkIndex;
        public int Layer;
        public Bounds Bounds;

        /// <summary>The mirror version this batch was meshed from. Stale means remesh.</summary>
        public int Version = -1;

        /// <summary>Walls, doors, pillars, stairs, ladders and solid strata.</summary>
        public readonly List<InstanceBucket> Body = new List<InstanceBucket>();

        /// <summary>Slabs and ground surfaces — everything the cut-away drops.</summary>
        public readonly List<InstanceBucket> Roof = new List<InstanceBucket>();

        public int InstanceCount;

        public void Clear()
        {
            for (int i = 0; i < Body.Count; i++) Body[i].Clear();
            for (int i = 0; i < Roof.Count; i++) Roof[i].Clear();
            InstanceCount = 0;
        }
    }
}
