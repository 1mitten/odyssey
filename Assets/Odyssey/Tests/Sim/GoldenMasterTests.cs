#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The golden-master gate (OQ-05): a world built and run now must come to the number it came
    /// to when somebody last looked.
    ///
    /// <para><b>Why this exists even though the simulation is already tested for determinism.</b>
    /// Every other hash test here compares a run against another run of the same build. They
    /// prove repeatability and are blind to change: a build that broke felling this morning still
    /// agrees with itself perfectly. The committed table is the only thing in the suite that
    /// compares against the past, and it is therefore the only thing that notices a change nobody
    /// meant to make. The project has already been bitten once by exactly this shape — the world
    /// content had no pin at all until 2026-09-17, and a one-integer edit to <c>Terrain.xml</c>
    /// went unnoticed by 448 passing tests.</para>
    ///
    /// <para><b>It runs in both tiers, which is the cross-runtime half.</b> The fast tier is
    /// CoreCLR and the Unity tier is Mono. One committed number that satisfies both is a stronger
    /// claim than either alone, and it is the standing check on the thing
    /// <c>docs/lessons.md</c> calls out: floating point and unordered iteration diverge between
    /// runtimes long before they diverge between runs.</para>
    ///
    /// <para><b>To re-bake:</b> <c>ODYSSEY_REGOLDEN=1 scripts/test-fast.sh</c> (add
    /// <c>--filter TestCategory=Long</c> for the other two), paste the printed values into
    /// <see cref="Golden"/>, and say in the commit message what moved them.</para>
    /// </summary>
    public class GoldenMasterTests
    {
        static bool Rebaking => Environment.GetEnvironmentVariable("ODYSSEY_REGOLDEN") == "1";

        /// <summary>
        /// The world's state hash.
        ///
        /// <para>This used to fold the cell grid in by hand, because the grid was not in
        /// <c>ComputeStateHash</c> and a golden that could not see the world would have been
        /// worse than none — it was this table's own control that found that gap. `OQ-50` put the
        /// grid where it belongs, so the composite is gone and the golden pins the same number
        /// everything else in the project compares.</para>
        /// </summary>
        static ulong FullHash(ColonyWorld colony) => colony.World.ComputeStateHash().Value;

        /// <summary>
        /// Build, hash, tick, hash again, and either assert or print.
        ///
        /// <para>Both hashes are checked in one test rather than two, because they are one
        /// measurement of one run and splitting them would double a ten-thousand-tick workload to
        /// assert half of it each time.</para>
        /// </summary>
        static void Check(Golden.Case golden)
        {
            ColonyWorld colony = golden.Build();
            ulong generated = FullHash(colony);

            colony.World.Tick(golden.Ticks);
            ulong simulated = FullHash(colony);

            // The Ticks column means ticks. Cheap, and it pins the one assumption the whole table
            // rests on: that the run the hash describes is the run the row says it is.
            Assert.That(colony.World.CurrentTick, Is.EqualTo(golden.Ticks),
                $"{golden.Name}: asked for {golden.Ticks:N0} ticks and the world ran " +
                $"{colony.World.CurrentTick:N0}");

            if (Rebaking)
            {
                Console.WriteLine(
                    $"REGOLDEN {golden.Name}{Environment.NewLine}" +
                    $"            Generated = {generated}UL,{Environment.NewLine}" +
                    $"            Simulated = {simulated}UL,");
                Assert.Pass("ODYSSEY_REGOLDEN=1: printed replacement values instead of asserting.");
            }

            // Worldgen first. When both have moved this is the one that explains the other, and
            // asserting it first means the failure names the cause rather than the consequence.
            Assert.That(generated, Is.EqualTo(golden.Generated),
                $"{golden.Name}: the world differs before a single tick ran. This covers the generated " +
                "board AND the colony placed on it, so check the grid hash alone before concluding " +
                "the generator changed. Re-bake with ODYSSEY_REGOLDEN=1 if that was deliberate.");

            Assert.That(simulated, Is.EqualTo(golden.Simulated),
                $"{golden.Name}: the board generated identically and the colony then ran to a " +
                $"different state over {golden.Ticks:N0} ticks, so a simulation system changed. " +
                "Re-bake with ODYSSEY_REGOLDEN=1 if that was deliberate.");
        }

        [Test]
        public void TheMeadowComesToTheCommittedHash() => Check(Golden.Meadow);

        [Test, Category("Long")]
        public void ThePlayedBoardComesToTheCommittedHash() => Check(Golden.PlayedBoard);

        [Test, Category("Long")]
        public void TheRuinedCityComesToTheCommittedHash() => Check(Golden.City);

        /// <summary>
        /// The control, and the one a golden most needs: a table of zeroes, or a hash that no
        /// longer depends on the world, would let every test above pass for ever. Two worlds that
        /// differ only by seed must not share a hash — at generation or after ten thousand ticks.
        /// </summary>
        [Test]
        public void TheHashActuallyDependsOnTheWorld()
        {
            ColonyWorld first = Golden.Meadow.Build();
            ColonyWorld second = ColonyWorld.Build(
                Golden.Meadow.Size, Golden.Meadow.Seed + 1u, ScenarioDef.Bare(),
                mapType: Golden.Meadow.Map, wooded: Golden.Meadow.Wooded);

            ulong firstGenerated = FullHash(first);
            ulong secondGenerated = FullHash(second);
            Assert.That(firstGenerated, Is.Not.EqualTo(secondGenerated), "two seeds generated the same board");
            Assert.That(firstGenerated, Is.Not.Zero, "a hash of zero would make the table meaningless");

            first.World.Tick(200);
            second.World.Tick(200);
            Assert.That(FullHash(first), Is.Not.EqualTo(FullHash(second)));
        }

        /// <summary>
        /// The committed number is of the run the table describes, not of some other run. Ticking
        /// a different number of ticks must not land on the same hash, or the <c>Ticks</c> column
        /// would be decoration and a change to tick counts would pass unnoticed.
        /// </summary>
        [Test]
        public void TheHashDependsOnHowLongItRan()
        {
            ColonyWorld colony = Golden.Meadow.Build();
            colony.World.Tick(Golden.Meadow.Ticks - 500);
            ulong early = FullHash(colony);

            colony.World.Tick(500);
            Assert.That(FullHash(colony), Is.Not.EqualTo(early));
        }
    }
}
