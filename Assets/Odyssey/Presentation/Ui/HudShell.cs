#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The HUD shell (regions A1 to A14 of the panel catalogue), as a first pass: every region
    /// present in its catalogue slot, styled after the owner-approved mockup
    /// (<c>docs/reference/mockups/hud-v2.html</c>), and divided honestly into what is live and
    /// what is not.
    ///
    /// Live in this pass: the roster bar, the colonist inspect pane (the first thing the
    /// interface owes the player), the clock, the speed controls, the Depth Ruler's layer
    /// navigation, and the real rows of the ledger. Displayed for the look, disabled with a
    /// reason and never hidden: the architect palette, the main tabs, the overlay toggles, the
    /// alert stack's empty state and the cancel affordance — per the catalogue's rule that a
    /// thing without its system yet is shown greyed with why, so the shape of the game is
    /// visible from the first version.
    ///
    /// The division of labour is the one ADR 0003 fixes: every state and formatting decision
    /// lives in the Unity-free <see cref="Odyssey.Hud"/> models; this class only realises their
    /// rows as elements and routes clicks to the existing seams — the camera rig for layer and
    /// speed, the pick resolver for selection, never the simulation directly.
    ///
    /// Regions refresh on wall-clock cadence buckets (15 Hz / 4 Hz / 1 Hz), never per frame and
    /// never keyed to ticks: at 3× speed the tick rate triples and the HUD's must not. Element
    /// trees are built once and updated in place; a refresh touches text and fill widths only.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [RequireComponent(typeof(OdysseyBootstrap))]
    public sealed class HudShell : MonoBehaviour
    {
        [Tooltip("The HUD stylesheet. Assigned by the play-scene builder; authored at Assets/Odyssey/Presentation/Ui/Hud.uss.")]
        public StyleSheet? hudStyles;

        const float FastBucketSeconds = 1f / 15f;   // roster, inspect pane
        const float MidBucketSeconds = 0.25f;       // ledger, speed state
        const float SlowBucketSeconds = 1f;         // clock, ruler

        readonly RosterModel _roster = new RosterModel();
        readonly InspectModel _inspect = new InspectModel();
        readonly LayerRulerModel _ruler = new LayerRulerModel();
        readonly LedgerModel _ledger = new LedgerModel();

        OdysseyBootstrap? _boot;
        SliceCameraRig? _rig;
        HudDirectors? _directors;

        /// <summary>The layer the colony actually stands on, for the ruler's surface marker.
        /// Captured from the rig on the first frame the world exists, because component Start
        /// order on one GameObject is not defined and this must not race the bootstrap.</summary>
        int _surfaceLayer;
        bool _surfaceCaptured;

        float _fast;
        float _mid;
        float _slow;
        bool _primed;

        // ---- element references, resolved once the tree is built
        VisualElement _hud = null!;
        VisualElement _leftColumn = null!;
        VisualElement _rightColumn = null!;
        VisualElement _bottomRow = null!;
        VisualElement _bottomStack = null!;
        VisualElement _buildPanel = null!;
        VisualElement _buildButton = null!;
        VisualElement _settingsPanel = null!;
        readonly Dictionary<GraphicsOption, VisualElement> _settingRows = new();
        readonly List<CardView> _cards = new List<CardView>();
        VisualElement _rosterHost = null!;
        VisualElement _rulerRows = null!;
        readonly List<RulerTickView> _rulerTicks = new List<RulerTickView>();
        Label _rulerActive = null!;
        Label _clockTime = null!;
        Label _clockDate = null!;
        readonly List<VisualElement> _speedButtons = new List<VisualElement>();
        VisualElement _ledgerRows = null!;
        readonly List<Label> _ledgerQty = new List<Label>();

        // inspect pane: structure rebuilt when the subject changes, values updated in place
        VisualElement _inspectBody = null!;
        VisualElement _inspectPane = null!;
        string _inspectBuiltFor = string.Empty;
        Label _inspectTitle = null!;
        Label _inspectKind = null!;
        Label _inspectBadge = null!;
        Label _inspectSub = null!;
        NeedViews _food = new();
        NeedViews _rest = new();
        NeedViews _mood = new();
        Label _tombReason = null!;

        // the drag-select marquee, and the roster's shift-drag sweep state
        VisualElement _marquee = null!;
        bool _sweepingRoster;

        struct CardView
        {
            public VisualElement Root;
            public Label Name;

            /// <summary>Mood, which is the one the card is bordered and coloured by.</summary>
            public VisualElement Fill;
            public VisualElement Bar;

            /// <summary>Food. Nought to a thousand in the frame, unlike mood's nought to a
            /// hundred, which is why the two are scaled differently where they are bound.</summary>
            public VisualElement FoodFill;
        }

        struct RulerTickView
        {
            public VisualElement Root;
            public VisualElement Dot;
            public int Layer;
        }

        sealed class NeedViews
        {
            public Label Value = null!;
            public VisualElement Fill = null!;
            public VisualElement Bar = null!;
        }

        void Awake()
        {
            _boot = GetComponent<OdysseyBootstrap>();
            _rig = _boot.cameraRig;
        }

        void Start()
        {
            // Building the tree needs no world; every refresh guards on one, because this
            // Start may run before the bootstrap's own Start has built anything.
            var doc = GetComponent<UIDocument>();
            var root = doc.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("[Hud] UIDocument has no root: the panel settings asset is missing or unassigned.");
                return;
            }
            if (hudStyles != null) root.styleSheets.Add(hudStyles);

            _hud = new VisualElement { pickingMode = PickingMode.Ignore };
            _hud.AddToClassList("hud");
            root.Add(_hud);

            // The drag-select marquee (A2's box in the world): a picture, not a decision, so it
            // sits here and polls the rig's rect every frame rather than subscribing to anything.
            // Screen-bottom-left origins become panel-top-left ones in UpdateMarquee.
            _marquee = new VisualElement { pickingMode = PickingMode.Ignore };
            _marquee.AddToClassList("marquee");
            _marquee.style.display = DisplayStyle.None;
            _hud.Add(_marquee);

            // The left edge is one column, not two absolute slots: the ledger and the architect
            // palette stack inside it, so neither can ever sit on top of the other.
            _leftColumn = new VisualElement();
            _leftColumn.AddToClassList("slot-left");
            _hud.Add(_leftColumn);

            // And the right edge, for the same reason and after the same accident. The clock,
            // the alerts and the ruler were three absolutely positioned slots with hand-picked
            // offsets, so the alerts panel sat at a fixed 132px from the top whatever height the
            // clock above it had grown to — and it had grown past it, burying the speed buttons.
            // A column cannot do that: whatever each region's height turns out to be, the next
            // one starts below it.
            _rightColumn = new VisualElement();
            _rightColumn.AddToClassList("slot-right");
            _hud.Add(_rightColumn);

            // And the bottom edge, third time. The tab bar, the overlay strip and the cancel
            // button were three absolute slots along the same line, so the strip sat on top of
            // the bar's right end and ate the last tab -- which read as the bar being too wide
            // when it was not. In a row they divide the edge between them: the tabs take what is
            // left after the other two have taken what they need.
            // A stack, not a row, and the palette lives in it above the bar. Pinning the
            // palette to a fixed distance from the bottom is the same mistake this file has
            // now made three times: the bar wraps to two rows once every tab carries its full
            // name, and a hand-picked offset put the palette straight through it.
            _bottomStack = new VisualElement();
            _bottomStack.AddToClassList("slot-bottom-stack");
            _hud.Add(_bottomStack);

            _bottomRow = new VisualElement();
            _bottomRow.AddToClassList("slot-bottom");
            _bottomStack.Add(_bottomRow);

            BuildLedger();
            BuildPalette();
            BuildRoster();
            BuildClock();
            BuildAlerts();
            BuildRuler();
            BuildInspect();
            BuildTabs();
            BuildOverlays();
            BuildCancel();
            BuildSettings();

        }

        void OnDestroy() => Detach();

        /// <summary>
        /// The directors are made by the bootstrap when the world is, which may be after this
        /// Start: component order on one GameObject is not defined. So the shell attaches to
        /// them on the first Update that finds them, and answers their events from then on.
        /// </summary>
        void Attach(HudDirectors directors)
        {
            _directors = directors;
            _directors.Selection.Changed += OnSelectionChanged;
            _directors.Slice.LayerChanged += OnLayerChanged;
            _directors.Settings.Changed += OnSettingsChanged;
            _directors.Settings.OptionChanged += OnSettingChanged;

            // The panel may already disagree with the director by the time we get here: the
            // presenter seeds it from the scene and then lays stored preferences over it, and both
            // happen before the shell has found anything to attach to.
            OnSettingsChanged();
            foreach (GraphicsOption option in SettingsDirector.All) OnSettingChanged(option);
        }

        void Detach()
        {
            if (_directors == null) return;
            _directors.Selection.Changed -= OnSelectionChanged;
            _directors.Slice.LayerChanged -= OnLayerChanged;
            _directors.Settings.Changed -= OnSettingsChanged;
            _directors.Settings.OptionChanged -= OnSettingChanged;
            _directors = null;
        }

        void OnEnable()
        {
            // The world's clicks must die at the panel edge, not sail through it onto the map.
            // Set here rather than in Start so the gate exists before the first frame's input.
            if (_rig != null) _rig.PointerOverInterface = PointOverUi;
        }

        void OnDisable()
        {
            if (_rig != null) _rig.PointerOverInterface = null;
        }

        /// <summary>
        /// True when a screen position sits over a HUD region. The rig asks before picking, so
        /// a click on a panel never reaches the world (input case 2 of design 09 §6); the shell
        /// element itself is non-pickable, so empty screen still belongs to the world.
        /// </summary>
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
        /// opposite side of the screen". Mirrored is exactly what that describes, and the x axis
        /// being right is what identifies it.</para>
        ///
        /// <para><b><see cref="PointOverUi"/> had the same line and therefore the same fault</b>,
        /// which is worse because it fails quietly: it decides whether a click belongs to the HUD
        /// or to the world, so a press near the bottom bar was tested against the top of the
        /// screen. <c>docs/plans/next-session-prompt.md</c> item 2 already recorded that guard as
        /// shipping "on inspection only" with no test, and this is what was waiting in it. Both
        /// call sites go through here now so they cannot disagree again.</para>
        /// </summary>
        Vector2 ToPanel(Vector2 screenPosition) =>
            RuntimePanelUtils.ScreenToPanel(
                _hud.panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));

        public bool PointOverUi(Vector2 screenPosition)
        {
            if (_hud == null || _hud.panel == null) return false;
            VisualElement? hit = _hud.panel.Pick(ToPanel(screenPosition));
            return hit != null && hit != _hud;
        }

        void Update()
        {
            var world = _boot!.World;
            if (world == null || _hud == null) return;
            if (_directors == null)
            {
                if (_boot.Directors == null) return;
                Attach(_boot.Directors);
            }

            // Dead handles are dropped before anything below reads the selection.
            _directors!.Refresh(world.Views.Current);

            if (!_surfaceCaptured)
            {
                _surfaceLayer = _directors.Slice.ActiveLayer;
                _surfaceCaptured = true;
            }

            // Prime on the first frame a world exists, rather than waiting out the fastest
            // cadence bucket: the shell's Start can run before the bootstrap's has built
            // anything, and a HUD that is empty for its first fraction of a second reads as
            // broken. It also makes the shell correct in any host whose frames cost almost no
            // real time — the playmode test harness being the one we actually meet.
            if (!_primed)
            {
                _primed = true;
                RefreshRoster();
                RefreshInspect();
                RefreshLedger();
                RefreshSpeed();
                RefreshClock();
                RefreshRuler();
            }

            _fast += Time.unscaledDeltaTime;
            _mid += Time.unscaledDeltaTime;
            _slow += Time.unscaledDeltaTime;

            if (_fast >= FastBucketSeconds)
            {
                _fast = 0f;
                RefreshRoster();
                RefreshInspect();
            }
            if (_mid >= MidBucketSeconds)
            {
                _mid = 0f;
                RefreshLedger();
                RefreshSpeed();
            }
            if (_slow >= SlowBucketSeconds)
            {
                _slow = 0f;
                RefreshClock();
                RefreshRuler();
            }

            UpdateMarquee();

            // The roster sweep ends when the button does, wherever the pointer happens to be when
            // it ends — a card's own PointerUp never arrives if the release landed off the bar.
            if (_sweepingRoster && UnityEngine.InputSystem.Mouse.current?.leftButton.isPressed != true)
                _sweepingRoster = false;
        }

        /// <summary>
        /// The marquee follows the rig's box every frame — a 15 Hz marquee trails the cursor and
        /// reads as lag. Screen coordinates grow from the bottom-left; panel coordinates grow
        /// from the top-left, so the rect is flipped once, here, at the only place that draws it.
        /// </summary>
        void UpdateMarquee()
        {
            Rect? box = _rig?.DragBox;
            if (box == null || _hud.panel == null)
            {
                _marquee.style.display = DisplayStyle.None;
                return;
            }

            // Both corners through the same conversion. Screen min-y is the BOTTOM of the box
            // and panel min-y is the top, so which corner is which flips with the axis — hence the
            // Min/Max pair below rather than using min for left/top directly.
            Vector2 min = ToPanel(box.Value.min);
            Vector2 max = ToPanel(box.Value.max);
            _marquee.style.left = Mathf.Min(min.x, max.x);
            _marquee.style.top = Mathf.Min(min.y, max.y);
            _marquee.style.width = Mathf.Abs(max.x - min.x);
            _marquee.style.height = Mathf.Abs(max.y - min.y);
            _marquee.style.display = DisplayStyle.Flex;
        }

        void OnLayerChanged(int layer)
        {
            // The ruler is the region the player just clicked; answering it on the next 1 Hz
            // pass would make the highlight lag its own click by up to a second.
            RefreshRuler();
        }

        // ------------------------------------------------------------ selection

        /// <summary>
        /// The pick resolver has decided what a click landed on (or the roster has set the
        /// selection directly). Push it into the inspect model and answer in the same frame —
        /// a click that only shows up on the next cadence pass reads as ignored.
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
            RefreshRoster();
        }

        // ------------------------------------------------------------ shared helpers

        static Label Label(string text, string ussClass)
        {
            var label = new Label(text);
            if (!string.IsNullOrEmpty(ussClass)) label.AddToClassList(ussClass);
            return label;
        }

        VisualElement Region(VisualElement parent, string title, string slotClass)
        {
            var region = new VisualElement();
            region.AddToClassList("region");
            if (!string.IsNullOrEmpty(slotClass)) region.AddToClassList(slotClass);
            region.Add(Label(title, "region__hdr"));
            parent.Add(region);
            return region;
        }

        /// <summary>A pill with an optional icon and a label. State classes are the caller's:
        /// a chip is neutral until something says it is on or off.</summary>
        static VisualElement Chip(IconBadge? icon, string label)
        {
            var chip = new VisualElement();
            chip.AddToClassList("chip");
            if (icon != null) chip.Add(icon);
            chip.Add(Label(label, string.Empty));
            return chip;
        }

        static VisualElement Off(VisualElement chip)
        {
            chip.AddToClassList("chip--off");
            return chip;
        }

        // ------------------------------------------------------------ A1 ledger

        void BuildLedger()
        {
            var region = Region(_leftColumn, "A1 · RESOURCES", "slot-ledger");
            _ledgerRows = new VisualElement();
            _ledgerRows.AddToClassList("ledger");
            region.Add(_ledgerRows);
        }

        void RefreshLedger()
        {
            var world = _boot!.World;
            if (world == null) return;
            _ledger.Refresh(world.Views.Current);

            if (_ledgerQty.Count != _ledger.Rows.Count)
            {
                _ledgerRows.Clear();
                _ledgerQty.Clear();
                foreach (var row in _ledger.Rows)
                {
                    var line = new VisualElement();
                    line.AddToClassList("ledger__row");
                    if (!row.Real) line.AddToClassList("ledger__row--planned");
                    line.Add(new IconBadge(row.IconKey));
                    line.Add(Label(row.Name, "ledger__name"));
                    var qty = Label(string.Empty, "ledger__qty");
                    _ledgerQty.Add(qty);
                    line.Add(qty);
                    line.tooltip = row.Real
                        ? row.Name + " — stacks on the ground, counted from the frame"
                        : row.Name + " — arrives with the economy (M4)";
                    _ledgerRows.Add(line);
                }
                _ledgerRows.Add(Label(
                    "grey rows are the economy to come", "ledger__note"));
            }

            for (int i = 0; i < _ledger.Rows.Count; i++)
                _ledgerQty[i].text = _ledger.Rows[i].Real ? _ledger.Rows[i].Quantity.ToString() : "—";
        }

        // ------------------------------------------------------------ A7 architect

        /// <summary>
        /// Categories in catalogue order, each with a few of its tools. Every icon key exists in
        /// the registry; every control is display-only until placement tools land in M3. Picking
        /// a category still answers, by showing that category's shape, so the region reads as a
        /// thing that will work rather than a decoration that ignores you.
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

        VisualElement _buildTools = null!;
        int _buildCategory = -1;

        void BuildPalette()
        {
            var region = Region(_bottomStack, "A7 · BUILD", "build");
            _buildPanel = region;
            region.style.display = DisplayStyle.None;
            var cats = new VisualElement();
            cats.AddToClassList("build__cats");

            // The categories scroll. The left column is not tall enough for the ledger and ten
            // categories at once -- and USS lengths here are reference pixels, not screen ones,
            // so the column is about 353 of them however large the monitor is. Left to flex, the
            // shortfall lands on whichever region yields first: it took the last row off the
            // palette, and when the palette was told not to yield it crushed the ledger instead.
            // A list too long for its panel should scroll rather than quietly lose its end.
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("build__scroll");
            for (int i = 0; i < BuildCategories.Length; i++)
            {
                var (key, label, _) = BuildCategories[i];
                var chip = Chip(new IconBadge(key), label);
                int index = i;
                chip.tooltip = label + " — placement tools arrive with M3";
                chip.RegisterCallback<ClickEvent>(_ => SelectBuildCategory(index));
                cats.Add(chip);
            }
            scroll.Add(cats);
            region.Add(scroll);

            _buildTools = new VisualElement();
            _buildTools.AddToClassList("build__tools");
            region.Add(_buildTools);
        }

        // ------------------------------------------------------------ B17 settings
        /// <summary>
        /// The settings panel: one section of graphics toggles, opened with Escape.
        ///
        /// <para>Built like the Build palette and hidden the same way, but parented to the shell
        /// root rather than to the bottom stack, because it is centred on the screen rather than
        /// sitting over the bar.</para>
        ///
        /// <para><b>It is not a modal.</b> There is no scrim and nothing is blocked: the world
        /// runs, the camera orbits and the clock ticks while it is open, because the only reason
        /// to have the panel is to watch the board change as a lever moves. Clicks that land on it
        /// already stop at the panel edge through <see cref="PointOverUi"/>, which is the one piece
        /// of blocking it actually needs.</para>
        ///
        /// <para>Rows are chips rather than UI Toolkit <c>Toggle</c> controls, because a chip that
        /// lights is the on-off idiom the rest of this HUD already uses, and the first real control
        /// in the project should arrive with the interface pass rather than in a settings panel.
        /// Every row carries its full name beside the icon, per the labels rule (owner,
        /// 2026-09-16); nothing here is named in C#, only keyed.</para>
        /// </summary>
        void BuildSettings()
        {
            var region = Region(_hud, "B17 · " + Registry.Label(SettingsDirector.PanelKey).ToUpperInvariant(),
                "settings");
            _settingsPanel = region;
            region.style.display = DisplayStyle.None;

            region.Add(Label(Registry.Label(SettingsDirector.GraphicsKey), "settings__section"));

            foreach (GraphicsOption option in SettingsDirector.All)
            {
                string key = SettingsDirector.KeyOf(option);
                var chip = Chip(new IconBadge(key), Registry.Label(key));
                chip.AddToClassList("settings__row");

                // Said in the tooltip rather than on the row, because it is a fact about what the
                // toggle costs, not about what it does: two of these are read as the frame is
                // drawn and two send the board back through the mesher.
                chip.tooltip = SettingsDirector.NeedsRedraw(option)
                    ? "Redraws the board when it changes"
                    : "Takes effect on the next frame";

                GraphicsOption captured = option;
                chip.RegisterCallback<ClickEvent>(_ => _directors?.Settings.Toggle(captured));
                _settingRows[option] = chip;
                region.Add(chip);
            }

            region.Add(Label("Escape closes. Graphics only, and none of it is in the save.",
                "settings__note"));
        }

        void OnSettingsChanged() =>
            _settingsPanel.style.display =
                _directors != null && _directors.Settings.Open ? DisplayStyle.Flex : DisplayStyle.None;

        void OnSettingChanged(GraphicsOption option)
        {
            if (_directors == null) return;
            if (!_settingRows.TryGetValue(option, out VisualElement? row)) return;
            row.EnableInClassList("chip--on", _directors.Settings.IsOn(option));
        }

        const string BuildTabKey = "ui.tab.build";

        /// <summary>Open or close the placement palette from the bottom bar.</summary>
        void ToggleBuildPalette()
        {
            bool opening = _buildPanel.style.display == DisplayStyle.None;
            _buildPanel.style.display = opening ? DisplayStyle.Flex : DisplayStyle.None;
            _buildButton.EnableInClassList("chip--on", opening);
        }

        void SelectBuildCategory(int index)
        {
            if (_buildCategory == index) return;
            _buildCategory = index;

            _buildTools.Clear();
            foreach (string tool in BuildCategories[index].tools)
                _buildTools.Add(Off(Chip(new IconBadge(tool), IconBadge.Abbreviation(tool))));
        }

        // ------------------------------------------------------------ A2 roster

        void BuildRoster()
        {
            _rosterHost = new VisualElement();
            _rosterHost.AddToClassList("slot-roster");
            _hud.Add(_rosterHost);
        }

        void RefreshRoster()
        {
            var world = _boot!.World;
            if (world == null) return;
            _roster.Refresh(world.Views.Current,
                selected: _directors != null ? _directors.Selection.Pawns : (IReadOnlyList<PawnId>)Array.Empty<PawnId>());

            while (_cards.Count < _roster.Cards.Count)
            {
                // A name and two bars. The placeholder badges and the layer number are gone
                // (owner, 2026-09-16): the badges named nothing a player could read, and the
                // layer is on the ruler and in the inspect pane already. What a card is for is
                // "is this colonist all right", and that is food and mood.
                var card = new VisualElement();
                card.AddToClassList("card");
                var name = Label(string.Empty, "card__name");

                var foodBar = new VisualElement();
                foodBar.AddToClassList("bar");
                foodBar.AddToClassList("bar--food");
                var foodFill = new VisualElement();
                foodFill.AddToClassList("bar__fill");
                foodBar.Add(foodFill);

                var bar = new VisualElement();
                bar.AddToClassList("bar");
                var fill = new VisualElement();
                fill.AddToClassList("bar__fill");
                bar.Add(fill);

                card.Add(name);
                card.Add(foodBar);
                card.Add(bar);

                int index = _cards.Count;

                // Shift is the roster's toggle, exactly as it is in the world: a shift-press on a
                // card turns it on or off without moving the camera, and while shift is held a
                // drag across cards toggles each one it crosses (A2 "drag-select a range"). A
                // plain press keeps the jump: a card is a way of getting to someone far away.
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

                _rosterHost.Add(card);
                _cards.Add(new CardView
                {
                    Root = card, Name = name, Fill = fill, Bar = bar, FoodFill = foodFill,
                });
            }
            while (_cards.Count > _roster.Cards.Count)
            {
                _cards[^1].Root.RemoveFromHierarchy();
                _cards.RemoveAt(_cards.Count - 1);
            }

            for (int i = 0; i < _roster.Cards.Count; i++)
            {
                RosterCard model = _roster.Cards[i];
                CardView view = _cards[i];

                view.Name.text = model.Name;
                view.FoodFill.style.width = Length.Percent(Clamp1000(model.Food));
                view.Fill.style.width = Length.Percent(Clamp1000(model.Mood));
                view.Bar.EnableInClassList("bar--lo", model.Mood < MoodBands.Strained);
                view.Root.EnableInClassList("card--sel", model.Selected);
                view.Root.tooltip = $"{model.Name} — {JobLabels.Label(model.JobDef)}, layer {model.Layer}." +
                    " Click to select.";
            }
        }

        // ------------------------------------------------------------ A3/A4 clock + speed

        void BuildClock()
        {
            var region = Region(_rightColumn, "A3 · TIME   ·   A4 · SPEED", "slot-clock");
            var body = new VisualElement();
            body.AddToClassList("clock");
            _clockTime = Label(string.Empty, "clock__time");
            _clockDate = Label(string.Empty, "clock__date");
            body.Add(_clockTime);
            body.Add(_clockDate);

            var speed = new VisualElement();
            speed.AddToClassList("speed");
            // Shapes, not words and not placeholder badges: stop, play, double, triple. A
            // transport control is the one row on this sheet that needs no naming, because the
            // shapes are older than the game and everyone already reads them (owner, 2026-09-16).
            (string glyph, string name)[] speeds =
            {
                ("■", "Stop"), ("▶", "Play"), ("▶▶", "Double"), ("▶▶▶", "Triple"),
            };
            for (int i = 0; i < speeds.Length; i++)
            {
                int requested = i; // 0 paused, 1..3 speeds — the rig's own convention
                var button = new VisualElement();
                button.AddToClassList("speed__btn");
                button.Add(Label(speeds[i].glyph, "speed__glyph"));
                button.tooltip = speeds[i].name + " — Space pauses, 1/2/3 set speed";
                button.RegisterCallback<ClickEvent>(_ => _rig?.RequestGameSpeed(requested));
                speed.Add(button);
                _speedButtons.Add(button);
            }
            body.Add(speed);
            region.Add(body);
        }

        void RefreshClock()
        {
            var world = _boot!.World;
            if (world == null) return;
            long tick = world.CurrentTick;
            _clockTime.text = $"{GameClock.HourOfDay(tick):00}h";
            _clockDate.text =
                $"Day {GameClock.DayOfMonth(tick)} · {GameClock.MonthName(tick)} · {GameClock.SeasonName(tick)}";
        }

        void RefreshSpeed()
        {
            var world = _boot!.World;
            if (world == null) return;
            int current = world.GameSpeed;
            for (int i = 0; i < _speedButtons.Count; i++)
                _speedButtons[i].EnableInClassList("chip--on", i == current);
        }

        // ------------------------------------------------------------ A5 alerts

        void BuildAlerts()
        {
            var region = Region(_rightColumn, "A5 · ALERTS", "slot-alerts");
            var body = new VisualElement();
            body.AddToClassList("alerts");
            body.Add(Label("No active alerts.", "alerts__empty"));
            body.Add(Label("Conditions arrive with M2; each will carry its layer and a jump target.",
                "alerts__note"));
            region.Add(body);
        }

        // ------------------------------------------------------------ A11 depth ruler

        void BuildRuler()
        {
            var region = Region(_rightColumn, "A11 · DEPTH", "slot-ruler");
            _rulerActive = Label(string.Empty, "ruler__active");
            region.Add(_rulerActive);
            _rulerRows = new VisualElement();
            _rulerRows.AddToClassList("ruler");
            region.Add(_rulerRows);
            region.Add(Label("R / F · Home", "ruler__foot"));
        }

        /// <summary>
        /// One bar, one step per layer, the top step the top layer: a depth ruler reads like a
        /// ruler. A step is the whole width so it can be clicked at any zoom; the active step is
        /// lit, the surface step carries a mark, and a step with colonists on it carries a dot
        /// whose tooltip says how many. The row-per-layer list this replaces took the space of
        /// a panel to say what a scale says at a glance.
        /// </summary>
        void RefreshRuler()
        {
            if (_directors == null) return;
            var world = _boot!.World;
            if (world == null) return;
            _ruler.Refresh(world.Views.Current, _directors.Slice.ActiveLayer, _surfaceLayer);

            // Steps are built once, from the layer count of the first frame with a world in it,
            // and added top layer first so the bar reads downwards like depth does.
            while (_rulerTicks.Count < _ruler.Rows.Count)
            {
                // Rows come out of the model top layer first and the bar is a column, so the
                // first step built is the top step and tick i is Rows[i]. This used to index
                // from the far end, which built the bar upside down: the top step was layer 0
                // and the bottom step the sky. Clicking low on the ruler took you high, which
                // is how it was reported from a playtest on 2026-09-16.
                int layer = _ruler.Rows[_rulerTicks.Count].Layer;
                var tick = new VisualElement();
                tick.AddToClassList("ruler__tick");
                var dot = new VisualElement();
                dot.AddToClassList("ruler__dot");
                tick.Add(dot);

                int clicked = layer;
                tick.RegisterCallback<ClickEvent>(_ => _directors?.Slice.SetLayer(clicked));

                // The layer this step will actually send, hung on the element so a test can read
                // it. Without it the only observable is the lit step, and the lit step cannot
                // tell these two bugs apart: build the bar upside down, read it with a mirrored
                // index, and the highlight lands correctly while the click still goes elsewhere.
                // A test written against the highlight passed with the bug restored.
                tick.userData = clicked;
                _rulerRows.Add(tick);
                _rulerTicks.Add(new RulerTickView { Root = tick, Dot = dot, Layer = layer });
            }
            if (_rulerTicks.Count != _ruler.Rows.Count) return;

            _rulerActive.text = "L" + _directors.Slice.ActiveLayer;

            for (int i = 0; i < _rulerTicks.Count; i++)
            {
                RulerTickView view = _rulerTicks[i];

                // By index, not by layer number. Rows[i].Layer is layers-1-i, so indexing the
                // list with a layer number reads a different row for every layer but the middle
                // one — which mirrored the lit step, the surface mark, the colonist dot and the
                // tooltip all at once. The two bugs hid each other: both were mirrored, so the
                // lit step often looked plausible while the click did something else.
                LayerRow model = _ruler.Rows[i];

                view.Root.EnableInClassList("ruler__tick--active", model.Active);
                view.Root.EnableInClassList("ruler__tick--surface", model.Surface);
                view.Dot.style.display = model.Pawns > 0 ? DisplayStyle.Flex : DisplayStyle.None;

                string surface = model.Surface ? " (surface)" : string.Empty;
                string occupancy = model.Occupancy >= 0f
                    ? $"{model.Occupancy * 100f:0}% built"
                    : "occupancy publishes for the active slice only";
                view.Root.tooltip = $"Layer {model.Layer}{surface} — {model.Pawns} colonists, {occupancy}. Click to move the slice.";
            }
        }

        // ------------------------------------------------------------ A9/A10 inspect

        void BuildInspect()
        {
            _inspectPane = Region(_hud, "A9 · INSPECT", "slot-inspect");
            _inspectBody = new VisualElement();
            _inspectBody.AddToClassList("inspect");
            _inspectPane.Add(_inspectBody);
        }

        void RefreshInspect()
        {
            var world = _boot!.World;
            if (world == null || _inspectBody == null) return;
            _inspect.Refresh(world.Views.Current);

            // The structure is rebuilt only when the subject changes; values update in place,
            // so a refresh allocates nothing but the few strings it shows.
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

            _inspectPane.EnableInClassList("inspect--tomb", _inspect.Tombstoned);
            _tombReason.style.display = _inspect.Tombstoned ? DisplayStyle.Flex : DisplayStyle.None;

            _inspectTitle.text = _inspect.Title;
            _inspectKind.text = _inspect.Subtitle;
            _inspectBadge.text = _inspect.Layer >= 0 ? $"L{_inspect.Layer}" : string.Empty;
            _inspectSub.text = SubtitleLine();

            if (_inspect.Subject == InspectSubject.Colonist && !_inspect.Tombstoned)
            {
                _food.Value.text = Percent(_inspect.Food);
                _rest.Value.text = Percent(_inspect.Rest);
                _mood.Value.text = Percent(_inspect.Mood);
                _food.Fill.style.width = Length.Percent(Clamp1000(_inspect.Food));
                _rest.Fill.style.width = Length.Percent(Clamp1000(_inspect.Rest));
                _mood.Fill.style.width = Length.Percent(Clamp1000(_inspect.Mood));
                _mood.Bar.EnableInClassList("bar--lo", _inspect.Mood < MoodBands.Strained);
            }
        }

        string SubtitleLine()
        {
            switch (_inspect.Subject)
            {
                case InspectSubject.Colonist:
                    {
                        // A multi-selection shows the primary colonist in full, with the size of
                        // the set said out loud: "3 selected" is the whole of what a pane can add
                        // to several brackets until commands arrive (A9).
                        string count = _directors != null && _directors.Selection.HasMultiple
                            ? $"{_directors.Selection.Pawns.Count} selected  ·  "
                            : string.Empty;
                        return count + $"{_inspect.Job}  ·  {_inspect.Position}  ·  mood {MoodBands.Band(_inspect.Mood)}";
                    }
                case InspectSubject.Item:
                    return _inspect.Position;
                case InspectSubject.Cell:
                    return "cell readout arrives with cell inspection";
                default:
                    return "click a colonist, an item, or the ground";
            }
        }

        void BuildInspectBody()
        {
            _inspectBody.Clear();

            var header = new VisualElement();
            header.AddToClassList("inspect__hdr");
            header.Add(new IconBadge(
                _inspect.Subject == InspectSubject.Colonist ? "ui.pawn.colonist"
                : _inspect.Subject == InspectSubject.Item ? "ui.res.meal"
                : "ui.overlay.zones",
                IconBadge.ClassAvatar));
            _inspectTitle = Label(string.Empty, "inspect__title");
            header.Add(_inspectTitle);
            _inspectKind = Label(string.Empty, "inspect__kind");
            header.Add(_inspectKind);
            _inspectBadge = Label(string.Empty, "inspect__badge");
            header.Add(_inspectBadge);
            _inspectBody.Add(header);

            _inspectSub = Label(string.Empty, "inspect__sub");
            _inspectBody.Add(_inspectSub);

            if (_inspect.Subject == InspectSubject.Colonist)
            {
                var strip = new VisualElement();
                strip.AddToClassList("inspect__tabstrip");
                foreach (var tab in _inspect.Tabs)
                {
                    var chip = Chip(null, tab.Name);
                    if (tab.Enabled) chip.AddToClassList("chip--on");
                    else Off(chip);
                    chip.tooltip = tab.Enabled ? "Needs" : tab.Name + " — " + tab.Reason;
                    strip.Add(chip);
                }
                _inspectBody.Add(strip);

                var body = new VisualElement();
                body.AddToClassList("inspect__body");
                _food = NeedRow(body, "Food", "ui.need.food");
                _rest = NeedRow(body, "Rest", "ui.need.rest");
                _mood = NeedRow(body, "Mood", "ui.mood.content");
                _inspectBody.Add(body);

                var commands = new VisualElement();
                commands.AddToClassList("commands");
                foreach (var command in _inspect.Commands)
                {
                    var slot = new VisualElement();
                    slot.AddToClassList("command");
                    slot.Add(new IconBadge(command.IconKey));
                    slot.Add(Label(command.Label, "command__label"));
                    slot.tooltip = command.Label + " — " + command.Reason;
                    commands.Add(slot);
                }
                _inspectBody.Add(commands);
            }
            else if (_inspect.Subject == InspectSubject.None)
            {
                var body = new VisualElement();
                body.AddToClassList("inspect__body");
                body.Add(Label($"{_inspect.ColonySize} colonists", "summary__row"));
                for (int job = 0; job < _inspect.JobCounts.Count; job++)
                    if (_inspect.JobCounts[job] > 0)
                        body.Add(Label($"{_inspect.JobCounts[job]} {JobLabels.Label(job)}", "summary__row"));
                body.Add(Label("Salvage lies where it fell: there is no stockpile yet.", "summary__row"));
                _inspectBody.Add(body);
            }

            _tombReason = Label("no longer present — the pane keeps last-known values", "inspect__reason");
            _tombReason.style.display = DisplayStyle.None;
            _inspectBody.Add(_tombReason);
        }

        static NeedViews NeedRow(VisualElement parent, string name, string iconKey)
        {
            var views = new NeedViews();

            var row = new VisualElement();
            row.AddToClassList("need");
            var line = new VisualElement();
            line.AddToClassList("need__line");
            var left = new VisualElement();
            left.style.flexDirection = FlexDirection.Row;
            left.style.alignItems = Align.Center;
            left.Add(new IconBadge(iconKey));
            left.Add(Label(name, "need__name"));
            views.Value = Label(string.Empty, "need__value");
            line.Add(left);
            line.Add(views.Value);
            row.Add(line);

            views.Bar = new VisualElement();
            views.Bar.AddToClassList("bar");
            views.Fill = new VisualElement();
            views.Fill.AddToClassList("bar__fill");
            views.Bar.Add(views.Fill);
            row.Add(views.Bar);

            parent.Add(row);
            return views;
        }

        // ------------------------------------------------------------ A8 tabs, A12 overlays, A14 cancel

        static readonly string[] TabKeys =
        {
            "ui.tab.work", "ui.tab.schedule", "ui.tab.research", "ui.tab.colonists",
            "ui.tab.animals", "ui.tab.wildlife", "ui.tab.bills",
            "ui.tab.factions", "ui.tab.archive", "ui.tab.menu",
        };

        static readonly string[] TabReasons =
        {
            "the work grid arrives with M7", "M7", "M7", "the roster is the top bar",
            "M5", "M5", "M5", "M7", "the archive arrives with M2",
            "save, load and settings arrive with the game menu",
        };

        void BuildTabs()
        {
            var bar = new VisualElement();
            bar.AddToClassList("slot-tabs");

            // Build first, left of Work, because it is the one control on this bar that does
            // something today: it opens the placement palette. It used to be a panel pinned to
            // the left edge, where it competed with the ledger for a column that could not hold
            // both, and it used to be called Architect (owner decisions, 2026-09-16). Trade left
            // the bar in the same breath, so that adding this did not widen the row.
            _buildButton = Chip(new IconBadge(BuildTabKey), Registry.Label(BuildTabKey));
            _buildButton.AddToClassList("tab--build");
            _buildButton.tooltip = "Build, dig and zone — placement tools arrive with M3";
            _buildButton.RegisterCallback<ClickEvent>(_ => ToggleBuildPalette());
            bar.Add(_buildButton);

            for (int i = 0; i < TabKeys.Length; i++)
            {
                // The label comes from the naming registry, which is the whole point of the
                // registry: a name the owner corrects in the CSV reaches the screen without
                // anyone retyping it. Deriving it from the key spelled ui.tab.archive as
                // "Archive" while the registry called it "History".
                var chip = Off(Chip(new IconBadge(TabKeys[i]), Registry.Label(TabKeys[i])));
                chip.tooltip = TabKeys[i] + " — " + TabReasons[i];
                bar.Add(chip);
            }
            _bottomRow.Add(bar);
        }

        static readonly string[] OverlayKeys =
        {
            "ui.overlay.temperature", "ui.overlay.light", "ui.overlay.beauty", "ui.overlay.cleanliness",
            "ui.overlay.roofs", "ui.overlay.zones", "ui.overlay.power", "ui.overlay.salvage",
            "ui.overlay.support", "ui.overlay.traffic",
        };

        /// <summary>
        /// A12 as a strip of icons beside the cancel affordance, not a framed panel: ten labelled
        /// chips took a quarter of the bottom edge and read as a menu that is not open yet. The
        /// icons are the same keys the eventual overlay menu will use, each disabled with its
        /// reason in the tooltip until its channel renders (M4).
        /// </summary>
        void BuildOverlays()
        {
            var strip = new VisualElement();
            strip.AddToClassList("slot-overlays");
            foreach (string key in OverlayKeys)
            {
                var button = new VisualElement();
                button.AddToClassList("overlay__btn");
                button.AddToClassList("chip--off");
                button.Add(new IconBadge(key));
                button.tooltip = key[11..].Capitalise() + " — overlay channels arrive with M4";
                strip.Add(button);
            }
            _bottomRow.Add(strip);
        }

        void BuildCancel()
        {
            var cancel = new VisualElement();
            cancel.AddToClassList("cancel");
            cancel.Add(Label("✕", "cancel__x"));
            cancel.tooltip = "Nothing to cancel — tools and panels arrive with M3";
            _bottomRow.Add(cancel);
        }

        // ------------------------------------------------------------ formatting

        static string Percent(int thousandths) => (thousandths / 10) + "%";

        static float Clamp1000(int value) => Mathf.Clamp(value, 0, 1000) / 10f;
    }

    static class HudStringExtensions
    {
        /// <summary>First letter upper, rest as-is: "temperature" to "Temperature". Chips are
        /// built once, so the allocation is not worth avoiding.</summary>
        public static string Capitalise(this string text) =>
            string.IsNullOrEmpty(text) || char.IsUpper(text[0])
                ? text
                : char.ToUpperInvariant(text[0]) + text[1..];
    }
}
