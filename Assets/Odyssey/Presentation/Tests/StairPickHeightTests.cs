#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// <b>A stair is clicked where it is drawn</b>, which it was not until the owner reported it:
    /// <i>"I couldn't click on the stairs either to get any information"</i> (2026-09-21, the first
    /// play of U44).
    ///
    /// <para><b>The same fault the bed had</b>, and <see cref="BedPickHeightTests"/> is the
    /// precedent in full. A stair does not occlude — it is passable, for the ladder's reason — so
    /// the only surface it offered a ray was the floor of the cell it stands in. But a stair is
    /// <em>drawn climbing</em>: the lower half rises to 1.5 m across its own cell and the upper
    /// half from 1.5 m to 3.0 m across the next one. At the play camera's 48° a surface 1.5 m up
    /// is drawn more than half a cell nearer the viewer than the floor beneath it, so the whole of
    /// the drawn staircase sat in front of the cells that answered for it and every click on it
    /// went through to the ground behind.</para>
    ///
    /// <para><b>Why the ramp is two surfaces and not one.</b> <c>StandHeight</c> offers one flat
    /// plane per cell, at the top of that cell's own run, and the picker claims it only where the
    /// ray crosses it <em>inside that cell's footprint</em>. The far half of each cell is therefore
    /// answered by the top plane and the near half by the floor underneath — which between them
    /// approximate the ramp, from either end, without a second kind of geometry in the picker.
    /// The two together are the test: aiming anywhere along the drawn flight must answer with the
    /// cell it is over.</para>
    /// </summary>
    public class StairPickHeightTests
    {
        const int X = 4, LowZ = 4, HighZ = 5, Layer = 1;

        /// <summary>The play camera's own elevation, which is the angle the drift scales with.</summary>
        const float PlayCameraDegrees = 48f;

        /// <summary>
        /// How high the flight is drawn at a point <paramref name="along"/> cells from the foot of
        /// it — the mesher's own arithmetic, so a stair redrawn moves this test with it.
        /// <c>SM_Bld_Base_Stairs_01</c> is a half-flight rising 1.50 m over a 2.5 m run, and
        /// <c>ChunkMesher.EmitStair</c> lifts the upper half by half a layer.
        /// </summary>
        static float DrawnHeight(float along) => along * (CellMetrics.SizeY * 0.5f);

        /// <summary>A ray from the play camera's angle aimed at a point on the drawn flight.</summary>
        static Ray AtTheFlight(float cellX, float cellZ)
        {
            float along = cellZ - LowZ;
            var target = new Vector3(
                cellX * CellMetrics.SizeXZ,
                Layer * CellMetrics.SizeY + DrawnHeight(along),
                cellZ * CellMetrics.SizeXZ);
            float radians = PlayCameraDegrees * Mathf.Deg2Rad;
            var direction = new Vector3(0f, -Mathf.Sin(radians), Mathf.Cos(radians)).normalized;
            return new Ray(target - direction * 60f, direction);
        }

        static RenderTestWorld StairOnFlatGround()
        {
            var world = new RenderTestWorld(8, 8, 4);
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
                world.Solid(x, z, 0, CoreContent.TerrainRock);
            return world.Stair(X, LowZ, Layer, facing: 0).Publish();
        }

        static CellRef Pick(RenderTestWorld world, float cellX, float cellZ)
        {
            Assert.That(
                SlicePicker.Pick(AtTheFlight(cellX, cellZ), world.Model, Layer, new SliceSettings(),
                    out CellRef cell),
                Is.True, $"a ray aimed at the stair at ({cellX}, {cellZ}) hit nothing at all");
            return cell;
        }

        [Test]
        public void EitherHalfOfAStairIsPickedByAimingAtTheFlightOverIt()
        {
            RenderTestWorld world = StairOnFlatGround();

            Assert.That(Pick(world, X + 0.5f, LowZ + 0.5f), Is.EqualTo(new CellRef(X, LowZ, Layer)),
                "the middle of the lower half");
            Assert.That(Pick(world, X + 0.5f, HighZ + 0.5f), Is.EqualTo(new CellRef(X, HighZ, Layer)),
                "the middle of the upper half");
        }

        [Test]
        public void EveryPointAlongADrawnFlightPicksTheCellItIsOver()
        {
            // The whole climb, in tenths of a cell. This is the test the report was about: it is
            // the *top* of the flight that drifts furthest, because that is where the drawn
            // surface stands highest above the cell answering for it.
            RenderTestWorld world = StairOnFlatGround();

            for (int step = 1; step < 20; step++)
            {
                // Not the seam itself: a ray aimed exactly at the boundary between the two cells
                // is ambiguous by a float, and either answer is the stair.
                if (step == 10) continue;

                float z = LowZ + step * 0.1f;
                int expected = z < HighZ ? LowZ : HighZ;
                Assert.That(Pick(world, X + 0.5f, z), Is.EqualTo(new CellRef(X, expected, Layer)),
                    $"aiming at the flight {step / 10f:0.0} cells up it");
            }
        }

        [Test]
        public void TheGroundInFrontOfAStairIsStillTheGround()
        {
            // The other half of the fault, and the half that would go unnoticed: a lid that is too
            // generous steals the grass in front of the flight and gives it to the stair.
            RenderTestWorld world = StairOnFlatGround();

            Assert.That(Pick(world, X + 0.5f, LowZ - 0.5f).Z, Is.EqualTo(LowZ - 1),
                "a click a whole cell in front of the stair must not reach it");
        }

        [Test]
        public void EachHalfStandsAsHighAsItIsDrawn()
        {
            // StandHeight is the seam and it must stay narrow — bare ground answers nought — but
            // the two halves are *different*, which the bed never had to be: one climbs to 1.5 m
            // and the other from 1.5 m to 3.0 m, so a single shared height would put half the
            // flight in the wrong place.
            RenderTestWorld world = StairOnFlatGround();
            var size = world.Model.Size;

            Assert.That(world.Model.StandHeight(size.Index(0, 0, Layer)), Is.EqualTo(0f));
            Assert.That(world.Model.StandHeight(size.Index(X, LowZ, Layer)),
                Is.EqualTo(CellMetrics.SizeY * 0.5f).Within(0.001f),
                "the lower half is drawn climbing to half a layer");
            Assert.That(world.Model.StandHeight(size.Index(X, HighZ, Layer)),
                Is.EqualTo(CellMetrics.SizeY).Within(0.001f),
                "the upper half starts where the lower one stopped and reaches the next floor");
        }
    }
}
