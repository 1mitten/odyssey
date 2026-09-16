#nullable enable
using System.Collections.Generic;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
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
                grid, nav, new PathService(new PathFinder(nav)), PawnContent.Core())
                { Chunks = chunks };
            var support = new SupportSystem(grid, new SupportSolver(grid), chunks);
            var designations = new DesignationGrid(grid, result.Edifices);

            SimWorld world = new SimWorldBuilder()
                .WithSeed(1u)
                .WithSize(size)
                .AddColony(pawns, designations, support, nav)
                .Build();

            ScenarioDef scenario = ScenarioDef.Playtest();
            ColonyScenario.Place(grid, pawns, result.StartCell, 1u, scenario);
            ColonyScenario.GiveStartingOrders(designations, result.StartCell, scenario);

            var floating = new Dictionary<int, int>();   // pawn id -> first tick seen unsupported
                        int layerSteps = 0, expensiveSteps = 0, worstCost = 0;
            int longestHang = 0, totalHang = 0, hangs = 0, ladderStands = 0;
            int climbTicks = 0, climbsAgainstNothing = 0;

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

                    // 2a. Is there anything to climb AGAINST? A climb needs a block beside the
                    //     hole whose edge the colonist goes up. An open quarry has none in the
                    //     middle of it, and a connector laid there is a colonist going up through
                    //     thin air.
                    if (pawn.HasPath)
                    {
                        int step = pawn.Path[pawn.PathIndex];
                        if (pawn.Cell / size.LayerStride != step / size.LayerStride)
                        {
                            int lower = System.Math.Min(pawn.Cell, step);
                            climbTicks++;
                            if (!HasWallBeside(grid, size, lower)) climbsAgainstNothing++;
                        }
                    }

                    // 2. What a layer change is costing, which is what decides how long a drop
                    //    takes and therefore how it reads.
                    if (!pawn.HasPath) continue;
                    int next = pawn.Path[pawn.PathIndex];
                    if (pawn.Cell / size.LayerStride == next / size.LayerStride) continue;

                    layerSteps++;
                    int cost = CostOf(nav, pawn, next);
                    if (cost <= MoveCost.ClimbDown) continue;
                    expensiveSteps++;
                    if (cost > worstCost)
                    {
                        worstCost = cost;
                        Debug.Log($"[Footing] tick {tick}: pawn {pawn.Id.Value} is taking a " +
                                  $"{size.FromIndex(pawn.Cell)} -> {size.FromIndex(next)} step " +
                                  $"costing {cost} units ({cost / 100f:0.0} flat cells, " +
                                  $"{cost / (float)PawnContent.Core().Movement.movePerTick:0} ticks)");
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
                      $"{climbsAgainstNothing} of {climbTicks} climbing pawn-ticks had no wall beside them");
        }

        /// <summary>Is any of the four horizontal neighbours of this cell solid rock?</summary>
        static bool HasWallBeside(CellGrid grid, GridSize size, int cell)
        {
            CellRef at = size.FromIndex(cell);
            if (at.X > 0 && grid.IsSolidTerrain(size.Index(at.X - 1, at.Z, at.Y))) return true;
            if (at.X < size.SizeX - 1 && grid.IsSolidTerrain(size.Index(at.X + 1, at.Z, at.Y))) return true;
            if (at.Z > 0 && grid.IsSolidTerrain(size.Index(at.X, at.Z - 1, at.Y))) return true;
            if (at.Z < size.SizeZ - 1 && grid.IsSolidTerrain(size.Index(at.X, at.Z + 1, at.Y))) return true;
            return false;
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
