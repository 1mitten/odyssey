#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// How the Build palette arranges itself. Three layouts over one set of contents.
    ///
    /// <para><b>They are layouts, not modes.</b> Nothing about what the palette can do changes
    /// between them, and nothing about what is selected changes either — <see cref="BuildPaletteModel"/>
    /// holds the selection and all three read it. That is the whole reason the switch is allowed
    /// to exist: a layout switch that could lose the player's place would be a setting nobody
    /// dares touch.</para>
    /// </summary>
    public enum BuildPaletteLayout
    {
        /// <summary>Bands stacked down a wide panel: categories, then sub-types, then materials.
        /// The default on a fresh profile.</summary>
        Rows,

        /// <summary>Categories down a left rail, everything else in a pane beside it. The one
        /// layout whose height does not change when the category does.</summary>
        Rail,

        /// <summary>Two dense rows of icon-only tiles along the bottom. The least of the board
        /// hidden, and the most hover required.</summary>
        Bar,
    }

    /// <summary>
    /// The Build palette's selection and the arithmetic around it: which category is open, which
    /// sub-type is armed, what it is being made of, and what that costs.
    ///
    /// <para><b>One object for three layouts, and that is the point.</b> The specification asks
    /// for a switch between Rows, Rail and Bar that "never changes selection state". The only way
    /// to make that true rather than to keep making it true is for there to be one place the
    /// selection lives, which no layout owns and every layout reads. A layout builder in the shell
    /// draws what this says and decides nothing — the same division <see cref="PaletteTools"/> and
    /// <see cref="HudCommands"/> already draw between data and the shell that renders it.</para>
    ///
    /// <para><b>It is in this assembly so the fast tier can see it.</b> Every rule below — the
    /// category reset, the per-sub-type material memory, the breadcrumb, the refusal to switch
    /// mid-drag — is a claim about behaviour, and a claim that can only be checked by opening the
    /// game and clicking is a claim nobody checks. There is no <c>UnityEngine</c> in here (ADR
    /// 0003) and there is no reference to a <c>VisualElement</c>.</para>
    ///
    /// <para><b>What it does not own.</b> It does not own the tool the player is holding — that is
    /// <see cref="DesignateDirector"/>, which the world also reads, and two of those would be the
    /// drift <see cref="PaletteTool"/>'s own header warns about. This model <i>drives</i> the
    /// director and then reads it back.</para>
    /// </summary>
    public sealed class BuildPaletteModel
    {
        readonly DesignateDirector _designate;

        /// <summary>
        /// Where the chosen layout is kept, so that the switcher in this panel's header and the
        /// row in the settings panel are one preference rather than two. Optional, because the
        /// fast tier builds this model without a settings panel and gets <see cref="Default"/>.
        /// </summary>
        readonly SettingsDirector? _settings;

        /// <summary>The material last chosen for each sub-type key, so that a player who builds
        /// stone walls and wooden floors is not re-choosing on every visit.</summary>
        readonly Dictionary<string, int> _lastMaterial = new Dictionary<string, int>();

        int _category;
        string _subType = string.Empty;

        /// <summary>Stock held, by <see cref="StuffHandle"/> value. Null until the shell has a
        /// snapshot to answer from, which is what the "everything is in stock" default means.</summary>
        Func<int, int>? _stock;

        /// <summary>Units of a material one cell of a building costs. Null until wired.</summary>
        Func<int, int, int>? _cost;

        /// <summary>The layout a profile that has never been told starts in.</summary>
        public const BuildPaletteLayout Default = BuildPaletteLayout.Rows;

        public BuildPaletteModel(DesignateDirector designate, SettingsDirector? settings = null)
        {
            _designate = designate ?? throw new ArgumentNullException(nameof(designate));
            _settings = settings;

            // Whichever control moves the preference, this model hears about it and the shell
            // rebuilds — so the settings row and the header switcher cannot come to disagree
            // about which layout is showing.
            if (settings != null)
                settings.BuildPaletteLayoutChanged += layout => LayoutChanged?.Invoke(layout);

            // Seeded, not armed. See SelectCategory.
            SelectCategory(0, force: true);
        }

        /// <summary>Raised when the layout changes, so the shell can tear one down and raise
        /// another. Never raised by a selection change: the three layouts are rebuilt from the
        /// same state, so a selection change is a repaint rather than a rebuild.</summary>
        public event Action<BuildPaletteLayout>? LayoutChanged;

        /// <summary>Raised when the category, sub-type or material changes — everything a drawn
        /// layout has to repaint and nothing it has to rebuild.</summary>
        public event Action? SelectionChanged;

        // ---------------------------------------------------------------- layout

        public BuildPaletteLayout Layout => _settings?.BuildPaletteLayout ?? Default;

        /// <summary>
        /// Switch layout, keeping the selection exactly.
        ///
        /// <para><b>Refused mid-drag</b>, and the refusal is the interesting half. A drag is a
        /// gesture in progress against a panel that is about to be replaced by a differently
        /// shaped one, and the cells already swept have no meaning in the new panel's coordinates.
        /// Finishing or abandoning first is one extra click and it is the only version of this
        /// that cannot leave a half-placed run on the board.</para>
        /// </summary>
        /// <returns>False when the switch was refused, which is only ever because of a drag.</returns>
        public bool SetLayout(BuildPaletteLayout layout)
        {
            if (_designate.Dragging) return false;
            if (Layout == layout) return true;

            // The director both stores the choice and raises the event this model forwards, so
            // there is no second write and no second notification here.
            _settings?.SetBuildPaletteLayout(layout);
            return true;
        }

        /// <summary>Whether a stored ordinal names a layout. A file can hold anything; a switcher
        /// of three cannot.</summary>
        public static bool IsLayout(int value) =>
            value >= (int)BuildPaletteLayout.Rows && value <= (int)BuildPaletteLayout.Bar;

        /// <summary>Every layout, in switcher order, for the shell and the settings panel.</summary>
        public static readonly BuildPaletteLayout[] Layouts =
        {
            BuildPaletteLayout.Rows, BuildPaletteLayout.Rail, BuildPaletteLayout.Bar,
        };

        /// <summary>What a layout is called on its switcher button and in the settings panel.
        /// Three words, chosen to describe the shape rather than to rank it.</summary>
        public static string LayoutName(BuildPaletteLayout layout) => layout switch
        {
            BuildPaletteLayout.Rows => "Rows",
            BuildPaletteLayout.Rail => "Rail",
            BuildPaletteLayout.Bar => "Bar",
            _ => "Rows",
        };

        // ---------------------------------------------------------------- category

        /// <summary>Which of <see cref="PaletteTools.Categories"/> is open.</summary>
        public int Category => _category;

        public string CategoryKey => PaletteTools.Categories[_category].key;

        public string CategoryLabel => Registry.Label(CategoryKey);

        /// <summary>The open category's tools, which is what the sub-type tier draws.</summary>
        public IReadOnlyList<string> SubTypes => PaletteTools.Categories[_category].tools;

        /// <summary>
        /// Open a category, and land on something usable inside it.
        ///
        /// <para><b>The reset is to the first <i>buildable</i> entry, not to the first entry</b>
        /// (specification). Most of this palette is drawn-disabled and will be for a while, so a
        /// reset that always lands on index zero would, for five of the seven categories, arm
        /// nothing and light nothing — which reads as the category being empty rather than as its
        /// contents being unfinished. Landing on the first live tool means the panel always has
        /// something armed to show, and falls back to the first entry only when the category has
        /// no live tool at all.</para>
        /// </summary>
        public void SelectCategory(int index) => SelectCategory(index, force: false);

        /// <summary>
        /// <paramref name="force"/> is the constructor seeding the palette rather than the player
        /// choosing, and it is the difference between pointing at a tool and picking one up.
        ///
        /// <para><b>Nothing is armed until the player asks.</b> This model is built when the HUD
        /// attaches to its directors, long before the palette is opened — and it used to arm its
        /// landing sub-type there, which put the whole game into build mode on the first frame with
        /// a wall on the cursor that nobody had asked for. The command bar's Build cap was lit from
        /// the start, which is how it surfaced (owner: <i>"the build button still stays bold when
        /// it shouldn't"</i>), but the lit cap was telling the truth: a click on the world really
        /// would have placed a wall.</para>
        ///
        /// <para>So the seeded pass sets where the palette is <i>pointing</i> and arms nothing. A
        /// click on a category is a player choice and does arm — which is what makes the specified
        /// reset, "selecting a category resets the sub-type to its first buildable entry",
        /// meaningful rather than decorative.</para>
        /// </summary>
        void SelectCategory(int index, bool force)
        {
            if (index < 0 || index >= PaletteTools.Categories.Length) return;
            if (!force && _category == index) return;

            _category = index;

            string[] tools = PaletteTools.Categories[index].tools;
            string landing = tools.Length > 0 ? tools[0] : string.Empty;
            foreach (string tool in tools)
            {
                if (!PaletteTools.TryGet(tool, out _)) continue;
                landing = tool;
                break;
            }

            ApplySubType(landing, arm: !force);
            if (!force) SelectionChanged?.Invoke();
        }

        // ---------------------------------------------------------------- sub-type

        /// <summary>The armed sub-type's registry key, or empty in a category with no tools.</summary>
        public string SubType => _subType;

        /// <summary>Whether a sub-type key does anything yet. Everything else is drawn and
        /// disabled, which is how the shape of the game stays visible before its contents
        /// exist.</summary>
        public static bool IsBuildable(string key) => PaletteTools.TryGet(key, out _);

        /// <summary>
        /// Whether a selection change should close the palette.
        ///
        /// <para><b>The missing half of a rule that was already half-written</b> (owner,
        /// 2026-09-18: <i>"If I have the build menu open and I haven't selected anything to build
        /// — I go to click on any tile for info — that panel appears but underneath the build
        /// menu"</i>). Opening the palette has cleared the selection since 2026-09-17, because
        /// both panels dock into the same bottom-left corner and the corner holds one. Selecting
        /// something <i>while</i> the palette was up was never wired, so the pane opened
        /// underneath it.</para>
        ///
        /// <para><b>Asked of the reason and not of the input device</b> (owner's choice of the two
        /// offered). What collides is the inspect pane, so whatever raises the pane is what closes
        /// the palette: a world click, a roster card, an alert jump, a drag box. The other
        /// direction — a selection that <i>goes away</i> — leaves the palette alone, and that
        /// half is load-bearing rather than tidy: opening the palette calls
        /// <c>Selection.Clear()</c>, so a rule that fired on <see cref="SelectionChange.Cleared"/>
        /// would close the palette on the frame it opened.</para>
        ///
        /// <para>An empty selection never closes it either, which is what covers a shift-click
        /// that takes the last colonist back out again: nothing is left to show, so there is
        /// nothing for the corner to argue about.</para>
        ///
        /// <para><b>A tool in hand stays in hand</b> (owner, same exchange). This closes a panel;
        /// it does not put the player's tool down. Note that a <i>world</i> selection cannot
        /// happen while a tool is armed at all — <c>SliceCameraRig.WorldToolArmed</c> sends that
        /// click to the designate path instead — so the only way to reach this holding something
        /// is a roster click, and disarming there would silently undo a choice nobody revoked.</para>
        /// </summary>
        public static bool ClosedBy(SelectionChange reason, bool selectionIsEmpty)
        {
            if (selectionIsEmpty) return false;

            switch (reason)
            {
                case SelectionChange.Picked:
                case SelectionChange.Chosen:
                case SelectionChange.Boxed:
                case SelectionChange.Toggled:
                case SelectionChange.Similar:
                    return true;

                // Cleared, Died and LayerChanged are the selection being taken away rather than
                // made. Nothing new is shown, so nothing wants the corner.
                default:
                    return false;
            }
        }

        /// <summary>
        /// Arm a sub-type, restoring whatever it was last made of.
        ///
        /// <para>The material memory is keyed on the sub-type rather than held once for the
        /// palette because the two choices are not independent in practice: a player who has
        /// settled on stone walls has usually not settled on stone everything, and a single
        /// remembered material would silently re-answer a question they had already answered
        /// differently one tier up.</para>
        /// </summary>
        /// <summary>
        /// <paramref name="key"/> may be the one already pointed at, and that case matters.
        ///
        /// <para>The palette points at its category's first buildable entry from the moment it is
        /// built, without arming it — so the very first thing a player clicks is usually the tile
        /// the palette was already pointing at, and an early return on "same key" made that click
        /// do nothing at all. It reads as a dead button, which is the same class of fault as a chip
        /// that arms a tool and never lights. The guard is therefore "already pointed at
        /// <i>and</i> already in the player's hand".</para>
        /// </summary>
        public void SelectSubType(string key)
        {
            if (_subType == key && SubTypeIsArmed) return;
            if (!Array.Exists(PaletteTools.Categories[_category].tools, t => t == key)) return;

            ApplySubType(key, arm: true);
            SelectionChanged?.Invoke();
        }

        void ApplySubType(string key, bool arm)
        {
            _subType = key;
            if (key.Length == 0 || !PaletteTools.TryGet(key, out PaletteTool tool)) return;

            // Arming is a toggle, so a second call would put the tool back down. Only a player
            // choice reaches this.
            if (arm && !tool.IsArmed(_designate)) tool.Arm(_designate);

            if (!tool.WantsMaterial) return;
            if (_lastMaterial.TryGetValue(key, out int remembered) && IsStocked(remembered))
                _designate.ChooseStuff(remembered);
            else if (!IsStocked(_designate.Stuff))
                FallBackToSomethingStocked();
        }

        // ---------------------------------------------------------------- material

        /// <summary>
        /// Whether the sub-type the palette is pointing at is the tool the player is actually
        /// holding.
        ///
        /// <para>Asked of <see cref="DesignateDirector"/> rather than assumed from
        /// <see cref="SubType"/>, which is the same discipline <see cref="PaletteTool"/>'s own
        /// header argues for: the palette points at something from the moment it is built, and a
        /// tile that lights because the palette is pointing at it — rather than because the tool is
        /// in the player's hand — is a tile that says "armed" when nothing is.</para>
        /// </summary>
        public bool SubTypeIsArmed =>
            PaletteTools.TryGet(_subType, out PaletteTool tool) && tool.IsArmed(_designate);

        /// <summary>What the armed sub-type is being made of, as a <see cref="StuffHandle"/>.</summary>
        public int Material => _designate.Stuff;

        /// <summary>
        /// Whether the material tier means anything right now. False for an order — a verb applied
        /// to what is already there is not made of anything — and false for a disabled sub-type.
        ///
        /// <para><b>Also false while a pinned action is held</b>, which is not the same condition
        /// and was a real bug the tests caught. The sub-type does not clear when the player picks
        /// Cancel up: it is still there, remembered, and it is what they go back to when they put
        /// Cancel down. But asking it whether it is made of something, while the thing the cursor
        /// will actually do is cancel orders, left the material band showing under a mode that has
        /// no material — a panel offering a choice that could not have any effect.</para>
        /// </summary>
        public bool WantsMaterial =>
            ArmedPinned.Length == 0 &&
            PaletteTools.TryGet(_subType, out PaletteTool tool) && tool.WantsMaterial;

        public void SelectMaterial(int stuff)
        {
            if (!WantsMaterial || !IsStocked(stuff)) return;
            if (_designate.Stuff == stuff) return;

            _designate.ChooseStuff(stuff);
            if (_subType.Length > 0) _lastMaterial[_subType] = stuff;
            SelectionChanged?.Invoke();
        }

        // ---------------------------------------------------------------- plant

        /// <summary>What the armed sub-type is planted with, as a <see cref="PlantHandle"/>.</summary>
        public int Plant => _designate.Plant;

        /// <summary>
        /// Whether the plant tier means anything right now. The growing zone's counterpart of
        /// <see cref="WantsMaterial"/>, with one deliberate difference: Grow zone is itself a
        /// pinned action, so the rule "hidden while a pinned action is held" exempts the tool
        /// whose payload the tier <i>is</i>. Held, the zone tool's next drag plants — which is
        /// exactly when the picker is wanted. Another pinned action held (cancel, mine) hides it,
        /// on the same reasoning that hides the material band: the panel would be offering a
        /// choice the next drag could not use.
        /// </summary>
        public bool WantsPlant
        {
            get
            {
                if (!PaletteTools.TryGet(_subType, out PaletteTool tool) || !tool.WantsPlant)
                    return false;
                string pinned = ArmedPinned;
                return pinned.Length == 0 || pinned == PaletteTools.GrowZone;
            }
        }

        /// <summary>
        /// Choose the crop. Does not arm the tool, and does not remember per sub-type the way
        /// <see cref="SelectMaterial"/> does — one crop exists, and a memory table for one row is
        /// the second source of nothing.
        /// </summary>
        public void SelectPlant(int plant)
        {
            if (!WantsPlant) return;
            if (!Array.Exists(PaletteTools.Plants, p => p == plant)) return;
            if (_designate.Plant == plant) return;

            _designate.ChoosePlant(plant);
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Where the shell learns how much of a material the colony holds. Left null until a
        /// snapshot exists, and null means "in stock" rather than "out of stock" — an interface
        /// that greys every material out because it has not been told yet is worse than one that
        /// offers a build the hauler then has to wait on.
        /// </summary>
        public void ReadStockFrom(Func<int, int>? stock)
        {
            _stock = stock;
            if (WantsMaterial && !IsStocked(_designate.Stuff)) FallBackToSomethingStocked();
        }

        /// <summary>Where the shell supplies the cost table, which lives in the simulation
        /// assembly this one cannot see. A function rather than a copied table, so that
        /// <c>SiteView</c>'s standing objection — "the interface keeping its own copy of the cost
        /// table, which is two sources for one number" — stays answered.</summary>
        public void ReadCostFrom(Func<int, int, int>? cost) => _cost = cost;

        public bool IsStocked(int stuff) => _stock == null || _stock(stuff) > 0;

        void FallBackToSomethingStocked()
        {
            foreach (int stuff in PaletteTools.Materials)
            {
                if (!IsStocked(stuff)) continue;
                _designate.ChooseStuff(stuff);
                return;
            }
        }

        // ---------------------------------------------------------------- readouts

        /// <summary>
        /// Which of the pinned actions the player is holding, or empty when they are holding
        /// a placement tool or nothing at all.
        ///
        /// <para>Asked of <see cref="DesignateDirector"/> rather than remembered here, for the
        /// reason <see cref="PaletteTool"/>'s own header gives: a second record of which tool is
        /// armed is a record that can drift from the first, and the symptom is not a compile error
        /// but a panel that says one thing while the cursor does another.</para>
        /// </summary>
        public string ArmedPinned
        {
            get
            {
                foreach (string key in PaletteTools.Pinned)
                    if (PaletteTools.TryGet(key, out PaletteTool tool) && tool.IsArmed(_designate))
                        return key;
                return string.Empty;
            }
        }

        /// <summary>
        /// The order being held, pinned or not — <see cref="ArmedPinned"/>, or the one order tool
        /// that lives in a category rather than on the strip: taking power lines up (design 32).
        /// What the armed banner names and colours itself from, so a player holding the remove
        /// tool is not told they are building a conduit.
        /// </summary>
        public string ArmedOrder
        {
            get
            {
                string pinned = ArmedPinned;
                if (pinned.Length > 0) return pinned;
                return _designate.Tool == DesignateTool.RemoveConduit ? PaletteTools.Unwire : string.Empty;
            }
        }

        /// <summary>
        /// The key the armed banner <em>names</em> the held order by: <see cref="ArmedOrder"/>,
        /// except that the Mine order reads <b>Dig</b> on soft ground (design 62 §4) — over the
        /// cell under the pointer, or over a drag's start cell for as long as the drag runs, which
        /// is the cell its run was decided from. The colour stays <see cref="ArmedOrder"/>'s: it is
        /// one order under two words, and the strip's button lit for it is the Mine button.
        /// </summary>
        public string ArmedWordKey
        {
            get
            {
                string order = ArmedOrder;
                return order == PaletteTools.Mine ? DigOrMine.OrderKey(_designate.PointerTerrain) : order;
            }
        }

        /// <summary>
        /// Arm or put down a pinned action. The same toggle the category tools use, so a player
        /// can always put a tool down the way they picked it up.
        /// </summary>
        public void TogglePinned(string key)
        {
            if (!Array.Exists(PaletteTools.Pinned, k => k == key)) return;
            if (!PaletteTools.TryGet(key, out PaletteTool tool)) return;

            tool.Arm(_designate);
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// What the panel's header line should say: the mode, in the armed action's own words,
        /// while one is held — otherwise the ordinary build breadcrumb.
        ///
        /// <para>The mode replaces the breadcrumb rather than sitting beside it, because the two
        /// would be describing different things at the same time. While Cancel is held the
        /// player's next drag cancels orders; what the palette would have built if they had not
        /// picked Cancel up is not what is about to happen, and a header showing both reads as
        /// though it might be.</para>
        /// </summary>
        public string HeaderLine
        {
            get
            {
                string pinned = ArmedPinned;
                return pinned.Length > 0 ? Registry.Label(pinned) : Breadcrumb;
            }
        }

        /// <summary>
        /// The live breadcrumb — "Zones › Growing zone › Carrot" — present in all three layouts.
        ///
        /// <para>It stops at whatever tier is meaningful: an order has no material, so its
        /// breadcrumb has two parts, and a category whose sub-type is disabled still names the
        /// sub-type, because the player did select it.</para>
        /// </summary>
        public string Breadcrumb
        {
            get
            {
                string crumb = CategoryLabel;
                if (_subType.Length > 0) crumb += " › " + Registry.Label(_subType);
                if (WantsPlant)
                {
                    string plant = BuildLabels.PlantKey(Plant);
                    if (plant.Length > 0) crumb += " › " + Registry.Label(plant);
                }
                if (WantsMaterial)
                {
                    string material = BuildLabels.StuffKey(Material);
                    if (material.Length > 0) crumb += " › " + Registry.Label(material);
                }
                return crumb;
            }
        }

        /// <summary>
        /// The per-unit cost, once, as the panel's one mono readout — "6 wood / tile". Empty when
        /// there is nothing to price, which is an order, a disabled sub-type, or a shell that has
        /// not been handed a cost table.
        ///
        /// <para>Once rather than per button (specification): a number under every material is
        /// four numbers saying the same thing about four buttons only one of which is armed, and
        /// it is the armed one the player is pricing.</para>
        /// </summary>
        public string CostLine
        {
            get
            {
                if (_cost == null || !WantsMaterial) return string.Empty;

                string material = BuildLabels.Stuff(Material);
                if (material.Length == 0) return string.Empty;

                int units = _cost(BuildingOf(_subType), Material);
                if (units <= 0) return string.Empty;

                return units + " " + material + " / " + UnitWord(_subType);
            }
        }

        /// <summary>
        /// What one of a thing is called in the cost line. A wall is priced per tile like
        /// everything else that is placed cell by cell, and the word is here rather than in the
        /// registry because it is a unit of measure rather than a name in the game.
        /// </summary>
        static string UnitWord(string key) => "tile";

        /// <summary>
        /// The <see cref="BuildingHandle"/> a sub-type key places, or <see cref="BuildingHandle.None"/>
        /// for an order. The inverse of <see cref="BuildLabels.BuildingKeys"/>, walked rather than
        /// kept as a second table, because a second table is the thing that goes stale.
        /// </summary>
        public static int BuildingOf(string key)
        {
            for (int i = 1; i < BuildLabels.BuildingKeys.Length; i++)
                if (BuildLabels.BuildingKeys[i] == key) return i;
            return BuildingHandle.None;
        }
    }
}
