#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The mental-break taxonomy (design 43 §5c): which break a colonist falls into, and what each
    /// one does as a job. <b>One owner</b> for both halves, so the requirement that makes a break
    /// eligible and the search that gives it something to do cannot disagree — a tantrum chosen
    /// because a wall was in reach is a tantrum that finds that wall.
    ///
    /// <para><b>Every break falls back to wandering</b> when it has nothing to do: a binge with no
    /// food left, a tantrum with nothing in reach, a berserker with nobody near. The state lasts its
    /// drawn length either way; the fall-back only decides the job.</para>
    ///
    /// <para><b>Cost.</b> <see cref="Choose"/> runs once per break and asks what the eat and bandit
    /// givers already ask; the fills run when a broken colonist's job ends, the think-tree cadence.
    /// Nothing here is per tick.</para>
    /// </summary>
    public static class MentalBreaks
    {
        /// <summary>
        /// Which break, from the tier she is under (design 43 §5c): a weighted pick by
        /// <see cref="MentalBreakDef.commonality"/> among that tier's breaks whose requirement holds,
        /// falling through to the next shallower tier when none does, and to the wander at the end.
        /// Draws from <paramref name="rng"/>, the break's own stream, after the draw that decided the
        /// break at all.
        /// </summary>
        public static int Choose(Pawn pawn, PawnContext ctx, int tier, ref DeterministicRandom rng)
        {
            MentalBreakDef[] breaks = ctx.Content.Breaks;
            for (int t = tier; t >= BreakHandle.Minor; t--)
            {
                int total = 0;
                for (int k = 0; k < breaks.Length; k++)
                    if (breaks[k].tier == t && Eligible(pawn, ctx, k)) total += breaks[k].commonality;
                if (total <= 0) continue;

                int roll = rng.NextInt(total);
                for (int k = 0; k < breaks.Length; k++)
                {
                    if (breaks[k].tier != t || !Eligible(pawn, ctx, k)) continue;
                    roll -= breaks[k].commonality;
                    if (roll < 0) return k;
                }
            }
            return BreakHandle.Wander;
        }

        /// <summary>Can this break happen to her now? Binge wants food; tantrum wants something to strike.</summary>
        public static bool Eligible(Pawn pawn, PawnContext ctx, int kind)
        {
            if (ctx.Content.Breaks[kind].commonality <= 0) return false;
            switch (kind)
            {
                case BreakHandle.Binge: return HasFood(pawn, ctx);
                case BreakHandle.Tantrum: return TryBuilding(pawn, ctx, out _);
                default: return true;
            }
        }

        /// <summary>
        /// The job for a colonist in a break: the one branch per <see cref="BreakHandle"/>, and the
        /// wander when the break has nothing to do.
        /// </summary>
        public static bool Fill(Pawn pawn, PawnContext ctx, Job job)
        {
            switch (pawn.BreakKind)
            {
                case BreakHandle.Sulk:
                    return Sulk(pawn, ctx, job);
                case BreakHandle.Binge:
                    return CriticalNeedsThinkNode.TryEat(pawn, ctx, job) || WanderTarget.Fill(pawn, ctx, job);
                case BreakHandle.Tantrum:
                    return TryBuilding(pawn, ctx, out BuildingTarget building)
                        ? AttackJob.FillBuilding(ctx, pawn, building, job, pawn.OwnMode)
                        : WanderTarget.Fill(pawn, ctx, job);
                case BreakHandle.Berserk:
                    Pawn? victim = NearestStanding(pawn, ctx);
                    return victim != null
                        ? AttackJob.Fill(pawn, victim, job, pawn.OwnMode)
                        : WanderTarget.Fill(pawn, ctx, job);
                default:
                    return WanderTarget.Fill(pawn, ctx, job);
            }
        }

        /// <summary>
        /// May a colonist in this break be running this job? Her break's own jobs, and the wander
        /// every break falls back to; anything else is ended as a failure (design 43 §5c).
        /// </summary>
        public static bool IsBreakJob(Pawn pawn, Job job, PawnContent content)
        {
            if (content.Jobs[job.DefIndex].driver == JobIndex.Wander) return true;
            switch (pawn.BreakKind)
            {
                case BreakHandle.Sulk: return job.DefIndex == JobIndex.Wait;
                case BreakHandle.Binge: return job.DefIndex == JobIndex.Eat;
                case BreakHandle.Tantrum:
                case BreakHandle.Berserk: return job.DefIndex == JobIndex.AttackMelee;
                default: return false;
            }
        }

        /// <summary>How long a sulker stands at her bed before she thinks again.</summary>
        public const int SulkStandTicks = 600;

        /// <summary>
        /// Walk to her own bed and stand at it, doing nothing; with no bed of her own she can reach,
        /// stand where she is. The walk is a wander to the bed's cell, so the job filter needs no
        /// case of its own for it.
        /// </summary>
        static bool Sulk(Pawn pawn, PawnContext ctx, Job job)
        {
            int bed = OwnBed(pawn, ctx);
            if (bed >= 0 && bed != pawn.Cell)
            {
                job.Reset(JobIndex.Wander);
                job.TargetCell = bed;
                job.Mode = pawn.OwnMode;
                return true;
            }

            job.Reset(JobIndex.Wait);
            job.WorkTicks = SulkStandTicks;
            return true;
        }

        static int OwnBed(Pawn pawn, PawnContext ctx)
        {
            if (ctx.Construction == null) return -1;
            var beds = ctx.Items.Beds;
            int me = pawn.Id.Value;
            for (int i = 0; i < beds.Count; i++)
            {
                int cell = beds[i];
                if (ctx.Construction.BedOwnerAt(cell) != me) continue;
                return ctx.Reachable(pawn, cell) ? cell : -1;
            }
            return -1;
        }

        /// <summary>Is there any food she could walk to and eat? The eat giver's question, without claiming it.</summary>
        static bool HasFood(Pawn pawn, PawnContext ctx)
        {
            var items = ctx.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Despawned || item.Forbidden) continue;
                if (ctx.Content.Items[item.DefIndex].nutrition <= 0) continue;
                int at = ctx.WhereIs(item);
                if (at >= 0 && ctx.Reachable(pawn, at)) return true;
            }
            return false;
        }

        /// <summary>
        /// The nearest colony building within the tantrum's reach that she can take a side of — the
        /// bandit's own search (beds are passed over, design 33 §16), held to the break's reach.
        /// </summary>
        static bool TryBuilding(Pawn pawn, PawnContext ctx, out BuildingTarget building)
        {
            building = default;
            int reach = ctx.Content.Breaks[BreakHandle.Tantrum].reachCells * 100;
            if (!BuildingTargets.TryNearestColonyTarget(ctx, pawn, pawn.OwnMode, out building)) return false;
            return ctx.Distance(pawn.Cell, building.Anchor) <= reach;
        }

        /// <summary>
        /// The nearest standing pawn within the berserker's reach that she can walk to: a colonist,
        /// a bandit or an animal, anybody but herself.
        /// </summary>
        static Pawn? NearestStanding(Pawn pawn, PawnContext ctx)
        {
            int reach = ctx.Content.Breaks[BreakHandle.Berserk].reachCells * 100;
            Pawn? best = null;
            int bestDistance = int.MaxValue;
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == pawn || !Melee.IsStanding(other) || other.CarriedBy != 0) continue;
                int distance = ctx.Distance(pawn.Cell, other.Cell);
                if (distance > reach || distance >= bestDistance) continue;
                if (!ctx.Reachable(pawn, other.Cell)) continue;
                best = other;
                bestDistance = distance;
            }
            return best;
        }
    }
}
