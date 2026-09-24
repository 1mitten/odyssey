#nullable enable

using NUnit.Framework;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// A frame meshes at most so many chunks, and the rest wait for the next one.
    ///
    /// <para><b>Why this exists</b> (§6c.6, and it is the largest performance fault found in this
    /// project so far). <c>ChunkRenderer.BatchFor</c> meshed every stale chunk the draw loop
    /// touched, in that frame, with no limit. A traced player session on the Huge board at 4K
    /// measured the consequence exactly: **fifty-six seconds and eight thousand frames with no
    /// meshing produced not one frame over 33 ms, and all 192 slow frames fell in the sixty-one
    /// seconds where meshing ran**. Sixty-one of the captured stalls meshed on their own frame, and
    /// every one of them meshed 900 chunks — a whole-board re-mesh in a single frame, about
    /// 165 ms of it.</para>
    ///
    /// <para><b>The load-bearing test is
    /// <see cref="AWholeBoardRemeshIsSpreadOverFramesInsteadOfLandingInOne"/></b>, because that is
    /// the shape of the fault rather than any one instance of it. What must never come back is a
    /// frame whose meshing cost is proportional to how much went stale.</para>
    ///
    /// <para>The fast tier compiles neither Presentation nor Editor, so this file is only ever
    /// proved by the Unity tier.</para>
    /// </summary>
    public class MeshBudgetTests
    {
        /// <summary>
        /// Enough chunks that a budget of four has something to refuse.
        ///
        /// <para><c>CellGrid.ChunkSize</c> is 25, so this is a 5 x 5 grid of chunks a layer. The
        /// first draft used 48 cells, which is 2 x 2, and a board with four chunks cannot tell a
        /// budget of four from no budget at all.</para>
        /// </summary>
        const int Side = 125;

        static RenderTestWorld Board()
        {
            var world = new RenderTestWorld(Side, Side, 4);
            for (int z = 0; z < Side; z++)
            for (int x = 0; x < Side; x++)
                world.Solid(x, z, 1, NaturalContent.TerrainGrass);
            return world.Publish();
        }

        static ChunkRenderer RendererFor(RenderTestWorld world, int budget)
        {
            var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false, ScatterDensity = 0 };
            renderer.Skirt.Enabled = false;
            renderer.MeshBudgetPerFrame = budget;
            return renderer;
        }

        [Test]
        public void AFrameMeshesNoMoreThanItsBudget()
        {
            RenderTestWorld world = Board();
            using ChunkRenderer renderer = RendererFor(world, budget: 4);

            renderer.Render(1, new SliceSettings());

            Assert.That(renderer.ChunksMeshedThisFrame, Is.LessThanOrEqualTo(4),
                "the budget was ignored");
            Assert.That(renderer.ChunksMeshedThisFrame, Is.GreaterThan(0),
                "nothing was meshed at all, so this proves nothing about a budget");
        }

        /// <summary>
        /// The fault itself: a board-wide re-mesh must not land in one frame.
        ///
        /// <para>Every non-ladder graphics toggle calls <c>Model.Remesh()</c>, which dirties every
        /// chunk at once. That is the 900-chunk frame the trace caught, sixty-one times in two
        /// minutes.</para>
        /// </summary>
        [Test]
        public void AWholeBoardRemeshIsSpreadOverFramesInsteadOfLandingInOne()
        {
            RenderTestWorld world = Board();
            using ChunkRenderer renderer = RendererFor(world, budget: 4);

            // Settle: mesh the board out, a budget at a time, as a session would.
            for (int frame = 0; frame < 4096 && renderer.ChunksMeshedThisFrame != 0 || frame == 0; frame++)
                renderer.Render(1, new SliceSettings());

            int drawn = renderer.ChunksDrawn;
            Assert.That(drawn, Is.GreaterThan(4), "too few chunks to say anything about a budget");

            // Now dirty every one of them at once, as a graphics toggle does.
            world.Model.Remesh();
            renderer.Render(1, new SliceSettings());

            Assert.That(renderer.ChunksMeshedThisFrame, Is.LessThanOrEqualTo(4),
                $"{renderer.ChunksMeshedThisFrame} chunks were re-meshed in one frame after a " +
                "board-wide Remesh. The meshing cost is proportional to what went stale again, " +
                "which is the fault 6c.6 measured at 165 ms a frame.");
        }

        /// <summary>
        /// And the deferral finishes. A budget that dropped work rather than postponing it would
        /// leave the board permanently out of date, which is a worse bug than the one it fixes.
        /// </summary>
        [Test]
        public void EverythingDeferredIsMeshedByALaterFrame()
        {
            RenderTestWorld world = Board();
            using ChunkRenderer renderer = RendererFor(world, budget: 4);

            int meshed = 0;
            for (int frame = 0; frame < 4096; frame++)
            {
                renderer.Render(1, new SliceSettings());
                meshed += renderer.ChunksMeshedThisFrame;
                if (renderer.ChunksMeshedThisFrame == 0) break;
            }

            Assert.That(meshed, Is.GreaterThan(4), "the board never meshed past one budget");
            Assert.That(renderer.ChunksMeshDeferred, Is.Zero,
                "the walk still owes chunks after settling, so deferral is not draining");

            // A settled board re-meshes nothing at all: the deferral queue is the staleness itself,
            // so a chunk that has caught up stays caught up.
            renderer.Render(1, new SliceSettings());
            Assert.That(renderer.ChunksMeshedThisFrame, Is.Zero, "a settled board is re-meshing");
        }

        /// <summary>
        /// <c>PrimeAll</c> is the one exception, for the loading screen: on a new game every chunk
        /// is never-meshed, and dribbling the board in over hundreds of frames while the player
        /// watches would be a worse first impression than the freeze they are already waiting
        /// through.
        /// </summary>
        [Test]
        public void PrimingIgnoresTheBudgetBecauseTheLoadingScreenIsTheRightPlaceForAStall()
        {
            RenderTestWorld world = Board();
            using ChunkRenderer renderer = RendererFor(world, budget: 4);

            renderer.PrimeAll(1, new SliceSettings());

            Assert.That(renderer.ChunksMeshedThisFrame, Is.GreaterThan(4),
                "PrimeAll respected the budget, so a new game will arrive in instalments");

            renderer.Render(1, new SliceSettings());
            Assert.That(renderer.ChunksMeshedThisFrame, Is.Zero,
                "the first frame after priming still had meshing to do");
        }

        /// <summary>
        /// The frustum is asked before the mesher, so a chunk nobody can see spends none of the
        /// budget.
        ///
        /// <para>Frustum culling first went in after <c>BatchFor</c>: every stale chunk on the
        /// walk was meshed and only then rejected, so after a board-wide <c>Remesh</c> the budget
        /// went on chunks behind the camera in index order and the ones on screen waited behind
        /// them. With the test ahead of the mesher, a whole-board re-mesh under a frustum round one
        /// chunk meshes that chunk's column and defers nothing.</para>
        /// </summary>
        [Test]
        public void AnOffScreenChunkSpendsNoneOfTheBudget()
        {
            RenderTestWorld world = Board();
            using ChunkRenderer renderer = RendererFor(world, budget: 4);
            renderer.PrimeAll(1, new SliceSettings());

            // A frustum round the middle of the first 25 x 25 chunk only, with no shadow margin to
            // widen it. Inward-facing normals, as GeometryUtility.CalculateFrustumPlanes gives.
            renderer.ShadowCasterMarginMetres = 0f;
            renderer.FrustumOverride = new[]
            {
                new Plane(Vector3.right, new Vector3(20f, 0f, 0f)),
                new Plane(Vector3.left, new Vector3(40f, 0f, 0f)),
                new Plane(Vector3.forward, new Vector3(0f, 0f, 20f)),
                new Plane(Vector3.back, new Vector3(0f, 0f, 40f)),
            };

            world.Model.Remesh();
            renderer.Render(1, new SliceSettings());

            Assert.That(renderer.ChunksDrawn, Is.GreaterThan(0), "the frustum admitted nothing, so this proves nothing");
            Assert.That(renderer.ChunksOutsideFrustum, Is.GreaterThan(4),
                "too few chunks were off screen for the budget to have been at stake");
            Assert.That(renderer.ChunksMeshDeferred, Is.Zero,
                $"{renderer.ChunksMeshDeferred} chunks were deferred with only one chunk on screen, " +
                "so off-screen chunks are still being offered to the mesher");

            // And the off-screen chunks are still stale rather than dropped: take the frustum away
            // and they are meshed.
            renderer.FrustumOverride = null;
            renderer.Render(1, new SliceSettings());
            Assert.That(renderer.ChunksMeshedThisFrame, Is.GreaterThan(0),
                "the off-screen chunks were never marked for meshing, so they would draw stale for ever");
        }

        /// <summary>A budget of zero or less is off, so the old behaviour stays reachable.</summary>
        [Test]
        public void ABudgetOfZeroMeansNoLimit()
        {
            RenderTestWorld world = Board();
            using ChunkRenderer renderer = RendererFor(world, budget: 0);

            renderer.Render(1, new SliceSettings());

            Assert.That(renderer.ChunksMeshedThisFrame, Is.GreaterThan(4),
                "a zero budget is meant to mean 'no limit', for the check harness and the tests " +
                "that measure what meshing costs");
        }
    }
}
