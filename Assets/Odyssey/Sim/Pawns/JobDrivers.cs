#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Take a thing to a stockpile: a loose one to any pile that will have it, or a stored one
    /// to a better pile than it is in.
    ///
    /// Toils: walk to the thing, pick it up, walk to the destination, put it down. Both the thing
    /// and the destination cell are claimed up front, which is what stops two haulers setting off
    /// for the same crate or for the same square. The destination may already hold a stack of
    /// the same def: the load merges into it, and the cell claim is what keeps a second hauler
    /// from counting on the same room.
    /// </summary>
    public class HaulJobDriver : JobDriver
    {
        /// <summary>
        /// The destination store, or null when the load is going on to the ground. Resolved from
        /// the destination <em>cell</em>, because that is all the job record carries and a cell
        /// holds at most one edifice.
        /// </summary>
        Storage.StorageUnit? Destination(PawnContext ctx) => ctx.StorageUnits?.AtCell(Job.DestCell);

        public override bool TryMakeReservations(PawnContext ctx)
        {
            var item = ctx.Items.Get(Job.TargetItem);
            // Somewhere real: on the floor, or in a store. A thing already in a pair of hands is
            // not something to be sent for.
            if (item == null || ctx.WhereIs(item) < 0) return false;

            // **Every question first, then every claim.** All or nothing before the toils run
            // (design 05 §2), and the reason this order matters rather than merely reading better:
            // a check that fails after a claim has been taken walks out holding it.
            Storage.StorageUnit? into = Destination(ctx);
            if (into != null)
            {
                if (!ctx.StorageUnits!.HasSpaceFor(into, item.DefIndex, item.Stack)) return false;
            }
            else if (!ctx.Items.CellHasSpace(Job.DestCell, item.DefIndex, item.Stack)) return false;

            long itemKey = ReservationManager.Key(ReservationTargetKind.Item, Job.TargetItem.Value);
            // The destination is claimed as whichever kind of thing it is. **Only the
            // destination** — a store being taken *out of* is deliberately left unclaimed, because
            // the item claim already stops two haulers lifting the same stack, and a claim on the
            // source would stop a second colonist putting something *into* a shelf while this one
            // empties it.
            long destKey = into != null
                ? ReservationManager.Key(ReservationTargetKind.Container, into.Edifice)
                : ReservationManager.Key(ReservationTargetKind.Cell, Job.DestCell);

            if (!ctx.Reservations.Reserve(Pawn.Id, itemKey)) return false;
            Pawn.HeldReservations.Add(itemKey);

            if (!ctx.Reservations.Reserve(Pawn.Id, destKey)) return false;
            Pawn.HeldReservations.Add(destKey);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
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
                    // Taken up rather than merely moved, and it takes time: LiftToil is the stoop,
                    // the grasp and the rise, so every job which ever lifts anything gets all three
                    // without being asked. Reaching into a shelf is the same motion and the same
                    // duration as stooping to the floor, deliberately.
                    return LiftToil(ctx, item);

                case 2:
                {
                    // The carry is the work of a haul. The walk to the thing is not counted,
                    // because the pawn is not hauling yet; the drop is one tick and is not
                    // counted either, so the experience is bounded by the carry alone.
                    Work(ctx);
                    JobStatus walk = GotoCell(ctx, Job.DestCell);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                default:
                {
                    // Checked again on arrival: something may have been dropped or eaten here
                    // meanwhile, and the destination claim guards against haulers, not against
                    // eaters — which is as true of a shelf somebody has just taken the last meal
                    // out of as it is of a cell.
                    Storage.StorageUnit? into = Destination(ctx);
                    if (into != null)
                    {
                        if (!ctx.StorageUnits!.HasSpaceFor(into, item.DefIndex, item.Stack))
                            return JobStatus.Failed;
                        PutInto(ctx, item, into);
                    }
                    else
                    {
                        if (!ctx.Items.CellHasSpace(Job.DestCell, item.DefIndex, item.Stack))
                            return JobStatus.Failed;
                        // The same motion the other way up, and only on a haul that *arrived*: see
                        // PutDown, and see Cleanup below for the failure path that deliberately
                        // says nothing.
                        PutDown(ctx, item, Job.DestCell);
                    }

                    Job.CarriedItem = -1;
                    return JobStatus.Succeeded;
                }
            }
        }

        // The drop-what-you-are-holding rule moved to JobDriver.DropCarried when delivery to a
        // building site became the second job that carries something: two copies of it would have
        // been two places to forget that a lost load is a hole in the colony's stores.
        public override void Cleanup(PawnContext ctx, JobStatus status) => DropCarried(ctx);
    }

    /// <summary>Walk to food, eat it, remember having done so.</summary>
    public class EatJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx)
        {
            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null || ctx.WhereIs(item) < 0) return false;

            // The item, and **not the store it is in**. Eating takes no slot and leaves the shelf
            // no fuller than it found it, so two colonists helping themselves from one pantry is
            // fine; the per-item claim is what keeps them off the same meal.
            long key = ReservationManager.Key(ReservationTargetKind.Item, Job.TargetItem.Value);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null) return JobStatus.Failed;

            if (ToilIndex == 0)
            {
                if (!StillAt(ctx, item, Job.TargetCell)) return JobStatus.Failed;
                JobStatus walk = GotoCell(ctx, Job.TargetCell);
                if (walk == JobStatus.Succeeded) NextToil();
                return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            // Eating is not work and no rate touches it, but the counter it advances is shared
            // with the toils that are: ToilProgress has one unit, and the unit is milliwork. A
            // bare ++ here left the same saved, hashed field counting ticks in this driver and
            // thousandths in the four that swing — which made Rates.FromSave wrong for an older
            // file caught mid-meal (it scaled a tick count by a thousand and the meal finished on
            // the next tick), and left this toil contributing nothing to the state hash for its
            // whole length. A toil that has no rate pays at exactly the standard one.
            ToilProgress += Rates.Scale;
            if (ToilProgress < ctx.Content.Jobs[Job.DefIndex].workTicks * Rates.Scale)
                return JobStatus.Ongoing;

            var need = ctx.Content.Needs[NeedIndex.Food];
            int nutrition = ctx.Content.Items[item.DefIndex].nutrition;
            Pawn.Needs[NeedIndex.Food] = System.Math.Min(need.max, Pawn.Needs[NeedIndex.Food] + nutrition);

            // One meal from the pile, not the pile. Despawning the whole item ate four meals per
            // sitting, which is why the soak found the pantry empty by the end of day one and
            // why every pantry-size estimate made before this was four times too high.
            if (item.Stack > 1) item.Stack--;
            else ctx.Items.Despawn(item);

            // What she thinks of it is the food's (design 48 §4): a cooked meal pleases, a ration
            // is what every food used to be, burnt or raw food she minds.
            int thought = ctx.Content.Items[item.DefIndex].ateThought;
            if ((uint)thought < (uint)ctx.Content.Thoughts.Length) Pawn.AddMemory(thought, ctx.CurrentTick);
            ctx.Kitchen?.Invalidate();
            return JobStatus.Succeeded;
        }
    }

    /// <summary>
    /// Sleep in a bed, or where the pawn stands when no bed is reachable.
    ///
    /// The rest a sleeping pawn gains is applied by the needs system rather than here, so that
    /// every rate in the game lives in one place and on one cadence.
    /// </summary>
    public class SleepJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx)
        {
            if (Job.TargetCell < 0) return true; // sleeping on the ground claims nothing

            long key = ReservationManager.Key(ReservationTargetKind.Cell, Job.TargetCell);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            if (ToilIndex == 0)
            {
                // The collapse catches up with the walk as well as the departure: rest that runs
                // out on the way to the bed drops the colonist where she is, and the bed waits
                // for whoever holds it next (WS3, design 17 §4c). She is about to sleep on the
                // ground all the same, so she carries the ground's thought with her — the one
                // toil 1 adds below when the giver found no bed at all, which is left to fire
                // there so a collapse is remembered once, never twice. The bed stays reserved
                // until the job ends: letting go here would need a second exit from the job, and
                // the one-exit rule is worth more than the hour an unreserved bed would buy.
                //
                // A colonist already standing on her bed is not collapsing on the way to it —
                // she has arrived, and the walk this branch exists to cut short is over. Without
                // the cell test she took the ground's thought while sleeping in her own bed,
                // because rest effectiveness is read off the cell and the thought was not: the
                // bed's rate and the mud's memory, on the one tick where her rest reached zero
                // as she arrived.
                if (Pawn.Needs[NeedIndex.Rest] <= 0 && Pawn.Cell != Job.TargetCell)
                {
                    if (Job.TargetCell >= 0)
                        Pawn.AddMemory(ThoughtIndex.SleptOnGround, ctx.CurrentTick);
                    NextToil();
                    return JobStatus.Ongoing;
                }

                if (Job.TargetCell < 0 || Pawn.Cell == Job.TargetCell)
                {
                    Claim(ctx);
                    NextToil();
                    return JobStatus.Ongoing;
                }

                JobStatus walk = GotoCell(ctx, Job.TargetCell);
                if (walk == JobStatus.Succeeded)
                {
                    Claim(ctx);
                    NextToil();
                }
                return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            Pawn.Asleep = true;
            // Milliwork, like every other toil: nothing reads this counter — the wake threshold
            // is what ends a sleep — but it is saved and hashed, and one field with two units is
            // what made a mid-sleep load and the sleeping half of the hash wrong. See the eat
            // toil above for the whole of that argument.
            ToilProgress += Rates.Scale;
            if (Pawn.Needs[NeedIndex.Rest] < ctx.Content.Kind.wakeThreshold && !WakesToEat(ctx)) return JobStatus.Ongoing;

            if (Job.TargetCell < 0) Pawn.AddMemory(ThoughtIndex.SleptOnGround, ctx.CurrentTick);

            // A night outside the temperature bands is remembered on waking (design 28 §8) —
            // the memory half, beside the mechanical half that ran all night as the sleep
            // factor. Read from where the sleep ended, which is where it happened.
            if (ctx.Temperature != null)
            {
                int temp = ctx.Temperature.CellTemp(Pawn.Cell, ctx.CurrentTick);
                if (ctx.Content.Temperature.BandOf(temp) >= 2)
                    Pawn.AddMemory(temp < ctx.Content.Temperature.comfortMinC
                        ? ThoughtIndex.SleptCold
                        : ThoughtIndex.SleptHot, ctx.CurrentTick);
            }
            return JobStatus.Succeeded;
        }

        /// <summary>
        /// Starving, she wakes to eat (owner, 2026-09-25: <i>"if colonist is starving - yes they would
        /// wake up"</i>) — but only when there is food she could eat. Woken with nothing to eat she
        /// would go straight back to bed and be woken again on the next pass, all night.
        ///
        /// <para>Asked on the needs cadence and only at zero food, so a colony that eats costs
        /// nothing, and a starving sleeper one food scan every needs interval. The scan is
        /// <see cref="CriticalNeedsThinkNode.TryEat"/>'s own, into a scratch job, so "could she
        /// eat" and "what will she eat" are one rule. Eating is asked before sleep by the think
        /// node, so the woken colonist eats rather than lying down again.</para>
        /// </summary>
        bool WakesToEat(PawnContext ctx)
        {
            if (Pawn.Needs[NeedIndex.Food] > 0) return false;
            if (ctx.CurrentTick % ctx.Content.NeedsIntervalTicks != 0) return false;
            return CriticalNeedsThinkNode.TryEat(Pawn, ctx, WakeScratch);
        }

        /// <summary>Filled by <see cref="WakesToEat"/>'s question and never started.</summary>
        static readonly Job WakeScratch = new Job();

        /// <summary>
        /// Arriving in a bed nobody owns makes it hers, where the colony can spare it
        /// (<c>ConstructionGrid.TryClaimForSleeper</c> holds the rule and the exception).
        ///
        /// <para>On arrival, once, rather than every tick of the sleep: the rule counts beds and
        /// colonists, and a colony of fifty asking it sixty times a second would be the only
        /// thing in this driver that costs anything. Not on the collapse branch either — a body
        /// that goes down on the way to a bed has not reached it, and does not get to own it.</para>
        /// </summary>
        void Claim(PawnContext ctx)
        {
            if (Job.TargetCell < 0 || Pawn.Cell != Job.TargetCell) return;
            ctx.Construction?.TryClaimForSleeper(Job.TargetCell, Pawn.Id);
        }

        public override void Cleanup(PawnContext ctx, JobStatus status) => Pawn.Asleep = false;
    }

    /// <summary>Walk somewhere nearby for no reason. What a broken or idle pawn does.</summary>
    public class WanderJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;

        public override JobStatus Tick(PawnContext ctx)
        {
            JobStatus walk = GotoCell(ctx, Job.TargetCell);
            return walk == JobStatus.Ongoing ? JobStatus.Ongoing : JobStatus.Succeeded;
        }
    }

    /// <summary>
    /// Stand still for a while. Also the stand-down the think-loop circuit breaker parks a pawn
    /// in, so a pawn that cannot find anything to do costs a counter rather than a rescan.
    /// </summary>
    public class WaitJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;

        public override JobStatus Tick(PawnContext ctx)
        {
            int duration = Job.WorkTicks > 0 ? Job.WorkTicks : ctx.Content.Jobs[Job.DefIndex].workTicks;
            // Milliwork, for the reason the eat toil above states: a stand-down has no rate, so
            // it pays at exactly the standard one, and the counter keeps one unit.
            ToilProgress += Rates.Scale;
            return ToilProgress >= duration * Rates.Scale ? JobStatus.Succeeded : JobStatus.Ongoing;
        }
    }
}

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Walk to a marked tree, work at it, and leave wood where it stood.
    ///
    /// A tree blocks nothing, but the colonist works from a neighbouring cell rather than from
    /// inside the trunk: <see cref="StandBeside"/> picks the stand, the job carries it as the
    /// target cell and the tree as the destination. The order is cleared the moment the last swing lands, so no other colonist sets off for
    /// it; the world edit itself (the tree going and the wood appearing) is a structural event
    /// and runs in the deferred phase of the same tick, like every collapse and removal.
    /// </summary>
    public class FellJobDriver : JobDriver
    {
        public override int WorkType => WorkTypeIndex.Cutting;

        /// <summary>
        /// The tree, once the walk is over and the swings have started. Presentation turns this
        /// into an axe in the hands and an arm that comes down on it; before the walk ends it is
        /// -1, so a colonist crossing the map does it empty-handed.
        ///
        /// The destination cell in preference to the target cell, so that this keeps naming the
        /// tree whichever of the two the job carries it in. A driver that walks *into* the trunk
        /// has the tree as its target and nothing as its destination; one that stands beside it
        /// has the stand as its target and the tree as its destination. Both are reasonable, the
        /// second is better, and the figure has to face the tree under either.
        /// </summary>
        /// <para>Nothing during the settle toil either, which is the point of it: the tree is
        /// already down and the figure should be easing out of its stance, not still swinging.</para>
        public override int WorkFocus =>
            ToilIndex != 1 ? -1 : Job.DestCell >= 0 ? Job.DestCell : Job.TargetCell;

        public override bool TryMakeReservations(PawnContext ctx)
        {
            if (Job.TargetCell < 0 || Job.DestCell < 0) return false;
            long key = ReservationManager.Key(ReservationTargetKind.Cell, Job.DestCell);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            // Before every guard below: by now the tree is down and its order cleared, so asking
            // whether it is still a marked tree would fail the job on the first settle tick.
            if (ToilIndex == SettleToil) return Settle(ctx);

            var designations = ctx.Designations;
            if (designations == null) return JobStatus.Failed;

            int cell = Job.DestCell;
            // Somebody else felled it, or the player changed their mind: stop, do not swing at air.
            if (designations.At(cell) != DesignationKind.Fell || !designations.IsFellable(cell))
                return JobStatus.Failed;
            // The species decides the work and the yield (design 45 §2), so a birch comes down
            // quicker than a giant and a bush yields nothing at all.
            WildPlantDef? plant = designations.WildPlantAt(cell);
            if (plant == null) return JobStatus.Failed;

            if (ToilIndex == 0)
            {
                JobStatus walk = GotoCell(ctx, Job.TargetCell);
                if (walk == JobStatus.Succeeded) NextToil();
                return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            // Still beside it, and on its own floor. A colonist that has been moved since it
            // arrived — dropped by a dig under its feet, or by a climb retired beneath it — is no
            // longer felling this tree, whatever its job says. Failing rather than walking back:
            // the stance it was given may not exist any more, and the work giver will choose a
            // fresh one next tick if there is one to choose.
            if (!StillInReach(ctx, Pawn, cell, layersAbove: 0, layersBelow: 0))
            {
                WalkBack();
                return JobStatus.Ongoing;
            }

            int rate = Pawn.WorkRatePerMille(WorkTypeIndex.Cutting);
            ToilProgress += rate;
            Work(ctx);
            if (ToilProgress < plant.clearWorkTicks * Rates.Scale)
                return JobStatus.Ongoing;

            designations.Clear(cell);
            int item = plant.clearYields.Length == 0 ? -1 : ctx.Content.ItemIndexOf(plant.clearYields);
            int yield = item < 0 ? 0 : plant.clearYieldCount;
            ctx.Defer(_ => FellTree(ctx, cell, item, yield));

            // The tree falls now; the woodcutter straightens up before walking off.
            NextToil();
            return JobStatus.Ongoing;
        }

        /// <summary>
        /// The nearest walkable cell beside the tree that the pawn can reach, or -1. Beside means
        /// one of the eight neighbours on the same layer, so the colonist stands at the trunk's
        /// side and the wood falls where the tree stood.
        /// </summary>
        public static int StandBeside(PawnContext ctx, Pawn pawn, int tree)
        {
            // The thing worked on must be hers to work on as well as the cell she stands in
            // (design 43 §4c): a tree just outside home is outside home, whichever side of the
            // line the stump is felled from.
            if (!ctx.MayWork(pawn, tree)) return -1;
            GridSize size = ctx.Size;
            CellRef at = size.FromIndex(tree);
            int best = -1, bestDistance = int.MaxValue;
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int x = at.X + dx, z = at.Z + dz;
                if (!size.Contains(x, z, at.Y)) continue;
                int cell = size.Index(x, z, at.Y);
                if (!ctx.Cells.IsWalkable(cell)) continue;
                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;
                if (!ctx.Reachable(pawn, cell)) continue;
                bestDistance = distance;
                best = cell;
            }
            return best;
        }

        static void FellTree(PawnContext ctx, int cell, int item, int yield)
        {
            // A bush prices its cell (design 45 §4), so taking it out changes what the cell costs
            // to cross and navigation has to re-read it; a tree blocks nothing and costs nothing.
            bool bush = ctx.Cells.IsUndergrowth(cell);
            ctx.Cells.RemoveEdifice(cell);
            ctx.Chunks?.MarkDirty(ctx.Size.FromIndex(cell));
            if (bush) ctx.Nav.MarkDirty(cell);
            if (item < 0 || yield <= 0) return;

            // Where the tree stood, or the nearest cell nearby that can take the wood — which
            // includes a pile of wood from the tree next door with room on it, so a stand of
            // trees comes down into a few stacks rather than a scatter of small ones. Nowhere
            // within three cells is a board packed solid with things, which nothing in the game
            // can produce yet; losing the wood then is the least bad answer, because spawning
            // onto a cell that cannot take it would corrupt the cell index.
            int at = ctx.Items.NearestCellWithSpace(ctx.Cells, cell, item, yield, maxRadius: 3);
            if (at >= 0) ctx.Items.Spawn(item, at, yield);
        }
    }
}
