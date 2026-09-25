#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The two pieces of cover as buildings (design 50 §4): what they are made of, what crossing
    /// one costs and that the path search and the mover agree on it, that neither blocks a shot or
    /// a walk, what they are worth as cover, and what one leaves when a fight destroys it.
    /// </summary>
    public class CoverBuildingTests
    {
        static int Offset(int cell, int dx, int dz = 0)
        {
            CellRef c = Size.FromIndex(cell);
            return Size.Index(c.X + dx, c.Z + dz, c.Y);
        }

        static void Raise(ColonyWorld colony, int cell, int building, int stuff)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), building, stuff, 0), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
        }

        static int Count(ColonyWorld colony, int def)
        {
            int n = 0;
            foreach (var item in colony.Pawns.Items.Items)
                if (item.DefIndex == def && item.Cell >= 0) n += item.Stack;
            return n;
        }

        static PlacedEdifice At(ColonyWorld colony, int cell) =>
            colony.Construction.Edifices.Records[colony.Pawns.Cells.Edifice[cell]];

        [Test]
        public void SandbagsAreStoneWhateverTheOrderNamed()
        {
            var colony = Board();
            int cell = Near(colony, -15, 12);
            Raise(colony, cell, BuildingHandle.Sandbags, StuffHandle.Wood);
            Assert.That(At(colony, cell).Def, Is.EqualTo(CoreContent.EdificeSandbags));
            Assert.That(At(colony, cell).Stuff, Is.EqualTo(ConstructionContent.StuffAt(StuffHandle.Stone).stuff));
        }

        [TestCase(StuffHandle.Wood)]
        [TestCase(StuffHandle.Stone)]
        public void ABarricadeIsWhatTheOrderNamedAndItsHitPointsFollow(int stuff)
        {
            var colony = Board();
            int cell = Near(colony, -15, 12);
            Raise(colony, cell, BuildingHandle.Barricade, stuff);
            Assert.That(At(colony, cell).Stuff, Is.EqualTo(ConstructionContent.StuffAt(stuff).stuff));
            Assert.That(BuildingTargets.TryFind(colony.Pawns, cell, out BuildingTarget target), Is.True);
            Assert.That(target.MaxMilli, Is.EqualTo(300 * ConstructionContent.StuffAt(stuff).hitPointsFactorPerMille));
        }

        [Test]
        public void NeitherBlocksAWalkOrAShotAndBothAreLowCover()
        {
            var colony = Board();
            int sandbags = Near(colony, -15, 12);
            int barricade = Offset(sandbags, 0, 2);
            Raise(colony, sandbags, BuildingHandle.Sandbags, StuffHandle.Stone);
            Raise(colony, barricade, BuildingHandle.Barricade, StuffHandle.Wood);
            foreach (int cell in new[] { sandbags, barricade })
            {
                Assert.That(colony.Pawns.Cells.IsWalkable(cell), Is.True);
                Assert.That(LineOfSight.Blocks(colony.Pawns.Cells, colony.Pawns.Nav.Grid, cell), Is.False);
                Assert.That(Cover.BaseAt(colony.Pawns, cell, out bool tall), Is.EqualTo(550));
                Assert.That(tall, Is.False);
                Assert.That((colony.Pawns.Nav.Grid.Flags[cell] & NavFlags.PassThrough) != 0, Is.True);
            }
        }

        /// <summary>
        /// Crossing costs +150 for sandbags and +250 for a barricade, the same to the mover and to
        /// the region graph, straight or diagonal — the two must agree or a route is priced at one
        /// cost and walked at another (<c>HopPriceHasOneOwnerTests</c>'s fault).
        /// </summary>
        [Test]
        public void CrossingCostsTheSameToTheMoverAndTheGraph()
        {
            var colony = Board();
            int sandbags = Near(colony, -15, 12);
            int barricade = Offset(sandbags, 0, 2);
            int open = Offset(sandbags, 0, 4);
            Raise(colony, sandbags, BuildingHandle.Sandbags, StuffHandle.Stone);
            Raise(colony, barricade, BuildingHandle.Barricade, StuffHandle.Wood);
            NavGraph nav = colony.Pawns.Nav;
            foreach (bool diagonal in new[] { false, true })
            {
                int step = nav.Grid.EnterCost(open, TraverseMode.Colonist, diagonal);
                Assert.That(nav.Grid.EnterCost(sandbags, TraverseMode.Colonist, diagonal) - step,
                    Is.EqualTo(diagonal ? (150 * MoveCost.Diagonal + 50) / MoveCost.Orthogonal : 150));
                Assert.That(nav.Grid.EnterCost(barricade, TraverseMode.Colonist, diagonal) - step,
                    Is.EqualTo(diagonal ? (250 * MoveCost.Diagonal + 50) / MoveCost.Orthogonal : 250));
                foreach (int cell in new[] { sandbags, barricade, open })
                    Assert.That(nav.StepCost(cell, diagonal), Is.EqualTo(nav.Grid.EnterCost(cell, TraverseMode.Colonist, diagonal)));
            }
        }

        [Test]
        public void TakingItDownTakesTheCrossingWithIt()
        {
            var colony = Board();
            int cell = Near(colony, -15, 12);
            Raise(colony, cell, BuildingHandle.Sandbags, StuffHandle.Stone);
            Assert.That(colony.Construction.Demolish(colony.Pawns, cell, out _), Is.True);
            Assert.That((colony.Pawns.Nav.Grid.Flags[cell] & NavFlags.PassThrough) != 0, Is.False);
            Assert.That(colony.Pawns.Nav.Grid.PassCostAt(cell), Is.EqualTo(0));
        }

        /// <summary>
        /// Sandbags beaten to nothing leave a quarter of their five stone — one, or two on the
        /// draw — and a shelf beaten to nothing leaves nothing, as every building before cover does.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void DestroyedCoverLeavesAQuarterAndAShelfNothing(bool sandbags)
        {
            var colony = Board();
            colony.World.Tick();
            int cell = Near(colony, -15, 12);
            Raise(colony, cell, sandbags ? BuildingHandle.Sandbags : BuildingHandle.Shelf, sandbags ? StuffHandle.Stone : StuffHandle.Wood);
            Assert.That(BuildingTargets.TryFind(colony.Pawns, cell, out BuildingTarget target), Is.True);
            int stoneBefore = Count(colony, ItemIndex.Stone);
            int woodBefore = Count(colony, ItemIndex.Wood);

            Pawn attacker = colony.Pawns.Pawns.All[0];
            colony.Pawns.Combat!.StrikeBuilding(attacker, target, cell, Fists(colony.Pawns),
                new SwingOutcome(CombatEventKind.Hit, target.MaxMilli + 1), colony.World.CurrentTick);
            colony.World.Tick();

            Assert.That(colony.Pawns.Cells.Edifice[cell], Is.EqualTo(-1), "it came down");
            int stone = Count(colony, ItemIndex.Stone) - stoneBefore;
            int wood = Count(colony, ItemIndex.Wood) - woodBefore;
            if (sandbags) Assert.That(stone, Is.InRange(1, 2));
            else Assert.That(stone + wood, Is.EqualTo(0));
        }
    }
}
