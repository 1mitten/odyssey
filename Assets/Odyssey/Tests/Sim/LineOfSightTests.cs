#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A shot's line in three dimensions (design 47 §2b, §5): what blocks it, the corner rule, the
    /// slab between layers, and the two properties the whole design leans on — symmetry, and a
    /// mirrored board giving mirrored answers. Each rule carries the control that tells a working
    /// rule from a missing one; the strict corner rule the design rejects is written here, as
    /// <see cref="StrictClear"/>, and never in production.
    /// </summary>
    public class LineOfSightTests
    {
        /// <summary>
        /// A hand-made board: layer 0 solid rock, the ground at layer 1, open air above. The nav
        /// graph is there for its door flags and nothing else.
        /// </summary>
        sealed class Board
        {
            public readonly CellGrid Cells;
            public readonly NavGraph Nav;

            public Board(int sx = 12, int sz = 12, int sy = 4, int rockLayers = 1)
            {
                Cells = new CellGrid(new GridSize(sx, sz, sy));
                for (int y = 0; y < rockLayers; y++)
                for (int z = 0; z < sz; z++)
                for (int x = 0; x < sx; x++)
                    Rock(x, z, y);
                Nav = new NavGraph(Cells);
            }

            public GridSize Size => Cells.Size;
            public int C(int x, int z, int y = 1) => Cells.Index(x, z, y);
            public void Rock(int x, int z, int y) => Cells.Flags[C(x, z, y)] |= CellFlags.SolidTerrain;
            public void Dig(int x, int z, int y) => Cells.Flags[C(x, z, y)] &= ~CellFlags.SolidTerrain;
            public void Wall(int x, int z, int y = 1) => Cells.Flags[C(x, z, y)] |= CellFlags.BlockingEdifice;
            public void Unwall(int x, int z, int y = 1) => Cells.Flags[C(x, z, y)] &= ~CellFlags.BlockingEdifice;
            public void Slab(int x, int z, int y) => Cells.Floor[C(x, z, y)] = 1;

            public void Door(int x, int z, int y, bool open)
            {
                Wall(x, z, y);
                Nav.SetDoor(C(x, z, y), isDoor: true, open);
            }

            public bool Clear(int a, int b) => LineOfSight.Clear(Cells, Nav.Grid, a, b);
        }

        /// <summary>
        /// The rule the design rejects (§2b): every cell the line touches, corners included, must be
        /// open. The negative control for the lenient corner rule, and only ever that.
        /// </summary>
        static bool StrictClear(CellGrid cells, NavGrid? nav, int from, int to)
        {
            var line = new SightLine();
            LineOfSight.Walk(cells.Size, from, to, line);
            int stride = cells.Size.LayerStride;
            for (int i = 1; i < line.Count; i++)
            {
                int cell = line.Cells[i];
                if (cell != to && LineOfSight.Blocks(cells, nav, cell)) return false;
            }
            // Every vertical crossing between two touched cells stacked in one column.
            var touched = new HashSet<int>(line.Cells);
            foreach (int cell in touched)
                if (touched.Contains(cell + stride) && LineOfSight.SlabBetween(cells, cell, cell + stride)) return false;
            return true;
        }

        // ---- what blocks -------------------------------------------------------------------------

        [Test]
        public void OpenGroundIsClear()
        {
            var b = new Board();
            Assert.That(b.Clear(b.C(1, 1), b.C(9, 6)), Is.True, "across open ground");
            Assert.That(b.Clear(b.C(2, 5), b.C(8, 5)), Is.True, "along a row");
            Assert.That(b.Clear(b.C(4, 4), b.C(4, 4)), Is.True, "a cell to itself");
            Assert.That(b.Clear(b.C(2, 2, 3), b.C(9, 7, 1)), Is.True, "down through open air");
        }

        [Test]
        public void AWallBlocks()
        {
            var b = new Board();
            int from = b.C(2, 5), to = b.C(8, 5);
            b.Wall(5, 5);
            Assert.That(b.Clear(from, to), Is.False, "a wall on the line");
            Assert.That(b.Clear(to, from), Is.False, "and from the other end");

            // Control: the same wall a row off the line stops nothing.
            b.Unwall(5, 5);
            b.Wall(5, 6);
            Assert.That(b.Clear(from, to), Is.True, "a wall beside the line");
        }

        [Test]
        public void AClosedDoorBlocksAndTheSameDoorOpenedPasses()
        {
            var b = new Board();
            int from = b.C(2, 5), to = b.C(8, 5);
            b.Door(5, 5, 1, open: false);
            Assert.That(b.Clear(from, to), Is.False, "a closed door");

            b.Nav.SetDoorOpen(b.C(5, 5), true);
            Assert.That(b.Clear(from, to), Is.True, "the same door, open");

            // Control: open is a nav flag, so a grid read without the nav cannot see it — the
            // blocking flag alone is a wall.
            Assert.That(LineOfSight.Clear(b.Cells, null, from, to), Is.False, "without the nav grid, a wall");
        }

        [Test]
        public void TheEndCellsAreNeverTested()
        {
            var b = new Board();
            int from = b.C(2, 5), to = b.C(8, 5);
            b.Wall(2, 5);
            b.Door(8, 5, 1, open: false);
            Assert.That(b.Clear(from, to), Is.True, "a shooter on an edifice, a target in a shut doorway");
            Assert.That(b.Clear(to, from), Is.True, "and the other way");

            // Control: the same door one cell short of the target is on the line, and blocks.
            Assert.That(b.Clear(from, b.C(9, 5)), Is.False, "the door between");
        }

        [Test]
        public void SolidRockBetweenTwoCellarsBlocks()
        {
            var b = new Board(rockLayers: 3);
            b.Dig(2, 5, 1);
            b.Dig(8, 5, 1);
            int from = b.C(2, 5), to = b.C(8, 5);
            Assert.That(b.Clear(from, to), Is.False, "rock between the cellars");

            // Control: a tunnel dug between them opens the line.
            for (int x = 3; x < 8; x++) b.Dig(x, 5, 1);
            Assert.That(b.Clear(from, to), Is.True, "the tunnel dug through");
        }

        [Test]
        public void ATreeBlocksNothing()
        {
            // A tree is an edifice with no blocking flag: it stands in the cell and stops no path.
            var b = new Board();
            b.Cells.Edifice[b.C(5, 5)] = 0;
            Assert.That(b.Clear(b.C(2, 5), b.C(8, 5)), Is.True);
        }

        // ---- the corner rule ----------------------------------------------------------------------

        [Test]
        public void TwoWallCornersTouchingDiagonallyBlockAndOneAloneDoesNot()
        {
            var b = new Board();
            int from = b.C(2, 2), to = b.C(6, 6);
            b.Wall(4, 3);
            b.Wall(3, 4);
            Assert.That(b.Clear(from, to), Is.False, "both routes round the corner shut");
            Assert.That(b.Clear(to, from), Is.False, "and from the other end");

            // Control: one corner removed, one route is open, and the lenient rule passes it.
            b.Unwall(3, 4);
            Assert.That(b.Clear(from, to), Is.True, "one route open");
            Assert.That(b.Clear(to, from), Is.True, "one route open, the other way");

            // The rule withheld: the strict rule blocks on the remaining corner.
            Assert.That(StrictClear(b.Cells, b.Nav.Grid, from, to), Is.False, "strict blocks a line one route clears");
        }

        [Test]
        public void AShooterOnATerraceTopSeesATargetOnTheGroundBelow()
        {
            var colony = CombatFixture.Board();
            PawnContext ctx = colony.Pawns;
            CellGrid cells = ctx.Cells;
            CellRef start = colony.Start;
            int g = start.Y, x = start.X + 5, z = start.Z;

            // A raised column: the ground cell under the shooter is filled with rock.
            int column = cells.Index(x, z, g);
            cells.Flags[column] |= CellFlags.SolidTerrain;
            int shooter = cells.Index(x, z, g + 1);
            int target = cells.Index(x + 1, z, g);
            Assume.That(cells.IsWalkable(shooter) && cells.IsWalkable(target), "both ends stand on something");
            Assume.That(cells.Floor[cells.Index(x + 1, z, g + 1)], Is.EqualTo(0), "open air over the target");

            Assert.That(LineOfSight.Clear(ctx, shooter, target), Is.True, "down off the lip");
            Assert.That(LineOfSight.Clear(ctx, target, shooter), Is.True, "and back up at her");

            // The rule withheld: the line passes exactly through the edge the rock shares with the
            // air, so the strict rule blocks the commonest shot on the map.
            Assert.That(StrictClear(cells, ctx.Nav.Grid, shooter, target), Is.False, "strict blocks the terrace shot");

            // A terrace two cells wide: the line dips into the rock before it clears the rim.
            cells.Flags[cells.Index(x + 1, z, g)] |= CellFlags.SolidTerrain;
            int foot = cells.Index(x + 2, z, g);
            Assert.That(LineOfSight.Clear(ctx, shooter, foot), Is.False, "the rim intercepts a shot at the foot of a wide terrace");
        }

        [Test]
        public void ASlabOnTheLayerBetweenThemBlocks()
        {
            var colony = CombatFixture.Board();
            PawnContext ctx = colony.Pawns;
            CellGrid cells = ctx.Cells;
            CellRef start = colony.Start;
            int g = start.Y, x = start.X + 5, z = start.Z;
            cells.Flags[cells.Index(x, z, g)] |= CellFlags.SolidTerrain;
            int shooter = cells.Index(x, z, g + 1);
            int target = cells.Index(x + 1, z, g);
            Assume.That(LineOfSight.Clear(ctx, shooter, target), Is.True, "the terrace shot, before the slab");

            // A slab over the target — the floor of the cell above it, which is its roof.
            cells.Floor[cells.Index(x + 1, z, g + 1)] = 1;
            Assert.That(LineOfSight.Clear(ctx, shooter, target), Is.False, "a roof over the target");
            Assert.That(LineOfSight.Clear(ctx, target, shooter), Is.False, "and from under it");

            // Straight down one column: the last step into the target is tested too.
            int above = cells.Index(x + 1, z, g + 1);
            Assert.That(LineOfSight.Clear(ctx, above, target), Is.False, "through the slab, straight down");
            cells.Floor[above] = 0;
            Assert.That(LineOfSight.Clear(ctx, above, target), Is.True, "control: the slab lifted");
        }

        // ---- the walk -----------------------------------------------------------------------------

        [Test]
        public void TheWalkVisitsBothCornerCellsAtAnExactTie()
        {
            var size = new GridSize(8, 8, 4);
            var line = new SightLine();
            int n = LineOfSight.Walk(size, size.Index(2, 2, 1), size.Index(4, 4, 1), line);

            int[] expected =
            {
                size.Index(2, 2, 1),
                size.Index(3, 2, 1), size.Index(2, 3, 1),
                size.Index(3, 3, 1),
                size.Index(4, 3, 1), size.Index(3, 4, 1),
                size.Index(4, 4, 1),
            };
            Assert.That(n, Is.EqualTo(expected.Length));
            Assert.That(line.Cells, Is.EqualTo(expected));
            bool[] corner = { false, true, true, false, true, true, false };
            for (int i = 0; i < n; i++) Assert.That(line.IsCorner(i), Is.EqualTo(corner[i]), $"cell {i}");
            Assert.That(line.Steps[3], Is.EqualTo(LineOfSight.StepX | LineOfSight.StepZ), "the diagonal step names both axes");

            // A vertex: all three axes at once, six corner cells round it.
            LineOfSight.Walk(size, size.Index(0, 0, 0), size.Index(1, 1, 1), line);
            Assert.That(line.Count, Is.EqualTo(8));
            Assert.That(new HashSet<int>(line.Cells).Count, Is.EqualTo(8), "every cell of the 2 × 2 × 2 block, once");
            Assert.That(line.Steps[7], Is.EqualTo(LineOfSight.StepX | LineOfSight.StepY | LineOfSight.StepZ));
        }

        [Test]
        public void TheWalkIsEveryCellTheSegmentEntersAndTouches()
        {
            // An oracle that knows nothing of the stepping: a cell is on the line when the open
            // segment between the two centres meets its inside, and a corner when it meets only
            // its boundary. Checked over every cell of the box the two ends span.
            var size = new GridSize(14, 14, 10);
            var rng = new Random(4711);
            var line = new SightLine();
            int edges = 0, vertices = 0;
            for (int trial = 0; trial < 400; trial++)
            {
                CellRef a = RandomCell(rng, size), b = RandomCell(rng, size);
                // Half the pairs short, where small equal and multiple extents make ties common.
                if ((trial & 1) != 0)
                    b = new CellRef(Clamp(a.X + rng.Next(-4, 5), size.SizeX), Clamp(a.Z + rng.Next(-4, 5), size.SizeZ),
                        Clamp(a.Y + rng.Next(-4, 5), size.SizeY));
                int from = size.Index(a), to = size.Index(b);
                LineOfSight.Walk(size, from, to, line);

                var inside = new HashSet<int>();
                var corners = new HashSet<int>();
                for (int i = 0; i < line.Count; i++)
                {
                    (line.IsCorner(i) ? corners : inside).Add(line.Cells[i]);
                    if (line.IsCorner(i)) continue;
                    if (line.Steps[i] == (LineOfSight.StepX | LineOfSight.StepY | LineOfSight.StepZ)) vertices++;
                    else if (line.Steps[i] != 0 && (line.Steps[i] & (line.Steps[i] - 1)) != 0) edges++;
                }
                Assert.That(inside.Count + corners.Count, Is.EqualTo(line.Count), "no cell listed twice");

                int met = 0;
                for (int y = Math.Min(a.Y, b.Y); y <= Math.Max(a.Y, b.Y); y++)
                for (int z = Math.Min(a.Z, b.Z); z <= Math.Max(a.Z, b.Z); z++)
                for (int x = Math.Min(a.X, b.X); x <= Math.Max(a.X, b.X); x++)
                {
                    var c = new CellRef(x, z, y);
                    int cell = size.Index(c);
                    bool enters = Meets(a, b, c, closed: false);
                    bool touches = Meets(a, b, c, closed: true);
                    if (touches) met++;
                    Assert.That(inside.Contains(cell), Is.EqualTo(enters), $"{a} → {b}: {c} entered");
                    Assert.That(corners.Contains(cell), Is.EqualTo(touches && !enters), $"{a} → {b}: {c} touched only");
                }
                Assert.That(line.Count, Is.EqualTo(met), $"{a} → {b}: nothing listed outside the box the ends span");

                // Walked the other way: the same cells, the inside ones in reverse order.
                var back = new SightLine();
                LineOfSight.Walk(size, to, from, back);
                var forwardInside = new List<int>();
                var backInside = new List<int>();
                for (int i = 0; i < line.Count; i++) if (!line.IsCorner(i)) forwardInside.Add(line.Cells[i]);
                for (int i = back.Count - 1; i >= 0; i--) if (!back.IsCorner(i)) backInside.Add(back.Cells[i]);
                Assert.That(backInside, Is.EqualTo(forwardInside), $"{a} → {b} reversed");
                Assert.That(new HashSet<int>(back.Cells).SetEquals(line.Cells), Is.True, $"{a} → {b}: the same cells back");
            }
            Assert.That(edges, Is.GreaterThan(20), "edge ties walked");
            Assert.That(vertices, Is.GreaterThan(5), "vertex ties walked");
        }

        /// <summary>
        /// Does the segment between the centres of <paramref name="a"/> and <paramref name="b"/>
        /// meet cell <paramref name="c"/> — its open inside, or with <paramref name="closed"/> its
        /// closed box? Doubled coordinates, so a centre is odd and a face even; each axis gives an
        /// interval of the parameter, compared as exact fractions.
        /// </summary>
        static bool Meets(CellRef a, CellRef b, CellRef c, bool closed)
        {
            // The parameter interval as fractions lo = ln/ld, hi = hn/hd, starting at the whole [0, 1].
            long ln = 0, ld = 1, hn = 1, hd = 1;
            int[] from = { a.X, a.Y, a.Z }, to = { b.X, b.Y, b.Z }, at = { c.X, c.Y, c.Z };
            for (int i = 0; i < 3; i++)
            {
                long p = 2L * from[i] + 1, d = 2L * (to[i] - from[i]);
                long lower = 2L * at[i], upper = 2L * at[i] + 2;
                if (d == 0)
                {
                    bool inAxis = closed ? lower <= p && p <= upper : lower < p && p < upper;
                    if (!inAxis) return false;
                    continue;
                }
                // t = (face − p) / d for each face; order the two by the sign of d.
                long n1 = lower - p, n2 = upper - p, den = d;
                if (den < 0)
                {
                    den = -den;
                    n1 = -n1;
                    n2 = -n2;
                    (n1, n2) = (n2, n1);
                }
                if (n1 * ld > ln * den) { ln = n1; ld = den; }
                if (n2 * hd < hn * den) { hn = n2; hd = den; }
            }
            long l = ln * hd, h = hn * ld;
            return closed ? l <= h : l < h;
        }

        // ---- the two properties ------------------------------------------------------------------

        [Test]
        public void SymmetricAndMirroredOnThePlayedMap()
        {
            var size = new GridSize(60, 60, 16);
            CellGrid grid = PlayedMap.Generate(size, 20260925u);
            var (clear, blocked) = AssertSymmetricAndMirrored(grid, doors: null, seed: 17);
            TestContext.Progress.WriteLine($"played map: {clear} clear, {blocked} blocked of 500");
        }

        [Test]
        public void SymmetricAndMirroredWithEveryBlockerOnTheBoard()
        {
            // The played map has no walls, slabs or doors of its own; sprinkle them on its surface so
            // every rule is on some line.
            var size = new GridSize(60, 60, 16);
            CellGrid grid = PlayedMap.Generate(size, 20260926u);
            var rng = new Random(99);
            var doors = new Dictionary<int, bool>();
            for (int i = 0; i < size.CellCount; i++)
            {
                if (!grid.IsWalkable(i)) continue;
                int roll = rng.Next(100);
                if (roll < 4) grid.Flags[i] |= CellFlags.BlockingEdifice;
                else if (roll < 6)
                {
                    grid.Flags[i] |= CellFlags.BlockingEdifice;
                    doors[i] = rng.Next(2) == 0;
                }
                int above = i + size.LayerStride;
                if (above < size.CellCount && rng.Next(100) < 6) grid.Floor[above] = 1;
            }
            var (clear, blocked) = AssertSymmetricAndMirrored(grid, doors, seed: 23);
            TestContext.Progress.WriteLine($"with blockers: {clear} clear, {blocked} blocked of 500");

            // Control: the property has teeth. A rule that settles a tie by one route only — the
            // first corner in axis order, as a walk that "picks an axis" would — answers some of
            // these same pairs differently from the two ends.
            NavGraph nav = new NavGraph(grid);
            foreach (var door in doors) nav.SetDoor(door.Key, true, door.Value);
            var line = new SightLine();
            var pick = new Random(23);
            int lopsided = 0;
            for (int trial = 0; trial < 20_000 && lopsided == 0; trial++)
            {
                CellRef a = RandomCell(pick, size);
                var b = new CellRef(Clamp(a.X + pick.Next(-6, 7), size.SizeX), Clamp(a.Z + pick.Next(-6, 7), size.SizeZ),
                    Clamp(a.Y + pick.Next(-2, 3), size.SizeY));
                int from = size.Index(a), to = size.Index(b);
                if (OneRouteClear(grid, nav.Grid, from, to, line) != OneRouteClear(grid, nav.Grid, to, from, line)) lopsided++;
            }
            Assert.That(lopsided, Is.GreaterThan(0), "a one-route tie rule is caught asymmetric");
        }

        /// <summary>
        /// The negative control for symmetry: at a tie, only the first corner cell listed counts as
        /// the route, and slabs are ignored. Walked from the other end, the first corner is a
        /// different cell.
        /// </summary>
        static bool OneRouteClear(CellGrid cells, NavGrid nav, int from, int to, SightLine line)
        {
            LineOfSight.Walk(cells.Size, from, to, line);
            bool firstCorner = true;
            for (int i = 1; i < line.Count; i++)
            {
                int cell = line.Cells[i];
                if (line.IsCorner(i))
                {
                    if (firstCorner && LineOfSight.Blocks(cells, nav, cell)) return false;
                    firstCorner = false;
                    continue;
                }
                firstCorner = true;
                if (cell != to && LineOfSight.Blocks(cells, nav, cell)) return false;
            }
            return true;
        }

        /// <summary>
        /// 500 random pairs of standable cells within twenty cells of each other: <c>Clear(a, b)</c>
        /// equals <c>Clear(b, a)</c>, and on the board mirrored in x the mirrored pair gets the same
        /// answer. Both answers must turn up often, or the property says nothing.
        /// </summary>
        static (int clear, int blocked) AssertSymmetricAndMirrored(CellGrid grid, Dictionary<int, bool>? doors, int seed)
        {
            GridSize size = grid.Size;
            NavGraph? nav = null, mirrorNav = null;
            CellGrid mirror = MirrorX(grid);
            if (doors != null)
            {
                nav = new NavGraph(grid);
                mirrorNav = new NavGraph(mirror);
                foreach (var door in doors)
                {
                    nav.SetDoor(door.Key, true, door.Value);
                    mirrorNav.SetDoor(MirrorX(size, door.Key), true, door.Value);
                }
            }

            var standable = new List<int>();
            for (int i = 0; i < size.CellCount; i++)
                if (grid.IsWalkable(i) || (doors != null && doors.ContainsKey(i))) standable.Add(i);
            Assume.That(standable.Count, Is.GreaterThan(1000));

            var rng = new Random(seed);
            int clear = 0, blocked = 0;
            while (clear + blocked < 500)
            {
                int a = standable[rng.Next(standable.Count)], b = standable[rng.Next(standable.Count)];
                CellRef p = size.FromIndex(a), q = size.FromIndex(b);
                if (Math.Abs(p.X - q.X) > 20 || Math.Abs(p.Z - q.Z) > 20) continue;

                bool there = LineOfSight.Clear(grid, nav?.Grid, a, b);
                bool back = LineOfSight.Clear(grid, nav?.Grid, b, a);
                bool mirrored = LineOfSight.Clear(mirror, mirrorNav?.Grid, MirrorX(size, a), MirrorX(size, b));
                Assert.That(back, Is.EqualTo(there), $"{p} ↔ {q}: symmetric");
                Assert.That(mirrored, Is.EqualTo(there), $"{p} → {q}: mirrored in x");
                if (there) clear++;
                else blocked++;
            }
            Assert.That(clear, Is.GreaterThan(50), "enough clear lines to test anything");
            Assert.That(blocked, Is.GreaterThan(50), "enough blocked lines to test anything");
            return (clear, blocked);
        }

        static int MirrorX(GridSize size, int cell)
        {
            CellRef c = size.FromIndex(cell);
            return size.Index(size.SizeX - 1 - c.X, c.Z, c.Y);
        }

        static CellGrid MirrorX(CellGrid grid)
        {
            GridSize size = grid.Size;
            var mirror = new CellGrid(size);
            for (int i = 0; i < size.CellCount; i++)
            {
                int m = MirrorX(size, i);
                mirror.Terrain[m] = grid.Terrain[i];
                mirror.Floor[m] = grid.Floor[i];
                mirror.Edifice[m] = grid.Edifice[i];
                mirror.Flags[m] = grid.Flags[i];
            }
            return mirror;
        }

        static int Clamp(int v, int n) => v < 0 ? 0 : v >= n ? n - 1 : v;

        static CellRef RandomCell(Random rng, GridSize size) =>
            new CellRef(rng.Next(size.SizeX), rng.Next(size.SizeZ), rng.Next(size.SizeY));

        // ---- the counter ----------------------------------------------------------------------------

        [Test]
        public void EveryWalkIsCounted()
        {
            var b = new Board();
            long before = LineOfSight.Walks;
            b.Clear(b.C(1, 1), b.C(9, 6));
            b.Clear(b.C(1, 1), b.C(1, 1));
            LineOfSight.Walk(b.Size, b.C(1, 1), b.C(9, 6), new SightLine());
            Assert.That(LineOfSight.Walks - before, Is.EqualTo(3));
        }
    }
}
