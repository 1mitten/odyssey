#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Whether a pawn's weapon is out (design 33 §8b, owner 2026-09-23): <b>drawn</b> in the right
    /// hand, or <b>sheathed</b> at the left hip. One rule, one owner, published as
    /// <see cref="PawnFlags.Drawn"/>.
    ///
    /// <para>Drawn when the pawn holds a weapon and any of four things is true:</para>
    /// <list type="bullet">
    /// <item>it is hostile — a bandit always has its weapon out;</item>
    /// <item>it is a drafted colonist;</item>
    /// <item>it is in a melee attack whose target is within <see cref="Reach"/> tiles —
    /// Chebyshev on its own layer, or the layer above or below;</item>
    /// <item>it is fighting back after a blow: inside the retaliation window
    /// <see cref="Pawn.RetaliateUntilTick"/> that the blow opened.</item>
    /// </list>
    ///
    /// <para><b>A report, never a state.</b> Every input is saved and hashed where it lives — the
    /// faction, the draft, the job, the two cells, the retaliation window, the hand — so the answer
    /// is derived at each publish, is itself neither saved nor hashed, and a save taken mid-fight
    /// publishes the same answer after the load. Nothing in the simulation reads it back. <b>The put
    /// away is presentation's</b>: about two seconds after the last reason ends, or at once on
    /// release from the draft (<c>Odyssey.Hud.WeaponSheath</c>), because the simulation has no saved
    /// tick the last reason ended on and a new unsaved one would be a field that changes nothing
    /// the simulation does and is still not reproducible after a load.</para>
    ///
    /// <para><b>Scales with nothing</b>: constant work per pawn per publish — four field reads,
    /// one registry lookup and two cell decodes.</para>
    /// </summary>
    public static class WeaponDraw
    {
        /// <summary>How near an attack's target draws the weapon, in tiles (owner, 2026-09-23).</summary>
        public const int Reach = 2;

        /// <summary>
        /// Is <paramref name="pawn"/>'s weapon out at <paramref name="tick"/>? False for bare hands:
        /// there is nothing to draw.
        /// </summary>
        public static bool IsDrawn(PawnContext ctx, Pawn pawn, int tick) =>
            WeaponHand.Held(pawn, ctx) != null && HasReason(ctx, pawn, tick);

        /// <summary>The rule without the hand: does this pawn have a reason to be fighting now?</summary>
        public static bool HasReason(PawnContext ctx, Pawn pawn, int tick)
        {
            if (pawn.IsHostile || pawn.Drafted) return true;
            if (pawn.RetaliateAgainst != 0 && tick < pawn.RetaliateUntilTick) return true;
            return TargetNear(ctx, pawn);
        }

        /// <summary>
        /// Is this pawn in a melee attack on a pawn within <see cref="Reach"/> tiles? The attack job
        /// and not merely a named target: a rescue names its patient in the same field.
        /// </summary>
        public static bool TargetNear(PawnContext ctx, Pawn pawn)
        {
            if (pawn.CombatTarget == 0 || pawn.CurrentJob == null
                || pawn.CurrentJob.DefIndex != JobIndex.AttackMelee) return false;
            Pawn? target = ctx.Pawns.Get(new PawnId(pawn.CombatTarget));
            return target != null && Within(ctx.Size, pawn.Cell, target.Cell, Reach);
        }

        /// <summary>
        /// Chebyshev distance on the ground of at most <paramref name="tiles"/>, and at most one
        /// layer apart: a target on the step above is near, one three floors down is not.
        /// </summary>
        public static bool Within(GridSize size, int a, int b, int tiles)
        {
            CellRef p = size.FromIndex(a), q = size.FromIndex(b);
            int dx = p.X - q.X, dz = p.Z - q.Z, dy = p.Y - q.Y;
            if (dx < 0) dx = -dx;
            if (dz < 0) dz = -dz;
            if (dy < 0) dy = -dy;
            return dx <= tiles && dz <= tiles && dy <= 1;
        }
    }
}
