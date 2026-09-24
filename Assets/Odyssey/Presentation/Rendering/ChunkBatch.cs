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

        /// <summary>
        /// Bit 12 marks a code as a tree, whose colour is four colours rather than one.
        ///
        /// <para>Every other code in this space resolves to a single tint that multiplies whatever
        /// is underneath. A tree cannot: it is one mesh with one material, and the pack paints its
        /// trunk and its canopy from different cells of the same atlas, so one multiply browns the
        /// leaves along with the bark. It is drawn by <c>TreeMaterials</c> against
        /// <c>Odyssey/Tree</c>, with the four colours handed over per instance, rather than by
        /// <c>ResolveColour</c> against <c>MaterialCache</c>.</para>
        ///
        /// <para>It earns a bit of its own for the reason foliage and water did: the bucket key
        /// already <em>is</em> the whole material identity, and which cache a bucket wants is
        /// exactly the kind of thing that integer is for.</para>
        /// </summary>
        public const int TreeBase = 4096;

        /// <summary>
        /// Where a tree's <b>species</b> sits in the code: bits 16 and up, clear of the terrain,
        /// foliage, water and daylight markers that live in the low bits.
        ///
        /// <para><b>It used to be the theme, and moving it out is what made a coloured wood free.</b>
        /// A bucket is one instanced draw of one (module, part, tint), so while the colour lived in
        /// this code it <em>was</em> a bucket key: every extra colour standing in a chunk cost a
        /// draw call, and a thoroughly mixed wood cost 384 of them on the played board. The colours
        /// are per-instance data now (<c>InstanceBucket.BarkDeep</c> and its three siblings), and
        /// all this has to say is which of the two trees it is — because that is what decides which
        /// atlas cells are repainted, and so which material draws it.</para>
        /// </summary>
        public const int TreeShift = 16;

        /// <summary>The largest species index a code can carry.</summary>
        public const int MaxTreeValue = (1 << 12) - 1;

        /// <summary>
        /// Bit 13 marks a code as <b>linen</b> — bedding, which is white whatever it lies on.
        ///
        /// <para>A bed is tinted by the stuff it was built of, and its pillow is not: a stone bed
        /// has a white pillow exactly as a wooden one does (owner, 2026-09-17: "make the pillow
        /// white"). That cannot be said by picking a stuff, because every stuff in the table is
        /// something a colonist carries and none of them is cloth — so it is said the way foliage
        /// and water already say the same kind of thing, with a bit of its own.</para>
        ///
        /// <para><b>Bit 13, not 12.</b> It was written as 12 and the tree took that on the way
        /// past; the two are independent markers and a shared bit would have made every pillow a
        /// tree. Nothing is stored in these codes, so moving one costs nothing but this sentence.</para>
        /// </summary>
        public const int LinenBase = 8192;

        /// <summary>
        /// Bit 14 marks a code as a surface that is drawn <b>whole</b>: the sight fade never splits
        /// it, however squarely it stands in the beam.
        ///
        /// <para>It names a rule rather than a thing, because two different things want it and a
        /// third will. A <b>bank</b> is the stepped earth ramp drawn against a one-layer terrace
        /// step, which no cell in the simulation contains at all. <b>Marsh</b> is the wet fringe of
        /// a pond, which is an ordinary solid cell but reads as part of the water beside it. In both
        /// cases a half-ghosted surface is not a view through anything — it is a hole in the
        /// landscape, in a place that has no hole in it, and next to water that stays whole because
        /// water is exempt too. <c>ChunkRenderer.NeverFades</c> is where the rule is spent.</para>
        ///
        /// <para>This bit says nothing about colour — <see cref="ChunkRenderer.ResolveColour"/>
        /// never reads it, and a bank and a marsh are both still terrain, still tinted as terrain.
        /// It is here so the <em>drawing</em> can tell one from the ground beside it.</para>
        ///
        /// <para>It costs no extra bucket worth counting. A bank is its own module and so was
        /// already its own bucket; marsh is its own terrain index and so already had its own tint.
        /// That is exactly why the marker can be free here and could not be on, say, a tree's
        /// colour.</para>
        /// </summary>
        public const int WholeBase = 16384;

        /// <summary>
        /// Bit 15 marks a terrain code as <b>tilled</b>: worked soil inside a growing zone, which
        /// is the ordinary earth of its cell graded darker.
        ///
        /// <para><b>This replaces a whole draw pass.</b> A zone used to be drawn as a second copy
        /// of the ground module laid over the first, tinted and translucent, one
        /// <c>Graphics.RenderMesh</c> per zoned cell per frame - 2,065 draw calls on the
        /// benchmark's field, and 3.67 ms of a 5 ms budget, against 0.17 ms for the same field
        /// with the pass switched off. It was also the only thing in the feature that stood
        /// outside the instanced-chunk architecture, and it paid the neighbour scan, the variant
        /// lookup, the module lookup and a four-stage matrix compose every frame for ground that
        /// changes when a player paints it and at no other time.</para>
        ///
        /// <para>Saying it with a bit instead is the same argument foliage, water, daylight and
        /// linen already make here: the bucket key is the whole of a bucket's material identity,
        /// and "this ground is worked" is exactly that kind of fact. The per-cell work moves to
        /// the re-mesh, where <see cref="Odyssey.Sim.Growing.GrowingZones"/> already marks the
        /// chunk on every designate, cancel, sow and uproot; the field inherits frustum culling,
        /// slice culling and instancing for nothing.</para>
        ///
        /// <para>It costs no extra bucket worth counting, for the reason <see cref="WholeBase"/>
        /// gives: a zoned cell's ground is already swapped to earth by <c>DrawnTerrain</c>, so it
        /// had its own terrain tint before this bit existed. A chunk holding a field pays one
        /// bucket for the tilled earth and one for the untilled, where it used to pay one draw
        /// per cell.</para>
        /// </summary>
        public const int TilledBase = 32768;

        /// <summary>
        /// Bit 16 marks a surface as <b>stored</b>: it is inside a storage zone, and the ground —
        /// or the slab — it already draws is washed towards the store's blue-grey.
        ///
        /// <para><b>The same trick as <see cref="TilledBase"/>, and it has to be applied in two
        /// places rather than one.</b> A growing zone is legal only on open fertile soil, so the
        /// terrain quad is always the thing being drawn under it; a storage zone's commonest home
        /// is a wooden floor inside a building, where there is no terrain quad at all and the
        /// slab is what the player sees. So the bit is set on the terrain emit <em>and</em> on the
        /// floor emit, and <c>ChunkRenderer</c> grades whichever arrives.</para>
        ///
        /// <para><b>And it grades rather than swapping.</b> Tilled soil swaps the terrain itself —
        /// <c>DrawnTerrain</c> answers bare earth for a zoned cell, because a field is soil
        /// somebody turned over. A store changes nothing about what the ground <i>is</i>: the
        /// stone stays stone and the planks stay planks, with a wash over them saying the colony
        /// has claimed the spot.</para>
        ///
        /// <para>The cost is buckets, not draws: a chunk holding a warehouse pays one extra bucket
        /// per surface material it covers, and no per-cell submission at all. That is the whole
        /// reason the drawing is a bit and not an overlay — <c>docs/bug-patterns.md</c> P10, the
        /// pass that draws once per cell, at about 4.6 µs a submission.</para>
        /// </summary>
        public const int StoredBase = 65536;

        /// <summary>Is this bucket inside a storage zone, and so washed towards the store's hue?</summary>
        public static bool IsStored(int code) => (code & StoredBase) != 0;

        /// <summary>The same code, marked as a store's ground.</summary>
        public static int Stored(int code) => code | StoredBase;

        /// <summary>
        /// The line round a stockpile's outer edge, drawn in the store's own hue at full strength
        /// where the ground under it is only washed a third of the way (owner, 2026-09-23). Bit 28,
        /// above the twelve bits a tree's species is packed into from <see cref="TreeShift"/>, and
        /// asked with <see cref="IsTree"/> excluded all the same.
        /// </summary>
        public const int StoreEdgeBase = 1 << 28;

        public static bool IsStoreEdge(int code) => (code & StoreEdgeBase) != 0 && (code & TreeBase) == 0;

        public static int StoreEdge() => StoreEdgeBase;

        /// <summary>
        /// Bit 29 marks a bucket as <b>Meadow dressing</b> — scenery the simulation never heard of
        /// (design 38 §17) — so the drawing can treat a bush apart from a tree that wears the same
        /// tree path. Says nothing about colour.
        /// </summary>
        public const int DressingBase = 1 << 29;

        public static int Dressing(int code) => code | DressingBase;

        public static bool IsDressing(int code) => (code & DressingBase) != 0;

        public static int Stuff(int stuff) => stuff;

        /// <summary>Bedding: one fixed colour, ignoring whatever material is passed beside it.</summary>
        public static int Linen() => LinenBase;

        public static bool IsLinen(int code) => (code & LinenBase) != 0;

        public static int Terrain(int terrain) => TerrainBase + terrain;

        public static int Foliage(int variant) => FoliageBase + variant;

        /// <summary>A tree of this species. Its colours travel per instance, not in the code.</summary>
        public static int Tree(TreeSpecies species) => TreeBase | ((int)species << TreeShift);

        /// <summary>
        /// The species out of a tree code. <see cref="Value"/> is the wrong reader for one — it
        /// masks the low byte, where a tree keeps nothing at all.
        /// </summary>
        public static TreeSpecies TreeSpeciesOf(int code) =>
            ((code >> TreeShift) & MaxTreeValue) == (int)TreeSpecies.Conifer
                ? TreeSpecies.Conifer
                : TreeSpecies.Broadleaf;

        /// <summary>Water is terrain as well, so it keeps the terrain bit and its palette entry.</summary>
        public static int Water(int terrain) => WaterBase + TerrainBase + terrain;

        /// <summary>
        /// The same code, marked as a surface to draw whole. Adds nothing to how it is coloured.
        /// </summary>
        public static int Whole(int code) => code | WholeBase;

        public static bool IsTerrain(int code) => (code & TerrainBase) != 0;

        /// <summary>Is this bucket worked soil, and so graded darker than the earth it is?</summary>
        public static bool IsTilled(int code) => (code & TilledBase) != 0;

        /// <summary>The same code, marked as worked soil.</summary>
        public static int Tilled(int code) => code | TilledBase;

        public static bool IsFoliage(int code) => (code & FoliageBase) != 0;

        public static bool IsWater(int code) => (code & WaterBase) != 0;

        /// <summary>Is this bucket a surface the sight fade must leave in one piece?</summary>
        public static bool IsWhole(int code) => (code & WholeBase) != 0;

        /// <summary>Is this bucket a tree, and so coloured from <see cref="TreePalette"/>?</summary>
        public static bool IsTree(int code) => (code & TreeBase) != 0;

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

        /// <summary>
        /// The four colours of each instance, parallel to <see cref="Matrices"/> — or null for
        /// every bucket that is not a tree, which is nearly all of them.
        ///
        /// <para>This is what took a coloured wood off the draw-call bill. A colour on the material
        /// is a bucket key and costs a draw call per distinct colour in a chunk; a colour beside
        /// the matrix costs nothing at all, so the palette is free to be any size. They are four
        /// separate lists rather than one interleaved array because that is the shape
        /// <c>MaterialPropertyBlock.SetVectorArray</c> takes — and <b>lists rather than arrays</b>
        /// because that overload uploads exactly as many entries as the list holds. An array would
        /// upload its whole capacity, which doubles: a bucket of 600 trees would hand over 1,024
        /// vectors and trip Unity's 1,023 ceiling for a bucket that is nowhere near it.</para>
        /// </summary>
        public List<Vector4>? BarkDeep;
        public List<Vector4>? BarkWarm;
        public List<Vector4>? LeafDeep;
        public List<Vector4>? LeafFresh;

        /// <summary>
        /// The block handed to the draw, built once when the renderer first needs it rather than
        /// once a frame. <see cref="Clear"/> drops it, because a re-meshed chunk's colours are new.
        /// </summary>
        public MaterialPropertyBlock? Props;

        /// <summary>True when this bucket carries per-instance colours.</summary>
        public bool IsColoured => BarkDeep != null;

        public void Clear()
        {
            Count = 0;
            Props = null;
            BarkDeep?.Clear();
            BarkWarm?.Clear();
            LeafDeep?.Clear();
            LeafFresh?.Clear();
        }

        public void Add(in Matrix4x4 matrix)
        {
            if (Count == Matrices.Length) System.Array.Resize(ref Matrices, Matrices.Length * 2);
            Matrices[Count++] = matrix;
        }

        /// <summary>Add an instance that carries its own four colours.</summary>
        public void Add(in Matrix4x4 matrix, in Vector4 barkDeep, in Vector4 barkWarm,
            in Vector4 leafDeep, in Vector4 leafFresh)
        {
            if (BarkDeep == null)
            {
                BarkDeep = new List<Vector4>(Matrices.Length);
                BarkWarm = new List<Vector4>(Matrices.Length);
                LeafDeep = new List<Vector4>(Matrices.Length);
                LeafFresh = new List<Vector4>(Matrices.Length);
            }

            Add(matrix);
            BarkDeep.Add(barkDeep);
            BarkWarm!.Add(barkWarm);
            LeafDeep!.Add(leafDeep);
            LeafFresh!.Add(leafFresh);
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

        /// <summary>
        /// The ground skin in this chunk (<see cref="GroundSkin"/>): ramps, flat tops and the skirts
        /// between them, as one mesh. Drawn with <see cref="Body"/>; owned here and destroyed with
        /// the batch.
        /// </summary>
        public readonly GroundSkinMesh Skin = new GroundSkinMesh();

        public void Clear()
        {
            for (int i = 0; i < Body.Count; i++) Body[i].Clear();
            for (int i = 0; i < Roof.Count; i++) Roof[i].Clear();
            Skin.Clear();
            InstanceCount = 0;
        }

        public void Dispose() => Skin.Dispose();
    }
}
