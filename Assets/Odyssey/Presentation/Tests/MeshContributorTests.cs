#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// A terrain kind brings its own meshing rather than adding a branch to the mesher (OQ-46).
    ///
    /// <para><b>What was wrong.</b> Drawing a terrain cell was a chain of early returns in one
    /// method — water, surface, an exposure gate, stone, earth, a plain block — so every new
    /// terrain feature was an edit to <c>ChunkMesher</c>. That file is on the queue's do-not-touch
    /// list and both the water line and the mining line edited it anyway, which is what a rule the
    /// design leaves no way to obey looks like from the outside.</para>
    ///
    /// <para><b>What this must not do is change a single pixel.</b> The acceptance for the row is
    /// that existing output is unchanged: <c>ChunkMesherTests</c> passes with no edits, and the
    /// bucket and instance counts pinned by <c>ChunkBucketScaleTests</c> are identical before and
    /// after. Those two suites are the real test of this change; what is here is only what they
    /// cannot say — that the seam is a seam, and that the order the chain relied on survived
    /// becoming a list.</para>
    /// </summary>
    public class MeshContributorTests
    {
        /// <summary>
        /// The order is the chain's order, and it is load-bearing rather than tidy. Water must be
        /// asked before surface, because water is not solid and the surface contributor would
        /// otherwise draw a pond as pavement. Stone and earth must be asked before the plain
        /// block, because the block claims anything with a face. A list that reordered itself
        /// would fail no other test until somebody looked at a lake.
        /// </summary>
        [Test]
        public void TheContributorsAreAskedInTheOrderTheChainUsedToRun()
        {
            var mesher = new ChunkMesher(new RenderTestWorld(4, 4, 2).Publish().Model);

            var order = new List<string>();
            foreach (ITerrainContributor c in mesher.TerrainContributors) order.Add(c.GetType().Name);

            Assert.That(order, Is.EqualTo(new[]
            {
                nameof(WaterContributor),
                nameof(SurfaceContributor),
                nameof(StoneContributor),
                nameof(EarthContributor),
                nameof(SolidBlockContributor),
            }), "the contributor order is the old chain's order and changing it changes what is drawn");
        }

        /// <summary>
        /// A feature can claim cells without editing the mesher — the whole point of the row. It
        /// is registered ahead of the plain block, so it takes cells that would otherwise be drawn
        /// as ordinary solid terrain, and the block stays last.
        ///
        /// <para><b>The board is fill, not rock, and that is the test's real content.</b>
        /// "Registered ahead of the plain block" does not mean registered ahead of *everything*:
        /// stone and earth are asked first and claim their own terrains, so a spy on a rock board
        /// is never asked at all — which is exactly what the first version of this test measured,
        /// and it read as the seam being broken when it was the fixture being wrong. `TerrainFill`
        /// is solid and is neither stone nor earth, so it reaches the fallback, which is the only
        /// place a newly registered contributor can intercept.</para>
        /// </summary>
        [Test]
        public void ARegisteredContributorClaimsCellsAheadOfThePlainBlock()
        {
            var world = new RenderTestWorld(8, 8, 3);
            for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
            {
                world.Solid(x, z, 0, CoreContent.TerrainFill);
                // One cell left open, so its neighbours show a face and are drawn at all.
                if (x != 3 || z != 3) world.Solid(x, z, 1, CoreContent.TerrainFill);
            }
            world.Publish();

            var plain = new ChunkMesher(world.Model);
            int before = Instances(MeshLayer(plain, world, 1));

            var claiming = new ChunkMesher(world.Model);
            var spy = new ClaimEverythingSolid();
            claiming.AddTerrainContributor(spy);
            int after = Instances(MeshLayer(claiming, world, 1));

            Assert.That(spy.Claimed, Is.GreaterThan(0), "the registered contributor was never asked");
            Assert.That(after, Is.Zero,
                "a contributor that claims a cell and draws nothing should leave nothing drawn; " +
                "the plain block drew it anyway, so registration is not ahead of the fallback");
            Assert.That(before, Is.GreaterThan(0), "the fixture is wrong: nothing was drawn without the spy");
        }

        /// <summary>
        /// And it is inserted before the block rather than appended after it, which would be a
        /// registration that silently never fires.
        /// </summary>
        [Test]
        public void RegistrationKeepsThePlainBlockLast()
        {
            var mesher = new ChunkMesher(new RenderTestWorld(4, 4, 2).Publish().Model);
            mesher.AddTerrainContributor(new ClaimEverythingSolid());

            IReadOnlyList<ITerrainContributor> list = mesher.TerrainContributors;
            Assert.That(list[list.Count - 1], Is.InstanceOf<SolidBlockContributor>(),
                "the plain block must stay at the end or a registered contributor never runs");
            Assert.That(list[list.Count - 2], Is.InstanceOf<ClaimEverythingSolid>(),
                "a registered contributor goes immediately ahead of the block");
        }

        /// <summary>
        /// <c>ChunkRenderer.Earth</c> switches the earth look, and it now reaches a contributor
        /// rather than a field. If that forwarding broke, the switch would silently do nothing —
        /// which is the failure this change could most easily have introduced.
        /// </summary>
        [Test]
        public void TheEarthSwitchStillReachesTheThingItSwitches()
        {
            var mesher = new ChunkMesher(new RenderTestWorld(4, 4, 2).Publish().Model);

            Assert.That(mesher.Earth, Is.True, "earth is on by default");

            mesher.Earth = false;
            foreach (ITerrainContributor c in mesher.TerrainContributors)
                if (c is EarthContributor earth)
                    Assert.That(earth.Enabled, Is.False, "the switch did not reach the contributor");

            mesher.Earth = true;
            Assert.That(mesher.Earth, Is.True, "the switch does not read back what it wrote");
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Claims every solid cell and draws nothing, so its effect is visible as an absence.</summary>
        sealed class ClaimEverythingSolid : ITerrainContributor
        {
            public int Claimed { get; private set; }

            public bool Emit(in TerrainCell cell, in MeshSink sink)
            {
                if (!cell.Solid) return false;
                Claimed++;
                return true;
            }
        }

        static ChunkBatch MeshLayer(ChunkMesher mesher, RenderTestWorld world, int layer)
        {
            var batch = new ChunkBatch();
            mesher.Mesh(batch, layer * world.Chunks.ChunksX * world.Chunks.ChunksZ);
            return batch;
        }

        static int Instances(ChunkBatch batch)
        {
            int n = 0;
            foreach (InstanceBucket b in batch.Body) n += b.Count;
            foreach (InstanceBucket b in batch.Roof) n += b.Count;
            return n;
        }
    }
}
