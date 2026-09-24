#nullable enable

using NUnit.Framework;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// Trees grouped across chunks, and simpler far away (<c>docs/design/38-meadow-overhaul.md</c>
    /// §23).
    ///
    /// <para>Measured before this (<c>FrameTimeTests.TheTreesAgainstTheFrame</c>): a board's trees
    /// went out four and a half to five a draw call, because every chunk submitted its own, and
    /// that submission was up to 1.33 ms of CPU on Huge zoomed out. These pin the two halves of the
    /// fix without a GPU — a draw call is counted whether or not it is sent — and the picture is
    /// proved unchanged in PlayMode (<c>GroupingTheTreesDoesNotChangeThePicture</c>).</para>
    /// </summary>
    public class TreeGroupingTests
    {
        /// <summary>Five by five chunks of meadow with a tree on every third cell.</summary>
        static RenderTestWorld Wood()
        {
            const int side = 125;
            var world = new RenderTestWorld(side, side, 4);
            for (int z = 0; z < side; z++)
            for (int x = 0; x < side; x++)
            {
                world.Solid(x, z, 1, NaturalContent.TerrainGrass);
                if ((x + z * 7) % 3 == 0)
                    world.Edifice(x, z, 2, NaturalContent.EdificeTreeBroadleaf, blocking: false);
            }
            return world.Publish();
        }

        static ChunkRenderer RendererFor(RenderTestWorld world)
        {
            var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false, ScatterDensity = 0 };
            renderer.Skirt.Enabled = false;
            renderer.MeshBudgetPerFrame = 0;
            return renderer;
        }

        [Test]
        public void GroupingSendsTheSameTreesInFarFewerCalls()
        {
            RenderTestWorld world = Wood();
            using ChunkRenderer renderer = RendererFor(world);
            renderer.PrimeAll(2, new SliceSettings());

            renderer.GroupTrees = false;
            renderer.Render(2, new SliceSettings());
            int perChunkCalls = renderer.ChunkCallsByKind[0];
            int drawnPerChunk = renderer.InstancesDrawn;
            int trees = renderer.TreeInstances;
            Assert.That(trees, Is.GreaterThan(1000), "the wood grew too few trees to say anything about batching");
            Assert.That(perChunkCalls, Is.GreaterThanOrEqualTo(25), "each chunk should have submitted its own trees");

            renderer.GroupTrees = true;
            renderer.Render(2, new SliceSettings());
            int groupedCalls = renderer.ChunkCallsByKind[0];

            Assert.That(renderer.InstancesDrawn, Is.EqualTo(drawnPerChunk),
                "grouping changed what was drawn, not only how it was submitted");
            Assert.That(renderer.GroupedTreeInstances, Is.GreaterThanOrEqualTo(trees),
                "some trees were submitted outside the groups");
            Assert.That(groupedCalls, Is.LessThanOrEqualTo(perChunkCalls / 5),
                $"{groupedCalls} grouped calls against {perChunkCalls} chunk by chunk: the trees were not grouped");
            Assert.That(renderer.GroupedTreeCalls, Is.EqualTo(groupedCalls),
                "a tree call went out that the flush did not account for");
        }

        [Test]
        public void TheGatherIsEmptiedEveryFrame()
        {
            RenderTestWorld world = Wood();
            using ChunkRenderer renderer = RendererFor(world);
            renderer.PrimeAll(2, new SliceSettings());

            renderer.Render(2, new SliceSettings());
            int first = renderer.GroupedTreeInstances;
            renderer.Render(2, new SliceSettings());
            Assert.That(renderer.GroupedTreeInstances, Is.EqualTo(first),
                "a second frame submitted a different number of trees: last frame's gather was not emptied");
        }

        [Test]
        public void NearTreesKeepTheirBiasAndFarTreesTakeTheLowerOne()
        {
            RenderTestWorld world = Wood();
            using ChunkRenderer renderer = RendererFor(world);
            renderer.TreeLodBias = 3f;
            renderer.FarTreeLodBias = 1f;
            renderer.FarTreeNear = 60f;
            renderer.FarTreeFar = 120f;

            Assert.That(renderer.TreeBiasAt(10f), Is.EqualTo(3f), "a near tree changed");
            Assert.That(renderer.TreeBiasAt(60f), Is.EqualTo(3f), "the near band's edge changed");
            Assert.That(renderer.TreeBiasAt(90f), Is.EqualTo(2f).Within(1e-4f), "the ramp is not halfway at halfway");
            Assert.That(renderer.TreeBiasAt(120f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(renderer.TreeBiasAt(1000f), Is.EqualTo(1f).Within(1e-4f), "past the ramp the far bias holds");

            renderer.SimplerFarTrees = false;
            Assert.That(renderer.TreeBiasAt(1000f), Is.EqualTo(3f), "switched off, every tree keeps today's bias");

            renderer.SimplerFarTrees = true;
            renderer.FarTreeLodBias = 5f;
            Assert.That(renderer.TreeBiasAt(1000f), Is.EqualTo(3f),
                "a far bias above the near one would make far trees finer, which is never the point");
        }
    }
}
