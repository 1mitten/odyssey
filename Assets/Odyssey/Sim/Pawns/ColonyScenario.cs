#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Places a starting colony: colonists, a food store, beds and a stockpile near the map's
    /// start location.
    ///
    /// This is scenario setup rather than world generation, because it is a choice about *this*
    /// prototype and a different scenario would choose differently. It lives in the simulation
    /// assembly, not in the Unity composition root, for one specific reason: while it lived in a
    /// MonoBehaviour it could not be tested, and a colony that spawned but never reached the
    /// published snapshot looked exactly like a colony that worked. Every piece around it had
    /// tests; the join did not.
    /// </summary>
    public static class ColonyScenario
    {
        /// <summary>
        /// Meals in each starting pile. Twelve piles of twenty is 240 meals. Measured, not
        /// estimated: five colonists ate 120 meals in about 7.8 days on the ten-day soak (seed 1),
        /// which is 15 a day, so ten days is about 155 and this leaves a good half in hand. The
        /// arithmetic from the Defs said 9 a day and was wrong, which is why the number comes
        /// from the run. The ten-day run proves the simulation is stable unattended, not that a
        /// food economy balances, and nothing in the slice makes food (OQ-39); the pantry is sized
        /// so the gate measures the simulation.
        /// </summary>
        public const int MealsPerPile = 20;

        /// <summary>
        /// Mark every tree within <paramref name="radius"/> cells of the start for felling, on the
        /// start layer. The scene does this before its first tick so the colony has work from the
        /// moment it exists; a scenario choice, not a player command, so it writes the grid
        /// directly rather than queueing intents. Returns how many trees were marked.
        /// </summary>
        public static int DesignateTreesNear(Designations.DesignationGrid designations, CellRef start, int radius)
        {
            GridSize size = designations.Size;
            int marked = 0;
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = start.X + dx, z = start.Z + dz;
                if (!size.Contains(x, z, start.Y)) continue;
                var cell = new CellRef(x, z, start.Y);
                if (!designations.IsTree(size.Index(cell))) continue;
                if (designations.Designate(cell, Designations.DesignationKind.Fell) == IntentRejection.None) marked++;
            }
            return marked;
        }

        /// <summary>What a placement actually managed to do, so a caller can check rather than hope.</summary>
        public readonly struct Result
        {
            public readonly int Colonists;
            public readonly int Meals;
            public readonly int Beds;
            public readonly int StockpileCells;
            public readonly int Salvage;
            public readonly int SpotsFound;

            public Result(int colonists, int meals, int beds, int stockpileCells, int salvage, int spotsFound)
            {
                Colonists = colonists;
                Meals = meals;
                Beds = beds;
                StockpileCells = stockpileCells;
                Salvage = salvage;
                SpotsFound = spotsFound;
            }

            public override string ToString() =>
                $"{Colonists} colonists, {Meals} meals, {Beds} beds, {StockpileCells} stockpile cells, " +
                $"{Salvage} salvage, from {SpotsFound} spots";
        }

        /// <summary>
        /// Walkable cells near the start, spiralling outward so the colony lands together.
        ///
        /// The search widens through nearby layers as well as the start layer. On natural terrain
        /// the surface is terraced, so a neighbour of the start cell is often one step up or down,
        /// and a search pinned to a single layer finds a thin scatter of cells rather than a
        /// clearing.
        /// </summary>
        public static List<int> FindStartSpots(CellGrid grid, CellRef start, int wanted, int maxRadius = 24, int layerSpread = 2)
        {
            var size = grid.Size;
            var spots = new List<int>(wanted);

            for (int radius = 0; radius < maxRadius && spots.Count < wanted; radius++)
            {
                for (int dz = -radius; dz <= radius && spots.Count < wanted; dz++)
                for (int dx = -radius; dx <= radius && spots.Count < wanted; dx++)
                {
                    int ax = dx < 0 ? -dx : dx, az = dz < 0 ? -dz : dz;
                    if ((ax > az ? ax : az) != radius) continue;

                    int x = start.X + dx, z = start.Z + dz;
                    // Nearest layer first, so the colony stays on one level where it can.
                    for (int spread = 0; spread <= layerSpread; spread++)
                    {
                        if (TryTake(grid, size, x, z, start.Y + spread, spots)) break;
                        if (spread != 0 && TryTake(grid, size, x, z, start.Y - spread, spots)) break;
                    }
                }
            }
            return spots;
        }

        static bool TryTake(CellGrid grid, GridSize size, int x, int z, int y, List<int> spots)
        {
            if (!size.Contains(x, z, y)) return false;
            int index = size.Index(x, z, y);
            if (!grid.IsWalkable(index)) return false;
            spots.Add(index);
            return true;
        }

        /// <summary>
        /// Place the colony. Returns what it managed, so the caller can assert rather than assume.
        /// </summary>
        public static Result Place(
            CellGrid grid,
            PawnContext pawns,
            CellRef start,
            uint seed,
            int colonistCount = 5,
            int meals = 12,
            int stockpileCells = 9,
            int salvage = 8)
        {
            var spots = FindStartSpots(grid, start, wanted: colonistCount + meals + colonistCount + stockpileCells + 4);
            if (spots.Count == 0) return new Result(0, 0, 0, 0, 0, 0);

            var rng = DeterministicRandom.ForTick(seed, 0, purpose: 0xC0101);
            int take = 0;

            int placedColonists = 0;
            for (int i = 0; i < colonistCount && take < spots.Count; i++, take++, placedColonists++)
                pawns.Pawns.Spawn(spots[take]);

            int placedMeals = 0;
            for (int i = 0; i < meals && take < spots.Count; i++, take++, placedMeals++)
                pawns.Items.Spawn(ItemIndex.Meal, spots[take], stack: MealsPerPile);

            int placedBeds = 0;
            for (int i = 0; i < colonistCount && take < spots.Count; i++, take++, placedBeds++)
                pawns.Items.AddBed(spots[take]);

            var stockpile = new List<int>(stockpileCells);
            for (int i = 0; i < stockpileCells && take < spots.Count; i++, take++) stockpile.Add(spots[take]);
            if (stockpile.Count > 0)
            {
                var allow = new bool[ItemIndex.Count];
                for (int i = 0; i < allow.Length; i++) allow[i] = true;
                pawns.Items.AddStockpile(new Stockpile(priority: 2, stockpile.ToArray(), allow));
            }

            // Loose salvage so hauling has work from the first tick.
            int placedSalvage = 0;
            for (int i = 0; i < salvage; i++, placedSalvage++)
                pawns.Items.Spawn(ItemIndex.Salvage, spots[rng.NextInt(spots.Count)]);

            return new Result(placedColonists, placedMeals, placedBeds, stockpile.Count, placedSalvage, spots.Count);
        }
    }
}
