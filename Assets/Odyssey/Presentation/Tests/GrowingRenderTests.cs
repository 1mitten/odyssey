#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What the renderer does with a standing crop: that the mirror takes one from the
    /// snapshot's crop channel, that the mesher turns it into ordinary bucket instances, and
    /// that a clone with no catalogue still draws something honest rather than nothing at all.
    ///
    /// <para>The fast tier compiles neither Presentation nor Editor, so this file is the proof
    /// of the crop half of U48 — nothing in <c>tools/dotnet</c> sees any of it.</para>
    /// </summary>
    public class GrowingRenderTests
    {
        /// <summary>A turf at layer 1, the crop's air cell at layer 2, and a layer 3 above it
        /// for a roof — a zone cell is air, so the field is one layer taller than its soil.</summary>
        static RenderTestWorld Field()
        {
            var world = new RenderTestWorld(8, 8, 4);
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
                world.Solid(x, z, 1);
            return world;
        }

        static List<InstanceBucket> BucketsFor(ChunkBatch batch, int module)
        {
            var found = new List<InstanceBucket>();
            foreach (InstanceBucket bucket in batch.Body)
                if (bucket.Module == module)
                    found.Add(bucket);
            return found;
        }

        /// <summary>
        /// The mirror answers the plant the snapshot published, at the stage it published, and
        /// forgets both when the channel stops carrying the cell — a harvest that left no mark
        /// would otherwise draw a field of crops the simulation had already taken out.
        /// </summary>
        [Test]
        public void TheCropMirrorStandsAStageAndClearsIt()
        {
            var world = Field();
            int cell = world.Index(3, 3, 2);

            Assert.That(world.Model.CropModule(cell), Is.EqualTo(0), "a bare field draws no crop");

            world.Model.UpdateCrops(new PlantView[] { new PlantView(cell, 0, 1, 0) });
            int sprout = world.Model.CropModule(cell);
            Assert.That(sprout, Is.Not.EqualTo(0), "a published crop resolves to a module");

            world.Model.UpdateCrops(new PlantView[] { new PlantView(cell, 0, 3, 255) });
            int mature = world.Model.CropModule(cell);
            Assert.That(mature, Is.Not.EqualTo(sprout),
                "the drawn stages are different buckets, or ripening would redraw the same picture");

            world.Model.UpdateCrops(System.Array.Empty<PlantView>());
            Assert.That(world.Model.CropModule(cell), Is.EqualTo(0), "an uprooted crop stops drawing");
        }

        /// <summary>
        /// The crop goes through the ordinary bucket machinery: one instance of the stage's
        /// module in the chunk it stands in, exactly as a tuft or a wall panel is. A second way
        /// of drawing crops — a renderer of its own, a GameObject per plant — is what this test
        /// exists to refuse.
        /// </summary>
        [Test]
        public void AStandingCropIsOneInstancedBodyInItsChunk()
        {
            var world = Field();
            int cell = world.Index(3, 3, 2);
            world.Model.UpdateCrops(new PlantView[] { new PlantView(cell, 0, 2, 128) });

            int module = world.Model.CropModule(cell);
            ChunkBatch batch = MeshOneChunk(world, 2);

            List<InstanceBucket> buckets = BucketsFor(batch, module);
            Assert.That(buckets, Has.Count.EqualTo(1), "one stage of one crop is one bucket");
            Assert.That(buckets[0].Count, Is.EqualTo(1), "one plant is one instance");

            Vector3 at = buckets[0].Matrices[0].MultiplyPoint3x4(Vector3.zero);
            Assert.That(at.y, Is.EqualTo(2f * 3f).Within(0.35f),
                "the crop stands at the floor of its own cell — the ground surface, not three metres up");
        }

        /// <summary>
        /// A crop under a roof draws dimmer than one in the open, by the same daylit bit the
        /// ground and the tufts read — the bucket is keyed by it, so the two crops are two
        /// buckets and neither re-tints the other.
        /// </summary>
        [Test]
        public void ACropUnderARoofIsDaylitDifferentlyFromOneInTheOpen()
        {
            var world = Field();
            world.Solid(4, 4, 3); // the roof
            world.Model.UpdateCrops(new PlantView[]
            {
                new PlantView(world.Index(3, 3, 2), 0, 2, 128),
                new PlantView(world.Index(4, 4, 2), 0, 2, 128),
            });

            int module = world.Model.CropModule(world.Index(3, 3, 2));
            ChunkBatch batch = MeshOneChunk(world, 2);

            List<InstanceBucket> buckets = BucketsFor(batch, module);
            Assert.That(buckets, Has.Count.EqualTo(2),
                "the same crop daylit two ways is two buckets");
            Assert.That(TintCode.IsDaylit(buckets[0].Tint),
                Is.Not.EqualTo(TintCode.IsDaylit(buckets[1].Tint)),
                "exactly one of the two is lit as open to the sky");
        }

        /// <summary>
        /// A pack-less checkout — which is what this test runs on, its library built with no
        /// catalogue at all — still draws the crop, as a fallback primitive. A crop that
        /// vanished without its art would take the player's field with it: the zone is painted,
        /// the colonists sow it, and nothing would say so.
        /// </summary>
        [Test]
        public void ACropWithoutArtStillDrawsAsItsFallback()
        {
            var world = Field();
            int cell = world.Index(3, 3, 2);
            world.Model.UpdateCrops(new PlantView[] { new PlantView(cell, 0, 1, 0) });

            int module = world.Model.CropModule(cell);
            Assert.That(module, Is.Not.EqualTo(0));

            var resolved = world.Library[module];
            Assert.That(resolved.UsesArt, Is.False, "this library has no catalogue to find art in");
            Assert.That(resolved.IsEmpty, Is.False, "the fallback is a real part, not a hole");
            Assert.That(resolved.Parts[0].IsFallback, Is.True, "and it is the fallback part");

            ChunkBatch batch = MeshOneChunk(world, 2);
            Assert.That(BucketsFor(batch, module), Is.Not.Empty,
                "the fallback draws through the same buckets the art would");
        }

        static ChunkBatch MeshOneChunk(RenderTestWorld world, int layer)
        {
            // Published first, as the frame path does: the daylit query reads the geometry
            // mirror RefreshAll fills, and an unpublished roof reads as open sky.
            world.Publish();
            var batch = new ChunkBatch();
            var mesher = new ChunkMesher(world.Model);
            int chunksPerLayer = world.Chunks.ChunksX * world.Chunks.ChunksZ;
            mesher.Mesh(batch, layer * chunksPerLayer);
            return batch;
        }
    }
}
