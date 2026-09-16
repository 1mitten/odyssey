#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// An undiscovered seam draws as plain rock, and a discovered one draws as itself and glows.
    ///
    /// The substitution happens once, on the way into the mirror, so this is where it can be
    /// checked: if the mirror says rock then the module, the colour and the emissive trim all say
    /// rock, because none of them is consulted about anything else.
    /// </summary>
    public class OreSightRenderTests
    {
        /// <summary>
        /// One world holding all three cases side by side: plain rock, a hidden seam and a seen
        /// one. Comparing within a single mirror is the point — two mirrors would be two module
        /// libraries, and then a passing test would only prove that two libraries number their
        /// modules alike.
        /// </summary>
        static RenderTestWorld ThreeCells()
        {
            var world = new RenderTestWorld(8, 8, 3);
            world.Solid(2, 2, 0).Solid(2, 2, 1);                                   // rock
            world.Solid(4, 2, 0).Solid(4, 2, 1, NaturalContent.TerrainIronOre);    // hidden seam
            world.Solid(6, 2, 0).Solid(6, 2, 1, NaturalContent.TerrainIronOre);    // seen seam
            world.Grid.Flags[world.Index(6, 2, 1)] |= CellFlags.Discovered;
            return world.Publish();
        }

        [Test]
        public void AnUndiscoveredSeamIsIndistinguishableFromRock()
        {
            var world = ThreeCells();
            int rock = world.Index(2, 2, 1);
            int hidden = world.Index(4, 2, 1);

            Assert.That(world.Model.Terrain(hidden), Is.EqualTo(NaturalContent.TerrainRock),
                "the mirror gave the seam away");
            Assert.That(world.Model.TerrainModule(hidden), Is.EqualTo(world.Model.TerrainModule(rock)),
                "the seam resolves to a different module from the rock around it");
            Assert.That(world.Grid.Terrain[hidden], Is.EqualTo(NaturalContent.TerrainIronOre),
                "the simulation forgot what the cell is actually made of");
        }

        [Test]
        public void ADiscoveredSeamIsItself()
        {
            var world = ThreeCells();
            int rock = world.Index(2, 2, 1);
            int seen = world.Index(6, 2, 1);

            Assert.That(world.Model.Terrain(seen), Is.EqualTo(NaturalContent.TerrainIronOre));
            Assert.That(world.Model.TerrainModule(seen), Is.Not.EqualTo(world.Model.TerrainModule(rock)),
                "a seen seam still draws as rock");
        }

        [Test]
        public void OnlyOreGlows()
        {
            // Rock and bedrock have nothing to advertise. Coal is the brightest of the trims,
            // which looks backwards until you put it next to rock: iron's rust brown separates
            // itself on base colour, and coal is a near-black that has nothing but the trim.
            Color rock = StuffPalette.TerrainEmissive(CoreContent.TerrainRock);
            Color bedrock = StuffPalette.TerrainEmissive(NaturalContent.TerrainBedrock);
            Color grass = StuffPalette.TerrainEmissive(NaturalContent.TerrainGrass);
            Color iron = StuffPalette.TerrainEmissive(NaturalContent.TerrainIronOre);
            Color coal = StuffPalette.TerrainEmissive(NaturalContent.TerrainCoalSeam);

            Assert.That(rock, Is.EqualTo(Color.black), "rock glows");
            Assert.That(bedrock, Is.EqualTo(Color.black), "bedrock glows");
            Assert.That(grass, Is.EqualTo(Color.black), "grass glows");
            Assert.That(iron.maxColorComponent, Is.GreaterThan(0f), "iron does not glow");
            Assert.That(coal.maxColorComponent, Is.GreaterThan(iron.maxColorComponent),
                "coal is no brighter than iron, and coal is the one that cannot be seen");
        }

        [Test]
        public void TheEmissiveTableCoversEveryTerrain()
        {
            // The table used to stop at the city's ten entries, so every natural index fell
            // through the bounds check to black. That is invisible for grass and fatal for ore:
            // the trim would simply never have been drawn, and nothing would have said so.
            for (ushort terrain = 0; terrain < NaturalContent.TerrainCount; terrain++)
                Assert.DoesNotThrow(() => StuffPalette.TerrainEmissive(terrain), $"terrain {terrain}");

            Assert.That(StuffPalette.TerrainEmissive(NaturalContent.TerrainIronOre),
                Is.Not.EqualTo(Color.black), "iron ore fell off the end of the emissive table");
        }
    }
}
