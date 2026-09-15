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

        /// <summary>Anything to undo when the job ends, beyond releasing reservations.</summary>
        public virtual void Cleanup(PawnContext ctx, JobStatus status) { }

        protected void NextToil()
        {
            ToilIndex++;
            ToilProgress = 0;
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
