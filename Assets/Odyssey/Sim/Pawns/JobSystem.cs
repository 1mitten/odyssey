#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Storage;

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
            GetOutOfTheWrongBed(world.CurrentTick);
            var pawns = _ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++) TickPawn(pawns[i], world.CurrentTick);
        }

        /// <summary>
        /// A colonist asleep somewhere that is no longer hers gets up now, rather than in the
        /// morning.
        ///
        /// <para><b>The owner's ask, 2026-09-20:</b> <i>"when I assigned a bed to a colonist and
        /// they are asleep - I expect them to get up immediately and get into the bed they have
        /// been assigned to"</i>. Before this the assignment landed on the record and nothing read
        /// it again until the next time she looked for somewhere to sleep, so a bed given to a
        /// sleeping colonist did nothing anybody could see until the following night.</para>
        ///
        /// <para><b>Three cases, and the middle one is the whole reason this is a sweep rather
        /// than a note naming who changed.</b> For each colonist on a sleep job:</para>
        /// <list type="bullet">
        /// <item>she is in <i>her own</i> bed — left alone, whatever changed. This is not a
        /// nicety: a sleeper claims an unowned bed the moment she arrives in it
        /// (<c>ConstructionGrid.TryClaimForSleeper</c>), and that claim raises the same flag a
        /// player's assignment does, so waking on the assignment alone would get her up, send her
        /// to walk to the bed she is already in, and do it again for ever.</item>
        /// <item>she has a bed of her own <i>somewhere else</i> — up she gets, and the tree sends
        /// her to it, because an own bed wins outright in <c>TrySleep</c>. This covers the
        /// colonist asleep on the ground as well as the one in a borrowed bed.</item>
        /// <item>she has no bed and is lying in one that now belongs to somebody — up, because it
        /// is not hers to be in. A colony short of beds keeps them unowned and shared, so this is
        /// very often the colonist the player just took a bed away from, and she is in no list of
        /// owners because she never was one.</item>
        /// </list>
        ///
        /// <para>Anything else — asleep in an unowned bed, or on the ground with no bed to go to —
        /// is left where it is. The job ends as a failure, which is what releases the bed she was
        /// holding, so she is walking before the player's hand has left the mouse.</para>
        ///
        /// <para><b>And an interrupted sleep resumes; it is not handed to the tree.</b> The first
        /// version did hand it over, and the owner's second play day found what that meant
        /// (<i>"when I assigned someone else to a bed - everyone just started going back to
        /// work"</i>): the tree sleeps below the <c>seekThreshold</c> of 280 and wakes at 950, so a
        /// colonist got up at 600 in the middle of the night was, by the tree's lights, not tired,
        /// and went to work. Every test of the rule had assigned the bed at a rest of 40 and never
        /// met the gap. So the woken colonist goes straight back through <c>TrySleep</c> — her own
        /// bed if she has one, the nearest free one if not, the ground if there is none — and
        /// only if that cannot start does the tree get her on her own tick.</para>
        ///
        /// <para><b>Two passes, not one.</b> Every affected sleep ends before anybody chooses
        /// again, because the bed the player just gave B is the bed A is still lying in: choose
        /// in the same pass and whether B gets her own bed or the nearest spare depends on which
        /// of the two the colony list happens to hold first.</para>
        ///
        /// <para>Every sleeper is examined rather than a named few, once, on the same tick the
        /// assignment arrived — intents drain at step 1 and this runs at step 4. An assignment is
        /// a player's click; the sweep costs one pass over the colony and a bed lookup each, and
        /// it cannot be wrong about who was left out.</para>
        /// </summary>
        void GetOutOfTheWrongBed(int tick)
        {
            Construction.ConstructionGrid? sites = _ctx.Construction;
            if (sites == null || !sites.BedOwnershipChanged) return;
            sites.ClearBedOwnershipChanged();

            var pawns = _ctx.Pawns.All;
            _woken.Clear();
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.CurrentJob == null) continue;
                if (_ctx.Content.Jobs[pawn.CurrentJob.DefIndex].driver != JobIndex.Sleep) continue;

                int owner = sites.BedOwnerAt(pawn.CurrentJob.TargetCell);
                if (owner == pawn.Id.Value) continue;

                if (owner == 0 && !sites.PawnOwnsABed(pawn.Id.Value)) continue;

                EndJob(pawn, JobStatus.Failed);
                _woken.Add(pawn);
            }

            for (int i = 0; i < _woken.Count; i++) ResumeSleep(_woken[i], tick);
            _woken.Clear();
        }

        /// <summary>
        /// Put a colonist whose sleep was interrupted back to bed, wherever the sleep chooser now
        /// sends her. Not through the tree, which would ask whether she is tired enough to
        /// <i>start</i> sleeping; she was asleep, so the only question is where. If the job
        /// cannot start — its claim refused, which the chooser has already ruled out — she is
        /// between jobs and <see cref="TickPawn"/> consults the tree for her this same tick.
        /// </summary>
        void ResumeSleep(Pawn pawn, int tick)
        {
            var job = pawn.JobBuffer;
            job.Reset(JobIndex.Wait);
            if (CriticalNeedsThinkNode.TrySleep(pawn, _ctx, job)) StartJob(pawn, job, tick);
        }

        // Scratch for the sweep above: cleared before and after every use, so it is neither
        // state nor a second copy of who is asleep.
        readonly List<Pawn> _woken = new List<Pawn>();

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
                // A job expires at the next cell boundary, never mid-step (design 29 §3a; owner,
                // 2026-09-22: a hog "went past the tree, then suddenly appeared before it again").
                // Ending a job drops the step in progress, so the pawn is put back on the cell it
                // was leaving while its figure was already drawn most of the way into the next —
                // a snap of up to a cell. Waiting for progress to reach nought costs at most one
                // step and is the whole of the fix. It was animals only for an afternoon, with
                // the colonists' snap at the end of a mental-break wander recorded as a gap; the
                // owner asked for the gap closed the same day, and every golden re-baked for it.
                bool expired = def.expiryTicks > 0 && tick - pawn.JobStartTick >= def.expiryTicks;
                if (expired && pawn.MoveProgress == 0)
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

            // An animal consults the animal tree (design 29 §3), never the colonist's: a node
            // that returned false for a person would be a node every colonist evaluated on
            // every think, and the animal's whole mind is one node anyway.
            ThinkNode[] tree = pawn.IsPerson ? _tree : AnimalTree;
            var job = pawn.JobBuffer;
            for (int i = 0; i < tree.Length; i++)
            {
                job.Reset(JobIndex.Wait);
                if (!tree[i].TryGiveJob(pawn, _ctx, job)) continue;
                if (StartJob(pawn, job, tick)) return;
            }
        }

        /// <summary>The whole of an animal's mind. Shared: the node holds no state.</summary>
        static readonly ThinkNode[] AnimalTree = { new AnimalIdleThinkNode() };

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

            int bestCell = -1;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Despawned || item.Forbidden) continue;
                if (ctx.Content.Items[item.DefIndex].nutrition <= 0) continue;

                // **A meal in a shelf is a meal.** This line read `item.Cell < 0` until shelves
                // existed, and that was right while "no cell" meant "in somebody's hands". A
                // colonist who cannot see into a store starves beside a full pantry, which is why
                // this scan is not optional once a shelf accepts food.
                int at = ctx.WhereIs(item);
                if (at < 0) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Item, item.Id.Value);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;
                if (!ctx.Reachable(pawn, at)) continue;

                int distance = ctx.Distance(pawn.Cell, at);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
                bestCell = at;
            }

            if (best < 0) return false;

            job.Reset(JobIndex.Eat);
            job.TargetItem = items[best].Id;
            job.TargetCell = bestCell;
            return true;
        }

        /// <summary>
        /// Choose where to sleep and write it into <paramref name="job"/>. Public because the job
        /// system resumes an interrupted sleep through it directly, past the tiredness gate in
        /// <see cref="TryGiveJob"/>.
        /// </summary>
        public static bool TrySleep(Pawn pawn, PawnContext ctx, Job job)
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

    /// <summary>
    /// An animal between jobs (design 29 §3): one roll on its own stream, and either a leg — a
    /// reachable cell within the species' radius, under the species' traverse mode — or a rest
    /// for a jittered span between the species' two bounds. Roughly two thinks in five are legs.
    /// Scales with the animals that are between jobs on a tick and with nothing else; a resting
    /// animal costs one integer increment a tick.
    ///
    /// <para>Not a branch of the colonist's tree, on purpose; see <c>JobSystem.Think</c>.</para>
    /// </summary>
    public sealed class AnimalIdleThinkNode : ThinkNode
    {
        public override string Name => "AnimalIdle";

        /// <summary>Legs per hundred thinks. INVENTED; a playtest number.</summary>
        public const int LegPerCent = 40;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            SpeciesDef species = pawn.Species;
            var rng = DeterministicRandom.ForTick(
                ctx.Seed, ctx.CurrentTick, PawnPurpose.AnimalMind ^ (uint)pawn.Id.Value);

            if (rng.NextInt(100) < LegPerCent &&
                WanderTarget.Fill(pawn, ctx, job, species.wanderRadius, species.traverseMode, avoidSlopes: true))
                return true;

            int span = species.restTicksMax > species.restTicksMin
                ? species.restTicksMin + rng.NextInt(species.restTicksMax - species.restTicksMin + 1)
                : species.restTicksMin;
            job.Reset(JobIndex.Wait);
            job.Mode = species.traverseMode;
            job.WorkTicks = span > 0 ? span : 1;
            return true;
        }
    }

    /// <summary>Picking somewhere nearby to drift to. Shared by idling, by the break and by an animal.</summary>
    static class WanderTarget
    {
        public static bool Fill(Pawn pawn, PawnContext ctx, Job job) =>
            Fill(pawn, ctx, job, ctx.Content.Break.wanderRadius, TraverseMode.Colonist, avoidSlopes: false);

        /// <summary>
        /// The same pick under a given radius and traverse mode (design 29 §3, §4). The mode goes
        /// on the job, where the hauler's goes, so the walk is planned and validated under the
        /// species' own rules — a hog at the foot of a ladder is refused the link by the mask the
        /// graph already carries.
        /// </summary>
        /// <para><paramref name="avoidSlopes"/> refuses the foot cell of a terrace step as a
        /// destination (owner, 2026-09-22: an animal must not rest on one — it is drawn as a ramp,
        /// and a body resting in it is drawn on the ramp and then snaps to the floor when it sets
        /// off). Walking <i>through</i> one is unchanged. Animals only, for now: a colonist's
        /// wander is the mental break's, and moving it would move every golden.</para>
        public static bool Fill(Pawn pawn, PawnContext ctx, Job job, int radius, TraverseMode mode, bool avoidSlopes)
        {
            var rng = DeterministicRandom.ForTick(
                ctx.Seed, ctx.CurrentTick, PawnPurpose.Wander ^ (uint)pawn.Id.Value);

            GridSize size = ctx.Size;
            CellRef from = size.FromIndex(pawn.Cell);

            for (int attempt = 0; attempt < 8; attempt++)
            {
                int x = from.X + rng.NextInt(-radius, radius + 1);
                int z = from.Z + rng.NextInt(-radius, radius + 1);
                if (!size.Contains(x, z, from.Y)) continue;

                int cell = size.Index(x, z, from.Y);
                if (cell == pawn.Cell) continue;
                if (avoidSlopes && ctx.Nav.Grid.CostClass[cell] == Worldgen.Natural.NaturalContent.CostClassSlope) continue;
                if (!ctx.Reachable(pawn, cell, mode)) continue;

                job.Reset(JobIndex.Wander);
                job.TargetCell = cell;
                job.Mode = mode;
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
        /// How far the search for somewhere to set an unwanted load down may reach: twelve cells,
        /// which is what it takes to get out of the middle of a field - or of a warehouse - that
        /// a player would actually paint. A three-cell ring found only dirt inside a field six
        /// tiles across, and that measurement is what set it. Where even twelve finds nothing the
        /// thing stays where it is: a board packed that solid is one nothing in the game can
        /// produce, and the alternative is a hauler walking the map for a rock.
        ///
        /// <para>One constant for both clearance cases, because the moment they disagree one of
        /// them is the wrong answer to the same question.</para>
        /// </summary>
        const int ClearanceRadius = 12;
        /// <summary>
        /// A hauler with its arms full is not a colonist: the mode is fixed for the whole job,
        /// and the scan tests reachability in the <em>same</em> mode the job will walk in. A scan
        /// that tested a laxer mode would hand out jobs that fail on their first step.
        /// </summary>
        const TraverseMode Mode = TraverseMode.Hauler;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job) =>
            TryHaul(pawn, ctx, job, restow: false) ||
            TryHaul(pawn, ctx, job, restow: true);

        /// <summary>
        /// One pass of the scan. The <em>first</em> walks the loose things and the things a store
        /// refuses; the second walks the things a store is content to hold, which is the tidying.
        ///
        /// <para><b>The two listers are walked as one because a refused thing belongs in neither
        /// on its own.</b> <see cref="ColonyItems"/> buckets by whether a cell is inside a zone,
        /// which is the question it can answer cheaply, so a thing whose store refuses it is
        /// bucketed stored - while by <see cref="StoredPriority"/>'s own words it "is not stored
        /// at all, only in the way". Leaving it in the tidying pass is what left a rock sitting
        /// in a meals-only stockpile for ever (owner, 2026-09-21), because that pass runs only
        /// when nothing loose is waiting, and a busy colony always has something loose.
        /// Re-bucketing on every filter edit was the other answer and is the worse one: it would
        /// make <c>ColonyItems</c>' buckets depend on the filter table, so ticking one commodity
        /// would have to walk a zone's items and a save would have to agree about which lister
        /// each thing was in.</para>
        /// </summary>
        static bool TryHaul(Pawn pawn, PawnContext ctx, Job job, bool restow)
        {
            var items = ctx.Items.Items;
            IReadOnlyList<int> loose = ctx.Items.LooseItems;
            IReadOnlyList<int> stored = ctx.Items.StoredItems;

            // The third lister: things inside a built store. They are walked with the stored ones
            // and split by the same question, because a shelf that refuses a thing and a stockpile
            // cell that refuses it are the same sentence — and a store being *emptied* refuses
            // everything in it, which is how "a shelf ordered taken apart gives up its contents"
            // falls out of a rule that was already here rather than out of a pass of its own.
            IReadOnlyList<int> contained = ctx.Items.ContainedItems;

            int held = stored.Count + contained.Count;
            int count = restow ? held : loose.Count + held;

            int bestItem = -1;
            int bestDest = -1;
            int bestFrom = -1;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < count; i++)
            {
                int inHeld = restow ? i : i - loose.Count;
                bool isStored = inHeld >= 0;
                int index = !isStored ? loose[i]
                    : inHeld < stored.Count ? stored[inHeld]
                    : contained[inHeld - stored.Count];

                var item = items[index];
                if (item.Despawned || item.Forbidden) continue;

                // Where it is, which is now three questions and not two. A thing in a pair of
                // hands answers -1 and is skipped exactly as `Cell < 0` used to skip it; a thing
                // in a store answers the store's cell, which is where a colonist walks to reach
                // it.
                int at = ctx.WhereIs(item);
                if (at < 0) continue;
                if (!ctx.Content.Items[item.DefIndex].haulable) continue;

                // Which pass this stored thing belongs to. A loose thing is never refused -
                // there is no filter over bare ground - so the question is asked only of the
                // stored lister.
                if (isStored && Refused(ctx, item) == restow) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Item, item.Id.Value);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, at);

                // A thing standing on tilled soil is in the way of the field (owner,
                // 2026-09-19: "all items should be removed by colonists first from the dirt
                // before sowing to an appropriate place"): the sowing scan will not touch its
                // cell while it lies there, so it outranks every ordinary pile however near -
                // the field cannot wait on a nearer rock.
                if (OnSoilSomebodyWantsToSow(ctx, item)) distance -= ClearanceBias;

                if (distance >= bestDistance) continue;
                if (!ctx.Reachable(pawn, at, Mode)) continue;

                if (!TryBestStorageSlot(pawn, ctx, item, at,
                        restow ? CurrentPriority(ctx, item) : int.MinValue, out StorageSlot slot))
                    slot = StorageSlot.None;
                int dest = slot.Stand;

                // A thing that is in the way and that no store will take still has to come off
                // the cell it is spoiling. Three cases, one rule:
                //
                //   - on tilled soil, where the sowing of the cell is waiting on it and a full
                //     store is not a reason for a field to stand idle (owner, 2026-09-20: "the
                //     colonists didn't remove the stone from the dirt tile and didn't bother
                //     sowing and nothing happened");
                //   - inside a store that refuses it, where it is holding a cell the player set
                //     aside for something else and no amount of waiting will make the store want
                //     it (owner, 2026-09-21: "I expect the colonists to ensure that all those
                //     tiles are occupied by meals or nothing, not leave rocks in there");
                //   - inside a store that is being taken apart, which refuses everything for the
                //     same reason: waiting will not help, because the store is going.
                //
                // **The third case is what stops an ordered shelf deadlocking.** Its contents rank
                // below every real store so they want to leave, but with nowhere better to go the
                // scan finds no destination and they stay — while the deconstruct gate refuses to
                // remove the shelf until they have gone. Nothing moves and nothing says why.
                //
                // It goes to the nearest cell that neither wants to be empty nor refuses it, and
                // becomes an ordinary pile there: a stockpile's business again the moment one
                // has room for it. Hauled out rather than spilled, deliberately — the load ends on
                // the same sort of cell either way, but a colonist carries it, which is the
                // difference between a colony emptying a shelf and a shelf emptying itself.
                if (dest < 0 && !restow && InTheWay(ctx, item))
                    dest = ctx.Items.NearestCellWithSpace(
                        ctx.Cells, at, item.DefIndex, item.Stack, maxRadius: ClearanceRadius,
                        accept: ctx.OpenGroundFor(item.DefIndex));
                if (dest < 0) continue;

                bestDistance = distance;
                bestItem = index;
                bestDest = dest;
                bestFrom = at;
            }

            if (bestItem < 0) return false;

            job.Reset(JobIndex.Haul);
            job.TargetItem = items[bestItem].Id;
            // Both ends are named by a **cell**, and a store at either end is named by the cell it
            // stands in. A cell holds at most one edifice, so that is unambiguous — and it costs no
            // new field on the job record, which is written inside the pawns section and read
            // sequentially, so two ints there would be a save-format bump for nothing.
            job.TargetCell = bestFrom;
            job.DestCell = bestDest;
            job.Mode = Mode;
            return true;
        }

        /// <summary>
        /// Is this thing lying in a store whose filter refuses it? Such a thing is not stored at
        /// all, only in the way: it is scanned in the first pass beside the loose things, and it
        /// is carried out to open ground when no store will have it.
        ///
        /// <para>A cell in no zone answers false, which is why the loose lister never has to be
        /// asked.</para>
        /// </summary>
        static bool Refused(PawnContext ctx, ColonyItem item)
        {
            // A built store, which is also the only case that can answer "yes" about a thing with
            // no cell at all. **A store being taken apart refuses everything in it**, which is not
            // a special case bolted on: it is the same sentence as a filter refusing a thing —
            // this store will not have it and waiting will not change that — and putting it here
            // rather than in a pass of its own is what gives an emptying shelf the urgent pass and
            // the clearance fallback for free.
            if (item.ContainerId != 0)
            {
                Storage.StorageUnits? units = ctx.StorageUnits;
                Storage.StorageUnit? holding = units?.ByContainerId(item.ContainerId);
                if (holding == null) return false;

                return units!.IsEmptying(holding) || !units.Accepts(holding, item.DefIndex);
            }

            var zones = ctx.Storage;
            return zones != null
                && zones.IsStorage(item.Cell)
                && !zones.Accepts(item.Cell, item.DefIndex);
        }

        /// <summary>
        /// Is this thing spoiling the cell it lies in - standing on soil somebody wants to sow,
        /// or squatting in a store that will not have it? The question the clearance fallback
        /// asks, and the one place the two cases are named together.
        /// </summary>
        static bool InTheWay(PawnContext ctx, ColonyItem item) =>
            OnSoilSomebodyWantsToSow(ctx, item) || Refused(ctx, item);

        /// <summary>
        /// Is this thing lying on ground somebody wants to plant?
        ///
        /// <para><b>Its own method because two callers ask it</b> — the clearance bias and
        /// <see cref="InTheWay"/> — and because it is exactly the question a third home makes easy
        /// to get wrong. It asks <c>item.Cell</c> and not "where can a colonist reach it": a thing
        /// on a shelf is not lying on anything, and a shelf built on a zone cell would otherwise
        /// give everything standing on it the bias meant for a rock in the dirt.</para>
        /// </summary>
        static bool OnSoilSomebodyWantsToSow(PawnContext ctx, ColonyItem item) =>
            item.Cell >= 0 && ctx.Growing != null && ctx.Growing.ZonePlantAt(item.Cell) >= 0;

        /// <summary>
        /// The priority a stored thing already enjoys, which a re-stow has to beat: the implicit
        /// "unstored" rank below every real one that a-14 §1 infers, for anything no store is
        /// holding.
        ///
        /// <para>Only ever asked of a thing its own store accepts, because <see cref="Refused"/>
        /// takes the others out of the re-stow pass one loop above. The filter test is kept all
        /// the same, so that the function is a true answer to its own question rather than one
        /// that depends on its caller: it is a single lookup, and a re-stow that compared against
        /// a priority a refused thing does not actually have would shuttle it between piles for
        /// ever.</para>
        /// </summary>
        static int CurrentPriority(PawnContext ctx, ColonyItem item)
        {
            if (item.ContainerId != 0)
            {
                Storage.StorageUnit? unit = ctx.StorageUnits?.ByContainerId(item.ContainerId);
                if (unit == null) return int.MinValue;

                // An emptying store holds nothing at a rank worth keeping. **Not reached in
                // practice** — `Refused` says the same thing one loop up and sends these to the
                // first pass, where no priority is consulted — and kept for the reason this method's
                // own doc gives: it is a true answer to its own question rather than one that
                // depends on which caller asked. The day something re-stows out of an emptying
                // store by another route, this is already right.
                if (ctx.StorageUnits!.IsEmptying(unit)) return int.MinValue;

                return ctx.StorageUnits!.Accepts(unit, item.DefIndex)
                    ? ctx.StorageUnits!.PriorityOf(unit)
                    : int.MinValue;
            }

            var zones = ctx.Storage;
            if (zones == null) return int.MinValue;
            return zones.Accepts(item.Cell, item.DefIndex) ? zones.PriorityAt(item.Cell) : int.MinValue;
        }

        /// <summary>
        /// The cell the load should go to, or -1: filter, then space for the whole load, then
        /// the highest priority strictly above <paramref name="abovePriority"/>, then nearest.
        /// </summary>
        static bool TryBestStorageSlot(Pawn pawn, PawnContext ctx, ColonyItem item, int from,
            int abovePriority, out StorageSlot slot)
        {
            slot = StorageSlot.None;
            var zones = ctx.Storage;
            Storage.StorageUnits? units = ctx.StorageUnits;
            if (zones == null && units == null) return false;

            // **Bands, high to low, stopping at the first that yields.** Priority dominates
            // distance — nearest only breaks ties *inside* a band — so once a band has produced a
            // reachable cell, no lower band can win and the walk is over. That turns the scan from
            // "every cell of every zone, for every candidate item" into "the cells of the best
            // band", which is the difference between a warehouse being free to have and being
            // paid for on every think (docs/plans/storage.md §5f).
            //
            // Strictly above `abovePriority`, which is how a re-stow is kept from shuffling
            // between two piles at one priority: equal is not better (a-14 §3D).
            // `priority >= 0` as well as `> abovePriority`, and not for tidiness: the floor for a
            // loose thing is int.MinValue — the implicit rank of "not stored at all" — and a loop
            // that only tested the floor would count down two billion times before it stopped.
            for (int priority = StoragePriority.Count - 1; priority >= 0 && priority > abovePriority; priority--)
            {
                StorageSlot best = StorageSlot.None;
                int bestDistance = int.MaxValue;

                for (int zone = 0; zones != null && zone < zones.ZoneCount; zone++)
                {
                    StorageSettings settings = zones.SettingsOf(zone);
                    if (settings.Priority != priority) continue;
                    if (!settings.Accepts(item.DefIndex)) continue;

                    IReadOnlyList<int> cells = zones.CellsOf(zone);
                    for (int c = 0; c < cells.Count; c++)
                    {
                        int cell = cells[c];
                        if (cell == item.Cell) continue;
                        if (!ctx.Items.CellHasSpace(cell, item.DefIndex, item.Stack)) continue;

                        // The distance first, because it is two array reads and it is what lets a
                        // cell that cannot win skip the reservation, the nav flag and the region
                        // lookup behind it.
                        int distance = ctx.Distance(from, cell);
                        if (distance >= bestDistance) continue;

                        long key = ReservationManager.Key(ReservationTargetKind.Cell, cell);
                        if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;
                        if (!ctx.Nav.Grid.CanEnter(cell, Mode)) continue;
                        if (!ctx.Nav.Reachable(from, cell, Mode)) continue;

                        bestDistance = distance;
                        best = new StorageSlot(cell, 0, cell);
                    }
                }

                // The built stores, in the same band, competing on the same distance and answering
                // the same three questions in the same order. **Not a second walk and not a second
                // rule** — the destination has one owner, which is the lesson HopPriceHasOneOwner
                // records for the price of a hop.
                for (int u = 0; units != null && u < units.Units.Count; u++)
                {
                    Storage.StorageUnit unit = units.Units[u];
                    if (unit.Removed) continue;

                    // A store being emptied is never a destination, or a shelf ordered taken apart
                    // would re-stow into itself for ever.
                    if (units.IsEmptying(unit)) continue;
                    if (Storage.StorageUnits.ContainerIdOf(unit.Edifice) == item.ContainerId) continue;

                    StorageSettings settings = units.SettingsOf(unit);
                    if (settings.Priority != priority) continue;
                    if (!settings.Accepts(item.DefIndex)) continue;
                    if (!units.HasSpaceFor(unit, item.DefIndex, item.Stack)) continue;

                    int cell = units.CellOf(unit);
                    int distance = ctx.Distance(from, cell);
                    if (distance >= bestDistance) continue;

                    // Keyed on the edifice, not on the cell: a shelf is passable, so its cell is
                    // one a colonist stands *in* rather than one a load is put down on, and a cell
                    // claim there would stop a second colonist walking through the room.
                    long key = ReservationManager.Key(ReservationTargetKind.Container, unit.Edifice);
                    if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;
                    if (!ctx.Nav.Grid.CanEnter(cell, Mode)) continue;
                    if (!ctx.Nav.Reachable(from, cell, Mode)) continue;

                    bestDistance = distance;
                    best = new StorageSlot(-1, Storage.StorageUnits.ContainerIdOf(unit.Edifice), cell);
                }

                if (best.Stand >= 0)
                {
                    slot = best;
                    return true;
                }
            }

            return false;
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
