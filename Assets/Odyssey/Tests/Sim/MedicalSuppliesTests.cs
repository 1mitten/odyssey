#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Storage;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Medical supplies as a thing (design 37 §5, MD1): what the Def says, and that the colony
    /// stores, shelves and fetches them by the rules every other commodity already follows. The
    /// owner asked for exactly this — "ensure items can be stored on stockpile, shelves and is
    /// carried" — and the point of these tests is that it took no storage code to do it.
    /// </summary>
    public class MedicalSuppliesTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 8);
        const uint Seed = 20260924;
        const int Medical = ItemIndex.MedicalSupplies;

        static ColonyWorld Fresh()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 0;
            return ColonyWorld.Build(Size, Seed, scenario);
        }

        static void Run(ColonyWorld colony, int ticks)
        {
            for (int i = 0; i < ticks; i++) colony.World.Tick();
        }

        /// <summary>The <paramref name="nth"/> walkable empty cell in rings about the start.</summary>
        static int FreeGround(ColonyWorld colony, int nth, System.Func<int, bool>? also = null)
        {
            CellRef start = colony.Start;
            int seen = 0;
            for (int radius = 1; radius < 16; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!Size.Contains(x, z, start.Y)) continue;

                int cell = Size.Index(x, z, start.Y);
                if (!colony.Pawns.Cells.IsWalkable(cell)) continue;
                if (!colony.Pawns.Items.CellHasSpace(cell)) continue;
                if (also != null && !also(cell)) continue;
                if (seen++ != nth) continue;
                return cell;
            }

            return -1;
        }

        /// <summary>The scenario's own kit is forbidden, so the only thing to haul is what a test put down.</summary>
        static void ForbidWhatIsAlreadyLying(ColonyWorld colony)
        {
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].Cell >= 0) items[i].Forbidden = true;
        }

        [Test]
        public void TheDefIsMedicineTenToAStackAndHealsForty()
        {
            ItemDef def = ContentPack.Pawns().Items[Medical];

            Assert.That(def.defName, Is.EqualTo("Item_MedicalSupplies"));
            Assert.That(def.category, Is.EqualTo(ItemCategory.Medicine),
                "a store's Medicine row is what files it; Materials would put it beside the stone");
            Assert.That(def.stackLimit, Is.EqualTo(10), "one shelf bay (owner, 2026-09-24)");
            Assert.That(def.healPerUnit, Is.EqualTo(40), "owner, 2026-09-24");
            Assert.That(def.haulable, Is.True);
            Assert.That(def.nutrition, Is.Zero, "nobody eats a box of dressings");
        }

        [Test]
        public void AMedicineOnlyStockpileTakesTheSuppliesAndRefusesTheWood()
        {
            ColonyWorld colony = Fresh();
            ForbidWhatIsAlreadyLying(colony);

            int pile = FreeGround(colony, 0);
            Assume.That(pile, Is.GreaterThanOrEqualTo(0));
            Assume.That(colony.Pawns.Storage!.Designate(Size.FromIndex(pile), pile, StoragePreset.Nothing),
                Is.EqualTo(IntentRejection.None));
            StorageSettings settings = colony.Pawns.Storage.SettingsAt(pile)!;
            settings.SetCategory(ItemCategory.Medicine, true, colony.Pawns.Content);

            Assert.That(settings.Accepts(Medical), Is.True);
            Assert.That(settings.Accepts(ItemIndex.Wood), Is.False);

            int supplies = FreeGround(colony, 3, cell => cell != pile);
            int wood = FreeGround(colony, 5, cell => cell != pile && cell != supplies);
            colony.Pawns.Items.Spawn(Medical, supplies, 6);
            colony.Pawns.Items.Spawn(ItemIndex.Wood, wood, 20);

            Run(colony, 4_000);

            ColonyItem? stored = colony.Pawns.Items.ItemAt(pile);
            Assert.That(stored, Is.Not.Null, "nobody stored the supplies in 4,000 ticks");
            Assert.That(stored!.DefIndex, Is.EqualTo(Medical));
            Assert.That(stored.Stack, Is.EqualTo(6), "all six, none lost in the carrying");
            Assert.That(colony.Pawns.Items.ItemAt(wood)?.DefIndex, Is.EqualTo(ItemIndex.Wood),
                "the wood has nowhere that accepts it and stays where it lies");
        }

        [Test]
        public void AWholeStackGoesOnAShelfInOneBay()
        {
            ColonyWorld colony = Fresh();
            int shelf = FreeGround(colony, 0, cell => colony.Construction.Allows(cell, BuildingHandle.Shelf));
            Assume.That(shelf, Is.GreaterThanOrEqualTo(0));
            Assume.That(colony.Construction.Place(Size.FromIndex(shelf), BuildingHandle.Shelf, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None));
            colony.Construction.Raise(colony.Pawns, shelf);
            colony.RebuildDerived();
            StorageUnit unit = colony.Pawns.StorageUnits!.AtCell(shelf)!;
            Assume.That(unit, Is.Not.Null);

            ForbidWhatIsAlreadyLying(colony);
            int ground = FreeGround(colony, 2, cell => cell != shelf);
            colony.Pawns.Items.Spawn(Medical, ground, 10);

            Run(colony, 4_000);

            ColonyItem? shelved = colony.Pawns.Items.ResidentIn(StorageUnits.ContainerIdOf(unit.Edifice), Medical);
            Assert.That(shelved, Is.Not.Null, "the supplies are on the shelf");
            Assert.That(shelved!.Stack, Is.EqualTo(10), "a full stack is one bay, not two");
            Assert.That(colony.Pawns.Items.ItemAt(ground), Is.Null, "and nothing was left behind");
        }

        [Test]
        public void TheMedicalDropLandsFourToEightSupplies()
        {
            ColonyWorld colony = Fresh();
            IncidentDef def = colony.Incidents.Content.Defs[IncidentHandle.MedicalDrop];
            Assert.That(def.defName, Is.EqualTo("Incident_MedicalDrop"));
            Assert.That(colony.Incidents.Content.ItemIndex[IncidentHandle.MedicalDrop], Is.EqualTo(Medical));
            Assert.That(colony.Incidents.Content.Workers[IncidentHandle.MedicalDrop], Is.InstanceOf<SupplyDropWorker>());
            Assert.That(def.bulletinKey, Is.EqualTo("ui.bulletin.medicaldrop"));

            int before = CountOnBoard(colony);
            colony.World.Intents.Submit(new Intent(IntentKind.InvokeIncident, default, IncidentHandle.MedicalDrop));
            Run(colony, def.fallTicks + 2);

            int landed = CountOnBoard(colony) - before;
            Assert.That(landed, Is.InRange(4, 8), "owner, 2026-09-24: four to eight");
        }

        static int CountOnBoard(ColonyWorld colony)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == Medical) total += items[i].Stack;
            return total;
        }
    }
}
