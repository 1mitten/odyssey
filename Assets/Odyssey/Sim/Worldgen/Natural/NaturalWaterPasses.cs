#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Worldgen.Natural
{
    /// <summary>What a column of water is. Stored one byte per column in the generation context.</summary>
    public enum WaterClass : byte
    {
        None = 0,

        /// <summary>Wadeable: within <see cref="NaturalMapGenDef.deepShoreDistance"/> rings of a shore.</summary>
        Shallow = 1,

        /// <summary>Out of one's depth. Impassable, and what a bridge will one day span.</summary>
        Deep = 2,

        /// <summary>Wet ground beside water. Not lowered, not water — ordinary ground that is slow.</summary>
        Marsh = 3,
    }

    public enum WaterShapeKind : byte { Pond = 0, Stream = 1, River = 2 }

    /// <summary>One body of water, for the report and for tests that want to find it again.</summary>
    public readonly struct WaterShape
    {
        public readonly WaterShapeKind Kind;

        /// <summary>A pond's centre column, or the mid-point of a path.</summary>
        public readonly int CentreColumn;

        public readonly int Cells;

        public WaterShape(WaterShapeKind kind, int centreColumn, int cells)
        {
            Kind = kind; CentreColumn = centreColumn; Cells = cells;
        }
    }

    /// <summary>
    /// Pass 2 — the water plan.
    ///
    /// **This pass writes no cells.** It decides which columns hold water, how deep, and which
    /// dry columns are boggy, and then lowers the four per-column boundary arrays for every
    /// channel column so that the strata pass which follows builds a correct, contiguous column
    /// by construction — bed at the new surface, subsoil beneath it, rock beneath that.
    ///
    /// That split is the whole design. The alternative, one pass *after* the strata that lowers
    /// columns and rewrites them, has to hand-repair the terrain, the solid flags, the report
    /// counters and the stratum boundaries: a second, worse copy of the strata pass, and exactly
    /// the kind of fix-up that drifts out of sync the next time a stratum is added. Geometry is a
    /// column decision and content is a cell decision, so they sit either side of the one
    /// full-grid loop. <see cref="WaterFillPass"/> is the other half.
    ///
    /// The proof that "one layer down" is honest geometry is that neither
    /// <c>AssertConsistent</c>'s column rule (<c>solid up to TopSolidY, air above</c>) nor the
    /// test that neighbouring surface cells are never more than one layer apart needed changing.
    ///
    /// Every field here is integer and every field is either <see cref="ValueNoise"/> at a
    /// deterministic coordinate or a draw from one keyed random stream, so the pass may be
    /// evaluated in any order and produces the same map on any machine.
    /// </summary>
    public sealed class WaterPlanPass : INaturalGenPass
    {
        public int Order => 2;
        public string Name => "WaterPlan";

        // Item numbers within the pass's random purpose, spaced widely so that adding a pond can
        // never shift the river's stream.
        const int ItemLayout = 0;
        const int ItemRiver = 1;
        const int ItemStreamBase = 100;
        const int ItemPondBase = 1000;

        public void Run(NaturalGenContext ctx)
        {
            var gen = ctx.Gen;
            if (!gen.water) return;   // bit-identical to a map generated before water existed

            var layout = ctx.Random(NaturalGenPurpose.Water, ItemLayout);

            // A river *or* streams, never both. Both at once reads as noise rather than as
            // landscape, and it doubles the number of ways the map can be cut in two, which is
            // the one failure the connectivity guarantee below has to reason about.
            bool river = layout.NextInt(1000) < gen.riverChancePerMille;

            int ponds = ctx.Columns * gen.pondsPer10000Columns / 10000;
            // The same floor rule the outcrop and ore passes use: rounding on a small map must
            // not silently wipe the feature out, but a rate of zero still means zero.
            if (ponds < 1) ponds = gen.pondsPer10000Columns > 0 ? 1 : 0;
            if (river) ponds -= ponds / 2;   // a river is already a lot of water

            if (river) StampRiver(ctx, ref layout);
            else for (int i = 0; i < gen.streamCount; i++) StampStream(ctx, i);

            for (int i = 0; i < ponds; i++) StampPond(ctx, i);

            ClassifyDepth(ctx);
            if (river) CutFords(ctx, ref layout);
            SpreadMarsh(ctx);
            LowerChannels(ctx);

            ctx.Report.WaterShapes = ctx.WaterShapes.Count;
        }

        // ---- paths: a noise-displaced straight line ---------------------------------------
        //
        // The cross coordinate is a *function* of the march step, which is what makes this safe:
        // the path cannot self-intersect (one cross value per step), cannot leave the map (it is
        // clamped), cannot wander off on step three (both ends are on the edge by construction)
        // and terminates in exactly one step per cell of the dominant axis.
        //
        // The two rejected alternatives. A free noise-steered walk can double back, stall or
        // leave early, and every guard against that is where the non-determinism lives. A contour
        // of a noise field has a width of 2e/|grad f|, so it is a hairline where the field is
        // steep and a lake where it is flat — and width is the entire requirement here, 1 to 3
        // cells for a stream against 5 to 12 for a river.
        //
        // The accepted limitation: a path that is a function of its march step has no oxbows and
        // no hairpins. At the default period that reads as a proper meander at map scale, and the
        // upgrade — marching in (a, b) pairs — is a much less safe algorithm.

        static void StampStream(NaturalGenContext ctx, int index)
        {
            var gen = ctx.Gen;
            var rng = ctx.Random(NaturalGenPurpose.Water, ItemStreamBase + index);
            StampPath(ctx, ref rng, WaterShapeKind.Stream,
                      gen.streamMeanderAmplitudePerMille, gen.streamMeanderPeriod, gen.streamMeanderOctaves,
                      0, gen.streamMaxHalfWidth, gen.streamWidthPeriod, null, null);
        }

        static void StampRiver(NaturalGenContext ctx, ref DeterministicRandom layout)
        {
            var gen = ctx.Gen;
            var rng = ctx.Random(NaturalGenPurpose.Water, ItemRiver);

            // The river's march is recorded so a ford can be cut across it later without having
            // to rediscover where the channel is.
            int span = Math.Max(ctx.Size.SizeX, ctx.Size.SizeZ);
            ctx.RiverCross = new int[span];
            ctx.RiverHalfWidth = new int[span];

            StampPath(ctx, ref rng, WaterShapeKind.River,
                      gen.riverMeanderAmplitudePerMille, gen.streamMeanderPeriod, gen.streamMeanderOctaves,
                      gen.riverMinHalfWidth, gen.riverMaxHalfWidth, gen.streamWidthPeriod,
                      ctx.RiverCross, ctx.RiverHalfWidth);
        }

        static void StampPath(NaturalGenContext ctx, ref DeterministicRandom rng, WaterShapeKind kind,
                              int amplitudePerMille, int meanderPeriod, int meanderOctaves,
                              int minHalfWidth, int maxHalfWidth, int widthPeriod,
                              int[]? crossOut, int[]? halfOut)
        {
            bool alongX = rng.NextInt(2) == 0;
            int sizeA = alongX ? ctx.Size.SizeX : ctx.Size.SizeZ;
            int sizeB = alongX ? ctx.Size.SizeZ : ctx.Size.SizeX;
            if (sizeA < 2 || sizeB < 3) return;

            uint pathSeed = rng.NextUInt();
            uint widthSeed = pathSeed ^ 0x5BD1E995u;

            int amplitude = sizeB * amplitudePerMille / 1000;
            int centre = sizeB / 2;
            int cells = 0, previous = -1;
            int midStep = sizeA / 2, midCross = centre;

            for (int a = 0; a < sizeA; a++)
            {
                int half = ValueNoise.Band(ValueNoise.Value2D(widthSeed, a, 0, widthPeriod),
                                           minHalfWidth, maxHalfWidth);

                int noise = ValueNoise.Fractal2D(pathSeed, a, 0, meanderPeriod, meanderOctaves);
                int cross = centre + (noise - ValueNoise.Scale / 2) * amplitude / (ValueNoise.Scale / 2);
                int low = half, high = sizeB - 1 - half;
                if (low > high) { low = high = sizeB / 2; }
                if (cross < low) cross = low;
                if (cross > high) cross = high;

                if (crossOut != null) { crossOut[a] = cross; halfOut![a] = half; }
                if (a == midStep) midCross = cross;

                // The cross coordinate can jump by more than one between steps, which would leave
                // a diagonal gap. Filling the span back to the previous step's centre keeps the
                // channel a single 4-connected set, which the depth flood and the reachability
                // check both depend on.
                int from = cross - half, to = cross + half;
                if (previous >= 0)
                {
                    if (previous < from) from = previous;
                    if (previous > to) to = previous;
                }
                previous = cross;

                for (int b = from; b <= to; b++)
                {
                    if ((uint)b >= (uint)sizeB) continue;
                    int column = alongX ? ctx.Column(a, b) : ctx.Column(b, a);
                    if (ctx.Water[column] != (byte)WaterClass.None) continue;
                    ctx.Water[column] = (byte)WaterClass.Shallow;   // depth is decided later
                    cells++;
                }
            }

            if (kind == WaterShapeKind.River)
            {
                ctx.RiverAlongX = alongX;
                ctx.RiverSteps = sizeA;
            }

            if (cells <= 0) return;
            int centreColumn = alongX ? ctx.Column(midStep, midCross) : ctx.Column(midCross, midStep);
            ctx.WaterShapes.Add(new WaterShape(kind, centreColumn, cells));
        }

        // ---- ponds ------------------------------------------------------------------------

        /// <summary>
        /// A pond is placed and then validated, not searched for. The heightfield terraces from a
        /// long-period field, so "find a local minimum" returns either a whole terrace or nothing.
        ///
        /// The level-footprint rule is load-bearing rather than fastidious: requiring one surface
        /// layer across the whole disc is what makes the water surface genuinely level, what lets
        /// every column of the pond drop to the same bed, and what keeps neighbouring surface
        /// cells within one layer of each other. A pond that does not fit a terrace is dropped.
        /// </summary>
        static void StampPond(NaturalGenContext ctx, int index)
        {
            var gen = ctx.Gen;
            var rng = ctx.Random(NaturalGenPurpose.Water, ItemPondBase + index);

            int cx = rng.NextInt(ctx.Size.SizeX);
            int cz = rng.NextInt(ctx.Size.SizeZ);
            int radius = rng.NextInt(gen.minPondRadius, gen.maxPondRadius + 1);
            uint edgeSeed = rng.NextUInt();

            int reach = radius + gen.pondEdgeJitter;
            if (cx - reach < 0 || cx + reach >= ctx.Size.SizeX ||
                cz - reach < 0 || cz + reach >= ctx.Size.SizeZ)
            {
                ctx.Report.PondsRejectedForEdge++;
                return;
            }

            int level = ctx.SurfaceY[ctx.Column(cx, cz)];
            for (int dz = -reach; dz <= reach; dz++)
            for (int dx = -reach; dx <= reach; dx++)
            {
                if (ctx.SurfaceY[ctx.Column(cx + dx, cz + dz)] == level) continue;
                ctx.Report.PondsRejectedForRelief++;
                return;
            }

            int cells = 0;
            for (int dz = -reach; dz <= reach; dz++)
            for (int dx = -reach; dx <= reach; dx++)
            {
                int x = cx + dx, z = cz + dz;

                // The edge is distorted by sampling a noise field in *world* space rather than by
                // angle: no trig, no floats, and two overlapping ponds wobble consistently with
                // each other because they read the same field at the same places.
                int jitter = ValueNoise.Band(
                    ValueNoise.Fractal2D(edgeSeed, x, z, gen.pondEdgePeriod, 2),
                    -gen.pondEdgeJitter, gen.pondEdgeJitter);
                int effective = radius + jitter;
                if (effective < 1) effective = 1;
                if (dx * dx + dz * dz > effective * effective) continue;

                int column = ctx.Column(x, z);
                if (ctx.Water[column] != (byte)WaterClass.None) continue;
                ctx.Water[column] = (byte)WaterClass.Shallow;
                cells++;
            }

            if (cells > 0) ctx.WaterShapes.Add(new WaterShape(WaterShapeKind.Pond, ctx.Column(cx, cz), cells));
        }

        // ---- depth ------------------------------------------------------------------------

        /// <summary>
        /// One bounded breadth-first sweep inward from every shore. A water column within
        /// <see cref="NaturalMapGenDef.deepShoreDistance"/> rings of dry land stays wadeable;
        /// everything further in is out of one's depth.
        ///
        /// One rule and one bound give the whole depth model: a stream of one to three cells is
        /// wadeable end to end, a five-wide river gets a single deep cell down its middle, an
        /// eleven-wide one a seven-cell channel with a wadeable margin either side, and a pond a
        /// broad deep middle inside a rim you can paddle in.
        ///
        /// Off-map neighbours deliberately do **not** seed the flood. If they did, a river would
        /// arrive and leave through wadeable mouths, which reads as the map cutting the river off
        /// rather than the river crossing the map.
        /// </summary>
        static void ClassifyDepth(NaturalGenContext ctx)
        {
            int limit = ctx.Gen.deepShoreDistance;
            var distance = ctx.ShoreDistance;
            var queue = new int[ctx.Columns];
            int head = 0, tail = 0;

            for (int column = 0; column < ctx.Columns; column++)
            {
                if (ctx.Water[column] == (byte)WaterClass.None) continue;
                if (!HasDryNeighbour(ctx, column)) continue;
                distance[column] = 1;
                queue[tail++] = column;
            }

            while (head < tail)
            {
                int column = queue[head++];
                int next = distance[column] + 1;
                if (next > limit) continue;

                int x = column % ctx.Size.SizeX, z = column / ctx.Size.SizeX;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + NeighbourX[d], nz = z + NeighbourZ[d];
                    if ((uint)nx >= (uint)ctx.Size.SizeX || (uint)nz >= (uint)ctx.Size.SizeZ) continue;
                    int n = ctx.Column(nx, nz);
                    if (ctx.Water[n] == (byte)WaterClass.None || distance[n] != 0) continue;
                    distance[n] = (byte)next;
                    queue[tail++] = n;
                }
            }

            int shallow = 0, deep = 0;
            for (int column = 0; column < ctx.Columns; column++)
            {
                if (ctx.Water[column] == (byte)WaterClass.None) continue;
                if (distance[column] != 0) { ctx.Water[column] = (byte)WaterClass.Shallow; shallow++; }
                else { ctx.Water[column] = (byte)WaterClass.Deep; deep++; }
            }

            ctx.Report.ShallowWaterColumns = shallow;
            ctx.Report.DeepWaterColumns = deep;
        }

        static readonly int[] NeighbourX = { -1, 1, 0, 0 };
        static readonly int[] NeighbourZ = { 0, 0, -1, 1 };

        static bool HasDryNeighbour(NaturalGenContext ctx, int column)
        {
            int x = column % ctx.Size.SizeX, z = column / ctx.Size.SizeX;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + NeighbourX[d], nz = z + NeighbourZ[d];
                if ((uint)nx >= (uint)ctx.Size.SizeX || (uint)nz >= (uint)ctx.Size.SizeZ) continue;
                if (ctx.Water[ctx.Column(nx, nz)] == (byte)WaterClass.None) return true;
            }
            return false;
        }

        // ---- fords ------------------------------------------------------------------------

        /// <summary>
        /// A river is the one shape that can cut the map in two, so every river is given
        /// crossings by construction rather than left to be rescued by the reachability check.
        /// A ford is still a channel with real banks — a gravel shallows, not a bridge — so it
        /// costs a colonist time without ever costing them the far side of the map.
        /// </summary>
        static void CutFords(NaturalGenContext ctx, ref DeterministicRandom rng)
        {
            var gen = ctx.Gen;
            if (gen.riverFords <= 0 || ctx.RiverCross == null || ctx.RiverSteps <= 0) return;

            int length = ctx.RiverSteps;
            for (int i = 0; i < gen.riverFords; i++)
            {
                int at = length * (i + 1) / (gen.riverFords + 1);
                int jitter = Math.Max(1, length / 16);
                at += rng.NextInt(-jitter, jitter + 1);
                if (at < 0) at = 0;
                if (at >= length) at = length - 1;
                ctx.Ford(at, gen.fordHalfLength);
            }
        }

        // ---- marsh ------------------------------------------------------------------------

        /// <summary>
        /// A second bounded flood, this time over dry land, out from every water-adjacent column.
        /// Marsh columns are **not** lowered: a bog is wet ground at its own height, so nothing
        /// about the geometry changes and no invariant has to be re-checked.
        ///
        /// A pure distance ring reads as a cartoon outline, so the outer rings are broken up with
        /// the same field the cover pass uses rather than a new one — the cost is one noise
        /// evaluation per fringe column and the map gains no new parameter to keep in step.
        /// </summary>
        static void SpreadMarsh(NaturalGenContext ctx)
        {
            var gen = ctx.Gen;
            if (gen.marshFringe <= 0) return;

            uint coverSeed = ctx.Seed ^ 0x2B6F1D07u;
            var land = new byte[ctx.Columns];
            var queue = new int[ctx.Columns];
            int head = 0, tail = 0;

            for (int column = 0; column < ctx.Columns; column++)
            {
                if (ctx.Water[column] != (byte)WaterClass.None) continue;
                if (!HasWetNeighbour(ctx, column)) continue;
                land[column] = 1;
                queue[tail++] = column;
            }

            int marsh = 0;
            while (head < tail)
            {
                int column = queue[head++];
                int ring = land[column];

                int x = column % ctx.Size.SizeX, z = column / ctx.Size.SizeX;
                int noise = ValueNoise.Fractal2D(coverSeed, x, z, gen.coverPeriod, gen.coverOctaves);
                if (noise >= gen.marshThreshold + (ring - 1) * gen.marshFalloff)
                {
                    ctx.Water[column] = (byte)WaterClass.Marsh;
                    marsh++;
                }

                if (ring >= gen.marshFringe) continue;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + NeighbourX[d], nz = z + NeighbourZ[d];
                    if ((uint)nx >= (uint)ctx.Size.SizeX || (uint)nz >= (uint)ctx.Size.SizeZ) continue;
                    int n = ctx.Column(nx, nz);
                    if (land[n] != 0) continue;
                    if (ctx.Water[n] == (byte)WaterClass.Shallow || ctx.Water[n] == (byte)WaterClass.Deep) continue;
                    land[n] = (byte)(ring + 1);
                    queue[tail++] = n;
                }
            }

            ctx.Report.MarshColumns = marsh;
        }

        static bool HasWetNeighbour(NaturalGenContext ctx, int column)
        {
            int x = column % ctx.Size.SizeX, z = column / ctx.Size.SizeX;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + NeighbourX[d], nz = z + NeighbourZ[d];
                if ((uint)nx >= (uint)ctx.Size.SizeX || (uint)nz >= (uint)ctx.Size.SizeZ) continue;
                byte w = ctx.Water[ctx.Column(nx, nz)];
                if (w == (byte)WaterClass.Shallow || w == (byte)WaterClass.Deep) return true;
            }
            return false;
        }

        // ---- the geometry -----------------------------------------------------------------

        /// <summary>
        /// Drop every channel column by one layer, exactly as the heightfield derives its own
        /// boundaries, so the strata pass lays a bed at the new surface with the full stack
        /// beneath it. The water itself is written by <see cref="WaterFillPass"/>, into the cell
        /// the old surface used to occupy.
        ///
        /// A shape whose columns cannot be lowered — a map so shallow that there is no solid cell
        /// left beneath the bed — is abandoned **whole**, rather than per column, because a
        /// stream with one column left behind is a stream with a hole in it.
        /// </summary>
        static void LowerChannels(NaturalGenContext ctx)
        {
            var gen = ctx.Gen;

            for (int column = 0; column < ctx.Columns; column++)
            {
                byte w = ctx.Water[column];
                if (w != (byte)WaterClass.Shallow && w != (byte)WaterClass.Deep) continue;
                if (ctx.SurfaceY[column] >= 1) continue;
                Abandon(ctx);
                return;
            }

            var pending = new bool[ctx.Columns];
            var queue = new Queue<int>();

            for (int column = 0; column < ctx.Columns; column++)
            {
                if (!IsChannel(ctx, column)) continue;
                Settle(ctx, column, ctx.SurfaceY[column] - 1);
                Push(queue, pending, column);
            }

            CutValleys(ctx, queue, pending);

            for (int column = 0; column < ctx.Columns; column++)
            {
                if (!IsChannel(ctx, column) || ctx.SurfaceY[column] >= 1) continue;
                Abandon(ctx);
                return;
            }

            Rescan(ctx);
        }

        /// <summary>
        /// Settle the ground around every channel until the water lies in it properly, by
        /// lowering — only ever lowering — whichever of a disagreeing pair is too high.
        ///
        /// Two rules, and both are needed. A bank more than one layer above the water beside it
        /// is a cliff, and "neighbouring surface cells are never more than one layer apart" is
        /// the invariant that keeps the board walkable without ramps; a path cutting across a
        /// terrace step makes one every time, because it lowers a column that was already a
        /// layer below its neighbour. And a bank *below* the water beside it is worse, though it
        /// breaks no invariant: it is water perched above open ground, which would drain. So the
        /// channel comes down to meet it.
        ///
        /// Together they say a dry bank sits **exactly** one layer above the bed it looks down
        /// on, which is what a watercourse does to the ground it runs through. The result reads
        /// as a shallow valley rather than a trench slotted into a hillside, and it is why a
        /// stream can cross terraced ground at all.
        ///
        /// It terminates because every step lowers a column, no column goes below its floor, and
        /// the heightfield's range is a handful of layers.
        /// </summary>
        static void CutValleys(NaturalGenContext ctx, Queue<int> queue, bool[] pending)
        {
            while (queue.Count > 0)
            {
                int column = queue.Dequeue();
                pending[column] = false;

                int x = column % ctx.Size.SizeX, z = column / ctx.Size.SizeX;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + NeighbourX[d], nz = z + NeighbourZ[d];
                    if ((uint)nx >= (uint)ctx.Size.SizeX || (uint)nz >= (uint)ctx.Size.SizeZ) continue;
                    Reconcile(ctx, column, ctx.Column(nx, nz), queue, pending);
                }
            }
        }

        /// <summary>
        /// Settle one pair of neighbouring columns, whichever end of it woke up.
        ///
        /// Symmetry is the whole of it, and getting it wrong is subtle: if only the wet end knows
        /// how to pull its bank down, then a bank lowered by some *other* channel never tells the
        /// channel beside it to follow, and the map keeps a stretch of water perched over open
        /// ground. So the relation is stated once here and both ends enforce it.
        /// </summary>
        static void Reconcile(NaturalGenContext ctx, int a, int b, Queue<int> queue, bool[] pending)
        {
            bool wetA = IsChannel(ctx, a), wetB = IsChannel(ctx, b);
            int ya = ctx.SurfaceY[a], yb = ctx.SurfaceY[b];

            if (wetA == wetB)
            {
                // Two banks, or two beds: never more than one layer apart, either way round.
                if (yb > ya + 1) Lower(ctx, b, ya + 1, queue, pending);
                else if (ya > yb + 1) Lower(ctx, a, yb + 1, queue, pending);
                return;
            }

            int bed = wetA ? a : b;
            int bank = wetA ? b : a;
            int bedY = ctx.SurfaceY[bed], bankY = ctx.SurfaceY[bank];

            // A bank stands exactly one layer over the bed it looks down on. Higher is a cliff;
            // lower is water hanging over open ground, which would drain.
            if (bankY > bedY + 1) Lower(ctx, bank, bedY + 1, queue, pending);
            else if (bankY < bedY + 1) Lower(ctx, bed, bankY - 1, queue, pending);
        }

        /// <summary>
        /// Put a column down to a new layer, unless it is already at the floor. Only ever down,
        /// which is what makes the relaxation terminate.
        /// </summary>
        static void Lower(NaturalGenContext ctx, int column, int to, Queue<int> queue, bool[] pending)
        {
            if (to < 0) to = 0;
            if (to >= ctx.SurfaceY[column]) return;

            Settle(ctx, column, to);
            Push(queue, pending, column);
        }

        static void Push(Queue<int> queue, bool[] pending, int column)
        {
            if (pending[column]) return;
            pending[column] = true;
            queue.Enqueue(column);
        }

        static bool IsChannel(NaturalGenContext ctx, int column)
        {
            byte w = ctx.Water[column];
            return w == (byte)WaterClass.Shallow || w == (byte)WaterClass.Deep;
        }

        /// <summary>
        /// Take the water off the map. A shape is abandoned whole rather than column by column,
        /// because a stream with one column left behind is a stream with a hole in it, and the
        /// ground the relaxation already moved is left where it is: it is a valley with no water
        /// in it, which is unremarkable, where a dry gap in a river would not be.
        /// </summary>
        static void Abandon(NaturalGenContext ctx)
        {
            Array.Clear(ctx.Water, 0, ctx.Water.Length);
            Array.Clear(ctx.ShoreDistance, 0, ctx.ShoreDistance.Length);
            ctx.WaterShapes.Clear();
            ctx.Report.WaterShapesAbandoned++;
            ctx.Report.ShallowWaterColumns = 0;
            ctx.Report.DeepWaterColumns = 0;
            ctx.Report.MarshColumns = 0;
            Rescan(ctx);
        }

        /// <summary>The heightfield's own report figures, after the valleys moved the ground.</summary>
        static void Rescan(NaturalGenContext ctx)
        {
            int min = int.MaxValue, max = int.MinValue;
            for (int column = 0; column < ctx.Columns; column++)
            {
                int y = ctx.SurfaceY[column];
                if (y < min) min = y;
                if (y > max) max = y;
            }
            ctx.Report.SurfaceMinY = min;
            ctx.Report.SurfaceMaxY = max;
        }

        /// <summary>
        /// Put a column's surface at a new layer and re-derive its stratum boundaries exactly as
        /// the heightfield pass does, so the strata pass that follows still sees one consistent
        /// set of per-column arrays and needs no knowledge that water exists.
        /// </summary>
        static void Settle(NaturalGenContext ctx, int column, int surface)
        {
            if (surface < 0) surface = 0;

            ctx.SurfaceY[column] = surface;
            ctx.TopSolidY[column] = surface;

            int subsoilBase = surface - ctx.Gen.subsoilDepth;
            if (subsoilBase < 0) subsoilBase = 0;
            ctx.SubsoilBaseY[column] = subsoilBase;
            ctx.BedrockTopY[column] = Math.Min(ctx.Gen.bedrockLayers, subsoilBase);
        }
    }

    /// <summary>
    /// Pass 4 — filling the channels.
    ///
    /// The cell half of the water decision, run after the strata pass has laid a correct column
    /// beneath every bed. Three writes per column and no searching: the plan pass already decided
    /// everything.
    ///
    /// The bed reuses sand and gravel rather than introducing a riverbed terrain. Both are
    /// already ground, so every strata and surface-kind invariant passes without a special case,
    /// and nobody ever sees the bottom of a deep channel anyway.
    ///
    /// Water occupies exactly one cell, whatever its depth. Cutting a deep core two layers down
    /// for visual depth is deliberately rejected: the bed *is* the surface, so a two-layer core
    /// would put neighbouring surface cells two layers apart and break the invariant that keeps
    /// the board walkable. Depth is a rendering problem, not a geometry problem.
    /// </summary>
    public sealed class WaterFillPass : INaturalGenPass
    {
        public int Order => 4;
        public string Name => "WaterFill";

        public void Run(NaturalGenContext ctx)
        {
            if (!ctx.Gen.water) return;

            int shallow = 0, deep = 0, marsh = 0;
            for (int column = 0; column < ctx.Columns; column++)
            {
                switch ((WaterClass)ctx.Water[column])
                {
                    case WaterClass.Shallow: WriteColumn(ctx, column); shallow++; break;
                    case WaterClass.Deep: WriteColumn(ctx, column); deep++; break;
                    case WaterClass.Marsh: WriteColumn(ctx, column); marsh++; break;
                }
            }

            ctx.Report.ShallowWaterCells = shallow;
            ctx.Report.DeepWaterCells = deep;
            ctx.Report.MarshCells = marsh;
            ctx.Report.WaterCells = shallow + deep;
        }

        /// <summary>
        /// One column's worth of water. Exposed because the start pass forces fords after the
        /// fact and has to rewrite the cells it changed the class of.
        /// </summary>
        public static void WriteColumn(NaturalGenContext ctx, int column)
        {
            int x = column % ctx.Size.SizeX, z = column / ctx.Size.SizeX;
            var kind = (WaterClass)ctx.Water[column];
            if (kind == WaterClass.None) return;

            int surface = ctx.SurfaceY[column];
            int bedIndex = ctx.Index(x, z, surface);

            if (kind == WaterClass.Marsh)
            {
                // Not lowered, and not water: the surface cell itself becomes wet ground.
                Recount(ctx, bedIndex);
                ctx.SetTerrain(bedIndex, NaturalContent.TerrainMarsh);
                return;
            }

            int waterIndex = bedIndex + ctx.Size.LayerStride;
            if (waterIndex >= ctx.Size.CellCount) return;

            Recount(ctx, bedIndex);
            ctx.SetTerrain(bedIndex, kind == WaterClass.Shallow
                ? NaturalContent.TerrainSand
                : NaturalContent.TerrainPackedGravel);

            ctx.SetTerrain(waterIndex, kind == WaterClass.Shallow
                ? NaturalContent.TerrainShallowWater
                : NaturalContent.TerrainDeepWater);

            // The strata pass counted the cell the water now fills as air, and it is now
            // water — neither air nor solid. The bed below was already the solid surface and
            // has only changed what it is made of, so SolidCells does not move.
            ctx.Report.AirCells--;
        }

        /// <summary>Keeps the cover counts honest as a surface cell changes what it is made of.</summary>
        static void Recount(NaturalGenContext ctx, int index)
        {
            ushort was = ctx.Grid.Terrain[index];
            if (was == NaturalContent.TerrainGrass) ctx.Report.GrassCells--;
            else if (was == NaturalContent.TerrainBareEarth) ctx.Report.BareEarthCells--;
            else if (was == NaturalContent.TerrainPackedGravel) ctx.Report.GravelCells--;
            else if (was == NaturalContent.TerrainSand) ctx.Report.SandCells--;
        }
    }
}
