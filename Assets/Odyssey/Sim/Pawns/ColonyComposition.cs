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
        /// jobs, because a hungry pawn picks a different job; jobs before movement. Then the
        /// pawns and the designations as hashed, saved and published state, and the player
        /// commands the colony owns: forbid, designate, cancel.
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
                .AddSystem(_ => pipeline)
                .AddSystem(_ => new MovementSystem(pawns))
                .AddTickable(_ => pawns.Pawns)
                .AddSnapshotContributor(pawns.Pawns)
                .AddIntentHandler(IntentKind.SetForbidden, pawns.Items.HandleSetForbidden);
            designations.Attach(builder);
            return builder;
        }
    }
}
