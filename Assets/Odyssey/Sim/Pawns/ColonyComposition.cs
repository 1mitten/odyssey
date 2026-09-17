#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

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
        /// <param name="edifices">Everything standing in a cell, writable, because finishing a
        /// building appends to it. The same list the designation grid reads.</param>
        /// <param name="construction">Handed back rather than taken, so that a caller cannot forget
        /// to supply one. See the remarks below — this is a fix for exactly that.</param>
        /// <param name="jobs">The job pipeline to run, when the caller wants to hold on to it for its
        /// per-def counters; a fresh one otherwise.</param>
        ///
        /// <remarks>
        /// <para><b>The construction grid is built here rather than passed in, and that is a
        /// correction.</b> It arrived as an optional argument defaulting to null, which meant every
        /// one of the twelve existing call sites went on compiling and silently built a colony that
        /// could not be given a build order: no intent handler for <c>PlaceBuilding</c>, no sites,
        /// and both work givers answering no for ever. The play scene was one of the twelve, so the
        /// feature worked in every test and in nothing a player could touch — the owner dragged a
        /// wall across the meadow, watched the preview draw, and watched nothing be built
        /// (2026-09-17).</para>
        ///
        /// <para>This file's own summary predicted it in as many words: <i>"a designation grid that
        /// one of them forgot to attach would be a player command that silently did nothing in that
        /// build"</i>. An optional parameter is how a caller forgets. Building the grid here means
        /// there is nothing to forget, and it is what "the list lives here, once" has to mean to be
        /// worth anything.</para>
        /// </remarks>
        public static SimWorldBuilder AddColony(this SimWorldBuilder builder, PawnContext pawns,
            DesignationGrid designations, SupportSystem support, NavGraph nav,
            List<PlacedEdifice> edifices, out ConstructionGrid construction, JobSystem? jobs = null)
        {
            // pawns.Cells, not a grid of its own: the context already carries the one cell grid the
            // colony is about, and taking a second would be an invitation to hand in two.
            // The edifice list becomes a saved, hashed component here and nowhere else, so every
            // colony gets it — the same argument the `out ConstructionGrid` above is built on.
            // Until 2026-09-17 a wall a colonist raised was in neither the save nor the hash;
            // `EdificeSaveSection` carries the measurement that found it.
            var edificeSave = new EdificeSaveSection(edifices);
            construction = new ConstructionGrid(pawns.Cells, edificeSave, pawns.Items, pawns.Pawns);
            pawns.Designations = designations;
            pawns.Construction = construction;
            JobSystem pipeline = jobs ?? new JobSystem(pawns);
            builder
                // The world itself, first: it is what everything below reads, and it ticks
                // nothing, so nothing else would ever have put it in the hash (OQ-50).
                .AddHashable(pawns.Cells)
                // What stands on the board, beside what the board is made of. `CellGrid` hashes
                // `Edifice[cell]`, which is only an index into this list — so without this line a
                // wooden wall and a stone wall in the same cell hash identically. Measured, not
                // supposed: `EdificeRoundTripTests.AWallsMaterialIsInTheStateHash`.
                .AddHashable(edificeSave)
                .AddSystem(_ => support)
                .AddSystem(_ => new NavigationSystem(nav, support))
                // Starting skills (U37), before Needs and the job pipeline for the same reason
                // they run: a colonist should not be scanned for work on the first tick it is
                // ever ticked with the zero skills its constructor gave it, when its rolled ones
                // are one order earlier in the same phase.
                .AddSystem(_ => new StartingSkillsSystem(pawns))
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
                // The world's own answer to "what is this cell", beside the pawn registry's
                // answer to "who is here". Every colony gets it, so a click is answered in any
                // build rather than the ones that remembered to attach the question.
                .AddSnapshotContributor(new CellDetailContributor(pawns.Cells, edifices))
                .AddIntentHandler(IntentKind.SetForbidden, pawns.Items.HandleSetForbidden)
                // The one command that names a colonist rather than only a cell. It belongs to the
                // pipeline because starting and ending jobs is what the pipeline is, and because a
                // second path into `StartJob` would be a second path out of it — which is where a
                // reservation leak comes from.
                .AddIntentHandler(IntentKind.ForceJob, pipeline.HandleForceJob)
                // The debug menu's two rows. Neither is player content — see the doc comments on
                // the intents themselves — so both live beside the ordinary handlers rather than in
                // a debug-only wiring path a real colony would not otherwise get.
                .AddIntentHandler(IntentKind.SpawnPawn, pawns.Pawns.HandleSpawnPawn)
                .AddIntentHandler(IntentKind.GiveResource, intent => pawns.Items.HandleGiveResource(intent, pawns.Cells));
            designations.Attach(builder);
            construction.Attach(builder);
            return builder;
        }
    }
}
