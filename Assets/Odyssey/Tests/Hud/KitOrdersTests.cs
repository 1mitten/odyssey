#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The interface's half of the kit (design 54): the names it reads, the orders it sends, the
    /// caps it mirrors, and the three places that draw it — the Gear tab's kit row, Pick from stores
    /// and the right-click menu — each with its control.
    /// </summary>
    public class KitOrdersTests
    {
        static readonly GridSize Size = new GridSize(10, 10, 4);
        static readonly PawnId Ada = new PawnId(1);

        static int At(int x, int z, int y = 1) => Size.Index(x, z, y);

        /// <summary>
        /// Ada, with whatever is in her kit; a stockpile at (1,1) holding eight medical supplies and
        /// ten wood, and three rations loose at (8,8).
        /// </summary>
        static WorldSnapshot Board(PawnFlags flags = PawnFlags.None, params (int Def, int Count, int Use)[] kit)
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddPawn(new PawnView(Ada, new CellRef(3, 3, 1), 800, 800, 800, JobHandle.Wait, flags: PawnFlags.Person | flags));
            for (int slot = 0; slot < kit.Length; slot++)
            {
                if (kit[slot].Def < 0) continue;
                frame.AddPawnAspect(new PawnAspect(Ada, KitOrders.DefKey(slot), kit[slot].Def));
                frame.AddPawnAspect(new PawnAspect(Ada, KitOrders.CountKey(slot), kit[slot].Count));
                if (kit[slot].Use != KitUseHandle.None)
                    frame.AddPawnAspect(new PawnAspect(Ada, KitOrders.UseKey(slot), kit[slot].Use));
            }
            frame.AddStore(new StoreView(At(1, 1), zone: 0, priority: 2, ordinal: 1));
            frame.AddStore(new StoreView(At(2, 1), zone: 0, priority: 2, ordinal: 1));
            frame.AddThing(new ThingView(new ThingId(20), Size.FromIndex(At(1, 1)), ItemHandle.MedicalSupplies, 0, 8));
            frame.AddThing(new ThingView(new ThingId(21), Size.FromIndex(At(2, 1)), ItemHandle.Wood, 0, 10));
            frame.AddThing(new ThingView(new ThingId(22), Size.FromIndex(At(8, 8)), ItemHandle.Meal, 0, 3));
            return frame;
        }

        // ---- the mirror and the names ----------------------------------------------------------

        /// <summary>Held to <c>ItemDef.kitCap</c>, which <c>KitTests</c> holds to the same numbers from the Defs.</summary>
        [Test]
        public void OnlyMedicalSuppliesAndRationsFitAKit()
        {
            for (int def = 0; def < ItemHandle.Count; def++)
            {
                int cap = def == ItemHandle.MedicalSupplies ? 5 : def == ItemHandle.Meal ? 3 : 0;
                Assert.That(KitOrders.Cap(def), Is.EqualTo(cap), $"item def {def}");
            }
            Assert.That(KitOrders.Fits(-1), Is.False);
        }

        [Test]
        public void TheAspectNamesAreSpelledAsTheSimulationPublishesThem()
        {
            Assert.That(KitOrders.DefKey(0), Is.EqualTo(AspectKey.Of("odyssey.pawn.kit.0")));
            Assert.That(KitOrders.CountKey(1), Is.EqualTo(AspectKey.Of("odyssey.pawn.kit.1.count")));
            Assert.That(KitOrders.UseKey(1), Is.EqualTo(AspectKey.Of("odyssey.pawn.kit.1.use")));
            Assert.That(KitOrders.Slots, Is.EqualTo(GearLayout.KitBelt));
        }

        [Test]
        public void TheOrdersCarryTheirArguments()
        {
            var thing = new ThingView(new ThingId(20), new CellRef(1, 1, 1), ItemHandle.MedicalSupplies, 0, 8);
            Intent take = KitOrders.Take(Ada, thing);
            Assert.That((take.Kind, take.Cell, take.A, take.B), Is.EqualTo((IntentKind.OrderTakeIntoKit, thing.Cell, 1, 20)));
            Intent drop = KitOrders.Drop(Ada, 1, leaveHere: true);
            Assert.That((drop.Kind, drop.A, drop.B, drop.C), Is.EqualTo((IntentKind.OrderKitDrop, 1, 1, 1)));
            Assert.That(KitOrders.Drop(Ada, 0, leaveHere: false).C, Is.Zero);
            Intent use = KitOrders.Use(Ada, 1);
            Assert.That((use.Kind, use.A, use.B), Is.EqualTo((IntentKind.OrderUseKit, 1, 1)));
        }

        /// <summary>One slot's worth, as the simulation's <c>Kit.TakeRoom</c>: top up, else one empty slot, else nothing.</summary>
        [Test]
        public void TakeRoomIsOneSlotsWorth()
        {
            const int Med = ItemHandle.MedicalSupplies, Ration = ItemHandle.Meal;
            Assert.That(KitOrders.TakeRoom(Board(), Ada, Med), Is.EqualTo(5), "an empty kit: one slot's cap");
            Assert.That(KitOrders.TakeRoom(Board(kit: (Med, 2, 0)), Ada, Med), Is.EqualTo(3), "a top-up, not a second slot");
            Assert.That(KitOrders.TakeRoom(Board(kit: (Med, 5, 0)), Ada, Med), Is.EqualTo(5), "a full slot of it: the empty one");
            Assert.That(KitOrders.TakeRoom(Board(kit: new[] { (Med, 5, 0), (Ration, 3, 0) }), Ada, Med), Is.Zero, "both full");
            Assert.That(KitOrders.TakeRoom(Board(kit: new[] { (Med, 2, 0), (Med, 5, 0) }), Ada, Ration), Is.Zero, "no slot for a new kind");
            Assert.That(KitOrders.TakeRoom(Board(), Ada, ItemHandle.Wood), Is.Zero, "wood fits no kit");
        }

        // ---- the Gear tab ----------------------------------------------------------------------

        [Test]
        public void TheKitRowDrawsHerRealKit()
        {
            var gear = new GearModel();
            Assert.That(gear.Refresh(Board(kit: new[] { (ItemHandle.MedicalSupplies, 4, KitUseHandle.NotHurt), (-1, 0, 0) }), Ada), Is.True);

            KitTile first = gear.Kit[0];
            Assert.That(first.State, Is.EqualTo(KitTileState.Filled));
            Assert.That(first.Real, Is.True);
            Assert.That((first.Slot, first.ItemDef, first.Count, first.CountText), Is.EqualTo((0, ItemHandle.MedicalSupplies, 4, "4")));
            Assert.That(first.Name, Is.EqualTo(ItemLabels.Label(ItemHandle.MedicalSupplies)));
            Assert.That(first.Use, Is.EqualTo(KitUseHandle.NotHurt));
            Assert.That(first.UseReason, Is.EqualTo(Registry.Label(KitOrders.NotHurtKey)));

            Assert.That(gear.Kit[1].State, Is.EqualTo(KitTileState.Empty));
            Assert.That(gear.Kit[1].Slot, Is.EqualTo(1), "an empty real slot opens Pick from stores for that slot");
            Assert.That(gear.Kit[2].State, Is.EqualTo(KitTileState.Locked));
            Assert.That(gear.Effects[3].Value, Is.EqualTo(GearModel.KitCount(1, 2)), "the effects line counts the real kit");
        }

        [Test]
        public void TheKitRowRebuildsWhenACountMovesAndNotOtherwise()
        {
            var gear = new GearModel();
            gear.Refresh(Board(kit: (ItemHandle.MedicalSupplies, 4, 0)), Ada);
            int version = gear.Version;
            gear.Refresh(Board(kit: (ItemHandle.MedicalSupplies, 4, 0)), Ada);
            Assert.That(gear.Version, Is.EqualTo(version), "the control: nothing moved");
            gear.Refresh(Board(kit: (ItemHandle.MedicalSupplies, 3, 0)), Ada);
            Assert.That(gear.Version, Is.Not.EqualTo(version), "one spent and the tab did not notice");
            Assert.That(gear.Kit[0].CountText, Is.EqualTo("3"));
        }

        /// <summary>With the preview on, the whole row is the preview's, as the worn slots are.</summary>
        [Test]
        public void ThePreviewReplacesTheRealKit()
        {
            var preview = new GearPreview();
            preview.Set(true);
            var gear = new GearModel { Preview = preview };
            gear.Refresh(Board(kit: (ItemHandle.MedicalSupplies, 4, 0)), Ada);
            Assert.That(gear.Kit[0].Real, Is.False);
            Assert.That(gear.Kit[0].PreviewIndex, Is.EqualTo(0));
        }

        // ---- Pick from stores ------------------------------------------------------------------

        [Test]
        public void PickFromStoresListsStoredKitThingsAndSendsTheMenusOrder()
        {
            var pick = new GearPickModel();
            pick.BuildKit(Board(), Ada, 0);

            Assert.That(pick.Rows.Select(r => (r.Name, r.Place)), Is.EqualTo(new[]
                { (ItemLabels.Label(ItemHandle.MedicalSupplies), "Stockpile 1") }),
                "the wood fits no kit and the rations are loose, not in the stores");
            Assert.That(pick.Rows[0].QualityWord, Is.EqualTo("× 8"));
            Assert.That(pick.SlotName, Is.EqualTo(Registry.Label(GearModel.KitKey)));

            Assert.That(pick.Choose(0, Ada, preview: null, out Intent order), Is.True);
            Assert.That(order, Is.EqualTo(KitOrders.Take(Ada, pick.Rows[0].Thing)));
        }

        [Test]
        public void AFullKitHasNothingToPick()
        {
            var pick = new GearPickModel();
            pick.BuildKit(Board(kit: new[] { (ItemHandle.MedicalSupplies, 5, 0), (ItemHandle.Meal, 3, 0) }), Ada, 1);
            Assert.That(pick.Total, Is.Zero);
        }

        /// <summary>The hand's list is untouched by the kit's: it still lists weapons, and sends Equip.</summary>
        [Test]
        public void TheHandsListIsStillTheHands()
        {
            var pick = new GearPickModel();
            pick.BuildKit(Board(), Ada, 0);
            pick.Build(Board(), GearSlot.Weapon, preview: null);
            Assert.That(pick.KitSlot, Is.EqualTo(-1));
            Assert.That(pick.Total, Is.Zero, "no weapons in these stores");
        }

        // ---- the right-click menu --------------------------------------------------------------

        static List<ContextMenuRow> Menu(WorldSnapshot frame, CellRef cell)
        {
            var rows = new List<ContextMenuRow>();
            ContextMenuModel.Build(new[] { Ada }, frame, cell, PawnId.None, rows);
            return rows;
        }

        [Test]
        public void RightClickingSuppliesOffersTakeIntoKit()
        {
            List<ContextMenuRow> rows = Menu(Board(), Size.FromIndex(At(1, 1)));
            ContextMenuRow take = rows.Single(r => r.Key == KitOrders.TakeKey);
            Assert.That(take.Enabled, Is.True);
            Assert.That(take.Label, Is.EqualTo(KitOrders.TakeLabel(ItemHandle.MedicalSupplies)));
            Assert.That(take.Intents.Single().Kind, Is.EqualTo(IntentKind.OrderTakeIntoKit));

            Assert.That(Menu(Board(), Size.FromIndex(At(2, 1))), Is.Empty, "the control: wood offers nothing");
        }

        [Test]
        public void AFullKitOrADownedColonistGreysTheRow()
        {
            ContextMenuRow full = Menu(Board(kit: new[] { (ItemHandle.MedicalSupplies, 5, 0), (ItemHandle.Meal, 3, 0) }),
                Size.FromIndex(At(1, 1))).Single(r => r.Key == KitOrders.TakeKey);
            Assert.That(full.Enabled, Is.False);
            Assert.That(full.Reason, Is.EqualTo(Registry.Label(KitOrders.KitFullKey)));

            ContextMenuRow downed = Menu(Board(PawnFlags.Downed), Size.FromIndex(At(1, 1))).Single(r => r.Key == KitOrders.TakeKey);
            Assert.That(downed.Enabled, Is.False);
            Assert.That(downed.Reason, Is.EqualTo(Registry.Label(ContextMenuModel.DownedReasonKey)));
        }
    }
}
