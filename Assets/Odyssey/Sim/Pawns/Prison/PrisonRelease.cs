#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <b>Letting a prisoner go</b> (design 58 §10): the one owner of what release and exile do.
    /// A warden asks <see cref="Wanted"/> and, beside her, calls <see cref="Let"/>.
    /// <list type="bullet">
    /// <item><b>An arrested colonist released goes back to work</b> (ruling 8): free, out of the
    /// jumpsuit, her own again — and she remembers being arrested.</item>
    /// <item>Anybody else, and an arrested colonist exiled, is <see cref="PawnCustody.Released"/>:
    /// the door is opened for her, and she walks off the board and is gone
    /// (<see cref="LeaveFreeThinkNode"/>). Nothing else changes: release is where a faction's
    /// goodwill will hang (M7), and exile is the same walk with no way back.</item>
    /// </list>
    /// </summary>
    public static class PrisonRelease
    {
        /// <summary>A held prisoner the player has chosen to release or exile, on her feet.</summary>
        public static bool Wanted(Pawn prisoner) =>
            prisoner.Custody == PawnCustody.Prisoner && prisoner.Prison != null && !prisoner.Downed
            && (prisoner.Prison.Mode == PrisonMode.Release || prisoner.Prison.Mode == PrisonMode.Exile);

        /// <summary>Let her go, by her mode. Nothing for anybody <see cref="Wanted"/> does not name.</summary>
        public static void Let(Pawn prisoner, PawnContext ctx)
        {
            if (!Wanted(prisoner)) return;
            PrisonRecord record = prisoner.Prison!;
            ctx.Combat?.Jobs.EndJob(prisoner, JobStatus.Failed);
            ctx.Construction?.ReleaseBedsOf(prisoner.Id.Value);

            if (record.Arrested && record.Mode == PrisonMode.Release)
            {
                prisoner.Custody = PawnCustody.Free;
                record.Arrested = false;
                record.Dressed = false;
                record.Mode = PrisonMode.Hold;
                record.Willingness = 0;
                record.LastChatTick = 0;
                if (record.IsEmpty) prisoner.Prison = null;
                prisoner.AddMemory(ThoughtIndex.WasArrested, ctx.CurrentTick);
                return;
            }
            prisoner.Custody = PawnCustody.Released;
        }
    }
}
