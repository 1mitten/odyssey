#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <b>A held pawn leaving the board</b> (design 60 §9c, §10): an escapee reaching the edge, or a
    /// prisoner released or exiled walking off it. One owner of what goes with her, so the two ways
    /// out cannot disagree: her weapon leaves with her (a disarmed prisoner has none), her prison bed
    /// goes back, a raid band is told she is gone, the ledger is told if there is anything to tell,
    /// and she is despawned. Runs deferred, at the end of the tick.
    /// </summary>
    public static class PrisonExit
    {
        /// <summary>
        /// Take her off the board if she is still on the walk that brought her here and standing on
        /// its end. Returns whether she left.
        /// </summary>
        public static bool Leave(PawnContext ctx, Pawn pawn, int jobDef, int incident, int tick)
        {
            if (ctx.Pawns.Get(pawn.Id) != pawn) return false;
            Job? job = pawn.CurrentJob;
            if (pawn.Downed || job == null || job.DefIndex != jobDef || pawn.Cell != job.DestCell) return false;

            ColonyItem? weapon = WeaponHand.Held(pawn, ctx);
            pawn.EquippedItem = 0;
            if (weapon != null) ctx.Items.Despawn(weapon);

            ctx.Construction?.ReleaseBedsOf(pawn.Id.Value);
            if (incident >= 0) ctx.Incidents?.Ledger.Record(incident, pawn.Cell, tick);
            ctx.Raids?.NoteLeft(pawn);

            ctx.Combat?.Jobs.EndJob(pawn, JobStatus.Succeeded);
            ctx.Pawns.Despawn(pawn);
            return true;
        }
    }
}
