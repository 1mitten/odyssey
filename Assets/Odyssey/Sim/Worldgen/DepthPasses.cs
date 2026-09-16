#nullable enable
using System;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Worldgen
{
    /// <summary>Generation produced a map that cannot be used. Always a content or parameter bug.</summary>
    public sealed class WorldGenException : Exception
    {
        public WorldGenException(string message) : base(message) { }
    }

    /// <summary>
    /// Pass 6 — underground strata.
    ///
    /// The vertical budget from a-12-map-generation.md, expressed as depth bands below street
    /// level: L-1 is the service stratum of utility tunnels and drains; L-2 and L-3 are deep
    /// infrastructure, the metro and the buried-city seam that carries the richest salvage; L-4 to
    /// L-6 are engineered fill grading into soil; below that, natural rock with caves.
    ///
    /// The two boundaries that vary — where fill becomes soil and where soil becomes rock — are
    /// per column from noise, so the strata undulate rather than sitting at fixed depths.
    ///
    /// This runs *after* stamping, as the design document orders it, and skips claimed cells. That
    /// keeps both rules true at once: the document's pass order, and the research note's
    /// "templates override strata" — a basement stays a basement.
    /// </summary>
    public sealed class StrataPass : IWorldGenPass
    {
        public int Order => 6;
        public string Name => "Strata";

        const int BoundaryPeriod = 24;
        const int BoundaryOctaves = 2;
        const int TunnelPeriod = 18;
        const int SeamPeriod = 20;
        const int CavePeriod = 12;

        public void Run(WorldGenContext ctx)
        {
            var gen = ctx.Gen;
            int ground = ctx.GroundLayer;
            if (ground == 0) return;

            ChooseMetroLines(ctx);

            uint soilSeed = ctx.Seed ^ 0x2C9E17B5u;
            uint rockSeed = ctx.Seed ^ 0x7F4A3311u;
            uint tunnelSeed = ctx.Seed ^ 0x3D18C0EBu;
            uint seamSeed = ctx.Seed ^ 0x64B1D7A3u;
            uint caveSeed = ctx.Seed ^ 0x19AE5C77u;

            // Per column, not per cell: the two boundaries are columnar by definition, and
            // evaluating the noise once per column rather than once per cell is the difference
            // between a cheap pass and an expensive one at the scale target.
            var soilDepth = new int[ctx.Columns];
            var rockDepth = new int[ctx.Columns];
            for (int z = 0; z < ctx.Size.SizeZ; z++)
            for (int x = 0; x < ctx.Size.SizeX; x++)
            {
                int column = ctx.Column(x, z);
                soilDepth[column] = ValueNoise.Band(
                    ValueNoise.Fractal2D(soilSeed, x, z, BoundaryPeriod, BoundaryOctaves),
                    gen.minSoilDepth, gen.maxSoilDepth);
                rockDepth[column] = ValueNoise.Band(
                    ValueNoise.Fractal2D(rockSeed, x, z, BoundaryPeriod, BoundaryOctaves),
                    gen.minRockDepth, gen.maxRockDepth);
            }

            for (int y = ground - 1; y >= 0; y--)
            {
                int depth = ground - y;
                for (int z = 0; z < ctx.Size.SizeZ; z++)
                for (int x = 0; x < ctx.Size.SizeX; x++)
                {
                    int column = ctx.Column(x, z);
                    int index = ctx.Index(x, z, y);
                    if (ctx.IsClaimed(index)) continue;

                    if (depth == 1)
                    {
                        // Service stratum: solid fill, cut by utility tunnels that follow the
                        // streets above, because that is where a city puts its services. Never
                        // where the metro already runs one layer down: two engineered voids
                        // stacked with nothing solid between them leaves the upper one's floor
                        // resting on the lower one's floor, which SupportConsistencyCheck rightly
                        // calls a collapse — the metro's own tube is deep enough infrastructure
                        // that a utility line would not be re-dug directly above it anyway.
                        bool tunnel = ctx.IsStreet[column] && !OnMetro(ctx, x, z) &&
                                      ValueNoise.Fractal2D(tunnelSeed, x, z, TunnelPeriod, 2) > gen.tunnelThreshold;
                        if (tunnel)
                        {
                            ctx.SetTerrain(index, CoreContent.TerrainAir);
                            // An engineered void has an engineered floor. Without this a tunnel on
                            // the bottom map layer has nothing beneath it to stand on, and the
                            // slice map — where the service stratum *is* the bottom layer — would
                            // generate tunnels no colonist could ever enter.
                            ctx.SetSlab(index, CoreContent.SlabStructural, CoreContent.StuffConcrete);

                            // The tunnel's ceiling is the road deck above it. Without this the
                            // street cell over a tunnel has neither a slab nor solid ground below
                            // and becomes an unannounced hole in the middle of a main road.
                            int above = index + ctx.Size.LayerStride;
                            if (!ctx.IsClaimed(above) && ctx.Grid.Floor[above] == CoreContent.SlabNone)
                                ctx.SetSlab(above, CoreContent.SlabStructural, CoreContent.StuffConcrete);

                            ctx.Report.TunnelCells++;
                        }
                        else
                        {
                            ctx.SetTerrain(index, CoreContent.TerrainFill);
                            ctx.Report.SolidCells++;
                        }
                        continue;
                    }

                    if (depth <= 3)
                    {
                        if (depth == 2 && OnMetro(ctx, x, z))
                        {
                            ctx.SetTerrain(index, CoreContent.TerrainAir);
                            ctx.SetSlab(index, CoreContent.SlabStructural, CoreContent.StuffConcrete);
                            ctx.Report.MetroCells++;
                            continue;
                        }
                        bool seam = ValueNoise.Fractal2D(seamSeed, x, z, SeamPeriod, 2) > gen.seamThreshold;
                        ctx.SetTerrain(index, seam ? CoreContent.TerrainBuriedSeam : CoreContent.TerrainFill);
                        if (seam) ctx.Report.SeamCells++;
                        ctx.Report.SolidCells++;
                        continue;
                    }

                    if (depth <= soilDepth[column])
                    {
                        ctx.SetTerrain(index, CoreContent.TerrainFill);
                        ctx.Report.SolidCells++;
                        continue;
                    }

                    if (depth <= rockDepth[column])
                    {
                        // Soil and gravel are not solid: this is the band a colony can dig
                        // through cheaply, and the first place anything will grow.
                        ctx.SetTerrain(index, ((depth + column) & 3) == 0
                            ? CoreContent.TerrainGravel
                            : CoreContent.TerrainSoil);
                        continue;
                    }

                    // No caves on the bottom map layer: a void there has nothing beneath it, so it
                    // would be a hole a colonist can fall into and never stand in.
                    if (y > 0 && ValueNoise.Fractal3D(caveSeed, x, z, y, CavePeriod, 2) > gen.caveThreshold)
                    {
                        ctx.SetTerrain(index, CoreContent.TerrainAir);
                        ctx.Report.CaveCells++;
                        continue;
                    }

                    ctx.SetTerrain(index, CoreContent.TerrainRock);
                    ctx.Report.SolidCells++;
                }
            }
        }

        /// <summary>The metro runs under the street line nearest the middle of the map, on each axis.</summary>
        static void ChooseMetroLines(WorldGenContext ctx)
        {
            ctx.MetroLineX = NearestLine(ctx.IsStreetX);
            ctx.MetroLineZ = NearestLine(ctx.IsStreetZ);
        }

        static int NearestLine(bool[] mask)
        {
            int centre = mask.Length / 2;
            int best = -1, bestDistance = int.MaxValue;
            for (int i = 0; i < mask.Length; i++)
            {
                if (!mask[i]) continue;
                int distance = i > centre ? i - centre : centre - i;
                if (distance < bestDistance) { bestDistance = distance; best = i; }
            }
            return best;
        }

        static bool OnMetro(WorldGenContext ctx, int x, int z)
        {
            int half = ctx.Gen.metroHalfWidth;
            if (ctx.MetroLineX >= 0 && Math.Abs(x - ctx.MetroLineX) <= half) return true;
            if (ctx.MetroLineZ >= 0 && Math.Abs(z - ctx.MetroLineZ) <= half) return true;
            return false;
        }
    }

    /// <summary>
    /// Pass 7 — salvage deposits.
    ///
    /// Blobs grown by a bounded random walk, in the shape RimWorld scatters ore, but weighted
    /// toward the buried-city seam at L-2 and L-3 rather than toward rock: a colony expands by
    /// emptying the city, not by tunnelling into a mountain, and the generator has to make that
    /// economically true.
    /// </summary>
    public sealed class SalvagePass : IWorldGenPass
    {
        public int Order => 7;
        public string Name => "Salvage";

        public void Run(WorldGenContext ctx)
        {
            var gen = ctx.Gen;
            int count = ctx.Columns * gen.salvageDepositsPer10000Columns / 10000;
            if (count < 1) count = 1;

            for (int i = 0; i < count; i++)
            {
                var rng = ctx.Random(WorldGenPurpose.Salvage, i);
                int y = ChooseLayer(ctx, ref rng);
                if (y < 0) continue;

                int x = rng.NextInt(ctx.Size.SizeX);
                int z = rng.NextInt(ctx.Size.SizeZ);
                int target = rng.NextInt(gen.minSalvageBlob, gen.maxSalvageBlob + 1);
                int placed = GrowBlob(ctx, ref rng, x, z, y, target);
                if (placed > 0)
                {
                    ctx.SalvageDeposits.Add(new SalvageDeposit(ctx.Index(x, z, y), placed));
                    ctx.Report.SalvageCells += placed;
                }
            }
            ctx.Report.SalvageDeposits = ctx.SalvageDeposits.Count;
        }

        /// <summary>
        /// Depth weighting, as percentages: half in the seam, the rest spread over the service
        /// stratum, the surface rubble and the deeper fill.
        /// </summary>
        static int ChooseLayer(WorldGenContext ctx, ref DeterministicRandom rng)
        {
            int ground = ctx.GroundLayer;
            int roll = rng.NextInt(100);
            int depth;
            if (roll < 50) depth = 2 + rng.NextInt(2);           // buried-city seam, L-2 and L-3
            else if (roll < 70) depth = 1;                        // service stratum
            else if (roll < 82) depth = 0;                        // street rubble
            else depth = 4 + rng.NextInt(4);                      // deeper fill and soil

            if (depth > ground) depth = ground;
            int y = ground - depth;
            return y >= 0 && y < ctx.Size.SizeY ? y : -1;
        }

        /// <summary>
        /// A bounded random walk within one layer. Bounded because an unbounded walk is the one
        /// worldgen construct that can turn a fast pass into a slow one on an unlucky seed.
        /// </summary>
        static int GrowBlob(WorldGenContext ctx, ref DeterministicRandom rng, int x, int z, int y, int target)
        {
            int placed = 0;
            int steps = target * 4;
            for (int s = 0; s < steps && placed < target; s++)
            {
                if (ctx.Size.Contains(x, z, y))
                {
                    int index = ctx.Index(x, z, y);
                    ushort terrain = ctx.Grid.Terrain[index];
                    if (!ctx.IsClaimed(index) &&
                        terrain != CoreContent.TerrainSalvage &&
                        CoreContent.TerrainAt(terrain).salvageWeight > 0)
                    {
                        ctx.SetTerrain(index, CoreContent.TerrainSalvage);
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
    /// Pass 8 — utility taps.
    ///
    /// A handful of still-live connection points, spaced apart, standing in for RimWorld's steam
    /// geysers: they are the early power source, and there are deliberately few of them so that
    /// where the colony settles is a decision rather than a shrug.
    /// </summary>
    public sealed class UtilityTapPass : IWorldGenPass
    {
        public int Order => 8;
        public string Name => "UtilityTaps";

        public void Run(WorldGenContext ctx)
        {
            var gen = ctx.Gen;
            int wanted = ctx.Columns * gen.utilityTapsPer10000Columns / 10000;
            if (wanted < gen.minUtilityTaps) wanted = gen.minUtilityTaps;

            var rng = ctx.Random(WorldGenPurpose.UtilityTaps);
            int attempts = wanted * 60;
            int spacingSquared = gen.utilityTapSpacing * gen.utilityTapSpacing;

            for (int a = 0; a < attempts && ctx.UtilityTaps.Count < wanted; a++)
            {
                int x = rng.NextInt(ctx.Size.SizeX);
                int z = rng.NextInt(ctx.Size.SizeZ);

                // A tap sits in the service stratum where there is a tunnel, otherwise at street
                // level: a live main is a buried thing that surfaces at an access point. Both are
                // tried, so a map whose service stratum is solid still gets its taps.
                int index = -1;
                if (ctx.GroundLayer > 0 && Suitable(ctx, ctx.Index(x, z, ctx.GroundLayer - 1)))
                    index = ctx.Index(x, z, ctx.GroundLayer - 1);
                else if (Suitable(ctx, ctx.Index(x, z, ctx.GroundLayer)))
                    index = ctx.Index(x, z, ctx.GroundLayer);

                if (index < 0) continue;
                if (TooClose(ctx, x, z, spacingSquared)) continue;

                ctx.PlaceEdifice(index, CoreContent.EdificeUtilityTap, CoreContent.StuffSteel, false);
                ctx.UtilityTaps.Add(new UtilityTap(index));
            }
            ctx.Report.UtilityTaps = ctx.UtilityTaps.Count;
        }

        static bool Suitable(WorldGenContext ctx, int index) =>
            !ctx.IsClaimed(index) && ctx.Grid.Edifice[index] < 0 && ctx.Grid.IsWalkable(index);

        static bool TooClose(WorldGenContext ctx, int x, int z, int spacingSquared)
        {
            for (int i = 0; i < ctx.UtilityTaps.Count; i++)
            {
                var cell = ctx.Size.FromIndex(ctx.UtilityTaps[i].CellIndex);
                int dx = cell.X - x, dz = cell.Z - z;
                if (dx * dx + dz * dz < spacingSquared) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Pass 9 — sealed vaults.
    ///
    /// The ancient-danger analogue: a sealed room with no door, buried in the strata, generated
    /// but **inert** in the slice (section 6, pass 9). It exists now so that the map a save was
    /// taken on does not change shape when the contents arrive; breaching one currently yields an
    /// empty room, which is a disappointment rather than a bug.
    /// </summary>
    public sealed class SealedVaultPass : IWorldGenPass
    {
        public int Order => 9;
        public string Name => "SealedVaults";

        public void Run(WorldGenContext ctx)
        {
            var gen = ctx.Gen;
            int wanted = ctx.Columns * gen.vaultsPer10000Columns / 10000;
            if (wanted < gen.minVaults) wanted = gen.minVaults;
            if (ctx.GroundLayer < 1) return;

            var rng = ctx.Random(WorldGenPurpose.Vaults);
            int size = gen.vaultSize;
            int attempts = wanted * 40;

            for (int a = 0; a < attempts && ctx.SealedVaults.Count < wanted; a++)
            {
                int depth = 2 + rng.NextInt(3);
                if (depth > ctx.GroundLayer) depth = ctx.GroundLayer;
                int y = ctx.GroundLayer - depth;
                if (y < 0) continue;

                int x0 = rng.NextInt(1, Math.Max(2, ctx.Size.SizeX - size));
                int z0 = rng.NextInt(1, Math.Max(2, ctx.Size.SizeZ - size));
                if (x0 + size > ctx.Size.SizeX || z0 + size > ctx.Size.SizeZ) continue;
                if (!IsClear(ctx, x0, z0, y, size)) continue;

                Carve(ctx, x0, z0, y, size);
                ctx.SealedVaults.Add(new SealedVault(x0, z0, y, size, size));
            }
            ctx.Report.SealedVaults = ctx.SealedVaults.Count;
        }

        static bool IsClear(WorldGenContext ctx, int x0, int z0, int y, int size)
        {
            for (int z = z0; z < z0 + size; z++)
            for (int x = x0; x < x0 + size; x++)
                if (ctx.IsClaimed(ctx.Index(x, z, y))) return false;
            return true;
        }

        static void Carve(WorldGenContext ctx, int x0, int z0, int y, int size)
        {
            for (int z = z0; z < z0 + size; z++)
            for (int x = x0; x < x0 + size; x++)
            {
                int index = ctx.Index(x, z, y);
                ctx.Claim(index);
                ctx.SetTerrain(index, CoreContent.TerrainAir);
                ctx.SetSlab(index, CoreContent.SlabStructural, CoreContent.StuffComposite);

                bool perimeter = x == x0 || z == z0 || x == x0 + size - 1 || z == z0 + size - 1;
                if (perimeter)
                    ctx.PlaceEdifice(index, CoreContent.EdificeVaultWall, CoreContent.StuffComposite, true);
            }
        }
    }

    /// <summary>
    /// Pass 10 — start location and the consistency assertion.
    ///
    /// The start is the walkable street cell nearest the middle of the map: streets are the
    /// circulation, so starting on one guarantees the colony can reach something.
    ///
    /// The pass then asserts the map is internally consistent and, via
    /// <see cref="IStructuralConsistencyCheck"/>, runs the full support solve: does every stamped
    /// shell stand up with no credit for having been stamped that way?
    /// <see cref="WorldGenerator.CreatePasses"/> supplies <see cref="World.SupportConsistencyCheck"/>
    /// by default; a caller only ever passes its own to substitute a test probe.
    /// </summary>
    public sealed class StartPass : IWorldGenPass
    {
        readonly IStructuralConsistencyCheck? _structuralCheck;

        public StartPass(IStructuralConsistencyCheck? structuralCheck = null)
        {
            _structuralCheck = structuralCheck;
        }

        public int Order => 10;
        public string Name => "Start";

        public void Run(WorldGenContext ctx)
        {
            ctx.Report.StartCell = ChooseStart(ctx);
            AssertConsistent(ctx);

            // Always non-null via WorldGenerator.CreatePasses; the check is only ever skipped by
            // a test that constructs this pass directly.
            if (_structuralCheck != null)
            {
                _structuralCheck.Verify(ctx.Grid, ctx);
                ctx.Report.StructuralCheckRan = true;
            }

            // TODO(items): "scatter starting resources" is the other half of this pass. It needs
            // the thing registry, which does not exist yet; nothing about the cell grid changes
            // when it arrives.
        }

        static CellRef ChooseStart(WorldGenContext ctx)
        {
            int y = ctx.GroundLayer;
            int centreX = ctx.Size.SizeX / 2, centreZ = ctx.Size.SizeZ / 2;
            int best = -1, bestScore = int.MaxValue;

            for (int z = 0; z < ctx.Size.SizeZ; z++)
            for (int x = 0; x < ctx.Size.SizeX; x++)
            {
                int column = ctx.Column(x, z);
                if (!ctx.IsStreet[column]) continue;
                int index = ctx.Index(x, z, y);
                if (ctx.IsClaimed(index)) continue;
                if (!ctx.Grid.IsWalkable(index)) continue;

                int score = Math.Abs(x - centreX) + Math.Abs(z - centreZ);
                if (score < bestScore) { bestScore = score; best = index; }
            }

            if (best < 0)
                throw new WorldGenException(
                    "No walkable street cell to start on. The street or strata pass produced an unusable map.");

            return ctx.Size.FromIndex(best);
        }

        /// <summary>
        /// Cheap invariants that a generation bug would break long before a player noticed. These
        /// are not the support solve; they are the things that must hold for the support solve to
        /// even be meaningful.
        /// </summary>
        static void AssertConsistent(WorldGenContext ctx)
        {
            var grid = ctx.Grid;
            for (int i = 0; i < grid.Terrain.Length; i++)
            {
                if (grid.Terrain[i] >= CoreContent.TerrainCount)
                    throw new WorldGenException($"Cell {ctx.Size.FromIndex(i)} has unknown terrain {grid.Terrain[i]}.");

                bool solidFlag = (grid.Flags[i] & CellFlags.SolidTerrain) != 0;
                if (solidFlag != CoreContent.IsSolid(grid.Terrain[i]))
                    throw new WorldGenException(
                        $"Cell {ctx.Size.FromIndex(i)} has a solid flag that disagrees with its terrain.");

                int handle = grid.Edifice[i];
                if (handle < -1 || handle >= ctx.Edifices.Count)
                    throw new WorldGenException($"Cell {ctx.Size.FromIndex(i)} has edifice handle {handle}, out of range.");
                if (handle >= 0)
                {
                    var placed = ctx.Edifices[handle];
                    if (placed.CellIndex != i)
                        throw new WorldGenException($"Edifice {handle} is filed under cell {i} but records cell {placed.CellIndex}.");
                    if (solidFlag)
                        throw new WorldGenException($"Cell {ctx.Size.FromIndex(i)} holds an edifice inside solid terrain.");
                }
            }
        }
    }
}
