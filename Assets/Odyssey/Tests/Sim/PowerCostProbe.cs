#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Power;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What a power net costs to solve, at the scale target (design 32 §11, process §3).
    ///
    /// <para><b>The number that matters is the edit, not the rest.</b> A clean grid costs one
    /// flag test, so the question is what one line laid or taken up costs the tick it happens
    /// on: the whole solve runs again, linear in lines. This lays a serpentine of lines across
    /// several layers — joined by risers, so it is one net as a player's would be — and times a
    /// solve after each of a run of single-cell edits.</para>
    ///
    /// <para><c>[Explicit]</c>, like every other probe: it measures a machine as much as a
    /// program and must never decide whether a pull request lands.</para>
    /// </summary>
    public class PowerCostProbe
    {
        [Test, Explicit, Category("Benchmark")]
        public void OneLineEditAtTheScaleTarget()
        {
            foreach (int lines in new[] { 500, 2_000, 10_000 }) Measure(lines);
        }

        static void Measure(int count)
        {
            GridSize size = GridSize.ScaleTarget;
            var cells = new CellGrid(size);
            var power = new PowerGrid(cells, new List<PlacedEdifice>());

            // A serpentine on layer 20 upward, rows of 200 along X, stepping one layer up at the
            // end of every tenth row so the net climbs as a real one would.
            int laid = 0, y = 20, z = 5;
            while (laid < count)
            {
                for (int x = 10; x < 210 && laid < count; x++, laid++) power.AddLine(size.Index(x, z, y));
                z += 1;
                if (z % 10 == 0 && y + 1 < size.SizeY) y++;
            }
            power.EnsureSolved();

            // One edit at a time: take the middle line up and put it back, solving after each.
            int middle = power.Lines[power.Lines.Count / 2];
            const int Edits = 50;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Edits; i++)
            {
                if ((i & 1) == 0) power.MarkRemoval(middle);
                if ((i & 1) == 0) power.TakeUp(middle); else power.AddLine(middle);
                power.EnsureSolved();
            }
            double solveMs = sw.Elapsed.TotalMilliseconds / Edits;

            sw.Restart();
            for (int i = 0; i < 10_000; i++) power.EnsureSolved();
            double cleanUs = sw.Elapsed.TotalMilliseconds * 1_000 / 10_000;

            string line = $"[power] {count} lines at {size.SizeX}x{size.SizeZ}x{size.SizeY}: " +
                          $"{power.Nets.Count} net(s); one edit's solve {solveMs:F4} ms; a clean ask {cleanUs:F4} us";
            TestContext.WriteLine(line);
            Console.WriteLine(line);
        }
    }
}
