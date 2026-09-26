#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Whether a shot can pass from one cell to another, in three dimensions (design 47 §2b,
    /// <c>docs/research/c-3d-shot-line.md</c>). A static class beside <see cref="Melee"/>: the one
    /// owner of "can she see it", asked by the ranged driver, the bullet's landing and anything
    /// later that aims.
    ///
    /// <para><b>The line.</b> A straight segment between the two cell <b>centres</b>, both at
    /// mid-height — one rule for shooter and target, standing or lying. <see cref="Walk"/> lists
    /// every cell it passes through: an integer supercover, Amanatides–Woo in exact rationals. The
    /// k-th face crossed on an axis the line spans <c>n</c> cells of lies at the fraction
    /// <c>(2k + 1) / 2n</c> of the way along (a centre is half a cell from its first face), so which
    /// axis crosses next is one cross-multiplication in <c>long</c>: no division, no float. An axis
    /// scaling maps lines to lines and faces to faces, so the walk is in index space and the
    /// anisotropic cell (2.5 × 2.5 × 3.0 m) enters only the distance
    /// (<see cref="RangedGeometry"/>).</para>
    ///
    /// <para><b>What blocks</b> (<see cref="Clear(CellGrid, NavGrid, int, int)"/>), on entering a
    /// cell: solid terrain, and a blocking edifice — a wall, a pillar, a closed door — unless the nav
    /// grid marks it an open door. <b>The two end cells are never tested</b>, so a pawn in a doorway
    /// is hittable and a shooter may stand on anything she can stand on. Crossing between layers
    /// inside one column is blocked by a slab on the upper cell's <see cref="CellGrid.Floor"/>, which
    /// is where the grid keeps the roof of the lower cell — one read is the whole slab-and-roof rule,
    /// and it applies to the last step into the target too. Trees block nothing: they block no
    /// path either.</para>
    ///
    /// <para><b>The corner rule.</b> Where the segment passes exactly through an edge (two axes cross
    /// at once) or a vertex (three), it touches every cell round that corner without entering any
    /// of them but the next. <b>The shot passes if either monotone route round the corner is
    /// open</b> — each route its intermediate cells' entry tests plus its own slab tests; at a
    /// vertex, any of the six orderings. Cataclysm-DDA's shipped <c>map::sees</c> rule. The strict
    /// rule, every touched cell open, blocks a colonist firing down off the lip of her own terrace,
    /// which is the commonest shot on the played map: that line passes exactly through the edge
    /// shared by her cell, the air above the target, the target and the rock under her feet. Two
    /// wall corners touching diagonally still block, because both routes are shut. <b>Do not make
    /// it strict "for safety"</b> (design 47 §7).</para>
    ///
    /// <para><b>Symmetric by construction.</b> The cells a segment touches do not depend on which
    /// end it is walked from, and the corner rule examines the same cells and the same slabs either
    /// way round, so <c>Clear(a, b) == Clear(b, a)</c> without canonicalising the ends — which is
    /// also why a mirrored board gives mirrored answers. <c>LineOfSightTests</c> holds both as
    /// properties.</para>
    ///
    /// <para><b>No cache, and no allocation.</b> A 25 m shot is about fourteen reads. <see cref="Walks"/>
    /// counts the calls so the day it reads in the hundreds a tick is seen; the symmetric key makes
    /// a per-tick memo a small change then.</para>
    /// </summary>
    public static class LineOfSight
    {
        /// <summary>An axis a step crossed, in <see cref="SightLine.Steps"/>: x.</summary>
        public const byte StepX = 1;

        /// <summary>An axis a step crossed: the layer, y.</summary>
        public const byte StepY = 2;

        /// <summary>An axis a step crossed: z.</summary>
        public const byte StepZ = 4;

        /// <summary>
        /// Marks a cell the segment only touches, at an exact edge or vertex tie, beside the axes
        /// that separate it from the cell before the tie. The line does not pass through its inside.
        /// </summary>
        public const byte Corner = 0x80;

        /// <summary>
        /// How many times <see cref="Clear(CellGrid, NavGrid, int, int)"/> and
        /// <see cref="Walk(GridSize, int, int, SightLine)"/> have been called in this process. <b>A
        /// diagnostic, not state</b>: never saved, never hashed, never read by a rule, and shared
        /// by every world in the process, so read it as a difference across the stretch being
        /// measured (design 47 §5's benchmark reports walks per tick).
        /// </summary>
        public static long Walks;

        /// <inheritdoc cref="Clear(CellGrid, NavGrid, int, int)"/>
        public static bool Clear(PawnContext ctx, int from, int to) => Clear(ctx.Cells, ctx.Nav.Grid, from, to);

        /// <summary>
        /// Can a bullet cross from <paramref name="from"/> into the adjacent <paramref name="to"/> —
        /// the step between two consecutive cells of a <see cref="Walk"/>, one axis or a tie of two
        /// or three — by <see cref="Clear"/>'s own rules: the slab between layers, and at a corner
        /// either way round open? What the bullet's landing asks cell by cell, so it stops where
        /// <see cref="Clear"/> would have said the line shut. The two cells themselves are not
        /// tested. Not counted in <see cref="Walks"/>: it is part of a walk, not one of its own.
        /// </summary>
        public static bool Passes(PawnContext ctx, int from, int to) => Passes(ctx.Cells, ctx.Nav.Grid, from, to);

        /// <inheritdoc cref="Passes(PawnContext, int, int)"/>
        public static bool Passes(CellGrid cells, NavGrid? nav, int from, int to)
        {
            if (from == to) return true;
            var walk = new Stepper(cells.Size, from, to);
            int axes = walk.Next();
            return Round(cells, nav, ref walk, from, axes);
        }

        /// <summary>
        /// Where a bullet sent from <paramref name="from"/> towards <paramref name="to"/> first stops,
        /// by the walls and the ground alone: the first cell it cannot pass into (a slab or a shut
        /// corner stops it in the cell before), or the first solid cell it enters, or
        /// <paramref name="to"/>. What a miss's end is cut to (design 47 §2c), so its streak ends where
        /// it goes down rather than inside a terrace. Pawns are not asked: the landing does that.
        /// </summary>
        public static int StopCell(PawnContext ctx, int from, int to)
        {
            if (from == to) return to;
            var walk = new Stepper(ctx.Size, from, to);
            int at = from;
            for (int axes = walk.Next(); axes != 0; axes = walk.Next())
            {
                if (!Round(ctx.Cells, ctx.Nav.Grid, ref walk, at, axes)) return at;
                at = walk.Cell;
                if (Blocks(ctx.Cells, ctx.Nav.Grid, at)) return at;
            }
            return to;
        }

        /// <summary>
        /// Can a shot pass from the centre of <paramref name="from"/> to the centre of
        /// <paramref name="to"/>? The walk of <see cref="Walk(GridSize, int, int, SightLine)"/>
        /// with the blocking tests and an early out, so it allocates nothing and stops at the
        /// first blocker. A cell is always clear to itself.
        /// </summary>
        /// <param name="nav">The nav grid, read only for an open door. Null on a bare grid with no
        /// doors registered, where every blocking edifice blocks.</param>
        public static bool Clear(CellGrid cells, NavGrid? nav, int from, int to)
        {
            Walks++;
            if (from == to) return true;
            var walk = new Stepper(cells.Size, from, to);
            int at = from;
            for (int axes = walk.Next(); axes != 0; axes = walk.Next())
            {
                if (!Round(cells, nav, ref walk, at, axes)) return false;
                at = walk.Cell;
                if (at != to && Blocks(cells, nav, at)) return false;
            }
            return true;
        }

        /// <inheritdoc cref="Walk(GridSize, int, int, SightLine)"/>
        public static int Walk(PawnContext ctx, int from, int to, SightLine line) => Walk(ctx.Size, from, to, line);

        /// <summary>
        /// Every cell the segment between the centres of <paramref name="from"/> and
        /// <paramref name="to"/> passes through, in order from <paramref name="from"/>, written into
        /// <paramref name="line"/> (which is cleared first) — so a caller that keeps one
        /// <see cref="SightLine"/> walks without allocating. Both ends are included. At an exact edge
        /// or vertex tie the corner cells round it are listed, marked <see cref="Corner"/>, before
        /// the cell the tie steps into. Returns the number of cells written.
        ///
        /// <para>Walked the other way, the same cells come back in the reverse order, except that
        /// one tie's corner cells are always listed by axis (x, then y, then z; single steps before
        /// double) rather than by direction.</para>
        /// </summary>
        public static int Walk(GridSize size, int from, int to, SightLine line)
        {
            Walks++;
            line.Reset();
            line.Add(from, 0);
            if (from == to) return 1;
            var walk = new Stepper(size, from, to);
            int at = from;
            for (int axes = walk.Next(); axes != 0; axes = walk.Next())
            {
                if ((axes & (axes - 1)) != 0)
                {
                    // Every proper, non-empty subset of the tied axes is one corner cell: the three
                    // single steps first, then the pairs, so an edge gives two and a vertex six.
                    for (int bits = 1; bits <= 2; bits++)
                    for (int sub = 1; sub < axes; sub++)
                    {
                        if ((sub & ~axes) != 0 || PopCount(sub) != bits) continue;
                        line.Add(at + walk.Delta(sub), (byte)(Corner | sub));
                    }
                }
                at = walk.Cell;
                line.Add(at, (byte)axes);
            }
            return line.Count;
        }

        /// <summary>
        /// Does entering <paramref name="cell"/> stop a shot? Solid terrain does, and a blocking
        /// edifice (a wall, a pillar, a closed door) does unless the nav grid marks it an open door.
        /// Never asked of either end of the line.
        /// </summary>
        public static bool Blocks(CellGrid cells, NavGrid? nav, int cell)
        {
            if (cells.IsSolidTerrain(cell)) return true;
            if (!cells.IsBlockedByEdifice(cell)) return false;
            const NavFlags open = NavFlags.Door | NavFlags.DoorOpen;
            return nav == null || (nav.Flags[cell] & open) != open;
        }

        /// <summary>
        /// Is there a slab between two cells stacked in one column? The slab is stored on the
        /// <b>upper</b> cell's <see cref="CellGrid.Floor"/> — the roof of the lower cell is the floor
        /// of the one above — so this is one read whichever way the shot is going.
        /// </summary>
        public static bool SlabBetween(CellGrid cells, int a, int b) => cells.Floor[a > b ? a : b] != 0;

        /// <summary>
        /// Can the shot get from <paramref name="at"/> to the cell <paramref name="axes"/> away by
        /// some monotone route? One axis: the slab test if it is a layer step, and nothing else.
        /// Several (a tie): any ordering whose every step is open and whose every intermediate cell
        /// does not block. The cell arrived at is not tested here; the caller does, unless it is
        /// the end.
        /// </summary>
        static bool Round(CellGrid cells, NavGrid? nav, ref Stepper walk, int at, int axes)
        {
            for (int axis = StepX; axis <= StepZ; axis <<= 1)
            {
                if ((axes & axis) == 0) continue;
                int next = at + walk.Delta(axis);
                if (axis == StepY && SlabBetween(cells, at, next)) continue;
                int rest = axes & ~axis;
                if (rest == 0) return true;
                if (Blocks(cells, nav, next)) continue;
                if (Round(cells, nav, ref walk, next, rest)) return true;
            }
            return false;
        }

        static int PopCount(int v) => (v & 1) + ((v >> 1) & 1) + ((v >> 2) & 1);

        /// <summary>
        /// The supercover's state: how many faces have been crossed on each axis, and the cell the
        /// segment is in. One owner of the traversal, shared by <see cref="Walk(GridSize, int, int, SightLine)"/>
        /// and <see cref="Clear(CellGrid, NavGrid, int, int)"/>, so the cells listed and the cells
        /// tested cannot drift apart.
        /// </summary>
        struct Stepper
        {
            readonly int _nx, _ny, _nz;
            readonly int _dx, _dy, _dz;
            int _kx, _ky, _kz;

            /// <summary>The cell the segment is in after the last <see cref="Next"/>.</summary>
            public int Cell;

            public Stepper(GridSize size, int from, int to)
            {
                if ((uint)from >= (uint)size.CellCount) throw new ArgumentOutOfRangeException(nameof(from));
                if ((uint)to >= (uint)size.CellCount) throw new ArgumentOutOfRangeException(nameof(to));
                CellRef a = size.FromIndex(from), b = size.FromIndex(to);
                _nx = Math.Abs(b.X - a.X);
                _ny = Math.Abs(b.Y - a.Y);
                _nz = Math.Abs(b.Z - a.Z);
                _dx = Math.Sign(b.X - a.X);
                _dy = Math.Sign(b.Y - a.Y) * size.LayerStride;
                _dz = Math.Sign(b.Z - a.Z) * size.SizeX;
                _kx = _ky = _kz = 0;
                Cell = from;
            }

            /// <summary>The index offset of one step along each axis in <paramref name="axes"/>.</summary>
            public int Delta(int axes) =>
                ((axes & StepX) != 0 ? _dx : 0) + ((axes & StepY) != 0 ? _dy : 0) + ((axes & StepZ) != 0 ? _dz : 0);

            /// <summary>
            /// Step into the next cell the segment passes through and return the axes whose faces it
            /// crossed to get there — one, or two or three at an exact tie. Nought at the far end.
            /// </summary>
            public int Next()
            {
                int axes = 0;
                long num = 0, den = 1;
                Consider(_kx, _nx, StepX, ref axes, ref num, ref den);
                Consider(_ky, _ny, StepY, ref axes, ref num, ref den);
                Consider(_kz, _nz, StepZ, ref axes, ref num, ref den);
                if ((axes & StepX) != 0) _kx++;
                if ((axes & StepY) != 0) _ky++;
                if ((axes & StepZ) != 0) _kz++;
                Cell += Delta(axes);
                return axes;
            }

            /// <summary>
            /// The next face on an axis with <paramref name="k"/> of <paramref name="n"/> crossed
            /// lies at <c>(2k + 1) / 2n</c>; the common factor of two is dropped from both sides of
            /// the comparison. Earlier replaces the best; equal joins it (a tie).
            /// </summary>
            static void Consider(int k, int n, int axis, ref int axes, ref long num, ref long den)
            {
                if (k >= n) return;
                long faceNum = 2L * k + 1;
                if (axes == 0)
                {
                    axes = axis;
                    num = faceNum;
                    den = n;
                    return;
                }
                long left = faceNum * den, right = num * n;
                if (left < right)
                {
                    axes = axis;
                    num = faceNum;
                    den = n;
                }
                else if (left == right) axes |= axis;
            }
        }
    }

    /// <summary>
    /// A caller's reusable buffer for <see cref="LineOfSight.Walk(GridSize, int, int, SightLine)"/>:
    /// the cells a line passes through and how each was reached. Keep one and pass it to every walk;
    /// the lists grow to the longest line walked and are then reused, so a walk allocates nothing.
    /// Not state: nothing in it is saved or hashed.
    /// </summary>
    public class SightLine
    {
        /// <summary>The cells, in order from the shooter's.</summary>
        public readonly List<int> Cells = new List<int>(64);

        /// <summary>
        /// Beside each cell, the axes that separate it from the last cell the line passed
        /// through (<see cref="LineOfSight.StepX"/>, <see cref="LineOfSight.StepY"/>,
        /// <see cref="LineOfSight.StepZ"/>; nought for the first), with <see cref="LineOfSight.Corner"/>
        /// set on a cell the line only touches at a tie.
        /// </summary>
        public readonly List<byte> Steps = new List<byte>(64);

        public int Count => Cells.Count;

        /// <summary>Is the <paramref name="i"/>-th cell only touched, at an edge or vertex, not passed through?</summary>
        public bool IsCorner(int i) => (Steps[i] & LineOfSight.Corner) != 0;

        internal void Reset()
        {
            Cells.Clear();
            Steps.Clear();
        }

        internal void Add(int cell, byte step)
        {
            Cells.Add(cell);
            Steps.Add(step);
        }
    }
}
