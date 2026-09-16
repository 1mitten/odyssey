#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// Rounding off the lip of a terrace step, and the one rule that makes it affordable: the
    /// chamfer goes on the sides that are actually open and on no others.
    ///
    /// <para>Chamfer all four sides of every riser cell and each one opens a groove against the
    /// flat ground behind it — a line following every terrace, which is the same fault the rim
    /// ripple made and had to be turned off for. So there is a mesh per pattern of exposed sides.
    /// Sixteen patterns collapse to five, because turning one is free: a square footprint is
    /// unchanged by a quarter turn and the turn rides in the instance matrix the cell already
    /// had.</para>
    /// </summary>
    public class GroundChamferTests
    {
        const float Tolerance = 1e-4f;

        [SetUp]
        public void Reset() => GroundMesh.ResetLevers();

        [TearDown]
        public void Restore() => GroundMesh.ResetLevers();

        static bool Exposed(int mask, int direction) => (mask & (1 << direction)) != 0;

        /// <summary>The mask you get by drawing <paramref name="local"/> turned by a rotation.</summary>
        static int ToWorld(int local, int rotation)
        {
            int world = 0;
            for (int d = 0; d < 4; d++)
                if ((local & (1 << d)) != 0) world |= 1 << ((d + rotation) & 3);
            return world;
        }

        [Test]
        public void EveryPatternOfExposedSidesFoldsOntoOneOfFiveMeshes()
        {
            // The whole cost argument. If some pattern did not fold, it would need a mesh of its
            // own and the family would grow towards sixteen.
            var used = new HashSet<int>();

            for (int mask = 1; mask < 16; mask++)
            {
                int canonical = GroundMesh.CanonicalExposure(mask, out int rotation);

                Assert.That(GroundMesh.PatternIndex(canonical), Is.GreaterThanOrEqualTo(0),
                    $"mask {mask} folded to {canonical}, which is not a pattern any mesh is built for");

                // The round trip is the part that matters: turning the canonical mesh by the
                // rotation has to put its open sides exactly where this cell's open sides are.
                // Getting this backwards draws a chamfer on the wrong edge, which is a perfectly
                // good-looking mesh facing the wrong way — the kind of fault a screenshot hides.
                Assert.That(ToWorld(canonical, rotation), Is.EqualTo(mask),
                    $"mask {mask} does not come back from canonical {canonical} turned by {rotation}");

                Assert.That(rotation, Is.InRange(0, 3));
                used.Add(canonical);
            }

            Assert.That(used, Is.EquivalentTo(GroundMesh.ExposurePatterns),
                "some built pattern is never reached, or some reached pattern is never built");
        }

        [Test]
        public void ThePatternsAreTheFiveGenuinelyDifferentOnes()
        {
            // One side, two adjacent, two opposite, three, four. Two adjacent and two opposite are
            // not rotations of one another, which is why both have to exist.
            Assert.That(GroundMesh.ExposurePatterns.Length, Is.EqualTo(5));
            Assert.That(GroundMesh.CanonicalExposure(0b0011, out int _), Is.Not.EqualTo(
                GroundMesh.CanonicalExposure(0b0101, out int _)),
                "two adjacent sides and two opposite sides fold to the same mesh");
        }

        [Test]
        public void OnlyAnExposedEdgeIsCutBack()
        {
            // The rule the whole design rests on. An unexposed edge has solid ground against it, so
            // anything taken off it is a groove.
            foreach (int pattern in GroundMesh.ExposurePatterns)
            {
                Vector3[] crown = GroundMesh.Crown(pattern);
                Vector3[] shoulder = GroundMesh.Shoulder(pattern);

                // The middle of each edge is the honest place to ask: a corner belongs to two edges
                // and has to compromise, the middle belongs to one.
                CheckEdge(pattern, Directions.West, crown[3], shoulder[3], -0.5f, axisX: true);
                CheckEdge(pattern, Directions.East, crown[5], shoulder[5], 0.5f, axisX: true);
                CheckEdge(pattern, Directions.South, crown[1], shoulder[1], -0.5f, axisX: false);
                CheckEdge(pattern, Directions.North, crown[7], shoulder[7], 0.5f, axisX: false);
            }
        }

        static void CheckEdge(int pattern, int direction, Vector3 crown, Vector3 shoulder,
            float boundary, bool axisX)
        {
            float crownAcross = axisX ? crown.x : crown.z;
            string where = $"pattern {pattern} edge {direction}";

            if (Exposed(pattern, direction))
            {
                Assert.That(Mathf.Abs(crownAcross - boundary), Is.GreaterThan(0.01f),
                    where + " is exposed but its lip was not cut back");
                Assert.That(shoulder.y, Is.LessThan(0.5f - 0.01f),
                    where + " is exposed but its rim was not dropped");
            }
            else
            {
                Assert.That(crownAcross, Is.EqualTo(boundary).Within(Tolerance),
                    where + " is not exposed and was cut back anyway, which grooves the ground behind it");
                Assert.That(shoulder.y, Is.EqualTo(0.5f).Within(Tolerance),
                    where + " is not exposed and its rim dropped, which steps against a flat neighbour");
            }
        }

        [Test]
        public void TheChamferNeverReachesWhereAnythingStands()
        {
            // Everything in the world is drawn at the cell centre. A chamfer that reached it would
            // undercut a colonist, which is the same class of fault as a dished top.
            GroundMesh.ChamferMetres = 0.8f;      // far past anything that would be chosen

            foreach (int pattern in GroundMesh.ExposurePatterns)
            foreach (Vector3 point in GroundMesh.Crown(pattern))
            {
                Assert.That(Mathf.Abs(point.x), Is.LessThanOrEqualTo(0.5f + Tolerance));
                Assert.That(Mathf.Abs(point.z), Is.LessThanOrEqualTo(0.5f + Tolerance));
            }

            Vector3 centre = GroundMesh.Crown(0b1111)[4];
            Assert.That(centre.x, Is.EqualTo(0f).Within(Tolerance), "the chamfer moved the middle of the cell");
            Assert.That(centre.z, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(centre.y, Is.EqualTo(0.5f).Within(Tolerance), "the middle of the cell left its layer");
        }

        [Test]
        public void ZeroIsASquareLipExactlyAsBefore()
        {
            // The control the contact sheet is judged against, and a lever that cannot be returned
            // to zero is not a lever.
            GroundMesh.ChamferMetres = 0f;
            GroundMesh.Invalidate();

            foreach (int pattern in GroundMesh.ExposurePatterns)
            {
                foreach (Vector3 point in GroundMesh.Crown(pattern))
                {
                    Assert.That(Mathf.Abs(Mathf.Abs(point.x) - 0.5f) < Tolerance || Mathf.Abs(point.x) < Tolerance,
                        Is.True, $"pattern {pattern}: the crown left the lattice at zero chamfer");
                    Assert.That(point.y, Is.EqualTo(0.5f).Within(Tolerance));
                }

                foreach (Vector3 point in GroundMesh.Shoulder(pattern))
                    Assert.That(point.y, Is.EqualTo(0.5f).Within(Tolerance),
                        $"pattern {pattern}: the rim dropped at zero chamfer");
            }
        }

        [Test]
        public void EveryPatternBuildsAClosedBlockThatFacesOutwards()
        {
            // The chamfer band is stitched between two rings that are the *same points* along an
            // unexposed side, so it collapses to nothing there and to a triangle at a corner where
            // one side is cut and its neighbour is not. Emitting those as four-sided faces leaves
            // zero-length edges and counts a real edge three times. This is the test that catches
            // it, and it is the reason AddQuad fans over its distinct corners.
            foreach (int pattern in GroundMesh.ExposurePatterns)
            for (int variant = 0; variant < GroundMesh.Variants; variant++)
            {
                Mesh mesh = GroundMesh.Face(variant, pattern);
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                int[] triangles = mesh.triangles;
                var edges = new Dictionary<string, int>();

                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]];
                    Vector3 b = vertices[triangles[t + 1]];
                    Vector3 c = vertices[triangles[t + 2]];

                    Vector3 wound = Vector3.Cross(b - a, c - a);
                    Assert.That(wound.sqrMagnitude, Is.GreaterThan(1e-12f),
                        $"pattern {pattern} variant {variant} has a degenerate triangle at {a}");
                    Assert.That(Vector3.Dot(wound.normalized, normals[triangles[t]]), Is.GreaterThan(0.5f),
                        $"pattern {pattern} variant {variant} triangle {t / 3} is wound against its normal");

                    for (int e = 0; e < 3; e++)
                    {
                        string key = EdgeKey(vertices[triangles[t + e]], vertices[triangles[t + (e + 1) % 3]]);
                        edges[key] = edges.TryGetValue(key, out int n) ? n + 1 : 1;
                    }
                }

                foreach (KeyValuePair<string, int> edge in edges)
                    Assert.That(edge.Value, Is.EqualTo(2),
                        $"pattern {pattern} variant {variant} has an edge used {edge.Value} times at " +
                        $"{edge.Key}: the block is not closed");
            }
        }

        static string EdgeKey(Vector3 a, Vector3 b)
        {
            string first = $"{a.x:F3},{a.y:F3},{a.z:F3}";
            string second = $"{b.x:F3},{b.y:F3},{b.z:F3}";
            return string.CompareOrdinal(first, second) <= 0 ? first + "/" + second : second + "/" + first;
        }

        [Test]
        public void WithNoLipToCutTheFamilyCollapsesToOneBlock()
        {
            // Paying five buckets a chunk for five copies of one mesh would make turning the
            // chamfer off cost more than leaving it on, which is not a lever anybody can use.
            GroundMesh.ChamferMetres = 0f;
            GroundMesh.Invalidate();

            Assert.That(GroundMesh.FaceSlots, Is.EqualTo(GroundMesh.Variants),
                "the patterns did not collapse with the chamfer off");

            for (int slot = 0; slot < GroundMesh.FaceSlots; slot++)
                Assert.That(GroundMesh.FaceBySlot(slot), Is.Not.Null);
        }

        [Test]
        public void AFaceIsCachedPerPatternAndPerVariant()
        {
            Assert.That(GroundMesh.FaceSlots, Is.EqualTo(GroundMesh.ExposurePatterns.Length * GroundMesh.Variants));

            var seen = new HashSet<Mesh>();
            for (int slot = 0; slot < GroundMesh.FaceSlots; slot++)
            {
                Mesh mesh = GroundMesh.FaceBySlot(slot);
                Assert.That(mesh, Is.SameAs(GroundMesh.FaceBySlot(slot)), "a new mesh per call would leak one per cell");
                Assert.That(seen.Add(mesh), Is.True, $"slot {slot} resolved to a mesh another slot already has");
            }

            // And the packing is what the render model assumes: pattern-major, variants within.
            Assert.That(GroundMesh.FaceBySlot(0), Is.SameAs(GroundMesh.Face(0, GroundMesh.ExposurePatterns[0])));
            Assert.That(GroundMesh.FaceBySlot(GroundMesh.Variants),
                Is.SameAs(GroundMesh.Face(0, GroundMesh.ExposurePatterns[1])));
            Assert.That(GroundMesh.FaceBySlot(-1), Is.SameAs(GroundMesh.FaceBySlot(GroundMesh.FaceSlots - 1)),
                "negative slots must wrap");
        }

        [Test]
        public void ACutLipCostsTrianglesOnlyWhereThereIsALip()
        {
            // The cost, asserted rather than asserted about. A face is a small fraction of the
            // cells drawn, so it may be dearer; turf is nearly all of them and must not move.
            int turf = GroundMesh.Turf(0).triangles.Length / 3;
            int oneSide = GroundMesh.Face(0, 0b0001).triangles.Length / 3;
            int allFour = GroundMesh.Face(0, 0b1111).triangles.Length / 3;

            Assert.That(turf, Is.LessThanOrEqualTo(40), $"turf costs {turf} triangles on every cell drawn");
            Assert.That(oneSide, Is.LessThan(allFour), "cutting one lip costs as much as cutting four");
            Assert.That(allFour, Is.LessThanOrEqualTo(140), $"a fully exposed face costs {allFour} triangles");
        }
    }
}
