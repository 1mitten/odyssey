#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A bullet from the shot to the impact (design 47 §2c): it flies for the ticks decided when it was
    /// fired, survives a save, hashes only while it flies, and on landing is taken by the first of a
    /// blocker, its own target on a shot aimed true, a bystander at its chance, or the ground.
    /// Every shot here is fired through <see cref="CombatSystem.Fire"/> with the rules pinned
    /// (<see cref="FixedShot"/>), so the geometry is what is tested and not the dice.
    /// </summary>
    public class BulletTests
    {
        /// <summary>The real rules, with whether the shot is aimed, where it ends and the interception chance pinned.</summary>
        internal sealed class FixedShot : RangedRules
        {
            public bool Aimed = true;
            public int End = -1;
            public int Damage = 6_000;

            /// <summary>Past the dead zone, this chance instead of the species'; -1 for the real one.</summary>
            public int Intercept = -1;

            public override ShotOutcome Resolve(Pawn shooter, Pawn target, in Armament armament, PawnContext ctx, int tick)
            {
                ShotOutcome real = base.Resolve(shooter, target, armament, ctx, tick);
                int end = End >= 0 ? End : Aimed ? target.Cell : real.EndCell;
                return new ShotOutcome(Aimed, real.HitPerMille, Damage, end,
                    FlightTicks(RangedGeometry.DistanceMm(ctx.Size, shooter.Cell, end), armament));
            }

            public override int InterceptPerMille(Pawn bystander, int distanceFromShooterMm, PawnContext ctx)
            {
                int real = base.InterceptPerMille(bystander, distanceFromShooterMm, ctx);
                return Intercept < 0 || real == 0 ? real : Intercept;
            }
        }

        /// <summary>A board of three colonists, all drafted so they hold where they are put.</summary>
        static ColonyWorld Range(out Pawn shooter, out Pawn target, out Pawn bystander, FixedShot rules)
        {
            var colony = Board(colonists: 3);
            colony.World.Tick();
            shooter = colony.Pawns.Pawns.All[0];
            target = colony.Pawns.Pawns.All[1];
            bystander = colony.Pawns.Pawns.All[2];
            foreach (Pawn p in colony.Pawns.Pawns.All) Assert.That(Draft(colony, p), Is.EqualTo(IntentRejection.None));
            colony.Pawns.RangedRules = rules;
            return colony;
        }

        static void Arm(ColonyWorld colony, Pawn pawn, int def)
        {
            int cell = colony.Pawns.Items.NearestCellWithSpace(colony.Pawns.Cells, pawn.Cell, def, 1, JobDriver.DropSearchRadius);
            ThingId id = colony.Pawns.Items.Spawn(def, cell);
            WeaponHand.TakeUp(pawn, colony.Pawns.Items.Get(id)!, colony.Pawns);
        }

        static Projectiles.Entry Fire(ColonyWorld colony, Pawn shooter, Pawn target) =>
            colony.Pawns.Combat!.Fire(shooter, target, Weapon(colony.Pawns, ItemIndex.Pistol), colony.World.CurrentTick);

        static void TickTo(ColonyWorld colony, int tick)
        {
            while (colony.World.CurrentTick <= tick) colony.World.Tick();
        }

        // ---- the flight -------------------------------------------------------------------------

        [Test]
        public void ItLandsOnTheTickDecidedWhenItWasFired()
        {
            var colony = Range(out Pawn shooter, out Pawn target, out _, new FixedShot());
            Stand(colony, shooter, Near(colony, 0, 0));
            Stand(colony, target, Near(colony, 8, 0));
            int fired = colony.World.CurrentTick;
            Projectiles.Entry bullet = Fire(colony, shooter, target);
            int distance = RangedGeometry.DistanceMm(Size, shooter.Cell, target.Cell);
            Assert.That(bullet.ImpactTick, Is.EqualTo(fired + (distance + 999) / 1_000));
            Assert.That(colony.Pawns.Projectiles.Count, Is.EqualTo(1));

            int hp = target.HpMilli;
            TickTo(colony, bullet.ImpactTick - 1);
            Assert.That(target.HpMilli, Is.EqualTo(hp), "not before its impact tick");
            Assert.That(colony.Pawns.Projectiles.Count, Is.EqualTo(1));
            colony.World.Tick();
            Assert.That(target.HpMilli, Is.EqualTo(hp - 6_000), "on it");
            Assert.That(colony.Pawns.Projectiles.Count, Is.EqualTo(0));
        }

        [Test]
        public void ItIsPublishedWhileItFlies()
        {
            var colony = Range(out Pawn shooter, out Pawn target, out _, new FixedShot());
            Stand(colony, shooter, Near(colony, 0, 0));
            Stand(colony, target, Near(colony, 8, 0));
            Projectiles.Entry bullet = Fire(colony, shooter, target);
            colony.World.Tick();
            var views = colony.World.Views.Current.Projectiles;
            Assert.That(views.Length, Is.EqualTo(1));
            Assert.That(views[0].Shooter, Is.EqualTo(shooter.Id));
            Assert.That(views[0].Target, Is.EqualTo(target.Id));
            Assert.That(views[0].Start, Is.EqualTo(Size.FromIndex(bullet.StartCell)));
            Assert.That(views[0].End, Is.EqualTo(Size.FromIndex(target.Cell)));
            Assert.That(views[0].ImpactTick, Is.EqualTo(bullet.ImpactTick));
            Assert.That(views[0].Weapon, Is.EqualTo(ItemIndex.Pistol));
        }

        /// <summary>
        /// A colony that has never fired hashes exactly as it did before guns: the registry adds
        /// nothing while empty, which is what let R0 re-bake the goldens once and no unit after.
        /// </summary>
        [Test]
        public void ItHashesOnlyWhileSomethingFlies()
        {
            var empty = new Projectiles(Size);
            var h = StateHash.New();
            empty.ContributeTo(ref h);
            Assert.That(h.Value, Is.EqualTo(StateHash.New().Value));

            empty.Launch(1, 2, ItemIndex.Pistol, 10, 20, 0, 5, true, false, 6_000);
            var g = StateHash.New();
            empty.ContributeTo(ref g);
            Assert.That(g.Value, Is.Not.EqualTo(StateHash.New().Value), "the control: one in the air is seen");
        }

        /// <summary>
        /// A save taken with a bullet in the air lands it on the same tick, on the same body, for the
        /// same damage, and the two worlds hash alike after.
        /// </summary>
        [Test]
        public void ASaveTakenMidFlightLandsItTheSame()
        {
            var colony = Range(out Pawn shooter, out Pawn target, out _, new FixedShot());
            Stand(colony, shooter, Near(colony, 0, 0));
            Stand(colony, target, Near(colony, 9, 0));
            Projectiles.Entry bullet = Fire(colony, shooter, target);
            colony.World.Tick(4);
            Assert.That(colony.Pawns.Projectiles.Count, Is.EqualTo(1), "the control: still in the air");

            byte[] saved = colony.Save();
            var restored = Board(colonists: 3);
            restored.Load(saved);
            restored.Pawns.RangedRules = colony.Pawns.RangedRules;
            Assert.That(restored.Pawns.Projectiles.Count, Is.EqualTo(1));
            Assert.That(restored.Pawns.Projectiles.InFlight[0].ImpactTick, Is.EqualTo(bullet.ImpactTick));
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));

            TickTo(colony, bullet.ImpactTick + 30);
            TickTo(restored, bullet.ImpactTick + 30);
            Pawn back = restored.Pawns.Pawns.Get(target.Id)!;
            Assert.That(back.HpMilli, Is.EqualTo(target.HpMilli));
            Assert.That(back.HpMilli, Is.LessThan(back.HpMaxMilli), "it landed");
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));
        }

        [Test]
        public void ABulletOutlivesItsShooter()
        {
            var colony = Range(out Pawn shooter, out Pawn target, out _, new FixedShot());
            Stand(colony, shooter, Near(colony, 0, 0));
            Stand(colony, target, Near(colony, 9, 0));
            Projectiles.Entry bullet = Fire(colony, shooter, target);
            colony.Pawns.Combat!.Kill(shooter, null, -1, colony.World.CurrentTick);
            colony.World.Tick();
            Assert.That(colony.Pawns.Pawns.Get(shooter.Id), Is.Null, "the control: she is gone");

            int hp = target.HpMilli;
            TickTo(colony, bullet.ImpactTick);
            Assert.That(target.HpMilli, Is.EqualTo(hp - 6_000), "her bullet still lands");
        }

        // ---- the landing ------------------------------------------------------------------------

        /// <summary>
        /// Real flight (the owner's answer 5): a shot aimed true at a target that stepped off the line
        /// while it flew is a miss where the bullet went down — and the target that walked into it is
        /// hit. The two together, so neither passes by accident.
        /// </summary>
        [Test]
        public void ATargetThatSteppedOffTheLineIsMissedAndOneThatSteppedOnIsHit()
        {
            var colony = Range(out Pawn shooter, out Pawn target, out _, new FixedShot());
            Stand(colony, shooter, Near(colony, 0, 0));
            Stand(colony, target, Near(colony, 10, 0));
            var tape = new Tape();
            Projectiles.Entry bullet = Fire(colony, shooter, target);
            int end = bullet.EndCell;
            Stand(colony, target, Near(colony, 10, 4));
            int hp = target.HpMilli;
            TickTo(colony, bullet.ImpactTick);
            tape.Read(colony);
            Assert.That(target.HpMilli, Is.EqualTo(hp), "stepped off, so missed");
            Assert.That(tape.Of(CombatEventKind.Miss).Exists(e => e.Cell == Size.FromIndex(end)), Is.True,
                "reported where it went down, for the dust");

            Stand(colony, target, Near(colony, 10, 0));
            bullet = Fire(colony, shooter, target);
            Stand(colony, target, Near(colony, 5, 0));
            var line = new SightLine();
            LineOfSight.Walk(colony.Pawns, bullet.StartCell, bullet.EndCell, line);
            Assert.That(line.Cells.Contains(target.Cell), Is.True, "the control: she stepped on to the line");
            hp = target.HpMilli;
            TickTo(colony, bullet.ImpactTick);
            Assert.That(target.HpMilli, Is.EqualTo(hp - 6_000), "walked into it, so hit");
        }

        /// <summary>A shot that missed never takes its own target on the way past: that is the near miss.</summary>
        [Test]
        public void AMissPassesItsOwnTarget()
        {
            var colony = Range(out Pawn shooter, out Pawn target, out _, new FixedShot { Aimed = false });
            Stand(colony, shooter, Near(colony, 0, 0));
            Stand(colony, target, Near(colony, 5, 0));
            ((FixedShot)colony.Pawns.RangedRules).End = Near(colony, 8, 0);
            int hp = target.HpMilli;
            Projectiles.Entry bullet = Fire(colony, shooter, target);
            TickTo(colony, bullet.ImpactTick);
            Assert.That(target.HpMilli, Is.EqualTo(hp), "the line runs through her cell and passes her");
        }

        [Test]
        public void ABystanderOnTheLineTakesItAtItsChance()
        {
            var colony = Range(out Pawn shooter, out Pawn target, out Pawn bystander, new FixedShot { Intercept = 1_000 });
            Stand(colony, shooter, Near(colony, 0, 0));
            Stand(colony, bystander, Near(colony, 6, 0));
            Stand(colony, target, Near(colony, 10, 0));
            int hp = target.HpMilli, bhp = bystander.HpMilli;
            var tape = new Tape();
            Projectiles.Entry bullet = Fire(colony, shooter, target);
            TickTo(colony, bullet.ImpactTick);
            tape.Read(colony);
            Assert.That(bystander.HpMilli, Is.EqualTo(bhp - 6_000), "she was in the way");
            Assert.That(target.HpMilli, Is.EqualTo(hp), "and so it never reached its target");
            Assert.That(bystander.Memories.Exists(m => m.ThoughtIndex == ThoughtIndex.AttackedByColonist), Is.True,
                "shot by a colonist, she remembers it");
            Assert.That(bystander.RetaliateAgainst, Is.EqualTo(0), "in the way is not attacked: she does not turn on her");

            // The control: at no chance, the same line reaches its target.
            ((FixedShot)colony.Pawns.RangedRules).Intercept = 0;
            bullet = Fire(colony, shooter, target);
            TickTo(colony, bullet.ImpactTick);
            Assert.That(target.HpMilli, Is.EqualTo(hp - 6_000));
        }

        [Test]
        public void NobodyInsideTheDeadZoneIsHit()
        {
            var colony = Range(out Pawn shooter, out Pawn target, out Pawn bystander, new FixedShot { Intercept = 1_000 });
            Stand(colony, shooter, Near(colony, 0, 0));
            Stand(colony, bystander, Near(colony, 2, 0));
            Stand(colony, target, Near(colony, 10, 0));
            int bhp = bystander.HpMilli, hp = target.HpMilli;
            Projectiles.Entry bullet = Fire(colony, shooter, target);
            TickTo(colony, bullet.ImpactTick);
            Assert.That(bystander.HpMilli, Is.EqualTo(bhp), "5 m from the muzzle: shot over her shoulder");
            Assert.That(target.HpMilli, Is.EqualTo(hp - 6_000));
        }

        /// <summary>
        /// A downed pawn never takes a stray (design 47 §7), so an unordered fight ends in downs and
        /// never deaths; standing, the same pawn on the same cell takes it — the control.
        /// </summary>
        [Test]
        public void ADownedPawnOnTheLineIsPassedOver()
        {
            var colony = Range(out Pawn shooter, out Pawn target, out Pawn bystander, new FixedShot { Intercept = 1_000 });
            Stand(colony, shooter, Near(colony, 0, 0));
            Stand(colony, bystander, Near(colony, 6, 0));
            Stand(colony, target, Near(colony, 10, 0));
            colony.Pawns.Combat!.Down(bystander, null, -1, colony.World.CurrentTick);
            int bhp = bystander.HpMilli, hp = target.HpMilli;
            Projectiles.Entry bullet = Fire(colony, shooter, target);
            TickTo(colony, bullet.ImpactTick);
            Assert.That(bystander.HpMilli, Is.EqualTo(bhp), "down, so passed over");
            Assert.That(target.HpMilli, Is.EqualTo(hp - 6_000));
        }

        [Test]
        public void AWallOnTheLineTakesTheBulletAtItsFullDamage()
        {
            var colony = Range(out Pawn shooter, out Pawn target, out _, new FixedShot());
            Stand(colony, shooter, Near(colony, 0, 0));
            Stand(colony, target, Near(colony, 8, 0));
            Projectiles.Entry bullet = Fire(colony, shooter, target);
            int wall = Near(colony, 4, 0);
            Assert.That(colony.Construction.Place(Size.FromIndex(wall), BuildingHandle.Wall, StuffHandle.Wood, 0), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.Raise(colony.Pawns, wall), Is.True);
            Assert.That(BuildingTargets.TryFind(colony.Pawns, wall, out BuildingTarget building), Is.True);

            int hp = target.HpMilli;
            TickTo(colony, bullet.ImpactTick);
            Assert.That(target.HpMilli, Is.EqualTo(hp), "raised mid-flight, the wall took it");
            Assert.That(BuildingTargets.HpMilli(colony.Pawns, building), Is.EqualTo(building.MaxMilli - 6_000),
                "at ×1, whatever wood takes from an edge or a head");
        }

        [Test]
        public void AMissIntoRockGoesDownAtTheRock()
        {
            var colony = Range(out Pawn shooter, out Pawn target, out _, new FixedShot { Aimed = false });
            Stand(colony, shooter, Near(colony, 0, 0));
            Stand(colony, target, Near(colony, 5, 0));
            // The end a layer down, inside the ground under the target.
            int below = target.Cell - Size.LayerStride;
            Assert.That(colony.Pawns.Cells.IsSolidTerrain(below), Is.True, "the control: rock under her feet");
            ((FixedShot)colony.Pawns.RangedRules).End = below;
            var tape = new Tape();
            Projectiles.Entry bullet = Fire(colony, shooter, target);
            TickTo(colony, bullet.ImpactTick);
            tape.Read(colony);
            var misses = tape.Of(CombatEventKind.Miss);
            Assert.That(misses.Count, Is.EqualTo(1));
            Assert.That(misses[0].Cell, Is.Not.EqualTo(Size.FromIndex(below)), "it stopped where the ground did, short of the end");
        }

        [Test]
        public void AShotTrainsShootingHitOrMissAndNotMelee()
        {
            var colony = Range(out Pawn shooter, out Pawn target, out _, new FixedShot { Aimed = false });
            Stand(colony, shooter, Near(colony, 0, 0));
            Stand(colony, target, Near(colony, 5, 0));
            int shooting = shooter.Skills[SkillIndex.Shooting], melee = shooter.Skills[SkillIndex.Melee];
            Projectiles.Entry bullet = Fire(colony, shooter, target);
            TickTo(colony, bullet.ImpactTick);
            Assert.That(shooter.Skills[SkillIndex.Shooting], Is.GreaterThan(shooting), "a miss trains it");
            Assert.That(shooter.Skills[SkillIndex.Melee], Is.EqualTo(melee));
        }

        /// <summary>The reference's rule: a pawn with her eye down the barrel does not step out of a blow.</summary>
        [Test]
        public void ADefenderMidAimDoesNotDodge()
        {
            var colony = Range(out Pawn shooter, out Pawn target, out _, new FixedShot());
            SetMelee(shooter, 20);
            var rules = new MeleeRules();
            Assert.That(rules.DodgeChancePerMille(shooter, colony.Pawns), Is.GreaterThan(0), "the control: not aiming, the curve");

            Arm(colony, shooter, ItemIndex.Pistol);
            Stand(colony, shooter, Near(colony, 0, 0));
            Stand(colony, target, Near(colony, 5, 0));
            // A shooter mid-aim: her driver in its Aim toil.
            Spawn(colony, PawnKindIndex.Bandit, Near(colony, 6, 0));
            for (int t = 0; t < 300 && !Ranged.IsAiming(shooter); t++) colony.World.Tick();
            Assert.That(Ranged.IsAiming(shooter), Is.True, "the control: she took aim");
            Assert.That(rules.DodgeChancePerMille(shooter, colony.Pawns), Is.EqualTo(0));
        }
    }
}
