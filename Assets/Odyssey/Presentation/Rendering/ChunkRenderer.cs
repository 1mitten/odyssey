#nullable enable
using Odyssey.Hud;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
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
            Appearances ??= AppearanceBooks.For(0u, _model.Library.Catalogue);

        /// <summary>
        /// Where the viewer is, for the one culling decision that depends on distance. Null draws
        /// everything, which is what a headless measurement wants.
        /// </summary>
        public Vector3? ViewerPosition { get; set; }
        /// <summary>
        /// This frame's crowd buckets, or null for the plain scan.
        ///
        /// <para>Set once a frame by the composition root, which rebuilds one index and hands the
        /// same one to every pass that poses a pawn — see <c>OdysseyBootstrap</c> and
        /// <see cref="Odyssey.Presentation.Rendering.PawnCrowdIndex"/>. Null is correct and merely
        /// slow, which is what a harness or an editor tool with no bootstrap gets.</para>
        /// </summary>
        public Odyssey.Presentation.Rendering.PawnCrowdIndex? Crowd { get; set; }

        /// <summary>Tracks loose items falling between layers and provides their drop offset.</summary>
        public ItemFallingTracker FallingItems { get; } = new ItemFallingTracker();

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

        /// <summary>
        /// Cell plates drawn last frame: an order's mark, a cut, a build's fill, a drag preview.
        ///
        /// <para>Its own counter and not merely a share of <see cref="DrawCalls"/>, because the
        /// question this pass keeps raising is "did it run at all" — it is filtered to the drawn
        /// slice band, so a board covered in orders on a plateau the camera is not slicing draws
        /// none of them, and a frame number taken that way says the pass is free when it simply
        /// did not happen (2026-09-20, and it cost a measurement).</para>
        /// </summary>
        public int CellPlatesDrawn { get; private set; }

        /// <summary>Chunks the coarse sight test admitted last frame, and so tested per instance.</summary>
        public int ChunksSightTested { get; private set; }

        /// <summary>Total chunks meshed since start. A large number in steady state is a bug.</summary>
        public int TotalChunksMeshed { get; private set; }

        /// <summary>
        /// The most chunks one frame will mesh. Zero or less is no limit.
        ///
        /// <para><b>Eleven, and the number came from the fault it fixes.</b> Until 2026-09-21 this
        /// did not exist: <see cref="BatchFor"/> meshed every stale chunk the draw walk touched, in
        /// that frame, however many there were. A traced player session on the Huge board at 4K
        /// measured what that costs — <b>fifty-six seconds and eight thousand frames with no
        /// meshing produced not one frame over 33 ms, while all 192 slow frames fell in the
        /// sixty-one seconds where meshing ran</b>. Sixty-one captured stalls meshed on their own
        /// frame and every one of them meshed 900 chunks: a whole board, in a frame, about 165 ms.
        /// §6c.6.</para>
        ///
        /// <para>165 ms for 900 chunks is about <b>0.18 ms a chunk</b>. Against a 5 ms frame, a
        /// meshing frame should not spend more than about 2 ms of it here, which is eleven.</para>
        ///
        /// <para><b>It breaks nothing to miss the budget.</b> A deferred chunk keeps
        /// <c>batch.Version != _model.Version</c>, so the next frame's walk finds it again — the
        /// staleness *is* the queue, and a second list of owed chunks would be a copy of state the
        /// batch already holds. What the player sees is a chunk one to three frames out of date, or
        /// one that arrives a frame late if it had no geometry at all. At 150 fps neither is
        /// visible, and the alternative is a sixth of a second of nothing.</para>
        /// </summary>
        public int MeshBudgetPerFrame { get; set; } = DefaultMeshBudgetPerFrame;

        /// <summary>What the game ships with. See <see cref="MeshBudgetPerFrame"/> for the arithmetic.</summary>
        public const int DefaultMeshBudgetPerFrame = 11;

        /// <summary>
        /// Chunks the last frame wanted to mesh and would not, because the budget was spent.
        ///
        /// <para>Its own counter because "the budget is working" and "the budget is starving the
        /// board" look identical in <see cref="ChunksMeshedThisFrame"/> — both cap it at the
        /// budget. A number that stays high for many seconds means the world is being dirtied
        /// faster than eleven chunks a frame can absorb, which is a different problem from the one
        /// this budget solves.</para>
        /// </summary>
        public int ChunksMeshDeferred { get; private set; }

        /// <summary>Set for the length of <see cref="PrimeAll"/>, which is the one unbudgeted walk.</summary>
        bool _priming;

        int _meshedThisFrame;

        /// <summary>
        /// What submitting the surround cost last frame, in milliseconds.
        ///
        /// <para><b>Its own number because the surround is the one pass whose cost was measured
        /// and then buried.</b> §6c found it was 3.65 ms of a 5 ms frame and cut it to about 2.2,
        /// and from that day it was charged to <c>FrameSection.World</c> alongside the chunk
        /// buckets — so nothing on the overlay or in <c>FrameTimeTests</c> could say which of the
        /// two a number belonged to. The board and the land beyond it scale with completely
        /// different things (chunks with the slice and the edits, the surround with the ring and
        /// the tree sectors), so one figure covering both answers no question anybody asks.</para>
        ///
        /// <para>CPU submission only. It cannot see fill or the shadow pass, which is exactly the
        /// axis §6c.3 says is unmeasured — read it beside the GPU frame time, never instead.</para>
        /// </summary>
        public double SurroundMs { get; private set; }

        readonly System.Diagnostics.Stopwatch _surroundTimer = new System.Diagnostics.Stopwatch();

        public void Render(int activeLayer, SliceSettings slice)
        {
            DrawCalls = 0;
            InstancesDrawn = 0;
            ChunksDrawn = 0;
            ChunksMeshedThisFrame = 0;
            InstancesFaded = 0;
            ChunksSightTested = 0;
            CellPlatesDrawn = 0;
            ChunksMeshDeferred = 0;
            _meshedThisFrame = 0;

            // Before the board, not after it: the surround is the furthest thing in the scene, and
            // submitting it first lets the depth buffer reject it behind the board rather than
            // shading it and then overdrawing the lot.
            Skirt.GameObjectLayer = GameObjectLayer;
            Skirt.CastShadows = CastShadows;
            Skirt.SubmitToGpu = SubmitToGpu;
            _surroundTimer.Restart();
            Skirt.Render(activeLayer);
            _surroundTimer.Stop();
            SurroundMs = _surroundTimer.Elapsed.TotalMilliseconds;
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

        /// <summary>
        /// Mesh everything the given view would draw, ignoring <see cref="MeshBudgetPerFrame"/>.
        ///
        /// <para><b>For the loading screen and nothing else.</b> On a new game every chunk is
        /// never-meshed, so a budgeted first frame would draw almost nothing and the board would
        /// arrive in instalments over several hundred frames while the player watched. The
        /// composition root calls this once, before the first drawn frame, which puts the stall
        /// where the player is already waiting — and where §6c.6 measured 14.7 seconds of worldgen
        /// stall already sitting.</para>
        ///
        /// <para>It is a <see cref="Render"/> with the budget off rather than a second walk, so
        /// there is no second copy of the rule about which chunks a view draws.</para>
        /// </summary>
        public void PrimeAll(int activeLayer, SliceSettings slice)
        {
            bool submitting = SubmitToGpu;
            _priming = true;
            try
            {
                // Nothing is shown from a priming pass: it exists to fill the batches, and
                // submitting a frame the player never sees would be a frame's work for nothing.
                SubmitToGpu = false;
                Render(activeLayer, slice);
            }
            finally
            {
                _priming = false;
                SubmitToGpu = submitting;
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
            // Per chunk, not the whole board: see WorldRenderModel.ChunkVersion for the
            // measurement that made this a per-chunk question.
            if (batch.Version != _model.ChunkVersion(chunkIndex))
            {
                // The whole of §6c.6's fix. Past the budget the chunk keeps the geometry it has
                // and stays stale, so the next frame's walk picks it up; nothing is dropped and
                // nothing is queued.
                if (!_priming && MeshBudgetPerFrame > 0 && _meshedThisFrame >= MeshBudgetPerFrame)
                {
                    ChunksMeshDeferred++;
                    return batch;
                }

                _mesher.Mesh(batch, chunkIndex);
                _meshedThisFrame++;
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

                // A tree is the one bucket whose colour is not a tint: it is four colours painted
                // into four cells of the pack atlas, which needs its own shader and its own cache.
                // Everything else — and a ghosted tree, which is drawn by the translucent stand-in
                // and has no atlas to repaint — goes the ordinary way. A null from the tree cache
                // means there was no shader or no art, and the fallback is exactly what a tree
                // drew before this feature existed.
                Material? painted = !ghost && TintCode.IsTree(bucket.Tint) && !part.IsFallback
                    ? _materials.Trees.For(part.Material, TintCode.TreeSpeciesOf(bucket.Tint), shade)
                    : null;
                Material material = painted ?? _materials.Get(part.Material, tint, emission, ghost, alpha,
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

                // The per-instance colours of a tree, which is what lets every colour in a chunk
                // share one draw. Built once per meshing rather than once a frame, and only when
                // the material that reads them is the one actually drawing.
                bool coloured = painted != null && bucket.IsColoured;

                int faded = sight && !NeverFades(bucket.Tint) ? Partition(bucket) : 0;
                if (faded == 0)
                {
                    if (coloured)
                        SubmitColoured(rp, part, bucket.Matrices, bucket.Count,
                            bucket.BarkDeep!, bucket.BarkWarm!, bucket.LeafDeep!, bucket.LeafFresh!,
                            PropsOf(bucket));
                    else
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
                // The solid half is a *filtered* subsequence, so its colours have to be gathered in
                // the same order — which Partition does as it splits, into scratch lists that are
                // reused. The ghosted half needs none: it is drawn by the translucent stand-in,
                // which has no atlas to repaint.
                if (coloured)
                    SubmitColoured(rp, part, _solid, bucket.Count - faded,
                        _solidBarkDeep, _solidBarkWarm, _solidLeafDeep, _solidLeafFresh, SolidProps());
                else
                    Submit(rp, part, _solid, bucket.Count - faded);
                InstancesDrawn += bucket.Count;
                InstancesFaded += faded;
            }
        }

        /// <summary>
        /// What the sight fade never touches, however squarely it stands in the beam.
        ///
        /// <para>The fade exists to stop something <em>hiding a person</em>, and three kinds of
        /// thing on the board cannot do that however much of the screen they cover.</para>
        ///
        /// <para><b>Foliage</b>, because grass is ankle-high and is also nearly every instance on
        /// the board — leaving it out is what keeps the cost of the feature confined to the things
        /// that can actually hide somebody.</para>
        ///
        /// <para><b>Water, banks and marsh</b> (owner, 2026-09-18, in two reports), because all
        /// three are <em>surfaces</em> rather than objects, and half a surface is not a view through
        /// it — it is a hole. A pond is drawn as a body of faces, so fading the instances the beam
        /// crosses opens a window into the bed of the stream and leaves a ragged edge where the beam
        /// stops; a bank is a sheet leaning on a terrace step that no cell in the simulation even
        /// contains, so fading it cuts a gap in a hillside that has no gap in it; and marsh is the
        /// wet fringe of the same pond, so fading it punched a hole in the shore right beside water
        /// that stayed whole. None of them ever stands between the camera and a colonist the way a
        /// wall or an outcrop does: a colonist in the water or the bog is standing <em>in</em> it,
        /// and one at the top of a step is above the bank, not behind it. The ground either side of
        /// them still fades, which is what the feature is for.</para>
        ///
        /// <para>Water keeps its own marker because it already had one; the bank and the bog share
        /// <c>TintCode.WholeBase</c>, which is named for this rule rather than for either of them,
        /// so the next surface that wants it needs nothing here.</para>
        /// </summary>
        public static bool NeverFades(int tint) =>
            TintCode.IsFoliage(tint) || TintCode.IsWater(tint) || TintCode.IsWhole(tint);

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

            bool coloured = bucket.IsColoured;
            if (coloured)
            {
                _solidBarkDeep.Clear();
                _solidBarkWarm.Clear();
                _solidLeafDeep.Clear();
                _solidLeafFresh.Clear();
            }

            int fadedCount = 0;
            int solidCount = 0;
            for (int i = 0; i < bucket.Count; i++)
            {
                Matrix4x4 m = bucket.Matrices[i];
                if (Sight.Blocks(SightLines.Place(local, m * unplace)))
                {
                    _faded[fadedCount++] = m;
                    continue;
                }

                _solid[solidCount++] = m;
                if (!coloured) continue;
                _solidBarkDeep.Add(bucket.BarkDeep![i]);
                _solidBarkWarm.Add(bucket.BarkWarm![i]);
                _solidLeafDeep.Add(bucket.LeafDeep![i]);
                _solidLeafFresh.Add(bucket.LeafFresh![i]);
            }
            return fadedCount;
        }

        readonly System.Collections.Generic.List<Vector4> _sliceBarkDeep = new System.Collections.Generic.List<Vector4>();
        readonly System.Collections.Generic.List<Vector4> _sliceBarkWarm = new System.Collections.Generic.List<Vector4>();
        readonly System.Collections.Generic.List<Vector4> _sliceLeafDeep = new System.Collections.Generic.List<Vector4>();
        readonly System.Collections.Generic.List<Vector4> _sliceLeafFresh = new System.Collections.Generic.List<Vector4>();
        MaterialPropertyBlock? _sliceProps;

        /// <summary>
        /// Submit instances that carry their own colours, in draw calls of at most
        /// <see cref="MaxInstancesPerCall"/>.
        ///
        /// <para><b>Why this is not just <see cref="Submit"/> with a block attached.</b> A property
        /// block's array is indexed from zero by <em>every</em> draw call, not from the instance
        /// offset the call starts at — so a run of instances split across two calls would hand the
        /// second call the colours of the first. Whole runs take the block they already have; a run
        /// long enough to split copies each slice into a scratch block instead.</para>
        ///
        /// <para>The split cannot happen today: a bucket is one module in one chunk, a chunk is 625
        /// cells, and one cell holds one tree. It is here because "cannot happen today" is a
        /// property of the board's dimensions rather than of this code, and the failure it would
        /// produce — a patch of wood wearing its neighbour's colours — is one nobody would read as
        /// a bug.</para>
        /// </summary>
        void SubmitColoured(RenderParams rp, ModulePart part, Matrix4x4[] matrices, int count,
            System.Collections.Generic.List<Vector4> barkDeep,
            System.Collections.Generic.List<Vector4> barkWarm,
            System.Collections.Generic.List<Vector4> leafDeep,
            System.Collections.Generic.List<Vector4> leafFresh,
            MaterialPropertyBlock whole)
        {
            if (count <= MaxInstancesPerCall)
            {
                rp.matProps = whole;
                Submit(rp, part, matrices, count);
                return;
            }

            int drawn = 0;
            while (drawn < count)
            {
                int n = Mathf.Min(MaxInstancesPerCall, count - drawn);
                _sliceProps ??= new MaterialPropertyBlock();
                Slice(_sliceProps, BarkDeepId, _sliceBarkDeep, barkDeep, drawn, n);
                Slice(_sliceProps, BarkWarmId, _sliceBarkWarm, barkWarm, drawn, n);
                Slice(_sliceProps, LeafDeepId, _sliceLeafDeep, leafDeep, drawn, n);
                Slice(_sliceProps, LeafFreshId, _sliceLeafFresh, leafFresh, drawn, n);

                rp.matProps = _sliceProps;
                if (SubmitToGpu)
                    Graphics.RenderMeshInstanced(rp, part.Mesh, part.Submesh, matrices, n, drawn);
                DrawCalls++;
                drawn += n;
            }
        }

        static void Slice(MaterialPropertyBlock props, int id,
            System.Collections.Generic.List<Vector4> scratch,
            System.Collections.Generic.List<Vector4> source, int from, int count)
        {
            scratch.Clear();
            for (int i = 0; i < count; i++) scratch.Add(source[from + i]);
            WritePadded(props, id, scratch);
        }

        /// <summary>
        /// Write one property's per-instance colours into a <em>shared</em> block, padded to the
        /// draw-call ceiling so that the array is the same length every time.
        ///
        /// <para><b>A property block fixes an array's length the first time it is set and caps every
        /// later set to it</b> — "Property (_LeafDeepColour) exceeds previous array size (31 vs 25).
        /// Cap to previous size." A block used once, at one size, never meets this; a block reused
        /// across buckets of different sizes meets it on the second bucket, and from then on every
        /// bucket larger than the first one drawn reads colours that were never written for it. That
        /// is the whole of the bug the sight fade showed: with see-through off nothing partitions and
        /// every tree draws from its own bucket's block, so the wood is right; with it on the solid
        /// halves all share this block, the first one seen locks the length, and the wood repaints
        /// itself as the selection moves and the locked length stops fitting.</para>
        ///
        /// <para>Padding is the fix rather than a fresh block per size because it is the fix already
        /// in use: <c>TreeMaterials.UniformProps</c> fills to the same ceiling for the same family of
        /// reason, that a block's array is indexed from zero by every draw call. The padding is never
        /// read — a draw of <c>n</c> instances indexes the first <c>n</c> entries — so what it holds
        /// does not matter, only that the length never changes. It costs the shared blocks a fixed
        /// upload; the per-bucket blocks of <see cref="PropsOf"/> are deliberately left unpadded,
        /// since each is written once at its own size and every tree on the board pays for those.</para>
        /// </summary>
        public static void WritePadded(MaterialPropertyBlock props, int id,
            System.Collections.Generic.List<Vector4> values)
        {
            while (values.Count < MaxInstancesPerCall) values.Add(default);
            props.SetVectorArray(id, values);
        }

        static readonly int BarkDeepId = Shader.PropertyToID("_BarkDeepColour");
        static readonly int BarkWarmId = Shader.PropertyToID("_BarkWarmColour");
        static readonly int LeafDeepId = Shader.PropertyToID("_LeafDeepColour");
        static readonly int LeafFreshId = Shader.PropertyToID("_LeafFreshColour");

        readonly System.Collections.Generic.List<Vector4> _solidBarkDeep = new System.Collections.Generic.List<Vector4>();
        readonly System.Collections.Generic.List<Vector4> _solidBarkWarm = new System.Collections.Generic.List<Vector4>();
        readonly System.Collections.Generic.List<Vector4> _solidLeafDeep = new System.Collections.Generic.List<Vector4>();
        readonly System.Collections.Generic.List<Vector4> _solidLeafFresh = new System.Collections.Generic.List<Vector4>();
        MaterialPropertyBlock? _solidProps;

        /// <summary>
        /// The bucket's own colours, as a block the draw can take. Kept on the bucket, so a chunk
        /// that nothing has changed pays for this once rather than once a frame.
        /// </summary>
        static MaterialPropertyBlock PropsOf(InstanceBucket bucket)
        {
            if (bucket.Props != null) return bucket.Props;

            var props = new MaterialPropertyBlock();
            props.SetVectorArray(BarkDeepId, bucket.BarkDeep);
            props.SetVectorArray(BarkWarmId, bucket.BarkWarm);
            props.SetVectorArray(LeafDeepId, bucket.LeafDeep);
            props.SetVectorArray(LeafFreshId, bucket.LeafFresh);
            bucket.Props = props;
            return props;
        }

        /// <summary>
        /// The same for the solid half of a partitioned bucket, which changes every frame the
        /// camera or the selection moves and so is rebuilt into one reused block. Reused, so
        /// <see cref="WritePadded"/> rather than a bare set — that is where the reuse bites.
        /// </summary>
        MaterialPropertyBlock SolidProps()
        {
            _solidProps ??= new MaterialPropertyBlock();
            WritePadded(_solidProps, BarkDeepId, _solidBarkDeep);
            WritePadded(_solidProps, BarkWarmId, _solidBarkWarm);
            WritePadded(_solidProps, LeafDeepId, _solidLeafDeep);
            WritePadded(_solidProps, LeafFreshId, _solidLeafFresh);
            return _solidProps;
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
            else if (TintCode.IsTree(tintCode))
            {
                // Over real art this is not what colours the tree — TreeMaterials repaints the
                // atlas's bark and canopy cells separately, which one multiply cannot do — and
                // white is what the tree drew before any of this existed, so a missing shader
                // degrades to the pack's own wood rather than to something wrong. Over a
                // primitive there is no atlas to repaint and the tint is the only colour there
                // is, so a stand-in box takes the theme's canopy and a clone without the packs
                // gets a green wood instead of a grey one.
                // The first theme of the species, because over a primitive there is no atlas to
                // repaint and no instancing buffer to read: a clone without the packs draws its
                // whole wood in one colour per species, which is a green box rather than a grey one.
                tint = fallback
                    ? TreeMaterials.Colour(TreePalette.At(
                        TreePalette.For(TintCode.TreeSpeciesOf(tintCode))[0]).Leaf.Lit)
                    : Color.white;
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

            // Worked soil: the earth of the cell, graded down. A multiply rather than a blend
            // towards black, because this is a tint on the ground's own texture and the texture
            // is the point - the field has to read as soil somebody turned over, not as paint
            // (owner, 2026-09-19: "the dirt tile is black with no texture instead the brown that
            // was before"). The factor is the old translucent cover's own arithmetic: it sat at
            // alpha 0.78 over the lit ground, so 22% of the earth came through, and 0.25 lands in
            // the same place while keeping every bit of the texture's variation instead of
            // flattening it onto a near-black pedestal.
            if (TintCode.IsTilled(tintCode))
                tint = new Color(tint.r * TilledGrade.r, tint.g * TilledGrade.g,
                    tint.b * TilledGrade.b, tint.a);

            // A store's ground: the surface it already is, washed towards the store's own hue.
            // A lerp rather than the multiply above, and the difference is the difference between
            // the two features. Tilled earth IS a different material — a field is soil somebody
            // turned over, and a multiply says "this ground, darker". A store changes nothing
            // about the ground: the stone is still stone and the planks are still planks under
            // however many crates, so the wash has to sit *over* the surface and leave it
            // recognisable. Kept light for the same reason the hue is desaturated: a mine order is
            // worked off and a field becomes a crop, but a warehouse floor is a warehouse floor
            // for the rest of the colony's life, and a heavy wash over fifty cells for a hundred
            // hours is a screen the player stops seeing past.
            if (TintCode.IsStored(tintCode))
                tint = new Color(
                    tint.r + (StoredGrade.r - tint.r) * StoredWash,
                    tint.g + (StoredGrade.g - tint.g) * StoredWash,
                    tint.b + (StoredGrade.b - tint.b) * StoredWash,
                    tint.a);

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
            System.Collections.Generic.HashSet<int>? drawnAsFigures = null,
            ICarriedLoads? carried = null)
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
            EnsureAttachmentBuckets();
            System.Array.Clear(_colonistCounts, 0, _colonistCounts.Length);
            System.Array.Clear(_hairCounts, 0, _hairCounts.Length);
            System.Array.Clear(_beardCounts, 0, _beardCounts.Length);

            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                var cell = pawns[i].Cell;
                if (cell.Y < lowest || cell.Y > highest) continue;
                if (drawnAsFigures != null && drawnAsFigures.Contains(pawns[i].Id.Value)) continue;

                // An animal is drawn only as a figure (design 29): this pass deals every pawn a
                // colonist's face, and a hog past the figure cap wearing one would be worse than
                // a hog not drawn. A baked animal pose is a recorded gap, not an oversight.
                if (pawns[i].IsAnimal) continue;

                // Glide between cells rather than snapping. The simulation is discrete and
                // integer, which determinism requires; this is a presentation facade over it,
                // and it is shared with the animated figures so the two cannot disagree.
                Vector3 position = PawnPose.Of(pawns[i], tickAlpha, movePerTick, out Vector3 heading, _model, pawns, Crowd);

                // The same face the live figures would have given this pawn, so a colonist does
                // not change identity on crossing the figure cap. Same object, same answer.
                // Asked once and carried. LookFor is For(...).Look, and the far form needs the
                // whole appearance for the hair and the beard -- so asking for both cost every
                // far colonist two snapshot aspect lookups and two dictionary hits a frame.
                ColonistAppearance look = Cast.For(snapshot, pawns[i].Id);
                int variant = look.Look;
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

                Matrix4x4 placement = Matrix4x4.TRS(
                    position,
                    Quaternion.Euler(0f, FacingOf(pawns[i].Id, heading), 0f),
                    Vector3.one);
                AppendColonist(variant, placement);

                // Whatever this colonist is wearing on their head, at the head of the body they
                // are wearing. The appearance is the same object the figures read, so the person
                // past the cap is the person in front of the camera.
                if (colonist.HasHead && ColonistAttachments.Enabled)
                {
                    Matrix4x4 head = placement * colonist.Head;
                    if (Attachments.Hair(look.HairPiece).Usable)
                        AppendPiece(look.HairPiece, head, _hairPlacements, _hairCounts);
                    if (Attachments.Beard(look.BeardPiece).Usable)
                        AppendPiece(look.BeardPiece, head, _beardPlacements, _beardCounts);
                }

                // No beacon over a real figure. It was there to make a grey box findable, and
                // against the same orange the item markers use it read as one more piece of
                // clutter rather than as a colonist. Finding people at a glance is a job for the
                // interface, not for a cube floating over their heads.
            }

            for (int variant = 0; variant < _colonistCounts.Length; variant++)
                if (_colonistCounts[variant] > 0)
                    SubmitInstances(ColonistModule(variant),
                        _colonistPlacements[variant], _colonistCounts[variant], ref _actorMatrices);

            for (int i = 0; i < _hairCounts.Length; i++)
                if (_hairCounts[i] > 0)
                    SubmitPiece(Attachments.Hair(i), _hairPlacements[i], _hairCounts[i]);

            for (int i = 0; i < _beardCounts.Length; i++)
                if (_beardCounts[i] > 0)
                    SubmitPiece(Attachments.Beard(i), _beardPlacements[i], _beardCounts[i]);

            RenderThings(snapshot, lowest, highest, material, carried, tickAlpha, movePerTick);
        }

        /// <summary>
        /// Where a load rides on a colonist drawn as an instanced stand-in rather than as a live
        /// figure, as a fraction of a cell's height above the pawn's feet.
        ///
        /// <para>Those colonists are the ones past the figure cap, which is to say the ones
        /// furthest from the camera, and they have no arms to measure a cradle off. A flat waist
        /// offset is the honest answer: at that distance the question is whether the colonist is
        /// carrying something, not how well they are holding it. Design 24 §5a.</para>
        /// </summary>
        public const float StandInCarryHeight = 0.36f;

        /// <summary>And how far in front of them, in metres.</summary>
        public const float StandInCarryReach = 0.35f;

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
        void RenderThings(WorldSnapshot snapshot, int lowest, int highest,
            Material fallback, ICarriedLoads? carried, float tickAlpha, int movePerTick)
        {
            System.ReadOnlySpan<ThingView> things = snapshot.Things;
            if (things.Length == 0 && snapshot.PawnCount == 0 && snapshot.FallingCount == 0) return;
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

                // **On the shelf, not on the floor under it.** A contained thing is published at
                // its store's cell so that every count of what the colony holds stays right; this
                // is the one place that has to care which of the two it is looking at.
                //
                // It costs no extra draw calls, and that is the point rather than a hope: these
                // instances land in the per-def bucket that was going to be submitted anyway, so a
                // forty-shelf warehouse adds matrices and not submissions. The alternative — a pass
                // of its own over the shelves — is `docs/bug-patterns.md` P10, which cost the
                // growing zone 2,065 draw calls before it was deleted.
                if (things[i].Contained)
                {
                    int shelfIndex = _model.Size.Index(cell);
                    Matrix4x4 shelf = ShelfShape.Root(cell.X, cell.Z, cell.Y,
                        _model.EdificeFacing(shelfIndex));
                    Vector3 stand = ShelfShape.SlotCentre(shelf, _model.EdificeFacing(shelfIndex),
                        things[i].Slot);

                    // **A thing just set on a shelf is still leaving the hands that held it**, the
                    // same rule the floor path keeps one paragraph down and for the same reason:
                    // the simulation moves a load in one instant, and an instant transfer drawn
                    // literally is a teleport. The hands were a third of a metre in front of the
                    // colonist; the deck is a metre up. Without this the load pops.
                    Vector3 settling = Vector3.zero;
                    if (carried != null
                        && carried.TryGetSettling(things[i].Id.Value, out Vector3 fromHands, out float held))
                        settling = Vector3.Lerp(fromHands - stand, Vector3.zero,
                            CarryHandover.Fallen(held));

                    stand += settling;

                    if (ItemHeap.TryRecipe(def, out ItemHeap.Recipe onShelf))
                    {
                        // The same ramp and the same spiral, tightened: ItemHeap's recipes are
                        // sized for a 2.5 m cell floor and a slot is a fifth of that, so at the
                        // recipe's own numbers neighbouring stacks interleave.
                        //
                        // **The count as well as the spread.** Tightening only the spread left
                        // each heap with its cell-floor population, so eight full slots of wood
                        // drew twenty-four bundles in one cell's footprint and the rack was
                        // invisible under them. A bay holds one stack and reads as one or two
                        // bundles; the fill tell on a shelf is how many bays are taken.
                        var tight = new ItemHeap.Recipe(
                            Mathf.Min(onShelf.Fewest, ShelfShape.SlotLumps),
                            Mathf.Min(onShelf.Biggest, ShelfShape.SlotLumps),
                            onShelf.Full,
                            ShelfShape.SlotSpread, onShelf.SizeJitter, lyingDown: onShelf.LyingDown);

                        int rocks = ItemHeap.Place(things[i].Stack, (uint)things[i].Id.Value,
                            stand, tight, _heapPlacements);

                        for (int rock = 0; rock < rocks; rock++)
                        {
                            Matrix4x4 placement = _heapPlacements[rock];

                            // **No GroundRelief.Lift here**, unlike the loose-pile path below.
                            // ShelfShape.Root is already draped, so lifting again would float the
                            // goods a few centimetres off their own deck on sloping ground — and
                            // only on sloping ground, which is the kind of fault nobody
                            // reproduces.
                            Vector3 at = (Vector3)placement.GetColumn(3);
                            placement = Matrix4x4.TRS(at, placement.rotation,
                                placement.lossyScale * ShelfShape.GoodsScale);
                            AppendItem(def, placement);
                        }

                        continue;
                    }

                    AppendItem(def, Matrix4x4.TRS(stand,
                        Quaternion.Euler(0f, YawOf(things[i].Id), 0f),
                        Vector3.one * ShelfShape.GoodsScale));
                    continue;
                }

                // **A thing just put down is still falling out of the hands that held it.** The
                // simulation transfers it in one instant, because a thing is in a cell or in a
                // pair of hands and there is nothing sensible between — but the hands were a
                // third of a metre in front of the colonist and rather higher than this cell's
                // floor, so drawn literally that instant is a teleport. See CarryHandover.
                //
                // A whole-cell offset rather than a per-rock one: the heap keeps its own shape
                // and the shape lands together, which is a load being set down. Settling each
                // rock separately would be a load coming apart in mid-air.
                Vector3 falling = Vector3.zero;
                if (carried != null
                    && carried.TryGetSettling(things[i].Id.Value, out Vector3 leftHands, out float since))
                {
                    falling = Vector3.Lerp(
                        leftHands - GroundRelief.Lift(floor), Vector3.zero,
                        CarryHandover.Fallen(since));
                }
                else if (FallingItems.TryGetFallingOffset(things[i].Id.Value, out Vector3 worldFallOffset))
                {
                    falling = worldFallOffset;
                }

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
                        Vector3 at = GroundRelief.Lift(placement.GetColumn(3)) + falling;
                        placement.SetColumn(3, new Vector4(at.x, at.y, at.z, 1f));
                        AppendItem(def, placement);
                    }

                    continue;
                }

                AppendItem(def, Matrix4x4.TRS(
                    GroundRelief.Lift(floor) + falling,
                    Quaternion.Euler(0f, YawOf(things[i].Id), 0f),
                    Vector3.one));
            }

            RenderCarriedLoads(snapshot, carried, lowest, highest, tickAlpha, movePerTick);
            RenderFalling(snapshot, lowest, highest, fallback, tickAlpha);

            for (int def = 0; def < _itemCounts.Length; def++)
                if (_itemCounts[def] > 0)
                    SubmitInstances(_model.Library[_itemModules[def]], _itemPlacements[def],
                        _itemCounts[def], ref _itemMatrices);
        }

        /// <summary>
        /// Draw whatever is in the air on its way down (design 23 §6), into the same instanced
        /// batches as the piles it will join: the thing is its own art at the height
        /// <see cref="FallArc"/> gives the frame, over the cell the simulation said it lands in.
        ///
        /// <para><b>Nothing here is a cell, a save or a hash.</b> The simulation owns the flight
        /// and publishes it as a <see cref="FallingView"/>; this only decides where along that
        /// line the frame is, exactly as <see cref="PawnPose"/> does for a colonist between two
        /// cells. A paused world publishes the same view and the same alpha, so the thing hangs
        /// where it is.</para>
        ///
        /// <para>A flat pad is drawn on the landing cell for the whole flight, in the stand-in
        /// material: a player who jumped to the event from the Events panel is looking at an
        /// empty cell for two seconds, and the pad says where to keep looking. It is a cursor,
        /// not a thing, and it is the first thing to drop if it reads as clutter.</para>
        /// </summary>
        void RenderFalling(WorldSnapshot snapshot, int lowest, int highest, Material fallback, float tickAlpha)
        {
            System.ReadOnlySpan<FallingView> falling = snapshot.Falling;
            for (int i = 0; i < falling.Length; i++)
            {
                CellRef landing = falling[i].Landing;
                if (landing.Y < lowest || landing.Y > highest) continue;

                float height = FallArc.HeightAbove(snapshot.Size.SizeY, landing.Y, snapshot.Tick, tickAlpha,
                    falling[i].LaunchTick, falling[i].LandTick);

                DrawMarker(fallback, landing, new Vector3(1.8f, 0.04f, 1.8f), 0.02f);

                int def = falling[i].ThingDef;
                ResolvedModule? module = ItemModule(def);
                if (module == null || module.IsEmpty || !module.UsesArt)
                {
                    DrawMarker(fallback, landing, new Vector3(1.2f, 0.8f, 1.2f), 0.4f + height);
                    continue;
                }

                Vector3 lift = Vector3.up * height;
                Vector3 floor = CellMetrics.FloorCentre(landing);

                // A bearing and a heap layout from the launch and the cell rather than from an id:
                // the thing has no id until it lands, and the pile it becomes will take its own.
                uint seed = unchecked((uint)(falling[i].LaunchTick * 31 + snapshot.Size.Index(landing)));

                if (ItemHeap.TryRecipe(def, out ItemHeap.Recipe heap))
                {
                    int rocks = ItemHeap.Place(falling[i].Stack, seed, floor, heap, _heapPlacements);
                    for (int rock = 0; rock < rocks; rock++)
                    {
                        Matrix4x4 placement = _heapPlacements[rock];
                        Vector3 at = GroundRelief.Lift(placement.GetColumn(3)) + lift;
                        placement.SetColumn(3, new Vector4(at.x, at.y, at.z, 1f));
                        AppendItem(def, placement);
                    }
                    continue;
                }

                AppendItem(def, Matrix4x4.TRS(
                    GroundRelief.Lift(floor) + lift,
                    Quaternion.Euler(0f, (seed * 137u) % 360u, 0f),
                    Vector3.one));
            }
        }

        /// <summary>
        /// Draw what the colonists are holding, into the same instanced batches as the piles they
        /// came off. Design 24 §5a.
        ///
        /// <para><b>Between the ground items and the submit, on purpose.</b> A carried rock is
        /// one more matrix in a buffer that was going to be submitted anyway, so the whole feature
        /// costs no draw call at all — and it is the same mesh, the same material and the same
        /// size as the pile it was picked up from, which is the owner's "true scale" decision
        /// enforced by construction rather than by a constant that could drift.</para>
        ///
        /// <para><b>Placed by the figure director where there is a figure, and approximated where
        /// there is not.</b> Past the figure cap a colonist is an instanced stand-in with no arms
        /// to measure, and it is also a long way off; a flat waist offset answers the only
        /// question anybody is asking at that distance, which is whether they are carrying
        /// something.</para>
        ///
        /// <para><b>Culled on the pawn's layer, not the load's</b>, because the load has no cell —
        /// that is the whole of what carrying means to the item store — and a colonist and what
        /// she is holding must appear and disappear together.</para>
        /// </summary>
        void RenderCarriedLoads(WorldSnapshot snapshot, ICarriedLoads? carried,
            int lowest, int highest, float tickAlpha, int movePerTick)
        {
            // **Driven off the aspect rows, not off the pawns, and that is the performance
            // decision.** Carrying is sparse: most colonists are holding nothing most of the time,
            // so there are a handful of these rows against a thousand aspects on a colony of
            // fifty. Walking the pawns instead would mean an aspect scan per pawn — fifty scans of
            // a thousand rows every frame to find three loads. This is one scan, and then a
            // per-carrier lookup that only carriers pay for. It is also exactly what
            // TryGetPawnAspect's own remarks recommend for a reader that wants one key across
            // every pawn.
            var rows = snapshot.PawnAspects;

            for (int r = 0; r < rows.Length; r++)
            {
                if (rows[r].Key != CarryAspects.Carrying) continue;

                int def = rows[r].Value;
                if (def < 0) continue;

                PawnId pawnId = rows[r].Pawn;
                if (!snapshot.TryGetPawn(pawnId, out PawnView pawn)) continue;

                // Culled on the pawn's layer because the load has no cell of its own — that is
                // the whole of what carrying means to the item store — and a colonist and what
                // she is holding must come into view and leave it together.
                if (pawn.Cell.Y < lowest || pawn.Cell.Y > highest) continue;

                if (!snapshot.TryGetPawnAspect(pawnId, CarryAspects.Stack, out int stack))
                    stack = 1;

                Vector3 at;
                float yaw;
                if (carried != null && carried.HasFigureFor(pawnId.Value))
                {
                    // A live figure answers for itself, or answers no — empty-handed on its own
                    // reckoning, or holding something the swim placeholder is hiding. Either way
                    // the renderer must not second-guess it with a stand-in load, or a wading
                    // colonist's bundle would reappear at her waist the frame the pose hid it.
                    if (!carried.TryGetCarried(pawnId.Value, out _, out _, out at, out yaw))
                        continue;
                }
                else
                {
                    Vector3 body = PawnPose.Of(pawn, tickAlpha, movePerTick,
                        out Vector3 heading, _model, snapshot.Pawns, Crowd);
                    if (heading.sqrMagnitude < 1e-6f) heading = Vector3.forward;
                    yaw = FacingOf(pawnId, heading);
                    at = body
                        + Vector3.up * (CellMetrics.SizeY * StandInCarryHeight)
                        + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * StandInCarryReach;
                }

                ResolvedModule? module = ItemModule(def);
                if (module == null || module.IsEmpty || !module.UsesArt) continue;

                // The load's own identity, not the pawn's: a heap picked up keeps the bearings and
                // sizes it had on the ground, so the rocks in her arms are the rocks that were in
                // the pile. The stack stands in for the item id, which is not published — two
                // colonists carrying the same commodity will hold it the same way round, which is
                // a great deal less noticeable than a heap reshuffling itself as it is lifted.
                uint seed = unchecked((uint)(def * 2654435761u + (uint)stack));

                // **The load is turned the way the colonist is** (owner, 2026-09-19: "when you
                // turn a direction the logs don't turn with you and they should"). It reads as
                // obvious and the first version did not do it: the yaw came from FacingOf, which
                // is the renderer's memory of the last heading it drew a *stand-in* at, and the
                // colonist loop skips anyone who has a live figure. So every load on every real
                // colonist was drawn at a yaw of exactly nought and a log pointed north for ever.
                Quaternion turned = Quaternion.Euler(0f, yaw, 0f);

                // Not every heap is carried as one: wood scatters on the floor and is carried as
                // the single bundle it was before, because the prop is a bound log pile and three
                // of those in two hands is not a load. See Recipe.CarriedAsHeap.
                if (ItemHeap.TryRecipe(def, out ItemHeap.Recipe heap) && heap.CarriedAsHeap)
                {
                    // The armful turns as one thing — about the cradle, not each rock about
                    // itself. ItemHeap lays its sunflower out on the world axes, so spinning the
                    // rocks in place would keep the cluster's shape while the shape went on
                    // pointing north: the same fault one layer down.
                    int rocks = ItemHeap.Armful(seed, at, turned, heap, _heapPlacements);
                    for (int rock = 0; rock < rocks; rock++) AppendItem(def, _heapPlacements[rock]);
                    continue;
                }

                // Never lifted by GroundRelief, unlike everything in RenderThings: the relief is a
                // correction for a thing lying on ground that is a shallow field rather than a
                // plane, and this thing is not on the ground. Lifting it would raise it off the
                // hands by however much the terrain happened to be doing underfoot.
                AppendItem(def, Matrix4x4.TRS(at, turned, Vector3.one));
            }
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

        /// <summary>
        /// Whether this kind of item resolved to real art, or draws as the stand-in marker.
        ///
        /// <para><b>Whether the art resolved, not whether there is a catalogue.</b> The catalogue
        /// is committed and its prefab references point into the gitignored <c>Assets/Synty</c>,
        /// so on the self-hosted runner it loads perfectly with every reference null — and every
        /// item then takes the marker path above, which costs a draw call and <i>no instance</i>.
        /// A measurement that counts instances measures nothing there and has to say so rather
        /// than fail. This is the item-side pair of <c>PawnFigureDirector.Enabled</c> and
        /// <c>PortraitStudio.Available</c>; see <c>CLAUDE.md</c>, "the runner has no
        /// Assets/Synty".</para>
        /// </summary>
        public bool ItemArtResolved(int defIndex)
        {
            EnsureItemModules();
            ResolvedModule? module = ItemModule(defIndex);
            return module != null && !module.IsEmpty && module.UsesArt;
        }

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
        ColonistAttachments? _attachments;
        Matrix4x4[][] _hairPlacements = System.Array.Empty<Matrix4x4[]>();
        int[] _hairCounts = System.Array.Empty<int>();
        Matrix4x4[][] _beardPlacements = System.Array.Empty<Matrix4x4[]>();
        int[] _beardCounts = System.Array.Empty<int>();

        /// <summary>
        /// The hair and beards a far colonist wears
        /// (<c>docs/design/29-modular-colonists.md</c>, MC6).
        ///
        /// <para><b>The same object the figures and the portraits hold</b>, so a colonist does not
        /// change hair on crossing the figure cap — which is the fault <c>ColonistLook</c>'s header
        /// warns about, arriving through a different door.</para>
        /// </summary>
        ColonistAttachments Attachments =>
            _attachments ??= new ColonistAttachments(_model.Library.Catalogue);

        /// <summary>
        /// Hair and beards are drawn as <b>two more instanced buckets</b>, never baked into the
        /// body mesh.
        ///
        /// <para>Baking them in would multiply the mesh variants by hair × beard and turn a
        /// bounded set of a few dozen into one bounded only by colony size. They are rigid props
        /// on a bone and the baked pose is fixed, so the head transform is a constant per body and
        /// a piece is one mesh instanced across everyone wearing it: the bucket key is the piece,
        /// not the person (<c>docs/research/e-06-modular-colonists.md</c> §8).</para>
        /// </summary>
        void EnsureAttachmentBuckets()
        {
            if (_hairCounts.Length != 0) return;

            _hairCounts = new int[Mathf.Max(1, Attachments.HairCount)];
            _hairPlacements = new Matrix4x4[_hairCounts.Length][];
            for (int i = 0; i < _hairCounts.Length; i++) _hairPlacements[i] = new Matrix4x4[16];

            _beardCounts = new int[Mathf.Max(1, Attachments.BeardCount)];
            _beardPlacements = new Matrix4x4[_beardCounts.Length][];
            for (int i = 0; i < _beardCounts.Length; i++) _beardPlacements[i] = new Matrix4x4[16];
        }

        static void AppendPiece(int index, in Matrix4x4 placement,
            Matrix4x4[][] into, int[] counts)
        {
            if ((uint)index >= (uint)counts.Length) return;

            Matrix4x4[] bucket = into[index];
            if (counts[index] == bucket.Length)
            {
                System.Array.Resize(ref bucket, bucket.Length * 2);
                into[index] = bucket;
            }
            bucket[counts[index]++] = placement;
        }

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
        /// <summary>
        /// One hair piece or beard, instanced across everyone wearing it.
        ///
        /// <para>Drawn through <see cref="MaterialCache"/> with the pack's own colours and no tint,
        /// which is <b>exactly what the baked body beside it gets</b>: the far form is not
        /// recoloured per colonist, and a head that was would be the one part of a distant figure
        /// that varied.</para>
        /// </summary>
        void SubmitPiece(in ColonistAttachments.Piece piece, Matrix4x4[] placements, int count)
        {
            if (count == 0 || piece.Mesh == null) return;

            Material material = _materials.Get(
                piece.Material, Color.white, Color.black, ghost: false, alpha: 1f);

            var rp = new RenderParams(material)
            {
                layer = GameObjectLayer,
                // Hair sitting flush on a scalp that already casts one. See ColonistAttachments.
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
            };

            int sent = 0;
            while (sent < count)
            {
                int n = Mathf.Min(MaxInstancesPerCall, count - sent);
                if (SubmitToGpu)
                    Graphics.RenderMeshInstanced(rp, piece.Mesh, 0, placements, n, sent);
                DrawCalls++;
                sent += n;
            }
        }

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
        /// Bias above the floor surface in metres, so the bracket does not z-fight with the ground mesh.
        /// </summary>
        public const float FloorBracketBias = 0.008f;

        /// <summary>
        /// The eight bars of a floor bracket in the placement's own space: two along the horizontal
        /// axes at each of the four corners of a cell.
        ///
        /// <para>Separated from the draw so the geometry can be asserted rather than looked at.</para>
        /// </summary>
        public static int FloorBracketEdges(Matrix4x4 place, Matrix4x4[] into, float[]? cornerRises = null)
        {
            float half = CellMetrics.HalfXZ;
            float length = Mathf.Max(CellMetrics.SizeXZ * BracketStub, BracketThickness);
            int n = 0;

            for (int corner = 0; corner < 4; corner++)
            {
                float sx = (corner & 1) == 0 ? -1f : 1f;
                float sz = (corner & 2) == 0 ? -1f : 1f;
                float rise = cornerRises != null && corner < cornerRises.Length ? cornerRises[corner] : 0f;
                float y = BracketThickness * 0.5f + FloorBracketBias + rise;

                // Along X: extends inward from sx * half towards 0
                var offsetX = new Vector3(
                    sx * (half - length * 0.5f),
                    y,
                    sz * (half - BracketThickness * 0.5f));
                var scaleX = new Vector3(length, BracketThickness, BracketThickness);
                into[n++] = place * ScaledAt(offsetX, scaleX);

                // Along Z: extends inward from sz * half towards 0
                var offsetZ = new Vector3(
                    sx * (half - BracketThickness * 0.5f),
                    y,
                    sz * (half - length * 0.5f));
                var scaleZ = new Vector3(BracketThickness, BracketThickness, length);
                into[n++] = place * ScaledAt(offsetZ, scaleZ);
            }

            return n;
        }

        /// <summary>
        /// The cursor for a cell with nothing in it: four corners on the floor, two stubs each.
        ///
        /// <para>Draped rather than lifted, so the ring lies along the same tangent plane the ground
        /// or floor slab does and the stubs stay flush above the surface without cutting into it on
        /// a slope.</para>
        /// </summary>
        public void DrawFloorBracket(CellRef cell, Color colour) =>
            DrawFloorBracket(GroundRelief.Drape(CellMetrics.FloorCentre(cell)), colour);

        /// <summary>
        /// The cursor placed at an explicit surface transform, for water and banks.
        /// </summary>
        public void DrawFloorBracket(Matrix4x4 placement, Color colour, float[]? cornerRises = null)
        {
            Material material = BracketMaterial(colour);
            var rp = new RenderParams(material)
            {
                layer = GameObjectLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };

            int n = FloorBracketEdges(placement, _floorMatrices, cornerRises);

            if (SubmitToGpu)
                Graphics.RenderMeshInstanced(rp, PrimitiveMeshes.UnitCube, 0, _floorMatrices, n);
            DrawCalls++;
            InstancesDrawn += n;
        }

        /// <summary>The bracket cursor around one whole cell.</summary>
        public void DrawCellHighlight(CellRef cell, Color colour) =>
            DrawSelectionBracket(
                GroundRelief.Drape(CellMetrics.Centre(cell.X, cell.Z, cell.Y)),
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
        /// <para>On top of whatever is in the cell, because that is the face you see it from: a
        /// mine order is read looking down at the stone, a deconstruct order on the top of the
        /// wall or the bed it is taking apart, and a fell order on the ground the tree stands in.
        /// <see cref="WorldRenderModel.MarkHeight"/> is the one place that decides which — it used
        /// to be a solid-terrain test here, which is why deconstruct needed a shape of its own
        /// before it could be seen at all. Inset from the cell edges so a row of marked cells
        /// reads as a row rather than as one continuous sheet, and flat, so it never competes with
        /// the thing it is marking.</para>
        /// </summary>
        public void DrawCellMark(CellRef cell, Color colour) =>
            DrawCellMark(cell, colour, inset: 0.22f);

        /// <summary>
        /// The same floor plate with the inset the caller names. An order's mark sits inset so it
        /// reads as a mark ON the tile; a growing zone's cover wants the whole tile (owner,
        /// 2026-09-18: "make the entire tile brown so they can look like one patch") - inset
        /// plates drew a border of ground between them and a field read as separate squares
        /// rather than one patch of soil.
        /// </summary>
        /// <summary>
        /// A growing zone's whole-tile cover: <b>the ground's own module, drawn again over
        /// itself and tinted</b>, lifted a mark's height along the drape.
        ///
        /// <para><b>Why the terrain mesh and not a plate.</b> The terrain quad is draped -
        /// sheared onto the relief field's tangent plane - and rippled inside its own cell,
        /// and the first cover was a flat plate at the cell centre's height: it sank into the
        /// ripple's convex corners and floated over the concave ones, so every tile showed
        /// gaps, thick borders or missing parts depending on the bearing it was seen from
        /// (owner, 2026-09-19, with the screenshots that prove it). Drawing the same mesh with
        /// the same drape is flush by construction - identical geometry, one constant offset -
        /// and uniform from every angle because there is nothing left to disagree with.</para>
        ///
        /// <para>Draped on the <i>ground</i> cell's floor so the module's top face lands where
        /// the terrain's top face is: the cell handed in is the zone's air cell, and the
        /// terrain that shows through it is the cell below. The sides of the ground box are
        /// tinted with it, which is right where a plot meets a terrace edge - the soil column
        /// is the plot - and buried everywhere else.</para>
        /// </summary>
        public void DrawCellMark(CellRef cell, Color colour, float inset)
        {
            int index = _model.Index(cell.X, cell.Z, cell.Y);

            Vector3 centre = GroundRelief.Lift(CellMetrics.FloorCentre(cell));
            centre.y += _model.MarkHeight(index) + MarkLift;

            var size = new Vector3(
                CellMetrics.SizeXZ - inset * 2f, MarkThickness, CellMetrics.SizeXZ - inset * 2f);

            // Gathered, not submitted - see FlushCellPlates. This was one Graphics.RenderMesh per
            // designated cell, and a player who marks a wood designates hundreds of them.
            GatherCellPlate(colour, Matrix4x4.TRS(centre, Quaternion.identity, size));
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

        /// <summary>
        /// How far worked soil is graded below the earth it is, per channel. See
        /// <see cref="TintCode.TilledBase"/> for why it is a grade on the ground's own bucket
        /// rather than a second mesh laid over it.
        ///
        /// <para><b>Derived from the cover it replaces, so the field keeps the colour that was
        /// already agreed</b> (owner, 2026-09-18: "make the entire tile brown so they can look
        /// like one patch and make it a darker brown"). The cover composited as
        /// <c>0.78 x (0.06, 0.032, 0.012) + 0.22 x ground</c> - a scale plus a warm pedestal -
        /// and against a representative lit earth of about (0.55, 0.45, 0.38) that lands on
        /// (0.168, 0.124, 0.093). These are the per-channel factors that reach the same place by
        /// multiply alone. Uniform 0.25 was tried first and read grey: the pedestal was carrying
        /// the warmth, and dropping it took the brown out of the brown.</para>
        /// </summary>
        public static readonly Color TilledGrade = new Color(0.305f, 0.276f, 0.245f);

        /// <summary>
        /// The colour a store's ground is washed towards — <c>OrderColours.StoreHue</c>, the same
        /// blue-grey the chip, the drag cursor and the armed banner wear.
        ///
        /// <para><b>The order's colour and the result's are the same one here, and that is not an
        /// oversight.</b> A growing zone is deliberately the other way round: its chip is the
        /// Zones brown and its committed ground is <see cref="TilledGrade"/>, because a painted
        /// field turns into worked soil and the soil is the result rather than the order — exactly
        /// as a built wall is not the blue of its blueprint. A store has no result: the ground
        /// under a warehouse is the ground it always was, and the wash <i>is</i> the standing
        /// order, still in force, for as long as the zone exists.</para>
        ///
        /// <para>Written as the same three bytes <c>OrderColours.StoreHue</c> carries rather than
        /// reached for across the assembly boundary: <c>Odyssey.Hud</c> has no Unity types and
        /// this file has nothing else. <c>OrderColoursTests</c> holds the pair together.</para>
        /// </summary>
        public static readonly Color StoredGrade = new Color(0x7f / 255f, 0x96 / 255f, 0xa8 / 255f);

        /// <summary>
        /// How far a store's ground is pulled towards <see cref="StoredGrade"/>. A third: enough
        /// that the edge of a zone is unmistakable at the play camera, little enough that a stone
        /// floor still reads as stone and a wooden one as wood.
        /// </summary>
        public const float StoredWash = 0.33f;

        /// <summary>
        /// The seed specks on a sown zone cell (owner, 2026-09-18: "speckled white tiny dots to
        /// indicate it's sown"). Six tiny flecks at deterministic positions hashed from the cell,
        /// so they sit still frame to frame and every cell scatters differently; lifted just
        /// clear of the ground the way a mark is, and cast no shadows — a shadow the size of the
        /// fleck itself would double it.
        /// </summary>
        /// <summary>
        /// Gather one cell's handful of seed specks. Nothing is submitted here - see
        /// <see cref="FlushSeedSpecks"/>, which draws the whole frame's worth in one call.
        /// </summary>
        public void DrawSeedSpecks(CellRef cell, Color colour, float sinceDrop = -1f)
        {
            int index = _model.Index(cell.X, cell.Z, cell.Y);
            Vector3 centre = GroundRelief.Lift(CellMetrics.FloorCentre(cell));
            centre.y += MarkLift;

            // The drop, when the caller has one running: a handful held at the sower's hand
            // height for a beat, then each seed falling to its own spot, staggered so they
            // scatter rather than move as one board (owner, 2026-09-19: "an animation that
            // starts from a bundle of seeds from a hand and then the seed fall onto their
            // destinations"). The fall accelerates - a seed is dropped, not lowered - and once
            // every seed is down the call degenerates to the static handful the sown cell
            // draws until it sprouts, which is why the landed positions are the same hashed
            // spots both ways: the handoff from dropping to lying there is invisible by
            // construction, not by luck.
            const int Specks = 6;
            const float Size = 0.045f;
            const float HandHeight = 0.5f;
            const float BundleSeconds = 0.25f;
            const float FallSeconds = 0.35f;
            const float FallStagger = 0.04f;
            bool dropping = sinceDrop >= 0f &&
                sinceDrop < BundleSeconds + FallSeconds + FallStagger * (Specks - 1);
            Vector3 hand = centre + Vector3.up * HandHeight;

            for (int i = 0; i < Specks; i++)
            {
                // A cheap per-cell-per-speck hash: the cell's own index twisted by the speck's,
                // mapped to the cell's inner square. Deterministic, board-stable, and no two
                // neighbouring cells repeat their handful.
                uint h = (uint)(index * 747_796_405u + i * 289_133_645_3u);
                h = (h ^ (h >> 13)) * 1_274_126_177u;
                float ox = ((h & 0xFFFF) / 65535f - 0.5f) * (CellMetrics.SizeXZ - 0.7f);
                float oz = (((h >> 16) & 0xFFFF) / 65535f - 0.5f) * (CellMetrics.SizeXZ - 0.7f);

                Vector3 at = centre + new Vector3(ox, 0f, oz);
                if (dropping)
                {
                    float into = sinceDrop - BundleSeconds - i * FallStagger;
                    if (into < 0f) at = hand;
                    else
                    {
                        float t = Mathf.Clamp01(into / FallSeconds);
                        at = Vector3.Lerp(hand, at, t * t);
                    }
                }
                if (_speckCount == _speckMatrices.Length)
                    System.Array.Resize(ref _speckMatrices, _speckMatrices.Length * 2);
                _speckMatrices[_speckCount++] =
                    Matrix4x4.TRS(at, Quaternion.identity, new Vector3(Size, Size * 0.5f, Size));
            }
        }

        /// <summary>
        /// Draw every seed speck gathered this frame, instanced.
        ///
        /// <para><b>Why they are gathered at all.</b> Each sown cell wears six specks and each
        /// used to be its own <c>Graphics.RenderMesh</c> - six calls and a <c>RenderParams</c>
        /// per cell, of the same unit cube in the same material. The surround taught the price
        /// of that: a submission costs about 4.6 us whatever is in it, so a field part-way
        /// through sowing was paying milliseconds to draw a few hundred cubes. They share one
        /// mesh and one material by construction - the colour is a single constant - so they are
        /// one instanced call, or a handful once past <see cref="MaxInstancesPerCall"/>.</para>
        /// </summary>
        public void FlushSeedSpecks(Color colour)
        {
            if (_speckCount == 0) return;

            var rp = new RenderParams(BracketMaterial(colour))
            {
                layer = GameObjectLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };

            int drawn = 0;
            while (drawn < _speckCount)
            {
                int n = Mathf.Min(MaxInstancesPerCall, _speckCount - drawn);
                Graphics.RenderMeshInstanced(rp, PrimitiveMeshes.UnitCube, 0, _speckMatrices, n, drawn);
                drawn += n;
                DrawCalls++;
                InstancesDrawn += n;
            }
            _speckCount = 0;
        }

        Matrix4x4[] _speckMatrices = new Matrix4x4[512];
        int _speckCount;


        /// <summary>
        /// One colour's worth of cell plates gathered this frame, and the matrices to draw them
        /// with. Reused frame to frame: the count is reset by the flush, the array is not.
        /// </summary>
        sealed class PlateBucket
        {
            public Color Colour;
            public Matrix4x4[] Matrices = new Matrix4x4[256];
            public int Count;
        }

        PlateBucket?[] _plateBuckets = System.Array.Empty<PlateBucket?>();
        int _plateBucketCount;

        /// <summary>Cell plates gathered this frame and not yet flushed. Read by the tests, so a
        /// pass that grows with the board can be asserted about rather than looked at.</summary>
        public int PlatesGathered { get; private set; }

        /// <summary>
        /// Off, <see cref="FlushCellPlates"/> submits each gathered plate on its own instead of
        /// instancing the bucket. <b>This exists to be measured and for nothing else.</b>
        ///
        /// <para>It is the control that prices the batching, and it is a control rather than a
        /// second code path: the geometry still has exactly one owner in
        /// <see cref="GatherCellPlate"/> and only the submission changes, so the two readings
        /// differ in the thing being priced and in nothing else — which is what a control has to
        /// do (<c>docs/lessons.md</c>, "a probe that disables the thing you are pricing will lie
        /// to you"). Without it the before and after have to come from two Unity runs minutes
        /// apart, and on a machine that runs several editors at once that is not a measurement:
        /// the same batched pass read 0.81 ms and 0.19 ms on two runs of the same code on
        /// 2026-09-20, purely on how busy the machine was.</para>
        /// </summary>
        public bool InstanceCellPlates { get; set; } = true;

        /// <summary>
        /// Hold one cell plate until the end of the frame, bucketed by its colour.
        ///
        /// <para>Colour is the bucket key because it is the whole of what varies: every plate is
        /// the same unit cube in the same fallback material, and <see cref="BracketMaterial"/>
        /// already caches one material per colour. Four order colours, a cut and a fill is the
        /// entire range a frame ever sees, so the linear scan below is over a handful of entries
        /// and allocates nothing after the first frame.</para>
        /// </summary>
        void GatherCellPlate(Color colour, in Matrix4x4 place)
        {
            PlateBucket? bucket = null;
            for (int i = 0; i < _plateBucketCount; i++)
                if (_plateBuckets[i]!.Colour == colour) { bucket = _plateBuckets[i]; break; }

            if (bucket == null)
            {
                if (_plateBucketCount == _plateBuckets.Length)
                    System.Array.Resize(ref _plateBuckets,
                        _plateBuckets.Length == 0 ? 8 : _plateBuckets.Length * 2);
                bucket = _plateBuckets[_plateBucketCount] ??= new PlateBucket();
                bucket.Colour = colour;
                bucket.Count = 0;
                _plateBucketCount++;
            }

            if (bucket.Count == bucket.Matrices.Length)
                System.Array.Resize(ref bucket.Matrices, bucket.Matrices.Length * 2);
            bucket.Matrices[bucket.Count++] = place;
            PlatesGathered++;
        }

        /// <summary>
        /// Draw every cell plate gathered this frame: one instanced call per colour, or a handful
        /// once a colour passes <see cref="MaxInstancesPerCall"/>.
        ///
        /// <para><b>Why they are gathered at all.</b> These were one submission per designated
        /// cell - priced by how much of the board the player had marked rather than by how much
        /// there is to see, and invisible to the budget because neither the mark nor the slab
        /// incremented <see cref="DrawCalls"/>. That is P10 in <c>docs/bug-patterns.md</c>, the
        /// same fault the zone cover and the seed specks each wore.</para>
        ///
        /// <para><b>The composition root must call this after the last thing that marks a cell.</b>
        /// Gathered plates that are never flushed are not drawn, and <c>CellPlateTests</c> is the
        /// guard that a marked board costs draws in colours rather than in cells.</para>
        /// </summary>
        public void FlushCellPlates()
        {
            for (int i = 0; i < _plateBucketCount; i++)
            {
                PlateBucket bucket = _plateBuckets[i]!;
                if (bucket.Count == 0) continue;

                var rp = new RenderParams(BracketMaterial(bucket.Colour))
                {
                    layer = GameObjectLayer,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                };

                if (InstanceCellPlates)
                {
                    int drawn = 0;
                    while (drawn < bucket.Count)
                    {
                        int n = Mathf.Min(MaxInstancesPerCall, bucket.Count - drawn);
                        if (SubmitToGpu)
                            Graphics.RenderMeshInstanced(
                                rp, PrimitiveMeshes.UnitCube, 0, bucket.Matrices, n, drawn);
                        drawn += n;
                        DrawCalls++;
                        InstancesDrawn += n;
                    }
                }
                else
                {
                    for (int at = 0; at < bucket.Count; at++)
                    {
                        if (SubmitToGpu)
                            Graphics.RenderMesh(in rp, PrimitiveMeshes.UnitCube, 0,
                                bucket.Matrices[at]);
                        DrawCalls++;
                        InstancesDrawn++;
                    }
                }

                CellPlatesDrawn += bucket.Count;
                bucket.Count = 0;
            }

            _plateBucketCount = 0;
            PlatesGathered = 0;
        }

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

            // Inset a little so the slab sits inside the cell rather than z-fighting the faces of
            // the rock it is drawn over, and of whatever stands beside it.
            const float Inset = 0.06f;
            float height = CellMetrics.SizeY * fraction;
            var size = new Vector3(
                CellMetrics.SizeXZ - Inset * 2f, height, CellMetrics.SizeXZ - Inset * 2f);

            Vector3 centre = CellMetrics.Centre(cell.X, cell.Z, cell.Y);
            float offset = (CellMetrics.SizeY - height) * 0.5f;
            centre.y += fromTheFloor ? -offset : offset;

            // Gathered with the marks: the same unit cube in the same material, and a colony
            // part-way through a large build or a large quarry has one of these per cell too.
            GatherCellPlate(colour, Matrix4x4.TRS(centre, Quaternion.identity, size));
        }

        public void DrawSelectionBracket(Vector3 centre, Vector3 size, Color colour) =>
            DrawSelectionBracket(Matrix4x4.Translate(centre), size, colour);

        public void DrawSelectionBracket(Matrix4x4 place, Vector3 size, Color colour)
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
                    sign.x * half.x,
                    sign.y * half.y,
                    sign.z * half.z);

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

                    _bracketMatrices[n++] = place * ScaledAt(position, scale);
                }
            }

            if (SubmitToGpu)
                Graphics.RenderMeshInstanced(rp, PrimitiveMeshes.UnitCube, 0, _bracketMatrices, n);
            DrawCalls++;
            InstancesDrawn += n;
        }

        /// <summary>
        /// A straight bar from one point to another in the bracket's lit, translucent material — the
        /// line from a drafted colonist to where it has been sent (design 33 §2g). One submission.
        /// </summary>
        public void DrawSegment(Vector3 from, Vector3 to, float thickness, Color colour)
        {
            Vector3 along = to - from;
            float length = along.magnitude;
            if (length < 0.01f) return;

            var rp = new RenderParams(BracketMaterial(colour))
            {
                layer = GameObjectLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Matrix4x4 bar = Matrix4x4.TRS((from + to) * 0.5f, Quaternion.LookRotation(along / length),
                new Vector3(thickness, thickness, length));
            if (SubmitToGpu) Graphics.RenderMesh(rp, PrimitiveMeshes.UnitCube, 0, bar);
            DrawCalls++;
            InstancesDrawn++;
        }

        /// <summary>
        /// A small cube stood on its corner — a diamond — in the bracket's material: the drafted
        /// marker over a colonist's head (design 33 §2g). One submission.
        /// </summary>
        public void DrawMarker(Vector3 centre, float size, Color colour)
        {
            var rp = new RenderParams(BracketMaterial(colour))
            {
                layer = GameObjectLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Matrix4x4 diamond = Matrix4x4.TRS(centre, MarkerTurn, Vector3.one * size);
            if (SubmitToGpu) Graphics.RenderMesh(rp, PrimitiveMeshes.UnitCube, 0, diamond);
            DrawCalls++;
            InstancesDrawn++;
        }

        /// <summary>
        /// A flat ring lying on the ground in the bracket's lit, translucent material — the
        /// lock-on ring under an attack order's target (design 33 §7b). <paramref name="placement"/>
        /// carries the drape, the lift and the radius in x and z; the mesh is
        /// <see cref="PrimitiveMeshes.UnitRing"/>. One submission.
        /// </summary>
        public void DrawRing(Matrix4x4 placement, Color colour)
        {
            var rp = new RenderParams(BracketMaterial(colour))
            {
                layer = GameObjectLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            if (SubmitToGpu) Graphics.RenderMesh(rp, PrimitiveMeshes.UnitRing, 0, placement);
            DrawCalls++;
            InstancesDrawn++;
        }

        /// <summary>Turned 45 degrees about the vertical, then tipped on to a corner.</summary>
        static readonly Quaternion MarkerTurn = Quaternion.Euler(0f, 45f, 0f) * Quaternion.Euler(35.264f, 0f, 45f);

        public void Dispose()
        {
            Skirt.Dispose();
            _materials.Dispose();
        }
    }
}
