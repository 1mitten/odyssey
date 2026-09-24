#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The indirect scenery path decides what the chunk path decides (design 38 §22). The picture
    /// proof is PlayMode (<c>FrameTimeTests.TheIndirectSceneryDoesNotChangeThePicture</c>); this pins
    /// the one piece of arithmetic the path reimplements: the thinned prefix of a segment, taken from
    /// ranks captured at the regather, must equal <see cref="GrassThinning.CountBelow"/> on the
    /// bucket's own matrices, for every keep and at any offset inside a group.
    /// </summary>
    public class IndirectSceneryTests
    {
        static Matrix4x4[] SortedClumps(int count, int seed)
        {
            var random = new System.Random(seed);
            var matrices = new Matrix4x4[count];
            for (int i = 0; i < count; i++)
                matrices[i] = Matrix4x4.Translate(new Vector3(
                    (float)random.NextDouble() * 60f, 3f, (float)random.NextDouble() * 60f));
            float[] keys = new float[8];
            GrassThinning.SortByRank(matrices, count, ref keys);
            return matrices;
        }

        [Test]
        public void TheSegmentPrefixIsTheChunkPathsPrefix()
        {
            // Two segments one after the other, as a group holds them.
            Matrix4x4[] first = SortedClumps(137, 1), second = SortedClumps(211, 2);
            var ranks = new List<float>();
            foreach (Matrix4x4 m in first) ranks.Add(GrassThinning.Rank(in m));
            foreach (Matrix4x4 m in second) ranks.Add(GrassThinning.Rank(in m));

            foreach (float keep in new[] { 0f, 0.1f, 0.25f, 0.5f, 0.731f, 0.999f, 1f, 2f })
            {
                Assert.That(IndirectScenery.CountBelow(ranks, 0, first.Length, keep),
                    Is.EqualTo(GrassThinning.CountBelow(first, first.Length, keep)), $"first segment at keep {keep}");
                Assert.That(IndirectScenery.CountBelow(ranks, first.Length, second.Length, keep),
                    Is.EqualTo(GrassThinning.CountBelow(second, second.Length, keep)), $"second segment at keep {keep}");
            }
        }

        [Test]
        public void KeepingEverythingKeepsTheWholeSegment()
        {
            var ranks = new List<float> { 0.2f, 0.9f, 0.99f };
            Assert.That(IndirectScenery.CountBelow(ranks, 0, 3, 1f), Is.EqualTo(3));
            Assert.That(IndirectScenery.CountBelow(ranks, 1, 2, 5f), Is.EqualTo(2));
        }

        static Matrix4x4[] Marked(int count, float tag)
        {
            var m = new Matrix4x4[count];
            for (int i = 0; i < count; i++) m[i] = Matrix4x4.Translate(new Vector3(tag, i, 0f));
            return m;
        }

        static float TagAt(IndirectScenery.Group g, int chunk, int i) =>
            g.Matrices[g.Segments[g.SlotOf[chunk]].Start + i].m03;

        /// <summary>
        /// A re-meshed chunk rewrites its own slot in place, grows by moving to the end, and leaves
        /// every other chunk's instances where they were — the incremental regather that replaced
        /// the 3.9 ms whole-layer one on Huge (design 38 §22).
        /// </summary>
        [Test]
        public void AChunkRewritesOnlyItsOwnSlot()
        {
            var g = new IndirectScenery.Group();
            g.Write(1, Marked(3, 1f), 3);
            g.Write(2, Marked(5, 2f), 5);
            Assert.That(g.Live, Is.EqualTo(8));

            // Smaller: in place.
            int startBefore = g.Segments[g.SlotOf[1]].Start;
            g.Write(1, Marked(2, 10f), 2);
            Assert.That(g.Segments[g.SlotOf[1]].Start, Is.EqualTo(startBefore), "a smaller rewrite moved the slot");
            Assert.That(g.Live, Is.EqualTo(7));
            Assert.That(TagAt(g, 1, 0), Is.EqualTo(10f));
            Assert.That(TagAt(g, 2, 4), Is.EqualTo(2f), "rewriting chunk 1 disturbed chunk 2");

            // Larger than its room: moves, the old room is dead, nothing else moves.
            int usedBefore = g.Used;
            g.Write(1, Marked(40, 11f), 40);
            Assert.That(g.Segments[g.SlotOf[1]].Start, Is.GreaterThanOrEqualTo(usedBefore), "the grown slot did not move to the end");
            Assert.That(g.Dead, Is.GreaterThan(0));
            Assert.That(g.Live, Is.EqualTo(45));
            Assert.That(TagAt(g, 1, 39), Is.EqualTo(11f));
            Assert.That(TagAt(g, 2, 0), Is.EqualTo(2f));

            // Gone from the chunk: zero live, slot kept for the next write.
            g.ZeroChunk(2);
            Assert.That(g.Live, Is.EqualTo(40));
            Assert.That(g.Segments[g.SlotOf[2]].Count, Is.Zero);
        }

        [Test]
        public void CompactingKeepsEveryLiveInstanceAndDropsTheDeadSpace()
        {
            var g = new IndirectScenery.Group();
            g.Write(1, Marked(3, 1f), 3);
            g.Write(2, Marked(5, 2f), 5);
            g.Write(1, Marked(60, 3f), 60);
            int usedBefore = g.Used;
            g.Compact();
            Assert.That(g.Dead, Is.Zero);
            Assert.That(g.Used, Is.LessThan(usedBefore));
            Assert.That(g.Live, Is.EqualTo(65));
            Assert.That(TagAt(g, 1, 59), Is.EqualTo(3f));
            Assert.That(TagAt(g, 2, 4), Is.EqualTo(2f));
            // Every entry of a slot points back at that slot, so the cull reads the right decision.
            foreach (var kv in g.SlotOf)
            {
                IndirectScenery.Segment seg = g.Segments[kv.Value];
                for (int i = 0; i < seg.Capacity; i++)
                    Assert.That(g.SegmentOf[seg.Start + i], Is.EqualTo((uint)kv.Value));
            }
        }

        /// <summary>The path is on unless someone turns it off: the owner's frame drop was its reason.</summary>
        [Test]
        public void TheIndirectPathIsOnByDefault()
        {
            var world = new RenderTestWorld(4, 4, 2).Publish();
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            Assert.That(renderer.UseIndirectScenery, Is.True);
        }
    }
}
