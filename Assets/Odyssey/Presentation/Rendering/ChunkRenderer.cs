#nullable enable
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Draws the world: one instanced call per (chunk, mesh, material), for the layers the slice
    /// asks for and no others.
    ///
    /// There is no <c>GameObject</c> per cell and there never will be — a single layer of the
    /// scale-target map is 62,500 cells. Geometry is meshed per chunk into matrix arrays and
    /// submitted with <see cref="Graphics.RenderMeshInstanced"/>; a chunk is re-meshed only when
    /// the mirror says it changed, and the slice decides which chunks are submitted at all rather
    /// than submitting everything and clipping it.
    ///
    /// Depth shading and ghosting are chosen at draw time from the same buckets, so moving the
    /// slice up or down does no meshing work whatsoever.
    /// </summary>
    public sealed class ChunkRenderer : System.IDisposable
    {
        /// <summary>
        /// Instances per call. <c>RenderMeshInstanced</c> takes up to 1023, but the ceiling halves
        /// when the pipeline also needs previous-frame matrices, so the conservative figure is
        /// used and the cost of the extra calls is nil.
        /// </summary>
        public const int MaxInstancesPerCall = 511;

        readonly WorldRenderModel _model;
        readonly ChunkMesher _mesher;
        readonly MaterialCache _materials = new MaterialCache();
        readonly ChunkBatch?[] _batches;

        public ChunkRenderer(WorldRenderModel model)
        {
            _model = model;
            _mesher = new ChunkMesher(model);
            _batches = new ChunkBatch?[model.Chunks.Count];
            Skirt = new TerrainSkirt(model, _materials);
        }

        /// <summary>
        /// The land outside the board: decoration, drawn from the same materials and measured off
        /// the same map, so the meadow does not end in mid-air at the rim.
        ///
        /// It lives here rather than beside the renderer because it shares the material cache and
        /// because its draw calls have to be counted with everyone else's. A figure in the readout
        /// that leaves the surround out is a figure that will be quoted at a milestone and be
        /// wrong.
        /// </summary>
        public TerrainSkirt Skirt { get; }

        /// <summary>Unity layer the geometry is drawn on. Set before the first frame.</summary>
        public int GameObjectLayer { get; set; }

        public bool CastShadows { get; set; } = true;

        /// <summary>
        /// Whether tufts of grass are drawn into the shadow map as well as into the picture.
        ///
        /// Off by default, and measured rather than assumed — see <c>RenderBench</c>. It is the
        /// same argument that keeps terrain out of the shadow pass: a shadow map stretched over a
        /// 300 m board has texels far larger than a clump of grass, so what comes back is not a
        /// shadow but a speckle, and there are tens of thousands of them.
        /// </summary>
        public bool FoliageCastsShadows { get; set; }

        /// <summary>
        /// Who every colonist is. Must be the <b>same object</b> the figure director holds, not an
        /// equal one, or a colonist changes identity on crossing the figure cap — see
        /// <see cref="ColonistAppearanceBook"/>.
        ///
        /// <para>Set it before the first render: the size of the face lottery is taken from it
        /// once, when the colonist rows are first prepared.</para>
        /// </summary>
        public ColonistAppearanceBook? Appearances { get; set; }

        /// <summary>The book, or one dealt from seed 0 so an editor harness still gets variety.</summary>
        ColonistAppearanceBook Cast =>
            Appearances ??= new ColonistAppearanceBook(0u, _model.Library.Catalogue);

        /// <summary>
        /// Where the viewer is, for the one culling decision that depends on distance. Null draws
        /// everything, which is what a headless measurement wants.
        /// </summary>
        public Vector3? ViewerPosition { get; set; }

        /// <summary>
        /// Metres beyond which chunks draw no grass. Infinite by default: grass is drawn to the
        /// rim of the board.
        ///
        /// A 110 m cutoff was introduced when the far meadow went dark, on the theory that a
        /// clump's facets average darker than flat ground at range. The theory was wrong: the
        /// darkness was the outline pass inking the ground itself at grazing angles, which
        /// <c>MeadowCheck</c> showed and the outline shader now explains. With that fixed, far
        /// grass looks as it should from every view, and the cutoff was a workaround that had
        /// outlived its problem. It is kept as a lever, per chunk against the chunk's bounds, for
        /// a machine that needs the far half of the board's largest instance count back: on the
        /// 120-cell meadow the whole field is 116 draw calls and 23,000 instances against 89 and
        /// 20,400 with the cutoff, measured under <c>MeadowCheck</c> on 2026-09-16.
        /// </summary>
        public float FoliageDrawDistance { get; set; } = DefaultFoliageDrawDistance;

        /// <summary>What the game ships with; <c>MeadowCheck</c> photographs against it.</summary>
        public const float DefaultFoliageDrawDistance = float.PositiveInfinity;

        /// <summary>
        /// Tufts of grass per hundred grass cells. Zero is bare ground. Changing it after chunks
        /// have been meshed has no effect until they are meshed again, which is the same rule
        /// every other meshing decision follows.
        /// </summary>
        public int ScatterDensity
        {
            get => _mesher.ScatterDensity;
            set
            {
                _mesher.ScatterDensity = value;
                // The first ring of the surround is strewn at the same density, fading out. Kept
                // in step here rather than read across at draw time so there is one setting, not
                // two that can disagree about how grassy the meadow is.
                Skirt.TuftDensity = value;
            }
        }

        /// <summary>
        /// Draw banks up terrace steps, and soil with a surface of its own. Both are levers for
        /// the check harness rather than settings anyone is expected to turn off, and both only
        /// take effect on chunks meshed after they change — the same rule
        /// <see cref="ScatterDensity"/> follows, because meshing is where the decision is made.
        /// </summary>
        public bool Banks
        {
            get => _mesher.Banks;
            set => _mesher.Banks = value;
        }

        /// <inheritdoc cref="Banks"/>
        public bool EarthGeometry
        {
            get => _mesher.Earth;
            set => _mesher.Earth = value;
        }

        /// <inheritdoc cref="Banks"/>
        public bool BanksInWorkings
        {
            get => _mesher.BanksInWorkings;
            set => _mesher.BanksInWorkings = value;
        }

        /// <summary>
        /// Off, everything happens except the submission itself. That makes the draw-call and
        /// instance counts measurable in a headless editor run with no graphics device, which is
        /// the only way to get real numbers into a milestone report from CI.
        /// </summary>
        public bool SubmitToGpu { get; set; } = true;

        /// <summary>
        /// The lines from the eye to each selected colonist. Anything standing on one of them is
        /// drawn ghosted instead of solid, so a selected colonist is never hidden behind a tree.
        ///
        /// <para>Null or empty is the ordinary case and costs a single null test per chunk: the
        /// buckets are submitted exactly as they were before this existed, from the arrays the
        /// mesher filled, with no copying and no extra call.</para>
        /// </summary>
        public SightLines? Sight { get; set; }

        /// <summary>How solid an occluder in the way is left. Zero would be invisible; this is a
        /// hint of what is there, in the same idiom as a ghosted storey above the slice.</summary>
        public float SightFadeAlpha { get; set; } = DefaultSightFadeAlpha;

        public const float DefaultSightFadeAlpha = 0.22f;

        /// <summary>
        /// How far above its own layer a chunk may hold geometry, in metres.
        ///
        /// <para>A chunk's bounds are one layer high, and the tallest thing rooted in a layer is a
        /// tree — which is precisely what the sight line is for. Without this allowance the coarse
        /// test would reject the very chunk holding the crown that is doing the hiding, and the
        /// feature would do nothing at all while every per-instance test still passed.</para>
        /// </summary>
        public const float TallestModuleMetres = 12f;

        /// <summary>Scratch, reused every frame: the instances of one bucket that are in the way,
        /// and the ones that are not. Partitioning in place would corrupt the mesher's array.</summary>
        Matrix4x4[] _solid = new Matrix4x4[64];
        Matrix4x4[] _faded = new Matrix4x4[64];

        // ---- last-frame measurements, for the milestone report and the on-screen readout ----

        public int DrawCalls { get; private set; }
        public int InstancesDrawn { get; private set; }
        public int ChunksDrawn { get; private set; }
        public int ChunksMeshedThisFrame { get; private set; }
        public int MaterialCount => _materials.MaterialCount;

        /// <summary>Instances drawn ghosted last frame because they stood in a line of sight.</summary>
        public int InstancesFaded { get; private set; }

        /// <summary>Chunks the coarse sight test admitted last frame, and so tested per instance.</summary>
        public int ChunksSightTested { get; private set; }

        /// <summary>Total chunks meshed since start. A large number in steady state is a bug.</summary>
        public int TotalChunksMeshed { get; private set; }

        public void Render(int activeLayer, SliceSettings slice)
        {
            DrawCalls = 0;
            InstancesDrawn = 0;
            ChunksDrawn = 0;
            ChunksMeshedThisFrame = 0;
            InstancesFaded = 0;
            ChunksSightTested = 0;

            // Before the board, not after it: the surround is the furthest thing in the scene, and
            // submitting it first lets the depth buffer reject it behind the board rather than
            // shading it and then overdrawing the lot.
            Skirt.GameObjectLayer = GameObjectLayer;
            Skirt.CastShadows = CastShadows;
            Skirt.SubmitToGpu = SubmitToGpu;
            Skirt.Render(activeLayer);
            DrawCalls += Skirt.DrawCalls;
            InstancesDrawn += Skirt.InstancesDrawn;

            var size = _model.Size;
            // Floored at the bottom of the landscape as well as at the policy's own limit. The
            // surface is terraced and spans several layers, so the depth budget alone deletes the
            // low ground and leaves its trees over the skybox. See
            // WorldRenderModel.LowestOutdoorLayer.
            int lowest = Mathf.Max(0, slice.LowestDrawnLayer(activeLayer, _model.LowestOutdoorLayer));
            // Capped at the top of the geometry as well as at the policy's own limit. Above the
            // surface the policy says "every layer, solid", and solid has no fade to cut the loop
            // short — so without this a tall map would mesh a dozen layers of empty sky on every
            // edit. See WorldRenderModel.HighestOccupiedLayer.
            int highest = Mathf.Min(size.SizeY - 1, slice.HighestDrawnLayer(activeLayer, size.SizeY));
            highest = Mathf.Max(activeLayer, Mathf.Min(highest, _model.HighestOccupiedLayer));
            int chunksPerLayer = _model.Chunks.ChunksX * _model.Chunks.ChunksZ;

            for (int layer = lowest; layer <= highest; layer++)
            {
                int steps = layer - activeLayer;
                bool above = steps > 0;
                bool ghost = above && slice.GhostsAbove(activeLayer);
                float alpha = ghost ? slice.AlphaAbove(activeLayer, steps) : 1f;
                if (ghost && alpha < SliceSettings.MinVisibleAlpha) continue;

                // The layer immediately below the active one is the floor being stood on, not a
                // storey beneath it, so it is lit as part of the active layer. Dimming it was
                // costing the ground a third of its brightness before a single light was applied,
                // and because the tint is a colour the shader reads in linear space, a 0.68
                // multiplier landed nearer 0.42 in practice. That alone made a healthy green
                // texture render as dark olive.
                float shade = above ? 1f : slice.ShadeBelow(-steps - 1);
                if (ghost) shade *= 1.15f; // translucent geometry reads darker than it is

                // The active layer's ceiling is the slab of the layer above it. Dropping it is
                // what makes interiors visible, and it is also exactly what roofs-off mode wants
                // for every layer it draws.
                AboveMode aboveMode = slice.AboveAt(activeLayer);
                bool drawRoof = true;
                if (above && aboveMode == AboveMode.RoofsOff) drawRoof = false;
                else if (steps == 1 && slice.SuppressCeilingAt(activeLayer)) drawRoof = false;

                int first = layer * chunksPerLayer;
                for (int i = 0; i < chunksPerLayer; i++)
                {
                    ChunkBatch batch = BatchFor(first + i);
                    if (batch.InstanceCount == 0) continue;
                    ChunksDrawn++;

                    // The coarse half of the sight test, asked once for the whole chunk. A layer
                    // already ghosted is left alone: it is translucent, so nothing in it is in
                    // anybody's way, and splitting its buckets would buy nothing.
                    bool sight = !ghost && Sight != null && Sight.Any
                                 && Sight.Touches(batch.Bounds, TallestModuleMetres);
                    if (sight) ChunksSightTested++;

                    DrawBuckets(batch, batch.Body, shade, ghost, alpha, sight);
                    if (drawRoof) DrawBuckets(batch, batch.Roof, shade, ghost, alpha, sight);
                }
            }
        }

        ChunkBatch BatchFor(int chunkIndex)
        {
            ChunkBatch? batch = _batches[chunkIndex];
            if (batch == null)
            {
                batch = new ChunkBatch();
                _batches[chunkIndex] = batch;
            }
            if (batch.Version != _model.Version)
            {
                _mesher.Mesh(batch, chunkIndex);
                ChunksMeshedThisFrame++;
                TotalChunksMeshed++;
            }
            return batch;
        }

        void DrawBuckets(ChunkBatch batch, System.Collections.Generic.List<InstanceBucket> buckets,
            float shade, bool ghost, float alpha, bool sight = false)
        {
            for (int b = 0; b < buckets.Count; b++)
            {
                InstanceBucket bucket = buckets[b];
                if (bucket.Count == 0) continue;

                ModulePart part = _model.Library[bucket.Module].Parts[bucket.Part];
                ResolveColour(bucket.Tint, part.IsFallback, shade, out Color tint, out Color emission);
                Material material = _materials.Get(part.Material, tint, emission, ghost, alpha,
                    foliage: TintCode.IsFoliage(bucket.Tint),
                    water: TintCode.IsWater(bucket.Tint));

                // Terrain receives shadows but never casts them, and that is not a saving so much
                // as a correctness fix. Ground is a contiguous mass of cell-sized boxes; letting
                // each box cast meant the surface shadowed itself, and with a shadow map stretched
                // over a 300 m board the texel is far larger than a cell, so every ground tile
                // acned against its neighbours. The result was a dark cross-hatch over the whole
                // map that read as filth on the grass rather than as light.
                //
                // Nothing worth seeing is lost: a colonist, a wall or a tree still casts onto the
                // ground, which is what actually tells the eye where something is standing. It
                // also takes 14,400 instances per layer out of the shadow pass.
                //
                // Foliage is the same argument a second time, and a bigger one. A meadow is
                // seventeen thousand clumps, each of which would be drawn again into the shadow
                // map to cast a shadow a few centimetres long onto grass of the same colour. The
                // reference art has no per-tuft shadows either — its ground is evenly lit and the
                // shadows that matter are the ones people and buildings cast onto it.
                // Water is terrain, so it already inherits terrain's "receives but never casts".
                bool terrain = TintCode.IsTerrain(bucket.Tint);
                bool foliage = TintCode.IsFoliage(bucket.Tint);

                if (foliage && ViewerPosition.HasValue
                    && batch.Bounds.SqrDistance(ViewerPosition.Value) > FoliageDrawDistance * FoliageDrawDistance)
                    continue;
                bool casts = CastShadows && !ghost && !terrain && (!foliage || FoliageCastsShadows);

                var rp = new RenderParams(material)
                {
                    worldBounds = batch.Bounds,
                    layer = GameObjectLayer,
                    receiveShadows = !ghost,
                    shadowCastingMode = casts ? ShadowCastingMode.On : ShadowCastingMode.Off,
                };

                // Grass is never in the way, and it is nearly every instance on the board. Leaving
                // foliage out of the partition is what keeps the cost of this feature confined to
                // the things that can actually hide a person.
                int faded = sight && !foliage ? Partition(bucket) : 0;
                if (faded == 0)
                {
                    Submit(rp, part, bucket.Matrices, bucket.Count);
                    InstancesDrawn += bucket.Count;
                    continue;
                }

                // Ghosted with the same tint the solid draw would have had, so what shows through
                // still reads as the tree or the wall it is, rather than as a grey pane. The
                // ghost material is the translucent stand-in the x-rayed storeys use; the pack's
                // own shaders are alpha-clipped and cannot be turned transparent from script.
                var ghostParams = new RenderParams(
                    _materials.Get(part.Material, tint, emission, ghost: true, SightFadeAlpha))
                {
                    worldBounds = batch.Bounds,
                    layer = GameObjectLayer,
                    receiveShadows = false,
                    shadowCastingMode = ShadowCastingMode.Off,
                };
                Submit(ghostParams, part, _faded, faded);
                Submit(rp, part, _solid, bucket.Count - faded);
                InstancesDrawn += bucket.Count;
                InstancesFaded += faded;
            }
        }

        /// <summary>
        /// Split one bucket's instances into the ones standing in a line of sight and the rest,
        /// into the reused scratch arrays. Returns how many are in the way.
        ///
        /// <para>The verdict is taken on the <em>module</em>, not on the part: a tree is a trunk
        /// bucket and a crown bucket, and asking each separately would fade the crown while
        /// leaving the trunk solid. The bucket stores <c>placement * part.Local</c>, so the
        /// placement is recovered once per bucket and the module's own bounds — which is what the
        /// selection cursor already fits to a thing — is placed by it.</para>
        /// </summary>
        int Partition(InstanceBucket bucket)
        {
            ResolvedModule module = _model.Library[bucket.Module];
            if (module.IsEmpty || Sight == null) return 0;

            Matrix4x4 unplace = module.Parts[bucket.Part].Local.inverse;
            Bounds local = module.Bounds;

            if (_faded.Length < bucket.Count)
            {
                int size = Mathf.NextPowerOfTwo(bucket.Count);
                _faded = new Matrix4x4[size];
                _solid = new Matrix4x4[size];
            }

            int fadedCount = 0;
            int solidCount = 0;
            for (int i = 0; i < bucket.Count; i++)
            {
                Matrix4x4 m = bucket.Matrices[i];
                if (Sight.Blocks(SightLines.Place(local, m * unplace))) _faded[fadedCount++] = m;
                else _solid[solidCount++] = m;
            }
            return fadedCount;
        }

        void Submit(in RenderParams rp, ModulePart part, Matrix4x4[] matrices, int count)
        {
            int drawn = 0;
            while (drawn < count)
            {
                int n = Mathf.Min(MaxInstancesPerCall, count - drawn);
                if (SubmitToGpu)
                    Graphics.RenderMeshInstanced(rp, part.Mesh, part.Submesh, matrices, n, drawn);
                drawn += n;
                DrawCalls++;
            }
        }

        public static void ResolveColour(int tintCode, bool fallback, float shade, out Color tint, out Color emission)
        {
            int value = TintCode.Value(tintCode);
            if (TintCode.IsLinen(tintCode))
            {
                // One colour, whatever art or stuff is underneath: bedding is bedding.
                tint = StuffPalette.Linen;
                emission = Color.black;
            }
            else if (TintCode.IsFoliage(tintCode))
            {
                // Foliage is only ever drawn from real art — the mesher drops a tuft module that
                // resolved to a primitive rather than strewing boxes over a meadow — so there is
                // no fallback colour to choose between here.
                tint = StuffPalette.FoliageTint(value);
                emission = Color.black;
            }
            else if (TintCode.IsWater(tintCode))
            {
                // Always the solid colour, whatever art resolved underneath: water is drawn by
                // Odyssey/Water and the palette entry *is* its colour. The alpha is carried
                // through untouched below, because for water it is the opacity.
                tint = StuffPalette.TerrainSolid(value);
                emission = Color.black;
            }
            else if (TintCode.IsTerrain(tintCode))
            {
                // Over a primitive the tint is the only colour there is. Over a texture it grades
                // what the texture already says, which for grass means lifting a muted meadow
                // olive to the saturated green the reference art uses.
                tint = fallback ? StuffPalette.TerrainSolid(value) : StuffPalette.TerrainTint(value);
                emission = StuffPalette.TerrainEmissive(value);
            }
            else
            {
                tint = StuffPalette.For(value, overArt: !fallback);
                emission = Color.black;
            }

            // Open to the sky means the depth shade has nothing to say. The shade measures how far
            // you are peering *through* the world, and there is nothing over an outdoor surface —
            // so a lower terrace is not dim ground, it is ground. Without this the meadow came out
            // in one green per terrace, which reads as lighting that no light explains.
            // TintCode.DaylitBase carries the whole argument.
            if (TintCode.IsDaylit(tintCode)) shade = 1f;

            // Alpha survives the shade for water and for nothing else. Everywhere else it is
            // meaningless and forcing it to one keeps the material key from splitting on noise.
            float keepAlpha = TintCode.IsWater(tintCode) ? tint.a : 1f;
            tint = new Color(tint.r * shade, tint.g * shade, tint.b * shade, keepAlpha);
            emission = new Color(emission.r * shade, emission.g * shade, emission.b * shade, 1f);
        }

        /// <summary>
        /// Draw the pawns and things the published snapshot contains, culled to the drawn layers.
        ///
        /// M1 has no pawns yet, so this normally draws nothing. It is here because it is the one
        /// place the renderer consumes <c>world.Views.Current</c> directly, and having it wired
        /// means the first pawn to exist appears without a rendering change.
        /// </summary>
        /// <param name="tickAlpha">How far this frame sits between two ticks, 0 to 1.</param>
        /// <param name="movePerTick">Cost units a pawn retires per tick, so a partial tick can be extrapolated.</param>
        /// <param name="drawnAsFigures">
        /// Pawn ids that already have a live animated figure in the scene, which must not also be
        /// drawn here. Without it a walking colonist renders twice — the figure and its own baked
        /// stand-in occupying the same cell — which reads as one colonist with a shadow problem
        /// rather than as two overlapping draws.
        /// </param>
        public void RenderActors(
            WorldSnapshot snapshot, int activeLayer, SliceSettings slice, Material material,
            float tickAlpha = 0f, int movePerTick = 0,
            System.Collections.Generic.HashSet<int>? drawnAsFigures = null)
        {
            if (snapshot.PawnCount == 0 && snapshot.ThingCount == 0) return;

            // Down to the bottom of the landscape, not merely to the depth budget — the same
            // band Render draws, because a colonist on a low terrace was being culled along with
            // the terrace. See WorldRenderModel.LowestOutdoorLayer.
            int lowest = Mathf.Max(0, slice.LowestDrawnLayer(activeLayer, _model.LowestOutdoorLayer));

            // Up to the highest layer anything is drawn on, NOT up to the active layer.
            //
            // **This was the owner's bug** (2026-09-16): "I couldn't see another person mining
            // above me." The layers above the slice were x-rayed, so the rock was there — but
            // everything alive in them was culled outright, by this line and by the one in
            // PawnFigureDirector. A colonist working a storey up simply did not exist on screen.
            // Actors are drawn solid at full opacity (owner's call), so the cut-off has to be the
            // fade cutoff rather than the nominal cap, or a figure is drawn in rock that is not.
            int highest = slice.HighestVisibleLayer(activeLayer, _model.Size.SizeY);

            EnsureColonistModules();
            System.Array.Clear(_colonistCounts, 0, _colonistCounts.Length);

            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                var cell = pawns[i].Cell;
                if (cell.Y < lowest || cell.Y > highest) continue;
                if (drawnAsFigures != null && drawnAsFigures.Contains(pawns[i].Id.Value)) continue;

                // Glide between cells rather than snapping. The simulation is discrete and
                // integer, which determinism requires; this is a presentation facade over it,
                // and it is shared with the animated figures so the two cannot disagree.
                Vector3 position = PawnPose.Of(pawns[i], tickAlpha, movePerTick, out Vector3 heading, _model);

                // The same face the live figures would have given this pawn, so a colonist does
                // not change identity on crossing the figure cap. Same object, same answer.
                int variant = Cast.LookFor(pawns[i].Id.Value);
                if ((uint)variant >= (uint)_colonistModules.Length) variant = 0;
                ResolvedModule colonist = ColonistModule(variant);

                if (!colonist.UsesArt || colonist.IsEmpty)
                {
                    // No licensed art: the stand-in is a body and a beacon, both deliberately
                    // larger than life, because a true-to-scale figure is a few pixels once the
                    // camera pulls back. That is how five colonists managed to be invisible.
                    // Measured against the lifted centre, because DrawMarker lifts as well.
                    // Against the flat one the lift would be counted in the drift and again in
                    // the marker, and the stand-in would float at twice the height of the ground.
                    Vector3 drift = position - GroundRelief.Lift(CellMetrics.FloorCentre(cell));
                    DrawMarker(material, cell, new Vector3(1.4f, 2.6f, 1.4f), 1.3f, drift);
                    DrawMarker(material, cell, new Vector3(0.7f, 0.7f, 0.7f), 3.6f, drift);
                    continue;
                }

                AppendColonist(variant, Matrix4x4.TRS(
                    position,
                    Quaternion.Euler(0f, FacingOf(pawns[i].Id, heading), 0f),
                    Vector3.one));

                // No beacon over a real figure. It was there to make a grey box findable, and
                // against the same orange the item markers use it read as one more piece of
                // clutter rather than as a colonist. Finding people at a glance is a job for the
                // interface, not for a cube floating over their heads.
            }

            for (int variant = 0; variant < _colonistCounts.Length; variant++)
                if (_colonistCounts[variant] > 0)
                    SubmitInstances(ColonistModule(variant),
                        _colonistPlacements[variant], _colonistCounts[variant], ref _actorMatrices);

            RenderThings(snapshot.Things, lowest, highest, material);
        }

        // ---- loose items ----------------------------------------------------------------------

        int[] _itemModules = System.Array.Empty<int>();
        Matrix4x4[][] _itemPlacements = System.Array.Empty<Matrix4x4[]>();
        int[] _itemCounts = System.Array.Empty<int>();
        Matrix4x4[] _itemMatrices = new Matrix4x4[16];

        /// <summary>Scratch for one heap's worth of rocks. Reused, never grown: ItemHeap caps it.</summary>
        readonly Matrix4x4[] _heapPlacements = new Matrix4x4[ItemHeap.Most];

        /// <summary>
        /// Draw the items lying on the ground, one instanced submission per item kind.
        ///
        /// Items are grouped by def before anything is submitted, because the alternative — a
        /// draw per item, which is what the stand-in marker did — costs a call for every ration
        /// crate on a map that will eventually hold thousands of them.
        /// </summary>
        void RenderThings(System.ReadOnlySpan<ThingView> things, int lowest, int highest, Material fallback)
        {
            if (things.Length == 0) return;
            EnsureItemModules();
            System.Array.Clear(_itemCounts, 0, _itemCounts.Length);

            for (int i = 0; i < things.Length; i++)
            {
                CellRef cell = things[i].Cell;
                if (cell.Y < lowest || cell.Y > highest) continue;

                int def = things[i].DefIndex;
                ResolvedModule? module = ItemModule(def);
                if (module == null || module.IsEmpty || !module.UsesArt)
                {
                    // No art for this kind — either the packs are absent or the def is newer than
                    // the catalogue. The stand-in marker is deliberately ugly so the gap shows.
                    DrawMarker(fallback, cell, new Vector3(1.2f, 0.8f, 1.2f), 0.4f);
                    continue;
                }

                Vector3 floor = CellMetrics.FloorCentre(cell);

                // Rubble is several rocks, and how many says how much. See ItemHeap: everything
                // else on the floor is one prop, and stone drawn that way was a cairn standing in
                // the cell rather than spoil lying in it.
                if (ItemHeap.TryRecipe(def, out ItemHeap.Recipe heap))
                {
                    int rocks = ItemHeap.Place(things[i].Stack, (uint)things[i].Id.Value,
                        floor, heap, _heapPlacements);

                    // Lifted one rock at a time, not once for the cell. The ground is a shallow
                    // field now rather than a plane, and a heap is spread over most of a metre —
                    // lift the centre and scatter from it and the outer rocks sit above or below
                    // the ground they are supposed to be lying on.
                    for (int rock = 0; rock < rocks; rock++)
                    {
                        Matrix4x4 placement = _heapPlacements[rock];
                        Vector3 at = GroundRelief.Lift(placement.GetColumn(3));
                        placement.SetColumn(3, new Vector4(at.x, at.y, at.z, 1f));
                        AppendItem(def, placement);
                    }

                    continue;
                }

                AppendItem(def, Matrix4x4.TRS(
                    GroundRelief.Lift(floor),
                    Quaternion.Euler(0f, YawOf(things[i].Id), 0f),
                    Vector3.one));
            }

            for (int def = 0; def < _itemCounts.Length; def++)
                if (_itemCounts[def] > 0)
                    SubmitInstances(_model.Library[_itemModules[def]], _itemPlacements[def],
                        _itemCounts[def], ref _itemMatrices);
        }

        void EnsureItemModules()
        {
            if (_itemModules.Length == ModuleIds.ItemModuleCount) return;

            _itemModules = new int[ModuleIds.ItemModuleCount];
            _itemCounts = new int[ModuleIds.ItemModuleCount];
            _itemPlacements = new Matrix4x4[ModuleIds.ItemModuleCount][];
            for (int i = 0; i < _itemModules.Length; i++)
            {
                // Pillar is only the shape of the primitive the library invents when the catalogue
                // has no row at all; in that case UsesArt is false and the marker is drawn
                // instead, so the invented shape is never what reaches the screen.
                _itemModules[i] = _model.Library.Resolve(ModuleIds.Item(i), ModuleShape.Pillar);
                _itemPlacements[i] = new Matrix4x4[16];
            }
        }

        ResolvedModule? ItemModule(int defIndex) =>
            defIndex >= 0 && defIndex < _itemModules.Length ? _model.Library[_itemModules[defIndex]] : null;

        void AppendItem(int def, in Matrix4x4 placement)
        {
            Matrix4x4[] into = _itemPlacements[def];
            if (_itemCounts[def] == into.Length)
            {
                System.Array.Resize(ref into, into.Length * 2);
                _itemPlacements[def] = into;
            }
            into[_itemCounts[def]++] = placement;
        }

        /// <summary>
        /// A stable bearing per item, so eight heaps of scrap are not eight copies of one heap.
        ///
        /// Derived from the id rather than drawn from a generator: presentation must not consume
        /// simulation randomness, and the same item has to face the same way after a reload.
        /// </summary>
        static float YawOf(ThingId id) => (id.Value * 137) % 360;

        // ---- colonist figures ---------------------------------------------------------------

        int[] _colonistModules = System.Array.Empty<int>();
        Matrix4x4[][] _colonistPlacements = System.Array.Empty<Matrix4x4[]>();
        int[] _colonistCounts = System.Array.Empty<int>();
        bool _colonistsResolved;
        Matrix4x4[] _actorMatrices = new Matrix4x4[16];
        readonly System.Collections.Generic.Dictionary<int, float> _facing =
            new System.Collections.Generic.Dictionary<int, float>();

        /// <summary>
        /// The baked colonist meshes, one per face the catalogue offers.
        ///
        /// This is the cheap far form of a colonist and the only form once the live-figure cap is
        /// reached, so it has to offer the same cast the figures do — otherwise a colony would be
        /// individuals up to the cap and identical twins past it.
        /// </summary>
        void EnsureColonistModules()
        {
            if (_colonistsResolved) return;
            _colonistsResolved = true;

            // Sized from the appearance book, not counted again here. Two places counting the
            // catalogue's colonist rows is two places that can come to disagree, and when they do
            // the symptom is every colonist changing face on crossing the figure cap.
            ModuleCatalogue? catalogue = _model.Library.Catalogue;
            int variants = Cast.LookCount;
            if (variants < 1) variants = 1;

            _colonistModules = new int[variants];
            _colonistCounts = new int[variants];
            _colonistPlacements = new Matrix4x4[variants][];
            for (int i = 0; i < variants; i++)
            {
                // Unresolved. Resolving a colonist means instantiating a rigged character and
                // baking its skinned meshes, which is not something to do sixty-one times on the
                // first frame for a colony of five — and most of those faces will never appear in
                // a given game at all. A face costs nothing until somebody wears it.
                _colonistModules[i] = -1;
                _colonistPlacements[i] = new Matrix4x4[16];
            }
        }

        /// <summary>The baked module for one face, baking it the first time it is asked for.</summary>
        ResolvedModule ColonistModule(int variant)
        {
            if (_colonistModules[variant] < 0)
                _colonistModules[variant] =
                    _model.Library.Resolve(ModuleIds.Colonist(variant), ModuleShape.Pillar);
            return _model.Library[_colonistModules[variant]];
        }

        void AppendColonist(int variant, in Matrix4x4 placement)
        {
            Matrix4x4[] into = _colonistPlacements[variant];
            if (_colonistCounts[variant] == into.Length)
            {
                System.Array.Resize(ref into, into.Length * 2);
                _colonistPlacements[variant] = into;
            }
            into[_colonistCounts[variant]++] = placement;
        }

        /// <summary>
        /// Which way a colonist is turned, in degrees.
        ///
        /// Remembered per pawn rather than derived per frame, because the snapshot only says where
        /// someone is heading while they are actually moving. Deriving it fresh each frame would
        /// snap everyone back to a default bearing the instant they stopped, so a colonist would
        /// pivot on arrival every single time. Keeping the last real heading means they stand
        /// facing wherever they walked in from, which is what a person does.
        /// </summary>
        float FacingOf(PawnId id, Vector3 heading)
        {
            if (heading.sqrMagnitude > 1e-4f)
            {
                float yaw = PawnPose.YawOf(heading);
                _facing[id.Value] = yaw;
                return yaw;
            }
            return _facing.TryGetValue(id.Value, out float last) ? last : 0f;
        }

        /// <summary>
        /// Submit <paramref name="count"/> copies of a module, one instanced call per part.
        ///
        /// Shared by colonists and by loose items: both are placements published in a snapshot
        /// rather than cells in the mirror, and both want the art drawn the same way the world is.
        /// <paramref name="scratch"/> is the caller's own buffer for placement × part, grown here
        /// so a caller never has to size it against a part count it cannot see.
        /// </summary>
        void SubmitInstances(ResolvedModule module, Matrix4x4[] placements, int count,
            ref Matrix4x4[] scratch)
        {
            if (count == 0) return;
            if (scratch.Length < count) scratch = new Matrix4x4[count];

            var parts = module.Parts;
            for (int p = 0; p < parts.Length; p++)
            {
                ModulePart part = parts[p];
                for (int i = 0; i < count; i++)
                    scratch[i] = placements[i] * part.Local;

                // Through the cache, not the pack material directly: the clone is what carries
                // GPU instancing, and a pack material does not have it switched on.
                Material material = _materials.Get(
                    part.Material, Color.white, Color.black, ghost: false, alpha: 1f);

                var rp = new RenderParams(material)
                {
                    layer = GameObjectLayer,
                    shadowCastingMode = CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true,
                };

                int sent = 0;
                while (sent < count)
                {
                    int n = Mathf.Min(MaxInstancesPerCall, count - sent);
                    if (SubmitToGpu)
                        Graphics.RenderMeshInstanced(rp, part.Mesh, part.Submesh, scratch, n, sent);
                    sent += n;
                    DrawCalls++;
                }
                InstancesDrawn += count;
            }
        }

        void DrawMarker(Material material, CellRef cell, Vector3 size, float height, Vector3 drift = default)
        {
            var rp = new RenderParams(material) { layer = GameObjectLayer };
            Matrix4x4 m = Matrix4x4.TRS(
                GroundRelief.Lift(CellMetrics.FloorCentre(cell)) + Vector3.up * height + drift,
                Quaternion.identity, size);
            Graphics.RenderMesh(rp, PrimitiveMeshes.UnitCube, 0, m);
            DrawCalls++;
        }

        // ---- the selection cursor -------------------------------------------------------------

        /// <summary>How much of each edge a corner stub covers. A fifth reads as a corner mark.</summary>
        public const float BracketStub = 0.18f;

        /// <summary>Stub thickness in metres, held constant so a small bracket is not a thin one.</summary>
        public const float BracketThickness = 0.07f;

        readonly Matrix4x4[] _bracketMatrices = new Matrix4x4[24];

        /// <summary>
        /// How see-through every cursor is, applied on top of the colour's own alpha.
        ///
        /// The rig's colour is the hue; this is the weight, and it lives with the drawing because
        /// it is a fact about how a cursor should sit on a scene rather than about which colour
        /// was chosen.
        ///
        /// Raised from 0.32 on the owner's eye, 2026-09-16: against the wooded meadow the cursor
        /// was hard to pick out at a glance, which is the one job it has. Still short of solid,
        /// because a cursor is a note on the world rather than a thing in it.
        /// </summary>
        public const float BracketOpacity = 0.62f;

        /// <summary>
        /// Emission on the cursor, which is what keeps it the same white in shade as in sun.
        ///
        /// Raised with the opacity above. The earlier note here warned that lifting emission made
        /// a translucent material read as solid — that was a fair objection when the alpha was
        /// 0.32 and the glow was doing the work of being visible on its own. With the alpha
        /// carrying it, the emission can go back to holding the colour steady under the light.
        /// </summary>
        const float BracketGlow = 0.85f;

        Material BracketMaterial(Color colour) =>
            _materials.Get(_model.Library.FallbackMaterial, colour, colour * BracketGlow,
                ghost: true, alpha: colour.a * BracketOpacity);

        readonly Matrix4x4[] _floorMatrices = new Matrix4x4[8];

        /// <summary>
        /// The cursor for a cell with nothing in it: four corners on the floor, two stubs each.
        ///
        /// Selecting empty ground has to show *something*, because a click with no visible
        /// answer reads as a click that was ignored — but a three-metre cube over bare grass says
        /// there is a thing there when there is not. A flat ring says "this square", which is all
        /// that is true, and it is the natural shape for the build and dig designations that will
        /// land on empty ground later.
        /// </summary>
        public void DrawFloorBracket(CellRef cell, Color colour)
        {
            Material material = BracketMaterial(colour);
            var rp = new RenderParams(material)
            {
                layer = GameObjectLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };

            // A few centimetres up, or the ring z-fights with the ground it is drawn on.
            Vector3 centre = CellMetrics.FloorCentre(cell) + Vector3.up * 0.04f;
            float half = CellMetrics.SizeXZ * 0.5f;
            float length = Mathf.Max(CellMetrics.SizeXZ * BracketStub, BracketThickness);
            int n = 0;

            for (int corner = 0; corner < 4; corner++)
            {
                float sx = (corner & 1) == 0 ? -1f : 1f;
                float sz = (corner & 2) == 0 ? -1f : 1f;
                // Each corner at its own height: the cell under the cursor is tilted, so a ring
                // drawn at one height would sink into the ground on one side and hover on the
                // other - which is exactly the tell that the cursor and the ground disagree.
                var at = GroundRelief.Lift(
                    new Vector3(centre.x + sx * half, centre.y, centre.z + sz * half));

                _floorMatrices[n++] = Matrix4x4.TRS(
                    at - new Vector3(sx * length * 0.5f, 0f, 0f), Quaternion.identity,
                    new Vector3(length, BracketThickness, BracketThickness));
                _floorMatrices[n++] = Matrix4x4.TRS(
                    at - new Vector3(0f, 0f, sz * length * 0.5f), Quaternion.identity,
                    new Vector3(BracketThickness, BracketThickness, length));
            }

            if (SubmitToGpu)
                Graphics.RenderMeshInstanced(rp, PrimitiveMeshes.UnitCube, 0, _floorMatrices, n);
            DrawCalls++;
            InstancesDrawn += n;
        }

        /// <summary>The bracket cursor around one whole cell.</summary>
        public void DrawCellHighlight(CellRef cell, Color colour) =>
            DrawSelectionBracket(
                GroundRelief.Lift(CellMetrics.Centre(cell.X, cell.Z, cell.Y)),
                new Vector3(CellMetrics.SizeXZ, CellMetrics.SizeY, CellMetrics.SizeXZ),
                colour);

        /// <summary>
        /// The selection cursor: a box drawn only at its corners, three short stubs meeting at
        /// each of the eight joins.
        ///
        /// **Why stubs rather than a filled box.** The cursor used to be a translucent cyan cube
        /// filling the cell, which put a wash over the very thing that had just been selected —
        /// worst of all on a colonist, who is the thing you most want to look at after clicking
        /// them. Corners mark the volume and leave the middle of every face open.
        ///
        /// **Why 24 matrices rather than a bracket mesh.** The stubs have to keep a constant
        /// thickness whatever they enclose: a cell is 2.5 x 3.0 x 2.5 and a colonist is nearer
        /// 1.1 x 2.7 x 1.1, and a single mesh scaled to both would come out three times thicker
        /// in one axis than another. Giving each stub its own matrix makes thickness exact, and it
        /// is still one instanced call because every stub is the same unit cube.
        /// </summary>
        /// <summary>
        /// How far a cell has been cut into, drawn as the material already taken out of it: a
        /// slab eating down from the top of the cell as the work goes on.
        ///
        /// <para><b>An overlay, and deliberately not the rock itself.</b> The obvious way to show
        /// a half-mined cell is to shrink its lump, and that is the one thing that must not
        /// happen: a cell that pulls in from its neighbours opens daylight at the joint, which is
        /// precisely the fault the whole solidity rule exists to prevent. The rock keeps filling
        /// its box for as long as it exists and then goes all at once; what changes is this.</para>
        ///
        /// <para>Eating downward rather than filling upward because that is the way a cut reads —
        /// the missing part is at the top, where a pick would have taken it. At nought nothing is
        /// drawn at all, so an untouched order is a bracket and no more.</para>
        /// </summary>
        /// <summary>
        /// Marks a cell as carrying a standing order: a thin translucent plate laid on the face a
        /// worker would come at it from.
        ///
        /// <para><b>Not the selection bracket, and that is the whole point of it existing.</b>
        /// Standing orders used to be drawn with <see cref="DrawCellHighlight"/>, which is the
        /// corner-stub cursor — so the starting scenario, which marks every tree within ten cells
        /// and three outcrops of stone, opened the game with a selection cursor around a hundred
        /// things at once. The owner's words were "there seems to be faint selection over every
        /// tree and stone". Nothing was broken; the wrong word was being used. A selection is one
        /// thing the player is looking at and an order is a job on a list, and if they look alike
        /// then neither means anything.</para>
        ///
        /// <para>On top of solid rock and on the floor of anything else, because that is the face
        /// you see it from: a mine order is read looking down at the stone, and a fell order is
        /// read on the ground the tree stands in. Inset from the cell edges so a row of marked
        /// cells reads as a row rather than as one continuous sheet, and flat, so it never
        /// competes with the thing it is marking.</para>
        /// </summary>
        public void DrawCellMark(CellRef cell, Color colour)
        {
            Material material = BracketMaterial(colour);
            var rp = new RenderParams(material)
            {
                layer = GameObjectLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };

            int index = _model.Index(cell.X, cell.Z, cell.Y);
            bool solid = _model.IsSolid(index);

            Vector3 centre = GroundRelief.Lift(CellMetrics.FloorCentre(cell));
            centre.y += solid ? CellMetrics.SizeY + MarkLift : MarkLift;

            const float Inset = 0.22f;
            var size = new Vector3(
                CellMetrics.SizeXZ - Inset * 2f, MarkThickness, CellMetrics.SizeXZ - Inset * 2f);

            Graphics.RenderMesh(in rp, PrimitiveMeshes.UnitCube, 0,
                Matrix4x4.TRS(centre, Quaternion.identity, size));
        }

        /// <summary>
        /// The outline of a whole cell, for a thing that is going to fill one.
        ///
        /// <para><b>A box rather than the floor plate <see cref="DrawCellMark"/> draws</b>, and the
        /// shape is the whole point. A mine order and a fell order are read looking down at a face
        /// that is already there, so paint on the floor says everything. A wall is not there yet
        /// and stands three metres tall: marked with a plate, a row of ordered walls reads as a
        /// path drawn on the grass. The owner asked for "some kind of outline" and this is it
        /// (2026-09-17).</para>
        ///
        /// <para>The same corner brackets a selected thing gets, so an outlined cell and an
        /// outlined colonist are recognisably the same language — one says "this is what you
        /// picked", the other "this is what you asked for".</para>
        /// </summary>
        public void DrawCellOutline(CellRef cell, Color colour)
        {
            Vector3 centre = GroundRelief.Lift(CellMetrics.Centre(cell.X, cell.Z, cell.Y));
            var size = new Vector3(
                CellMetrics.SizeXZ - OutlineInset * 2f,
                CellMetrics.SizeY - OutlineInset * 2f,
                CellMetrics.SizeXZ - OutlineInset * 2f);

            DrawSelectionBracket(centre, size, colour);
        }

        /// <summary>
        /// The run of cells a build drag covers, drawn as one closed box: every one of its twelve
        /// edges, sheared onto the ground the way the wall itself will be.
        ///
        /// <para><b>Not the selection cursor, and that is the point of it</b> (owner, 2026-09-17).
        /// A bracket is eight corner stubs and says "this is the thing you picked"; along a run of
        /// six cells it says it six times and reads as a dotted line. What a player wants to see
        /// before letting go of the button is <em>the wall</em> — where it starts, where it ends,
        /// how tall it will stand — so the cursor becomes the shape of the thing, closed on all
        /// twelve edges and spanning the whole run at once.</para>
        ///
        /// <para><b>Draped, not lifted</b>, which is the rule the stepped-wall fault settled: a
        /// thing that fills cells is sheared onto the tangent plane of the drawn ground at its own
        /// centre, so it lies along a slope the way the finished wall will. Lifting it would take
        /// one height from one point, and over a six-cell run the far end would float or sink by
        /// the field's slope across fifteen metres. Vertical edges stay vertical under the shear,
        /// so the box stands plumb and full height whatever it is standing on.</para>
        ///
        /// <para><b>Nothing calls this today, and it is kept deliberately</b> (2026-09-18). The
        /// build preview draws the ghosts of the things and nothing else now — the owner's ask was
        /// <i>"lets not print the cursor, just the shape/outline of what is going to be built …
        /// it's difficult to visualize anything and just adds noise"</i> — and once every cell
        /// carried a translucent model of its wall, this box was a second outline of the same run
        /// in a different colour, one cell bigger than the thing inside it. The geometry is right,
        /// was judged right by the owner when it landed, and is held by <c>BuildCursorTests</c>;
        /// it is one call away if the ghosts alone turn out to be too sparse. Delete it only on a
        /// decision, not as tidying.</para>
        /// </summary>
        public void DrawCellSpanBox(CellRef min, CellRef max, Color colour)
        {
            SpanBox(min, max, out Vector3 centre, out Vector3 size);
            DrawWireBox(centre, size, colour);
        }

        /// <summary>
        /// The run of cells a <b>floor</b> drag covers, drawn flat on the boundary the slab will
        /// be laid on rather than as a cell-tall box.
        ///
        /// <para><b>Because the cursor is the shape of the thing</b> — the argument
        /// <see cref="DrawCellSpanBox"/> already makes — and a floor is not a cube. A 3 m box drawn
        /// for a slab says "a wall goes here", it hides the tile it is promising underneath itself,
        /// and at the slice camera's working range it is impossible to tell which of two layers it
        /// is standing on. The owner reported exactly that (2026-09-17): *"the selection box for
        /// floors should be flat to the tile that it will be placed on rather than a cube."*</para>
        ///
        /// <para>Laid at <see cref="CellMetrics.FloorCentre"/>, which is where
        /// <c>ChunkMesher.EmitFloor</c> puts the slab itself, so the cursor and the finished floor
        /// occupy the same plane. Draped like everything fixed to the grid.</para>
        /// </summary>
        public void DrawCellSpanPlate(CellRef min, CellRef max, Color colour)
        {
            SpanPlate(min, max, out Vector3 centre, out Vector3 size);
            DrawWireBox(centre, size, colour);
        }

        /// <summary>
        /// The box a span of cells occupies: its centre and its extent, held in from the cell
        /// edges by <see cref="OutlineInset"/> so it never z-fights whatever it is drawn beside.
        /// </summary>
        public static void SpanBox(CellRef min, CellRef max, out Vector3 centre, out Vector3 size)
        {
            Vector3 low = CellMetrics.Centre(min.X, min.Z, min.Y);
            Vector3 high = CellMetrics.Centre(max.X, max.Z, max.Y);
            centre = (low + high) * 0.5f;

            size = new Vector3(
                (max.X - min.X + 1) * CellMetrics.SizeXZ - OutlineInset * 2f,
                (max.Y - min.Y + 1) * CellMetrics.SizeY - OutlineInset * 2f,
                (max.Z - min.Z + 1) * CellMetrics.SizeXZ - OutlineInset * 2f);
        }

        /// <summary>
        /// How thick the flat floor cursor is drawn, in metres.
        ///
        /// <para><b>A cursor convention rather than a model of the slab.</b> The slab prefab is
        /// 0.10 m deep, and at the slice camera's 32–160 m that is under a pixel — a cursor nobody
        /// can see is worse than one that is slightly fatter than the thing it promises. A tenth of
        /// a cell's height reads as a plate at every camera distance the game has, and against a
        /// wall cursor's full 3 m it cannot be mistaken for one.</para>
        ///
        /// <para>One line, and the owner's to tune.</para>
        /// </summary>
        public const float PlateThickness = CellMetrics.SizeY * 0.1f;

        /// <summary>
        /// The plate a span of cells occupies on its lower boundary: where a floor goes, rather
        /// than the volume a wall would fill.
        ///
        /// <para>Sits <em>on</em> the boundary — the box's underside is the plane the slab is laid
        /// on, so the cursor covers the tile it is promising instead of straddling it. Held in from
        /// the cell edges by the same <see cref="OutlineInset"/> the box uses, so a floor cursor
        /// beside a wall cursor lines up.</para>
        /// </summary>
        public static void SpanPlate(CellRef min, CellRef max, out Vector3 centre, out Vector3 size)
        {
            Vector3 low = CellMetrics.FloorCentre(min.X, min.Z, min.Y);
            Vector3 high = CellMetrics.FloorCentre(max.X, max.Z, max.Y);
            centre = (low + high) * 0.5f;
            centre.y += PlateThickness * 0.5f;

            size = new Vector3(
                (max.X - min.X + 1) * CellMetrics.SizeXZ - OutlineInset * 2f,
                PlateThickness,
                (max.Z - min.Z + 1) * CellMetrics.SizeXZ - OutlineInset * 2f);
        }

        /// <summary>
        /// A closed wireframe box: twelve bars, one per edge, placed by the ground's own drape at
        /// the box's centre.
        ///
        /// <para>Each bar is a unit cube stretched along its edge and left at
        /// <see cref="WireThickness"/> on the other two axes, and runs a thickness long so that
        /// three bars meeting at a corner close it rather than leaving a notch. The whole box goes
        /// in one instanced submission, as the bracket does.</para>
        /// </summary>
        /// <summary>
        /// A <b>ghost of a thing that does not exist yet</b>: the module a building would be drawn
        /// with, placed where it would stand, translucent.
        ///
        /// <para><b>The first cursor in this project that is the shape of the thing rather than a
        /// marker standing for it.</b> Everything else here draws a bracket, a box or a plate; the
        /// owner reported that they could not tell what they were about to build or where it would
        /// land, and a box cannot answer the first half of that (`19-build-cursor.md`).</para>
        ///
        /// <para><b>Translucency is not decoration, it is the existing vocabulary.</b> A storey
        /// above the slice is ghosted through this same <see cref="MaterialCache"/> path, so the
        /// player has already been taught that see-through means "there, but not your business
        /// right now" — and a thing that has not been built yet is the same sentence.</para>
        ///
        /// <para><b>The colour carries whether it can be built</b>, and green is deliberately not
        /// used for yes. A legal ghost wears its own material's tint, which is the affirmative
        /// signal: it looks like the wooden wall you asked for. Only the refusal needs a colour of
        /// its own, and it is the red this interface already uses for "this does nothing".</para>
        ///
        /// <para>One submission per part of the module, which for everything a player can build is
        /// one or two. Nothing here touches a chunk batch — a ghost is not in the world and must
        /// never be meshed into it.</para>
        /// </summary>
        public void DrawGhost(int module, Color colour, in Matrix4x4 placement)
        {
            if (module <= 0) return;

            var parts = _model.Library[module].Parts;
            for (int p = 0; p < parts.Length; p++)
            {
                ModulePart part = parts[p];
                Material material = _materials.Get(
                    part.Material, colour, colour * GhostGlow, ghost: true, alpha: colour.a);

                var rp = new RenderParams(material)
                {
                    layer = GameObjectLayer,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                };

                if (SubmitToGpu)
                    Graphics.RenderMesh(rp, part.Mesh, part.Submesh, placement * part.Local);
                DrawCalls++;
                InstancesDrawn++;
            }
        }

        /// <summary>
        /// How much the ghost lifts its own emission, so it stays readable against bright ground.
        ///
        /// <para>Lower than <see cref="BracketGlow"/>: a bracket is a line and has to compete with
        /// whatever it is drawn over, while a ghost is a solid volume and one that glows reads as a
        /// building already standing there — which is the one way this cursor could mislead rather
        /// than help (`19-build-cursor.md` §8).</para>
        /// </summary>
        const float GhostGlow = 0.45f;

        public void DrawWireBox(Vector3 centre, Vector3 size, Color colour)
        {
            Material material = BracketMaterial(colour);
            var rp = new RenderParams(material)
            {
                layer = GameObjectLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };

            // The drape is an object-to-world matrix about the centre, so every bar is positioned
            // in the box's own coordinates and carried onto the ground by it.
            int n = WireBoxEdges(size, GroundRelief.Drape(centre), _wireMatrices);

            if (SubmitToGpu)
                Graphics.RenderMeshInstanced(rp, PrimitiveMeshes.UnitCube, 0, _wireMatrices, n);
            DrawCalls++;
            InstancesDrawn += n;
        }

        /// <summary>
        /// The twelve bars of a closed box, in the placement's own space: four along each axis, at
        /// the four combinations of the other two axes' signs.
        ///
        /// <para>Separated from the draw so the geometry can be asserted rather than looked at. A
        /// cursor that is a box only when somebody presses Play is a cursor nobody can hold to
        /// twelve edges, to a full span or to standing plumb on a slope.</para>
        ///
        /// <para>Each bar overruns its edge by one thickness so that the three meeting at a corner
        /// close it instead of leaving a notch at every corner of every box.</para>
        /// </summary>
        public static int WireBoxEdges(Vector3 size, Matrix4x4 place, Matrix4x4[] into)
        {
            Vector3 half = size * 0.5f;
            int n = 0;

            for (int axis = 0; axis < 3; axis++)
            {
                int b = (axis + 1) % 3;
                int c = (axis + 2) % 3;

                for (int corner = 0; corner < 4; corner++)
                {
                    var offset = Vector3.zero;
                    offset[b] = (corner & 1) == 0 ? -half[b] : half[b];
                    offset[c] = (corner & 2) == 0 ? -half[c] : half[c];

                    var scale = new Vector3(WireThickness, WireThickness, WireThickness);
                    scale[axis] = size[axis] + WireThickness;

                    into[n++] = place * ScaledAt(offset, scale);
                }
            }

            return n;
        }

        /// <summary>
        /// A scale and a translation, written out rather than asked of <c>Matrix4x4.TRS</c>.
        ///
        /// <para>No bar of a box is rotated, so the quaternion in a TRS is an identity being
        /// multiplied through for nothing. The reason it is worth a method is the other half:
        /// <c>TRS</c> is an engine call and throws outside the player, so a box built with it can
        /// only be checked by looking at it, while this one can be measured in a test. The same
        /// argument <c>GroundRelief.Drape</c> makes for writing its own shear out by hand.</para>
        /// </summary>
        static Matrix4x4 ScaledAt(Vector3 offset, Vector3 scale)
        {
            var m = new Matrix4x4();
            m.m00 = scale.x;
            m.m11 = scale.y;
            m.m22 = scale.z;
            m.m33 = 1f;
            m.m03 = offset.x;
            m.m13 = offset.y;
            m.m23 = offset.z;
            return m;
        }

        /// <summary>
        /// Twelve edges, and the array is held rather than allocated because this is drawn every
        /// frame a build drag is running.
        /// </summary>
        readonly Matrix4x4[] _wireMatrices = new Matrix4x4[12];

        /// <summary>
        /// Thinner than a selection bracket's stub. A bracket has twenty-four short bars and has to
        /// be found at a glance; this has twelve long ones and would read as a solid crate at the
        /// same weight.
        /// </summary>
        public const float WireThickness = 0.05f;

        /// <summary>
        /// Held in from the cell edges so that two outlined cells side by side read as two, and so
        /// the box never z-fights the faces of whatever is standing next to it.
        /// </summary>
        const float OutlineInset = 0.12f;

        /// <summary>Clear of the face it is laid on, or it z-fights with it.</summary>
        const float MarkLift = 0.05f;

        /// <summary>A plate, not a box. Thin enough to read as paint rather than as a thing.</summary>
        const float MarkThickness = 0.04f;

        public void DrawCellCut(CellRef cell, float fraction, Color colour) =>
            DrawCellSlab(cell, fraction, colour, fromTheFloor: false);

        /// <summary>
        /// A slab growing out of the floor, for a thing being built.
        ///
        /// <para>The same slab as <see cref="DrawCellCut"/> the other way up, and the direction is
        /// the whole of the difference: a cut eats down from the top of the rock because that is
        /// where the pick lands, and a wall rises from the floor because that is how a wall is
        /// built. Drawn with a cut's downward fill, a wall at nine tenths would read as a wall with
        /// its bottom missing.</para>
        /// </summary>
        public void DrawCellFill(CellRef cell, float fraction, Color colour) =>
            DrawCellSlab(cell, fraction, colour, fromTheFloor: true);

        void DrawCellSlab(CellRef cell, float fraction, Color colour, bool fromTheFloor)
        {
            if (fraction <= 0.02f) return;
            if (fraction > 1f) fraction = 1f;

            Material material = BracketMaterial(colour);
            var rp = new RenderParams(material)
            {
                layer = GameObjectLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };

            // Inset a little so the slab sits inside the cell rather than z-fighting the faces of
            // the rock it is drawn over, and of whatever stands beside it.
            const float Inset = 0.06f;
            float height = CellMetrics.SizeY * fraction;
            var size = new Vector3(
                CellMetrics.SizeXZ - Inset * 2f, height, CellMetrics.SizeXZ - Inset * 2f);

            Vector3 centre = CellMetrics.Centre(cell.X, cell.Z, cell.Y);
            float offset = (CellMetrics.SizeY - height) * 0.5f;
            centre.y += fromTheFloor ? -offset : offset;

            Graphics.RenderMesh(in rp, PrimitiveMeshes.UnitCube, 0,
                Matrix4x4.TRS(centre, Quaternion.identity, size));
        }

        /// <summary>
        /// A whole cell washed in a colour, for an order given about a thing that <b>fills</b> its
        /// cell — a wall marked for demolition.
        ///
        /// <para><b>Why a wash and not the floor plate every other order gets.</b> A mine order and
        /// a fell order are read looking down at a face that is already there, so
        /// <see cref="DrawCellMark"/> paints the floor and that is the whole of it. A wall is three
        /// metres of solid thing standing in the cell, and its floor is <em>inside</em> it: the
        /// plate is drawn, correctly, exactly where the wall's own panels and core hide it. That is
        /// not a hypothesis — it is why the owner reported deconstruct as having no marker at all
        /// (2026-09-17).</para>
        ///
        /// <para><b>Proud of the cell rather than inset.</b> <see cref="DrawCellSlab"/> insets by
        /// 6 cm so a slab does not fight the faces of the rock it is drawn over; this has the
        /// opposite problem and needs the opposite answer, because anything inside the cell is
        /// behind an opaque wall. Three centimetres clears the panels and is sub-pixel at the
        /// nearest the camera comes, so the wall does not visibly grow.</para>
        ///
        /// <para><b>Draped, not lifted</b>, per the rule the stepped-wall fault produced: anything
        /// fixed to the grid is draped and only what moves over it is lifted. A run of walls marked
        /// together abuts, and a lift would step each wash against its neighbour by the ground's
        /// slope across a cell exactly as it once stepped the walls themselves.</para>
        /// </summary>
        public void DrawCellShade(CellRef cell, Color colour)
        {
            Material material = BracketMaterial(colour);
            var rp = new RenderParams(material)
            {
                layer = GameObjectLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };

            const float Outset = 0.03f;
            var size = new Vector3(
                CellMetrics.SizeXZ + Outset * 2f,
                CellMetrics.SizeY + Outset * 2f,
                CellMetrics.SizeXZ + Outset * 2f);

            Vector3 centre = CellMetrics.Centre(cell.X, cell.Z, cell.Y);
            Graphics.RenderMesh(in rp, PrimitiveMeshes.UnitCube, 0,
                GroundRelief.Drape(centre) * Matrix4x4.Scale(size));
        }

        public void DrawSelectionBracket(Vector3 centre, Vector3 size, Color colour)
        {
            // Translucent, and emissive so it does not go dim with the light: a cursor has to be
            // findable at a glance without becoming the brightest thing on the board. The alpha
            // rides on the colour, so the one dial on the camera rig sets both.
            Material material = BracketMaterial(colour);

            var rp = new RenderParams(material)
            {
                layer = GameObjectLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };

            Vector3 half = size * 0.5f;
            int n = 0;

            for (int corner = 0; corner < 8; corner++)
            {
                // The three bits of the corner index are the three signs, so this walks all eight.
                var sign = new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f);

                var at = new Vector3(
                    centre.x + sign.x * half.x,
                    centre.y + sign.y * half.y,
                    centre.z + sign.z * half.z);

                for (int axis = 0; axis < 3; axis++)
                {
                    // Never shorter than it is thick, or a bracket round something very flat
                    // degenerates into a scattering of cubes.
                    float length = Mathf.Max(size[axis] * BracketStub, BracketThickness);

                    var scale = new Vector3(BracketThickness, BracketThickness, BracketThickness);
                    scale[axis] = length;

                    // Runs from the corner inwards along this edge, so the bar ends exactly on
                    // the corner rather than straddling it.
                    Vector3 position = at;
                    position[axis] -= sign[axis] * length * 0.5f;

                    _bracketMatrices[n++] = Matrix4x4.TRS(position, Quaternion.identity, scale);
                }
            }

            if (SubmitToGpu)
                Graphics.RenderMeshInstanced(rp, PrimitiveMeshes.UnitCube, 0, _bracketMatrices, n);
            DrawCalls++;
            InstancesDrawn += n;
        }

        public void Dispose()
        {
            Skirt.Dispose();
            _materials.Dispose();
        }
    }
}
