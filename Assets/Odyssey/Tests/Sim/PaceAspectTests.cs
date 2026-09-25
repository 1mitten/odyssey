#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Weather;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What reaches the colonist pane about a pace (design 17 §5a, design 43 §6a). The move rate
    /// was published and nothing read it; the owner could not tell whether anybody was slower in
    /// the rain. So the factors it is a product of go out beside it, one row each, and an animal
    /// the rain has sent for cover says so.
    ///
    /// <para>The property that matters is that <b>the factors multiply back to the rate</b>: the
    /// pane's headline is their product and its tooltip lists them, so a factor published from a
    /// second formula, or a new factor multiplied into the rate and never published, would make
    /// the pane explain a number it is not showing.</para>
    /// </summary>
    public class PaceAspectTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 16);

        /// <summary>08:00 on the first day, so a hog is awake (see WeatherWorldTests.Morning).</summary>
        const int Morning = 20_000;

        static ColonyWorld Board(int colonists = 1, int startTick = 0)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = colonists;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(new ColonyRequest
            {
                Size = Size,
                Seed = 1u,
                Scenario = scenario,
                Barren = true,
                StartTick = startTick,
            });
        }

        static void SetSky(ColonyWorld colony, WeatherKind kind, int intensity) =>
            Assert.That(colony.Pawns.Weather!.HandleForce(new Intent(IntentKind.DebugSetWeather, default,
                (int)kind, intensity, 1)), Is.EqualTo(IntentRejection.None));

        static void SettleSky(ColonyWorld colony) =>
            colony.World.Tick(WeatherSystem.QuickBlendTicks + WeatherSystem.IntervalTicks);

        static void Roof(ColonyWorld colony, int cell)
        {
            int above = cell + Size.LayerStride;
            colony.Grid.Floor[above] = CoreContent.SlabBuilt;
            colony.Pawns.Chunks!.MarkDirty(Size.FromIndex(above));
        }

        static int Tree(ColonyWorld colony, int cell)
        {
            var records = colony.Pawns.Construction!.Edifices.Records;
            records.Add(new PlacedEdifice { CellIndex = cell, Def = NaturalContent.EdificeTreeBirch, Stuff = NaturalContent.StuffWood });
            colony.Grid.Edifice[cell] = records.Count - 1;
            colony.Pawns.Chunks!.MarkDirty(Size.FromIndex(cell));
            return cell;
        }

        static int Ground(ColonyWorld colony, int x, int z) => colony.Grid.NearestWalkableInColumn(x, z, Size.SizeY - 1);

        /// <summary>A published factor, or 1,000 when it is absent — which is what absence means.</summary>
        static int Factor(ColonyWorld colony, Pawn pawn, AspectKey key) =>
            colony.World.Views.Current.TryGetPawnAspect(pawn.Id, key, out int value) ? value : Rates.Scale;

        static bool Has(ColonyWorld colony, Pawn pawn, AspectKey key) =>
            colony.World.Views.Current.TryGetPawnAspect(pawn.Id, key, out _);

        /// <summary>
        /// The published factors, multiplied in <see cref="Pawn.MoveRatePerMille"/>'s order and
        /// with its truncation, starting from the tuned walk. The species' pace is 1,000 for a
        /// person and is not published, so it is multiplied in from the pawn.
        /// </summary>
        static int Recomposed(ColonyWorld colony, Pawn pawn) =>
            pawn.Content.Movement.movePerTick * Rates.Scale
                * Factor(colony, pawn, RateAspects.PaceRolled) / 1_000
                * Factor(colony, pawn, RateAspects.PaceCondition) / 1_000
                * pawn.Species.movePerMille / 1_000
                * Factor(colony, pawn, RateAspects.PaceWeather) / 1_000
                * Factor(colony, pawn, RateAspects.PaceUrgency) / 1_000;

        static int PublishedMove(ColonyWorld colony, Pawn pawn)
        {
            Assert.That(colony.World.Views.Current.TryGetPawnAspect(pawn.Id, RateAspects.Move, out int move), Is.True);
            return move;
        }

        /// <summary>
        /// The interface reads these by string, from an assembly that cannot see this one; its
        /// twin is <c>PaceModelTests.TheAspectNamesMatchTheOnesTheSimulationPublishes</c>.
        /// </summary>
        [Test]
        public void TheNamesAreTheOnesTheInterfaceReads()
        {
            Assert.That(RateAspects.PaceRolledName, Is.EqualTo("odyssey.pawn.rate.move.rolled"));
            Assert.That(RateAspects.PaceConditionName, Is.EqualTo("odyssey.pawn.rate.move.condition"));
            Assert.That(RateAspects.PaceWeatherName, Is.EqualTo("odyssey.pawn.rate.move.weather"));
            Assert.That(RateAspects.PaceUrgencyName, Is.EqualTo("odyssey.pawn.rate.move.urgency"));
            Assert.That(AnimalShelterThinkNode.ShelteringName, Is.EqualTo("odyssey.pawn.sheltering"));
        }

        // ---------------------------------------------------------------------------- the factors

        [Test]
        public void ADryFedUndraftedColonistPublishesHerRolledPaceAndNothingElse()
        {
            // The negative control for everything below: the three sparse factors are absent, so
            // a colony on a dry day pays one row a colonist for the whole readout.
            ColonyWorld colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            SetSky(colony, WeatherKind.Clear, 1000);
            SettleSky(colony);

            Assume.That(pawn.ConditionPerMille(), Is.EqualTo(Rates.Scale), "the fixture wants her well");
            Assert.That(Factor(colony, pawn, RateAspects.PaceRolled), Is.EqualTo(pawn.InnatePacePerMille()));
            Assert.That(Has(colony, pawn, RateAspects.PaceCondition), Is.False);
            Assert.That(Has(colony, pawn, RateAspects.PaceWeather), Is.False);
            Assert.That(Has(colony, pawn, RateAspects.PaceUrgency), Is.False);
            Assert.That(Recomposed(colony, pawn), Is.EqualTo(PublishedMove(colony, pawn)));
        }

        [Test]
        public void EachFactorIsTheValueOfTheMethodTheRateMultiplies()
        {
            // Starving, drafted and out in a downpour, so every sparse factor is present at once.
            ColonyWorld colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            SetSky(colony, WeatherKind.Rain, 1000);
            SettleSky(colony);
            Assume.That(colony.Pawns.Sky!.ShelteredFromSky(pawn.Cell), Is.False, "the fixture wants her in the open");

            pawn.StarvationSeverity = 750;
            pawn.Drafted = true;
            colony.World.Tick();

            Assert.That(Factor(colony, pawn, RateAspects.PaceRolled), Is.EqualTo(pawn.InnatePacePerMille()));
            Assert.That(Has(colony, pawn, RateAspects.PaceCondition), Is.True, "starving cost her nothing");
            Assert.That(Factor(colony, pawn, RateAspects.PaceCondition), Is.EqualTo(pawn.ConditionPerMille()));
            Assert.That(Has(colony, pawn, RateAspects.PaceWeather), Is.True, "the rain cost her nothing");
            Assert.That(Factor(colony, pawn, RateAspects.PaceWeather), Is.EqualTo(pawn.WeatherPerMille()));
            Assert.That(Has(colony, pawn, RateAspects.PaceUrgency), Is.True, "the draft cost her nothing");
            Assert.That(Factor(colony, pawn, RateAspects.PaceUrgency), Is.EqualTo(pawn.UrgencyPerMille()));
        }

        [Test]
        public void ThePublishedFactorsMultiplyBackToThePublishedMoveRateExactly()
        {
            // Every combination of the three sparse factors, and the product in the rate's own
            // order, truncation and all, lands on the published integer — not within one of it.
            for (int mask = 0; mask < 8; mask++)
            {
                bool starving = (mask & 1) != 0, raining = (mask & 2) != 0, drafted = (mask & 4) != 0;
                ColonyWorld colony = Board();
                Pawn pawn = colony.Pawns.Pawns.All[0];
                SetSky(colony, raining ? WeatherKind.Rain : WeatherKind.Clear, 1000);
                SettleSky(colony);
                if (starving) pawn.StarvationSeverity = 1_000;
                pawn.Drafted = drafted;
                colony.World.Tick();

                string state = $"starving {starving}, raining {raining}, drafted {drafted}";
                Assert.That(Recomposed(colony, pawn), Is.EqualTo(PublishedMove(colony, pawn)), state);
                Assert.That(Has(colony, pawn, RateAspects.PaceCondition), Is.EqualTo(starving), state);
                Assert.That(Has(colony, pawn, RateAspects.PaceUrgency), Is.EqualTo(drafted), state);
            }
        }

        [Test]
        public void TheTunedWalkIsOneCellCostATickSoThePaneCanStartFromAThousand()
        {
            // The pane cannot read content: it composes its headline from the factors alone,
            // starting at 1,000, the standard walk. That equals the move rate only while the walk
            // is tuned at one cost unit a tick. Retuning it is legitimate; this is where it says
            // the pane's headline needs the base published beside the factors.
            Assert.That(ContentPack.Pawns().Movement.movePerTick, Is.EqualTo(1));
        }

        [Test]
        public void UnderARoofInTheRainTheWeatherFactorIsAbsent()
        {
            ColonyWorld colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            SetSky(colony, WeatherKind.Rain, 1000);
            SettleSky(colony);
            Assume.That(Has(colony, pawn, RateAspects.PaceWeather), Is.True, "the fixture wants her rained on first");

            pawn.Drafted = true; // so she holds still under the roof
            Roof(colony, pawn.Cell);
            colony.World.Tick();
            Assert.That(Has(colony, pawn, RateAspects.PaceWeather), Is.False, "a roof gives the pace back");
            Assert.That(Recomposed(colony, pawn), Is.EqualTo(PublishedMove(colony, pawn)));
        }

        // ---------------------------------------------------------------------------- sheltering

        /// <summary>A hog on open ground, with a tree four columns east of it.</summary>
        static (ColonyWorld colony, Pawn hog) HogAndTree()
        {
            ColonyWorld colony = Board(colonists: 0, startTick: Morning);
            Pawn hog = colony.Pawns.Pawns.Spawn(Ground(colony, 12, 12), PawnKindIndex.MiddenHog);
            Tree(colony, Ground(colony, 16, 12));
            Assume.That(colony.Pawns.Sky!.ShelteredFromSky(hog.Cell), Is.False, "the hog starts in the open");
            return (colony, hog);
        }

        [Test]
        public void AnAnimalHeadingForCoverAndWaitingUnderItReadsAsSheltering()
        {
            var (colony, hog) = HogAndTree();
            SetSky(colony, WeatherKind.Rain, 1000);

            bool walked = false, waited = false;
            for (int t = 0; t < 3_000 && !waited; t++)
            {
                colony.World.Tick();
                bool sheltering = Has(colony, hog, AnimalShelterThinkNode.Sheltering);
                int job = hog.CurrentJob?.DefIndex ?? -1;
                if (job == JobIndex.Wander && sheltering) walked = true;
                if (job == JobIndex.Wait && colony.Pawns.Sky!.ShelteredFromSky(hog.Cell))
                {
                    Assert.That(sheltering, Is.True, $"under the tree at tick {t} and not sheltering");
                    waited = true;
                }
            }

            Assert.That(walked, Is.True, "the walk to the tree never read as sheltering");
            Assert.That(waited, Is.True, "the hog never reached the tree");
        }

        [Test]
        public void OnADryDayTheSameAnimalNeverReadsAsSheltering()
        {
            var (colony, hog) = HogAndTree();
            SetSky(colony, WeatherKind.Clear, 1000);
            for (int t = 0; t < 3_000; t++)
            {
                colony.World.Tick();
                Assert.That(Has(colony, hog, AnimalShelterThinkNode.Sheltering), Is.False, $"sheltering on a dry day at tick {t}");
            }
        }

        [Test]
        public void RainBelowTheGateIsNotSheltering()
        {
            // The same gate the node uses: a drizzle the hog does not mind is not a drizzle it is
            // sheltering from, even resting under a roof.
            ColonyWorld colony = Board(colonists: 0, startTick: Morning);
            int start = Ground(colony, 12, 12);
            Pawn hog = colony.Pawns.Pawns.Spawn(start, PawnKindIndex.MiddenHog);
            Roof(colony, start);
            SetSky(colony, WeatherKind.Rain, AnimalShelterThinkNode.RainGatePerMille - 100);
            SettleSky(colony);
            for (int t = 0; t < 1_000; t++)
            {
                colony.World.Tick();
                Assert.That(Has(colony, hog, AnimalShelterThinkNode.Sheltering), Is.False, $"sheltering from a drizzle at tick {t}");
            }
        }

        [Test]
        public void AColonistNeverPublishesSheltering()
        {
            ColonyWorld colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Roof(colony, pawn.Cell);
            SetSky(colony, WeatherKind.Rain, 1000);
            SettleSky(colony);
            Assert.That(Has(colony, pawn, AnimalShelterThinkNode.Sheltering), Is.False);
        }
    }
}
