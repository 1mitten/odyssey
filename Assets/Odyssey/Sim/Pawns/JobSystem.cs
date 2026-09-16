#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// One node of the think tree. A node either fills in a job and returns true, or declines.
    ///
    /// The tree is traversed depth-first and the <b>first valid job wins</b>. This is an ordered
    /// scan, not a global utility argmax, and that matters twice: it is far cheaper, and it is
    /// predictable, so a player can reason about why a colonist did something. The scan order
    /// <em>is</em> the tuning surface.
    /// </summary>
    public abstract class ThinkNode
    {
        public abstract string Name { get; }

        public abstract bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job);
    }

    /// <summary>
    /// One scanner inside the work branch. Givers sit on a flat, pre-sorted list; the player's
    /// 1..4 priority decides which pass picks them up.
    /// </summary>
    public abstract class WorkGiver
    {
        public abstract string Name { get; }

        /// <summary>The work type this giver belongs to, as a <see cref="WorkTypeIndex"/> value.</summary>
        public abstract int WorkType { get; }

        /// <summary>Order within the work type. Lower scans first.</summary>
        public virtual int IntraPriority => 0;

        /// <summary>Scanned ahead of non-emergency givers whatever the priority numbers say.</summary>
        public virtual bool Emergency => false;

        public abstract bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job);
    }

    /// <summary>
    /// The think tree, job execution and the work scan.
    ///
    /// The pipeline is <c>think tree → work giver → job → toils</c>, and the shape is deliberately
    /// unclever because it is well proven. On every tick a pawn either advances the toils of the
    /// job it has, or consults the tree for a new one. A job that ends for <em>any</em> reason —
    /// success, failure, expiry, a mental break taking over — releases every reservation it held
    /// before the next think runs. That single rule is what a reservation leak is the absence of,
    /// and it is the fault a ten-day unattended run surfaces and a two-minute test does not.
    /// </summary>
    public sealed class JobSystem : IWorldSystem, IStateHashable, ISaveable
    {
        readonly PawnContext _ctx;
        readonly ThinkNode[] _tree;
        readonly WorkGiver[] _givers;

        public JobSystem(PawnContext ctx) : this(ctx, DefaultTree(), DefaultGivers()) { }

        public JobSystem(PawnContext ctx, ThinkNode[] tree, WorkGiver[] givers)
        {
            _ctx = ctx;
            _givers = SortGivers(ctx, givers);
            _tree = tree;
            _completed = new int[ctx.Content.Jobs.Length];
            _failed = new int[ctx.Content.Jobs.Length];
            for (int i = 0; i < _tree.Length; i++)
                if (_tree[i] is WorkThinkNode work) work.Bind(_givers);
        }

        public string Name => "Jobs";

        public TickPhase Phase => TickPhase.Pawns;

        /// <summary>After needs, before movement: decide with settled needs, then walk.</summary>
        public int Order => 20;

        /// <summary>The think tree, in traversal order. Exposed so a test can assert the order.</summary>
        public IReadOnlyList<ThinkNode> Tree => _tree;

        /// <summary>The flat, pre-sorted giver list. Sorted once, never per tick.</summary>
        public IReadOnlyList<WorkGiver> Givers => _givers;

        public int JobsStarted { get; private set; }
        public int JobsFailed { get; private set; }

        /// <summary>
        /// Jobs that ended, per job def, split by outcome.
        ///
        /// <para>A soak run's only real question is "did the colony keep working", and a total
        /// job count cannot answer it: five pawns wandering in a mental break for a day start and
        /// end thousands of jobs. Per def, "at least one haul, one meal and one sleep completed"
        /// is the assertion that actually distinguishes a living colony from a busy one.</para>
        ///
        /// <para>Counted in <see cref="EndJob"/>, which is the single funnel every ending goes
        /// through, rather than at the call sites — two of which were previously reached without
        /// incrementing <see cref="JobsFailed"/> at all.</para>
        /// </summary>
        public int CompletedOf(int jobDefIndex) => _completed[jobDefIndex];

        public int FailedOf(int jobDefIndex) => _failed[jobDefIndex];

        readonly int[] _completed;
        readonly int[] _failed;

        /// <summary>
        /// Sorted once at construction by emergency flag, then work-type order, then the giver's
        /// own intra-type number. The player's 1..4 priority is per pawn and cannot be baked in
        /// here, so the scan makes one pass per priority level over this list instead.
        /// </summary>
        static WorkGiver[] SortGivers(PawnContext ctx, WorkGiver[] givers)
        {
            var sorted = new List<WorkGiver>(givers);
            sorted.Sort((a, b) =>
            {
                if (a.Emergency != b.Emergency) return a.Emergency ? -1 : 1;
                int byType = ctx.Content.WorkTypes[a.WorkType].order
                    .CompareTo(ctx.Content.WorkTypes[b.WorkType].order);
                if (byType != 0) return byType;
                int byIntra = a.IntraPriority.CompareTo(b.IntraPriority);
                return byIntra != 0 ? byIntra : string.CompareOrdinal(a.Name, b.Name);
            });
            return sorted.ToArray();
        }

        public static ThinkNode[] DefaultTree() => new ThinkNode[]
        {
            new MentalStateThinkNode(),
            new CriticalNeedsThinkNode(),
            new WorkThinkNode(),
            new IdleThinkNode(),
        };

        public static WorkGiver[] DefaultGivers() => new WorkGiver[] { new HaulWorkGiver() };

        public void Tick(SimWorld world)
        {
            _ctx.Sync(world);
            var pawns = _ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++) TickPawn(pawns[i], world.CurrentTick);
        }

        void TickPawn(Pawn pawn, int tick)
        {
            if (pawn.BreakTicksLeft > 0)
            {
                pawn.BreakTicksLeft--;
                if (pawn.BreakTicksLeft == 0)
                {
                    // Breaks are self-limiting: the aftermath lifts mood clear of the threshold,
                    // so a broken colonist recovers rather than cycling forever.
                    pawn.AddMemory(_ctx.Content.Break.catharsisThought, tick);
                    EndJob(pawn, JobStatus.Succeeded);
                }
                else if (pawn.CurrentJob != null && !IsBreakJob(pawn, pawn.CurrentJob))
                {
                    // A forced interrupt. The job ends as a failure, which releases its claims.
                    EndJob(pawn, JobStatus.Failed);
                }
            }

            if (pawn.CurrentJob != null)
            {
                var def = _ctx.Content.Jobs[pawn.CurrentJob.DefIndex];
                if (def.expiryTicks > 0 && tick - pawn.JobStartTick >= def.expiryTicks)
                {
                    EndJob(pawn, JobStatus.Succeeded);
                }
                else
                {
                    JobStatus status = pawn.Driver!.Tick(_ctx);
                    if (status != JobStatus.Ongoing)
                    {
                        if (status == JobStatus.Failed) JobsFailed++;
                        EndJob(pawn, status);
                    }
                }
            }

            // The tree is re-consulted in the same tick a job ends, exactly as the research
            // describes, so a pawn never stands idle for a tick it did not need to.
            if (pawn.CurrentJob == null) Think(pawn, tick);
        }

        bool IsBreakJob(Pawn pawn, Job job) =>
            _ctx.Content.Jobs[job.DefIndex].driver == JobIndex.Wander;

        // ---- state ------------------------------------------------------------------------
        //
        // The counters are hashed, which makes them state rather than statistics: two runs that
        // reach the same world by different sequences of job outcomes are no longer allowed to
        // agree. That is the point — a job that silently fails and is silently retried leaves
        // the world identical and the counters different.
        //
        // Hashed therefore means saved. A counter in the hash and not in the file is a save that
        // resumes wrongly, which is the failure this pairing exists to prevent; the round-trip
        // test in WorldRoundTripTests is what enforces it.

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(JobsStarted);
            hash.Add(JobsFailed);
            for (int i = 0; i < _completed.Length; i++)
            {
                hash.Add(_completed[i]);
                hash.Add(_failed[i]);
            }
        }

        public string SaveKey => "odyssey.jobs";

        public void Save(SaveWriter writer)
        {
            writer.Write(JobsStarted);
            writer.Write(JobsFailed);
            writer.Write(_completed.Length);
            for (int i = 0; i < _completed.Length; i++)
            {
                writer.Write(_completed[i]);
                writer.Write(_failed[i]);
            }
        }

        public void Load(SaveReader reader)
        {
            JobsStarted = reader.ReadInt();
            JobsFailed = reader.ReadInt();

            int count = reader.ReadInt();
            if (count != _completed.Length)
                throw new SaveLoadException(
                    $"The save has {count} job defs and this build has {_completed.Length}. Def " +
                    "migration is not written yet, and guessing at the mapping would silently " +
                    "attribute one job's history to another.");

            for (int i = 0; i < count; i++)
            {
                _completed[i] = reader.ReadInt();
                _failed[i] = reader.ReadInt();
            }
        }

        void Think(Pawn pawn, int tick)
        {
            // The circuit breaker. A pawn that starts job after job inside a short window is in a
            // think loop; parking it for a while is cheaper than rescanning forever and is how
            // the fault becomes visible instead of becoming a frame-rate mystery.
            if (tick - pawn.WindowStartTick >= _ctx.Content.ThinkLoopWindowTicks)
            {
                pawn.WindowStartTick = tick;
                pawn.JobStartsInWindow = 0;
            }

            if (pawn.JobStartsInWindow >= _ctx.Content.ThinkLoopLimit)
            {
                var standDown = pawn.JobBuffer;
                standDown.Reset(JobIndex.Wait);
                standDown.WorkTicks = _ctx.Content.StandDownTicks;
                pawn.JobStartsInWindow = 0;
                pawn.WindowStartTick = tick;
                StartJob(pawn, standDown, tick);
                return;
            }

            var job = pawn.JobBuffer;
            for (int i = 0; i < _tree.Length; i++)
            {
                job.Reset(JobIndex.Wait);
                if (!_tree[i].TryGiveJob(pawn, _ctx, job)) continue;
                if (StartJob(pawn, job, tick)) return;
            }
        }

        /// <summary>
        /// Claim everything, then run. A driver whose claims cannot all be taken releases what it
        /// did take and the job never starts — all or nothing, with no half-claimed state that a
        /// later failure would have to unpick.
        /// </summary>
        public bool StartJob(Pawn pawn, Job job, int tick)
        {
            var driver = pawn.DriverPool[_ctx.Content.Jobs[job.DefIndex].driver];
            driver.Begin(pawn, job);

            if (!driver.TryMakeReservations(_ctx))
            {
                _ctx.Reservations.ReleaseAll(pawn);
                return false;
            }

            pawn.CurrentJob = job;
            pawn.Driver = driver;
            pawn.JobStartTick = tick;
            pawn.JobStartsInWindow++;
            JobsStarted++;
            return true;
        }

        /// <summary>
        /// End a job, for any reason at all. Every exit runs through here, which is the only way
        /// "released whenever the job ends" can be true rather than aspirational.
        /// </summary>
        public void EndJob(Pawn pawn, JobStatus status)
        {
            if (pawn.CurrentJob == null)
            {
                _ctx.Reservations.ReleaseAll(pawn);
                return;
            }

            if (status == JobStatus.Failed) _failed[pawn.CurrentJob.DefIndex]++;
            else _completed[pawn.CurrentJob.DefIndex]++;

            pawn.Driver?.Cleanup(_ctx, status);
            _ctx.Reservations.ReleaseAll(pawn);
            pawn.CurrentJob = null;
            pawn.Driver = null;
            pawn.ClearPath();
            pawn.Destination = -1;
        }
    }

    // =====================================================================================
    // The think tree
    // =====================================================================================

    /// <summary>A broken colonist wanders and does nothing useful. One behaviour; the taxonomy is later.</summary>
    public sealed class MentalStateThinkNode : ThinkNode
    {
        public override string Name => "MentalState";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (!pawn.IsBroken) return false;
            return WanderTarget.Fill(pawn, ctx, job);
        }
    }

    /// <summary>
    /// Self-care: eat when hungry, sleep when tired. Above work in the tree, so a starving
    /// colonist does not haul crates until it collapses.
    /// </summary>
    public sealed class CriticalNeedsThinkNode : ThinkNode
    {
        public override string Name => "CriticalNeeds";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (pawn.Asleep || pawn.IsBroken) return false;

            if (pawn.Needs[NeedIndex.Food] < ctx.Content.Needs[NeedIndex.Food].seekThreshold &&
                TryEat(pawn, ctx, job))
                return true;

            return pawn.Needs[NeedIndex.Rest] < ctx.Content.Needs[NeedIndex.Rest].seekThreshold &&
                   TrySleep(pawn, ctx, job);
        }

        static bool TryEat(Pawn pawn, PawnContext ctx, Job job)
        {
            var items = ctx.Items.Items;
            int best = -1;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Despawned || item.Cell < 0 || item.Forbidden) continue;
                if (ctx.Content.Items[item.DefIndex].nutrition <= 0) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Item, item.Id.Value);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;
                if (!ctx.Reachable(pawn, item.Cell)) continue;

                int distance = ctx.Distance(pawn.Cell, item.Cell);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }

            if (best < 0) return false;

            job.Reset(JobIndex.Eat);
            job.TargetItem = items[best].Id;
            job.TargetCell = items[best].Cell;
            return true;
        }

        static bool TrySleep(Pawn pawn, PawnContext ctx, Job job)
        {
            var beds = ctx.Items.Beds;
            int bestBed = -1;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < beds.Count; i++)
            {
                int cell = beds[i];
                long key = ReservationManager.Key(ReservationTargetKind.Cell, cell);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;
                if (!ctx.Reachable(pawn, cell)) continue;

                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestBed = cell;
            }

            job.Reset(JobIndex.Sleep);

            // No bed within reach is not a failure: a tired colonist lies down in the rubble and
            // remembers having done so.
            job.TargetCell = bestBed;
            return true;
        }
    }

    /// <summary>
    /// The work branch: one pass per player priority, 1 (highest) to 4, over the flat pre-sorted
    /// giver list. A work type at priority 1 is exhausted before anything at priority 2 is
    /// touched, and distance only breaks ties inside one giver — which is legible, and is what
    /// the player's numbers are for.
    /// </summary>
    public sealed class WorkThinkNode : ThinkNode
    {
        WorkGiver[] _givers = System.Array.Empty<WorkGiver>();

        internal void Bind(WorkGiver[] givers) => _givers = givers;

        public override string Name => "Work";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (!pawn.WillWork()) return false;

            for (int priority = 1; priority <= 4; priority++)
            {
                for (int i = 0; i < _givers.Length; i++)
                {
                    var giver = _givers[i];
                    if (pawn.WorkPriority(giver.WorkType) != priority) continue;
                    if (giver.TryGiveJob(pawn, ctx, job)) return true;
                }
            }

            return false;
        }
    }

    /// <summary>Nothing to do: wander a little, or stand still when there is nowhere to go.</summary>
    public sealed class IdleThinkNode : ThinkNode
    {
        public override string Name => "Idle";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (pawn.Asleep) return false;
            if (WanderTarget.Fill(pawn, ctx, job)) return true;

            job.Reset(JobIndex.Wait);
            return true;
        }
    }

    /// <summary>Picking somewhere nearby to drift to. Shared by idling and by the break.</summary>
    static class WanderTarget
    {
        public static bool Fill(Pawn pawn, PawnContext ctx, Job job)
        {
            var rng = DeterministicRandom.ForTick(
                ctx.Seed, ctx.CurrentTick, PawnPurpose.Wander ^ (uint)pawn.Id.Value);

            GridSize size = ctx.Size;
            CellRef from = size.FromIndex(pawn.Cell);
            int radius = ctx.Content.Break.wanderRadius;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                int x = from.X + rng.NextInt(-radius, radius + 1);
                int z = from.Z + rng.NextInt(-radius, radius + 1);
                if (!size.Contains(x, z, from.Y)) continue;

                int cell = size.Index(x, z, from.Y);
                if (cell == pawn.Cell) continue;
                if (!ctx.Reachable(pawn, cell)) continue;

                job.Reset(JobIndex.Wander);
                job.TargetCell = cell;
                return true;
            }

            return false;
        }
    }

    // =====================================================================================
    // Work givers
    // =====================================================================================

    /// <summary>
    /// Take a loose thing to the best stockpile that will have it.
    ///
    /// Destination choice is, in order: the filter accepts the item, the cell has space, highest
    /// priority, then nearest. Priority orders the <em>destination</em>, never the haul queue,
    /// which is what makes re-stowing into a better zone fall out of the same rule.
    ///
    /// Every candidate is tested for reachability before anything is pathed. That test is two
    /// array reads. It has to be, because this scan asks it for every loose thing on the map.
    /// </summary>
    public sealed class HaulWorkGiver : WorkGiver
    {
        public override string Name => "Haul";

        public override int WorkType => WorkTypeIndex.Haul;

        /// <summary>
        /// A hauler with its arms full is not a colonist: the mode is fixed for the whole job,
        /// and the scan tests reachability in the <em>same</em> mode the job will walk in. A scan
        /// that tested a laxer mode would hand out jobs that fail on their first step.
        /// </summary>
        const TraverseMode Mode = TraverseMode.Hauler;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            var loose = ctx.Items.LooseItems;
            var items = ctx.Items.Items;

            int bestItem = -1;
            int bestDest = -1;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < loose.Count; i++)
            {
                var item = items[loose[i]];
                if (item.Despawned || item.Cell < 0 || item.Forbidden) continue;
                if (!ctx.Content.Items[item.DefIndex].haulable) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Item, item.Id.Value);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, item.Cell);
                if (distance >= bestDistance) continue;
                if (!ctx.Reachable(pawn, item.Cell, Mode)) continue;

                int dest = BestStorageCell(pawn, ctx, item);
                if (dest < 0) continue;

                bestDistance = distance;
                bestItem = loose[i];
                bestDest = dest;
            }

            if (bestItem < 0) return false;

            job.Reset(JobIndex.Haul);
            job.TargetItem = items[bestItem].Id;
            job.TargetCell = items[bestItem].Cell;
            job.DestCell = bestDest;
            job.Mode = Mode;
            return true;
        }

        static int BestStorageCell(Pawn pawn, PawnContext ctx, ColonyItem item)
        {
            var piles = ctx.Items.Stockpiles;
            int bestCell = -1;
            int bestPriority = int.MinValue;
            int bestDistance = int.MaxValue;

            for (int s = 0; s < piles.Count; s++)
            {
                var pile = piles[s];
                if (!pile.Accepts(item.DefIndex)) continue;
                if (pile.Priority < bestPriority) continue;

                bool better = pile.Priority > bestPriority;
                for (int c = 0; c < pile.Cells.Length; c++)
                {
                    int cell = pile.Cells[c];
                    if (cell == item.Cell) continue;
                    if (!ctx.Items.CellHasSpace(cell)) continue;

                    long key = ReservationManager.Key(ReservationTargetKind.Cell, cell);
                    if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;
                    if (!ctx.Nav.Grid.CanEnter(cell, Mode)) continue;
                    if (!ctx.Nav.Reachable(item.Cell, cell, Mode)) continue;

                    int distance = ctx.Distance(item.Cell, cell);
                    if (!better && distance >= bestDistance) continue;

                    better = false;
                    bestPriority = pile.Priority;
                    bestDistance = distance;
                    bestCell = cell;
                }
            }

            return bestCell;
        }
    }
}
