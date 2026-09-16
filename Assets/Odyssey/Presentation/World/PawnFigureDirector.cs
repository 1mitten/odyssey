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

        /// <summary>
        /// How fast a figure turns to face where it is going, in degrees per second.
        ///
        /// Fast enough to be facing its path within a step, slow enough that the turn reads as a
        /// turn. The simulation has no notion of facing at all — a pawn simply occupies the next
        /// cell — so this is presentation inventing something plausible, not tracking anything.
        /// </summary>
        public float TurnDegreesPerSecond { get; set; } = 540f;

        /// <summary>
        /// How long a figure takes to ease into a work pose, and out of it again, in seconds.
        ///
        /// A colonist that snapped into a full swing on the tick the walk ended would pop, and
        /// one that kept the last angle after the tree came down would stand there with an arm in
        /// the air. A quarter of a second is short enough to feel immediate and long enough to
        /// read as somebody setting themselves.
        /// </summary>
        public float WorkEaseSeconds { get; set; } = 0.25f;

        /// <summary>
        /// How much of the swing the off hand takes, 0 to 1.
        ///
        /// An axe is held in two hands, so the left arm has to travel with the right or the
        /// figure reads as holding it one-handed while the other arm goes on breathing in the
        /// idle. Not the full amount, because the hands are not in the same place on the haft.
        /// </summary>
        public float OffHandShare { get; set; } = 0.82f;

        /// <summary>
        /// How far up the haft the hand grips, 0 at the butt and 1 at the head.
        ///
        /// A felling grip is near the butt, which is what gives the blow its leverage. Not *at*
        /// the butt: an axe held right on the end reads as being dangled rather than held.
        /// </summary>
        public float AxeGripFraction { get; set; } = 0.16f;

        /// <summary>
        /// Which way the blade faces, in degrees about the haft.
        ///
        /// One lever rather than three Euler angles, and it is the only part of the grip that is
        /// still a matter of taste: everything else — which way the haft runs, which end the head
        /// is on, where the hand sits along it — is measured off the mesh at build time. See
        /// <see cref="GripAxe"/>.
        /// </summary>
        public float AxeBladeRoll { get; set; } = 0f;

        /// <summary>Pawn ids drawn as live figures this frame. The instanced pass skips these.</summary>
        public HashSet<int> Drawn { get; } = new HashSet<int>();

        readonly Transform _parent;
        readonly int _layer;

        /// <summary>One face a colonist can wear: its art, its size and the gaits it can walk in.</summary>
        sealed class Look
        {
            public GameObject Prefab = null!;
            public Vector3 Scale;
            public LocomotionEntry[] Gaits = Array.Empty<LocomotionEntry>();

            /// <summary>Gait speeds as drawn, i.e. after Scale. See <see cref="GroundSpeeds"/>.</summary>
            public float[] Speeds = Array.Empty<float>();
        }

        readonly Look[] _looks;

        /// <summary>
        /// The axe row, or null on a clone without the packs. See <see cref="ModuleIds.ToolAxe"/>.
        /// </summary>
        readonly ModuleEntry? _axe;

        /// <summary>The tick the last published snapshot carried, and how long ago it changed.</summary>
        int _lastTick = -1;
        float _sinceTick;

        readonly List<Figure> _figures = new List<Figure>();
        readonly Dictionary<int, Figure> _byPawn = new Dictionary<int, Figure>();
        readonly List<int> _retired = new List<int>();

        /// <summary>True when there is at least one usable face, so figures can be made at all.</summary>
        public bool Enabled => _looks.Length > 0;

        /// <summary>How many different faces a colonist can be drawn with.</summary>
        public int LookCount => _looks.Length;

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
            _looks = LooksFrom(catalogue);
            _axe = catalogue != null ? catalogue.Find(ModuleIds.ToolAxe) : null;
        }

        /// <summary>
        /// Every colonist row in the catalogue that has both art and something to walk with.
        ///
        /// Rows that resolve to nothing are dropped rather than kept as holes, so a clone missing
        /// one pack still gets every face the packs it does have can provide, and a colony on a
        /// machine with no packs at all simply falls through to the baked instanced path.
        /// </summary>
        static Look[] LooksFrom(ModuleCatalogue? catalogue)
        {
            if (catalogue == null) return Array.Empty<Look>();

            var looks = new List<Look>();
            foreach (ModuleEntry row in catalogue.FindFamily(ModuleIds.ColonistBase))
            {
                if (row.prefab == null) continue;
                LocomotionEntry[] gaits = Gaits(row);
                if (gaits.Length == 0) continue;

                looks.Add(new Look
                {
                    Prefab = row.prefab,
                    Scale = row.scale,
                    Gaits = gaits,
                    Speeds = GroundSpeeds(gaits, row.scale),
                });
            }
            return looks.ToArray();
        }

        /// <summary>
        /// Which face a pawn wears, fixed by its id.
        ///
        /// By id and not by draw order, because a figure is leased and returned as a pawn crosses
        /// the drawn layers, and a colonist who came back from a trip downstairs as somebody else
        /// would be worse than a colony of identical twins.
        /// </summary>
        /// <summary>
        /// Per-session salt for the face lottery. Set before the first <see cref="Sync"/> and then
        /// left alone, and set to the *same* value on the instanced renderer, or a colonist will
        /// change face on crossing the figure cap.
        /// </summary>
        public uint LookSalt { get; set; }

        int LookFor(PawnId pawn) => ColonistLook.For(pawn.Value, _looks.Length, LookSalt);

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

            // Is the world actually running? A swing is the only thing on the board that would
            // otherwise keep moving while the game is paused — a paused pawn stops moving, so its
            // measured speed falls to zero and it settles into the idle, and a colonist calmly
            // chopping through a pause would be the one figure still working. There is no pause
            // signal in the snapshot, so it is inferred from the tick standing still: a quarter
            // of a second without one is a pause, and at sixty ticks a second nothing else is.
            _sinceTick = snapshot.Tick == _lastTick ? _sinceTick + deltaTime : 0f;
            _lastTick = snapshot.Tick;
            bool running = _sinceTick < 0.25f;

            int lowest = Mathf.Max(0, slice.LowestDrawnLayer(activeLayer));
            var pawns = snapshot.Pawns;

            for (int i = 0; i < pawns.Length && Drawn.Count < MaxFigures; i++)
            {
                CellRef cell = pawns[i].Cell;
                if (cell.Y < lowest || cell.Y > activeLayer) continue;

                Vector3 position = PawnPose.Of(pawns[i], tickAlpha, movePerTick, out Vector3 heading);
                Figure figure = Lease(pawns[i].Id, position);
                Pose(figure, in pawns[i], position, heading, deltaTime, running);
                if (figure.Speed > FastestSpeed) FastestSpeed = figure.Speed;
                Drawn.Add(pawns[i].Id.Value);
            }

            Retire();
            ApplyWorkPose();
        }

        /// <summary>Advance every live figure's animation. Separate from posing so an editor
        /// tool can step the clock deliberately rather than relying on a running player.</summary>
        public void Evaluate(float deltaTime)
        {
            for (int i = 0; i < _figures.Count; i++)
                if (_figures[i].Pawn >= 0) _figures[i].Graph.Evaluate(deltaTime);
            ApplyWorkPose();
        }

        /// <summary>
        /// Lay the work pose over whatever the mixer just wrote.
        ///
        /// **Why this runs after everything else, and twice.** Bone rotations written here are
        /// overwritten by the next animator evaluation, so they have to be the last thing to
        /// touch the skeleton before it is drawn — and the two ways this director is driven put
        /// that in two different places. Under the player loop Unity evaluates the graph between
        /// Update and LateUpdate and nothing calls <see cref="Evaluate"/> at all, so the end of
        /// <see cref="Sync"/> is the last word. In an editor tool with no player loop the graph
        /// is stepped by hand *after* Sync, so the end of <see cref="Evaluate"/> is. Applying at
        /// the end of both is correct in each case and harmless in the other: a second pass
        /// simply re-derives the same angles from a freshly written pose.
        ///
        /// It is also why this is transform work rather than an animation job. The swing is a
        /// handful of bones on at most a handful of figures, it needs no blending against
        /// anything, and a job would have to be bound per rig at build time for a pose that is
        /// six lines of quaternion arithmetic.
        /// </summary>
        void ApplyWorkPose()
        {
            for (int i = 0; i < _figures.Count; i++)
            {
                Figure figure = _figures[i];
                if (figure.Pawn < 0 || figure.WorkWeight <= 0.001f) continue;
                if (figure.RightUpperArm == null) continue;

                WorkSwing swing = WorkSwing.At(WorkSwing.Phase(figure.SwingClock, figure.SwingOffset))
                    .Scaled(figure.WorkWeight);

                // About the figure's own right-hand axis, not the bone's local axis. Which way a
                // bone's local axes point is a decision made by whoever rigged the character;
                // the plane an axe swings in is a fact about the figure, and is the same on every
                // rig the packs contain or ever will.
                Vector3 axis = figure.Transform.right;

                // The spine first, because the arms hang off it.
                //
                // And then the spine's own pitch is *subtracted* from the shoulders, because they
                // have already inherited it through the skeleton. Without that the three angles
                // are not three angles at all: folding the torso twenty degrees further into the
                // blow also swings both arms twenty degrees, so every attempt to tune the bow of
                // the back moved the axe as well and nothing could be settled. Taking it back out
                // makes Shoulder mean the upper arm's pitch against the world, which is the thing
                // anybody looking at a photograph is actually judging.
                Pitch(figure.Spine, axis, swing.Spine);
                Pitch(figure.RightUpperArm, axis, swing.Shoulder - swing.Spine);
                Pitch(figure.RightLowerArm, axis, swing.Elbow);
                Pitch(figure.LeftUpperArm, axis, swing.Shoulder * OffHandShare - swing.Spine);
                Pitch(figure.LeftLowerArm, axis, swing.Elbow * OffHandShare);
            }
        }

        /// <summary>Add a world-space pitch to a bone, leaving the rest of its pose alone.</summary>
        static void Pitch(Transform? bone, Vector3 axis, float degrees)
        {
            if (bone == null) return;
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }

        void Pose(Figure figure, in PawnView pawn, Vector3 position, Vector3 heading,
            float deltaTime, bool running)
        {
            // Work eases in and out rather than switching, and the axe is in the hand for exactly
            // as long as the pose is worth anything. See WorkEaseSeconds.
            float step = WorkEaseSeconds > 1e-3f ? deltaTime / WorkEaseSeconds : 1f;
            figure.WorkWeight = Mathf.MoveTowards(figure.WorkWeight, pawn.Working ? 1f : 0f, step);

            // The swing's own clock, which runs only while there is work. Freezing it between
            // jobs rather than letting it free-run means a colonist's first blow at a new tree
            // is a first blow, not whatever part of a stroke the wall clock happened to be in.
            if (pawn.Working && running) figure.SwingClock += deltaTime;
            else if (!pawn.Working && figure.WorkWeight <= 0f) figure.SwingClock = 0f;

            if (figure.Axe != null) figure.Axe.SetActive(figure.WorkWeight > 0.001f);

            // Face the work. A pawn that has stopped walking has no heading left — that is what
            // makes PawnPose hand back a zero vector — so without the work cell the figure would
            // swing at whatever it happened to be facing when it arrived, which is as often as
            // not straight past the tree.
            if (pawn.Working)
            {
                Vector3 toWork = CellMetrics.FloorCentre(pawn.WorkCell) - position;
                toWork.y = 0f;
                if (toWork.sqrMagnitude > 1e-4f) heading = toWork;
            }

            // Speed from displacement, which is right at every game speed and while paused, and
            // needs to know nothing about ticks. A figure that has just been leased has no
            // previous position worth differencing, hence Settled.
            bool settled = figure.Settled;

            // Differenced against where the *simulation* last put the pawn, not against where the
            // figure was last drawn. Those parted company the moment a working figure began
            // stepping up to its tree: a metre and a half of step over a quarter of a second is
            // six metres a second, which would have thrown a standing woodcutter into a sprint
            // cycle on the spot.
            float speed = 0f;
            if (settled && deltaTime > 1e-5f)
                speed = Vector3.Distance(position, figure.SimPosition) / deltaTime;

            // One frame of a lost path or a slice change can jump a pawn further than any gait
            // covers. Smoothing keeps a single frame from throwing the figure into a sprint.
            figure.Speed = settled ? Mathf.Lerp(figure.Speed, speed, 0.35f) : 0f;
            figure.Settled = true;
            figure.SimPosition = position;

            // Step up to the work. See WorkStance for why the drawn place and the simulated place
            // are allowed to differ, and by how much.
            figure.Transform.position = figure.WorkWeight > 0.001f
                ? WorkStance.StandAt(position, CellMetrics.FloorCentre(pawn.WorkCell),
                    Quaternion.Euler(0f, figure.Yaw, 0f) * Vector3.forward, figure.WorkWeight)
                : position;

            // Turn towards the heading rather than snapping to it.
            //
            // A pawn that sets off in a new direction used to change facing between one frame and
            // the next, which at this camera height reads as the figure blinking round. People
            // turn. The rate is fast enough that a colonist is facing its path within a step, and
            // slow enough that the turn is visible; a pawn that has never moved keeps whatever it
            // was given rather than swinging to north.
            if (heading.sqrMagnitude > 1e-4f) figure.TargetYaw = PawnPose.YawOf(heading);
            figure.Yaw = settled
                ? Mathf.MoveTowardsAngle(figure.Yaw, figure.TargetYaw, TurnDegreesPerSecond * deltaTime)
                : figure.TargetYaw;
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
            Look look = _looks[figure.Look];
            GaitBlend blend = GaitBlend.Solve(look.Speeds, speed);
            for (int i = 0; i < look.Gaits.Length; i++)
            {
                figure.Mixer.SetInputWeight(i, blend.WeightOf(i));
                figure.Clips[i].SetSpeed(blend.Rate);
            }
        }

        Figure Lease(PawnId pawn, Vector3 at)
        {
            if (_byPawn.TryGetValue(pawn.Value, out Figure? existing)) return existing;

            // The pool is keyed by face as well as by being free: a figure is a *built* prefab
            // with a graph bound to its own rig, so handing a parked one to a pawn wearing a
            // different face would put the wrong person on screen rather than save any work.
            int look = LookFor(pawn);
            Figure figure = Free(look) ?? Create(look);
            figure.Pawn = pawn.Value;
            figure.Settled = false;
            figure.Speed = 0f;
            figure.WorkWeight = 0f;
            figure.SwingClock = 0f;
            figure.SimPosition = at;
            figure.Transform.position = at;
            figure.GameObject.SetActive(true);
            Desynchronise(figure, pawn);
            _byPawn[pawn.Value] = figure;
            return figure;
        }

        /// <summary>
        /// Start this figure's clips part-way through, at a phase fixed by the pawn's id.
        ///
        /// Every graph otherwise begins at zero, and a colony is created in a single frame, so
        /// five colonists breathe in unison and put the same foot down on the same frame for as
        /// long as they walk together. It is a small thing that makes a crowd read as a machine.
        /// Keying the phase to the id rather than to a generator means a pawn keeps the same one
        /// across a save, a slice change and a trip through the figure pool — a phase that
        /// re-rolled on every lease would make colonists twitch each time they crossed a layer.
        /// </summary>
        static void Desynchronise(Figure figure, PawnId pawn)
        {
            // The golden ratio, which spreads successive ids about as evenly as anything can.
            float phase = (pawn.Value * 0.6180339887f) % 1f;
            // The same phase serves the swing, for the same reason and with the same objection
            // to re-rolling it: two colonists on neighbouring trees must not strike in unison.
            figure.SwingOffset = phase;

            for (int i = 0; i < figure.Clips.Length; i++)
            {
                double length = figure.Clips[i].GetAnimationClip().length;
                figure.Clips[i].SetTime(length * phase);
            }
        }

        Figure? Free(int look)
        {
            for (int i = 0; i < _figures.Count; i++)
                if (_figures[i].Pawn < 0 && _figures[i].Look == look) return _figures[i];
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
                // Put the axe away on the way into the pool. A figure parked mid-swing and handed
                // to a colonist who is only walking past would otherwise arrive carrying it.
                figure.WorkWeight = 0f;
                if (figure.Axe != null) figure.Axe.SetActive(false);
                figure.GameObject.SetActive(false);
                _byPawn.Remove(_retired[i]);
            }
        }

        Figure Create(int look)
        {
            Look face = _looks[look];
            GameObject instance = UnityEngine.Object.Instantiate(face.Prefab, _parent);
            instance.name = $"Colonist figure {_figures.Count} ({face.Prefab.name})";
            instance.transform.localScale = face.Scale;
            SetLayer(instance.transform, _layer);

            // The pack prefabs carry colliders for their own demo scenes. Cell picking is done by
            // ray against the grid, not against the scene, so a collider here can only intercept a
            // click meant for the ground.
            var colliders = instance.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

            // Re-skin from the bones as they are at the moment of drawing, not as they were when
            // the animation system last looked at them.
            //
            // This is what makes a computed pose visible at all. A SkinnedMeshRenderer normally
            // caches its bone matrices from the animation update, and the work swing is written
            // *after* that update by design — it has to be, or the mixer would overwrite it. The
            // symptom when this is off is the specific and thoroughly misleading one that cost an
            // hour here: the axe, which is an ordinary child of the hand bone, swings through a
            // perfect arc while the colonist holding it stands perfectly still, because a child
            // transform reads the live bone and a skinned vertex reads the cached matrix. It
            // looks like the arm pose failing, and the arm pose is fine.
            var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
            for (int i = 0; i < skins.Length; i++) skins[i].forceMatrixRecalculationPerRender = true;

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            // The simulation says where a pawn is. A clip that also moved it would fight that.
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var graph = PlayableGraph.Create($"Odyssey pawn {_figures.Count}");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var mixer = AnimationMixerPlayable.Create(graph, face.Gaits.Length);
            var clips = new AnimationClipPlayable[face.Gaits.Length];

            for (int i = 0; i < face.Gaits.Length; i++)
            {
                clips[i] = AnimationClipPlayable.Create(graph, face.Gaits[i].clip);
                graph.Connect(clips[i], 0, mixer, i);
                mixer.SetInputWeight(i, i == 0 ? 1f : 0f);
            }

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Pose", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();

            var figure = new Figure(instance, animator, graph, mixer, clips) { Look = look };
            BindWorkBones(figure, animator);
            _figures.Add(figure);
            return figure;
        }

        /// <summary>
        /// Find the bones the swing moves, and put an axe in the hand.
        ///
        /// Both hang on the rig being <b>Humanoid</b>, which every character in the packs is: the
        /// bones are asked for by their role rather than by name, so one set of angles drives all
        /// sixty-one faces and would drive a sixty-second nobody has imported yet. A generic rig,
        /// or a prefab whose Animator arrived without an avatar, answers null to every one of
        /// these, and the figure quietly goes on walking and never swings — which is the same
        /// thing that happens on a clone with no packs at all.
        /// </summary>
        void BindWorkBones(Figure figure, Animator animator)
        {
            if (!animator.isHuman) return;

            figure.Spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            figure.RightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            figure.RightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            figure.LeftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            figure.LeftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);

            Transform? hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            GameObject? held = _axe != null ? _axe.prefab : null;
            if (hand == null || held == null) return;

            GameObject axe = UnityEngine.Object.Instantiate(held, hand);
            axe.name = "Axe";
            GripAxe(axe.transform, hand, figure.RightLowerArm);
            SetLayer(axe.transform, _layer);

            // Same argument as the character's own colliders: picking is a ray against the grid,
            // so anything with a collider on it can only steal a click meant for the ground.
            var colliders = axe.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

            axe.SetActive(false);
            figure.Axe = axe;
        }

        /// <summary>
        /// Put the axe in the fist the way a person holds one: the haft continuing the line of
        /// the forearm, the head out at the far end, the hand near the butt.
        ///
        /// **Measured off the mesh rather than authored as angles.** Which way a prop's haft runs
        /// in its own local space is a decision made by whoever modelled it, and three Euler
        /// numbers tuned by eye against one prefab are wrong for the next one and tell a reader
        /// nothing about what they mean. So the haft is found — it is the long axis of the
        /// combined mesh bounds — the head end is found, and the tool is then rotated to lie
        /// along the forearm and slid so that the grip point sits in the palm. The first version
        /// of this hung the axe head-down by the hip on a fixed rotation, which looked like a
        /// woman carrying a hatchet rather than one about to use it.
        ///
        /// The forearm gives the direction because it is the one part of a hand's pose that means
        /// the same thing on every rig: out of the fist is away from the elbow. Any pose will do
        /// to read it in, including the bind pose, since it is the bone's axis and not its angle
        /// that is being asked for.
        /// </summary>
        void GripAxe(Transform axe, Transform hand, Transform? lowerArm)
        {
            axe.localPosition = Vector3.zero;
            axe.localRotation = Quaternion.identity;

            if (!LocalBounds(axe, out Bounds bounds)) return;

            // The haft is the long axis, and the head is whichever end of it the mass sits
            // towards — a prop's origin is at the grip on every Synty weapon looked at so far,
            // so the bounds centre is offset towards the head.
            Vector3 extents = bounds.extents;
            Vector3 haft = extents.x >= extents.y && extents.x >= extents.z ? Vector3.right
                : extents.y >= extents.z ? Vector3.up : Vector3.forward;
            float half = Vector3.Dot(extents, haft);
            if (half <= 1e-4f) return;
            if (Vector3.Dot(bounds.center, haft) < 0f) haft = -haft;

            Vector3 outOfTheFist = lowerArm != null
                ? (hand.position - lowerArm.position)
                : hand.forward;
            if (outOfTheFist.sqrMagnitude < 1e-6f) return;
            outOfTheFist.Normalize();

            axe.rotation = Quaternion.FromToRotation(axe.TransformDirection(haft), outOfTheFist) * axe.rotation;
            axe.rotation = Quaternion.AngleAxis(AxeBladeRoll, outOfTheFist) * axe.rotation;

            // Slide the tool along its own haft until the grip point is in the palm. The grip is
            // measured from the butt, which is the end of the bounds away from the head.
            Vector3 butt = bounds.center - haft * half;
            Vector3 grip = butt + haft * (2f * half * Mathf.Clamp01(AxeGripFraction));
            axe.position += hand.position - axe.TransformPoint(grip);
        }

        /// <summary>
        /// The bounds of everything under a transform, in that transform's own space.
        ///
        /// Renderer bounds are world axis-aligned and so say nothing about which way a mesh runs
        /// once it is parented to a rotated bone; these are the mesh's own corners brought back
        /// into the prop's frame, which is the only frame in which "the long axis" means anything.
        /// </summary>
        static bool LocalBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;

            var filters = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh? mesh = filters[i].sharedMesh;
                if (mesh == null) continue;

                Bounds local = mesh.bounds;
                Transform from = filters[i].transform;
                for (int corner = 0; corner < 8; corner++)
                {
                    var offset = new Vector3(
                        (corner & 1) == 0 ? local.min.x : local.max.x,
                        (corner & 2) == 0 ? local.min.y : local.max.y,
                        (corner & 4) == 0 ? local.min.z : local.max.z);
                    Vector3 point = root.InverseTransformPoint(from.TransformPoint(offset));
                    if (any) bounds.Encapsulate(point);
                    else { bounds = new Bounds(point, Vector3.zero); any = true; }
                }
            }

            return any;
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

            /// <summary>Which face this figure was built from. Fixed for its life; the rig is bound.</summary>
            public int Look;

            /// <summary>False for the first frame after a lease, when there is no previous position.</summary>
            public bool Settled;

            /// <summary>
            /// Where the simulation put this pawn last frame, which is not where it was drawn once
            /// it steps up to a tree. Speed is differenced against this and never against the
            /// drawn position, or the step itself would register as a sprint.
            /// </summary>
            public Vector3 SimPosition;

            public float Speed;

            /// <summary>The bearing the figure is actually drawn at, which chases the target.</summary>
            public float Yaw;

            /// <summary>The last real heading. Kept when standing, so a pawn faces where it walked in from.</summary>
            public float TargetYaw;

            /// <summary>How much of the work pose is showing, 0 to 1. Eased, never switched.</summary>
            public float WorkWeight;

            /// <summary>Seconds of work this figure has done. Only runs while there is work.</summary>
            public float SwingClock;

            /// <summary>Where in a stroke this figure starts, so two woodcutters are not in step.</summary>
            public float SwingOffset;

            // The bones the swing pitches, resolved once when the figure is built. Null on
            // anything that is not a Humanoid rig, which simply never gets a work pose.
            public Transform? Spine;
            public Transform? RightUpperArm;
            public Transform? RightLowerArm;
            public Transform? LeftUpperArm;
            public Transform? LeftLowerArm;

            /// <summary>The axe, parented to the right hand. Shown only while working.</summary>
            public GameObject? Axe;
        }
    }
}
