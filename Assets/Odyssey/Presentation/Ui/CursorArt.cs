#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using UnityEngine;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The pointer's art, drawn in code rather than imported.
    ///
    /// <para><b>32 px is a hardware limit, not a taste.</b> Above 32 × 32 Windows cannot carry the
    /// cursor itself, so <c>CursorMode.Auto</c> falls back to a <i>software</i> cursor that Unity
    /// composites into the frame — one frame behind the real pointer at best, and frozen entirely
    /// while the game hitches. Shipping a 64 px cursor to answer an accuracy complaint would have
    /// made the complaint worse, on a machine other than the one it was reported from.
    /// <see cref="Size"/> is asserted rather than trusted.</para>
    ///
    /// <para><b>Why it is not art under <c>Assets/Art/Ui/</c>.</b> ADR 0007 says interface art is
    /// 64 px, point-filtered, uncompressed and lives there. A cursor breaks the first of those by
    /// necessity, and carving an exception into an absolute rule is how the rule dies — the note
    /// <see cref="IconArt"/> carries about its own folder. So the cursor is not imported art at
    /// all: it is drawn, as <see cref="HudGlyph"/> draws every other shape the project wanted and
    /// no sheet had. It also means the tool crosshair is a <i>function</i> of
    /// <see cref="OrderColours.Hue"/>, so a new order tool inherits a cursor in its own colour
    /// with no art work at all.</para>
    ///
    /// <para><b>The hotspot is authored data, not a convention.</b> A wrong hotspot is precisely an
    /// inaccurate pointer, it is invisible in a screenshot, and it is the first thing anybody would
    /// blame. <c>CursorArtTests</c> asserts each one lands inside the art and on a pixel that is
    /// actually drawn.</para>
    ///
    /// <para>See <c>docs/design/28-pointer-cursor.md</c>.</para>
    /// </summary>
    public static class CursorArt
    {
        /// <summary>The edge of every cursor texture, in pixels.</summary>
        public const int Size = 32;

        /// <summary>
        /// The largest cursor Windows will carry in hardware. Equal to <see cref="Size"/> today,
        /// and named separately because the two are different facts: one is what we draw, the
        /// other is what the platform can hold, and a future 16 px cursor must still fail the day
        /// somebody raises it to 48.
        /// </summary>
        public const int HardwareCeiling = 32;

        /// <summary>
        /// The arrow, in mask rows top-down. <c>X</c> is the dark edge, <c>.</c> the pale fill,
        /// a space is nothing at all. The tip is the top-left pixel, which is where
        /// <see cref="ArrowHotspot"/> points.
        /// </summary>
        static readonly string[] ArrowMask =
        {
            "X",
            "XX",
            "X.X",
            "X..X",
            "X...X",
            "X....X",
            "X.....X",
            "X......X",
            "X.......X",
            "X........X",
            "X.........X",
            "X..........X",
            "X...........X",
            "X............X",
            "X.......XXXXXX",
            "X....X..X",
            "X...XX..X",
            "X..X XX..X",
            "X.X   X..X",
            "XX    X..X",
            "X      X..X",
            "       X..X",
            "        X.X",
            "        XX",
        };

        /// <summary>
        /// The tip of the arrow, in pixels from the texture's top-left — which is the corner Unity
        /// measures a hotspot from. The extreme corner, because the tip is drawn there: the point
        /// of the arrow and the point the click lands on are the same pixel or the cursor lies.
        /// </summary>
        public static readonly Vector2 ArrowHotspot = new(0f, 0f);

        /// <summary>
        /// The middle of the crosshair. 15 rather than 16 because a 32-wide texture has no centre
        /// pixel and the arms are drawn on row and column 15; the centre dot is there, so a
        /// hotspot there is on something drawn.
        /// </summary>
        public static readonly Vector2 CrosshairHotspot = new(15f, 15f);

        /// <summary>The pale body of the pointer. Near-white, so it reads on a dark board.</summary>
        static readonly Color32 Fill = new(244, 246, 248, 255);

        /// <summary>
        /// The dark edge every cursor carries. Without it a pale pointer disappears over snow,
        /// over a lit floor and over the HUD's own pale text — the reason every operating
        /// system's arrow is outlined.
        /// </summary>
        static readonly Color32 Edge = new(12, 14, 18, 255);

        static Texture2D? _arrow;
        static readonly Dictionary<DesignateTool, Texture2D> Crosshairs = new();

        /// <summary>The plain pointer: the default, and what the HUD wears.</summary>
        public static Texture2D Arrow => _arrow ??= BuildArrow();

        /// <summary>
        /// The crosshair for an armed order, in that order's own hue.
        ///
        /// <para>The colour comes from <see cref="OrderColours.Hue"/> and nowhere else. The chip in
        /// the orders strip, the palette header, the drag box and the mark on the board are already
        /// one hue per tool by a rule with a test behind it; the pointer is the fifth surface an
        /// armed order appears on, and it would have been the only one guessing.</para>
        ///
        /// <para>Cached per tool. A cursor rebuilt per frame would allocate a texture per frame,
        /// which is the sort of cost that never shows up in a review and always shows up in the
        /// frame budget.</para>
        /// </summary>
        public static Texture2D Crosshair(DesignateTool tool)
        {
            if (Crosshairs.TryGetValue(tool, out Texture2D? cached) && cached != null) return cached;

            Texture2D built = BuildCrosshair(HudTokens.Convert(OrderColours.Hue(tool)));
            Crosshairs[tool] = built;
            return built;
        }

        /// <summary>
        /// Throw the baked textures away. For editor tooling and for tests that want a clean
        /// build; a running game never needs it.
        /// </summary>
        public static void Forget()
        {
            Destroy(_arrow);
            _arrow = null;
            foreach (Texture2D texture in Crosshairs.Values) Destroy(texture);
            Crosshairs.Clear();
        }

        static void Destroy(Texture2D? texture)
        {
            if (texture == null) return;
            if (Application.isPlaying) Object.Destroy(texture);
            else Object.DestroyImmediate(texture);
        }

        static Texture2D BuildArrow()
        {
            Color32[] pixels = Blank();

            for (int row = 0; row < ArrowMask.Length; row++)
            {
                string line = ArrowMask[row];
                for (int column = 0; column < line.Length; column++)
                {
                    char ink = line[column];
                    if (ink == ' ') continue;
                    Plot(pixels, column, row, ink == 'X' ? Edge : Fill);
                }
            }

            return Bake(pixels, "OdysseyCursorArrow");
        }

        /// <summary>
        /// A gapped cross: four arms that stop short of the middle, a single dot at the hotspot,
        /// and a dark edge down both sides of every arm.
        ///
        /// <para>The gap is the point of it. A solid cross covers the cell corner the player is
        /// aiming at, which is the one part of the picture a placement cursor must not hide.</para>
        /// </summary>
        static Texture2D BuildCrosshair(Color hue)
        {
            Color32 arm = hue;
            arm.a = 255;

            Color32[] pixels = Blank();

            const int centre = 15;
            const int gap = 4;     // half-width of the hole in the middle
            const int reach = 13;  // how far an arm runs from the centre

            for (int offset = gap; offset <= reach; offset++)
            {
                foreach (int sign in Signs)
                {
                    int x = centre + sign * offset;
                    int y = centre + sign * offset;

                    Plot(pixels, x, centre - 1, Edge);
                    Plot(pixels, x, centre + 1, Edge);
                    Plot(pixels, x, centre, arm);

                    Plot(pixels, centre - 1, y, Edge);
                    Plot(pixels, centre + 1, y, Edge);
                    Plot(pixels, centre, y, arm);
                }
            }

            // The hotspot itself, so that the one pixel the click lands on is visible and is the
            // tool's own colour.
            Plot(pixels, centre, centre, arm);

            return Bake(pixels, "OdysseyCursorCrosshair");
        }

        static readonly int[] Signs = { -1, 1 };

        static Color32[] Blank()
        {
            var pixels = new Color32[Size * Size];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
            return pixels;
        }

        /// <summary>
        /// Paint one pixel, addressed from the <b>top-left</b> — the corner a hotspot is measured
        /// from and the corner a mask is written in. <see cref="Texture2D"/> stores its rows the
        /// other way up, and doing that flip in one place is what keeps the art, the hotspot and
        /// the texture from disagreeing about which end is the top.
        /// </summary>
        static void Plot(Color32[] pixels, int x, int y, Color32 colour)
        {
            if (x < 0 || x >= Size || y < 0 || y >= Size) return;
            pixels[(Size - 1 - y) * Size + x] = colour;
        }

        static Texture2D Bake(Color32[] pixels, string name)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return texture;
        }
    }
}
