#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.WeaponFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A weapon outlives its holder (design 33 §1, §6D): the dead let go of it where they fall,
    /// and the downed keep it. Heard through the <c>Died</c> hook, which is the only way lane D
    /// learns of a death.
    /// </summary>
    public class WeaponDropTests
    {
        static (ColonyWorld colony, Pawn colonist, ColonyItem weapon) Armed(int def = ItemIndex.Bat)
        {
            var colony = Board();
            colony.World.Tick(30);
            Pawn colonist = colony.Pawns.Pawns.All[0];
            ColonyItem weapon = PutDown(colony, colonist, def);
            WeaponHand.TakeUp(colonist, weapon, colony.Pawns);
            Assert.That(colonist.EquippedItem, Is.EqualTo(weapon.Id.Value), "the fixture could not arm her");
            return (colony, colonist, weapon);
        }

        /// <summary>
        /// The dead let go: the weapon lies on the corpse's cell, carried by nobody, a thing any
        /// colonist may take up. The control is the moment before the death, when it had no cell.
        /// </summary>
        [Test]
        public void TheDeadDropTheirWeaponWhereTheyFell()
        {
            var (colony, colonist, bat) = Armed();
            Assert.That(bat.Cell, Is.EqualTo(-1), "the control: held, it has no cell");
            ClearGround(colony, colonist.Cell);

            Corpse corpse = Kill(colony, colonist);

            Assert.That(bat.Despawned, Is.False, "the weapon died with her");
            Assert.That(bat.CarriedBy, Is.Zero);
            Assert.That(bat.Cell, Is.EqualTo(corpse.Cell));
            Assert.That(colony.Pawns.Items.ItemAt(corpse.Cell), Is.SameAs(bat));
            Assert.That(colonist.EquippedItem, Is.Zero);
            Assert.That(colony.Pawns.WeaponRules.CanEquip(colony.Pawns.Pawns.All[0], bat, colony.Pawns), Is.True,
                "the survivor may not take it up");
        }

        /// <summary>
        /// A corpse cell that already holds something: the weapon goes to the nearest cell that
        /// can take it, as every other dropped load does, rather than on top of the pile. (Nearest
        /// free, not adjacent: a start is crowded with meals and scrap.)
        /// </summary>
        [Test]
        public void ADeathOnAPileDropsTheWeaponBesideIt()
        {
            var (colony, colonist, bat) = Armed();
            ClearGround(colony, colonist.Cell);
            colony.Pawns.Items.Spawn(ItemIndex.Stone, colonist.Cell, stack: 10);

            Corpse corpse = Kill(colony, colonist);

            Assert.That(bat.Cell, Is.GreaterThanOrEqualTo(0));
            Assert.That(bat.Cell, Is.Not.EqualTo(corpse.Cell));
            Assert.That(colony.Pawns.Items.ItemAt(bat.Cell), Is.SameAs(bat));
            Assert.That(colony.Pawns.Items.ItemAt(corpse.Cell)!.DefIndex, Is.EqualTo(ItemIndex.Stone));
            CellRef at = Size.FromIndex(bat.Cell), fell = Size.FromIndex(corpse.Cell);
            Assert.That(System.Math.Max(System.Math.Abs(at.X - fell.X), System.Math.Abs(at.Z - fell.Z)),
                Is.LessThanOrEqualTo(JobDriver.DropSearchRadius), "the weapon was carried off");
        }

        /// <summary>The downed keep theirs (the C2 default): going down says nothing to the hand.</summary>
        [Test]
        public void TheDownedKeepTheirWeapon()
        {
            var (colony, colonist, bat) = Armed(ItemIndex.Machete);
            colonist.Downed = true;
            colony.Pawns.CombatHooks.RaiseDowned(colonist, null, colony.World.CurrentTick);

            Assert.That(colonist.EquippedItem, Is.EqualTo(bat.Id.Value));
            Assert.That(bat.CarriedBy, Is.EqualTo(colonist.Id.Value));
            Assert.That(bat.Cell, Is.EqualTo(-1));
        }

        /// <summary>A death with nothing in the hand drops nothing, and the ground is as it was.</summary>
        [Test]
        public void ABareHandedDeathDropsNothing()
        {
            var colony = Board();
            colony.World.Tick(30);
            Pawn colonist = colony.Pawns.Pawns.All[0];
            int things = colony.Pawns.Items.Items.Count(i => !i.Despawned && i.Cell >= 0);
            Kill(colony, colonist);
            Assert.That(colony.Pawns.Items.Items.Count(i => !i.Despawned && i.Cell >= 0), Is.EqualTo(things));
        }

        /// <summary>
        /// The weapons lane listens once, first: <c>CombatListeners.Register</c> is the one place
        /// a listener is added, and C4 and C5 append after it.
        /// </summary>
        [Test]
        public void TheWeaponDropIsTheFirstListenerAndListensOnce()
        {
            var colony = Board();
            var listeners = colony.Pawns.CombatHooks.Listeners;
            Assert.That(listeners.Count(l => l is WeaponDropListener), Is.EqualTo(1));
            Assert.That(listeners[0], Is.InstanceOf<WeaponDropListener>());
        }
    }
}
