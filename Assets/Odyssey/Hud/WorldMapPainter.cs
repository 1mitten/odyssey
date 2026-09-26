#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// Paints the planet into one RGBA image, once per seed (design 57 §9a–§9b, the specification's
    /// "Map texture"): every tile in its biome's two-stop ramp, the hill marks, then the polar haze,
    /// the sheen and the vignette baked in. The page uploads the bytes to one texture, so the whole
    /// map is one draw (P10), and repaints only when the seed changes.
    ///
    /// <para><b>Presentation, so floating point is allowed here</b>: nothing it computes reaches a
    /// cell, a save or the hash. It is engine-free so it can be tested and timed in the fast tier.</para>
    /// </summary>
    public static class WorldMapPainter
    {
        /// <summary>The ink the hill marks are drawn in: rgb(12, 16, 20).</summary>
        const float InkR = 12f, InkG = 16f, InkB = 20f;

        /// <summary>
        /// The painted image, <paramref name="width"/> × <paramref name="height"/> RGBA bytes.
        /// <paramref name="bottomUp"/> writes the last row first, which is the order a texture upload
        /// expects; the tests read it top-down. <paramref name="into"/> is reused when it is the right
        /// size, so a reroll allocates nothing (ten megabytes at 2× is a collection otherwise).
        /// </summary>
        public static byte[] Paint(PlanetView planet, float scale, out int width, out int height, bool bottomUp = false,
            byte[]? into = null)
        {
            var map = new WorldMapGeometry(planet.Width, planet.Height);
            width = (int)Math.Ceiling(map.Width * scale);
            height = (int)Math.Ceiling(map.Height * scale);
            byte[] bytes = into != null && into.Length == width * height * 4 ? into : new byte[width * height * 4];

            // Each tile's colour once, so the fill is a lookup.
            var tileR = new float[planet.TileCount];
            var tileG = new float[planet.TileCount];
            var tileB = new float[planet.TileCount];
            for (int t = 0; t < planet.TileCount; t++) TileColour(planet, t, out tileR[t], out tileG[t], out tileB[t]);

            var finish = new Finish(width, height);

            // 1. The tiles and the finish in one pass. A pixel belongs to the nearest hex centre (the
            //    hexes are exactly the nearest-centre cells). A pixel row within half a radius of a row
            //    of centres can only be in that row, so it weighs one candidate; the rows between weigh
            //    the two rows that bracket them. The finish — the haze, the sheen and the vignette — is
            //    the same affine map on every channel, so it is applied here rather than in a pass of
            //    its own, and the hill marks below blend towards the finished ink.
            float inverse = 1f / scale;
            float hexWidth = map.HexWidth, perHex = 1f / map.HexWidth, halfRadius = map.Radius / 2f;
            int columns = planet.Width, lastRow = planet.Height - 1;
            for (int y = 0; y < height; y++)
            {
                float my = (y + 0.5f) * inverse;
                int nearest = (int)Math.Floor((my - map.HexHeight / 2f) / map.RowStep + 0.5f);
                if (nearest < 0) nearest = 0;
                if (nearest > lastRow) nearest = lastRow;
                float yNearest = map.CentreY(nearest);
                bool single = Math.Abs(my - yNearest) <= halfRadius || nearest == 0 && my < yNearest
                              || nearest == lastRow && my > yNearest;
                int other = my < yNearest ? nearest - 1 : nearest + 1;
                if (other < 0 || other > lastRow) { single = true; other = nearest; }

                float offsetA = (nearest & 1) == 1 ? hexWidth : hexWidth / 2f;
                float offsetB = (other & 1) == 1 ? hexWidth : hexWidth / 2f;
                float dya = (my - yNearest) * (my - yNearest);
                float yOther = map.CentreY(other), dyb = (my - yOther) * (my - yOther);
                int rowA = nearest * columns, rowB = other * columns;

                finish.Row(y);
                int o = 4 * (bottomUp ? height - 1 - y : y) * width;
                for (int x = 0; x < width; x++, o += 4)
                {
                    float mx = (x + 0.5f) * inverse;
                    // A floor by cast, kept positive by a turn of the planet added and taken away.
                    int ca = (int)((mx - offsetA) * perHex + 0.5f + columns) - columns;
                    int tile = rowA + Wrap(ca, columns);
                    if (!single)
                    {
                        int cb = (int)((mx - offsetB) * perHex + 0.5f + columns) - columns;
                        float dxa = mx - (offsetA + ca * hexWidth), dxb = mx - (offsetB + cb * hexWidth);
                        if (dxb * dxb + dyb < dxa * dxa + dya) tile = rowB + Wrap(cb, columns);
                    }
                    finish.At(x, out float m, out float ar, out float ag, out float ab);
                    bytes[o] = ToByte(tileR[tile] * m + ar);
                    bytes[o + 1] = ToByte(tileG[tile] * m + ag);
                    bytes[o + 2] = ToByte(tileB[tile] * m + ab);
                    bytes[o + 3] = 255;
                }
            }

            // 2. The hill marks, the one cue that does not rely on colour, stamped from one coverage
            //    mask per band; not on Ice. They blend towards the ink as finished at that pixel, which
            //    is the same picture as marking first and finishing after, because the finish is affine.
            Stamp? hilly = null, mountainous = null, sheer = null;
            for (int t = 0; t < planet.TileCount; t++)
            {
                HillBand hills = planet.HillsAt(t);
                if (hills < HillBand.Hilly || planet.Water[t] || planet.BiomeAt(t).Ramp == MapRamp.Ice) continue;
                Stamp stamp = hills == HillBand.Hilly ? hilly ??= new Stamp(hills, scale)
                    : hills == HillBand.Mountainous ? mountainous ??= new Stamp(hills, scale)
                    : sheer ??= new Stamp(hills, scale);
                int column = HexGrid.Column(t, planet.Width), hexRow = HexGrid.Row(t, planet.Width);
                stamp.Apply(bytes, width, height, map.CentreX(column, hexRow) * scale, map.CentreY(hexRow) * scale,
                    bottomUp, finish);
            }
            return bytes;
        }

        static int Wrap(int column, int columns) =>
            column < 0 ? column + columns : column >= columns ? column - columns : column;

        /// <summary>
        /// The polar haze, the sheen and the vignette as one affine map per pixel, c × m + add
        /// (the specification's "Finish overlays"): flat ramps, baked once, the HUD's panels untouched.
        /// Tabulated by row and column, since the haze is a function of the row alone and the sheen and
        /// vignette of the row and column separately.
        /// </summary>
        sealed class Finish
        {
            // Tabulated by the two things the sheen and the vignette depend on, so a pixel costs two
            // lookups and no square root: the diagonal (0–1) for the sheen, the squared distance from
            // the centre (0–2) for the vignette. 2,048 steps each, finer than a byte of colour can see.
            const int Steps = 2048;
            static readonly float[] SheenKeep = new float[Steps + 1], SheenWhite = new float[Steps + 1];
            static readonly float[] VignetteKeep = new float[Steps + 1];

            static Finish()
            {
                for (int i = 0; i <= Steps; i++)
                {
                    // Sheen: white at 10 % top-left, nothing through the middle, black at 18 % bottom-right.
                    float diagonal = (float)i / Steps;
                    float white = diagonal < 0.5f ? 0.10f * (1f - diagonal * 2f) : 0f;
                    float black = diagonal > 0.5f ? 0.18f * (diagonal * 2f - 1f) : 0f;
                    SheenKeep[i] = (1f - white) * (1f - black);
                    SheenWhite[i] = 255f * white;

                    // Vignette: radial, clear to 55 % of the way out, black at 55 % at the edge. The
                    // distance is 1 at the middle of each edge, so the corners reach the full 55 %.
                    float d = (float)Math.Sqrt(2.0 * i / Steps);
                    float vignette = d <= 0.55f ? 0f : 0.55f * Math.Min(1f, (d - 0.55f) / 0.45f);
                    VignetteKeep[i] = 1f - vignette;
                }
            }

            readonly int[] _diagonalX, _distanceX;
            readonly int _height;
            int _diagonalY, _distanceY;
            float _hazeKeep, _hazeR, _hazeG, _hazeB;

            public Finish(int width, int height)
            {
                _height = height;
                _diagonalX = new int[width];
                _distanceX = new int[width];
                for (int x = 0; x < width; x++)
                {
                    float fx = (x + 0.5f) / width;
                    _diagonalX[x] = (int)(fx / 2f * Steps);
                    float dx = (fx - 0.5f) * 2f;
                    _distanceX[x] = (int)(dx * dx / 2f * Steps);
                }
            }

            public void Row(int y)
            {
                float fy = (y + 0.5f) / _height;
                // Polar haze: #e8f2f7 at 28 % at the top and bottom edges, gone by 14 % in.
                float haze = fy < 0.14f ? 0.28f * (1f - fy / 0.14f) : fy > 0.86f ? 0.28f * ((fy - 0.86f) / 0.14f) : 0f;
                _hazeKeep = 1f - haze;
                _hazeR = haze * 232f;
                _hazeG = haze * 242f;
                _hazeB = haze * 247f;
                _diagonalY = (int)(fy / 2f * Steps);
                float dy = (fy - 0.5f) * 2f;
                _distanceY = (int)(dy * dy / 2f * Steps);
            }

            public void At(int x, out float m, out float addR, out float addG, out float addB)
            {
                int diagonal = _diagonalX[x] + _diagonalY;
                int distance = _distanceX[x] + _distanceY;
                if (diagonal > Steps) diagonal = Steps;
                if (distance > Steps) distance = Steps;
                float sheen = SheenKeep[diagonal], white = SheenWhite[diagonal], keep = VignetteKeep[distance];
                m = _hazeKeep * sheen * keep;
                addR = (_hazeR * sheen + white) * keep;
                addG = (_hazeG * sheen + white) * keep;
                addB = (_hazeB * sheen + white) * keep;
            }
        }

        /// <summary>One band's mark rasterised once as a coverage mask about a pixel centre, then stamped.</summary>
        sealed class Stamp
        {
            readonly float[] _coverage;
            readonly int _size, _half;

            public Stamp(HillBand hills, float scale)
            {
                float[] path = MarkPath(hills);
                MarkStroke(hills, out float stroke, out float alpha);
                float halfStroke = stroke * scale / 2f;
                float reach = 0f;
                for (int i = 0; i < path.Length; i++) reach = Math.Max(reach, Math.Abs(path[i]) * scale);
                _half = (int)Math.Ceiling(reach + halfStroke + 1f);
                _size = 2 * _half + 1;
                _coverage = new float[_size * _size];
                for (int y = 0; y < _size; y++)
                    for (int x = 0; x < _size; x++)
                    {
                        float px = x - _half, py = y - _half, nearest = float.MaxValue;
                        for (int i = 0; i + 3 < path.Length; i += 2)
                            nearest = Math.Min(nearest, SegmentDistance(px, py,
                                path[i] * scale, path[i + 1] * scale, path[i + 2] * scale, path[i + 3] * scale));
                        _coverage[y * _size + x] = Math.Max(0f, Math.Min(1f, halfStroke + 0.5f - nearest)) * alpha;
                    }
            }

            public void Apply(byte[] bytes, int width, int height, float cx, float cy, bool bottomUp, Finish finish)
            {
                int ox = (int)Math.Floor(cx) - _half, oy = (int)Math.Floor(cy) - _half;
                for (int sy = 0; sy < _size; sy++)
                {
                    int y = oy + sy;
                    if (y < 0 || y >= height) continue;
                    finish.Row(y);
                    int row = bottomUp ? height - 1 - y : y;
                    for (int sx = 0; sx < _size; sx++)
                    {
                        float a = _coverage[sy * _size + sx];
                        int x = ox + sx;
                        if (a <= 0f || x < 0 || x >= width) continue;
                        finish.At(x, out float m, out float ar, out float ag, out float ab);
                        int o = 4 * (row * width + x);
                        bytes[o] = ToByte(bytes[o] + (InkR * m + ar - bytes[o]) * a);
                        bytes[o + 1] = ToByte(bytes[o + 1] + (InkG * m + ag - bytes[o + 1]) * a);
                        bytes[o + 2] = ToByte(bytes[o + 2] + (InkB * m + ab - bytes[o + 2]) * a);
                    }
                }
            }
        }

        /// <summary>
        /// A tile's colour: its biome's ramp at the specification's t (design 57 §9b). Ocean by depth,
        /// land by height with a tile-hashed jitter, and Ice on the sea by depth in a pale band.
        /// </summary>
        public static void TileColour(PlanetView planet, int tile, out float r, out float g, out float b)
        {
            BiomeView biome = planet.BiomeAt(tile);
            float e = planet.Elevation[tile] / 1023f;
            float sea = Math.Max(0.001f, planet.SeaLevel / 1023f);
            int column = HexGrid.Column(tile, planet.Width), row = HexGrid.Row(tile, planet.Width);

            float t;
            if (planet.Water[tile])
            {
                float depth = Math.Min(1f, e / sea);
                t = biome.Ramp == MapRamp.Ice ? 0.6f + depth * 0.4f : (float)Math.Pow(depth, 2.2);
            }
            else
            {
                float land = (e - sea) / Math.Max(0.001f, 1f - sea);
                t = land * 1.1f + (Jitter(column, row) - 0.5f) * 0.12f;
            }
            t = Math.Max(0f, Math.Min(1f, t));
            Lerp(biome.RampFromRgb, biome.RampToRgb, t, out r, out g, out b);
        }

        /// <summary>The ramp's middle, which is what the legend's swatch shows.</summary>
        public static void MidColour(BiomeView biome, out float r, out float g, out float b) =>
            Lerp(biome.RampFromRgb, biome.RampToRgb, 0.5f, out r, out g, out b);

        /// <summary>A deterministic 0–1 from a tile's place: the specification's hash(col, row, 7).</summary>
        public static float Jitter(int column, int row)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)column) * 16777619u;
                h = (h ^ (uint)row) * 16777619u;
                h = (h ^ 7u) * 16777619u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFF) / 65535f;
            }
        }

        /// <summary>
        /// The three marks in 1× pixels about the tile centre (the specification's): Hilly a bump,
        /// Mountainous a peak, Sheer a taller peak. Returned as polylines, x, y pairs.
        /// </summary>
        public static float[] MarkPath(HillBand hills)
        {
            switch (hills)
            {
                case HillBand.Hilly:
                    // A quadratic from (−4, 2) to (4, 2) with its control 5 above them, at (0, −3), as eight segments.
                    var bump = new float[18];
                    for (int i = 0; i <= 8; i++)
                    {
                        float u = i / 8f;
                        float x = (1 - u) * (1 - u) * -4f + 2 * (1 - u) * u * 0f + u * u * 4f;
                        float y = (1 - u) * (1 - u) * 2f + 2 * (1 - u) * u * -3f + u * u * 2f;
                        bump[2 * i] = x;
                        bump[2 * i + 1] = y;
                    }
                    return bump;
                case HillBand.Mountainous:
                    return new[] { -4.5f, 3f, 0f, -3f, 4.5f, 3f };
                case HillBand.Sheer:
                    return new[] { -4.5f, 3f, 0f, -5f, 4.5f, 3f };
                default:
                    return Array.Empty<float>();
            }
        }

        /// <summary>A mark's stroke width and ink alpha: Hilly 1.5 at 40 %, the peaks 1.6 at 55 %.</summary>
        public static void MarkStroke(HillBand hills, out float width, out float alpha)
        {
            width = hills == HillBand.Hilly ? 1.5f : 1.6f;
            alpha = hills == HillBand.Hilly ? 0.40f : 0.55f;
        }

        static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            float length = vx * vx + vy * vy;
            float u = length <= 0f ? 0f : Math.Max(0f, Math.Min(1f, ((px - ax) * vx + (py - ay) * vy) / length));
            float qx = ax + u * vx - px, qy = ay + u * vy - py;
            return (float)Math.Sqrt(qx * qx + qy * qy);
        }

        static void Lerp(int from, int to, float t, out float r, out float g, out float b)
        {
            r = ((from >> 16) & 0xFF) + ((((to >> 16) & 0xFF) - ((from >> 16) & 0xFF)) * t);
            g = ((from >> 8) & 0xFF) + ((((to >> 8) & 0xFF) - ((from >> 8) & 0xFF)) * t);
            b = (from & 0xFF) + (((to & 0xFF) - (from & 0xFF)) * t);
        }

        static byte ToByte(float v) => (byte)Math.Max(0, Math.Min(255, (int)(v + 0.5f)));
    }
}
