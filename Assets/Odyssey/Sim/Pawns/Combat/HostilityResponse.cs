#nullable enable
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// What an undrafted colonist does about danger near her (design 33 §18): the setting on her
    /// pane beside Draft. <b>The values are a save contract</b> — two bits of the combat record's
    /// flags word and of the hashed kind word — so they are numbered and appended to, never
    /// reordered.
    /// </summary>
    public enum HostilityResponse
    {
        /// <summary>
        /// The default, and every colonist's behaviour before §18: she fights whoever strikes her,
        /// and a threat beside her when she next thinks (design 33 §6A.6).
        /// </summary>
        FightBack = 0,

        /// <summary>
        /// Fight back, and also join a fight near her exactly as a drafted colonist on her hold
        /// does (design 33 §15) — then go back to work. Never drafted.
        /// </summary>
        Defend = 1,

        /// <summary>
        /// Run from danger near her, and from whoever strikes her; fight back only when cornered.
        /// </summary>
        Flee = 2,
    }

    /// <summary>
    /// The one owner of what a colonist's <see cref="HostilityResponse"/> does (design 33 §18d). Two
    /// callers ask the same function: <see cref="SelfDefenceThinkNode"/>, which gives the job, and
    /// <see cref="JobSystem"/>'s per-tick notice, which ends a job in hand so the node can. A notice
    /// that decided differently from the node would interrupt her and then give her work again,
    /// every tick, until the think-loop breaker parked her.
    /// </summary>
    public static class HostilityResponses
    {
        /// <summary>The number of responses: a value at or above it is not one.</summary>
        public const int Count = 3;

        /// <summary>
        /// What <paramref name="pawn"/>'s response would do now, if anything: for <b>Defend</b>, the
        /// drafted hold's own answer (<see cref="Melee.HoldTarget"/>) — a threat in reach, else the
        /// attacker of a colonist near her, with <paramref name="joining"/> saying which; for
        /// <b>Flee</b>, the nearest danger near her (<see cref="Melee.DangerTo"/>) and somewhere to
        /// run from it (<paramref name="fleeCell"/>) — no flee cell is no flight, and she fights
        /// back as Fight back would. <b>Fight back answers nothing here</b>: its rule is the
        /// retaliation and the threat beside her, which the node asks for every response.
        /// <para><b>Scales with the pawns on the board</b>, one pass, and for a flight the flee-cell
        /// search (at most twenty-five column searches), which is made only when there is danger.</para>
        /// </summary>
        public static bool Choose(PawnContext ctx, Pawn pawn, out Pawn? foe, out bool joining, out int fleeCell,
            bool sight = true)
        {
            foe = null;
            joining = false;
            fleeCell = -1;
            switch (pawn.Response)
            {
                case HostilityResponse.Defend:
                    foe = Melee.HoldTarget(ctx, pawn, out joining, sight);
                    return foe != null;
                case HostilityResponse.Flee:
                    foe = Melee.DangerTo(ctx, pawn);
                    if (foe == null) return false;
                    fleeCell = FleeJobDriver.FindFleeCell(ctx, pawn, foe.Cell, ctx.Content.Combat.fleeCells, TraverseMode.Colonist);
                    return fleeCell >= 0;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Fill <paramref name="job"/> with what the response chose: the run, or the attack — marked
        /// <see cref="AttackMeleeJobDriver.Joining"/> when it is somebody else's fight, so she chases
        /// it and lets it go once it is on no colonist (design 33 §15c). Unforced either way: it is
        /// her own idea, and a forced job is the player's.
        /// </summary>
        public static bool Fill(PawnContext ctx, Pawn pawn, Pawn foe, bool joining, int fleeCell, Job job)
        {
            if (fleeCell >= 0)
            {
                job.Reset(JobIndex.Flee);
                job.TargetCell = fleeCell;
                job.Mode = TraverseMode.Colonist;
                return true;
            }

            job.Reset(CombatJobs.AttackJobFor(pawn, ctx));
            job.TargetCell = foe.Cell;
            job.Mode = TraverseMode.Colonist;
            if (joining) job.DestCell = AttackMeleeJobDriver.Joining;
            pawn.CombatTarget = foe.Id.Value;
            return true;
        }

        /// <summary>
        /// Should the job in hand end so her response can act (design 33 §18d)? Asked every tick by
        /// the job system, only of a colonist whose response is not the default and only while
        /// something hostile is about. Not asked of one who is drafted (the draft overrides her
        /// response), down, broken, carried, asleep (a fight nearby does not wake her; a blow does),
        /// already fighting, fleeing or carrying somebody to a bed, or on a job the player forced —
        /// the player's order stands.
        /// </summary>
        public static bool Notices(PawnContext ctx, Pawn pawn)
        {
            if (pawn.Response == HostilityResponse.FightBack) return false;
            if (!pawn.IsColonist || pawn.Drafted || pawn.Downed || pawn.IsBroken || pawn.Asleep || pawn.CarriedBy != 0)
                return false;

            Job? job = pawn.CurrentJob;
            if (job == null || job.PlayerForced) return false;
            int def = job.DefIndex;
            if (CombatJobs.IsAttack(def) || def == JobIndex.Flee || def == JobIndex.Downed || def == JobIndex.Rescue)
                return false;

            // The sight scan on its cadence (design 47 §2d): asked every tick, it walks lines.
            return Choose(ctx, pawn, out _, out _, out _, Ranged.ScanDue(ctx, pawn));
        }

        /// <summary>
        /// Would <paramref name="response"/> have started the job in hand? A flight only under Flee;
        /// an attack she started herself under anything but Flee, except that a join into somebody
        /// else's fight is Defend's alone. What <c>SetHostilityResponse</c> asks of an undrafted
        /// colonist, so a new setting answers at once rather than when the old one's job ends.
        /// </summary>
        public static bool Started(HostilityResponse response, Job job)
        {
            if (job.PlayerForced) return true;
            if (job.DefIndex == JobIndex.Flee) return response == HostilityResponse.Flee;
            if (!CombatJobs.IsAttack(job.DefIndex)) return true;
            if (response == HostilityResponse.Flee) return false;
            return job.DestCell != AttackMeleeJobDriver.Joining || response == HostilityResponse.Defend;
        }
    }
}
