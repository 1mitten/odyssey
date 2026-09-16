#nullable enable
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The player's standing orders: what a cell may be told to become, and how an order that
    /// does not make sense is refused with a reason rather than dropped.
    /// </summary>
    public class DesignationTests
    {
        const int Layer = 1;

        /// <summary>A small flat board: rock below, air above, a tree at (3,3) and a wall at (5,3).</summary>
        static (CellGrid grid, List<PlacedEdifice> edifices, DesignationGrid designations) Board()
        {
            var size = new GridSize(8, 8, 3);
            var grid = new CellGrid(size);
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
            {
                int ground = size.Index(x, z, 0);
                grid.Terrain[ground] = CoreContent.TerrainRock;
                grid.Flags[ground] |= CellFlags.SolidTerrain;
            }

            var edifices = new List<PlacedEdifice>();
            int tree = size.Index(3, 3, Layer);
            edifices.Add(new PlacedEdifice { CellIndex = tree, Def = NaturalContent.EdificeTreeConifer, Stuff = NaturalContent.StuffWood });
            grid.Edifice[tree] = 0;

            int wall = size.Index(5, 3, Layer);
            edifices.Add(new PlacedEdifice { CellIndex = wall, Def = CoreContent.EdificeWall, Stuff = CoreContent.StuffConcrete });
            grid.Edifice[wall] = 1;
            grid.Flags[wall] |= CellFlags.BlockingEdifice;

            return (grid, edifices, new DesignationGrid(grid, edifices));
        }

        [Test]
        public void FellOnlyOnATree()
        {
            var (_, _, d) = Board();
            Assert.That(d.Designate(new CellRef(3, 3, Layer), DesignationKind.Fell), Is.EqualTo(IntentRejection.None));
            Assert.That(d.Designate(new CellRef(5, 3, Layer), DesignationKind.Fell), Is.EqualTo(IntentRejection.NotPermitted), "a wall is not a tree");
            Assert.That(d.Designate(new CellRef(1, 1, Layer), DesignationKind.Fell), Is.EqualTo(IntentRejection.NotPermitted), "open ground is not a tree");
            Assert.That(d.Designate(new CellRef(1, 1, 0), DesignationKind.Fell), Is.EqualTo(IntentRejection.NotPermitted), "rock is not a tree");
        }

        [Test]
        public void MineOnlyOnSolidTerrainAndDeconstructOnlyOnAConstruction()
        {
            var (_, _, d) = Board();
            Assert.That(d.Designate(new CellRef(1, 1, 0), DesignationKind.Mine), Is.EqualTo(IntentRejection.None));
            Assert.That(d.Designate(new CellRef(1, 1, Layer), DesignationKind.Mine), Is.EqualTo(IntentRejection.NotPermitted), "air cannot be mined");
            Assert.That(d.Designate(new CellRef(5, 3, Layer), DesignationKind.Deconstruct), Is.EqualTo(IntentRejection.None));
            Assert.That(d.Designate(new CellRef(3, 3, Layer), DesignationKind.Deconstruct), Is.EqualTo(IntentRejection.NotPermitted), "a tree is felled, not deconstructed");
        }

        [Test]
        public void BedrockRefusesTheOrderRatherThanQuotingAPrice()
        {
            // Bedrock is the floor of the world. It could be allowed at its 2,400 ticks — forty
            // trees' work for one cell of nothing — but an order a colonist would spend a day on
            // and get no material from is worse than no order, because the colonist takes the day.
            var (grid, edifices, d) = Board();
            int cell = grid.Size.Index(2, 2, 0);
            grid.Terrain[cell] = NaturalContent.TerrainBedrock;

            Assert.That(d.Designate(new CellRef(2, 2, 0), DesignationKind.Mine),
                Is.EqualTo(IntentRejection.NotPermitted), "bedrock accepted a mining order");
            Assert.That(d.CanMine(cell), Is.False);
            Assert.That(d.CanMine(grid.Size.Index(1, 1, 0)), Is.True, "ordinary rock stopped being minable");
        }

        [Test]
        public void TheGroundUnderAStandingTreeIsNotMinable()
        {
            // Digging it away would leave the tree rooted in mid-air. Felling it first is the
            // answer, and the order becomes available the moment the tree is gone.
            var (grid, edifices, d) = Board();
            int under = grid.Size.Index(3, 3, 0);

            Assert.That(d.Designate(new CellRef(3, 3, 0), DesignationKind.Mine),
                Is.EqualTo(IntentRejection.NotPermitted), "the ground under a tree accepted a mining order");

            grid.RemoveEdifice(grid.Size.Index(3, 3, Layer));
            Assert.That(d.CanMine(under), Is.True, "felling the tree did not free the ground under it");
        }

        [Test]
        public void OutOfTheMapAndRepeatsAreRefusedWithTheirReasons()
        {
            var (_, _, d) = Board();
            Assert.That(d.Designate(new CellRef(9, 3, Layer), DesignationKind.Fell), Is.EqualTo(IntentRejection.OutOfBounds));
            Assert.That(d.Designate(new CellRef(3, 3, Layer), DesignationKind.None), Is.EqualTo(IntentRejection.NotPermitted));
            d.Designate(new CellRef(3, 3, Layer), DesignationKind.Fell);
            Assert.That(d.Designate(new CellRef(3, 3, Layer), DesignationKind.Fell), Is.EqualTo(IntentRejection.AlreadyInThatState));
            Assert.That(d.Cancel(new CellRef(1, 1, Layer)), Is.EqualTo(IntentRejection.AlreadyInThatState), "nothing to cancel");
            Assert.That(d.Cancel(new CellRef(3, 3, Layer)), Is.EqualTo(IntentRejection.None));
            Assert.That(d.Count, Is.Zero);
        }

        [Test]
        public void TheCellListStaysSortedAndTheHashFollowsIt()
        {
            var (grid, _, d) = Board();
            d.Designate(new CellRef(6, 6, 0), DesignationKind.Mine);
            d.Designate(new CellRef(1, 1, 0), DesignationKind.Mine);
            d.Designate(new CellRef(3, 3, Layer), DesignationKind.Fell);
            Assert.That(d.Cells, Is.Ordered);
            Assert.That(d.Cells, Has.Count.EqualTo(3));

            var before = StateHash.New();
            d.ContributeTo(ref before);
            d.Cancel(new CellRef(1, 1, 0));
            var after = StateHash.New();
            d.ContributeTo(ref after);
            Assert.That(after.Value, Is.Not.EqualTo(before.Value), "an order changing must change the hash");
        }

        [Test]
        public void OrdersSurviveASave()
        {
            var (grid, edifices, d) = Board();
            d.Designate(new CellRef(3, 3, Layer), DesignationKind.Fell);
            d.Designate(new CellRef(2, 2, 0), DesignationKind.Mine);
            SimWorld world = new SimWorldBuilder().WithSize(grid.Size).WithSeed(5u).Build();

            var stream = new MemoryStream();
            WorldSave.Save(world, stream, new ISaveable[] { d });
            stream.Position = 0;

            var restored = new DesignationGrid(grid, edifices);
            SimWorld into = new SimWorldBuilder().WithSize(grid.Size).WithSeed(5u).Build();
            WorldSave.Load(into, stream, new ISaveable[] { restored });

            Assert.That(restored.At(grid.Index(3, 3, Layer)), Is.EqualTo(DesignationKind.Fell));
            Assert.That(restored.At(grid.Index(2, 2, 0)), Is.EqualTo(DesignationKind.Mine));
            Assert.That(restored.Cells, Is.EqualTo(d.Cells));
        }

        [Test]
        public void IntentsPlaceAndCancelOrdersAndTheSnapshotShowsThem()
        {
            var (grid, _, d) = Board();
            SimWorld world = d.Attach(new SimWorldBuilder().WithSize(grid.Size)).Build();
            Assert.That(world.HandlesIntent(IntentKind.Designate), Is.True);
            Assert.That(world.HandlesIntent(IntentKind.CancelDesignation), Is.True);

            world.Intents.Submit(new Intent(IntentKind.SetSliceLayer, a: Layer));
            world.Intents.Submit(new Intent(IntentKind.Designate, new CellRef(3, 3, Layer), (int)DesignationKind.Fell));
            world.Intents.Submit(new Intent(IntentKind.Designate, new CellRef(5, 3, Layer), (int)DesignationKind.Fell));
            world.Tick();

            Assert.That(world.Intents.Rejected, Has.Count.EqualTo(1));
            Assert.That(world.Intents.Rejected[0].Reason, Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(d.At(grid.Index(3, 3, Layer)), Is.EqualTo(DesignationKind.Fell));

            var snapshot = world.Views.Current;
            Assert.That(snapshot.Designations.Length, Is.EqualTo(grid.Size.LayerStride));
            Assert.That(snapshot.Designations[grid.Size.Index(3, 3, 0)], Is.EqualTo((byte)DesignationKind.Fell),
                "the slice channel is indexed within the layer");

            world.Intents.Submit(new Intent(IntentKind.CancelDesignation, new CellRef(3, 3, Layer)));
            world.Tick();
            Assert.That(d.Count, Is.Zero);
            Assert.That(world.Views.Current.Designations[grid.Size.Index(3, 3, 0)], Is.Zero);
        }

        [Test]
        public void AKindOfIntentCanBeClaimedOnlyOnce()
        {
            var (grid, _, d) = Board();
            var builder = d.Attach(new SimWorldBuilder().WithSize(grid.Size))
                .AddIntentHandler(IntentKind.Designate, _ => IntentRejection.None);
            Assert.Throws<System.InvalidOperationException>(() => builder.Build());
        }
    }
}
