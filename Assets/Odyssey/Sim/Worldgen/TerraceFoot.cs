#nullable enable

using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Worldgen
{
    /// <summary>
    /// Is this cell the foot of a terrace step — the empty cell a bank ramp fills?
    ///
    /// <para><b>Why the simulation has to know.</b> A bank is presentation's own invention
    /// (<c>BankLayout</c>): the sim has no idea one exists, and that was harmless while the only
    /// question was what to draw. It stopped being harmless the moment something was <i>put</i> in
    /// such a cell. A bank fills its cell from the floor to the rim of the step above, so anything
    /// standing in it is inside a solid-looking wedge of hillside — the owner's report, 2026-09-18:
    /// trees generated at the foot of a terrace are sheared off by the façade, and a colonist who
    /// lies down there disappears into it.</para>
    ///
    /// <para><b>What it is not.</b> Not a rule about walking. The cell is walkable and stays
    /// walkable: it is the take-off cell for the hop up the terrace, and the whole reason a bank is
    /// drawn there is to make that hop legible. Nothing here belongs in <c>NavGraph</c>.</para>
    ///
    /// <para><b>Two owners, held together by a test.</b> This is the same rule as
    /// <c>BankLayout.At(...).Exists</c>, stated over the cell grid instead of over the render
    /// mirror, because the two read different data: worldgen has a <see cref="CellGrid"/> and no
    /// render model, presentation has a render model and no grid. That is a duplicated rule, which
    /// this project has been bitten by before, so it is duplicated <i>deliberately and checked</i>:
    /// <c>TerraceFootAgreesWithBankLayoutTests</c> walks every cell of several worlds — a plain
    /// step, an inner corner, an outer corner, a quarry — and fails if the two answers ever differ.
    /// Change one and that test tells you to change the other.</para>
    /// </summary>
    public static class TerraceFoot
    {
        // The four orthogonal neighbours and the four diagonals, in one table. A bank stands
        // against an orthogonal step (straight and inner-corner pieces) or, where there is none,
        // against a diagonal one (the outer-corner piece that wraps a convex corner) — so for the
        // question "is a bank here at all", the eight are one list.
        static readonly int[] NeighbourX = { 0, 1, 0, -1, 1, 1, -1, -1 };
        static readonly int[] NeighbourZ = { 1, 0, -1, 0, 1, -1, -1, 1 };

        /// <summary>The same, by cell index.</summary>
        public static bool IsFoot(CellGrid grid, int index)
        {
            CellRef cell = grid.FromIndex(index);
            return IsFoot(grid, cell.X, cell.Z, cell.Y);
        }

        /// <summary>
        /// Does a terrace step rise out of this cell: is it empty, standing on something, under
        /// open sky, uncut, and next to ground exactly one layer higher?
        /// </summary>
        public static bool IsFoot(CellGrid grid, int x, int z, int y)
        {
            GridSize size = grid.Size;
            if (y == 0 || !size.Contains(x, z, y) || y + 1 >= size.SizeY) return false;

            int index = size.Index(x, z, y);
            if (grid.Terrain[index] != CoreContent.TerrainAir) return false;

            // Something underfoot: the top of the lower terrace. Terrain rather than solidity, so
            // that the cell a stream runs in counts — a channel is cut one layer down, which makes
            // every stream bank one of these steps.
            int floor = index - size.LayerStride;
            if (grid.Terrain[floor] == CoreContent.TerrainAir) return false;

            // Nothing spills into a hole the colony cut. A mined cell reveals the floor it leaves
            // behind, so a discovered floor is the mark of an excavation on this very cell.
            if (grid.IsDiscovered(floor)) return false;

            if (!OpenToTheSky(grid, index, y)) return false;

            for (int i = 0; i < NeighbourX.Length; i++)
                if (IsStep(grid, x + NeighbourX[i], z + NeighbourZ[i], y)) return true;

            return false;
        }

        /// <summary>
        /// Is the cell beside this one, at the same layer, a step a bank could climb: natural soil,
        /// uncut, with nothing on top of it?
        /// </summary>
        static bool IsStep(CellGrid grid, int x, int z, int y)
        {
            GridSize size = grid.Size;
            if (!size.Contains(x, z, y)) return false;

            int step = size.Index(x, z, y);
            if (!grid.IsSolidTerrain(step)) return false;

            // Soil, not stone: a rock outcrop or a cut rock face is sheer, and no ramp of earth
            // spills down it.
            if (!NaturalContent.IsEarth(grid.Terrain[step])) return false;
            if (grid.IsDiscovered(step)) return false;

            // Its top has to be open, or this is the wall of a tunnel rather than a terrace.
            return !grid.IsSolidTerrain(step + size.LayerStride);
        }

        /// <summary>
        /// Nothing at all over this cell — no slab and no solid cell, all the way up. The mirror of
        /// <c>WorldRenderModel.OpenToTheSky</c>, and a slab is stored on the cell above the
        /// boundary it occupies, so the roof over this cell is the next cell's own floor.
        /// </summary>
        static bool OpenToTheSky(CellGrid grid, int index, int y)
        {
            GridSize size = grid.Size;
            int above = index + size.LayerStride;

            for (int layer = y + 1; layer < size.SizeY; layer++, above += size.LayerStride)
            {
                if (grid.Floor[above] != 0) return false;
                if (grid.IsSolidTerrain(above)) return false;
            }

            return true;
        }
    }
}
