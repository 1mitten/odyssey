#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// A7, the Build palette: one header, three layouts, one selection.
    ///
    /// <para><b>Why this is its own file.</b> The palette used to be a hundred lines inside
    /// <c>HudShell.Bar.cs</c>, between the command bar and the settings panel, which was fine
    /// while it was one column of chips. It is now three arrangements of four tiers with a mode
    /// banner and a switcher, and it reads better beside its own model than in the middle of the
    /// bar's file.</para>
    ///
    /// <para><b>The shell decides nothing.</b> Every question this file could ask — which category
    /// is open, what is armed, what that is made of, what it costs, which layout to draw — is
    /// answered by <see cref="BuildPaletteModel"/> in the Unity-free assembly. What is left here
    /// is the part that genuinely needs a renderer: boxes, and where they go. That division is why
    /// "switching layout never changes selection" is a property of the design rather than a thing
    /// three builders each have to remember.</para>
    ///
    /// <para><b>Where the panel sits</b> (owner, 2026-09-17: <i>"tight and flush to other elements
    /// to enable full use of space"</i>). The specification drew all three floating at a 28 px
    /// margin, over a bare board with no HUD behind it. The real screen has the stores panel down
    /// the left edge and the roster strip across the top, so a floating panel there covers both.
    /// All three layouts are docked instead, flush on whatever is under them — the command bar, or
    /// the inspect pane's header when something is selected — which is the same rule the bar and
    /// the popovers already follow and the same words the owner used about the popovers a day
    /// earlier: "directly above the build button … no spacing and padding to ensure tight space".</para>
    ///
    /// <para><b>Only Bar spans the screen.</b> Rows and Bar both did at first, which is what the
    /// mockup drew. Seven category tiles stretched across 1920 are seven very wide tiles with a
    /// small icon adrift in each, and the board they cover is the board the player is aiming at —
    /// so the owner asked for the default to go the other way (2026-09-17): <i>"short width as
    /// possible but evenly sized … you could probably fit 4 on a row but increase the height and
    /// try to use the left hand side of the screen instead of the width"</i>. Rows is a 372 px
    /// column now, four category tiles across and therefore two deep, and it is anchored to the
    /// button that raised it exactly as Rail is — which, the Build cap being first on the bar,
    /// puts both against the left edge.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        BuildPaletteModel? _palette;

        VisualElement _buildBody = null!;
        VisualElement _buildSwitch = null!;
        Label _buildCrumb = null!;
        VisualElement _buildModeIcon = null!;

        /// <summary>Every tile currently on screen that has a lit state, by the thing it stands
        /// for. Rebuilt with the layout and walked on every repaint, so a repaint never has to
        /// know which layout it is repainting.</summary>
        readonly Dictionary<string, VisualElement> _buildTiles = new Dictionary<string, VisualElement>();

        readonly List<(int stuff, VisualElement tile)> _buildMaterialTiles =
            new List<(int, VisualElement)>();

        /// <summary>The plant tier's tiles, by the crop each stands for. Only the growing zone
        /// opens the tier, but the tiles exist in every layout, built once with it.</summary>
        readonly List<(int plant, VisualElement tile)> _buildPlantTiles =
            new List<(int, VisualElement)>();

        readonly List<(int index, VisualElement tile)> _buildCategoryTiles =
            new List<(int, VisualElement)>();

        /// <summary>The cost readout the current layout is using: the body's in Rows and Rail,
        /// the header's in Bar.</summary>
        Label? _buildCost;

        /// <summary>The header's copy, which only Bar shows.</summary>
        Label _buildHeaderCost = null!;

        // ============================================================ the panel

        /// <summary>
        /// Raise the palette: the chrome that does not change with the layout, and then the layout.
        /// </summary>
        void BuildPalette()
        {
            _buildPanel = Popover("build", "Build", () => SetBuildPalette(false), "bp");

            // The header the specification describes is not the one Popover builds — it carries a
            // breadcrumb and a layout switcher — so the stock one is taken back out and replaced.
            // It carried the four order buttons too until they left for the strip in the
            // right-hand gutter (HudShell.Orders.cs). Going through Popover first is still worth it: the fill, the docking
            // and the Escape contract are the parts that must not differ between panels.
            if (_buildPanel.childCount > 0) _buildPanel.RemoveAt(0);
            _buildPanel.Insert(0, BuildPaletteHeader());

            _buildBody = new VisualElement();
            _buildBody.AddToClassList("bp__body");
            _buildPanel.Add(_buildBody);

            // No hint line. The specification put "Left click places · drag for a run · right click
            // cancels" under the panel; the owner had it removed outright (2026-09-17). It is a
            // sentence about the three most basic gestures in the game, printed permanently over
            // the board, and a player who needs it needs it once. The tooltips on every tile still
            // say what a drag does.
            _worldUi.Add(_buildPanel);
        }

        /// <summary>
        /// Attach the model. Called once the directors exist, because the palette is a view of
        /// <see cref="DesignateDirector"/> and cannot be built before there is one.
        /// </summary>
        void BindBuildPalette()
        {
            if (_directors == null) return;

            _palette = new BuildPaletteModel(_directors.Designate, _directors.Settings);
            _palette.LayoutChanged += _ => RaiseBuildLayout();
            _palette.SelectionChanged += MarkBuildState;

            // The cost table lives in the simulation assembly, which Odyssey.Hud cannot see and
            // this one can. Handed over as a function rather than copied, so that SiteView's
            // standing objection — "the interface keeping its own copy of the cost table, which is
            // two sources for one number" — stays answered.
            _palette.ReadCostFrom((building, stuff) =>
                ConstructionContent.IsBuilding(building)
                    ? ConstructionContent.BuildingAt(building).costCount
                    : 0);

            // And what the colony holds, so a material it has none of stops being offered. Read
            // from the published snapshot on demand rather than cached: the palette asks while it
            // is repainting, which is already the moment the answer has to be current.
            _palette.ReadStockFrom(StockOf);

            // The world can arm or disarm a tool without the palette being touched — Escape,
            // right-click, a hotkey — and neither the panel nor the cap on the bar would hear
            // about it. The cap is the half the owner noticed: it stayed lit after Escape.
            _directors.Designate.ToolChanged += _ =>
            {
                MarkBuildState();
                MarkBuildMode();
            };

            MarkBuildMode();

            RaiseBuildLayout();
        }

        /// <summary>
        /// How many units of a material the colony is holding, counted off the published
        /// snapshot.
        ///
        /// <para>Loose stacks on the ground, which is what the stores panel counts too, and for
        /// the same reason: a wall is built out of what a hauler can fetch. A material with no
        /// item behind it can never be held, so it answers zero rather than pretending.</para>
        /// </summary>
        int StockOf(int stuff)
        {
            WorldSnapshot? snapshot = _boot == null ? null : _boot.World?.Views.Current;
            if (snapshot == null || !ConstructionContent.IsBuildable(stuff)) return 0;

            int item = ConstructionContent.StuffAt(stuff).item;
            if (item < 0) return 0;

            int held = 0;
            ReadOnlySpan<ThingView> things = snapshot.Things;
            for (int i = 0; i < things.Length; i++)
                if (things[i].DefIndex == item) held += things[i].Stack;
            return held;
        }

        /// <summary>
        /// The header, in two halves on one row: what is selected, and the controls.
        ///
        /// <para><b>It was two stacked lines in Rows until 2026-09-17.</b> The controls there were
        /// eight buttons — three of switcher, four actions and the way out — and with "BUILD" and
        /// a three-part breadcrumb in front of them the row came to 426 px against a 372 px panel.
        /// Then the four actions left for the orders strip down the right edge
        /// (<c>HudShell.Orders.cs</c>) and the owner asked for the rest to come back up: <i>"shift
        /// the toggle view, esc and x onto the same row as the build text — this will tidy that
        /// up"</i>.</para>
        ///
        /// <para><b>The halves stay, and they are what makes one row possible.</b> The identity
        /// half shrinks and its breadcrumb ellipsises; the controls half does not shrink at all.
        /// So the longest breadcrumb the content table can produce — "Structure › Roof and floor
        /// above › Wood", which is not reachable until roofs are built but is in the table today —
        /// gives way to the switcher and the X rather than pushing them off the end. A single
        /// wrapping row would instead break wherever it happened to run out of room, which could
        /// put the close button on a line of its own.</para>
        /// </summary>
        VisualElement BuildPaletteHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("bp__hdr");

            // --- what is selected
            var identity = new VisualElement();
            identity.AddToClassList("bp__hdr-id");

            identity.Add(HudText.Make("BUILD", HudTextRole.PanelLabel, ussClass: "panel__label"));

            // The mode icon sits between the panel label and the line, and is empty except while
            // one of the four actions is held — at which point it and the line beside it are that
            // action's own colour, which is what says which mode the player is in.
            _buildModeIcon = new VisualElement { pickingMode = PickingMode.Ignore };
            _buildModeIcon.AddToClassList("bp__mode-icon");
            _buildModeIcon.style.display = DisplayStyle.None;
            identity.Add(_buildModeIcon);

            _buildCrumb = HudText.Make(string.Empty, HudTextRole.Body, ussClass: "bp__crumb");
            identity.Add(_buildCrumb);
            header.Add(identity);

            // --- the controls
            var controls = new VisualElement();
            controls.AddToClassList("bp__hdr-ctl");

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            controls.Add(spacer);

            // The cost, in the header, for Bar only — the other two have room for it in the body,
            // and two dense rows do not. Its own class as well as the shared one, so that the
            // layout builders can find this copy and not the body's.
            _buildHeaderCost = HudText.Make(string.Empty, HudTextRole.Body, numeric: true, "bp__cost");
            _buildHeaderCost.AddToClassList("bp__cost--hdr");
            controls.Add(_buildHeaderCost);

            _buildSwitch = BuildLayoutSwitcher();
            controls.Add(_buildSwitch);

            controls.Add(HudText.Make("ESC", HudTextRole.Hotkey, ussClass: "bp__esc"));

            var close = new VisualElement();
            close.AddToClassList("bp__action");
            close.AddToClassList("bp__action--close");
            // The shared class as well as its own: "every window carries an X" is a rule with a
            // test behind it, and a panel that built a private close button would pass by
            // looking right and fail the rule.
            close.AddToClassList("panel__close");
            close.Add(new HudGlyph(HudGlyphKind.Close, 13f, HudTokens.HeaderNeutralInk));
            close.tooltip = "Close Build — Esc";
            close.RegisterCallback<ClickEvent>(_ => SetBuildPalette(false));
            controls.Add(close);

            header.Add(controls);
            return header;
        }

        /// <summary>The key that arms a tool, if it has one, as " (M)". Read from the binding map
        /// rather than written out, so a rebind reaches the tooltip.</summary>
        string HotkeyLegend(string key)
        {
            if (_directors == null) return string.Empty;

            HotkeyAction? action = key == PaletteTools.Mine ? HotkeyAction.ToolMine
                : key == PaletteTools.Fell ? HotkeyAction.ToolFell
                : key == PaletteTools.Cancel ? HotkeyAction.ToolCancel
                : key == PaletteTools.GrowZone ? HotkeyAction.ToolGrowZone
                : (HotkeyAction?)null;
            if (action == null) return string.Empty;

            string cap = HotkeyDirector.Display(_directors.Hotkeys.Key(action.Value, 0));
            return cap.Length == 0 ? string.Empty : " (" + cap + ")";
        }

        VisualElement BuildLayoutSwitcher()
        {
            var group = new VisualElement();
            group.AddToClassList("bp__switch");

            foreach (BuildPaletteLayout layout in BuildPaletteModel.Layouts)
            {
                var button = new VisualElement { name = "layout-" + layout };
                button.AddToClassList("bp__switch-btn");
                button.Add(new HudGlyph(PaletteGlyphs.For(layout), 13f, HudTokens.TextDim));
                button.tooltip = BuildPaletteModel.LayoutName(layout);

                BuildPaletteLayout chosen = layout;
                button.RegisterCallback<ClickEvent>(_ => _palette?.SetLayout(chosen));
                group.Add(button);
            }

            return group;
        }

        // ============================================================ the three layouts

        /// <summary>
        /// Throw the body away and build the current layout. The selection is untouched, because
        /// it was never in here.
        /// </summary>
        void RaiseBuildLayout()
        {
            if (_palette == null) return;

            _buildBody.Clear();
            _buildCost = null;
            _buildHeaderCost.text = string.Empty;
            _buildTiles.Clear();
            _buildCategoryTiles.Clear();
            _buildMaterialTiles.Clear();
            _buildPlantTiles.Clear();

            _buildPanel.EnableInClassList("bp--rows", _palette.Layout == BuildPaletteLayout.Rows);
            _buildPanel.EnableInClassList("bp--rail", _palette.Layout == BuildPaletteLayout.Rail);
            _buildPanel.EnableInClassList("bp--bar", _palette.Layout == BuildPaletteLayout.Bar);

            switch (_palette.Layout)
            {
                case BuildPaletteLayout.Rail: RaiseRailLayout(); break;
                case BuildPaletteLayout.Bar: RaiseBarLayout(); break;
                default: RaiseRowsLayout(); break;
            }

            if (BuildPaletteOpen) PlaceBuildPalette();
            MarkBuildState();
        }

        /// <summary>
        /// 4a, Rows, and the default: three bands down a full-width panel, each divided from the
        /// next, each reading left to right.
        /// </summary>
        void RaiseRowsLayout()
        {
            var cats = Band("bp__cats");
            for (int i = 0; i < PaletteTools.Categories.Length; i++)
                cats.Add(CategoryTile(i, "bp__cat"));
            _buildBody.Add(cats);

            _buildBody.Add(Divider());

            var subs = Band("bp__subs");
            foreach (string key in _palette!.SubTypes) subs.Add(SubTypeTile(key, "bp__sub"));
            _buildBody.Add(subs);

            _buildBody.Add(Divider());

            // The plant band, on the same footing as the material one below: always present,
            // dimmed to a heading when nothing armed wants it, because the alternative — the
            // band appearing and vanishing with the tool — is the panel-height jump every other
            // reserved band in this file exists to prevent. One crop today; the band is one
            // button wide and cheap to keep.
            var plants = new VisualElement();
            plants.AddToClassList("bp__band");
            plants.AddToClassList("bp__mats");
            Label plantWord = HudText.Make("PLANT", HudTextRole.PanelLabel, ussClass: "bp__mats-label");
            plantWord.AddToClassList("bp__plants-label");
            plants.Add(plantWord);

            var plantRow = new VisualElement();
            plantRow.AddToClassList("bp__mats-row");
            foreach (int plant in PaletteTools.Plants) plantRow.Add(PlantTile(plant, "bp__mat"));
            plants.Add(plantRow);
            _buildBody.Add(plants);

            _buildBody.Add(Divider());

            // The material band stacks in a column: the word, the buttons, then the price. Across
            // a full-width band the three sat on one line with the cost pushed to the far right;
            // in a 372 px column that line does not exist, and a 66 px label column in front of
            // two buttons would spend a fifth of the width on a word.
            var mats = new VisualElement();
            mats.AddToClassList("bp__band");
            mats.AddToClassList("bp__mats");
            mats.Add(HudText.Make("MATERIAL", HudTextRole.PanelLabel, ussClass: "bp__mats-label"));

            var buttons = new VisualElement();
            buttons.AddToClassList("bp__mats-row");
            foreach (int stuff in PaletteTools.Materials) buttons.Add(MaterialTile(stuff, "bp__mat"));
            mats.Add(buttons);

            _buildCost = HudText.Make(string.Empty, HudTextRole.Body, numeric: true, "bp__cost");

            // Reserved, like Rail's. An empty label does not stand as tall as a full one, and an
            // order has no price — so without this the panel would still move by a line between a
            // category that opens on a wall and one that opens on something unbuilt.
            _buildCost.style.minHeight = HudText.LineHeight(HudTextRole.Body);
            mats.Add(_buildCost);
            _buildBody.Add(mats);
        }

        /// <summary>
        /// 4b, Rail: categories down a 186 px column, everything else in a pane beside it.
        ///
        /// <para>The point of this one is that <b>the panel's height never changes when you switch
        /// category</b>, so nothing below it reflows — which is why the pane is a fixed height and
        /// the sub-type grid is allowed to end in empty cells rather than closing up.</para>
        /// </summary>
        void RaiseRailLayout()
        {
            var split = new VisualElement();
            split.AddToClassList("bp__split");

            var rail = new VisualElement();
            rail.AddToClassList("bp__rail");
            for (int i = 0; i < PaletteTools.Categories.Length; i++)
                rail.Add(CategoryTile(i, "bp__rail-row"));
            split.Add(rail);

            var pane = new VisualElement();
            pane.AddToClassList("bp__pane");

            pane.Add(HudText.Make(_palette!.CategoryLabel.ToUpperInvariant(),
                HudTextRole.PanelLabel, ussClass: "bp__pane-label"));

            var subs = new VisualElement();
            subs.AddToClassList("bp__sub-grid");
            foreach (string key in _palette.SubTypes) subs.Add(SubTypeTile(key, "bp__sub-tile"));
            pane.Add(subs);

            pane.Add(Divider());

            pane.Add(HudText.Make("MATERIAL", HudTextRole.PanelLabel, ussClass: "bp__pane-label"));

            var mats = new VisualElement();
            mats.AddToClassList("bp__mat-grid");
            foreach (int stuff in PaletteTools.Materials) mats.Add(MaterialTile(stuff, "bp__mat-tile"));
            pane.Add(mats);

            // The plant tier, under the materials and before the spacer: same band, same rule —
            // Rail's pane is a fixed height, so the tier is always laid out and only ever shown
            // or hidden, never built.
            pane.Add(Divider());
            pane.Add(HudText.Make("PLANT", HudTextRole.PanelLabel, ussClass: "bp__pane-label"));

            var plantGrid = new VisualElement();
            plantGrid.AddToClassList("bp__mat-grid");
            foreach (int plant in PaletteTools.Plants) plantGrid.Add(PlantTile(plant, "bp__mat-tile"));
            pane.Add(plantGrid);

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            pane.Add(spacer);

            _buildCost = HudText.Make(string.Empty, HudTextRole.Body, numeric: true, "bp__cost");

            // Rail alone reserves the line. An empty label does not stand as tall as a full one,
            // which left the panel 17 px shorter on a category that has nothing to price — the
            // third and last place the constant-height promise leaked, after the sub-type grid and
            // the material band. The figure is the renderer's, not the model's, which is why it is
            // taken from HudText here rather than written into HudLayout.
            _buildCost.style.minHeight = HudText.LineHeight(HudTextRole.Body);
            pane.Add(_buildCost);

            split.Add(pane);
            _buildBody.Add(split);
        }

        /// <summary>
        /// 4c, Bar: two dense rows of icon-only tiles, which is the least of the board hidden and
        /// the most hover required.
        /// </summary>
        void RaiseBarLayout()
        {
            var cats = new VisualElement();
            cats.AddToClassList("bp__bar-row");
            for (int i = 0; i < PaletteTools.Categories.Length; i++)
                cats.Add(CategoryTile(i, "bp__bar-cat"));

            var hint = HudText.Make("icon tiles name themselves on hover", HudTextRole.Meta,
                ussClass: "bp__bar-hint");
            hint.pickingMode = PickingMode.Ignore;
            var hintSpacer = new VisualElement { pickingMode = PickingMode.Ignore };
            hintSpacer.style.flexGrow = 1;
            cats.Add(hintSpacer);
            cats.Add(hint);
            _buildBody.Add(cats);

            var content = new VisualElement();
            content.AddToClassList("bp__bar-row");

            var subs = new VisualElement();
            subs.AddToClassList("bp__bar-subs");
            foreach (string key in _palette!.SubTypes) subs.Add(SubTypeTile(key, "bp__bar-sub"));
            content.Add(subs);

            // The plant group, between the sub-types and the materials, because it belongs to the
            // sub-type tier — it is the zone tool's payload — and only rides in the material
            // band's shape because that is the chip the dense row has room for.
            var plants = new VisualElement();
            plants.AddToClassList("bp__bar-mats");
            foreach (int plant in PaletteTools.Plants) plants.Add(PlantTile(plant, "bp__mat"));
            content.Add(plants);

            var mats = new VisualElement();
            mats.AddToClassList("bp__bar-mats");
            foreach (int stuff in PaletteTools.Materials) mats.Add(MaterialTile(stuff, "bp__mat"));
            content.Add(mats);

            _buildBody.Add(content);

            // Bar has no room for a body cost line, so the header's one is used instead.
            _buildCost = _buildHeaderCost;
        }

        VisualElement Band(string extra)
        {
            var band = new VisualElement();
            band.AddToClassList("bp__band");
            band.AddToClassList(extra);
            return band;
        }

        static VisualElement Divider()
        {
            var rule = new VisualElement { pickingMode = PickingMode.Ignore };
            rule.AddToClassList("bp__divider");
            return rule;
        }

        // ============================================================ the tiers

        /// <summary>
        /// One category, in whichever shape the layout wants. The colours are the same in all
        /// three, because the hue is the category's identity and a category that changed colour
        /// with the layout would be telling the player it was a different category.
        /// </summary>
        VisualElement CategoryTile(int index, string shape)
        {
            var (key, _) = PaletteTools.Categories[index];
            string label = Registry.Label(key);

            var tile = new VisualElement { name = "cat-" + key };
            tile.AddToClassList("bp__tile");
            tile.AddToClassList(shape);

            HudTheme.BuildTier tier = HudTheme.BuildCategoryTiers[index];
            Color ink = HudTokens.Convert(tier.Ink);

            tile.Add(new HudGlyph(PaletteGlyphs.For(key), shape == "bp__cat" ? 20f : 18f, ink));

            if (shape != "bp__bar-cat")
            {
                Label name = HudText.Make(label, HudTextRole.Row, ussClass: "bp__tile-label");
                name.style.color = ink;
                tile.Add(name);
            }

            tile.tooltip = label;
            tile.RegisterCallback<ClickEvent>(_ =>
            {
                _palette?.SelectCategory(index);
                // The sub-type tier's contents change with the category, so this is the one
                // selection change that is a rebuild rather than a repaint.
                RaiseBuildLayout();
            });

            _buildCategoryTiles.Add((index, tile));
            return tile;
        }

        /// <summary>One sub-type. Neutral at rest, cyan when armed, and quiet when the thing
        /// behind the key does not exist yet.</summary>
        VisualElement SubTypeTile(string key, string shape)
        {
            var tile = new VisualElement { name = "sub-" + key };
            tile.AddToClassList("bp__tile");
            tile.AddToClassList(shape);

            bool live = BuildPaletteModel.IsBuildable(key);
            float size = shape == "bp__sub-tile" ? 22f : shape == "bp__bar-sub" ? 20f : 17f;
            tile.Add(new HudGlyph(PaletteGlyphs.For(key), size,
                HudTokens.Convert(live ? HudTheme.SubTypeInk : HudTheme.SubTypeDisabledInk)));

            string name = Registry.Label(key);
            if (shape != "bp__bar-sub")
                tile.Add(HudText.Make(name, HudTextRole.Row, ussClass: "bp__tile-label"));

            if (live)
            {
                tile.tooltip = name + " — drag a box over the world";
                tile.RegisterCallback<ClickEvent>(_ => _palette?.SelectSubType(key));
            }
            else
            {
                tile.AddToClassList("bp__tile--off");
                // The disabled state states its reason, which is the only thing that separates
                // "not yet" from "you have done something wrong".
                tile.tooltip = name + " — not built yet; the tool arrives with its content";
            }

            _buildTiles[key] = tile;
            return tile;
        }

        /// <summary>
        /// One material, tinted from the material so that it reads before its label does.
        ///
        /// <para><b>The icon is the game's own sprite</b>, not a drawn shape (specification: "do
        /// not redraw or replace the material icons"). Materials are the one tier of this panel
        /// whose art does not change, so this stays an <see cref="IconBadge"/> on the same key the
        /// stores panel draws.</para>
        /// </summary>
        VisualElement MaterialTile(int stuff, string shape)
        {
            var tile = new VisualElement { name = "mat-" + stuff };
            tile.AddToClassList("bp__tile");
            tile.AddToClassList("bp__material");
            tile.AddToClassList(shape);

            bool large = shape == "bp__mat-tile";

            string key = BuildLabels.StuffKey(stuff);
            tile.Add(new IconBadge(key, large ? 26f : IconBadge.RowSize));

            // Rail's grid keeps the specification's heavier type, because a 86 px tile with a
            // 14 px word in it is mostly empty. Rows takes the same role as the sub-type buttons
            // above it (owner, 2026-09-17: "evenly sized in font and size as the other buttons but
            // keep the style") — the tint and the doubled border are what say "terminal choice",
            // and they survive the type going back to the row's.
            tile.Add(HudText.Make(Registry.Label(key),
                large ? HudTextRole.Name : HudTextRole.Row, ussClass: "bp__material-label"));

            tile.RegisterCallback<ClickEvent>(_ => _palette?.SelectMaterial(stuff));

            _buildMaterialTiles.Add((stuff, tile));
            return tile;
        }

        /// <summary>
        /// One crop, on the material band's chip, lit as a sub-type.
        ///
        /// <para><b>Why drawn art and not an <see cref="IconBadge"/>.</b> The badge wants pixel
        /// art under its key and the icon library has none for a crop, so a badge here would
        /// draw the outlined square — which <c>HudGeometryTests</c> forbids anywhere in the
        /// palette. The glyph is the same vector stroke the categories and sub-types wear, and
        /// pixel art for the carrot arrives with the icon sheet that draws it.</para>
        ///
        /// <para><b>Why the lit state is the sub-type's.</b> A crop is not a material — it is
        /// the growing zone tool's payload, chosen the way a wall's sub-type is — so it wears
        /// <c>bp__tile--on</c> and its label turns with <c>bp__tile--on .bp__tile-label</c>,
        /// both already in the sheet. No inline painting, no second lit style, nothing for a
        /// second tier to drift away from.</para>
        /// </summary>
        VisualElement PlantTile(int plant, string shape)
        {
            string key = BuildLabels.PlantKey(plant);

            var tile = new VisualElement { name = "plant-" + plant };
            tile.AddToClassList("bp__tile");
            tile.AddToClassList(shape);

            bool large = shape == "bp__mat-tile";
            tile.Add(new HudGlyph(PaletteGlyphs.For(key), large ? 22f : 17f,
                HudTokens.Convert(HudTheme.SubTypeInk)));
            tile.Add(HudText.Make(Registry.Label(key),
                large ? HudTextRole.Name : HudTextRole.Row, ussClass: "bp__tile-label"));

            tile.tooltip = Registry.Label(key) + " — the crop the zone sows";
            tile.RegisterCallback<ClickEvent>(_ => _palette?.SelectPlant(plant));

            _buildPlantTiles.Add((plant, tile));
            return tile;
        }

        // ============================================================ state

        /// <summary>
        /// Light the Build cap on the command bar when, and only when, the player is in build mode.
        ///
        /// <para><b>Which is a different question from "is the palette open"</b>, and getting the
        /// two confused is the fault the owner reported (2026-09-17): <i>"when I come out of build
        /// mode by escaping etc, the build button still stays bold when it shouldn't and is
        /// confusing — it's an indicator to whether you are truly in build mode"</i>. The cap was
        /// drawn with a permanent accent fill because Build is the bar's primary item, and the
        /// faint wash that was meant to say "open" was invisible underneath it. So the fill is the
        /// state now and an outline is the resting style, and the state it reports is this
        /// one.</para>
        ///
        /// <para><b>Build mode is a tool in the player's hand, and nothing else</b> (owner,
        /// 2026-09-17: <i>"when you click off build mode the button shouldn't be highlighted — ie
        /// I click esc, that button is not highlighted at all"</i>). It counted an open panel as
        /// well for a while, on the reasoning that a player who has opened the palette is about to
        /// build. That is not what the cap is being asked. Escape puts the tool down before it
        /// closes anything (<c>09-ui-and-input.md</c> §6), so counting the panel meant the first
        /// Escape left the cap lit over an empty hand — which is the state the owner was
        /// complaining about in the first place, one step further on.</para>
        ///
        /// <para>What is left is the honest question: <b>will the next click on the world place,
        /// cancel or dig something, rather than select it?</b> Any designate tool answers yes, and
        /// all of them are reached through this panel. An open palette with nothing chosen answers
        /// no, and the open palette is its own evidence that it is open.</para>
        /// </summary>
        void MarkBuildMode()
        {
            if (_buildItem == null) return;

            bool on = _directors != null && _directors.Designate.Tool != DesignateTool.None;

            _buildItem.EnableInClassList("cmd--on", on);
            _buildIcon?.Inherit(on ? HudTokens.OnAccent : HudTokens.Accent);
        }

        /// <summary>
        /// Repaint the open palette on the middle cadence, beside the stores panel it shares a
        /// question with.
        ///
        /// <para><b>Because stock moves without the player touching the palette.</b> Everything
        /// else here repaints on an event — a click, a layout switch, a tool armed elsewhere — and
        /// that was enough for every tier but the materials, which are tinted by whether the colony
        /// has any. A hauler emptying the last stack would otherwise leave a button promising a
        /// build that cannot start, until something unrelated happened to repaint it. Shut, it
        /// costs nothing; open, it is two buttons and the same sweep of things the stores panel is
        /// doing a line above.</para>
        /// </summary>
        void RefreshBuildPalette()
        {
            if (BuildPaletteOpen) MarkBuildState();
        }

        /// <summary>
        /// Repaint everything that can be lit, without rebuilding anything.
        ///
        /// <para>Each tier asks the model what it is rather than being told, which is the same
        /// discipline the old palette's <c>MarkArmedTool</c> was rewritten into: a chip that arms
        /// a tool and never lights reads to a player as the click having missed, and a second list
        /// of what is armed is how that happens.</para>
        /// </summary>
        void MarkBuildState()
        {
            if (_palette == null) return;

            string armedAction = _palette.ArmedPinned;

            // --- the header line, and the mode it is in
            HudColour? mode = armedAction.Length > 0 ? HudTheme.PinnedActionHue(armedAction) : null;
            _buildCrumb.text = _palette.HeaderLine;
            _buildCrumb.style.color = HudTokens.Convert(mode ?? HudTheme.Accent);

            _buildModeIcon.Clear();
            _buildModeIcon.style.display = mode.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (mode.HasValue)
                _buildModeIcon.Add(new HudGlyph(PaletteGlyphs.For(armedAction), 14f,
                    HudTokens.Convert(mode.Value)));

            // The panel wears the mode too: a hairline along its top edge in the held action's
            // colour. The header line alone is a sentence to read; the edge is seen without
            // reading, which is the difference the owner asked for.
            _buildPanel.style.borderTopColor = HudTokens.Convert(mode ?? HudTheme.PanelBorder);
            _buildPanel.style.borderTopWidth = mode.HasValue ? 2f : HudTheme.BorderWidth;

            // The four order buttons are painted by MarkOrders, in the strip they now live in.
            // They are deliberately not repainted from here as well: two painters for one button
            // is the drift this file spends its comments avoiding.

            // --- the switcher
            int at = 0;
            foreach (BuildPaletteLayout layout in BuildPaletteModel.Layouts)
            {
                if (at >= _buildSwitch.childCount) break;
                VisualElement button = _buildSwitch[at++];
                bool on = layout == _palette.Layout;
                button.EnableInClassList("bp__switch-btn--on", on);
                if (button.childCount > 0 && button[0] is HudGlyph glyph)
                    glyph.Tint = on ? HudTokens.OnAccent : HudTokens.TextDim;
            }

            // --- categories
            foreach (var (index, tile) in _buildCategoryTiles)
            {
                HudTheme.BuildTier tier = HudTheme.BuildCategoryTiers[index];
                bool on = index == _palette.Category;

                tile.style.backgroundColor = HudTokens.Convert(on ? tier.SelectedFill : tier.Fill);
                Color edge = HudTokens.Convert(on ? tier.Selected : tier.Border);
                tile.style.borderTopColor = tile.style.borderRightColor =
                    tile.style.borderBottomColor = tile.style.borderLeftColor = edge;

                Color ink = HudTokens.Convert(on ? tier.Selected : tier.Ink);
                foreach (VisualElement child in tile.Children())
                {
                    if (child is HudGlyph glyph) glyph.Tint = ink;
                    else if (child is Label label) label.style.color = ink;
                }
            }

            // --- sub-types
            //
            // Lit by what is in the player's hand, not by what the palette is pointing at. The two
            // differ before the first click of a session: the palette points at Wall from the
            // moment it is built, and nothing is armed until somebody asks for it.
            foreach (string key in _palette.SubTypes)
            {
                if (!_buildTiles.TryGetValue(key, out VisualElement tile)) continue;
                tile.EnableInClassList("bp__tile--on",
                    armedAction.Length == 0 && key == _palette.SubType && _palette.SubTypeIsArmed);
            }

            // --- materials
            bool wanted = _palette.WantsMaterial;
            foreach (var (stuff, tile) in _buildMaterialTiles)
            {
                tile.style.display = wanted ? DisplayStyle.Flex : DisplayStyle.None;
                if (!wanted) continue;
                PaintMaterialTile(stuff, tile, _palette.IsStocked(stuff), stuff == _palette.Material);
            }

            // The word MATERIAL stays whether or not there are buttons under it. It was hidden with
            // them, which took another 31 px out of the panel on a category that opens on an order
            // — the last of four places the fixed height leaked, and the one the reserved band
            // heights above could not catch, because the label is not in either band. Rail had
            // always kept its copy; this is Rows catching up, and it is the same bargain: a heading
            // over an empty space is the honest price of a control that does not move.
            foreach (Label label in _buildPanel.Query<Label>(className: "bp__mats-label").ToList())
                label.style.opacity = wanted ? 1f : 0.35f;

            // --- plants
            //
            // The same bargain as the material band: always built with the layout, shown or
            // hidden here, the heading dimmed rather than removed so the panel never changes
            // height when a tool takes an interest. Rail's headings keep their full ink, exactly
            // as its MATERIAL heading does — the dim query below only finds the Rows copy,
            // because Rail's pane sections its headings as furniture rather than flags.
            bool plantWanted = _palette.WantsPlant;
            foreach (var (plant, tile) in _buildPlantTiles)
            {
                tile.style.display = plantWanted ? DisplayStyle.Flex : DisplayStyle.None;
                tile.EnableInClassList("bp__tile--on", plantWanted && plant == _palette.Plant);
            }

            foreach (Label label in _buildPanel.Query<Label>(className: "bp__plants-label").ToList())
                label.style.opacity = plantWanted ? 1f : 0.35f;

            // --- the cost, once
            //
            // Emptied rather than hidden, and that is not fussiness: a hidden row takes its height
            // with it, and in Rail that moved the panel 25 px the moment a category opened on a
            // tool that is not made of anything. Same fault as the material band above, one tier
            // down, and found the same way. An empty label costs nothing in the other two.
            if (_buildCost != null) _buildCost.text = _palette.CostLine;
        }

        /// <summary>
        /// A material button's four states. Out of stock drops the tint entirely rather than
        /// dimming it, so that a tinted button always means buildable — which is what lets the
        /// stock state be read without a number on the button.
        /// </summary>
        static void PaintMaterialTile(int stuff, VisualElement tile, bool stocked, bool chosen)
        {
            HudTheme.MaterialTint? tint = stocked ? HudTheme.MaterialTintOf(stuff) : null;

            HudColour fill = tint?.Fill ?? HudTheme.MaterialOutFill;
            HudColour border = tint?.Border ?? HudTheme.MaterialOutBorder;
            HudColour ink = tint?.Ink ?? HudTheme.MaterialOutInk;

            tile.style.backgroundColor = HudTokens.Convert(fill);
            Color edge = HudTokens.Convert(border);
            tile.style.borderTopColor = tile.style.borderRightColor =
                tile.style.borderBottomColor = tile.style.borderLeftColor = edge;

            tile.EnableInClassList("bp__material--on", chosen && stocked);
            tile.EnableInClassList("bp__material--out", !stocked);

            foreach (VisualElement child in tile.Children())
                if (child is Label label) label.style.color = HudTokens.Convert(ink);

            tile.tooltip = stocked
                ? Registry.Label(BuildLabels.StuffKey(stuff))
                : Registry.Label(BuildLabels.StuffKey(stuff)) + " — none in store";
        }

        // ============================================================ opening and placing

        /// <summary>Whether the palette is up, for whoever owns the Escape key.</summary>
        public bool BuildPaletteOpen =>
            _buildPanel != null && _buildPanel.style.display == DisplayStyle.Flex;

        void SetBuildPalette(bool open)
        {
            // One popover at a time. Two raised from the same bar would overlap each other over
            // the buttons that raised them, and the player would have no way to tell which of the
            // two the Escape they are about to press belongs to.
            if (open) ToggleMenu(false);

            _buildPanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

            // Opening Build puts away whatever was being inspected (owner, 2026-09-17: "if the
            // tile info dialog is showing, that is closed down and the build mode is open"). The
            // specification only asked for the pane to collapse to its header, and collapsing was
            // the wrong half of the idea: the pane is docked in the same bottom-left corner the
            // palette now pins to, so a collapsed header is still a strip of panel wedged between
            // the palette and the bar, and it is describing a cell the player has stopped asking
            // about. Clearing the selection is also what lets the palette sit on the bar in every
            // case rather than lifting over a pane whose height changes with what is selected.
            if (open) _directors?.Selection.Clear();

            // No longer waits on the bar being laid out: the corner it docks into is two screen
            // edges, not the position of a button.
            if (open) PlaceBuildPalette();

            // The cap is lit by build mode, not by this panel — see MarkBuildMode.
            MarkBuildMode();
            if (open) MarkBuildState();
        }

        /// <summary>
        /// Dock the palette into the bottom-left corner: hard against the left edge of the screen
        /// and sitting on the command bar.
        ///
        /// <para><b>Both anchors are the owner's</b> (2026-09-17: <i>"it needs to pin/dock against
        /// the bottom and left for space — so up against the left screen border and also attached
        /// to the bottom bar"</i>), and both replace something that was nearly right. The left edge
        /// was <see cref="HudLayout.PopoverLeft"/> under the Build cap, which is the rule every
        /// other popover follows and which left the panel a few pixels of the bar's own padding
        /// short of the screen. The bottom used to lift over the inspect pane when something was
        /// selected; the pane is closed when the palette opens now (see
        /// <see cref="SetBuildPalette"/>), so there is nothing to lift over and the panel sits on
        /// the bar in every case.</para>
        ///
        /// <para>Only the width differs between layouts, and Bar has none — it spans.</para>
        /// </summary>
        void PlaceBuildPalette()
        {
            if (_palette == null) return;

            _buildPanel.style.bottom = HudLayout.PopoverBottom;
            _buildPanel.style.left = HudLayout.Edge;

            // Written as two statements rather than a conditional: an edge is a length and Null is
            // a keyword, and the two have no common type to pick between.
            if (_palette.Layout == BuildPaletteLayout.Bar) _buildPanel.style.right = HudLayout.Edge;
            else _buildPanel.style.right = StyleKeyword.Null;
        }
    }
}
