#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What the mesher chooses to draw, and — more importantly — what it chooses not to.
    /// </summary>
    public class ChunkMesherTests
    {
        static ChunkBatch MeshLayer(RenderTestWorld world, int layer)
        {
            var batch = new ChunkBatch();
            var mesher = new ChunkMesher(world.Model);
            int chunksPerLayer = world.Chunks.ChunksX * world.Chunks.ChunksZ;
            mesher.Mesh(batch, layer * chunksPerLayer);
            return batch;
        }

        static int Instances(List<InstanceBucket> buckets)
        {
            int n = 0;
            for (int i = 0; i < buckets.Count; i++) n += buckets[i].Count;
            return n;
        }

        [Test]
        public void AFreeStandingWallCellGetsAPanelOnEveryExposedFace()
        {
            var world = new RenderTestWorld(8, 8, 3)
                .Edifice(3, 3, 1, CoreContent.EdificeWall)
                .Publish();

            ChunkBatch batch = MeshLayer(world, 1);

            Assert.That(Instances(batch.Body), Is.EqualTo(4),
                "a wall cell with four open neighbours shows four faces");
        }

        [Test]
        public void AWallRunDrawsOnlyItsOutsideFaces()
        {
            // The middle of a three-cell wall run has wall on two sides, so only the two long
            // faces are drawn. This is the whole reason walls are meshed per face: a solid box per
            // wall cell would draw the hidden faces too and read as a 2.5 m thick slab.
            var world = new RenderTestWorld(8, 8, 3)
                .Edifice(2, 3, 1, CoreContent.EdificeWall)
                .Edifice(3, 3, 1, CoreContent.EdificeWall)
                .Edifice(4, 3, 1, CoreContent.EdificeWall)
                .Publish();

            var batch = new ChunkBatch();
            var mesher = new ChunkMesher(world.Model);
            mesher.Mesh(batch, world.Chunks.ChunksX * world.Chunks.ChunksZ * 1);

            // Three cells: the two ends show three faces each, the middle shows two.
            Assert.That(Instances(batch.Body), Is.EqualTo(8));
        }

        [Test]
        public void ADoorIsOneLeafInTheMiddleOfItsCell()
        {
            var world = new RenderTestWorld(8, 8, 3)
                .Edifice(2, 3, 1, CoreContent.EdificeWall)
                .Edifice(3, 3, 1, CoreContent.EdificeDoor, blocking: false)
                .Edifice(4, 3, 1, CoreContent.EdificeWall)
                .Publish();

            ChunkBatch batch = MeshLayer(world, 1);

            // Two walls at three faces each (the face towards the door is hidden by the door),
            // plus exactly one door leaf.
            Assert.That(Instances(batch.Body), Is.EqualTo(7));
        }

        [Test]
        public void BuriedStrataAreNotDrawn()
        {
            // A 3 x 3 block of rock: only the eight cells on the edge have an exposed face. The
            // centre one is enclosed and must not be submitted.
            var world = new RenderTestWorld(8, 8, 3);
            for (int x = 2; x <= 4; x++)
            for (int z = 2; z <= 4; z++)
            {
                world.Solid(x, z, 0);
                world.Solid(x, z, 1);
            }
            world.Publish();

            ChunkBatch batch = MeshLayer(world, 0);

            Assert.That(Instances(batch.Body), Is.EqualTo(8),
                "the enclosed centre cell of the lower layer is invisible and must not be drawn");
        }

        [Test]
        public void TheWorldBoundaryIsNotAnExposedFace()
        {
            // Fill a whole layer and bury it under another. Every cell is enclosed on all four
            // sides except the outermost ring, which is enclosed only by the edge of the world.
            //
            // That ring used to draw, which gave a flat map a cross-section wall around its entire
            // perimeter — as many cells tall as the slice drew layers below the surface. There is
            // no outside of the map, so there is nowhere those faces could be seen from.
            var world = new RenderTestWorld(8, 8, 3);
            for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
            {
                world.Solid(x, z, 0);
                world.Solid(x, z, 1);
            }
            world.Publish();

            Assert.That(Instances(MeshLayer(world, 0).Body), Is.Zero,
                "a fully buried layer is invisible, and the map edge does not expose it");
        }

        [Test]
        public void GroundStillShowsItsFacesWhereSomethingIsDugOutBesideIt()
        {
            // The boundary rule must not make interior faces disappear too. One cell cleared out
            // of an otherwise solid layer leaves its four neighbours each showing one face.
            var world = new RenderTestWorld(8, 8, 3);
            for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
            {
                if (x != 3 || z != 3) world.Solid(x, z, 0);   // one cell left as air
                world.Solid(x, z, 1);
            }
            world.Publish();

            Assert.That(Instances(MeshLayer(world, 0).Body), Is.EqualTo(4),
                "the four cells around a dug-out one are exposed and must still be drawn");
        }

        [Test]
        public void SlabsAndGroundGoInTheRoofListSoTheCutAwayCanDropThem()
        {
            var world = new RenderTestWorld(8, 8, 3)
                .Slab(3, 3, 1)
                .Surface(4, 4, 1)
                .Publish();

            ChunkBatch batch = MeshLayer(world, 1);

            Assert.That(Instances(batch.Roof), Is.EqualTo(2));
            Assert.That(Instances(batch.Body), Is.EqualTo(0));
        }

        [Test]
        public void RemeshingAChunkReusesItsBucketsAndDoesNotAccumulate()
        {
            var world = new RenderTestWorld(8, 8, 3)
                .Edifice(3, 3, 1, CoreContent.EdificeWall)
                .Publish();

            var batch = new ChunkBatch();
            var mesher = new ChunkMesher(world.Model);
            int chunk = world.Chunks.ChunksX * world.Chunks.ChunksZ;
            mesher.Mesh(batch, chunk);
            int first = Instances(batch.Body);
            int buckets = batch.Body.Count;

            mesher.Mesh(batch, chunk);

            Assert.That(Instances(batch.Body), Is.EqualTo(first), "a rebuild must replace, not append");
            Assert.That(batch.Body.Count, Is.EqualTo(buckets), "bucket arrays are reused across rebuilds");
        }
    }

    /// <summary>
    /// The slice table from 06-rendering-and-camera.md section 3, as arithmetic rather than prose.
    /// </summary>
    public class SliceSettingsTests
    {
        [Test]
        public void XrayDrawsAboveAndBelowWithinTheirCaps()
        {
            var slice = new SliceSettings { above = AboveMode.Xray, aboveDepth = 2, belowDepth = 3 };

            Assert.That(slice.HighestDrawnLayer(5, 20), Is.EqualTo(7));
            Assert.That(slice.LowestDrawnLayer(5), Is.EqualTo(2));
            Assert.That(slice.GhostsAbove, Is.True);
        }

        [Test]
        public void HideDrawsNothingAboveTheSlice()
        {
            var slice = new SliceSettings { above = AboveMode.Hide };
            Assert.That(slice.HighestDrawnLayer(5, 20), Is.EqualTo(5));
        }

        [Test]
        public void LayersBelowDarkenWithDepthAndNeverGoBlack()
        {
            var slice = new SliceSettings { below = BelowMode.Dim, belowFalloff = 0.6f };

            Assert.That(slice.ShadeBelow(0), Is.EqualTo(1f));
            Assert.That(slice.ShadeBelow(1), Is.LessThan(1f));
            Assert.That(slice.ShadeBelow(2), Is.LessThan(slice.ShadeBelow(1)));
            Assert.That(slice.ShadeBelow(8), Is.GreaterThan(0f));
        }

        [Test]
        public void LayersAboveFadeWithDistance()
        {
            var slice = new SliceSettings { above = AboveMode.Xray };
            Assert.That(slice.AlphaAbove(2), Is.LessThan(slice.AlphaAbove(1)));
        }
    }
}
