#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// What happens to whatever is in a cell when the thing it was standing on goes away.
    ///
    /// <para><b>Extracted rather than written.</b> Every line here was <see cref="MineJobDriver"/>'s,
    /// private to it, and correct: a dig drops whoever was standing on the cell it cuts and whatever
    /// was lying there, and both had to learn the same two lessons the hard way. A collapsing floor
    /// asks the identical question, and the second caller is exactly where a copy would have been
    /// made — so it moved here and mining calls it. Nothing about mining changed, which is a test
    /// (`MineJobTests` passes unedited) rather than a hope.</para>
    ///
    /// <para><b>The first real floor, not one layer.</b> Dropping a colonist exactly one layer is
    /// right only when the cell below is solid, and a shaft is by definition the case where it is
    /// not — so a colonist over a two-deep hole was moved into the middle of it and left there.
    /// <see cref="World.CellGrid.FirstFloorAtOrBelow"/> is the one answer, for people and for
    /// things alike.</para>
    ///
    /// <para><b>A climb is not a floor.</b> A cell with a climb footprint counts as standable to
    /// navigation — that is what lets a colonist be on a rock face at all — but there is nothing
    /// under it, so it is not somewhere to leave anybody. Measured without this rule: two of five
    /// colonists spent the last 12,600 ticks of a 40,000-tick run standing still in mid-air.</para>
    /// </summary>
    public static class Falling
    {
        /// <summary>No memory: the thing fell and thought nothing of it.</summary>
        public const int NoThought = -1;

        /// <summary>
        /// Everything in this cell has lost what it was standing on. Pawns first, then loose
        /// things, both to the first real floor at or below.
        ///
        /// <para>Whether a colonist <em>remembers</em> the fall is the caller's to say, and the two
        /// callers disagree on purpose. A collapse is a frightening event and passes a thought; a
        /// dig asking a colonist to step down one block into the hole it was told to make is a
        /// Tuesday, and passes none.</para>
        /// </summary>
        /// <returns>How many pawns actually moved.</returns>
        public static int OutOf(PawnContext ctx, int cell, int thought = NoThought, int tick = 0)
        {
            if ((uint)cell >= (uint)ctx.Size.CellCount) return 0;

            int moved = PawnsOutOf(ctx, cell, thought, tick);
            ItemsOutOf(ctx, cell);
            return moved;
        }

        /// <summary>
        /// Anybody standing in this cell falls to the first real floor below it, and remembers it
        /// when the caller asked for that.
        ///
        /// <para>The memory goes only to a pawn that actually <b>moved</b>. A colonist beside the
        /// hole, or one standing on a cell the collapse merely opened a view of, has nothing to
        /// remember — and a thought handed out for standing still is how a mood becomes noise.</para>
        /// </summary>
        public static int PawnsOutOf(PawnContext ctx, int cell, int thought = NoThought, int tick = 0)
        {
            int landing = ctx.Cells.FirstFloorAtOrBelow(cell);
            if (landing == cell) return 0;

            int moved = 0;
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.Cell != cell) continue;

                pawn.Cell = landing;
                pawn.ClearPath();
                pawn.Destination = -1;
                if (thought != NoThought) pawn.AddMemory(thought, tick);
                moved++;
            }

            return moved;
        }

        /// <summary>
        /// Anything lying in this cell falls to the first real floor below.
        ///
        /// <para>It <em>merges</em> where it lands, up to the ordinary stack limit, because
        /// <see cref="ColonyItems.MoveTo"/> merges — which is the owner's decision and the point of
        /// the exercise: two loads that fall into the same hole become one load, and one hauler
        /// trip carries what took two. A load that will not fit goes to the nearest cell that can
        /// take it rather than being lost.</para>
        /// </summary>
        public static void ItemsOutOf(PawnContext ctx, int cell)
        {
            ColonyItem? resting = ctx.Items.ItemAt(cell);
            if (resting == null || resting.Despawned) return;
            if (ctx.Cells.HasFloor(cell)) return;

            int landing = ctx.Cells.FirstFloorAtOrBelow(cell);
            if (landing == cell) return;

            int room = ctx.Items.NearestCellWithSpace(
                ctx.Cells, landing, resting.DefIndex, resting.Stack, maxRadius: 3);
            if (room >= 0) ctx.Items.MoveTo(resting, room);
        }
    }
}
