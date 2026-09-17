#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The HUD, rebuilt to the approved interface specification (2026-09-16).
    ///
    /// <para><b>What changed and why.</b> The first pass put every region of the panel catalogue
    /// on screen, which was the right thing to prove and the wrong thing to keep: it covered about
    /// a third of the viewport, printed its own debug codes ("A1 · RESOURCES") at the player,
    /// carried three development notes as if they were game text, put two or three letters of an
    /// icon key in a coloured tile wherever a picture was missing, and had accumulated fourteen
    /// font sizes and a hand-picked offset for every panel that had ever grown into its
    /// neighbour. This pass keeps every piece of information and sets it in a fixed type scale on
    /// smaller panels over two always-on scrims, which is what carries the contrast the panels
    /// used to carry themselves.</para>
    ///
    /// <para><b>Where the rules live now.</b> Type is <see cref="HudType"/>, colour and space are
    /// <see cref="HudTheme"/> and <see cref="HudLayout"/>, the command bar and its overflow are
    /// <see cref="HudCommands"/> — all in the Unity-free <c>Odyssey.Hud</c> assembly, so the
    /// acceptance criteria ("no two panels overlap at three resolutions", "coverage at or under
    /// eighteen per cent", "every command item shows a hotkey", "body text over 4.5:1") are tests
    /// in the fast tier rather than opinions in a review. This class realises those decisions and
    /// routes clicks; it decides nothing and it names nothing.</para>
    ///
    /// <para><b>Anchoring, rather than offsets.</b> Three times in this file's history two regions
    /// were given hand-picked offsets in one corner and one grew into the other. Every region here
    /// is anchored to a screen edge or centred by the layout engine, the right-hand column stacks
    /// the clock and the alerts so neither can be pinned under the other, and the command bar
    /// cannot wrap — anything that does not fit goes into Menu — so the inspect pane above it no
    /// longer has to guess how tall the bar is.</para>
    ///
    /// <para>Regions refresh on wall-clock cadence buckets (15 Hz / 4 Hz / 1 Hz), never per frame
    /// and never keyed to ticks: at 3x speed the tick rate triples and the HUD's must not. Element
    /// trees are built once and updated in place.</para>
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [RequireComponent(typeof(OdysseyBootstrap))]
    public sealed partial class HudShell : MonoBehaviour
    {
        [Tooltip("The HUD stylesheet. Assigned by the play-scene builder; authored at Assets/Odyssey/Presentation/Ui/Hud.uss.")]
        public StyleSheet? hudStyles;

        [Tooltip("Archivo Narrow, the interface face. Optional: without it the panel's theme font is used and only the letter shapes differ.")]
        public Font? uiFont;

        [Tooltip("IBM Plex Mono, the face every number is set in. Optional, as above.")]
        public Font? monoFont;

        const float FastBucketSeconds = 1f / 15f;   // roster, inspect pane
        const float MidBucketSeconds = 0.25f;       // stores, speed state, alerts
        const float SlowBucketSeconds = 1f;         // clock, rail

        readonly RosterModel _roster = new RosterModel();
        readonly InspectModel _inspect = new InspectModel();
        readonly LayerRulerModel _ruler = new LayerRulerModel();
        readonly LedgerModel _ledger = new LedgerModel();
        readonly AlertModel _alerts = new AlertModel();

        OdysseyBootstrap? _boot;
        SliceCameraRig? _rig;
        HudDirectors? _directors;

        /// <summary>The layer the colony actually stands on, which is where the depth rail divides
        /// sky from ground. Captured on the first frame a world exists, because component Start
        /// order on one GameObject is not defined and this must not race the bootstrap.</summary>
        int _surfaceLayer;
        bool _surfaceCaptured;

        float _fast;
        float _mid;
        float _slow;
        bool _primed;

        // ---- roots
        VisualElement _hud = null!;

        /// <summary>
        /// Everything that only means anything with a colony running — the bars, the panels, the
        /// rail, the two contrast scrims. Put away as one thing when no session is built.
        /// </summary>
        VisualElement _worldUi = null!;

        /// <summary>The picture behind the main menu, and the only thing on screen when there is
        /// no world to be the backdrop itself.</summary>
        VisualElement _backdrop = null!;

        /// <summary>
        /// Where the menu's backdrop is loaded from.
        ///
        /// <para><c>Resources</c> rather than a serialised field, because scenes here are generated
        /// by editor scripts and a field somebody has to remember to drag an image into is a field
        /// that is empty in every scene but the one they did it in. A missing file is not an error:
        /// the menu simply has no picture, which is what a clone without the art gets.</para>
        /// </summary>
        public const string MenuBackdropResource = "Odyssey/menu-backdrop";
        VisualElement _marquee = null!;
        VisualElement _armedBanner = null!;
        Label _armedWhat = null!;

        // ---- stores (A1)
        VisualElement _storesPanel = null!;
        VisualElement _storesRows = null!;
        Label _storesCount = null!;
        HudGlyph _storesChevron = null!;
        bool _storesExpanded;
        int _storesStocked = int.MinValue;
        int _storesTotal = int.MinValue;
        readonly List<StoreRowView> _storeRows = new List<StoreRowView>();

        // ---- colonist strip (A2)
        VisualElement _strip = null!;
        readonly List<CardView> _cards = new List<CardView>();
        int _stripCapacity = int.MaxValue;
        bool _sweepingRoster;

        // ---- clock and speed (A3/A4)
        Label _clockTime = null!;
        Label _clockDate = null!;
        readonly List<VisualElement> _speedButtons = new List<VisualElement>();

        // ---- alerts (A5)
        VisualElement _alertsPanel = null!;
        VisualElement _alertRows = null!;
        readonly List<AlertRowView> _alertViews = new List<AlertRowView>();

        // ---- depth rail (A11)
        VisualElement _railCells = null!;
        Label _railHint = null!;
        readonly List<RailCellView> _rail = new List<RailCellView>();
        float _railPitch = -1f;

        // ---- inspect (A9/A10)
        VisualElement _inspectPanel = null!;
        VisualElement _inspectBody = null!;
        string _inspectBuiltFor = string.Empty;
        Label _inspectTitle = null!;
        Label _inspectMeta = null!;
        Label _inspectState = null!;
        IconBadge _inspectAvatar = null!;
        readonly List<NeedView> _needs = new List<NeedView>();
        readonly List<SkillLineView> _skills = new List<SkillLineView>();
        readonly List<Label> _tabChips = new List<Label>();
        VisualElement? _needsGrid;
        VisualElement? _skillsGrid;
        Label _tombReason = null!;
        int _needRows;

        // What the two header lines were last built from, so they are not rebuilt fifteen times a
        // second out of values that have not moved.
        int _metaLayer = int.MinValue;
        string? _metaPosition;
        string? _stateJob;
        string? _stateBand;
        int _stateSelected = int.MinValue;
        string? _stateSite;

        // The pile count, held beside the site line above for the same reason: the state line is
        // interpolated, so it is rebuilt only when a value it quotes has moved.
        int _stateStack = int.MinValue;

        // What the avatar was last keyed with, so a tile whose answer changed under the selection
        // — a face mined through, a tree felled — swaps its icon without rebuilding the pane.
        string _inspectAvatarKey = string.Empty;

        // ---- the tile readout's rows, built once and updated in place
        VisualElement? _cellRowsGrid;
        readonly List<CellRowView> _cellRows = new List<CellRowView>();

        // ---- command bar (A8)
        VisualElement _barRow = null!;
        VisualElement _bar = null!;
        VisualElement _barDivider = null!;
        readonly List<VisualElement> _barItems = new List<VisualElement>();
        readonly List<float> _barWidths = new List<float>();
        VisualElement _menuItem = null!;
        VisualElement _menuPopup = null!;
        VisualElement _menuOverflow = null!;
        float _barMeasuredAt = -1f;
        int _barShown = -1;

        // ---- panels over the board
        VisualElement _buildPanel = null!;

        VisualElement _settingsPanel = null!;
        VisualElement _debugPanel = null!;
        VisualElement _interfaceSection = null!;
        VisualElement _graphicsSection = null!;
        VisualElement _audioSection = null!;
        VisualElement _keysSection = null!;
        VisualElement _exitRow = null!;
        Label _exitLabel = null!;
        readonly Dictionary<GraphicsOption, VisualElement> _settingRows = new();
        readonly Dictionary<SettingsTab, Label> _settingTabs = new();
        readonly Dictionary<int, Label> _scaleRungs = new();
        readonly Dictionary<int, Label> _cameraRungs = new();
        readonly Dictionary<BuildPaletteLayout, Label> _layoutRungs = new();
        readonly Dictionary<SettingsBus, FaderView> _busFaders = new();
        readonly Dictionary<HotkeyAction, KeyRowView> _keyRows = new();

        /// <summary>One volume row: its fader and the readout beside it, refreshed when the
        /// bus's value moves and never per frame.</summary>
        sealed class FaderView
        {
            public Slider Fader = null!;
            public Label Value = null!;
        }

        /// <summary>One binding row: its root and the caps of its two slots, for
        /// event-driven refresh. Strings are rebuilt on click, never per frame.</summary>
        sealed class KeyRowView
        {
            public VisualElement Root = null!;
            public readonly Label[] Caps = new Label[HotkeyDirector.SlotCount];
        }

        /// <summary>The command-bar Build cap and its item, so a rebind can move the legend
        /// with the key it names.</summary>
        Label _buildCap = null!;
        IconBadge? _buildIcon;
        VisualElement _buildItem = null!;
        string _buildTooltipLabel = "";

        /// <summary>Defaults-only binding map for the frames before the shell attaches to
        /// the colony's directors, and for harness scenes that build no world.</summary>
        HotkeyDirector? _hotkeysFallback;

        /// <summary>Our own copy of the panel settings, so that changing the interface scale does
        /// not write to the committed asset. See <see cref="ApplyUiScale"/>.</summary>
        PanelSettings? _panelCopy;

        Texture2D? _topRamp;
        Texture2D? _bottomRamp;

        /// <summary>
        /// A row's view, with the last values it was given beside it.
        ///
        /// <para><b>The cached values are not an optimisation for its own sake.</b> ADR 0003's
        /// flip condition F1 — asserted by <c>HudStressTests</c> — is that the HUD allocates
        /// nothing per frame in steady state, and every one of <c>ToString</c>, string
        /// interpolation and <c>+</c> allocates. UI Toolkit's own <c>text</c> setter compares
        /// before assigning, so the element is not the problem; <i>building</i> the string is.
        /// A refresh that knows nothing changed builds nothing.</para>
        /// </summary>
        class StoreRowView
        {
            public VisualElement Root = null!;
            public IconBadge Icon = null!;
            public Label Name = null!;
            public Label Value = null!;
            public HudGlyph Falling = null!;

            public int LastQuantity = int.MinValue;
            public string? SteadyTip;
            public string? FallingTip;
        }

        class CardView
        {
            public VisualElement Root = null!;
            public VisualElement Ring = null!;
            public Label Initial = null!;
            public Label Name = null!;
            public IconBadge JobIcon = null!;
            public Label Job = null!;

            public PawnId LastId;
            public int LastJob = int.MinValue;
            public int LastLayer = int.MinValue;
        }

        class AlertRowView
        {
            public VisualElement Root = null!;
            public HudGlyph Icon = null!;
            public Label Lead = null!;
            public Label Detail = null!;
            public string? LastLead;
        }

        class RailCellView
        {
            public VisualElement Root = null!;
            public Label Number = null!;
            public VisualElement Dot = null!;
            public int Layer;
            public int LastPawns = int.MinValue;
            public int LastOccupancy = int.MinValue;
        }

        sealed class NeedView
        {
            public VisualElement Root = null!;
            public IconBadge Icon = null!;
            public Label Name = null!;
            public Label Value = null!;
            public VisualElement Fill = null!;
            public int LastPercent = int.MinValue;
        }

        sealed class SkillLineView
        {
            public VisualElement Root = null!;
            public IconBadge Icon = null!;
            public Label Name = null!;

            /// <summary>The level, or the reason there is no level.</summary>
            public Label Value = null!;

            /// <summary>One or two lozenges, hidden at no passion.</summary>
            public VisualElement Passion = null!;

            public string LastKey = string.Empty;
            public int LastLevel = int.MinValue;
            public int LastPassion = int.MinValue;
            public bool LastLive;
        }

        /// <summary>
        /// One fact of the tile readout: the label in its fixed column, the value beside it.
        /// Held like a skill row — built once, updated in place, its last strings cached so a
        /// refresh that says the same thing writes nothing.
        /// </summary>
        sealed class CellRowView
        {
            public VisualElement Root = null!;
            public Label Name = null!;
            public Label Value = null!;
            public string LastName = string.Empty;
            public string LastValue = string.Empty;

            /// <summary>The type role the value was last set in, so weight follows pickability.</summary>
            public HudTextRole LastRole = HudTextRole.Meta;

            /// <summary>
            /// Whether this row is, for the tile currently held, the owner picker — the pane's one
            /// interactive fact. Rows are reused across tiles, so the affordance travels with the
            /// row's current meaning rather than being built into the element.
            /// </summary>
            public bool IsPick;

            /// <summary>The tint last applied to the value, so a redraw does not restyle on every frame.</summary>
            public HudColour? LastTint;

            /// <summary>The "this row does something" chevron, shown only while <see cref="IsPick"/>.</summary>
            public Label Chevron = null!;

            /// <summary>The box the value sits in, styled as a button while <see cref="IsPick"/>.</summary>
            public VisualElement PickBox = null!;

            /// <summary>The bed glyph before the value, shown only while <see cref="IsPick"/>.</summary>
            public HudGlyph Glyph = null!;
        }

        void Awake()
        {
            _boot = GetComponent<OdysseyBootstrap>();
            _rig = _boot.cameraRig;
        }

        void Start()
        {
            var doc = GetComponent<UIDocument>();

            // Before anything is built. Swapping a UIDocument's panel settings re-attaches its
            // root to a different panel, and doing that after the tree exists is asking the
            // engine to carry a live HUD across the change for us. Taking the copy first means
            // the interface scale only ever writes a number on an asset nobody else holds.
            EnsurePanelCopy(doc);

            var root = doc.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("[Hud] UIDocument has no root: the panel settings asset is missing or unassigned.");
                return;
            }
            if (hudStyles != null) root.styleSheets.Add(hudStyles);

            HudText.Ui = uiFont;
            HudText.Mono = monoFont;

            _hud = new VisualElement { name = "hud", pickingMode = PickingMode.Ignore };
            _hud.AddToClassList("hud");
            root.Add(_hud);

            // Behind everything, and only ever seen when there is no colony: with a world running
            // the backdrop is the world.
            BuildBackdrop();

            // Everything that is only meaningful with a colony running lives in here, so it can be
            // put away as one thing (owner, 2026-09-17: the in-game interface was showing through
            // the main menu's scrim).
            //
            // **A container rather than a class on the shell**, and the reason is mechanical: half
            // these regions set `style.display` in code as they come and go, and an inline style
            // beats a stylesheet rule in UI Toolkit — so `.hud--noworld .inspect { display: none }`
            // would be quietly ignored by exactly the panels most likely to be showing. Hiding the
            // parent cannot be argued with.
            _worldUi = new VisualElement { name = "world-ui", pickingMode = PickingMode.Ignore };
            _worldUi.AddToClassList("worldui");
            _hud.Add(_worldUi);

            BuildScrims();

            // The marquee is a picture, not a decision: it polls the rig's rect every frame rather
            // than subscribing to anything. Screen-bottom-left origins become panel-top-left ones
            // in UpdateMarquee.
            _marquee = new VisualElement { name = "marquee", pickingMode = PickingMode.Ignore };
            _marquee.AddToClassList("marquee");
            _marquee.style.display = DisplayStyle.None;
            _worldUi.Add(_marquee);

            // What the player is holding, and how to stop holding it. Above the command
            // bar, where the eye already goes for the bar and its popovers.
            _armedBanner = new VisualElement { name = "armed", pickingMode = PickingMode.Ignore };
            _armedBanner.AddToClassList("armed");
            _armedBanner.style.display = DisplayStyle.None;
            _armedWhat = HudText.Make(string.Empty, HudTextRole.Name, ussClass: "armed__what");
            _armedBanner.Add(_armedWhat);

            // A floor rather than a width, so the four orders all draw the same box and the banner
            // stops resizing under the eye every time the mode changes; an armed build is longer
            // and is allowed to push past it. Written from code because the number is derived from
            // names in the content registry, and a literal in the sheet would be a second copy of
            // an answer a rename can change.
            _armedBanner.style.minWidth = HudLayout.ArmedWidth;
            _worldUi.Add(_armedBanner);

            BuildStores();
            BuildStrip();
            BuildRightColumn();
            BuildGutter();
            BuildInspect();
            BuildBar();
            BuildPalette();
            BuildSettings();
            BuildDebug();

            // B18, last, so it is the top-most element in the tree and its scrim covers everything
            // above. Built whether or not a session exists, because the state it belongs to is the
            // one where none does.
            BuildStartScreen();

            // After it, so the naming prompt is above the start screen in the tree — it is raised
            // from in game today, but the two are both modals and the one raised last should win.
            BuildSavePrompt();

            _hud.RegisterCallback<GeometryChangedEvent>(_ => OnResized());

            // A session coming or going is the one thing that decides whether the start screen is
            // on screen, so it is driven by the event rather than polled: Update returns early
            // with no world, which is exactly when the start screen has to be visible.
            _boot!.SessionChanged += OnSessionChanged;
            OnSessionChanged();
        }

        void OnDestroy()
        {
            if (_boot != null)
            {
                _boot.SessionChanged -= OnSessionChanged;
                // The preferences outlive every session and this component, so a subscription left
                // on them is a leak that survives the scene.
                _boot.Preferences.Changed -= OnPreferencesChanged;
            }
            Detach();
            if (_topRamp != null) DestroyImmediate(_topRamp);
            if (_bottomRamp != null) DestroyImmediate(_bottomRamp);
            if (_panelCopy != null) DestroyImmediate(_panelCopy);
        }

        /// <summary>
        /// The directors are made by the bootstrap when the world is, which may be after this
        /// Start: component order on one GameObject is not defined. So the shell attaches to them
        /// on the first Update that finds them, and answers their events from then on.
        /// </summary>
        void Attach(HudDirectors directors)
        {
            _directors = directors;
            _directors.Selection.Changed += OnSelectionChanged;
            _directors.Slice.LayerChanged += OnLayerChanged;
            _directors.Settings.Changed += OnSettingsChanged;
            _directors.Settings.OptionChanged += OnSettingChanged;
            _directors.Settings.TabChanged += OnSettingsTabChanged;
            _directors.Settings.UiScaleChanged += OnUiScaleChanged;
            _directors.Settings.CameraSpeedChanged += OnCameraSpeedChanged;
            _directors.Settings.BuildPaletteLayoutChanged += OnBuildLayoutChanged;
            _directors.Settings.DeveloperOverlayChanged += OnDeveloperOverlayChanged;
            _directors.Settings.BusDbChanged += OnBusDbChanged;
            _directors.Settings.ExitChanged += OnExitChanged;
            _directors.Settings.RowRequested += OnSessionRow;
            _directors.Debug.Changed += OnDebugChanged;
            _directors.Hotkeys.BindingChanged += OnBindingChanged;
            _directors.Hotkeys.ListenChanged += OnListenChanged;
            _directors.Hotkeys.ConflictNoted += OnHotkeyConflict;

            // The palette is a view of the designate director, so it cannot be built until there
            // is one. Everything above this line is the shell catching up with state that already
            // existed; this is the one thing that did not exist at all until now.
            BindBuildPalette();

            // The panel may already disagree with the director by the time we get here: the
            // presenter seeds it from the scene and the screen and then lays stored preferences
            // over it, and both happen before the shell has found anything to attach to.
            OnSettingsChanged();
            OnSettingsTabChanged(_directors.Settings.Tab);
            OnUiScaleChanged(_directors.Settings.UiScale);
            OnCameraSpeedChanged(_directors.Settings.CameraSpeed);
            OnBuildLayoutChanged(_directors.Settings.BuildPaletteLayout);
            OnDeveloperOverlayChanged();
            foreach (SettingsBus bus in SettingsDirector.Buses) OnBusDbChanged(bus);
            OnExitChanged();
            foreach (GraphicsOption option in SettingsDirector.All) OnSettingChanged(option);
            RefreshKeyCaps();
            OnDebugChanged();
        }

        void Detach()
        {
            if (_directors == null) return;
            _directors.Selection.Changed -= OnSelectionChanged;
            _directors.Slice.LayerChanged -= OnLayerChanged;
            _directors.Settings.Changed -= OnSettingsChanged;
            _directors.Settings.OptionChanged -= OnSettingChanged;
            _directors.Settings.TabChanged -= OnSettingsTabChanged;
            _directors.Settings.UiScaleChanged -= OnUiScaleChanged;
            _directors.Settings.CameraSpeedChanged -= OnCameraSpeedChanged;
            _directors.Settings.BuildPaletteLayoutChanged -= OnBuildLayoutChanged;
            _directors.Settings.DeveloperOverlayChanged -= OnDeveloperOverlayChanged;
            _directors.Settings.BusDbChanged -= OnBusDbChanged;
            _directors.Settings.ExitChanged -= OnExitChanged;
            _directors.Settings.RowRequested -= OnSessionRow;
            _directors.Debug.Changed -= OnDebugChanged;
            _directors.Hotkeys.BindingChanged -= OnBindingChanged;
            _directors.Hotkeys.ListenChanged -= OnListenChanged;
            _directors.Hotkeys.ConflictNoted -= OnHotkeyConflict;
            _directors = null;
        }

        /// <summary>A slot opened or closed its wait for a key; the caps say which.</summary>
        void OnListenChanged() => RefreshKeyCaps();

        void OnEnable()
        {
            // The world's clicks must die at the panel edge, not sail through it onto the map.
            if (_rig != null) _rig.PointerOverInterface = PointOverUi;
        }

        void OnDisable()
        {
            if (_rig != null) _rig.PointerOverInterface = null;
        }

        /// <summary>
        /// A pointer position, as the mouse reports it, in this panel's coordinates.
        ///
        /// <para><b>The Y axis has to be turned over first, and that is the whole of it.</b>
        /// <c>Mouse.current.position</c> is screen space with its origin at the <i>bottom</i> left
        /// and y climbing upward; a UI Toolkit panel has its origin at the <i>top</i> left with y
        /// climbing downward. <see cref="RuntimePanelUtils.ScreenToPanel"/> resolves the panel's
        /// own scaling — which is why it cannot simply be divided out by hand — but it does not
        /// turn the axis over, so handing it a mouse position directly mirrors everything about
        /// the middle of the screen.</para>
        ///
        /// <para>Reported by the owner against the drag marquee: "the selectable box does not come
        /// from the mouse cursor but actually quite below it some distance... like the exact
        /// opposite side of the screen". <see cref="PointOverUi"/> had the same line and therefore
        /// the same fault, which is worse because it fails quietly: it decides whether a click
        /// belongs to the HUD or to the world. Both call sites go through here now.</para>
        /// </summary>
        Vector2 ToPanel(Vector2 screenPosition) =>
            RuntimePanelUtils.ScreenToPanel(
                _hud.panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));

        /// <summary>
        /// True when a screen position sits over a HUD region, so the rig can decide whether a
        /// press belonged to the world (input case 2 of design 09 §6).
        ///
        /// <para>The shell root and both scrims are non-pickable, so the 370 rows of gradient that
        /// carry the HUD's text contrast do not swallow clicks on the board behind them — which
        /// they would, silently, and over a third of the screen.</para>
        /// </summary>
        public bool PointOverUi(Vector2 screenPosition)
        {
            if (_hud == null || _hud.panel == null) return false;
            VisualElement? hit = _hud.panel.Pick(ToPanel(screenPosition));
            if (hit == null || hit == _hud) return false;

            // **The panel's root is not interface, it is the canvas the interface sits on.** It
            // covers the whole screen and is pickable by default, so counting it made every point
            // on the screen "over the interface" — and the rig gates hover, scroll and the left
            // press on exactly that answer, which is a build cursor that never appears anywhere
            // (owner, 2026-09-17: "the build cursor doesn't appear for walls, floors etc"). `_hud`
            // was already excluded for this reason; its parent needs excluding for the same one.
            return hit != hit.panel.visualTree;
        }

        /// <summary>Close the Build palette. The Escape half, called by <c>SettingsPresenter</c>.</summary>
        public void CloseBuildPalette() => SetBuildPalette(false);

        void Update()
        {
            var world = _boot!.World;
            if (world == null || _hud == null) return;
            if (_directors == null)
            {
                if (_boot.Directors == null) return;
                Attach(_boot.Directors);
            }

            _directors!.Refresh(world.Views.Current);

            if (!_surfaceCaptured)
            {
                _surfaceLayer = _directors.Slice.ActiveLayer;
                _surfaceCaptured = true;
            }

            // Prime on the first frame a world exists rather than waiting out the fastest cadence
            // bucket: a HUD that is empty for its first fraction of a second reads as broken, and
            // in the playmode harness a frame costs almost no real time at all.
            if (!_primed)
            {
                _primed = true;
                RefreshAll();
            }

            _fast += Time.unscaledDeltaTime;
            _mid += Time.unscaledDeltaTime;
            _slow += Time.unscaledDeltaTime;

            if (_fast >= FastBucketSeconds)
            {
                _fast = 0f;
                RefreshStrip();
                RefreshInspect();
            }
            if (_mid >= MidBucketSeconds)
            {
                _mid = 0f;
                RefreshStores();
                RefreshAlerts();
                RefreshSpeed();
                RefreshBuildPalette();
            }
            if (_slow >= SlowBucketSeconds)
            {
                _slow = 0f;
                RefreshClock();
                RefreshRail();
            }

            UpdateMarquee();
            UpdateArmedBanner();
            MarkOrders();
            ReadBarKeys();

            // The roster sweep ends when the button does, wherever the pointer happens to be when
            // it ends — a card's own PointerUp never arrives if the release landed off the strip.
            if (_sweepingRoster && UnityEngine.InputSystem.Mouse.current?.leftButton.isPressed != true)
                _sweepingRoster = false;
        }

        void RefreshAll()
        {
            RefreshStrip();
            RefreshInspect();
            RefreshStores();
            RefreshAlerts();
            RefreshSpeed();
            RefreshClock();
            RefreshRail();
        }

        /// <summary>
        /// The two decisions that depend on how wide the screen is: how many colonist cards the
        /// strip may hold without running into the panels either side of it, and how many command
        /// items fit on the bar before the rest go into Menu.
        /// </summary>
        void OnResized()
        {
            float width = _hud.resolvedStyle.width;
            if (width <= 0f) return;

            // The strip wraps to a second row, so its box has to be exactly the room it may
            // occupy — twice the smaller of its two clearances, not the screen — or the wrap
            // happens at the wrong width and the second row runs under the clock. The insets are
            // written here rather than in the sheet because that room is a function of the
            // viewport and the panels either side of it.
            float inset = Math.Max(0f, (width - HudLayout.StripRoom(width)) * 0.5f);
            if (_strip != null)
            {
                _strip.style.left = inset;
                _strip.style.right = inset;
            }

            float height = _hud.resolvedStyle.height;
            int capacity = Math.Max(1, HudLayout.VisibleCards(width, height, int.MaxValue / 2));
            if (capacity != _stripCapacity)
            {
                _stripCapacity = capacity;
                RefreshStrip();
            }

            FitRail();
            ReflowBar(width);
        }

        /// <summary>
        /// Squeeze the depth rail into the height it has.
        ///
        /// <para>The rail is the one region whose length is set by the world rather than by the
        /// interface: sixteen layers today, and the generator can be asked for more. At the
        /// specified 26x16 cells a thirty-two layer board does not fit a 720p screen, and what
        /// the rail must never do is silently lose its last layers — that was a playtest report
        /// on 2026-09-16, and the reason <c>HudSmokeTests.EveryLayerHasAStepThatCanBeHit</c>
        /// exists. So the cells shrink instead, in the proportion the specification draws them
        /// at, down to a floor that is still something a player can hit.</para>
        /// </summary>
        void FitRail()
        {
            float height = _hud.resolvedStyle.height;
            if (height <= 0f || _rail.Count == 0) return;

            float pitch = HudLayout.RailPitch(height, _rail.Count);
            if (Mathf.Approximately(pitch, _railPitch)) return;
            _railPitch = pitch;

            float gap = HudLayout.RailGap(pitch);
            float cell = HudLayout.RailCell(pitch);

            // The negative bottom margin cancels the last cell's own gap, so the rail's realised
            // height is exactly what HudLayout.RailHeight says it is.
            _railCells.style.marginTop = gap;
            _railCells.style.marginBottom = -gap;
            _railHint.style.marginTop = gap;

            foreach (RailCellView view in _rail)
            {
                view.Root.style.height = cell;
                view.Root.style.marginBottom = gap;

                // A squeezed cell has no room for its own number. The rail still says which layer
                // is live by which cell is lit, which is the part that has to survive.
                view.Number.style.display = cell >= HudLayout.RailCellHeight - 2f
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
        }

        /// <summary>
        /// The marquee follows the rig's box every frame — a 15 Hz marquee trails the cursor and
        /// reads as lag. Screen coordinates grow from the bottom-left and panel coordinates from
        /// the top-left, so the rect is flipped once, here, at the only place that draws it.
        /// </summary>
        /// <summary>
        /// A strip over the board saying what the player is holding, in that thing's own colour.
        ///
        /// <para><b>An armed tool was invisible, and the way out of it was a key nobody had
        /// been told about.</b> Escape has disarmed the tool since the settings panel landed —
        /// it is the first step of <c>SettingsDirector.Escape</c> — but nothing on screen said
        /// a tool was held, so the only evidence of build mode was that clicking stopped
        /// selecting things. Reported by the owner as being hard to get out of (2026-09-17).</para>
        ///
        /// <para><b>The border is the held order's hue, three pixels of it</b> (owner, same day:
        /// <i>"the border around the big dialog … should be the same colour as that order …
        /// chopping should have a green border … make that border much thicker"</i>). It is the
        /// same four tokens the orders strip paints its buttons with and the same idea the open
        /// palette wears along its top edge, so the button pressed, the panel and this banner
        /// cannot come to disagree about what colour a mode is. A build tool has no order hue and
        /// keeps the accent.</para>
        ///
        /// <para><b>The second line came off on the same instruction.</b> It read "drag over the
        /// board · right-click or Esc to stop" and was the only place the right-click gesture was
        /// written down — which is the one thing lost here, and it is recorded rather than
        /// glossed. What replaced it is not nothing: the orders strip lights the button that armed
        /// the tool and puts it down when pressed again, so the way out is now a thing on screen
        /// rather than a sentence about a key.</para>
        ///
        /// <para>Above the command bar rather than at the cursor: a cursor decoration is the
        /// conventional answer and cannot carry a word, let alone a colour this size.</para>
        /// </summary>
        void UpdateArmedBanner()
        {
            DesignateDirector? tool = _directors?.Designate;
            DesignateTool armed = tool?.Tool ?? DesignateTool.None;

            // Not while the Build palette is open. The banner floats in the middle of the screen
            // saying what is armed, and the open palette says the same thing in its own header,
            // in the armed tool's colour, a few pixels away — two labels about one tool, one of
            // them over the panel that set it. The banner is for the player who armed something
            // and then closed the palette, which is the case it was written for.
            if (armed == DesignateTool.None || BuildPaletteOpen)
            {
                _armedBanner.style.display = DisplayStyle.None;
                _armedFor = DesignateTool.None;
                _armedStuffFor = -1;
                return;
            }

            _armedBanner.style.display = DisplayStyle.Flex;
            int stuff = tool!.Stuff;
            if (_armedFor == armed && _armedStuffFor == stuff) return;

            _armedFor = armed;
            _armedStuffFor = stuff;

            // Which of the four orders is held, or empty for a build tool. Asked of the palette
            // model rather than switched on the tool here: ArmedPinned is what the orders strip
            // lights its button from, so one question answers the word, the colour and the lit
            // button, and the three cannot drift apart.
            string order = _palette?.ArmedPinned ?? string.Empty;

            // The order's own registry name — the same words the wiki prints, the palette's
            // breadcrumb says and the strip's tooltip repeats (owner: "keep the consistent in the
            // wiki and the language and UI"). This banner used to say "Chopping" and "Cancelling
            // orders", the second of them a C# literal, which is the failure the naming registry
            // exists to prevent.
            string building = Registry.Label("ui.status.building");
            string what = order.Length > 0
                ? PaletteTools.OrderWord(order)
                : BuildLabels.Building(tool.Building) is { Length: > 0 } name
                    ? building + " " + name.ToLowerInvariant() + " of " + BuildLabels.Stuff(stuff)
                    : building;

            HudText.Set(_armedWhat, what, HudTextRole.Name);

            // The border, in the held order's own colour — the same four tokens the strip paints
            // its buttons with. A build tool is not an order and has no hue of its own, which is
            // what the accent is doing here.
            HudColour hue = (order.Length > 0 ? HudTheme.PinnedActionHue(order) : null)
                            ?? HudTheme.Accent;
            Color edge = HudTokens.Convert(hue);
            _armedBanner.style.borderTopColor = _armedBanner.style.borderRightColor =
                _armedBanner.style.borderBottomColor = _armedBanner.style.borderLeftColor = edge;
        }

        DesignateTool _armedFor = DesignateTool.None;
        int _armedStuffFor = -1;

        void UpdateMarquee()
        {
            Rect? box = _rig?.DragBox;
            if (box == null || _hud.panel == null)
            {
                _marquee.style.display = DisplayStyle.None;
                return;
            }

            // Both corners through the same conversion. Screen min-y is the BOTTOM of the box and
            // panel min-y is the top, so which corner is which flips with the axis.
            Vector2 min = ToPanel(box.Value.min);
            Vector2 max = ToPanel(box.Value.max);
            _marquee.style.left = Mathf.Min(min.x, max.x);
            _marquee.style.top = Mathf.Min(min.y, max.y);
            _marquee.style.width = Mathf.Abs(max.x - min.x);
            _marquee.style.height = Mathf.Abs(max.y - min.y);
            _marquee.style.display = DisplayStyle.Flex;
        }

        void OnLayerChanged(int layer) => RefreshRail();

        /// <summary>
        /// The pick resolver has decided what a click landed on (or the strip has set the
        /// selection directly). Push it into the inspect model and answer in the same frame — a
        /// click that only shows up on the next cadence pass reads as ignored.
        /// </summary>
        void OnSelectionChanged(SelectionChange reason)
        {
            var world = _boot!.World;
            if (world == null || _directors == null) return;

            SelectionDirector selection = _directors.Selection;
            if (selection.HasPawn) _inspect.SetColonist(selection.Pawn);
            else if (selection.HasThing) _inspect.SetItem(selection.Thing);
            else if (selection.Cell is { } cell) _inspect.SetCell(cell);
            else _inspect.ClearSelection();

            _inspect.Refresh(world.Views.Current);
            RefreshInspect();
            RefreshStrip();
        }
    }
}
