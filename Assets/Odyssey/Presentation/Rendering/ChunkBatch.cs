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

        /// <summary>
        /// Bit 9 marks a code as foliage, which is neither terrain nor stuff.
        ///
        /// It exists because grass tufts stand on grass and are emphatically not tinted like it.
        /// The terrain tint is calibrated against a **tiling ground texture** that needed lifting
        /// towards the reference art — it multiplies blue by 1.55. Worn by a tuft, whose art is
        /// already the right yellow-green, that same multiplier turned a meadow into a stand of
        /// dark teal reeds. Two different things being tinted needs two different tints, and
        /// sharing one code space would have made that impossible to express.
        /// </summary>
        public const int FoliageBase = 512;

        /// <summary>
        /// Bit 10 marks a code as water, which is terrain that is drawn by a different shader.
        ///
        /// It is a code rather than a test on the terrain index because the renderer has no
        /// business knowing which indices happen to be wet. The bucket already carries one
        /// integer that is the whole of its material identity, and the shader a bucket wants is
        /// exactly the kind of thing that integer is for — the same argument that gave foliage
        /// its own bit, arriving at the same answer.
        /// </summary>
        public const int WaterBase = 1024;

        /// <summary>
        /// Bit 11 marks a code as open to the sky, which exempts it from the depth shade.
        ///
        /// <para><b>Why the landscape must not dim.</b> The board is terraced — `surfaceRelief`
        /// gives it a five-step surface — so the outdoor ground spans five layers and only one of
        /// them is the active one. Every other step of the hillside was being multiplied by
        /// <c>SliceSettings.belowFalloff</c> once per layer of drop, so grass two terraces below
        /// the slice drew at 0.46 of its colour and the same meadow came out in three different
        /// greens. It reads as lighting, and there is no light there to explain it.</para>
        ///
        /// <para>The depth shade is a cue for looking <em>through</em> something: it says how far
        /// under the surface you are peering, and it is what keeps the layer being worked distinct
        /// from the working below it. Nothing is over an outdoor surface, so the cue has nothing
        /// to say about one. A cell earns this bit when no slab and no solid cell stands anywhere
        /// above it — so grass under a tree keeps it (a trunk is not a roof) and the floor of a
        /// roofed room does not.</para>
        ///
        /// <para>It rides in the tint code rather than being tested at draw time because the shade
        /// is resolved per bucket, not per cell, and the bucket key already <em>is</em> the whole
        /// material identity — the same argument that gave foliage and water their bits. In
        /// practice it costs no extra bucket worth counting: a chunk's terrain is nearly all
        /// daylit or nearly all buried, and buried cells are culled before they reach a bucket.</para>
        /// </summary>
        public const int DaylitBase = 2048;

        public static int Stuff(int stuff) => stuff;

        public static int Terrain(int terrain) => TerrainBase + terrain;

        public static int Foliage(int variant) => FoliageBase + variant;

        /// <summary>Water is terrain as well, so it keeps the terrain bit and its palette entry.</summary>
        public static int Water(int terrain) => WaterBase + TerrainBase + terrain;

        public static bool IsTerrain(int code) => (code & TerrainBase) != 0;

        public static bool IsFoliage(int code) => (code & FoliageBase) != 0;

        public static bool IsWater(int code) => (code & WaterBase) != 0;

        /// <summary>Is this bucket open to the sky, and so exempt from the depth shade?</summary>
        public static bool IsDaylit(int code) => (code & DaylitBase) != 0;

        /// <summary>The same code with the daylight marker set, when the cell is open to the sky.</summary>
        public static int Daylit(int code, bool open) => open ? code | DaylitBase : code;

        /// <summary>The material index, with the terrain marker stripped off.</summary>
        public static int Value(int code) => code & 0xFF;
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
