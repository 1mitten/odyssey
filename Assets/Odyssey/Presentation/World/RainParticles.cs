#nullable enable
using System;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Rain as the weather design's §7 first wrote it: two shared world-space
    /// <see cref="ParticleSystem"/>s, the <see cref="FireDirector"/> pattern exactly, fed by
    /// fixed-cadence <c>Emit()</c> bursts that sample columns near the focus and only emit where the
    /// sky reaches.
    ///
    /// <para><b>The control arm, not a candidate for the game.</b> Kept so the GPU-procedural
    /// <see cref="RainDirector"/> is judged against the design as written, by eye in
    /// <c>RainCheck</c> and by number in the frame-time arm, rather than by argument. It uses the
    /// same cover map, so the two differ in how the drops are made and drawn and in nothing else.</para>
    ///
    /// <para>Every drop is a CPU particle: emitted one <c>EmitParams</c> at a time, simulated by the
    /// particle system on the main thread, sorted and meshed by its renderer. That is the cost this
    /// arm exists to measure.</para>
    /// </summary>
    public sealed class RainParticles : IDisposable
    {
        const float EmitInterval = 0.05f;
        const float FallSpeed = 11f;
        const float SpawnAbove = 22f;

        readonly ParticleSystem? _streaks;
        readonly ParticleSystem? _splashes;
        readonly SkyHeightMap _sky;
        readonly Material? _streakMaterial;
        readonly Material? _splashMaterial;
        readonly GameObject _root;
        float _sinceEmit;
        uint _seed = 0x9E3779B9u;

        public RainParticles(Transform parent, SkyHeightMap sky, int maxStreaks = 24_000, int maxSplashes = 7_000)
        {
            _sky = sky;
            _root = new GameObject("RainParticles") { hideFlags = HideFlags.DontSave };
            _root.transform.SetParent(parent, worldPositionStays: false);

            _streakMaterial = BuildMaterial("Odyssey/RainParticles/Streak", Streak());
            _splashMaterial = BuildMaterial("Odyssey/RainParticles/Splash", Dot());
            if (_streakMaterial == null || _splashMaterial == null) return;

            _streaks = Emitter("Streaks", _streakMaterial, maxStreaks, stretched: true);
            _splashes = Emitter("Splashes", _splashMaterial, maxSplashes, stretched: false);
        }

        public float Intensity { get; set; }

        bool _running = true;

        /// <summary>
        /// Whether the world is advancing: false holds every drop where it is, as the campfire's
        /// <see cref="FireDirector.Running"/> holds its flame, by the particle systems' own speed.
        /// </summary>
        public bool Running
        {
            get => _running;
            set
            {
                if (_running == value) return;
                _running = value;
                if (_streaks == null || _splashes == null) return;
                ParticleSystem.MainModule a = _streaks.main, b = _splashes.main;
                a.simulationSpeed = value ? 1f : 0f;
                b.simulationSpeed = value ? 1f : 0f;
            }
        }

        /// <summary>Streaks alive at full intensity, matched to <see cref="RainDirector.MaxStreaks"/>.</summary>
        public int TargetStreaks { get; set; } = 24_000;

        public int TargetSplashes { get; set; } = 7_000;

        public int LiveStreaks => _streaks != null ? _streaks.particleCount : 0;
        public int LiveSplashes => _splashes != null ? _splashes.particleCount : 0;

        /// <summary>Emit this frame's bursts. Every sampled column costs a cover lookup on the CPU.</summary>
        public void Sync(Vector3 focus, float cameraDistance, float dt, bool underground)
        {
            if (_streaks == null || _splashes == null) return;
            if (underground || Intensity <= 0.001f)
            {
                _streaks.Clear();
                _splashes.Clear();
                return;
            }

            if (!_running) return;
            _sinceEmit += dt;
            if (_sinceEmit < EmitInterval) return;
            float span = _sinceEmit;
            _sinceEmit = 0f;

            float half = RainDirector.HalfWidthFor(cameraDistance);
            float life = SpawnAbove / FallSpeed;

            // Enough emitted per second that the live count settles at the target.
            int streaks = Mathf.RoundToInt(TargetStreaks * Intensity * span / life);
            int splashes = Mathf.RoundToInt(TargetSplashes * Intensity * span / 0.32f);
            float wind = 0f;

            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < streaks; i++)
            {
                float x = focus.x + (Next() * 2f - 1f) * half;
                float z = focus.z + (Next() * 2f - 1f) * half;
                if (!Stop(x, z, out float stop, out _)) continue;
                float top = Mathf.Max(focus.y, stop) + SpawnAbove * (0.4f + 0.6f * Next());
                p.position = new Vector3(x, top, z);
                p.velocity = new Vector3(wind, -FallSpeed, 0f);
                p.startLifetime = Mathf.Max(0.05f, (top - stop) / FallSpeed);
                p.startSize = 0.03f;
                p.startColor = new Color(0.78f, 0.84f, 0.92f, 0.45f);
                _streaks.Emit(p, 1);
            }

            for (int i = 0; i < splashes; i++)
            {
                float x = focus.x + (Next() * 2f - 1f) * half;
                float z = focus.z + (Next() * 2f - 1f) * half;
                if (!Stop(x, z, out float stop, out float kind) || kind == SkyHeightMap.KindCanopy) continue;
                p.position = new Vector3(x, stop + 0.05f, z);
                p.velocity = Vector3.zero;
                p.startLifetime = 0.32f;
                p.startSize = 0.28f;
                p.startColor = new Color(0.85f, 0.9f, 0.96f, 0.55f);
                _splashes.Emit(p, 1);
            }
        }

        /// <summary>Step both systems by hand, for editor tools that have no player loop.</summary>
        public void Evaluate(float dt)
        {
            _streaks?.Simulate(dt, withChildren: false, restart: false, fixedTimeStep: false);
            _splashes?.Simulate(dt, withChildren: false, restart: false, fixedTimeStep: false);
        }

        bool Stop(float x, float z, out float stop, out float kind)
        {
            int cx = Mathf.FloorToInt(x / CellMetrics.SizeXZ), cz = Mathf.FloorToInt(z / CellMetrics.SizeXZ);
            stop = 0f;
            kind = 0f;
            if (cx < 0 || cz < 0 || cx >= _sky.Width || cz >= _sky.Depth) return false;
            stop = _sky.StopAt(cx, cz);
            kind = _sky.KindAt(cx, cz);
            return true;
        }

        float Next()
        {
            _seed ^= _seed << 13;
            _seed ^= _seed >> 17;
            _seed ^= _seed << 5;
            return (_seed & 0xFFFFFF) / 16777216f;
        }

        ParticleSystem Emitter(string name, Material material, int max, bool stretched)
        {
            var host = new GameObject(name) { hideFlags = HideFlags.DontSave };
            host.transform.SetParent(_root.transform, worldPositionStays: false);
            var system = host.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.gravityModifier = 0f;
            main.startSpeed = 0f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            if (!stretched)
            {
                ParticleSystem.SizeOverLifetimeModule grow = system.sizeOverLifetime;
                grow.enabled = true;
                grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.2f, 1f, 1f));
                ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                fade.color = gradient;
            }

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (stretched)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.09f;
                renderer.lengthScale = 1f;
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            }
            return system;
        }

        /// <summary>The fire's recipe: URP's particle shader, told it is transparent in every dialect.</summary>
        static Material? BuildMaterial(string name, Texture2D texture)
        {
            Shader? shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return null;
            var material = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 50;
            material.SetTexture("_BaseMap", texture);
            return material;
        }

        static Texture2D Streak()
        {
            const int W = 8, H = 64;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "Odyssey/RainStreak", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float across = 1f - Mathf.Abs((x + 0.5f) / W * 2f - 1f);
                float along = Mathf.Sin((y + 0.5f) / H * Mathf.PI);
                px[y * W + x] = new Color32(255, 255, 255, (byte)(255 * across * along));
            }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        static Texture2D Dot()
        {
            const int S = 32;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = "Odyssey/RainRing", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x + 0.5f) / S * 2f - 1f, dy = (y + 0.5f) / S * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.75f) / 0.18f);
                px[y * S + x] = new Color32(255, 255, 255, (byte)(255 * ring));
            }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        public void Dispose()
        {
            Destroy(_root);
            if (_streakMaterial != null) { Destroy(_streakMaterial.GetTexture("_BaseMap")); Destroy(_streakMaterial); }
            if (_splashMaterial != null) { Destroy(_splashMaterial.GetTexture("_BaseMap")); Destroy(_splashMaterial); }
        }

        static void Destroy(UnityEngine.Object? o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
