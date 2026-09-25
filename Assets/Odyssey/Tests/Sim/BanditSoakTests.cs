#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A bandit a day for ten days (design 33 §6A), on the soak's own board and colony: the
    /// fight left running unattended, with the colony's ordinary life around it. What it holds is
    /// what a two-minute test cannot see — that nothing throws, that the fight's invariants hold at
    /// every sample (a pawn past its death line never outlives its tick, a downed pawn is on
    /// <c>Job_Downed</c>, the reservation table agrees with the pawns), that every death left
    /// exactly one corpse, that the fight actually happened, and that a save taken in the middle of
    /// it resumes on the same hash a day later.
    ///
    /// <para><b>Fists only until lane D's merge</b> arms the bandit with its machete; the
    /// integrator's run after that merge is the one with steel in it. Nothing here rescues a
    /// downed colonist (C4), so the colony wears down over the ten days, which is the honest
    /// picture of C2 alone.</para>
    /// </summary>
    public class BanditSoakTests
    {
        static readonly GridSize PlaySize = new GridSize(120, 120, 16);
        const int Day = 60_000;
        const int SampleEvery = 500;

        [Test, Category("Long")]
        public void ABanditADayForTenDays()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.stockpileCells = 9;
            ColonyWorld colony = ColonyWorld.Build(PlaySize, 1u, scenario);
            var hooks = new CombatFixture.HookCounter();
            colony.Pawns.CombatHooks.Add(hooks);
            var rules = new CombatFixture.RecordingRules();
            colony.Pawns.MeleeRules = rules;

            int spawned = 0, recovered = 0;
            var ids = new System.Collections.Generic.List<int>();
            ulong resumed = 0, original = 0;
            byte[]? midway = null;
            var tape = new CombatFixture.Tape();
            var watch = Stopwatch.StartNew();

            for (int day = 0; day < 10; day++)
            {
                // A quarter into the day, twenty cells out along a heading that turns each day.
                colony.World.Tick((Day / 4 - colony.World.CurrentTick % Day + Day) % Day);
                int dx = (day % 4 < 2 ? 1 : -1) * 20, dz = (day % 2 == 0 ? 1 : -1) * 20;
                CellRef at = colony.Start;
                int before = colony.Pawns.Pawns.Count;
                colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn,
                    new CellRef(at.X + dx, at.Z + dz, at.Y), PawnKindIndex.Bandit));
                colony.World.Tick();
                if (colony.Pawns.Pawns.Count == before + 1)
                {
                    spawned++;
                    ids.Add(colony.Pawns.Pawns.All[colony.Pawns.Pawns.Count - 1].Id.Value);
                }

                if (day == 5)
                {
                    midway = colony.Save();
                }

                for (int t = colony.World.CurrentTick % Day; t < Day - 1; t += SampleEvery)
                {
                    int step = System.Math.Min(SampleEvery, Day - 1 - t);
                    for (int s = 0; s < step; s++)
                    {
                        colony.World.Tick();
                        tape.Read(colony);
                    }
                    Invariants(colony, day);
                }

                if (day == 5)
                {
                    // A day on from the save: the loaded world must have lived the same day.
                    var loaded = ColonyWorld.Build(PlaySize, 1u, scenario);
                    loaded.Load(midway!);
                    loaded.World.Tick(colony.World.CurrentTick - loaded.World.CurrentTick);
                    resumed = loaded.World.ComputeStateHash().Value;
                    original = colony.World.ComputeStateHash().Value;
                }
            }
            watch.Stop();

            recovered = tape.Of(CombatEventKind.Recovered).Count;
            int standing = 0, downed = 0, bandits = 0;
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            {
                if (pawn.IsHostile) bandits++;
                if (pawn.Downed) downed++; else standing++;
            }

            // Where every bandit went (design 33 §17): still on the board, dead, or off the edge. A
            // bandit that left is a ledger entry, a theft or an empty-handed leaving, and nothing
            // else takes one off the board.
            int dead = 0, departed = 0;
            foreach (int id in ids)
            {
                if (colony.Pawns.Pawns.Get(new PawnId(id)) != null) continue;
                bool corpse = false;
                for (int c = 0; c < colony.Pawns.Corpses.Count; c++) corpse |= colony.Pawns.Corpses[c].Pawn == id;
                if (corpse) dead++; else departed++;
            }
            var ledger = colony.Incidents.Ledger;
            int thefts = ledger.Fires(IncidentHandle.Theft), empty = ledger.Fires(IncidentHandle.BanditLeft);

            TestContext.WriteLine(
                $"bandit soak: {watch.Elapsed.TotalSeconds:F1} s wall; {spawned} bandits spawned, {bandits} left on the board; " +
                $"{dead} killed, {thefts} left with a stack and {empty} empty-handed (design 33 §17); " +
                $"{rules.Swings.Count} swings resolved, {tape.Of(CombatEventKind.Hit).Count} hits, " +
                $"{hooks.DownedCount} downed, {hooks.DiedCount} died, {recovered} got up; " +
                $"{tape.Of(CombatEventKind.Demolished).Count} buildings broken down (design 33 §14b); " +
                $"{standing} standing and {downed} down at the end; jobs failed — attack " +
                $"{colony.Jobs.FailedOf(JobIndex.AttackMelee)}, downed {colony.Jobs.FailedOf(JobIndex.Downed)}, " +
                $"flee {colony.Jobs.FailedOf(JobIndex.Flee)}");

            Assert.That(spawned, Is.EqualTo(10), "a bandit could not be spawned");
            Assert.That(rules.Swings.Count, Is.GreaterThan(100), "ten bandits and hardly a blow: the fight never happened");
            Assert.That(hooks.DownedCount, Is.GreaterThan(0), "ten days of fighting and nobody went down");
            Assert.That(colony.Pawns.Corpses.Count, Is.EqualTo(hooks.DiedCount), "a death without its corpse, or a corpse without a death");
            Assert.That(colony.Jobs.FailedOf(JobIndex.Downed), Is.EqualTo(0), "Job_Downed failed: somebody got up by the wrong door");
            Assert.That(resumed, Is.EqualTo(original), "a save taken mid-fight did not resume the same");
            Assert.That(departed, Is.EqualTo(thefts + empty), "a bandit left the board without the ledger saying so, or the ledger says one left that did not");
            Assert.That(bandits + dead + departed, Is.EqualTo(spawned), "a bandit is unaccounted for");
        }

        // ------------------------------------------------------------------ the gate (C7)

        const int Hour = Day / 24;

        /// <summary>
        /// The raids of the gate: a day and a party. One bandit or three — the debug menu's two rows
        /// (design 33 §9i) — a raid every day or two, thirteen bandits in seven raids. There is no
        /// storyteller (owner's call), so the schedule is the test's, and it is fixed so that three
        /// seeds are three colonies meeting the same pressure.
        /// </summary>
        static readonly (int Day, int Party)[] Raids = { (0, 1), (1, 3), (3, 1), (4, 3), (6, 1), (7, 3), (9, 1) };

        /// <summary>
        /// The raid a save is taken in, at its first swing: day one's three, the one fight every seed
        /// has. By day four seed 1's colony is all down and its hut broken, so that party only steals
        /// and never swings.
        /// </summary>
        const int SavedRaid = 1;

        /// <summary>
        /// The longest a bandit on <c>Job_AttackMelee</c> may go at a <b>building</b> neither moving
        /// nor starting a swing. Every attack nobody ordered thinks again at
        /// <see cref="CombatDef.rechooseTicks"/> (300), and since §19b one whose every side is held ends
        /// at once, so the owner's "said they were fighting but kinda stood around" is at most a
        /// re-choice plus the longest swing cooldown (2.4 s, 144 ticks) and the hop up a step §19b
        /// measured at 183. 500 covers that with room; before §19b it was 3,245.
        /// </summary>
        const int BuildingStallBound = 500;

        /// <summary>
        /// The longest a downed colonist may lie out of a bed while one could be carried to it: a free
        /// bed she can reach, and a colonist on her feet, undrafted, unbroken and awake who can reach
        /// her, with nothing hostile standing. Rescue is emergency work (design 33 §11b) and is taken
        /// at the rescuer's next think; the longest ordinary job a waking colonist holds in this colony
        /// is a haul or a meal, well under an hour. Sampled hourly, so two hours is one missed think
        /// and a walk, and a rescue that never comes fails it on the third sample.
        /// </summary>
        const int RescueBound = 2 * Hour;

        /// <summary>
        /// The ten-day gate with hostiles (design 33 §C7). The soak's board and colony, armed through
        /// the debug menu's own <c>DebugArmColonists</c>, with a hut of wooden walls and a door beside
        /// the beds, and seven raids. The invariants are asked every in-game hour; a lockstep twin of
        /// the same seed runs beside it and must hash the same every hour; and a save taken at the first
        /// swing of day four's raid must resume to the same hash a day later.
        /// </summary>
        [Test, Category("Long")]
        [TestCase(1u)]
        [TestCase(2u)]
        [TestCase(3u)]
        public void TheGateWithRaids(uint seed)
        {
            var run = new Gate(seed);
            var twin = new Gate(seed);
            ColonyWorld colony = run.Colony;

            var ids = new List<int>();
            var savedRaid = new HashSet<int>();
            byte[]? midway = null;
            int savedAt = -1, raidSpawnedAt = -1;
            ulong resumed = 0, original = 0;
            var stall = new Dictionary<int, (int Cell, int Swing, int Since)>();
            var stale = new Dictionary<int, int>();
            var rescuable = new Dictionary<int, int>();
            int longestAtBuilding = 0, longestAtPawn = 0, longestStale = 0, longestUnrescued = 0, raidsHeld = 0;
            var watch = Stopwatch.StartNew();

            int next = 0;
            for (int tick = colony.World.CurrentTick; tick < 10 * Day; tick = colony.World.CurrentTick)
            {
                // A raid lands a quarter into its day, twenty cells out, on a heading that turns.
                if (next < Raids.Length && tick == Raids[next].Day * Day + Day / 4)
                {
                    // By id, not by index: a thief leaving in the same tick shifts the list.
                    var known = new HashSet<int>(ids);
                    run.Raid(next);
                    twin.Raid(next);
                    foreach (Pawn pawn in colony.Pawns.Pawns.All)
                    {
                        if (!pawn.IsHostile || known.Contains(pawn.Id.Value)) continue;
                        ids.Add(pawn.Id.Value);
                        if (next == SavedRaid) savedRaid.Add(pawn.Id.Value);
                    }
                    if (next == SavedRaid) raidSpawnedAt = colony.World.CurrentTick;
                    raidsHeld++;
                    next++;
                    continue;
                }

                run.Step();
                twin.Step();
                tick = colony.World.CurrentTick;

                // The save, at the first swing of the saved raid's party — at a colonist or at a
                // building, whichever it reaches first: mid-fight, with a blow in the air.
                if (midway == null && raidSpawnedAt >= 0 && run.Tape.Events.Count > 0)
                {
                    CombatEventView last = run.Tape.Events[run.Tape.Events.Count - 1];
                    if (last.Tick >= raidSpawnedAt && last.Kind == CombatEventKind.Swing && savedRaid.Contains(last.Attacker.Value))
                    {
                        midway = colony.Save();
                        savedAt = tick;
                    }
                }
                if (midway != null && resumed == 0 && tick == savedAt + Day)
                {
                    var loaded = ColonyWorld.Build(PlaySize, seed, Gate.Scenario());
                    loaded.Load(midway);
                    loaded.World.Tick(tick - loaded.World.CurrentTick);
                    resumed = loaded.World.ComputeStateHash().Value;
                    original = colony.World.ComputeStateHash().Value;
                }

                Watch(colony, stall, stale, ref longestAtBuilding, ref longestAtPawn, ref longestStale);

                if (tick % Hour == 0)
                {
                    int day = tick / Day;
                    Invariants(colony, day);
                    Rescues(colony, rescuable, ref longestUnrescued);
                    Assert.That(twin.Colony.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value),
                        $"seed {seed}, day {day}, tick {tick}: the same seed and the same raids came to two hashes");
                }
            }
            watch.Stop();

            // Where every bandit went (design 33 §17), as the soak above accounts for them.
            int onBoard = 0, dead = 0, departed = 0;
            foreach (int id in ids)
            {
                if (colony.Pawns.Pawns.Get(new PawnId(id)) != null) { onBoard++; continue; }
                bool corpse = false;
                for (int c = 0; c < colony.Pawns.Corpses.Count; c++) corpse |= colony.Pawns.Corpses[c].Pawn == id;
                if (corpse) dead++; else departed++;
            }
            int colonistsStanding = 0, colonistsDown = 0, colonistsDead = 0;
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                if (pawn.IsColonist) { if (pawn.Downed) colonistsDown++; else colonistsStanding++; }
            for (int c = 0; c < colony.Pawns.Corpses.Count; c++)
                if (colony.Pawns.Corpses[c].Kind == PawnKindIndex.Colonist) colonistsDead++;
            var ledger = colony.Incidents.Ledger;
            int thefts = ledger.Fires(IncidentHandle.Theft), empty = ledger.Fires(IncidentHandle.BanditLeft);
            int freed = 0;
            foreach (IWorldSystem system in colony.World.Systems.WorldSystems)
                if (system is TrappedPawnSystem trapped) freed = trapped.Freed;

            TestContext.WriteLine(
                $"gate seed {seed}: {watch.Elapsed.TotalSeconds:F1} s wall for the run and its twin; " +
                $"{raidsHeld} raids, {ids.Count} bandits, {run.Drafts} drafts given; {run.Rules.Swings.Count} swings at pawns, " +
                $"{run.Tape.Of(CombatEventKind.Hit).Count} blows landed on pawns and buildings; " +
                $"{run.Hooks.DownedCount} downed ({run.DownedColonists} colonists), {run.Hooks.DiedCount} died, " +
                $"{run.Tape.Of(CombatEventKind.Recovered).Count} got up; {colony.Jobs.CompletedOf(JobIndex.Rescue)} rescues " +
                $"(failed {colony.Jobs.FailedOf(JobIndex.Rescue)}); {run.Tape.Of(CombatEventKind.Demolished).Count} buildings broken " +
                $"of {run.Built} raised; {thefts} thefts, {empty} left empty-handed, {dead} bandits killed, {onBoard} still on the board; " +
                $"colonists at day ten: {colonistsStanding} standing, {colonistsDown} down, {colonistsDead} dead");
            TestContext.WriteLine(
                $"  longest on Fighting without a step or a swing — at a building {longestAtBuilding} ticks, at a pawn {longestAtPawn}; " +
                $"longest on a target already gone {longestStale}; longest rescuable and not rescued {longestUnrescued}; " +
                $"freed from a wall {freed}; save at tick {savedAt}, final hash {colony.World.ComputeStateHash().Value:x16}");

            Assert.That(ids.Count, Is.EqualTo(13), "a bandit could not be spawned");
            Assert.That(run.Rules.Swings.Count, Is.GreaterThan(100), "seven raids and hardly a blow: the fight never happened");
            Assert.That(run.Hooks.DownedCount, Is.GreaterThan(0), "ten days of raids and nobody went down");
            Assert.That(colony.Pawns.Corpses.Count, Is.EqualTo(run.Hooks.DiedCount), "a death without its corpse, or a corpse without a death");
            Assert.That(colonistsDead, Is.EqualTo(0), "a colonist died, and nobody ordered a death: an unordered fight ends in downs (design 33 §3)");
            Assert.That(colony.Jobs.FailedOf(JobIndex.Downed), Is.EqualTo(0), "Job_Downed failed: somebody got up by the wrong door");
            Assert.That(departed, Is.EqualTo(thefts + empty), "a bandit left the board without the ledger saying so, or the reverse");
            Assert.That(onBoard + dead + departed, Is.EqualTo(ids.Count), "a bandit is unaccounted for");
            Assert.That(midway, Is.Not.Null, "the saved raid never swung, so nothing was saved mid-fight");
            Assert.That(resumed, Is.EqualTo(original), "a save taken mid-raid did not resume the same");
            Assert.That(longestStale, Is.LessThanOrEqualTo(1), "an attacker, bandit or colonist, stayed on Fighting at a target already gone while nothing held its job");
            // The same stand at a colonist is printed and not held to a bound: a bandit that can get
            // no side of her queues a ring back (design 33 §7c), which §19d measured at 2,506 ticks and
            // left, by design, for the owner. Read 95 on all three seeds when this was written.
            Assert.That(longestAtBuilding, Is.LessThanOrEqualTo(BuildingStallBound),
                "a bandit stood on Fighting at a building, neither stepping nor swinging (design 33 §19)");
            Assert.That(longestUnrescued, Is.LessThanOrEqualTo(RescueBound),
                "a downed colonist lay out of bed with a bed free and somebody free to carry her (design 33 §11)");
        }

        /// <summary>
        /// Every tick: how long each bandit in an attack has gone without a step or a swing, and how
        /// long any attacker has stood on a target that has gone. Scales with the pawns.
        /// </summary>
        static void Watch(ColonyWorld colony, Dictionary<int, (int Cell, int Swing, int Since)> stall,
                          Dictionary<int, int> stale, ref int atBuilding, ref int atPawn, ref int longestStale)
        {
            PawnContext ctx = colony.Pawns;
            int tick = colony.World.CurrentTick;
            foreach (Pawn pawn in ctx.Pawns.All)
            {
                int id = pawn.Id.Value;
                if (!Melee.IsInAnAttack(pawn))
                {
                    stall.Remove(id);
                    stale.Remove(id);
                    continue;
                }

                bool building = pawn.CombatTarget == 0;
                bool gone;
                if (building) gone = !BuildingTargets.TryStanding(ctx, pawn.CurrentJob!.DestCell, out _);
                else
                {
                    Pawn? target = ctx.Pawns.Get(new PawnId(pawn.CombatTarget));
                    gone = target == null || Melee.IsDead(target)
                        || (target.Downed && pawn.CurrentJob!.DestCell != AttackMeleeJobDriver.ToTheDeath);
                }
                // Only ticks the driver is free to notice: CurrentTick is the tick the next job loop
                // runs, and a driver it holds — stunned, knocked down, landing a step — neither ticks
                // nor ends its job (design 33 §5c: a stun is a pause, not an interrupt). Counting
                // those read a stunned colonist whose target went down as a 109-tick stall, on one
                // seed in three whenever the random stream moved (2026-09-25). The rule is the job
                // system's own, not a copy of it.
                int held = stale.TryGetValue(id, out int s) ? s : 0;
                int run = !gone ? 0 : JobSystem.HoldsDriver(pawn, tick) ? held : held + 1;
                stale[id] = run;
                if (run > longestStale) longestStale = run;

                if (!pawn.IsHostile) continue;
                if (!stall.TryGetValue(id, out var was) || was.Cell != pawn.Cell || was.Swing != pawn.NextSwingTick)
                {
                    stall[id] = (pawn.Cell, pawn.NextSwingTick, tick);
                    continue;
                }
                int stood = tick - was.Since;
                if (building) { if (stood > atBuilding) atBuilding = stood; }
                else if (stood > atPawn) atPawn = stood;
            }
        }

        /// <summary>
        /// Hourly: a downed colonist out of bed who could be carried to one — nothing hostile standing,
        /// a free bed, and a colonist free to carry her — is timed until she is in somebody's arms, in a
        /// bed, or no longer rescuable.
        /// </summary>
        static void Rescues(ColonyWorld colony, Dictionary<int, int> since, ref int longest)
        {
            PawnContext ctx = colony.Pawns;
            int tick = colony.World.CurrentTick;
            bool hostile = Melee.AnythingHostile(ctx);
            foreach (Pawn patient in ctx.Pawns.All)
            {
                int id = patient.Id.Value;
                if (hostile || !RescueRules.NeedsRescue(patient, ctx) || !SomebodyCouldCarry(ctx, patient))
                {
                    since.Remove(id);
                    continue;
                }
                if (!since.TryGetValue(id, out int from)) since[id] = from = tick;
                if (tick - from > longest) longest = tick - from;
                if (tick - from > RescueBound)
                    Assert.Fail($"tick {tick}: colonist {id} has lain rescuable at {ctx.Size.FromIndex(patient.Cell)} " +
                                $"since tick {from}, and nobody came. {WhoCouldCarry(ctx, patient)}");
            }
        }

        /// <summary>Every colonist on her feet, and what she is doing instead: the failure's own evidence.</summary>
        static string WhoCouldCarry(PawnContext ctx, Pawn patient)
        {
            var said = new System.Text.StringBuilder();
            foreach (Pawn pawn in ctx.Pawns.All)
            {
                if (!pawn.IsColonist || pawn.Downed) continue;
                said.Append($"[{pawn.Id.Value}: job {pawn.CurrentJob?.DefIndex}, drafted {pawn.Drafted}, broken {pawn.IsBroken}, " +
                            $"asleep {pawn.Asleep}, reach {ctx.Reachable(pawn, patient.Cell, RescueRules.Mode)}, " +
                            $"bed {RescueRules.BedFor(patient, pawn, ctx)}, claim free {ctx.Reservations.CanReserve(pawn.Id, RescueRules.PatientKey(patient))}] ");
            }
            return said.ToString();
        }

        static bool SomebodyCouldCarry(PawnContext ctx, Pawn patient)
        {
            foreach (Pawn pawn in ctx.Pawns.All)
            {
                if (!pawn.IsColonist || pawn.Downed || pawn.Drafted || pawn.IsBroken || pawn.Asleep || pawn.CarriedBy != 0) continue;
                // Already carrying somebody else: one rescuer takes the wounded one at a time, and
                // the rest wait their turn (seed 1 first read 12,500 ticks here, one rescuer and four down).
                if (pawn.CurrentJob?.DefIndex == JobIndex.Rescue) continue;
                if (!ctx.Reachable(pawn, patient.Cell, RescueRules.Mode)) continue;
                if (RescueRules.BedFor(patient, pawn, ctx) >= 0) return true;
            }
            return false;
        }

        /// <summary>One colony of the gate, with what the gate reads off it.</summary>
        sealed class Gate
        {
            public readonly ColonyWorld Colony;
            public readonly CombatFixture.RecordingRules Rules = new CombatFixture.RecordingRules();
            public readonly CombatFixture.HookCounter Hooks = new CombatFixture.HookCounter();
            public readonly CombatFixture.Tape Tape = new CombatFixture.Tape();
            public readonly int Built;
            readonly HashSet<int> _downedColonists = new HashSet<int>();

            /// <summary>Distinct colonists who went down at least once.</summary>
            public int DownedColonists => _downedColonists.Count;

            public static ScenarioDef Scenario()
            {
                ScenarioDef scenario = ScenarioDef.Bare();
                scenario.stockpileCells = 9;
                return scenario;
            }

            public Gate(uint seed)
            {
                Colony = ColonyWorld.Build(PlaySize, seed, Scenario());
                Colony.Pawns.CombatHooks.Add(Hooks);
                Colony.Pawns.MeleeRules = Rules;
                Colony.World.Tick();

                Assert.That(Colony.World.Intents.Submit(new Intent(IntentKind.DebugArmColonists, default)), Is.True);
                Colony.World.Tick();
                int armed = 0;
                foreach (Pawn pawn in Colony.Pawns.Pawns.All) if (pawn.IsColonist && pawn.EquippedItem != 0) armed++;
                Assert.That(armed, Is.EqualTo(5), "the debug menu's arming did not arm the colony");

                Built = Hut();
                Colony.World.Tick();
            }

            /// <summary>
            /// A hut: a five-by-five ring of wooden walls with a door on its near side, eight cells off
            /// the start in the first direction where the door and at least ten walls go up — the
            /// colony's buildings, and a door a bandit must break (design 33 §16). Raised outright;
            /// the gate is about the fight, not the building of it. Returns how many went up.
            /// </summary>
            int Hut()
            {
                GridSize size = Colony.Pawns.Size;
                CellRef start = Colony.Start;
                foreach ((int ox, int oz) in new[] { (8, 0), (-8, 0), (0, 8), (0, -8), (8, 8), (-8, -8), (8, -8), (-8, 8) })
                {
                    int cx = start.X + ox, cz = start.Z + oz;
                    int raised = 0;
                    bool door = false;
                    // The door in the middle of the side facing the start.
                    (int doorX, int doorZ) = ox != 0 ? (-System.Math.Sign(ox) * 2, 0) : (0, -System.Math.Sign(oz) * 2);
                    for (int dz = -2; dz <= 2; dz++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        if (System.Math.Abs(dx) != 2 && System.Math.Abs(dz) != 2) continue;
                        bool isDoor = dx == doorX && dz == doorZ;
                        var cell = new CellRef(cx + dx, cz + dz, start.Y);
                        if (!size.Contains(cell)) continue;
                        if (Colony.Construction.Place(cell, isDoor ? BuildingHandle.Door : BuildingHandle.Wall, StuffHandle.Wood) != IntentRejection.None)
                            continue;
                        if (!Colony.Construction.Raise(Colony.Pawns, size.Index(cell.X, cell.Z, cell.Y))) continue;
                        raised++;
                        door |= isDoor;
                    }
                    if (door && raised >= 11) return raised;
                }
                Assert.Fail("no ground near the start would take a hut with a door");
                return 0;
            }

            /// <summary>
            /// Raid <paramref name="index"/>: its party through the debug menu's spawn, one intent each.
            /// <b>A party of three is answered</b>: every colonist on her feet is drafted, as a player
            /// at the keyboard would, and the draft's own four quiet hours let her go afterwards
            /// (design 33 §2b). A lone bandit is left to the colony's own response (§18). Without
            /// the answer every seed was all down by day two, and eight days of the gate were theft.
            /// </summary>
            public void Raid(int index)
            {
                (int day, int party) = Raids[index];
                int dx = (day % 4 < 2 ? 1 : -1) * 20, dz = (day % 2 == 0 ? 1 : -1) * 20;
                CellRef at = Colony.Start;
                for (int i = 0; i < party; i++)
                    Colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn,
                        new CellRef(at.X + dx, at.Z + dz, at.Y), PawnKindIndex.Bandit));
                var answering = new List<int>();
                if (party >= 3)
                    foreach (Pawn pawn in Colony.Pawns.Pawns.All)
                        if (pawn.IsColonist && !pawn.Downed && !pawn.Drafted)
                        {
                            Colony.World.Intents.Submit(new Intent(IntentKind.SetDrafted, default, pawn.Id.Value, 1));
                            answering.Add(pawn.Id.Value);
                            Drafts++;
                        }
                Step();

                // And gathered at the start, one order to the squad, as a box selection sends them:
                // the spread stands them side by side (design 33 §2d). Drafted where each happened to
                // be, five colonists across a 120-cell board met the party one at a time, three to one.
                foreach (int id in answering)
                    Colony.World.Intents.Submit(new Intent(IntentKind.OrderMove, at, id));
                if (answering.Count > 0) Step();
            }

            /// <summary>Drafts the gate gave, answering the parties of three.</summary>
            public int Drafts;

            public void Step()
            {
                Colony.World.Tick();
                Tape.Read(Colony);
                foreach (Pawn pawn in Colony.Pawns.Pawns.All)
                    if (pawn.IsColonist && pawn.Downed) _downedColonists.Add(pawn.Id.Value);
            }
        }

        /// <summary>
        /// What must be true of a fight at any instant, asked of both soaks. The last four were added
        /// for the gate (C7): a carried pawn is carried by somebody who is carrying her, nobody stands
        /// inside something solid, and every claim a pawn believes it holds is in the table under its
        /// own name — which, with the count agreeing, is "no claim held by a pawn that has gone".
        /// </summary>
        static void Invariants(ColonyWorld colony, int day)
        {
            PawnContext ctx = colony.Pawns;
            int held = 0;
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            {
                held += pawn.HeldReservations.Count;
                string who = $"day {day}, tick {colony.World.CurrentTick}, pawn {pawn.Id.Value}";
                for (int r = 0; r < pawn.HeldReservations.Count; r++)
                    Assert.That(ctx.Reservations.IsReservedBy(pawn.Id, pawn.HeldReservations[r]), Is.True,
                        $"{who}: believes it holds claim {pawn.HeldReservations[r]:x} and the table does not say so");
                if (pawn.CarriedBy != 0)
                {
                    // In somebody's arms (design 33 §11a): that somebody exists, is on the rescue, names
                    // her, and she is where her carrier is. A carrier gone or on another job is a
                    // colonist carried by nobody, for ever — §11f's "Cleanup puts her down on every exit".
                    Pawn? carrier = ctx.Pawns.Get(new PawnId(pawn.CarriedBy));
                    Assert.That(carrier, Is.Not.Null, $"{who}: carried by pawn {pawn.CarriedBy}, who has gone");
                    Assert.That(carrier!.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Rescue), $"{who}: carried by somebody not rescuing");
                    Assert.That(carrier.CombatTarget, Is.EqualTo(pawn.Id.Value), $"{who}: carried by somebody rescuing another");
                    Assert.That(pawn.Cell, Is.EqualTo(carrier.Cell), $"{who}: carried, and not where her carrier is");
                }
                else
                {
                    // Nobody inside a wall (design 30, nobody-in-a-wall): something solid or blocking in
                    // the cell she stands in that her own mode may not enter. A door she can open is not it.
                    Odyssey.Sim.Pathing.NavFlags flags = ctx.Nav.Grid.Flags[pawn.Cell];
                    bool filled = (flags & (Odyssey.Sim.Pathing.NavFlags.Solid | Odyssey.Sim.Pathing.NavFlags.Blocked)) != 0;
                    Assert.That(filled && !ctx.Nav.Grid.CanEnter(pawn.Cell, pawn.Mode), Is.False,
                        $"{who}: inside something solid at {ctx.Size.FromIndex(pawn.Cell)}");
                }
                Assert.That(Melee.IsDead(pawn), Is.False, $"{who}: dead and still on the board");
                Assert.That(pawn.HpMilli, Is.LessThanOrEqualTo(pawn.HpMaxMilli), $"{who}: over its pool");
                if (pawn.HpMilli <= 0) Assert.That(pawn.Downed, Is.True, $"{who}: at {pawn.HpMilli} and standing");
                // The line a pawn gets up at: whole for a colonist, who heals only in a bed and stays
                // there until she is (design 33 §11c); the content's threshold for an animal.
                // Past that line only while the body still holds her down (design 43 §3): blood past
                // its worst stage keeps a colonist down at a full pool.
                int upAt = pawn.IsColonist ? 1_000 : pawn.Content.Combat.downedRecoverAtPerMille;
                if (pawn.Downed && !pawn.CurrentVitals().Incapacitated)
                    Assert.That((long)pawn.HpMilli * 1_000, Is.LessThan((long)pawn.HpMaxMilli * upAt),
                        $"{who}: down past the line it gets up at");
                // The pool and the ledger never disagree (design 43 §2): every point a person with a
                // body has lost is on it somewhere, and a pawn with no body carries no ledger.
                if (pawn.Body != null)
                    Assert.That(pawn.Health?.TotalSeverityMilli ?? 0, Is.EqualTo(pawn.HpMaxMilli - pawn.HpMilli),
                        $"{who}: the pool and the ledger disagree");
                else
                    Assert.That(pawn.Health, Is.Null, $"{who}: a pawn with no body carries a ledger");
                // And whoever the body says is down is down.
                if (pawn.CurrentVitals().Incapacitated) Assert.That(pawn.Downed, Is.True, $"{who}: incapacitated and standing");
                if (pawn.Downed && pawn.FinishingStepTo < 0)
                    Assert.That(pawn.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Downed), $"{who}: down and doing something else");
                Assert.That(pawn.Downed && pawn.Drafted, Is.False, $"{who}: down and drafted");
            }
            Assert.That(colony.Pawns.Reservations.ActiveClaims, Is.EqualTo(held), $"day {day}: the reservation table disagrees with the pawns");
        }
    }
}
