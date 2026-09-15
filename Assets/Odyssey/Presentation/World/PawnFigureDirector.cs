#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Gives the pawns actually on screen a live, animated figure, and lets the rest stay as the
    /// baked meshes the instanced pass draws.
    ///
    /// **Why a GameObject at all, in a renderer built to avoid them.** Skinning is the one thing
    /// the instanced path cannot do. <c>RenderMeshInstanced</c> takes one mesh and many matrices
    /// with no per-instance bone palette, so a rig cannot go through it; that is why colonists are
    /// baked to a static mesh at load and why, until now, they glided. A skinned figure has to be
    /// a real object with a real <see cref="Animator"/>. The rule this does not break is the one
    /// that matters: there is no GameObject per *cell*, and there never will be. There is one per
    /// visible *pawn*, capped, pooled and reused, which is a handful of objects rather than tens
    /// of thousands.
    ///
    /// **Why Playables rather than an AnimatorController.** A controller is an asset, and an asset
    /// referencing clips inside the licensed packs is one more thing to generate, commit and keep
    /// in step. A two- or three-clip blend needs no state machine at all: a mixer with one input
    /// per gait, weights set from how fast the pawn is going. It is about thirty lines, it is
    /// built from the catalogue rows the rest of the renderer already reads, and a clone without
    /// the packs resolves the clips to null and simply gets no figures.
    ///
    /// **Speed is measured, not assumed.** The blend is driven by the figure's own displacement
    /// per frame rather than by the tick rate and the movement def. That costs a subtraction and
    /// it is right by construction at every game speed, while paused, and during the part-tick
    /// extrapolation — none of which a derivation from the movement def would survive without
    /// being told about each of them in turn.
    /// </summary>
    public sealed class PawnFigureDirector : IDisposable
    {
        /// <summary>
        /// How many pawns may have a live figure at once.
        ///
        /// A cap rather than a promise: past it, pawns keep the baked instanced form, which costs
        /// what a wall costs. The number is deliberately generous for the slice — the colony is
        /// five — and exists so that a later crowd degrades in quality rather than in frame rate.
        /// </summary>
        public int MaxFigures { get; set; } = 64;

        /// <summary>Pawn ids drawn as live figures this frame. The instanced pass skips these.</summary>
        public HashSet<int> Drawn { get; } = new HashSet<int>();

        readonly Transform _parent;
        readonly GameObject? _prefab;
        readonly LocomotionEntry[] _gaits;

        /// <summary>Gait speeds as drawn, i.e. after the figure's scale. See <see cref="GroundSpeeds"/>.</summary>
        readonly float[] _gaitSpeeds;

        readonly Vector3 _scale;
        readonly int _layer;

        readonly List<Figure> _figures = new List<Figure>();
        readonly Dictionary<int, Figure> _byPawn = new Dictionary<int, Figure>();
        readonly List<int> _retired = new List<int>();

        /// <summary>True when there is art and at least one gait, so figures can be made at all.</summary>
        public bool Enabled => _prefab != null && _gaits.Length > 0;

        public int FigureCount => _byPawn.Count;

        /// <summary>
        /// The fastest live figure's ground speed, in metres per second.
        ///
        /// Diagnostic, and not an idle one: the speed drives the blend, and a speed that is
        /// plausible but wrong — out by a game-speed multiplier, or by the figure's scale —
        /// produces an animation that plays perfectly and simply does not match the walk. A number
        /// on screen next to a gait that can be recognised by eye is what makes that checkable.
        /// </summary>
        public float FastestSpeed { get; private set; }

        /// <summary>
        /// Read the colonist row out of the library's catalogue and stand ready to make figures.
        ///
        /// Everything is optional. No catalogue, no prefab, no clips, or a clone without the packs
        /// all leave <see cref="Enabled"/> false and every pawn on the baked path, which is the
        /// same world it was before this existed.
        /// </summary>
        public PawnFigureDirector(ModuleCatalogue? catalogue, Transform parent, int layer)
        {
            _parent = parent;
            _layer = layer;

            ModuleEntry? row = catalogue != null ? catalogue.Find(ModuleIds.Colonist) : null;
            _prefab = row != null ? row.prefab : null;
            _scale = row != null ? row.scale : Vector3.one;
            _gaits = Gaits(row);
            _gaitSpeeds = GroundSpeeds(_gaits, _scale);
        }

        /// <summary>Gaits with a live clip, slowest first. Order is what makes the blend a blend.</summary>
        static LocomotionEntry[] Gaits(ModuleEntry? row)
        {
            if (row == null) return Array.Empty<LocomotionEntry>();

            var usable = new List<LocomotionEntry>();
            for (int i = 0; i < row.locomotion.Count; i++)
                if (row.locomotion[i].clip != null) usable.Add(row.locomotion[i]);

            usable.Sort((a, b) => a.metresPerSecond.CompareTo(b.metresPerSecond));
            return usable.ToArray();
        }

        /// <summary>
        /// Each gait's speed **as drawn**, which is not the speed the clip was measured at.
        ///
        /// Colonists are drawn half again as large as life, because a true-to-scale figure is a
        /// few pixels once the camera pulls back. A scaled figure takes scaled strides: the same
        /// walk cycle covers 1.4 times the ground it was authored to. Blending against the
        /// unscaled numbers would pick a gait too slow for the speed and then play it too fast,
        /// which is the exact recipe for feet skating over the grass — and it would have looked
        /// like an animation fault rather than an arithmetic one.
        /// </summary>
        static float[] GroundSpeeds(LocomotionEntry[] gaits, Vector3 scale)
        {
            float factor = Mathf.Max(0.01f, (Mathf.Abs(scale.x) + Mathf.Abs(scale.z)) * 0.5f);
            var speeds = new float[gaits.Length];
            for (int i = 0; i < gaits.Length; i++) speeds[i] = gaits[i].metresPerSecond * factor;
            return speeds;
        }

        /// <summary>
        /// Place and animate a figure for every pawn in the drawn layers, and park the rest.
        ///
        /// <paramref name="deltaTime"/> is the frame's own elapsed time, which is also the clock
        /// the graphs advance on; passing it in rather than reading <c>Time.deltaTime</c> is what
        /// lets an editor tool with no frame loop render a walk cycle at a chosen moment.
        /// </summary>
        public void Sync(WorldSnapshot snapshot, int activeLayer, SliceSettings slice,
            float tickAlpha, int movePerTick, float deltaTime)
        {
            Drawn.Clear();
            FastestSpeed = 0f;
            if (!Enabled) return;

            int lowest = Mathf.Max(0, slice.LowestDrawnLayer(activeLayer));
            var pawns = snapshot.Pawns;

            for (int i = 0; i < pawns.Length && Drawn.Count < MaxFigures; i++)
            {
                CellRef cell = pawns[i].Cell;
                if (cell.Y < lowest || cell.Y > activeLayer) continue;

                Vector3 position = PawnPose.Of(pawns[i], tickAlpha, movePerTick, out Vector3 heading);
                Figure figure = Lease(pawns[i].Id, position);
                Pose(figure, position, heading, deltaTime);
                if (figure.Speed > FastestSpeed) FastestSpeed = figure.Speed;
                Drawn.Add(pawns[i].Id.Value);
            }

            Retire();
        }

        /// <summary>Advance every live figure's animation. Separate from posing so an editor
        /// tool can step the clock deliberately rather than relying on a running player.</summary>
        public void Evaluate(float deltaTime)
        {
            for (int i = 0; i < _figures.Count; i++)
                if (_figures[i].Pawn >= 0) _figures[i].Graph.Evaluate(deltaTime);
        }

        void Pose(Figure figure, Vector3 position, Vector3 heading, float deltaTime)
        {
            // Speed from displacement, which is right at every game speed and while paused, and
            // needs to know nothing about ticks. A figure that has just been leased has no
            // previous position worth differencing, hence Settled.
            float speed = 0f;
            if (figure.Settled && deltaTime > 1e-5f)
                speed = Vector3.Distance(position, figure.Transform.position) / deltaTime;

            // One frame of a lost path or a slice change can jump a pawn further than any gait
            // covers. Smoothing keeps a single frame from throwing the figure into a sprint.
            figure.Speed = figure.Settled ? Mathf.Lerp(figure.Speed, speed, 0.35f) : 0f;
            figure.Settled = true;

            figure.Transform.position = position;
            float yaw = PawnPose.YawOf(heading);
            if (heading.sqrMagnitude > 1e-4f) figure.Yaw = yaw;
            figure.Transform.rotation = Quaternion.Euler(0f, figure.Yaw, 0f);

            Blend(figure, figure.Speed);
        }

        /// <summary>
        /// Set the mixer weights for a speed, and the playback rate that keeps feet on the ground.
        ///
        /// Inside the range the gaits cover, a linear blend between the two that bracket the speed
        /// means the blended stride already matches the ground speed, so the clips play at their
        /// authored rate. Only above the fastest gait does the rate have to stretch, and that is
        /// the one case where a figure is genuinely moving faster than any clip was made for.
        /// </summary>
        void Blend(Figure figure, float speed)
        {
            GaitBlend blend = GaitBlend.Solve(_gaitSpeeds, speed);
            for (int i = 0; i < _gaits.Length; i++)
            {
                figure.Mixer.SetInputWeight(i, blend.WeightOf(i));
                figure.Clips[i].SetSpeed(blend.Rate);
            }
        }

        Figure Lease(PawnId pawn, Vector3 at)
        {
            if (_byPawn.TryGetValue(pawn.Value, out Figure? existing)) return existing;

            Figure figure = Free() ?? Create();
            figure.Pawn = pawn.Value;
            figure.Settled = false;
            figure.Speed = 0f;
            figure.Transform.position = at;
            figure.GameObject.SetActive(true);
            _byPawn[pawn.Value] = figure;
            return figure;
        }

        Figure? Free()
        {
            for (int i = 0; i < _figures.Count; i++)
                if (_figures[i].Pawn < 0) return _figures[i];
            return null;
        }

        /// <summary>Park figures whose pawn is no longer drawn: dead, unloaded, or on another layer.</summary>
        void Retire()
        {
            _retired.Clear();
            foreach (KeyValuePair<int, Figure> pair in _byPawn)
                if (!Drawn.Contains(pair.Key)) _retired.Add(pair.Key);

            for (int i = 0; i < _retired.Count; i++)
            {
                Figure figure = _byPawn[_retired[i]];
                figure.Pawn = -1;
                figure.GameObject.SetActive(false);
                _byPawn.Remove(_retired[i]);
            }
        }

        Figure Create()
        {
            GameObject instance = UnityEngine.Object.Instantiate(_prefab!, _parent);
            instance.name = $"Colonist figure {_figures.Count}";
            instance.transform.localScale = _scale;
            SetLayer(instance.transform, _layer);

            // The pack prefabs carry colliders for their own demo scenes. Cell picking is done by
            // ray against the grid, not against the scene, so a collider here can only intercept a
            // click meant for the ground.
            var colliders = instance.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            // The simulation says where a pawn is. A clip that also moved it would fight that.
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var graph = PlayableGraph.Create($"Odyssey pawn {_figures.Count}");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var mixer = AnimationMixerPlayable.Create(graph, _gaits.Length);
            var clips = new AnimationClipPlayable[_gaits.Length];

            for (int i = 0; i < _gaits.Length; i++)
            {
                clips[i] = AnimationClipPlayable.Create(graph, _gaits[i].clip);
                graph.Connect(clips[i], 0, mixer, i);
                mixer.SetInputWeight(i, i == 0 ? 1f : 0f);
            }

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Pose", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();

            var figure = new Figure(instance, animator, graph, mixer, clips);
            _figures.Add(figure);
            return figure;
        }

        static void SetLayer(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            for (int i = 0; i < transform.childCount; i++) SetLayer(transform.GetChild(i), layer);
        }

        public void Dispose()
        {
            for (int i = 0; i < _figures.Count; i++)
            {
                if (_figures[i].Graph.IsValid()) _figures[i].Graph.Destroy();
                GameObject go = _figures[i].GameObject;
                if (go == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(go);
                else UnityEngine.Object.DestroyImmediate(go);
            }
            _figures.Clear();
            _byPawn.Clear();
            Drawn.Clear();
        }

        sealed class Figure
        {
            public Figure(GameObject go, Animator animator, PlayableGraph graph,
                AnimationMixerPlayable mixer, AnimationClipPlayable[] clips)
            {
                GameObject = go;
                Transform = go.transform;
                Animator = animator;
                Graph = graph;
                Mixer = mixer;
                Clips = clips;
                Pawn = -1;
            }

            public readonly GameObject GameObject;
            public readonly Transform Transform;
            public readonly Animator Animator;
            public readonly PlayableGraph Graph;
            public readonly AnimationMixerPlayable Mixer;
            public readonly AnimationClipPlayable[] Clips;

            /// <summary>The pawn this figure is lent to, or -1 when it is parked in the pool.</summary>
            public int Pawn;

            /// <summary>False for the first frame after a lease, when there is no previous position.</summary>
            public bool Settled;

            public float Speed;
            public float Yaw;
        }
    }
}
