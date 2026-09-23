#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Power (design 32): the line layer, the nets, the balance, the burn and the heat. Built up a
    /// unit at a time; each section below is one of design 32's own.
    /// </summary>
    public class PowerTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Board(uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: false);
        }

        static IntentRejection Order(ColonyWorld colony, CellRef cell, int building, int stuff, int facing = 0)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(new Intent(IntentKind.PlaceBuilding, cell, building, stuff, facing));
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        // ---- the content (§6, §7) --------------------------------------------------------------

        /// <summary>
        /// The three rows say what design 32 says, and the one number every later test leans on —
        /// five heaters to a generator — holds.
        /// </summary>
        [Test]
        public void TheContentIsDesign32s()
        {
            BuildingDef line = ConstructionContent.BuildingAt(BuildingHandle.Conduit);
            BuildingDef generator = ConstructionContent.BuildingAt(BuildingHandle.Generator);
            BuildingDef heater = ConstructionContent.BuildingAt(BuildingHandle.Heater);

            Assert.That(line.conduit, Is.True);
            Assert.That(line.edifice, Is.EqualTo(CoreContent.EdificeNone), "a line is not an edifice");
            Assert.That(line.fixedStuff, Is.EqualTo(StuffHandle.Wood));
            Assert.That(line.costCount, Is.EqualTo(1));

            Assert.That(generator.edifice, Is.EqualTo(CoreContent.EdificeGenerator));
            Assert.That(generator.footprint, Is.EqualTo(2));
            Assert.That(generator.powerOutputW, Is.EqualTo(1_000));
            Assert.That(generator.fuelItem, Is.EqualTo(ItemHandle.Wood));
            Assert.That(generator.fuelCapacity, Is.EqualTo(75));
            Assert.That(generator.fuelPerDay, Is.EqualTo(22));

            Assert.That(heater.edifice, Is.EqualTo(CoreContent.EdificeHeater));
            Assert.That(heater.powerDrawW, Is.EqualTo(175));
            Assert.That(generator.powerOutputW / heater.powerDrawW, Is.EqualTo(5),
                "five heaters to a generator, and a sixth darkens the net");

            Assert.That(ConstructionContent.BuildingAt(BuildingHandle.Campfire).IsPowered, Is.False);
            Assert.That(generator.IsPowered && heater.IsPowered, Is.True);
        }
    }
}
