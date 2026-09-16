#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Audio;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The measurement the ambience is made of: how much water is near the focus, and where the
    /// middle of it sits.
    ///
    /// What is being held here is less the numbers than the behaviour the ear will judge the
    /// system by: a pond by the camera is audible and a lake a screen away is not, a river
    /// crossing the view is louder than a puddle at its edge, and the same map measured twice
    /// gives the same answer.
    /// </summary>
    public class AmbienceProbeTests
    {
        /// <summary>A plain array of terrain, addressed exactly as the mirror is.</summary>
        sealed class FlatTerrain : ITerrainLookup
        {
            readonly ushort[] _terrain;
            readonly GridSize _size;

            public FlatTerrain(GridSize size)
            {
                _size = size;
                _terrain = new ushort[size.SizeX * size.SizeZ * size.SizeY];
                for (int i = 0; i < _terrain.Length; i++)
                    _terrain[i] = NaturalContent.TerrainGrass;
            }

            public void Pond(int x, int z, int radius, int layer = 0)
            {
                for (int dz = -radius; dz <= radius; dz++)
                for (int dx = -radius; dx <= radius; dx++)
                    if (dx * dx + dz * dz <= radius * radius)
                        _terrain[_size.Index(x + dx, z + dz, layer)] = NaturalContent.TerrainShallowWater;
            }

            public ushort TerrainAt(int cellIndex) => _terrain[cellIndex];
        }

        static readonly GridSize Size = new(48, 48, 2);

        [Test]
        public void DryGroundIsSilent()
        {
            var terrain = new FlatTerrain(Size);
            AmbienceField field = AmbienceProbe.Sample(terrain, Size, 0, new Vector2(24f, 24f));
            Assert.That(field.WaterIntensity, Is.EqualTo(0f));
        }

        [Test]
        public void APondBesideTheFocusIsAudibleAndComesFromThePond()
        {
            var terrain = new FlatTerrain(Size);
            terrain.Pond(28, 24, 3);

            AmbienceField field = AmbienceProbe.Sample(terrain, Size, 0, new Vector2(24f, 24f));

            Assert.That(field.WaterIntensity, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f),
                "a pond four cells away is audible but not full");
            Assert.That(field.WaterCentreCell.x, Is.GreaterThan(24f).And.LessThan(32f),
                "the centre of the sound is in the pond, east of the focus");
        }

        [Test]
        public void ALakeAScreenAwayIsSilent()
        {
            var terrain = new FlatTerrain(Size);
            terrain.Pond(44, 44, 3);

            AmbienceField field = AmbienceProbe.Sample(terrain, Size, 0, new Vector2(10f, 10f));

            Assert.That(field.WaterIntensity, Is.EqualTo(0f),
                "water beyond the radius is silence: the ear does not hear through the ground");
        }

        [Test]
        public void ASeaSaturatesAtFullRatherThanGrowingPastIt()
        {
            AmbienceField field = AmbienceProbe.Sample(
                new AllWater(), Size, 0, new Vector2(24f, 24f));

            Assert.That(field.WaterIntensity, Is.EqualTo(1f),
                "standing in the ocean is full water, not 3x water");
        }

        sealed class AllWater : ITerrainLookup
        {
            public ushort TerrainAt(int cellIndex) => NaturalContent.TerrainShallowWater;
        }

        [Test]
        public void TheSameMapMeasuresTheSameTwice()
        {
            var terrain = new FlatTerrain(Size);
            terrain.Pond(20, 26, 4);

            AmbienceField a = AmbienceProbe.Sample(terrain, Size, 0, new Vector2(22f, 24f));
            AmbienceField b = AmbienceProbe.Sample(terrain, Size, 0, new Vector2(22f, 24f));

            Assert.That(b.WaterIntensity, Is.EqualTo(a.WaterIntensity));
            Assert.That(b.WaterCentreCell, Is.EqualTo(a.WaterCentreCell));
        }

        [Test]
        public void WaterOnAnotherLayerIsNotHeardThroughTheFloor()
        {
            var terrain = new FlatTerrain(Size);
            terrain.Pond(24, 24, 3, layer: 1);

            AmbienceField onTheGround = AmbienceProbe.Sample(terrain, Size, 0, new Vector2(24f, 24f));

            Assert.That(onTheGround.WaterIntensity, Is.EqualTo(0f),
                "the probe reads the layer the camera is on; a slice below is a different place");
        }
    }
}
