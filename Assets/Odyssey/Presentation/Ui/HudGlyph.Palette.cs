#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The Build palette's line art: eight categories, twenty-seven placeable things, five
    /// actions, one crop and three layout switches.
    ///
    /// <para><b>Why these are drawn and not imported.</b> The palette specification is explicit —
    /// every button is "1.8px-stroke line art on a 24px grid, single colour inheriting the label
    /// colour", and "no empty-checkbox placeholders anywhere in the palette". Neither half of that
    /// can be met with what the project has: the ADR 0007 pixel pipeline covers nineteen keys and
    /// not one of them is an architecture tool, so every tile in this panel would otherwise be the
    /// outlined square. Importing a second sprite set to fill the gap would create exactly the
    /// second source of truth that the symbolic-key rule exists to prevent.</para>
    ///
    /// <para><b>They cost nothing and they are not permanent.</b> One <see cref="Painter2D"/> path
    /// each, no texture, no atlas, no licence — the same bargain <see cref="HudGlyph"/> already
    /// struck for the chrome set, in the same 24-unit box with the same stroke rule, which is what
    /// makes a category tile and a play button read as parts of one interface. The key is still
    /// the contract: <see cref="PaletteGlyphs.For"/> maps a registry key to a shape, and the day a
    /// sheet covers these keys the slot goes back to <see cref="IconBadge"/> with nothing about
    /// the layout moving.</para>
    ///
    /// <para><b>The materials are deliberately not here.</b> Wood and Stone keep the exact sprites
    /// the game already draws (specification: "do not redraw or replace the material icons"), so
    /// they stay <see cref="IconBadge"/> slots on <c>ui.res.wood</c> and <c>ui.res.stone</c>.
    /// Materials are the one tier of this panel whose art does not change.</para>
    /// </summary>
    public partial class HudGlyph
    {
        /// <summary>
        /// The palette half of the drawing switch. Reached from <c>Paint</c> for every kind the
        /// chrome set does not claim.
        /// </summary>
        /// <param name="p">Maps a point in the 24-unit design box to element space.</param>
        /// <param name="scale">Element pixels per design unit, for radii and dots.</param>
        void PaintPalette(Painter2D painter, Func<float, float, Vector2> p, float scale)
        {
            switch (_kind)
            {
                // ------------------------------------------------------ categories

                // A house frame: two walls and a pitched roof, open at the bottom.
                case HudGlyphKind.CategoryStructure:
                    Polyline(painter, true, p(4, 20), p(4, 11), p(12, 4), p(20, 11), p(20, 20));
                    return;

                // A factory roofline: a sawtooth over a box.
                case HudGlyphKind.CategoryProduction:
                    Polyline(painter, true, p(3, 20), p(3, 11), p(9, 15), p(9, 11), p(15, 15),
                        p(15, 11), p(21, 15), p(21, 20));
                    painter.ClosePath();
                    painter.Stroke();
                    return;

                // A bench: a seat, a back rail and two legs.
                case HudGlyphKind.CategoryFurniture:
                    Polyline(painter, true, p(3, 13), p(21, 13));
                    Polyline(painter, true, p(5, 9), p(19, 9));
                    Polyline(painter, true, p(6, 13), p(6, 19));
                    Polyline(painter, true, p(18, 13), p(18, 19));
                    return;

                // A bolt.
                case HudGlyphKind.CategoryPower:
                    Polyline(painter, false, p(13.5f, 3), p(6, 13.5f), p(11.5f, 13.5f),
                        p(10.5f, 21), p(18, 10.5f), p(12.5f, 10.5f));
                    painter.ClosePath();
                    painter.Stroke();
                    return;

                // A shield.
                case HudGlyphKind.CategorySecurity:
                    Polyline(painter, false, p(12, 3), p(20, 6.5f), p(20, 12),
                        p(12, 21), p(4, 12), p(4, 6.5f));
                    painter.ClosePath();
                    painter.Stroke();
                    return;

                // A tile grid: a square divided both ways.
                case HudGlyphKind.CategoryFloors:
                    Rect(painter, p(4, 4), p(20, 20));
                    Polyline(painter, true, p(12, 4), p(12, 20));
                    Polyline(painter, true, p(4, 12), p(20, 12));
                    return;

                // Four corner brackets: a marquee, which is what painting a zone is — drag a
                // rectangle of board and everything inside it becomes one thing.
                case HudGlyphKind.CategoryZones:
                    Polyline(painter, true, p(4, 8), p(4, 4), p(8, 4));
                    Polyline(painter, true, p(16, 4), p(20, 4), p(20, 8));
                    Polyline(painter, true, p(20, 16), p(20, 20), p(16, 20));
                    Polyline(painter, true, p(8, 20), p(4, 20), p(4, 16));
                    return;

                // An arch on two posts.
                case HudGlyphKind.CategoryRecreation:
                    Polyline(painter, false, p(5, 20), p(5, 12));
                    painter.Arc(p(12, 12), 7f * scale,
                        new Angle(180f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
                    painter.LineTo(p(19, 20));
                    painter.Stroke();
                    return;

                // ------------------------------------------------------ structure

                // Coursed masonry: three rows, staggered.
                case HudGlyphKind.ToolWall:
                    Rect(painter, p(3, 6), p(21, 18));
                    Polyline(painter, true, p(3, 12), p(21, 12));
                    Polyline(painter, true, p(9, 6), p(9, 12));
                    Polyline(painter, true, p(15, 12), p(15, 18));
                    return;

                // A door in its frame, with a handle.
                case HudGlyphKind.ToolDoor:
                    Rect(painter, p(6, 3), p(18, 21));
                    Dot(painter, p(14.5f, 12), Mathf.Max(0.9f, 1.15f * scale));
                    return;

                // A flight of three steps.
                case HudGlyphKind.ToolStair:
                    Polyline(painter, true, p(3, 20), p(9, 20), p(9, 15),
                        p(15, 15), p(15, 10), p(21, 10), p(21, 5));
                    return;

                // Two stiles and four rungs.
                case HudGlyphKind.ToolLadder:
                    Polyline(painter, true, p(8, 3), p(8, 21));
                    Polyline(painter, true, p(16, 3), p(16, 21));
                    Polyline(painter, true, p(8, 7), p(16, 7));
                    Polyline(painter, true, p(8, 12), p(16, 12));
                    Polyline(painter, true, p(8, 17), p(16, 17));
                    return;

                // A roof over the layer below: a pitch with a ceiling line under it.
                case HudGlyphKind.ToolRoof:
                    Polyline(painter, true, p(3, 12), p(12, 5), p(21, 12));
                    Polyline(painter, true, p(5, 17), p(19, 17));
                    return;

                // What a pillar is *for*, not what it looks like: a plate held up off the ground
                // by a column standing under the middle of it. A bare column would read as a wall
                // seen end-on, which is the one thing on this row it must not be mistaken for; the
                // span across the top is the whole of what the tool buys.
                case HudGlyphKind.ToolPillar:
                    Polyline(painter, true, p(3, 5), p(21, 5));
                    Polyline(painter, true, p(7, 8), p(17, 8));
                    Polyline(painter, true, p(10, 8), p(10, 17));
                    Polyline(painter, true, p(14, 8), p(14, 17));
                    Polyline(painter, true, p(7, 20), p(17, 20));
                    Polyline(painter, true, p(10, 17), p(7, 20));
                    Polyline(painter, true, p(14, 17), p(17, 20));
                    return;

                // A ruined shell taken on as ours: a house frame with a broken wall.
                case HudGlyphKind.ToolReclaim:
                    Polyline(painter, true, p(4, 20), p(4, 11), p(12, 4), p(20, 11), p(20, 20));
                    Polyline(painter, true, p(9, 20), p(9, 15), p(13, 15), p(13, 20));
                    return;

                // ------------------------------------------------------ production

                // A fabricator: a cabinet with a lit port.
                case HudGlyphKind.ToolFabricator:
                    Rect(painter, p(3, 6), p(21, 19));
                    Polyline(painter, true, p(3, 11), p(21, 11));
                    Rect(painter, p(7, 14), p(13, 16.5f));
                    return;

                // A galley: a pot over a flame.
                case HudGlyphKind.ToolGalley:
                    Polyline(painter, true, p(5, 8), p(7, 16), p(17, 16), p(19, 8));
                    Polyline(painter, true, p(4, 8), p(20, 8));
                    Polyline(painter, true, p(9, 19), p(15, 19));
                    return;

                // A reclaimer: a hopper sorting into two streams.
                case HudGlyphKind.ToolReclaimer:
                    Polyline(painter, true, p(3, 5), p(21, 5), p(15, 13), p(9, 13));
                    painter.ClosePath();
                    painter.Stroke();
                    Polyline(painter, true, p(10, 16), p(10, 20));
                    Polyline(painter, true, p(14, 16), p(14, 20));
                    return;

                // A bench: a worktop on two legs with a tool on it.
                case HudGlyphKind.ToolBench:
                    Polyline(painter, true, p(3, 11), p(21, 11));
                    Polyline(painter, true, p(6, 11), p(6, 20));
                    Polyline(painter, true, p(18, 11), p(18, 20));
                    Polyline(painter, true, p(9, 7), p(15, 7));
                    return;

                // ------------------------------------------------------ furniture

                // A bunk: a mattress with a pillow, on a frame.
                case HudGlyphKind.ToolBunk:
                    Polyline(painter, true, p(3, 12), p(21, 12), p(21, 18), p(3, 18));
                    painter.ClosePath();
                    painter.Stroke();
                    Rect(painter, p(5, 8), p(11, 12));
                    Polyline(painter, true, p(3, 18), p(3, 21));
                    Polyline(painter, true, p(21, 18), p(21, 21));
                    return;

                // A table: a top and two legs.
                case HudGlyphKind.ToolTable:
                    Polyline(painter, true, p(3, 9), p(21, 9));
                    Polyline(painter, true, p(7, 9), p(7, 20));
                    Polyline(painter, true, p(17, 9), p(17, 20));
                    return;

                // A lamp: a shade on a stem, over a base.
                case HudGlyphKind.ToolLamp:
                    Polyline(painter, true, p(6, 12), p(10, 4), p(14, 4), p(18, 12));
                    painter.ClosePath();
                    painter.Stroke();
                    Polyline(painter, true, p(12, 12), p(12, 19));
                    Polyline(painter, true, p(8, 20), p(16, 20));
                    return;

                // A shelf: an upright with three boards.
                case HudGlyphKind.ToolShelf:
                    Rect(painter, p(4, 4), p(20, 20));
                    Polyline(painter, true, p(4, 9.5f), p(20, 9.5f));
                    Polyline(painter, true, p(4, 15), p(20, 15));
                    return;

                // ------------------------------------------------------ power

                // A conduit: a run with a junction box on it.
                case HudGlyphKind.ToolConduit:
                    Polyline(painter, true, p(3, 12), p(9, 12));
                    Polyline(painter, true, p(15, 12), p(21, 12));
                    Rect(painter, p(9, 8), p(15, 16));
                    return;

                // A battery: a cell with a terminal and a charge bar.
                case HudGlyphKind.ToolBattery:
                    Rect(painter, p(3, 7), p(19, 17));
                    Polyline(painter, true, p(21, 10), p(21, 14));
                    Polyline(painter, true, p(7, 10), p(7, 14));
                    Polyline(painter, true, p(11, 10), p(11, 14));
                    return;

                // A generator: a housing with a bolt in it.
                case HudGlyphKind.ToolGenerator:
                    Rect(painter, p(3, 6), p(21, 19));
                    Polyline(painter, false, p(13, 8.5f), p(9.5f, 13), p(12.5f, 13),
                        p(11, 17), p(15, 12), p(12, 12));
                    painter.ClosePath();
                    painter.Stroke();
                    return;

                // A reactor: a core with a ring around it.
                case HudGlyphKind.ToolReactor:
                    Circle(painter, p(12, 12), 8.5f * scale);
                    Dot(painter, p(12, 12), Mathf.Max(1.2f, 2.2f * scale));
                    Polyline(painter, true, p(12, 3.5f), p(12, 6.5f));
                    Polyline(painter, true, p(12, 17.5f), p(12, 20.5f));
                    return;

                // ------------------------------------------------------ security

                // A turret: a barrel on a mount.
                case HudGlyphKind.ToolTurret:
                    Polyline(painter, true, p(4, 20), p(20, 20));
                    Polyline(painter, true, p(8, 20), p(8, 15), p(16, 15), p(16, 20));
                    Polyline(painter, true, p(12, 15), p(12, 10));
                    Polyline(painter, true, p(12, 10), p(20, 5));
                    return;

                // A trap: jaws under a trigger plate.
                case HudGlyphKind.ToolTrap:
                    Polyline(painter, true, p(3, 17), p(21, 17));
                    Polyline(painter, true, p(6, 17), p(9, 10), p(12, 17), p(15, 10), p(18, 17));
                    return;

                // A barricade: crossed timbers behind a rail.
                case HudGlyphKind.ToolBarricade:
                    Polyline(painter, true, p(3, 9), p(21, 9));
                    Polyline(painter, true, p(3, 16), p(21, 16));
                    Polyline(painter, true, p(7, 5), p(7, 20));
                    Polyline(painter, true, p(17, 5), p(17, 20));
                    return;

                // ------------------------------------------------------ floors

                // Deck plate: a panel with two fixing lines.
                case HudGlyphKind.ToolDeckplate:
                    Rect(painter, p(3, 7), p(21, 17));
                    Polyline(painter, true, p(7, 7), p(7, 17));
                    Polyline(painter, true, p(17, 7), p(17, 17));
                    return;

                // Grating: a frame you can see through.
                case HudGlyphKind.ToolGrating:
                    Rect(painter, p(4, 4), p(20, 20));
                    Polyline(painter, true, p(9.3f, 4), p(9.3f, 20));
                    Polyline(painter, true, p(14.6f, 4), p(14.6f, 20));
                    Polyline(painter, true, p(4, 12), p(20, 12));
                    return;

                // Tile: a square laid on the diagonal, quartered.
                case HudGlyphKind.ToolTile:
                    Polyline(painter, false, p(12, 3), p(21, 12), p(12, 21), p(3, 12));
                    painter.ClosePath();
                    painter.Stroke();
                    Polyline(painter, true, p(7.5f, 7.5f), p(16.5f, 16.5f));
                    return;

                // ------------------------------------------------------ recreation

                // A games table: a top with two counters on it.
                case HudGlyphKind.ToolGamesTable:
                    Rect(painter, p(3, 7), p(21, 17));
                    Dot(painter, p(9, 12), Mathf.Max(1f, 1.8f * scale));
                    Circle(painter, p(15, 12), 1.8f * scale);
                    return;

                // A viewscreen: a panel on a stand.
                case HudGlyphKind.ToolViewscreen:
                    Rect(painter, p(3, 5), p(21, 16));
                    Polyline(painter, true, p(12, 16), p(12, 19));
                    Polyline(painter, true, p(8, 19), p(16, 19));
                    return;

                // A planter: a shoot in a trough.
                case HudGlyphKind.ToolPlanter:
                    Polyline(painter, true, p(5, 14), p(7, 20), p(17, 20), p(19, 14));
                    painter.ClosePath();
                    painter.Stroke();
                    Polyline(painter, true, p(12, 14), p(12, 7));
                    Polyline(painter, true, p(12, 10), p(8, 6));
                    Polyline(painter, true, p(12, 11), p(16, 7));
                    return;

                // ------------------------------------------------------ the four actions

                // Chop: an axe head on a haft, and the cut.
                case HudGlyphKind.ToolFell:
                    Polyline(painter, true, p(5, 20), p(15, 7));
                    Polyline(painter, false, p(13, 4), p(20, 9), p(15.5f, 12.5f), p(11.5f, 7));
                    painter.ClosePath();
                    painter.Stroke();
                    return;

                // Mine: a pick, two arms over a haft.
                case HudGlyphKind.ToolMine:
                    Polyline(painter, true, p(6, 20), p(14, 9));
                    painter.BeginPath();
                    painter.MoveTo(p(8, 4));
                    painter.BezierCurveTo(p(14, 4.5f), p(18, 8), p(20, 13));
                    painter.Stroke();
                    Polyline(painter, true, p(10.5f, 6), p(17, 11.5f));
                    return;

                // Deconstruct: a pick over rubble.
                case HudGlyphKind.ToolDeconstruct:
                    Polyline(painter, true, p(6, 15), p(15, 5));
                    Polyline(painter, true, p(11, 3), p(18, 9));
                    Polyline(painter, true, p(3, 20), p(21, 20));
                    Polyline(painter, true, p(8, 20), p(10.5f, 16.5f), p(13, 20));
                    return;

                // Cancel: a circle with a slash, which is the one shape nobody has to be taught.
                case HudGlyphKind.ToolCancel:
                    Circle(painter, p(12, 12), 8.6f * scale);
                    Polyline(painter, true, p(6, 18), p(18, 6));
                    return;

                // Grow: a seedling over the soil line it is planted in. The marquee above says
                // "a zone"; the sprout says what the zone is for.
                case HudGlyphKind.ToolGrowZone:
                    Polyline(painter, true, p(5, 19), p(19, 19));
                    Polyline(painter, true, p(12, 19), p(12, 7));
                    Polyline(painter, true, p(12, 12), p(7.5f, 8));
                    Polyline(painter, true, p(12, 12), p(16.5f, 8));
                    return;

                // Stockpile: a crate with its lid seam. The tools in this category answer "where
                // things go" — the crate is the where, and the seam is what says box rather
                // than slab.
                case HudGlyphKind.ToolStockpile:
                    Rect(painter, p(4, 7), p(20, 19));
                    Polyline(painter, true, p(4, 11), p(20, 11));
                    return;

                // Dumping: a down arrow onto the same soil line the zone tool plants over —
                // the one says grow here, this says throw here.
                case HudGlyphKind.ToolDumping:
                    Polyline(painter, true, p(12, 4), p(12, 13));
                    Polyline(painter, true, p(8, 9.5f), p(12, 13.5f), p(16, 9.5f));
                    Polyline(painter, true, p(5, 19), p(19, 19));
                    return;

                // A carrot: a tapered root with its top left on. The plant tier's chip shows the
                // crop itself, the same way the material tier shows the stuff — and it is drawn
                // rather than an IconBadge for the reason the rest of this panel is: no sheet in
                // the ADR 0007 pipeline covers it, and the panel forbids the placeholder square.
                case HudGlyphKind.PlantCarrot:
                    Polyline(painter, true, p(12, 7), p(12, 4));
                    Polyline(painter, true, p(12, 5.5f), p(8.5f, 3));
                    Polyline(painter, true, p(12, 5.5f), p(15.5f, 3));
                    Polyline(painter, false, p(9, 7), p(12, 21), p(15, 7));
                    painter.ClosePath();
                    painter.Stroke();
                    return;

                // ------------------------------------------------------ the layout switcher

                // Rows: three full-width bands.
                case HudGlyphKind.LayoutRows:
                    Rect(painter, p(3, 5), p(21, 9));
                    Rect(painter, p(3, 11), p(21, 15));
                    Rect(painter, p(3, 17), p(21, 21));
                    return;

                // Rail: a tall column on the left, a pane beside it.
                case HudGlyphKind.LayoutRail:
                    Rect(painter, p(3, 5), p(9, 21));
                    Rect(painter, p(11, 5), p(21, 21));
                    return;

                // Bar: two bands along the bottom, and the board above them.
                case HudGlyphKind.LayoutBar:
                    Rect(painter, p(3, 12), p(21, 16));
                    Rect(painter, p(3, 18), p(21, 22));
                    return;
            }
        }

        /// <summary>A stroked rectangle. Five of these shapes want one and the chrome set had
        /// only the filled form.</summary>
        static void Rect(Painter2D painter, Vector2 min, Vector2 max)
        {
            painter.BeginPath();
            painter.MoveTo(min);
            painter.LineTo(new Vector2(max.x, min.y));
            painter.LineTo(max);
            painter.LineTo(new Vector2(min.x, max.y));
            painter.ClosePath();
            painter.Stroke();
        }
    }

    /// <summary>
    /// Which shape a Build palette key is drawn as.
    ///
    /// <para><b>One table, and it is checked.</b> <c>PaletteGlyphTests</c> walks every key the
    /// palette can put on screen — <c>PaletteTools.IconKeys</c> — and fails if any of them lands
    /// here without a shape. That is the test that would have caught the palette drawing a row of
    /// identical squares, which is the failure this whole file exists to prevent and which no
    /// amount of looking at one screenshot would reveal.</para>
    /// </summary>
    public static class PaletteGlyphs
    {
        static readonly Dictionary<string, HudGlyphKind> Shapes = new Dictionary<string, HudGlyphKind>
        {
            { "ui.arch.category.structure", HudGlyphKind.CategoryStructure },
            { "ui.arch.category.production", HudGlyphKind.CategoryProduction },
            { "ui.arch.category.furniture", HudGlyphKind.CategoryFurniture },
            { "ui.arch.category.power", HudGlyphKind.CategoryPower },
            { "ui.arch.category.security", HudGlyphKind.CategorySecurity },
            { "ui.arch.category.floors", HudGlyphKind.CategoryFloors },
            { "ui.arch.category.zones", HudGlyphKind.CategoryZones },
            { "ui.arch.category.recreation", HudGlyphKind.CategoryRecreation },

            { "ui.arch.tool.wall", HudGlyphKind.ToolWall },
            { "ui.arch.tool.door", HudGlyphKind.ToolDoor },
            { "ui.arch.tool.stair", HudGlyphKind.ToolStair },
            { "ui.arch.tool.pillar", HudGlyphKind.ToolPillar },
            { "ui.arch.tool.ladder", HudGlyphKind.ToolLadder },
            { "ui.arch.tool.roof", HudGlyphKind.ToolRoof },
            { "ui.arch.tool.reclaim", HudGlyphKind.ToolReclaim },

            { "ui.arch.tool.fabricator", HudGlyphKind.ToolFabricator },
            { "ui.arch.tool.galley", HudGlyphKind.ToolGalley },
            { "ui.arch.tool.reclaimer", HudGlyphKind.ToolReclaimer },
            { "ui.arch.tool.bench", HudGlyphKind.ToolBench },

            // The bed borrows the bunk's shape until its own is drawn: a bunk is a bed, the
            // shape reads as one at 17 px, and a wrong-shaped bed is a lesser wrong than the
            // placeholder square the specification forbids. Its own path joins here the day
            // one is drawn (design 20 §12).
            { "ui.arch.tool.bed", HudGlyphKind.ToolBunk },
            { "ui.arch.tool.bunk", HudGlyphKind.ToolBunk },
            { "ui.arch.tool.table", HudGlyphKind.ToolTable },
            { "ui.arch.tool.lamp", HudGlyphKind.ToolLamp },
            { "ui.arch.tool.shelf", HudGlyphKind.ToolShelf },

            { "ui.arch.tool.conduit", HudGlyphKind.ToolConduit },
            { "ui.arch.tool.battery", HudGlyphKind.ToolBattery },
            { "ui.arch.tool.generator", HudGlyphKind.ToolGenerator },
            { "ui.arch.tool.reactor", HudGlyphKind.ToolReactor },

            { "ui.arch.tool.turret", HudGlyphKind.ToolTurret },
            { "ui.arch.tool.trap", HudGlyphKind.ToolTrap },
            { "ui.arch.tool.barricade", HudGlyphKind.ToolBarricade },

            { "ui.arch.tool.deckplate", HudGlyphKind.ToolDeckplate },
            { "ui.arch.tool.grating", HudGlyphKind.ToolGrating },
            { "ui.arch.tool.tile", HudGlyphKind.ToolTile },

            { "ui.arch.tool.gamestable", HudGlyphKind.ToolGamesTable },
            { "ui.arch.tool.viewscreen", HudGlyphKind.ToolViewscreen },
            { "ui.arch.tool.planter", HudGlyphKind.ToolPlanter },

            { PaletteTools.Fell, HudGlyphKind.ToolFell },
            { PaletteTools.Mine, HudGlyphKind.ToolMine },
            { PaletteTools.Deconstruct, HudGlyphKind.ToolDeconstruct },
            { PaletteTools.Cancel, HudGlyphKind.ToolCancel },
            { PaletteTools.GrowZone, HudGlyphKind.ToolGrowZone },
            { "ui.arch.tool.stockpile", HudGlyphKind.ToolStockpile },
            { "ui.arch.tool.dumping", HudGlyphKind.ToolDumping },
            { "ui.terrain.carrot", HudGlyphKind.PlantCarrot },
        };

        /// <summary>The shape for a key, or <see cref="HudGlyphKind.Placeholder"/> where none is
        /// drawn — which the palette's own test forbids, and which every other part of the HUD is
        /// still entitled to.</summary>
        public static HudGlyphKind For(string key) =>
            Shapes.TryGetValue(key, out HudGlyphKind kind) ? kind : HudGlyphKind.Placeholder;

        /// <summary>Whether a key has a shape of its own.</summary>
        public static bool Has(string key) => Shapes.ContainsKey(key);

        /// <summary>The switcher's three shapes, in switcher order.</summary>
        public static HudGlyphKind For(BuildPaletteLayout layout) => layout switch
        {
            BuildPaletteLayout.Rows => HudGlyphKind.LayoutRows,
            BuildPaletteLayout.Rail => HudGlyphKind.LayoutRail,
            BuildPaletteLayout.Bar => HudGlyphKind.LayoutBar,
            _ => HudGlyphKind.LayoutRows,
        };
    }
}
