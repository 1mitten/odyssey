#nullable enable
using System.IO;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Cover in the shot (design 50 §2d, §2e): the second roll after a true aim, the defeated shot
    /// fired into the cover and striking it, a stray caught by cover it crosses, the dead zone that
    /// keeps a shooter's own sandbags out of it, and a covered bullet surviving a save.
    /// </summary>
    public class CoverShotTests
    {
        static ColonyWorld Range(out Pawn shooter, out Pawn target)
        {
            var colony = Board(colonists: 2);
            colony.World.Tick();
            shooter = colony.Pawns.Pawns.All[0];
            target = colony.Pawns.Pawns.All[1];
            foreach (Pawn p in colony.Pawns.Pawns.All) Assert.That(Draft(colony, p), Is.EqualTo(IntentRejection.None));
            return colony;
        }

        static int Offset(int cell, int dx, int dz = 0)
        {
            CellRef c = Size.FromIndex(cell);
            return Size.Index(c.X + dx, c.Z + dz, c.Y);
        }

        static void Raise(ColonyWorld colony, int cell, int building, int stuff)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), building, stuff, 0), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
        }

        /// <summary>
        /// Over a thousand shots at a target with a shelf (500) square between it and the shooter,
        /// about half of the true aims are fired into the shelf, and every one of those names the
        /// shelf's cell and is no longer aimed. The control: the same target with the shelf behind
        /// it is never covered.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void HalfTheTrueAimsAtATargetBehindAShelfAreFiredIntoIt(bool shelfBetween)
        {
            var colony = Range(out Pawn shooter, out Pawn target);
            int spot = Near(colony, -15, 12);
            Stand(colony, target, spot);
            Stand(colony, shooter, Offset(spot, 8));
            int shelf = Offset(spot, shelfBetween ? 1 : -1);
            Raise(colony, shelf, BuildingHandle.Shelf, StuffHandle.Wood);

            var rules = new RangedRules();
            Armament pistol = Weapon(colony.Pawns, ItemIndex.Pistol);
            int aimedOrCovered = 0, covered = 0;
            for (int tick = 1; tick <= 1_000; tick++)
            {
                ShotOutcome shot = rules.Resolve(shooter, target, pistol, colony.Pawns, tick);
                if (shot.CoverCell >= 0)
                {
                    covered++;
                    Assert.That(shot.CoverCell, Is.EqualTo(shelf));
                    Assert.That(shot.EndCell, Is.EqualTo(shelf));
                    Assert.That(shot.Aimed, Is.False);
                    Assert.That(shot.CoverPerMille, Is.EqualTo(500));
                }
                if (shot.Aimed || shot.CoverCell >= 0) aimedOrCovered++;
            }

            if (!shelfBetween)
            {
                Assert.That(covered, Is.EqualTo(0));
                return;
            }
            double share = (double)covered / aimedOrCovered;
            Assert.That(share, Is.InRange(0.44, 0.56), $"{covered} of {aimedOrCovered} true aims went into the shelf");
        }

        [Test]
        public void TheToldChanceIsTheAimTimesWhatTheCoverLeaves()
        {
            var outcome = new ShotOutcome(false, 800, 0, 0, 1, coverPerMille: 550, coverCell: 3);
            Assert.That(outcome.TotalPerMille, Is.EqualTo(360));
        }

        /// <summary>A shot fired into a shelf strikes the shelf, reports Covered there, and leaves the target whole.</summary>
        [Test]
        public void ACoveredBulletStrikesTheCoverAndNotTheTarget()
        {
            var colony = Range(out Pawn shooter, out Pawn target);
            int spot = Near(colony, -15, 12);
            Stand(colony, target, spot);
            Stand(colony, shooter, Offset(spot, 8));
            int shelf = Offset(spot, 1);
            Raise(colony, shelf, BuildingHandle.Shelf, StuffHandle.Wood);
            Assert.That(BuildingTargets.TryFind(colony.Pawns, shelf, out BuildingTarget building), Is.True);
            int hpBefore = BuildingTargets.HpMilli(colony.Pawns, building);
            int targetHp = target.HpMilli;

            int now = colony.World.CurrentTick;
            colony.Pawns.Projectiles.Launch(shooter.Id.Value, target.Id.Value, ItemIndex.Pistol, shooter.Cell, shelf,
                now, now + 3, aimed: false, toTheDeath: false, damageMilli: 7_000, coverCell: shelf);

            var tape = new Tape();
            tape.Tick(colony, 6);
            Assert.That(BuildingTargets.HpMilli(colony.Pawns, building), Is.EqualTo(hpBefore - 7_000));
            Assert.That(target.HpMilli, Is.EqualTo(targetHp));
            var coveredEvents = tape.Of(CombatEventKind.Covered);
            Assert.That(coveredEvents.Count, Is.EqualTo(1));
            Assert.That(coveredEvents[0].Cell, Is.EqualTo(Size.FromIndex(shelf)));
            Assert.That(coveredEvents[0].Target, Is.EqualTo(target.Id));
            Assert.That(coveredEvents[0].Amount, Is.EqualTo(7_000));
        }

        // ---- strays ----------------------------------------------------------------------------

        [Test]
        public void AStrayIsCaughtAtHalfTheCoverPastTheDeadZoneAndNeverInIt()
        {
            var colony = Board();
            var rules = new RangedRules();
            CombatDef combat = colony.Pawns.Content.Combat;
            Assert.That(rules.CoverInterceptPerMille(550, combat.interceptFullMm, colony.Pawns), Is.EqualTo(275));
            Assert.That(rules.CoverInterceptPerMille(550, 40_000, colony.Pawns), Is.EqualTo(275));
            Assert.That(rules.CoverInterceptPerMille(550, 2_500, colony.Pawns), Is.EqualTo(0), "her own sandbag, a cell away");
            Assert.That(rules.CoverInterceptPerMille(550, combat.interceptDeadZoneMm, colony.Pawns), Is.EqualTo(0));
            int middle = (combat.interceptDeadZoneMm + combat.interceptFullMm) / 2;
            Assert.That(rules.CoverInterceptPerMille(550, middle, colony.Pawns), Is.InRange(135, 140));
        }

        /// <summary>
        /// Misses fired through a shelf far from the shooter are caught by it about a quarter of the
        /// time (half its 500); the same misses fired past it with nothing on the line never are.
        /// Each on its own tick, so each has its own roll.
        /// </summary>
        [Test]
        public void MissesThroughAShelfAreCaughtAtAboutAQuarter()
        {
            var colony = Range(out Pawn shooter, out Pawn target);
            int spot = Near(colony, -15, 12);
            Stand(colony, shooter, spot);
            Stand(colony, target, Offset(spot, 0, 12));
            int shelf = Offset(spot, 8);
            Raise(colony, shelf, BuildingHandle.Shelf, StuffHandle.Wood);
            Assert.That(BuildingTargets.TryFind(colony.Pawns, shelf, out BuildingTarget building), Is.True);
            int end = Offset(spot, 12);

            var tape = new Tape();
            const int shots = 400;
            for (int i = 0; i < shots; i++)
            {
                // Keep the shelf standing: this counts catches, not how long a shelf lasts.
                colony.Pawns.EdificeDamage.Set(building.Anchor, building.MaxMilli);
                int now = colony.World.CurrentTick;
                colony.Pawns.Projectiles.Launch(shooter.Id.Value, target.Id.Value, ItemIndex.Pistol, shooter.Cell, end,
                    now, now + 1, aimed: false, toTheDeath: false, damageMilli: 1);
                tape.Tick(colony, 1);
            }
            tape.Tick(colony, 2);
            int caught = tape.Of(CombatEventKind.Covered).Count;
            Assert.That(caught, Is.InRange(shots * 18 / 100, shots * 32 / 100), $"{caught} of {shots}");
        }

        // ---- the save --------------------------------------------------------------------------

        [Test]
        public void ACoveredBulletSurvivesASaveAndAnOldFileStillLoads()
        {
            var flying = new Projectiles(Size);
            flying.Launch(1, 2, ItemIndex.Pistol, 10, 20, 5, 9, aimed: false, toTheDeath: false, damageMilli: 3_000, coverCell: 20);
            flying.Launch(1, 2, ItemIndex.Pistol, 10, 30, 5, 9, aimed: true, toTheDeath: false, damageMilli: 3_000);

            var bytes = new MemoryStream();
            using (var binary = new BinaryWriter(bytes, System.Text.Encoding.UTF8, leaveOpen: true))
                flying.Save(new SaveWriter(binary));
            bytes.Position = 0;
            var loaded = new Projectiles(Size);
            loaded.Load(new SaveReader(new BinaryReader(bytes), WorldSave.CurrentFormatVersion));
            Assert.That(loaded.InFlight[0].CoverCell, Is.EqualTo(20));
            Assert.That(loaded.InFlight[1].CoverCell, Is.EqualTo(-1));
            Assert.That(loaded.InFlight[1].DamageMilli, Is.EqualTo(3_000));

            var a = StateHash.New();
            var b = StateHash.New();
            flying.ContributeTo(ref a);
            loaded.ContributeTo(ref b);
            Assert.That(b.Value, Is.EqualTo(a.Value));
        }

        /// <summary>A cover cell moves the hash; a flight with none adds nothing for it (design 50 §11).</summary>
        [Test]
        public void ACoverCellIsHashedOnlyWhenSet()
        {
            var plain = new Projectiles(Size);
            plain.Launch(1, 2, ItemIndex.Pistol, 10, 30, 5, 9, aimed: true, toTheDeath: false, damageMilli: 3_000);
            var covered = new Projectiles(Size);
            covered.Launch(1, 2, ItemIndex.Pistol, 10, 30, 5, 9, aimed: true, toTheDeath: false, damageMilli: 3_000, coverCell: 31);
            var a = StateHash.New();
            var b = StateHash.New();
            plain.ContributeTo(ref a);
            covered.ContributeTo(ref b);
            Assert.That(b.Value, Is.Not.EqualTo(a.Value));
        }
    }
}
