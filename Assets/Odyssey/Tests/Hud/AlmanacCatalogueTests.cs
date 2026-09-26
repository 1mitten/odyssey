#nullable enable
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// What keeps the Almanac true to the game (owner, 2026-09-26: <i>"ensure the almanac in game is
    /// completely up-to-date with everything in game. The information is correct and links are
    /// good"</i>). The HUD's own tables say what the game has — every item it can label, every
    /// standing thing and ground it can name, every animal and person, every live skill, work type,
    /// palette tool, weather and event — so a new thing that ships without a page fails here, in
    /// the fast tier, rather than in a player's hands.
    /// </summary>
    public class AlmanacCatalogueTests
    {
        static IEnumerable<AlmanacEntry> Entries()
        {
            foreach (AlmanacCategory category in AlmanacCatalogue.Categories)
                foreach (AlmanacEntry entry in category.Entries)
                    yield return entry;
        }

        static void Want(List<string> missing, string key, string table)
        {
            if (key.Length == 0) return;
            if (AlmanacCatalogue.ForKey(key) == null) missing.Add($"{key} ({table})");
        }

        [Test]
        public void EveryThingTheGameCanShowHasAnEntry()
        {
            var missing = new List<string>();
            foreach (string key in ItemLabels.Keys) Want(missing, key, "ItemLabels");
            foreach (string key in EdificeLabels.Keys) Want(missing, key, "EdificeLabels");
            foreach (string key in TerrainLabels.Keys) Want(missing, key, "TerrainLabels");
            foreach (string key in PawnKindLabels.IconKeys) Want(missing, key, "PawnKindLabels");
            foreach (string key in WeatherLabels.IconKeys) Want(missing, key, "WeatherLabels");
            foreach (string key in IncidentLabels.Keys) Want(missing, key, "IncidentLabels");
            foreach (PaletteTool tool in PaletteTools.Live) Want(missing, tool.Key, "PaletteTools.Live");
            foreach (SkillCatalogue.Entry skill in SkillCatalogue.All)
                if (skill.Live) Want(missing, skill.Key, "SkillCatalogue (live)");
            foreach (WorkCatalogue.Entry work in WorkCatalogue.All)
                if (work.Live) Want(missing, work.Key, "WorkCatalogue (live)");
            for (int building = 0; building < 64; building++) Want(missing, BuildLabels.BuildingKey(building), "BuildLabels");

            Assert.That(missing, Is.Empty,
                "the game can show these and the Almanac has no page for them; add an entry keyed by " +
                "each (AlmanacCatalogue.*.cs), or an alias on the entry that covers it:\n  " + string.Join("\n  ", missing));
        }

        [Test]
        public void EveryLinkGoesSomewhereElse()
        {
            var broken = new List<string>();
            foreach (AlmanacEntry entry in Entries())
                foreach ((string target, string _) in entry.Related)
                {
                    AlmanacEntry? to = AlmanacCatalogue.Resolve(target);
                    if (to == null) broken.Add($"{entry.Name} → {target}");
                    else if (ReferenceEquals(to, entry)) broken.Add($"{entry.Name} → itself");
                }
            Assert.That(broken, Is.Empty, "links that go nowhere:\n  " + string.Join("\n  ", broken));
        }

        /// <summary>
        /// A name may repeat across categories — the Construction skill and the Construction work
        /// type are one word for two things, as on the Work tab — but never within one, because
        /// the director navigates by category and name. A key never repeats anywhere.
        /// </summary>
        [Test]
        public void NoTwoEntriesShareAKeyOrANameInOneCategory()
        {
            var names = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var keys = new HashSet<string>();
            foreach (AlmanacEntry entry in Entries())
            {
                Assert.That(names.Add(entry.CategoryName + "/" + entry.Name), Is.True, $"two {entry.CategoryName} entries are called {entry.Name}");
                if (entry.Key.Length > 0) Assert.That(keys.Add(entry.Key), Is.True, $"two entries are keyed {entry.Key}");
                foreach (string also in entry.AlsoKeys) Assert.That(keys.Add(also), Is.True, $"two entries answer to {also}");
            }
        }

        [Test]
        public void AKeyedEntryIsNamedAndSummarisedByTheRegistry()
        {
            foreach (AlmanacEntry entry in Entries())
            {
                if (entry.Key.Length == 0) continue;
                Assert.That(Registry.Labels.ContainsKey(entry.Key), Is.True, $"{entry.Key} is not a registry key");
                Assert.That(entry.Name, Is.EqualTo(Registry.Label(entry.Key)), entry.Key);
                Assert.That(Registry.Describe(entry.Key), Is.Not.Empty,
                    $"{entry.Key} has no description in Registry.g.cs; add its namespace to emit_labels.py DESCRIBED");
                foreach (string also in entry.AlsoKeys)
                    Assert.That(Registry.Labels.ContainsKey(also), Is.True, $"{entry.Name}'s alias {also} is not a registry key");
            }
        }

        /// <summary>Every entry draws a line icon when there is no pixel art, so every path must parse.</summary>
        [Test]
        public void EveryIconIsAPathThatDraws()
        {
            foreach (AlmanacCategory category in AlmanacCatalogue.Categories)
            {
                Assert.That(SvgPath.Parse(category.IconPath), Is.Not.Empty, category.Name);
                foreach (AlmanacEntry entry in category.Entries)
                    Assert.That(SvgPath.Parse(entry.Icon.Path), Is.Not.Empty, entry.Name);
            }
        }

        /// <summary>A find-on-map button needs a key to find by.</summary>
        [Test]
        public void OnlyAKeyedEntryOffersToFindItselfOnTheMap()
        {
            foreach (AlmanacEntry entry in Entries())
                if (entry.Action == AlmanacAction.FindOnMap)
                    Assert.That(entry.Key, Is.Not.Empty, entry.Name);
        }

        /// <summary>
        /// The first Almanac was a mock-up's text: silver prices, steel, pines, traits, inspirations.
        /// None of it is in the game, and none of it may come back by copy and paste.
        /// </summary>
        [Test]
        public void NothingFromTheMockUpComesBack()
        {
            var ghost = new Regex(@"\b(silver|steel|pines?|inspirations?|traits?|iron stomach|night owl|hard worker|concrete|cold snap|smelter)\b",
                RegexOptions.IgnoreCase);
            var found = new List<string>();
            foreach (AlmanacEntry entry in Entries())
            {
                var texts = new List<string> { entry.Name, entry.Summary, entry.Definition, entry.Source, entry.Body.Paragraph };
                foreach ((string k, string v) in entry.Properties) { texts.Add(k); texts.Add(v); }
                if (entry.Body.Effects != null) foreach ((string a, string b) in entry.Body.Effects) { texts.Add(a); texts.Add(b); }
                if (entry.Body.Specs != null) foreach ((string a, string b, string c) in entry.Body.Specs) { texts.Add(a); texts.Add(b); texts.Add(c); }
                if (entry.Body.Levels != null) foreach ((string a, string b, string c) in entry.Body.Levels) { texts.Add(a); texts.Add(b); texts.Add(c); }
                foreach (string text in texts)
                {
                    Match match = ghost.Match(text);
                    if (match.Success) found.Add($"{entry.Name}: \"{match.Value}\" in \"{text}\"");
                }
            }
            Assert.That(found, Is.Empty, "the Almanac names things the game does not have:\n  " + string.Join("\n  ", found));
        }
    }
}
