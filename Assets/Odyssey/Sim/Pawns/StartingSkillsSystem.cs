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
    /// <para><b>The first tick this system sees, not tick zero</b> (fixed 2026-09-24, design 41
    /// §6.7). It used to fire when <see cref="SimWorld.CurrentTick"/> read zero, and the played scene
    /// starts its clock at noon (<c>ColonyRequest.StartTick</c>), so <b>no colonist in any played
    /// game had starting skills</b> while every headless run and golden, which start at zero, did.
    /// Nothing reported it: the setup page showed the card's skills and the colony simply did not
    /// have them. Found by the draw's end-to-end PlayMode test.</para>
    ///
    /// <para><b>Firing again after a load is safe, and that is why a flag that is not saved will
    /// do.</b> A fresh system after a load fires once more, but the roll writes only into a skill
    /// still at zero experience and is a pure function of the pawn's seed, id and profile — so a
    /// skill rolled at zero is rolled at zero again, and one with any experience in it is left
    /// alone (<c>DrawTests.TheStartingRollNeverOverwritesEarnedExperienceWhenItFiresAgainAfterALoad</c>).
    /// A save from a played game before the fix, whose colonists were never rolled, gets their
    /// starting skills on its first tick. A colonist spawned later rolls at the spawn.</para>
    /// </summary>
    public sealed class StartingSkillsSystem : IWorldSystem
    {
        readonly PawnContext _ctx;

        public StartingSkillsSystem(PawnContext ctx) { _ctx = ctx; }

        /// <summary>Whether this system has had its first tick. Deliberately not saved: see the summary.</summary>
        bool _rolled;

        public string Name => "StartingSkills";

        // Before Needs and the job pipeline, so the very first tick a pawn is ever scanned for
        // work it already has the skills that tick's decisions should see.
        public TickPhase Phase => TickPhase.Pawns;
        public int Order => -1000;

        public void Tick(SimWorld world)
        {
            if (_rolled) return;
            _rolled = true;

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
