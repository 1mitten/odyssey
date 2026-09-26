#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// What a bandit does when there is nobody left to fight and nothing left to break (design
    /// 33 §17; the owner, 2026-09-24: <i>"It will thieve items or kidnap people depending on their
    /// motivation creating a negative event (but they could be rescued later) - seam this later but
    /// for now - thieve items"</i>): it lifts the nearest stack it can reach, walks to the nearest
    /// edge of the board it can reach, and leaves, taking the stack with it. With nothing to take
    /// it leaves empty-handed; with no edge to reach it stays, and thinks again as it did.
    ///
    /// <para><b>The last thing a bandit's mind reaches for</b> (<see cref="HostileThinkNode"/>): a
    /// colonist it can reach comes first, then a colony building it may break, and only then what
    /// it came for. A thief looks up on <see cref="CombatDef.rechooseTicks"/> while it walks
    /// (<see cref="StealJobDriver"/>), so a colonist who can be reached again takes it back to the
    /// fight, and it drops what it was carrying to go.</para>
    ///
    /// <para><b>Leaving is not dying.</b> <see cref="Leave"/> takes the pawn off the board through
    /// <see cref="PawnRegistry.Despawn"/>, the one way off it, which ends every attack on it and
    /// releases its reservations; but there is no corpse, no <c>Died</c> report, no hook and so no
    /// mourning, and the ledger records a theft, not a death.</para>
    /// </summary>
    public static class Theft
    {
        /// <summary>
        /// Fill <paramref name="job"/> with a theft, if <paramref name="pawn"/> came for one and an
        /// edge of the board can be reached: the nearest stack it can lift and the edge nearest
        /// that stack, or — with nothing to lift — the edge nearest itself. False, touching nothing,
        /// for a pawn that came for nothing (<see cref="Motive.None"/>) and for one with no edge to
        /// reach, which falls through to idling.
        ///
        /// <para><b>Scales with the item stacks on the board</b> (every loose, stored and contained
        /// one: a branch and a reservation probe each, and a reachability test for each nearer
        /// than the best so far) plus the edge search, which is bounded by the board's side. Asked
        /// on a bandit's think, and only when it has nobody to fight and nothing to break.</para>
        /// </summary>
        public static bool TryFill(PawnContext ctx, Pawn pawn, TraverseMode mode, Job job)
        {
            switch (pawn.Motive)
            {
                case Motive.Loot:
                    break;
                case Motive.Kidnap:
                    // The seam (design 33 §17f): a kidnapper would carry a downed colonist to the
                    // edge in the rescue's cradle, for the colony to get back later. Until that unit
                    // it does exactly what a looter does, and TheftTests says so.
                    break;
                default:
                    return false;
            }

            ColonyItem? loot = NearestLoot(ctx, pawn, mode, out int at);
            int edge = loot != null ? EdgeFrom(ctx, at, mode) : -1;

            // A stack from which no edge can be reached is no loot: a pocket the drop into is
            // one way. It leaves empty-handed instead, if it can.
            if (edge < 0)
            {
                loot = null;
                at = -1;
                edge = EdgeFrom(ctx, pawn.Cell, mode);
            }
            if (edge < 0) return false;

            job.Reset(JobIndex.Steal);
            job.TargetItem = loot?.Id ?? ThingId.None;
            job.TargetCell = at;
            job.DestCell = edge;
            job.Mode = mode;
            pawn.CombatTarget = 0;
            return true;
        }

        /// <summary>
        /// Fill <paramref name="job"/> with a walk off the board and nothing taken: a raid member
        /// withdrawing (design 53 §6). The thief's own job with no loot, so the leaving, the ledger
        /// and the weapon going with it are the thief's; a member already carrying loot is in its own
        /// theft and keeps it. False, touching nothing, with no edge to reach.
        /// </summary>
        public static bool FillLeave(PawnContext ctx, Pawn pawn, TraverseMode mode, Job job)
        {
            int edge = EdgeFrom(ctx, pawn.Cell, mode);
            if (edge < 0) return false;
            job.Reset(JobIndex.Steal);
            job.TargetItem = ThingId.None;
            job.TargetCell = -1;
            job.DestCell = edge;
            job.Mode = mode;
            pawn.CombatTarget = 0;
            return true;
        }

        /// <summary>
        /// The nearest cell on the board's edge that can be reached from <paramref name="origin"/>
        /// in <paramref name="mode"/> — <paramref name="origin"/> itself when it is on the edge.
        /// </summary>
        public static int EdgeFrom(PawnContext ctx, int origin, TraverseMode mode)
        {
            if ((uint)origin >= (uint)ctx.Size.CellCount) return -1;
            if (Wildlife.WildlifeSystem.IsEdge(ctx.Size, origin)) return origin;
            return EdgeTarget.Find(ctx, origin, mode);
        }

        /// <summary>
        /// The stack a thief takes (design 33 §17): of every thing on the ground or in a store —
        /// the three listers a hauler walks — that can be carried, that nobody else has claimed,
        /// and that <paramref name="pawn"/> can reach in <paramref name="mode"/>, the nearest by
        /// <see cref="PawnContext.Distance"/>, <b>a tie to the lower item id</b>, so the choice is
        /// the same whatever order the listers hold them in and after a load. There is no value
        /// yet, so nearest is the whole of the choice. A forbidden thing is taken too: forbidding
        /// is the colony's word to its own people. A weapon in somebody's hand, a load in somebody's
        /// arms and a thing already despawned have no place (<see cref="PawnContext.WhereIs"/>) and
        /// are passed over.
        /// </summary>
        public static ColonyItem? NearestLoot(PawnContext ctx, Pawn pawn, TraverseMode mode, out int where)
        {
            where = -1;
            ColonyItem? best = null;
            int bestDistance = int.MaxValue;
            Consider(ctx, pawn, mode, ctx.Items.LooseItems, ref best, ref bestDistance, ref where);
            Consider(ctx, pawn, mode, ctx.Items.StoredItems, ref best, ref bestDistance, ref where);
            Consider(ctx, pawn, mode, ctx.Items.ContainedItems, ref best, ref bestDistance, ref where);
            return best;
        }

        static void Consider(PawnContext ctx, Pawn pawn, TraverseMode mode, System.Collections.Generic.IReadOnlyList<int> list,
            ref ColonyItem? best, ref int bestDistance, ref int where)
        {
            var items = ctx.Items.Items;
            for (int i = 0; i < list.Count; i++)
            {
                ColonyItem item = items[list[i]];
                if (item.Despawned) continue;
                int at = ctx.WhereIs(item);
                if (at < 0) continue;
                if (!ctx.Content.Items[item.DefIndex].haulable) continue;

                int distance = ctx.Distance(pawn.Cell, at);
                if (distance > bestDistance) continue;
                if (distance == bestDistance && best != null && item.Id.Value >= best.Id.Value) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Item, item.Id.Value);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;
                if (at != pawn.Cell && !ctx.CanTravel(pawn, at, mode)) continue;

                best = item;
                bestDistance = distance;
                where = at;
            }
        }

        /// <summary>
        /// Take a thief that has reached its edge off the board (design 33 §17): the stack in its
        /// arms and the weapon in its hand go with it, the ledger records a theft — or, with
        /// nothing carried, a bandit leaving — and the pawn is despawned. <b>Deferred</b> to the
        /// end of the tick by the driver (<see cref="PawnContext.Defer"/>), for death's reason:
        /// <see cref="PawnRegistry.Despawn"/> shifts the list every pawn loop walks.
        ///
        /// <para>Asks again that it is still a thief standing on its edge, because the fight's pass
        /// runs between the driver and the end of the tick: a thief downed there has already dropped
        /// its load (<c>JobDriver.DropCarried</c>) and is not leaving.</para>
        ///
        /// <para><b>Its weapon leaves with it.</b> <see cref="PawnRegistry.Despawn"/> puts a held
        /// weapon down where the pawn stood, which is right for a death and for a pawn that is simply
        /// gone; a bandit walking off with its machete has not been disarmed, and leaving one on
        /// the edge of the board for every thief would arm the colony for free.</para>
        /// </summary>
        public static bool Leave(PawnContext ctx, Pawn pawn, int tick)
        {
            if (ctx.Pawns.Get(pawn.Id) != pawn) return false;
            Job? job = pawn.CurrentJob;
            if (pawn.Downed || job == null || job.DefIndex != JobIndex.Steal || pawn.Cell != job.DestCell) return false;

            int subject = -1, amount = 0;
            if (job.CarriedItem >= 0)
            {
                ColonyItem? load = ctx.Items.Get(new ThingId(job.CarriedItem));
                // Out of the job before the job ends, or its cleanup would put the load down.
                job.CarriedItem = -1;
                if (load != null && !load.Despawned)
                {
                    subject = load.DefIndex;
                    amount = load.Stack;
                    ctx.Items.Despawn(load);
                }
            }

            ColonyItem? weapon = WeaponHand.Held(pawn, ctx);
            pawn.EquippedItem = 0;
            if (weapon != null) ctx.Items.Despawn(weapon);

            // A raid member leaving empty-handed writes nothing (design 53 §6): a band of a hundred
            // would post a hundred rows. A theft is still written: that stack is a real loss.
            bool raider = ctx.Raids?.GroupOf(pawn.Id.Value) != null;
            if (subject >= 0 || !raider)
                ctx.Incidents?.Ledger.Record(
                    subject >= 0 ? IncidentHandle.Theft : IncidentHandle.BanditLeft, pawn.Cell, tick, subject, amount);
            ctx.Raids?.NoteLeft(pawn);

            ctx.Combat?.Jobs.EndJob(pawn, JobStatus.Succeeded);
            ctx.Pawns.Despawn(pawn);
            return true;
        }
    }
}
