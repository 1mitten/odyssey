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
    /// earlier: "directly above the build button … no spacing and padding to ensure tight space".
    /// Rows and Bar take the full width; Rail keeps its 840 and stays anchored to the button that
    /// raised it.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        BuildPaletteModel? _palette;

        VisualElement _buildBody = null!;
        VisualElement _buildSwitch = null!;
        Label _buildCrumb = null!;
        VisualElement _buildModeIcon = null!;
        VisualElement _buildActions = null!;
        Label _buildHint = null!;

        /// <summary>Every tile currently on screen that has a lit state, by the thing it stands
        /// for. Rebuilt with the layout and walked on every repaint, so a repaint never has to
        /// know which layout it is repainting.</summary>
        readonly Dictionary<string, VisualElement> _buildTiles = new Dictionary<string, VisualElement>();

        readonly List<(int stuff, VisualElement tile)> _buildMaterialTiles =
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
            // breadcrumb, a switcher and four action buttons — so the stock one is taken back out
            // and replaced. Going through Popover first is still worth it: the fill, the docking
            // and the Escape contract are the parts that must not differ between panels.
            if (_buildPanel.childCount > 0) _buildPanel.RemoveAt(0);
            _buildPanel.Insert(0, BuildPaletteHeader());

            _buildBody = new VisualElement();
            _buildBody.AddToClassList("bp__body");
            _buildPanel.Add(_buildBody);

            // The hint sits outside the panel — it is an instruction about the world rather than a
            // part of the control surface — but it is a *child* of the panel, absolutely
            // positioned just above its top edge.
            //
            // That is the second attempt. The first put it in the shell and worked out where the
            // top of the panel was, which is a measurement that does not exist yet when the panel
            // is first placed: it fell back to a fixed offset and drew the sentence through the
            // material buttons. Hanging it off the panel's own top edge needs no measurement and
            // cannot be out of date, and UI Toolkit draws a child outside its parent's box quite
            // happily as long as nothing clips it.
            _buildHint = HudText.Make(
                "Left click places · drag for a run · right click cancels",
                HudTextRole.Meta, ussClass: "bp__hint");
            _buildHint.pickingMode = PickingMode.Ignore;
            _buildPanel.Add(_buildHint);

            _hud.Add(_buildPanel);
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

            // The world can disarm a tool without the palette being touched — Escape, right-click,
            // a hotkey — and a panel that did not hear about it would sit there lit.
            _directors.Designate.ToolChanged += _ => MarkBuildState();

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

        VisualElement BuildPaletteHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("bp__hdr");

            header.Add(HudText.Make("BUILD", HudTextRole.PanelLabel, ussClass: "panel__label"));

            // The mode icon sits between the panel label and the line, and is empty except while
            // one of the four actions is held — at which point it and the line beside it are that
            // action's own colour, which is what says which mode the player is in.
            _buildModeIcon = new VisualElement { pickingMode = PickingMode.Ignore };
            _buildModeIcon.AddToClassList("bp__mode-icon");
            _buildModeIcon.style.display = DisplayStyle.None;
            header.Add(_buildModeIcon);

            _buildCrumb = HudText.Make(string.Empty, HudTextRole.Body, ussClass: "bp__crumb");
            header.Add(_buildCrumb);

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            header.Add(spacer);

            // The cost, in the header, for Bar only — the other two have room for it in the body,
            // and two dense rows do not. Its own class as well as the shared one, so that the
            // layout builders can find this copy and not the body's.
            _buildHeaderCost = HudText.Make(string.Empty, HudTextRole.Body, numeric: true, "bp__cost");
            _buildHeaderCost.AddToClassList("bp__cost--hdr");
            header.Add(_buildHeaderCost);

            _buildSwitch = BuildLayoutSwitcher();
            header.Add(_buildSwitch);

            header.Add(HudText.Make("ESC", HudTextRole.Hotkey, ussClass: "bp__esc"));

            _buildActions = new VisualElement();
            _buildActions.AddToClassList("bp__actions");
            foreach (string key in PaletteTools.Pinned) _buildActions.Add(BuildActionButton(key));
            header.Add(_buildActions);

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
            header.Add(close);

            return header;
        }

        /// <summary>
        /// One of the four actions, as a 26 px square in its own colour.
        ///
        /// <para>Icon and tooltip rather than a labelled button in the body (specification): these
        /// are escape hatches, not primary actions, and as full-width rows they were competing
        /// with the build tiers for the eye and pushing the panel over its width.</para>
        /// </summary>
        VisualElement BuildActionButton(string key)
        {
            var button = new VisualElement { name = "action-" + key };
            button.AddToClassList("bp__action");

            HudColour hue = HudTheme.PinnedActionHue(key) ?? HudTheme.TextMeta;
            button.style.borderTopColor = button.style.borderRightColor =
                button.style.borderBottomColor = button.style.borderLeftColor =
                    HudTokens.Convert(hue.WithAlpha(0.45f));
            button.style.backgroundColor = HudTokens.Convert(hue.WithAlpha(0.12f));
            button.Add(new HudGlyph(PaletteGlyphs.For(key), 15f, HudTokens.Convert(hue)));

            string name = Registry.Label(key);
            button.tooltip = name + HotkeyLegend(key) + " — drag a box over the world";
            button.RegisterCallback<ClickEvent>(_ =>
            {
                _palette?.TogglePinned(key);
                MarkBuildState();
            });

            _buildTiles[key] = button;
            return button;
        }

        /// <summary>The key that arms a tool, if it has one, as " (M)". Read from the binding map
        /// rather than written out, so a rebind reaches the tooltip.</summary>
        string HotkeyLegend(string key)
        {
            if (_directors == null) return string.Empty;

            HotkeyAction? action = key == PaletteTools.Mine ? HotkeyAction.ToolMine
                : key == PaletteTools.Fell ? HotkeyAction.ToolFell
                : key == PaletteTools.Cancel ? HotkeyAction.ToolCancel
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

            // The action buttons live in the header and survive the rebuild, so they go back into
            // the lit-state index by hand.
            foreach (VisualElement action in _buildActions.Children())
                if (action.name.StartsWith("action-", StringComparison.Ordinal))
                    _buildTiles[action.name.Substring("action-".Length)] = action;

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

            var mats = Band("bp__mats");
            mats.Add(HudText.Make("MATERIAL", HudTextRole.PanelLabel, ussClass: "bp__mats-label"));
            foreach (int stuff in PaletteTools.Materials) mats.Add(MaterialTile(stuff, "bp__mat"));

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            mats.Add(spacer);

            _buildCost = HudText.Make(string.Empty, HudTextRole.Body, numeric: true, "bp__cost");
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
            var (key, label, _) = PaletteTools.Categories[index];

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

            string key = BuildLabels.StuffKey(stuff);
            var icon = new IconBadge(key, shape == "bp__mat-tile" ? 26f : 19f);
            tile.Add(icon);

            // The specification asks for 17/700 here, and 18/700 in Rail's larger tile. The type
            // scale is closed at six steps and both snap to the same one (HudType's own header
            // sets that precedent), so the two sizes are one role — which is the right answer
            // anyway: a material is the same word in both layouts.
            tile.Add(HudText.Make(Registry.Label(key), HudTextRole.Name, ussClass: "bp__material-label"));

            tile.RegisterCallback<ClickEvent>(_ => _palette?.SelectMaterial(stuff));

            _buildMaterialTiles.Add((stuff, tile));
            return tile;
        }

        // ============================================================ state

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

            // --- the four actions
            foreach (string key in PaletteTools.Pinned)
            {
                if (!_buildTiles.TryGetValue(key, out VisualElement button)) continue;
                bool on = key == armedAction;
                HudColour hue = HudTheme.PinnedActionHue(key) ?? HudTheme.TextMeta;
                button.style.backgroundColor = HudTokens.Convert(hue.WithAlpha(on ? 0.30f : 0.12f));
                button.style.borderTopColor = button.style.borderRightColor =
                    button.style.borderBottomColor = button.style.borderLeftColor =
                        HudTokens.Convert(hue.WithAlpha(on ? 1f : 0.45f));
            }

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
            foreach (string key in _palette.SubTypes)
            {
                if (!_buildTiles.TryGetValue(key, out VisualElement tile)) continue;
                tile.EnableInClassList("bp__tile--on",
                    armedAction.Length == 0 && key == _palette.SubType &&
                    BuildPaletteModel.IsBuildable(key));
            }

            // --- materials
            bool wanted = _palette.WantsMaterial;
            foreach (var (stuff, tile) in _buildMaterialTiles)
            {
                tile.style.display = wanted ? DisplayStyle.Flex : DisplayStyle.None;
                if (!wanted) continue;
                PaintMaterialTile(stuff, tile, _palette.IsStocked(stuff), stuff == _palette.Material);
            }

            foreach (Label label in _buildPanel.Query<Label>(className: "bp__mats-label").ToList())
                label.style.display = wanted ? DisplayStyle.Flex : DisplayStyle.None;

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

            // While the palette is open the inspect pane collapses to its header (specification).
            // The palette docks on top of whatever is under it, so this is not only tidiness: it
            // is what stops a selected colonist's needs pushing the palette up over the board.
            _inspectPanel?.EnableInClassList("inspect--collapsed", open);

            if (_barItems.Count > 0)
            {
                _barItems[0].EnableInClassList("cmd--on", open);
                if (open) PlaceBuildPalette();
            }

            if (open) MarkBuildState();
        }

        /// <summary>
        /// Dock the palette flush on whatever is under it.
        ///
        /// <para>Rows and Bar span the screen, so they only need a bottom. Rail keeps its width
        /// and is anchored to the button that raised it, like every other popover.</para>
        /// </summary>
        void PlaceBuildPalette()
        {
            if (_palette == null) return;

            float bottom = HudLayout.PopoverBottom;

            // Flush on the inspect pane's collapsed header when there is one, rather than on the
            // bar behind it — "tight and flush to other elements" (owner, 2026-09-17).
            if (_inspectPanel != null && _inspectPanel.style.display == DisplayStyle.Flex)
            {
                float top = _inspectPanel.worldBound.height;
                if (!float.IsNaN(top) && top > 1f) bottom += top;
            }

            _buildPanel.style.bottom = bottom;

            if (_palette.Layout == BuildPaletteLayout.Rail)
            {
                _buildPanel.style.left = StyleKeyword.Null;
                _buildPanel.style.right = StyleKeyword.Null;
                if (_barItems.Count > 0)
                {
                    float screen = _hud.resolvedStyle.width;
                    float width = HudLayout.BuildRailWidth;
                    _buildPanel.style.left =
                        HudLayout.PopoverLeft(_barItems[0].worldBound.xMin, width, screen);
                }
                return;
            }

            _buildPanel.style.left = HudLayout.Edge;
            _buildPanel.style.right = HudLayout.Edge;
        }
    }
}
