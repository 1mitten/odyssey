#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A marked tree becomes wood on the ground, and the wood becomes stock. The whole line the
    /// colony's first work runs along, on the wooded board the scene loads.
    /// </summary>
    public class FellJobTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 8);

        static ColonyWorld Wooded(uint seed = 1u, int fellRadius = 0)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            scenario.startingFellRadius = fellRadius;
            // A colony no longer starts with a store (owner, 2026-09-20), and half this file's
            // subject is where the wood goes — so the fixture asks for the nine cells the
            // scenario used to ship, and every test here measures what it always measured.
            scenario.stockpileCells = 9;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: true);
        }

        /// <summary>The nearest tree to the start on the start layer, as a cell index, or -1.</summary>
        static int NearestTree(ColonyWorld colony)
        {
            CellRef start = colony.Start;
            int best = -1, bestDistance = int.MaxValue;
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
            {
                int index = Size.Index(x, z, start.Y);
                if (!colony.Designations.IsTree(index)) continue;
                int distance = System.Math.Abs(x - start.X) + System.Math.Abs(z - start.Z);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = index;
            }
            return best;
        }

        static int WoodOnTheGround(ColonyWorld colony)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == ItemIndex.Wood) total += items[i].Stack;
            return total;
        }

        [Test]
        public void AMarkedTreeIsFelledAndLeavesWood()
        {
            ColonyWorld colony = Wooded();
            int tree = NearestTree(colony);
            Assume.That(tree, Is.GreaterThanOrEqualTo(0), "the wooded board has a tree to fell");
            CellRef cell = Size.FromIndex(tree);

            colony.World.Intents.Submit(new Intent(IntentKind.Designate, cell, (int)DesignationKind.Fell));
            colony.World.Tick();
            Assert.That(colony.World.Intents.Rejected, Is.Empty);
            Assert.That(colony.Designations.At(tree), Is.EqualTo(DesignationKind.Fell));

            int felledAt = -1;
            for (int tick = 0; tick < 6_000 && felledAt < 0; tick++)
            {
                colony.World.Tick();
                if (colony.Grid.Edifice[tree] < 0) felledAt = colony.World.CurrentTick;
            }

            Assert.That(felledAt, Is.GreaterThan(0), "the tree was never felled");
            Assert.That(colony.Designations.At(tree), Is.EqualTo(DesignationKind.None), "the order is cleared once carried out");
            Assert.That(WoodOnTheGround(colony), Is.EqualTo(colony.Pawns.Pawns.All[0].Content.WoodPerTree), "one tree, one stack of wood");
            Assert.That(colony.Grid.IsWalkable(tree), Is.True, "the cell is open ground afterwards");
        }

        [Test]
        public void TheWoodIsHauledToTheStockpile()
        {
            ColonyWorld colony = Wooded(fellRadius: 6);
            Assume.That(colony.Designations.Count, Is.GreaterThan(0), "trees stand within six cells of the start");
            int marked = colony.Designations.Count;

            colony.World.Tick(20_000);

            Assert.That(colony.Designations.Count, Is.LessThan(marked), "some orders were carried out");
            Assert.That(WoodOnTheGround(colony), Is.GreaterThan(0));

            int stocked = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Despawned || item.DefIndex != ItemIndex.Wood || item.Cell < 0) continue;
                if (colony.Pawns.Items.IsStockpileCell(item.Cell)) stocked++;
            }
            Assert.That(stocked, Is.GreaterThan(0), "felled wood ends up in the stockpile");
        }

        [Test]
        public void FellingIsDeterministic()
        {
            ColonyWorld first = Wooded(seed: 3u, fellRadius: 8);
            ColonyWorld second = Wooded(seed: 3u, fellRadius: 8);
            first.World.Tick(12_000);
            second.World.Tick(12_000);
            Assert.That(first.World.ComputeStateHash().Value, Is.EqualTo(second.World.ComputeStateHash().Value));
            Assert.That(WoodOnTheGround(first), Is.GreaterThan(0));
        }

        [Test]
        public void TheWoodcutterStandsBesideTheTreeNotInIt()
        {
            // A colonist is drawn at the cell centre and so is a tree, so working from inside the
            // cell put the figure in the trunk. The job walks to a neighbouring cell and works the
            // tree from there.
            ColonyWorld colony = Wooded();
            int tree = NearestTree(colony);
            Assume.That(tree, Is.GreaterThanOrEqualTo(0));
            CellRef at = Size.FromIndex(tree);

            colony.World.Intents.Submit(new Intent(IntentKind.Designate, at, (int)DesignationKind.Fell));
            Pawn? cutter = null;
            for (int tick = 0; tick < 3_000 && cutter == null; tick++)
            {
                colony.World.Tick();
                foreach (Pawn pawn in colony.Pawns.Pawns.All)
                    if (pawn.CurrentJob != null && pawn.CurrentJob.DefIndex == JobIndex.Fell) cutter = pawn;
            }

            Assert.That(cutter, Is.Not.Null, "somebody took the order");
            Job job = cutter!.CurrentJob!;
            Assert.That(job.DestCell, Is.EqualTo(tree), "the tree is the destination");
            Assert.That(job.TargetCell, Is.Not.EqualTo(tree), "the stand is not the tree");
            CellRef stand = Size.FromIndex(job.TargetCell);
            Assert.That(System.Math.Abs(stand.X - at.X), Is.LessThanOrEqualTo(1));
            Assert.That(System.Math.Abs(stand.Z - at.Z), Is.LessThanOrEqualTo(1));
            Assert.That(stand.Y, Is.EqualTo(at.Y));
        }

        [Test]
        public void ACancelledOrderStopsTheJob()
        {
            ColonyWorld colony = Wooded();
            int tree = NearestTree(colony);
            Assume.That(tree, Is.GreaterThanOrEqualTo(0));
            CellRef cell = Size.FromIndex(tree);

            colony.World.Intents.Submit(new Intent(IntentKind.Designate, cell, (int)DesignationKind.Fell));
            colony.World.Tick(200);
            colony.World.Intents.Submit(new Intent(IntentKind.CancelDesignation, cell));
            colony.World.Tick(3_000);

            Assert.That(colony.Grid.Edifice[tree], Is.GreaterThanOrEqualTo(0), "the tree still stands");
            Assert.That(WoodOnTheGround(colony), Is.Zero);
            Assert.That(colony.Pawns.Reservations.ActiveClaims, Is.Zero, "no claim outlives the cancelled job");
        }

        /// <summary>
        /// The signal the axe animation hangs on: a colonist is *working* only once it has
        /// arrived and the swings have started, and the snapshot says which cell it is swinging
        /// at. Without the second half presentation cannot turn the figure to face the tree,
        /// because a pawn that has stopped walking has no heading left to read.
        /// </summary>
        [Test]
        public void AColonistPublishesWhatItIsWorkingOnOnlyOnceItGetsThere()
        {
            ColonyWorld colony = Wooded();
            int tree = NearestTree(colony);
            Assume.That(tree, Is.GreaterThanOrEqualTo(0));
            CellRef cell = Size.FromIndex(tree);

            colony.World.Intents.Submit(new Intent(IntentKind.Designate, cell, (int)DesignationKind.Fell));
            colony.World.Tick();

            // Somebody has taken the job and is on their way, but nobody is swinging yet.
            colony.World.Tick(5);
            Assert.That(AnyoneWorking(colony), Is.False, "a colonist walking to a tree is not working at it");

            PawnView worker = default;
            for (int tick = 0; tick < 6_000 && !worker.Working; tick++)
            {
                colony.World.Tick();
                foreach (PawnView pawn in colony.World.Views.Current.Pawns)
                    if (pawn.Working) { worker = pawn; break; }
            }

            Assert.That(worker.Working, Is.True, "nobody ever started swinging");
            Assert.That(worker.WorkCell, Is.EqualTo(cell), "the work cell is the tree, which is what the figure turns to face");
            Assert.That(worker.MovePercent, Is.Zero, "a pawn at work is standing still");

            // And it stops: the tree comes down and the swing has nothing left to land on.
            colony.World.Tick(6_000);
            Assert.That(colony.Grid.Edifice[tree], Is.LessThan(0), "the tree came down");
            Assert.That(AnyoneWorking(colony), Is.False, "the axe is put away when the work ends");
        }

        static bool AnyoneWorking(ColonyWorld colony)
        {
            foreach (PawnView pawn in colony.World.Views.Current.Pawns)
                if (pawn.Working) return true;
            return false;
        }

        /// <summary>
        /// The tree comes down, and the woodcutter stands a beat before walking off (owner,
        /// 2026-09-16: "should there be a second delay so you can motion more naturally instead of
        /// snapping?").
        ///
        /// <para><b>What it is really protecting.</b> The drawn figure steps in towards its work
        /// and eases back out over <c>PawnFigureDirector.WorkEaseSeconds</c> — 0.45 s, 27 ticks.
        /// Without the settle, every single work-to-move transition began gliding within 1 to 3
        /// ticks of the work stopping (27 of 27, measured over 40,000 ticks), so the figure was
        /// walking and un-stepping at once. The gait blend leaves the stance out of the speed it
        /// measures, so the feet played an ordinary walk while the body covered both, and it read
        /// as a colonist skating away from the stump and then settling to a normal pace.</para>
        ///
        /// <para>So the assertion is not "there is a pause" but "the pause outlasts the ease", and
        /// <see cref="JobDef.settleTicks"/> has to keep doing so. Measured afterwards: every gap
        /// 31 to 33 ticks, none under 27.</para>
        /// </summary>
        [Test, Category("Long")]
        public void AFelledTreeIsFollowedThroughRatherThanSnappedOutOf()
        {
            // With work to do: the default fixture marks nothing, and a colony with nothing to
            // fell finishes no work and so has no transition to measure.
            ColonyWorld colony = Wooded(fellRadius: 14);
            // Asserted, not assumed. An Assume here would let somebody set settleTicks to zero
            // and leave this test reporting "skipped" in a green run for ever — which is the trap
            // docs/lessons.md records under "An Assume can hide a dead feature".
            //
            // 27 ticks is PawnFigureDirector.WorkEaseSeconds (0.45 s) at sixty ticks a second.
            // The settle has to outlast the ease or it does not do its job.
            const int EaseTicks = 27;
            int settle = colony.Pawns.Content.Jobs[JobIndex.Fell].settleTicks;
            Assert.That(settle, Is.GreaterThanOrEqualTo(EaseTicks),
                "felling's settle is shorter than the work pose takes to ease out, so a colonist " +
                "still walks away mid-retraction");

            var stoppedAt = new Dictionary<int, int>();
            var wasWorking = new Dictionary<int, bool>();
            var gaps = new List<int>();

            for (int tick = 0; tick < 40_000; tick++)
            {
                colony.World.Tick();
                foreach (Pawn pawn in colony.Pawns.Pawns.All)
                {
                    int id = pawn.Id.Value;
                    bool working = pawn.Driver != null && pawn.Driver.WorkFocus >= 0;
                    wasWorking.TryGetValue(id, out bool before);

                    if (before && !working) stoppedAt[id] = tick;
                    else if (!working && stoppedAt.ContainsKey(id) && pawn.HasPath && pawn.MoveProgress > 0)
                    {
                        gaps.Add(tick - stoppedAt[id]);
                        stoppedAt.Remove(id);
                    }

                    wasWorking[id] = working;
                }
            }

            Assume.That(gaps, Is.Not.Empty, "nobody finished a piece of work and then walked away");

            foreach (int gap in gaps)
                Assert.That(gap, Is.GreaterThanOrEqualTo(settle),
                    $"a colonist began walking {gap} ticks after its work stopped, inside the " +
                    $"{settle}-tick settle — it is stepping out of the work stance while walking, " +
                    "which reads as skating away from the stump");
        }

        [Test]
        public void TheDefaultWorkPrioritiesLetAColonistCut()
        {
            ColonyWorld colony = Wooded();
            var pawn = colony.Pawns.Pawns.All[0];
            Assert.That(pawn.WorkPriorities.Length, Is.EqualTo(WorkTypeIndex.Count));
            Assert.That(pawn.WorkPriority(WorkTypeIndex.Cutting), Is.InRange(1, 4));
        }
    }
}
