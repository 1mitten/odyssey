#nullable enable

using System;
using System.Collections.Generic;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Draws the land beyond the board: the ground carried out to the fog, and the wood carried
    /// out with it.
    ///
    /// <see cref="SkirtLayout"/> decides where everything goes; this decides what it is made of,
    /// and it decides by **looking at the board** rather than by being told. The surface layer,
    /// the terrain module, its tint, which trees grow and how thickly are all measured off the
    /// generated map, which is what makes the surround a continuation rather than a second
    /// opinion: a bare board gets bare ground, a wooded one gets woodland at its own density, and
    /// the ruined city gets whatever the ruined city has. Nothing here needs changing when a new
    /// map type is added.
    ///
    /// Built once and then submitted unchanged every frame. The skirt has no simulation behind it
    /// so nothing can dirty it; the instance arrays are filled at startup and the per-frame cost
    /// is the submission alone.
    ///
    /// Culling is by batch bounds. Batches are split by strip for the ground and by a sector grid
    /// for the trees, so that the half of the surround behind the camera is rejected by Unity
    /// rather than drawn. That split is the only reason the draw-call count is what it is; one
    /// batch per material would be fewer calls and far more work.
    /// </summary>
    public sealed class TerrainSkirt : IDisposable
    {
        /// <summary>Matches <see cref="ChunkRenderer.MaxInstancesPerCall"/>, for the same reason.</summary>
        public const int MaxInstancesPerCall = ChunkRenderer.MaxInstancesPerCall;

        /// <summary>
        /// How wide a tree sector is — the spatial half of a batch key, and so one of the two
        /// knobs that decide how many instanced calls the near wood costs.
        ///
        /// <para><b>800 m since 2026-09-21; 400 m from 2026-09-20; 80 m before that.</b> The
        /// original finding stands and is worth restating, because it is the reason this is a
        /// batch-count problem at all: the surround was 3.65 ms of a 5 ms frame on the played
        /// meadow and the trees were all of it — its ground and its tufts are free to within
        /// noise. The instinct is that four thousand trees are too many trees, and the measurement
        /// says otherwise. Dropping the 2,577 hill trees changed nothing at all (5.37 ms against
        /// 5.04), while the cost tracked the <b>batch</b> count almost exactly — 760 batches
        /// 3.5 ms, 438 batches 2.1 ms, about 4.6 us a batch. It is per-call overhead, not trees
        /// and not fill; at 640 x 480 there are not enough pixels on the board for fill to cost
        /// this.</para>
        ///
        /// <para><b>Where 400 came from, and why it was not the floor.</b> At 80 m the ring fell
        /// into hundreds of sectors holding a handful of trees each; 400 m rode the same trees in
        /// 266 batches instead of 760 and took the meadow 5.04 → 2.52 ms. That pass then recorded
        /// that the ladder "saturates" past 400 m and that the remaining floor was "the variants,
        /// themes, mute steps and parts, which no sector size can merge". **The first half was
        /// right and the second was not measured.** The census of 2026-09-21 found the wood at
        /// <b>230 batches over 115 sectors, mean 17 trees a call, 192 of the 230 holding fewer
        /// than 32</b> — and only 4 mute steps, 2 tints and 1 part in the whole key. The space
        /// was still doing the splitting, and one more step of it was free.</para>
        ///
        /// <para><b>The sweep, one built world, one run</b>
        /// (<c>FrameTimeTests.TheSurroundSectorSweep</c>), surround section in milliseconds:
        /// 400/800 m <b>1.160</b> at 266 batches, 800/1600 <b>1.003</b> at 230, 1600/3200
        /// <b>1.002</b> at 230, and a single 100 km sector also 230. So 800 m takes the last of
        /// what space is worth and everything past it is nothing. <b>The rest of the saving is in
        /// <see cref="TreeVariantSlots"/></b>, because <c>SectorOf</c> folds the variant into the
        /// sector number and a sixteen-kind wood therefore cannot fall below sixteen batches a
        /// spatial cell however coarse the cells are.</para>
        ///
        /// <para><b>The trade, stated.</b> A coarser sector is a looser <c>worldBounds</c>, so
        /// less of the wood frustum-culls and more of it is submitted every frame. That is the
        /// right way round here and was measured to be, twice: the draw the culling saves is
        /// cheaper than the per-batch cost of being able to save it. If a weaker machine ever
        /// reverses that, this is one number to turn and <see cref="NearWoodDensityPercent"/> is
        /// another.</para>
        ///
        /// <para><b>Settable since 2026-09-21, and a <c>const</c> until then.</b> A const cannot
        /// be swept, so the number was chosen once by hand and then believed for a day longer than
        /// it deserved. Nothing but the sweep writes it; it restores the default in a
        /// <c>finally</c>, because it is process-wide and a leak would read as a performance
        /// change rather than as a test fault.</para>
        /// </summary>
        public static float TreeSectorMetres { get; set; } = DefaultTreeSectorMetres;

        /// <summary>What the game ships with.</summary>
        public const float DefaultTreeSectorMetres = 800f;

        /// <summary>
        /// How wide a sector of the far wood is. Twice the near one, and swept with it.
        ///
        /// <para>The far band covers some four million square metres, and before any of this work
        /// it drew <b>1,154 surround batches</b> against 72 before the hill wood existed. Coarse
        /// sectors cull worse — a sector this wide is nearly always partly on screen, so its
        /// instances are submitted whether or not they can be seen — and that is the right trade
        /// out here and the wrong one at the rim. There are only a few thousand far trees in all,
        /// so submitting them costs less than the draw calls that culling them finely would.</para>
        ///
        /// <para>1600 m since 2026-09-21, with the near sector. Nothing further was on offer: the
        /// 1600/3200 rung measured identically to 800/1600, to three decimal places.</para>
        /// </summary>
        public static float FarTreeSectorMetres { get; set; } = DefaultFarTreeSectorMetres;

        /// <summary>What the game ships with.</summary>
        public const float DefaultFarTreeSectorMetres = 1600f;

        /// <summary>
        /// How many kinds of tree the far wood picks between.
        ///
        /// <para>Fewer than the near wood, because variety is part of a batch key and at 300 m
        /// and beyond a tree is a few dozen pixels: nobody can tell one conifer from another,
        /// and every extra kind multiplies the batch count for a difference that cannot be
        /// seen. Four is enough that a silhouette does not visibly repeat along a ridge.</para>
        /// </summary>
        public const int FarTreeVariants = 4;

        /// <summary>
        /// How many kinds of tree the near wood picks between, sampled by frequency off the board.
        ///
        /// <para><b>Eight since 2026-09-21, down from sixteen, and it is the single largest saving
        /// available in this class.</b> The variant is folded into the sector number by
        /// <c>SectorOf</c>, so it multiplies the batch count whatever the sector size — which is
        /// exactly why the sector ladder saturated. Swept on one built world in one run, surround
        /// section in milliseconds at 800/1600 m sectors: <b>×16 = 1.003</b> at 230 batches,
        /// <b>×8 = 0.619</b> at 151, <b>×6 = 0.515</b> at 129, <b>×4 = 0.392</b> at 104. Against
        /// the shipped 400 m ×16 at 1.160 ms and 266 batches, the pair of changes is
        /// <b>1.160 → 0.619 ms, a 47 per cent cut, with the same 3,907 trees standing.</b></para>
        ///
        /// <para><b>What a slot actually is, which decides how far this can go.</b> A slot is a
        /// (module, theme) pair sampled from the board's own wood by frequency. On the played
        /// meadow the census finds <b>2 tints and 16 themes</b> — so sixteen slots were buying
        /// sixteen colour palettes over two silhouettes, not sixteen kinds of tree. Halving them
        /// halves the palettes and touches the silhouettes not at all, which is why 8 is the
        /// recommendation and not 4: 4 is available, measured and cheaper again, and it is a look
        /// decision that wants an eye on the horizon rather than another reading.</para>
        ///
        /// <para>Settable for the sweep, like the two sector sizes, and written by nothing
        /// else.</para>
        /// </summary>
        public static int TreeVariantSlots { get; set; } = DefaultTreeVariantSlots;

        /// <summary>What the game ships with.</summary>
        public const int DefaultTreeVariantSlots = 8;

        sealed class Batch
        {
            public Mesh? Mesh;
            public int Submesh;
            public Material? Material;
            public Matrix4x4[] Matrices = Array.Empty<Matrix4x4>();
            public int Count;
            public Bounds Bounds;
            public bool CastsShadow;

            /// <summary>The four tree colours, when this batch is a tree. Null for everything else.</summary>
            public MaterialPropertyBlock? Props;

            // The key this batch was opened under, kept only so the census can say which field of
            // it is doing the splitting. Four ints against a Matrix4x4 array; it costs nothing and
            // it is the difference between knowing which knob to turn and guessing.
            public int Sector, MuteStep, Part, TintCode, Theme;

            public void Add(in Matrix4x4 matrix)
            {
                if (Count == Matrices.Length)
                    Array.Resize(ref Matrices, Matrices.Length == 0 ? 64 : Matrices.Length * 2);
                Matrices[Count++] = matrix;
            }
        }

        readonly WorldRenderModel _model;
        readonly MaterialCache _materials;
        readonly List<Batch> _ground = new List<Batch>();
        readonly List<Batch> _trees = new List<Batch>();
        readonly List<Batch> _tufts = new List<Batch>();
        readonly List<SkirtLayout.SkirtTile> _tiles = new List<SkirtLayout.SkirtTile>();
        readonly List<SkirtLayout.SkirtTree> _scattered = new List<SkirtLayout.SkirtTree>();
        readonly List<SkirtLayout.FarTree> _farScattered = new List<SkirtLayout.FarTree>();

        /// <summary>
        /// Which batch an instance belongs in. A tuple rather than a packed integer on purpose:
        /// the sector index runs into the millions once the tree grid is laid over a board, and a
        /// hand-packed key that silently overflowed into the shadow flag would merge two batches
        /// that differ in whether they cast — a fault that renders perfectly and looks like a
        /// lighting bug.
        /// </summary>
        readonly Dictionary<(int Sector, int Mute, int Part, int Tint, bool Shadow, bool Foliage, int Theme), Batch> _index =
            new Dictionary<(int, int, int, int, bool, bool, int), Batch>();

        public TerrainSkirt(WorldRenderModel model, MaterialCache materials)
        {
            _model = model;
            _materials = materials;
        }

        /// <summary>Off draws nothing at all, and the board ends in mid-air as it used to.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// How much of the board's own tree density the surround gets, as a percentage. 100 is a
        /// true continuation; lower is the knob to turn on a machine that cannot afford the wood,
        /// since the trees are the only part of the skirt with a real vertex cost.
        /// </summary>
        public int TreeDensityPercent { get; set; } = 100;

        /// <summary>
        /// How much of that again the <b>near</b> wood gets - the ring of full-size trees just
        /// outside the rim, as against the hill wood behind it. <b>100 by default: this is a
        /// lever, not a setting, and nothing turns it today.</b>
        ///
        /// <para>It is separate from <see cref="TreeDensityPercent"/> because the two halves of
        /// the wood cost very different amounts and only one of them is doing the job. Dropping
        /// the 2,577 hill trees changed the frame by nothing measurable; dropping the 1,592 near
        /// trees took the meadow from 5.04 ms to 2.26. The hill wood is what sells distance and
        /// it is nearly free; the near ring is the detailed half.</para>
        ///
        /// <para>It exists unturned because the cost turned out not to be the trees at all - see
        /// <see cref="TreeSectorMetres"/>, where cutting the batch count from 760 to 272 bought
        /// the same two milliseconds with the wood left whole. Thinning was measured too (30 per
        /// cent took 1,592 trees to 435 and saved 1.5 ms) and is strictly worse: it costs the
        /// look and buys less. Keep it here as the second knob for a machine that still cannot
        /// afford the wood after the first one.</para>
        /// </summary>
        public int NearWoodDensityPercent { get; set; } = 100;

        /// <summary>
        /// Whether the wood carries on over the background hills.
        ///
        /// <para>Off gives the bare hillsides the surround had before, which is the comparison
        /// this switch exists to make: the far wood is the only thing out there with a known
        /// size, so it is the only thing telling the eye how far away a hill is.</para>
        /// </summary>
        public bool HillTrees { get; set; } = true;

        /// <summary>
        /// Tufts of grass per hundred cells in the first ring, kept in step with the mesher's own
        /// density by <see cref="ChunkRenderer.ScatterDensity"/>.
        ///
        /// Without this the tufts stopped dead at the rim, which drew precisely the line the
        /// surround exists to rub out: bare ground meeting a field of grass, straight, for three
        /// hundred metres. They fade to nothing over the first ring, because a tuft is a
        /// close-range detail and nobody is inspecting the ground twenty metres past the board.
        /// </summary>
        public int TuftDensity { get; set; } = 60;

        public bool SubmitToGpu { get; set; } = true;
        public int GameObjectLayer { get; set; }
        public bool CastShadows { get; set; } = true;

        /// <summary>The layer the surround's ground sits in: the board's own commonest surface.</summary>
        public int SurfaceLayer { get; private set; }

        /// <summary>Trees per thousand cells measured off the board. Zero on a bare map.</summary>
        public int MeasuredTreeDensity { get; private set; }

        public bool Built { get; private set; }

        public int GroundInstances { get; private set; }
        public int TreeInstances { get; private set; }

        /// <summary>The wood on the hills, counted apart from the near wood so the two costs
        /// can be told apart in a log or a milestone report.</summary>
        public int FarTreeInstances { get; private set; }
        public int TuftInstances { get; private set; }

        // ---- last-frame measurements, rolled into the renderer's own readout ----

        public int DrawCalls { get; private set; }
        public int InstancesDrawn { get; private set; }
        public int BatchesDrawn { get; private set; }

        // ---- the batch census, for deciding what to do about the surround's cost ----

        /// <summary>Which of the three lists a census is being taken of.</summary>
        public enum SkirtPart { Ground, Trees, Tufts }

        /// <summary>A batch holding fewer than this is not paying for the call it costs.</summary>
        public const int ThinBatch = 32;

        /// <summary>
        /// What one of the three lists is made of: how many batches, how many instances ride in
        /// them, how many of those batches are thin, and how big the largest is.
        ///
        /// <para><b>Why the distribution and not just the count.</b> §6c established that this
        /// pass costs its <em>batch</em> count and not its tree count, and cut the count once by
        /// coarsening the spatial half of the batch key. What it could not say is whether the
        /// remaining batches are full or nearly empty, and those want opposite fixes: a list of
        /// full batches is paying for content and can only be made cheaper by having less of it,
        /// while a list of thin ones is paying for a key that splits too finely and can be made
        /// cheaper for nothing. One number cannot tell the two apart.</para>
        /// </summary>
        public readonly struct Census
        {
            public Census(int batches, int instances, int thin, int largest)
            {
                Batches = batches;
                Instances = instances;
                Thin = thin;
                Largest = largest;
            }

            public readonly int Batches;
            public readonly int Instances;

            /// <summary>Batches holding fewer than <see cref="ThinBatch"/> instances.</summary>
            public readonly int Thin;

            public readonly int Largest;

            /// <summary>Mean instances a batch, or 0 where there are none.</summary>
            public float Mean => Batches > 0 ? (float)Instances / Batches : 0f;

            public override string ToString() =>
                $"{Batches} batches, {Instances} instances, mean {Mean:0.0}, " +
                $"{Thin} thin (<{ThinBatch}), largest {Largest}";
        }

        /// <summary>Take the census of one of the three lists. Needs <see cref="Build"/> first.</summary>
        public Census CensusOf(SkirtPart part)
        {
            List<Batch> batches = part switch
            {
                SkirtPart.Ground => _ground,
                SkirtPart.Trees => _trees,
                _ => _tufts,
            };

            int instances = 0, thin = 0, largest = 0;
            for (int i = 0; i < batches.Count; i++)
            {
                int count = batches[i].Count;
                instances += count;
                if (count < ThinBatch) thin++;
                if (count > largest) largest = count;
            }
            return new Census(batches.Count, instances, thin, largest);
        }

        /// <summary>
        /// How many distinct values of each key field one of the lists uses, as
        /// <c>sectors x mutes x parts x tints x themes</c>.
        ///
        /// <para>The batch count is the size of the product of these, restricted to the
        /// combinations that actually occur. So this is the line that says which factor to attack:
        /// a field with one value is not costing anything, and a field with sixteen is costing
        /// sixteenfold whatever sits beside it.</para>
        /// </summary>
        public string KeySpreadOf(SkirtPart part)
        {
            List<Batch> batches = part switch
            {
                SkirtPart.Ground => _ground,
                SkirtPart.Trees => _trees,
                _ => _tufts,
            };

            var sectors = new HashSet<int>();
            var mutes = new HashSet<int>();
            var parts = new HashSet<int>();
            var tints = new HashSet<int>();
            var themes = new HashSet<int>();
            for (int i = 0; i < batches.Count; i++)
            {
                sectors.Add(batches[i].Sector);
                mutes.Add(batches[i].MuteStep);
                parts.Add(batches[i].Part);
                tints.Add(batches[i].TintCode);
                themes.Add(batches[i].Theme);
            }

            return $"{sectors.Count} sectors x {mutes.Count} mutes x {parts.Count} parts x " +
                   $"{tints.Count} tints x {themes.Count} themes " +
                   $"= {sectors.Count * mutes.Count * parts.Count * tints.Count * themes.Count} " +
                   $"possible, {batches.Count} used";
        }

        /// <summary>
        /// Measure the board and lay the surround out. Idempotent; call it again after a rebuild
        /// of the world to pick up a different map.
        /// </summary>
        public void Build()
        {
            Clear();
            Built = true;

            if (!Survey(out int surfaceLayer, out ushort terrain, out int treeDensity,
                    out int[] treeModules, out int[] treeThemes))
                return;

            SurfaceLayer = surfaceLayer;
            MeasuredTreeDensity = treeDensity;

            BuildGround(terrain);
            BuildTrees(treeModules, treeThemes);
        }

        // ------------------------------------------------------------- survey

        /// <summary>
        /// What the board actually is: its commonest surface level, the terrain on it, and the
        /// trees growing out of it.
        ///
        /// Commonest, not highest or lowest. The skirt is one flat sheet — it has no heightfield
        /// and wants none, since a stepped surround would read as terraces rather than as
        /// landscape — so the honest choice is the level most of the board is at, which on a flat
        /// map is every column of it and on a varied one is the level the rim mostly sits at.
        /// </summary>
        bool Survey(out int surfaceLayer, out ushort terrain, out int treeDensityPerMille,
            out int[] treeModules, out int[] treeThemes)
        {
            surfaceLayer = 0;
            terrain = 0;
            treeDensityPerMille = 0;
            treeModules = Array.Empty<int>();
            treeThemes = Array.Empty<int>();

            GridSize size = _model.Size;
            if (size.SizeX <= 0 || size.SizeZ <= 0 || size.SizeY <= 0) return false;

            var layerCounts = new int[size.SizeY];
            int columns = 0;

            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
            {
                int top = TopSolid(x, z);
                if (top < 0) continue;
                layerCounts[top]++;
                columns++;
            }

            if (columns == 0) return false;

            int best = 0;
            for (int y = 1; y < layerCounts.Length; y++)
                if (layerCounts[y] > layerCounts[best]) best = y;
            surfaceLayer = best;

            // The commonest terrain at that level, and the trees standing on it. Both are counted
            // over the whole board rather than over its rim: a clearing at the start would
            // otherwise make the surround bald on the side the colony happens to have landed.
            var terrainCounts = new Dictionary<ushort, int>();
            var treeCounts = new Dictionary<long, int>();
            int surfaceCells = 0, trees = 0;

            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
            {
                if (TopSolid(x, z) != surfaceLayer) continue;
                surfaceCells++;

                int index = size.Index(x, z, surfaceLayer);
                ushort code = _model.Terrain(index);
                terrainCounts.TryGetValue(code, out int seen);
                terrainCounts[code] = seen + 1;

                if (surfaceLayer + 1 >= size.SizeY) continue;
                int above = index + size.LayerStride;
                ushort edifice = _model.EdificeDef(above);
                if (!NaturalContent.IsTree(edifice)) continue;

                trees++;
                int module = _model.EdificeModule(above);
                if (module == 0) continue;
                // The tree's own tint, which since TreePalette is which stand of wood it grows
                // in. Sampling it here is what makes the surround the same wood as the board:
                // the tally is taken by frequency, so a board whose rim is mostly birch puts
                // mostly birch in the ring outside it without anything having to be told.
                // The tint takes the low thirty-two bits, not the low twenty it used to: a tree
                // code carries its theme in bits 16 and up, so twenty bits would have thrown the
                // theme away and made every tree out here the same colour.
                // The theme, not the tint code: the code says only which species a tree is now,
                // and what the surround has to mirror is which *colours* the board is wearing.
                long key = ((long)module << 32) | (uint)TreeLook.ThemeFor(x, z, edifice);
                treeCounts.TryGetValue(key, out int count);
                treeCounts[key] = count + 1;
            }

            if (surfaceCells == 0) return false;

            int bestCount = -1;
            foreach (var pair in terrainCounts)
                if (pair.Value > bestCount)
                {
                    bestCount = pair.Value;
                    terrain = pair.Key;
                }

            treeDensityPerMille = (int)(trees * 1000L / surfaceCells);
            SampleTreeVariants(treeCounts, trees, out treeModules, out treeThemes);
            return true;
        }

        int TopSolid(int x, int z)
        {
            GridSize size = _model.Size;
            for (int y = size.SizeY - 1; y >= 0; y--)
                if (_model.IsSolid(size.Index(x, z, y))) return y;
            return -1;
        }

        /// <summary>
        /// Turn the tally of trees on the board into a flat table the layout can index into.
        ///
        /// Filled by frequency, so a wood that is four parts conifer to one part broadleaf comes
        /// out of the table in the same proportion and the surround is the same wood rather than
        /// an even mixture of the kinds that happen to exist.
        /// </summary>
        static void SampleTreeVariants(Dictionary<long, int> counts, int total,
            out int[] modules, out int[] themes)
        {
            modules = Array.Empty<int>();
            themes = Array.Empty<int>();
            if (counts.Count == 0 || total <= 0) return;

            var slotModules = new List<int>(TreeVariantSlots);
            var slotThemes = new List<int>(TreeVariantSlots);

            foreach (var pair in counts)
            {
                int share = Mathf.Max(1, Mathf.RoundToInt(pair.Value / (float)total * TreeVariantSlots));
                for (int i = 0; i < share && slotModules.Count < TreeVariantSlots; i++)
                {
                    slotModules.Add((int)(pair.Key >> 32));
                    slotThemes.Add((int)(pair.Key & 0xFFFFFFFFL));
                }
            }

            modules = slotModules.ToArray();
            themes = slotThemes.ToArray();
        }

        // ------------------------------------------------------------- ground

        void BuildGround(ushort terrain)
        {
            int module = _model.TerrainModuleFor(terrain);
            if (module == 0) return;

            ResolvedModule resolved = _model.Library[module];
            if (resolved.IsEmpty) return;

            int tintCode = TintCode.Terrain(terrain);
            float surfaceY = SurfaceLayer * CellMetrics.SizeY;

            SkirtLayout.BuildTiles(_model.Size, _tiles);

            SkirtLayout.SkirtRect board = SkirtLayout.Board(_model.Size);

            for (int i = 0; i < _tiles.Count; i++)
            {
                SkirtLayout.SkirtTile tile = _tiles[i];
                float outside = board.DistanceOutside(tile.CentreX, tile.CentreZ);

                // How deep the tile has to be so that it still meets its neighbours.
                //
                // This is the one thing the surround needs that the board does not. A board cell is
                // 2.5 m across and 3 m deep, so a neighbour can never drop far enough to show a gap
                // beneath it. A surround tile is up to 120 m across, and over that distance the
                // hills can fall tens of metres - so a 3 m box would leave an open trench to the
                // sky along every tile boundary. The tile is therefore sunk to reach below whatever
                // its neighbours can do, which costs nothing at all: it is the same instance with a
                // different scale, and all of it is underground.
                float depth = TileDepth(tile, outside);

                // Drape works from the bottom of the box up, so the origin is set so that the TOP
                // still lands on the field, wherever the bottom ends up.
                var placement = GroundRelief.DrapeSurround(
                    new Vector3(tile.CentreX, surfaceY + CellMetrics.SizeY - depth, tile.CentreZ),
                    outside) *
                    Matrix4x4.Scale(new Vector3(
                        tile.SizeX / CellMetrics.SizeXZ,
                        depth / CellMetrics.SizeY,
                        tile.SizeZ / CellMetrics.SizeXZ));

                float height = GroundRelief.SurroundHeightAt(tile.CentreX, tile.CentreZ, outside);
                float reach = GroundRelief.SurroundMaxSlope(outside) *
                              Mathf.Max(tile.SizeX, tile.SizeZ) * 0.5f;
                var bounds = new Bounds(
                    new Vector3(tile.CentreX,
                        surfaceY + CellMetrics.SizeY + height - depth * 0.5f,
                        tile.CentreZ),
                    new Vector3(tile.SizeX, depth + 2f * reach, tile.SizeZ));

                // Terrain never casts: the argument is the one in ChunkRenderer, and it applies
                // with more force out here, where the ground is a single flat sheet whose only
                // possible shadow is on itself.
                int sector = tile.Band * SkirtLayout.StripCount + tile.Strip;
                Add(_ground, resolved, tintCode, tile.MuteStep, sector,
                    castsShadow: false, foliage: false, placement, bounds);

                if (tile.Band == 0) EmitTufts(tile, surfaceY);
            }

            GroundInstances = CountOf(_ground);
            TuftInstances = CountOf(_tufts);
        }

        /// <summary>
        /// How deep a surround tile has to be so that it always reaches below its neighbours.
        ///
        /// The full height of the layer, plus however far the land can fall across one tile, plus
        /// the second-order disagreement between two tangent planes that wide. Generous on purpose:
        /// every metre of it is below ground and costs nothing, whereas being a metre short is a
        /// hole through to the sky along a tile edge.
        /// </summary>
        static float TileDepth(SkirtLayout.SkirtTile tile, float metresOutsideBoard)
        {
            float span = Mathf.Max(tile.SizeX, tile.SizeZ);
            float slope = GroundRelief.SurroundMaxSlope(metresOutsideBoard);
            return CellMetrics.SizeY + 2f * slope * span + 1f;
        }

        /// <summary>
        /// Strew tufts over one tile of the first ring, which is exactly one cell, by the same
        /// hash of the same coordinates the mesher uses inside the board.
        ///
        /// Same hash, same salts, same placement: the field does not change character at the rim,
        /// it only thins out. Continuing the cell coordinates past the board is what makes that
        /// free - the tuft at cell (-1, 40) is the one that cell would have had if the board had
        /// been one wider.
        /// </summary>
        void EmitTufts(SkirtLayout.SkirtTile tile, float surfaceY)
        {
            if (TuftDensity <= 0) return;

            int[] modules = TuftModules();
            if (modules.Length == 0) return;

            int x = Mathf.FloorToInt(tile.CentreX / CellMetrics.SizeXZ);
            int z = Mathf.FloorToInt(tile.CentreZ / CellMetrics.SizeXZ);

            float distance = SkirtLayout.Board(_model.Size).DistanceOutside(tile.CentreX, tile.CentreZ);
            int density = Mathf.RoundToInt(TuftDensity * SkirtLayout.TuftDensityScale(distance));
            int count = GroundScatter.CountFor(x, z, density);
            if (count == 0) return;

            var surface = new Vector3(tile.CentreX, surfaceY + CellMetrics.SizeY, tile.CentreZ);
            float tileOutside = SkirtLayout.Board(_model.Size)
                .DistanceOutside(tile.CentreX, tile.CentreZ);

            for (int slot = 0; slot < count; slot++)
            {
                GroundScatter.Placement(x, z, slot,
                    out float offsetX, out float offsetZ, out float yaw, out float scale);

                int module = modules[GroundScatter.VariantFor(x, z, slot, modules.Length)];
                ResolvedModule tuft = _model.Library[module];
                if (tuft.IsEmpty) continue;

                Vector3 at = GroundRelief.LiftSurround(
                    surface + new Vector3(offsetX * CellMetrics.SizeXZ, 0f, offsetZ * CellMetrics.SizeXZ),
                    tileOutside);
                var placement = Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, 0f),
                    new Vector3(scale, scale, scale));

                Bounds local = tuft.Bounds;
                var bounds = new Bounds(at + local.center * scale, local.size * scale);

                Add(_tufts, tuft, TintCode.FoliageBase, tile.MuteStep,
                    SectorOf(tile.CentreX, tile.CentreZ, modules.Length, 0),
                    castsShadow: false, foliage: true, placement, bounds);
            }
        }

        int[]? _tuftModules;

        /// <summary>
        /// The tuft meshes, resolved once, keeping only the ones that found real art.
        ///
        /// Dropping the rest is the same judgement <c>ChunkMesher</c> makes and for the same
        /// reason: a box where a wall should be is still a wall, and a box where a tuft of grass
        /// should be is a strewing of grey cubes. A clone without the packs gets bare ground,
        /// inside the board and outside it alike.
        /// </summary>
        int[] TuftModules()
        {
            if (_tuftModules != null) return _tuftModules;

            var usable = new List<int>();
            for (int variant = 0; variant < ModuleIds.GrassTuftCount; variant++)
            {
                int module = _model.Library.Resolve(ModuleIds.GrassTuft(variant), ModuleShape.Pillar);
                ResolvedModule resolved = _model.Library[module];
                if (resolved.UsesArt && !resolved.IsEmpty) usable.Add(module);
            }

            return _tuftModules = usable.ToArray();
        }

        // -------------------------------------------------------------- trees

        void BuildTrees(int[] treeModules, int[] treeThemes)
        {
            if (treeModules.Length == 0 || TreeDensityPercent <= 0) return;

            GridSize size = _model.Size;
            SkirtLayout.BuildTrees(size, MeasuredTreeDensity, treeModules.Length,
                TreeDensityPercent * NearWoodDensityPercent * 0.0001f, _scattered);
            if (_scattered.Count == 0) return;

            // Trees stand on top of the surface cell, exactly as the mesher stands them on the
            // board: the layer above the solid one, at its floor.
            float standY = (SurfaceLayer + 1) * CellMetrics.SizeY;
            SkirtLayout.SkirtRect board = SkirtLayout.Board(_model.Size);

            for (int i = 0; i < _scattered.Count; i++)
            {
                SkirtLayout.SkirtTree tree = _scattered[i];
                int module = treeModules[tree.Variant];
                ResolvedModule resolved = _model.Library[module];
                if (resolved.IsEmpty) continue;

                float x = tree.CellX * CellMetrics.SizeXZ + CellMetrics.HalfXZ;
                float z = tree.CellZ * CellMetrics.SizeXZ + CellMetrics.HalfXZ;

                // On the hillside rather than through it. A tree is lifted and never draped: a
                // sheared trunk would lean, and trees on a slope grow up.
                Vector3 foot = GroundRelief.LiftSurround(
                    new Vector3(x, standY, z), board.DistanceOutside(x, z));
                var placement = Matrix4x4.Translate(foot);

                Bounds local = resolved.Bounds;
                var bounds = new Bounds(foot + local.center, local.size);

                int sector = SectorOf(x, z, treeModules.Length, tree.Variant);
                int theme = treeThemes[tree.Variant];
                Add(_trees, resolved, TintCode.Tree(TreePalette.At(theme).Species), tree.MuteStep,
                    sector, castsShadow: tree.CastsShadow, foliage: false, placement, bounds, theme);
            }

            // Counted before the far wood is added, so the two numbers are separable in the log
            // and in a milestone report. Order matters: the far count is the difference.
            TreeInstances = CountOf(_trees);
            BuildFarTrees(treeModules, treeThemes, standY, board);
            FarTreeInstances = CountOf(_trees) - TreeInstances;
        }

        /// <summary>
        /// The wood on the hills, from the edge of the near wood out to
        /// <see cref="SkirtLayout.FarTreeRangeMetres"/>.
        ///
        /// <para>The near wood stops 90 m out, where the hills have risen about six of their fifty
        /// metres, so every hill in the background was bare. A bare hillside has nothing of known
        /// size on it, and without that the eye cannot place it: it reads as a green backdrop
        /// rather than as land a long way off. These trees are the scale reference, and they are
        /// the whole of what makes the distance read as distance.</para>
        ///
        /// <para>Three things keep it cheap, and all three matter more out here than near the rim.
        /// They are batched on a much coarser sector, because 80 m sectors over four million
        /// square metres would be several hundred draw calls to no purpose when the whole far wood
        /// is one distant thing. They take the deepest mute step outright, which collapses a batch
        /// key and is what the desaturation ramp would have given them anyway. And they cast no
        /// shadow at all, which they could not do regardless with a 50 m shadow distance.</para>
        /// </summary>
        void BuildFarTrees(int[] treeModules, int[] treeThemes, float standY,
            SkirtLayout.SkirtRect board)
        {
            if (!HillTrees) return;

            int variants = Mathf.Min(treeModules.Length, FarTreeVariants);
            SkirtLayout.BuildFarTrees(_model.Size, variants,
                TreeDensityPercent * 0.01f, _farScattered);
            if (_farScattered.Count == 0) return;

            for (int i = 0; i < _farScattered.Count; i++)
            {
                SkirtLayout.FarTree tree = _farScattered[i];
                int module = treeModules[tree.Variant];
                ResolvedModule resolved = _model.Library[module];
                if (resolved.IsEmpty) continue;

                // Lifted onto the hill, never draped along it, exactly as the near wood is: a
                // sheared trunk leans, and trees on a slope grow up.
                Vector3 foot = GroundRelief.LiftSurround(
                    new Vector3(tree.X, standY, tree.Z), board.DistanceOutside(tree.X, tree.Z));
                var placement = Matrix4x4.Translate(foot);

                Bounds local = resolved.Bounds;
                var bounds = new Bounds(foot + local.center, local.size);

                int sector = FarSectorOf(tree.X, tree.Z, variants, tree.Variant);
                int theme = treeThemes[tree.Variant];
                Add(_trees, resolved, TintCode.Tree(TreePalette.At(theme).Species),
                    SkirtLayout.MuteSteps, sector,
                    castsShadow: false, foliage: false, placement, bounds, theme);
            }

        }

        static int SectorOf(float x, float z, int variants, int variant)
        {
            int sx = Mathf.FloorToInt(x / TreeSectorMetres) + 512;
            int sz = Mathf.FloorToInt(z / TreeSectorMetres) + 512;
            return ((sz * 1024 + sx) * Mathf.Max(1, variants)) + variant;
        }

        /// <summary>
        /// The far wood's own sectors, offset out of the near wood's numbering so the two can
        /// never share a batch and quietly undo the coarser grouping.
        /// </summary>
        static int FarSectorOf(float x, float z, int variants, int variant)
        {
            int sx = Mathf.FloorToInt(x / FarTreeSectorMetres) + 512;
            int sz = Mathf.FloorToInt(z / FarTreeSectorMetres) + 512;
            return -1 - (((sz * 1024 + sx) * Mathf.Max(1, variants)) + variant);
        }

        // ------------------------------------------------------------ batches

        void Add(List<Batch> into, ResolvedModule resolved, int tintCode, int muteStep, int sector,
            bool castsShadow, bool foliage, in Matrix4x4 placement, in Bounds bounds, int theme = -1)
        {
            ModulePart[] parts = resolved.Parts;
            for (int p = 0; p < parts.Length; p++)
            {
                // The theme is part of the key as well as the tint code, because a tree code names
                // only its species: two themes of one species would otherwise share a batch and the
                // surround would go back to being one colour.
                var key = (sector, muteStep, p, tintCode, castsShadow, foliage, theme);
                if (!_index.TryGetValue(key, out Batch? batch))
                {
                    batch = NewBatch(parts[p], tintCode, muteStep, castsShadow, foliage, bounds, theme);
                    batch.Sector = sector;
                    batch.MuteStep = muteStep;
                    batch.Part = p;
                    batch.TintCode = tintCode;
                    batch.Theme = theme;
                    _index.Add(key, batch);
                    into.Add(batch);
                }
                else
                {
                    batch.Bounds.Encapsulate(bounds);
                }

                batch.Add(placement * parts[p].Local);
            }
        }

        Batch NewBatch(ModulePart part, int tintCode, int muteStep, bool castsShadow, bool foliage,
            in Bounds bounds, int theme)
        {
            ChunkRenderer.ResolveColour(tintCode, part.IsFallback, 1f, out Color tint, out Color emission);
            tint = SkirtLayout.Mute(tint, muteStep);
            emission = SkirtLayout.Mute(emission, muteStep);

            // A tree out here is repainted exactly as one on the board is, haze and all — see
            // ChunkRenderer.DrawBuckets for why a tree cannot take a single tint. Without this
            // the wood would change colour at the rim, which is the one thing the surround exists
            // to prevent.
            Material? painted = TintCode.IsTree(tintCode) && !part.IsFallback
                ? _materials.Trees.For(part.Material, TintCode.TreeSpeciesOf(tintCode), 1f, muteStep)
                : null;

            return new Batch
            {
                Mesh = part.Mesh,
                Submesh = part.Submesh,
                Material = painted ?? _materials.Get(part.Material, tint, emission, ghost: false, alpha: 1f,
                    foliage: foliage),
                Props = painted != null && theme >= 0
                    ? _materials.Trees.UniformProps(theme, muteStep)
                    : null,
                Bounds = bounds,
                CastsShadow = castsShadow,
            };
        }

        static int CountOf(List<Batch> batches)
        {
            int total = 0;
            for (int i = 0; i < batches.Count; i++) total += batches[i].Count;
            return total;
        }

        // ------------------------------------------------------------- render

        /// <summary>
        /// Submit the surround for one frame.
        ///
        /// The slice rule is the board's rule, applied to a thing that has no layers: the ground
        /// is drawn while the player is looking at the surface or above it, and vanishes the
        /// moment they slice below it, because a sheet of landscape sitting over an open mine
        /// would bury exactly the thing they went down to look at. The trees stand a layer higher
        /// again and follow the same rule one layer up, so they never obscure the layer being
        /// worked on — which is the one promise the slice makes.
        /// </summary>
        public void Render(int activeLayer)
        {
            DrawCalls = 0;
            InstancesDrawn = 0;
            BatchesDrawn = 0;

            if (!Enabled) return;
            if (!Built) Build();
            if (activeLayer < SurfaceLayer) return;

            Submit(_ground);
            if (activeLayer > SurfaceLayer)
            {
                Submit(_trees);
                Submit(_tufts);
            }
        }

        void Submit(List<Batch> batches)
        {
            for (int i = 0; i < batches.Count; i++)
            {
                Batch batch = batches[i];
                if (batch.Count == 0 || batch.Mesh == null || batch.Material == null) continue;
                BatchesDrawn++;

                var rp = new RenderParams(batch.Material)
                {
                    worldBounds = batch.Bounds,
                    layer = GameObjectLayer,
                    receiveShadows = true,
                    shadowCastingMode = CastShadows && batch.CastsShadow
                        ? ShadowCastingMode.On
                        : ShadowCastingMode.Off,
                    matProps = batch.Props,
                };

                int drawn = 0;
                while (drawn < batch.Count)
                {
                    int n = Mathf.Min(MaxInstancesPerCall, batch.Count - drawn);
                    if (SubmitToGpu)
                        Graphics.RenderMeshInstanced(rp, batch.Mesh, batch.Submesh, batch.Matrices, n, drawn);
                    drawn += n;
                    DrawCalls++;
                }

                InstancesDrawn += batch.Count;
            }
        }

        void Clear()
        {
            _ground.Clear();
            _trees.Clear();
            _tufts.Clear();
            _tiles.Clear();
            _scattered.Clear();
            _index.Clear();
            GroundInstances = 0;
            TreeInstances = 0;
            FarTreeInstances = 0;
            TuftInstances = 0;
            MeasuredTreeDensity = 0;
            SurfaceLayer = 0;
        }

        public void Dispose() => Clear();
    }
}
