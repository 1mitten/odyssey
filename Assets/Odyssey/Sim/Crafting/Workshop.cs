#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Cooking;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.Crafting
{
    /// <summary>
    /// A crafting station's state (design 62 §9): its bills, the batch in it, and its hopper.
    ///
    /// <para><b>The batch is the station's, not the crafter's</b> — the kitchen's pan rule (design
    /// 48 §5) carried over whole. Ore put in stays in; work done on a batch is banked here; a
    /// crafter called away leaves the batch for the next, and a save taken mid-batch loses nothing.
    /// So the crafter's job carries nothing that has to survive it.</para>
    ///
    /// <para><b>A batch is locked to its recipe from the first ingredient in.</b> A player who
    /// reorders the bills while a crucible of iron ore is heating gets the iron bars first: ore
    /// already in is ore, and the next bill starts once the station is empty.</para>
    /// </summary>
    public sealed class CraftStation
    {
        /// <summary>The index of the <see cref="PlacedEdifice"/> this station is. The key, as a store's is.</summary>
        public int Edifice;

        /// <summary>The list, top first. The kitchen's <see cref="Bill"/>, because it is the same bill.</summary>
        public readonly List<Bill> Bills = new List<Bill>();

        /// <summary>The <see cref="RecipeHandle"/> the ingredients in are for, or -1 while nothing is in.</summary>
        public int BatchRecipe = -1;

        /// <summary>Units of each of <see cref="BatchRecipe"/>'s ingredients in, in its order. Empty while nothing is in.</summary>
        public int[] In = System.Array.Empty<int>();

        /// <summary>
        /// The batch's fuel has been taken from the hopper. Taken once, when the work starts, so a
        /// hopper running dry part-way never strands a batch half-worked.
        /// </summary>
        public bool FuelPaid;

        /// <summary>Milliwork done on the batch. Nought until the work starts.</summary>
        public int CraftMilliwork;

        /// <summary>What is in the hopper, in the building's worth units (coal 3, wood 1 at the smelter).</summary>
        public int Fuel;

        /// <summary>The station came down. The slot is kept, so ids stay stable.</summary>
        public bool Removed;

        /// <summary>Is a batch under way: anything in, paid for, or worked.</summary>
        public bool BatchInUse => BatchRecipe >= 0;

        public void EmptyBatch()
        {
            BatchRecipe = -1;
            In = System.Array.Empty<int>();
            FuelPaid = false;
            CraftMilliwork = 0;
        }
    }

    /// <summary>
    /// Every crafting station that has ever had a bill (design 62 §9): the smelter, today. The
    /// kitchen's sibling, not a bend of it — <see cref="Kitchen"/> works recipes that take raw food
    /// by nutrition into a pan; this works <see cref="RecipeDef.Crafted"/> recipes, which name
    /// their ingredients and products item by item and burn fuel from a hopper. The bill itself,
    /// its three modes and its edits are shared (<see cref="Bill"/>, <see cref="BillEdits"/>), so
    /// the pane is one control for both (design 49).
    ///
    /// <para><b>A station is any building a crafted recipe names</b>, created on its first bill
    /// and keyed by edifice index as the kitchen's are, for the reasons <c>StorageUnits</c> gives.</para>
    ///
    /// <para><b>The hopper</b> is the generator's pattern (design 32 §6) with two fuels: what each
    /// is worth is the building's (<see cref="BuildingDef.hopperFuels"/>), what a batch burns is
    /// the recipe's (<see cref="RecipeStation.fuelPerBatch"/>). A hauler fills it below half —
    /// the existing refuel job, which asks <see cref="NeedsRefuel"/> — but only while the station
    /// has a bill to work, so a smelter nobody uses does not draw the colony's wood into it.</para>
    ///
    /// <para><b>Its own save section, so no format bump</b>, and hashed only while it holds
    /// anything, so a colony that never smelts hashes exactly as it did before.</para>
    /// </summary>
    public sealed class Workshop : ITickable, IStateHashable, ISaveable, ISnapshotContributor
    {
        /// <summary>A new bill's target: the kitchen's, so the pane's first number is the same on every station.</summary>
        public const int DefaultTarget = Kitchen.DefaultTarget;

        readonly PawnContext _ctx;
        readonly IReadOnlyList<PlacedEdifice> _edifices;
        readonly List<CraftStation> _stations = new List<CraftStation>();

        /// <summary>Edifice index to slot. Derived, rebuilt on load, and probed — never iterated in a tick.</summary>
        readonly Dictionary<int, int> _atEdifice = new Dictionary<int, int>();

        // Products held, per recipe, counted at most once a tick: every crafter's think asks it
        // for every bill, and the answer cannot change between two thinks in one tick that make
        // nothing. Keyed by recipe, because the smelter has two and they count different bars.
        int[] _countedTick = System.Array.Empty<int>();
        int[] _counted = System.Array.Empty<int>();

        public Workshop(PawnContext ctx, IReadOnlyList<PlacedEdifice> edifices)
        {
            _ctx = ctx ?? throw new System.ArgumentNullException(nameof(ctx));
            _edifices = edifices ?? throw new System.ArgumentNullException(nameof(edifices));
        }

        /// <summary>Every station with state, in the order each was first used; tombstones included.</summary>
        public IReadOnlyList<CraftStation> Stations => _stations;

        /// <summary>The station this edifice is, or null. A tombstoned one answers null.</summary>
        public CraftStation? At(int edifice)
        {
            if (!_atEdifice.TryGetValue(edifice, out int slot)) return null;
            CraftStation station = _stations[slot];
            return station.Removed ? null : station;
        }

        /// <summary>The station standing in this cell, or null.</summary>
        public CraftStation? AtCell(int cell)
        {
            if ((uint)cell >= (uint)_ctx.Size.CellCount) return null;
            int edifice = _ctx.Cells.Edifice[cell];
            return edifice < 0 ? null : At(edifice);
        }

        /// <summary>Where a station stands. Off the edifice record, so there is one copy of it.</summary>
        public int CellOf(CraftStation station) => _edifices[station.Edifice].CellIndex;

        /// <summary>The <see cref="BuildingHandle"/> a station was built as.</summary>
        public int BuildingOf(CraftStation station) => ConstructionContent.BuildingForEdifice(_edifices[station.Edifice].Def);

        /// <summary>The building row a station was built as.</summary>
        public BuildingDef DefOf(CraftStation station) => ConstructionContent.BuildingAt(BuildingOf(station));

        /// <summary>
        /// Can anything be crafted at the thing standing as this edifice? Any building some crafted
        /// recipe names. The kitchen asks the same of the food recipes, so a building is one
        /// system's station or the other's and never both (<see cref="RecipeDef.Crafted"/>).
        /// </summary>
        public bool IsStation(int edifice)
        {
            if ((uint)edifice >= (uint)_edifices.Count) return false;
            PlacedEdifice placed = _edifices[edifice];
            if (placed.Removed) return false;
            int building = ConstructionContent.BuildingForEdifice(placed.Def);
            if (building == BuildingHandle.None) return false;
            RecipeDef[] recipes = _ctx.Content.Recipes;
            for (int r = 0; r < recipes.Length; r++)
                if (recipes[r].Crafted && recipes[r].At(building) != null) return true;
            return false;
        }

        /// <summary>Does the building standing in this cell take crafting bills? The <c>EditBill</c> router's question.</summary>
        public bool ClaimsCell(CellRef cell)
        {
            GridSize size = _ctx.Cells.Size;
            if (!size.Contains(cell)) return false;
            int edifice = _ctx.Cells.Edifice[size.Index(cell)];
            return edifice >= 0 && IsStation(edifice);
        }

        /// <summary>The terms the recipe is made on at this station, or null where it cannot be.</summary>
        public RecipeStation? TermsAt(CraftStation station, RecipeDef recipe) =>
            recipe.Crafted ? recipe.At(BuildingOf(station)) : null;

        RecipeDef? Recipe(int handle) =>
            (uint)handle < (uint)_ctx.Content.Recipes.Length ? _ctx.Content.Recipes[handle] : null;

        // ---- bills ------------------------------------------------------------------------------

        /// <summary>
        /// The bill a crafter should work now: the first from the top that is not suspended, not
        /// satisfied, and makeable here. Null when there is nothing to do. The kitchen's rule.
        /// </summary>
        public Bill? ActiveBill(CraftStation station)
        {
            for (int i = 0; i < station.Bills.Count; i++)
            {
                Bill bill = station.Bills[i];
                if (bill.Suspended || Satisfied(bill)) continue;
                RecipeDef? recipe = Recipe(bill.Recipe);
                if (recipe == null || TermsAt(station, recipe) == null) continue;
                return bill;
            }

            return null;
        }

        /// <summary>
        /// The recipe the station is working now: the batch's once anything is in, otherwise the
        /// active bill's. -1 for nothing.
        /// </summary>
        public int WorkRecipe(CraftStation station)
        {
            if (station.BatchInUse) return station.BatchRecipe;
            Bill? bill = ActiveBill(station);
            return bill?.Recipe ?? -1;
        }

        /// <summary>Does this bill's mode say it has nothing to do right now?</summary>
        public bool Satisfied(Bill bill) => bill.Mode switch
        {
            BillModeHandle.Times => bill.Done >= bill.Target,
            BillModeHandle.Forever => false,
            _ => Held(bill.Recipe) >= bill.Target,
        };

        /// <summary>What a bill's mode counts right now: products held, batches' products made, or nought.</summary>
        public int CountFor(Bill bill) => bill.Mode switch
        {
            BillModeHandle.Times => bill.Done,
            BillModeHandle.Forever => 0,
            _ => Held(bill.Recipe),
        };

        /// <summary>
        /// Units of the recipe's first product the colony holds (design 62 §9): every spawned,
        /// unforbidden stack of it on the ground or in a store. What "until you have 20" counts —
        /// twenty iron bars, not twenty batches — and <see cref="Bill.Done"/> counts in the same
        /// unit, so "make 20" makes twenty bars.
        /// </summary>
        public int Held(int recipe)
        {
            RecipeDef? def = Recipe(recipe);
            if (def == null || !def.Crafted || def.products.Count == 0) return 0;
            if (_countedTick.Length != _ctx.Content.Recipes.Length)
            {
                _countedTick = new int[_ctx.Content.Recipes.Length];
                _counted = new int[_ctx.Content.Recipes.Length];
                for (int i = 0; i < _countedTick.Length; i++) _countedTick[i] = -1;
            }
            if (_countedTick[recipe] == _ctx.CurrentTick) return _counted[recipe];

            int wanted = def.products[0].Item;
            int total = 0;
            var items = _ctx.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                ColonyItem item = items[i];
                if (item.Despawned || item.Forbidden || item.DefIndex != wanted) continue;
                if (_ctx.WhereIs(item) < 0) continue;
                total += item.Stack;
            }

            _countedTick[recipe] = _ctx.CurrentTick;
            _counted[recipe] = total;
            return total;
        }

        /// <summary>Something was made this tick: the next question recounts.</summary>
        public void Invalidate()
        {
            for (int i = 0; i < _countedTick.Length; i++) _countedTick[i] = -1;
        }

        /// <summary>
        /// How many batches of this recipe the map's ingredients would make, capped at 999: every
        /// spawned, unforbidden stack on the ground or in a store, the scarcest ingredient deciding.
        /// Asked only by the snapshot, once a published frame per station with a bill.
        /// </summary>
        public int SupplyBatches(RecipeDef recipe)
        {
            if (!recipe.Crafted) return 0;
            long best = 999;
            var items = _ctx.Items.Items;
            for (int n = 0; n < recipe.ingredients.Count; n++)
            {
                RecipeCount need = recipe.ingredients[n];
                long held = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    ColonyItem item = items[i];
                    if (item.Despawned || item.Forbidden || item.DefIndex != need.Item) continue;
                    if (_ctx.WhereIs(item) < 0) continue;
                    held += item.Stack;
                }
                long batches = need.count > 0 ? held / need.count : 999;
                if (batches < best) best = batches;
            }
            return (int)best;
        }

        /// <summary>
        /// The pane's one command (design 48 §5), for a crafting station: the kitchen's edits, plus
        /// <see cref="BillEdit.SetRecipe"/>, because a smelter makes two things and a row should be
        /// able to change which without being removed.
        /// </summary>
        public IntentRejection HandleEditBill(Intent intent)
        {
            GridSize size = _ctx.Cells.Size;
            if (!size.Contains(intent.Cell)) return IntentRejection.OutOfBounds;

            int edifice = _ctx.Cells.Edifice[size.Index(intent.Cell)];
            if (edifice < 0 || !IsStation(edifice)) return IntentRejection.NotPermitted;

            CraftStation? station = At(edifice);
            int op = intent.A, index = intent.B, value = intent.C;

            if (op == BillEdit.Add)
            {
                RecipeDef? recipe = Recipe(index);
                if (recipe == null) return IntentRejection.NotPermitted;
                station ??= Create(edifice);
                if (TermsAt(station, recipe) == null) return IntentRejection.NotPermitted;
                if (station.Bills.Count >= Kitchen.MaxBills) return IntentRejection.NotPermitted;
                station.Bills.Add(new Bill { Recipe = index, Target = DefaultTarget });
                return IntentRejection.None;
            }

            if (station == null || (uint)index >= (uint)station.Bills.Count) return IntentRejection.NotPermitted;

            if (op == BillEdit.SetRecipe)
            {
                Bill bill = station.Bills[index];
                RecipeDef? recipe = Recipe(value);
                if (recipe == null || TermsAt(station, recipe) == null) return IntentRejection.NotPermitted;
                if (bill.Recipe == value) return IntentRejection.AlreadyInThatState;
                bill.Recipe = value;
                // A count of one bar means nothing against another: "make 10" starts from nought.
                bill.Done = 0;
                return IntentRejection.None;
            }

            return BillEdits.Apply(station.Bills, op, index, value);
        }

        CraftStation Create(int edifice)
        {
            var station = new CraftStation { Edifice = edifice };
            _atEdifice[edifice] = _stations.Count;
            _stations.Add(station);
            return station;
        }

        /// <summary>
        /// The station came down (<c>ConstructionGrid</c>'s one removal path). Its bills go with it,
        /// and so does whatever was in it — the ore in the crucible and the fuel in the hopper, as
        /// a generator's wood goes with the generator.
        /// </summary>
        public void Remove(int edifice)
        {
            CraftStation? station = At(edifice);
            if (station == null) return;
            station.Bills.Clear();
            station.EmptyBatch();
            station.Fuel = 0;
            station.Removed = true;
        }

        // ---- the hopper ---------------------------------------------------------------------------

        /// <summary>The fuel a batch of this recipe burns at this station, in worth units.</summary>
        public int FuelPerBatch(CraftStation station, RecipeDef recipe) => TermsAt(station, recipe)?.fuelPerBatch ?? 0;

        /// <summary>
        /// Could a batch of the station's work recipe be started or carried on right now, as far as
        /// fuel goes: its fuel already paid, or enough in the hopper to pay it. True where nothing
        /// is to be worked, so an idle station never reads as short.
        /// </summary>
        public bool Fuelled(CraftStation station)
        {
            if (station.Removed) return false;
            if (station.FuelPaid) return true;
            RecipeDef? recipe = Recipe(WorkRecipe(station));
            if (recipe == null) return true;
            return station.Fuel >= FuelPerBatch(station, recipe);
        }

        /// <summary>
        /// Does this station want fuel fetched (design 62 §9)? It has a hopper, it has a bill that
        /// is not paused, and the hopper is below half — the generator's half (design 32 §6).
        /// </summary>
        public bool NeedsRefuel(CraftStation station)
        {
            if (station.Removed) return false;
            BuildingDef def = DefOf(station);
            if (!def.HasHopper) return false;
            bool working = false;
            for (int i = 0; i < station.Bills.Count; i++)
                if (!station.Bills[i].Suspended) { working = true; break; }
            if (!working) return false;
            return station.Fuel * 2 < def.hopperCapacity;
        }

        /// <summary>Whole units of this item that would go into the hopper without passing the top; 0 for anything it does not take.</summary>
        public int RoomFor(CraftStation station, int item)
        {
            if (station.Removed) return 0;
            BuildingDef def = DefOf(station);
            int worth = def.HopperWorth(item);
            if (worth <= 0) return 0;
            int room = (def.hopperCapacity - station.Fuel) / worth;
            return room < 0 ? 0 : room;
        }

        /// <summary>Tip whole units of fuel into the hopper, never past the top; returns how many went in.</summary>
        public int AddFuel(CraftStation station, int item, int units)
        {
            if (units <= 0) return 0;
            int take = System.Math.Min(units, RoomFor(station, item));
            if (take <= 0) return 0;
            station.Fuel += take * DefOf(station).HopperWorth(item);
            return take;
        }

        /// <summary>
        /// The fuel a hauler should fetch for this station: the nearest reachable stack of the first
        /// fuel the hopper lists that has room and any on the map — coal before wood, because coal
        /// is the better fuel (design 62 §9, the owner). Null when there is none of either.
        /// </summary>
        public ColonyItem? FuelLoad(Pawn pawn, CraftStation station)
        {
            BuildingDef def = DefOf(station);
            for (int i = 0; i < def.hopperFuels.Count; i++)
            {
                int item = def.hopperFuels[i].item;
                if (RoomFor(station, item) <= 0) continue;
                ColonyItem? load = DeliverWorkGiver.NearestLoad(pawn, _ctx, item);
                if (load != null) return load;
            }
            return null;
        }

        /// <summary>Set a hopper directly, in worth units. For tests and for the debug menu.</summary>
        public void SetFuel(CraftStation station, int worth) => station.Fuel = System.Math.Max(0, worth);

        /// <summary>Create the station's state without a bill: for tests that fill a hopper before the first bill.</summary>
        public CraftStation Ensure(int edifice) => At(edifice) ?? Create(edifice);

        // ---- the crafter's side -------------------------------------------------------------------

        /// <summary>Units of the ingredient at <paramref name="slot"/> the batch still wants.</summary>
        public int Short(CraftStation station, RecipeDef recipe, int slot)
        {
            int have = station.BatchInUse && slot < station.In.Length ? station.In[slot] : 0;
            int want = recipe.ingredients[slot].count - have;
            return want < 0 ? 0 : want;
        }

        /// <summary>How many of this item the batch of this recipe still wants: nought for anything that is not one of its ingredients.</summary>
        public int Wanted(CraftStation station, RecipeDef recipe, int item)
        {
            if (station.BatchInUse && station.BatchRecipe != IndexOf(recipe)) return 0;
            for (int n = 0; n < recipe.ingredients.Count; n++)
                if (recipe.ingredients[n].Item == item) return Short(station, recipe, n);
            return 0;
        }

        int IndexOf(RecipeDef recipe) => System.Array.IndexOf(_ctx.Content.Recipes, recipe);

        /// <summary>Does the station hold every ingredient of the recipe?</summary>
        public bool Complete(CraftStation station, RecipeDef recipe)
        {
            for (int n = 0; n < recipe.ingredients.Count; n++)
                if (Short(station, recipe, n) > 0) return false;
            return true;
        }

        /// <summary>
        /// Put a load into the station for this recipe. The first load locks the batch to the
        /// recipe. Returns false for a thing the batch has no use for, which the job treats as a
        /// failure; more than the batch wants is refused the same way, because the lift took only
        /// what was wanted.
        /// </summary>
        public bool AddIngredient(CraftStation station, int recipeHandle, int item, int count)
        {
            RecipeDef? recipe = Recipe(recipeHandle);
            if (recipe == null || count <= 0 || TermsAt(station, recipe) == null) return false;
            if (station.BatchInUse && station.BatchRecipe != recipeHandle) return false;

            for (int n = 0; n < recipe.ingredients.Count; n++)
            {
                if (recipe.ingredients[n].Item != item) continue;
                if (count > Short(station, recipe, n)) return false;
                if (!station.BatchInUse)
                {
                    station.BatchRecipe = recipeHandle;
                    station.In = new int[recipe.ingredients.Count];
                }
                station.In[n] += count;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Take the batch's fuel from the hopper if it has not been taken: the moment the work
        /// starts. False where the hopper is short, which stops the work until a hauler fills it.
        /// </summary>
        public bool PayFuel(CraftStation station, RecipeDef recipe)
        {
            if (station.FuelPaid) return true;
            int cost = FuelPerBatch(station, recipe);
            if (station.Fuel < cost) return false;
            station.Fuel -= cost;
            station.FuelPaid = true;
            return true;
        }

        /// <summary>The whole of a batch's work at this station, in milliwork, before the crafter's speed.</summary>
        public long WorkMilli(CraftStation station, RecipeDef recipe)
        {
            RecipeStation? terms = TermsAt(station, recipe);
            int factor = terms?.workFactorPerMille ?? 1_000;
            return (long)recipe.workTicks * factor / 1_000 * Rates.Scale;
        }

        /// <summary>
        /// The batch is done: the station is emptied and the bill that ordered it credited with
        /// what it made — the first unpaused bill for the recipe, else the first for it at all,
        /// else none (a bill removed mid-batch). Returns the recipe, whose products the caller
        /// puts down.
        /// </summary>
        public RecipeDef? Finish(CraftStation station)
        {
            RecipeDef? recipe = Recipe(station.BatchRecipe);
            int handle = station.BatchRecipe;
            station.EmptyBatch();
            if (recipe == null) return null;

            Bill? credit = null;
            for (int i = 0; i < station.Bills.Count && credit == null; i++)
                if (station.Bills[i].Recipe == handle && !station.Bills[i].Suspended) credit = station.Bills[i];
            for (int i = 0; i < station.Bills.Count && credit == null; i++)
                if (station.Bills[i].Recipe == handle) credit = station.Bills[i];
            if (credit != null && recipe.products.Count > 0) credit.Done += recipe.products[0].count;

            Invalidate();
            return recipe;
        }

        // ---- registration --------------------------------------------------------------------------

        /// <summary>
        /// Registration. The <c>EditBill</c> intent is routed by the colony, as the kitchen's is:
        /// one kind for every station that takes bills (<c>ColonyComposition</c>).
        /// </summary>
        public SimWorldBuilder Attach(SimWorldBuilder builder) =>
            builder.AddTickable(_ => this)
                .AddSnapshotContributor(this);

        public TickGroup TickGroup => TickGroup.Never;
        public int TickPhaseOffset => 0;
        public void Tick(SimWorld world) { }

        /// <summary>
        /// Every station with a bill, a batch or fuel, each after its bills (<see cref="StationView"/>):
        /// the pan is how much of a batch is in, the cook is how far it is worked, the supply is the
        /// batches the ore on the map would make, and <see cref="StationView.FuelBatches"/> the
        /// batches the hopper would burn for.
        /// </summary>
        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            for (int s = 0; s < _stations.Count; s++)
            {
                CraftStation station = _stations[s];
                if (station.Removed) continue;
                if (station.Bills.Count == 0 && !station.BatchInUse && station.Fuel == 0) continue;

                int first = writer.BillCursor;
                for (int b = 0; b < station.Bills.Count; b++)
                {
                    Bill bill = station.Bills[b];
                    writer.AddBill(new BillView(bill.Recipe, (byte)bill.Mode, bill.Target, bill.Done,
                        CountFor(bill), bill.Suspended, Satisfied(bill)));
                }

                // The recipe the pane talks about: what is being worked, else the top bill's.
                int handle = WorkRecipe(station);
                if (handle < 0 && station.Bills.Count > 0) handle = station.Bills[0].Recipe;
                RecipeDef? recipe = Recipe(handle);

                short pan = 0, work = 0, supply = 0, fuel = -1;
                if (recipe != null && recipe.Crafted)
                {
                    int want = 0, have = 0;
                    for (int n = 0; n < recipe.ingredients.Count; n++)
                    {
                        want += recipe.ingredients[n].count;
                        have += recipe.ingredients[n].count - Short(station, recipe, n);
                    }
                    pan = (short)(want > 0 ? System.Math.Min(1_000, have * 1_000 / want) : 0);
                    long total = WorkMilli(station, recipe);
                    work = (short)(total > 0 ? System.Math.Min(1_000L, station.CraftMilliwork * 1_000L / total) : 0);
                    supply = (short)SupplyBatches(recipe);
                    int cost = FuelPerBatch(station, recipe);
                    if (DefOf(station).HasHopper)
                        fuel = (short)(cost > 0 ? System.Math.Min(999, station.Fuel / cost) : 999);
                }
                else if (DefOf(station).HasHopper) fuel = 0;

                writer.AddStation(new StationView(CellOf(station), _edifices[station.Edifice].Def, Fuelled(station),
                    pan, work, false, false, first, station.Bills.Count, supply, fuel));
            }
        }

        // ---- the hash ------------------------------------------------------------------------------

        /// <summary>Nothing at all while no station has ever been used, so a colony that never smelts hashes as before.</summary>
        public void ContributeTo(ref StateHash hash)
        {
            if (_stations.Count == 0) return;
            hash.Add(_stations.Count);
            for (int s = 0; s < _stations.Count; s++)
            {
                CraftStation station = _stations[s];
                hash.Add(station.Edifice);
                hash.Add(station.Removed);
                hash.Add(station.BatchRecipe);
                hash.Add(station.In.Length);
                for (int n = 0; n < station.In.Length; n++) hash.Add(station.In[n]);
                hash.Add(station.FuelPaid);
                hash.Add(station.CraftMilliwork);
                hash.Add(station.Fuel);
                hash.Add(station.Bills.Count);
                for (int b = 0; b < station.Bills.Count; b++)
                {
                    Bill bill = station.Bills[b];
                    hash.Add(bill.Recipe);
                    hash.Add(bill.Mode);
                    hash.Add(bill.Target);
                    hash.Add(bill.Done);
                    hash.Add(bill.Suspended);
                }
            }
        }

        // ---- the save ------------------------------------------------------------------------------

        public string SaveKey => "odyssey.workshop";

        /// <summary>The layout of this section. A later field is read behind a check on this.</summary>
        const int Layout = 1;

        public void Save(SaveWriter writer)
        {
            writer.Write(Layout);
            writer.Write(_stations.Count);
            for (int s = 0; s < _stations.Count; s++)
            {
                CraftStation station = _stations[s];
                writer.Write(station.Edifice);
                writer.Write(station.Removed);
                writer.Write(station.BatchRecipe);
                writer.Write(station.In.Length);
                for (int n = 0; n < station.In.Length; n++) writer.Write(station.In[n]);
                writer.Write(station.FuelPaid);
                writer.Write(station.CraftMilliwork);
                writer.Write(station.Fuel);
                writer.Write(station.Bills.Count);
                for (int b = 0; b < station.Bills.Count; b++)
                {
                    Bill bill = station.Bills[b];
                    writer.Write(bill.Recipe);
                    writer.Write(bill.Mode);
                    writer.Write(bill.Target);
                    writer.Write(bill.Done);
                    writer.Write(bill.Suspended);
                }
            }
        }

        public void Load(SaveReader reader)
        {
            _stations.Clear();
            _atEdifice.Clear();
            Invalidate();

            reader.ReadInt(); // layout 1: nothing is read behind it yet
            int count = reader.ReadInt();
            for (int s = 0; s < count; s++)
            {
                var station = new CraftStation
                {
                    Edifice = reader.ReadInt(),
                    Removed = reader.ReadBool(),
                    BatchRecipe = reader.ReadInt(),
                };
                int slots = reader.ReadInt();
                station.In = slots == 0 ? System.Array.Empty<int>() : new int[slots];
                for (int n = 0; n < slots; n++) station.In[n] = reader.ReadInt();
                station.FuelPaid = reader.ReadBool();
                station.CraftMilliwork = reader.ReadInt();
                station.Fuel = reader.ReadInt();

                int bills = reader.ReadInt();
                for (int b = 0; b < bills; b++)
                {
                    station.Bills.Add(new Bill
                    {
                        Recipe = reader.ReadInt(),
                        Mode = reader.ReadInt(),
                        Target = reader.ReadInt(),
                        Done = reader.ReadInt(),
                        Suspended = reader.ReadBool(),
                    });
                }

                _atEdifice[station.Edifice] = _stations.Count;
                _stations.Add(station);
            }
        }
    }
}
