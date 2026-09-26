#nullable enable
using System;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Worldgen.Natural
{
    /// <summary>
    /// Pass 6 — rock outcrops.
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
        public int Order => 6;
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

                // A mound rising out of a river would be rock standing in the water, and would
                // break the fill pass's assumption that a water column's top solid cell is its
                // bed. Rejecting on the centre rather than per cell keeps a mound from becoming
                // a ring around a pond.
                if (ctx.Water[ctx.Column(cx, cz)] != (byte)WaterClass.None) continue;

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
                if (ctx.Water[column] != (byte)WaterClass.None) continue;

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
            if (NaturalContent.IsHostRock(was)) return;

            if (was == NaturalContent.TerrainGrass) ctx.Report.GrassCells--;
            else if (was == NaturalContent.TerrainBareEarth) ctx.Report.BareEarthCells--;
            else if (was == NaturalContent.TerrainPackedGravel) ctx.Report.GravelCells--;
            else if (was == NaturalContent.TerrainSand) ctx.Report.SandCells--;

            ctx.SetTerrain(index, NaturalContent.TerrainRock);
            ctx.Report.RockCells++;
        }
    }

    /// <summary>
    /// Pass 8 — caverns.
    ///
    /// Sealed chambers in the rock (design 62 §5d). There is no entrance: nothing connects a
    /// chamber to the surface, and the only way in is to mine into one, which is what makes
    /// digging downward exploration rather than bookkeeping (<c>docs/research/mining-interview.md</c>,
    /// answer 7).
    ///
    /// <para><b>Round, not stringy.</b> A chamber is an ellipse in plan with a jittered edge, a
    /// flat floor and a domed roof: full height in the middle, a layer lower towards the edge, two
    /// lower at the rim. It replaced a bounded random walk, which at the 300-1,500 cells the deep
    /// bands ask for came out as a tangle of one-cell corridors.</para>
    ///
    /// <para><b>Two depth bands.</b> Even-numbered chambers are drawn in the upper band, odd ones in
    /// the lower (<see cref="NaturalMapGenDef.upperCavernMinDepth"/> and after), the floor's depth
    /// drawn anywhere in the band rather than always mid-rock. Where a column's rock does not reach
    /// the band the floor is clamped to the lowest layer it may hollow, so a 16-layer board's
    /// chambers sit at the bottom of its rock, lower and smaller, and none is dropped for it.</para>
    ///
    /// <para><b>Every chamber keeps a rock shell.</b> A cell is carved only where the terrain is
    /// host rock and the cell sits strictly inside its own column's rock band, so a chamber can
    /// neither undermine the subsoil holding the surface up nor breach the bedrock. See
    /// <see cref="CanHollow"/> for why the band, and not the neighbouring terrain, is what it
    /// reads.</para>
    ///
    /// <para><b>And every ceiling is held.</b> A chamber is planned whole, then pillars are left
    /// standing — a whole column of it un-carved — until every column of it is within
    /// <see cref="RockSpan.Cells"/> steps of a column with no hole in it from the bottom of the
    /// world to above the chamber's top. That is stricter than <see cref="RockSpan"/>'s own check,
    /// so the cave-in unit's first solve finds nothing to bring down.</para>
    ///
    /// <para>This runs <em>before</em> the ore pass so that ore can be hung on chamber walls, and
    /// after the outcrops, which only ever add rock above ground.</para>
    /// </summary>
    public sealed class CavernPass : INaturalGenPass
    {
        public int Order => 8;
        public string Name => "Caverns";

        public void Run(NaturalGenContext ctx)
        {
            var gen = ctx.Gen;
            int count = ctx.Columns * gen.cavernsPer10000Columns / 10000;
            // A rate of zero means zero. The floor exists so rounding on a small map cannot
            // silently wipe the feature out; it is not a licence to overrule a map that asked
            // for no caverns at all.
            if (count < 1) count = gen.cavernsPer10000Columns > 0 ? 1 : 0;
            if (count == 0) { ctx.Report.Caverns = 0; return; }

            var plan = new Plan(ctx.Size);
            uint edgeSeed = ctx.Seed ^ 0x6C1A7E29u;

            for (int i = 0; i < count; i++)
            {
                var rng = ctx.Random(NaturalGenPurpose.Caverns, i);
                int x = rng.NextInt(ctx.Size.SizeX);
                int z = rng.NextInt(ctx.Size.SizeZ);
                bool upper = i % 2 == 0;
                int depth = upper
                    ? rng.NextInt(gen.upperCavernMinDepth, gen.upperCavernMaxDepth + 1)
                    : rng.NextInt(gen.lowerCavernMinDepth, gen.lowerCavernMaxDepth + 1);
                int height = rng.NextInt(gen.minCavernHeight, gen.maxCavernHeight + 1);
                int target = rng.NextInt(gen.minCavernCells, gen.maxCavernCells + 1);
                int stretch = rng.NextInt(80, 126);   // per cent: the second axis against the first
                bool alongX = rng.NextInt(2) == 0;

                int column = ctx.Column(x, z);
                int lowest = ctx.BedrockTopY[column] + 1;
                int highest = ctx.SubsoilBaseY[column] - 2;
                if (highest < lowest) continue;         // no rock band thick enough to hollow out

                int floor = ctx.SurfaceY[column] - depth;
                int ceilingRoom = Math.Max(lowest, highest - height + 1);
                if (floor > ceilingRoom) floor = ceilingRoom;
                if (floor < lowest) floor = lowest;

                // The area that gives the target at about three fifths of the full height, since the
                // dome is lower at its rim and the jittered edge and the pillars take their share:
                // r^2 = target / (0.6 * pi * height), measured against the census.
                int r2 = target * 100 / (188 * height);
                int radius = 3;
                while ((radius + 1) * (radius + 1) <= r2) radius++;
                int rx = radius, rz = Math.Max(2, radius * stretch / 100);
                if (!alongX) { int t = rx; rx = rz; rz = t; }

                plan.Begin();
                Lay(ctx, plan, edgeSeed + (uint)i * 7919u, x, z, floor, height, rx, rz);
                if (plan.Count == 0) continue;
                ctx.Report.CavernPillars += Prop(ctx, plan, floor + height);

                int carved = 0;
                for (int c = 0; c < plan.Count; c++)
                {
                    int cell = plan.CellAt(c);
                    if (!plan.Holds(cell)) continue;    // un-planned for a pillar
                    CellRef at = ctx.Size.FromIndex(cell);
                    if (at.Y < ctx.DeepStoneTopY[ctx.Column(at.X, at.Z)]) ctx.Report.DeepStoneCells--;
                    else ctx.Report.RockCells--;
                    ctx.Carve(cell);
                    // Nobody has seen inside (design 62 §6): drawn and described as rock until a
                    // cut breaks in. The simulation's truth is the air Carve just wrote.
                    ctx.Grid.Unseen.Add(cell);
                    carved++;
                }
                if (carved == 0) continue;

                ctx.Caverns.Add(new CavernChamber(ctx.Index(x, z, floor), carved));
                ctx.Report.CavernCells += carved;
                ctx.Report.SolidCells -= carved;
                ctx.Report.AirCells += carved;
            }

            ctx.Report.Caverns = ctx.Caverns.Count;
        }

        /// <summary>
        /// Plan the chamber's cells: an ellipse in plan with a jittered edge, flat-floored, its
        /// roof a dome of up to <paramref name="height"/> layers.
        /// </summary>
        static void Lay(NaturalGenContext ctx, Plan plan, uint edgeSeed, int cx, int cz, int floor,
                        int height, int rx, int rz)
        {
            int reachX = rx + rx * 3 / 10 + 1, reachZ = rz + rz * 3 / 10 + 1;
            long rx2 = (long)rx * rx, rz2 = (long)rz * rz;

            for (int z = cz - reachZ; z <= cz + reachZ; z++)
            for (int x = cx - reachX; x <= cx + reachX; x++)
            {
                if ((uint)x >= (uint)ctx.Size.SizeX || (uint)z >= (uint)ctx.Size.SizeZ) continue;
                int dx = x - cx, dz = z - cz;

                // Per mille of the way to the rim, against an edge that wanders ±30 % by a value
                // noise, so the rim is a cave's and not a compass's.
                long q = dx * dx * 1000L / rx2 + dz * dz * 1000L / rz2;
                int edge = 1000 + (ValueNoise.Value2D(edgeSeed, x, z, 5) - ValueNoise.Scale / 2) * 600 / ValueNoise.Scale;
                if (q > edge) continue;

                int tall = q * 100 <= edge * 40L ? height
                         : q * 100 <= edge * 75L ? height - 1
                         : height - 2;
                if (tall < 1) tall = 1;

                for (int y = floor; y < floor + tall; y++)
                    if (ctx.Size.Contains(x, z, y) && CanHollow(ctx, x, z, y))
                        plan.Add(ctx.Index(x, z, y));
            }
        }

        /// <summary>
        /// Leave pillars until every planned column — and every column of an earlier chamber near
        /// enough for this one to have taken its support — is within <see cref="RockSpan.Cells"/>
        /// steps of a supporting column: one with no hole planned or carved in it, solid from layer
        /// 0 to above the highest ceiling in the box. A multi-source breadth-first search over the
        /// box's columns; then the farthest planned column stands (or, where the farthest is an
        /// earlier chamber's, the planned column nearest it), and again, until none is too far.
        /// Every round un-plans a column, so it ends. Returns the pillars left.
        ///
        /// <para>The earlier chambers are the case that needs care: a second chamber carved beside
        /// a first can hollow the very columns the first was propped by. Only an earlier column
        /// within the span of this plan can have lost a support to it, and that column's own
        /// supports lie within the span of it, so the box reaches twice the span past the plan.</para>
        /// </summary>
        static int Prop(NaturalGenContext ctx, Plan plan, int top)
        {
            var size = ctx.Size;
            int span = RockSpan.Cells;
            int margin = 2 * span + 1;
            int x0 = Math.Max(0, plan.MinX - margin), x1 = Math.Min(size.SizeX - 1, plan.MaxX + margin);
            int z0 = Math.Max(0, plan.MinZ - margin), z1 = Math.Min(size.SizeZ - 1, plan.MaxZ + margin);
            int w = x1 - x0 + 1, h = z1 - z0 + 1, n = w * h;

            var planned = new bool[n];
            for (int c = 0; c < plan.Count; c++)
            {
                int cell = plan.CellAt(c);
                if (!plan.Holds(cell)) continue;
                CellRef at = size.FromIndex(cell);
                planned[(at.Z - z0) * w + (at.X - x0)] = true;
            }

            var distance = new int[n];
            var queue = new int[n];

            // Earlier chambers' columns within the span of this plan, and how high they reach.
            Spread(planned, distance, queue, w, h);
            var earlier = new bool[n];
            if (ctx.CavernCells.Count > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    if (distance[i] > span) continue;
                    int x = x0 + i % w, z = z0 + i / w;
                    for (int y = 0; y < size.SizeY; y++)
                    {
                        if (!ctx.IsCavern(size.Index(x, z, y))) continue;
                        earlier[i] = true;
                        if (y + 1 > top) top = y + 1;
                    }
                }
            }
            if (top >= size.SizeY) top = size.SizeY - 1;

            var support = new bool[n];
            for (int i = 0; i < n; i++)
                support[i] = !planned[i] && !earlier[i] &&
                    RockSpan.SupportsTo(size, ctx.Grid.IsSolidTerrain, x0 + i % w, z0 + i / w, top);

            int pillars = 0;
            while (true)
            {
                Spread(support, distance, queue, w, h);

                int worst = -1, worstDistance = span;
                for (int i = 0; i < n; i++)
                    if ((planned[i] || earlier[i]) && distance[i] > worstDistance) { worst = i; worstDistance = distance[i]; }
                if (worst < 0) return pillars;

                int stand = worst;
                if (!planned[worst])
                {
                    // An earlier chamber's column: stand the planned column nearest it.
                    stand = -1;
                    int best = int.MaxValue, wx = worst % w, wz = worst / w;
                    for (int i = 0; i < n; i++)
                    {
                        if (!planned[i]) continue;
                        int d = Math.Abs(i % w - wx) + Math.Abs(i / w - wz);
                        if (d < best) { best = d; stand = i; }
                    }
                    if (stand < 0) return pillars;   // nothing of this plan left to take back
                }

                int px = x0 + stand % w, pz = z0 + stand / w;
                plan.RemoveColumn(px, pz);
                planned[stand] = false;
                support[stand] = !earlier[stand] && RockSpan.SupportsTo(size, ctx.Grid.IsSolidTerrain, px, pz, top);
                pillars++;
            }
        }

        /// <summary>Four-neighbour steps from the nearest set column of <paramref name="from"/>, over a w x h box.</summary>
        static void Spread(bool[] from, int[] distance, int[] queue, int w, int h)
        {
            int head = 0, tail = 0;
            for (int i = 0; i < distance.Length; i++)
            {
                if (from[i]) { distance[i] = 0; queue[tail++] = i; }
                else distance[i] = int.MaxValue;
            }
            while (head < tail)
            {
                int i = queue[head++];
                int bx = i % w, bz = i / w, next = distance[i] + 1;
                if (bx > 0 && distance[i - 1] > next) { distance[i - 1] = next; queue[tail++] = i - 1; }
                if (bx < w - 1 && distance[i + 1] > next) { distance[i + 1] = next; queue[tail++] = i + 1; }
                if (bz > 0 && distance[i - w] > next) { distance[i - w] = next; queue[tail++] = i - w; }
                if (bz < h - 1 && distance[i + w] > next) { distance[i + w] = next; queue[tail++] = i + w; }
            }
        }

        /// <summary>
        /// Host rock here (rock or deep stone), and strictly inside this column's rock band so that a layer of rock is
        /// left under the subsoil and over the bedrock.
        ///
        /// <para>The band is read from the column's own stratum boundaries rather than from the
        /// terrain above and below, and that distinction is the whole method: asking "is the cell
        /// above still rock?" makes a chamber one cell tall for ever, because the cell above the
        /// one just carved is now air and refuses its own carve. The boundaries are what the
        /// strata pass laid down and they do not move.</para>
        ///
        /// <para>It is asked per cell, because the rock band is a different thickness under every
        /// terrace: a chamber comfortably buried at its centre can be one step from the subsoil at
        /// its edge.</para>
        /// </summary>
        static bool CanHollow(NaturalGenContext ctx, int x, int z, int y)
        {
            int column = ctx.Column(x, z);
            if (y < ctx.BedrockTopY[column] + 1) return false;
            if (y > ctx.SubsoilBaseY[column] - 2) return false;
            return NaturalContent.IsHostRock(ctx.Grid.Terrain[ctx.Index(x, z, y)]);
        }

        /// <summary>
        /// One chamber's planned cells, in the order they were laid, with a board-sized flag so a
        /// pillar can take a column back. The flag array is allocated once per pass and cleared
        /// cell by cell, never whole.
        /// </summary>
        sealed class Plan
        {
            readonly GridSize _size;
            readonly bool[] _planned;
            readonly System.Collections.Generic.List<int> _cells = new System.Collections.Generic.List<int>();

            public Plan(GridSize size) { _size = size; _planned = new bool[size.CellCount]; }

            public int Count => _cells.Count;
            public int MinX, MaxX, MinZ, MaxZ;

            public void Begin()
            {
                for (int i = 0; i < _cells.Count; i++) _planned[_cells[i]] = false;
                _cells.Clear();
                MinX = MinZ = int.MaxValue;
                MaxX = MaxZ = int.MinValue;
            }

            public void Add(int cell)
            {
                if (_planned[cell]) return;
                _planned[cell] = true;
                _cells.Add(cell);
                CellRef at = _size.FromIndex(cell);
                if (at.X < MinX) MinX = at.X;
                if (at.X > MaxX) MaxX = at.X;
                if (at.Z < MinZ) MinZ = at.Z;
                if (at.Z > MaxZ) MaxZ = at.Z;
            }

            public int CellAt(int i) => _cells[i];
            public bool Holds(int cell) => _planned[cell];

            public void RemoveColumn(int x, int z)
            {
                for (int y = 0; y < _size.SizeY; y++) _planned[_size.Index(x, z, y)] = false;
            }
        }
    }

    /// <summary>
    /// Pass 9 — ore deposits (design 62 §5c).
    ///
    /// <para>Every kind in <c>Ores.xml</c> is placed on its own: so many deposits per ten thousand
    /// columns, each in the kind's band of depths below the local surface, in the kind's shape,
    /// of the kind's size. The band is clamped into the column's rock where the column does not
    /// reach it, so a shallow board still has every kind, squeezed to the bottom of its rock.
    /// A share of deposits is drawn anywhere in the rock instead, and a share of gold and gems is
    /// hung on a cavern wall inside the band, so opening a cave pays.</para>
    ///
    /// <para><b>A deposit only ever replaces rock-like cells</b> (<see cref="NaturalContent.IsHostRock"/>),
    /// so ore is never found in soil, subsoil, bedrock or the open, and never over another ore —
    /// mining it always means digging into stone.</para>
    ///
    /// <para><b>Each deposit draws its whole stream first</b> — column, band roll, size, wall roll
    /// and the rest — from its own stream keyed on (kind, deposit), before it looks at the map.
    /// So switching caverns off moves no deposit's draw, and appending a kind moves nothing that
    /// was there before it.</para>
    /// </summary>
    public sealed class OrePass : INaturalGenPass
    {
        // Nine, not eight: the water passes took two slots on main and caverns took one here.
        public int Order => 9;
        public string Name => "Ore";

        public void Run(NaturalGenContext ctx)
        {
            var gen = ctx.Gen;
            var ores = NaturalContent.Ores;
            var placed = new System.Collections.Generic.List<int>(64);
            var candidates = new System.Collections.Generic.List<(long, int)>();

            for (int kind = 0; kind < ores.Count; kind++)
            {
                var ore = ores[kind];
                long wanted = (long)ctx.Columns * ore.DepositsPer10000Columns * gen.oreAbundancePerMille;
                int count = (int)((wanted + 5_000_000L) / 10_000_000L);
                // A rate of zero means zero, and so does a board with no ore; otherwise rounding
                // on a small map does not wipe a kind out.
                if (count < 1) count = ore.DepositsPer10000Columns > 0 && gen.oreAbundancePerMille > 0 ? 1 : 0;

                for (int j = 0; j < count; j++)
                {
                    var rng = ctx.Random(NaturalGenPurpose.Ore, (kind << 16) | j);
                    int x = rng.NextInt(ctx.Size.SizeX);
                    int z = rng.NextInt(ctx.Size.SizeZ);
                    bool offBand = rng.NextInt(1000) < ore.OffBandPerMille;
                    int depthRoll = rng.NextInt(1 << 20);
                    int target = rng.NextInt(ore.MinCells, ore.MaxCells + 1);
                    bool toWall = rng.NextInt(1000) < ore.CaveWallPerMille;
                    int wallRoll = rng.NextInt(1 << 30);

                    int y;
                    if (toWall && WallAnchor(ctx, ore, wallRoll, out int wx, out int wz, out int wy))
                    {
                        x = wx;
                        z = wz;
                        y = wy;
                    }
                    else
                    {
                        int column = ctx.Column(x, z);
                        if (!DepthIn(ctx, ore, column, offBand, depthRoll, out y)) continue;
                    }

                    placed.Clear();
                    Grow(ctx, ref rng, ore, ref x, ref z, ref y, target, placed, candidates);
                    if (placed.Count == 0) continue;

                    bool wall = false;
                    for (int c = 0; c < placed.Count && !wall; c++) wall = TouchesCavern(ctx, placed[c]);

                    ctx.OreDeposits.Add(new OreDeposit(ctx.Index(x, z, y), placed.Count, kind, offBand, wall));
                    ctx.Report.OreCells += placed.Count;
                    ctx.Report.OreCellsByKind[kind] += placed.Count;
                    for (int c = 0; c < placed.Count; c++)
                    {
                        CellRef at = ctx.Size.FromIndex(placed[c]);
                        if (at.Y < ctx.DeepStoneTopY[ctx.Column(at.X, at.Z)]) ctx.Report.DeepStoneCells--;
                        else ctx.Report.RockCells--;
                    }
                }
            }

            ctx.Report.OreDeposits = ctx.OreDeposits.Count;
        }

        /// <summary>
        /// The layer a deposit starts at in a column: its band, clamped into the column's rock;
        /// or, off-band, anywhere in that rock; and for a kind that favours deep stone, inside the
        /// deep stone where the band reaches it. False where the column has no rock at all.
        /// </summary>
        static bool DepthIn(NaturalGenContext ctx, NaturalContent.OreKind ore, int column, bool offBand,
                            int roll, out int y)
        {
            int surface = ctx.SurfaceY[column];
            int highest = ctx.SubsoilBaseY[column] - 1;
            int lowest = ctx.BedrockTopY[column];
            y = lowest;
            if (highest < lowest) return false;

            int dMin = ore.MinDepth, dMax = ore.MaxDepth;
            if (offBand) { dMin = surface - highest; dMax = surface - lowest; }
            else if (ore.FavoursDeepStone && ctx.DeepStoneTopY[column] > lowest)
            {
                // Deep stone is the layers below DeepStoneTopY: depths from surface - (top - 1).
                int deepMin = surface - (ctx.DeepStoneTopY[column] - 1);
                int lo = Math.Max(dMin, deepMin), hi = Math.Min(dMax, surface - lowest);
                if (lo <= hi) { dMin = lo; dMax = hi; }
            }

            int depth = dMin + roll % (dMax - dMin + 1);
            y = surface - depth;
            if (y > highest) y = highest;
            if (y < lowest) y = lowest;
            return true;
        }

        /// <summary>
        /// A rock-like cell beside a cavern, at a depth the kind's band (clamped into that column's
        /// rock) allows, found by a fixed number of probes into the carved cells. The probes are
        /// derived from one roll already drawn, so how many caverns there are changes nothing about
        /// the stream.
        /// </summary>
        static bool WallAnchor(NaturalGenContext ctx, NaturalContent.OreKind ore, int roll,
                               out int x, out int z, out int y)
        {
            x = z = y = 0;
            int count = ctx.CavernCells.Count;
            if (count == 0) return false;

            for (int probe = 0; probe < 16; probe++)
            {
                uint mixed = (uint)roll * 2654435761u + (uint)probe * 2246822519u;
                mixed ^= mixed >> 15;
                CellRef at = ctx.Size.FromIndex(ctx.CavernCells[(int)(mixed % (uint)count)]);
                int column = ctx.Column(at.X, at.Z);

                int surface = ctx.SurfaceY[column];
                int highest = ctx.SubsoilBaseY[column] - 1, lowest = ctx.BedrockTopY[column];
                int top = Math.Min(highest, Math.Max(lowest, surface - ore.MinDepth));
                int bottom = Math.Min(highest, Math.Max(lowest, surface - ore.MaxDepth));
                // A band wholly beneath this column's rock is clamped to its bottom, and a chamber
                // there has its floor a layer above the bedrock's shell: the bottom few layers are
                // the band, or a shallow board could never hang a find on a cave wall.
                if (surface - ore.MinDepth < lowest) top = Math.Min(highest, lowest + 2);
                if (at.Y < bottom || at.Y > top) continue;

                if (RockBeside(ctx, at, out x, out z)) { y = at.Y; return true; }
            }
            return false;
        }

        /// <summary>
        /// The first rock-like cell beside a cavern cell on its own layer, in a fixed compass
        /// order. Starting on the wall rather than in the chamber matters: a deposit only replaces
        /// rock-like cells, so a growth seeded in the chamber's air would place nothing at all.
        /// </summary>
        static bool RockBeside(NaturalGenContext ctx, CellRef at, out int x, out int z)
        {
            ReadOnlySpan<int> dx = stackalloc int[] { 1, -1, 0, 0 };
            ReadOnlySpan<int> dz = stackalloc int[] { 0, 0, 1, -1 };

            for (int i = 0; i < 4; i++)
            {
                x = at.X + dx[i];
                z = at.Z + dz[i];
                if (!ctx.Size.Contains(x, z, at.Y)) continue;
                if (NaturalContent.IsHostRock(ctx.Grid.Terrain[ctx.Index(x, z, at.Y)])) return true;
            }

            x = at.X;
            z = at.Z;
            return false;
        }

        static bool TouchesCavern(NaturalGenContext ctx, int cell)
        {
            CellRef at = ctx.Size.FromIndex(cell);
            return Carved(ctx, at.X + 1, at.Z, at.Y) || Carved(ctx, at.X - 1, at.Z, at.Y) ||
                   Carved(ctx, at.X, at.Z + 1, at.Y) || Carved(ctx, at.X, at.Z - 1, at.Y) ||
                   Carved(ctx, at.X, at.Z, at.Y + 1) || Carved(ctx, at.X, at.Z, at.Y - 1);
        }

        static bool Carved(NaturalGenContext ctx, int x, int z, int y) =>
            ctx.Size.Contains(x, z, y) && ctx.IsCavern(ctx.Index(x, z, y));

        /// <summary>
        /// Put ore in one cell, if it is rock-like and inside its column's rock band. The band
        /// check matters for the shapes that move between layers: an outcrop is rock-like too, and
        /// stands above the surface where no ore belongs.
        /// </summary>
        static bool Place(NaturalGenContext ctx, int x, int z, int y, ushort material,
                          System.Collections.Generic.List<int> placed)
        {
            if (!ctx.Size.Contains(x, z, y)) return false;
            int column = ctx.Column(x, z);
            if (y < ctx.BedrockTopY[column] || y >= ctx.SubsoilBaseY[column]) return false;
            int index = ctx.Index(x, z, y);
            if (!NaturalContent.IsHostRock(ctx.Grid.Terrain[index])) return false;
            ctx.SetTerrain(index, material);
            placed.Add(index);
            return true;
        }

        static void Grow(NaturalGenContext ctx, ref DeterministicRandom rng, NaturalContent.OreKind ore,
                         ref int x, ref int z, ref int y, int target, System.Collections.Generic.List<int> placed,
                         System.Collections.Generic.List<(long, int)> candidates)
        {
            // Start on rock. A seed can land in a cavern's air or on an ore already grown there,
            // and a deposit that places nothing is a deposit whose presence depends on the caverns,
            // which is the entanglement the draw-everything-first rule exists to prevent.
            if (!SeedNear(ctx, ref x, ref z, ref y)) return;

            switch (ore.Shape)
            {
                case OreShape.Vein: Vein(ctx, ref rng, ore.Terrain, x, z, y, target, placed); break;
                case OreShape.Oval: Oval(ctx, ref rng, ore.Terrain, x, z, y, target, placed, candidates); break;
                case OreShape.Seam: Accrete(ctx, ref rng, ore.Terrain, x, z, y, target, placed, 0, 0, rng.NextInt(2) == 0 ? 1 : 2); break;
                case OreShape.Blob: Accrete(ctx, ref rng, ore.Terrain, x, z, y, target, placed, 0, 1, 0); break;
                default: Accrete(ctx, ref rng, ore.Terrain, x, z, y, target, placed, -1, 1, 0); break;   // cluster, pocket
            }
        }

        /// <summary>
        /// The nearest cell a deposit may start in — rock-like, inside its column's rock band — to
        /// (x, z, y): rings outward on the layer, the layer above and the layer below at each
        /// distance, in a fixed order. False when there is none within twelve cells, which only a
        /// board with no rock could produce.
        /// </summary>
        static bool SeedNear(NaturalGenContext ctx, ref int x, ref int z, ref int y)
        {
            for (int r = 0; r <= 12; r++)
            for (int layer = 0; layer < 3; layer++)
            {
                int ny = y + (layer == 0 ? 0 : layer == 1 ? 1 : -1);
                for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                    int nx = x + dx, nz = z + dz;
                    if (!ctx.Size.Contains(nx, nz, ny)) continue;
                    int column = ctx.Column(nx, nz);
                    if (ny < ctx.BedrockTopY[column] || ny >= ctx.SubsoilBaseY[column]) continue;
                    if (!NaturalContent.IsHostRock(ctx.Grid.Terrain[ctx.Index(nx, nz, ny)])) continue;
                    x = nx; z = nz; y = ny;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Grow outward from a seed by adding a random neighbour of a random placed cell — round
        /// and compact, where a random walk strings out. Vertical steps are allowed within
        /// [y + <paramref name="below"/>, y + <paramref name="above"/>] (a seam passes 0 and 0, a
        /// blob 0 and 1, a cluster -1 and 1), and a seam stretched along one axis weights that
        /// axis three to one (<paramref name="axis"/> 1 for x, 2 for z, 0 for none).
        /// </summary>
        static void Accrete(NaturalGenContext ctx, ref DeterministicRandom rng, ushort material,
                            int x, int z, int y, int target, System.Collections.Generic.List<int> placed,
                            int below, int above, int axis)
        {
            if (!Place(ctx, x, z, y, material, placed)) return;   // SeedNear found it rock-like

            bool vertical = above > below;
            int steps = target * 8;
            for (int s = 0; s < steps && placed.Count < target; s++)
            {
                CellRef from = ctx.Size.FromIndex(placed[rng.NextInt(placed.Count)]);
                int nx = from.X, nz = from.Z, ny = from.Y;
                int pick = rng.NextInt(vertical ? 10 : 8);
                if (pick >= 8) ny += pick == 8 ? 1 : -1;
                else
                {
                    // Eight horizontal slots: two a direction, or three along the stretch axis and
                    // one across it.
                    bool alongX = axis == 1 ? pick < 6 : axis == 2 ? pick >= 6 : pick < 4;
                    bool positive = (pick & 1) == 0;
                    if (alongX) nx += positive ? 1 : -1; else nz += positive ? 1 : -1;
                }
                if (ny < y + below || ny > y + above) continue;
                Place(ctx, nx, nz, ny, material, placed);
            }
        }

        /// <summary>
        /// A winding vein: a walker holding a heading, turning a quarter now and then, drifting a
        /// layer rarely, and every so often laying a cell beside itself so the vein is one or two
        /// wide.
        /// </summary>
        static void Vein(NaturalGenContext ctx, ref DeterministicRandom rng, ushort material,
                         int x, int z, int y, int target, System.Collections.Generic.List<int> placed)
        {
            ReadOnlySpan<int> hx = stackalloc int[] { 1, 0, -1, 0 };
            ReadOnlySpan<int> hz = stackalloc int[] { 0, 1, 0, -1 };
            int heading = rng.NextInt(4);
            int steps = target * 4;
            for (int s = 0; s < steps && placed.Count < target; s++)
            {
                Place(ctx, x, z, y, material, placed);
                if (placed.Count < target && rng.NextInt(3) == 0)
                {
                    int side = (heading + (rng.NextInt(2) == 0 ? 1 : 3)) & 3;
                    Place(ctx, x + hx[side], z + hz[side], y, material, placed);
                }

                int turn = rng.NextInt(10);
                if (turn == 0) heading = (heading + 1) & 3;
                else if (turn == 1) heading = (heading + 3) & 3;

                int drift = rng.NextInt(12);
                int ny = drift == 0 ? y + 1 : drift == 1 ? y - 1 : y;
                int column = ctx.Column(Math.Clamp(x, 0, ctx.Size.SizeX - 1), Math.Clamp(z, 0, ctx.Size.SizeZ - 1));
                if (ny >= ctx.BedrockTopY[column] && ny < ctx.SubsoilBaseY[column]) y = ny;

                x = Math.Clamp(x + hx[heading], 0, ctx.Size.SizeX - 1);
                z = Math.Clamp(z + hz[heading], 0, ctx.Size.SizeZ - 1);
            }
        }

        /// <summary>
        /// One oval cluster: an ellipse two to three times as long as it is wide, one or two
        /// layers thick, filled from the middle outward until it holds the target — so it is an
        /// oval at any size rather than a clipped rectangle.
        /// </summary>
        static void Oval(NaturalGenContext ctx, ref DeterministicRandom rng, ushort material,
                         int cx, int cz, int cy, int target, System.Collections.Generic.List<int> placed,
                         System.Collections.Generic.List<(long, int)> candidates)
        {
            int layers = rng.NextInt(1, 3);
            bool alongX = rng.NextInt(2) == 0;
            int per = (target + layers - 1) / layers;
            // a * b * pi = per, with a = 2.5 b: b^2 = per / 7.85.
            int b = 1;
            while ((b + 1) * (b + 1) * 785 <= per * 100) b++;
            int a = b * 5 / 2 + 1;
            int ax = alongX ? a : b, az = alongX ? b : a;

            candidates.Clear();
            for (int y = cy; y < cy + layers; y++)
            for (int dz = -az - 1; dz <= az + 1; dz++)
            for (int dx = -ax - 1; dx <= ax + 1; dx++)
            {
                int x = cx + dx, z = cz + dz;
                if (!ctx.Size.Contains(x, z, y)) continue;
                long q = dx * dx * 1000L / ((long)ax * ax) + dz * dz * 1000L / ((long)az * az);
                if (q > 1300) continue;
                candidates.Add((q * 64 + (y - cy), ctx.Index(x, z, y)));
            }
            candidates.Sort((p, o) => p.Item1 != o.Item1 ? p.Item1.CompareTo(o.Item1) : p.Item2.CompareTo(o.Item2));

            for (int i = 0; i < candidates.Count && placed.Count < target; i++)
            {
                CellRef at = ctx.Size.FromIndex(candidates[i].Item2);
                Place(ctx, at.X, at.Z, at.Y, material, placed);
            }
        }
    }

    /// <summary>
    /// Pass 9 — the start location, and the consistency check that ends generation.
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
        public int Order => 10;
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

            EnsureReachable(ctx, column);
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

                // Dry ground, with a margin. Marsh counts as ground — it has to, or a bog column
                // would fail every surface-kind and walkability invariant — so the ground check
                // below would happily land the colony in a swamp. This is what does not.
                if (!DryAround(ctx, x, z, radius + ctx.Gen.startWaterClearance)) continue;

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

        /// <summary>Nothing wet inside the square. Off-map cells are not an obstacle.</summary>
        static bool DryAround(NaturalGenContext ctx, int cx, int cz, int reach)
        {
            for (int dz = -reach; dz <= reach; dz++)
            for (int dx = -reach; dx <= reach; dx++)
            {
                int x = cx + dx, z = cz + dz;
                if ((uint)x >= (uint)ctx.Size.SizeX || (uint)z >= (uint)ctx.Size.SizeZ) continue;
                if (ctx.Water[ctx.Column(x, z)] != (byte)WaterClass.None) return false;
            }
            return true;
        }

        /// <summary>
        /// The colony must be able to walk to most of its map. A wide river is the one shape that
        /// can cut it in two, and with no build pipeline yet a far bank is not merely inconvenient
        /// but dead, so this floods the surface from the start and cuts another ford wherever the
        /// river separates the two components.
        ///
        /// Forcing a ford rather than re-rolling the map is deliberate. Re-rolling makes
        /// generation take unbounded time on an unlucky seed, and a generator handed a seed that
        /// quietly uses a different one is a determinism smell even when it is reproducible.
        ///
        /// Failing after <see cref="NaturalMapGenDef.maxForcedFords"/> throws rather than shipping
        /// a severed map: a pond cannot sever anything, so a river still splitting the board after
        /// three crossings means the shape code is wrong, which is a bug and not a seed.
        /// </summary>
        static void EnsureReachable(NaturalGenContext ctx, int startColumn)
        {
            if (!ctx.Gen.water) return;

            var seen = new byte[ctx.Columns];
            var queue = new int[ctx.Columns];

            for (int attempt = 0; ; attempt++)
            {
                int reached = Flood(ctx, startColumn, seen, queue, out int walkable);
                ctx.Report.ReachableColumns = reached;
                ctx.Report.WalkableColumns = walkable;

                if (walkable <= 0 || reached * 100 / walkable >= ctx.Gen.minReachablePercent) return;
                if (attempt >= ctx.Gen.maxForcedFords || ctx.RiverCross == null)
                    throw new NaturalGenException(
                        $"The start reaches only {reached} of {walkable} walkable columns after " +
                        $"{ctx.Report.ForcedFords} forced fords. The water shapes have severed the map.");

                if (!ForceAFord(ctx, seen)) return;   // nothing left to open; the rest is islands
            }
        }

        /// <summary>
        /// Walkable columns reachable from the start, 4-connected, stepping at most one layer at
        /// a time. Columns rather than cells: the surface is a heightfield, so one bit per column
        /// answers the only question that matters here.
        /// </summary>
        static int Flood(NaturalGenContext ctx, int startColumn, byte[] seen, int[] queue, out int walkable)
        {
            Array.Clear(seen, 0, seen.Length);

            walkable = 0;
            for (int column = 0; column < ctx.Columns; column++)
                if (Standable(ctx, column)) walkable++;

            if (!Standable(ctx, startColumn)) return 0;

            int head = 0, tail = 0, reached = 0;
            seen[startColumn] = 1;
            queue[tail++] = startColumn;

            while (head < tail)
            {
                int column = queue[head++];
                reached++;
                int x = column % ctx.Size.SizeX, z = column / ctx.Size.SizeX;

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + StepX[d], nz = z + StepZ[d];
                    if ((uint)nx >= (uint)ctx.Size.SizeX || (uint)nz >= (uint)ctx.Size.SizeZ) continue;

                    int n = ctx.Column(nx, nz);
                    if (seen[n] != 0 || !Standable(ctx, n)) continue;
                    if (Math.Abs(ctx.SurfaceY[n] - ctx.SurfaceY[column]) > 1) continue;

                    seen[n] = 1;
                    queue[tail++] = n;
                }
            }

            return reached;
        }

        static readonly int[] StepX = { -1, 1, 0, 0 };
        static readonly int[] StepZ = { 0, 0, -1, 1 };

        static bool Standable(NaturalGenContext ctx, int column)
        {
            if (ctx.Water[column] == (byte)WaterClass.Deep) return false;
            if (ctx.TopSolidY[column] != ctx.SurfaceY[column]) return false;   // an outcrop
            return true;
        }

        /// <summary>
        /// Cut the river at the first march step whose two banks are on opposite sides of the
        /// flood, and rewrite the cells the class change touched. False when no such step exists,
        /// which means what is unreached is islanded by something other than the river.
        /// </summary>
        static bool ForceAFord(NaturalGenContext ctx, byte[] seen)
        {
            for (int step = 0; step < ctx.RiverSteps; step++)
            {
                int cross = ctx.RiverCross![step], half = ctx.RiverHalfWidth![step];
                int near = BankColumn(ctx, step, cross - half - 1);
                int far = BankColumn(ctx, step, cross + half + 1);
                if (near < 0 || far < 0) continue;
                if ((seen[near] != 0) == (seen[far] != 0)) continue;

                if (ctx.Ford(step, ctx.Gen.fordHalfLength) <= 0) continue;
                ctx.Report.ForcedFords++;
                Rewrite(ctx, step);
                return true;
            }

            return false;
        }

        static int BankColumn(NaturalGenContext ctx, int step, int cross)
        {
            int x = ctx.RiverAlongX ? step : cross;
            int z = ctx.RiverAlongX ? cross : step;
            if ((uint)x >= (uint)ctx.Size.SizeX || (uint)z >= (uint)ctx.Size.SizeZ) return -1;
            return ctx.Column(x, z);
        }

        /// <summary>Re-lay the cells of a ford, whose columns just changed depth.</summary>
        static void Rewrite(NaturalGenContext ctx, int step)
        {
            int reach = ctx.Gen.fordHalfLength;
            for (int s = step - reach; s <= step + reach; s++)
            {
                if ((uint)s >= (uint)ctx.RiverSteps) continue;
                int cross = ctx.RiverCross![s], half = ctx.RiverHalfWidth![s];
                for (int b = cross - half; b <= cross + half; b++)
                {
                    int column = BankColumn(ctx, s, b);
                    if (column >= 0) WaterFillPass.WriteColumn(ctx, column);
                }
            }
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
        /// and — apart from the caverns — no holes, which is why no support solve is needed at
        /// tick zero.
        ///
        /// <para>A cavern is the one hole the rule allows, and it is allowed by name rather than
        /// by loosening the rule: the cell must be one the cavern pass recorded carving, and it
        /// must still lie strictly inside its column's rock band. A hole anywhere else is a bug,
        /// and a cavern that has drifted out of the rock is the specific bug that would leave a
        /// pit in the ground or a crack into the bedrock.</para>
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

                    int column = columnBase + x;
                    bool carved = ctx.IsCavern(index);
                    bool shouldBeSolid = y <= ctx.TopSolidY[column] && !carved;
                    if (solidFlag != shouldBeSolid)
                        throw new NaturalGenException(
                            $"Cell {size.FromIndex(index)} breaks the column rule: solid up to the surface, " +
                            "air above, and holes only where a cavern was carved.");

                    if (carved && (y < ctx.BedrockTopY[column] + 1 || y > ctx.SubsoilBaseY[column] - 2))
                        throw new NaturalGenException(
                            $"Cell {size.FromIndex(index)} is a cavern outside the rock band: it would " +
                            "undermine the surface or break into the bedrock.");

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
