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
        public void PlaytestMarksTheTreesNearTheStartAndNothingElse()
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
            Assert.That(colony.Designations.Count, Is.EqualTo(marked), "orders exist off the start layer");
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
