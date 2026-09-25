#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Growing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The growth pass: a crop gains ground only inside the daylight window, on the Rare
    /// cadence, and re-meshes only when it crosses a drawn stage.
    ///
    /// <para>The fixture is a real colony world on the bare board, because the arithmetic under
    /// test is the clock's — tick of day, the window, the 250-tick cadence — and those live in
    /// <see cref="PlantGrowthSystem"/>'s read of the running tick, not in any table. The colony
    /// is empty of people so that nothing else moves the world or dirties a chunk under the
    /// assertions; the field is alone on the board, which is the point.</para>
    /// </summary>
    public class PlantGrowthTests
    {
        static readonly GridSize FieldSize = new GridSize(24, 24, 4);

        /// <summary>An empty colony on flat grass, clock parked at <paramref name="startTick"/>.</summary>
        static ColonyWorld FieldAt(int startTick, ChunkGrid? chunks = null) =>
            ColonyWorld.Build(new ColonyRequest
            {
                Size = FieldSize,
                Seed = 5u,
                Scenario = Nobody(ScenarioDef.Bare()),
                StartTick = startTick,
                Chunks = chunks,
            });

        static ScenarioDef Nobody(ScenarioDef scenario)
        {
            scenario.colonists = 0;
            return scenario;
        }

        /// <summary>
        /// How long before the named tick the world is built, so the sky can be settled first.
        ///
        /// <para><b>The sky is held clear</b> (design 43 §5). This file is about the clock — the
        /// window, the cadence, the stage thresholds — and since the weather-world step a crop the
        /// rain reaches gains more than one interval a pass. Seed 5's first roll put rain on the
        /// field, so without this the clock tests measured the sky. The debug menu's command,
        /// given early enough that its quick hand-over has landed before anything is sown.</para>
        /// </summary>
        const int SkyLead = 600;

        /// <summary>One sown cell: the air above the turf in one column, where the crop stands.</summary>
        static (int index, GrowingZones zones, ColonyWorld colony) OneCell(int startTick, ChunkGrid? chunks = null)
        {
            int lead = startTick >= SkyLead ? SkyLead : 0;
            ColonyWorld colony = FieldAt(startTick - lead, chunks);
            HoldClear(colony);
            colony.World.Tick(lead);
            var zones = colony.Pawns.Growing!;
            CellRef at = new CellRef(8, 8, TopAir(colony.Grid, 8, 8));
            Assert.That(zones.Designate(at, PlantHandle.Carrot), Is.EqualTo(IntentRejection.None),
                "bare board is grass everywhere, so this cell plants or the fixture is wrong");
            int index = colony.Grid.Index(at);
            zones.Sow(index);
            return (index, zones, colony);
        }

        /// <summary>
        /// Set the sky clear, as the debug menu does. A forced spell lasts its rolled length and the
        /// season takes over after it, so a test that runs for days sets it again as it goes;
        /// clear handing over to clear is clear throughout.
        /// </summary>
        static void HoldClear(ColonyWorld colony) =>
            Assert.That(colony.Pawns.Weather!.HandleForce(new Intent(IntentKind.DebugSetWeather, default,
                (int)WeatherKind.Clear, 1000, 1)), Is.EqualTo(IntentRejection.None));

        static int TopAir(CellGrid grid, int x, int z)
        {
            var size = grid.Size;
            for (int y = size.SizeY - 1; y >= 0; y--)
                if (grid.IsSolidTerrain(size.Index(x, z, y)))
                    return y + 1;
            Assert.Fail($"column {x},{z} has no ground");
            return -1;
        }

        [Test]
        public void GrowthAdvancesInsideTheWindowAndNotOutside()
        {
            // 15000 is the first tick of daylight; 47500 the first of the dark, and a multiple
            // of 250, so it is exactly the tick a cadence run would fall on if the window did
            // not refuse it.
            var (day, dayZones, dayColony) = OneCell(15_000);
            dayColony.World.Tick(250);
            Assert.That(dayZones.GrowthTicks(day), Is.EqualTo(250),
                "one cadence run inside the window credits one interval");

            var (dusk, duskZones, duskColony) = OneCell(47_500);
            duskColony.World.Tick(250);
            Assert.That(duskZones.GrowthTicks(dusk), Is.EqualTo(0),
                "47500 is night: the pass runs and refuses, which is what the window means");
        }

        [Test]
        public void TheCounterStopsAtRipe()
        {
            var (index, zones, _) = OneCell(0);
            PlantDef carrot = zones.Plant(PlantHandle.Carrot);
            zones.Advance(index, carrot.growTicks + 1_000_000);
            Assert.That(zones.GrowthTicks(index), Is.EqualTo(carrot.growTicks),
                "a ripe field's counter stops, so the hash of a ripe field is one number");
        }

        [Test]
        public void TheDrawnStageCrossesThirdsAndTwoThirds()
        {
            PlantDef carrot = ContentPack.Plants()[0];

            // The stage thresholds are where the re-meshes live: one just below 45 per cent,
            // one just past it — the exact pair of cadence values either side of the boundary.
            // The bands were thirds until play showed the third stage arriving a third short of
            // ripe: full-size carrots standing correctly unharvestable for over a day read as a
            // harvest nobody was taking (owner, 2026-09-18), so the big art now arrives at 85.
            Assert.That(carrot.StageOfTicks(58_250), Is.EqualTo(1));
            Assert.That(carrot.StageOfTicks(58_500), Is.EqualTo(2));
            Assert.That(carrot.StageOfTicks(110_250), Is.EqualTo(2));
            Assert.That(carrot.StageOfTicks(110_500), Is.EqualTo(3));
            Assert.That(carrot.StageOfTicks(carrot.growTicks), Is.EqualTo(3));
        }

        [Test]
        public void TheFirstDayIsTheSeedDayAndTheSproutComesAfterIt()
        {
            PlantDef carrot = ContentPack.Plants()[0];

            // 25 per cent of the carrot is one daylight window - 32,500 of 130,000 ticks - and
            // below it the drawn stage is nought: the seed lies in the soil and nothing stands
            // above it. The owner asked for exactly that reading (2026-09-19: "the seeds should
            // stay there at first - the sprouting should appear after a day rather than
            // immediately"), and a whole daylight day is what a day of growth actually is here.
            Assert.That(carrot.StageOfTicks(0), Is.EqualTo(0),
                "a seed that went in this morning does not sprout by the afternoon");
            Assert.That(carrot.StageOfTicks(32_250), Is.EqualTo(0));
            Assert.That(carrot.StageOfTicks(32_500), Is.EqualTo(1),
                "the sprout stands after the first full day of light");
        }

        [Test]
        public void CrossingAStageDirtiesTheChunkAndGrowingWithinItDoesNot()
        {
            var chunks = new ChunkGrid(FieldSize);
            var (index, zones, colony) = OneCell(15_000, chunks);
            CellRef at = colony.Grid.Size.FromIndex(index);
            int chunk = chunks.ChunkIndexOfCell(at);

            // The fixture's sowing dirties the chunk itself, and rightly: a crop appearing is a
            // drawn change. Cleared here so what this test reads is what the growth pass alone
            // marks.
            chunks.ClearDirty(chunk);

            colony.World.Tick(250);
            Assert.That(chunks.IsDirty(chunk), Is.False,
                "sprout to slightly taller sprout re-meshes nothing: a field at noon draws as the meadow beside it");

            // Run the crop to just past the first boundary — 45 per cent of 130,000 is 58,500,
            // a whole number of 250-tick runs, so the loop lands on it exactly without deriving
            // the window cadence by hand: it crosses the boundary on the run that reaches it.
            while (zones.GrowthTicks(index) < 58_500)
            {
                HoldClear(colony);
                colony.World.Tick(250);
            }
            Assert.That(zones.GrowthTicks(index), Is.EqualTo(58_500));
            Assert.That(chunks.IsDirty(chunk), Is.True,
                "the stage bucket changed, and a bucket change is the only thing that re-meshes");
        }

        [Test]
        public void TwoFieldsGrowAlikeAndTheClockAloneDecides()
        {
            // Two sowings at the same tick must read the same by construction — the pass adds a
            // constant inside the window and nothing rolls dice — but the pair is what catches
            // somebody ordering the scan over an unordered collection rather than the sorted one.
            var (a, aZones, aColony) = OneCell(15_000);
            var (b, bZones, bColony) = OneCell(15_000);
            aColony.World.Tick(5_000);
            bColony.World.Tick(5_000);
            Assert.That(aZones.GrowthTicks(a), Is.EqualTo(bZones.GrowthTicks(b)));
        }
    }
}
