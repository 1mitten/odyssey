#nullable enable
using System;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// <b>The clouds, drawn</b> (design 63): the Meadow demo's two cloud rings on the pack's own
    /// <c>Clouds</c> shader graph, round the camera, standing taller as the sky clouds over, turning
    /// slowly on game time, and coloured by the day's light.
    ///
    /// <para><b>Two calls, or none.</b> Each ring is one <c>Graphics.RenderMesh</c>, with no shadow,
    /// no probes and only the game camera. When the camera cannot see the ring's lowest point, which
    /// is the colony camera at every pitch but its lowest, or the view is below the surface, or it is
    /// night and the clouds have gone (design 63 §4c), nothing is submitted at all, so the depth, the SSAO normals and the colour pass all stay empty
    /// (<see cref="CloudDeck.CanBeSeen"/>).</para>
    ///
    /// <para><b>The pack's material is copied, never written</b>, the sky's rule
    /// (<see cref="DaylightDirector"/>): writing its colours at runtime in the editor would edit the
    /// licensed asset on disk. The copy is drawn after the board's opaque geometry, so the terrain in
    /// front of a ring has already filled the depth and the ring's hidden pixels are never shaded.</para>
    ///
    /// <para><b>Without the packs there are no clouds</b>, and nothing else changes: the references
    /// in <see cref="MeadowLook"/> resolve to null and <see cref="Available"/> is false.</para>
    ///
    /// <para><b>Nothing here reaches a cell, a save or the hash.</b></para>
    /// </summary>
    public sealed class CloudDirector : IDisposable
    {
        /// <summary>After every opaque queue the board draws in, before the transparent ones.</summary>
        public const int RenderQueue = 2450;

        static readonly int TopId = Shader.PropertyToID("_Top_Color");
        static readonly int UnderId = Shader.PropertyToID("_Base_Color");
        static readonly int RimId = Shader.PropertyToID("_Fresnel_Color");
        static readonly int ScatteringId = Shader.PropertyToID("_Enable_Scattering");
        static readonly int FogId = Shader.PropertyToID("_Enable_Fog");
        static readonly int SpeedId = Shader.PropertyToID("_Cloud_Speed");
        static readonly int LightOverrideId = Shader.PropertyToID("_Light_Direction_Override");
        static readonly int EnvironmentId = Shader.PropertyToID("_Use_Environment_Override");

        readonly Mesh? _low;
        readonly Mesh? _high;
        readonly Material? _material;
        readonly float _boardTop;
        float _lowYaw;
        float _highYaw = 137f;
        int _appliedVersion = -1;
        float _appliedCloud = -1f, _appliedGloom = -1f, _appliedRain = -1f, _appliedPresence = -1f;
        float _shown = -1f;

        public CloudDirector(WorldRenderModel model, MeadowLook? look)
        {
            _boardTop = model.Size.SizeY * CellMetrics.SizeY;
            if (look == null || !look.HasClouds)
            {
                // The art is here and its shader is not: a player build that stripped it. Said once,
                // so a smoke test's log shows it rather than a sky that is quietly empty.
                if (look != null && look.cloudRing != null && look.clouds != null)
                    Debug.LogWarning("[Clouds] the cloud art resolved but its shader cannot run here; no clouds are drawn.");
                return;
            }

            _low = look.cloudRing;
            _high = look.cloudRingHigh;
            _material = new Material(look.clouds!)
            {
                name = look.clouds!.name + " (sky)",
                hideFlags = HideFlags.DontSave,
                renderQueue = RenderQueue,
            };
            // The pack billows its clouds on the wall clock, which would move them on a paused
            // world; the drift here is on game time instead. And its light is the sun's, not the
            // demo's nudge towards one side of its own scene.
            _material.SetFloat(SpeedId, 0f);
            _material.SetVector(LightOverrideId, Vector4.zero);
            _material.SetFloat(EnvironmentId, 0f);
            // The pack's scattering blends towards its colour by the sun's direction taken channel
            // by channel, so a high sun pushes red and blue up against green: a noon cloud came out
            // lilac (design 63 §4a). The demo's sun never moves; ours does.
            _material.SetFloat(ScatteringId, 0f);
            // And its fog lerps past the fog colour: 0.18 plus the distance fog, which in our haze
            // is about 1 at a ring's range, so the shading inverted and a night cloud went to pure
            // blue (§4b). The haze is in the colours instead, which is exact for a ring at one range.
            _material.SetFloat(FogId, 0f);
        }

        /// <summary>Whether the art resolved and there is something to draw.</summary>
        public bool Available => _material != null && _low != null;

        /// <summary>Off, nothing is stepped or drawn. The control the frame-time arm times against.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Calls submitted last frame: two when the rings could be seen, none when not.</summary>
        public int LastDrawCalls { get; private set; }

        /// <summary>Whether last frame's camera could see the rings.</summary>
        public bool LastVisible { get; private set; }

        /// <summary>The low ring's turn, in degrees.</summary>
        public float LowYaw => _lowYaw;

        /// <summary>The material the rings draw with, for the tests.</summary>
        public Material? Material => _material;

        /// <summary>How much of the clouds was shown last frame: 1 by day, 0 by night, eased (<see cref="CloudDeck.Presence"/>).</summary>
        public float LastPresence { get; private set; } = 1f;

        /// <summary>
        /// One frame. <paramref name="gameSeconds"/> is the frame's seconds times the game speed
        /// (nought while paused); <paramref name="realSeconds"/> is the frame's own, which paces a
        /// jump in the hour. <paramref name="cloud"/>, <paramref name="gloom"/>,
        /// <paramref name="rain"/> and <paramref name="wind"/> are the weather as eased for drawing
        /// (<see cref="WeatherLook"/>). The hour is the daylight's, or the baked noon without one.
        /// </summary>
        public void Sync(Camera? camera, float gameSeconds, float realSeconds, float cloud, float gloom, float rain,
            float wind, DaylightDirector? daylight, bool underground)
        {
            LastDrawCalls = 0;
            LastVisible = false;
            if (!Enabled || !Available || camera == null) return;

            _lowYaw = CloudDeck.Advance(_lowYaw, gameSeconds, wind, CloudDeck.DriftDegreesPerSecond);
            _highYaw = CloudDeck.Advance(_highYaw, gameSeconds, wind,
                CloudDeck.DriftDegreesPerSecond * CloudDeck.HighDriftRatio);

            // Gone by night (design 63 §4c): nothing is submitted, so a night sky costs nothing. The
            // clock's presence is followed, not taken, so a jump in the hour fades (§4e); the first
            // frame of a session takes it as it is.
            float hour = daylight != null && daylight.Hour >= 0f ? daylight.Hour : Daylight.DefaultHour;
            float target = CloudDeck.Presence(hour);
            _shown = _shown < 0f ? target : CloudDeck.EasePresence(_shown, target, realSeconds);
            float presence = _shown;
            LastPresence = presence;
            if (underground || presence <= 0f) return;

            Transform eye = camera.transform;
            Vector3 at = eye.position;
            float pitchDown = -Mathf.Asin(Mathf.Clamp(eye.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            float highest = CloudDeck.HighestElevation(pitchDown, camera.fieldOfView, camera.aspect);

            // A fading ring sinks to the eye line and flattens there, so it goes edge-on rather than
            // off (§4e). A ring is round: its radius is its larger half-width, whatever its turn.
            Bounds shape = _low!.bounds;
            float radius = Mathf.Max(shape.extents.x, shape.extents.z) * CloudDeck.LowSpread;
            float bottom = CloudDeck.BaseAt(_boardTop + CloudDeck.LowBase, at.y, presence);
            if (!CloudDeck.CanBeSeen(highest, CloudDeck.LowestElevation(radius, bottom, at.y))) return;
            LastVisible = true;

            Colour(daylight, cloud, gloom, rain, presence);
            float cover = CloudDeck.StandingCover(cloud, rain), sink = CloudDeck.Sink(presence);
            Draw(camera, _low, at, bottom, _lowYaw, CloudDeck.LowSpread,
                CloudDeck.HeightScale(cover, CloudDeck.LowHeightClear, CloudDeck.LowHeightFull) * sink);
            if (_high != null)
                Draw(camera, _high, at, CloudDeck.BaseAt(_boardTop + CloudDeck.HighBase, at.y, presence),
                    _highYaw, CloudDeck.HighSpread,
                    CloudDeck.HeightScale(cover, CloudDeck.HighHeightClear, CloudDeck.HighHeightFull) * sink);
        }

        /// <summary>
        /// The colours, rewritten only when something they are made of has moved: the daylight
        /// director writes its state a dozen times a second at most (and the hour, so the presence,
        /// moves with it), rain is followed in steps of two per cent, and a frame in which nothing
        /// moved costs two compares.
        /// </summary>
        void Colour(DaylightDirector? daylight, float cloud, float gloom, float rain, float presence)
        {
            bool rainMoved = Mathf.Abs(rain - _appliedRain) >= 0.02f || (rain == 0f && _appliedRain != 0f);
            // An eased fade moves the presence between the daylight's writes, so it is followed too.
            bool presenceMoved = Mathf.Abs(presence - _appliedPresence) >= 0.01f
                                 || ((presence == 0f || presence == 1f) && presence != _appliedPresence);
            rainMoved |= presenceMoved;
            DaylightState state;
            if (daylight != null)
            {
                if (daylight.Version == _appliedVersion && !rainMoved) return;
                _appliedVersion = daylight.Version;
                state = daylight.State;
            }
            else
            {
                // No cycle: the baked hour, graded here since nobody else will.
                if (Mathf.Abs(cloud - _appliedCloud) < 0.02f && Mathf.Abs(gloom - _appliedGloom) < 0.02f && !rainMoved) return;
                _appliedCloud = cloud;
                _appliedGloom = gloom;
                state = Overcast.Grade(BakedHour(), cloud, gloom);
            }
            // Only once rewritten: a fade creeping under the step each frame must still add up to one.
            _appliedRain = rain;
            _appliedPresence = presence;

            CloudColourSet set = CloudColours.For(state, daylight != null ? daylight.Gloom : gloom, rain, presence);
            _material!.SetColor(TopId, set.Top);
            _material.SetColor(UnderId, set.Under);
            _material.SetColor(RimId, set.Rim);
        }

        void Draw(Camera camera, Mesh mesh, Vector3 eye, float bottom, float yaw, float spread, float height)
        {
            Bounds shape = mesh.bounds;
            // The ring's own middle on the camera, and its lowest point on the base.
            Matrix4x4 place = Matrix4x4.TRS(new Vector3(eye.x, bottom, eye.z), Quaternion.Euler(0f, yaw, 0f),
                                  new Vector3(spread, height, spread))
                              * Matrix4x4.Translate(new Vector3(-shape.center.x, -shape.min.y, -shape.center.z));

            float reach = Reach(shape) * spread;
            float tall = shape.size.y * height;
            var parameters = new RenderParams(_material)
            {
                camera = camera,
                worldBounds = new Bounds(new Vector3(eye.x, bottom + tall * 0.5f, eye.z),
                    new Vector3(2f * reach, tall + 2f, 2f * reach)),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
            };
            Graphics.RenderMesh(parameters, mesh, 0, place);
            LastDrawCalls++;
        }

        /// <summary>The light a board without the day cycle is drawn in, as the daylight director would make it.</summary>
        public static DaylightState BakedHour()
        {
            DaylightState s = Daylight.Sample(Daylight.DefaultHour);
            return Daylight.MeadowLight ? Daylight.Meadow(s) : s;
        }

        /// <summary>The half-diagonal of the ring's footprint: an axis-aligned box that holds it at any turn.</summary>
        static float Reach(Bounds shape) => Mathf.Sqrt(shape.extents.x * shape.extents.x + shape.extents.z * shape.extents.z);

        public void Dispose()
        {
            if (_material == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(_material);
            else UnityEngine.Object.DestroyImmediate(_material);
        }
    }
}
