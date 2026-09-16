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

        protected void NextToil()
        {
            ToilIndex++;
            ToilProgress = 0;
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
        /// <para>The simulation's whole part is that it happened, here, now: this is one tick and
        /// stays one tick. The stoop and the rise are presentation's, take about eight-tenths of a
        /// second of game time and cost the colony nothing, so no throughput, golden or balance
        /// number moves — which means a figure may still be straightening as its pawn sets off
        /// walking. That is the accepted price of the owner's decision (2026-09-16) to keep the
        /// duration out of the simulation.</para>
        /// </summary>
        protected void TakeUp(PawnContext ctx, ColonyItem item)
        {
            ctx.Items.PickUp(item, Pawn.Id);
            Pawn.BeginGesture(PawnGesture.Lift);
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
