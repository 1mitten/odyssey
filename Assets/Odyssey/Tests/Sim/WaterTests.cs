#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Water: that it generates, that it has the shape it claims, that wading is a third of
    /// walking, and above all that it never cuts the colony off from its own map.
    ///
    /// The geometry assertions here are the ones worth keeping. The generator's own invariants —
    /// the column rule, neighbouring surface cells being within one layer, the surface being
    /// continuous with nothing floating — were **not** relaxed to let water in, and they are the
    /// real evidence that a channel one layer down is honest geometry rather than a special case.
    /// </summary>
    public class WaterTests
    {
        static readonly GridSize Size = new GridSize(64, 64, 20);

        static NaturalMapResult Generate(uint seed, Action<NaturalMapGenDef>? tune = null, GridSize? size = null)
        {
            var s = size ?? Size;
            var gen = NaturalMapGenDef.For(s);
            tune?.Invoke(gen);
            return NaturalMapGenerator.Generate(new CellGrid(s), seed, gen);
        }

        static NaturalMapResult River(uint seed) => Generate(seed, g => g.riverChancePerMille = 1000);

        // ---------------------------------------------------------------- it exists

        [Test]
        public void AMapHasWaterOnIt()
        {
            var result = Generate(3);
            Assert.That(result.Report.WaterCells, Is.GreaterThan(0), "no water anywhere");
            Assert.That(result.Report.MarshCells, Is.GreaterThan(0), "water with no bog beside it");
            Assert.That(result.Report.WaterShapes, Is.GreaterThan(0));
            TestContext.WriteLine(result.ToString());
        }

        /// <summary>
        /// The hashes a wilderness map had **before water was written**, measured on the commit
        /// before it by generating the same sizes and seeds there and printing the grid hash.
        ///
        /// These are the only baked hashes in the suite, and they earn it: they are what turns
        /// "water should be a no-op when it is switched off" from an argument into a check. If
        /// anything about water ever leaks into a pass that runs whatever the switch says — a
        /// draw taken from the random stream before the early return, a column skipped, a bank
        /// lowered — one of these moves and the barren board stops being the baseline that every
        /// other judgement is made against.
        /// </summary>
        /// <remarks>
        /// <b>Re-based on 2026-09-16 when the mining branch merged, and the mechanism is two
        /// deliberate changes to every natural map — not water leaking past its switch.</b>
        ///
        /// <para><b>Headroom moved the ground.</b> The ground layer is now "as high as it can sit
        /// while leaving <c>headroomLayers</c> of sky above the tallest terrace" rather than two
        /// fifths of the way up, so every column on every board starts at a different height. That
        /// is why the <em>barren</em> goldens moved as well, and they are still seed-invariant —
        /// one flat board per size, exactly as before, at a new level.</para>
        ///
        /// <para><b>Caverns are a new pass.</b> Pass 8 carves sealed voids into the rock of any
        /// board that has rock, which the dry board does. <c>MakeBarren</c> has no rock band worth
        /// carving, which is why the barren hashes move only by the headroom and not by the seed.</para>
        ///
        /// <para>What this test is for is unchanged and still holds: with <c>water = false</c> the
        /// water passes must draw nothing at all, so a dry map is exactly the map the rest of the
        /// generator makes. The numbers are the baseline for that, not for anything else.</para>
        ///
        /// <para><b>Re-based a second time on 2026-09-18, by the terrace guard, and the shape of
        /// the re-base is the evidence.</b> <c>TreePass</c> now refuses a tree in a cell at the foot
        /// of a terrace step, because presentation fills that cell with a bank and the tree is
        /// sheared off by it (<c>TerraceFoot</c>). A wooded board therefore holds a few dozen fewer
        /// trees, and a tree is an edifice in the grid, so every <c>dry</c> number below moved.
        /// <b>Every <c>barren</c> number is byte-for-byte the one this table already held</b> —
        /// measured, not assumed — which is exactly right: the bare board grows no trees, so a
        /// guard on tree placement cannot touch it. Anything else moving would have meant something
        /// else had come along with it.</para>
        ///
        /// <para><b>Re-based a third time on 2026-09-25, by design 45, and again the barren column
        /// is the evidence.</b> Trees became species — the same trees in the same cells, carrying
        /// four edifice ids where there were two — and the undergrowth pass placed bushes, each an
        /// edifice with its own flag bit. Every <c>dry</c> number moved; <b>every <c>barren</c> number
        /// is the one this table already held</b>, measured, because the bare board grows neither.</para>
        /// </remarks>
        static readonly (GridSize size, uint seed, ulong dry, ulong barren)[] BeforeWater =
        {
            (new GridSize(64, 64, 20), 1u, 0x94bd80785a5dcce0UL, 0x5ba75a6d28bea325UL),
            (new GridSize(64, 64, 20), 7u, 0x73d4b2ce2a816929UL, 0x5ba75a6d28bea325UL),
            (new GridSize(64, 64, 20), 42u, 0xbcf541841cc12611UL, 0x5ba75a6d28bea325UL),
            (new GridSize(120, 120, 16), 1u, 0x72bd1d555391b46dUL, 0xc0dc37cc3b1edd25UL),
            (new GridSize(120, 120, 16), 7u, 0x8b6673f0bdffec70UL, 0xc0dc37cc3b1edd25UL),
            (new GridSize(120, 120, 16), 42u, 0xf1fa23293f14341bUL, 0xc0dc37cc3b1edd25UL),
        };

        [Test]
        public void TurningWaterOffLeavesTheMapTheGeneratorMadeBeforeItExisted()
        {
            foreach (var (size, seed, dry, barren) in BeforeWater)
            {
                Assert.That(Generate(seed, g => g.water = false, size).GridHash, Is.EqualTo(dry),
                    $"a dry {size} map at seed {seed} is not the map this generator used to make");
                Assert.That(Generate(seed, g => g.MakeBarren(), size).GridHash, Is.EqualTo(barren),
                    $"the bare {size} board at seed {seed} moved");
            }

            // Every water knob moved as far as it goes, with the master switch off. If anything
            // about water leaked into a pass that runs whatever the switch says — a stray draw
            // from the random stream, a skipped column, a lowered bank — these two hashes differ.
            var plain = Generate(21, g => g.water = false);
            var loud = Generate(21, g =>
            {
                g.water = false;
                g.riverChancePerMille = 1000;
                g.pondsPer10000Columns = 400;
                g.streamCount = 6;
                g.marshFringe = 6;
            });

            Assert.That(loud.GridHash, Is.EqualTo(plain.GridHash));
            Assert.That(plain.Report.WaterCells, Is.Zero);

            // And the barren board, which is the baseline everything else is judged against, must
            // stay bone dry however the water is tuned.
            var tuned = Generate(21, g => { g.MakeBarren(); g.pondsPer10000Columns = 400; });
            Assert.That(tuned.Report.WaterCells, Is.Zero, "the bare board grew a pond");
        }

        // ---------------------------------------------------------------- the geometry

        [Test]
        public void WaterSitsOneLayerBelowTheGroundAroundIt()
        {
            var result = River(8);
            var ctx = result.Context;

            int checkedBanks = 0;
            for (int z = 0; z < ctx.Size.SizeZ; z++)
            for (int x = 0; x < ctx.Size.SizeX; x++)
            {
                int column = ctx.Column(x, z);
                if (!IsChannel(ctx, column)) continue;

                // The bed is the column's own solid top: nothing stands in a channel.
                Assert.That(ctx.TopSolidY[column], Is.EqualTo(ctx.SurfaceY[column]),
                    $"something is standing in the channel at {x},{z}");

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + StepX[d], nz = z + StepZ[d];
                    if ((uint)nx >= (uint)ctx.Size.SizeX || (uint)nz >= (uint)ctx.Size.SizeZ) continue;

                    int n = ctx.Column(nx, nz);
                    if (IsChannel(ctx, n)) continue;

                    Assert.That(ctx.SurfaceY[n], Is.EqualTo(ctx.SurfaceY[column] + 1),
                        $"the bank at {nx},{nz} is not one step above the water at {x},{z}");
                    checkedBanks++;
                }
            }

            Assert.That(checkedBanks, Is.GreaterThan(0), "the map has no banks to check");
        }

        [Test]
        public void DeepWaterIsNotWalkableAndNothingStandsOnIt()
        {
            var result = River(12);
            var ctx = result.Context;
            int deep = 0;

            for (int column = 0; column < ctx.Columns; column++)
            {
                if (ctx.Water[column] != (byte)WaterClass.Deep) continue;
                int x = column % ctx.Size.SizeX, z = column / ctx.Size.SizeX;
                int cell = ctx.Index(x, z, ctx.SurfaceY[column] + 1);

                Assert.That(ctx.Grid.Terrain[cell], Is.EqualTo(NaturalContent.TerrainDeepWater));
                Assert.That(ctx.Grid.IsWalkable(cell), Is.False, $"deep water at {x},{z} is walkable");

                // And nobody walks over the top of it either, which is the half a `solid` flag
                // would have got wrong.
                int above = cell + ctx.Size.LayerStride;
                if (above < ctx.Size.CellCount)
                    Assert.That(ctx.Grid.IsWalkable(above), Is.False, $"the surface of the water at {x},{z} is walkable");
                deep++;
            }

            Assert.That(deep, Is.GreaterThan(0), "a river with no deep water in it");
        }

        [Test]
        public void ShallowWaterIsWalkableAndMarshIsOrdinaryGround()
        {
            var result = Generate(44);
            var ctx = result.Context;
            int shallow = 0, marsh = 0;

            for (int column = 0; column < ctx.Columns; column++)
            {
                int x = column % ctx.Size.SizeX, z = column / ctx.Size.SizeX;
                var kind = (WaterClass)ctx.Water[column];

                if (kind == WaterClass.Shallow)
                {
                    int cell = ctx.Index(x, z, ctx.SurfaceY[column] + 1);
                    Assert.That(ctx.Grid.Terrain[cell], Is.EqualTo(NaturalContent.TerrainShallowWater));
                    Assert.That(ctx.Grid.IsWalkable(cell), Is.True, $"shallow water at {x},{z} cannot be waded");
                    shallow++;
                }
                else if (kind == WaterClass.Marsh)
                {
                    int cell = ctx.Index(x, z, ctx.SurfaceY[column]);
                    Assert.That(ctx.Grid.Terrain[cell], Is.EqualTo(NaturalContent.TerrainMarsh));
                    Assert.That(ctx.Grid.IsSolidTerrain(cell), Is.True, "marsh is solid ground");
                    Assert.That(ctx.Grid.IsWalkable(cell + ctx.Size.LayerStride), Is.True,
                        $"the bog at {x},{z} cannot be crossed");
                    marsh++;
                }
            }

            Assert.That(shallow, Is.GreaterThan(0));
            Assert.That(marsh, Is.GreaterThan(0));
        }

        [Test]
        public void AStreamIsWadeableEndToEnd()
        {
            // The depth rule has one bound, and this is what it buys: a brook of one to three
            // cells is shallow the whole way across, so it slows a colonist without ever being a
            // decision. Anything deep in a stream means deepShoreDistance has been lowered.
            for (uint seed = 1; seed <= 40; seed++)
            {
                var result = Generate(seed, g => g.riverChancePerMille = 0);
                var ctx = result.Context;
                for (int column = 0; column < ctx.Columns; column++)
                    if (ctx.Water[column] == (byte)WaterClass.Deep && InAShapeOfKind(ctx, WaterShapeKind.Stream))
                        AssertNotInAStream(ctx, column, seed);
            }
        }

        static bool InAShapeOfKind(NaturalGenContext ctx, WaterShapeKind kind)
        {
            foreach (var shape in ctx.WaterShapes) if (shape.Kind == kind) return true;
            return false;
        }

        /// <summary>
        /// Deep water on a river-free map is allowed — a pond has a middle — but a stream must
        /// not have one, and a stream is at most three cells across, so a deep cell with dry land
        /// within two steps in both directions along either axis came from a stream.
        /// </summary>
        static void AssertNotInAStream(NaturalGenContext ctx, int column, uint seed)
        {
            int x = column % ctx.Size.SizeX, z = column / ctx.Size.SizeX;
            Assert.That(WaterWidth(ctx, x, z, 1, 0) > 3 || WaterWidth(ctx, x, z, 0, 1) > 3, Is.True,
                $"seed {seed}: deep water at {x},{z} sits in a body no more than three cells across");
        }

        static int WaterWidth(NaturalGenContext ctx, int x, int z, int dx, int dz)
        {
            int width = 1;
            for (int s = 1; ; s++)
            {
                int nx = x + dx * s, nz = z + dz * s;
                if ((uint)nx >= (uint)ctx.Size.SizeX || (uint)nz >= (uint)ctx.Size.SizeZ) break;
                if (!IsChannel(ctx, ctx.Column(nx, nz))) break;
                width++;
            }
            for (int s = 1; ; s++)
            {
                int nx = x - dx * s, nz = z - dz * s;
                if ((uint)nx >= (uint)ctx.Size.SizeX || (uint)nz >= (uint)ctx.Size.SizeZ) break;
                if (!IsChannel(ctx, ctx.Column(nx, nz))) break;
                width++;
            }
            return width;
        }

        // ---------------------------------------------------------------- connectivity

        [Test]
        public void TheColonyCanReachMostOfItsMap()
        {
            var forced = new Dictionary<int, int>();

            for (uint seed = 1; seed <= 60; seed++)
            {
                var result = Generate(seed);
                var report = result.Report;
                Assert.That(report.WalkableColumns, Is.GreaterThan(0));

                int percent = report.ReachableColumns * 100 / report.WalkableColumns;
                Assert.That(percent, Is.GreaterThanOrEqualTo(80),
                    $"seed {seed}: the start reaches only {percent}% of the board");

                forced.TryGetValue(report.ForcedFords, out int count);
                forced[report.ForcedFords] = count + 1;
            }

            TestContext.WriteLine("forced fords: " + string.Join(", ", Histogram(forced)));
        }

        static IEnumerable<string> Histogram(Dictionary<int, int> counts)
        {
            var keys = new List<int>(counts.Keys);
            keys.Sort();
            foreach (int key in keys) yield return $"{key}x{counts[key]}";
        }

        [Test]
        public void AWideRiverAlwaysHasACrossing()
        {
            for (uint seed = 1; seed <= 40; seed++)
            {
                var result = River(seed);
                var ctx = result.Context;
                if (ctx.RiverSteps <= 0) continue;

                Assert.That(result.Report.Fords, Is.GreaterThan(0), $"seed {seed}: a river with no ford");

                // The crossing has to be real, not merely counted: flood the surface from the
                // start and insist both banks of the river came out in the same component.
                var reached = FloodFromStart(ctx);
                bool joined = false;
                for (int step = 0; step < ctx.RiverSteps && !joined; step++)
                {
                    int cross = ctx.RiverCross![step], half = ctx.RiverHalfWidth![step];
                    int near = BankOf(ctx, step, cross - half - 1);
                    int far = BankOf(ctx, step, cross + half + 1);
                    joined = near >= 0 && far >= 0 && reached[near] && reached[far];
                }

                Assert.That(joined, Is.True, $"seed {seed}: no march step has both banks on the colony's side");
            }
        }

        [Test]
        public void AForcedFordIsRareAndBounded()
        {
            int forced = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var result = River(seed);
                Assert.That(result.Report.ForcedFords, Is.LessThanOrEqualTo(3));
                if (result.Report.ForcedFords > 0) forced++;
            }

            // Fords cut by construction should be doing the work; the reachability check is the
            // backstop, not the mechanism.
            Assert.That(forced, Is.LessThan(20), $"{forced} of 40 rivers needed rescuing");
            TestContext.WriteLine($"{forced} of 40 rivers needed a forced ford");
        }

        [Test]
        public void TheStartIsNeverInWaterOrBog()
        {
            for (uint seed = 1; seed <= 20; seed++)
            {
                var result = Generate(seed);
                var ctx = result.Context;
                var start = result.StartCell;

                int reach = ctx.Gen.startClearingRadius + ctx.Gen.startWaterClearance;
                for (int dz = -reach; dz <= reach; dz++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int x = start.X + dx, z = start.Z + dz;
                    if ((uint)x >= (uint)ctx.Size.SizeX || (uint)z >= (uint)ctx.Size.SizeZ) continue;
                    Assert.That(ctx.Water[ctx.Column(x, z)], Is.EqualTo((byte)WaterClass.None),
                        $"seed {seed}: the colony lands within {reach} cells of water at {x},{z}");
                }
            }
        }

        // ---------------------------------------------------------------- movement

        [Test]
        public void WadingCostsThreeTimesWalking()
        {
            var size = new GridSize(8, 8, 6);
            var grid = new CellGrid(size);
            var nav = new NavGrid(size);
            nav.SetTerrainCosts(ClassTable(), CostTable());

            // A little board laid by hand: solid ground at layer 1, one bog column, and one
            // channel a layer down with water in it.
            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
            {
                Set(grid, size.Index(x, z, 0), NaturalContent.TerrainSubsoil);
                Set(grid, size.Index(x, z, 1), NaturalContent.TerrainGrass);
            }

            Set(grid, size.Index(3, 3, 1), NaturalContent.TerrainMarsh);
            Set(grid, size.Index(5, 3, 1), NaturalContent.TerrainSand);
            Set(grid, size.Index(5, 3, 2), NaturalContent.TerrainShallowWater);

            for (int i = 0; i < size.CellCount; i++) nav.RefreshFrom(grid, i);

            int clear = nav.EnterCost(size.Index(1, 1, 2), TraverseMode.Colonist);
            int bog = nav.EnterCost(size.Index(3, 3, 2), TraverseMode.Colonist);
            int wade = nav.EnterCost(size.Index(5, 3, 2), TraverseMode.Colonist);

            Assert.That(clear, Is.EqualTo(MoveCost.Orthogonal), "clear ground is the unit of cost");
            Assert.That(wade, Is.EqualTo(3 * MoveCost.Orthogonal), "wading is not a third of walking");

            // Marsh is charged on the cell *above* it, which is the half that fails silently if
            // the lookup is written the other way round.
            Assert.That(bog, Is.EqualTo(140), "crossing a bog costs the same as crossing grass");
        }

        [Test]
        public void APathWouldRatherGoRoundThanThroughTheWater()
        {
            var size = new GridSize(9, 5, 6);
            var grid = new CellGrid(size);
            var nav = new NavGrid(size);
            nav.SetTerrainCosts(ClassTable(), CostTable());

            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
            {
                Set(grid, size.Index(x, z, 0), NaturalContent.TerrainSubsoil);
                Set(grid, size.Index(x, z, 1), NaturalContent.TerrainGrass);
            }

            // A three-cell puddle straight across the middle row, leaving dry rows either side.
            for (int x = 3; x <= 5; x++)
            {
                Set(grid, size.Index(x, 2, 1), NaturalContent.TerrainSand);
                Set(grid, size.Index(x, 2, 2), NaturalContent.TerrainShallowWater);
            }

            for (int i = 0; i < size.CellCount; i++) nav.RefreshFrom(grid, i);

            // Straight through is three cells of water: 3 x 300. Round the top is two sideways
            // steps plus three dry ones: 5 x 100. Going round is cheaper, and the cost model is
            // what has to notice, not the caller.
            int through = 3 * nav.EnterCost(size.Index(4, 2, 2), TraverseMode.Colonist);
            int around = 5 * MoveCost.Orthogonal;
            Assert.That(around, Is.LessThan(through));
        }

        static byte[] ClassTable()
        {
            var table = new byte[NaturalContent.TerrainCount];
            for (ushort t = 0; t < table.Length; t++) table[t] = NaturalContent.CostClassOf(t);
            return table;
        }

        static int[] CostTable()
        {
            var costs = new int[256];
            NaturalContent.ApplyCostClasses(costs);
            return costs;
        }

        static void Set(CellGrid grid, int index, ushort terrain)
        {
            grid.Terrain[index] = terrain;
            if (NaturalContent.IsSolid(terrain)) grid.Flags[index] |= CellFlags.SolidTerrain;
            else grid.Flags[index] &= ~CellFlags.SolidTerrain;
            if (NaturalContent.IsImpassable(terrain)) grid.Flags[index] |= CellFlags.ImpassableTerrain;
            else grid.Flags[index] &= ~CellFlags.ImpassableTerrain;
        }

        /// <summary>
        /// The hand-built board above proves the arithmetic. This proves the wiring: a nav grid
        /// built the way the game builds one, over a map the generator actually made, charges
        /// three times as much to enter the water as to cross the grass beside it. Nothing here
        /// passes a cost table in — if the default had to be handed down by a caller, this is the
        /// test that would fail.
        /// </summary>
        [Test]
        public void ARealMapChargesThreeTimesToWadeThroughIt()
        {
            var result = Generate(5);
            var ctx = result.Context;
            var nav = new NavGraph(ctx.Grid);
            nav.Rebuild();   // the rebuild is what fills the per-cell classes, as in ColonyWorld.Build

            int waded = 0;
            for (int column = 0; column < ctx.Columns; column++)
            {
                if (ctx.Water[column] != (byte)WaterClass.Shallow) continue;
                int x = column % ctx.Size.SizeX, z = column / ctx.Size.SizeX;

                int cell = ctx.Index(x, z, ctx.SurfaceY[column] + 1);
                Assert.That(nav.Grid.EnterCost(cell, TraverseMode.Colonist),
                    Is.EqualTo(3 * MoveCost.Orthogonal), $"wading at {x},{z} is not a third of walking");
                waded++;
            }

            Assert.That(waded, Is.GreaterThan(0), "the map has no water to wade");

            // And the grass is still the unit it always was, which is the other half of the
            // claim: water is slow, not everything.
            var start = result.StartCell;
            Assert.That(nav.Grid.EnterCost(ctx.Index(start.X, start.Z, start.Y), TraverseMode.Colonist),
                Is.EqualTo(MoveCost.Orthogonal));
        }

        /// <summary>
        /// The reachability check is a backstop, and a backstop that never fires is indistinguishable
        /// from one that does not work. Measured, no river in forty needs it — so it is provoked
        /// here by taking away the fords cut by construction, which is the only way to find out
        /// whether it would catch a map that really was cut in two.
        /// </summary>
        [Test]
        public void TheBackstopReconnectsAMapThatWasGenuinelySevered()
        {
            int rescued = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var result = Generate(seed, g =>
                {
                    g.riverChancePerMille = 1000;
                    g.riverFords = 0;         // no crossing by construction: sink or swim
                    g.riverMinHalfWidth = 4;  // and a river far too wide to be an accident
                    g.riverMaxHalfWidth = 6;
                });

                var report = result.Report;
                int percent = report.ReachableColumns * 100 / report.WalkableColumns;
                Assert.That(percent, Is.GreaterThanOrEqualTo(80),
                    $"seed {seed}: a fordless river left the colony {percent}% of the board");
                if (report.ForcedFords > 0) rescued++;
            }

            Assert.That(rescued, Is.GreaterThan(0),
                "no map needed rescuing even with the fords switched off, so this proves nothing");
            TestContext.WriteLine($"{rescued} of 40 fordless rivers were reconnected by the backstop");
        }

        /// <summary>Nothing is ordered in water, and the colony is not unpacked in it either.</summary>
        [Test]
        public void NothingIsPutInTheWater()
        {
            var result = Generate(5);
            var ctx = result.Context;
            var designations = new DesignationGrid(ctx.Grid, ctx.Edifices);

            int refused = 0;
            for (int column = 0; column < ctx.Columns; column++)
            {
                if (!IsChannel(ctx, column)) continue;
                int x = column % ctx.Size.SizeX, z = column / ctx.Size.SizeX;
                int cell = ctx.Index(x, z, ctx.SurfaceY[column] + 1);

                Assert.That(designations.IsWater(cell), Is.True);
                foreach (DesignationKind kind in Enum.GetValues(typeof(DesignationKind)))
                    Assert.That(designations.Allows(cell, kind), Is.False,
                        $"{kind} was allowed in the water at {x},{z}");
                refused++;
            }

            Assert.That(refused, Is.GreaterThan(0));

            // The colony's own gear goes on dry land. Shallow water is walkable, so this is the
            // case that would otherwise slip through: a bed standing in a stream.
            var spots = ColonyScenario.FindStartSpots(ctx.Grid, result.StartCell, 64);
            Assert.That(spots, Is.Not.Empty);
            foreach (int spot in spots)
                Assert.That(NaturalContent.IsWater(ctx.Grid.Terrain[spot]), Is.False,
                    "the colony was unpacked in the water");
        }

        // ---------------------------------------------------------------- helpers

        static readonly int[] StepX = { -1, 1, 0, 0 };
        static readonly int[] StepZ = { 0, 0, -1, 1 };

        static bool IsChannel(NaturalGenContext ctx, int column)
        {
            byte w = ctx.Water[column];
            return w == (byte)WaterClass.Shallow || w == (byte)WaterClass.Deep;
        }

        static int BankOf(NaturalGenContext ctx, int step, int cross)
        {
            int x = ctx.RiverAlongX ? step : cross;
            int z = ctx.RiverAlongX ? cross : step;
            if ((uint)x >= (uint)ctx.Size.SizeX || (uint)z >= (uint)ctx.Size.SizeZ) return -1;
            return ctx.Column(x, z);
        }

        /// <summary>The surface columns a colonist can walk to from the start, the same way the
        /// generator's own reachability check does it.</summary>
        static bool[] FloodFromStart(NaturalGenContext ctx)
        {
            var seen = new bool[ctx.Columns];
            var queue = new int[ctx.Columns];
            int head = 0, tail = 0;

            int start = ctx.Column(ctx.Report.StartCell.X, ctx.Report.StartCell.Z);
            seen[start] = true;
            queue[tail++] = start;

            while (head < tail)
            {
                int column = queue[head++];
                int x = column % ctx.Size.SizeX, z = column / ctx.Size.SizeX;

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + StepX[d], nz = z + StepZ[d];
                    if ((uint)nx >= (uint)ctx.Size.SizeX || (uint)nz >= (uint)ctx.Size.SizeZ) continue;

                    int n = ctx.Column(nx, nz);
                    if (seen[n]) continue;
                    if (ctx.Water[n] == (byte)WaterClass.Deep) continue;
                    if (ctx.TopSolidY[n] != ctx.SurfaceY[n]) continue;
                    if (Math.Abs(ctx.SurfaceY[n] - ctx.SurfaceY[column]) > 1) continue;

                    seen[n] = true;
                    queue[tail++] = n;
                }
            }

            return seen;
        }
    }
}
