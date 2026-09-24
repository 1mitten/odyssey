#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Drafted colonists help (design 33 §15). Owner, 2026-09-24: <i>"if a colonist [is] attack[ed]
    /// — by default they will fight back. If I draft colonist/colonists by default — if there is any
    /// fight going on nearby (another colonist is being attack[ed]) they will help and start
    /// attacking the attacker and help other colonists by default."</i>
    ///
    /// <para>A drafted colonist on her hold who sees another colonist attacked by a bandit or an
    /// animal within <see cref="CombatDef.helpRadiusCells"/> goes to the attacker and fights it —
    /// unforced, on the existing attack job. Not while walking an order, not undrafted, not for a
    /// colonist fighting a colonist; she holds where the fight ends; several helpers take sides of
    /// their own.</para>
    /// </summary>
    public class DraftedHelpTests
    {
        /// <summary>The shipped rules with every swing a miss, so a fight lasts; records who swung at whom.</summary>
        sealed class Whiffs : MeleeRules
        {
            public readonly List<(int Tick, int Attacker, int Target)> Swings = new List<(int, int, int)>();

            public override SwingOutcome Resolve(Pawn attacker, Pawn defender, in Armament armament, PawnContext ctx, int tick)
            {
                Swings.Add((tick, attacker.Id.Value, defender.Id.Value));
                return new SwingOutcome(CombatEventKind.Miss);
            }

            public int At(Pawn attacker, Pawn target) =>
                Swings.FindAll(s => s.Attacker == attacker.Id.Value && s.Target == target.Id.Value).Count;
        }

        static void Tick(ColonyWorld colony, params Pawn[] keepWhole)
        {
            // Nothing on a bare board lifts a colonist's mood, and a drafted colonist neither eats
            // nor sleeps: a break or exhaustion would undraft her in the longer fights. Neither is
            // what these tests are about.
            foreach (Pawn p in keepWhole)
            {
                p.BreakTicksLeft = 0;
                for (int n = 0; n < NeedIndex.Count; n++) p.Needs[n] = 800;
            }
            colony.World.Tick();
        }

        static bool IsJoining(Pawn pawn, Pawn attacker) =>
            pawn.CurrentJob is { DefIndex: JobIndex.AttackMelee } job
            && job.DestCell == AttackMeleeJobDriver.Joining && pawn.CombatTarget == attacker.Id.Value;

        /// <summary>
        /// A victim drafted on the start and a helper drafted <paramref name="away"/> cells west of
        /// her, with every swing a miss; a bandit four cells east of the victim, so the victim is
        /// the one it hunts.
        /// </summary>
        static (ColonyWorld colony, Pawn helper, Pawn victim, Whiffs rules) Scene(int away, bool helperDrafted = true)
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            var rules = new Whiffs();
            colony.Pawns.MeleeRules = rules;
            Pawn helper = colony.Pawns.Pawns.All[0], victim = colony.Pawns.Pawns.All[1];
            Stand(colony, victim, Near(colony, 0, 0));
            Stand(colony, helper, Near(colony, -away, 0));
            Assert.That(Draft(colony, victim), Is.EqualTo(IntentRejection.None));
            if (helperDrafted) Assert.That(Draft(colony, helper), Is.EqualTo(IntentRejection.None));
            return (colony, helper, victim, rules);
        }

        /// <summary>
        /// The heart of it, and its distance: five cells from a bandit beating on a colonist, she
        /// goes to it and fights; twelve cells off, she holds. At the moment she sets off the
        /// bandit is fighting the victim, not her, and is out of her reach — so this is the help,
        /// not the hold's own blow — and nobody ordered it.
        /// </summary>
        [TestCase(5, true)]
        [TestCase(12, false)]
        public void AHoldingColonistJoinsAFightNearbyAndNotOneFarOff(int away, bool joins)
        {
            var (colony, helper, victim, rules) = Scene(away);
            int home = helper.Cell;
            for (int t = 0; t < 5; t++) Tick(colony, helper, victim);
            Assert.That(helper.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.DraftHold), "the control: nothing to join yet");

            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 4, 0));
            int joinedAt = -1;
            for (int t = 0; t < 1_500; t++)
            {
                Tick(colony, helper, victim);
                if (joinedAt >= 0 || helper.CombatTarget != bandit.Id.Value) continue;
                joinedAt = colony.World.CurrentTick;
                Assert.That(IsJoining(helper, bandit), Is.True, "she took the bandit on, but not as a helper");
                Assert.That(helper.CurrentJob!.PlayerForced, Is.False, "nobody ordered it");
                Assert.That(bandit.CombatTarget, Is.EqualTo(victim.Id.Value), "the bandit was not fighting the victim");
                Assert.That(Melee.InReach(colony.Pawns, helper, bandit, TraverseMode.Colonist), Is.False,
                    "it was beside her: the hold's own blow, not the help");
            }

            if (joins)
            {
                Assert.That(joinedAt, Is.GreaterThanOrEqualTo(0), "five cells from the fight, she never joined it");
                Assert.That(rules.At(helper, bandit), Is.GreaterThan(0), "she joined but never swung");
                Assert.That(helper.Cell, Is.Not.EqualTo(home), "she swung without leaving her hold");
                Assert.That(helper.Drafted, Is.True);
            }
            else
            {
                Assert.That(joinedAt, Is.EqualTo(-1), "twelve cells off, she joined");
                Assert.That(helper.Cell, Is.EqualTo(home), "twelve cells off, she left her hold");
                Assert.That(rules.At(bandit, victim), Is.GreaterThan(0), "the control: the fight happened");
            }
        }

        /// <summary>
        /// A drafted colonist walking a move order does not turn aside for a fight she passes, and
        /// joins it once she has arrived and holds — the move is the player's, the hold is hers.
        /// </summary>
        [Test]
        public void OneWalkingAMoveOrderDoesNotTurnAsideAndJoinsOnceSheHolds()
        {
            var (colony, helper, victim, rules) = Scene(14);
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 4, 0));
            for (int t = 0; t < 1_500 && rules.At(bandit, victim) == 0; t++) Tick(colony, helper, victim);
            Assume.That(rules.At(bandit, victim), Is.GreaterThan(0), "the fight never started");
            Assert.That(helper.CombatTarget, Is.EqualTo(0), "the control: fourteen cells off, she holds");

            // Sent to a cell north-west of the fight, three cells from the victim: the walk crosses
            // the radius from outside it.
            int goal = Near(colony, -3, 2);
            Assert.That(Send(colony, new Intent(IntentKind.OrderMove, Size.FromIndex(goal), helper.Id.Value)),
                Is.EqualTo(IntentRejection.None));
            int walked = 0;
            while (helper.CurrentJob?.DefIndex == JobIndex.Goto && walked < 2_000)
            {
                Assert.That(helper.CombatTarget, Is.EqualTo(0), $"tick {walked}: she turned aside from the order");
                Tick(colony, helper, victim);
                walked++;
            }
            Assert.That(walked, Is.GreaterThan(20), "the walk was too short to prove anything");
            Assert.That(helper.Cell, Is.EqualTo(goal), "she did not arrive where she was sent");

            // The control: holding within the radius, she joins.
            for (int t = 0; t < 10 && helper.CombatTarget == 0; t++) Tick(colony, helper, victim);
            Assert.That(IsJoining(helper, bandit), Is.True, "holding beside the fight, she did not join it");
        }

        /// <summary>
        /// Undrafted colonists keep today's behaviour: five cells from the same fight, she does not
        /// go to it. (The drafted colonist in the same scene does — the first test.)
        /// </summary>
        [Test]
        public void AnUndraftedColonistLeavesAFightNearbyAlone()
        {
            var (colony, helper, victim, rules) = Scene(5, helperDrafted: false);
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 4, 0));
            for (int t = 0; t < 1_500; t++)
            {
                Tick(colony, helper, victim);
                Assert.That(helper.CombatTarget, Is.Not.EqualTo(bandit.Id.Value), $"tick {t}: undrafted, she joined the fight");
            }
            Assert.That(rules.At(bandit, victim), Is.GreaterThan(0), "the control: the fight happened");
        }

        /// <summary>
        /// A colonist attacking a colonist — the Ctrl order, and the blows she takes back — summons
        /// nobody: help is for a bandit or an animal.
        /// </summary>
        [Test]
        public void AColonistFightingAColonistSummonsNobody()
        {
            var colony = Board(colonists: 3);
            colony.World.Tick(5);
            var rules = new Whiffs();
            colony.Pawns.MeleeRules = rules;
            Pawn helper = colony.Pawns.Pawns.All[0], by = colony.Pawns.Pawns.All[1], victim = colony.Pawns.Pawns.All[2];
            Stand(colony, victim, Near(colony, 0, 0));
            Stand(colony, by, Near(colony, 3, 0));
            Stand(colony, helper, Near(colony, -4, 0));
            foreach (Pawn p in new[] { helper, by, victim }) Assert.That(Draft(colony, p), Is.EqualTo(IntentRejection.None));
            int home = helper.Cell;
            Assert.That(Attack(colony, by, victim), Is.EqualTo(IntentRejection.None));

            for (int t = 0; t < 1_500; t++)
            {
                Tick(colony, helper, by, victim);
                Assert.That(helper.CombatTarget, Is.EqualTo(0), $"tick {t}: a colonist's fight with a colonist drew her in");
            }
            Assert.That(helper.Cell, Is.EqualTo(home));
            Assert.That(rules.At(by, victim), Is.GreaterThan(0), "the control: the fight happened");
            Assert.That(rules.At(victim, by), Is.GreaterThan(0), "the control: she fought back");
        }

        /// <summary>
        /// When the attacker goes down she holds where the fight ended — a fresh hold on her own cell,
        /// still drafted — and does not walk back to where she stood before.
        /// </summary>
        [Test]
        public void WhenTheAttackerIsDownSheHoldsWhereTheFightEnded()
        {
            var (colony, helper, victim, rules) = Scene(5);
            int home = helper.Cell;
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 4, 0));
            for (int t = 0; t < 2_000 && rules.At(helper, bandit) == 0; t++) Tick(colony, helper, victim);
            Assert.That(rules.At(helper, bandit), Is.GreaterThan(0), "she never joined and swung");

            Strike(colony, victim, bandit, bandit.HpMilli);
            Assume.That(bandit.Downed, Is.True);
            for (int t = 0; t < 3; t++) Tick(colony, helper, victim);
            int ended = helper.Cell;
            Assert.That(ended, Is.Not.EqualTo(home));
            for (int t = 0; t < 600; t++)
            {
                Tick(colony, helper, victim);
                Assert.That(helper.Cell, Is.EqualTo(ended), $"tick {t}: she walked off after the fight");
            }
            Assert.That(helper.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.DraftHold));
            Assert.That(helper.Drafted, Is.True);
        }

        /// <summary>
        /// An animal on a colonist is a fight to join too; and once its revenge is spent and it is
        /// on nobody, the reason for the help is over — she holds where she is rather than chasing a
        /// hog that has gone back to rooting about.
        /// </summary>
        [Test]
        public void SheJoinsAgainstAnAnimalAndLetsItGoWhenItIsOnNobody()
        {
            var (colony, helper, victim, rules) = Scene(5);
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 1, 1));
            hog.RetaliateAgainst = victim.Id.Value;
            hog.RetaliateUntilTick = colony.World.CurrentTick + 100_000;
            colony.Jobs.EndJob(hog, JobStatus.Failed);

            for (int t = 0; t < 1_500 && rules.At(helper, hog) == 0; t++) Tick(colony, helper, victim);
            Assert.That(rules.At(hog, victim), Is.GreaterThan(0), "the control: the hog went for her");
            Assert.That(rules.At(helper, hog), Is.GreaterThan(0), "she never joined against the hog");

            // Its revenge spent: it lets the victim be.
            hog.RetaliateUntilTick = colony.World.CurrentTick;
            colony.Jobs.EndJob(hog, JobStatus.Failed);
            for (int t = 0; t < 60; t++) Tick(colony, helper, victim);
            Assert.That(hog.CombatTarget, Is.EqualTo(0), "the control: the hog is on nobody");
            Assert.That(helper.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.DraftHold), "she went on after a hog that was on nobody");
            int at = helper.Cell;
            for (int t = 0; t < 300; t++)
            {
                Tick(colony, helper, victim);
                Assert.That(helper.Cell, Is.EqualTo(at), $"tick {t}: she followed the hog");
            }
        }

        /// <summary>
        /// Two helpers coming from one side take sides of their own (design 33 §7c, §8c): the fight
        /// guard, after every tick, finds no two fighters on one tile, and both helpers fought.
        /// </summary>
        [Test]
        public void TwoHelpersNeverShareATile()
        {
            var colony = Board(colonists: 3);
            colony.World.Tick(5);
            var rules = new Whiffs();
            colony.Pawns.MeleeRules = rules;
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1], victim = colony.Pawns.Pawns.All[2];
            Stand(colony, victim, Near(colony, 0, 0));
            Stand(colony, a, Near(colony, -5, 0));
            Stand(colony, b, Near(colony, -5, 1));
            foreach (Pawn p in new[] { a, b, victim }) Assert.That(Draft(colony, p), Is.EqualTo(IntentRejection.None));
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 4, 0));

            var guard = new FightGuardTests.Guard();
            bool aJoined = false, bJoined = false;
            for (int t = 0; t < 1_500; t++)
            {
                Tick(colony, a, b, victim);
                guard.Check(colony);
                aJoined |= IsJoining(a, bandit);
                bJoined |= IsJoining(b, bandit);
            }
            guard.AssertClean("two helpers");
            Assert.That(aJoined && bJoined, Is.True, $"the control: both joined (a {aJoined}, b {bJoined})");
            Assert.That(rules.At(a, bandit), Is.GreaterThan(0), "the control: the first helper swung");
            Assert.That(rules.At(b, bandit), Is.GreaterThan(0), "the control: the second helper swung");
            Assert.That(guard.CloseTicks, Is.GreaterThan(100), "the control: nobody fought at close quarters");
        }

        /// <summary>
        /// A fight is activity for the draft: a helper in a fight longer than the four quiet hours
        /// is still drafted at its end, and the hours count from where the fight stopped.
        /// </summary>
        [Test]
        public void AFightLongerThanFourHoursKeepsTheDraft()
        {
            var (colony, helper, victim, rules) = Scene(5);
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 4, 0));
            int quiet = colony.Pawns.Content.DraftQuietTicks;
            for (int t = 0; t < quiet + 2_000; t++) Tick(colony, helper, victim);
            Assert.That(rules.At(helper, bandit), Is.GreaterThan(50), "the control: she fought all along");
            Assert.That(helper.Drafted, Is.True, "a fight longer than four hours let the draft go");

            int down = colony.World.CurrentTick;
            Strike(colony, victim, bandit, bandit.HpMilli);
            for (int t = 0; t < 10; t++) Tick(colony, helper, victim);
            Assert.That(helper.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.DraftHold));
            Assert.That(down - helper.DraftQuietSinceTick, Is.LessThan(colony.Pawns.Content.Combat.rechooseTicks + 200),
                "the quiet hours did not start again near the fight's end");
        }

        // ---- the scan itself: range, nearest, the tie ----------------------------------------------

        /// <summary>
        /// A bandit spawned beside a victim; <see cref="Settle"/> gives it a tick to take her on
        /// and then stuns it, so its attack stands still.
        /// </summary>
        static Pawn Pinned(ColonyWorld colony, int dx, int dz) => Spawn(colony, PawnKindIndex.Bandit, Near(colony, dx, dz));

        static void Settle(ColonyWorld colony, params Pawn[] bandits)
        {
            colony.World.Tick();
            foreach (Pawn m in bandits)
            {
                m.StunnedUntilTick = colony.World.CurrentTick + 100_000;
                m.ClearPath();
                m.Destination = -1;
            }
        }

        /// <summary>
        /// The radius holds both the victim and her attacker: eight cells is in, nine is out, for
        /// either. Asked of the scan directly, with the fights held still.
        /// </summary>
        [TestCase(7, 8, true)]
        [TestCase(8, 9, false)]
        [TestCase(9, 8, false)]
        public void TheRadiusHoldsTheVictimAndHerAttacker(int victimAt, int banditAt, bool found)
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn helper = colony.Pawns.Pawns.All[0], victim = colony.Pawns.Pawns.All[1];
            Assume.That(colony.Pawns.Content.Combat.helpRadiusCells, Is.EqualTo(8));
            Stand(colony, victim, Near(colony, victimAt, 0));
            Stand(colony, helper, Near(colony, -30, -20));
            Pawn bandit = Pinned(colony, banditAt, 0);
            Settle(colony, bandit);
            Assume.That(bandit.CombatTarget, Is.EqualTo(victim.Id.Value), "the bandit took on the victim");
            Stand(colony, helper, Near(colony, 0, 0));

            Pawn? foe = Melee.HoldTarget(colony.Pawns, helper, out bool joining);
            Assert.That(foe, found ? Is.SameAs(bandit) : Is.Null);
            Assert.That(joining, Is.EqualTo(found));
        }

        /// <summary>
        /// The nearest victim's attacker first, and a tie goes to the lower victim id — whichever
        /// attacker came first.
        /// </summary>
        [Test]
        public void TheNearestVictimsAttackerFirstAndATieToTheLowerVictim()
        {
            var colony = Board(colonists: 3);
            colony.World.Tick(5);
            Pawn helper = colony.Pawns.Pawns.All[0], low = colony.Pawns.Pawns.All[1], high = colony.Pawns.Pawns.All[2];
            Assume.That(low.Id.Value, Is.LessThan(high.Id.Value));
            Stand(colony, helper, Near(colony, -30, -20));

            // Nearest: high at three cells beats low at six.
            Stand(colony, high, Near(colony, 3, 0));
            Stand(colony, low, Near(colony, -6, 0));
            Pawn onHigh = Pinned(colony, 4, 0);
            Pawn onLow = Pinned(colony, -7, 0);
            Settle(colony, onHigh, onLow);
            Assume.That(onHigh.CombatTarget, Is.EqualTo(high.Id.Value));
            Assume.That(onLow.CombatTarget, Is.EqualTo(low.Id.Value));
            Stand(colony, helper, Near(colony, 0, 0));
            Assert.That(Melee.HoldTarget(colony.Pawns, helper, out _), Is.SameAs(onHigh), "the nearer victim's attacker was not first");

            // A tie: both victims four cells off. The lower victim's attacker has the higher id.
            Stand(colony, low, Near(colony, 4, 0));
            Stand(colony, high, Near(colony, -4, 0));
            Stand(colony, onHigh, Near(colony, -5, 0));
            Stand(colony, onLow, Near(colony, 5, 0));
            // Stand ended their jobs; give them back, held.
            onHigh.StunnedUntilTick = 0;
            onLow.StunnedUntilTick = 0;
            Stand(colony, helper, Near(colony, -30, -20));
            Settle(colony, onHigh, onLow);
            Assume.That(onLow.CombatTarget, Is.EqualTo(low.Id.Value));
            Assume.That(onHigh.CombatTarget, Is.EqualTo(high.Id.Value));
            Assume.That(onHigh.Id.Value, Is.LessThan(onLow.Id.Value), "the tie must not be won by the attacker's id");
            Stand(colony, helper, Near(colony, 0, 0));
            Assert.That(Melee.HoldTarget(colony.Pawns, helper, out _), Is.SameAs(onLow), "a tie did not go to the lower victim");
        }

        // ---- fighting back when struck: already there (design 33 §6A.6), held here -------------------

        /// <summary>
        /// The owner's first sentence, which the game already did: a colonist a bandit attacks
        /// fights back, drafted (the hold's blow) or not (self-defence). No change; held.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void AColonistAttackedFightsBackDraftedOrNot(bool drafted)
        {
            var colony = Board(colonists: 1);
            colony.World.Tick(5);
            Pawn her = colony.Pawns.Pawns.All[0];
            Stand(colony, her, Near(colony, 0, 0));
            if (drafted) Assert.That(Draft(colony, her), Is.EqualTo(IntentRejection.None));
            var rules = new RecordingRules();
            colony.Pawns.MeleeRules = rules;
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 3, 0));
            for (int t = 0; t < 2_000 && rules.TicksOf(her).Count == 0; t++) Tick(colony, her);
            Assert.That(rules.TicksOf(her).Count, Is.GreaterThan(0), "attacked, she never struck back");
            Assert.That(rules.Swings.FindAll(s => s.Attacker == her.Id.Value).TrueForAll(s => s.Target == bandit.Id.Value), Is.True);
        }
    }
}
