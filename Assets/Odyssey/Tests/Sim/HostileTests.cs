#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The marauder's hunt and a colonist's self-defence (design 33 §1, §6A): the nearest standing
    /// colonist is hunted and a downed one is not; a colonist struck by a hostile, or by a colonist,
    /// stops what she is doing and fights back; with nobody left standing the marauder idles.
    /// </summary>
    public class HostileTests
    {
        [Test]
        public void AMarauderHuntsTheNearestStandingColonistAndIgnoresADownedOne()
        {
            var colony = Board(colonists: 3);
            colony.World.Tick(5);
            Pawn near = colony.Pawns.Pawns.All[0], mid = colony.Pawns.Pawns.All[1], far = colony.Pawns.Pawns.All[2];
            Stand(colony, near, Near(colony, 4, 0));
            Stand(colony, mid, Near(colony, 8, 0));
            Stand(colony, far, Near(colony, 16, 0));
            foreach (Pawn p in new[] { near, mid, far }) Assert.That(Draft(colony, p), Is.EqualTo(IntentRejection.None));

            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 0, 0));
            colony.World.Tick();
            Assert.That(marauder.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee));
            Assert.That(marauder.CombatTarget, Is.EqualTo(near.Id.Value), "the control: the nearest is hunted first");

            Strike(colony, mid, near, near.HpMilli);
            Assert.That(near.Downed, Is.True);
            for (int t = 0; t < 400; t++)
            {
                colony.World.Tick();
                Assert.That(marauder.CombatTarget, Is.Not.EqualTo(near.Id.Value), $"tick {t}: a downed colonist was hunted");
            }
            Assert.That(marauder.CombatTarget, Is.EqualTo(mid.Id.Value), "the next nearest standing colonist was not chosen");
        }

        [Test]
        public void WithNobodyStandingAMarauderIdles()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick(5);
            Pawn only = colony.Pawns.Pawns.All[0];
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 6, 0));
            colony.World.Tick();
            Assert.That(marauder.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee), "the control: it hunts a standing colonist");

            Strike(colony, marauder, only, only.HpMilli);
            colony.World.Tick(400);
            Assert.That(marauder.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.AttackMelee));
            Assert.That(marauder.CombatTarget, Is.EqualTo(0));
        }

        /// <summary>
        /// An undrafted colonist at her work, struck by a marauder, stops and hits back (design 33
        /// §1): the blow interrupts her and her self-defence finds it beside her. The control is
        /// the same colonist before the marauder reached her, doing something else.
        /// </summary>
        [Test]
        public void AColonistStruckByAMarauderFightsBack()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick(5);
            Pawn her = colony.Pawns.Pawns.All[0];
            var rules = new RecordingRules();
            colony.Pawns.MeleeRules = rules;
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 8, 8));
            colony.World.Tick();
            Assert.That(her.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.AttackMelee), "the control: she was not fighting");

            for (int t = 0; t < 3_000 && rules.TicksOf(her).Count == 0; t++)
            {
                // A broken colonist does not fight (the break is the one state nobody commands
                // through); on a bare board with nothing to lift her mood she would break.
                her.BreakTicksLeft = 0;
                colony.World.Tick();
            }
            Assert.That(rules.TicksOf(her).Count, Is.GreaterThan(0), "she never struck back");
            Assert.That(rules.Swings.Where(s => s.Attacker == her.Id.Value).All(s => s.Target == marauder.Id.Value), Is.True);
        }

        /// <summary>
        /// A colonist struck fights back for the retaliation window (design 33 §1) — by a colonist,
        /// the owner's rule, and by a marauder or an animal alike — and the blow is remembered on her
        /// as who and until when. A drafted colonist remembers nothing, because her hold does its
        /// own fighting and the player's hand is on her: the control.
        /// </summary>
        [Test]
        public void AColonistStruckRetaliatesAndADraftedOneLeavesItToTheHold()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn victim = colony.Pawns.Pawns.All[0], by = colony.Pawns.Pawns.All[1];
            Stand(colony, by, Near(colony, 0, 0));
            Stand(colony, victim, Near(colony, 1, 0));
            colony.World.Tick();
            victim.BreakTicksLeft = 0;
            int tick = colony.World.CurrentTick;
            Strike(colony, by, victim, 1_000);

            Assert.That(victim.RetaliateAgainst, Is.EqualTo(by.Id.Value));
            Assert.That(victim.RetaliateUntilTick, Is.EqualTo(tick + colony.Pawns.Content.Combat.retaliationTicks));
            colony.World.Tick(2);
            Assert.That(victim.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee));
            Assert.That(victim.CombatTarget, Is.EqualTo(by.Id.Value));

            Assert.That(Draft(colony, by), Is.EqualTo(IntentRejection.None));
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 12, 12));
            colony.World.Tick();
            Strike(colony, marauder, by, 1_000);
            Assert.That(by.RetaliateAgainst, Is.EqualTo(0), "the control: a drafted colonist remembered the blow");
        }
    }
}
