#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The arithmetic of a swing (design 33 §3, §6A), through <see cref="MeleeRules"/>: the owner's
    /// two curves, the damage spread, a blunt blow's stun and a sharp one's none, and four rolls
    /// on four streams. The rates are measured over hundreds of ticks rather than asserted from one
    /// roll, and each claim carries the control that tells a working rule from a missing one.
    /// </summary>
    public class CombatMathTests
    {
        const int Trials = 4_000;

        static (ColonyWorld colony, Pawn a, Pawn b, FixedLevelRules rules) Fighters(int attackerLevel = 0, int defenderLevel = 0)
        {
            var colony = Board();
            colony.World.Tick();
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            var rules = new FixedLevelRules();
            rules.Levels[a.Id.Value] = attackerLevel;
            rules.Levels[b.Id.Value] = defenderLevel;
            colony.Pawns.MeleeRules = rules;
            return (colony, a, b, rules);
        }

        static int Count(ColonyWorld colony, IMeleeRules rules, Pawn a, Pawn b, in Armament armament, CombatEventKind kind)
        {
            int n = 0;
            for (int t = 0; t < Trials; t++)
                if (rules.Resolve(a, b, armament, colony.Pawns, 10_000 + t).Result == kind) n++;
            return n;
        }

        [Test]
        public void TheHitAndDodgeCurvesAreTheOwners()
        {
            var (colony, a, b, rules) = Fighters();
            int[] levels = { 0, 5, 10, 15, 20, 30 };
            int[] hit = { 500, 650, 800, 850, 900, 900 };
            int[] dodge = { 0, 50, 100, 200, 300, 300 };
            for (int i = 0; i < levels.Length; i++)
            {
                rules.Levels[a.Id.Value] = levels[i];
                Assert.That(rules.HitChancePerMille(a, colony.Pawns), Is.EqualTo(hit[i]), $"hit at level {levels[i]}");
                Assert.That(rules.DodgeChancePerMille(a, colony.Pawns), Is.EqualTo(dodge[i]), $"dodge at level {levels[i]}");
            }

            // A person's level is her Melee skill; an animal's is its species' figure.
            var shipped = new MeleeRules();
            SetMelee(b, 10);
            Assert.That(shipped.MeleeLevel(b), Is.EqualTo(10));
            SetMelee(b, 0);
            Assert.That(shipped.MeleeLevel(b), Is.EqualTo(0), "the control: the level follows the skill");
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, a.Cell);
            Assert.That(shipped.MeleeLevel(hog), Is.EqualTo(hog.Species.meleeSkill));
            Assert.That(shipped.HitChancePerMille(hog, colony.Pawns), Is.EqualTo(500 + 300 * hog.Species.meleeSkill / 10));

            // Lying down, nobody dodges.
            rules.Levels[b.Id.Value] = 20;
            Assert.That(rules.DodgeChancePerMille(b, colony.Pawns), Is.EqualTo(300));
            b.Downed = true;
            Assert.That(rules.DodgeChancePerMille(b, colony.Pawns), Is.EqualTo(0));
            b.Downed = false;
        }

        [TestCase(0, 500)]
        [TestCase(10, 800)]
        [TestCase(20, 900)]
        public void ASwingLandsAtTheAttackersRate(int level, int perMille)
        {
            var (colony, a, b, rules) = Fighters(attackerLevel: level);
            int hits = Count(colony, rules, a, b, Fists(colony.Pawns), CombatEventKind.Hit);
            Assert.That(hits * 1_000 / Trials, Is.EqualTo(perMille).Within(30));
        }

        /// <summary>
        /// Dodge is by the defender's level and only of a swing that would have landed: at level 20
        /// a fifth of the attacker's half becomes a dodge — 150 in 1,000 — and the misses stay at
        /// half. The control is the same attacker against a level-0 defender, who dodges nothing.
        /// </summary>
        [Test]
        public void ADodgeIsTheDefendersAndOnlyOfASwingThatWouldHaveLanded()
        {
            var (colony, a, b, rules) = Fighters(attackerLevel: 0, defenderLevel: 20);
            Armament fists = Fists(colony.Pawns);
            int dodged = Count(colony, rules, a, b, fists, CombatEventKind.Dodge);
            int missed = Count(colony, rules, a, b, fists, CombatEventKind.Miss);
            Assert.That(dodged * 1_000 / Trials, Is.EqualTo(150).Within(25));
            Assert.That(missed * 1_000 / Trials, Is.EqualTo(500).Within(30), "a dodge ate into the misses");

            rules.Levels[b.Id.Value] = 0;
            Assert.That(Count(colony, rules, a, b, fists, CombatEventKind.Dodge), Is.EqualTo(0),
                "the control: a level-0 defender dodges nothing");
        }

        /// <summary>The figure give or take a fifth, both ends reached, nothing outside.</summary>
        [Test]
        public void DamageStaysInsideTheSpreadAndReachesBothEnds()
        {
            var (colony, a, b, rules) = Fighters(attackerLevel: 20);
            Armament machete = Weapon(colony.Pawns, ItemHandle.Machete);
            int figure = machete.Attack.damage * 1_000;
            int low = int.MaxValue, high = int.MinValue, landed = 0;
            for (int t = 0; t < Trials; t++)
            {
                SwingOutcome o = rules.Resolve(a, b, machete, colony.Pawns, 10_000 + t);
                // A critical is half as much again (design 33 §9b), and CriticalsLandAtTheirRate owns it.
                if (!o.Landed || o.Critical) continue;
                landed++;
                if (o.DamageMilli < low) low = o.DamageMilli;
                if (o.DamageMilli > high) high = o.DamageMilli;
            }
            Assert.That(landed, Is.GreaterThan(Trials / 2));
            Assert.That(low, Is.GreaterThanOrEqualTo(figure * 8 / 10));
            Assert.That(high, Is.LessThanOrEqualTo(figure * 12 / 10));
            Assert.That(low, Is.LessThan(figure * 82 / 100), "the spread never reached its low end");
            Assert.That(high, Is.GreaterThan(figure * 118 / 100), "the spread never reached its high end");
        }

        /// <summary>
        /// A blunt blow stuns at its weapon's chance, for its weapon's ticks; a sharp one never does,
        /// even given a certain chance — the control that the kind, not the number, decides.
        /// </summary>
        [Test]
        public void ABluntBlowStunsAndASharpOneNever()
        {
            var (colony, a, b, rules) = Fighters(attackerLevel: 20);
            Armament bat = Weapon(colony.Pawns, ItemHandle.Bat);
            int landed = 0, stunned = 0;
            for (int t = 0; t < Trials; t++)
            {
                SwingOutcome o = rules.Resolve(a, b, bat, colony.Pawns, 10_000 + t);
                if (!o.Landed) continue;
                landed++;
                if (o.StunTicks == 0) continue;
                stunned++;
                Assert.That(o.StunTicks, Is.EqualTo(bat.Attack.stunTicks));
            }
            Assert.That(stunned * 1_000 / landed, Is.EqualTo(bat.Attack.stunPerMille).Within(30));

            var certainSharp = new AttackDef { damage = 8, damageKind = DamageKind.Sharp, stunPerMille = 1_000, stunTicks = 90 };
            var certainBlunt = new AttackDef { damage = 8, damageKind = DamageKind.Blunt, stunPerMille = 1_000, stunTicks = 90 };
            int sharpStuns = 0, bluntStuns = 0;
            for (int t = 0; t < Trials; t++)
            {
                if (rules.Resolve(a, b, new Armament(certainSharp), colony.Pawns, 10_000 + t).StunTicks > 0) sharpStuns++;
                SwingOutcome blunt = rules.Resolve(a, b, new Armament(certainBlunt), colony.Pawns, 10_000 + t);
                if (blunt.Landed && blunt.StunTicks == 0) Assert.Fail("a certain blunt stun did not stun");
                if (blunt.StunTicks > 0) bluntStuns++;
            }
            Assert.That(sharpStuns, Is.EqualTo(0), "a sharp blow stunned");
            Assert.That(bluntStuns, Is.GreaterThan(0), "the control: the same numbers, blunt, stun");

            Assert.That(Count(colony, rules, a, b, Weapon(colony.Pawns, ItemHandle.Machete), CombatEventKind.Hit), Is.GreaterThan(0));
            for (int t = 0; t < Trials; t++)
                Assert.That(rules.Resolve(a, b, Fists(colony.Pawns), colony.Pawns, 10_000 + t).StunTicks, Is.EqualTo(0),
                    "fists carry no stun chance");
        }

        /// <summary>
        /// Four rolls, four streams: changing the dodge curve's input moves no hit roll, and a
        /// blow that lands either way lands for the same damage. The control is a second attacker,
        /// salted differently, whose misses fall on other ticks.
        /// </summary>
        [Test]
        public void EachRollIsItsOwnStream()
        {
            var (colony, a, b, rules) = Fighters(attackerLevel: 0, defenderLevel: 0);
            Armament fists = Fists(colony.Pawns);
            var whole = new SwingOutcome[Trials];
            for (int t = 0; t < Trials; t++) whole[t] = rules.Resolve(a, b, fists, colony.Pawns, 10_000 + t);

            rules.Levels[b.Id.Value] = 20;
            int bothLanded = 0;
            for (int t = 0; t < Trials; t++)
            {
                SwingOutcome o = rules.Resolve(a, b, fists, colony.Pawns, 10_000 + t);
                Assert.That(o.Result == CombatEventKind.Miss, Is.EqualTo(whole[t].Result == CombatEventKind.Miss),
                    $"tick {t}: the defender's level moved the attacker's hit roll");
                if (!o.Landed || !whole[t].Landed) continue;
                bothLanded++;
                Assert.That(o.DamageMilli, Is.EqualTo(whole[t].DamageMilli), $"tick {t}: the dodge moved the damage");
            }
            Assert.That(bothLanded, Is.GreaterThan(Trials / 4));

            // Salted by the attacker: the other colonist swinging on the same ticks misses on others.
            int differ = 0;
            for (int t = 0; t < Trials; t++)
                if ((rules.Resolve(b, a, fists, colony.Pawns, 10_000 + t).Result == CombatEventKind.Miss)
                    != (whole[t].Result == CombatEventKind.Miss)) differ++;
            Assert.That(differ, Is.GreaterThan(Trials / 4), "the control: two attackers share a stream");
        }

        [Test]
        public void TheSameSwingOnTheSameTickComesOutTheSame()
        {
            var (colony, a, b, rules) = Fighters(attackerLevel: 7, defenderLevel: 12);
            Armament bat = Weapon(colony.Pawns, ItemHandle.Bat);
            for (int t = 0; t < 200; t++)
            {
                SwingOutcome x = rules.Resolve(a, b, bat, colony.Pawns, 5_000 + t);
                SwingOutcome y = rules.Resolve(a, b, bat, colony.Pawns, 5_000 + t);
                Assert.That((x.Result, x.DamageMilli, x.StunTicks), Is.EqualTo((y.Result, y.DamageMilli, y.StunTicks)));
            }
        }
    }
}
