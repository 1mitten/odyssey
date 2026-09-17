#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// How often a walking colonist changes the direction it faces, measured rather than argued.
    ///
    /// <para><b>Why this exists.</b> The owner reported (2026-09-17) that colonists "jolt when
    /// around certain areas of terrain … like it keeps snapping in 2 directions quickly", most
    /// often near water. Two candidates were proposed from reading the code, and the standing rule
    /// in <c>docs/lessons.md</c> is that reading code has been wrong every time — so this measures
    /// the first of them before anybody fixes anything.</para>
    ///
    /// <para><b>The candidate.</b> <see cref="PathFinder"/> is orthogonal-only by design and there
    /// is no path smoothing anywhere in <c>Odyssey.Sim.Pathing</c>. A colonist's drawn bearing is
    /// the raw step vector (<c>PawnPose.YawOf</c>), so a journey with any diagonal component in it
    /// comes out as a staircase — north, east, north, east — and the figure turns ninety degrees
    /// on every cell for the whole walk. Open ground makes the fewest demands of a path, so if the
    /// staircase is there it is there everywhere and this does not need a pond to show it.</para>
    ///
    /// <para><b>What the numbers mean.</b> A turn is a change of orthogonal direction between two
    /// consecutive steps. The ideal for a straight-line journey between two points is <b>1</b>: go
    /// along, turn once, go up. Anything approaching one turn per cell is a staircase, and a
    /// staircase is the thing being reported.</para>
    ///
    /// <para>It asserts nothing about what the number ought to be, on purpose. This is an
    /// instrument, not a gate: the fix has not been chosen yet, and a threshold written before the
    /// fix would be a guess dressed as a requirement. It does assert that the instrument works —
    /// that a straight walk turns none — so a later change cannot quietly make it measure
    /// nothing.</para>
    /// </summary>
    public class WalkHeadingMeasurementTests
    {
        /// <summary>One flat open layer, floored everywhere: the fewest possible demands on a path.</summary>
        static NavGraph OpenBoard(int size, out CellGrid cells)
        {
            cells = NavWorld.MakeCells(size, size, 1);
            var nav = new NavGraph(cells);
            nav.Rebuild();
            return nav;
        }

        /// <summary>
        /// How many times a path changes orthogonal direction, and over how many steps.
        ///
        /// <para>Direction is the step itself rather than an angle, because on a 4-connected grid
        /// those are the same thing and a comparison of integers cannot drift.</para>
        /// </summary>
        static (int turns, int steps) Turns(int[] path, GridSize size)
        {
            if (path.Length < 3) return (0, Math.Max(0, path.Length - 1));

            int turns = 0;
            int previous = path[1] - path[0];
            for (int i = 2; i < path.Length; i++)
            {
                int step = path[i] - path[i - 1];
                if (step != previous) turns++;
                previous = step;
            }

            return (turns, path.Length - 1);
        }

        [Test]
        public void AStraightWalkTurnsNotAtAll()
        {
            NavGraph nav = OpenBoard(40, out CellGrid cells);
            var finder = new PathFinder(nav);

            PathResult result = finder.FindPath(
                cells.Index(2, 20, 0), cells.Index(37, 20, 0), TraverseMode.Colonist);
            Assert.That(result.Ok, Is.True);

            (int turns, int steps) = Turns(finder.PathToArray(), cells.Size);
            Assert.That(steps, Is.EqualTo(35));
            Assert.That(turns, Is.Zero, "the instrument reports a turn where there is none");
        }

        /// <summary>
        /// The measurement itself: a diagonal walk across open ground, and how many times the
        /// colonist walking it has to turn.
        /// </summary>
        [Test]
        public void HowOftenADiagonalWalkTurns()
        {
            NavGraph nav = OpenBoard(40, out CellGrid cells);
            var finder = new PathFinder(nav);

            PathResult result = finder.FindPath(
                cells.Index(2, 2, 0), cells.Index(32, 32, 0), TraverseMode.Colonist);
            Assert.That(result.Ok, Is.True);

            (int turns, int steps) = Turns(finder.PathToArray(), cells.Size);

            // A flat cell is 100 cost units and a colonist retires 1 a tick at sixty ticks a
            // second, so a step is 1.67 s and a turn is one every so many seconds of walking.
            double secondsPerTurn = turns == 0 ? double.PositiveInfinity : steps * 100.0 / 60.0 / turns;

            string report =
                $"MEASURED diagonal walk (2,2) -> (32,32) on open ground: " +
                $"{steps} steps, {turns} turns, " +
                $"{(steps == 0 ? 0 : turns * 100.0 / steps):F1}% of steps change direction, " +
                $"a turn every {secondsPerTurn:F2} s of walking. " +
                $"An unsmoothed straight line would turn once.";

            // Written to a file as well as to the test output, because `dotnet test` hides the
            // output of a passing test and an instrument nobody can read is not an instrument.
            // The same argument as the PlayMode palette shots in `Logs/`.
            TestContext.WriteLine(report);
            Report(report);

            Assert.That(steps, Is.EqualTo(60), "a 4-connected walk of 30 by 30 is 60 steps");
        }

        /// <summary>
        /// The case the report is actually about: a walk forced along a diagonal shore.
        ///
        /// <para>Open ground is where a path has the most freedom, and the freedom is what let the
        /// measurement above come out at five turns in sixty steps — the search runs straight and
        /// turns only where it must. A shoreline takes that freedom away. Deep water is
        /// <c>impassable</c> (<c>Terrain.xml</c>), and a stream that runs diagonally therefore
        /// presents a staircase of blocked cells with no straight run anywhere along it, so a
        /// colonist following the bank has to step across and along, across and along.</para>
        ///
        /// <para>The water is modelled here as solid cells rather than as water, deliberately: what
        /// the path sees of deep water is exactly "cannot enter", and building a real stream would
        /// bring worldgen into a measurement that is about the shape of an obstacle.</para>
        /// </summary>
        [Test]
        public void HowOftenAWalkAlongADiagonalShoreTurns()
        {
            NavGraph nav = OpenBoard(40, out CellGrid cells);

            // A diagonal band two cells thick, from one corner towards the other: the staircase a
            // stream cutting across the board presents to anything walking beside it.
            for (int x = 0; x < 40; x++)
            {
                for (int d = 0; d < 2; d++)
                {
                    int z = x + d;
                    if (z < 0 || z >= 40) continue;
                    NavWorld.SetSolid(cells, nav, cells.Index(x, z, 0), true);
                }
            }

            nav.Rebuild();
            var finder = new PathFinder(nav);

            // Both ends on the same side of the band, so the walk runs along the shore rather than
            // looking for a crossing that does not exist.
            PathResult result = finder.FindPath(
                cells.Index(6, 2, 0), cells.Index(36, 32, 0), TraverseMode.Colonist);
            Assert.That(result.Ok, Is.True, "the shore walk found no path at all");

            (int turns, int steps) = Turns(finder.PathToArray(), cells.Size);
            double secondsPerTurn = turns == 0 ? double.PositiveInfinity : steps * 100.0 / 60.0 / turns;

            string report =
                $"MEASURED walk along a diagonal shore (6,2) -> (36,32): " +
                $"{steps} steps, {turns} turns, " +
                $"{(steps == 0 ? 0 : turns * 100.0 / steps):F1}% of steps change direction, " +
                $"a turn every {secondsPerTurn:F2} s of walking.";

            TestContext.WriteLine(report);
            Append(report);
        }

        static void Report(string line) => Write(line, append: false);

        static void Append(string line) => Write(line, append: true);

        static void Write(string line, bool append)
        {
            string directory = System.IO.Path.Combine(RepoPaths.Root, "Logs");
            System.IO.Directory.CreateDirectory(directory);
            string path = System.IO.Path.Combine(directory, "walk-heading.txt");
            if (append) System.IO.File.AppendAllText(path, line + Environment.NewLine);
            else System.IO.File.WriteAllText(path, line + Environment.NewLine);
        }
    }
}
