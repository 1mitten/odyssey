#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// What a colony starts with: how many colonists, what lies on the ground beside them, and
    /// which standing orders are already given when the first tick runs.
    ///
    /// A Def rather than a set of parameters because these numbers are content (04-data-model.md:
    /// content is data from day one), and a set of parameters is where content hides. The felling
    /// radius lived on the bootstrap as a public field, which meant a presentation component
    /// decided what the colonists did first and nothing that ran headless could see the
    /// decision. Built in code for the reason <see cref="PawnContent.Core"/> is: there is no
    /// content pack yet, and a clone without one must still run.
    /// </summary>
    public class ScenarioDef : Def
    {
        public int colonists = 5;

        public int mealPiles = 12;

        /// <summary>
        /// Meals in each starting pile. Twelve piles of twelve is 144 meals. Measured on the
        /// ten-day soak (seed 1) with a ration worth a full vanilla meal of 900 units: five
        /// colonists ate 92 meals in ten days, 1.8 a day each, which is the vanilla figure
        /// (docs/research/a-08-plants-growing-food.md §2), so 144 leaves a third in hand. It
        /// was 240 when the ration restored 450 and the burn was double. The ten-day run proves
        /// the simulation is stable unattended, not that a food economy balances, and nothing in
        /// the slice makes food (OQ-39); the pantry is sized so the gate measures the simulation.
        /// </summary>
        public int mealsPerPile = 12;

        /// <summary>
        /// One per colonist is what the placement always gave. A number of its own so that a
        /// scenario can short the colony of beds on purpose, which is the first thing a
        /// difficulty setting would do.
        /// </summary>
        public int beds = 5;

        public int stockpileCells = 9;

        /// <summary>Loose salvage scattered about, so hauling has work from the first tick.</summary>
        public int salvage = 8;

        /// <summary>
        /// Every tree within this many cells of the start is marked for felling before the first
        /// tick, on the start layer. Zero marks nothing.
        /// </summary>
        public int startingFellRadius = 10;

        /// <summary>
        /// The nearest rock outcrop within this many cells of the start is marked for mining
        /// before the first tick. Zero marks nothing.
        ///
        /// <para>It exists for the same reason <see cref="startingFellRadius"/> does: there is no
        /// tool to give the order with yet, and a feature nobody can reach is a feature nobody can
        /// judge. The radius is generous because outcrops are scattered thinly — twenty-three over
        /// a 120-cell board — and a playtest where the nearest stone is off in the trees shows
        /// nothing at all.</para>
        /// </summary>
        public int startingMineRadius = 30;

        /// <summary>
        /// How many of the starting colonists take mining as their first call, the rest taking
        /// cutting. Zero leaves every colonist on the default priority, which sends them all to
        /// the trees together.
        /// </summary>
        public int miners = 2;

        /// <summary>
        /// The scene's scenario: the colony has felling work the moment it exists, because there
        /// is no tool to give the order with yet. When the UI line's designate tool lands, the
        /// scene moves to <see cref="Bare"/> and the player gives the first order.
        /// </summary>
        public static ScenarioDef Playtest() =>
            new ScenarioDef { defName = "Scenario_Playtest", label = "playtest" };

        /// <summary>
        /// The same colony with no standing orders. Bare of orders, not of trees: the terrain is
        /// the map generator's business. What the headless runs and the tests measure, since an
        /// order nobody gave is not part of the simulation they are proving.
        /// </summary>
        public static ScenarioDef Bare() =>
            new ScenarioDef
            {
                defName = "Scenario_Bare", label = "bare",
                startingFellRadius = 0, startingMineRadius = 0, miners = 0,
            };
    }

    /// <summary>
    /// Places a starting colony: colonists, a food store, beds and a stockpile near the map's
    /// start location, as a <see cref="ScenarioDef"/> describes it.
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
        /// The standing orders a scenario starts with, given before the first tick. Returns how
        /// many cells were marked in total; zero when the scenario gives none.
        /// </summary>
        public static int GiveStartingOrders(Designations.DesignationGrid designations, CellRef start, ScenarioDef scenario)
        {
            int marked = 0;
            if (scenario.startingFellRadius > 0)
                marked += DesignateTreesNear(designations, start, scenario.startingFellRadius);
            if (scenario.startingMineRadius > 0)
                marked += DesignateOutcropNear(designations, start, scenario.startingMineRadius);
            return marked;
        }

        /// <summary>
        /// Mark the nearest rock outcrop to the start for mining: find the closest minable stone
        /// standing at or above the start layer, then mark everything that belongs to the same
        /// lump. Returns how many cells were marked.
        ///
        /// <para>Above the start layer, so this only ever finds an <em>outcrop</em> — stone
        /// standing on the ground where a colonist can walk up to it and swing. The rock beneath
        /// the subsoil is out of reach until somebody has dug a way down, and marking a cell
        /// nobody can reach would give the colony an order it can never take.</para>
        ///
        /// <para>The lump is taken as everything minable within
        /// <see cref="OutcropLumpRadius"/> of the first cell found, which matches how the outcrop
        /// pass builds one — a tapering mound of radius one to three. It is a scenario choice
        /// rather than a player command, so it writes the grid directly rather than queueing
        /// intents.</para>
        /// </summary>
        public static int DesignateOutcropNear(Designations.DesignationGrid designations, CellRef start, int radius)
        {
            GridSize size = designations.Size;

            int found = -1, foundDistance = int.MaxValue;
            for (int y = start.Y - 1; y < size.SizeY; y++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = start.X + dx, z = start.Z + dz;
                if (!size.Contains(x, z, y)) continue;

                int index = size.Index(x, z, y);
                if (!designations.IsMinableStone(index)) continue;

                int distance = System.Math.Abs(dx) + System.Math.Abs(dz) + System.Math.Abs(y - start.Y);
                if (distance >= foundDistance) continue;
                foundDistance = distance;
                found = index;
            }

            if (found < 0) return 0;

            CellRef at = size.FromIndex(found);
            int marked = 0;
            for (int dy = -OutcropLumpRadius; dy <= OutcropLumpRadius; dy++)
            for (int dz = -OutcropLumpRadius; dz <= OutcropLumpRadius; dz++)
            for (int dx = -OutcropLumpRadius; dx <= OutcropLumpRadius; dx++)
            {
                int x = at.X + dx, z = at.Z + dz, y = at.Y + dy;
                if (!size.Contains(x, z, y)) continue;
                if (y < start.Y - 1) continue;
                if (!designations.IsMinableStone(size.Index(x, z, y))) continue;
                if (designations.Designate(new CellRef(x, z, y), Designations.DesignationKind.Mine) == IntentRejection.None)
                    marked++;
            }

            return marked;
        }

        /// <summary>How far from its first cell an outcrop is taken to extend. The pass makes them 1 to 3.</summary>
        public const int OutcropLumpRadius = 3;

        /// <summary>
        /// Mark every tree within <paramref name="radius"/> cells of the start for felling, on the
        /// start layer. A scenario choice rather than a player command, so it writes the grid
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


        /// <summary>
        /// Give a colonist a trade: the first <see cref="ScenarioDef.miners"/> of them favour
        /// mining, the rest favour cutting.
        ///
        /// <para><b>Why this exists at all.</b> Every work priority starts at 3, and the givers are
        /// scanned in work-type order — cutting, then mining, then hauling. So a colony of five
        /// identical colonists all go to the trees, and the stone is not touched until the last
        /// tree within reach is down. Watching both happen at once is the whole point of having
        /// two kinds of work, and until the player can set priorities from the interface the
        /// scenario has to do it, exactly as it has to give the first orders.</para>
        ///
        /// <para>The trade is a priority, not a restriction: a miner with no reachable rock left
        /// still fells, hauls and eats. Priority 1 is scanned before 2, so the split decides what
        /// a colonist reaches for first and nothing else.</para>
        /// </summary>
        static void AssignTrade(Pawn colonist, int index, ScenarioDef scenario)
        {
            if (scenario.miners <= 0) return;

            bool miner = index < scenario.miners;
            colonist.WorkPriorities[WorkTypeIndex.Mining] = (byte)(miner ? 1 : 3);
            colonist.WorkPriorities[WorkTypeIndex.Cutting] = (byte)(miner ? 3 : 1);
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
        public static Result Place(CellGrid grid, PawnContext pawns, CellRef start, uint seed, ScenarioDef scenario)
        {
            int wanted = scenario.colonists + scenario.mealPiles + scenario.beds + scenario.stockpileCells + 4;
            var spots = FindStartSpots(grid, start, wanted);
            if (spots.Count == 0) return new Result(0, 0, 0, 0, 0, 0);

            var rng = DeterministicRandom.ForTick(seed, 0, purpose: 0xC0101);
            int take = 0;

            int placedColonists = 0;
            for (int i = 0; i < scenario.colonists && take < spots.Count; i++, take++, placedColonists++)
            {
                // Passions come from the seed and the pawn's own id, not from this placement
                // stream, so rolling them does not move the salvage that is scattered below.
                Pawn colonist = pawns.Pawns.Spawn(spots[take]);
                colonist.RollPassions(seed);
                AssignTrade(colonist, i, scenario);
            }

            int placedMeals = 0;
            for (int i = 0; i < scenario.mealPiles && take < spots.Count; i++, take++, placedMeals++)
                pawns.Items.Spawn(ItemIndex.Meal, spots[take], stack: scenario.mealsPerPile);

            int placedBeds = 0;
            for (int i = 0; i < scenario.beds && take < spots.Count; i++, take++, placedBeds++)
                pawns.Items.AddBed(spots[take]);

            var stockpile = new List<int>(scenario.stockpileCells);
            for (int i = 0; i < scenario.stockpileCells && take < spots.Count; i++, take++) stockpile.Add(spots[take]);
            if (stockpile.Count > 0)
            {
                var allow = new bool[ItemIndex.Count];
                for (int i = 0; i < allow.Length; i++) allow[i] = true;
                pawns.Items.AddStockpile(new Stockpile(priority: 2, stockpile.ToArray(), allow));
            }

            // Loose salvage so hauling has work from the first tick. A draw that lands on a
            // cell already holding something — a meal pile, or an earlier piece of salvage,
            // which does not stack — is drawn again, because two things cannot share a cell.
            // It used to spawn straight onto whatever was there and corrupt the cell index.
            int placedSalvage = 0;
            for (int i = 0; i < scenario.salvage; i++)
            {
                for (int attempt = 0; attempt < spots.Count; attempt++)
                {
                    int spot = spots[rng.NextInt(spots.Count)];
                    if (!pawns.Items.CellHasSpace(spot, ItemIndex.Salvage, 1)) continue;
                    pawns.Items.Spawn(ItemIndex.Salvage, spot);
                    placedSalvage++;
                    break;
                }
            }

            return new Result(placedColonists, placedMeals, placedBeds, stockpile.Count, placedSalvage, spots.Count);
        }
    }
}
