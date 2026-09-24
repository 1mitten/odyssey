#nullable enable
using System;
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What the thermal pass costs, isolated from the tick it hides in.
    ///
    /// <para><b>Why it is not enough to read the class.</b> <c>TemperatureSystem</c>'s own summary
    /// claims the pass is <i>O(rooms + surfaces), never O(cells)</i>. The room half is, and the
    /// heat-source half is not: it walks every standing edifice on the board. A wooded board is
    /// mostly trees, so the claim and the code can only be separated by a measurement with the
    /// edifice count printed beside the time.</para>
    ///
    /// <para><c>[Explicit]</c>, like every other probe here: it measures a machine as much as a
    /// program and must never decide whether a pull request lands.</para>
    /// </summary>
    public class TemperatureCostProbe
    {
        [Test, Explicit, Category("Benchmark")]
        public void TheThermalPassOnEveryOfferedBoard()
        {
            Measure("standard, wooded", BoardSizes.Standard, wooded: true);
            Measure("huge, wooded", BoardSizes.Huge, wooded: true);
            Measure("scale target, barren", GridSize.ScaleTarget, wooded: false);
        }

        static void Measure(string name, GridSize size, bool wooded)
        {
            ColonyWorld colony = ColonyWorld.Build(size, 12345u, ScenarioDef.Bare(),
                mapType: MapType.Natural, wooded: wooded);
            SimWorld world = colony.World;
            var temperature = colony.Pawns.Temperature!;

            int edifices = 0;
            var placed = colony.Construction.Edifices.Records;
            for (int i = 0; i < placed.Count; i++) if (!placed[i].Removed) edifices++;

            int rooms = 0;
            for (int y = 0; y < size.SizeY; y++) rooms += colony.Pawns.Enclosure!.RoomsOn(y).Count;

            // Warm the pass, then time it alone. The system only does work on a multiple of
            // IntervalTicks, so ticking it off-beat measures the early return and ticking it
            // on-beat measures the pass; the difference is what the pass costs.
            for (int i = 0; i < 4; i++) temperature.Tick(world);

            const int Passes = 200;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Passes; i++) temperature.Tick(world);
            double passMs = sw.Elapsed.TotalMilliseconds / Passes;

            string line = $"[thermal] {name} ({size.SizeX}x{size.SizeZ}x{size.SizeY}): " +
                          $"{rooms} rooms, {edifices} standing edifices; " +
                          $"one pass {passMs:F4} ms, so {passMs / TemperatureSystemIntervalTicks():F5} ms a tick amortised";
            TestContext.WriteLine(line);
            Console.WriteLine(line);
        }

        static double TemperatureSystemIntervalTicks() =>
            Odyssey.Sim.Temperature.TemperatureSystem.IntervalTicks;
    }
}
