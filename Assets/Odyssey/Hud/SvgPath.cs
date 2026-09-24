#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Odyssey.Hud
{
    /// <summary>
    /// An SVG path's <c>d</c> string, flattened into polylines once, so a line-art icon can be
    /// written the way its designer wrote it and stroked by whatever draws it.
    ///
    /// <para><b>Why the settings window draws its icons from path strings</b> (design 39 §4):
    /// the approved mockup gives every icon as a path on a 24-unit grid, and copying those into
    /// <see cref="HudGlyph"/>'s hand-written segments would be a second transcription of each one,
    /// which is where they would drift. The string is the source; this is the only reader.</para>
    ///
    /// <para><b>Flattened here rather than handed to the painter as curves</b>, because the
    /// arc command is written endpoint-first and the painter wants a centre, and because a
    /// polyline can be tested in the fast tier: every icon parses, and every point lands inside
    /// its box. Curves become <see cref="CurveSteps"/> segments and arcs one segment per
    /// <see cref="ArcStepDegrees"/>, which at 20 px is finer than a pixel.</para>
    ///
    /// <para>Supports M L H V C S Q T A Z in both cases — every command the mockup uses and the
    /// three it does not, so the next icon does not need this file opened.</para>
    /// </summary>
    public static class SvgPath
    {
        /// <summary>Segments per Bezier curve.</summary>
        public const int CurveSteps = 12;

        /// <summary>Degrees of arc per segment.</summary>
        public const float ArcStepDegrees = 12f;

        /// <summary>One subpath: x, y pairs in the path's own units, and whether it closes.</summary>
        public readonly struct Subpath
        {
            public Subpath(float[] points, bool closed)
            {
                Points = points;
                Closed = closed;
            }

            /// <summary>Interleaved x, y.</summary>
            public float[] Points { get; }

            public bool Closed { get; }

            public int Count => Points.Length / 2;
        }

        /// <summary>
        /// Flatten a path. Throws <see cref="FormatException"/> on a command it does not know, so
        /// a typo in an icon fails the fast tier rather than drawing half an icon.
        /// </summary>
        public static IReadOnlyList<Subpath> Parse(string d)
        {
            var result = new List<Subpath>();
            var current = new List<float>();
            bool closed = false;

            float x = 0, y = 0;           // the pen
            float sx = 0, sy = 0;         // where this subpath started
            float cx = 0, cy = 0;         // the last control point, for S and T
            char last = ' ';

            int i = 0;
            char command = ' ';

            void Flush()
            {
                if (current.Count >= 4) result.Add(new Subpath(current.ToArray(), closed));
                current.Clear();
                closed = false;
            }

            void Add(float px, float py)
            {
                current.Add(px);
                current.Add(py);
            }

            while (true)
            {
                SkipSeparators(d, ref i);
                if (i >= d.Length) break;

                char c = d[i];
                if (char.IsLetter(c))
                {
                    command = c;
                    i++;
                }
                else if (command == ' ')
                {
                    throw new FormatException($"path starts with a number: \"{d}\"");
                }

                bool rel = char.IsLower(command);
                char upper = char.ToUpperInvariant(command);
                float ox = rel ? x : 0, oy = rel ? y : 0;

                switch (upper)
                {
                    case 'M':
                    {
                        Flush();
                        x = ox + Number(d, ref i);
                        y = oy + Number(d, ref i);
                        sx = x;
                        sy = y;
                        Add(x, y);
                        // Pairs after a moveto are linetos, in the same case.
                        command = rel ? 'l' : 'L';
                        cx = x;
                        cy = y;
                        last = 'M';
                        continue;
                    }
                    case 'L':
                        x = ox + Number(d, ref i);
                        y = oy + Number(d, ref i);
                        Add(x, y);
                        break;
                    case 'H':
                        x = ox + Number(d, ref i);
                        Add(x, y);
                        break;
                    case 'V':
                        y = oy + Number(d, ref i);
                        Add(x, y);
                        break;
                    case 'C':
                    {
                        float x1 = ox + Number(d, ref i), y1 = oy + Number(d, ref i);
                        float x2 = ox + Number(d, ref i), y2 = oy + Number(d, ref i);
                        float ex = ox + Number(d, ref i), ey = oy + Number(d, ref i);
                        Cubic(current, x, y, x1, y1, x2, y2, ex, ey);
                        cx = x2; cy = y2; x = ex; y = ey;
                        last = 'C';
                        continue;
                    }
                    case 'S':
                    {
                        float x1 = last == 'C' ? 2 * x - cx : x;
                        float y1 = last == 'C' ? 2 * y - cy : y;
                        float x2 = ox + Number(d, ref i), y2 = oy + Number(d, ref i);
                        float ex = ox + Number(d, ref i), ey = oy + Number(d, ref i);
                        Cubic(current, x, y, x1, y1, x2, y2, ex, ey);
                        cx = x2; cy = y2; x = ex; y = ey;
                        last = 'C';
                        continue;
                    }
                    case 'Q':
                    {
                        float x1 = ox + Number(d, ref i), y1 = oy + Number(d, ref i);
                        float ex = ox + Number(d, ref i), ey = oy + Number(d, ref i);
                        Quad(current, x, y, x1, y1, ex, ey);
                        cx = x1; cy = y1; x = ex; y = ey;
                        last = 'Q';
                        continue;
                    }
                    case 'T':
                    {
                        float x1 = last == 'Q' ? 2 * x - cx : x;
                        float y1 = last == 'Q' ? 2 * y - cy : y;
                        float ex = ox + Number(d, ref i), ey = oy + Number(d, ref i);
                        Quad(current, x, y, x1, y1, ex, ey);
                        cx = x1; cy = y1; x = ex; y = ey;
                        last = 'Q';
                        continue;
                    }
                    case 'A':
                    {
                        float rx = Number(d, ref i), ry = Number(d, ref i);
                        float rotation = Number(d, ref i);
                        bool large = Flag(d, ref i);
                        bool sweep = Flag(d, ref i);
                        float ex = ox + Number(d, ref i), ey = oy + Number(d, ref i);
                        Arc(current, x, y, rx, ry, rotation, large, sweep, ex, ey);
                        x = ex;
                        y = ey;
                        break;
                    }
                    case 'Z':
                        closed = true;
                        x = sx;
                        y = sy;
                        Flush();
                        // A path may carry on drawing after a close without a moveto; it starts
                        // again from where the closed subpath began.
                        Add(x, y);
                        command = ' ';
                        last = 'Z';
                        continue;
                    default:
                        throw new FormatException($"unsupported path command '{command}' in \"{d}\"");
                }

                cx = x;
                cy = y;
                last = upper;
            }

            Flush();
            return result;
        }

        static void Cubic(List<float> into, float x0, float y0, float x1, float y1,
            float x2, float y2, float x3, float y3)
        {
            for (int s = 1; s <= CurveSteps; s++)
            {
                float t = s / (float)CurveSteps, u = 1 - t;
                into.Add(u * u * u * x0 + 3 * u * u * t * x1 + 3 * u * t * t * x2 + t * t * t * x3);
                into.Add(u * u * u * y0 + 3 * u * u * t * y1 + 3 * u * t * t * y2 + t * t * t * y3);
            }
        }

        static void Quad(List<float> into, float x0, float y0, float x1, float y1, float x2, float y2)
        {
            for (int s = 1; s <= CurveSteps; s++)
            {
                float t = s / (float)CurveSteps, u = 1 - t;
                into.Add(u * u * x0 + 2 * u * t * x1 + t * t * x2);
                into.Add(u * u * y0 + 2 * u * t * y1 + t * t * y2);
            }
        }

        /// <summary>The endpoint-to-centre conversion of SVG 1.1 appendix F.6.5, then a
        /// polyline round the centre.</summary>
        static void Arc(List<float> into, float x1, float y1, float rx, float ry, float degrees,
            bool large, bool sweep, float x2, float y2)
        {
            if (Math.Abs(x1 - x2) < 1e-6f && Math.Abs(y1 - y2) < 1e-6f) return;
            rx = Math.Abs(rx);
            ry = Math.Abs(ry);
            if (rx < 1e-6f || ry < 1e-6f)
            {
                into.Add(x2);
                into.Add(y2);
                return;
            }

            double phi = degrees * Math.PI / 180.0;
            double cos = Math.Cos(phi), sin = Math.Sin(phi);
            double dx = (x1 - x2) / 2.0, dy = (y1 - y2) / 2.0;
            double x1p = cos * dx + sin * dy;
            double y1p = -sin * dx + cos * dy;

            // Radii too small to reach are scaled up until they just do.
            double lambda = x1p * x1p / (rx * rx) + y1p * y1p / (ry * ry);
            if (lambda > 1)
            {
                double k = Math.Sqrt(lambda);
                rx = (float)(rx * k);
                ry = (float)(ry * k);
            }

            double num = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p;
            double den = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
            double coef = den == 0 ? 0 : Math.Sqrt(Math.Max(0, num / den));
            if (large == sweep) coef = -coef;
            double cxp = coef * rx * y1p / ry;
            double cyp = -coef * ry * x1p / rx;
            double ccx = cos * cxp - sin * cyp + (x1 + x2) / 2.0;
            double ccy = sin * cxp + cos * cyp + (y1 + y2) / 2.0;

            double theta1 = Angle(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
            double delta = Angle((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);
            if (!sweep && delta > 0) delta -= 2 * Math.PI;
            else if (sweep && delta < 0) delta += 2 * Math.PI;

            int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(delta) * 180.0 / Math.PI / ArcStepDegrees));
            for (int s = 1; s <= steps; s++)
            {
                double t = theta1 + delta * s / steps;
                double ex = rx * Math.Cos(t), ey = ry * Math.Sin(t);
                into.Add((float)(cos * ex - sin * ey + ccx));
                into.Add((float)(sin * ex + cos * ey + ccy));
            }
        }

        static double Angle(double ux, double uy, double vx, double vy)
        {
            double a = Math.Atan2(uy, ux);
            double b = Math.Atan2(vy, vx);
            double d = b - a;
            while (d > Math.PI) d -= 2 * Math.PI;
            while (d < -Math.PI) d += 2 * Math.PI;
            return d;
        }

        static void SkipSeparators(string d, ref int i)
        {
            while (i < d.Length && (d[i] == ' ' || d[i] == ',' || d[i] == '\n' || d[i] == '\t' || d[i] == '\r'))
                i++;
        }

        /// <summary>An arc flag, which may be written with no separator after it ("a3 3 0 1 0 0-6").</summary>
        static bool Flag(string d, ref int i)
        {
            SkipSeparators(d, ref i);
            if (i >= d.Length || (d[i] != '0' && d[i] != '1'))
                throw new FormatException($"expected an arc flag at {i} in \"{d}\"");
            return d[i++] == '1';
        }

        /// <summary>
        /// One number, in SVG's compact grammar: a sign or a second decimal point starts the next
        /// number without a separator ("M6.5 10.5h1", "a7 7 0 1 0 10 0", "M16 8l4 4-4 4",
        /// "M9.5 13.5 12 11l2.5 2.5", "h.5").
        /// </summary>
        static float Number(string d, ref int i)
        {
            SkipSeparators(d, ref i);
            int start = i;
            if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++;
            bool dot = false;
            while (i < d.Length)
            {
                char c = d[i];
                if (c >= '0' && c <= '9') { i++; continue; }
                if (c == '.' && !dot) { dot = true; i++; continue; }
                break;
            }
            if (i == start || (i == start + 1 && (d[start] == '-' || d[start] == '+')))
                throw new FormatException($"expected a number at {start} in \"{d}\"");
            return float.Parse(d.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
