#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The fight's own pass over the pawns (design 33 §3, §5): where a swing that has reached its
    /// wind-up tick is resolved and applied, where a stun and a retaliation run out, where a
    /// downed animal heals and a downed colonist in a bed does, and where a death is deferred.
    /// <b>Lane A's file</b> (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>Registered in the Pawns phase at order 25</b> — after the job pipeline (20) has
    /// decided who swings, before movement (30) steps anybody — so a blow is decided against the
    /// positions the jobs saw and lands before anyone walks out of reach.</para>
    ///
    /// <para><b>An empty tick from the contracts step.</b> Lane A fills it and, per
    /// <c>docs/process.md</c> §3, states what it scales with here. The shape the design expects is
    /// "the pawns in a fight, plus one comparison per pawn": a colony at peace should pay a branch
    /// a pawn and nothing else, which the 20-against-20 row in <c>TickBenchmarkTests</c> will
    /// measure.</para>
    ///
    /// <para>Not <see cref="Odyssey.Sim.Contracts.IStateHashable"/>: every piece of combat state it
    /// touches is hashed where it lives — on the pawn (<see cref="Pawn.ContributeTo"/>), in the
    /// corpse registry and in the edifice damage store.</para>
    /// </summary>
    public class CombatSystem : IWorldSystem
    {
        readonly PawnContext _ctx;

        public CombatSystem(PawnContext ctx) =>
            _ctx = ctx ?? throw new System.ArgumentNullException(nameof(ctx));

        public string Name => "Combat";

        public TickPhase Phase => TickPhase.Pawns;

        /// <summary>After the job pipeline (20), before movement (30).</summary>
        public int Order => 25;

        /// <summary>The context the fight reads and writes through: its rules, hooks, log and corpses.</summary>
        public PawnContext Context => _ctx;

        /// <summary>Scales with: nothing, until lane A fills it.</summary>
        public virtual void Tick(SimWorld world)
        {
        }
    }
}
