#nullable enable
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Which of <see cref="TreePalette"/>'s themes a tree wears, and how thoroughly a wood is
    /// mixed.
    ///
    /// <para><b>A stand deals a handful of colours, and every tree in it takes one.</b> The first
    /// version dealt <i>one</i> colour to a stand, which made a wood of uniform patches; the
    /// owner's answer was <i>"but also really mix them in together"</i>. So a stand now draws
    /// <see cref="ThemesPerStand"/> themes per species out of the whole table and each tree picks
    /// one of them by its own hash. Neighbouring trees differ; neighbouring woods differ more,
    /// because they drew different handfuls. <b>One slot of every handful is reserved for a bright
    /// leaf</b>, which is what actually lifted the wood — see <see cref="ThemesOfStand"/>.</para>
    ///
    /// <para><b>Why a handful and not the whole table.</b> This is the performance argument and it
    /// is the only reason stands exist at all. Drawing is bucketed per <i>(module, part, tint)</i>
    /// inside a chunk, so the draw calls a wood costs are the number of distinct colours <b>in that
    /// chunk</b>. A chunk of woodland holds about 160 trees and the table holds 240 themes, so a
    /// colour rolled freely per tree would put essentially the whole table in every chunk — the
    /// bill would be the length of the table, and the table could never grow again. Dealing a stand
    /// a fixed handful caps a chunk at (stands it overlaps) x (two species) x this number, however
    /// long the table becomes. <c>TreeBucketTests</c> measures all of it.</para>
    ///
    /// <para><b>Everything here is a hash of the cell's own coordinates</b>, for the reason
    /// <see cref="GroundLook"/> records: a chunk is re-meshed whenever anything in it changes, so a
    /// stream of random numbers would recolour the wood every time a colonist felled a tree twenty
    /// metres away. A hash depends on nothing but the cell, so the wood looks the same after a
    /// rebuild, after a reload and on another machine. Nothing here is saved, hashed or visible to
    /// the simulation — a tree's colour is drawing, exactly as a colonist's face is.</para>
    /// </summary>
    public static class TreeLook
    {
        /// <summary>
        /// How far apart the stand sites are sown, in cells. 40 cells is 100 m.
        ///
        /// <para>Chosen against the chunk, which is 25 cells square, and the number was measured
        /// rather than picked: at 20 cells a chunk overlaps about five stand squares and the wood
        /// cost 6.75 buckets a chunk, at 40 it overlaps about two and cost 4.34. The other end of
        /// the trade is the board — the played meadow is 120 cells, so this is nine stands — and
        /// with each stand dealing <see cref="ThemesPerStand"/> colours per species that is plenty
        /// of character across a board.</para>
        /// </summary>
        public const int StandCell = 40;

        /// <summary>
        /// How many themes a stand deals each species. This is the mixing knob, and it is the one
        /// number that decides what the feature costs.
        ///
        /// <para>Every step of it multiplies the tree buckets in a chunk, so it is bought with draw
        /// calls and nothing else: <c>TreeBucketTests</c> prints the bill and <c>TreeCheck</c>
        /// measures it on the board the game loads. Four is what the owner's <i>"really mix them in
        /// together"</i> bought, and the measurement beside it is what says whether a fifth is
        /// affordable.</para>
        /// </summary>
        public const int ThemesPerStand = 4;

        const uint SaltSiteX = 0x5E11u;
        const uint SaltSiteZ = 0x77C3u;
        const uint SaltConifer = 0x1A93u;
        const uint SaltBroadleaf = 0x3C57u;
        const uint SaltPick = 0x6BD5u;

        /// <summary>
        /// Whether a wood is divided into stands at all.
        ///
        /// <para>Off, every tree of a kind takes the first theme of its species, so the board
        /// carries exactly the two tree tints it carried before this feature existed. That is what
        /// makes the "before" column of <c>TreeCheck</c> a real before: switching off the
        /// <em>materials</em> alone leaves the mesher splitting buckets by stand, so the draw-call
        /// comparison would have been the feature measured against itself — which is what the first
        /// run of that sheet reported, identically, three times.</para>
        ///
        /// <para>Settable only so that a diagnostic can measure the alternative, the same bargain
        /// <c>MaterialCache.FoliageQueue</c> makes. The game runs with it on.</para>
        /// </summary>
        public static bool Stands { get; set; } = true;

        /// <summary>
        /// Which stand the cell belongs to, as a packed id.
        ///
        /// <para><b>Cellular, not a grid.</b> Quantising the cell straight to a stand square is one
        /// line shorter and draws stands with ruler-straight edges running north-south and
        /// east-west across the whole board — which nothing in a landscape does, and which reads at
        /// once as a bug. Each stand square instead sows one site at a jittered point inside itself
        /// and a tree joins the nearest site; the boundary between two stands is then the
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
        /// The <see cref="ThemesPerStand"/> themes this stand deals to trees of this species,
        /// written into <paramref name="into"/>, which must be at least that long. Returns how many
        /// were written, which is fewer only if the palette itself is smaller.
        ///
        /// <para><b>The bark and the leaf are drawn separately</b>, each without replacement by
        /// walking its own table from a hashed start at a hashed stride. A stride sharing no factor
        /// with the table's length visits every row before repeating, so the handful is always
        /// distinct — four independent hashes would hand the same colour out twice about one stand
        /// in ten and quietly narrow the mixing.</para>
        ///
        /// <para><b>And the first leaf of every stand is a bright one.</b> That is not a flourish;
        /// it is the answer to the owner's *"add some bright colours into the leaf — it seems a bit
        /// dull still"*, and adding bright rows to the table was tried first and did not work.
        /// Seven bright tones among twenty-one means a handful of four draws about one on average
        /// and often draws none, so the board came back warmer and no brighter: **adding a colour
        /// to a table dilutes it, it does not lift it.** Reserving a slot makes every wood carry a
        /// bright note, at no cost at all — the handful is the same size, so the buckets are the
        /// same buckets.</para>
        /// </summary>
        public static int ThemesOfStand(int stand, TreeSpecies species, int[] into)
        {
            TreeTone[] barks = TreePalette.Barks(species);
            TreeTone[] leaves = TreePalette.Leaves(species);
            if (barks.Length == 0 || leaves.Length == 0) return 0;

            int want = ThemesPerStand;
            if (want > into.Length) want = into.Length;
            if (want <= 0) return 0;

            uint salt = species == TreeSpecies.Conifer ? SaltConifer : SaltBroadleaf;
            uint h = GroundScatter.Hash(stand, stand >> 16, salt);

            int barkStart = (int)(h % (uint)barks.Length);
            int barkStride = Coprime((int)((h >> 5) % (uint)barks.Length), barks.Length);
            int leafStart = (int)((h >> 10) % (uint)leaves.Length);
            int leafStride = Coprime((int)((h >> 18) % (uint)leaves.Length), leaves.Length);

            int[] bright = TreePalette.BrightLeaves(species);

            for (int i = 0; i < want; i++)
            {
                int bark = (barkStart + i * barkStride) % barks.Length;
                int leaf = (leafStart + i * leafStride) % leaves.Length;

                // Slot zero is the reserved bright one. It is a slot rather than a coin flip so
                // that a stand's mixture is the same every time it is asked for, which is what the
                // whole file turns on.
                if (i == 0 && bright.Length > 0)
                    leaf = bright[(h >> 26) % (uint)bright.Length];

                into[i] = TreePalette.ThemeOf(species, bark, leaf);
            }

            return want;
        }

        /// <summary>
        /// The theme a tree of this species standing here wears: an index into
        /// <see cref="TreePalette"/>.
        ///
        /// <para>The stand decides which handful; the tree's own cell decides which of the handful.
        /// That is the whole of the mixing — two trees side by side are different colours, and the
        /// wood they stand in is a different mixture from the wood over the hill.</para>
        /// </summary>
        public static int Theme(int x, int z, TreeSpecies species)
        {
            int[] rows = TreePalette.For(species);
            if (rows.Length == 0) return 0;
            if (!Stands) return rows[0];

            int[] handful = Handful;
            int n = ThemesOfStand(Stand(x, z), species, handful);
            if (n <= 0) return rows[0];
            return handful[GroundScatter.Hash(x, z, SaltPick) % (uint)n];
        }

        /// <summary>
        /// Scratch for the stand's handful, so that meshing a chunk of woodland does not allocate
        /// an array per tree. The mesher is single-threaded by the architecture decision, and this
        /// is only ever read back on the line after it is written.
        /// </summary>
        [System.ThreadStatic] static int[]? _handful;

        static int[] Handful => _handful ??= new int[ThemesPerStand];

        /// <summary>The theme for whatever tree edifice this is, by its def.</summary>
        public static int ThemeFor(int x, int z, ushort edifice) =>
            Theme(x, z, SpeciesOf(edifice));

        /// <summary>
        /// Which kind of tree a natural edifice is.
        ///
        /// Anything that is not the conifer is drawn as a broadleaf, which is the honest default: a
        /// second conifer added later would name itself here, and a new broadleaf needs no edit at
        /// all.
        /// </summary>
        public static TreeSpecies SpeciesOf(ushort edifice) =>
            edifice == NaturalContent.EdificeTreeConifer ? TreeSpecies.Conifer : TreeSpecies.Broadleaf;

        /// <summary>
        /// The nearest stride at or above <paramref name="want"/> that shares no factor with
        /// <paramref name="count"/>, so that walking by it visits every row.
        /// </summary>
        static int Coprime(int want, int count)
        {
            if (count <= 1) return 1;
            for (int stride = want < 1 ? 1 : want; stride < want + count + 1; stride++)
                if (Gcd(stride, count) == 1)
                    return stride;
            return 1;
        }

        static int Gcd(int a, int b)
        {
            while (b != 0) { int t = a % b; a = b; b = t; }
            return a < 0 ? -a : a;
        }

        static int Pack(int x, int z) => (x << 16) ^ (z & 0xFFFF);

        /// <summary>
        /// Floor division, because <c>/</c> rounds towards zero and the board's coordinates are
        /// never negative but a stand's neighbours are: at x = 3 the square to the west is -1, and
        /// integer division would put it in the same square as the cell itself.
        /// </summary>
        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -(((-a) + b - 1) / b);
    }
}
