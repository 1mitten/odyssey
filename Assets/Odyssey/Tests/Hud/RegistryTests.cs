#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The HUD names nothing itself: every key it shows must be one the naming registry knows,
    /// so a name that is not in <c>docs/design/icon-keys.csv</c> (and so not in the wiki) fails
    /// here, in the fast tier, before it can reach the screen.
    /// </summary>
    public class RegistryTests
    {
        [Test]
        public void EveryJobKeyIsARegisteredName()
        {
            foreach (string key in JobLabels.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
            Assert.That(Registry.Labels, Does.ContainKey(JobLabels.IconKey(-1)), "the fallback key must be registered too");
        }

        [Test]
        public void EverySkillKeyIsARegisteredName()
        {
            foreach (string key in SkillCatalogue.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
        }

        [Test]
        public void EveryLedgerKeyIsARegisteredName()
        {
            foreach (string key in LedgerModel.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
        }

        /// <summary>
        /// <c>AlertModel.IconKeys</c> said it existed "for the registry test" from the day it was
        /// written, and no such test did until the events work (2026-09-20).
        /// </summary>
        [Test]
        public void EveryDebugKeyIsARegisteredName()
        {
            foreach (string key in DebugDirector.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
        }

        [Test]
        public void EveryAlertKeyIsARegisteredName()
        {
            foreach (string key in AlertModel.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
        }

        /// <summary>
        /// The Work tab's three key sets, and <b>the third time a catalogue has arrived carrying an
        /// <c>IconKeys</c> array for a registry test that nobody wrote</b>.
        ///
        /// <para><c>AlertModel</c> did it and went two milestones unchecked; <c>DebugDirector</c>
        /// did it and was caught by the events work; <c>WorkCatalogue</c>, <c>ScheduleCatalogue</c>
        /// and <c>WorkDirector</c> all did it on 2026-09-20 and were caught by this review. The
        /// array is not the guard — <b>the loop over it is</b>, and an array that no loop reads is
        /// a comment claiming a test exists.</para>
        ///
        /// <para>What it buys: the grid's twenty-two column headers, the legend's six block names
        /// and the panel's own title all come out of <c>Registry.Label</c>, and a key the CSV does
        /// not know draws as a raw key on the screen. Twenty-nine names, checked in the fast tier,
        /// before the panel is ever opened.</para>
        /// </summary>
        [Test]
        public void EveryWorkTabKeyIsARegisteredName()
        {
            foreach (string key in WorkCatalogue.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
            foreach (string key in ScheduleCatalogue.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
            foreach (string key in WorkDirector.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
            foreach (string key in WildlifeDirector.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");

            // And the two lists are the ones the panel actually draws from, not copies of them:
            // an entry added to a catalogue and forgotten in its IconKeys would pass the loops
            // above by saying nothing, which is the failure mode this pairing closes.
            Assert.That(WorkCatalogue.IconKeys.Length, Is.EqualTo(WorkCatalogue.All.Count));
            Assert.That(ScheduleCatalogue.IconKeys.Length, Is.EqualTo(ScheduleCatalogue.All.Count));
            for (int i = 0; i < WorkCatalogue.All.Count; i++)
                Assert.That(WorkCatalogue.IconKeys[i], Is.EqualTo(WorkCatalogue.All[i].Key));
            for (int i = 0; i < ScheduleCatalogue.All.Count; i++)
                Assert.That(ScheduleCatalogue.IconKeys[i], Is.EqualTo(ScheduleCatalogue.All[i].Key));
        }

        [Test]
        public void EveryAlmanacKeyIsARegisteredName()
        {
            foreach (string key in AlmanacDirector.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
        }

        [Test]
        public void EveryIncidentKeyIsARegisteredNameAndTheTableIsTheHandleTable()
        {
            Assert.That(IncidentLabels.Keys.Length, Is.EqualTo(IncidentHandle.Count),
                "an incident the label table does not know draws as the fallback key");
            foreach (string key in IncidentLabels.Keys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
            Assert.That(Registry.Labels, Does.ContainKey(IncidentLabels.Unknown), "the fallback key must be registered too");
        }

        /// <summary>
        /// The Def file spells each incident's key beside the incident; this assembly spells the
        /// same keys in <see cref="IncidentLabels"/> because it cannot import the Def (ADR 0003).
        /// The two sets are held equal here, on the bargain <see cref="JobLabels.CarryingAspect"/>
        /// already makes for an aspect name.
        /// </summary>
        [Test]
        public void TheIncidentKeysAreTheOnesTheDefsDeclare()
        {
            string? folder = Find(Path.Combine("Assets", "Odyssey", "Defs", "Core", "Events"));
            Assert.That(folder, Is.Not.Null, "the incident Defs were not found above the test assembly");
            string xml = File.ReadAllText(Path.Combine(folder!, "Incidents.xml"));
            var declared = new List<string>();
            foreach (Match match in Regex.Matches(xml, @"<bulletinKey>\s*([^<\s]+)\s*</bulletinKey>"))
                declared.Add(match.Groups[1].Value);

            Assert.That(declared, Is.Not.Empty, "no incident in the Defs declares a bulletinKey");
            Assert.That(declared, Is.EquivalentTo(IncidentLabels.Keys),
                "IncidentLabels.Keys and the Defs' bulletinKeys are two spellings of one list");
        }

        [Test]
        public void EverySettingsKeyIsARegisteredName()
        {
            foreach (string key in SettingsDirector.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
            foreach (GraphicsOption option in SettingsDirector.All)
                Assert.That(SettingsDirector.IconKeys, Does.Contain(SettingsDirector.KeyOf(option)),
                    "an option the panel can draw but the registry test does not cover is a label nobody checks");
        }

        /// <summary>
        /// Every category and every chip on the Build palette, live or drawn disabled.
        ///
        /// <para>New coverage, and it is new because the table used to live in the shell where
        /// this tier cannot see it. A palette key that the registry does not know draws a blank
        /// label on a chip the player is looking straight at.</para>
        /// </summary>
        [Test]
        public void EveryPaletteKeyIsARegisteredName()
        {
            foreach (string key in PaletteTools.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
            foreach (PaletteTool tool in PaletteTools.Live)
                Assert.That(Registry.Labels, Does.ContainKey(tool.Key),
                    $"{tool.Key} can be armed and has no name");
        }

        /// <summary>
        /// The session rows — Save, Load, Quit to main menu, Exit, New game, Settings — held to
        /// the CSV like every other panel here (U38).
        ///
        /// <para>The second loop is the one that matters: it walks what the two surfaces actually
        /// <i>draw</i>, rather than the array that says what they might, so a row added to the
        /// table and forgotten in <c>IconKeys</c> fails here instead of drawing a key at the
        /// player.</para>
        /// </summary>
        [Test]
        public void EverySessionRowIsARegisteredName()
        {
            foreach (string key in SessionCommands.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");

            foreach (SessionContext context in SessionCommands.Contexts)
            foreach (SessionCommand command in SessionCommands.For(context))
            {
                Assert.That(Registry.Labels, Does.ContainKey(command.Key),
                    $"{command.Key} is drawn in {context} and has no name");
                Assert.That(SessionCommands.IconKeys, Does.Contain(command.Key),
                    "a row the menu can draw but the registry test does not cover is a label nobody checks");
            }
        }

        /// <summary>The naming prompt's four words, held to the CSV like every other panel.</summary>
        [Test]
        public void EverySavePromptWordIsARegisteredName()
        {
            foreach (string key in SavePrompt.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");

            // The two the prompt swaps between on its own button. A missing one would draw the key
            // at the player on exactly the press that is about to overwrite a save.
            Assert.That(SavePrompt.IconKeys, Does.Contain(SavePrompt.ConfirmKey));
            Assert.That(SavePrompt.IconKeys, Does.Contain(SavePrompt.OverwriteKey));
        }

        /// <summary>The New game screen's three words (U39), held to the CSV like every other
        /// surface.</summary>
        [Test]
        public void EveryNewGameWordIsARegisteredName()
        {
            foreach (string key in SeedField.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");

            // The row that commits is a word of its own rather than the root row's (§11.3), so a
            // rename of one must not silently become a rename of both.
            Assert.That(SeedField.StartKey, Is.Not.EqualTo(SessionCommands.NewGameKey));
        }

        [Test]
        public void EveryHotkeyKeyIsARegisteredName()
        {
            foreach (string key in HotkeyDirector.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
            foreach (HotkeyAction action in HotkeyDirector.All)
                Assert.That(HotkeyDirector.IconKeys, Does.Contain(HotkeyDirector.KeyOf(action)),
                    "an action the binding panel can draw but the registry test does not cover is a label nobody checks");
        }

        /// <summary>
        /// A job with no icon key reads as <i>idle</i>, which is a lie rather than a gap.
        ///
        /// <para>The table is bounds-checked and falls through to <c>ui.status.idle</c>, so
        /// two missing entries are not a compile error — they are every builder and every
        /// porter in the game showing as having nothing to do. That happened when the build
        /// pipeline added two job indices, and this is what stops the next one.</para>
        /// </summary>
        [Test]
        public void EveryJobHasAStatusOfItsOwn()
        {
            Assert.That(JobLabels.IconKeys.Length, Is.EqualTo(JobHandle.Count),
                "a job the table does not cover draws as idle, silently");

            foreach (string key in JobLabels.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
        }

        [Test]
        public void EveryBuildableAndMaterialHasARegisteredNameOrNoneAtAll()
        {
            Assert.That(BuildLabels.BuildingKeys.Length, Is.EqualTo(BuildingHandle.Count));
            Assert.That(BuildLabels.StuffKeys.Length, Is.EqualTo(StuffHandle.Count));

            // A blank is deliberate and means "the player can never be shown this" - the
            // three stuffs only the generator stamps, and the zero slot. Anything else must
            // be a name the wiki knows.
            foreach (string key in BuildLabels.BuildingKeys)
                if (key.Length > 0)
                    Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
            foreach (string key in BuildLabels.StuffKeys)
                if (key.Length > 0)
                    Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");

            Assert.That(BuildLabels.Building(BuildingHandle.Wall), Is.EqualTo("Wall"));
            Assert.That(BuildLabels.Stuff(StuffHandle.Wood), Is.EqualTo("wood"),
                "a material is read inside a sentence, so it is lower case");
        }

        /// <summary>
        /// <see cref="BuildShapes"/> is parallel to <see cref="BuildingHandle"/> and must stay
        /// exactly as long as it.
        ///
        /// <para><b>Written because its absence cost a merge.</b> That class's own remarks claimed
        /// "the two tables are held together the same way the labels are — a test walks both", and
        /// no such test existed. When the bed's handle moved from 2 to 5 to make room for the
        /// floor, the deck plate and the ladder, this three-entry table was <i>not</i> a conflict
        /// — main had never touched the file — so it merged in silence and the bed became a
        /// one-cell thing that could not be turned. Three <c>DesignateDirector</c> tests failed
        /// and none of them named the cause.</para>
        ///
        /// <para>A length check is the whole of what this assembly can assert: the real table is
        /// <c>ConstructionContent</c>'s, in the simulation, which the interface may not reference
        /// (ADR 0003). It is enough — a handle past the end of either array reads as one cell and
        /// no rotation, which is exactly the silently wrong answer.</para>
        /// </summary>
        [Test]
        public void EveryBuildableHasAShapeOfItsOwn()
        {
            Assert.That(BuildShapes.Cells.Length, Is.EqualTo(BuildingHandle.Count),
                "a buildable the shape table does not cover is silently one cell wide");
            Assert.That(BuildShapes.Rotates.Length, Is.EqualTo(BuildingHandle.Count),
                "a buildable the rotation table does not cover silently cannot be turned");

            // The bed is the one thing either table says anything but the default about, so it is
            // named here rather than left to the lengths to imply.
            Assert.That(BuildShapes.CellsOf(BuildingHandle.Bed), Is.EqualTo(2));
            Assert.That(BuildShapes.CanRotate(BuildingHandle.Bed), Is.True);
            Assert.That(BuildShapes.CellsOf(BuildingHandle.Wall), Is.EqualTo(1));
            Assert.That(BuildShapes.CanRotate(BuildingHandle.Wall), Is.False);
            Assert.That(BuildShapes.CanRotate(BuildingHandle.Door), Is.True);
        }

        /// <summary>
        /// The item, terrain and edifice tables the inspect pane reads, held to the same rule as
        /// the jobs: the length matches the contract's count and every key is a name the wiki
        /// knows. A table that is short is not a compile error — it is every pile of the missing
        /// thing mislabelled, or every tile of the missing terrain reading as bare ground.
        /// </summary>
        [Test]
        public void EveryItemTerrainAndEdificeKeyIsARegisteredNameOrNoneAtAll()
        {
            Assert.That(ItemLabels.Keys.Length, Is.EqualTo(ItemHandle.Count));
            foreach (string key in ItemLabels.Keys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
            Assert.That(Registry.Labels, Does.ContainKey(ItemLabels.IconKey(-1)),
                "the fallback key must be registered too");

            Assert.That(TerrainLabels.Keys.Length, Is.EqualTo(TerrainHandle.Count));
            foreach (string key in TerrainLabels.Keys)
                if (key.Length > 0)
                    Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");

            Assert.That(EdificeLabels.Keys.Length, Is.EqualTo(EdificeHandle.Count));
            foreach (string key in EdificeLabels.Keys)
                if (key.Length > 0)
                    Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");

            // The words the playtest reports were about: a pile of wood is Wood and counted, a
            // rock is Rock, water is water at the speed it is crossed at.
            Assert.That(ItemLabels.Label(ItemHandle.Wood), Is.EqualTo("Wood"));
            Assert.That(ItemLabels.Label(ItemHandle.Salvage), Is.EqualTo("Scrap"),
                "salvage is scrap: the ledger settled the word, and the pane had hard-coded the other one");
            Assert.That(TerrainLabels.Label(TerrainHandle.Rock), Is.EqualTo("Rock"));
            Assert.That(TerrainLabels.Label(TerrainHandle.ShallowWater), Is.EqualTo("Shallow Water"));
            Assert.That(TerrainLabels.Label(TerrainHandle.IronOre), Is.EqualTo("Iron ore"));
            Assert.That(EdificeLabels.Title(EdificeHandle.TreeConifer), Is.EqualTo("Conifer"));
        }

        [Test]
        public void AnUnregisteredKeyShowsItselfRatherThanNothing()
        {
            Assert.That(Registry.Label("ui.status.hauling"), Is.EqualTo("Hauling"));
            Assert.That(Registry.Label("ui.nothing.of.the.kind"), Is.EqualTo("ui.nothing.of.the.kind"),
                "a raw key on screen is a visible fault; a blank is a silent one");
        }

        // ------------------------------------------------------------------ one name, one place

        /// <summary>
        /// The namespaces this enforces: the things a player points at and talks about.
        ///
        /// <para>Not every key in the registry. "Build" and "Menu" are registry names too, and
        /// they are also ordinary English words that appear in a tooltip, a log line or a class
        /// name — enforcing those would cost more in escapes than it earns. These six are the
        /// namespaces where the same thing is named on several surfaces at once, which is where
        /// two copies actually drift apart.</para>
        /// </summary>
        static readonly string[] Enforced =
        {
            "ui.arch.tool.", "ui.arch.category.", "ui.status.", "ui.res.", "ui.alert.", "ui.job.",
            // An event is named on the Events panel today and the History screen tomorrow
            // (design 23 §5), which is this test's own criterion for a namespace.
            "ui.bulletin.",
        };

        /// <summary>
        /// No name a player reads for one of those things is written out in C#.
        ///
        /// <para><b>The centralised place already existed; this is what enforces it</b> (owner,
        /// 2026-09-17: <i>"keep the consistent in the wiki and the language and UI … ensure that
        /// consistency can be enforced using a centralised place"</i>). One name lives in
        /// <c>docs/design/icon-keys.csv</c>, is generated into <see cref="Registry"/> and printed
        /// into the wiki from the same row, so the screen and the wiki cannot disagree — as long
        /// as nobody types the word a second time. Every test above asks whether a key is
        /// registered. This asks the opposite and harder question: whether a name has been
        /// written anywhere it could go stale.</para>
        ///
        /// <para><b>It found two real duplicates on the run that introduced it.</b> The seven
        /// Build categories carried their labels in <c>PaletteTools.Categories</c> beside the keys
        /// that already named them, and the armed banner's fallback said "Building" in a literal.
        /// Both agreed with the registry at the time, which is exactly what a silent duplicate
        /// looks like until somebody corrects one copy. The banner had already been caught once
        /// this way, by hand: it said "Felling" in a <c>switch</c> and disagreed with the palette
        /// the day that chip became "Chop trees".</para>
        ///
        /// <para>It is the shape of <c>HopPriceHasOneOwnerTests</c> in the simulation assembly —
        /// read the source, fail on a second owner — and it runs in the fast tier, so the answer
        /// arrives in eleven seconds rather than in a playtest.</para>
        /// </summary>
        [Test]
        public void NoPlayerFacingNameIsWrittenInCSharp()
        {
            var watched = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> entry in Registry.Labels)
            {
                if (entry.Value.Length == 0) continue;
                foreach (string space in Enforced)
                    if (entry.Key.StartsWith(space, StringComparison.Ordinal))
                    {
                        // A label two keys share is still one word to look for; the message names
                        // the first, which is enough to find it.
                        if (!watched.ContainsKey(entry.Value)) watched[entry.Value] = entry.Key;
                        break;
                    }
            }

            Assert.That(watched.Count, Is.GreaterThan(50),
                "the registry lost most of its player-facing names, so this test is watching " +
                "almost nothing and would pass whatever the source said");

            var offences = new List<string>();
            var literal = new Regex("\"([^\"\\\\]*)\"");

            foreach (string file in Sources())
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;

                    foreach (Match match in literal.Matches(lines[i]))
                    {
                        string text = match.Groups[1].Value;
                        if (!watched.TryGetValue(text, out string? key)) continue;
                        offences.Add($"{Short(file)}:{i + 1} writes \"{text}\", which is the " +
                                     $"registry's name for {key}");
                    }
                }
            }

            Assert.That(offences, Is.Empty,
                "a player-facing name is written in C# as well as in docs/design/icon-keys.csv. " +
                "Two copies of one name drift, and the symptom is the screen and the wiki calling " +
                "one thing two things. Call Registry.Label(key) instead:\n  " +
                string.Join("\n  ", offences));
        }

        /// <summary>
        /// Every C# file the HUD and the presentation layer are built from, apart from the
        /// generated registry itself — which is where the names are supposed to be.
        ///
        /// <para>Test sources are excluded on purpose: a test that asserts a chip reads "Chopping"
        /// is naming the expected value, which is what a test is for.</para>
        /// </summary>
        static IEnumerable<string> Sources()
        {
            foreach (string root in new[] { "Assets/Odyssey/Hud", "Assets/Odyssey/Presentation" })
            {
                string? found = Find(root);
                if (found == null) continue;
                foreach (string file in Directory.GetFiles(found, "*.cs", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(file);
                    if (name == "Registry.g.cs") continue;
                    if (file.Replace('\\', '/').Contains("/Tests/")) continue;
                    yield return file;
                }
            }
        }

        /// <summary>
        /// Walk up for a path relative to the repository root.
        ///
        /// <para>The same trick <c>HudStyleSheetTests</c> uses, and for the same reason: under
        /// Unity the working directory is the project root, and in the fast tier it is a build
        /// output several levels down.</para>
        /// </summary>
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

        static string Short(string file)
        {
            string path = file.Replace('\\', '/');
            int at = path.IndexOf("Assets/", StringComparison.Ordinal);
            return at >= 0 ? path.Substring(at) : path;
        }
    }
}
