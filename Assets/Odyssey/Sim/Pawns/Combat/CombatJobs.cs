#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The one owner of "which attack job" and "is this an attack" (design 47 §2d). Every place that
    /// starts a fight — the think nodes, the draft's hold, the order, the knockback's re-issue — asks
    /// <see cref="AttackJobFor"/>, and every place that asks whether a pawn is fighting asks
    /// <see cref="IsAttack"/>, so a gun-holder shoots and a sword-holder swings from one rule and
    /// "in an attack" never forgets the gun.
    /// </summary>
    public static class CombatJobs
    {
        /// <summary>Is this job def an attack on somebody or something — a swing or a shot?</summary>
        public static bool IsAttack(int jobDef) => jobDef == JobIndex.AttackMelee || jobDef == JobIndex.AttackRanged;

        /// <summary>Is this pawn in an attack job right now, whatever its target?</summary>
        public static bool InAttack(Pawn pawn) => pawn.CurrentJob != null && IsAttack(pawn.CurrentJob.DefIndex);

        /// <summary>
        /// The attack this pawn makes with what it holds: <c>Job_AttackRanged</c> for a gun,
        /// <c>Job_AttackMelee</c> for anything else — a blade, a club, bare hands or teeth.
        /// </summary>
        public static int AttackJobFor(Pawn pawn, PawnContext ctx) =>
            ctx.WeaponRules.ArmamentOf(pawn, ctx).Attack.IsRanged ? JobIndex.AttackRanged : JobIndex.AttackMelee;

        /// <summary>
        /// May this pawn attack a building with what it holds? Not with a gun, until the ranged
        /// driver learns to (design 47 §2d, R6): a gun-holder's order on a door is refused, and a
        /// pistol bandit with nobody to reach falls through to theft rather than pistol-whipping a wall.
        /// </summary>
        public static bool CanBreakBuildings(Pawn pawn, PawnContext ctx) =>
            !ctx.WeaponRules.ArmamentOf(pawn, ctx).Attack.IsRanged;
    }
}
