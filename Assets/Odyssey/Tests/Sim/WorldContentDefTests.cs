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
    /// The world's tables live in XML and the game reads them (OQ-16, finished by OQ-49). The
    /// in-code tables that used to mirror them are gone, so this guards the content the same way
    /// <see cref="PawnContentDefTests"/> does: with a fingerprint over the loaded table.
    ///
    /// <para><b>The stakes here are higher than for the pawn tables.</b> A terrain index is
    /// written into every cell of every save and folded into every state hash, so a table whose
    /// order moved would not merely retune the game: it would make every existing save read as a
    /// different world, silently, with grass where the water was. That is why
    /// <see cref="TheIndexOrderIsTheOneTheConstantsSay"/> exists and why it checks all
    /// twenty-one names against the constants rather than spot-checking a few.</para>
    ///
    /// <para><b>The fingerprint is not decoration, and it was measured rather than assumed.</b>
    /// The moment the generators started reading the XML, the old oracle became a comparison of
    /// the XML against itself — it passed without asking anything. It was checked: editing rock's
    /// <c>workToClear</c> from 700 to 701, a number mining reads on every work tick, left all 448
    /// tests green. Nothing at all pinned these values. The fingerprint is what closed that, and
    /// the same edit fails it.</para>
    /// </summary>
    public class WorldContentDefTests
    {
        static DefDatabase LoadCore() => ContentPack.LoadCore(RepoPaths.CoreDefs);

        /// <summary>
        /// The terrain table as it stands. Update this number only when you meant to change the
        /// world's content, and say what moved in the commit message.
        /// </summary>
        const ulong TerrainFingerprint = 675045117585215745UL;

        [Test]
        public void TheTerrainIsStillWhatItWas()
        {
            ulong actual = DefComparison.Fingerprint(WorldContent.Table, "Terrain");

            Assert.That(actual, Is.EqualTo(TerrainFingerprint),
                "the terrain table has moved. If that was deliberate, set TerrainFingerprint to " +
                $"{actual}UL and say what changed. If it was not, " +
                "`git diff Assets/Odyssey/Defs/Core/World` is what moved.");
        }

        /// <summary>
        /// The control, without which the test above proves nothing: change one field of one
        /// terrain and the fingerprint must move.
        /// </summary>
        [Test]
        public void TheFingerprintNoticesAChangedField()
        {
            SimTerrainDef[] fromXml = WorldContent.TerrainFromDefs(LoadCore());
            ulong before = DefComparison.Fingerprint(fromXml, "Terrain");
            Assert.That(before, Is.EqualTo(TerrainFingerprint), "the freshly loaded pack is the shipped one");

            fromXml[NaturalContent.TerrainDeepWater].impassable = false;

            Assert.That(DefComparison.Fingerprint(fromXml, "Terrain"), Is.Not.EqualTo(before));
        }

        /// <summary>
        /// The exact edit that went unnoticed before this test existed: one integer, on the
        /// terrain the mining job prices its work from.
        /// </summary>
        [Test]
        public void TheFingerprintNoticesTheEditThatUsedToBeSilent()
        {
            SimTerrainDef[] fromXml = WorldContent.TerrainFromDefs(LoadCore());
            ulong before = DefComparison.Fingerprint(fromXml, "Terrain");

            fromXml[CoreContent.TerrainRock].workToClear += 1;

            Assert.That(DefComparison.Fingerprint(fromXml, "Terrain"), Is.Not.EqualTo(before));
        }

        /// <summary>
        /// The game reads the pack rather than a table built in code, which is the whole of this
        /// row. Asserted on a value with teeth: rock's work cost is what mining prices from.
        /// </summary>
        [Test]
        public void TheRunningGameReadsTheLoadedTable()
        {
            Assert.That(WorldContent.Table, Has.Length.EqualTo(WorldContent.TerrainOrder.Length));
            Assert.That(NaturalContent.TerrainAt(CoreContent.TerrainRock).workToClear,
                Is.EqualTo(WorldContent.Table[CoreContent.TerrainRock].workToClear));
            Assert.That(NaturalContent.TerrainAt(NaturalContent.TerrainDeepWater).impassable, Is.True);
            Assert.That(CoreContent.TerrainAt(CoreContent.TerrainAir).defName, Is.EqualTo("Air"));
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
        /// The table the game reads is in the order the order list declares, row by row.
        ///
        /// <para>Worth asserting separately from the fingerprint, which would also move if the
        /// order did but would only say "something changed". This names the row and both
        /// spellings, and a rename is the thing that breaks it first. It is also the check that
        /// the loader's own by-defName sort has not become the order the table is built in —
        /// <see cref="WorldContent.TerrainOrder"/> is what decides an index, not the loader.</para>
        /// </summary>
        [Test]
        public void TheLoadedTableIsInTheDeclaredOrder()
        {
            SimTerrainDef[] table = WorldContent.Table;
            Assert.That(table, Has.Length.EqualTo(WorldContent.TerrainOrder.Length));
            for (int i = 0; i < table.Length; i++)
                Assert.That(table[i].defName, Is.EqualTo(WorldContent.TerrainOrder[i]),
                    $"terrain {i} loaded as '{table[i].defName}' but the order list says '{WorldContent.TerrainOrder[i]}'");
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
