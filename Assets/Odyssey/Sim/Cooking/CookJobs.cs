#nullable enable
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.Cooking
{
    /// <summary>
    /// The Cooking work type's one giver (design 48 §5): the nearest station this colonist can reach
    /// and claim that has a bill wanting work, and then whichever half of the bill is next — fetch
    /// a load of raw food into the pan, fetch the campfire's wood, or cook what the pan holds.
    ///
    /// <para><b>One load a job.</b> A meal of carrots and meat is two trips, and each is its own
    /// job: the pan is the station's, so the next job — this cook's or another's — finds the
    /// first load already in it. That keeps the driver to one carry, the shape every carrying
    /// driver in the project already has.</para>
    ///
    /// <para>Scales with the stations that have ever had a bill, and only on a think by a colonist
    /// with Cooking switched on.</para>
    /// </summary>
    public sealed class CookWorkGiver : WorkGiver
    {
        public override string Name => "Cook";

        public override int WorkType => WorkTypeIndex.Cooking;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            Kitchen? kitchen = ctx.Kitchen;
            if (kitchen == null) return false;

            var stations = kitchen.Stations;
            CookStation? best = null;
            int bestStand = -1, bestDistance = int.MaxValue;
            for (int s = 0; s < stations.Count; s++)
            {
                CookStation station = stations[s];
                if (station.Removed || !kitchen.Ready(station)) continue;
                if (kitchen.ActiveBill(station) == null) continue;

                int cell = kitchen.CellOf(station);
                if (ctx.Designations?.At(cell) == Designations.DesignationKind.Deconstruct) continue;

                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Device, station.Edifice);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int stand = StandFor(ctx, pawn, kitchen, station);
                if (stand < 0) continue;

                // Something to do there: the pan is ready, or there is something to put in it.
                RecipeDef recipe = ctx.Content.Recipes[kitchen.ActiveBill(station)!.Recipe];
                if (!kitchen.PanReady(station, recipe) && NextLoad(pawn, ctx, kitchen, station, recipe) == null)
                    continue;

                bestDistance = distance;
                best = station;
                bestStand = stand;
            }

            if (best == null) return false;

            RecipeDef chosen = ctx.Content.Recipes[kitchen.ActiveBill(best)!.Recipe];
            job.Reset(JobIndex.Cook);
            job.DestCell = kitchen.CellOf(best);
            job.WorkTicks = bestStand;
            if (!kitchen.PanReady(best, chosen))
            {
                ColonyItem load = NextLoad(pawn, ctx, kitchen, best, chosen)!;
                job.TargetItem = load.Id;
                job.TargetCell = ctx.WhereIs(load);
            }
            return true;
        }

        /// <summary>
        /// Where a cook stands to work this station: in front of it where that is somewhere to
        /// stand, else beside it. A galley is worked from the front (design 48 §3); a fire from
        /// any side, and so is a galley someone has backed against the wrong wall.
        /// </summary>
        public static int StandFor(PawnContext ctx, Pawn pawn, Kitchen kitchen, CookStation station) =>
            StandAt(ctx, pawn, station.Edifice, kitchen.CellOf(station));

        /// <summary>
        /// <see cref="StandFor"/> by edifice and cell, so every station that is worked from its
        /// front — the galley, and the smelter (design 62 §9) — shares the one rule.
        /// </summary>
        public static int StandAt(PawnContext ctx, Pawn pawn, int edifice, int cell)
        {
            PlacedEdifice placed = ctx.Construction != null
                ? ctx.Construction.Edifices.Records[edifice] : default;
            GridSize size = ctx.Size;
            CellRef at = size.FromIndex(cell);
            int f = placed.Facing & 3;
            int x = at.X + (f == 1 ? 1 : f == 3 ? -1 : 0);
            int z = at.Z + (f == 0 ? 1 : f == 2 ? -1 : 0);
            if (size.Contains(x, z, at.Y))
            {
                int front = size.Index(x, z, at.Y);
                if (ctx.Cells.IsWalkable(front) && ctx.Reachable(pawn, front)) return front;
            }

            return FellJobDriver.StandBeside(ctx, pawn, cell);
        }

        /// <summary>
        /// The next thing the pan wants, or null: raw food until it holds the recipe's amount, then
        /// the station's fuel. The nearest by walking distance, and among equals the stack that has
        /// gone longest — which is every raw food's tie today, because nothing rots yet (K2).
        /// </summary>
        public static ColonyItem? NextLoad(Pawn pawn, PawnContext ctx, Kitchen kitchen, CookStation station,
            RecipeDef recipe)
        {
            RecipeStation? terms = kitchen.TermsAt(station, recipe);
            if (terms == null) return null;

            if (station.PanNutrition < recipe.ingredientNutrition) return NearestIngredient(pawn, ctx);
            if (terms.NeedsFuel && station.FuelIn < terms.fuelCount)
                return DeliverWorkGiver.NearestLoad(pawn, ctx, terms.fuelItem);
            return null;
        }

        /// <summary>The nearest raw food a colonist could fetch: on the ground or in a store, unforbidden, unclaimed and reachable.</summary>
        public static ColonyItem? NearestIngredient(Pawn pawn, PawnContext ctx)
        {
            ColonyItem? best = null;
            int bestDistance = int.MaxValue;
            var items = ctx.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                ColonyItem item = items[i];
                if (item.Despawned || item.Forbidden) continue;
                ItemDef def = ctx.Content.Items[item.DefIndex];
                if (!def.rawIngredient || def.nutrition <= 0) continue;

                int at = ctx.WhereIs(item);
                if (at < 0) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Item, item.Id.Value);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, at);
                if (distance >= bestDistance) continue;
                if (!ctx.Reachable(pawn, at)) continue;

                bestDistance = distance;
                best = item;
            }

            return best;
        }
    }

    /// <summary>
    /// <c>Job_Cook</c> (design 48 §5). Two shapes on one driver, chosen by whether the job names a
    /// thing to fetch:
    ///
    /// <list type="bullet">
    /// <item><b>A load.</b> Walk to it, take up as many as the pan still wants, carry them to the
    /// station and tip them in. The job ends there; the next think gives the next load, or the
    /// cooking.</item>
    /// <item><b>The cooking.</b> Walk to the station, roll the burn once, and work until the meal
    /// is done — the work banked on the station, so a cook called away leaves it for the next.
    /// Then the meal is set down at the cook's feet for the haulers.</item>
    /// </list>
    ///
    /// <para>The station is <see cref="Job.DestCell"/> and the cook's stance
    /// <see cref="Job.WorkTicks"/>, both saved and hashed with the job — the refuel job's own
    /// arrangement, so nothing here needs a field of its own.</para>
    /// </summary>
    public class CookJobDriver : JobDriver
    {
        const int ToilFetch = 0, ToilLift = 1, ToilCarry = 2, ToilTipIn = 3, ToilGo = 4, ToilCook = 5;

        public override int WorkType => WorkTypeIndex.Cooking;

        public override int WorkFocus => ToilIndex == ToilCook ? Job.DestCell : -1;

        public override void Begin(Pawn pawn, Job job)
        {
            base.Begin(pawn, job);
            if (job.TargetItem == ThingId.None) ToilIndex = ToilGo;
        }

        CookStation? Station(PawnContext ctx)
        {
            Kitchen? kitchen = ctx.Kitchen;
            if (kitchen == null || (uint)Job.DestCell >= (uint)ctx.Size.CellCount) return null;
            int edifice = ctx.Cells.Edifice[Job.DestCell];
            return edifice < 0 ? null : kitchen.At(edifice);
        }

        public override bool TryMakeReservations(PawnContext ctx)
        {
            CookStation? station = Station(ctx);
            if (station == null) return false;

            if (Job.TargetItem != ThingId.None)
            {
                var item = ctx.Items.Get(Job.TargetItem);
                if (item == null || ctx.WhereIs(item) < 0) return false;
                long itemKey = ReservationManager.Key(ReservationTargetKind.Item, Job.TargetItem.Value);
                if (!ctx.Reservations.Reserve(Pawn.Id, itemKey)) return false;
                Pawn.HeldReservations.Add(itemKey);
            }

            long deviceKey = ReservationManager.Key(ReservationTargetKind.Device, station.Edifice);
            if (!ctx.Reservations.Reserve(Pawn.Id, deviceKey)) return false;
            Pawn.HeldReservations.Add(deviceKey);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            Kitchen? kitchen = ctx.Kitchen;
            CookStation? station = Station(ctx);
            if (kitchen == null || station == null) return JobStatus.Failed;

            Bill? bill = kitchen.ActiveBill(station);
            // A pan already on the hob is finished whatever the bills now say: food in it is food.
            if (bill == null && !station.PanInUse) return JobStatus.Failed;
            RecipeDef recipe = ctx.Content.Recipes[bill?.Recipe ?? RecipeHandle.Meal];

            switch (ToilIndex)
            {
                case ToilFetch:
                {
                    var item = ctx.Items.Get(Job.TargetItem);
                    if (item == null || !StillAt(ctx, item, Job.TargetCell)) return JobStatus.Failed;
                    JobStatus walk = GotoCell(ctx, Job.TargetCell);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                case ToilLift:
                    return Lift(ctx, kitchen, station, recipe);

                case ToilCarry:
                {
                    JobStatus walk = GotoCell(ctx, Job.WorkTicks);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                case ToilTipIn:
                {
                    if (Job.CarriedItem < 0) return JobStatus.Failed;
                    ColonyItem? carried = ctx.Items.Get(new ThingId(Job.CarriedItem));
                    if (carried == null) return JobStatus.Failed;
                    if (!kitchen.AddToPan(station, recipe, carried.DefIndex, carried.Stack)) return JobStatus.Failed;
                    ctx.Items.Despawn(carried);
                    Job.CarriedItem = -1;
                    Pawn.BeginGesture(PawnGesture.Stow);
                    return JobStatus.Succeeded;
                }

                case ToilGo:
                {
                    if (!kitchen.PanReady(station, recipe)) return JobStatus.Failed;
                    JobStatus walk = GotoCell(ctx, Job.WorkTicks);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                default:
                    return Cook(ctx, kitchen, station, recipe, bill);
            }
        }

        JobStatus Cook(PawnContext ctx, Kitchen kitchen, CookStation station, RecipeDef recipe, Bill? bill)
        {
            // The power went, or the station was switched off: the meal waits on the hob with its
            // work banked, and the cook goes and does something else.
            if (!kitchen.Ready(station) || !kitchen.PanReady(station, recipe)) return JobStatus.Failed;
            if (!StillInReach(ctx, Pawn, Job.DestCell, 0, 0))
            {
                ToilIndex = ToilGo;
                ToilProgress = 0;
                return JobStatus.Ongoing;
            }

            if (station.Burn < 0)
            {
                int chance = kitchen.BurnChancePerMille(station, recipe, Pawn.SkillLevel(SkillIndex.Cooking));
                var roll = DeterministicRandom.ForTick(ctx.Seed, ctx.CurrentTick,
                    PawnPurpose.Burn ^ (uint)station.Edifice);
                station.Burn = roll.NextInt(1_000) < chance ? 1 : 0;
            }

            int rate = Pawn.WorkRatePerMille(WorkTypeIndex.Cooking);
            station.CookMilliwork += rate;
            ToilProgress += rate;
            Work(ctx);

            if (station.CookMilliwork < kitchen.WorkMilli(station, recipe)) return JobStatus.Ongoing;

            int product = kitchen.Finish(station, recipe, bill);
            int at = ctx.Items.NearestCellWithSpace(ctx.Cells, Pawn.Cell, product, 1, DropSearchRadius);
            if (at >= 0)
            {
                ctx.Items.Spawn(product, at, 1);
                Pawn.BeginGesture(PawnGesture.Stow);
            }
            return JobStatus.Succeeded;
        }

        /// <summary>
        /// <see cref="JobDriver.LiftToil"/>'s motion and timing, taking only what the pan still
        /// wants: three carrots from a pile of forty, and the rest left where they were.
        /// </summary>
        JobStatus Lift(PawnContext ctx, Kitchen kitchen, CookStation station, RecipeDef recipe)
        {
            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null) return JobStatus.Failed;

            int total = ctx.Content.LiftTicks * Rates.Scale;
            int grasp = ctx.Content.LiftGraspTicks * Rates.Scale;
            if (grasp > total) grasp = total;
            if (grasp < 1) grasp = 1;

            if (ToilProgress == 0) Pawn.BeginGesture(PawnGesture.Lift);
            int elapsed = ToilProgress += Rates.Scale;

            if (elapsed < grasp) return AtHand(ctx, item) ? JobStatus.Ongoing : JobStatus.Failed;

            if (elapsed == grasp)
            {
                if (!AtHand(ctx, item)) return JobStatus.Failed;
                int want = Wanted(ctx, kitchen, station, recipe, item.DefIndex);
                if (want <= 0) return JobStatus.Failed;
                ColonyItem taken = want >= item.Stack ? item : ctx.Items.SplitOff(item, want, Pawn.Id);
                if (taken == item) ctx.Items.PickUp(item, Pawn.Id);
                Job.CarriedItem = taken.Id.Value;
            }

            if (elapsed < total) return JobStatus.Ongoing;
            NextToil();
            return JobStatus.Ongoing;
        }

        /// <summary>How many of this def the pan still wants: enough raw food to reach the recipe's amount, or the fuel it is short.</summary>
        static int Wanted(PawnContext ctx, Kitchen kitchen, CookStation station, RecipeDef recipe, int defIndex)
        {
            ItemDef def = ctx.Content.Items[defIndex];
            if (def.rawIngredient && def.nutrition > 0)
            {
                int short_ = recipe.ingredientNutrition - station.PanNutrition;
                return short_ <= 0 ? 0 : (short_ + def.nutrition - 1) / def.nutrition;
            }

            RecipeStation? terms = kitchen.TermsAt(station, recipe);
            if (terms != null && terms.NeedsFuel && defIndex == terms.fuelItem)
                return terms.fuelCount - station.FuelIn;
            return 0;
        }

        public override void Cleanup(PawnContext ctx, JobStatus status) => DropCarried(ctx);
    }
}
