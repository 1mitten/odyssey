#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// One guard for every fighter (design 33 §8c). Owner, 2026-09-23: <i>"make it a guard that
    /// enemies when sharing tiles going side by side as well or handled uniformly"</i>.
    ///
    /// <para><b>The rule:</b> at every tick, no two pawns in a fight stand on one cell. A pawn is
    /// in a fight while it is in a melee attack on its feet (<see cref="Melee.IsInAnAttack"/>) —
    /// ordered, the drafted hold's blow, self-defence, the hunt, an animal's revenge — or while it
    /// is the target of one, whatever it is doing, standing or down — a colonist hunted from across
    /// the board included. A pawn <b>stands</b> once it has arrived and had a tick to act on it:
    /// no walk in hand (<see cref="Pawn.Destination"/> −1) and no interrupted step still landing,
    /// now and at the tick before, in a job it has ticked or held by a stun. Walking through
    /// somebody's cell is not sharing it — the simulation has no collision, and the drawn
    /// sidestep carries a passer-by round — and neither is the one tick a pawn spends between
    /// jobs or landing; anything that stays is.</para>
    ///
    /// <para>Each case below is a brawl of a different shape, run tick by tick with the rule
    /// asserted after every one, and a control that the fight really happened. The Long tier's
    /// <see cref="MixedBrawlsOnManySeeds"/> throws every kind at every kind on twelve seeds.</para>
    /// </summary>
    public class FightGuardTests
    {
        /// <summary>The shipped rules with every swing a miss: a fight that lasts for ever.</summary>
        sealed class Whiffs : MeleeRules
        {
            public int Swings;

            public override SwingOutcome Resolve(Pawn attacker, Pawn defender, in Armament armament, PawnContext ctx, int tick)
            {
                Swings++;
                return new SwingOutcome(CombatEventKind.Miss);
            }
        }

        /// <summary>The shipped rules, counting swings.</summary>
        sealed class Counted : MeleeRules
        {
            public int Swings;

            public override SwingOutcome Resolve(Pawn attacker, Pawn defender, in Armament armament, PawnContext ctx, int tick)
            {
                Swings++;
                return base.Resolve(attacker, defender, armament, ctx, tick);
            }
        }

        /// <summary>
        /// Watches a colony tick by tick for two fighters standing on one cell. Counts what it saw,
        /// so a case can show the fight it guarded was a fight.
        /// </summary>
        internal sealed class Guard
        {
            readonly List<Pawn> _fighters = new List<Pawn>();
            readonly HashSet<int> _ids = new HashSet<int>();

            /// <summary>Pawn-ticks a fighter stood beside another, in reach of it — the fight at close quarters.</summary>
            public int CloseTicks;

            /// <summary>Kinds seen in a fight, as a bit per kind.</summary>
            public int Kinds;

            /// <summary>Pawn-ticks a drafted colonist spent joining another colonist's fight (design 33 §15).</summary>
            public int JoiningTicks;

            public readonly List<string> Violations = new List<string>();

            /// <summary>Pairs found sharing a tile, over every tick watched.</summary>
            public int Count;

            /// <summary>
            /// Is <paramref name="pawn"/> on the move: a walk in hand, or an interrupted step still
            /// landing? A walk is in hand until its job has seen it arrive, which is the tick after
            /// the step that reached it.
            /// </summary>
            public static bool Moving(Pawn pawn) =>
                pawn.FinishingStepTo >= 0 || pawn.Destination >= 0;

            // Who was on the move at the last check: a pawn that has only just arrived has not yet
            // had a tick on its cell to choose whether to stay.
            HashSet<int> _movingBefore = new HashSet<int>(), _movingNow = new HashSet<int>();

            /// <summary>
            /// Has <paramref name="pawn"/> arrived where it is, and chosen to be there? Not on the
            /// move now or at the last check — the tick it lands is the tick it has to act — and in
            /// a job it has had a tick to act on, or held where it is by a stun. A pawn between
            /// jobs, or given one at the end of the tick just run, has not chosen its cell yet;
            /// the tick after is when it must be somewhere of its own, and that tick is checked
            /// like any other. So a one-tick crossing is excused and anything that stays is not.
            /// </summary>
            bool Stands(Pawn pawn, int now)
            {
                if (Moving(pawn) || _movingBefore.Contains(pawn.Id.Value)) return false;
                if (pawn.StunnedAt(now - 1)) return true;
                // Knocked down (design 33 §9b): lying on the tile the blow put it on, with no job,
                // from the tick it lands. That tile is its own as surely as a side is.
                if (pawn.KnockedDownAt(now - 1)) return true;
                return pawn.CurrentJob != null && pawn.JobStartTick < now - 1;
            }

            public void Check(ColonyWorld colony)
            {
                _fighters.Clear();
                _ids.Clear();
                var pawns = colony.Pawns.Pawns.All;
                _movingNow.Clear();
                for (int i = 0; i < pawns.Count; i++)
                    if (Moving(pawns[i])) _movingNow.Add(pawns[i].Id.Value);
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn p = pawns[i];
                    if (!Melee.IsInAnAttack(p)) continue;
                    if (p.CurrentJob!.DestCell == AttackMeleeJobDriver.Joining && p.CombatTarget != 0) JoiningTicks++;
                    Add(p);
                    Pawn? target = colony.Pawns.Pawns.Get(new PawnId(p.CombatTarget));
                    if (target != null) Add(target);
                }

                for (int i = 0; i < _fighters.Count; i++)
                {
                    Pawn a = _fighters[i];
                    Kinds |= 1 << a.Kind;
                    for (int j = i + 1; j < _fighters.Count; j++)
                    {
                        Pawn b = _fighters[j];
                        if (a.Cell != b.Cell) continue;
                        int now = colony.World.CurrentTick;
                        if (!Stands(a, now) || !Stands(b, now)) continue;
                        Count++;
                        if (Violations.Count < 8)
                            Violations.Add($"tick {colony.World.CurrentTick}: {Describe(a)} and {Describe(b)} stand on {Size.FromIndex(a.Cell)}");
                        else if (Violations.Count == 8) Violations.Add("...");
                    }
                    if (Melee.IsInAnAttack(a))
                    {
                        Pawn? t = colony.Pawns.Pawns.Get(new PawnId(a.CombatTarget));
                        if (t != null && t.Cell != a.Cell && Melee.InReach(colony.Pawns, a, t, a.Species.traverseMode)) CloseTicks++;
                    }
                }

                (_movingBefore, _movingNow) = (_movingNow, _movingBefore);
            }

            void Add(Pawn p)
            {
                if (_ids.Add(p.Id.Value)) _fighters.Add(p);
            }

            public static string Describe(Pawn p) =>
                $"[{p.Id.Value} kind {p.Kind} job {p.CurrentJob?.DefIndex} target {p.CombatTarget} dest {p.Destination} "
                + $"finishing {p.FinishingStepTo} drafted {p.Drafted} down {p.Downed} toil {p.Driver?.ToilIndex}]";

            public void AssertClean(string name) =>
                Assert.That(Violations, Is.Empty, $"{name}: fighters sharing a tile, {Count} pair-ticks:\n" + string.Join("\n", Violations));
        }

        static Guard Run(ColonyWorld colony, int ticks, Guard? guard = null)
        {
            guard ??= new Guard();
            for (int t = 0; t < ticks; t++)
            {
                colony.World.Tick();
                guard.Check(colony);
            }
            return guard;
        }

        static ColonyWorld Board(int colonists, bool whiffs, out MeleeRules rules)
        {
            var colony = CombatFixture.Board(colonists: colonists);
            colony.World.Tick();
            rules = whiffs ? new Whiffs() : new Counted();
            colony.Pawns.MeleeRules = rules;
            return colony;
        }

        static int Swings(MeleeRules rules) => rules is Whiffs w ? w.Swings : ((Counted)rules).Swings;

        static void Controls(string name, Guard guard, MeleeRules rules, int minSwings)
        {
            Assert.That(Swings(rules), Is.GreaterThanOrEqualTo(minSwings), $"{name}, the control: the fight never happened");
            Assert.That(guard.CloseTicks, Is.GreaterThan(100), $"{name}, the control: nobody fought at close quarters");
        }

        /// <summary>
        /// An interrupted step is held until it lands (design 33 §7c, §2d): a pawn stunned mid-step
        /// holds the cell it is stepping into, so no attacker takes it as a side. Found by
        /// <see cref="MixedBrawlsOnManySeeds"/> seed 11 once the body changed who stood where
        /// (design 43 §3). The control is the same pawn with no step in hand, which holds only its
        /// own cell.
        /// </summary>
        [Test]
        public void AStepStillLandingIsHeld()
        {
            var colony = CombatFixture.Board(colonists: 2);
            colony.World.Tick();
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            Stand(colony, a, Near(colony, 0, 0));
            Stand(colony, b, Near(colony, 3, 0));
            Assert.That(Draft(colony, b), Is.EqualTo(IntentRejection.None));
            Assert.That(Attack(colony, b, a), Is.EqualTo(IntentRejection.None));
            Assume.That(Melee.IsAttacking(b, a), Is.True, "the fixture: nobody is attacking her");
            int next = Near(colony, -1, 0);

            // Stunned mid-step: no walk in hand, the step still landing.
            a.ClearPath();
            a.Destination = -1;
            a.FinishingStepTo = next;
            Assert.That(Melee.SideOf(a), Is.EqualTo(next));
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 0, 5));
            Assert.That(Melee.Holds(colony.Pawns, bandit, next), Is.True, "a cell she is stepping into was free to take");

            a.FinishingStepTo = -1;
            Assert.That(Melee.Holds(colony.Pawns, bandit, next), Is.False, "the control: with no step in hand she holds only her own cell");
        }

        /// <summary>Four bandits from four sides on one colonist going about her day, who fights back.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void SeveralBanditsOnOneColonist(bool whiffs)
        {
            var colony = Board(1, whiffs, out var rules);
            Pawn c = colony.Pawns.Pawns.All[0];
            Stand(colony, c, Near(colony, 0, 0));
            Spawn(colony, PawnKindIndex.Bandit, Near(colony, -9, 0));
            Spawn(colony, PawnKindIndex.Bandit, Near(colony, 9, 1));
            Spawn(colony, PawnKindIndex.Bandit, Near(colony, 1, -9));
            Spawn(colony, PawnKindIndex.Bandit, Near(colony, -1, 9));
            var guard = Run(colony, 1_500);
            guard.AssertClean(nameof(SeveralBanditsOnOneColonist));
            Controls(nameof(SeveralBanditsOnOneColonist), guard, rules, 10);
        }

        /// <summary>Four drafted colonists ordered on to one bandit, which fights back.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void SeveralColonistsOnOneBandit(bool whiffs)
        {
            var colony = Board(4, whiffs, out var rules);
            var all = colony.Pawns.Pawns.All;
            var cs = new List<Pawn>(all);
            for (int i = 0; i < cs.Count; i++) Stand(colony, cs[i], Near(colony, -6, i * 2 - 3));
            Pawn m = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 4, 0));
            foreach (Pawn c in cs) Assert.That(Draft(colony, c), Is.EqualTo(IntentRejection.None));
            foreach (Pawn c in cs) Assert.That(Attack(colony, c, m), Is.EqualTo(IntentRejection.None));
            var guard = Run(colony, 1_500);
            guard.AssertClean(nameof(SeveralColonistsOnOneBandit));
            Controls(nameof(SeveralColonistsOnOneBandit), guard, rules, 10);
        }

        /// <summary>
        /// Two colonists side by side, undrafted, and two bandits arriving side by side: each
        /// bandit takes one, both colonists fight back, and the four stand on four cells.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void TwoBanditsOnTwoColonistsSideBySide(bool whiffs)
        {
            var colony = Board(2, whiffs, out var rules);
            var all = colony.Pawns.Pawns.All;
            Stand(colony, all[0], Near(colony, 0, 0));
            Stand(colony, all[1], Near(colony, 0, 1));
            Spawn(colony, PawnKindIndex.Bandit, Near(colony, -8, 0));
            Spawn(colony, PawnKindIndex.Bandit, Near(colony, -8, 1));
            var guard = Run(colony, 1_500);
            guard.AssertClean(nameof(TwoBanditsOnTwoColonistsSideBySide));
            Controls(nameof(TwoBanditsOnTwoColonistsSideBySide), guard, rules, 10);
        }

        /// <summary>
        /// Drafted colonists holding a line, side by side, and bandits walking into it: the hold's
        /// own blow strikes from where she stands, and nobody arriving may stand on her.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void BanditsIntoADraftedLine(bool whiffs)
        {
            var colony = Board(3, whiffs, out var rules);
            var cs = new List<Pawn>(colony.Pawns.Pawns.All);
            for (int i = 0; i < cs.Count; i++) Stand(colony, cs[i], Near(colony, 0, i - 1));
            foreach (Pawn c in cs) Assert.That(Draft(colony, c), Is.EqualTo(IntentRejection.None));
            for (int i = 0; i < 4; i++) Spawn(colony, PawnKindIndex.Bandit, Near(colony, -9, i - 2));
            var guard = Run(colony, 1_500);
            guard.AssertClean(nameof(BanditsIntoADraftedLine));
            Controls(nameof(BanditsIntoADraftedLine), guard, rules, 10);
        }

        /// <summary>
        /// Five bandits converging from one direction on two colonists: they must fan out round
        /// both, not queue on one tile.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void BanditsConvergingFromOneDirection(bool whiffs)
        {
            var colony = Board(2, whiffs, out var rules);
            var all = colony.Pawns.Pawns.All;
            Stand(colony, all[0], Near(colony, 0, 0));
            Stand(colony, all[1], Near(colony, 1, 0));
            for (int i = 0; i < 5; i++) Spawn(colony, PawnKindIndex.Bandit, Near(colony, -10 - (i % 2), i - 2));
            var guard = Run(colony, 1_500);
            guard.AssertClean(nameof(BanditsConvergingFromOneDirection));
            Controls(nameof(BanditsConvergingFromOneDirection), guard, rules, 10);
        }

        /// <summary>
        /// Drafted colonists ordered on to hogs, with the shipped rules, so the hogs roll their
        /// revenge and turn on whoever hit them — two colonists on one hog, one on the other.
        /// </summary>
        [Test]
        public void HogsTurningOnColonists()
        {
            var colony = Board(3, whiffs: false, out var rules);
            var cs = new List<Pawn>(colony.Pawns.Pawns.All);
            for (int i = 0; i < cs.Count; i++) Stand(colony, cs[i], Near(colony, 0, i * 2 - 2));
            Pawn h1 = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 5, 0));
            Pawn h2 = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 5, 2));
            foreach (Pawn c in cs) Assert.That(Draft(colony, c), Is.EqualTo(IntentRejection.None));
            Assert.That(Attack(colony, cs[0], h1), Is.EqualTo(IntentRejection.None));
            Assert.That(Attack(colony, cs[1], h1), Is.EqualTo(IntentRejection.None));
            Assert.That(Attack(colony, cs[2], h2), Is.EqualTo(IntentRejection.None));

            var guard = new Guard();
            bool turned = false;
            for (int t = 0; t < 2_000; t++)
            {
                colony.World.Tick();
                guard.Check(colony);
                turned |= Melee.IsInAnAttack(h1) || Melee.IsInAnAttack(h2);
            }
            guard.AssertClean(nameof(HogsTurningOnColonists));
            Controls(nameof(HogsTurningOnColonists), guard, rules, 10);
            Assert.That(turned, Is.True, "the control: no hog ever turned");
        }

        /// <summary>
        /// A rat struck mostly runs; the colonists ordered on it chase, and a bandit fights a third
        /// colonist close by. The runner, the chasers and the brawl beside them all obey the rule.
        /// </summary>
        [Test]
        public void ARatFleeingPastABrawl()
        {
            var colony = Board(3, whiffs: false, out var rules);
            var cs = new List<Pawn>(colony.Pawns.Pawns.All);
            Stand(colony, cs[0], Near(colony, -2, 0));
            Stand(colony, cs[1], Near(colony, -2, 1));
            Stand(colony, cs[2], Near(colony, 0, 3));
            Pawn rat = Spawn(colony, PawnKindIndex.DuctRat, Near(colony, 1, 0));
            Spawn(colony, PawnKindIndex.Bandit, Near(colony, 3, 6));
            Assert.That(Draft(colony, cs[0]), Is.EqualTo(IntentRejection.None));
            Assert.That(Draft(colony, cs[1]), Is.EqualTo(IntentRejection.None));
            Assert.That(Attack(colony, cs[0], rat), Is.EqualTo(IntentRejection.None));
            Assert.That(Attack(colony, cs[1], rat), Is.EqualTo(IntentRejection.None));

            var guard = new Guard();
            bool fled = false;
            for (int t = 0; t < 2_000; t++)
            {
                colony.World.Tick();
                guard.Check(colony);
                fled |= rat.CurrentJob?.DefIndex == JobIndex.Flee;
            }
            guard.AssertClean(nameof(ARatFleeingPastABrawl));
            Controls(nameof(ARatFleeingPastABrawl), guard, rules, 5);
            Assert.That(fled, Is.True, "the control: the rat never ran");
        }

        /// <summary>
        /// A downed body somebody is finishing off holds its cell: a bandit coming for the
        /// colonist beside it takes a side of her that is not the body. From the west, the body's
        /// cell is the nearest side of her, and it is what the bandit stood on before §8c.
        /// </summary>
        [Test]
        public void ABodyBeingFinishedOffIsNobodysSide()
        {
            var colony = Board(2, whiffs: true, out var rules);
            var all = colony.Pawns.Pawns.All;
            Pawn c = all[0], a = all[1];
            Stand(colony, c, Near(colony, 0, 0));
            Stand(colony, a, Near(colony, -1, -4));
            Pawn body = Spawn(colony, PawnKindIndex.Bandit, Near(colony, -1, 0));
            Pawn m = Spawn(colony, PawnKindIndex.Bandit, Near(colony, -5, 0));
            Strike(colony, a, body, body.HpMilli);
            Assert.That(body.Downed, Is.True, "the control: the body is down");

            // She holds where she stands, and the bandit remembers she struck it, so it comes for her.
            Assert.That(Draft(colony, c), Is.EqualTo(IntentRejection.None));
            Strike(colony, c, m, 1);
            Assert.That(Draft(colony, a), Is.EqualTo(IntentRejection.None));
            Assert.That(Attack(colony, a, body), Is.EqualTo(IntentRejection.None));

            var guard = new Guard();
            int atHer = 0;
            for (int t = 0; t < 900; t++)
            {
                colony.World.Tick();
                guard.Check(colony);
                if (m.CombatTarget == c.Id.Value && m.Destination < 0 && Melee.InReach(colony.Pawns, m, c, m.Species.traverseMode)) atHer++;
            }
            guard.AssertClean(nameof(ABodyBeingFinishedOffIsNobodysSide));
            Controls(nameof(ABodyBeingFinishedOffIsNobodysSide), guard, rules, 10);
            Assert.That(atHer, Is.GreaterThan(100), "the control: the bandit never stood at her");
            Assert.That(body.Downed && Melee.IsAttacking(a, body), Is.True, "the control: the body is not being finished off");
        }

        /// <summary>
        /// A drafted colonist sent on to the tile a bandit is swinging from stops beside it: the
        /// move order's spread treats a cell somebody in a fight holds as taken, as it does one
        /// another drafted colonist holds. Before §8c she walked on to it and swung from it.
        /// </summary>
        [Test]
        public void AColonistSentOnToAFighterStopsBesideIt()
        {
            var colony = Board(2, whiffs: true, out var rules);
            var all = colony.Pawns.Pawns.All;
            Pawn c = all[0], sent = all[1];
            Stand(colony, c, Near(colony, 0, 0));
            Stand(colony, sent, Near(colony, 0, 6));
            Assert.That(Draft(colony, c), Is.EqualTo(IntentRejection.None));
            Assert.That(Draft(colony, sent), Is.EqualTo(IntentRejection.None));
            Pawn m = Spawn(colony, PawnKindIndex.Bandit, Near(colony, -6, 0));
            var guard = Run(colony, 400);
            Assume.That(Melee.IsAttacking(m, c) && m.Destination < 0 && Melee.InReach(colony.Pawns, m, c, m.Species.traverseMode),
                Is.True, "the control: the bandit is not at her");

            int held = m.Cell;
            Assert.That(Send(colony, new Intent(IntentKind.OrderMove, Size.FromIndex(held), sent.Id.Value)), Is.EqualTo(IntentRejection.None));
            Assert.That(sent.CurrentJob?.TargetCell, Is.Not.EqualTo(held), "sent on to the bandit's tile");
            Run(colony, 900, guard);
            guard.AssertClean(nameof(AColonistSentOnToAFighterStopsBesideIt));
            Assert.That(sent.Destination < 0 && sent.Cell != m.Cell, Is.True, "she never stopped, or stopped on the bandit");
            Controls(nameof(AColonistSentOnToAFighterStopsBesideIt), guard, rules, 5);
        }

        /// <summary>
        /// Targets on the move: two drafted colonists under attack are ordered away and back, again
        /// and again, and the bandits on them follow and spread again wherever they stop.
        /// </summary>
        [Test]
        public void TargetsThatKeepMoving()
        {
            var colony = Board(2, whiffs: true, out var rules);
            var cs = new List<Pawn>(colony.Pawns.Pawns.All);
            Stand(colony, cs[0], Near(colony, 0, 0));
            Stand(colony, cs[1], Near(colony, 0, 2));
            foreach (Pawn c in cs) Assert.That(Draft(colony, c), Is.EqualTo(IntentRejection.None));
            for (int i = 0; i < 5; i++) Spawn(colony, PawnKindIndex.Bandit, Near(colony, -8, i - 2));

            var guard = Run(colony, 600);
            int[] hops = { 6, -6, 0 };
            for (int round = 0; round < 6; round++)
            {
                for (int k = 0; k < cs.Count; k++)
                {
                    int dx = hops[(round + k) % 3], dz = k * 2 + (round % 2);
                    Assert.That(Send(colony, new Intent(IntentKind.OrderMove, Size.FromIndex(Near(colony, dx, dz)), cs[k].Id.Value)),
                        Is.EqualTo(IntentRejection.None));
                    guard.Check(colony);
                }
                Run(colony, 300, guard);
            }
            guard.AssertClean(nameof(TargetsThatKeepMoving));
            Controls(nameof(TargetsThatKeepMoving), guard, rules, 20);
        }

        /// <summary>
        /// Colonists hunted while going about their day on the bare board — wandering, not drafted —
        /// with a hog and a bandit in the same fight: every mind at once.
        /// </summary>
        [Test]
        public void EveryMindAtOnce()
        {
            var colony = Board(4, whiffs: false, out var rules);
            var cs = new List<Pawn>(colony.Pawns.Pawns.All);
            for (int i = 0; i < cs.Count; i++) Stand(colony, cs[i], Near(colony, i - 2, 0));
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 0, -4));
            Spawn(colony, PawnKindIndex.Bandit, Near(colony, -8, 2));
            Spawn(colony, PawnKindIndex.Bandit, Near(colony, 8, 2));
            Spawn(colony, PawnKindIndex.Bandit, Near(colony, 0, 9));
            Assert.That(Draft(colony, cs[0]), Is.EqualTo(IntentRejection.None));
            Assert.That(Attack(colony, cs[0], hog), Is.EqualTo(IntentRejection.None));
            var guard = Run(colony, 2_500);
            guard.AssertClean(nameof(EveryMindAtOnce));
            Controls(nameof(EveryMindAtOnce), guard, rules, 20);
        }

        /// <summary>
        /// Every landed blow critical and knocking its target back, for a fifth of a point so the
        /// brawl goes on: the knockback's own guard (design 33 §9b) is that it never lands a pawn
        /// on a tile another fighter holds.
        /// </summary>
        sealed class Knockers : MeleeRules
        {
            public int Swings;

            public override SwingOutcome Resolve(Pawn attacker, Pawn defender, in Armament armament, PawnContext ctx, int tick)
            {
                Swings++;
                SwingOutcome o = base.Resolve(attacker, defender, armament, ctx, tick);
                return o.Landed ? new SwingOutcome(CombatEventKind.Hit, 200, 0, critical: true, knockback: true) : o;
            }
        }

        /// <summary>
        /// A knockback never stands two fighters on one tile (design 33 §9b): four brawl shapes with
        /// every landed blow a critical that knocks back, the rule asserted after every tick, and the
        /// control that pawns really were knocked about. With the knockback's own
        /// <see cref="Melee.Holds"/> check withheld the guard names the tiles (measured).
        /// </summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void KnockbacksNeverStackFighters(int shape)
        {
            var colony = CombatFixture.Board(colonists: shape == 1 ? 4 : 3);
            colony.World.Tick();
            var rules = new Knockers();
            colony.Pawns.MeleeRules = rules;
            var cs = new List<Pawn>(colony.Pawns.Pawns.All);
            switch (shape)
            {
                case 0:
                    Stand(colony, cs[0], Near(colony, 0, 0));
                    Stand(colony, cs[1], Near(colony, 0, 1));
                    Stand(colony, cs[2], Near(colony, 1, 0));
                    for (int i = 0; i < 4; i++) Spawn(colony, PawnKindIndex.Bandit, Near(colony, i % 2 == 0 ? -8 : 8, i - 2));
                    break;
                case 1:
                    for (int i = 0; i < cs.Count; i++) Stand(colony, cs[i], Near(colony, -6, i * 2 - 3));
                    Pawn m = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 4, 0));
                    foreach (Pawn c in cs) Assert.That(Draft(colony, c), Is.EqualTo(IntentRejection.None));
                    foreach (Pawn c in cs) Assert.That(Attack(colony, c, m), Is.EqualTo(IntentRejection.None));
                    break;
                case 2:
                    for (int i = 0; i < cs.Count; i++) Stand(colony, cs[i], Near(colony, 0, i - 1));
                    foreach (Pawn c in cs) Assert.That(Draft(colony, c), Is.EqualTo(IntentRejection.None));
                    for (int i = 0; i < 4; i++) Spawn(colony, PawnKindIndex.Bandit, Near(colony, -9, i - 2));
                    break;
                default:
                    for (int i = 0; i < cs.Count; i++) Stand(colony, cs[i], Near(colony, 0, i * 2 - 2));
                    Pawn h1 = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 5, 0));
                    Pawn h2 = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 5, 2));
                    Spawn(colony, PawnKindIndex.Bandit, Near(colony, -9, 0));
                    foreach (Pawn c in cs) Assert.That(Draft(colony, c), Is.EqualTo(IntentRejection.None));
                    Assert.That(Attack(colony, cs[0], h1), Is.EqualTo(IntentRejection.None));
                    Assert.That(Attack(colony, cs[1], h1), Is.EqualTo(IntentRejection.None));
                    Assert.That(Attack(colony, cs[2], h2), Is.EqualTo(IntentRejection.None));
                    break;
            }

            var guard = new Guard();
            var tape = new Tape();
            for (int t = 0; t < 2_000; t++)
            {
                colony.World.Tick();
                tape.Read(colony);
                guard.Check(colony);
            }
            guard.AssertClean($"{nameof(KnockbacksNeverStackFighters)} shape {shape}");
            Assert.That(tape.Of(CombatEventKind.KnockedBack).Count, Is.GreaterThan(5), "the control: nobody was knocked back");
            Assert.That(rules.Swings, Is.GreaterThan(20), "the control: the fight never happened");
        }

        /// <summary>
        /// The Long tier's sweep: on twelve seeds, a colony of three to six, and a mix of bandits,
        /// hogs and rats dropped round it from every side, some colonists drafted and ordered on to
        /// the nearest animal. Four thousand ticks each, the rule asserted on every one.
        /// </summary>
        [Test, Category("Long")]
        public void MixedBrawlsOnManySeeds()
        {
            int kinds = 0, swings = 0, joining = 0;
            for (uint seed = 1; seed <= 12; seed++)
            {
                var roll = new System.Random((int)seed * 7919);
                int colonists = 3 + roll.Next(4);
                var colony = CombatFixture.Board(colonists: colonists, seed: seed);
                colony.World.Tick();
                var rules = new Counted();
                colony.Pawns.MeleeRules = rules;
                var cs = new List<Pawn>(colony.Pawns.Pawns.All);
                for (int i = 0; i < cs.Count; i++) Stand(colony, cs[i], Near(colony, roll.Next(-3, 4), roll.Next(-3, 4)));

                var animals = new List<Pawn>();
                int bandits = 1 + roll.Next(colonists);
                for (int i = 0; i < bandits; i++)
                    Spawn(colony, PawnKindIndex.Bandit, Near(colony, roll.Next(-12, 13), roll.Next(0, 2) == 0 ? -12 : 12));
                for (int i = 0; i < 1 + roll.Next(3); i++)
                    animals.Add(Spawn(colony, roll.Next(0, 2) == 0 ? PawnKindIndex.MiddenHog : PawnKindIndex.DuctRat,
                        Near(colony, roll.Next(-6, 7), roll.Next(-6, 7))));

                int drafted = roll.Next(cs.Count);
                for (int i = 0; i < drafted; i++)
                {
                    Assert.That(Draft(colony, cs[i]), Is.EqualTo(IntentRejection.None));
                    Attack(colony, cs[i], animals[i % animals.Count]);
                }

                var guard = Run(colony, 4_000);
                guard.AssertClean($"seed {seed}");
                kinds |= guard.Kinds;
                swings += rules.Swings;
                joining += guard.JoiningTicks;
            }

            // Drafted colonists help (design 33 §15): the sweep's drafted colonists join the fights
            // round them once their own order is done, so the rule is guarded here too.
            TestContext.WriteLine($"mixed brawls: {swings} swings over twelve seeds; {joining} pawn-ticks joining another colonist's fight");
            Assert.That(swings, Is.GreaterThan(200), "the control: the fights never happened");
            Assert.That(joining, Is.GreaterThan(0), "the control: no drafted colonist joined a fight");
            // Every kind the sweep spawns: the colonist, both animals and the bandit. The gunman
            // (design 50 §8) is a shooter and this is the hand-to-hand guard, so it is not spawned.
            Assert.That(kinds, Is.EqualTo((1 << PawnKindIndex.Gunman) - 1), "the control: not every kind fought");
        }
    }
}
