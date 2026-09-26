#nullable enable
namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Rolls every colonist's starting skill levels, once, on the world's first tick (U37).
    ///
    /// <para><b>Why the first tick and not placement.</b> <see cref="ColonyScenario.Place"/> runs
    /// inside <see cref="ColonyWorld.Build"/>, before <c>GoldenMasterTests</c> takes its
    /// "Generated" hash — the state of a built-but-never-ticked world. The plan's done criterion
    /// for this unit is that the roll moves every "Simulated" golden and no "Generated" one, so
    /// the roll cannot happen at placement: it has to happen after that hash is read and before
    /// or during the run that produces "Simulated". The first tick is the earliest point that is
    /// true at, and it is also, incidentally, the honest shape of the thing — a colonist has no
    /// standing at all before the clock has moved.</para>
    ///
    /// <para><b>Runs exactly once because <see cref="SimWorld.CurrentTick"/> only ever reads zero
    /// on the very first call.</b> No per-pawn flag is needed: the counter starts at zero, this
    /// system rolls when it sees zero, and it is incremented before the next call — including the
    /// resumed case, where a loaded save restores a non-zero tick and this never fires again, so
    /// a pawn's saved (and possibly since-earned) skill experience is never overwritten. A pawn
    /// spawned after tick zero — no such case exists yet — would keep the zero skills its
    /// constructor gives it; extending this to late arrivals is future work, not U37's.</para>
    /// </summary>
    public sealed class StartingSkillsSystem : IWorldSystem
    {
        readonly PawnContext _ctx;

        public StartingSkillsSystem(PawnContext ctx) { _ctx = ctx; }

        public string Name => "StartingSkills";

        // Before Needs and the job pipeline, so the very first tick a pawn is ever scanned for
        // work it already has the skills that tick's decisions should see.
        public TickPhase Phase => TickPhase.Pawns;
        public int Order => -1000;

        public void Tick(SimWorld world)
        {
            if (world.CurrentTick != 0) return;

            var pawns = _ctx.Pawns.All;
            // Each from its own seed since U40, which is the world's for every colonist the world
            // placed itself and the candidate's for one chosen on the select screen.
            // An animal has no skills to roll (design 29 §2). The draw is not made for it either,
            // which is safe: each pawn's draw is keyed on its own id and seed, so skipping one
            // cannot shift another's.
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i].IsPerson) pawns[i].RollStartingSkills();
        }
    }
}
