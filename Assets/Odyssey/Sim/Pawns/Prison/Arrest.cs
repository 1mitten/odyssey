#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <b>Arresting one of the colony's own</b> (design 58 §10; owner's ruling 8). Any colonist, at
    /// any time, by another's hand. At the touch she may resist — more likely the unhappier she is —
    /// and a colonist who resists is an escapee: she runs, and is brought down and carried in like
    /// any other. One who comes quietly is given a prison bed and walks there herself. <b>Either
    /// way the colony minds</b>: every free colonist takes <i>Colonist arrested</i>, and the ledger
    /// is told. Releasing her later returns her to the colony (<see cref="PrisonRelease"/>).
    /// </summary>
    public static class Arrest
    {
        /// <summary>How long she holds a grudge against the arrester, in ticks: the rest of the fight.</summary>
        public const int GrudgeTicks = 2_500;

        /// <summary>
        /// Her chance of resisting, per mille: 20 % + (500 − mood) / 20 points, held to 5–60 %.
        /// A content colonist rarely fights it; a miserable one often does.
        /// </summary>
        public static int ResistPerMille(Pawn target) =>
            System.Math.Clamp(200 + (500 - target.Mood) / 2, 50, 600);

        /// <summary>Whether <paramref name="target"/> can be arrested at all: one of ours, on her feet.</summary>
        public static bool CanBeArrested(Pawn target) => target.IsColonist && Melee.IsStanding(target);

        /// <summary>
        /// The arrester has reached her. Roll whether she resists, take her either way, tell the
        /// colony; a quiet arrest is given a bed, a resisted one breaks out at once.
        /// </summary>
        public static void Contact(Pawn arrester, Pawn target, PawnContext ctx)
        {
            if (!CanBeArrested(target) || ctx.Combat == null) return;
            int tick = ctx.CurrentTick;
            var rng = DeterministicRandom.ForTick(ctx.Seed, tick, PrisonPurpose.ArrestResist ^ (uint)target.Id.Value);
            bool resists = rng.NextInt(1_000) < ResistPerMille(target);

            JobSystem jobs = ctx.Combat.Jobs;
            if (!jobs.TakeIntoCustody(target)) return;

            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i].IsColonist) pawns[i].AddMemory(ThoughtIndex.ColonistArrested, tick);
            ctx.Incidents?.Ledger.Record(IncidentHandle.Arrested, target.Cell, tick);

            if (resists)
            {
                jobs.BreakOut(target);
                target.RetaliateAgainst = arrester.Id.Value;
                target.RetaliateUntilTick = tick + GrudgeTicks;
                return;
            }
            int bed = CaptureRules.BedFor(target, target, ctx);
            if (bed >= 0) ctx.Construction?.AssignOwnerAt(bed, target.Id.Value);
        }
    }
}
