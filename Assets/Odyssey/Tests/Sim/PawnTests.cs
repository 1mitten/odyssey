#nullable enable
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Storage;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A world with a floor under every cell and nothing in the way, so "walkable" means "not
    /// walled in" and the only vertical movement is through a connector that was declared.
    ///
    /// Everything a test needs is built through the same composition root the game uses, so a
    /// test cannot accidentally exercise a world the game could not produce.
    /// </summary>
    sealed class Colony
    {
        public readonly SimWorld World;
        public readonly PawnContext Ctx;
        public readonly CellGrid Cells;
        public readonly NavGraph Nav;
        public readonly NeedsSystem Needs;
        public readonly JobSystem Jobs;
        public readonly MovementSystem Movement;
        public readonly StorageZones Storage;

        Colony(int sx, int sz, int sy, uint seed)
        {
            var size = new GridSize(sx, sz, sy);
            Cells = new CellGrid(size);
            for (int i = 0; i < size.CellCount; i++) Cells.Floor[i] = 1;

            Nav = new NavGraph(Cells);
            var finder = new PathFinder(Nav);
            var paths = new PathService(finder);
            Ctx = new PawnContext(Cells, Nav, paths, ContentPack.Pawns());

            // The storage zones, wired exactly as `ColonyComposition` wires them — the zones hold
            // the membership and the items ask it back — so a test cannot exercise a colony the
            // game could not produce, which is what the note at the top of this class is for.
            Storage = new StorageZones(Cells, new StorageSettingsTable(Ctx.Content), Ctx.Items);
            Ctx.Storage = Storage;
            Ctx.Items.Membership = Storage;

            Needs = new NeedsSystem(Ctx);
            Jobs = new JobSystem(Ctx);
            Movement = new MovementSystem(Ctx);

            World = new SimWorldBuilder()
                .WithSeed(seed)
                .WithSize(size)
                .AddTickable(_ => Ctx.Pawns)
                .AddSnapshotContributor(Ctx.Pawns)
                .AddSystem(_ => Needs)
                .AddSystem(_ => Jobs)
                .AddSystem(_ => Movement)
                .AddTickable(_ => new SkillSystem(Ctx))
                // Hashed the way the game hashes them — the zones through the tickable, what they
                // accept through the table beside it — so that a fixture's save proves what the
                // game's save has to prove rather than something weaker.
                .AddTickable(_ => Storage)
                .AddHashable(Storage.Settings)
                .Build();

            RebuildNav();
        }

        public static Colony Build(int sx = 16, int sz = 16, int sy = 2, uint seed = 20260915u) =>
            new Colony(sx, sz, sy, seed);

        public GridSize Size => Cells.Size;

        public int Cell(int x, int z, int y) => Size.Index(x, z, y);

        public int LayerOf(int cell) => Size.FromIndex(cell).Y;

        public void RebuildNav()
        {
            Nav.MarkAllDirty();
            Nav.Rebuild();
        }

        /// <summary>A stair is one cell at each end, and it declares both of them.</summary>
        public void AddStair(int x, int z, int lowerLayer)
        {
            Nav.AddConnector(ConnectorKind.Stair,
                new[] { Cell(x, z, lowerLayer) },
                new[] { Cell(x, z, lowerLayer + 1) });
            RebuildNav();
        }

        /// <summary>
        /// A storage zone over these cells, at this priority, accepting everything — a real one,
        /// painted through the intent path with the first cell as its anchor, so a test's zone
        /// obeys the same anchor rule a player's drag does.
        /// </summary>
        public StorageSettings Stockpile(int priority, params int[] cells)
        {
            // The ladder is five rungs now and the destination scan walks them by name, so a test
            // asking for priority 9 is asking for a rung that does not exist — which used to work
            // silently, because the priority was a bare integer nothing bounded. It is worth
            // failing loudly here rather than in a haul that mysteriously never happens.
            Assert.That(priority, Is.InRange(0, StoragePriority.Count - 1),
                "a storage priority is one of the five rungs, 0 (Last) to 4 (Urgent)");

            int anchor = cells[0];
            for (int i = 0; i < cells.Length; i++)
                Storage.Designate(Size.FromIndex(cells[i]), anchor, StoragePreset.Everything);

            StorageSettings settings = Storage.SettingsAt(cells[0])!;
            settings.Priority = priority;
            return settings;
        }

        /// <summary>
        /// Everything this fixture's world holds that is in its state hash. The job system is
        /// here because its per-def counters are hashed: a hashed field that is not saved is a
        /// save that resumes wrongly, and this list is where the two are kept in step.
        /// </summary>
        public IReadOnlyList<ISaveable> SaveComponents =>
            new ISaveable[] { Ctx.Pawns, Ctx.Items, Jobs, Storage.Settings, Storage };
    }

    public class PawnNeedsTests
    {
        [Test]
        public void NeedsFallOnTheOneHundredAndFiftyTickInterval()
        {
            // The cadence is the frame everything else hangs on, so it is asserted exactly
            // rather than approximately: four intervals, four applications of the top-band rate.
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(8, 8, 0));
            pawn.Needs[NeedIndex.Food] = 800;
            pawn.Needs[NeedIndex.Rest] = 800;

            int interval = colony.Needs.IntervalTicks;
            Assert.That(interval, Is.EqualTo(150));

            int fall = colony.Ctx.Content.Needs[NeedIndex.Food].FallPerInterval(800);
            colony.World.Tick(interval * 4);

            Assert.That(pawn.Needs[NeedIndex.Food], Is.EqualTo(800 - fall * 4));
        }

        [Test]
        public void NeedsDoNotMoveBetweenIntervals()
        {
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(8, 8, 0));
            pawn.Needs[NeedIndex.Food] = 800;

            // Pawn id 1 updates on the tick before each interval boundary, so 148 ticks is a
            // whole window with no update in it.
            colony.World.Tick(148);
            Assert.That(pawn.Needs[NeedIndex.Food], Is.EqualTo(800));
        }

        [Test]
        public void FallRateIsPerBandNotFlat()
        {
            var food = ContentPack.Pawns().Needs[NeedIndex.Food];
            Assert.That(food.FallPerInterval(900), Is.GreaterThan(food.FallPerInterval(200)));
            Assert.That(food.MoodOffset(900), Is.GreaterThan(food.MoodOffset(100)));
        }

        [Test]
        public void MoodDriftsTowardItsTargetRatherThanSnapping()
        {
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(8, 8, 0));
            pawn.Needs[NeedIndex.Food] = 60;   // a large negative situational thought
            pawn.Needs[NeedIndex.Joy] = 500;
            pawn.Mood = 900;

            colony.World.Tick(colony.Needs.IntervalTicks);

            int drop = 900 - pawn.Mood;
            Assert.That(pawn.MoodTarget, Is.LessThan(500), "the target should have moved a long way");
            Assert.That(drop, Is.EqualTo(colony.Ctx.Content.Mood.fallPerInterval),
                "mood must approach the target at the capped rate, not jump to it");
        }

        [Test]
        public void AMiserablePawnBreaksEventuallyRatherThanImmediately()
        {
            // A mean-time-between-events roll, not a cliff edge. At the shipped mean of ten
            // in-game days a miserable colonist is nowhere near certain to break in the first
            // few minutes, which is the point — so the first half of the test uses the real
            // number and the second half scales it down to something a test can watch.
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(8, 8, 0));

            int threshold = colony.Ctx.Content.Mood.breakThreshold;

            // Mood drifts rather than snapping, so a colonist whose world has just fallen apart
            // takes a good while to get all the way down to the break band.
            for (int i = 0; i < 40_000 && pawn.Mood >= threshold; i++) { Miserable(pawn); colony.World.Tick(); }
            Assert.That(pawn.Mood, Is.LessThan(threshold));
            Assert.That(colony.World.CurrentTick, Is.GreaterThan(colony.Needs.IntervalTicks * 10),
                "mood must take time to fall, or one bad moment would break a colonist outright");
            Assert.That(pawn.IsBroken, Is.False, "a break is a roll, not a trigger");

            for (int i = 0; i < colony.Needs.IntervalTicks * 2; i++) { Miserable(pawn); colony.World.Tick(); }
            Assert.That(pawn.IsBroken, Is.False, "and still a roll a few intervals later");

            // Retuned by replacing the Def on this colony's own content, never by writing through
            // it: the MoodDef is shared by every test in the process. All three clocks, because
            // a miserable colonist is under the major line and it is the deepest that rolls
            // (design 44 §5c).
            MoodDef shipped = colony.Ctx.Content.Mood;
            colony.Ctx.Content.Mood = new MoodDef
            {
                baseMood = shipped.baseMood, max = shipped.max,
                risePerInterval = shipped.risePerInterval, fallPerInterval = shipped.fallPerInterval,
                breakThreshold = shipped.breakThreshold, strainMargin = shipped.strainMargin,
                breakMtbTicks = 15_000, majorMtbTicks = 15_000, extremeMtbTicks = 15_000,
            };
            for (int i = 0; i < 120_000 && colony.Needs.BreaksTriggered == 0; i++)
            {
                Miserable(pawn);
                colony.World.Tick();
            }

            Assert.That(colony.Needs.BreaksTriggered, Is.GreaterThan(0));
            Assert.That(pawn.IsBroken, Is.True);
        }

        /// <summary>Hold a pawn's needs where its mood cannot climb back out of the break band.</summary>
        static void Miserable(Pawn pawn)
        {
            pawn.Needs[NeedIndex.Food] = 60;
            pawn.Needs[NeedIndex.Joy] = 0;
            pawn.Needs[NeedIndex.Rest] = 800;
        }

        [Test]
        public void MemoriesStackWithADiminishingMultiplierAndExpire()
        {
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(8, 8, 0));
            var def = colony.Ctx.Content.Thoughts[ThoughtIndex.AteMeal];

            pawn.AddMemory(ThoughtIndex.AteMeal, 0);
            int one = pawn.MemoryMoodOffset(0);
            pawn.AddMemory(ThoughtIndex.AteMeal, 0);
            int two = pawn.MemoryMoodOffset(0);

            Assert.That(one, Is.EqualTo(def.moodOffset));
            Assert.That(two, Is.LessThan(one * 2), "a second copy must be worth less than the first");

            pawn.AddMemory(ThoughtIndex.AteMeal, 0);
            Assert.That(pawn.Memories.Count, Is.EqualTo(def.stackLimit), "the stack limit is the point");

            Assert.That(pawn.MemoryMoodOffset(def.durationTicks + 1), Is.EqualTo(0));
        }
    }

    public class PawnJobTests
    {
        [Test]
        public void AHungryPawnEatsAndTheNeedRecovers()
        {
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            pawn.Needs[NeedIndex.Food] = 100;

            ThingId meal = colony.Ctx.Items.Spawn(ItemIndex.Meal, colony.Cell(6, 2, 0));

            for (int i = 0; i < 3_000 && colony.Ctx.Items.Get(meal) != null; i++) colony.World.Tick();

            Assert.That(colony.Ctx.Items.Get(meal), Is.Null, "the meal should have been eaten");
            Assert.That(pawn.Needs[NeedIndex.Food], Is.GreaterThan(400));
        }

        [Test]
        public void EatingTakesOneMealFromThePileNotThePile()
        {
            // Despawning the whole pile ate four meals a sitting; the soak found the pantry empty
            // by the end of day one and blamed the scope. One meal leaves the pile, and the pile
            // goes only with its last meal.
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            pawn.Needs[NeedIndex.Food] = 100;

            ThingId pile = colony.Ctx.Items.Spawn(ItemIndex.Meal, colony.Cell(6, 2, 0), stack: 4);

            for (int i = 0; i < 3_000 && pawn.Needs[NeedIndex.Food] < 400; i++) colony.World.Tick();

            var left = colony.Ctx.Items.Get(pile);
            Assert.That(left, Is.Not.Null, "three meals are still there");
            Assert.That(left!.Stack, Is.EqualTo(3));
        }

        [Test]
        public void ATiredPawnGoesToBedAndRestRecovers()
        {
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            pawn.Needs[NeedIndex.Rest] = 100;
            int bed = colony.Cell(7, 3, 0);
            colony.Ctx.Items.AddBed(bed);

            for (int i = 0; i < 2_000 && !pawn.Asleep; i++) colony.World.Tick();

            Assert.That(pawn.Asleep, Is.True);
            Assert.That(pawn.Cell, Is.EqualTo(bed), "a pawn with a reachable bed sleeps in it");

            int before = pawn.Needs[NeedIndex.Rest];
            colony.World.Tick(3_000);
            Assert.That(pawn.Needs[NeedIndex.Rest], Is.GreaterThan(before));
        }

        [Test]
        public void APawnWithNoBedSleepsOnTheGroundAndRemembersIt()
        {
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            pawn.Needs[NeedIndex.Rest] = 40;

            for (int i = 0; i < 1_000 && !pawn.Asleep; i++) colony.World.Tick();
            Assert.That(pawn.Asleep, Is.True);
            Assert.That(pawn.HeldReservations, Is.Empty, "the ground is not reservable");
        }

        [Test]
        public void APawnHaulsALooseThingIntoAStockpile()
        {
            var colony = Colony.Build();
            colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            int store = colony.Cell(12, 12, 0);
            colony.Stockpile(1, store);

            ThingId scrap = colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(6, 4, 0));

            for (int i = 0; i < 3_000 && colony.Ctx.Items.Get(scrap)!.Cell != store; i++) colony.World.Tick();

            Assert.That(colony.Ctx.Items.Get(scrap)!.Cell, Is.EqualTo(store));
            Assert.That(colony.Ctx.Items.LooseItems, Is.Empty, "a stored thing is no longer haulable");
        }

        /// <summary>
        /// Picking a thing up takes time, and the thing changes hands in the middle of it (owner,
        /// 2026-09-17: "there should be time spent motion down, picking up object and standing up").
        ///
        /// <para>Three claims, and each of them is a fault that has to be observable from the
        /// simulation side or it is only observable by looking. <b>The thing is still on the
        /// ground</b> while the colonist bends, or the pile vanishes from under a standing figure.
        /// <b>It is in the colonist's arms before the toil ends</b>, or the rise is drawn empty.
        /// And <b>the whole motion costs</b> <see cref="PawnContent.LiftTicks"/>, which is what
        /// stops the pawn setting off walking while its figure is still straightening — the thing
        /// that was wrong before this existed, when a pickup was one tick.</para>
        /// </summary>
        [Test]
        public void PickingSomethingUpTakesTimeAndTheGraspIsInTheMiddleOfIt()
        {
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            colony.Stockpile(1, colony.Cell(12, 12, 0));

            ThingId scrap = colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(6, 4, 0));
            var item = colony.Ctx.Items.Get(scrap)!;

            // Walk until the colonist is standing on the thing with the lift toil running. The
            // haul's toil 1 IS the lift, so reaching it is what "arrived" means here.
            for (int i = 0; i < 3_000 && !InLiftToil(pawn); i++) colony.World.Tick();
            Assert.That(InLiftToil(pawn), Is.True, "the hauler never reached the thing");

            int total = colony.Ctx.Content.LiftTicks;
            int grasp = colony.Ctx.Content.LiftGraspTicks;

            // The toil is entered by the walk toil's own last tick, so none of the lift is spent
            // yet: after N more ticks the motion is N ticks old.
            colony.World.Tick(grasp - 1);
            Assert.That(item.Cell, Is.GreaterThanOrEqualTo(0),
                "the thing left the ground before the hands reached it");

            colony.World.Tick(1);
            Assert.That(item.Cell, Is.EqualTo(-1), "the thing was not taken up at the grasp");
            Assert.That(pawn.CurrentJob!.CarriedItem, Is.EqualTo(scrap.Value));

            // Still rising, and still on the same toil: the colonist has not set off walking.
            colony.World.Tick(total - grasp - 1);
            Assert.That(InLiftToil(pawn), Is.True, "the lift ended at the grasp instead of at the rise");

            colony.World.Tick(1);
            Assert.That(InLiftToil(pawn), Is.False, "the lift outlasted its own duration");
        }

        static bool InLiftToil(Pawn pawn) =>
            pawn.Driver is HaulJobDriver driver && driver.ToilIndex == 1;

        [Test]
        public void HaulersPreferTheHigherPriorityStockpile()
        {
            // Priority orders the destination, not the haul queue. The far, better zone wins
            // over the near, worse one.
            var colony = Colony.Build(24, 24);
            colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            int near = colony.Cell(8, 2, 0);
            int far = colony.Cell(20, 20, 0);
            colony.Stockpile(StoragePriority.Low, near);
            colony.Stockpile(StoragePriority.Urgent, far);

            ThingId scrap = colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(4, 2, 0));
            for (int i = 0; i < 6_000 && colony.Ctx.Items.Get(scrap)!.Cell != far; i++) colony.World.Tick();

            Assert.That(colony.Ctx.Items.Get(scrap)!.Cell, Is.EqualTo(far));
        }

        [Test]
        public void APawnPathsToATargetOnAnotherLayerThroughAStair()
        {
            var colony = Colony.Build(16, 16, 2);
            colony.AddStair(8, 8, 0);

            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            pawn.Needs[NeedIndex.Food] = 150;

            ThingId meal = colony.Ctx.Items.Spawn(ItemIndex.Meal, colony.Cell(13, 13, 1));
            Assert.That(colony.Nav.Reachable(pawn.Cell, colony.Cell(13, 13, 1), TraverseMode.Colonist),
                Is.True, "the declared stair is the only thing that can make this reachable");

            int highestLayer = 0;
            for (int i = 0; i < 6_000 && colony.Ctx.Items.Get(meal) != null; i++)
            {
                colony.World.Tick();
                int layer = colony.LayerOf(pawn.Cell);
                if (layer > highestLayer) highestLayer = layer;
            }

            Assert.That(highestLayer, Is.EqualTo(1), "the pawn must actually have climbed");
            Assert.That(colony.Ctx.Items.Get(meal), Is.Null);
        }

        [Test]
        public void AnUnreachableTargetIsNeverPathedFor()
        {
            // The whole architecture: reachability is answered before pathing, never by pathing.
            // With no stair, the layer above is a different district and costs two array reads
            // to rule out.
            var colony = Colony.Build(12, 12, 2);
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            pawn.Needs[NeedIndex.Food] = 100;
            colony.Ctx.Items.Spawn(ItemIndex.Meal, colony.Cell(9, 9, 1));

            colony.World.Tick(600);

            Assert.That(colony.Movement.PathsFailed, Is.EqualTo(0),
                "an unreachable target must never reach the path service at all");
            Assert.That(colony.LayerOf(pawn.Cell), Is.EqualTo(0));
        }

        [Test]
        public void TheThinkTreeIsAnOrderedScanWithSelfCareAboveWork()
        {
            var colony = Colony.Build();
            var order = new List<string>();
            foreach (var node in colony.Jobs.Tree) order.Add(node.Name);
            // Drafted sits above the needs branch, or a drafted colonist wanders off to eat
            // (design 33 §2b). Downed is first of all and SelfDefence sits between the draft and
            // the needs (design 33 §5): both decline for anybody standing and unhurt.
            Assert.That(order, Is.EqualTo(new[]
            {
                "Downed", "MentalState", "Drafted", "SelfDefence", "CriticalNeeds", "Work", "Idle",
            }));
        }

        [Test]
        public void ADisabledWorkTypeIsNeverScanned()
        {
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            pawn.WorkPriorities[WorkTypeIndex.Haul] = 0;
            colony.Stockpile(1, colony.Cell(12, 12, 0));
            colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(6, 4, 0));

            colony.World.Tick(500);

            Assert.That(colony.Ctx.Items.LooseItems, Is.Not.Empty);
            Assert.That(colony.Ctx.Reservations.ActiveClaims, Is.EqualTo(0));
        }
    }

    public class PawnReservationTests
    {
        [Test]
        public void TwoPawnsCannotClaimTheSameTarget()
        {
            var colony = Colony.Build();
            var first = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            var second = colony.Ctx.Pawns.Spawn(colony.Cell(3, 2, 0));
            colony.Stockpile(1, colony.Cell(12, 12, 0));
            ThingId scrap = colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(6, 4, 0));

            colony.World.Tick(5);

            long key = ReservationManager.Key(ReservationTargetKind.Item, scrap.Value);
            int haulers = 0;
            if (IsHauling(colony, first)) haulers++;
            if (IsHauling(colony, second)) haulers++;

            Assert.That(haulers, Is.EqualTo(1), "exactly one pawn may own the crate");
            Assert.That(colony.Ctx.Reservations.IsReservedByAnyone(key), Is.True);
            Assert.That(colony.Ctx.Reservations.CanReserve(
                    IsHauling(colony, first) ? second.Id : first.Id, key),
                Is.False);
        }

        [Test]
        public void AClaimIsAllOrNothing()
        {
            // The destination is already taken, so the item claim that was made first must be
            // handed back: a half-claimed job is a leak waiting for a long run to find it.
            var colony = Colony.Build();
            var blocker = colony.Ctx.Pawns.Spawn(colony.Cell(11, 12, 0));
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            int store = colony.Cell(12, 12, 0);

            long cellKey = ReservationManager.Key(ReservationTargetKind.Cell, store);
            colony.Ctx.Reservations.Reserve(blocker.Id, cellKey);
            blocker.HeldReservations.Add(cellKey);

            colony.Stockpile(1, store);
            ThingId scrap = colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(6, 4, 0));

            colony.World.Tick(3);

            Assert.That(IsHauling(colony, pawn), Is.False);
            Assert.That(colony.Ctx.Reservations.IsReservedBy(
                pawn.Id, ReservationManager.Key(ReservationTargetKind.Item, scrap.Value)), Is.False);
            Assert.That(pawn.HeldReservations, Is.Empty);
        }

        [Test]
        public void ReservationsAreReleasedWhenAJobFails()
        {
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(2, 2, 0));
            colony.Stockpile(1, colony.Cell(12, 12, 0));
            ThingId scrap = colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(6, 4, 0));

            colony.World.Tick(3);
            Assert.That(pawn.HeldReservations.Count, Is.EqualTo(2), "the item and the destination");
            Assert.That(colony.Ctx.Reservations.ActiveClaims, Is.EqualTo(2));

            // Pull the target out from under the job: the classic fail condition.
            colony.Ctx.Items.Despawn(colony.Ctx.Items.Get(scrap)!);
            colony.World.Tick(2);

            Assert.That(colony.Jobs.JobsFailed, Is.GreaterThan(0));
            Assert.That(pawn.HeldReservations, Is.Empty);
            Assert.That(colony.Ctx.Reservations.ActiveClaims, Is.EqualTo(0));
        }

        [Test]
        public void ReservationsDoNotLeakOverThousandsOfTicks()
        {
            // The fault a ten-day unattended run surfaces and a two-minute test does not. Six
            // colonists, a churn of hauling, eating and sleeping, twenty thousand ticks, and the
            // reservation table must still agree with what the pawns think they hold.
            var colony = Colony.Build(24, 24, 2);
            colony.AddStair(12, 12, 0);

            for (int i = 0; i < 6; i++) colony.Ctx.Pawns.Spawn(colony.Cell(2 + i, 2, 0));

            var storeCells = new List<int>();
            for (int x = 18; x < 22; x++)
                for (int z = 18; z < 22; z++)
                    storeCells.Add(colony.Cell(x, z, 0));
            colony.Stockpile(1, storeCells.ToArray());

            for (int i = 0; i < 4; i++) colony.Ctx.Items.AddBed(colony.Cell(4 + i, 20, 0));

            for (int i = 0; i < 14; i++)
                colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(5 + i % 10, 8 + i / 10, i % 2));
            for (int i = 0; i < 10; i++)
                colony.Ctx.Items.Spawn(ItemIndex.Meal, colony.Cell(3 + i, 14, 0));

            for (int tick = 0; tick < 20_000; tick++)
            {
                colony.World.Tick();

                if (tick % 500 != 0) continue;
                AssertNoLeak(colony);
            }

            AssertNoLeak(colony);

            // And when every job is ended by hand, the table must come back to nothing at all.
            foreach (var pawn in colony.Ctx.Pawns.All) colony.Jobs.EndJob(pawn, JobStatus.Failed);
            Assert.That(colony.Ctx.Reservations.ActiveClaims, Is.EqualTo(0));
            Assert.That(colony.Ctx.Reservations.ClaimedTargets, Is.EqualTo(0));
        }

        static void AssertNoLeak(Colony colony)
        {
            int held = 0;
            foreach (var pawn in colony.Ctx.Pawns.All)
            {
                held += pawn.HeldReservations.Count;
                if (pawn.CurrentJob == null)
                    Assert.That(pawn.HeldReservations, Is.Empty,
                        $"{pawn.Id} holds claims with no job at tick {colony.World.CurrentTick}");
            }

            Assert.That(colony.Ctx.Reservations.ActiveClaims, Is.EqualTo(held),
                $"the table and the pawns disagree at tick {colony.World.CurrentTick}");
        }

        internal static bool IsHauling(Colony colony, Pawn pawn) =>
            pawn.CurrentJob != null &&
            colony.Ctx.Content.Jobs[pawn.CurrentJob.DefIndex].driver == JobIndex.Haul;
    }

    public class PawnDeterminismTests
    {
        static Colony Populated(uint seed)
        {
            var colony = Colony.Build(24, 24, 2, seed);
            colony.AddStair(12, 12, 0);

            for (int i = 0; i < 8; i++) colony.Ctx.Pawns.Spawn(colony.Cell(2 + i, 2, 0));

            var store = new List<int>();
            for (int x = 18; x < 22; x++)
                for (int z = 18; z < 22; z++)
                    store.Add(colony.Cell(x, z, 0));
            colony.Stockpile(1, store.ToArray());

            for (int i = 0; i < 4; i++) colony.Ctx.Items.AddBed(colony.Cell(4 + i, 20, 0));
            for (int i = 0; i < 16; i++)
                colony.Ctx.Items.Spawn(ItemIndex.Salvage, colony.Cell(5 + i % 10, 8 + i / 10, i % 2));
            for (int i = 0; i < 12; i++)
                colony.Ctx.Items.Spawn(ItemIndex.Meal, colony.Cell(3 + i, 14, 0));

            return colony;
        }

        [Test]
        public void SameSeedSameWorldAfterTenThousandTicks()
        {
            var a = Populated(77u);
            var b = Populated(77u);

            a.World.Tick(10_000);
            b.World.Tick(10_000);

            for (int i = 0; i < a.Ctx.Pawns.Count; i++)
            {
                Assert.That(b.Ctx.Pawns.All[i].Cell, Is.EqualTo(a.Ctx.Pawns.All[i].Cell),
                    $"pawn {i} ended up somewhere else");
                Assert.That(b.Ctx.Pawns.All[i].Needs[NeedIndex.Food],
                    Is.EqualTo(a.Ctx.Pawns.All[i].Needs[NeedIndex.Food]));
            }

            Assert.That(b.World.ComputeStateHash().Value, Is.EqualTo(a.World.ComputeStateHash().Value));
        }

        [Test]
        public void TheColonyActuallyDoesSomethingOverTenThousandTicks()
        {
            // A determinism test passes trivially if nothing happens, so this says it did.
            var colony = Populated(77u);
            colony.World.Tick(10_000);

            Assert.That(colony.Jobs.JobsStarted, Is.GreaterThan(20));
            Assert.That(colony.Movement.StepsTaken, Is.GreaterThan(200));
            Assert.That(colony.Ctx.Items.LooseItems.Count, Is.LessThan(16));
        }

        [Test]
        public void ASaveTakenMidJobPreservesTheStateHash()
        {
            var original = Populated(303u);
            original.World.Tick(1_500);

            int withJobs = 0;
            foreach (var pawn in original.Ctx.Pawns.All) if (pawn.CurrentJob != null) withJobs++;
            Assert.That(withJobs, Is.GreaterThan(0), "the save has to be taken mid-job to mean anything");

            ulong expected = original.World.ComputeStateHash().Value;

            using var stream = new MemoryStream();
            WorldSave.Save(original.World, stream, original.SaveComponents);
            byte[] bytes = stream.ToArray();

            var restored = Colony.Build(24, 24, 2, 303u);
            restored.AddStair(12, 12, 0);
            using var input = new MemoryStream(bytes);
            WorldSave.Load(restored.World, input, restored.SaveComponents);
            // What `ColonyWorld.RebuildDerived` does for a real colony: the listers are derived
            // from the items and the zones together, and both have only just finished loading.
            restored.Storage.RebucketAll();

            Assert.That(restored.World.CurrentTick, Is.EqualTo(original.World.CurrentTick));
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(expected));

            // Toil progress is part of that hash, so a job halfway through its work survived.
            var restoredPawns = restored.Ctx.Pawns.All;
            for (int i = 0; i < restoredPawns.Count; i++)
            {
                var before = original.Ctx.Pawns.All[i];
                var after = restoredPawns[i];
                Assert.That(after.CurrentJob != null, Is.EqualTo(before.CurrentJob != null));
                if (before.CurrentJob == null) continue;
                Assert.That(after.CurrentJob!.DefIndex, Is.EqualTo(before.CurrentJob.DefIndex));
                Assert.That(after.Driver!.ToilIndex, Is.EqualTo(before.Driver!.ToilIndex));
                Assert.That(after.Driver.ToilProgress, Is.EqualTo(before.Driver.ToilProgress));
                Assert.That(after.HeldReservations, Is.EqualTo(before.HeldReservations));
            }

            Assert.That(restored.Ctx.Reservations.ActiveClaims,
                Is.EqualTo(original.Ctx.Reservations.ActiveClaims));

            // Resume equivalence: paths are re-derived rather than loaded, so this is also the
            // assertion that a recomputed path walks the same route the saved run was walking.
            original.World.Tick(3_000);
            restored.World.Tick(3_000);
            Assert.That(restored.World.ComputeStateHash().Value,
                Is.EqualTo(original.World.ComputeStateHash().Value),
                "an unbroken run and a resumed one must not diverge");
        }

        [Test]
        public void SavingTheSameStateTwiceProducesIdenticalBytes()
        {
            // The cheapest possible detector for unordered iteration, which is the most common
            // way determinism is lost.
            var colony = Populated(909u);
            colony.World.Tick(900);

            using var first = new MemoryStream();
            WorldSave.Save(colony.World, first, colony.SaveComponents);
            using var second = new MemoryStream();
            WorldSave.Save(colony.World, second, colony.SaveComponents);

            Assert.That(second.ToArray(), Is.EqualTo(first.ToArray()));
        }
    }

    public class PawnSnapshotTests
    {
        [Test]
        public void PawnsAppearInThePublishedSnapshot()
        {
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(5, 6, 0));
            colony.Ctx.Items.Spawn(ItemIndex.Meal, colony.Cell(9, 9, 0));

            colony.World.Tick();

            var snapshot = colony.World.Views.Current;
            Assert.That(snapshot.PawnCount, Is.EqualTo(1));
            Assert.That(snapshot.ThingCount, Is.EqualTo(1));

            Assert.That(snapshot.TryGetPawn(pawn.Id, out var view), Is.True);
            Assert.That(view.Cell, Is.EqualTo(new CellRef(5, 6, 0)));
            Assert.That(view.Food, Is.EqualTo(pawn.Needs[NeedIndex.Food]));
            Assert.That(view.Mood, Is.EqualTo(pawn.Mood));
        }

        [Test]
        public void SystemsRegisterIntoThePawnPhaseInTheCataloguedOrder()
        {
            var colony = Colony.Build();
            var names = new List<string>();
            foreach (var system in colony.World.Systems.PawnSystems) names.Add(system.Name);

            Assert.That(names, Is.EqualTo(new[] { "Needs", "Jobs", "Movement" }));
            Assert.That(colony.Needs.Order, Is.EqualTo(10));
            Assert.That(colony.Jobs.Order, Is.EqualTo(20));
            Assert.That(colony.Movement.Order, Is.EqualTo(30));
        }
    }
}
