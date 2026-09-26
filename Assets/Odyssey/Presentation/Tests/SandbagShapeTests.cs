#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The sandbag and the wall it is laid in (design 53 §7a-bis). A mis-wound mesh and a bag drawn
    /// twice both render without complaint, so both are asserted here rather than looked for.
    /// </summary>
    public class SandbagShapeTests
    {
        const ushort Sandbags = CoreContent.EdificeSandbags;
        const int Y = 3;

        float _amplitude;

        [SetUp]
        public void FlatGround()
        {
            // Relief lifts each cell by its own height, which would part the courses of two cells
            // by a few centimetres and tell this test nothing about the bond.
            _amplitude = GroundRelief.Amplitude;
            GroundRelief.Amplitude = 0f;
        }

        [TearDown]
        public void PutTheGroundBack() => GroundRelief.Amplitude = _amplitude;

        [Test]
        public void TheSandbagFacesOutwards()
        {
            Mesh bag = SandbagMesh.Mesh;
            Vector3[] vertices = bag.vertices;
            int[] triangles = bag.triangles;

            int checkedFaces = 0;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                Vector3 wound = Vector3.Cross(b - a, c - a);
                if (wound.sqrMagnitude < 1e-12f) continue; // a sliver at one of the two closed ends

                // Outward from the bag's own axis, which droops at the tie: the pillow's oracle, the
                // origin, would call the ear's underside inward.
                Vector3 centroid = (a + b + c) / 3f;
                Vector3 outward = centroid - SandbagMesh.AxisAt(centroid.x);
                if (outward.sqrMagnitude < 1e-6f) continue;

                Assert.That(Vector3.Dot(wound.normalized, outward.normalized), Is.GreaterThan(0f),
                    $"triangle {t / 3} of the sandbag is wound inward");
                checkedFaces++;
            }
            Assert.That(checkedFaces, Is.GreaterThan(150), "most of the bag was actually checked");
        }

        [Test]
        public void TheSandbagFillsTheUnitBoxAndIsRounded()
        {
            Bounds bounds = SandbagMesh.Mesh.bounds;
            Assert.That(bounds.min.x, Is.EqualTo(-0.5f).Within(0.01f));
            Assert.That(bounds.max.x, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(bounds.min.y, Is.EqualTo(-0.5f).Within(0.01f));
            Assert.That(bounds.max.y, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(bounds.min.z, Is.EqualTo(-0.5f).Within(0.01f));
            Assert.That(bounds.max.z, Is.EqualTo(0.5f).Within(0.01f));

            float cubeCorner = new Vector3(0.5f, 0.5f, 0.5f).magnitude;
            float furthest = SandbagMesh.Mesh.vertices.Max(v => v.magnitude);
            Assert.That(furthest, Is.LessThan(cubeCorner * 0.95f), "the bag is a box again");

            // The two ends differ: the tie is pinched. Its rings are narrower than the sewn end's
            // at the same distance in.
            float Width(float x) => SandbagMesh.Mesh.vertices.Where(v => Mathf.Abs(v.x - x) < 0.02f)
                .Select(v => Mathf.Abs(v.z)).DefaultIfEmpty(0f).Max();
            Assert.That(Width(-0.35f), Is.GreaterThan(0.4f), "no ring was sampled at the sewn end");
            Assert.That(Width(0.35f), Is.LessThan(Width(-0.35f) * 0.8f), "the tied end is not pinched");
        }

        [Test]
        public void ALonePieceStaysInItsCellAndIsAboutTheWallsHeight()
        {
            List<Bag> bags = Bags(10, 10, 0);
            Assert.That(bags, Is.Not.Empty);
            Vector3 centre = CellMetrics.FloorCentre(10, 10, Y);
            foreach (Bag bag in bags)
            {
                Assert.That(bag.Min.x, Is.GreaterThan(centre.x - CellMetrics.HalfXZ - 0.1f));
                Assert.That(bag.Max.x, Is.LessThan(centre.x + CellMetrics.HalfXZ + 0.1f));
                Assert.That(bag.Min.y, Is.GreaterThanOrEqualTo(centre.y - 1e-3f), "a bag below the floor");
            }
            float top = bags.Max(b => b.Max.y) - centre.y;
            Assert.That(top, Is.EqualTo(CoverShape.SandbagHeight).Within(0.12f));
        }

        /// <summary>
        /// A dragged line of four is one wall: along every course and row the bags meet end to end
        /// from the first cell's edge to the last's, with no gap and no bag drawn by two cells.
        /// </summary>
        [Test]
        public void ARunIsOneWallWithEveryBagDrawnOnce()
        {
            const int first = 10, cells = 4;
            var bags = new List<Bag>();
            for (int x = first; x < first + cells; x++)
            {
                int joins = (x > first ? 1 << Directions.West : 0) | (x < first + cells - 1 ? 1 << Directions.East : 0);
                bags.AddRange(Bags(x, 10, joins));
            }

            float west = CellMetrics.FloorCentre(first, 10, Y).x - CellMetrics.HalfXZ;
            float east = west + cells * CellMetrics.SizeXZ;
            foreach (IGrouping<(int, int), Bag> line in bags.GroupBy(b => (b.Course, b.Row)))
            {
                List<Bag> run = line.OrderBy(b => b.Centre.x).ToList();
                Assert.That(run[0].Min.x, Is.LessThan(west + 0.1f), $"course {line.Key} starts short of the line's end");
                Assert.That(run[run.Count - 1].Max.x, Is.GreaterThan(east - 0.1f), $"course {line.Key} stops short");
                for (int i = 1; i < run.Count; i++)
                {
                    Assert.That(run[i].Centre.x - run[i - 1].Centre.x, Is.GreaterThan(0.15f),
                        $"course {line.Key}: two bags at x {run[i].Centre.x:F2}, a bag drawn twice");
                    Assert.That(run[i].Min.x, Is.LessThan(run[i - 1].Max.x + 0.02f),
                        $"course {line.Key}: a gap before x {run[i].Centre.x:F2}");
                }
            }
        }

        /// <summary>The bond: the joints of one stretcher course fall mid-bag in the next.</summary>
        [Test]
        public void TheCoursesAreStaggeredHalfABag()
        {
            List<Bag> bags = Bags(10, 10, (1 << Directions.East) | (1 << Directions.West));
            float Joint(int course) => bags.Where(b => b.Course == course && b.Row < 0)
                .Select(b => b.Centre.x).OrderBy(x => x).First();
            float pitch = CellMetrics.SizeXZ / 3f;
            float shift = Mathf.Abs(Joint(1) - Joint(0)) % pitch;
            Assert.That(shift, Is.EqualTo(pitch * 0.5f).Within(0.08f), "the courses are stacked, not bonded");
        }

        [Test]
        public void EveryJoinFitsAndUsesMoreThanOneCloth()
        {
            var parts = new Matrix4x4[CoverShape.MaxParts];
            var shades = new int[CoverShape.MaxParts];
            var used = new HashSet<int>();
            for (int joins = 0; joins < 16; joins++)
            {
                int count = CoverShape.Parts(Sandbags, 10, 10, Y, joins, parts, shades);
                Assert.That(count, Is.LessThan(CoverShape.MaxParts), $"joins {joins} ran out of parts");
                Assert.That(count, Is.GreaterThan(20), $"joins {joins} drew almost nothing");
                for (int i = 0; i < count; i++)
                {
                    Assert.That(shades[i], Is.InRange(0, StuffPalette.HessianShades - 1));
                    used.Add(shades[i]);
                }
            }
            Assert.That(used.Count, Is.GreaterThan(1), "every bag is the same cloth, which reads as tiles");
        }

        [Test]
        public void AHessianCodeIsClothAndNeverLinen()
        {
            for (int shade = 0; shade < StuffPalette.HessianShades; shade++)
            {
                int code = TintCode.Hessian(shade);
                Assert.That(TintCode.IsLinen(code), Is.True);
                Assert.That(code, Is.Not.EqualTo(TintCode.Linen()));
                ChunkRenderer.ResolveColour(code, fallback: true, shade: 1f, out Color tint, out _);
                Assert.That(tint, Is.EqualTo(StuffPalette.Hessian(shade)));
            }
            ChunkRenderer.ResolveColour(TintCode.Linen(), fallback: true, shade: 1f, out Color linen, out _);
            Assert.That(linen, Is.EqualTo(StuffPalette.Linen), "the pillow changed colour");
        }

        // ---- helpers --------------------------------------------------------------------------

        struct Bag
        {
            public Vector3 Centre, Min, Max;
            public int Course, Row;
        }

        static List<Bag> Bags(int x, int z, int joins)
        {
            var parts = new Matrix4x4[CoverShape.MaxParts];
            var shades = new int[CoverShape.MaxParts];
            int count = CoverShape.Parts(Sandbags, x, z, Y, joins, parts, shades);
            Vector3 cell = CellMetrics.FloorCentre(x, z, Y);
            float rise = CoverShape.SandbagHeight / (CoverShape.StretcherCourses + 1);
            var bags = new List<Bag>(count);
            for (int i = 0; i < count; i++)
            {
                Matrix4x4 m = parts[i];
                Vector3 centre = m.MultiplyPoint3x4(Vector3.zero);
                var half = new Vector3(
                    0.5f * (Mathf.Abs(m.m00) + Mathf.Abs(m.m01) + Mathf.Abs(m.m02)),
                    0.5f * (Mathf.Abs(m.m10) + Mathf.Abs(m.m11) + Mathf.Abs(m.m12)),
                    0.5f * (Mathf.Abs(m.m20) + Mathf.Abs(m.m21) + Mathf.Abs(m.m22)));
                bags.Add(new Bag
                {
                    Centre = centre, Min = centre - half, Max = centre + half,
                    Course = Mathf.FloorToInt((centre.y - half.y - cell.y + 0.02f) / rise),
                    Row = centre.z < cell.z - 0.05f ? -1 : centre.z > cell.z + 0.05f ? 1 : 0,
                });
            }
            return bags;
        }
    }
}
