#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// How far two neighbouring pieces of ground part company at the edge they share, and why the
    /// answer is a lighting question rather than a hole.
    ///
    /// <para><b>The owner saw black lines between the tiles.</b> They are not gaps — a gap would
    /// show the skybox through it and the skybox is pale blue. Every ground cell is a full
    /// 2.5 x 3.0 x 2.5 box sheared onto the tangent plane of the relief field, so where two
    /// tangent planes disagree the taller cell's own side wall fills the step. That wall is
    /// vertical, the sun sits at 72 degrees and terrain casts no shadows, so it catches almost no
    /// light and reads as a black hairline.</para>
    ///
    /// <para>These tests measure the step, which is the thing the fix has to be sized against.
    /// There are two contributions and they are independent: the relief field's own second-order
    /// parting, which has been there since <c>GroundRelief</c> landed, and the per-cell rim ripple,
    /// which is newer and is a constant somebody chose. Knowing which dominates is the difference
    /// between tuning a number and changing an approach.</para>
    /// </summary>
    public class GroundSeamTests
    {
        [SetUp]
        public void Setup()
        {
            GroundRelief.Reset();
            GroundMesh.ResetLevers();
        }

        [TearDown]
        public void TearDown()
        {
            GroundRelief.Reset();
            GroundMesh.ResetLevers();
        }

        /// <summary>
        /// The worst disagreement in metres between two neighbouring draped cells, measured along
        /// every shared edge over a patch of board.
        ///
        /// <para>Each cell is drawn as the tangent plane of the field at its own centre, so its
        /// height at a point is <c>h(c) + slope(c) . (p - c)</c>. Two neighbours evaluated at the
        /// same point on their shared edge give two different answers, and the difference is the
        /// step the eye sees.</para>
        /// </summary>
        static float WorstSeam(int samplesAcross = 40)
        {
            float worst = 0f;
            const float cell = CellMetrics.SizeXZ;

            for (int j = 0; j < samplesAcross; j++)
            for (int i = 0; i < samplesAcross; i++)
            {
                float ax = i * cell + CellMetrics.HalfXZ;
                float az = j * cell + CellMetrics.HalfXZ;

                // The edge shared with the neighbour in +x, sampled at both ends and the middle.
                float edgeX = ax + CellMetrics.HalfXZ;
                for (int s = 0; s <= 2; s++)
                {
                    float ez = az + (s - 1) * CellMetrics.HalfXZ;
                    worst = Mathf.Max(worst, Mathf.Abs(
                        PlaneHeight(ax, az, edgeX, ez) - PlaneHeight(ax + cell, az, edgeX, ez)));
                }

                // And the edge shared with the neighbour in +z.
                float edgeZ = az + CellMetrics.HalfXZ;
                for (int s = 0; s <= 2; s++)
                {
                    float ex = ax + (s - 1) * CellMetrics.HalfXZ;
                    worst = Mathf.Max(worst, Mathf.Abs(
                        PlaneHeight(ax, az, ex, edgeZ) - PlaneHeight(ax, az + cell, ex, edgeZ)));
                }
            }

            return worst;
        }

        /// <summary>The height a cell centred at (cx, cz) draws at the point (px, pz).</summary>
        static float PlaneHeight(float cx, float cz, float px, float pz)
        {
            float height = GroundRelief.HeightAt(cx, cz);
            GroundRelief.SlopeAt(cx, cz, out float slopeX, out float slopeZ);
            return height + slopeX * (px - cx) + slopeZ * (pz - cz);
        }

        [Test]
        public void TheReliefFieldPartsByMillimetresAndTheDocumentedFigureIsRight()
        {
            // 06-rendering-and-camera.md 2b claims about 41 mm at the shipped amplitude and
            // wavelength. If that is wrong, every argument built on it is wrong too.
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;
            GroundRelief.Period = 150f;

            float seam = WorstSeam();
            Debug.Log($"[Seam] relief only: worst step between neighbours {seam * 1000f:F1} mm");

            Assert.That(seam, Is.LessThan(0.08f),
                $"the relief seam is {seam * 1000f:F1} mm, far more than the documented 41 mm");
            Assert.That(seam, Is.GreaterThan(0.005f), "the seam has vanished, so this measures nothing");
        }

        [Test]
        public void TheRippleWasTheLargerContributionByFarWhichIsWhyItIsOff()
        {
            // The measurement that settled it, kept because the conclusion is a design decision and
            // a decision without its evidence is a preference. A cell picks its rim heights from
            // its own variant, so two neighbours can disagree by twice the ripple at a shared edge,
            // where the relief's own parting is only second order.
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;
            GroundRelief.Period = 150f;

            float fromRelief = WorstSeam();
            const float trialRipple = 0.012f;   // what it was set to when the owner saw the lines
            float fromRipple = trialRipple * 2f * CellMetrics.SizeY;

            Debug.Log($"[Seam] relief {fromRelief * 1000f:F1} mm; a {trialRipple:F3} ripple would add " +
                      $"{fromRipple * 1000f:F1} mm, for {(fromRelief + fromRipple) * 1000f:F1} mm together");

            Assert.That(fromRipple, Is.GreaterThan(fromRelief * 3f),
                "the ripple is no longer the dominant source of the seam, so the reason it is off has changed");

            // And the conclusion, asserted where it will be noticed if somebody turns it back on
            // without re-measuring.
            Assert.That(GroundMesh.MaxRipple, Is.EqualTo(0f),
                "the rim ripple is on again; it was five times the relief's own seam for a benefit " +
                "no photograph could find, so turning it on needs a fresh contact sheet");
        }

        [Test]
        public void AFlatBoardHasNoSeamAtAllFromTheRelief()
        {
            // The barren test board, and every golden taken on it. With the relief off the tangent
            // planes are all the same plane, so any step there would be purely the ripple.
            GroundRelief.Amplitude = 0f;
            Assert.That(WorstSeam(), Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void ASideFaceIsLitLikeGroundRatherThanLikeAWall()
        {
            // The fix. The step cannot be removed while every cell is its own box, so what is
            // removed instead is the blackness: the side faces carry normals tilted up towards the
            // sky, so the sliver that fills a seam shades like the ground it sits between rather
            // than like the side of a building.
            foreach (int variant in new[] { 0, 1 })
            {
                Mesh mesh = GroundMesh.Turf(variant);
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                int[] triangles = mesh.triangles;

                int sides = 0;
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]];
                    Vector3 b = vertices[triangles[t + 1]];
                    Vector3 c = vertices[triangles[t + 2]];
                    Vector3 wound = Vector3.Cross(b - a, c - a);
                    if (wound.sqrMagnitude < 1e-9f) continue;
                    if (Mathf.Abs(wound.normalized.y) > 0.1f) continue;   // a top or a base

                    sides++;
                    Vector3 shaded = normals[triangles[t]];
                    Assert.That(shaded.y, Is.GreaterThan(0.2f),
                        $"variant {variant}: a side face still shades as a vertical wall ({shaded})");
                    Assert.That(shaded.magnitude, Is.EqualTo(1f).Within(1e-3f), "the normal is not unit length");

                    // It still has to face outward, or the wall lights from the wrong side and the
                    // cure is worse than the black line.
                    Assert.That(Vector3.Dot(
                            new Vector3(shaded.x, 0f, shaded.z).normalized,
                            new Vector3(wound.normalized.x, 0f, wound.normalized.z).normalized),
                        Is.GreaterThan(0.9f),
                        $"variant {variant}: a side normal no longer faces the way its face does");
                }

                Assert.That(sides, Is.GreaterThan(0), $"variant {variant} has no side faces to check");
            }
        }

        [Test]
        public void TheTopAndTheBaseAreNotTilted()
        {
            // Only the walls. Tilting the top would light the ground as though the sun were
            // somewhere it is not, across every cell on the board at once.
            foreach (int variant in new[] { 0, 1 })
            {
                Mesh mesh = GroundMesh.Turf(variant);
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                int[] triangles = mesh.triangles;

                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]];
                    Vector3 wound = Vector3.Cross(vertices[triangles[t + 1]] - a, vertices[triangles[t + 2]] - a);
                    if (wound.sqrMagnitude < 1e-9f) continue;
                    if (Mathf.Abs(wound.normalized.y) <= 0.9f) continue;

                    Assert.That(Vector3.Dot(normals[triangles[t]], wound.normalized), Is.GreaterThan(0.99f),
                        $"variant {variant}: a horizontal face had its normal tilted");
                }
            }
        }

        [Test]
        public void TheTiltIsALeverAndZeroIsExactlyTheOldNormals()
        {
            // Off has to be exactly off, because it is the control the contact sheet is judged
            // against and because a lever that cannot be returned to zero is not a lever.
            float was = GroundMesh.SideNormalTiltDegrees;
            try
            {
                GroundMesh.SideNormalTiltDegrees = 0f;
                GroundMesh.Invalidate();

                Mesh mesh = GroundMesh.Turf(0);
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                int[] triangles = mesh.triangles;

                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]];
                    Vector3 wound = Vector3.Cross(vertices[triangles[t + 1]] - a, vertices[triangles[t + 2]] - a);
                    if (wound.sqrMagnitude < 1e-9f) continue;
                    Assert.That(Vector3.Dot(normals[triangles[t]], wound.normalized), Is.GreaterThan(0.999f),
                        "at zero tilt a normal no longer matches its own face");
                }
            }
            finally
            {
                GroundMesh.SideNormalTiltDegrees = was;
                GroundMesh.Invalidate();
            }
        }
    }
}
