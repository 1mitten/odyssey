#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// Bullets, drawn (design 47 §4c, §5): a bucket of streaks is one call whatever the fight; a
    /// streak is cut at the edge of the drawn band and not drawn at all when neither end is; a hit
    /// ends on the drawn body while the figure is on the end cell and on the cell's centre
    /// otherwise; a flight too short for any frame still leaves an afterimage for 0.08 s; and a
    /// paused bullet hangs. None of it needs the licensed art: the geometry is tested as pure
    /// functions, and the director runs on a hand-built board with the renderer's submission off.
    /// </summary>
    public class ProjectileDirectorTests
    {
        /// <summary>
        /// A miss's streak passes beside its target and goes into the ground behind it (design 47 §4c,
        /// amended on the owner's first play): at the target the line stands off to one side by a good
        /// part of <see cref="ProjectileDirector.MissAside"/>, the two sides are mirror images, and the
        /// end is low — never through the body, never off into the air.
        /// </summary>
        [Test]
        public void AMissPassesBesideItsTargetIntoTheGround()
        {
            var shooterCell = new CellRef(2, 5, 3);
            var targetCell = new CellRef(8, 5, 3);
            var endCell = new CellRef(10, 5, 3);
            Vector3 from = ProjectileDirector.ChestOf(shooterCell);
            Vector3 body = ProjectileDirector.ChestOf(targetCell);
            foreach (float side in new[] { 1f, -1f })
            {
                Vector3 to = ProjectileDirector.MissPoint(endCell, from, side);
                Assert.That(to.y - CellMetrics.FloorCentre(endCell).y, Is.LessThan(0.6f), "it ends in the air");
                // Where the line passes the target's depth along the shot.
                float t = (body.x - from.x) / (to.x - from.x);
                Vector3 passing = Vector3.Lerp(from, to, t);
                float aside = passing.z - body.z;
                Assert.That(Mathf.Abs(aside), Is.GreaterThan(0.3f), $"side {side}: passed through the body");
                Assert.That(Mathf.Sign(aside), Is.EqualTo(Mathf.Sign(to.z - body.z)), "the two sides are the two sides");
            }
        }

        const int Layers = 6;

        [SetUp]
        public void SetUp() => GroundRelief.Reset();

        [TearDown]
        public void TearDown() => GroundRelief.Reset();

        static WorldSnapshot Frame(int tick, params ProjectileView[] bullets)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(tick, new GridSize(40, 40, Layers), 1);
            foreach (ProjectileView bullet in bullets) snapshot.AddProjectile(bullet);
            return snapshot;
        }

        /// <summary>A frame with these people standing on it, for a shot whose shooter must be found.</summary>
        static WorldSnapshot FrameWith(int tick, params PawnView[] pawns)
        {
            WorldSnapshot snapshot = Frame(tick);
            foreach (PawnView pawn in pawns) snapshot.AddPawn(pawn);
            return snapshot;
        }

        static PawnView Standing(int id, CellRef cell) =>
            new PawnView(new PawnId(id), cell, 800, 800, 600, nextCell: cell, flags: PawnFlags.Person);

        static ProjectileView Bullet(int shooter, CellRef start, CellRef end, int fire, int impact, int target = 0) =>
            new ProjectileView(new PawnId(shooter), new PawnId(target), start, end, fire, impact, weapon: 7);

        sealed class Rig : System.IDisposable
        {
            public readonly RenderTestWorld World = new RenderTestWorld(40, 40, Layers);
            public readonly ChunkRenderer Renderer;
            public readonly ProjectileDirector Director;

            public Rig()
            {
                World.Publish();
                Renderer = new ChunkRenderer(World.Model) { SubmitToGpu = false };
                Director = new ProjectileDirector(World.Model);
            }

            public void Draw(WorldSnapshot snapshot, float alpha = 0.5f, float seconds = 0.016f,
                int lowest = 0, int highest = Layers - 1) =>
                Director.Draw(Renderer, snapshot, alpha, seconds, lowest, highest);

            public void Dispose()
            {
                Director.Dispose();
                Renderer.Dispose();
            }
        }

        // ---------------------------------------------------------------- one call

        [Test]
        public void ABucketOfBulletsIsOneDrawCall()
        {
            using var rig = new Rig();

            rig.Draw(Frame(100));
            Assert.That(rig.Director.LastDrawCalls, Is.Zero, "a sky with no bullet in it submitted something");

            var bullets = new ProjectileView[40];
            for (int i = 0; i < bullets.Length; i++)
                bullets[i] = Bullet(i + 1, new CellRef(1, i % 30 + 1, 1), new CellRef(20, i % 30 + 1, 1), 95, 110);
            rig.Draw(Frame(100, bullets));

            Assert.That(rig.Director.LastStreaksDrawn, Is.EqualTo(40), "every bullet in the air was drawn");
            Assert.That(rig.Director.LastDrawCalls, Is.EqualTo(1),
                "forty bullets cost forty draws: the streaks are not in one bucket (P10)");
        }

        [Test]
        public void TheFlashesAreASecondCallAndNoMore()
        {
            using var rig = new Rig();
            var shooters = new PawnView[12];
            for (int i = 0; i < shooters.Length; i++) shooters[i] = Standing(i + 1, new CellRef(1, i + 1, 1));
            WorldSnapshot frame = FrameWith(100, shooters);
            for (int i = 0; i < 12; i++)
                rig.Director.OnCombatEvent(Shot(i + 1, 100, new CellRef(20, i + 1, 1), flight: 10), frame, figures: null);

            rig.Draw(frame, alpha: 0.5f);
            Assert.That(rig.Director.LastStreaksDrawn, Is.EqualTo(12), "the streaks went into more than their one bucket");
            Assert.That(rig.Director.LastFlashesDrawn, Is.EqualTo(12));
            Assert.That(rig.Director.LastDrawCalls, Is.EqualTo(2),
                "twelve shots are one call of streaks and one of flashes, whatever the count");
        }

        // ---------------------------------------------------------------- the slice

        [Test]
        public void ABulletFromAHiddenStoreyEntersAtTheBandsCeiling()
        {
            // The pure rule first: an end above the band crosses the band's ceiling plane.
            Vector3 from = new Vector3(0f, 3 * CellMetrics.SizeY + 1.3f, 0f);
            Vector3 to = new Vector3(20f, 1 * CellMetrics.SizeY + 1.3f, 0f);
            float ceiling = ProjectileDirector.PlaneFor(3, lowest: 0, highest: 2);
            Assert.That(ceiling, Is.EqualTo(3 * CellMetrics.SizeY));
            Assert.That(ProjectileDirector.VisibleSpan(from, to, false, ceiling, true, 0f, out float tMin, out float tMax), Is.True);
            Assert.That(tMax, Is.EqualTo(1f));
            Assert.That(ProjectileDirector.StreakAt(from, to, 0.3f, tMin, tMax, out Vector3 tail, out _), Is.True);
            Assert.That(tail.y, Is.EqualTo(ceiling).Within(1e-3f), "the streak's tail is not cut at the ceiling");

            // And through the director: the slice shows layers 0 to 2, the shooter stands on 3, and
            // the bullet is three tenths of the way down.
            using var rig = new Rig();
            rig.Draw(Frame(98, Bullet(1, new CellRef(2, 5, 3), new CellRef(10, 5, 1), 95, 105)), alpha: 0f, highest: 2);
            Assert.That(rig.Director.LastStreaksDrawn, Is.EqualTo(1), "a bullet into a drawn layer was not drawn");
            Vector3 drawnTail = ProjectileDirector.TailOf(rig.Director.StreakDrawn(0));
            Assert.That(drawnTail.y, Is.EqualTo(3 * CellMetrics.SizeY).Within(1e-3f),
                "a bullet from a hidden storey must enter at the band's ceiling, not hang in the air above it");

            // Control: the same shot with the storey drawn is not cut.
            rig.Draw(Frame(98, Bullet(1, new CellRef(2, 5, 3), new CellRef(10, 5, 1), 95, 105)), alpha: 0f, highest: 3);
            Assert.That(ProjectileDirector.TailOf(rig.Director.StreakDrawn(0)).y,
                Is.GreaterThan(3 * CellMetrics.SizeY + 0.1f), "the control was cut too, so the clip is not what moved it");
        }

        [Test]
        public void ABulletIntoAnUndrawnCellarLeavesAtTheBandsFloor()
        {
            float floor = ProjectileDirector.PlaneFor(0, lowest: 1, highest: 4);
            Assert.That(floor, Is.EqualTo(1 * CellMetrics.SizeY));

            using var rig = new Rig();
            // Eight tenths of the way down: the head is past the floor plane, the tail above it.
            ProjectileView down = Bullet(1, new CellRef(2, 5, 2), new CellRef(10, 5, 0), 95, 105);
            rig.Draw(Frame(102, down), alpha: 1f, lowest: 1);
            Assert.That(rig.Director.LastStreaksDrawn, Is.EqualTo(1));
            Vector3 head = ProjectileDirector.HeadOf(rig.Director.StreakDrawn(0));
            Assert.That(head.y, Is.EqualTo(floor).Within(1e-3f),
                "a bullet into an undrawn cellar must leave at the band's floor");

            // Control: the cellar drawn, the head goes on down into it.
            rig.Draw(Frame(102, down), alpha: 1f, lowest: 0);
            Assert.That(ProjectileDirector.HeadOf(rig.Director.StreakDrawn(0)).y, Is.LessThan(floor - 0.1f),
                "the control was cut too, so the clip is not what stopped it");
        }

        [Test]
        public void ABulletWithNeitherEndDrawnDrawsNothing()
        {
            Assert.That(ProjectileDirector.VisibleSpan(Vector3.zero, Vector3.one * 10f, false, 3f, false, 3f, out _, out _),
                Is.False);

            using var rig = new Rig();
            rig.Draw(Frame(100, Bullet(1, new CellRef(2, 5, 4), new CellRef(10, 5, 5), 95, 105)), highest: 2);
            Assert.That(rig.Director.LastStreaksDrawn, Is.Zero, "a bullet between two hidden storeys was drawn");
            Assert.That(rig.Director.LastDrawCalls, Is.Zero);

            // Control: one end in the band, and the part of the flight inside it is drawn.
            rig.Draw(Frame(95, Bullet(2, new CellRef(2, 5, 2), new CellRef(10, 5, 5), 95, 105)), highest: 2);
            Assert.That(rig.Director.LastStreaksDrawn, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- the body

        [Test]
        public void AHitEndsOnTheDrawnChestWhileTheTargetStepsOutOfTheEndCell()
        {
            var end = new CellRef(6, 6, 1);
            var next = new CellRef(7, 6, 1);
            var stepping = new PawnView(new PawnId(9), end, 800, 800, 600, nextCell: next, movePercent: 60, movePerMille: 600);
            Vector3 drawnChest = CellMetrics.FloorCentre(end) + new Vector3(1.5f, 1.4f, 0f);

            Assert.That(ProjectileDirector.HoldsEnd(stepping, end), Is.True, "a figure stepping out of the end cell holds it");
            Vector3 hit = ProjectileDirector.EndPoint(end, ProjectileDirector.HoldsEnd(stepping, end), true, drawnChest);
            Assert.That(Vector3.Distance(hit, drawnChest), Is.LessThan(1e-4f),
                "the streak stops in the air beside a walking body rather than on it");

            // Control: no target, the end cell's centre at chest height.
            Vector3 none = ProjectileDirector.EndPoint(end, targetHoldsEnd: false, hasDrawnChest: false, default);
            Assert.That(Vector3.Distance(none, CellMetrics.FloorCentre(end) + Vector3.up * ProjectileDirector.ChestHeight),
                Is.LessThan(1e-4f));

            // And a target that has walked clean off the end is not followed: the bullet goes to the cell.
            var gone = new PawnView(new PawnId(9), new CellRef(9, 6, 1), 800, 800, 600, nextCell: new CellRef(10, 6, 1));
            Assert.That(ProjectileDirector.HoldsEnd(gone, end), Is.False);
        }

        // ---------------------------------------------------------------- the eye

        [Test]
        public void AOneTickFlightStillLeavesAStreakForEightyMilliseconds()
        {
            using var rig = new Rig();
            ProjectileView pointBlank = Bullet(1, new CellRef(3, 3, 1), new CellRef(4, 3, 1), 100, 101);

            rig.Draw(Frame(100, pointBlank), alpha: 0.9f);
            Assert.That(rig.Director.LastStreaksDrawn, Is.EqualTo(1), "the bullet in flight");

            // It has landed: gone from the snapshot, and its streak stays where it ended.
            rig.Draw(Frame(101), alpha: 0.1f, seconds: 0.016f);
            Assert.That(rig.Director.LastStreaksDrawn, Is.EqualTo(1), "the afterimage is not drawn on the frame it lands");
            Assert.That(ProjectileDirector.FadeOf(rig.Director.StreakDrawn(0)), Is.EqualTo(1f).Within(1e-3f),
                "the first frame of the afterimage is at full strength");

            rig.Draw(Frame(102), seconds: 0.06f);
            Assert.That(rig.Director.LastStreaksDrawn, Is.EqualTo(1), "gone before 0.08 s");
            rig.Draw(Frame(103), seconds: 0.03f);
            Assert.That(rig.Director.LastStreaksDrawn, Is.Zero, "still there after 0.08 s");
            Assert.That(rig.Director.Following, Is.Zero, "a faded afterimage is still being followed");
        }

        [Test]
        public void AFlightThatFellBetweenTwoFramesIsStillSeen()
        {
            // At 3x a point-blank shot flies inside one frame: no snapshot ever carries it, and
            // only its Shot and its Hit arrive. It must still be seen.
            using var rig = new Rig();
            WorldSnapshot after = FrameWith(103, Standing(1, new CellRef(3, 3, 1)));
            rig.Director.OnCombatEvent(Shot(1, 101, new CellRef(5, 3, 1), flight: 1), after, null);
            rig.Director.OnCombatEvent(new CombatEventView(2, 102, CombatEventKind.Miss, new PawnId(1), new PawnId(2),
                new CellRef(5, 3, 1), 0, 7), after, null);

            rig.Draw(after, seconds: 0.016f);
            Assert.That(rig.Director.LastStreaksDrawn, Is.EqualTo(1), "a shot with no frame of flight was never drawn");
            Assert.That(ProjectileDirector.HeadOf(rig.Director.StreakDrawn(0)).x,
                Is.EqualTo(CellMetrics.FloorCentre(new CellRef(5, 3, 1)).x).Within(1e-3f), "a miss runs on to where it went down");
        }

        [Test]
        public void APausedBulletDoesNotMove()
        {
            using var rig = new Rig();
            WorldSnapshot frame = Frame(100, Bullet(1, new CellRef(2, 5, 1), new CellRef(30, 5, 1), 95, 110));

            rig.Draw(frame, alpha: 0.4f, seconds: 0.016f);
            Vector3 before = ProjectileDirector.HeadOf(rig.Director.StreakDrawn(0));

            // Paused: the tick and its alpha stand still while real time goes on.
            rig.Draw(frame, alpha: 0.4f, seconds: 0.5f);
            rig.Draw(frame, alpha: 0.4f, seconds: 0.5f);
            Vector3 held = ProjectileDirector.HeadOf(rig.Director.StreakDrawn(0));
            Assert.That(Vector3.Distance(before, held), Is.LessThan(1e-5f), "a paused bullet moved on real time");
            Assert.That(rig.Director.LastStreaksDrawn, Is.EqualTo(1), "a paused bullet faded away");

            // Control: the clock moving on moves it.
            rig.Draw(frame, alpha: 0.9f, seconds: 0.016f);
            Assert.That(ProjectileDirector.HeadOf(rig.Director.StreakDrawn(0)).x, Is.GreaterThan(before.x + 0.5f),
                "the control did not move either, so the test proves nothing");
        }

        [Test]
        public void TheTailIsAFractionOfTheFlightAndNeverUnderTwoMetres()
        {
            Vector3 from = Vector3.zero, to = new Vector3(40f, 0f, 0f);
            Assert.That(ProjectileDirector.StreakAt(from, to, 0.5f, 0f, 1f, out Vector3 tail, out Vector3 head), Is.True);
            Assert.That(head.x - tail.x, Is.EqualTo(40f * ProjectileDirector.TailFraction).Within(1e-3f));

            Vector3 near = new Vector3(5f, 0f, 0f);
            Assert.That(ProjectileDirector.StreakAt(from, near, 0.9f, 0f, 1f, out tail, out head), Is.True);
            Assert.That(head.x - tail.x, Is.EqualTo(ProjectileDirector.MinTailMetres).Within(1e-3f),
                "a short flight's streak is shorter than two metres");
        }

        // ---------------------------------------------------------------- the seam

        [Test]
        public void TheSeamHandsTheDirectorAShotAndThrowsDustOnlyForABulletOnADrawnLayer()
        {
            using var rig = new Rig();
            var feedback = new CombatFeedback
            {
                Projectiles = rig.Director,
                WeaponStyles = new AttackStyle?[] { AttackStyle.Light, AttackStyle.Pistol },
            };
            object world = new object();
            feedback.Consume(Frame(100), world, null, null);

            var snapshot = Frame(101);
            snapshot.AddCombatEvent(new CombatEventView(1, 101, CombatEventKind.Shot, new PawnId(1), new PawnId(2),
                new CellRef(8, 3, 1), 3, 1));
            snapshot.AddCombatEvent(new CombatEventView(2, 101, CombatEventKind.Miss, new PawnId(3), new PawnId(4),
                new CellRef(8, 8, 1), 0, 0));
            feedback.Consume(snapshot, world, null, null, lowestLayer: 0, highestLayer: 5);
            Assert.That(rig.Director.Following, Is.EqualTo(1), "the shot did not reach the director");
            Assert.That(feedback.DustThrown, Is.Zero, "a missed blow threw a bullet's dust");

            var landed = Frame(104);
            landed.AddCombatEvent(new CombatEventView(3, 104, CombatEventKind.Miss, new PawnId(1), new PawnId(2),
                new CellRef(8, 3, 1), 0, 1));
            feedback.Consume(landed, world, null, null, lowestLayer: 0, highestLayer: 5);
            Assert.That(feedback.DustThrown, Is.EqualTo(1), "a bullet into the ground threw no dust");

            var below = Frame(110);
            below.AddCombatEvent(new CombatEventView(4, 110, CombatEventKind.Miss, new PawnId(1), new PawnId(2),
                new CellRef(8, 3, 1), 0, 1));
            feedback.Consume(below, world, null, null, lowestLayer: 2, highestLayer: 5);
            Assert.That(feedback.DustThrown, Is.EqualTo(1), "dust was thrown on a layer the slice does not draw");
        }

        [Test]
        public void AShotIsTheCrackNearAndTheThumpFar()
        {
            Assert.That(CombatFeedback.ShotSoundFor(10f), Is.EqualTo(Odyssey.Presentation.Audio.SoundIds.CombatShot));
            Assert.That(CombatFeedback.ShotSoundFor(CombatFeedback.ShotNearMetres), Is.EqualTo(Odyssey.Presentation.Audio.SoundIds.CombatShot));
            Assert.That(CombatFeedback.ShotSoundFor(CombatFeedback.ShotNearMetres + 0.1f),
                Is.EqualTo(Odyssey.Presentation.Audio.SoundIds.CombatShotFar));
        }

        static CombatEventView Shot(int shooter, int tick, CellRef end, int flight) =>
            new CombatEventView(shooter * 10 + tick, tick, CombatEventKind.Shot, new PawnId(shooter), new PawnId(99),
                end, flight, 7);
    }
}
