#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Growing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A growing field is authored state: the zones, which cells hold a crop and how far that
    /// crop has got all survive a save, and the world they come back into hashes the same as
    /// the one that was saved.
    ///
    /// <para>The silent four-day theft this file exists against: growth is a counter the player
    /// never sees directly, so a round trip that reset it to nought would still draw a green
    /// field and still look like the game working. The hash assertion is what would catch it —
    /// a reset counter is a different world — but only alongside the direct read, which names
    /// the loss rather than diagnosing it.</para>
    /// </summary>
    public class GrowingZoneSaveTests
    {
        static readonly GridSize Size = new GridSize(24, 24, 4);
        const uint Seed = 5u;

        static ColonyWorld Bare() => ColonyWorld.Build(new ColonyRequest
        {
            Size = Size,
            Seed = Seed,
            Scenario = EmptyColony(ScenarioDef.Bare()),
            StartTick = 15_000, // first light, so the ticks after it grow the crop
        });

        static ScenarioDef EmptyColony(ScenarioDef scenario)
        {
            scenario.colonists = 0;
            return scenario;
        }

        /// <summary>The air cell above the turf of a column, where a crop stands.</summary>
        static CellRef TopAir(ColonyWorld colony, int x, int z)
        {
            var grid = colony.Grid;
            for (int y = grid.Size.SizeY - 1; y >= 0; y--)
                if (grid.IsSolidTerrain(grid.Size.Index(x, z, y)))
                    return new CellRef(x, z, y + 1);
            Assert.Fail($"column {x},{z} has no ground");
            return default;
        }

        [Test]
        public void AFieldSurvivesTheRoundTripDownToTheCounter()
        {
            ColonyWorld original = Bare();
            var zones = original.Pawns.Growing!;

            CellRef field = TopAir(original, 8, 8);
            CellRef edge = TopAir(original, 9, 8);
            CellRef lone = TopAir(original, 12, 12); // its own record, and never sown

            Assert.That(zones.Designate(field, PlantHandle.Carrot), Is.EqualTo(IntentRejection.None));
            Assert.That(zones.Designate(edge, PlantHandle.Carrot), Is.EqualTo(IntentRejection.None));
            Assert.That(zones.Designate(lone, PlantHandle.Carrot), Is.EqualTo(IntentRejection.None));
            zones.Sow(original.Grid.Index(field));
            zones.Sow(original.Grid.Index(edge));

            original.World.Tick(251); // two cadence runs: the standing crop is at 500 ticks

            int before = zones.GrowthTicks(original.Grid.Index(field));
            Assert.That(before, Is.GreaterThan(0), "the crop grew before the save, or this test proves nothing");
            StateHash savedHash = original.World.ComputeStateHash();

            ColonyWorld restored = Bare();
            restored.Load(original.Save());

            var back = restored.Pawns.Growing!;
            Assert.That(back.ZoneCount, Is.EqualTo(zones.ZoneCount), "both fields came back");
            Assert.That(back.Cells.Count, Is.EqualTo(zones.Cells.Count));
            Assert.That(back.ZonePlantAt(restored.Grid.Index(lone)), Is.EqualTo(PlantHandle.Carrot),
                "the unsown cell is still zoned");
            Assert.That(back.IsPlanted(restored.Grid.Index(lone)), Is.False,
                "and still fallow: a painted cell is not a planted one");
            Assert.That(back.IsPlanted(restored.Grid.Index(field)), Is.True);
            Assert.That(back.GrowthTicks(restored.Grid.Index(field)), Is.EqualTo(before),
                "the counter is the save's business: resetting it would be a silent four-day theft");
            Assert.That(restored.World.ComputeStateHash(), Is.EqualTo(savedHash),
                "the restored world is, to the hash, the world that was saved");
        }
    }
}
