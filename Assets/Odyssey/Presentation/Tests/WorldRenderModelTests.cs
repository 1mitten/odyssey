#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What module the render mirror picks for what stands in a cell.
    ///
    /// The natural generator numbers its edifices after the city's, and the mirror used to
    /// switch on the city's codes alone with the wall as its default. Nothing failed: every tree
    /// on the wooded board simply drew as a grey wall box, which is exactly the kind of fault
    /// that only a picture or a test like this one catches.
    /// </summary>
    public class WorldRenderModelTests
    {
        [Test]
        public void ATreeResolvesToItsOwnModuleAndNotToTheWall()
        {
            var world = new RenderTestWorld(6, 6, 3)
                .Solid(2, 2, 0).Edifice(2, 2, 1, NaturalContent.EdificeTreeConifer, blocking: false)
                .Solid(3, 2, 0).Edifice(3, 2, 1, NaturalContent.EdificeTreeBroadleaf, blocking: false)
                .Solid(4, 2, 0).Edifice(4, 2, 1, CoreContent.EdificeWall)
                .Publish();

            int conifer = world.Model.EdificeModule(world.Index(2, 2, 1));
            int broadleaf = world.Model.EdificeModule(world.Index(3, 2, 1));
            int wall = world.Model.EdificeModule(world.Index(4, 2, 1));

            Assert.That(conifer, Is.EqualTo(world.Library.Resolve(NaturalContent.ModuleTreeConifer, ModuleShape.Pillar)));
            Assert.That(broadleaf, Is.EqualTo(world.Library.Resolve(NaturalContent.ModuleTreeBroadleaf, ModuleShape.Pillar)));
            Assert.That(conifer, Is.Not.EqualTo(wall), "a conifer is not a wall");
            Assert.That(broadleaf, Is.Not.EqualTo(wall), "a broadleaf is not a wall");
            Assert.That(conifer, Is.Not.EqualTo(broadleaf), "the two kinds of tree are told apart");
        }

        [Test]
        public void ATreeIsABodyStandingInTheCellNotAPanelOnItsFaces()
        {
            // The mesher emits a wall as up to four face panels and everything else as one body.
            // A tree drawn as panels would be the grey-box fault by another route.
            var world = new RenderTestWorld(6, 6, 3)
                .Solid(2, 2, 0).Edifice(2, 2, 1, NaturalContent.EdificeTreeConifer, blocking: false)
                .Publish();

            int module = world.Model.EdificeModule(world.Index(2, 2, 1));
            Assert.That(module, Is.Not.Zero, "a tree has a module");
            Assert.That(world.Library[module].Shape, Is.EqualTo(ModuleShape.Pillar));
        }

        /// <summary>
        /// The floor of the open landscape is the lowest column top on the board, not the lowest
        /// cell in it. A terrace two steps down is still ground somebody can stand on and has to
        /// be drawn; the rock under it is not.
        /// </summary>
        [Test]
        public void TheLandscapeFloorIsTheLowestColumnTop()
        {
            // Three terraces, two layers apart at the extremes, over a common base.
            var world = new RenderTestWorld(4, 1, 6)
                .Solid(0, 0, 0).Solid(0, 0, 1)
                .Solid(1, 0, 0).Solid(1, 0, 1).Solid(1, 0, 2)
                .Solid(2, 0, 0).Solid(2, 0, 1).Solid(2, 0, 2).Solid(2, 0, 3)
                .Solid(3, 0, 0).Solid(3, 0, 1).Solid(3, 0, 2)
                .Publish();

            Assert.That(world.Model.LowestOutdoorLayer, Is.EqualTo(1),
                "the lowest terrace top, not the bedrock under all four columns");
            Assert.That(world.Model.HighestOccupiedLayer, Is.EqualTo(4),
                "and the high-water mark is still the top of the geometry plus a standing colonist");
        }

        /// <summary>
        /// A column with a roof over it is not open landscape, so its ground has no claim on the
        /// drawn band. The slab is what the column's top is, and the mark reads it.
        /// </summary>
        [Test]
        public void ARoofedColumnDoesNotPullTheLandscapeFloorDown()
        {
            var world = new RenderTestWorld(2, 1, 6)
                .Solid(0, 0, 0).Solid(0, 0, 1).Solid(0, 0, 2)      // open ground at L2
                .Solid(1, 0, 0).Slab(1, 0, 3)                      // ground at L0, roofed at L3
                .Publish();

            Assert.That(world.Model.LowestOutdoorLayer, Is.EqualTo(2),
                "the buried floor of a roofed column counted as landscape");
        }

        /// <summary>
        /// A column of pure air contributes nothing rather than contributing zero, or one empty
        /// corner of a map would drag the drawn band down to the bedrock everywhere.
        /// </summary>
        [Test]
        public void AnEmptyColumnDoesNotCount()
        {
            var world = new RenderTestWorld(2, 1, 6)
                .Solid(0, 0, 0).Solid(0, 0, 1).Solid(0, 0, 2)
                .Publish();

            Assert.That(world.Model.LowestOutdoorLayer, Is.EqualTo(2));
        }
    }
}
