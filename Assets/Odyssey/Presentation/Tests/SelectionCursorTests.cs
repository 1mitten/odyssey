#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The white selection cursor that sits on top of surfaces (floors, natural rolling terrain,
    /// water, and bank risers).
    ///
    /// <para><b>Why the geometry is asserted rather than looked at:</b> On sloped terrain, rigid
    /// horizontal brackets cut into the ground on one side and hovered on the other. Draping the
    /// stubs in cell-local coordinates holds them to the tangent plane of the ground mesh,
    /// guaranteeing a uniform clearance of <see cref="ChunkRenderer.FloorBracketBias"/> across all
    /// eight bars regardless of the terrain slope.</para>
    /// </summary>
    public class SelectionCursorTests
    {
        readonly Matrix4x4[] _edges = new Matrix4x4[8];

        static Vector3 Lengths(Matrix4x4 m) => new Vector3(
            m.MultiplyVector(Vector3.right).magnitude,
            m.MultiplyVector(Vector3.up).magnitude,
            m.MultiplyVector(Vector3.forward).magnitude);

        [TearDown]
        public void Reset()
        {
            GroundRelief.Reset();
            BankLayout.Reset();
        }

        /// <summary>
        /// A floor bracket consists of eight stubs: two per corner extending inward from each of
        /// the four cell corners.
        /// </summary>
        [Test]
        public void TheFloorBracketHasEightStubsTwoPerCorner()
        {
            GroundRelief.Amplitude = 0f;

            int n = ChunkRenderer.FloorBracketEdges(Matrix4x4.identity, _edges);

            Assert.That(n, Is.EqualTo(8), "four corners with two stubs each make eight edges");

            float half = CellMetrics.HalfXZ;
            float expectedLength = Mathf.Max(CellMetrics.SizeXZ * ChunkRenderer.BracketStub, ChunkRenderer.BracketThickness);
            float thickness = ChunkRenderer.BracketThickness;

            int alongX = 0;
            int alongZ = 0;

            for (int i = 0; i < n; i++)
            {
                Vector3 scale = Lengths(_edges[i]);
                Vector3 centre = _edges[i].GetColumn(3);

                if (scale.x > scale.z)
                {
                    alongX++;
                    Assert.That(scale.x, Is.EqualTo(expectedLength).Within(1e-4f));
                    Assert.That(scale.y, Is.EqualTo(thickness).Within(1e-4f));
                    Assert.That(scale.z, Is.EqualTo(thickness).Within(1e-4f));

                    // Stubs along X extend inward from sx * half towards 0
                    float sx = Mathf.Sign(centre.x);
                    float outerX = centre.x + sx * (expectedLength * 0.5f);
                    Assert.That(outerX, Is.EqualTo(sx * half).Within(1e-4f),
                        "stub outer edge meets the cell boundary");
                }
                else
                {
                    alongZ++;
                    Assert.That(scale.x, Is.EqualTo(thickness).Within(1e-4f));
                    Assert.That(scale.y, Is.EqualTo(thickness).Within(1e-4f));
                    Assert.That(scale.z, Is.EqualTo(expectedLength).Within(1e-4f));

                    // Stubs along Z extend inward from sz * half towards 0
                    float sz = Mathf.Sign(centre.z);
                    float outerZ = centre.z + sz * (expectedLength * 0.5f);
                    Assert.That(outerZ, Is.EqualTo(sz * half).Within(1e-4f),
                        "stub outer edge meets the cell boundary");
                }
            }

            Assert.That(alongX, Is.EqualTo(4), "one X stub per corner");
            Assert.That(alongZ, Is.EqualTo(4), "one Z stub per corner");
        }

        /// <summary>
        /// On flat ground, the bottom face of every stub sits at exactly FloorBracketBias above the
        /// floor plane.
        /// </summary>
        [Test]
        public void EveryStubSitsFlushAboveFlatGroundByExactBias()
        {
            GroundRelief.Amplitude = 0f;
            var cell = new CellRef(10, 5, 2);
            Vector3 floorCentre = CellMetrics.FloorCentre(cell);

            int n = ChunkRenderer.FloorBracketEdges(GroundRelief.Drape(floorCentre), _edges);
            Assert.That(n, Is.EqualTo(8));

            for (int i = 0; i < n; i++)
            {
                // Test all 4 bottom vertices of the unit cube instance
                for (int corner = 0; corner < 4; corner++)
                {
                    float vx = (corner & 1) == 0 ? -0.5f : 0.5f;
                    float vz = (corner & 2) == 0 ? -0.5f : 0.5f;
                    Vector3 worldPt = _edges[i].MultiplyPoint3x4(new Vector3(vx, -0.5f, vz));

                    float clearance = worldPt.y - floorCentre.y;
                    Assert.That(clearance, Is.EqualTo(ChunkRenderer.FloorBracketBias).Within(1e-4f),
                        $"stub {i} vertex {corner} bottom is exactly FloorBracketBias above the floor");
                }
            }
        }

        /// <summary>
        /// On rolling ground with steep slopes (~8°), the draped floor bracket conforms exactly to the
        /// tangent plane of the ground mesh. Every bottom vertex of every stub has positive clearance
        /// and zero penetration into the terrain.
        /// </summary>
        [Test]
        public void EveryStubSitsFlushAboveSlopedGroundUnderDrape()
        {
            GroundRelief.Reset();
            GroundRelief.Amplitude = 2f;

            // A point near the steepest the field gets over the board
            var centre = new Vector3(277f, 15f, 110.4f);
            GroundRelief.SlopeAt(centre.x, centre.z, out float slopeX, out float slopeZ);
            Assert.That(Mathf.Sqrt(slopeX * slopeX + slopeZ * slopeZ), Is.GreaterThan(0.05f),
                "the sample point has to be on a steep slope for this test to be asking a question");

            int n = ChunkRenderer.FloorBracketEdges(GroundRelief.Drape(centre), _edges);
            Assert.That(n, Is.EqualTo(8));

            float groundHeightAtCentre = centre.y + GroundRelief.HeightAt(centre.x, centre.z);

            for (int i = 0; i < n; i++)
            {
                // All 4 bottom vertices of the stub
                for (int corner = 0; corner < 4; corner++)
                {
                    float vx = (corner & 1) == 0 ? -0.5f : 0.5f;
                    float vz = (corner & 2) == 0 ? -0.5f : 0.5f;
                    Vector3 worldPt = _edges[i].MultiplyPoint3x4(new Vector3(vx, -0.5f, vz));

                    // Tangent plane height at this world (x, z):
                    float groundPlaneY = groundHeightAtCentre + slopeX * (worldPt.x - centre.x) + slopeZ * (worldPt.z - centre.z);
                    float clearance = worldPt.y - groundPlaneY;

                    Assert.That(clearance, Is.EqualTo(ChunkRenderer.FloorBracketBias).Within(1e-4f),
                        $"stub {i} vertex {corner} clearance matches FloorBracketBias exactly on slope");
                    Assert.That(clearance, Is.GreaterThan(0f), "never penetrates into ground");
                }

                // All 4 top vertices of the stub
                for (int corner = 0; corner < 4; corner++)
                {
                    float vx = (corner & 1) == 0 ? -0.5f : 0.5f;
                    float vz = (corner & 2) == 0 ? -0.5f : 0.5f;
                    Vector3 worldPt = _edges[i].MultiplyPoint3x4(new Vector3(vx, 0.5f, vz));

                    float groundPlaneY = groundHeightAtCentre + slopeX * (worldPt.x - centre.x) + slopeZ * (worldPt.z - centre.z);
                    float clearance = worldPt.y - groundPlaneY;

                    Assert.That(clearance,
                        Is.EqualTo(ChunkRenderer.FloorBracketBias + ChunkRenderer.BracketThickness).Within(1e-4f),
                        $"stub {i} top face parallels the sloped ground surface");
                }
            }
        }

        /// <summary>
        /// Straight bank shear climbs SizeY over SizeXZ along local Z. Stubs sheared by
        /// StraightBankShear rise with the 1.2 bank slope from floor level at the lower terrace
        /// to full cell height at the upper terrace.
        /// </summary>
        [Test]
        public void StraightBankShearTiltedStubsClimbWithBankRamp()
        {
            Matrix4x4 shear = BankLayout.StraightBankShear();
            Assert.That(shear.m12, Is.EqualTo(CellMetrics.SizeY / CellMetrics.SizeXZ).Within(1e-4f),
                "shear slope along local Z is SizeY / SizeXZ = 1.2");
            Assert.That(shear.m13, Is.EqualTo(CellMetrics.SizeY * 0.5f).Within(1e-4f),
                "shear center elevation is SizeY / 2 = 1.5 m");

            int n = ChunkRenderer.FloorBracketEdges(shear, _edges);
            Assert.That(n, Is.EqualTo(8));

            float expectedSlope = CellMetrics.SizeY / CellMetrics.SizeXZ;

            for (int i = 0; i < n; i++)
            {
                Vector3 scale = Lengths(_edges[i]);
                Vector3 centre = _edges[i].GetColumn(3);

                if (scale.z > scale.x)
                {
                    // Stub along Z: climbs with slope 1.2
                    Vector3 alongZ = _edges[i].MultiplyVector(Vector3.forward);
                    Assert.That(alongZ.y / alongZ.z, Is.EqualTo(expectedSlope).Within(1e-4f),
                        "stub along local Z tilts at the ramp slope");
                }
                else
                {
                    // Stub along X: horizontal across the ramp
                    Vector3 alongX = _edges[i].MultiplyVector(Vector3.right);
                    Assert.That(alongX.y, Is.EqualTo(0f).Within(1e-4f),
                        "stub along local X remains horizontal across the ramp");
                }

                // Check elevation bounds: low corner at z = -HalfXZ sits near y = Bias;
                // high corner at z = +HalfXZ sits near y = SizeY + Bias.
                float sz = Mathf.Sign(centre.z);
                if (sz < 0f)
                {
                    // Low corner
                    Assert.That(centre.y, Is.LessThan(CellMetrics.SizeY * 0.5f));
                }
                else
                {
                    // High corner
                    Assert.That(centre.y, Is.GreaterThan(CellMetrics.SizeY * 0.5f));
                }
            }
        }

        /// <summary>
        /// Explicit corner rises lift each corner stub independently to match complex surfaces like
        /// inner or outer bank hips.
        /// </summary>
        [Test]
        public void CornerRisesLiftEachCornerIndependently()
        {
            float[] rises = new[] { 0.2f, 0.8f, 1.5f, 2.7f };
            int n = ChunkRenderer.FloorBracketEdges(Matrix4x4.identity, _edges, rises);
            Assert.That(n, Is.EqualTo(8));

            for (int corner = 0; corner < 4; corner++)
            {
                int xIndex = corner * 2;
                int zIndex = corner * 2 + 1;

                float expectedY = ChunkRenderer.BracketThickness * 0.5f + ChunkRenderer.FloorBracketBias + rises[corner];

                Vector3 centreX = _edges[xIndex].GetColumn(3);
                Vector3 centreZ = _edges[zIndex].GetColumn(3);

                Assert.That(centreX.y, Is.EqualTo(expectedY).Within(1e-4f));
                Assert.That(centreZ.y, Is.EqualTo(expectedY).Within(1e-4f));
            }
        }

        /// <summary>
        /// On a water cell, lifting the placement by WaterLine.SurfaceAbove puts the floor bracket
        /// right on the water surface plane.
        /// </summary>
        [Test]
        public void WaterPlacementRestsOnWaterSurface()
        {
            GroundRelief.Amplitude = 0f;
            var cell = new CellRef(4, 4, 1);
            float waterRise = CellMetrics.SizeY * ChunkMesher.WaterSurface;

            Matrix4x4 placement = GroundRelief.Drape(CellMetrics.FloorCentre(cell) + Vector3.up * waterRise);
            int n = ChunkRenderer.FloorBracketEdges(placement, _edges);
            Assert.That(n, Is.EqualTo(8));

            float expectedY = CellMetrics.FloorCentre(cell).y + waterRise + ChunkRenderer.FloorBracketBias;

            for (int i = 0; i < n; i++)
            {
                for (int corner = 0; corner < 4; corner++)
                {
                    float vx = (corner & 1) == 0 ? -0.5f : 0.5f;
                    float vz = (corner & 2) == 0 ? -0.5f : 0.5f;
                    Vector3 worldPt = _edges[i].MultiplyPoint3x4(new Vector3(vx, -0.5f, vz));

                    Assert.That(worldPt.y, Is.EqualTo(expectedY).Within(1e-4f),
                        "bottom of bracket sits exactly FloorBracketBias above water surface");
                }
            }
        }
    }
}
