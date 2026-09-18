#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What a coloured wood costs in draw calls, measured rather than argued.
    ///
    /// <para>Drawing is bucketed per <i>(module, part, tint)</i> within a chunk, so for as long as a
    /// tree's colour lived in its tint code the colour <b>was</b> a bucket key — and trees are the
    /// second most numerous thing on the board, so the arithmetic was unforgiving: a wood of many
    /// colours cost a draw call for each of them in every chunk it stood in. The colours are
    /// per-instance data now and the tint code says only which of the two trees it is, so a wood
    /// costs two draw calls a chunk whatever it is wearing.</para>
    ///
    /// <para>This measures both halves, because either alone is easy to satisfy and useless: a board
    /// drawing two colours would also cost two buckets.</para>
    ///
    /// <para><c>ChunkBucketScaleTests</c> is the same question asked of walls, and this follows its
    /// shape deliberately: print the numbers, then assert the structural bound.</para>
    /// </summary>
    public class TreeBucketTests
    {
        const int Side = 200;
        const int GroundLayer = 0;
        const int TreeLayer = 1;

        /// <summary>
        /// Trees per thousand columns, to match <c>NaturalMapGenDef.treeDensityPerMille</c>'s
        /// nominal 260 — the density the played meadow is generated at.
        /// </summary>
        const int DensityPerMille = 260;

        [Test]
        public void AWoodCostsTwoDrawsAChunkHoweverManyColoursItWears()
        {
            GroundRelief.Reset();

            var world = new RenderTestWorld(Side, Side, 3);
            int trees = 0;
            var placed = new List<(int X, int Z, ushort Def)>();

            for (int z = 0; z < Side; z++)
            for (int x = 0; x < Side; x++)
            {
                world.Solid(x, z, GroundLayer, NaturalContent.TerrainGrass);

                // Deterministic, and spread the way the generator spreads them rather than on a
                // lattice: a lattice would put the same number of trees in every chunk and hide
                // exactly the variation this is measuring.
                if (GroundScatter.Hash(x, z, 0x7011u) % 1000u >= DensityPerMille) continue;

                ushort def = GroundScatter.Hash(x, z, 0x4413u) % 1000u < 400u
                    ? NaturalContent.EdificeTreeBroadleaf
                    : NaturalContent.EdificeTreeConifer;

                world.Edifice(x, z, TreeLayer, def, NaturalContent.StuffWood, blocking: false);
                placed.Add((x, z, def));
                trees++;
            }

            world.Publish();

            int chunksPerLayer = world.Chunks.ChunksX * world.Chunks.ChunksZ;
            var mesher = new ChunkMesher(world.Model);

            int treeBuckets = 0, worstChunk = 0, instances = 0;
            var colours = new HashSet<int>();

            for (int c = 0; c < chunksPerLayer; c++)
            {
                var batch = new ChunkBatch();
                mesher.Mesh(batch, TreeLayer * chunksPerLayer + c);

                int here = 0;
                foreach (List<InstanceBucket> list in new[] { batch.Body, batch.Roof })
                for (int i = 0; i < list.Count; i++)
                {
                    InstanceBucket b = list[i];
                    if (!TintCode.IsTree(b.Tint)) continue;
                    here++;
                    instances += b.Count;

                    // Counted off the instances, not off the buckets: the buckets no longer know
                    // what colour anything is, which is the whole of the saving.
                    Assert.That(b.IsColoured, "a tree bucket carries per-instance colours");
                    Assert.That(b.BarkDeep!.Count, Is.EqualTo(b.Count), "one colour per instance");
                    for (int k = 0; k < b.Count; k++) colours.Add(Packed(b, k));
                }

                treeBuckets += here;
                if (here > worstChunk) worstChunk = here;
            }

            // What the same variety cost while a colour was a bucket key, counted over the same
            // board. Nothing in the game does this any more; it is here so the two below is a
            // comparison rather than a bare number.
            int asBucketKeys = WorstChunkIfColourWereABucketKey(world, placed);

            TestContext.WriteLine(
                $"board {Side} x {Side}, {trees} trees over {chunksPerLayer} chunks, " +
                $"palette of {TreePalette.Count}\n" +
                $"tree buckets {treeBuckets} total, worst chunk {worstChunk}, " +
                $"{treeBuckets / (float)chunksPerLayer:0.00} per chunk\n" +
                $"colours drawn {colours.Count}, instances {instances}\n" +
                $"a colour in the tint code would have cost a worst chunk of {asBucketKeys} buckets");

            Assert.That(trees, Is.GreaterThan(5_000), "the board did not grow a wood to measure");

            // **One draw per species per chunk, and nothing else will do.** This is the whole
            // claim. It was 17.72 buckets a chunk while the colour lived in the tint code, 4.34
            // when a stand was one colour, and it is 2 now because a colour costs a vector beside a
            // matrix rather than a draw call. If this ever rises, a colour has found its way back
            // into the bucket key and the wood is back on the draw-call bill.
            Assert.That(worstChunk, Is.EqualTo(2),
                "a chunk draws more than one bucket per species");

            // And the half that stops the line above being satisfied by a dull wood.
            Assert.That(colours.Count, Is.GreaterThanOrEqualTo(100),
                "the board drew too little of the palette");

            // The control, pinned to what it measured: 31 in the worst chunk, which is the same
            // 31 the shipped code measured the day before this change, when the colour was still
            // in the tint code. Against 2 now, for the same 150 colours on the same board.
            Assert.That(asBucketKeys, Is.GreaterThan(20),
                "the control is not measuring what it claims to");
        }

        /// <summary>
        /// One instance's colours as a single number, so a set of them counts the distinct colours
        /// actually drawn.
        /// </summary>
        static int Packed(InstanceBucket bucket, int i)
        {
            Vector4 bark = bucket.BarkDeep![i], leaf = bucket.LeafFresh![i];
            return (Mathf.RoundToInt(bark.x * 255f) << 24) ^ (Mathf.RoundToInt(bark.y * 255f) << 16) ^
                   (Mathf.RoundToInt(leaf.x * 255f) << 8) ^ Mathf.RoundToInt(leaf.z * 255f);
        }

        /// <summary>
        /// The worst chunk's bucket count if a tree's colour were still part of its tint code: the
        /// same board, the same chunks, the same colours, counted straight off the placements.
        /// </summary>
        static int WorstChunkIfColourWereABucketKey(RenderTestWorld world,
            List<(int X, int Z, ushort Def)> placed)
        {
            var perChunk = new Dictionary<int, HashSet<int>>();
            foreach ((int x, int z, ushort def) in placed)
            {
                int chunk = world.Chunks.ChunkIndexOfCell(x, z, TreeLayer);
                if (!perChunk.TryGetValue(chunk, out HashSet<int>? seen))
                    perChunk[chunk] = seen = new HashSet<int>();
                seen.Add(TreeLook.ThemeFor(x, z, def));
            }

            int worst = 0;
            foreach (HashSet<int> seen in perChunk.Values)
                if (seen.Count > worst) worst = seen.Count;
            return worst;
        }
    }
}
