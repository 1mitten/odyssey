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
        public void AColonyStartsKnowingWiringAndNothingElse()
        {
            var research = new ResearchDirector();
            Assert.That(research.StatusOf(P(ResearchCatalogue.WiringKey)), Is.EqualTo(ResearchStatus.Done));
            Assert.That(research.StatusOf(P(ResearchCatalogue.GeneratorsKey)), Is.EqualTo(ResearchStatus.Available));
            Assert.That(research.StatusOf(P(ResearchCatalogue.LightingKey)), Is.EqualTo(ResearchStatus.Available));
            Assert.That(research.StatusOf(P(ResearchCatalogue.BatteriesKey)), Is.EqualTo(ResearchStatus.Locked));
            Assert.That(research.StatusOf(P(ResearchCatalogue.SolarKey)), Is.EqualTo(ResearchStatus.Locked));
            Assert.That(research.Current, Is.Null);
        }

        [Test]
        public void TheTableSortsByStatusThenCheapestFirst()
        {
            var research = new ResearchDirector();
            research.Start(ResearchCatalogue.GeneratorsKey);

            string[] order = ResearchModel.Sorted(ResearchCatalogue.PowerKey, research).Select(p => p.Key).ToArray();

            Assert.That(order, Is.EqualTo(new[]
            {
                ResearchCatalogue.GeneratorsKey,   // researching
                ResearchCatalogue.LightingKey,     // available
                ResearchCatalogue.WiringKey,       // done
                ResearchCatalogue.BatteriesKey,    // locked, 700
                ResearchCatalogue.SolarKey,        // locked, 1200
            }));
        }

        [Test]
        public void StartingAnotherProjectPutsTheFirstBackWithItsProgress()
        {
            var research = new ResearchDirector();
            research.Start(ResearchCatalogue.GeneratorsKey);
            research.Advance(250);
            Assert.That(research.PercentOf(P(ResearchCatalogue.GeneratorsKey)), Is.EqualTo(50));

            Assert.That(research.Start(ResearchCatalogue.LightingKey), Is.True);
            Assert.That(research.StatusOf(P(ResearchCatalogue.GeneratorsKey)), Is.EqualTo(ResearchStatus.Available));
            Assert.That(research.ProgressOf(ResearchCatalogue.GeneratorsKey), Is.EqualTo(250), "a switch loses nothing");
        }

        [Test]
        public void OnlyAnAvailableProjectStarts()
        {
            var research = new ResearchDirector();
            Assert.That(research.Start(ResearchCatalogue.BatteriesKey), Is.False, "locked");
            Assert.That(research.Start(ResearchCatalogue.WiringKey), Is.False, "done");
            Assert.That(research.Enqueue(ResearchCatalogue.SolarKey), Is.False, "locked");
        }

        [Test]
        public void QueueingWithNothingInHandStartsIt()
        {
            var research = new ResearchDirector();
            Assert.That(research.Enqueue(ResearchCatalogue.LightingKey), Is.True);
            Assert.That(research.Current, Is.EqualTo(ResearchCatalogue.LightingKey));
            Assert.That(research.Queue, Is.Empty);
        }

        [Test]
        public void FinishingTheProjectInHandStartsTheQueueAndUnlocksWhatNeededIt()
        {
            var research = new ResearchDirector();
            research.Start(ResearchCatalogue.GeneratorsKey);
            research.Enqueue(ResearchCatalogue.LightingKey);
            Assert.That(ResearchModel.ThenLine(research), Is.EqualTo("then Electric light"));

            Assert.That(research.FinishCurrent(), Is.True);

            Assert.That(research.IsDone(ResearchCatalogue.GeneratorsKey), Is.True);
            Assert.That(research.Current, Is.EqualTo(ResearchCatalogue.LightingKey), "the queue's head starts");
            Assert.That(research.StatusOf(P(ResearchCatalogue.BatteriesKey)), Is.EqualTo(ResearchStatus.Available),
                "Batteries needed Generators and nothing else");
            Assert.That(ResearchModel.ThenLine(research), Is.Empty);
        }

        [Test]
        public void PausingKeepsTheQueueWaiting()
        {
            var research = new ResearchDirector();
            research.Start(ResearchCatalogue.GeneratorsKey);
            research.Enqueue(ResearchCatalogue.LightingKey);
            research.Pause();
            Assert.That(research.Current, Is.Null);
            Assert.That(research.Queue, Is.EqualTo(new[] { ResearchCatalogue.LightingKey }));
            Assert.That(ResearchModel.ThenLine(research), Is.Empty, "nothing is in hand, so nothing comes next");
        }

        [Test]
        public void TheDetailPaneSaysWhatALockedProjectWaitsFor()
        {
            var research = new ResearchDirector();
            Assert.That(ResearchModel.LockedLine(P(ResearchCatalogue.BatteriesKey), research),
                Is.EqualTo("Needs Generators first."));
            Assert.That(ResearchModel.LockedLine(P(ResearchCatalogue.LightingKey), research), Is.Empty);
            Assert.That(ResearchModel.Meta(P(ResearchCatalogue.GeneratorsKey)), Is.EqualTo("Power, cost 500"));
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
            Assert.That(ResearchModel.SecondaryOf(P(ResearchCatalogue.LightingKey), research), Is.EqualTo(ResearchSecondary.Queue));
            Assert.That(ResearchModel.SecondaryOf(P(ResearchCatalogue.WiringKey), research), Is.EqualTo(ResearchSecondary.None));
            Assert.That(ResearchModel.SecondaryOf(P(ResearchCatalogue.SolarKey), research), Is.EqualTo(ResearchSecondary.None));
            research.Start(ResearchCatalogue.GeneratorsKey);
            research.Enqueue(ResearchCatalogue.LightingKey);
            Assert.That(ResearchModel.SecondaryOf(P(ResearchCatalogue.GeneratorsKey), research), Is.EqualTo(ResearchSecondary.Pause));
            Assert.That(ResearchModel.SecondaryOf(P(ResearchCatalogue.LightingKey), research), Is.EqualTo(ResearchSecondary.Unqueue));
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
            Assert.That(model.Categories[0].Done, Is.EqualTo(1));
            Assert.That(model.Categories[0].Total, Is.EqualTo(5));
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
