#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The Research tab's content and its placeholder research state (design 34), in the fast
    /// tier. The shell only paints what these answer.
    /// </summary>
    public class ResearchTabTests
    {
        static ResearchProject P(string key) => ResearchCatalogue.Find(key)!;

        [Test]
        public void TheListIsOnlyWhatIsInTheGame()
        {
            // Owner, 2026-09-23: Electricity, Power lines, Generator, Ladder, and nothing else.
            Assert.That(ResearchCatalogue.Projects.Select(p => Registry.Label(p.Key)),
                Is.EqualTo(new[] { "Electricity", "Power lines", "Generator", "Ladder" }));
            Assert.That(ResearchCatalogue.Categories.Select(Registry.Label), Is.EqualTo(new[] { "Power", "Furniture" }));
            Assert.That(P(ResearchCatalogue.PowerLinesKey).Needs, Is.EqualTo(new[] { ResearchCatalogue.ElectricityKey }));
            Assert.That(P(ResearchCatalogue.GeneratorKey).Needs, Is.EqualTo(new[] { ResearchCatalogue.ElectricityKey }));
            Assert.That(P(ResearchCatalogue.LadderKey).CategoryKey, Is.EqualTo(ResearchCatalogue.FurnitureKey));
        }

        [Test]
        public void AColonyStartsKnowingNothing()
        {
            var research = new ResearchDirector();
            Assert.That(research.StatusOf(P(ResearchCatalogue.ElectricityKey)), Is.EqualTo(ResearchStatus.Available));
            Assert.That(research.StatusOf(P(ResearchCatalogue.LadderKey)), Is.EqualTo(ResearchStatus.Available));
            Assert.That(research.StatusOf(P(ResearchCatalogue.PowerLinesKey)), Is.EqualTo(ResearchStatus.Locked));
            Assert.That(research.StatusOf(P(ResearchCatalogue.GeneratorKey)), Is.EqualTo(ResearchStatus.Locked));
            Assert.That(research.Current, Is.Null);
        }

        [Test]
        public void TheTableSortsByStatusThenCheapestFirst()
        {
            var research = new ResearchDirector();
            research.Start(ResearchCatalogue.ElectricityKey);
            research.FinishCurrent();
            research.Start(ResearchCatalogue.GeneratorKey);

            string[] order = ResearchModel.Sorted(ResearchCatalogue.PowerKey, research).Select(p => p.Key).ToArray();

            Assert.That(order, Is.EqualTo(new[]
            {
                ResearchCatalogue.GeneratorKey,     // researching
                ResearchCatalogue.PowerLinesKey,    // available
                ResearchCatalogue.ElectricityKey,   // done
            }));
        }

        [Test]
        public void StartingAnotherProjectPutsTheFirstBackWithItsProgress()
        {
            var research = new ResearchDirector();
            research.Start(ResearchCatalogue.ElectricityKey);
            research.Advance(150);
            Assert.That(research.PercentOf(P(ResearchCatalogue.ElectricityKey)), Is.EqualTo(50));

            Assert.That(research.Start(ResearchCatalogue.LadderKey), Is.True);
            Assert.That(research.StatusOf(P(ResearchCatalogue.ElectricityKey)), Is.EqualTo(ResearchStatus.Available));
            Assert.That(research.ProgressOf(ResearchCatalogue.ElectricityKey), Is.EqualTo(150), "a switch loses nothing");
        }

        [Test]
        public void OnlyAnAvailableProjectStarts()
        {
            var research = new ResearchDirector();
            Assert.That(research.Start(ResearchCatalogue.GeneratorKey), Is.False, "locked");
            Assert.That(research.Enqueue(ResearchCatalogue.PowerLinesKey), Is.False, "locked");
            research.Start(ResearchCatalogue.LadderKey);
            research.FinishCurrent();
            Assert.That(research.Start(ResearchCatalogue.LadderKey), Is.False, "done");
        }

        [Test]
        public void QueueingWithNothingInHandStartsIt()
        {
            var research = new ResearchDirector();
            Assert.That(research.Enqueue(ResearchCatalogue.LadderKey), Is.True);
            Assert.That(research.Current, Is.EqualTo(ResearchCatalogue.LadderKey));
            Assert.That(research.Queue, Is.Empty);
        }

        [Test]
        public void FinishingTheProjectInHandStartsTheQueueAndUnlocksWhatNeededIt()
        {
            var research = new ResearchDirector();
            research.Start(ResearchCatalogue.ElectricityKey);
            research.Enqueue(ResearchCatalogue.LadderKey);
            Assert.That(ResearchModel.ThenLine(research), Is.EqualTo("then Ladder"));

            Assert.That(research.FinishCurrent(), Is.True);

            Assert.That(research.IsDone(ResearchCatalogue.ElectricityKey), Is.True);
            Assert.That(research.Current, Is.EqualTo(ResearchCatalogue.LadderKey), "the queue's head starts");
            Assert.That(research.StatusOf(P(ResearchCatalogue.PowerLinesKey)), Is.EqualTo(ResearchStatus.Available));
            Assert.That(research.StatusOf(P(ResearchCatalogue.GeneratorKey)), Is.EqualTo(ResearchStatus.Available));
            Assert.That(ResearchModel.ThenLine(research), Is.Empty);
        }

        [Test]
        public void PausingKeepsTheQueueWaiting()
        {
            var research = new ResearchDirector();
            research.Start(ResearchCatalogue.ElectricityKey);
            research.Enqueue(ResearchCatalogue.LadderKey);
            research.Pause();
            Assert.That(research.Current, Is.Null);
            Assert.That(research.Queue, Is.EqualTo(new[] { ResearchCatalogue.LadderKey }));
            Assert.That(ResearchModel.ThenLine(research), Is.Empty, "nothing is in hand, so nothing comes next");
        }

        [Test]
        public void TheDetailPaneSaysWhatALockedProjectWaitsFor()
        {
            var research = new ResearchDirector();
            Assert.That(ResearchModel.LockedLine(P(ResearchCatalogue.GeneratorKey), research),
                Is.EqualTo("Needs Electricity first."));
            Assert.That(ResearchModel.LockedLine(P(ResearchCatalogue.LadderKey), research), Is.Empty);
            Assert.That(ResearchModel.Meta(P(ResearchCatalogue.GeneratorKey)), Is.EqualTo("Power, cost 500"));
            Assert.That(ResearchModel.Meta(P(ResearchCatalogue.LadderKey)), Is.EqualTo("Furniture, cost 200"));
            Assert.That(ResearchCatalogue.LeadingFrom(ResearchCatalogue.ElectricityKey).Select(p => p.Key),
                Is.EqualTo(new[] { ResearchCatalogue.PowerLinesKey, ResearchCatalogue.GeneratorKey }));
        }

        [Test]
        public void ThePrimaryButtonFollowsTheStatusAndOnlyAvailableIsPressable()
        {
            Assert.That(ResearchModel.PrimaryLabel(ResearchStatus.Available), Is.EqualTo("Research"));
            Assert.That(ResearchModel.PrimaryLabel(ResearchStatus.Researching), Is.EqualTo("Researching"));
            Assert.That(ResearchModel.PrimaryLabel(ResearchStatus.Done), Is.EqualTo("Done"));
            Assert.That(ResearchModel.PrimaryLabel(ResearchStatus.Locked), Is.EqualTo("Locked"));
            Assert.That(ResearchModel.PrimaryPressable(ResearchStatus.Available), Is.True);
            Assert.That(ResearchModel.PrimaryPressable(ResearchStatus.Done), Is.False);
            Assert.That(ResearchModel.PrimaryPressable(ResearchStatus.Locked), Is.False);

            var research = new ResearchDirector();
            Assert.That(ResearchModel.SecondaryOf(P(ResearchCatalogue.LadderKey), research), Is.EqualTo(ResearchSecondary.Queue));
            Assert.That(ResearchModel.SecondaryOf(P(ResearchCatalogue.GeneratorKey), research), Is.EqualTo(ResearchSecondary.None));
            research.Start(ResearchCatalogue.ElectricityKey);
            research.Enqueue(ResearchCatalogue.LadderKey);
            Assert.That(ResearchModel.SecondaryOf(P(ResearchCatalogue.ElectricityKey), research), Is.EqualTo(ResearchSecondary.Pause));
            Assert.That(ResearchModel.SecondaryOf(P(ResearchCatalogue.LadderKey), research), Is.EqualTo(ResearchSecondary.Unqueue));
        }

        [Test]
        public void TheStatusTagIsTheStatusWordInLowerCase()
        {
            Assert.That(ResearchModel.Tag(ResearchStatus.Done), Is.EqualTo("done"));
            Assert.That(ResearchModel.Tag(ResearchStatus.Researching), Is.EqualTo("researching"));
        }

        [Test]
        public void ExactlyOneCategoryAndOneProjectAreSelected()
        {
            var research = new ResearchDirector();
            var model = new ResearchModel();
            model.Refresh(research);

            Assert.That(model.Categories.Count(c => c.Selected), Is.EqualTo(1));
            Assert.That(model.Rows.Count(r => r.Selected), Is.EqualTo(1));
            Assert.That(model.Project, Is.EqualTo(model.Rows[0].Project.Key), "the first row, when nothing was chosen");
            Assert.That(model.Categories[0].Done, Is.EqualTo(0));
            Assert.That(model.Categories[0].Total, Is.EqualTo(3));
            Assert.That(model.Categories[1].Total, Is.EqualTo(1));

            model.SelectCategory(ResearchCatalogue.FurnitureKey);
            model.Refresh(research);
            Assert.That(model.Project, Is.EqualTo(ResearchCatalogue.LadderKey), "a new category selects its first row");
        }

        [Test]
        public void TheBodyHoldsThirteenRowsAndAPagedCategoryGivesOneToItsFoot()
        {
            Assert.That(ResearchLayout.RowsPerPage, Is.EqualTo(13));
            Assert.That(ResearchModel.PageSize(13), Is.EqualTo(13));
            Assert.That(ResearchModel.PageSize(14), Is.EqualTo(12));
            Assert.That(ResearchLayout.Height, Is.EqualTo(34 + 30 + 420 + 2));
            Assert.That(ResearchLayout.DetailWidth, Is.EqualTo(438));
        }

        [Test]
        public void EveryWordTheTabDrawsIsRegisteredAndAscii()
        {
            foreach (string key in ResearchDirector.IconKeys.Concat(ResearchCatalogue.IconKeys()))
            {
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
                Assert.That(Registry.Label(key).All(c => c < 128), Is.True, $"{key} is not ASCII");
            }
            foreach (ResearchProject project in ResearchCatalogue.Projects)
            {
                string text = Registry.Describe(project.Key);
                Assert.That(text, Is.Not.Empty, $"{project.Key} has no description");
                Assert.That(text.All(c => c < 128), Is.True, $"{project.Key}'s description is not ASCII");
                foreach (string need in project.Needs)
                    Assert.That(ResearchCatalogue.Find(need), Is.Not.Null, $"{project.Key} needs an unknown project");
            }
        }
    }
}
