#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The activity line for a colonist with something in her arms. Design 24 §8.
    ///
    /// <para><b>This line carries the amount now.</b> The load is drawn as a constant armful
    /// whatever the stack, so nothing on the board says how much anybody is holding and this is
    /// the only thing that does. That makes it worth more than a label usually is, and worth
    /// testing as something other than a formatting detail.</para>
    /// </summary>
    public class CarryLabelTests
    {
        static readonly PawnId Ada = new PawnId(1);

        /// <summary>
        /// The spelling this assembly mints for itself must be the spelling the simulation
        /// publishes under.
        ///
        /// <para><c>Odyssey.Hud</c> cannot reference <c>Odyssey.Sim</c> — that is the point of the
        /// aspect seam and is why there is no shared constant to import. The two sides agree by
        /// string and by nothing else, so this test and its twin in <c>CarryTests</c> are the
        /// whole of the guarantee. <c>ColonistNames.RollSeedAspect</c> makes the same bargain and
        /// is held by the same pair.</para>
        /// </summary>
        [Test]
        public void TheAspectNamesMatchTheOnesTheSimulationPublishes()
        {
            Assert.That(JobLabels.CarryingAspect, Is.EqualTo("odyssey.pawn.carrying"));
            Assert.That(JobLabels.CarryStackAspect, Is.EqualTo("odyssey.pawn.carrying.stack"));
        }

        [Test]
        public void AnEmptyHandedColonistJustSaysWhatSheIsDoing()
        {
            // No load, no clause. A colonist chopping a tree should not read as carrying nothing.
            Assert.That(JobLabels.Carrying(JobIndexHaul, -1, 0),
                Is.EqualTo(JobLabels.Label(JobIndexHaul)));
        }

        [Test]
        public void TheLineNamesTheLoadAndHowMuchOfIt()
        {
            string line = JobLabels.Carrying(JobIndexHaul, ItemDefWood, 8);

            Assert.That(line, Does.StartWith(JobLabels.Label(JobIndexHaul)));
            Assert.That(line, Does.Contain(ItemLabels.Label(ItemDefWood)));
            Assert.That(line, Does.Contain("8"));
        }

        [Test]
        public void OneOfSomethingIsNotCountedAtYou()
        {
            // "Wood × 1" is a machine talking. One log is a log.
            string line = JobLabels.Carrying(JobIndexHaul, ItemDefWood, 1);

            Assert.That(line, Does.Contain(ItemLabels.Label(ItemDefWood)));
            Assert.That(line, Does.Not.Contain("×"));
        }

        /// <summary>
        /// Both names come from the registry, and that is the rule
        /// <c>RegistryTests.NoPlayerFacingNameIsWrittenInCSharp</c> exists to keep: a name written
        /// in C# is a name the wiki cannot correct, and the two copies disagree the first time
        /// anybody edits one. Punctuation is not a name.
        /// </summary>
        [Test]
        public void EveryWordOnTheLineComesFromTheRegistry()
        {
            string line = JobLabels.Carrying(JobIndexHaul, ItemDefWood, 8);
            string stripped = line
                .Replace(JobLabels.Label(JobIndexHaul), string.Empty)
                .Replace(ItemLabels.Label(ItemDefWood), string.Empty);

            foreach (char c in stripped)
                Assert.That(char.IsLetter(c), Is.False,
                    $"'{c}' in \"{line}\" is a word this file wrote rather than one the wiki owns");
        }

        /// <summary>
        /// The pane refreshes fifteen times a second and the line is a composed string, so a
        /// colonist on a thirty-second haul would otherwise allocate one per refresh for the whole
        /// walk — ADR 0003's flip condition F1, and the same argument the position line already
        /// carries. Identity, not equality: a fresh instance every time also defeats the view's
        /// own "has this changed" guard.
        /// </summary>
        [Test]
        public void TheLineIsNotRebuiltWhileNothingAboutItChanges()
        {
            var model = new InspectModel();
            model.SetColonist(Ada);

            WorldSnapshot frame = Frame(carrying: ItemDefWood, stack: 8);
            model.Refresh(frame);
            string first = model.Job;

            model.Refresh(frame);
            Assert.That(ReferenceEquals(model.Job, first), Is.True,
                "the activity line was rebuilt although nothing about it moved");
        }

        [Test]
        public void TheLineFollowsTheLoadOntoAndOffTheColonist()
        {
            var model = new InspectModel();
            model.SetColonist(Ada);

            model.Refresh(Frame(carrying: ItemDefWood, stack: 8));
            Assert.That(model.Job, Does.Contain(ItemLabels.Label(ItemDefWood)));

            model.Refresh(Frame(carrying: -1, stack: 0));
            Assert.That(model.Job, Does.Not.Contain(ItemLabels.Label(ItemDefWood)),
                "she put it down and the pane still says she is holding it");

            model.Refresh(Frame(carrying: ItemDefStone, stack: 40));
            Assert.That(model.Job, Does.Contain(ItemLabels.Label(ItemDefStone)));
            Assert.That(model.Job, Does.Contain("40"));
        }

        static WorldSnapshot Frame(int carrying, int stack)
        {
            var frame = new WorldSnapshot();
            frame.AddPawn(new PawnView(Ada, new CellRef(4, 0, 4), 800, 800, 700, JobIndexHaul));

            if (carrying >= 0)
            {
                frame.AddPawnAspect(new PawnAspect(
                    Ada, AspectKey.Of(JobLabels.CarryingAspect), carrying));
                frame.AddPawnAspect(new PawnAspect(
                    Ada, AspectKey.Of(JobLabels.CarryStackAspect), stack));
            }

            return frame;
        }

        // Parallel to the tables the simulation owns, named here because this assembly cannot see
        // them — the same bargain ItemLabels and JobLabels themselves make.
        const int JobIndexHaul = 0;
        const int ItemDefWood = 2;
        const int ItemDefStone = 3;
    }
}
