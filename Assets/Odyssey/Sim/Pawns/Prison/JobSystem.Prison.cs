#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    // Taking a pawn into custody and letting her out of it (design 58 §4). In the job system for
    // the reason the draft gives: a change of custody ends the job in hand, and ending jobs is what
    // the pipeline is — a second path out of a job is where a reservation leak comes from.
    public sealed partial class JobSystem
    {
        /// <summary>
        /// Make <paramref name="pawn"/> the colony's prisoner, now: the job in hand ends as a
        /// failure — so a carried load is put down and a claim let go — and then
        /// <see cref="CustodyRules.Take"/>, the one owner of what being taken means. Returns false,
        /// changing nothing, for a pawn that is not a person, is dead, or is already held.
        /// </summary>
        public bool TakeIntoCustody(Pawn pawn)
        {
            if (!CustodyRules.CanTake(pawn)) return false;
            Interrupt(pawn, JobStatus.Failed);
            return CustodyRules.Take(pawn, _ctx);
        }

        /// <summary>
        /// <c>SetCaptureMark(A = pawn, B = 1 mark / 0 clear)</c> (design 58 §7): ask a warden to
        /// bring this person in once she is down. Refused for a colonist, an animal, the dead and
        /// anybody already held; <c>AlreadyInThatState</c> for a no-op. A standing setting: the
        /// warden's giver reads it, and it is spent when she is taken.
        /// </summary>
        public IntentRejection HandleSetCaptureMark(Intent intent)
        {
            Pawn? pawn = _ctx.Pawns.Get(new PawnId(intent.A));
            if (pawn == null || !pawn.IsPerson || pawn.IsColonist || Melee.IsDead(pawn)
                || pawn.Custody != PawnCustody.Free)
                return IntentRejection.NotPermitted;

            bool mark = intent.B != 0;
            bool marked = pawn.Prison != null && pawn.Prison.CaptureMark;
            if (mark == marked) return IntentRejection.AlreadyInThatState;
            if (mark) (pawn.Prison ??= new PrisonRecord()).CaptureMark = true;
            else
            {
                pawn.Prison!.CaptureMark = false;
                if (pawn.Prison.IsEmpty) pawn.Prison = null;
            }
            return IntentRejection.None;
        }

        /// <summary>
        /// <c>OrderCapture(A = colonist, B = target)</c> (design 58 §7): send this colonist, drafted
        /// or not, to carry the downed target to a prison bed now. Marks the target first, so a
        /// refused or interrupted order still leaves the warden's giver to finish it. Refused for a
        /// colonist who cannot act, a target nobody means to hold, one somebody is already coming
        /// for, and when there is no free prison bed to take her to.
        /// </summary>
        public IntentRejection HandleOrderCapture(Intent intent)
        {
            Pawn? pawn = _ctx.Pawns.Get(new PawnId(intent.A));
            if (pawn == null || !pawn.IsColonist || pawn.Downed || pawn.IsBroken) return IntentRejection.NotPermitted;
            Pawn? target = intent.B == 0 ? null : _ctx.Pawns.Get(new PawnId(intent.B));
            if (target == null || target == pawn) return IntentRejection.NotPermitted;
            if (pawn.CurrentJob?.DefIndex == JobIndex.Capture && pawn.CombatTarget == target.Id.Value)
                return IntentRejection.AlreadyInThatState;

            if (target.Custody == PawnCustody.Free && !target.IsColonist && target.IsPerson && !Melee.IsDead(target))
                (target.Prison ??= new PrisonRecord()).CaptureMark = true;
            if (!CaptureRules.WantsCapture(target, _ctx)) return IntentRejection.NotPermitted;
            if (!_ctx.Reservations.CanReserve(pawn.Id, RescueRules.PatientKey(target))) return IntentRejection.NotPermitted;
            if (!_ctx.Reachable(pawn, target.Cell, CaptureRules.Mode)) return IntentRejection.NotPermitted;
            int bed = CaptureRules.BedFor(target, pawn, _ctx);
            if (bed < 0) return IntentRejection.NotPermitted;

            int tick = IntentTick;
            if (pawn.Drafted) pawn.DraftQuietSinceTick = tick;
            Interrupt(pawn, JobStatus.Failed);

            pawn.CombatTarget = target.Id.Value;
            Job job = pawn.JobBuffer;
            job.Reset(JobIndex.Capture);
            job.TargetCell = target.Cell;
            job.DestCell = bed;
            job.Mode = CaptureRules.Mode;
            job.PlayerForced = true;
            return StartJob(pawn, job, tick) ? IntentRejection.None : IntentRejection.NotPermitted;
        }

        /// <summary>
        /// <c>DebugImprison(cell, A = pawn, B = 0 take / 1 free)</c> (design 58 §4): take the pawn
        /// named by <c>A</c> — or, with <c>A</c> of nought, the person nearest the cell who is not
        /// already held — into custody, or set a held one free. Debug-menu only, so the whole
        /// prisoner model can be played before capture exists. Freeing drops the record, joined
        /// or not: a debug freeing is an undo, not a recruitment.
        /// </summary>
        public IntentRejection HandleDebugImprison(Intent intent)
        {
            bool take = intent.B == 0;
            Pawn? pawn = intent.A > 0 ? _ctx.Pawns.Get(new PawnId(intent.A)) : NearestPerson(intent.Cell, held: !take);
            if (pawn == null || !pawn.IsPerson) return IntentRejection.NotPermitted;

            if (take)
                return TakeIntoCustody(pawn) ? IntentRejection.None : IntentRejection.AlreadyInThatState;

            if (pawn.Custody == PawnCustody.Free) return IntentRejection.AlreadyInThatState;
            Interrupt(pawn, JobStatus.Failed);
            pawn.Custody = PawnCustody.Free;
            pawn.Prison = null;
            _ctx.Construction?.ReleaseBedsOf(pawn.Id.Value);
            return IntentRejection.None;
        }

        /// <summary>The living person nearest <paramref name="cell"/> who is held, or who is not.</summary>
        Pawn? NearestPerson(CellRef cell, bool held)
        {
            int at = _ctx.Size.Contains(cell) ? _ctx.Size.Index(cell) : -1;
            Pawn? nearest = null;
            int best = int.MaxValue;
            var pawns = _ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!pawn.IsPerson || Melee.IsDead(pawn)) continue;
                if ((pawn.Custody != PawnCustody.Free) != held) continue;
                int distance = at < 0 ? 0 : _ctx.Distance(at, pawn.Cell);
                if (distance >= best) continue;
                nearest = pawn;
                best = distance;
            }
            return nearest;
        }
    }
}
