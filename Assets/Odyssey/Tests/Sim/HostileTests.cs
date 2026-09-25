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
    /// The bandit's hunt and a colonist's self-defence (design 33 §1, §6A): the nearest standing
    /// colonist is hunted and a downed one is not; a colonist struck by a hostile, or by a colonist,
    /// stops what she is doing and fights back; with nobody left standing and nothing built the bandit
    /// idles (with a building, §14b, it breaks that — <c>BuildingTargetTests</c>).
    /// </summary>
    public class HostileTests
    {
        [Test]
        public void ABanditHuntsTheNearestStandingColonistAndIgnoresADownedOne()
        {
            var colony = Board(colonists: 3);
            colony.World.Tick(5);
            Pawn near = colony.Pawns.Pawns.All[0], mid = colony.Pawns.Pawns.All[1], far = colony.Pawns.Pawns.All[2];
            Stand(colony, near, Near(colony, 4, 0));
            Stand(colony, mid, Near(colony, 8, 0));
            Stand(colony, far, Near(colony, 16, 0));
            foreach (Pawn p in new[] { near, mid, far }) Assert.That(Draft(colony, p), Is.EqualTo(IntentRejection.None));

            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 0, 0));
            colony.World.Tick();
            Assert.That(bandit.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee));
            Assert.That(bandit.CombatTarget, Is.EqualTo(near.Id.Value), "the control: the nearest is hunted first");

            Strike(colony, mid, near, near.HpMilli);
            Assert.That(near.Downed, Is.True);
            for (int t = 0; t < 400; t++)
            {
                colony.World.Tick();
                Assert.That(bandit.CombatTarget, Is.Not.EqualTo(near.Id.Value), $"tick {t}: a downed colonist was hunted");
            }
            Assert.That(bandit.CombatTarget, Is.EqualTo(mid.Id.Value), "the next nearest standing colonist was not chosen");
        }

        /// <summary>
        /// With nobody standing and nothing the colony built to break (design 33 §14b: a bandit
        /// with no colonist to reach attacks the base; here there is none — no bed), it idles.
        /// </summary>
        [Test]
        public void WithNobodyStandingAndNothingBuiltABanditIdles()
        {
            var colony = Board(colonists: 1, beds: 0);
            colony.World.Tick(5);
            Pawn only = colony.Pawns.Pawns.All[0];
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 6, 0));
            colony.World.Tick();
            Assert.That(bandit.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee), "the control: it hunts a standing colonist");

            Strike(colony, bandit, only, only.HpMilli);
            colony.World.Tick(400);
            Assert.That(bandit.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.AttackMelee));
            Assert.That(bandit.CombatTarget, Is.EqualTo(0));
        }

        /// <summary>
        /// An undrafted colonist at her work, struck by a bandit, stops and hits back (design 33
        /// §1): the blow interrupts her and her self-defence finds it beside her. The control is
        /// the same colonist before the bandit reached her, doing something else.
        /// </summary>
        [Test]
        public void AColonistStruckByABanditFightsBack()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick(5);
            Pawn her = colony.Pawns.Pawns.All[0];
            var rules = new RecordingRules();
            colony.Pawns.MeleeRules = rules;
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 8, 8));
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
            Assert.That(rules.Swings.Where(s => s.Attacker == her.Id.Value).All(s => s.Target == bandit.Id.Value), Is.True);
        }

        /// <summary>
        /// A colonist struck fights back for the retaliation window (design 33 §1) — by a colonist,
        /// the owner's rule, and by a bandit or an animal alike — and the blow is remembered on her
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
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 12, 12));
            colony.World.Tick();
            Strike(colony, bandit, by, 1_000);
            Assert.That(by.RetaliateAgainst, Is.EqualTo(0), "the control: a drafted colonist remembered the blow");
        }

        /// <summary>
        /// A hunt thinks again after <c>CombatDef.rechooseTicks</c> (§6A), and not before: the
        /// bandit's attack ends at the first step boundary past it and the hunt starts a fresh
        /// one, so a nearer colonist would be noticed. The number moved from a constant on the
        /// driver into the Def at the integration (2026-09-23); nothing tested it before.
        /// </summary>
        [Test]
        public void AHuntThinksAgainAfterTheContentsRechooseTicks()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick(5);
            Pawn her = colony.Pawns.Pawns.All[0];
            Stand(colony, her, Near(colony, 22, 22));
            Assert.That(Draft(colony, her), Is.EqualTo(IntentRejection.None), "drafted, so she holds where she is");

            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, -4, -4));
            colony.World.Tick();
            Assume.That(bandit.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee));
            int started = bandit.JobStartTick;
            int rechoose = colony.Pawns.Content.Combat.rechooseTicks;

            int rethought = -1;
            for (int t = 0; t < rechoose * 3 && rethought < 0; t++)
            {
                colony.World.Tick();
                if (bandit.JobStartTick != started) rethought = bandit.JobStartTick;
            }

            Assert.That(rethought, Is.GreaterThanOrEqualTo(0), "the hunt never thought again");
            Assert.That(rethought - started, Is.GreaterThanOrEqualTo(rechoose), "the hunt thought again early");
            Assert.That(rethought - started, Is.LessThan(rechoose + 120), "the hunt waited well past a step boundary");
            Assert.That(bandit.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee), "with nobody nearer, it hunts her again");
            Assert.That(bandit.CombatTarget, Is.EqualTo(her.Id.Value));
        }

        /// <summary>
        /// A bandit chasing somebody else turns on the colonist who hits it (design 33 §6A), and
        /// the hitter is remembered rather than rediscovered: with two colonists equally near, the
        /// one who struck is the one it fights. Before the fix (review, 2026-09-23) a blow only
        /// interrupted it, the hunt chose the nearest again, and a tie went to the lower id — the
        /// colonist who had not touched it.
        /// </summary>
        [Test]
        public void ABanditChasingSomebodyElseTurnsOnTheColonistWhoHitsIt()
        {
            var colony = Board(colonists: 3);
            colony.World.Tick(5);
            Pawn bystander = colony.Pawns.Pawns.All[0], hitter = colony.Pawns.Pawns.All[1], quarry = colony.Pawns.Pawns.All[2];
            Assume.That(bystander.Id.Value, Is.LessThan(hitter.Id.Value), "the tie goes to the lower id");
            Stand(colony, bystander, Near(colony, 20, 20));
            Stand(colony, hitter, Near(colony, 20, -20));
            Stand(colony, quarry, Near(colony, 8, 0));
            foreach (Pawn p in new[] { bystander, hitter, quarry }) Assert.That(Draft(colony, p), Is.EqualTo(IntentRejection.None));

            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 0, 0));
            colony.World.Tick();
            Assert.That(bandit.CombatTarget, Is.EqualTo(quarry.Id.Value), "the control: it chases the nearest");

            // Stop it where it is, mid-chase, and bring the other two up beside it, one on each
            // side and equally near.
            bandit.ClearPath();
            bandit.Destination = -1;
            bandit.MoveProgress = 0;
            CellRef at = colony.Pawns.Size.FromIndex(bandit.Cell);
            Stand(colony, bystander, colony.Pawns.Cells.NearestWalkableInColumn(at.X, at.Z - 1, at.Y));
            Stand(colony, hitter, colony.Pawns.Cells.NearestWalkableInColumn(at.X, at.Z + 1, at.Y));
            Assume.That(colony.Pawns.Distance(bandit.Cell, bystander.Cell),
                Is.EqualTo(colony.Pawns.Distance(bandit.Cell, hitter.Cell)), "the two are equally near");
            Assume.That(Melee.InReach(colony.Pawns, bandit, quarry, TraverseMode.Colonist), Is.False, "still chasing");

            Strike(colony, hitter, bandit, 1_000);
            Assert.That(bandit.RetaliateAgainst, Is.EqualTo(hitter.Id.Value), "the blow is remembered on the bandit");
            colony.World.Tick(2);

            Assert.That(bandit.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee));
            Assert.That(bandit.CombatTarget, Is.EqualTo(hitter.Id.Value), "it turned on somebody who had not hit it");
        }

        /// <summary>
        /// A bandit already trading blows with a colonist beside it keeps to her when a second
        /// colonist hits it: the swing in the air is not thrown away, and the hitter is remembered
        /// for when she goes down. Before the fix every such blow interrupted the bandit, lost the
        /// swing it had wound up and left it a whole cooldown before it could swing again, so two
        /// colonists could keep a bandit from ever landing a blow (review, 2026-09-23).
        /// </summary>
        [Test]
        public void ABanditInAFightKeepsItsSwingWhenASecondColonistHitsIt()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn engaged = colony.Pawns.Pawns.All[0], hitter = colony.Pawns.Pawns.All[1];
            Stand(colony, engaged, Near(colony, 1, 0));
            Stand(colony, hitter, Near(colony, 20, 20));
            foreach (Pawn p in new[] { engaged, hitter }) Assert.That(Draft(colony, p), Is.EqualTo(IntentRejection.None));
            var rules = new RecordingRules();
            colony.Pawns.MeleeRules = rules;

            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 0, 0));
            var tape = new Tape();
            for (int t = 0; t < 600 && !(bandit.Driver is AttackMeleeJobDriver { InWindup: true }); t++) tape.Tick(colony, 1);
            Assert.That(bandit.Driver is AttackMeleeJobDriver { InWindup: true }, Is.True, "the bandit never wound up a swing");
            Assert.That(bandit.CombatTarget, Is.EqualTo(engaged.Id.Value));
            int swings = tape.Landed(bandit).Count;
            int started = bandit.JobStartTick;

            CellRef at = colony.Pawns.Size.FromIndex(bandit.Cell);
            Stand(colony, hitter, colony.Pawns.Cells.NearestWalkableInColumn(at.X, at.Z + 1, at.Y));
            Strike(colony, hitter, bandit, 1_000);

            Assert.That(bandit.JobStartTick, Is.EqualTo(started), "the blow ended the bandit's attack");
            Assert.That(bandit.Driver is AttackMeleeJobDriver { InWindup: true }, Is.True, "the swing in the air was lost");
            Assert.That(bandit.CombatTarget, Is.EqualTo(engaged.Id.Value), "it let go of the colonist beside it");
            Assert.That(bandit.RetaliateAgainst, Is.EqualTo(hitter.Id.Value), "the hitter is remembered");

            int held = colony.Pawns.Items.Get(new ThingId(bandit.EquippedItem))!.DefIndex;
            int windup = colony.Pawns.Content.Items[held].weapon!.windupTicks;
            for (int t = 0; t <= windup + 1 && tape.Landed(bandit).Count == swings; t++) tape.Tick(colony, 1);
            Assert.That(tape.Landed(bandit).Count, Is.EqualTo(swings + 1), "the swing it had wound up never landed");

            // She goes down: the one who hit it is next, as remembered.
            Strike(colony, bandit, engaged, engaged.HpMilli);
            colony.World.Tick(2);
            Assert.That(bandit.CombatTarget, Is.EqualTo(hitter.Id.Value));
        }
    }
}
