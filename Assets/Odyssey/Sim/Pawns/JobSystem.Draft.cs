#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    // The draft and the orders a drafted colonist takes (design 33 §2). In the job system rather
    // than beside it for the reason HandleForceJob gives: starting and ending jobs is what the
    // pipeline is, and a second path into StartJob would be a second path out of it — which is
    // where a reservation leak comes from.
    public sealed partial class JobSystem
    {
        /// <summary>
        /// <c>SetDrafted(A = pawn, B = 1 to draft, 0 to release)</c>.
        ///
        /// <para>Refused for a pawn that does not exist or is not a person, and for drafting a
        /// colonist in a mental break — the one state the player may not command through.
        /// <c>AlreadyInThatState</c> for a no-op, so a key held down or a double click is quiet
        /// rather than an error.</para>
        /// </summary>
        public IntentRejection HandleSetDrafted(Intent intent)
        {
            Pawn? pawn = _ctx.Pawns.Get(new PawnId(intent.A));
            if (pawn == null || !pawn.IsPerson) return IntentRejection.NotPermitted;

            bool want = intent.B != 0;
            if (pawn.Drafted == want) return IntentRejection.AlreadyInThatState;
            if (want && pawn.IsBroken) return IntentRejection.NotPermitted;

            SetDrafted(pawn, want, IntentTick);
            return IntentRejection.None;
        }

        /// <summary>
        /// Draft or release one colonist, now. The job in hand ends as a failure — the one release
        /// path, so a carried load is put down, a claimed bed freed and a sleeper woken by the
        /// drivers' own cleanup — keeping the step in progress (<see cref="Interrupt"/>). A draft
        /// then starts the hold in the same call, so a paused game already reads "Drafted"; a
        /// release leaves the colonist between jobs and the tree gives it work on its next tick.
        /// </summary>
        public void SetDrafted(Pawn pawn, bool drafted, int tick)
        {
            if (pawn.Drafted == drafted) return;

            Interrupt(pawn, JobStatus.Failed);
            pawn.Drafted = drafted;
            if (!drafted) return;

            pawn.DraftQuietSinceTick = tick;
            Job job = pawn.JobBuffer;
            job.Reset(JobIndex.DraftHold);
            job.PlayerForced = true;
            StartJob(pawn, job, tick);
        }

        /// <summary>
        /// <c>OrderMove(cell, A = pawn)</c>: walk there and hold (design 33 §2d).
        ///
        /// <para>The clicked cell is lifted to where a colonist would stand in that column — the
        /// debug spawn's rule, because a click names the ground and a colonist stands on it — and
        /// must be reachable. Only a drafted colonist takes the order, as in the reference: a
        /// right-click on an undrafted colonist's behalf is not a gesture this build gives a
        /// meaning. A colonist sent where another drafted colonist already stands or is going is
        /// spread to the nearest free cell beside it (<see cref="Spread"/>).</para>
        /// </summary>
        public IntentRejection HandleOrderMove(Intent intent)
        {
            Pawn? pawn = _ctx.Pawns.Get(new PawnId(intent.A));
            if (pawn == null || !pawn.IsPerson || !pawn.Drafted) return IntentRejection.NotPermitted;

            CellRef cell = intent.Cell;
            if (!_ctx.Size.Contains(cell.X, cell.Z, cell.Y)) return IntentRejection.OutOfBounds;

            int dest = _ctx.Cells.NearestWalkableInColumn(cell.X, cell.Z, cell.Y);
            if (dest < 0 || !_ctx.Reachable(pawn, dest, TraverseMode.Colonist)) return IntentRejection.NotPermitted;
            dest = Spread(pawn, dest);

            int tick = IntentTick;
            pawn.DraftQuietSinceTick = tick;

            Interrupt(pawn, JobStatus.Failed);
            Job job = pawn.JobBuffer;
            job.Reset(dest == pawn.Cell ? JobIndex.DraftHold : JobIndex.Goto);
            job.TargetCell = dest == pawn.Cell ? -1 : dest;
            job.PlayerForced = true;
            return StartJob(pawn, job, tick) ? IntentRejection.None : IntentRejection.NotPermitted;
        }

        /// <summary>
        /// Intents drain at the top of the tick, before the pawn phase syncs the context, so the
        /// context's tick is still the last one's while the world's is this one's (the argument
        /// <see cref="HandleForceJob"/> makes). The fallback is a command sent before the world
        /// has ever ticked, when the two are the same number anyway.
        /// </summary>
        int IntentTick => _ctx.World?.CurrentTick ?? _ctx.CurrentTick;

        /// <summary>
        /// End the job in hand, but let the colonist land the step it is part way through
        /// (design 33 §2d).
        ///
        /// <para>Ending a job clears the path, and with it the step in progress: the pawn stays on
        /// the cell it was leaving while its figure has been drawn most of the way into the next,
        /// so the figure snaps back by up to a cell. That is the fault the job expiry was taught to
        /// avoid (design 29 §3a) and an order cannot wait for a boundary the way an expiry can —
        /// the player clicked now. So the one step is kept as a path of its own, the pawn is
        /// marked as finishing it, and the next job's walk waits for it to land.</para>
        /// </summary>
        internal void Interrupt(Pawn pawn, JobStatus status)
        {
            bool midStep = pawn.HasPath && pawn.MoveProgress > 0;
            int next = midStep ? pawn.Path[pawn.PathIndex] : -1;
            int progress = pawn.MoveProgress;

            EndJob(pawn, status);
            if (!midStep) return;

            _step[0] = pawn.Cell;
            _step[1] = next;
            pawn.AdoptPath(_step, 2);
            pawn.MoveProgress = progress;
            pawn.FinishingStepTo = next;
        }

        // Scratch for Interrupt: copied into the pawn's own buffer by AdoptPath, never kept.
        readonly int[] _step = new int[2];

        /// <summary>How far round the named cell a colonist is spread, in rings. Small on purpose:
        /// a click that sends somebody three cells from where it was aimed is worse than two
        /// figures sharing a tile, which the crowd sidestep already draws legibly.</summary>
        public const int SpreadRings = 2;

        /// <summary>
        /// The named cell if no other drafted colonist stands on it or is walking to it, else the
        /// nearest cell within <see cref="SpreadRings"/> that is free, standable and reachable — in
        /// a fixed scan order, so the answer is a function of the world and never of timing. Falls
        /// back to the named cell if the rings are full.
        /// </summary>
        int Spread(Pawn pawn, int want)
        {
            if (!Taken(pawn, want)) return want;

            GridSize size = _ctx.Size;
            CellRef at = size.FromIndex(want);
            for (int ring = 1; ring <= SpreadRings; ring++)
            for (int dz = -ring; dz <= ring; dz++)
            for (int dx = -ring; dx <= ring; dx++)
            {
                if (System.Math.Abs(dx) != ring && System.Math.Abs(dz) != ring) continue;
                int x = at.X + dx, z = at.Z + dz;
                if (!size.Contains(x, z, at.Y)) continue;

                int cell = _ctx.Cells.NearestWalkableInColumn(x, z, at.Y);
                if (cell < 0 || System.Math.Abs(size.FromIndex(cell).Y - at.Y) > 1) continue;
                if (Taken(pawn, cell)) continue;
                if (!_ctx.Reachable(pawn, cell, TraverseMode.Colonist)) continue;
                return cell;
            }

            return want;
        }

        /// <summary>Is another drafted colonist standing on this cell, or on its way to it?</summary>
        bool Taken(Pawn pawn, int cell)
        {
            var pawns = _ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == pawn || !other.Drafted) continue;
                Job? job = other.CurrentJob;
                if (job != null && job.DefIndex == JobIndex.Goto)
                {
                    if (job.TargetCell == cell) return true;
                    continue;
                }
                if (other.Cell == cell) return true;
            }
            return false;
        }
    }
}
