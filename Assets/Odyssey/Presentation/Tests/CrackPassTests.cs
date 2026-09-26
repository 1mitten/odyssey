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

        // ---- the break (design 57 §7) ------------------------------------------------------

        static RenderTestWorld RemoveEdifice(RenderTestWorld world, int x, int z, int y)
        {
            int index = world.Index(x, z, y);
            world.Grid.Edifice[index] = -1;
            world.Grid.Flags[index] &= ~Odyssey.Sim.World.CellFlags.BlockingEdifice;
            return world.Publish();
        }

        [Test]
        public void ACrackedWallThatComesDownBreaksIntoFourPieces()
        {
            var world = new RenderTestWorld(8, 8, 3).Edifice(3, 3, 1, CoreContent.EdificeWall).Publish();
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };

            renderer.DrawCracks(Cells(new CrackedCell(world.Index(3, 3, 1), CrackModel.Levels, ground: false)));
            RemoveEdifice(world, 3, 3, 1);
            renderer.DrawCracks(Cells());

            Assert.That(renderer.BreaksInFlight, Is.EqualTo(1), "Odyssey/Shard missing, or the fall was not seen");
            Assert.That(renderer.BreakCellsWatched, Is.Zero);
            // Five parts, four quarters, and a block inside each quarter.
            Assert.That(renderer.BreakDrawCalls, Is.EqualTo(5 * 4 + 4));
        }

        [Test]
        public void AWallNoLongerCrackedButStillStandingDoesNotBreak()
        {
            var world = new RenderTestWorld(8, 8, 3).Edifice(3, 3, 1, CoreContent.EdificeWall).Publish();
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };

            renderer.DrawCracks(Cells(new CrackedCell(world.Index(3, 3, 1), 2, ground: false)));
            renderer.DrawCracks(Cells());
            Assert.That(renderer.BreakCellsWatched, Is.EqualTo(1), "the control: it is watched");

            // Its chunk changes for another reason — a wall built beside it — and it is still there.
            world.Edifice(4, 3, 1, CoreContent.EdificeWall).Publish();
            for (int i = 0; i < ChunkRenderer.WatchFrames; i++) renderer.DrawCracks(Cells());

            Assert.That(renderer.BreaksInFlight, Is.Zero, "a repaired wall fell apart");
            Assert.That(renderer.BreakCellsWatched, Is.Zero, "the watch never let go of its batch");
        }

        [Test]
        public void AFaceMinedOutBreaks()
        {
            var world = new RenderTestWorld(8, 8, 3);
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
                world.Solid(x, z, 0);
            world.Publish();
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };

            renderer.DrawCracks(Cells(new CrackedCell(world.Index(3, 3, 0), CrackModel.Levels, ground: true)));
            world.Mine(3, 3, 0).Publish();
            renderer.DrawCracks(Cells());

            Assert.That(renderer.BreaksInFlight, Is.EqualTo(1));
        }

        [Test]
        public void ACellListedAgainWhileWatchedTakesItsBatchBack()
        {
            var world = new RenderTestWorld(8, 8, 3).Edifice(3, 3, 1, CoreContent.EdificeWall).Publish();
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            List<CrackedCell> cells = Cells(new CrackedCell(world.Index(3, 3, 1), 3, ground: false));

            renderer.DrawCracks(cells);
            renderer.DrawCracks(Cells());
            renderer.DrawCracks(cells);

            Assert.That(renderer.BreakCellsWatched, Is.Zero);
            Assert.That(renderer.CrackCellsHeld, Is.EqualTo(1));
            Assert.That(renderer.CrackCellsMeshed, Is.Zero, "meshed a second batch for a cell it already had");
        }

        [Test]
        public void ThePiecesSplitApartFallAndSinkOutOfSight()
        {
            var centre = new UnityEngine.Vector3(10f, 4.5f, 10f);
            var extents = new UnityEngine.Vector3(1.25f, 1.5f, 1.25f);
            var axis = UnityEngine.Vector3.right;
            float floor = centre.y - extents.y;

            UnityEngine.Vector3 Where(int side, bool upper, float age)
            {
                var start = centre + axis * (side * extents.x * 0.5f)
                            + UnityEngine.Vector3.up * ((upper ? 1f : -1f) * extents.y * 0.5f);
                return ChunkRenderer.PieceMotion(centre, extents, axis, 0.3f, false, side, upper, age).MultiplyPoint3x4(start);
            }

            foreach (int side in new[] { -1, 1 })
            foreach (bool upper in new[] { false, true })
            {
                var start = Where(side, upper, ChunkRenderer.ShudderSeconds);
                var midway = Where(side, upper, ChunkRenderer.SinkFrom);
                var end = Where(side, upper, ChunkRenderer.BreakSeconds);

                Assert.That((midway.x - start.x) * side, Is.GreaterThan(0.02f), $"piece {side},{upper} did not move away from the other half");
                Assert.That(end.y, Is.LessThan(floor), $"piece {side},{upper} is still above ground at the end");
                if (upper)
                    Assert.That(midway.y, Is.LessThan(start.y - 0.5f), "an upper quarter did not fall");
            }
        }
    }
}
