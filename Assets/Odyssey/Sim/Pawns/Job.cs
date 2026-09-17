#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>How a toil, and therefore a job, ended.</summary>
    public enum JobStatus : byte
    {
        Ongoing = 0,
        Succeeded = 1,

        /// <summary>A fail condition tripped. Reservations are released exactly as on success.</summary>
        Failed = 2,
    }

    /// <summary>
    /// One unit of intent. A job names a driver and carries its targets; the driver runs the
    /// toils. Jobs are mutable instances owned by the pawn, pooled per driver kind so that
    /// starting one allocates nothing.
    /// </summary>
    public class Job
    {
        /// <summary>A <see cref="JobIndex"/> value: an integer handle into the job table.</summary>
        public int DefIndex;

        /// <summary>The thing the job is about, or <see cref="ThingId.None"/>.</summary>
        public ThingId TargetItem;

        /// <summary>The cell the job walks to first, or -1.</summary>
        public int TargetCell = -1;

        /// <summary>Where the payload ends up, or -1.</summary>
        public int DestCell = -1;

        /// <summary>The thing in the pawn's hands, or -1.</summary>
        public int CarriedItem = -1;

        /// <summary>A player-forced job resists casual interruption.</summary>
        public bool PlayerForced;

        /// <summary>Fixed for the life of the job, so a path cannot be invalidated mid-walk.</summary>
        public TraverseMode Mode = TraverseMode.Colonist;

        /// <summary>Overrides the def's work duration when positive. Used by the stand-down.</summary>
        public int WorkTicks;

        public void Reset(int defIndex)
        {
            DefIndex = defIndex;
            TargetItem = ThingId.None;
            TargetCell = -1;
            DestCell = -1;
            CarriedItem = -1;
            PlayerForced = false;
            Mode = TraverseMode.Colonist;
            WorkTicks = 0;
        }

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(DefIndex);
            hash.Add(TargetItem.Value);
            hash.Add(TargetCell);
            hash.Add(DestCell);
            hash.Add(CarriedItem);
            hash.Add(PlayerForced);
            hash.Add((int)Mode);
            hash.Add(WorkTicks);
        }
    }

    /// <summary>
    /// Runs one job as an ordered sequence of toils.
    ///
    /// A toil here is an integer index plus a progress counter rather than a closure, for two
    /// reasons that both matter. Starting a job allocates nothing, because the driver is pooled
    /// on the pawn per job kind; and the whole of a driver's state is two integers, which is
    /// exactly what has to survive a save taken mid-job.
    ///
    /// Reservations are claimed once, all or nothing, before the first toil runs. A driver whose
    /// claim fails aborts the job rather than proceeding half-claimed.
    /// </summary>
    public abstract class JobDriver
    {
        public Pawn Pawn { get; internal set; } = null!;
        public Job Job { get; internal set; } = null!;

        /// <summary>Which toil is running. Saved.</summary>
        public int ToilIndex { get; internal set; }

        /// <summary>Ticks accumulated inside the current toil. Saved.</summary>
        public int ToilProgress { get; internal set; }

        /// <summary>
        /// The cell this driver is working on *right now*, or -1 when it is not working.
        ///
        /// Working means a toil turning in place — a swing landing, a wall going up — and not
        /// merely having a job, which for most of its length is a walk. Presentation reads this
        /// through the snapshot and it is the only thing that distinguishes a colonist standing
        /// by a tree with an axe from a colonist standing by a tree.
        ///
        /// Default -1, so a driver that has not thought about it animates as it always did. A
        /// driver that has says which cell, because the figure must face what it is working on
        /// and by then its heading is zero.
        /// </summary>
        public virtual int WorkFocus => -1;

        public virtual void Begin(Pawn pawn, Job job)
        {
            Pawn = pawn;
            Job = job;
            ToilIndex = 0;
            ToilProgress = 0;
        }

        /// <summary>
        /// Claim everything the job will touch. All or nothing: a partial claim is released by
        /// the caller and the job never starts.
        /// </summary>
        public abstract bool TryMakeReservations(PawnContext ctx);

        /// <summary>Run one tick of the current toil.</summary>
        public abstract JobStatus Tick(PawnContext ctx);

        /// <summary>
        /// Is the pawn still standing somewhere it could actually do this work?
        ///
        /// <para><b>Asked every tick of the working toil, because arriving is not staying.</b> The
        /// walk toil puts a colonist on a stance its work giver chose, and every driver then
        /// worked for as long as the job lasted without ever asking again. That held while nothing
        /// could move a pawn it had not asked to move — and mining ended it: a dig drops anybody
        /// standing on the cell it cuts, retiring a climb drops whoever was on it, and both are
        /// deliberate. The owner watched the consequence and reported it as a colonist chopping a
        /// tree from one block down.</para>
        ///
        /// <para>The envelope is given as two numbers because the two directions are different
        /// questions. <paramref name="layersAbove"/> is how far above the work the pawn may stand
        /// — nought for felling, one for mining, which is worked from the rim of a hole and from
        /// directly on top of it as well as from beside it. <paramref name="layersBelow"/> is how
        /// far under it — nought for felling, because a tree and the colonist cutting it share a
        /// floor, and one for mining, because a pick goes overhead: a miner standing on the ground
        /// can cut the rock above its head or the overhang beside it, which is how you undercut a
        /// face. That second one is the owner's decision, and without it every rock that could
        /// only be worked from below simply waited — 33 standing orders with nowhere to stand,
        /// measured.</para>
        ///
        /// <para>Within one cell in X and Z, so the eight neighbours and the pawn's own cell. A
        /// driver that stands its colonist <em>in</em> the work — felling did, once — is covered
        /// by the same test.</para>
        /// </summary>
        /// <summary>
        /// Send the pawn back to the walk toil: it has been moved off the stance it was given and
        /// has to go and get back on it.
        ///
        /// <para>Walking back rather than failing, and measurement is why. Being displaced is not
        /// the pawn's fault and usually not permanent — a dig drops whoever is standing on the
        /// cell it cuts, and the stance is generally a step away afterwards. Failing the job
        /// instead cost half the colony's mining over 40,000 ticks (65 cells to 32), because
        /// every displacement threw away a walk as well as the work. If the stance really has
        /// gone, <see cref="GotoCell"/> fails on the next tick and the job fails with it, which is
        /// the same answer arrived at honestly.</para>
        /// </summary>
        protected void WalkBack()
        {
            ToilIndex = 0;
            ToilProgress = 0;
        }

        protected static bool StillInReach(
            PawnContext ctx, Pawn pawn, int work, int layersAbove, int layersBelow)
        {
            GridSize size = ctx.Size;
            if ((uint)work >= (uint)size.CellCount) return false;

            CellRef at = size.FromIndex(pawn.Cell);
            CellRef target = size.FromIndex(work);

            int up = at.Y - target.Y;
            if (up > layersAbove || up < -layersBelow) return false;

            return System.Math.Abs(at.X - target.X) <= 1 && System.Math.Abs(at.Z - target.Z) <= 1;
        }

        /// <summary>Anything to undo when the job ends, beyond releasing reservations.</summary>
        public virtual void Cleanup(PawnContext ctx, JobStatus status) { }

        /// <summary>
        /// Put down whatever the pawn is still holding, wherever there is room for it.
        ///
        /// <para><b>A job that ends mid-carry must put its load somewhere real. Anything else
        /// deletes it</b>, and a ten-day run would quietly eat the colony's stores. Where the pawn
        /// stands is the first choice, then the nearest cell that can take the load; it used to
        /// fall back to the cell the thing was carried <i>from</i>, which is -1 while it is
        /// carried, and the thing then existed nowhere at all.</para>
        ///
        /// <para>Shared rather than copied because there are two carriers now — a haul and a
        /// delivery — and a third would have copied whichever it happened to read. It says nothing
        /// to presentation, deliberately: this is a colonist dropping what it is holding, not
        /// stowing it on purpose, and that is a different motion which nothing draws yet. See
        /// <see cref="PutDown"/>.</para>
        /// </summary>
        protected void DropCarried(PawnContext ctx)
        {
            if (Job.CarriedItem < 0) return;
            var item = ctx.Items.Get(new ThingId(Job.CarriedItem));
            Job.CarriedItem = -1;
            if (item == null) return;

            int at = ctx.Items.NearestCellWithSpace(
                ctx.Cells, Pawn.Cell, item.DefIndex, item.Stack, DropSearchRadius);

            // A board with no room within that radius is packed solid with things, which nothing
            // in the game can produce; losing the load is the least bad answer, because putting it
            // down on top of something else would corrupt the cell index.
            if (at >= 0) ctx.Items.Drop(item, at);
            else ctx.Items.Despawn(item);
        }

        /// <summary>Cells to search outward for somewhere to put a dropped load down.</summary>
        public const int DropSearchRadius = 8;

        protected void NextToil()
        {
            ToilIndex++;
            ToilProgress = 0;
        }

        /// <summary>
        /// The toil index a driver uses for its settle, if it has one. Drivers put the settle last,
        /// so this is a name rather than a rule.
        /// </summary>
        public const int SettleToil = 2;

        /// <summary>
        /// Stand still for a beat now the work is done, then end the job.
        ///
        /// <para>Called as the whole of the settle toil, and it must be reached <b>before</b> a
        /// driver's own guards — by then the tree is felled and the designation cleared, so a
        /// guard asking "is this still a marked tree" would fail the job on the first settle tick
        /// and undo the very thing being added.</para>
        ///
        /// <para>The work has already happened. Nothing here changes the world, earns experience
        /// or holds anything up: the pawn simply stays where it is with
        /// <see cref="WorkFocus"/> reporting nothing, so the drawn figure eases out of its work
        /// stance while standing still instead of while walking away. See
        /// <see cref="JobDef.settleTicks"/> for the measurement that made this necessary.</para>
        /// </summary>
        protected JobStatus Settle(PawnContext ctx)
        {
            if (++ToilProgress < ctx.Content.Jobs[Job.DefIndex].settleTicks) return JobStatus.Ongoing;
            return JobStatus.Succeeded;
        }

        /// <summary>
        /// Count this tick as work: the pawn earns the job's experience in the skill the job
        /// trains. A driver calls it on the ticks that are the work — the swing, the carry — and
        /// not on the walk to it. A job that trains nothing costs one comparison.
        /// </summary>
        protected void Work(PawnContext ctx)
        {
            var def = ctx.Content.Jobs[Job.DefIndex];
            if (def.trainsSkill < 0 || def.experiencePerWorkTick <= 0) return;
            Pawn.GainExperience(def.trainsSkill, def.experiencePerWorkTick, ctx.CurrentTick);
        }

        /// <summary>
        /// Take a thing up off the floor, and report the stoop that goes with it.
        ///
        /// <para><b>The two belong together, which is why they are one call.</b> Anything a
        /// colonist lifts — a log, a stack of stone, a basket at the end of a row of crops, a
        /// carcass — is the same motion, and a job that picked something up without saying so would
        /// show a colonist acquiring it by magic while standing upright. Today there is exactly one
        /// pickup in the game and it is wired; tomorrow there is a harvest driver and a butcher's,
        /// and a seam is the only thing that stops either of them forgetting. This is the
        /// <c>docs/plans/vertical-slice.md</c> "where the seams are" argument applied to a very
        /// small thing.</para>
        ///
        /// <para><b>It now costs time, and it did not before</b> (owner, 2026-09-17). The stoop and
        /// the rise were presentation's alone: this was one tick, the drawn gesture took 0.8 s, and
        /// the pawn set off walking while its figure was still straightening. So it is no longer a
        /// call a driver makes in passing but a whole toil — the thing is still taken up in one
        /// instant, and the instant is now in the middle of a motion the colony pays for.</para>
        ///
        /// <para>Named for the toil rather than for the transfer, and there is deliberately no
        /// instant form left: a driver that wanted one would be a driver whose colonist acquires
        /// things by magic while standing upright, which is the case this seam exists to
        /// prevent.</para>
        ///
        /// <para>Returns <see cref="JobStatus.Ongoing"/> while the motion is running, and advances
        /// the driver to its next toil on the tick it finishes. A driver's whole case is
        /// <c>return LiftToil(ctx, item);</c>, so the two drivers that carry things cannot come to
        /// spend different amounts of time on the same motion.</para>
        ///
        /// <para><b>The gesture starts on the first tick and the thing changes hands in the
        /// middle</b>, at <see cref="PawnContent.LiftGraspTicks"/>. Both ends matter and they are
        /// different faults: report the gesture late and the figure snaps into a crouch it has no
        /// time left to finish; transfer early and the pile vanishes off the ground while the
        /// colonist is still upright, which is the magic-acquisition the stoop exists to
        /// prevent.</para>
        ///
        /// <para>The guard that the thing is still where the pawn is standing holds only until the
        /// grasp. After it the thing is carried, its cell is -1, and asking again would fail the
        /// job for having succeeded.</para>
        /// </summary>
        protected JobStatus LiftToil(PawnContext ctx, ColonyItem item)
        {
            int total = ctx.Content.LiftTicks;
            int grasp = ctx.Content.LiftGraspTicks;
            if (grasp > total) grasp = total;
            if (grasp < 1) grasp = 1;

            if (ToilProgress == 0) Pawn.BeginGesture(PawnGesture.Lift);

            int elapsed = ++ToilProgress;

            if (elapsed < grasp)
            {
                // Still bending. Somebody else may have taken it, or it may have been eaten.
                return item.Cell == Pawn.Cell ? JobStatus.Ongoing : JobStatus.Failed;
            }

            if (elapsed == grasp)
            {
                if (item.Cell != Pawn.Cell) return JobStatus.Failed;
                ctx.Items.PickUp(item, Pawn.Id);
                Job.CarriedItem = item.Id.Value;
            }

            if (elapsed < total) return JobStatus.Ongoing;

            NextToil();
            return JobStatus.Ongoing;
        }

        /// <summary>
        /// Set a carried thing down on purpose, and report the motion that goes with it.
        ///
        /// <para><b>On purpose</b> is the whole of the distinction, and it is why this is not
        /// simply "whenever a carried thing reaches the floor". A job that fails mid-carry also
        /// puts its load somewhere, and that is a colonist dropping what it is holding rather than
        /// stowing it — a different motion, and one nothing draws yet. So a failure path calls the
        /// store directly and says nothing, deliberately.</para>
        /// </summary>
        protected void PutDown(PawnContext ctx, ColonyItem item, int cell)
        {
            ctx.Items.Drop(item, cell);
            Pawn.BeginGesture(PawnGesture.Stow);
        }

        /// <summary>
        /// The walk toil, shared by every driver that goes somewhere.
        ///
        /// Reachability is answered <b>before</b> a path is asked for, never by asking for one:
        /// an unreachable target produces the most expensive possible search, and here it costs
        /// two array reads instead.
        /// </summary>
        protected JobStatus GotoCell(PawnContext ctx, int dest)
        {
            if (dest < 0) return JobStatus.Failed;

            if (Pawn.Cell == dest)
            {
                Pawn.ClearPath();
                Pawn.Destination = -1;
                return JobStatus.Succeeded;
            }

            if (Pawn.PathFailed)
            {
                Pawn.ClearPath();
                Pawn.Destination = -1;
                return JobStatus.Failed;
            }

            if (Pawn.Destination != dest)
            {
                Pawn.ClearPath();
                Pawn.Destination = dest;
            }

            if (!Pawn.HasPath && !Pawn.PathPending)
            {
                if (!ctx.Reachable(Pawn, dest))
                {
                    Pawn.Destination = -1;
                    return JobStatus.Failed;
                }

                ctx.Paths.Enqueue(new PathRequest(Pawn.Id.Value, Pawn.Cell, dest, Job.Mode));
                Pawn.PathPending = true;
            }

            return JobStatus.Ongoing;
        }
    }
}
