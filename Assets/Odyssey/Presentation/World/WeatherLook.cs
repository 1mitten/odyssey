#nullable enable
using System;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// The weather as the player sees it: the simulation's sky (<see cref="WeatherView"/>,
    /// design 43) turned into light, rain and wet ground each frame.
    ///
    /// <para><b>One object so the bootstrap gains four lines, not forty.</b> It owns the cover map,
    /// the GPU rain, the particle control arm and the grey volume, and it moves the daylight's
    /// cloud term; the bootstrap builds it, calls <see cref="Sync"/> once a frame and disposes it
    /// with the session.</para>
    ///
    /// <para><b>The sky is the simulation's; the ground is the drawing's.</b> The weather system
    /// blends one spell into the next and publishes the terms; the ground here wets over seconds
    /// and dries more slowly than it wets, which is drawing and not saved. All of it runs on game
    /// time read from the tick: paused, the sky holds; at speed 3 it moves three times as fast.</para>
    /// </summary>
    public sealed class WeatherLook : IDisposable
    {
        /// <summary>How fast cloud and rain close on their target, per game second.</summary>
        public const float SkyRate = 0.5f;

        /// <summary>How fast the ground wets, per game second, and how fast it dries.</summary>
        public const float WetRate = 0.12f, DryRate = 0.04f;

        readonly SkyHeightMap _sky;
        readonly RainDirector _rain;
        readonly RainParticles _particles;
        readonly OvercastVolume _grey;
        long _lastTick = -1;
        float _windBase = -1f;

        public WeatherLook(WorldRenderModel model, Transform parent)
        {
            _sky = new SkyHeightMap(model);
            _rain = new RainDirector();
            _particles = new RainParticles(parent, _sky);
            _grey = new OvercastVolume(parent);
        }

        public float Cloud { get; private set; }
        public float Rain { get; private set; }
        public float Wet { get; private set; }
        public float Puddles { get; private set; }
        public float Gloom { get; private set; }
        public float Wind { get; private set; } = 1f;

        /// <summary>
        /// What the ground ends up as under this much rain: the wet it closes on and the puddles.
        /// The simulation says how hard it rains; how wet that leaves the ground is drawing, and
        /// lags the rain here rather than being state anybody saves (design 43 §7).
        /// </summary>
        public static float WetFor(float rain) => Mathf.Clamp01(rain * 1.2f);

        public static float PuddlesFor(float rain) => Mathf.Clamp01((rain - 0.35f) / 0.65f);

        /// <summary>
        /// Jump straight to a sky rather than easing to it: for a contact sheet, which has no game
        /// time to ease over.
        /// </summary>
        public void Snap(in WeatherView view)
        {
            Cloud = view.CloudPerMille / 1000f;
            Rain = view.RainPerMille / 1000f;
            Wet = WetFor(Rain);
            Puddles = PuddlesFor(Rain);
            Gloom = view.GloomPerMille / 1000f;
            Wind = view.WindPerMille / 1000f;
        }

        /// <summary>For the frame-time overlay and the tests: what the GPU rain submitted last frame.</summary>
        public RainDirector Drawer => _rain;

        public RainParticles Particles => _particles;

        /// <summary>
        /// One frame. <paramref name="tick"/> and <paramref name="ticksPerSecond"/> give game time;
        /// <paramref name="underground"/> is the slice's own answer, and a view below the surface
        /// draws no rain (its ground is still wet when the player comes back up).
        /// </summary>
        public void Sync(in WeatherView view, bool asParticles, DaylightDirector? daylight,
            Camera? camera, Vector3 focus, float cameraDistance, bool underground, long tick, int ticksPerSecond,
            bool running, float frameSeconds, bool wetGlossOnly = false, WindDirector? wind = null)
        {
            float seconds = _lastTick < 0 ? 0f : Mathf.Max(0, tick - _lastTick) / (float)Mathf.Max(1, ticksPerSecond);
            _lastTick = tick;

            // The sky's own terms, as the simulation published them: it blends one spell into the
            // next over two game hours, so these are followed closely rather than eased again.
            // The ground alone lags, wetting over seconds and drying three times slower.
            float cloud = view.CloudPerMille / 1000f, rain = view.RainPerMille / 1000f;
            float wet = WetFor(rain), puddles = PuddlesFor(rain);
            Cloud = Mathf.MoveTowards(Cloud, cloud, SkyRate * seconds);
            Rain = Mathf.MoveTowards(Rain, rain, SkyRate * seconds);
            Wet = Mathf.MoveTowards(Wet, wet, (wet > Wet ? WetRate : DryRate) * seconds);
            Puddles = Mathf.MoveTowards(Puddles, puddles, (puddles > Puddles ? WetRate : DryRate) * seconds);
            Gloom = Mathf.MoveTowards(Gloom, view.GloomPerMille / 1000f, SkyRate * seconds);
            Wind = Mathf.MoveTowards(Wind, view.WindPerMille / 1000f, SkyRate * seconds);

            // Cover dims and keeps the colour; gloom drains it (Overcast). Only gloom weights the
            // colour-draining volume, so ordinary rain stays as colourful as a clear day.
            if (daylight != null)
            {
                daylight.Cloud = Cloud;
                daylight.Gloom = Gloom;
            }
            _grey.Cover = Gloom;

            // The storm's wind: the grass bends harder, and the rain slants with it because both
            // read the same global. Strength only — never the gust period: the gust phase is the
            // tick over the period, so moving the period at tick 100,000 would spin the phase and
            // the whole meadow would thrash while the storm eased in. The ordinary strength is
            // remembered on first sight and handed back whenever the multiplier is 1.
            if (wind != null)
            {
                if (_windBase < 0f) _windBase = wind.Strength;
                wind.Strength = _windBase * Wind;
            }

            // The cover map only while there is weather to mask; a clear, dry world never pays for it.
            bool weather = Rain > 0.001f || Wet > 0.001f;
            if (weather) _sky.SyncDirty();
            else _sky.Invalidate();

            // The rain clock wraps every game hour so the shader's float keeps its precision; one
            // seam an hour in a pattern of falling drops is not something anybody can see.
            int tps = Mathf.Max(1, ticksPerSecond);
            _rain.Clock = (tick % (tps * 3600L)) / (float)tps;
            _rain.Wetness = Wet;
            _rain.WetGlossOnly = wetGlossOnly;
            _rain.Puddles = Puddles;
            _rain.Intensity = asParticles ? 0f : Rain;
            _particles.Intensity = asParticles ? Rain : 0f;

            if (camera != null) _rain.Draw(camera, focus, cameraDistance, underground);
            else _rain.Publish();
            // The particle arm emits on real frame time and holds on pause through its own speed,
            // the campfire's rule, so its density does not triple at speed 3.
            _particles.Running = running;
            _particles.Sync(focus, cameraDistance, frameSeconds, underground || !asParticles);
        }

        public void Dispose()
        {
            _particles.Dispose();
            _rain.Dispose();
            _grey.Dispose();
            _sky.Dispose();
        }
    }
}
