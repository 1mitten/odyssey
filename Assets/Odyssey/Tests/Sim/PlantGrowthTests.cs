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

        /// <summary>One sown cell: the air above the turf in one column, where the crop stands.</summary>
        static (int index, GrowingZones zones, ColonyWorld colony) OneCell(int startTick, ChunkGrid? chunks = null)
        {
            ColonyWorld colony = FieldAt(startTick, chunks);
            var zones = colony.Pawns.Growing!;
            CellRef at = new CellRef(8, 8, TopAir(colony.Grid, 8, 8));
            Assert.That(zones.Designate(at, PlantHandle.Carrot), Is.EqualTo(IntentRejection.None),
                "bare board is grass everywhere, so this cell plants or the fixture is wrong");
            int index = colony.Grid.Index(at);
            zones.Sow(index);
            return (index, zones, colony);
        }

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

            // The stage thresholds are where the re-meshes live: one just below a third, one
            // just past it — the exact pair of cadence values either side of the boundary.
            Assert.That(carrot.StageOfTicks(43_250), Is.EqualTo(1));
            Assert.That(carrot.StageOfTicks(43_500), Is.EqualTo(2));
            Assert.That(carrot.StageOfTicks(86_500), Is.EqualTo(2));
            Assert.That(carrot.StageOfTicks(86_750), Is.EqualTo(3));
            Assert.That(carrot.StageOfTicks(carrot.growTicks), Is.EqualTo(3));
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

            // Run the crop to just past the first third. 43,500 accumulated ticks is 174 runs:
            // 130 through the first day's window and 44 more when day one opens at 75,000 —
            // the last of them on tick 85,750, which this run executes exactly.
            colony.World.Tick(70_501);
            Assert.That(zones.GrowthTicks(index), Is.EqualTo(43_500));
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
