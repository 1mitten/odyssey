#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Which rectangles of the PolygonGeneric atlas a tree paints its bark and its canopy from.
    ///
    /// <para><b>Measured, not guessed</b> — <c>TreeSwatchProbe</c>, 2026-09-18, and the numbers it
    /// printed are in the comments below. The one claim the whole mechanism rests on is that these
    /// cells are <b>flat</b>, so that replacing the colour inside one throws no art away: the probe
    /// reports a maximum deviation of <b>0</b> on every cluster of every tree mesh in the pack,
    /// which is the same answer <c>SwatchProbe</c> got for character bodies.</para>
    ///
    /// <para><b>Why two tables and not one.</b> Conifers and broadleaves are painted from different
    /// <i>columns</i> of the atlas — the pines around u 0.293, the broadleaves around u 0.272 —
    /// so a single set of rectangles wide enough for both would be a band across the middle of the
    /// atlas catching whatever else is painted between them. They are also different shapes: a
    /// broadleaf has one bark cell and a pine has two, the second being the warm tan at u 0.008
    /// that its branch stubs are painted from.</para>
    ///
    /// <para><b>Why they are unions over every tree in the pack</b>, when only two are cast. Each
    /// mesh sits at a slightly different spot inside the same cell — the pine's upper needles are
    /// at v 0.2058, 0.2082 and 0.2095 on the three pine meshes — and a table fitted to the one
    /// mesh in the catalogue would silently stop covering the day somebody recasts the wood.
    /// Silently is the word: an uncovered cluster is not an error, it is a tree that keeps the
    /// pack's own colour while its neighbours change, which reads as an art bug.</para>
    ///
    /// <para><b>The padding is deliberate.</b> A cluster's rectangle is the extent of the UVs
    /// themselves and is often degenerate — 483 of the pine's vertices share one exact point — so
    /// an unpadded rectangle relies on a fragment's interpolated UV landing on an exact float. The
    /// gaps between neighbouring cells are an order of magnitude wider than this pad (0.0079 at
    /// the narrowest, between the pine's trunk and its lower needles), so padding cannot bridge
    /// two of them. <see cref="Disjoint"/> is the test that says so rather than the comment.</para>
    /// </summary>
    public static class TreeSwatches
    {
        /// <summary>How far each measured rectangle is grown on every side, in UV.</summary>
        public const float Pad = 0.0015f;

        /// <summary>
        /// The bark and canopy cells of the broadleaf meshes (SM_Gen_Env_Tree_01 to _03), which
        /// all three paint identically.
        ///
        /// <para>Probe, Tree_03: trunk #6B5E4E at v 0.1678 carrying the lowest 46% of the mesh;
        /// the darker canopy #586644 at v 0.1880-0.1886; the lighter canopy #6A7B52 at
        /// v 0.2059-0.2068, which reaches the very top. There is no second bark cell, so the
        /// warm-trunk colour of a broadleaf theme is not drawn — the art has one trunk colour and
        /// this does not invent a second.</para>
        /// </summary>
        public static readonly TreeCells Broadleaf = new TreeCells(
            barkDeep: new[] { Pack(0.2719f, 0.1678f, 0.2727f, 0.1682f) },
            barkWarm: new Rect[0],
            leafDeep: new[] { Pack(0.2719f, 0.1880f, 0.2727f, 0.1887f) },
            leafFresh: new[] { Pack(0.2719f, 0.2059f, 0.2727f, 0.2068f) });

        /// <summary>
        /// The same for the pines (SM_Gen_Env_Tree_Pine_01 to _03), unioned across the three.
        ///
        /// <para>Probe, Pine_01: trunk #554B40 at v 0.1691-0.1725 from the ground up; lower
        /// needles #4E543D at v 0.1818-0.1936; upper needles #5E654A at v 0.2058, the top half of
        /// the tree. The fifth cluster is 21 vertices of warm tan #9B7E5A at (0.0082, 0.3305) in
        /// the far-left column, sitting between a third and two thirds of the way up — the branch
        /// stubs — and it is the one place a pine has a second wood colour to take a theme's warm
        /// trunk.</para>
        /// </summary>
        public static readonly TreeCells Conifer = new TreeCells(
            barkDeep: new[] { Pack(0.2925f, 0.1630f, 0.2963f, 0.1725f) },
            barkWarm: new[] { Pack(0.0082f, 0.3305f, 0.0097f, 0.3314f) },
            leafDeep: new[] { Pack(0.2925f, 0.1804f, 0.2959f, 0.1936f) },
            leafFresh: new[] { Pack(0.2933f, 0.2058f, 0.2959f, 0.2095f) });

        public static TreeCells For(TreeSpecies species) =>
            species == TreeSpecies.Conifer ? Conifer : Broadleaf;

        static Rect Pack(float u0, float v0, float u1, float v1) =>
            Rect.MinMaxRect(u0 - Pad, v0 - Pad, u1 + Pad, v1 + Pad);

        /// <summary>
        /// Do any two of these rectangles overlap? The shader repaints in a fixed order and would
        /// simply give a shared texel to the last slot, which is a silent wrong colour rather than
        /// an error — so this is asserted rather than trusted.
        /// </summary>
        public static bool Disjoint(TreeCells cells)
        {
            Rect[][] slots = { cells.BarkDeep, cells.BarkWarm, cells.LeafDeep, cells.LeafFresh };
            for (int a = 0; a < slots.Length; a++)
            for (int b = a + 1; b < slots.Length; b++)
            foreach (Rect x in slots[a])
            foreach (Rect y in slots[b])
                if (x.Overlaps(y)) return false;
            return true;
        }
    }

    /// <summary>The four slots of one kind of tree, each up to two rectangles of the atlas.</summary>
    public sealed class TreeCells
    {
        public TreeCells(Rect[] barkDeep, Rect[] barkWarm, Rect[] leafDeep, Rect[] leafFresh)
        {
            BarkDeep = barkDeep;
            BarkWarm = barkWarm;
            LeafDeep = leafDeep;
            LeafFresh = leafFresh;
        }

        public Rect[] BarkDeep { get; }
        public Rect[] BarkWarm { get; }
        public Rect[] LeafDeep { get; }
        public Rect[] LeafFresh { get; }
    }
}
