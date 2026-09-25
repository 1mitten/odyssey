#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.Cooking
{
    /// <summary>One order on a station's list (design 48 §5, a-14's bill cut down to three modes).</summary>
    public sealed class Bill
    {
        /// <summary>A <see cref="RecipeHandle"/> value.</summary>
        public int Recipe;

        /// <summary>A <see cref="BillModeHandle"/> value.</summary>
        public int Mode = BillModeHandle.UntilYouHave;

        /// <summary>Meals to keep, or meals to make, by the mode.</summary>
        public int Target = Kitchen.DefaultTarget;

        /// <summary>Meals this bill has made since it was added. What <see cref="BillModeHandle.Times"/> counts.</summary>
        public int Done;

        /// <summary>Stopped by the player; skipped, and the next bill down is worked instead.</summary>
        public bool Suspended;
    }

    /// <summary>
    /// A cooking station's state (design 48 §5): its bills, and the meal in its pan.
    ///
    /// <para><b>The pan is the station's, not the cook's.</b> Food put in stays in whoever put it
    /// there, and a meal half cooked when its cook is called away — or when the galley's power
    /// goes — is finished by the next cook to come. So a cook's own job carries nothing that has
    /// to survive it, and a save taken with a meal on the hob loses nothing.</para>
    /// </summary>
    public sealed class CookStation
    {
        /// <summary>The index of the <see cref="PlacedEdifice"/> this station is. The key, as a store's is.</summary>
        public int Edifice;

        /// <summary>The list, top first.</summary>
        public readonly List<Bill> Bills = new List<Bill>();

        /// <summary>Raw food in the pan, by nutrition.</summary>
        public int PanNutrition;

        /// <summary>There is meat in the pan: what comes out is a meal, not a vegetable meal.</summary>
        public bool PanMeat;

        /// <summary>Fuel put in for the meal on the hob: the campfire's one wood.</summary>
        public int FuelIn;

        /// <summary>Milliwork done on the meal on the hob. Nought until the cooking starts.</summary>
        public int CookMilliwork;

        /// <summary>
        /// Whether the meal on the hob will come out burnt: -1 not yet rolled, 0 no, 1 yes. Rolled
        /// once, when the cooking starts, so the pan can be seen to catch before it comes out.
        /// </summary>
        public int Burn = -1;

        /// <summary>The station came down. The slot is kept, so ids stay stable.</summary>
        public bool Removed;

        /// <summary>Is anything on the hob.</summary>
        public bool PanInUse => PanNutrition > 0 || FuelIn > 0 || CookMilliwork > 0;

        public void EmptyPan()
        {
            PanNutrition = 0;
            PanMeat = false;
            FuelIn = 0;
            CookMilliwork = 0;
            Burn = -1;
        }
    }

    /// <summary>
    /// Every cooking station that has ever had a bill (design 48 §5).
    ///
    /// <para><b>A station is any building a recipe names</b> — the galley and the campfire — and
    /// needs no registration: a campfire built before the kitchen existed takes a bill like one
    /// built after. What this holds is the state a station has once somebody uses it, created on
    /// the first bill and keyed by edifice index exactly as <c>StorageUnits</c> keys a shelf,
    /// for the reasons that class gives.</para>
    ///
    /// <para><b>Its own save section, so no format bump</b>: a file from before the kitchen simply
    /// has no section here. Hashed only while it holds anything, so a colony that never cooks
    /// hashes as it did before.</para>
    /// </summary>
    public sealed class Kitchen : ITickable, IStateHashable, ISaveable, ISnapshotContributor
    {
        /// <summary>A new bill's target: ten meals, two days' eating for five colonists.</summary>
        public const int DefaultTarget = 10;

        /// <summary>The most a target may be. The pane's arithmetic stops here and so does this.</summary>
        public const int MaxTarget = 999;

        /// <summary>
        /// The most bills one station may hold: five, the rows the pane is laid out for, so adding a
        /// bill never changes the pane's height under the pointer (the storage pane's lesson). The
        /// interface keeps the same number as <c>BillsModel.MaxRows</c>, and a test holds the two.
        /// </summary>
        public const int MaxBills = 5;

        readonly PawnContext _ctx;
        readonly IReadOnlyList<PlacedEdifice> _edifices;
        readonly List<CookStation> _stations = new List<CookStation>();

        /// <summary>Edifice index to slot. Derived, rebuilt on load, and probed — never iterated in a tick.</summary>
        readonly Dictionary<int, int> _atEdifice = new Dictionary<int, int>();

        // The colony's meal count, taken once a tick at most: every cook's think asks it for every
        // bill, and the answer cannot change between two thinks in one tick that make nothing.
        int _countedTick = -1;
        int _counted;

        public Kitchen(PawnContext ctx, IReadOnlyList<PlacedEdifice> edifices)
        {
            _ctx = ctx ?? throw new System.ArgumentNullException(nameof(ctx));
            _edifices = edifices ?? throw new System.ArgumentNullException(nameof(edifices));
        }

        /// <summary>Every station with state, in the order each was first used; tombstones included.</summary>
        public IReadOnlyList<CookStation> Stations => _stations;

        /// <summary>The station this edifice is, or null. A tombstoned one answers null.</summary>
        public CookStation? At(int edifice)
        {
            if (!_atEdifice.TryGetValue(edifice, out int slot)) return null;
            CookStation station = _stations[slot];
            return station.Removed ? null : station;
        }

        /// <summary>Where a station stands. Off the edifice record, so there is one copy of it.</summary>
        public int CellOf(CookStation station) => _edifices[station.Edifice].CellIndex;

        /// <summary>The <see cref="BuildingHandle"/> a station was built as.</summary>
        public int BuildingOf(CookStation station) => ConstructionContent.BuildingForEdifice(_edifices[station.Edifice].Def);

        /// <summary>
        /// Can anything be cooked on the thing standing as this edifice? Any building some recipe
        /// names. Asked of the edifice record, because that is what a cell carries.
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
                if (recipes[r].At(building) != null) return true;
            return false;
        }

        /// <summary>
        /// Could a meal be cooked here right now: switched on and powered where it runs on power,
        /// and always for a fire. Not about whether anybody can reach it.
        /// </summary>
        public bool Ready(CookStation station)
        {
            if (station.Removed) return false;
            PlacedEdifice placed = _edifices[station.Edifice];
            if (placed.Removed) return false;
            int building = ConstructionContent.BuildingForEdifice(placed.Def);
            if (building == BuildingHandle.None) return false;
            if (!ConstructionContent.BuildingAt(building).IsPowered) return true;
            return _ctx.Power != null && _ctx.Power.IsPowered(station.Edifice);
        }

        /// <summary>The terms the recipe is made on at this station, or null where it cannot be.</summary>
        public RecipeStation? TermsAt(CookStation station, RecipeDef recipe) => recipe.At(BuildingOf(station));

        // ---- bills ------------------------------------------------------------------------------

        /// <summary>
        /// The bill a cook should work now: the first from the top that is not suspended, not
        /// satisfied, and makeable here. Null when there is nothing to do.
        /// </summary>
        public Bill? ActiveBill(CookStation station)
        {
            for (int i = 0; i < station.Bills.Count; i++)
            {
                Bill bill = station.Bills[i];
                if (bill.Suspended || Satisfied(bill)) continue;
                if ((uint)bill.Recipe >= (uint)_ctx.Content.Recipes.Length) continue;
                if (TermsAt(station, _ctx.Content.Recipes[bill.Recipe]) == null) continue;
                return bill;
            }

            return null;
        }

        /// <summary>Does this bill's mode say it has nothing to do right now?</summary>
        public bool Satisfied(Bill bill) => bill.Mode switch
        {
            BillModeHandle.Times => bill.Done >= bill.Target,
            BillModeHandle.Forever => false,
            _ => MealsHeld(bill.Recipe) >= bill.Target,
        };

        /// <summary>
        /// What a bill's mode counts right now: the meals the colony holds, the meals this bill has
        /// made, or nought for a bill that never stops. What the pane prints beside the target.
        /// </summary>
        public int CountFor(Bill bill) => bill.Mode switch
        {
            BillModeHandle.Times => bill.Done,
            BillModeHandle.Forever => 0,
            _ => MealsHeld(bill.Recipe),
        };

        /// <summary>
        /// Meals of the good kind the colony holds (design 48 §5): every spawned, unforbidden unit of
        /// the recipe's two products on the ground or in a store. Not burnt meals — keeping ten of
        /// those in stock is not what the player asked for — and not a meal in somebody's hands,
        /// which is on its way to being eaten.
        /// </summary>
        public int MealsHeld(int recipe)
        {
            if ((uint)recipe >= (uint)_ctx.Content.Recipes.Length) return 0;
            if (_countedTick == _ctx.CurrentTick) return _counted;

            RecipeDef def = _ctx.Content.Recipes[recipe];
            int total = 0;
            var items = _ctx.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                ColonyItem item = items[i];
                if (item.Despawned || item.Forbidden) continue;
                if (item.DefIndex != def.ProductItem && item.DefIndex != def.ProductNoMeatItem) continue;
                if (_ctx.WhereIs(item) < 0) continue;
                total += item.Stack;
            }

            // Only one recipe exists, so one cached number serves; a second recipe would key this.
            _countedTick = _ctx.CurrentTick;
            _counted = total;
            return total;
        }

        /// <summary>A meal was made or eaten this tick: the next question recounts.</summary>
        public void Invalidate() => _countedTick = -1;

        /// <summary>
        /// The pane's one command (design 48 §5). The station is the building standing in the cell;
        /// a station nobody has used yet is created by its first bill.
        /// </summary>
        public IntentRejection HandleEditBill(Intent intent)
        {
            GridSize size = _ctx.Cells.Size;
            if (!size.Contains(intent.Cell)) return IntentRejection.OutOfBounds;

            int edifice = _ctx.Cells.Edifice[size.Index(intent.Cell)];
            if (edifice < 0 || !IsStation(edifice)) return IntentRejection.NotPermitted;

            CookStation? station = At(edifice);
            int op = intent.A, index = intent.B, value = intent.C;

            if (op == BillEdit.Add)
            {
                if ((uint)index >= (uint)_ctx.Content.Recipes.Length) return IntentRejection.NotPermitted;
                station ??= Create(edifice);
                if (TermsAt(station, _ctx.Content.Recipes[index]) == null) return IntentRejection.NotPermitted;
                if (station.Bills.Count >= MaxBills) return IntentRejection.NotPermitted;
                station.Bills.Add(new Bill { Recipe = index });
                return IntentRejection.None;
            }

            if (station == null || (uint)index >= (uint)station.Bills.Count) return IntentRejection.NotPermitted;
            List<Bill> bills = station.Bills;
            Bill bill = bills[index];

            switch (op)
            {
                case BillEdit.Remove:
                    bills.RemoveAt(index);
                    return IntentRejection.None;

                case BillEdit.MoveUp:
                    if (index == 0) return IntentRejection.AlreadyInThatState;
                    bills[index] = bills[index - 1];
                    bills[index - 1] = bill;
                    return IntentRejection.None;

                case BillEdit.MoveDown:
                    if (index == bills.Count - 1) return IntentRejection.AlreadyInThatState;
                    bills[index] = bills[index + 1];
                    bills[index + 1] = bill;
                    return IntentRejection.None;

                case BillEdit.SetMode:
                    if ((uint)value >= (uint)BillModeHandle.Count) return IntentRejection.NotPermitted;
                    if (bill.Mode == value) return IntentRejection.AlreadyInThatState;
                    bill.Mode = value;
                    // A count of meals made means nothing across a change of what is being counted:
                    // "make 10" after "keep 10" starts from nought, which is what a player expects.
                    bill.Done = 0;
                    return IntentRejection.None;

                case BillEdit.SetTarget:
                {
                    int target = value < 1 ? 1 : value > MaxTarget ? MaxTarget : value;
                    if (bill.Target == target) return IntentRejection.AlreadyInThatState;
                    bill.Target = target;
                    return IntentRejection.None;
                }

                case BillEdit.SetSuspended:
                {
                    bool suspended = value != 0;
                    if (bill.Suspended == suspended) return IntentRejection.AlreadyInThatState;
                    bill.Suspended = suspended;
                    return IntentRejection.None;
                }

                default:
                    return IntentRejection.UnknownIntent;
            }
        }

        CookStation Create(int edifice)
        {
            var station = new CookStation { Edifice = edifice };
            _atEdifice[edifice] = _stations.Count;
            _stations.Add(station);
            return station;
        }

        /// <summary>
        /// The station came down (<c>ConstructionGrid</c>'s one removal path). Its bills go with it,
        /// and so does whatever was in the pan — a building knocked down with dinner on the hob.
        /// </summary>
        public void Remove(int edifice)
        {
            CookStation? station = At(edifice);
            if (station == null) return;
            station.Bills.Clear();
            station.EmptyPan();
            station.Removed = true;
        }

        // ---- the cook's side ---------------------------------------------------------------------

        /// <summary>
        /// Put a load into the pan (design 48 §5): raw food adds its nutrition, fuel its count.
        /// Returns false for a thing the pan has no use for, which the job treats as a failure.
        /// </summary>
        public bool AddToPan(CookStation station, RecipeDef recipe, int defIndex, int count)
        {
            if (count <= 0) return false;
            ItemDef item = _ctx.Content.Items[defIndex];
            RecipeStation? terms = TermsAt(station, recipe);
            if (terms == null) return false;

            if (terms.NeedsFuel && defIndex == terms.fuelItem && !item.rawIngredient)
            {
                station.FuelIn += count;
                return true;
            }

            if (!item.rawIngredient || item.nutrition <= 0) return false;
            station.PanNutrition += item.nutrition * count;
            if (item.meat) station.PanMeat = true;
            return true;
        }

        /// <summary>Does the pan hold everything the recipe takes here — its food and its fuel?</summary>
        public bool PanReady(CookStation station, RecipeDef recipe)
        {
            RecipeStation? terms = TermsAt(station, recipe);
            if (terms == null) return false;
            if (station.PanNutrition < recipe.ingredientNutrition) return false;
            return !terms.NeedsFuel || station.FuelIn >= terms.fuelCount;
        }

        /// <summary>The whole of the cooking's work at this station, in milliwork, before the cook's speed.</summary>
        public long WorkMilli(CookStation station, RecipeDef recipe)
        {
            RecipeStation? terms = TermsAt(station, recipe);
            int factor = terms?.workFactorPerMille ?? 1_000;
            return (long)recipe.workTicks * factor / 1_000 * Rates.Scale;
        }

        /// <summary>
        /// The chance per mille that a cook of this level burns the meal here: the recipe's table,
        /// times the station's factor, capped at certain.
        /// </summary>
        public int BurnChancePerMille(CookStation station, RecipeDef recipe, int level)
        {
            RecipeStation? terms = TermsAt(station, recipe);
            long chance = (long)recipe.BurnPerMille(level) * (terms?.burnFactorPerMille ?? 1_000) / 1_000;
            return chance > 1_000 ? 1_000 : (int)chance;
        }

        /// <summary>
        /// The meal is done: what comes out, and the pan left with whatever food was over. The pan's
        /// fuel is spent and its roll forgotten; the food over the recipe's amount stays in for the
        /// next meal, which is a-18's own rule — nothing is wasted by the pan.
        /// </summary>
        public int Finish(CookStation station, RecipeDef recipe, Bill? bill)
        {
            int product = station.Burn == 1 ? recipe.BurntItem
                : station.PanMeat ? recipe.ProductItem : recipe.ProductNoMeatItem;

            int left = station.PanNutrition - recipe.ingredientNutrition;
            station.EmptyPan();
            if (left > 0) station.PanNutrition = left;

            if (bill != null) bill.Done++;
            Invalidate();
            return product;
        }

        // ---- registration --------------------------------------------------------------------------

        public SimWorldBuilder Attach(SimWorldBuilder builder) =>
            builder.AddTickable(_ => this)
                .AddSnapshotContributor(this)
                .AddIntentHandler(IntentKind.EditBill, HandleEditBill);

        public TickGroup TickGroup => TickGroup.Never;
        public int TickPhaseOffset => 0;
        public void Tick(SimWorld world) { }

        /// <summary>
        /// Every station with a bill or a pan in use, each after its bills (<see cref="StationView"/>).
        /// A station nobody cooks at is not published.
        /// </summary>
        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            RecipeDef[] recipes = _ctx.Content.Recipes;
            for (int s = 0; s < _stations.Count; s++)
            {
                CookStation station = _stations[s];
                if (station.Removed) continue;
                if (station.Bills.Count == 0 && !station.PanInUse) continue;

                int first = writer.BillCursor;
                for (int b = 0; b < station.Bills.Count; b++)
                {
                    Bill bill = station.Bills[b];
                    writer.AddBill(new BillView(bill.Recipe, (byte)bill.Mode, bill.Target, bill.Done,
                        CountFor(bill), bill.Suspended, Satisfied(bill)));
                }

                RecipeDef recipe = recipes.Length > 0 ? recipes[0] : new RecipeDef();
                Bill? active = ActiveBill(station);
                if (active != null && (uint)active.Recipe < (uint)recipes.Length) recipe = recipes[active.Recipe];

                int pan = recipe.ingredientNutrition > 0
                    ? System.Math.Min(1_000, station.PanNutrition * 1_000 / recipe.ingredientNutrition) : 0;
                long work = WorkMilli(station, recipe);
                int cook = work > 0 ? (int)System.Math.Min(1_000L, station.CookMilliwork * 1_000L / work) : 0;

                writer.AddStation(new StationView(CellOf(station), _edifices[station.Edifice].Def, Ready(station),
                    (short)pan, (short)cook, station.Burn == 1, station.PanMeat, first, station.Bills.Count));
            }
        }

        // ---- the hash ------------------------------------------------------------------------------

        /// <summary>Nothing at all while no station has ever been used, so a colony that never cooks hashes as before.</summary>
        public void ContributeTo(ref StateHash hash)
        {
            if (_stations.Count == 0) return;
            hash.Add(_stations.Count);
            for (int s = 0; s < _stations.Count; s++)
            {
                CookStation station = _stations[s];
                hash.Add(station.Edifice);
                hash.Add(station.Removed);
                hash.Add(station.PanNutrition);
                hash.Add(station.PanMeat);
                hash.Add(station.FuelIn);
                hash.Add(station.CookMilliwork);
                hash.Add(station.Burn);
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

        public string SaveKey => "odyssey.kitchen";

        /// <summary>The layout of this section. A later field is read behind a check on this.</summary>
        const int Layout = 1;

        public void Save(SaveWriter writer)
        {
            writer.Write(Layout);
            writer.Write(_stations.Count);
            for (int s = 0; s < _stations.Count; s++)
            {
                CookStation station = _stations[s];
                writer.Write(station.Edifice);
                writer.Write(station.Removed);
                writer.Write(station.PanNutrition);
                writer.Write(station.PanMeat);
                writer.Write(station.FuelIn);
                writer.Write(station.CookMilliwork);
                writer.Write(station.Burn);
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
            _countedTick = -1;

            reader.ReadInt(); // layout 1: nothing is read behind it yet
            int count = reader.ReadInt();
            for (int s = 0; s < count; s++)
            {
                var station = new CookStation
                {
                    Edifice = reader.ReadInt(),
                    Removed = reader.ReadBool(),
                    PanNutrition = reader.ReadInt(),
                    PanMeat = reader.ReadBool(),
                    FuelIn = reader.ReadInt(),
                    CookMilliwork = reader.ReadInt(),
                    Burn = reader.ReadInt(),
                };

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
