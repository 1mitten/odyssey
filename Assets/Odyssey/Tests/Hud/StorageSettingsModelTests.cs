#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The storage pane as a player works it: the rungs, the six categories, the commodities
    /// inside them, the tri-state cycle, the search that turns itself on, and the counts.
    ///
    /// <para><b>Most of the design brief's acceptance list is checkable here</b> and nowhere else —
    /// registry order against alphabetical order, the remembered mixture, the match offsets, the
    /// empty categories that stay pressable, the twenty-row rule. The shell lays the rows out; it
    /// decides none of this.</para>
    ///
    /// <para>The fixture is a hand-made item table rather than the shipped one, because what is
    /// being tested is the behaviour of a filter and not which commodities the game happens to have
    /// this week. It keeps the shape that matters: categories with several members and categories
    /// with none, which is the shipped table's own shape.</para>
    /// </summary>
    public class StorageSettingsModelTests
    {
        const int Food = 0;
        const int Medicine = 1;
        const int Materials = 2;
        const int Weapons = 5;

        // Deliberately out of alphabetical order and out of category order, so a model that simply
        // echoed its input would fail the ordering tests below.
        static readonly string[] Keys =
        {
            "ui.res.wood",        // 0  Materials
            "ui.res.carrots",     // 1  Food
            "ui.res.stone",       // 2  Materials
            "ui.res.rations",     // 3  Food
            "ui.res.ironore",     // 4  Materials
        };

        static readonly int[] Categories = { Materials, Food, Materials, Food, Materials };

        static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "ui.res.wood", "wood" },
            { "ui.res.carrots", "carrots" },
            { "ui.res.stone", "stone" },
            { "ui.res.rations", "ration pack" },
            { "ui.res.ironore", "iron ore" },
            { "ui.res.category.food", "Food" },
            { "ui.res.category.medicine", "Medicine" },
            { "ui.res.category.materials", "Materials" },
            { "ui.res.category.books", "Books" },
            { "ui.res.category.items", "Items" },
            { "ui.res.category.weapons", "Weapons" },
        };

        static string Label(string key) => Labels.TryGetValue(key, out string? v) ? v : key;

        sealed class Store
        {
            public readonly bool[] Accepted;

            public Store(params bool[] accepted) { Accepted = accepted; }
        }

        static StorageSettingsModel Showing(Store store, StorageSettingsModel? reuse = null, int zone = 1)
        {
            var model = reuse ?? new StorageSettingsModel();
            model.Show(cell: 42, zone: zone, hasStore: true, priority: 2, cellCount: 9,
                title: "Stockpile 3", Keys, i => store.Accepted[i], i => Categories[i], Label);
            return model;
        }

        static Store All() => new Store(true, true, true, true, true);

        static Store None() => new Store(false, false, false, false, false);

        /// <summary>Apply what a press produced, the way the shell does, and rebuild.</summary>
        static void Apply(Store store, IReadOnlyList<StorageSettingsModel.Command> commands)
        {
            foreach (StorageSettingsModel.Command c in commands)
            {
                // Braced, and it matters: without them the second `else if` binds to the brace-less
                // `if` inside the category loop rather than to this chain, so the preset branch is
                // unreachable and Clear all silently does nothing. Cost half an hour.
                if (c.A == StorageSettingsModel.ScopeDef)
                {
                    store.Accepted[c.B] = c.C != 0;
                }
                else if (c.A == StorageSettingsModel.ScopeCategory)
                {
                    for (int d = 0; d < Categories.Length; d++)
                        if (Categories[d] == c.B) store.Accepted[d] = c.C != 0;
                }
                else if (c.A == StorageSettingsModel.ScopePreset)
                {
                    for (int p = 0; p < store.Accepted.Length; p++) store.Accepted[p] = c.B == 0;
                }
            }
        }

        static List<StorageSettingsModel.Row> RowsOf(StorageSettingsModel model) =>
            new List<StorageSettingsModel.Row>(model.Rows);

        // ---- order ------------------------------------------------------------------------------

        [Test]
        public void CategoriesAreInRegistryOrderAndNeverAlphabetical()
        {
            var model = Showing(All());
            var rows = RowsOf(model);

            var names = new List<string>();
            foreach (var row in rows)
                if (row.Kind == StorageSettingsModel.RowKind.Category) names.Add(row.Label);

            Assert.That(names, Is.EqualTo(new[] { "Food", "Medicine", "Materials", "Books", "Items", "Weapons" }),
                "the categories came out sorted, or in some other order than the registry's");
        }

        [Test]
        public void CommoditiesAreAlphabeticalInsideTheirCategoryAndCapitalised()
        {
            var model = Showing(All());
            model.ToggleExpanded(Materials);
            Showing(All(), model);

            var names = new List<string>();
            foreach (var row in RowsOf(model))
                if (row.Kind == StorageSettingsModel.RowKind.Commodity) names.Add(row.Label);

            // Iron ore, Stone, Wood — alphabetical, and each with its first letter raised while the
            // registry's own string stays as the wiki wrote it.
            Assert.That(names, Is.EqualTo(new[] { "Iron ore", "Stone", "Wood" }));
        }

        [Test]
        public void OnlyTheExpandedCategoryShowsItsCommodities()
        {
            var model = Showing(All());
            Assert.That(RowsOf(model), Has.Count.EqualTo(6), "nothing is expanded, so six category rows");

            Assert.That(model.ToggleExpanded(Food), Is.True);
            Showing(All(), model);
            Assert.That(RowsOf(model), Has.Count.EqualTo(8), "Food has two commodities");
        }

        // ---- empty categories -------------------------------------------------------------------

        [Test]
        public void AnEmptyCategoryHasNoMembersDoesNotExpandAndIsStillDrawn()
        {
            var model = Showing(All());
            StorageSettingsModel.Row weapons = RowsOf(model)[Weapons];

            Assert.That(weapons.IsEmpty, Is.True);
            Assert.That(weapons.Members, Is.Zero);
            Assert.That(weapons.Expanded, Is.False);
            Assert.That(model.ToggleExpanded(Weapons), Is.False, "an empty category has no caret to press");
            Assert.That(RowsOf(model), Has.Count.EqualTo(6),
                "an empty category vanished — four of six would, and the list would look broken when they arrive");
        }

        [Test]
        public void AnEmptyCategoryReadsAsOffRatherThanOn()
        {
            // A row drawn ticked with nothing behind it tells the player the store takes medicine.
            var model = Showing(All());
            Assert.That(RowsOf(model)[Medicine].State, Is.EqualTo(StorageSettingsModel.CategoryOff));
        }

        // ---- the tri-state cycle ----------------------------------------------------------------

        [Test]
        public void ACategoryReadsAllSomeOrNone()
        {
            Assert.That(RowsOf(Showing(All()))[Food].State, Is.EqualTo(StorageSettingsModel.CategoryOn));
            Assert.That(RowsOf(Showing(None()))[Food].State, Is.EqualTo(StorageSettingsModel.CategoryOff));

            var some = new Store(true, true, true, false, true);   // carrots yes, ration pack no
            StorageSettingsModel.Row row = RowsOf(Showing(some))[Food];
            Assert.That(row.State, Is.EqualTo(StorageSettingsModel.CategoryMixed));
            Assert.That(row.Accepted, Is.EqualTo(1));
            Assert.That(row.Members, Is.EqualTo(2), "the row must be able to say '1 of 2'");
        }

        /// <summary>
        /// The rule the brief is most emphatic about: <b>a mis-click must not destroy a hand-built
        /// selection with no undo</b>. Mixed goes to all, all to none, and none comes back to the
        /// mixture that was there.
        /// </summary>
        [Test]
        public void PressingAMixedCategoryCyclesAllThenNoneThenBackToTheMixture()
        {
            var store = new Store(true, true, true, false, true);   // Food is carrots but not rations
            var model = Showing(store);
            var commands = new List<StorageSettingsModel.Command>();

            Assert.That(RowsOf(model)[Food].State, Is.EqualTo(StorageSettingsModel.CategoryMixed));

            // Mixed -> all
            commands.Clear();
            Assert.That(model.PressCategory(Food, commands), Is.True);
            Apply(store, commands);
            Showing(store, model);
            Assert.That(RowsOf(model)[Food].State, Is.EqualTo(StorageSettingsModel.CategoryOn));
            Assert.That(model.HasRememberedMixture(Food), Is.True, "the mixture was not put by");

            // All -> none
            commands.Clear();
            Assert.That(model.PressCategory(Food, commands), Is.True);
            Apply(store, commands);
            Showing(store, model);
            Assert.That(RowsOf(model)[Food].State, Is.EqualTo(StorageSettingsModel.CategoryOff));

            // None -> the mixture that was there
            commands.Clear();
            Assert.That(model.PressCategory(Food, commands), Is.True);
            Apply(store, commands);
            Showing(store, model);
            Assert.That(RowsOf(model)[Food].State, Is.EqualTo(StorageSettingsModel.CategoryMixed));
            Assert.That(store.Accepted[1], Is.True, "carrots came back");
            Assert.That(store.Accepted[3], Is.False, "and the ration pack stayed out, which is the whole point");
        }

        [Test]
        public void ACategoryWithNoMixtureBehindItSimplyTurnsOn()
        {
            var store = None();
            var model = Showing(store);
            var commands = new List<StorageSettingsModel.Command>();

            Assert.That(model.PressCategory(Food, commands), Is.True);
            Apply(store, commands);
            Showing(store, model);
            Assert.That(RowsOf(model)[Food].State, Is.EqualTo(StorageSettingsModel.CategoryOn));
        }

        [Test]
        public void AnEmptyCategoryCannotBePressedIntoEmittingAnything()
        {
            var model = Showing(All());
            var commands = new List<StorageSettingsModel.Command>();
            Assert.That(model.PressCategory(Weapons, commands), Is.False);
            Assert.That(commands, Is.Empty, "an intent was sent about a category with nothing in it");
        }

        [Test]
        public void SelectingADifferentZoneForgetsTheMixtureRememberedForThisOne()
        {
            var store = new Store(true, true, true, false, true);
            var model = Showing(store);
            var commands = new List<StorageSettingsModel.Command>();
            model.PressCategory(Food, commands);
            Assert.That(model.HasRememberedMixture(Food), Is.True);

            Showing(All(), model, zone: 7);
            Assert.That(model.HasRememberedMixture(Food), Is.False,
                "a remembered selection belongs to the store it was built in");
        }

        // ---- the header buttons -----------------------------------------------------------------

        [Test]
        public void ClearAllIsRefusedWhenNothingIsAccepted()
        {
            var model = Showing(None());
            Assert.That(model.ClearAllEnabled, Is.False, "the button must dim rather than do nothing");
            Assert.That(model.PressClearAll(out _), Is.False);

            Assert.That(Showing(All()).PressClearAll(out _), Is.True);
        }

        [Test]
        public void AllowAllAndClearAllAreThePresetsUnderTheWordsTheyBelongWith()
        {
            var store = All();
            var model = Showing(store);

            Assert.That(model.PressClearAll(out StorageSettingsModel.Command clear), Is.True);
            Apply(store, new[] { clear });
            Showing(store, model);
            Assert.That(model.AcceptedCount, Is.Zero);
            Assert.That(model.NothingAccepted, Is.True);

            Assert.That(model.PressAllowAll(out StorageSettingsModel.Command allow), Is.True);
            Apply(store, new[] { allow });
            Showing(store, model);
            Assert.That(model.AcceptedCount, Is.EqualTo(Keys.Length));
            Assert.That(model.NothingAccepted, Is.False);
        }

        // ---- search -----------------------------------------------------------------------------

        [Test]
        public void TheSearchFieldIsAbsentAtSevenCommoditiesAndPresentAtSixty()
        {
            // The rule, not a toggle: it reads the data, so it turns itself on as commodities land.
            Assert.That(Showing(All()).ShowSearch, Is.False, "five commodities plus six categories is eleven rows");

            var many = new StorageSettingsModel();
            var keys = new List<string>();
            var accepted = new List<bool>();
            for (int i = 0; i < 60; i++) { keys.Add("ui.res.k" + i); accepted.Add(true); }
            many.Show(1, 1, true, 2, 9, "Stockpile 1", keys, i => accepted[i], _ => Materials, k => k);

            Assert.That(many.ShowSearch, Is.True);
            Assert.That(many.ShowFooter, Is.True, "the footer arrives on the same rule");
            Assert.That(many.FooterText, Is.EqualTo("60 of 60 accepted"));
        }

        [Test]
        public void TypingIsIgnoredWhileTheFieldDoesNotExist()
        {
            var model = Showing(All());
            Assert.That(model.SetSearch("wood"), Is.False);
            Assert.That(model.Searching, Is.False);
        }

        [Test]
        public void ASearchFlattensTheListAndMarksTheMatchAtItsRealOffset()
        {
            var model = new StorageSettingsModel();
            var keys = new List<string>(Keys);
            var labels = new Dictionary<string, string>(Labels);
            var accepted = new List<bool>();
            var categories = new List<int>(Categories);
            for (int i = 0; i < Keys.Length; i++) accepted.Add(true);

            // Padded past the threshold so the field exists, without disturbing the five real rows.
            for (int i = 0; i < 20; i++)
            {
                keys.Add("ui.res.pad" + i);
                labels["ui.res.pad" + i] = "padding " + i;
                accepted.Add(true);
                categories.Add(Materials);
            }

            void Rebuild() => model.Show(1, 1, true, 2, 9, "Stockpile 1", keys,
                i => accepted[i], i => categories[i],
                k => labels.TryGetValue(k, out string? v) ? v : k);

            Rebuild();
            Assert.That(model.ShowSearch, Is.True);
            Assert.That(model.SetSearch("o"), Is.True);
            Rebuild();

            Assert.That(model.Searching, Is.True);
            StorageSettingsModel.Row? ironOre = null;
            foreach (var row in model.Rows)
                if (row.Label == "Iron ore") ironOre = row;

            Assert.That(ironOre, Is.Not.Null, "the search did not find a commodity whose name contains an o");
            // "Iron ore" — the first o is at index 2, inside "Iron", which is exactly the point:
            // the offset is real rather than an arbitrary slice of the string.
            Assert.That(ironOre!.Value.MatchStart, Is.EqualTo(2));
            Assert.That(ironOre.Value.MatchLength, Is.EqualTo(1));
            Assert.That(model.MatchCount, Is.GreaterThan(0));
        }

        [Test]
        public void ASearchThatMatchesNothingSaysSoWithTheWordThatWasTyped()
        {
            var model = new StorageSettingsModel();
            var keys = new List<string>();
            var accepted = new List<bool>();
            for (int i = 0; i < 60; i++) { keys.Add("ui.res.k" + i); accepted.Add(false); }

            void Rebuild() => model.Show(1, 1, true, 2, 9, "Stockpile 1", keys,
                i => accepted[i], _ => Materials, k => "thing " + k.Substring("ui.res.k".Length));

            Rebuild();
            model.SetSearch("plasteel");
            Rebuild();

            Assert.That(model.Rows, Is.Empty);
            Assert.That(model.MatchCount, Is.Zero);
            Assert.That(model.NoMatchesLead, Is.EqualTo("No commodity called 'plasteel'."));
            Assert.That(model.NoMatchesHint, Is.EqualTo("Clear the search to see all 60."));
        }

        // ---- the states the pane has to draw ------------------------------------------------------

        [Test]
        public void NothingAcceptedIsALegalStateThePaneWarnsAbout()
        {
            var model = Showing(None());
            Assert.That(model.NothingAccepted, Is.True);
            Assert.That(model.AcceptedCount, Is.Zero);
            Assert.That(StorageSettingsModel.NothingAcceptedLead, Does.Contain("walk past"));
            Assert.That(StorageSettingsModel.NothingAcceptedHint, Does.Contain("Allow all"));
        }

        [Test]
        public void ThePaneShowsNothingWhenNoStoreCoversTheCell()
        {
            var model = new StorageSettingsModel();
            model.Show(7, -1, hasStore: false, 0, 0, string.Empty, Keys,
                _ => true, i => Categories[i], Label);

            Assert.That(model.HasStore, Is.False);
            Assert.That(model.Rows, Is.Empty);
            Assert.That(model.ShowSearch, Is.False);
            Assert.That(model.NothingAccepted, Is.False, "no store is not the same as a store that takes nothing");
        }

        [Test]
        public void TheRungIsReadableWithoutOpeningAnything()
        {
            var model = Showing(All());
            Assert.That(model.Priority, Is.EqualTo(2));
            Assert.That(Registry.Label(StorageSettingsModel.PriorityKeys[model.Priority]), Is.EqualTo("Normal"));
            Assert.That(model.Title, Is.EqualTo("Stockpile 3"), "an unnamed zone is never blank");
        }

        [Test]
        public void PressingTheRungAlreadyHeldSendsNothing()
        {
            var model = Showing(All());
            Assert.That(model.PressPriority(4, out _), Is.True);
            Assert.That(model.PressPriority(2, out _), Is.False);
            Assert.That(model.PressPriority(9, out _), Is.False);
        }

        // ---- content ----------------------------------------------------------------------------

        [Test]
        public void ThereAreFiveRungsAndSixCategoriesAndEveryKeyIsOneTheRegistryKnows()
        {
            Assert.That(StorageSettingsModel.PriorityKeys, Has.Length.EqualTo(5));
            Assert.That(StorageSettingsModel.CategoryKeys, Has.Length.EqualTo(6));

            foreach (string key in StorageSettingsModel.PriorityKeys)
                Assert.That(Registry.Label(key), Is.Not.Empty, key);
            foreach (string key in StorageSettingsModel.CategoryKeys)
                Assert.That(Registry.Label(key), Is.Not.Empty, key);
            foreach (string key in StorageSettingsModel.PresetKeys)
                Assert.That(Registry.Label(key), Is.Not.Empty, key);
        }

        [Test]
        public void CapitalisingRaisesTheFirstLetterAndLeavesTheRestAlone()
        {
            Assert.That(StorageSettingsModel.Capitalise("iron ore"), Is.EqualTo("Iron ore"));
            Assert.That(StorageSettingsModel.Capitalise("Food"), Is.EqualTo("Food"));
            Assert.That(StorageSettingsModel.Capitalise(string.Empty), Is.Empty);
        }
    }
}
