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
        /// <summary>Bit 8 marks a code as terrain rather than construction stuff.</summary>
        public const int TerrainBase = 256;

        /// <summary>How many shades of one material a surface is dithered across.</summary>
        public const int Variations = 4;

        const int VariationShift = 9;

        public static int Stuff(int stuff) => stuff;
        public static int Terrain(int terrain) => TerrainBase + terrain;

        /// <summary>
        /// A terrain code carrying one of <see cref="Variations"/> shades of its material.
        ///
        /// A barren map is a single material across every cell, and in one flat colour that reads
        /// as a painted plane rather than as ground: there is no grain for the eye to catch, so
        /// nothing conveys that a colonist is crossing a surface at all. Dithering the tint over a
        /// few very close shades puts the grain back with no texture, no extra mesh and no shader
        /// work. The shades are deliberately near-identical; the goal is a surface, not a patchwork.
        ///
        /// The cost is bounded and known. A chunk keeps one bucket per shade it actually uses
        /// rather than one, so ground costs up to four instanced calls per chunk instead of one.
        /// Nothing is resolved per cell or per frame: the shade is part of the bucket key, so it is
        /// looked up once per bucket at draw time, exactly as the material tint already was.
        /// </summary>
        public static int Terrain(int terrain, int variation) =>
            TerrainBase + terrain + (variation << VariationShift);

        public static bool IsTerrain(int code) => (code & TerrainBase) != 0;

        /// <summary>The material index, with the terrain marker and shade bits stripped off.</summary>
        public static int Value(int code) => code & 0xFF;

        /// <summary>Which shade this code asks for, 0 to <see cref="Variations"/> - 1.</summary>
        public static int Variation(int code) => code >> VariationShift;
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
