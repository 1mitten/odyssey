#nullable enable
using System;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What a field costs the <b>tick</b>, which nothing measured until 2026-09-20.
    ///
    /// <para><c>TickBenchmarkTests</c> measures a structured world with fifty pawns and no zone
    /// in it, and <c>FrameTimeTests</c> measures the frame. Growing added three things to the
    /// tick — a growth pass over the planted cells, two work givers that scan the zone's cell
    /// list, and a snapshot channel that republishes every zoned cell every tick — and the
    /// storage work about to land multiplies all three by painting zones across a whole base.
    /// The same board with and without a field is the only honest way to price them, so that is
    /// what this runs.</para>
    ///
    /// <para><b>Explicit, and a benchmark rather than a gate</b>, for the reason
    /// <c>TickBenchmarkTests</c> gives: it measures a machine as much as a program and must
    /// never decide whether a pull request lands.</para>
    /// </summary>
    public class FieldTickBenchmarkTests
    {
        static readonly GridSize Size = new GridSize(120, 120, 16);
        const int WarmUp = 400;
        const int Ticks = 4_000;

        [Test, Explicit, Category("Benchmark")]
        public void AFieldAgainstNoField()
        {
            string bare = Measure("no field", side: 0);
            string small = Measure("an 8 x 8 plot", side: 8);
            string large = Measure("a 45 x 45 field", side: 45);
            // Same field, one colonist instead of six. If the cost is the work-giver scan it
            // falls with the thinking; if it is the per-tick snapshot republish of the zone
            // channel it does not move, because that happens once a tick whoever is alive.
            string lone = Measure("a 45 x 45 field, ONE colonist", side: 45, colonists: 1);
            string idle = Measure("a 45 x 45 field, NO colonists", side: 45, colonists: 0);
            TestContext.WriteLine(bare + small + large + lone + idle);
        }

        static string Measure(string label, int side, int colonists = 6)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            ColonyWorld colony = ColonyWorld.Build(Size, 1u, scenario, wooded: false);

            int painted = 0;
            if (side > 0)
            {
                CellRef start = colony.Start;
                for (int dz = 0; dz < side; dz++)
                for (int dx = 0; dx < side; dx++)
                {
                    var cell = new CellRef(start.X + dx - side / 2, start.Z + dz - side / 2, start.Y);
                    if (!colony.Grid.Contains(cell.X, cell.Z, cell.Y)) continue;
                    colony.World.Intents.Submit(
                        new Intent(IntentKind.DesignateZone, cell, PlantHandle.Carrot + 1));
                }
                colony.World.Tick();
                painted = colony.Growing!.Cells.Count;
            }

            // Long enough that the field is sown and part-way grown: a fallow zone exercises the
            // sowing scan and an empty growth pass, which is the cheap half of the question.
            colony.World.Tick(WarmUp);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long heapBefore = GC.GetTotalMemory(false);
            int gc0 = GC.CollectionCount(0);

            var wall = Stopwatch.StartNew();
            colony.World.Tick(Ticks);
            wall.Stop();

            long heapAfter = GC.GetTotalMemory(false);
            int collections = GC.CollectionCount(0) - gc0;

            double perTick = wall.Elapsed.TotalMilliseconds / Ticks;
            double bytesPerTick = (heapAfter - heapBefore) / (double)Ticks;

            var report = new StringBuilder();
            report.AppendLine($"--- {label}: {painted} zone cells, " +
                              $"{colony.Growing?.Planted.Count ?? 0} planted, {colony.Pawns.Pawns.All.Count} pawns ---");
            report.AppendLine($"  {perTick:F4} ms per tick over {Ticks} ticks");
            report.AppendLine($"  heap growth {bytesPerTick:F0} bytes per tick, {collections} gen-0 collections");
            report.AppendLine($"  snapshot: {colony.World.Views.Current.ZoneCount} zone rows, " +
                              $"{colony.World.Views.Current.PlantCount} plant rows a publish");
            return report.ToString();
        }
    }
}
