#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Odyssey.Hud;
using Odyssey.Presentation.Audio;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.Ui;
using Odyssey.Presentation.World;
using Odyssey.Sim;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Defs;
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

        [Tooltip("What a colony started from this scene is called, when the setup page did not ask. Leave it empty to take the naming registry's default, which is the one the setup page prefills and the one place that word is written.")]
        public string colonyName = string.Empty;

        /// <summary>
        /// What to call a colony nobody named: this scene's own word if the inspector carries one,
        /// and the naming registry's otherwise (owner, 2026-09-17: <i>"The Lost Buckets"</i>).
        ///
        /// <para><b>One word, in the CSV, reached from both places that want it.</b> The setup
        /// page prefills its field from the same key, so a player who types nothing and a scene
        /// that asks nobody end up with the same colony rather than with "The Lost Buckets" and
        /// "Landfall" depending on how the world was started.</para>
        /// </summary>
        string DefaultColonyName =>
            string.IsNullOrWhiteSpace(colonyName)
                ? Registry.Label(SeedField.DefaultColonyKey)
                : colonyName;

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

        [Tooltip("Roll a fresh cast every session: different faces, hair, skin and clothes each time you press Play. This overrules each colonist's own roll seed, so the people you picked on the setup screen will NOT be the people you get — it is for judging the palette, not for playing. Off, every colonist looks the way their roll says, and a given world is the same people on every load. colonistLookSeed overrides it.")]
        public bool randomCastEachSession;

        [Tooltip("Pin one cast. 0 follows the switch above; any other value deals the whole colony that cast every time, overruling each colonist's own roll seed, and the log prints the value used so a cast you liked can be kept.")]
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

        /// <summary>
        /// Build a world the moment Play starts, skipping the start screen.
        ///
        /// <para><b>False since U38 (2026-09-17), which is the change of behaviour in that unit
        /// rather than a detail of it.</b> Pressing Play now lands on the start screen, and a
        /// colony exists only once somebody has asked for one. U35 built this flag for exactly
        /// this day, and it stays rather than being deleted: it is the development loop — press
        /// Play, be in a colony — and it is what lets every PlayMode test written before the start
        /// screen existed carry on assuming <c>Start</c> builds a world, by setting it explicitly
        /// in the rig instead of relying on the default.</para>
        ///
        /// <para>A test that relies on the <i>default</i> being true will fail, and should: what
        /// it was really asserting was that a session exists, and it can say so in one line.</para>
        /// </summary>
        public bool buildOnPlay;


        SimWorld? _world;
        CellGrid? _grid;
        WorldRenderModel? _model;
        ChunkRenderer? _renderer;
        PawnContext? _pawns;
        PawnFigureDirector? _figures;
        DesignatePresenter? _designate;
        AudioDirector? _audio;
        DoorDirector? _doors;

        /// <summary>The dead, drawn (design 33 §5). Built, synced and disposed beside the doors; lane B's to fill.</summary>
        CorpseDirector? _corpses;
        BloodDirector? _blood;

        /// <summary>The one reader of the fight's events (design 33 §5).</summary>
        readonly CombatFeedback _combatFeedback = new CombatFeedback();

        /// <summary>The fight's floating words on screen, beneath the HUD (design 33 §1). Built with the session.</summary>
        Ui.CombatFloaterView? _floaterView;

        /// <summary>The dead, for the pick that finds a corpse under the pointer (design 33 §5f).</summary>
        public CorpseDirector? Corpses => _corpses;

        /// <summary>
        /// The title screen's bed. Owned by the root rather than by the session, because it is
        /// the sound of there being no session: it is built once at <see cref="Start"/>, it
        /// survives every world being made and torn down, and it is the only piece of audio that
        /// exists before a colony does.
        /// </summary>
        MenuAmbience? _menuBed;
        DaylightDirector? _daylight;
        Material? _actorMaterial;
        ColonistMaterials? _colonistMaterials;

        PortraitStudio? _portraits;

        /// <summary>
        /// The colonist photographer (<c>docs/design/20-avatars.md</c> §10).
        ///
        /// <para><b>It outlives a colony on purpose.</b> A portrait is a fact about an appearance
        /// rather than about a world, and the screen that needs it most — the setup page — runs
        /// when no colony exists at all. That works because <c>moduleCatalogue</c> is a serialized
        /// field rather than something world build produces. Its pictures are released with the
        /// component, not with the session.</para>
        /// </summary>
        public PortraitStudio Portraits
        {
            get
            {
                if (_portraits == null)
                {
                    ColonistMaterials.AdoptInkFrom();
                    _portraits = new PortraitStudio(moduleCatalogue,
                        _colonistMaterials ??= new ColonistMaterials());
                }
                else if (_portraits.Materials == null)
                {
                    // A colony ending takes the materials with it -- deliberately, since the
                    // pictures were rendered through them. The studio outlives the colony, so
                    // asking for it again has to give it live ones back or it would photograph
                    // every colonist from then on with no shader at all.
                    ColonistMaterials.AdoptInkFrom();
                    _portraits.Materials = _colonistMaterials ??= new ColonistMaterials();
                }

                return _portraits;
            }
        }
        ColonyWorld? _colony;
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

        /// <summary>
        /// Cost units a pawn retires in one tick, the other half of that same tween.
        ///
        /// <para>Read off the colony's own content rather than loaded per read. It used to call
        /// <c>PawnContent.Core()</c>, which built the whole content table — every Def, every array
        /// and list — and then took one integer off it. This property is read three times a frame
        /// (twice by <see cref="SelectionPresenter"/>, once by the render path), so that was a
        /// full content table allocated three times a frame to answer a question the world already
        /// knew the answer to. Zero before the world is built, which is the value the callers
        /// already substitute when the bootstrap is missing.</para>
        /// </summary>
        public int MovePerTick => _pawns?.Content.Movement.movePerTick ?? 0;

        /// <summary>
        /// The live figures, for anything that must agree with where a colonist is actually drawn
        /// rather than with where the simulation keeps them. See <see cref="PawnFigureDirector.TryGetFeet"/>.
        /// </summary>
        public PawnFigureDirector? Figures => _figures;
        public DoorDirector? Doors => _doors;
        readonly Stopwatch _frameTimer = new Stopwatch();
        double _renderMs;
        double _tickMs;

        /// <summary>
        /// How long last frame spent inside <c>SimWorld.Tick</c>, in milliseconds.
        ///
        /// <para>Exposed because a frame-time difference is not automatically a rendering
        /// difference, and this project has already read one as though it were. A thousand
        /// standing orders make the renderer draw a thousand marks <i>and</i> make the work
        /// givers scan a thousand designated cells; both land on the main thread and both show
        /// up in <c>Time.unscaledDeltaTime</c>. The developer overlay has printed these two
        /// numbers since it existed — <c>FrameTimeTests</c> can now read the same pair rather
        /// than attributing the whole difference to whichever half is being worked on.</para>
        /// </summary>
        public double TickMs => _tickMs;

        /// <summary>
        /// How long last frame spent submitting the world, in milliseconds — the other half of
        /// the pair <see cref="TickMs"/> describes.
        /// </summary>
        public double SubmitMs => _renderMs;

        /// <summary>
        /// The parts of the draw block, in the order <c>LateUpdate</c> runs them.
        ///
        /// <para><b>Added 2026-09-20, off a Play report.</b> The owner watched the frame stay
        /// flat while the colony grew and then fall over at a high count, and the sweep that
        /// followed (<c>FrameTimeTests.TheFrameAgainstColonySize</c>) found the growth is
        /// entirely in <see cref="SubmitMs"/> — not the tick (0.43 ms at 384 pawns) and not the
        /// draw calls (1,243 to 1,324 across the whole range). "Submit" is eight different
        /// things, and a number that says the renderer is slow without saying which part of it
        /// is slow only licences a guess. These are that split.</para>
        /// </summary>
        public enum FrameSection
        {
            /// <summary>The render mirror: sites, crops, zones.</summary>
            Mirror = 0,
            /// <summary>The eye-to-colonist lines that ghost whatever stands on them.</summary>
            Sight,
            /// <summary>The chunk buckets and the falling items. The surround is <b>not</b> here.</summary>
            World,
            /// <summary>
            /// The land beyond the board: its ground, its tufts and its wood.
            ///
            /// <para>Split out of <see cref="World"/> on 2026-09-21. It is submitted from inside
            /// <c>ChunkRenderer.Render</c> and had been charged to the board ever since, which
            /// made the one pass §6c spent a day cutting from 3.65 ms to 2.2 invisible on the
            /// overlay and in <c>FrameTimeTests</c>. The two scale with different things and want
            /// separate numbers.</para>
            /// </summary>
            Surround,
            /// <summary>
            /// Bucketing the colony so the sidestep can ask who is within three metres.
            ///
            /// <para>Its own section rather than a charge on <see cref="Figures"/>, which is where
            /// it is built: the index is rebuilt once and shared by the figures, the baked far
            /// form and the carried stand-in, so billing it to the first of the three would
            /// flatter <see cref="Actors"/> by exactly the amount it hid. It is O(N) and it
            /// replaces an O(N squared) in both of the other two — see
            /// <c>docs/design/25-pawn-steering.md</c>.</para>
            /// </summary>
            Crowd,
            /// <summary>The live Synty figures, capped at <c>PawnFigureDirector.FigureCeiling</c>.</summary>
            Figures,
            /// <summary>Sound: what played, what was culled, the ambience.</summary>
            Audio,
            /// <summary>The baked instanced stand-ins for pawns without a figure.</summary>
            Actors,
            /// <summary>Doors.</summary>
            Doors,
            /// <summary>Orders, zones, sites, the tool preview and the cursors.</summary>
            Overlays,
            Count,
        }

        /// <summary>
        /// This frame's crowd buckets, rebuilt once in <c>LateUpdate</c> and shared by everything
        /// that poses a pawn. See <see cref="FrameSection.Crowd"/>.
        /// </summary>
        readonly Rendering.PawnCrowdIndex _crowd = new Rendering.PawnCrowdIndex();

        readonly double[] _sectionMs = new double[(int)FrameSection.Count];
        readonly Stopwatch _sectionTimer = new Stopwatch();

        /// <summary>Last frame's draw block, split by <see cref="FrameSection"/>.</summary>
        public System.ReadOnlySpan<double> FrameSectionMs => _sectionMs;

        /// <summary>Charge everything since the last mark to this section, and start the next.</summary>
        void MarkSection(FrameSection section)
        {
            _sectionMs[(int)section] += _sectionTimer.Elapsed.TotalMilliseconds;
            _sectionTimer.Restart();
        }
        float _smoothedFrameMs;
        float _smoothedGpuMs;

        Diagnostics.PerfTracer? _tracer;
        double[] _traceSections = System.Array.Empty<double>();

        /// <summary>
        /// Whether a session writes a performance trace.
        ///
        /// <para><b>On in the editor and in development builds, off in a shipped one.</b> The
        /// whole value of a trace is catching what does not reproduce, and a tracer that has to be
        /// switched on before the interesting thing happens never is. It costs a dozen doubles a
        /// frame and a kilobyte a second, and a released player has nobody to read it.</para>
        ///
        /// <para><b>And off in batch mode, which is not a detail.</b> A batch run is the test
        /// tiers, and leaving it on there would do two bad things at once: every arm in
        /// <c>FrameTimeTests</c> would silently start carrying the tracer's own cost in the numbers
        /// this project quotes as budgets, and a PlayMode run would drop a dozen traces of its own
        /// into the folder beside the ones somebody took by playing — where the newest file is no
        /// longer theirs. The one arm that wants tracing sets this itself and puts it back in a
        /// <c>finally</c>.</para>
        /// </summary>
        /// <para><b>Worked out on first use, not in a field initialiser, and that is not a
        /// style choice.</b> A static field initialiser on a <see cref="MonoBehaviour"/> runs in
        /// the serialisation context, where Unity forbids most of its own API — asking
        /// <c>Application.isBatchMode</c> there throws <c>UnityException: get_isBatchMode is not
        /// allowed to be called from a MonoBehaviour constructor (or instance field
        /// initializer)</c>, and because it throws inside the static constructor the whole type
        /// fails to initialise, so every later touch of it rethrows
        /// <c>TypeInitializationException</c>. It shipped that way for one commit on 2026-09-21
        /// and filled the console. Nothing reads this before the first frame, so a lazy default
        /// costs nothing and cannot be asked at a moment Unity objects to.</para>
        public static bool TraceEnabled
        {
            get => _traceEnabled ??= DefaultTracing();
            set => _traceEnabled = value;
        }

        static bool? _traceEnabled;

        static bool DefaultTracing() =>
            !Application.isBatchMode && (Application.isEditor || Debug.isDebugBuild);

        /// <summary>The trace this session is writing, or null when it is not writing one.</summary>
        public Diagnostics.PerfTracer? Trace => _tracer;

        /// <summary>
        /// <see cref="FrameSection"/> by name, minus <c>Count</c>, in enum order — the names a
        /// trace's section columns carry.
        /// </summary>
        public static readonly string[] SectionNames = BuildSectionNames();

        static string[] BuildSectionNames()
        {
            var names = new string[(int)FrameSection.Count];
            for (int i = 0; i < names.Length; i++) names[i] = ((FrameSection)i).ToString();
            return names;
        }
        readonly UnityEngine.FrameTiming[] _frameTimings = new UnityEngine.FrameTiming[1];
        string _catalogueNote = string.Empty;

        /// <summary>
        /// Last frame's GPU time in milliseconds, smoothed, or <c>0</c> where the platform will
        /// not say.
        ///
        /// <para><b>The one number nothing here could previously read, and the reason §6c's
        /// findings stop where they do.</b> Every timing this class takes is a stopwatch around
        /// CPU work, so a frame that is entirely GPU-bound — the case §6c predicts for
        /// alpha-tested foliage at a real resolution, and cannot see at 640 x 480 — reads as a
        /// cheap frame with a slow clock. Fill, overdraw and the shadow pass are invisible to
        /// <see cref="SubmitMs"/> by construction.</para>
        ///
        /// <para>Needs <c>enableFrameTimingStats</c> in the player settings, which is on since
        /// 2026-09-21, and a development build or the editor. Where it is unavailable it stays 0
        /// and the overlay says so rather than printing a zero that looks like a measurement.</para>
        /// </summary>
        public float GpuFrameMs => _smoothedGpuMs;

        // There was a CpuFrameMs here for one day, off FrameTiming.cpuFrameTime, and it was
        // **wrong on screen in its first real session** (owner's 4K shots, 2026-09-21): it read
        // 16.81 ms beside a 16.79 ms frame, which is right, then 296.32, then 17,898.04 over about
        // twenty-five seconds — climbing, so not one poisoned sample decaying out of an average
        // but a stream of bad ones. It is removed rather than repaired because **nothing was lost
        // by removing it**: `frame` and `submit` are this class's own stopwatches, they agree with
        // each other, and between them they say everything a CPU figure would have. The GPU time
        // is kept because it is the one number nothing else here can get, and because the same
        // shots show it steady and plausible — 8.40, 8.15, 9.12 ms at 3840 x 2160.
        //
        // The lesson, which is the general one: a figure the platform hands over is not a
        // measurement until it has been seen beside a figure taken independently. This one was
        // shipped on the strength of being plausible in a batch run at 640 x 480.

        /// <summary>
        /// Ask the platform what the last frame cost on each side of the bus.
        ///
        /// <para><c>CaptureFrameTimings</c> gathers what is ready, which lags the current frame by
        /// a few; that is fine for a readout and useless for attributing a single frame, so the
        /// numbers are smoothed exactly as the frame time is and read as a trend.</para>
        /// </summary>
        /// <summary>The largest a sample may be and still be a frame. Anything over is rejected.</summary>
        const float PlausibleFrameMs = 500f;

        void SampleFrameTimings()
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _frameTimings) == 0) return;

            float gpu = (float)_frameTimings[0].gpuFrameTime;
            if (gpu <= 0f || gpu > PlausibleFrameMs || float.IsNaN(gpu)) return;

            _smoothedGpuMs = _smoothedGpuMs <= 0f ? gpu : Mathf.Lerp(_smoothedGpuMs, gpu, 0.05f);
        }

        public SimWorld? World => _world;

        /// <summary>Is a world built right now? False before the first build and after a teardown.</summary>
        public bool HasSession => _world != null;

        /// <summary>
        /// The world as the simulation composed it — the grid, the colony, the scenario it was
        /// given, and the list of components a save is written from.
        ///
        /// <para>Exposed because the scene had no way to write a save file. Not for want of a save
        /// format: <c>WorldSave</c> has been complete and tested for weeks, but every caller was a
        /// test holding a <c>ColonyWorld</c>, and this was the one build that composed its world by
        /// hand and therefore held none.</para>
        /// </summary>
        public ColonyWorld? Colony => _colony;

        /// <summary>
        /// The interface directors: selection, slice and camera, Unity-free and made here with the
        /// world, because the composition root is the one place that knows the layer count and
        /// the start layer. The rig, the pick presenter and the HUD shell all realise these.
        /// </summary>
        public HudDirectors? Directors { get; private set; }

        /// <summary>
        /// The preferences that outlive a session: the settings panel's levers and the key
        /// bindings (U38).
        ///
        /// <para>Held here rather than inside <see cref="HudDirectors"/>, which is built and thrown
        /// away with each colony. The start screen's Options row opens the settings panel with no
        /// session built at all, so the panel behind it cannot be a thing a session owns — and a
        /// player who quits to the menu and starts again has not asked for their interface scale,
        /// their volumes or their key bindings to be reconsidered.</para>
        /// </summary>
        public SettingsDirector Preferences { get; } = new SettingsDirector();

        /// <summary>The key bindings, for the life of the game rather than of a colony.</summary>
        public HotkeyDirector Keys { get; } = new HotkeyDirector();
        public WorldRenderModel? Model => _model;
        public ChunkRenderer? Renderer => _renderer;

        /// <summary>The colony's one audio director, for the presenter that applies the
        /// settings panel's faders to it live.</summary>
        public AudioDirector? Audio => _audio;

        void Start()
        {
            PointContentAtTheShippedPack();
            WarnIfTheSceneIsStale();

            // Before the session, so that a player who boots straight into a colony never hears
            // a menu bed start under it. Sync decides whether it plays at all, and the first
            // thing it will see is a world.
            _menuBed = new MenuAmbience(audioCatalogue, transform, gameObject.layer);

            if (buildOnPlay || StartedFromTheCommandLine()) BuildSession();
        }

        /// <summary>The switch that boots a player straight into a colony. See below.</summary>
        public const string NewGameArgument = "-odyssey-newgame";

        /// <summary>
        /// Whether this player was told to skip the menus and generate a world at once.
        ///
        /// <para><b>So that a build can be smoke-tested without a person clicking.</b> The first
        /// player build this project made was empty (2026-09-19) and the reason took three
        /// attempts to find, because everything I could run from a terminal stopped at the main
        /// screen — where nothing loads the content pack, nothing generates a world and nothing
        /// draws terrain. A clean log from a player sitting on a menu proves almost nothing, and
        /// I twice reported a fix on the strength of one.</para>
        ///
        /// <para>It exists for the same reason <c>PlayScene.Measure</c> does: the alternative is
        /// judging a build by looking at it, and nobody can look at a build in CI.</para>
        /// </summary>
        static bool StartedFromTheCommandLine()
        {
            foreach (string argument in Environment.GetCommandLineArgs())
                if (argument == NewGameArgument) return true;
            return false;
        }

        /// <summary>
        /// In a built player, read the content pack from <c>StreamingAssets</c>.
        ///
        /// <para><b>Because a player has no repository to walk up to</b>, and the world is made
        /// of the pack: the terrain table, the materials, the pawn tuning. <c>ContentPack</c>
        /// finds its Defs by looking for a directory holding both <c>Assets</c> and
        /// <c>ProjectSettings</c>, which exists on a dev machine and nowhere else, and it throws
        /// when it cannot. World generation then never runs and the scene is empty but for the
        /// figures the start flow had already made — which is exactly what the owner saw on the
        /// first player build this project ever produced (2026-09-19): <i>"there is no terrain —
        /// there seemed to be no graphics, terrain etc, apart from characters"</i>.</para>
        ///
        /// <para><b>This is the half of the arrangement that lives in the composition root, and
        /// <c>ContentPack.FindRoot</c> named it before either half was written</b> — "copy it
        /// into StreamingAssets at build time, and the composition root then calls UseRoot with
        /// Application.streamingAssetsPath. That is why UseRoot exists and why nothing in this
        /// assembly mentions Unity." <c>ContentPackBuild</c> is the other half, and the two share
        /// one spelling of the path rather than agreeing by coincidence.</para>
        ///
        /// <para><b>Only outside the editor</b>, and deliberately. In the editor the repository
        /// is right there and is the one live copy anybody edits; pointing at a staged duplicate
        /// would mean Def changes silently not taking effect on Play. The staged copy is removed
        /// after every build for the same reason — one source of truth, which CLAUDE.md lists as
        /// a standing rule.</para>
        /// </summary>
        static void PointContentAtTheShippedPack()
        {
            if (Application.isEditor) return;

            string root = System.IO.Path.Combine(
                Application.streamingAssetsPath, "Odyssey", "Defs", ContentPack.CoreId);

            if (!System.IO.Directory.Exists(root))
            {
                // Said out loud rather than left to the loader's own exception, because that one
                // reports the directory it searched *from* and not the one it was told to use.
                Debug.LogError(
                    $"[Odyssey] the content pack is not in this build ({root}). The world cannot " +
                    "be generated. It is copied in by ContentPackBuild at build time — build " +
                    "through scripts/unity.sh build rather than Unity's own Build Settings.");
                return;
            }

            ContentPack.UseRoot(root);
        }

        /// <summary>
        /// The presenter is a sibling component, not a thing a session owns.
        ///
        /// <para><b>It used to be dropped by <c>TeardownSession</c> along with the renderer, the
        /// mirror and the colony, and it was the only line in that list that was wrong.</b> Those
        /// are built by a session and must not outlive one; this is a component on the same
        /// GameObject, found once in <c>Start</c> and alive for as long as the object is. Nulling
        /// it meant the reference was gone and nothing ever looked for it again, because
        /// <c>Start</c> does not run twice.</para>
        ///
        /// <para><b>What that cost.</b> <c>DrawToolPreview</c> begins
        /// <c>if (_renderer == null || _designate == null) return;</c>, so from the first teardown
        /// onwards the build cursor, the drag box and the run's ghosts all stopped being drawn —
        /// and nothing said so, because every diagnostic in that path lives further down a method
        /// that was no longer being reached. It did not matter while a session was built once at
        /// <c>Start</c> and never torn down. The start menu made a teardown-and-rebuild the normal
        /// way into every game, so the cursor vanished on every New game and every Load, on every
        /// branch that has the menu (owner, 2026-09-17: *"this is happening on other builds - did
        /// the new menus bust something? it used to highlight the wall immediately onto the
        /// placement area"*).</para>
        ///
        /// <para>Re-found on build as well as kept, so a session built after the component was
        /// added at runtime still has it.</para>
        /// </summary>
        void FindTheSiblingPresenters()
        {
            if (_designate == null) _designate = GetComponent<DesignatePresenter>();
        }

        /// <summary>
        /// Build a world and everything that draws it, now, rather than at <c>Start</c> (U35).
        ///
        /// <para><b>Why this is a method and not a lifecycle hook.</b> A menu has to be able to
        /// ask for a world after the scene is already running, and to put one down again without
        /// reloading the scene — that is the whole of the session seam. While the menu does not
        /// exist, <see cref="buildOnPlay"/> is true and pressing Play lands straight in a world,
        /// so every PlayMode test that assumed <c>Start</c> built one still passes unedited.</para>
        ///
        /// <para>Building twice without a teardown between is a caller error rather than a
        /// silently doubled world: the second call would leak the first world's meshes and
        /// figures, which is exactly the failure this unit exists to make impossible.</para>
        /// </summary>
        public void BuildSession() => BuildSession(null, null);

        public void BuildSession(uint? seedOverride, SaveHeader? from) =>
            BuildSession(seedOverride, from, null);

        public void BuildSession(uint? seedOverride, SaveHeader? from, uint[]? colonists) =>
            BuildSession(seedOverride, from, colonists, null, null);

        /// <summary>
        /// Give the colonists the names the player typed on the setup page, in slot order. Null,
        /// an empty list, or a null entry all mean "keep the name you were dealt".
        ///
        /// <para><b>A call of its own rather than a sixth parameter on <see cref="BuildSession"/>,
        /// and it has to run after it</b>: a name belongs to a colonist, and there are no
        /// colonists until the colony is built. It is also the only part of the start flow that is
        /// purely interface — the request carries seeds because the simulation rolls people from
        /// them, and it carries no names because the simulation has none.</para>
        ///
        /// <para><b>The seed is checked, not trusted.</b> Which pawn a slot becomes is
        /// <c>ColonistDraw.IdForSlot</c>'s to say — the rule that file exists to state once — but
        /// it rests on <c>ColonyScenario.Place</c> spawning the chosen colonists first and in
        /// order, which is two files away from here. So each name is placed only on a pawn whose
        /// roll seed is the one that slot was dealt, and a mismatch is reported rather than
        /// putting somebody's name on a stranger.</para>
        /// </summary>
        public void NameColonists(IReadOnlyList<string?>? names, IReadOnlyList<uint>? seeds = null)
        {
            if (names == null || _colony == null) return;

            for (int slot = 0; slot < names.Count; slot++)
            {
                string? typed = names[slot];
                if (string.IsNullOrEmpty(typed)) continue;

                PawnId id = ColonistDraw.IdForSlot(slot);
                Pawn? pawn = _colony.Pawns.Pawns.Get(id);
                if (pawn == null)
                {
                    Debug.LogWarning($"[Odyssey] no colonist in slot {slot} to call \"{typed}\"");
                    continue;
                }
                if (seeds != null && slot < seeds.Count && pawn.RollSeed != seeds[slot])
                {
                    Debug.LogWarning(
                        $"[Odyssey] slot {slot} was dealt seed {seeds[slot]} but pawn {id.Value} rolled " +
                        $"{pawn.RollSeed}; \"{typed}\" is not being given to somebody else");
                    continue;
                }

                ColonistNames.Book.Rename(id, typed);
            }
        }

        /// <summary>
        /// Build a session, optionally on a seed and a shape that are not the scene's (U38).
        ///
        /// <para><b>Both overrides exist for one caller each.</b> <paramref name="seedOverride"/>
        /// is New game: everything else about the request stays a default on the inspector, so
        /// size, map type and scenario remain tunable later without new interface, which is what
        /// the plan's U39 row asks for. <paramref name="from"/> is Load: a save refuses to open
        /// into a world of a different seed or size (<c>WorldSave.Load</c> checks both), so a
        /// loaded session is built from the header rather than from whatever the inspector happens
        /// to say — and if the header does not carry enough to rebuild the same world, the load
        /// fails loudly here rather than producing a world that quietly differs.</para>
        ///
        /// <para><paramref name="colonists"/> is the third caller, colonist select (U40): one roll
        /// seed per person the player kept. It decides <i>who</i> they are, and the scenario's own
        /// colonist count is overridden to match, because the owner's ruling is that a new game
        /// starts with exactly the three that were chosen. Null leaves both alone, which is every
        /// other caller.</para>
        ///
        /// <para><paramref name="name"/> and <paramref name="sizeOverride"/> are the setup page's
        /// other two knobs. Both are null for every caller that does not ask, and both are ignored
        /// on a load — a saved colony's name and board are facts about the file, and
        /// <c>WorldSave.Load</c> refuses a world of a different size anyway.</para>
        /// </summary>
        public void BuildSession(uint? seedOverride, SaveHeader? from, uint[]? colonists,
            string? name, GridSize? sizeOverride)
        {
            if (HasSession)
                throw new System.InvalidOperationException(
                    "a session is already built; call TeardownSession before building another");

            // Set before anything is meshed, because the relief is read at mesh time and a chunk
            // built flat would stay flat until something dirtied it. Statics, like the scatter
            // density beside them: the field has to be reachable from the mesher, the picker and
            // the figures alike, and it is a property of how the world is drawn rather than of any
            // one of them.
            GroundRelief.Amplitude = groundRelief;
            GroundRelief.Period = groundReliefPeriod;

            // **The board's size, decided once, before anything is built from it.**
            //
            // A loaded session takes its shape from the file, not from the inspector. WorldSave
            // refuses a save whose seed or size differs from the world it is opened into, so this
            // is not a convenience — it is the only way a load can succeed at all. Otherwise the
            // setup page's choice when the player made one, and the inspector's when they did not.
            //
            // <b>The setup page's choice used to be applied at the request and nowhere else</b>,
            // three lines further down, while the chunk grid and the render model below were built
            // from the inspector's numbers. So a new game on any board but the scene's default had
            // a mirror and a chunk grid of one size over a world of another, and every cell index
            // near the far edge landed outside them. Nothing had ever written to the chunk grid
            // during a world build, so it stayed silent until the scenario started raising beds
            // (2026-09-20) and three PlayMode tests threw `IndexOutOfRangeException` from
            // `ChunkGrid.MarkDirty` — with the bounds check one frame above it passing, because
            // it asked the *cell* grid. One rule, two owners, and the usual silence.
            GridSize size = from != null
                ? from.Size
                : sizeOverride ?? new GridSize(sizeX, sizeZ, layers);
            var chunks = new ChunkGrid(size);

            // The render model is built before the world, because the mirror the world publishes
            // through is built from it. It needs the size and the chunk grid and nothing else, so
            // none of it depends on a board that has not been generated yet.
            var library = new ModuleLibrary(moduleCatalogue);
            _model = new WorldRenderModel(size, chunks, library);
            WorldRenderModel model = _model;

            // One build, the same one the headless runs and every test use.
            //
            // This was forty lines of wiring re-typed here, and it had drifted from the original
            // twice over: it registered no connectors with the navigation graph, so a stair on a
            // city map joined no region; it never ran the full support solve; and it assembled no
            // save components, which is why the one world a player actually ran was the one world
            // that could not be written to a file.
            //
            // The prototype starts on empty natural ground and the colony builds from nothing
            // (ADR 0008). The ruined-city generator is still here and still tested; switch
            // mapType to reach it.
            ScenarioDef scenarioDef = ScenarioFor(from);

            // A new game starts with exactly the people that were chosen (U40, owner's decision 3).
            // The count is taken off the chosen list rather than written down again, so the screen
            // and the colony cannot come to disagree about how many there are — and a load leaves
            // it alone, because a saved colony's population is a fact about the file.
            if (colonists != null && colonists.Length > 0 && from == null)
                scenarioDef = scenarioDef.WithColonists(colonists.Length);

            uint sessionSeed = from != null ? from.Seed : seedOverride ?? seed;
            MapType sessionMap = from != null && from.Recipe.Map != MapType.Unknown
                ? from.Recipe.Map
                : mapType;
            var generation = Stopwatch.StartNew();
            ColonyWorld colony = ColonyWorld.Build(new ColonyRequest
            {
                // The one `size` decided above, which already folds in the setup page's choice,
                // the file's shape and the inspector's default. It was decided here once and the
                // chunk grid above was left on the inspector's — see the note up there.
                Size = size,
                Seed = sessionSeed,
                Scenario = scenarioDef,
                // From the file when loading, for the reason SaveRecipe.Barren sets out: three
                // different boards answer to MapType.Natural, and rebuilding the wrong one would
                // put a restored colony's camera on empty ground far from the colony.
                Barren = from != null ? from.Recipe.Barren : barrenMap,
                Wooded = from != null ? from.Recipe.Wooded : woodedMap,
                Map = sessionMap,
                Chunks = chunks,

                // A colony keeps the name it was saved under. Nothing names one yet — that is the
                // New game screen's, in U39 — so a fresh session takes the request's default and
                // only a loaded one carries a name here.
                // The file's when loading; the setup page's when the player typed one; the scene's
                // otherwise. An empty typed name is not a name, so it falls through rather than
                // making a colony called nothing.
                Name = from != null && from.Recipe.ColonyName.Length > 0
                    ? from.Recipe.ColonyName
                    : !string.IsNullOrWhiteSpace(name) ? name!.Trim() : DefaultColonyName,

                // Who they are (U40). Null for a loaded session, whose colonists come out of the
                // file with their seeds already on them, and for every caller that never asked.
                Colonists = from == null ? colonists : null,

                // Noon, and it belongs to the build rather than to a call after it: a colony that
                // starts at tick 0 starts at midnight, SimWorld.StartAtTick refuses a clock that
                // has already run, and the composition root ticks once below to publish a first
                // frame. That ordering has been got wrong here once already.
                StartTick = startHour >= 0 ? startHour * GameClock.TicksPerHour : 0,

                // Built from the grid the generator has just filled, which is why it is a factory
                // and not a ready-made contributor. It is registered ahead of the colony, so the
                // geometry a frame shows is the one that frame's pawns and orders were computed
                // against.
                Mirror = (grid, outcome) => new GridMirrorContributor(grid, outcome.Edifices, model),
            });
            generation.Stop();

            _colony = colony;
            _grid = colony.Grid;
            _pawns = colony.Pawns;
            _world = colony.World;
            MapGenOutcome outcome = colony.Outcome;

            // Shell templates only exist on a city map; natural ground has no stamped buildings.
            // After the build and before the first tick is the window: the mirror reads the model
            // when it publishes, not when it is made.
            if (outcome.City != null) _model.ApplyTemplates(outcome.City, colony.Gen);

            if (colony.Placement.Colonists == 0)
                Debug.LogError($"[Odyssey] no colonists were placed near {outcome.StartCell}: {colony.Placement}");

            // Nobody in this colony has a name of their own yet. Emptied here rather than in
            // ColonistNameSection.Apply, because Apply only runs for a file that carries the
            // section — so a colony whose people were never renamed would open wearing the last
            // colony's names, which is the same trap the view section records for the camera.
            ColonistNames.Book.Clear();
            if (colony.MarkedForWork > 0)
                Debug.Log($"[Odyssey] {scenarioDef}: {colony.MarkedForWork} cells marked for work before the first " +
                          $"tick — trees within {scenarioDef.startingFellRadius} cells of the start, and the nearest " +
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
            // for looking at what the palette does, which is what the owner was doing then — so the
            // roll came back behind a switch, defaulting on while the look was being judged.
            //
            // **That default is off since 2026-09-18**, and the portraits are what closed it. A
            // colonist is dealt from their own roll seed now, and the setup page photographs the
            // three candidates *before* a colony exists — so a session-wide roll, which is applied
            // as a pin when the world is built, would deal three different people the moment you
            // pressed Start. That is the owner's original complaint ("the colonists look nothing
            // like their profile picture") reappearing in a new form, and by construction rather
            // than by accident. The switch is still here and still does what it says; judging the
            // palette is now `Logs/portraits.png`, which shows more of the cast at once than
            // pressing Play repeatedly ever did.
            //
            // None of it enters the simulation and none of it is saved: nothing is stored, because
            // the same inputs are re-derived. See ColonistAppearance.
            // The *session's* seed, not the inspector's: a loaded colony has to deal the same
            // people it was saved with, and a new game on a rolled seed has to deal that seed's
            // people rather than seed 1's. Reading the field here would have made every world look
            // like the scene's default one, which is the sort of fault that reads as "the save
            // lost my colonists' faces".
            uint castSeed =
                colonistLookSeed != 0 ? (uint)colonistLookSeed :
                randomCastEachSession ? (uint)UnityEngine.Random.Range(1, int.MaxValue) :
                sessionSeed;
            ColonistAppearanceBook appearances = AppearanceBooks.For(castSeed, moduleCatalogue);

            // Since 2026-09-18 a colonist is dealt from *their own* roll seed, not the world's
            // (docs/design/20-avatars.md §5) — which is what lets a face on the setup screen be
            // the face the colony gives them. The cast seed above is then only the fallback, for a
            // save written before pawns carried one.
            //
            // So the two switches have to say so out loud or they would be inspector fields that
            // silently do nothing: either of them on means "overrule the pawns and deal the whole
            // colony from this number", which is exactly what both were for.
            appearances.Pinned =
                colonistLookSeed != 0 || randomCastEachSession ? castSeed : 0u;
            // One ink line in the game, not two. Characters draw their own hull because they are
            // absent from the depth texture the world's outline pass reads, so the colour and
            // width have to be copied across from the feature that inks everything else.
            ColonistMaterials.AdoptInkFrom();
            _colonistMaterials ??= new ColonistMaterials();

            // The photographer takes this colony's book and its materials, so a portrait, the
            // figure walking around and the baked instanced form are three drawings of one answer
            // — including the pinned cast above, which would otherwise show on the board and not
            // on the card.
            Portraits.Appearances = appearances;
            Portraits.Materials = _colonistMaterials;
            Portraits.Clear();

            Debug.Log($"[Odyssey] colonist cast seed {castSeed} over {appearances.LookCount} faces, " +
                      (colonistLookSeed != 0 ? "pinned by colonistLookSeed, overruling every pawn's own seed" :
                       randomCastEachSession ? "rolled for this session, overruling every pawn's own seed — copy it into colonistLookSeed to keep this cast" :
                       "the fallback only; every colonist is dealt from their own roll seed"));

            _renderer = new ChunkRenderer(_model)
            {
                CastShadows = castShadows,
                GameObjectLayer = gameObject.layer,
                ScatterDensity = grassScatter,
                Appearances = appearances,
            };
            // The power lines (design 32 §9): their own pass, outside the chunk meshes, so showing
            // and hiding them costs no re-mesh. A new session starts unwatched, because its view
            // store does.
            _powerLines = new PowerLinePass { GameObjectLayer = gameObject.layer };
            _watchingPower = false;
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

            // Over the preferences this component has held since it woke, not over fresh ones:
            // the same settings panel and the same key bindings serve every session.
            FindTheSiblingPresenters();
            Directors = new HudDirectors(size.SizeY, outcome.StartCell.Y, Preferences, Keys);
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

            if (_model != null)
            {
                _doors = new DoorDirector(_model, moduleCatalogue, transform, gameObject.layer);
                _corpses = new CorpseDirector(_model, moduleCatalogue, _figures, transform, gameObject.layer);
                // Blood (design 33 §10): what the seam hands on, drawn. It asks the corpses and
                // the figures where a fallen body lies, for the pool under it.
                _blood = new BloodDirector(_model, FindBody);
                _combatFeedback.Blood = _blood;
            }

            // Which family each weapon swings in (design 33 §5j), read once off the content, so a
            // figure can pick its clip row from the event's weapon without asking the simulation.
            if (_figures != null && _pawns != null)
                _figures.WeaponStyles = CombatPose.StylesOf(_pawns.Content.Items);

            // Which blows cut (design 33 §7d), read once off the same content, for the blood seam.
            _combatFeedback.BloodSides = CombatFeedback.BloodSidesOf(_pawns?.Content);

            // The fight's floating words, beneath the HUD's own tree (design 33 §1).
            UnityEngine.UIElements.VisualElement? hudRoot =
                GetComponent<UnityEngine.UIElements.UIDocument>()?.rootVisualElement;
            if (hudRoot != null) _floaterView = new Ui.CombatFloaterView(hudRoot);

            // The light through the day. It finds the scene's own sun rather than making one,
            // because the scene builder already places it and two directional lights is a
            // doubled key nobody would think to look for.
            Light? key = sun != null ? sun : FindKeyLight();
            if (daylightCycle && key != null)
            {
                _daylight = new DaylightDirector(key, RenderSettings.skybox);
                _daylight.Apply(_world.CurrentTick);
            }
            if (_figures != null)
            {
                _figures.BlowLanded += OnBlowLanded;
                _figures.LoadLifted += OnLoadLifted;
                _figures.LoadSet += OnLoadSet;
            }
            if (_renderer != null)
            {
                _renderer.FallingItems.ItemLanded += OnLoadSet;
            }

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

            // Mesh the whole board before the first drawn frame, ignoring the per-frame budget.
            //
            // **This is where the meshing stall is meant to be.** Every chunk of a new world is
            // never-meshed, so a budgeted first frame would draw almost nothing and the board would
            // arrive in instalments over several hundred frames while the player watched it build
            // itself. The player is already waiting here — 6c.6 measured 14.7 seconds of worldgen
            // in this very call on the Huge board — so one more pass costs them nothing they can
            // tell apart from the wait they are already in, and it buys a first frame that is
            // whole. Everything after this frame is budgeted (6c.7).
            if (_renderer != null && cameraRig != null)
                _renderer.PrimeAll(cameraRig.ActiveLayer, cameraRig.slice);

            SessionChanged?.Invoke();

            Debug.Log(
                $"[Odyssey] world {size} seed {sessionSeed} generated in {generation.ElapsedMilliseconds} ms. " +
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

        /// <summary>A load came up off the ground: the lighter of the two carry sounds, from the
        /// spot it was lying on.</summary>
        void OnLoadLifted(Vector3 from) => _audio?.PlayOneShot(SoundIds.CarryLift, from);

        /// <summary>And a load touched down: the heavier one, from where it landed.</summary>
        void OnLoadSet(Vector3 at) => _audio?.PlayOneShot(SoundIds.CarryDrop, at);

        void OnGameSpeedRequested(int speed)
        {
            if (_world == null) return;
            // Space toggles: asking for pause while already paused means "start again" — at the
            // speed the player was last running at, not at normal. SpeedControl owns that rule
            // and the memory behind it.
            int next = _speed.Resolve(speed, _world.GameSpeed);
            _world.Intents.Submit(new Intent(IntentKind.SetGameSpeed, default, next));
            _speedChangePending = true;
        }

        /// <summary>What an unpause comes back to. See <see cref="Odyssey.Hud.SpeedControl"/>.</summary>
        readonly Odyssey.Hud.SpeedControl _speed = new();

        bool _speedChangePending;

        /// <summary>
        /// The pointer's one owner. Not part of a session: the menu has a cursor too, and a
        /// teardown must not leave the player without one.
        /// </summary>
        readonly CursorDirector _cursor = new();

        void Update()
        {
            // Before the session guard, deliberately. There is a pointer on the main screen and
            // during a load, and both of them are this object's to set.
            UpdatePointerCursor();

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
                //
                // A question about a cell was once the only thing that could not wait. An order
                // cannot wait either, and that gap was real: a slab laid while paused sat in the
                // queue and drew nothing at all until the clock started (owner, 2026-09-17). The
                // player pauses in order to give orders, so both are settled here by republishing
                // the view over the same settled world — no tick, no system, no hash moved at a
                // boundary. PausedIntents.AppliesWhilePaused names the set and says why it is safe.
                if (_world.Intents.HasAnyPending(PausedIntents.AppliesWhilePaused))
                    _world.RepublishViews();
                if (_speedChangePending)
                {
                    _world.Tick();
                    _speedChangePending = false;
                }
            }

            ReportRejections();
            ConsiderAutosave();

            // The light follows the clock every frame, not every tick: at speed 3 several ticks
            // retire in one frame and the sky would step, and when the game is paused the hour
            // stops with it, which is right — a paused world should not go on getting dark.
            _daylight?.Apply(_world.CurrentTick);
        }

        /// <summary>
        /// The debug menu's day skip: spend this many real ticks right now, in one synchronous
        /// batch, and let the next frame's normal loop redraw what moved. About a fifth of a
        /// second for a whole day (the ten-day soak runs in under two), so it lands as one hitch
        /// rather than a freeze.
        ///
        /// <para><b>Why a method on the root and not an intent.</b> Ticking is this class's one
        /// job and the intent bus is drained <i>inside</i> a tick — a skip sent through it would
        /// ask the world to re-enter its own tick, and a warp is not state for the simulation to
        /// author anyway; it is the tester spending the same ticks the clock would have spent.
        /// Every tick skipped is an ordinary tick: colonists walk, eat, sow and harvest through
        /// it, the hash is taken at the same boundaries, and it works while paused, because the
        /// paused branch only refuses the <em>clock</em>, not the world. The crop's four-day
        /// wait is what this exists to skip (docs/design/22-growing.md §9): a day a press, and
        /// the stage changes arrive at the same hour of the day each time.</para>
        /// </summary>
        public void DebugSkipTicks(int count)
        {
            if (_world == null || count <= 0) return;
            _world.Tick(count);
            _daylight?.Apply(_world.CurrentTick);
        }

        /// <summary>
        /// The other half of the day skip (owner, 2026-09-19): skipping a WHOLE day lands where
        /// you started, and everything that happened in between - the harvest above all -
        /// happened inside the warp, unseen, so a field that ripes and ripes-again reads as
        /// "seeds back down before a harvest I never watched". Skipping to MORNING hands the
        /// clock back with a whole day ahead of it: the crops finish, the harvesters walk, the
        /// sowers kneel, all at watchable speed, and the skip-a-day row stays for the long haul.
        /// Morning is a sixth past midnight, ahead of the growth window's 15,000, so the day is
        /// seen whole.
        /// </summary>
        public void DebugSkipToMorning()
        {
            if (_world == null || Colony == null) return;
            int day = Colony.Pawns.Content.DayTicks;
            int morning = day / 6;
            int now = _world.CurrentTick % day;
            int skip = (morning - now + day) % day;
            if (skip == 0) skip = day;
            DebugSkipTicks(skip);
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
                // **The scene's own sun, and only that.** A light on a hidden, unsaved object
                // belongs to something that built a rig of its own — today that is
                // PortraitStudio's key light, which exists from the moment the setup page
                // photographs its first candidate, sits at 1.6 and is switched off except for the
                // instant a portrait is taken. Hand *that* to the daylight cycle and two things go
                // wrong at once: the world loses its sun, and every portrait is lit by a light the
                // clock has been recolouring and dimming behind the studio's back. Measured on
                // 2026-09-20: the portrait's brightness still tracked the time of day after the
                // studio had been made to own the ambient, and this was why.
                if (light.gameObject.hideFlags != HideFlags.None) continue;
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
            _designate = GetComponent<DesignatePresenter>();
            if (_designate != null) return;

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
            _designate = gameObject.AddComponent<DesignatePresenter>();

            Debug.LogWarning(
                "[Odyssey] This play scene was built before DesignatePresenter existed. It has " +
                "been added at runtime so the mining and felling keys (M, C, X) work, but the " +
                "scene is generated and is out of date: rebuild it with " +
                "Odyssey > Presentation > Build play scene.");
        }

        void LateUpdate()
        {
            // Above the guard below, because the menus are exactly the state the guard returns
            // on: no world, nothing rendered, and a title screen that still wants a bed under it.
            // Unscaled, because a fade that is part of the interface must not care that the game
            // behind it is paused or running at six times speed.
            _menuBed?.Sync(Time.unscaledDeltaTime, wanted: _world == null);

            if (_renderer == null || _model == null || _world == null) return;
            int activeLayer = cameraRig != null ? cameraRig.ActiveLayer : _world.Views.SliceLayer;
            SliceSettings slice = cameraRig != null ? cameraRig.slice : new SliceSettings();

            _frameTimer.Restart();
            System.Array.Clear(_sectionMs, 0, _sectionMs.Length);
            _sectionTimer.Restart();
            // The rig sits on the camera, so its position is the viewer's.
            if (cameraRig != null)
            {
                _renderer.ViewerPosition = cameraRig.transform.position;
                // And the figure director wants it for one decision of its own: which colonists
                // keep a live figure when there are more of them than the cap allows.
                if (_figures != null) _figures.ViewerPosition = cameraRig.transform.position;
            }

            // Before anything reads the mirror, because the picker reads it and a waiting order is
            // one of the things a click can land on (WorldRenderModel.SetSites). Until this line
            // existed a site was drawn and not clickable: over open air the ray found nothing in
            // the column and the layer was dead to every tool, which is what the cancel tool could
            // not cancel on 2026-09-18.
            _model.SetSites(_world.Views.Current.Sites);
            _model.SetLines(_world.Views.Current.Conduits);
            int movePerTick = MovePerTick;
            MarkSection(FrameSection.Mirror);

            // Before the world is submitted, because it decides how part of the world is drawn.
            // It reads the figures placed on the *previous* frame, which is the one frame of lag
            // this is worth: a tree fading a sixtieth of a second late is not observable, and
            // placing the figures first would mean drawing the world after the people in it.
            UpdateSightLines(_world.Views.Current, movePerTick);
            MarkSection(FrameSection.Sight);

            // The crop mirror: the meshed world must already know a crop ripened this
            // tick before the dirty chunk the simulation marked is rebuilt, or the field
            // would redraw one stage behind what the orders and the figures show.
            _model.UpdateCrops(_world.Views.Current.Plants);

            // The zone mirror for the same reason: the field's tilled ground must be in
            // place before the dirty chunk a designation marked is rebuilt, or a painted
            // field would show its rows one refresh behind its tint.
            _model.UpdateZones(_world.Views.Current.Zones);
            MarkSection(FrameSection.Mirror);

            // And the storage mirror, third and for the same reason: the chunk a store's drag
            // marked dirty is rebuilt below, and a mesher that had not yet heard about the zone
            // would bake the ground untinted and only wash it on the *next* thing that dirtied
            // that chunk — which might be never.
            _model.UpdateStorage(_world.Views.Current.Stores);

            if (_renderer != null)
            {
                _renderer.FallingItems.UpdateSnapshot(_world.Views.Current);
                _renderer.FallingItems.Advance(Time.deltaTime);
                _renderer.Render(activeLayer, slice);
            }
            MarkSection(FrameSection.World);

            // And then take the surround back out of it. The skirt is submitted from inside
            // ChunkRenderer.Render — deliberately, because it must go to the GPU before the board
            // does (see the comment there) — so it cannot be bracketed by a MarkSection of its
            // own. Charging it here keeps the two numbers separate without moving the submission.
            if (_renderer != null)
            {
                double surroundMs = _renderer.SurroundMs;
                _sectionMs[(int)FrameSection.Surround] += surroundMs;
                _sectionMs[(int)FrameSection.World] -= surroundMs;
            }

            // **Where every colonist is, bucketed, once.**
            //
            // Three things pose a pawn against its neighbours this frame — the live figures, the
            // baked far form and the stand-in load a carrier holds — and each of them used to
            // walk the whole colony per pawn to do it. One index, built here and handed to both
            // owners, is what makes that a neighbourhood lookup instead. Built from
            // `Views.Current`, which is the same snapshot all three of them read, so it cannot be
            // a frame out of step with what is being drawn.
            //
            // Before the figures, because they are the first to ask.
            _crowd.Rebuild(_world.Views.Current.Pawns);
            if (_renderer != null) _renderer.Crowd = _crowd;
            if (_figures != null) _figures.Crowd = _crowd;
            MarkSection(FrameSection.Crowd);

            // Figures first, because what they take is what the instanced pass must leave alone.
            // Their graphs advance on their own clock once played, so nothing is evaluated here.
            _figures?.Sync(_world.Views.Current, activeLayer, slice, _tickAlpha, movePerTick,
                Time.deltaTime);
            MarkSection(FrameSection.Figures);

            // Sound after the figures, so a blow that landed this frame sounds on the same frame
            // its chips fly. The listener is the camera (where the AudioListener lives) and the
            // ambience anchor is its focus, which sits down among the water rather than up where
            // the camera itself is.
            if (_audio != null)
                _audio.Sync(Time.deltaTime, _world.Views.Current,
                    cameraRig != null ? cameraRig.transform.position : transform.position,
                    cameraRig != null ? cameraRig.Focus : transform.position,
                    activeLayer);
            MarkSection(FrameSection.Audio);

            if (_actorMaterial != null)
                _renderer.RenderActors(_world.Views.Current, activeLayer, slice, _actorMaterial,
                    _tickAlpha, movePerTick, _figures?.Drawn, _figures);
            MarkSection(FrameSection.Actors);

            _doors?.Sync(_world.Views.Current, activeLayer, slice, Time.deltaTime, _audio);
            _corpses?.Sync(_world.Views.Current, activeLayer, slice, Time.deltaTime);
            MarkSection(FrameSection.Doors);

            DrawStandingOrders(_world.Views.Current);
            DrawZones(_world.Views.Current);
            DrawBuildingSites(_world.Views.Current);
            DrawToolPreview();
            // After everything that marks a cell and before the cursors, which are brackets and
            // not plates: the order marks, the cut and fill slabs and the drag preview are all
            // gathered by colour and go out as one instanced call each. They were one submission
            // per cell, counted nowhere - P10.
            _renderer.FlushCellPlates();
            // After the plates: the lines are drawn over everything and go last among the world's
            // overlays, before the brackets that are the pointer's.
            DrawPowerLines(_world.Views.Current, activeLayer);
            DrawSelectionCursor(_world.Views.Current, movePerTick);
            DrawDraftMarks(_world.Views.Current, movePerTick);
            // The lock-on ring under whoever the selection is attacking (design 33 §7b).
            DrawLockOnRings(_world.Views.Current, movePerTick, activeLayer, slice);
            // The fight (design 33 §1): a bar over the hurt and the drafted, the hostile marker,
            // then the moments since last frame, handed on once each, and the words they float.
            DrawCombatMarks(_world.Views.Current, movePerTick, activeLayer, slice);
            _combatFeedback.Floaters.Step(_world.Views.Current.Running ? Time.deltaTime : 0f);
            int bloodLowest = Mathf.Max(0, slice.LowestDrawnLayer(activeLayer, _model?.LowestOutdoorLayer ?? int.MaxValue));
            int bloodHighest = slice.HighestVisibleLayer(activeLayer, _world.Views.Current.Size.SizeY);
            // Blood ages to this tick before the frame's hits are handed on, so a mark made now is
            // born now; it is drawn after them, so a hit this frame throws its drops this frame.
            _blood?.Step(_world.Views.Current.Running ? Time.deltaTime : 0f, _world.Views.Current.Tick);
            _combatFeedback.Consume(_world.Views.Current, _world, _figures, _audio,
                bloodLowest, bloodHighest, _tickAlpha, ticksPerSecond);
            _blood?.Draw(_renderer, bloodLowest, bloodHighest);
            _floaterView?.Draw(_combatFeedback.Floaters,
                cameraRig != null ? cameraRig.GetComponent<Camera>() : null);
            MarkSection(FrameSection.Overlays);
            _frameTimer.Stop();
            _renderMs = _frameTimer.Elapsed.TotalMilliseconds;

            float frameMs = Time.unscaledDeltaTime * 1000f;
            _smoothedFrameMs = _smoothedFrameMs <= 0f ? frameMs : Mathf.Lerp(_smoothedFrameMs, frameMs, 0.05f);
            SampleFrameTimings();
            SampleTrace(frameMs);
        }

        /// <summary>
        /// Hand this frame to the trace, opening one if the session has not got one yet.
        ///
        /// <para>Opened here rather than at the end of <c>BuildSession</c> on purpose: by the time
        /// a frame has run, the settings are seeded, the renderer exists and the board is meshed,
        /// so the header describes the session the player is actually in rather than the one that
        /// was requested. It costs one null check a frame for the life of the session.</para>
        /// </summary>
        void SampleTrace(float frameMs)
        {
            if (!TraceEnabled || _world == null) return;

            if (_tracer == null)
            {
                _tracer = Diagnostics.PerfTracer.TryOpen(SectionNames, TraceEnvironment());
                if (_tracer == null)
                {
                    // One failed attempt is enough. Retrying every frame would turn a full disk
                    // into a stutter of its own, which is the diagnostic causing the fault.
                    TraceEnabled = false;
                    return;
                }

                _traceSections = new double[(int)FrameSection.Count];
                // The tick's own phase split, which PhaseTrace has been able to produce since the
                // tick benchmark was written and which nothing in the running game has ever read.
                _world.PhaseSink = _tracer.PhaseSink;
            }

            System.ReadOnlySpan<double> split = FrameSectionMs;
            for (int i = 0; i < _traceSections.Length && i < split.Length; i++)
                _traceSections[i] = split[i];

            var counters = new Odyssey.Hud.Diagnostics.FrameCounters(
                tick: _world.CurrentTick,
                speed: _world.GameSpeed,
                drawCalls: _renderer?.DrawCalls ?? 0,
                instances: _renderer?.InstancesDrawn ?? 0,
                chunks: _renderer?.ChunksDrawn ?? 0,
                cellPlates: _renderer?.CellPlatesDrawn ?? 0,
                remeshed: _renderer?.ChunksMeshedThisFrame ?? 0,
                materials: _renderer?.MaterialCount ?? 0,
                surroundBatches: _renderer?.Skirt.BatchesDrawn ?? 0,
                figures: _figures?.FigureCount ?? 0,
                pawns: _colony?.Pawns.Pawns.Count ?? 0,
                layer: cameraRig != null ? cameraRig.ActiveLayer : _world.Views.SliceLayer,
                // The daylight cycle's own comment nominates this as "the only real cost in the
                // cycle", and it runs in Update, which no FrameSection covers.
                probes: _daylight?.ProbeUpdates ?? 0);

            _tracer.Sample(Time.unscaledDeltaTime, frameMs, _smoothedGpuMs, _renderMs, _tickMs,
                _traceSections, counters);
        }

        /// <summary>
        /// Mark this moment in the trace. Returns the marker's number, or 0 if nothing is being
        /// written.
        /// </summary>
        public int MarkTrace(string note) => _tracer?.Mark(note) ?? 0;

        /// <summary>
        /// Close the trace this session is writing, leaving the session running.
        ///
        /// <para>The phase sink goes back to null first, because it belongs to the tracer and a
        /// simulation holding a disposed one would be recording into nothing. Turning tracing on
        /// again opens a new file rather than reopening this one — see the debug row.</para>
        /// </summary>
        public void StopTrace()
        {
            if (_tracer == null) return;
            if (_world != null) _world.PhaseSink = null;
            _tracer.Dispose();
            _tracer = null;
        }

        /// <summary>
        /// What the trace's header says about this machine and this session.
        ///
        /// <para><b>This is the half that makes two traces comparable</b>, and the reader refuses
        /// to diff two whose headers disagree on the things that would make a comparison a lie.
        /// <c>docs/process.md</c>: "a number in a doc names its machine and its date; a timing
        /// without either is a rumour."</para>
        ///
        /// <para>The graphics settings are walked through <c>SettingsDirector</c>'s own
        /// <c>Order</c> and <c>LadderOrder</c> arrays rather than listed here, so a setting added
        /// later appears in traces without anybody remembering to add it.</para>
        /// </summary>
        System.Collections.Generic.List<(string, string)> TraceEnvironment()
        {
            var pairs = new System.Collections.Generic.List<(string, string)>
            {
                ("started", System.DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture)),
                ("unity", Application.unityVersion),
                ("platform", Application.platform.ToString()),
                ("editor", Application.isEditor ? "yes" : "no"),
                ("gpu", SystemInfo.graphicsDeviceName),
                ("gpu_api", SystemInfo.graphicsDeviceType.ToString()),
                ("cpu", SystemInfo.processorType),
                ("cpu_threads", SystemInfo.processorCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("ram_mb", SystemInfo.systemMemorySize.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("screen", $"{Screen.width}x{Screen.height}"),
                ("fullscreen", Screen.fullScreenMode.ToString()),
                ("vsync", QualitySettings.vSyncCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("frame_cap", Application.targetFrameRate.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            };

            if (_model != null)
                pairs.Add(("board", $"{_model.Size.SizeX}x{_model.Size.SizeZ}x{_model.Size.SizeY}"));
            pairs.Add(("map", mapType.ToString()));
            pairs.Add(("barren", barrenMap ? "yes" : "no"));
            pairs.Add(("seed", _world != null
                ? _world.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : seed.ToString(System.Globalization.CultureInfo.InvariantCulture)));

            pairs.Add(("scatter", (_renderer?.ScatterDensity ?? grassScatter)
                .ToString(System.Globalization.CultureInfo.InvariantCulture)));
            pairs.Add(("surround", (_renderer?.Skirt.Enabled ?? terrainSkirt) ? "on" : "off"));
            pairs.Add(("tree_sector", Rendering.TerrainSkirt.TreeSectorMetres
                .ToString(System.Globalization.CultureInfo.InvariantCulture)));
            pairs.Add(("far_tree_sector", Rendering.TerrainSkirt.FarTreeSectorMetres
                .ToString(System.Globalization.CultureInfo.InvariantCulture)));
            pairs.Add(("tree_variants", Rendering.TerrainSkirt.TreeVariantSlots
                .ToString(System.Globalization.CultureInfo.InvariantCulture)));
            pairs.Add(("figure_cap", (_figures?.MaxFigures ?? 0)
                .ToString(System.Globalization.CultureInfo.InvariantCulture)));

            Odyssey.Hud.SettingsDirector? settings = Directors?.Settings;
            if (settings != null)
            {
                foreach (Odyssey.Hud.GraphicsOption option in Odyssey.Hud.SettingsDirector.All)
                    pairs.Add(("gfx." + option, settings.IsOn(option) ? "on" : "off"));
                foreach (Odyssey.Hud.GraphicsLadder ladder in Odyssey.Hud.SettingsDirector.AllLadders)
                    pairs.Add(("gfx." + ladder, settings.Value(ladder)
                        .ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }

            return pairs;
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

                var kind = (DesignationKind)orders[i].Kind;
                Color tint = OrderColour(kind);

                // One shape for every order, at whatever height the thing in the cell puts it —
                // WorldRenderModel.MarkHeight. Deconstruct had a whole-cell wash of its own until
                // 2026-09-20, because a floor plate under a wall is inside the wall; the owner
                // asked for it to "mark the tile for deconstruction instead like you would mark
                // in mining", and marking the wall's top face is what mining already does to rock.
                _renderer.DrawCellMark(cell, tint);

                if (orders[i].Progress > 0)
                    _renderer.DrawCellCut(cell, orders[i].Progress / 255f, CutColour);
            }
        }

        /// <summary>The colour a growing zone's whole-tile cover is drawn in — a dark worked-soil
        /// brown (owner, 2026-09-18: "make the entire tile brown so they can look like one patch
        /// and make it a darker brown"). Drawn as a full-cell cover, no inset, at a higher alpha
        /// than an order's mark, so the dirt texture underneath flattens into one patch; still no
        /// order's hue, so a field and an order never ask to be told apart by reading a
        /// tooltip.</summary>
        /// <summary>The colour of a sown cell's seed specks - pale enough to read as seed against the dark soil, and nothing else on the board's floor is white.</summary>
        public static readonly Color SeedSpeckColour = new Color(0.92f, 0.90f, 0.82f, 1f);


        /// <summary>
        /// Every growing-zone cell on a drawn layer, tinted.
        ///
        /// <para>The interim overlay until the crisp-bordered region shader of
        /// <c>09-ui-and-input.md</c> §4.6 — the same debt the stockpiles carry, and the same
        /// answer: paint the cells the player set aside so a field reads as one thing and not as
        /// a mystery patch of short carrots.</para>
        ///
        /// <para><b>A cell mark, not a cell shade, and the geometry is why.</b>
        /// <see cref="ChunkRenderer.DrawCellShade"/> fills its cell's whole volume, which is right
        /// for a deconstruct order standing in the wall it is taking apart — and on a zone cell,
        /// which is open air above the soil, it would draw a three-metre glass box standing over
        /// every row of the field. <see cref="ChunkRenderer.DrawCellMark"/>'s plate sits at the
        /// floor of that air cell, which is the ground surface: paint on the field, where the
        /// player's eye already is.</para>
        ///
        /// <para>Filtered to the drawn band rather than the active layer, for the reason
        /// <see cref="DrawStandingOrders"/> gives: the zone was painted where the player could
        /// see, and that is where it must be drawn.</para>
        /// </summary>
        /// <summary>Is this cell in the published zone channel? Binary, because the channel is
        /// ascending by cell index - see <c>GrowingZones.Contribute</c>.</summary>
        static bool ZoneHolds(System.ReadOnlySpan<ZoneView> zones, int cellIndex)
        {
            int lo = 0, hi = zones.Length - 1;
            while (lo <= hi)
            {
                int mid = (int)(((uint)lo + (uint)hi) >> 1);
                int at = zones[mid].CellIndex;
                if (at == cellIndex) return true;
                if (at < cellIndex) lo = mid + 1; else hi = mid - 1;
            }
            return false;
        }

        /// <summary>The same search over the crop channel, which is ascending for the same reason.</summary>
        static bool PlantStands(System.ReadOnlySpan<PlantView> planted, int cellIndex)
        {
            int lo = 0, hi = planted.Length - 1;
            while (lo <= hi)
            {
                int mid = (int)(((uint)lo + (uint)hi) >> 1);
                int at = planted[mid].CellIndex;
                if (at == cellIndex) return true;
                if (at < cellIndex) lo = mid + 1; else hi = mid - 1;
            }
            return false;
        }

        void DrawZones(WorldSnapshot snapshot)
        {
            if (_renderer == null || cameraRig == null) return;

            System.ReadOnlySpan<ZoneView> zones = snapshot.Zones;
            if (zones.Length == 0) return;

            GridSize size = snapshot.Size;
            int lowest = System.Math.Max(0, cameraRig.LowestSelectableLayer);
            int highest = cameraRig.HighestSelectableLayer;

            // The tilled ground itself is not drawn here any more and must not be again: it is
            // a bit on the terrain bucket's tint (TintCode.TilledBase), so the field is part of
            // the chunk mesh and costs a field nothing per frame. What is left in this method is
            // the seed, which is genuinely per-event and genuinely transient.

            // The seed the sower left (owner, 2026-09-18: "some kind of seed on the surface like
            // speckled white tiny dots to indicate it's sown"). A sown cell is a dark tile until
            // the sprout's first stage is big enough to read, so the sowing itself is invisible
            // for the first hours; the specks are the feedback, one handful per planted cell.
            // The seed day and the sprout only: the specks are the seed, and they germinate away
            // once there is a plant to see (owner, 2026-09-19: "the seeds should stay there at
            // first - the sprouting should appear after a day rather than immediately" - which
            // made the first day a stage of its own, nought, that draws specks and no plant).
            System.ReadOnlySpan<PlantView> planted = snapshot.Plants;
            for (int i = 0; i < planted.Length; i++)
            {
                if (planted[i].Stage > 1) continue;
                CellRef cell = size.FromIndex(planted[i].CellIndex);
                if (cell.Y < lowest || cell.Y > highest) continue;
                _renderer.DrawSeedSpecks(cell, SeedSpeckColour);
            }

            // And while the seed is still going in (owner, 2026-09-18: "seeds should appear
            // during when the colonist is on the ground for a little time, not after"). Gated on
            // the KNEEL and not the job: a sower walks to her plot inside the same job, and the
            // first version drew specks under her feet on every zoned tile she crossed - the
            // owner watched seeds appear on a tile she merely walked over. The kneel plays only
            // in the work toil, and a cell that already stands a plant draws its own specks (or
            // its plants) and none of these.
            //
            // And not from the first frame of the kneel either (owner, 2026-09-19: "it should
            // have a delay so the colonist is actually bent down for some time and seeds
            // appear"): the specks wait out <see cref="Gesture.SeedSpecksAfter"/>
            // of the kneel, measured on the serial's own clock below, so the ground stays bare
            // while she is only arriving at the soil.
            System.ReadOnlySpan<PawnView> pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                if (pawns[i].JobDef != JobIndex.Sow) continue;
                if (pawns[i].Gesture != PawnGesture.Sow) continue;
                float kneelAge = KneelAge(pawns[i].Id, pawns[i].GestureSerial);
                if (kneelAge < Gesture.SeedSpecksAfter) continue;
                CellRef at = pawns[i].Cell;
                if (at.Y < lowest || at.Y > highest) continue;
                int atIndex = size.Index(at.X, at.Z, at.Y);
                // Both channels are published in ascending cell order, so these are searches
                // and not sweeps. They were sweeps, which is O(sowers x zone cells) every frame
                // - nothing on a carrot plot and a real cost on the stockpile-sized zones this
                // same channel is about to carry.
                if (!ZoneHolds(zones, atIndex)) continue;
                if (PlantStands(planted, atIndex)) continue;
                // How long since the handful opened: the bundle shows at the hand, the specks
                // scatter and fall to their spots, and from then they are the seeds. The age
                // past its threshold is the drop's own clock, so nothing else is timed.
                _renderer.DrawSeedSpecks(at, SeedSpeckColour,
                    kneelAge - Gesture.SeedSpecksAfter);
            }

            // Both loops above only gather. One instanced call draws the lot: six cubes a cell
            // in one material, which used to be six submissions a cell.
            _renderer.FlushSeedSpecks(SeedSpeckColour);
        }

        /// <summary>
        /// When each pawn's current gesture began, on the frame clock: pawn id to the serial it
        /// was last seen wearing and the time that serial was first seen in. The same
        /// serial-differs test the figure director uses to fire a pose, answering a lazier
        /// question — not "did it just begin" but "has it been running this long" — which is
        /// what a drawn effect that should wait out part of a hold needs and no snapshot field
        /// carries. Never cleared: a stale entry costs one dictionary slot per pawn the colony
        /// has ever had, and a pawn that kneels again bumps its serial and re-times itself.
        /// </summary>
        readonly Dictionary<int, (byte Serial, float Started)> _gestureBegan = new();

        /// <summary>
        /// How long this pawn's current gesture has been running, in seconds — or -1 on
        /// the first frame a serial is seen, the figure-director rule that keeps a colonist
        /// walking into view from playing a gesture it never made. The seed specks read it
        /// twice: once against <see cref="Gesture.SeedSpecksAfter"/> for the delay the kneel
        /// is owed, and once past it, as the drop's own clock.
        /// </summary>
        float KneelAge(PawnId pawn, byte serial)
        {
            int id = pawn.Value;
            if (!_gestureBegan.TryGetValue(id, out var began) || began.Serial != serial)
            {
                _gestureBegan[id] = (serial, Time.time);
                return -1f;
            }
            return Time.time - began.Started;
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
        /// <para><b>A ghost of the finished thing, since 2026-09-17.</b> This used to be a mark and
        /// a fill and nothing else, and the reason written here was that "a ghost wants the mesher
        /// to place a module it has not been asked for". That reason has gone:
        /// <see cref="ChunkRenderer.DrawGhost"/> was built for the build cursor and places a module
        /// without touching the mesher or a chunk batch at all. The owner asked for the two to
        /// match — *"keep the same selection tool graphics, wall stays the wall"* — and they are
        /// now literally the same call, so what you saw under the pointer is what stands on the
        /// board while it waits.</para>
        ///
        /// <para>The mark stays underneath it. A ghost says <em>what</em> and the plate says
        /// <em>which cell</em>, which is the thing that reads from directly above when a ghost is
        /// foreshortened to nothing.</para>
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

                // **The shape and the progress, and nothing else** (owner, 2026-09-18: "just the
                // shape/outline of what is going to be built because it's difficult to visualize
                // anything and just adds noise"). A waiting order used to carry a cell outline, a
                // filled cell mark, the ghost and a progress bar — four overlapping things on one
                // cell, three of which described the *cell* rather than the thing going in it.
                //
                // The two-cell mark went with them and needs no replacement: a bed's ghost spans
                // both of its cells on its own, which is what makes the footprint legible now.
                DrawSiteGhost(cell, sites[i].Building, sites[i].Stuff, sites[i].Facing);

                // **A slab does not rise, so it must not be drawn rising** (owner, 2026-09-17:
                // "these little gaps or white lines appearing on the builds"). DrawCellFill grows
                // a bone-coloured box out of the cell floor because that is how a wall is built —
                // and a floor's cell floor is exactly the plane the finished slab occupies, so a
                // slab a few per cent built is a thin pale plate lying in the deck, reading as a
                // bright hairline between the boards around it. Two contact sheets came back clean
                // trying to reproduce it, and that was the evidence: both stamped finished slabs
                // with no site in progress anywhere.
                //
                // The ghost above already says what is coming and where. A slab's progress is the
                // one thing left unsaid, and a wrong picture is worse than none until there is a
                // right one.
                if (sites[i].Progress > 0 && !ConstructionContent.BuildingAt(sites[i].Building).slab)
                    _renderer.DrawCellFill(cell, sites[i].Progress / 255f, FrameColour);
            }
        }

        /// <summary>
        /// The waiting thing, drawn as the thing it will be.
        ///
        /// <para>The same module, tint and placement the build cursor uses, so a wall ordered looks
        /// like the wall the cursor promised and a slab like the slab. Fainter than the cursor:
        /// the cursor follows the pointer and has to be found instantly, while a site sits on the
        /// board for as long as it takes a colonist to walk over, and a colony of them at cursor
        /// weight would read as a finished town.</para>
        /// </summary>
        /// <summary>
        /// Would the simulation refuse a cell of this run? Asked of the grid that will land the
        /// order, never worked out here — the same rule the cursor has followed since the build
        /// cursor was written, and the reason it cannot come to disagree with what happens on the
        /// click.
        ///
        /// <para>Per cell rather than per run. A drag across a hillside is usually part legal, and
        /// a whole run painted red because one end of it is rock says less than the cells
        /// themselves do.</para>
        /// </summary>
        bool Refused(CellRef cell, int building) => Refused(cell, building, facing: 0);

        /// <summary>
        /// The same question for a thing that is more than one cell: <b>every</b> cell it would
        /// occupy has to take it, not just the one under the pointer.
        ///
        /// <para><b>The guard was in the simulation and not on the screen</b>, which is the half
        /// the player meets. <c>ConstructionGrid.Place</c> derives the far cell from the facing and
        /// refuses the order when anything is standing in it — correct, tested, and completely
        /// invisible: the ghost asked only about the head cell, so a bed whose far half was in a
        /// wall drew in its own material like any legal order, and clicking it did nothing at all.
        /// A click that silently does nothing is indistinguishable from a click that missed, which
        /// is how "it doesn't respect where I placed it" gets reported (owner, 2026-09-18).</para>
        ///
        /// <para>The footprint is derived by <c>EdificeFootprint</c> — the simulation's own rule,
        /// asked rather than restated, so the ghost cannot come to disagree with the order about
        /// which cells a thing claims. That disagreement is the fault this line of work has hit
        /// three times (`19-build-cursor.md` §6).</para>
        /// </summary>
        bool Refused(CellRef cell, int building, int facing)
        {
            ConstructionGrid? sites = _colony?.Construction;
            if (sites == null || _grid == null) return false;

            int index = _grid.Index(cell);
            if (!sites.Allows(index, building)) return true;

            BuildingDef def = ConstructionContent.BuildingAt(building);
            if (def.footprint <= 1) return false;

            int second = EdificeFootprint.SecondCell(index, def.edifice, facing, _grid.Size);
            return second < 0 || !sites.Allows(second);
        }

        void DrawSiteGhost(CellRef cell, int building, int stuff, int facing = 0, bool refused = false)
        {
            if (_renderer == null || _model == null || _grid == null) return;
            if (!ConstructionContent.IsBuilding(building)) return;

            int index = _grid.Index(cell);
            BuildingDef what = ConstructionContent.BuildingAt(building);

            // A line has no module — it is not a thing standing in the cell but a rod through it
            // (design 32 §9) — so its ghost is the cell's plate in the build accent, or red where
            // it would be refused: the same plate an ordered line's run is judged by.
            if (what.conduit)
            {
                _renderer.DrawCellMark(cell, refused
                    ? PreviewRefusedColour
                    : Ui.HudTokens.Convert(OrderColours.Cursor(DesignateTool.Build)));
                return;
            }

            ushort material = ConstructionContent.StuffAt(stuff).stuff;
            int module = GhostModuleFor(index, what, material);

            // Its own material when it can be built and red when it cannot — the ghost is the only
            // thing drawn now, so it is also the only thing left to carry the refusal (owner,
            // 2026-09-18). Green is deliberately still not used for yes: looking right IS yes.
            Color tint = refused ? PreviewRefusedColour : StuffPalette.For(material, overArt: true);
            tint.a = SiteGhostAlpha;

            DrawThingGhost(module, what, tint, cell, facing);
        }

        /// <summary>
        /// The armed or waiting thing, drawn where it will stand — one module in a cell, or a
        /// bed's own three boxes across the two cells its facing claims.
        ///
        /// <para><b>The bed is why this is not one line.</b> Every other buildable fills its cell,
        /// so a module dropped on the cell floor is the whole picture. A bed is two cells long and
        /// low, and drawn as a single cell-filling cube it said nothing about either — and turning
        /// it with <b>R</b> changed nothing the player could see, because a cube looks the same
        /// all four ways round. <c>BedShape</c> is the mesher's own geometry, asked here so the
        /// ghost and the built thing cannot come to disagree.</para>
        /// </summary>
        void DrawThingGhost(int module, BuildingDef what, Color tint, CellRef cell, int facing)
        {
            if (_renderer == null) return;

            if (what.edifice == CoreContent.EdificeBed)
            {
                Matrix4x4 root = BedShape.Root(cell.X, cell.Z, cell.Y, facing);
                for (int part = 0; part < BedShape.PartCount; part++)
                {
                    // The pillow keeps its own module and its own colour in the ghost too, or the
                    // cursor would promise a bed it is not about to build.
                    bool pillow = BedShape.IsPillow(part);
                    Color partTint = tint;
                    if (pillow)
                    {
                        Color linen = StuffPalette.Linen;
                        partTint = new Color(linen.r, linen.g, linen.b, tint.a);
                    }

                    _renderer.DrawGhost(
                        pillow && _model != null ? _model.BedPillowModule : module,
                        partTint, BedShape.Part(root, facing, part));
                }
                return;
            }

            // Power's machines, when their art resolved: once, at the middle of the footprint,
            // turned — PropShape, the mesher's own answer (design 32 §14).
            if ((what.edifice == CoreContent.EdificeGenerator || what.edifice == CoreContent.EdificeHeater)
                && _model != null && _model.Library[module].Shape == ModuleShape.Pillar)
            {
                int drawn = what.edifice == CoreContent.EdificeHeater
                    ? _model.BackedFacing(_model.Size.Index(cell.X, cell.Z, cell.Y), facing) : facing;
                _renderer.DrawGhost(module, tint, PropShape.Root(cell.X, cell.Z, cell.Y, drawn, what.footprint));
                return;
            }

            // The shelf's ghost, from the shelf's own shape — the same three boxes the mesher
            // draws, so the thing under the pointer and the thing on the board cannot disagree
            // about where a shelf stands in its cell. It stands against the back of the cell, so
            // that disagreement would be visible rather than subtle.
            if (what.edifice == CoreContent.EdificeShelf)
            {
                Matrix4x4 shelf = ShelfShape.Root(cell.X, cell.Z, cell.Y, facing);
                for (int part = 0; part < ShelfShape.PartCount; part++)
                    _renderer.DrawGhost(module, tint, ShelfShape.Part(shelf, facing, part));
                return;
            }

            // A ladder's ghost stands on the face the built one will stand on: the wall it would be
            // fixed to if there is one, and the rotation the player has turned it to if there is
            // not. Asked of the model rather than worked out here, because that rule has one owner
            // and two systems have already disagreed about it once.
            Matrix4x4 placed;
            if (what.edifice == CoreContent.EdificeDoor && _model != null)
            {
                int dir = _model.DoorFacing(cell.X, cell.Z, cell.Y, facing);
                placed = GroundRelief.Drape(CellMetrics.FaceCentre(cell.X, cell.Z, cell.Y, dir)) *
                         Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[dir], 0f));
            }
            else
            {
                placed = GroundRelief.Drape(CellMetrics.FloorCentre(cell.X, cell.Z, cell.Y));
                if (what.edifice == CoreContent.EdificeLadder && _model != null && _grid != null)
                    placed *= Matrix4x4.Rotate(Quaternion.Euler(
                        0f, Directions.Yaw[_model.LadderFacing(_grid.Index(cell), facing)], 0f));
            }

            _renderer.DrawGhost(module, tint, placed);
        }

        /// <summary>
        /// The things a pending run would build, drawn as themselves inside its box.
        ///
        /// <para><b>The wireframe alone was a step backwards</b> (owner, 2026-09-17: *"it switches
        /// back to square"*). Moving the pointer with a tool armed shows a ghost of the thing; the
        /// moment a run was anchored that became a bare box, so the player lost sight of what they
        /// were placing at exactly the point they were deciding how much of it to place.</para>
        ///
        /// <para><b>Capped, because a drag can cover the board.</b> Beyond the cap the box alone is
        /// the honest summary — a thousand translucent walls would be a wall of fog, and one
        /// instanced submission each is a cost worth bounding on a frame that is already drawing a
        /// preview every frame of a drag.</para>
        /// </summary>
        void DrawRunGhosts(PreviewBox box, int building, int stuff, int facing)
        {
            if (box.Cells > MaxRunGhosts) return;

            // A multi-cell thing is one ghost, not one per cell. Its preview box IS its footprint
            // — the director draws the shape of the thing rather than the shape of the drag — so
            // walking the box would stamp a whole bed in each of the two cells it occupies.
            // The head is whichever end the facing points away from.
            if (BuildShapes.CellsOf(building) > 1)
            {
                var head = new CellRef(
                    facing == 3 ? box.Max.X : box.Min.X,
                    facing == 2 ? box.Max.Z : box.Min.Z,
                    box.Min.Y);
                DrawSiteGhost(head, building, stuff, facing, Refused(head, building, facing));
                return;
            }

            // **Drawn on the layer the run will actually land on**, which until 2026-09-18 it was
            // not: these ghosts were stamped at the box's own Y with no lift at all, while the
            // order lifted each cell separately on its way in. So the preview showed one storey,
            // the sites appeared on another, and where the lift disagreed cell by cell the player
            // got a hole. One owner for the answer — the same one the order asks.
            int runY = box.Min.Y;
            if (_colony?.Construction is { } sites && _grid != null)
            {
                _runCells.Clear();
                for (int z = box.Min.Z; z <= box.Max.Z; z++)
                for (int x = box.Min.X; x <= box.Max.X; x++)
                    _runCells.Add(new CellRef(x, z, box.Min.Y));
                runY = sites.RunLayerFor(_runCells, building);
            }

            // The facing goes to a one-cell thing too, since 2026-09-18: a ladder rotates now, and
            // a ghost that would not turn is a player pressing R and seeing nothing happen — the
            // exact complaint the rotation was added to answer.
            for (int z = box.Min.Z; z <= box.Max.Z; z++)
            for (int x = box.Min.X; x <= box.Max.X; x++)
            {
                var at = new CellRef(x, z, runY);
                DrawSiteGhost(at, building, stuff, facing, Refused(at, building));
            }
        }

        /// <summary>
        /// How many cells of a pending run are drawn as the thing rather than as a box. A run
        /// longer than this is being judged by its extent, not by its contents.
        /// </summary>
        const int MaxRunGhosts = 64;

        /// <summary>Scratch for the run's cells, reused so a preview allocates nothing per frame.</summary>
        readonly List<CellRef> _runCells = new List<CellRef>();

        /// <summary>
        /// Which module stands in for a thing that is not there yet.
        ///
        /// <para>Its own, for everything except a wall. A ladder ghosts as a ladder and a door as a
        /// door, which is the whole of what the owner asked for — *"wall stays the wall, and same
        /// goes for floor, slab and anything else"*.</para>
        ///
        /// <para><b>A wall is the exception and stays one.</b> A finished wall is drawn as panels on
        /// whichever faces something can be seen through, chosen from what stands beside it — and a
        /// ghost has no neighbours, because it is not in the grid. Reproducing that choice for a
        /// thing that does not exist would be a second copy of the mesher's hardest rule.
        /// <c>WallCore</c> is the cell-filling block and is exactly "a wall-shaped thing of this
        /// material" (`19-build-cursor.md` §2).</para>
        /// </summary>
        int GhostModuleFor(int index, BuildingDef what, ushort stuff)
        {
            if (_model == null) return 0;
            // The material, not just the handle: a slab's art now varies by what it is made of,
            // and a ghost that showed the wood deck for a stone order would be lying about the
            // one thing the cursor exists to say.
            if (what.slab) return _model.SlabModuleFor(index, stuff);
            return what.edifice == CoreContent.EdificeWall
                ? _model.WallCoreModule
                : _model.ModuleForEdificeAt(index, what.edifice);
        }

        /// <summary>
        /// How solid a waiting site is. Half the cursor's, for the reason above: one of these
        /// follows your pointer and a hundred of them sit on the board at once.
        /// </summary>
        const float SiteGhostAlpha = 0.22f;

        /// <summary>
        /// Say out loud when the simulation refuses a command.
        ///
        /// <para><b>Nothing in the build read <c>Intents.Rejected</c> at all</b>, although
        /// <c>Intents.cs</c> has said since it was written that "a command that silently does
        /// nothing is the worst possible outcome for a player". Every refusal in the game was
        /// therefore invisible: the build pipeline shipped with no handler for
        /// <c>PlaceBuilding</c>, every order came back <c>UnknownIntent</c>, and the only evidence
        /// available to anyone was that walls did not appear. Two playtests were spent on that.</para>
        ///
        /// <para><b>A log line, not an alert.</b> A refusal is usually correct and usually
        /// expected — a box dragged over a hillside is meant to contain cells that cannot be mined,
        /// and the rejection is silent and right. What is wanted is not a warning in the player's
        /// face but a record a developer can read afterwards, which is exactly what the console
        /// is for. The alerts panel stays for things the colony needs a decision about.</para>
        ///
        /// <para>Grouped and throttled, or a 400-cell drag writes 400 lines and the one that
        /// matters scrolls away. One line per (command, reason) per second, with the count.</para>
        /// </summary>
        void ReportRejections()
        {
            if (_world == null) return;
            var rejected = _world.Intents.Rejected;
            if (rejected.Count == 0) return;

            for (int i = 0; i < rejected.Count; i++)
            {
                RejectedIntent r = rejected[i];

                // AlreadyInThatState is the ordinary answer to marking the same cell twice, which
                // a drag does constantly. It is never the reason a feature does not work.
                if (r.Reason == IntentRejection.AlreadyInThatState) continue;

                long key = ((long)r.Intent.Kind << 32) | (uint)r.Reason;
                if (_rejectionCounts.TryGetValue(key, out int count)) _rejectionCounts[key] = count + 1;
                else _rejectionCounts[key] = 1;
            }

            // Emptied every frame, whether or not this is the frame that logs. Nothing in the
            // build had ever cleared it, so the list grew for the life of the session and each
            // frame re-counted the whole of it: one refused spawn was reported as 1,293 of them
            // a few seconds later, and the number said "a loop is submitting this" when the truth
            // was one click.
            _world.Intents.ClearRejected();

            if (Time.unscaledTime - _lastRejectionReport < 1f || _rejectionCounts.Count == 0) return;
            _lastRejectionReport = Time.unscaledTime;

            foreach (var pair in _rejectionCounts)
            {
                var kind = (IntentKind)(pair.Key >> 32);
                var reason = (IntentRejection)(uint)pair.Key;
                Debug.LogWarning($"[Odyssey] the simulation refused {pair.Value} x {kind}: {reason}" +
                    (reason == IntentRejection.UnknownIntent
                        ? " — nothing in the colony handles this command, which is a composition " +
                          "fault rather than a rule (see ColonyComposition.AddColony)"
                        : string.Empty));
            }

            _rejectionCounts.Clear();
        }

        readonly Dictionary<long, int> _rejectionCounts = new Dictionary<long, int>();
        float _lastRejectionReport;

        /// <summary>
        /// The box the player is dragging right now, before they let go.
        ///
        /// <para><b>Nothing drew this at all, and it is the whole of the owner's report that
        /// dragging a wall over the meadow did nothing</b> (2026-09-17). The order was placed
        /// correctly on release — measured — but between the press and the release the board
        /// looked exactly as it had before, so there was no way to tell a tool that was working
        /// from one that was not, and no way to see what a box was going to cover before
        /// committing to it.</para>
        ///
        /// <para>Drawn from the director's own <c>TryPreview</c>, which was written for this and
        /// had never been called. That is what stops the preview and the order disagreeing: they
        /// are the same object's answer to "which cells does this box cover", a frame apart.</para>
        ///
        /// <para><b>Green for a build and the tool's own colour otherwise.</b> Green because it is
        /// the colour of a thing about to be added and nothing else on the board uses it, and
        /// because it is what the owner asked for by name.</para>
        /// </summary>
        void DrawToolPreview()
        {
            if (_renderer == null || _designate == null) return;

            DesignateDirector director = _designate.Director;
            if (!director.TryPreview(out CellRef min, out CellRef max))
            {
                DrawHoverGhost(director);
                return;
            }

            // **A build drag draws the things, and only the things** (owner, 2026-09-18: "lets not
            // print the cursor, just the shape/outline of what is going to be built because it's
            // difficult to visualize anything and just adds noise").
            //
            // It used to draw a closed box over the whole run as well, one per layer where the run
            // steps up a riser, and that box was itself the answer to an earlier report — the
            // cursor being invisible. The two asks are not in conflict: what was missing then was
            // any promise of *what* would be built, and the ghosts are that promise. Once they
            // existed the box was a second outline of the same thing, drawn in a different colour,
            // one cell bigger than the wall it contained.
            //
            // The area tools keep their per-cell plate (the owner's own call, same day): a mine
            // order is paint on a face that is already there, so there is no thing to ghost.
            if (director.Tool == DesignateTool.Build)
            {
                _previewLayer = min.Y;
                _previewIsSlab = ConstructionContent.BuildingAt(director.Building).slab;
                BuildPreview.Gather(min, max, _previewLayerAt ??= PreviewLayerAt, _previewBoxes);

                for (int i = 0; i < _previewBoxes.Count; i++)
                    DrawRunGhosts(_previewBoxes[i], director.Building, director.Stuff, director.Facing);

                return;
            }

            // The order tools' own colours, below the build branch rather than above it, because
            // build no longer has one: a ghost is tinted by its material or by its refusal, so a
            // green arm here would be a colour nothing reads.
            //
            // One line, and the mapping is `OrderColours`' rather than this file's. It was a
            // switch here with four constants below it, and it disagreed with the palette chip on
            // two of the four — see that class for the whole of why.
            Color tint = Ui.HudTokens.Convert(OrderColours.Cursor(director.Tool));

            for (int z = min.Z; z <= max.Z; z++)
            for (int x = min.X; x <= max.X; x++)
                _renderer.DrawCellMark(new CellRef(x, z, min.Y), tint);
        }

        /// <summary>
        /// The thing under the pointer, before any button has been pressed.
        ///
        /// <para><b>This is the answer to "I don't know what I'm going to build is going to
        /// land"</b> (owner, 2026-09-17). Until now nothing at all was drawn between arming a tool
        /// and pressing: the board looked exactly as it had, and a player found out where a wall
        /// went by placing one. See `19-build-cursor.md`.</para>
        ///
        /// <para><b>Only for a build tool.</b> Mine, chop and cancel are verbs applied to what is
        /// already there — the thing they act on is drawn, and a ghost of it would be a second copy
        /// of something the player is already looking at.</para>
        ///
        /// <para>The cell comes from <see cref="DesignateDirector.Hover"/> rather than being worked
        /// out here, so the ghost and the order it promises are the same object's answer.</para>
        /// </summary>
        void DrawHoverGhost(DesignateDirector director)
        {
            if (_renderer == null || _model == null || _grid == null)
            {
                WhyNoCursor("the session's renderer, render mirror or grid is missing");
                return;
            }
            if (director.Tool != DesignateTool.Build)
            {
                WhyNoCursor($"no build tool is armed (tool is {director.Tool})");
                return;
            }

            // Split from the line above rather than folded into it with `||`: definite assignment
            // across a short-circuit and a negated pattern is the sort of thing that compiles on
            // one C# version and not the next, and the owner has the editor open on this worktree.
            if (director.Hover is not CellRef hover)
            {
                WhyNoCursor(cameraRig != null && cameraRig.PointerWasOverInterface
                    ? "the interface claims the pointer, so the rig raises no hover — "
                      + "something in the HUD is picking over the whole screen"
                    : "the pointer is over no cell, so the rig raises no hover");
                return;
            }

            ConstructionGrid? sites = _colony?.Construction;
            if (sites == null)
            {
                WhyNoCursor("the session has no construction grid");
                return;
            }

            // Where the order would land, asked of the grid that will land it rather than guessed:
            // a wall is lifted onto the ground it was clicked on, a slab onto whatever fills the
            // cell, and paving into the air over the block. WhereItWouldLand is that one answer.
            int cell = sites.WhereItWouldLand(_grid.Index(hover), director.Building);
            CellRef at = _grid.Size.FromIndex(cell);

            // The whole footprint, not the cell under the pointer: a bed whose far half is in a
            // wall is a refused order and has to look like one before it is given, or the click
            // does nothing and the player is left to guess why. See the Refused overload.
            bool allowed = !Refused(at, director.Building, director.Facing);
            BuildingDef what = ConstructionContent.BuildingAt(director.Building);

            // **Inside something is still an answer, and now it is the thing itself in red**
            // (owner, 2026-09-18: the ghost is the only thing drawn, so it is the only thing left
            // to carry a refusal).
            //
            // This spot has had three answers and the history is worth keeping, because two of
            // them were wrong in opposite directions. StandingOn lifts a wall order over solid
            // terrain but not over an edifice, so pointing a ladder at a wall resolves to the
            // wall's own cell and the ghost was drawn *inside* it (owner, 2026-09-17: "the ladder
            // placement does appear inside the walls, which is odd"). The first fix drew nothing
            // at all, which was worse: hover draws one thing, so the pointer went blank over every
            // wall and the player got no answer to "can I build here". The second drew a red cell
            // box instead of the thing — correct, and one of the cell-shaped overlays this pass
            // has just been asked to remove.
            //
            // So: draw the thing, in red. A red ladder standing in a wall is odd-looking and says
            // exactly what is true — that is where the order would go, and it would be refused.
            // A line's cursor is its cell's plate (design 32 §9): it has no module to ghost, and
            // without this the pointer went blank and the log said "no module resolved" every time
            // the line tool crossed a new cell.
            if (what.conduit)
            {
                _renderer.DrawCellMark(at, allowed
                    ? Ui.HudTokens.Convert(OrderColours.Cursor(DesignateTool.Build))
                    : PreviewRefusedColour);
                WhyNoCursor(null);
                return;
            }

            ushort material = ConstructionContent.StuffAt(director.Stuff).stuff;
            int module = GhostModuleFor(cell, what, material);

            // ChunkRenderer.DrawGhost returns on module <= 0 and says nothing, which is the last
            // silent way for this cursor to vanish.
            if (module <= 0)
            {
                WhyNoCursor($"no module resolved for building {director.Building} at cell {at}");
                return;
            }

            // Its own material when it can be built, which is the affirmative signal - it looks
            // like the wooden wall you asked for - and red when it cannot. Green is deliberately
            // not used for yes: looking right IS yes.
            Color tint = allowed
                ? StuffPalette.For(material, overArt: true)
                : PreviewRefusedColour;
            tint.a = GhostAlpha;

            // Draped, like everything fixed to the grid, and placed where the mesher would put it.
            // The facing is the director's, so R turns the thing under the pointer.
            DrawThingGhost(module, what, tint, at, director.Facing);

            // Drawn — so if it still cannot be seen, it is being drawn somewhere the player is not
            // looking. The owner's own guess (2026-09-17: "maybe it's a depth issue?") is the one
            // thing the guards above cannot answer, because a ghost drawn at the wrong layer looks
            // exactly like a ghost not drawn at all. Reported on change, so moving the pointer
            // across a flat field says this once.
            WhyNoCursor(null);
            ReportCursorLayer(hover, at, director);
        }

        /// <summary>
        /// Say once why the build cursor is not being drawn, and say it again only when the answer
        /// changes.
        ///
        /// <para><b>This exists because the cursor went missing and could not be found by reading.</b>
        /// The owner reported it after loading a game (2026-09-17); the rig's hover branch, the
        /// preview gate, the director's state machine and the session's field lifecycle were all
        /// walked through and all of them were sound, which is the point at which
        /// <c>docs/lessons.md</c> says to stop reasoning and measure. Every early return in
        /// <see cref="DrawHoverGhost"/> was silent, so a cursor that did not appear looked
        /// identical whichever of five reasons was the true one.</para>
        ///
        /// <para>Rate-limited by the message rather than by a timer: a reason that holds for a
        /// thousand frames logs once, and the log is the transition. Passing null means the cursor
        /// drew, which arms the next report.</para>
        /// </summary>
        void WhyNoCursor(string? reason)
        {
            if (reason == _lastCursorComplaint) return;
            _lastCursorComplaint = reason;
            if (reason != null) Debug.Log($"[Cursor] no build cursor: {reason}");
        }

        string? _lastCursorComplaint;

        /// <summary>
        /// Where the cursor is being drawn, against where the camera is slicing.
        ///
        /// <para>The cell the pointer is over, the cell the order would land in, the layer the ghost
        /// is drawn at, and the layer the rig is showing. If the last two disagree the cursor is
        /// real and out of sight, and the fault is the working layer or the slice rather than
        /// anything in the drawing.</para>
        /// </summary>
        void ReportCursorLayer(CellRef hover, CellRef at, DesignateDirector director)
        {
            int active = cameraRig != null ? cameraRig.ActiveLayer : -1;
            string note = $"pointer L{hover.Y} -> ghost L{at.Y}, camera L{active}, " +
                          $"working layer {(director.WorkingLayer is int w ? w.ToString() : "none")}";
            if (note == _lastCursorNote) return;
            _lastCursorNote = note;
            Debug.Log($"[Cursor] drawn: {note}");
        }

        string? _lastCursorNote;

        /// <summary>
        /// How solid the build ghost is. Enough to read its shape and its material, not enough to
        /// be mistaken for a building that is already standing — which is the one way this cursor
        /// could mislead rather than help.
        /// </summary>
        const float GhostAlpha = 0.45f;

        /// <summary>
        /// Which layer a build order dragged over this column would actually stand on.
        ///
        /// <para>The simulation lifts an order named at solid ground onto the cell above it
        /// (<c>ConstructionGrid.StandingOn</c>), because a click on grass names the ground
        /// <em>block</em> and a wall goes in the air. The cursor has to be lifted by the same rule
        /// or it draws one layer below the wall it is promising.</para>
        ///
        /// <para><b>A floor is lifted over more than a wall is</b>, and the cursor has to know
        /// which is armed. <c>ConstructionGrid.StandingOver</c> puts a slab on top of anything
        /// that fills a cell, a wall included, because the first slab of a storey rests on the
        /// walls of the one below. Asking the solid-terrain question for a floor drawn over a run
        /// of walls put the green box one layer under the floor it was promising.</para>
        ///
        /// <para>A method and a cached delegate rather than a lambda, because this is handed to
        /// <see cref="BuildPreview.Gather"/> on every frame of a drag and a closure over
        /// <c>min.Y</c> would allocate on each one.</para>
        /// </summary>
        int PreviewLayerAt(int x, int z)
        {
            int y = _previewLayer;
            if (_grid == null || !_grid.Contains(x, z, y) || y + 1 >= _grid.Size.SizeY) return y;

            int index = _grid.Index(new CellRef(x, z, y));
            bool fills = _grid.IsSolidTerrain(index)
                || (_previewIsSlab && _grid.Edifice[index] >= 0);
            return fills ? y + 1 : y;
        }

        /// <summary>
        /// Is the armed build tool a floor? Read once when the preview is gathered rather than per
        /// column, because it is the same answer for every cell of one box.
        /// </summary>
        bool _previewIsSlab;

        /// <summary>
        /// The build cursor over a run that will build nothing.
        ///
        /// <para>The cancel tool's own hue at a heavier alpha. Both halves are deliberate: the
        /// colour is one a player has already seen mean "this takes something away or does
        /// nothing", and it is heavier than any order mark because it is a refusal and has to be
        /// read before the button is let go. Its own line rather than a call to
        /// <see cref="Odyssey.Hud.OrderColours"/>'s cursor alpha, because this is not the cancel
        /// tool and tuning that one should not re-weight this.</para>
        /// </summary>
        static readonly Color PreviewRefusedColour =
            Ui.HudTokens.Convert(OrderColours.Hue(DesignateTool.Cancel).WithAlpha(0.70f));

        int _previewLayer;
        Func<int, int, int>? _previewLayerAt;
        readonly List<PreviewBox> _previewBoxes = new List<PreviewBox>();

        /// <summary>
        /// What colour a standing order is drawn in.
        ///
        /// <para><b>Four constants used to live here</b>, and the reason they do not any more is
        /// that a colour is not a rendering detail: it is what tells a player which tool they are
        /// holding, and the same four tools are coloured on the orders strip and in the palette
        /// header by <see cref="Odyssey.Hud.HudTheme"/>. Two of the four disagreed —
        /// <see cref="Odyssey.Hud.OrderColours"/> has the history. This method stays because
        /// <c>DesignationKind</c> lives in the simulation and the Hud assembly cannot name it; it
        /// converts a kind to a tool and asks, and decides nothing itself.</para>
        ///
        /// <para><b>Total over the enum, and that is still the point.</b> This was a two-branch
        /// ternary — <c>Kind == Mine ? mine : fell</c> — answering a three-kind question, so a
        /// deconstruct order was drawn in the felling green. Exactly the shape of the fault that
        /// made a palette chip arm a tool and never light. <c>OrderColoursTests</c> walks every
        /// kind and every tool, in the fast tier, which is where the mapping now lives.</para>
        /// </summary>
        public static Color OrderColour(DesignationKind kind) =>
            Ui.HudTokens.Convert(OrderColours.Mark((byte)kind));

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

        PowerLinePass? _powerLines;

        /// <summary>Whether this session last told the world it was watching the lines — so the
        /// watch intent is sent on the change, not every frame.</summary>
        bool _watchingPower;

        /// <summary>The lines' draw calls last frame, for the frame budget (design 32 §11).</summary>
        public int PowerLineDrawCalls => _powerLines?.LastDrawCalls ?? 0;

        /// <summary>
        /// Decide whether the lines are shown and draw them (design 32 §9). The decision is
        /// <see cref="PowerLinesVisibility"/>'s: a power tool, deconstruct or cancel armed, a power
        /// building selected, or the overlay on. When it changes, the world is told — built lines
        /// are published only while watched (process §3) — and a paused world answers at once,
        /// because <c>WatchPower</c> applies while paused.
        /// </summary>
        void DrawPowerLines(WorldSnapshot snapshot, int activeLayer)
        {
            if (_powerLines == null || _world == null) return;

            bool selected = Directors?.Selection?.Cell is CellRef cell
                            && snapshot.Size.Contains(cell)
                            && snapshot.TryGetPowerDevice(snapshot.Size.Index(cell), out _);
            bool overlay = Directors?.Overlays?.PowerVisible ?? false;
            bool visible = _designate != null
                ? PowerLinesVisibility.Visible(_designate.Director, selected, overlay)
                : selected || overlay;

            if (visible != _watchingPower)
            {
                _watchingPower = visible;
                _world.Intents.Submit(new Intent(IntentKind.WatchPower, default, visible ? 1 : 0));
            }

            _powerLines.Draw(snapshot, visible, activeLayer);
        }

        /// <summary>
        /// The draft on the board (design 33 §2g): a diamond over every drafted colonist's head, and
        /// for the <i>selected</i> ones that are walking under orders, a line to where they were
        /// sent and a floor bracket on it. Twenty lines across the board would be noise; the ones
        /// being commanded are signal. One walk of the aspects finds them all
        /// (<see cref="OrderModel.CollectDrafted"/>), and the cost is a submission or three per
        /// drafted colonist — it scales with the draft, never with the board.
        ///
        /// <para><b>An undrafted colonist sent for a weapon gets the same line</b> (design 33 §7a,
        /// the context menu's Equip): the simulation publishes the weapon's cell as her order cell,
        /// the same walk of <c>OrderModel.CollectDrafted</c> finds it, and the line and bracket are
        /// drawn exactly as a drafted move's — without the diamond, which says "drafted".</para>
        /// </summary>
        void DrawDraftMarks(WorldSnapshot snapshot, int movePerTick)
        {
            if (_renderer == null || _model == null || _world == null) return;

            OrderModel.CollectDrafted(snapshot, _draftMarks, _undraftedOrders);
            SoundTheDraft();
            if (_draftMarks.Count == 0 && _undraftedOrders.Count == 0) return;

            Color hue = Ui.HudTokens.Convert(OrderColours.Draft.WithAlpha(OrderColours.DraftAlpha));
            Color line = Ui.HudTokens.Convert(OrderColours.Draft.WithAlpha(OrderColours.DraftAlpha * 0.75f));
            SelectionDirector? selection = Directors?.Selection;

            for (int i = 0; i < _draftMarks.Count; i++)
            {
                OrderModel.DraftedMark mark = _draftMarks[i];
                if (!snapshot.TryGetPawn(mark.Pawn, out PawnView pawn)) continue;

                if (_figures == null || !_figures.TryGetFeet(pawn.Id, out Vector3 feet))
                    feet = PawnPose.Of(pawn, _tickAlpha, movePerTick, out _, _model);
                _renderer.DrawMarker(feet + Vector3.up * (colonistCursor.y + DraftMarkerLift), DraftMarkerSize, hue);

                if (mark.OrderCell < 0 || selection == null || !IsSelected(selection, pawn.Id)) continue;
                DrawOrderLine(feet, mark.OrderCell, hue, line);
            }

            for (int i = 0; i < _undraftedOrders.Count; i++)
            {
                OrderModel.DraftedMark order = _undraftedOrders[i];
                if (selection == null || !IsSelected(selection, order.Pawn)) continue;
                if (!snapshot.TryGetPawn(order.Pawn, out PawnView pawn)) continue;

                if (_figures == null || !_figures.TryGetFeet(pawn.Id, out Vector3 feet))
                    feet = PawnPose.Of(pawn, _tickAlpha, movePerTick, out _, _model);
                DrawOrderLine(feet, order.OrderCell, hue, line);
            }
        }

        /// <summary>The order line from a colonist's feet to the cell she was sent to, and a bracket on it.</summary>
        void DrawOrderLine(Vector3 feet, int orderCell, Color bracket, Color line)
        {
            CellRef dest = _world!.Size.FromIndex(orderCell);
            Vector3 to = GroundRelief.Drape(CellMetrics.FloorCentre(dest)).GetPosition() + Vector3.up * 0.12f;
            _renderer!.DrawSegment(feet + Vector3.up * 0.12f, to, DraftLineThickness, line);
            _renderer.DrawFloorBracket(dest, bracket);
        }

        /// <summary>
        /// A blade drawn, once, on the frame the snapshot first shows a colonist drafted (design 33
        /// §2i; owner, 2026-09-23: <i>"use it when draft mode is clicked/actioned as an
        /// indicator"</i>). Read off the frame rather than the key, so the key, the pane's button
        /// and anything later that drafts all sound, and a refused draft — a broken or spent
        /// colonist — stays silent. Releasing is silent, and so is the four-hour let-go.
        ///
        /// <para>A world seen for the first time is taken as it is and never sounds: loading a
        /// save with three colonists drafted is not three orders given.</para>
        /// </summary>
        void SoundTheDraft()
        {
            bool first = !ReferenceEquals(_draftSoundWorld, _world);
            _draftSoundWorld = _world;

            bool fresh = false;
            for (int i = 0; i < _draftMarks.Count; i++)
                if (!_draftedLastFrame.Contains(_draftMarks[i].Pawn.Value)) fresh = true;

            _draftedLastFrame.Clear();
            for (int i = 0; i < _draftMarks.Count; i++) _draftedLastFrame.Add(_draftMarks[i].Pawn.Value);

            if (fresh && !first) _audio?.PlayOneShot(SoundIds.Draft, Vector3.zero);
        }

        /// <summary>
        /// Where a fallen body's middle is, for the pool under it (design 33 §10c): a dead body at
        /// rest is the corpse's own drawn box; one still falling, or a downed one, is halfway from
        /// its figure's feet to its head, which is on the body whichever way it went. No answer
        /// with neither, and the pool goes at the feet it was given.
        /// </summary>
        bool FindBody(PawnId who, out Vector3 middle)
        {
            if (_corpses != null && _corpses.TryGetMiddle(who, out middle)) return true;
            if (_figures != null && _figures.TryGetFeet(who, out Vector3 feet) && _figures.TryGetHead(who, out Vector3 head))
            {
                middle = new Vector3((feet.x + head.x) * 0.5f, feet.y, (feet.z + head.z) * 0.5f);
                return true;
            }
            middle = default;
            return false;
        }

        /// <summary>
        /// The fight's marks over the pawns (design 33 §1): a health bar where
        /// <see cref="CombatFeedbackModel.HealthBar"/> owes one — the hurt, the downed and the
        /// drafted — and the red marker over a hostile. What is owed is the model's, the bar's
        /// pieces are <see cref="HealthBarLayout"/>'s and where they stand is
        /// <see cref="CombatMarks"/>'. The bars' pieces are gathered by ink and go out together at
        /// the end, one instanced call per ink (at most five, whatever the number of bars); the
        /// hostile marker is one submission each. A walk of the pawns on the drawn layers: it
        /// scales with the pawns in view, never with the board, and allocates nothing.
        ///
        /// <para><b>The bar faces the camera and is laid as pieces that never overlap</b> (design
        /// 33 §8a). It was a translucent fill box inside a translucent track box, and the
        /// transparent sort decided which covered the other — a tie on every frame of a full bar,
        /// which is what the owner saw flicker.</para>
        /// </summary>
        void DrawCombatMarks(WorldSnapshot snapshot, int movePerTick, int activeLayer, SliceSettings slice)
        {
            if (_renderer == null || _model == null) return;

            int lowest = Mathf.Max(0, slice.LowestDrawnLayer(activeLayer, _model.LowestOutdoorLayer));
            int highest = slice.HighestVisibleLayer(activeLayer, snapshot.Size.SizeY);
            Quaternion facing = cameraRig != null ? cameraRig.transform.rotation : Quaternion.identity;
            Color outline = Ui.HudTokens.Convert(CombatFeedbackModel.HealthBarOutline);
            Color plate = Ui.HudTokens.Convert(CombatFeedbackModel.HealthBarPlate);
            Color hostileInk = Ui.HudTokens.Convert(CombatMarks.HostileInk);

            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                PawnView pawn = pawns[i];
                if (pawn.Cell.Y < lowest || pawn.Cell.Y > highest) continue;

                bool bar = CombatFeedbackModel.HealthBar(snapshot, in pawn, out int hp, out int hpMax);
                bool hostile = CombatFeedbackModel.HostileMarker(in pawn);
                if (!bar && !hostile) continue;

                if (_figures == null || !_figures.TryGetFeet(pawn.Id, out Vector3 feet))
                    feet = PawnPose.Of(pawn, _tickAlpha, movePerTick, out _, _model);

                // The top of the pawn as the cursor has it: the colonist's box, an animal's own,
                // and a body on the ground much lower.
                float top = colonistCursor.y;
                if (pawn.IsAnimal)
                    top = _figures != null && _figures.TryGetAnimalBox(pawn.Id, out _, out Vector3 box) ? box.y : 1.0f;
                if (pawn.IsDowned) top = CombatMarks.DownedTop;

                if (bar)
                {
                    Vector3 centre = feet + Vector3.up * (top + CombatMarks.BarLift);
                    Color fill = Ui.HudTokens.Convert(HealthBarLayout.InkOf(HealthBarInk.Fill,
                        CombatFeedbackModel.HealthBarColour(hp, hpMax)));
                    int pieces = HealthBarLayout.Pieces(CombatMarks.Fraction(hp, hpMax), _barPieces);
                    for (int p = 0; p < pieces; p++)
                    {
                        HealthBarInk ink = _barPieces[p].Ink;
                        Color colour = ink == HealthBarInk.Fill ? fill : ink == HealthBarInk.Plate ? plate : outline;
                        _renderer.GatherBarPiece(colour, CombatMarks.Place(in _barPieces[p], centre, facing));
                    }
                }

                if (hostile)
                    _renderer.DrawMarker(feet + Vector3.up * (top + DraftMarkerLift), DraftMarkerSize, hostileInk);
            }

            _renderer.FlushBarPieces();
        }

        /// <summary>One bar's pieces, reused for every bar every frame.</summary>
        readonly HealthBarPiece[] _barPieces = new HealthBarPiece[HealthBarLayout.MaxPieces];

        /// <summary>
        /// The lock-on ring (design 33 §7b; owner, 2026-09-23: <i>"paints a red transparent circle
        /// quickly around the selected enemy to indicate that target"</i>): a flat red ring under
        /// every pawn a selected, drafted colonist is attacking, snapping in from 1.6 times its
        /// footprint on the frame the order is published. Which targets and how far through the
        /// animation are <see cref="LockOnRings"/>' — fast-tier tested — and this only finds where
        /// each target stands and draws. <b>Read off the frame, not the click</b>: a refused order
        /// publishes no target and draws nothing.
        ///
        /// <para>The ring lies <b>draped</b> on the ground under the feet — the relief's tilt from
        /// <see cref="GroundRelief.Drape"/>, the height from the drawn figure, which is lifted on
        /// to the same ground — so it neither floats on a slope nor cuts into one. A target that
        /// dies leaves the frame, so the ring's last place is remembered for its fade. A target
        /// on a layer the slice does not draw draws no ring, the health bars' rule.</para>
        ///
        /// <para>One submission per ring, and at most one ring per target: it scales with the
        /// fight the selection is in, never with the board. Its opacity is quantised
        /// (<see cref="LockOnRing.Quantise"/>), so the animation reuses a bounded set of cached
        /// materials instead of minting one per frame.</para>
        /// </summary>
        void DrawLockOnRings(WorldSnapshot snapshot, int movePerTick, int activeLayer, SliceSettings slice)
        {
            if (_renderer == null || _model == null) return;

            SelectionDirector? selection = Directors?.Selection;
            IReadOnlyList<PawnId> selected = selection != null ? selection.Pawns : Array.Empty<PawnId>();
            _lockOn.Update(snapshot, selected, Time.unscaledTime, _world);

            IReadOnlyList<LockOnRings.Ring> rings = _lockOn.Rings;
            if (rings.Count == 0)
            {
                _ringPlaces.Clear();
                return;
            }

            int lowest = Mathf.Max(0, slice.LowestDrawnLayer(activeLayer, _model.LowestOutdoorLayer));
            int highest = slice.HighestVisibleLayer(activeLayer, snapshot.Size.SizeY);

            for (int i = 0; i < rings.Count; i++)
            {
                LockOnRings.Ring ring = rings[i];
                if (snapshot.TryGetPawn(ring.Target, out PawnView pawn))
                    _ringPlaces[ring.Target.Value] = RingPlaceOf(in pawn, movePerTick);
                if (!_ringPlaces.TryGetValue(ring.Target.Value, out RingPlace place)) continue;
                if (place.Layer < lowest || place.Layer > highest || ring.Alpha <= 0f) continue;

                float radius = place.Radius * ring.Scale;
                Matrix4x4 at = GroundRelief.Drape(new Vector3(place.Centre.x, 0f, place.Centre.z));
                at.m13 = place.Centre.y + LockOnRingLift;
                Color colour = Ui.HudTokens.Convert(OrderColours.Attack.WithAlpha(ring.Alpha));
                _renderer.DrawRing(at * Matrix4x4.Scale(new Vector3(radius, 1f, radius)), colour);
            }

            // Forget the places of targets whose rings have gone. The scratch list is reused.
            if (_ringPlaces.Count <= rings.Count) return;
            _ringPlacesGone.Clear();
            foreach (int target in _ringPlaces.Keys)
            {
                bool live = false;
                for (int i = 0; i < rings.Count && !live; i++) live = rings[i].Target.Value == target;
                if (!live) _ringPlacesGone.Add(target);
            }
            for (int i = 0; i < _ringPlacesGone.Count; i++) _ringPlaces.Remove(_ringPlacesGone[i]);
        }

        /// <summary>
        /// Where a ring stands under a pawn, and how wide it is at rest: an animal's own drawn box
        /// (<see cref="PawnFigureDirector.TryGetAnimalBox"/>) centred where the box is, a person
        /// the colonist cursor's box at the feet, and a person lying down half a body's length.
        /// </summary>
        RingPlace RingPlaceOf(in PawnView pawn, int movePerTick)
        {
            if (_figures == null || !_figures.TryGetFeet(pawn.Id, out Vector3 feet))
                feet = PawnPose.Of(pawn, _tickAlpha, movePerTick, out _, _model);

            if (pawn.IsAnimal && _figures != null
                && _figures.TryGetAnimalBox(pawn.Id, out Matrix4x4 box, out Vector3 size))
            {
                Vector3 middle = box.GetPosition();
                return new RingPlace(new Vector3(middle.x, feet.y, middle.z),
                    LockOnRing.FootRadius(size.x, size.z), pawn.Cell.Y);
            }

            float radius = pawn.IsDowned && pawn.IsPerson
                ? LockOnRing.DownedRadius
                : LockOnRing.FootRadius(colonistCursor.x, colonistCursor.z);
            return new RingPlace(feet, radius, pawn.Cell.Y);
        }

        readonly struct RingPlace
        {
            public readonly Vector3 Centre;
            public readonly float Radius;
            public readonly int Layer;

            public RingPlace(Vector3 centre, float radius, int layer)
            {
                Centre = centre;
                Radius = radius;
                Layer = layer;
            }
        }

        readonly LockOnRings _lockOn = new LockOnRings();
        readonly Dictionary<int, RingPlace> _ringPlaces = new Dictionary<int, RingPlace>();
        readonly List<int> _ringPlacesGone = new List<int>();

        /// <summary>How far above the ground the ring lies, in metres: clear of the ground mesh, under a boot.</summary>
        const float LockOnRingLift = 0.02f;

        // Who was drafted last frame, and in which world: the draft sound's memory.
        readonly HashSet<int> _draftedLastFrame = new HashSet<int>();
        object? _draftSoundWorld;

        static bool IsSelected(SelectionDirector selection, PawnId pawn)
        {
            IReadOnlyList<PawnId> pawns = selection.Pawns;
            for (int i = 0; i < pawns.Count; i++) if (pawns[i] == pawn) return true;
            return false;
        }

        // Scratch for DrawDraftMarks, refilled every frame.
        readonly List<OrderModel.DraftedMark> _draftMarks = new List<OrderModel.DraftedMark>();
        readonly List<OrderModel.DraftedMark> _undraftedOrders = new List<OrderModel.DraftedMark>();

        /// <summary>How far above the cursor box's top the drafted diamond floats, and how big it is, in metres.</summary>
        const float DraftMarkerLift = 0.35f, DraftMarkerSize = 0.28f;

        /// <summary>The order line's thickness in metres: thin enough to read as a line from the play camera.</summary>
        const float DraftLineThickness = 0.06f;

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
                    Color strength = i == 0 ? colour : SecondarySelectionColour;

                    // An animal is bracketed as its own drawn box, turned the way it faces, with
                    // the item bracket's margin (owner, 2026-09-22: the cell-sized column round a
                    // hog highlighted the whole tile). A colonist keeps the one fixed box below.
                    if (pawn.IsAnimal && _figures != null
                        && _figures.TryGetAnimalBox(pawn.Id, out Matrix4x4 place, out Vector3 box))
                    {
                        _renderer.DrawSelectionBracket(place, box + Vector3.one * ItemCursorMargin, strength);
                        continue;
                    }

                    // The figure's own position where there is one, for the same reason the hit-test
                    // uses it: a working colonist is stepped off their cell, and a bracket drawn from
                    // the pose would sit on the cell while the person stands beside it.
                    if (_figures == null || !_figures.TryGetFeet(pawn.Id, out Vector3 feet))
                        feet = PawnPose.Of(pawn, _tickAlpha, movePerTick, out _, _model);
                    _renderer.DrawSelectionBracket(
                        feet + Vector3.up * (colonistCursor.y * 0.5f), colonistCursor, strength);
                }
                return;
            }

            // A corpse is bracketed as the body lying there (design 33 §5f), not as its cell: it
            // is a person's length along the ground and knee high.
            if (_corpses != null && _corpses.TryBracket(selection, out Matrix4x4 corpsePlace, out Vector3 corpseBox))
            {
                _renderer.DrawSelectionBracket(corpsePlace, corpseBox, colour);
                return;
            }

            CellRef? picked = selection?.Cell;
            if (picked == null) return;
            CellRef cell = picked.Value;

            if (selection != null && selection.HasThing)
            {
                // The thing's own cell, not the cell the pick resolved to: a pile on bare ground
                // is picked through the solid block under it, and a bracket drawn there is buried
                // in the ground rather than around the object (owner, 2026-09-17: meals and scrap
                // showed no cursor at all). The snapshot is the thing's address, and it is
                // already in hand.
                var things = snapshot.Things;
                for (int i = 0; i < things.Length; i++)
                {
                    if (things[i].Id != selection.Thing) continue;
                    cell = things[i].Cell;
                    break;
                }

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

            // A bed is bracketed as the bed, not as the cell it was clicked in (owner,
            // 2026-09-17: "it highlighted the entire cell instead of highlighting the bed"). It is
            // two cells long and knee high, so a cell highlight is wrong about how big it is,
            // which way it faces and where it ends — and it is the one edifice in the game that
            // does not fill the cell it stands in. The head cell is what the bracket is measured
            // from, whichever half was clicked, so either end selects the same box.
            if (_model.EdificeDef(index) == CoreContent.EdificeBed)
            {
                int head = _model.BedHeadAt(index);
                if (head >= 0)
                {
                    CellRef at = _model.Size.FromIndex(head);
                    BedShape.WorldBounds(
                        at.X, at.Z, at.Y, _model.BedFacing(head),
                        out Vector3 centre, out Vector3 size);
                    _renderer.DrawSelectionBracket(centre, size + Vector3.one * ItemCursorMargin, colour);
                    return;
                }
            }

            // A shelf, for the bed's reason one cell along: it does not fill the cell it stands in,
            // so a cell highlight is wrong about how big it is and which way it faces. Its box is
            // off-centre in plan — the carcass is against the back — which is why the bracket asks
            // ShelfShape rather than being built from the cell here.
            if (_model.EdificeDef(index) == CoreContent.EdificeShelf)
            {
                ShelfShape.WorldBounds(cell.X, cell.Z, cell.Y, _model.EdificeFacing(index),
                    out Vector3 shelfCentre, out Vector3 shelfSize);
                _renderer.DrawSelectionBracket(shelfCentre, shelfSize + Vector3.one * ItemCursorMargin, colour);
                return;
            }

            if (_model.IsSolid(index) || _model.EdificeDef(index) != CoreContent.EdificeNone)
            {
                _renderer.DrawCellHighlight(cell, colour);
                return;
            }

            if (WaterLine.IsWater(_model, cell))
            {
                float lift = WaterLine.SurfaceAbove(_model, cell);
                Matrix4x4 placement = GroundRelief.Drape(CellMetrics.FloorCentre(cell) + Vector3.up * lift);
                _renderer.DrawFloorBracket(placement, colour);
                return;
            }

            BankLayout.Bank bank = BankLayout.At(_model, cell);
            if (bank.Exists)
            {
                if (bank.Kind == BankMesh.Kind.Straight)
                {
                    Matrix4x4 placement = GroundRelief.Drape(CellMetrics.FloorCentre(cell)) *
                                          Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[bank.Rotation], 0f)) *
                                          BankLayout.StraightBankShear();
                    _renderer.DrawFloorBracket(placement, colour);
                }
                else
                {
                    Matrix4x4 place = GroundRelief.Drape(CellMetrics.FloorCentre(cell)) *
                                      Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[bank.Rotation], 0f));
                    var cornerRises = new float[4];
                    for (int k = 0; k < 4; k++)
                    {
                        float localX = ((k & 1) == 0 ? -1f : 1f) * 0.5f;
                        float localZ = ((k & 2) == 0 ? -1f : 1f) * 0.5f;
                        cornerRises[k] = (BankMesh.HeightAt(bank.Kind, localX, localZ) + 0.5f) * CellMetrics.SizeY;
                    }
                    _renderer.DrawFloorBracket(place, colour, cornerRises);
                }
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
                // The line that answers "is this the CPU or the GPU", which nothing on this
                // overlay could say until 2026-09-21. Read `gpu` against `frame` and `submit`:
                // where it is at or above the frame time the frame is fill-bound, no amount of
                // batching will move it, and the render-scale rung is the lever; where it is well
                // under, the cost is on this side of the bus and the split below says which pass.
                //
                // **VSync and the frame cap are printed beside them because without those two the
                // frame time is not evidence of anything.** A frame pinned at 16.7 ms with 8.4 ms
                // of GPU and 5.5 ms of submit in it is a frame spending three milliseconds waiting
                // for a monitor, and reading that as "we are at budget" is the wrong conclusion
                // twice over — it hides both the headroom and the real cost.
                $"gpu {Timing(_smoothedGpuMs)}   {Screen.width}x{Screen.height}" +
                $"   vsync {(QualitySettings.vSyncCount > 0 ? $"on/{QualitySettings.vSyncCount}" : "off")}" +
                $"   cap {(Application.targetFrameRate > 0 ? Application.targetFrameRate.ToString() : "none")}" +
                // Where the trace is going and how much of it there is. Printed because "is it
                // recording" is otherwise a question with no answer until the session is over,
                // which is far too late to discover that it was not.
                $"   trace {TraceNote()}\n" +
                $"submit split: {SubmitSplit()}\n" +
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

            // This is the developer overlay (A15), the one region immediate mode is permitted in.
            // It sat under the HUD's top-left ledger until 2026-09-17; the owner asked for it much
            // bigger, which put it back on top of that ledger at the old position, so it now anchors
            // to the bottom of the screen instead — clear of the top-left panels regardless of size,
            // and clear of the command bar and orders strip along the bottom edge.
            //
            // Font size is roughly six times the default GUI.skin.label size (owner, 2026-09-17,
            // first pass: "much much much bigger... cannot be read"; second pass, after judging the
            // first: move it toward the bottom and make it bigger again). Still unjudged past this
            // second pass.
            GUIStyle style = DeveloperOverlayStyle();
            int lines = 1;
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') lines++;
            float lineHeight = style.fontSize * 1.3f;
            float height = lines * lineHeight;
            float x = 10f;
            float y = Screen.height - height - 96f;
            var rect = new Rect(x, y, Screen.width - 2f * x, height);
            var shadow = new Rect(x + 1f, y + 1f, rect.width, rect.height);

            GUI.color = Color.black;
            GUI.Label(shadow, text, style);
            GUI.color = Color.white;
            GUI.Label(rect, text, style);
        }

        /// <summary>
        /// A millisecond figure, or <c>n/a</c> where the platform declined to give one.
        ///
        /// <para>Printed rather than zeroed deliberately. A GPU time of 0.00 ms and a GPU time
        /// that could not be read look identical, and the second is the likelier of the two on a
        /// non-development build — a zero there would be read as "the GPU is free", which is the
        /// exact wrong conclusion to hand somebody hunting a fill-bound frame.</para>
        /// </summary>
        static string Timing(float ms) => ms > 0f ? $"{ms:0.00} ms" : "n/a";

        /// <summary>The trace file and how many seconds are in it, or why there is not one.</summary>
        string TraceNote()
        {
            if (_tracer == null) return TraceEnabled ? "opening" : "off";
            if (!_tracer.Active) return "stopped: " + (_tracer.Fault ?? "unknown");
            return System.IO.Path.GetFileName(_tracer.Path) + $" ({_tracer.Rows}s, {_tracer.Marks} marked)";
        }

        readonly int[] _splitOrder = new int[(int)FrameSection.Count];

        /// <summary>
        /// Last frame's draw block, section by section, largest first, skipping what rounds to
        /// nothing.
        ///
        /// <para>Largest first because the list is read while something is wrong, and in that
        /// state the only question is which name is at the front. Enum order would put
        /// <c>Mirror</c> there every time.</para>
        /// </summary>
        string SubmitSplit()
        {
            for (int i = 0; i < _splitOrder.Length; i++) _splitOrder[i] = i;
            System.Array.Sort(_splitOrder, (a, b) => _sectionMs[b].CompareTo(_sectionMs[a]));

            var parts = new System.Text.StringBuilder();
            for (int i = 0; i < _splitOrder.Length; i++)
            {
                double ms = _sectionMs[_splitOrder[i]];
                if (ms < 0.005d) continue;
                if (parts.Length > 0) parts.Append("  ");
                parts.Append((FrameSection)_splitOrder[i]).Append(' ').Append(ms.ToString("0.00"));
            }
            return parts.Length > 0 ? parts.ToString() : "nothing measurable";
        }

        GUIStyle? _developerOverlayStyle;

        /// <summary>
        /// Built lazily and cached: <see cref="GUIStyle"/> may only be constructed inside a GUI
        /// callback, and OnGUI runs every frame the overlay is visible, so the style is made once
        /// rather than allocated per frame.
        /// </summary>
        GUIStyle DeveloperOverlayStyle()
        {
            _developerOverlayStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 56, wordWrap = false };
            return _developerOverlayStyle;
        }

        /// <summary>
        /// Hand the two facts that decide the pointer to <see cref="CursorDirector"/>: where it is,
        /// and what is in hand. Both are already published for other reasons, which is why this is
        /// three lines and not a subscription.
        ///
        /// <para>With no rig the pointer is treated as being over the interface — on the main
        /// screen and between sessions the HUD <i>is</i> the whole screen, and the arrow is what
        /// belongs there.</para>
        ///
        /// <para>See <c>docs/design/28-pointer-cursor.md</c>.</para>
        /// </summary>
        void UpdatePointerCursor() => _cursor.Update(
            cameraRig == null || cameraRig.PointerWasOverInterface,
            _designate != null ? _designate.Director.Tool : DesignateTool.None);

        void OnDestroy()
        {
            // Give the pointer back before anything else goes. `Cursor.SetCursor` outlives play
            // mode, so a session that exits holding a crosshair leaves the *editor* wearing one.
            _cursor.Release();
            CursorArt.Forget();

            TeardownSession();

            // And the two things teardown deliberately leaves standing, because neither is part
            // of a session: the portrait studio's rig and its one render texture, and the menu
            // bed, which is the sound of there being no session at all.
            _portraits?.Dispose();
            _portraits = null;
            _menuBed?.Dispose();
            _menuBed = null;
        }

        /// <summary>
        /// Put the world down: dispose everything that owns a GPU or engine resource, drop every
        /// reference, and leave the component able to build again (U35).
        ///
        /// <para>Safe to call with no session, and safe to call twice — a menu unwinding and a
        /// scene closing both reach it, and they can reach it in either order.</para>
        ///
        /// <para><b>What must not survive.</b> A <see cref="ModuleLibrary"/> owns every mesh it
        /// baked, and a <c>Mesh</c> made in code is a GPU allocation Unity never collects; the
        /// figures own GameObjects and their animation graphs; the colonist materials own textures
        /// written at runtime. Leaving any of them behind does not fail a test that looks at the
        /// world — the next world is perfectly correct — it just costs the memory twice, which is
        /// why the check for it is a test that counts rather than an eye that looks.</para>
        /// </summary>
        /// <summary>
        /// The scenario a session runs: the file's, when loading, and the scene's otherwise.
        ///
        /// <para>Matched by <c>defName</c> rather than by index, because an index is a number that
        /// means something different the day a scenario is inserted before another — and the
        /// header carries the name for exactly this reason. A name this build does not know falls
        /// back to the scene's scenario and says so, rather than refusing to open a colony over a
        /// starting condition that has already happened: the scenario places things at tick zero,
        /// and a save is a world long past it.</para>
        /// </summary>
        ScenarioDef ScenarioFor(SaveHeader? from)
        {
            ScenarioDef scene = scenario == StartingScenario.Bare ? ScenarioDef.Bare() : ScenarioDef.Playtest();
            if (from == null || from.Recipe.Scenario.Length == 0) return scene;

            foreach (ScenarioDef known in new[] { ScenarioDef.Bare(), ScenarioDef.Playtest() })
                if (string.Equals(known.defName, from.Recipe.Scenario, StringComparison.Ordinal))
                    return known;

            Debug.LogWarning($"[Odyssey] save names scenario '{from.Recipe.Scenario}', which this " +
                             $"build does not know; opening it on {scene.defName} instead. The " +
                             "scenario only acts at tick zero, so a loaded world is unaffected.");
            return scene;
        }

        /// <summary>Raised when a session has been built, and when one has been torn down. The
        /// start screen shows itself on the second and hides on the first.</summary>
        public event Action? SessionChanged;

        /// <summary>
        /// Write the running colony to a path (U38).
        ///
        /// <para>The day is computed here rather than in Sim, which has no calendar:
        /// <c>GameClock</c> lives in the Hud assembly and <c>SaveRecipe</c>'s own doc comment
        /// names this as the caller's job.</para>
        ///
        /// <para><b>Taken between ticks, not during one.</b> The menu is a HUD click, so this runs
        /// inside a frame's update and the world is at rest — but that is a property of where it
        /// is called from rather than of this method, so it is asserted rather than assumed.</para>
        /// </summary>
        /// <summary>
        /// The file this session is bound to: the one it was loaded from, or the one it last saved
        /// to. Null until it has been saved or loaded once.
        ///
        /// <para><b>This is what stops a folder filling up</b> (owner, 2026-09-17: *"I notice you
        /// keep saving a new game everytime … otherwise lots of saves will be created"*). Save
        /// writes here; only Save as changes where here is.</para>
        /// </summary>
        public string? BoundSavePath { get; private set; }

        /// <summary>
        /// What the naming prompt should offer: the name this session already has, or the colony's
        /// own name the first time. Never empty — a prompt whose default is blank is a prompt that
        /// cannot be confirmed until the player has thought of something.
        /// </summary>
        public string SuggestedSaveName()
        {
            if (BoundSavePath != null)
                return System.IO.Path.GetFileNameWithoutExtension(BoundSavePath);

            // SaveCatalogue's own suggestion rather than the colony's bare name, because it carries
            // an invariant worth having: a player who accepts the offer lands on exactly the file
            // an unnamed save would have chosen, so one colony cannot end up holding both
            // "riverbend-day-12" and a near-identical twin of it.
            return _colony != null
                ? SaveCatalogue.SuggestedName(CurrentRecipe())
                : DefaultColonyName;
        }

        /// <summary>
        /// Save over the file this session is bound to.
        ///
        /// <para>Returns the path written, or <b>null when there is nothing to write over</b> — the
        /// first save of a colony, which the caller answers by asking for a name. It is a return
        /// value rather than an exception because "this colony has not been named yet" is an
        /// ordinary state, not a fault.</para>
        /// </summary>
        public string? SaveSession()
        {
            if (BoundSavePath == null) return null;
            SaveSession(BoundSavePath);
            return BoundSavePath;
        }

        /// <summary>
        /// Save under a name the player chose, and bind the session to it.
        ///
        /// <para>Overwrites a file of that name if there is one. The asking is the prompt's, and it
        /// has already happened by the time this is called — a method that re-asked would be a
        /// second place the rule lived.</para>
        /// </summary>
        public string SaveSessionAs(string name)
        {
            string path = SaveFiles.PathForName(name);
            SaveSession(path);
            return path;
        }

        // ==================================================================== the autosave

        /// <summary>
        /// When the colony next writes itself. The rule and the day bookkeeping are
        /// <see cref="Odyssey.Hud.AutosaveClock"/>, in the assembly the fast tier compiles; this
        /// field is only where the running one lives.
        /// </summary>
        readonly Odyssey.Hud.AutosaveClock _autosave = new();

        /// <summary>
        /// Raised after the game has written the colony by itself, with the save's own name. The
        /// HUD puts a line on the Events panel from it; nothing else listens, and nothing in the
        /// simulation hears about it at all.
        /// </summary>
        public event Action<string>? Autosaved;

        /// <summary>
        /// A day has turned. Write the colony over its own save, keeping one previous generation.
        ///
        /// <para><b>A colony that has never been named is named here rather than skipped.</b> The
        /// owner's call (2026-09-21): a brand-new colony is exactly the one a crash hurts most, so
        /// the first autosave takes the name <see cref="SuggestedSaveName"/> would have offered,
        /// binds the session to it, and says so on the Events panel. From then on it is "the same
        /// game" every following autosave overwrites.</para>
        ///
        /// <para>Returns the path written, or null when there was no session to write.</para>
        /// </summary>
        public string? Autosave()
        {
            if (_world == null || _colony == null) return null;

            string path = BoundSavePath ?? SaveFiles.PathForName(SuggestedSaveName());
            SaveFiles.KeepPrevious(path);
            SaveSession(path);

            Autosaved?.Invoke(System.IO.Path.GetFileNameWithoutExtension(path));
            return path;
        }

        /// <summary>
        /// Asked once a frame: has the clock come round? Arithmetic on every frame it has not,
        /// and the disk is touched only on the one it has.
        ///
        /// <para>After the ticks rather than before them, so the day the save records is the day
        /// the frame ended on — and never while paused, because a paused world's tick does not
        /// move and the clock reads the tick.</para>
        /// </summary>
        void ConsiderAutosave()
        {
            if (_world == null || Directors == null) return;
            if (!_autosave.Due(_world.CurrentTick, Directors.Settings.AutosaveDays)) return;

            Autosave();
        }

        /// <summary>The same, to a path of the caller's choosing. What a test uses.</summary>
        public void SaveSession(string path)
        {
            if (_colony == null || _world == null)
                throw new InvalidOperationException("there is no session to save");

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);

            // The view is captured at the moment of saving rather than tracked, because it is only
            // ever needed once and a camera that told a save component about every frame would be
            // the most-written state in the game for the least reason.
            if (cameraRig != null && Directors != null)
            {
                var hud = GetComponent<Ui.HudShell>();
                _view.Capture(cameraRig, Directors, _world.GameSpeed,
                    hud != null ? hud.Roster.CustomOrder : null,
                    hud != null ? hud.Roster.Page : 0);
            }

            // The same bargain the view makes, for the same reason: names are read once, at the
            // moment of writing, rather than tracked.
            _names.Capture(ColonistNames.Book);

            WorldSave.SaveToFile(path, _world, WithPresentationState(_colony.SaveComponents), CurrentRecipe());

            // Bound by writing it, so the next Save goes here rather than somewhere new.
            BoundSavePath = path;
        }

        /// <summary>
        /// The view section for the session being saved or loaded right now.
        ///
        /// <para><b>Replaced on every load rather than reused, and the reason is a bug this had
        /// before the wiring was reviewed.</b> The section remembers whether it captured anything,
        /// and <c>WorldSave.Load</c> simply does not call <c>Load</c> on a section the file does
        /// not contain. So a component kept across sessions would, on opening a save written before
        /// this section existed, still be holding the *previous* colony's camera — and would
        /// faithfully restore it. A restore that quietly uses another game's view is worse than no
        /// restore at all, because it looks like the feature working.</para>
        /// </summary>
        ViewStateSection _view = new ViewStateSection();

        /// <summary>
        /// The names the player typed, for the session being saved or loaded right now. Replaced
        /// on every load for the reason <see cref="_view"/> gives — a section kept across sessions
        /// would still be holding the last colony's names when it opens a save that has none.
        /// </summary>
        ColonistNameSection _names = new ColonistNameSection();

        /// <summary>
        /// The simulation's save components plus the ones this assembly owns.
        ///
        /// <para><b>The view section is Presentation's, and that is why it is appended here rather
        /// than living in <c>ColonyWorld.SaveComponents</c>.</b> Sim cannot see a camera and must
        /// not learn to. It is <c>ISaveable</c> and deliberately <i>not</i>
        /// <c>IStateHashable</c>, so it cannot move the state hash, desync a load or appear in a
        /// determinism gate — which is what makes writing presentation state into a save file safe
        /// at all. `CLAUDE.md`'s rule that nothing in presentation reaches the save was aimed at
        /// determinism, and determinism is the hash's business.</para>
        ///
        /// <para>A headless caller that passes only the simulation's own components is unaffected
        /// in both directions: it writes no view section, and <c>WorldSave.Load</c> skips one it
        /// was not given a handler for.</para>
        ///
        /// <para>The colonist names are here on the same argument and the same terms
        /// (<see cref="ColonistNameSection"/>). This method was called <c>WithView</c> while there
        /// was one of them.</para>
        /// </summary>
        IReadOnlyList<ISaveable> WithPresentationState(IReadOnlyList<ISaveable> components)
        {
            var all = new List<ISaveable>(components.Count + 2);
            all.AddRange(components);
            all.Add(_view);
            all.Add(_names);
            return all;
        }

        SaveRecipe CurrentRecipe()
        {
            if (_colony == null || _world == null)
                throw new InvalidOperationException("there is no session to describe");
            return _colony.Recipe(GameClock.DayOfMonthsStart(_world.CurrentTick));
        }

        /// <summary>
        /// Put down whatever is running and open a save in its place (U38).
        ///
        /// <para><b>Three steps, and the order is the whole of it:</b> tear the session down, read
        /// the header, and build a world from <i>the header</i> before restoring into it.
        /// <c>WorldSave.Load</c> refuses a file whose seed or size differs from the world it is
        /// given, so building from the inspector and loading over the top would fail on any save
        /// not made by the current scene settings — which is every save, the moment a seed can be
        /// rolled.</para>
        /// </summary>
        public void LoadSession(string path)
        {
            SaveHeader header = WorldSave.ReadHeaderOnly(path);

            TeardownSession();
            BuildSession(null, header);

            // Fresh ones, so that a save with no view or name section cannot be restored using the
            // view — or the people — of whatever was open before it. See the fields.
            _view = new ViewStateSection();
            _names = new ColonistNameSection();

            // The colony was built from the header, so this cannot mismatch; if it ever does, the
            // exception from WorldSave says which of seed or size disagreed, and that is a fault
            // in the rebuild above rather than in the file.
            using (var stream = System.IO.File.OpenRead(path))
                WorldSave.Load(_world!, stream, WithPresentationState(_colony!.SaveComponents));

            _names.Apply(ColonistNames.Book);

            // A loaded colony is bound to the file it came out of, so Save puts it back where the
            // player found it. This is the half that makes Save mean "save" rather than "save a
            // copy" for every session after the first.
            BoundSavePath = path;

            // The day this colony arrives on counts as already saved: a save opened and left alone
            // must not be written straight back over the file it came out of.
            _autosave.Begin(_world!.CurrentTick);

            RefreshAfterLoad();
        }

        /// <summary>
        /// What a loaded world needs before it is drawn: the derived state the save deliberately
        /// does not carry, and one tick to publish a frame.
        ///
        /// <para><c>ColonyWorld.RebuildDerived</c> is where support and the region graph come back
        /// (OQ-37) — the save omits both because they are derived, and a grid loaded without them
        /// has zero support everywhere.</para>
        ///
        /// <para><b>The full mirror refresh is the part that is easy to leave out and hard to
        /// diagnose.</b> The mirror primes itself with <c>RefreshAll</c> on the first publish and
        /// uses <c>RefreshDirty</c> for every one after, and a load writes the cell arrays
        /// wholesale without going through anything that marks a chunk dirty. A tick alone would
        /// therefore publish a frame drawn from the *pre-load* world — the colony would be right
        /// and the board would be the one you left. <c>RefreshAll</c> is also the only thing that
        /// maintains <c>LowestOutdoorLayer</c>, which decides how far the slice view reaches down,
        /// so a load without it would draw a correct board at the wrong depth.</para>
        /// </summary>
        void RefreshAfterLoad()
        {
            _colony!.RebuildDerived();
            _model!.RefreshAll(_colony.Grid, _colony.Outcome.Edifices);
            _world!.Tick();

            // The view goes back last, and after the tick: BuildSession has already pointed the
            // camera at the generated start cell and the slice at its layer, so anything applied
            // before this would be overwritten by the build it is trying to correct.
            //
            // The pose is the section's own business — it calls SliceCameraRig.RestorePose, which
            // sets the smoothing targets as well as the live values. The speed is not: the rig can
            // only *request* a speed, and its request handler reads 0 as "start again" when the
            // world is already paused, so a colony saved paused would come back running. The
            // intent says what was meant.
            if (cameraRig != null && Directors != null)
            {
                var hud = GetComponent<Ui.HudShell>();
                _view.Apply(cameraRig, Directors,
                    setGameSpeed: speed =>
                    {
                        // Straight to the intent, bypassing the toggle, so a colony saved paused
                        // comes back paused — but the memory still hears about it, or the first
                        // unpause of a colony saved at triple speed would drop it to normal.
                        _speed.Remember(speed);
                        _world.Intents.Submit(new Intent(IntentKind.SetGameSpeed, default, speed));
                    },
                    hud: hud);
            }

            SessionChanged?.Invoke();
        }

        public void TeardownSession()
        {
            if (cameraRig != null)
            {
                if (Directors != null) Directors.Slice.LayerChanged -= OnActiveLayerChanged;
                cameraRig.GameSpeedRequested -= OnGameSpeedRequested;
            }
            if (_figures != null)
            {
                _figures.BlowLanded -= OnBlowLanded;
                _figures.LoadLifted -= OnLoadLifted;
                _figures.LoadSet -= OnLoadSet;
            }
            if (_renderer != null)
            {
                _renderer.FallingItems.ItemLanded -= OnLoadSet;
            }
            _audio?.Dispose();
            _daylight?.Dispose();
            // The corpses before the figures: a body still falling hands its lent figure back as
            // it goes, and after the figures that indexed a cleared list and threw out of the
            // teardown, which a pause on a death and a load reached (review, 2026-09-23).
            _corpses?.Dispose();
            _figures?.Dispose();
            _blood = null;
            _doors?.Dispose();
            _floaterView?.Dispose();
            _combatFeedback.Floaters.Clear();
            _combatFeedback.Blood.Clear();
            _combatFeedback.Blood = NoBloodEffects.Instance;
            _combatFeedback.Sounds.Clear();

            // The pictures go with the materials that painted them — a portrait outlives a colony
            // but not the materials it was rendered through, and a cached texture whose shader is
            // gone is worse than one render.
            _portraits?.Clear();
            if (_portraits != null) _portraits.Materials = null;
            _colonistMaterials?.Dispose();
            _renderer?.Dispose();
            _powerLines?.Dispose();
            _powerLines = null;
            // The library owns every mesh it baked or merged, and a Mesh made in code is a GPU
            // allocation Unity never collects. Leaving Play mode without this leaked the whole
            // cast, every session, until the graphics device was reset out from under the editor.
            _model?.Library.Dispose();
            if (_actorMaterial != null) Destroy(_actorMaterial);

            // Dropped, not merely disposed. A disposed object still reachable from here would let
            // the next session read a torn-down library and fail somewhere far from the cause.
            // Before the world goes, because the world holds the phase sink this owns.
            if (_world != null) _world.PhaseSink = null;
            _tracer?.Dispose();
            _tracer = null;

            _audio = null;
            _daylight = null;
            _figures = null;
            _doors = null;
            _corpses = null;
            _floaterView = null;
            _colonistMaterials = null;
            _renderer = null;
            _actorMaterial = null;
            _model = null;
            _colony = null;
            _world = null;
            _grid = null;
            _pawns = null;
            Directors = null;
            _accumulator = 0d;
            _tickAlpha = 0f;

            // The binding belongs to the session, not to the component. A new colony that inherited
            // the last one's file would overwrite it on its first Save, which is the worst of both
            // behaviours: a lost save and no prompt.
            BoundSavePath = null;
            _autosave.Forget();

            // Last, and after everything is null: whoever listens is about to ask whether a
            // session exists, and the answer has to already be no.
            SessionChanged?.Invoke();
        }
    }
}
