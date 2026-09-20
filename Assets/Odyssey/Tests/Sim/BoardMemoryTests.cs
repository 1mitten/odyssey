#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What a board costs to hold, per cell and in total.
    ///
    /// <para><b>The per-cell figure is the one that transfers.</b> A total says what one board
    /// costs on one day; bytes per cell says what the next board will cost before anybody builds
    /// it, which is the only way to answer "how big could we go" without going there. The arrays
    /// are allocated in one burst when the world is built, straight into gen2 and the large object
    /// heap, so the figure is a step at load rather than a pressure during play.</para>
    ///
    /// <para><b>This is the simulation's half only.</b> Nine owners keep an array per cell and
    /// eight of them are here — the cell grid, construction, growing zones, designations, the
    /// navigation graph and grid, the support solver and the enclosure grid. The ninth is
    /// <c>WorldRenderModel</c>, a second full mirror of the grid on the presentation side, which
    /// the fast tier cannot compile and which <c>docs/design/28-map-size.md</c> carries as
    /// arithmetic beside these measurements.</para>
    ///
    /// <para>It prints and does not budget, for the reason <c>TickBenchmarkTests</c> gives: a
    /// threshold that trips on a busy machine teaches people to ignore red. What it does assert is
    /// that the fixture measured something — a collected heap and a real one look identical in a
    /// log line and completely different in a decision.</para>
    /// </summary>
    public class BoardMemoryTests
    {
        static IEnumerable<GridSize> OfferedBoards()
        {
            yield return BoardSizes.Small;
            yield return BoardSizes.Standard;
            yield return BoardSizes.Large;
            yield return BoardSizes.Huge;
            yield return GridSize.ScaleTarget;
        }

        [Test, Category("Long")]
        public void EveryOfferedBoardSaysWhatItCostsToHold(
            [ValueSource(nameof(OfferedBoards))] GridSize size)
        {
            // Settle first, twice, or the "before" reading carries whatever the previous test left
            // behind and the delta is the difference between two unrelated heaps.
            Settle();
            long before = GC.GetTotalMemory(true);

            ColonyWorld colony = ColonyWorld.Build(size, seed: 4242u, ScenarioDef.Bare());

            Settle();
            long after = GC.GetTotalMemory(true);
            long delta = after - before;

            TestContext.WriteLine(
                $"[Memory] {size}: {size.CellCount:N0} cells, heap {before:N0} -> {after:N0}, " +
                $"world {delta / (1024.0 * 1024.0):F1} MiB = {(double)delta / size.CellCount:F1} bytes/cell " +
                "(simulation only; WorldRenderModel is a second mirror on the presentation side)");

            // The fixture control. GetTotalMemory(true) collects, so a world that was somehow
            // never rooted would read as free and the arm would report a triumph.
            Assert.That(colony, Is.Not.Null);
            Assert.That(delta, Is.GreaterThan(size.CellCount),
                "a world of this many cells cannot cost under a byte each; the measurement " +
                "collected the thing it was measuring");
        }

        static void Settle()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
