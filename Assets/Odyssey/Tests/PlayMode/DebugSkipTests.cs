#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Growing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// The debug menu's day skip, on the real bootstrap: the warp spends the day's own ticks in
    /// one synchronous batch, and it does so while the clock is paused — the whole point of the
    /// row is testing growth without waiting for the clock, and the tester pauses.
    /// </summary>
    public class DebugSkipTests
    {
        [UnityTest]
        public IEnumerator SkipOneDaySpendsTheDaysOwnTicksWhilePausedAndTheCropGrows()
        {
            GameObject root = RigWorld.Build(out OdysseyBootstrap boot, out _);

            try
            {
                yield return RigWorld.WarmUp();
                Assert.That(boot.World, Is.Not.Null, "the bootstrap never built a world");
                Assert.That(boot.Colony, Is.Not.Null, "the bootstrap never built a colony");
                var colony = boot.Colony!;
                var zones = colony.Growing!;
                Assert.That(zones, Is.Not.Null, "the session has no growing zones");

                // One sown cell beside the start, the thing the skip exists to grow.
                var at = new CellRef(colony.Start.X, colony.Start.Z, colony.Start.Y);
                boot.World!.Intents.Submit(
                    new Intent(IntentKind.DesignateZone, at, PlantHandle.Carrot + 1));
                boot.World.Intents.Submit(new Intent(IntentKind.SetGameSpeed, at, 0));
                boot.World.Tick();
                Assert.That(boot.World.GameSpeed, Is.EqualTo(0), "the clock is paused");
                int index = colony.Grid.Size.Index(at.X, at.Z, at.Y);
                zones.Sow(index);
                int before = zones.GrowthTicks(index);

                int day = colony.Pawns.Content.DayTicks;
                int tickBefore = boot.World.CurrentTick;
                boot.DebugSkipTicks(day);

                Assert.That(boot.World.CurrentTick, Is.EqualTo(tickBefore + day),
                    "the skip spends exactly the day's own ticks");
                Assert.That(boot.World.GameSpeed, Is.EqualTo(0),
                    "a skip does not start the clock the tester stopped");
                Assert.That(zones.GrowthTicks(index), Is.GreaterThan(before),
                    "the crop grew through the skipped day's daylight windows");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }
    }
}
