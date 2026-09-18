#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Growing;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Find a zoned cell still waiting for its seed.
    ///
    /// <para>The scan is the zone grid's own cell list, which is already the shape the sowing
    /// order left behind — the player drew the fields, this only asks which cells of them are
    /// fallow. A cell that stopped making sense since it was painted (a roof raised over it, a
    /// slab laid on it) is refused here exactly as it was refused at designation, by asking
    /// <see cref="GrowingZones.SiteAllows"/> again: the gate has one home.</para>
    /// </summary>
    public sealed class SowWorkGiver : WorkGiver
    {
        public override string Name => "Sow";

        public override int WorkType => WorkTypeIndex.Growing;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            var zones = ctx.Growing;
            if (zones == null) return false;

            var cells = zones.Cells;
            int best = -1;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < cells.Count; i++)
            {
                int cell = cells[i];
                if (zones.IsPlanted(cell)) continue;

                PlantDef plant = zones.Plant(zones.ZonePlantAt(cell));
                if (!zones.SiteAllows(cell, plant)) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Cell, cell);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = cell;
            }

            if (best < 0) return false;

            job.Reset(JobIndex.Sow);
            job.TargetCell = best;
            return true;
        }
    }

    /// <summary>Find a crop that has finished growing and is ready to cut.</summary>
    public sealed class HarvestWorkGiver : WorkGiver
    {
        public override string Name => "Harvest";

        public override int WorkType => WorkTypeIndex.Growing;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            var zones = ctx.Growing;
            if (zones == null) return false;

            var planted = zones.Planted;
            int best = -1;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < planted.Count; i++)
            {
                int cell = planted[i];
                if (!zones.IsRipe(cell)) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Cell, cell);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = cell;
            }

            if (best < 0) return false;

            job.Reset(JobIndex.Harvest);
            job.TargetCell = best;
            return true;
        }
    }

    /// <summary>
    /// Walk into a fallow zone cell, break its ground, and leave a seed in it.
    ///
    /// <para>The colonist works standing <em>in</em> the plot rather than beside it — a crop
    /// cell is open air above walkable soil, and there is nothing in it to keep a body away
    /// from. That makes the walk, the stance and the focus all one cell, which is the whole
    /// difference from felling: a tree has a trunk.</para>
    ///
    /// <para>Priced per plant: the work is the plant's own <c>sowWorkTicks</c>, read as the
    /// colonist swings rather than taken from the job def, on the same argument mining makes —
    /// one number here would make every crop cost the same to plant. The seed itself goes in as
    /// a deferred world edit on the tick the last swing lands, so a scan running the same tick
    /// never sees half a sowing.</para>
    /// </summary>
    public class SowJobDriver : JobDriver
    {
        /// <summary>
        /// The plot, once the walk is over and the swings have started. The target and the
        /// destination are the same cell here — the one the colonist stands in — so either name
        /// answers; the figure has to face the ground it is breaking.
        /// </summary>
        /// <para>Nothing during the settle toil: the seed is already in and the figure should
        /// be easing out of its stance, not still bending.</para>
        public override int WorkFocus =>
            ToilIndex != 1 ? -1 : Job.TargetCell >= 0 ? Job.TargetCell : Job.DestCell;

        public override bool TryMakeReservations(PawnContext ctx)
        {
            if (Job.TargetCell < 0) return false;
            long key = ReservationManager.Key(ReservationTargetKind.Cell, Job.TargetCell);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            // Before every guard below: by now the seed is in and the cell is planted, so
            // asking whether it is still fallow would fail the job on the first settle tick.
            if (ToilIndex == SettleToil) return Settle(ctx);

            var zones = ctx.Growing;
            if (zones == null) return JobStatus.Failed;

            int cell = Job.TargetCell;
            // The zone is gone, or the cell holds a crop it cannot hold twice: stop, do not
            // plant into a bed somebody else filled or a field the player erased.
            if (zones.ZonePlantAt(cell) < 0 || zones.IsPlanted(cell))
                return JobStatus.Failed;

            if (ToilIndex == 0)
            {
                JobStatus walk = GotoCell(ctx, cell);
                if (walk == JobStatus.Succeeded) NextToil();
                return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            // Still in the plot, or a step from it. The same displacement question felling
            // asks: a colonist moved off its stance — dropped by a dig under its feet — is no
            // longer sowing this cell, whatever its job says.
            if (!StillInReach(ctx, Pawn, cell, layersAbove: 0, layersBelow: 0))
            {
                WalkBack();
                return JobStatus.Ongoing;
            }

            PlantDef plant = zones.Plant(zones.ZonePlantAt(cell));
            ToilProgress++;
            Work(ctx);
            if (ToilProgress < plant.sowWorkTicks) return JobStatus.Ongoing;

            ctx.Defer(_ => Sowed(ctx, cell));

            // The seed is in; the colonist straightens up before walking off.
            NextToil();
            return JobStatus.Ongoing;
        }

        static void Sowed(PawnContext ctx, int cell)
        {
            var zones = ctx.Growing;
            if (zones == null) return;
            // The zone can have been dissolved in the same tick the swing landed — cancelling a
            // one-cell field while the sower was mid-stroke. Sow refuses a cell in no zone, and
            // refusing quietly here is the honest end of a job whose ground vanished under it.
            // Sow itself tells the renderer: who changed the world owns the remesh mark, and the
            // cancel route has no context to mark from.
            if (zones.ZonePlantAt(cell) < 0 || zones.IsPlanted(cell)) return;
            zones.Sow(cell);
        }
    }

    /// <summary>
    /// Walk into a ripe crop cell, cut it, and leave what it yielded on the ground.
    ///
    /// <para>The same one-cell shape as sowing — walk in, swing, focus the plot — and the same
    /// per-plant pricing, from <c>harvestWorkTicks</c>. The crop comes up and the yield goes
    /// down in one deferred edit: a scan running that tick sees fallow soil and a pile of
    /// carrots or neither, never one without the other.</para>
    ///
    /// <para>The cell left fallow is the whole of re-sowing: the sowing scan finds it again on
    /// its next pass, and the zone the player painted sows itself forever after. Nobody queues
    /// a second job and nothing remembers the crop was there.</para>
    /// </summary>
    public class HarvestJobDriver : JobDriver
    {
        /// <summary>The row being cut, once the walk is over. As sowing: target and destination
        /// are the one cell, and the figure has to face the plant it is cutting.</summary>
        /// <para>Nothing during the settle toil — the crop is already in the pile.</para>
        public override int WorkFocus =>
            ToilIndex != 1 ? -1 : Job.TargetCell >= 0 ? Job.TargetCell : Job.DestCell;

        public override bool TryMakeReservations(PawnContext ctx)
        {
            if (Job.TargetCell < 0) return false;
            long key = ReservationManager.Key(ReservationTargetKind.Cell, Job.TargetCell);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            // Before every guard below: by now the crop is up and the cell is fallow, so asking
            // whether it is still planted would fail the job on the first settle tick.
            if (ToilIndex == SettleToil) return Settle(ctx);

            var zones = ctx.Growing;
            if (zones == null) return JobStatus.Failed;

            int cell = Job.TargetCell;
            // Somebody else cut it, or the player cancelled the field under the cutter: stop.
            if (!zones.IsPlanted(cell)) return JobStatus.Failed;

            if (ToilIndex == 0)
            {
                JobStatus walk = GotoCell(ctx, cell);
                if (walk == JobStatus.Succeeded) NextToil();
                return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            if (!StillInReach(ctx, Pawn, cell, layersAbove: 0, layersBelow: 0))
            {
                WalkBack();
                return JobStatus.Ongoing;
            }

            PlantDef plant = zones.Plant(zones.CropPlant(cell));
            ToilProgress++;
            Work(ctx);
            if (ToilProgress < plant.harvestWorkTicks) return JobStatus.Ongoing;

            ctx.Defer(_ => Harvested(ctx, cell, plant));

            // The crop is down; the colonist straightens up before walking off.
            NextToil();
            return JobStatus.Ongoing;
        }

        static void Harvested(PawnContext ctx, int cell, PlantDef plant)
        {
            var zones = ctx.Growing;
            if (zones == null) return;
            // Uproot tells the renderer itself — the same mark a zone cancel rides.
            zones.Uproot(cell);

            // Where the crop stood, or the nearest cell nearby that can take the yield — the
            // felling argument again: a row cut at once should gather into a few stacks, and
            // nowhere within three cells that can take them is a board packed too solid for
            // anything in the game to have produced.
            int at = ctx.Items.NearestCellWithSpace(
                ctx.Cells, cell, ItemIndex.Carrots, plant.yieldCount, maxRadius: 3);
            if (at >= 0) ctx.Items.Spawn(ItemIndex.Carrots, at, plant.yieldCount);
        }
    }
}
