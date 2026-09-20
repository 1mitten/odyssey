#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Storage;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The player's storage zones: where one may go, how a drag becomes one zone, and — the rule
    /// this unit exists for — what happens when a drag meets a zone that is already there.
    ///
    /// <para><b>The anchor decides.</b> A growing zone folds any touching zone of the same plant
    /// into itself, which loses nothing because a growing zone is identified by its crop. A
    /// storage zone carries a filter and a priority, so a fold would destroy one of two
    /// configurations without saying so. These tests are the rule stated where it can be broken.</para>
    /// </summary>
    public class StorageZoneTests
    {
        const int Ground = 0;
        const int Layer = 1;
        static readonly GridSize Size = new GridSize(10, 10, 3);

        sealed class Fixture
        {
            public readonly CellGrid Grid;
            public readonly ColonyItems Items;
            public readonly StorageSettingsTable Settings;
            public readonly StorageZones Zones;

            public Fixture()
            {
                Grid = new CellGrid(Size);
                for (int z = 0; z < Size.SizeZ; z++)
                for (int x = 0; x < Size.SizeX; x++)
                {
                    int ground = Size.Index(x, z, Ground);
                    Grid.Terrain[ground] = NaturalContent.TerrainGrass;
                    Grid.Flags[ground] |= CellFlags.SolidTerrain;
                }

                PawnContent content = ContentPack.Pawns();
                Items = new ColonyItems(content);
                Settings = new StorageSettingsTable(content);
                Zones = new StorageZones(Grid, Settings, Items);
                Items.Membership = Zones;
            }

            public int Cell(int x, int z) => Size.Index(x, z, Layer);

            public CellRef At(int x, int z) => new CellRef(x, z, Layer);

            /// <summary>A drag from one cell to another along a row, anchored at the first.</summary>
            public void Drag(int x0, int x1, int z, int preset = StoragePreset.Everything)
            {
                int anchor = Cell(x0, z);
                for (int x = x0; x <= x1; x++) Zones.Designate(At(x, z), anchor, preset);
            }
        }

        // ---- where a zone may go ----------------------------------------------------------------

        [Test]
        public void AZoneGoesAnywhereAThingCanBePutDownAndNowhereElse()
        {
            var fix = new Fixture();

            Assert.That(fix.Zones.Designate(fix.At(2, 2), fix.Cell(2, 2), StoragePreset.Everything),
                Is.EqualTo(IntentRejection.None), "open ground takes a store");

            // Water: a stack in a stream is not stored, and wadeable water is walkable — which is
            // exactly why it is asked apart rather than left to IsWalkable.
            fix.Grid.Terrain[fix.Cell(1, 1)] = NaturalContent.TerrainShallowWater;
            Assert.That(fix.Zones.Designate(fix.At(1, 1), fix.Cell(1, 1), StoragePreset.Everything),
                Is.EqualTo(IntentRejection.NotPermitted), "a store is not a stream");

            // Something standing in the cell. On the wooded board this is a tree, and it is the
            // case that moved the played golden: the old code asked nothing of a cell at all.
            fix.Grid.Edifice[fix.Cell(3, 3)] = 0;
            Assert.That(fix.Zones.Designate(fix.At(3, 3), fix.Cell(3, 3), StoragePreset.Everything),
                Is.EqualTo(IntentRejection.NotPermitted), "nothing is stored where something stands");

            Assert.That(fix.Zones.Designate(new CellRef(99, 99, Layer), 0, StoragePreset.Everything),
                Is.EqualTo(IntentRejection.OutOfBounds));
            Assert.That(fix.Zones.Designate(fix.At(2, 2), fix.Cell(2, 2), StoragePreset.Everything),
                Is.EqualTo(IntentRejection.AlreadyInThatState), "the same cell twice in the same zone");
        }

        // ---- the anchor rule --------------------------------------------------------------------

        [Test]
        public void OneDragIsOneZoneHoweverManyCellsItCovers()
        {
            var fix = new Fixture();
            fix.Drag(1, 5, 2);

            Assert.That(fix.Zones.ZoneCount, Is.EqualTo(1));
            Assert.That(fix.Zones.Cells, Has.Count.EqualTo(5));
            for (int x = 1; x <= 5; x++)
                Assert.That(fix.Zones.ZoneAt(fix.Cell(x, 2)), Is.EqualTo(0));
        }

        [Test]
        public void TwoDragsThatTouchStayTwoZones()
        {
            // The whole rule. On the growing zone's substrate these would fold into one, and one
            // of the two filters would be gone with no way to get it back.
            var fix = new Fixture();
            fix.Drag(1, 3, 2);
            fix.Drag(4, 6, 2);

            Assert.That(fix.Zones.ZoneCount, Is.EqualTo(2), "two touching stores are two stores");
            Assert.That(fix.Zones.ZoneAt(fix.Cell(3, 2)), Is.Not.EqualTo(fix.Zones.ZoneAt(fix.Cell(4, 2))));
        }

        [Test]
        public void ADragBegunInsideAZoneExtendsThatZoneAndKeepsItsSettings()
        {
            var fix = new Fixture();
            fix.Drag(1, 3, 2);
            StorageSettings settings = fix.Zones.SettingsAt(fix.Cell(1, 2))!;
            settings.Priority = StoragePriority.Preferred;
            settings.ApplyPreset(StoragePreset.Nothing);
            settings.SetDef(ItemIndex.Wood, true);

            // Anchored on a cell of the existing zone, running out past its edge.
            int anchor = fix.Cell(3, 2);
            for (int x = 3; x <= 6; x++) fix.Zones.Designate(fix.At(x, 2), anchor, StoragePreset.Everything);

            Assert.That(fix.Zones.ZoneCount, Is.EqualTo(1), "the drag founded a second zone instead of extending");
            Assert.That(fix.Zones.Cells, Has.Count.EqualTo(6));
            Assert.That(fix.Zones.SettingsAt(fix.Cell(6, 2))!.Priority, Is.EqualTo(StoragePriority.Preferred),
                "the new cells joined at the tool's default instead of the zone's own settings");
            Assert.That(fix.Zones.Accepts(fix.Cell(6, 2), ItemIndex.Meal), Is.False,
                "extending a narrowed zone quietly re-opened it");
        }

        [Test]
        public void ADragOverAnotherZoneTakesItsCellsRatherThanSharingThem()
        {
            var fix = new Fixture();
            fix.Drag(1, 3, 2);           // zone A
            fix.Drag(5, 7, 2);           // zone B, not touching

            // A drag anchored in B, running back across A.
            int anchor = fix.Cell(5, 2);
            for (int x = 5; x >= 2; x--) fix.Zones.Designate(fix.At(x, 2), anchor, StoragePreset.Everything);

            int b = fix.Zones.ZoneAt(fix.Cell(5, 2));
            Assert.That(fix.Zones.ZoneAt(fix.Cell(2, 2)), Is.EqualTo(b), "the crossed cells did not transfer");
            Assert.That(fix.Zones.ZoneAt(fix.Cell(3, 2)), Is.EqualTo(b));
            Assert.That(fix.Zones.ZoneAt(fix.Cell(1, 2)), Is.Not.EqualTo(b), "A kept the cell the drag never reached");
            Assert.That(fix.Zones.ZoneCount, Is.EqualTo(2), "a transfer is not a merge");

            // And no cell is in two zones: the count of zoned cells is the sum of the two.
            int total = 0;
            for (int slot = 0; slot < fix.Zones.ZoneCount; slot++) total += fix.Zones.CellsOf(slot).Count;
            Assert.That(total, Is.EqualTo(fix.Zones.Cells.Count), "a cell is in two zones at once");
        }

        [Test]
        public void AZoneSwallowedWholeByAnotherDragIsDeleted()
        {
            var fix = new Fixture();
            fix.Drag(4, 5, 2);           // the small zone
            fix.Drag(1, 8, 2);           // a drag begun outside it, running right over it

            Assert.That(fix.Zones.ZoneCount, Is.EqualTo(1), "the swallowed zone was left behind empty");
            Assert.That(fix.Zones.Cells, Has.Count.EqualTo(8));
        }

        [Test]
        public void ACancelTakesACellBackAndDeletesAZoneReducedToNothing()
        {
            var fix = new Fixture();
            fix.Drag(1, 2, 2);

            Assert.That(fix.Zones.Cancel(fix.At(1, 2)), Is.EqualTo(IntentRejection.None));
            Assert.That(fix.Zones.ZoneCount, Is.EqualTo(1), "one cell left, so the zone stands");

            Assert.That(fix.Zones.Cancel(fix.At(2, 2)), Is.EqualTo(IntentRejection.None));
            Assert.That(fix.Zones.ZoneCount, Is.Zero, "a zone reduced to nothing is deleted");
            Assert.That(fix.Zones.Cancel(fix.At(2, 2)), Is.EqualTo(IntentRejection.AlreadyInThatState));
        }

        // ---- the lister, which is the half that never existed -----------------------------------

        [Test]
        public void AThingBecomesStoredWhenAZoneArrivesOverItAndLooseWhenTheZoneLeaves()
        {
            var fix = new Fixture();
            int cell = fix.Cell(4, 4);
            fix.Items.Spawn(ItemIndex.Wood, cell, 10);
            Assert.That(fix.Items.LooseItems, Has.Count.EqualTo(1));
            Assert.That(fix.Items.StoredItems, Is.Empty);

            fix.Zones.Designate(fix.At(4, 4), cell, StoragePreset.Everything);
            Assert.That(fix.Items.StoredItems, Has.Count.EqualTo(1), "a zone painted over a pile leaves it loose");
            Assert.That(fix.Items.LooseItems, Is.Empty);

            // The half `AddStockpile` never had: nothing could take a cell out of a zone, so a
            // stored thing could never become loose again.
            fix.Zones.Cancel(fix.At(4, 4));
            Assert.That(fix.Items.LooseItems, Has.Count.EqualTo(1), "the cell left the zone and the thing stayed stored");
            Assert.That(fix.Items.StoredItems, Is.Empty);
        }

        // ---- the filter ------------------------------------------------------------------------

        [Test]
        public void APresetIsAStatementAboutKindsAndCarriesTheUnknownDefWithIt()
        {
            var fix = new Fixture();
            int id = fix.Settings.Create(StoragePreset.Everything);
            StorageSettings settings = fix.Settings[id];

            Assert.That(settings.Accepts(ItemIndex.Wood), Is.True);
            Assert.That(settings.Accepts(settings.Allow.Length + 5), Is.True,
                "Everything means a commodity added tomorrow too");

            settings.ApplyPreset(StoragePreset.Nothing);
            Assert.That(settings.Accepts(ItemIndex.Wood), Is.False);
            Assert.That(settings.Accepts(settings.Allow.Length + 5), Is.False);

            // A per-def tick is not a statement about kinds, so it leaves the unknown answer alone.
            settings.SetDef(ItemIndex.Wood, true);
            Assert.That(settings.Accepts(ItemIndex.Wood), Is.True);
            Assert.That(settings.Accepts(settings.Allow.Length + 5), Is.False);
        }

        [Test]
        public void ACategoryRowTicksEveryDefOfThatKindAndRollsUpToThree()
        {
            var fix = new Fixture();
            PawnContent content = fix.Items.Content;
            int id = fix.Settings.Create(StoragePreset.Nothing);
            StorageSettings settings = fix.Settings[id];

            Assert.That(settings.CategoryState(ItemCategory.Food, content), Is.EqualTo(StorageSettings.CategoryOff));

            settings.SetCategory(ItemCategory.Food, true, content);
            Assert.That(settings.Accepts(ItemIndex.Meal), Is.True);
            Assert.That(settings.Accepts(ItemIndex.Carrots), Is.True);
            Assert.That(settings.Accepts(ItemIndex.Wood), Is.False, "Food ticked a Material");
            Assert.That(settings.CategoryState(ItemCategory.Food, content), Is.EqualTo(StorageSettings.CategoryOn));

            settings.SetDef(ItemIndex.Carrots, false);
            Assert.That(settings.CategoryState(ItemCategory.Food, content), Is.EqualTo(StorageSettings.CategoryMixed));

            // Four of the six categories have no members yet, and an empty branch reads as off
            // rather than as on — which is the whole argument for deferring the tri-state tree.
            Assert.That(settings.CategoryState(ItemCategory.Weapons, content), Is.EqualTo(StorageSettings.CategoryOff));
            settings.SetCategory(ItemCategory.Weapons, true, content);
            Assert.That(settings.CategoryState(ItemCategory.Weapons, content), Is.EqualTo(StorageSettings.CategoryOff),
                "a category with no members cannot be turned on, because there is nothing to turn on");
        }

        [Test]
        public void EveryItemDefDeclaresACategoryTheRegistryKnows()
        {
            PawnContent content = ContentPack.Pawns();
            for (int i = 0; i < content.Items.Length; i++)
                Assert.That((int)content.Items[i].category, Is.InRange(0, ItemCategories.Count - 1),
                    $"{content.Items[i].defName} is filed under a category that does not exist");

            // The two that matter to a hungry colonist, pinned by name rather than by number.
            Assert.That(content.Items[ItemIndex.Meal].category, Is.EqualTo(ItemCategory.Food));
            Assert.That(content.Items[ItemIndex.Carrots].category, Is.EqualTo(ItemCategory.Food));
            Assert.That(content.Items[ItemIndex.Salvage].category, Is.EqualTo(ItemCategory.Materials),
                "salvage is reclaimer feedstock, not a made thing (decision 25)");
        }

        // ---- the intent seam --------------------------------------------------------------------

        [Test]
        public void ThePriorityAndFilterIntentsNameACellAndNotAZone()
        {
            var fix = new Fixture();
            fix.Drag(1, 3, 2);
            CellRef any = fix.At(2, 2);

            Assert.That(fix.Zones.HandleSetPriority(new Intent(IntentKind.SetStoragePriority, any, StoragePriority.Urgent)),
                Is.EqualTo(IntentRejection.None));
            Assert.That(fix.Zones.SettingsAt(fix.Cell(1, 2))!.Priority, Is.EqualTo(StoragePriority.Urgent),
                "the priority landed on one cell instead of on the zone");

            Assert.That(fix.Zones.HandleSetPriority(new Intent(IntentKind.SetStoragePriority, any, 99)),
                Is.EqualTo(IntentRejection.NotPermitted), "a rung that does not exist");
            Assert.That(fix.Zones.HandleSetPriority(new Intent(IntentKind.SetStoragePriority, fix.At(9, 9), 1)),
                Is.EqualTo(IntentRejection.NotPermitted), "no zone under that cell");

            Assert.That(fix.Zones.HandleSetFilter(new Intent(IntentKind.SetStorageFilter, any,
                    StorageZones.FilterScopePreset, StoragePreset.Nothing, 0)),
                Is.EqualTo(IntentRejection.None));
            Assert.That(fix.Zones.Accepts(fix.Cell(3, 2), ItemIndex.Wood), Is.False);

            Assert.That(fix.Zones.HandleSetFilter(new Intent(IntentKind.SetStorageFilter, any,
                    StorageZones.FilterScopeCategory, (int)ItemCategory.Food, 1)),
                Is.EqualTo(IntentRejection.None));
            Assert.That(fix.Zones.Accepts(fix.Cell(3, 2), ItemIndex.Meal), Is.True);
            Assert.That(fix.Zones.Accepts(fix.Cell(3, 2), ItemIndex.Wood), Is.False);
        }

        [Test]
        public void EveryStorageIntentAppliesWhilePaused()
        {
            // A filter is a thing you change with a panel open, and a panel is a thing you open
            // while paused. A tick-boundary filter would leave the popover reading one thing and
            // the world doing another until the clock was started.
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.DesignateStorage), Is.True);
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.CancelStorage), Is.True);
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.SetStoragePriority), Is.True);
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.SetStorageFilter), Is.True);
        }

        // ---- saving ----------------------------------------------------------------------------

        [Test]
        public void ZonesAndTheirSettingsComeBackFromASave()
        {
            var fix = new Fixture();
            fix.Drag(1, 3, 2);
            fix.Drag(5, 6, 2);
            fix.Zones.SettingsAt(fix.Cell(1, 2))!.Priority = StoragePriority.Urgent;
            fix.Zones.SettingsAt(fix.Cell(5, 2))!.ApplyPreset(StoragePreset.Nothing);

            var world = new SimWorldBuilder().WithSeed(1u).WithSize(Size).Build();
            using var stream = new System.IO.MemoryStream();
            Odyssey.Sim.Saving.WorldSave.Save(world, stream,
                new Odyssey.Sim.Saving.ISaveable[] { fix.Settings, fix.Zones });
            stream.Position = 0;

            var back = new Fixture();
            var backWorld = new SimWorldBuilder().WithSeed(1u).WithSize(Size).Build();
            Odyssey.Sim.Saving.WorldSave.Load(backWorld, stream,
                new Odyssey.Sim.Saving.ISaveable[] { back.Settings, back.Zones });

            Assert.That(back.Zones.ZoneCount, Is.EqualTo(2));
            Assert.That(back.Zones.Cells, Is.EqualTo(new List<int>(fix.Zones.Cells)));
            Assert.That(back.Zones.SettingsAt(back.Cell(1, 2))!.Priority, Is.EqualTo(StoragePriority.Urgent));
            Assert.That(back.Zones.Accepts(back.Cell(5, 2), ItemIndex.Wood), Is.False,
                "the narrowed zone came back open, which silently undoes every filter the player set");
            Assert.That(back.Zones.Accepts(back.Cell(1, 2), ItemIndex.Wood), Is.True,
                "the two zones came back sharing one settings record");
        }

        /// <summary>
        /// A settings record written before a commodity existed reads short, and what the missing
        /// entries mean is <b>authored</b> — the fault the old <c>Allow[]</c> had, where a save
        /// from an older def table silently refused every new item for ever.
        /// </summary>
        [Test]
        public void ACommodityAddedAfterASaveGetsTheAnswerThePresetGave()
        {
            var fix = new Fixture();
            int open = fix.Settings.Create(StoragePreset.Everything);
            int shut = fix.Settings.Create(StoragePreset.Nothing);

            // Shorten both filters, as a save from a build with fewer defs would.
            fix.Settings[open].Allow = new bool[2] { true, true };
            fix.Settings[shut].Allow = new bool[2] { false, false };

            var world = new SimWorldBuilder().WithSeed(1u).WithSize(Size).Build();
            using var stream = new System.IO.MemoryStream();
            Odyssey.Sim.Saving.WorldSave.Save(world, stream,
                new Odyssey.Sim.Saving.ISaveable[] { fix.Settings });
            stream.Position = 0;

            var back = new Fixture();
            var backWorld = new SimWorldBuilder().WithSeed(1u).WithSize(Size).Build();
            Odyssey.Sim.Saving.WorldSave.Load(backWorld, stream,
                new Odyssey.Sim.Saving.ISaveable[] { back.Settings });

            Assert.That(back.Settings[open].Allow, Has.Length.EqualTo(ItemIndex.Count),
                "the filter came back the old table's length and every later def is off the end");
            Assert.That(back.Settings[open].Accepts(ItemIndex.Carrots), Is.True,
                "a zone that accepted everything must accept a commodity added since");
            Assert.That(back.Settings[shut].Accepts(ItemIndex.Carrots), Is.False,
                "a zone the player narrowed must not quietly open for a commodity added since");
        }
    }
}
