#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What a stockpile looks like in the chunk mesh (owner, 2026-09-23: "wash + edge outline"):
    /// the ground under it washed, and a line along its outer edge only — never the grid between
    /// its own cells.
    /// </summary>
    public class StoreEdgeTests
    {
        /// <summary>A 2 x 2 store at (1..2, 1..2) and a lone one at (5, 5), on ground at layer 0.</summary>
        static RenderTestWorld World()
        {
            var world = new RenderTestWorld(8, 8, 3);
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
                world.Solid(x, z, 0);
            world.Publish();

            var cells = new List<int>
            {
                world.Index(1, 1, 1), world.Index(2, 1, 1), world.Index(1, 2, 1), world.Index(2, 2, 1),
                world.Index(5, 5, 1),
            };
            cells.Sort();
            var stores = new StoreView[cells.Count];
            for (int i = 0; i < cells.Count; i++) stores[i] = new StoreView(cells[i], zone: 0, priority: 2);
            world.Model.UpdateStorage(stores);
            return world;
        }

        static ChunkBatch MeshLayer(RenderTestWorld world, int layer)
        {
            var batch = new ChunkBatch();
            new ChunkMesher(world.Model).Mesh(batch, layer * world.Chunks.ChunksX * world.Chunks.ChunksZ);
            return batch;
        }

        [Test]
        public void TheLineFollowsTheOuterEdgeAndNeverTheInsideOfAZone()
        {
            RenderTestWorld world = World();
            ChunkBatch batch = MeshLayer(world, 1);

            int strips = 0;
            foreach (InstanceBucket bucket in batch.Roof)
            {
                if (bucket.Module != world.Model.StoreEdgeModule) continue;
                Assert.That(TintCode.IsStoreEdge(bucket.Tint), Is.True, "an edge strip in a bucket that is not the edge's");
                strips += bucket.Count;
            }

            // A 2 x 2 block has eight outer sides and no inner one drawn; a lone cell has four.
            Assert.That(strips, Is.EqualTo(8 + 4));
        }

        [Test]
        public void TheGroundUnderEveryStoredCellIsWashed()
        {
            RenderTestWorld world = World();
            ChunkBatch batch = MeshLayer(world, 0);

            int washed = 0;
            foreach (InstanceBucket bucket in batch.Body)
                if (TintCode.IsStored(bucket.Tint) && !TintCode.IsTree(bucket.Tint)) washed += bucket.Count;
            foreach (InstanceBucket bucket in batch.Roof)
                if (TintCode.IsStored(bucket.Tint) && !TintCode.IsTree(bucket.Tint)) washed += bucket.Count;

            Assert.That(washed, Is.GreaterThanOrEqualTo(5), "the ground a layer down carries the wash for all five cells");
        }

        [Test]
        public void TheEdgeBitIsNeverReadOffATree()
        {
            for (int species = 0; species < 8; species++)
                Assert.That(TintCode.IsStoreEdge(TintCode.TreeBase | (species << TintCode.TreeShift)), Is.False);
            Assert.That(TintCode.IsStoreEdge(TintCode.StoreEdge()), Is.True);
        }
    }
}
