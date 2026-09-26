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
        /// Make <paramref name="pawn"/> the colony's prisoner, now. Every way in — capture,
        /// surrender, arrest and the debug menu's row — ends here, so they cannot disagree about
        /// what being taken means:
        /// <list type="bullet">
        /// <item>the job in hand ends as a failure, so a carried load is put down and a claim let go;</item>
        /// <item>a draft ends — a prisoner is nobody's to command;</item>
        /// <item>a colonist taken is remembered as arrested, and her colony bed goes back to the pool;</item>
        /// <item>a raider leaves her band, which no longer steers her.</item>
        /// </list>
        /// Returns false, changing nothing, for a pawn that is not a person, is dead, or is already held.
        /// </summary>
        public bool TakeIntoCustody(Pawn pawn)
        {
            if (!pawn.IsPerson || Melee.IsDead(pawn)) return false;
            if (pawn.Custody == PawnCustody.Prisoner) return false;

            bool wasColonist = pawn.IsColonist;
            Interrupt(pawn, JobStatus.Failed);
            pawn.Drafted = false;

            PrisonRecord record = pawn.Prison ??= new PrisonRecord();
            if (wasColonist)
            {
                record.Arrested = true;
                _ctx.Construction?.ReleaseBedsOf(pawn.Id.Value);
            }
            record.CaptureMark = false;
            _ctx.Raids?.Release(pawn);
            pawn.Custody = PawnCustody.Prisoner;
            return true;
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
