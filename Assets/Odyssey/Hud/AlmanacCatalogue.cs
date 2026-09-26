#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// What an entry's button does. An entry is for a thing on the map, for a thing whose live
    /// half is a tab, or for neither — and then it has no button, rather than one that closes the
    /// Almanac and does nothing (which is what every Skills, Needs and Health page did until
    /// 2026-09-26).
    /// </summary>
    public enum AlmanacAction
    {
        None,
        FindOnMap,
        OpenWork,
        OpenAnimals,
        OpenInventory,
        OpenAssign,
        OpenBuild,
    }

    public sealed class AlmanacBody
    {
        public string Type { get; }
        public string SectionLabel { get; }
        public string Paragraph { get; }
        public IReadOnlyList<(string Title, string Kind, string Text)>? Cards { get; }
        public IReadOnlyList<(string Range, string Name, string Note)>? Levels { get; }
        public IReadOnlyList<(string Title, string Value, string Detail)>? Specs { get; }
        public IReadOnlyList<(string Label, string Desc)>? Effects { get; }

        public AlmanacBody(
            string type,
            string sectionLabel,
            string paragraph,
            IReadOnlyList<(string Title, string Kind, string Text)>? cards = null,
            IReadOnlyList<(string Range, string Name, string Note)>? levels = null,
            IReadOnlyList<(string Title, string Value, string Detail)>? specs = null,
            IReadOnlyList<(string Label, string Desc)>? effects = null)
        {
            Type = type;
            SectionLabel = sectionLabel;
            Paragraph = paragraph;
            Cards = cards;
            Levels = levels;
            Specs = specs;
            Effects = effects;
        }
    }

    public sealed class AlmanacEntry
    {
        /// <summary>
        /// The registry key the entry is about, or empty for an idea that has no key of its own
        /// (a season, the temperature). The key is how everything else finds the entry: the
        /// inspect pane's info button, a related link, <c>Find on map</c>.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Further keys that open this entry: a picked berry bush is a berry bush, a stair's
        /// two halves are one stair.
        /// </summary>
        public IReadOnlyList<string> AlsoKeys { get; }

        public string Name { get; }
        public string CategoryName { get; }

        /// <summary>The one line under the name in the index: the registry's own description where the entry has a key.</summary>
        public string Summary { get; }
        public string TypeChip { get; }
        public string NatureChip { get; }
        /// <summary>
        /// The key its picture is drawn by: its own key, or a key chosen for an idea that has none.
        /// The picture itself is never here — it is the owner's art for the key, or the key's line
        /// art in <see cref="IconGlyphs"/>, which is what every icon slot in the game draws.
        /// </summary>
        public string IconKey { get; }
        public string Definition { get; }

        /// <summary>Where a player meets it: how it is made, found or arrives. A fact, never a count.</summary>
        public string Source { get; }
        public AlmanacAction Action { get; }
        public IReadOnlyList<(string Key, string Value)> Properties { get; }
        public AlmanacBody Body { get; }

        /// <summary>Links, each naming its target by registry key (or by name where it has none).</summary>
        public IReadOnlyList<(string Target, string Reason)> Related { get; }

        public AlmanacEntry(
            string key,
            string name,
            string categoryName,
            string summary,
            string typeChip,
            string natureChip,
            string iconKey,
            string definition,
            string source,
            AlmanacAction action,
            IReadOnlyList<(string Key, string Value)> properties,
            AlmanacBody body,
            IReadOnlyList<(string Target, string Reason)> related,
            IReadOnlyList<string>? alsoKeys = null)
        {
            Key = key;
            Name = name;
            CategoryName = categoryName;
            Summary = summary;
            TypeChip = typeChip;
            NatureChip = natureChip;
            IconKey = iconKey;
            Definition = definition;
            Source = source;
            Action = action;
            Properties = properties;
            Body = body;
            Related = related;
            AlsoKeys = alsoKeys ?? Array.Empty<string>();
        }

        /// <summary>The button's words for <see cref="Action"/>; empty when there is no button.</summary>
        public string PrimaryAction => Action switch
        {
            AlmanacAction.FindOnMap => "Find on map",
            AlmanacAction.OpenWork => "Open the Work tab",
            AlmanacAction.OpenAnimals => "Open the Animals tab",
            AlmanacAction.OpenInventory => "Open the Inventory",
            AlmanacAction.OpenAssign => "Open the Assign tab",
            AlmanacAction.OpenBuild => "Open Build",
            _ => string.Empty,
        };
    }

    public sealed class AlmanacCategory
    {
        public string Name { get; }
        public string IconPath { get; }
        public IReadOnlyList<AlmanacEntry> Entries { get; }

        public AlmanacCategory(string name, string iconPath, IReadOnlyList<AlmanacEntry> entries)
        {
            Name = name;
            IconPath = iconPath;
            Entries = entries;
        }
    }

    /// <summary>
    /// The Almanac's content: every thing in the game a player can meet, and what it does, as the
    /// Defs and the simulation have it on the day it was written.
    ///
    /// <para><b>Rewritten 2026-09-26 against the code</b> (owner: <i>"ensure the almanac in game is
    /// completely up-to-date with everything in game. The information is correct and links are
    /// good"</i>). The first version was a mock-up's text: silver prices, steel smelting, pine
    /// trees, traits, a cold-snap event and live counts none of which existed. Every number here
    /// is now the Def's or the code's, and <c>AlmanacFactsTests</c> reads the Def XML to hold the
    /// ones that live there.</para>
    ///
    /// <para><b>Three things keep it true.</b> An entry is keyed by its registry key, so its name
    /// is the registry's and a rename moves it. <c>AlmanacCatalogueTests</c> walks the HUD's own
    /// tables of what the game has — items, standing things, ground, animals, live skills and work
    /// types, events — and fails on any without an entry, so a new thing cannot ship unexplained.
    /// And every link is resolved by a test, so none goes nowhere.</para>
    /// </summary>
    public static partial class AlmanacCatalogue
    {
        public static readonly IReadOnlyList<AlmanacCategory> Categories = BuildCategories();

        static readonly Dictionary<string, AlmanacCategory> CategoriesByName = new(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, AlmanacEntry> EntriesByName = new(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, AlmanacEntry> EntriesByKey = new(StringComparer.Ordinal);

        static AlmanacCatalogue()
        {
            foreach (AlmanacCategory cat in Categories)
            {
                CategoriesByName[cat.Name] = cat;
                foreach (AlmanacEntry entry in cat.Entries)
                {
                    EntriesByName[entry.Name] = entry;
                    if (entry.Key.Length > 0) EntriesByKey[entry.Key] = entry;
                    foreach (string also in entry.AlsoKeys) EntriesByKey[also] = entry;
                }
            }
        }

        public static AlmanacCategory? GetCategory(string name) =>
            CategoriesByName.TryGetValue(name, out AlmanacCategory? cat) ? cat : null;

        /// <summary>
        /// An entry by name alone. Names repeat across categories — the Construction skill and the
        /// Construction work type — so navigation asks with the category; this is for a link to an
        /// entry that has no key, whose name is its only handle.
        /// </summary>
        public static AlmanacEntry? GetEntry(string name) =>
            EntriesByName.TryGetValue(name, out AlmanacEntry? entry) ? entry : null;

        /// <summary>An entry by its category and name: the pair the director navigates by.</summary>
        public static AlmanacEntry? GetEntry(string category, string name)
        {
            AlmanacCategory? cat = GetCategory(category);
            if (cat == null) return null;
            foreach (AlmanacEntry entry in cat.Entries)
                if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)) return entry;
            return null;
        }

        /// <summary>The entry a registry key opens, including its <see cref="AlmanacEntry.AlsoKeys"/>.</summary>
        public static AlmanacEntry? ForKey(string key) =>
            key.Length > 0 && EntriesByKey.TryGetValue(key, out AlmanacEntry? entry) ? entry : null;

        /// <summary>A link's target: a registry key first, then a name.</summary>
        public static AlmanacEntry? Resolve(string keyOrName) => ForKey(keyOrName) ?? GetEntry(keyOrName);

        public static int TotalEntriesCount
        {
            get
            {
                int count = 0;
                foreach (AlmanacCategory cat in Categories) count += cat.Entries.Count;
                return count;
            }
        }

        // ---- the categories, in the rail's order
        public const string Terrain = "Terrain";
        public const string Flora = "Flora";
        public static string Materials => Registry.Label("ui.res.category.materials");
        public static string Food => Registry.Label("ui.res.category.food");
        public static string Medicine => Registry.Label("ui.res.category.medicine");
        public static string Weapons => Registry.Label("ui.res.category.weapons");
        public const string Structures = "Structures";
        public static string Furniture => Registry.Label("ui.arch.category.furniture");
        public static string Production => Registry.Label("ui.arch.category.production");
        public static string Power => Registry.Label("ui.arch.category.power");
        public const string ZonesAndOrders = "Zones and orders";
        public const string People = "People";
        public const string Fauna = "Fauna";
        public const string Skills = "Skills";
        public const string WorkTypes = "Work types";
        public const string Needs = "Needs";
        public const string Health = "Health";
        public const string Weather = "Weather and seasons";
        public const string Events = "Events";

        static IReadOnlyList<AlmanacCategory> BuildCategories()
        {
            var list = new List<AlmanacCategory>
            {
                new AlmanacCategory(Terrain, "M2 18l5-10 6 5 9-9v14H2z", TerrainEntries()),
                new AlmanacCategory(Flora, "M12 22v-8 M8 14c0-4 4-8 4-8s4 4 4 8 M4 22h16", FloraEntries()),
                new AlmanacCategory(Materials, "M19 7l-7-4-7 4v10l7 4 7-4V7z", MaterialEntries()),
                new AlmanacCategory(Food, "M6 3v8a3 3 0 0 0 6 0V3M9 3v18M17 3c-2 2-2 6 0 8v10", FoodEntries()),
                new AlmanacCategory(Medicine, "M4 7h16v13H4z M9 7V4h6v3 M12 10v7 M8.5 13.5h7", MedicineEntries()),
                new AlmanacCategory(Weapons, "M14.5 3.5l6 6-11 11-3-3z M3.5 20.5l3-3 M12 6l6 6", WeaponEntries()),
                new AlmanacCategory(Structures, "M3 21h18M5 21V7l7-4 7 4v14", StructureEntries()),
                new AlmanacCategory(Furniture, "M3 7v11M21 7v11M3 13h18M3 9h8a2 2 0 0 1 2 2v2H3z", FurnitureEntries()),
                new AlmanacCategory(Production, "M4 8h16v12H4z M4 12h16 M8 5v3 M12 5v3 M16 5v3", ProductionEntries()),
                new AlmanacCategory(Power, "M13 2L4 14h7l-1 8 9-12h-7l1-8z", PowerEntries()),
                new AlmanacCategory(ZonesAndOrders, "M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z", ZoneAndOrderEntries()),
                new AlmanacCategory(People, "M12 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8z M4 21a8 8 0 0 1 16 0", PeopleEntries()),
                new AlmanacCategory(Fauna, "M4.5 9.5a3.5 3.5 0 1 1 7 0 3.5 3.5 0 0 1-7 0z M12.5 9.5a3.5 3.5 0 1 1 7 0 3.5 3.5 0 0 1-7 0z M8.5 15.5a3.5 3.5 0 1 1 7 0 3.5 3.5 0 0 1-7 0z", FaunaEntries()),
                new AlmanacCategory(Skills, "M12 2l3 7h7l-5.5 4.5 2 7.5-6.5-4.5-6.5 4.5 2-7.5-5.5-4.5h7z", SkillEntries()),
                new AlmanacCategory(WorkTypes, "M9 5H7a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-2M9 5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2", WorkEntries()),
                new AlmanacCategory(Needs, "M12 21l-1.4-1.3C5.4 15.4 2 12.3 2 8.5 2 5.4 4.4 3 7.5 3c1.7 0 3.4.8 4.5 2.1C13.1 3.8 14.8 3 16.5 3 19.6 3 22 5.4 22 8.5c0 3.8-3.4 6.9-8.6 11.5z", NeedEntries()),
                new AlmanacCategory(Health, "M22 12h-4l-3 9L9 3l-3 9H2", HealthEntries()),
                new AlmanacCategory(Weather, "M7 18a5 5 0 1 1 1-9.9A6 6 0 0 1 19 10a4 4 0 0 1 0 8z", WeatherEntries()),
                new AlmanacCategory(Events, "M12 2v10 M12 12l4-4 M12 12l-4-4 M4 16h16v4H4z", EventEntries()),
            };
            return list;
        }

        // ---- helpers the content files share

        /// <summary>A registry name, for a property value or a sentence: never the word typed twice.</summary>
        static string L(string key) => Registry.Label(key);

        /// <summary>A registry name lower-cased, for the middle of a sentence.</summary>
        static string Lc(string key) => Registry.Label(key).ToLowerInvariant();

        /// <summary>
        /// An entry about a registry key: its name and its index line are the registry's, so the
        /// wiki, the pane and the Almanac say the same thing.
        /// </summary>
        static AlmanacEntry Keyed(
            string key, string category, string typeChip, string natureChip,
            string definition, string source, AlmanacAction action,
            (string, string)[] properties, AlmanacBody body, (string, string)[] related,
            string[]? alsoKeys = null, string? summary = null)
        {
            string line = summary ?? Registry.Describe(key);
            return new AlmanacEntry(key, Registry.Label(key), category, line, typeChip, natureChip, key,
                definition, source, action, properties, body, related, alsoKeys);
        }

        /// <summary>An entry for an idea with no registry key of its own.</summary>
        static AlmanacEntry Named(
            string name, string category, string summary, string typeChip, string natureChip, string iconKey,
            string definition, string source, AlmanacAction action,
            (string, string)[] properties, AlmanacBody body, (string, string)[] related) =>
            new AlmanacEntry(string.Empty, name, category, summary, typeChip, natureChip, iconKey,
                definition, source, action, properties, body, related);

        static AlmanacBody Effects(string label, string paragraph, params (string, string)[] effects) =>
            new AlmanacBody("simple", label, paragraph, effects: effects);

        static AlmanacBody Specs(string paragraph, params (string, string, string)[] specs) =>
            new AlmanacBody("item", "SPECIFICATIONS", paragraph, specs: specs);

        static AlmanacBody Levels(string paragraph, params (string, string, string)[] levels) =>
            new AlmanacBody("skill", "LEVELS", paragraph, levels: levels);
    }
}
