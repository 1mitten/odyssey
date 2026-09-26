#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Weapon quality (design 47 §11; owner, 2026-09-25: <i>"give the guns quality like you do with
    /// beds (and apply this to all weapons) depending on the spawn/who crafted them"</i>): the beds'
    /// tiers and roll, carried on the item, scaling a weapon's damage and hit chance.
    /// </summary>
    public class WeaponQualityTests
    {
        static ColonyWorld Colony()
        {
            var colony = Board();
            colony.World.Tick();
            return colony;
        }

        static ColonyItem? Grant(ColonyWorld colony, int def)
        {
            int before = colony.Pawns.Items.Items.Count;
            Assert.That(Send(colony, new Intent(IntentKind.GiveResource, Size.FromIndex(Near(colony, 4, 4)), def, 1)),
                Is.EqualTo(IntentRejection.None));
            var items = colony.Pawns.Items.Items;
            return items.Count > before ? items[items.Count - 1] : null;
        }

        [Test]
        public void AGrantedWeaponHasATierAndWoodHasNone()
        {
            var colony = Colony();
            ColonyItem? pistol = Grant(colony, ItemIndex.Pistol);
            ColonyItem? machete = Grant(colony, ItemIndex.Machete);
            ColonyItem? wood = Grant(colony, ItemIndex.Wood);
            Assert.That(QualityContent.IsQuality(pistol!.Quality), Is.True, "every weapon, the gun among them");
            Assert.That(QualityContent.IsQuality(machete!.Quality), Is.True, "and every melee weapon");
            Assert.That(wood!.Quality, Is.EqualTo(0), "the control: wood takes no quality");
        }

        /// <summary>The tier is the thing's own for life: rolling again on the same thing changes nothing.</summary>
        [Test]
        public void TheTierIsRolledOnceAndKept()
        {
            var colony = Colony();
            ColonyItem pistol = Grant(colony, ItemIndex.Pistol)!;
            byte first = pistol.Quality;
            colony.World.Tick(50);
            WeaponQuality.Assign(colony.Pawns, pistol, 20);
            Assert.That(pistol.Quality, Is.EqualTo(first));
        }

        /// <summary>
        /// Who made it (design 47 §11): a bandit's own gear is poorer than a find. Over a hundred of each
        /// the mean tier of a find is higher; neither ever rolls outside the tiers.
        /// </summary>
        [Test]
        public void ABanditsGearIsPoorerThanAFind()
        {
            var colony = Colony();
            int found = 0, bandit = 0;
            for (int i = 0; i < 100; i++)
            {
                var a = new ColonyItem { Id = new ThingId(1_000 + i), DefIndex = ItemIndex.Pistol };
                var b = new ColonyItem { Id = new ThingId(1_000 + i), DefIndex = ItemIndex.Pistol };
                WeaponQuality.Assign(colony.Pawns, a, WeaponQuality.FoundSkill);
                WeaponQuality.Assign(colony.Pawns, b, WeaponQuality.BanditSkill);
                Assert.That(QualityContent.IsQuality(a.Quality) && QualityContent.IsQuality(b.Quality), Is.True);
                found += a.Quality;
                bandit += b.Quality;
            }
            Assert.That(found, Is.GreaterThan(bandit), $"finds {found / 100.0:F2}, bandit gear {bandit / 100.0:F2}");

            Pawn raider = colony.Pawns.Pawns.Spawn(Near(colony, 6, 0), PawnKindIndex.Bandit);
            ColonyItem? held = WeaponHand.Held(raider, colony.Pawns);
            Assert.That(held, Is.Not.Null);
            Assert.That(QualityContent.IsQuality(held!.Quality), Is.True, "a bandit arrives with its gear's tier");
        }

        /// <summary>
        /// What a tier does: an epic pistol hits more often and harder than a poor one, a normal one is
        /// the Def's own weapon, and a weapon with no tier fights as a normal one.
        /// </summary>
        [Test]
        public void ATierMovesTheHitChanceAndTheDamage()
        {
            var colony = Colony();
            Pawn shooter = colony.Pawns.Pawns.All[0];
            AttackDef gun = colony.Pawns.Content.Items[ItemIndex.Pistol].weapon!;
            var rules = new RangedRules();
            int At(byte tier) => rules.HitChancePerMille(shooter, 12_500, new Armament(gun, ItemIndex.Pistol, tier), colony.Pawns);

            Assert.That(At(QualityHandle.Epic), Is.GreaterThan(At(QualityHandle.Normal)));
            Assert.That(At(QualityHandle.Normal), Is.GreaterThan(At(QualityHandle.Poor)));
            Assert.That(At(0), Is.EqualTo(At(QualityHandle.Normal)), "no tier fights as normal");

            Assert.That(WeaponQuality.Damage(10_000, new Armament(gun, ItemIndex.Pistol, QualityHandle.Epic)), Is.EqualTo(13_500));
            Assert.That(WeaponQuality.Damage(10_000, new Armament(gun, ItemIndex.Pistol, QualityHandle.Poor)), Is.EqualTo(9_000));
            Assert.That(WeaponQuality.Damage(10_000, new Armament(gun, ItemIndex.Pistol, QualityHandle.Normal)), Is.EqualTo(10_000));
            Assert.That(WeaponQuality.Damage(10_000, new Armament(colony.Pawns.Content.Combat.fists)), Is.EqualTo(10_000),
                "bare hands have no tier to scale");
        }

        /// <summary>The hand carries the held weapon's tier into the fight.</summary>
        [Test]
        public void TheArmamentCarriesTheHeldWeaponsTier()
        {
            var colony = Colony();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            ColonyItem pistol = Grant(colony, ItemIndex.Pistol)!;
            WeaponHand.TakeUp(colonist, pistol, colony.Pawns);
            Armament armed = colony.Pawns.WeaponRules.ArmamentOf(colonist, colony.Pawns);
            Assert.That(armed.ItemDef, Is.EqualTo(ItemIndex.Pistol));
            Assert.That(armed.Quality, Is.EqualTo(pistol.Quality));
        }

        [Test]
        public void TheTierSurvivesASave()
        {
            var colony = Colony();
            ColonyItem pistol = Grant(colony, ItemIndex.Pistol)!;
            pistol.Quality = QualityHandle.Uber;
            var restored = Board();
            restored.Load(colony.Save());
            Assert.That(restored.Pawns.Items.Get(pistol.Id)!.Quality, Is.EqualTo(QualityHandle.Uber));
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));
        }
    }
}
