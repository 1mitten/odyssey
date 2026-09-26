#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Critical blows (design 33 §9b) and the swing decided when it starts (§9g). The owner's
    /// numbers: 10 % plus 1 % per four attacker Melee levels, half as much damage again, and a
    /// knockback on half of them — three in four for a blunt weapon. Rates are measured over
    /// thousands of rolls, each with the control that tells a working rule from a missing one.
    /// </summary>
    public class CriticalTests
    {
        const int Trials = 20_000;

        static (ColonyWorld colony, Pawn a, Pawn b, FixedLevelRules rules) Fighters(int attackerLevel)
        {
            var colony = Board();
            colony.World.Tick();
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            var rules = new FixedLevelRules();
            rules.Levels[a.Id.Value] = attackerLevel;
            rules.Levels[b.Id.Value] = 0;
            colony.Pawns.MeleeRules = rules;
            return (colony, a, b, rules);
        }

        /// <summary>The shipped rules with the critical taken away: the same blow, never critical.</summary>
        sealed class NoCriticals : MeleeRules
        {
            public override int CriticalChancePerMille(Pawn attacker, PawnContext ctx) => 0;
        }

        [Test]
        public void TheCriticalChanceIsTheOwners()
        {
            var (colony, a, _, rules) = Fighters(0);
            int[] levels = { 0, 3, 4, 7, 8, 20, 30 };
            int[] chance = { 100, 100, 110, 110, 120, 150, 170 };
            for (int i = 0; i < levels.Length; i++)
            {
                rules.Levels[a.Id.Value] = levels[i];
                Assert.That(rules.CriticalChancePerMille(a, colony.Pawns), Is.EqualTo(chance[i]), $"level {levels[i]}");
            }

            // An animal's level is its species' meleeSkill.
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, a.Cell);
            Assert.That(new MeleeRules().CriticalChancePerMille(hog, colony.Pawns),
                Is.EqualTo(colony.Pawns.Content.Combat.CritChancePerMille(hog.Species.meleeSkill)));
        }

        /// <summary>
        /// Among the blows that land, the critical share is the owner's chance at the attacker's
        /// level. Rolled on its own stream: on the hit roll's stream the share at level 0 would be
        /// the critical chance over the hit chance, 200 in a thousand, not 100 (measured).
        /// </summary>
        [TestCase(0, 100)]
        [TestCase(20, 150)]
        public void CriticalsLandAtTheirRate(int level, int perMille)
        {
            var (colony, a, b, rules) = Fighters(level);
            Armament machete = Weapon(colony.Pawns, ItemHandle.Machete);
            int landed = 0, critical = 0;
            for (int t = 0; t < Trials; t++)
            {
                SwingOutcome o = rules.Resolve(a, b, machete, colony.Pawns, 10_000 + t);
                if (!o.Landed)
                {
                    Assert.That(o.Critical || o.Knockback, Is.False, "a blow that missed was critical");
                    continue;
                }
                landed++;
                if (o.Critical) critical++;
            }
            int measured = (int)((long)critical * 1_000 / landed);
            Assert.That(measured, Is.InRange(perMille - 20, perMille + 20), $"{critical} criticals in {landed} landed blows");
        }

        /// <summary>
        /// A critical is the same blow half as much again: the same hit, dodge, damage and stun
        /// rolls as the rules with no criticals at all give on the same tick, and the damage times
        /// 1.5. So adding the critical moved nothing else a swing rolls.
        /// </summary>
        [Test]
        public void ACriticalIsTheSameBlowHalfAsMuchAgain()
        {
            var (colony, a, b, _) = Fighters(20);
            SetMelee(a, 20);
            SetMelee(b, 0);
            var rules = new MeleeRules();
            var plain = new NoCriticals();
            Armament bat = Weapon(colony.Pawns, ItemHandle.Bat);
            int criticals = 0;
            for (int t = 0; t < Trials; t++)
            {
                SwingOutcome o = rules.Resolve(a, b, bat, colony.Pawns, 10_000 + t);
                SwingOutcome n = plain.Resolve(a, b, bat, colony.Pawns, 10_000 + t);
                Assert.That(o.Result, Is.EqualTo(n.Result), $"tick {t}: the critical moved the hit or the dodge");
                Assert.That(o.StunTicks, Is.EqualTo(n.StunTicks), $"tick {t}: the critical moved the stun");
                Assert.That(n.Critical, Is.False, "the control: no criticals");
                if (!o.Critical)
                {
                    Assert.That(o.DamageMilli, Is.EqualTo(n.DamageMilli));
                    continue;
                }
                criticals++;
                Assert.That(o.DamageMilli, Is.EqualTo(n.DamageMilli * 1_500 / 1_000), $"tick {t}: not half as much again");
            }
            Assert.That(criticals, Is.GreaterThan(Trials / 20), "the control: criticals happened");
        }

        /// <summary>
        /// A critical knocks back on half of them with an edge, three in four with a blunt weapon,
        /// and a blow that is not critical never does.
        /// </summary>
        [Test]
        public void AKnockbackIsSurerWithABluntWeapon()
        {
            var (colony, a, b, rules) = Fighters(20);
            int Rate(int item)
            {
                Armament weapon = Weapon(colony.Pawns, item);
                int criticals = 0, knocks = 0;
                for (int t = 0; t < Trials; t++)
                {
                    SwingOutcome o = rules.Resolve(a, b, weapon, colony.Pawns, 10_000 + t);
                    if (!o.Critical)
                    {
                        Assert.That(o.Knockback, Is.False, "a blow that was not critical knocked back");
                        continue;
                    }
                    criticals++;
                    if (o.Knockback) knocks++;
                }
                Assert.That(criticals, Is.GreaterThan(1_000));
                return (int)((long)knocks * 1_000 / criticals);
            }

            int sharp = Rate(ItemHandle.Machete), blunt = Rate(ItemHandle.Bat);
            Assert.That(sharp, Is.InRange(460, 540), "a machete's knockbacks");
            Assert.That(blunt, Is.InRange(710, 790), "a bat's knockbacks");
        }

        /// <summary>
        /// The swing is decided when its wind-up begins (design 33 §9g; owner, 2026-09-23: a sharp
        /// critical's slice is heard during the swing): a critical that will land is announced as
        /// <see cref="CombatEventKind.SwingCritical"/> instead of <see cref="CombatEventKind.Swing"/>,
        /// and every <c>Critical</c> at an impact follows a <c>SwingCritical</c> one wind-up
        /// earlier, with the damage the rules gave at the start. With the outcome rolled again at
        /// the impact — what the resolver did before — the two disagree (measured).
        /// </summary>
        [Test]
        public void ACriticalIsAnnouncedWhenItsSwingBeginsAndLandsAsAnnounced()
        {
            var colony = Board();
            colony.World.Tick();
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            SetMelee(a, 20);
            Stand(colony, a, Near(colony, 0, 0));
            Stand(colony, b, Near(colony, 3, 0));
            Assert.That(Draft(colony, a), Is.EqualTo(IntentRejection.None));
            Assert.That(Draft(colony, b), Is.EqualTo(IntentRejection.None));
            colony.Pawns.WeaponRules = new HeldWeapon().Give(a, ItemHandle.Machete);
            var rules = new RecordingOutcomes();
            colony.Pawns.MeleeRules = rules;
            Assert.That(Attack(colony, a, b), Is.EqualTo(IntentRejection.None));

            var tape = new Tape();
            int windup = colony.Pawns.Content.Items[ItemHandle.Machete].weapon!.windupTicks;
            int announced = 0, landedAsAnnounced = 0;
            for (int round = 0; round < 12; round++)
            {
                tape.Tick(colony, 1_000);
                // Keep her at it: stand the target up whole whenever it goes down.
                if (b.Downed) colony.Pawns.Combat!.Recover(b, colony.World.CurrentTick);
                MakeWhole(b);
                if (a.CurrentJob?.DefIndex != JobIndex.AttackMelee) Attack(colony, a, b);
            }

            var byA = tape.Events.Where(e => e.Attacker == a.Id).ToList();
            foreach (CombatEventView start in byA.Where(e => e.Kind == CombatEventKind.SwingCritical))
            {
                announced++;
                Assert.That(rules.Decided.TryGetValue(start.Tick, out SwingOutcome decided), Is.True);
                Assert.That(decided.Landed && decided.Critical, Is.True, $"tick {start.Tick}: announced a critical the rules did not give");
                var hit = byA.FirstOrDefault(e => e.Tick == start.Tick + windup && e.Kind == CombatEventKind.Hit);
                if (hit.Id == 0) continue;
                landedAsAnnounced++;
                Assert.That(hit.Amount, Is.EqualTo(decided.DamageMilli), $"tick {hit.Tick}: the blow was not the one decided");
                Assert.That(byA.Any(e => e.Tick == hit.Tick && e.Kind == CombatEventKind.Critical && e.Id == hit.Id + 1), Is.True,
                    $"tick {hit.Tick}: the announced critical landed without its Critical straight after the Hit");
            }
            foreach (CombatEventView crit in byA.Where(e => e.Kind == CombatEventKind.Critical))
                Assert.That(byA.Any(e => e.Kind == CombatEventKind.SwingCritical && e.Tick == crit.Tick - windup), Is.True,
                    $"tick {crit.Tick}: a critical landed that was never announced");
            foreach (CombatEventView start in byA.Where(e => e.Kind == CombatEventKind.Swing))
                Assert.That(rules.Decided[start.Tick].Landed && rules.Decided[start.Tick].Critical, Is.False,
                    $"tick {start.Tick}: a critical announced as a plain swing");

            Assert.That(announced, Is.GreaterThan(3), "the control: no critical was ever announced");
            Assert.That(landedAsAnnounced, Is.GreaterThan(3), "the control: no announced critical ever landed");
        }

        /// <summary>The shipped rules, keeping what they decided for each tick a swing began.</summary>
        sealed class RecordingOutcomes : MeleeRules
        {
            public readonly Dictionary<int, SwingOutcome> Decided = new Dictionary<int, SwingOutcome>();

            public override SwingOutcome Resolve(Pawn attacker, Pawn defender, in Armament armament, PawnContext ctx, int tick)
            {
                SwingOutcome o = base.Resolve(attacker, defender, armament, ctx, tick);
                if (attacker.IsColonist && armament.Armed) Decided[tick] = o;
                return o;
            }
        }
    }
}
