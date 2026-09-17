#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// Does the instancing hold its shape at the scale the design promised (OQ-03)?
    ///
    /// <para><b>What the claim is.</b> ADR 0005 and the rendering design rest on one structural
    /// property: draw submission is bucketed per <i>(module, part, tint)</i> within a chunk, so the
    /// number of buckets — and therefore of draw calls — is bounded by the *variety* on the board
    /// and not by the *quantity*. Twenty thousand walls of four materials should cost about as many
    /// buckets as two hundred walls of four materials, and vastly more instances. Nothing had
    /// measured that; every mesher test until now used an eight-by-eight board where the
    /// distinction cannot appear.</para>
    ///
    /// <para><b>Why the numbers are printed as well as asserted.</b> This is the before half of
    /// `OQ-46`, whose acceptance is that mesh contributors leave the bucket counts identical. A
    /// figure nobody wrote down cannot be compared with afterwards, so the run prints its own
    /// oracle.</para>
    ///
    /// <para>Presentation tests are EditMode only — there is no fast-tier mirror for anything that
    /// needs UnityEngine — so this runs in the Unity gate, where the mesher's other tests live.</para>
    ///
    /// <para><b>What the scale found that an eight-by-eight board could not.</b> The mesher draws
    /// <b>exactly five instances per wall cell</b> — four side panels and one fill-and-cap —
    /// whether or not a given side is exposed. Counted off the grid independently, this board has
    /// <b>79,600</b> exposed wall faces, not 80,000: the missing 400 are the faces that point off
    /// the edge of the board, 100 along each of the four sides. The mesher draws those 400 anyway.
    /// </para>
    ///
    /// <para><c>TheWorldBoundaryIsNotAnExposedFace</c> establishes that rule for <i>terrain</i> —
    /// "there is no outside of the map, so there is nowhere those faces could be seen from" — and
    /// it is not applied to <i>edifice walls</i>. On an eight-by-eight fixture the difference is a
    /// handful of instances and invisible; here it is 400 of 100,000, which is 0.4% and still
    /// nothing, but it is an inconsistency between two kinds of geometry rather than a cost.
    /// <b>Recorded, not fixed:</b> this row may not touch <c>Presentation/Rendering/</c>, and
    /// <c>ChunkMesher</c> is the owner's live file. The assertions below pin the behaviour as it
    /// is, so that a deliberate fix shows up as a failing number rather than as drift.</para>
    /// </summary>
    public class ChunkBucketScaleTests
    {
        const int Side = 200;
        const int WallLayer = 1;

        /// <summary>Four materials, which is what the row asked the board to carry.</summary>
        static readonly ushort[] Stuffs =
        {
            CoreContent.StuffConcrete,
            CoreContent.StuffSteel,
            CoreContent.StuffComposite,
            NaturalContent.StuffWood,
        };

        [Test]
        public void TwentyThousandWallsStayBoundedByVarietyRatherThanQuantity()
        {
            GroundRelief.Reset();

            // A checkerboard, so every wall is an island with four exposed sides. A solid field of
            // walls would be twenty thousand cells and almost no faces — it would exercise the
            // bucket count and not the instance count, and the instance count is the half that is
            // supposed to grow.
            var world = new RenderTestWorld(Side, Side, 3);

            int walls = 0;
            for (int z = 0; z < Side; z++)
            for (int x = 0; x < Side; x++)
            {
                world.Solid(x, z, 0);                       // ground beneath, so no downward face
                if (((x + z) & 1) != 0) continue;

                world.Edifice(x, z, WallLayer, CoreContent.EdificeWall, Stuffs[(x / 2 + z / 2) % Stuffs.Length]);
                walls++;
            }

            world.Publish();

            // One batch per chunk: a ChunkBatch belongs to a chunk, and meshing replaces its
            // contents rather than appending to them.
            int chunksPerLayer = world.Chunks.ChunksX * world.Chunks.ChunksZ;
            var mesher = new ChunkMesher(world.Model);

            int buckets = 0, instances = 0, worstChunkBuckets = 0;
            var kinds = new HashSet<long>();
            var tints = new HashSet<int>();

            for (int c = 0; c < chunksPerLayer; c++)
            {
                var batch = new ChunkBatch();
                mesher.Mesh(batch, WallLayer * chunksPerLayer + c);

                int here = batch.Body.Count + batch.Roof.Count;
                buckets += here;
                if (here > worstChunkBuckets) worstChunkBuckets = here;

                Tally(batch.Body, kinds, tints, ref instances);
                Tally(batch.Roof, kinds, tints, ref instances);
            }

            int exposed = ExposedSideFaces(world, walls);

            TestContext.WriteLine(
                $"board {Side} x {Side}, {walls} wall cells of {Stuffs.Length} stuffs over " +
                $"{chunksPerLayer} chunks\n" +
                $"buckets {buckets} total, worst chunk {worstChunkBuckets}\n" +
                $"distinct (module, part) kinds {kinds.Count}, distinct tints {tints.Count}\n" +
                $"instances {instances} = {(double)instances / walls:F2} per wall cell\n" +
                $"exposed side faces (counted from the grid, independently) {exposed}\n" +
                $"instances per bucket {(buckets == 0 ? 0 : (double)instances / buckets):F1}");

            Assert.That(walls, Is.GreaterThanOrEqualTo(20_000),
                "the row asks for at least twenty thousand wall cells");

            // Exact, because this test's other job is to be the "before" half of OQ-46: mesh
            // contributors must leave these numbers untouched, and "about the same" cannot be
            // compared a month later. A bucket is one (module, part, tint) in one chunk, and this
            // board carries every combination in every chunk, so the bound is met rather than
            // merely respected.
            Assert.That(buckets, Is.EqualTo(chunksPerLayer * kinds.Count * tints.Count),
                "the bucket count moved. If mesh contributors did this, they changed submission " +
                "and not only structure; if content did it, the board is no longer four stuffs " +
                "in every chunk.");

            // Five per wall cell: four side panels and one fill-and-cap. Also exact, and also for
            // OQ-46 — see the boundary note in the class comment for why five and not 4.98.
            Assert.That(instances, Is.EqualTo(walls * 5),
                "instances per wall cell moved away from five. Either the mesher stopped drawing " +
                "something, or it started culling the board-edge faces described in the class " +
                "comment — the second would be a deliberate fix and this number should follow it.");

            // The structural claim, and the one worth failing on. A bucket is one
            // (module, part, tint) within one chunk, so the total cannot exceed the number of
            // chunks times the variety the board actually carries. If this ever fails, buckets
            // have started tracking quantity — per instance, or leaking between chunks — and the
            // instancing design is broken rather than merely slower.
            int bound = chunksPerLayer * kinds.Count * tints.Count;
            Assert.That(buckets, Is.LessThanOrEqualTo(bound),
                $"buckets ({buckets}) exceeded chunks x kinds x tints ({bound}), so submission is " +
                "no longer bounded by variety");

            // And the instances did grow, which is what makes the bound above worth having: a
            // mesher that drew nothing would satisfy it trivially.
            Assert.That(instances, Is.GreaterThan(walls),
                "twenty thousand walls produced fewer instances than cells, so something is not " +
                "being drawn at all");
        }

        static void Tally(List<InstanceBucket> list, HashSet<long> kinds, HashSet<int> tints, ref int instances)
        {
            for (int i = 0; i < list.Count; i++)
            {
                InstanceBucket b = list[i];
                kinds.Add(((long)b.Module << 20) | (uint)b.Part);
                tints.Add(b.Tint);
                instances += b.Count;
            }
        }

        /// <summary>
        /// Exposed vertical faces, counted straight off the grid rather than off the mesher, so
        /// the two are independent. A face is exposed where a wall has no wall beside it; the edge
        /// of the board is not an outside (`TheWorldBoundaryIsNotAnExposedFace`).
        /// </summary>
        static int ExposedSideFaces(RenderTestWorld world, int walls)
        {
            int faces = 0;
            for (int z = 0; z < Side; z++)
            for (int x = 0; x < Side; x++)
            {
                if (((x + z) & 1) != 0) continue;

                if (x > 0 && ((x - 1 + z) & 1) != 0) faces++;
                if (x + 1 < Side && ((x + 1 + z) & 1) != 0) faces++;
                if (z > 0 && ((x + z - 1) & 1) != 0) faces++;
                if (z + 1 < Side && ((x + z + 1) & 1) != 0) faces++;
            }

            return faces;
        }
    }
}
