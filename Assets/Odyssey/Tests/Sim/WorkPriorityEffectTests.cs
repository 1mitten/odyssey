#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// <b>Does the Work tab actually do anything?</b>
    ///
    /// <para>Owner, 2026-09-20, before merging: <i>"is it fully functional? as I've only tested the
    /// menu and not whether it's hooked up"</i>. Every other test on this panel proves a link in the
    /// chain — the panel emits an intent, the intent writes a byte, the byte is saved and hashed.
    /// <b>None of them proved the byte changes what a colonist does</b>, and a grid of numbers
    /// nobody obeys is exactly the failure the schedule half is honest about being.</para>
    ///
    /// <para>So these are behavioural: set a priority through the same intent the panel sends, run
    /// the colony, and watch the work happen or not happen.</para>
    ///
    /// <para><b>The seam is closed by a third test, not by these.</b> The panel sends a
    /// <c>WorkHandle</c> and the simulation reads a <c>WorkTypeIndex</c>;
    /// <c>WorkPriorityTests.TheHandlesAndTheSimulationsIndicesAreOneOrder</c> holds those two to
    /// one order, which is why it is safe for this file to write the index directly.</para>
    /// </summary>
    public class WorkPriorityEffectTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 8);

        /// <summary>One colonist on a wooded board, so there is no second pair of hands to do the
        /// work the first has been told not to.</summary>
        static ColonyWorld Wooded(uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: true);
        }

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

        /// <summary>The intent the Work tab's cell click sends, applied and checked.</summary>
        static void SetPriority(ColonyWorld colony, Pawn pawn, int work, int priority)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(
                new Intent(IntentKind.SetWorkPriority, default, pawn.Id.Value, work, priority));
            colony.World.Tick();
            Assert.That(colony.World.Intents.Rejected, Is.Empty,
                "the intent the panel sends was refused");
            Assert.That(pawn.WorkPriority(work), Is.EqualTo(priority));
        }

        static void MarkForFelling(ColonyWorld colony, int tree)
        {
            colony.World.Intents.Submit(new Intent(
                IntentKind.Designate, Size.FromIndex(tree), (int)DesignationKind.Fell));
            colony.World.Tick();
            Assert.That(colony.Designations.At(tree), Is.EqualTo(DesignationKind.Fell));
        }

        static bool RunUntilFelled(ColonyWorld colony, int tree, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                colony.World.Tick();
                if (colony.Grid.Edifice[tree] < 0) return true;
            }
            return false;
        }

        /// <summary>
        /// <b>Never means never, and the same colony does the work once it is allowed to.</b>
        ///
        /// <para>The A and the B are the same board, the same seed and the same marked tree; the
        /// only difference is the number the panel wrote. Without the B half this would pass just
        /// as well on a colonist who could not reach the tree.</para>
        ///
        /// <para><b>The priority is set before the order is given</b>, and that ordering is the
        /// point rather than an accident of the fixture — see
        /// <see cref="ChangingAPriorityDoesNotAbandonTheJobAlreadyInHand"/>, which is what the
        /// first draft of this test discovered by failing.</para>
        /// </summary>
        [Test]
        public void SettingAWorkTypeToNeverStopsTheWorkAndSettingItBackStartsIt()
        {
            ColonyWorld colony = Wooded();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            int tree = NearestTree(colony);
            Assume.That(tree, Is.GreaterThanOrEqualTo(0), "the wooded board has a tree to fell");

            // A: told never, and only then given something to cut. The scan runs 1 to 4 and zero
            // matches none of them, so no cutting giver is ever offered to her.
            SetPriority(colony, colonist, WorkTypeIndex.Cutting, 0);
            MarkForFelling(colony, tree);

            Assert.That(RunUntilFelled(colony, tree, 8_000), Is.False,
                "the colonist felled a tree she was told never to cut, so the priority the Work " +
                "tab writes is not reaching the job scan");
            Assert.That(colony.Designations.At(tree), Is.EqualTo(DesignationKind.Fell),
                "and the order is still standing, waiting for somebody allowed to take it");

            // B: told to do it after all. Same colony, same tree, one number changed.
            SetPriority(colony, colonist, WorkTypeIndex.Cutting, 3);
            Assert.That(RunUntilFelled(colony, tree, 8_000), Is.True,
                "the tree was never felled even at priority 3, so A proved nothing");
        }

        /// <summary>
        /// <b>A priority decides the next job, not the one in hand.</b>
        ///
        /// <para>Found by writing the test above the wrong way round: it marked a tree, let the
        /// colonist take the job, then set cutting to never — and she went on chopping, because
        /// <c>WorkThinkNode</c> runs when a colonist needs something to do and not while she is
        /// doing it. That is the right behaviour and the same as the reference's; it is asserted
        /// here so it reads as a decision rather than as the gap the first draft took it for.</para>
        ///
        /// <para><b>It is also what a player will notice first</b> — set a column to never and the
        /// colonist finishes her stroke rather than dropping the axe — so it is on the playtest
        /// queue as an expected answer rather than a bug.</para>
        /// </summary>
        [Test]
        public void ChangingAPriorityDoesNotAbandonTheJobAlreadyInHand()
        {
            ColonyWorld colony = Wooded();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            int tree = NearestTree(colony);
            Assume.That(tree, Is.GreaterThanOrEqualTo(0));

            MarkForFelling(colony, tree);
            Assume.That(colonist.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Fell),
                "the fixture depends on her having taken the job already");

            SetPriority(colony, colonist, WorkTypeIndex.Cutting, 0);

            Assert.That(colonist.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Fell),
                "she put the axe down mid-stroke. That is a change of behaviour, not a bug, but " +
                "it is a change: a priority used to decide the next job and not the one in hand.");
            Assert.That(RunUntilFelled(colony, tree, 8_000), Is.True,
                "and she finishes the tree she had already started");
        }

        /// <summary>
        /// A high number is last in the queue, not a refusal: a colonist with one order at
        /// priority 4 and nothing else to do still takes it.
        /// </summary>
        [Test]
        public void PriorityFourIsStillTakenWhenThereIsNothingAboveIt()
        {
            ColonyWorld colony = Wooded();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            int tree = NearestTree(colony);
            Assume.That(tree, Is.GreaterThanOrEqualTo(0));

            SetPriority(colony, colonist, WorkTypeIndex.Cutting, 4);
            MarkForFelling(colony, tree);

            Assert.That(RunUntilFelled(colony, tree, 8_000), Is.True,
                "cutting at priority 4 was never taken, so the scan is treating a high number as " +
                "a refusal rather than as last in the queue");
        }

        /// <summary>
        /// The panel writes while the colony is paused, which is the state a player sets
        /// priorities in.
        /// </summary>
        [Test]
        public void ThePriorityLandsWhileTheColonyIsPaused()
        {
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.SetWorkPriority), Is.True);
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.SetScheduleBlock), Is.True);
        }

        /// <summary>
        /// <b>The schedule is still read by nothing, and this test is the alarm on that.</b>
        ///
        /// <para>Design 27 §12d says the day is authored, saved, published and obeyed by no system,
        /// which is why it is outside the state hash. That is a claim about the simulation, and it
        /// is asserted here rather than only written down: a colonist whose every hour says Sleep
        /// goes on working, because nothing consults the array.</para>
        ///
        /// <para><b>When this test fails, the schedule has started working</b> — and the unit that
        /// made it work owes the state hash the array and the goldens a re-bake. See
        /// <c>ScheduleTests.EditingTheDayDoesNotMoveTheStateHash</c>, which fails at the same
        /// moment and for the same reason.</para>
        /// </summary>
        [Test]
        public void TheScheduleStillGovernsNothing()
        {
            ColonyWorld colony = Wooded();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            int tree = NearestTree(colony);
            Assume.That(tree, Is.GreaterThanOrEqualTo(0));

            // Every hour of her day is Sleep, set before she is given anything to do.
            for (int h = 0; h < ScheduleHandle.Hours; h++)
                colonist.ScheduleHours[h] = (byte)ScheduleHandle.Sleep;

            MarkForFelling(colony, tree);

            Assert.That(RunUntilFelled(colony, tree, 8_000), Is.True,
                "a colonist scheduled to sleep around the clock stopped working, so something " +
                "has started reading the schedule. That is good news and three things now owe " +
                "an update: this test, ScheduleTests.EditingTheDayDoesNotMoveTheStateHash, and " +
                "the goldens, which move once, deliberately, with ODYSSEY_REGOLDEN=1.");
        }
    }
}
