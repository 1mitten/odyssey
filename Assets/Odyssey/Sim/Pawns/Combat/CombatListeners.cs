#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Who hears the fight's hooks (<see cref="CombatHooks"/>), registered once per colony by
    /// <c>ColonyComposition</c>, in the order written here — which is the order they are called in.
    ///
    /// <para><b>One file, so a lane adding a listener edits no spine.</b> In Phase 2 of
    /// <c>docs/plans/combat.md</c> this is <b>lane D's</b> (drop the weapon on a death); in Phase 4
    /// the C4 and C5 lanes append theirs after it, one at a time, in that order
    /// (<c>docs/plans/combat-contracts.md</c>).</para>
    /// </summary>
    public static class CombatListeners
    {
        public static void Register(PawnContext ctx, JobSystem jobs)
        {
            // C3 (lane D): the dead let go of their weapon; the downed keep it. First, so that a
            // later listener hearing the same death sees the weapon already on the ground.
            ctx.CombatHooks.Add(new WeaponDropListener(ctx));

            // C5 (friendly fire, design 33 §12): a colonist hurt by a colonist remembers it, and
            // every colonist feels a colonist's death. After the weapon drop, so a death is mourned
            // with the weapon already on the ground. C4 (rescue) needed no listener.
            ctx.CombatHooks.Add(new FriendlyFireListener(ctx));

            // The storyteller (design 68 §5): a colonist lost eases the tension. Last, because it
            // only writes numbers of its own and no other listener reads them.
            ctx.CombatHooks.Add(new Events.StorytellerCombatListener(ctx));
        }
    }
}
