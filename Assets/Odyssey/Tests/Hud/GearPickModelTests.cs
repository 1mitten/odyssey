#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Pick from stores (design 47 §3): for the hand, the weapons the stores hold, named by their
    /// store, eight a page, and a row chosen is the right-click menu's own equip order; for a worn
    /// slot, nothing yet — or the preview's made-up things while it is on.
    /// </summary>
    public class GearPickModelTests
    {
        static readonly GridSize Size = new GridSize(10, 10, 4);
        static readonly PawnId Ada = new PawnId(1);

        static int At(int x, int z, int y = 1) => Size.Index(x, z, y);

        /// <summary>Stockpile 1 at (1,1), Shelf 2 at (5,5); a bat in each, a machete on the shelf, a crowbar on open ground.</summary>
        static WorldSnapshot Colony()
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddStore(new StoreView(At(1, 1), zone: 0, priority: 2, ordinal: 1));
            frame.AddStorageUnit(new StorageUnitView(At(5, 5), stacks: 2, slots: 8, emptying: false, ordinal: 2));
            frame.AddThing(new ThingView(new ThingId(10), Size.FromIndex(At(1, 1)), ItemHandle.Bat, 0));
            frame.AddThing(new ThingView(new ThingId(11), Size.FromIndex(At(5, 5)), ItemHandle.Bat, 0, 1, container: 7));
            frame.AddThing(new ThingView(new ThingId(12), Size.FromIndex(At(5, 5)), ItemHandle.Machete, 0, 1, container: 7));
            frame.AddThing(new ThingView(new ThingId(13), Size.FromIndex(At(8, 8)), ItemHandle.Crowbar, 0));
            frame.AddThing(new ThingView(new ThingId(14), Size.FromIndex(At(1, 1)), 0 /* a meal */, 0, 4));
            return frame;
        }

        [Test]
        public void TheHandListsTheWeaponsInTheStoresAndNothingElse()
        {
            var pick = new GearPickModel();
            pick.Build(Colony(), GearSlot.Weapon, preview: null);

            Assert.That(pick.Rows.Select(r => (r.Name, r.Place)), Is.EqualTo(new[]
            {
                ("Bat", "Shelf 2"), ("Bat", "Stockpile 1"), ("Machete", "Shelf 2"),
            }), "the crowbar on open ground and the meal are not weapons in the stores");
            Assert.That(pick.Rows.All(r => r.PreviewIndex < 0), Is.True);
            Assert.That(pick.SlotName, Is.EqualTo("Weapon"));
        }

        /// <summary>The Gear tab and the right-click menu must send the same order for the same weapon.</summary>
        [Test]
        public void ChoosingAWeaponIsTheContextMenusEquipOrder()
        {
            var pick = new GearPickModel();
            pick.Build(Colony(), GearSlot.Weapon, preview: null);

            Assert.That(pick.Choose(2, Ada, preview: null, out Intent order), Is.True);
            ThingView machete = pick.Rows[2].Thing;
            Assert.That(order, Is.EqualTo(CombatOrders.Equip(Ada, machete)));
            Assert.That(order.Kind, Is.EqualTo(IntentKind.OrderEquip));
            Assert.That(order.A, Is.EqualTo(Ada.Value));
            Assert.That(order.B, Is.EqualTo(12));
        }

        [Test]
        public void EightRowsAPage()
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddStore(new StoreView(At(1, 1), zone: 0, priority: 2, ordinal: 1));
            for (int i = 0; i < 10; i++)
            {
                frame.AddStore(new StoreView(At(i, 3), zone: 0, priority: 2, ordinal: 1));
                frame.AddThing(new ThingView(new ThingId(100 + i), Size.FromIndex(At(i, 3)), ItemHandle.Bat, 0));
            }

            var pick = new GearPickModel();
            pick.Build(frame, GearSlot.Weapon, preview: null);
            Assert.That(pick.Total, Is.EqualTo(10));
            Assert.That(pick.PageCount, Is.EqualTo(2));
            Assert.That(pick.Rows.Count, Is.EqualTo(GearLayout.PickMaxRows));

            pick.NextPage();
            Assert.That(pick.Rows.Count, Is.EqualTo(2));
            pick.NextPage();
            Assert.That(pick.Page, Is.EqualTo(1), "no page after the last");
            pick.PreviousPage();
            Assert.That(pick.Rows.Count, Is.EqualTo(8));
        }

        /// <summary>No garment exists yet: a worn slot's list is empty with the preview off.</summary>
        [Test]
        public void AWornSlotListsNothingUntilThePreview()
        {
            var pick = new GearPickModel();
            var preview = new GearPreview();
            pick.Build(Colony(), GearSlot.Head, preview);
            Assert.That(pick.Rows, Is.Empty);

            preview.Set(true);
            pick.Build(Colony(), GearSlot.Head, preview);
            Assert.That(pick.Rows.Select(r => r.Name), Is.EqualTo(new[] { "Wool cap", "Helmet" }));
            Assert.That(pick.Rows.All(r => r.Place == "Preview"), Is.True);
        }

        /// <summary>A made-up row dresses the preview and sends nothing to the world.</summary>
        [Test]
        public void ChoosingAPreviewRowDressesThePreviewAndSendsNothing()
        {
            var preview = new GearPreview();
            preview.Set(true);
            preview.Remove(Ada, GearSlot.Head);

            var pick = new GearPickModel();
            pick.Build(Colony(), GearSlot.Head, preview);
            Assert.That(pick.Choose(1, Ada, preview, out _), Is.False, "nothing to send");
            Assert.That(preview.TryWorn(Ada, GearSlot.Head, out PreviewItem worn), Is.True);
            Assert.That(worn.Key, Is.EqualTo("ui.item.helmet"));
        }

        [Test]
        public void UnequipAndDropDifferByOneFlag()
        {
            Intent unequip = CombatOrders.Unequip(Ada, leaveHere: false);
            Intent drop = CombatOrders.Unequip(Ada, leaveHere: true);
            Assert.That(unequip.Kind, Is.EqualTo(IntentKind.OrderUnequip));
            Assert.That((unequip.A, unequip.B), Is.EqualTo((Ada.Value, 0)));
            Assert.That((drop.A, drop.B), Is.EqualTo((Ada.Value, 1)));
        }
    }
}
