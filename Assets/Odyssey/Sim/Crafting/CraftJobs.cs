#nullable enable
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Cooking;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Crafting
{
    /// <summary>
    /// The Crafting work type's one giver (design 62 §9): the nearest station this colonist can
    /// reach and claim that has a bill wanting work, and then whichever half of the bill is next —
    /// fetch one load of an ingredient into the station, or work the batch the station holds.
    ///
    /// <para><b>The cook's shape, not the cook's food.</b> One load a job, the batch banked on the
    /// station, the station reserved as a device, the stance in front of it — all
    /// <see cref="CookWorkGiver"/>'s arrangements, because they are right for any station. What is
    /// fetched is named by the recipe item by item rather than being any raw food by nutrition.</para>
    ///
    /// <para><b>No fuel is fetched here.</b> The smelter burns from a hopper a hauler fills
    /// (<see cref="RefuelWorkGiver"/>); a station short of fuel with its ore all in is simply not
    /// offered, which is what "an empty hopper stops work" means.</para>
    ///
    /// <para>Scales with the stations that have ever had a bill, and only on a think by a colonist
    /// with Crafting switched on.</para>
    /// </summary>
    public sealed class CraftWorkGiver : WorkGiver
    {
        public override string Name => "Craft";

        public override int WorkType => WorkTypeIndex.Crafting;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            Workshop? shop = ctx.Workshop;
            if (shop == null) return false;

            var stations = shop.Stations;
            CraftStation? best = null;
            ColonyItem? bestLoad = null;
            int bestStand = -1, bestDistance = int.MaxValue;
            for (int s = 0; s < stations.Count; s++)
            {
                CraftStation station = stations[s];
                if (station.Removed) continue;
                if (shop.ActiveBill(station) == null) continue;

                int cell = shop.CellOf(station);
                if (ctx.Designations?.At(cell) == Designations.DesignationKind.Deconstruct) continue;

                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Device, station.Edifice);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int handle = shop.WorkRecipe(station);
                if ((uint)handle >= (uint)ctx.Content.Recipes.Length) continue;
                RecipeDef recipe = ctx.Content.Recipes[handle];

                // Something to do there: the batch is in and there is fuel to work it, or there is
                // something to fetch for it.
                ColonyItem? load = null;
                if (shop.Complete(station, recipe))
                {
                    if (!shop.Fuelled(station)) continue;
                }
                else
                {
                    load = NextLoad(pawn, ctx, shop, station, recipe);
                    if (load == null) continue;
                }

                int stand = CookWorkGiver.StandAt(ctx, pawn, station.Edifice, cell);
                if (stand < 0) continue;

                bestDistance = distance;
                best = station;
                bestLoad = load;
                bestStand = stand;
            }

            if (best == null) return false;

            job.Reset(JobIndex.Craft);
            job.DestCell = shop.CellOf(best);
            job.WorkTicks = bestStand;
            if (bestLoad != null)
            {
                job.TargetItem = bestLoad.Id;
                job.TargetCell = ctx.WhereIs(bestLoad);
            }
            return true;
        }

        /// <summary>
        /// The next thing the batch wants, or null: the nearest reachable, unclaimed stack of the
        /// first ingredient still short, in the recipe's order.
        /// </summary>
        public static ColonyItem? NextLoad(Pawn pawn, PawnContext ctx, Workshop shop, CraftStation station,
            RecipeDef recipe)
        {
            for (int n = 0; n < recipe.ingredients.Count; n++)
            {
                if (shop.Short(station, recipe, n) <= 0) continue;
                ColonyItem? load = DeliverWorkGiver.NearestLoad(pawn, ctx, recipe.ingredients[n].Item);
                if (load != null) return load;
            }
            return null;
        }
    }

    /// <summary>
    /// <c>Job_Craft</c> (design 62 §9). Two shapes on one driver, chosen by whether the job names
    /// a thing to fetch — <see cref="CookJobDriver"/>'s arrangement exactly:
    ///
    /// <list type="bullet">
    /// <item><b>A load.</b> Walk to it, take up as many as the batch still wants, carry them to
    /// the station and put them in. The job ends there; the next think gives the next load, or the
    /// work.</item>
    /// <item><b>The work.</b> Walk to the station, take the batch's fuel from the hopper once, and
    /// work until it is done — the work banked on the station, so a crafter called away leaves it
    /// for the next. Then the products are put down at the crafter's feet for the haulers.</item>
    /// </list>
    ///
    /// <para>The station is <see cref="Job.DestCell"/> and the stance <see cref="Job.WorkTicks"/>,
    /// both saved and hashed with the job, so nothing here needs a field of its own.</para>
    /// </summary>
    public class CraftJobDriver : JobDriver
    {
        const int ToilFetch = 0, ToilLift = 1, ToilCarry = 2, ToilPutIn = 3, ToilGo = 4, ToilWork = 5;

        public override int WorkType => WorkTypeIndex.Crafting;

        public override int WorkFocus => ToilIndex == ToilWork ? Job.DestCell : -1;

        public override void Begin(Pawn pawn, Job job)
        {
            base.Begin(pawn, job);
            if (job.TargetItem == ThingId.None) ToilIndex = ToilGo;
        }

        CraftStation? Station(PawnContext ctx) => ctx.Workshop?.AtCell(Job.DestCell);

        public override bool TryMakeReservations(PawnContext ctx)
        {
            CraftStation? station = Station(ctx);
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
            Workshop? shop = ctx.Workshop;
            CraftStation? station = Station(ctx);
            if (shop == null || station == null) return JobStatus.Failed;

            // A batch already in is finished whatever the bills now say: ore in the crucible is ore.
            int handle = shop.WorkRecipe(station);
            if ((uint)handle >= (uint)ctx.Content.Recipes.Length) return JobStatus.Failed;
            RecipeDef recipe = ctx.Content.Recipes[handle];

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
                    return Lift(ctx, shop, station, recipe);

                case ToilCarry:
                {
                    JobStatus walk = GotoCell(ctx, Job.WorkTicks);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                case ToilPutIn:
                {
                    if (Job.CarriedItem < 0) return JobStatus.Failed;
                    ColonyItem? carried = ctx.Items.Get(new ThingId(Job.CarriedItem));
                    if (carried == null) return JobStatus.Failed;
                    if (!shop.AddIngredient(station, handle, carried.DefIndex, carried.Stack)) return JobStatus.Failed;
                    ctx.Items.Despawn(carried);
                    Job.CarriedItem = -1;
                    Pawn.BeginGesture(PawnGesture.Stow);
                    return JobStatus.Succeeded;
                }

                case ToilGo:
                {
                    if (!shop.Complete(station, recipe) || !shop.Fuelled(station)) return JobStatus.Failed;
                    JobStatus walk = GotoCell(ctx, Job.WorkTicks);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                default:
                    return WorkBatch(ctx, shop, station, recipe);
            }
        }

        JobStatus WorkBatch(PawnContext ctx, Workshop shop, CraftStation station, RecipeDef recipe)
        {
            if (!shop.Complete(station, recipe)) return JobStatus.Failed;
            if (!StillInReach(ctx, Pawn, Job.DestCell, 0, 0))
            {
                ToilIndex = ToilGo;
                ToilProgress = 0;
                return JobStatus.Ongoing;
            }

            // The fuel goes in as the work starts, once: a hopper that is short stops the batch
            // before it begins, and one that runs dry later never strands it half-worked.
            if (!shop.PayFuel(station, recipe)) return JobStatus.Failed;

            int rate = Pawn.WorkRatePerMille(WorkTypeIndex.Crafting);
            station.CraftMilliwork += rate;
            ToilProgress += rate;
            Work(ctx);

            if (station.CraftMilliwork < shop.WorkMilli(station, recipe)) return JobStatus.Ongoing;

            RecipeDef? made = shop.Finish(station);
            if (made != null)
            {
                for (int p = 0; p < made.products.Count; p++)
                {
                    RecipeCount product = made.products[p];
                    int at = ctx.Items.NearestCellWithSpace(ctx.Cells, Pawn.Cell, product.Item, product.count,
                        DropSearchRadius);
                    if (at >= 0) ctx.Items.Spawn(product.Item, at, product.count);
                }
                Pawn.BeginGesture(PawnGesture.Stow);
            }
            return JobStatus.Succeeded;
        }

        /// <summary>
        /// <see cref="JobDriver.LiftToil"/>'s motion and timing, taking only what the batch still
        /// wants: ten ore from a pile of seventy-five, and the rest left where it was.
        /// </summary>
        JobStatus Lift(PawnContext ctx, Workshop shop, CraftStation station, RecipeDef recipe)
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
                int want = shop.Wanted(station, recipe, item.DefIndex);
                if (want <= 0) return JobStatus.Failed;
                ColonyItem taken = want >= item.Stack ? item : ctx.Items.SplitOff(item, want, Pawn.Id);
                if (taken == item) ctx.Items.PickUp(item, Pawn.Id);
                Job.CarriedItem = taken.Id.Value;
            }

            if (elapsed < total) return JobStatus.Ongoing;
            NextToil();
            return JobStatus.Ongoing;
        }

        public override void Cleanup(PawnContext ctx, JobStatus status) => DropCarried(ctx);
    }
}
