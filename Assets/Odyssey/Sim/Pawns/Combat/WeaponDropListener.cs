#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The dead let go of their weapon where they fall; the downed keep it (design 33 §1, the C2
    /// default; §6D). <b>Lane D's file</b>, registered first by <see cref="CombatListeners"/>.
    ///
    /// <para><b>At the corpse's cell</b>, which is where the pawn stood when it died (the hook is
    /// raised before the despawn, so the pawn still has its cell), or the nearest cell that can
    /// take it — a death on a pile puts the weapon beside the pile, never on it.</para>
    ///
    /// <para><b>Not forbidden.</b> A marauder's machete is the colony's to pick up the moment it
    /// falls; there is no hauling of corpses yet and no reason to hide the one thing a fight
    /// leaves that a colonist can use. <i>Our call</i>; the reference forbids some dropped gear,
    /// and a line here is where that would go.</para>
    ///
    /// <para>Costs one comparison per death for a bare-handed pawn. Called on an event, never per
    /// tick.</para>
    /// </summary>
    public sealed class WeaponDropListener : ICombatListener
    {
        readonly PawnContext _ctx;

        public WeaponDropListener(PawnContext ctx) { _ctx = ctx; }

        public void DamageApplied(in DamageReport report) { }

        /// <summary>A downed pawn keeps its weapon: nothing to do.</summary>
        public void Downed(Pawn pawn, Pawn? by, int tick) { }

        public void Died(Pawn pawn, Pawn? by, int corpseId, int tick)
        {
            if (pawn.EquippedItem == 0) return;
            int cell = _ctx.Corpses.TryGet(corpseId, out Corpse corpse) ? corpse.Cell : pawn.Cell;
            WeaponHand.PutDown(pawn, _ctx, cell);
        }
    }
}
