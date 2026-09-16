#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using Odyssey.Hud;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Odyssey.Presentation.Bootstrap
{
    /// <summary>
    /// The scenarios the inspector can pick from. Until scenario Defs load from a content pack
    /// this is the whole list, one entry per factory on <see cref="ScenarioDef"/>; the scene
    /// chooses a scenario by name here and holds none of its numbers.
    /// </summary>
    public enum StartingScenario
    {
        Playtest,
        Bare,
    }

    /// <summary>
    /// The composition root for a playable scene: generate a world, register its systems, tick it
    /// at a fixed rate, and hand the renderer the published frame.
    ///
    /// The division of labour is the one the architecture depends on. This class owns the
    /// <see cref="SimWorld"/> because someone has to build it; everything under
    /// <c>Presentation.Rendering</c> reads the mirror and the snapshot and has no access to a
    /// simulation object at all. Player input becomes intents on a queue, drained at the start of
    /// the next tick; nothing here reaches in and mutates the world.
    ///
    /// The tick is fixed-rate and is never scaled by frame time. Game speed is a tick-rate
    /// multiplier, not a delta multiplier — a determinism requirement, not a stylistic one, since
    /// the simulation contains no floating-point time at all.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OdysseyBootstrap : MonoBehaviour
    {
        [Header("World")]
        // A 60-cell board read as cramped once the camera pulled back far enough to see it all:
        // there was nowhere to walk to. 120 gives room to spread out, and 16 layers leave depth to
        // mine and headroom to build in. Well inside the 250-cell scale target the renderer is
        // built for, so this costs nothing that has not already been measured.
        public int sizeX = 120;
        public int sizeZ = 120;
        public int layers = 16;
        public uint seed = 1;

        [Tooltip("Natural wilderness is the prototype default (ADR 0008). RuinedCity is kept and still works.")]
        public MapType mapType = MapType.Natural;

        [Tooltip("Flat grass everywhere, no rock or ore. The plain board to build from.")]
        public bool barrenMap = true;

        [Tooltip("With barrenMap: keep the woodland, so there are trees to fell. Off gives the bare board.")]
        public bool woodedMap = true;

        // Playtest marks the trees near the start for felling before the first tick, because
        // there is no tool to give that order with yet and a colony with nothing to do proves
        // nothing. When the UI line's designate tool lands, the default here flips to Bare and
        // the first order is the player's.
        [Tooltip("What the colony starts with. Playtest gives it felling work near the start at once; Bare gives the same colony and no orders. The default flips to Bare when the designate tool lands.")]
        public StartingScenario scenario = StartingScenario.Playtest;

        [Tooltip("Carry the land on past the rim of the board, so it does not end in mid-air. Decoration only: nothing out there is a cell.")]
        public bool terrainSkirt = true;

        [Tooltip("How much of the board's own tree density the surround gets. 100 continues the wood; lower is the lever for a machine that cannot afford it.")]
        [Range(0, 100)]
        public int skirtTreeDensity = 100;

        [Tooltip("Which faces the colonists get. 0 draws a fresh cast every session; any other value pins one, and the log prints the value each session used so a cast you liked can be kept.")]
        public int colonistLookSeed = 0;

        [Header("Presentation")]
        public ModuleCatalogue? moduleCatalogue;
        public SliceCameraRig? cameraRig;
        public bool castShadows = true;

        [Tooltip("Tufts of grass per hundred grass cells. 0 is bare ground; 60 is a tuft on six cells in ten.")]
        [Range(0, 300)]
        public int grassScatter = 60;

        [Tooltip("How far the drawn ground rolls above and below its layer, in metres. Decoration only: the cells stay flat, so nothing here changes pathing, the save or the hash. 0 is the flat board.")]
        [Range(0f, 3f)]
        public float groundRelief = GroundRelief.BoardAmplitude;

        [Tooltip("The wavelength of the longest swell, in metres. Shorter is steeper and reads more strongly, at the cost of faceting between cells.")]
        [Range(40f, 400f)]
        public float groundReliefPeriod = 150f;

        [Header("Tick")]
        [Tooltip("Ticks per second at speed 1. The simulation has no notion of seconds; this is it.")]
        public int ticksPerSecond = 60;

        [Tooltip("Ceiling on catch-up ticks in one frame, so a stall cannot spiral.")]
        public int maxTicksPerFrame = 8;


        SimWorld? _world;
        CellGrid? _grid;
        WorldRenderModel? _model;
        ChunkRenderer? _renderer;
        PawnContext? _pawns;
        PawnFigureDirector? _figures;
        Material? _actorMaterial;
        MapGenDef? _gen;
        double _accumulator;
        float _tickAlpha;

        /// <summary>
        /// How far this frame sits between two ticks, 0 to 1.
        ///
        /// Exposed because the pick hit-test has to build its box from the same number the figure
        /// is drawn with. When it did not, the box sat at the tick boundary and the figure had
        /// moved on, so a walking colonist was not clickable where they appeared.
        /// </summary>
        public float TickAlpha => _tickAlpha;

        /// <summary>Cost units a pawn retires in one tick, the other half of that same tween.</summary>
        public int MovePerTick => PawnContent.Core().Movement.movePerTick;

        /// <summary>
        /// The live figures, for anything that must agree with where a colonist is actually drawn
        /// rather than with where the simulation keeps them. See <see cref="PawnFigureDirector.TryGetFeet"/>.
        /// </summary>
        public PawnFigureDirector? Figures => _figures;
        readonly Stopwatch _frameTimer = new Stopwatch();
        double _renderMs;
        double _tickMs;
        float _smoothedFrameMs;
        string _catalogueNote = string.Empty;

        public SimWorld? World => _world;

        /// <summary>
        /// The interface directors: selection, slice and camera, Unity-free and made here with the
        /// world, because the composition root is the one place that knows the layer count and
        /// the start layer. The rig, the pick presenter and the HUD shell all realise these.
        /// </summary>
        public HudDirectors? Directors { get; private set; }
        public WorldRenderModel? Model => _model;
        public ChunkRenderer? Renderer => _renderer;

        void Start()
        {
            // Set before anything is meshed, because the relief is read at mesh time and a chunk
            // built flat would stay flat until something dirtied it. Statics, like the scatter
            // density beside them: the field has to be reachable from the mesher, the picker and
            // the figures alike, and it is a property of how the world is drawn rather than of any
            // one of them.
            GroundRelief.Amplitude = groundRelief;
            GroundRelief.Period = groundReliefPeriod;

            var size = new GridSize(sizeX, sizeZ, layers);
            _grid = new CellGrid(size);
            var chunks = new ChunkGrid(size);

            // The prototype starts on empty natural ground and the colony builds from nothing
            // (ADR 0008). The ruined-city generator is still here and still tested; switch
            // mapType to reach it.
            _gen = MapGenerator.DefaultDef(mapType, size);
            if (barrenMap && _gen is NaturalMapGenDef natural)
            {
                if (woodedMap) natural.MakeWooded();
                else natural.MakeBarren();
            }

            var generation = Stopwatch.StartNew();
            MapGenOutcome outcome = MapGenerator.Generate(_grid, seed, _gen);
            generation.Stop();

            var library = new ModuleLibrary(moduleCatalogue);
            _model = new WorldRenderModel(size, chunks, library);

            // Shell templates only exist on a city map; natural ground has no stamped buildings.
            if (outcome.City != null) _model.ApplyTemplates(outcome.City, _gen);

            var edifices = outcome.City != null
                ? outcome.City.Context.Edifices
                : outcome.Natural!.Context.Edifices;
            var mirror = new GridMirrorContributor(_grid, edifices, _model);
            var solver = new SupportSolver(_grid);
            CellGrid grid = _grid;

            // Navigation and the colonists that use it. The graph is built once here and then
            // maintained inside the tick by NavigationSystem, which runs after the support solver
            // because a collapse changes what is walkable.
            var nav = new NavGraph(_grid);
            nav.Rebuild();
            var pathService = new PathService(new PathFinder(nav));
            _pawns = new PawnContext(_grid, nav, pathService, PawnContent.Core()) { Chunks = chunks };

            var support = new SupportSystem(grid, solver, chunks);
            var designations = new DesignationGrid(_grid, edifices);

            // The mirror publishes first so the geometry a frame shows is the one its pawns and
            // orders were computed against; the colony itself is listed once, in ColonyComposition.
            _world = new SimWorldBuilder()
                .WithSeed(seed)
                .WithSize(size)
                .AddSnapshotContributor(mirror)
                .AddColony(_pawns, designations, support, nav)
                .Build();

            ScenarioDef scenarioDef = scenario == StartingScenario.Bare ? ScenarioDef.Bare() : ScenarioDef.Playtest();
            var placement = ColonyScenario.Place(_grid, _pawns, outcome.StartCell, seed, scenarioDef);
            if (placement.Colonists == 0)
                Debug.LogError($"[Odyssey] no colonists were placed near {outcome.StartCell}: {placement}");
            int marked = ColonyScenario.GiveStartingOrders(designations, outcome.StartCell, scenarioDef);
            if (marked > 0)
                Debug.Log($"[Odyssey] {scenarioDef}: {marked} trees within {scenarioDef.startingFellRadius} cells of the start are marked for felling");

            // One tick primes the mirror: the contributor runs in the publish phase, so until the
            // world has ticked once there is no published frame and nothing to draw.
            _world.Intents.Submit(new Intent(IntentKind.SetSliceLayer, default, outcome.StartCell.Y));
            _world.Tick();

            // The face lottery is a presentation choice and never enters the simulation, so it is
            // free to be genuinely random per session — which is the point: with a fixed hash the
            // starting five wore the same five faces every play and a cast of sixty-one read as a
            // cast of five. Logged so a cast worth keeping can be pinned by copying the number
            // into colonistLookSeed.
            uint lookSalt = colonistLookSeed != 0
                ? (uint)colonistLookSeed
                : (uint)UnityEngine.Random.Range(1, int.MaxValue);
            Debug.Log($"[Odyssey] colonist look seed {lookSalt} (set colonistLookSeed to keep this cast)");

            _renderer = new ChunkRenderer(_model)
            {
                CastShadows = castShadows,
                GameObjectLayer = gameObject.layer,
                ScatterDensity = grassScatter,
                ColonistLookSalt = lookSalt,
            };
            _renderer.Skirt.Enabled = terrainSkirt;
            _renderer.Skirt.TreeDensityPercent = skirtTreeDensity;
            if (terrainSkirt)
            {
                _renderer.Skirt.Build();
                Debug.Log($"[Odyssey] surround: {_renderer.Skirt.GroundInstances} ground tiles, " +
                          $"{_renderer.Skirt.TreeInstances} trees and {_renderer.Skirt.TuftInstances} tufts " +
                          $"beyond the rim, at the board's own " +
                          $"{_renderer.Skirt.MeasuredTreeDensity} trees per thousand cells");
            }
            _actorMaterial = new Material(library.FallbackMaterial) { name = "Odyssey/Actor" };
            // High-contrast against grass, earth and stone, which tan was not.
            _actorMaterial.SetColor("_BaseColor", new Color(0.98f, 0.36f, 0.20f));

            // Live figures for the pawns on screen. Everything else keeps the baked instanced
            // form, and so does everybody if the packs are absent or the catalogue has no gaits.
            _figures = new PawnFigureDirector(moduleCatalogue, transform, gameObject.layer)
            {
                LookSalt = lookSalt,
            };

            Directors = new HudDirectors(size.SizeY, outcome.StartCell.Y);
            Directors.Slice.LayerChanged += OnActiveLayerChanged;

            if (cameraRig != null)
            {
                // Bind to the layer the colony actually stands on, not the generator nominal
                // ground layer. The surface is terraced, so StartCell.Y sits one to three layers
                // above groundLayer, and RenderActors culls anything above the active layer -
                // which meant every colonist was culled every frame while the terrain drew fine.
                cameraRig.Bind(_model, _renderer, Directors);
                // The composition root draws every cursor tier; the rig's own cell cube is off from
                // the first frame, not from the first LateUpdate that happens to say so.
                cameraRig.SuppressCellCursor = true;                cameraRig.GameSpeedRequested += OnGameSpeedRequested;
                // Open on the colony, not on the whole map: see SliceCameraRig.FocusOn.
                cameraRig.FocusOn(outcome.StartCell);
            }

            _catalogueNote = moduleCatalogue == null
                ? "no catalogue asset: every module is a primitive"
                : $"catalogue {moduleCatalogue.name}: {moduleCatalogue.ResolvedPrefabCount()}/" +
                  $"{moduleCatalogue.Entries.Count} rows have art";

            Debug.Log(
                $"[Odyssey] world {size} seed {seed} generated in {generation.ElapsedMilliseconds} ms. " +
                $"{(outcome.Natural != null ? outcome.Natural.Report.ToString() : outcome.City!.Report.ToString())}. {_catalogueNote}. " +
                $"Modules with art: {library.ArtBackedCount()}/{library.Count - 1}" +
                (library.MissingArt.Count == 0
                    ? "."
                    : $"; falling back to primitives for: {string.Join(", ", library.MissingArt)}."));
        }

        void OnActiveLayerChanged(int layer) =>
            _world?.Intents.Submit(new Intent(IntentKind.SetSliceLayer, default, layer));

        void OnGameSpeedRequested(int speed)
        {
            if (_world == null) return;
            // Space toggles: asking for pause while already paused means "start again".
            int next = speed == 0 && _world.GameSpeed == 0 ? 1 : speed;
            _world.Intents.Submit(new Intent(IntentKind.SetGameSpeed, default, next));
            _speedChangePending = true;
        }

        bool _speedChangePending;

        void Update()
        {
            if (_world == null) return;

            int speed = _world.GameSpeed;
            _tickMs = 0d;
            if (speed > 0)
            {
                double interval = 1d / (ticksPerSecond * speed);
                _accumulator += Time.deltaTime;
                int budget = maxTicksPerFrame;
                _frameTimer.Restart();
                while (_accumulator >= interval && budget-- > 0)
                {
                    _world.Tick();
                    _accumulator -= interval;
                }
                _frameTimer.Stop();
                _tickMs = _frameTimer.Elapsed.TotalMilliseconds;
                _speedChangePending = false;
                if (budget <= 0) _accumulator = 0d; // give up rather than spiral

                // How far this frame sits between the last tick and the next. Presentation uses
                // it to carry a walking pawn on past the tick that last moved it, so motion stays
                // smooth when the frame rate runs ahead of the tick rate. RimWorld tweens the
                // same way; without it a 144 Hz display shows the same position for two frames
                // out of three and the walk micro-stutters.
                _tickAlpha = interval > 0d ? Mathf.Clamp01((float)(_accumulator / interval)) : 0f;
            }
            else
            {
                _accumulator = 0d;
                // A paused world never reaches a tick boundary, so it never drains its queue and
                // could never be un-paused. One tick is spent to let the speed change through, and
                // only for a speed change: layer changes are presentation state and can wait, so a
                // player scrolling through layers while paused does not advance the simulation.
                if (_speedChangePending)
                {
                    _world.Tick();
                    _speedChangePending = false;
                }
            }
        }

        void LateUpdate()
        {
            if (_renderer == null || _model == null || _world == null) return;
            int activeLayer = cameraRig != null ? cameraRig.ActiveLayer : _world.Views.SliceLayer;
            SliceSettings slice = cameraRig != null ? cameraRig.slice : new SliceSettings();

            _frameTimer.Restart();
            // The rig sits on the camera, so its position is the viewer's.
            if (cameraRig != null) _renderer.ViewerPosition = cameraRig.transform.position;
            _renderer.Render(activeLayer, slice);

            // Figures first, because what they take is what the instanced pass must leave alone.
            // Their graphs advance on their own clock once played, so nothing is evaluated here.
            int movePerTick = PawnContent.Core().Movement.movePerTick;
            _figures?.Sync(_world.Views.Current, activeLayer, slice, _tickAlpha, movePerTick,
                Time.deltaTime);

            if (_actorMaterial != null)
                _renderer.RenderActors(_world.Views.Current, activeLayer, slice, _actorMaterial,
                    _tickAlpha, movePerTick, _figures?.Drawn);

            DrawSelectionCursor(_world.Views.Current, movePerTick);
            _frameTimer.Stop();
            _renderMs = _frameTimer.Elapsed.TotalMilliseconds;

            float frameMs = Time.unscaledDeltaTime * 1000f;
            _smoothedFrameMs = _smoothedFrameMs <= 0f ? frameMs : Mathf.Lerp(_smoothedFrameMs, frameMs, 0.05f);
        }

        /// <summary>
        /// The size of the bracket drawn around a selected colonist, in metres.
        ///
        /// Fixed rather than measured off the figure, and that is deliberate. A colonist's own
        /// renderer bounds breathe with the walk cycle — the arms swing — so a cursor sized from
        /// them would pulse in and out every stride, which is worse than one that is a few
        /// centimetres off. The cast is all built to one scale, so one box fits all of them.
        /// </summary>
        [Tooltip("The bracket drawn around a selected colonist: width, height, depth in metres.")]
        public Vector3 colonistCursor = new Vector3(1.15f, 2.7f, 1.15f);

        /// <summary>
        /// Margin the item bracket leaves around the art, in metres, so it frames rather than clips.
        /// </summary>
        const float ItemCursorMargin = 0.16f;

        /// <summary>
        /// The selection cursor, sized to what is actually selected rather than to the cell.
        ///
        /// Four tiers, decided in this order from data presentation already reads:
        ///
        /// - **A colonist** — the figure bracket, placed with the same tween the figure uses so it
        ///   rides the walk instead of hopping cell to cell out of step with the person in it.
        /// - **An item** — a box fitted to the item's own drawn bounds, which the module library
        ///   already knows because it is what placed the art on the floor. A half-height crate
        ///   gets a half-height bracket because the art says so, not because anyone typed a
        ///   number for crates.
        /// - **A wall or solid cell** — the full cell cube, since the cell really is full.
        /// - **Empty ground** — a flat ring on the floor. A click has to answer with something or
        ///   it reads as ignored, but a three-metre cube over bare grass claims a thing is there
        ///   when it is not.
        ///
        /// The composition root draws every tier, because the tiers need the snapshot and the
        /// rig deliberately has no access to it; the rig's own cell cursor is switched off for
        /// good rather than negotiated frame by frame.
        /// </summary>
        void DrawSelectionCursor(WorldSnapshot snapshot, int movePerTick)
        {
            if (_renderer == null || _model == null || cameraRig == null) return;
            cameraRig.SuppressCellCursor = true;

            Color colour = cameraRig.selectionColour;
            SelectionDirector? selection = Directors?.Selection;

            if (selection != null && selection.HasPawn
                && snapshot.TryGetPawn(selection.Pawn, out PawnView pawn))
            {
                // The figure's own position where there is one, for the same reason the hit-test
                // uses it: a working colonist is stepped off their cell, and a bracket drawn from
                // the pose would sit on the cell while the person stands beside it.
                if (_figures == null || !_figures.TryGetFeet(pawn.Id, out Vector3 feet))
                    feet = PawnPose.Of(pawn, _tickAlpha, movePerTick, out _);
                _renderer.DrawSelectionBracket(
                    feet + Vector3.up * (colonistCursor.y * 0.5f), colonistCursor, colour);
                return;
            }

            CellRef? picked = selection?.Cell;
            if (picked == null) return;
            CellRef cell = picked.Value;

            if (selection != null && selection.HasThing)
            {
                ResolvedModule item = _model.Library[
                    _model.Library.Resolve(ModuleIds.Item(selection.ThingDef), ModuleShape.Pillar)];
                if (item.UsesArt && !item.IsEmpty)
                {
                    Bounds box = item.Bounds;
                    _renderer.DrawSelectionBracket(
                        GroundRelief.Lift(CellMetrics.FloorCentre(cell)) + box.center,
                        box.size + Vector3.one * ItemCursorMargin, colour);
                    return;
                }
            }

            int index = _model.Size.Index(cell.X, cell.Z, cell.Y);
            if (_model.IsSolid(index) || _model.EdificeDef(index) != CoreContent.EdificeNone)
            {
                _renderer.DrawCellHighlight(cell, colour);
                return;
            }

            _renderer.DrawFloorBracket(cell, colour);
        }

        void OnGUI()
        {
            // The developer overlay (A15) is state on the overlay director, off by default and
            // toggled by the backtick key; this is the one region immediate mode is permitted in.
            if (Directors == null || !Directors.Overlays.DeveloperVisible) return;
            if (_renderer == null || _world == null || _model == null) return;
            int activeLayer = cameraRig != null ? cameraRig.ActiveLayer : _world.Views.SliceLayer;
            AboveMode above = cameraRig != null ? cameraRig.slice.above : AboveMode.Xray;

            var text =
                $"tick {_world.CurrentTick}  speed {_world.GameSpeed}   layer {activeLayer}/{_model.Size.SizeY - 1}" +
                $"  above: {above}\n" +
                $"draw calls {_renderer.DrawCalls}   instances {_renderer.InstancesDrawn}" +
                $"   chunks {_renderer.ChunksDrawn}   materials {_renderer.MaterialCount}" +
                $"   surround {_renderer.Skirt.InstancesDrawn}" +
                $"   figures {_figures?.FigureCount ?? 0} @ {_figures?.FastestSpeed ?? 0f:0.0} m/s\n" +
                $"frame {_smoothedFrameMs:0.00} ms ({(_smoothedFrameMs > 0f ? 1000f / _smoothedFrameMs : 0f):0}fps)" +
                $"   submit {_renderMs:0.00} ms   tick {_tickMs:0.00} ms   remeshed {_renderer.ChunksMeshedThisFrame}\n" +
                $"WASD pan - Q/E orbit - wheel zoom - R/F layer - V above-mode - B below-mode - " +
                $"space pause - 1/2/3 speed - Home frame\n{_catalogueNote}";

            // Drawn below the ledger rather than over it: this is the developer overlay (A15),
            // the one region immediate mode is permitted in, and it must not sit on the HUD's
            // top-left region when both are visible.
            GUI.color = Color.black;
            GUI.Label(new Rect(11f, 181f, 1400f, 110f), text);
            GUI.color = Color.white;
            GUI.Label(new Rect(10f, 180f, 1400f, 110f), text);
        }

        void OnDestroy()
        {
            if (cameraRig != null)
            {
                if (Directors != null) Directors.Slice.LayerChanged -= OnActiveLayerChanged;
                cameraRig.GameSpeedRequested -= OnGameSpeedRequested;
            }
            _figures?.Dispose();
            _renderer?.Dispose();
            // The library owns every mesh it baked or merged, and a Mesh made in code is a GPU
            // allocation Unity never collects. Leaving Play mode without this leaked the whole
            // cast, every session, until the graphics device was reset out from under the editor.
            _model?.Library.Dispose();
            if (_actorMaterial != null) Destroy(_actorMaterial);
        }
    }
}
