#nullable enable
using System.Collections.Generic;
using Odyssey.Presentation.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Turns one chunk of the cell mirror into instanced draw buckets.
    ///
    /// Everything interesting about how the world looks is decided here, and all of it is per
    /// cell arithmetic with no allocation in the steady state:
    ///
    /// - **Solid strata are face-culled.** A rock cell with six solid neighbours is not drawn.
    ///   Underground layers are mostly solid, so this is the difference between drawing a chunk
    ///   and drawing its surface.
    /// - **Walls are drawn on faces, not in cells.** A wall occupies a whole 2.5 m cell, so
    ///   drawing a wall-shaped box would give a 2.5 m thick slab. Instead each exposed vertical
    ///   face of a wall cell takes a panel, which is what makes a stamped shell read as a
    ///   building from outside and as a room from inside.
    /// - **Doors get one leaf, in the middle.** Two panels on opposite faces of the same cell
    ///   would read as an airlock.
    /// - **Stairs climb.** The lower half sits on the floor, the upper half 1.5 m up, both facing
    ///   the direction of travel — which is the kit's native geometry (<c>e-01</c>).
    /// - **Everything fixed to the grid is draped; only what moves over it is lifted.** A lift
    ///   takes a single height from a single point, so two neighbouring pieces of one wall or one
    ///   floor sit at heights differing by the relief's slope across a whole cell and the seam
    ///   between them steps. A drape is the tangent plane of the same field, so neighbours
    ///   disagree only by its curvature — second order, about 11 mm — and the seam closes. Ground,
    ///   banks, water, floors, walls, doors, stairs, ladders and pillars are therefore all draped;
    ///   grass tufts, dropped items, figures and cursors are lifted, because they stand at a point
    ///   and share no edge with anything.
    /// </summary>
    public sealed class ChunkMesher
    {
        readonly Dictionary<long, int> _bodyIndex = new Dictionary<long, int>();
        readonly Dictionary<long, int> _roofIndex = new Dictionary<long, int>();
        readonly Dictionary<long, int> _wallIndex = new Dictionary<long, int>();
        readonly Dictionary<long, int> _stumpIndex = new Dictionary<long, int>();

        /// <summary>Whether the cell being emitted is an upper storey (design 42 §4).</summary>
        bool _stacked;
        readonly WorldRenderModel _model;

        public ChunkMesher(WorldRenderModel model) => _model = model;

        /// <summary>
        /// One cell's building — and, with <paramref name="terrain"/>, its ground — meshed on its
        /// own into <paramref name="batch"/>, by the same emitters <see cref="Mesh"/> runs over a
        /// chunk (<c>docs/design/44-selection-highlight.md</c> §3).
        ///
        /// <para><b>For the selection highlight, which has to draw exactly what is drawn.</b> A wall
        /// is a core and a panel per exposed face, a bed three parts, a tree a trunk and a crown in
        /// its stand's colours; a second copy of any of those rules would drift from the first the
        /// day either was touched. The skin is built too, so a skinned surface comes back as a
        /// mesh. Leaves nothing behind that <see cref="Mesh"/> relies on: it resets every piece of
        /// per-chunk state it reads.</para>
        /// </summary>
        public void MeshCell(ChunkBatch batch, int index, bool terrain)
        {
            CellRef cell = _model.Size.FromIndex(index);
            int x = cell.X, z = cell.Z, y = cell.Y;

            batch.ChunkIndex = -1;
            batch.Layer = y;
            _rampCacheIndex = -1;
            _dipIndex = -1;
            batch.Bounds = ChunkWorldBounds(x, z, y, x + 1, z + 1);
            if (GroundSkin.Enabled) FillCornerRelief(Mathf.Max(0, x - 1), Mathf.Max(0, z - 1), x + 2, z + 2);
            batch.Clear();
            _bodyIndex.Clear();
            _roofIndex.Clear();
            _wallIndex.Clear();
            _stumpIndex.Clear();
            for (int i = 0; i < batch.Body.Count; i++) _bodyIndex[KeyOf(batch.Body[i])] = i;
            for (int i = 0; i < batch.Roof.Count; i++) _roofIndex[KeyOf(batch.Roof[i])] = i;
            for (int i = 0; i < batch.Walls.Count; i++) _wallIndex[KeyOf(batch.Walls[i])] = i;
            for (int i = 0; i < batch.Stumps.Count; i++) _stumpIndex[KeyOf(batch.Stumps[i])] = i;

            _stacked = _model.IsStackedAt(index);
            if (terrain) EmitTerrain(batch, index, x, z, y);
            EmitEdifice(batch, index, x, z, y);
            if (terrain) batch.Skin.Build(batch.Bounds);

            _rampCacheIndex = -1;
            _dipIndex = -1;
        }

        public void Mesh(ChunkBatch batch, int chunkIndex)
        {
            _model.ChunkBounds(chunkIndex, out int x0, out int z0, out int y, out int x1, out int z1);

            batch.ChunkIndex = chunkIndex;
            batch.Layer = y;
            _rampCacheIndex = -1;
            _dipIndex = -1;
            batch.Bounds = ChunkWorldBounds(x0, z0, y, x1, z1);
            if (GroundSkin.Enabled) FillCornerRelief(x0, z0, x1, z1);
            batch.Clear();
            _bodyIndex.Clear();
            _roofIndex.Clear();
            _wallIndex.Clear();
            _stumpIndex.Clear();
            for (int i = 0; i < batch.Body.Count; i++)
                _bodyIndex[KeyOf(batch.Body[i])] = i;
            for (int i = 0; i < batch.Roof.Count; i++)
                _roofIndex[KeyOf(batch.Roof[i])] = i;
            for (int i = 0; i < batch.Walls.Count; i++)
                _wallIndex[KeyOf(batch.Walls[i])] = i;
            for (int i = 0; i < batch.Stumps.Count; i++)
                _stumpIndex[KeyOf(batch.Stumps[i])] = i;

            var size = _model.Size;
            for (int z = z0; z < z1; z++)
            for (int x = x0; x < x1; x++)
            {
                int index = size.Index(x, z, y);
                // Once per cell, for everything the cell emits: only something built can be
                // stacked, so terrain, trees and grass are never marked by it.
                _stacked = _model.IsStackedAt(index);
                EmitTerrain(batch, index, x, z, y);
                EmitBank(batch, index, x, z, y);
                EmitScatter(batch, index, x, z, y);
                EmitDressing(batch, index, x, z, y);
                EmitFloor(batch, index, x, z, y);
                EmitStoreEdge(batch, index, x, z, y);
                EmitEdifice(batch, index, x, z, y);
                EmitCrop(batch, index, x, z, y);
            }

            // Grass in rank order, so the renderer's distance thinning submits a prefix (design 38
            // §21, GrassThinning). Once per meshing, not per frame.
            for (int i = 0; i < batch.Body.Count; i++)
            {
                InstanceBucket bucket = batch.Body[i];
                if (bucket.Count > 1 && !bucket.IsColoured && IsGrassModule(bucket.Module))
                    GrassThinning.SortByRank(bucket.Matrices, bucket.Count, ref _rankKeys);
            }

            batch.Skin.Build(batch.Bounds);
            batch.Version = _model.ChunkVersion(chunkIndex);
        }

        float[] _rankKeys = new float[256];

        /// <summary>
        /// Whether a module is grass the distance thinning may thin (design 38 §21): the tufts, and
        /// the Meadow dressing that reads as meadow — tall-grass stands, flowers, ground cover and
        /// sunflowers. Not bushes or stones, which are single features rather than a carpet, and not
        /// crops, which are the colony's own.
        /// </summary>
        public bool IsGrassModule(int module)
        {
            if (_grassModules == null)
            {
                EnsureScatterModules();
                EnsureDressModules();
                _grassModules = new HashSet<int>(_scatterModules);
                if (_dressModules.Length > 0)
                    foreach (MeadowDressing.Kind kind in new[] { MeadowDressing.Kind.TallGrass,
                                 MeadowDressing.Kind.Flower, MeadowDressing.Kind.Cover, MeadowDressing.Kind.Sunflower })
                        foreach (int m in _dressModules[(int)kind]) _grassModules.Add(m);
            }
            return _grassModules.Contains(module);
        }

        HashSet<int>? _grassModules;

        /// <summary>
        /// The chunk's cell volume, padded.
        ///
        /// The padding is not cosmetic. <c>RenderParams.worldBounds</c> is what culling believes,
        /// and modules legitimately overhang their cells: a wall panel straddles the face it sits
        /// on, a pillar is 3.02 m in a 3.00 m layer, the upper half of a stair reaches 3.33 m. Too
        /// tight a box and whole chunks vanish at the screen edge, which is a horrible bug to
        /// track down because the geometry is provably correct.
        /// </summary>
        const float BoundsPadding = 2f;

        /// <summary>
        /// How far the relief can carry a cell out of its own layer, in metres.
        ///
        /// Two parts, and leaving out the second is the easy mistake: the cell is lifted by up to
        /// the amplitude, and then *tilted*, so its high corner rises a further half-diagonal times
        /// the steepest slope the field can reach. Derived rather than folded into the existing
        /// padding on the grounds that 2 m happens to cover it, because the day somebody raises the
        /// amplitude that coincidence becomes the culling bug this padding already exists to stop.
        /// Only Y moves: a shear shifts nothing horizontally.
        /// </summary>
        static float ReliefReach()
        {
            float amplitude = Mathf.Abs(GroundRelief.Amplitude);
            if (amplitude == 0f) return 0f;
            return amplitude + GroundRelief.MaxSlope(amplitude) * CellMetrics.SizeXZ;
        }

        /// <summary>
        /// The box <see cref="Mesh"/> gives a chunk, without meshing it. It depends on the chunk's
        /// footprint and nothing inside it, which is what lets the renderer ask the frustum before
        /// deciding whether the chunk is worth meshing at all.
        /// </summary>
        public Bounds BoundsOf(int chunkIndex)
        {
            _model.ChunkBounds(chunkIndex, out int x0, out int z0, out int y, out int x1, out int z1);
            return ChunkWorldBounds(x0, z0, y, x1, z1);
        }

        Bounds ChunkWorldBounds(int x0, int z0, int y, int x1, int z1)
        {
            Bounds bounds = ChunkWorldBoundsCore(x0, z0, y, x1, z1);
            // A rim chunk's skin runs ApronMetres past the board's edge, down or up to the
            // surround; its box has to hold that or the cull drops the apron at the screen edge.
            if (GroundSkin.Enabled && (x0 == 0 || z0 == 0 || x1 >= _model.Size.SizeX || z1 >= _model.Size.SizeZ))
            {
                bounds.Expand(new Vector3(2f * ApronMetres, 2f * CellMetrics.SizeY, 2f * ApronMetres));
            }
            return bounds;
        }

        static Bounds ChunkWorldBoundsCore(int x0, int z0, int y, int x1, int z1)
        {
            float relief = ReliefReach();
            var bounds = new Bounds();
            bounds.SetMinMax(
                new Vector3(
                    x0 * CellMetrics.SizeXZ - BoundsPadding,
                    y * CellMetrics.SizeY - BoundsPadding - relief,
                    z0 * CellMetrics.SizeXZ - BoundsPadding),
                new Vector3(
                    x1 * CellMetrics.SizeXZ + BoundsPadding,
                    (y + 1) * CellMetrics.SizeY + BoundsPadding + relief,
                    z1 * CellMetrics.SizeXZ + BoundsPadding));
            return bounds;
        }

        // --------------------------------------------------------------- cells

        /// <summary>
        /// How far up its own cell a water surface is drawn, as a fraction of the cell height.
        ///
        /// Both depths use the same number, and that is the point: a body of water has one
        /// level. Shallow and deep sit side by side in the same pond, so drawing them at
        /// different heights would put a step in the middle of the surface. Depth is told by
        /// colour and opacity instead — which is also why the channel is one cell deep whatever
        /// the depth, since a two-layer deep core would put neighbouring surface cells two layers
        /// apart and break the invariant that keeps the board walkable.
        ///
        /// Just under a full cell, so the water nearly fills the channel it was cut into and a
        /// bank reads as a low bank rather than as the lip of a dry ditch.
        /// </summary>
        public static float WaterSurface { get; set; } = 0.72f;

        /// <summary>
        /// The contributors, in the order they are asked. First to claim the cell owns it.
        ///
        /// <para>This replaced a chain of early returns inside <see cref="EmitTerrain"/> (OQ-46).
        /// The order here is the order those returns were in, and it is load-bearing: water before
        /// surface before stone before earth before the plain block. A contributor states its own
        /// condition, so adding a terrain feature is a registration rather than an edit to this
        /// file — which is the point, since this file is on the queue's do-not-touch list and both
        /// the water line and the mining line had to edit it anyway.</para>
        /// </summary>
        readonly List<ITerrainContributor> _terrain = new List<ITerrainContributor>
        {
            new WaterContributor(),
            new SurfaceContributor(),
            new StoneContributor(),
            new EarthContributor(),
            new SolidBlockContributor(),
        };

        /// <summary>
        /// Add a contributor ahead of the plain block that ends the chain, so a feature can claim
        /// cells the built-in kinds would otherwise draw as ordinary solid terrain.
        /// </summary>
        public void AddTerrainContributor(ITerrainContributor contributor)
        {
            if (contributor == null) throw new System.ArgumentNullException(nameof(contributor));
            _terrain.Insert(_terrain.Count - 1, contributor);
        }

        /// <summary>The contributors as registered, for a test to assert the order it relies on.</summary>
        public IReadOnlyList<ITerrainContributor> TerrainContributors => _terrain;

        void EmitTerrain(ChunkBatch batch, int index, int x, int z, int y)
        {
            // The ground a zoned cell draws is dirt: the look swap happens here, on the terrain
            // quad itself, so a field is seamless and boolean by construction — one quad, one
            // material, no mesh laid over the ground to sit proud of its tile.
            ushort terrain = _model.DrawnTerrain(index);
            if (terrain == CoreContent.TerrainAir) return;

            int module = _model.TerrainModuleFor(terrain);
            if (module == 0) return;

            int tint = TintCode.Daylit(TintCode.Terrain(terrain), _model.OpenToTheSky(index, y));
            // Worked soil is this same earth graded darker, and saying so in the bucket key is
            // the whole of drawing a growing zone: no second mesh, no per-cell draw, no per-frame
            // work at all. TintCode.TilledBase carries the measurement that justifies it.
            if (_model.IsZoned(index)) tint = TintCode.Tilled(tint);
            // And a store's ground, which is the same idea said on the other kind of surface: the
            // terrain quad here, the slab in EmitFloor. A cell can be zoned for growing or for
            // storage and never both — the two siting gates disagree about almost everything —
            // so the two bits never meet on one bucket in practice.
            if (_model.IsStoredAbove(index)) tint = TintCode.Stored(tint);
            if (DrawnWhole(terrain)) tint = TintCode.Whole(tint);

            // Terrain is the ground, so it is the one thing that is draped rather than lifted: the
            // cell is tilted onto the tangent plane of the relief field so its top face follows
            // the slope. Everything built or standing on it is lifted instead - see GroundRelief.
            Matrix4x4 at = GroundRelief.Drape(CellMetrics.FloorCentre(x, z, y));

            // Computed here and only for solid cells, which is exactly where the chain computed
            // it. Three contributors read it, and asking each to work it out for itself would pay
            // for a neighbour scan three times on the hot path.
            bool solid = _model.IsSolid(index);
            bool showsAFace = solid && HasExposedFace(index, x, z, y);

            var cell = new TerrainCell(this, _model, index, x, z, y, terrain, module, tint, at, solid, showsAFace);
            var sink = new MeshSink(this, batch);

            for (int i = 0; i < _terrain.Count; i++)
                if (_terrain[i].Emit(cell, sink))
                    return;
        }

        /// <summary>
        /// Which terrains are part of the waterside rather than things standing in front of it, and
        /// so are drawn whole however squarely they stand in a sight line (<c>TintCode.WholeBase</c>).
        ///
        /// <para>Marsh, and marsh only. Water already has its own marker and banks get theirs where
        /// they are emitted; a bog is the odd one out because it is an ordinary solid ground cell —
        /// it is in <c>NaturalContent.IsGround</c>, a colonist walks over it — and so it was fading
        /// like any other ground, opening a hole in the shore right beside water that stayed whole
        /// because water is exempt. Owner, 2026-09-18: "sometimes it hides marsh as well — omit
        /// this."</para>
        ///
        /// <para>Asked of the terrain index here rather than through a <c>NaturalContent</c>
        /// predicate because it is a <em>drawing</em> decision and not a content one: nothing in the
        /// simulation is different about a bog for this reason, and a name in the content tables
        /// would invite one to be.</para>
        /// </summary>
        static bool DrawnWhole(ushort terrain) => terrain == NaturalContent.TerrainMarsh;

        internal int ExposedSidesOf(int x, int z, int y) => ExposedSides(x, z, y);

        internal void SinkBody(ChunkBatch batch, int module, int tint, in Matrix4x4 at) =>
            AddBody(batch, module, tint, at);

        internal void SinkRoof(ChunkBatch batch, int module, int tint, in Matrix4x4 at) =>
            AddRoof(batch, module, tint, at);

        internal bool SinkSkinsTop(in TerrainCell cell) => SkinsTop(cell.Index, cell.X, cell.Z, cell.Y);

        internal void SinkSkin(ChunkBatch batch, int module, int tint, int x, int z, int y) =>
            SinkSkinTop(batch, module, tint, x, z, y);

        // ------------------------------------------------------------- scatter

        /// <summary>
        /// Tufts of grass per hundred grass cells. 60 means six cells in ten get one tuft and the
        /// rest are bare — sparse enough that the meadow reads as a field with grass on it rather
        /// than as grass with a field somewhere underneath, which is what 120 did at board
        /// distance. Zero turns scatter off entirely.
        /// </summary>
        public int ScatterDensity { get; set; } = 60;

        /// <summary>Whether the Meadow dressing is strewn at all, apart from the density. A
        /// measurement seam (the player benchmark, design 38 §18e): on in the game.</summary>
        public bool Dressing { get; set; } = true;

        /// <summary>Whether the grass tufts are strewn at all, apart from the density. A
        /// measurement seam, as <see cref="Dressing"/>: on in the game.</summary>
        public bool Tufts { get; set; } = true;

        /// <summary>Which dressing kinds are strewn, one bit per <see cref="MeadowDressing.Kind"/>.
        /// A measurement seam (design 38 §21: what each kind costs at distance): all on in the game.</summary>
        public int DressingKinds { get; set; } = ~0;

        int[] _scatterModules = System.Array.Empty<int>();

        /// <summary>Whether a module is one of the grass tufts the scatter strews — the kind the
        /// indirect path draws (design 38 §18). Resolves the tufts on first ask.</summary>
        public bool IsScatterModule(int module)
        {
            EnsureScatterModules();
            return System.Array.IndexOf(_scatterModules, module) >= 0;
        }
        bool _scatterResolved;

        /// <summary>
        /// Strew tufts over an exposed grass surface.
        ///
        /// They go through the ordinary bucket machinery, which is the point: a tuft is one more
        /// instance of one more module in the chunk it stands in, so it inherits chunk culling,
        /// the slice, the depth shade and the single instanced submission per mesh with no new
        /// code path and no per-blade object. The alternative — a particle system, a detail
        /// renderer, a GameObject per clump — would have been a second way of drawing the world.
        ///
        /// Only the top surface is dressed. A grass cell with something solid stacked on it is a
        /// cell nobody can see the top of, and grass growing inside a floor is the sort of fault
        /// that renders perfectly and is spotted a fortnight later.
        /// </summary>
        void EmitScatter(ChunkBatch batch, int index, int x, int z, int y)
        {
            if (ScatterDensity <= 0 || !Tufts) return;

            // The drawn terrain, so a zoned cell - drawn as dirt - grows no tuft through it.
            ushort terrain = _model.DrawnTerrain(index);
            if (terrain != NaturalContent.TerrainGrass) return;
            if (!_model.IsSolid(index)) return;

            var size = _model.Size;
            if (y + 1 < size.SizeY)
            {
                int above = index + size.LayerStride;

                // Something solid stacked on it — the original rule.
                if (_model.IsSolid(above)) return;

                // Or a floor laid over it, which is the same argument and was found by the same
                // sort of picture: paving drew correctly and the grass went on growing straight
                // through it (`PavingProbe`, 2026-09-17, U42). The kind is not examined, because a
                // built floor, a stamped deck and a deck plate all equally hide what is beneath.
                if (_model.Floor(above) != CoreContent.SlabNone) return;

                // Or something the colony BUILT standing in it (owner, 2026-09-23: "make sure the
                // grass tufts are removed when campfire is placed down"). Same argument a third
                // time, and the same picture: a campfire drew correctly with grass growing up
                // through the middle of it.
                //
                // **Built, and not a tree, which is the whole of the rule.** A tree is an edifice
                // in this cell too, and a woodland floor with no grass under the canopy would be
                // a bald patch around every trunk — trees are exactly what the tufts are *for*
                // standing among. Anything a colonist raised is different: it sits on the ground
                // rather than growing out of it, and the ground under it is not somewhere grass
                // still is. That covers the campfire the owner asked about and the bed and the
                // shelf, which have had the same fault since they landed and nobody had looked.
                ushort built = _model.EdificeDef(above);
                if (built != CoreContent.EdificeNone && !NaturalContent.IsNatural(built)) return;
            }

            EnsureScatterModules();
            if (_scatterModules.Length == 0) return;

            int count = GroundScatter.CountFor(x, z, ScatterDensity);
            if (count == 0) return;

            // Tufts stand on top of the solid cell, not inside it.
            Vector3 surface = CellMetrics.FloorCentre(x, z, y) + Vector3.up * CellMetrics.SizeY;

            // Which top this cell wears, and which way round, so a tuft can be set on the surface
            // that is actually drawn rather than on the flat one that used to be. The cell's
            // ripple is at most 12 cm, which sounds ignorable and is not: a tuft is about half a
            // metre, so a quarter of one hanging in the air is plainly wrong, and the same again
            // buried reads as a bald patch.
            int groundVariant = GroundLook.Variant(x, z, y);
            Quaternion untwist = Quaternion.Euler(0f, -GroundLook.Yaw(x, z, y), 0f);

            // Foliage, not terrain. Tinting a tuft the way the ground beneath it is tinted turned
            // a meadow into dark teal reeds; TintCode.FoliageBase says why.
            //
            // Daylit on the same terms as the ground it stands in, and it has to be asked rather
            // than assumed: a tuft that kept dimming while the terrace under it stopped would be
            // the same fault, a layer smaller and much harder to see.
            bool daylit = _model.OpenToTheSky(index, y);

            // A clump is placed in a ring of its own cell but its mesh reaches a metre past
            // it, and the tile beside a growing zone is tilled ground: before this pull, every
            // border tile of a plot wore a fringe of meadow lying over it (owner, 2026-09-19:
            // "remove the grass graphics from the garden plots ... it makes it jarring"). The
            // neighbours are asked through the zone mirror, so a freshly painted field pulls
            // its surrounding tufts in on the re-mesh the designation itself marks.
            bool tilledXPlus = x + 1 < size.SizeX && _model.IsZoned(index + 1);
            bool tilledXMinus = x > 0 && _model.IsZoned(index - 1);
            bool tilledZPlus = z + 1 < size.SizeZ && _model.IsZoned(index + size.SizeX);
            bool tilledZMinus = z > 0 && _model.IsZoned(index - size.SizeX);

            for (int slot = 0; slot < count; slot++)
            {
                GroundScatter.Placement(x, z, slot,
                    out float offsetX, out float offsetZ, out float yaw, out float scale);
                GroundScatter.PullInFromTilled(
                    ref offsetX, ref offsetZ, tilledXPlus, tilledXMinus, tilledZPlus, tilledZMinus);

                int which = GroundScatter.VariantFor(x, z, slot, _scatterModules.Length);
                int module = _scatterModules[which];

                // The tint follows the clump mesh rather than the tuft, and that is what makes a
                // varied meadow free: a module is already its own instancing bucket, so three
                // tints across three modules costs exactly what one tint across three modules did.
                // Choosing per tuft would multiply the buckets by the number of tints, on the
                // heaviest instanced thing in the world.
                int tint = TintCode.Daylit(
                    TintCode.Foliage(which % StuffPalette.FoliageTintCount), daylit);

                // Lifted at the tuft's own position rather than the cell's, because the cell is
                // tilted: a tuft near the low corner of a sloping cell would otherwise float, and
                // one at the high corner would be buried to its neck.
                //
                // And set on the cell's own ripple as well as on the board's roll — two separate
                // shapes, asked separately. The offset is turned back through the block's bearing
                // first, because the mesh is rotated by the instance matrix and its ripple turns
                // with it; sampling the unturned mesh at a turned position puts the tuft on the
                // wrong corner of the cell, which is a subtler wrong than being on no corner.
                Vector3 local = untwist * new Vector3(offsetX, 0f, offsetZ);
                float ripple = GroundMesh.HeightAtLocal(groundVariant, local.x, local.z) * CellMetrics.SizeY;

                Vector3 at = GroundRelief.Lift(
                    surface + new Vector3(offsetX * CellMetrics.SizeXZ, ripple, offsetZ * CellMetrics.SizeXZ));
                at.y += SkinRise(x, z, surface.y, at.x, at.z);

                AddBody(batch, module, tint, Matrix4x4.TRS(
                    at, Quaternion.Euler(0f, yaw, 0f), new Vector3(scale, scale, scale)));
            }
        }

        /// <summary>
        /// The crop standing in this cell, at its drawn stage, through the ordinary bucket
        /// machinery — a crop is one more module per chunk, and inherits culling, the slice, the
        /// depth shade and the single instanced submission with no code path of its own.
        ///
        /// <para>It stands at a point, so it is lifted rather than draped (see the class header's
        /// split), at its own cell's floor — which for a zone cell is the ground surface, where
        /// <see cref="GroundMesh"/> pins the turf's middle, so a sprout neither floats above the
        /// field nor starts life buried.</para>
        ///
        /// <para>Foliage, not terrain, for the reason <see cref="EmitScatter"/> gives; and daylit
        /// on the same terms as the ground it grows in, so a crop under a roof reads dimmer than
        /// one in the open rather than glowing.</para>
        /// </summary>
        void EmitCrop(ChunkBatch batch, int index, int x, int z, int y)
        {
            int module = _model.CropModule(index);
            if (module == 0) return;

            // The plot draws its yield: as many plants as the harvest will give, from the first
            // sprout to the last pull (owner, 2026-09-18). Free by instancing - more matrices in
            // the same bucket, not more buckets - and scattered at hashed per-cell-per-plant
            // positions so a field reads as rows of plants rather than one repeated clump. The
            // offset/scale the catalogue row carries moves every plant alike, so the sink the
            // row asks for is honoured per plant and not just per cell.
            bool daylit = _model.OpenToTheSky(index, y);
            int tint = TintCode.Daylit(TintCode.Foliage(0), daylit);

            int count = _model.CropCount(index);
            Vector3 centre = GroundRelief.Lift(CellMetrics.FloorCentre(x, z, y));
            for (int i = 0; i < count; i++)
            {
                uint h = (uint)(index * 747_796_405u + i * 289_133_645_3u);
                h = (h ^ (h >> 13)) * 1_274_126_177u;
                float ox = ((h & 0xFFFF) / 65535f - 0.5f) * (CellMetrics.SizeXZ - 1.1f);
                float oz = (((h >> 16) & 0xFFFF) / 65535f - 0.5f) * (CellMetrics.SizeXZ - 1.1f);
                float yaw = (h % 4u) * 90f;

                AddBody(batch, module, tint, Matrix4x4.TRS(
                    centre + new Vector3(ox, 0f, oz),
                    Quaternion.Euler(0f, yaw, 0f), Vector3.one));
            }
        }


        /// <summary>
        /// Resolve the tuft modules once, and keep only the ones that found real art.
        ///
        /// Dropping the rest is the important half. Every other module in the world falls back to
        /// a tinted primitive when its art is missing, which is right for a wall — a box where a
        /// wall should be is still a wall. A box where a tuft of grass should be is fourteen
        /// thousand grey cubes strewn across a meadow, so a clone without the packs gets bare
        /// ground instead, which is what it had before any of this existed.
        /// </summary>
        // ------------------------------------------------------------ dressing

        /// <summary>
        /// A cell the colony started in, and how far round it the big dressing stays away: the
        /// clearing is where the colony lays out its first buildings, and a bush there would be in
        /// the way of every order. Set by the composition root before the first meshing; none means
        /// the dressing stands anywhere it may.
        /// </summary>
        public Vector2Int? DressingClearing { get; set; }

        /// <summary>The clearing's radius in cells.</summary>
        public int DressingClearRadius { get; set; } = 4;

        /// <summary>
        /// The tallest tree or bush the dressing and the tree variants resolved to, in metres above
        /// the cell floor: what the frustum and sight tests must allow for above a chunk's box.
        /// Zero until something has resolved.
        /// </summary>
        public float TallestResolved { get; private set; }

        /// <summary>
        /// How far past its chunk's box, sideways, anything resolved so far can reach: a tree crown
        /// or a bush wider than its cell, turned, scaled and jittered off its cell centre, less the
        /// half cell and the padding the box already allows. The frustum test grows a chunk's box
        /// by this in x and z (design 38 §18) — the sun-ward shadow sweep starts from the box, and
        /// a 17 m crown whose edge overhangs it cast a shadow the sweep did not see. Zero until
        /// something wide has resolved.
        /// </summary>
        public float OverhangResolved { get; private set; }

        void NoteReach(int module, float scale, float jitter)
        {
            Bounds b = _model.Library[module].Bounds;
            float x = Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x));
            float z = Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z));
            float reach = Mathf.Sqrt(x * x + z * z) * scale + jitter;
            OverhangResolved = Mathf.Max(OverhangResolved,
                reach - CellMetrics.SizeXZ * 0.5f - BoundsPadding);
        }

        /// <summary>How many dressing families resolved to real art. Zero on a checkout without the
        /// packs, which is how a measurement knows there is no dressing to price.</summary>
        public int DressingFamiliesWithArt
        {
            get
            {
                EnsureDressModules();
                int n = 0;
                foreach (int[] family in _dressModules) if (family.Length > 0) n++;
                return n;
            }
        }

        int[][] _dressModules = System.Array.Empty<int[]>();
        bool _dressResolved;
        readonly Dictionary<int, int[]> _treeVariants = new Dictionary<int, int[]>();

        /// <summary>
        /// Strew the Meadow dressing over an exposed grass surface: one big piece on the lattice
        /// (a bush or a stand of tall grass) and a handful of small ones (flowers, cover, a stone),
        /// by <see cref="MeadowDressing"/>'s fields. The same ground rule as the tufts, and more:
        /// the big pieces overhang their cell, so they also need a clear ring round it — no zone,
        /// floor or building in any neighbour — and keep out of the colony's clearing.
        /// </summary>
        void EmitDressing(ChunkBatch batch, int index, int x, int z, int y)
        {
            if (ScatterDensity <= 0 || !Dressing) return;
            if (!DressableSurface(index, y)) return;
            EnsureDressModules();
            if (_dressModules.Length == 0) return;

            float density = ScatterDensity / 60f;
            var size = _model.Size;
            bool inClearing = DressingClearing.HasValue
                && (new Vector2Int(x, z) - DressingClearing.Value).sqrMagnitude
                   <= DressingClearRadius * DressingClearRadius;
            bool daylit = _model.OpenToTheSky(index, y);
            Vector3 surface = CellMetrics.FloorCentre(x, z, y) + Vector3.up * CellMetrics.SizeY;

            bool ringFree = true, woodEdge = false, nearRock = false;
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int nx = x + dx, nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= size.SizeX || nz >= size.SizeZ) continue;
                int n = size.Index(nx, nz, y);
                if (_model.IsZoned(n)) ringFree = false;
                if (y + 1 < size.SizeY)
                {
                    int up = n + size.LayerStride;
                    if (_model.Floor(up) != CoreContent.SlabNone) ringFree = false;
                    ushort edifice = _model.EdificeDef(up);
                    if (edifice != CoreContent.EdificeNone)
                    {
                        if (NaturalContent.IsTree(edifice)) woodEdge = true;
                        else if (!NaturalContent.IsBush(edifice)) ringFree = false;
                    }
                    if (_model.IsSolid(up) && _model.DrawnTerrain(up) == NaturalContent.TerrainRock) nearRock = true;
                }
                if (_model.DrawnTerrain(n) == NaturalContent.TerrainRock) nearRock = true;
            }

            // The big piece, on its lattice.
            if (ringFree && !inClearing)
            {
                MeadowDressing.Kind big = MeadowDressing.BigPiece(x, z, density, woodEdge);
                if (big != MeadowDressing.Kind.None)
                    PlaceDressing(batch, big, x, z, surface, daylit, spread: 0.5f);
            }

            for (int slot = 0; slot < MeadowDressing.SmallSlots; slot++)
            {
                MeadowDressing.Kind small = MeadowDressing.SmallPiece(x, z, slot, density, nearRock);
                if (small == MeadowDressing.Kind.None) continue;
                if (small == MeadowDressing.Kind.Rock && inClearing) continue;
                PlaceDressing(batch, small, x, z, surface, daylit, spread: 0.7f, slot: slot);
            }
        }

        /// <summary>
        /// A bush the simulation placed (design 45 §4), standing in the air cell above its grass.
        /// Drawn whatever the grass ladder and the dressing switch say, because it is a thing a
        /// colonist pushes through and a builder has to clear: turning the grass off must not hide
        /// it. A checkout without the packs has no bush art and draws nothing, the tufts' rule.
        /// </summary>
        void EmitBush(ChunkBatch batch, ushort def, int x, int z, int y)
        {
            if ((DressingKinds & (1 << (int)MeadowDressing.Kind.Bush)) == 0) return;
            if (!TryBushPlacement(x, z, y, out Matrix4x4 placed, out int module)) return;

            AddBody(batch, module, TintCode.Dressing(TintCode.Tree(TreeSpecies.Broadleaf)), placed);
            // Its drawn height, for the click (design 45 §12): the crown's top over the cell's floor.
            _model.NoteBushTop(_model.Size.Index(x, z, y),
                placed.MultiplyPoint3x4(new Vector3(0f, _model.Library[module].Bounds.max.y, 0f)).y
                - CellMetrics.FloorCentre(x, z, y).y);

            // Where the bush stands and how wide, for the renderer's "is this thing under a bush"
            // (design 38 §19): the half-diagonal of its footprint, so any bearing is covered.
            float scale = placed.lossyScale.x;
            Vector3 extent = _model.Library[module].Bounds.extents * scale;
            Vector3 at = placed.GetColumn(3);
            batch.BushDiscs.Add(new Vector3(at.x, at.z, Mathf.Sqrt(extent.x * extent.x + extent.z * extent.z)));

            if (def == NaturalContent.EdificeBerryBush) EmitBerries(batch, x, z, placed, module);
        }

        /// <summary>
        /// Where and how a bush in this cell is drawn: which of the Meadow bushes, and its matrix —
        /// the jitter, the turn and the size of <see cref="MeadowDressing.Placement"/>, lifted onto
        /// the ground and the skin. The one owner of that answer, asked by the mesher to draw the
        /// bush and hang its berries, and by anything that has to know where a drawn bush is (the
        /// pick measurement, design 45 §12). False when there is no bush art.
        /// </summary>
        public bool TryBushPlacement(int x, int z, int y, out Matrix4x4 placed, out int module)
        {
            placed = Matrix4x4.identity;
            module = 0;
            EnsureDressModules();
            if (_dressModules.Length == 0) return false;
            int[] family = _dressModules[(int)MeadowDressing.Kind.Bush];
            if (family.Length == 0) return false;

            uint salt = MeadowDressing.SaltOf(MeadowDressing.Kind.Bush);
            module = family[MeadowDressing.VariantFor(x, z, salt, family.Length)];
            MeadowDressing.Placement(x, z, salt, BushSpread,
                out float offsetX, out float offsetZ, out float yaw, out float scale);
            scale *= BushScale;

            Vector3 surface = CellMetrics.FloorCentre(x, z, y);
            Vector3 at = GroundRelief.Lift(
                surface + new Vector3(offsetX * CellMetrics.SizeXZ, 0f, offsetZ * CellMetrics.SizeXZ));
            at.y += SkinRise(x, z, surface.y, at.x, at.z);
            placed = Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, 0f), new Vector3(scale, scale, scale));
            return true;
        }

        /// <summary>
        /// How far a simulated bush may stand off its cell's centre, as a fraction of the cell: the
        /// dressing's own half cell. A setting so a measurement can move it; the pick fix of design
        /// 45 §12 was measured not to need it moved (<c>BushPickTests</c>).
        /// </summary>
        public static float BushSpread { get; set; } = 0.5f;

        /// <summary>The drawn size of a simulated bush against the dressing's; one, as it was.</summary>
        public static float BushScale { get; set; } = 1f;

        /// <summary>
        /// The berries on a ripe berry bush (design 45 §6, §12): clusters set <b>on the bush's own
        /// crown</b>, placed through the bush's drawn matrix so they turn, size and stand with it.
        ///
        /// <para>The crown is taken as the ellipsoid the bush's bounds describe, from its middle
        /// height upwards, and each cluster is set a little inside that surface — the Meadow bushes
        /// are lumpy, the meshes are not readable at run time to find the true surface, and a
        /// cluster sunk into the leaves reads as growing there where one outside it reads as
        /// floating. The bounds are the resolved module's, so nothing of the art is copied.</para>
        /// </summary>
        void EmitBerries(ChunkBatch batch, int x, int z, in Matrix4x4 bush, int bushModule)
        {
            if (_berryModule < 0)
            {
                _berryModule = 0;
                if (_model.Library.Catalogue != null && _model.Library.Catalogue.Find(ModuleIds.ItemBerries) != null)
                {
                    int module = _model.Library.Resolve(ModuleIds.ItemBerries, ModuleShape.Pillow);
                    if (_model.Library[module].UsesArt && !_model.Library[module].IsEmpty) _berryModule = module;
                }
            }
            if (_berryModule == 0) return;

            Bounds crown = _model.Library[bushModule].Bounds;
            int tint = TintCode.Dressing(TintCode.Stuff(CoreContent.StuffNone));
            if (!BerriesOnTheCrown)
            {
                // As first built, for a before-and-after photograph only: a ring at a fraction of
                // the footprint's half-diagonal, heights off the bush's top, with neither the bush's
                // turn nor the crown's shape — which is why they floated (design 45 §12).
                float scale = bush.lossyScale.x;
                Vector3 foot = bush.GetColumn(3);
                float radius = new Vector2(crown.extents.x, crown.extents.z).magnitude * scale;
                float topY = crown.max.y * scale;
                float spin = GroundScatter.Unit(x, z, 0xBE44u) * 360f;
                for (int i = 0; i < 7; i++)
                {
                    float angle = (spin + i * (360f / 7)) * Mathf.Deg2Rad;
                    float reach = radius * (0.55f + 0.2f * GroundScatter.Unit(x + i, z, 0xBE45u));
                    float height = topY * (0.45f + 0.35f * GroundScatter.Unit(x, z + i, 0xBE46u));
                    var at = new Vector3(foot.x + Mathf.Cos(angle) * reach, foot.y + height, foot.z + Mathf.Sin(angle) * reach);
                    AddBody(batch, _berryModule, tint, Matrix4x4.TRS(at, Quaternion.Euler(0f, angle * 57f, 0f), Vector3.one));
                }
                return;
            }
            float turn = GroundScatter.Unit(x, z, 0xBE44u) * 360f;
            for (int i = 0; i < BerryClusters; i++)
            {
                float azimuth = (turn + i * (360f / BerryClusters)) * Mathf.Deg2Rad;
                // Between the crown's shoulder and near its top, where a berry is seen from above.
                float elevation = (42f + 33f * GroundScatter.Unit(x + i, z, 0xBE45u)) * Mathf.Deg2Rad;
                var onCrown = new Vector3(
                    Mathf.Cos(elevation) * Mathf.Cos(azimuth) * crown.extents.x,
                    Mathf.Sin(elevation) * crown.extents.y,
                    Mathf.Cos(elevation) * Mathf.Sin(azimuth) * crown.extents.z) * BerryDepth;
                Vector3 local = crown.center + onCrown;
                Vector3 world = bush.MultiplyPoint3x4(local);
                AddBody(batch, _berryModule, tint, Matrix4x4.TRS(world,
                    Quaternion.Euler(0f, azimuth * Mathf.Rad2Deg, 0f), Vector3.one));
            }
        }

        /// <summary>False draws the berries as they were first built, for a before photograph (design 45 §12).</summary>
        public static bool BerriesOnTheCrown { get; set; } = true;

        /// <summary>Clusters on a ripe berry bush.</summary>
        public const int BerryClusters = 8;

        /// <summary>How far out along the crown ellipsoid a cluster sits: under one, so it is in the
        /// leaves and not beside them.</summary>
        public const float BerryDepth = 0.85f;

        int _berryModule = -1;

        void PlaceDressing(ChunkBatch batch, MeadowDressing.Kind kind, int x, int z, Vector3 surface,
            bool daylit, float spread, int slot = 0)
        {
            int[] family = _dressModules[(int)kind];
            if (family.Length == 0) return;
            if ((DressingKinds & (1 << (int)kind)) == 0) return;
            uint salt = MeadowDressing.SaltOf(kind) + (uint)slot * 104729u;
            int which = MeadowDressing.VariantFor(x, z, salt, family.Length);
            MeadowDressing.Placement(x, z, salt, spread,
                out float offsetX, out float offsetZ, out float yaw, out float scale);

            // Grass and flowers are foliage (no shadow, cleared round items, our shader); a bush
            // takes the tree's path (it casts a shadow, and fades when it stands between the camera
            // and a colonist); a stone is plain art.
            int tint = kind switch
            {
                MeadowDressing.Kind.Bush => TintCode.Dressing(TintCode.Tree(TreeSpecies.Broadleaf)),
                // Dressing, so a stone casts no shadow either (design 38 §18f): its tint colours only
                // by the low byte, which the flag leaves alone.
                MeadowDressing.Kind.Rock => TintCode.Dressing(TintCode.Stuff(CoreContent.StuffNone)),
                _ => TintCode.Daylit(TintCode.Foliage(which % StuffPalette.FoliageTintCount), daylit),
            };

            Vector3 at = GroundRelief.Lift(
                surface + new Vector3(offsetX * CellMetrics.SizeXZ, 0f, offsetZ * CellMetrics.SizeXZ));
            at.y += SkinRise(x, z, surface.y, at.x, at.z);
            AddBody(batch, family[which], tint, Matrix4x4.TRS(
                at, Quaternion.Euler(0f, yaw, 0f), new Vector3(scale, scale, scale)));

            // Where the bush stands and how wide, for the renderer's "is this thing under a bush"
            // (design 38 §19): the half-diagonal of its footprint, so any bearing is covered.
            if (kind == MeadowDressing.Kind.Bush)
            {
                Vector3 extent = _model.Library[family[which]].Bounds.extents * scale;
                batch.BushDiscs.Add(new Vector3(at.x, at.z,
                    Mathf.Sqrt(extent.x * extent.x + extent.z * extent.z)));
            }
        }

        /// <summary>
        /// Whether the top of this cell is open grass the dressing may stand on: the tufts' own
        /// rule — grass, solid, nothing solid, no floor and nothing built on it (a tree is allowed:
        /// woodland has a floor).
        /// </summary>
        bool DressableSurface(int index, int y)
        {
            if (_model.DrawnTerrain(index) != NaturalContent.TerrainGrass) return false;
            if (!_model.IsSolid(index)) return false;
            var size = _model.Size;
            if (y + 1 >= size.SizeY) return true;
            int above = index + size.LayerStride;
            if (_model.IsSolid(above)) return false;
            if (_model.Floor(above) != CoreContent.SlabNone) return false;
            ushort built = _model.EdificeDef(above);
            return built == CoreContent.EdificeNone || NaturalContent.IsNatural(built);
        }

        /// <summary>
        /// Resolve every dressing family once, keeping only the rows that found real art — a
        /// clone without the packs strews no boxes, the tufts' rule — and note the tallest.
        /// Indexed by <see cref="MeadowDressing.Kind"/>.
        /// </summary>
        void EnsureDressModules()
        {
            if (_dressResolved) return;
            _dressResolved = true;

            var families = new int[System.Enum.GetValues(typeof(MeadowDressing.Kind)).Length][];
            for (int i = 0; i < families.Length; i++) families[i] = System.Array.Empty<int>();
            families[(int)MeadowDressing.Kind.TallGrass] = ResolveFamily(ModuleIds.DressTallGrass);
            families[(int)MeadowDressing.Kind.Cover] = ResolveFamily(ModuleIds.DressCover);
            families[(int)MeadowDressing.Kind.Flower] = ResolveFamily(ModuleIds.DressFlowers);
            families[(int)MeadowDressing.Kind.Sunflower] = ResolveFamily(ModuleIds.DressSunflower);
            families[(int)MeadowDressing.Kind.Bush] = ResolveFamily(ModuleIds.DressBushes);
            families[(int)MeadowDressing.Kind.Rock] = ResolveFamily(ModuleIds.DressRocks);

            bool any = false;
            foreach (int[] family in families) any |= family.Length > 0;
            _dressModules = any ? families : System.Array.Empty<int[]>();
            foreach (int bush in families[(int)MeadowDressing.Kind.Bush])
                TallestResolved = Mathf.Max(TallestResolved, _model.Library[bush].Bounds.max.y);
            // Every family, turned, at MeadowDressing.Placement's largest scale (1.2) and furthest
            // jitter (half the widest spread, 0.7, of a cell).
            foreach (int[] family in families)
                foreach (int module in family)
                    NoteReach(module, 1.2f, 0.35f * CellMetrics.SizeXZ);
        }

        int[] ResolveFamily(string[] ids)
        {
            var usable = new List<int>();
            foreach (string id in ids)
            {
                if (_model.Library.Catalogue == null || _model.Library.Catalogue.Find(id) == null) continue;
                int module = _model.Library.Resolve(id, ModuleShape.Pillar);
                ResolvedModule resolved = _model.Library[module];
                if (resolved.UsesArt && !resolved.IsEmpty) usable.Add(module);
            }
            return usable.ToArray();
        }

        /// <summary>
        /// A tree species' art variants, its own row first, resolved once per species module.
        /// Only rows the catalogue actually has are asked for, so a checkout whose catalogue
        /// predates the variants — or has no packs — gets its one tree, and no "missing art" noise.
        /// </summary>
        /// <summary>The same, for the topple (design 45 §5): a falling tree wears the row it stood in.</summary>
        public int[] TreeVariants(int module) => TreeVariantsOf(module);

        int[] TreeVariantsOf(int module)
        {
            if (_treeVariants.TryGetValue(module, out int[]? known)) return known;

            var variants = new List<int> { module };
            string baseId = _model.Library[module].Id;
            for (int v = 1; v < ModuleIds.MaxTreeVariants; v++)
            {
                string id = ModuleIds.TreeVariant(baseId, v);
                if (_model.Library.Catalogue == null || _model.Library.Catalogue.Find(id) == null) continue;
                int resolved = _model.Library.Resolve(id, ModuleShape.Pillar);
                if (_model.Library[resolved].UsesArt && !_model.Library[resolved].IsEmpty) variants.Add(resolved);
            }
            int[] result = variants.ToArray();
            _treeVariants[module] = result;
            foreach (int m in result)
            {
                TallestResolved = Mathf.Max(TallestResolved, _model.Library[m].Bounds.max.y * 1.15f);
                NoteReach(m, 1.15f, 0f);
            }
            return result;
        }

        void EnsureScatterModules()
        {
            if (_scatterResolved) return;
            _scatterResolved = true;

            var usable = new List<int>();
            for (int variant = 0; variant < ModuleIds.GrassTuftCount; variant++)
            {
                int module = _model.Library.Resolve(ModuleIds.GrassTuft(variant), ModuleShape.Pillar);
                ResolvedModule resolved = _model.Library[module];
                if (resolved.UsesArt && !resolved.IsEmpty) usable.Add(module);
            }
            _scatterModules = usable.ToArray();
        }

        /// <summary>
        /// A buried cell is invisible, and the world boundary counts as buried.
        ///
        /// The boundary used to count as open air, which meant every solid cell in the outermost
        /// ring drew its outward face and the map gained a cross-section wall around its whole
        /// perimeter, as many cells tall as the slice drew layers below the surface. Looking down
        /// at a flat meadow you saw a slab of ground with sides, not a field.
        ///
        /// Treating the boundary as solid is the honest answer rather than a cosmetic one: there
        /// is no outside of the map, so there is nowhere a face on that plane could be seen from.
        /// Nothing interior changes, because a cut into the ground still exposes its neighbours in
        /// the ordinary way — a pit dug against the map edge still shows all four of its walls.
        /// </summary>
        /// <inheritdoc cref="BankLayout.Enabled"/>
        public bool Banks
        {
            get => BankLayout.Enabled;
            set => BankLayout.Enabled = value;
        }

        /// <summary>
        /// Draw soil as <see cref="GroundMesh"/> rather than as the plain cube. On by default, and
        /// off is exactly the ground as it was drawn before any of this, which is what makes the
        /// check harness's first photograph a real comparison rather than a remembered one.
        /// </summary>
        /// <summary>
        /// The earth look, on or off while it is being judged. Forwarded to the contributor that
        /// owns it (OQ-46) rather than kept here as a second copy of the same switch.
        /// </summary>
        public bool Earth
        {
            get => EarthKind.Enabled;
            set => EarthKind.Enabled = value;
        }

        EarthContributor EarthKind
        {
            get
            {
                for (int i = 0; i < _terrain.Count; i++)
                    if (_terrain[i] is EarthContributor earth) return earth;

                throw new System.InvalidOperationException(
                    "the earth contributor was unregistered; ChunkRenderer.Earth has nothing to switch");
            }
        }

        /// <inheritdoc cref="BankLayout.InWorkings"/>
        public bool BanksInWorkings
        {
            get => BankLayout.InWorkings;
            set => BankLayout.InWorkings = value;
        }

        /// <summary>
        /// A stepped earth bank in this empty cell, for each one-layer step beside it that a
        /// colonist could walk up.
        ///
        /// <para>Where one belongs, which of the three shapes it is and what it is made of all live
        /// in <see cref="BankLayout"/>, because a drawn figure has to stand on the same surface
        /// this draws — see that class for the conditions and for why they left the mesher. All
        /// that is left here is turning the answer into an instance.</para>
        /// </summary>
        void EmitBank(ChunkBatch batch, int index, int x, int z, int y)
        {
            if (GroundSkin.Enabled)
            {
                EmitRamp(batch, x, z, y);
                return;
            }

            BankLayout.Bank bank = BankLayout.At(_model, x, z, y);
            if (!bank.Exists) return;

            int module = _model.BankModuleFor(bank.Terrain, (int)bank.Kind);
            if (module == 0) return;

            // Draped, so a bank lies along the same rolling field the ground either side of it
            // does. Always daylit: BankLayout required the cell to be open to the sky.
            AddBody(batch, module,
                TintCode.Daylit(TintCode.Whole(TintCode.Terrain(bank.Terrain)), open: true),
                GroundRelief.Drape(CellMetrics.FloorCentre(x, z, y)) *
                Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[bank.Rotation], 0f)));
        }


        // ------------------------------------------------------------- the ground skin

        // Corner i of a cell, in cells from its low-x low-z corner: the order BankLayout.Ramp uses.
        static readonly int[] SkinCornerX = { 0, 1, 1, 0 };
        static readonly int[] SkinCornerZ = { 0, 0, 1, 1 };

        // The edge on each side of a cell as a pair of corners, indexed by Directions.
        static readonly int[] EdgeFirst = new int[Directions.Count];
        static readonly int[] EdgeSecond = new int[Directions.Count];

        static ChunkMesher()
        {
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int dx = Directions.DeltaX[dir], dz = Directions.DeltaZ[dir];
                // The two corners on that side are the ones whose offset agrees with the direction.
                int first = -1, second = -1;
                for (int c = 0; c < 4; c++)
                {
                    bool onSide = dx != 0 ? SkinCornerX[c] == (dx > 0 ? 1 : 0) : SkinCornerZ[c] == (dz > 0 ? 1 : 0);
                    if (!onSide) continue;
                    if (first < 0) first = c; else second = c;
                }
                EdgeFirst[dir] = first;
                EdgeSecond[dir] = second;
            }
        }

        // The relief at every cell corner of the chunk being meshed, sampled once each. A corner is
        // shared by up to six triangles and the relief is four sine waves: sampling per vertex was
        // most of what made a skinned chunk dearer to mesh than a boxed one (design 38 §20).
        float[] _cornerRelief = System.Array.Empty<float>();
        int _reliefX0, _reliefZ0, _reliefSpan;

        void FillCornerRelief(int x0, int z0, int x1, int z1)
        {
            _reliefX0 = x0;
            _reliefZ0 = z0;
            _reliefSpan = Mathf.Max(x1 - x0, z1 - z0) + 1;
            int count = _reliefSpan * _reliefSpan;
            if (_cornerRelief.Length < count) _cornerRelief = new float[count];
            for (int j = 0; j < _reliefSpan; j++)
            for (int i = 0; i < _reliefSpan; i++)
                _cornerRelief[i + j * _reliefSpan] = GroundRelief.HeightAt(
                    (x0 + i) * CellMetrics.SizeXZ, (z0 + j) * CellMetrics.SizeXZ);
        }

        float CornerRelief(int cx, int cz)
        {
            int i = cx - _reliefX0, j = cz - _reliefZ0;
            if ((uint)i < (uint)_reliefSpan && (uint)j < (uint)_reliefSpan)
                return _cornerRelief[i + j * _reliefSpan];
            return GroundRelief.HeightAt(cx * CellMetrics.SizeXZ, cz * CellMetrics.SizeXZ);
        }

        /// <summary>
        /// A corner of the skin in world space: the cell's corner at a rise above a plane, on the
        /// relief sampled at the corner itself so two cells meeting there agree to the bit.
        /// </summary>
        Vector3 SkinCorner(int x, int z, float planeY, int corner, float rise)
        {
            int cx = x + SkinCornerX[corner], cz = z + SkinCornerZ[corner];
            return new Vector3(cx * CellMetrics.SizeXZ, planeY + rise + CornerRelief(cx, cz), cz * CellMetrics.SizeXZ);
        }

        // The last cell a point was lifted onto a ramp for: a meadow cell asks for up to three tufts
        // and a clump or two of dressing, and each would otherwise re-run the ramp's neighbour scan.
        int _rampCacheIndex = -1;
        bool _rampCacheHas;
        bool _rampCacheIsRamp;
        BankLayout.Ramp _rampCache;

        /// <summary>
        /// How far the skin's ramp lifts a point standing on the ground whose top is at
        /// <paramref name="surfaceY"/> — zero off a ramp. Grass, dressing and crops stand at points,
        /// so they are lifted onto the ramp rather than buried in it (the draped/lifted rule).
        /// </summary>
        float SkinRise(int x, int z, float surfaceY, float worldX, float worldZ)
        {
            if (!GroundSkin.Enabled || !BankLayout.LiftFigures) return 0f;
            int layer = Mathf.RoundToInt(surfaceY / CellMetrics.SizeY);
            var size = _model.Size;
            if (!size.Contains(x, z, layer)) return 0f;
            int index = size.Index(x, z, layer);
            if (index != _rampCacheIndex)
            {
                _rampCacheIndex = index;
                _rampCacheHas = BankLayout.GroundCorners(_model, x, z, layer, out _rampCache);
                _rampCacheIsRamp = _rampCacheHas && BankLayout.RampCorners(_model, x, z, layer, out _);
            }
            if (!_rampCacheHas) return 0f;
            float u = Mathf.Clamp01((worldX - x * CellMetrics.SizeXZ) / CellMetrics.SizeXZ);
            float v = Mathf.Clamp01((worldZ - z * CellMetrics.SizeXZ) / CellMetrics.SizeXZ);
            return _rampCache.HeightAt(u, v) * CellMetrics.SizeY + (_rampCacheIsRamp ? GroundSkin.RampLift : 0f);
        }

        /// <summary>
        /// The layer the surround's ground stands on (<c>TerrainSkirt.SurfaceLayer</c>), or -1 when
        /// there is no surround. Set by the renderer before it meshes; the skin's apron at the
        /// board's rim runs down (or up) to it.
        /// </summary>
        public int SurroundLevel { get; set; } = -1;

        /// <summary>
        /// How far the skin runs past the board's edge to meet the surround, in metres. Two cells: a
        /// rim one layer above the surround then slopes at about 30 degrees, which the ground shader
        /// still paints as grass, where a wall at the edge read as a brown line round the board
        /// from far out (owner's report via the look pass, design 38 §20).
        /// </summary>
        public const float ApronMetres = 5f;

        /// <summary>
        /// The skin's apron beyond each side of a rim cell that is the board's edge: a strip from the
        /// cell's own edge, at the heights it was drawn at, to the surround's ground at the apron's
        /// outer edge, so the meadow runs off the board instead of stopping at a step.
        /// </summary>
        void EmitApron(GroundSkinMesh skin, ModulePart part, int tint, int x, int z,
            Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3)
        {
            if (SurroundLevel < 0) return;
            var size = _model.Size;
            bool west = x == 0, east = x == size.SizeX - 1, south = z == 0, north = z == size.SizeZ - 1;
            if (!west && !east && !south && !north) return;

            Vector3[] corners = _apronCorners;
            corners[0] = c0; corners[1] = c1; corners[2] = c2; corners[3] = c3;
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int dx = Directions.DeltaX[dir], dz = Directions.DeltaZ[dir];
                if (size.Contains(x + dx, z + dz, 0)) continue;
                Vector3 a = corners[EdgeFirst[dir]], b = corners[EdgeSecond[dir]];
                var outward = new Vector3(dx, 0f, dz) * ApronMetres;
                Vector3 a2 = SurroundPoint(a + outward);
                Vector3 b2 = SurroundPoint(b + outward);
                skin.Triangle(part.Material, tint, part.IsFallback, a, b, b2, Vector3.up);
                skin.Triangle(part.Material, tint, part.IsFallback, a, b2, a2, Vector3.up);
            }

            // At a corner of the board the two strips leave a square between them: fill it.
            if ((west || east) && (south || north))
            {
                int cx = east ? 1 : 0, cz = north ? 1 : 0;
                int corner = cx == 0 ? (cz == 0 ? 0 : 3) : (cz == 0 ? 1 : 2);
                Vector3 c = corners[corner];
                var ox = new Vector3(east ? ApronMetres : -ApronMetres, 0f, 0f);
                var oz = new Vector3(0f, 0f, north ? ApronMetres : -ApronMetres);
                Vector3 px = SurroundPoint(c + ox), pz = SurroundPoint(c + oz), pxz = SurroundPoint(c + ox + oz);
                skin.Triangle(part.Material, tint, part.IsFallback, c, px, pxz, Vector3.up);
                skin.Triangle(part.Material, tint, part.IsFallback, c, pxz, pz, Vector3.up);
            }
        }

        readonly Vector3[] _apronCorners = new Vector3[4];

        // The last cell SkinsTop asked BankDips about, so SinkSkinTop does not ask again.
        int _dipIndex = -1;
        bool _dipHas;
        BankLayout.Ramp _dip;

        /// <summary>A point outside the board, on the surround's ground: its level plus its field.</summary>
        Vector3 SurroundPoint(Vector3 at)
        {
            var size = _model.Size;
            float boardX = size.SizeX * CellMetrics.SizeXZ, boardZ = size.SizeZ * CellMetrics.SizeXZ;
            float outX = Mathf.Max(0f, Mathf.Max(-at.x, at.x - boardX));
            float outZ = Mathf.Max(0f, Mathf.Max(-at.z, at.z - boardZ));
            float outside = Mathf.Sqrt(outX * outX + outZ * outZ);
            float y = (SurroundLevel + 1) * CellMetrics.SizeY + GroundRelief.SurroundHeightAt(at.x, at.z, outside);
            return new Vector3(at.x, y, at.z);
        }

        /// <summary>The flat earth this ground is made of, as the material its turf box wore.</summary>
        ModulePart? GroundPart(ushort terrain)
        {
            int module = _model.EarthModule(terrain, 0, showsAFace: false);
            if (module == 0) return null;
            ModulePart[] parts = _model.Library[module].Parts;
            return parts.Length > 0 ? parts[0] : null;
        }

        /// <summary>
        /// The ramp in a terrace's foot cell, and a skirt down any side where the ground beside it is
        /// lower than the ramp's edge — a cell drawn flat because a ramp would have capped it.
        /// </summary>
        void EmitRamp(ChunkBatch batch, int x, int z, int y)
        {
            if (!BankLayout.RampCorners(_model, x, z, y, out BankLayout.Ramp ramp)) return;
            BankLayout.Bank bank = BankLayout.At(_model, x, z, y);
            ModulePart? part = GroundPart(bank.Terrain);
            if (part == null) return;

            // Always daylit: the ramp needs open sky over it (BankLayout.CanBank).
            int tint = TintCode.Daylit(TintCode.Whole(TintCode.Terrain(bank.Terrain)), open: true);
            float plane = y * CellMetrics.SizeY + GroundSkin.RampLift;
            float h = CellMetrics.SizeY;
            Vector3 c0 = SkinCorner(x, z, plane, 0, ramp.R0 * h);
            Vector3 c1 = SkinCorner(x, z, plane, 1, ramp.R1 * h);
            Vector3 c2 = SkinCorner(x, z, plane, 2, ramp.R2 * h);
            Vector3 c3 = SkinCorner(x, z, plane, 3, ramp.R3 * h);

            GroundSkinMesh skin = batch.Skin;
            if (ramp.SplitZeroTwo)
            {
                skin.Triangle(part.Material, tint, part.IsFallback, c0, c2, c1, Vector3.up);
                skin.Triangle(part.Material, tint, part.IsFallback, c0, c3, c2, Vector3.up);
            }
            else
            {
                skin.Triangle(part.Material, tint, part.IsFallback, c0, c3, c1, Vector3.up);
                skin.Triangle(part.Material, tint, part.IsFallback, c1, c3, c2, Vector3.up);
            }
            batch.InstanceCount++;
            EmitApron(skin, part, tint, x, z, c0, c1, c2, c3);

            // Skirts. A neighbouring ramp meets this one exactly and a solid neighbour is a wall of
            // its own, so only an open neighbour lower than the edge needs closing — the flat cell
            // beside a trench, or the ground past the lip of a step down.
            var size = _model.Size;
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (!size.Contains(nx, nz, y)) continue;
                int neighbour = size.Index(nx, nz, y);
                if (_model.IsSolid(neighbour)) continue;

                int a = EdgeFirst[dir], b = EdgeSecond[dir];
                float mineA = ramp.Corner(a), mineB = ramp.Corner(b);
                float theirA = 0f, theirB = 0f;
                if (BankLayout.RampCorners(_model, nx, nz, y, out BankLayout.Ramp other))
                {
                    // The same world corner seen from the other cell: its offset is ours, less the step.
                    theirA = other.Corner(Mirror(a, dir));
                    theirB = other.Corner(Mirror(b, dir));
                }
                if (mineA <= theirA && mineB <= theirB) continue;

                Vector3 topA = SkinCorner(x, z, plane, a, mineA * h);
                Vector3 topB = SkinCorner(x, z, plane, b, mineB * h);
                Vector3 lowA = SkinCorner(x, z, plane, a, Mathf.Min(mineA, theirA) * h);
                Vector3 lowB = SkinCorner(x, z, plane, b, Mathf.Min(mineB, theirB) * h);
                var outward = new Vector3(Directions.DeltaX[dir], 0f, Directions.DeltaZ[dir]);
                skin.Triangle(part.Material, tint, part.IsFallback, topA, topB, lowB, outward, vertical: true);
                skin.Triangle(part.Material, tint, part.IsFallback, topA, lowB, lowA, outward, vertical: true);
            }
        }

        /// <summary>The index, in the neighbour across <paramref name="dir"/>, of our corner <paramref name="corner"/>.</summary>
        static int Mirror(int corner, int dir)
        {
            int cx = SkinCornerX[corner] - (Directions.DeltaX[dir] > 0 ? 1 : Directions.DeltaX[dir] < 0 ? -1 : 0);
            int cz = SkinCornerZ[corner] - (Directions.DeltaZ[dir] > 0 ? 1 : Directions.DeltaZ[dir] < 0 ? -1 : 0);
            for (int c = 0; c < 4; c++)
                if (SkinCornerX[c] == cx && SkinCornerZ[c] == cz) return c;
            return corner;
        }

        /// <summary>
        /// Whether this earth cell's top goes into the skin rather than a box: nothing but its top
        /// can be seen (no side open, open above, solid below), and nothing is built on it whose
        /// drape the ground must match to the millimetre — a slab, a wall, a bed. A tree is not
        /// built; woodland keeps its skin.
        /// </summary>
        internal bool SkinsTop(int index, int x, int z, int y)
        {
            if (!GroundSkin.Enabled) return false;
            var size = _model.Size;
            if (y + 1 >= size.SizeY) return false;
            // A side open onto water only: a stream bank, drawn as skin sloping down into it.
            if (ExposedSides(x, z, y) != 0)
            {
                _dipIndex = index;
                _dipHas = BankLayout.BankDips(_model, x, z, y, out _dip);
                return _dipHas;
            }
            int above = index + size.LayerStride;
            if (_model.IsSolid(above)) return false;
            if (y > 0 && !_model.IsSolid(index - size.LayerStride)) return false;
            if (_model.Floor(above) != CoreContent.SlabNone) return false;
            ushort edifice = _model.EdificeDef(above);
            if (edifice != 0 && !Odyssey.Sim.Worldgen.Natural.NaturalContent.IsNatural(edifice)) return false;
            return true;
        }

        /// <summary>
        /// The top of an earth cell as two triangles of skin — flat, or dipping at the corners that
        /// touch water (<see cref="BankLayout.BankDips"/>) — and, on a stream bank, a wall down each
        /// open side to the bed under the water.
        /// </summary>
        internal void SinkSkinTop(ChunkBatch batch, int module, int tint, int x, int z, int y)
        {
            ModulePart[] parts = _model.Library[module].Parts;
            if (parts.Length == 0) return;
            ModulePart part = parts[0];
            int cellIndex = _model.Size.Index(x, z, y);
            bool dips;
            BankLayout.Ramp dip;
            if (cellIndex == _dipIndex) { dips = _dipHas; dip = _dip; }
            else dips = BankLayout.BankDips(_model, x, z, y, out dip);
            // Or the bed under the water, rising to meet the bank (design 38 §24).
            bool shaped = dips || BankLayout.BedRises(_model, x, z, y, out dip);
            float h = CellMetrics.SizeY;
            float plane = (y + 1) * h;
            Vector3 c0 = SkinCorner(x, z, plane, 0, shaped ? dip.R0 * h : 0f);
            Vector3 c1 = SkinCorner(x, z, plane, 1, shaped ? dip.R1 * h : 0f);
            Vector3 c2 = SkinCorner(x, z, plane, 2, shaped ? dip.R2 * h : 0f);
            Vector3 c3 = SkinCorner(x, z, plane, 3, shaped ? dip.R3 * h : 0f);
            GroundSkinMesh skin = batch.Skin;
            if (shaped && dip.Fan)
            {
                // Eight triangles fanned from the centre, the same eight BankLayout.Ramp.HeightAt
                // reads back, so a figure on the shore stands on what is drawn.
                Vector3 centre = SkinPoint(x, z, plane, 0.5f, 0.5f, dip.C * h);
                _fanCorners[0] = c0; _fanCorners[1] = c1; _fanCorners[2] = c2; _fanCorners[3] = c3;
                for (int e = 0; e < 4; e++)
                {
                    Vector3 mid = SkinPoint(x, z, plane, FanEdgeU[e], FanEdgeV[e], dip.Edge(e) * h);
                    skin.Triangle(part.Material, tint, part.IsFallback, centre, _fanCorners[e], mid, Vector3.up);
                    skin.Triangle(part.Material, tint, part.IsFallback, centre, mid, _fanCorners[(e + 1) & 3], Vector3.up);
                }
            }
            else if (!shaped || dip.SplitZeroTwo)
            {
                skin.Triangle(part.Material, tint, part.IsFallback, c0, c2, c1, Vector3.up);
                skin.Triangle(part.Material, tint, part.IsFallback, c0, c3, c2, Vector3.up);
            }
            else
            {
                skin.Triangle(part.Material, tint, part.IsFallback, c0, c3, c1, Vector3.up);
                skin.Triangle(part.Material, tint, part.IsFallback, c1, c3, c2, Vector3.up);
            }
            batch.InstanceCount++;
            EmitApron(skin, part, tint, x, z, c0, c1, c2, c3);
            if (!dips) return;

            // The wall under each open side, from the dipped edge down to the bed the water lies
            // on: seen through the water, as the box's side was.
            var size = _model.Size;
            float bed = y * h;
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (!size.Contains(nx, nz, y) || _model.IsSolid(size.Index(nx, nz, y))) continue;
                int a = EdgeFirst[dir], b = EdgeSecond[dir];
                Vector3 topA = SkinCorner(x, z, plane, a, dip.Corner(a) * h);
                Vector3 topB = SkinCorner(x, z, plane, b, dip.Corner(b) * h);
                Vector3 lowA = SkinCorner(x, z, bed, a, 0f);
                Vector3 lowB = SkinCorner(x, z, bed, b, 0f);
                var outward = new Vector3(Directions.DeltaX[dir], 0f, Directions.DeltaZ[dir]);
                if (dip.Fan)
                {
                    // In two halves, through the edge's midpoint, or the wall stands proud of a
                    // fan whose midpoint is lower than the line between its corners. The bed rises
                    // to meet this edge, so the wall is behind it wherever the bed is skinned.
                    int edge = FanEdgeOf(Directions.DeltaX[dir], Directions.DeltaZ[dir]);
                    Vector3 topM = SkinPoint(x, z, plane, FanEdgeU[edge], FanEdgeV[edge], dip.Edge(edge) * h);
                    Vector3 lowM = SkinPoint(x, z, bed, FanEdgeU[edge], FanEdgeV[edge], 0f);
                    skin.Triangle(part.Material, tint, part.IsFallback, topA, topM, lowM, outward, vertical: true);
                    skin.Triangle(part.Material, tint, part.IsFallback, topA, lowM, lowA, outward, vertical: true);
                    skin.Triangle(part.Material, tint, part.IsFallback, topM, topB, lowB, outward, vertical: true);
                    skin.Triangle(part.Material, tint, part.IsFallback, topM, lowB, lowM, outward, vertical: true);
                    continue;
                }
                skin.Triangle(part.Material, tint, part.IsFallback, topA, topB, lowB, outward, vertical: true);
                skin.Triangle(part.Material, tint, part.IsFallback, topA, lowB, lowA, outward, vertical: true);
            }
        }

        // A shore fan's edges, numbered as BankLayout.Ramp numbers them: −z, +x, +z, −x.
        static readonly float[] FanEdgeU = { 0.5f, 1f, 0.5f, 0f };
        static readonly float[] FanEdgeV = { 0f, 0.5f, 1f, 0.5f };
        readonly Vector3[] _fanCorners = new Vector3[4];

        static int FanEdgeOf(int dx, int dz) => dx > 0 ? 1 : dx < 0 ? 3 : dz > 0 ? 2 : 0;

        /// <summary>
        /// A point of the skin inside a cell, (fu, fv) from its low corner, with the ground relief
        /// interpolated from the corners' — so an edge's midpoint lies on the straight edge a flat
        /// neighbour draws, and the two meet without a crack.
        /// </summary>
        Vector3 SkinPoint(int x, int z, float planeY, float fu, float fv, float rise)
        {
            float r00 = CornerRelief(x, z), r10 = CornerRelief(x + 1, z);
            float r01 = CornerRelief(x, z + 1), r11 = CornerRelief(x + 1, z + 1);
            float relief = Mathf.Lerp(Mathf.Lerp(r00, r10, fu), Mathf.Lerp(r01, r11, fu), fv);
            return new Vector3((x + fu) * CellMetrics.SizeXZ, planeY + rise + relief, (z + fv) * CellMetrics.SizeXZ);
        }

        /// <summary>
        /// Can any of this cell's four vertical faces be seen — is it a terrace riser, the wall of
        /// a cutting, or the side of an outcrop?
        ///
        /// <para>The horizontal half of <see cref="HasExposedFace"/>, split out because earth pays
        /// for coursed walls only where it has a wall to show. The world boundary counts as solid
        /// here for the same reason it does there: it is not an exposed face, and treating it as
        /// one drew a cross-section wall round the whole perimeter of the map.</para>
        /// </summary>
        bool ShowsAVerticalFace(int x, int z, int y) => ExposedSides(x, z, y) != 0;

        /// <summary>
        /// Which of this cell's four vertical faces can be seen, as a bitmask over
        /// <see cref="Directions"/>.
        ///
        /// <para>The chamfer needs the pattern and not just the count, because it has to go on the
        /// edges that are actually open and no others. Put it on all four and every riser cell
        /// opens a groove against the flat ground behind it, which is the same fault the rim ripple
        /// already made once.</para>
        ///
        /// <para>The world boundary counts as solid, for the same reason it does in
        /// <see cref="HasExposedFace"/>: it is not an exposed face, and treating it as one drew a
        /// cross-section wall round the whole perimeter of the map.</para>
        /// </summary>
        int ExposedSides(int x, int z, int y)
        {
            var size = _model.Size;
            int mask = 0;

            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (!size.Contains(nx, nz, y)) continue;
                if (!_model.IsSolid(size.Index(nx, nz, y))) mask |= 1 << dir;
            }

            return mask;
        }

        bool HasExposedFace(int index, int x, int z, int y)
        {
            var size = _model.Size;
            if (ShowsAVerticalFace(x, z, y)) return true;
            if (y + 1 >= size.SizeY) return true;
            if (!_model.IsSolid(index + size.LayerStride)) return true;
            // Downwards only matters at the very bottom, which nothing can see.
            return y > 0 && !_model.IsSolid(index - size.LayerStride);
        }

        void EmitFloor(ChunkBatch batch, int index, int x, int z, int y)
        {
            if (_model.IsSolid(index)) return; // a slab inside rock is not visible
            int module = _model.FloorModule(index);
            if (module == 0) return;
            // Draped, like every other thing that fills a cell - see EmitFacePanels for why a
            // lift cannot close a seam, and WaterContributor for the same argument made about tiles.
            //
            // And drawn as a sheet rather than as a plate. Draping closes the seam in *metres* and
            // still leaves it open by a pixel: a plate's rim ends exactly in the plane of its
            // neighbour's top face, ties with it on depth, and wins often enough to draw a dotted
            // line of dark wood along every seam in the colony. CellMetrics.FloorTile is the one
            // owner of both halves of the answer, and CellMetrics.FloorSheet carries the argument,
            // the measurements and the owner's two screenshots of it.
            // A store's wash goes on the slab as well as on bare ground, and asks about *this*
            // cell rather than the one above: a slab is drawn at the lower boundary of the cell a
            // pawn walks in, where the terrain quad belongs to the cell below. That is the half a
            // store needs and a growing zone never did — nothing grows through a slab, so
            // EmitTerrain was the only site the tilled bit ever had to exist on.
            int tint = TintCode.Stuff(_model.FloorStuff(index));
            if (_model.IsStoredHere(index)) tint = TintCode.Stored(tint);

            AddRoof(batch, module, tint,
                GroundRelief.Drape(CellMetrics.FloorCentre(x, z, y)) * CellMetrics.FloorTile);
        }

        /// <summary>
        /// The line round a stockpile (owner, 2026-09-23: "wash + edge outline"): a strip on each
        /// side of a stored cell whose neighbour on that side is not stored, so it follows the
        /// zone's <i>outer</i> edge and never draws the interior grid — the interior borders were
        /// what the owner objected to in the growing zone's old cover.
        ///
        /// <para>Baked into the chunk like the wash, so it costs one bucket per chunk that holds a
        /// store and nothing per frame (P10). Emitted at the store's own cell, which is where a
        /// slab would be drawn, so one placement serves bare ground and a built floor alike. A
        /// neighbour joining or leaving changes this cell's line, which is why
        /// <c>StorageZones.Mark</c> dirties the four neighbours' chunks as well as its own.</para>
        /// </summary>
        void EmitStoreEdge(ChunkBatch batch, int index, int x, int z, int y)
        {
            if (!_model.IsStoredHere(index)) return;
            var size = _model.Size;
            int tint = TintCode.Daylit(TintCode.StoreEdge(), _model.OpenToTheSky(index, y));
            Matrix4x4 at = GroundRelief.Drape(CellMetrics.FloorCentre(x, z, y));
            int module = _model.StoreEdgeModule;

            if (x + 1 >= size.SizeX || !_model.IsStoredHere(index + 1)) AddRoof(batch, module, tint, at * CellMetrics.StoreEdge(0));
            if (x == 0 || !_model.IsStoredHere(index - 1)) AddRoof(batch, module, tint, at * CellMetrics.StoreEdge(1));
            if (z + 1 >= size.SizeZ || !_model.IsStoredHere(index + size.SizeX)) AddRoof(batch, module, tint, at * CellMetrics.StoreEdge(2));
            if (z == 0 || !_model.IsStoredHere(index - size.SizeX)) AddRoof(batch, module, tint, at * CellMetrics.StoreEdge(3));
        }

        void EmitEdifice(ChunkBatch batch, int index, int x, int z, int y)
        {
            ushort def = _model.EdificeDef(index);
            if (def == CoreContent.EdificeNone) return;

            // A bush (design 45 §4) is a thing the simulation placed, drawn by the dressing's own
            // path so it looks exactly as the dressing's bushes did — the Meadow art, the jittered
            // placement, the tint that casts no shadow and never fades. It has no module of its
            // own, so it goes before the module test that would turn it away.
            if (NaturalContent.IsBush(def))
            {
                EmitBush(batch, def, x, z, y);
                return;
            }

            int module = _model.EdificeModule(index);
            if (module == 0) return;

            // **The stuff tint says what a thing was *built* from, and a tree was not built.**
            // A tree is placed with NaturalContent.StuffWood because that is what it is made of,
            // and it wears its own pack material — green canopy, brown trunk, already right. So
            // long as the wood tint was white the conflation cost nothing; the moment wood became
            // a brown multiply, so that a wooden wall stopped coming out as cream plaster, every
            // tree on the board would have been multiplied brown with it. A natural edifice takes
            // no stuff tint at all.
            //
            // A tree takes a tint of its own instead: which stand of wood it grows in, which is
            // what decides its four colours. TreeLook has the argument for why the stand and not
            // the tree — in short, that this code is a bucket key and a colour per tree would
            // multiply the tree buckets in every chunk by the length of the palette.
            if (NaturalContent.IsTree(def))
            {
                EmitTree(batch, module, def, x, z, y);
                return;
            }

            // And what is left wears what it was built of, with no id-range test in front of it.
            //
            // There used to be one — `def >= NaturalContent.FirstEdifice ? StuffNone : …` — which
            // meant "the natural things have no stuff", and while trees were the only natural
            // edifices it was true. The bed's id is 12 and FirstEdifice is 10, so that test would
            // now strip a bed of the wood or stone it was built from and draw every one of them
            // untinted. Trees having gone out above, the question the test was asking no longer
            // has anything to ask it about: whatever reaches here has a record, and the record
            // says what it is made of.
            int tint = TintCode.Stuff(_model.EdificeStuff(index));
            var shape = _model.Library[module].Shape;

            switch (def)
            {
                case CoreContent.EdificeDoor:
                    EmitDoor(batch, module, tint, index, x, z, y);
                    return;
                case CoreContent.EdificeStairLower:
                case CoreContent.EdificeStairUpper:
                    EmitStair(batch, module, tint, def, x, z, y);
                    return;
                case CoreContent.EdificeLadder:
                    EmitLadder(batch, module, tint, index, x, z, y);
                    return;
                case CoreContent.EdificeBed:
                    EmitBed(batch, module, tint, index, x, z, y);
                    return;
                case CoreContent.EdificeShelf:
                    EmitShelf(batch, module, tint, index, x, z, y);
                    return;
                case CoreContent.EdificeSandbags:
                case CoreContent.EdificeBarricade:
                    EmitCover(batch, module, tint, def, x, z, y);
                    return;
                // Power's machines, drawn from pack art once from the head at the middle of their
                // footprint (design 32 §14). Only when the art resolved: a clone without the packs
                // has the tinted block, which is drawn per cell below as it always was.
                case CoreContent.EdificeGenerator:
                case CoreContent.EdificeHeater:
                case CoreContent.EdificeGalley:
                    if (shape == ModuleShape.Pillar)
                    {
                        // The galley's facing is the player's too: it is the side the cook
                        // stands on (design 48 §5), so it is drawn turned to it and never backed.
                        if (def == CoreContent.EdificeGalley)
                        {
                            if (_model.EdificeHead(index))
                                AddBody(batch, module, TintCode.Stuff(CoreContent.StuffNone),
                                    PropShape.Root(x, z, y, _model.EdificeFacing(index), 1));
                            return;
                        }

                        // The generator's facing is where its second cell lies, so it is the
                        // player's; the heater's is drawing only, and backs on to a wall (§14c).
                        if (_model.EdificeHead(index))
                            AddBody(batch, module, TintCode.Stuff(CoreContent.StuffNone), def == CoreContent.EdificeGenerator
                                ? PropShape.Root(x, z, y, _model.EdificeFacing(index), 2)
                                : PropShape.Root(x, z, y, _model.BackedFacing(index), 1));
                        return;
                    }
                    break;
                case CoreContent.EdificePillar:
                    AddWall(batch, module, tint,
                        GroundRelief.Drape(CellMetrics.FloorCentre(x, z, y)));
                    EmitPillarStump(batch, module, tint, x, z, y);
                    return;
                case CoreContent.EdificeUtilityTap:
                    AddBody(batch, module, tint,
                        GroundRelief.Drape(CellMetrics.FloorCentre(x, z, y)));
                    return;
            }

            if (shape != ModuleShape.WallPanel)
            {
                AddBody(batch, module, tint,
                    GroundRelief.Drape(CellMetrics.FloorCentre(x, z, y)));
                return;
            }

            EmitFacePanels(batch, module, tint, index, x, z, y);
            EmitWallCore(batch, tint, def, x, z, y);
            EmitWallStump(batch, tint, x, z, y);
        }

        /// <summary>
        /// A wall as it stands while the walls are down (design 42 §4): the core block, cut to
        /// <see cref="CellMetrics.StumpHeight"/> and draped exactly as the core is, in the wall's
        /// own tint. One per wall cell whatever its faces, and a window gets one too — a window is
        /// part of the wall line, and a gap in the stumps would read as a doorway.
        ///
        /// <para>Meshed beside the full wall rather than instead of it, into its own list, so the
        /// toggle is a choice of list at draw time and never a re-mesh. The block's pivot is at
        /// its base, so scaling it in y shortens it from the top down and it stays on the
        /// floor.</para>
        /// </summary>
        void EmitWallStump(ChunkBatch batch, int tint, int x, int z, int y)
        {
            int core = _model.WallCoreModule;
            if (core == 0) return;

            AddStump(batch, core, tint, GroundRelief.Drape(
                CellMetrics.FloorCentre(x, z, y) - Vector3.up * CoreRecess) * StumpScale);
        }

        /// <summary>The core block's height scaled to a stump's, about its base.</summary>
        static readonly Matrix4x4 StumpScale =
            Matrix4x4.Scale(new Vector3(1f, CellMetrics.StumpHeight / CellMetrics.SizeY, 1f));

        /// <summary>
        /// How far along the wall line each jamb of a lowered doorway runs, in metres. Wide enough
        /// to read as a post from the play camera, narrow enough that the opening between the two
        /// is most of the cell — which is what makes it read as a way through.
        /// </summary>
        public const float JambWidth = 0.35f;

        /// <summary>How thick a jamb is across the wall line: the thickness of the door frame.</summary>
        public const float JambDepth = 0.25f;

        /// <summary>
        /// A doorway as it stands while the walls are down: two short jambs, one at each end of
        /// the frame, in the frame's own face and turn (design 42 §4). The leaf is not drawn at
        /// all then — <c>DoorDirector</c> asks the same slice question — so the gap between the
        /// jambs is the doorway.
        /// </summary>
        void EmitDoorStump(ChunkBatch batch, int tint, int dir, int x, int z, int y)
        {
            int core = _model.WallCoreModule;
            if (core == 0) return;

            Matrix4x4 face = GroundRelief.Drape(CellMetrics.FaceCentre(x, z, y, dir)) *
                             Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[dir], 0f));
            var size = new Vector3(JambWidth / CellMetrics.SizeXZ, CellMetrics.StumpHeight / CellMetrics.SizeY,
                JambDepth / CellMetrics.SizeXZ);
            float along = CellMetrics.HalfXZ - JambWidth * 0.5f;
            AddStump(batch, core, tint, face * Matrix4x4.Translate(new Vector3(-along, 0f, 0f)) * Matrix4x4.Scale(size));
            AddStump(batch, core, tint, face * Matrix4x4.Translate(new Vector3(along, 0f, 0f)) * Matrix4x4.Scale(size));
        }

        /// <summary>
        /// A pillar as it stands while the walls are down: the core block cut to the pillar's own
        /// footprint and to a stump's height, so a colonnade still reads as a colonnade.
        /// </summary>
        void EmitPillarStump(ChunkBatch batch, int module, int tint, int x, int z, int y)
        {
            int core = _model.WallCoreModule;
            if (core == 0) return;

            Vector3 footprint = _model.Library[module].Bounds.size;
            var size = new Vector3(
                Mathf.Clamp01(footprint.x / CellMetrics.SizeXZ),
                CellMetrics.StumpHeight / CellMetrics.SizeY,
                Mathf.Clamp01(footprint.z / CellMetrics.SizeXZ));
            if (size.x <= 0f || size.z <= 0f) size.x = size.z = 0.3f;
            AddStump(batch, core, tint, GroundRelief.Drape(CellMetrics.FloorCentre(x, z, y)) * Matrix4x4.Scale(size));
        }

        /// <summary>
        /// How far the core's head sits below the panels', in metres.
        ///
        /// Only to keep two opaque surfaces off the same plane. A panel straddles its face, so
        /// 0.125 m of it stands inside the cell and its top would otherwise be exactly coplanar
        /// with the core's over that strip — which is a z-fight, and a z-fight along the whole
        /// head of every wall in the colony is a shimmering line the camera cannot get away from.
        /// A centimetre is sub-pixel from 32 m up, the nearest the camera comes, and where the
        /// panels do frame the core it reads as a coping rather than as a mistake.
        /// </summary>
        const float CoreRecess = 0.01f;

        /// <summary>
        /// The mass a wall is made of: one cell-shaped block behind the panels on its faces.
        ///
        /// <para><b>Why a wall needed one at all.</b> Panels are drawn on faces, which is what
        /// stops a one-cell wall reading as a 2.5 m slab — but a straight run puts two panels
        /// 2.5 m apart with 2.25 m of nothing between them and nothing over them. From a high
        /// camera every wall had a black slot down its middle, and a slice or an x-ray looked
        /// straight into it (owner, 2026-09-17). This fills the cell and caps it.</para>
        ///
        /// <para>It changes nothing about how thick a wall <i>reads</i>, and that is worth saying
        /// because it is the usual objection: a wall already occupied the full 2.5 m of its cell
        /// as drawn, two faces of it and a hole. A thinner wall is a different question and is
        /// kept in <c>15-building.md</c>.</para>
        ///
        /// <para>Hidden wherever it should be. A panel covers its whole face and stands 0.125 m
        /// proud of it, so on any face something can see through, the panel is what is seen; on
        /// any face it cannot, the neighbour is. Two cores in a run touch exactly, so a wall is
        /// one continuous mass and not a row of boxes.</para>
        ///
        /// <para><b>Not a window.</b> A window cell keeps its hollow, because filling it in is
        /// the one thing a window must not do.</para>
        /// </summary>
        void EmitWallCore(ChunkBatch batch, int tint, ushort def, int x, int z, int y)
        {
            if (def == CoreContent.EdificeWindow) return;
            int core = _model.WallCoreModule;
            if (core == 0) return;

            AddWall(batch, core, tint, GroundRelief.Drape(
                CellMetrics.FloorCentre(x, z, y) - Vector3.up * CoreRecess));
        }

        /// <summary>
        /// A panel on every face of this cell that something can be seen through.
        ///
        /// <para><b>Draped, not lifted, and this is what a built wall lives or dies by.</b> A
        /// lifted panel is flat and takes its height from its own centre, so two panels in a run
        /// sit at heights that differ by the relief's slope across a whole cell - 73 mm on
        /// average over this board and 220 mm at the worst of it, against a 3 m wall. That is a
        /// visible step at every cell join and a notch at every corner, which is exactly what a
        /// finished wall looked like the first time one went up (owner, 2026-09-17).
        ///
        /// A draped panel is sheared onto the tangent plane of the field at its own centre, so
        /// two panels are tangent planes of one smooth surface and part company at the vertical
        /// edge they share only by the field's curvature over a cell - about 11 mm, second order,
        /// and invisible. The shear leaves vertical edges vertical, so the wall does not lean and
        /// stays a full 3 m everywhere; only its head and its foot rake with the ground, which is
        /// what a wall built along a slope does. It is the same argument <see cref="WaterContributor"/>
        /// makes about tiles, and it was already the right one there.
        ///
        /// The drape is taken at the *face's* own centre rather than the cell's, for the reason
        /// the lift was: half a cell along a slope is enough for a panel and the cell it belongs
        /// to to disagree. And it composes on the left of the yaw, so the panel is turned in its
        /// own space and then sheared in the world's - the order <see cref="EmitBank"/> uses.</para>
        /// </summary>
        void EmitFacePanels(ChunkBatch batch, int module, int tint, int index, int x, int z, int y)
        {
            var size = _model.Size;
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (size.Contains(nx, nz, y) && _model.OccludesFace(size.Index(nx, nz, y))) continue;
                AddWall(batch, module, tint,
                    GroundRelief.Drape(CellMetrics.FaceCentre(x, z, y, dir)) *
                    Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[dir], 0f)));
            }
        }

        void EmitDoor(ChunkBatch batch, int module, int tint, int index, int x, int z, int y)
        {
            int dir = _model.DoorFacing(x, z, y);
            AddWall(batch, module, tint,
                GroundRelief.Drape(CellMetrics.FaceCentre(x, z, y, dir)) *
                Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[dir], 0f)));
            EmitDoorStump(batch, tint, dir, x, z, y);
        }

        void EmitStair(ChunkBatch batch, int module, int tint, ushort def, int x, int z, int y)
        {
            ushort partner = def == CoreContent.EdificeStairLower
                ? CoreContent.EdificeStairUpper
                : CoreContent.EdificeStairLower;
            int dir = FindNeighbour(x, z, y, partner);
            if (dir < 0) dir = Directions.North;

            // The climb runs lower -> upper. Seen from the upper half, that is the way it came.
            int climb = def == CoreContent.EdificeStairLower ? dir : Directions.Opposite(dir);
            float rise = def == CoreContent.EdificeStairLower ? 0f : CellMetrics.SizeY * 0.5f;

            AddBody(batch, module, tint,
                GroundRelief.Drape(CellMetrics.FloorCentre(x, z, y) + Vector3.up * rise) *
                Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[climb], 0f)));
        }

        void EmitLadder(ChunkBatch batch, int module, int tint, int index, int x, int z, int y)
        {
            // The face is the model's to decide, not the mesher's: the figure director has to hang
            // a climbing colonist on this exact plane, and while the rule lived here the two
            // disagreed wherever nothing occluded. See WorldRenderModel.LadderFacing.
            int facing = _model.LadderFacing(index);
            AddBody(batch, module, tint,
                GroundRelief.Drape(CellMetrics.FloorCentre(x, z, y)) *
                Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[facing], 0f)));
        }

        /// <summary>
        /// The bed, drawn once from the head cell as three scaled instances of the plain block
        /// module: a frame, a mattress, a pillow — the computed-swing idiom, an honest placeholder
        /// for two-tile art no pack contains (design 20 §9). The far cell points at the same
        /// record and draws nothing of it.
        ///
        /// <para><b>Centred on the seam and draped there</b>, because a bed is one thing fixed to
        /// the grid across two cells, and the rule the stepped walls settled is that anything
        /// fixed to the grid is draped. The scale factors are fractions of the block module's own
        /// cell extents, so the geometry is stated in cells and survives a cell-size change.</para>
        ///
        /// <para><b>It may straddle a chunk boundary</b> when its two cells sit across a chunk
        /// edge: instances are not clipped by their bucket, so it renders correctly from either
        /// side, and this is written here so nobody "fixes" it later. Both cells' chunks are
        /// marked dirty by the same edit that raised the bed.</para>
        ///
        /// <para>The pillow sits at the head end — local −Z — and the facing's yaw turns local +Z
        /// outward, so the head of a north-facing bed is the cell the order named, which is the
        /// cell the sleep chooser sends its owner to.</para>
        /// </summary>
        void EmitBed(ChunkBatch batch, int module, int tint, int index, int x, int z, int y)
        {
            if (!_model.BedHead(index)) return;

            int facing = _model.BedFacing(index);

            // The three parts come from BedShape, which the cursor's ghost asks as well: a bed
            // drawn one way under the pointer and another way on the board is how the build
            // cursor's whole bargain comes undone.
            //
            // The pillow is its own module and its own tint — a rounded shape rather than a box,
            // and linen rather than the stuff the frame is made of.
            Matrix4x4 root = BedShape.Root(x, z, y, facing);
            for (int part = 0; part < BedShape.PartCount; part++)
            {
                bool pillow = BedShape.IsPillow(part);
                AddBody(batch,
                    pillow ? _model.BedPillowModule : module,
                    pillow ? TintCode.Linen() : tint,
                    BedShape.Part(root, facing, part));
            }
        }

        /// <summary>
        /// A shelf: three boxes from <see cref="ShelfShape"/>, which the cursor's ghost asks as
        /// well, so a shelf drawn under the pointer and a shelf drawn on the board cannot disagree.
        ///
        /// <para>No head guard, unlike the bed: one cell, one record, one emitter. Three instances
        /// into the chunk's body buckets, keyed by module, part and tint — so forty wooden shelves
        /// in a chunk are three buckets and not a hundred and twenty.</para>
        /// </summary>
        void EmitShelf(ChunkBatch batch, int module, int tint, int index, int x, int z, int y)
        {
            int facing = _model.EdificeFacing(index);
            Matrix4x4 root = ShelfShape.Root(x, z, y, facing);
            for (int part = 0; part < ShelfShape.PartCount; part++)
                AddBody(batch, module, tint, ShelfShape.Part(root, facing, part));
        }

        readonly Matrix4x4[] _coverParts = new Matrix4x4[CoverShape.MaxParts];

        /// <summary>
        /// Sandbags or a barricade (design 50 §7): a core and an arm towards each neighbour that
        /// holds the same thing, so a dragged line is drawn joined. Sandbags wear the colour of sand
        /// whatever stone filled them; a barricade wears what it was built of. A neighbour raised or
        /// taken down re-meshes this chunk too (<c>MarkChunksAround</c>), so the join follows.
        /// </summary>
        void EmitCover(ChunkBatch batch, int module, int tint, ushort def, int x, int z, int y)
        {
            var size = _model.Size;
            int joins = 0;
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (size.Contains(nx, nz, y) && _model.EdificeDef(size.Index(nx, nz, y)) == def) joins |= 1 << dir;
            }
            if (def == CoreContent.EdificeSandbags) tint = TintCode.Terrain(NaturalContent.TerrainSand);
            int count = CoverShape.Parts(def, x, z, y, joins, _coverParts);
            for (int i = 0; i < count; i++) AddBody(batch, module, tint, _coverParts[i]);
        }

        int FirstOpenDirection(int x, int z, int y) => _model.DoorFacing(x, z, y);

        int FirstOccludingDirection(int x, int z, int y)
        {
            var size = _model.Size;
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (size.Contains(nx, nz, y) && _model.OccludesFace(size.Index(nx, nz, y))) return dir;
            }
            return -1;
        }

        int FindNeighbour(int x, int z, int y, ushort edificeDef)
        {
            var size = _model.Size;
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (!size.Contains(nx, nz, y)) continue;
                if (_model.EdificeDef(size.Index(nx, nz, y)) == edificeDef) return dir;
            }
            return -1;
        }

        // ------------------------------------------------------------- buckets

        /// <summary>
        /// A tree: one bucket per species per chunk, however many colours stand in it.
        ///
        /// <para>The four colours travel beside the matrix rather than in the tint code, which is
        /// the whole reason a wood of two hundred colours costs what a wood of one costs. See
        /// <c>InstanceBucket.BarkDeep</c> and the instancing buffer in <c>Odyssey/Tree</c>.</para>
        ///
        /// <para>A tree is drawn draped like everything else fixed to the grid, and it takes the
        /// same lift a pillar does, so this is the ordinary body path with a colour attached.</para>
        /// </summary>
        void EmitTree(ChunkBatch batch, int module, ushort def, int x, int z, int y)
        {
            TreeSpecies species = TreeLook.SpeciesOf(def);
            TreeTheme theme = TreePalette.At(TreeLook.Theme(x, z, species));
            Matrix4x4 placement = GroundRelief.Drape(CellMetrics.FloorCentre(x, z, y));

            // Which tree this is, and which way it faces (the look pass, design 38 §17): a species
            // has several pieces of art, chosen per cell, each turned and sized a little by the
            // hash so a wood is not one tree stamped in rows. Which rows are the species' own is
            // the simulation's species (design 45 §3), not a hash: a giant is a giant because the
            // simulation says so and fells like one. A family with one row — no packs, or the old
            // art — is drawn exactly as before.
            int[] variants = TreeVariantsOf(module);
            if (variants.Length > 1)
            {
                module = variants[TreeArt.VariantFor(def, x, z, variants.Length)];
                TreeArt.Stance(x, z, out float yaw, out float size);
                placement *= Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, yaw, 0f), Vector3.one * size);
            }

            var parts = _model.Library[module].Parts;
            int tint = TintCode.Tree(species);
            for (int p = 0; p < BucketsPerPlacement(module); p++)
            {
                InstanceBucket bucket = BucketFor(batch.Body, _bodyIndex, module, p, tint, _stacked);
                bucket.Add(placement * parts[p].Local,
                    Colour(theme.Bark.Shaded), Colour(theme.Bark.Lit),
                    Colour(theme.Leaf.Shaded), Colour(theme.Leaf.Lit));
                batch.InstanceCount++;
            }
        }

        /// <summary>
        /// A palette colour as the shader wants it: the bytes divided down, with no colour-space
        /// conversion.
        ///
        /// <para>Unconverted for the reason <c>ColonistMaterials.Colour</c> records — the tables are
        /// authored in sRGB hex and the project renders linear, and <c>Material.SetColor</c> is what
        /// converts a <c>Color</c> property. <b>A vector array is not a colour property</b>, so the
        /// conversion that used to happen for free has to happen here, and it is why this returns
        /// the linear value rather than the raw bytes: <c>MaterialPropertyBlock.SetVectorArray</c>
        /// hands its contents to the shader untouched.</para>
        /// </summary>
        static Vector4 Colour(Rgb24 c)
        {
            var srgb = new Color(c.R / 255f, c.G / 255f, c.B / 255f, 1f);
            Color value = QualitySettings.activeColorSpace == ColorSpace.Linear ? srgb.linear : srgb;
            return new Vector4(value.r, value.g, value.b, 1f);
        }

        void AddBody(ChunkBatch batch, int module, int tint, in Matrix4x4 placement) =>
            Add(batch.Body, _bodyIndex, batch, module, tint, placement);

        void AddRoof(ChunkBatch batch, int module, int tint, in Matrix4x4 placement) =>
            Add(batch.Roof, _roofIndex, batch, module, tint, placement);

        void AddWall(ChunkBatch batch, int module, int tint, in Matrix4x4 placement) =>
            Add(batch.Walls, _wallIndex, batch, module, tint, placement);

        void AddStump(ChunkBatch batch, int module, int tint, in Matrix4x4 placement) =>
            Add(batch.Stumps, _stumpIndex, batch, module, tint, placement);

        void Add(List<InstanceBucket> list, Dictionary<long, int> lookup, ChunkBatch batch,
            int module, int tint, in Matrix4x4 placement)
        {
            var parts = _model.Library[module].Parts;
            for (int p = 0; p < BucketsPerPlacement(module); p++)
            {
                BucketFor(list, lookup, module, p, tint, _stacked).Add(placement * parts[p].Local);
                batch.InstanceCount++;
            }
        }

        /// <summary>
        /// One bucket for a module drawn by level, whatever its part count; one per part otherwise.
        ///
        /// <para>A module drawn by level has every part of every level at the local transform of its
        /// first part (<see cref="ResolvedModule.DrawsByLevel"/>), so the first part's bucket holds
        /// the only matrix array it needs and the renderer draws the chosen level's parts from it.
        /// A bucket per part as well would draw the finest level twice over.</para>
        /// </summary>
        int BucketsPerPlacement(int module)
        {
            ResolvedModule resolved = _model.Library[module];
            return resolved.DrawsByLevel ? 1 : resolved.Parts.Length;
        }

        static InstanceBucket BucketFor(List<InstanceBucket> list, Dictionary<long, int> lookup,
            int module, int part, int tint, bool stacked)
        {
            long key = Key(module, part, tint, stacked);
            if (!lookup.TryGetValue(key, out int slot))
            {
                slot = list.Count;
                list.Add(new InstanceBucket { Module = module, Part = part, Tint = tint, Stacked = stacked });
                lookup.Add(key, slot);
            }
            return list[slot];
        }

        /// <summary>
        /// The bucket's identity as one number.
        ///
        /// <para><b>The tint gets thirty-two bits, and it used to get twenty.</b> That was enough
        /// while every tint was a small material index, and it silently stopped being enough when a
        /// tree code started carrying a value at bit 16: a code of fifteen million overflowed into
        /// the part field. Nothing was observably wrong, because the only modules with a tint that
        /// large had exactly one part — but "wrong only by luck" is not a property to leave in a
        /// key.</para>
        /// </summary>
        /// <summary>
        /// The bucket key: module, part, tint and whether it is an upper storey's. The stacked bit
        /// sits between the part (twelve bits) and the module, so no field is narrowed.
        /// </summary>
        static long Key(int module, int part, int tint, bool stacked) =>
            ((long)module << 45) | ((stacked ? 1L : 0L) << 44) | ((long)part << 32) | (uint)tint;

        static long KeyOf(InstanceBucket bucket) => Key(bucket.Module, bucket.Part, bucket.Tint, bucket.Stacked);
    }
}
