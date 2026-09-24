#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// The side of a body a sheathed weapon hangs against, as two relief maps taken off the drawn
    /// mesh once a look: the envelope of the whole idle, which every pawn plays from a phase of its
    /// own (design 33 §9c).
    ///
    /// <para><b>The frame.</b> x is out from the body on the sheath's side, y is up, z is the
    /// figure's forward, all in world metres about the figure's root as it stood when measured.
    /// The grid runs over y and z; each cell of <see cref="_body"/> holds how far out the body
    /// (everything but the arms) reaches there, and each cell of <see cref="_arm"/> how far in the
    /// hanging arm comes. A weapon hung against the hip must lie outside the first and inside the
    /// second, and that is the whole fit (<see cref="Fit"/>).</para>
    ///
    /// <para><b>Why a relief and not the nearest triangle.</b> A weapon is fitted when it is put in
    /// the hand, which can be mid-stride and long after the figure was built; the relief is kept in
    /// the pelvis's space as it was in the idle, so the fit is the idle's whatever the leg is doing
    /// then, and costs a lookup per weapon point rather than a walk over the skin.</para>
    /// </summary>
    public sealed class SheathSurface
    {
        /// <summary>The grid's cell, as a fraction of the figure's height.</summary>
        public const float CellFraction = 0.006f;

        /// <summary>
        /// The window of the body that is mapped, as fractions of the figure's height about the hip
        /// joint (y) and the thigh joint's depth (z): from the ankle to the ribs, a foot either way.
        /// </summary>
        public const float BelowHip = 0.5f, AboveHip = 0.25f, Depth = 0.15f;

        readonly float[] _body;
        readonly float[] _arm;
        readonly int _rows, _cols;
        readonly float _y0, _z0;

        /// <summary>The grid's cell, in metres.</summary>
        public float Cell { get; }

        /// <summary>From the pelvis's own space, as it was when measured, into the frame; and back.</summary>
        public Matrix4x4 PelvisToFrame { get; }
        public Matrix4x4 FrameToPelvis { get; }

        /// <summary>How many cells the body was seen in; nought is a body with nothing bakeable.</summary>
        public int BodyCells { get; }

        SheathSurface(float cell, int rows, int cols, float y0, float z0, float[] body, float[] arm,
            Matrix4x4 pelvisToFrame, int bodyCells)
        {
            Cell = cell;
            _rows = rows;
            _cols = cols;
            _y0 = y0;
            _z0 = z0;
            _body = body;
            _arm = arm;
            PelvisToFrame = pelvisToFrame;
            FrameToPelvis = pelvisToFrame.inverse;
            BodyCells = bodyCells;
        }

        /// <summary>The hip joint's height and the thigh joint's depth in the frame, and the figure's height.</summary>
        public float HipY { get; private set; }
        public float ThighZ { get; private set; }
        public float Height { get; private set; }

        /// <summary>
        /// The depth at which the side of the hip and the upper thigh stands out furthest: for
        /// each row from a quarter of the figure's height below the hip joint up to it, the depth
        /// of its outermost cell, and the median of those. Where a weapon hung at the side belongs;
        /// the thigh joint's own depth where nothing was seen.
        /// </summary>
        public float SideDepth()
        {
            var depths = new List<float>();
            int from = Mathf.Max(0, Mathf.FloorToInt((HipY - 0.25f * Height - _y0) / Cell));
            int to = Mathf.Min(_rows - 1, Mathf.FloorToInt((HipY - _y0) / Cell));
            for (int r = from; r <= to; r++)
            {
                int best = -1;
                for (int c = 0; c < _cols; c++)
                    if (!float.IsNegativeInfinity(_body[r * _cols + c]) && (best < 0 || _body[r * _cols + c] > _body[r * _cols + best]))
                        best = c;
                if (best >= 0) depths.Add(_z0 + (best + 0.5f) * Cell);
            }
            if (depths.Count == 0) return ThighZ;
            depths.Sort();
            return depths[depths.Count / 2];
        }

        /// <summary>
        /// The depth of a weapon's own line, given its points in the frame: the middle of its
        /// front-to-back spread in each band of height it occupies, and the median of those — the
        /// shaft of a crowbar rather than the middle of its claw, the middle of a blade rather than
        /// of its bounds.
        /// </summary>
        public float SpineDepth(List<Vector3> points)
        {
            var bands = new Dictionary<int, Vector2>();
            float band = 2f * Cell;
            for (int i = 0; i < points.Count; i++)
            {
                int key = Mathf.FloorToInt(points[i].y / band);
                bands[key] = bands.TryGetValue(key, out Vector2 range)
                    ? new Vector2(Mathf.Min(range.x, points[i].z), Mathf.Max(range.y, points[i].z))
                    : new Vector2(points[i].z, points[i].z);
            }
            var mids = new List<float>(bands.Count);
            foreach (Vector2 range in bands.Values) mids.Add(0.5f * (range.x + range.y));
            if (mids.Count == 0) return 0f;
            mids.Sort();
            return mids[mids.Count / 2];
        }

        /// <summary>
        /// Map a posed body. <paramref name="bodyTriangles"/> and <paramref name="armTriangles"/> are
        /// world-space triangle soups (three points each), as posed now; the frame is
        /// <paramref name="origin"/> with <paramref name="outward"/>, up and <paramref name="forward"/>;
        /// <paramref name="hipY"/> and <paramref name="thighZ"/> centre the window, in frame units.
        /// Null with nothing of the body in the window.
        /// </summary>
        public static SheathSurface? Map(List<Vector3> bodyTriangles, List<Vector3> armTriangles,
            Vector3 origin, Vector3 outward, Vector3 forward, float hipY, float thighZ, float height,
            Matrix4x4 pelvisLocalToWorld)
        {
            float cell = Mathf.Max(0.002f, CellFraction * height);
            float y0 = hipY - BelowHip * height, z0 = thighZ - Depth * height;
            int rows = Mathf.CeilToInt((BelowHip + AboveHip) * height / cell);
            int cols = Mathf.CeilToInt(2f * Depth * height / cell);

            Vector3 up = Vector3.Cross(forward, outward).normalized;
            if (Vector3.Dot(up, Vector3.up) < 0f) up = -up;
            var toFrame = Matrix4x4.identity;
            toFrame.SetRow(0, new Vector4(outward.x, outward.y, outward.z, -Vector3.Dot(outward, origin)));
            toFrame.SetRow(1, new Vector4(up.x, up.y, up.z, -Vector3.Dot(up, origin)));
            toFrame.SetRow(2, new Vector4(forward.x, forward.y, forward.z, -Vector3.Dot(forward, origin)));
            toFrame.SetRow(3, new Vector4(0f, 0f, 0f, 1f));

            var body = new float[rows * cols];
            var arm = new float[rows * cols];
            for (int i = 0; i < body.Length; i++)
            {
                body[i] = float.NegativeInfinity;
                arm[i] = float.PositiveInfinity;
            }

            Rasterise(bodyTriangles, toFrame, body, rows, cols, y0, z0, cell, outermost: true);
            Rasterise(armTriangles, toFrame, arm, rows, cols, y0, z0, cell, outermost: false);

            // The arm is given a cell of grace each way; the body is not, because the grace put a
            // centimetre of air between every weapon and the hip it was fitted to (measured).
            arm = Spread(arm, rows, cols, outermost: false);

            int seen = 0;
            for (int i = 0; i < body.Length; i++) if (!float.IsNegativeInfinity(body[i])) seen++;
            if (seen == 0) return null;
            return new SheathSurface(cell, rows, cols, y0, z0, body, arm, toFrame * pelvisLocalToWorld, seen)
            {
                HipY = hipY,
                ThighZ = thighZ,
                Height = height,
            };
        }

        /// <summary>How far out the body reaches at (y, z) in the frame; negative infinity where it is not.</summary>
        public float BodyAt(float y, float z) => Lookup(_body, y, z, float.NegativeInfinity);

        /// <summary>How far in the arm comes at (y, z); positive infinity where it is not.</summary>
        public float ArmAt(float y, float z) => Lookup(_arm, y, z, float.PositiveInfinity);

        float Lookup(float[] map, float y, float z, float none)
        {
            int r = Mathf.FloorToInt((y - _y0) / Cell), c = Mathf.FloorToInt((z - _z0) / Cell);
            return r < 0 || r >= _rows || c < 0 || c >= _cols ? none : map[r * _cols + c];
        }

        /// <summary>
        /// How far out a weapon must slide to keep every point <paramref name="clearance"/> clear of
        /// the body, at <paramref name="along"/> forward; negative infinity with no body beside it.
        /// </summary>
        public float Slide(List<Vector3> points, float clearance, float along)
        {
            float low = float.NegativeInfinity;
            for (int i = 0; i < points.Count; i++)
            {
                float body = BodyAt(points[i].y, points[i].z + along);
                if (!float.IsNegativeInfinity(body)) low = Mathf.Max(low, body + clearance - points[i].x);
            }
            return low;
        }

        /// <summary>What <see cref="Fit"/> chose, and why: for the probe and the design document.</summary>
        public struct Placement
        {
            /// <summary>How far the weapon is slid out, and back (negative) or forward, in metres.</summary>
            public float Outward, Along;

            /// <summary>How far the weapon still overlaps the hanging arm at the chosen depth, in metres; nought when it clears.</summary>
            public float ArmMiss;

            /// <summary>The weapon point that decided how far out, in the frame after the move.</summary>
            public Vector3 Binding;
        }

        /// <summary>
        /// Where to slide a weapon, given its surface points in the frame: the smallest move out
        /// that keeps every point <paramref name="clearance"/> clear of the body, at the first depth
        /// — from <paramref name="start"/>, stepping a cell at a time back as far as
        /// <paramref name="maxBack"/> and forward as far as <paramref name="maxForward"/>, back
        /// first — where the weapon is also clear of the hanging arm. With no depth that clears
        /// the arm, the start: the hand hangs over the weapon rather than the weapon wandering off
        /// the hip to avoid it (the first try let it walk twenty centimetres forward, in front of
        /// the knee). False when no point of the weapon has body beside it.
        /// </summary>
        public bool Fit(List<Vector3> points, float clearance, float start, float maxBack, float maxForward,
            out Placement placement)
        {
            placement = default;
            bool any = false;
            int back = Mathf.Max(0, Mathf.FloorToInt(maxBack / Cell)), forward = Mathf.Max(0, Mathf.FloorToInt(maxForward / Cell));
            for (int k = 0; k <= 2 * Mathf.Max(back, forward); k++)
            {
                // 0, -1, +1, -2, +2, ... cells from the start, within each side's reach.
                int n = (k + 1) / 2;
                bool behind = k % 2 == 1;
                if (behind ? n > back : n > forward) continue;
                float dz = start + (behind ? -n : n) * Cell;
                float low = float.NegativeInfinity, high = float.PositiveInfinity;
                int binding = -1;
                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 p = points[i];
                    float body = BodyAt(p.y, p.z + dz);
                    if (!float.IsNegativeInfinity(body) && body + clearance - p.x > low)
                    {
                        low = body + clearance - p.x;
                        binding = i;
                    }
                    float arm = ArmAt(p.y, p.z + dz);
                    if (!float.IsPositiveInfinity(arm)) high = Mathf.Min(high, arm - p.x);
                }
                if (binding < 0) continue;
                float miss = Mathf.Max(0f, low - high);
                // The start is the answer unless a depth that clears the hand turns up.
                if (!any || miss <= 0f)
                {
                    placement = new Placement
                    {
                        Outward = low,
                        Along = dz,
                        ArmMiss = miss,
                        Binding = points[binding] + new Vector3(low, 0f, dz),
                    };
                }
                any = true;
                if (miss <= 0f) break;
            }
            return any;
        }

        static void Rasterise(List<Vector3> triangles, Matrix4x4 toFrame, float[] map, int rows, int cols,
            float y0, float z0, float cell, bool outermost)
        {
            float spacing = cell * 0.75f;
            for (int t = 0; t + 2 < triangles.Count; t += 3)
            {
                Vector3 a = toFrame.MultiplyPoint3x4(triangles[t]);
                Vector3 b = toFrame.MultiplyPoint3x4(triangles[t + 1]);
                Vector3 c = toFrame.MultiplyPoint3x4(triangles[t + 2]);
                // Only the sheath's side of the body; the far side is never what a weapon meets.
                if (a.x < 0f && b.x < 0f && c.x < 0f) continue;
                float yLo = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), yHi = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                float zLo = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), zHi = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
                if (yHi < y0 || yLo > y0 + rows * cell || zHi < z0 || zLo > z0 + cols * cell) continue;

                int ab = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / spacing));
                int ac = Mathf.Max(1, Mathf.CeilToInt((c - a).magnitude / spacing));
                int n = Mathf.Min(256, Mathf.Max(ab, ac));
                for (int i = 0; i <= n; i++)
                {
                    for (int j = 0; i + j <= n; j++)
                    {
                        Vector3 p = a + (b - a) * (i / (float)n) + (c - a) * (j / (float)n);
                        int r = Mathf.FloorToInt((p.y - y0) / cell), col = Mathf.FloorToInt((p.z - z0) / cell);
                        if (r < 0 || r >= rows || col < 0 || col >= cols) continue;
                        int at = r * cols + col;
                        if (outermost ? p.x > map[at] : p.x < map[at]) map[at] = p.x;
                    }
                }
            }
        }

        static float[] Spread(float[] map, int rows, int cols, bool outermost)
        {
            var spread = new float[map.Length];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float v = map[r * cols + c];
                    for (int dr = -1; dr <= 1; dr++)
                    {
                        int rr = r + dr;
                        if (rr < 0 || rr >= rows) continue;
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            int cc = c + dc;
                            if (cc < 0 || cc >= cols) continue;
                            float w = map[rr * cols + cc];
                            if (outermost ? w > v : w < v) v = w;
                        }
                    }
                    spread[r * cols + c] = v;
                }
            }
            return spread;
        }
    }
}
