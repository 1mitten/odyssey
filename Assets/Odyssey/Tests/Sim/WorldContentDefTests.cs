#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

// DefLoaderTests declares its own fixture types in this namespace, one of which is called
// TerrainDef. The alias keeps this file talking about the real one rather than the fixture.
using SimTerrainDef = Odyssey.Sim.Worldgen.TerrainDef;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The world's tables now live in XML (OQ-16), and the in-code tables stay as the oracle, as
    /// the pawn tuning does (<see cref="PawnContentDefTests"/>).
    ///
    /// <para><b>The stakes here are higher than for the pawn tables.</b> A terrain index is
    /// written into every cell of every save and folded into every state hash, so a table whose
    /// order moved would not merely retune the game: it would make every existing save read as a
    /// different world, silently, with grass where the water was. That is why
    /// <see cref="TheIndexOrderIsTheOneTheConstantsSay"/> exists and why it checks all
    /// twenty-one names against the constants rather than spot-checking a few.</para>
    /// </summary>
    public class WorldContentDefTests
    {
        static DefDatabase LoadCore() => ContentPack.LoadCore(RepoPaths.CoreDefs);

        /// <summary>The in-code table: the city's ten, then the wilderness's eleven.</summary>
        static SimTerrainDef[] Oracle()
        {
            var table = new List<SimTerrainDef>(CoreContent.Terrain);
            table.AddRange(NaturalContent.Terrain);
            return table.ToArray();
        }

        [Test]
        public void TheXmlIsTheSameTerrainAsTheCodeOracle()
        {
            SimTerrainDef[] fromXml = WorldContent.TerrainFromDefs(LoadCore());

            var differences = DefComparison.Differences(Oracle(), fromXml, "Terrain");

            Assert.That(differences, Is.Empty,
                "the XML terrain and the in-code tables have parted:" + Environment.NewLine +
                string.Join(Environment.NewLine, differences));
        }

        /// <summary>
        /// The control, without which the test above proves nothing: change one field of one
        /// terrain and the walk must name it.
        /// </summary>
        [Test]
        public void TheComparisonCanFail()
        {
            SimTerrainDef[] fromXml = WorldContent.TerrainFromDefs(LoadCore());
            fromXml[NaturalContent.TerrainDeepWater].impassable = false;

            var differences = DefComparison.Differences(Oracle(), fromXml, "Terrain");

            Assert.That(differences, Has.Count.EqualTo(1), string.Join(Environment.NewLine, differences));
            Assert.That(differences[0], Does.Contain($"Terrain[{NaturalContent.TerrainDeepWater}].impassable"));
        }

        /// <summary>
        /// <b>The load-bearing test of this row.</b> Position in
        /// <see cref="WorldContent.TerrainOrder"/> is the terrain index that every cell, every
        /// save and every hash carries, and the constants in the two content classes are what the
        /// generators and the pathfinder actually use. If those two ever disagree, a map loads as
        /// a different map. Every name is checked, not a sample.
        /// </summary>
        [Test]
        public void TheIndexOrderIsTheOneTheConstantsSay()
        {
            var expected = new (ushort index, string name)[]
            {
                (CoreContent.TerrainAir, "Air"),
                (CoreContent.TerrainPavement, "Pavement"),
                (CoreContent.TerrainCrackedPavement, "CrackedPavement"),
                (CoreContent.TerrainRubble, "Rubble"),
                (CoreContent.TerrainSoil, "Soil"),
                (CoreContent.TerrainGravel, "Gravel"),
                (CoreContent.TerrainFill, "EngineeredFill"),
                (CoreContent.TerrainRock, "Rock"),
                (CoreContent.TerrainBuriedSeam, "BuriedSeam"),
                (CoreContent.TerrainSalvage, "Salvage"),
                (NaturalContent.TerrainGrass, "Grass"),
                (NaturalContent.TerrainBareEarth, "BareEarth"),
                (NaturalContent.TerrainPackedGravel, "PackedGravel"),
                (NaturalContent.TerrainSand, "Sand"),
                (NaturalContent.TerrainSubsoil, "Subsoil"),
                (NaturalContent.TerrainBedrock, "Bedrock"),
                (NaturalContent.TerrainIronOre, "IronOre"),
                (NaturalContent.TerrainCoalSeam, "CoalSeam"),
                (NaturalContent.TerrainShallowWater, "ShallowWater"),
                (NaturalContent.TerrainDeepWater, "DeepWater"),
                (NaturalContent.TerrainMarsh, "Marsh"),
            };

            Assert.That(WorldContent.TerrainOrder.Length, Is.EqualTo(NaturalContent.TerrainCount),
                "the order list and the terrain count disagree, so some terrain has no index or two");
            Assert.That(expected.Length, Is.EqualTo(NaturalContent.TerrainCount),
                "a terrain constant exists that this test does not check");

            foreach (var (index, name) in expected)
                Assert.That(WorldContent.TerrainOrder[index], Is.EqualTo(name),
                    $"index {index} is '{WorldContent.TerrainOrder[index]}' in the order list but '{name}' in the constants");
        }

        /// <summary>
        /// The defName the XML declares is the defName the in-code table carries, for every
        /// terrain. Separate from the comparison above because it is the thing a rename breaks
        /// first, and a message naming the two spellings is worth more than a field path.
        /// </summary>
        [Test]
        public void EveryTerrainIsNamedTheSameInBothPlaces()
        {
            SimTerrainDef[] oracle = Oracle();
            for (int i = 0; i < oracle.Length; i++)
                Assert.That(WorldContent.TerrainOrder[i], Is.EqualTo(oracle[i].defName),
                    $"terrain {i} is '{oracle[i].defName}' in code and '{WorldContent.TerrainOrder[i]}' in the order list");
        }

        [Test]
        public void TheOreKindsAreTheSameInBothPlaces()
        {
            NaturalContent.OreKind[] fromXml = WorldContent.OresFromDefs(LoadCore());

            Assert.That(fromXml.Length, Is.EqualTo(NaturalContent.OreKindCount));
            for (int i = 0; i < fromXml.Length; i++)
            {
                NaturalContent.OreKind expected = NaturalContent.OreAt(i);
                Assert.That(fromXml[i].Terrain, Is.EqualTo(expected.Terrain), $"ore {i} is made of the wrong terrain");
                Assert.That(fromXml[i].Weight, Is.EqualTo(expected.Weight), $"ore {i} weight");
                Assert.That(fromXml[i].MinDepth, Is.EqualTo(expected.MinDepth), $"ore {i} minimum depth");
                Assert.That(fromXml[i].MaxDepth, Is.EqualTo(expected.MaxDepth), $"ore {i} maximum depth");
                Assert.That(fromXml[i].ModuleId, Is.EqualTo(expected.ModuleId), $"ore {i} module id");
            }
        }

        /// <summary>
        /// An ore made of a terrain that does not exist is a load error naming the file, not a
        /// stratum of nothing found three seeds later. This is <c>[DefReference]</c> earning its
        /// keep on real content for the first time.
        /// </summary>
        [Test]
        public void AnOreMadeOfNothingIsRefusedAtLoad()
        {
            var loader = new DefLoader();
            ContentPack.Register(loader);
            loader.AddSource(new InMemoryDefSource("Test").Add("Bad.xml", @"<Defs>
  <TerrainDef><defName>Rock</defName></TerrainDef>
  <OreKindDef>
    <defName>Ore_Unobtainium</defName>
    <terrain>Unobtainium</terrain>
  </OreKindDef>
</Defs>"));

            var error = Assert.Throws<DefLoadException>(() => loader.Load())!;

            Assert.That(error.Message, Does.Contain("Bad.xml"));
            Assert.That(error.Message, Does.Contain("Unobtainium"));
        }

        /// <summary>
        /// The core pack loads whole through the one registration point. A file added to it that
        /// nobody registered a type for fails here rather than in whichever test happens to load
        /// the pack next.
        /// </summary>
        [Test]
        public void TheWholeCorePackLoads()
        {
            DefDatabase defs = LoadCore();

            Assert.That(defs.Table<SimTerrainDef>().Count, Is.EqualTo(NaturalContent.TerrainCount));
            Assert.That(defs.Table<OreKindDef>().Count, Is.EqualTo(NaturalContent.OreKindCount));
        }
    }
}
