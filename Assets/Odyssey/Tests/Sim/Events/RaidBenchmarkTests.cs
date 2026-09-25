#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Diagnostics;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Pawns.Wildlife;

namespace Odyssey.Tests.Sim.Events
{
    /// <summary>
    /// What two hundred raiders cost the tick (design 50 §11), measured before the pawn ceiling is
    /// raised to make room for them. One colony of twenty on the played map at the scale target,
    /// walked through every phase of one raid in turn: peace, the band loitering at the edge, the
    /// band loitering while every colonist is on <see cref="HostilityResponse.Defend"/> — the known
    /// risk, because a non-FightBack colonist's notice scans every pawn every tick while anything
    /// hostile stands — then the assault and the withdrawal.
    ///
    /// <para><b>Explicit, and a benchmark rather than a gate</b>, like every arm in
    /// <c>TickBenchmarkTests</c>. The band is scheduled through <see cref="RaidSystem.Begin"/>
    /// directly, because the worker refuses a band past today's ceiling — which is the thing this
    /// measures whether to move.</para>
    /// </summary>
    public class RaidBenchmarkTests
    {
        const int Ticks = 1_500;
        const int Raiders = 200;

        [Test, Explicit, Category("Benchmark")]
        public void TwoHundredRaidersThroughEveryPhase()
        {
            var report = new StringBuilder();
            GridSize board = GridSize.ScaleTarget;
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 20;
            scenario.beds = 20;
            scenario.stockpileCells = 9;
            var setup = Stopwatch.StartNew();
            ColonyWorld colony = ColonyWorld.Build(board, 12345u, scenario, wooded: true);
            setup.Stop();
            report.AppendLine($"{board.SizeX} x {board.SizeZ} x {board.SizeY}, the played map, 20 colonists, setup {setup.ElapsedMilliseconds} ms, {Ticks} ticks an arm");

            colony.World.Tick(600);
            Measure(colony, report, "peace");
            int peaceSpawned = colony.Pawns.Pawns.Count;

            RaidSystem raids = colony.Pawns.Raids!;
            RaidGroup group = Schedule(colony, raids);
            colony.World.Tick(1_200);
            Assert.That(group.Pending, Is.Empty, "the band had not all arrived");
            Assert.That(group.Phase, Is.EqualTo(RaidPhase.Gathering));
            Measure(colony, report, $"{Raiders} loitering at the edge");

            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                if (pawn.IsColonist) pawn.Response = HostilityResponse.Defend;
            Measure(colony, report, $"{Raiders} loitering, every colonist on Defend");
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                if (pawn.IsColonist) pawn.Response = HostilityResponse.FightBack;

            raids.SetPhase(group, RaidPhase.Assaulting, colony.World.CurrentTick);
            // Long enough for the band to cross the board and be among the colony.
            colony.World.Tick(4_000);
            Measure(colony, report, $"{Raiders} assaulting");

            raids.SetPhase(group, RaidPhase.Withdrawing, colony.World.CurrentTick);
            Measure(colony, report, $"{Raiders} withdrawing");

            TestContext.WriteLine(report.ToString());
        }

        static void Measure(ColonyWorld colony, StringBuilder report, string label)
        {
            var trace = new PhaseTrace(Ticks);
            colony.World.PhaseSink = trace;
            colony.World.Tick(Ticks);
            colony.World.PhaseSink = null;
            int hostile = 0, standing = 0;
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            {
                if (!pawn.IsHostile) continue;
                hostile++;
                if (Melee.IsStanding(pawn)) standing++;
            }
            report.AppendLine($"--- {label}: {colony.Pawns.Pawns.Count} pawns, {hostile} hostile ({standing} standing) --- " +
                              $"tick {trace.MeanTickMs():F3} ms mean, {trace.P95TickMs():F3} p95; " +
                              $"Pawns phase {trace.MeanMs(TickSegment.Pawns):F3} ms mean, {trace.P95Ms(TickSegment.Pawns):F3} p95");
            report.AppendLine($"    by segment, mean ms: intents {trace.MeanMs(TickSegment.Intents):F3}, world {trace.MeanMs(TickSegment.WorldSystems):F3}, " +
                              $"things {trace.MeanMs(TickSegment.Things):F3}, pawns {trace.MeanMs(TickSegment.Pawns):F3}, " +
                              $"deferred {trace.MeanMs(TickSegment.Deferred):F3}, snapshot {trace.MeanMs(TickSegment.Snapshot):F3}, " +
                              $"hash {trace.MeanMs(TickSegment.Hash):F3}");
        }

        /// <summary>The worker's schedule without its ceiling: one edge, 200 slots, the Mixed band, a loiter that outlasts the arms.</summary>
        static RaidGroup Schedule(ColonyWorld colony, RaidSystem raids)
        {
            PawnContext ctx = colony.Pawns;
            SurfaceCensus census = SurfaceCensus.Take(ctx.Cells, ctx.Nav, null, colony.Start, 0, TraverseMode.Bandit);
            var west = new List<int>();
            foreach (int cell in census.Edge)
                if (ctx.Size.FromIndex(cell).X == 0) west.Add(cell);
            Assert.That(west, Is.Not.Empty, "no reachable west edge");
            int midZ = ctx.Size.SizeZ / 2;
            west.Sort((a, b) => Math.Abs(ctx.Size.FromIndex(a).Z - midZ).CompareTo(Math.Abs(ctx.Size.FromIndex(b).Z - midZ)));

            RaidMix mix = ContentPack.Incidents().Mixes[2];
            var counts = new int[mix.Kinds.Length];
            mix.Compose(Raiders, counts);
            var arrivals = new List<RaidArrival>(Raiders);
            int tick = colony.World.CurrentTick, k = 0;
            for (int row = 0; row < counts.Length; row++)
                for (int n = 0; n < counts[row]; n++, k++)
                    arrivals.Add(new RaidArrival(tick + 1 + k * 300 / Raiders, west[k % west.Count], mix.Kinds[row]));

            int gather = west[0] + 12;
            int target = RaidTargets.Resolve(ctx, gather);
            return raids.Begin(IncidentHandle.Raid, 2, tick, gather, gather, target, loiterTicks: 1_000_000,
                probeTicks: 1_000_000, millRadius: 5, earlyTriggerCells: -1, retreatPerMille: 1_000, arrivals);
        }
    }
}
