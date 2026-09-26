#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Hand a miner an exposed face the player has ordered prospected (design 62 §7).
    ///
    /// <para>Mining work, and the same shape as <see cref="MineWorkGiver"/>: the scan walks the
    /// designation grid's own list of orders, never the board, and asks the mine giver's stance
    /// rule where to stand (<see cref="MineWorkGiver.StandAtFace"/>). It sorts after Mine by name
    /// inside the work type, so a colonist with both in reach cuts before she looks.</para>
    /// </summary>
    public sealed class ProspectWorkGiver : WorkGiver
    {
        public override string Name => "Prospect";

        public override int WorkType => WorkTypeIndex.Mining;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            var designations = ctx.Designations;
            if (designations == null) return false;

            var cells = designations.Cells;
            int best = -1, bestStand = -1, bestDistance = int.MaxValue;

            for (int i = 0; i < cells.Count; i++)
            {
                int cell = cells[i];
                if (designations.At(cell) != DesignationKind.Prospect) continue;
                if (!designations.CanProspect(cell)) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Cell, cell);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;

                // The face stays where it is, so standing on it strands nobody.
                int stand = MineWorkGiver.StandAtFace(ctx, pawn, cell, cutting: false);
                if (stand < 0) continue;

                bestDistance = distance;
                best = cell;
                bestStand = stand;
            }

            if (best < 0) return false;

            job.Reset(JobIndex.Prospect);
            job.TargetCell = bestStand;
            job.DestCell = best;
            return true;
        }
    }

    /// <summary>
    /// Walk to an exposed face, tap it for a few seconds, and learn what the rock round it is made
    /// of (design 62 §7). The face is left standing; the terrain of no cell changes.
    ///
    /// <para><b>The work is the job def's</b> (<c>Job_Prospect</c>'s <c>workTicks</c>, a fifth of
    /// cutting a cell of rock), at mining's pace, and counted on the job rather than banked on the
    /// cell: a prospect is seconds long, so an interrupted one costing its seconds again is not
    /// the hours of thrown-away work that moved mining's ledger on to the cell.</para>
    ///
    /// <para><b>How far it sees is the job def's too</b> — <see cref="JobDef.revealRadius"/> and the
    /// band table beside it — read at the instant the work is done, against the prospector's own
    /// Mining level (<see cref="RadiusFor"/>). The reveal itself is
    /// <see cref="CellGrid.RevealRockWithin"/>, the owner of the latch it writes, and runs in
    /// the deferred phase like every other world edit.</para>
    /// </summary>
    public class ProspectJobDriver : JobDriver
    {
        /// <summary>
        /// Layers above and below the face that a prospect reads: one each way (design 62 §7, "the
        /// same layer and one above and below"). A face is looked at, not sounded, so the reach is
        /// the height of a person's view along it rather than a depth.
        /// </summary>
        public const int LayersEachWay = 1;

        public override int WorkType => WorkTypeIndex.Mining;

        /// <summary>
        /// The face, once the walk is over and the tapping has started, exactly as
        /// <see cref="MineJobDriver.WorkFocus"/> is: the figure faces the rock and works it.
        /// </summary>
        public override int WorkFocus =>
            ToilIndex != 1 ? -1 : Job.DestCell >= 0 ? Job.DestCell : Job.TargetCell;

        /// <summary>
        /// How far a prospector at this Mining level sees: the def's radius, and one more cell for
        /// every band entry the level has reached. The band table is the def's alone; this counts.
        /// </summary>
        public static int RadiusFor(JobDef def, int level)
        {
            int radius = def.revealRadius;
            int[] bands = def.revealRadiusBands;
            for (int i = 0; i < bands.Length; i++)
                if (level >= bands[i]) radius++;
            return radius;
        }

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
            // Before every guard below: by now the order is cleared, so asking whether the face is
            // still ordered would fail the job on the first settle tick.
            if (ToilIndex == SettleToil) return Settle(ctx);

            var designations = ctx.Designations;
            if (designations == null) return JobStatus.Failed;

            int cell = Job.DestCell;
            // The player took the order off, or somebody mined the face away: stop.
            if (designations.At(cell) != DesignationKind.Prospect || !designations.CanProspect(cell))
                return JobStatus.Failed;

            if (ToilIndex == 0)
            {
                JobStatus walk = GotoCell(ctx, Job.TargetCell);
                if (walk == JobStatus.Succeeded) NextToil();
                return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            // Mining's envelope, because these are mining's stances: beside, on the rim or on top,
            // and from below reaching up.
            if (!StillInReach(ctx, Pawn, cell, layersAbove: 1, layersBelow: 1))
            {
                WalkBack();
                return JobStatus.Ongoing;
            }

            JobDef def = ctx.Content.Jobs[Job.DefIndex];
            ToilProgress += Pawn.WorkRatePerMille(WorkTypeIndex.Mining);
            Work(ctx);
            if (ToilProgress < def.workTicks * Rates.Scale) return JobStatus.Ongoing;

            designations.Clear(cell);
            int radius = RadiusFor(def, def.trainsSkill >= 0 ? Pawn.SkillLevel(def.trainsSkill) : 0);
            ctx.Defer(_ => Reveal(ctx, cell, radius));

            NextToil();
            return JobStatus.Ongoing;
        }

        /// <summary>
        /// The reveal, as the job lands it: every rock-like cell in the square is known now, and
        /// the chunks it sits in are re-meshed so a seam among them shows its glow. Public so a
        /// test can land one without walking a colonist to a face.
        /// </summary>
        public static int Reveal(PawnContext ctx, int cell, int radius) =>
            ctx.Cells.RevealRockWithin(cell, radius, LayersEachWay, ctx.Chunks);
    }
}
