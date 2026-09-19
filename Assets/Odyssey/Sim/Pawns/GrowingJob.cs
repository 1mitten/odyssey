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

                // Nothing stands where a yield still lies (owner, 2026-09-19: "you cannot
                // sow unless the tile has been harvested"). The pile normally lands off the
                // soil now, but a packed board falls back to the plot itself and a failed
                // haul can put one down anywhere - either way, a sower kneeling in a heap
                // of carrots is a tile that has not been cleared, whatever its crop says.
                if (ctx.Items.ItemAt(cell) != null) continue;

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
        /// Never a work focus: sowing is a kneel, not a stance (owner, 2026-09-18 — the sow must
        /// not chop). A work focus would summon the computed swing and its tool, and a sower
        /// carries neither; the pose is the <see cref="PawnGesture.Sow"/> the toil begins below,
        /// the pickup's kneel re-timed so the hold is the work. The figure still knows where the
        /// plot is from the job's own target, and the settle toil needs nothing here.
        /// </summary>
        public override int WorkFocus => -1;

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
                // The kneel is over the moment the stance is: a sticky gesture carried across
                // the walk back would draw seeds under a sower who is only returning.
                Pawn.BeginGesture(PawnGesture.None);
                return JobStatus.Ongoing;
            }

            // The kneel begins with the work and holds while it runs — the gesture idiom the
            // lift toil established: begun once at the toil's start, ended by its own clock, so
            // the rise lands as the settle begins. WalkBack zeroes the progress, so a sower
            // displaced mid-hold kneels again on return rather than finishing a kneel it left.
            //
            // And the END is the driver's to author, not the clock's: the gesture is sticky by
            // contract (a flag set for one tick would be missed between frames), so a kneel
            // nobody cleared leaked through the whole walk to the next plot — and the seed
            // specks, gated on the gesture, flashed under the sower's feet on every fallow tile
            // she crossed (owner, 2026-09-19: seeds "appear immediately … then disappear — then
            // it appears again"). Cleared here, at the boundary where the work is over, the
            // specks cut on the same tick the plant record lands and takes over the cell.
            if (ToilProgress == 0) Pawn.BeginGesture(PawnGesture.Sow);

            PlantDef plant = zones.Plant(zones.ZonePlantAt(cell));
            // Progress is milliwork and the plant's price is ticks (design 17 §3b's convention):
            // pay at the pawn's own rate — flat today, since no skill drives the curve yet
            // (design 22 §5) — and read the price against the standard one.
            ToilProgress += Pawn.WorkRatePerMille(WorkTypeIndex.Growing);
            Work(ctx);
            if (ToilProgress < plant.sowWorkTicks * Rates.Scale) return JobStatus.Ongoing;

            Pawn.BeginGesture(PawnGesture.None);
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
    /// Walk into a ripe crop cell, pull it up, and leave what it yielded on the ground.
    ///
    /// <para>The same one-cell shape as sowing — walk in, kneel, work the plot — and the same
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
        /// <summary>
        /// Never a work focus: the harvest is a pull from the soil at kneeling reach, not a
        /// swing at a trunk (owner, 2026-09-19 — "the colonists still use their axe to harvest
        /// … use the same pose as sowing"). A focus would summon the computed swing and the axe
        /// it carries, and nothing about pulling a carrot asks for either; the pose is the
        /// <see cref="PawnGesture.Sow"/> kneel the toil begins below, re-timed for the harvest
        /// it now also serves. Nothing during the settle toil — the crop is already in the pile.
        /// </summary>
        public override int WorkFocus => -1;

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
                Pawn.BeginGesture(PawnGesture.None);
                return JobStatus.Ongoing;
            }

            // The pull kneels like the sow (owner, 2026-09-19): begun with the work and held
            // while it runs, so the harvest reads as the same bending reach at the same soil.
            // WalkBack zeroes the progress, so a puller displaced mid-hold kneels again on
            // return rather than finishing a kneel it left — and the END is authored with the
            // same line as the sower's, for the same reason: the gesture is sticky, and nobody
            // else will clear it.
            if (ToilProgress == 0) Pawn.BeginGesture(PawnGesture.Sow);

            PlantDef plant = zones.Plant(zones.CropPlant(cell));
            ToilProgress += Pawn.WorkRatePerMille(WorkTypeIndex.Growing);
            Work(ctx);
            if (ToilProgress < plant.harvestWorkTicks * Rates.Scale) return JobStatus.Ongoing;

            Pawn.BeginGesture(PawnGesture.None);
            ctx.Defer(_ => Harvested(ctx, cell, plant));

            // The crop is down; the colonist straightens up before walking off.
            NextToil();
            return JobStatus.Ongoing;
        }

        static void Harvested(PawnContext ctx, int cell, PlantDef plant)
        {
            var zones = ctx.Growing;
            if (zones == null) return;
            // The same re-ask Sowed makes after its own defer: the world moved between the
            // swing that earned this edit and the tick boundary that lands it, and a crop
            // that is no longer standing — cancelled out of its zone — yields nothing.
            if (!zones.IsPlanted(cell)) return;
            // Uproot tells the renderer itself — the same mark a zone cancel rides.
            zones.Uproot(cell);

            // Off the soil first (owner, 2026-09-19: "harvested materials should not be laid on
            // the soil and should look to be moved off it"): a yield dropped where it grew
            // reads as a plot nobody cleared, and the sower who follows kneels in it. The
            // nearest ground that is outside every zone wins; where nothing within three cells
            // qualifies, the felling argument takes over - nowhere at all that can take the
            // yield is a board packed too solid for anything in the game to have produced.
            int at = ctx.Items.NearestCellWithSpace(
                ctx.Cells, cell, ItemIndex.Carrots, plant.yieldCount, maxRadius: 3,
                accept: c => zones.ZonePlantAt(c) < 0);
            if (at < 0)
                at = ctx.Items.NearestCellWithSpace(
                    ctx.Cells, cell, ItemIndex.Carrots, plant.yieldCount, maxRadius: 3);
            if (at >= 0) ctx.Items.Spawn(ItemIndex.Carrots, at, plant.yieldCount);
        }
    }
}
