#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The weather's temperature offset comes back with a load (found by the cover gate,
    /// 2026-09-25). It is derived and written only on a weather pass, so a colony loaded between two
    /// passes read the fresh board's offset — and a colonist's ambient, and her mood, with it —
    /// until the next one, and parted from the run that was never saved.
    /// </summary>
    public class WeatherLoadTests
    {
        [Test]
        public void ALoadBetweenTwoWeatherPassesKeepsTheSkysTemperature()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick();
            colony.World.Intents.Submit(new Intent(IntentKind.DebugSetWeather, default, (int)WeatherKind.Storm, 1000, 0));
            // Past the next pass, so the storm's offset is the one written, then a little more so
            // the save falls between passes.
            for (int t = 0; t < 250; t++) colony.World.Tick();
            int offset = colony.Pawns.Temperature!.WeatherOffsetC;
            Assert.That(offset, Is.Not.EqualTo(0), "the control: a storm moves the temperature");

            byte[] saved = colony.Save();
            var restored = Board(colonists: 1);
            restored.Load(saved);
            Assert.That(restored.Pawns.Temperature!.WeatherOffsetC, Is.EqualTo(offset));

            for (int t = 0; t < 400; t++)
            {
                colony.World.Tick();
                restored.World.Tick();
                Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value),
                    $"diverged {t + 1} ticks after the load");
            }
        }
    }
}
