#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Odyssey.Hud;
using Odyssey.Presentation.Audio;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
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
        float _smoothedFrameMs;
        string _catalogueNote = string.Empty;

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
            }

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
            // The rig sits on the camera, so its position is the viewer's.
            if (cameraRig != null) _renderer.ViewerPosition = cameraRig.transform.position;

            // Before anything reads the mirror, because the picker reads it and a waiting order is
            // one of the things a click can land on (WorldRenderModel.SetSites). Until this line
            // existed a site was drawn and not clickable: over open air the ray found nothing in
            // the column and the layer was dead to every tool, which is what the cancel tool could
            // not cancel on 2026-09-18.
            _model.SetSites(_world.Views.Current.Sites);
            int movePerTick = MovePerTick;

            // Before the world is submitted, because it decides how part of the world is drawn.
            // It reads the figures placed on the *previous* frame, which is the one frame of lag
            // this is worth: a tree fading a sixtieth of a second late is not observable, and
            // placing the figures first would mean drawing the world after the people in it.
            UpdateSightLines(_world.Views.Current, movePerTick);
            if (_renderer != null)
            {
                _renderer.FallingItems.UpdateSnapshot(_world.Views.Current);
                _renderer.FallingItems.Advance(Time.deltaTime);
                _renderer.Render(activeLayer, slice);
            }

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
                    _tickAlpha, movePerTick, _figures?.Drawn, _figures);

            _doors?.Sync(_world.Views.Current, activeLayer, slice, Time.deltaTime, _audio);

            DrawStandingOrders(_world.Views.Current);
            DrawBuildingSites(_world.Views.Current);
            DrawToolPreview();
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

            // A ladder's ghost stands on the face the built one will stand on: the wall it would be
            // fixed to if there is one, and the rotation the player has turned it to if there is
            // not. Asked of the model rather than worked out here, because that rule has one owner
            // and two systems have already disagreed about it once.
            Matrix4x4 placed =
                GroundRelief.Drape(CellMetrics.FloorCentre(cell.X, cell.Z, cell.Y));
            if (what.edifice == CoreContent.EdificeLadder && _model != null && _grid != null)
                placed *= Matrix4x4.Rotate(Quaternion.Euler(
                    0f, Directions.Yaw[_model.LadderFacing(_grid.Index(cell), facing)], 0f));

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

        void OnDestroy()
        {
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
                        _world.Intents.Submit(new Intent(IntentKind.SetGameSpeed, default, speed)),
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
            _figures?.Dispose();
            _doors?.Dispose();

            // The pictures go with the materials that painted them — a portrait outlives a colony
            // but not the materials it was rendered through, and a cached texture whose shader is
            // gone is worse than one render.
            _portraits?.Clear();
            if (_portraits != null) _portraits.Materials = null;
            _colonistMaterials?.Dispose();
            _renderer?.Dispose();
            // The library owns every mesh it baked or merged, and a Mesh made in code is a GPU
            // allocation Unity never collects. Leaving Play mode without this leaked the whole
            // cast, every session, until the graphics device was reset out from under the editor.
            _model?.Library.Dispose();
            if (_actorMaterial != null) Destroy(_actorMaterial);

            // Dropped, not merely disposed. A disposed object still reachable from here would let
            // the next session read a torn-down library and fail somewhere far from the cause.
            _audio = null;
            _daylight = null;
            _figures = null;
            _doors = null;
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

            // Last, and after everything is null: whoever listens is about to ask whether a
            // session exists, and the answer has to already be no.
            SessionChanged?.Invoke();
        }
    }
}
