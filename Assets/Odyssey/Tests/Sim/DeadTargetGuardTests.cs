#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The dead are not targets (design 33 §9e). Owner, 2026-09-23: <i>"You could still attack a pig
    /// after it died — make a guard for this for now — check marauder does this."</i>
    ///
    /// <para><b>The rule:</b> at the end of every tick, nobody carries an attack job against a pawn
    /// that is dead or gone from the board, and no swing, blow, stun, critical or knockback is
    /// published against a pawn after its death was. The one report a dead pawn may still be named
    /// in is the <see cref="CombatEventKind.Miss"/> of a swing already in the air when another
    /// fighter's blow killed it on the same tick: the swing began against the living, and it falls
    /// on air.</para>
    ///
    /// <para>The guard runs fights to the death on several seeds — colonists with machetes against
    /// marauders, colonists against hogs, marauders and hogs together — with a player who keeps
    /// re-ordering every drafted colonist on to the nearest foe, standing or down, the way a player
    /// finishes a downed marauder, and who right-clicks every body the moment it dies. Blows land
    /// at ten times the shipped damage in half the brawls, so pawns die from standing, several
    /// attackers at once, which is where a swing outlives its target.</para>
    /// </summary>
    public class DeadTargetGuardTests
    {
        /// <summary>The shipped rules with every landed blow multiplied: deaths from standing.</summary>
        sealed class Brutal : MeleeRules
        {
            public int Swings;
            public override SwingOutcome Resolve(Pawn attacker, Pawn defender, in Armament armament, PawnContext ctx, int tick)
            {
                Swings++;
                SwingOutcome o = base.Resolve(attacker, defender, armament, ctx, tick);
                return o.Landed ? new SwingOutcome(o.Result, o.DamageMilli * 10, o.StunTicks, o.Critical, o.Knockback) : o;
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
        /// Watches a colony tick by tick for anybody fighting the dead: an attack job whose target
        /// is dead or gone, and a report against a pawn after its death.
        /// </summary>
        internal sealed class DeadGuard
        {
            readonly Tape _tape = new Tape();
            readonly Dictionary<int, int> _diedAt = new Dictionary<int, int>();
            readonly HashSet<int> _seen = new HashSet<int>();
            int _read;

            public readonly List<string> Violations = new List<string>();
            public int Count;
            public int Deaths => _diedAt.Count;
            public IEnumerable<int> Dead => _diedAt.Keys;

            public void Check(ColonyWorld colony)
            {
                _tape.Read(colony);
                int tick = colony.World.CurrentTick - 1;
                for (; _read < _tape.Events.Count; _read++)
                {
                    CombatEventView e = _tape.Events[_read];
                    if (e.Kind == CombatEventKind.Died)
                    {
                        if (!_diedAt.ContainsKey(e.Target.Value)) _diedAt[e.Target.Value] = e.Id;
                        continue;
                    }
                    if (e.Kind == CombatEventKind.Miss) continue;
                    if (!IsAgainst(e.Kind)) continue;
                    if (_diedAt.TryGetValue(e.Target.Value, out int died) && died < e.Id)
                        Flag($"tick {e.Tick}: {e.Kind} by {e.Attacker.Value} against {e.Target.Value}, who died (event {died})");
                }

                var pawns = colony.Pawns.Pawns.All;
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn p = pawns[i];
                    _seen.Add(p.Id.Value);
                    if (p.CurrentJob?.DefIndex != JobIndex.AttackMelee || p.CombatTarget == 0) continue;
                    Pawn? target = colony.Pawns.Pawns.Get(new PawnId(p.CombatTarget));
                    if (target == null)
                        Flag($"tick {tick}: {p.Id.Value} (kind {p.Kind}) keeps an attack job on {p.CombatTarget}, who is gone"
                            + (_diedAt.ContainsKey(p.CombatTarget) ? " (dead)" : ""));
                    else if (Melee.IsDead(target))
                        Flag($"tick {tick}: {p.Id.Value} (kind {p.Kind}) keeps an attack job on {p.CombatTarget}, who is dead");
                }
            }

            static bool IsAgainst(CombatEventKind kind) =>
                kind == CombatEventKind.Swing || kind == CombatEventKind.Hit || kind == CombatEventKind.Dodge
                || kind == CombatEventKind.Stun || kind == CombatEventKind.Critical || kind == CombatEventKind.KnockedBack
                || kind == CombatEventKind.SwingCritical;

            void Flag(string what)
            {
                Count++;
                if (Violations.Count < 8) Violations.Add(what);
                else if (Violations.Count == 8) Violations.Add("...");
            }

            public void AssertClean(string name) =>
                Assert.That(Violations, Is.Empty, $"{name}: fighting the dead, {Count} times:\n" + string.Join("\n", Violations));
        }

        public enum Mix { ColonistsWithMachetesOnMarauders, ColonistsOnHogs, MaraudersAndHogs }

        static void Arm(ColonyWorld colony, Pawn pawn, int item)
        {
            PawnContext ctx = colony.Pawns;
            int cell = ctx.Items.NearestCellWithSpace(ctx.Cells, pawn.Cell, item, 1, JobDriver.DropSearchRadius);
            ThingId id = ctx.Items.Spawn(item, cell);
            WeaponHand.TakeUp(pawn, ctx.Items.Get(id)!, ctx);
        }

        /// <summary>The nearest foe to a colonist, standing or down: what a player clicks next.</summary>
        static Pawn? NearestFoe(ColonyWorld colony, Pawn c)
        {
            Pawn? best = null;
            int bestD = int.MaxValue;
            var pawns = colony.Pawns.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn o = pawns[i];
                if (o.IsColonist || Melee.IsDead(o)) continue;
                int d = colony.Pawns.Distance(c.Cell, o.Cell);
                if (d < bestD) { best = o; bestD = d; }
            }
            return best;
        }

        /// <summary>
        /// One brawl to the death, the guard asserted after every tick. Returns the guard, the swings
        /// decided, and how many of the dead-pawn orders the player sent were refused.
        /// </summary>
        internal static (DeadGuard guard, int swings, int deadOrders, int refused) Brawl(Mix mix, uint seed, bool brutal, int ticks = 3_000)
        {
            var roll = new System.Random((int)seed * 104_729 + (int)mix);
            int colonists = mix == Mix.MaraudersAndHogs ? 1 : 3 + roll.Next(2);
            var colony = CombatFixture.Board(colonists: colonists, seed: seed);
            colony.World.Tick();
            MeleeRules rules = brutal ? new Brutal() : new Counted();
            colony.Pawns.MeleeRules = rules;

            var cs = new List<Pawn>(colony.Pawns.Pawns.All);
            for (int i = 0; i < cs.Count; i++)
            {
                Stand(colony, cs[i], Near(colony, roll.Next(-2, 3), i * 2 - 2));
                Arm(colony, cs[i], ItemHandle.Machete);
            }

            switch (mix)
            {
                case Mix.ColonistsWithMachetesOnMarauders:
                    for (int i = 0; i < 2 + roll.Next(2); i++)
                        Spawn(colony, PawnKindIndex.Marauder, Near(colony, roll.Next(-10, 11), roll.Next(0, 2) == 0 ? -10 : 10));
                    break;
                case Mix.ColonistsOnHogs:
                    for (int i = 0; i < 2 + roll.Next(2); i++)
                        Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, roll.Next(-6, 7), roll.Next(3, 7)));
                    break;
                case Mix.MaraudersAndHogs:
                    // The one colonist is sent far off; the marauders hunt her, and the hogs are
                    // set on the marauders by a blow each, so hog and marauder fight to the death.
                    Stand(colony, cs[0], Near(colony, 20, 20));
                    var ms = new List<Pawn>();
                    for (int i = 0; i < 2; i++) ms.Add(Spawn(colony, PawnKindIndex.Marauder, Near(colony, -4 + i * 8, 0)));
                    for (int i = 0; i < 3; i++)
                    {
                        // A blow a tick until it turns: a hog that fails its roll runs instead.
                        Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, -3 + i * 3, 2));
                        for (int k = 0; k < 20 && hog.RetaliateAgainst == 0; k++)
                        {
                            colony.Pawns.Combat!.ApplySwing(ms[i % 2], hog, Fists(colony.Pawns), Blow(1), colony.World.CurrentTick);
                            colony.World.Tick();
                        }
                    }
                    break;
            }

            var guard = new DeadGuard();
            bool player = mix != Mix.MaraudersAndHogs;
            if (player)
                foreach (Pawn c in cs) Assert.That(Draft(colony, c), Is.EqualTo(IntentRejection.None));

            int deadOrders = 0, refused = 0;
            var ordered = new HashSet<int>();
            for (int t = 0; t < ticks; t++)
            {
                if (player && t % 20 == 0)
                {
                    colony.World.Intents.ClearRejected();
                    for (int i = 0; i < cs.Count; i++)
                    {
                        Pawn c = cs[i];
                        if (colony.Pawns.Pawns.Get(c.Id) != c || c.Downed || !c.Drafted) continue;
                        if (c.CurrentJob?.DefIndex == JobIndex.AttackMelee) continue;
                        Pawn? foe = NearestFoe(colony, c);
                        if (foe != null)
                            colony.World.Intents.Submit(new Intent(IntentKind.OrderAttack, Size.FromIndex(foe.Cell), c.Id.Value, foe.Id.Value));
                    }
                    // And a click on every body, the tick after it fell: the order the owner gave.
                    foreach (int dead in guard.Dead)
                    {
                        if (!ordered.Add(dead)) continue;
                        foreach (Pawn c in cs)
                        {
                            if (colony.Pawns.Pawns.Get(c.Id) != c || !c.Drafted) continue;
                            deadOrders++;
                            colony.World.Intents.Submit(new Intent(IntentKind.OrderAttack, default, c.Id.Value, dead));
                            break;
                        }
                    }
                }
                colony.World.Tick();
                if (player && t % 20 == 0)
                    foreach (var r in colony.World.Intents.Rejected)
                        if (r.Intent.Kind == IntentKind.OrderAttack && ordered.Contains(r.Intent.B) && r.Reason == IntentRejection.NotPermitted)
                            refused++;
                guard.Check(colony);
            }

            int swings = rules is Brutal b ? b.Swings : ((Counted)rules).Swings;
            return (guard, swings, deadOrders, refused);
        }

        [TestCase(Mix.ColonistsWithMachetesOnMarauders, 1u, false)]
        [TestCase(Mix.ColonistsWithMachetesOnMarauders, 2u, true)]
        [TestCase(Mix.ColonistsOnHogs, 3u, false)]
        [TestCase(Mix.ColonistsOnHogs, 4u, true)]
        [TestCase(Mix.MaraudersAndHogs, 5u, true)]
        [TestCase(Mix.MaraudersAndHogs, 6u, true)]
        public void NobodyFightsTheDead(Mix mix, uint seed, bool brutal)
        {
            var (guard, swings, deadOrders, refused) = Brawl(mix, seed, brutal);
            guard.AssertClean($"{mix} seed {seed}");
            Assert.That(swings, Is.GreaterThan(brutal ? 2 : 10), "the control: the fight never happened");
            Assert.That(guard.Deaths, Is.GreaterThan(0), "the control: nobody died");
            Assert.That(refused, Is.EqualTo(deadOrders), "an attack order on the dead was taken");
        }

        /// <summary>
        /// The Long tier's sweep of the same guard: every mix on eight more seeds, shipped and
        /// brutal blows alternating, 4,000 ticks each.
        /// </summary>
        [Test, Category("Long")]
        public void NobodyFightsTheDeadOnManySeeds()
        {
            int deaths = 0;
            for (uint seed = 11; seed <= 18; seed++)
            foreach (Mix mix in new[] { Mix.ColonistsWithMachetesOnMarauders, Mix.ColonistsOnHogs, Mix.MaraudersAndHogs })
            {
                bool brutal = mix == Mix.MaraudersAndHogs || seed % 2 == 0;
                var (guard, _, deadOrders, refused) = Brawl(mix, seed, brutal, ticks: 4_000);
                guard.AssertClean($"{mix} seed {seed}");
                Assert.That(refused, Is.EqualTo(deadOrders), $"{mix} seed {seed}: an attack order on the dead was taken");
                deaths += guard.Deaths;
            }
            Assert.That(deaths, Is.GreaterThan(24), "the control: too few deaths for the guard to have been tested");
        }

        /// <summary>Kill <paramref name="victim"/> now, between ticks: dead at once, gone at the end of the next tick.</summary>
        static void Kill(ColonyWorld colony, Pawn by, Pawn victim) =>
            Strike(colony, by, victim, victim.HpMilli - victim.DeathAtMilli);

        /// <summary>
        /// The attack order is refused on the dead and on the gone (design 33 §9e). A death is
        /// carried out at the end of the tick, so a pawn killed between ticks is still on the board,
        /// dead, when the next tick's orders are read. Then it is gone, and the order names nobody;
        /// the corpse is not a pawn, so naming it is the same. The control is the same order on the
        /// living.
        /// </summary>
        [Test]
        public void TheOrderIsRefusedOnTheDeadAndTheGone()
        {
            var colony = CombatFixture.Board(colonists: 2);
            colony.World.Tick();
            Pawn c = colony.Pawns.Pawns.All[0], other = colony.Pawns.Pawns.All[1];
            Stand(colony, c, Near(colony, 0, 0));
            Assert.That(Draft(colony, c), Is.EqualTo(IntentRejection.None));
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 3, 0));
            Pawn pig = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, -3, 0));
            Assert.That(Attack(colony, c, hog), Is.EqualTo(IntentRejection.None), "the control: a live hog is a target");

            Kill(colony, other, pig);
            Assert.That(colony.Pawns.Pawns.Get(pig.Id), Is.SameAs(pig), "the control: the dead pig is still on the board");
            Assert.That(Attack(colony, c, pig), Is.EqualTo(IntentRejection.NotPermitted), "an attack on a dead pig was taken");
            Assert.That(c.CombatTarget, Is.EqualTo(hog.Id.Value), "the refused order changed her target");

            colony.World.Tick();
            Assert.That(colony.Pawns.Pawns.Get(pig.Id), Is.Null);
            Assert.That(colony.Pawns.Corpses.Count, Is.EqualTo(1));
            Assert.That(colony.Pawns.Corpses[0].Pawn, Is.EqualTo(pig.Id.Value));
            Assert.That(Attack(colony, c, pig), Is.EqualTo(IntentRejection.NotPermitted), "an attack on a pig's corpse was taken");
        }

        /// <summary>
        /// Every target chosen without an order skips the dead (design 33 §9e): the marauder's hunt,
        /// a colonist's self-defence and the threat beside her, an animal's revenge, and the drafted
        /// hold's blow. Each is asked with a pawn killed between ticks — still on the board, dead —
        /// as the thing it would choose; the control is the same question asked while it lived.
        /// </summary>
        [Test]
        public void EveryAutomaticChoiceSkipsTheDead()
        {
            var colony = CombatFixture.Board(colonists: 2);
            colony.World.Tick();
            Pawn c = colony.Pawns.Pawns.All[0], far = colony.Pawns.Pawns.All[1];
            Stand(colony, c, Near(colony, 0, 0));
            Stand(colony, far, Near(colony, 15, 15));
            PawnContext ctx = colony.Pawns;

            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 1, 0));
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 0, 1));
            hog.RetaliateAgainst = c.Id.Value;
            hog.RetaliateUntilTick = colony.World.CurrentTick + 5_000;
            c.RetaliateAgainst = marauder.Id.Value;
            c.RetaliateUntilTick = colony.World.CurrentTick + 5_000;

            Assert.That(new AnimalCombatThinkNode().TryGiveJob(hog, ctx, new Job()), Is.True, "the control: revenge on the living");
            Assert.That(new SelfDefenceThinkNode().TryGiveJob(c, ctx, new Job()), Is.True, "the control: self-defence on the living");
            Assert.That(Melee.AdjacentThreat(ctx, c), Is.SameAs(marauder), "the control: a threat beside her");
            c.Drafted = true;
            Assert.That(new DraftedThinkNode().TryGiveJob(c, ctx, new Job()), Is.True);
            Assert.That(c.CombatTarget, Is.EqualTo(marauder.Id.Value), "the control: the hold strikes the living");
            c.Drafted = false;
            c.CombatTarget = 0;
            hog.CombatTarget = 0;

            // The marauder dies: nothing picks it.
            Kill(colony, far, marauder);
            Assert.That(Melee.IsDead(marauder) && ctx.Pawns.Get(marauder.Id) == marauder, Is.True);
            Assert.That(Melee.AdjacentThreat(ctx, c), Is.Not.SameAs(marauder), "a dead marauder is a threat");
            new SelfDefenceThinkNode().TryGiveJob(c, ctx, new Job());
            Assert.That(c.CombatTarget, Is.Not.EqualTo(marauder.Id.Value), "self-defence chose the dead marauder");
            c.CombatTarget = 0;
            c.Drafted = true;
            var hold = new Job();
            Assert.That(new DraftedThinkNode().TryGiveJob(c, ctx, hold), Is.True);
            Assert.That(hold.DefIndex, Is.EqualTo(JobIndex.DraftHold), "the hold struck the dead marauder");
            c.Drafted = false;
            c.CombatTarget = 0;

            // c dies: the hog's revenge and a marauder's hunt pass her by.
            Pawn hunter = Spawn(colony, PawnKindIndex.Marauder, Near(colony, -2, 0));
            Stand(colony, far, Near(colony, -25, -25));
            Assert.That(new HostileThinkNode().TryGiveJob(hunter, ctx, new Job()), Is.True);
            Assert.That(hunter.CombatTarget, Is.EqualTo(c.Id.Value), "the control: the hunt picks the nearer colonist");
            hunter.CombatTarget = 0;
            Kill(colony, far, c);
            Assert.That(new AnimalCombatThinkNode().TryGiveJob(hog, ctx, new Job()), Is.False, "the hog's revenge chose a dead colonist");
            new HostileThinkNode().TryGiveJob(hunter, ctx, new Job());
            Assert.That(hunter.CombatTarget, Is.Not.EqualTo(c.Id.Value), "the hunt chose a dead colonist");
        }

        /// <summary>
        /// An attack ends the tick its target leaves the board, however it leaves (design 33 §9e):
        /// dead, or a wild animal walking off an edge, through the one despawn. Before the rule the
        /// attacker carried the job to her own next tick. The control is the same attack on a
        /// target that stays.
        /// </summary>
        [Test]
        public void AnAttackEndsTheTickItsTargetLeavesTheBoard()
        {
            var colony = CombatFixture.Board(colonists: 2);
            colony.World.Tick();
            Pawn c = colony.Pawns.Pawns.All[0];
            Stand(colony, c, Near(colony, 0, 0));
            Assert.That(Draft(colony, c), Is.EqualTo(IntentRejection.None));
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 4, 0));
            Assert.That(Attack(colony, c, hog), Is.EqualTo(IntentRejection.None));
            colony.World.Tick(3);
            Assert.That(Melee.IsAttacking(c, hog), Is.True, "the control: the attack goes on while the hog is here");

            colony.Pawns.Pawns.Despawn(hog);
            Assert.That(c.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.AttackMelee), "she still attacks a hog that has gone");
            Assert.That(c.CombatTarget, Is.EqualTo(0));
        }
    }
}
