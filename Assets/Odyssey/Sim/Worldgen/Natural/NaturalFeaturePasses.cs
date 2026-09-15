#nullable enable
using System;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Worldgen.Natural
{
    /// <summary>
    /// Pass 4 — rock outcrops.
    ///
    /// Occasional stone formations standing above the ground: the early mining target, and the
    /// only vertical relief on a map that is otherwise a heightfield. A mound tapers with distance
    /// from its centre, so it is a lump of rock rather than a block, and it never reaches the top
    /// map layer — a colonist must be able to stand on top of anything the generator builds.
    ///
    /// The surface cell beneath an outcrop becomes rock as well, so a formation is stone all the
    /// way down instead of stone balanced on a patch of grass, and the column stays contiguous.
    /// </summary>
    public sealed class RockOutcropPass : INaturalGenPass
    {
        public int Order => 4;
        public string Name => "Outcrops";

        public void Run(NaturalGenContext ctx)
        {
            var gen = ctx.Gen;
            int count = ctx.Columns * gen.outcropsPer10000Columns / 10000;
            // A rate of zero means zero. The floor below exists so that rounding on a small map
            // cannot silently wipe the feature out; it is not a licence to overrule a map that
            // asked for no outcrops at all.
            if (count < 1) count = gen.outcropsPer10000Columns > 0 ? 1 : 0;

            int cells = 0;
            for (int i = 0; i < count; i++)
            {
                var rng = ctx.Random(NaturalGenPurpose.Outcrops, i);
                int cx = rng.NextInt(ctx.Size.SizeX);
                int cz = rng.NextInt(ctx.Size.SizeZ);
                int radius = rng.NextInt(gen.minOutcropRadius, gen.maxOutcropRadius + 1);
                int height = rng.NextInt(gen.minOutcropHeight, gen.maxOutcropHeight + 1);

                int placed = Raise(ctx, cx, cz, radius, height);
                if (placed <= 0) continue;

                int column = ctx.Column(cx, cz);
                ctx.Outcrops.Add(new RockOutcrop(ctx.Index(cx, cz, ctx.SurfaceY[column]), placed, height));
                cells += placed;
            }

            ctx.Report.Outcrops = ctx.Outcrops.Count;
            ctx.Report.OutcropCells = cells;
        }

        /// <summary>
        /// Raises a tapering mound. The taper is Chebyshev distance rather than Euclidean: it
        /// costs one comparison, and at radii of one to three the difference is a single corner
        /// cell that nobody will ever identify as the wrong shape.
        /// </summary>
        static int Raise(NaturalGenContext ctx, int cx, int cz, int radius, int height)
        {
            int placed = 0;
            int ceiling = ctx.Size.SizeY - 2;   // leave one air layer above the tallest rock

            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = cx + dx, z = cz + dz;
                if ((uint)x >= (uint)ctx.Size.SizeX || (uint)z >= (uint)ctx.Size.SizeZ) continue;

                int distance = Math.Max(dx < 0 ? -dx : dx, dz < 0 ? -dz : dz);
                int localHeight = height - distance;
                if (localHeight < 1) continue;

                int column = ctx.Column(x, z);
                int top = ctx.TopSolidY[column] + localHeight;
                if (top > ceiling) top = ceiling;
                if (top <= ctx.TopSolidY[column]) continue;

                // Stone through: the cover cell under the mound stops being grass.
                int surfaceIndex = ctx.Index(x, z, ctx.SurfaceY[column]);
                Uncover(ctx, surfaceIndex);

                for (int y = ctx.TopSolidY[column] + 1; y <= top; y++)
                {
                    ctx.SetTerrain(ctx.Index(x, z, y), NaturalContent.TerrainRock);
                    ctx.Report.RockCells++;
                    ctx.Report.SolidCells++;
                    ctx.Report.AirCells--;
                    placed++;
                }

                ctx.TopSolidY[column] = top;
            }

            return placed;
        }

        /// <summary>Turns a cover cell to rock, keeping the cover counts honest as it goes.</summary>
        static void Uncover(NaturalGenContext ctx, int index)
        {
            ushort was = ctx.Grid.Terrain[index];
            if (was == NaturalContent.TerrainRock) return;

            if (was == NaturalContent.TerrainGrass) ctx.Report.GrassCells--;
            else if (was == NaturalContent.TerrainBareEarth) ctx.Report.BareEarthCells--;
            else if (was == NaturalContent.TerrainPackedGravel) ctx.Report.GravelCells--;
            else if (was == NaturalContent.TerrainSand) ctx.Report.SandCells--;

            ctx.SetTerrain(index, NaturalContent.TerrainRock);
            ctx.Report.RockCells++;
        }
    }

    /// <summary>
    /// Pass 6 — ore deposits.
    ///
    /// Lumps grown by a bounded random walk, the same shape the city generator scatters salvage
    /// with, and bounded for the same reason: an unbounded walk is the one worldgen construct that
    /// can turn a fast pass into a slow one on an unlucky seed.
    ///
    /// Two differences from salvage. The first is that a lump only ever replaces **rock**, so ore
    /// is never found in soil, in subsoil, in bedrock or in the open — mining it always means
    /// digging into stone. The second is depth weighting: each kind in
    /// <see cref="NaturalContent.Ores"/> carries its own band of depths below the local surface,
    /// so iron is the shallow find and coal is the reason to keep going down.
    /// </summary>
    public sealed class OrePass : INaturalGenPass
    {
        public int Order => 6;
        public string Name => "Ore";

        public void Run(NaturalGenContext ctx)
        {
            var gen = ctx.Gen;
            int count = ctx.Columns * gen.oreDepositsPer10000Columns / 10000;
            // A rate of zero means zero. The floor below exists so that rounding on a small map
            // cannot silently wipe the feature out; it is not a licence to overrule a map that
            // asked for no ore at all.
            if (count < 1) count = gen.oreDepositsPer10000Columns > 0 ? 1 : 0;

            int totalWeight = NaturalContent.TotalOreWeight();

            for (int i = 0; i < count; i++)
            {
                var rng = ctx.Random(NaturalGenPurpose.Ore, i);

                int kind = 0;
                int roll = rng.NextInt(totalWeight);
                for (int k = 0; k < NaturalContent.OreKindCount; k++)
                {
                    roll -= NaturalContent.OreAt(k).Weight;
                    if (roll < 0) { kind = k; break; }
                }

                var ore = NaturalContent.OreAt(kind);
                int x = rng.NextInt(ctx.Size.SizeX);
                int z = rng.NextInt(ctx.Size.SizeZ);
                int depth = rng.NextInt(ore.MinDepth, ore.MaxDepth + 1);
                int target = rng.NextInt(gen.minOreBlob, gen.maxOreBlob + 1);

                // The kind's depth band is a preference, not a promise: on a map too shallow to
                // hold it the lump is pulled into the rock that exists rather than dropped. Skip
                // only where there is no rock at all in the column — a map compressed until the
                // subsoil sits straight on the bedrock.
                int column = ctx.Column(x, z);
                int highest = ctx.SubsoilBaseY[column] - 1;
                int lowest = ctx.BedrockTopY[column];
                if (highest < lowest) continue;

                int y = ctx.SurfaceY[column] - depth;
                if (y > highest) y = highest;
                if (y < lowest) y = lowest;

                int placed = GrowBlob(ctx, ref rng, x, z, y, target, ore.Terrain);
                if (placed <= 0) continue;

                ctx.OreDeposits.Add(new OreDeposit(ctx.Index(x, z, y), placed, kind));
                ctx.Report.OreCells += placed;
                ctx.Report.OreCellsByKind[kind] += placed;
                ctx.Report.RockCells -= placed;
            }

            ctx.Report.OreDeposits = ctx.OreDeposits.Count;
        }

        static int GrowBlob(NaturalGenContext ctx, ref DeterministicRandom rng, int x, int z, int y,
                            int target, ushort material)
        {
            int placed = 0;
            int steps = target * 4;
            for (int s = 0; s < steps && placed < target; s++)
            {
                if (ctx.Size.Contains(x, z, y))
                {
                    int index = ctx.Index(x, z, y);
                    if (ctx.Grid.Terrain[index] == NaturalContent.TerrainRock)
                    {
                        ctx.SetTerrain(index, material);
                        placed++;
                    }
                }

                switch (rng.NextInt(4))
                {
                    case 0: x++; break;
                    case 1: x--; break;
                    case 2: z++; break;
                    default: z--; break;
                }
                if (x < 0) x = 0; else if (x >= ctx.Size.SizeX) x = ctx.Size.SizeX - 1;
                if (z < 0) z = 0; else if (z >= ctx.Size.SizeZ) z = ctx.Size.SizeZ - 1;
            }
            return placed;
        }
    }

    /// <summary>
    /// Pass 7 — the start location, and the consistency check that ends generation.
    ///
    /// The colony lands in a clearing: a square of ground that is flat, free of outcrops and free
    /// of trees, as near the middle of the map as one can be found. Flatness matters more than
    /// anything else here because the cell model has no slopes — a "clearing" spanning two surface
    /// layers is two clearings with a step between them, and the first thing a player builds is a
    /// rectangle.
    ///
    /// The search scores every candidate centre and keeps the best, so it is a single deterministic
    /// sweep with no randomness at all. Whatever it settles on, any trees left inside the clearing
    /// are felled, which makes "the start is clear" an unconditional guarantee rather than
    /// something that holds for most seeds.
    /// </summary>
    public sealed class NaturalStartPass : INaturalGenPass
    {
        public int Order => 7;
        public string Name => "Start";

        public void Run(NaturalGenContext ctx)
        {
            int radius = ctx.Gen.startClearingRadius;
            int limit = (Math.Min(ctx.Size.SizeX, ctx.Size.SizeZ) - 1) / 2;
            if (radius > limit) radius = limit;
            if (radius < 0) radius = 0;

            int centre = -1;
            while (radius >= 0)
            {
                centre = FindClearing(ctx, radius);
                if (centre >= 0) break;
                radius--;
            }

            if (centre < 0)
                throw new NaturalGenException(
                    "No walkable ground to start on. The heightfield or strata pass produced an unusable map.");

            ClearTrees(ctx, centre, radius);

            int column = centre;
            int x = column % ctx.Size.SizeX;
            int z = column / ctx.Size.SizeX;
            ctx.Report.StartCell = new CellRef(x, z, ctx.TopSolidY[column] + 1);

            AssertConsistent(ctx);
        }

        /// <summary>
        /// The best clearing of the given half-width, or -1 if there is none. Score is lexicographic
        /// by weight: height spread first (a step through the middle of a base is the worst
        /// outcome), then trees to fell, then distance from the middle of the map.
        /// </summary>
        static int FindClearing(NaturalGenContext ctx, int radius)
        {
            int sizeX = ctx.Size.SizeX, sizeZ = ctx.Size.SizeZ;
            int centreX = sizeX / 2, centreZ = sizeZ / 2;
            int best = -1, bestScore = int.MaxValue;

            for (int z = radius; z < sizeZ - radius; z++)
            for (int x = radius; x < sizeX - radius; x++)
            {
                int column = ctx.Column(x, z);
                int distance = Math.Abs(x - centreX) + Math.Abs(z - centreZ);

                // Cheap reject before the window scan: nothing beats the incumbent from here.
                if (distance >= bestScore) continue;

                int minY = int.MaxValue, maxY = int.MinValue, trees = 0;
                bool usable = true;

                for (int dz = -radius; dz <= radius && usable; dz++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int wc = ctx.Column(x + dx, z + dz);
                    int surface = ctx.SurfaceY[wc];

                    // An outcrop is rock standing on the ground, not ground: never build on one.
                    if (ctx.TopSolidY[wc] != surface) { usable = false; break; }
                    if (!NaturalContent.IsGround(ctx.Grid.Terrain[ctx.Index(x + dx, z + dz, surface)]))
                    {
                        usable = false;
                        break;
                    }
                    if (!ctx.Grid.IsWalkable(ctx.Index(x + dx, z + dz, surface + 1))) { usable = false; break; }

                    if (surface < minY) minY = surface;
                    if (surface > maxY) maxY = surface;
                    if (ctx.HasTree[wc]) trees++;
                }

                if (!usable) continue;

                int score = (maxY - minY) * 65536 + trees * 256 + distance;
                if (score < bestScore) { bestScore = score; best = column; }
            }

            return best;
        }

        static void ClearTrees(NaturalGenContext ctx, int centreColumn, int radius)
        {
            int cx = centreColumn % ctx.Size.SizeX;
            int cz = centreColumn / ctx.Size.SizeX;

            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = cx + dx, z = cz + dz;
                if ((uint)x >= (uint)ctx.Size.SizeX || (uint)z >= (uint)ctx.Size.SizeZ) continue;

                int column = ctx.Column(x, z);
                if (!ctx.HasTree[column]) continue;

                ctx.RemoveEdifice(ctx.Index(x, z, ctx.SurfaceY[column] + 1));
                ctx.HasTree[column] = false;
                ctx.Report.TreesClearedForStart++;
                ctx.Report.Trees--;
            }

            if (ctx.Report.TreesClearedForStart == 0) return;

            // Compact the placement list so it lists standing trees only. The edifice *handles*
            // are untouched — a felled tree keeps its slot, as the city generator's removed
            // edifices do — so nothing that holds a handle is invalidated by this.
            int keep = 0;
            for (int i = 0; i < ctx.Trees.Count; i++)
            {
                var tree = ctx.Trees[i];
                if (ctx.Grid.Edifice[tree.CellIndex] < 0) continue;
                ctx.Trees[keep++] = tree;
            }
            ctx.Trees.RemoveRange(keep, ctx.Trees.Count - keep);
        }

        /// <summary>
        /// The invariants a generation bug would break long before a player noticed. The column
        /// one is the important one and is specific to this generator: a wilderness map is solid
        /// from the bottom of the world up to the surface and air above it, with nothing floating
        /// and no holes, which is why no support solve is needed at tick zero.
        /// </summary>
        static void AssertConsistent(NaturalGenContext ctx)
        {
            var grid = ctx.Grid;
            var size = ctx.Size;

            for (int y = 0; y < size.SizeY; y++)
            for (int z = 0; z < size.SizeZ; z++)
            {
                int columnBase = z * size.SizeX;
                int rowBase = (y * size.SizeZ + z) * size.SizeX;

                for (int x = 0; x < size.SizeX; x++)
                {
                    int index = rowBase + x;
                    ushort material = grid.Terrain[index];

                    if (!NaturalContent.IsKnown(material))
                        throw new NaturalGenException($"Cell {size.FromIndex(index)} has unknown terrain {material}.");

                    bool solidFlag = (grid.Flags[index] & CellFlags.SolidTerrain) != 0;
                    if (solidFlag != NaturalContent.IsSolid(material))
                        throw new NaturalGenException(
                            $"Cell {size.FromIndex(index)} has a solid flag that disagrees with its terrain.");

                    bool shouldBeSolid = y <= ctx.TopSolidY[columnBase + x];
                    if (solidFlag != shouldBeSolid)
                        throw new NaturalGenException(
                            $"Cell {size.FromIndex(index)} breaks the column rule: solid up to the surface, air above.");

                    int handle = grid.Edifice[index];
                    if (handle < -1 || handle >= ctx.Edifices.Count)
                        throw new NaturalGenException(
                            $"Cell {size.FromIndex(index)} has edifice handle {handle}, out of range.");
                    if (handle >= 0)
                    {
                        var placed = ctx.Edifices[handle];
                        if (placed.CellIndex != index)
                            throw new NaturalGenException(
                                $"Edifice {handle} is filed under cell {index} but records cell {placed.CellIndex}.");
                        if (solidFlag)
                            throw new NaturalGenException(
                                $"Cell {size.FromIndex(index)} holds an edifice inside solid terrain.");
                    }
                }
            }
        }
    }
}
