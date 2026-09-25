#nullable enable
using System.Collections.Generic;
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
    ///
    /// <para><b>Not everything in a cell falls.</b> A crop and a tree are rooted in the ground
    /// rather than resting on it, so when the ground goes they go with it — added 2026-09-21 after
    /// a quarry dug out from under a sown field left the seeds hanging in the air. Both are the
    /// same shape as the item rule and neither is a drop, which is why they live beside it here
    /// rather than growing a falling rule of their own: the question "what was this standing on"
    /// has one place to be asked, and the next kind that can be orphaned should be a few lines in
    /// <see cref="OutOf"/> rather than a fourth site that remembers half of it.</para>
    /// </summary>
    public static class Falling
    {
        /// <summary>No memory: the thing fell and thought nothing of it.</summary>
        public const int NoThought = -1;

        /// <summary>
        /// Everything in this cell has lost what it was standing on. Pawns first, then loose
        /// things, both to the first real floor at or below — and then the two kinds that do not
        /// fall at all, because they were rooted in the ground that went.
        ///
        /// <para><b>Four kinds, two answers.</b> A colonist and a sack of carrots are <em>on</em>
        /// the floor and land on the next one down. A crop and a tree are <em>in</em> the soil, and
        /// when the soil is carried away there is nothing left to land: they go with it. That is
        /// the owner's call of 2026-09-21, from a screenshot of sown seeds hanging over a quarry
        /// the colony had dug out from under them (docs/design/22-growing.md §10).</para>
        ///
        /// <para>Whether a colonist <em>remembers</em> the fall is the caller's to say, and the two
        /// callers disagree on purpose. A collapse is a frightening event and passes a thought; a
        /// dig asking a colonist to step down one block into the hole it was told to make is a
        /// Tuesday, and passes none.</para>
        /// </summary>
        /// <returns>How many pawns actually moved.</returns>
        /// <param name="collapse">A floor gave way (design 43 §7): even a one-layer drop is a fall
        /// and hurts. Without it a one-layer drop is a step — a miner into the hole she dug, a
        /// deconstructor off the slab she took up — and only two layers or more hurt.</param>
        public static int OutOf(PawnContext ctx, int cell, int thought = NoThought, int tick = 0, bool collapse = false)
        {
            if ((uint)cell >= (uint)ctx.Size.CellCount) return 0;

            int moved = PawnsOutOf(ctx, cell, thought, tick, collapse);
            ItemsOutOf(ctx, cell);
            PlantsOutOf(ctx, cell);
            TreesOutOf(ctx, cell);
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
        public static int PawnsOutOf(PawnContext ctx, int cell, int thought = NoThought, int tick = 0, bool collapse = false)
        {
            int landing = ctx.Cells.FirstFloorAtOrBelow(cell);
            if (landing == cell) return 0;

            // How far, in layers: the landing's height is the fall (design 43 §7), and each landing
            // of a cascade is its own fall, because a later collapse under her calls this again.
            int layers = ctx.Size.FromIndex(cell).Y - ctx.Size.FromIndex(landing).Y;
            bool hurts = layers >= 2 || (collapse && layers >= 1);

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
                // The injury, through the one owner of damage, so a fall that kills is mourned. The
                // memory stays as well: a fall is frightening as well as painful.
                if (hurts) ctx.Combat?.Fall(pawn, layers, ctx.CurrentTick);
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
            if (landing == cell)
            {
                ctx.Items.Despawn(resting);
                return;
            }

            int room = ctx.Items.NearestCellWithSpace(
                ctx.Cells, landing, resting.DefIndex, resting.Stack, maxRadius: 8);
            if (room >= 0) ctx.Items.MoveTo(resting, room);
            else ctx.Items.Despawn(resting);
        }

        /// <summary>
        /// Safety sweep: ensure no loose items on the entire board are suspended in mid-air.
        ///
        /// <para>Walks active items; any item resting on a cell without a floor is dropped to the
        /// nearest real floor below. Returns the number of items that fell.</para>
        /// </summary>
        public static int DropFloatingItems(PawnContext ctx)
        {
            var items = ctx.Items.Items;
            int dropped = 0;
            for (int i = 0; i < items.Count; i++)
            {
                ColonyItem item = items[i];
                if (item.Despawned || item.Cell < 0) continue;
                if (!ctx.Cells.HasFloor(item.Cell))
                {
                    ItemsOutOf(ctx, item.Cell);
                    dropped++;
                }
            }
            return dropped;
        }

        /// <summary>
        /// The crop and the zone paint in this cell go with the ground that held them.
        ///
        /// <para><b>Nothing is dropped, and nothing is salvaged.</b> A seed put in soil that is
        /// then quarried away is gone, at any stage of its growth — the owner's answer of
        /// 2026-09-21 to the alternative of yielding a ripe crop, which would have made mining
        /// under a field a harvesting technique.</para>
        ///
        /// <para><b>The paint goes too.</b> A zone square left hanging over the hole would draw
        /// tilled rows on nothing and re-sow itself the moment a colonist could reach it, and the
        /// board would be telling the player a field is there when it is not. Repainting a
        /// rebuilt floor is one drag, and it is the player saying they meant it.</para>
        ///
        /// <para><b>The floor is the question, not the site.</b> A zone is deliberately <em>not</em>
        /// cancelled by every change that would have refused it at painting time — a roof raised
        /// over a field does not unzone it, and the sowing giver asks again for itself
        /// (<see cref="Growing.GrowingZones.SiteAllows"/>). The one condition that ends a zone cell
        /// from the outside is the one that leaves it drawn in mid-air, which is exactly the
        /// condition <see cref="DropFloatingItems"/> asks about items.</para>
        /// </summary>
        /// <returns>Whether a zone cell or a crop was taken.</returns>
        public static bool PlantsOutOf(PawnContext ctx, int cell)
        {
            var zones = ctx.Growing;
            if (zones == null) return false;
            if ((uint)cell >= (uint)ctx.Size.CellCount) return false;
            if (ctx.Cells.HasFloor(cell)) return false;

            return zones.CancelAt(cell);
        }

        /// <summary>
        /// A tree standing in this cell goes with the ground it was rooted in, leaving no wood.
        ///
        /// <para><b>Mining cannot reach this, and that is deliberate.</b>
        /// <c>DesignationGrid.CanMine</c> refuses the cell under a standing tree outright — "the
        /// right answer is to fell it first rather than to invent a falling rule here". This is
        /// the rule for the two callers that never asked: a collapsing slab and a deconstructed
        /// floor, neither of which consults the designation grid about what is standing on the
        /// thing it is taking away. It costs nothing when there is no tree, and it means the next
        /// caller of <see cref="OutOf"/> inherits the answer instead of rediscovering it.</para>
        ///
        /// <para>No wood, for the same reason a crop leaves no carrot: what is lost is lost with
        /// the ground. Felling is still the way to get the timber, and now the only way.</para>
        /// </summary>
        /// <returns>Whether a tree was taken.</returns>
        public static bool TreesOutOf(PawnContext ctx, int cell)
        {
            var designations = ctx.Designations;
            if (designations == null) return false;
            if ((uint)cell >= (uint)ctx.Size.CellCount) return false;
            if (ctx.Cells.HasFloor(cell)) return false;
            if (!designations.IsTree(cell)) return false;

            // A felled tree's teardown minus the yield: the handle goes and the chunk re-meshes.
            // Navigation is not marked, because a tree blocks nothing and never did.
            ctx.Cells.RemoveEdifice(cell);
            designations.Clear(cell);
            ctx.Chunks?.MarkDirty(ctx.Size.FromIndex(cell));
            return true;
        }

        /// <summary>
        /// Safety sweep for the rooted kinds, the sibling of <see cref="DropFloatingItems"/>.
        ///
        /// <para>Walks the <em>zoned</em> cells rather than the board — a sparse list that is empty
        /// in most colonies and a few thousand long in a big farm, against 250 × 250 × 40 cells if
        /// this asked the grid. Planted cells are a subset of zoned cells, because sowing needs a
        /// zone, so one walk covers both. The cells are collected before any is cancelled: a
        /// cancel writes to the very list being read.</para>
        /// </summary>
        /// <returns>How many zone cells were taken.</returns>
        public static int UprootFloatingPlants(PawnContext ctx)
        {
            var zones = ctx.Growing;
            if (zones == null) return 0;

            var cells = zones.Cells;
            List<int>? doomed = null;
            for (int i = 0; i < cells.Count; i++)
            {
                int cell = cells[i];
                if (ctx.Cells.HasFloor(cell)) continue;
                (doomed ??= new List<int>()).Add(cell);
            }

            if (doomed == null) return 0;
            for (int i = 0; i < doomed.Count; i++) zones.CancelAt(doomed[i]);
            return doomed.Count;
        }
    }
}
