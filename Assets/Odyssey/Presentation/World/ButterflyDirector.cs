#nullable enable
using System;
using System.Runtime.InteropServices;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// The butterflies, drawn (design 52): the <see cref="ButterflyMeadow"/> stepped each frame, its
    /// butterflies packed into one structured buffer, and two <c>Graphics.RenderPrimitives</c> calls —
    /// every wing on screen in one, and at night every halo and pool of light in the other.
    ///
    /// <para><b>Drawn, never simulated.</b> The meadow is seeded from the world's own seed so a board
    /// shows the same butterflies twice, but nothing here reaches a cell, a save, the state hash or
    /// the pawn registry, and nothing reads it back.</para>
    ///
    /// <para><b>The cost is two calls whatever the count</b> (P10): the buffer is written once a frame
    /// from the model's own arrays, and the shader builds every vertex from it by id. There is no
    /// mesh, no matrix and no per-butterfly submission. The ladder's Off rung steps and draws
    /// nothing at all.</para>
    ///
    /// <para><b>The colours are <see cref="ButterflyPalette"/>'s, handed to the shader once</b> as
    /// uniform arrays, so the fast tier's colour-blind check is a check on what is drawn.</para>
    /// </summary>
    public sealed class ButterflyDirector : IDisposable
    {
        /// <summary>Corners one butterfly is drawn with: 24 wing triangles and 8 body triangles.
        /// <c>OdysseyButterfly.shader</c>'s <c>BUTTERFLY_VERTS</c>; a test reads the shader to hold
        /// the two to one number.</summary>
        public const int VertsPerButterfly = 96;

        /// <summary>A butterfly's width across the wings, metres: four to six times life (e-13 §12).</summary>
        public static float Span { get; set; } = 0.36f;

        /// <summary>
        /// The camera distances over which the wings shrink out, metres. Past the far one no wing is
        /// drawn — but at night the halos go on to the full zoom, so a far night is a meadow of
        /// coloured lights (design 52 §5).
        /// </summary>
        public static Vector2 WingFade { get; set; } = new Vector2(80f, 110f);

        /// <summary>The halo's radius in metres, before its floor in pixels.</summary>
        public const float HaloRadius = 0.32f;

        /// <summary>How far the wind global's push drifts a butterfly, metres a second per unit.</summary>
        public const float WindDrift = 0.25f;

        /// <summary>Walkers read from the crowd index each frame; past this the rest are not felt.</summary>
        public const int MaxWalkers = 512;

        static readonly int DataId = Shader.PropertyToID("_ButterflyData");
        static readonly int ClockId = Shader.PropertyToID("_ButterflyClock");
        static readonly int LightId = Shader.PropertyToID("_ButterflyLight");
        static readonly int SpeciesId = Shader.PropertyToID("_ButterflySpecies");
        static readonly int ShapeId = Shader.PropertyToID("_ButterflyShape");
        static readonly int Shape2Id = Shader.PropertyToID("_ButterflyShape2");
        static readonly int GlowId = Shader.PropertyToID("_ButterflyGlow");
        static readonly int DriftId = Shader.PropertyToID("_ButterflyDrift");
        static readonly int BreathId = Shader.PropertyToID("_ButterflyBreath");
        static readonly int SpanId = Shader.PropertyToID("_Span");
        static readonly int WingFadeId = Shader.PropertyToID("_WingFade");
        static readonly int WindId = Shader.PropertyToID("_OdysseyWind");

        readonly ButterflyHabitat _habitat;
        readonly ButterflyMeadow _meadow;
        readonly Material? _wings;
        readonly Material? _glow;
        readonly float[] _walkers = new float[MaxWalkers * 3];
        Vector4[] _packed = Array.Empty<Vector4>();
        GraphicsBuffer? _buffer;
        float _clock;

        public ButterflyDirector(WorldRenderModel model, uint seed)
        {
            _habitat = new ButterflyHabitat(model);
            _meadow = new ButterflyMeadow(seed ^ 0xB077E5u, 0);

            Shader? shader = Shader.Find("Odyssey/Butterfly");
            if (shader == null)
            {
                Debug.LogWarning("Odyssey/Butterfly shader not found; butterflies will not be drawn.");
                return;
            }

            _wings = new Material(shader) { name = "Odyssey/Butterfly/Wings", hideFlags = HideFlags.DontSave };
            _wings.SetFloat("_Mode", 0f);
            _wings.SetFloat("_SrcBlend", (float)BlendMode.One);
            _wings.SetFloat("_DstBlend", (float)BlendMode.Zero);
            _wings.SetFloat("_ZWrite", 1f);
            _wings.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            // After the opaque world and the depth copy, where the grass is drawn (design 38 §13).
            _wings.renderQueue = (int)RenderQueue.GeometryLast + 1;

            _glow = new Material(shader) { name = "Odyssey/Butterfly/Glow", hideFlags = HideFlags.DontSave };
            _glow.SetFloat("_Mode", 1f);
            _glow.SetFloat("_SrcBlend", (float)BlendMode.One);
            _glow.SetFloat("_DstBlend", (float)BlendMode.One);
            _glow.SetFloat("_ZWrite", 0f);
            // The glow asks the depth texture itself where the surface behind a pixel is, so it is
            // drawn whatever stands in front and lights a colonist as readily as the grass.
            _glow.SetFloat("_ZTest", (float)CompareFunction.Always);
            _glow.renderQueue = (int)RenderQueue.Transparent + 60;

            SetPalette(_wings);
            SetPalette(_glow);
        }

        /// <summary>Whether the shader was found. Without it the meadow still steps and nothing draws.</summary>
        public bool Available => _wings != null;

        /// <summary>The model, for the tests and the overlay.</summary>
        public ButterflyMeadow Meadow => _meadow;

        /// <summary>The habitat, for the tests.</summary>
        public ButterflyHabitat Habitat => _habitat;

        /// <summary>The ladder's rung: how many butterflies the meadow may hold. Zero is Off.</summary>
        public int Capacity
        {
            get => _meadow.Capacity;
            set
            {
                int capacity = Math.Max(0, value);
                if (capacity == _meadow.Capacity) return;
                _meadow.Resize(capacity);
                if (capacity == 0) _meadow.Clear();
            }
        }

        /// <summary>Butterflies drawn last frame.</summary>
        public int LastDrawn { get; private set; }

        /// <summary>Calls submitted last frame: nought, one by day, two at night.</summary>
        public int LastDrawCalls { get; private set; }

        /// <summary>How far into the night last frame was, 0 to 1.</summary>
        public float LastNight { get; private set; }

        /// <summary>
        /// One frame. <paramref name="seconds"/> is the frame's real seconds while the world runs and
        /// zero while it is paused (design 52 §7); the season, the hour and the weather come from the
        /// game's own clock. <paramref name="lowestLayer"/> to <paramref name="highestLayer"/> is the
        /// band the slice draws; <paramref name="underground"/> is the slice's own answer and hides
        /// them all, though the meadow keeps stepping so they are where they should be on the way back.
        /// </summary>
        public void Sync(float seconds, long tick, float hour, float cloud, float rain,
            PawnCrowdIndex? crowd, Vector3 focus, float cameraDistance,
            int lowestLayer, int highestLayer, bool underground)
        {
            LastDrawn = LastDrawCalls = 0;
            if (_meadow.Capacity == 0) return;

            _habitat.SyncDirty();

            int walkers = 0;
            if (crowd != null)
            {
                walkers = Math.Min(crowd.Count, MaxWalkers);
                for (int i = 0; i < walkers; i++)
                {
                    Vector3 at = crowd.PositionAt(i);
                    _walkers[i * 3] = at.x;
                    _walkers[i * 3 + 1] = at.y;
                    _walkers[i * 3 + 2] = at.z;
                }
            }

            Vector4 wind = Shader.GetGlobalVector(WindId);
            var sky = new ButterflyConditions(tick, cloud, rain, wind.x * WindDrift, wind.z * WindDrift);
            float radius = ButterflyMeadow.RadiusFor(cameraDistance);
            _meadow.Step(seconds, sky, _habitat, focus.x, focus.z, radius,
                new ReadOnlySpan<float>(_walkers, 0, walkers * 3));

            // The ambient clock: real seconds while running, held on a pause. Wrapped every half
            // hour so the shader's float keeps its precision; a seam that rare in a beat is unseen.
            _clock = (_clock + seconds) % 1800f;
            LastNight = ButterflyPalette.NightFor(Daylight.Sample(hour).SunElevation);

            if (_wings == null || _glow == null || underground) return;

            Draw(lowestLayer, highestLayer, focus, radius);
        }

        void Draw(int lowestLayer, int highestLayer, Vector3 focus, float radius)
        {
            int capacity = _meadow.Capacity;
            if (_packed.Length < capacity * 4)
            {
                _packed = new Vector4[capacity * 4];
                _buffer?.Release();
                _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity * 4, 16) { name = "Odyssey/Butterflies" };
                _wings!.SetBuffer(DataId, _buffer);
                _glow!.SetBuffer(DataId, _buffer);
            }

            Span<float> floats = MemoryMarshal.Cast<Vector4, float>(_packed.AsSpan());
            int count = _meadow.Pack(floats, lowestLayer * CellMetrics.SizeY, (highestLayer + 1) * CellMetrics.SizeY);
            if (count == 0) return;
            _buffer!.SetData(_packed, 0, 0, count * 4);

            SetFrame(_wings!);
            SetFrame(_glow!);

            // Everything drawn is inside the window, and the window is always round the focus.
            var bounds = new Bounds(focus, new Vector3(radius * 2.4f, 200f, radius * 2.4f));
            var wings = new RenderParams(_wings)
            {
                worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderPrimitives(wings, MeshTopology.Triangles, VertsPerButterfly, count);
            LastDrawCalls++;

            if (LastNight > 0.001f)
            {
                var glow = wings;
                glow.material = _glow;
                Graphics.RenderPrimitives(glow, MeshTopology.Triangles, 6, count);
                LastDrawCalls++;
            }
            LastDrawn = count;
        }

        /// <summary>This frame's clock, night and sizes. Called once per material rather than over an
        /// array of the two, which would allocate every frame.</summary>
        void SetFrame(Material material)
        {
            material.SetVector(ClockId, new Vector4(_clock, LastNight, ButterflyPalette.WingGlowCeiling, ButterflyPalette.HaloPeak));
            material.SetVector(LightId, new Vector4(ButterflyPalette.LightRadius, ButterflyPalette.LightGain, HaloRadius, 0f));
            material.SetFloat(SpanId, Span);
            material.SetVector(WingFadeId, new Vector4(WingFade.x, WingFade.y, 0f, 0f));
        }

        /// <summary>The palette, once: every colour the shader draws with comes from here.</summary>
        static void SetPalette(Material material)
        {
            ButterflyPalette.Species[] all = ButterflyPalette.All;
            var species = new Vector4[24];
            var shape = new Vector4[6];
            var shape2 = new Vector4[6];
            for (int s = 0; s < 6; s++)
            {
                ButterflyPalette.Species sp = all[Math.Min(s, all.Length - 1)];
                species[s * 4] = Linear(sp.Ground);
                species[s * 4 + 1] = Linear(sp.Dark);
                species[s * 4 + 2] = Linear(sp.Accent);
                species[s * 4 + 3] = Linear(sp.Eye);
                shape[s] = new Vector4(sp.Veins, sp.Margin, sp.Eyespots, sp.Band);
                shape2[s] = new Vector4(sp.MarginSpots, 0f, 0f, 0f);
            }
            material.SetVectorArray(SpeciesId, species);
            material.SetVectorArray(ShapeId, shape);
            material.SetVectorArray(Shape2Id, shape2);

            var glows = new Vector4[4];
            float running = 0f;
            for (int g = 0; g < 4; g++)
            {
                ButterflyPalette.Glow glow = ButterflyPalette.Glows[Math.Min(g, ButterflyPalette.Glows.Length - 1)];
                running += glow.Weight;
                Vector4 c = Linear(glow.Colour) * glow.Gain;
                glows[g] = new Vector4(c.x, c.y, c.z, g == 3 ? 1.01f : running);
            }
            material.SetVectorArray(GlowId, glows);
            material.SetVector(DriftId, new Vector4(ButterflyPalette.HueDriftDegrees * Mathf.Deg2Rad,
                ButterflyPalette.HueDriftSlowest, ButterflyPalette.HueDriftQuickest, ButterflyPalette.PulseFloor));
            material.SetVector(BreathId, new Vector4(ButterflyPalette.PulseSlowest, ButterflyPalette.PulseQuickest, 0f, 0f));
        }

        static Vector4 Linear(HudColour c) =>
            new Vector4(ButterflyPalette.Linear(c.R), ButterflyPalette.Linear(c.G), ButterflyPalette.Linear(c.B), 1f);

        public void Dispose()
        {
            _buffer?.Release();
            _buffer = null;
            Destroy(_wings);
            Destroy(_glow);
        }

        static void Destroy(UnityEngine.Object? o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
