#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The storage filter as a player works it: the rungs, the two presets, the six category rows
    /// and one row per commodity — and what each press means.
    ///
    /// <para>The fixture is a hand-made item table rather than the shipped one, because what is
    /// being tested is the arithmetic of a filter and not which commodities the game happens to
    /// have this week. It keeps the shape that matters: some categories with several members, and
    /// some with none at all, which is the shipped table's own shape and the reason the tri-state
    /// tree is deferred.</para>
    /// </summary>
    public class StorageSettingsModelTests
    {
        const int Food = 0;
        const int Medicine = 1;
        const int Materials = 2;

        static readonly string[] Keys = { "ui.res.rations", "ui.res.carrots", "ui.res.wood", "ui.res.stone" };
        static readonly int[] Categories = { Food, Food, Materials, Materials };

        static StorageSettingsModel Showing(params bool[] accepted)
        {
            var model = new StorageSettingsModel();
            var state = new List<bool>(accepted);
            model.Show(cell: 42, hasStore: true, priority: 2, cellCount: 9, Keys,
                accepts: i => state[i], categoryOf: i => Categories[i]);
            return model;
        }

        [Test]
        public void ThePanelDoesNotOpenOverACellWithNoStore()
        {
            var model = new StorageSettingsModel();
            model.Show(cell: 7, hasStore: false, priority: 0, cellCount: 0, Keys,
                accepts: _ => true, categoryOf: i => Categories[i]);

            Assert.That(model.HasStore, Is.False);
            Assert.That(model.Defs, Is.Empty, "rows were built for a store that is not there");
            Assert.That(model.Categories, Is.Empty);
            Assert.That(model.Title, Is.Empty);
        }

        [Test]
        public void EveryCommodityGetsARowAndEveryCategoryGetsOneWhetherOrNotItHasMembers()
        {
            var model = Showing(true, true, true, true);

            Assert.That(model.Defs, Has.Count.EqualTo(Keys.Length));
            Assert.That(model.Categories, Has.Count.EqualTo(StorageSettingsModel.CategoryKeys.Length),
                "a category with nothing in it is still drawn, so the six read as a fixed list");

            Assert.That(model.Categories[Food].IsEmpty, Is.False);
            Assert.That(model.Categories[Medicine].IsEmpty, Is.True,
                "nothing is medicine yet, and the row has to say so rather than look ticked");
        }

        [Test]
        public void ACategoryRowSaysAllSomeOrNone()
        {
            Assert.That(Showing(true, true, true, true).Categories[Food].State,
                Is.EqualTo(StorageSettingsModel.CategoryOn));
            Assert.That(Showing(true, false, true, true).Categories[Food].State,
                Is.EqualTo(StorageSettingsModel.CategoryMixed));
            Assert.That(Showing(false, false, true, true).Categories[Food].State,
                Is.EqualTo(StorageSettingsModel.CategoryOff));

            // The one that matters: an empty category is off, never on. A row drawn ticked with
            // nothing behind it tells the player the store takes medicine.
            Assert.That(Showing(true, true, true, true).Categories[Medicine].State,
                Is.EqualTo(StorageSettingsModel.CategoryOff));
        }

        [Test]
        public void PressingACategoryTurnsItOnUnlessItIsAlreadyWhollyOn()
        {
            // Mixed goes to on, which is what a hand expects: a half-ticked row is a row you are
            // in the middle of turning on.
            Assert.That(Showing(true, false, true, true).PressCategory(Food, out var mixed), Is.True);
            Assert.That(mixed.C, Is.EqualTo(1), "a half-ticked category should fill, not empty");

            Assert.That(Showing(true, true, true, true).PressCategory(Food, out var full), Is.True);
            Assert.That(full.C, Is.Zero, "a full category empties");

            Assert.That(Showing(false, false, true, true).PressCategory(Food, out var empty), Is.True);
            Assert.That(empty.C, Is.EqualTo(1));

            foreach (var command in new[] { mixed, full, empty })
            {
                Assert.That(command.A, Is.EqualTo(StorageSettingsModel.ScopeCategory));
                Assert.That(command.B, Is.EqualTo(Food));
            }
        }

        [Test]
        public void ACategoryWithNoMembersCannotBePressed()
        {
            // There is nothing to turn on, so the press is refused rather than sent as an intent
            // the simulation would answer with a shrug.
            Assert.That(Showing(true, true, true, true).PressCategory(Medicine, out _), Is.False);
        }

        [Test]
        public void PressingACommodityFlipsThatOneRow()
        {
            var model = Showing(true, false, true, true);

            Assert.That(model.PressDef(0, out var off), Is.True);
            Assert.That(off.A, Is.EqualTo(StorageSettingsModel.ScopeDef));
            Assert.That(off.B, Is.Zero);
            Assert.That(off.C, Is.Zero, "an accepted commodity turns off");

            Assert.That(model.PressDef(1, out var on), Is.True);
            Assert.That(on.C, Is.EqualTo(1), "a refused commodity turns on");

            Assert.That(model.PressDef(99, out _), Is.False, "a row that is not there");
        }

        [Test]
        public void PressingARungSendsItAndPressingTheRungAlreadyHeldSendsNothing()
        {
            var model = Showing(true, true, true, true);

            Assert.That(model.PressPriority(4, out var urgent), Is.True);
            Assert.That(urgent.A, Is.EqualTo(4));

            Assert.That(model.PressPriority(2, out _), Is.False, "the store is already at Normal");
            Assert.That(model.PressPriority(9, out _), Is.False, "a rung that does not exist");
        }

        [Test]
        public void ThereAreFiveRungsTwoPresetsAndSixCategories()
        {
            // The three lists are content — they are registry keys the wiki prints — and their
            // lengths are the decisions. A seventh category or a third preset is a content commit,
            // not an edit here.
            Assert.That(StorageSettingsModel.PriorityKeys, Has.Length.EqualTo(5));
            Assert.That(StorageSettingsModel.PresetKeys, Has.Length.EqualTo(2));
            Assert.That(StorageSettingsModel.CategoryKeys, Has.Length.EqualTo(6));
        }

        [Test]
        public void EveryKeyTheControlDrawsIsANameTheRegistryKnows()
        {
            // The rule this project enforces both ways: a name the screen shows comes from the
            // registry, and the registry is the wiki. A key with no entry draws an empty label.
            foreach (string key in StorageSettingsModel.PriorityKeys)
                Assert.That(Registry.Label(key), Is.Not.Empty, key);
            foreach (string key in StorageSettingsModel.PresetKeys)
                Assert.That(Registry.Label(key), Is.Not.Empty, key);
            foreach (string key in StorageSettingsModel.CategoryKeys)
                Assert.That(Registry.Label(key), Is.Not.Empty, key);
        }

        [Test]
        public void AZoneWithNoNameIsTitledByItsRungAndItsSize()
        {
            var model = Showing(true, true, true, true);
            Assert.That(model.Title, Does.Contain(Registry.Label("ui.storage.priority.normal")));
            Assert.That(model.Title, Does.Contain("9"));
        }
    }
}
