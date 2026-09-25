#nullable enable
using System;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// The weather as the player sees it, driven by the debug menu's Weather tab until the
    /// simulation has a sky of its own (design 43 §8: the tab is how the rain-look prototype is
    /// tested in Play, owner 2026-09-24).
    ///
    /// <para><b>One object so the bootstrap gains four lines, not forty.</b> It owns the cover map,
    /// the GPU rain, the particle control arm and the grey volume, and it moves the daylight's
    /// cloud term; the bootstrap builds it, calls <see cref="Sync"/> once a frame and disposes it
    /// with the session.</para>
    ///
    /// <para><b>A preset is a target, not a switch.</b> Cloud and rain close on it over a couple
    /// of game seconds, the ground wets over several and dries more slowly than it wets — so a
    /// change of sky is something that arrives. All of it runs on game time read from the tick:
    /// paused, the sky holds; at speed 3 it moves three times as fast.</para>
    ///
    /// <para><b>Nothing here is simulation</b>: no cell, no save, no hash. The day the weather
    /// system exists, <see cref="Sync"/> is handed its numbers instead of a preset's.</para>
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
        /// Jump straight to a preset rather than easing to it: for a contact sheet, which has no
        /// game time to ease over.
        /// </summary>
        public void Snap(DebugDirector.WeatherPreset target)
        {
            Cloud = target.Cloud;
            Rain = target.Rain;
            Wet = target.Wet;
            Puddles = target.Puddles;
            Gloom = target.Gloom;
            Wind = target.Wind;
        }

        /// <summary>For the frame-time overlay and the tests: what the GPU rain submitted last frame.</summary>
        public RainDirector Drawer => _rain;

        public RainParticles Particles => _particles;

        /// <summary>
        /// One frame. <paramref name="tick"/> and <paramref name="ticksPerSecond"/> give game time;
        /// <paramref name="underground"/> is the slice's own answer, and a view below the surface
        /// draws no rain (its ground is still wet when the player comes back up).
        /// </summary>
        public void Sync(DebugDirector.WeatherPreset target, bool asParticles, DaylightDirector? daylight,
            Camera? camera, Vector3 focus, float cameraDistance, bool underground, long tick, int ticksPerSecond,
            bool running, float frameSeconds, bool wetGlossOnly = false, WindDirector? wind = null)
        {
            float seconds = _lastTick < 0 ? 0f : Mathf.Max(0, tick - _lastTick) / (float)Mathf.Max(1, ticksPerSecond);
            _lastTick = tick;

            Cloud = Mathf.MoveTowards(Cloud, target.Cloud, SkyRate * seconds);
            Rain = Mathf.MoveTowards(Rain, target.Rain, SkyRate * seconds);
            Wet = Mathf.MoveTowards(Wet, target.Wet, (target.Wet > Wet ? WetRate : DryRate) * seconds);
            Puddles = Mathf.MoveTowards(Puddles, target.Puddles, (target.Puddles > Puddles ? WetRate : DryRate) * seconds);
            Gloom = Mathf.MoveTowards(Gloom, target.Gloom, SkyRate * seconds);
            Wind = Mathf.MoveTowards(Wind, target.Wind, SkyRate * seconds);

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
