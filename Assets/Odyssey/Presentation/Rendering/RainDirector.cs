#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Draws the rain: two <c>Graphics.RenderPrimitives</c> calls a frame, whatever the rain.
    ///
    /// <para><b>Prototype (claude/rain-look).</b> The GPU-procedural candidate the weather design's
    /// §7 is weighed against (<c>docs/research/d-20-rain-rendering.md</c>). It owns the four
    /// weather globals the shaders read (<c>OdysseyWeather.hlsl</c>) and the two rain materials,
    /// and nothing else: the cover map is <see cref="SkyHeightMap"/>'s, the grey sky is
    /// <see cref="Overcast"/>'s, the wind is <see cref="WindDirector"/>'s.</para>
    ///
    /// <para><b>The CPU cost is flat.</b> Every drop is placed in the vertex shader from its
    /// instance id and <see cref="Clock"/>, so a downpour is the same two calls and the same four
    /// globals as a drizzle; only the instance count changes, and that is a number the GPU reads.</para>
    ///
    /// <para><b>The clock is the caller's.</b> The game passes seconds of <em>game</em> time, so a
    /// pause holds every drop where it is and speed 3 rains three times as fast, the rule
    /// <see cref="WindDirector"/> follows for the same reasons.</para>
    /// </summary>
    public sealed class RainDirector : IDisposable
    {
        static readonly int RainId = Shader.PropertyToID("_OdysseyRain");
        static readonly int BoxId = Shader.PropertyToID("_Box");
        static readonly int ModeId = Shader.PropertyToID("_Mode");
        static readonly int ZTestId = Shader.PropertyToID("_ZTest");
        static readonly int ScreenId = Shader.PropertyToID("_Screen");
        static readonly int LookId = Shader.PropertyToID("_OdysseyRainLook");
        static readonly int WindId = Shader.PropertyToID("_OdysseyWind");

        readonly Material? _streaks;
        readonly Material? _splashes;
        readonly Material? _screen;

        public RainDirector()
        {
            Shader? shader = Shader.Find("Odyssey/Rain");
            if (shader == null)
            {
                Debug.LogWarning("Odyssey/Rain shader not found; rain will not be drawn.");
                return;
            }

            _streaks = new Material(shader) { name = "Odyssey/Rain/Streaks", hideFlags = HideFlags.DontSave };
            _streaks.SetFloat(ModeId, 0f);
            _splashes = new Material(shader) { name = "Odyssey/Rain/Splashes", hideFlags = HideFlags.DontSave };
            _splashes.SetFloat(ModeId, 1f);
            _screen = new Material(shader) { name = "Odyssey/Rain/Screen", hideFlags = HideFlags.DontSave };
            _screen.SetFloat(ModeId, 2f);
            _screen.SetFloat(ZTestId, (float)CompareFunction.Always);
        }

        public bool Available => _streaks != null;


        /// <summary>How hard it is raining, 0 to 1. Sets the count, the streak length and the splash rate.</summary>
        public float Intensity { get; set; }

        /// <summary>How wet the ground has got, 0 to 1. Lags the rain in a game; set directly here.</summary>
        public float Wetness { get; set; }

        /// <summary>How far puddles have gathered, 0 to 1. Only drawn where the ground is also soaked.</summary>
        public float Puddles { get; set; }

        /// <summary>Seconds of rain clock. Game time, so a paused world holds its drops.</summary>
        public float Clock { get; set; }

        /// <summary>Streaks at full intensity. The quality lever: 24k at Ultra, a quarter of it at Low.</summary>
        public int MaxStreaks { get; set; } = 24_000;

        /// <summary>Splashes at full intensity.</summary>
        public int MaxSplashes { get; set; } = 7_000;

        /// <summary>
        /// The box's half-width for a camera this far from its focus. Grows with zoom so the rain
        /// covers what is on screen, which thins it as the camera pulls back — deliberately, since
        /// a streak at 160 m is two pixels a frame and the ground is carrying the rain there.
        /// </summary>
        public static float HalfWidthFor(float cameraDistance) => Mathf.Clamp(cameraDistance * 0.85f, 18f, 110f);

        /// <summary>Draw wet ground as gloss only, rather than richer and a little darker (owner, choosing by eye).</summary>
        public bool WetGlossOnly { get; set; }

        /// <summary>
        /// The screen layer's zoom: nothing closer than <see cref="ScreenFadeFrom"/> metres from the
        /// focus, all of it from <see cref="ScreenFadeTo"/>. Close up the 3D drops carry the rain; as
        /// the camera pulls back they shrink to a couple of pixels a frame and this takes over
        /// (owner, 2026-09-25: zoomed out "I couldn't really see any rain").
        /// </summary>
        public static float ScreenFadeFrom { get; set; } = 55f;

        public static float ScreenFadeTo { get; set; } = 95f;

        /// <summary>How strong the screen layer is at full zoom and full rain. The owner's dial.</summary>
        public static float ScreenStrength { get; set; } = 1f;

        /// <summary>The screen layer's strength for this rain at this zoom: 0 means it is not drawn.</summary>
        public static float ScreenLayerFor(float intensity, float cameraDistance) =>
            Mathf.Clamp01(intensity) * ScreenStrength *
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ScreenFadeFrom, ScreenFadeTo, cameraDistance));

        public int LastStreaks { get; private set; }

        /// <summary>The screen layer's strength last frame, 0 when it was not drawn.</summary>
        public float LastScreenLayer { get; private set; }
        public int LastSplashes { get; private set; }
        public int LastDrawCalls { get; private set; }

        /// <summary>Publish the weather globals. Cheap; call once a frame before anything draws.</summary>
        public void Publish()
        {
            Shader.SetGlobalVector(RainId, new Vector4(Mathf.Clamp01(Intensity), Mathf.Clamp01(Wetness),
                Clock, Mathf.Clamp01(Puddles)));
            Shader.SetGlobalVector(LookId, new Vector4(WetGlossOnly ? 1f : 0f, 0f, 0f, 0f));
        }

        /// <summary>
        /// Submit the rain for one camera. <paramref name="underground"/> is the slice's own answer
        /// (<c>SliceSettings.BelowSurface</c>): a view cut below the surface shows no sky and no rain.
        /// </summary>
        public void Draw(Camera camera, Vector3 focus, float cameraDistance, bool underground)
        {
            LastStreaks = LastSplashes = LastDrawCalls = 0;
            LastScreenLayer = 0f;
            Publish();
            if (_streaks == null || _splashes == null || underground) return;

            float intensity = Mathf.Clamp01(Intensity);
            if (intensity <= 0.001f) return;

            var box = new Vector4(focus.x, focus.y, focus.z, HalfWidthFor(cameraDistance));
            _streaks.SetVector(BoxId, box);
            _splashes.SetVector(BoxId, box);

            // The drawn volume is the box, and it is always around the focus: nothing to cull.
            var bounds = new Bounds(focus, new Vector3(box.w * 2f, 80f, box.w * 2f));

            LastStreaks = Mathf.RoundToInt(MaxStreaks * intensity);
            LastSplashes = Mathf.RoundToInt(MaxSplashes * intensity);

            // No camera named, as ChunkRenderer submits: a draw enqueued from inside a camera's
            // render for that camera by name did not reach the picture.
            var streaks = new RenderParams(_streaks)
            {
                worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            var splashes = streaks;
            splashes.material = _splashes;

            if (LastStreaks > 0)
            {
                Graphics.RenderPrimitives(streaks, MeshTopology.Triangles, 6, LastStreaks);
                LastDrawCalls++;
            }
            if (LastSplashes > 0)
            {
                Graphics.RenderPrimitives(splashes, MeshTopology.Triangles, 6, LastSplashes);
                LastDrawCalls++;
            }

            // The screen layer: one triangle over the view, drawn once the camera is far enough
            // out that the 3D drops no longer read. Its slant is the wind across the screen.
            float layer = ScreenLayerFor(intensity, cameraDistance);
            if (_screen != null && layer > 0.001f)
            {
                Vector4 wind = Shader.GetGlobalVector(WindId);
                float across = Vector3.Dot(new Vector3(wind.x, 0f, wind.z), camera.transform.right);
                float slant = Mathf.Clamp(-across * 0.6f, -0.45f, 0.45f);
                _screen.SetVector(ScreenId, new Vector4(layer, Mathf.Lerp(0.3f, 0.85f, intensity), slant, 0f));
                var screen = streaks;
                screen.material = _screen;
                Graphics.RenderPrimitives(screen, MeshTopology.Triangles, 3, 1);
                LastDrawCalls++;
                LastScreenLayer = layer;
            }
        }

        /// <summary>A dry world, and the materials gone.</summary>
        public void Dispose()
        {
            Shader.SetGlobalVector(RainId, Vector4.zero);
            Destroy(_streaks);
            Destroy(_splashes);
            Destroy(_screen);
            Shader.SetGlobalVector(LookId, Vector4.zero);
        }

        static void Destroy(UnityEngine.Object? o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
