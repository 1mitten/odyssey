#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The cursor a build drag draws: a closed box over the whole run, not the selection bracket
    /// repeated once per cell (owner, 2026-09-17).
    ///
    /// <para>The geometry is asserted rather than looked at. A cursor that is only a box when
    /// somebody presses Play is a cursor nobody can hold to twelve edges, to spanning the run, or
    /// to standing plumb on a slope — and the stepped wall was exactly that class of fault living
    /// in drawing code nothing measured.</para>
    /// </summary>
    public class BuildCursorTests
    {
        readonly Matrix4x4[] _edges = new Matrix4x4[12];

        /// <summary>
        /// The length of a bar along each of its own three axes.
        ///
        /// <para><c>lossyScale</c> is deliberately not used: a draped matrix is sheared, and
        /// Unity's answer for the scale of a non-orthogonal matrix is an approximation.
        /// Transforming the unit directions is exact, and is what the bar is actually drawn
        /// at.</para>
        /// </summary>
        static Vector3 Lengths(Matrix4x4 m) => new Vector3(
            m.MultiplyVector(Vector3.right).magnitude,
            m.MultiplyVector(Vector3.up).magnitude,
            m.MultiplyVector(Vector3.forward).magnitude);

        [TearDown]
        public void Reset() => GroundRelief.Reset();

        /// <summary>The whole of the owner's request about shape: all twelve edges, closed.</summary>
        [Test]
        public void TheCursorIsAClosedBoxOfTwelveEdges()
        {
            GroundRelief.Amplitude = 0f;
            var size = new Vector3(5f, 3f, 2f);

            int n = ChunkRenderer.WireBoxEdges(size, Matrix4x4.identity, _edges);

            Assert.That(n, Is.EqualTo(12), "a box has twelve edges and a bracket has corner stubs");

            // Four bars along each axis, and no two of them in the same place.
            int[] along = new int[3];
            for (int i = 0; i < n; i++)
            {
                Vector3 scale = Lengths(_edges[i]);
                int axis = scale.x > scale.y && scale.x > scale.z ? 0
                         : scale.y > scale.z ? 1 : 2;
                along[axis]++;

                Assert.That(scale[axis],
                    Is.EqualTo(size[axis] + ChunkRenderer.WireThickness).Within(1e-4f),
                    "an edge runs the full length of its side, plus a thickness so corners close");
            }

            Assert.That(along, Is.EqualTo(new[] { 4, 4, 4 }));
        }

        /// <summary>
        /// Every corner of the box is met by three edges. This is what "closed" means and what a
        /// notch at each corner would break.
        /// </summary>
        [Test]
        public void EveryCornerIsMetByThreeEdges()
        {
            GroundRelief.Amplitude = 0f;
            var size = new Vector3(4f, 3f, 4f);
            Vector3 half = size * 0.5f;

            int n = ChunkRenderer.WireBoxEdges(size, Matrix4x4.identity, _edges);

            for (int corner = 0; corner < 8; corner++)
            {
                var at = new Vector3(
                    (corner & 1) == 0 ? -half.x : half.x,
                    (corner & 2) == 0 ? -half.y : half.y,
                    (corner & 4) == 0 ? -half.z : half.z);

                int touching = 0;
                for (int i = 0; i < n; i++)
                {
                    Vector3 centre = _edges[i].GetColumn(3);
                    Vector3 scale = Lengths(_edges[i]);
                    if (Mathf.Abs(centre.x - at.x) <= scale.x * 0.5f + 1e-3f
                        && Mathf.Abs(centre.y - at.y) <= scale.y * 0.5f + 1e-3f
                        && Mathf.Abs(centre.z - at.z) <= scale.z * 0.5f + 1e-3f)
                        touching++;
                }

                Assert.That(touching, Is.EqualTo(3), $"corner {corner} is a three-way joint");
            }
        }

        /// <summary>
        /// A run of six cells is one box six cells long, which is the difference between a cursor
        /// that shows the wall and one that shows the cells.
        /// </summary>
        [Test]
        public void TheBoxSpansTheWholeRunAndStandsOneCellTall()
        {
            ChunkRenderer.SpanBox(new CellRef(3, 7, 5), new CellRef(8, 7, 5),
                out Vector3 centre, out Vector3 size);

            Assert.That(size.x, Is.EqualTo(6f * CellMetrics.SizeXZ).Within(0.3f),
                "six cells of run, less the inset");
            Assert.That(size.z, Is.EqualTo(CellMetrics.SizeXZ).Within(0.3f), "one cell across");
            Assert.That(size.y, Is.EqualTo(CellMetrics.SizeY).Within(0.3f), "one cell tall");

            Assert.That(centre.x,
                Is.EqualTo((CellMetrics.Centre(3, 7, 5).x + CellMetrics.Centre(8, 7, 5).x) * 0.5f)
                    .Within(1e-3f));
        }

        /// <summary>
        /// <b>A floor cursor is a plate, not a cube</b>, and it lies on the boundary the slab will
        /// be laid on.
        ///
        /// <para>The owner's report (2026-09-17): *"the selection box for floors should be flat to
        /// the tile that it will be placed on rather than a cube."* A cell-tall box drawn for a
        /// slab claims a wall, hides the tile underneath itself, and at the slice camera's range
        /// gives no way to tell which of two layers it means.</para>
        ///
        /// <para>The height is pinned against the wall cursor rather than against a number, so the
        /// two can never drift into looking alike: whatever <c>PlateThickness</c> is tuned to, a
        /// floor cursor stays a small fraction of a wall cursor.</para>
        /// </summary>
        [Test]
        public void AFloorCursorIsAPlateOnTheBoundaryItWillBeLaidOn()
        {
            ChunkRenderer.SpanPlate(new CellRef(3, 7, 5), new CellRef(8, 7, 5),
                out Vector3 centre, out Vector3 size);

            Assert.That(size.x, Is.EqualTo(6f * CellMetrics.SizeXZ).Within(0.3f),
                "six cells of run, less the inset — the same footprint the box cursor covers");
            Assert.That(size.z, Is.EqualTo(CellMetrics.SizeXZ).Within(0.3f), "one cell across");

            Assert.That(size.y, Is.LessThan(CellMetrics.SizeY * 0.25f),
                "and flat: a quarter of a cell would already read as a wall");

            // On the boundary, not straddling it: the underside of the plate is the plane the slab
            // is laid on, which is CellMetrics.FloorCentre — where ChunkMesher.EmitFloor puts it.
            float underside = centre.y - size.y * 0.5f;
            Assert.That(underside, Is.EqualTo(CellMetrics.FloorCentre(3, 7, 5).y).Within(1e-3f),
                "the cursor sits on the floor plane the slab will occupy");

            ChunkRenderer.SpanBox(new CellRef(3, 7, 5), new CellRef(8, 7, 5), out _, out Vector3 wall);
            Assert.That(size.y, Is.LessThan(wall.y * 0.25f),
                "a floor cursor cannot be mistaken for a wall cursor at any tuning");
        }

        /// <summary>
        /// The drape leaves the box plumb and full height, and the shear is what carries it onto a
        /// slope. This is the same guarantee <c>ChunkMesherTests.ADrapedWallStaysVerticalAndFullHeight</c>
        /// holds the wall itself to — the cursor promises the wall, so it has to stand the way the
        /// wall will.
        /// </summary>
        [Test]
        public void ADrapedCursorStaysPlumbAndFullHeight()
        {
            GroundRelief.Reset();
            GroundRelief.Amplitude = 2f;

            // A point measured to be near the steepest the field gets over a 300 m board
            // (slope 0.088), so the drape has something to do and the test is asking a question.
            var centre = new Vector3(277f, 15f, 110.4f);
            var size = new Vector3(15f, 3f, 2.5f);

            int n = ChunkRenderer.WireBoxEdges(size, GroundRelief.Drape(centre), _edges);

            int uprights = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 scale = Lengths(_edges[i]);
                if (!(scale.y > scale.x && scale.y > scale.z)) continue;

                uprights++;
                Vector3 up = _edges[i].MultiplyVector(Vector3.up);
                Assert.That(up.x, Is.EqualTo(0f).Within(1e-4f),
                    "a shear that moves y by x and z leaves a vertical edge vertical");
                Assert.That(up.z, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(up.y, Is.EqualTo(size.y + ChunkRenderer.WireThickness).Within(1e-3f),
                    "and does not shorten it: the box stands its full height on a slope");
            }

            Assert.That(uprights, Is.EqualTo(4));

            // And the other half of a drape: the head and the foot rake with the ground. A box
            // that were merely lifted would pass everything above and still float at one end, so
            // this is the assertion that tells the two apart.
            GroundRelief.SlopeAt(centre.x, centre.z, out float slopeX, out _);
            Assert.That(Mathf.Abs(slopeX), Is.GreaterThan(0.05f),
                "the sample point has to be on a slope for this to be asking anything");

            for (int i = 0; i < n; i++)
            {
                Vector3 scale = Lengths(_edges[i]);
                if (!(scale.x > scale.y && scale.x > scale.z)) continue;

                Vector3 along = _edges[i].MultiplyVector(Vector3.right);
                Assert.That(along.y / along.x, Is.EqualTo(slopeX).Within(1e-4f),
                    "an edge along x rises with the field's slope, which is what draping is");
            }
        }

        /// <summary>
        /// And it is actually carried onto the ground rather than left at the cell's own height:
        /// the box sits on the drawn field, which is the whole reason a cursor over rolling ground
        /// does not float.
        /// </summary>
        [Test]
        public void ADrapedCursorSitsOnTheDrawnGround()
        {
            GroundRelief.Reset();
            GroundRelief.Amplitude = 2f;

            var centre = new Vector3(61.3f, 15f, 44.7f);
            float expected = centre.y + GroundRelief.HeightAt(centre.x, centre.z);

            ChunkRenderer.WireBoxEdges(new Vector3(5f, 3f, 2.5f), GroundRelief.Drape(centre), _edges);

            // The bar at the box's own centre height: take the mean of the four uprights, which is
            // the centre by symmetry.
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < 12; i++)
            {
                Vector3 scale = Lengths(_edges[i]);
                if (!(scale.y > scale.x && scale.y > scale.z)) continue;
                sum += ((Vector3)_edges[i].GetColumn(3)).y;
                count++;
            }

            Assert.That(count, Is.EqualTo(4));
            Assert.That(sum / count, Is.EqualTo(expected).Within(1e-3f));
        }
    }
}
