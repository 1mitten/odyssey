#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The ambient birds (design 50): the shape built in code, the size that grows with zoom, and
    /// the flock model stepped against a fake board of trees. Everything a person at the keyboard
    /// would otherwise have to wait for — a dusk, a storm, a colonist walking under a tree — is
    /// driven here in seconds.
    /// </summary>
    public class BirdTests
    {
        const float Standard = 300f; // 120 cells of 2.5 m

        // ------------------------------------------------------------------ the shape

        [Test]
        public void ARookIsTwentyEightTrianglesAndABuzzardThirtyTwo()
        {
            Assert.That(BirdShape.TriangleCount(BirdSpecies.Rook), Is.EqualTo(28));
            Assert.That(BirdShape.TriangleCount(BirdSpecies.Buzzard), Is.EqualTo(32),
                "the buzzard's fingered tips are two more a side");
        }

        [Test]
        public void EveryBirdIsItsOwnMirrorImageAcrossTheKeel(
            [Values(BirdKind.Rook, BirdKind.Buzzard)] BirdKind kind)
        {
            BirdShape.Build(BirdSpecies.Of(kind), out float[] p, out _, out BirdColour[] c, out _);
            var faces = new List<string>();
            var mirrored = new List<string>();
            for (int t = 0; t < p.Length / 9; t++)
            {
                faces.Add(Face(p, t, 1f, c[t * 3]));
                mirrored.Add(Face(p, t, -1f, c[t * 3]));
            }
            faces.Sort(StringComparer.Ordinal);
            mirrored.Sort(StringComparer.Ordinal);
            Assert.That(mirrored, Is.EqualTo(faces),
                "every face has a twin with x negated and the same colour, or the bird flies lopsided");
        }

        [Test]
        public void OnlyTheWingsMoveAndTheTipsMoveMost(
            [Values(BirdKind.Rook, BirdKind.Buzzard)] BirdKind kind)
        {
            BirdShape.Build(BirdSpecies.Of(kind), out float[] p, out float[] w, out _, out _);
            float widest = 0f;
            for (int v = 0; v < w.Length; v++)
            {
                float x = Math.Abs(p[v * 3]);
                Assert.That(w[v], Is.InRange(0f, 1f));
                if (x < 0.1f) Assert.That(w[v], Is.EqualTo(0f), "the body and tail never flap");
                if (w[v] >= 0.99f) widest = Math.Max(widest, x);
            }
            Assert.That(widest, Is.GreaterThan(0.45f), "the full weight is out at the tip");
        }

        [Test]
        public void TheWingspanIsOne(
            [Values(BirdKind.Rook, BirdKind.Buzzard)] BirdKind kind)
        {
            BirdShape.Build(BirdSpecies.Of(kind), out float[] p, out _, out _, out _);
            float min = float.MaxValue, max = float.MinValue;
            for (int v = 0; v < p.Length / 3; v++)
            {
                min = Math.Min(min, p[v * 3]);
                max = Math.Max(max, p[v * 3]);
            }
            // The fingered tip reaches a little past the half-span: within 10 % of one either way.
            Assert.That(max - min, Is.EqualTo(1f).Within(0.1f));
        }

        [Test]
        public void NoTriangleIsDegenerate(
            [Values(BirdKind.Rook, BirdKind.Buzzard)] BirdKind kind)
        {
            BirdShape.Build(BirdSpecies.Of(kind), out float[] p, out _, out _, out int[] tris);
            for (int t = 0; t < tris.Length; t += 3)
            {
                float ax = p[tris[t + 1] * 3] - p[tris[t] * 3], ay = p[tris[t + 1] * 3 + 1] - p[tris[t] * 3 + 1], az = p[tris[t + 1] * 3 + 2] - p[tris[t] * 3 + 2];
                float bx = p[tris[t + 2] * 3] - p[tris[t] * 3], by = p[tris[t + 2] * 3 + 1] - p[tris[t] * 3 + 1], bz = p[tris[t + 2] * 3 + 2] - p[tris[t] * 3 + 2];
                float cx = ay * bz - az * by, cy = az * bx - ax * bz, cz = ax * by - ay * bx;
                Assert.That(Math.Sqrt(cx * cx + cy * cy + cz * cz), Is.GreaterThan(1e-6), $"triangle {t / 3}");
            }
        }

        static string Face(float[] p, int t, float sign, BirdColour colour)
        {
            var corners = new List<string>();
            for (int k = 0; k < 3; k++)
            {
                int v = (t * 3 + k) * 3;
                corners.Add($"{Math.Round(p[v] * sign, 4) + 0.0:F4},{Math.Round(p[v + 1], 4):F4},{Math.Round(p[v + 2], 4):F4}");
            }
            corners.Sort(StringComparer.Ordinal);
            return colour + ":" + string.Join("|", corners);
        }

        // ------------------------------------------------------------------ the size

        [Test]
        public void ABirdIsLifeSizeCloseInAndSeventyFivePercentBiggerFarOut()
        {
            Assert.That(BirdScale.For(10f), Is.EqualTo(1f));
            Assert.That(BirdScale.For(BirdScale.Near), Is.EqualTo(1f));
            Assert.That(BirdScale.For(160f), Is.EqualTo(1.75f));
            Assert.That(BirdScale.For(BirdScale.Far), Is.EqualTo(1.75f).Within(1e-5f));

            float last = 0f;
            for (float d = 0f; d <= 200f; d += 1f)
            {
                float s = BirdScale.For(d);
                Assert.That(s, Is.GreaterThanOrEqualTo(last), $"never shrinks as the camera pulls back ({d} m)");
                last = s;
            }
        }

        // ------------------------------------------------------------------ the flock

        [Test]
        public void AStandardBoardHasTwoRookFlocksAndABuzzardAndNoBoardHasMoreThanTheCap()
        {
            BirdSky.FlocksFor(Standard, Standard, out int rooks, out int buzzards);
            Assert.That(rooks, Is.EqualTo(2));
            Assert.That(buzzards, Is.EqualTo(1));

            var huge = new BirdSky(600f, 600f, new FakeBoard(), 7);
            Assert.That(huge.Count, Is.LessThanOrEqualTo(BirdSky.MaxBirds));
            Assert.That(huge.Flocks, Has.Count.EqualTo(5 + 3));

            var tiny = new BirdSky(100f, 100f, new FakeBoard(), 7);
            Assert.That(tiny.Flocks, Has.Count.EqualTo(2), "every board has at least one of each");
        }

        [Test]
        public void TheSameSeedFliesTheSameSky()
        {
            var a = new BirdSky(Standard, Standard, FakeBoard.Wood(), 42);
            var b = new BirdSky(Standard, Standard, FakeBoard.Wood(), 42);
            Run(a, 120f, BirdConditions.ClearNoon);
            Run(b, 120f, BirdConditions.ClearNoon);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(b.Birds[i].X, Is.EqualTo(a.Birds[i].X));
                Assert.That(b.Birds[i].Y, Is.EqualTo(a.Birds[i].Y));
                Assert.That(b.Birds[i].State, Is.EqualTo(a.Birds[i].State));
            }
        }

        [Test]
        public void APausedSkyHoldsStill()
        {
            var sky = new BirdSky(Standard, Standard, FakeBoard.Wood(), 3);
            Run(sky, 30f, BirdConditions.ClearNoon);
            float x = sky.Birds[0].X, elapsed = sky.Elapsed;
            sky.Step(0f, BirdConditions.ClearNoon, default);
            Assert.That(sky.Birds[0].X, Is.EqualTo(x));
            Assert.That(sky.Elapsed, Is.EqualTo(elapsed));
        }

        [Test]
        public void AHitchCannotFlingABirdAcrossTheBoard()
        {
            var sky = new BirdSky(Standard, Standard, FakeBoard.Wood(), 3);
            Run(sky, 10f, BirdConditions.ClearNoon);
            float before = sky.Elapsed;
            sky.Step(30f, BirdConditions.ClearNoon, default);
            Assert.That(sky.Elapsed - before, Is.EqualTo(BirdSky.MaxSteps * BirdSky.StepSeconds).Within(1e-4f),
                "a thirty-second frame advances the birds half a second, not thirty");
        }

        [Test]
        public void RooksComeDownOnTreetopsByDay()
        {
            var board = FakeBoard.Wood();
            var sky = new BirdSky(Standard, Standard, board, 11);
            bool sawPerched = false;
            for (int s = 0; s < 240 * 10 && !sawPerched; s++)
            {
                sky.Step(0.1f, BirdConditions.ClearNoon, default);
                foreach (Bird bird in sky.Birds)
                {
                    if (bird.State != BirdState.Perched) continue;
                    sawPerched = true;
                    Assert.That(bird.Kind, Is.EqualTo(BirdKind.Rook), "only rooks perch");
                    Assert.That(board.IsOnATree(bird.X, bird.Y, bird.Z), Is.True,
                        $"a perched rook sits on a crown, not in the air ({bird.X:F1}, {bird.Y:F1}, {bird.Z:F1})");
                }
            }
            Assert.That(sawPerched, Is.True, "within four minutes a rook flock has come down");
        }

        [Test]
        public void AColonistWalkingUnderAPerchedFlockPutsItUp()
        {
            var sky = new BirdSky(Standard, Standard, FakeBoard.Wood(), 11);
            BirdFlock flock = RunUntilPerched(sky);
            Bird sitter = sky.Birds[flock.First];

            var walker = new[] { sitter.X + 3f, sitter.Y - 12f + 5f, sitter.Z };
            sky.Step(0.1f, BirdConditions.ClearNoon, walker);

            Assert.That(flock.Mode, Is.EqualTo(FlockMode.Scattered));
            for (int i = flock.First; i < flock.First + flock.Count; i++)
            {
                Assert.That(sky.Birds[i].State, Is.EqualTo(BirdState.Flying), "the whole flock goes, not one bird");
                Assert.That(sky.Birds[i].VY, Is.GreaterThan(0f), "and it goes up");
            }
        }

        [Test]
        public void AColonistAStoreyTooFarDownLeavesThemBe()
        {
            var sky = new BirdSky(Standard, Standard, FakeBoard.Wood(), 11);
            BirdFlock flock = RunUntilPerched(sky);
            Bird sitter = sky.Birds[flock.First];

            var walker = new[] { sitter.X + 3f, sitter.Y - BirdSky.ScareHeight - 1f, sitter.Z };
            sky.Step(0.1f, BirdConditions.ClearNoon, walker);
            Assert.That(flock.Mode, Is.EqualTo(FlockMode.Perched));

            var farAway = new[] { sitter.X + BirdSky.ScareRadius + 2f, sitter.Y, sitter.Z };
            sky.Step(0.1f, BirdConditions.ClearNoon, farAway);
            Assert.That(flock.Mode, Is.EqualTo(FlockMode.Perched));
        }

        [Test]
        public void FellingTheirTreePutsThemUp()
        {
            var board = FakeBoard.Wood();
            var sky = new BirdSky(Standard, Standard, board, 11);
            BirdFlock flock = RunUntilPerched(sky);
            Bird sitter = sky.Birds[flock.First];

            sky.Disturb(sitter.PerchX - 1f, sitter.PerchZ - 1f, sitter.PerchX + 1f, sitter.PerchZ + 1f);
            sky.Step(0.1f, BirdConditions.ClearNoon, default);
            Assert.That(flock.Mode, Is.EqualTo(FlockMode.Scattered));
        }

        [Test]
        public void APerchTheBoardNoLongerHoldsPutsThemUp()
        {
            var board = FakeBoard.Wood();
            var sky = new BirdSky(Standard, Standard, board, 11);
            BirdFlock flock = RunUntilPerched(sky);

            board.CutAll();
            Run(sky, 1f, BirdConditions.ClearNoon);
            Assert.That(flock.Mode, Is.Not.EqualTo(FlockMode.Perched),
                "a roof the slice cut away, or a tree gone by any road, is not sat on");
        }

        [Test]
        public void AtNightEveryRookIsAtTheRookeryAndNoneIsFlying()
        {
            var sky = new BirdSky(Standard, Standard, FakeBoard.Wood(), 5);
            Run(sky, 60f, BirdConditions.ClearNoon);
            Run(sky, 240f, new BirdConditions(20f, WeatherKind.Clear, 0f));

            Assert.That(sky.HasRookery, Is.True);
            foreach (Bird bird in sky.Birds)
            {
                if (bird.Kind == BirdKind.Buzzard)
                {
                    Assert.That(bird.State, Is.EqualTo(BirdState.Away), "the buzzard is off the board after dark");
                    continue;
                }
                Assert.That(bird.State, Is.EqualTo(BirdState.Perched));
                float dx = bird.X - sky.RookeryX, dz = bird.Z - sky.RookeryZ;
                Assert.That(Math.Sqrt(dx * dx + dz * dz), Is.LessThan(30f), "roosting together, at one stand");
            }
            foreach (BirdFlock flock in sky.Flocks)
                if (flock.Kind == BirdKind.Rook) Assert.That(flock.Roosting, Is.True);
        }

        [Test]
        public void AColonyLoadedAtNightStartsWithItsRooksAlreadyAtRoost()
        {
            var sky = new BirdSky(Standard, Standard, FakeBoard.Wood(), 5);
            sky.Step(0.1f, new BirdConditions(23f, WeatherKind.Clear, 0f), default);
            Assert.That(sky.CountIn(BirdState.Flying) + sky.CountIn(BirdState.Landing), Is.EqualTo(0),
                "nobody is seen flying home through the dark on the first frame");
        }

        [Test]
        public void AtDawnTheRooksGoOutAgain()
        {
            var sky = new BirdSky(Standard, Standard, FakeBoard.Wood(), 5);
            sky.Step(0.1f, new BirdConditions(23f, WeatherKind.Clear, 0f), default);
            Run(sky, 2f, new BirdConditions(7f, WeatherKind.Clear, 0f));
            foreach (BirdFlock flock in sky.Flocks)
                if (flock.Kind == BirdKind.Rook) Assert.That(flock.Mode, Is.EqualTo(FlockMode.Cruising));
        }

        [Test]
        public void InAStormNoRookIsInTheAir()
        {
            var sky = new BirdSky(Standard, Standard, FakeBoard.Wood(), 9);
            Run(sky, 30f, BirdConditions.ClearNoon);
            Run(sky, 240f, new BirdConditions(12f, WeatherKind.Storm, 1f));
            foreach (Bird bird in sky.Birds)
            {
                if (bird.Kind == BirdKind.Rook) Assert.That(bird.State, Is.EqualTo(BirdState.Perched));
                else Assert.That(bird.State, Is.EqualTo(BirdState.Away), "the buzzard does not hunt in a storm");
            }
        }

        [Test]
        public void TheBuzzardCirclesByDayAndLeavesInTheRain()
        {
            var sky = new BirdSky(Standard, Standard, FakeBoard.Wood(), 9);
            Run(sky, 60f, BirdConditions.ClearNoon);
            Bird buzzard = BuzzardOf(sky);
            Assert.That(buzzard.State, Is.EqualTo(BirdState.Flying));
            Assert.That(buzzard.Y, Is.GreaterThan(20f), "it soars well above the treetops");

            Run(sky, 240f, new BirdConditions(12f, WeatherKind.Rain, 0.6f));
            Assert.That(BuzzardOf(sky).State, Is.EqualTo(BirdState.Away));

            Run(sky, 5f, BirdConditions.ClearNoon);
            Assert.That(BuzzardOf(sky).State, Is.EqualTo(BirdState.Flying), "and comes back when it clears");
        }

        [Test]
        public void ABoardWithNothingToSitOnKeepsItsRooksFlyingAndSendsThemOffAtDusk()
        {
            var sky = new BirdSky(Standard, Standard, new FakeBoard(), 13);
            Run(sky, 240f, BirdConditions.ClearNoon);
            Assert.That(sky.CountIn(BirdState.Perched), Is.EqualTo(0));
            Assert.That(sky.CountIn(BirdState.Flying), Is.EqualTo(sky.Count));

            Run(sky, 300f, new BirdConditions(21f, WeatherKind.Clear, 0f));
            Assert.That(sky.CountIn(BirdState.Away), Is.EqualTo(sky.Count), "they leave rather than circle in the dark");
        }

        [Test]
        public void TheBirdsStayOverTheBoard()
        {
            var sky = new BirdSky(Standard, Standard, FakeBoard.Wood(), 21);
            for (int s = 0; s < 600 * 10; s++)
            {
                sky.Step(0.1f, BirdConditions.ClearNoon, default);
                if (s % 50 != 0) continue;
                foreach (Bird bird in sky.Birds)
                {
                    if (bird.State == BirdState.Away) continue;
                    Assert.That(bird.X, Is.InRange(-40f, Standard + 40f));
                    Assert.That(bird.Z, Is.InRange(-40f, Standard + 40f));
                    Assert.That(bird.Y, Is.InRange(0f, 80f));
                    Assert.That(float.IsNaN(bird.X) || float.IsNaN(bird.Yaw), Is.False);
                }
            }
        }

        [Test]
        public void AFlockFliesTogether()
        {
            var sky = new BirdSky(Standard, Standard, new FakeBoard(), 17);
            Run(sky, 120f, BirdConditions.ClearNoon);
            BirdFlock flock = sky.Flocks[0];
            float cx = 0f, cz = 0f;
            for (int i = flock.First; i < flock.First + flock.Count; i++)
            {
                cx += sky.Birds[i].X;
                cz += sky.Birds[i].Z;
            }
            cx /= flock.Count;
            cz /= flock.Count;
            for (int i = flock.First; i < flock.First + flock.Count; i++)
            {
                float dx = sky.Birds[i].X - cx, dz = sky.Birds[i].Z - cz;
                Assert.That(Math.Sqrt(dx * dx + dz * dz), Is.LessThan(20f), "a loose flock, not a scatter of strays");
            }
        }

        // ------------------------------------------------------------------ helpers

        static void Run(BirdSky sky, float seconds, BirdConditions conditions)
        {
            for (float t = 0f; t < seconds; t += 0.1f)
                sky.Step(0.1f, conditions, default);
        }

        static BirdFlock RunUntilPerched(BirdSky sky)
        {
            for (int s = 0; s < 600 * 10; s++)
            {
                sky.Step(0.1f, BirdConditions.ClearNoon, default);
                foreach (BirdFlock flock in sky.Flocks)
                    if (flock.Mode == FlockMode.Perched) return flock;
            }
            Assert.Fail("no flock came down in ten minutes");
            return null!;
        }

        static Bird BuzzardOf(BirdSky sky)
        {
            foreach (Bird bird in sky.Birds)
                if (bird.Kind == BirdKind.Buzzard) return bird;
            throw new InvalidOperationException("no buzzard");
        }

        /// <summary>
        /// A flat board with trees 12 m tall standing on a 20 m grid, or none. Perches are crown
        /// tops; the ceiling is the crown's top within its reach and the ground elsewhere.
        /// </summary>
        sealed class FakeBoard : IBirdPerches
        {
            const float CrownTop = 12f, Reach = 2.5f;
            readonly List<(float X, float Z)> _trees = new List<(float X, float Z)>();
            bool _cut;

            public static FakeBoard Wood()
            {
                var board = new FakeBoard();
                for (float x = 10f; x < Standard; x += 20f)
                for (float z = 10f; z < Standard; z += 20f)
                    board._trees.Add((x, z));
                return board;
            }

            public void CutAll() => _cut = true;

            public int Near(float x, float z, float radius, BirdPerch[] into)
            {
                if (_cut) return 0;
                var hits = new List<(float D, int I)>();
                for (int i = 0; i < _trees.Count; i++)
                {
                    float dx = _trees[i].X - x, dz = _trees[i].Z - z;
                    float d = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (d <= radius) hits.Add((d, i));
                }
                hits.Sort((a, b) => a.D != b.D ? a.D.CompareTo(b.D) : a.I.CompareTo(b.I));
                int n = Math.Min(into.Length, hits.Count);
                for (int k = 0; k < n; k++)
                {
                    var tree = _trees[hits[k].I];
                    into[k] = new BirdPerch(tree.X, CrownTop, tree.Z, Reach, hits[k].I);
                }
                return n;
            }

            public float Ceiling(float x, float z)
            {
                if (_cut) return 0f;
                foreach (var tree in _trees)
                    if (Math.Abs(tree.X - x) <= Reach && Math.Abs(tree.Z - z) <= Reach) return CrownTop;
                return 0f;
            }

            public bool Holds(in BirdPerch perch) => !_cut;

            public bool IsOnATree(float x, float y, float z)
            {
                foreach (var tree in _trees)
                {
                    float dx = tree.X - x, dz = tree.Z - z;
                    if (dx * dx + dz * dz <= Reach * Reach && y <= CrownTop + 0.01f && y >= CrownTop - 0.5f) return true;
                }
                return false;
            }
        }
    }
}
