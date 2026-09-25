#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Growing;
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
            construction = new ConstructionGrid(
                pawns.Cells, edificeSave, pawns.Items, pawns.Pawns, support.Solver);
            pawns.Designations = designations;
            pawns.Construction = construction;
            // Every colony has a chunk grid, a headless one included: it is how an edit tells the
            // sky map which columns moved (design 43 §6). ColonyWorld.Build makes one; this is for
            // the fixtures that assemble a colony by hand.
            pawns.Chunks ??= new ChunkGrid(pawns.Size);
            // So a site can carry its detour in the navigation flags, and so raising a building
            // can ask who is standing in it. Set here because this is the one place that holds
            // both the graph and the grid.
            construction.Nav = nav;
            // Built here rather than passed in, for the same argument the construction grid's
            // `out` was: an optional growing-zone parameter is how a caller forgets one, and a
            // forgetful build is a paint tool that silently does nothing. Reached through
            // `pawns.Growing` by the sowing giver and the save.
            var growing = new GrowingZones(pawns.Cells, ContentPack.Plants(), pawns.Chunks);
            pawns.Growing = growing;
            // And the storage zones, for the same argument again: an optional one is how a caller
            // forgets, and a colony that forgot them would have a stockpile tool that silently did
            // nothing and a haul scan with nowhere to go. The items are told about the zones here
            // rather than constructing them, because the dependency runs one way — the zones
            // re-bucket the things, and the things ask one boolean back (`IZoneMembership`).
            var storage = new Storage.StorageZones(
                pawns.Cells, new Storage.StorageSettingsTable(pawns.Content), pawns.Items, pawns.Chunks);
            pawns.Storage = storage;
            pawns.Items.Membership = storage;
            // And the built stores, sharing the zones' own settings table rather than keeping a
            // second one: "what the colony accepts where" stays one table, one save section and
            // one walk of the hash, whether the store in question was painted or raised. It takes
            // the designations because a shelf being emptied is derived from the deconstruct order
            // standing on it, rather than from a flag that could disagree with the order.
            var units = new Storage.StorageUnits(
                pawns.Cells, edifices, storage.Settings, pawns.Items, designations);
            pawns.StorageUnits = units;
            // And back the other way, which is what makes SettingsAt one resolver rather than two:
            // the priority and filter intents name a cell, and that cell may be a painted zone's or
            // a raised shelf's. Without this the whole storage control silently does nothing over a
            // shelf — the panel opens and closes again, which is the shape of fault design 20 §8
            // records as "Assign did nothing, three times".
            storage.Units = units;
            // And the numbering, which both kinds of store share: the Inventory tab names shelves
            // and stockpiles side by side off the published rows (design 35).
            units.Zones = storage;
            // U29: the seam through which a job that edits the world says the structure changed.
            // Taken off the system rather than passed in beside it, so the solver a collapse is
            // computed from and the solver a wall marks dirty cannot be two different objects.
            pawns.Support = support.Solver;
            // And the other way: the system that finds a collapse needs the colony to drop things
            // into. Bound here because this is the one place that holds both (U29).
            support.Bind(pawns);
            var doors = new DoorSystem(pawns);
            pawns.Doors = doors;
            var enclosure = new World.EnclosureGrid(pawns.Cells, edifices);
            pawns.Enclosure = enclosure;
            // The thermal pass, after the enclosure it reads rooms from and after the items and
            // construction it reads sources through. Built here for the same argument as every
            // other seam on the context: an optional one is how a caller forgets it, and a
            // colony that forgot it would be a colony where nothing is ever cold.
            var temperature = new Temperature.TemperatureSystem(pawns, edifices, Worldgen.WorldContent.Climate);
            pawns.Temperature = temperature;
            // The sky (design 43), which writes the outdoor curve's weather term before the thermal
            // pass reads it: Order 35 against temperature's 50.
            var weather = new Weather.WeatherSystem(pawns, Worldgen.WorldContent.Weathers);
            pawns.Weather = weather;
            // And where it reaches: the one shelter rule (design 43 §6), read by pace, growth and
            // the animals. Derived, so it is neither saved nor hashed and needs no schedule slot.
            pawns.Sky = new World.SkyColumns(pawns.Cells, edifices, pawns.Chunks);
            // Power (design 32). Built here for the same argument again, and handed to the
            // construction grid because that is where a line order arrives: a colony that forgot
            // it would have a Power category whose every tool silently did nothing.
            var power = new Power.PowerGrid(pawns.Cells, edifices);
            pawns.Power = power;
            construction.Power = power;
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
                // After navigation, so it reads the flags this tick's edits produced: nobody is
                // left standing inside solid world, whatever put the world there.
                .AddSystem(_ => new TrappedPawnSystem(pawns))
                .AddSystem(_ => enclosure)
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
                // The fight's own pass (design 33 §5): order 25, after the jobs decide who swings
                // and before movement steps anybody. Built here and handed to the context, like the
                // doors, so the job drivers reach it through ctx.Combat.
                .AddSystem(_ =>
                {
                    var combat = new CombatSystem(pawns, pipeline);
                    pawns.Combat = combat;
                    // Whoever listens to the fight's hooks (design 33 §5), registered in one fixed
                    // order in one lane-owned file so a lane adding a listener edits no spine.
                    CombatListeners.Register(pawns, pipeline);
                    return combat;
                })
                .AddSystem(_ => new MovementSystem(pawns))
                // The crops grow after the world has moved; Order 40 puts the pass there whatever
                // line of this chain it sits on, which is the whole point of the schedule.
                .AddSystem(_ => new PlantGrowthSystem(pawns, growing))
                .AddSystem(_ => doors)
                // The thermal pass, beside the other world systems: Order 50 puts it after the
                // enclosure solve (30) whatever line of this chain it sits on.
                .AddSystem(_ => temperature)
                .AddSystem(_ => weather)
                .AddSnapshotContributor(weather)
                .AddIntentHandler(IntentKind.DebugSetWeather, weather.HandleForce)
                // The burn, and the lazy solve behind it. Order 45 puts it before the thermal pass
                // (50), which asks it for heat on the same tick.
                .AddSystem(_ => power)
                .AddSnapshotContributor(power)
                .AddTickable(_ => new SkillSystem(pawns))
                .AddTickable(_ => pawns.Pawns)
                // The dead and the struck buildings (design 33 §5): hashed only while either holds
                // anything, so their registration moves no golden. Beside the pawns because the
                // corpses are what the pawns become.
                .AddHashable(pawns.Corpses)
                .AddHashable(pawns.EdificeDamage)
                .AddSnapshotContributor(pawns.Pawns)
                .AddSnapshotContributor(pawns.Corpses)
                // The telling of every fight, for presentation: never saved, never hashed.
                .AddSnapshotContributor(pawns.CombatLog)
                // The struck buildings and which edifices are targets (design 33 §13i): neither
                // saved nor hashed, a report of the damage store and of the content.
                .AddSnapshotContributor(new EdificeDamageContributor(pawns))
                // The world's own answer to "what is this cell", beside the pawn registry's
                // answer to "who is here". Every colony gets it, so a click is answered in any
                // build rather than the ones that remembered to attach the question.
                .AddSnapshotContributor(new CellDetailContributor(
                    pawns.Cells, edifices, growing, enclosure, storage, units, pawns.Items,
                    temperature))
                .AddIntentHandler(IntentKind.SetForbidden, pawns.Items.HandleSetForbidden)
                // The one command that names a colonist rather than only a cell. It belongs to the
                // pipeline because starting and ending jobs is what the pipeline is, and because a
                // second path into `StartJob` would be a second path out of it — which is where a
                // reservation leak comes from.
                .AddIntentHandler(IntentKind.ForceJob, pipeline.HandleForceJob)
                // The draft and its orders (design 33 §2d), on the pipeline for the same reason.
                .AddIntentHandler(IntentKind.SetDrafted, pipeline.HandleSetDrafted)
                .AddIntentHandler(IntentKind.OrderMove, pipeline.HandleOrderMove)
                // The fight's three orders (design 33 §5), on the pipeline for the same reason.
                // Registered from the contracts step so a command is never unhandled; each
                // refuses until its lane writes it.
                .AddIntentHandler(IntentKind.OrderAttack, pipeline.HandleOrderAttack)
                .AddIntentHandler(IntentKind.OrderEquip, pipeline.HandleOrderEquip)
                .AddIntentHandler(IntentKind.OrderRescue, pipeline.HandleOrderRescue)
                // A colonist's response to danger (design 33 §18c), on the pipeline because a new
                // setting may end a fight or a flight she started under the old one.
                .AddIntentHandler(IntentKind.SetHostilityResponse, pipeline.HandleSetHostilityResponse)
                .AddIntentHandler(IntentKind.DebugHealth, pawns.Pawns.HandleDebugHealth)
                .AddIntentHandler(IntentKind.OrderTend, pipeline.HandleOrderTend)
                // The Work tab's one command (design 27). It belongs to the registry because a
                // priority is a field on a pawn and the registry is the one owner of those; the
                // job pipeline only ever reads it.
                .AddIntentHandler(IntentKind.SetWorkPriority, pawns.Pawns.HandleSetWorkPriority)
                // The other half of the same panel: what they do, and when.
                .AddIntentHandler(IntentKind.SetScheduleBlock, pawns.Pawns.HandleSetScheduleBlock)
                // The debug menu's two rows. Neither is player content — see the doc comments on
                // the intents themselves — so both live beside the ordinary handlers rather than in
                // a debug-only wiring path a real colony would not otherwise get.
                .AddIntentHandler(IntentKind.SpawnPawn, pawns.Pawns.HandleSpawnPawn)
                .AddIntentHandler(IntentKind.DebugArmColonists, pawns.Pawns.HandleDebugArmColonists)
                // Jumps always fail (design 46 §6): a switch on the context, read by the one roll.
                .AddIntentHandler(IntentKind.DebugJumpsFail, intent =>
                {
                    bool on = intent.A != 0;
                    if (pawns.DebugJumpsAlwaysFail == on) return IntentRejection.AlreadyInThatState;
                    pawns.DebugJumpsAlwaysFail = on;
                    return IntentRejection.None;
                })
                .AddIntentHandler(IntentKind.GiveResource, intent => pawns.Items.HandleGiveResource(intent, pawns.Cells))
                // The two power commands that are not a build (design 32): taking a line up, and
                // throwing a building's switch. Both belong to the power grid, the one owner of both.
                .AddIntentHandler(IntentKind.RemoveConduit, intent =>
                    pawns.Cells.Size.Contains(intent.Cell)
                        ? power.MarkRemoval(pawns.Cells.Size.Index(intent.Cell))
                        : IntentRejection.OutOfBounds)
                .AddIntentHandler(IntentKind.CancelConduit, intent =>
                    pawns.Cells.Size.Contains(intent.Cell)
                        ? (power.CancelAt(pawns.Cells.Size.Index(intent.Cell))
                            ? IntentRejection.None : IntentRejection.AlreadyInThatState)
                        : IntentRejection.OutOfBounds)
                .AddIntentHandler(IntentKind.SetPowerSwitch, intent =>
                    pawns.Cells.Size.Contains(intent.Cell)
                        ? power.SetSwitch(pawns.Cells.Size.Index(intent.Cell), intent.A != 0)
                        : IntentRejection.OutOfBounds);
            designations.Attach(builder);
            construction.Attach(builder);

            // The events (design 23): the ledger, whatever is in the air, and the one command that
            // fires an incident on demand. Every colony gets them for the reason every colony gets
            // the debug intents above — a world that could not be sent an event is a world the
            // debug menu cannot test, and the storyteller, when it exists, will need the same seam.
            var incidents = new Events.Incidents(pawns, ContentPack.Incidents());
            pawns.Incidents = incidents;
            incidents.Attach(builder);
            growing.Attach(builder);
            storage.Attach(builder);
            units.Attach(builder);
            return builder;
        }
    }
}
