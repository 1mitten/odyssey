#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// A pillow: a rounded box, smooth-shaded, spanning the same −0.5..0.5 unit box every other
    /// primitive stand-in does, so the placement maths in <see cref="ModuleLibrary"/> is identical
    /// for it.
    ///
    /// <para><b>Why a mesh of its own.</b> The bed's three parts were all the shared unit cube
    /// scaled three ways, and the owner's report of the result was exact: <i>"make the pillow a bit
    /// bigger and round edges like a pillow is"</i>. A box cannot be rounded by a matrix, and the
    /// renderer already keeps four such shapes for exactly this reason — <see cref="RockMesh"/>,
    /// <see cref="GroundMesh"/>, <see cref="BankMesh"/> and <see cref="WaterMesh"/> are all "the
    /// cube, but the right shape for the thing".</para>
    ///
    /// <para><b>Smooth normals, deliberately, and the only ones in the renderer.</b> Everything
    /// else here is hard-normalled because the flat-lit low-poly look depends on it. A pillow is
    /// the one thing in the game that is soft, and reading as soft is the whole of what it is
    /// being asked to do; hard facets on a rounded box look like a cut gem, which is worse than
    /// the cube it replaces.</para>
    ///
    /// <para>The shape is a superellipsoid — a sphere whose exponent is pulled towards a box.
    /// <see cref="Roundness"/> at 1 is a sphere and at 0 is a cube; the value chosen is plump
    /// rather than either, and it is the one number to turn if the pillow reads wrong.</para>
    /// </summary>
    public static class PillowMesh
    {
        /// <summary>
        /// How far from a box towards a sphere. 1 is a sphere, 0 is a cube. Lower reads firmer.
        /// </summary>
        public const float Roundness = 0.45f;

        // Enough to read as round at the size a pillow is drawn and no more: 16 x 8 is 128 quads,
        // and there is one pillow per bed rather than one per cell, so this is not a population
        // that needs watching the way ground and walls do.
        const int Slices = 16;
        const int Stacks = 8;

        static Mesh? _mesh;

        public static Mesh Mesh => _mesh != null ? _mesh : (_mesh = Build());

        static float SignedPow(float t, float e) =>
            t < 0f ? -Mathf.Pow(-t, e) : Mathf.Pow(t, e);

        static Vector3 Point(float u, float v)
        {
            // Clamped at zero, and the clamp is load-bearing. cos v cannot be negative on
            // [-pi/2, pi/2], but Mathf.Cos(-pi/2) returns -4.4e-8 in float — and SignedPow carries
            // that sign through, which mirrors the pole ring through the axis and reverses the
            // winding of every triangle touching it. ThePillowFacesOutwards caught exactly that:
            // triangle 0 faced inward by a dot of -0.99 while every triangle away from the poles
            // was correct.
            float cv = SignedPow(Mathf.Max(0f, Mathf.Cos(v)), Roundness);
            float sv = SignedPow(Mathf.Sin(v), Roundness);
            float cu = SignedPow(Mathf.Cos(u), Roundness);
            float su = SignedPow(Mathf.Sin(u), Roundness);
            return new Vector3(0.5f * cv * cu, 0.5f * sv, 0.5f * cv * su);
        }

        static Mesh Build()
        {
            int columns = Slices + 1;
            var vertices = new Vector3[columns * (Stacks + 1)];
            var uvs = new Vector2[vertices.Length];

            for (int i = 0; i <= Stacks; i++)
            {
                float fv = (float)i / Stacks;
                float v = -Mathf.PI * 0.5f + fv * Mathf.PI;
                for (int j = 0; j <= Slices; j++)
                {
                    float fu = (float)j / Slices;
                    int k = i * columns + j;
                    vertices[k] = Point(fu * 2f * Mathf.PI, v);
                    uvs[k] = new Vector2(fu, fv);
                }
            }

            var triangles = new int[Slices * Stacks * 6];
            int t = 0;
            for (int i = 0; i < Stacks; i++)
            for (int j = 0; j < Slices; j++)
            {
                int a = i * columns + j;
                int b = a + 1;
                int c = a + columns;
                int d = c + 1;

                // Wound so that Cross(b − a, c − a) — Unity's front face — points outward.
                // The same trap PrimitiveMeshes.BuildUnitCube documents at length: an inside-out
                // mesh is valid geometry and nothing reports it, so it is derived rather than
                // guessed, and ThePillowFacesOutwards holds it.
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
            }

            var mesh = new Mesh { name = "Odyssey/Pillow" };
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
