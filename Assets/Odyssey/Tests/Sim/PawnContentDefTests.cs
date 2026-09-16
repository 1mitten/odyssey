#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The pawn tuning now lives in XML (OQ-15), and this is what makes that safe: the XML and
    /// <see cref="PawnContent.Core"/> are compared field for field, and <c>Core</c> stays as the
    /// oracle until the bootstrap loads content at startup.
    ///
    /// <para><b>Why an oracle rather than a golden file.</b> A baked hash of the XML would prove
    /// only that the XML has not changed, which is not the question. The question is whether the
    /// content a clone loads is the content the simulation has been measured on — every soak
    /// hash, every golden, every tuning decision in <c>docs/research</c> was taken against
    /// <c>Core()</c>. So <c>Core()</c> is the specification and the XML is the implementation,
    /// and the day they part is the day this fails with the field that differs.</para>
    ///
    /// <para>The comparison walks public fields recursively rather than listing them, so a field
    /// added to a Def tomorrow is compared without anybody remembering to add it here. That is
    /// also why <see cref="TheComparisonCanFail"/> exists: a reflective comparer that quietly
    /// walks nothing would pass forever, and this one is made to fail on demand before it is
    /// trusted.</para>
    /// </summary>
    public class PawnContentDefTests
    {
        static DefDatabase LoadCore()
        {
            var loader = new DefLoader();
            PawnContent.Register(loader);
            return loader.AddSource(new DirectoryDefSource("Core", RepoPaths.CoreDefs)).Load();
        }

        [Test]
        public void TheXmlIsTheSameContentAsTheCodeOracle()
        {
            PawnContent fromXml = PawnContent.FromDefs(LoadCore());

            var differences = new List<string>();
            Compare(PawnContent.Core(), fromXml, "PawnContent", differences);

            Assert.That(differences, Is.Empty,
                "the XML content and PawnContent.Core() have parted:" + Environment.NewLine +
                string.Join(Environment.NewLine, differences));
        }

        /// <summary>
        /// The control. One value is changed in the loaded content and the comparison must name
        /// exactly that field — otherwise the test above passes because the comparer walks
        /// nothing, which is the failure mode a reflective comparer has.
        /// </summary>
        [Test]
        public void TheComparisonCanFail()
        {
            PawnContent fromXml = PawnContent.FromDefs(LoadCore());
            fromXml.Items[ItemIndex.Wood].stackLimit += 1;

            var differences = new List<string>();
            Compare(PawnContent.Core(), fromXml, "PawnContent", differences);

            Assert.That(differences, Has.Count.EqualTo(1), string.Join(Environment.NewLine, differences));
            Assert.That(differences[0], Does.Contain("Items[2].stackLimit"));
        }

        /// <summary>
        /// A deeper control: the walk must reach inside a list of nested records, which is where
        /// the need bands live and where a comparer that stops at the first object would silently
        /// stop looking.
        /// </summary>
        [Test]
        public void TheComparisonReachesInsideNestedLists()
        {
            PawnContent fromXml = PawnContent.FromDefs(LoadCore());
            fromXml.Needs[NeedIndex.Rest].bands[2].fallPerInterval += 1;

            var differences = new List<string>();
            Compare(PawnContent.Core(), fromXml, "PawnContent", differences);

            Assert.That(differences, Has.Count.EqualTo(1), string.Join(Environment.NewLine, differences));
            Assert.That(differences[0], Does.Contain("Needs[1].bands[2].fallPerInterval"));
        }

        /// <summary>
        /// Every skill's tables came through the loader's array parsing, which did not exist
        /// before this row. Asserted on their own because an empty array compares equal to an
        /// empty array, so a table that failed to parse would look like agreement if both sides
        /// were empty — they are not, but the day one is, this says so.
        /// </summary>
        [Test]
        public void TheSkillTablesArrivedWhole()
        {
            PawnContent fromXml = PawnContent.FromDefs(LoadCore());

            foreach (SkillDef skill in fromXml.Skills)
            {
                Assert.That(skill.experienceToAdvance.Length, Is.EqualTo(skill.maxLevel), skill.defName);
                Assert.That(skill.decayPerDay.Length, Is.EqualTo(skill.maxLevel + 1), skill.defName);
                Assert.That(skill.Level(0), Is.Zero, skill.defName);
                Assert.That(skill.Level(skill.MaxExperience), Is.EqualTo(20), skill.defName);
                // a-01's cumulative figure, in thousandths: the whole reason the second slope is
                // what it is. If the table is ever edited into disagreeing with it, say so here.
                Assert.That(skill.MaxExperience, Is.EqualTo(265_000_000), skill.defName);
            }
        }

        /// <summary>
        /// Both skills inherit one abstract parent, so the tables are written once. Proving the
        /// inheritance actually ran matters: without it both children would bind with empty
        /// tables and <see cref="SkillDef.Level"/> would throw on its first index.
        /// </summary>
        [Test]
        public void TheAbstractSkillParentIsNotItselfContent()
        {
            DefDatabase defs = LoadCore();

            Assert.That(defs.Table<SkillDef>().Count, Is.EqualTo(2), "the abstract parent reached the game");
        }

        /// <summary>
        /// A content error names the pack, the file and the line. Three in-memory documents
        /// rather than the real pack, because the real pack is required to be correct.
        /// </summary>
        [Test]
        public void AMisspeltFieldNamesTheFileAndTheLine()
        {
            var loader = new DefLoader();
            PawnContent.Register(loader);
            loader.AddSource(new InMemoryDefSource("Test").Add("Broken.xml", @"<Defs>
  <ItemDef>
    <defName>Item_Typo</defName>
    <stackLmit>4</stackLmit>
  </ItemDef>
</Defs>"));

            var error = Assert.Throws<DefLoadException>(() => loader.Load())!;

            Assert.That(error.Message, Does.Contain("Broken.xml"));
            Assert.That(error.Message, Does.Contain("(2)"), "the line of the offending Def is not reported");
            Assert.That(error.Message, Does.Contain("stackLmit"));
        }

        /// <summary>
        /// A Def the handle order requires and the content does not have is refused by name at
        /// world construction, rather than leaving a null in an array for the first tick to trip
        /// over half an hour into a headless run.
        /// </summary>
        [Test]
        public void AMissingDefIsRefusedByName()
        {
            var loader = new DefLoader();
            PawnContent.Register(loader);
            loader.AddSource(new InMemoryDefSource("Test").Add("Thin.xml", @"<Defs>
  <ItemDef><defName>Item_Meal</defName></ItemDef>
</Defs>"));

            var error = Assert.Throws<DefLoadException>(() => PawnContent.FromDefs(loader.Load()))!;

            Assert.That(error.Message, Does.Contain("Item_Salvage").Or.Contain("NeedDef"));
        }

        // ------------------------------------------------------------------ the comparison

        /// <summary>
        /// Walk two objects of the same shape and record every public field that differs, with
        /// the path to it. Fields only: <c>Def.Origin</c> and <c>Def.Abstract</c> are properties
        /// and are provenance rather than content — the XML has a file and a line and the oracle
        /// does not, and that is not a difference in the content.
        /// </summary>
        static void Compare(object? expected, object? actual, string path, List<string> differences)
        {
            if (expected == null || actual == null)
            {
                if (!ReferenceEquals(expected, actual))
                    differences.Add($"{path}: expected {Show(expected)}, found {Show(actual)}");
                return;
            }

            Type type = expected.GetType();
            if (type != actual.GetType())
            {
                differences.Add($"{path}: expected a {type.Name}, found a {actual.GetType().Name}");
                return;
            }

            if (type.IsPrimitive || type.IsEnum || type == typeof(string))
            {
                if (!expected.Equals(actual))
                    differences.Add($"{path}: expected {Show(expected)}, found {Show(actual)}");
                return;
            }

            if (expected is IList expectedList && actual is IList actualList)
            {
                if (expectedList.Count != actualList.Count)
                {
                    differences.Add($"{path}: expected {expectedList.Count} entries, found {actualList.Count}");
                    return;
                }
                for (int i = 0; i < expectedList.Count; i++)
                    Compare(expectedList[i], actualList[i], $"{path}[{i}]", differences);
                return;
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                Compare(field.GetValue(expected), field.GetValue(actual), $"{path}.{field.Name}", differences);
        }

        static string Show(object? value) => value == null ? "nothing" : $"'{value}'";
    }
}
