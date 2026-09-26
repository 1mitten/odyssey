#nullable enable
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The cover rule (design 53 §2): the angle bands and the diagonal penalty, the shooter's
    /// distance, the descent, noisy-OR, and what each thing is worth. Each factor is tested alone
    /// against the number the design writes down, then together on a board, with the control
    /// beside every claim — the same wall from the side, the same sandbag from above.
    /// </summary>
    public class CoverTests
    {
        static CombatDef Combat => ContentPack.Pawns().Combat;

        /// <summary>A vector at <paramref name="degrees"/> from <c>(vx, vz)</c>, scaled up so the rounding is far below a band.</summary>
        static (int x, int z) At(double degrees, int vx, int vz)
        {
            double base0 = Math.Atan2(vz, vx);
            double a = base0 + degrees * Math.PI / 180.0;
            return ((int)Math.Round(Math.Cos(a) * 100_000), (int)Math.Round(Math.Sin(a) * 100_000));
        }

        // ---- the angle -------------------------------------------------------------------------

        [TestCase(0, 1000)]
        [TestCase(14, 1000)]
        [TestCase(16, 800)]
        [TestCase(26, 800)]
        [TestCase(28, 600)]
        [TestCase(39, 600)]
        [TestCase(41, 400)]
        [TestCase(51, 400)]
        [TestCase(53, 200)]
        [TestCase(64, 200)]
        [TestCase(66, 0)]
        [TestCase(90, 0)]
        [TestCase(180, 0)]
        public void ACardinalNeighboursBandsAreTheReferencesEdges(double degrees, int expected)
        {
            var (x, z) = At(degrees, 1, 0);
            Assert.That(Cover.AngleFactorPerMille(x, z, 1, 0), Is.EqualTo(expected));
            // The other side of the line is the same band: the rule has no handedness.
            var (mx, mz) = At(-degrees, 1, 0);
            Assert.That(Cover.AngleFactorPerMille(mx, mz, 1, 0), Is.EqualTo(expected));
        }

        /// <summary>A diagonal neighbour's angle counts ×1.75: its edges are 8.57°, 15.43°, 22.86°, 29.71°, 37.14°.</summary>
        [TestCase(0, 1000)]
        [TestCase(8, 1000)]
        [TestCase(9, 800)]
        [TestCase(15, 800)]
        [TestCase(16, 600)]
        [TestCase(22, 600)]
        [TestCase(23.5, 400)]
        [TestCase(29, 400)]
        [TestCase(30.5, 200)]
        [TestCase(37, 200)]
        [TestCase(38, 0)]
        [TestCase(45, 0)]
        public void ADiagonalNeighboursAngleCountsSevenQuartersAsMuch(double degrees, int expected)
        {
            var (x, z) = At(degrees, 1, 1);
            Assert.That(Cover.AngleFactorPerMille(x, z, 1, 1), Is.EqualTo(expected));
        }

        /// <summary>
        /// The reference players' rule of thumb falls out of the bands: a shot from due east meets the
        /// east neighbour in full and neither diagonal beside it; a shot from exactly north-east meets
        /// the north-east neighbour in full and the north and the east each at 40 %.
        /// </summary>
        [Test]
        public void OneNeighbourCountsAtNinetyDegreesAndThreeAtFortyFive()
        {
            Assert.That(Cover.AngleFactorPerMille(10, 0, 1, 0), Is.EqualTo(1000));
            Assert.That(Cover.AngleFactorPerMille(10, 0, 1, 1), Is.EqualTo(0));
            Assert.That(Cover.AngleFactorPerMille(10, 0, 1, -1), Is.EqualTo(0));

            Assert.That(Cover.AngleFactorPerMille(10, 10, 1, 1), Is.EqualTo(1000));
            Assert.That(Cover.AngleFactorPerMille(10, 10, 1, 0), Is.EqualTo(400));
            Assert.That(Cover.AngleFactorPerMille(10, 10, 0, 1), Is.EqualTo(400));
        }

        /// <summary>Mirroring the board in x, in z or through the diagonal changes no answer.</summary>
        [Test]
        public void TheAngleIsMirrorInvariant()
        {
            for (int ux = -9; ux <= 9; ux++)
            for (int uz = -9; uz <= 9; uz++)
            for (int vx = -1; vx <= 1; vx++)
            for (int vz = -1; vz <= 1; vz++)
            {
                int a = Cover.AngleFactorPerMille(ux, uz, vx, vz);
                Assert.That(Cover.AngleFactorPerMille(-ux, uz, -vx, vz), Is.EqualTo(a));
                Assert.That(Cover.AngleFactorPerMille(ux, -uz, vx, -vz), Is.EqualTo(a));
                Assert.That(Cover.AngleFactorPerMille(uz, ux, vz, vx), Is.EqualTo(a));
            }
        }

        // ---- the shooter's distance to the cover -----------------------------------------------

        [TestCase(1, 0, 333)]
        [TestCase(1, 1, 333)]
        [TestCase(0, 0, 333)]
        [TestCase(2, 0, 667)]
        [TestCase(2, 1, 667)]
        [TestCase(2, 2, 667)]
        [TestCase(3, 0, 1000)]
        [TestCase(8, 0, 1000)]
        public void AShooterNearTheCoverHasWalkedRoundMostOfIt(int dx, int dz, int expected) =>
            Assert.That(Cover.ShooterDistanceFactorPerMille(dx, dz), Is.EqualTo(expected));

        // ---- the descent -----------------------------------------------------------------------

        /// <summary>Design 53 §2b's worked table, one layer (3 m) above.</summary>
        [TestCase(25_000, false, 1000)]
        [TestCase(12_500, false, 877)]
        [TestCase(7_500, false, 572)]
        [TestCase(5_000, false, 190)]
        [TestCase(2_500, false, 0)]
        [TestCase(25_000, true, 1000)]
        [TestCase(5_000, true, 980)]
        [TestCase(2_500, true, 460)]
        public void AShotFromOneLayerUpIsGradedByItsDescent(int horizontalMm, bool tall, int expected) =>
            Assert.That(Cover.ElevationFactorPerMille(1, horizontalMm, tall, Combat), Is.EqualTo(expected));

        [Test]
        public void AShotFromLevelOrBelowKeepsAllOfIt()
        {
            Assert.That(Cover.ElevationFactorPerMille(0, 2_500, false, Combat), Is.EqualTo(1000));
            Assert.That(Cover.ElevationFactorPerMille(-3, 2_500, false, Combat), Is.EqualTo(1000));
            Assert.That(Cover.ElevationFactorPerMille(1, 0, true, Combat), Is.EqualTo(0), "straight overhead");
        }

        // ---- combining -------------------------------------------------------------------------

        [Test]
        public void CoverCombinesByNoisyOr()
        {
            int total = Cover.Combine(Cover.Combine(Cover.Combine(0, 550), 220), 220);
            Assert.That(total, Is.EqualTo(726), "1 - 0.45 x 0.78 x 0.78 in integers");
            Assert.That(Cover.Combine(0, 750), Is.EqualTo(750));
            Assert.That(Cover.Combine(750, 0), Is.EqualTo(750));
        }

        // ---- what things are worth -------------------------------------------------------------

        [Test]
        public void TheContentGivesDesignFiftysValues()
        {
            Assert.That(Combat.fullFillCoverPerMille, Is.EqualTo(750));
            BuildingDef Row(int handle) => ConstructionContent.BuildingAt(handle);
            Assert.That(Row(BuildingHandle.Shelf).coverPerMille, Is.EqualTo(500));
            Assert.That(Row(BuildingHandle.Generator).coverPerMille, Is.EqualTo(500));
            Assert.That(Row(BuildingHandle.Heater).coverPerMille, Is.EqualTo(400));
            Assert.That(Row(BuildingHandle.Bed).coverPerMille, Is.EqualTo(300));
            Assert.That(Row(BuildingHandle.Campfire).coverPerMille, Is.EqualTo(250));
            Assert.That(Row(BuildingHandle.Wall).coverPerMille, Is.EqualTo(0), "a wall is the full-fill rule's, not a number of its own");

            foreach (var plant in WorldContent.WildPlants)
            {
                bool tree = plant.defName.Contains("Tree") || plant.defName.Contains("Birch");
                Assert.That(plant.coverPerMille, Is.EqualTo(tree ? 250 : 150), plant.defName);
                Assert.That(plant.coverTall, Is.EqualTo(tree), plant.defName);
            }
        }

        // ---- on a board ------------------------------------------------------------------------

        static int East(ColonyWorld colony, int cell, int dx, int dz = 0)
        {
            CellRef c = Size.FromIndex(cell);
            return Size.Index(c.X + dx, c.Z + dz, c.Y);
        }

        static void Raise(ColonyWorld colony, int cell, int building, int stuff)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), building, stuff, 0), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
        }

        [Test]
        public void AWallBesideTheTargetCoversItFromTheWallsSideAndNotFromAbove()
        {
            var colony = Board();
            int target = Near(colony, -15, 12);
            int wall = East(colony, target, 1);
            Raise(colony, wall, BuildingHandle.Wall, StuffHandle.Stone);
            var report = new CoverReport();

            int fromEast = Cover.Evaluate(colony.Pawns, East(colony, target, 8), target, report);
            Assert.That(fromEast, Is.EqualTo(750));
            Assert.That(report.Count, Is.EqualTo(1));
            Assert.That(report.Cells[0], Is.EqualTo(wall));
            Assert.That(report.Tall[0], Is.True);

            // The control: the same wall seen from due north protects nothing.
            Assert.That(Cover.Evaluate(colony.Pawns, East(colony, target, 0, 8), target), Is.EqualTo(0));
            // And from due west the target stands in the open.
            Assert.That(Cover.Evaluate(colony.Pawns, East(colony, target, -8), target), Is.EqualTo(0));
        }

        [Test]
        public void AShooterStandingAtTheCoverHasBeatenTwoThirdsOfIt()
        {
            var colony = Board();
            int target = Near(colony, -15, 12);
            Raise(colony, East(colony, target, 1), BuildingHandle.Shelf, StuffHandle.Wood);

            Assert.That(Cover.Evaluate(colony.Pawns, East(colony, target, 8), target), Is.EqualTo(500));
            Assert.That(Cover.Evaluate(colony.Pawns, East(colony, target, 3), target), Is.EqualTo(333), "two cells from the shelf");
            Assert.That(Cover.Evaluate(colony.Pawns, East(colony, target, 2), target), Is.EqualTo(166), "one cell from the shelf");
        }

        [Test]
        public void TheShootersOwnCellIsNeverCover()
        {
            var colony = Board();
            int target = Near(colony, -15, 12);
            int shelf = East(colony, target, 1);
            Raise(colony, shelf, BuildingHandle.Shelf, StuffHandle.Wood);
            // A shooter can never stand in a shelf, but the rule does not rely on it.
            Assert.That(Cover.Evaluate(colony.Pawns, shelf, target), Is.EqualTo(0));
        }

        [Test]
        public void TwoPiecesRoundACornerCombine()
        {
            var colony = Board();
            int target = Near(colony, -15, 12);
            Raise(colony, East(colony, target, 1), BuildingHandle.Shelf, StuffHandle.Wood);
            Raise(colony, East(colony, target, 0, 1), BuildingHandle.Shelf, StuffHandle.Wood);
            Raise(colony, East(colony, target, 1, 1), BuildingHandle.Shelf, StuffHandle.Wood);
            var report = new CoverReport();

            // From exactly north-east: the corner piece in full and the two sides at 40 % each.
            int total = Cover.Evaluate(colony.Pawns, East(colony, target, 8, 8), target, report);
            Assert.That(report.Count, Is.EqualTo(3));
            int expected = Cover.Combine(Cover.Combine(Cover.Combine(0, 200), 200), 500);
            Assert.That(total, Is.EqualTo(Cover.Combine(Cover.Combine(Cover.Combine(0, report.PerMille[0]), report.PerMille[1]), report.PerMille[2])));
            Assert.That(report.PerMille.Take(3).OrderBy(v => v).ToArray(), Is.EqualTo(new[] { 200, 200, 500 }));
            Assert.That(total, Is.EqualTo(expected));
        }

        [Test]
        public void ThePickWalksTheContributorsInProportion()
        {
            var report = new CoverReport { Count = 2 };
            report.Cells[0] = 10; report.PerMille[0] = 300;
            report.Cells[1] = 20; report.PerMille[1] = 100;
            Assert.That(report.Sum, Is.EqualTo(400));
            Assert.That(report.CellForPick(0), Is.EqualTo(10));
            Assert.That(report.CellForPick(299), Is.EqualTo(10));
            Assert.That(report.CellForPick(300), Is.EqualTo(20));
            Assert.That(report.CellForPick(399), Is.EqualTo(20));
            Assert.That(report.CellForPick(400), Is.EqualTo(-1));
        }

        // ---- integers only ---------------------------------------------------------------------

        /// <summary>
        /// A cover value feeds a roll, and a roll feeds the hash: the rule must not touch a float, a
        /// double or a library angle. Read the file rather than trust a comment.
        /// </summary>
        [Test]
        public void TheRuleNeverTouchesAFloat()
        {
            string file = Path.Combine(SimSourceRoot(), "Pawns", "Combat", "Cover.cs");
            string[] code = File.ReadAllLines(file).Select(l => l.Split("//")[0]).Where(l => !l.TrimStart().StartsWith("///")).ToArray();
            foreach (string banned in new[] { "float", "double", "Math.", "MathF", "Atan" })
                Assert.That(code.Any(l => l.Contains(banned)), Is.False, $"Cover.cs uses {banned}");
        }

        static string SimSourceRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "Assets", "Odyssey", "Sim");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("could not find Assets/Odyssey/Sim");
        }
    }
}
