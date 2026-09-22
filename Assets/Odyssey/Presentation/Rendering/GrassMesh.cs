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
    /// <para><b>Vertex colour red is the height along the blade</b>, black at the root and full at
    /// the tip. One channel drives three things in the shader — the wind bend, the root-to-tip
    /// colour ramp and the lean towards the camera — and it is the convention the commercial
    /// stylised-grass shaders use, so a blade authored here would drop into one of those
    /// unchanged. Our meshes carry positions, normals and UV0 and nothing else, so the channel
    /// was free.</para>
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
        /// How far the tip of a blade leans out from its root, as a fraction of its length.
        ///
        /// A clump of parallel uprights reads as a brush. The splay is what makes it read as
        /// something growing, and it is also what gives the clump a silhouette wider than one
        /// blade at the board distance, where a single blade is a pixel.
        /// </summary>
        const float Splay = 0.42f;

        /// <summary>The widest a clump reaches from its own centre, in metres, before scaling.</summary>
        public const float Reach = 0.62f;

        // A blade is five vertices and three triangles: a quad from the root to the shoulder and
        // a triangle from the shoulder to the point. Three would be a spike and seven buys
        // nothing at a camera that never sees a single blade fill more than a few pixels.
        const int VerticesPerBlade = 5;
        const int TrianglesPerBlade = 3;

        /// <summary>Where the blade narrows, as a fraction of its length.</summary>
        const float ShoulderT = 0.55f;

        /// <summary>
        /// How wide the blade is at the shoulder, against its width at the root.
        ///
        /// <para>Raised from 0.55 with the width in <see cref="Build"/> (owner, 2026-09-22:
        /// thicker). A blade that narrows fast is a spike at any distance the eye can resolve it,
        /// so thickening the root without thickening the shoulder only makes the spike start
        /// wider.</para>
        /// </summary>
        const float ShoulderWidth = 0.62f;

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
                float length = Mathf.Lerp(0.34f, 0.72f, Unit(variant, b, 37u));
                // 45-75 mm until the owner saw it (2026-09-22: thicker). A blade at 2.5 m a
                // cell and a camera tens of metres up is a pixel or two wide, so the honest
                // width is whatever reads rather than whatever a real blade measures.
                float width = Mathf.Lerp(0.075f, 0.115f, Unit(variant, b, 53u));
                float splay = Splay * Mathf.Lerp(0.6f, 1.25f, Unit(variant, b, 71u));

                var outward = new Vector3(Mathf.Cos(bearing), 0f, Mathf.Sin(bearing));
                Vector3 across = Vector3.Cross(Vector3.up, outward);

                Vector3 basePoint = outward * root;
                Vector3 tip = basePoint + Vector3.up * length + outward * (length * splay);

                // The blade's own face normal: perpendicular to the blade and to its width. Taken
                // before the lift so the lift has something honest to lean away from.
                Vector3 along = (tip - basePoint).normalized;
                Vector3 face = Vector3.Cross(along, across).normalized;
                if (Vector3.Dot(face, outward) < 0f) face = -face;
                Vector3 normal = Vector3.Slerp(face, Vector3.up, NormalLift).normalized;

                int v = b * VerticesPerBlade;

                Vector3 shoulder = Vector3.Lerp(basePoint, tip, ShoulderT);
                float shoulderHalf = width * 0.5f * ShoulderWidth;
                float rootHalf = width * 0.5f;

                vertices[v + 0] = basePoint - across * rootHalf;
                vertices[v + 1] = basePoint + across * rootHalf;
                vertices[v + 2] = shoulder - across * shoulderHalf;
                vertices[v + 3] = shoulder + across * shoulderHalf;
                vertices[v + 4] = tip;

                uvs[v + 0] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(0f, ShoulderT);
                uvs[v + 3] = new Vector2(1f, ShoulderT);
                uvs[v + 4] = new Vector2(0.5f, 1f);

                // Red is the height along the blade. Green carries a per-blade constant so the
                // shader can give neighbouring blades different wind phases without needing a
                // second channel or a per-instance array — a clump that sways as one rigid object
                // is the thing that makes cheap grass look cheap.
                float phase = Unit(variant, b, 97u);
                colours[v + 0] = new Color(0f, phase, 0f, 1f);
                colours[v + 1] = new Color(0f, phase, 0f, 1f);
                colours[v + 2] = new Color(ShoulderT, phase, 0f, 1f);
                colours[v + 3] = new Color(ShoulderT, phase, 0f, 1f);
                colours[v + 4] = new Color(1f, phase, 0f, 1f);

                for (int i = 0; i < VerticesPerBlade; i++) normals[v + i] = normal;

                // Wound so the face side is the front. The shader draws both sides — a blade seen
                // from behind is half the meadow at any given moment — but the winding still has
                // to be consistent or the front face is the far one and the depth passes, which
                // do cull, keep the wrong surface.
                int t = b * TrianglesPerBlade * 3;
                triangles[t + 0] = v + 0; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
                triangles[t + 6] = v + 2; triangles[t + 7] = v + 4; triangles[t + 8] = v + 3;
            }

            var mesh = new Mesh { name = "Odyssey/GrassClump" + variant };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
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

        /// <summary>
        /// The furthest a vertex can be moved by wind and camera lean together, in metres.
        ///
        /// Matched by hand to the shader's own clamps rather than derived from them, because the
        /// two live in different languages; <c>GrassMeshTests</c> is what stops them drifting.
        /// </summary>
        public const float MaxSway = 0.35f;

        /// <summary>A stable value in [0, 1) for a variant, a blade and a salt.</summary>
        static float Unit(int variant, int blade, uint salt) =>
            (GroundScatter.Hash(variant, blade, salt) & 0xFFFFFFu) * (1f / 0x1000000);
    }
}
