#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// A cut has broken into a chamber nobody had seen (design 62 §6): reveal it whole, once.
    ///
    /// <para><b>Every path that opens a solid cell calls <see cref="Open"/></b>, after the cell is
    /// open. Today that is one path, <see cref="MineJobDriver.MineCell"/>: nothing else in the
    /// simulation turns solid terrain into anything else — a collapse brings down slabs and
    /// buildings and fills open air with rubble, a deconstruct takes away what the colony built,
    /// and neither can stand beside an unseen cell, because a cell beside one that was open would
    /// already have revealed it. A later path that takes rock out of the world (a cave-in that
    /// drops rock, a blast) owes this one line, and <c>UnseenCells.RevealBreached</c> on the next
    /// load would catch it if it forgot — but only then, so it must not forget.</para>
    ///
    /// <para><b>Nothing the simulation does changes.</b> An unseen cell was air all along; the
    /// graph, the rooms and the paths already knew it. What changes is what the colony may be
    /// told: the chamber's cells stop being drawn and described as rock, the rock round them is
    /// <c>Discovered</c> so the ore on its walls shows, and the chunks that draw either re-mesh.
    /// An order a player gave on a cell that has turned out to be air is taken off — there is
    /// nothing there to mine — along with any cut kept for it.</para>
    /// </summary>
    public static class CavernBreach
    {
        /// <summary>
        /// A cell has just been opened. Reveal every unseen chamber touching it and return how
        /// many cells came into view; nought, and six bit tests, for a cell beside none.
        /// </summary>
        public static int Open(PawnContext ctx, int opened)
        {
            var grid = ctx.Cells;
            if (grid.Unseen.Count == 0) return 0;

            var revealed = new List<int>();
            int count = grid.Unseen.RevealFrom(grid, opened, revealed);
            if (count > 0) Settle(ctx, revealed);
            return count;
        }

        /// <summary>
        /// Reveal any chamber that the grid as it stands has already broken into — the load
        /// path's repair for a save written before the unseen bitset existed, whose board keeps
        /// the chambers its seed generated, opened or not. Nothing on a save this build wrote.
        /// </summary>
        public static int RevealBreached(PawnContext ctx)
        {
            var grid = ctx.Cells;
            if (grid.Unseen.Count == 0) return 0;

            var revealed = new List<int>();
            int count = grid.Unseen.RevealBreached(grid, revealed);
            if (count > 0) Settle(ctx, revealed);
            return count;
        }

        static void Settle(PawnContext ctx, List<int> revealed)
        {
            var grid = ctx.Cells;
            var designations = ctx.Designations;
            GridSize size = ctx.Size;

            for (int i = 0; i < revealed.Count; i++)
            {
                int cell = revealed[i];

                if (designations != null)
                {
                    // An order on "rock" that was a void: nothing to cut, so nothing is ordered.
                    designations.Clear(cell);
                    designations.PartMined.TryTake(cell, grid.Terrain[cell], out _);
                }

                if (ctx.Chunks == null) continue;

                // The cell, which is drawn as air now, and its six neighbours, whose Discovered
                // flag may have changed and which sit in other chunks at a chunk's edge and on
                // the layers above and below — the same seven a dig marks.
                CellRef at = size.FromIndex(cell);
                ctx.Chunks.MarkDirty(at);
                if (at.X > 0) ctx.Chunks.MarkDirty(at.X - 1, at.Z, at.Y);
                if (at.X < size.SizeX - 1) ctx.Chunks.MarkDirty(at.X + 1, at.Z, at.Y);
                if (at.Z > 0) ctx.Chunks.MarkDirty(at.X, at.Z - 1, at.Y);
                if (at.Z < size.SizeZ - 1) ctx.Chunks.MarkDirty(at.X, at.Z + 1, at.Y);
                if (at.Y > 0) ctx.Chunks.MarkDirty(at.X, at.Z, at.Y - 1);
                if (at.Y < size.SizeY - 1) ctx.Chunks.MarkDirty(at.X, at.Z, at.Y + 1);
            }
        }
    }
}
