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
    /// The landscape is drawn at one brightness however high it sits.
    ///
    /// <para><b>The fault this pins.</b> The board is terraced, so the outdoor surface spans five
    /// layers and only ever one of them is the active one. Every other step was multiplied by the
    /// depth shade once per layer of drop — 0.68 a layer — so the same meadow came out in three
    /// greens and the hillside read as though it were in shadow. There is no light there to
    /// explain it, and the owner saw it immediately: "the grass gets darker as the height drops".</para>
    ///
    /// <para>The rule that fixes it is not "terrain never dims", which would take the depth cue
    /// off the rock in a mine, where it is doing real work. It is that the shade says how far you
    /// are peering <em>through</em> the world, and nothing is over an outdoor surface. So a cell
    /// with no slab and no solid cell anywhere above it is exempt, and everything else is not.</para>
    /// </summary>
    /// <summary>
    /// What the material a thing is built from does to its colour.
    /// </summary>
    public class StuffTintTests
    {
        /// <summary>
        /// A wooden wall must read as wood.
        ///
        /// <para>It did not. The wood entry was white, so a wall built of wood drew the plaster of
        /// the concrete wall prefab untouched and came out cream — the owner's report of
        /// 2026-09-17. White is the one value that cannot be a mistake in any other entry and is
        /// always a mistake in this one, because over Synty art <c>_BaseColor</c> is a multiply:
        /// white means "leave the concrete alone".</para>
        ///
        /// <para>The assertion is the shape of the colour and not the numbers themselves, so the
        /// tint can be tuned off a screenshot without breaking a test — which is exactly how it
        /// was arrived at. What it will not allow is wood going neutral again.</para>
        /// </summary>
        [Test]
        public void WoodTintsTheArtBrownRatherThanLeavingItAlone()
        {
            Color wood = StuffPalette.StuffTint(NaturalContent.StuffWood);

            Assert.That(wood.r, Is.LessThan(0.99f), "white is a multiply that changes nothing");
            Assert.That(wood.r, Is.GreaterThan(wood.g), "brown is red over green");
            Assert.That(wood.g, Is.GreaterThan(wood.b), "and green over blue");
            Assert.That(wood.r - wood.b, Is.GreaterThan(0.2f),
                "far enough apart to read as timber and not as dirty cream");
        }

        /// <summary>
        /// And stone must not follow it. The two are the only things a colony can build with, so
        /// a change that browned both would leave the player unable to tell them apart — which is
        /// the whole job of this table.
        ///
        /// <para><b>This asked for "grey or cooler" until 2026-09-17, and that half was wrong.</b>
        /// It was written to separate stone from wood, which it does, and nothing in it had ever
        /// looked at steel — so the cool end it permitted was where steel already was, and stone
        /// sat there, brighter, reading as polished metal (owner: *"the stone floor looks more
        /// like steel"*). The guarantee this test exists for is unharmed: wood is warm, stone is
        /// not, and they are still told apart by warmth rather than only by lightness. What it no
        /// longer does is push stone into steel's corner to achieve that.</para>
        ///
        /// <para>Near-neutral rather than cool, then, with the direction left to
        /// <c>StuffPaletteTests</c>, which is where stone is compared with the materials it has to
        /// be distinguished from rather than only with the one it must not resemble.</para>
        /// </summary>
        [Test]
        public void StoneStaysNeutralWhileWoodIsWarm()
        {
            Color wood = StuffPalette.StuffTint(NaturalContent.StuffWood);
            Color stone = StuffPalette.StuffTint(NaturalContent.StuffStone);

            Assert.That(Mathf.Abs(stone.r - stone.b), Is.LessThan(0.08f),
                $"stone {stone} has a colour cast; it is a grey");
            Assert.That(wood.r - wood.b, Is.GreaterThan(stone.r - stone.b),
                "and the two are told apart by warmth, not only by lightness");
        }
    }

    public class DaylightTintTests
    {
        static ChunkBatch MeshLayer(RenderTestWorld world, int layer)
        {
            var batch = new ChunkBatch();
            var mesher = new ChunkMesher(world.Model);
            int chunksPerLayer = world.Chunks.ChunksX * world.Chunks.ChunksZ;
            mesher.Mesh(batch, layer * chunksPerLayer);
            return batch;
        }

        /// <summary>Every tint code emitted into a layer's body and roof lists.</summary>
        static List<int> TintsOf(ChunkBatch batch)
        {
            var codes = new List<int>();
            foreach (InstanceBucket bucket in batch.Body) if (bucket.Count > 0) codes.Add(bucket.Tint);
            // And the ground skin's groups, which is where the ground's tint lives now (design 38 §20).
            for (int g = 0; g < batch.Skin.GroupCount; g++) codes.Add(batch.Skin.GroupTint(g));
            foreach (InstanceBucket bucket in batch.Roof) if (bucket.Count > 0) codes.Add(bucket.Tint);
            return codes;
        }

        /// <summary>
        /// A terrace: grass two layers apart, both under open sky. This is the board the owner is
        /// looking at, reduced to the two cells that disagreed.
        /// </summary>
        static RenderTestWorld Terrace()
        {
            var world = new RenderTestWorld(4, 4, 6);

            // The high step, x = 0..1, and the low step, x = 2..3. Each column is solid to its own
            // surface, because a terrace is ground rather than a shelf floating in air.
            for (int z = 0; z < 4; z++)
            {
                for (int y = 0; y <= 3; y++) world.Solid(0, z, y, NaturalContent.TerrainSubsoil);
                for (int y = 0; y <= 3; y++) world.Solid(1, z, y, NaturalContent.TerrainSubsoil);
                for (int y = 0; y <= 1; y++) world.Solid(2, z, y, NaturalContent.TerrainSubsoil);
                for (int y = 0; y <= 1; y++) world.Solid(3, z, y, NaturalContent.TerrainSubsoil);

                world.Solid(0, z, 3, NaturalContent.TerrainGrass);
                world.Solid(1, z, 3, NaturalContent.TerrainGrass);
                world.Solid(2, z, 1, NaturalContent.TerrainGrass);
                world.Solid(3, z, 1, NaturalContent.TerrainGrass);
            }

            return world.Publish();
        }

        [Test]
        public void BothStepsOfATerraceAreDrawnInTheSameGreen()
        {
            RenderTestWorld world = Terrace();

            List<int> high = TintsOf(MeshLayer(world, 3));
            List<int> low = TintsOf(MeshLayer(world, 1));

            Assert.That(high, Is.Not.Empty, "the high step drew nothing");
            Assert.That(low, Is.Not.Empty, "the low step drew nothing");

            // The whole claim, stated where it bites: resolve each step's colour at the shade its
            // own depth would have earned it, and require the two to be the same colour.
            const float twoLayersDown = 0.68f * 0.68f;

            foreach (int code in high)
            {
                ChunkRenderer.ResolveColour(code, fallback: true, shade: 1f,
                    out Color lit, out Color _);
                foreach (int deep in low)
                {
                    if (TintCode.Value(deep) != TintCode.Value(code)) continue;
                    ChunkRenderer.ResolveColour(deep, fallback: true, shade: twoLayersDown,
                        out Color dimmed, out Color _);
                    Assert.That(dimmed, Is.EqualTo(lit),
                        "two steps of one hillside are drawn in different greens");
                }
            }
        }

        [Test]
        public void GroundUnderTheOpenSkyIsMarkedDaylitAndGroundUnderAnythingIsNot()
        {
            RenderTestWorld world = Terrace();

            foreach (int code in TintsOf(MeshLayer(world, 1)))
                Assert.That(TintCode.IsDaylit(code), Is.True,
                    "a lower terrace with nothing over it was not marked daylit");

            // Now roof the low step and re-ask. One slab is enough: a slab is stored on the cell
            // above the boundary it occupies, so the roof over the grass at layer 1 is the floor
            // of layer 2.
            var roofed = new RenderTestWorld(4, 4, 6);
            for (int z = 0; z < 4; z++)
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y <= 1; y++) roofed.Solid(x, z, y, NaturalContent.TerrainSubsoil);
                roofed.Solid(x, z, 1, NaturalContent.TerrainGrass);
                roofed.Slab(x, z, 2);
            }
            roofed.Publish();

            foreach (int code in TintsOf(MeshLayer(roofed, 1)))
                Assert.That(TintCode.IsDaylit(code), Is.False,
                    "ground under a slab kept its daylight, so a roofed room would not dim");
        }

        [Test]
        public void RockWithRockOverItStillDims()
        {
            // The control. If this ever passes by being daylit, the depth cue has been taken off
            // the one place it earns its keep — the shape of a working, seen from above.
            //
            // The rock has to be *exposed* as well as buried, or the mesher culls it before it
            // reaches a bucket and the test passes by drawing nothing. So the last column is left
            // open: it is the wall of a cutting, which is exactly the thing the shade grades.
            var world = new RenderTestWorld(4, 4, 6);
            for (int z = 0; z < 4; z++)
            for (int x = 0; x < 3; x++)
            for (int y = 0; y <= 4; y++)
                world.Solid(x, z, y);
            world.Publish();

            List<int> buried = TintsOf(MeshLayer(world, 2));
            Assert.That(buried, Is.Not.Empty, "nothing was drawn to check");

            foreach (int code in buried)
            {
                Assert.That(TintCode.IsDaylit(code), Is.False, "buried rock was marked daylit");

                ChunkRenderer.ResolveColour(code, fallback: true, shade: 1f, out Color lit, out Color _);
                ChunkRenderer.ResolveColour(code, fallback: true, shade: 0.46f, out Color dim, out Color _);
                Assert.That(dim, Is.Not.EqualTo(lit), "the depth shade stopped reaching rock");
            }
        }

        [Test]
        public void ATreeIsNotARoof()
        {
            // Woodland is the case that makes "is anything above me" the wrong question and "is
            // anything drawn *over* me" the right one. A trunk occupies the cell above the grass
            // and a colonist walks straight through it; grass in a wood is lit like the grass
            // beside it, or a copse reads as a hole in the meadow.
            var world = new RenderTestWorld(4, 4, 6);
            for (int z = 0; z < 4; z++)
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y <= 1; y++) world.Solid(x, z, y, NaturalContent.TerrainSubsoil);
                world.Solid(x, z, 1, NaturalContent.TerrainGrass);
            }
            world.Edifice(1, 1, 2, NaturalContent.EdificeTreeMeadow, blocking: false);
            world.Publish();

            foreach (int code in TintsOf(MeshLayer(world, 1)))
                Assert.That(TintCode.IsDaylit(code), Is.True, "grass under a tree lost its daylight");
        }

        [Test]
        public void TheDaylightBitDisturbsNothingElseInTheCode()
        {
            // It shares twenty bits with the module and part in the bucket key, and it sits above
            // the eight the palette index uses. Both are easy to break silently.
            foreach (int terrain in new[] { 0, 1, 7, 255 })
            {
                int plain = TintCode.Terrain(terrain);
                int lit = TintCode.Daylit(plain, open: true);

                Assert.That(TintCode.Value(lit), Is.EqualTo(TintCode.Value(plain)), "the palette index moved");
                Assert.That(TintCode.IsTerrain(lit), Is.True);
                Assert.That(TintCode.IsFoliage(lit), Is.False);
                Assert.That(TintCode.IsWater(lit), Is.False);
                Assert.That(TintCode.IsDaylit(plain), Is.False);
                Assert.That(TintCode.Daylit(plain, open: false), Is.EqualTo(plain), "it set the bit anyway");

                int wet = TintCode.Daylit(TintCode.Water(terrain), open: true);
                Assert.That(TintCode.IsWater(wet), Is.True);
                Assert.That(TintCode.IsTerrain(wet), Is.True);

                int leaf = TintCode.Daylit(TintCode.Foliage(terrain), open: true);
                Assert.That(TintCode.IsFoliage(leaf), Is.True);
                Assert.That(TintCode.IsDaylit(leaf), Is.True);
            }

            // The bucket key packs the tint into twenty bits. A code that overflowed them would
            // collide with the part index and two different materials would share one bucket.
            Assert.That(TintCode.Daylit(TintCode.Water(255), open: true), Is.LessThan(1 << 20));
        }

        [Test]
        public void WaterOnALowerStepKeepsItsOpacity()
        {
            // Water carries its depth in the alpha channel, and the shade is explicitly not
            // allowed to touch it. Exempting the colour must not quietly start touching it.
            int code = TintCode.Daylit(TintCode.Water(NaturalContent.TerrainShallowWater), open: true);

            ChunkRenderer.ResolveColour(code, fallback: true, shade: 1f, out Color lit, out Color _);
            ChunkRenderer.ResolveColour(code, fallback: true, shade: 0.2f, out Color deep, out Color _);

            Assert.That(deep, Is.EqualTo(lit), "daylit water dimmed with depth");
            Assert.That(lit.a, Is.EqualTo(StuffPalette.TerrainSolid(
                TintCode.Value(code)).a).Within(1e-4f), "the opacity was overwritten");
        }
    }
}
