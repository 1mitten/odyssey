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
        /// How many outcrops near the start are marked for mining.
        ///
        /// <para>One was not enough by a long way. A single outcrop is about a dozen cells; the
        /// colony worked through it in six game-hours and then never mined again, while the trees
        /// beside it kept five colonists busy all day. Three gives mining roughly the standing the
        /// felling order has, which is "enough to watch" rather than "enough to finish"
        /// (owner, 2026-09-16: no stone was appearing on the floor at all).</para>
        /// </summary>
        public int startingMineOutcrops = 3;

        /// <summary>
        /// How many of the starting colonists take mining as their first call, the rest taking
        /// cutting. Zero leaves every colonist on the default priority, which sends them all to
        /// the trees together.
        /// </summary>
        public int miners = 2;

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
        /// The scene's scenario: a colony with people, food, beds and a store, and **no standing
        /// orders** — nothing is felled or mined until the player says so.
        ///
        /// <para><b>This is the change this comment used to promise</b> (owner, 2026-09-17: *"at
        /// the start of game there are no orders, until you assign them for the time being"*). It
        /// read: *"the colony has felling work the moment it exists, because there is no tool to
        /// give the order with yet. When the UI line's designate tool lands, the scene moves to
        /// Bare and the player gives the first order."* The designate tool landed in M3, the
        /// cancel tool and the Build palette after it, and this was never taken — so a new colony
        /// arrived with a ring of trees already marked and colonists walking off to chop them
        /// before the player had touched anything. **A note saying what to do when a thing lands
        /// does not do it**; this one outlived the condition it was waiting on by a milestone.</para>
        ///
        /// <para><b>It is not the same as <see cref="Bare"/>, and the difference is deliberate.</b>
        /// Bare also flattens <see cref="miners"/>, which is not an order but an inclination — who
        /// reaches for a pick rather than an axe when work does appear. A colony where nobody
        /// favours mining is a different colony; one where nobody has been *told* to mine yet is
        /// this one on its first morning.</para>
        ///
        /// <para>No golden moves: the golden table builds on <see cref="Bare"/>, which has had no
        /// orders since it was written.</para>
        /// </summary>
        public static ScenarioDef Playtest() =>
            new ScenarioDef
            {
                defName = "Scenario_Playtest", label = "playtest",
                startingFellRadius = 0, startingMineRadius = 0, startingMineOutcrops = 0,
            };

        /// <summary>
        /// The same colony with no standing orders. Bare of orders, not of trees: the terrain is
        /// the map generator's business. What the headless runs and the tests measure, since an
        /// order nobody gave is not part of the simulation they are proving.
        /// </summary>
        public static ScenarioDef Bare() =>
            new ScenarioDef
            {
                defName = "Scenario_Bare", label = "bare",
                startingFellRadius = 0, startingMineRadius = 0, startingMineOutcrops = 0, miners = 0,
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
            if (scenario.startingMineRadius > 0 && scenario.startingMineOutcrops > 0)
                marked += DesignateOutcropsNear(designations, start,
                    scenario.startingMineRadius, scenario.startingMineOutcrops);
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
        public static int DesignateOutcropsNear(Designations.DesignationGrid designations, CellRef start,
            int radius, int wanted)
        {
            int marked = 0;
            for (int i = 0; i < wanted; i++)
            {
                int got = DesignateOutcropNear(designations, start, radius);
                // Nothing left within reach that is not already ordered: stop rather than spin.
                if (got == 0) break;
                marked += got;
            }
            return marked;
        }

        /// <summary>
        /// Mark the nearest unordered rock outcrop to the start for mining: find the closest
        /// minable stone at or above the start layer that carries no order yet, then mark
        /// everything belonging to the same lump. Returns how many cells were marked.
        ///
        /// <para>Above the start layer, so this only ever finds an <em>outcrop</em> — stone
        /// standing on the ground that a colonist can walk up to and swing at. The rock beneath
        /// the subsoil is out of reach until somebody has dug down to it, and an order nobody can
        /// take is worse than no order.</para>
        ///
        /// <para>It is a scenario choice rather than a player command, so it writes the grid
        /// directly rather than queueing intents.</para>
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
                // Already ordered, so this is a lump a previous pass took.
                if (designations.At(index) != Designations.DesignationKind.None) continue;

                int distance = System.Math.Abs(dx) + System.Math.Abs(dz) + System.Math.Abs(y - start.Y);
                if (distance >= foundDistance) continue;
                foundDistance = distance;
                found = index;
            }

            if (found < 0) return 0;

            CellRef at = size.FromIndex(found);
            int count = 0;
            for (int dy = -OutcropLumpRadius; dy <= OutcropLumpRadius; dy++)
            for (int dz = -OutcropLumpRadius; dz <= OutcropLumpRadius; dz++)
            for (int dx = -OutcropLumpRadius; dx <= OutcropLumpRadius; dx++)
            {
                int x = at.X + dx, z = at.Z + dz, y = at.Y + dy;
                if (!size.Contains(x, z, y)) continue;
                if (y < start.Y - 1) continue;
                if (!designations.IsMinableStone(size.Index(x, z, y))) continue;
                if (designations.Designate(new CellRef(x, z, y), Designations.DesignationKind.Mine) == IntentRejection.None)
                    count++;
            }

            return count;
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
        /// <param name="chosen">
        /// One roll seed per colonist the player picked on the select screen (U40), in the order
        /// they were shown, or null for a colony nobody chose.
        ///
        /// <para><b>Null is the ordinary case and it is not a degraded one</b>: every headless run,
        /// every test and every scenario placed by the world itself passes null, and each colonist
        /// then keeps the world seed <see cref="PawnRegistry.Spawn"/> gave it — which is exactly
        /// what those colonies rolled before pawns had seeds of their own. A list shorter than
        /// <see cref="ScenarioDef.colonists"/> covers the ones it names and leaves the rest on the
        /// world seed, rather than refusing: it is the scenario that decides how many colonists
        /// there are, and this only decides who some of them are.</para>
        /// </param>
        public static Result Place(CellGrid grid, PawnContext pawns, CellRef start, uint seed,
            ScenarioDef scenario, IReadOnlyList<uint>? chosen = null)
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
                // Main's storey-aware spot, this branch's trade split: a scenario now says
                // which floor a colonist starts on AND which of them mine rather than cut.
                Pawn colonist = pawns.Pawns.Spawn(spot);
                // A colonist chosen on the select screen brings its own seed (U40); one the world
                // placed by itself keeps the world's, which Spawn has already given it. The order
                // matters — the seed must be in place before either roll reads it.
                if (chosen != null && i < chosen.Count) colonist.RollSeed = chosen[i];
                colonist.RollPassions();
                AssignTrade(colonist, i, scenario);
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
