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
    public sealed class HudShell : MonoBehaviour
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
        VisualElement _marquee = null!;

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
        VisualElement _buildTools = null!;
        int _buildCategory = -1;
        VisualElement _settingsPanel = null!;
        VisualElement _interfaceSection = null!;
        VisualElement _graphicsSection = null!;
        readonly Dictionary<GraphicsOption, VisualElement> _settingRows = new();
        readonly Dictionary<SettingsTab, Label> _settingTabs = new();
        readonly Dictionary<int, Label> _scaleRungs = new();

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
            public VisualElement Swatch = null!;
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

            BuildScrims();

            // The marquee is a picture, not a decision: it polls the rig's rect every frame rather
            // than subscribing to anything. Screen-bottom-left origins become panel-top-left ones
            // in UpdateMarquee.
            _marquee = new VisualElement { name = "marquee", pickingMode = PickingMode.Ignore };
            _marquee.AddToClassList("marquee");
            _marquee.style.display = DisplayStyle.None;
            _hud.Add(_marquee);

            BuildStores();
            BuildStrip();
            BuildRightColumn();
            BuildRail();
            BuildInspect();
            BuildBar();
            BuildPalette();
            BuildSettings();

            _hud.RegisterCallback<GeometryChangedEvent>(_ => OnResized());
        }

        void OnDestroy()
        {
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

            // The panel may already disagree with the director by the time we get here: the
            // presenter seeds it from the scene and the screen and then lays stored preferences
            // over it, and both happen before the shell has found anything to attach to.
            OnSettingsChanged();
            OnSettingsTabChanged(_directors.Settings.Tab);
            OnUiScaleChanged(_directors.Settings.UiScale);
            foreach (GraphicsOption option in SettingsDirector.All) OnSettingChanged(option);
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
            _directors = null;
        }

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
            return hit != null && hit != _hud;
        }

        /// <summary>Whether the Build palette is open, for whoever owns the Escape key.</summary>
        public bool BuildPaletteOpen => _buildPanel != null && _buildPanel.style.display == DisplayStyle.Flex;

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
            }
            if (_slow >= SlowBucketSeconds)
            {
                _slow = 0f;
                RefreshClock();
                RefreshRail();
            }

            UpdateMarquee();
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

        // ============================================================ shared furniture

        /// <summary>
        /// A panel: the fill, the hairline, the radius and the padding, anchored by the caller.
        /// Every framed region on the screen is one of these, and <c>HudSmokeTests</c> counts
        /// them by name.
        /// </summary>
        static VisualElement Panel(string name, params string[] extraClasses)
        {
            var panel = new VisualElement { name = name };
            panel.AddToClassList("panel");
            panel.AddToClassList("region");   // the smoke test's name for a framed region
            foreach (string extra in extraClasses) panel.AddToClassList(extra);
            return panel;
        }

        /// <summary>A panel's label row: the 11 px tracked capitals, and whatever sits opposite.</summary>
        static VisualElement Header(VisualElement panel, string label, out Label title)
        {
            var row = new VisualElement();
            row.AddToClassList("panel__hdr");
            title = HudText.Make(label, HudTextRole.PanelLabel, ussClass: "panel__label");
            row.Add(title);
            panel.Add(row);
            return row;
        }

        /// <summary>
        /// The close button every window carries in its top right (owner, 2026-09-17: "all windows
        /// can be escaped but also should have an X in the top right … like the one used in the
        /// tile selection").
        ///
        /// <para>It is the inspect pane's own control, lifted out rather than reinvented: the
        /// same glyph, the same 26 px box, the same red hairline on hover. A second close button
        /// that looked slightly different would be the interface disagreeing with itself about
        /// what closing means.</para>
        /// </summary>
        static VisualElement CloseButton(VisualElement header, string what, Action onClose)
        {
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            header.Add(spacer);

            var close = new VisualElement();
            close.AddToClassList("inspect__close");
            close.AddToClassList("panel__close");
            close.Add(new HudGlyph(HudGlyphKind.Close, 14f, HudTokens.TextDim));
            close.tooltip = "Close " + what + " — Esc";
            close.RegisterCallback<ClickEvent>(_ => onClose());
            header.Add(close);
            return close;
        }

        /// <summary>
        /// A panel raised from the command bar: the one rule, in one place.
        ///
        /// <para>Anchored to the button that raised it, flush on the bar with no gap, less
        /// transparent than a board panel, an X in the header and closed by Escape. Every popover
        /// is built through here, so "consistent" is a property of the code rather than a
        /// convention three call sites have to remember.</para>
        /// </summary>
        VisualElement Popover(string name, string label, Action onClose, params string[] extraClasses)
        {
            VisualElement panel = Window(name, label, onClose, extraClasses);
            panel.AddToClassList("popover");
            return panel;
        }

        /// <summary>
        /// A panel the player opened and is looking at, as against a board panel read while
        /// watching the world: the opaque fill and the close X, which are the two halves of the
        /// owner's rule. Every window is built through here, so "consistent" is a property of the
        /// code rather than a convention three call sites have to remember.
        /// </summary>
        VisualElement Window(string name, string label, Action onClose, params string[] extraClasses)
        {
            var panel = Panel(name, extraClasses);
            panel.AddToClassList("window");
            VisualElement header = Header(panel, label, out _);
            CloseButton(header, label, onClose);
            panel.style.display = DisplayStyle.None;
            return panel;
        }

        /// <summary>
        /// Put a popover over the button that raised it.
        ///
        /// <para>Written from code because where it sits is a fact about the bar, and only the
        /// laid-out bar knows where its buttons are: the reflow moves them as items go into Menu,
        /// and the interface scale moves them again. The arithmetic itself is
        /// <see cref="HudLayout.PopoverLeft"/>, in the assembly the fast tier can read.</para>
        /// </summary>
        void PlacePopover(VisualElement popover, VisualElement anchor)
        {
            float screen = _hud.resolvedStyle.width;
            float width = popover.resolvedStyle.width;
            if (float.IsNaN(width) || width <= 1f) width = popover.worldBound.width;

            Rect button = anchor.worldBound;
            popover.style.left = HudLayout.PopoverLeft(button.xMin, width, screen);
            popover.style.bottom = HudLayout.PopoverBottom;
        }

        // ============================================================ scrims

        /// <summary>
        /// The two gradients that carry the HUD's text contrast.
        ///
        /// <para><b>They are the reason the panels could shrink.</b> A panel dark enough to hold
        /// 13 px text over bright terrain has to be nearly opaque, and a screen of nearly opaque
        /// panels is the 31% coverage this rebuild was asked to halve. A scrim costs no panel area
        /// at all: it is a transparent ramp, it is never a pointer target, and with it the fill
        /// can drop to 86% and the panels can stop being walls.</para>
        ///
        /// <para>USS has no gradient property, so each is a one-pixel-wide ramp texture stretched
        /// over its element — one 1x64 texture apiece for the whole HUD, no shader and no pass.</para>
        /// </summary>
        void BuildScrims()
        {
            Color ink = HudTokens.ScrimInk;
            Color clear = new Color(ink.r, ink.g, ink.b, 0f);

            _topRamp = HudTokens.VerticalRamp(clear, new Color(ink.r, ink.g, ink.b, HudTheme.TopScrimAlpha));
            _bottomRamp = HudTokens.VerticalRamp(new Color(ink.r, ink.g, ink.b, HudTheme.BottomScrimAlpha), clear);

            _hud.Add(Scrim("scrim-top", _topRamp, top: true, HudTheme.TopScrimHeight));
            _hud.Add(Scrim("scrim-bottom", _bottomRamp, top: false, HudTheme.BottomScrimHeight));
        }

        static VisualElement Scrim(string name, Texture2D ramp, bool top, int height)
        {
            // Never a pointer target. A pickable scrim would eat every click in the top 170 and
            // bottom 200 rows of the screen, which is a third of the board, silently.
            var scrim = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            scrim.AddToClassList("scrim");
            scrim.style.height = height;
            if (top) scrim.style.top = 0;
            else scrim.style.bottom = 0;
            scrim.style.backgroundImage = new StyleBackground(ramp);
            return scrim;
        }

        // ============================================================ A1 stores

        void BuildStores()
        {
            _storesPanel = Panel("stores", "stores");
            Header(_storesPanel, "Stores", out _);

            // The header's right-hand side: how many rows have anything in them, and the
            // disclosure that shows the ones that do not.
            VisualElement header = _storesPanel.Q(className: "panel__hdr");
            var right = new VisualElement();
            right.AddToClassList("panel__hdrright");
            _storesCount = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true, "stores__count");
            _storesChevron = new HudGlyph(HudGlyphKind.ChevronDown, 14f, HudTokens.TextDim);
            right.Add(_storesCount);
            right.Add(_storesChevron);
            header.Add(right);
            header.RegisterCallback<ClickEvent>(_ => ToggleStores());
            header.tooltip = "Show or hide the commodities the colony has none of";

            _storesRows = new VisualElement();
            _storesRows.AddToClassList("stores__rows");
            _storesPanel.Add(_storesRows);
            _hud.Add(_storesPanel);
        }

        void ToggleStores()
        {
            _storesExpanded = !_storesExpanded;
            _storesChevron.Kind = _storesExpanded ? HudGlyphKind.ChevronUp : HudGlyphKind.ChevronDown;
            RefreshStores();
        }

        void RefreshStores()
        {
            var world = _boot!.World;
            if (world == null) return;
            _ledger.Refresh(world.Views.Current, Time.unscaledTimeAsDouble);

            while (_storeRows.Count < _ledger.Rows.Count) _storeRows.Add(NewStoreRow());
            while (_storeRows.Count > _ledger.Rows.Count)
            {
                _storeRows[^1].Root.RemoveFromHierarchy();
                _storeRows.RemoveAt(_storeRows.Count - 1);
            }

            for (int i = 0; i < _ledger.Rows.Count; i++)
            {
                LedgerRow model = _ledger.Rows[i];
                StoreRowView view = _storeRows[i];

                if (view.Icon.Key != model.IconKey)
                {
                    view.Icon.SetKey(model.IconKey);
                    HudText.Set(view.Name, model.Name, HudTextRole.Row);
                    view.SteadyTip = model.Real
                        ? model.Name + " — counted from the published frame"
                        : model.Name + " — arrives with the economy (M4)";
                    view.FallingTip = model.Name + " — counted from the published frame, and falling";
                }

                if (view.LastQuantity != model.Quantity)
                {
                    view.LastQuantity = model.Quantity;
                    HudText.Set(view.Value, model.Quantity.ToString(), HudTextRole.Row);
                }

                bool falling = model.Trend == StockTrend.Falling;
                view.Falling.style.display = falling ? DisplayStyle.Flex : DisplayStyle.None;
                view.Root.EnableInClassList("stores__row--falling", falling);
                view.Root.EnableInClassList("stores__row--zero", model.Zero);
                view.Root.tooltip = falling && model.Real ? view.FallingTip : view.SteadyTip;

                // Zero rows are folded away by default. Not deleted: the disclosure is how a
                // player finds out what the economy will eventually hold, and a row that vanishes
                // when its last item is hauled away is a row that reads as a bug.
                view.Root.style.display = model.Zero && !_storesExpanded
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            if (_storesStocked != _ledger.Stocked || _storesTotal != _ledger.Total)
            {
                _storesStocked = _ledger.Stocked;
                _storesTotal = _ledger.Total;
                HudText.Set(_storesCount, $"{_storesStocked} / {_storesTotal}", HudTextRole.Meta);
            }
        }

        StoreRowView NewStoreRow()
        {
            var row = new VisualElement();
            row.AddToClassList("stores__row");

            // Categorised: stores is one of the two places the spec allows an icon to carry a
            // colour of its own, and it is a stroke colour, never a filled tile behind the value.
            var icon = new IconBadge(string.Empty, IconBadge.RowSize, categorised: true);
            var name = HudText.Make(string.Empty, HudTextRole.Row, ussClass: "stores__name");
            var falling = new HudGlyph(HudGlyphKind.ChevronDown, 12f, HudTokens.Warn);
            falling.AddToClassList("stores__falling");
            var value = HudText.Make(string.Empty, HudTextRole.Row, numeric: true, "stores__value");

            row.Add(icon);
            row.Add(name);
            row.Add(falling);
            row.Add(value);
            _storesRows.Add(row);

            return new StoreRowView
            {
                Root = row, Icon = icon, Name = name, Value = value, Falling = falling,
            };
        }

        // ============================================================ A2 colonist strip

        void BuildStrip()
        {
            // A full-width row that centres its cards, rather than a panel placed at a computed x.
            // Centring is what the layout engine is for, and the model's own centring arithmetic
            // then describes what the engine will do rather than competing with it.
            _strip = new VisualElement { name = "strip", pickingMode = PickingMode.Ignore };
            _strip.AddToClassList("strip");
            _hud.Add(_strip);
        }

        void RefreshStrip()
        {
            var world = _boot!.World;
            if (world == null) return;
            _roster.Refresh(world.Views.Current,
                selected: _directors != null ? _directors.Selection.Pawns : (IReadOnlyList<PawnId>)Array.Empty<PawnId>());

            int shown = Math.Min(_roster.Cards.Count, _stripCapacity);

            while (_cards.Count < shown) _cards.Add(NewCard(_cards.Count));
            while (_cards.Count > shown)
            {
                _cards[^1].Root.RemoveFromHierarchy();
                _cards.RemoveAt(_cards.Count - 1);
            }

            for (int i = 0; i < shown; i++)
            {
                RosterCard model = _roster.Cards[i];
                CardView view = _cards[i];

                if (view.LastId != model.Id)
                {
                    view.LastId = model.Id;
                    HudText.Set(view.Name, model.Name, HudTextRole.Row);
                    HudText.Set(view.Initial, Initial(model.Name), HudTextRole.Row);
                }
                if (view.LastJob != model.JobDef)
                {
                    HudText.Set(view.Job, JobLabels.Label(model.JobDef), HudTextRole.Meta);
                    // A same-key call does nothing, so a colonist moving between two jobs that
                    // read as idle retargets nothing at all.
                    view.JobIcon.SetKey(JobLabels.IconKey(model.JobDef));
                }

                view.Root.EnableInClassList("card--sel", model.Selected);
                view.Ring.style.display = model.Selected ? DisplayStyle.Flex : DisplayStyle.None;

                // The layer is deliberately not on the card any more (spec): the depth rail states
                // it once and the inspect pane states it again for whoever is selected. It stays
                // in the tooltip, where it costs no pixels — and is rebuilt only when it would
                // read differently, because building a string every refresh is what ADR 0003's
                // flip condition F1 forbids.
                if (view.LastJob != model.JobDef || view.LastLayer != model.Layer)
                {
                    view.LastJob = model.JobDef;
                    view.LastLayer = model.Layer;
                    view.Root.tooltip =
                        $"{model.Name} — {JobLabels.Label(model.JobDef)}, layer {model.Layer}. Click to select.";
                }
            }
        }

        CardView NewCard(int index)
        {
            var card = new VisualElement();
            card.AddToClassList("card");

            // The selection ring: UI Toolkit has no box-shadow, so the spec's
            // "0 0 0 1px rgba(111,211,227,.35)" outside the accent border is an inset element.
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("card__ring");
            ring.style.display = DisplayStyle.None;
            card.Add(ring);

            var top = new VisualElement();
            top.AddToClassList("card__top");

            var avatar = new VisualElement();
            avatar.AddToClassList("card__avatar");
            avatar.style.backgroundColor = HudTokens.Category(HudCategory.People);
            Label initial = HudText.Make(string.Empty, HudTextRole.Row, ussClass: "card__initial");
            avatar.Add(initial);

            var names = new VisualElement();
            names.AddToClassList("card__names");
            Label name = HudText.Make(string.Empty, HudTextRole.Row, ussClass: "card__name");
            names.Add(name);

            top.Add(avatar);
            top.Add(names);
            card.Add(top);

            // The job goes on its own line under the avatar row, not in the strip beside the
            // avatar. The whole width of the card is what lets it be a full word: an ellipsis is
            // allowed on a colonist's name and on nothing else.
            //
            // The icon leads the word rather than replacing it (owner, 2026-09-16). Uncategorised,
            // because the spec gives a colour of its own only to stores and the command bar; here
            // it takes the ink of the word beside it, which is what makes the line read as one
            // thing. It is not hidden where a key has no art: the slot is always occupied, so the
            // word does not shift sideways as a colonist changes job, and the outlined square
            // says a picture belongs there — which is true, and is the same thing it says
            // everywhere else in the HUD.
            var jobRow = new VisualElement { pickingMode = PickingMode.Ignore };
            jobRow.AddToClassList("card__jobrow");
            var jobIcon = new IconBadge(JobLabels.IconKey(-1), IconBadge.RowSize);
            jobIcon.Inherit(HudTokens.TextDim);
            Label job = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "card__job");
            jobRow.Add(jobIcon);
            jobRow.Add(job);
            card.Add(jobRow);

            // Shift is the strip's toggle, exactly as it is in the world: a shift-press on a card
            // turns it on or off without moving the camera, and while shift is held a drag across
            // cards toggles each one it crosses (A2 "drag-select a range"). A plain press keeps the
            // jump: a card is a way of getting to someone far away.
            card.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (index >= _roster.Cards.Count || _boot!.World == null) return;
                PawnId id = _roster.Cards[index].Id;
                if (evt.shiftKey)
                {
                    _sweepingRoster = true;
                    _directors?.Selection.Toggle(id);
                }
                else _directors?.ChooseColonist(id, _boot.World.Views.Current);
            });
            card.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (!_sweepingRoster || index >= _roster.Cards.Count) return;
                _directors?.Selection.Toggle(_roster.Cards[index].Id);
            });

            _strip.Add(card);
            return new CardView
            {
                Root = card, Ring = ring, Initial = initial, Name = name,
                JobIcon = jobIcon, Job = job,
            };
        }

        static string Initial(string name) =>
            string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();

        // ============================================================ A3/A4 clock and speed, A5 alerts

        /// <summary>
        /// The right-hand column: the clock with the speed buttons under it, then the alerts.
        ///
        /// <para><b>This merge is the fix for a specific accident.</b> The clock and the alerts
        /// were two absolutely positioned panels in the same corner with hand-picked tops, and the
        /// clock grew past the 122 px the alerts panel's fixed top allowed it, burying the speed
        /// buttons underneath. In a column, whatever height each region turns out to be, the next
        /// one starts below it — and the spec's instruction is blunter still: never stack two
        /// panels in the same corner again.</para>
        /// </summary>
        void BuildRightColumn()
        {
            var column = new VisualElement { name = "right-column", pickingMode = PickingMode.Ignore };
            column.AddToClassList("column-right");
            _hud.Add(column);

            // ---- clock and speed, one panel
            VisualElement clock = Panel("clock", "clock");

            var line = new VisualElement();
            line.AddToClassList("clock__line");
            _clockTime = HudText.Make(string.Empty, HudTextRole.Clock, numeric: true, "clock__time");
            _clockDate = HudText.Make(string.Empty, HudTextRole.Body, ussClass: "clock__date");
            line.Add(_clockTime);
            line.Add(_clockDate);
            clock.Add(line);

            var speed = new VisualElement();
            speed.AddToClassList("speed");
            (HudGlyphKind glyph, string name)[] speeds =
            {
                (HudGlyphKind.Pause, "Pause"),
                (HudGlyphKind.Play, "Play"),
                (HudGlyphKind.Forward, "Double speed"),
                (HudGlyphKind.FastForward, "Triple speed"),
            };
            for (int i = 0; i < speeds.Length; i++)
            {
                int requested = i;      // 0 paused, 1..3 speeds — the rig's own convention
                var button = new VisualElement();
                button.AddToClassList("speed__btn");
                var glyph = new HudGlyph(speeds[i].glyph, 14f, HudTokens.TextPrimary);
                glyph.AddToClassList("speed__glyph");
                button.Add(glyph);
                button.tooltip = speeds[i].name + " — Space pauses, 1/2/3 set speed";
                button.RegisterCallback<ClickEvent>(_ => _rig?.RequestGameSpeed(requested));
                speed.Add(button);
                _speedButtons.Add(button);
            }
            clock.Add(speed);
            column.Add(clock);

            // ---- alerts, under it in the same column
            _alertsPanel = Panel("alerts", "alerts");
            Header(_alertsPanel, "Alerts", out _);
            _alertRows = new VisualElement();
            _alertRows.AddToClassList("alerts__rows");
            _alertsPanel.Add(_alertRows);

            // Hidden outright when there is nothing to say. The panel it replaces printed "No
            // active alerts." above a sentence explaining that conditions arrive with M2, which is
            // a development note sitting in a player-facing region.
            _alertsPanel.style.display = DisplayStyle.None;
            column.Add(_alertsPanel);
        }

        void RefreshClock()
        {
            var world = _boot!.World;
            if (world == null) return;
            long tick = world.CurrentTick;
            HudText.Set(_clockTime, $"{GameClock.HourOfDay(tick):00}:00", HudTextRole.Clock);
            HudText.Set(_clockDate,
                $"Day {GameClock.DayOfMonth(tick)} · {GameClock.MonthName(tick)} · {GameClock.SeasonName(tick)}",
                HudTextRole.Body);
        }

        void RefreshSpeed()
        {
            var world = _boot!.World;
            if (world == null) return;
            int current = world.GameSpeed;
            for (int i = 0; i < _speedButtons.Count; i++)
            {
                bool on = i == current;
                _speedButtons[i].EnableInClassList("speed__btn--on", on);
                if (_speedButtons[i].Q<HudGlyph>() is { } glyph)
                    glyph.Tint = on ? HudTokens.OnAccent : HudTokens.TextPrimary;
            }
        }

        void RefreshAlerts()
        {
            var world = _boot!.World;
            if (world == null) return;
            _alerts.Refresh(world.Views.Current, Time.unscaledTimeAsDouble);

            while (_alertViews.Count < _alerts.Rows.Count) _alertViews.Add(NewAlertRow());
            while (_alertViews.Count > _alerts.Rows.Count)
            {
                _alertViews[^1].Root.RemoveFromHierarchy();
                _alertViews.RemoveAt(_alertViews.Count - 1);
            }

            for (int i = 0; i < _alerts.Rows.Count; i++)
            {
                AlertRow model = _alerts.Rows[i];
                AlertRowView view = _alertViews[i];

                Color ink = model.Severity switch
                {
                    AlertSeverity.Danger => HudTokens.Bad,
                    AlertSeverity.Warning => HudTokens.Warn,
                    _ => HudTokens.Info,
                };
                view.Icon.Kind = model.Severity == AlertSeverity.Notice
                    ? HudGlyphKind.Info
                    : HudGlyphKind.AlertTriangle;
                view.Icon.Tint = ink;

                // The actionable clause in full ink, the detail trailing in meta. A player who
                // reads only the leads should know what to do.
                //
                // The model hands back the same string instance while the alert stands, so the
                // reference comparison is exact and the two strings below are built once per
                // change of state rather than four times a second.
                if (!ReferenceEquals(view.LastLead, model.Lead))
                {
                    view.LastLead = model.Lead;
                    HudText.Set(view.Lead, model.Lead, HudTextRole.Body);
                    HudText.Set(view.Detail, " — " + model.Detail, HudTextRole.Body);
                    view.Root.tooltip = $"{model.Lead} — {model.Detail} ({model.Count})";
                }
            }

            _alertsPanel.style.display =
                _alerts.Rows.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        AlertRowView NewAlertRow()
        {
            var row = new VisualElement();
            row.AddToClassList("alert");

            var icon = new HudGlyph(HudGlyphKind.AlertTriangle, IconBadge.BarSize, HudTokens.Warn);
            icon.AddToClassList("alert__icon");

            var text = new VisualElement();
            text.AddToClassList("alert__text");
            Label lead = HudText.Make(string.Empty, HudTextRole.Body, ussClass: "alert__lead");
            Label detail = HudText.Make(string.Empty, HudTextRole.Body, ussClass: "alert__detail");
            text.Add(lead);
            text.Add(detail);

            row.Add(icon);
            row.Add(text);
            _alertRows.Add(row);

            return new AlertRowView { Root = row, Icon = icon, Lead = lead, Detail = detail };
        }

        // ============================================================ A11 depth rail

        void BuildRail()
        {
            VisualElement rail = Panel("rail", "rail");
            Header(rail, "Depth", out _);

            _railCells = new VisualElement();
            _railCells.AddToClassList("rail__cells");
            rail.Add(_railCells);

            _railHint = HudText.Make("R / F", HudTextRole.Meta, numeric: false, "rail__hint");
            _railHint.tooltip = "R and F move the slice up and down. Home recentres.";
            rail.Add(_railHint);

            _hud.Add(rail);
        }

        /// <summary>
        /// One cell per layer, the top cell the top layer, so the rail reads downwards like depth
        /// does. Above ground is pale and below ground is dark, which is the one thing a depth
        /// readout has to say at a glance; the live layer is filled and carries its own number.
        /// </summary>
        void RefreshRail()
        {
            if (_directors == null) return;
            var world = _boot!.World;
            if (world == null) return;
            _ruler.Refresh(world.Views.Current, _directors.Slice.ActiveLayer, _surfaceLayer);

            while (_rail.Count < _ruler.Rows.Count)
            {
                // Rows come out of the model top layer first and the rail is a column, so the
                // first cell built is the top cell and cell i is Rows[i]. This used to index from
                // the far end, which built the bar upside down: clicking low on the rail took you
                // high, reported from a playtest on 2026-09-16.
                int layer = _ruler.Rows[_rail.Count].Layer;

                var cell = new VisualElement();
                cell.AddToClassList("rail__cell");
                cell.AddToClassList("ruler__tick");   // the name the playmode gate knows it by

                Label number = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true, "rail__number");
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.AddToClassList("rail__dot");
                cell.Add(number);
                cell.Add(dot);

                int clicked = layer;
                cell.RegisterCallback<ClickEvent>(_ => _directors?.Slice.SetLayer(clicked));

                // What the cell will DO, hung on the element so a test can read it. Without it the
                // only observable is the lit cell, and the lit cell cannot tell two bugs apart:
                // build the rail upside down, read it with a mirrored index, and the highlight
                // lands correctly while the click still goes elsewhere.
                cell.userData = clicked;
                _railCells.Add(cell);
                _rail.Add(new RailCellView { Root = cell, Number = number, Dot = dot, Layer = layer });
                _railPitch = -1f;   // a rail that has grown has to be measured again
            }
            if (_rail.Count != _ruler.Rows.Count) return;
            FitRail();

            for (int i = 0; i < _rail.Count; i++)
            {
                RailCellView view = _rail[i];

                // By index, not by layer number. Rows[i].Layer is layers-1-i, so indexing the list
                // with a layer number reads a different row for every layer but the middle one.
                LayerRow model = _ruler.Rows[i];

                view.Root.EnableInClassList("rail__cell--active", model.Active);
                view.Root.EnableInClassList("rail__cell--above", model.Layer > _surfaceLayer);
                view.Root.EnableInClassList("rail__cell--surface", model.Surface);
                view.Root.EnableInClassList("ruler__tick--active", model.Active);

                HudText.Set(view.Number, model.Active ? model.Layer.ToString() : string.Empty,
                    HudTextRole.Meta);
                view.Dot.style.display =
                    model.Pawns > 0 && !model.Active ? DisplayStyle.Flex : DisplayStyle.None;

                // Rebuilt only when it would read differently. The occupancy is compared as the
                // whole percent the tooltip prints rather than as a float, so a figure drifting
                // in the fourth decimal does not rebuild a sentence once a second for ever.
                int occupancyPercent = model.Occupancy >= 0f ? Mathf.RoundToInt(model.Occupancy * 100f) : -1;
                if (view.LastPawns != model.Pawns || view.LastOccupancy != occupancyPercent)
                {
                    view.LastPawns = model.Pawns;
                    view.LastOccupancy = occupancyPercent;

                    string surface = model.Surface ? " (surface)" : string.Empty;
                    string occupancy = occupancyPercent >= 0
                        ? $"{occupancyPercent}% built"
                        : "occupancy publishes for the active slice only";
                    view.Root.tooltip =
                        $"Layer {model.Layer}{surface} — {model.Pawns} colonists, {occupancy}. Click to move the slice.";
                }
            }
        }

        // ============================================================ A9/A10 inspect

        void BuildInspect()
        {
            _inspectPanel = Panel("inspect", "inspect");
            _inspectBody = new VisualElement();
            _inspectBody.AddToClassList("inspect__body");
            _inspectPanel.Add(_inspectBody);

            // Hidden from the first frame. Nothing is selected when a game starts, and a pane that
            // appeared for one frame before the first refresh took it away would be the kind of
            // flicker nobody can reproduce on demand.
            _inspectPanel.style.display = DisplayStyle.None;
            _hud.Add(_inspectPanel);
        }

        void RefreshInspect()
        {
            var world = _boot!.World;
            if (world == null || _inspectBody == null) return;
            _inspect.Refresh(world.Views.Current);

            // The structure is rebuilt only when the subject changes; values update in place, so a
            // refresh allocates nothing but the few strings it shows.
            string signature =
                _inspect.Subject + ":" +
                (_inspect.Subject == InspectSubject.Colonist ? _inspect.Pawn.ToString()
                 : _inspect.Subject == InspectSubject.Item ? _inspect.Thing.ToString()
                 : _inspect.Position);
            if (signature != _inspectBuiltFor)
            {
                BuildInspectBody();
                _inspectBuiltFor = signature;
            }

            if (_inspect.Subject == InspectSubject.None) return;

            _inspectPanel.EnableInClassList("inspect--tomb", _inspect.Tombstoned);
            _tombReason.style.display = _inspect.Tombstoned ? DisplayStyle.Flex : DisplayStyle.None;

            HudText.Set(_inspectTitle, _inspect.Title, HudTextRole.Name);

            // The two header lines are interpolated, and the pane refreshes fifteen times a
            // second, so they are rebuilt only when one of the values they quote has moved. The
            // cell is the thing that moves most; the mood band and the selection size change
            // rarely and the job hardly at all.
            int layer = _inspect.Layer;
            int selected = _directors != null ? _directors.Selection.Pawns.Count : 0;
            string band = _inspect.Subject == InspectSubject.Colonist
                ? MoodBands.Band(_inspect.Mood)
                : string.Empty;

            if (_metaLayer != layer || !ReferenceEquals(_metaPosition, _inspect.Position))
            {
                _metaLayer = layer;
                _metaPosition = _inspect.Position;
                HudText.Set(_inspectMeta, MetaLine(), HudTextRole.Meta);
            }
            if (!ReferenceEquals(_stateJob, _inspect.Job) || !ReferenceEquals(_stateBand, band) ||
                _stateSelected != selected)
            {
                _stateJob = _inspect.Job;
                _stateBand = band;
                _stateSelected = selected;
                HudText.Set(_inspectState, StateLine(), HudTextRole.Meta);
            }

            if (_inspect.Subject != InspectSubject.Colonist || _inspect.Tombstoned) return;

            SetNeed(0, _inspect.Food);
            SetNeed(1, _inspect.Rest);
            SetNeed(2, _inspect.Mood);

            for (int i = 0; i < _inspect.Skills.Count; i++)
            {
                SkillRow row = _inspect.Skills[i];
                SetSkill(i, row);
            }
        }

        void SetNeed(int index, int thousandths)
        {
            if (index >= _needs.Count) return;
            NeedView view = _needs[index];

            float percent = Percent(thousandths);
            view.Fill.style.width = Length.Percent(percent);
            Color band = HudTokens.NeedBand(thousandths);
            view.Fill.style.backgroundColor = band;
            view.Swatch.style.backgroundColor = band;

            // A need moves by fractions of a per cent between refreshes, so the label is rebuilt
            // only when the whole number it prints has actually changed.
            int whole = Mathf.RoundToInt(percent);
            if (view.LastPercent == whole) return;
            view.LastPercent = whole;
            HudText.Set(view.Value, whole.ToString("0") + "%", HudTextRole.Meta);
        }

        /// <summary>The line beside the name: what it is, which layer, where.</summary>
        string MetaLine() => _inspect.Layer >= 0
            ? $"{_inspect.Subtitle} · L{_inspect.Layer} · {Coordinates()}"
            : _inspect.Subtitle;

        string Coordinates()
        {
            // InspectModel says "at 78, 59"; the spec's header says "78, 59". The words belong to
            // the model, so they are trimmed here rather than changed there.
            string position = _inspect.Position;
            return position.StartsWith("at ", StringComparison.Ordinal) ? position.Substring(3) : position;
        }

        /// <summary>The line under the name: what they are doing, and how they are.</summary>
        string StateLine()
        {
            switch (_inspect.Subject)
            {
                case InspectSubject.Colonist:
                    {
                        // A multi-selection shows the primary colonist in full, with the size of
                        // the set said out loud: "3 selected" is the whole of what a pane can add
                        // to several brackets until commands arrive (A10).
                        string count = _directors != null && _directors.Selection.HasMultiple
                            ? $"{_directors.Selection.Pawns.Count} selected · "
                            : string.Empty;
                        return count + $"{_inspect.Job} · mood {MoodBands.Band(_inspect.Mood)}";
                    }
                case InspectSubject.Item:
                    return "item on the ground";
                case InspectSubject.Cell:
                    return "cell readout arrives with cell inspection";
                default:
                    return string.Empty;
            }
        }

        void BuildInspectBody()
        {
            _inspectBody.Clear();
            _needs.Clear();
            _skills.Clear();
            _tabChips.Clear();
            _needsGrid = null;
            _skillsGrid = null;
            _needRows = 0;

            // Nothing selected: no panel at all (owner, 2026-09-16), and this is the HUD's resting
            // state. It was a 41 px strip reading "Nothing selected", itself already a cut-down of
            // a three-sentence empty state; both were the interface talking about itself, and a
            // panel whose only content is the news that it has none earns less than the gap.
            // display:none rather than zero opacity, so it leaves the layout, leaves the measured
            // region set, and cannot take a click.
            if (_inspect.Subject == InspectSubject.None)
            {
                _inspectPanel.style.display = DisplayStyle.None;
                return;
            }

            _inspectPanel.style.display = DisplayStyle.Flex;

            // ---- header: avatar, name and its two lines, then the actions on the right
            var header = new VisualElement();
            header.AddToClassList("inspect__hdr");

            _inspectAvatar = new IconBadge(
                _inspect.Subject == InspectSubject.Colonist ? "ui.pawn.colonist"
                : _inspect.Subject == InspectSubject.Item ? "ui.res.meal"
                : "ui.overlay.zones",
                IconBadge.AvatarSize);
            _inspectAvatar.Inherit(HudTokens.TextPrimary);
            header.Add(_inspectAvatar);

            var titles = new VisualElement();
            titles.AddToClassList("inspect__titles");

            var nameLine = new VisualElement();
            nameLine.AddToClassList("inspect__nameline");
            _inspectTitle = HudText.Make(string.Empty, HudTextRole.Name, ussClass: "inspect__title");
            _inspectMeta = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "inspect__meta");
            nameLine.Add(_inspectTitle);
            nameLine.Add(_inspectMeta);

            _inspectState = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "inspect__state");
            titles.Add(nameLine);
            titles.Add(_inspectState);
            header.Add(titles);

            var actions = new VisualElement();
            actions.AddToClassList("inspect__actions");
            if (_inspect.Subject == InspectSubject.Colonist)
                foreach (InspectCommand command in _inspect.Commands)
                {
                    // Two of the three: the pane's header carries the commands a player reaches
                    // for, and Inspect is not one of them when the pane is already open.
                    if (command.Label == "Inspect") continue;
                    actions.Add(ActionButton(command));
                }

            var close = new VisualElement();
            close.AddToClassList("inspect__close");
            close.Add(new HudGlyph(HudGlyphKind.Close, 14f, HudTokens.TextDim));
            close.tooltip = "Clear the selection";
            close.RegisterCallback<ClickEvent>(_ => _directors?.Selection.Clear());
            actions.Add(close);
            header.Add(actions);
            _inspectBody.Add(header);

            if (_inspect.Subject == InspectSubject.Colonist)
            {
                var strip = new VisualElement();
                strip.AddToClassList("inspect__tabs");
                _tabChips.Clear();
                for (int i = 0; i < _inspect.Tabs.Count; i++)
                {
                    InspectTab tab = _inspect.Tabs[i];
                    Label chip = HudText.Make(tab.Name, HudTextRole.Body, ussClass: "tab");
                    chip.tooltip = tab.Enabled ? tab.Name : tab.Name + " — " + tab.Reason;
                    if (tab.Enabled)
                    {
                        int index = i;
                        chip.RegisterCallback<PointerDownEvent>(_ =>
                        {
                            _inspect.ShowTab(index);
                            ShowActiveTab();
                        });
                    }
                    _tabChips.Add(chip);
                    strip.Add(chip);
                }
                _inspectBody.Add(strip);

                var grid = new VisualElement();
                grid.AddToClassList("needs");
                _needs.Add(Need(grid, "Food"));
                _needs.Add(Need(grid, "Rest"));
                _needs.Add(Need(grid, "Mood"));
                _needRows = (_needs.Count + 1) / 2;
                _needsGrid = grid;
                _inspectBody.Add(grid);

                _skillsGrid = new VisualElement();
                _skillsGrid.AddToClassList("skills");
                for (int i = 0; i < _inspect.Skills.Count; i++)
                    _skills.Add(SkillLine(_skillsGrid));
                _inspectBody.Add(_skillsGrid);

                ShowActiveTab();
            }

            _tombReason = HudText.Make("no longer present — the pane keeps last-known values",
                HudTextRole.Meta, ussClass: "inspect__reason");
            _tombReason.style.display = DisplayStyle.None;
            _inspectBody.Add(_tombReason);
        }

        VisualElement ActionButton(InspectCommand command)
        {
            var button = new VisualElement();
            button.AddToClassList("action");
            if (!command.Enabled) button.AddToClassList("action--off");
            var icon = new IconBadge(command.IconKey, IconBadge.BarSize);
            icon.Inherit(HudTokens.TextMeta);
            button.Add(icon);
            button.Add(HudText.Make(command.Label, HudTextRole.Meta, ussClass: "action__label"));
            button.tooltip = command.Label + " — " + command.Reason;
            return button;
        }

        /// <summary>
        /// Show the tab the model says is active, and mark its chip.
        ///
        /// <para>The bodies are built once and hidden, not built on demand: a tab that rebuilds
        /// its tree on every click churns thirteen rows of elements for a control the player
        /// flicks between, and the pane's own rule is that structure is rebuilt when the subject
        /// changes and never for a value.</para>
        /// </summary>
        void ShowActiveTab()
        {
            string active = _inspect.ActiveTabName;
            bool skills = active == "Skills";

            if (_needsGrid != null)
                _needsGrid.style.display = skills ? DisplayStyle.None : DisplayStyle.Flex;
            if (_skillsGrid != null)
                _skillsGrid.style.display = skills ? DisplayStyle.Flex : DisplayStyle.None;

            for (int i = 0; i < _tabChips.Count && i < _inspect.Tabs.Count; i++)
            {
                bool on = _inspect.Tabs[i].Enabled && i == _inspect.ActiveTab;
                _tabChips[i].EnableInClassList("tab--on", on);
                _tabChips[i].EnableInClassList("tab--off", !_inspect.Tabs[i].Enabled);
            }
        }

        /// <summary>
        /// One line of the Skills tab. The passion mark is two lozenges rather than a word,
        /// because thirteen rows of "major"/"minor" is a column of text nobody reads and the
        /// thing the player wants is the shape of the list at a glance.
        /// </summary>
        static SkillLineView SkillLine(VisualElement grid)
        {
            var view = new SkillLineView();

            view.Root = new VisualElement();
            view.Root.AddToClassList("skill");

            view.Icon = new IconBadge(string.Empty, IconBadge.RowSize);
            view.Icon.Inherit(HudTokens.TextMeta);
            view.Name = HudText.Make(string.Empty, HudTextRole.Body, ussClass: "skill__name");
            view.Value = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true, "skill__level");

            view.Passion = new VisualElement();
            view.Passion.AddToClassList("skill__passion");
            for (int i = 0; i < 2; i++)
            {
                var pip = new VisualElement();
                pip.AddToClassList("skill__pip");
                view.Passion.Add(pip);
            }

            view.Root.Add(view.Icon);
            view.Root.Add(view.Name);
            view.Root.Add(view.Value);
            view.Root.Add(view.Passion);

            grid.Add(view.Root);
            return view;
        }

        void SetSkill(int index, in SkillRow row)
        {
            if (index >= _skills.Count) return;
            SkillLineView view = _skills[index];

            if (view.LastKey != row.IconKey)
            {
                view.LastKey = row.IconKey;
                view.Icon.SetKey(row.IconKey);
                HudText.Set(view.Name, row.Name, HudTextRole.Body);
                view.Root.tooltip = row.Live
                    ? (row.Note.Length > 0 ? row.Name + " — " + row.Note : row.Name)
                    : row.Name + " — " + row.Reason;
            }

            if (view.LastLive != row.Live)
            {
                view.LastLive = row.Live;
                view.Root.EnableInClassList("skill--off", !row.Live);
                view.Icon.Inherit(row.Live ? HudTokens.TextMeta : HudTokens.TextFaint);
            }

            // A level moves once in a working day, so the string is built on the change and not
            // fifteen times a second for as long as somebody is selected.
            if (view.LastLevel != row.Level || !row.Live)
            {
                view.LastLevel = row.Level;
                HudText.Set(view.Value, row.Live ? row.Level.ToString("0") : "—", HudTextRole.Meta);
            }

            if (view.LastPassion == row.Passion) return;
            view.LastPassion = row.Passion;
            for (int i = 0; i < view.Passion.childCount; i++)
                view.Passion[i].style.display =
                    row.Live && row.Passion > i ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static NeedView Need(VisualElement grid, string name)
        {
            var view = new NeedView();

            view.Root = new VisualElement();
            view.Root.AddToClassList("need");

            var line = new VisualElement();
            line.AddToClassList("need__line");
            view.Swatch = new VisualElement();
            view.Swatch.AddToClassList("need__swatch");
            view.Name = HudText.Make(name, HudTextRole.Body, ussClass: "need__name");
            view.Value = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true, "need__value");
            line.Add(view.Swatch);
            line.Add(view.Name);
            line.Add(view.Value);
            view.Root.Add(line);

            var bar = new VisualElement();
            bar.AddToClassList("bar");
            bar.AddToClassList("bar--need");
            view.Fill = new VisualElement();
            view.Fill.AddToClassList("bar__fill");
            bar.Add(view.Fill);
            view.Root.Add(bar);

            grid.Add(view.Root);
            return view;
        }

        // ============================================================ A8 command bar

        void BuildBar()
        {
            _barRow = new VisualElement { name = "bar-row", pickingMode = PickingMode.Ignore };
            _barRow.AddToClassList("bar-row");

            _bar = new VisualElement { name = "bar" };
            _bar.AddToClassList("commandbar");
            _barRow.Add(_bar);
            _hud.Add(_barRow);

            IReadOnlyList<HudCommand> commands = HudCommands.All;
            for (int i = 0; i < commands.Count; i++)
            {
                HudCommand command = commands[i];
                bool isMenu = command.Key == HudCommands.MenuKey;

                if (isMenu)
                {
                    _barDivider = new VisualElement { pickingMode = PickingMode.Ignore };
                    _barDivider.AddToClassList("commandbar__divider");
                    _bar.Add(_barDivider);
                }

                VisualElement item = CommandItem(command);
                _bar.Add(item);
                if (isMenu) _menuItem = item;
                else _barItems.Add(item);

                // Laid out but invisible until the first reflow has measured them. Hiding with
                // visibility rather than display is what makes the measurement possible at all:
                // a display:none element has no width to read, so the bar would have to be drawn
                // overflowing for one frame to find out that it overflows.
                item.style.visibility = Visibility.Hidden;
            }

            _barRow.RegisterCallback<GeometryChangedEvent>(_ => ReflowBar(_hud.resolvedStyle.width));

            BuildMenuPopup();
        }

        VisualElement CommandItem(HudCommand command)
        {
            var item = new VisualElement();
            item.AddToClassList("cmd");
            if (command.Primary) item.AddToClassList("cmd--primary");
            if (!command.Live) item.AddToClassList("cmd--off");

            // Categorised: the command bar is the second and last place the spec allows an icon to
            // carry a colour of its own.
            var icon = new IconBadge(command.Key, IconBadge.BarSize, categorised: !command.Primary);
            if (command.Primary) icon.Inherit(HudTokens.OnAccent);
            item.Add(icon);

            Label label = HudText.Make(command.Label, HudTextRole.Row, ussClass: "cmd__label");
            if (command.Primary) label.AddToClassList("cmd__label--primary");
            item.Add(label);

            // Every item shows its key. An acceptance criterion, and the reason the bar is set in
            // a narrow face: eleven labelled items with their caps have to cross the screen.
            item.Add(HudText.Make(command.Hotkey, HudTextRole.Hotkey, ussClass: "cmd__key"));

            item.tooltip = command.Live
                ? command.Label + " — " + command.Hotkey
                : command.Label + " — " + command.Reason;

            string key = command.Key;
            item.RegisterCallback<ClickEvent>(_ => OnCommand(key));
            return item;
        }

        void OnCommand(string key)
        {
            if (key == HudCommands.BuildKey) SetBuildPalette(!BuildPaletteOpen);
            else if (key == HudCommands.MenuKey) ToggleMenu();
        }

        /// <summary>
        /// The two hotkeys on the bar that are live. The rest are legends on controls whose
        /// systems do not exist, and they deliberately avoid every key the game already uses —
        /// M, C and X arm the designate tools, R and F move the slice, V cycles visibility,
        /// Space and 1 to 3 are the clock and Home recentres.
        /// </summary>
        void ReadBarKeys()
        {
            var keys = UnityEngine.InputSystem.Keyboard.current;
            if (keys == null) return;
            if (keys.bKey.wasPressedThisFrame) SetBuildPalette(!BuildPaletteOpen);
        }

        /// <summary>
        /// Fit the bar to the width it has, and put whatever does not fit into Menu.
        ///
        /// <para><b>The bar may not wrap and may not overflow</b>, which is the acceptance
        /// criterion and also what lets the inspect pane above it stop guessing at its height. The
        /// widths are the ones UI Toolkit actually laid out rather than an estimate, measured once
        /// while every item is present and invisible; re-measuring after items have been hidden
        /// would measure the shortened bar and oscillate.</para>
        /// </summary>
        void ReflowBar(float screenWidth)
        {
            if (_barItems.Count == 0 || screenWidth <= 0f) return;

            if (_barWidths.Count == 0)
            {
                bool measured = true;
                foreach (VisualElement item in _barItems)
                {
                    float width = item.resolvedStyle.width;
                    if (float.IsNaN(width) || width <= 1f) { measured = false; break; }
                }
                float menuWidth = _menuItem.resolvedStyle.width;
                if (float.IsNaN(menuWidth) || menuWidth <= 1f) measured = false;
                if (!measured) return;

                foreach (VisualElement item in _barItems) _barWidths.Add(item.resolvedStyle.width);
                _barWidths.Add(menuWidth);
            }

            // The bar's own padding, not the screen margin: the bar runs edge to edge now, so the
            // room an item has is the screen less what the bar itself takes.
            float available = screenWidth - 2 * HudCommands.BarPad;
            if (Mathf.Approximately(available, _barMeasuredAt)) return;
            _barMeasuredAt = available;

            int shown = HudCommands.Fit(_barWidths, available);
            if (shown == _barShown) return;
            _barShown = shown;

            _menuOverflow.Clear();
            IReadOnlyList<HudCommand> commands = HudCommands.All;
            for (int i = 0; i < _barItems.Count; i++)
            {
                bool fits = i < shown;
                _barItems[i].style.display = fits ? DisplayStyle.Flex : DisplayStyle.None;
                _barItems[i].style.visibility = Visibility.Visible;

                // The last item before the divider carries no trailing gap, so the bar's realised
                // width is exactly what HudCommands.BarWidth says it is — which is what the
                // PlayMode gate compares against, and what "nothing is cut off at the right edge"
                // reduces to once the model is trusted.
                _barItems[i].style.marginRight = fits && i == shown - 1 ? 0 : HudCommands.ItemGap;

                if (!fits) _menuOverflow.Add(MenuRow(commands[i]));
            }
            _menuItem.style.marginRight = 0;
            _menuItem.style.visibility = Visibility.Visible;
            _barDivider.style.display = DisplayStyle.Flex;
        }

        static readonly string[] OverlayKeys =
        {
            "ui.overlay.temperature", "ui.overlay.light", "ui.overlay.beauty", "ui.overlay.cleanliness",
            "ui.overlay.roofs", "ui.overlay.zones", "ui.overlay.power", "ui.overlay.salvage",
            "ui.overlay.support", "ui.overlay.traffic",
        };

        void BuildMenuPopup()
        {
            _menuPopup = Popover("menu", "Menu", () => ToggleMenu(false), "menu");

            _menuOverflow = new VisualElement();
            _menuOverflow.AddToClassList("menu__rows");
            _menuPopup.Add(_menuOverflow);

            // A12, the overlay toggles. They used to be a strip of ten unlabelled icon buttons at
            // the right end of the bottom bar, where they sat on top of the tab row and ate its
            // last tab — and the acceptance criteria strike out "overflowing colour chips at the
            // right end of the command bar" by name. They are not information a player can act
            // on until a channel renders (M4), so they come here, labelled, rather than being
            // deleted: a control that exists and is disabled with its reason is the catalogue's
            // rule, and an unlabelled chip on the bar was neither.
            _menuPopup.Add(HudText.Make("Overlays", HudTextRole.PanelLabel, ussClass: "menu__section"));
            foreach (string key in OverlayKeys)
            {
                var overlay = new VisualElement();
                overlay.AddToClassList("menu__row");
                overlay.AddToClassList("menu__row--off");
                var icon = new IconBadge(key, IconBadge.BarSize);
                icon.Inherit(HudTokens.TextMeta);
                overlay.Add(icon);
                overlay.Add(HudText.Make(Registry.Label(key), HudTextRole.Row, ussClass: "menu__label"));
                overlay.tooltip = Registry.Label(key) + " — overlay channels arrive with M4";
                _menuPopup.Add(overlay);
            }

            _menuPopup.Add(HudText.Make("Game", HudTextRole.PanelLabel, ussClass: "menu__section"));

            var settings = new VisualElement();
            settings.AddToClassList("menu__row");
            settings.Add(HudText.Make("Settings", HudTextRole.Row, ussClass: "menu__label"));
            settings.Add(HudText.Make("Esc", HudTextRole.Hotkey, ussClass: "menu__key"));
            settings.tooltip = "Graphics settings. None of it is in the save.";
            settings.RegisterCallback<ClickEvent>(_ =>
            {
                ToggleMenu(false);
                _directors?.Settings.SetOpen(true);
            });
            _menuPopup.Add(settings);

            _hud.Add(_menuPopup);
        }

        VisualElement MenuRow(HudCommand command)
        {
            var row = new VisualElement();
            row.AddToClassList("menu__row");
            row.AddToClassList("menu__row--off");
            var icon = new IconBadge(command.Key, IconBadge.BarSize, categorised: true);
            row.Add(icon);
            row.Add(HudText.Make(command.Label, HudTextRole.Row, ussClass: "menu__label"));
            row.Add(HudText.Make(command.Hotkey, HudTextRole.Hotkey, ussClass: "menu__key"));
            row.tooltip = command.Label + " — " + command.Reason;
            return row;
        }

        void ToggleMenu() => ToggleMenu(_menuPopup.style.display == DisplayStyle.None);

        void ToggleMenu(bool open)
        {
            if (open) SetBuildPalette(false);

            _menuPopup.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _menuItem.EnableInClassList("cmd--on", open);
            if (open && _menuItem != null) PlacePopover(_menuPopup, _menuItem);
        }

        /// <summary>Whether the Menu popover is open, for whoever owns the Escape key.</summary>
        public bool MenuOpen => _menuPopup != null && _menuPopup.style.display == DisplayStyle.Flex;

        /// <summary>Close the Menu popover. The Escape half, called by <c>SettingsPresenter</c>.</summary>
        public void CloseMenu() => ToggleMenu(false);

        // ============================================================ A7 build palette

        /// <summary>
        /// Categories in catalogue order, each with a few of its tools. Every icon key exists in
        /// the registry; every control is display-only until placement tools land in M3.
        /// </summary>
        static readonly (string key, string label, string[] tools)[] BuildCategories =
        {
            ("ui.arch.category.structure", "Structure", new[] { "ui.arch.tool.wall", "ui.arch.tool.door", "ui.arch.tool.stair", "ui.arch.tool.ladder", "ui.arch.tool.roof", "ui.arch.tool.reclaim" }),
            ("ui.arch.category.orders", "Orders", new[] { "ui.arch.tool.mine", "ui.arch.tool.deconstruct", "ui.arch.tool.harvest", "ui.arch.tool.forbid", "ui.arch.tool.clearrubble" }),
            ("ui.arch.category.zones", "Zones", new[] { "ui.arch.tool.stockpile", "ui.arch.tool.growzone", "ui.arch.tool.dumping" }),
            ("ui.arch.category.production", "Production", new[] { "ui.arch.tool.fabricator", "ui.arch.tool.galley", "ui.arch.tool.reclaimer", "ui.arch.tool.bench" }),
            ("ui.arch.category.furniture", "Furniture", new[] { "ui.arch.tool.bunk", "ui.arch.tool.table", "ui.arch.tool.lamp", "ui.arch.tool.shelf" }),
            ("ui.arch.category.power", "Power", new[] { "ui.arch.tool.conduit", "ui.arch.tool.battery", "ui.arch.tool.generator", "ui.arch.tool.reactor" }),
            ("ui.arch.category.security", "Security", new[] { "ui.arch.tool.turret", "ui.arch.tool.trap", "ui.arch.tool.barricade" }),
            ("ui.arch.category.salvage", "Salvage", new[] { "ui.arch.tool.salvage", "ui.arch.tool.deconstruct", "ui.arch.tool.reclaim" }),
            ("ui.arch.category.floors", "Floors", new[] { "ui.arch.tool.deckplate", "ui.arch.tool.grating", "ui.arch.tool.tile" }),
            ("ui.arch.category.recreation", "Recreation", new[] { "ui.arch.tool.gamestable", "ui.arch.tool.viewscreen", "ui.arch.tool.planter" }),
        };

        /// <summary>
        /// The palette, opened by the Build command and closed by Escape.
        ///
        /// <para>It used to be a column pinned to the left edge and permanently open, competing
        /// with the stores panel for a column that could not hold both. As a transient panel over
        /// the board it costs nothing when it is shut, which is most of the time.</para>
        /// </summary>
        void BuildPalette()
        {
            _buildPanel = Popover("build", "Build", () => SetBuildPalette(false), "build");

            var cats = new VisualElement();
            cats.AddToClassList("build__cats");

            // The categories scroll: ten categories and their tools will outgrow any panel that
            // has to share a screen, and a list too long for its panel should scroll rather than
            // quietly lose its end.
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("build__scroll");
            for (int i = 0; i < BuildCategories.Length; i++)
            {
                var (key, label, _) = BuildCategories[i];
                int index = i;
                VisualElement chip = PaletteChip(key, label);
                chip.tooltip = label + " — placement tools arrive with M3";
                chip.RegisterCallback<ClickEvent>(_ => SelectBuildCategory(index));
                cats.Add(chip);
            }
            scroll.Add(cats);
            _buildPanel.Add(scroll);

            _buildTools = new VisualElement();
            _buildTools.AddToClassList("build__tools");
            _buildPanel.Add(_buildTools);

            _hud.Add(_buildPanel);
        }

        static VisualElement PaletteChip(string key, string label)
        {
            var chip = new VisualElement();
            chip.AddToClassList("chip");
            var icon = new IconBadge(key, IconBadge.BarSize);
            icon.Inherit(HudTokens.TextMeta);
            chip.Add(icon);
            chip.Add(HudText.Make(label, HudTextRole.Row, ussClass: "chip__label"));
            return chip;
        }

        void SetBuildPalette(bool open)
        {
            // One popover at a time. Two raised from the same bar would overlap each other over
            // the buttons that raised them, and the player would have no way to tell which of the
            // two the Escape they are about to press belongs to.
            if (open) ToggleMenu(false);

            _buildPanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (_barItems.Count > 0)
            {
                _barItems[0].EnableInClassList("cmd--on", open);
                if (open) PlacePopover(_buildPanel, _barItems[0]);
            }
        }

        void SelectBuildCategory(int index)
        {
            if (_buildCategory == index) return;
            _buildCategory = index;

            _buildTools.Clear();
            foreach (string tool in BuildCategories[index].tools)
            {
                // The label is the registry's, never a two-letter sigil derived from the key: the
                // acceptance criteria strike out every three-letter placeholder on the screen.
                VisualElement chip = PaletteChip(tool, Registry.Label(tool));
                chip.AddToClassList("chip--off");
                chip.tooltip = Registry.Label(tool) + " — placement tools arrive with M3";
                _buildTools.Add(chip);
            }
        }

        // ============================================================ B17 settings

        /// <summary>
        /// The settings panel: two sections behind a tab strip, opened with Escape or from Menu.
        ///
        /// <para><b>It is not a modal.</b> There is no scrim and nothing is blocked: the world
        /// runs, the camera orbits and the clock ticks while it is open, because the only reason
        /// to have the panel is to watch the board change as a lever moves. Clicks that land on it
        /// already stop at the panel edge through <see cref="PointOverUi"/>.</para>
        ///
        /// <para><b>Interface before Graphics</b>, because the first thing a player wants from a
        /// settings panel on a large monitor is to make the type bigger, and because that is the
        /// one setting here that changes the panel they are looking at while they look at it.</para>
        /// </summary>
        void BuildSettings()
        {
            // A window but not a popover: it is reached from Menu and from Escape, so there is no
            // one button it belongs over, and it keeps the centring a settings panel wants.
            _settingsPanel = Window("settings", Registry.Label(SettingsDirector.PanelKey),
                () => _directors?.Settings.SetOpen(false), "settings");

            // The tab strip, in the same idiom as the inspect pane's: nothing new is invented for
            // a second use of a control the HUD already has.
            var tabs = new VisualElement();
            tabs.AddToClassList("settings__tabs");
            foreach (SettingsTab tab in new[] { SettingsTab.Interface, SettingsTab.Graphics })
            {
                string key = tab == SettingsTab.Interface
                    ? SettingsDirector.InterfaceKey
                    : SettingsDirector.GraphicsKey;
                Label chip = HudText.Make(Registry.Label(key), HudTextRole.Body, ussClass: "tab");
                SettingsTab captured = tab;
                chip.RegisterCallback<ClickEvent>(_ => _directors?.Settings.SetTab(captured));
                _settingTabs[tab] = chip;
                tabs.Add(chip);
            }
            _settingsPanel.Add(tabs);

            BuildInterfaceSection();
            BuildGraphicsSection();

            // The director opens on Interface, and the shell may never attach to a director at all
            // in a harness that builds no world. Showing both sections at once is not a state
            // anything asks for, so it is not a state the panel is ever in.
            OnSettingsTabChanged(SettingsTab.Interface);

            _settingsPanel.Add(HudText.Make("Escape closes. None of it is in the save.",
                HudTextRole.Meta, ussClass: "settings__note"));
            _hud.Add(_settingsPanel);
        }

        /// <summary>
        /// The Interface section: how large the HUD is drawn.
        ///
        /// <para>A ladder of percentages rather than a slider. The rungs are the director's, so
        /// the set is testable in the fast tier, and each says out loud what the trade is — larger
        /// type is easier to read and hides more of the board, which is the whole of the decision
        /// the player is making.</para>
        /// </summary>
        void BuildInterfaceSection()
        {
            _interfaceSection = new VisualElement();
            _interfaceSection.AddToClassList("settings__body");

            var row = new VisualElement();
            row.AddToClassList("settings__row");
            row.AddToClassList("settings__row--static");
            var icon = new IconBadge(SettingsDirector.UiScaleKey, IconBadge.RowSize);
            icon.Inherit(HudTokens.TextMeta);
            row.Add(icon);
            row.Add(HudText.Make(Registry.Label(SettingsDirector.UiScaleKey), HudTextRole.Row,
                ussClass: "settings__label"));
            _interfaceSection.Add(row);

            var ladder = new VisualElement();
            ladder.AddToClassList("settings__ladder");
            foreach (int percent in SettingsDirector.UiScales)
            {
                // A percentage is a figure, so it is set in the mono face like every other figure
                // on this screen.
                Label rung = HudText.Make(percent + "%", HudTextRole.Body, numeric: true, "rung");
                rung.tooltip = percent == 100
                    ? "The size the interface is designed at"
                    : percent < 100
                        ? "Smaller type, less of the board hidden"
                        : "Larger type, more of the board hidden";
                int captured = percent;
                rung.RegisterCallback<ClickEvent>(_ => _directors?.Settings.SetUiScale(captured));
                _scaleRungs[percent] = rung;
                ladder.Add(rung);
            }
            _interfaceSection.Add(ladder);
            _settingsPanel.Add(_interfaceSection);
        }

        void BuildGraphicsSection()
        {
            _graphicsSection = new VisualElement();
            _graphicsSection.AddToClassList("settings__body");

            foreach (GraphicsOption option in SettingsDirector.All)
            {
                string key = SettingsDirector.KeyOf(option);
                var row = new VisualElement();
                row.AddToClassList("settings__row");
                var icon = new IconBadge(key, IconBadge.RowSize);
                icon.Inherit(HudTokens.TextMeta);
                row.Add(icon);
                row.Add(HudText.Make(Registry.Label(key), HudTextRole.Row, ussClass: "settings__label"));

                var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                pip.AddToClassList("settings__pip");
                row.Add(pip);

                // Said in the tooltip rather than on the row, because it is a fact about what the
                // toggle costs, not about what it does.
                row.tooltip = SettingsDirector.NeedsRedraw(option)
                    ? "Redraws the board when it changes"
                    : "Takes effect on the next frame";

                GraphicsOption captured = option;
                row.RegisterCallback<ClickEvent>(_ => _directors?.Settings.Toggle(captured));
                _settingRows[option] = row;
                _graphicsSection.Add(row);
            }

            _settingsPanel.Add(_graphicsSection);
        }

        void OnSettingsTabChanged(SettingsTab tab)
        {
            foreach (var entry in _settingTabs)
            {
                bool on = entry.Key == tab;
                entry.Value.EnableInClassList("tab--on", on);
                entry.Value.EnableInClassList("tab--off", !on);
            }
            _interfaceSection.style.display =
                tab == SettingsTab.Interface ? DisplayStyle.Flex : DisplayStyle.None;
            _graphicsSection.style.display =
                tab == SettingsTab.Graphics ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void OnUiScaleChanged(int percent)
        {
            foreach (var entry in _scaleRungs)
                entry.Value.EnableInClassList("rung--on", entry.Key == percent);
            ApplyUiScale(percent);
        }

        /// <summary>
        /// Draw the HUD larger or smaller by telling its panel a different reference resolution.
        ///
        /// <para><b>On a copy of the asset, never the asset itself.</b> <c>PanelSettings</c> is a
        /// file on disk, and writing to it from play mode in the editor leaves the change behind
        /// after the session ends — permanently, and invisibly, in a committed asset. This project
        /// has met that exact trap once already with the sky material, which is copied before it
        /// is tinted for the same reason.</para>
        ///
        /// <para>Everything else follows for free: the panel re-lays-out, the shell's own
        /// <c>GeometryChangedEvent</c> fires, the colonist strip re-clamps to what fits and the
        /// command bar reflows its tail into Menu. The layout is anchored rather than sized, so a
        /// smaller canvas is a case it already handles and is already tested at.</para>
        /// </summary>
        public void ApplyUiScale(int percent)
        {
            if (_panelCopy == null) EnsurePanelCopy(GetComponent<UIDocument>());
            if (_panelCopy == null) return;

            (int width, int height) = HudLayout.ReferenceFor(percent);
            _panelCopy.referenceResolution = new Vector2Int(width, height);
        }

        void EnsurePanelCopy(UIDocument? doc)
        {
            if (_panelCopy != null || doc == null || doc.panelSettings == null) return;

            _panelCopy = Instantiate(doc.panelSettings);
            _panelCopy.name = doc.panelSettings.name + " (scaled)";
            _panelCopy.hideFlags = HideFlags.HideAndDontSave;
            doc.panelSettings = _panelCopy;
        }

        void OnSettingsChanged()
        {
            bool open = _directors != null && _directors.Settings.Open;
            _settingsPanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open) ToggleMenu(false);
        }

        void OnSettingChanged(GraphicsOption option)
        {
            if (_directors == null) return;
            if (!_settingRows.TryGetValue(option, out VisualElement? row)) return;
            row.EnableInClassList("settings__row--on", _directors.Settings.IsOn(option));
        }

        // ============================================================ formatting

        /// <summary>Thousandths to a percentage of a bar's width.</summary>
        static float Percent(int thousandths) => Mathf.Clamp(thousandths, 0, 1000) / 10f;
    }
}
