#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen;
using UnityEngine;

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

        /// <summary>
        /// The culling box has to contain the geometry, and when it does not the symptom is whole
        /// chunks vanishing at the edge of the screen while every matrix in them is provably
        /// correct. The relief makes that easy to reintroduce, because it pushes cells out of the
        /// layer the box was built from in two ways at once: it lifts them, and then it tilts them
        /// so a corner reaches higher still.
        /// </summary>
        [Test]
        public void TheChunkBoxContainsEveryCornerOfEveryShearedCell()
        {
            GroundRelief.Reset();
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;
            try
            {
                var world = new RenderTestWorld(8, 8, 3);
                for (int z = 0; z < 8; z++)
                for (int x = 0; x < 8; x++)
                    world.Solid(x, z, 1);
                world.Publish();

                ChunkBatch batch = MeshLayer(world, 1);
                Assert.That(Instances(batch.Body), Is.GreaterThan(0), "there is ground to check");

                foreach (InstanceBucket bucket in batch.Body)
                for (int i = 0; i < bucket.Count; i++)
                {
                    Matrix4x4 m = bucket.Matrices[i];
                    // The bucket matrix already carries the module's own scale and offset, so
                    // the corners to test are the unit cube's.
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var local = new Vector3(
                            (corner & 1) == 0 ? -0.5f : 0.5f,
                            (corner & 2) == 0 ? -0.5f : 0.5f,
                            (corner & 4) == 0 ? -0.5f : 0.5f);

                        Vector3 world3 = m.MultiplyPoint3x4(local);
                        Assert.That(batch.Bounds.Contains(world3), Is.True,
                            $"corner {world3} escapes the chunk box {batch.Bounds}");
                    }
                }
            }
            finally
            {
                GroundRelief.Reset();
            }
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
            var slice = new SliceSettings
            {
                followDepth = false, above = AboveMode.Xray, aboveDepth = 2, belowDepth = 3,
            };

            Assert.That(slice.HighestDrawnLayer(5, 20), Is.EqualTo(7));
            Assert.That(slice.LowestDrawnLayer(5), Is.EqualTo(2));
            Assert.That(slice.GhostsAbove(5), Is.True);
        }

        [Test]
        public void HideDrawsNothingAboveTheSlice()
        {
            var slice = new SliceSettings { followDepth = false, above = AboveMode.Hide };
            Assert.That(slice.HighestDrawnLayer(5, 20), Is.EqualTo(5));
        }

        /// <summary>
        /// The owner's rule, 2026-09-16: at the depth the game opens at, everything at that level
        /// and above is drawn — no depth cap — because you have to be able to see somebody working
        /// over your head.
        ///
        /// <para>And drawn <b>solid</b>, which was the correction after the first attempt x-rayed
        /// it: "this includes everything buildings, stones, rocks and everything, as I noticed the
        /// mining rocks were transparent". An outcrop standing above the surface is a rock.</para>
        /// </summary>
        [Test]
        public void AtTheSurfaceEverythingAboveIsDrawnSolid()
        {
            var slice = new SliceSettings { surfaceLayer = 5, aboveDepth = 2 };

            Assert.That(slice.BelowSurface(5), Is.False);
            Assert.That(slice.HighestDrawnLayer(5, 20), Is.EqualTo(19),
                "the depth cap still applied at the surface");
            Assert.That(slice.AboveAt(5), Is.EqualTo(AboveMode.Full));
            Assert.That(slice.GhostsAbove(5), Is.False, "rock above the surface is still see-through");
            Assert.That(slice.HighestVisibleLayer(5, 20), Is.EqualTo(19),
                "solid geometry was cut off by a fade that does not apply to it");
        }

        /// <summary>
        /// Solid above is not the same as the `full` mode, although it draws the same. The active
        /// layer stays roofless, because that is its own decision and the default has no business
        /// reversing it — while an explicit `full`, the exterior view, keeps its lid.
        /// </summary>
        [Test]
        public void TheDefaultKeepsTheActiveLayerRooflessAndExplicitFullDoesNot()
        {
            var byDepth = new SliceSettings { surfaceLayer = 5 };
            Assert.That(byDepth.AboveAt(5), Is.EqualTo(AboveMode.Full));
            Assert.That(byDepth.SuppressCeilingAt(5), Is.True, "the default lidded the active layer");

            var chosen = new SliceSettings { followDepth = false, above = AboveMode.Full };
            Assert.That(chosen.SuppressCeilingAt(5), Is.False,
                "the exterior view cut away the storey above, which is the one thing it must not");
        }

        /// <summary>
        /// And the other half: underground the question reverses. One layer of ceiling for
        /// context, and every layer below, because what is under you is the shape of the working.
        /// </summary>
        [Test]
        public void UndergroundDrawsOneLayerAboveAndEveryLayerBelow()
        {
            var slice = new SliceSettings { surfaceLayer = 11, belowDepth = 3 };

            Assert.That(slice.BelowSurface(6), Is.True);
            Assert.That(slice.HighestDrawnLayer(6, 20), Is.EqualTo(7), "more than one ceiling layer");
            Assert.That(slice.LowestDrawnLayer(6), Is.EqualTo(0), "the depth cap still applied below");
            Assert.That(slice.AboveAt(6), Is.EqualTo(AboveMode.XrayMin));
        }

        /// <summary>
        /// Standing exactly at the surface is not underground, and one layer above it is not
        /// either. The boundary is worth pinning because the whole rule turns on it.
        /// </summary>
        [Test]
        public void TheSurfaceItselfCountsAsAboveGround()
        {
            var slice = new SliceSettings { surfaceLayer = 11 };

            Assert.That(slice.BelowSurface(12), Is.False);
            Assert.That(slice.BelowSurface(11), Is.False);
            Assert.That(slice.BelowSurface(10), Is.True);
        }

        /// <summary>
        /// Switching the default off hands every field back, which is what choosing a mode
        /// explicitly will do.
        /// </summary>
        [Test]
        public void TurningTheDefaultOffObeysTheFieldsExactly()
        {
            var slice = new SliceSettings
            {
                followDepth = false, surfaceLayer = 11, above = AboveMode.Hide, belowDepth = 3,
            };

            Assert.That(slice.AboveAt(6), Is.EqualTo(AboveMode.Hide));
            Assert.That(slice.HighestDrawnLayer(6, 20), Is.EqualTo(6));
            Assert.That(slice.LowestDrawnLayer(6), Is.EqualTo(3), "the cap was ignored below ground");
        }

        /// <summary>
        /// A figure is drawn on every layer the world is drawn on, and on no other. "Everything
        /// above" is bounded by the fade rather than by a count, so the answer has to stop where
        /// the geometry stops — or a colonist stands in rock that is no longer there.
        /// </summary>
        [Test]
        public void TheHighestVisibleLayerStopsWhereTheFadeDoes()
        {
            // Underground, where the treatment above is translucent and the fade therefore bites.
            var slice = new SliceSettings { followDepth = false, above = AboveMode.Xray, aboveDepth = 40 };
            int visible = slice.HighestVisibleLayer(0, 40);

            Assert.That(visible, Is.LessThan(39), "the fade never cut anything off");
            Assert.That(slice.AlphaAbove(0, visible - 0),
                Is.GreaterThanOrEqualTo(SliceSettings.MinVisibleAlpha),
                "the top visible layer is already invisible");
            Assert.That(slice.AlphaAbove(0, visible + 1 - 0),
                Is.LessThan(SliceSettings.MinVisibleAlpha),
                "the layer above the top visible one would still have been drawn");
        }

        /// <summary>Hide draws nothing above, so the visible top is the slice itself.</summary>
        [Test]
        public void TheHighestVisibleLayerIsTheSliceWhenNothingAboveIsDrawn()
        {
            var slice = new SliceSettings { followDepth = false, above = AboveMode.Hide };
            Assert.That(slice.HighestVisibleLayer(5, 20), Is.EqualTo(5));
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
            Assert.That(slice.AlphaAbove(5, 2), Is.LessThan(slice.AlphaAbove(5, 1)));
        }
    }
}
