#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.WeaponFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What a pawn swings with and what it may take up (design 33 §6D, lane D): the equipped
    /// weapon first, else the species' teeth, else bare hands; and a weapon may be taken only by a
    /// standing colonist, only while it lies somewhere real, and never while it is forbidden.
    /// </summary>
    public class WeaponRulesTests
    {
        [Test]
        public void AColonistWithNothingInTheHandFightsWithFists()
        {
            var colony = Board();
            PawnContext ctx = colony.Pawns;
            Pawn colonist = ctx.Pawns.All[0];

            Armament bare = ctx.WeaponRules.ArmamentOf(colonist, ctx);
            Assert.That(bare.Attack, Is.SameAs(ctx.Content.Combat.fists));
            Assert.That(bare.Armed, Is.False);
            Assert.That(bare.ItemDef, Is.EqualTo(-1));
        }

        [Test]
        public void AnAnimalFightsWithItsTeeth()
        {
            var colony = Board();
            PawnContext ctx = colony.Pawns;
            Pawn hog = SpawnKind(colony, PawnKindIndex.MiddenHog);

            Armament teeth = ctx.WeaponRules.ArmamentOf(hog, ctx);
            Assert.That(teeth.Attack, Is.SameAs(hog.Species.naturalAttack));
            Assert.That(teeth.Attack, Is.Not.SameAs(ctx.Content.Combat.fists), "the control: teeth are not fists");
            Assert.That(teeth.Armed, Is.False);
        }

        /// <summary>
        /// Each of the four, once in the hand, is what she swings: its own attack block, shared with
        /// the content rather than copied, and its item def. The control is the same colonist a
        /// moment before, who swings her fists.
        /// </summary>
        [TestCase(ItemIndex.Bat)]
        [TestCase(ItemIndex.Crowbar)]
        [TestCase(ItemIndex.Machete)]
        [TestCase(ItemIndex.ArcBlade)]
        public void AWeaponInTheHandIsWhatSheSwings(int def)
        {
            var colony = Board();
            PawnContext ctx = colony.Pawns;
            Pawn colonist = ctx.Pawns.All[0];
            ColonyItem weapon = PutDown(colony, colonist, def);

            Assert.That(ctx.WeaponRules.ArmamentOf(colonist, ctx).Armed, Is.False, "armed before taking it up");

            WeaponHand.TakeUp(colonist, weapon, ctx);
            Armament armed = ctx.WeaponRules.ArmamentOf(colonist, ctx);

            Assert.That(armed.Attack, Is.SameAs(ctx.Content.Items[def].weapon));
            Assert.That(armed.ItemDef, Is.EqualTo(def));
            Assert.That(armed.Armed, Is.True);
        }

        /// <summary>
        /// A weapon on the ground is not in anybody's hand, whatever a pawn's field says. The
        /// armament asks the item where it is, so a hand that names a thing lying on a cell swings
        /// fists — which is what makes "held" one fact with one owner (the item's carrier) rather
        /// than two that could disagree.
        /// </summary>
        [Test]
        public void AWeaponLyingOnTheGroundArmsNobody()
        {
            var colony = Board();
            PawnContext ctx = colony.Pawns;
            Pawn colonist = ctx.Pawns.All[0];
            ColonyItem weapon = PutDown(colony, colonist, ItemIndex.Bat);

            colonist.EquippedItem = weapon.Id.Value;
            Assert.That(ctx.WeaponRules.ArmamentOf(colonist, ctx).Armed, Is.False);

            // The control: the same thing taken up is a weapon.
            WeaponHand.TakeUp(colonist, weapon, ctx);
            Assert.That(ctx.WeaponRules.ArmamentOf(colonist, ctx).Armed, Is.True);
        }

        /// <summary>
        /// The stun is lane A's to roll and apply; what is lane D's is that the armament of a held
        /// bat or crowbar <em>carries</em> it (combat-contracts, lane D). The control is the sharp
        /// pair, which carry none.
        /// </summary>
        [TestCase(ItemIndex.Bat, true)]
        [TestCase(ItemIndex.Crowbar, true)]
        [TestCase(ItemIndex.Machete, false)]
        [TestCase(ItemIndex.ArcBlade, false)]
        public void ABluntWeaponCarriesItsStunAndASharpOneNone(int def, bool blunt)
        {
            var colony = Board();
            PawnContext ctx = colony.Pawns;
            Pawn colonist = ctx.Pawns.All[0];
            WeaponHand.TakeUp(colonist, PutDown(colony, colonist, def), ctx);

            AttackDef attack = ctx.WeaponRules.ArmamentOf(colonist, ctx).Attack;
            Assert.That(attack.damageKind, Is.EqualTo(blunt ? DamageKind.Blunt : DamageKind.Sharp));
            if (blunt)
            {
                Assert.That(attack.stunPerMille, Is.GreaterThan(0));
                Assert.That(attack.stunTicks, Is.GreaterThan(0));
            }
            else Assert.That(attack.stunPerMille, Is.Zero);
        }

        // ---- CanEquip ------------------------------------------------------------------------

        [Test]
        public void AStandingColonistMayTakeUpAWeaponLyingOnTheGround()
        {
            var colony = Board();
            PawnContext ctx = colony.Pawns;
            Pawn colonist = ctx.Pawns.All[0];
            Assert.That(ctx.WeaponRules.CanEquip(colonist, PutDown(colony, colonist, ItemIndex.Crowbar), ctx), Is.True);
        }

        [Test]
        public void AThingThatIsNotAWeaponCannotBeTakenUp()
        {
            var colony = Board();
            PawnContext ctx = colony.Pawns;
            Pawn colonist = ctx.Pawns.All[0];
            Assert.That(ctx.WeaponRules.CanEquip(colonist, PutDown(colony, colonist, ItemIndex.Stone), ctx), Is.False);
            Assert.That(ctx.WeaponRules.CanEquip(colonist, PutDown(colony, colonist, ItemIndex.Bat, away: 9), ctx), Is.True,
                "the control: a bat beside it");
        }

        [Test]
        public void AForbiddenWeaponCannotBeTakenUp()
        {
            var colony = Board();
            PawnContext ctx = colony.Pawns;
            Pawn colonist = ctx.Pawns.All[0];
            ColonyItem bat = PutDown(colony, colonist, ItemIndex.Bat);
            bat.Forbidden = true;
            Assert.That(ctx.WeaponRules.CanEquip(colonist, bat, ctx), Is.False);
            bat.Forbidden = false;
            Assert.That(ctx.WeaponRules.CanEquip(colonist, bat, ctx), Is.True, "the control: allowed again");
        }

        [Test]
        public void AWeaponInSomebodysHandCannotBeTakenUp()
        {
            var colony = Board();
            PawnContext ctx = colony.Pawns;
            Pawn a = ctx.Pawns.All[0], b = ctx.Pawns.All[1];
            ColonyItem bat = PutDown(colony, a, ItemIndex.Bat);
            Assert.That(ctx.WeaponRules.CanEquip(b, bat, ctx), Is.True, "the control: on the ground");
            WeaponHand.TakeUp(a, bat, ctx);
            Assert.That(ctx.WeaponRules.CanEquip(b, bat, ctx), Is.False);
        }

        [Test]
        public void NoAnimalHostileOrDownedColonistMayTakeUpAWeapon()
        {
            var colony = Board();
            PawnContext ctx = colony.Pawns;
            Pawn colonist = ctx.Pawns.All[0];
            ColonyItem bat = PutDown(colony, colonist, ItemIndex.Bat);

            Pawn hog = SpawnKind(colony, PawnKindIndex.MiddenHog);
            Pawn marauder = SpawnKind(colony, PawnKindIndex.Marauder);
            Assert.That(ctx.WeaponRules.CanEquip(hog, bat, ctx), Is.False, "an animal");
            Assert.That(ctx.WeaponRules.CanEquip(marauder, bat, ctx), Is.False, "a hostile");

            Assert.That(ctx.WeaponRules.CanEquip(colonist, bat, ctx), Is.True, "the control: standing");
            colonist.Downed = true;
            Assert.That(ctx.WeaponRules.CanEquip(colonist, bat, ctx), Is.False, "downed");
        }
    }
}
