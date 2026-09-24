#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// The debug menu's Weather tab, on the real bootstrap (owner, 2026-09-24: "we need to be able
    /// to test it"). A preset is set on the director the tab's rows call, and the test follows it
    /// into what the frame draws: the rain closes on the preset over game seconds, the GPU rain
    /// submits its two calls, the particle arm takes over when asked, and Clear stops the drops at
    /// once while the ground dries more slowly than it wet.
    ///
    /// <para>What it cannot prove is that a click on the row reaches the director — no PlayMode
    /// test here can press a button (CLAUDE.md, "Nothing tests that a click reaches the game").
    /// The row's whole body is <c>SetWeather(index)</c>, which is what is called here.</para>
    /// </summary>
    public class WeatherTabTests
    {
        [UnityTest]
        public IEnumerator APresetArrivesDrawsAndClearsWhileTheGroundDriesSlower()
        {
            GameObject root = RigWorld.Build(out OdysseyBootstrap boot, out SliceCameraRig _);
            try
            {
                yield return RigWorld.WarmUp();
                Assert.That(boot.Directors, Is.Not.Null, "the bootstrap built no directors");
                WeatherLook? weather = boot.Weather;
                Assert.That(weather, Is.Not.Null, "the bootstrap built no weather look");
                if (!weather!.Drawer.Available)
                    Assert.Ignore("Odyssey/Rain did not load, so there is no rain to follow");

                DebugDirector debug = boot.Directors!.Debug;
                Assert.That(weather.Rain, Is.Zero, "a session starts clear");
                Assert.That(weather.Drawer.LastDrawCalls, Is.Zero, "clear submits no rain");

                debug.SetWeather(System.Array.FindIndex(DebugDirector.WeatherPresets,
                    p => p.Key == DebugDirector.WeatherDownpourKey));
                // Enough game time for the sky to close on the preset: two seconds at SkyRate.
                yield return Frames(boot, seconds: 3f);
                Assert.That(weather.Rain, Is.EqualTo(1f).Within(0.01f), "the downpour never arrived");
                Assert.That(weather.Cloud, Is.EqualTo(1f).Within(0.01f));
                Assert.That(weather.Drawer.LastDrawCalls, Is.EqualTo(2), "the GPU rain did not submit");
                Assert.That(weather.Drawer.LastStreaks, Is.EqualTo(weather.Drawer.MaxStreaks));
                float wetAtDownpour = weather.Wet;
                Assert.That(wetAtDownpour, Is.GreaterThan(0.2f), "the ground did not start to wet");

                debug.SetRainAsParticles(true);
                yield return Frames(boot, seconds: 0.5f);
                Assert.That(weather.Drawer.LastDrawCalls, Is.Zero, "both kinds of rain drew at once");
                Assert.That(weather.Particles.LiveStreaks, Is.GreaterThan(0), "the particle arm emitted nothing");
                debug.SetRainAsParticles(false);

                debug.SetWeather(0);
                yield return Frames(boot, seconds: 3f);
                Assert.That(weather.Rain, Is.Zero, "the rain outlasted Clear");
                Assert.That(weather.Drawer.LastDrawCalls, Is.Zero);
                Assert.That(weather.Wet, Is.GreaterThan(0f), "the ground dried as fast as the rain stopped");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// Run frames until this many seconds of game time have passed. Guarded by real time, not
        /// a frame count: the clock ticks on wall time, and a batch run draws frames far faster
        /// than sixty a second, so two thousand frames were one second of game.
        /// </summary>
        static IEnumerator Frames(OdysseyBootstrap boot, float seconds)
        {
            int start = boot.World!.CurrentTick;
            int ticks = Mathf.CeilToInt(seconds * boot.ticksPerSecond);
            float deadline = Time.realtimeSinceStartup + seconds * 4f + 10f;
            while (boot.World.CurrentTick - start < ticks && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(boot.World.CurrentTick - start, Is.GreaterThanOrEqualTo(ticks),
                "the clock did not advance, so no game time passed");
        }
    }
}
