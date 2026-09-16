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
    /// The one rule the picker exists for: **a click never reaches a layer above the slice.**
    ///
    /// This is not a nicety. Going Medieval's most-reported complaint is misclicking something on
    /// another floor, with players reporting buildings deconstructed by accident, and
    /// 06-rendering-and-camera.md settles it in the renderer's favour: geometry above the slice is
    /// a depth cue and nothing more. It costs nothing to hold now and is unpleasant to retrofit,
    /// so it is pinned by a test from the first commit.
    /// </summary>
    public class SlicePickerTests
    {
        /// <summary>A ray looking down at cell (x, z) from high above, at the given angle.</summary>
        static Ray DownAt(int x, int z, float height = 60f)
        {
            var target = new Vector3(
                (x + 0.5f) * CellMetrics.SizeXZ, 0f, (z + 0.5f) * CellMetrics.SizeXZ);
            var origin = target + new Vector3(0f, height, 0f);
            return new Ray(origin, Vector3.down);
        }

        [Test]
        public void AWallOnTheLayerAboveIsNeverPicked()
        {
            // A floor at layer 1 under the cursor, and a wall directly above it at layer 2. The
            // ray passes straight through the wall on its way down.
            var world = new RenderTestWorld(8, 8, 4)
                .Slab(4, 4, 1)
                .Edifice(4, 4, 2, CoreContent.EdificeWall)
                .Publish();

            bool hit = SlicePicker.Pick(DownAt(4, 4), world.Model, activeLayer: 1, out CellRef cell);

            Assert.That(hit, Is.True, "the floor of the active layer should still be pickable");
            Assert.That(cell.Y, Is.EqualTo(1), "the pick must not climb to the wall above the slice");
            Assert.That(cell, Is.EqualTo(new CellRef(4, 4, 1)));
        }

        [Test]
        public void GeometryAboveIsNotPickedEvenWhenTheActiveLayerIsEmpty()
        {
            // Nothing at all on the active layer, a full storey of wall above it. A picker that
            // raycast the drawn scene would happily return the wall; this one returns nothing.
            var world = new RenderTestWorld(8, 8, 4)
                .Edifice(4, 4, 3, CoreContent.EdificeWall)
                .Edifice(4, 4, 2, CoreContent.EdificeWall)
                .Publish();

            bool hit = SlicePicker.Pick(DownAt(4, 4), world.Model, activeLayer: 1, out CellRef cell);

            Assert.That(hit, Is.False, $"empty active layer must pick nothing, got {cell}");
        }

        [Test]
        public void EveryPickOnAFullColumnLandsOnTheActiveLayer()
        {
            // Solid rock all the way up the column, at every layer. Whatever the slice is set to,
            // the answer is always that layer and never another.
            var world = new RenderTestWorld(6, 6, 6);
            for (int y = 0; y < 6; y++) world.Solid(3, 3, y);
            world.Publish();

            for (int layer = 0; layer < 6; layer++)
            {
                bool hit = SlicePicker.Pick(DownAt(3, 3), world.Model, layer, out CellRef cell);
                Assert.That(hit, Is.True, $"layer {layer} should hit its own rock");
                Assert.That(cell.Y, Is.EqualTo(layer));
            }
        }

        /// <summary>
        /// The guarantee the whole picker exists to make, now that the ground is not where the
        /// grid says it is.
        ///
        /// Going Medieval's most-reported complaint is misclicking, and relief reintroduces it by
        /// the back door: the ground is drawn up to two metres off its layer, so a ray tested
        /// against the flat plane crosses it in the wrong cell. At a shallow pitch that is most of
        /// a cell out. The picker must meet the tilted floor it actually draws.
        /// </summary>
        [Test]
        public void AClickOnASlopeLandsOnTheCellTheCursorIsOver()
        {
            GroundRelief.Reset();
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;
            try
            {
                var world = new RenderTestWorld(40, 40, 3);
                for (int z = 0; z < 40; z++)
                for (int x = 0; x < 40; x++)
                    world.Solid(x, z, 0);
                world.Publish();

                // A shallow, three-quarter ray of the kind the board camera casts - the worst case
                // for a height error, because a vertical mistake becomes a long horizontal one.
                var direction = new Vector3(0.6f, -0.5f, 0.6f).normalized;

                int checkedCells = 0;
                for (int target = 6; target < 30; target += 3)
                {
                    Vector3 centre = CellMetrics.FloorCentre(target, target, 1);
                    // Aim at the point on the DRAWN ground, which is where the player is pointing.
                    Vector3 aim = GroundRelief.Lift(centre);
                    var ray = new Ray(aim - direction * 60f, direction);

                    bool hit = SlicePicker.Pick(ray, world.Model, activeLayer: 1, out CellRef cell);

                    // Exactly the cell aimed at, not merely near it. A tolerance of one cell is
                    // worse than no test at all here: the error a flat floor plane produces at
                    // this amplitude is almost exactly one cell, so a loose assertion passes just
                    // as happily with the bug in place. Checked by putting the bug back.
                    Assert.That(hit, Is.True, $"the ray at {aim} should meet the ground");
                    Assert.That(cell.X, Is.EqualTo(target),
                        $"clicked the drawn ground of cell {target} but picked {cell.X}");
                    Assert.That(cell.Z, Is.EqualTo(target));
                    checkedCells++;
                }

                Assert.That(checkedCells, Is.GreaterThan(4), "the case was actually exercised");
            }
            finally
            {
                GroundRelief.Reset();
            }
        }

        [Test]
        public void ASlantedRayStopsAtTheFirstWallOnTheActiveLayer()
        {
            // A wall two cells along the ray's path, and a second wall behind it. Only the near
            // one may be returned: the picker marches and stops, it does not take the last hit.
            var world = new RenderTestWorld(10, 10, 3)
                .Edifice(5, 2, 1, CoreContent.EdificeWall)
                .Edifice(7, 2, 1, CoreContent.EdificeWall)
                .Publish();

            var origin = new Vector3(1f * CellMetrics.SizeXZ, 1 * CellMetrics.SizeY + 1.5f, 2.5f * CellMetrics.SizeXZ);
            var ray = new Ray(origin, Vector3.right);

            bool hit = SlicePicker.Pick(ray, world.Model, activeLayer: 1, out CellRef cell);

            Assert.That(hit, Is.True);
            Assert.That(cell, Is.EqualTo(new CellRef(5, 2, 1)));
        }

        [Test]
        public void ARayThatMissesTheMapPicksNothing()
        {
            var world = new RenderTestWorld(8, 8, 3).Slab(4, 4, 1).Publish();
            var ray = new Ray(new Vector3(-50f, 20f, -50f), Vector3.up);

            Assert.That(SlicePicker.Pick(ray, world.Model, 1, out _), Is.False);
        }

        [Test]
        public void AnOpenCellWithNoFloorIsNotPicked()
        {
            // A hole in the floor is a hole, not a surface: clicking through it selects nothing on
            // this layer rather than selecting the void.
            var world = new RenderTestWorld(8, 8, 3).Slab(4, 4, 1).Publish();

            Assert.That(SlicePicker.Pick(DownAt(5, 5), world.Model, 1, out _), Is.False);
            Assert.That(SlicePicker.Pick(DownAt(4, 4), world.Model, 1, out _), Is.True);
        }

        [Test]
        public void SolidGroundBeneathCountsAsAFloor()
        {
            var world = new RenderTestWorld(8, 8, 3).Solid(4, 4, 0).Publish();

            bool hit = SlicePicker.Pick(DownAt(4, 4), world.Model, activeLayer: 1, out CellRef cell);

            Assert.That(hit, Is.True);
            Assert.That(cell, Is.EqualTo(new CellRef(4, 4, 1)));
        }
    }
}
