#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The Almanac's account of how every animal behaves (<see cref="AnimalBehaviour"/>): complete for
    /// the forest roster, held to the content it describes where the game does it today, and marked
    /// wherever it does not yet.
    /// </summary>
    public class AnimalBehaviourTests
    {
        static readonly int[] Forest =
        {
            PawnKindLabels.VergeRabbit, PawnKindLabels.HedgerowDeer, PawnKindLabels.AshFox,
            PawnKindLabels.GutterRaccoon, PawnKindLabels.RubbleSkunk, PawnKindLabels.ThicketBoar,
            PawnKindLabels.MireMoose, PawnKindLabels.RidgeWolf, PawnKindLabels.QuarryBear,
        };

        [Test]
        public void EveryForestKindHasEveryFact()
        {
            foreach (int kind in Forest)
            {
                AnimalBehaviourRecord? record = AnimalBehaviour.For(kind);
                Assert.That(record, Is.Not.Null, "kind " + kind);
                for (var fact = AnimalFact.Temperament; fact <= AnimalFact.Signature; fact++)
                {
                    bool found = false;
                    foreach (AnimalFactLine line in record!.Lines)
                        found |= line.Fact == fact;
                    Assert.That(found, Is.True, record.KindDefName + " says nothing under " + fact);
                }
            }
        }

        [Test]
        public void EveryAnimalKindHasARecordAndNoHostileDoes()
        {
            var kinds = new HashSet<int>();
            foreach (AnimalBehaviourRecord record in AnimalBehaviour.Records)
                Assert.That(kinds.Add(record.Kind), Is.True, "two records for kind " + record.Kind);
            foreach (int kind in Forest)
                Assert.That(kinds.Contains(kind), Is.True, "kind " + kind);
            foreach (int kind in new[] { PawnKindLabels.MiddenHogKind, PawnKindLabels.DuctRatKind, PawnKindLabels.CulvertFrogKind })
                Assert.That(kinds.Contains(kind), Is.True, "kind " + kind);
            foreach (int kind in new[] { PawnKindLabels.ColonistKind, PawnKindLabels.Bandit, PawnKindLabels.Gunman,
                         PawnKindLabels.Butcher, PawnKindLabels.ButcherScarred, PawnKindLabels.ButcherBlood, PawnKindLabels.ButcherKing })
                Assert.That(AnimalBehaviour.For(kind), Is.Null, "kind " + kind + " is not an animal");
            Assert.That(AnimalBehaviour.Records.Count, Is.EqualTo(12));
        }

        [Test]
        public void EveryFactAndTemperamentHasARegistryLabel()
        {
            for (var fact = AnimalFact.Temperament; fact <= AnimalFact.Signature; fact++)
                Assert.That(Registry.Labels.ContainsKey(AnimalBehaviour.FactKey(fact)), Is.True, fact.ToString());
            Assert.That(Registry.Labels.ContainsKey(AnimalBehaviour.NotYetKey), Is.True);
            foreach (AnimalBehaviourRecord record in AnimalBehaviour.Records)
            {
                Assert.That(Registry.Labels.ContainsKey(record.TemperamentKey), Is.True, record.KindDefName);
                Assert.That(Registry.Descriptions.ContainsKey(record.TemperamentKey), Is.True,
                    record.TemperamentKey + " is drawn with its description (emit_labels.py DESCRIBED)");
            }
        }

        /// <summary>The live <i>When struck</i> line is the species' <c>revengePerMille</c> in words, so retuning the number moves the words.</summary>
        [Test]
        public void TheStruckLineAgreesWithTheDefs()
        {
            Dictionary<string, XElement> species = SpeciesByKind();
            foreach (AnimalBehaviourRecord record in AnimalBehaviour.Records)
            {
                Assert.That(species.TryGetValue(record.KindDefName, out XElement? def), Is.True, record.KindDefName);
                int revenge = int.Parse(def!.Element("revengePerMille")?.Value ?? "0");
                Assert.That(record.Struck, Is.EqualTo(AnimalBehaviour.BandOf(revenge)),
                    record.KindDefName + ": revengePerMille " + revenge);
                bool said = false;
                foreach (AnimalFactLine line in record.Lines)
                    if (line.Fact == AnimalFact.Struck && line.Live)
                    {
                        Assert.That(line.Text, Is.EqualTo(AnimalBehaviour.StruckText(record.Struck)), record.KindDefName);
                        said = true;
                    }
                Assert.That(said, Is.True, record.KindDefName);
            }
        }

        /// <summary>The live <i>Active</i> lines are the species' <c>nocturnal</c> and <c>ignoresRain</c>.</summary>
        [Test]
        public void TheActiveLinesAgreeWithTheDefs()
        {
            Dictionary<string, XElement> species = SpeciesByKind();
            foreach (AnimalBehaviourRecord record in AnimalBehaviour.Records)
            {
                XElement def = species[record.KindDefName];
                Assert.That(record.Nocturnal, Is.EqualTo(def.Element("nocturnal")?.Value == "true"), record.KindDefName);
                Assert.That(record.ShelterFromRain, Is.EqualTo(def.Element("ignoresRain")?.Value != "true"), record.KindDefName);
            }
        }

        /// <summary>The live <i>Group</i> line is the seeded group size, across the meadow's table and the city's.</summary>
        [Test]
        public void TheGroupLineAgreesWithTheWildlifeTables()
        {
            string? sim = Find(Path.Combine("Assets", "Odyssey", "Sim", "Worldgen"));
            Assert.That(sim, Is.Not.Null, "the Sim sources are read, not referenced: this assembly cannot see them");
            var text = new StringBuilder();
            text.Append(File.ReadAllText(Path.Combine(sim!, "Natural", "NaturalMapGenDef.cs")));
            text.Append(File.ReadAllText(Path.Combine(sim!, "WorldGenDefs.cs")));
            var range = new Dictionary<string, (int Min, int Max)>();
            foreach (Match m in Regex.Matches(text.ToString(), "WildlifeEntry\\(\"(\\w+)\",\\s*\\d+,\\s*(\\d+),\\s*(\\d+)"))
            {
                string kind = m.Groups[1].Value;
                int min = int.Parse(m.Groups[2].Value), max = int.Parse(m.Groups[3].Value);
                range[kind] = range.TryGetValue(kind, out var had)
                    ? (System.Math.Min(had.Min, min), System.Math.Max(had.Max, max))
                    : (min, max);
            }
            foreach (AnimalBehaviourRecord record in AnimalBehaviour.Records)
            {
                Assert.That(range.TryGetValue(record.KindDefName, out var r), Is.True, record.KindDefName + " is in no wildlife table");
                Assert.That((record.GroupMin, record.GroupMax), Is.EqualTo(r), record.KindDefName);
            }
        }

        [Test]
        public void TheAlmanacDrawsTheTableInItsOrder()
        {
            foreach (AnimalBehaviourRecord record in AnimalBehaviour.Records)
            {
                AlmanacEntry? entry = AlmanacCatalogue.GetEntry(PawnKindLabels.Label(record.Kind));
                Assert.That(entry, Is.Not.Null, record.KindDefName);
                Assert.That(entry!.Body.Specs, Is.EqualTo(AnimalBehaviour.SpecsFor(record.Kind)), record.KindDefName);
                Assert.That(entry.Body.Specs!.Count, Is.EqualTo(record.Lines.Count), record.KindDefName);
            }
        }

        [Test]
        public void ALineTheGameDoesNotDoYetCarriesTheMarker()
        {
            Assert.That(Registry.Label(AnimalBehaviour.NotYetKey), Is.EqualTo("Not yet in the game"));
            foreach (AnimalBehaviourRecord record in AnimalBehaviour.Records)
            {
                IReadOnlyList<(string Title, string Value, string Detail)> specs = AnimalBehaviour.SpecsFor(record.Kind);
                for (int i = 0; i < record.Lines.Count; i++)
                {
                    AnimalFactLine line = record.Lines[i];
                    Assert.That(specs[i].Title, Is.EqualTo(Registry.Label(AnimalBehaviour.FactKey(line.Fact))));
                    Assert.That(specs[i].Detail, Is.EqualTo(line.Live ? "" : Registry.Label(AnimalBehaviour.NotYetKey)),
                        record.KindDefName + ": " + line.Text);
                }
            }
        }

        /// <summary>
        /// Exactly which facts the game does today, kind by kind. **FA2 and FA3 flip a line to live in
        /// the same commit that builds it, and edit this list beside it** — nothing else checks that a
        /// line's liveness is true (the SK5 lesson). The temperament is FA2's everywhere; warnings,
        /// herds, predators and the kill window are FA2's; hours, the spray, the rut, grazing and
        /// raids are FA3's.
        /// </summary>
        [Test]
        public void TheLiveLinesAreExactlyTheseToday()
        {
            var expected = new Dictionary<int, string>
            {
                [PawnKindLabels.MiddenHogKind] = "Struck Group Active Active Hunts Danger Signature",
                [PawnKindLabels.DuctRatKind] = "Struck Group Active Active Hunts Danger Signature",
                [PawnKindLabels.CulvertFrogKind] = "Struck Group Active Hunts Danger Signature",
                [PawnKindLabels.VergeRabbit] = "Struck Group Active Active Danger Signature",
                [PawnKindLabels.HedgerowDeer] = "Struck Group Active Active Danger Signature",
                [PawnKindLabels.AshFox] = "Struck Group Active Active Danger",
                [PawnKindLabels.GutterRaccoon] = "Struck Group Active Active Danger",
                [PawnKindLabels.RubbleSkunk] = "Struck Group Active Active Hunts Danger",
                [PawnKindLabels.ThicketBoar] = "Struck Group Active Active Hunts Danger Signature",
                [PawnKindLabels.MireMoose] = "Struck Group Active Active Hunts Danger Signature",
                [PawnKindLabels.RidgeWolf] = "Struck Group Active Active Danger",
                [PawnKindLabels.QuarryBear] = "Struck Group Active Active Hunts Danger",
            };
            foreach (AnimalBehaviourRecord record in AnimalBehaviour.Records)
            {
                var live = new List<string>();
                foreach (AnimalFactLine line in record.Lines)
                    if (line.Live)
                        live.Add(line.Fact.ToString());
                Assert.That(string.Join(" ", live), Is.EqualTo(expected[record.Kind]), record.KindDefName);
                foreach (AnimalFactLine line in record.Lines)
                    if (line.Fact == AnimalFact.Temperament || line.Fact == AnimalFact.Approached)
                        Assert.That(line.Live, Is.False, record.KindDefName + ": " + line.Fact + " is FA2's");
            }
        }

        /// <summary>Species Defs by the kind that names them: kind defName to the species element.</summary>
        static Dictionary<string, XElement> SpeciesByKind()
        {
            string? pawns = Find(Path.Combine("Assets", "Odyssey", "Defs", "Core", "Pawns"));
            Assert.That(pawns, Is.Not.Null);
            XDocument doc = XDocument.Load(Path.Combine(pawns!, "Species.xml"));
            var species = new Dictionary<string, XElement>();
            foreach (XElement e in doc.Descendants())
            {
                string? name = e.Element("defName")?.Value;
                if (name != null && name.StartsWith("Species_"))
                    species[name] = e;
            }
            var byKind = new Dictionary<string, XElement>();
            foreach (XElement e in doc.Descendants())
            {
                string? name = e.Element("defName")?.Value;
                string? of = e.Element("species")?.Value;
                if (name != null && of != null && name.StartsWith("PawnKind_") && species.TryGetValue(of, out XElement? def))
                    byKind[name] = def;
            }
            return byKind;
        }

        static string? Find(string relative)
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 12 && directory != null; i++, directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (Directory.Exists(candidate)) return candidate;
            }
            return null;
        }
    }
}
