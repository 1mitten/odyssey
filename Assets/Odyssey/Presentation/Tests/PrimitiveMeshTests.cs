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

        /// <summary>
        /// The lock-on ring (design 33 §7b) faces up, by the cube's rule: the fast tier checks the
        /// winding it is built from (<c>LockOnRingTests.TheRingFacesUp</c>), and this checks the
        /// mesh was built from it — normals up and every triangle wound to match them.
        /// </summary>
        [Test]
        public void TheRingFacesUp()
        {
            Mesh ring = PrimitiveMeshes.UnitRing;
            Vector3[] vertices = ring.vertices;
            Vector3[] normals = ring.normals;
            int[] triangles = ring.triangles;

            Assert.That(triangles.Length, Is.EqualTo(Odyssey.Hud.LockOnRing.Segments * 6));
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]];
                Vector3 b = vertices[triangles[t + 1]];
                Vector3 c = vertices[triangles[t + 2]];
                Vector3 wound = Vector3.Cross(b - a, c - a).normalized;
                Assert.That(Vector3.Dot(wound, Vector3.up), Is.GreaterThan(0.99f), $"triangle {t / 3} faces down");
                Assert.That(normals[triangles[t]], Is.EqualTo(Vector3.up));
            }

            Assert.That(ring.bounds.extents.x, Is.EqualTo(1f).Within(1e-4f), "outer radius 1 m");
            Assert.That(ring.bounds.extents.y, Is.EqualTo(0f).Within(1e-6f), "flat");
        }

        /// <summary>
        /// The pillow faces outwards too, and the same argument applies: a rounded box wound the
        /// wrong way is valid geometry that draws its own inside and reports healthy counts.
        ///
        /// <para>Checked against the outward direction rather than against a stored normal, because
        /// the pillow's normals are recalculated and smooth — so a normal that agreed with a
        /// mis-wound triangle would prove nothing. Every vertex of a superellipsoid about the
        /// origin is itself an outward direction, which is the oracle used here.</para>
        /// </summary>
        [Test]
        public void ThePillowFacesOutwards()
        {
            Mesh pillow = PillowMesh.Mesh;
            Vector3[] vertices = pillow.vertices;
            int[] triangles = pillow.triangles;

            int checkedFaces = 0;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]];
                Vector3 b = vertices[triangles[t + 1]];
                Vector3 c = vertices[triangles[t + 2]];

                Vector3 wound = Vector3.Cross(b - a, c - a);
                if (wound.sqrMagnitude < 1e-12f) continue; // degenerate sliver at a pole

                Vector3 outward = ((a + b + c) / 3f).normalized;
                if (outward.sqrMagnitude < 0.5f) continue;

                Assert.That(Vector3.Dot(wound.normalized, outward), Is.GreaterThan(0f),
                    $"triangle {t / 3} of the pillow is wound inward");
                checkedFaces++;
            }

            Assert.That(checkedFaces, Is.GreaterThan(100), "most of the pillow was actually checked");
        }

        /// <summary>
        /// The pillow fills the same −0.5..0.5 unit box every other stand-in does, so
        /// <c>ModuleLibrary</c>'s placement maths means the same thing for it.
        /// </summary>
        [Test]
        public void ThePillowFillsTheUnitBox()
        {
            Bounds bounds = PillowMesh.Mesh.bounds;

            Assert.That(bounds.min.x, Is.EqualTo(-0.5f).Within(0.01f));
            Assert.That(bounds.max.x, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(bounds.min.y, Is.EqualTo(-0.5f).Within(0.01f));
            Assert.That(bounds.max.y, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(bounds.min.z, Is.EqualTo(-0.5f).Within(0.01f));
            Assert.That(bounds.max.z, Is.EqualTo(0.5f).Within(0.01f));

            // Rounded, not a box: a cube of the same extent would have eight corners at the full
            // diagonal, and this must not. If this ever passes at the cube's distance the
            // roundness has been turned to zero and the pillow is a block again.
            float cubeCorner = new Vector3(0.5f, 0.5f, 0.5f).magnitude;
            float furthest = 0f;
            foreach (Vector3 v in PillowMesh.Mesh.vertices) furthest = Mathf.Max(furthest, v.magnitude);
            Assert.That(furthest, Is.LessThan(cubeCorner * 0.95f), "the pillow is not rounded at all");
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
