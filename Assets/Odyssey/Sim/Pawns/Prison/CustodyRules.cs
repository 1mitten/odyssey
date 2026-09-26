#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <b>Taking a pawn into custody, and what that means</b> (design 58 §4). Every way in —
    /// capture, surrender, arrest and the debug menu's row — ends here, so they cannot disagree:
    /// <list type="bullet">
    /// <item>a draft ends — a prisoner is nobody's to command;</item>
    /// <item>a colonist taken is remembered as arrested, and her colony bed goes back to the pool;</item>
    /// <item>a capture mark is spent;</item>
    /// <item>custody is <see cref="PawnCustody.Prisoner"/>.</item>
    /// </list>
    /// <b>A raider taken stays on her band's roll.</b> The band counts a held member as lost and
    /// never as standing (<c>RaidSystem.InTheFight</c>), which is what a downed raider already was,
    /// so taking one changes neither when the band withdraws nor when it is done with.
    ///
    /// <para>The job in hand is the caller's: <c>JobSystem.TakeIntoCustody</c> ends it first, and
    /// the capture driver takes a pawn who is lying downed in a job that is still right for her.</para>
    /// </summary>
    public static class CustodyRules
    {
        /// <summary>Whether this pawn may be taken at all: a living person nobody holds already.</summary>
        public static bool CanTake(Pawn pawn) =>
            pawn.IsPerson && !Melee.IsDead(pawn) && pawn.Custody != PawnCustody.Prisoner;

        /// <summary>Take her. Returns false, changing nothing, where <see cref="CanTake"/> says no.</summary>
        public static bool Take(Pawn pawn, PawnContext ctx)
        {
            if (!CanTake(pawn)) return false;
            bool wasColonist = pawn.IsColonist;
            pawn.Drafted = false;

            PrisonRecord record = pawn.Prison ??= new PrisonRecord();
            if (wasColonist)
            {
                record.Arrested = true;
                ctx.Construction?.ReleaseBedsOf(pawn.Id.Value);
            }
            record.CaptureMark = false;
            pawn.Custody = PawnCustody.Prisoner;
            return true;
        }
    }
}
