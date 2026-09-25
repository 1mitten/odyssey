#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The gate for traits and mental health (design 43, TM6): ten days on three seeds with traits
    /// dealt, and from the third day the colony's mood pushed through all three break lines in turn,
    /// on faster clocks than the game's so every break shows inside ten days. Asked every in-game
    /// hour: nobody dead, a broken colonist running only her break's jobs and never drafted, the
    /// band saying broken while she is, and the reservation table agreeing with the pawns. A lockstep
    /// twin must hash the same every hour, and a save taken while somebody is in a break must resume
    /// to the same hash a day later.
    ///
    /// <para><b>The mood is pushed through the Def, not the pawns.</b> Each day's base is replaced on
    /// both worlds' own content — never written through the shared Def — so the needs pass itself
    /// drifts every colonist down to the line, and the break is rolled, chosen and run by the game's
    /// own code. Across the three seeds every one of the five breaks must be seen.</para>
    /// </summary>
    public class MindSoakTests
    {
        static readonly GridSize Board = new GridSize(60, 60, 16);
        const int Day = 60_000;
        const int Hour = Day / 24;

        /// <summary>The base mood on each day: rest, rest, rest, then minor, major, extreme, twice, then rest.</summary>
        static readonly int[] BaseByDay = { 500, 500, 500, 300, 150, 20, 300, 150, 20, 500 };

        static readonly HashSet<int> SeenAcrossSeeds = new HashSet<int>();

        sealed class World
        {
            public readonly ColonyWorld Colony;

            public World(uint seed)
            {
                ScenarioDef scenario = ScenarioDef.Bare();
                scenario.colonists = 5;
                scenario.beds = 5;
                scenario.startingFellRadius = 0;
                Colony = ColonyWorld.Build(Board, seed, scenario, barren: true, wooded: false);
                Colony.World.Tick();

                // Something for a tantrum to strike: a short wall beside the start, raised outright.
                CellRef start = Colony.Start;
                for (int i = 0; i < 4; i++)
                {
                    int cell = Colony.Grid.NearestWalkableInColumn(start.X + 6, start.Z - 2 + i, start.Y);
                    if (cell < 0) continue;
                    if (Colony.Construction.Place(Board.FromIndex(cell), BuildingHandle.Wall, StuffHandle.Wood, 0)
                        != IntentRejection.None) continue;
                    Colony.Construction.Raise(Colony.Pawns, cell);
                }
            }

            public void Day(int day)
            {
                MoodDef shipped = Colony.Pawns.Content.Mood;
                Colony.Pawns.Content.Mood = new MoodDef
                {
                    baseMood = BaseByDay[day], max = shipped.max,
                    risePerInterval = shipped.risePerInterval, fallPerInterval = shipped.fallPerInterval,
                    breakThreshold = shipped.breakThreshold, strainMargin = shipped.strainMargin,
                    // Faster than the game's four, 0.8 and half a day, so ten days sees everything.
                    breakMtbTicks = 20_000, majorMtbTicks = 10_000, extremeMtbTicks = 6_000,
                };
            }
        }

        [Test, Category("Long")]
        [TestCase(1u)]
        [TestCase(2u)]
        [TestCase(3u)]
        public void TenDaysThroughEveryBreakLine(uint seed)
        {
            var world = new World(seed);
            var twin = new World(seed);
            ColonyWorld colony = world.Colony;
            var seen = new HashSet<int>();
            int breaks = 0, wasBroken = 0;
            byte[]? saved = null;
            int savedAt = -1;
            ulong resumed = 0, original = 0;
            var watch = Stopwatch.StartNew();

            Assert.That(System.Linq.Enumerable.All(colony.Pawns.Pawns.All, p => !p.IsColonist || p.Traits.Count >= 2), Is.True,
                "the gate runs with traits dealt, so a line a trait moved is part of what it tests");

            for (int day = 0; day < 10; day++)
            {
                world.Day(day);
                twin.Day(day);

                for (int hour = 0; hour < 24; hour++)
                {
                    for (int t = 0; t < Hour; t++)
                    {
                        colony.World.Tick();
                        twin.Colony.World.Tick();

                        int broken = 0;
                        foreach (Pawn pawn in colony.Pawns.Pawns.All)
                        {
                            if (!pawn.IsBroken) continue;
                            broken++;
                            seen.Add(pawn.BreakKind);
                        }
                        if (broken > wasBroken) breaks += broken - wasBroken;
                        wasBroken = broken;

                        // The first moment on or after day four that somebody is in a break.
                        if (saved == null && day >= 4 && broken > 0)
                        {
                            saved = colony.Save();
                            savedAt = colony.World.CurrentTick;
                        }
                    }

                    Invariants(colony, day, hour);
                    Assert.That(twin.Colony.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value),
                        $"seed {seed}, day {day}, hour {hour}: the lockstep twin diverged");

                    if (saved != null && resumed == 0 && colony.World.CurrentTick >= savedAt + Day)
                    {
                        var loaded = new World(seed).Colony;
                        loaded.Load(saved);
                        // The loaded world's content is its own; bring it to the same day's base.
                        loaded.Pawns.Content.Mood = colony.Pawns.Content.Mood;
                        // Every day it lived through after the save used the base of that day, so it
                        // is replayed a day at a time from the save.
                        int tick = loaded.World.CurrentTick;
                        while (tick < colony.World.CurrentTick)
                        {
                            int d = tick / Day;
                            int until = System.Math.Min(colony.World.CurrentTick, (d + 1) * Day);
                            var dayWorld = new MoodDef
                            {
                                baseMood = BaseByDay[d], max = colony.Pawns.Content.Mood.max,
                                risePerInterval = colony.Pawns.Content.Mood.risePerInterval,
                                fallPerInterval = colony.Pawns.Content.Mood.fallPerInterval,
                                breakThreshold = colony.Pawns.Content.Mood.breakThreshold,
                                strainMargin = colony.Pawns.Content.Mood.strainMargin,
                                breakMtbTicks = 20_000, majorMtbTicks = 10_000, extremeMtbTicks = 6_000,
                            };
                            loaded.Pawns.Content.Mood = dayWorld;
                            loaded.World.Tick(until - tick);
                            tick = until;
                        }
                        resumed = loaded.World.ComputeStateHash().Value;
                        original = colony.World.ComputeStateHash().Value;
                    }
                }
            }
            watch.Stop();

            lock (SeenAcrossSeeds) SeenAcrossSeeds.UnionWith(seen);
            TestContext.WriteLine($"mind gate seed {seed}: {watch.Elapsed.TotalSeconds:F1} s for the run and its twin; " +
                $"{breaks} breaks entered, kinds seen [{string.Join(",", seen)}]; save at tick {savedAt}");

            Assert.That(breaks, Is.GreaterThan(0), $"seed {seed}: nobody broke in ten days pushed to the lines");
            Assert.That(saved, Is.Not.Null, $"seed {seed}: never saved mid-break");
            Assert.That(resumed, Is.EqualTo(original), $"seed {seed}: the save taken mid-break did not resume to the same day");
        }

        /// <summary>
        /// Runs after the three seeds (NUnit orders by name within a fixture, and this sorts last):
        /// every break was seen by at least one of them.
        /// </summary>
        [Test, Category("Long")]
        public void ZzEveryBreakWasSeenOnSomeSeed()
        {
            if (SeenAcrossSeeds.Count == 0)
                for (uint seed = 1; seed <= 3; seed++) TenDaysThroughEveryBreakLine(seed);
            for (int k = 0; k < BreakHandle.Count; k++)
                Assert.That(SeenAcrossSeeds, Does.Contain(k), $"no seed saw {BreakHandle.Names[k]}");
        }

        static void Invariants(ColonyWorld colony, int day, int hour)
        {
            PawnContext ctx = colony.Pawns;
            int held = 0;
            foreach (Pawn pawn in ctx.Pawns.All)
            {
                string who = $"day {day}, hour {hour}, pawn {pawn.Id.Value}";
                held += pawn.HeldReservations.Count;
                Assert.That(Melee.IsDead(pawn), Is.False, $"{who}: dead — an unordered fight ends in a downing");
                if (!pawn.IsBroken)
                {
                    Assert.That(pawn.BreakKind, Is.EqualTo(BreakHandle.Wander), $"{who}: a break kind with no break");
                    continue;
                }
                Assert.That(pawn.Drafted, Is.False, $"{who}: drafted in a break");
                Assert.That(pawn.Band(), Is.EqualTo(MoodBand.Broken), $"{who}: in a break and not saying so");
                if (pawn.CurrentJob != null && !pawn.Downed)
                    Assert.That(MentalBreaks.IsBreakJob(pawn, pawn.CurrentJob, ctx.Content), Is.True,
                        $"{who}: in a {BreakHandle.Names[pawn.BreakKind]} and running job {pawn.CurrentJob.DefIndex}");
            }
            Assert.That(ctx.Reservations.ActiveClaims, Is.EqualTo(held), $"day {day}, hour {hour}: the reservation table disagrees with the pawns");
        }
    }
}
