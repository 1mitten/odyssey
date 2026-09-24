#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Moves the light through the day: blue at noon, orange at either end, dark at night.
    ///
    /// <para>A presentation <b>director</b> in the <c>AudioDirector</c> sense — it owns the sun,
    /// the ambient, the haze and the sky, reads nothing but the tick, and writes nothing anybody
    /// else reads. No cell, no save key, no hash bit. <see cref="Daylight"/> decides what the
    /// light is at an hour and is pure; this is the half that needs an engine to say it to.</para>
    ///
    /// <para><b>The sky material is copied, never edited.</b> <c>RenderSettings.skybox</c> points
    /// at an asset on disk, and writing colours into it at runtime in the editor edits that asset
    /// — so a play session would leave the sky wherever the clock happened to stop, permanently,
    /// and the change would show up as an unexplained diff days later. The copy is made here and
    /// destroyed with the director.</para>
    /// </summary>
    public sealed class DaylightDirector : IDisposable
    {
        /// <summary>
        /// How far the hour must move before the environment is told, in hours.
        ///
        /// <para><c>DynamicGI.UpdateEnvironment</c> re-integrates the ambient probe and is far too
        /// expensive to call every frame for a sky that takes a quarter of an hour of game time to
        /// change perceptibly. At speed 1 an hour is about forty-two real seconds, so this is a
        /// probe update roughly every four seconds — under the rate at which the sky visibly
        /// moves, and a small fraction of what per-frame would cost.</para>
        ///
        /// <para>The sun, the ambient and the fog are held to the far finer
        /// <see cref="ApplyEpsilonHours"/> instead, which is about a twelfth of a second rather
        /// than four — fine enough that the shadows still move smoothly, coarse enough that the
        /// same values are not written sixty times a second.</para>
        /// </summary>
        public const float ProbeUpdateHours = 0.1f;

        /// <summary>
        /// How far the hour must move before anything is written at all, in hours.
        ///
        /// <para>Two thousandths of an hour is five ticks, about a twelfth of a second at speed 1.
        /// Finer than the sky changes and coarser than a frame, which is the whole point.</para>
        /// </summary>
        public const float ApplyEpsilonHours = 0.002f;

        readonly Light _sun;
        readonly Material? _sky;
        readonly Material? _skyAsset;

        float _lastProbeHour = float.NegativeInfinity;

        public DaylightDirector(Light sun, Material? skybox)
        {
            _sun = sun;
            _skyAsset = skybox;
            if (skybox != null)
            {
                _sky = new Material(skybox) { name = skybox.name + " (cycle)" };
                RenderSettings.skybox = _sky;
            }
        }

        /// <summary>The hour last applied, for the developer overlay and the tests.</summary>
        public float Hour { get; private set; } = -1f;

        /// <summary>How many times the ambient probe has been re-integrated. A cost counter.</summary>
        public int ProbeUpdates { get; private set; }

        float _cloud;
        float _probedCloud;

        /// <summary>How far the cover must move before the ambient probe is re-integrated for it.</summary>
        public const float ProbeUpdateCloud = 0.1f;

        /// <summary>
        /// Cloud cover, 0 to 1, graded over the hour's light by <see cref="Overcast"/>. Setting it
        /// forgets the last hour applied so the next <see cref="ApplyHour"/> writes the light. The
        /// probe follows the cover in steps of <see cref="ProbeUpdateCloud"/>, not every frame: a
        /// sky easing in over two seconds would otherwise re-integrate it a hundred times.
        /// </summary>
        public float Cloud
        {
            get => _cloud;
            set
            {
                float v = Mathf.Clamp01(value);
                if (Mathf.Approximately(v, _cloud)) return;
                _cloud = v;
                Hour = -1f;
            }
        }

        /// <summary>Put the light where the tick says it should be.</summary>
        public void Apply(long tick) => ApplyHour(Daylight.HourOf(tick));

        /// <summary>The same, at an hour chosen directly. For the check tools and the tests.</summary>
        public void ApplyHour(float hour)
        {
            // **Nothing is written unless the hour has actually moved.** At speed 1 a frame is one
            // tick, which is four ten-thousandths of an hour — a change no eye can see and no
            // colour channel can hold. Writing it anyway means setting the ambient and the fog
            // sixty times a second to the values they already had, and `RenderSettings.ambient*`
            // is not a cheap field: it is engine state with work behind it, and the frame-time
            // test caught the whole cycle costing about half a millisecond before this guard.
            //
            // The step is far finer than the sky moves, so the light is still continuous: a tenth
            // of this is still a dozen updates a second at ordinary speed.
            if (Hour >= 0f && Mathf.Abs(hour - Hour) < ApplyEpsilonHours) return;

            DaylightState state = Daylight.Sample(hour);
            if (Daylight.MeadowLight) state = Daylight.Meadow(state);
            if (_cloud > 0f) state = Overcast.Grade(state, _cloud);
            Hour = hour;

            _sun.color = state.SunColour;
            _sun.intensity = state.SunIntensity;
            _sun.shadowStrength = state.ShadowStrength;
            _sun.transform.rotation = Quaternion.Euler(state.SunElevation, state.SunAzimuth, 0f);
            // Below the horizon the sun would otherwise light the underside of the world, and a
            // shadow cast upwards through the ground is a stripe across everything.
            _sun.enabled = state.SunElevation > 0f || state.SunIntensity > 0f;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = state.AmbientSky;
            RenderSettings.ambientEquatorColor = state.AmbientEquator;
            RenderSettings.ambientGroundColor = state.AmbientGround;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = state.FogDensity;
            // The identity the whole look rests on, now enforced every frame rather than once at
            // build time: what the distance fades into is exactly what the sky is at the horizon.
            RenderSettings.fogColor = state.Horizon;

            if (_sky != null)
            {
                _sky.SetColor(SkyColour, state.Zenith);
                _sky.SetColor(HorizonColour, state.Horizon);
                _sky.SetColor(GroundColour, state.BelowHorizon);
            }

            bool coverMoved = Mathf.Abs(_cloud - _probedCloud) >= ProbeUpdateCloud
                              || (_cloud == 0f && _probedCloud != 0f);
            if (Mathf.Abs(hour - _lastProbeHour) < ProbeUpdateHours && !coverMoved) return;
            _lastProbeHour = hour;
            _probedCloud = _cloud;
            ProbeUpdates++;
            DynamicGI.UpdateEnvironment();
        }

        public void Dispose()
        {
            if (_sky == null) return;
            // Hand the sky back before destroying the copy, or the scene is left pointing at a
            // dead material and renders the editor's default blue-grey.
            if (ReferenceEquals(RenderSettings.skybox, _sky)) RenderSettings.skybox = _skyAsset;
            if (Application.isPlaying) UnityEngine.Object.Destroy(_sky);
            else UnityEngine.Object.DestroyImmediate(_sky);
        }

        static readonly int SkyColour = Shader.PropertyToID("_SkyColour");
        static readonly int HorizonColour = Shader.PropertyToID("_HorizonColour");
        static readonly int GroundColour = Shader.PropertyToID("_GroundColour");
    }
}
