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
        /// The seed day draws no plant: stage nought is the specks' day, and the module that
        /// arrived at growth nought was a sprout standing by afternoon (owner, 2026-09-19).
        /// The count is still the yield - the plot knows what it will give - and only the art
        /// waits, which is why this asserts the module and not the count.
        /// </summary>
        /// <summary>
        /// A zone is painted on the air the colonist stands in, and the soil it tills is the
        /// cell beneath her feet - so the drawn-terrain swap and the tuft pull both ask the
        /// zone ONE LAYER UP from the ground they are deciding about. The first version asked
        /// the ground cell itself, the answer was always no, and the tilled-earth swap never
        /// fired: every brown tile the owner had ever seen was the cover overlay (2026-09-19).
        /// </summary>
        [Test]
        public void TilledGroundAsksTheZoneOneLayerUp()
        {
            var world = Field();
            world.Solid(4, 4, 1, Odyssey.Sim.Worldgen.Natural.NaturalContent.TerrainGrass);
            world.Solid(5, 5, 1, Odyssey.Sim.Worldgen.Natural.NaturalContent.TerrainGrass);
            int zonedGround = world.Index(4, 4, 1);
            int zonedAir = world.Index(4, 4, 2);

            world.Publish();

            Assert.That(world.Model.DrawnTerrain(zonedGround),
                Is.EqualTo(Odyssey.Sim.Worldgen.Natural.NaturalContent.TerrainGrass),
                "no zone, no swap");

            world.Model.UpdateZones(new ZoneView[] { new ZoneView(zonedAir, 0) });

            Assert.That(world.Model.IsZoned(zonedGround), Is.True,
                "the ground under a zone is tilled ground");
            Assert.That(world.Model.DrawnTerrain(zonedGround),
                Is.EqualTo(Odyssey.Sim.Worldgen.Natural.NaturalContent.TerrainBareEarth),
                "the swap fires through the layer ask");
            Assert.That(world.Model.DrawnTerrain(world.Index(5, 5, 1)),
                Is.EqualTo(Odyssey.Sim.Worldgen.Natural.NaturalContent.TerrainGrass),
                "a neighbour outside the zone keeps its grass");
            Assert.That(world.Model.IsZoned(world.Index(5, 5, 1)), Is.False);

            world.Model.UpdateZones(System.Array.Empty<ZoneView>());
            Assert.That(world.Model.DrawnTerrain(zonedGround),
                Is.EqualTo(Odyssey.Sim.Worldgen.Natural.NaturalContent.TerrainGrass),
                "an unpainted field reverts to what it was");
        }
        [Test]
        public void TheSeedDayDrawsNoPlantAndStillKnowsItsYield()
        {
            var world = Field();
            int cell = world.Index(3, 3, 2);

            world.Model.UpdateCrops(new PlantView[] { new PlantView(cell, 0, 0, 0) });
            Assert.That(world.Model.CropModule(cell), Is.EqualTo(0),
                "a seed in the soil stands nothing above it for its first day of light");
            Assert.That(world.Model.CropCount(cell), Is.EqualTo(5),
                "the plot still answers its yield: what waits is the art, not the count");
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
            // The plot draws its yield (owner, 2026-09-18: the number of carrots growing is the
            // amount in the plot), so the carrot's five is the instance count - and still ONE
            // bucket, which is the whole argument that it is free.
            Assert.That(buckets[0].Count, Is.EqualTo(world.Model.CropCount(cell)),
                "a plot draws as many plants as its harvest will yield");

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
