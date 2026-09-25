#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// A colonist's pace on the pane (design 17 §5a), and an animal sheltering from the rain
    /// (design 43 §6a). Owner, 2026-09-25: <i>"I notice the move speed is not shown anywhere so I
    /// couldn't tell whether people were moving slower."</i>
    /// </summary>
    public class PaceModelTests
    {
        static readonly PawnId Ada = new PawnId(1);
        static readonly PawnId Hog = new PawnId(2);

        /// <summary>
        /// The spellings this assembly mints must be the ones the simulation publishes under; the
        /// twin is <c>PaceAspectNameTests</c> in the simulation's suite. The two sides agree by
        /// string and by nothing else.
        /// </summary>
        [Test]
        public void TheAspectNamesMatchTheOnesTheSimulationPublishes()
        {
            Assert.That(PaceModel.RolledAspect, Is.EqualTo("odyssey.pawn.rate.move.rolled"));
            Assert.That(PaceModel.ConditionAspect, Is.EqualTo("odyssey.pawn.rate.move.condition"));
            Assert.That(PaceModel.WeatherAspect, Is.EqualTo("odyssey.pawn.rate.move.weather"));
            Assert.That(PaceModel.UrgencyAspect, Is.EqualTo("odyssey.pawn.rate.move.urgency"));
            Assert.That(PawnKindLabels.ShelteringAspect, Is.EqualTo("odyssey.pawn.sheltering"));
        }

        static WorldSnapshot Colonist(int rolled, int condition = 1000, int weather = 1000,
            int urgency = 1000, bool drafted = false, bool published = true)
        {
            var frame = new WorldSnapshot();
            frame.AddPawn(new PawnView(Ada, new CellRef(4, 0, 4), 800, 800, 700, 0));
            if (!published) return frame;
            frame.AddPawnAspect(new PawnAspect(Ada, AspectKey.Of(PaceModel.RolledAspect), rolled));
            // Sparse, as the simulation publishes them: a factor at 1,000 is no row at all.
            if (condition != 1000) frame.AddPawnAspect(new PawnAspect(Ada, AspectKey.Of(PaceModel.ConditionAspect), condition));
            if (weather != 1000) frame.AddPawnAspect(new PawnAspect(Ada, AspectKey.Of(PaceModel.WeatherAspect), weather));
            if (urgency != 1000) frame.AddPawnAspect(new PawnAspect(Ada, AspectKey.Of(PaceModel.UrgencyAspect), urgency));
            if (drafted) frame.AddPawnAspect(new PawnAspect(Ada, AspectKey.Of(OrderModel.DraftedAspect), 1));
            return frame;
        }

        static string Pace => Registry.Label(PaceModel.PaceLabel);

        // ---------------------------------------------------------------------------- the line

        [Test]
        public void ADryColonistReadsHerPaceAndNothingElse()
        {
            PaceModel.Factors f = PaceModel.Of(Colonist(rolled: 1040), Ada);
            Assert.That(PaceModel.Line(f), Is.EqualTo(Pace + " 104%"));
        }

        [Test]
        public void InADownpourTheLineSaysSoBesideTheNumber()
        {
            // 1,042 × 900 is 937 in the rate's truncation: "94%", and the rain named beside it,
            // which is where the owner chose to put it.
            PaceModel.Factors f = PaceModel.Of(Colonist(rolled: 1042, weather: 900), Ada);
            Assert.That(PaceModel.PerMille(f), Is.EqualTo(937));
            Assert.That(PaceModel.Line(f), Is.EqualTo(
                Pace + " 94% · " + Registry.Label(PaceModel.InTheRain).ToLowerInvariant()));
        }

        [Test]
        public void TheHeadlineIsTheProductInTheRatesOrderWithItsTruncation()
        {
            // Every step truncates, as Pawn.MoveRatePerMille's does: 1,149 × 700 = 804.3 → 804,
            // × 900 = 723.6 → 723, × 2,000 = 1,446.
            PaceModel.Factors f = PaceModel.Of(Colonist(rolled: 1149, condition: 700, weather: 900, urgency: 2000, drafted: true), Ada);
            Assert.That(PaceModel.PerMille(f), Is.EqualTo(1446));
            Assert.That(PaceModel.Line(f), Does.StartWith(Pace + " 145%"));
        }

        [Test]
        public void ANeverPublishedPaceIsNoLine()
        {
            // A frame from before, a bandit, an animal: nothing to say, and the pane hides the row
            // rather than inventing a hundred per cent.
            var model = new InspectModel();
            model.SetColonist(Ada);
            model.Refresh(Colonist(rolled: 0, published: false));
            Assert.That(model.Pace, Is.Empty);
        }

        // ---------------------------------------------------------------------------- the tooltip

        [Test]
        public void TheTooltipNamesOnlyWhatIsNotTheStandardWalk()
        {
            string tip = PaceModel.Tooltip(PaceModel.Of(Colonist(rolled: 1040, weather: 900), Ada));
            Assert.That(tip, Is.EqualTo(Lower(PaceModel.RolledLabel) + " 104% · " + Lower(PaceModel.RainLabel) + " −10%"));
            Assert.That(tip, Does.Not.Contain(Lower(PaceModel.ConditionLabel)), "a well colonist's condition is not news");
        }

        [Test]
        public void AStarvingColonistShowsTheConditionFactor()
        {
            string tip = PaceModel.Tooltip(PaceModel.Of(Colonist(rolled: 1000, condition: 800), Ada));
            Assert.That(tip, Is.EqualTo(Lower(PaceModel.ConditionLabel) + " 80%"));
        }

        [Test]
        public void TheRunIsTheDraftsWhenSheIsDraftedAndAFightsOtherwise()
        {
            string drafted = PaceModel.Tooltip(PaceModel.Of(Colonist(rolled: 1000, urgency: 2000, drafted: true), Ada));
            string fighting = PaceModel.Tooltip(PaceModel.Of(Colonist(rolled: 1000, urgency: 2000), Ada));
            Assert.That(drafted, Is.EqualTo(Lower(PaceModel.DraftedLabel) + " ×2"));
            Assert.That(fighting, Is.EqualTo(Lower(PaceModel.RunningLabel) + " ×2"));
        }

        [Test]
        public void NothingToSayIsSaidInWords()
        {
            // A colonist rolled at exactly the standard walk, dry, fed and undrafted — one in 301 —
            // still gets a tooltip, rather than a hover that shows nothing.
            string tip = PaceModel.Tooltip(PaceModel.Of(Colonist(rolled: 1000), Ada));
            Assert.That(tip, Is.EqualTo(Registry.Label(PaceModel.StandardLabel)));
        }

        static string Lower(string key) => Registry.Label(key).ToLowerInvariant();

        // ---------------------------------------------------------------------------- the pane

        [Test]
        public void ThePaneCarriesTheLineAndItsTooltip()
        {
            var model = new InspectModel();
            model.SetColonist(Ada);
            model.Refresh(Colonist(rolled: 1000, weather: 900));
            Assert.That(model.Pace, Does.StartWith(Pace + " 90%"));
            Assert.That(model.PaceTip, Does.Contain(Lower(PaceModel.RainLabel)));

            // Under a roof the rain factor stops being published and the pane follows it.
            model.Refresh(Colonist(rolled: 1000));
            Assert.That(model.Pace, Is.EqualTo(Pace + " 100%"));
        }

        [Test]
        public void ARefreshThatChangesNothingBuildsNoNewString()
        {
            var model = new InspectModel();
            model.SetColonist(Ada);
            WorldSnapshot frame = Colonist(rolled: 1040, weather: 900);
            model.Refresh(frame);
            string line = model.Pace, tip = model.PaceTip;

            model.Refresh(frame);
            Assert.That(ReferenceEquals(model.Pace, line), Is.True, "the pace line was rebuilt although nothing moved");
            Assert.That(ReferenceEquals(model.PaceTip, tip), Is.True, "the pace tooltip was rebuilt although nothing moved");
        }

        // ---------------------------------------------------------------------------- sheltering

        static WorldSnapshot Animal(int job, bool sheltering)
        {
            var frame = new WorldSnapshot();
            frame.AddPawn(new PawnView(Ada, new CellRef(4, 0, 4), 800, 800, 700, 0));
            frame.AddPawn(new PawnView(Hog, new CellRef(8, 0, 8), 800, 800, 800, job, kind: PawnKindLabels.MiddenHogKind));
            if (sheltering) frame.AddPawnAspect(new PawnAspect(Hog, AspectKey.Of(PawnKindLabels.ShelteringAspect), 1));
            return frame;
        }

        [Test]
        public void AnAnimalShelteringReadsAsShelteringWhateverJobItBorrows()
        {
            var model = new InspectModel();
            model.SetColonist(Hog);
            string sheltering = Registry.Label(PawnKindLabels.Sheltering);

            model.Refresh(Animal(JobHandle.Wander, sheltering: true));
            Assert.That(model.Job, Is.EqualTo(sheltering), "walking to cover");
            model.Refresh(Animal(JobHandle.Wait, sheltering: true));
            Assert.That(model.Job, Is.EqualTo(sheltering), "under cover");
            Assert.That(model.JobIconKey, Is.EqualTo(PawnKindLabels.Sheltering));
        }

        [Test]
        public void WhenTheRainStopsTheSameJobReadsAsItAlwaysDid()
        {
            // The negative control, and the cache's: the same job on the next frame without the
            // flag must rebuild the line, not keep "Sheltering".
            var model = new InspectModel();
            model.SetColonist(Hog);
            model.Refresh(Animal(JobHandle.Wait, sheltering: true));
            model.Refresh(Animal(JobHandle.Wait, sheltering: false));
            Assert.That(model.Job, Is.EqualTo(Registry.Label(PawnKindLabels.Resting)));
        }

        [Test]
        public void TheAnimalsTabSaysShelteringToo()
        {
            var model = new AnimalsModel();
            model.Refresh(Animal(JobHandle.Wait, sheltering: true), new List<PawnId>());
            Assert.That(model.All.Count, Is.EqualTo(1));
            Assert.That(model.All[0].ActivityKey, Is.EqualTo(PawnKindLabels.Sheltering));
        }
    }
}
