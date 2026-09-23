#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The simulation half of the Work tab (design 27): the command that writes a priority, and
    /// the two aspects that publish one.
    ///
    /// <para>The priority model itself is older than this panel — <c>Pawn.WorkPriorities</c> is
    /// saved and hashed and the job scan has always run one priority band at a time. What is new
    /// is that anything outside the simulation can now read it or change it, and these are the
    /// tests for that seam.</para>
    /// </summary>
    public class WorkPriorityTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 12);

        static ColonyWorld Board(uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 2;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: true);
        }

        static IntentRejection Set(ColonyWorld colony, int pawn, int work, int priority)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(
                new Intent(IntentKind.SetWorkPriority, default, pawn, work, priority));
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        [Test]
        public void APawnIsBornOnPriorityThreeAndTheBareBoardLeavesItThere()
        {
            // Pawn's constructor fills every priority with 3, and Bare() sets miners = 0, so
            // AssignTrade returns early and nothing moves. This is the control for the test below.
            ColonyWorld colony = Board();

            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            for (int w = 0; w < WorkTypeIndex.Count; w++)
                Assert.That(pawn.WorkPriority(w), Is.EqualTo(3),
                    "Bare() has no miners, so the scenario leaves the constructor's threes alone.");
        }

        [Test]
        public void ThePlayedScenarioDealsADivisionOfLabourAndTheGridWillShowIt()
        {
            // The correction to a claim this design made and got wrong: a colony a player
            // actually starts does NOT open on a grid of threes. ColonyScenario.AssignTrade deals
            // the first `miners` colonists Mining 1 / Cutting 3 and everybody else the reverse,
            // "until the player can set priorities from the interface" — which is what this panel
            // is. The Work tab therefore opens on a visible division of labour, and that is the
            // thing it has to draw correctly on day one.
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 4;
            scenario.miners = 2;
            ColonyWorld colony = ColonyWorld.Build(Size, 1u, scenario, barren: true, wooded: true);

            var pawns = new List<Pawn>();
            foreach (Pawn pawn in colony.Pawns.Pawns.All) if (pawn.IsPerson) pawns.Add(pawn);
            Assert.That(pawns.Count, Is.EqualTo(4), "the four colonists; the world's own animals are not dealt a trade");

            for (int i = 0; i < pawns.Count; i++)
            {
                bool miner = i < scenario.miners;
                Assert.That(pawns[i].WorkPriority(WorkTypeIndex.Mining), Is.EqualTo(miner ? 1 : 3));
                Assert.That(pawns[i].WorkPriority(WorkTypeIndex.Cutting), Is.EqualTo(miner ? 3 : 1));

                Assert.That(pawns[i].WorkPriority(WorkTypeIndex.Haul), Is.EqualTo(3),
                    "AssignTrade touches two work types and no others.");
                Assert.That(pawns[i].WorkPriority(WorkTypeIndex.Construction), Is.EqualTo(3));
            }
        }

        [Test]
        public void TheCommandWritesOnePriorityOnOneColonist()
        {
            ColonyWorld colony = Board();
            Pawn subject = colony.Pawns.Pawns.All[0];
            Pawn other = colony.Pawns.Pawns.All[1];

            Assert.That(Set(colony, subject.Id.Value, WorkHandle.Mining, 1),
                Is.EqualTo(IntentRejection.None));

            Assert.That(subject.WorkPriority(WorkTypeIndex.Mining), Is.EqualTo(1));
            Assert.That(subject.WorkPriority(WorkTypeIndex.Haul), Is.EqualTo(3),
                "One work type, not the row.");
            Assert.That(other.WorkPriority(WorkTypeIndex.Mining), Is.EqualTo(3),
                "One colonist, not the column.");
        }

        [Test]
        public void ZeroIsNeverAndIsAcceptedAsAValue()
        {
            ColonyWorld colony = Board();
            Pawn subject = colony.Pawns.Pawns.All[0];

            Assert.That(Set(colony, subject.Id.Value, WorkHandle.Cutting, 0),
                Is.EqualTo(IntentRejection.None));
            Assert.That(subject.WorkPriority(WorkTypeIndex.Cutting), Is.Zero);
        }

        [Test]
        public void AnOutOfRangeArgumentIsRefusedRatherThanClamped()
        {
            // A priority of nine is a caller that has misunderstood the range, and quietly
            // storing four would put a number in the state hash that nobody asked for.
            ColonyWorld colony = Board();
            Pawn subject = colony.Pawns.Pawns.All[0];

            Assert.That(Set(colony, subject.Id.Value, WorkHandle.Mining, 9),
                Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(Set(colony, subject.Id.Value, WorkTypeIndex.Count, 2),
                Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(Set(colony, -1, WorkHandle.Mining, 2),
                Is.EqualTo(IntentRejection.NotPermitted));

            Assert.That(subject.WorkPriority(WorkTypeIndex.Mining), Is.EqualTo(3),
                "Nothing was written by any of the three refusals.");
        }

        [Test]
        public void SettingThePriorityItAlreadyHasSaysSo()
        {
            ColonyWorld colony = Board();
            Pawn subject = colony.Pawns.Pawns.All[0];

            Assert.That(Set(colony, subject.Id.Value, WorkHandle.Mining, 3),
                Is.EqualTo(IntentRejection.AlreadyInThatState));
        }

        [Test]
        public void ThePrioritiesAndTheirCapabilitiesArePublishedForEveryColonist()
        {
            ColonyWorld colony = Board();
            Pawn subject = colony.Pawns.Pawns.All[0];
            Set(colony, subject.Id.Value, WorkHandle.Construction, 2);

            WorldSnapshot frame = colony.World.Views.Current;

            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            for (int w = 0; w < WorkTypeIndex.Count; w++)
            {
                if (!pawn.IsPerson) continue;   // the world's animals do no work (design 30)
                Assert.That(frame.TryGetPawnAspect(pawn.Id, WorkAspects.Priority[w], out int p),
                    Is.True, "Every colonist's every work type, not only the selected one.");
                Assert.That(p, Is.EqualTo(pawn.WorkPriority(w)));

                Assert.That(frame.TryGetPawnAspect(pawn.Id, WorkAspects.Capable[w], out int c),
                    Is.True);
                Assert.That(c, Is.EqualTo(1),
                    "Nothing can answer this with a no yet: there are no traits and no health.");
            }

            Assert.That(frame.TryGetPawnAspect(subject.Id,
                WorkAspects.Priority[WorkTypeIndex.Construction], out int written), Is.True);
            Assert.That(written, Is.EqualTo(2), "The publish reflects the command that ran.");
        }

        [Test]
        public void TheAspectNamesAreTheOnesTheInterfaceSpells()
        {
            // The interface cannot reference this assembly, so the only thing holding the two
            // halves together is this string. WorkCatalogue mints the same one.
            Assert.That(WorkAspects.Name("mining", "priority"),
                Is.EqualTo("odyssey.pawn.work.mining.priority"));
            Assert.That(WorkAspects.Priority[WorkTypeIndex.Mining],
                Is.EqualTo(AspectKey.Of("odyssey.pawn.work.mining.priority")));
            Assert.That(WorkAspects.Capable[WorkTypeIndex.Haul],
                Is.EqualTo(AspectKey.Of("odyssey.pawn.work.haul.capable")));
        }

        [Test]
        public void TheHandlesAndTheSimulationsIndicesAreOneOrder()
        {
            Assert.That(WorkTypeIndex.Count, Is.EqualTo(WorkHandle.Count));
            Assert.That(WorkTypeIndex.Haul, Is.EqualTo(WorkHandle.Haul));
            Assert.That(WorkTypeIndex.Cutting, Is.EqualTo(WorkHandle.Cutting));
            Assert.That(WorkTypeIndex.Mining, Is.EqualTo(WorkHandle.Mining));
            Assert.That(WorkTypeIndex.Construction, Is.EqualTo(WorkHandle.Construction));
            Assert.That(WorkTypeIndex.Names.Length, Is.EqualTo(WorkTypeIndex.Count));
        }
    }
}
