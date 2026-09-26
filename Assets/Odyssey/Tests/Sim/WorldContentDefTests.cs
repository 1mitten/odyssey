#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Growing;
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
    /// twenty-six names against the constants rather than spot-checking a few.</para>
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
        // U29 gave Rubble two flags: `buildable` false, so a collapse leaves a mess that has to be
        // cleared before anything is built where it fell, and `clearable` true, so a Mine order
        // can clear it although it is not solid. Rubble is the only terrain that sets either.
        // DM3 (design 62 §5) appended five after Marsh: DeepStone (workToClear 1400, twice rock's),
        // CopperOre 820, GoldOre 1100, Gems 1200 and Emberquartz 1600. No existing row moved.
        const ulong TerrainFingerprint = 8771646294617608521UL;

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
                (NaturalContent.TerrainDeepStone, "DeepStone"),
                (NaturalContent.TerrainCopperOre, "CopperOre"),
                (NaturalContent.TerrainGoldOre, "GoldOre"),
                (NaturalContent.TerrainGems, "Gems"),
                (NaturalContent.TerrainEmberquartz, "Emberquartz"),
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

        /// <summary>
        /// The ore table as it stands (design 62 §5c). It had two owners until DM3 — a table in
        /// <c>NaturalContent</c> and this XML, with a test comparing them — and now has one, so it
        /// is pinned the way the terrain is. Every number in it reshuffles every seeded map.
        /// </summary>
        // DM3: the table moved into Ores.xml whole — band, shape, size, frequency, yield and item —
        // and copper, gold, gems and Emberquartz joined iron and coal.
        const ulong OreFingerprint = 2994934247155920581UL;

        static OreKindDef[] OreDefs(DefDatabase defs)
        {
            var table = new OreKindDef[WorldContent.OreOrder.Length];
            for (int i = 0; i < table.Length; i++)
            {
                Assert.That(defs.Table<OreKindDef>().TryGetHandle(WorldContent.OreOrder[i], out var handle), Is.True,
                    $"no ore named {WorldContent.OreOrder[i]}");
                table[i] = defs.Table<OreKindDef>()[handle];
            }
            return table;
        }

        [Test]
        public void TheOresAreStillWhatTheyWere()
        {
            ulong actual = DefComparison.Fingerprint(OreDefs(LoadCore()), "Ore");

            Assert.That(actual, Is.EqualTo(OreFingerprint),
                "the ore table has moved. If that was deliberate, set OreFingerprint to " +
                $"{actual}UL and say what changed. If it was not, " +
                "`git diff Assets/Odyssey/Defs/Core/World/Ores.xml` is what moved.");
        }

        /// <summary>The control: one changed yield must move the ore fingerprint.</summary>
        [Test]
        public void TheOreFingerprintNoticesAChangedYield()
        {
            OreKindDef[] ores = OreDefs(LoadCore());
            ulong before = DefComparison.Fingerprint(ores, "Ore");
            ores[0].yieldPerCell += 1;
            Assert.That(DefComparison.Fingerprint(ores, "Ore"), Is.Not.EqualTo(before));
        }

        /// <summary>
        /// The ore table is the one owner of what an ore is: <see cref="NaturalContent.IsOre"/> and
        /// <see cref="NaturalContent.OreKindOf"/> answer from it, and the running game reads the
        /// same rows a fresh load does.
        /// </summary>
        [Test]
        public void IsOreIsTheOreTable()
        {
            NaturalContent.OreKind[] fromXml = WorldContent.OresFromDefs(LoadCore());
            Assert.That(fromXml.Length, Is.EqualTo(NaturalContent.OreKindCount));
            Assert.That(fromXml.Length, Is.EqualTo(6), "iron, coal, copper, gold, gems and Emberquartz");

            var ores = new HashSet<ushort>();
            for (int k = 0; k < fromXml.Length; k++)
            {
                ores.Add(fromXml[k].Terrain);
                Assert.That(NaturalContent.OreKindOf(fromXml[k].Terrain), Is.EqualTo(k));
                Assert.That(NaturalContent.OreAt(k).Terrain, Is.EqualTo(fromXml[k].Terrain));
            }

            for (ushort t = 0; t < NaturalContent.TerrainCount; t++)
                Assert.That(NaturalContent.IsOre(t), Is.EqualTo(ores.Contains(t)), $"terrain {WorldContent.TerrainOrder[t]}");

            // Every ore is rock-like, by the one owner of that rule (design 62 §4): a kind added to
            // Ores.xml without a word in TerrainHandle.IsRockLike would read "Dig" and host nothing.
            for (int k = 0; k < fromXml.Length; k++)
                Assert.That(TerrainHandle.IsRockLike(fromXml[k].Terrain), Is.True,
                    $"{WorldContent.OreOrder[k]} is not rock-like");

            // Host rock is rock-like with what cannot host a deposit taken out.
            Assert.That(NaturalContent.IsHostRock(NaturalContent.TerrainDeepStone), Is.True);
            Assert.That(NaturalContent.IsHostRock(CoreContent.TerrainRock), Is.True);
            Assert.That(NaturalContent.IsHostRock(NaturalContent.TerrainBedrock), Is.False);
            Assert.That(NaturalContent.IsHostRock(CoreContent.TerrainRubble), Is.False);
            Assert.That(NaturalContent.IsHostRock(NaturalContent.TerrainIronOre), Is.False, "ore never grows over ore");
            Assert.That(TerrainHandle.IsRockLike(NaturalContent.TerrainDeepStone), Is.True);
        }

        /// <summary>
        /// The crop table as it stands, for the same reason as the terrain's. A plant's numbers
        /// are the field: <c>growTicks</c> prices how long a sowing takes to ripen, the work costs
        /// price every swing of the hoe and the sickle, and <c>minFertility</c> decides where a
        /// zone may sit at all.
        /// </summary>
        // U46 put the first crop in. The one number to re-check by hand when this moves is
        // growTicks: 130,000 inside the daylight window is four game days to a full field, which
        // is the pace the start flow's pantry was tuned against.
        const ulong PlantFingerprint = 7107735981392633136UL;

        [Test]
        public void ThePlantsAreStillWhatTheyWere()
        {
            ulong actual = DefComparison.Fingerprint(ContentPack.Plants(), "Plant");

            Assert.That(actual, Is.EqualTo(PlantFingerprint),
                "the plant table has moved. If that was deliberate, set PlantFingerprint to " +
                $"{actual}UL and say what changed. If it was not, " +
                "`git diff Assets/Odyssey/Defs/Core` is what moved.");
        }

        /// <summary>
        /// A zone record stores its plant as a <see cref="PlantHandle"/> number, so the order list
        /// and the handles are the save contract, exactly as the terrain index order is. Checked
        /// by name rather than a sample, for the same reason the terrain order is.
        /// </summary>
        [Test]
        public void ThePlantTableIsInTheDeclaredOrder()
        {
            PlantDef[] table = ContentPack.Plants();

            Assert.That(WorldContent.PlantOrder.Length, Is.EqualTo(PlantHandle.Count),
                "the order list and the plant handle count disagree, so some crop has no handle or two");
            Assert.That(table, Has.Length.EqualTo(PlantHandle.Count),
                "the loaded table and the order list disagree in length");

            for (int i = 0; i < table.Length; i++)
                Assert.That(table[i].defName, Is.EqualTo(WorldContent.PlantOrder[i]),
                    $"plant {i} loaded as '{table[i].defName}' but the order list says '{WorldContent.PlantOrder[i]}'");
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
            Assert.That(defs.Table<PlantDef>().Count, Is.EqualTo(PlantHandle.Count));
        }
    }
}
