#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What a coloured wood costs in draw calls, measured rather than argued.
    ///
    /// <para>Trees are the second most numerous thing on the board and drawing is bucketed per
    /// <i>(module, part, tint)</i> within a chunk, so the colour of a tree is a bucket key and the
    /// arithmetic is unforgiving: a colour rolled per tree puts every theme in the palette into
    /// every chunk that holds a dozen trees. This is the test that says by how much, and it
    /// measures the alternative alongside the choice so that the number is a comparison and not an
    /// assertion about one board.</para>
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
        public void AWoodCostsAFewBucketsPerChunkHoweverLongThePaletteIs()
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
            var themes = new HashSet<int>();

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
                    themes.Add(TintCode.Value(b.Tint));
                }

                treeBuckets += here;
                if (here > worstChunk) worstChunk = here;
            }

            // What the obvious implementation would have cost: a colour rolled per tree, counted
            // over the same board. Nothing in the game does this; it is here so the number below
            // is a comparison rather than a bare figure.
            int perTreeWorst = PerTreeWorstChunk(world, placed);

            TestContext.WriteLine(
                $"board {Side} x {Side}, {trees} trees over {chunksPerLayer} chunks, " +
                $"palette of {TreePalette.Count}\n" +
                $"tree buckets {treeBuckets} total, worst chunk {worstChunk}, " +
                $"{treeBuckets / (float)chunksPerLayer:0.00} per chunk\n" +
                $"themes drawn {themes.Count}, instances {instances}\n" +
                $"a colour per tree instead would have cost a worst chunk of {perTreeWorst}");

            Assert.That(trees, Is.GreaterThan(5_000), "the board did not grow a wood to measure");

            // Pinned to the measured figure rather than bounded by an argument, for the reason
            // ChunkBucketScaleTests gives about its own numbers: "about the same" cannot be
            // compared a month later, and this is the number that says whether the feature is
            // affordable. Measured 2026-09-18 at TreeLook.StandCell = 40: **4.34 tree buckets a
            // chunk, worst chunk 9**, against 6.75 and a worst of 10 when the stand was 20 cells,
            // and against a worst of 11 — the whole palette — for a colour dealt per tree.
            Assert.That(worstChunk, Is.LessThanOrEqualTo(9),
                "a chunk carries more tree colours than its stands can account for — either the " +
                "stand field stopped clumping, or a colour is being dealt per tree");

            Assert.That(treeBuckets / (float)chunksPerLayer, Is.LessThan(5f),
                "the average chunk's tree buckets grew; this is what the draw-call bill is made of");

            // And the cheap answer — one colour everywhere — is excluded, or the bound above
            // would be satisfied by the dullness this replaced.
            Assert.That(themes.Count, Is.GreaterThanOrEqualTo(TreePalette.Count - 1),
                "the board drew only part of the palette");

            // The comparison, asserted so that it cannot quietly stop being true. A per-tree
            // colour is what a later session would reach for first.
            Assert.That(worstChunk, Is.LessThan(perTreeWorst),
                "dealing a colour to a stand cost as much as dealing one to every tree");
        }

        /// <summary>
        /// The worst chunk's tree-colour count if every tree rolled its own theme: the same board,
        /// the same chunks, counted straight off the placements.
        /// </summary>
        static int PerTreeWorstChunk(RenderTestWorld world, List<(int X, int Z, ushort Def)> placed)
        {
            var perChunk = new Dictionary<int, HashSet<int>>();
            foreach ((int x, int z, ushort def) in placed)
            {
                int chunk = world.Chunks.ChunkIndexOfCell(x, z, TreeLayer);
                if (!perChunk.TryGetValue(chunk, out HashSet<int>? seen))
                    perChunk[chunk] = seen = new HashSet<int>();

                TreeSpecies species = TreeLook.SpeciesOf(def);
                int[] rows = TreePalette.For(species);
                seen.Add(rows[GroundScatter.Hash(x, z, 0x9915u) % (uint)rows.Length]);
            }

            int worst = 0;
            foreach (HashSet<int> seen in perChunk.Values)
                if (seen.Count > worst) worst = seen.Count;
            return worst;
        }
    }
}
