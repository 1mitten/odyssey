#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Hand a colonist a marked cell to dig out.
    ///
    /// The scan walks the designation grid's own list of designated cells, never the map, so it
    /// costs what the orders cost and not what the board costs — the same shape
    /// <see cref="FellWorkGiver"/> uses, and for the same reason.
    /// </summary>
    public sealed class MineWorkGiver : WorkGiver
    {
        public override string Name => "Mine";

        public override int WorkType => WorkTypeIndex.Mining;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            var designations = ctx.Designations;
            if (designations == null) return false;

            var cells = designations.Cells;
            int best = -1;
            int bestStand = -1;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < cells.Count; i++)
            {
                int cell = cells[i];
                if (designations.At(cell) != DesignationKind.Mine) continue;
                if (!designations.CanMine(cell)) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Cell, cell);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;

                int stand = StandToMine(ctx, pawn, cell);
                if (stand < 0) continue;

                bestDistance = distance;
                best = cell;
                bestStand = stand;
            }

            if (best < 0) return false;

            job.Reset(JobIndex.Mine);
            job.TargetCell = bestStand;
            job.DestCell = best;
            return true;
        }

        /// <summary>
        /// Where a colonist stands to cut this cell out: beside it if that is possible at all,
        /// and only otherwise on top of it.
        ///
        /// <para>The preference is the whole method. Standing on top works — it is the only
        /// stance there is for cutting a shaft downward — but it costs the colonist its own floor
        /// when the cell goes, and it is answered by stepping down into the hole
        /// (<c>MineJobDriver.StepDownOntoTheFloorJustCut</c>). Sideways costs nothing at all. So a
        /// colonist cuts an adit into a terrace or an outcrop by walking up to it, and only drops
        /// into a shaft when the player has asked for a hole in the ground.</para>
        ///
        /// <para>Beside means the eight neighbours on the same layer, including the diagonals: a
        /// colonist can swing round a corner.</para>
        /// </summary>
        public static int StandToMine(PawnContext ctx, Pawn pawn, int cell)
        {
            GridSize size = ctx.Size;
            CellRef at = size.FromIndex(cell);
            int best = -1, bestDistance = int.MaxValue;

            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int x = at.X + dx, z = at.Z + dz;
                if (!size.Contains(x, z, at.Y)) continue;

                int candidate = size.Index(x, z, at.Y);
                if (!ctx.Cells.IsWalkable(candidate)) continue;

                int distance = ctx.Distance(pawn.Cell, candidate);
                if (distance >= bestDistance) continue;
                if (!ctx.Reachable(pawn, candidate)) continue;

                bestDistance = distance;
                best = candidate;
            }

            if (best >= 0) return best;

            int above = cell + size.LayerStride;
            if (above >= size.CellCount) return -1;
            if (!ctx.Cells.IsWalkable(above) || !ctx.Reachable(pawn, above)) return -1;
            return above;
        }
    }

    /// <summary>
    /// Walk to a marked cell, cut it out of the world, and leave whatever it was made of on the
    /// floor.
    ///
    /// <para>Priced per material: the work is <c>TerrainAt(terrain).workToClear</c>, read as the
    /// colonist swings rather than taken from the job def, so rock costs 700 ticks, an iron seam
    /// 900 and a coal seam 760. One number on the job def would have made every cell cost the
    /// same, which is the difference between materials.</para>
    ///
    /// <para>The order is cleared the moment the last swing lands, so no other colonist sets off
    /// for it. The world edit — the cell going, the seams around it becoming known, the stone
    /// appearing — is a structural event and runs in the deferred phase of the same tick, like
    /// every collapse and removal: a scan walking the grid must never see it move.</para>
    /// </summary>
    public class MineJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx)
        {
            if (Job.TargetCell < 0 || Job.DestCell < 0) return false;
            long key = ReservationManager.Key(ReservationTargetKind.Cell, Job.DestCell);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            var designations = ctx.Designations;
            if (designations == null) return JobStatus.Failed;

            int cell = Job.DestCell;
            // Somebody else dug it, or the player changed their mind: stop, do not swing at air.
            if (designations.At(cell) != DesignationKind.Mine || !designations.CanMine(cell))
                return JobStatus.Failed;

            if (ToilIndex == 0)
            {
                JobStatus walk = GotoCell(ctx, Job.TargetCell);
                if (walk == JobStatus.Succeeded) NextToil();
                return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            ushort terrain = ctx.Cells.Terrain[cell];
            ToilProgress++;
            if (ToilProgress < NaturalContent.TerrainAt(terrain).workToClear) return JobStatus.Ongoing;

            designations.Clear(cell);
            ctx.Defer(_ => MineCell(ctx, cell, terrain));
            return JobStatus.Succeeded;
        }

        /// <summary>
        /// Take the cell out of the world. Five things change, every one of them has to, and they
        /// are listed here in one place rather than discovered one bug at a time.
        /// </summary>
        public static void MineCell(PawnContext ctx, int cell, ushort terrain)
        {
            var grid = ctx.Cells;
            GridSize size = ctx.Size;

            grid.Terrain[cell] = NaturalContent.TerrainAir;
            grid.Flags[cell] &= ~CellFlags.SolidTerrain;

            // 1. The colony can now see what the walls of the hole are made of.
            grid.RevealAround(cell);

            // 2. The cell and everything touching it must be re-meshed. The neighbours are not an
            //    optimisation to skip: a seam revealed by this dig draws differently now, and the
            //    vertical neighbours are in different chunks entirely, a chunk being one layer.
            MarkChunksAround(ctx, cell);

            // 3. What is walkable changed here and, through the floor rule, in the cell above.
            ctx.Nav.MarkDirty(cell);
            int above = cell + size.LayerStride;
            if (above < size.CellCount) ctx.Nav.MarkDirty(above);

            // 3a. A shaft has to be climbable, or the colonist that cut it is lost down it.
            LadderTheShaft(ctx, cell);

            // 4. Anybody standing on this cell is now standing on nothing.
            StepDownOntoTheFloorJustCut(ctx, cell);

            // 5. What the cell was made of, if it left anything.
            SpawnYield(ctx, cell, terrain);
        }

        /// <summary>
        /// Join a newly opened cell to whichever of its vertical neighbours is also open.
        ///
        /// <para>Both directions, because a shaft is cut in both: downward, where the cell above
        /// is the one the colonist was standing in, and upward, where a dig breaks into a chamber
        /// or an older working from below. Asking the question as "is my vertical neighbour open"
        /// covers the two without caring which happened.</para>
        ///
        /// <para>The ladder itself is a placeholder, and <see cref="NavGraph.EnsureLadder"/> says
        /// what for.</para>
        /// </summary>
        static void LadderTheShaft(PawnContext ctx, int cell)
        {
            GridSize size = ctx.Size;
            CellGrid grid = ctx.Cells;

            int above = cell + size.LayerStride;
            if (above < size.CellCount && !grid.IsSolidTerrain(above) && !grid.IsBlockedByEdifice(above))
                ctx.Nav.EnsureLadder(cell, above);

            int below = cell - size.LayerStride;
            if (below >= 0 && !grid.IsSolidTerrain(below) && !grid.IsBlockedByEdifice(below))
                ctx.Nav.EnsureLadder(below, cell);
        }

        static void MarkChunksAround(PawnContext ctx, int cell)
        {
            if (ctx.Chunks == null) return;
            GridSize size = ctx.Size;
            CellRef at = size.FromIndex(cell);

            ctx.Chunks.MarkDirty(at);
            if (at.X > 0) ctx.Chunks.MarkDirty(new CellRef(at.X - 1, at.Z, at.Y));
            if (at.X < size.SizeX - 1) ctx.Chunks.MarkDirty(new CellRef(at.X + 1, at.Z, at.Y));
            if (at.Z > 0) ctx.Chunks.MarkDirty(new CellRef(at.X, at.Z - 1, at.Y));
            if (at.Z < size.SizeZ - 1) ctx.Chunks.MarkDirty(new CellRef(at.X, at.Z + 1, at.Y));
            if (at.Y > 0) ctx.Chunks.MarkDirty(new CellRef(at.X, at.Z, at.Y - 1));
            if (at.Y < size.SizeY - 1) ctx.Chunks.MarkDirty(new CellRef(at.X, at.Z, at.Y + 1));
        }

        /// <summary>
        /// A colonist digging the cell under its own feet steps down into the hole it just made.
        ///
        /// <para>This is not a courtesy, it is the alternative to a colonist standing on nothing.
        /// Cutting a shaft means mining downward, and mining downward means standing on the cell
        /// being cut away — there is no other stance for it. The moment the floor goes, the pawn's
        /// cell has nothing under it, and a pawn in an unstandable cell is a pawn whose next path
        /// starts from a lie.</para>
        ///
        /// <para>It is also how a shaft gets cut at all, and why
        /// <see cref="MineWorkGiver.StandToMine"/> prefers a stance beside the cell: a step down
        /// should be a decision about the shape of the dig, never a surprise.</para>
        /// </summary>
        static void StepDownOntoTheFloorJustCut(PawnContext ctx, int cell)
        {
            int above = cell + ctx.Size.LayerStride;
            if (above >= ctx.Size.CellCount) return;

            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.Cell != above) continue;
                pawn.Cell = cell;
                pawn.ClearPath();
                pawn.Destination = -1;
            }
        }

        static void SpawnYield(PawnContext ctx, int cell, ushort terrain)
        {
            if (!Yield(ctx, cell, terrain, out int item, out int count)) return;

            int at = FellJobDriver.FreeCellNear(ctx, cell);
            // Nowhere within three cells to put it is a board packed solid with things, which
            // nothing in the game can produce yet; losing the stone then is the least bad answer,
            // because spawning onto an occupied cell would corrupt the item index.
            if (at >= 0) ctx.Items.Spawn(item, at, count);
        }

        /// <summary>
        /// What a cell leaves behind.
        ///
        /// <para>A seam always gives up its metal. Plain rock gives up stone one time in
        /// <see cref="PawnContent.StoneChanceOneIn"/>, and that roll is drawn from the world seed
        /// and the <b>cell index</b> — never the tick, never a live stream. The answer belongs to
        /// the cell, so it is the same whether the cell is mined on the first day or the
        /// hundredth, it survives a save and a reload, and no amount of re-ordering the colony's
        /// work can reroll it. A live draw would make the yield depend on how many other things
        /// happened to roll dice earlier in the same tick, which is a determinism leak that
        /// surfaces as a save-and-resume divergence days later.</para>
        ///
        /// <para>Subsoil, grass, earth and sand leave nothing. They are dug through, not mined.</para>
        /// </summary>
        public static bool Yield(PawnContext ctx, int cell, ushort terrain, out int item, out int count)
        {
            if (terrain == NaturalContent.TerrainIronOre)
            {
                item = ItemIndex.IronOre;
                count = ctx.Content.OrePerCell;
                return true;
            }

            if (terrain == NaturalContent.TerrainCoalSeam)
            {
                item = ItemIndex.Coal;
                count = ctx.Content.OrePerCell;
                return true;
            }

            if (terrain == NaturalContent.TerrainRock)
            {
                var rng = DeterministicRandom.ForTick(ctx.Seed, cell, PawnPurpose.StoneYield);
                if (rng.NextInt(ctx.Content.StoneChanceOneIn) == 0)
                {
                    item = ItemIndex.Stone;
                    count = ctx.Content.StonePerRock;
                    return true;
                }
            }

            item = -1;
            count = 0;
            return false;
        }
    }
}
