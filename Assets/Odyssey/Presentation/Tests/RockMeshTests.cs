#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The chipped stone lumps, and the one property they have to have: they tile.
    ///
    /// A lump is free to be as irregular as it likes, but the moment a vertex moves *inward* from
    /// the cell boundary it opens a crack between that cell and its neighbour — and a crack in a
    /// cliff face is a far worse fault than a flat one, because it lets daylight through the
    /// world. The rule is cheap to state and cheap to check, so it is checked rather than trusted.
    /// </summary>
    public class RockMeshTests
    {
        const float Tolerance = 1e-4f;

        [Test]
        public void NoVertexEverMovesInward()
        {
            // The whole tiling guarantee, on every variant. A vertex may push out past the cell
            // (neighbouring rock overlaps, which is invisible) and a top vertex may drop. What it
            // may never do is retreat inside the footprint.
            for (int variant = 0; variant < RockMesh.Variants; variant++)
            {
                Vector3[] top = RockMesh.Top(variant);

                for (int j = 0; j < 3; j++)
                for (int i = 0; i < 3; i++)
                {
                    Vector3 point = top[j * 3 + i];
                    string where = $"variant {variant} vertex ({i},{j}) at {point}";

                    if (i == 0) Assert.That(point.x, Is.LessThanOrEqualTo(-0.5f + Tolerance), where + " moved inward in x");
                    if (i == 2) Assert.That(point.x, Is.GreaterThanOrEqualTo(0.5f - Tolerance), where + " moved inward in x");
                    if (j == 0) Assert.That(point.z, Is.LessThanOrEqualTo(-0.5f + Tolerance), where + " moved inward in z");
                    if (j == 2) Assert.That(point.z, Is.GreaterThanOrEqualTo(0.5f - Tolerance), where + " moved inward in z");
                }
            }
        }

        [Test]
        public void AMidRimVertexDoesNotSlideAlongItsOwnEdge()
        {
            // Sliding it looks harmless — it stays on the seam plane — but it drags the facets
            // that share it off the corner they meet at and pinches a sliver of daylight into
            // the joint. Only the one true interior vertex is free to wander.
            for (int variant = 0; variant < RockMesh.Variants; variant++)
            {
                Vector3[] top = RockMesh.Top(variant);

                Assert.That(top[1].x, Is.EqualTo(0f).Within(Tolerance), $"variant {variant}: -z mid-rim slid along x");
                Assert.That(top[7].x, Is.EqualTo(0f).Within(Tolerance), $"variant {variant}: +z mid-rim slid along x");
                Assert.That(top[3].z, Is.EqualTo(0f).Within(Tolerance), $"variant {variant}: -x mid-rim slid along z");
                Assert.That(top[5].z, Is.EqualTo(0f).Within(Tolerance), $"variant {variant}: +x mid-rim slid along z");
            }
        }

        [Test]
        public void NothingRisesAboveTheCellTop()
        {
            // The cell above a rock is where a colonist stands. Rock growing through somebody's
            // feet is a worse fault than a straight edge, so the lump only ever chips downward.
            for (int variant = 0; variant < RockMesh.Variants; variant++)
            {
                Mesh mesh = RockMesh.For(variant);
                foreach (Vector3 vertex in mesh.vertices)
                    Assert.That(vertex.y, Is.LessThanOrEqualTo(0.5f + Tolerance),
                        $"variant {variant} rises to {vertex.y}, above the cell top");
            }
        }

        [Test]
        public void TheBaseIsTheExactCellFootprint()
        {
            // Stacked cells meet here. If the base shrank, a column of rock would show a ring of
            // daylight at every layer boundary.
            for (int variant = 0; variant < RockMesh.Variants; variant++)
            {
                Mesh mesh = RockMesh.For(variant);
                // Two things, and only these two. Nothing sticks out past the footprint, so a
                // block never overhangs the cell below it; and all four corners are present, so
                // the base genuinely spans the cell rather than being tucked inside it. The base
                // is subdivided to match the walls' feet, so it has interior vertices too — an
                // earlier version of this demanded every base vertex sit on the boundary and
                // failed the moment that subdivision arrived, which was the test being specific
                // about incidental structure rather than about the property.
                var corners = new HashSet<string>();
                int onTheBase = 0;

                foreach (Vector3 vertex in mesh.vertices)
                {
                    if (vertex.y > -0.5f - RockMesh.Skirt + Tolerance) continue;

                    Assert.That(Mathf.Abs(vertex.x), Is.LessThanOrEqualTo(0.5f + Tolerance),
                        $"variant {variant}: base vertex {vertex} overhangs the cell in x");
                    Assert.That(Mathf.Abs(vertex.z), Is.LessThanOrEqualTo(0.5f + Tolerance),
                        $"variant {variant}: base vertex {vertex} overhangs the cell in z");

                    if (Mathf.Abs(Mathf.Abs(vertex.x) - 0.5f) < Tolerance &&
                        Mathf.Abs(Mathf.Abs(vertex.z) - 0.5f) < Tolerance)
                        corners.Add($"{Mathf.Sign(vertex.x)},{Mathf.Sign(vertex.z)}");

                    onTheBase++;
                }

                Assert.That(onTheBase, Is.GreaterThan(0), $"variant {variant} has no base at all");
                Assert.That(corners.Count, Is.EqualTo(4), $"variant {variant} does not reach all four cell corners");
            }
        }

        [Test]
        public void EveryFacetFacesOutwards()
        {
            // The same fault the unit cube had once: an inside-out mesh is perfectly valid
            // geometry that renders as a hole, and nothing reports it.
            for (int variant = 0; variant < RockMesh.Variants; variant++)
            {
                Mesh mesh = RockMesh.For(variant);
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                int[] triangles = mesh.triangles;

                Assert.That(triangles.Length % 3, Is.Zero);

                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]];
                    Vector3 b = vertices[triangles[t + 1]];
                    Vector3 c = vertices[triangles[t + 2]];
                    Vector3 wound = Vector3.Cross(b - a, c - a);
                    if (wound.sqrMagnitude < 1e-9f) continue;   // a degenerate sliver faces nowhere

                    Vector3 shaded = normals[triangles[t]];
                    Assert.That(Vector3.Dot(wound.normalized, shaded), Is.GreaterThan(0.5f),
                        $"variant {variant} triangle {t / 3} is wound against its own normal");
                }
            }
        }

        [Test]
        public void TheMiddleOfTheFaceStaysWhereThingsStandOnIt()
        {
            // A colonist, an item and a stack of stone are all drawn at the cell centre, and the
            // floor of a mined tunnel is the top of the rock below it. Chipping that centre down
            // by the full drop would leave a colonist hovering over the floor of its own mine.
            for (int variant = 0; variant < RockMesh.Variants; variant++)
            {
                Vector3 centre = RockMesh.Top(variant)[4];
                Assert.That(0.5f - centre.y, Is.LessThan(RockMesh.MaxDrop * 0.25f),
                    $"variant {variant} drops its middle to {centre.y}, far enough to hover a colonist");
            }

            // And the rim genuinely does drop, or there is no silhouette and no point to any of it.
            float deepest = 0f;
            for (int variant = 0; variant < RockMesh.Variants; variant++)
            {
                Vector3[] top = RockMesh.Top(variant);
                foreach (int rim in new[] { 0, 1, 2, 3, 5, 6, 7, 8 })
                    deepest = Mathf.Max(deepest, 0.5f - top[rim].y);
            }

            Assert.That(deepest, Is.GreaterThan(RockMesh.MaxDrop * 0.5f), "the rim is barely chipped at all");
        }

        [Test]
        public void TheSkirtIsDeeperThanAnyTopCanDrop()
        {
            // THE rule for a solid stack, and the one the first two attempts did not have. A
            // dished top means a cell does not fill its own box, so a column of rock had a
            // horizontal void at every layer boundary; wherever the neighbours' rims had dropped
            // too, that void was open sideways and you could see clean through the stack. The
            // skirt of the cell above fills the dish of the cell below — but only while it is
            // deeper than the deepest possible dish, which is what this pins.
            Assert.That(RockMesh.Skirt, Is.GreaterThan(RockMesh.MaxDrop + RockMesh.MaxTilt),
                "a top can be chipped deeper than the cell above hangs down, so a stack has a slot in it");

            // Measured on the meshes as well as on the constants, in case the drop ever stops
            // being the sum of those two numbers.
            for (int variant = 0; variant < RockMesh.Variants; variant++)
            {
                float lowestTop = 0.5f;
                foreach (Vector3 point in RockMesh.Top(variant)) lowestTop = Mathf.Min(lowestTop, point.y);

                float foot = 0.5f;
                foreach (Vector3 vertex in RockMesh.For(variant).vertices) foot = Mathf.Min(foot, vertex.y);

                Assert.That(foot, Is.LessThan(lowestTop - 1f + Tolerance),
                    $"variant {variant}: its foot at {foot} does not reach under a top dished to {lowestTop} " +
                    "one cell below");
            }
        }

        [Test]
        public void TheSkirtStaysInsideTheFootprint()
        {
            // It hangs into the cell below, so a lip poking out sideways would be visible wherever
            // the cell beside *that* one is open — a rock with a flange round its ankles.
            for (int variant = 0; variant < RockMesh.Variants; variant++)
            {
                foreach (Vector3 vertex in RockMesh.For(variant).vertices)
                {
                    if (vertex.y > -0.5f + Tolerance) continue;
                    Assert.That(Mathf.Abs(vertex.x), Is.LessThanOrEqualTo(0.5f + Tolerance),
                        $"variant {variant}: the skirt flares out to {vertex}");
                    Assert.That(Mathf.Abs(vertex.z), Is.LessThanOrEqualTo(0.5f + Tolerance),
                        $"variant {variant}: the skirt flares out to {vertex}");
                }
            }
        }

        [Test]
        public void AVerticalCornerIsABrokenLine()
        {
            // Straight corners are the tell of a cut block. Every course steps its corner in and
            // out by its own amount, so the edge from foot to rim is a broken line.
            for (int variant = 0; variant < RockMesh.Variants; variant++)
            {
                Vector3[] first = RockMesh.Course(variant, 0);
                Vector3[] second = RockMesh.Course(variant, 1);

                int stepped = 0;
                foreach (int corner in new[] { 0, 2, 6, 8 })
                {
                    float a = Mathf.Abs(first[corner].x) + Mathf.Abs(first[corner].z);
                    float b = Mathf.Abs(second[corner].x) + Mathf.Abs(second[corner].z);
                    if (Mathf.Abs(a - b) > 0.01f) stepped++;
                }

                Assert.That(stepped, Is.GreaterThan(0),
                    $"variant {variant} has the same corner offset at every course, so its edges are ruled");
            }
        }

        [Test]
        public void TheLumpIsWatertight()
        {
            // Every edge shared by exactly two facets. This is the test that would have caught
            // the corner crack outright, and it is here because the rule that was supposed to
            // prevent that crack did not: each wall bulged its own half-height vertex, both moved
            // strictly outward as required, and the two walls still stopped meeting because they
            // moved outward in *different directions*. A hole in a closed solid is a structural
            // property, so it is worth asserting structurally rather than by reasoning about the
            // rules that are meant to imply it.
            //
            // Edges are keyed by position rather than by vertex index: the facets are flat-shaded,
            // so every corner is duplicated per face and indices never match across a seam.
            for (int variant = 0; variant < RockMesh.Variants; variant++)
            {
                Mesh mesh = RockMesh.For(variant);
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                var edges = new Dictionary<string, int>();

                for (int t = 0; t < triangles.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    Vector3 a = vertices[triangles[t + e]];
                    Vector3 b = vertices[triangles[t + (e + 1) % 3]];
                    string key = EdgeKey(a, b);
                    edges[key] = edges.TryGetValue(key, out int n) ? n + 1 : 1;
                }

                foreach (KeyValuePair<string, int> edge in edges)
                    Assert.That(edge.Value, Is.EqualTo(2),
                        $"variant {variant} has an edge used {edge.Value} times at {edge.Key}: the lump is not closed");
            }
        }

        /// <summary>An unordered edge key, rounded so the two facets sharing it agree.</summary>
        static string EdgeKey(Vector3 a, Vector3 b)
        {
            string first = $"{a.x:F3},{a.y:F3},{a.z:F3}";
            string second = $"{b.x:F3},{b.y:F3},{b.z:F3}";
            return string.CompareOrdinal(first, second) <= 0 ? first + "/" + second : second + "/" + first;
        }

        [Test]
        public void ACornerIsOneVertexThatBothWallsShare()
        {
            // The direct statement of the same thing: the half-height vertex at a corner belongs
            // to the plan position, not to whichever wall is asking about it.
            for (int variant = 0; variant < RockMesh.Variants; variant++)
            {
                Vector3[] mid = RockMesh.Course(variant, 0);

                foreach (int corner in new[] { 0, 2, 6, 8 })
                {
                    Vector3 point = mid[corner];
                    Assert.That(Mathf.Abs(point.x), Is.GreaterThanOrEqualTo(0.5f - Tolerance),
                        $"variant {variant} corner {corner} pulled in along x");
                    Assert.That(Mathf.Abs(point.z), Is.GreaterThanOrEqualTo(0.5f - Tolerance),
                        $"variant {variant} corner {corner} pulled in along z");
                }
            }
        }

        [Test]
        public void TheLumpsAreActuallyDifferentFromOneAnother()
        {
            // Six identical lumps would pass every rule above and change nothing on screen.
            var seen = new HashSet<string>();
            for (int variant = 0; variant < RockMesh.Variants; variant++)
            {
                var key = new System.Text.StringBuilder();
                foreach (Vector3 point in RockMesh.Top(variant))
                    key.Append(point.ToString("F4")).Append('|');
                Assert.That(seen.Add(key.ToString()), Is.True, $"variant {variant} is a copy of an earlier one");
            }
        }

        [Test]
        public void ALumpIsCachedRatherThanRebuilt()
        {
            Assert.That(RockMesh.For(2), Is.SameAs(RockMesh.For(2)), "a new mesh per call would leak one per cell");
            Assert.That(RockMesh.For(-1), Is.SameAs(RockMesh.For(RockMesh.Variants - 1)), "negative variants must wrap");
        }

        [Test]
        public void StoneIsTheThingsAPickGoesThrough()
        {
            Assert.That(RockLook.IsStone(CoreContent.TerrainRock), Is.True);
            Assert.That(RockLook.IsStone(NaturalContent.TerrainBedrock), Is.True);
            Assert.That(RockLook.IsStone(NaturalContent.TerrainIronOre), Is.True);
            Assert.That(RockLook.IsStone(NaturalContent.TerrainCoalSeam), Is.True);

            // The ground a colony stands and builds on stays flat-topped. A chipped, uneven floor
            // would read as damage rather than as earth.
            Assert.That(RockLook.IsStone(NaturalContent.TerrainGrass), Is.False);
            Assert.That(RockLook.IsStone(NaturalContent.TerrainSubsoil), Is.False);
            Assert.That(RockLook.IsStone(NaturalContent.TerrainSand), Is.False);
            Assert.That(RockLook.IsStone(NaturalContent.TerrainAir), Is.False);
        }

        [Test]
        public void TheLookOfACellIsStableAndVaried()
        {
            // Stable, because a chunk is re-meshed whenever anything in it changes and a cliff
            // that rearranged itself every time somebody dug nearby would be worse than a flat one.
            for (int i = 0; i < 20; i++)
            {
                Assert.That(RockLook.Variant(7, 9, 3), Is.EqualTo(RockLook.Variant(7, 9, 3)));
                Assert.That(RockLook.Yaw(7, 9, 3), Is.EqualTo(RockLook.Yaw(7, 9, 3)));
            }

            // Varied, and in particular varied *up a column*: the same block stacked three times
            // is the single most box-like thing a voxel world can do.
            var downAColumn = new HashSet<int>();
            for (int y = 0; y < 8; y++) downAColumn.Add(RockLook.Variant(4, 4, y) * 4 + (int)(RockLook.Yaw(4, 4, y) / 90f));
            Assert.That(downAColumn.Count, Is.GreaterThan(1), "a column of rock is the same block stacked");

            var overAField = new HashSet<int>();
            for (int x = 0; x < 16; x++)
            for (int z = 0; z < 16; z++)
                overAField.Add(RockLook.Variant(x, z, 5));
            Assert.That(overAField.Count, Is.EqualTo(RockMesh.Variants), "some lumps are never used");
        }

        [Test]
        public void EveryYawIsARightAngle()
        {
            // A square footprint is unchanged by a quarter turn, which is what keeps the lump
            // tiling after it has been rotated. Any other angle would swing its corners out of
            // the cell and its edges away from the seam.
            var seen = new HashSet<float>();
            for (int x = 0; x < 24; x++)
            for (int z = 0; z < 24; z++)
            {
                float yaw = RockLook.Yaw(x, z, 2);
                Assert.That(yaw % 90f, Is.EqualTo(0f).Within(1e-4f), $"yaw {yaw} is not a right angle");
                seen.Add(yaw);
            }

            Assert.That(seen.Count, Is.EqualTo(4), "not all four bearings are used");
        }
    }
}
