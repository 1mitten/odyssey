#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// What the interface says about a shelf, and what it counts.
    ///
    /// <para>The counting half is the one that matters most and reads least like a feature: goods
    /// on a shelf are published at the shelf's cell carrying its id, which keeps every consumer of
    /// "what does the colony hold" correct <b>without being told anything about shelves</b>. The
    /// alternative — a channel of its own for contained goods — would have given four separate
    /// consumers a second place to look, and the one that was forgotten would have undercounted in
    /// silence.</para>
    /// </summary>
    public class ShelfReadoutTests
    {
        static readonly GridSize Size = new GridSize(10, 10, 4);
        static readonly CellRef At = new CellRef(3, 4, 1);

        static WorldSnapshot Snapshot()
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(tick: 0, Size, sliceLayer: 1);
            return snapshot;
        }

        [Test]
        public void WoodOnAShelfCountsTowardsWhatTheColonyCanBuildWith()
        {
            // **The correctness bug, in one assertion.** Without it the Build palette greys a
            // material the colony is holding, and a player is refused a wall they can pay for
            // because they tidied the timber away.
            WorldSnapshot snapshot = Snapshot();
            snapshot.AddThing(new ThingView(new ThingId(1), new CellRef(1, 1, 1), ItemHandle.Wood, 0, stack: 30));
            snapshot.AddThing(new ThingView(new ThingId(2), new CellRef(4, 4, 1), ItemHandle.Wood, 0,
                stack: 45, container: 7));

            Assert.That(ColonyStock.Of(snapshot, ItemHandle.Wood), Is.EqualTo(75),
                "loose and shelved alike");
            Assert.That(ColonyStock.Of(snapshot, ItemHandle.Stone), Is.Zero);
            Assert.That(ColonyStock.Of(snapshot, -1), Is.Zero, "a material with no item behind it");
        }

        [Test]
        public void TheStoresPanelCountsShelvedGoodsToo()
        {
            // Same rule, second consumer, and it needed no change at all: a ledger counts stacks
            // and not piles, so it was already right the moment the view existed.
            WorldSnapshot snapshot = Snapshot();
            snapshot.AddThing(new ThingView(new ThingId(1), new CellRef(4, 4, 1), ItemHandle.Meal, 0,
                stack: 12, container: 3));

            var ledger = new LedgerModel();
            ledger.Refresh(snapshot);

            LedgerRow meals = FindRow(ledger, "ui.res.meal");
            Assert.That(meals.Quantity, Is.EqualTo(12));
        }

        static LedgerRow FindRow(LedgerModel ledger, string key)
        {
            for (int i = 0; i < ledger.Rows.Count; i++)
                if (ledger.Rows[i].IconKey == key) return ledger.Rows[i];

            Assert.Fail($"no ledger row for {key}");
            return default;
        }

        // ---------------------------------------------------------------- the pane

        static CellDetail Shelf(byte stacks, byte slots, byte def, int units, byte priority) =>
            new CellDetail(
                Size.Index(At), TerrainHandle.Grass, (byte)EdificeHandle.Shelf, StuffHandle.None, 0,
                1000, 0,
                storageZone: -1, storagePriority: priority,
                storageCells: 1, storageOrdinal: 4,
                storeKind: CellDetail.StoreShelf, storedStacks: stacks, storeSlots: slots,
                storedDef: def, storedUnits: units);

        static InspectModel Showing(in CellDetail detail)
        {
            WorldSnapshot frame = Snapshot();
            frame.AddCellDetail(detail);

            var model = new InspectModel();
            model.SetCell(At);
            model.Refresh(frame);
            return model;
        }

        [Test]
        public void AShelfIsAStoreThePaneIsAbout()
        {
            // The whole of "reuse the controls". A click inside a painted store leads with the
            // store and puts the tile on a second tab; a shelf is a store the player built rather
            // than painted, and a pane that led with the tile for one and the store for the other
            // would be the same report arriving a second time.
            InspectModel model = Showing(Shelf(1, 8, (byte)ItemHandle.Wood, 400, StorageRung.Preferred));

            Assert.That(model.IsStore, Is.True);
            Assert.That(model.Title, Is.EqualTo(Registry.Label(PaletteTools.Shelf) + " 4"),
                "named as a shelf, and numbered in the one series stockpiles are numbered in");
            Assert.That(model.Subtitle, Is.EqualTo("1 of 8 stacks"),
                "its extent is how full it is — '1 tile' says nothing about a thing that is always one tile");
            Assert.That(model.Tabs, Has.Count.EqualTo(2));
            Assert.That(model.Tabs[0].Name, Is.EqualTo(Registry.Label(InspectModel.TabStorage)));
            Assert.That(RowValue(model, "storage"),
                Is.EqualTo(Registry.Label(StorageSettingsModel.PriorityKeys[StorageRung.Preferred])),
                "the rung is a fact; the settings are the tab");
        }

        [Test]
        public void AStockpileIsStillNamedAStockpile()
        {
            // The guard against the obvious way to break the line above: naming every store after
            // whichever kind was added last.
            var zone = new CellDetail(
                Size.Index(At), TerrainHandle.Grass, EdificeHandle.None, StuffHandle.None, 0, 1000, 0,
                storageZone: 0, storagePriority: StorageRung.Normal,
                storageCells: 9, storageOrdinal: 2, storeKind: CellDetail.StoreZone);

            InspectModel model = Showing(zone);
            Assert.That(model.Title, Is.EqualTo(Registry.Label(PaletteTools.Stockpile) + " 2"));
            Assert.That(model.Subtitle, Is.EqualTo("9 tiles"));
        }

        [Test]
        public void AShelfSaysHowFullItIsInStacks()
        {
            // Stacks rather than units against a total, because a unit total has no honest
            // denominator: eight slots of wood is 600 and eight of meals is 160, so one number
            // would make a full pantry read as nearly empty.
            Assert.That(RowValue(Showing(Shelf(1, 8, (byte)ItemHandle.Wood, 400, StorageRung.Normal)), "holding"),
                Is.EqualTo("Wood × 400 — 1 of 8 stacks"));

            Assert.That(RowValue(Showing(Shelf(3, 8, 255, 46, StorageRung.Normal)), "holding"),
                Is.EqualTo("46 — 3 of 8 stacks"), "several kinds say only how much and how full");

            Assert.That(RowValue(Showing(Shelf(0, 8, 255, 0, StorageRung.Normal)), "holding"),
                Is.EqualTo("empty — 8 stacks free"));
        }

        [Test]
        public void AShelfDoesNotGrowTheBedsOwnerRow()
        {
            // The pane's first interactive row keys off a quality tier, and a shelf takes none —
            // but the flag asks what the thing is as well now, because the next piece of
            // quality-bearing furniture would otherwise open the *bed* picker over itself.
            InspectModel model = Showing(Shelf(0, 8, 255, 0, StorageRung.Normal));
            Assert.That(model.BedUnderPane, Is.False);
        }

        // ---------------------------------------------------------------- the alert

        [Test]
        public void AStoreThatCannotBeEmptiedSaysSoAfterAWhile()
        {
            // The state comes from the simulation and the latch is the interface's, exactly as it
            // is for an idle colony. The wait matters: a shelf ordered taken apart is full until a
            // hauler has walked to it, so saying it is stuck at once would be crying wolf.
            WorldSnapshot frame = Snapshot();
            frame.AddStorageUnit(new StorageUnitView(cellIndex: 9, stacks: 2, slots: 8, emptying: true));

            var alerts = new AlertModel();
            alerts.Refresh(frame, seconds: 0.0);
            Assert.That(HasAlert(alerts, AlertModel.StoreStuckKey), Is.False, "not while somebody may be on their way");

            alerts.Refresh(frame, seconds: AlertModel.StoreStuckSustain + 0.1);
            Assert.That(HasAlert(alerts, AlertModel.StoreStuckKey), Is.True);
        }

        [Test]
        public void AStoreBeingEmptiedNormallyNeverRaisesIt()
        {
            WorldSnapshot frame = Snapshot();
            frame.AddStorageUnit(new StorageUnitView(cellIndex: 9, stacks: 0, slots: 8, emptying: true));

            var alerts = new AlertModel();
            alerts.Refresh(frame, seconds: 0.0);
            alerts.Refresh(frame, seconds: AlertModel.StoreStuckSustain + 0.1);

            Assert.That(HasAlert(alerts, AlertModel.StoreStuckKey), Is.False,
                "an emptied shelf is not a stuck one");
        }

        static bool HasAlert(AlertModel alerts, string key)
        {
            for (int i = 0; i < alerts.Rows.Count; i++)
                if (alerts.Rows[i].Key == key) return true;
            return false;
        }

        static string RowValue(InspectModel model, string name)
        {
            for (int i = 0; i < model.CellRows.Count; i++)
                if (model.CellRows[i].Name == name) return model.CellRows[i].Value;
            return string.Empty;
        }

        static class StorageRung
        {
            public const byte Normal = 2;
            public const byte Preferred = 3;
        }
    }
}
