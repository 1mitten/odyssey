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

        /// <summary>
        /// How many stand squares a 25-cell chunk can be nearest to.
        ///
        /// A stand site is jittered anywhere inside its own 40-cell square, so a chunk straddling a
        /// boundary can take cells from the squares on either side of it and from the diagonals:
        /// three squares along each axis rather than the two its size would suggest.
        /// </summary>
        const int StandsAChunkCanOverlap = 9;

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
                    themes.Add(TintCode.TreeValue(b.Tint));
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

            // Measured 2026-09-18 on this board, with StandCell 40 and a handful of four:
            // **17.72 tree buckets a chunk, worst chunk 29**, drawing 130 of the 166 themes —
            // against 4.34 and a worst of 9 when a stand dealt one colour, and against a worst of
            // 107 for a colour rolled freely per tree. The handful is what sits between those two.
            //
            // Measured 2026-09-18 on this board, with StandCell 40 and a handful of four:
            // **17.72 tree buckets a chunk, worst chunk 29**, drawing 130 of the 166 themes —
            // against 4.34 and a worst of 9 when a stand dealt one colour, and against a worst of
            // 107 for a colour rolled freely per tree. The handful sits between those two.
            //
            // The structural bound, and it is what makes the palette free to grow. A chunk
            // overlaps a handful of stand squares; each stand deals ThemesPerStand colours to each
            // of the two species; and that product is the ceiling however long TreePalette
            // becomes. It is generous rather than tight on purpose — the jittered sites let a
            // 25-cell chunk reach into more squares than its size suggests — and the printed
            // figure above is the number to compare against next time.
            int ceiling = StandsAChunkCanOverlap * 2 * TreeLook.ThemesPerStand;
            Assert.That(worstChunk, Is.LessThanOrEqualTo(ceiling),
                $"a chunk carries more tree colours ({worstChunk}) than its stands can account " +
                $"for ({ceiling}) — either the stand field stopped clumping, or a colour is being " +
                "dealt per tree");

            // And the comparison that says the bound is worth having: rolling a colour freely per
            // tree over the same board. Nothing in the game does this; it is here so the figure
            // above is a comparison rather than a bare number, and because a per-tree roll is what
            // a later session would reach for first.
            Assert.That(worstChunk, Is.LessThan(perTreeWorst / 2),
                "dealing a handful to a stand cost about as much as dealing a colour to every tree");

            // The wood is mixed, not patched: a chunk carries several colours per species rather
            // than the one the first version of this feature gave it.
            Assert.That(treeBuckets / (float)chunksPerLayer, Is.GreaterThan(6f),
                "a chunk carries too few tree colours for the wood to read as mixed");

            Assert.That(themes.Count, Is.GreaterThanOrEqualTo(40),
                "the board drew too little of the palette");
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
