#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The shape of the cursor a build drag draws: the run as one box, and one box per layer when
    /// the run steps.
    /// </summary>
    public class BuildPreviewTests
    {
        static CellRef At(int x, int z, int y) => new CellRef(x, z, y);

        readonly List<PreviewBox> _boxes = new List<PreviewBox>();

        /// <summary>
        /// The ordinary case and the one the owner asked for: a wall dragged over flat ground is
        /// one box, not six brackets.
        /// </summary>
        [Test]
        public void ARunOnOneLayerIsOneBox()
        {
            BuildPreview.Gather(At(3, 3, 5), At(8, 3, 5), (x, z) => 5, _boxes);

            Assert.That(_boxes.Count, Is.EqualTo(1));
            Assert.That(_boxes[0].Min, Is.EqualTo(At(3, 3, 5)));
            Assert.That(_boxes[0].Max, Is.EqualTo(At(8, 3, 5)));
            Assert.That(_boxes[0].Cells, Is.EqualTo(6));
        }

        /// <summary>A widened drag is still one box, because it is still one slab of order.</summary>
        [Test]
        public void AWidenedDragIsStillOneBox()
        {
            BuildPreview.Gather(At(2, 2, 4), At(4, 6, 4), (x, z) => 4, _boxes);

            Assert.That(_boxes.Count, Is.EqualTo(1));
            Assert.That(_boxes[0].Cells, Is.EqualTo(15));
        }

        /// <summary>
        /// The reason this is not simply the drag's own rectangle.
        ///
        /// <para>A build order stands on the cell above solid ground, decided per column, so a run
        /// across a terrace riser stands on two layers at once. One box around all of it would be
        /// a box around neither, and it would promise a wall where no wall is going.</para>
        /// </summary>
        [Test]
        public void ARunThatStepsUpIsOneBoxPerLayer()
        {
            // Four cells on the low ground, then three that have stepped up a riser.
            BuildPreview.Gather(At(0, 0, 4), At(6, 0, 4), (x, z) => x < 4 ? 4 : 5, _boxes);

            Assert.That(_boxes.Count, Is.EqualTo(2));

            Assert.That(_boxes[0].Min, Is.EqualTo(At(0, 0, 4)));
            Assert.That(_boxes[0].Max, Is.EqualTo(At(3, 0, 4)));

            Assert.That(_boxes[1].Min, Is.EqualTo(At(4, 0, 5)));
            Assert.That(_boxes[1].Max, Is.EqualTo(At(6, 0, 5)));
        }

        /// <summary>
        /// Layers come out in the order the walk first meets them, so the same drag submits the
        /// same instances in the same order every frame and on every machine.
        /// </summary>
        [Test]
        public void LayersComeOutInTheOrderTheyAreFirstMet()
        {
            BuildPreview.Gather(At(0, 0, 2), At(3, 0, 2), (x, z) => x == 0 ? 7 : 2, _boxes);

            Assert.That(_boxes[0].Min.Y, Is.EqualTo(7), "the high one was met first");
            Assert.That(_boxes[1].Min.Y, Is.EqualTo(2));
        }

        /// <summary>A click that never travelled is one cell, and one box. No special case.</summary>
        [Test]
        public void AClickIsOneBoxOfOneCell()
        {
            BuildPreview.Gather(At(9, 9, 3), At(9, 9, 3), (x, z) => 3, _boxes);

            Assert.That(_boxes.Count, Is.EqualTo(1));
            Assert.That(_boxes[0].Cells, Is.EqualTo(1));
        }

        /// <summary>
        /// Gathering twice does not accumulate: the list is the caller's, reused every frame, and
        /// a cursor that grew a box a frame would be a fine way to spend a morning.
        /// </summary>
        [Test]
        public void GatheringAgainReplacesWhatWasThereBefore()
        {
            BuildPreview.Gather(At(0, 0, 1), At(5, 0, 1), (x, z) => 1, _boxes);
            BuildPreview.Gather(At(0, 0, 1), At(2, 0, 1), (x, z) => 1, _boxes);

            Assert.That(_boxes.Count, Is.EqualTo(1));
            Assert.That(_boxes[0].Cells, Is.EqualTo(3));
        }
    }
}
