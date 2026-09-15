#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The meshes the renderer builds for itself.
    ///
    /// Worth testing precisely because a malformed mesh does not fail. An inside-out cube is
    /// valid geometry: nothing throws, nothing is logged, and the renderer reports healthy draw
    /// call and instance counts while drawing the inside of every box in the world. It cost four
    /// rounds of visual debugging before anyone rendered a picture and looked at it.
    /// </summary>
    public class PrimitiveMeshTests
    {
        [Test]
        public void TheCubeFacesOutwards()
        {
            Mesh cube = PrimitiveMeshes.UnitCube;
            Vector3[] vertices = cube.vertices;
            Vector3[] normals = cube.normals;
            int[] triangles = cube.triangles;

            Assert.That(triangles.Length, Is.EqualTo(36), "six quads, two triangles each");

            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]];
                Vector3 b = vertices[triangles[t + 1]];
                Vector3 c = vertices[triangles[t + 2]];

                // How Unity decides which way a triangle faces, given its winding.
                Vector3 wound = Vector3.Cross(b - a, c - a).normalized;

                // What the shader will light it as, which is the vertex normal.
                Vector3 shaded = normals[triangles[t]];

                Assert.That(Vector3.Dot(wound, shaded), Is.GreaterThan(0.99f),
                    $"triangle {t / 3} is wound against its own normal: it faces {wound} " +
                    $"but is shaded as {shaded}, so it is culled from the side it should be seen " +
                    "from and lit from the side it should not");
            }
        }

        [Test]
        public void EveryFaceCarriesAWholeTexture()
        {
            // One unit of UV per face, so a material tiled once shows exactly one copy on each
            // side of a cell. Ground depends on this to read as a tile rather than as a crop.
            Mesh cube = PrimitiveMeshes.UnitCube;
            Vector2[] uv = cube.uv;
            Assert.That(uv.Length, Is.EqualTo(24), "four corners per face, unshared");

            for (int face = 0; face < 6; face++)
            {
                var corners = new[]
                {
                    uv[face * 4 + 0], uv[face * 4 + 1], uv[face * 4 + 2], uv[face * 4 + 3],
                };
                Assert.That(corners, Does.Contain(new Vector2(0f, 0f)), $"face {face}");
                Assert.That(corners, Does.Contain(new Vector2(0f, 1f)), $"face {face}");
                Assert.That(corners, Does.Contain(new Vector2(1f, 1f)), $"face {face}");
                Assert.That(corners, Does.Contain(new Vector2(1f, 0f)), $"face {face}");
            }
        }

        [Test]
        public void TheCubeIsOneMetreAcrossAndCentred()
        {
            // Every placement matrix in the renderer assumes exactly this, so it is a contract
            // rather than a detail: sizes and offsets are baked into the instance matrix.
            Bounds bounds = PrimitiveMeshes.UnitCube.bounds;
            Assert.That((bounds.size - Vector3.one).magnitude, Is.LessThan(1e-4f), "one metre across");
            Assert.That(bounds.center.magnitude, Is.LessThan(1e-4f), "centred on the origin");
        }
    }
}
