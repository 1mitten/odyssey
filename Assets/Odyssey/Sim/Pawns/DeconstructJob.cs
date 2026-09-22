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

                // **A store with anything in it is not offered to a deconstructor at all.** The
                // order still stands, and haulers can see it: an ordered store ranks its contents
                // below every real store, so the colony empties it first and a deconstructor
                // arrives to a store that is already empty.
                //
                // Without this gate the refusal below is a loop rather than a rule. The work is
                // banked on the cell, so a colonist would walk over, swing until the work was
                // done, be refused, and be handed the same site again on the next think — for
                // ever, until the think-tree's own circuit breaker parked her. The fault would
                // then read as idleness, which is a long way from "that shelf cannot be emptied".
                if (ctx.StorageUnits != null)
                {
                    Storage.StorageUnit? store = ctx.StorageUnits.AtCell(cell);
                    if (store != null && !ctx.StorageUnits.IsEmpty(store)) continue;
                }

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
        public override int WorkType => WorkTypeIndex.Construction;

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

            // A wall or one of our own floors: the resolver knows the difference and the rest of
            // this driver does not need to.
            if (!designations.TryTakeApart(cell, out int building, out int stuff)) return JobStatus.Failed;

            int rate = Pawn.WorkRatePerMille(WorkTypeIndex.Construction);
            ToilProgress += rate;
            if (designations.AddWork(cell, rate) < ConstructionContent.WorkToDeconstruct(building, stuff) * Rates.Scale)
                return JobStatus.Ongoing;

            // The last word before the thing comes down: a store whose contents have nowhere to go
            // is **not** taken apart. A player's order that cannot be carried out is refused, not
            // approximated — losing things to a building collapsing is a consequence, losing them
            // to your own tidying is a bug.
            //
            // Reached only when something was put into the store during the final swing, because
            // the giver above will not offer a store that is not already empty. It is the guard
            // rather than the rule, and it is here because "the gate held" is not something to
            // assume about a colony with several colonists in it.
            if (ctx.StorageUnits != null)
            {
                Storage.StorageUnit? store = ctx.StorageUnits.AtCell(cell);
                if (store != null && !ctx.StorageUnits.CanSpillAll(ctx, store)) return JobStatus.Failed;
            }

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
            //
            // Two removals rather than one because there are two kinds of thing: a wall standing in
            // the cell and a floor at its lower boundary. Whichever it is, the grid that put it
            // there is the one that takes it away — and taking a floor away is the edit that lets a
            // player pull the last support out of a room and watch the rest come down.
            if (ctx.Construction == null) return;
            bool removed = ConstructionContent.BuildingAt(building).slab
                ? ctx.Construction.RemoveSlab(ctx, cell, out _)
                : ctx.Construction.Demolish(ctx, cell, out _);
            if (!removed) return;

            // Where the wall or floor stood, or as near as will take it on a real floor below.
            // Slabs and open-air cuts resolve to FirstFloorAtOrBelow so refunds never hang in mid-air.
            int refund = Refund(ctx, cell, building, stuff, tick);
            if (refund <= 0) return;

            int item = ConstructionContent.StuffAt(stuff).item;
            if (item < 0) return;

            int landing = ctx.Cells.FirstFloorAtOrBelow(cell);
            int at = ctx.Items.NearestCellWithSpace(ctx.Cells, landing, item, refund, maxRadius: 3);
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
