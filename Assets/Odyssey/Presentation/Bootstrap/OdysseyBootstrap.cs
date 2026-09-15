#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
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

        [Tooltip("Flat grass everywhere, no trees, rock or ore. The plain board to build from.")]
        public bool barrenMap = true;

        [Tooltip("Colonists spawned near the start location when the scene begins.")]
        public int colonistCount = 5;

        [Header("Presentation")]
        public ModuleCatalogue? moduleCatalogue;
        public SliceCameraRig? cameraRig;
        public bool castShadows = true;

        [Header("Tick")]
        [Tooltip("Ticks per second at speed 1. The simulation has no notion of seconds; this is it.")]
        public int ticksPerSecond = 60;

        [Tooltip("Ceiling on catch-up ticks in one frame, so a stall cannot spiral.")]
        public int maxTicksPerFrame = 8;

        [Header("Diagnostics")]
        public bool showReadout = true;

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
        readonly Stopwatch _frameTimer = new Stopwatch();
        double _renderMs;
        double _tickMs;
        float _smoothedFrameMs;
        string _catalogueNote = string.Empty;

        public SimWorld? World => _world;
        public WorldRenderModel? Model => _model;
        public ChunkRenderer? Renderer => _renderer;

        void Start()
        {
            var size = new GridSize(sizeX, sizeZ, layers);
            _grid = new CellGrid(size);
            var chunks = new ChunkGrid(size);

            // The prototype starts on empty natural ground and the colony builds from nothing
            // (ADR 0008). The ruined-city generator is still here and still tested; switch
            // mapType to reach it.
            _gen = MapGenerator.DefaultDef(mapType, size);
            if (barrenMap && _gen is NaturalMapGenDef natural) natural.MakeBarren();

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
            _pawns = new PawnContext(_grid, nav, pathService, PawnContent.Core());

            var support = new SupportSystem(grid, solver, chunks);
            PawnContext pawns = _pawns;

            _world = new SimWorldBuilder()
                .WithSeed(seed)
                .WithSize(size)
                .AddSystem(_ => support)
                .AddSystem(_ => new NavigationSystem(nav, support))
                .AddSystem(_ => new NeedsSystem(pawns))
                .AddSystem(_ => new JobSystem(pawns))
                .AddSystem(_ => new MovementSystem(pawns))
                .AddTickable(_ => pawns.Pawns)
                .AddSnapshotContributor(mirror)
                .AddSnapshotContributor(pawns.Pawns)
                .Build();

            var placement = ColonyScenario.Place(_grid, _pawns, outcome.StartCell, seed, colonistCount);
            if (placement.Colonists == 0)
                Debug.LogError($"[Odyssey] no colonists were placed near {outcome.StartCell}: {placement}");

            // One tick primes the mirror: the contributor runs in the publish phase, so until the
            // world has ticked once there is no published frame and nothing to draw.
            _world.Intents.Submit(new Intent(IntentKind.SetSliceLayer, default, outcome.StartCell.Y));
            _world.Tick();

            _renderer = new ChunkRenderer(_model) { CastShadows = castShadows, GameObjectLayer = gameObject.layer };
            _actorMaterial = new Material(library.FallbackMaterial) { name = "Odyssey/Actor" };
            // High-contrast against grass, earth and stone, which tan was not.
            _actorMaterial.SetColor("_BaseColor", new Color(0.98f, 0.36f, 0.20f));

            // Live figures for the pawns on screen. Everything else keeps the baked instanced
            // form, and so does everybody if the packs are absent or the catalogue has no gaits.
            _figures = new PawnFigureDirector(moduleCatalogue, transform, gameObject.layer);

            if (cameraRig != null)
            {
                // Bind to the layer the colony actually stands on, not the generator nominal
                // ground layer. The surface is terraced, so StartCell.Y sits one to three layers
                // above groundLayer, and RenderActors culls anything above the active layer -
                // which meant every colonist was culled every frame while the terrain drew fine.
                cameraRig.Bind(_model, _renderer, outcome.StartCell.Y);
                cameraRig.ActiveLayerChanged += OnActiveLayerChanged;
                cameraRig.GameSpeedRequested += OnGameSpeedRequested;
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
            _renderer.Render(activeLayer, slice);

            // Figures first, because what they take is what the instanced pass must leave alone.
            // Their graphs advance on their own clock once played, so nothing is evaluated here.
            int movePerTick = PawnContent.Core().Movement.movePerTick;
            _figures?.Sync(_world.Views.Current, activeLayer, slice, _tickAlpha, movePerTick,
                Time.deltaTime);

            if (_actorMaterial != null)
                _renderer.RenderActors(_world.Views.Current, activeLayer, slice, _actorMaterial,
                    _tickAlpha, movePerTick, _figures?.Drawn);
            _frameTimer.Stop();
            _renderMs = _frameTimer.Elapsed.TotalMilliseconds;

            float frameMs = Time.unscaledDeltaTime * 1000f;
            _smoothedFrameMs = _smoothedFrameMs <= 0f ? frameMs : Mathf.Lerp(_smoothedFrameMs, frameMs, 0.05f);
        }

        void OnGUI()
        {
            if (!showReadout || _renderer == null || _world == null || _model == null) return;
            int activeLayer = cameraRig != null ? cameraRig.ActiveLayer : _world.Views.SliceLayer;
            AboveMode above = cameraRig != null ? cameraRig.slice.above : AboveMode.Xray;

            var text =
                $"tick {_world.CurrentTick}  speed {_world.GameSpeed}   layer {activeLayer}/{_model.Size.SizeY - 1}" +
                $"  above: {above}\n" +
                $"draw calls {_renderer.DrawCalls}   instances {_renderer.InstancesDrawn}" +
                $"   chunks {_renderer.ChunksDrawn}   materials {_renderer.MaterialCount}" +
                $"   figures {_figures?.FigureCount ?? 0} @ {_figures?.FastestSpeed ?? 0f:0.0} m/s\n" +
                $"frame {_smoothedFrameMs:0.00} ms ({(_smoothedFrameMs > 0f ? 1000f / _smoothedFrameMs : 0f):0}fps)" +
                $"   submit {_renderMs:0.00} ms   tick {_tickMs:0.00} ms   remeshed {_renderer.ChunksMeshedThisFrame}\n" +
                $"WASD pan - Q/E orbit - wheel zoom - R/F layer - V above-mode - B below-mode - " +
                $"space pause - 1/2/3 speed - Home frame\n{_catalogueNote}";

            GUI.color = Color.black;
            GUI.Label(new Rect(11f, 11f, 1400f, 110f), text);
            GUI.color = Color.white;
            GUI.Label(new Rect(10f, 10f, 1400f, 110f), text);
        }

        void OnDestroy()
        {
            if (cameraRig != null)
            {
                cameraRig.ActiveLayerChanged -= OnActiveLayerChanged;
                cameraRig.GameSpeedRequested -= OnGameSpeedRequested;
            }
            _figures?.Dispose();
            _renderer?.Dispose();
            if (_actorMaterial != null) Destroy(_actorMaterial);
        }
    }
}
