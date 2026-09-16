#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The colony, added to a world builder in the one order everything agrees on.
    ///
    /// <see cref="ColonyWorld"/> builds the headless world this way, and so do the bootstrap and
    /// the screenshot harness, which each also attach a renderer mirror. The wiring had drifted
    /// between them once already (one built the support system with a chunk grid and the others
    /// without), and a designation grid that one of them forgot to attach would be a player
    /// command that silently did nothing in that build. So the list lives here, once.
    /// </summary>
    public static class ColonyComposition
    {
        /// <summary>
        /// Support before navigation, because a collapse changes what is walkable; needs before
        /// jobs, because a hungry pawn picks a different job; jobs before movement. Skill decay
        /// on the Long tick group. Then the pawns and the designations as hashed, saved and
        /// published state, and the player commands the colony owns: forbid, designate, cancel.
        ///
        /// <para>What is <em>not</em> listed here is the work givers. A giver in the simulation
        /// assembly joins the scan by existing (<see cref="WorkGiverRegistry"/>); one from outside
        /// joins through <see cref="SimWorldBuilder.AddWorkGiver"/> and is collected below. Adding
        /// a kind of work therefore edits no file that anything else owns, which is the whole of
        /// OQ-44.</para>
        /// </summary>
        /// <param name="jobs">The job pipeline to run, when the caller wants to hold on to it for its
        /// per-def counters; a fresh one otherwise.</param>
        public static SimWorldBuilder AddColony(this SimWorldBuilder builder, PawnContext pawns,
            DesignationGrid designations, SupportSystem support, NavGraph nav, JobSystem? jobs = null)
        {
            pawns.Designations = designations;
            JobSystem pipeline = jobs ?? new JobSystem(pawns);
            builder
                .AddSystem(_ => support)
                .AddSystem(_ => new NavigationSystem(nav, support))
                .AddSystem(_ => new NeedsSystem(pawns))
                // Inside the lambda, not before it: the factory runs during Build(), so a giver
                // registered after this call is still picked up. Outside it, AddColony would have
                // had to be the last call on the builder, which is precisely the kind of ordering
                // rule that holds until somebody writes the lines the other way round.
                .AddSystem(_ =>
                {
                    pipeline.AddGivers(builder.WorkGivers);
                    return pipeline;
                })
                .AddSystem(_ => new MovementSystem(pawns))
                .AddTickable(_ => new SkillSystem(pawns))
                .AddTickable(_ => pawns.Pawns)
                .AddSnapshotContributor(pawns.Pawns)
                .AddIntentHandler(IntentKind.SetForbidden, pawns.Items.HandleSetForbidden);
            designations.Attach(builder);
            return builder;
        }
    }
}
