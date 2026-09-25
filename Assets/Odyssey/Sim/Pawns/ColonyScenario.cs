#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
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

        /// <summary>
        /// Are this scenario's colonists dealt traits (design 44 §5f)? True for every scenario a
        /// player starts. A test fixture about something else — how a floor is fed, how a sower
        /// kneels, what a level-twenty miner digs in a tick — sets it false, so a colonist dealt
        /// <i>Ham-fisted</i> or <i>Tireless</i> by her seed cannot make that test about traits.
        /// </summary>
        public bool traits = true;

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

        /// <summary>
        /// Cells of storage the colony starts with. <b>Zero</b> (owner, 2026-09-20: <i>"there
        /// shouldn't be a default stockpile zone"</i>).
        ///
        /// <para>It was nine, and for most of the project's life that was invisible — nothing drew
        /// a stockpile, so nobody ever saw the zone every colony was given. S1 made it visible and
        /// the answer was immediate: a colony arrives with nothing marked and nothing zoned, and
        /// the player draws their first store where they want it. The same call as
        /// <c>ScenarioDef.Playtest</c> giving no starting orders (2026-09-17).</para>
        ///
        /// <para>The machinery stays rather than going with it, exactly as the felling and mining
        /// radii did: a scenario may ask for a starting store, and a later one almost certainly
        /// will — a "prepared site" start is the obvious use — so it stays under test with a
        /// scenario that asks.</para>
        /// </summary>
        public int stockpileCells;

        /// <summary>Loose salvage scattered about, so hauling has work from the first tick.</summary>
        public int salvage = 8;

        /// <summary>
        /// Piles of stone and of wood laid out beside the food, and how much is in each.
        ///
        /// <para><b>Zero by default, so <see cref="Bare"/> is untouched.</b> The golden table
        /// builds on Bare, and a starting kit that moved it would re-bake three hashes to say
        /// nothing about the simulation. These are a <see cref="Playtest"/> concern: what a player
        /// can build with before anybody has swung an axe.</para>
        ///
        /// <para>Both stack to 75 (<c>Items.xml</c>), so a full pile is one square rather than a
        /// scatter — the same reason the meals go out in piles rather than singly.</para>
        /// </summary>
        public int stonePiles = 0;

        /// <inheritdoc cref="stonePiles"/>
        public int stonePerPile = 75;

        /// <inheritdoc cref="stonePiles"/>
        public int woodPiles = 0;

        /// <inheritdoc cref="stonePiles"/>
        public int woodPerPile = 75;

        /// <summary>
        /// Piles of medical supplies beside the food (design 37 §5): what the first fight is
        /// treated with. Zero by default on the same terms as <see cref="stonePiles"/>, so
        /// <see cref="Bare"/> and every golden are untouched; <see cref="Playtest"/> sets one pile
        /// of six (owner, 2026-09-24).
        /// </summary>
        public int medicalPiles = 0;

        /// <inheritdoc cref="medicalPiles"/>
        public int medicalPerPile = 6;

        /// <summary>
        /// Piles of scrap metal left lying about the board — old wreckage — per ten thousand
        /// columns (design 32 §14). Zero by default, so <see cref="Bare"/> and every golden built
        /// on it are untouched; <see cref="Playtest"/> sets it.
        /// </summary>
        public int wreckagePer10kColumns;

        /// <summary>The fewest and most scrap metal in one pile of wreckage.</summary>
        public int wreckageMin = 10;
        public int wreckageMax = 25;

        /// <summary>
        /// How far from the start, in cells either way, the nearest wreckage may lie: the scrap is
        /// out in the world to be fetched, not a starting kit under the colonists' feet.
        /// </summary>
        public int wreckageClearance = 15;

        /// Weapons laid on the ground beside the food, one to a cell, as item def indices (design
        /// 33 §1: "one or two in the starting kit", §6D). On the ground and in nobody's hand: the
        /// player decides who fights with what.
        ///
        /// <para><b>Empty by default, so <see cref="Bare"/> is untouched</b> — the same terms as
        /// <see cref="stonePiles"/>, and for the same reason: every golden builds on Bare, and an
        /// empty kit asks the storey search for no more spots than it did before weapons
        /// existed.</para>
        /// </summary>
        public int[] startingWeapons = System.Array.Empty<int>();

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
        ///
        /// <para><b>The starting kit is a building kit now, not a pantry</b> (owner, 2026-09-18:
        /// *"don't start the game with any scrap and only 3 meal piles, but a couple piles of
        /// stone and wood"*). It was 144 meals and eight pieces of scrap and nothing to build
        /// with, which is the wrong shape for a colony sim's first hour: food was a non-question
        /// for a fortnight, the only hauling job on the board was scrap nobody had a use for, and
        /// a player who wanted to put up a wall had to fell a tree first. It is now 36 meals —
        /// three or four days, so food becomes a question rather than an emergency — no scrap at
        /// all, and 150 each of stone and wood, which is a hut and a few floors without waiting on
        /// anybody's axe. <b>The numbers live here and only here</b>, and they are invited tuning:
        /// nothing else in the game reads them and nothing derives from them.</para>
        /// </summary>
        public static ScenarioDef Playtest() =>
            new ScenarioDef
            {
                defName = "Scenario_Playtest", label = "playtest",
                startingFellRadius = 0, startingMineRadius = 0, startingMineOutcrops = 0,
                mealPiles = 3, salvage = 0,
                stonePiles = 2, woodPiles = 2,
                // One stack of six medical supplies (design 37, owner's number).
                medicalPiles = 1,
                // Old wreckage scattered over the board, the colony's first scrap metal and the
                // only source of it until a scrap drop falls (design 32 §14). Seven piles per ten
                // thousand columns is ten on the played 120 x 120 board.
                wreckagePer10kColumns = 7,
                // A blunt one and a sharp one (design 33 §6D), so the first fight shows both a
                // stun and the quicker blade. INVENTED inside the owner's "one or two".
                startingWeapons = new[] { ItemIndex.Bat, ItemIndex.Machete },
                // No beds (owner, 2026-09-20: "beds should never be given on startup / new
                // game"). The colony sleeps on the ground until it builds some, which is what
                // makes a bed the first thing worth building. Bare keeps its five: the tests
                // and the goldens baseline on a colony that is rested.
                beds = 0,
            };

        /// <summary>
        /// This scenario with a different number of colonists (U40).
        ///
        /// <para>A copy rather than an assignment, because <see cref="Playtest"/> and
        /// <see cref="Bare"/> hand out fresh objects but a caller may perfectly well be holding one
        /// that something else also holds — a scenario is a Def, and a Def is shared. The select
        /// screen changing the population of every other colony in the process would be a memorable
        /// afternoon.</para>
        /// </summary>
        public virtual ScenarioDef WithColonists(int count)
        {
            var copy = (ScenarioDef)MemberwiseClone();
            copy.colonists = count < 0 ? 0 : count;
            return copy;
        }

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

            /// <summary>Piles of stone and wood laid out, counted together: what the colony can
            /// build with before it has worked for anything.</summary>
            public readonly int MaterialPiles;

            public readonly int SpotsFound;

            /// <summary>Weapons laid on the ground: the starting kit (design 33 §6D).</summary>
            public readonly int Weapons;

            public Result(int colonists, int meals, int beds, int stockpileCells, int salvage,
                int materialPiles, int spotsFound, int weapons = 0)
            {
                Colonists = colonists;
                Meals = meals;
                Beds = beds;
                StockpileCells = stockpileCells;
                Salvage = salvage;
                MaterialPiles = materialPiles;
                SpotsFound = spotsFound;
                Weapons = weapons;
            }

            public override string ToString() =>
                $"{Colonists} colonists, {Meals} meals, {Beds} beds, {StockpileCells} stockpile cells, " +
                $"{Salvage} salvage, {MaterialPiles} material piles, {Weapons} weapons, from {SpotsFound} spots";
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

        /// <summary>
        /// Stand a real bed at a starting spot, in whichever of the four facings the cell beside
        /// it will take. False when none of them will, and the colony simply starts one bed short.
        ///
        /// <para><b>It used to be <c>Items.AddBed(spot)</c> and nothing else</b>, which put a
        /// <i>cell</i> in the sleep chooser's list with no bed standing in it. That was right when
        /// it was written — a bed was a property of a cell and there was nothing to build — and it
        /// became a bug the day beds became furniture, because everything a bed now is hangs off
        /// the record rather than the cell: it cannot be seen (nothing is drawn there), it cannot
        /// be owned (<c>AssignOwnerAt</c> refuses a cell with no edifice in it), it cannot be
        /// taken apart, and — the one the owner photographed — a colonist who sleeps in it is laid
        /// out by the <i>ground</i> pose, flat on the grass at whatever angle she last faced.
        /// Beside a real bed that reads as a colonist hanging half off it (owner, 2026-09-20).</para>
        ///
        /// <para><b>Measured before it was changed.</b> Three colonists, three built beds, three
        /// days: with the scenario's five phantom cells in the list, one colonist spent every one
        /// of her 53,222 sleeping ticks off a bed and a second spent 17,399 of hers off one, all
        /// within three cells of a real bed she never used. With the phantom cells gone, all three
        /// slept on a bed for every tick. <c>BedTests.EveryCellTheSleepChooserKnowsHasABedInIt</c> holds it.</para>
        ///
        /// <para>Normal quality, deliberately: a qualityless bed cell restored 100 per cent and so
        /// does a Normal bed, so the colony wakes up exactly as rested as it always did. And
        /// <c>Raise</c> puts the head cell in the chooser's list itself, which is why this method
        /// does not — one owner for "what counts as a bed", which is the whole of the fix.</para>
        /// </summary>
        static bool RaiseAStartingBed(
            PawnContext pawns, CellGrid grid, int head, System.Func<int, bool> spoken)
        {
            ConstructionGrid? sites = pawns.Construction;
            if (sites == null) return false;

            // **Eight footprints, not four.** The spot can be the head of the bed or its foot,
            // and both put the bed on the storey the scenario named. Four was measurably not
            // enough: on the ruined city, where a storey is rooms rather than open ground, two of
            // five spots had no free neighbour in the direction a head would need and the colony
            // started three beds short.
            //
            // Four, because a facing is two bits — `EdificeFootprint.SecondCell` masks it to
            // exactly that. `Directions` itself is Presentation's and this assembly has no
            // UnityEngine in it by design.
            //
            // **Two passes over the eight.** The first skips any footprint whose far cell is a
            // spot another group has been promised — see <c>Storeys.Spoken</c> for why a bed
            // standing on the stockpile is worse than a bed one cell further along. The second
            // takes what is left, because a bed on a crowded storey is still better than no bed.
            GridSize size = grid.Size;
            for (int pass = 0; pass < 2; pass++)
            for (int facing = 0; facing < 4; facing++)
            for (int asFoot = 0; asFoot < 2; asFoot++)
            {
                // Laying the bed the other way round is the same footprint approached from the
                // far end: the head goes one cell back along the facing, which is the cell whose
                // second cell is the spot. `SecondCell` of the opposite facing is that cell.
                int at = asFoot == 0
                    ? head
                    : EdificeFootprint.SecondCell(head, CoreContent.EdificeBed, (facing + 2) & 3, size);
                if (at < 0) continue;

                int far = EdificeFootprint.SecondCell(at, CoreContent.EdificeBed, facing, size);
                if (pass == 0 && (far < 0 || spoken(far))) continue;

                // Place answers for both cells — a two-cell order whose far half is refused is
                // refused whole — so trying it is the cheapest way to ask, and the only way that
                // cannot disagree with what the player's own order would have been told.
                if (sites.Place(size.FromIndex(at), BuildingHandle.Bed, StuffHandle.Wood, facing)
                    != IntentRejection.None)
                    continue;

                sites.Raise(pawns, at, (byte)QualityHandle.Normal);
                return true;
            }

            return false;
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
            /// Has this cell been handed to a storey group — any of them, on any floor?
            ///
            /// <para><b>Asked by the bed, and by nothing else</b>, because the bed is the one
            /// thing the scenario places that is wider than the spot it was given. Every group is
            /// searched up front, so by the time a bed is raised the stockpile's cells are already
            /// chosen and adding the bed's far cell to <see cref="taken"/> would be too late to
            /// matter. Asking instead lets the bed pick a footprint that stays off them — which is
            /// not a nicety: a bed claims its cells against items, so a stockpile cell under a
            /// bed's foot is a stockpile cell nothing can ever be put in, and the colony's hauling
            /// quietly stalls two crates short. Measured by
            /// <c>ForbidIntentTests.AForbiddenThingIsNotHauledUntilAllowed</c>, which is what
            /// caught it.</para>
            /// </summary>
            public bool Spoken(int cell) => taken.Contains(cell);

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
            // The material piles sit on the food's storey and come out of the same budget: they are
            // the same kind of thing — a pile on the ground the colony wakes up beside — and asking
            // for their spots separately would be a second answer to "which floor does the colony
            // start on" that nothing keeps in step with the first.
            int weapons = scenario.startingWeapons?.Length ?? 0;
            storeys.Want(scenario.mealLayerOffset,
                scenario.mealPiles + scenario.stonePiles + scenario.woodPiles
                + scenario.medicalPiles + weapons);
            storeys.Want(scenario.bedLayerOffset, scenario.beds);
            storeys.Want(scenario.stockpileLayerOffset, scenario.stockpileCells);
            storeys.Search();

            List<int> home = storeys.On(ColonistStorey);
            if (storeys.Found == 0) return new Result(0, 0, 0, 0, 0, 0, 0);

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

            // Stone and wood, in full stacks beside the food. Placed before the beds so that a
            // cramped start spends its spots on the things a player reaches for first; the storey
            // search is ordered, so "before" here is a priority and not a coincidence.
            int placedMaterials = 0;
            placedMaterials += PlacePiles(pawns, storeys, scenario.mealLayerOffset,
                ItemIndex.Stone, scenario.stonePiles, scenario.stonePerPile);
            placedMaterials += PlacePiles(pawns, storeys, scenario.mealLayerOffset,
                ItemIndex.Wood, scenario.woodPiles, scenario.woodPerPile);
            placedMaterials += PlacePiles(pawns, storeys, scenario.mealLayerOffset,
                ItemIndex.MedicalSupplies, scenario.medicalPiles, scenario.medicalPerPile);

            // The kit's weapons, after the materials and on the same storey: one a cell, on the
            // ground, in nobody's hand (design 33 §6D).
            int placedWeapons = 0;
            for (int i = 0; i < weapons; i++)
                placedWeapons += PlacePiles(pawns, storeys, scenario.mealLayerOffset,
                    scenario.startingWeapons![i], piles: 1, perPile: 1);

            // Until a spot works or the storey runs out, rather than one attempt per bed: a bed
            // wants two cells and a spot is one, so a spot whose neighbours are all walls buys
            // nothing and the colony should try the next one rather than start a bed short. The
            // storey search hands out spares for exactly this.
            int placedBeds = 0;
            while (placedBeds < scenario.beds)
            {
                int spot = storeys.Next(scenario.bedLayerOffset);
                if (spot < 0) break;
                if (RaiseAStartingBed(pawns, grid, spot, storeys.Spoken)) placedBeds++;
            }

            var stockpile = new List<int>(scenario.stockpileCells);
            for (int i = 0; i < scenario.stockpileCells; i++)
            {
                int spot = storeys.Next(scenario.stockpileLayerOffset);
                if (spot < 0) break;
                stockpile.Add(spot);
            }
            if (stockpile.Count > 0 && pawns.Storage != null)
            {
                // Through the zones rather than around them, and at the anchor the colony's first
                // cell names: the scenario's zone is an ordinary zone from the first tick, with an
                // ordinary settings record at Normal accepting everything — which is what a zone a
                // player draws is too (decision 22). It used to be built by hand with its own
                // priority integer and its own filter array, and it was the only zone in the game
                // that nothing could edit.
                //
                // **And it is now drawn**, which nothing about the starting zone ever was. Every
                // colony ever made has had one and nobody has seen it.
                // A spot the gate refuses is simply left out, and on the wooded board exactly one
                // is: cell 180436 of the played golden has a tree standing in it. That cell was
                // in the zone before this unit — `AddStockpile` asked nothing of a cell — so a
                // starting item that landed on it counted as "stored" in a place nothing could
                // ever be stored. It is loose now, which is what it is, and a hauler collects it.
                int anchor = stockpile[0];
                for (int i = 0; i < stockpile.Count; i++)
                    pawns.Storage.Designate(
                        grid.Size.FromIndex(stockpile[i]), anchor, Storage.StoragePreset.Everything);
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
                    // An empty cell, not merely one with room: scrap metal stacks since power
                    // (design 32 §14), and "room" would now let a second piece join the first
                    // where it used to be drawn again — a different starting kit, and every
                    // golden moved to say so. One piece to a cell is what the scatter has always
                    // placed, so it is what it places.
                    if (pawns.Items.ItemAt(spot) != null
                        || !pawns.Items.CellHasSpace(spot, ItemIndex.Salvage, 1)) continue;
                    pawns.Items.Spawn(ItemIndex.Salvage, spot);
                    placedSalvage++;
                    break;
                }
            }

            // Wreckage last, so a scenario that has none draws nothing from the stream and every
            // placement above is exactly what it was (design 32 §14).
            PlaceWreckage(grid, pawns, start, scenario, ref rng);

            return new Result(placedColonists, placedMeals, placedBeds, stockpile.Count, placedSalvage,
                placedMaterials, storeys.Found, placedWeapons);
        }

        /// <summary>
        /// Scatter piles of scrap metal over the whole board: each on the topmost walkable cell of
        /// a random column, clear of the start by <see cref="ScenarioDef.wreckageClearance"/>, out
        /// of the water, on a cell with room. A column that fails is drawn again, up to a budget,
        /// and a pile that finds nowhere is simply not placed.
        /// </summary>
        static void PlaceWreckage(CellGrid grid, PawnContext pawns, CellRef start, ScenarioDef scenario,
            ref DeterministicRandom rng)
        {
            GridSize size = grid.Size;
            int piles = scenario.wreckagePer10kColumns * size.SizeX * size.SizeZ / 10_000;
            int span = scenario.wreckageMax - scenario.wreckageMin + 1;
            if (piles <= 0 || span <= 0) return;

            for (int p = 0; p < piles; p++)
            {
                int stack = scenario.wreckageMin + rng.NextInt(span);
                for (int attempt = 0; attempt < 64; attempt++)
                {
                    int x = rng.NextInt(size.SizeX), z = rng.NextInt(size.SizeZ);
                    if (System.Math.Abs(x - start.X) < scenario.wreckageClearance
                        && System.Math.Abs(z - start.Z) < scenario.wreckageClearance) continue;

                    int cell = grid.SkyLanding(x, z);
                    if (cell < 0 || NaturalContent.IsWater(grid.Terrain[cell])) continue;
                    if (pawns.Items.ItemAt(cell) != null) continue;

                    pawns.Items.Spawn(ItemIndex.Salvage, cell, stack);
                    break;
                }
            }
        }

        /// <summary>
        /// Lay out <paramref name="piles"/> stacks of one item on a storey, one stack a cell, and
        /// say how many went down.
        ///
        /// <para><b>A pile is clamped to the item's own stack limit, here, because nothing below
        /// does it.</b> <c>ColonyItems.Spawn</c> checks the limit when a cell already holds
        /// something and does not check it at all when the cell is empty — so a scenario asking
        /// for 200 stone would lay down a single stack of 200 that no hauler could ever carry and
        /// no stockpile could ever take back apart. A pile is not split across cells either: a
        /// scenario that wants 150 stone says two piles of 75, which is what the field pair is
        /// for.</para>
        /// </summary>
        static int PlacePiles(PawnContext pawns, Storeys storeys, int layerOffset,
            int item, int piles, int perPile)
        {
            if (piles <= 0 || perPile <= 0) return 0;

            int limit = pawns.Items.Content.Items[item].stackLimit;
            int stack = perPile < limit ? perPile : limit;

            int placed = 0;
            for (int i = 0; i < piles; i++)
            {
                int spot = storeys.Next(layerOffset);
                if (spot < 0) break;
                pawns.Items.Spawn(item, spot, stack: stack);
                placed++;
            }
            return placed;
        }
    }
}
