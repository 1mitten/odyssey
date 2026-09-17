#nullable enable
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Walk to one of our own buildings and take it apart, leaving half of what it cost.
    ///
    /// <para><b>It is building's driver run backwards, and shares its shape on purpose.</b> Stand
    /// beside rather than inside (the wall is there until the moment it is not); bank the work on
    /// the cell so a colonist who breaks off for a meal does not throw the morning away; defer the
    /// world edit to the structural phase like every other removal.</para>
    ///
    /// <para><b>Only what we built.</b> <c>DesignationGrid.CanDeconstruct</c> is the rule and it
    /// asks <see cref="PlacedEdifice.Built"/> — the ruined city is Reclaim's and Salvage's, with
    /// their own yields.</para>
    /// </summary>
    public sealed class DeconstructWorkGiver : WorkGiver
    {
        public override string Name => "Deconstruct";

        public override int WorkType => WorkTypeIndex.Construction;

        /// <summary>
        /// After delivering and building.
        ///
        /// <para>A colony that pulls a wall down while a half-ordered hut stands waiting for its
        /// last plank finishes neither. Demolition is also the one job here that is never urgent:
        /// the thing being removed is already standing and doing its job.</para>
        /// </summary>
        public override int IntraPriority => 2;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            var designations = ctx.Designations;
            if (designations == null) return false;

            var cells = designations.Cells;
            int best = -1, bestStand = -1, bestDistance = int.MaxValue;

            for (int i = 0; i < cells.Count; i++)
            {
                int cell = cells[i];
                if (designations.At(cell) != DesignationKind.Deconstruct) continue;
                if (!designations.CanDeconstruct(cell)) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Cell, cell);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;

                // The same stance building uses, and for the same reason turned round: a wall
                // fills its cell, so there is nowhere to stand but beside it.
                int stand = FellJobDriver.StandBeside(ctx, pawn, cell);
                if (stand < 0) continue;

                bestDistance = distance;
                best = cell;
                bestStand = stand;
            }

            if (best < 0) return false;

            job.Reset(JobIndex.Deconstruct);
            job.TargetCell = bestStand;
            job.DestCell = best;
            return true;
        }
    }

    public class DeconstructJobDriver : JobDriver
    {
        /// <summary>The wall being taken down, so the figure swings at it rather than at its own feet.</summary>
        public override int WorkFocus =>
            ToilIndex != 1 ? -1 : Job.DestCell >= 0 ? Job.DestCell : Job.TargetCell;

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
            // By now the wall is down and its order cleared, so asking whether it is still there
            // would fail the job on the first settle tick.
            if (ToilIndex == SettleToil) return Settle(ctx);

            var designations = ctx.Designations;
            if (designations == null) return JobStatus.Failed;

            int cell = Job.DestCell;
            // Somebody else pulled it down, or the player changed their mind.
            if (designations.At(cell) != DesignationKind.Deconstruct || !designations.CanDeconstruct(cell))
                return JobStatus.Failed;

            if (ToilIndex == 0)
            {
                JobStatus walk = GotoCell(ctx, Job.TargetCell);
                if (walk == JobStatus.Succeeded) NextToil();
                return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            if (!StillInReach(ctx, Pawn, cell, layersAbove: 0, layersBelow: 0))
            {
                WalkBack();
                return JobStatus.Ongoing;
            }

            if (!designations.TryEdifice(cell, out PlacedEdifice placed)) return JobStatus.Failed;
            int building = ConstructionContent.BuildingForEdifice(placed.Def);
            int stuff = ConstructionContent.StuffForValue(placed.Stuff);
            if (building == BuildingHandle.None) return JobStatus.Failed;

            ToilProgress++;
            if (designations.AddWork(cell, 1) < ConstructionContent.WorkToDeconstruct(building, stuff))
                return JobStatus.Ongoing;

            designations.Clear(cell);
            int tick = ctx.CurrentTick;
            ctx.Defer(_ => TakeApart(ctx, cell, building, stuff, tick));

            // The wall comes down now; the colonist straightens up before walking off.
            NextToil();
            return JobStatus.Ongoing;
        }

        /// <summary>Take the building out of the world and leave the salvage.</summary>
        public static void TakeApart(PawnContext ctx, int cell, int building, int stuff, int tick)
        {
            // The world edit belongs to the grid that raised it, beside `Raise` so the two cannot
            // drift. What is left here is the salvage, which is this job's own business.
            if (ctx.Construction == null || !ctx.Construction.Demolish(ctx, cell, out _)) return;

            // Where the wall stood, or as near as will take it — the same landing felled wood and
            // mined stone already use, so a row of walls comes down into a few stacks rather than
            // a scatter.
            int refund = Refund(ctx, cell, building, stuff, tick);
            if (refund <= 0) return;

            int item = ConstructionContent.StuffAt(stuff).item;
            if (item < 0) return;

            int at = ctx.Items.NearestCellWithSpace(ctx.Cells, cell, item, refund, maxRadius: 3);
            if (at >= 0) ctx.Items.Spawn(item, at, refund);
        }

        /// <summary>
        /// Half of what it cost, with the odd unit decided by a seeded coin flip.
        ///
        /// <para>The reference's own behaviour (<c>a-04</c> §1: 50%, stochastic rounding), and the
        /// flip is the point rather than the decoration. Round down always and a wall is
        /// predictably lossy, so the arithmetic of when to build one is solvable on paper; round up
        /// always and a wall is a warehouse you can store wood in for free. Neither makes the
        /// choice to build interesting.</para>
        ///
        /// <para><b>Deterministic, necessarily.</b> The refund lands in the state hash, so a
        /// <c>System.Random</c> here would desync a replay on the first demolished wall. See
        /// <see cref="PawnPurpose.DeconstructRefund"/> for why the tick is in the key and the cell
        /// alone is not enough.</para>
        /// </summary>
        public static int Refund(PawnContext ctx, int cell, int building, int stuff, int tick)
        {
            int cost = ConstructionContent.BuildingAt(building).costCount;
            int half = cost / 2;
            if (cost % 2 == 0) return half;

            var rng = DeterministicRandom.ForTick(ctx.Seed, cell ^ tick, PawnPurpose.DeconstructRefund);
            return half + (rng.NextInt(2) == 0 ? 0 : 1);
        }
    }
}
