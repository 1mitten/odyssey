#nullable enable
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Power;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Hand a colonist an ordered line to lay (design 32 §3).
    ///
    /// <para><b>One job fetches the scrap metal and lays the line</b>, where a wall is two — a
    /// delivery and a build. A line costs one scrap metal and nothing waits for it, so a separate
    /// delivery would be a second walk to put one piece down beside the cell the same colonist then walks back
    /// to. The carry is the delivery's (fetch, lift, carry) and the work is the build's (swing at
    /// the site, bank the work on it), in one driver.</para>
    ///
    /// <para>Construction work, after delivering and building for the reason deconstruct is: a
    /// hut waiting for its last plank matters more than the wire to it.</para>
    /// </summary>
    public sealed class LayConduitWorkGiver : WorkGiver
    {
        public override string Name => "LayConduit";

        public override int WorkType => WorkTypeIndex.Construction;

        public override int IntraPriority => 1;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            PowerGrid? power = ctx.Power;
            if (power == null) return false;

            var sites = power.Sites;
            if (sites.Count == 0) return false;

            // The scrap metal is the nearest to the colonist whichever line she lays, so it is found
            // once: none anywhere reachable is no line anywhere, and the scan stops before it starts.
            ColonyItem? load = LineLoad(pawn, ctx);
            if (load == null) return false;

            int bestSite = -1, bestStand = -1, bestDistance = int.MaxValue;
            for (int i = 0; i < sites.Count; i++)
            {
                int site = sites[i];
                int distance = ctx.Distance(pawn.Cell, site);
                if (distance >= bestDistance) continue;

                // A claim of its own kind: a builder on a wall in the same cell holds a Cell
                // claim, and the two must not block each other — a cell holds a wall order and a
                // line order at once.
                long key = ReservationManager.Key(ReservationTargetKind.Conduit, site);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                // Worked like a slab, not a wall: a line in open air — up a shaft — has nothing
                // beside it to stand on, and is reached from below.
                int stand = BuildWorkGiver.StandToBuild(ctx, pawn, site, slab: true);
                if (stand < 0) continue;

                bestDistance = distance;
                bestSite = site;
                bestStand = stand;
            }

            if (bestSite < 0) return false;
            ColonyItem bestLoad = load;

            job.Reset(JobIndex.LayConduit);
            job.TargetItem = bestLoad.Id;
            job.TargetCell = ctx.WhereIs(bestLoad);
            job.DestCell = bestSite;
            job.WorkTicks = bestStand;
            return true;
        }

        /// <summary>
        /// What a line is laid with: the nearer of the nearest scrap metal and the nearest copper
        /// bar (design 62 §9, <see cref="BuildingDef.partAltItem"/>), scrap on a tie. A colony
        /// with no copper bars asks exactly what it asked before there were any, and gets the
        /// same answer.
        /// </summary>
        public static ColonyItem? LineLoad(Pawn pawn, PawnContext ctx)
        {
            BuildingDef conduit = ConstructionContent.BuildingAt(BuildingHandle.Conduit);
            ColonyItem? scrap = DeliverWorkGiver.NearestLoad(pawn, ctx, conduit.partItem);
            if (conduit.partAltItem < 0) return scrap;
            ColonyItem? copper = DeliverWorkGiver.NearestLoad(pawn, ctx, conduit.partAltItem);
            if (copper == null) return scrap;
            if (scrap == null) return copper;
            return ctx.Distance(pawn.Cell, ctx.WhereIs(copper)) < ctx.Distance(pawn.Cell, ctx.WhereIs(scrap))
                ? copper : scrap;
        }
    }

    /// <summary>
    /// Fetch the line's scrap metal, carry it to an ordered line, and work until the line is in.
    /// Or a copper bar in its place (design 62 §9): the driver spends whatever it carried, and
    /// the giver chose it (<see cref="LayConduitWorkGiver.LineLoad"/>).
    ///
    /// <para>Toils: walk to the scrap metal, take it up, carry it to the stance, work, settle. Whatever is
    /// left of the stack after the one piece the line takes is put down by <see cref="Cleanup"/>
    /// near the site — which is where the next line of the run wants it.</para>
    /// </summary>
    public class LayConduitJobDriver : JobDriver
    {
        const int WorkToil = 3;
        const int SettleAfterWork = 4;

        public override int WorkType => WorkTypeIndex.Construction;

        public override int WorkFocus => ToilIndex == WorkToil ? Job.DestCell : -1;

        public override bool TryMakeReservations(PawnContext ctx)
        {
            PowerGrid? power = ctx.Power;
            if (power == null || !power.HasSite(Job.DestCell)) return false;

            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null || ctx.WhereIs(item) < 0) return false;

            long itemKey = ReservationManager.Key(ReservationTargetKind.Item, Job.TargetItem.Value);
            long siteKey = ReservationManager.Key(ReservationTargetKind.Conduit, Job.DestCell);

            if (!ctx.Reservations.Reserve(Pawn.Id, itemKey)) return false;
            Pawn.HeldReservations.Add(itemKey);

            if (!ctx.Reservations.Reserve(Pawn.Id, siteKey)) return false;
            Pawn.HeldReservations.Add(siteKey);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            // Before every guard: by now the line is in and the order gone.
            if (ToilIndex == SettleAfterWork) return Settle(ctx);

            PowerGrid? power = ctx.Power;
            if (power == null) return JobStatus.Failed;

            // Cancelled, or laid by somebody else, while this colonist walked.
            int site = Job.DestCell;
            if (!power.HasSite(site)) return JobStatus.Failed;

            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null) return JobStatus.Failed;

            switch (ToilIndex)
            {
                case 0:
                {
                    if (!StillAt(ctx, item, Job.TargetCell)) return JobStatus.Failed;
                    JobStatus walk = GotoCell(ctx, Job.TargetCell);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                case 1:
                    return LiftToil(ctx, item);

                case 2:
                {
                    JobStatus walk = GotoCell(ctx, Job.WorkTicks);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                default:
                {
                    if (Job.CarriedItem < 0) return JobStatus.Failed;
                    if (!StillInReach(ctx, Pawn, site, layersAbove: 0, layersBelow: 1))
                    {
                        // Moved off the stance: walk back to it, the scrap metal still in hand.
                        ToilIndex = 2;
                        ToilProgress = 0;
                        return JobStatus.Ongoing;
                    }

                    int rate = Pawn.WorkRatePerMille(WorkTypeIndex.Construction);
                    ToilProgress += rate;
                    Work(ctx);
                    int price = ConstructionContent.WorkFor(BuildingHandle.Conduit, StuffHandle.Wood) * Rates.Scale;
                    if (power.AddSiteWork(site, rate) < price) return JobStatus.Ongoing;

                    // The line's scrap metal goes into it as it goes in.
                    item.Stack -= ConstructionContent.BuildingAt(BuildingHandle.Conduit).partCount;
                    if (item.Stack <= 0)
                    {
                        ctx.Items.Despawn(item);
                        Job.CarriedItem = -1;
                    }

                    ctx.Defer(_ => power.Lay(site));
                    NextToil();
                    return JobStatus.Ongoing;
                }
            }
        }

        public override void Cleanup(PawnContext ctx, JobStatus status) => DropCarried(ctx);
    }

    /// <summary>
    /// Hand a colonist a line marked to come up. Construction work, last — the deconstruct
    /// giver's place and its reason: what is being removed is already standing and doing no harm.
    /// </summary>
    public sealed class RemoveConduitWorkGiver : WorkGiver
    {
        public override string Name => "RemoveConduit";

        public override int WorkType => WorkTypeIndex.Construction;

        public override int IntraPriority => 2;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            PowerGrid? power = ctx.Power;
            if (power == null) return false;

            var marks = power.Marks;
            int best = -1, bestStand = -1, bestDistance = int.MaxValue;
            for (int i = 0; i < marks.Count; i++)
            {
                int cell = marks[i];
                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Conduit, cell);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int stand = BuildWorkGiver.StandToBuild(ctx, pawn, cell, slab: true);
                if (stand < 0) continue;

                bestDistance = distance;
                best = cell;
                bestStand = stand;
            }

            if (best < 0) return false;

            job.Reset(JobIndex.RemoveConduit);
            job.TargetCell = bestStand;
            job.DestCell = best;
            return true;
        }
    }

    /// <summary>
    /// Walk to a marked line and take it up, leaving the reference's half of its one scrap metal — which,
    /// by the seeded coin flip every refund uses, is nothing or one (design 32 §3).
    /// </summary>
    public class RemoveConduitJobDriver : JobDriver
    {
        public override int WorkType => WorkTypeIndex.Construction;

        public override int WorkFocus => ToilIndex == 1 ? Job.DestCell : -1;

        public override bool TryMakeReservations(PawnContext ctx)
        {
            if (Job.TargetCell < 0 || Job.DestCell < 0) return false;
            long key = ReservationManager.Key(ReservationTargetKind.Conduit, Job.DestCell);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            if (ToilIndex == SettleToil) return Settle(ctx);

            PowerGrid? power = ctx.Power;
            if (power == null) return JobStatus.Failed;

            int cell = Job.DestCell;
            if (!power.IsMarked(cell) || !power.IsLine(cell)) return JobStatus.Failed;

            if (ToilIndex == 0)
            {
                JobStatus walk = GotoCell(ctx, Job.TargetCell);
                if (walk == JobStatus.Succeeded) NextToil();
                return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            if (!StillInReach(ctx, Pawn, cell, layersAbove: 0, layersBelow: 1))
            {
                WalkBack();
                return JobStatus.Ongoing;
            }

            int rate = Pawn.WorkRatePerMille(WorkTypeIndex.Construction);
            ToilProgress += rate;
            Work(ctx);
            int price = ConstructionContent.WorkToDeconstruct(BuildingHandle.Conduit, StuffHandle.Wood) * Rates.Scale;
            if (power.AddMarkWork(cell, rate) < price) return JobStatus.Ongoing;

            int tick = ctx.CurrentTick;
            ctx.Defer(_ => TakeUp(ctx, power, cell, tick));
            NextToil();
            return JobStatus.Ongoing;
        }

        /// <summary>The line comes out, and whatever the refund pays lands on the floor under it.</summary>
        public static void TakeUp(PawnContext ctx, PowerGrid power, int cell, int tick)
        {
            if (!power.TakeUp(cell)) return;

            int refund = DeconstructJobDriver.RefundParts(ctx, cell, BuildingHandle.Conduit, tick);
            if (refund <= 0) return;

            int item = ConstructionContent.BuildingAt(BuildingHandle.Conduit).partItem;
            int landing = ctx.Cells.FirstFloorAtOrBelow(cell);
            int at = ctx.Items.NearestCellWithSpace(ctx.Cells, landing, item, refund, maxRadius: 3);
            if (at >= 0) ctx.Items.Spawn(item, at, refund);
        }
    }

    /// <summary>
    /// Keep a generator fed (design 32 §6): when its hopper is below half, a colonist fetches fuel
    /// and fills it to the top.
    ///
    /// <para><b>Hauling, and before the tidying.</b> It is carrying, so it trains and prices as
    /// carrying; and a generator running dry takes a net dark, which matters more than a log lying
    /// in the wrong place — so it is scanned first in the hauling work type (a-07 §3 records the
    /// reference's generators running dry while pawns tidied).</para>
    ///
    /// <para>Not offered for a generator somebody has ordered taken down (the fuel would go with
    /// it), nor for one nobody can reach — <c>docs/bug-patterns.md</c>'s "every giver asks
    /// <c>ctx.Reachable</c>", which the growing givers were found missing.</para>
    /// </summary>
    public sealed class RefuelWorkGiver : WorkGiver
    {
        public override string Name => "Refuel";

        public override int WorkType => WorkTypeIndex.Haul;

        public override int IntraPriority => -1;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            PowerGrid? power = ctx.Power;
            Crafting.Workshop? shop = ctx.Workshop;
            if (power == null && shop == null) return false;

            var edifices = ctx.Construction?.Edifices.Records;
            if (edifices == null) return false;

            int best = -1, bestStand = -1, bestDistance = int.MaxValue;
            ColonyItem? bestLoad = null;
            var devices = power?.Devices;
            for (int i = 0; devices != null && i < devices.Count; i++)
            {
                int edifice = devices[i].Edifice;
                if (!power!.NeedsRefuel(edifice)) continue;
                if ((uint)edifice >= (uint)edifices.Count) continue;

                int head = edifices[edifice].CellIndex;
                if (ctx.Designations?.At(head) == Designations.DesignationKind.Deconstruct) continue;

                int distance = ctx.Distance(pawn.Cell, head);
                if (distance >= bestDistance) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Device, edifice);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int stand = StandToFeed(ctx, pawn, edifices[edifice]);
                if (stand < 0) continue;

                int fuel = FuelItemOf(edifices[edifice].Def);
                if (fuel < 0) continue;
                ColonyItem? load = DeliverWorkGiver.NearestLoad(pawn, ctx, fuel);
                if (load == null) continue;

                bestDistance = distance;
                best = head;
                bestStand = stand;
                bestLoad = load;
            }

            // A crafting station's hopper (design 62 §9), on the same terms and in the same race by
            // distance: a smelter with a bill to work and its hopper below half. None exists in a
            // colony that has never smelted, so the scan above answers exactly as it did before.
            var stations = shop?.Stations;
            for (int i = 0; stations != null && i < stations.Count; i++)
            {
                Crafting.CraftStation station = stations[i];
                if (!shop!.NeedsRefuel(station)) continue;
                if ((uint)station.Edifice >= (uint)edifices.Count) continue;

                int head = edifices[station.Edifice].CellIndex;
                if (ctx.Designations?.At(head) == Designations.DesignationKind.Deconstruct) continue;

                int distance = ctx.Distance(pawn.Cell, head);
                if (distance >= bestDistance) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Device, station.Edifice);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int stand = StandToFeed(ctx, pawn, edifices[station.Edifice]);
                if (stand < 0) continue;

                ColonyItem? load = shop.FuelLoad(pawn, station);
                if (load == null) continue;

                bestDistance = distance;
                best = head;
                bestStand = stand;
                bestLoad = load;
            }

            if (best < 0 || bestLoad == null) return false;

            job.Reset(JobIndex.Refuel);
            job.TargetItem = bestLoad.Id;
            job.TargetCell = ctx.WhereIs(bestLoad);
            job.DestCell = best;
            job.WorkTicks = bestStand;
            return true;
        }

        /// <summary>Beside either of the building's cells — a generator is blocking, so beside is the only place.</summary>
        internal static int StandToFeed(PawnContext ctx, Pawn pawn, Worldgen.PlacedEdifice placed)
        {
            int stand = FellJobDriver.StandBeside(ctx, pawn, placed.CellIndex);
            if (stand >= 0) return stand;
            int second = EdificeFootprint.SecondCell(placed.CellIndex, placed.Def, placed.Facing, ctx.Size);
            return second >= 0 ? FellJobDriver.StandBeside(ctx, pawn, second) : -1;
        }

        internal static int FuelItemOf(ushort edifice)
        {
            int building = ConstructionContent.BuildingForEdifice(edifice);
            return building == BuildingHandle.None ? -1 : ConstructionContent.BuildingAt(building).fuelItem;
        }
    }

    /// <summary>
    /// Fetch a stack of fuel, carry it to a generator, and tip in as much as it will take. What
    /// is left is put down beside it by <see cref="Cleanup"/>.
    ///
    /// <para><b>Or to a crafting station's hopper</b> (design 62 §9): the smelter's coal or wood.
    /// The destination is a power building first — a generator is never a crafting station — and
    /// the workshop's otherwise, so a generator's refuel runs exactly as it did before.</para>
    /// </summary>
    public class RefuelJobDriver : JobDriver
    {
        public override int WorkType => WorkTypeIndex.Haul;

        /// <summary>The power building at the destination, or -1.</summary>
        int GeneratorAt(PawnContext ctx) => ctx.Power?.EdificeAt(Job.DestCell) ?? -1;

        /// <summary>The crafting station at the destination, or null. Asked only where no power building stands.</summary>
        Crafting.CraftStation? HopperAt(PawnContext ctx) => ctx.Workshop?.AtCell(Job.DestCell);

        public override bool TryMakeReservations(PawnContext ctx)
        {
            int edifice = GeneratorAt(ctx);
            if (edifice < 0) edifice = HopperAt(ctx)?.Edifice ?? -1;
            if (edifice < 0) return false;

            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null || ctx.WhereIs(item) < 0) return false;

            long itemKey = ReservationManager.Key(ReservationTargetKind.Item, Job.TargetItem.Value);
            long deviceKey = ReservationManager.Key(ReservationTargetKind.Device, edifice);

            if (!ctx.Reservations.Reserve(Pawn.Id, itemKey)) return false;
            Pawn.HeldReservations.Add(itemKey);

            if (!ctx.Reservations.Reserve(Pawn.Id, deviceKey)) return false;
            Pawn.HeldReservations.Add(deviceKey);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            PowerGrid? power = ctx.Power;
            Crafting.CraftStation? hopper = null;

            // The generator was taken down, or somebody else has filled it.
            int edifice = GeneratorAt(ctx);
            if (edifice >= 0)
            {
                if (power!.RoomForFuel(edifice) <= 0) return JobStatus.Failed;
            }
            else
            {
                hopper = HopperAt(ctx);
                if (hopper == null) return JobStatus.Failed;
            }

            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null) return JobStatus.Failed;
            // The same question of a hopper, which needs to know what is being carried to it.
            if (hopper != null && ctx.Workshop!.RoomFor(hopper, item.DefIndex) <= 0) return JobStatus.Failed;

            switch (ToilIndex)
            {
                case 0:
                {
                    if (!StillAt(ctx, item, Job.TargetCell)) return JobStatus.Failed;
                    JobStatus walk = GotoCell(ctx, Job.TargetCell);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                case 1:
                    return LiftToil(ctx, item);

                case 2:
                {
                    // The carry is the work, as it is for a haul.
                    Work(ctx);
                    JobStatus walk = GotoCell(ctx, Job.WorkTicks);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                default:
                {
                    if (Job.CarriedItem < 0) return JobStatus.Failed;
                    int put = hopper != null
                        ? ctx.Workshop!.AddFuel(hopper, item.DefIndex, item.Stack)
                        : power!.AddFuel(edifice, item.Stack);
                    if (put <= 0) return JobStatus.Failed;
                    item.Stack -= put;
                    Pawn.BeginGesture(PawnGesture.Stow);
                    if (item.Stack <= 0)
                    {
                        ctx.Items.Despawn(item);
                        Job.CarriedItem = -1;
                    }
                    return JobStatus.Succeeded;
                }
            }
        }

        public override void Cleanup(PawnContext ctx, JobStatus status) => DropCarried(ctx);
    }
}
