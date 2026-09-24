#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Design 33 §18. The owner, 2026-09-24, on the lock-on ring: <i>"Update the depending on the
    /// correct action"</i> — the ring means an attack the player ordered. And on undrafted colonists
    /// helping: <i>"Maybe a setting to configure this"</i> — each colonist's response, Fight back,
    /// Defend or Flee.
    /// </summary>
    public class ResponseTests
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

            public int By(Pawn attacker) => Swings.FindAll(s => s.Attacker == attacker.Id.Value).Count;
        }

        /// <summary>A tick with the named colonists kept fed, rested and unbroken: none of that is under test.</summary>
        static void Tick(ColonyWorld colony, params Pawn[] keepWhole)
        {
            foreach (Pawn p in keepWhole)
            {
                p.BreakTicksLeft = 0;
                for (int n = 0; n < NeedIndex.Count; n++) p.Needs[n] = 800;
            }
            colony.World.Tick();
        }

        static IntentRejection Respond(ColonyWorld colony, Pawn pawn, HostilityResponse response) =>
            Send(colony, new Intent(IntentKind.SetHostilityResponse, default, pawn.Id.Value, (int)response));

        /// <summary>
        /// Put her on a long job nobody forced — a twenty-thousand-tick wait, standing in for work —
        /// so the only thing that can take her off it is the response's notice: the tree runs only
        /// between jobs.
        /// </summary>
        static void Busy(ColonyWorld colony, Pawn pawn)
        {
            colony.Jobs.EndJob(pawn, JobStatus.Failed);
            Job job = pawn.JobBuffer;
            job.Reset(JobIndex.Wait);
            job.WorkTicks = 20_000;
            Assert.That(colony.Jobs.StartJob(pawn, job, colony.World.CurrentTick), Is.True);
        }

        static bool OnTheLongWait(Pawn pawn) =>
            pawn.CurrentJob is { DefIndex: JobIndex.Wait } job && job.WorkTicks == 20_000;

        static bool IsJoining(Pawn pawn, Pawn attacker) =>
            pawn.CurrentJob is { DefIndex: JobIndex.AttackMelee } job
            && job.DestCell == AttackMeleeJobDriver.Joining && pawn.CombatTarget == attacker.Id.Value;

        static bool Published(ColonyWorld colony, Pawn pawn, AspectKey key, out int value) =>
            colony.World.Views.Current.TryGetPawnAspect(pawn.Id, key, out value);

        /// <summary>
        /// A victim drafted on the start, so she holds and the fight stays put; a helper
        /// <paramref name="away"/> cells west, undrafted, at <paramref name="response"/> and busy; and
        /// every swing a miss. The marauder, four cells east of the victim, is the caller's.
        /// </summary>
        static (ColonyWorld colony, Pawn helper, Pawn victim, Whiffs rules) Scene(int away, HostilityResponse response)
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            var rules = new Whiffs();
            colony.Pawns.MeleeRules = rules;
            Pawn helper = colony.Pawns.Pawns.All[0], victim = colony.Pawns.Pawns.All[1];
            Stand(colony, victim, Near(colony, 0, 0));
            Stand(colony, helper, Near(colony, -away, 0));
            Assert.That(Draft(colony, victim), Is.EqualTo(IntentRejection.None));
            if (response != HostilityResponse.FightBack)
                Assert.That(Respond(colony, helper, response), Is.EqualTo(IntentRejection.None));
            Busy(colony, helper);
            return (colony, helper, victim, rules);
        }

        // ---- the ring means an order (§18b) --------------------------------------------------------

        /// <summary>
        /// The order's target is published for an attack the player ordered and for nothing else. The
        /// ordered attack is the control; each of the four fights a colonist starts herself has her
        /// on the attack job with the target on her, and publishes no order target.
        /// </summary>
        [TestCase("ordered")]
        [TestCase("hold")]
        [TestCase("struck")]
        [TestCase("joined")]
        [TestCase("defend")]
        public void OnlyAnOrderedAttackPublishesItsTarget(string fight)
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            colony.Pawns.MeleeRules = new Whiffs();
            Pawn a = colony.Pawns.Pawns.All[0], victim = colony.Pawns.Pawns.All[1];
            Stand(colony, victim, Near(colony, 0, 0));
            Pawn marauder;

            switch (fight)
            {
                case "ordered":
                    Stand(colony, a, Near(colony, -3, 0));
                    marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 8, 0));
                    Assert.That(Draft(colony, a), Is.EqualTo(IntentRejection.None));
                    Assert.That(Attack(colony, a, marauder), Is.EqualTo(IntentRejection.None));
                    break;
                case "hold":
                    // Drafted, a marauder beside her: the hold strikes it, of her own accord.
                    Stand(colony, a, Near(colony, -8, 0));
                    Assert.That(Draft(colony, a), Is.EqualTo(IntentRejection.None));
                    marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, -7, 0));
                    break;
                case "struck":
                    Stand(colony, a, Near(colony, -8, 0));
                    marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, -7, 0));
                    Strike(colony, marauder, a, 1_000);
                    break;
                case "joined":
                    Stand(colony, a, Near(colony, -5, 0));
                    Assert.That(Draft(colony, victim), Is.EqualTo(IntentRejection.None));
                    Assert.That(Draft(colony, a), Is.EqualTo(IntentRejection.None));
                    marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 4, 0));
                    break;
                default:
                    Stand(colony, a, Near(colony, -5, 0));
                    Assert.That(Draft(colony, victim), Is.EqualTo(IntentRejection.None));
                    Assert.That(Respond(colony, a, HostilityResponse.Defend), Is.EqualTo(IntentRejection.None));
                    marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 4, 0));
                    break;
            }

            for (int t = 0; t < 1_500 && a.CombatTarget != marauder.Id.Value; t++) Tick(colony, a, victim);
            Tick(colony, a, victim);
            Assert.That(a.CombatTarget, Is.EqualTo(marauder.Id.Value), "the control: she never took the marauder on");
            Assert.That(a.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee));

            bool ordered = fight == "ordered";
            Assert.That(a.CurrentJob!.PlayerForced, Is.EqualTo(ordered), "the scene is not the fight it says it is");
            Assert.That(Published(colony, a, CombatAspects.OrderTarget, out int target), Is.EqualTo(ordered),
                ordered ? "the order's target was not published" : "a fight nobody ordered published an order target");
            if (ordered) Assert.That(target, Is.EqualTo(marauder.Id.Value));
            Assert.That(Published(colony, a, CombatAspects.RescuePatient, out _), Is.False);
        }

        /// <summary>
        /// A rescue, ordered or of her own accord, publishes its patient under an aspect of its own
        /// and no order target (§18b): the ring means an attack, and presentation finds the carrier
        /// by the patient.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void ARescuePublishesItsPatientAndNoOrderTarget(bool ordered)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            var colony = ColonyWorld.Build(Size, 7u, scenario, barren: true, wooded: false);
            colony.World.Tick(5);
            var all = colony.Pawns.Pawns.All;
            Pawn rescuer = all[0], patient = all[1], by = all[2];
            foreach (Pawn pawn in all) pawn.WorkPriorities[WorkTypeIndex.Rescue] = 0;
            Stand(colony, patient, Near(colony, 8, 4));
            Strike(colony, by, patient, patient.HpMilli + 1_000);
            Assume.That(patient.Downed, Is.True);

            if (ordered)
            {
                Assert.That(Draft(colony, rescuer), Is.EqualTo(IntentRejection.None));
                Assert.That(Send(colony, new Intent(IntentKind.OrderRescue, Size.FromIndex(patient.Cell),
                    rescuer.Id.Value, patient.Id.Value)), Is.EqualTo(IntentRejection.None));
            }
            else
            {
                rescuer.WorkPriorities[WorkTypeIndex.Rescue] = 1;
            }

            bool carried = false;
            for (int t = 0; t < 3_000 && !carried; t++)
            {
                colony.World.Tick();
                if (rescuer.CurrentJob?.DefIndex != JobIndex.Rescue) continue;
                Assert.That(Published(colony, rescuer, CombatAspects.RescuePatient, out int named), Is.True,
                    $"tick {t}: a rescuer did not publish her patient");
                Assert.That(named, Is.EqualTo(patient.Id.Value));
                Assert.That(Published(colony, rescuer, CombatAspects.OrderTarget, out _), Is.False,
                    $"tick {t}: a rescue published an order target, which the ring would draw");
                carried = patient.CarriedBy == rescuer.Id.Value;
            }
            Assert.That(carried, Is.True, "the control: she was never carried, so the rescue proves nothing");
            Assert.That(rescuer.CurrentJob!.PlayerForced, Is.EqualTo(ordered), "the control: the rescue is not the kind it says");
        }

        // ---- the setting (§18c) ----------------------------------------------------------------------

        /// <summary>The numbers are a save contract and the interface's (<c>ResponseModel</c>): 0, 1, 2.</summary>
        [Test]
        public void TheNumbersAreTheInterfaces()
        {
            Assert.That((int)HostilityResponse.FightBack, Is.EqualTo(0));
            Assert.That((int)HostilityResponse.Defend, Is.EqualTo(1));
            Assert.That((int)HostilityResponse.Flee, Is.EqualTo(2));
            Assert.That(HostilityResponses.Count, Is.EqualTo(3));
        }

        /// <summary>
        /// Fight back by default, and published only once it is not; set by the intent, a no-op quiet,
        /// and refused where it means nothing — a marauder, an animal, a number that is no response.
        /// A drafted colonist may be given one, and keeps her hold.
        /// </summary>
        [Test]
        public void TheIntentSetsItAndRefusesWhatMeansNothing()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            Assert.That(a.Response, Is.EqualTo(HostilityResponse.FightBack));
            Assert.That(Published(colony, a, CombatAspects.Response, out _), Is.False, "the default was published");

            Assert.That(Respond(colony, a, HostilityResponse.Defend), Is.EqualTo(IntentRejection.None));
            Assert.That(a.Response, Is.EqualTo(HostilityResponse.Defend));
            Assert.That(Published(colony, a, CombatAspects.Response, out int shown) && shown == 1, Is.True);
            Assert.That(Respond(colony, a, HostilityResponse.Defend), Is.EqualTo(IntentRejection.AlreadyInThatState));

            Assert.That(Send(colony, new Intent(IntentKind.SetHostilityResponse, default, a.Id.Value, 3)),
                Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(Send(colony, new Intent(IntentKind.SetHostilityResponse, default, a.Id.Value, -1)),
                Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(a.Response, Is.EqualTo(HostilityResponse.Defend), "a refused number changed it");

            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 20, 20));
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, -20, 20));
            Assert.That(Respond(colony, marauder, HostilityResponse.Flee), Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(Respond(colony, hog, HostilityResponse.Flee), Is.EqualTo(IntentRejection.NotPermitted));

            Assert.That(Draft(colony, b), Is.EqualTo(IntentRejection.None));
            Assert.That(Respond(colony, b, HostilityResponse.Flee), Is.EqualTo(IntentRejection.None));
            Assert.That(b.Response, Is.EqualTo(HostilityResponse.Flee));
            Assert.That(b.Drafted, Is.True);
            Assert.That(b.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.DraftHold), "setting a response broke her hold");

            Assert.That(Respond(colony, a, HostilityResponse.FightBack), Is.EqualTo(IntentRejection.None));
            Assert.That(Published(colony, a, CombatAspects.Response, out _), Is.False, "back at the default, still published");
        }

        /// <summary>
        /// It applies while paused (§18c): in the paused set, and landed by a republish that spends no
        /// tick — a button that read one thing while the world did another would be the slab fault.
        /// </summary>
        [Test]
        public void ItAppliesWhilePaused()
        {
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.SetHostilityResponse), Is.True);

            var colony = Board(colonists: 1);
            colony.World.Tick(5);
            Pawn a = colony.Pawns.Pawns.All[0];
            int tick = colony.World.CurrentTick;
            colony.World.Intents.Submit(new Intent(IntentKind.SetHostilityResponse, default, a.Id.Value, (int)HostilityResponse.Flee));
            colony.World.RepublishViews();
            Assert.That(colony.World.CurrentTick, Is.EqualTo(tick), "the control: no tick was spent");
            Assert.That(a.Response, Is.EqualTo(HostilityResponse.Flee), "a paused world did not apply it");
            Assert.That(Published(colony, a, CombatAspects.Response, out int shown) && shown == 2, Is.True,
                "and the paused frame does not show it");
        }

        /// <summary>
        /// Saved and hashed only while it is not the default: a colony that sets one and sets it back
        /// hashes and saves exactly as it did, and Defend and Flee come back from a load.
        /// </summary>
        [Test]
        public void ItIsSavedAndHashedOnlyWhenNotTheDefault()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(60);
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            ulong before = colony.World.ComputeStateHash().Value;
            byte[] saved = colony.Save();

            a.Response = HostilityResponse.Defend;
            Assert.That(colony.World.ComputeStateHash().Value, Is.Not.EqualTo(before), "the hash cannot see Defend");
            a.Response = HostilityResponse.Flee;
            ulong flee = colony.World.ComputeStateHash().Value;
            Assert.That(flee, Is.Not.EqualTo(before), "the hash cannot see Flee");
            a.Response = HostilityResponse.FightBack;
            Assert.That(colony.World.ComputeStateHash().Value, Is.EqualTo(before), "the default hashes differently from before");
            Assert.That(colony.Save(), Is.EqualTo(saved), "the default saves differently from before");

            a.Response = HostilityResponse.Defend;
            b.Response = HostilityResponse.Flee;
            var restored = Board(colonists: 2);
            restored.Load(colony.Save());
            Assert.That(restored.Pawns.Pawns.Get(a.Id)!.Response, Is.EqualTo(HostilityResponse.Defend));
            Assert.That(restored.Pawns.Pawns.Get(b.Id)!.Response, Is.EqualTo(HostilityResponse.Flee));
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));

            colony.World.Tick(300);
            restored.World.Tick(300);
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value),
                "the worlds parted after the load");
        }

        // ---- Defend (§18d) ----------------------------------------------------------------------------

        /// <summary>
        /// The heart of it. Undrafted and busy, five cells from a marauder on a colonist, she leaves
        /// her job, joins the fight exactly as a drafted colonist would — unforced, marked, the
        /// marauder on the victim and out of her reach — swings, and when it is down goes back to
        /// the tree's work. Never drafted. Twelve cells off, she stays on her job.
        /// </summary>
        [TestCase(5, true)]
        [TestCase(12, false)]
        public void DefendJoinsAFightNearbyThenGoesBackToWorkNeverDrafted(int away, bool joins)
        {
            var (colony, helper, victim, rules) = Scene(away, HostilityResponse.Defend);
            for (int t = 0; t < 5; t++) Tick(colony, helper, victim);
            Assert.That(OnTheLongWait(helper), Is.True, "the control: nothing to join, and she is on her job");

            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 4, 0));
            int joinedAt = -1;
            for (int t = 0; t < 1_500; t++)
            {
                Tick(colony, helper, victim);
                Assert.That(helper.Drafted, Is.False, $"tick {t}: she drafted herself");
                if (joinedAt >= 0 || helper.CombatTarget != marauder.Id.Value) continue;
                joinedAt = colony.World.CurrentTick;
                Assert.That(IsJoining(helper, marauder), Is.True, "she took the marauder on, but not as a helper");
                Assert.That(helper.CurrentJob!.PlayerForced, Is.False, "nobody ordered it");
                Assert.That(marauder.CombatTarget, Is.EqualTo(victim.Id.Value), "the marauder was not on the victim");
                Assert.That(Melee.InReach(colony.Pawns, helper, marauder, TraverseMode.Colonist), Is.False,
                    "it was beside her: a threat in reach, not somebody else's fight");
            }

            if (!joins)
            {
                Assert.That(joinedAt, Is.EqualTo(-1), "twelve cells off, she joined");
                Assert.That(OnTheLongWait(helper), Is.True, "twelve cells off, she left her job");
                Assert.That(rules.At(marauder, victim), Is.GreaterThan(0), "the control: the fight happened");
                return;
            }

            Assert.That(joinedAt, Is.GreaterThanOrEqualTo(0), "five cells from the fight, she never joined it");
            Assert.That(rules.At(helper, marauder), Is.GreaterThan(0), "she joined but never swung");

            Strike(colony, victim, marauder, marauder.HpMilli);
            Assume.That(marauder.Downed, Is.True);
            for (int t = 0; t < 30; t++) Tick(colony, helper, victim);
            Assert.That(helper.CurrentJob, Is.Not.Null, "she was given nothing after the fight");
            Assert.That(helper.CurrentJob!.DefIndex, Is.Not.EqualTo(JobIndex.AttackMelee), "she fought on after it was down");
            Assert.That(helper.CombatTarget, Is.EqualTo(0));
            Assert.That(helper.Drafted, Is.False);
        }

        /// <summary>
        /// Fight back is today's behaviour, unchanged: the same scene at the default and she stays on
        /// her job; and a colonist moved from Defend back to Fight back is back to it.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void FightBackLeavesAFightNearbyAlone(bool wasDefend)
        {
            var (colony, helper, victim, rules) = Scene(5, wasDefend ? HostilityResponse.Defend : HostilityResponse.FightBack);
            if (wasDefend) Assert.That(Respond(colony, helper, HostilityResponse.FightBack), Is.EqualTo(IntentRejection.None));
            Busy(colony, helper);
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 4, 0));
            for (int t = 0; t < 1_500; t++)
            {
                Tick(colony, helper, victim);
                Assert.That(helper.CombatTarget, Is.Not.EqualTo(marauder.Id.Value), $"tick {t}: at Fight back, she joined");
            }
            Assert.That(OnTheLongWait(helper), Is.True, "at Fight back, something took her off her job");
            Assert.That(rules.At(marauder, victim), Is.GreaterThan(0), "the control: the fight happened");
        }

        /// <summary>
        /// A fight nearby does not wake a sleeper set to Defend (§18d); she sleeps on. The control is
        /// the same colonist awake, who goes.
        /// </summary>
        [Test]
        public void ASleeperIsNotRousedByAFightNearby()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            var rules = new Whiffs();
            colony.Pawns.MeleeRules = rules;
            Pawn sleeper = colony.Pawns.Pawns.All[0], victim = colony.Pawns.Pawns.All[1];
            Stand(colony, victim, Near(colony, 0, 0));
            Assert.That(Draft(colony, victim), Is.EqualTo(IntentRejection.None));
            Assert.That(Respond(colony, sleeper, HostilityResponse.Defend), Is.EqualTo(IntentRejection.None));

            sleeper.Needs[NeedIndex.Rest] = 50;
            for (int t = 0; t < 4_000 && !sleeper.Asleep; t++) colony.World.Tick();
            Assume.That(sleeper.Asleep, Is.True, "she never fell asleep");
            // The fight is brought to her: the victim, still drafted, three cells from the bed she
            // sleeps in, and the marauder three beyond.
            CellRef at = Size.FromIndex(sleeper.Cell);
            Stand(colony, victim, colony.Pawns.Cells.NearestWalkableInColumn(at.X + 3, at.Z, at.Y));
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, colony.Pawns.Cells.NearestWalkableInColumn(at.X + 6, at.Z, at.Y));

            for (int t = 0; t < 600; t++)
            {
                victim.BreakTicksLeft = 0;
                colony.World.Tick();
                Assert.That(sleeper.CombatTarget, Is.Not.EqualTo(marauder.Id.Value), $"tick {t}: a fight woke her");
            }
            Assert.That(rules.At(marauder, victim), Is.GreaterThan(0), "the control: the fight happened");
            Assert.That(sleeper.Asleep, Is.True, "something else woke her, so the test proves nothing");

            // The control: woken, and at Defend, she goes.
            Busy(colony, sleeper);
            for (int t = 0; t < 600 && sleeper.CombatTarget != marauder.Id.Value; t++) Tick(colony, sleeper, victim);
            Assert.That(sleeper.CombatTarget, Is.EqualTo(marauder.Id.Value), "the control: awake, she did not join");
        }

        /// <summary>
        /// A job the player forced stands (§18d): sent for a weapon across the fight, a Defend colonist
        /// keeps walking to it.
        /// </summary>
        [Test]
        public void APlayersOrderIsNotTurnedAsideByDefend()
        {
            var (colony, helper, victim, rules) = Scene(5, HostilityResponse.Defend);
            int far = Near(colony, -20, 0);
            ThingId blade = colony.Pawns.Items.Spawn(ItemIndex.ArcBlade, far);
            Assert.That(Send(colony, new Intent(IntentKind.OrderEquip, Size.FromIndex(far), helper.Id.Value, blade.Value)),
                Is.EqualTo(IntentRejection.None));
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 4, 0));
            for (int t = 0; t < 400 && helper.CurrentJob?.DefIndex == JobIndex.Equip; t++)
            {
                Tick(colony, helper, victim);
                Assert.That(helper.CombatTarget, Is.EqualTo(0), $"tick {t}: the fight took her off the player's order");
            }
            Assert.That(rules.At(marauder, victim), Is.GreaterThan(0), "the control: the fight happened");
        }

        // ---- Flee (§18d) -----------------------------------------------------------------------------

        /// <summary>
        /// A colonist set to Flee, busy, with a marauder coming for her: she drops her job and runs,
        /// never swinging while she has somewhere to run; when it is down she goes back to work. At
        /// Fight back the same colonist stays on her job until it reaches her — the control.
        /// </summary>
        [TestCase(HostilityResponse.Flee)]
        [TestCase(HostilityResponse.FightBack)]
        public void FleeRunsFromAMarauderNearHerAndGoesBackToWork(HostilityResponse response)
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            var rules = new Whiffs();
            colony.Pawns.MeleeRules = rules;
            Pawn runner = colony.Pawns.Pawns.All[0], other = colony.Pawns.Pawns.All[1];
            Stand(colony, runner, Near(colony, 0, 0));
            Stand(colony, other, Near(colony, -20, -20));
            Assert.That(Draft(colony, other), Is.EqualTo(IntentRejection.None));
            if (response != HostilityResponse.FightBack) Assert.That(Respond(colony, runner, response), Is.EqualTo(IntentRejection.None));
            Busy(colony, runner);
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 7, 0));

            bool fled = false;
            for (int t = 0; t < 30; t++)
            {
                Tick(colony, runner, other);
                fled |= runner.CurrentJob?.DefIndex == JobIndex.Flee;
            }

            if (response == HostilityResponse.FightBack)
            {
                Assert.That(fled, Is.False, "at Fight back, she ran");
                Assert.That(OnTheLongWait(runner), Is.True, "the control: at Fight back she stays on her job till it reaches her");
                return;
            }

            Assert.That(fled, Is.True, "a marauder seven cells off and she did not run");
            int start = Near(colony, 0, 0);
            for (int t = 0; t < 300; t++)
            {
                Tick(colony, runner, other);
                Assert.That(runner.Drafted, Is.False);
            }
            Assert.That(rules.By(runner), Is.EqualTo(0), "with somewhere to run, she swung");
            Assert.That(colony.Pawns.Distance(runner.Cell, start), Is.GreaterThan(0), "she ran nowhere");

            Strike(colony, other, marauder, marauder.HpMilli);
            Assume.That(marauder.Downed, Is.True);
            for (int t = 0; t < 600 && runner.CurrentJob?.DefIndex == JobIndex.Flee; t++) Tick(colony, runner, other);
            Tick(colony, runner, other);
            Assert.That(runner.CurrentJob, Is.Not.Null);
            Assert.That(runner.CurrentJob!.DefIndex, Is.Not.EqualTo(JobIndex.Flee), "she ran on from a marauder that was down");
            Assert.That(runner.CurrentJob.DefIndex, Is.Not.EqualTo(JobIndex.AttackMelee), "she went back to fight it");
        }

        /// <summary>
        /// Struck while on a job the player forced — which the notice leaves alone — a colonist at
        /// Flee runs rather than fighting back; at Fight back, the control, she fights back.
        /// </summary>
        [TestCase(HostilityResponse.Flee, JobIndex.Flee)]
        [TestCase(HostilityResponse.FightBack, JobIndex.AttackMelee)]
        public void StruckSheRunsRatherThanFightingBack(HostilityResponse response, int answer)
        {
            var colony = Board(colonists: 1);
            colony.World.Tick(5);
            colony.Pawns.MeleeRules = new Whiffs();
            Pawn a = colony.Pawns.Pawns.All[0];
            Stand(colony, a, Near(colony, 0, 0));
            if (response != HostilityResponse.FightBack) Assert.That(Respond(colony, a, response), Is.EqualTo(IntentRejection.None));
            int far = Near(colony, -15, 0);
            ThingId blade = colony.Pawns.Items.Spawn(ItemIndex.ArcBlade, far);
            Assert.That(Send(colony, new Intent(IntentKind.OrderEquip, Size.FromIndex(far), a.Id.Value, blade.Value)),
                Is.EqualTo(IntentRejection.None));
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 1, 0));
            Tick(colony, a);
            Assert.That(a.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Equip), "the control: danger beside her, the player's order stands");

            // Struck mid-stride she lands the step she was on before anything else (design 33 §2d).
            Strike(colony, marauder, a, 1_000);
            for (int t = 0; t < 300 && a.CurrentJob == null; t++) Tick(colony, a);
            Assert.That(a.CurrentJob?.DefIndex, Is.EqualTo(answer));
        }

        /// <summary>
        /// Cornered — danger beside her and nowhere to run — a colonist at Flee fights back as Fight
        /// back would. She and the marauder are walled into two cells.
        /// </summary>
        [Test]
        public void CorneredSheFightsBack()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick(5);
            colony.Pawns.MeleeRules = new Whiffs();
            Pawn a = colony.Pawns.Pawns.All[0];
            int home = Near(colony, 0, 0);
            Stand(colony, a, home);
            CellRef at = Size.FromIndex(home);
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 2; dx++)
            {
                if (dz == 0 && (dx == 0 || dx == 1)) continue;
                int cell = Size.Index(at.X + dx, at.Z + dz, at.Y);
                Assert.That(colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Wall, StuffHandle.Wood, 0),
                    Is.EqualTo(IntentRejection.None));
                Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
            }
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Size.Index(at.X + 1, at.Z, at.Y));
            Assert.That(Respond(colony, a, HostilityResponse.Flee), Is.EqualTo(IntentRejection.None));
            Assert.That(FleeJobDriver.FindFleeCell(colony.Pawns, a, marauder.Cell, colony.Pawns.Content.Combat.fleeCells,
                TraverseMode.Colonist), Is.EqualTo(-1), "the control: there is somewhere to run");

            Strike(colony, marauder, a, 1_000);
            Tick(colony, a);
            Assert.That(a.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee), "cornered, she did not fight back");
            Assert.That(a.CombatTarget, Is.EqualTo(marauder.Id.Value));
        }

        /// <summary>
        /// A wild animal at peace is not danger; one attacking a colonist is. A colonist at Flee, busy
        /// three cells from a hog, stays on her job; once it turns on the colonist beside her, she runs.
        /// </summary>
        [Test]
        public void AnAnimalAtPeaceIsNotDangerAndOneOnAColonistIs()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            colony.Pawns.MeleeRules = new Whiffs();
            Pawn runner = colony.Pawns.Pawns.All[0], victim = colony.Pawns.Pawns.All[1];
            Stand(colony, runner, Near(colony, 0, 0));
            Stand(colony, victim, Near(colony, 4, 0));
            Assert.That(Draft(colony, victim), Is.EqualTo(IntentRejection.None));
            Assert.That(Respond(colony, runner, HostilityResponse.Flee), Is.EqualTo(IntentRejection.None));
            Busy(colony, runner);
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 3, 1));

            for (int t = 0; t < 300; t++)
            {
                Tick(colony, runner, victim);
                Assert.That(runner.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.Flee), $"tick {t}: she ran from a hog at peace");
            }
            Assert.That(OnTheLongWait(runner), Is.True);

            hog.RetaliateAgainst = victim.Id.Value;
            hog.RetaliateUntilTick = colony.World.CurrentTick + 100_000;
            colony.Jobs.EndJob(hog, JobStatus.Failed);
            bool fled = false;
            for (int t = 0; t < 120 && !fled; t++)
            {
                Tick(colony, runner, victim);
                fled = runner.CurrentJob?.DefIndex == JobIndex.Flee;
            }
            Assert.That(hog.CombatTarget, Is.EqualTo(victim.Id.Value), "the control: the hog is on the victim");
            Assert.That(fled, Is.True, "a hog attacking the colonist beside her, and she did not run");
        }

        /// <summary>
        /// A new setting answers at once (§18c): a colonist fighting back — an attack she started — set
        /// to Flee leaves the fight and runs, rather than trading blows until it ends.
        /// </summary>
        [Test]
        public void ANewSettingAnswersAtOnce()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick(5);
            colony.Pawns.MeleeRules = new Whiffs();
            Pawn a = colony.Pawns.Pawns.All[0];
            Stand(colony, a, Near(colony, 0, 0));
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 1, 0));
            Strike(colony, marauder, a, 1_000);
            Tick(colony, a);
            Assert.That(a.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee), "the control: she fights back");

            Assert.That(Respond(colony, a, HostilityResponse.Flee), Is.EqualTo(IntentRejection.None));
            Assert.That(a.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Flee), "the fight went on under the new setting");
        }

        /// <summary>
        /// The draft overrides the response (§18c). Drafted, a colonist at Flee with a marauder six
        /// cells off holds — the notice leaves her hold alone, so no job starts under her — and when
        /// it comes beside her she strikes it from her hold, as any drafted colonist does.
        /// </summary>
        [Test]
        public void DraftedSheDoesWhatTheDraftSays()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick(5);
            colony.Pawns.MeleeRules = new Whiffs();
            Pawn a = colony.Pawns.Pawns.All[0];
            Stand(colony, a, Near(colony, 0, 0));
            Assert.That(Respond(colony, a, HostilityResponse.Flee), Is.EqualTo(IntentRejection.None));
            Assert.That(Draft(colony, a), Is.EqualTo(IntentRejection.None));

            // Six cells off and stunned where it stands: danger near her, and nothing in reach.
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 6, 0));
            Tick(colony, a);
            marauder.StunnedUntilTick = colony.World.CurrentTick + 100_000;
            Tick(colony, a);
            int started = colony.Jobs.JobsStarted;
            for (int t = 0; t < 60; t++)
            {
                Tick(colony, a);
                Assert.That(a.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.DraftHold), $"tick {t}: drafted, she left her hold");
            }
            Assert.That(colony.Jobs.JobsStarted, Is.EqualTo(started), "her response kept ending her hold");

            marauder.StunnedUntilTick = 0;
            Stand(colony, marauder, Near(colony, 1, 0));
            for (int t = 0; t < 30; t++)
            {
                Tick(colony, a);
                Assert.That(a.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.Flee), $"tick {t}: drafted, she ran");
            }
            Assert.That(a.CombatTarget, Is.EqualTo(marauder.Id.Value), "drafted, she did not strike the marauder beside her");
        }

        /// <summary>
        /// Only danger that can reach her counts (§18d): behind a shut door a marauder four cells off
        /// does not keep her off work. The control is the same room with an empty doorway.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void AMarauderBehindAShutDoorIsNotDanger(bool open)
        {
            var colony = Board(colonists: 1, beds: 0);
            colony.World.Tick(5);
            colony.Pawns.MeleeRules = new Whiffs();
            Pawn a = colony.Pawns.Pawns.All[0];
            int home = Near(colony, 0, 0);
            Stand(colony, a, home);
            CellRef at = Size.FromIndex(home);
            int door = -1;
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int cell = Size.Index(at.X + dx, at.Z + dz, at.Y);
                bool isDoor = (dx, dz) == (1, 0);
                if (isDoor) door = cell;
                if (isDoor && open) continue;
                Assert.That(colony.Construction.Place(Size.FromIndex(cell), isDoor ? BuildingHandle.Door : BuildingHandle.Wall,
                    StuffHandle.Wood, 0), Is.EqualTo(IntentRejection.None));
                Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
            }
            colony.World.Tick();
            Assert.That(Respond(colony, a, HostilityResponse.Flee), Is.EqualTo(IntentRejection.None));
            Busy(colony, a);

            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Size.Index(at.X + 5, at.Z, at.Y));
            Tick(colony, a);
            // Stunned where it stands, so neither the door nor her is ever reached: what is under
            // test is whether it is danger from there.
            marauder.StunnedUntilTick = colony.World.CurrentTick + 100_000;
            Assert.That(colony.Pawns.Reachable(marauder, a.Cell, marauder.OwnMode), Is.EqualTo(open),
                "the control: the doorway is not what the test says it is");
            Assert.That(door, Is.GreaterThanOrEqualTo(0));

            bool fled = false;
            for (int t = 0; t < 60; t++)
            {
                Tick(colony, a);
                fled |= a.CurrentJob?.DefIndex == JobIndex.Flee;
            }
            Assert.That(fled, Is.EqualTo(open), open ? "the door open and she did not run" : "a shut door, and she ran");
        }

        /// <summary>
        /// At Flee she does not go back for whoever struck her (§18d): run clear of a marauder that
        /// hit her, with it standing out of range, she goes back to work rather than walking back to
        /// fight it with the blow still remembered. At Fight back, the control, she goes for it.
        /// </summary>
        [TestCase(HostilityResponse.Flee)]
        [TestCase(HostilityResponse.FightBack)]
        public void SheDoesNotGoBackForWhoeverStruckHer(HostilityResponse response)
        {
            var colony = Board(colonists: 1);
            colony.World.Tick(5);
            colony.Pawns.MeleeRules = new Whiffs();
            Pawn a = colony.Pawns.Pawns.All[0];
            Stand(colony, a, Near(colony, 0, 0));
            if (response != HostilityResponse.FightBack) Assert.That(Respond(colony, a, response), Is.EqualTo(IntentRejection.None));
            Busy(colony, a);
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 20, 0));
            Tick(colony, a);
            // Stunned twenty cells off, out of range: nothing is danger to her, and the blow is
            // remembered (Strike lands it as if from there).
            marauder.StunnedUntilTick = colony.World.CurrentTick + 100_000;
            Strike(colony, marauder, a, 1_000);
            Assert.That(a.RetaliateAgainst, Is.EqualTo(marauder.Id.Value), "the control: she remembers the blow");

            bool went = false;
            for (int t = 0; t < 300; t++)
            {
                Tick(colony, a);
                went |= a.CombatTarget == marauder.Id.Value;
            }
            Assert.That(went, Is.EqualTo(response == HostilityResponse.FightBack),
                response == HostilityResponse.FightBack ? "the control: at Fight back she did not go for it" : "at Flee, she went back to fight");
        }

        /// <summary>
        /// The notice is gated on anything hostile being about, found once a tick and forgotten when
        /// the next begins: a colonist at Defend whose first asking found nothing still notices the
        /// marauder that comes later. (The first Defend test is the same claim in the round; this is
        /// the gate's own.)
        /// </summary>
        [Test]
        public void TheGateIsAskedAgainEachTick()
        {
            var (colony, helper, victim, rules) = Scene(5, HostilityResponse.Defend);
            for (int t = 0; t < 50; t++) Tick(colony, helper, victim);
            Assert.That(OnTheLongWait(helper), Is.True, "the control: nothing hostile, she works");
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 4, 0));
            for (int t = 0; t < 1_500 && helper.CombatTarget != marauder.Id.Value; t++) Tick(colony, helper, victim);
            Assert.That(helper.CombatTarget, Is.EqualTo(marauder.Id.Value), "the gate kept its first answer");
        }
    }
}
