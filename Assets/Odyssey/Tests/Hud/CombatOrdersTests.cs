#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The fight's half of a right-click (design 33 §1, §5j): the owner's four gestures, the Ctrl
    /// rule, and everything else still a move. <see cref="CombatOrders.Route"/> is Unity-free so
    /// the rule lives here; the presenter that hears the click only carries the answer.
    ///
    /// <para>Most of these go through <see cref="OrderModel.RightClick"/>, the one door the
    /// presenter uses, so a claimed click is also shown <em>not</em> to have become a move as
    /// well.</para>
    /// </summary>
    public class CombatOrdersTests
    {
        static readonly PawnId Ada = new PawnId(1), Bo = new PawnId(2), Hog = new PawnId(3),
            Raider = new PawnId(4), Cy = new PawnId(5);

        static readonly ThingId Machete = new ThingId(20), Wood = new ThingId(21);

        static readonly CellRef Ground = new CellRef(1, 8, 1);
        static readonly CellRef MacheteCell = new CellRef(3, 3, 1);
        static readonly CellRef WoodCell = new CellRef(2, 2, 1);

        /// <summary>
        /// Two colonists, a hog, a marauder and a downed colonist, a machete and a pile of wood on
        /// the ground. <paramref name="drafted"/> names who is under the player's hand.
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

            AspectKey key = AspectKey.Of(OrderModel.DraftedAspect);
            foreach (PawnId pawn in drafted) frame.AddPawnAspect(new PawnAspect(pawn, key, 1));
            return frame;
        }

        static readonly PawnId[] Both = { Ada, Bo };

        static List<Intent> RightClick(IReadOnlyList<PawnId> selection, WorldSnapshot frame, CellRef? cell,
            PawnId under, bool ctrl = false)
        {
            var sent = new List<Intent>();
            var menu = new List<ContextMenuRow>();
            OrderModel.RightClick(selection, frame, cell, under, ctrl, sent, menu);
            Assert.That(menu, Is.Empty, "a click that acts at once opened a menu as well");
            return sent;
        }

        static void AssertAttacks(List<Intent> sent, PawnId target, params PawnId[] attackers)
        {
            Assert.That(sent.Count, Is.EqualTo(attackers.Length), "one order per drafted attacker, and no move");
            for (int i = 0; i < attackers.Length; i++)
            {
                Assert.That(sent[i].Kind, Is.EqualTo(IntentKind.OrderAttack));
                Assert.That(sent[i].A, Is.EqualTo(attackers[i].Value));
                Assert.That(sent[i].B, Is.EqualTo(target.Value));
            }
        }

        // ---- the owner's four gestures ------------------------------------------------------

        [Test]
        public void RightClickOnAnAnimalAttacksFromEveryDraftedColonist()
        {
            WorldSnapshot frame = Board(Ada, Bo);
            AssertAttacks(RightClick(Both, frame, new CellRef(6, 6, 1), Hog), Hog, Ada, Bo);
        }

        [Test]
        public void RightClickOnAHostileAttacks()
        {
            WorldSnapshot frame = Board(Ada, Bo);
            AssertAttacks(RightClick(Both, frame, new CellRef(8, 8, 1), Raider), Raider, Ada, Bo);
        }

        /// <summary>
        /// Ctrl turns a click on one of our own into an attack; without it the click is the move it
        /// was in C1 (a colonist's hit box covers the cell behind her, design 33 §2f). The control
        /// is the same click without Ctrl.
        /// </summary>
        [Test]
        public void CtrlRightClickOnAColonistAttacksAndWithoutCtrlItIsAMove()
        {
            WorldSnapshot frame = Board(Ada);
            var ada = new[] { Ada };

            List<Intent> plain = RightClick(ada, frame, new CellRef(7, 4, 1), Bo);
            Assert.That(plain.Count, Is.EqualTo(1));
            Assert.That(plain[0].Kind, Is.EqualTo(IntentKind.OrderMove), "the control: no Ctrl is a move");

            AssertAttacks(RightClick(ada, frame, new CellRef(7, 4, 1), Bo, ctrl: true), Bo, Ada);
        }

        [Test]
        public void ACtrlClickedColonistDoesNotAttackHerself()
        {
            WorldSnapshot frame = Board(Ada, Bo);
            AssertAttacks(RightClick(Both, frame, new CellRef(7, 4, 1), Bo, ctrl: true), Bo, Ada);
        }

        /// <summary>
        /// One body, one carrier: a downed colonist is rescued by the nearest drafted colonist in
        /// the selection, not by all of them. Bo stands a cell from Cy and Ada four.
        /// </summary>
        [Test]
        public void RightClickOnADownedColonistSendsTheNearestDraftedColonistToRescue()
        {
            WorldSnapshot frame = Board(Ada, Bo);
            List<Intent> sent = RightClick(Both, frame, new CellRef(8, 4, 1), Cy);

            Assert.That(sent.Count, Is.EqualTo(1), "one rescuer, and no move");
            Assert.That(sent[0].Kind, Is.EqualTo(IntentKind.OrderRescue));
            Assert.That(sent[0].A, Is.EqualTo(Bo.Value), "not the nearest");
            Assert.That(sent[0].B, Is.EqualTo(Cy.Value));
        }

        /// <summary>
        /// Ctrl on a downed colonist is the explicit attack, and wins over the rescue: Ctrl is the
        /// one gesture that says "hit this one of ours" in so many words.
        /// </summary>
        [Test]
        public void CtrlOnADownedColonistIsAnAttackNotARescue()
        {
            WorldSnapshot frame = Board(Ada);
            AssertAttacks(RightClick(new[] { Ada }, frame, new CellRef(8, 4, 1), Cy, ctrl: true), Cy, Ada);
        }

        // The weapon's right-click opens the context menu now (design 33 §7a): ContextMenuModelTests.

        // ---- what stays a move -----------------------------------------------------------------

        /// <summary>
        /// The undrafted: no attack and no rescue (design 33 §5j), whatever is clicked, and the
        /// move after it sends nothing either, because the move is for the drafted too. The equip
        /// is the one order that does not need a draft, and it is the context menu's
        /// (<c>ContextMenuModelTests</c>).
        /// </summary>
        [Test]
        public void AnUndraftedSelectionSendsNoAttackAndNoRescue()
        {
            WorldSnapshot frame = Board();
            Assert.That(RightClick(Both, frame, new CellRef(6, 6, 1), Hog), Is.Empty, "attacked a hog");
            Assert.That(RightClick(Both, frame, new CellRef(8, 8, 1), Raider), Is.Empty, "attacked a marauder");
            Assert.That(RightClick(Both, frame, new CellRef(8, 4, 1), Cy), Is.Empty, "rescued");
            Assert.That(RightClick(Both, frame, new CellRef(7, 4, 1), Bo, ctrl: true), Is.Empty, "Ctrl-attacked");
        }

        [Test]
        public void AClickNothingClaimsIsStillAMove()
        {
            WorldSnapshot frame = Board(Ada);
            foreach (CellRef cell in new[] { Ground, WoodCell })
            {
                List<Intent> sent = RightClick(new[] { Ada }, frame, cell, PawnId.None);
                Assert.That(sent.Count, Is.EqualTo(1), $"at {cell}");
                Assert.That(sent[0].Kind, Is.EqualTo(IntentKind.OrderMove), "a pile of wood is not a weapon");
                Assert.That(sent[0].Cell, Is.EqualTo(cell));
            }
        }

        /// <summary>
        /// A building is not a target until C6 (design 33 §5j), and even then a floor never is. A
        /// click on a cell whose detail reports a wall, and on one with a wooden floor, moves the
        /// drafted colonist exactly as a click on grass does.
        /// </summary>
        [Test]
        public void AClickOnAFlooredOrWalledCellIsStillAMove()
        {
            WorldSnapshot frame = Board(Ada);
            var wall = new CellRef(2, 6, 1);
            var floor = new CellRef(3, 6, 1);
            frame.AddCellDetail(new CellDetail(frame.Size.Index(wall), (byte)TerrainHandle.Soil,
                (byte)EdificeHandle.Wall, 0, 0, 1000, 0));
            frame.AddCellDetail(new CellDetail(frame.Size.Index(floor), (byte)TerrainHandle.Soil,
                (byte)EdificeHandle.None, (byte)StuffHandle.Wood, 0, 1000, 0));

            foreach (CellRef cell in new[] { wall, floor })
            {
                List<Intent> sent = RightClick(new[] { Ada }, frame, cell, PawnId.None);
                Assert.That(sent.Count, Is.EqualTo(1));
                Assert.That(sent[0].Kind, Is.EqualTo(IntentKind.OrderMove), $"a building at {cell} was attacked");
            }
        }

        [Test]
        public void ADownedOrHostileSelectionIsNobodysToOrder()
        {
            // The draft cannot reach either (design 33 §5c), but a stale selection can hold them:
            // neither is an attacker.
            WorldSnapshot frame = Board(Ada);
            AssertAttacks(RightClick(new[] { Raider, Cy, Ada }, frame, new CellRef(6, 6, 1), Hog), Hog, Ada);
        }

        // ---- the presenter's gate --------------------------------------------------------------

        /// <summary>
        /// The presenter asks before it hit-tests. Until the equip, only a drafted selection took a
        /// right-click; now a colonist drafted or not does, because a weapon is fetched without a
        /// draft. An animal alone still takes none.
        /// </summary>
        [Test]
        public void ARightClickIsHeardForAnyColonistDraftedOrNot()
        {
            Assert.That(OrderModel.HearsRightClick(Both, Board()), Is.True, "an undrafted colonist can equip");
            Assert.That(OrderModel.AnyDrafted(Both, Board()), Is.False, "the old gate, which could not");
            Assert.That(OrderModel.HearsRightClick(new[] { Hog }, Board()), Is.False);
            Assert.That(OrderModel.HearsRightClick(new[] { Raider }, Board()), Is.False);
            Assert.That(OrderModel.HearsRightClick(new PawnId[0], Board()), Is.False);
        }

        [Test]
        public void TheFourWeaponsAreWeaponsAndNothingElseIs()
        {
            for (int def = 0; def < ItemHandle.Count; def++)
            {
                bool weapon = def == ItemHandle.Bat || def == ItemHandle.Crowbar
                    || def == ItemHandle.Machete || def == ItemHandle.ArcBlade;
                Assert.That(CombatOrders.IsWeapon(def), Is.EqualTo(weapon), $"item def {def}");
            }
            Assert.That(CombatOrders.IsWeapon(-1), Is.False);
        }
    }
}
