#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// A clump of grass blades, built in code.
    ///
    /// <para><b>Why ours and not the pack's.</b> Until this existed the meadow was three Synty
    /// foliage prefabs, and <c>ChunkMesher.EnsureScatterModules</c> dropped any variant that did
    /// not resolve to real art — so a clone without <c>Assets/Synty</c>, which includes the
    /// continuous-integration runner, rendered a board of bare ground. Grass was the largest
    /// visible thing in the game that no test could see. It is also the thing the owner most
    /// wanted to look different, and a prefab's silhouette is not ours to change.</para>
    ///
    /// <para><b>Authored in metres, root at the origin.</b> Every other code-built mesh here
    /// spans the unit box and is scaled into place by the instance matrix
    /// (<see cref="PrimitiveMeshes.UnitCube"/>, <see cref="GroundMesh"/>). A clump does not: its
    /// shape *is* its size, a blade half a metre long is not a cell scaled down, and
    /// <see cref="ModuleShape.GrassClump"/>'s fallback box is one unit precisely so that the
    /// matrix the mesher builds — position, yaw, a uniform 0.7 to 1.2 — lands the clump where it
    /// stands with no constant to look up.</para>
    ///
    /// <para><b>Blades are curved, not straight</b> (owner, 2026-09-22, asking for something more
    /// like Breath of the Wild's). A straight tapered triangle reads as a spike however it is
    /// coloured; an arc that rises and bows over is the whole silhouette difference, and it is the
    /// change most likely to survive our camera, because what survives at two pixels wide is the
    /// *outline* of a clump and not anything inside it. The arc is a quadratic Bézier from root to
    /// tip, sampled at <see cref="Rows"/> stations, with the control point directly above the root
    /// so the blade leaves the ground vertical and does its bending late — a control point out at
    /// the side gives a hoop, which is the usual way this goes wrong.</para>
    ///
    /// <para><b>Vertex colour red is the height along the blade</b>, black at the root and full at
    /// the tip; green is a per-blade constant the shader uses to offset its wind phase. That one
    /// channel drives the colour ramp, the bend, the lean towards the camera and the base
    /// shading. Our meshes carry positions, normals and UV0 and nothing else, so it was free.</para>
    ///
    /// <para><b>The normals lean towards up, and that is deliberate.</b> Everywhere else in this
    /// renderer normals are hard and per-face, because the flat-lit low-poly look depends on it.
    /// A clump shaded that way is a dozen differently-angled facets and reads as a spiky grey
    /// star; leaning each blade's normal most of the way towards vertical makes the clump light
    /// as one soft mass, which is what a field of grass does. The *geometry* is still hard —
    /// nothing is smoothed across a seam. Do not "fix" this by recalculating normals.</para>
    ///
    /// <para>Everything here is a pure function of the variant index, so the same clump comes out
    /// on every machine, after every reload and after every re-mesh — the same promise
    /// <see cref="GroundScatter"/> makes about where clumps stand, for the same reason.</para>
    /// </summary>
    public static class GrassMesh
    {
        /// <summary>How many distinct clumps there are. The scatter picks between them per slot.</summary>
        public const int Variants = 3;

        /// <summary>Blades in each variant, in order. Different counts as well as different shapes.</summary>
        static readonly int[] BladesPerVariant = { 5, 4, 6 };

        /// <summary>
        /// Stations along a blade, the tip excluded: the arc is sampled at this many pairs of
        /// vertices and closed with a single point.
        ///
        /// <para>The cost is linear in it — a blade is <c>Rows * 2 + 1</c> vertices and
        /// <c>(Rows - 1) * 2 + 1</c> triangles, so three rows is seven and five against the five
        /// and three a straight blade needed.</para>
        ///
        /// <para><b>Three rather than the four this started at, because the research says the
        /// curve is nearly wasted here</b> (<c>b-botw-grass.md</c>). Breath of the Wild's own
        /// blade is a single triangle; the curved Bézier blade everybody associates with it is
        /// tutorial invention for a ground-level camera, and at one or two pixels a blade none of
        /// it survives. What does survive is the outline of a *clump*, which is ten or twenty
        /// pixels across and is visibly different bowed from spiky — so the bow is kept, at the
        /// least geometry that still reads as one. <b>Spend vertices on more blades, not better
        /// blades</b> is the rule; raising this is spending them the wrong way.</para>
        /// </summary>
        const int Rows = 3;

        const int VerticesPerBlade = Rows * 2 + 1;
        const int TrianglesPerBlade = (Rows - 1) * 2 + 1;

        /// <summary>
        /// How far the tip of a blade leans out from its root, as a fraction of its length.
        ///
        /// A clump of parallel uprights reads as a brush. The splay is what makes it read as
        /// something growing, and it is also what gives the clump a silhouette wider than one
        /// blade at the board distance, where a single blade is a pixel.
        /// </summary>
        const float Splay = 0.46f;

        /// <summary>
        /// How high the tip stands against the blade's own length, the rest having gone into the
        /// arc. A blade that arced to horizontal would be lying down; most of the length upright
        /// and the last of it leaning is a blade that has grown and then bowed.
        /// </summary>
        const float TipRise = 0.82f;

        /// <summary>Where the Bézier control point sits up the blade, directly over the root.</summary>
        const float ControlRise = 0.72f;

        /// <summary>The widest a clump reaches from its own centre, in metres, before scaling.</summary>
        public const float Reach = 0.72f;

        /// <summary>How fast the blade narrows. Below one it keeps its width and tapers late.</summary>
        const float TaperPower = 0.65f;

        /// <summary>How far the shading normal is pulled from the blade's own face towards up.</summary>
        const float NormalLift = 0.7f;

        static readonly Mesh?[] _cache = new Mesh?[Variants];

        /// <summary>The clump for a variant, built once and kept.</summary>
        public static Mesh For(int variant)
        {
            int index = ((variant % Variants) + Variants) % Variants;
            Mesh? mesh = _cache[index];
            if (mesh != null) return mesh;
            mesh = Build(index);
            _cache[index] = mesh;
            return mesh;
        }

        /// <summary>Drop the cached meshes. For tests that rebuild them; the game never calls it.</summary>
        public static void Forget()
        {
            for (int i = 0; i < Variants; i++) _cache[i] = null;
        }

        /// <summary>
        /// The library modules that draw the clumps, one per variant.
        ///
        /// <para>Both callers go through here — the board's scatter in <c>ChunkMesher</c> and the
        /// first ring of the surround in <c>TerrainSkirt</c> — because the two used to resolve the
        /// pack's tufts separately and could therefore have disagreed about how many there were.
        /// The board and the land beyond it have to be strewn with the same grass or the rim of
        /// the map is a line you can see.</para>
        ///
        /// <para>Resolution is cached inside the library by id, so calling this twice returns the
        /// same module indices and builds nothing a second time.</para>
        /// </summary>
        public static int[] Modules(ModuleLibrary library)
        {
            var modules = new int[Variants];
            for (int variant = 0; variant < Variants; variant++)
                modules[variant] = library.ResolveOwn(
                    ModuleIds.GrassClump(variant), ModuleShape.GrassClump, variant);
            return modules;
        }

        static Mesh Build(int variant)
        {
            int blades = BladesPerVariant[variant];

            var vertices = new Vector3[blades * VerticesPerBlade];
            var normals = new Vector3[blades * VerticesPerBlade];
            var uvs = new Vector2[blades * VerticesPerBlade];
            var colours = new Color[blades * VerticesPerBlade];

            // UV2 carries the vector from the blade's spine out to this vertex, in object
            // space and in metres. The shader adds a multiple of it to widen blades with
            // distance, which is the cheapest defence there is against a field of one-pixel
            // triangles shimmering — and it has to be authored here because the shader cannot
            // recover which way is across a blade from the vertex alone.
            var spread = new Vector2[blades * VerticesPerBlade];
            var triangles = new int[blades * TrianglesPerBlade * 3];

            for (int b = 0; b < blades; b++)
            {
                // Bearings are spread evenly and then jittered, rather than taken at random. An
                // even spread alone makes a rosette, which is conspicuous when a hundred clumps
                // share one board; jitter alone leaves two blades occupying the same place, which
                // wastes half the geometry of a clump that already has very little.
                float even = (b + 0.5f) / blades * Mathf.PI * 2f;
                float bearing = even + (Unit(variant, b, 11u) - 0.5f) * (Mathf.PI * 2f / blades) * 0.8f;

                float root = Mathf.Lerp(0.02f, 0.16f, Unit(variant, b, 23u));

                // Taller, since the owner asked for grass more like Breath of the Wild's
                // (2026-09-22). The arc means the *tip height* is about TipRise of this, so a
                // 0.95 m blade stands about 0.78 m — still below a colonist's knee, which is the
                // limit worth keeping while items, orders and zones are all read off the ground.
                float length = Mathf.Lerp(0.45f, 0.95f, Unit(variant, b, 37u));

                float width = Mathf.Lerp(0.075f, 0.115f, Unit(variant, b, 53u));
                float splay = Splay * Mathf.Lerp(0.6f, 1.25f, Unit(variant, b, 71u));

                var outward = new Vector3(Mathf.Cos(bearing), 0f, Mathf.Sin(bearing));
                Vector3 across = Vector3.Cross(Vector3.up, outward);

                Vector3 a = outward * root;
                Vector3 c = a + Vector3.up * (length * TipRise) + outward * (length * splay);
                Vector3 control = a + Vector3.up * (length * ControlRise);

                int v = b * VerticesPerBlade;
                float phase = Unit(variant, b, 97u);

                for (int row = 0; row < Rows; row++)
                {
                    float t = row / (float)Rows;
                    Vector3 point = Bezier(a, control, c, t);
                    Vector3 tangent = BezierTangent(a, control, c, t).normalized;

                    // Taken per row rather than per blade, so a curved blade is not lit as a flat
                    // one: the face turns over as the blade bows, and that turn is most of what
                    // separates a clump from a green star at this distance.
                    Vector3 face = Vector3.Cross(tangent, across).normalized;
                    if (Vector3.Dot(face, outward) < 0f) face = -face;
                    Vector3 normal = Vector3.Slerp(face, Vector3.up, NormalLift).normalized;

                    float half = width * 0.5f * Mathf.Pow(1f - t, TaperPower);

                    vertices[v + row * 2 + 0] = point - across * half;
                    vertices[v + row * 2 + 1] = point + across * half;
                    spread[v + row * 2 + 0] = new Vector2(-across.x * half, -across.z * half);
                    spread[v + row * 2 + 1] = new Vector2(across.x * half, across.z * half);
                    normals[v + row * 2 + 0] = normal;
                    normals[v + row * 2 + 1] = normal;
                    uvs[v + row * 2 + 0] = new Vector2(0f, t);
                    uvs[v + row * 2 + 1] = new Vector2(1f, t);
                    colours[v + row * 2 + 0] = new Color(t, phase, 0f, 1f);
                    colours[v + row * 2 + 1] = new Color(t, phase, 0f, 1f);
                }

                int tip = v + Rows * 2;
                vertices[tip] = c;
                normals[tip] = normals[v + (Rows - 1) * 2];
                uvs[tip] = new Vector2(0.5f, 1f);
                spread[tip] = Vector2.zero;
                colours[tip] = new Color(1f, phase, 0f, 1f);

                // Wound so the face side is the front. The shader draws both sides — half the
                // meadow faces away at any moment — but the winding still has to be consistent or
                // the front face is the far one and the depth passes, which do cull, keep the
                // wrong surface.
                int t0 = b * TrianglesPerBlade * 3;
                for (int row = 0; row < Rows - 1; row++)
                {
                    int lo = v + row * 2;
                    int hi = lo + 2;
                    triangles[t0 + row * 6 + 0] = lo;
                    triangles[t0 + row * 6 + 1] = hi;
                    triangles[t0 + row * 6 + 2] = lo + 1;
                    triangles[t0 + row * 6 + 3] = lo + 1;
                    triangles[t0 + row * 6 + 4] = hi;
                    triangles[t0 + row * 6 + 5] = hi + 1;
                }

                int last = v + (Rows - 1) * 2;
                triangles[t0 + (Rows - 1) * 6 + 0] = last;
                triangles[t0 + (Rows - 1) * 6 + 1] = tip;
                triangles[t0 + (Rows - 1) * 6 + 2] = last + 1;
            }

            var mesh = new Mesh { name = "Odyssey/GrassClump" + variant };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.SetUVs(2, spread);
            mesh.colors = colours;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            // The wind and the lean towards the camera move vertices in the vertex shader, and
            // nothing that culls knows about them. Growing the bounds by the largest offset
            // either can produce keeps a clump at the edge of the view from vanishing as it bends
            // — a whole-clump pop, not a sliver, because a bucket is culled as one bounding box.
            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(MaxSway * 2f, MaxSway, MaxSway * 2f));
            mesh.bounds = bounds;

            mesh.UploadMeshData(markNoLongerReadable: false);
            return mesh;
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        static Vector3 BezierTangent(Vector3 a, Vector3 b, Vector3 c, float t) =>
            2f * (1f - t) * (b - a) + 2f * t * (c - b);

        /// <summary>
        /// The furthest a vertex can be moved by wind and camera lean together, in metres.
        ///
        /// <para>Matched by hand to the shader's own clamp rather than derived from it, because
        /// the two live in different languages; <c>GrassTests</c> is what stops them drifting.
        /// The arithmetic is the chord of the arc: a vertex an arm's length from the root,
        /// rotated by the cap, moves <c>2 * arm * sin(cap / 2)</c>. The shader caps the bow at
        /// <c>ODYSSEY_GRASS_MAX_BOW</c> = 0.60 rad, and the longest blade's tip is about 1.05 m
        /// from its root — 0.71 m out and 0.78 m up — so it travels at most about 0.62 m.
        /// <c>GrassTests.TheBoundsCoverTheBowTheShaderCanApply</c> does that sum against the
        /// built mesh and the shader source rather than trusting this paragraph, which is just
        /// as well: the first figure written here was 0.90 m and 0.53 m, taken from the blade's
        /// height alone with its reach forgotten.</para>
        ///
        /// <para><b>Raised from 0.35 when the bow replaced the old tip drag</b>, which moved
        /// vertices far less because it was a translation the shader clamped directly. Leaving
        /// it at 0.35 would not have looked like a bounds bug; it would have looked like clumps
        /// at the edge of the view blinking out when the wind got up.</para>
        /// </summary>
        public const float MaxSway = 0.70f;

        /// <summary>A stable value in [0, 1) for a variant, a blade and a salt.</summary>
        static float Unit(int variant, int blade, uint salt) =>
            (GroundScatter.Hash(variant, blade, salt) & 0xFFFFFFu) * (1f / 0x1000000);
    }
}
