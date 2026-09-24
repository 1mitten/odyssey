#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The meshes the renderer owns outright, built in code.
    ///
    /// One unit cube serves every fallback module: size and position are baked into the instance
    /// matrix, so a clone with no licensed art still draws the whole world through a single mesh
    /// and gets better instancing than the art path does.
    /// </summary>
    public static class PrimitiveMeshes
    {
        static Mesh? _unitCube;

        /// <summary>A 1 m cube centred on the origin, with hard normals and per-face UVs.</summary>
        public static Mesh UnitCube => _unitCube != null ? _unitCube : (_unitCube = BuildUnitCube());

        static Mesh BuildUnitCube()
        {
            // Six independent quads: shared vertices would smooth the normals and the flat-lit
            // low-poly look depends on them staying hard.
            var normals = new[]
            {
                Vector3.forward, Vector3.back, Vector3.right,
                Vector3.left, Vector3.up, Vector3.down,
            };
            var tangentsAlong = new[]
            {
                Vector3.left, Vector3.right, Vector3.forward,
                Vector3.back, Vector3.right, Vector3.right,
            };

            var vertices = new Vector3[24];
            var meshNormals = new Vector3[24];
            var uvs = new Vector2[24];
            var triangles = new int[36];

            for (int f = 0; f < 6; f++)
            {
                Vector3 n = normals[f];
                Vector3 u = tangentsAlong[f];
                Vector3 v = Vector3.Cross(n, u);
                Vector3 centre = n * 0.5f;

                vertices[f * 4 + 0] = centre - u * 0.5f - v * 0.5f;
                vertices[f * 4 + 1] = centre - u * 0.5f + v * 0.5f;
                vertices[f * 4 + 2] = centre + u * 0.5f + v * 0.5f;
                vertices[f * 4 + 3] = centre + u * 0.5f - v * 0.5f;

                for (int i = 0; i < 4; i++) meshNormals[f * 4 + i] = n;

                uvs[f * 4 + 0] = new Vector2(0f, 0f);
                uvs[f * 4 + 1] = new Vector2(0f, 1f);
                uvs[f * 4 + 2] = new Vector2(1f, 1f);
                uvs[f * 4 + 3] = new Vector2(1f, 0f);

                // Wound 0-2-1 and 0-3-2, not 0-1-2 and 0-2-3.
                //
                // The obvious order is the wrong one here, and it was wrong for a long time
                // without ever looking like a crash. Vertices are laid out anticlockwise about
                // the outward normal, so taking them in that order builds triangles that face
                // *inward*: every primitive in the renderer was inside-out. Backface culling then
                // removed the surface the camera should see and left the far interior wall
                // showing through, lit by a vertex normal pointing away from the sun. Ground came
                // out as a dark lattice with the tile apparently only on one end of each cube,
                // which is precisely what it was.
                //
                // Nothing reported it, because an inside-out mesh is perfectly valid geometry.
                // TheCubeFacesOutwards in the presentation tests is what catches it now.
                triangles[f * 6 + 0] = f * 4 + 0;
                triangles[f * 6 + 1] = f * 4 + 2;
                triangles[f * 6 + 2] = f * 4 + 1;
                triangles[f * 6 + 3] = f * 4 + 0;
                triangles[f * 6 + 4] = f * 4 + 3;
                triangles[f * 6 + 5] = f * 4 + 2;
            }

            var mesh = new Mesh { name = "Odyssey/UnitCube" };
            mesh.vertices = vertices;
            mesh.normals = meshNormals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.UploadMeshData(markNoLongerReadable: false);
            return mesh;
        }

        static Mesh? _unitRing;

        /// <summary>
        /// A flat ring on the ground plane, outer radius 1 m, facing up: the lock-on ring under an
        /// attack order's target (design 33 §7b). Scaled in x and z by the placement to the
        /// target's footprint, so the band thickens with the ring as it snaps in — which reads as
        /// the ring arriving rather than as a line changing weight.
        ///
        /// <para>The vertex layout and the winding are <see cref="Odyssey.Hud.LockOnRing"/>'s, so
        /// the fast tier checks it faces up (<c>LockOnRingTests.TheRingFacesUp</c>) and this only
        /// copies it into a mesh.</para>
        /// </summary>
        public static Mesh UnitRing => _unitRing != null ? _unitRing : (_unitRing = BuildUnitRing());

        static Mesh BuildUnitRing()
        {
            int segments = Odyssey.Hud.LockOnRing.Segments;
            var vertices = new Vector3[segments * 2];
            var normals = new Vector3[segments * 2];
            var uvs = new Vector2[segments * 2];

            for (int i = 0; i < vertices.Length; i++)
            {
                Odyssey.Hud.LockOnRing.RingVertex(i, segments, out float x, out float z);
                vertices[i] = new Vector3(x, 0f, z);
                normals[i] = Vector3.up;
                uvs[i] = new Vector2(x * 0.5f + 0.5f, z * 0.5f + 0.5f);
            }

            var mesh = new Mesh { name = "Odyssey/UnitRing" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = Odyssey.Hud.LockOnRing.RingTriangles(segments);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(markNoLongerReadable: false);
            return mesh;
        }
    }
}
