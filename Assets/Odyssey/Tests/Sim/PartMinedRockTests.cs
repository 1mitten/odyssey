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
    /// Rock somebody started to mine keeps its cut when the order is taken off (design 57 §3,
    /// owner 2026-09-26: "Keep its state"). Before, a cancel zeroed the ledger and the rock healed —
    /// which nobody could see until the cut was drawn as cracks.
    /// </summary>
    public class PartMinedRockTests
    {
        static readonly CellRef Face = new CellRef(2, 2, 0);

        /// <summary>A small board of rock under air, the same shape <c>DesignationTests</c> uses.</summary>
        static (CellGrid grid, DesignationGrid designations) Board()
        {
            var size = new GridSize(6, 6, 2);
            var grid = new CellGrid(size);
            for (int z = 0; z < 6; z++)
            for (int x = 0; x < 6; x++)
            {
                int ground = size.Index(x, z, 0);
                grid.Terrain[ground] = CoreContent.TerrainRock;
                grid.Flags[ground] |= CellFlags.SolidTerrain;
            }
            return (grid, new DesignationGrid(grid, new List<PlacedEdifice>()));
        }

        static int Whole(CellGrid grid, int cell) =>
            NaturalContent.TerrainAt(grid.Terrain[cell]).workToClear * Rates.Scale;

        [Test]
        public void ACancelledCutIsKeptAndTheNextOrderCarriesOnFromIt()
        {
            var (grid, d) = Board();
            int cell = grid.Index(Face);
            int third = Whole(grid, cell) / 3;

            d.Designate(Face, DesignationKind.Mine);
            d.AddWork(cell, third);
            Assert.That(d.Cancel(Face), Is.EqualTo(IntentRejection.None));

            Assert.That(d.At(cell), Is.EqualTo(DesignationKind.None));
            Assert.That(d.PartMined.Count, Is.EqualTo(1), "the cut was thrown away with the order");
            Assert.That(d.PartMined.WorkAt(0), Is.EqualTo(third));

            d.Designate(Face, DesignationKind.Mine);
            Assert.That(d.WorkDone(cell), Is.EqualTo(third), "a new order started the face from nothing");
            Assert.That(d.PartMined.Count, Is.Zero, "the kept work is the order's again, not held twice");
        }

        [Test]
        public void TheControlsNothingStartedAndACutCarriedOutKeepNothing()
        {
            var (grid, d) = Board();
            int cell = grid.Index(Face);

            // Ordered and cancelled before anybody swung: nothing to keep.
            d.Designate(Face, DesignationKind.Mine);
            d.Cancel(Face);
            Assert.That(d.PartMined.Count, Is.Zero);

            // Carried out: the order is cleared, and the rock is about to be gone.
            d.Designate(Face, DesignationKind.Mine);
            d.AddWork(cell, Whole(grid, cell));
            d.Clear(cell);
            Assert.That(d.PartMined.Count, Is.Zero, "a finished cut left a row behind");
        }

        [Test]
        public void AKeptCutIsNotHandedToOtherTerrain()
        {
            var (grid, d) = Board();
            int cell = grid.Index(Face);
            d.Designate(Face, DesignationKind.Mine);
            d.AddWork(cell, 1000);
            d.Cancel(Face);
            Assume.That(d.PartMined.Count, Is.EqualTo(1));

            // Something other than a mining order changed the cell: the stone the cut was in is
            // not the stone there now.
            grid.Terrain[cell] = CoreContent.TerrainRubble;
            Assume.That(d.PartMined.Holds(cell, grid.Terrain[cell]), Is.False);
            Assume.That(d.CanMine(cell), Is.True, "rubble is cleared with the same order");

            d.Designate(Face, DesignationKind.Mine);
            Assert.That(d.WorkDone(cell), Is.Zero, "rubble inherited the rock's head start");
            Assert.That(d.PartMined.Count, Is.Zero, "the stale row outlived the order that read it");
        }

        [Test]
        public void TheCutIsPublishedWithNoOrderOnItAndIsGoneOnceOrderedAgain()
        {
            var (grid, d) = Board();
            SimWorld world = d.Attach(new SimWorldBuilder().WithSize(grid.Size)).Build();
            int cell = grid.Index(Face);
            int half = Whole(grid, cell) / 2;

            world.Intents.Submit(new Intent(IntentKind.Designate, Face, (int)DesignationKind.Mine));
            world.Tick();
            d.AddWork(cell, half);
            world.Intents.Submit(new Intent(IntentKind.CancelDesignation, Face));
            world.Tick();

            WorldSnapshot frame = world.Views.Current;
            Assert.That(frame.Orders.Length, Is.Zero);
            Assert.That(frame.PartMined.Length, Is.EqualTo(1), "the cracked face was not published");
            Assert.That(frame.PartMined[0].CellIndex, Is.EqualTo(cell));
            Assert.That(frame.PartMined[0].Progress, Is.InRange(126, 128), "half cut, as a byte");

            world.Intents.Submit(new Intent(IntentKind.Designate, Face, (int)DesignationKind.Mine));
            world.Tick();
            frame = world.Views.Current;
            Assert.That(frame.PartMined.Length, Is.Zero, "published twice, as an order and as a leftover");
            Assert.That(frame.Orders.Length, Is.EqualTo(1));
            Assert.That(frame.Orders[0].Progress, Is.InRange(126, 128), "the order lost the kept cut");
        }

        [Test]
        public void TheKeptCutIsHashedOnlyWhileThereIsOneAndSurvivesASave()
        {
            var (grid, d) = Board();
            int cell = grid.Index(Face);

            var empty = StateHash.New();
            d.PartMined.ContributeTo(ref empty);
            Assert.That(empty.Value, Is.EqualTo(StateHash.New().Value),
                "an empty store must hash nothing, or every golden moves");

            d.Designate(Face, DesignationKind.Mine);
            d.AddWork(cell, 1234);
            d.Cancel(Face);
            var kept = StateHash.New();
            d.PartMined.ContributeTo(ref kept);
            Assert.That(kept.Value, Is.Not.EqualTo(empty.Value));

            SimWorld world = new SimWorldBuilder().WithSize(grid.Size).WithSeed(5u).Build();
            var stream = new MemoryStream();
            WorldSave.Save(world, stream, new ISaveable[] { d, d.PartMined });
            stream.Position = 0;

            var restored = new DesignationGrid(grid, new List<PlacedEdifice>());
            SimWorld into = new SimWorldBuilder().WithSize(grid.Size).WithSeed(5u).Build();
            WorldSave.Load(into, stream, new ISaveable[] { restored, restored.PartMined });

            Assert.That(restored.PartMined.Count, Is.EqualTo(1));
            Assert.That(restored.PartMined.CellAt(0), Is.EqualTo(cell));
            Assert.That(restored.PartMined.WorkAt(0), Is.EqualTo(1234));
            restored.Designate(Face, DesignationKind.Mine);
            Assert.That(restored.WorkDone(cell), Is.EqualTo(1234), "the cut did not survive the load");
        }
    }
}
