#nullable enable
using System;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Worldgen.Natural
{
    /// <summary>Generation produced a map that cannot be used. Always a content or parameter bug.</summary>
    public sealed class NaturalGenException : Exception
    {
        public NaturalGenException(string message) : base(message) { }
    }

    /// <summary>
    /// Pass 1 — the heightfield.
    ///
    /// One integer value-noise field decides the surface layer of every column. Because the cell
    /// model is discrete and has no slopes, this is terracing rather than hills: with the default
    /// relief of two the surface spans five layers across the whole map, each step a single cell
    /// high, which reads as rolling ground and stays walkable in every direction without a ramp.
    ///
    /// The pass also fixes the stratum boundaries per column, so the strata pass that follows is a
    /// straight-line scan with no arithmetic beyond three comparisons. On a shallow map the stack
    /// is compressed rather than rejected: bedrock gives way to subsoil, and the surface is
    /// clamped to leave at least one layer of air above it, so the 60 x 60 x 5 slice still
    /// produces a playable, if unexciting, meadow.
    /// </summary>
    public sealed class HeightfieldPass : INaturalGenPass
    {
        public int Order => 1;
        public string Name => "Heightfield";

        public void Run(NaturalGenContext ctx)
        {
            var gen = ctx.Gen;
            uint heightSeed = ctx.Seed ^ 0x51A7C39Du;

            // One air layer above the tallest ground is the floor of the contract: the cell a
            // colonist stands in has to exist. Outcrops keep one of their own on top of that.
            int maxSurface = ctx.Size.SizeY - 2;
            if (maxSurface < 0) maxSurface = 0;

            // Enough room beneath for bedrock, the subsoil band and at least one rock cell —
            // unless the map is too shallow to hold all three, in which case the clamp wins and
            // the per-column boundaries below compress the stack.
            int wanted = gen.bedrockLayers + gen.subsoilDepth + 1;
            int minSurface = Math.Min(wanted, maxSurface);

            int min = int.MaxValue, max = int.MinValue;
            for (int z = 0; z < ctx.Size.SizeZ; z++)
            for (int x = 0; x < ctx.Size.SizeX; x++)
            {
                int noise = ValueNoise.Fractal2D(heightSeed, x, z, gen.surfacePeriod, gen.surfaceOctaves);
                int y = gen.groundLayer + ValueNoise.Band(noise, -gen.surfaceRelief, gen.surfaceRelief);
                if (y < minSurface) y = minSurface;
                if (y > maxSurface) y = maxSurface;

                int column = ctx.Column(x, z);
                ctx.SurfaceY[column] = y;
                ctx.TopSolidY[column] = y;

                int subsoilBase = y - gen.subsoilDepth;
                if (subsoilBase < 0) subsoilBase = 0;
                ctx.SubsoilBaseY[column] = subsoilBase;
                ctx.BedrockTopY[column] = Math.Min(gen.bedrockLayers, subsoilBase);

                if (y < min) min = y;
                if (y > max) max = y;
            }

            ctx.Report.SurfaceMinY = min;
            ctx.Report.SurfaceMaxY = max;
        }
    }

    /// <summary>
    /// Pass 2 — strata.
    ///
    /// Bedrock at the bottom, rock above it, a band of subsoil, then the soil surface, then air.
    /// Everything below the surface is solid: a wilderness map has no voids until a colonist digs
    /// one, which is what makes the support rule trivially satisfied at tick zero.
    ///
    /// This is the only pass that touches every cell, so it is the only one whose shape matters
    /// for the scale target. It runs layer-major — <c>y</c>, then <c>z</c>, then <c>x</c> — which
    /// is exactly the memory order of <see cref="GridSize.Index"/>, so the writes are sequential
    /// and the per-column boundaries come from four array reads per row rather than any noise
    /// evaluation. It writes the field arrays directly instead of going through
    /// <see cref="NaturalGenContext.SetTerrain"/> for the same reason.
    /// </summary>
    public sealed class NaturalStrataPass : INaturalGenPass
    {
        public int Order => 2;
        public string Name => "Strata";

        public void Run(NaturalGenContext ctx)
        {
            var grid = ctx.Grid;
            var size = ctx.Size;
            var report = ctx.Report;

            var terrain = grid.Terrain;
            var flags = grid.Flags;
            var floor = grid.Floor;
            var floorStuff = grid.FloorStuff;
            var surfaceY = ctx.SurfaceY;
            var subsoilBaseY = ctx.SubsoilBaseY;
            var bedrockTopY = ctx.BedrockTopY;

            int air = 0, solid = 0, subsoilCells = 0, rockCells = 0, bedrockCells = 0;

            for (int y = 0; y < size.SizeY; y++)
            for (int z = 0; z < size.SizeZ; z++)
            {
                int columnBase = z * size.SizeX;
                int rowBase = (y * size.SizeZ + z) * size.SizeX;

                for (int x = 0; x < size.SizeX; x++)
                {
                    int column = columnBase + x;
                    int index = rowBase + x;
                    int surface = surfaceY[column];

                    ushort material;
                    if (y > surface)
                    {
                        material = NaturalContent.TerrainAir;
                        air++;
                    }
                    else if (y == surface)
                    {
                        // Grass by default; the cover pass turns some of it into bare patches.
                        material = NaturalContent.TerrainGrass;
                        solid++;
                    }
                    else if (y >= subsoilBaseY[column])
                    {
                        material = NaturalContent.TerrainSubsoil;
                        subsoilCells++;
                        solid++;
                    }
                    else if (y < bedrockTopY[column])
                    {
                        material = NaturalContent.TerrainBedrock;
                        bedrockCells++;
                        solid++;
                    }
                    else
                    {
                        material = NaturalContent.TerrainRock;
                        rockCells++;
                        solid++;
                    }

                    terrain[index] = material;
                    // No slabs anywhere on a natural map: ground holds a colonist up, and the
                    // first slab in the world is one the colony builds.
                    floor[index] = CoreContent.SlabNone;
                    floorStuff[index] = CoreContent.StuffNone;
                    flags[index] = material == NaturalContent.TerrainAir
                        ? flags[index] & ~CellFlags.SolidTerrain
                        : flags[index] | CellFlags.SolidTerrain;
                }
            }

            report.AirCells = air;
            report.SolidCells = solid;
            report.SubsoilCells = subsoilCells;
            report.RockCells = rockCells;
            report.BedrockCells = bedrockCells;
            report.GrassCells = ctx.Columns;   // the cover pass converts some of these
        }
    }

    /// <summary>
    /// Pass 3 — surface cover.
    ///
    /// Grass over most of the map, with patches of bare earth, gravel and sand where a second,
    /// finer noise field says so. Two fields rather than one: the first decides *whether* a column
    /// is bare, the second decides *what* it is bare of, so a patch is one material throughout
    /// rather than a speckle of three.
    ///
    /// Everything written here is solid ground. That is the difference from the city generator's
    /// soil and gravel, which are non-solid because there they are the dug-out band beneath a
    /// street; here they are the ground itself, and a non-solid surface cell would be a hole.
    /// </summary>
    public sealed class SurfaceCoverPass : INaturalGenPass
    {
        public int Order => 3;
        public string Name => "SurfaceCover";

        public void Run(NaturalGenContext ctx)
        {
            var gen = ctx.Gen;
            uint coverSeed = ctx.Seed ^ 0x2B6F1D07u;
            uint patchSeed = ctx.Seed ^ 0x7C3319A5u;

            int grass = 0, bare = 0, gravel = 0, sand = 0;

            for (int z = 0; z < ctx.Size.SizeZ; z++)
            for (int x = 0; x < ctx.Size.SizeX; x++)
            {
                int column = ctx.Column(x, z);
                int index = ctx.Index(x, z, ctx.SurfaceY[column]);

                int cover = ValueNoise.Fractal2D(coverSeed, x, z, gen.coverPeriod, gen.coverOctaves);
                if (cover >= gen.barePatchThreshold)
                {
                    grass++;
                    continue;   // the strata pass already laid grass here
                }

                int patch = ValueNoise.Value2D(patchSeed, x, z, gen.patchPeriod);
                ushort material;
                if (patch >= gen.sandThreshold) { material = NaturalContent.TerrainSand; sand++; }
                else if (patch >= gen.gravelThreshold) { material = NaturalContent.TerrainPackedGravel; gravel++; }
                else { material = NaturalContent.TerrainBareEarth; bare++; }

                ctx.SetTerrain(index, material);
            }

            ctx.Report.GrassCells = grass;
            ctx.Report.BareEarthCells = bare;
            ctx.Report.GravelCells = gravel;
            ctx.Report.SandCells = sand;
        }
    }

    /// <summary>
    /// Pass 5 — trees.
    ///
    /// Scattered on grass, with a clumping bias so the map has copses and clearings rather than an
    /// even sprinkle. The bias is one noise field squared: squaring pushes the low end down much
    /// harder than the high end, which turns a smooth field into "mostly sparse, occasionally
    /// dense" — the cheapest thing that produces a wood with an edge to it.
    ///
    /// A tree lives in the **air cell above** the ground it grows from, and does not block
    /// movement. That is RimWorld's model (a tree is a plant, not a wall) and it is what keeps the
    /// walkability invariant true of a forested map: woodland slows a colonist down, it does not
    /// fence them in.
    ///
    /// The density roll is drawn for every column whether or not the column can hold a tree, so
    /// the stream does not depend on the terrain the previous passes happened to produce — change
    /// the grass threshold and the forest shifts, it does not reshuffle.
    /// </summary>
    public sealed class TreePass : INaturalGenPass
    {
        public int Order => 5;
        public string Name => "Trees";

        public void Run(NaturalGenContext ctx)
        {
            var gen = ctx.Gen;
            if (gen.treeDensityPerMille <= 0) return;

            uint clumpSeed = ctx.Seed ^ 0x1D9E4B63u;
            var rng = ctx.Random(NaturalGenPurpose.Trees);
            int conifers = 0, broadleaves = 0;

            for (int z = 0; z < ctx.Size.SizeZ; z++)
            for (int x = 0; x < ctx.Size.SizeX; x++)
            {
                int roll = rng.NextInt(ValueNoise.Scale);
                int species = rng.NextInt(1000);

                int column = ctx.Column(x, z);
                int surface = ctx.SurfaceY[column];
                int groundIndex = ctx.Index(x, z, surface);
                if (ctx.Grid.Terrain[groundIndex] != NaturalContent.TerrainGrass) continue;

                int clump = ValueNoise.Fractal2D(clumpSeed, x, z, gen.treeClumpPeriod, gen.treeClumpOctaves);
                int clumped = clump * clump >> 10;                       // 0..1023, biased low
                if (clumped < gen.treeClumpFloor) continue;              // a clearing, not a thin wood
                int chance = gen.treeDensityPerMille * clumped * ValueNoise.Scale / (1000 * 512);
                if (roll >= chance) continue;

                int treeIndex = groundIndex + ctx.Size.LayerStride;
                if (treeIndex >= ctx.Size.CellCount) continue;           // no room above; cannot happen after the clamp

                ushort def = species < gen.broadleafChance
                    ? NaturalContent.EdificeTreeBroadleaf
                    : NaturalContent.EdificeTreeConifer;
                ctx.PlaceEdifice(treeIndex, def, NaturalContent.StuffWood, blocking: false);
                ctx.Trees.Add(new TreePlacement(treeIndex, def));
                ctx.HasTree[column] = true;
                if (def == NaturalContent.EdificeTreeBroadleaf) broadleaves++; else conifers++;
            }

            ctx.Report.Trees = ctx.Trees.Count;
            ctx.Report.Conifers = conifers;
            ctx.Report.Broadleaves = broadleaves;
        }
    }
}
