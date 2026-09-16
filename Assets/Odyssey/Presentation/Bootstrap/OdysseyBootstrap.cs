#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using Odyssey.Hud;
using Odyssey.Presentation.Audio;
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

        [Tooltip("Scatter trees over the background hills, out to 900 m. They are what gives the distance a scale; off is the old bare hillside. Decoration only, like the rest of the surround.")]
        public bool skirtHillTrees = true;

        [Tooltip("Roll a fresh cast every session: different faces, hair, skin and clothes each time you press Play. Off deals from the world seed instead, so a given world is the same people on every load. Either way colonistLookSeed overrides it.")]
        public bool randomCastEachSession = true;

        [Tooltip("Pin one cast. 0 follows the switch above; any other value deals that cast every time, and the log prints the value used so a cast you liked can be kept.")]
        public int colonistLookSeed = 0;

        /// <summary>
        /// The hour the colony's first day begins, 0 to 23. Noon by default: the board is lit for
        /// midday and has no day/night lighting, so a clock starting at 00:00 meant a player saw
        /// noon and heard the night bed. Set it negative to start at tick 0 the way a headless
        /// run does.
        /// </summary>
        [Range(-1, 23)] public int startHour = 12;

        [Header("Presentation")]
        public ModuleCatalogue? moduleCatalogue;
        public AudioCatalogue? audioCatalogue;
        public SliceCameraRig? cameraRig;
        public bool castShadows = true;

        [Tooltip("The sun to move through the day. Left empty, the cycle finds the first directional light in the scene.")]
        public Light? sun;

        [Tooltip("Run the day through blue noon, orange dawn and dusk, and a dark night. Off holds the hour the scene was built at.")]
        public bool daylightCycle = true;

        [Tooltip("Tufts of grass per hundred grass cells. 0 is bare ground; 60 is a tuft on six cells in ten.")]
        [Range(0, 300)]
        public int grassScatter = 60;

        [Tooltip("How far the drawn ground rolls above and below its layer, in metres. Decoration only: the cells stay flat, so nothing here changes pathing, the save or the hash. 0 is the flat board.")]
        [Range(0f, 3f)]
        public float groundRelief = GroundRelief.BoardAmplitude;

        [Tooltip("The wavelength of the longest swell, in metres. Shorter is steeper and reads more strongly, at the cost of faceting between cells.")]
        [Range(40f, 400f)]
        public float groundReliefPeriod = 150f;

        [Tooltip("Fade whatever stands between the camera and a selected colonist, so a tree cannot hide the person you are watching.")]
        public bool seeThroughToSelection = true;

        [Tooltip("How wide the beam to a selected colonist is, in metres. It stands for the width of the person, not the thickness of the line.")]
        [Range(0.2f, 3f)]
        public float seeThroughRadius = SightLines.DefaultRadius;

        [Tooltip("How solid an occluder in the way is left. 0 would be invisible; a hint of what is there reads better than a hole.")]
        [Range(0f, 1f)]
        public float seeThroughAlpha = ChunkRenderer.DefaultSightFadeAlpha;

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
        AudioDirector? _audio;
        DaylightDirector? _daylight;
        Material? _actorMaterial;
        ColonistMaterials? _colonistMaterials;
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
            WarnIfTheSceneIsStale();

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

            // Noon, before the world has ticked once. It has to be here and not further down:
            // the composition root ticks once during setup to publish a first frame, and
            // SimWorld.StartAtTick refuses a clock that has already run — which is how this was
            // caught being in the wrong place rather than quietly starting the day an hour late.
            if (startHour >= 0) _world.StartAtTick(startHour * GameClock.TicksPerHour);

            ScenarioDef scenarioDef = scenario == StartingScenario.Bare ? ScenarioDef.Bare() : ScenarioDef.Playtest();
            var placement = ColonyScenario.Place(_grid, _pawns, outcome.StartCell, seed, scenarioDef);
            if (placement.Colonists == 0)
                Debug.LogError($"[Odyssey] no colonists were placed near {outcome.StartCell}: {placement}");
            int marked = ColonyScenario.GiveStartingOrders(designations, outcome.StartCell, scenarioDef);
            if (marked > 0)
                Debug.Log($"[Odyssey] {scenarioDef}: {marked} cells marked for work before the first tick — " +
                          $"trees within {scenarioDef.startingFellRadius} cells of the start, and the nearest " +
                          $"outcrop within {scenarioDef.startingMineRadius}");

            // One tick primes the mirror: the contributor runs in the publish phase, so until the
            // world has ticked once there is no published frame and nothing to draw.
            _world.Intents.Submit(new Intent(IntentKind.SetSliceLayer, default, outcome.StartCell.Y));
            _world.Tick();

            // Where the cast comes from, and the reasoning has now changed twice, so it is worth
            // stating all three positions rather than leaving the file arguing with itself.
            //
            // It began as a *fixed* hash of the pawn id, which dealt the starting five — always
            // pawns 1 to 5 — the same five faces every play, so a cast of sixty-one read as a cast
            // of five. Then a number rolled at startup, which fixed that at the price of a
            // colonist who was somebody else after a reload. Then the world seed, which fixes both
            // — a different world is a different cast, and a given world is the same people every
            // load, so an appearance is a property of the colonist rather than of the session.
            //
            // **And the world seed is pinned in the scene**, which is the part that matters here:
            // with `seed` fixed at 1, "the same world deals the same people" means the same twelve
            // people every single time you press Play. That is right for a saved colony and wrong
            // for looking at what the palette does, which is what the owner is doing now — so the
            // roll is back, behind a switch, defaulting on while the look is being judged. Turning
            // it off restores the stable cast exactly, and it is what a real saved game will want.
            //
            // None of it enters the simulation and none of it is saved: nothing is stored, because
            // the same inputs are re-derived. See ColonistAppearance.
            uint castSeed =
                colonistLookSeed != 0 ? (uint)colonistLookSeed :
                randomCastEachSession ? (uint)UnityEngine.Random.Range(1, int.MaxValue) :
                seed;
            var appearances = new ColonistAppearanceBook(castSeed, moduleCatalogue);
            // One ink line in the game, not two. Characters draw their own hull because they are
            // absent from the depth texture the world's outline pass reads, so the colour and
            // width have to be copied across from the feature that inks everything else.
            ColonistMaterials.AdoptInkFrom();
            _colonistMaterials = new ColonistMaterials();
            Debug.Log($"[Odyssey] colonist cast seed {castSeed} over {appearances.LookCount} faces, " +
                      (colonistLookSeed != 0 ? "pinned by colonistLookSeed" :
                       randomCastEachSession ? "rolled for this session — copy it into colonistLookSeed to keep this cast" :
                       "dealt from the world seed"));

            _renderer = new ChunkRenderer(_model)
            {
                CastShadows = castShadows,
                GameObjectLayer = gameObject.layer,
                ScatterDensity = grassScatter,
                Appearances = appearances,
            };
            _renderer.Skirt.Enabled = terrainSkirt;
            _renderer.Skirt.TreeDensityPercent = skirtTreeDensity;
            _renderer.Skirt.HillTrees = skirtHillTrees;
            if (terrainSkirt)
            {
                _renderer.Skirt.Build();
                Debug.Log($"[Odyssey] surround: {_renderer.Skirt.GroundInstances} ground tiles, " +
                          $"{_renderer.Skirt.TreeInstances} trees, " +
                          $"{_renderer.Skirt.FarTreeInstances} more on the hills " +
                          $"and {_renderer.Skirt.TuftInstances} tufts " +
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
                Appearances = appearances,
                Materials = _colonistMaterials,
                // So a climbing figure can find the block it is climbing against. The same mirror
                // the chunk renderer meshes from, so the rock it is pressed to is the rock drawn.
                //
                // **Restored by hand during the merge, and this line is a trap.** Removing
                // climbing as a MINING mechanic took this with it, and git then auto-merged that
                // removal without flagging a conflict — leaving the climb pose compiled, correct
                // and never executed, because `TryWallBeside` needs the mirror and `ApplyClimbPose`
                // is gated on it having found a face. Ladders are still climbed.
                World = _model,
            };

            Directors = new HudDirectors(size.SizeY, outcome.StartCell.Y);
            Directors.Slice.LayerChanged += OnActiveLayerChanged;

            // Sound, built once beside the figures: one director serves the whole colony, reading
            // the published frame and the render mirror and nothing the simulation owns. The
            // faders come from the player's stored settings (the B17 stub), so a volume the
            // player set last session is set again before the first frame is drawn. A null
            // catalogue — a clone without the audio assets — yields a working, silent game.
            _audio = new AudioDirector(
                audioCatalogue, _model != null ? new MirrorTerrain(_model) : null,
                size, transform, gameObject.layer, outcome.StartCell.Y);
            AudioSettingsStore.Load().ApplyTo(_audio);

            // The light through the day. It finds the scene's own sun rather than making one,
            // because the scene builder already places it and two directional lights is a
            // doubled key nobody would think to look for.
            Light? key = sun != null ? sun : FindKeyLight();
            if (daylightCycle && key != null)
            {
                _daylight = new DaylightDirector(key, RenderSettings.skybox);
                _daylight.Apply(_world.CurrentTick);
            }
            if (_figures != null) _figures.BlowLanded += OnBlowLanded;

            if (cameraRig != null)
            {
                // Bind to the layer the colony actually stands on, not the generator nominal
                // ground layer. The surface is terraced, so StartCell.Y sits one to three layers
                // above groundLayer, and RenderActors culls anything above the active layer -
                // which meant every colonist was culled every frame while the terrain drew fine.
                cameraRig.Bind(_model, _renderer, Directors);

                // The depth the game opens at, which is what SliceSettings.followDepth measures
                // "underground" against. It is the colony's own layer for the same reason the
                // slice binds to it: the surface is terraced, so the generator's nominal ground
                // layer is one to three below where anybody is actually standing.
                if (cameraRig.slice != null) cameraRig.slice.surfaceLayer = outcome.StartCell.Y;

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

        /// <summary>
        /// A tool landed somewhere: chop or pick by the style the figure already resolved, played
        /// from the edge the chips left. The director does the rest — distance, cooldown, pitch.
        /// </summary>
        void OnBlowLanded(int workStyle, Vector3 edge) =>
            _audio?.PlayOneShot(SoundIds.ForBlow(workStyle), edge);

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

            // The light follows the clock every frame, not every tick: at speed 3 several ticks
            // retire in one frame and the sky would step, and when the game is paused the hour
            // stops with it, which is right — a paused world should not go on getting dark.
            _daylight?.Apply(_world.CurrentTick);
        }

        /// <summary>
        /// The scene's own key light, when the inspector field is empty.
        ///
        /// <para>Found rather than created, because the scene builder already places a sun and a
        /// second directional light would be a doubled key — everything lit twice, no error, and
        /// nothing in the picture that says why. Brightest wins, so a lamp added for a screenshot
        /// cannot quietly take the sun's job.</para>
        /// </summary>
        static Light? FindKeyLight()
        {
            Light? best = null;
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                if (best == null || light.intensity > best.intensity) best = light;
            }
            return best;
        }

        /// <summary>
        /// Say so, loudly, when the play scene was built before a presenter existed.
        ///
        /// <para><b>A stale scene fails silently, and that is what makes it expensive.</b>
        /// <c>Assets/Scenes/Play.unity</c> is committed <i>and</i> generated: the generator is
        /// <c>PlayScene.cs</c> and the scene is its output. Add a component to the generator and
        /// the committed scene does not have it until somebody runs
        /// <b>Odyssey → Presentation → Build play scene</b>. Until they do, the feature is simply
        /// absent — no error, no missing reference, nothing to see. It is indistinguishable from a
        /// broken feature, and it cost a whole playtest round: designation was reported as "nothing
        /// happened" when in truth nothing was there.</para>
        ///
        /// <para>Checked by name rather than by a generator stamp because a stamp has to be
        /// remembered and this does not: a presenter the composition root depends on is either on
        /// the object or it is not. The list is short and it is the list of things whose absence
        /// is silent — a missing renderer throws, a missing presenter does nothing at all.</para>
        ///
        /// <para>It warns rather than adding the component itself. Adding it would paper over a
        /// scene that may be stale in ways this cannot see — the camera rig, the lighting, the
        /// module catalogue — and the useful signal is "rebuild the scene", not "one thing has
        /// been quietly patched".</para>
        /// </summary>
        void WarnIfTheSceneIsStale()
        {
            if (GetComponent<DesignatePresenter>() != null) return;

            // Add it, then say so. **This reverses a decision, and the reversal is the point.**
            //
            // The first version of this only warned, on the argument that self-healing would
            // paper over a scene that might be stale in other ways the check cannot see. That
            // argument is still true and it was still the wrong call: a presenter is a
            // composition-root concern, this IS the composition root, and the cost of being
            // principled about it was that the mining and felling keys did nothing four playtests
            // running. A warning nobody acts on is not a safeguard, it is a note.
            //
            // So the feature works whether or not the scene has been rebuilt, and the staleness
            // is still reported rather than hidden. Rebuilding remains the right thing to do —
            // the scene may well be stale in other ways — but it is no longer the difference
            // between a feature existing and not.
            gameObject.AddComponent<DesignatePresenter>();

            Debug.LogWarning(
                "[Odyssey] This play scene was built before DesignatePresenter existed. It has " +
                "been added at runtime so the mining and felling keys (M, C, X) work, but the " +
                "scene is generated and is out of date: rebuild it with " +
                "Odyssey > Presentation > Build play scene.");
        }

        void LateUpdate()
        {
            if (_renderer == null || _model == null || _world == null) return;
            int activeLayer = cameraRig != null ? cameraRig.ActiveLayer : _world.Views.SliceLayer;
            SliceSettings slice = cameraRig != null ? cameraRig.slice : new SliceSettings();

            _frameTimer.Restart();
            // The rig sits on the camera, so its position is the viewer's.
            if (cameraRig != null) _renderer.ViewerPosition = cameraRig.transform.position;
            int movePerTick = PawnContent.Core().Movement.movePerTick;

            // Before the world is submitted, because it decides how part of the world is drawn.
            // It reads the figures placed on the *previous* frame, which is the one frame of lag
            // this is worth: a tree fading a sixtieth of a second late is not observable, and
            // placing the figures first would mean drawing the world after the people in it.
            UpdateSightLines(_world.Views.Current, movePerTick);
            _renderer.Render(activeLayer, slice);

            // Figures first, because what they take is what the instanced pass must leave alone.
            // Their graphs advance on their own clock once played, so nothing is evaluated here.
            _figures?.Sync(_world.Views.Current, activeLayer, slice, _tickAlpha, movePerTick,
                Time.deltaTime);

            // Sound after the figures, so a blow that landed this frame sounds on the same frame
            // its chips fly. The listener is the camera (where the AudioListener lives) and the
            // ambience anchor is its focus, which sits down among the water rather than up where
            // the camera itself is.
            if (_audio != null)
                _audio.Sync(Time.deltaTime, _world.Views.Current,
                    cameraRig != null ? cameraRig.transform.position : transform.position,
                    cameraRig != null ? cameraRig.Focus : transform.position,
                    activeLayer);

            if (_actorMaterial != null)
                _renderer.RenderActors(_world.Views.Current, activeLayer, slice, _actorMaterial,
                    _tickAlpha, movePerTick, _figures?.Drawn);

            DrawStandingOrders(_world.Views.Current);
            DrawBuildingSites(_world.Views.Current);
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
        /// <summary>
        /// Every standing order on a drawn layer, and how far through it the colony is.
        ///
        /// <para><b>Nothing drew these at all.</b> The designation channel has been published
        /// since designations existed and no part of presentation ever read it, so a marked cell
        /// looked exactly like an unmarked one and the only way to know what had been ordered was
        /// to watch somebody walk to it. A bracket says the order is there; the cut slab says how
        /// far along it is (owner, 2026-09-16 — "some graphical indication").</para>
        ///
        /// <para><b>Filtered to the drawn band rather than to the active layer</b>, because the
        /// picker stopped being clipped to one layer on 2026-09-16 and an order can now be given
        /// anywhere the player can see. Marking it on the layer it was given on is the whole point;
        /// drawing it on a layer that is not on screen would put a bracket in mid-air.</para>
        /// </summary>
        void DrawStandingOrders(WorldSnapshot snapshot)
        {
            if (_renderer == null || cameraRig == null) return;

            System.ReadOnlySpan<OrderView> orders = snapshot.Orders;
            if (orders.Length == 0) return;

            GridSize size = snapshot.Size;
            int lowest = System.Math.Max(0, cameraRig.LowestSelectableLayer);
            int highest = cameraRig.HighestSelectableLayer;

            for (int i = 0; i < orders.Length; i++)
            {
                CellRef cell = size.FromIndex(orders[i].CellIndex);
                if (cell.Y < lowest || cell.Y > highest) continue;

                Color tint = orders[i].Kind == (byte)DesignationKind.Mine ? MineOrderColour : FellOrderColour;
                _renderer.DrawCellMark(cell, tint);

                if (orders[i].Progress > 0)
                    _renderer.DrawCellCut(cell, orders[i].Progress / 255f, CutColour);
            }
        }

        /// <summary>
        /// Every building site on a drawn layer: what was ordered, and how far along it is.
        ///
        /// <para><b>Two measures, drawn as two things.</b> The mark says an order is here; the slab
        /// rising out of the floor says how much of the thing exists. A site that has not been fed
        /// shows a mark and nothing else, which is the picture the player needs — "nobody has
        /// brought the wood yet" and "it is half built" are different problems with different
        /// answers, and one bar would merge them (<c>SiteView</c> says the same thing from the
        /// simulation's side).</para>
        ///
        /// <para>Deliberately a mark and a slab rather than a ghost of the finished wall. A ghost
        /// wants the mesher to place a module it has not been asked for, which is the mesh
        /// contributor seam (OQ-46) and a larger change than this; the mark and the fill are the
        /// precedent standing orders already set, cost one instanced cube each, and read.</para>
        /// </summary>
        void DrawBuildingSites(WorldSnapshot snapshot)
        {
            if (_renderer == null || cameraRig == null) return;

            System.ReadOnlySpan<SiteView> sites = snapshot.Sites;
            if (sites.Length == 0) return;

            GridSize size = snapshot.Size;
            int lowest = System.Math.Max(0, cameraRig.LowestSelectableLayer);
            int highest = cameraRig.HighestSelectableLayer;

            for (int i = 0; i < sites.Length; i++)
            {
                CellRef cell = size.FromIndex(sites[i].CellIndex);
                if (cell.Y < lowest || cell.Y > highest) continue;

                _renderer.DrawCellMark(cell, BuildOrderColour);

                if (sites[i].Progress > 0)
                    _renderer.DrawCellFill(cell, sites[i].Progress / 255f, FrameColour);
            }
        }

        /// <summary>Marks a cell ordered dug. Warm, against the cool stone it is drawn over.</summary>
        static readonly Color MineOrderColour = new Color(0.95f, 0.72f, 0.32f, 0.42f);

        /// <summary>Marks a tree ordered felled.</summary>
        static readonly Color FellOrderColour = new Color(0.55f, 0.85f, 0.45f, 0.42f);

        /// <summary>
        /// Marks a cell ordered built. The interface accent rather than a third warm hue, because
        /// a build order is the one standing order that is <em>additive</em> — mine and fell take
        /// something away, and a colour the rest of the interface already uses for "the player
        /// asked for this" separates the two at a glance.
        /// </summary>
        static readonly Color BuildOrderColour = new Color(0.44f, 0.83f, 0.89f, 0.42f);

        /// <summary>
        /// The thing going up. Pale and translucent like <see cref="CutColour"/> and for the same
        /// reason — it is material, not a marker — but it fills from the floor rather than eating
        /// down from the top, because that is the direction a wall is actually built in.
        /// </summary>
        static readonly Color FrameColour = new Color(0.82f, 0.78f, 0.66f, 0.38f);

        /// <summary>
        /// The non-primary members of a multi-selection: the same shape as the primary's bracket
        /// at half the presence, so the set reads as one selection with a head rather than as
        /// several selections that happen to share a screen.
        /// </summary>
        static readonly Color SecondarySelectionColour = new Color(1f, 1f, 1f, 0.45f);

        /// <summary>
        /// The cut itself: pale, so it reads as fresh broken stone rather than as a coloured
        /// marker, and translucent so the rock is still visible through what has come off it.
        /// </summary>
        static readonly Color CutColour = new Color(0.86f, 0.87f, 0.90f, 0.30f);

        /// <summary>
        /// The lines the renderer fades along: eye to chest, one per selected colonist.
        ///
        /// <para><b>The same point the bracket is drawn at and the same point the hit-test aims
        /// at</b>, taken from the live figure where there is one and from the pose otherwise —
        /// which matters for exactly the reason it matters there: a working colonist is stepped
        /// off their cell towards the tree they are felling, so a line aimed at the cell would
        /// clear the trunk that is hiding them. Three readings of one position would drift; this
        /// is the third caller of the same two lines and they should be one method
        /// (<c>OQ</c>-worthy, not done here).</para>
        ///
        /// <para>Only colonists. An item or a cell can be selected too, but a crate does not walk
        /// behind a tree and a marked rock is drawn with a bracket that already shows through.</para>
        /// </summary>
        void UpdateSightLines(WorldSnapshot snapshot, int movePerTick)
        {
            if (_renderer == null || _model == null) return;
            _sight.Clear();
            _renderer.Sight = _sight;
            _renderer.SightFadeAlpha = seeThroughAlpha;
            _sight.Radius = seeThroughRadius;

            if (!seeThroughToSelection || cameraRig == null) return;
            SelectionDirector? selection = Directors?.Selection;
            if (selection == null || !selection.HasPawn) return;

            Vector3 eye = cameraRig.transform.position;
            var selected = selection.Pawns;
            for (int i = 0; i < selected.Count && i < MaxSightLines; i++)
            {
                if (!snapshot.TryGetPawn(selected[i], out PawnView pawn)) continue;
                if (_figures == null || !_figures.TryGetFeet(pawn.Id, out Vector3 feet))
                    feet = PawnPose.Of(pawn, _tickAlpha, movePerTick, out _, _model);
                _sight.Add(eye, feet + Vector3.up * (colonistCursor.y * 0.5f));
            }
        }

        /// <summary>
        /// The most lines of sight drawn at once. A box selection can hold the whole colony, and
        /// each line costs a slab test per instance in every chunk any of them crosses — so the
        /// cost of the feature would grow with the size of the selection, which is the one thing
        /// it must not do. Beyond this many the player is commanding a crowd rather than watching
        /// a person, and the first few are the ones the camera is on.
        /// </summary>
        const int MaxSightLines = 8;

        readonly SightLines _sight = new SightLines();

        void DrawSelectionCursor(WorldSnapshot snapshot, int movePerTick)
        {
            if (_renderer == null || _model == null || cameraRig == null) return;
            cameraRig.SuppressCellCursor = true;

            Color colour = cameraRig.selectionColour;
            SelectionDirector? selection = Directors?.Selection;

            if (selection != null && selection.HasPawn)
            {
                // Every selected colonist is bracketed, the primary at full strength and the rest
                // dimmer, so a box selection reads as a set with one member the pane is about —
                // not as several coincidental primaries.
                for (int i = 0; i < selection.Pawns.Count; i++)
                {
                    if (!snapshot.TryGetPawn(selection.Pawns[i], out PawnView pawn)) continue;
                    // The figure's own position where there is one, for the same reason the hit-test
                    // uses it: a working colonist is stepped off their cell, and a bracket drawn from
                    // the pose would sit on the cell while the person stands beside it.
                    if (_figures == null || !_figures.TryGetFeet(pawn.Id, out Vector3 feet))
                        feet = PawnPose.Of(pawn, _tickAlpha, movePerTick, out _, _model);
                    _renderer.DrawSelectionBracket(
                        feet + Vector3.up * (colonistCursor.y * 0.5f), colonistCursor,
                        i == 0 ? colour : SecondarySelectionColour);
                }
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
            AboveMode above = cameraRig != null
                ? cameraRig.slice.AboveAt(activeLayer)
                : AboveMode.Xray;
            bool underground = cameraRig != null && cameraRig.slice.BelowSurface(activeLayer);

            var text =
                $"tick {_world.CurrentTick}  speed {_world.GameSpeed}   layer {activeLayer}/{_model.Size.SizeY - 1}" +
                $"  above: {above}\n" +
                $"draw calls {_renderer.DrawCalls}   instances {_renderer.InstancesDrawn}" +
                $"   chunks {_renderer.ChunksDrawn}   materials {_renderer.MaterialCount}" +
                $"   surround {_renderer.Skirt.InstancesDrawn}" +
                $"   figures {_figures?.FigureCount ?? 0} @ {_figures?.FastestSpeed ?? 0f:0.0} m/s\n" +
                $"frame {_smoothedFrameMs:0.00} ms ({(_smoothedFrameMs > 0f ? 1000f / _smoothedFrameMs : 0f):0}fps)" +
                $"   submit {_renderMs:0.00} ms   tick {_tickMs:0.00} ms   remeshed {_renderer.ChunksMeshedThisFrame}\n" +
                $"sound played {_audio?.OneShotsPlayed ?? 0} culled {_audio?.DistanceCulled ?? 0}" +
                $" skipped {_audio?.CooldownSkipped ?? 0} starved {_audio?.VoiceStarved ?? 0}" +
                $" noclip {_audio?.ClipMissing ?? 0}" +
                $" water {_audio?.WaterLevel ?? 0f:0.00} music {_audio?.MusicPhase.ToString().ToLowerInvariant() ?? "none"}\n" +
                // The hour the light is at, and how often the ambient probe has been re-integrated.
                // The second is the only real cost in the cycle, so it is the number to watch if
                // the sky is ever suspected of being expensive.
                $"light {(_daylight != null ? $"{_daylight.Hour:00.0}h" : "fixed")}" +
                $"   probe {_daylight?.ProbeUpdates ?? 0}\n" +
                $"WASD pan - Q/E orbit - wheel zoom - R/F layer - V above-mode - B below-mode - " +
                $"space pause - 1/2/3 speed - Home frame\n{_catalogueNote}";

            // Drawn below the ledger rather than over it: this is the developer overlay (A15),
            // the one region immediate mode is permitted in, and it must not sit on the HUD's
            // top-left region when both are visible.
            GUI.color = Color.black;
            GUI.Label(new Rect(11f, 181f, 1400f, 128f), text);
            GUI.color = Color.white;
            GUI.Label(new Rect(10f, 180f, 1400f, 128f), text);
        }

        void OnDestroy()
        {
            if (cameraRig != null)
            {
                if (Directors != null) Directors.Slice.LayerChanged -= OnActiveLayerChanged;
                cameraRig.GameSpeedRequested -= OnGameSpeedRequested;
            }
            if (_figures != null) _figures.BlowLanded -= OnBlowLanded;
            _audio?.Dispose();
            _daylight?.Dispose();
            _figures?.Dispose();
            _colonistMaterials?.Dispose();
            _renderer?.Dispose();
            // The library owns every mesh it baked or merged, and a Mesh made in code is a GPU
            // allocation Unity never collects. Leaving Play mode without this leaked the whole
            // cast, every session, until the graphics device was reset out from under the editor.
            _model?.Library.Dispose();
            if (_actorMaterial != null) Destroy(_actorMaterial);
        }
    }
}
