#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The butterflies' model and palette (design 52): what the fast tier can answer without a
    /// frame to draw. The drawing — the wings, the halo and the light — is the Unity tier's
    /// (<c>ButterflyDirectorTests</c>) and the owner's.
    /// </summary>
    public class ButterflyTests
    {
        /// <summary>
        /// A meadow with a hard edge: grass over x in [0, 60) at 30 m up, rock beyond it at 33 m.
        /// Flowers on the grass where z is under 10. Every question is exact, so a test can say
        /// where each butterfly may and may not be.
        /// </summary>
        sealed class Field : IButterflyHabitat
        {
            public const float Ground = 30f, Rock = 33f, Edge = 60f;

            public int Asked;

            public bool Habitat(float x, float z, out float ground)
            {
                Asked++;
                ground = Ground;
                return x >= 0f && x < Edge && z >= -200f && z < 200f;
            }

            public float Surface(float x, float z) => x >= 0f && x < Edge ? Ground : Rock;

            public bool Flowers(float x, float z) => z < 10f;
        }

        const float FocusX = 30f, FocusZ = 0f;
        static readonly float Radius = ButterflyMeadow.ReferenceRadius;

        static ButterflyMeadow Warm(int rung, out Field field, int frames = 120, uint seed = 7,
            ButterflyConditions? sky = null)
        {
            field = new Field();
            var meadow = new ButterflyMeadow(seed, rung);
            ButterflyConditions conditions = sky ?? ButterflyConditions.ClearSpring;
            for (int f = 0; f < frames; f++)
                meadow.Step(1f / 60f, conditions, field, FocusX, FocusZ, Radius, ReadOnlySpan<float>.Empty);
            return meadow;
        }

        // ------------------------------------------------------------------ the model

        [Test]
        public void TheSameSeedGivesTheSameMeadow()
        {
            ButterflyMeadow a = Warm(200, out _, frames: 300);
            ButterflyMeadow b = Warm(200, out _, frames: 300);
            Assert.That(a.Live, Is.GreaterThan(0));
            Assert.That(b.Live, Is.EqualTo(a.Live));
            for (int i = 0; i < a.Capacity; i++)
            {
                Assert.That(b.StateAt(i), Is.EqualTo(a.StateAt(i)), $"butterfly {i}'s state");
                Assert.That(b.XAt(i), Is.EqualTo(a.XAt(i)), $"butterfly {i}'s x");
                Assert.That(b.YAt(i), Is.EqualTo(a.YAt(i)), $"butterfly {i}'s y");
            }
        }

        /// <summary>A paused world passes zero seconds, and nothing moves — not a wing, not a fade.</summary>
        [Test]
        public void APausedStepMovesNothing()
        {
            ButterflyMeadow meadow = Warm(200, out Field field, frames: 90);
            var x = new float[meadow.Capacity];
            var flap = new float[meadow.Capacity];
            for (int i = 0; i < meadow.Capacity; i++)
            {
                x[i] = meadow.XAt(i);
                flap[i] = meadow.FlapAt(i);
            }
            int live = meadow.Live;

            for (int f = 0; f < 30; f++)
                meadow.Step(0f, ButterflyConditions.ClearSpring, field, FocusX, FocusZ, Radius, ReadOnlySpan<float>.Empty);

            Assert.That(meadow.Live, Is.EqualTo(live));
            for (int i = 0; i < meadow.Capacity; i++)
            {
                Assert.That(meadow.XAt(i), Is.EqualTo(x[i]));
                Assert.That(meadow.FlapAt(i), Is.EqualTo(flap[i]));
            }
        }

        [Test]
        public void AWalkerCloseByPutsARestingButterflyUp()
        {
            ButterflyMeadow meadow = Warm(200, out Field field, frames: 2);
            int resting = -1;
            for (int i = 0; i < meadow.Capacity && resting < 0; i++)
                if (meadow.StateAt(i) == ButterflyState.Resting) resting = i;
            Assert.That(resting, Is.GreaterThanOrEqualTo(0), "a warm start deals its butterflies at rest");

            float x0 = meadow.XAt(resting), z0 = meadow.ZAt(resting);
            // A colonist a metre and a half away, standing on the same grass.
            float[] walker = { x0 + 1.5f, Field.Ground, z0 };
            bool fled = false;
            for (int f = 0; f < 8 && !fled; f++)
            {
                meadow.Step(1f / 60f, ButterflyConditions.ClearSpring, field, FocusX, FocusZ, Radius, walker);
                fled = meadow.StateAt(resting) == ButterflyState.Fleeing;
            }
            Assert.That(fled, Is.True, "a walker 1.5 m away did not startle a resting butterfly within eight frames");

            float before = Distance(x0, z0, walker[0], walker[2]);
            for (int f = 0; f < 30; f++)
                meadow.Step(1f / 60f, ButterflyConditions.ClearSpring, field, FocusX, FocusZ, Radius, walker);
            float after = Distance(meadow.XAt(resting), meadow.ZAt(resting), walker[0], walker[2]);
            Assert.That(after, Is.GreaterThan(before + 0.5f), "a startled butterfly went towards the walker");
            Assert.That(meadow.YAt(resting), Is.GreaterThan(Field.Ground + ButterflyMeadow.RestHeight),
                "a startled butterfly did not climb");
        }

        [Test]
        public void AWalkerFarOffStartlesNobody()
        {
            ButterflyMeadow meadow = Warm(200, out Field field, frames: 2);
            float[] walker = { -500f, Field.Ground, -500f };
            for (int f = 0; f < 60; f++)
                meadow.Step(1f / 60f, ButterflyConditions.ClearSpring, field, FocusX, FocusZ, Radius, walker);
            for (int i = 0; i < meadow.Capacity; i++)
                Assert.That(meadow.StateAt(i), Is.Not.EqualTo(ButterflyState.Fleeing));
        }

        /// <summary>Every butterfly is dealt on the grass, never on the rock beside it.</summary>
        [Test]
        public void NoneIsDealtOffTheGrass()
        {
            ButterflyMeadow meadow = Warm(500, out _, frames: 1);
            Assert.That(meadow.Live, Is.GreaterThan(0));
            for (int i = 0; i < meadow.Capacity; i++)
            {
                if (meadow.StateAt(i) == ButterflyState.Empty) continue;
                Assert.That(meadow.XAt(i), Is.InRange(0f, Field.Edge), $"butterfly {i} was dealt off the grass");
                Assert.That(meadow.YAt(i), Is.EqualTo(Field.Ground + ButterflyMeadow.RestHeight).Within(1e-4f));
            }
        }

        /// <summary>After a minute of flying, nobody is over the rock for long, and nobody is in it.</summary>
        [Test]
        public void TheyKeepToTheGrassAndAboveTheGround()
        {
            ButterflyMeadow meadow = Warm(300, out _, frames: 60 * 60);
            int live = 0, over = 0;
            for (int i = 0; i < meadow.Capacity; i++)
            {
                if (meadow.StateAt(i) == ButterflyState.Empty) continue;
                live++;
                float floor = meadow.XAt(i) >= 0f && meadow.XAt(i) < Field.Edge ? Field.Ground : Field.Rock;
                Assert.That(meadow.YAt(i), Is.GreaterThanOrEqualTo(floor + ButterflyMeadow.Clearance - 1e-3f),
                    $"butterfly {i} is inside the ground");
                if (meadow.XAt(i) >= Field.Edge) over++;
            }
            Assert.That(live, Is.GreaterThan(0));
            Assert.That(over, Is.LessThanOrEqualTo(live / 10),
                $"{over} of {live} butterflies are over the rock after a minute");
        }

        [Test]
        public void NeverMoreThanTheRung()
        {
            foreach (int rung in new[] { 0, 1, 80, 200, 500 })
            {
                ButterflyMeadow meadow = Warm(rung, out _, frames: 200);
                Assert.That(meadow.Live, Is.LessThanOrEqualTo(rung), $"rung {rung}");
                Assert.That(meadow.Target, Is.LessThanOrEqualTo(rung), $"rung {rung}");
            }
        }

        /// <summary>
        /// The window here is half grass, so the meadow holds about half the rung — and half again
        /// when the window is a quarter the reference area. Not more than the rung ever, and not zero.
        /// </summary>
        [Test]
        public void TheCountFollowsTheGrassAndTheZoom()
        {
            // Focused on the field's edge, so half the window is grass and half is rock.
            var field = new Field();
            var meadow = new ButterflyMeadow(7, 200);
            for (int f = 0; f < 10; f++)
                meadow.Step(1f / 60f, ButterflyConditions.ClearSpring, field, Field.Edge, FocusZ, Radius,
                    ReadOnlySpan<float>.Empty);
            Assert.That(meadow.Share, Is.InRange(0.35f, 0.65f), "the field is about half the window");
            Assert.That(meadow.Target, Is.InRange(70, 130));

            Assert.That(ButterflyMeadow.TargetFor(200, 1f, ButterflyMeadow.ReferenceRadius, 1f, 1f), Is.EqualTo(200));
            Assert.That(ButterflyMeadow.TargetFor(200, 1f, ButterflyMeadow.ReferenceRadius * 0.5f, 1f, 1f), Is.EqualTo(50));
            Assert.That(ButterflyMeadow.TargetFor(200, 1f, ButterflyMeadow.ReferenceRadius * 2f, 1f, 1f), Is.EqualTo(200),
                "zoomed out, the rung is the cap: the same count over more ground");
        }

        [Test]
        public void NoneInRime()
        {
            long hollow = Calendar.TicksPerMonth * 4 + Calendar.TicksPerDay * 6;
            var sky = new ButterflyConditions(hollow, 0f, 0f, 0f, 0f);
            ButterflyMeadow meadow = Warm(500, out _, frames: 60, sky: sky);
            Assert.That(meadow.Target, Is.EqualTo(0));
            Assert.That(meadow.Live, Is.EqualTo(0));
        }

        [Test]
        public void RainSendsThemAway()
        {
            ButterflyMeadow meadow = Warm(200, out Field field, frames: 60);
            Assert.That(meadow.Live, Is.GreaterThan(0));
            var rain = new ButterflyConditions(ButterflyConditions.ClearSpring.Tick, 1f, 0.4f, 0f, 0f);
            for (int f = 0; f < 60 * 20; f++)
                meadow.Step(1f / 60f, rain, field, FocusX, FocusZ, Radius, ReadOnlySpan<float>.Empty);
            Assert.That(meadow.Target, Is.EqualTo(0));
            Assert.That(meadow.Live, Is.EqualTo(0), "twenty seconds of rain left butterflies out");

            for (int f = 0; f < 60 * 5; f++)
                meadow.Step(1f / 60f, ButterflyConditions.ClearSpring, field, FocusX, FocusZ, Radius, ReadOnlySpan<float>.Empty);
            Assert.That(meadow.Live, Is.GreaterThan(0), "they did not come back when the rain stopped");
        }

        [Test]
        public void TheSeasonThinsThroughGlareAndIsNoneInRime()
        {
            float previous = ButterflyMeadow.SeasonFor(0);
            long year = Calendar.TicksPerMonth * 6L;
            for (long tick = 0; tick < year; tick += Calendar.TicksPerHour)
            {
                float season = ButterflyMeadow.SeasonFor(tick);
                Assert.That(season, Is.InRange(0f, 1f));
                if (Calendar.SeasonOfYear(tick) == 2) Assert.That(season, Is.EqualTo(0f), "a butterfly in Rime");
                else if (Calendar.SeasonOfYear(tick - Calendar.TicksPerHour < 0 ? 0 : tick - Calendar.TicksPerHour) != 2)
                    Assert.That(Math.Abs(season - previous), Is.LessThan(0.02f),
                        $"the season jumps at tick {tick}: a month boundary is a cliff");
                previous = season;
            }
            Assert.That(ButterflyMeadow.SeasonFor(Calendar.TicksPerMonth + Calendar.TicksPerMonth / 2), Is.EqualTo(1f),
                "the middle of Tansy is the fullest meadow");
        }

        [Test]
        public void CloudThinsThemAndRainEndsThem()
        {
            Assert.That(ButterflyMeadow.WeatherFor(0f, 0f), Is.EqualTo(1f));
            Assert.That(ButterflyMeadow.WeatherFor(1f, 0f), Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(ButterflyMeadow.WeatherFor(0f, ButterflyMeadow.RainGone), Is.EqualTo(0f));
            Assert.That(ButterflyMeadow.WeatherFor(0f, 1f), Is.EqualTo(0f));
        }

        /// <summary>A pan leaves the old window's butterflies to fade and deals new ones under the new focus.</summary>
        [Test]
        public void TheMeadowFollowsTheFocus()
        {
            var field = new WideField();
            var meadow = new ButterflyMeadow(3, 200);
            for (int f = 0; f < 60; f++)
                meadow.Step(1f / 60f, ButterflyConditions.ClearSpring, field, 0f, 0f, Radius, ReadOnlySpan<float>.Empty);
            for (int f = 0; f < 60 * 3; f++)
                meadow.Step(1f / 60f, ButterflyConditions.ClearSpring, field, 300f, 0f, Radius, ReadOnlySpan<float>.Empty);

            Assert.That(meadow.Live, Is.GreaterThan(meadow.Target / 2));
            for (int i = 0; i < meadow.Capacity; i++)
            {
                if (meadow.StateAt(i) == ButterflyState.Empty) continue;
                Assert.That(Distance(meadow.XAt(i), meadow.ZAt(i), 300f, 0f),
                    Is.LessThanOrEqualTo(Radius * ButterflyMeadow.StrayFactor + 2f),
                    $"butterfly {i} is still at the old focus three seconds after the pan");
            }
        }

        sealed class WideField : IButterflyHabitat
        {
            public bool Habitat(float x, float z, out float ground)
            {
                ground = 0f;
                return true;
            }

            public float Surface(float x, float z) => 0f;
            public bool Flowers(float x, float z) => false;
        }

        [Test]
        public void TheyLandAndTakeOffAgain()
        {
            ButterflyMeadow meadow = Warm(200, out _, frames: 60 * 40);
            int flying = 0, resting = 0, landing = 0;
            for (int i = 0; i < meadow.Capacity; i++)
            {
                switch (meadow.StateAt(i))
                {
                    case ButterflyState.Flying: flying++; break;
                    case ButterflyState.Resting: resting++; break;
                    case ButterflyState.Landing: landing++; break;
                }
            }
            TestContext.WriteLine($"after forty seconds: {flying} flying, {landing} landing, {resting} resting");
            Assert.That(flying, Is.GreaterThan(0), "nobody took off");
            Assert.That(resting + landing, Is.GreaterThan(0), "nobody came down again");
        }

        [Test]
        public void AResizeKeepsWhoFits()
        {
            ButterflyMeadow meadow = Warm(200, out Field field, frames: 30);
            meadow.Resize(20);
            Assert.That(meadow.Capacity, Is.EqualTo(20));
            Assert.That(meadow.Live, Is.LessThanOrEqualTo(20));
            meadow.Step(1f / 60f, ButterflyConditions.ClearSpring, field, FocusX, FocusZ, Radius, ReadOnlySpan<float>.Empty);
            meadow.Resize(0);
            Assert.That(meadow.Live, Is.EqualTo(0));
        }

        [Test]
        public void AStepAllocatesNothingOnceWarm()
        {
            ButterflyMeadow meadow = Warm(500, out Field field, frames: 120);
            float[] walkers = { 30f, Field.Ground, 0f, 40f, Field.Ground, 5f };
            var packed = new float[meadow.Capacity * ButterflyMeadow.PackedFloats];
            for (int f = 0; f < 10; f++)
            {
                meadow.Step(1f / 60f, ButterflyConditions.ClearSpring, field, FocusX, FocusZ, Radius, walkers);
                meadow.Pack(packed, 0f, 1000f);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int f = 0; f < 100; f++)
            {
                meadow.Step(1f / 60f, ButterflyConditions.ClearSpring, field, FocusX, FocusZ, Radius, walkers);
                meadow.Pack(packed, 0f, 1000f);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            TestContext.WriteLine($"a hundred frames of 500 butterflies allocated {after - before} bytes");
            Assert.That(after - before, Is.LessThan(1024));
        }

        [Test]
        public void PackDrawsOnlyTheLayersTheSliceDraws()
        {
            ButterflyMeadow meadow = Warm(200, out _, frames: 30);
            var packed = new float[meadow.Capacity * ButterflyMeadow.PackedFloats];
            int all = meadow.Pack(packed, 0f, 1000f);
            Assert.That(all, Is.GreaterThan(0));
            Assert.That(meadow.Pack(packed, Field.Ground + 3f, 1000f), Is.EqualTo(0),
                "a slice that starts above the meadow's ground drew its butterflies");
            Assert.That(meadow.Pack(packed, 0f, Field.Ground), Is.EqualTo(0),
                "a slice that ends below the meadow's ground drew its butterflies");

            // The layout the shader reads: position, then the ground, then a seed that is a whole number.
            Assert.That(packed[4], Is.EqualTo(Field.Ground));
            Assert.That(packed[8], Is.EqualTo(Math.Floor(packed[8])));
            Assert.That(packed[11], Is.InRange(ButterflyMeadow.BeatSlowest, ButterflyMeadow.BeatQuickest));
        }

        [Test]
        public void TheWingbeatNeverStrobes() =>
            Assert.That(ButterflyMeadow.BeatQuickest, Is.LessThanOrEqualTo(10f),
                "over ten beats a second a wing strobes at sixty frames (e-13 §13)");

        // ------------------------------------------------------------------ the palette

        [Test]
        public void TheGlowWeightsSumToOne()
        {
            float sum = 0f;
            foreach (ButterflyPalette.Glow glow in ButterflyPalette.Glows) sum += glow.Weight;
            Assert.That(sum, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(ButterflyPalette.GlowFor(0f), Is.EqualTo(0));
            Assert.That(ButterflyPalette.GlowFor(0.9999f), Is.EqualTo(ButterflyPalette.Glows.Length - 1));
        }

        [Test]
        public void TheGlowRisesWithTheNight()
        {
            Assert.That(ButterflyPalette.NightFor(62f), Is.EqualTo(0f), "noon");
            Assert.That(ButterflyPalette.NightFor(ButterflyPalette.GlowStartsAt), Is.EqualTo(0f));
            Assert.That(ButterflyPalette.NightFor(-12f), Is.EqualTo(1f), "the night key");
            float dusk = ButterflyPalette.NightFor(-2f);
            Assert.That(dusk, Is.GreaterThan(0f).And.LessThan(1f), "the glow switches rather than rising");
        }

        /// <summary>
        /// Owner, 2026-09-25: "varying colours". e-14 §6: under deuteranopia cyan greys and violet and
        /// magenta fold towards the night's blue, so a glow told apart by hue alone is one glow to
        /// that player. Every pair is held apart in all four views, each at the gain it is drawn with,
        /// and every glow is held apart from the night sky itself. The same simulation as
        /// <c>StorageThemeTests</c> (Machado, Oliveira and Fernandes 2009, full severity).
        /// </summary>
        [Test]
        public void NoTwoGlowsLookAlikeToAColourBlindPlayer()
        {
            ButterflyPalette.Glow[] glows = ButterflyPalette.Glows;
            double[] night = { 0.13, 0.17, 0.30 };   // the night key's sky ambient (Daylight), linear
            foreach ((string name, double[,]? m) in Visions)
            {
                for (int a = 0; a < glows.Length; a++)
                {
                    double[] ga = Seen(glows[a], m);
                    double fromNight = DeltaE(ga, Apply(night, m));
                    Assert.That(fromNight, Is.GreaterThanOrEqualTo(25.0),
                        $"under {name}, the {glows[a].Name} glow is {fromNight:0.0} Lab units from the night sky");
                    for (int b = a + 1; b < glows.Length; b++)
                    {
                        double distance = DeltaE(ga, Seen(glows[b], m));
                        Assert.That(distance, Is.GreaterThanOrEqualTo(14.0),
                            $"under {name}, the {glows[a].Name} and {glows[b].Name} glows are {distance:0.0} " +
                            "Lab units apart: one colour to that player");
                    }
                }
            }
        }

        [Test]
        public void EverySpeciesIsDrawable()
        {
            Assert.That(ButterflyPalette.All, Has.Length.EqualTo(6), "the shader's species array is sized for six");
            foreach (ButterflyPalette.Species s in ButterflyPalette.All)
            {
                Assert.That(s.Veins, Is.InRange(0f, 0.25f), s.Name);
                Assert.That(s.Margin, Is.InRange(0f, 0.3f), s.Name);
                Assert.That(s.Eyespots, Is.InRange(0f, 1f), s.Name);
                // Is.AnyOf is newer than the NUnit Unity ships; the two tiers do not run the same one.
                Assert.That(s.Band == 0f || s.Band == 1f || s.Band == 2f, Is.True, $"{s.Name}'s band is {s.Band}");
            }
        }

        [Test]
        public void TheLitWingStaysUnderTheBloomThreshold()
        {
            // d-24 §7: a few-pixel wing over 1.1 shimmers as it moves.
            Assert.That(ButterflyPalette.WingGlowCeiling, Is.LessThanOrEqualTo(1.0f));
            Assert.That(ButterflyPalette.PulseFloor, Is.GreaterThanOrEqualTo(0.55f));
        }

        // ------------------------------------------------------------------ helpers

        static float Distance(float ax, float az, float bx, float bz) =>
            (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        static readonly (string, double[,]?)[] Visions =
        {
            ("normal vision", null),
            ("protanopia", new[,] { { 0.152286, 1.052583, -0.204868 }, { 0.114503, 0.786281, 0.099216 }, { -0.003882, -0.048116, 1.051998 } }),
            ("deuteranopia", new[,] { { 0.367322, 0.860646, -0.227968 }, { 0.280085, 0.672501, 0.047413 }, { -0.011820, 0.042940, 0.968881 } }),
            ("tritanopia", new[,] { { 1.255528, -0.076749, -0.178779 }, { -0.078411, 0.930809, 0.147602 }, { 0.004733, 0.691367, 0.303900 } }),
        };

        /// <summary>A glow as that eye sees it at the gain it is drawn with, in linear RGB, clamped.</summary>
        static double[] Seen(ButterflyPalette.Glow glow, double[,]? m)
        {
            double[] l =
            {
                Math.Min(1.0, ButterflyPalette.Linear(glow.Colour.R) * glow.Gain),
                Math.Min(1.0, ButterflyPalette.Linear(glow.Colour.G) * glow.Gain),
                Math.Min(1.0, ButterflyPalette.Linear(glow.Colour.B) * glow.Gain),
            };
            return Apply(l, m);
        }

        static double[] Apply(double[] l, double[,]? m)
        {
            if (m == null) return l;
            var o = new double[3];
            for (int r = 0; r < 3; r++)
                o[r] = Math.Clamp(m[r, 0] * l[0] + m[r, 1] * l[1] + m[r, 2] * l[2], 0.0, 1.0);
            return o;
        }

        static double DeltaE(double[] a, double[] b)
        {
            double[] la = Lab(a), lb = Lab(b);
            return Math.Sqrt((la[0] - lb[0]) * (la[0] - lb[0]) + (la[1] - lb[1]) * (la[1] - lb[1])
                             + (la[2] - lb[2]) * (la[2] - lb[2]));
        }

        static double[] Lab(double[] l)
        {
            double x = (0.4124 * l[0] + 0.3576 * l[1] + 0.1805 * l[2]) / 0.95047;
            double y = 0.2126 * l[0] + 0.7152 * l[1] + 0.0722 * l[2];
            double z = (0.0193 * l[0] + 0.1192 * l[1] + 0.9505 * l[2]) / 1.08883;
            static double F(double t) => t > 0.008856 ? Math.Pow(t, 1.0 / 3.0) : 7.787 * t + 16.0 / 116.0;
            return new[] { 116 * F(y) - 16, 500 * (F(x) - F(y)), 200 * (F(y) - F(z)) };
        }
    }
}
