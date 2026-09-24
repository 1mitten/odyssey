#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// How a sheathed weapon sits against the body it hangs on, measured on the drawn meshes
    /// (design 33 §9c).
    /// </summary>
    public struct SheathGap
    {
        /// <summary>
        /// The signed distance from the weapon's surface to the nearest surface of the body that
        /// is not an arm, in metres: positive clear of it, negative inside it.
        /// </summary>
        public readonly float Gap;

        /// <summary>How high the weapon's nearest point is above the left hip joint, in metres.</summary>
        public readonly float GapAbove;

        /// <summary>How deep the deepest weapon point is inside the body, in metres; nought when none is.</summary>
        public readonly float Depth;

        /// <summary>How many sampled weapon points are more than <see cref="SheathGauge.InsideTolerance"/> inside the body.</summary>
        public readonly int Inside;

        /// <summary>How many points of the weapon's surface were sampled.</summary>
        public readonly int Samples;

        /// <summary>The unsigned distance from the weapon to the nearest arm surface, in metres; infinite with none near.</summary>
        public readonly float ArmGap;

        /// <summary>The weapon's long axis from straight down, in degrees.</summary>
        public readonly float Lean;

        /// <summary>How far the top of the weapon is above the left hip joint, in metres.</summary>
        public readonly float TopAboveHip;

        /// <summary>How far the top of the weapon is above the pelvis bone, in metres.</summary>
        public readonly float TopAbovePelvis;

        /// <summary>The figure's drawn height, in metres, for reading the others as fractions.</summary>
        public readonly float Height;

        /// <summary>How far the lowest point of the weapon is above the floor the figure stands on, in metres.</summary>
        public float BottomAboveFloor;

        /// <summary>The weapon point that set <see cref="Gap"/>, the skin point nearest it and the skin's normal there, in world space.</summary>
        public Vector3 Nearest;
        public Vector3 NearestOn;
        public Vector3 NearestNormal;

        public SheathGap(float gap, float gapAbove, float depth, int inside, int samples, float armGap,
            float lean, float topAboveHip, float topAbovePelvis, float height)
        {
            Gap = gap;
            GapAbove = gapAbove;
            Depth = depth;
            Inside = inside;
            Samples = samples;
            ArmGap = armGap;
            Lean = lean;
            TopAboveHip = topAboveHip;
            TopAbovePelvis = topAbovePelvis;
            Height = height;
            BottomAboveFloor = 0f;
            Nearest = NearestOn = NearestNormal = Vector3.zero;
        }

        public override string ToString() =>
            $"gap {Gap * 100f,6:F1} cm at {GapAbove * 100f,6:F1} cm above the hip, " +
            $"inside {Inside,4}/{Samples,-5} (deepest {Depth * 100f,4:F1} cm), " +
            $"arm {(float.IsInfinity(ArmGap) ? "  none" : (ArmGap * 100f).ToString("F1").PadLeft(6))} cm, " +
            $"lean {Lean,5:F1} deg, top {TopAboveHip * 100f,5:F1} cm above the hip " +
            $"({TopAbovePelvis * 100f,5:F1} above the pelvis), bottom {BottomAboveFloor * 100f,5:F1} cm off the floor, height {Height:F2} m";
    }

    /// <summary>
    /// Measures a sheathed weapon against the drawn body: the posed skin baked, split into the arms
    /// and everything else by nearest bone segment (as the hip's relief is, because a mesh not
    /// marked readable gives up no skin weights in a player), and every sampled point of the
    /// weapon's surface taken to its nearest body triangle. A point with skin further out than it
    /// — a ray cast outward from it meets the body — is inside, and reads as negative by how far
    /// the skin reaches past it, so a weapon through the thigh or under a skirt is not "touching".
    /// (The nearest face's normal was tried first and read the lower edge of a jacket, an open
    /// boundary, as ten centimetres of thigh.)
    ///
    /// <para>A diagnostic, for the tests and the contact sheet: it bakes the skin and walks every
    /// weapon point against every triangle near it, which is far too much for a frame.</para>
    /// </summary>
    public static class SheathGauge
    {
        /// <summary>How far inside the body a weapon point must be to count as inside, in metres: a skin's own facets.</summary>
        public const float InsideTolerance = 0.002f;

        /// <summary>How finely the weapon's surface is sampled, in metres.</summary>
        public const float Spacing = 0.01f;

        /// <summary>The bones the body is split by, in world space, as the figure stands now.</summary>
        public struct Bones
        {
            public Vector3 LeftThigh, LeftKnee, LeftFoot, RightThigh, RightKnee, RightFoot;
            public Vector3 Pelvis, Spine, Chest;
            public Vector3 LeftShoulder, LeftElbow, LeftWrist, RightShoulder, RightElbow, RightWrist;

            /// <summary>
            /// The finger tips, where the rig maps fingers: the hand is a fan from the wrist to each,
            /// not a straight line on from the forearm. A hanging hand curls its fingers towards
            /// the thigh, and a line on from the forearm left them nearer the thigh than the hand,
            /// so the fingers were mapped as hip and every weapon was hung outside them.
            /// </summary>
            public Vector3[]? LeftTips, RightTips;

            /// <summary>The figure's height, which the limbs' thicknesses below are fractions of.</summary>
            public float Height;

            /// <summary>
            /// How thick each part is, as a fraction of the figure's height: a point belongs to the
            /// part whose surface, not whose bone, it is nearest. By bone alone, the side of the
            /// waist is nearer the hanging elbow than the spine and was mapped as arm, and the
            /// fingers nearer the thigh than the hand. Rough, and only ever compared with each
            /// other.
            /// </summary>
            public const float TorsoThickness = 0.065f, ThighThickness = 0.045f, ShinThickness = 0.03f,
                UpperArmThickness = 0.022f, ForearmThickness = 0.018f, HandThickness = 0.01f;

            public bool IsArm(Vector3 p)
            {
                float h = Height > 0.01f ? Height : 2f;
                float arm = Mathf.Min(Arm(p, LeftShoulder, LeftElbow, LeftWrist, LeftTips, h),
                    Arm(p, RightShoulder, RightElbow, RightWrist, RightTips, h));
                float body = Mathf.Min(
                    Mathf.Min(Surface(p, LeftThigh, LeftKnee, ThighThickness * h), Surface(p, LeftKnee, LeftFoot, ShinThickness * h)),
                    Mathf.Min(Surface(p, RightThigh, RightKnee, ThighThickness * h), Surface(p, RightKnee, RightFoot, ShinThickness * h)));
                body = Mathf.Min(body, Mathf.Min(Surface(p, Pelvis, Spine, TorsoThickness * h), Surface(p, Spine, Chest, TorsoThickness * h)));
                return arm < body;
            }

            static float Arm(Vector3 p, Vector3 shoulder, Vector3 elbow, Vector3 wrist, Vector3[]? tips, float h)
            {
                float arm = Mathf.Min(Surface(p, shoulder, elbow, UpperArmThickness * h), Surface(p, elbow, wrist, ForearmThickness * h));
                if (tips == null || tips.Length == 0)
                    return Mathf.Min(arm, Surface(p, wrist, wrist + (wrist - elbow) * 0.6f, HandThickness * h));
                for (int i = 0; i < tips.Length; i++) arm = Mathf.Min(arm, Surface(p, wrist, tips[i], HandThickness * h));
                return arm;
            }

            /// <summary>How far a point is from the surface of a round limb about a bone.</summary>
            static float Surface(Vector3 p, Vector3 a, Vector3 b, float thickness) =>
                Mathf.Sqrt(SegmentDistanceSq(p, a, b)) - thickness;
        }

        /// <summary>A triangle soup in world space with outward face normals and bounding spheres.</summary>
        sealed class Soup
        {
            public readonly List<Vector3> A = new List<Vector3>(), B = new List<Vector3>(), C = new List<Vector3>();
            public readonly List<Vector3> Normal = new List<Vector3>();
            public readonly List<Vector3> NA = new List<Vector3>(), NB = new List<Vector3>(), NC = new List<Vector3>();
            public readonly List<Vector3> Centre = new List<Vector3>();
            public readonly List<float> Radius = new List<float>();

            public void Add(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-14f) return;
                n.Normalize();
                Vector3 outward = na + nb + nc;
                if (outward.sqrMagnitude > 1e-12f && Vector3.Dot(n, outward) < 0f) n = -n;
                Vector3 centre = (a + b + c) / 3f;
                A.Add(a);
                B.Add(b);
                C.Add(c);
                NA.Add(na.sqrMagnitude > 1e-12f ? na : n);
                NB.Add(nb.sqrMagnitude > 1e-12f ? nb : n);
                NC.Add(nc.sqrMagnitude > 1e-12f ? nc : n);
                Normal.Add(n);
                Centre.Add(centre);
                Radius.Add(Mathf.Sqrt(Mathf.Max((a - centre).sqrMagnitude,
                    Mathf.Max((b - centre).sqrMagnitude, (c - centre).sqrMagnitude))));
            }

            public int Count => A.Count;

            // The triangles bucketed by where they fall across a direction, for Beyond.
            Dictionary<long, List<int>>? _buckets;
            Vector3 _u, _v;
            const float BucketSize = 0.02f;

            static long Key(int a, int b) => ((long)a << 32) ^ (uint)b;

            /// <summary>Bucket the triangles by their extent across a direction, spanned by u and v.</summary>
            public void Index(Vector3 u, Vector3 v)
            {
                _u = u;
                _v = v;
                _buckets = new Dictionary<long, List<int>>();
                for (int t = 0; t < A.Count; t++)
                {
                    float u0 = Mathf.Min(Vector3.Dot(A[t], u), Mathf.Min(Vector3.Dot(B[t], u), Vector3.Dot(C[t], u)));
                    float u1 = Mathf.Max(Vector3.Dot(A[t], u), Mathf.Max(Vector3.Dot(B[t], u), Vector3.Dot(C[t], u)));
                    float v0 = Mathf.Min(Vector3.Dot(A[t], v), Mathf.Min(Vector3.Dot(B[t], v), Vector3.Dot(C[t], v)));
                    float v1 = Mathf.Max(Vector3.Dot(A[t], v), Mathf.Max(Vector3.Dot(B[t], v), Vector3.Dot(C[t], v)));
                    for (int a = Mathf.FloorToInt(u0 / BucketSize); a <= Mathf.FloorToInt(u1 / BucketSize); a++)
                    {
                        for (int b = Mathf.FloorToInt(v0 / BucketSize); b <= Mathf.FloorToInt(v1 / BucketSize); b++)
                        {
                            long key = Key(a, b);
                            if (!_buckets.TryGetValue(key, out List<int>? list)) _buckets[key] = list = new List<int>();
                            list.Add(t);
                        }
                    }
                }
            }

            /// <summary>
            /// How far the skin reaches beyond a point along <paramref name="direction"/> (at right
            /// angles to the plane <see cref="Index"/> was given): the furthest crossing of a ray
            /// from the point that way, or nought when the ray meets no skin. A point on the
            /// outside of the body has nothing of the body further out; a point in the thigh, or
            /// under a skirt, has.
            /// </summary>
            public float Beyond(Vector3 p, Vector3 direction)
            {
                if (_buckets == null) return 0f;
                long key = Key(Mathf.FloorToInt(Vector3.Dot(p, _u) / BucketSize), Mathf.FloorToInt(Vector3.Dot(p, _v) / BucketSize));
                if (!_buckets.TryGetValue(key, out List<int>? list)) return 0f;
                float furthest = 0f;
                for (int i = 0; i < list.Count; i++)
                {
                    int t = list[i];
                    if (RayHits(p, direction, A[t], B[t], C[t], out float along) && along > furthest) furthest = along;
                }
                return furthest;
            }

            /// <summary>Möller–Trumbore, either face, forward of the origin only.</summary>
            static bool RayHits(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float along)
            {
                along = 0f;
                Vector3 e1 = b - a, e2 = c - a;
                Vector3 h = Vector3.Cross(direction, e2);
                float det = Vector3.Dot(e1, h);
                if (Mathf.Abs(det) < 1e-12f) return false;
                float inv = 1f / det;
                Vector3 s = origin - a;
                float u = inv * Vector3.Dot(s, h);
                if (u < 0f || u > 1f) return false;
                Vector3 q = Vector3.Cross(s, e1);
                float v = inv * Vector3.Dot(direction, q);
                if (v < 0f || u + v > 1f) return false;
                along = inv * Vector3.Dot(e2, q);
                return along > 0f;
            }

            /// <summary>The nearest triangle to a point: its index, the distance and the sign. -1 with none.</summary>
            public int Nearest(Vector3 p, out float distance, out bool inside) => Nearest(p, out distance, out inside, out _, out _);

            public int Nearest(Vector3 p, out float distance, out bool inside, out Vector3 on, out Vector3 normal)
            {
                int best = -1;
                float bestSq = float.MaxValue;
                Vector3 bestQ = default;
                for (int t = 0; t < A.Count; t++)
                {
                    float reach = Mathf.Sqrt(bestSq) + Radius[t];
                    if (bestSq < float.MaxValue && (p - Centre[t]).sqrMagnitude > reach * reach) continue;
                    Vector3 q = ClosestOnTriangle(p, A[t], B[t], C[t]);
                    float d = (p - q).sqrMagnitude;
                    if (d < bestSq)
                    {
                        bestSq = d;
                        best = t;
                        bestQ = q;
                    }
                }
                distance = best < 0 ? float.PositiveInfinity : Mathf.Sqrt(bestSq);
                // Inside by the skin's own smooth normal where the nearest point lies, which an
                // edge or a corner shared by two faces cannot fool the way one face's normal can;
                // and never deeper than a limb is thick.
                inside = false;
                on = bestQ;
                normal = Vector3.zero;
                if (best >= 0)
                {
                    Vector3 w = Barycentric(bestQ, A[best], B[best], C[best]);
                    normal = NA[best] * w.x + NB[best] * w.y + NC[best] * w.z;
                }
                return best;
            }

            static Vector3 Barycentric(Vector3 q, Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 v0 = b - a, v1 = c - a, v2 = q - a;
                float d00 = Vector3.Dot(v0, v0), d01 = Vector3.Dot(v0, v1), d11 = Vector3.Dot(v1, v1);
                float d20 = Vector3.Dot(v2, v0), d21 = Vector3.Dot(v2, v1);
                float denom = d00 * d11 - d01 * d01;
                if (Mathf.Abs(denom) < 1e-14f) return new Vector3(1f / 3f, 1f / 3f, 1f / 3f);
                float v = (d11 * d20 - d01 * d21) / denom, u = (d00 * d21 - d01 * d20) / denom;
                return new Vector3(1f - v - u, v, u);
            }
        }

        /// <summary>
        /// Measure a weapon (its prop's transform, as drawn now) against a figure's skins (as posed
        /// now). <paramref name="hipY"/> is the left hip joint's height, <paramref name="pelvisY"/>
        /// the pelvis bone's.
        /// </summary>
        public static SheathGap Measure(SkinnedMeshRenderer[] skins, in Bones bones, Transform prop,
            Vector3 outward, Vector3 up, Vector3 forward, float hipY, float pelvisY, float floorY, float height)
        {
            List<Vector3> samples = Samples(prop, Spacing, out Vector3 axis);
            if (samples.Count == 0)
                return new SheathGap(float.PositiveInfinity, 0f, 0f, 0, 0, float.PositiveInfinity, 0f, 0f, 0f, height);

            Bounds near = new Bounds(samples[0], Vector3.zero);
            float top = float.MinValue, bottom = float.MaxValue;
            for (int i = 0; i < samples.Count; i++)
            {
                near.Encapsulate(samples[i]);
                float y = Vector3.Dot(samples[i], up);
                if (y > top) top = y;
                if (y < bottom) bottom = y;
            }
            near.Expand(0.3f);

            var body = new Soup();
            var arms = new Soup();
            Bake(skins, bones, near, body, arms);
            body.Index(up, forward);

            float gap = float.PositiveInfinity, gapAbove = 0f, depth = 0f, armGap = float.PositiveInfinity;
            int inside = 0;
            Vector3 nearest = default, nearestOn = default, nearestNormal = default;
            for (int i = 0; i < samples.Count; i++)
            {
                Vector3 p = samples[i];
                if (body.Nearest(p, out float d, out _, out Vector3 on, out Vector3 normal) >= 0)
                {
                    // Inside when there is skin further out than the point: the thigh it is in,
                    // or the skirt it is under. How far is how deep.
                    float beyond = body.Beyond(p, outward);
                    bool within = beyond > InsideTolerance;
                    float signed = within ? -beyond : d;
                    if (signed < gap)
                    {
                        gap = signed;
                        gapAbove = Vector3.Dot(p, up) - hipY;
                        nearest = p;
                        nearestOn = on;
                        nearestNormal = normal;
                    }
                    if (within) inside++;
                    if (within && beyond > depth) depth = beyond;
                }
                if (arms.Nearest(p, out float a, out _) >= 0 && a < armGap) armGap = a;
            }

            float lean = Mathf.Acos(Mathf.Clamp01(Mathf.Abs(Vector3.Dot(axis.normalized, up)))) * Mathf.Rad2Deg;
            return new SheathGap(gap, gapAbove, depth, inside, samples.Count, armGap, lean,
                top - hipY, top - pelvisY, height)
            {
                BottomAboveFloor = bottom - floorY,
                Nearest = nearest,
                NearestOn = nearestOn,
                NearestNormal = nearestNormal,
            };
        }

        /// <summary>
        /// Every drawn skin's triangles near <paramref name="near"/>, baked as posed and split into
        /// the arms and the rest; faces turned outward by the baked vertex normals.
        /// </summary>
        static void Bake(SkinnedMeshRenderer[] skins, in Bones bones, Bounds near, Soup body, Soup arms)
        {
            Mesh? baked = null;
            for (int s = 0; s < skins.Length; s++)
            {
                SkinnedMeshRenderer skin = skins[s];
                if (skin == null || !skin.enabled || !skin.gameObject.activeInHierarchy || skin.sharedMesh == null) continue;
                baked ??= new Mesh { name = "Odyssey/SheathGauge" };
                skin.BakeMesh(baked, useScale: true);
                Vector3[] vertices = baked.vertices;
                Vector3[] normals = baked.normals;
                int[] triangles = baked.triangles;
                Transform at = skin.transform;
                var world = new Vector3[vertices.Length];
                var worldNormals = new Vector3[vertices.Length];
                for (int v = 0; v < vertices.Length; v++)
                {
                    world[v] = at.TransformPoint(vertices[v]);
                    worldNormals[v] = normals.Length == vertices.Length ? at.TransformDirection(normals[v]) : Vector3.zero;
                }
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    int i0 = triangles[t], i1 = triangles[t + 1], i2 = triangles[t + 2];
                    Vector3 a = world[i0], b = world[i1], c = world[i2];
                    if (!near.Contains(a) && !near.Contains(b) && !near.Contains(c)) continue;
                    (bones.IsArm((a + b + c) / 3f) ? arms : body).Add(a, b, c,
                        worldNormals[i0], worldNormals[i1], worldNormals[i2]);
                }
            }
            if (baked != null) Object.DestroyImmediate(baked);
        }

        /// <summary>
        /// Every drawn skin's triangles, baked as posed, three world points each, split into the
        /// arms and the rest by nearest bone segment. For <see cref="SheathSurface.Map"/>.
        /// </summary>
        public static void BakeTriangles(SkinnedMeshRenderer[] skins, in Bones bones, List<Vector3> body, List<Vector3> arms) =>
            BakeTriangles(skins, bones, body, arms, Vector3.zero, Vector3.zero, Vector3.zero, float.NegativeInfinity, float.PositiveInfinity);

        /// <summary>
        /// The same, keeping only triangles with a corner on the <paramref name="outward"/> side of
        /// <paramref name="origin"/> and between the heights <paramref name="low"/> and
        /// <paramref name="high"/> above it: the side of the body a sheath hangs on, which is a
        /// third of the skin and spares classifying the rest.
        /// </summary>
        public static void BakeTriangles(SkinnedMeshRenderer[] skins, in Bones bones, List<Vector3> body, List<Vector3> arms,
            Vector3 origin, Vector3 outward, Vector3 up, float low, float high)
        {
            bool crop = outward != Vector3.zero;
            Mesh? baked = null;
            for (int s = 0; s < skins.Length; s++)
            {
                SkinnedMeshRenderer skin = skins[s];
                if (skin == null || !skin.enabled || !skin.gameObject.activeInHierarchy || skin.sharedMesh == null) continue;
                baked ??= new Mesh { name = "Odyssey/SheathSurface" };
                skin.BakeMesh(baked, useScale: true);
                Vector3[] vertices = baked.vertices;
                int[] triangles = baked.triangles;
                Transform at = skin.transform;
                var world = new Vector3[vertices.Length];
                for (int v = 0; v < vertices.Length; v++) world[v] = at.TransformPoint(vertices[v]);
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    Vector3 a = world[triangles[t]], b = world[triangles[t + 1]], c = world[triangles[t + 2]];
                    if (crop)
                    {
                        if (Vector3.Dot(a - origin, outward) < 0f && Vector3.Dot(b - origin, outward) < 0f
                            && Vector3.Dot(c - origin, outward) < 0f)
                            continue;
                        float ya = Vector3.Dot(a - origin, up), yb = Vector3.Dot(b - origin, up), yc = Vector3.Dot(c - origin, up);
                        if (Mathf.Max(ya, Mathf.Max(yb, yc)) < low || Mathf.Min(ya, Mathf.Min(yb, yc)) > high) continue;
                    }
                    List<Vector3> into = bones.IsArm((a + b + c) / 3f) ? arms : body;
                    into.Add(a);
                    into.Add(b);
                    into.Add(c);
                }
            }
            if (baked != null) Object.DestroyImmediate(baked);
        }

        /// <summary>
        /// Points over the surface of every mesh under a prop, in world space, no further apart
        /// than <paramref name="spacing"/>; and the prop's long axis in world space.
        /// </summary>
        public static List<Vector3> Samples(Transform prop, float spacing, out Vector3 axis)
        {
            var points = new List<Vector3>();
            var local = new Bounds();
            bool any = false;
            MeshFilter[] filters = prop.GetComponentsInChildren<MeshFilter>(includeInactive: false);
            for (int f = 0; f < filters.Length; f++)
            {
                Mesh? mesh = filters[f].sharedMesh;
                if (mesh == null) continue;
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                Transform at = filters[f].transform;
                var world = new Vector3[vertices.Length];
                for (int v = 0; v < vertices.Length; v++)
                {
                    world[v] = at.TransformPoint(vertices[v]);
                    Vector3 inProp = prop.InverseTransformPoint(world[v]);
                    if (any) local.Encapsulate(inProp);
                    else { local = new Bounds(inProp, Vector3.zero); any = true; }
                }
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    SampleTriangle(points, world[triangles[t]], world[triangles[t + 1]], world[triangles[t + 2]], spacing);
                }
            }

            Vector3 e = local.extents;
            Vector3 longAxis = e.x >= e.y && e.x >= e.z ? Vector3.right : e.y >= e.z ? Vector3.up : Vector3.forward;
            axis = any ? prop.TransformDirection(longAxis) : Vector3.down;
            return points;
        }

        /// <summary>
        /// Points over a prop that a player can read, in world space, no further apart than
        /// <paramref name="spacing"/>: a mesh's own surface where it is marked readable; its
        /// measured <see cref="WeaponProfile"/> where the table has one; and otherwise the surface
        /// of its bounds, which every mesh gives up. The weapons are not readable, so in the game —
        /// and in the editor, which asks the same question — they are their profiles.
        /// </summary>
        public static List<Vector3> HullPoints(Transform prop, float spacing)
        {
            var points = new List<Vector3>();
            MeshFilter[] filters = prop.GetComponentsInChildren<MeshFilter>(includeInactive: false);
            for (int f = 0; f < filters.Length; f++)
            {
                Mesh? mesh = filters[f].sharedMesh;
                if (mesh == null) continue;
                Transform at = filters[f].transform;
                if (mesh.isReadable)
                {
                    Vector3[] vertices = mesh.vertices;
                    int[] triangles = mesh.triangles;
                    for (int t = 0; t + 2 < triangles.Length; t += 3)
                    {
                        SampleTriangle(points, at.TransformPoint(vertices[triangles[t]]),
                            at.TransformPoint(vertices[triangles[t + 1]]), at.TransformPoint(vertices[triangles[t + 2]]), spacing);
                    }
                    continue;
                }
                if (WeaponProfiles.TryGet(mesh.name, out WeaponProfile? profile))
                {
                    profile!.AddPoints(at, spacing, points);
                    continue;
                }

                Bounds box = mesh.bounds;
                for (int axis = 0; axis < 3; axis++)
                {
                    int u = (axis + 1) % 3, v = (axis + 2) % 3;
                    Vector3 size = box.size;
                    int nu = Mathf.Clamp(Mathf.CeilToInt(Scale(at, u) * size[u] / spacing), 1, 512);
                    int nv = Mathf.Clamp(Mathf.CeilToInt(Scale(at, v) * size[v] / spacing), 1, 512);
                    for (int side = 0; side < 2; side++)
                    {
                        for (int i = 0; i <= nu; i++)
                        {
                            for (int j = 0; j <= nv; j++)
                            {
                                Vector3 p = box.min;
                                p[axis] = side == 0 ? box.min[axis] : box.max[axis];
                                p[u] = box.min[u] + size[u] * i / nu;
                                p[v] = box.min[v] + size[v] * j / nv;
                                points.Add(at.TransformPoint(p));
                            }
                        }
                    }
                }
            }
            return points;
        }

        static float Scale(Transform at, int axis)
        {
            Vector3 unit = Vector3.zero;
            unit[axis] = 1f;
            return at.TransformVector(unit).magnitude;
        }

        /// <summary>
        /// The edges at the spacing, which is what catches a sliver, and the face by its area on a
        /// Halton sequence, which a long thin triangle's grid would waste.
        /// </summary>
        static void SampleTriangle(List<Vector3> points, Vector3 a, Vector3 b, Vector3 c, float spacing)
        {
            Edge(points, a, b, spacing);
            Edge(points, b, c, spacing);
            Edge(points, c, a, spacing);
            float area = 0.5f * Vector3.Cross(b - a, c - a).magnitude;
            int inner = Mathf.Min(4096, Mathf.CeilToInt(area / (spacing * spacing)));
            for (int k = 1; k <= inner; k++)
            {
                float r1 = Mathf.Sqrt(Halton(k, 2)), r2 = Halton(k, 3);
                points.Add(a * (1f - r1) + b * (r1 * (1f - r2)) + c * (r1 * r2));
            }
        }

        static void Edge(List<Vector3> points, Vector3 from, Vector3 to, float spacing)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt((to - from).magnitude / spacing), 1, 512);
            for (int i = 0; i < n; i++) points.Add(Vector3.Lerp(from, to, i / (float)n));
        }

        static float Halton(int index, int radix)
        {
            float result = 0f, f = 1f / radix;
            for (int i = index; i > 0; i /= radix, f /= radix) result += f * (i % radix);
            return result;
        }

        static float SegmentDistanceSq(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            float t = lengthSq > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / lengthSq) : 0f;
            return (a + ab * t - p).sqrMagnitude;
        }

        /// <summary>The nearest point of triangle abc to p (Ericson, <i>Real-Time Collision Detection</i> §5.1.5).</summary>
        public static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
                return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            float denom = 1f / (va + vb + vc);
            return a + ab * (vb * denom) + ac * (vc * denom);
        }
    }
}
