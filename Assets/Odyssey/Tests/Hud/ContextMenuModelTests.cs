#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The context menu (design 33 §7a; owner, 2026-09-23: picking up a weapon "wasn't clear").
    /// A right-click on a weapon opens a menu of <i>Equip &lt;weapon&gt;</i> and <i>Cancel</i>; bare
    /// ground stays an instant move and an enemy an instant attack. Everything goes through
    /// <see cref="OrderModel.RightClick"/>, the one door the presenter uses, so each case is also
    /// shown not to have done the other thing — a menu that also acted, or an act that also asked.
    /// </summary>
    public class ContextMenuModelTests
    {
        static readonly PawnId Ada = new PawnId(1), Bo = new PawnId(2), Hog = new PawnId(3),
            Raider = new PawnId(4), Cy = new PawnId(5);

        static readonly ThingId Machete = new ThingId(20), Wood = new ThingId(21),
            Bat = new ThingId(22), SecondBat = new ThingId(23), Blade = new ThingId(24);

        static readonly CellRef Ground = new CellRef(1, 8, 1);
        static readonly CellRef MacheteCell = new CellRef(3, 3, 1);
        static readonly CellRef WoodCell = new CellRef(2, 2, 1);
        static readonly CellRef ShelfCell = new CellRef(5, 2, 1);

        /// <summary>
        /// Two colonists, a hog, a bandit and a downed colonist; a machete and a pile of wood on
        /// the ground, and a shelf holding two bats and an arc blade. <paramref name="drafted"/>
        /// names who is under the player's hand.
        /// </summary>
        static WorldSnapshot Board(params PawnId[] drafted)
        {
            WorldSnapshot frame = Frame.Write(layers: 4);
            frame.AddPawn(new PawnView(Ada, new CellRef(4, 4, 1), 800, 800, 700, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Bo, new CellRef(7, 4, 1), 800, 800, 700, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Hog, new CellRef(6, 6, 1), 800, 800, 700, kind: 1, flags: PawnFlags.None));
            frame.AddPawn(new PawnView(Raider, new CellRef(8, 8, 1), 800, 800, 700, kind: 3,
                flags: PawnFlags.Person | PawnFlags.Hostile));
            frame.AddPawn(new PawnView(Cy, new CellRef(8, 4, 1), 800, 800, 700,
                flags: PawnFlags.Person | PawnFlags.Downed));

            frame.AddThing(new ThingView(Machete, MacheteCell, ItemHandle.Machete, 0));
            frame.AddThing(new ThingView(Wood, WoodCell, ItemHandle.Wood, 0, stack: 20));
            frame.AddThing(new ThingView(Bat, ShelfCell, ItemHandle.Bat, 0, container: 9, slot: 0));
            frame.AddThing(new ThingView(SecondBat, ShelfCell, ItemHandle.Bat, 0, container: 9, slot: 1));
            frame.AddThing(new ThingView(Blade, ShelfCell, ItemHandle.ArcBlade, 0, container: 9, slot: 2));

            AspectKey key = AspectKey.Of(OrderModel.DraftedAspect);
            foreach (PawnId pawn in drafted) frame.AddPawnAspect(new PawnAspect(pawn, key, 1));
            return frame;
        }

        static readonly PawnId[] Both = { Ada, Bo };

        /// <summary>The right-click, and what it did: the orders it sent and the menu it opened.</summary>
        static (List<Intent> Sent, List<ContextMenuRow> Menu) RightClick(IReadOnlyList<PawnId> selection,
            WorldSnapshot frame, CellRef? cell, PawnId under = default, bool ctrl = false)
        {
            var sent = new List<Intent>();
            var menu = new List<ContextMenuRow>();
            OrderModel.RightClick(selection, frame, cell, under, ctrl, sent, menu);
            return (sent, menu);
        }

        static List<Intent> Choose(ContextMenuRow row)
        {
            var sent = new List<Intent>();
            ContextMenuModel.Choose(row, sent);
            return sent;
        }

        // ---- a weapon opens the menu --------------------------------------------------------------

        /// <summary>
        /// The owner's decision: <i>Equip machete</i> and <i>Cancel</i>, in that order, for a
        /// selection drafted or not — and nothing sent until a row is chosen.
        /// </summary>
        [Test]
        public void AWeaponOpensTheMenuWithEquipAndCancelDraftedOrNot()
        {
            foreach (WorldSnapshot frame in new[] { Board(), Board(Ada, Bo) })
            {
                var (sent, menu) = RightClick(Both, frame, MacheteCell);
                Assert.That(sent, Is.Empty, "the right-click acted before a row was chosen");
                Assert.That(menu.Count, Is.EqualTo(2));

                Assert.That(menu[0].Key, Is.EqualTo(ContextMenuModel.EquipKey));
                Assert.That(menu[0].Label, Is.EqualTo("Equip machete"));
                Assert.That(menu[0].Enabled, Is.True);
                Assert.That(menu[0].Reason, Is.Empty);

                Assert.That(menu[1].IsCancel, Is.True);
                Assert.That(menu[1].Label, Is.EqualTo("Cancel"));
                Assert.That(menu[1].Enabled, Is.True);
            }
        }

        /// <summary>
        /// Equip sends <c>OrderEquip</c> for the <b>primary</b> colonist only — one weapon fills
        /// one hand — aimed at the weapon's own cell and naming the thing.
        /// </summary>
        [Test]
        public void EquipSendsOrderEquipForThePrimaryColonist()
        {
            var (_, menu) = RightClick(Both, Board(), MacheteCell);
            List<Intent> sent = Choose(menu[0]);

            Assert.That(sent.Count, Is.EqualTo(1), "one colonist is sent, not the selection");
            Assert.That(sent[0].Kind, Is.EqualTo(IntentKind.OrderEquip));
            Assert.That(sent[0].A, Is.EqualTo(Ada.Value), "not the primary");
            Assert.That(sent[0].B, Is.EqualTo(Machete.Value));
            Assert.That(sent[0].Cell, Is.EqualTo(MacheteCell));
        }

        [Test]
        public void TheFirstStandingColonistInTheSelectionIsThePrimary()
        {
            // A hog, a bandit and a downed colonist ahead of Bo in a stale selection are passed
            // over; Bo is sent.
            var (_, menu) = RightClick(new[] { Hog, Raider, Cy, Bo, Ada }, Board(), MacheteCell);
            List<Intent> sent = Choose(menu[0]);
            Assert.That(sent.Count, Is.EqualTo(1));
            Assert.That(sent[0].A, Is.EqualTo(Bo.Value));
        }

        [Test]
        public void CancelSendsNothing()
        {
            var (_, menu) = RightClick(Both, Board(Ada), MacheteCell);
            ContextMenuRow cancel = menu[menu.Count - 1];
            Assert.That(cancel.IsCancel, Is.True);
            Assert.That(cancel.Intents, Is.Empty);
            Assert.That(Choose(cancel), Is.Empty);
        }

        /// <summary>
        /// Downed colonists cannot take a weapon up (§6D refuses them): the row is there, so the
        /// player sees why, but disabled with the reason "Downed" and nothing behind it.
        /// </summary>
        [Test]
        public void ADownedSelectionGetsEquipDisabledWithAReason()
        {
            var (sent, menu) = RightClick(new[] { Cy }, Board(), MacheteCell);
            Assert.That(sent, Is.Empty);
            Assert.That(menu.Count, Is.EqualTo(2));
            Assert.That(menu[0].Key, Is.EqualTo(ContextMenuModel.EquipKey));
            Assert.That(menu[0].Enabled, Is.False);
            Assert.That(menu[0].Reason, Is.EqualTo("Downed"));
            Assert.That(menu[0].Intents, Is.Empty);
            Assert.That(Choose(menu[0]), Is.Empty, "a disabled row sent its order");
        }

        /// <summary>
        /// <see cref="ContextMenuModel.Choose"/> is the one door from a row to the world, and it
        /// refuses a disabled row whatever the row carries — so a view that forgets to look at
        /// <c>Enabled</c> still cannot send one.
        /// </summary>
        [Test]
        public void ChooseSendsNothingForADisabledRowWhateverItCarries()
        {
            var order = new Intent(IntentKind.OrderEquip, MacheteCell, Ada.Value, Machete.Value);
            var disabled = new ContextMenuRow(ContextMenuModel.EquipKey, "Equip machete", enabled: false,
                "Downed", new[] { order });
            var enabled = new ContextMenuRow(ContextMenuModel.EquipKey, "Equip machete", enabled: true,
                string.Empty, new[] { order });

            Assert.That(Choose(disabled), Is.Empty);
            Assert.That(Choose(enabled).Count, Is.EqualTo(1), "the control: the same row enabled sends");
        }

        /// <summary>
        /// An animal or a bandit takes no orders, so a selection with no colonist in it opens no
        /// menu at all — a menu of one Cancel would say nothing — and sends nothing.
        /// </summary>
        [Test]
        public void AnAnimalOrHostileSelectionOpensNoMenuAndSendsNothing()
        {
            foreach (PawnId[] selection in new[] { new[] { Hog }, new[] { Raider }, new[] { Hog, Raider } })
            {
                var (sent, menu) = RightClick(selection, Board(), MacheteCell);
                Assert.That(menu, Is.Empty);
                Assert.That(sent, Is.Empty);
            }
        }

        /// <summary>
        /// The pick names the block a thing lies on as often as the air it lies in, so a click on
        /// the ground under the machete finds it one cell up — the rule a left click already uses
        /// to select a pile (<c>SelectionDirector.ThingAt</c>).
        /// </summary>
        [Test]
        public void AWeaponIsFoundOnTheBlockUnderItToo()
        {
            var (sent, menu) = RightClick(Both, Board(), MacheteCell.Below);
            Assert.That(sent, Is.Empty);
            Assert.That(menu.Count, Is.EqualTo(2));
            Assert.That(Choose(menu[0])[0].B, Is.EqualTo(Machete.Value));
        }

        /// <summary>
        /// "Or in a store" (owner): a shelf holding weapons offers one row per <i>kind</i> — two bats
        /// are one "Equip bat" — each sending the first thing of its kind, then Cancel.
        /// </summary>
        [Test]
        public void AStoreOffersOneRowPerKindOfWeaponItHolds()
        {
            var (sent, menu) = RightClick(Both, Board(), ShelfCell);
            Assert.That(sent, Is.Empty);
            Assert.That(menu.Count, Is.EqualTo(3), "bat, arc blade, cancel");
            Assert.That(menu[0].Label, Is.EqualTo("Equip bat"));
            Assert.That(menu[1].Label, Is.EqualTo("Equip arc blade"));
            Assert.That(menu[2].IsCancel, Is.True);

            Assert.That(Choose(menu[0])[0].B, Is.EqualTo(Bat.Value));
            Assert.That(Choose(menu[1])[0].B, Is.EqualTo(Blade.Value));
        }

        // ---- what does not open it --------------------------------------------------------------

        /// <summary>Bare ground, and a pile that is not a weapon, stay the instant move: no menu.</summary>
        [Test]
        public void GroundIsAnInstantMoveAndNoMenu()
        {
            foreach (CellRef cell in new[] { Ground, WoodCell })
            {
                var (sent, menu) = RightClick(new[] { Ada }, Board(Ada), cell);
                Assert.That(menu, Is.Empty, $"a menu at {cell}");
                Assert.That(sent.Count, Is.EqualTo(1));
                Assert.That(sent[0].Kind, Is.EqualTo(IntentKind.OrderMove));
                Assert.That(sent[0].Cell, Is.EqualTo(cell));
            }
        }

        /// <summary>
        /// An enemy has one sensible order, so it takes one click (owner): an instant attack from
        /// every drafted colonist, and no menu — even when it stands on the machete, because the
        /// pawn under the pointer wins over the cell it stands in.
        /// </summary>
        [Test]
        public void AnEnemyIsAnInstantAttackAndNoMenuEvenOnAWeapon()
        {
            foreach (PawnId enemy in new[] { Hog, Raider })
            {
                var (sent, menu) = RightClick(Both, Board(Ada, Bo), MacheteCell, enemy);
                Assert.That(menu, Is.Empty, "a menu over an enemy");
                Assert.That(sent.Count, Is.EqualTo(2));
                Assert.That(sent.TrueForAll(i => i.Kind == IntentKind.OrderAttack && i.B == enemy.Value), Is.True);
            }
        }

        /// <summary>
        /// The context menu (design 33 §7a) is the top rung: it closes before anything else Escape
        /// could mean, and with it shut the order below is untouched.
        /// </summary>
        [Test]
        public void EscapeClosesTheContextMenuFirst()
        {
            var settings = new SettingsDirector();
            Assert.That(settings.Escape(true, false, true, true, true, true, true, null),
                Is.EqualTo(EscapeAction.CloseContextMenu), "something else unwound before the menu at the pointer");
            Assert.That(settings.Escape(false, false, true, true, true, true, true, null),
                Is.EqualTo(EscapeAction.CloseMenu), "the control: without it the order is as it was");
            Assert.That(settings.Escape(false, false, false, false, false, false, false, null),
                Is.EqualTo(EscapeAction.OpenPanel));

            // And over the two panels main added beside it (the merge of 2026-09-24), which is the
            // overload the presenter actually calls.
            Assert.That(settings.Escape(true, false, false, false, false, false, false, true, true, null),
                Is.EqualTo(EscapeAction.CloseContextMenu), "the inventory or research panel unwound before the menu");
            Assert.That(settings.Escape(false, false, false, false, false, false, false, true, false, null),
                Is.EqualTo(EscapeAction.CloseInventory), "the control: with the menu shut the inventory closes");
            Assert.That(settings.Escape(false, false, false, false, false, false, false, false, true, null),
                Is.EqualTo(EscapeAction.CloseResearch));
        }

        [Test]
        public void TheLabelIsTheRegistrysVerbAndTheRegistrysWeapon()
        {
            // Both words from icon-keys.csv, so renaming either renames the row.
            Assert.That(ContextMenuModel.EquipLabel(ItemHandle.Crowbar),
                Is.EqualTo(Registry.Label("ui.command.equip") + " " + Registry.Label("ui.item.crowbar").ToLowerInvariant()));
            Assert.That(Registry.Labels.ContainsKey(ContextMenuModel.CancelKey), Is.True, "Cancel has no key");
            Assert.That(Registry.Labels.ContainsKey(ContextMenuModel.EquipKey), Is.True);
            Assert.That(Registry.Labels.ContainsKey(ContextMenuModel.DownedReasonKey), Is.True);
        }
    }
}
