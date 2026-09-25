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
        /// Two colonists, a hog, a bandit and a downed colonist, a machete and a pile of wood on
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
            PawnId under, bool ctrl = false, int edifice = EdificeHandle.None)
        {
            var sent = new List<Intent>();
            var menu = new List<ContextMenuRow>();
            OrderModel.RightClick(selection, frame, cell, under, ctrl, sent, menu, edifice);
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
            Assert.That(RightClick(Both, frame, new CellRef(8, 8, 1), Raider), Is.Empty, "attacked a bandit");
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

        // ---- buildings (C6, design 33 §13i) ------------------------------------------------------

        /// <summary>
        /// The content's table as the simulation publishes it: which edifices have hit points. A
        /// tree and nothing have none.
        /// </summary>
        static WorldSnapshot WithBuildings(WorldSnapshot frame)
        {
            frame.SetEdificeHitPoints(EdificeHandle.Wall, 300);
            frame.SetEdificeHitPoints(EdificeHandle.Door, 160);
            frame.SetEdificeHitPoints(EdificeHandle.Bed, 120);
            frame.SetEdificeHitPoints(EdificeHandle.Shelf, 100);
            return frame;
        }

        static readonly CellRef WallCell = new CellRef(2, 6, 1);
        static readonly CellRef FloorCell = new CellRef(3, 6, 1);

        /// <summary>
        /// A right-click on a wall, a door or a bed — an edifice that occupies the cell and has hit
        /// points — is an attack by every selected drafted colonist, with the building as target 0
        /// at the cell clicked (design 33 §5j, §13i).
        /// </summary>
        [Test]
        public void RightClickOnABuildingAttacksItFromEveryDraftedColonist()
        {
            WorldSnapshot frame = WithBuildings(Board(Ada, Bo));
            foreach (int edifice in new[] { EdificeHandle.Wall, EdificeHandle.Door, EdificeHandle.Bed })
            {
                List<Intent> sent = RightClick(Both, frame, WallCell, PawnId.None, edifice: edifice);
                AssertAttacks(sent, PawnId.None, Ada, Bo);
                Assert.That(sent[0].Cell, Is.EqualTo(WallCell), $"edifice {edifice}");
            }
        }

        /// <summary>
        /// The case the brief names (design 33 §5j): <b>a click on a floored cell still moves</b> —
        /// a floor is a slab, not an edifice, so the presenter reports nothing standing there. So
        /// does a tree, which stands in its cell but has no hit points, and a cell whose edifice the
        /// frame knows nothing of. The control is the wall, which attacks.
        /// </summary>
        [Test]
        public void AClickOnAFlooredCellIsStillAMove()
        {
            WorldSnapshot frame = WithBuildings(Board(Ada));
            frame.AddCellDetail(new CellDetail(frame.Size.Index(FloorCell), (byte)TerrainHandle.Soil,
                (byte)EdificeHandle.None, (byte)StuffHandle.Wood, 0, 1000, 0));

            foreach (int edifice in new[] { EdificeHandle.None, EdificeHandle.TreeBirch, EdificeHandle.Campfire })
            {
                List<Intent> sent = RightClick(new[] { Ada }, frame, FloorCell, PawnId.None, edifice: edifice);
                Assert.That(sent.Count, Is.EqualTo(1));
                Assert.That(sent[0].Kind, Is.EqualTo(IntentKind.OrderMove), $"edifice {edifice} was attacked");
                Assert.That(sent[0].Cell, Is.EqualTo(FloorCell));
            }

            List<Intent> wall = RightClick(new[] { Ada }, frame, WallCell, PawnId.None, edifice: EdificeHandle.Wall);
            Assert.That(wall[0].Kind, Is.EqualTo(IntentKind.OrderAttack), "the control: a wall");
        }

        /// <summary>
        /// A pawn under the pointer wins over the building it stands on (design 33 §6C): a colonist
        /// on a bed, no Ctrl, is the move it always was, and a hog on one is attacked, not the bed.
        /// </summary>
        [Test]
        public void APawnUnderThePointerWinsOverTheBuildingItStandsOn()
        {
            WorldSnapshot frame = WithBuildings(Board(Ada));
            List<Intent> onBo = RightClick(new[] { Ada }, frame, new CellRef(7, 4, 1), Bo, edifice: EdificeHandle.Bed);
            Assert.That(onBo.Count, Is.EqualTo(1));
            Assert.That(onBo[0].Kind, Is.EqualTo(IntentKind.OrderMove));

            AssertAttacks(RightClick(new[] { Ada }, frame, new CellRef(6, 6, 1), Hog, edifice: EdificeHandle.Bed), Hog, Ada);
        }

        /// <summary>No draft, no fight (design 33 §5j): an undrafted selection sends nothing at a wall, and no move either.</summary>
        [Test]
        public void AnUndraftedSelectionSendsNothingAtABuilding()
        {
            WorldSnapshot frame = WithBuildings(Board());
            Assert.That(RightClick(Both, frame, WallCell, PawnId.None, edifice: EdificeHandle.Wall), Is.Empty);
        }

        /// <summary>
        /// A weapon on a shelf opens the context menu rather than the shelf being attacked (design 33
        /// §13i): equipping is the answer the owner asked for on a weapon (§7a).
        /// </summary>
        [Test]
        public void AWeaponOnAShelfOpensTheMenuRatherThanAnAttack()
        {
            WorldSnapshot frame = WithBuildings(Board(Ada));
            var shelf = new CellRef(5, 2, 1);
            frame.AddThing(new ThingView(new ThingId(30), shelf, ItemHandle.Bat, 0, 1, 1, 0));

            var sent = new List<Intent>();
            var menu = new List<ContextMenuRow>();
            OrderModel.RightClick(new[] { Ada }, frame, shelf, PawnId.None, false, sent, menu, EdificeHandle.Shelf);
            Assert.That(sent, Is.Empty, "the shelf was attacked");
            Assert.That(menu, Is.Not.Empty, "no menu for the bat");

            List<Intent> bare = RightClick(new[] { Ada }, frame, WallCell, PawnId.None, edifice: EdificeHandle.Shelf);
            Assert.That(bare[0].Kind, Is.EqualTo(IntentKind.OrderAttack), "the control: an empty shelf is attacked");
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
        public void TheWeaponsAreWeaponsAndNothingElseIs()
        {
            for (int def = 0; def < ItemHandle.Count; def++)
            {
                bool weapon = def == ItemHandle.Bat || def == ItemHandle.Crowbar
                    || def == ItemHandle.Machete || def == ItemHandle.ArcBlade
                    || def == ItemHandle.Pistol;
                Assert.That(CombatOrders.IsWeapon(def), Is.EqualTo(weapon), $"item def {def}");
            }
            Assert.That(CombatOrders.IsWeapon(-1), Is.False);
        }
    }
}
