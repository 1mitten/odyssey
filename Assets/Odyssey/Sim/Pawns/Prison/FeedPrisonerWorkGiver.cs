#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// A warden feeds a hungry prisoner who cannot feed herself (design 60 §7): one who is
    /// shackled, down, kept in no cell, or in a cell with nothing to eat in it. The nearest such
    /// prisoner, and the best food nearest the warden — the eater's own rule, best tier first —
    /// never food lying in a cell, which is somebody's already.
    ///
    /// <para><b>Nobody held, nothing touched</b>: the scan asks custody first, so a colony with no
    /// prisoner pays one comparison a pawn.</para>
    /// </summary>
    public sealed class FeedPrisonerWorkGiver : WorkGiver
    {
        public override string Name => "FeedPrisoner";

        public override int WorkType => WorkTypeIndex.Warden;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (!pawn.IsColonist || pawn.Downed) return false;

            Pawn? hungry = null;
            int bestDistance = int.MaxValue;
            int seek = ctx.Content.Needs[NeedIndex.Food].seekThreshold;
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn prisoner = pawns[i];
                if (prisoner.Custody != PawnCustody.Prisoner || prisoner.CarriedBy != 0) continue;
                if (prisoner.Needs[NeedIndex.Food] >= seek) continue;
                if (CanFeedHerself(prisoner, ctx)) continue;
                int distance = ctx.Distance(pawn.Cell, prisoner.Cell);
                if (distance >= bestDistance) continue;
                if (!ctx.Reservations.CanReserve(pawn.Id, ReservationManager.Key(ReservationTargetKind.Pawn, prisoner.Id.Value))) continue;
                if (!ctx.Reachable(pawn, prisoner.Cell)) continue;
                hungry = prisoner;
                bestDistance = distance;
            }
            if (hungry == null) return false;

            ColonyItem? food = NearestFood(pawn, ctx, out int at);
            if (food == null) return false;

            job.Reset(JobIndex.FeedPrisoner);
            job.TargetItem = food.Id;
            job.TargetCell = at;
            job.WorkTicks = hungry.Id.Value;
            return true;
        }

        /// <summary>Whether food lies in her own cell for her to walk to: she is standing, in a cell, and it has some.</summary>
        static bool CanFeedHerself(Pawn prisoner, PawnContext ctx)
        {
            if (prisoner.Downed || PrisonerTrees.IsShackled(prisoner, ctx)) return false;
            int room = PrisonerTrees.CellRoom(prisoner, ctx);
            if (room == 0) return false;
            var items = ctx.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Despawned || item.Forbidden || ctx.Content.Items[item.DefIndex].nutrition <= 0) continue;
                int at = ctx.WhereIs(item);
                if (at >= 0 && ctx.Enclosure!.RoomAt(at) == room) return true;
            }
            return false;
        }

        /// <summary>The best food, then the nearest of it, that the warden can reach and claim and no cell holds.</summary>
        static ColonyItem? NearestFood(Pawn pawn, PawnContext ctx, out int bestCell)
        {
            var items = ctx.Items.Items;
            ColonyItem? best = null;
            int bestTier = int.MaxValue, bestDistance = int.MaxValue;
            bestCell = -1;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Despawned || item.Forbidden) continue;
                ItemDef food = ctx.Content.Items[item.DefIndex];
                if (food.nutrition <= 0 || food.foodTier > bestTier) continue;
                int at = ctx.WhereIs(item);
                if (at < 0 || PrisonCells.Holds(ctx, at)) continue;
                int distance = ctx.Distance(pawn.Cell, at);
                if (food.foodTier == bestTier && distance >= bestDistance) continue;
                if (!ctx.Reservations.CanReserve(pawn.Id, ReservationManager.Key(ReservationTargetKind.Item, item.Id.Value))) continue;
                if (!ctx.Reachable(pawn, at)) continue;
                best = item;
                bestTier = food.foodTier;
                bestDistance = distance;
                bestCell = at;
            }
            return best;
        }
    }
}
