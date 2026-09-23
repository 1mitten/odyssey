#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// <c>Job_AttackMelee</c> and the order that starts it (design 33 §3, §6A): the chase, the
    /// swing clock, the wind-up the figure turns through, the weapon on every report, the stun
    /// that loses a swing in the air, the drafted hold's blow that never chases, and a save taken
    /// with a swing in the air.
    ///
    /// <para>The duel is two drafted colonists four cells apart, A ordered at B. B's hold answers
    /// A's blows without moving (a threat beside her), so both stand still once A arrives and the
    /// swing clock is the only thing that times A's blows.</para>
    /// </summary>
    public class AttackDriverTests
    {
        static (ColonyWorld colony, Pawn a, Pawn b, RecordingRules rules) Duel(uint seed = 7u, bool order = true)
        {
            var colony = Board(seed: seed);
            colony.World.Tick();
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            Stand(colony, a, Near(colony, 0, 0));
            Stand(colony, b, Near(colony, 4, 0));
            Assert.That(Draft(colony, a), Is.EqualTo(IntentRejection.None));
            Assert.That(Draft(colony, b), Is.EqualTo(IntentRejection.None));
            var rules = new RecordingRules();
            colony.Pawns.MeleeRules = rules;
            if (order) Assert.That(Attack(colony, a, b), Is.EqualTo(IntentRejection.None));
            return (colony, a, b, rules);
        }

        static void TickUntil(ColonyWorld colony, System.Func<bool> done, int limit, string what)
        {
            for (int t = 0; t < limit && !done(); t++) colony.World.Tick();
            Assert.That(done(), Is.True, what);
        }

        static AttackMeleeJobDriver? Swing(Pawn pawn) => pawn.Driver as AttackMeleeJobDriver;

        [Test]
        public void TheOrderedColonistClosesAndSwings()
        {
            var (colony, a, b, rules) = Duel();
            Assert.That(a.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee));
            Assert.That(a.CurrentJob!.PlayerForced, Is.True);
            Assert.That(a.CombatTarget, Is.EqualTo(b.Id.Value));
            Assert.That(Melee.InReach(colony.Pawns, a, b, TraverseMode.Colonist), Is.False, "the control: she starts out of reach");

            byte serial = a.GestureSerial;
            TickUntil(colony, () => rules.TicksOf(a).Count >= 2, 1_500, "she never swung twice");

            Assert.That(Melee.InReach(colony.Pawns, a, b, TraverseMode.Colonist), Is.True);
            Assert.That(a.GestureSerial, Is.Not.EqualTo(serial), "no gesture reported");
            Assert.That(a.Gesture, Is.EqualTo(PawnGesture.Strike));
            Assert.That(b.Cell, Is.EqualTo(Near(colony, 4, 0)), "the held colonist moved");
        }

        /// <summary>Fists come round every 120 ticks, exactly, while the two stand in reach.</summary>
        [Test]
        public void ASwingComesRoundOnTheCooldown()
        {
            var (colony, a, _, rules) = Duel();
            TickUntil(colony, () => rules.TicksOf(a).Count >= 5, 2_000, "five swings never came");
            var ticks = rules.TicksOf(a);
            int cooldown = colony.Pawns.Content.Combat.fists.cooldownTicks;
            for (int i = 1; i < 5; i++)
                Assert.That(ticks[i] - ticks[i - 1], Is.EqualTo(cooldown), $"swing {i}");
        }

        /// <summary>
        /// The clock is the pawn's, not the job's (design 33 §3): an order given again straight after
        /// a swing starts a new job and the next swing still waits out the cooldown. The control is
        /// the same board with the clock cleared at the re-order, which swings early — what a clock
        /// on the job would have done.
        /// </summary>
        [Test]
        public void ANewOrderDoesNotResetTheSwingClock()
        {
            int Gap(bool clearTheClock)
            {
                var (colony, a, b, rules) = Duel();
                TickUntil(colony, () => rules.TicksOf(a).Count >= 1, 1_500, "no first swing");
                int first = rules.TicksOf(a)[0];
                colony.World.Tick(10);
                Assert.That(Attack(colony, a, b), Is.EqualTo(IntentRejection.None));
                if (clearTheClock) a.NextSwingTick = 0;
                TickUntil(colony, () => rules.TicksOf(a).Count >= 2, 1_500, "no second swing");
                return rules.TicksOf(a)[1] - first;
            }

            int cooldown = Board().Pawns.Content.Combat.fists.cooldownTicks;
            Assert.That(Gap(clearTheClock: false), Is.GreaterThanOrEqualTo(cooldown));
            Assert.That(Gap(clearTheClock: true), Is.LessThan(cooldown), "the control: a reset clock swings early");
        }

        /// <summary>The figure turns to the target during the wind-up, and at no other time.</summary>
        [Test]
        public void WorkFocusIsTheTargetsCellInTheWindupAndNothingAfter()
        {
            var (colony, a, b, rules) = Duel();
            int windup = 0, ready = 0;
            for (int t = 0; t < 1_500; t++)
            {
                colony.World.Tick();
                var driver = Swing(a);
                if (driver == null) continue;
                if (driver.InWindup)
                {
                    windup++;
                    Assert.That(driver.WorkFocus, Is.EqualTo(b.Cell), $"tick {t}: the wind-up faces somewhere else");
                }
                else
                {
                    ready++;
                    Assert.That(driver.WorkFocus, Is.EqualTo(-1), $"tick {t}: a focus outside the wind-up");
                }
            }
            Assert.That(rules.TicksOf(a).Count, Is.GreaterThan(3));
            Assert.That(windup, Is.GreaterThan(0));
            Assert.That(ready, Is.GreaterThan(windup), "the control: most of a fight is not a wind-up");
        }

        /// <summary>
        /// Every moment of a swing is told with what it was swung with (design 33 §5j), because
        /// presentation picks the clip family from it: a bat in A's hand, and B's bare fists as the
        /// control, reported as −1.
        /// </summary>
        [Test]
        public void EveryReportCarriesTheWeapon()
        {
            var (colony, a, b, _) = Duel(order: false);
            colony.Pawns.WeaponRules = new HeldWeapon().Give(a, ItemHandle.Bat);
            Assert.That(Attack(colony, a, b), Is.EqualTo(IntentRejection.None));

            var tape = new Tape();
            tape.Tick(colony, 3_000);

            var byA = tape.Events.Where(e => e.Attacker == a.Id).ToList();
            var byB = tape.Events.Where(e => e.Attacker == b.Id).ToList();
            Assert.That(byA.Count(e => e.Kind == CombatEventKind.Swing), Is.GreaterThan(3));
            Assert.That(byA.Count(e => e.Kind == CombatEventKind.Hit), Is.GreaterThan(0));
            Assert.That(byA.Count(e => e.Kind == CombatEventKind.Stun), Is.GreaterThan(0), "a bat never stunned in 3,000 ticks");
            Assert.That(byA.All(e => e.Weapon == ItemHandle.Bat), Is.True, "a report of A's without the bat");
            Assert.That(byB.Count, Is.GreaterThan(0));
            Assert.That(byB.All(e => e.Weapon == -1), Is.True, "the control: fists are reported as -1");

            // The swing's report says how long its wind-up is, and where it is aimed.
            var swing = byA.First(e => e.Kind == CombatEventKind.Swing);
            Assert.That(swing.Amount, Is.EqualTo(colony.Pawns.Content.Items[ItemHandle.Bat].weapon!.windupTicks));
            Assert.That(swing.Target, Is.EqualTo(b.Id));
        }

        /// <summary>
        /// A swing whose attacker is stunned before it lands is lost (design 33 §5j), not paused:
        /// every blow A lands after the stun is one she started after it. Without the rule the stun
        /// pauses the wind-up with the job, and the old swing lands the moment the stun wears off —
        /// a resolve with no swing of its own, which the count below would see.
        /// </summary>
        [Test]
        public void AStunnedAttackersSwingDoesNotLand()
        {
            var (colony, a, _, rules) = Duel();
            TickUntil(colony, () => Swing(a) is { InWindup: true } s && s.ToilProgress > 0, 1_500, "no wind-up");

            int stunnedAt = colony.World.CurrentTick;
            a.StunnedUntilTick = stunnedAt + 100;
            var tape = new Tape();
            tape.Tick(colony, 600);

            // The swings begun after the stun that have had time to land, against the blows that did.
            int windup = colony.Pawns.Content.Combat.fists.windupTicks;
            int last = colony.World.CurrentTick - 1;
            var swingsAfter = tape.By(a, CombatEventKind.Swing).Where(e => e.Tick >= stunnedAt && e.Tick + windup <= last)
                .Select(e => e.Tick).ToList();
            var landedAfter = rules.TicksOf(a).Where(t => t >= stunnedAt).ToList();
            Assert.That(landedAfter.Count, Is.GreaterThan(0), "she never swung again after the stun");
            Assert.That(landedAfter.Count, Is.EqualTo(swingsAfter.Count), "a swing began before the stun landed after it");
            Assert.That(landedAfter[0], Is.EqualTo(swingsAfter[0] + windup));
            Assert.That(swingsAfter[0], Is.GreaterThanOrEqualTo(stunnedAt + 100), "she swung while stunned");
        }

        [Test]
        public void TheAttackOrderIsForADraftedColonistAndAPawn()
        {
            var (colony, a, b, _) = Duel(order: false);
            Pawn c = Spawn(colony, 0, Near(colony, 8, 0));
            colony.World.Tick();

            Assert.That(Attack(colony, c, b), Is.EqualTo(IntentRejection.NotPermitted), "an undrafted colonist took an attack order");
            Assert.That(Send(colony, new Intent(IntentKind.OrderAttack, Size.FromIndex(b.Cell), a.Id.Value, 0)),
                Is.EqualTo(IntentRejection.NotPermitted), "a building is C6's");
            Assert.That(Attack(colony, a, a), Is.EqualTo(IntentRejection.NotPermitted), "she attacked herself");
            Assert.That(Send(colony, new Intent(IntentKind.OrderAttack, default, a.Id.Value, 9_999)),
                Is.EqualTo(IntentRejection.NotPermitted), "nobody by that id");
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 12, 0));
            Assert.That(Send(colony, new Intent(IntentKind.OrderAttack, default, marauder.Id.Value, a.Id.Value)),
                Is.EqualTo(IntentRejection.NotPermitted), "a marauder took an order");

            Assert.That(Attack(colony, a, b), Is.EqualTo(IntentRejection.None), "the control: drafted, at a colonist");
            Assert.That(a.CombatTarget, Is.EqualTo(b.Id.Value));
            Assert.That(Attack(colony, a, marauder), Is.EqualTo(IntentRejection.None), "at a marauder");
            Assert.That(a.CombatTarget, Is.EqualTo(marauder.Id.Value), "the second order did not take the target");
        }

        /// <summary>
        /// A drafted colonist hits a hostile beside her without an order, and does not follow it
        /// (design 33 §1): she strikes from where she stands, and when it is carried away she holds
        /// again. The control is the same colonist given the order, who does follow.
        /// </summary>
        [Test]
        public void TheHoldStrikesAnAdjacentHostileAndNeverChases()
        {
            var (colony, a, b, rules) = Duel(order: false);
            Stand(colony, b, Near(colony, -20, -20));
            int home = a.Cell;
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 1, 0));

            // The moment she takes it on, it is carried off — so her blow is a young job, which
            // the hunt's re-choosing would not yet end, and only the hold's own rule keeps her.
            TickUntil(colony, () => a.CurrentJob?.DefIndex == JobIndex.AttackMelee, 10, "she never took the marauder on");
            Assert.That(a.CurrentJob!.PlayerForced, Is.False, "nobody ordered it");
            Stand(colony, marauder, Near(colony, 10, 10));
            marauder.StunnedUntilTick = colony.World.CurrentTick + 400;
            colony.World.Tick(200);
            Assert.That(a.Cell, Is.EqualTo(home), "she chased a threat nobody ordered her at");
            Assert.That(a.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.DraftHold));

            // Back beside her: she strikes from where she stands.
            marauder.StunnedUntilTick = 0;
            Stand(colony, marauder, Near(colony, 1, 0));
            for (int t = 0; t < 600; t++)
            {
                colony.World.Tick();
                Assert.That(a.Cell, Is.EqualTo(home), $"tick {t}: the hold moved");
            }
            Assert.That(rules.TicksOf(a).Count, Is.GreaterThan(0), "she never struck the marauder beside her");
            Assert.That(a.Drafted, Is.True);

            // The control: ordered, she follows.
            Stand(colony, marauder, Near(colony, 10, 10));
            marauder.StunnedUntilTick = colony.World.CurrentTick + 400;
            Assert.That(Attack(colony, a, marauder), Is.EqualTo(IntentRejection.None));
            colony.World.Tick(200);
            Assert.That(a.Cell, Is.Not.EqualTo(home), "the control: ordered, she follows");
        }

        /// <summary>
        /// A save taken with a swing in the air resumes on the same ticks (design 33 §6A): the same
        /// hash on load and 600 ticks later, blows landing in between. Everything the driver knows is
        /// saved — the toil and its progress, the job's cells, the pawn's clock and target.
        /// </summary>
        [Test]
        public void ASaveTakenMidSwingResumesTheSame()
        {
            var (colony, a, b, rules) = Duel();
            TickUntil(colony, () => Swing(a) is { InWindup: true } s && s.ToilProgress > 2_000, 1_500, "no wind-up");
            int progress = Swing(a)!.ToilProgress;

            byte[] saved = colony.Save();
            var restored = Board();
            restored.Load(saved);

            // The control: the same save with the wind-up forgotten — what a driver keeping its
            // swing anywhere but the saved toil would load as — parts from the original.
            var forgetful = Board();
            forgetful.Load(saved);
            Swing(forgetful.Pawns.Pawns.Get(a.Id)!)!.EndSwing();
            Pawn back = restored.Pawns.Pawns.Get(a.Id)!;
            Assert.That(Swing(back), Is.Not.Null);
            Assert.That(Swing(back)!.InWindup, Is.True);
            Assert.That(Swing(back)!.ToilProgress, Is.EqualTo(progress));
            Assert.That(back.CombatTarget, Is.EqualTo(b.Id.Value));
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));

            int before = rules.Swings.Count;
            colony.World.Tick(600);
            restored.World.Tick(600);
            forgetful.World.Tick(600);
            Assert.That(forgetful.World.ComputeStateHash().Value, Is.Not.EqualTo(colony.World.ComputeStateHash().Value),
                "the control: a lost wind-up resumes the same, so this test could not see one");
            Assert.That(rules.Swings.Count - before, Is.GreaterThan(4), "the control: 600 ticks of fighting");
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value),
                "the fight resumed differently");
            Assert.That(restored.Pawns.Pawns.Get(b.Id)!.HpMilli, Is.EqualTo(b.HpMilli));
        }
    }
}
