#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Growing;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Weather;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Rain touches the world (design 43 §5, §10): it slows whoever stands in it, it waters the
    /// crops it reaches, and it sends animals for cover — and a roof or a canopy undoes all three,
    /// because all three ask the one shelter owner.
    /// </summary>
    public class WeatherWorldTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 16);

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

        /// <summary>Set the sky the way the debug menu does, and let the quick hand-over land.</summary>
        static void SetSky(ColonyWorld colony, WeatherKind kind, int intensity)
        {
            Assert.That(colony.Pawns.Weather!.HandleForce(new Intent(IntentKind.DebugSetWeather, default,
                (int)kind, intensity, 1)), Is.EqualTo(IntentRejection.None));
        }

        static void SettleSky(ColonyWorld colony) =>
            colony.World.Tick(WeatherSystem.QuickBlendTicks + WeatherSystem.IntervalTicks);

        /// <summary>
        /// A loaded world reads the outdoor temperature the saved one did. The weather's share of it
        /// is written on the weather's own cadence and saved nowhere, so until 2026-09-26 a load
        /// kept the sky its build had rolled until the next boundary, and a colonist whose needs fell
        /// due in that gap parted from the saved world by hash (found by a raid's save test). A storm
        /// against a fresh build's own sky is the control: the two must differ for the load to
        /// have anything to put back.
        /// </summary>
        [Test]
        public void ALoadedWorldReadsTheOutdoorTemperatureItWasSavedWith()
        {
            ColonyWorld original = Board();
            SetSky(original, WeatherKind.Storm, 1000);
            SettleSky(original);
            original.World.Tick(WeatherSystem.IntervalTicks / 2);
            int offset = original.Pawns.Temperature!.WeatherOffsetC;

            ColonyWorld restored = Board();
            Assume.That(restored.Pawns.Temperature!.WeatherOffsetC, Is.Not.EqualTo(offset), "a fresh build already had the storm's offset");
            restored.Load(original.Save());

            Assert.That(restored.Pawns.Temperature!.WeatherOffsetC, Is.EqualTo(offset));
            int tick = restored.World.CurrentTick;
            Assert.That(restored.Pawns.Temperature!.OutdoorTempC(tick), Is.EqualTo(original.Pawns.Temperature!.OutdoorTempC(tick)));
        }

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

        static void Fell(ColonyWorld colony, int cell)
        {
            colony.Grid.RemoveEdifice(cell);
            colony.Pawns.Chunks!.MarkDirty(Size.FromIndex(cell));
        }

        /// <summary>The ground cell in a column, as a pawn would stand on it.</summary>
        static int Ground(ColonyWorld colony, int x, int z) => colony.Grid.NearestWalkableInColumn(x, z, Size.SizeY - 1);

        /// <summary><see cref="Pawn.MoveRatePerMille"/>'s product, written out with a given sky factor.</summary>
        static int Rate(Pawn p, int weather) =>
            p.Content.Movement.movePerTick * Rates.Scale
                * p.InnatePacePerMille() / 1_000
                * p.ConditionPerMille() / 1_000
                * p.Species.movePerMille / 1_000
                * weather / 1_000
                * p.UrgencyPerMille() / 1_000;

        // ---------------------------------------------------------------------------- pace

        [Test]
        public void TheSkysPaceIsTheDesignsFormula()
        {
            WeatherDef rain = WorldContent.Weathers[(int)WeatherKind.Rain];
            Assert.That(rain.moveFloorPerMille, Is.LessThan(1000), "rain costs nothing to walk in");
            Assert.That(WeatherSystem.PaceOf(rain, 1000), Is.EqualTo(rain.moveFloorPerMille));
            Assert.That(WeatherSystem.PaceOf(rain, 500), Is.EqualTo(1000 - (1000 - rain.moveFloorPerMille) / 2), "a drizzle costs half a downpour");
            Assert.That(WeatherSystem.PaceOf(rain, 0), Is.EqualTo(1000));
            Assert.That(WeatherSystem.PaceOf(WorldContent.Weathers[(int)WeatherKind.Clear], 1000), Is.EqualTo(1000));
            Assert.That(WeatherSystem.PaceOf(WorldContent.Weathers[(int)WeatherKind.Cloudy], 1000), Is.EqualTo(1000),
                "a grey day is dry");
        }

        [Test]
        public void AColonistInTheRainSlowsByExactlyTheFactorAndUnderARoofDoesNot()
        {
            ColonyWorld colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            SetSky(colony, WeatherKind.Rain, 1000);
            SettleSky(colony);

            WeatherDef rain = WorldContent.Weathers[(int)WeatherKind.Rain];
            Assume.That(colony.Pawns.Sky!.ShelteredFromSky(pawn.Cell), Is.False, "the fixture wants her out in the open");
            Assert.That(pawn.WeatherPerMille(), Is.EqualTo(rain.moveFloorPerMille));
            Assert.That(pawn.MoveRatePerMille(), Is.EqualTo(Rate(pawn, rain.moveFloorPerMille)));
            Assert.That(pawn.MoveRatePerMille(), Is.LessThan(Rate(pawn, 1000)), "the rain slowed nobody");

            // The same colonist, the same tick, a roof over her head.
            Roof(colony, pawn.Cell);
            Assert.That(pawn.WeatherPerMille(), Is.EqualTo(1000), "a roof gives the pace back");
            Assert.That(pawn.MoveRatePerMille(), Is.EqualTo(Rate(pawn, 1000)));
        }

        [Test]
        public void ADrySkyCostsNobodyAnything()
        {
            // The negative control: out in the open on a clear day is exactly 1,000.
            ColonyWorld colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            SetSky(colony, WeatherKind.Clear, 1000);
            SettleSky(colony);
            Assume.That(colony.Pawns.Sky!.ShelteredFromSky(pawn.Cell), Is.False);
            Assert.That(pawn.WeatherPerMille(), Is.EqualTo(1000));
        }

        [Test]
        public void TheRainIsARateAndNeverAPathPrice()
        {
            // Design 43 §5: the planner does not know it is raining, so the walk it prices and the
            // walk the mover makes are the same walk. A path planned under a downpour costs what
            // the same path costs on a clear day.
            ColonyWorld colony = Board(colonists: 0);
            int from = Ground(colony, 10, 10), goal = Ground(colony, 16, 14);
            var finder = new PathFinder(colony.Pawns.Nav);

            SetSky(colony, WeatherKind.Clear, 1000);
            SettleSky(colony);
            PathResult dry = finder.FindPath(from, goal, TraverseMode.Colonist);
            SetSky(colony, WeatherKind.Storm, 1000);
            SettleSky(colony);
            Assume.That(colony.Pawns.Weather!.PacePerMilleAt(from), Is.LessThan(1000), "the storm is slowing the open ground");
            PathResult wet = finder.FindPath(from, goal, TraverseMode.Colonist);

            Assert.That(dry.Status, Is.EqualTo(PathStatus.Success));
            Assert.That(wet.Cost, Is.EqualTo(dry.Cost));
        }

        // -------------------------------------------------------------------------- growth

        [Test]
        public void ACropInTheRainGainsByExactlyTheMultiplyAndARoofedOneDoesNot()
        {
            // 15,000 is the first tick of daylight (PlantGrowthTests), so every pass here counts.
            ColonyWorld colony = Board(colonists: 0, startTick: 15_000);
            GrowingZones zones = colony.Pawns.Growing!;
            int open = Sow(colony, zones, 8, 8);
            int roofed = Sow(colony, zones, 20, 20);
            Roof(colony, roofed);

            SetSky(colony, WeatherKind.Rain, 1000);
            colony.World.Tick(500);                           // the quick hand-over lands
            int openBefore = zones.GrowthTicks(open), roofedBefore = zones.GrowthTicks(roofed);
            colony.World.Tick(PlantGrowthSystem.IntervalTicks);

            int openGain = zones.GrowthTicks(open) - openBefore;
            int roofedGain = zones.GrowthTicks(roofed) - roofedBefore;
            int growth = colony.Pawns.Weather!.GrowthPerMille(colony.World.CurrentTick);
            WeatherDef rain = WorldContent.Weathers[(int)WeatherKind.Rain];

            Assert.That(growth, Is.EqualTo(1000 + rain.growBonusPerMilleAtFull));
            Assume.That(roofedGain, Is.GreaterThan(0), "the roofed crop did not grow at all, so it compares nothing");
            Assert.That(openGain, Is.EqualTo(roofedGain * growth / 1000), "the crop the rain reaches");
            Assert.That(openGain, Is.GreaterThan(roofedGain));
        }

        [Test]
        public void ADrySkyWatersNothing()
        {
            ColonyWorld colony = Board(colonists: 0, startTick: 15_000);
            GrowingZones zones = colony.Pawns.Growing!;
            int open = Sow(colony, zones, 8, 8);
            int roofed = Sow(colony, zones, 20, 20);
            Roof(colony, roofed);

            SetSky(colony, WeatherKind.Clear, 1000);
            colony.World.Tick(500);
            int openBefore = zones.GrowthTicks(open), roofedBefore = zones.GrowthTicks(roofed);
            colony.World.Tick(PlantGrowthSystem.IntervalTicks);
            Assert.That(zones.GrowthTicks(open) - openBefore, Is.EqualTo(zones.GrowthTicks(roofed) - roofedBefore));
        }

        static int Sow(ColonyWorld colony, GrowingZones zones, int x, int z)
        {
            int cell = Ground(colony, x, z);
            Assert.That(zones.Designate(Size.FromIndex(cell), PlantHandle.Carrot), Is.EqualTo(IntentRejection.None));
            zones.Sow(cell);
            return cell;
        }

        // ------------------------------------------------------------------------- animals

        /// <summary>
        /// 08:00 on the first day. A hog keeps day hours, and at midnight it rests three times as
        /// long and walks a third as often — so a board that starts at tick nought can pass "the
        /// hog stayed put" on a hog that was asleep.
        /// </summary>
        const int Morning = 20_000;

        /// <summary>A hog on open ground, with a tree four columns east of it.</summary>
        static (ColonyWorld colony, Pawn hog, int tree) HogAndTree()
        {
            ColonyWorld colony = Board(colonists: 0, startTick: Morning);
            int start = Ground(colony, 12, 12);
            Pawn hog = colony.Pawns.Pawns.Spawn(start, PawnKindIndex.MiddenHog);
            int tree = Tree(colony, Ground(colony, 16, 12));
            Assume.That(colony.Pawns.Sky!.ShelteredFromSky(hog.Cell), Is.False, "the hog starts in the open");
            return (colony, hog, tree);
        }

        static bool RunUntilSheltered(ColonyWorld colony, Pawn pawn, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                colony.World.Tick();
                if (colony.Pawns.Sky!.ShelteredFromSky(pawn.Cell) && pawn.CurrentJob?.DefIndex == JobIndex.Wait) return true;
            }
            return false;
        }

        [Test]
        public void TheNodeMindsOnlyRainPastItsGate()
        {
            var (colony, hog, _) = HogAndTree();
            var node = new AnimalShelterThinkNode();
            var job = new Job();

            SetSky(colony, WeatherKind.Rain, AnimalShelterThinkNode.RainGatePerMille - 100);
            SettleSky(colony);
            Assert.That(node.TryGiveJob(hog, colony.Pawns, job), Is.False, "a drizzle below the gate");

            SetSky(colony, WeatherKind.Rain, 1000);
            SettleSky(colony);
            Assume.That(colony.Pawns.Sky!.ShelteredFromSky(hog.Cell), Is.False);
            Assert.That(node.TryGiveJob(hog, colony.Pawns, job), Is.True);
            Assert.That(job.DefIndex, Is.EqualTo(JobIndex.Wander));
            Assert.That(colony.Pawns.Sky!.ShelteredFromSky(job.TargetCell), Is.True, "it was sent somewhere dry");
        }

        [Test]
        public void AnAnimalCaughtInTheRainGoesUnderTheTree()
        {
            var (colony, hog, tree) = HogAndTree();
            SetSky(colony, WeatherKind.Rain, 1000);
            Assert.That(RunUntilSheltered(colony, hog, 3_000), Is.True, "the hog never found the tree");

            CellRef at = Size.FromIndex(hog.Cell), trunk = Size.FromIndex(tree);
            Assert.That(System.Math.Max(System.Math.Abs(at.X - trunk.X), System.Math.Abs(at.Z - trunk.Z)), Is.LessThanOrEqualTo(1),
                "the only cover on the board is the tree's crown");
        }

        [Test]
        public void AnAnimalAlreadyUnderCoverStaysPut()
        {
            ColonyWorld colony = Board(colonists: 0, startTick: Morning);
            int start = Ground(colony, 12, 12);
            Pawn hog = colony.Pawns.Pawns.Spawn(start, PawnKindIndex.MiddenHog);
            Roof(colony, start);
            SetSky(colony, WeatherKind.Rain, 1000);

            for (int t = 0; t < 3_000; t++)
            {
                colony.World.Tick();
                Assert.That(hog.Cell, Is.EqualTo(start), $"the hog left its roof in the rain at tick {t}");
            }
        }

        [Test]
        public void TheSameAnimalUnderTheSameRoofWandersOnADryDay()
        {
            // The negative control for the one above: without the rain it does not stay put, so
            // staying put is the rain's doing and not a hog that never moves.
            ColonyWorld colony = Board(colonists: 0, startTick: Morning);
            int start = Ground(colony, 12, 12);
            Pawn hog = colony.Pawns.Pawns.Spawn(start, PawnKindIndex.MiddenHog);
            Roof(colony, start);
            SetSky(colony, WeatherKind.Clear, 1000);

            bool moved = false;
            for (int t = 0; t < 3_000 && !moved; t++)
            {
                colony.World.Tick();
                moved = hog.Cell != start;
            }
            Assert.That(moved, Is.True);
        }

        [Test]
        public void WhenItsTreeIsFelledMidRainTheAnimalLooksAgain()
        {
            var (colony, hog, tree) = HogAndTree();
            int second = Tree(colony, Ground(colony, 12, 17));
            SetSky(colony, WeatherKind.Rain, 1000);

            // Which tree it reaches first is the scan's to say; the other is where it must go next.
            Assert.That(RunUntilSheltered(colony, hog, 3_000), Is.True, "the hog never found cover");
            CellRef at = Size.FromIndex(hog.Cell);
            bool underFirst = Near(at, tree);
            int gone = underFirst ? tree : second, left = underFirst ? second : tree;
            Assume.That(underFirst || Near(at, second), Is.True);

            Fell(colony, gone);
            Assume.That(colony.Pawns.Sky!.ShelteredFromSky(hog.Cell), Is.False, "the felled tree still shades it");
            Assert.That(RunUntilSheltered(colony, hog, 3_000), Is.True, "the hog stood in the rain where its tree had been");
            Assert.That(Near(Size.FromIndex(hog.Cell), left), Is.True, "it went under the tree still standing");
        }

        static bool Near(CellRef at, int tree)
        {
            CellRef trunk = Size.FromIndex(tree);
            return System.Math.Max(System.Math.Abs(at.X - trunk.X), System.Math.Abs(at.Z - trunk.Z)) <= 1;
        }
    }
}
