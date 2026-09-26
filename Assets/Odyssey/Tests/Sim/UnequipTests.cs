#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.WeaponFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// <c>OrderUnequip</c> (design 47 §3, the Gear tab's weapon popover): the hand empties at once
    /// and the weapon lies at her feet. <c>B = 0</c> is <b>Unequip</b>, "put it down", and a hauler
    /// may carry it to the stores; <c>B = 1</c> is <b>Drop</b>, "leave this here", and the thing is
    /// forbidden so it stays where it lies. Refused for anybody who is not a standing colonist of
    /// ours, and a no-op for the bare hands.
    /// </summary>
    public class UnequipTests
    {
        static IntentRejection Unequip(ColonyWorld colony, Pawn pawn, bool leaveHere) =>
            Send(colony, new Intent(IntentKind.OrderUnequip, Size.FromIndex(pawn.Cell), pawn.Id.Value, leaveHere ? 1 : 0));

        static ColonyItem Arm(ColonyWorld colony, Pawn pawn, int def)
        {
            ColonyItem weapon = PutDown(colony, pawn, def);
            WeaponHand.TakeUp(pawn, weapon, colony.Pawns);
            Assume.That(WeaponHand.Held(pawn, colony.Pawns), Is.SameAs(weapon));
            return weapon;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TheHandEmptiesAndTheWeaponLiesAtHerFeet(bool leaveHere)
        {
            var colony = Board();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            colony.World.Tick(30);
            ColonyItem machete = Arm(colony, colonist, ItemIndex.Machete);

            Assert.That(Unequip(colony, colonist, leaveHere), Is.EqualTo(IntentRejection.None));

            Assert.That(colonist.EquippedItem, Is.Zero);
            Assert.That(WeaponHand.Held(colonist, colony.Pawns), Is.Null);
            Assert.That(machete.Despawned, Is.False);
            Assert.That(machete.CarriedBy, Is.Zero);
            Assert.That(machete.Cell, Is.GreaterThanOrEqualTo(0), "the weapon has no cell");
            CellRef at = Size.FromIndex(machete.Cell), feet = Size.FromIndex(colonist.Cell);
            Assert.That(System.Math.Max(System.Math.Abs(at.X - feet.X), System.Math.Abs(at.Z - feet.Z)),
                Is.LessThanOrEqualTo(JobDriver.DropSearchRadius), "not at her feet");
            Assert.That(colony.Pawns.WeaponRules.ArmamentOf(colonist, colony.Pawns).ItemDef, Is.LessThan(0),
                "she still fights with it");
        }

        /// <summary>
        /// The difference between the two words is the forbidden flag and nothing else: Drop keeps
        /// it where it lies, Unequip leaves it to the haulers.
        /// </summary>
        [Test]
        public void DropForbidsItAndUnequipDoesNot()
        {
            var colony = Board();
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            colony.World.Tick(30);
            ColonyItem bat = Arm(colony, a, ItemIndex.Bat);
            ColonyItem crowbar = Arm(colony, b, ItemIndex.Crowbar);

            Assert.That(Unequip(colony, a, leaveHere: false), Is.EqualTo(IntentRejection.None));
            Assert.That(Unequip(colony, b, leaveHere: true), Is.EqualTo(IntentRejection.None));

            Assert.That(bat.Forbidden, Is.False, "Unequip forbade it");
            Assert.That(crowbar.Forbidden, Is.True, "Drop did not keep it here");
        }

        /// <summary>A dropped weapon is an ordinary thing again: she can take it straight back up.</summary>
        [Test]
        public void AnUnequippedWeaponCanBeEquippedAgain()
        {
            var colony = Board();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            colony.World.Tick(30);
            ColonyItem bat = Arm(colony, colonist, ItemIndex.Bat);
            Assert.That(Unequip(colony, colonist, leaveHere: false), Is.EqualTo(IntentRejection.None));

            Assert.That(Equip(colony, colonist, bat), Is.EqualTo(IntentRejection.None));
            Assert.That(RunUntil(colony, () => colonist.EquippedItem == bat.Id.Value, 3_000), Is.True);
        }

        [Test]
        public void TheBareHandsAreAlreadyEmpty()
        {
            var colony = Board();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            colony.World.Tick(30);
            Assert.That(Unequip(colony, colonist, leaveHere: false), Is.EqualTo(IntentRejection.AlreadyInThatState));
        }

        [Test]
        public void OnlyAStandingColonistOfOursMayBeOrdered()
        {
            var colony = Board();
            Pawn colonist = colony.Pawns.Pawns.All[0], other = colony.Pawns.Pawns.All[1];
            colony.World.Tick(30);
            ColonyItem bat = Arm(colony, colonist, ItemIndex.Bat);
            Arm(colony, other, ItemIndex.Crowbar);
            Pawn bandit = SpawnKind(colony, PawnKindIndex.Bandit);

            Assert.That(Unequip(colony, bandit, leaveHere: false), Is.EqualTo(IntentRejection.NotPermitted), "a hostile");
            Assert.That(Send(colony, new Intent(IntentKind.OrderUnequip, default, 9_999, 0)),
                Is.EqualTo(IntentRejection.NotPermitted), "nobody");

            colonist.Downed = true;
            Assert.That(Unequip(colony, colonist, leaveHere: false), Is.EqualTo(IntentRejection.NotPermitted), "downed");
            Assert.That(colonist.EquippedItem, Is.EqualTo(bat.Id.Value), "the downed let go of it");

            Assert.That(Unequip(colony, other, leaveHere: false), Is.EqualTo(IntentRejection.None), "the control");
        }

        [Test]
        public void ItAppliesWhilePaused() =>
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.OrderUnequip), Is.True);
    }
}
