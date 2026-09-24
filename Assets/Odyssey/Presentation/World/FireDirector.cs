#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using Odyssey.Presentation.CameraRig;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using Odyssey.Presentation.Audio;
using Odyssey.Presentation.Rendering;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// The flame, the smoke and the light on every campfire the player can see
    /// (docs/design/31-campfire-art-and-fire.md §4, §8).
    ///
    /// <para>A director and not a subsystem: nothing here is in a cell, a save or the state hash.
    /// The simulation knows a campfire pushes <c>heatPerPass</c> into its room (design 28) and has
    /// never heard of a particle. The prop is the mesher's — one catalogue row on
    /// <c>ModuleIds.Campfire</c> — and this is everything about it that moves.</para>
    ///
    /// <para><b>Two shared emitters for the whole colony, and one light each.</b> The particles
    /// follow <see cref="ChipDirector"/>'s pattern exactly, for its reason: systems simulated in
    /// world space can be emitted into from anywhere, so one flame system and one smoke system
    /// serve every fire on the board at one draw call apiece, however many are burning. A light
    /// cannot be shared that way — it is a position — so those are pooled one per drawn fire,
    /// which is affordable here only because the renderer is <b>Forward+</b>
    /// (<c>PC_Renderer.asset</c>, <c>m_RenderingMode: 2</c>): local lights are clustered and
    /// <c>m_AdditionalLightsPerObjectLimit</c> does not apply. On a Forward renderer that limit is
    /// four per object and the symptom is lights popping in and out as the camera moves, so **the
    /// render path is load-bearing for this class** and `27-graphics-settings.md` says so beside
    /// the lever that would change it.</para>
    ///
    /// <para><b>Which fires exist is cached against <see cref="WorldRenderModel.Version"/></b>,
    /// the same trick <see cref="DoorDirector"/> plays and for the same reason: walking every cell
    /// on the board once a frame to find the campfires is `docs/bug-patterns.md` P10 wearing a
    /// different coat. The board is walked once per structural change instead.</para>
    /// </summary>
    public sealed class FireDirector : IDisposable
    {
        /// <summary>
        /// Where the flame is born, above the cell floor, in metres.
        ///
        /// <para><b>Tuned against a picture, and the first number was wrong.</b> At 0.55 m, with
        /// the rise the particles had, the flame came out as a bright blob hanging *over* the log
        /// teepee rather than burning inside it — the prop stands 1.65 m and the fire was clearing
        /// it. Low and short is what reads as a campfire: born among the logs, gone before it
        /// leaves them.</para>
        /// </summary>
        public const float FlameHeight = 0.28f;

        /// <summary>Where the smoke starts — above the flame's reach, not above the logs.</summary>
        public const float SmokeHeight = 1.05f;

        /// <summary>
        /// How far the light carries, in metres. Two cells and a little more at 2.5 m a cell —
        /// "lights the surroundings" is a hut's worth, not a street's.
        /// </summary>
        public const float LightRange = 7.5f;

        /// <summary>Intensity the flicker wanders around.</summary>
        public const float LightIntensity = 3.2f;

        /// <summary>How far either side of <see cref="LightIntensity"/> the flicker goes, as a
        /// fraction. A fire is not a lamp, and this is most of what says so.</summary>
        public const float FlickerDepth = 0.18f;

        static readonly Color FlameColour = new Color(1f, 0.58f, 0.18f, 1f);
        static readonly Color EmberColour = new Color(1f, 0.33f, 0.08f, 1f);
        static readonly Color LightColour = new Color(1f, 0.72f, 0.42f, 1f);
        static readonly Color SmokeColour = new Color(0.30f, 0.29f, 0.27f, 0.5f);

        /// <summary>How many fires are lit and drawn this frame. Diagnostic, and what a cost test
        /// counts.</summary>
        public int LitFires { get; private set; }

        /// <summary>True once the materials and systems exist and have been stepped.</summary>
        public bool Warmed { get; private set; }

        /// <summary>
        /// Whether the fire is burning. False holds the particles and the flicker exactly where
        /// they are, the way <see cref="ChipDirector.Running"/> does and for its reason: Unity's
        /// particle clock knows nothing about the simulation clock, so a paused game with a
        /// merrily flickering fire in it is the same fault.
        /// </summary>
        public bool Running
        {
            get => _running;
            set
            {
                if (_running == value) return;
                _running = value;
                SetSpeed(_flame, value);
                SetSpeed(_smoke, value);
            }
        }

        bool _running = true;

        readonly WorldRenderModel _model;
        readonly GameObject? _root;
        readonly ParticleSystem? _flame;
        readonly ParticleSystem? _smoke;
        readonly int _layer;

        /// <summary>
        /// How the campfires on the board are found. A control, so the two can be measured
        /// against each other in one run — this machine drifts by more between runs than most
        /// passes cost (CLAUDE.md).
        /// </summary>
        public enum Find
        {
            /// <summary>Sweep every cell. What this class shipped with, and the bug.</summary>
            Cells,

            /// <summary>Walk the standing edifices, which is where a campfire actually lives.</summary>
            Edifices,
        }

        public static Find Mode = Find.Edifices;

        /// <summary>How many times the board has been re-swept, and how many cells that cost.
        /// Diagnostic: a rescan count near the frame count means the cache is not caching.</summary>
        public int Rescans { get; private set; }

        /// <summary>Cells or edifice records visited by all rescans so far.</summary>
        public long RescanVisits { get; private set; }

        readonly IReadOnlyList<PlacedEdifice>? _edifices;
        readonly List<int> _cells = new List<int>(8);
        readonly List<Light> _lights = new List<Light>(8);

        /// <summary>The fires that are audible this frame, refilled rather than rebuilt — a
        /// sound that plays forever must not cost an allocation a frame to keep playing.</summary>
        readonly List<LoopPoint> _audible = new List<LoopPoint>(8);
        int _cellsVersion = -1;

        float _clock;
        float _emit;

        public FireDirector(WorldRenderModel model, Transform? parent, int layer,
            IReadOnlyList<PlacedEdifice>? edifices = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _layer = layer;
            _edifices = edifices;

            Material? flameMaterial = BuildMaterial("Odyssey/Flame", additive: true);
            Material? smokeMaterial = BuildMaterial("Odyssey/Smoke", additive: false);
            if (flameMaterial == null || smokeMaterial == null) return;

            _root = new GameObject("Fires");
            _root.transform.SetParent(parent, worldPositionStays: false);
            _root.layer = layer;

            _flame = Emitter(_root.transform, "Flame", layer, flameMaterial, 512);
            _smoke = Emitter(_root.transform, "Smoke", layer, smokeMaterial, 256);
            Warm();
        }

        /// <summary>
        /// An additive unlit material for the flame and a plain one for the smoke, built in code.
        ///
        /// <para><b>Ours rather than the pack's</b>, which is a licence decision and not a taste
        /// one (design 31 §2, §4). <c>PolygonParticleFX</c>'s materials hang off
        /// <c>PolygonGeneric/Shaders/Generic_Basic.shadergraph</c>, inside the gitignored folder —
        /// a Shader Graph no <c>Shader.Find</c> ever names, which the player build strips and
        /// whose keep-alive must never be committed, which is the <c>SyntyInstancingKeepAlive</c>
        /// trap that cost three passes on 2026-09-19. A built-in URP particle shader costs none of
        /// that, and the owner sanctioned it directly.</para>
        ///
        /// <para>A clone with no usable shader gets no emitter and no fire, and everything else
        /// goes on working — the same bargain every other piece of presentation art makes.</para>
        /// </summary>
        static Material? BuildMaterial(string name, bool additive)
        {
            Shader? shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                             ?? Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Sprites/Default");
            if (shader == null) return null;

            var material = new Material(shader) { name = name };

            // **Transparent, and said in every dialect the shader might be listening in.** A URP
            // particle material left at its defaults is *opaque*, and the first picture of this
            // showed exactly that: hard-edged orange rectangles stacked over the fire, which is
            // what a billboard is before anything tells it to blend. Setting `_Surface`/`_Blend`
            // alone is not enough from code — the keywords and the queue are what the pass
            // actually reads, and the material inspector is what normally sets them.
            material.SetFloat(Surface, 1f);                       // transparent
            material.SetFloat(Blend, additive ? 2f : 0f);         // additive : alpha
            material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat(DstBlend, (float)(additive
                ? UnityEngine.Rendering.BlendMode.One
                : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            material.SetFloat(ZWrite, 0f);
            material.SetFloat(Cull, (float)UnityEngine.Rendering.CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // A soft round sprite, made here rather than shipped. Without one every particle is a
            // square — which is what the first picture was — and the obvious fix is the pack's
            // own smoke texture, which is gitignored and would leave a clone with squares again.
            // Sixty-four pixels of radial falloff costs 16 kB and belongs to us.
            material.SetTexture(BaseMap, Puff());
            return material;
        }

        static readonly int Surface = Shader.PropertyToID("_Surface");
        static readonly int Blend = Shader.PropertyToID("_Blend");
        static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
        static readonly int Cull = Shader.PropertyToID("_Cull");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        static Texture2D? _puff;

        /// <summary>
        /// A soft circular sprite: white, with alpha falling from the centre on a smoothstep so
        /// the edge has no ring in it. One texture serves the flame and the smoke — the colour is
        /// per particle and the shape is the same puff either way.
        /// </summary>
        static Texture2D Puff()
        {
            if (_puff != null) return _puff;

            const int Size = 64;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: true)
            {
                name = "Odyssey/Puff",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[Size * Size];
            const float Centre = (Size - 1) * 0.5f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = (x - Centre) / Centre;
                float dy = (y - Centre) / Centre;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.15f, 1f, d));
                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true);
            _puff = texture;
            return texture;
        }

        static ParticleSystem Emitter(Transform parent, string name, int layer, Material material, int max)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, worldPositionStays: false);
            host.layer = layer;

            var system = host.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.gravityModifier = 0f;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.startSize = 0.3f;

            // Nothing emits on its own. Sync decides which fires are visible and throws for them,
            // which is what lets one system serve the whole board.
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.alignment = ParticleSystemRenderSpace.View;

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }

        /// <summary>
        /// Draw both materials once, far below the board, before anybody needs them.
        ///
        /// <para><see cref="ChipDirector.Warm"/> has the argument and it applies here with more
        /// force: the first draw of a particle material compiles its shader variant, and left to
        /// happen naturally that lands on the frame the player's <em>first campfire finishes</em>
        /// — the one frame in the sequence they are certainly watching, and one this project has
        /// already had a build-appearance complaint about.</para>
        /// </summary>
        public void Warm()
        {
            if (_flame == null || _smoke == null || Warmed) return;

            var parameters = new ParticleSystem.EmitParams
            {
                position = new Vector3(0f, -10_000f, 0f),
                velocity = Vector3.zero,
                startLifetime = 0.01f,
                startSize = 0.001f,
                startColor = new Color(0f, 0f, 0f, 0f),
                applyShapeToPosition = false,
            };

            _flame.Emit(parameters, 4);
            _smoke.Emit(parameters, 4);
            _flame.Simulate(0.02f, withChildren: true, restart: false);
            _smoke.Simulate(0.02f, withChildren: true, restart: false);
            _flame.Clear(true);
            _smoke.Clear(true);
            Warmed = true;
        }

        /// <summary>
        /// Burn, once a frame, wherever a campfire is drawn.
        ///
        /// <para>Visibility is the slice's, asked the same way <see cref="DoorDirector"/> asks it.
        /// <b>A light has to be switched off and not merely left unfed</b>, which is the trap in
        /// this class: hiding the flame on a hidden layer does nothing about the light, because a
        /// light does not know what the slice camera is doing and would go on lighting the floor
        /// above it from underneath.</para>
        /// </summary>
        public void Sync(int activeLayer, SliceSettings slice, float dt) =>
            Sync(activeLayer, slice, dt, null);

        /// <summary>
        /// Burn, and — given the director — crackle.
        ///
        /// <para>The sound is declared from here rather than from the audio side because this is
        /// the class that already knows which fires are <b>drawn</b>, and a fire on a hidden
        /// storey should be inaudible for the same reason it is unlit. Working that out twice, in
        /// two places, is how the two come to disagree.</para>
        /// </summary>
        public void Sync(int activeLayer, SliceSettings slice, float dt, AudioDirector? audio)
        {
            LitFires = 0;
            _audible.Clear();
            if (_root == null)
            {
                audio?.SyncLoops(SoundIds.Campfire, _audible);
                return;
            }

            RefreshCells();

            int lowest = Mathf.Max(0, slice.LowestDrawnLayer(activeLayer, _model.LowestOutdoorLayer));
            int highest = slice.HighestVisibleLayer(activeLayer, _model.Size.SizeY);

            if (_running) _clock += dt;

            // A particle budget spent evenly rather than per frame: the emitters are fed on a
            // fixed cadence so the look does not change with the frame rate, and a slow frame
            // does not throw a cloud.
            bool throwNow = false;
            if (_running)
            {
                _emit += dt;
                if (_emit >= EmitInterval) { _emit %= EmitInterval; throwNow = true; }
            }

            int used = 0;
            for (int i = 0; i < _cells.Count; i++)
            {
                CellRef cell = _model.Size.FromIndex(_cells[i]);
                if (cell.Y < lowest || cell.Y > highest) continue;

                Vector3 floor = GroundRelief.Drape(
                    CellMetrics.FloorCentre(cell.X, cell.Z, cell.Y)).GetColumn(3);

                if (throwNow) Throw(floor);
                Lamp(used++, floor, _cells[i]);
                _audible.Add(new LoopPoint(_cells[i], floor));
                LitFires++;
            }

            // Every light past the ones in use goes dark rather than being destroyed: a colony
            // whose fires come and go should not churn GameObjects every time the slice moves.
            for (int i = used; i < _lights.Count; i++)
                if (_lights[i] != null) _lights[i].enabled = false;

            // The crackle, from the same set. The pool keeps the nearest few and stops the rest,
            // so this stays one list however many fires a colony ends up with.
            audio?.SyncLoops(SoundIds.Campfire, _audible);
        }

        /// <summary>Seconds between feeds of the emitters.</summary>
        public const float EmitInterval = 0.06f;

        void Throw(Vector3 floor)
        {
            if (_flame == null || _smoke == null) return;

            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };

            // Three tongues a feed, thrown up and inward, short-lived and small: a Synty fire is
            // a handful of flat shapes rather than a volume, and more than this reads as a bonfire.
            for (int i = 0; i < 3; i++)
            {
                float a = (float)_random.NextDouble() * Mathf.PI * 2f;
                float r = 0.12f + (float)_random.NextDouble() * 0.22f;
                p.position = floor + new Vector3(Mathf.Cos(a) * r, FlameHeight, Mathf.Sin(a) * r);
                p.velocity = new Vector3(Mathf.Cos(a) * -0.06f, 0.42f + (float)_random.NextDouble() * 0.28f,
                                         Mathf.Sin(a) * -0.06f);
                p.startLifetime = 0.28f + (float)_random.NextDouble() * 0.18f;
                p.startSize = 0.30f + (float)_random.NextDouble() * 0.18f;
                p.startColor = _random.NextDouble() < 0.35 ? EmberColour : FlameColour;
                _flame.Emit(p, 1);
            }

            // Smoke is slower, larger, fewer and drifts: one every few feeds, or a campfire
            // smokes like a chimney.
            if (_random.NextDouble() < 0.34)
            {
                float a = (float)_random.NextDouble() * Mathf.PI * 2f;
                p.position = floor + new Vector3(Mathf.Cos(a) * 0.1f, SmokeHeight, Mathf.Sin(a) * 0.1f);
                p.velocity = new Vector3(0.12f, 0.55f, 0.08f);
                p.startLifetime = 1.8f + (float)_random.NextDouble() * 1.2f;
                p.startSize = 0.5f + (float)_random.NextDouble() * 0.4f;
                p.startColor = SmokeColour;
                _smoke.Emit(p, 1);
            }
        }

        /// <summary>
        /// The light on one fire, taken from the pool and flickered.
        ///
        /// <para><b>Shadows are off and that is a decision, not an omission</b> (design 31 §8). A
        /// point light's shadow is six faces out of one 2048 atlas, and each face re-renders the
        /// geometry around it — ten shadow-casting campfires would ask for sixty. A campfire is a
        /// warm pool on the ground, which is what it looks like anyway.</para>
        ///
        /// <para>The flicker is two sine waves at unrelated rates plus the cell's own index, so
        /// neighbouring fires are never in step and the pattern does not repeat in a way the eye
        /// catches. It is driven by <see cref="_clock"/>, which stops with <see cref="Running"/>.
        /// </para>
        /// </summary>
        void Lamp(int slot, Vector3 floor, int cellIndex)
        {
            while (_lights.Count <= slot)
            {
                var host = new GameObject($"Firelight {_lights.Count}");
                host.transform.SetParent(_root!.transform, worldPositionStays: false);
                host.layer = _layer;

                var made = host.AddComponent<Light>();
                made.type = LightType.Point;
                made.color = LightColour;
                made.range = LightRange;
                made.shadows = LightShadows.None;
                made.renderMode = LightRenderMode.Auto;
                _lights.Add(made);
            }

            Light light = _lights[slot];
            light.transform.position = floor + new Vector3(0f, FlameHeight, 0f);

            float phase = cellIndex * 0.618f;
            float wobble = Mathf.Sin(_clock * 11.7f + phase) * 0.6f
                         + Mathf.Sin(_clock * 4.3f + phase * 2.1f) * 0.4f;
            light.intensity = LightIntensity * (1f + wobble * FlickerDepth);
            light.enabled = true;
        }

        /// <summary>
        /// Which cells hold a campfire.
        ///
        /// <para><b>This shipped as P10 wearing a different coat, and its own comment said it was
        /// not.</b> The claim was that the board is swept "once per structural change rather than
        /// once a frame", and the cache is keyed on <see cref="WorldRenderModel.Version"/> — which
        /// <c>RefreshDirty</c> bumps whenever <b>any chunk remeshes</b>. In a colony that is doing
        /// anything at all that is most frames, so the sweep ran at 230,400 cells a frame on the
        /// played board to find at most a handful of fires. Exactly the fault this class was
        /// written to avoid, and exactly the fault found in <c>TemperatureSystem</c> the day
        /// before: a complexity claim in a doc comment is not a measurement.</para>
        ///
        /// <para><see cref="Find.Edifices"/> walks the standing edifices instead — where a
        /// campfire actually lives, and a list two orders of magnitude shorter than the cell
        /// grid. The trigger is unchanged, so the cache still refreshes exactly as often; what
        /// changed is what a refresh costs. <see cref="Find.Cells"/> is kept as the control that
        /// measured it.</para>
        /// </summary>
        void RefreshCells()
        {
            if (_cellsVersion == _model.Version) return;
            _cellsVersion = _model.Version;
            Rescans++;

            _cells.Clear();

            if (Mode == Find.Edifices && _edifices != null)
            {
                RescanVisits += _edifices.Count;
                for (int i = 0; i < _edifices.Count; i++)
                {
                    PlacedEdifice placed = _edifices[i];
                    if (placed.Removed) continue;
                    if (placed.Def != CoreContent.EdificeCampfire) continue;

                    // The model is still the authority on what is drawn where: an edifice record
                    // can outlive the cell it was in, and the mesher reads the mirror rather than
                    // the list. Asking it per candidate is a handful of lookups, not a sweep.
                    if (_model.EdificeDef(placed.CellIndex) == CoreContent.EdificeCampfire)
                        _cells.Add(placed.CellIndex);
                }
                return;
            }

            int count = _model.Size.CellCount;
            RescanVisits += count;
            for (int i = 0; i < count; i++)
                if (_model.EdificeDef(i) == CoreContent.EdificeCampfire) _cells.Add(i);
        }

        static void SetSpeed(ParticleSystem? system, bool running)
        {
            if (system == null) return;
            ParticleSystem.MainModule main = system.main;
            main.simulationSpeed = running ? 1f : 0f;
        }

        /// <summary>Step the systems by hand, for a tool with no player loop. The game does not
        /// call this; a probe photographing a fire does.</summary>
        public void Evaluate(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            _flame?.Simulate(deltaTime, withChildren: true, restart: false);
            _smoke?.Simulate(deltaTime, withChildren: true, restart: false);
        }

        readonly System.Random _random = new System.Random(20260923);

        public void Dispose()
        {
            if (_root == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
            else UnityEngine.Object.DestroyImmediate(_root);
        }
    }
}
