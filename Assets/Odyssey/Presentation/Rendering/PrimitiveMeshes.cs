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

                triangles[f * 6 + 0] = f * 4 + 0;
                triangles[f * 6 + 1] = f * 4 + 1;
                triangles[f * 6 + 2] = f * 4 + 2;
                triangles[f * 6 + 3] = f * 4 + 0;
                triangles[f * 6 + 4] = f * 4 + 2;
                triangles[f * 6 + 5] = f * 4 + 3;
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
    }
}
