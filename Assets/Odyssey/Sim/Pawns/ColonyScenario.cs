#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

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
        /// Which storey the meals go on, counted from the start layer. Zero is the layer the
        /// colony wakes up on, which is where everything went before these existed.
        ///
        /// <para>A scenario says <i>where</i> as well as <i>how much</i> because on a map with
        /// storeys the two are not separable: a colony whose food, beds and store are all on the
        /// floor it wakes up on never uses a stair, and a run of that colony cannot demonstrate
        /// that stairs work (OQ-47, and M2's central claim). An offset rather than an absolute
        /// layer, because a scenario does not know where the generator will put the start.</para>
        /// </summary>
        public int mealLayerOffset = 0;

        /// <summary>Which storey the beds go on, counted from the start layer.</summary>
        public int bedLayerOffset = 0;

        /// <summary>Which storey the stockpile goes on, counted from the start layer.</summary>
        public int stockpileLayerOffset = 0;

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
            new ScenarioDef { defName = "Scenario_Bare", label = "bare", startingFellRadius = 0 };
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
        /// many trees were marked; zero when the scenario gives none.
        /// </summary>
        public static int GiveStartingOrders(Designations.DesignationGrid designations, CellRef start, ScenarioDef scenario) =>
            scenario.startingFellRadius > 0
                ? DesignateTreesNear(designations, start, scenario.startingFellRadius)
                : 0;

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
        public static List<int> FindStartSpots(CellGrid grid, CellRef start, int wanted, int maxRadius = 24,
            int layerSpread = 2, Filter? filter = null)
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
                        if (TryTake(grid, size, x, z, start.Y + spread, spots, filter)) break;
                        if (spread != 0 && TryTake(grid, size, x, z, start.Y - spread, spots, filter)) break;
                    }
                }
            }
            return spots;
        }

        /// <summary>
        /// An extra condition a spot must meet, beyond standing on something walkable and dry.
        /// Null is the search as it has always been.
        ///
        /// <para>It exists because a storey a scenario <i>names</i> needs two guarantees the
        /// start layer never did: that a cell already handed to an earlier group is not handed
        /// out twice, and that the storey can actually be walked to — a bed stamped inside a
        /// sealed shell two floors up is a colonist who never sleeps, and the day's run would
        /// report it as a mood failure rather than as a placement one.</para>
        /// </summary>
        public delegate bool Filter(int cellIndex);

        static bool TryTake(CellGrid grid, GridSize size, int x, int z, int y, List<int> spots, Filter? filter)
        {
            if (!size.Contains(x, z, y)) return false;
            int index = size.Index(x, z, y);
            if (!grid.IsWalkable(index)) return false;
            if (filter != null && !filter(index)) return false;

            // Walkable is not enough. Shallow water can be waded, so it passes the test above,
            // and a bed or a stockpile would be placed standing in a stream. Deep water is
            // already excluded by walkability; this is the half that is not obvious.
            if (NaturalContent.IsWater(grid.Terrain[index])) return false;

            spots.Add(index);
            return true;
        }

        /// <summary>The storey the colonists themselves wake up on: the one the generator chose.</summary>
        const int ColonistStorey = 0;

        /// <summary>
        /// Spare spots per storey, so a pile that lands on an awkward cell is not the last one.
        /// Four is what the single-list search always asked for.
        /// </summary>
        const int Slack = 4;

        /// <summary>
        /// The spots a placement has to hand, grouped by the storey they were asked for.
        ///
        /// <para>One search per distinct storey rather than one search widened to cover them all,
        /// because the two searches want opposite things. The colonists' own storey wants the
        /// nearest walkable cell whatever layer it is on — natural terrain is terraced, and a
        /// search pinned to one layer finds a scatter rather than a clearing. A storey a scenario
        /// <i>named</i> wants that layer and no other, or it has not been honoured.</para>
        ///
        /// <para>When every offset is zero there is exactly one group, asked for exactly the
        /// number the single list used to be asked for, so a scenario that names no storey places
        /// its colony cell for cell where it always did. <c>ScenarioDefTests</c> pins that.</para>
        /// </summary>
        sealed class Storeys
        {
            readonly CellGrid grid;
            readonly NavGraph nav;
            readonly CellRef start;
            readonly List<int> order = new List<int>(4);
            readonly Dictionary<int, int> demand = new Dictionary<int, int>();
            readonly Dictionary<int, List<int>> spots = new Dictionary<int, List<int>>();
            readonly Dictionary<int, int> cursor = new Dictionary<int, int>();
            readonly HashSet<int> taken = new HashSet<int>();

            public Storeys(CellGrid grid, NavGraph nav, CellRef start)
            {
                this.grid = grid;
                this.nav = nav;
                this.start = start;
            }

            /// <summary>How many spots a storey has been found, across all of them.</summary>
            public int Found { get; private set; }

            public void Want(int offset, int count)
            {
                // The colonists' own storey is asked for even when it is asked for nothing: the
                // salvage is scattered over it, and a colony of none is still a colony.
                if (count <= 0 && offset != ColonistStorey) return;
                if (count < 0) count = 0;
                if (!demand.ContainsKey(offset))
                {
                    order.Add(offset);
                    demand[offset] = 0;
                }
                demand[offset] += count;
            }

            /// <summary>
            /// Search each storey once, in the order it was first asked for, so that two groups
            /// contending for one cell resolve the same way on every run.
            /// </summary>
            public void Search()
            {
                foreach (int offset in order)
                {
                    var origin = new CellRef(start.X, start.Z, start.Y + offset);
                    List<int> found = grid.Size.Contains(origin.X, origin.Z, origin.Y)
                        ? FindStartSpots(grid, origin, demand[offset] + Slack,
                            layerSpread: offset == ColonistStorey ? 2 : 0,
                            filter: Free(offset))
                        : new List<int>();

                    foreach (int index in found) taken.Add(index);
                    spots[offset] = found;
                    cursor[offset] = 0;
                    Found += found.Count;
                }
            }

            /// <summary>
            /// The next unclaimed spot on a storey, or -1 when that storey has run out. A
            /// shortfall is reported rather than filled from elsewhere: a scenario that asks for
            /// beds two floors up and gets them on the ground floor has been quietly disobeyed,
            /// and the run that follows would prove the wrong thing.
            /// </summary>
            public int Next(int offset)
            {
                if (!spots.TryGetValue(offset, out List<int>? list)) return -1;
                int at = cursor[offset];
                if (at >= list.Count) return -1;
                cursor[offset] = at + 1;
                return list[at];
            }

            public List<int> On(int offset) =>
                spots.TryGetValue(offset, out List<int>? list) ? list : new List<int>();

            /// <summary>
            /// A cell no earlier storey has claimed — and, off the colonists' own storey, one
            /// they can walk to. Reachability is not asked of the home storey because that is
            /// where they stand: the question answers itself, and asking it would move a
            /// placement that has been the same since the colony first spawned.
            /// </summary>
            Filter Free(int offset)
            {
                if (offset == ColonistStorey) return index => !taken.Contains(index);

                int anchor = Anchor();
                return index => !taken.Contains(index) &&
                                nav.Reachable(anchor, index, TraverseMode.Colonist);
            }

            /// <summary>
            /// Where "can be walked to" is measured from: a cell a colonist actually stands on,
            /// falling back to the start cell when none has been found yet. The start cell of a
            /// stamped city map is not always walkable itself.
            /// </summary>
            int Anchor()
            {
                List<int> home = On(ColonistStorey);
                return home.Count > 0 ? home[0] : grid.Size.Index(start);
            }
        }

        /// <summary>
        /// Place the colony. Returns what it managed, so the caller can assert rather than assume.
        /// </summary>
        public static Result Place(CellGrid grid, PawnContext pawns, CellRef start, uint seed, ScenarioDef scenario)
        {
            var storeys = new Storeys(grid, pawns.Nav, start);
            storeys.Want(ColonistStorey, scenario.colonists);
            storeys.Want(scenario.mealLayerOffset, scenario.mealPiles);
            storeys.Want(scenario.bedLayerOffset, scenario.beds);
            storeys.Want(scenario.stockpileLayerOffset, scenario.stockpileCells);
            storeys.Search();

            List<int> home = storeys.On(ColonistStorey);
            if (storeys.Found == 0) return new Result(0, 0, 0, 0, 0, 0);

            var rng = DeterministicRandom.ForTick(seed, 0, purpose: 0xC0101);

            int placedColonists = 0;
            for (int i = 0; i < scenario.colonists; i++)
            {
                int spot = storeys.Next(ColonistStorey);
                if (spot < 0) break;
                // Passions come from the seed and the pawn's own id, not from this placement
                // stream, so rolling them does not move the salvage that is scattered below.
                pawns.Pawns.Spawn(spot).RollPassions(seed);
                placedColonists++;
            }

            int placedMeals = 0;
            for (int i = 0; i < scenario.mealPiles; i++)
            {
                int spot = storeys.Next(scenario.mealLayerOffset);
                if (spot < 0) break;
                pawns.Items.Spawn(ItemIndex.Meal, spot, stack: scenario.mealsPerPile);
                placedMeals++;
            }

            int placedBeds = 0;
            for (int i = 0; i < scenario.beds; i++)
            {
                int spot = storeys.Next(scenario.bedLayerOffset);
                if (spot < 0) break;
                pawns.Items.AddBed(spot);
                placedBeds++;
            }

            var stockpile = new List<int>(scenario.stockpileCells);
            for (int i = 0; i < scenario.stockpileCells; i++)
            {
                int spot = storeys.Next(scenario.stockpileLayerOffset);
                if (spot < 0) break;
                stockpile.Add(spot);
            }
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
                for (int attempt = 0; attempt < home.Count; attempt++)
                {
                    int spot = home[rng.NextInt(home.Count)];
                    if (!pawns.Items.CellHasSpace(spot, ItemIndex.Salvage, 1)) continue;
                    pawns.Items.Spawn(ItemIndex.Salvage, spot);
                    placedSalvage++;
                    break;
                }
            }

            return new Result(placedColonists, placedMeals, placedBeds, stockpile.Count, placedSalvage, storeys.Found);
        }
    }
}
