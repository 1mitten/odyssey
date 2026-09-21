#nullable enable

using NUnit.Framework;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The surround is charged to itself, not to the board.
    ///
    /// <para><b>Why this is worth a test.</b> The land beyond the board is submitted from inside
    /// <see cref="ChunkRenderer.Render"/> — it has to be, because it must reach the GPU before the
    /// board does so the depth buffer can reject it — and from the day the frame sections were
    /// written it was therefore charged to <c>FrameSection.World</c> along with the chunk buckets.
    /// So the one pass the project has spent a day cutting (3.65 ms to about 2.2,
    /// <c>docs/design/06-rendering-and-camera.md</c> §6c) was the one pass no instrument could
    /// name. A number covering both the board and the surround answers no question: they scale
    /// with completely different things.</para>
    ///
    /// <para>These are timing assertions, so they are deliberately loose. Neither asserts a
    /// budget — the budget lives in the documents, as §6c says — they assert only that the
    /// stopwatch brackets the surround and nothing else, which is the thing that would silently
    /// rot.</para>
    ///
    /// <para>The fast tier compiles neither Presentation nor Editor, so this file is only ever
    /// proved by the Unity tier.</para>
    /// </summary>
    public class SurroundCostTests
    {
        static RenderTestWorld Meadow(int size = 16)
        {
            var world = new RenderTestWorld(size, size, 4);
            for (int z = 0; z < size; z++)
            for (int x = 0; x < size; x++)
                world.Solid(x, z, 0, NaturalContent.TerrainGrass);
            return world.Publish();
        }

        static ChunkRenderer RendererFor(RenderTestWorld world, bool surround)
        {
            var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            renderer.Skirt.Enabled = surround;
            if (surround) renderer.Skirt.Build();
            return renderer;
        }

        /// <summary>
        /// Rendered twice and the second reading kept: the first call builds the skirt, and what
        /// this measures is the steady-state submission, which is all a frame after the first one
        /// ever pays.
        /// </summary>
        static double SurroundMsOf(RenderTestWorld world, bool surround)
        {
            using ChunkRenderer renderer = RendererFor(world, surround);
            // Above the surface layer, which is where the wood and the tufts are submitted at all.
            renderer.Render(1, new SliceSettings());
            renderer.Render(1, new SliceSettings());
            return renderer.SurroundMs;
        }

        [Test]
        public void TheSurroundChargesItsOwnSubmissionAndTheBoardDoesNot()
        {
            RenderTestWorld world = Meadow();

            double withSurround = SurroundMsOf(world, surround: true);
            double without = SurroundMsOf(world, surround: false);

            Assert.That(withSurround, Is.GreaterThan(0d),
                "the surround submitted batches and the stopwatch charged nothing for them");
            Assert.That(without, Is.LessThan(0.05d),
                "a disabled surround returns on its first line and cannot cost 50 us");
        }

        /// <summary>
        /// The counter is per frame, not cumulative. It is read straight onto the overlay every
        /// frame, so an accumulating figure would climb for ever and read as a leak.
        /// </summary>
        [Test]
        public void TheFigureIsThisFramesAndNotASum()
        {
            RenderTestWorld world = Meadow();
            using ChunkRenderer renderer = RendererFor(world, surround: true);

            renderer.Render(1, new SliceSettings());
            double first = renderer.SurroundMs;
            for (int i = 0; i < 8; i++) renderer.Render(1, new SliceSettings());
            double later = renderer.SurroundMs;

            Assert.That(later, Is.LessThan(first * 8d + 1d),
                "SurroundMs is adding frames together instead of replacing them");
        }
    }
}
