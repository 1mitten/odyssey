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
    /// Uneven ground that still tiles, and terrace faces that are not cast planes.
    ///
    /// <para>Earth has one rule stone does not, and it is the rule most of these tests are about:
    /// <b>the middle of the top face is where everything in the world stands</b>. A colonist, a
    /// stack of wood, a tree and the selection cursor are all drawn at
    /// <c>CellMetrics.FloorCentre</c>. Rock may chip its whole top down because whatever stands on
    /// a rock stands on the cell above it; a meadow that did the same would hover every colonist
    /// on it.</para>
    ///
    /// <para>The second rule is the tiling one <see cref="RockMesh"/> already pays for: a block
    /// must cover its whole cell box, because a gap between two of them lets daylight through the
    /// world, which is a far worse fault than a flat face.</para>
    /// </summary>
    public class GroundMeshTests
    {
        const float Tolerance = 1e-4f;

        static IEnumerable<int> Variants
        {
            get { for (int v = 0; v < GroundMesh.Variants; v++) yield return v; }
        }

        [Test]
        public void TheMiddleOfTheTopIsPinnedExactlyWhereThingsStandOnIt()
        {
            // Exactly, not nearly. This is the one number in the file that may not be approximate:
            // it is the floor of the cell, the simulation's own FloorCentre is drawn on it, and
            // anything else puts every colonist in the colony a few centimetres out.
            foreach (int v in Variants)
                Assert.That(GroundMesh.Top(v)[4].y, Is.EqualTo(0.5f).Within(Tolerance),
                    $"variant {v} moved the middle of its own floor");
        }

        [Test]
        public void NothingMovesInPlan()
        {
            // Earth meets air at a riser, where a sideways bulge would hang over the terrace
            // below. Rock is free to bulge because neighbouring rock overlaps invisibly; turf is
            // not, so its footprint is exactly the cell and two neighbours cannot disagree.
            foreach (int v in Variants)
            foreach (Vector3 point in GroundMesh.Top(v))
            {
                float x = Mathf.Abs(point.x), z = Mathf.Abs(point.z);
                Assert.That(x == 0f || Mathf.Abs(x - 0.5f) < Tolerance, Is.True,
                    $"variant {v}: top vertex {point} left its lattice position in x");
                Assert.That(z == 0f || Mathf.Abs(z - 0.5f) < Tolerance, Is.True,
                    $"variant {v}: top vertex {point} left its lattice position in z");
            }

            foreach (int v in Variants)
            foreach (Vector3 vertex in GroundMesh.Turf(v).vertices)
            {
                Assert.That(Mathf.Abs(vertex.x), Is.LessThanOrEqualTo(0.5f + Tolerance),
                    $"variant {v}: turf vertex {vertex} overhangs its cell in x");
                Assert.That(Mathf.Abs(vertex.z), Is.LessThanOrEqualTo(0.5f + Tolerance),
                    $"variant {v}: turf vertex {vertex} overhangs its cell in z");
            }
        }

        [Test]
        public void TheRimRipplesBothWaysAndStaysInsideItsBudget()
        {
            // Both ways is the point. A rim that can only drop gives every cell the same shallow
            // bowl, and a field of identical bowls is a waffle — a more obviously artificial
            // pattern than the flat quads it replaced.
            bool rose = false, fell = false;

            foreach (int v in Variants)
            {
                Vector3[] top = GroundMesh.Top(v);
                foreach (int rim in new[] { 0, 1, 2, 3, 5, 6, 7, 8 })
                {
                    float rise = top[rim].y - 0.5f;
                    Assert.That(Mathf.Abs(rise), Is.LessThanOrEqualTo(GroundMesh.MaxRipple + Tolerance),
                        $"variant {v} rim {rim} moved {rise}, past its own budget");
                    if (rise > Tolerance) rose = true;
                    if (rise < -Tolerance) fell = true;
                }
            }

            Assert.That(rose, Is.True, "no rim vertex anywhere rises: the ground can only dish");
            Assert.That(fell, Is.True, "no rim vertex anywhere falls: the ground can only dome");
        }

        [Test]
        public void TheSkirtIsDeeperThanTheRimCanDip()
        {
            // The rule that keeps a column of earth solid. A dipped rim means the cell does not
            // fill its own box, so there is a horizontal slot at every layer boundary — and at a
            // terrace riser, which is exactly where anyone is looking, that slot is a dark line
            // ruled along the face. The skirt of the cell above fills the dish of the cell below.
            Assert.That(GroundMesh.Skirt, Is.GreaterThan(GroundMesh.MaxRipple),
                "a rim can dip deeper than the cell above hangs down, so a column has a slot in it");

            // Measured on the meshes too, in case the dip ever stops being exactly MaxRipple.
            foreach (int v in Variants)
            foreach (bool coursed in new[] { false, true })
            {
                Mesh mesh = coursed ? GroundMesh.Face(v) : GroundMesh.Turf(v);

                float lowestTop = 0.5f;
                foreach (Vector3 point in GroundMesh.Top(v)) lowestTop = Mathf.Min(lowestTop, point.y);

                float foot = 0.5f;
                foreach (Vector3 vertex in mesh.vertices) foot = Mathf.Min(foot, vertex.y);

                Assert.That(foot, Is.LessThan(lowestTop - 1f + Tolerance),
                    $"variant {v} ({(coursed ? "face" : "turf")}): its foot at {foot} does not reach " +
                    $"under a top dipped to {lowestTop} one cell below");
            }
        }

        [Test]
        public void ACourseStepsOutAndNeverIn()
        {
            // Outward is always safe: it reaches either into the open air in front of the riser or
            // into a neighbour's solid interior. Inward recedes past whatever is behind it, and
            // what is behind a riser may be a cell the mesher culled for being buried — which is
            // a hole through the world to the sky.
            foreach (int v in Variants)
            for (int course = 0; course < GroundMesh.CourseCount; course++)
            {
                Vector3[] ring = GroundMesh.Course(v, course);

                for (int j = 0; j < 3; j++)
                for (int i = 0; i < 3; i++)
                {
                    Vector3 point = ring[j * 3 + i];
                    string where = $"variant {v} course {course} vertex ({i},{j}) at {point}";

                    if (i == 0) Assert.That(point.x, Is.LessThanOrEqualTo(-0.5f + Tolerance), where + " stepped in");
                    if (i == 2) Assert.That(point.x, Is.GreaterThanOrEqualTo(0.5f - Tolerance), where + " stepped in");
                    if (j == 0) Assert.That(point.z, Is.LessThanOrEqualTo(-0.5f + Tolerance), where + " stepped in");
                    if (j == 2) Assert.That(point.z, Is.GreaterThanOrEqualTo(0.5f - Tolerance), where + " stepped in");

                    Assert.That(Mathf.Abs(point.x), Is.LessThanOrEqualTo(0.5f + GroundMesh.MaxBulge + Tolerance),
                        where + " stepped out past its budget");
                    Assert.That(Mathf.Abs(point.z), Is.LessThanOrEqualTo(0.5f + GroundMesh.MaxBulge + Tolerance),
                        where + " stepped out past its budget");
                }
            }
        }

        [Test]
        public void ACornerIsOneVertexThatBothWallsShare()
        {
            // RockMesh records what happens otherwise, and it is worth restating because it is the
            // kind of fault that obeys every rule and still fails: two walls each bulging their
            // own end of a shared corner both move strictly outward, as required, and still stop
            // meeting, because they move outward in different directions. The bulge belongs to the
            // position, so it is one direction, one amount, one point.
            foreach (int v in Variants)
            for (int course = 0; course < GroundMesh.CourseCount; course++)
            {
                Vector3[] ring = GroundMesh.Course(v, course);
                foreach (int corner in new[] { 0, 2, 6, 8 })
                {
                    Assert.That(Mathf.Abs(ring[corner].x), Is.GreaterThanOrEqualTo(0.5f - Tolerance),
                        $"variant {v} course {course} corner {corner} pulled in along x");
                    Assert.That(Mathf.Abs(ring[corner].z), Is.GreaterThanOrEqualTo(0.5f - Tolerance),
                        $"variant {v} course {course} corner {corner} pulled in along z");
                }
            }
        }

        [Test]
        public void EveryFacetFacesOutwards()
        {
            // The fault the unit cube had once: an inside-out mesh is perfectly valid geometry
            // that renders as a hole, and nothing reports it.
            foreach (int v in Variants)
            foreach (bool coursed in new[] { false, true })
            {
                Mesh mesh = coursed ? GroundMesh.Face(v) : GroundMesh.Turf(v);
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
                    if (wound.sqrMagnitude < 1e-9f) continue;

                    Assert.That(Vector3.Dot(wound.normalized, normals[triangles[t]]), Is.GreaterThan(0.5f),
                        $"variant {v} ({(coursed ? "face" : "turf")}) triangle {t / 3} is wound against its normal");
                }
            }
        }

        [Test]
        public void TheBlockIsWatertight()
        {
            // Every edge shared by exactly two facets. A hole in a closed solid is a structural
            // property, so it is worth asserting structurally rather than by reasoning about the
            // rules that are meant to imply it — which is how the corner crack survived in
            // RockMesh until it was measured this way.
            foreach (int v in Variants)
            foreach (bool coursed in new[] { false, true })
            {
                Mesh mesh = coursed ? GroundMesh.Face(v) : GroundMesh.Turf(v);
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                var edges = new Dictionary<string, int>();

                for (int t = 0; t < triangles.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    string key = EdgeKey(vertices[triangles[t + e]], vertices[triangles[t + (e + 1) % 3]]);
                    edges[key] = edges.TryGetValue(key, out int n) ? n + 1 : 1;
                }

                foreach (KeyValuePair<string, int> edge in edges)
                    Assert.That(edge.Value, Is.EqualTo(2),
                        $"variant {v} ({(coursed ? "face" : "turf")}) has an edge used {edge.Value} " +
                        $"times at {edge.Key}: the block is not closed");
            }
        }

        static string EdgeKey(Vector3 a, Vector3 b)
        {
            string first = $"{a.x:F3},{a.y:F3},{a.z:F3}";
            string second = $"{b.x:F3},{b.y:F3},{b.z:F3}";
            return string.CompareOrdinal(first, second) <= 0 ? first + "/" + second : second + "/" + first;
        }

        [Test]
        public void TurfIsCheaperThanAFaceAndBothAreCheapEnough()
        {
            // The performance decision, asserted rather than trusted. Ground is the largest
            // instance population in the world; if the cheap case ever stops being cheap, the cost
            // lands on every cell of the board at once.
            foreach (int v in Variants)
            {
                int turf = GroundMesh.Turf(v).triangles.Length / 3;
                int face = GroundMesh.Face(v).triangles.Length / 3;

                Assert.That(turf, Is.LessThan(face), $"variant {v}: turf is not the cheaper mesh");
                Assert.That(turf, Is.LessThanOrEqualTo(40),
                    $"variant {v}: turf costs {turf} triangles against a cube's 12, on every cell drawn");
                Assert.That(face, Is.LessThanOrEqualTo(96), $"variant {v}: a face costs {face} triangles");
            }
        }

        [Test]
        public void TheHeightFunctionIsTheSurfaceThatIsDrawn()
        {
            // What a tuft of grass asks. If it disagreed with the mesh the grass would float or
            // sink, which is exactly the fault it exists to prevent.
            foreach (int v in Variants)
            {
                Vector3[] top = GroundMesh.Top(v);

                for (int j = 0; j < 3; j++)
                for (int i = 0; i < 3; i++)
                {
                    Vector3 point = top[j * 3 + i];
                    Assert.That(GroundMesh.HeightAtLocal(v, point.x, point.z),
                        Is.EqualTo(point.y - 0.5f).Within(Tolerance),
                        $"variant {v}: the height function disagrees with the mesh at ({i},{j})");
                }

                // Pinned at the centre, bounded everywhere, and clamped rather than wrapped
                // outside the cell.
                Assert.That(GroundMesh.HeightAtLocal(v, 0f, 0f), Is.EqualTo(0f).Within(Tolerance));
                for (float u = -1f; u <= 1f; u += 0.1f)
                for (float w = -1f; w <= 1f; w += 0.1f)
                    Assert.That(Mathf.Abs(GroundMesh.HeightAtLocal(v, u, w)),
                        Is.LessThanOrEqualTo(GroundMesh.MaxRipple + Tolerance),
                        $"variant {v}: the height function leaves its budget at ({u},{w})");
            }
        }

        [Test]
        public void TheTopsAreActuallyDifferentFromOneAnother()
        {
            var seen = new HashSet<string>();
            foreach (int v in Variants)
            {
                var key = new System.Text.StringBuilder();
                foreach (Vector3 point in GroundMesh.Top(v)) key.Append(point.ToString("F4")).Append('|');
                Assert.That(seen.Add(key.ToString()), Is.True, $"variant {v} is a copy of an earlier one");
            }
        }

        [Test]
        public void AMeshIsCachedRatherThanRebuilt()
        {
            Assert.That(GroundMesh.Turf(1), Is.SameAs(GroundMesh.Turf(1)), "a new mesh per call would leak one per cell");
            Assert.That(GroundMesh.Face(1), Is.SameAs(GroundMesh.Face(1)));
            Assert.That(GroundMesh.Turf(1), Is.Not.SameAs(GroundMesh.Face(1)), "turf and face resolved to one mesh");
            Assert.That(GroundMesh.Turf(-1), Is.SameAs(GroundMesh.Turf(GroundMesh.Variants - 1)),
                "negative variants must wrap");
        }

        [Test]
        public void EarthIsTheNaturalSoilsAndNothingElse()
        {
            Assert.That(GroundLook.IsEarth(NaturalContent.TerrainGrass), Is.True);
            Assert.That(GroundLook.IsEarth(NaturalContent.TerrainBareEarth), Is.True);
            Assert.That(GroundLook.IsEarth(NaturalContent.TerrainSubsoil), Is.True);
            Assert.That(GroundLook.IsEarth(NaturalContent.TerrainSand), Is.True);
            Assert.That(GroundLook.IsEarth(NaturalContent.TerrainMarsh), Is.True);

            // Stone keeps its lump, water its shader, and the city generator's engineered fill and
            // salvage keep the cube: they are man-made and have no business rippling like a meadow.
            Assert.That(GroundLook.IsEarth(CoreContent.TerrainRock), Is.False);
            Assert.That(GroundLook.IsEarth(NaturalContent.TerrainBedrock), Is.False);
            Assert.That(GroundLook.IsEarth(NaturalContent.TerrainIronOre), Is.False);
            Assert.That(GroundLook.IsEarth(NaturalContent.TerrainShallowWater), Is.False);
            Assert.That(GroundLook.IsEarth(NaturalContent.TerrainDeepWater), Is.False);
            Assert.That(GroundLook.IsEarth(CoreContent.TerrainAir), Is.False);

            // Earth and stone are disjoint, or a cell would have two meshes and the mesher would
            // pick whichever branch it happened to test first.
            for (ushort t = 0; t < NaturalContent.TerrainCount; t++)
                Assert.That(GroundLook.IsEarth(t) && RockLook.IsStone(t), Is.False,
                    $"terrain {t} is both earth and stone");
        }

        [Test]
        public void TheLookOfACellIsStableAndVaried()
        {
            // Stable, because a chunk is re-meshed whenever anything in it changes, and a meadow
            // that reshuffled itself every time somebody felled a tree would be worse than a flat
            // one.
            for (int i = 0; i < 20; i++)
            {
                Assert.That(GroundLook.Variant(7, 9, 3), Is.EqualTo(GroundLook.Variant(7, 9, 3)));
                Assert.That(GroundLook.Yaw(7, 9, 3), Is.EqualTo(GroundLook.Yaw(7, 9, 3)));
            }

            // Every bearing is a right angle: a square footprint is unchanged by a quarter turn,
            // which is what keeps the block tiling after it has been turned.
            var bearings = new HashSet<float>();
            var looks = new HashSet<int>();
            for (int x = 0; x < 24; x++)
            for (int z = 0; z < 24; z++)
            {
                float yaw = GroundLook.Yaw(x, z, 5);
                Assert.That(yaw % 90f, Is.EqualTo(0f).Within(Tolerance), $"yaw {yaw} is not a right angle");
                bearings.Add(yaw);
                looks.Add(GroundLook.Variant(x, z, 5) * 4 + (int)(yaw / 90f));
            }

            Assert.That(bearings.Count, Is.EqualTo(4), "not all four bearings are used");
            Assert.That(looks.Count, Is.EqualTo(GroundMesh.Variants * 4),
                "some of the variant-and-bearing combinations never appear");
        }

        [Test]
        public void EarthAndStoneDoNotShareTheirLook()
        {
            // Sharing the salts would tie the variant of a grass cell to the variant of the rock
            // beneath it. That is invisible almost everywhere and then abruptly is not: cut a
            // bench out of a hillside and every exposed rock would wear the lump matching the turf
            // that used to be on top of it.
            int agreements = 0;
            for (int x = 0; x < 16; x++)
            for (int z = 0; z < 16; z++)
                if (GroundLook.Yaw(x, z, 4) == RockLook.Yaw(x, z, 4)) agreements++;

            Assert.That(agreements, Is.LessThan(200), "earth and stone are turning the same way everywhere");
        }
    }
}
