#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What a marked board costs to draw.
    ///
    /// <para><b>P10, the fourth time.</b> An order's mark and a job's progress slab were each one
    /// <c>Graphics.RenderMesh</c> per cell, of the same unit cube in the same material, and
    /// neither incremented <see cref="ChunkRenderer.DrawCalls"/> — so a board carrying nine
    /// hundred mine orders submitted nine hundred times and reported the same draw count as a
    /// bare meadow. A submission costs about 4.6 us whatever is in it
    /// (<c>docs/design/06-rendering-and-camera.md</c> §6c), which made the pass worth about
    /// 2.2 ms of a 5 ms budget and invisible to every instrument the project had.</para>
    ///
    /// <para>These are the guards on the batching. The load-bearing one is
    /// <see cref="ABiggerMarkedAreaAddsInstancesRatherThanDraws"/>: it fails the moment marks
    /// cost draws in proportion to cells again, which is the shape of the fault rather than any
    /// one instance of it.</para>
    ///
    /// <para>The fast tier compiles neither Presentation nor Editor, so this file is only ever
    /// proved by the Unity tier.</para>
    /// </summary>
    public class CellPlateTests
    {
        /// <summary>Solid ground at layer 1, air above it — enough for a mark to find a height.</summary>
        static RenderTestWorld Board(int size = 16)
        {
            var world = new RenderTestWorld(size, size, 4);
            for (int z = 0; z < size; z++)
            for (int x = 0; x < size; x++)
                world.Solid(x, z, 1);
            return world;
        }

        static ChunkRenderer Marked(RenderTestWorld world, int side, Color colour)
        {
            var renderer = new ChunkRenderer(world.Model);
            for (int z = 0; z < side; z++)
            for (int x = 0; x < side; x++)
                renderer.DrawCellMark(new CellRef(x, z, 1), colour);
            return renderer;
        }

        /// <summary>Gathering submits nothing; the flush submits it all in one call.</summary>
        [Test]
        public void AHundredMarkedCellsAreOneInstancedCall()
        {
            var world = Board();
            world.Publish();
            using ChunkRenderer renderer = Marked(world, 10, Color.yellow);

            Assert.That(renderer.DrawCalls, Is.Zero, "gathering must not submit anything");
            Assert.That(renderer.PlatesGathered, Is.EqualTo(100), "every mark must be held");

            renderer.FlushCellPlates();

            Assert.That(renderer.DrawCalls, Is.EqualTo(1),
                "a hundred marked cells must be one call, not a hundred");
            Assert.That(renderer.InstancesDrawn, Is.EqualTo(100),
                "and every mark must actually be in it");
            Assert.That(renderer.PlatesGathered, Is.Zero, "the flush empties the buffer");
        }

        /// <summary>
        /// The guard on the pattern rather than on the instance: four times the marked cells,
        /// the same number of draws.
        ///
        /// <para>The sibling of <c>GrowingRenderTests.ABiggerFieldAddsInstancesRatherThanDraws</c>,
        /// and written for the same reason — a cost that scales with what the player has marked
        /// rather than with what is on screen is the tell for P10.</para>
        /// </summary>
        [Test]
        public void ABiggerMarkedAreaAddsInstancesRatherThanDraws()
        {
            var world = Board();
            world.Publish();

            using ChunkRenderer small = Marked(world, 5, Color.yellow);
            small.FlushCellPlates();

            using ChunkRenderer large = Marked(world, 10, Color.yellow);
            large.FlushCellPlates();

            Assert.That(large.DrawCalls, Is.EqualTo(small.DrawCalls),
                $"a hundred marks cost {large.DrawCalls} draws against twenty-five marks' " +
                $"{small.DrawCalls} — the pass is priced per cell again");
            Assert.That(large.InstancesDrawn, Is.EqualTo(small.InstancesDrawn * 4),
                "and all four times the marks must still be drawn");
        }

        /// <summary>
        /// A colour is a bucket, so two order colours are two calls and not two hundred. Four
        /// tools and a cut is the whole range a frame ever carries.
        /// </summary>
        [Test]
        public void EachMarkColourIsItsOwnCall()
        {
            var world = Board();
            world.Publish();
            using var renderer = new ChunkRenderer(world.Model);

            for (int x = 0; x < 50; x++) renderer.DrawCellMark(new CellRef(x % 16, 0, 1), Color.yellow);
            for (int x = 0; x < 50; x++) renderer.DrawCellMark(new CellRef(x % 16, 1, 1), Color.red);

            renderer.FlushCellPlates();

            Assert.That(renderer.DrawCalls, Is.EqualTo(2), "one call per colour");
            Assert.That(renderer.InstancesDrawn, Is.EqualTo(100));
        }

        /// <summary>
        /// A cut slab rides in the same bucket as a mark of the same colour: both are the unit
        /// cube in the bracket material, and a quarry in progress carries one of each per cell.
        /// </summary>
        [Test]
        public void ProgressSlabsBatchWithTheMarks()
        {
            var world = Board();
            world.Publish();
            using var renderer = new ChunkRenderer(world.Model);

            for (int x = 0; x < 16; x++)
            {
                renderer.DrawCellMark(new CellRef(x, 0, 1), Color.yellow);
                renderer.DrawCellCut(new CellRef(x, 0, 1), 0.5f, Color.yellow);
            }

            renderer.FlushCellPlates();

            Assert.That(renderer.DrawCalls, Is.EqualTo(1),
                "a mark and a cut of one colour are one call, not thirty-two");
            Assert.That(renderer.InstancesDrawn, Is.EqualTo(32));
        }

        /// <summary>Past the instancing cap a colour splits, and nothing is dropped at the seam.</summary>
        [Test]
        public void AColourPastTheInstancingCapSplitsAndKeepsEveryPlate()
        {
            var world = Board(40);
            world.Publish();
            using ChunkRenderer renderer = Marked(world, 30, Color.yellow);

            renderer.FlushCellPlates();

            Assert.That(renderer.DrawCalls, Is.EqualTo(2),
                $"900 plates at a cap of {ChunkRenderer.MaxInstancesPerCall} is two calls");
            Assert.That(renderer.InstancesDrawn, Is.EqualTo(900), "and none may be lost at the seam");
        }

        /// <summary>Flushing twice must not draw the same marks again — the buffer empties.</summary>
        [Test]
        public void FlushingTwiceDrawsThemOnce()
        {
            var world = Board();
            world.Publish();
            using var renderer = new ChunkRenderer(world.Model);

            renderer.DrawCellMark(new CellRef(3, 3, 1), Color.yellow);
            renderer.FlushCellPlates();
            int after = renderer.DrawCalls;

            renderer.FlushCellPlates();
            Assert.That(renderer.DrawCalls, Is.EqualTo(after), "an empty flush draws nothing");
        }

        /// <summary>
        /// A second frame reuses the buckets and draws its own marks, not the last frame's.
        ///
        /// <para>The buckets are kept across frames on purpose — the allocation is the thing
        /// being avoided — so the risk this covers is a stale count carrying yesterday's plates
        /// into today's call.</para>
        /// </summary>
        [Test]
        public void ASecondFrameDrawsItsOwnMarks()
        {
            var world = Board();
            world.Publish();
            using var renderer = new ChunkRenderer(world.Model);

            renderer.DrawCellMark(new CellRef(1, 1, 1), Color.yellow);
            renderer.FlushCellPlates();
            int first = renderer.InstancesDrawn;

            renderer.DrawCellMark(new CellRef(2, 2, 1), Color.yellow);
            renderer.DrawCellMark(new CellRef(3, 3, 1), Color.yellow);
            renderer.FlushCellPlates();

            Assert.That(renderer.InstancesDrawn - first, Is.EqualTo(2),
                "the second frame draws two plates, not three and not one");
        }
    }
}
