#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// One filled sandbag (design 50 §7a-bis, research <c>e-12</c>): a tamped, flattened pillow with
    /// one sewn end and one choked, tied end, spanning the −0.5..0.5 unit box every primitive
    /// stand-in does. X is the bag's length, Y its height and Z its width; the caller scales the box
    /// to the bag's size in metres, so the proportions here are only the <em>shape</em>.
    ///
    /// <para><b>Why a mesh of its own.</b> The first build drew the sandbags as scaled boxes and
    /// the owner's report was exact: <i>"the sandbags need to look like sandbags"</i>. What makes a
    /// wall read as sandbags from the play camera is the bag: a scalloped top, one hump per bag, and
    /// a dark groove wherever two rounded bags meet. A box scaled by a matrix has neither, which is
    /// the pillow's reason again (<see cref="PillowMesh"/>).</para>
    ///
    /// <para><b>The shape.</b> A tube round the X axis. Each ring is a superellipse in Y and Z,
    /// squarer below than above, because a bag tamped onto the course beneath it is flat underneath
    /// and domed on top. The width and height of the rings follow <see cref="Profile"/>: a rounded
    /// sewn end at −X, a full body, then the gathered neck and a small ear of cloth at +X, drooping
    /// a little, where the bag is tied. Both ends close to a point, as the pillow's poles do.</para>
    ///
    /// <para><b>Smooth normals</b>, as the pillow's and for the same reason: a bag is soft, and
    /// hard facets on a rounded tube read as a pipe. 12 round and 9 bands is 216 triangles; a
    /// straight cell draws about thirty bags, all in one instanced bucket per shade.</para>
    /// </summary>
    public static class SandbagMesh
    {
        /// <summary>How far each ring is from a rectangle towards an ellipse, above the bag's middle. 1 is an ellipse.</summary>
        public const float TopRoundness = 0.6f;

        /// <summary>The same below: squarer, since the bag is tamped onto the one beneath.</summary>
        public const float BottomRoundness = 0.35f;

        const int Around = 12;

        // (t along the bag from the sewn end at 0 to the tie at 1, width, height, how far the axis
        // droops), width and height as fractions of the full body. INVENTED to read as a bag at
        // the size one is drawn; e-12 findings 3 and 4 are the shape they were drawn from.
        static readonly Vector4[] Profile =
        {
            new Vector4(0.00f, 0.00f, 0.00f, 0.00f),
            new Vector4(0.05f, 0.78f, 0.80f, 0.00f),
            new Vector4(0.15f, 0.96f, 0.97f, 0.00f),
            new Vector4(0.32f, 1.00f, 1.00f, 0.00f),
            new Vector4(0.55f, 1.00f, 1.00f, 0.00f),
            new Vector4(0.72f, 0.94f, 0.88f, -0.02f),
            new Vector4(0.84f, 0.62f, 0.50f, -0.10f),
            new Vector4(0.90f, 0.40f, 0.30f, -0.16f),
            new Vector4(0.96f, 0.46f, 0.20f, -0.20f),
            new Vector4(1.00f, 0.00f, 0.00f, -0.20f),
        };

        static Mesh? _mesh;

        public static Mesh Mesh => _mesh != null ? _mesh : (_mesh = Build());

        /// <summary>
        /// The point on the bag's axis at <paramref name="x"/> in the unit box: what "outward" is
        /// measured from, for a tube whose tied end droops below the middle.
        /// </summary>
        public static Vector3 AxisAt(float x)
        {
            float t = Mathf.Clamp01(x + 0.5f);
            for (int i = 1; i < Profile.Length; i++)
            {
                if (t > Profile[i].x) continue;
                float f = Mathf.InverseLerp(Profile[i - 1].x, Profile[i].x, t);
                return new Vector3(x, 0.5f * Mathf.Lerp(Profile[i - 1].w, Profile[i].w, f), 0f);
            }
            return new Vector3(x, 0.5f * Profile[Profile.Length - 1].w, 0f);
        }

        static float SignedPow(float t, float e) =>
            t < 0f ? -Mathf.Pow(-t, e) : Mathf.Pow(t, e);

        static Mesh Build()
        {
            int columns = Around + 1;
            int rings = Profile.Length;
            var vertices = new Vector3[columns * rings];
            var uvs = new Vector2[vertices.Length];

            for (int i = 0; i < rings; i++)
            {
                Vector4 ring = Profile[i];
                float x = ring.x - 0.5f;
                float halfWidth = 0.5f * ring.y, halfHeight = 0.5f * ring.z, axis = 0.5f * ring.w;
                for (int j = 0; j <= Around; j++)
                {
                    // The seam column repeats the first so the UVs close; the angle is taken modulo
                    // a turn so the two are the same point to the bit, and the normals meet.
                    float theta = (j % Around) * (2f * Mathf.PI / Around);
                    float s = Mathf.Sin(theta), c = Mathf.Cos(theta);
                    float e = s >= 0f ? TopRoundness : BottomRoundness;
                    int k = i * columns + j;
                    vertices[k] = new Vector3(x, axis + halfHeight * SignedPow(s, e), halfWidth * SignedPow(c, e));
                    uvs[k] = new Vector2((float)j / Around, ring.x);
                }
            }

            var triangles = new int[Around * (rings - 1) * 6];
            int t = 0;
            for (int i = 0; i < rings - 1; i++)
            for (int j = 0; j < Around; j++)
            {
                int a = i * columns + j;
                int b = a + 1;
                int c = a + columns;
                int d = c + 1;

                // θ runs from +Z towards +Y and i along +X, so Cross(c − a, b − a) is +X × +Y, which
                // points out of the +Z side the ring starts on — wound outward, the pillow's order.
                // Derived rather than guessed, and TheSandbagFacesOutwards holds it.
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
            }

            var mesh = new Mesh { name = "Odyssey/Sandbag" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(markNoLongerReadable: false);
            return mesh;
        }
    }
}
