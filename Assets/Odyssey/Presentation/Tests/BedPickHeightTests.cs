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
    /// **A bed is clicked where it is drawn**, which it was not until 2026-09-19.
    ///
    /// <para>A bed does not occlude, so the only surface it used to offer a ray was the floor
    /// underneath it. At the play camera's 48° a surface 0.70 m up is drawn a quarter of a cell
    /// nearer the viewer than the floor it stands on, so the clickable bed and the drawn bed sat
    /// a quarter cell apart. Measured before the fix, aiming at the drawn mattress:</para>
    ///
    /// <code>
    ///   the grass in front of the bed  ->  the bed
    ///   the near half of the bed       ->  the bed's *other* cell
    ///   the far end of the bed         ->  the grass behind it
    /// </code>
    ///
    /// <para>The owner's report was that clicking a bed "seems to be really specific". These
    /// tests aim at the mattress, which is what a player aims at, and require the cell under the
    /// pointer to come back.</para>
    /// </summary>
    public class BedPickHeightTests
    {
        const int Head = 4, HeadZ = 4, FootZ = 5, Layer = 1;

        /// <summary>The play camera's own elevation, which is the angle the drift scales with.</summary>
        const float PlayCameraDegrees = 48f;

        /// <summary>
        /// A ray from the play camera's angle aimed at a point on the bed's top surface, in
        /// fractional cells. Nothing here is a metre by accident: the aim height is
        /// <see cref="BedShape"/>'s own, so a bed redrawn taller moves this test with it.
        /// </summary>
        static Ray AtTheMattress(float cellX, float cellZ)
        {
            var target = new Vector3(
                cellX * CellMetrics.SizeXZ,
                Layer * CellMetrics.SizeY + BedShape.MattressTop,
                cellZ * CellMetrics.SizeXZ);
            float radians = PlayCameraDegrees * Mathf.Deg2Rad;
            var direction = new Vector3(0f, -Mathf.Sin(radians), Mathf.Cos(radians)).normalized;
            return new Ray(target - direction * 60f, direction);
        }

        static RenderTestWorld BedOnFlatGround()
        {
            var world = new RenderTestWorld(8, 8, 4);
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
                world.Solid(x, z, 0, CoreContent.TerrainRock);
            return world.Bed(Head, HeadZ, Layer, facing: 0).Publish();
        }

        static CellRef Pick(RenderTestWorld world, float cellX, float cellZ)
        {
            Assert.That(
                SlicePicker.Pick(AtTheMattress(cellX, cellZ), world.Model, Layer, new SliceSettings(),
                    out CellRef cell),
                Is.True, $"a ray aimed at the bed at ({cellX}, {cellZ}) hit nothing at all");
            return cell;
        }

        [Test]
        public void EitherCellOfABedIsPickedByAimingAtTheBedInIt()
        {
            RenderTestWorld world = BedOnFlatGround();

            Assert.That(Pick(world, Head + 0.5f, HeadZ + 0.5f), Is.EqualTo(new CellRef(Head, HeadZ, Layer)),
                "the middle of the head cell");
            Assert.That(Pick(world, Head + 0.5f, FootZ + 0.5f), Is.EqualTo(new CellRef(Head, FootZ, Layer)),
                "the middle of the foot cell");
        }

        [Test]
        public void EveryPointAlongADrawnBedPicksTheCellItIsOver()
        {
            // The whole length of it, in tenths of a cell, and not one of them may answer with
            // ground. This is the test the report was about: it was the *ends* that failed, and
            // a check of the two centres alone would have passed before the fix.
            RenderTestWorld world = BedOnFlatGround();

            for (int step = 1; step < 20; step++)
            {
                // Not the boundary itself: a ray aimed exactly at the seam between the two cells
                // is ambiguous by a float, and either answer is the bed. The question here is
                // whether the *ends* work, not which side of a hair a middle click lands on.
                if (step == 10) continue;

                float z = HeadZ + step * 0.1f;
                int expected = z < FootZ ? HeadZ : FootZ;
                Assert.That(Pick(world, Head + 0.5f, z), Is.EqualTo(new CellRef(Head, expected, Layer)),
                    $"aiming at the bed {step / 10f:0.0} cells along it");
            }
        }

        [Test]
        public void TheGroundInFrontOfABedIsStillTheGround()
        {
            // The other half of the same fault, and the half that would go unnoticed: the drift
            // stole a quarter cell of grass in front of the bed and gave it to the bed.
            RenderTestWorld world = BedOnFlatGround();

            Assert.That(Pick(world, Head + 0.5f, HeadZ - 0.5f).Z, Is.EqualTo(HeadZ - 1),
                "a click a whole cell in front of the bed must not reach it");
        }

        [Test]
        public void NothingThatDoesNotStandUpGainsAHeight()
        {
            // StandHeight is the seam, and it must stay narrow: bare ground answers nought, or
            // every cell on the board grows an invisible lid a quarter cell out of place.
            RenderTestWorld world = BedOnFlatGround();
            var size = world.Model.Size;

            Assert.That(world.Model.StandHeight(size.Index(0, 0, Layer)), Is.EqualTo(0f));
            Assert.That(world.Model.StandHeight(size.Index(Head, HeadZ, Layer)), Is.GreaterThan(0f));
            Assert.That(world.Model.StandHeight(size.Index(Head, FootZ, Layer)), Is.GreaterThan(0f),
                "both cells of the bed stand, not just the head");
        }
    }
}
