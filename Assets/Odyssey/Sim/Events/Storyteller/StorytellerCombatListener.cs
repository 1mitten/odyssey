#nullable enable
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Events
{
    /// <summary>
    /// Tells the storyteller when a colonist dies or is downed (design 68 §5), so the tension can
    /// ease after a disaster. Asks for the storyteller at the moment of the loss rather than holding
    /// it, because the listeners are registered while the world is being composed. Writes only the
    /// storyteller's own numbers: a listener may not touch the pawn list (<see cref="ICombatListener"/>).
    /// </summary>
    public sealed class StorytellerCombatListener : ICombatListener
    {
        readonly PawnContext _ctx;

        public StorytellerCombatListener(PawnContext ctx) => _ctx = ctx;

        public void SwingResolved(in SwingReport report) { }

        public void DamageApplied(in DamageReport report) { }

        public void Downed(Pawn pawn, Pawn? by, int tick) => _ctx.Storyteller?.NoteLoss(pawn, died: false, tick);

        public void Died(Pawn pawn, Pawn? by, int corpseId, int tick) => _ctx.Storyteller?.NoteLoss(pawn, died: true, tick);
    }
}
