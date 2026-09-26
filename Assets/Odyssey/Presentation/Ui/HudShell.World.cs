#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Planet;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// <see cref="HudShell"/>: the World screen (design 59 §9, Claude Design's specification in
    /// <c>docs/reference/mockups/world-screen-spec.md</c>). The planet, the site picked on it, and the
    /// way on to the setup page.
    ///
    /// <para><b>Everything with a rule in it is engine-free and tested in the fast tier</b>: which
    /// planet a seed makes and which site is picked (<see cref="WorldChoice"/>), where a hex is and
    /// which one a pixel is over (<see cref="WorldMapGeometry"/>), the zoom and the pan
    /// (<see cref="WorldMapView"/>), the picture (<see cref="WorldMapPainter"/>) and the names
    /// (<see cref="RegionNames"/>). This file places elements and forwards input.</para>
    ///
    /// <para><b>The map is one texture, painted when the seed changes</b> (P10), drawn as three
    /// side-by-side copies so a pan across the east–west seam shows the planet going on. The
    /// hover and selection outlines are drawn over it each repaint; they are not in the texture.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        /// <summary>
        /// The main screen's director with its two presenter-supplied halves: the colonist roll
        /// (U40) and the planet (design 59), both of which need <c>Odyssey.Sim</c>. One seed field
        /// is shared, because the seed the World screen shows is the seed the menu starts from.
        /// </summary>
        static MenuDirector MakeMenu()
        {
            var seed = new SeedField();
            var world = new WorldChoice(seed, GeneratePlanet);
            return new MenuDirector(seed, new ColonistSelect(RollCandidate), world);
        }

        /// <summary>The simulation's planet for a seed, with the content's climate curve so the site panel can say its seasons.</summary>
        static PlanetView GeneratePlanet(uint seed) =>
            PlanetGenerator.Generate(seed, WorldContent.Planet, WorldContent.Biomes, WorldContent.Climate);

        static readonly WorldMapGeometry WorldMap = new WorldMapGeometry(WorldLayout.MapColumns, WorldLayout.MapRows);

        readonly WorldMapView _mapView = new WorldMapView(WorldMap);

        VisualElement _worldPage = null!;
        VisualElement _mapBox = null!;
        readonly Image[] _mapCopies = new Image[3];
        VisualElement _mapOverlay = null!;
        VisualElement _labelLayer = null!;
        Label _zoomReadout = null!;
        VisualElement _legend = null!;
        VisualElement _statsSwatch = null!;
        Label _statsName = null!;
        VisualElement _statusBox = null!;
        VisualElement _statusIconSlot = null!;
        Label _statusTitle = null!;
        Label _statusReason = null!;
        VisualElement _statsRows = null!;
        VisualElement _worldNext = null!;
        Label _worldNextLabel = null!;
        HudGlyph _worldNextChevron = null!;
        Label _setupSiteLine = null!;
        Texture2D? _mapTexture;
        byte[]? _mapBuffer;
        IVisualElementScheduledItem? _mapEase;

        // What the site panel was last built for, so a hover that moves nothing it shows costs nothing.
        PlanetView? _panelPlanet;
        int _panelTile = -1, _panelLayers;
        bool _panelNextLive;
        readonly List<(Label Label, RegionLabel Place)> _placeLabels = new List<(Label, RegionLabel)>();

        // The pointer on the map: where a press began, and whether it has moved far enough to be a drag.
        bool _mapPressed;
        bool _mapDragged;
        Vector2 _mapPressAt, _mapLastAt;
        const float DragThreshold = 4f;

        /// <summary>A small hexagon with three pips: Random site.</summary>
        const string HexDieIcon = "M12 2.8 L20 7.4 L20 16.6 L12 21.2 L4 16.6 L4 7.4 Z M9 10 L9.01 10 M15 10 L15.01 10 M12 15 L12.01 15";
        const string PlusIcon = "M12 5 L12 19 M5 12 L19 12";
        const string MinusIcon = "M5 12 L19 12";
        const string FitIcon = "M4 9 L4 4 L9 4 M15 4 L20 4 L20 9 M20 15 L20 20 L15 20 M9 20 L4 20 L4 15";

        VisualElement BuildWorldPage()
        {
            // The setup page's own frame and classes (the specification: "same as the New game
            // page"), named apart so the window rule and the tests can find it.
            var page = new VisualElement { name = "world", focusable = true };
            page.AddToClassList("panel");
            page.AddToClassList("window");
            page.AddToClassList("setup");
            page.AddToClassList("world");
            page.style.display = DisplayStyle.None;

            // ---- the title ---------------------------------------------------------------
            var title = new VisualElement();
            title.AddToClassList("world__titlerow");
            // The one label on the page larger than the scale's Name step (owner, 2026-09-26: "make
            // the label World bigger"): the page's heading, read from across the room.
            var worldTitle = HudText.Make(Registry.Label("ui.world.title"), HudTextRole.Name, ussClass: "setup__title");
            worldTitle.style.fontSize = WorldLayout.TitleSize;
            worldTitle.style.height = WorldLayout.TitleHeight;
            title.Add(worldTitle);
            var subtitle = HudText.Make(Registry.Label("ui.world.subtitle"), HudTextRole.Meta, ussClass: "world__subtitle");
            subtitle.style.color = HudTokens.TextMeta;
            title.Add(subtitle);
            page.Add(title);

            // ---- the seed, Random site and the wrap hint ------------------------------------
            var top = new VisualElement();
            top.AddToClassList("world__top");

            // The one seed box in the game now: it names a planet (design 59 §8). Named "seed" so
            // the tests that type into the box a player types into still find it.
            TextField seedBox = Field("seed", SeedEntry.MaxDigits, text => _menu.Seed.Type(text));
            seedBox.AddToClassList("world__seed");
            _worldSeedBox = seedBox;
            top.Add(Captioned(SeedField.SeedKey, seedBox));

            top.Add(WorldButton(new PathGlyph(SettingsLayout.ResetIcon, 14f, HudTokens.TextMeta),
                Registry.Label(SeedField.RerollKey), () => _menu.Seed.Reroll()));
            top.Add(WorldButton(new PathGlyph(HexDieIcon, 14f, HudTokens.TextMeta),
                Registry.Label("ui.world.random"), () => _menu.World!.SelectRandom()));

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            top.Add(spacer);
            var wrapHint = HudText.Make(Registry.Label("ui.world.wraphint"), HudTextRole.Meta, ussClass: "world__hint");
            wrapHint.style.color = HudTokens.TextDim;
            top.Add(wrapHint);
            page.Add(top);

            // ---- the body: the map and its legend, and the site panel ------------------------
            var body = new VisualElement();
            body.AddToClassList("world__body");

            var mapColumn = new VisualElement();
            mapColumn.AddToClassList("world__mapcolumn");
            mapColumn.Add(BuildMapBox());
            _legend = new VisualElement();
            _legend.AddToClassList("world__legend");
            mapColumn.Add(_legend);
            body.Add(mapColumn);

            body.Add(BuildSitePanel());
            page.Add(body);

            // ---- Back and Next ----------------------------------------------------------------
            var footer = new VisualElement();
            footer.AddToClassList("setup__footer");
            footer.AddToClassList("world__footer");

            // Classed like the setup page's Back so the window rule's exemption applies: this page's
            // way out says in words where it goes (HudGeometryTests).
            var back = WorldButton(new HudGlyph(HudGlyphKind.ChevronLeft, 14f, HudTokens.TextMeta),
                Registry.Label("ui.start.back"), () => _menu.Back());
            back.AddToClassList("setup__back");
            back.AddToClassList("world__footerbutton");
            footer.Add(back);

            // Next is the accent, never green: Start stays the only green button (the specification).
            _worldNext = new VisualElement();
            _worldNext.AddToClassList("settings__row");
            _worldNext.AddToClassList("world__button");
            _worldNext.AddToClassList("world__footerbutton");
            _worldNext.AddToClassList("world__next");
            // 14/600, the list-heading step: the specification's "Accent ink at 600", without a seventh step.
            _worldNextLabel = HudText.Make(Registry.Label("ui.world.next"), HudTextRole.ListHeading, ussClass: "settings__label");
            _worldNext.Add(_worldNextLabel);
            _worldNextChevron = new HudGlyph(HudGlyphKind.ChevronRight, 14f, HudTokens.Accent);
            _worldNext.Add(_worldNextChevron);
            _worldNext.RegisterCallback<ClickEvent>(_ => _menu.NextFromWorld());
            footer.Add(_worldNext);
            page.Add(footer);

            page.RegisterCallback<KeyDownEvent>(OnWorldKey);

            // The zoom's ease, stepped on real seconds while it runs. Paused while a colony is up
            // (ReleaseWorldMap): a display:none element's scheduler still fires.
            _mapEase = page.schedule.Execute(() =>
            {
                if (_mapView.Moving) _mapView.Tick(Time.unscaledDeltaTime);
            }).Every(16);

            _mapView.Changed += LayoutMap;
            return page;
        }

        TextField _worldSeedBox = null!;

        /// <summary>A 36-high button with a drawn icon at 14 and a label: Reroll, Random site, Back.</summary>
        static VisualElement WorldButton(VisualElement icon, string label, System.Action pressed)
        {
            var row = new VisualElement();
            row.AddToClassList("settings__row");
            row.AddToClassList("world__button");
            row.Add(icon);
            row.Add(HudText.Make(label, HudTextRole.Row, ussClass: "settings__label"));
            row.RegisterCallback<ClickEvent>(_ => pressed());
            return row;
        }

        VisualElement BuildMapBox()
        {
            _mapBox = new VisualElement { name = "worldmap" };
            _mapBox.AddToClassList("world__map");
            _mapBox.style.backgroundColor = HudTokens.Convert(HudTheme.MapBackdrop);

            for (int i = 0; i < _mapCopies.Length; i++)
            {
                var copy = new Image { scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
                copy.AddToClassList("world__mapimage");
                _mapCopies[i] = copy;
                _mapBox.Add(copy);
            }

            _mapOverlay = new VisualElement { pickingMode = PickingMode.Ignore };
            _mapOverlay.AddToClassList("world__overlay");
            _mapOverlay.generateVisualContent += PaintMapOverlay;
            _mapBox.Add(_mapOverlay);

            _labelLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _labelLayer.AddToClassList("world__overlay");
            _mapBox.Add(_labelLayer);

            _mapBox.Add(BuildZoomStack());

            var hint = HudText.Make(Registry.Label("ui.world.zoomhint"), HudTextRole.Meta, ussClass: "world__zoomhint");
            hint.style.color = HudTokens.TextMeta;
            hint.style.backgroundColor = Faded(HudTokens.PanelFill, 0.85f);
            hint.pickingMode = PickingMode.Ignore;
            _mapBox.Add(hint);

            _mapBox.RegisterCallback<GeometryChangedEvent>(evt =>
                _mapView.Resize(evt.newRect.width, evt.newRect.height));
            _mapBox.RegisterCallback<PointerDownEvent>(OnMapPointerDown);
            _mapBox.RegisterCallback<PointerMoveEvent>(OnMapPointerMove);
            _mapBox.RegisterCallback<PointerUpEvent>(OnMapPointerUp);
            _mapBox.RegisterCallback<PointerLeaveEvent>(_ => _menu.World?.Hover(-1));
            _mapBox.RegisterCallback<WheelEvent>(evt =>
            {
                Vector2 at = _mapBox.WorldToLocal(evt.mousePosition);
                if (evt.delta.y < 0f) _mapView.ZoomIn(at.x, at.y);
                else if (evt.delta.y > 0f) _mapView.ZoomOut(at.x, at.y);
                evt.StopPropagation();
            });
            return _mapBox;
        }

        VisualElement BuildZoomStack()
        {
            var stack = new VisualElement();
            stack.AddToClassList("world__zoom");
            stack.style.backgroundColor = HudTokens.PanelFill;
            stack.style.borderTopColor = stack.style.borderBottomColor =
                stack.style.borderLeftColor = stack.style.borderRightColor = HudTokens.PanelBorder;

            stack.Add(ZoomButton(PlusIcon, "ui.world.zoomin", () => _mapView.ZoomIn()));
            _zoomReadout = HudText.Make(_mapView.ZoomLabel, HudTextRole.Meta, numeric: true, ussClass: "world__zoomreadout");
            _zoomReadout.style.color = HudTokens.TextMeta;
            stack.Add(_zoomReadout);
            stack.Add(ZoomButton(MinusIcon, "ui.world.zoomout", () => _mapView.ZoomOut()));
            stack.Add(ZoomButton(FitIcon, "ui.world.fit", () => _mapView.FitToBox()));

            // A press on the stack is the stack's: it must not pick the tile under it or start a drag.
            stack.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            stack.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
            return stack;
        }

        static VisualElement ZoomButton(string icon, string tooltipKey, System.Action pressed)
        {
            var button = new VisualElement { tooltip = Registry.Label(tooltipKey) };
            button.AddToClassList("world__zoombutton");
            button.Add(new PathGlyph(icon, 14f, HudTokens.TextPrimary, stroke: 2.6f));
            button.RegisterCallback<ClickEvent>(evt => { pressed(); evt.StopPropagation(); });
            return button;
        }

        VisualElement BuildSitePanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("world__stats");
            panel.style.borderTopColor = panel.style.borderBottomColor =
                panel.style.borderLeftColor = panel.style.borderRightColor = HudTokens.PanelBorder;

            var header = new VisualElement();
            header.AddToClassList("world__statsheader");
            header.style.borderBottomColor = HudTokens.PanelBorder;
            _statsSwatch = new VisualElement();
            _statsSwatch.AddToClassList("world__statsswatch");
            header.Add(_statsSwatch);
            var heading = new VisualElement();
            heading.AddToClassList("world__statsheading");
            var caption = HudText.Make(Registry.Label("ui.world.selectedsite"), HudTextRole.PanelLabel,
                ussClass: "world__statscaption");
            caption.style.color = HudTokens.TextDim;
            heading.Add(caption);
            _statsName = HudText.Make(string.Empty, HudTextRole.Name, ussClass: "world__statsname");
            _statsName.style.color = HudTokens.TextPrimary;
            heading.Add(_statsName);
            header.Add(heading);
            panel.Add(header);

            _statusBox = new VisualElement();
            _statusBox.AddToClassList("world__status");
            var statusLine = new VisualElement();
            statusLine.AddToClassList("world__statusline");
            _statusIconSlot = new VisualElement();
            _statusIconSlot.AddToClassList("world__statusicon");
            statusLine.Add(_statusIconSlot);
            _statusTitle = HudText.Make(string.Empty, HudTextRole.Row);
            statusLine.Add(_statusTitle);
            _statusBox.Add(statusLine);
            _statusReason = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "world__statusreason");
            _statusReason.style.color = HudTokens.TextMeta;
            _statusBox.Add(_statusReason);
            panel.Add(_statusBox);

            _statsRows = new VisualElement();
            _statsRows.AddToClassList("world__statsrows");
            panel.Add(_statsRows);

            var foot = HudText.Make(Registry.Label("ui.world.foot"), HudTextRole.Meta, ussClass: "world__statsfoot");
            foot.style.color = HudTokens.TextDim;
            foot.style.borderTopColor = HudTokens.Divider;
            panel.Add(foot);
            return panel;
        }

        // ============================================================ refresh

        /// <summary>A new planet: paint its texture once, and lay out its legend and its names.</summary>
        void OnPlanetChanged()
        {
            PlanetView? planet = _menu.World?.Planet;
            if (planet == null) return;

            _mapBuffer = WorldMapPainter.Paint(planet, WorldLayout.PaintScale, out int width, out int height,
                bottomUp: true, into: _mapBuffer);
            if (_mapTexture == null || _mapTexture.width != width || _mapTexture.height != height)
            {
                if (_mapTexture != null) Object.Destroy(_mapTexture);
                // Mipmapped (design 59 §4e): at the fit the 128-wide planet's texture is drawn at
                // about a third of its size, and without mips a bilinear read of it shimmers.
                _mapTexture = new Texture2D(width, height, TextureFormat.RGBA32, true, false)
                {
                    filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                    name = "WorldMap",
                };
            }
            // Mip 0 only; Apply builds the rest. LoadRawTextureData would want every level in the buffer.
            _mapTexture.SetPixelData(_mapBuffer, 0);
            _mapTexture.Apply(true, false);
            foreach (Image copy in _mapCopies) copy.image = _mapTexture;

            BuildLegend(planet);
            BuildPlaceLabels(planet);
            _mapView.FitToBox();
            LayoutMap();
            RefreshWorldSelection();
        }

        void BuildLegend(PlanetView planet)
        {
            _legend.Clear();
            foreach (BiomeView biome in planet.Biomes)
            {
                var entry = new VisualElement();
                entry.AddToClassList("world__legendentry");
                var swatch = new VisualElement();
                swatch.AddToClassList("world__legendswatch");
                WorldMapPainter.MidColour(biome, out float r, out float g, out float b);
                swatch.style.backgroundColor = new Color(r / 255f, g / 255f, b / 255f);
                swatch.style.borderTopColor = swatch.style.borderBottomColor =
                    swatch.style.borderLeftColor = swatch.style.borderRightColor = HudTokens.PanelBorder;
                entry.Add(swatch);
                var name = HudText.Make(Registry.Label(biome.LabelKey), HudTextRole.Row);
                name.style.color = HudTokens.TextPrimary;
                entry.Add(name);
                var state = HudText.Make(Registry.Label(biome.Settleable ? "ui.world.settleable" : "ui.world.unavailable"),
                    HudTextRole.Meta, ussClass: "world__legendstate");
                state.style.color = biome.Settleable ? HudTokens.Good : HudTokens.TextDim;
                entry.Add(state);
                _legend.Add(entry);
            }

            var divider = new VisualElement();
            divider.AddToClassList("world__legenddivider");
            divider.style.backgroundColor = HudTokens.PanelBorder;
            _legend.Add(divider);

            foreach (HillBand band in new[] { HillBand.Hilly, HillBand.Mountainous, HillBand.Sheer })
            {
                var entry = new VisualElement();
                entry.AddToClassList("world__legendentry");
                WorldMapPainter.MarkStroke(band, out float stroke, out _);
                entry.Add(new PathGlyph(MarkIcon(band), WorldLayout.LegendHill, HudTokens.TextMeta, stroke: stroke * 2f));
                var name = HudText.Make(Registry.Label(WorldChoice.HillKeys[(int)band]), HudTextRole.Meta);
                name.style.color = HudTokens.TextMeta;
                entry.Add(name);
                _legend.Add(entry);
            }
        }

        /// <summary>A hill mark as a path in a 24 box: the painter's own polyline at twice its 1× size, centred.</summary>
        static string MarkIcon(HillBand band)
        {
            float[] points = WorldMapPainter.MarkPath(band);
            var d = new StringBuilder();
            for (int i = 0; i < points.Length; i += 2)
            {
                d.Append(i == 0 ? "M" : " L");
                d.Append((12f + points[i] * 2f).ToString("0.##", CultureInfo.InvariantCulture)).Append(' ');
                d.Append((12f + points[i + 1] * 2f).ToString("0.##", CultureInfo.InvariantCulture));
            }
            return d.ToString();
        }

        void BuildPlaceLabels(PlanetView planet)
        {
            _labelLayer.Clear();
            _placeLabels.Clear();
            foreach (RegionLabel place in RegionNames.For(planet))
            {
                // Land in the panel-label step, capitals already; seas upright, since neither shipped
                // face has an italic (design 59 §9a). The scale's own steps, so no seventh is added.
                Label label = HudText.Make(place.Text, place.Sea ? HudTextRole.Row : HudTextRole.PanelLabel,
                    ussClass: place.Sea ? "world__sea" : "world__land");
                label.pickingMode = PickingMode.Ignore;
                label.style.color = HudTokens.Convert(place.Sea ? HudTheme.MapSeaInk : HudTheme.MapLandInk);
                if (!place.Sea)
                {
                    label.style.unityTextOutlineColor = new Color(1f, 1f, 1f, 0.35f);
                    label.style.unityTextOutlineWidth = 1f;
                }
                label.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                _labelLayer.Add(label);
                _placeLabels.Add((label, place));
            }
        }

        /// <summary>Place the three copies of the texture and the names for the view as it is now.</summary>
        void LayoutMap()
        {
            float scale = _mapView.Scale;
            // The texture is whole pixels, ceil(Width × PaintScale), so a copy is sized from what was
            // painted rather than from the geometry, or the picture stretches up to a third of a map
            // pixel away from the outlines and the pick at the east and south edges.
            float paintedWidth = _mapTexture != null ? _mapTexture.width / WorldLayout.PaintScale : WorldMap.Width;
            float paintedHeight = _mapTexture != null ? _mapTexture.height / WorldLayout.PaintScale : WorldMap.Height;
            float width = paintedWidth * scale, height = paintedHeight * scale;
            float turn = WorldMap.WrapWidth * scale;
            for (int i = 0; i < _mapCopies.Length; i++)
            {
                Image copy = _mapCopies[i];
                copy.style.left = _mapView.OriginX + (i - 1) * turn;
                copy.style.top = _mapView.OriginY;
                copy.style.width = width;
                copy.style.height = height;
            }

            // A name stays at its pixel size whatever the zoom (the specification), and goes where
            // the copy that puts it inside the box has it.
            float boxWidth = _mapView.BoxWidth, boxHeight = _mapView.BoxHeight;
            foreach ((Label label, RegionLabel place) in _placeLabels)
            {
                float x = _mapView.ToScreenX(place.X);
                while (x < 0f) x += turn;
                while (x > boxWidth && x - turn >= 0f) x -= turn;
                float y = _mapView.ToScreenY(place.Y);
                bool inside = x >= 0f && x <= boxWidth && y >= 0f && y <= boxHeight;
                label.style.display = inside ? DisplayStyle.Flex : DisplayStyle.None;
                label.style.left = x;
                label.style.top = y;
            }

            HudText.Set(_zoomReadout, _mapView.ZoomLabel, HudTextRole.Meta);
            _mapOverlay.MarkDirtyRepaint();
        }

        /// <summary>The selection or the hover moved: the outlines, the site panel and Next.</summary>
        void RefreshWorldSelection()
        {
            WorldChoice? world = _menu.World;
            _mapOverlay.MarkDirtyRepaint();
            if (world?.Planet == null || world.Selected < 0) return;
            PlanetView planet = world.Planet;
            int tile = world.Selected;

            // The hover raises the same event as a selection, and the panel below is some twenty
            // labels built afresh: skip it while nothing it shows has moved.
            int shownLayers = MapSizes.At(_menu.Size).Y;
            bool nextLive = world.CanGoNext;
            if (ReferenceEquals(planet, _panelPlanet) && tile == _panelTile && shownLayers == _panelLayers
                && nextLive == _panelNextLive) return;
            _panelPlanet = planet;
            _panelTile = tile;
            _panelLayers = shownLayers;
            _panelNextLive = nextLive;

            WorldMapPainter.TileColour(planet, tile, out float r, out float g, out float b);
            _statsSwatch.style.backgroundColor = new Color(r / 255f, g / 255f, b / 255f);
            HudText.Set(_statsName, Registry.Label(planet.BiomeAt(tile).LabelKey), HudTextRole.Name);

            SettleVerdict verdict = planet.Verdict(tile);
            bool good = verdict == SettleVerdict.Settleable;
            Color ink = good ? HudTokens.Good : HudTokens.Warn;
            _statusBox.style.backgroundColor = Faded(ink, good ? 0.14f : 0.10f);
            _statusBox.style.borderTopColor = _statusBox.style.borderBottomColor =
                _statusBox.style.borderLeftColor = _statusBox.style.borderRightColor = Faded(ink, good ? 0.60f : 0.50f);
            _statusIconSlot.Clear();
            _statusIconSlot.Add(new HudGlyph(good ? HudGlyphKind.Check : HudGlyphKind.Info, 14f, ink));
            string key = WorldChoice.VerdictKey(verdict);
            HudText.Set(_statusTitle, Registry.Label(key), HudTextRole.Row);
            _statusTitle.style.color = ink;
            HudText.Set(_statusReason, good ? string.Empty : Registry.Describe(key), HudTextRole.Meta);
            _statusReason.style.display = good ? DisplayStyle.None : DisplayStyle.Flex;

            _statsRows.Clear();
            foreach (WorldStat stat in WorldChoice.Stats(planet, tile, shownLayers))
            {
                var row = new VisualElement();
                row.AddToClassList("world__statrow");
                row.style.borderBottomColor = HudTokens.Divider;
                var label = HudText.Make(Registry.Label(stat.LabelKey), HudTextRole.Body);
                label.style.color = HudTokens.TextMeta;
                row.Add(label);
                if (stat.Spans == null)
                {
                    var value = HudText.Make(stat.Value, HudTextRole.Row, numeric: stat.Numeric, ussClass: "world__statvalue");
                    value.style.color = HudTokens.TextPrimary;
                    row.Add(value);
                }
                else
                {
                    // Coloured runs side by side, right-aligned as one value, a word space apart:
                    // the gap is a margin, because a label's own edge spaces are not reliably kept.
                    var runs = new VisualElement();
                    runs.AddToClassList("world__statruns");
                    foreach (WorldStatSpan span in stat.Spans)
                    {
                        var run = HudText.Make(span.Text, HudTextRole.Row, numeric: stat.Numeric, ussClass: "world__statvalue");
                        run.style.color = span.Tint.HasValue ? HudTokens.Convert(span.Tint.Value) : HudTokens.TextMeta;
                        if (runs.childCount > 0) run.style.marginLeft = 5f;
                        runs.Add(run);
                    }
                    row.Add(runs);
                }
                _statsRows.Add(row);
            }

            RefreshWorldNext();
        }

        /// <summary>Next in the accent while a colony can land, faint and inert while it cannot.</summary>
        void RefreshWorldNext()
        {
            bool live = _menu.World != null && _menu.World.CanGoNext;
            Color border = live ? HudTokens.Accent : HudTokens.PanelBorder;
            _worldNext.style.borderTopColor = _worldNext.style.borderBottomColor =
                _worldNext.style.borderLeftColor = _worldNext.style.borderRightColor = border;
            _worldNext.style.backgroundColor = live ? Faded(HudTokens.Accent, 0.12f) : Color.clear;
            _worldNextLabel.style.color = live ? HudTokens.Accent : HudTokens.TextDim;
            _worldNextChevron.Tint = live ? HudTokens.Accent : HudTokens.TextDim;
            _worldNext.style.opacity = live ? 1f : 0.6f;
            _worldNext.EnableInClassList("settings__row--off", !live);
        }

        /// <summary>The setup page's read-only line naming the site it was reached from.</summary>
        void RefreshSetupSiteLine()
        {
            WorldChoice? world = _menu.World;
            string line = world?.Planet != null && world.Selected >= 0
                ? WorldChoice.SiteLine(world.Planet, world.Selected, MapSizes.At(_menu.Size).Y)
                : string.Empty;
            HudText.Set(_setupSiteLine, line, HudTextRole.Row);
        }

        /// <summary>
        /// Let go of everything the World screen holds (owner, 2026-09-26: "make sure it doesn't
        /// leak"): the texture (about 22 MB with its mips) and the paint buffer (about the same), the
        /// planet and its names, and the ease timer. Called whenever a colony goes live and when the
        /// shell is destroyed. Coming back to the World screen regenerates the planet from the seed,
        /// 12 ms, and repaints it, so nothing here needs to survive a game.
        /// </summary>
        void ReleaseWorldMap(bool immediate = false)
        {
            if (_mapTexture != null)
            {
                foreach (Image copy in _mapCopies)
                    if (copy != null) copy.image = null;
                if (immediate) Object.DestroyImmediate(_mapTexture);
                else Object.Destroy(_mapTexture);
                _mapTexture = null;
            }
            _mapBuffer = null;
            _labelLayer?.Clear();
            _placeLabels.Clear();
            _panelPlanet = null;
            _panelTile = -1;
            _menu?.World?.Release();
            _mapEase?.Pause();
        }

        void RefreshWorldPage()
        {
            _mapEase?.Resume();
            _menu.World?.Refresh();
            if (_worldSeedBox.value != _menu.Seed.Text) _worldSeedBox.SetValueWithoutNotify(_menu.Seed.Text);
            RefreshWorldSelection();
            RefreshWorldNext();
            LayoutMap();
            // Focus the page, not the seed box, so the arrows and Enter work at once and a key is not
            // swallowed by a text cursor nobody asked for (the setup page's rule for its fields).
            _worldPage.schedule.Execute(() =>
            {
                if (_menu.Showing && _menu.Screen == MenuScreen.World
                    && !(_worldPage.focusController?.focusedElement is TextField)) _worldPage.Focus();
            }).ExecuteLater(30);
        }

        // ============================================================ the outlines

        void PaintMapOverlay(MeshGenerationContext context)
        {
            WorldChoice? world = _menu.World;
            // Before the box's first GeometryChangedEvent the fit is 0, and a grow divided by the
            // scale would hand Painter2D infinities.
            if (world?.Planet == null || _mapView.Scale <= 0f) return;
            Painter2D painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;

            if (world.Hovered >= 0 && world.Hovered != world.Selected)
                Outline(painter, world.Hovered, WorldLayout.HoverGrow, WorldLayout.HoverRing,
                    Faded(HudTokens.TextPrimary, 0.8f));

            if (world.Selected >= 0)
            {
                Outline(painter, world.Selected, WorldLayout.SelectGrow, WorldLayout.SelectUnderline, HudTokens.OnAccent);
                Outline(painter, world.Selected, WorldLayout.SelectGrow, WorldLayout.SelectRing, HudTokens.Accent);
                DashHex(painter, world.Selected, WorldLayout.SelectDashGrow, WorldLayout.SelectDashWidth,
                    HudTokens.Accent);
            }
        }

        /// <summary>A hex's outline grown by a screen distance, drawn on each copy of the planet.</summary>
        void Outline(Painter2D painter, int tile, float growPixels, float width, Color colour)
        {
            float[] points = WorldMap.Outline(tile, growPixels / _mapView.Scale);
            float turn = WorldMap.WrapWidth * _mapView.Scale;
            painter.strokeColor = colour;
            painter.lineWidth = width;
            for (int copy = -1; copy <= 1; copy++)
            {
                painter.BeginPath();
                for (int i = 0; i < 12; i += 2)
                {
                    var at = new Vector2(_mapView.ToScreenX(points[i]) + copy * turn, _mapView.ToScreenY(points[i + 1]));
                    if (i == 0) painter.MoveTo(at);
                    else painter.LineTo(at);
                }
                painter.ClosePath();
                painter.Stroke();
            }
        }

        /// <summary>The selection's dashed ring: dashes of 3 and gaps of 3, in screen pixels, round the grown hex.</summary>
        void DashHex(Painter2D painter, int tile, float growPixels, float width, Color colour)
        {
            float[] points = WorldMap.Outline(tile, growPixels / _mapView.Scale);
            float turn = WorldMap.WrapWidth * _mapView.Scale;
            painter.strokeColor = colour;
            painter.lineWidth = width;
            painter.lineCap = LineCap.Butt;
            for (int copy = -1; copy <= 1; copy++)
                for (int i = 0; i < 12; i += 2)
                {
                    var from = new Vector2(_mapView.ToScreenX(points[i]) + copy * turn, _mapView.ToScreenY(points[i + 1]));
                    int j = (i + 2) % 12;
                    var to = new Vector2(_mapView.ToScreenX(points[j]) + copy * turn, _mapView.ToScreenY(points[j + 1]));
                    float length = Vector2.Distance(from, to);
                    if (length <= 0f) continue;
                    Vector2 dir = (to - from) / length;
                    for (float at = 0f; at < length; at += WorldLayout.SelectDash * 2f)
                    {
                        painter.BeginPath();
                        painter.MoveTo(from + dir * at);
                        painter.LineTo(from + dir * Mathf.Min(length, at + WorldLayout.SelectDash));
                        painter.Stroke();
                    }
                }
        }

        // ============================================================ input

        void OnMapPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            _mapPressed = true;
            _mapDragged = false;
            _mapPressAt = _mapLastAt = _mapBox.WorldToLocal(evt.position);
            _mapBox.CapturePointer(evt.pointerId);

            // A double press zooms in about the pointer (the specification).
            if (evt.clickCount == 2) _mapView.ZoomIn(_mapPressAt.x, _mapPressAt.y);
        }

        void OnMapPointerMove(PointerMoveEvent evt)
        {
            if (_mapView.Scale <= 0f) return;
            Vector2 at = _mapBox.WorldToLocal(evt.position);
            if (_mapPressed && _mapView.Zoomed)
            {
                if (!_mapDragged && Vector2.Distance(at, _mapPressAt) >= DragThreshold) _mapDragged = true;
                if (_mapDragged) _mapView.PanBy(at.x - _mapLastAt.x, at.y - _mapLastAt.y);
            }
            _mapLastAt = at;
            if (!_mapDragged) _menu.World?.Hover(_mapView.TileAt(at.x, at.y));
        }

        void OnMapPointerUp(PointerUpEvent evt)
        {
            if (!_mapPressed) return;
            _mapPressed = false;
            if (_mapBox.HasPointerCapture(evt.pointerId)) _mapBox.ReleasePointer(evt.pointerId);
            if (_mapDragged || _mapView.Scale <= 0f) return;
            Vector2 at = _mapBox.WorldToLocal(evt.position);
            int tile = _mapView.TileAt(at.x, at.y);
            if (tile >= 0) _menu.World?.Select(tile);
        }

        /// <summary>
        /// The keys (the specification): the arrows step the selection a hex at 1× and pan when zoomed,
        /// + and − zoom, 0 fits, Enter is Next and R is Random site. Escape is the start screen's own
        /// rung, which backs out a level.
        /// </summary>
        void OnWorldKey(KeyDownEvent evt)
        {
            // While the seed box has the keyboard its keys are digits, not map controls. The event's
            // target is the field's inner text element rather than the TextField itself, so the
            // type test alone let 0, − and Enter through; the hotkey director is the one owner of
            // "a field is being typed in" (TakesTheKeyboard).
            if (evt.target is TextField || Hotkeys().Typing) return;
            WorldChoice? world = _menu.World;
            if (world == null) return;
            const float step = 60f;
            bool zoomed = _mapView.Zoomed;
            int row = world.Selected >= 0 && world.Planet != null ? HexGrid.Row(world.Selected, world.Planet.Width) : 0;

            switch (evt.keyCode)
            {
                case KeyCode.RightArrow: if (zoomed) _mapView.PanBy(-step, 0f); else world.Move(0); break;
                case KeyCode.LeftArrow: if (zoomed) _mapView.PanBy(step, 0f); else world.Move(3); break;
                // Up and down zig-zag between the two neighbours above (or below) so a run of presses
                // goes straight rather than drifting sideways.
                case KeyCode.UpArrow: if (zoomed) _mapView.PanBy(0f, step); else world.Move((row & 1) == 0 ? 1 : 2); break;
                case KeyCode.DownArrow: if (zoomed) _mapView.PanBy(0f, -step); else world.Move((row & 1) == 0 ? 5 : 4); break;
                case KeyCode.Equals:
                case KeyCode.Plus:
                case KeyCode.KeypadPlus: _mapView.ZoomIn(); break;
                case KeyCode.Minus:
                case KeyCode.KeypadMinus: _mapView.ZoomOut(); break;
                case KeyCode.Alpha0:
                case KeyCode.Keypad0: _mapView.FitToBox(); break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter: _menu.NextFromWorld(); break;
                case KeyCode.R: world.SelectRandom(); break;
                default: return;
            }
            evt.StopPropagation();
        }

        static Color Faded(Color colour, float alpha) => new Color(colour.r, colour.g, colour.b, alpha);
    }
}
