#nullable enable

using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The surround reads the board rather than being told about it, so these tests state a board
    /// and ask what grew up around it.
    ///
    /// Nothing is submitted to a GPU: <c>SubmitToGpu</c> off does everything except the draw,
    /// which is what makes the instance and call counts measurable in a headless editor run.
    /// </summary>
    public class TerrainSkirtTests
    {
        static RenderTestWorld Meadow(int trees)
        {
            var world = new RenderTestWorld(12, 12, 4);
            for (int z = 0; z < 12; z++)
            for (int x = 0; x < 12; x++)
                world.Solid(x, z, 0, NaturalContent.TerrainGrass);

            // Trees stand in the layer above the ground they grow out of, and block nothing.
            int planted = 0;
            for (int z = 0; z < 12 && planted < trees; z++)
            for (int x = 0; x < 12 && planted < trees; x++, planted++)
                world.Edifice(x, z, 1, NaturalContent.EdificeTreeConifer, blocking: false);

            return world.Publish();
        }

        static TerrainSkirt SkirtFor(RenderTestWorld world, out MaterialCache materials)
        {
            materials = new MaterialCache();
            var skirt = new TerrainSkirt(world.Model, materials) { SubmitToGpu = false };
            skirt.Build();
            return skirt;
        }

        [Test]
        public void TheSurroundSitsAtTheBoardsOwnSurfaceLevel()
        {
            var world = Meadow(trees: 0);
            TerrainSkirt skirt = SkirtFor(world, out MaterialCache materials);
            try
            {
                Assert.That(skirt.SurfaceLayer, Is.Zero);
                Assert.That(skirt.GroundInstances, Is.GreaterThan(0), "there is ground out there");
            }
            finally
            {
                skirt.Dispose();
                materials.Dispose();
            }
        }

        [Test]
        public void ABareBoardGetsNoWoodAndAWoodedOneDoes()
        {
            var bare = Meadow(trees: 0);
            TerrainSkirt bareSkirt = SkirtFor(bare, out MaterialCache bareMaterials);
            var wooded = Meadow(trees: 40);
            TerrainSkirt woodedSkirt = SkirtFor(wooded, out MaterialCache woodedMaterials);

            try
            {
                Assert.That(bareSkirt.MeasuredTreeDensity, Is.Zero);
                Assert.That(bareSkirt.TreeInstances, Is.Zero,
                    "a surround is a continuation of the board, not a second opinion about it");

                Assert.That(woodedSkirt.MeasuredTreeDensity, Is.GreaterThan(0));
                Assert.That(woodedSkirt.TreeInstances, Is.GreaterThan(0),
                    "the wood carries on past the rim");
            }
            finally
            {
                bareSkirt.Dispose();
                bareMaterials.Dispose();
                woodedSkirt.Dispose();
                woodedMaterials.Dispose();
            }
        }

        [Test]
        public void TheWoodThinsWhenAskedToAndGoesWhenToldTo()
        {
            var world = Meadow(trees: 40);
            var materials = new MaterialCache();
            var full = new TerrainSkirt(world.Model, materials) { SubmitToGpu = false };
            var thin = new TerrainSkirt(world.Model, materials)
            {
                SubmitToGpu = false,
                TreeDensityPercent = 25,
            };
            var none = new TerrainSkirt(world.Model, materials)
            {
                SubmitToGpu = false,
                TreeDensityPercent = 0,
            };

            try
            {
                full.Build();
                thin.Build();
                none.Build();

                Assert.That(thin.TreeInstances, Is.LessThan(full.TreeInstances));
                Assert.That(none.TreeInstances, Is.Zero);
                Assert.That(none.GroundInstances, Is.EqualTo(full.GroundInstances),
                    "the ground is not the expensive half and does not go with the trees");
            }
            finally
            {
                full.Dispose();
                thin.Dispose();
                none.Dispose();
                materials.Dispose();
            }
        }

        [Test]
        public void SlicingBelowTheSurfaceTakesTheSurroundWithIt()
        {
            // A sheet of landscape sitting over an open mine would bury the very thing the player
            // went down to look at, so below ground level the surround is simply not there.
            var world = new RenderTestWorld(12, 12, 4);
            for (int z = 0; z < 12; z++)
            for (int x = 0; x < 12; x++)
            {
                world.Solid(x, z, 0, NaturalContent.TerrainGrass);
                world.Solid(x, z, 1, NaturalContent.TerrainGrass);
            }

            world.Edifice(2, 2, 2, NaturalContent.EdificeTreeConifer, blocking: false);
            world.Publish();

            TerrainSkirt skirt = SkirtFor(world, out MaterialCache materials);
            try
            {
                Assert.That(skirt.SurfaceLayer, Is.EqualTo(1));

                skirt.Render(activeLayer: 0);
                Assert.That(skirt.InstancesDrawn, Is.Zero, "underground, there is no horizon");

                skirt.Render(activeLayer: 1);
                int ground = skirt.InstancesDrawn;
                Assert.That(ground, Is.GreaterThan(0), "at the surface the ground is back");

                skirt.Render(activeLayer: 2);
                Assert.That(skirt.InstancesDrawn, Is.GreaterThanOrEqualTo(ground),
                    "and the trees stand a layer higher again");
            }
            finally
            {
                skirt.Dispose();
                materials.Dispose();
            }
        }

        [Test]
        public void ACloneWithoutThePacksGetsNoStrewnGreyCubes()
        {
            // RenderTestWorld resolves every module to a tinted primitive, which is the path a
            // clone without the licensed art takes. A box where a wall should be is still a wall;
            // a box where a tuft of grass should be is four thousand grey cubes on a meadow.
            var world = Meadow(trees: 20);
            TerrainSkirt skirt = SkirtFor(world, out MaterialCache materials);
            try
            {
                Assert.That(skirt.TuftInstances, Is.Zero);
                Assert.That(skirt.GroundInstances, Is.GreaterThan(0),
                    "the ground itself still draws, as it does inside the board");
            }
            finally
            {
                skirt.Dispose();
                materials.Dispose();
            }
        }

        [Test]
        public void TurningTheSurroundOffDrawsNothingAtAll()
        {
            var world = Meadow(trees: 20);
            TerrainSkirt skirt = SkirtFor(world, out MaterialCache materials);
            try
            {
                skirt.Enabled = false;
                skirt.Render(activeLayer: 1);
                Assert.That(skirt.InstancesDrawn, Is.Zero);
                Assert.That(skirt.DrawCalls, Is.Zero);
            }
            finally
            {
                skirt.Dispose();
                materials.Dispose();
            }
        }

        [Test]
        public void TheSurroundIsBatchedSmallEnoughToBeCulled()
        {
            // One batch per material would be fewer calls and far more work: every tree in the
            // surround would be submitted whichever way the camera was pointing.
            var world = Meadow(trees: 40);
            TerrainSkirt skirt = SkirtFor(world, out MaterialCache materials);
            try
            {
                skirt.Render(activeLayer: 1);
                Assert.That(skirt.BatchesDrawn, Is.GreaterThan(8),
                    "the surround is split into parts the camera can reject");
                Assert.That(skirt.DrawCalls, Is.LessThan(skirt.InstancesDrawn),
                    "and each part is still an instanced call, not a draw per thing");
            }
            finally
            {
                skirt.Dispose();
                materials.Dispose();
            }
        }
    }
}
