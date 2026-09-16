#nullable enable
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
        readonly List<CardView> _cards = new List<CardView>();
        VisualElement _rosterHost = null!;
        VisualElement _rulerRows = null!;
        readonly List<RulerRowView> _rulerRowViews = new List<RulerRowView>();
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

        struct CardView
        {
            public VisualElement Root;
            public IconBadge Job;
            public Label Name;
            public Label Layer;
            public VisualElement Fill;
            public VisualElement Bar;
        }

        struct RulerRowView
        {
            public VisualElement Root;
            public Label Count;
            public VisualElement PipFill;
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

            // The left edge is one column, not two absolute slots: the ledger and the architect
            // palette stack inside it, so neither can ever sit on top of the other.
            _leftColumn = new VisualElement();
            _leftColumn.AddToClassList("slot-left");
            _hud.Add(_leftColumn);

            BuildLedger();
            BuildArchitect();
            BuildRoster();
            BuildClock();
            BuildAlerts();
            BuildRuler();
            BuildInspect();
            BuildTabs();
            BuildOverlays();
            BuildCancel();

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
        }

        void Detach()
        {
            if (_directors == null) return;
            _directors.Selection.Changed -= OnSelectionChanged;
            _directors.Slice.LayerChanged -= OnLayerChanged;
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
        public bool PointOverUi(Vector2 screenPosition)
        {
            if (_hud == null || _hud.panel == null) return false;
            VisualElement? hit = _hud.panel.Pick(RuntimePanelUtils.ScreenToPanel(_hud.panel, screenPosition));
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
            var region = Region(_leftColumn, "A1 · RESOURCES", string.Empty);
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
        static readonly (string key, string label, string[] tools)[] ArchitectCategories =
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

        VisualElement _archPalette = null!;
        int _archCategory = -1;

        void BuildArchitect()
        {
            var region = Region(_leftColumn, "A7 · ARCHITECT", "arch");
            var cats = new VisualElement();
            cats.AddToClassList("arch__cats");
            for (int i = 0; i < ArchitectCategories.Length; i++)
            {
                var (key, label, _) = ArchitectCategories[i];
                var chip = Chip(new IconBadge(key), label);
                int index = i;
                chip.tooltip = label + " — placement tools arrive with M3";
                chip.RegisterCallback<ClickEvent>(_ => SelectArchitectCategory(index));
                cats.Add(chip);
            }
            region.Add(cats);

            _archPalette = new VisualElement();
            _archPalette.AddToClassList("arch__tools");
            region.Add(_archPalette);
        }

        void SelectArchitectCategory(int index)
        {
            if (_archCategory == index) return;
            _archCategory = index;

            _archPalette.Clear();
            foreach (string tool in ArchitectCategories[index].tools)
                _archPalette.Add(Off(Chip(new IconBadge(tool), IconBadge.Abbreviation(tool))));
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
                selected: _directors != null ? _directors.Selection.Pawn : PawnId.None);

            while (_cards.Count < _roster.Cards.Count)
            {
                var card = new VisualElement();
                card.AddToClassList("card");
                var top = new VisualElement();
                top.AddToClassList("card__top");
                top.Add(new IconBadge("ui.pawn.colonist"));
                var job = new IconBadge("ui.status.idle");
                top.Add(job);
                var name = Label(string.Empty, "card__name");
                var layer = Label(string.Empty, "card__layer");
                var bar = new VisualElement();
                bar.AddToClassList("bar");
                var fill = new VisualElement();
                fill.AddToClassList("bar__fill");
                bar.Add(fill);
                card.Add(top);
                card.Add(name);
                card.Add(layer);
                card.Add(bar);

                int index = _cards.Count;
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    if (index < _roster.Cards.Count && _boot!.World != null)
                        _directors?.ChooseColonist(_roster.Cards[index].Id, _boot.World.Views.Current);
                });
                _rosterHost.Add(card);
                _cards.Add(new CardView
                {
                    Root = card, Job = job, Name = name, Layer = layer, Fill = fill, Bar = bar,
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
                view.Layer.text = "L" + model.Layer;
                view.Job.SetKey(JobLabels.IconKey(model.JobDef));
                view.Fill.style.width = Length.Percent(Mathf.Clamp(model.Mood, 0, 100));
                view.Bar.EnableInClassList("bar--lo", model.Mood < MoodBands.Strained);
                view.Root.EnableInClassList("card--sel", model.Selected);
                view.Root.tooltip = $"{model.Name} — {JobLabels.Label(model.JobDef)}, layer {model.Layer}." +
                    " Click to select.";
            }
        }

        // ------------------------------------------------------------ A3/A4 clock + speed

        void BuildClock()
        {
            var region = Region(_hud, "A3 · TIME   ·   A4 · SPEED", "slot-clock");
            var body = new VisualElement();
            body.AddToClassList("clock");
            _clockTime = Label(string.Empty, "clock__time");
            _clockDate = Label(string.Empty, "clock__date");
            body.Add(_clockTime);
            body.Add(_clockDate);

            var speed = new VisualElement();
            speed.AddToClassList("speed");
            (string key, string label)[] speeds =
            {
                ("ui.speed.pause", "pause"), ("ui.speed.play", "1×"),
                ("ui.speed.fast", "2×"), ("ui.speed.ultra", "3×"),
            };
            for (int i = 0; i < speeds.Length; i++)
            {
                int requested = i; // 0 paused, 1..3 speeds — the rig's own convention
                var button = new VisualElement();
                button.AddToClassList("speed__btn");
                button.Add(new IconBadge(speeds[i].key));
                button.Add(Label(speeds[i].label, "speed__label"));
                button.tooltip = speeds[i].label + " — Space pauses, 1/2/3 set speed";
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
            var region = Region(_hud, "A5 · ALERTS", "slot-alerts");
            var body = new VisualElement();
            body.AddToClassList("alerts");
            body.Add(Label("No active alerts.", "alerts__empty"));
            body.Add(Label("Conditions arrive with M2; each will carry its layer and a jump target.",
                "alerts__empty"));
            region.Add(body);
        }

        // ------------------------------------------------------------ A11 depth ruler

        void BuildRuler()
        {
            var region = Region(_hud, "A11 · DEPTH", "slot-ruler");
            _rulerRows = new VisualElement();
            _rulerRows.AddToClassList("ruler");
            region.Add(_rulerRows);
            region.Add(Label(
                "Click a layer to move the slice · Page Up / Page Down, or R / F · Home frames it.",
                "ruler__foot"));
        }

        void RefreshRuler()
        {
            if (_directors == null) return;
            var world = _boot!.World;
            if (world == null) return;
            _ruler.Refresh(world.Views.Current, _directors.Slice.ActiveLayer, _surfaceLayer);

            // Rows are built once, from the layer count of the first frame with a world in it.
            while (_rulerRowViews.Count < _ruler.Rows.Count)
            {
                int layer = _ruler.Rows[_rulerRowViews.Count].Layer;
                var row = new VisualElement();
                row.AddToClassList("ruler__row");
                row.Add(Label(layer.ToString(), "ruler__label"));
                var pip = new VisualElement();
                pip.AddToClassList("ruler__pip");
                var fill = new VisualElement();
                fill.AddToClassList("ruler__pipfill");
                pip.Add(fill);
                var count = Label(string.Empty, "ruler__count");
                var mark = new VisualElement();
                mark.AddToClassList("ruler__mark");
                row.Add(pip);
                row.Add(count);
                row.Add(mark);

                int clicked = layer;
                row.RegisterCallback<ClickEvent>(_ => _directors?.Slice.SetLayer(clicked));
                _rulerRows.Add(row);
                _rulerRowViews.Add(new RulerRowView { Root = row, Count = count, PipFill = fill });
            }
            if (_rulerRowViews.Count != _ruler.Rows.Count) return;

            for (int i = 0; i < _ruler.Rows.Count; i++)
            {
                LayerRow model = _ruler.Rows[i];
                RulerRowView view = _rulerRowViews[i];

                view.Root.EnableInClassList("ruler__row--active", model.Active);
                view.Count.text = model.Pawns > 0 ? model.Pawns.ToString() : string.Empty;
                view.PipFill.style.width = Length.Percent(Mathf.Clamp01(model.Occupancy) * 100f);

                string suffix = model.Surface ? " (surface) " : " — ";
                string occupancy = model.Occupancy >= 0f
                    ? $"{model.Occupancy * 100f:0}% of the layer is built"
                    : "occupancy publishes for the active slice only";
                view.Root.tooltip = $"Layer {model.Layer}{suffix}{model.Pawns} colonists, {occupancy}";
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
                _mood.Value.text = _inspect.Mood.ToString();
                _food.Fill.style.width = Length.Percent(Clamp1000(_inspect.Food));
                _rest.Fill.style.width = Length.Percent(Clamp1000(_inspect.Rest));
                _mood.Fill.style.width = Length.Percent(Mathf.Clamp(_inspect.Mood, 0, 100));
                _mood.Bar.EnableInClassList("bar--lo", _inspect.Mood < MoodBands.Strained);
            }
        }

        string SubtitleLine()
        {
            switch (_inspect.Subject)
            {
                case InspectSubject.Colonist:
                    return $"{_inspect.Job}  ·  {_inspect.Position}  ·  mood {MoodBands.Band(_inspect.Mood)}";
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
            "ui.tab.animals", "ui.tab.wildlife", "ui.tab.bills", "ui.tab.trade",
            "ui.tab.factions", "ui.tab.archive", "ui.tab.menu",
        };

        static readonly string[] TabReasons =
        {
            "the work grid arrives with M7", "M7", "M7", "the roster is the top bar",
            "M5", "M5", "M5", "M7", "M7", "the archive arrives with M2",
            "save, load and settings arrive with the game menu",
        };

        void BuildTabs()
        {
            var bar = new VisualElement();
            bar.AddToClassList("slot-tabs");
            for (int i = 0; i < TabKeys.Length; i++)
            {
                var chip = Off(Chip(new IconBadge(TabKeys[i]), TabKeys[i][7..].Capitalise()));
                chip.tooltip = TabKeys[i] + " — " + TabReasons[i];
                bar.Add(chip);
            }
            _hud.Add(bar);
        }

        static readonly string[] OverlayKeys =
        {
            "ui.overlay.temperature", "ui.overlay.light", "ui.overlay.beauty", "ui.overlay.cleanliness",
            "ui.overlay.roofs", "ui.overlay.zones", "ui.overlay.power", "ui.overlay.salvage",
            "ui.overlay.support", "ui.overlay.traffic",
        };

        void BuildOverlays()
        {
            var region = Region(_hud, "A12 · OVERLAYS", "slot-overlays");
            var grid = new VisualElement();
            grid.AddToClassList("overlays");
            foreach (string key in OverlayKeys)
            {
                var chip = Off(Chip(new IconBadge(key), key[11..].Capitalise()));
                chip.tooltip = key + " — overlay channels arrive with M4";
                grid.Add(chip);
            }
            region.Add(grid);
        }

        void BuildCancel()
        {
            var cancel = new VisualElement();
            cancel.AddToClassList("cancel");
            cancel.Add(Label("✕", "cancel__x"));
            cancel.tooltip = "Nothing to cancel — tools and panels arrive with M3";
            _hud.Add(cancel);
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
