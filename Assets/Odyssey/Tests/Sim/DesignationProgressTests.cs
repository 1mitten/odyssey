#nullable enable
using System;
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The snapshot channel that says how far through its order each cell is — the pale slab
    /// eating down a rock face — and what it must not cost.
    ///
    /// <para>It shipped with the mining line and had no test, and the two things below are the
    /// two halves of the same bug: published correctly but by walking every cell of the active
    /// layer, which cost <b>0.055 ms a tick on a board with no orders on it</b>. That is
    /// twenty-eight times the rest of the simulation put together, and it took the ten-day soak
    /// from a second a seed to thirty-nine.</para>
    /// </summary>
    public class DesignationProgressTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 8);

        /// <summary>A natural map, because it has rock in it and rock is what can be mined.</summary>
        static ColonyWorld Build() =>
            ColonyWorld.Build(Size, seed: 1u, ScenarioDef.Bare(), barren: false);

        /// <summary>A minable cell, with the layer it is on made the active one.</summary>
        static int FindMinable(ColonyWorld colony)
        {
            for (int index = 0; index < Size.CellCount; index++)
            {
                if (!colony.Designations.Allows(index, DesignationKind.Mine)) continue;
                colony.World.Intents.Submit(new Intent(IntentKind.SetSliceLayer, a: Size.FromIndex(index).Y));
                colony.World.Tick();
                return index;
            }

            Assert.Fail("the generated map has nothing that can be mined");
            return -1;
        }

        [Test]
        public void AHalfCutFaceIsPublishedAsHalfCut()
        {
            ColonyWorld colony = Build();
            int cell = FindMinable(colony);

            colony.Designations.Designate(Size.FromIndex(cell), DesignationKind.Mine);
            colony.Designations.AddWork(cell, colony.Designations.WorkFor(cell) / 2);
            colony.World.Tick();

            ReadOnlySpan<byte> progress = colony.World.Views.Current.DesignationProgress;
            int offset = cell - Size.FromIndex(cell).Y * Size.LayerStride;

            // Half of 255, give or take the quantisation and the integer halving of odd work.
            Assert.That(progress[offset], Is.InRange(120, 135),
                "a face cut half way through is not published as half cut");
        }

        /// <summary>
        /// The control for the sparse walk, and the reason it needs the clear: the snapshot is
        /// double-buffered, so a cell written in one frame still holds its byte two frames later
        /// unless the buffer is cleared. Without the clear this test reads the old value and the
        /// rock face stays half cut on screen after the order is gone.
        /// </summary>
        [Test]
        public void ACancelledOrderStopsBeingPublished()
        {
            ColonyWorld colony = Build();
            int cell = FindMinable(colony);
            int offset = cell - Size.FromIndex(cell).Y * Size.LayerStride;

            colony.Designations.Designate(Size.FromIndex(cell), DesignationKind.Mine);
            colony.Designations.AddWork(cell, colony.Designations.WorkFor(cell) / 2);
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.DesignationProgress[offset], Is.GreaterThan(0),
                "nothing was published, so the test below would pass for the wrong reason");

            colony.Designations.Cancel(Size.FromIndex(cell));

            // Twice, because the buffer that must not hold a stale byte is the one two frames back.
            colony.World.Tick();
            colony.World.Tick();

            Assert.That(colony.World.Views.Current.DesignationProgress[offset], Is.Zero,
                "a cancelled order is still drawn as a half-cut face");
        }

        /// <summary>
        /// <b>The regression guard.</b> Publishing this channel must cost what the orders cost,
        /// not what the layer costs: a layer is 3,600 cells here and 14,400 on the played board,
        /// while a colony has tens of orders.
        ///
        /// <para><b>It runs on the played board, 120 x 120, and that is not incidental.</b> The
        /// first draft of this test used the 60 x 60 board the rest of the file uses, and it
        /// <i>passed against the unfixed code</i>: the cost is proportional to the layer, so a
        /// quarter of the cells is a quarter of the bug, 0.016 ms, which sat inside the
        /// threshold. A performance guard has to run at the size the thing is used at.</para>
        ///
        /// <para>A timing assertion is a blunt instrument and the threshold is loose — five times
        /// the measured figure — because the machine, the runtime and the build configuration all
        /// move it. It is worth having anyway: it was seen to fail against the unfixed code at
        /// this size, which is the only reason to believe it.</para>
        ///
        /// <para>Measured when written, on the 120 x 120 x 16 board with the whole simulation
        /// running: <b>0.0022 ms a tick fixed, 0.057 ms unfixed.</b></para>
        /// </summary>
        [Test, Category("Long")]
        public void PublishingCostsWhatTheOrdersCostRatherThanWhatTheLayerCosts()
        {
            // The board the game is played on, not the small one above: see the remarks.
            ColonyWorld colony = ColonyWorld.Build(
                new GridSize(120, 120, 16), seed: 1u, ScenarioDef.Bare(), barren: false);
            colony.World.Tick(100);

            const int Ticks = 20_000;
            var clock = Stopwatch.StartNew();
            colony.World.Tick(Ticks);
            clock.Stop();

            double msPerTick = clock.Elapsed.TotalMilliseconds / Ticks;
            Console.WriteLine($"[progress] {Ticks:N0} ticks on 120x120x16 with no orders: " +
                              $"{clock.Elapsed.TotalMilliseconds:F0} ms, {msPerTick:F4} ms/tick");

            Assert.That(msPerTick, Is.LessThan(0.012),
                $"a tick on an empty board costs {msPerTick:F4} ms, which is the whole-layer " +
                "publish loop back again (it cost 0.057 ms a tick, against 0.002 without it)");
        }
    }
}
