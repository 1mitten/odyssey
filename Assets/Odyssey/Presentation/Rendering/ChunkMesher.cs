#nullable enable
using System.Collections.Generic;
using Odyssey.Presentation.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;
using Odyssey.Hud;

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
        readonly WorldRenderModel _model;

        public ChunkMesher(WorldRenderModel model) => _model = model;

        public void Mesh(ChunkBatch batch, int chunkIndex)
        {
            _model.ChunkBounds(chunkIndex, out int x0, out int z0, out int y, out int x1, out int z1);

            batch.ChunkIndex = chunkIndex;
            batch.Layer = y;
            batch.Bounds = ChunkWorldBounds(x0, z0, y, x1, z1);
            batch.Clear();
            _bodyIndex.Clear();
            _roofIndex.Clear();
            for (int i = 0; i < batch.Body.Count; i++)
                _bodyIndex[KeyOf(batch.Body[i])] = i;
            for (int i = 0; i < batch.Roof.Count; i++)
                _roofIndex[KeyOf(batch.Roof[i])] = i;

            var size = _model.Size;
            for (int z = z0; z < z1; z++)
            for (int x = x0; x < x1; x++)
            {
                int index = size.Index(x, z, y);
                EmitTerrain(batch, index, x, z, y);
                EmitBank(batch, index, x, z, y);
                EmitScatter(batch, index, x, z, y);
                EmitFloor(batch, index, x, z, y);
                EmitStoreEdge(batch, index, x, z, y);
                EmitEdifice(batch, index, x, z, y);
                EmitCrop(batch, index, x, z, y);
            }

            batch.Version = _model.ChunkVersion(chunkIndex);
        }

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

        static Bounds ChunkWorldBounds(int x0, int z0, int y, int x1, int z1)
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

        // ------------------------------------------------------------- scatter

        /// <summary>
        /// Tufts of grass per hundred grass cells. 60 means six cells in ten get one tuft and the
        /// rest are bare — sparse enough that the meadow reads as a field with grass on it rather
        /// than as grass with a field somewhere underneath, which is what 120 did at board
        /// distance. Zero turns scatter off entirely.
        /// </summary>
        public int ScatterDensity { get; set; } = 60;

        int[] _scatterModules = System.Array.Empty<int>();
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
            if (ScatterDensity <= 0) return;

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
                if (built != CoreContent.EdificeNone && !NaturalContent.IsTree(built)) return;
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
                // Power's machines, drawn from pack art once from the head at the middle of their
                // footprint (design 32 §14). Only when the art resolved: a clone without the packs
                // has the tinted block, which is drawn per cell below as it always was.
                case CoreContent.EdificeGenerator:
                case CoreContent.EdificeHeater:
                    if (shape == ModuleShape.Pillar)
                    {
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

            AddBody(batch, core, tint, GroundRelief.Drape(
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
                AddBody(batch, module, tint,
                    GroundRelief.Drape(CellMetrics.FaceCentre(x, z, y, dir)) *
                    Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[dir], 0f)));
            }
        }

        void EmitDoor(ChunkBatch batch, int module, int tint, int index, int x, int z, int y)
        {
            int dir = _model.DoorFacing(x, z, y);
            AddBody(batch, module, tint,
                GroundRelief.Drape(CellMetrics.FaceCentre(x, z, y, dir)) *
                Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[dir], 0f)));
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

            var parts = _model.Library[module].Parts;
            int tint = TintCode.Tree(species);
            for (int p = 0; p < BucketsPerPlacement(module); p++)
            {
                InstanceBucket bucket = BucketFor(batch.Body, _bodyIndex, module, p, tint);
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

        void Add(List<InstanceBucket> list, Dictionary<long, int> lookup, ChunkBatch batch,
            int module, int tint, in Matrix4x4 placement)
        {
            var parts = _model.Library[module].Parts;
            for (int p = 0; p < BucketsPerPlacement(module); p++)
            {
                BucketFor(list, lookup, module, p, tint).Add(placement * parts[p].Local);
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
            int module, int part, int tint)
        {
            long key = Key(module, part, tint);
            if (!lookup.TryGetValue(key, out int slot))
            {
                slot = list.Count;
                list.Add(new InstanceBucket { Module = module, Part = part, Tint = tint });
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
        static long Key(int module, int part, int tint) =>
            ((long)module << 44) | ((long)part << 32) | (uint)tint;

        static long KeyOf(InstanceBucket bucket) => Key(bucket.Module, bucket.Part, bucket.Tint);
    }
}
