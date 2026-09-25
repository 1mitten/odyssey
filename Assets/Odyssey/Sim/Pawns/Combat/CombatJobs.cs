#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

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
        /// The attack this pawn makes on <paramref name="target"/> with what it holds:
        /// <c>Job_AttackRanged</c> for a gun at a target out of reach, <c>Job_AttackMelee</c> for
        /// anything else — a blade, a club, bare hands, teeth, or a gun at a target it could strike,
        /// which it clubs (design 47 §12, the reference's rule: an enemy within reach is fought in
        /// melee, gun or no gun).
        /// </summary>
        public static int AttackJobFor(Pawn pawn, PawnContext ctx, Pawn target) =>
            IsGunHolder(pawn, ctx) && !Melee.InReach(ctx, pawn, target, pawn.OwnMode)
                ? JobIndex.AttackRanged
                : JobIndex.AttackMelee;

        /// <summary>Does this pawn hold a gun?</summary>
        public static bool IsGunHolder(Pawn pawn, PawnContext ctx) => ctx.WeaponRules.ArmamentOf(pawn, ctx).Attack.IsRanged;

        /// <summary>
        /// An enemy standing within <paramref name="me"/>'s reach, or null (design 47 §12): her own
        /// target first — standing, or down on an order to finish it — else, for a colonist, a threat
        /// to her (<see cref="Melee.IsThreatTo"/>), and for anybody else a standing colonist. What
        /// stops a gun-holder shooting. <b>Scales with the pawns on the board</b>, and is asked only of
        /// a pawn in a ranged attack.
        /// </summary>
        public static Pawn? EnemyInReach(PawnContext ctx, Pawn me)
        {
            TraverseMode mode = me.OwnMode;
            if (me.CombatTarget != 0)
            {
                Pawn? own = ctx.Pawns.Get(new PawnId(me.CombatTarget));
                bool finishing = me.CurrentJob != null && me.CurrentJob.DestCell == AttackMeleeJobDriver.ToTheDeath;
                if (own != null && !Melee.IsDead(own) && (Melee.IsStanding(own) || finishing)
                    && Melee.InReach(ctx, me, own, mode)) return own;
            }
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == me || !Melee.IsStanding(other)) continue;
                bool foe = me.IsColonist ? Melee.IsThreatTo(other, me) : other.IsColonist;
                if (foe && Melee.InReach(ctx, me, other, mode)) return other;
            }
            return null;
        }

        /// <summary>
        /// May this pawn attack a building with what it holds? Not with a gun, until the ranged
        /// driver learns to (design 47 §2d, R6): a gun-holder's order on a door is refused, and a
        /// pistol bandit with nobody to reach falls through to theft rather than pistol-whipping a wall.
        /// </summary>
        public static bool CanBreakBuildings(Pawn pawn, PawnContext ctx) =>
            !ctx.WeaponRules.ArmamentOf(pawn, ctx).Attack.IsRanged;
    }
}
