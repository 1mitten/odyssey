#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Defs;

namespace Odyssey.Tests.Sim
{
    public sealed class TerrainDef : Def
    {
        public int workToClear;
        public bool diggable = true;
    }

    public sealed class StuffDef : Def
    {
        public float strengthFactor = 1f;
        public int strengthOffset;
        public List<string> tags = new List<string>();
    }

    public sealed class WallDef : Def
    {
        [DefRequired] public int workToBuild;

        [DefReference(typeof(StuffDef))] public string defaultStuff = string.Empty;

        [DefReference(typeof(TerrainDef), Optional = true)] public string placesTerrain = string.Empty;
    }

    public class DefLoaderTests
    {
        static DefLoader LoaderWith(string xml) =>
            new DefLoader()
                .Register<TerrainDef>()
                .Register<StuffDef>()
                .Register<WallDef>()
                .AddSource(new InMemoryDefSource("Core").Add("test.xml", xml));

        [Test]
        public void LoadsFieldsOfEverySupportedKind()
        {
            var db = LoaderWith(@"
<Defs>
  <StuffDef>
    <defName>Steel</defName>
    <label>steel</label>
    <strengthFactor>1.5</strengthFactor>
    <strengthOffset>40</strengthOffset>
    <tags><li>metal</li><li>salvage</li></tags>
  </StuffDef>
</Defs>").Load();

            var stuff = db.Table<StuffDef>().Get("Steel");
            Assert.That(stuff.label, Is.EqualTo("steel"));
            Assert.That(stuff.strengthFactor, Is.EqualTo(1.5f));
            Assert.That(stuff.strengthOffset, Is.EqualTo(40));
            Assert.That(stuff.tags, Is.EqualTo(new[] { "metal", "salvage" }));
        }

        [Test]
        public void HandlesAreStableAcrossRunsRegardlessOfFileOrder()
        {
            // Handles go into save files, so their assignment must not depend on discovery order.
            const string a = "<Defs><TerrainDef><defName>Rubble</defName></TerrainDef></Defs>";
            const string b = "<Defs><TerrainDef><defName>Concrete</defName></TerrainDef></Defs>";

            var forward = new DefLoader().Register<TerrainDef>()
                .AddSource(new InMemoryDefSource("Core").Add("a.xml", a).Add("b.xml", b)).Load();
            var reversed = new DefLoader().Register<TerrainDef>()
                .AddSource(new InMemoryDefSource("Core").Add("b.xml", b).Add("a.xml", a)).Load();

            Assert.That(forward.Table<TerrainDef>().Handle("Rubble").Index,
                Is.EqualTo(reversed.Table<TerrainDef>().Handle("Rubble").Index));
            Assert.That(forward.Table<TerrainDef>()[0].defName, Is.EqualTo("Concrete"),
                "tables are ordered by name, not by discovery");
        }

        [Test]
        public void ChildInheritsFromAbstractParentAndOverridesIt()
        {
            var db = LoaderWith(@"
<Defs>
  <TerrainDef Name=""BaseRock"" Abstract=""true"">
    <workToClear>500</workToClear>
    <diggable>true</diggable>
  </TerrainDef>
  <TerrainDef ParentName=""BaseRock"">
    <defName>Granite</defName>
  </TerrainDef>
  <TerrainDef ParentName=""BaseRock"">
    <defName>Concrete</defName>
    <workToClear>900</workToClear>
  </TerrainDef>
</Defs>").Load();

            var table = db.Table<TerrainDef>();
            Assert.That(table.Count, Is.EqualTo(2), "the abstract parent must not reach the game");
            Assert.That(table.Get("Granite").workToClear, Is.EqualTo(500), "inherited");
            Assert.That(table.Get("Concrete").workToClear, Is.EqualTo(900), "overridden");
            Assert.That(table.Get("Granite").diggable, Is.True);
        }

        [Test]
        public void CrossReferencesAreValidatedAtLoad()
        {
            // The whole point: a typo must fail here, with a file and line, rather than as a null
            // three in-game days into an unattended run.
            var ex = Assert.Throws<DefLoadException>(() => LoaderWith(@"
<Defs>
  <StuffDef><defName>Steel</defName></StuffDef>
  <WallDef>
    <defName>SteelWall</defName>
    <workToBuild>100</workToBuild>
    <defaultStuff>Stele</defaultStuff>
  </WallDef>
</Defs>").Load());

            Assert.That(ex!.Message, Does.Contain("Stele"));
            Assert.That(ex.Message, Does.Contain("StuffDef"));
            Assert.That(ex.Message, Does.Contain("test.xml"));
        }

        [Test]
        public void ValidCrossReferenceLoads()
        {
            var db = LoaderWith(@"
<Defs>
  <StuffDef><defName>Steel</defName></StuffDef>
  <WallDef>
    <defName>SteelWall</defName>
    <workToBuild>100</workToBuild>
    <defaultStuff>Steel</defaultStuff>
  </WallDef>
</Defs>").Load();

            Assert.That(db.Table<WallDef>().Get("SteelWall").defaultStuff, Is.EqualTo("Steel"));
        }

        [Test]
        public void OptionalReferenceMayBeEmpty()
        {
            Assert.DoesNotThrow(() => LoaderWith(@"
<Defs>
  <StuffDef><defName>Steel</defName></StuffDef>
  <WallDef>
    <defName>SteelWall</defName>
    <workToBuild>100</workToBuild>
    <defaultStuff>Steel</defaultStuff>
  </WallDef>
</Defs>").Load());
        }

        [Test]
        public void RequiredFieldsAreEnforced()
        {
            var ex = Assert.Throws<DefLoadException>(() => LoaderWith(@"
<Defs>
  <StuffDef><defName>Steel</defName></StuffDef>
  <WallDef>
    <defName>SteelWall</defName>
    <defaultStuff>Steel</defaultStuff>
  </WallDef>
</Defs>").Load());

            Assert.That(ex!.Message, Does.Contain("workToBuild"));
        }

        [Test]
        public void EveryErrorIsReportedInOneRun()
        {
            // Content authors should not have to fix errors one at a time.
            var ex = Assert.Throws<DefLoadException>(() => LoaderWith(@"
<Defs>
  <TerrainDef><defName>A</defName><nosuchfield>1</nosuchfield></TerrainDef>
  <TerrainDef><defName>B</defName><workToClear>notanumber</workToClear></TerrainDef>
</Defs>").Load());

            Assert.That(ex!.Message, Does.Contain("nosuchfield"));
            Assert.That(ex.Message, Does.Contain("workToClear"));
            Assert.That(ex.Message, Does.Contain("2 error(s)"));
        }

        [Test]
        public void DuplicateNamesAreRejected()
        {
            var ex = Assert.Throws<DefLoadException>(() => LoaderWith(@"
<Defs>
  <TerrainDef><defName>Rubble</defName></TerrainDef>
  <TerrainDef><defName>Rubble</defName></TerrainDef>
</Defs>").Load());

            Assert.That(ex!.Message, Does.Contain("duplicate"));
        }

        [Test]
        public void MissingParentIsReportedWithProvenance()
        {
            var ex = Assert.Throws<DefLoadException>(() => LoaderWith(@"
<Defs>
  <TerrainDef ParentName=""Nope""><defName>Granite</defName></TerrainDef>
</Defs>").Load());

            Assert.That(ex!.Message, Does.Contain("Nope"));
            Assert.That(ex.Message, Does.Contain("Core"));
        }

        [Test]
        public void InheritanceCyclesDoNotHang()
        {
            var ex = Assert.Throws<DefLoadException>(() => LoaderWith(@"
<Defs>
  <TerrainDef Name=""A"" ParentName=""B"" Abstract=""true""></TerrainDef>
  <TerrainDef Name=""B"" ParentName=""A"" Abstract=""true""></TerrainDef>
  <TerrainDef ParentName=""A""><defName>Granite</defName></TerrainDef>
</Defs>").Load());

            Assert.That(ex!.Message, Does.Contain("cycle"));
        }

        [Test]
        public void MalformedXmlIsReportedNotThrownRaw()
        {
            var ex = Assert.Throws<DefLoadException>(() => LoaderWith("<Defs><TerrainDef>").Load());
            Assert.That(ex!.Message, Does.Contain("well formed"));
        }

        [Test]
        public void UnknownDefTypeIsReported()
        {
            var ex = Assert.Throws<DefLoadException>(() => LoaderWith(
                "<Defs><NoSuchDef><defName>X</defName></NoSuchDef></Defs>").Load());
            Assert.That(ex!.Message, Does.Contain("NoSuchDef"));
        }

        [Test]
        public void ContentHashIsStableAndSensitive()
        {
            var one = LoaderWith("<Defs><TerrainDef><defName>A</defName></TerrainDef></Defs>").Load();
            var same = LoaderWith("<Defs><TerrainDef><defName>A</defName></TerrainDef></Defs>").Load();
            var more = LoaderWith(
                "<Defs><TerrainDef><defName>A</defName></TerrainDef><TerrainDef><defName>B</defName></TerrainDef></Defs>").Load();

            Assert.That(one.ContentHash().Value, Is.EqualTo(same.ContentHash().Value));
            Assert.That(one.ContentHash().Value, Is.Not.EqualTo(more.ContentHash().Value));
        }

        [Test]
        public void LaterPacksLoadAlongsideCore()
        {
            var db = new DefLoader()
                .Register<TerrainDef>()
                .AddSource(new InMemoryDefSource("Core")
                    .Add("core.xml", "<Defs><TerrainDef><defName>Rubble</defName></TerrainDef></Defs>"))
                .AddSource(new InMemoryDefSource("ModA")
                    .Add("mod.xml", "<Defs><TerrainDef><defName>Ash</defName></TerrainDef></Defs>"))
                .Load();

            Assert.That(db.Table<TerrainDef>().Count, Is.EqualTo(2));
            Assert.That(db.Table<TerrainDef>().Get("Ash").Origin.Pack, Is.EqualTo("ModA"));
        }
    }
}
