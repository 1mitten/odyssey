#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The field that gives the flat board its shape.
    ///
    /// Four things have to hold, and every one of them fails silently — as a seam nobody traces
    /// back, or as ground that crawls when a wall goes up.
    ///
    /// It must be **stable**, for the reason the grass is: a chunk is re-meshed whenever anything
    /// in it changes, so a field that was not a pure function of position would make the land move
    /// under the colony. It must be **switchable off** and reproduce the old flat board exactly,
    /// because that is the promise that this is decoration. It must be **smooth enough that
    /// neighbouring cells meet**, which is the one real risk in drawing a continuous surface out of
    /// per-cell tangent planes. And it must be **patternless**, because a lattice field sampled on
    /// a grid is exactly the shape that produces stripes.
    /// </summary>
    public class GroundReliefTests
    {
        [SetUp]
        public void SetUp() => GroundRelief.Reset();

        [TearDown]
        public void TearDown() => GroundRelief.Reset();

        [Test]
        public void TheSamePlaceAlwaysHasTheSameHeight()
        {
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;

            float first = GroundRelief.HeightAt(43.75f, 118.25f);
            for (int i = 0; i < 50; i++)
                Assert.That(GroundRelief.HeightAt(43.75f, 118.25f), Is.EqualTo(first));
        }

        [Test]
        public void NoAmplitudeIsTheFlatBoardExactly()
        {
            GroundRelief.Amplitude = 0f;

            for (int z = 0; z < 40; z++)
            for (int x = 0; x < 40; x++)
            {
                Vector3 centre = CellMetrics.FloorCentre(x, z, 3);
                Assert.That(GroundRelief.HeightAt(centre.x, centre.z), Is.Zero);
                Assert.That(GroundRelief.Lift(centre), Is.EqualTo(centre));
                Assert.That(GroundRelief.Drape(centre), Is.EqualTo(Matrix4x4.Translate(centre)),
                    "with relief off a ground cell must get precisely the matrix it had before");
            }
        }

        [Test]
        public void TheGroundStaysInsideTheAmplitudeItWasGiven()
        {
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;

            for (int z = 0; z < 120; z++)
            for (int x = 0; x < 120; x++)
            {
                Vector3 centre = CellMetrics.FloorCentre(x, z, 0);
                Assert.That(GroundRelief.HeightAt(centre.x, centre.z),
                    Is.InRange(-GroundRelief.BoardAmplitude, GroundRelief.BoardAmplitude));
            }
        }

        /// <summary>
        /// The one that pins the whole construction.
        ///
        /// Each cell is drawn as the tangent plane of the field at its own centre, so two
        /// neighbours agree along their shared edge only to second order. That error is what would
        /// show as a grid of hairline seams across the meadow, and it grows as the period shortens
        /// — so this test is really a guard on <see cref="GroundRelief.Period"/>: shorten it far
        /// enough to make the slope dramatic and this fails before anyone has to notice the seams
        /// in a screenshot.
        /// </summary>
        [Test]
        public void NeighbouringCellsMeetAlongTheirSharedEdge()
        {
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;

            const float tolerance = 0.06f; // 60 mm; the shipped field bounds out at about 41 mm
            float worst = 0f;

            for (int z = 0; z < 60; z++)
            for (int x = 0; x < 60; x++)
            {
                Vector3 here = CellMetrics.FloorCentre(x, z, 0);
                Vector3 east = CellMetrics.FloorCentre(x + 1, z, 0);

                // The midpoint of the shared face, evaluated on each cell's own tangent plane.
                float edgeX = here.x + CellMetrics.HalfXZ;
                worst = Mathf.Max(worst, Mathf.Abs(
                    PlaneAt(here, edgeX, here.z) - PlaneAt(east, edgeX, here.z)));

                Vector3 north = CellMetrics.FloorCentre(x, z + 1, 0);
                float edgeZ = here.z + CellMetrics.HalfXZ;
                worst = Mathf.Max(worst, Mathf.Abs(
                    PlaneAt(here, here.x, edgeZ) - PlaneAt(north, here.x, edgeZ)));
            }

            Assert.That(worst, Is.LessThan(tolerance),
                $"neighbouring cells part company by {worst * 1000f:0.0} mm at their shared edge, " +
                "which will read as a grid of facets. The period is too short for the amplitude.");
        }

        /// <summary>Evaluate a cell's drape at a world point, the way the GPU will.</summary>
        static float PlaneAt(Vector3 centre, float x, float z)
        {
            Matrix4x4 m = GroundRelief.Drape(centre);
            // The drape is built about the cell's own centre, so undo the translate the matrix
            // carries and ask where the top of the cell lands at (x, z).
            return m.MultiplyPoint3x4(new Vector3(x - centre.x, 0f, z - centre.z)).y;
        }

        [Test]
        public void TheGroundIsShearedAndNeverTwisted()
        {
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;

            for (int z = 0; z < 30; z++)
            for (int x = 0; x < 30; x++)
            {
                Matrix4x4 m = GroundRelief.Drape(CellMetrics.FloorCentre(x, z, 0));

                Assert.That(m.determinant, Is.EqualTo(1f).Within(1e-4f),
                    "a shear has unit determinant; anything else flips winding or changes volume");

                // A vertical edge must stay vertical or the cells gap against one another.
                Vector3 foot = m.MultiplyPoint3x4(new Vector3(1f, 0f, 1f));
                Vector3 head = m.MultiplyPoint3x4(new Vector3(1f, CellMetrics.SizeY, 1f));
                Assert.That(head.x, Is.EqualTo(foot.x).Within(1e-5f));
                Assert.That(head.z, Is.EqualTo(foot.z).Within(1e-5f));
                Assert.That(head.y - foot.y, Is.EqualTo(CellMetrics.SizeY).Within(1e-4f),
                    "a sheared cell keeps its full height, so it still reaches its neighbours");
            }
        }

        [Test]
        public void ThingsStandingOnTheGroundStandOnTheGround()
        {
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;

            for (int z = 0; z < 30; z++)
            for (int x = 0; x < 30; x++)
            {
                Vector3 centre = CellMetrics.FloorCentre(x, z, 2);
                Vector3 lifted = GroundRelief.Lift(centre);

                Assert.That(lifted.x, Is.EqualTo(centre.x), "a lift never moves anything sideways");
                Assert.That(lifted.z, Is.EqualTo(centre.z));
                Assert.That(lifted.y - centre.y,
                    Is.EqualTo(GroundRelief.HeightAt(centre.x, centre.z)).Within(1e-5f),
                    "a colonist stands at exactly the height of the ground drawn under her");
            }
        }

        /// <summary>
        /// A lattice field sampled on a grid is precisely the shape that stripes, and stripes in a
        /// meadow read as a worldgen fault. Sign changes along a row and down a column must not
        /// line up with the cell grid.
        /// </summary>
        [Test]
        public void TheFieldHasNoStripesInIt()
        {
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;

            int alternating = 0;
            for (int z = 0; z < 80; z++)
            {
                bool previous = GroundRelief.HeightAt(0f, z * CellMetrics.SizeXZ) > 0f;
                int flips = 0;
                for (int x = 1; x < 80; x++)
                {
                    bool sign = GroundRelief.HeightAt(x * CellMetrics.SizeXZ, z * CellMetrics.SizeXZ) > 0f;
                    if (sign != previous) flips++;
                    previous = sign;
                }

                // A smooth field of this period crosses zero a handful of times across 80 cells.
                // A striped one crosses on nearly every cell.
                if (flips > 20) alternating++;
            }

            Assert.That(alternating, Is.Zero,
                $"{alternating} rows of 80 alternate sign more than 20 times: the field is striping, " +
                "not rolling");
        }

        /// <summary>
        /// The constraint that stops the strata coming apart.
        ///
        /// A column is displaced as a column. If the surface cell were sheared and the rock under
        /// it were not, the surface would slide off its own foundation and the player would see
        /// daylight between them the moment they sliced down a layer.
        /// </summary>
        [Test]
        public void EveryLayerOfAColumnIsDisplacedTogether()
        {
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;

            for (int z = 0; z < 20; z++)
            for (int x = 0; x < 20; x++)
            {
                Matrix4x4 ground = GroundRelief.Drape(CellMetrics.FloorCentre(x, z, 0));
                for (int y = 1; y < 8; y++)
                {
                    Matrix4x4 above = GroundRelief.Drape(CellMetrics.FloorCentre(x, z, y));

                    Assert.That(above.m10, Is.EqualTo(ground.m10).Within(1e-6f),
                        "a column shears by the same amount at every layer");
                    Assert.That(above.m12, Is.EqualTo(ground.m12).Within(1e-6f));
                    Assert.That(above.m13 - ground.m13,
                        Is.EqualTo(y * CellMetrics.SizeY).Within(1e-3f),
                        "layers stay exactly one layer apart, so the strata cannot separate");
                }
            }
        }

        /// <summary>
        /// The slope has to be the real derivative of the height, or the tilt of a cell disagrees
        /// with where its neighbours sit and the seam test above is measuring the wrong thing.
        /// </summary>
        [Test]
        public void TheSlopeIsTheDerivativeOfTheHeight()
        {
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;

            const float h = 0.01f;
            for (int i = 0; i < 40; i++)
            {
                float x = i * 7.3f;
                float z = i * 11.9f;

                GroundRelief.SlopeAt(x, z, out float slopeX, out float slopeZ);

                float numericX = (GroundRelief.HeightAt(x + h, z) - GroundRelief.HeightAt(x - h, z)) / (2f * h);
                float numericZ = (GroundRelief.HeightAt(x, z + h) - GroundRelief.HeightAt(x, z - h)) / (2f * h);

                Assert.That(slopeX, Is.EqualTo(numericX).Within(1e-3f));
                Assert.That(slopeZ, Is.EqualTo(numericZ).Within(1e-3f));
            }
        }

        /// <summary>
        /// The bound the culling boxes are built from. If it ever understates the real slope a
        /// chunk gets culled while it is still on screen, which is the silent failure the mesher's
        /// own bounds comment already warns about.
        /// </summary>
        [Test]
        public void TheSlopeNeverExceedsItsStatedBound()
        {
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;
            float bound = GroundRelief.MaxSlope(GroundRelief.BoardAmplitude);

            for (int i = 0; i < 4000; i++)
            {
                float x = i * 3.7f;
                float z = i * 2.3f;
                GroundRelief.SlopeAt(x, z, out float slopeX, out float slopeZ);

                Assert.That(Mathf.Abs(slopeX), Is.LessThanOrEqualTo(bound + 1e-4f));
                Assert.That(Mathf.Abs(slopeZ), Is.LessThanOrEqualTo(bound + 1e-4f));
            }
        }

        /// <summary>
        /// The board relief has to read as shading, which is the whole reason it exists. A slope
        /// of a degree or two moves the lit value by well under a per cent against this scene's
        /// high sun and strong ambient, so an amplitude that looks sensible on paper can ship as
        /// no visible change at all. This is the guard against that.
        /// </summary>
        [Test]
        public void TheBoardActuallySlopesEnoughToSee()
        {
            float degrees = Mathf.Atan(GroundRelief.MaxSlope(GroundRelief.BoardAmplitude)) * Mathf.Rad2Deg;

            Assert.That(degrees, Is.GreaterThan(4f),
                $"the board slopes by at most {degrees:0.0} degrees, which will not read as relief");
            Assert.That(GroundRelief.BoardAmplitude, Is.LessThan(CellMetrics.SizeY),
                "relief must never rise far enough to be mistaken for a step up a layer");
        }

        [Test]
        public void TheSurroundCanRiseFurtherThanTheBoardWithoutBecomingADifferentField()
        {
            // The hills outside the board are the same shape as the roll inside it, only taller.
            // If they were a separate field the seam at the rim would be a discontinuity.
            const float far = 30f;
            float near = GroundRelief.HeightAt(500f, 500f, GroundRelief.BoardAmplitude);
            float hill = GroundRelief.HeightAt(500f, 500f, far);

            Assert.That(hill, Is.EqualTo(near * (far / GroundRelief.BoardAmplitude)).Within(1e-3f),
                "amplitude scales the one field; it does not choose a different one");
        }
    }
}
