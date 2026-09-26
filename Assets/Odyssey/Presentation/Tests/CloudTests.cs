#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using UnityEngine;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// The clouds (design 63): what colour a cloud is at each hour, and what the director submits.
    /// The colours need no art; the director's tests ignore themselves where the Meadow cloud art
    /// did not resolve, which is every machine without the packs, the CI runner included.
    /// </summary>
    public class CloudTests
    {
        // ------------------------------------------------------------------ colour

        static DaylightState At(float hour) => Daylight.Meadow(Daylight.Sample(hour));

        [Test]
        public void NoonTopsAreNearlyWhite()
        {
            Color top = CloudColours.For(At(13f), 0f).Top;
            Assert.That(CloudColours.Luma(top), Is.GreaterThan(0.8f), $"top {top}");
            float spread = Mathf.Max(top.r, Mathf.Max(top.g, top.b)) - Mathf.Min(top.r, Mathf.Min(top.g, top.b));
            Assert.That(spread, Is.LessThan(0.15f), $"a noon cloud should be white, not tinted: {top}");
        }

        [Test]
        public void DuskTopsAreWarm()
        {
            Color top = CloudColours.For(At(19f), 0f).Top;
            Assert.That(top.r, Is.GreaterThan(top.b + 0.1f), $"top {top}");
        }

        [Test]
        public void ANightTopIsMoonlightNotABlueLamp()
        {
            // The first night sheet drew the clouds a pure saturated blue (design 63 §4b). The
            // clouds are gone by full night now, but these are the colours they fade through.
            Color top = CloudColours.For(At(20.5f), 0f).Top;
            Assert.That(top.b / Mathf.Max(0.01f, top.r), Is.LessThan(2.5f), $"top {top}");
        }

        [Test]
        public void WithNoPresenceEveryColourIsTheHorizonTheyHaveSunkTo(
            [Values(13f, 20.5f, 23f)] float hour, [Values(0f, 1f)] float gloom)
        {
            DaylightState s = Overcast.Grade(At(hour), gloom, gloom);
            Color sky = s.Horizon;
            CloudColourSet set = CloudColours.For(s, gloom, gloom, presence: 0f);
            foreach (Color c in new[] { set.Top, set.Under, set.Rim })
                Assert.That(Vector4.Distance(new Vector4(c.r, c.g, c.b, 1f), new Vector4(sky.r, sky.g, sky.b, 1f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void AStormsUndersideIsDarkerThanTheSkyBehindIt([Values(10f, 13f, 17f)] float hour)
        {
            // Owner, 2026-09-26: dark slate, darker than the sky. The first storm sheet was lighter.
            DaylightState s = Overcast.Grade(At(hour), 1f, 1f);
            Color under = CloudColours.For(s, 1f, 1f).Under;
            Assert.That(CloudColours.Luma(under), Is.LessThan(0.85f * CloudColours.Luma(CloudColours.SkyAt(s))),
                $"under {under} against sky {CloudColours.SkyAt(s)}");
        }

        [Test]
        public void RainDarkensTheUndersideAndKeepsItsColour()
        {
            // Owner, 2026-09-25: rain keeps a clear day's colour; 2026-09-26: its clouds are heavier.
            DaylightState s = Overcast.Grade(At(13f), 0.56f, 0f);
            Color dry = CloudColours.For(s, 0f, 0f).Under, wet = CloudColours.For(s, 0f, 1f).Under;
            Assert.That(CloudColours.Luma(wet), Is.LessThan(0.9f * CloudColours.Luma(dry)), $"dry {dry} wet {wet}");
            Assert.That(Chroma(wet), Is.GreaterThan(0.7f * Chroma(dry)), $"rain drained the colour: dry {dry} wet {wet}");
        }

        static float Chroma(Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return max <= 0f ? 0f : (max - min) / max;
        }

        [Test]
        public void AnUndersideIsNeverBrighterThanItsTop(
            [Values(0f, 0.5f, 1f)] float cover, [Values(0f, 0.5f, 1f)] float gloom)
        {
            for (float hour = 0f; hour < 24f; hour += 0.25f)
            {
                DaylightState s = Overcast.Grade(At(hour), cover, gloom);
                CloudColourSet set = CloudColours.For(s, gloom);
                Assert.That(CloudColours.Luma(set.Under), Is.LessThanOrEqualTo(CloudColours.Luma(set.Top) + 1e-4f),
                    $"at {hour:0.00}h cover {cover} gloom {gloom}: top {set.Top} under {set.Under}");
            }
        }

        [Test]
        public void EveryColourIsOpaqueAndInRange()
        {
            for (float hour = 0f; hour < 24f; hour += 0.25f)
            foreach (float gloom in new[] { 0f, 1f })
            {
                CloudColourSet set = CloudColours.For(Overcast.Grade(At(hour), gloom, gloom), gloom);
                foreach (Color c in new[] { set.Top, set.Under, set.Rim })
                {
                    Assert.That(c.a, Is.EqualTo(1f));
                    Assert.That(Mathf.Min(c.r, Mathf.Min(c.g, c.b)), Is.GreaterThanOrEqualTo(0f), $"{c} at {hour}h");
                }
            }
        }

        [Test]
        public void TheStormDarkensTheUndersideHardestOfAll()
        {
            DaylightState noon = At(13f);
            CloudColourSet calm = CloudColours.For(noon, 0f), storm = CloudColours.For(noon, 1f);
            float under = CloudColours.Luma(storm.Under) / CloudColours.Luma(calm.Under);
            float top = CloudColours.Luma(storm.Top) / CloudColours.Luma(calm.Top);
            Assert.That(under, Is.LessThan(top), "a storm deck should read heavy, not as more of the same grey");
        }

        // ------------------------------------------------------------------ the director

        static readonly GridSize Size = new GridSize(8, 8, 16);

        sealed class Rig : System.IDisposable
        {
            public readonly CloudDirector Clouds;
            public readonly Camera Camera;
            public readonly MeadowLook Look;
            readonly ModuleLibrary _library;
            readonly GameObject _host;

            public Rig(MeadowLook look)
            {
                Look = look;
                var catalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(
                    "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
                _library = new ModuleLibrary(catalogue);
                Clouds = new CloudDirector(new WorldRenderModel(Size, new ChunkGrid(Size), _library), look);
                _host = new GameObject("CloudTests");
                Camera = _host.AddComponent<Camera>();
                Camera.aspect = 16f / 9f;
            }

            public float BoardTop => Size.SizeY * CellMetrics.SizeY;

            /// <summary>The colony camera at its usual 48 degrees, 150 m up.</summary>
            public void Colony()
            {
                Camera.fieldOfView = 40f;
                Camera.transform.SetPositionAndRotation(new Vector3(10f, BoardTop + 100f, 10f), Quaternion.Euler(48f, 0f, 0f));
            }

            /// <summary>A colonist's eye on top of the board, looking a little down, through the ride camera's lens.</summary>
            public void Eye()
            {
                Camera.fieldOfView = RideCamera.FieldOfView;
                Camera.transform.SetPositionAndRotation(new Vector3(10f, BoardTop + 2f, 10f), Quaternion.Euler(4f, 0f, 0f));
            }

            public void Dispose()
            {
                Clouds.Dispose();
                _library.Dispose();
                Object.DestroyImmediate(_host);
            }
        }

        static MeadowLook Art()
        {
            MeadowLook? look = MeadowLook.Loaded;
            if (look == null || !look.HasClouds) Assert.Ignore("the Meadow cloud art did not resolve on this machine");
            return look!;
        }

        [Test]
        public void WithoutTheArtThereIsNothingToDraw()
        {
            var catalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(
                "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
            using var library = new ModuleLibrary(catalogue);
            using var clouds = new CloudDirector(new WorldRenderModel(Size, new ChunkGrid(Size), library), null);
            var host = new GameObject("CloudTests");
            try
            {
                Assert.That(clouds.Available, Is.False);
                clouds.Sync(host.AddComponent<Camera>(), 1f, 0f, 0f, 0f, 0f, 1f, null, false);
                Assert.That(clouds.LastDrawCalls, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ThePacksMaterialIsCopiedAndNeverWritten()
        {
            using var rig = new Rig(Art());
            Material asset = rig.Look.clouds!;
            Color before = asset.GetColor("_Top_Color");
            rig.Eye();
            rig.Clouds.Sync(rig.Camera, 1f, 0f, 1f, 1f, 0f, 1f, null, false);

            Assert.That(rig.Clouds.Material, Is.Not.SameAs(asset));
            Assert.That(asset.GetColor("_Top_Color"), Is.EqualTo(before), "the licensed material on disk was edited");
        }

        [Test]
        public void TheCopyTurnsOffWhatTheDemoNeededAndOurSkyDoesNot()
        {
            using var rig = new Rig(Art());
            Material m = rig.Clouds.Material!;
            Assert.That(m.renderQueue, Is.EqualTo(CloudDirector.RenderQueue), "drawn after the board's opaque geometry");
            Assert.That(m.GetFloat("_Cloud_Speed"), Is.Zero, "the pack billows on the wall clock");
            Assert.That(m.GetFloat("_Enable_Scattering"), Is.Zero, "its scattering turns a noon cloud lilac");
            Assert.That(m.GetFloat("_Enable_Fog"), Is.Zero, "its fog overshoots in our haze");
            Assert.That(m.GetVector("_Light_Direction_Override"), Is.EqualTo(Vector4.zero));
        }

        [Test]
        public void TheColonyCameraAtItsUsualPitchSubmitsNothing()
        {
            using var rig = new Rig(Art());
            rig.Colony();
            rig.Clouds.Sync(rig.Camera, 1f, 0f, 0.5f, 0f, 0f, 1f, null, false);
            Assert.That(rig.Clouds.LastVisible, Is.False);
            Assert.That(rig.Clouds.LastDrawCalls, Is.Zero);
        }

        [Test]
        public void AnEyeOnTheHorizonSubmitsEachRingOnce()
        {
            using var rig = new Rig(Art());
            rig.Eye();
            rig.Clouds.Sync(rig.Camera, 1f, 0f, 0.5f, 0f, 0f, 1f, null, false);
            Assert.That(rig.Clouds.LastVisible, Is.True);
            Assert.That(rig.Clouds.LastDrawCalls, Is.EqualTo(rig.Look.cloudRingHigh != null ? 2 : 1));
        }

        [Test]
        public void BelowTheSurfaceNothingIsSubmitted()
        {
            using var rig = new Rig(Art());
            rig.Eye();
            rig.Clouds.Sync(rig.Camera, 1f, 0f, 0.5f, 0f, 0f, 1f, null, underground: true);
            Assert.That(rig.Clouds.LastDrawCalls, Is.Zero);
        }

        [Test]
        public void OffDrawsNothingAndHoldsTheSky()
        {
            using var rig = new Rig(Art());
            rig.Eye();
            rig.Clouds.Enabled = false;
            rig.Clouds.Sync(rig.Camera, 100f, 0f, 0.5f, 0f, 0f, 1f, null, false);
            Assert.That(rig.Clouds.LastDrawCalls, Is.Zero);
            Assert.That(rig.Clouds.LowYaw, Is.Zero);
        }

        [Test]
        public void APausedWorldHoldsTheDriftAndARunningOneMovesIt()
        {
            using var rig = new Rig(Art());
            rig.Eye();
            rig.Clouds.Sync(rig.Camera, 0f, 0f, 0.5f, 0f, 0f, 1f, null, false);
            Assert.That(rig.Clouds.LowYaw, Is.Zero);
            rig.Clouds.Sync(rig.Camera, 20f, 0f, 0.5f, 0f, 0f, 1f, null, false);
            Assert.That(rig.Clouds.LowYaw, Is.EqualTo(20f * CloudDeck.DriftDegreesPerSecond).Within(1e-4f));
        }

        [Test]
        public void ByNightNothingIsSubmittedAndByDayBothRingsAre()
        {
            using var rig = new Rig(Art());
            rig.Eye();
            var sunObject = new GameObject("CloudTestsSun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            // The daylight writes the scene's ambient and fog; they are handed back after.
            var ambientMode = RenderSettings.ambientMode;
            Color sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor,
                ground = RenderSettings.ambientGroundColor, fogColour = RenderSettings.fogColor;
            bool fog = RenderSettings.fog;
            FogMode fogMode = RenderSettings.fogMode;
            float fogDensity = RenderSettings.fogDensity;
            var daylight = new DaylightDirector(sun, null);
            try
            {
                daylight.ApplyHour(23f);
                rig.Clouds.Sync(rig.Camera, 0f, 0f, 0f, 0f, 0f, 1f, daylight, false);
                Assert.That(rig.Clouds.LastPresence, Is.Zero);
                Assert.That(rig.Clouds.LastDrawCalls, Is.Zero, "a night sky should cost nothing");

                // Each jump in the hour is given the real seconds a whole fade takes, so it arrives.
                float settle = CloudDeck.JumpFadeSeconds;
                daylight.ApplyHour(20.25f);
                rig.Clouds.Sync(rig.Camera, 0f, settle, 0f, 0f, 0f, 1f, daylight, false);
                Assert.That(rig.Clouds.LastPresence, Is.InRange(0.01f, 0.99f), "the fade");
                Assert.That(rig.Clouds.LastDrawCalls, Is.GreaterThan(0));

                daylight.ApplyHour(13f);
                rig.Clouds.Sync(rig.Camera, 0f, settle, 0f, 0f, 0f, 1f, daylight, false);
                Assert.That(rig.Clouds.LastPresence, Is.EqualTo(1f));
                Assert.That(rig.Clouds.LastDrawCalls, Is.EqualTo(rig.Look.cloudRingHigh != null ? 2 : 1));

                // Owner, 2026-09-26: a jump from night to day must fade in, not snap.
                daylight.ApplyHour(23f);
                rig.Clouds.Sync(rig.Camera, 0f, settle, 0f, 0f, 0f, 1f, daylight, false);
                Assert.That(rig.Clouds.LastPresence, Is.Zero);
                daylight.ApplyHour(13f);
                rig.Clouds.Sync(rig.Camera, 0f, 1f, 0f, 0f, 0f, 1f, daylight, false);
                Assert.That(rig.Clouds.LastPresence, Is.EqualTo(1f / CloudDeck.JumpFadeSeconds).Within(1e-4f),
                    "a second after the jump, a quarter of the way in");
                Color partway = rig.Clouds.Material!.GetColor("_Top_Color");
                rig.Clouds.Sync(rig.Camera, 0f, 1f, 0f, 0f, 0f, 1f, daylight, false);
                Assert.That(rig.Clouds.Material.GetColor("_Top_Color"), Is.Not.EqualTo(partway),
                    "the colours follow the fade between the daylight's own writes");
            }
            finally
            {
                daylight.Dispose();
                Object.DestroyImmediate(sunObject);
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground;
                RenderSettings.fog = fog;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogDensity = fogDensity;
                RenderSettings.fogColor = fogColour;
            }
        }

        [Test]
        public void WithoutADayCycleTheCloudsAreColouredByTheBakedHourAndTheSky()
        {
            using var rig = new Rig(Art());
            rig.Eye();
            rig.Clouds.Sync(rig.Camera, 0f, 0f, 0f, 0f, 0f, 1f, null, false);
            Color clear = rig.Clouds.Material!.GetColor("_Top_Color");
            Color expected = CloudColours.For(Overcast.Grade(CloudDirector.BakedHour(), 0f, 0f), 0f).Top;
            Assert.That(Vector4.Distance(clear, expected), Is.LessThan(1e-3f));

            rig.Clouds.Sync(rig.Camera, 0f, 0f, 1f, 1f, 0f, 1f, null, false);
            Color storm = rig.Clouds.Material.GetColor("_Top_Color");
            Assert.That(CloudColours.Luma(storm), Is.LessThan(CloudColours.Luma(clear)), "the storm did not reach the clouds");
        }
    }
}
