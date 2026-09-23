#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The Inventory tab's content (design 35): what the stores hold, by category, and for one
    /// item, which stores hold it. Read off a hand-written frame, in the fast tier.
    /// </summary>
    public class InventoryTabTests
    {
        const int Meal = 0, Wood = 2, Stone = 3, Carrots = 6;

        // Food 0, Materials 2, as the storage pane numbers its categories.
        static int CategoryOf(int def) => def == Meal || def == Carrots ? 0 : 2;

        static readonly GridSize Size = new GridSize(10, 10, 4);

        static int At(int x, int z, int y = 1) => Size.Index(x, z, y);

        /// <summary>
        /// Stockpile 1 over (1,1)-(2,1), Shelf 2 at (5,5), Stockpile 3 at (8,8), and a loose pile of
        /// stone outside every store that the tab must not count.
        /// </summary>
        static WorldSnapshot Colony()
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddStore(new StoreView(At(1, 1), zone: 0, priority: 2, ordinal: 1));
            frame.AddStore(new StoreView(At(2, 1), zone: 0, priority: 2, ordinal: 1));
            frame.AddStore(new StoreView(At(8, 8), zone: 1, priority: 2, ordinal: 3));
            frame.AddStorageUnit(new StorageUnitView(At(5, 5), stacks: 2, slots: 8, emptying: false, ordinal: 2));

            int id = 1;
            void Thing(int def, int stack, int cell, int container = 0) =>
                frame.AddThing(new ThingView(new ThingId(id++), Size.FromIndex(cell), def, 0, stack, container));

            Thing(Wood, 30, At(1, 1));
            Thing(Wood, 45, At(2, 1));             // the larger stack: Go lands here
            Thing(Wood, 25, At(5, 5), container: 7);
            Thing(Meal, 12, At(5, 5), container: 7);
            Thing(Meal, 3, At(8, 8));
            Thing(Stone, 50, At(4, 9));            // loose: not stored, not counted
            return frame;
        }

        static InventoryModel Read()
        {
            var model = new InventoryModel();
            model.Refresh(Colony(), CategoryOf);
            model.SelectLargest();
            return model;
        }

        [Test]
        public void OnlyWhatIsInAStoreIsCounted()
        {
            InventoryModel model = Read();
            Assert.That(model.Total, Is.EqualTo(115), "the loose stone is not in the total");
            Assert.That(model.Items.Select(i => i.DefIndex), Is.EquivalentTo(new[] { Wood, Meal }));
        }

        [Test]
        public void AnItemSaysWhichStoresHoldItMostFirstByTheirOwnNames()
        {
            InventoryItem wood = Read().Items.Single(i => i.DefIndex == Wood);
            Assert.That(wood.Total, Is.EqualTo(100));
            Assert.That(wood.Places.Select(p => p.Name), Is.EqualTo(new[] { "Stockpile 1", "Shelf 2" }));
            Assert.That(wood.Places.Select(p => p.Quantity), Is.EqualTo(new[] { 75, 25 }));
            Assert.That(wood.Places[0].Cell, Is.EqualTo(Size.FromIndex(At(2, 1))),
                "Go lands on the store's largest stack of the item, not its corner");
            Assert.That(InventoryModel.Meta(wood), Is.EqualTo("Materials, in 2 places"));
        }

        [Test]
        public void OneStoreIsOnePlace()
        {
            var model = new InventoryModel();
            WorldSnapshot frame = Frame.Write();
            frame.AddStore(new StoreView(At(1, 1), 0, 2, ordinal: 1));
            frame.AddThing(new ThingView(new ThingId(1), Size.FromIndex(At(1, 1)), Meal, 0, 4));
            model.Refresh(frame, CategoryOf);
            Assert.That(InventoryModel.Meta(model.Items[0]), Is.EqualTo("Food, in 1 place"));
        }

        [Test]
        public void ItOpensOnTheLargestItemAtItsLargestStore()
        {
            InventoryModel model = Read();
            Assert.That(model.SelectedDef, Is.EqualTo(Wood));
            Assert.That(model.SelectedPlace, Is.EqualTo(1));
            Assert.That(InventoryModel.GoTo(model.SelectedPlaceOf!), Is.EqualTo("Go to Stockpile 1"));
        }

        [Test]
        public void EverySixCategoriesShowInOrderAndAnEmptyOneHasNoCount()
        {
            InventoryModel model = Read();
            var groups = model.Rows.Where(r => r.IsGroup).ToList();
            Assert.That(groups.Select(g => g.Category), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
            Assert.That(groups.Select(g => g.Kinds), Is.EqualTo(new[] { 1, 0, 1, 0, 0, 0 }));
            Assert.That(model.Rows.Count, Is.EqualTo(8), "six headings and two items");
        }

        [Test]
        public void ASecondClickOnTheSelectedRowIsAGoToItsLargestStore()
        {
            InventoryModel model = Read();
            Assert.That(model.PressItem(Meal), Is.Null, "the first click selects");
            Assert.That(model.SelectedDef, Is.EqualTo(Meal));
            Assert.That(model.SelectedPlace, Is.EqualTo(2), "Shelf 2 holds twelve of the fifteen");
            InventoryPlace? go = model.PressItem(Meal);
            Assert.That(go, Is.Not.Null);
            Assert.That(go!.Ordinal, Is.EqualTo(2));
            Assert.That(go.Shelf, Is.True);
        }

        [Test]
        public void ASearchFiltersLiveAndHidesEmptyCategories()
        {
            InventoryModel model = Read();
            model.SetSearch("mea");
            Assert.That(model.Rows.Count(r => r.IsGroup), Is.EqualTo(1));
            Assert.That(model.Rows.Where(r => !r.IsGroup).Select(r => r.Item!.DefIndex), Is.EqualTo(new[] { Meal }));
            Assert.That(model.SelectedDef, Is.EqualTo(Meal), "the selection follows the search to what it shows");

            model.SetSearch("zzz");
            Assert.That(model.Rows, Is.Empty);
            Assert.That(model.SelectedDef, Is.EqualTo(-1));
        }

        [Test]
        public void ExactlyOneColumnIsSortedAndTotalIsTheDefault()
        {
            var model = new InventoryModel();
            Assert.That(model.Sort, Is.EqualTo(InventorySort.Total));
            Assert.That(model.SortBy(InventorySort.Total), Is.False);
            Assert.That(model.SortBy(InventorySort.Item), Is.True);
            Assert.That(model.Sort, Is.EqualTo(InventorySort.Item));
        }

        [Test]
        public void ItemsSortWithinTheirCategoryAndHeadingsNeverMove()
        {
            var model = new InventoryModel();
            WorldSnapshot frame = Frame.Write();
            frame.AddStore(new StoreView(At(1, 1), 0, 2, ordinal: 1));
            frame.AddStore(new StoreView(At(2, 1), 0, 2, ordinal: 1));
            frame.AddThing(new ThingView(new ThingId(1), Size.FromIndex(At(1, 1)), Stone, 0, 5));
            frame.AddThing(new ThingView(new ThingId(2), Size.FromIndex(At(2, 1)), Wood, 0, 9));
            model.Refresh(frame, CategoryOf);

            int[] Materials() => model.Rows.Where(r => !r.IsGroup).Select(r => r.Item!.DefIndex).ToArray();
            Assert.That(Materials(), Is.EqualTo(new[] { Wood, Stone }), "most first");
            model.SortBy(InventorySort.Item);
            Assert.That(Materials(), Is.EqualTo(new[] { Stone, Wood }), "A first");
            Assert.That(model.Rows.Where(r => r.IsGroup).Select(r => r.Category), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
        }

        [Test]
        public void ALongListPagesAndACategoryContinuingRepeatsItsHeading()
        {
            // Twenty stores of one item each would be one item; the pages need many kinds, so this
            // uses every item def the registry knows, twice over under two categories' worth.
            var model = new InventoryModel();
            WorldSnapshot frame = Frame.Write();
            int id = 1;
            for (int def = 0; def < ItemLabels.Keys.Length; def++)
            {
                frame.AddStore(new StoreView(At(def, 2), 0, 2, ordinal: 1));
                frame.AddThing(new ThingView(new ThingId(id++), Size.FromIndex(At(def, 2)), def, 0, 10 + def));
            }
            // Every item in Materials, so the one category runs past the first page.
            model.Refresh(frame, _ => 2);
            Assume.That(6 + ItemLabels.Keys.Length, Is.GreaterThan(InventoryLayout.RowsPerPage),
                "the fixture no longer overflows a page");

            Assert.That(model.PageCount, Is.EqualTo(2));
            Assert.That(model.Rows.Count, Is.EqualTo(InventoryLayout.RowsPerPagedPage),
                "a paged table gives one row to its foot");
            model.SetPage(1);
            Assert.That(model.Rows[0].IsGroup, Is.True);
            Assert.That(model.Rows[0].Category, Is.EqualTo(2), "Materials continues, so its heading repeats");
        }

        [Test]
        public void TheSameFrameTwiceIsNoRebuild()
        {
            var model = new InventoryModel();
            WorldSnapshot frame = Colony();
            Assert.That(model.Refresh(frame, CategoryOf), Is.True);
            Assert.That(model.Refresh(frame, CategoryOf), Is.False);
        }

        [Test]
        public void TheHeaderFigureHasThousandsSeparators()
        {
            Assert.That(InventoryModel.Figure(1026), Is.EqualTo("1,026"));
            Assert.That(InventoryModel.Figure(926), Is.EqualTo("926"));
        }

        [Test]
        public void TheWindowIsOneHeightAndItsRowsFitTheBody()
        {
            Assert.That(InventoryLayout.Height, Is.EqualTo(34 + 38 + 420 + 2));
            Assert.That(InventoryLayout.RowsPerPage, Is.EqualTo(13));
            Assert.That(InventoryLayout.RowHeight * (InventoryLayout.RowsPerPage + 1), Is.LessThanOrEqualTo(InventoryLayout.BodyHeight));
            Assert.That(InventoryLayout.DetailWidth, Is.EqualTo(458));
        }

        [Test]
        public void EveryWordTheTabDrawsIsRegisteredAndAscii()
        {
            foreach (string key in InventoryDirector.IconKeys)
            {
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
                Assert.That(Registry.Label(key).All(c => c < 128), Is.True, $"{key} is not ASCII");
            }
        }

        [Test]
        public void GoSelectsTheStoresCellOnItsLayerAndNotThePileInIt()
        {
            var directors = new HudDirectors(4, 1);
            var at = new CellRef(2, 1, 3);
            directors.ChooseStore(at);
            Assert.That(directors.Slice.ActiveLayer, Is.EqualTo(3));
            Assert.That(directors.Selection.Cell, Is.EqualTo(at));
            Assert.That(directors.Selection.HasThing, Is.False, "the pane leads with the store only when the cell is the subject");
            Assert.That(directors.Camera.JumpTarget, Is.EqualTo(at));
        }

        [Test]
        public void EscapeClosesTheTabsAtTheWorkTabsRung()
        {
            var settings = new SettingsDirector();
            Assert.That(settings.Escape(false, false, false, false, false, inventoryOpen: true, researchOpen: false, null),
                Is.EqualTo(EscapeAction.CloseInventory));
            Assert.That(settings.Escape(false, false, false, false, false, inventoryOpen: false, researchOpen: true, null),
                Is.EqualTo(EscapeAction.CloseResearch));
            Assert.That(settings.Escape(false, false, false, true, false, inventoryOpen: true, researchOpen: true, null),
                Is.EqualTo(EscapeAction.CloseWork));
        }

        [Test]
        public void TheTwoTabsShipOnF2AndF3()
        {
            var hotkeys = new HotkeyDirector();
            Assert.That(hotkeys.Key(HotkeyAction.InventoryTab, 0), Is.EqualTo(HudKey.F2));
            Assert.That(hotkeys.Key(HotkeyAction.ResearchTab, 0), Is.EqualTo(HudKey.F3));

            string[] bar = HudCommands.All.Select(c => c.Key).ToArray();
            Assert.That(System.Array.IndexOf(bar, HudCommands.InventoryKey),
                Is.EqualTo(System.Array.IndexOf(bar, HudCommands.WorkKey) + 1), "Inventory sits after Work");
            Assert.That(System.Array.IndexOf(bar, HudCommands.ResearchKey),
                Is.EqualTo(System.Array.IndexOf(bar, HudCommands.InventoryKey) + 1), "and before Research");
            Assert.That(HudCommands.All.Where(c => c.Key == HudCommands.InventoryKey || c.Key == HudCommands.ResearchKey)
                .All(c => c.Live), Is.True);
        }
    }
}
