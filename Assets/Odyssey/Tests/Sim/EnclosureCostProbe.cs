#nullable enable
using System;
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Review probe for PR #164: what the enclosure solve costs on the offered boards, at rest
    /// and under one structural edit per tick. Uses only the API main and the branch share, so
    /// the same file runs on both and the two numbers are comparable.
    /// </summary>
    public class EnclosureCostProbe
    {
        [Test, Explicit, Category("Benchmark")]
        public void TheEnclosureSolveOnEveryOfferedBoard()
        {
            Measure("standard, wooded", BoardSizes.Standard, wooded: true);
            Measure("huge, wooded", BoardSizes.Huge, wooded: true);
            Measure("scale target, barren", GridSize.ScaleTarget, wooded: false);
        }

        static void Measure(string name, GridSize size, bool wooded)
        {
            ColonyWorld colony = ColonyWorld.Build(size, 12345u, ScenarioDef.Bare(),
                mapType: MapType.Natural, wooded: wooded);
            EnclosureGrid enclosure = colony.Pawns.Enclosure!;
            SimWorld world = colony.World;
            int cellCount = size.CellCount;
            uint s = 777u;

            // The initial solve, every layer dirty — what a load or a new game pays once.
            enclosure.MarkAllDirty();
            var sw = Stopwatch.StartNew();
            enclosure.Tick(world);
            double initialMs = sw.Elapsed.TotalMilliseconds;

            // One edit per tick, the enclosure alone: mark a random cell, solve.
            const int Edits = 200;
            double total = 0, worst = 0;
            for (int i = 0; i < Edits; i++)
            {
                int cell = (int)(Next(ref s) % (uint)cellCount);
                enclosure.MarkDirty(cell);
                sw.Restart();
                enclosure.Tick(world);
                double ms = sw.Elapsed.TotalMilliseconds;
                total += ms;
                if (ms > worst) worst = ms;
            }

            // The whole tick, with and without one enclosure-dirtying edit a tick, 240 ticks
            // each so the temperature pass (every 120) is inside the window on the branch.
            const int Ticks = 240;
            for (int i = 0; i < 20; i++) world.Tick(); // warm
            sw.Restart();
            for (int i = 0; i < Ticks; i++) world.Tick();
            double quietTick = sw.Elapsed.TotalMilliseconds / Ticks;
            sw.Restart();
            for (int i = 0; i < Ticks; i++)
            {
                enclosure.MarkDirty((int)(Next(ref s) % (uint)cellCount));
                world.Tick();
            }
            double busyTick = sw.Elapsed.TotalMilliseconds / Ticks;

            string line = $"[enclosure] {name} ({size.SizeX}x{size.SizeZ}x{size.SizeY}): initial solve {initialMs:F1} ms; " +
                          $"one edit: mean {total / Edits:F3} ms, worst {worst:F3} ms; " +
                          $"whole tick quiet {quietTick:F3} ms, with an edit a tick {busyTick:F3} ms";
            TestContext.WriteLine(line);
            Console.WriteLine(line);
        }

        static uint Next(ref uint s)
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return s;
        }
    }
}
