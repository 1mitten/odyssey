#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// <c>Job_AttackRanged</c> and fire at will (design 47 §2d, §2e): who shoots, at whom, from where,
    /// and what breaks an aim. Every claim with its control — the machete that does not shoot, the
    /// hog that is not a threat, the wall that ends a line — so none of these passes by accident.
    /// </summary>
    public class RangedDriverTests
    {
        static ColonyWorld Range(int colonists = 2)
        {
            var colony = Board(colonists: colonists);
            colony.World.Tick();
            return colony;
        }

        static void Arm(ColonyWorld colony, Pawn pawn, int def)
        {
            int cell = colony.Pawns.Items.NearestCellWithSpace(colony.Pawns.Cells, pawn.Cell, def, 1, JobDriver.DropSearchRadius);
            ThingId id = colony.Pawns.Items.Spawn(def, cell);
            WeaponHand.TakeUp(pawn, colony.Pawns.Items.Get(id)!, colony.Pawns);
        }

        /// <summary>A bandit at a cell, holding a pistol or its own weapon, drafted colonists not needed.</summary>
        static Pawn Bandit(ColonyWorld colony, int cell, int weapon = -1) =>
            colony.Pawns.Pawns.Spawn(cell, PawnKindIndex.Bandit, weapon);

        static void RaiseWall(ColonyWorld colony, int cell)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Wall, StuffHandle.Stone, 0), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
        }

        // ---- fire at will -------------------------------------------------------------------------

        /// <summary>
        /// The owner's answer 1: a drafted colonist with a gun shoots the nearest hostile in range and
        /// sight with no order. The same colonist with a machete swings and never shoots — the control
        /// that the gun is the reason.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void ADraftedGunHolderShootsAHostileInSightWithoutAnOrder(bool pistol)
        {
            var colony = Range();
            Pawn shooter = colony.Pawns.Pawns.All[0];
            Stand(colony, shooter, Near(colony, 0, 0));
            Arm(colony, shooter, pistol ? ItemIndex.Pistol : ItemIndex.Machete);
            Assert.That(Draft(colony, shooter), Is.EqualTo(IntentRejection.None));
            Bandit(colony, Near(colony, 8, 0));

            var tape = new Tape();
            tape.Tick(colony, 120);
            Assert.That(tape.By(shooter, CombatEventKind.Shot).Count > 0, Is.EqualTo(pistol),
                pistol ? "she should have fired" : "a machete does not shoot");
        }

        /// <summary>
        /// Fire at will is for threats: a wild hog wandering in sight is not shot at. The control, a
        /// bandit in the same place, is (the test above).
        /// </summary>
        [Test]
        public void AnAnimalThatIsNoThreatIsNotShot()
        {
            var colony = Range();
            Pawn shooter = colony.Pawns.Pawns.All[0];
            Stand(colony, shooter, Near(colony, 0, 0));
            Arm(colony, shooter, ItemIndex.Pistol);
            Assert.That(Draft(colony, shooter), Is.EqualTo(IntentRejection.None));
            Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 6, 0));

            var tape = new Tape();
            tape.Tick(colony, 300);
            Assert.That(tape.By(shooter, CombatEventKind.Shot), Is.Empty);
        }

        /// <summary>
        /// The reach rule (design 47 §12; owner, 2026-09-25, reversing §8's point-blank): an enemy
        /// within reach is clubbed with the gun, never shot — the swing carries the pistol as its
        /// weapon. Moved three cells off, the same bandit is shot: the control that the gun still shoots.
        /// </summary>
        [Test]
        public void AnAdjacentEnemyIsClubbedAndOneThatStepsAwayIsShot()
        {
            var colony = Range();
            Pawn shooter = colony.Pawns.Pawns.All[0];
            Stand(colony, shooter, Near(colony, 0, 0));
            Arm(colony, shooter, ItemIndex.Pistol);
            Assert.That(Draft(colony, shooter), Is.EqualTo(IntentRejection.None));
            Pawn bandit = Bandit(colony, Near(colony, 1, 0));

            var tape = new Tape();
            tape.Tick(colony, 150);
            Assert.That(tape.By(shooter, CombatEventKind.Shot), Is.Empty, "she shot an enemy she could strike");
            var swings = tape.Swings(shooter);
            Assert.That(swings, Is.Not.Empty, "she did not club it");
            Assert.That(swings.TrueForAll(e => e.Weapon == ItemIndex.Pistol), Is.True, "with the pistol");

            // Stepped away: shot again.
            Stand(colony, bandit, Near(colony, 5, 0));
            bandit.StunnedUntilTick = colony.World.CurrentTick + 400;
            var after = new Tape();
            after.Read(colony);
            after.Tick(colony, 150);
            Assert.That(after.By(shooter, CombatEventKind.Shot), Is.Not.Empty, "out of reach, she did not shoot it");
        }

        /// <summary>
        /// The pistol's blow is its own (design 47 §12): blunt, five points before quality and spread,
        /// on the fists' cadence — not the bullet's ten. What the melee paths are handed for a gun.
        /// </summary>
        [Test]
        public void AGunsBlowIsItsOwnAndNotItsBullet()
        {
            var colony = Range();
            Armament gun = Weapon(colony.Pawns, ItemIndex.Pistol);
            Armament blow = gun.Melee;
            Assert.That(blow.Attack.IsRanged, Is.False);
            Assert.That(blow.Attack.damage, Is.EqualTo(5));
            Assert.That(blow.Attack.damageKind, Is.EqualTo(DamageKind.Blunt));
            Assert.That(blow.ItemDef, Is.EqualTo(ItemIndex.Pistol), "still the pistol, for the drawing and the log");
            Armament machete = Weapon(colony.Pawns, ItemIndex.Machete);
            Assert.That(machete.Melee.Attack, Is.SameAs(machete.Attack), "the control: a blade swings as itself");
        }

        /// <summary>
        /// An order survives the swap: ordered on a bandit that walks up to her, she clubs it under the
        /// same order, and when it steps away she shoots it under the same order again.
        /// </summary>
        [Test]
        public void AnOrderCarriesAcrossTheSwap()
        {
            var colony = Range();
            Pawn shooter = colony.Pawns.Pawns.All[0];
            Stand(colony, shooter, Near(colony, 0, 0));
            Arm(colony, shooter, ItemIndex.Pistol);
            Assert.That(Draft(colony, shooter), Is.EqualTo(IntentRejection.None));
            Pawn bandit = Bandit(colony, Near(colony, 6, 0));
            Assert.That(Attack(colony, shooter, bandit), Is.EqualTo(IntentRejection.None));
            Assert.That(shooter.CurrentJob!.DefIndex, Is.EqualTo(JobIndex.AttackRanged));

            Stand(colony, bandit, Near(colony, 1, 0));
            bandit.StunnedUntilTick = colony.World.CurrentTick + 60;
            colony.World.Tick();
            Assert.That(shooter.CurrentJob!.DefIndex, Is.EqualTo(JobIndex.AttackMelee), "within reach: a swing");
            Assert.That(shooter.CurrentJob.PlayerForced, Is.True, "still the player's order");
            Assert.That(shooter.CombatTarget, Is.EqualTo(bandit.Id.Value));

            colony.World.Tick(40);
            Stand(colony, bandit, Near(colony, 6, 0));
            bandit.StunnedUntilTick = colony.World.CurrentTick + 400;
            for (int t = 0; t < 60 && shooter.CurrentJob?.DefIndex != JobIndex.AttackRanged; t++) colony.World.Tick();
            Assert.That(shooter.CurrentJob!.DefIndex, Is.EqualTo(JobIndex.AttackRanged), "stepped away: a shot again");
            Assert.That(shooter.CurrentJob.PlayerForced, Is.True, "and still the player's order");
        }

        /// <summary>
        /// An aim in hand when an enemy steps within reach is lost with no shot fired, and its clock is
        /// given back, so the first blow is not delayed by a shot that never happened.
        /// </summary>
        [Test]
        public void AnAimIsLostWhenAnEnemyStepsWithinReach()
        {
            var colony = Range();
            Pawn shooter = colony.Pawns.Pawns.All[0];
            Stand(colony, shooter, Near(colony, 0, 0));
            Arm(colony, shooter, ItemIndex.Pistol);
            Assert.That(Draft(colony, shooter), Is.EqualTo(IntentRejection.None));
            Pawn bandit = Bandit(colony, Near(colony, 6, 0));
            Assert.That(Attack(colony, shooter, bandit), Is.EqualTo(IntentRejection.None));
            for (int t = 0; t < 60 && !Ranged.IsAiming(shooter); t++) colony.World.Tick();
            Assert.That(Ranged.IsAiming(shooter), Is.True, "the control: mid-aim");

            Stand(colony, bandit, Near(colony, 1, 0));
            bandit.StunnedUntilTick = colony.World.CurrentTick + 200;
            var tape = new Tape();
            tape.Tick(colony, 1);
            Assert.That(tape.By(shooter, CombatEventKind.Shot), Is.Empty, "the aim fired at an enemy she could strike");
            Assert.That(shooter.CurrentJob!.DefIndex, Is.EqualTo(JobIndex.AttackMelee));
            tape.Tick(colony, 5);
            Assert.That(tape.Swings(shooter), Is.Not.Empty, "the clock was not given back: no blow at once");
        }

        /// <summary>A pistol bandit caught by a colonist clubs her: the rule is everybody's.</summary>
        [Test]
        public void APistolBanditWithinReachClubs()
        {
            var colony = Range(colonists: 1);
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Stand(colony, colonist, Near(colony, 0, 0));
            Assert.That(Draft(colony, colonist), Is.EqualTo(IntentRejection.None));
            Pawn bandit = Bandit(colony, Near(colony, 1, 0), ItemIndex.Pistol);

            var tape = new Tape();
            tape.Tick(colony, 200);
            Assert.That(tape.By(bandit, CombatEventKind.Shot), Is.Empty);
            Assert.That(tape.Swings(bandit), Is.Not.Empty);
        }

        /// <summary>The hold never walks (design 47 §2d): on her hold she shoots from where she stands.</summary>
        [Test]
        public void TheHoldShootsFromWhereSheStands()
        {
            var colony = Range();
            Pawn shooter = colony.Pawns.Pawns.All[0];
            int at = Near(colony, 0, 0);
            Stand(colony, shooter, at);
            Arm(colony, shooter, ItemIndex.Pistol);
            Assert.That(Draft(colony, shooter), Is.EqualTo(IntentRejection.None));
            Bandit(colony, Near(colony, 9, 0));

            var tape = new Tape();
            for (int t = 0; t < 90; t++)
            {
                tape.Tick(colony, 1);
                if (tape.By(shooter, CombatEventKind.Shot).Count > 0) break;
            }
            Assert.That(tape.By(shooter, CombatEventKind.Shot), Is.Not.Empty, "the control: she fired");
            Assert.That(shooter.Cell, Is.EqualTo(at), "without a step");
        }

        // ---- orders -------------------------------------------------------------------------------

        /// <summary>An ordered target beats a nearer unordered one: an order is a forced job and the hold thinks only between jobs.</summary>
        [Test]
        public void AnOrderedTargetBeatsANearerOne()
        {
            var colony = Range();
            Pawn shooter = colony.Pawns.Pawns.All[0];
            Stand(colony, shooter, Near(colony, 0, 0));
            Arm(colony, shooter, ItemIndex.Pistol);
            Assert.That(Draft(colony, shooter), Is.EqualTo(IntentRejection.None));
            Bandit(colony, Near(colony, 3, 0));
            Pawn far = Bandit(colony, Near(colony, 0, 8));

            Assert.That(Attack(colony, shooter, far), Is.EqualTo(IntentRejection.None));
            Assert.That(shooter.CurrentJob!.DefIndex, Is.EqualTo(JobIndex.AttackRanged), "a gun makes the order a shot");
            var tape = new Tape();
            tape.Tick(colony, 150);
            var shots = tape.By(shooter, CombatEventKind.Shot);
            Assert.That(shots, Is.Not.Empty);
            Assert.That(shots.TrueForAll(e => e.Target == far.Id), Is.True, "every shot at the one she was told to shoot");
        }

        /// <summary>
        /// One order, two jobs, by what she holds (<see cref="CombatJobs.AttackJobFor"/>): a shot with a
        /// gun and a swing with a blade, at the same bandit from the same cell.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void AnOrderIsAShotWithAGunAndASwingWithABlade(bool pistol)
        {
            var colony = Range();
            Pawn shooter = colony.Pawns.Pawns.All[0];
            Stand(colony, shooter, Near(colony, 0, 0));
            Arm(colony, shooter, pistol ? ItemIndex.Pistol : ItemIndex.Machete);
            Assert.That(Draft(colony, shooter), Is.EqualTo(IntentRejection.None));
            Pawn bandit = Bandit(colony, Near(colony, 6, 0));
            Assert.That(Attack(colony, shooter, bandit), Is.EqualTo(IntentRejection.None));
            Assert.That(shooter.CurrentJob!.DefIndex, Is.EqualTo(pistol ? JobIndex.AttackRanged : JobIndex.AttackMelee));
            Assert.That(Melee.IsAttacking(shooter, bandit), Is.True, "either way she is attacking it");
            Assert.That(Melee.IsInAnAttack(shooter), Is.True, "and holds her cell as a fighter does");
        }

        /// <summary>
        /// A gun-holder ordered at a building is refused until the ranged driver can shoot one
        /// (design 47 §2d, R6); the same colonist with a machete is not — the control.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void AGunHoldersOrderAtABuildingIsRefused(bool pistol)
        {
            var colony = Range();
            Pawn shooter = colony.Pawns.Pawns.All[0];
            Stand(colony, shooter, Near(colony, 0, 0));
            Arm(colony, shooter, pistol ? ItemIndex.Pistol : ItemIndex.Machete);
            Assert.That(Draft(colony, shooter), Is.EqualTo(IntentRejection.None));
            int wall = Near(colony, 3, 0);
            RaiseWall(colony, wall);
            colony.World.Tick();
            var rejection = Send(colony, new Intent(IntentKind.OrderAttack, Size.FromIndex(wall), shooter.Id.Value, 0));
            Assert.That(rejection, Is.EqualTo(pistol ? IntentRejection.NotPermitted : IntentRejection.None));
        }

        // ---- the aim ------------------------------------------------------------------------------

        /// <summary>
        /// The aim breaks when the target ducks out of sight, and <b>no cooldown is spent</b>: the shot
        /// never happened (design 47 §7). With the line open again she aims again at once.
        /// </summary>
        [Test]
        public void TheAimBreaksBehindAWallWithNoCooldownSpent()
        {
            var colony = Range();
            Pawn shooter = colony.Pawns.Pawns.All[0];
            Stand(colony, shooter, Near(colony, 0, 0));
            Arm(colony, shooter, ItemIndex.Pistol);
            Assert.That(Draft(colony, shooter), Is.EqualTo(IntentRejection.None));
            Pawn bandit = Bandit(colony, Near(colony, 6, 0));
            Assert.That(Attack(colony, shooter, bandit), Is.EqualTo(IntentRejection.None));
            for (int t = 0; t < 60 && !Ranged.IsAiming(shooter); t++) colony.World.Tick();
            Assert.That(Ranged.IsAiming(shooter), Is.True, "the control: she took aim");
            Assert.That(shooter.NextSwingTick, Is.GreaterThan(colony.World.CurrentTick), "the clock ran forward at the aim");

            // A wall between them, mid-aim.
            RaiseWall(colony, Near(colony, 3, 0));
            colony.World.Tick();
            Assert.That(Ranged.IsAiming(shooter), Is.False, "the aim broke");
            Assert.That(shooter.NextSwingTick, Is.EqualTo(0), "and the clock was given back");
        }

        /// <summary>A stun is a pause for the job, so an aim waiting it out would fire the shot the stun exists to deny.</summary>
        [Test]
        public void AStunnedShootersAimIsLost()
        {
            var colony = Range();
            Pawn shooter = colony.Pawns.Pawns.All[0];
            Stand(colony, shooter, Near(colony, 0, 0));
            Arm(colony, shooter, ItemIndex.Pistol);
            Assert.That(Draft(colony, shooter), Is.EqualTo(IntentRejection.None));
            Pawn bandit = Bandit(colony, Near(colony, 6, 0));
            Assert.That(Attack(colony, shooter, bandit), Is.EqualTo(IntentRejection.None));
            for (int t = 0; t < 60 && !Ranged.IsAiming(shooter); t++) colony.World.Tick();
            Assert.That(Ranged.IsAiming(shooter), Is.True, "the control: she took aim");

            shooter.StunnedUntilTick = colony.World.CurrentTick + 200;
            var tape = new Tape();
            tape.Tick(colony, 100);
            Assert.That(tape.By(shooter, CombatEventKind.Shot), Is.Empty, "no shot while stunned");
            Assert.That(Ranged.IsAiming(shooter), Is.False, "the aim was let go");
        }

        /// <summary>
        /// The aim is paced by her condition and not her skill (design 47 §2d): at the condition floor
        /// it takes longer; at twenty in Shooting it takes exactly as long as at nought.
        /// </summary>
        [Test]
        public void TheAimIsPacedByConditionNotSkill()
        {
            int Aim(System.Action<Pawn> prepare)
            {
                var colony = Range();
                Pawn shooter = colony.Pawns.Pawns.All[0];
                Stand(colony, shooter, Near(colony, 0, 0));
                Arm(colony, shooter, ItemIndex.Pistol);
                Assert.That(Draft(colony, shooter), Is.EqualTo(IntentRejection.None));
                prepare(shooter);
                Pawn bandit = Bandit(colony, Near(colony, 6, 0));
                Assert.That(Attack(colony, shooter, bandit), Is.EqualTo(IntentRejection.None));
                int started = -1;
                var tape = new Tape();
                for (int t = 0; t < 200; t++)
                {
                    tape.Tick(colony, 1);
                    if (started < 0 && Ranged.IsAiming(shooter)) started = colony.World.CurrentTick;
                    var shots = tape.By(shooter, CombatEventKind.Shot);
                    if (started >= 0 && shots.Count > 0) return shots[0].Tick - started;
                }
                Assert.Fail("no shot");
                return -1;
            }

            int fed = Aim(_ => { });
            int skilled = Aim(p => RangedMathTests.SetShooting(p, 20));
            int starving = Aim(p => p.StarvationSeverity = 1_000_000);
            Assert.That(skilled, Is.EqualTo(fed), "skill buys accuracy, not speed");
            Assert.That(starving, Is.GreaterThan(fed), "a starving colonist aims slower");
        }

        /// <summary>A save taken mid-aim resumes on the same ticks: the toil, its progress and the clock are all saved.</summary>
        [Test]
        public void ASaveTakenMidAimResumesTheSame()
        {
            var colony = Range();
            Pawn shooter = colony.Pawns.Pawns.All[0];
            Stand(colony, shooter, Near(colony, 0, 0));
            Arm(colony, shooter, ItemIndex.Pistol);
            Assert.That(Draft(colony, shooter), Is.EqualTo(IntentRejection.None));
            Pawn bandit = Bandit(colony, Near(colony, 7, 0));
            Assert.That(Attack(colony, shooter, bandit), Is.EqualTo(IntentRejection.None));
            for (int t = 0; t < 60 && !Ranged.IsAiming(shooter); t++) colony.World.Tick();
            colony.World.Tick(5);
            Assert.That(Ranged.IsAiming(shooter), Is.True, "the control: mid-aim");

            byte[] saved = colony.Save();
            var restored = Board();
            restored.Load(saved);
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));
            Assert.That(Ranged.IsAiming(restored.Pawns.Pawns.Get(shooter.Id)!), Is.True, "still aiming after the load");

            for (int t = 0; t < 400; t++)
            {
                colony.World.Tick();
                restored.World.Tick();
                Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value),
                    $"diverged {t + 1} ticks after the load");
            }
        }

        // ---- being shot -----------------------------------------------------------------------------

        /// <summary>A pistol bandit hunts a colonist and shoots her: the debug gunman is a gunman.</summary>
        [Test]
        public void APistolBanditShootsAColonist()
        {
            var colony = Range(colonists: 1);
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Stand(colony, colonist, Near(colony, 0, 0));
            Pawn bandit = Bandit(colony, Near(colony, 9, 0), ItemIndex.Pistol);
            Assert.That(bandit.IsHostile && bandit.IsPerson, Is.True);

            var tape = new Tape();
            tape.Tick(colony, 400);
            Assert.That(tape.By(bandit, CombatEventKind.Shot), Is.Not.Empty);
        }

        /// <summary>
        /// A bandit busy with one colonist and shot from range by another turns on the one who shot
        /// it (the melee rule, <c>CombatSystem.React</c>): a gun draws the fight to the gunner.
        /// </summary>
        [Test]
        public void ABanditShotFromRangeTurnsOnTheShooter()
        {
            var colony = Range(colonists: 2);
            Pawn shooter = colony.Pawns.Pawns.All[0], other = colony.Pawns.Pawns.All[1];
            Stand(colony, shooter, Near(colony, 0, 0));
            Stand(colony, other, Near(colony, 9, 2));
            Pawn bandit = Bandit(colony, Near(colony, 8, 0));
            colony.World.Tick(5);
            Assert.That(bandit.CombatTarget, Is.EqualTo(other.Id.Value), "the control: it went for the nearer colonist");
            colony.Pawns.RangedRules = new BulletTests.FixedShot();
            Projectiles.Entry bullet = colony.Pawns.Combat!.Fire(shooter, bandit,
                Weapon(colony.Pawns, ItemIndex.Pistol), colony.World.CurrentTick);
            while (colony.World.CurrentTick <= bullet.ImpactTick) colony.World.Tick();
            Assert.That(bandit.RetaliateAgainst, Is.EqualTo(shooter.Id.Value));
        }

        [Test]
        public void AShooterRuns()
        {
            var colony = Range();
            Pawn shooter = colony.Pawns.Pawns.All[0];
            int walking = shooter.UrgencyPerMille();
            Arm(colony, shooter, ItemIndex.Pistol);
            Pawn bandit = Bandit(colony, Near(colony, 6, 0));
            Stand(colony, shooter, Near(colony, 0, 0));
            Assert.That(Draft(colony, shooter), Is.EqualTo(IntentRejection.None));
            Assert.That(Attack(colony, shooter, bandit), Is.EqualTo(IntentRejection.None));
            Assert.That(shooter.UrgencyPerMille(), Is.GreaterThan(walking));
        }

        /// <summary>The weapon is out for a ranged attack at any distance, where a melee one waits for two tiles.</summary>
        [Test]
        public void AGunIsDrawnForATargetFarOff()
        {
            var colony = Range();
            Pawn shooter = colony.Pawns.Pawns.All[0];
            Stand(colony, shooter, Near(colony, 0, 0));
            Arm(colony, shooter, ItemIndex.Pistol);
            Assert.That(Draft(colony, shooter), Is.EqualTo(IntentRejection.None));
            Pawn bandit = Bandit(colony, Near(colony, 9, 0));
            Assert.That(Attack(colony, shooter, bandit), Is.EqualTo(IntentRejection.None));
            Assert.That(WeaponDraw.TargetNear(colony.Pawns, shooter), Is.True);
        }
    }
}
