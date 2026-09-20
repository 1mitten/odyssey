#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.Ui;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// A portrait is taken once and kept for the session, so whatever is in the room when it is
    /// taken is in that colonist's picture for ever (<c>docs/design/20-avatars.md</c> §10.7).
    ///
    /// <para><b>The owner's report was that colonists generated later in a session had a black
    /// profile picture.</b> They were photographed after dark: the daylight cycle writes the
    /// global ambient, the fog and the sky, and every camera in the process reads them — so the
    /// same colonist measured a mean luminance of 80 of 255 at noon and 26 at midnight, and the
    /// 26 was the one that got cached. The studio owns its own light for the instant of the
    /// render; it now owns the rest of the environment for that instant too.</para>
    /// </summary>
    public class PortraitLightingTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        /// <summary>
        /// The hours to photograph at. Noon, the two ends of dusk and the dead of night, which is
        /// where the whole of the fault lived.
        /// </summary>
        static readonly int[] Hours = { 12, 18, 21, 22, 0, 3, 6 };

        /// <summary>
        /// How far apart the brightest and darkest picture of one colonist may be.
        ///
        /// <para>Not <c>Is.EqualTo</c>: this is a real render on a real device and a percent of
        /// drift is not a fault. Three per cent is a long way under the 3.1x the fault measured
        /// and a long way over anything a graphics driver will do.</para>
        /// </summary>
        const double Tolerance = 1.03;

        static void GiveItACatalogue(OdysseyBootstrap boot)
        {
#if UNITY_EDITOR
            if (boot.moduleCatalogue == null)
                boot.moduleCatalogue =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
#endif
        }

        /// <summary>
        /// A sun of the scene's own, because the play scene has one and the test rig does not.
        ///
        /// <para>Without a directional light the bootstrap builds no daylight cycle at all, and
        /// this test would report a perfectly steady portrait for entirely the wrong reason —
        /// which it did, once.</para>
        /// </summary>
        static void GiveItASun(GameObject root)
        {
            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(root.transform, false);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 2.05f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        /// <summary>
        /// One body and one set of colours, so the only thing that moves between shots is the
        /// clock. Asking for a pawn's portrait instead would ask for a different person in every
        /// run, because the setup page seeds the world at random — which made three successive
        /// readings of this look like three different answers.
        /// </summary>
        static readonly ColonistAppearance Sitter = new ColonistAppearance(
            3, Rgb24.FromHex(0xE0B088), Rgb24.FromHex(0x3B2A1E),
            Rgb24.FromHex(0x4A4F55), Rgb24.FromHex(0x2A2E33));

        [UnityTest]
        public IEnumerator APortraitIsTheSameAtMidnightAsAtNoon()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                GiveItACatalogue(boot);
                if (boot.moduleCatalogue == null)
                    Assert.Ignore("no module catalogue, so there is nobody to photograph");

                GiveItASun(root);
                for (int i = 0; i < 8; i++) yield return null;

                shell.Menu.Choose(SessionCommands.NewGameKey);
                for (int i = 0; i < 10; i++) yield return null;
                Assert.That(shell.Menu.Start(), Is.True, "Start built no world");
                for (int i = 0; i < 20; i++) yield return null;

                Assert.That(boot.World, Is.Not.Null, "no world");

                double darkest = double.MaxValue, brightest = 0;
                int hidden = -1;
                foreach (int hour in Hours)
                {
                    yield return RunTheClockTo(boot, hour);

                    boot.Portraits.Clear();
                    Texture2D? shot = boot.Portraits.For(Sitter);
                    Assert.That(shot, Is.Not.Null, $"no portrait at {hour}:00");

                    double luma = MeanLuma(shot!);
                    if (luma < darkest) darkest = luma;
                    if (luma > brightest) brightest = luma;
                    hidden = boot.Portraits.SunsHidden;
                }

                // The world's own sun really was in the way, so a pass is a pass for the right
                // reason rather than because there was nothing to take over.
                Assert.That(hidden, Is.GreaterThan(0),
                    "the studio switched off no scene light, so this proves nothing");

                Assert.That(darkest, Is.GreaterThan(1.0),
                    "every portrait came out black, whatever the hour");
                Assert.That(brightest / darkest, Is.LessThanOrEqualTo(Tolerance),
                    $"a portrait taken at midnight is not the one taken at noon: " +
                    $"{darkest:F1} to {brightest:F1} of 255");
            }
            finally { Object.Destroy(root); }
        }

        /// <summary>
        /// The other half of owning the environment: giving it back. A studio that left the
        /// ambient flat and the sun switched off would take one photograph and ruin the game.
        /// </summary>
        [UnityTest]
        public IEnumerator TheStudioPutsTheWorldBackAsItFoundIt()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                GiveItACatalogue(boot);
                if (boot.moduleCatalogue == null)
                    Assert.Ignore("no module catalogue, so there is nobody to photograph");

                GiveItASun(root);
                for (int i = 0; i < 8; i++) yield return null;

                shell.Menu.Choose(SessionCommands.NewGameKey);
                for (int i = 0; i < 10; i++) yield return null;
                Assert.That(shell.Menu.Start(), Is.True, "Start built no world");
                for (int i = 0; i < 20; i++) yield return null;

                yield return RunTheClockTo(boot, 12);

                AmbientMode mode = RenderSettings.ambientMode;
                Color sky = RenderSettings.ambientSkyColor;
                bool fog = RenderSettings.fog;
                float reflections = RenderSettings.reflectionIntensity;

                Light? sun = null;
                foreach (Light light in Resources.FindObjectsOfTypeAll<Light>())
                    if (light != null && light.name == "Sun") sun = light;
                Assert.That(sun, Is.Not.Null, "the rig lost its sun");
                Assert.That(sun!.enabled, Is.True, "the sun is out at noon");

                boot.Portraits.Clear();
                Assert.That(boot.Portraits.For(Sitter), Is.Not.Null);

                Assert.That(RenderSettings.ambientMode, Is.EqualTo(mode), "the ambient mode");
                Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(sky), "the ambient sky");
                Assert.That(RenderSettings.fog, Is.EqualTo(fog), "the fog");
                Assert.That(RenderSettings.reflectionIntensity, Is.EqualTo(reflections),
                    "the skybox reflection");
                Assert.That(sun.enabled, Is.True, "the studio left the world's sun switched off");
            }
            finally { Object.Destroy(root); }
        }

        /// <summary>Wind the simulation on to this hour of the day and let the cycle catch up.</summary>
        static IEnumerator RunTheClockTo(OdysseyBootstrap boot, int hour)
        {
            long now = boot.World!.CurrentTick;
            long want = now / GameClock.TicksPerDay * GameClock.TicksPerDay
                        + (long)hour * GameClock.TicksPerHour;
            while (want <= now) want += GameClock.TicksPerDay;
            while (boot.World.CurrentTick < want) boot.World.Tick();

            for (int i = 0; i < 3; i++) yield return null;
        }

        /// <summary>The mean brightness of the lit pixels, 0 to 255. Transparent ones are the
        /// background and would only measure how much of the frame the colonist fills.</summary>
        static double MeanLuma(Texture2D shot)
        {
            Color32[] pixels = shot.GetPixels32();
            long lit = 0;
            double luma = 0;
            for (int p = 0; p < pixels.Length; p++)
            {
                if (pixels[p].a <= 8) continue;
                lit++;
                luma += pixels[p].r * 0.299 + pixels[p].g * 0.587 + pixels[p].b * 0.114;
            }
            return lit == 0 ? 0 : luma / lit;
        }
    }
}
