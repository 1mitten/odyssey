#nullable enable
using System.Collections.Generic;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;

using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// A throwaway diagnostic: run the playtest colony and report, tick by tick, every colonist
    /// standing in a cell with nothing under it and every step that changes layer.
    ///
    /// <para>Written because a screenshot showed a colonist in mid-air over a worked face and
    /// the candidate explanations — a pawn left unsupported by a dig, a pawn drawn part way
    /// through a very expensive step, a pawn on an undrawn ladder — look identical in a photograph
    /// and want completely different fixes. It settled the question in one run: nothing was ever
    /// unsupported, and it was the ladder. It also found the bug nobody had gone looking for, a
    /// quarter of all spoil hanging in the air, because items had no support rule at all.</para>
    ///
    /// <para>Run it with <c>scripts/unity.sh exec Odyssey.EditorTools.FootingProbe.Run</c>. Kept
    /// rather than deleted: the numbers it prints are the ones the fixes are claimed against, and
    /// a claim you cannot re-measure is a claim you cannot defend.</para>
    /// </summary>
    public static class FootingProbe
    {
        public static void Run()
        {
            var size = new GridSize(100, 100, 16);
            var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
            gen.MakeWooded();
            var grid = new CellGrid(size);
            var chunks = new ChunkGrid(size);
            MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);

            var nav = new NavGraph(grid);
            nav.Rebuild();
            var pawns = new PawnContext(
                grid, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns())
                { Chunks = chunks };
            var support = new SupportSystem(grid, new SupportSolver(grid), chunks);
            var designations = new DesignationGrid(grid, result.Edifices);

            SimWorld world = new SimWorldBuilder()
                .WithSeed(1u)
                .WithSize(size)
                .AddColony(pawns, designations, support, nav, result.Placements, out _)
                .Build();

            ScenarioDef scenario = ScenarioDef.Playtest();
            ColonyScenario.Place(grid, pawns, result.StartCell, 1u, scenario);
            ColonyScenario.GiveStartingOrders(designations, result.StartCell, scenario);

            // How much rock is standing before anybody touches it, so the run can report what the
            // colony actually got done. A fix that makes the board tidy by stopping the work is
            // not a fix, and nothing else here would notice.
            int solidAtStart = 0;
            for (int i = 0; i < size.CellCount; i++) if (grid.IsSolidTerrain(i)) solidAtStart++;

            var floating = new Dictionary<int, int>();   // pawn id -> first tick seen unsupported
                        int layerSteps = 0, expensiveSteps = 0, worstCost = 0;
            int longestHang = 0, totalHang = 0, hangs = 0, ladderStands = 0;
            int hopTicks = 0, hopsOntoNothing = 0;
            int horizontalSteps = 0, stepsOntoAir = 0, standingOnAir = 0, airSamples = 0;
            int workTicks = 0, outOfReach = 0, reachSamples = 0;

            for (int tick = 0; tick < 40_000; tick++)
            {
                world.Tick();

                foreach (Pawn pawn in pawns.Pawns.All)
                {
                    // 1. Standing on nothing. Not "mid-step over a hole" — this is the cell the
                    //    simulation says the pawn is IN.
                    // Two different things, and the whole question is which one the screenshot
                    // showed. A connector cell counts as having a floor to the NAV grid — a pawn
                    // is on the ladder — but not to the cell grid. The answer was the second:
                    // nothing unsupported, and about a sixth of all colonist time spent on a
                    // connector. Nothing is drawn in one on purpose (the owner asked for the prop
                    // to go), so this count is not a fault; it is how much colonist time the climb
                    // pose has to carry, and it is the number to watch if that starts to matter.
                    bool onLadder = (nav.Grid.Flags[pawn.Cell] & NavFlags.Connector) != 0;
                    bool footed = grid.HasFloor(pawn.Cell) || onLadder;
                    if (!grid.HasFloor(pawn.Cell) && onLadder) ladderStands++;
                    if (!footed)
                    {
                        if (!floating.ContainsKey(pawn.Id.Value)) floating[pawn.Id.Value] = tick;
                    }
                    else if (floating.TryGetValue(pawn.Id.Value, out int since))
                    {
                        floating.Remove(pawn.Id.Value);
                        int held = tick - since;
                        if (held > longestHang) longestHang = held;
                        totalHang += held;
                        hangs++;
                        Debug.Log($"[Footing] tick {tick}: pawn {pawn.Id.Value} got its feet back " +
                                  $"in {size.FromIndex(pawn.Cell)} after {held} ticks " +
                                  $"({held / 60f:0.0} s at 60 tps)");
                    }

                    if (!footed && floating[pawn.Id.Value] == tick)
                    {
                        int below = pawn.Cell - size.LayerStride;
                        int drop = 0;
                        for (int c = below; c >= 0 && !grid.IsSolidTerrain(c); c -= size.LayerStride) drop++;
                        Debug.Log($"[Footing] tick {tick}: pawn {pawn.Id.Value} is in " +
                                  $"{size.FromIndex(pawn.Cell)} with no floor under it, " +
                                  $"{drop} empty layer(s) below it, path={pawn.HasPath}");
                    }

                    // 1a. Working something it could not reach. WorkFocus is the cell the driver
                    //     says it is working, published for presentation; -1 while walking. The
                    //     reach rule is: beside it, and for mining one layer up as well.
                    int focus = pawn.Driver != null ? pawn.Driver.WorkFocus : -1;
                    if (focus >= 0)
                    {
                        workTicks++;
                        CellRef me = size.FromIndex(pawn.Cell);
                        CellRef it = size.FromIndex(focus);
                        int up = me.Y - it.Y;
                        // Felling shares a floor with its tree. Mining works from beside, from a
                        // layer up (the rim and on-top stances) and from a layer down, because a
                        // pick goes overhead.
                        bool mining = pawn.CurrentJob != null && pawn.CurrentJob.DefIndex == JobIndex.Mine;
                        int above = mining ? 1 : 0;
                        int below = mining ? 1 : 0;
                        bool near = System.Math.Abs(me.X - it.X) <= 1
                                 && System.Math.Abs(me.Z - it.Z) <= 1
                                 && up <= above && up >= -below;
                        if (!near)
                        {
                            outOfReach++;
                            if (reachSamples < 5)
                            {
                                reachSamples++;
                                Debug.Log($"[Footing] tick {tick}: pawn {pawn.Id.Value} (job " +
                                          $"{pawn.CurrentJob?.DefIndex}) is in {me} working {it}");
                            }
                        }
                    }

                    // 1b. WALKING on air. The question the earlier version of this probe could
                    //     not ask, because it counted a connector cell as somewhere to stand: a
                    //     cell carrying a climb footprint has a floor as far as navigation is
                    //     concerned and nothing whatever underneath it, so a row of them is a
                    //     bridge over a hole. A HORIZONTAL step into one is a colonist walking
                    //     out over thin air.
                    if (pawn.HasPath)
                    {
                        int into = pawn.Path[pawn.PathIndex];
                        if (pawn.Cell / size.LayerStride == into / size.LayerStride)
                        {
                            horizontalSteps++;
                            if (!grid.HasFloor(into))
                            {
                                stepsOntoAir++;
                                if (airSamples < 6)
                                {
                                    airSamples++;
                                    Debug.Log($"[Footing] tick {tick}: pawn {pawn.Id.Value} steps " +
                                              $"sideways from {size.FromIndex(pawn.Cell)} into " +
                                              $"{size.FromIndex(into)}, which has no floor " +
                                              $"(connector flags {nav.Grid.Flags[into] & NavFlags.Connector})");
                                }
                            }
                        }
                    }

                    // 1c. STANDING on air: not moving at all, in a cell with nothing under it.
                    if (!pawn.HasPath && !grid.HasFloor(pawn.Cell)) standingOnAir++;

                    // 2a. Is there anything to land ON? Climbing is gone (owner, 2026-09-16) and
                    //     the only unaided layer change is a hop: one block up onto the block next
                    //     door, or one block down off it. Its upper end has to be the top of solid
                    //     terrain, or the colonist is stepping into clear air — the thing this
                    //     probe was written to catch, in its new form.
                    if (pawn.HasPath)
                    {
                        int step = pawn.Path[pawn.PathIndex];
                        if (pawn.Cell / size.LayerStride != step / size.LayerStride)
                        {
                            int upper = System.Math.Max(pawn.Cell, step);
                            hopTicks++;
                            if (!nav.UpperEndIsABlockTop(upper)) hopsOntoNothing++;
                        }
                    }

                    // 2. What a layer change is costing, which is what decides how long a drop
                    //    takes and therefore how it reads.
                    if (!pawn.HasPath) continue;
                    int next = pawn.Path[pawn.PathIndex];
                    if (pawn.Cell / size.LayerStride == next / size.LayerStride) continue;

                    layerSteps++;
                    int cost = CostOf(nav, pawn, next);
                    if (cost <= MoveCost.Drop) continue;
                    expensiveSteps++;
                    if (cost > worstCost)
                    {
                        worstCost = cost;
                        Debug.Log($"[Footing] tick {tick}: pawn {pawn.Id.Value} is taking a " +
                                  $"{size.FromIndex(pawn.Cell)} -> {size.FromIndex(next)} step " +
                                  $"costing {cost} units ({cost / 100f:0.0} flat cells, " +
                                  $"{cost / (float)ContentPack.Pawns().Movement.movePerTick:0} ticks)");
                    }
                }
            }

            // And the other half of the question: does anything a mine leaves ever end up with
            // nothing under it? Items have no support rule at all, so this is measurement rather
            // than a guess about whether one is needed.
            int floatingItems = 0, itemsOnGround = 0, deepest = 0;
            var stacks = pawns.Items.Items;
            for (int i = 0; i < stacks.Count; i++)
            {
                var item = stacks[i];
                if (item.Despawned || item.Cell < 0) continue;
                itemsOnGround++;
                if (grid.HasFloor(item.Cell)) continue;
                floatingItems++;
                int drop = 0;
                for (int c = item.Cell - size.LayerStride; c >= 0 && !grid.IsSolidTerrain(c); c -= size.LayerStride)
                    drop++;
                if (drop > deepest) deepest = drop;
                Debug.Log($"[Footing] item {item.DefIndex} x{item.Stack} hangs in " +
                          $"{size.FromIndex(item.Cell)} with {drop} empty layer(s) below it");
            }

            int solidAtEnd = 0;
            for (int i = 0; i < size.CellCount; i++) if (grid.IsSolidTerrain(i)) solidAtEnd++;
            // And what is LEFT, which is the difference between "the colony had to walk further"
            // and "the colony cannot get at its own orders any more". A fix that tidies the board
            // by quietly making the work unreachable is not a fix, and the cells-mined count on
            // its own cannot tell the two apart.
            int ordered = 0, ordersWithNoStance = 0, ordersCutOff = 0;
            Pawn first = pawns.Pawns.All[0];
            var marked = designations.Cells;
            for (int i = 0; i < marked.Count; i++)
            {
                int cell = marked[i];
                if (designations.At(cell) != Odyssey.Sim.Designations.DesignationKind.Mine) continue;
                if (!designations.CanMine(cell)) continue;
                ordered++;
                if (MineWorkGiver.StandToMine(pawns, first, cell) >= 0) continue;
                ordersWithNoStance++;

                // Two very different faults with one symptom. "No ground" means the cells around
                // the rock genuinely have nothing to stand on and the order was always going to
                // wait. "Cut off" means there IS somewhere to stand and the colony cannot get to
                // it, which is a routing failure and mine to fix.
                bool anyGround = false;
                CellRef at = size.FromIndex(cell);
                for (int dz = -1; dz <= 1 && !anyGround; dz++)
                for (int dx = -1; dx <= 1 && !anyGround; dx++)
                for (int dy = 0; dy <= 1 && !anyGround; dy++)
                {
                    if (dx == 0 && dz == 0) continue;
                    if (!size.Contains(at.X + dx, at.Z + dz, at.Y + dy)) continue;
                    if (grid.IsWalkable(size.Index(at.X + dx, at.Z + dz, at.Y + dy))) anyGround = true;
                }

                if (anyGround) ordersCutOff++;
            }

            Debug.Log($"[Footing] work: {solidAtStart - solidAtEnd} cells mined out in 40,000 ticks; " +
                      $"{ordered} orders still standing, {ordersWithNoStance} with no stance " +
                      $"({ordersCutOff} of those have ground beside them and cannot be reached)");

            Debug.Log($"[Footing] items: {floatingItems} of {itemsOnGround} stacks on the ground " +
                      $"have no floor under them, deepest drop {deepest} layer(s)");

            // The same predicate the per-tick check uses, or the two numbers are not comparable:
            // a pawn on a connector has somewhere to stand and a pawn on nothing does not.
            int stillFloating = 0;
            foreach (Pawn pawn in pawns.Pawns.All)
            {
                bool onConnector = (nav.Grid.Flags[pawn.Cell] & NavFlags.Connector) != 0;
                if (!grid.HasFloor(pawn.Cell) && !onConnector) stillFloating++;
            }

            Debug.Log($"[Footing] done: {hangs} unsupported spells, " +
                      $"{stillFloating} still are at tick 40000; " +
                      $"{layerSteps} layer steps seen, {expensiveSteps} of them dearer than a ladder, " +
                      $"worst {worstCost} units; {hangs} hangs, longest {longestHang} ticks, " +
                      $"mean {(hangs > 0 ? totalHang / hangs : 0)} ticks; " +
                      $"{ladderStands} pawn-ticks spent on a connector; " +
                      $"{hopsOntoNothing} of {hopTicks} layer-changing pawn-ticks had nothing to land on; " +
                      $"{stepsOntoAir} of {horizontalSteps} sideways pawn-ticks stepped onto a floorless cell; " +
                      $"{standingOnAir} pawn-ticks stood still on one; " +
                      $"{outOfReach} of {workTicks} working pawn-ticks were out of reach of their work");
        }

        static int CostOf(NavGraph nav, Pawn pawn, int next)
        {
            for (int edge = nav.FirstPortalEdge(pawn.Cell); edge != -1; edge = nav.PortalEdgeNext(edge))
                if (nav.PortalEdgeTarget(edge) == next
                    && TraverseModes.Allows(nav.PortalEdgeMode(edge), pawn.Mode))
                    return nav.PortalEdgeCost(edge);
            return MoveCost.Fall;
        }
    }
}
