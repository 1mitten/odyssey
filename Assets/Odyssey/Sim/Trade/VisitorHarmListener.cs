#nullable enable
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Trade
{
    /// <summary>
    /// What harm does to a guest (design 65 §7; the owner's <i>"1 and 3 depending"</i>):
    /// <list type="bullet">
    /// <item><b>On purpose</b> — a colonist swinging or shooting at the guest it was ordered to attack,
    /// hit or miss — turns it hostile (<see cref="TradeSystem.Turn"/>).</item>
    /// <item><b>By accident</b> — a stray, a raider, a fall, a colonist aiming at somebody else — sends
    /// it home at once (<see cref="TradeSystem.SendAway"/>): only a blow that landed counts, since a
    /// miss aimed elsewhere touched nobody.</item>
    /// </list>
    /// Registered after the friendly-fire listener; it costs one flag test per blow on anybody.
    /// </summary>
    public sealed class VisitorHarmListener : ICombatListener
    {
        readonly PawnContext _ctx;

        public VisitorHarmListener(PawnContext ctx) { _ctx = ctx; }

        public void SwingResolved(in SwingReport report)
        {
            Pawn target = report.Target;
            if (!target.IsVisitor || Melee.IsDead(target) || _ctx.Trade == null) return;
            Pawn? by = report.Attacker;
            if (by != null && by.IsColonist && by.CombatTarget == target.Id.Value)
            {
                _ctx.Trade.Turn(target);
                return;
            }
            if (report.Landed) Accident(target);
        }

        public void DamageApplied(in DamageReport report)
        {
            Pawn target = report.Target;
            if (!target.IsVisitor || Melee.IsDead(target) || _ctx.Trade == null) return;
            Pawn? by = report.Attacker;
            if (by != null && by.IsColonist && by.CombatTarget == target.Id.Value) return;
            Accident(target);
        }

        public void Downed(Pawn pawn, Pawn? by, int tick) { }

        public void Died(Pawn pawn, Pawn? by, int corpseId, int tick) { }

        void Accident(Pawn target)
        {
            Visit? visit = _ctx.Trade!.VisitOf(target.Id.Value);
            if (visit != null) _ctx.Trade.SendAway(visit, target);
        }
    }
}
