#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>The chrome icons the HUD draws. Transport, disclosure, dismissal and the two
    /// alert marks — the small set a control surface needs to work at all.</summary>
    public enum HudGlyphKind
    {
        /// <summary>A square outline: the neutral stand-in for a game glyph that does not exist
        /// yet. Never a letter — see <see cref="HudGlyph"/>.</summary>
        Placeholder,

        Pause,
        Play,
        Forward,
        FastForward,

        ChevronDown,
        ChevronUp,
        ChevronLeft,
        ChevronRight,
        Close,
        Menu,

        AlertTriangle,
        Info,

        /// <summary>
        /// A passion flame, filled: one means a colonist likes the work, two that they love it
        /// (design 27 §6.4). The only filled organic shape in this set — everything else here is
        /// a chevron, a triangle or a rule — so it is drawn as a closed polygon rather than built
        /// out of the stroke helpers.
        /// </summary>
        Flame,

        /// <summary>
        /// A tick and a cross: Simple mode's "will do" and "won't do" (design 27 §6.5).
        ///
        /// <para><b>Drawn rather than typed, and that is a measurement not a preference.</b> They
        /// were U+2713 and U+2715 in a label until the two font files were read:
        /// Archivo Narrow's cmap has neither, and IBM Plex Mono has the tick and not the cross. So
        /// the legend drew two blanks and every "won't do" cell in the grid drew one, and neither
        /// tier could see it — the fast tier has no text engine and the Unity tier asserts no
        /// pixels. <see cref="HudGlyphKind.Cross"/> is deliberately its own kind rather than
        /// <see cref="Close"/> at another inset: one dismisses a panel, the other is a value in a
        /// cell, and a glyph that means two things is a glyph that gets restyled for one of
        /// them.</para>
        /// </summary>
        Check,
        Cross,

        /// <summary>
        /// A single centred bar: a tri-state box that is <b>partly</b> ticked (design brief,
        /// 2026-09-21). Its own kind rather than a reused dash, because the fonts have no
        /// character that reads as "some of these" at 16 px and a glyph that means two things is
        /// a glyph that gets restyled for one of them.
        /// </summary>
        TriState,

        /// <summary>
        /// The six item categories, as the storage pane draws them beside their names (design
        /// brief, 2026-09-21): a lidded bowl, a cross, plank courses, an open spine, a crate and a
        /// blade.
        ///
        /// <para>Drawn rather than keyed for the reason the palette's forty-two are: the ADR 0007
        /// pipeline has no sheet that covers a single one of them, and a placeholder square beside
        /// six coloured names would be six squares. The keys still exist and still name the
        /// categories; the day a sheet covers them <c>IconBadge</c> takes the slot back with
        /// nothing about the layout moving.</para>
        /// </summary>
        CategoryFood,
        CategoryMedicine,
        CategoryMaterials,
        CategoryBooks,
        CategoryItems,
        CategoryWeapons,

        /// <summary>
        /// A circular arrow: put this back the way it was. The Work tab's reset, which drops a
        /// column sort and returns the rows to the roster's own order.
        /// </summary>
        Refresh,

        /// <summary>
        /// The sky on the clock (design 43 §5): a sun, a cloud, a cloud with rain, a cloud with a
        /// bolt. Drawn rather than written, because the clock's one row has no room for a word
        /// (the owner's overflow report, 2026-09-23) and the word lives in the tooltip instead.
        /// </summary>
        WeatherClear,
        WeatherCloudy,
        WeatherRain,
        WeatherStorm,

        // ---------------------------------------------------------------- Build palette
        //
        // Forty-two more, drawn for the same reason the eleven above are and under the same
        // rule: the palette's specification asks for "1.8px-stroke line art on a 24px grid,
        // single colour inheriting the label colour" and forbids the placeholder square anywhere
        // in the panel, and there is no sheet in the ADR 0007 pipeline that covers a single one
        // of these keys. The keys are still the contract — PaletteGlyphs.For maps one to a shape
        // here, and the day a sheet does cover them IconBadge takes the slot back with nothing
        // about the layout moving.
        //
        // The materials are deliberately absent. They keep the sprites the game already draws.

        CategoryStructure,
        CategoryProduction,
        CategoryFurniture,
        CategoryPower,
        CategorySecurity,
        CategoryFloors,
        CategoryZones,
        CategoryRecreation,

        ToolWall,
        ToolDoor,
        ToolStair,
        ToolLadder,
        ToolRoof,
        ToolReclaim,
        ToolFabricator,
        ToolGalley,
        ToolReclaimer,
        ToolBench,
        ToolBunk,
        ToolTable,
        ToolLamp,
        ToolShelf,
        ToolCampfire,
        ToolConduit,
        ToolUnwire,
        ToolBattery,
        ToolGenerator,
        ToolHeater,
        ToolReactor,
        ToolTurret,
        ToolTrap,
        ToolBarricade,
        ToolDeckplate,
        ToolGrating,
        ToolTile,
        ToolGamesTable,
        ToolViewscreen,
        ToolPlanter,

        ToolFell,
        ToolMine,
        ToolDeconstruct,
        ToolCancel,
        ToolGrowZone,
        ToolStockpile,
        ToolDumping,
        PlantCarrot,

        LayoutRows,
        LayoutRail,
        LayoutBar,

        /// <summary>A brick wall standing its full height: the walls are up (design 42 §7).</summary>
        WallsUp,

        /// <summary>
        /// The same wall cut to a stump, with where the rest of it stood left as a dashed outline:
        /// the walls are down. Drawn rather than typed — neither shipped font has a wall.
        /// </summary>
        WallsDown,
    }

    /// <summary>
    /// A vector icon, drawn rather than imported.
    ///
    /// <para><b>Why these are drawn.</b> The interface spec names two icon sets — game-icons.net
    /// for things in the world and Lucide for chrome — and this project has neither. It also has
    /// a standing rule that interface art is referenced by symbolic key and arrives through the
    /// ADR 0007 pixel-art pipeline, so importing a second set of sprites now would create the
    /// second source of truth that rule exists to prevent. Chrome is the part that cannot wait:
    /// a HUD with no play button is not a HUD. Ten shapes at Lucide's own proportions — a 24-unit
    /// box, a two-unit stroke, round caps and joins — cost one <see cref="Painter2D"/> path each,
    /// no texture, no atlas and no licence.</para>
    ///
    /// <para><b>And why the eleventh is a square.</b> The old placeholder put two or three letters
    /// of the icon key in a coloured tile: MEA, WOO, SCR. The acceptance criteria strike them out
    /// — "MEA / WOO / SCR read as truncated data" — and they are right, because a player cannot
    /// tell a deliberate abbreviation from a clipped word, and a coloured tile behind a value
    /// outshouts the value. A plain outlined square in the category colour says "a picture
    /// belongs here" and says nothing else, which is the honest amount to say.</para>
    ///
    /// <para>The stroke is in element pixels rather than scaled from the 24-unit box, so a
    /// 17 px row icon and a 30 px avatar have strokes of the same weight — which is what makes a
    /// screen of them read as one set rather than as one drawing enlarged.</para>
    /// </summary>
    public partial class HudGlyph : VisualElement
    {
        /// <summary>Lucide's design box. Every path below is written in it.</summary>
        const float Box = 24f;

        /// <summary>Lucide's stroke, in its own box, converted to element pixels per glyph.</summary>
        const float LucideStroke = 2f;

        HudGlyphKind _kind;
        Color _tint = Color.white;
        readonly float _strokeScale = 1f;

        public HudGlyph(HudGlyphKind kind, float size, Color tint, float strokeScale = 1f)
        {
            _kind = kind;
            _tint = tint;
            _strokeScale = strokeScale;
            pickingMode = PickingMode.Ignore;
            style.width = size;
            style.height = size;
            style.flexShrink = 0;
            generateVisualContent += Paint;
        }

        public HudGlyphKind Kind
        {
            get => _kind;
            set
            {
                if (_kind == value) return;
                _kind = value;
                MarkDirtyRepaint();
            }
        }

        public Color Tint
        {
            get => _tint;
            set
            {
                if (_tint == value) return;
                _tint = value;
                MarkDirtyRepaint();
            }
        }

        public void Resize(float size)
        {
            style.width = size;
            style.height = size;
            MarkDirtyRepaint();
        }

        /// <summary>
        /// Whether the drawn glyph is stood down. Set by a subclass that has something better to
        /// put in the box — <see cref="IconBadge"/> sets it when the key has real art, because a
        /// placeholder square stroked over a picture is a frame nobody asked for.
        /// </summary>
        protected bool PaintSuppressed
        {
            get => _suppressed;
            set
            {
                if (_suppressed == value) return;
                _suppressed = value;
                MarkDirtyRepaint();
            }
        }

        bool _suppressed;

        void Paint(MeshGenerationContext context)
        {
            if (_suppressed) return;

            Rect box = contentRect;
            if (box.width <= 1f || box.height <= 1f) return;

            float side = Mathf.Min(box.width, box.height);
            float scale = side / Box;
            float ox = (box.width - side) * 0.5f;
            float oy = (box.height - side) * 0.5f;

            Vector2 P(float x, float y) => new Vector2(ox + x * scale, oy + y * scale);

            Painter2D painter = context.painter2D;
            painter.strokeColor = _tint;
            painter.fillColor = _tint;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            painter.lineWidth = Mathf.Max(1f, LucideStroke * scale * _strokeScale);

            switch (_kind)
            {
                case HudGlyphKind.Placeholder:
                    // Its own stroke, fixed in element pixels: the spec asks for 1.6, and a
                    // placeholder that thickens with the icon size would read as a heavier
                    // category rather than as a bigger gap.
                    painter.lineWidth = HudTokens.PlaceholderStroke;
                    Polyline(painter, false,
                        P(4, 4), P(20, 4), P(20, 20), P(4, 20));
                    painter.ClosePath();
                    painter.Stroke();
                    return;

                case HudGlyphKind.Pause:
                    FillRect(painter, P(7, 5), P(10.5f, 19));
                    FillRect(painter, P(13.5f, 5), P(17, 19));
                    return;

                case HudGlyphKind.Play:
                    FillTriangle(painter, P(7, 4.5f), P(19, 12), P(7, 19.5f));
                    return;

                case HudGlyphKind.Forward:
                    FillTriangle(painter, P(3, 5.5f), P(11.5f, 12), P(3, 18.5f));
                    FillTriangle(painter, P(12.5f, 5.5f), P(21, 12), P(12.5f, 18.5f));
                    return;

                case HudGlyphKind.FastForward:
                    FillTriangle(painter, P(1.5f, 6.5f), P(8, 12), P(1.5f, 17.5f));
                    FillTriangle(painter, P(8.75f, 6.5f), P(15.25f, 12), P(8.75f, 17.5f));
                    FillTriangle(painter, P(16, 6.5f), P(22.5f, 12), P(16, 17.5f));
                    return;

                case HudGlyphKind.ChevronDown:
                    Polyline(painter, true, P(6, 9), P(12, 15), P(18, 9));
                    return;

                case HudGlyphKind.ChevronUp:
                    Polyline(painter, true, P(6, 15), P(12, 9), P(18, 15));
                    return;

                case HudGlyphKind.ChevronLeft:
                    Polyline(painter, true, P(15, 6), P(9, 12), P(15, 18));
                    return;

                case HudGlyphKind.ChevronRight:
                    Polyline(painter, true, P(9, 6), P(15, 12), P(9, 18));
                    return;

                case HudGlyphKind.Close:
                    Polyline(painter, true, P(6, 6), P(18, 18));
                    Polyline(painter, true, P(18, 6), P(6, 18));
                    return;

                case HudGlyphKind.Menu:
                    Polyline(painter, true, P(4, 7), P(20, 7));
                    Polyline(painter, true, P(4, 12), P(20, 12));
                    Polyline(painter, true, P(4, 17), P(20, 17));
                    return;

                case HudGlyphKind.AlertTriangle:
                    Polyline(painter, false, P(12, 3.5f), P(22, 20.5f), P(2, 20.5f));
                    painter.ClosePath();
                    painter.Stroke();
                    Polyline(painter, true, P(12, 9.5f), P(12, 14f));
                    Dot(painter, P(12, 17.6f), Mathf.Max(0.9f, 1.15f * scale));
                    return;

                case HudGlyphKind.Flame:
                    // Proportions lifted from the mockup's clip-path and put on the 24 grid this
                    // file draws everything on: a narrow teardrop with the kick to the right that
                    // makes it read as a flame rather than as a leaf, at the 9 px it is drawn at.
                    FillPolygon(painter,
                        P(12f, 1f), P(17.8f, 8.2f), P(16.6f, 11.5f), P(20.2f, 15.8f),
                        P(16.6f, 23f), P(6.2f, 23f), P(3.4f, 15.8f), P(7.8f, 9.6f));
                    return;

                case HudGlyphKind.Check:
                    Polyline(painter, true, P(4.5f, 12.4f), P(9.6f, 17.5f), P(19.5f, 6.5f));
                    return;

                case HudGlyphKind.CategoryFood:
                    // A lidded bowl: a shallow cup with a domed lid and a knob.
                    Polyline(painter, false, P(4f, 13f), P(20f, 13f), P(17f, 20f), P(7f, 20f), P(4f, 13f));
                    Polyline(painter, true, P(7f, 13f), P(9f, 8f), P(15f, 8f), P(17f, 13f));
                    Polyline(painter, true, P(12f, 8f), P(12f, 5f));
                    return;

                case HudGlyphKind.CategoryMedicine:
                    // A cross, equal arms, hollow — the one shape nobody has to be taught.
                    Polyline(painter, false,
                        P(9f, 4f), P(15f, 4f), P(15f, 9f), P(20f, 9f), P(20f, 15f), P(15f, 15f),
                        P(15f, 20f), P(9f, 20f), P(9f, 15f), P(4f, 15f), P(4f, 9f), P(9f, 9f), P(9f, 4f));
                    return;

                case HudGlyphKind.CategoryMaterials:
                    // Plank courses: three staggered boards, the way a stack of sawn timber ends up.
                    Polyline(painter, false, P(4f, 6f), P(20f, 6f), P(20f, 11f), P(4f, 11f), P(4f, 6f));
                    Polyline(painter, false, P(4f, 13f), P(14f, 13f), P(14f, 18f), P(4f, 18f), P(4f, 13f));
                    Polyline(painter, false, P(16f, 13f), P(20f, 13f), P(20f, 18f), P(16f, 18f), P(16f, 13f));
                    return;

                case HudGlyphKind.CategoryBooks:
                    // An open spine seen end on: two leaves rising from a centre fold.
                    Polyline(painter, true, P(12f, 7f), P(12f, 19f));
                    Polyline(painter, false, P(12f, 7f), P(5f, 5f), P(5f, 17f), P(12f, 19f));
                    Polyline(painter, false, P(12f, 7f), P(19f, 5f), P(19f, 17f), P(12f, 19f));
                    return;

                case HudGlyphKind.CategoryItems:
                    // A crate in three-quarter view: a box with its top face and one edge showing.
                    Polyline(painter, false, P(4f, 9f), P(12f, 5f), P(20f, 9f), P(12f, 13f), P(4f, 9f));
                    Polyline(painter, true, P(4f, 9f), P(4f, 16f), P(12f, 20f), P(20f, 16f), P(20f, 9f));
                    Polyline(painter, true, P(12f, 13f), P(12f, 20f));
                    return;

                case HudGlyphKind.CategoryWeapons:
                    // A blade: a long edge with a short guard across it.
                    Polyline(painter, true, P(6f, 19f), P(18f, 6f));
                    Polyline(painter, true, P(15f, 4f), P(20f, 9f));
                    Polyline(painter, true, P(5f, 15f), P(9f, 19f));
                    return;

                case HudGlyphKind.TriState:
                    // One bar, centred, the width of the tick it sits beside. The design brief is
                    // specific and right about why it is this and not a dash character, a minus or
                    // a square-in-square: at 16 px a bar is the only mark that reads as "partly"
                    // rather than as "off" or as a second kind of tick.
                    Polyline(painter, true, P(6f, 12f), P(18f, 12f));
                    return;

                case HudGlyphKind.Cross:
                    // Close's X, inset half a pixel further so that at the 13 px a cell draws it
                    // the strokes do not touch the rounded corner of the box behind them.
                    Polyline(painter, true, P(6.5f, 6.5f), P(17.5f, 17.5f));
                    Polyline(painter, true, P(17.5f, 6.5f), P(6.5f, 17.5f));
                    return;

                case HudGlyphKind.WeatherClear:
                    Circle(painter, P(12f, 12f), 4.2f * scale);
                    for (int ray = 0; ray < 8; ray++)
                    {
                        float a = ray * Mathf.PI / 4f;
                        float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
                        Polyline(painter, true, P(12f + cx * 6.8f, 12f + cy * 6.8f), P(12f + cx * 9.6f, 12f + cy * 9.6f));
                    }
                    return;

                case HudGlyphKind.WeatherCloudy:
                    Cloud(painter, P, 0f);
                    return;

                case HudGlyphKind.WeatherRain:
                    Cloud(painter, P, -3.5f);
                    Polyline(painter, true, P(8.5f, 17f), P(7.5f, 21f));
                    Polyline(painter, true, P(12.5f, 17f), P(11.5f, 21f));
                    Polyline(painter, true, P(16.5f, 17f), P(15.5f, 21f));
                    return;

                case HudGlyphKind.WeatherStorm:
                    Cloud(painter, P, -3.5f);
                    Polyline(painter, true, P(13f, 15.5f), P(10.5f, 19f), P(13.5f, 19f), P(11f, 22.5f));
                    return;

                case HudGlyphKind.Refresh:
                    // Three quarters of a circle with an arrowhead on the open end. Drawn as a
                    // polyline of eight points rather than with an arc, because every other shape
                    // in this file is a polyline and one arc would be one more thing to tune.
                    Polyline(painter, true,
                        P(19.4f, 8.6f), P(16.6f, 5.1f), P(12.0f, 3.6f), P(7.4f, 5.1f),
                        P(4.3f, 8.9f), P(4.0f, 13.6f), P(6.4f, 17.7f), P(10.6f, 19.9f),
                        P(15.3f, 19.5f), P(18.7f, 16.6f));
                    FillTriangle(painter, P(20.6f, 3.4f), P(21.0f, 10.0f), P(14.8f, 8.0f));
                    return;

                case HudGlyphKind.WallsUp:
                    // Three courses of brick, the joints staggered, the whole wall standing.
                    Polyline(painter, false, P(3, 5), P(21, 5), P(21, 19), P(3, 19));
                    painter.ClosePath();
                    painter.Stroke();
                    Polyline(painter, true, P(3, 9.7f), P(21, 9.7f));
                    Polyline(painter, true, P(3, 14.3f), P(21, 14.3f));
                    Polyline(painter, true, P(12, 5), P(12, 9.7f));
                    Polyline(painter, true, P(7.5f, 9.7f), P(7.5f, 14.3f));
                    Polyline(painter, true, P(16.5f, 9.7f), P(16.5f, 14.3f));
                    Polyline(painter, true, P(12, 14.3f), P(12, 19));
                    return;

                case HudGlyphKind.WallsDown:
                    // One course left standing, solid, and the rest of the wall a dotted ghost of
                    // itself — what it was, and what walls-down leaves.
                    FillRect(painter, P(3, 15), P(21, 19));
                    painter.lineWidth = Mathf.Max(1f, painter.lineWidth * 0.6f);
                    for (float y = 5f; y < 13.5f; y += 3f)
                    {
                        Polyline(painter, true, P(3, y), P(3, y + 1.4f));
                        Polyline(painter, true, P(21, y), P(21, y + 1.4f));
                    }
                    for (float x = 3f; x < 20.5f; x += 3f)
                        Polyline(painter, true, P(x, 5), P(x + 1.4f, 5));
                    return;

                case HudGlyphKind.Info:
                    Circle(painter, P(12, 12), 9.2f * scale);
                    Polyline(painter, true, P(12, 11.5f), P(12, 16.5f));
                    Dot(painter, P(12, 7.9f), Mathf.Max(0.9f, 1.15f * scale));
                    return;
            }

            // Everything the chrome set does not claim is a Build palette shape, drawn in
            // HudGlyph.Palette.cs. Split by file rather than by class so that the two sets share
            // one box, one stroke rule and one set of path helpers — which is what makes a
            // category tile and a play button read as belonging to the same interface.
            PaintPalette(painter, P, scale);
        }

        /// <summary>A cloud's outline on the 24 grid, lifted by <paramref name="dy"/> to make room beneath it.</summary>
        static void Cloud(Painter2D painter, System.Func<float, float, Vector2> P, float dy)
        {
            Polyline(painter, true,
                P(5.5f, 17f + dy), P(18.5f, 17f + dy), P(21f, 15f + dy), P(20.5f, 12.2f + dy),
                P(17.6f, 10.8f + dy), P(16.4f, 7.8f + dy), P(13f, 6.2f + dy), P(9.6f, 7.4f + dy),
                P(8.2f, 10.2f + dy), P(5f, 10.8f + dy), P(3f, 13.5f + dy), P(3.6f, 16f + dy), P(5.5f, 17f + dy));
        }

        static void Polyline(Painter2D painter, bool stroke, params Vector2[] points)
        {
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (int i = 1; i < points.Length; i++) painter.LineTo(points[i]);
            if (stroke) painter.Stroke();
        }

        static void FillTriangle(Painter2D painter, Vector2 a, Vector2 b, Vector2 c)
        {
            painter.BeginPath();
            painter.MoveTo(a);
            painter.LineTo(b);
            painter.LineTo(c);
            painter.ClosePath();
            painter.Fill();
        }

        /// <summary>Any closed filled figure. <see cref="FillTriangle"/> with no fixed arity.</summary>
        static void FillPolygon(Painter2D painter, params Vector2[] points)
        {
            if (points.Length < 3) return;
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (int i = 1; i < points.Length; i++) painter.LineTo(points[i]);
            painter.ClosePath();
            painter.Fill();
        }

        static void FillRect(Painter2D painter, Vector2 min, Vector2 max)
        {
            painter.BeginPath();
            painter.MoveTo(min);
            painter.LineTo(new Vector2(max.x, min.y));
            painter.LineTo(max);
            painter.LineTo(new Vector2(min.x, max.y));
            painter.ClosePath();
            painter.Fill();
        }

        static void Circle(Painter2D painter, Vector2 centre, float radius)
        {
            painter.BeginPath();
            painter.Arc(centre, Mathf.Max(0.5f, radius), new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            painter.Stroke();
        }

        static void Dot(Painter2D painter, Vector2 centre, float radius)
        {
            painter.BeginPath();
            painter.Arc(centre, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            painter.Fill();
        }
    }

    /// <summary>
    /// The design tokens, converted once from the Unity-free <see cref="Odyssey.Hud.HudTheme"/>.
    ///
    /// <para>The tokens themselves live in the <c>Odyssey.Hud</c> assembly, which is compiled
    /// without UnityEngine so that the fast tier can read them (ADR 0003). This is the one place
    /// they become <see cref="Color"/>, and nothing in the Presentation assembly is allowed to
    /// write a colour literal of its own.</para>
    /// </summary>
    public static class HudTokens
    {
        public static readonly Color PanelFill = Convert(Odyssey.Hud.HudTheme.PanelFill);
        public static readonly Color BarFill = Convert(Odyssey.Hud.HudTheme.BarFill);
        public static readonly Color PanelBorder = Convert(Odyssey.Hud.HudTheme.PanelBorder);
        public static readonly Color Divider = Convert(Odyssey.Hud.HudTheme.Divider);

        public static readonly Color TextPrimary = Convert(Odyssey.Hud.HudTheme.TextPrimary);
        public static readonly Color TextMeta = Convert(Odyssey.Hud.HudTheme.TextMeta);
        public static readonly Color TextDim = Convert(Odyssey.Hud.HudTheme.TextDim);
        public static readonly Color TextFaint = Convert(Odyssey.Hud.HudTheme.TextFaint);

        public static readonly Color Accent = Convert(Odyssey.Hud.HudTheme.Accent);
        public static readonly Color OnAccent = Convert(Odyssey.Hud.HudTheme.OnAccent);
        public static readonly Color Warn = Convert(Odyssey.Hud.HudTheme.Warn);
        public static readonly Color Bad = Convert(Odyssey.Hud.HudTheme.Bad);
        public static readonly Color Good = Convert(Odyssey.Hud.HudTheme.Good);
        public static readonly Color Info = Convert(Odyssey.Hud.HudTheme.Info);

        public static readonly Color ScrimInk = Convert(Odyssey.Hud.HudTheme.ScrimInk);

        public static readonly Color HeaderNeutralInk = Convert(Odyssey.Hud.HudTheme.HeaderNeutralInk);

        public const float PlaceholderStroke = Odyssey.Hud.HudTheme.PlaceholderStroke;

        public static readonly Color AvatarInk = Convert(Odyssey.Hud.HudTheme.AvatarInk);

        public static readonly Color AvatarBorder = Convert(Odyssey.Hud.HudTheme.AvatarBorder);

        public const int AvatarBorderWidth = Odyssey.Hud.HudTheme.AvatarBorderWidth;

        public const float AvatarInkWidth = Odyssey.Hud.HudTheme.AvatarInkWidth;

        /// <summary>A colonist's own colour, as the appearance derives it.</summary>
        public static Color Of(Odyssey.Hud.Rgb24 colour) =>
            new Color(colour.R / 255f, colour.G / 255f, colour.B / 255f, 1f);

        /// <summary>The stroke colour for an icon in this category.</summary>
        public static Color Category(Odyssey.Hud.HudCategory category) =>
            Convert(Odyssey.Hud.HudTheme.ColourOf(category));

        /// <summary>
        /// The band a need's value falls in, as the spec colours it: at or above 60% good,
        /// 40 to 59% warn, under 40% bad. Thousandths in, because that is the scale the
        /// simulation publishes needs on.
        /// </summary>
        public static Color NeedBand(int thousandths) =>
            thousandths >= 600 ? Good : thousandths >= 400 ? Warn : Bad;

        /// <summary>
        /// One <see cref="Odyssey.Hud.HudColour"/> as a Unity colour.
        ///
        /// <para><b>Public since the Build palette</b>, which is the first part of the HUD whose
        /// colours cannot be written in the stylesheet: seven category hues and four material
        /// tints, each with four states derived from it, is forty-four declarations of colours
        /// that are already arithmetic on a token. Those are set inline from
        /// <see cref="Odyssey.Hud.HudTheme"/> instead — which is still not a literal in the
        /// Presentation assembly, and that is the rule that matters.</para>
        /// </summary>
        public static Color Convert(Odyssey.Hud.HudColour colour) =>
            new Color(colour.R / 255f, colour.G / 255f, colour.B / 255f, colour.A);

        /// <summary>
        /// A vertical gradient texture, for the two scrims. UI Toolkit's USS has no gradient
        /// property, so the ramp is a one-pixel-wide texture stretched over the element — which
        /// costs one 1x64 texture for the whole HUD and no shader.
        /// </summary>
        public static Texture2D VerticalRamp(Color from, Color to, int height = 64)
        {
            var texture = new Texture2D(1, height, TextureFormat.RGBA32, mipChain: false)
            {
                name = "HudScrimRamp",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            for (int y = 0; y < height; y++)
            {
                // Row 0 is the bottom of a Unity texture, so the ramp is written upwards and the
                // caller says which end is which by the order of its two colours.
                float t = y / (height - 1f);
                texture.SetPixel(0, y, Color.Lerp(from, to, t));
            }
            texture.Apply(updateMipmaps: false);
            return texture;
        }
    }
}
