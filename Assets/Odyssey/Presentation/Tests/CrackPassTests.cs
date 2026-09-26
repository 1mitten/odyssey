#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What the crack pass draws (<c>docs/design/57-cracks.md</c>). How the cracks look is the
    /// owner's to judge; that the pass cracks exactly the cell's own meshes, batches a run of walls
    /// into the same calls as one wall, meshes a cell once rather than once a frame, and lets go of
    /// a cell that stopped being cracked is a test's. None of these needs the licensed art.
    /// </summary>
    public class CrackPassTests
    {
        [SetUp]
        public void SetUp() => GroundRelief.Reset();

        [TearDown]
        public void TearDown() => GroundRelief.Reset();

        static List<CrackedCell> Cells(params CrackedCell[] cells) => new List<CrackedCell>(cells);

        [Test]
        public void AStruckWallIsCrackedAsExactlyThePartsTheChunkDraws()
        {
            var world = new RenderTestWorld(8, 8, 3).Edifice(3, 3, 1, CoreContent.EdificeWall).Publish();
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            Assert.That(renderer.CracksAvailable, Is.True, "Odyssey/Crack was not found");

            renderer.DrawCracks(Cells(new CrackedCell(world.Index(3, 3, 1), 2, ground: false)));

            Assert.That(renderer.CrackedCellsDrawn, Is.EqualTo(1));
            Assert.That(renderer.CrackInstancesDrawn, Is.EqualTo(5), "four panels and a core, as the chunk draws it");
            Assert.That(renderer.CrackDrawCalls, Is.GreaterThan(0));
        }

        [Test]
        public void ARunOfWallsCostsTheCallsOfOneWall()
        {
            // P10: a pass that draws once per cell. Three walls at one stage share meshes, so they
            // must share calls — the instances grow, the calls do not.
            var one = new RenderTestWorld(8, 8, 3).Edifice(1, 3, 1, CoreContent.EdificeWall).Publish();
            using var single = new ChunkRenderer(one.Model) { SubmitToGpu = false };
            single.DrawCracks(Cells(new CrackedCell(one.Index(1, 3, 1), 3, ground: false)));

            var run = new RenderTestWorld(8, 8, 3);
            for (int x = 1; x <= 5; x += 2) run.Edifice(x, 3, 1, CoreContent.EdificeWall);
            run.Publish();
            using var many = new ChunkRenderer(run.Model) { SubmitToGpu = false };
            many.DrawCracks(Cells(
                new CrackedCell(run.Index(1, 3, 1), 3, ground: false),
                new CrackedCell(run.Index(3, 3, 1), 3, ground: false),
                new CrackedCell(run.Index(5, 3, 1), 3, ground: false)));

            Assert.That(many.CrackInstancesDrawn, Is.EqualTo(3 * single.CrackInstancesDrawn));
            Assert.That(many.CrackDrawCalls, Is.EqualTo(single.CrackDrawCalls), "a call per cracked cell");
        }

        [Test]
        public void AFaceBeingMinedIsCracked()
        {
            var world = new RenderTestWorld(8, 8, 3);
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
                world.Solid(x, z, 0);
            world.Publish();

            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            renderer.DrawCracks(Cells(new CrackedCell(world.Index(3, 3, 0), 1, ground: true)));

            Assert.That(renderer.CrackedCellsDrawn, Is.EqualTo(1));
            Assert.That(renderer.CrackInstancesDrawn, Is.GreaterThan(0), "the rock was drawn cracked as nothing");
        }

        [Test]
        public void ACellIsMeshedOnceAndAgainOnlyWhenItsChunkChanges()
        {
            var world = new RenderTestWorld(8, 8, 3).Edifice(3, 3, 1, CoreContent.EdificeWall).Publish();
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            List<CrackedCell> cells = Cells(new CrackedCell(world.Index(3, 3, 1), 1, ground: false));

            renderer.DrawCracks(cells);
            Assert.That(renderer.CrackCellsMeshed, Is.EqualTo(1));
            renderer.DrawCracks(cells);
            Assert.That(renderer.CrackCellsMeshed, Is.Zero, "meshed again with nothing changed");
            Assert.That(renderer.CrackInstancesDrawn, Is.EqualTo(5), "the kept batch still draws");

            world.Model.RemeshChunk(world.Chunks.ChunkIndexOfCell(3, 3, 1));
            renderer.DrawCracks(cells);
            Assert.That(renderer.CrackCellsMeshed, Is.EqualTo(1), "its chunk changed and it was not re-meshed");
        }

        [Test]
        public void ACellNoLongerCrackedLetsGoAndNothingCrackedDrawsNothing()
        {
            var world = new RenderTestWorld(8, 8, 3).Edifice(3, 3, 1, CoreContent.EdificeWall).Publish();
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };

            renderer.DrawCracks(Cells(new CrackedCell(world.Index(3, 3, 1), 2, ground: false)));
            Assert.That(renderer.CrackCellsHeld, Is.EqualTo(1));

            renderer.DrawCracks(Cells());
            Assert.That(renderer.CrackCellsHeld, Is.Zero, "a repaired or fallen wall kept its batch");
            Assert.That(renderer.CrackDrawCalls, Is.Zero);
            Assert.That(renderer.CrackInstancesDrawn, Is.Zero);
        }

        [Test]
        public void SwitchedOffItDrawsNothingAndSaysSo()
        {
            var world = new RenderTestWorld(8, 8, 3).Edifice(3, 3, 1, CoreContent.EdificeWall).Publish();
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false, Cracks = false };

            renderer.DrawCracks(Cells(new CrackedCell(world.Index(3, 3, 1), 3, ground: false)));
            Assert.That(renderer.CrackDrawCalls, Is.Zero);
            Assert.That(renderer.CracksAvailable, Is.False, "the mining slab would vanish with nothing in its place");
        }
    }
}
