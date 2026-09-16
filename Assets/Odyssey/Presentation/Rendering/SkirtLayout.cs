#nullable enable

using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Where the ground outside the board goes.
    ///
    /// The board is finite and the simulation has no cells beyond it, so past the rim there was
    /// nothing at all: the meadow ended in mid-air over the sky gradient and the whole thing read
    /// as a board game on a table rather than as a clearing in a landscape. The skirt is the
    /// answer — decoration only, drawn outside the grid, in nobody's save and in no system's way.
    ///
    /// **It is not a second world.** Nothing here is a cell, nothing is pathable, nothing can be
    /// clicked, built on, felled or stood in. <see cref="SlicePicker"/> still cannot return a cell
    /// that does not exist, which is why the skirt needs no defence against being selected. The
    /// only contract it has with the simulation is that it looks like more of the same.
    ///
    /// Three ideas carry the whole design:
    ///
    /// - **Rings of tiles, coarsening outwards.** The first ring is one cell per tile, so the
    ///   seam is drawn from the very geometry the board's own rim is drawn from and cannot show a
    ///   join. Each ring out from there uses tiles several times larger, because a tile 400 m away
    ///   is a few pixels tall and its texture scale is not something the eye can check. That is
    ///   what keeps 1.2 km of ground under eight thousand instances instead of under two hundred
    ///   thousand.
    /// - **Strips, not a grid.** Each ring is covered by four rectangles — west, east, south,
    ///   north — each divided into a whole number of equal tiles that exactly fill it. No tile
    ///   grid has to line up with any other, so there are no gaps, no overlapping coplanar ground
    ///   (which would z-fight) and no constraint on the board's dimensions.
    /// - **Everything is a hash of its own position.** As with <see cref="GroundScatter"/>, the
    ///   layout takes no random stream and holds no state, so it is identical on every machine and
    ///   after every reload, and can be tested by arithmetic rather than by screenshot.
    ///
    /// The muting is the one deliberate departure from "seamless". The playable area has to stay
    /// legible as the playable area, so the skirt desaturates slightly with distance — but it
    /// ramps from nothing at the rim over the first ring rather than switching at the boundary,
    /// because a step change at the join is exactly the tell this whole feature exists to remove.
    /// </summary>
    public static class SkirtLayout
    {
        /// <summary>One ring of ground: how big its tiles are, and how far out it reaches.</summary>
        public readonly struct Band
        {
            public Band(float tileMetres, float depthMetres)
            {
                TileMetres = tileMetres;
                DepthMetres = depthMetres;
            }

            /// <summary>Roughly how wide a tile in this ring is. Rounded to fill each strip exactly.</summary>
            public readonly float TileMetres;

            /// <summary>How far beyond the ring inside it this one reaches.</summary>
            public readonly float DepthMetres;
        }

        /// <summary>
        /// The rings, innermost first.
        ///
        /// The first is exactly one cell per tile (ADR 0002 fixes a cell at 2.5 m), which is what
        /// makes the seam invisible.
        ///
        /// The outer two were once 40 m and 120 m, which was right while the surround was one flat
        /// sheet and wrong the moment it grew hills. Each tile is drawn as a single tilted plane,
        /// and two neighbouring planes part company across their shared edge by roughly
        /// <c>(A/2) * (2*pi*L/P)^2</c> - which at 120 m tiles is twenty metres, and reads as long
        /// diagonal cracks scored across the hillsides. Halving the far tiles costs a few thousand
        /// instances out of twenty-five thousand and no extra draw call at all, because they batch
        /// exactly as they did before. The outermost reaches 1,220 m beyond the rim, comfortably
        /// past the 1,100 m at which linear fog has faded everything into the sky, so the skirt
        /// ends where nothing can see it end rather than at a visible edge.
        /// </summary>
        public static readonly Band[] Bands =
        {
            new Band(CellMetrics.SizeXZ, 20f),
            new Band(10f, 120f),
            new Band(20f, 480f),
            new Band(60f, 600f),
        };

        /// <summary>How far the skirt reaches beyond the board, in metres.</summary>
        public static float TotalDepthMetres
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < Bands.Length; i++) total += Bands[i].DepthMetres;
                return total;
            }
        }

        // ------------------------------------------------------------- muting

        /// <summary>
        /// How many distinct shades the skirt is drawn in. Each one costs a material and a draw
        /// call per strip it appears in, so this is small on purpose: three steps over the ramp
        /// are enough that the change reads as haze rather than as banding, because fog and
        /// distance are doing most of the work already.
        /// </summary>
        public const int MuteSteps = 3;

        /// <summary>Over what distance the muting reaches full strength. Inside the first ring.</summary>
        public const float MuteRampMetres = 18f;

        /// <summary>How far towards flat grey the furthest step goes. Subtle by intent.</summary>
        public const float MaxMute = 0.30f;

        /// <summary>Which shade a point this far outside the board is drawn in.</summary>
        public static int MuteStepAt(float distanceMetres)
        {
            if (distanceMetres <= 0f) return 0;
            int step = (int)(distanceMetres / MuteRampMetres * MuteSteps);
            return step < MuteSteps - 1 ? step : MuteSteps - 1;
        }

        /// <summary>
        /// The colour a board tint is drawn in at a given step: desaturated towards its own
        /// luminance and taken down a shade, so the board stays the brighter, more saturated thing
        /// on screen without the surround looking like a different material.
        /// </summary>
        public static Color Mute(Color colour, int step)
        {
            if (step <= 0) return colour;
            float t = MuteSteps > 1 ? step / (float)(MuteSteps - 1) : 1f;
            float mute = t * MaxMute;

            float grey = colour.r * 0.299f + colour.g * 0.587f + colour.b * 0.114f;
            var target = new Color(grey * 0.92f, grey * 0.92f, grey * 0.92f, colour.a);
            return Color.Lerp(colour, target, mute);
        }

        // -------------------------------------------------------------- rects

        /// <summary>An axis-aligned patch of ground in world metres. Z, not Unity's Rect y.</summary>
        public readonly struct SkirtRect
        {
            public SkirtRect(float minX, float minZ, float maxX, float maxZ)
            {
                MinX = minX;
                MinZ = minZ;
                MaxX = maxX;
                MaxZ = maxZ;
            }

            public readonly float MinX, MinZ, MaxX, MaxZ;

            public float Width => MaxX - MinX;
            public float Depth => MaxZ - MinZ;

            public SkirtRect Expand(float by) =>
                new SkirtRect(MinX - by, MinZ - by, MaxX + by, MaxZ + by);

            public bool Contains(float x, float z) =>
                x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

            /// <summary>How far a point lies outside this rect. Zero inside it.</summary>
            public float DistanceOutside(float x, float z)
            {
                float dx = Mathf.Max(Mathf.Max(MinX - x, x - MaxX), 0f);
                float dz = Mathf.Max(Mathf.Max(MinZ - z, z - MaxZ), 0f);
                return Mathf.Sqrt(dx * dx + dz * dz);
            }
        }

        /// <summary>The board's own footprint in world metres.</summary>
        public static SkirtRect Board(GridSize size) =>
            new SkirtRect(0f, 0f, size.SizeX * CellMetrics.SizeXZ, size.SizeZ * CellMetrics.SizeXZ);

        /// <summary>Everything the skirt covers, board included.</summary>
        public static SkirtRect Outer(GridSize size) => Board(size).Expand(TotalDepthMetres);

        // -------------------------------------------------------------- tiles

        /// <summary>One slab of ground outside the board.</summary>
        public readonly struct SkirtTile
        {
            public SkirtTile(float centreX, float centreZ, float sizeX, float sizeZ,
                int band, int strip, int muteStep)
            {
                CentreX = centreX;
                CentreZ = centreZ;
                SizeX = sizeX;
                SizeZ = sizeZ;
                Band = band;
                Strip = strip;
                MuteStep = muteStep;
            }

            public readonly float CentreX, CentreZ, SizeX, SizeZ;
            public readonly int Band, Strip, MuteStep;
        }

        /// <summary>The four strips a ring is covered by. Corners belong to west and east.</summary>
        public const int StripCount = 4;

        /// <summary>
        /// Lay the whole skirt out, innermost ring first.
        ///
        /// Deterministic and allocation-free past the list itself: called once when the world is
        /// built, and never again unless the board is rebuilt.
        /// </summary>
        public static void BuildTiles(GridSize size, List<SkirtTile> into)
        {
            into.Clear();
            SkirtRect board = Board(size);
            SkirtRect inner = board;

            for (int band = 0; band < Bands.Length; band++)
            {
                float depth = Bands[band].DepthMetres;
                float tile = Bands[band].TileMetres;
                SkirtRect outer = inner.Expand(depth);

                // West and east take the full height of the ring, so they own the corners; south
                // and north take only the width of the ring inside them. Between them they cover
                // the ring exactly once.
                AddStrip(into, board, band, 0, outer.MinX, outer.MinZ, inner.MinX, outer.MaxZ, tile);
                AddStrip(into, board, band, 1, inner.MaxX, outer.MinZ, outer.MaxX, outer.MaxZ, tile);
                AddStrip(into, board, band, 2, inner.MinX, outer.MinZ, inner.MaxX, inner.MinZ, tile);
                AddStrip(into, board, band, 3, inner.MinX, inner.MaxZ, inner.MaxX, outer.MaxZ, tile);

                inner = outer;
            }
        }

        static void AddStrip(List<SkirtTile> into, SkirtRect board, int band, int strip,
            float minX, float minZ, float maxX, float maxZ, float tile)
        {
            float width = maxX - minX, depth = maxZ - minZ;
            if (width <= 0f || depth <= 0f) return;

            // A whole number of equal tiles that exactly fills the strip. Rounding rather than
            // flooring keeps the tiles near their nominal size; filling exactly is what keeps the
            // rings from gapping or overlapping when the board is not a round number of tiles.
            int countX = Mathf.Max(1, Mathf.RoundToInt(width / tile));
            int countZ = Mathf.Max(1, Mathf.RoundToInt(depth / tile));
            float sizeX = width / countX, sizeZ = depth / countZ;

            for (int iz = 0; iz < countZ; iz++)
            for (int ix = 0; ix < countX; ix++)
            {
                float centreX = minX + (ix + 0.5f) * sizeX;
                float centreZ = minZ + (iz + 0.5f) * sizeZ;
                into.Add(new SkirtTile(centreX, centreZ, sizeX, sizeZ, band, strip,
                    MuteStepAt(board.DistanceOutside(centreX, centreZ))));
            }
        }

        // -------------------------------------------------------------- tufts

        /// <summary>
        /// How much of the board's tuft density a point this far out gets: all of it at the rim,
        /// none of it by the end of the first ring.
        ///
        /// The ramp is the whole point. Tufts that simply stopped at the boundary drew a straight
        /// line three hundred metres long between a field of grass and bare ground, which is a
        /// better advertisement for where the board ends than the mid-air edge ever was. Fading
        /// them out costs nothing, because a clump is a close-range detail that has stopped being
        /// resolvable long before the ramp ends.
        /// </summary>
        public static float TuftDensityScale(float distanceMetres)
        {
            if (distanceMetres <= 0f) return 1f;
            float depth = Bands[0].DepthMetres;
            if (distanceMetres >= depth) return 0f;
            return 1f - distanceMetres / depth;
        }

        // -------------------------------------------------------------- trees

        /// <summary>
        /// How far out trees are strewn. Past this the ground carries on alone, which costs
        /// nothing and is not noticed: at 90 m a tree is already small, and the ring beyond it is
        /// heading into fog.
        /// </summary>
        public const float TreeRangeMetres = 90f;

        /// <summary>Within this distance of the rim the wood is as dense as the board's own.</summary>
        public const float TreeFullDensityMetres = 15f;

        /// <summary>What the density has fallen to at <see cref="TreeRangeMetres"/>.</summary>
        public const float TreeFarDensity = 0.15f;

        /// <summary>
        /// How much of the board's own tree density a point this far out gets.
        ///
        /// Full at the rim so the wood reads as continuous, thinning outwards so the cost is paid
        /// where it can be seen. It is not ecology, it is drawing the near trees properly and
        /// spending nothing on the far ones.
        /// </summary>
        public static float TreeDensityScale(float distanceMetres)
        {
            if (distanceMetres <= TreeFullDensityMetres) return 1f;
            if (distanceMetres >= TreeRangeMetres) return TreeFarDensity;
            float t = (distanceMetres - TreeFullDensityMetres)
                      / (TreeRangeMetres - TreeFullDensityMetres);
            return Mathf.Lerp(1f, TreeFarDensity, t);
        }

        /// <summary>
        /// How near the rim a skirt tree still casts a shadow.
        ///
        /// Trees just outside the board throw shadows onto the board itself, and that is worth
        /// having: it is the clearest single sign that the wood does not stop at the rim. Further
        /// out a shadow falls on undifferentiated ground nobody is reading, so the whole rest of
        /// the wood — thousands of trees — stays out of the shadow pass entirely.
        ///
        /// Ten metres and not more, because the sun is steeply overhead (72 degrees in
        /// <c>PlayScene.BuildLighting</c>) and a tree that tall casts only a couple of metres. A
        /// generous range here would put a thousand extra casters in the shadow pass to no visible
        /// effect whatever, which is the expensive way of changing nothing.
        /// </summary>
        public const float TreeShadowRangeMetres = 10f;

        /// <summary>One tree standing outside the board.</summary>
        public readonly struct SkirtTree
        {
            public SkirtTree(int cellX, int cellZ, int variant, int muteStep, bool castsShadow)
            {
                CellX = cellX;
                CellZ = cellZ;
                Variant = variant;
                MuteStep = muteStep;
                CastsShadow = castsShadow;
            }

            /// <summary>Cell coordinates continued past the board, so they go negative. Not a cell.</summary>
            public readonly int CellX, CellZ;

            public readonly int Variant, MuteStep;
            public readonly bool CastsShadow;
        }

        // Arbitrary and fixed, and distinct from GroundScatter's, so that the tufts inside the
        // board and the trees outside it do not agree about which cells are interesting.
        const uint SaltPresence = 0x7F4Au;
        const uint SaltVariant = 0x2E9Bu;

        /// <summary>
        /// Strew trees over the skirt, at the board's own density graded down by distance.
        ///
        /// <paramref name="densityPerMille"/> is measured off the board rather than configured, so
        /// a bare board gets a bare surround and a wooded one gets woodland — the surround is
        /// always a continuation of whatever was actually generated, not a second opinion about
        /// what the map should look like.
        /// </summary>
        public static void BuildTrees(GridSize size, int densityPerMille, int variants,
            float densityScale, List<SkirtTree> into)
        {
            into.Clear();
            if (densityPerMille <= 0 || variants <= 0 || densityScale <= 0f) return;

            SkirtRect board = Board(size);
            int reach = Mathf.CeilToInt(TreeRangeMetres / CellMetrics.SizeXZ);

            for (int z = -reach; z < size.SizeZ + reach; z++)
            for (int x = -reach; x < size.SizeX + reach; x++)
            {
                // Inside the board the simulation owns the trees, and it has already drawn them.
                if (x >= 0 && x < size.SizeX && z >= 0 && z < size.SizeZ) continue;

                float centreX = x * CellMetrics.SizeXZ + CellMetrics.HalfXZ;
                float centreZ = z * CellMetrics.SizeXZ + CellMetrics.HalfXZ;
                float distance = board.DistanceOutside(centreX, centreZ);
                if (distance > TreeRangeMetres) continue;

                float chance = densityPerMille * 0.001f * TreeDensityScale(distance) * densityScale;
                if (GroundScatter.Unit(x, z, SaltPresence) >= chance) continue;

                into.Add(new SkirtTree(x, z,
                    (int)(GroundScatter.Hash(x, z, SaltVariant) % (uint)variants),
                    MuteStepAt(distance),
                    distance <= TreeShadowRangeMetres));
            }
        }
    }
}
