#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The two built-in scenarios differ in exactly one thing: whether the colony has orders
    /// before the first tick. The scene relies on Playtest giving it work, and every headless run
    /// relies on Bare giving it none, so both halves are pinned.
    /// </summary>
    public class ScenarioDefTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 8);

        static ColonyWorld Wooded(ScenarioDef scenario) =>
            ColonyWorld.Build(Size, seed: 1u, scenario, barren: true, wooded: true);

        [Test]
        public void PlaytestMarksEveryTreeNearTheStart()
        {
            ScenarioDef scenario = ScenarioDef.Playtest();
            ColonyWorld colony = Wooded(scenario);
            CellRef start = colony.Start;
            int radius = scenario.startingFellRadius;

            int marked = 0, standing = 0;
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
            {
                int index = Size.Index(x, z, start.Y);
                bool near = System.Math.Abs(x - start.X) <= radius && System.Math.Abs(z - start.Z) <= radius;
                bool ordered = colony.Designations.At(index) == DesignationKind.Fell;
                if (ordered) marked++;
                if (ordered && !near) Assert.Fail($"({x}, {z}) is marked but lies outside {radius} cells of {start}");
                if (near && colony.Designations.IsTree(index) && !ordered) standing++;
            }

            Assert.That(marked, Is.GreaterThan(0), "the colony has no felling work");
            Assert.That(standing, Is.Zero, "trees within the radius were left unmarked");
        }

        [Test]
        public void PlaytestAlsoMarksTheNearestOutcropForMining()
        {
            // Added with the mining MVP: the felling half of this used to assert the colony had
            // no other orders at all. It cannot any more, and that is the feature — there is
            // still no tool to give a mining order with, so the scenario gives one.
            ScenarioDef scenario = ScenarioDef.Playtest();
            ColonyWorld colony = Wooded(scenario);

            int mine = 0;
            foreach (int cell in colony.Designations.Cells)
                if (colony.Designations.At(cell) == DesignationKind.Mine) mine++;

            Assume.That(mine, Is.GreaterThan(0),
                $"no outcrop within {scenario.startingMineRadius} cells of {colony.Start} on this seed");

            foreach (int cell in colony.Designations.Cells)
            {
                if (colony.Designations.At(cell) != DesignationKind.Mine) continue;
                Assert.That(colony.Designations.IsMinableStone(cell), Is.True,
                    $"{Size.FromIndex(cell)} is marked for mining and is not minable stone");
                Assert.That(Size.FromIndex(cell).Y, Is.GreaterThanOrEqualTo(colony.Start.Y - 1),
                    "an order was given for rock buried under the subsoil, which nobody can reach");
            }
        }

        [Test]
        public void ThePlayedBoardStartsWithMiningWorkOnEverySeed()
        {
            // The 60 x 60 x 8 board above is the test fixture; this is the board the scene loads.
            // The point of the starting order is that a playtester can see mining happen, and a
            // seed where the nearest outcrop is off in the trees shows nothing at all — so the
            // radius is checked against the board it has to work on, over several seeds rather
            // than the one that happened to be lucky.
            var playSize = new GridSize(120, 120, 16);

            for (uint seed = 1; seed <= 5; seed++)
            {
                ColonyWorld colony = ColonyWorld.Build(playSize, seed, ScenarioDef.Playtest(),
                    barren: true, wooded: true);

                int mine = 0;
                foreach (int cell in colony.Designations.Cells)
                    if (colony.Designations.At(cell) == DesignationKind.Mine) mine++;

                Assert.That(mine, Is.GreaterThan(0), $"seed {seed} starts with no stone to mine");
            }
        }

        [Test]
        public void BareGivesTheSameColonyAndNoOrders()
        {
            ColonyWorld bare = Wooded(ScenarioDef.Bare());
            ColonyWorld playtest = Wooded(ScenarioDef.Playtest());

            Assert.That(bare.Designations.Count, Is.Zero, "a bare scenario gave an order");
            Assert.That(bare.Placement.ToString(), Is.EqualTo(playtest.Placement.ToString()),
                "the two scenarios are meant to differ only in their orders");
        }
    }
}
