#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
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
        WorkGiver[] _givers;

        /// <summary>
        /// The shipped pipeline: the standard tree, and every work giver the simulation assembly
        /// contains. There is no list of givers here any more — see <see cref="WorkGiverRegistry"/>
        /// for why, and <see cref="SimWorldBuilder.AddWorkGiver"/> for how a giver from outside the
        /// assembly joins in.
        /// </summary>
        public JobSystem(PawnContext ctx) : this(ctx, DefaultTree(), WorkGiverRegistry.Discover()) { }

        public JobSystem(PawnContext ctx, ThinkNode[] tree, WorkGiver[] givers)
        {
            _ctx = ctx;
            _givers = SortGivers(ctx, givers);
            _tree = tree;
            _completed = new int[ctx.Content.Jobs.Length];
            _failed = new int[ctx.Content.Jobs.Length];
            Bind();
        }

        /// <summary>
        /// Take on givers the composition registered, which are the ones discovery cannot see:
        /// a giver living outside the simulation assembly. Order of the calls is irrelevant —
        /// the whole list is re-sorted, so a giver added last still scans wherever its work type
        /// says it does.
        ///
        /// <para>A duplicate type is refused rather than merged. Two instances of one giver would
        /// scan the same work twice and reserve against themselves, and the shape of the fault —
        /// a colonist that starts a job and immediately fails it — points nowhere near the cause.</para>
        /// </summary>
        public void AddGivers(IReadOnlyList<WorkGiver> givers)
        {
            if (givers == null || givers.Count == 0) return;

            var combined = new List<WorkGiver>(_givers);
            for (int i = 0; i < givers.Count; i++)
            {
                var giver = givers[i];
                for (int j = 0; j < combined.Count; j++)
                    if (combined[j].GetType() == giver.GetType())
                        throw new System.InvalidOperationException(
                            $"{giver.GetType().FullName} is already in the work scan. A giver inside " +
                            "the simulation assembly is discovered automatically and must not also " +
                            "be registered by hand.");
                combined.Add(giver);
            }

            _givers = SortGivers(_ctx, combined.ToArray());
            Bind();
        }

        void Bind()
        {
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
        /// Sorted by emergency flag, then work-type order, then the giver's own intra-type number,
        /// then the name. The player's 1..4 priority is per pawn and cannot be baked in here, so
        /// the scan makes one pass per priority level over this list instead.
        ///
        /// <para>This is the whole of the scan order, and it is the reason givers may introduce
        /// themselves: the order is a property of the Defs and of each giver's own declared
        /// numbers, never of the order anything was discovered or registered in. The name is the
        /// last tie-break so the comparison is total — two givers can never compare equal and
        /// leave the sort to decide between them.</para>
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

        // ---- forced orders --------------------------------------------------------------------
        //
        // The player overruling the scan for one colonist. A forced order is emphatically *not* a
        // new kind of job: it is the same def, the same driver, the same toils and the same
        // reservations, with the scan bypassed and the job pushed onto a named pawn rather than
        // offered to whoever the tree walks to next. `Job.PlayerForced` has been saved and hashed
        // since the job record was written and this is the first thing that ever sets it.

        /// <summary>
        /// Could this colonist be given this job on this target right now? Nothing is claimed and
        /// nothing moves — see <see cref="BuildWorkGiver.CanBuild"/>, which is where the build
        /// answer lives and which the work scan asks as well.
        ///
        /// <para>The switch is the list of jobs a player may force, and it is one long. A default
        /// of "no" is deliberate: a job that has not been thought about as a forced order is one
        /// nobody has decided what a forced version of it means, and silently allowing it would be
        /// deciding by omission.</para>
        /// </summary>
        /// <param name="jobDefIndex">A <see cref="JobIndex"/> value.</param>
        /// <param name="target">The cell the job is about — a site, for a build.</param>
        /// <param name="stand">Where the colonist would stand to do it, or -1.</param>
        public static bool CanForce(Pawn pawn, PawnContext ctx, int jobDefIndex, int target, out int stand)
        {
            stand = -1;
            if (pawn == null || ctx == null) return false;

            switch (jobDefIndex)
            {
                case JobIndex.Build: return BuildWorkGiver.CanBuild(pawn, ctx, target, out stand);
                default: return false;
            }
        }

        /// <summary>The same question where the stance is not wanted: what a menu asks.</summary>
        public static bool CanForce(Pawn pawn, PawnContext ctx, int jobDefIndex, int target) =>
            CanForce(pawn, ctx, jobDefIndex, target, out _);

        /// <summary>
        /// <c>ForceJob(cell, A = job, B = pawn)</c>: this colonist, this job, this target, now.
        ///
        /// <para>Illegal is a no-op with a reason and never a half-done order — the legality query
        /// above runs first and claims nothing, so a refusal cannot leave a reservation behind,
        /// and the colonist's current job is not interrupted until the new one is known to be
        /// takeable. The one remaining way to fail after that is
        /// <see cref="StartJob"/>'s own all-or-nothing claim, which releases what it took.</para>
        ///
        /// <para>The colonist's job ends as a <b>failure</b>, which is how a mental break takes a
        /// job away a few lines above: failure is the ending that releases what was held, and
        /// there is exactly one release path in this class on purpose.</para>
        /// </summary>
        public IntentRejection HandleForceJob(Intent intent)
        {
            CellRef cell = intent.Cell;
            if (!_ctx.Size.Contains(cell.X, cell.Z, cell.Y)) return IntentRejection.OutOfBounds;

            var sites = _ctx.Construction;
            if (sites == null) return IntentRejection.NotPermitted;

            Pawn? pawn = _ctx.Pawns.Get(new PawnId(intent.B));
            if (pawn == null) return IntentRejection.NotPermitted;

            // The same lift a build order and a cancellation get: a click names the ground, an
            // order names the cell standing on it.
            int site = sites.SiteAt(cell);
            if (site < 0) return IntentRejection.NotPermitted;

            if (!CanForce(pawn, _ctx, intent.A, site, out int stand)) return IntentRejection.NotPermitted;

            EndJob(pawn, JobStatus.Failed);

            // The pawn's own buffer, which is what the think tree fills and therefore what
            // `CurrentJob` always points at. Reset after the old job has ended, never before: the
            // ending reads the def it is counting.
            Job job = pawn.JobBuffer;
            BuildWorkGiver.Fill(job, site, stand);
            job.PlayerForced = true;

            // Intents drain at the top of the tick, before the pawn phase syncs the context, so
            // the context's tick is still the last one's while the world's is this one's. The
            // fallback is for a command submitted before the world has ever ticked, when the two
            // are the same number anyway.
            int tick = _ctx.World?.CurrentTick ?? _ctx.CurrentTick;
            return StartJob(pawn, job, tick) ? IntentRejection.None : IntentRejection.NotPermitted;
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
            // Zero rest is a collapse, not a journey (WS3, design 17 §4c): a body that has run
            // out goes down where it stands, however comfortable the bed it was walking towards.
            // Tiredness below the threshold but above zero is still a walk — the control that
            // keeps this from becoming every colonist sleeping in the mud.
            if (pawn.Needs[NeedIndex.Rest] <= 0)
            {
                job.Reset(JobIndex.Sleep);
                job.TargetCell = -1;
                return true;
            }

            var beds = ctx.Items.Beds;
            int own = -1;
            int bestBed = -1;
            int bestDistance = int.MaxValue;
            int me = pawn.Id.Value;

            for (int i = 0; i < beds.Count; i++)
            {
                int cell = beds[i];

                // A bed that is somebody's is theirs and nobody else checks in: no colonist
                // sleeps in another's bed, which is the whole of what ownership is (design 20
                // §7). The scenario's own spots answer 0 — nobody's, as they always were.
                int owner = ctx.Construction != null ? ctx.Construction.BedOwnerAt(cell) : 0;
                if (owner != 0 && owner != me) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Cell, cell);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;
                if (!ctx.Reachable(pawn, cell)) continue;

                // My own bed wins outright, however far away it is: that is what having one
                // means. It has to be reachable and free like any other — an own bed on the far
                // side of a collapse does not keep a colonist awake.
                if (owner == me)
                {
                    own = cell;
                    break;
                }

                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestBed = cell;
            }

            job.Reset(JobIndex.Sleep);

            // No bed within reach is not a failure: a tired colonist lies down in the rubble and
            // remembers having done so.
            job.TargetCell = own >= 0 ? own : bestBed;
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
    /// Take a loose thing to the best stockpile that will have it; failing that, move a stored
    /// thing to a better pile than the one it is in.
    ///
    /// Destination choice is, in order: the filter accepts the item, the cell has space for the
    /// whole load, highest priority, then nearest (a-14 §3). Priority orders the
    /// <em>destination</em>, never the haul queue, which is what makes re-stowing into a better
    /// zone fall out of the same rule: a stored thing is a haul candidate whose destination must
    /// beat the priority of where it lies. Equal priority is not better — two piles at one
    /// priority are one warehouse in two places, and shuttling between them is the
    /// up-and-down-the-stairs failure the research warns of.
    ///
    /// Loose things are scanned first and re-stowing only when there is nothing loose, because
    /// tidying is the lowest job there is. Every candidate is tested for reachability before
    /// anything is pathed. That test is two array reads. It has to be, because this scan asks
    /// it for every loose thing on the map.
    /// </summary>
    public sealed class HaulWorkGiver : WorkGiver
    {
        public override string Name => "Haul";

        public override int WorkType => WorkTypeIndex.Haul;

        /// <summary>
        /// How far away an ordinary pile may be and still lose to a thing standing on tilled
        /// soil: fifty cells, which on these boards is "anywhere" - the point is the ordering,
        /// not the radius.
        /// </summary>
        const int ClearanceBias = 50;
        /// <summary>
        /// A hauler with its arms full is not a colonist: the mode is fixed for the whole job,
        /// and the scan tests reachability in the <em>same</em> mode the job will walk in. A scan
        /// that tested a laxer mode would hand out jobs that fail on their first step.
        /// </summary>
        const TraverseMode Mode = TraverseMode.Hauler;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job) =>
            TryHaul(pawn, ctx, job, ctx.Items.LooseItems, restow: false) ||
            TryHaul(pawn, ctx, job, ctx.Items.StoredItems, restow: true);

        static bool TryHaul(Pawn pawn, PawnContext ctx, Job job, IReadOnlyList<int> lister, bool restow)
        {
            var items = ctx.Items.Items;

            int bestItem = -1;
            int bestDest = -1;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < lister.Count; i++)
            {
                var item = items[lister[i]];
                if (item.Despawned || item.Cell < 0 || item.Forbidden) continue;
                if (!ctx.Content.Items[item.DefIndex].haulable) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Item, item.Id.Value);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, item.Cell);

                // A thing standing on tilled soil is in the way of the field (owner,
                // 2026-09-19: "all items should be removed by colonists first from the dirt
                // before sowing to an appropriate place"): the sowing scan will not touch its
                // cell while it lies there, so it outranks every ordinary pile however near -
                // the field cannot wait on a nearer rock.
                if (ctx.Growing != null && ctx.Growing.ZonePlantAt(item.Cell) >= 0)
                    distance -= ClearanceBias;

                if (distance >= bestDistance) continue;
                if (!ctx.Reachable(pawn, item.Cell, Mode)) continue;

                int dest = BestStorageCell(pawn, ctx, item, restow ? StoredPriority(ctx, item) : int.MinValue);

                // A thing on tilled soil that no stockpile will take still has to come off the
                // dirt - the sowing of its cell is waiting on it, and a full store is not a
                // reason for a field to stand idle (owner, 2026-09-20: "the colonists didn't
                // remove the stone from the dirt tile and didn't bother sowing and nothing
                // happened"). It goes to the nearest free cell outside every zone, and becomes
                // an ordinary pile there: the stockpile's business again once it has room.
                if (dest < 0 && !restow && ctx.Growing != null &&
                    ctx.Growing.ZonePlantAt(item.Cell) >= 0)
                    dest = ctx.Items.NearestCellWithSpace(
                        ctx.Cells, item.Cell, item.DefIndex, item.Stack, maxRadius: 6,
                        accept: c => ctx.Growing!.ZonePlantAt(c) < 0);
                if (dest < 0) continue;

                bestDistance = distance;
                bestItem = lister[i];
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

        /// <summary>
        /// The priority a stored thing already enjoys, which a re-stow has to beat. A thing lying
        /// in a pile whose filter no longer accepts it is not stored at all, only in the way,
        /// and any pile that does accept it is better: the implicit "unstored" priority below
        /// every real one that a-14 §1 infers.
        /// </summary>
        static int StoredPriority(PawnContext ctx, ColonyItem item)
        {
            var pile = ctx.Items.StockpileAt(item.Cell);
            return pile != null && pile.Accepts(item.DefIndex) ? pile.Priority : int.MinValue;
        }

        /// <summary>
        /// The cell the load should go to, or -1: filter, then space for the whole load, then
        /// the highest priority strictly above <paramref name="abovePriority"/>, then nearest.
        /// </summary>
        static int BestStorageCell(Pawn pawn, PawnContext ctx, ColonyItem item, int abovePriority)
        {
            var piles = ctx.Items.Stockpiles;
            int bestCell = -1;
            int bestPriority = abovePriority;
            int bestDistance = int.MaxValue;

            for (int s = 0; s < piles.Count; s++)
            {
                var pile = piles[s];
                if (!pile.Accepts(item.DefIndex)) continue;
                if (pile.Priority < bestPriority) continue;
                // At the floor itself nothing has been found yet, and the floor is not a find.
                if (pile.Priority == abovePriority) continue;
                // At the floor itself nothing has been found yet, and the floor is not a find.

                bool better = pile.Priority > bestPriority;
                for (int c = 0; c < pile.Cells.Length; c++)
                {
                    int cell = pile.Cells[c];
                    if (cell == item.Cell) continue;
                    if (!ctx.Items.CellHasSpace(cell, item.DefIndex, item.Stack)) continue;

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

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Fell a tree the player has marked.
    ///
    /// The scan walks the designation grid's own list of designated cells, never the map, so
    /// it costs what the orders cost and not what the board costs. A tree that has already gone
    /// is skipped here and its order left standing; the driver that reaches it clears it.
    /// </summary>
    public sealed class FellWorkGiver : WorkGiver
    {
        public override string Name => "Fell";

        public override int WorkType => WorkTypeIndex.Cutting;

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
                if (designations.At(cell) != DesignationKind.Fell) continue;
                if (!designations.IsTree(cell)) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Cell, cell);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;

                // Work from beside the tree, never from inside it: a colonist drawn at the cell
                // centre stood in the trunk. The stand is the nearest walkable neighbour the
                // pawn can reach; a tree with none is left for a colonist who can.
                int stand = FellJobDriver.StandBeside(ctx, pawn, cell);
                if (stand < 0) continue;

                bestDistance = distance;
                best = cell;
                bestStand = stand;
            }

            if (best < 0) return false;

            job.Reset(JobIndex.Fell);
            job.TargetCell = bestStand;
            job.DestCell = best;
            return true;
        }
    }
}
