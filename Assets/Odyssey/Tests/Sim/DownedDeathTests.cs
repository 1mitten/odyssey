#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Going down and dying (design 33 §1, §3, §5j, §6A): down at nought, dead at minus half the
    /// pool, the corpse, the hooks once each, the death carried out at the end of the tick and the
    /// pawn then gone from every loop, a broken colonist's break ended by the fall, and a dead
    /// colonist's bed given back. The blows are landed through <see cref="CombatSystem.ApplySwing"/>
    /// itself, exactly, so each threshold is met to the thousandth; the control beside each is
    /// the same blow one thousandth short.
    /// </summary>
    public class DownedDeathTests
    {
        /// <summary>
        /// The pool's own lines, on a bodiless board (design 43 §8): with a body, pain shock downs
        /// a person at 64 points, before nought, and <c>HealthRulesTests</c> holds that line.
        /// </summary>
        static (ColonyWorld colony, Pawn victim, Pawn by) Two()
        {
            var colony = Bodiless(Board());
            colony.World.Tick(5);
            return (colony, colony.Pawns.Pawns.All[0], colony.Pawns.Pawns.All[1]);
        }

        [Test]
        public void DownAtNoughtAndNotBefore()
        {
            var (colony, victim, by) = Two();
            Assert.That(Draft(colony, victim), Is.EqualTo(IntentRejection.None));

            Strike(colony, by, victim, victim.HpMilli - 1);
            Assert.That(victim.Downed, Is.False, "the control: one thousandth left is standing");
            Assert.That(victim.Drafted, Is.True);

            Strike(colony, by, victim, 1);
            Assert.That(victim.HpMilli, Is.EqualTo(0));
            Assert.That(victim.Downed, Is.True);
            Assert.That(victim.Drafted, Is.False, "going down ends the draft");
            Assert.That(victim.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Downed), "Job_Downed starts in the same call");

            colony.World.Tick(600);
            Assert.That(victim.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Downed), "she got up, or did something else");
            Assert.That(colony.Pawns.Pawns.Get(victim.Id), Is.SameAs(victim), "a downed colonist is not dead");
        }

        [Test]
        public void DeadAtMinusHalfThePoolAndNotBefore()
        {
            var (colony, victim, by) = Two();
            Strike(colony, by, victim, victim.HpMilli);
            Strike(colony, by, victim, 49_999);
            colony.World.Tick();
            Assert.That(colony.Pawns.Pawns.Get(victim.Id), Is.SameAs(victim), "the control: at -49.999 she lives");
            Assert.That(colony.Pawns.Corpses.Count, Is.EqualTo(0));

            Strike(colony, by, victim, 1);
            colony.World.Tick();
            Assert.That(colony.Pawns.Pawns.Get(victim.Id), Is.Null, "at -50 she is still on the board");
            Assert.That(colony.Pawns.Corpses.Count, Is.EqualTo(1));

            Corpse corpse = colony.Pawns.Corpses[0];
            Assert.That(corpse.Pawn, Is.EqualTo(victim.Id.Value));
            Assert.That(corpse.Kind, Is.EqualTo(victim.Kind));
            Assert.That(corpse.RollSeed, Is.EqualTo(victim.RollSeed));
            Assert.That(corpse.Cell, Is.EqualTo(victim.Cell));

            // Published beside the Died moment, in the same frame.
            var view = colony.World.Views.Current;
            Assert.That(view.Corpses.Length, Is.EqualTo(1));
            Assert.That(view.Corpses[0].Pawn, Is.EqualTo(victim.Id));
        }

        /// <summary>
        /// A single blow from standing to past the line kills without a fall: the rat that loses 15
        /// of its 15 and more is dead, not downed and then dead. The hooks hear it once each way.
        /// </summary>
        [Test]
        public void TheHooksAreHeardOnceEach()
        {
            var (colony, victim, by) = Two();
            var hooks = new HookCounter();
            colony.Pawns.CombatHooks.Add(hooks);

            Strike(colony, by, victim, 40_000);
            Strike(colony, by, victim, 60_000);
            Strike(colony, by, victim, 20_000);
            Strike(colony, by, victim, 40_000);
            // Past the line already: a further blow on the same tick kills nobody twice.
            Strike(colony, by, victim, 5_000);
            colony.World.Tick();

            Assert.That(hooks.DamageCount, Is.EqualTo(5));
            Assert.That(hooks.DownedCount, Is.EqualTo(1));
            Assert.That(hooks.DiedCount, Is.EqualTo(1));
            Assert.That(hooks.LastCorpse, Is.EqualTo(colony.Pawns.Corpses[0].Id));
            Assert.That(hooks.Heard.Last(), Is.EqualTo("died:" + victim.Id.Value));

            Pawn rat = Spawn(colony, PawnKindIndex.DuctRat, Near(colony, 6, 6));
            colony.World.Tick();
            Strike(colony, by, rat, 23_000);
            colony.World.Tick();
            Assert.That(hooks.DownedCount, Is.EqualTo(1), "a pawn killed outright was reported downed as well");
            Assert.That(hooks.DiedCount, Is.EqualTo(2));
        }

        /// <summary>
        /// Death is carried out at the end of a tick, never inside a loop over the pawns: struck
        /// dead between ticks, she is still in the registry, and the next tick's deferred phase
        /// removes her. Measured against an immediate removal, which fails the first assertion.
        /// </summary>
        [Test]
        public void ADeathIsDeferredToTheEndOfTheTick()
        {
            var (colony, victim, by) = Two();
            Strike(colony, by, victim, 150_000);
            Assert.That(Melee.IsDead(victim), Is.True);
            Assert.That(colony.Pawns.Pawns.Get(victim.Id), Is.SameAs(victim), "removed inside the blow");
            Assert.That(colony.Pawns.Corpses.Count, Is.EqualTo(0));

            colony.World.Tick();
            Assert.That(colony.Pawns.Pawns.Get(victim.Id), Is.Null);
            Assert.That(colony.Pawns.Corpses.Count, Is.EqualTo(1));
        }

        /// <summary>
        /// Gone from every per-pawn loop: not in the list, not published, holding nothing, and the
        /// colonist who was fighting her stops — her order ends when its target is gone.
        /// </summary>
        [Test]
        public void TheDeadAreGoneFromEveryLoop()
        {
            var (colony, victim, by) = Two();
            Assert.That(Draft(colony, by), Is.EqualTo(IntentRejection.None));
            Assert.That(Attack(colony, by, victim), Is.EqualTo(IntentRejection.None));
            Assert.That(by.CombatTarget, Is.EqualTo(victim.Id.Value), "the control: she is somebody's target");

            Strike(colony, by, victim, 150_000);
            // She may be part way through a step, which she lands before her job does anything.
            colony.World.Tick(200);

            Assert.That(colony.Pawns.Pawns.All.Contains(victim), Is.False);
            var view = colony.World.Views.Current;
            for (int i = 0; i < view.Pawns.Length; i++)
                Assert.That(view.Pawns[i].Id, Is.Not.EqualTo(victim.Id), "the dead are still published");
            Assert.That(victim.HeldReservations, Is.Empty);
            Assert.That(by.CombatTarget, Is.EqualTo(0), "the order outlived its target");
            Assert.That(by.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.DraftHold), "she did not go back to holding");

            int food = victim.Needs[NeedIndex.Food];
            colony.World.Tick(1_000);
            Assert.That(victim.Needs[NeedIndex.Food], Is.EqualTo(food), "a dead colonist's needs still tick");
        }

        /// <summary>
        /// A broken colonist who goes down stays down (design 33 §5j): the fall ends the break, so
        /// the break's rule — fail any job that is not the wander — never meets Job_Downed. One
        /// start, no failures, for the whole of what would have been the break. Measured with the
        /// break left running, which fails Job_Downed every tick.
        /// </summary>
        [Test]
        public void ABrokenColonistDownedHasOneDownedStartAndNoFailures()
        {
            var (colony, victim, by) = Two();
            victim.BreakTicksLeft = 5_000;
            colony.World.Tick(20);
            Assert.That(victim.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Wander), "the control: she is in the break's wander");

            int failed = colony.Jobs.FailedOf(JobIndex.Downed);
            Strike(colony, by, victim, victim.HpMilli + 10_000);
            int started = victim.JobStartTick;
            Assert.That(victim.IsBroken, Is.False, "the fall did not end the break");

            colony.World.Tick(5_000);
            Assert.That(victim.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Downed));
            Assert.That(victim.JobStartTick, Is.EqualTo(started), "Job_Downed was started again");
            Assert.That(colony.Jobs.FailedOf(JobIndex.Downed), Is.EqualTo(failed), "Job_Downed failed");
        }

        [Test]
        public void ADeadColonistOwnsNoBed()
        {
            var (colony, victim, by) = Two();
            int bed = -1;
            for (int cell = 0; cell < Size.CellCount && bed < 0; cell++)
                if (colony.Construction.AssignOwnerAt(cell, victim.Id.Value) == IntentRejection.None) bed = cell;
            Assume.That(bed, Is.GreaterThanOrEqualTo(0), "the board has no bed to own");
            Assert.That(colony.Construction.PawnOwnsABed(victim.Id.Value), Is.True, "the control: she owned it");

            Strike(colony, by, victim, 150_000);
            colony.World.Tick();

            Assert.That(colony.Construction.PawnOwnsABed(victim.Id.Value), Is.False, "the dead keep their beds");
            Assert.That(colony.Construction.BedOwnerAt(bed), Is.EqualTo(0));
        }

        /// <summary>A body falls away from the blow: struck from the west, it lies facing east.</summary>
        [Test]
        public void ACorpseFallsAwayFromTheBlow()
        {
            var (colony, victim, by) = Two();
            Stand(colony, by, Near(colony, 0, 0));
            Stand(colony, victim, Near(colony, 1, 0));
            Strike(colony, by, victim, 150_000);
            colony.World.Tick();
            Assert.That(colony.Pawns.Corpses[0].Facing, Is.EqualTo(2), "+X is heading 2");
        }

        /// <summary>The moments a fall is told by carry the weapon that did it, like the blow itself.</summary>
        [Test]
        public void TheFallAndTheDeathAreToldWithTheWeapon()
        {
            var (colony, victim, by) = Two();
            var tape = new Tape();
            Armament bat = Weapon(colony.Pawns, ItemHandle.Bat);
            colony.Pawns.Combat!.ApplySwing(by, victim, bat, Blow(victim.HpMilli), colony.World.CurrentTick);
            colony.Pawns.Combat!.ApplySwing(by, victim, bat, Blow(60_000), colony.World.CurrentTick);
            colony.World.Tick();
            tape.Read(colony);

            Assert.That(tape.Of(CombatEventKind.Downed).Single().Weapon, Is.EqualTo(ItemHandle.Bat));
            Assert.That(tape.Of(CombatEventKind.Died).Single().Weapon, Is.EqualTo(ItemHandle.Bat));
            Assert.That(tape.Of(CombatEventKind.Hit).All(e => e.Weapon == ItemHandle.Bat), Is.True);
        }
    }
}
