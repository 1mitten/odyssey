#nullable enable
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Which of <see cref="TreePalette"/>'s themes a tree wears, and where the woods it belongs to
    /// begin and end.
    ///
    /// <para><b>A stand, not a tree.</b> This is the whole performance argument of the feature and
    /// it is worth stating plainly, because the obvious implementation is the expensive one.
    /// Drawing is bucketed per <i>(module, part, tint)</i> inside a chunk, so the number of draw
    /// calls a wood costs is the number of distinct colours <b>in that chunk</b>, not on the board.
    /// A colour rolled per tree puts every theme in the table into every chunk that holds a dozen
    /// trees, and a chunk of woodland holds a hundred and sixty: measured, a per-tree colour
    /// saturates the whole palette in the worst chunk, so the bill is the length of the table. A
    /// colour dealt to a stand puts two or three in a chunk however long the table grows, so the
    /// table is free to grow. <c>TreeBucketTests</c> measures both rather than arguing either.</para>
    ///
    /// <para>It is also simply what a wood looks like. Trees of one species grow together; a
    /// hillside of birch beside a stand of pine reads as landscape, where a uniform confetti of
    /// every colour at once reads as a fault.</para>
    ///
    /// <para><b>Everything here is a hash of the cell's own coordinates</b>, for the reason
    /// <see cref="GroundLook"/> records: a chunk is re-meshed whenever anything in it changes, so
    /// a stream of random numbers would recolour the wood every time a colonist felled a tree
    /// twenty metres away. A hash depends on nothing but the cell, so the wood looks the same
    /// after a rebuild, after a reload and on another machine. Nothing here is saved, hashed or
    /// visible to the simulation — a tree's colour is drawing, exactly as a colonist's face is.
    /// </para>
    /// </summary>
    public static class TreeLook
    {
        /// <summary>
        /// How far apart the stand sites are sown, in cells. 40 cells is 100 m.
        ///
        /// <para><b>Chosen against the chunk, and the number was measured rather than picked.</b>
        /// A chunk is 25 cells square and the bucket bill is (stands overlapping a chunk) x (the
        /// two species), so the stand has to be large against the chunk or the saving disappears.
        /// At 20 cells <c>TreeBucketTests</c> measured 6.75 tree buckets a chunk against the 11 a
        /// colour-per-tree would have cost — a saving so thin it would not have been worth the
        /// feature. At 40 it is about half that, because a chunk then overlaps two squares rather
        /// than five.</para>
        ///
        /// <para>The other end is the board: the played meadow is 120 cells, which at this size is
        /// nine stands, and nine stands dealt two species each still put most of the palette on
        /// screen at once. Much larger and a board would carry three colours, which is the
        /// dullness this feature exists to fix.</para>
        /// </summary>
        public const int StandCell = 40;

        /// <summary>
        /// Whether a wood is divided into stands at all.
        ///
        /// <para>Off, every tree of a kind takes the first theme of its species, so the board
        /// carries exactly the two tree tints it carried before this feature existed. That is what
        /// makes the "before" column of <c>TreeCheck</c> a real before: switching off the
        /// <em>materials</em> alone leaves the mesher splitting buckets by stand, so the draw-call
        /// comparison would have been the feature measured against itself — which is what the
        /// first run of that sheet reported, identically, three times.</para>
        ///
        /// <para>Settable only so that a diagnostic can measure the alternative, the same bargain
        /// <c>MaterialCache.FoliageQueue</c> makes. The game runs with it on.</para>
        /// </summary>
        public static bool Stands { get; set; } = true;

        const uint SaltSiteX = 0x5E11u;
        const uint SaltSiteZ = 0x77C3u;
        const uint SaltConifer = 0x1A93u;
        const uint SaltBroadleaf = 0x3C57u;

        /// <summary>
        /// Which stand the cell belongs to, as a packed id.
        ///
        /// <para><b>Cellular, not a grid.</b> Quantising the cell straight to a stand square is
        /// one line shorter and draws stands with ruler-straight edges running north-south and
        /// east-west across the whole board — which nothing in a landscape does, and which reads
        /// at once as a bug. Each stand square instead sows one site at a jittered point inside
        /// itself, and a tree joins the nearest site; the boundary between two stands is then the
        /// perpendicular bisector of two arbitrary points, so it wanders. The nine candidates are
        /// the site in the cell's own square and its eight neighbours, which is enough: a site
        /// cannot leave its square, so no site further out can be nearer than the worst of these.
        /// </para>
        /// </summary>
        public static int Stand(int x, int z)
        {
            int cx = FloorDiv(x, StandCell), cz = FloorDiv(z, StandCell);
            long best = long.MaxValue;
            int bestX = cx, bestZ = cz;

            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int sx = cx + dx, sz = cz + dz;
                int siteX = sx * StandCell + (int)(GroundScatter.Hash(sx, sz, SaltSiteX) % (uint)StandCell);
                int siteZ = sz * StandCell + (int)(GroundScatter.Hash(sx, sz, SaltSiteZ) % (uint)StandCell);
                long ex = x - siteX, ez = z - siteZ;
                long d = ex * ex + ez * ez;

                // The tie-break is on the square's coordinates rather than on iteration order, so
                // the answer cannot depend on which way round the loops happen to run.
                if (d > best) continue;
                if (d == best && (sz > bestZ || (sz == bestZ && sx >= bestX))) continue;
                best = d;
                bestX = sx;
                bestZ = sz;
            }

            return Pack(bestX, bestZ);
        }

        /// <summary>
        /// The theme a tree of this species standing here wears: an index into
        /// <see cref="TreePalette"/>.
        ///
        /// <para>Each stand is dealt one conifer theme and one broadleaf theme, and a tree takes
        /// the one for its own kind. That is what lets the generator go on mixing pine and
        /// broadleaf inside a wood — which it does, per tree, at
        /// <c>NaturalMapGenDef.broadleafPerMille</c> — without a stand of Scots pine sprouting an
        /// oak in oak's colours. It costs the stand two buckets rather than one, which is the
        /// whole of the price.</para>
        /// </summary>
        public static int Theme(int x, int z, TreeSpecies species)
        {
            int[] rows = TreePalette.For(species);
            if (rows.Length == 0) return 0;
            if (!Stands) return rows[0];
            int stand = Stand(x, z);
            uint salt = species == TreeSpecies.Conifer ? SaltConifer : SaltBroadleaf;
            return rows[GroundScatter.Hash(stand, stand >> 16, salt) % (uint)rows.Length];
        }

        /// <summary>The theme for whatever tree edifice this is, by its def.</summary>
        public static int ThemeFor(int x, int z, ushort edifice) =>
            Theme(x, z, SpeciesOf(edifice));

        /// <summary>
        /// Which kind of tree a natural edifice is.
        ///
        /// Anything that is not the conifer is drawn as a broadleaf, which is the honest default:
        /// a second conifer added later would name itself here, and a new broadleaf needs no edit
        /// at all.
        /// </summary>
        public static TreeSpecies SpeciesOf(ushort edifice) =>
            edifice == NaturalContent.EdificeTreeConifer ? TreeSpecies.Conifer : TreeSpecies.Broadleaf;

        static int Pack(int x, int z) => (x << 16) ^ (z & 0xFFFF);

        /// <summary>
        /// Floor division, because <c>/</c> rounds towards zero and the board's coordinates are
        /// never negative but a stand's neighbours are: at x = 3 the square to the west is -1, and
        /// integer division would put it in the same square as the cell itself.
        /// </summary>
        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -(((-a) + b - 1) / b);
    }
}
