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
        /// the air.
        ///
        /// Raised from a quarter of a second to nearly a half (owner, 2026-09-16: the work was
        /// snapping on). A quarter was chosen when it had to carry the figure from a standing idle
        /// to wherever in the stroke that pawn's offset happened to start it, which no length of
        /// blend was ever going to make graceful; now that every stroke begins at its own
        /// beginning — see <see cref="WorkStroke.Phase(float, float)"/> — the ease has only to
        /// cover the short distance from standing to the end of a blow, and it can afford to take
        /// its time over it. The step up to the tree rides on the same weight, so the whole
        /// approach lengthens together.
        /// </summary>
        public float WorkEaseSeconds { get; set; } = 0.45f;

        /// <summary>
        /// How far the swing is tilted out of the straight-up-and-down plane, in degrees.
        ///
        /// A woodcutter does not raise an axe over the crown of their head and bring it down in
        /// front of their nose. It goes up past one shoulder and comes down diagonally across the
        /// body, which is where the power is and what the motion reads as. Negative takes it over
        /// the right shoulder, which is the hand the axe is in.
        /// </summary>
        /// <summary>
        /// The styles this director works in, one per kind of work, indexed by
        /// <see cref="WorkStyle.IndexForJob"/>.
        ///
        /// <para><b>These used to be five properties on the director</b> — tilt, grip fraction,
        /// blade roll, blade yaw and off-hand spacing — which made them properties of the whole
        /// colony rather than of the work being done. Harmless while felling was the only work;
        /// wrong the moment there were two, because a pick is not held like an axe and does not
        /// swing in the same plane.</para>
        ///
        /// <para>Mutable on purpose: a contact sheet tunes one number and calls
        /// <see cref="RegripTools"/>, which is how every settled angle in the project was settled.
        /// Use <see cref="WorkStyle.With"/> to make the copy.</para>
        /// </summary>
        public WorkStyle[] Styles { get; } = (WorkStyle[])WorkStyle.All.Clone();

        /// <summary>
        /// Where the off hand grips, as a fraction of the haft, relative to the main hand.
        ///
        /// Both fists at the butt (owner, 2026-09-16), so this is small: just far enough up the
        /// haft that the hands are side by side rather than in the same place.
        /// </summary>
        // OffHandSpacing moved to WorkStyle; see Styles above.

        /// <summary>
        /// How far up the haft the hand grips, 0 at the butt and 1 at the head.
        ///
        /// A felling grip is near the butt, which is what gives the blow its leverage. Not *at*
        /// the butt: an axe held right on the end reads as being dangled rather than held.
        /// </summary>
        // AxeGripFraction moved to WorkStyle; see Styles above.

        /// <summary>
        /// A trim on which way the blade faces, in degrees about the haft.
        ///
        /// The roll is *computed* rather than authored: the bit is turned to face the way the head
        /// is travelling, so the edge bites at whatever angle the haft happens to arrive at. What
        /// cannot be computed reliably is which way across the haft the bit points, because that
        /// means telling two axes of somebody else's mesh apart — see <see cref="BitAxis"/>, which
        /// has been written twice and gets `SM_Gen_Wep_Axe_01` ninety degrees round either way.
        /// The head of that axe is both widest and heaviest across its cutting edge, which is
        /// exactly the axis the bit is not.
        ///
        /// The photograph of a real felling cut settled what no amount of looking at the renders
        /// would have: the edge lies **horizontal**, cutting a level notch into the side of the
        /// trunk, and the poll trails up and back over the hands rather than the head hanging
        /// straight down off the haft. Ninety hung the head; two hundred and seventy swept it back
        /// but presented the cheek; nought is a further quarter turn, which puts the edge itself
        /// into the wood. The whole circle was photographed at one instant to get there
        /// (`Logs/blade-*.png`, made by the swing check).
        ///
        /// This belongs on the *tool* rather than on the director as soon as there is more than
        /// one — a pickaxe will want its own, and on a double-ended head the geometry cannot even
        /// guess — which is recorded in `12-work-poses-and-tools.md`.
        /// </summary>
        // AxeBladeRoll moved to WorkStyle; see Styles above.

        /// <summary>
        /// Which way the blade is turned to the work, in degrees about the figure's upright.
        ///
        /// The roll turns the head about its own haft, which can put the edge level or stand it on
        /// end but can never point it *at* anything: the haft lies along the swing, so rolling it
        /// only ever moves the bit around that line. Facing the edge at the tree is a turn about a
        /// different axis, and this is it (owner, 2026-09-16).
        ///
        /// It takes the haft off the line of the forearm, which is the price, and it is a smaller
        /// price than it looks: the grip is slid back into the palm afterwards and the whole
        /// strike offset is re-measured, so where the colonist stands follows the blade rather
        /// than having to be retuned after it. That is the measured stand paying for itself.
        /// </summary>
        /// Left at nought until somebody picks off the sheet. Ninety, tried first, swings the
        /// haft right across the body and tucks the axe behind the colonist — the lever works,
        /// that setting does not.
        // AxeBladeYaw moved to WorkStyle; see Styles above.

        /// <summary>Pawn ids drawn as live figures this frame. The instanced pass skips these.</summary>
        public HashSet<int> Drawn { get; } = new HashSet<int>();

        /// <summary>
        /// Where a pawn's live figure is actually standing this frame, if it has one.
        ///
        /// **Not the same as <see cref="Rendering.PawnPose"/>, and that is the whole point.** A
        /// working figure is stepped off its cell by <see cref="WorkStance.StandAt"/> so the axe
        /// reaches the wood, so the person on screen can be the better part of a stride from the
        /// cell the simulation has them in. Anything that has to agree with what the player can
        /// see — the click hit-test, the selection bracket — has to ask here rather than recompute
        /// the pose, because recomputing it disagrees with the screen exactly while a colonist is
        /// working, which is exactly when the player wants to click them.
        ///
        /// Reported from a playtest on 2026-09-16: a colonist chopping a tree could not be
        /// selected at all. The box was on the cell; the colonist was not.
        /// </summary>
        public bool TryGetFeet(PawnId id, out Vector3 feet)
        {
            for (int i = 0; i < _figures.Count; i++)
            {
                if (_figures[i].Pawn != id.Value || _figures[i].Transform == null) continue;
                feet = _figures[i].Transform.position;
                return true;
            }

            feet = default;
            return false;
        }

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
        /// The catalogue row per style, or null on a clone without the packs.
        /// </summary>
        readonly ModuleEntry?[] _toolRows = new ModuleEntry?[WorkStyle.Count];

        /// <summary>
        /// The chips that come off a cut.
        ///
        /// Owned here, and built with this director rather than on the first blow, because this is
        /// the only thing that knows the instant an axe lands and because a particle system built
        /// lazily is a particle system whose shader compiles on exactly the frame it is first
        /// wanted. It is warmed on construction for the same reason. Set to null to switch chips
        /// off; a clone that cannot find an unlit shader ends up there by itself.
        /// </summary>
        public ChipDirector? Chips { get; set; }

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
        /// How many figures actually have a prop fitted for each style, and what the catalogue
        /// answered for it.
        ///
        /// A tool that is missing looks exactly like a tool that is mis-fitted from any distance
        /// — the hands are empty either way — and the two want completely different fixes. This
        /// says which, in one line, rather than by staring at a picture.
        /// </summary>
        public string DescribeTools()
        {
            var report = new System.Text.StringBuilder();
            for (int style = 0; style < WorkStyle.Count; style++)
            {
                int fitted = 0;
                for (int i = 0; i < _figures.Count; i++)
                    if (_figures[i].Tools[style].Object != null) fitted++;

                report.Append(style == 0 ? "" : "; ")
                      .Append(Styles[style].ToolModule).Append(": row ")
                      .Append(_toolRows[style] == null ? "missing"
                            : _toolRows[style]!.prefab == null ? "no prefab" : "ok")
                      .Append(", fitted on ").Append(fitted).Append('/').Append(_figures.Count);

                // The dip's own numbers, per style, because the global Measured* properties only
                // ever hold whichever style was measured last. Height is the one that matters: it
                // should be near nought, the face being level with the miner's boots.
                if (Styles[style].Dip != 0f && _figures.Count > 0)
                {
                    FittedTool tool = _figures[_figures.Count - 1].Tools[style];
                    report.Append(", dip ").Append(Styles[style].Dip.ToString("0"))
                          .Append("° -> reach ").Append(tool.DippedStrike.magnitude.ToString("0.00"))
                          .Append(" m, height ").Append(MeasuredDippedBladeHeight.ToString("0.00"))
                          .Append(" m (level ").Append(MeasuredBladeHeight.ToString("0.00"))
                          .Append(" m), raise ").Append(Styles[style].Raise.ToString("0"))
                          .Append("° -> height ").Append(MeasuredRaisedBladeHeight.ToString("0.00"))
                          .Append(" m");
                }
            }
            return report.ToString();
        }

        /// <summary>
        /// The reach measured off the last figure built, in metres, and how high off the ground
        /// its edge lands.
        ///
        /// Diagnostic, and not an idle one: reach is what decides where a woodcutter stands, and a
        /// reach that is plausible but wrong puts the axe through the trunk or a foot short of it
        /// with nothing anywhere reporting a problem. A number the harness can print beside a
        /// photograph is what makes it checkable.
        /// </summary>
        public float MeasuredReach { get; private set; }

        /// <summary>How high the edge is when the blow lands, in metres. See <see cref="MeasuredReach"/>.</summary>
        public float MeasuredBladeHeight { get; private set; }

        /// <summary>
        /// Hold every figure at one point in the stroke instead of letting the clock run.
        ///
        /// For tools only, and it earns its place: the blade's roll is a matter of taste, so it is
        /// chosen off a contact sheet of the same instant at several settings, and the instant has
        /// to be the same one in every picture. Null lets the clock run, which is the game.
        /// </summary>
        public float? HeldPhase { get; set; }

        /// <summary>
        /// The mirrored world, when there is one, so a climbing figure can find the block it is
        /// climbing against.
        ///
        /// <para>Optional, and everything degrades to the old behaviour without it: a climber with
        /// no world to ask stays at the middle of its cell. Read-only here — the director never
        /// writes to it — and it is the same mirror the chunk renderer meshes from, so the rock a
        /// colonist is pressed against is the rock that is drawn.</para>
        /// </summary>
        public WorldRenderModel? World { get; set; }

        /// <summary>
        /// What every live figure is doing about climbing, for a harness to print beside a picture.
        ///
        /// A climber with its arms at its sides looks exactly like a colonist standing still, and
        /// the three things that can cause it — no world to ask, no wall found, no weight yet —
        /// are indistinguishable in a photograph and want different fixes.
        /// </summary>
        public string DescribeClimb()
        {
            if (World == null) return "no world: a climber cannot find its wall";

            var report = new System.Text.StringBuilder();
            int climbing = 0;
            for (int i = 0; i < _figures.Count; i++)
            {
                Figure figure = _figures[i];
                if (figure.Pawn < 0 || figure.ClimbPhase < 0f) continue;
                climbing++;
                report.Append(climbing == 1 ? "" : "; ")
                      .Append("pawn ").Append(figure.Pawn)
                      .Append(" phase ").Append(figure.ClimbPhase.ToString("0.00"))
                      .Append(" weight ").Append(figure.ClimbWeight.ToString("0.00"))
                      .Append(" face ").Append(figure.ClimbFace == Vector3.zero
                          ? "NONE - nothing to climb"
                          : figure.ClimbFace.ToString("0.0"));
            }

            return climbing == 0 ? "nobody is on a wall" : report.ToString();
        }

        /// <summary>Where the edge was on the last posed frame. Used to frame a picture on it.</summary>
        public Vector3 LastBladePosition { get; private set; }

        /// <summary>
        /// How far the blade actually finished from the middle of what it was aimed at, in metres,
        /// on the last frame anybody was working.
        ///
        /// The one number that says whether the axe hits the tree. Everything upstream of it is a
        /// pose measured at build time and a stand solved from it, and both can be right in
        /// isolation while the blade still arrives in mid-air; and a three-quarter photograph
        /// cannot settle it, because the woodcutter and her tree sit at different depths in the
        /// frame. This is measured where it matters, in the world, on the frame that was drawn.
        /// </summary>
        public float MeasuredBladeGap { get; private set; }

        /// <summary>
        /// How far to the side of straight ahead the edge lands, in metres.
        ///
        /// The number that showed reach could not be a scalar: with the swing tilted over the
        /// shoulder, most of a metre and a half of reach is sideways, and a figure stood at that
        /// distance puts its axe beside the tree rather than in it.
        /// </summary>
        public float MeasuredStrikeSideways { get; private set; }

        /// <summary>
        /// How high the edge lands above the feet with the stroke aimed down, in metres.
        ///
        /// The number that says whether the dip is the right size. It wants to be near zero: the
        /// face a miner strikes from a rim is level with its own boots, so an edge landing a metre
        /// up is a pick passing over the rock and one landing below the feet is a pick through the
        /// floor. Nothing else in the pipeline can tell you this — the stand solve only ever looks
        /// at the horizontal part.
        /// </summary>
        public float MeasuredDippedBladeHeight { get; private set; }

        /// <summary>How far in front the edge lands with the stroke aimed down, in metres.</summary>
        public float MeasuredDippedReach { get; private set; }

        /// <summary>
        /// How high the edge lands above the feet with the stroke aimed up, in metres.
        ///
        /// The number that says whether a miner can honestly reach the ceiling over its head. It
        /// cannot reach the bottom face of the cell above — that is three metres up and the arm
        /// swings about 1.76 m from a pivot a metre off the ground — so what this reports is how
        /// close it gets, and the pose is judged on looking like full stretch rather than on
        /// touching.
        /// </summary>
        public float MeasuredRaisedBladeHeight { get; private set; }

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
            for (int i = 0; i < _toolRows.Length; i++)
                _toolRows[i] = catalogue != null ? catalogue.Find(Styles[i].ToolModule) : null;
            Chips = new ChipDirector(parent, layer);
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
            // The chips as well: under the player loop Unity steps them, and in an editor tool
            // with no player loop nothing does, so a photographed blow would throw wood that
            // never moved. Stepping them here costs the game nothing, because the game never
            // calls Evaluate at all.
            Chips?.Evaluate(deltaTime);
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
                if (figure.Pawn < 0) continue;

                // A colonist cannot be swinging a pick and climbing at the same time, and the two
                // poses write the same bones, so the climb is applied here rather than in a pass
                // of its own — one place where an additive pose is laid over the clip, in one
                // order, for ever.
                if (figure.WorkWeight <= 0.001f)
                {
                    if (figure.ClimbPhase >= 0f && figure.ClimbFace != Vector3.zero)
                        ApplyClimbPose(figure);
                    continue;
                }

                if (figure.RightUpperArm == null) continue;

                WorkStyle look = Styles[figure.Style];
                WorkSwing swing = look.Stroke.At(
                    HeldPhase ?? look.Stroke.Phase(figure.SwingClock, figure.SwingOffset));

                // Dipped before scaled, so the lean comes on with the rest of the pose rather than
                // snapping into a bow the frame the work starts.
                Strike(figure, swing.Dipped(figure.WorkDip).Scaled(figure.WorkWeight), look.Tilt);

                // Check the blade got there, on the frame where it should have. Only at the moment
                // of the blow: anywhere else in the stroke the axe is over a shoulder and a
                // distance to the trunk means nothing.
                if (figure.Held.Transform != null)
                    LastBladePosition = figure.Held.Transform.TransformPoint(figure.Held.BladeTip);

                if (figure.WorkWeight > 0.99f && figure.Held.Transform != null
                    && swing.Shoulder >= look.Stroke.AtStrike.Shoulder - 1f)
                {
                    Vector3 gap = figure.Held.Transform.TransformPoint(figure.Held.BladeTip) - figure.WorkCentre;
                    gap.y = 0f;
                    MeasuredBladeGap = gap.magnitude;
                }

                if (!figure.Landed) continue;
                figure.Landed = false;
                if (Chips == null || figure.Held.Transform == null) continue;

                // Out of the cut, which is back towards whoever swung: an edge biting across the
                // grain throws wood at the woodcutter, not away into the forest.
                Vector3 edge = figure.Held.Transform.TransformPoint(figure.Held.BladeTip);
                Vector3 outward = figure.Transform.position - figure.WorkCentre;
                outward.y = 0f;

                // Thrown from the face rather than from wherever the head stopped. See
                // WorkStyle.ChipStandOff: the head finishes inside the thing it struck, which for
                // a 2.5 m block of stone means the pieces are born inside solid rock.
                if (look.ChipStandOff > 0f && outward.sqrMagnitude > 1e-6f)
                    edge += outward.normalized * look.ChipStandOff;

                // Which debris, chosen from the job the snapshot already publishes. This is the
                // smallest possible version of what docs/design/12-work-poses-and-tools.md calls
                // a WorkStyle — that note bundles the tool, the stroke, the grip and the chips
                // into one value selected from PawnView.JobDef, and the whole point of its design
                // is that the contract needs no change because JobDef is published already. Only
                // the chips are switched here; a pick in the hands and a stroke of its own are
                // that piece of work, not this one.
                Chips.Throw(look.Chips, edge, outward);
            }
        }

        /// <summary>
        /// Hand over hand: the pose of a colonist going up or down a shaft wall.
        ///
        /// <para><b>Arms only, and that is a deliberate limit rather than an oversight.</b> The
        /// rig's left and right arms are both bound — the off-hand inverse-kinematics solve needed
        /// them — and no leg is. At the height this game is looked at, two arms reaching alternately
        /// overhead with the body upright reads as climbing; legs would be better and are not
        /// available without binding four more bones, which is a piece of work rather than a
        /// tweak.</para>
        ///
        /// <para>One full cycle of reaches per cell climbed, taken from the step's own progress, so
        /// a colonist that is half way up a shaft has made half a reach. The angles follow
        /// <see cref="WorkSwing"/>'s convention exactly: an arm hangs down, so a large negative
        /// pitch carries it forward and then overhead.</para>
        /// </summary>
        void ApplyClimbPose(Figure figure)
        {
            if (figure.RightUpperArm == null || figure.LeftUpperArm == null) return;

            // Two reaches a cell. One would have a colonist take a whole three metres in a single
            // grab, which reads as being hauled up rather than as climbing.
            const float ReachesPerCell = 2f;

            // Overhead and down at the hip. Not as far back as a pick's raise: a climber's hand
            // goes up the wall in front of it, not over its own crown.
            const float Reaching = -150f;
            const float Pulling = -35f;
            const float ElbowBend = -30f;

            float swing = Mathf.Sin(figure.ClimbPhase * ReachesPerCell * 2f * Mathf.PI);
            float right = Mathf.Lerp(Pulling, Reaching, (swing + 1f) * 0.5f) * figure.ClimbWeight;
            float left = Mathf.Lerp(Reaching, Pulling, (swing + 1f) * 0.5f) * figure.ClimbWeight;

            // No tilt: a climb is straight up the sagittal plane, where a swing is across the body.
            Vector3 axis = SwingAxis(figure.Transform, 0f);

            Pitch(figure.RightUpperArm, axis, right);
            Pitch(figure.RightLowerArm, axis, ElbowBend * figure.ClimbWeight);
            Pitch(figure.LeftUpperArm, axis, left);
            Pitch(figure.LeftLowerArm, axis, ElbowBend * figure.ClimbWeight);
        }

        /// <summary>
        /// Put one figure into one moment of the stroke.
        ///
        /// Separate from the loop because the reach measurement needs exactly this and nothing
        /// else: strike the pose, look at where the edge ended up.
        /// </summary>
        void Strike(Figure figure, WorkSwing swing, float tilt)
        {
            // About the figure's own axis, tilted out of the vertical so the stroke goes up past
            // a shoulder and down across the body. Never the bone's local axis: which way those
            // point is a decision made by whoever rigged the character, where the plane an axe
            // swings in is a fact about the figure and the same on every rig the packs contain.
            Vector3 axis = SwingAxis(figure.Transform, tilt);

            // The spine first, because the arms hang off it.
            //
            // And then the spine's own pitch is subtracted from the shoulders, because they have
            // already inherited it through the skeleton. Without that the three angles are not
            // three angles at all: folding the torso twenty degrees further into the blow also
            // swings both arms twenty degrees, so every attempt to tune the bow of the back moved
            // the axe as well and nothing could be settled. Taking it back out makes Shoulder mean
            // the upper arm's pitch against the world, which is what a photograph shows.
            Pitch(figure.Spine, axis, swing.Spine);
            Pitch(figure.RightUpperArm, axis, swing.Shoulder - swing.Spine);
            Pitch(figure.RightLowerArm, axis, swing.Elbow);

            // The off hand goes on the haft rather than being swung in sympathy.
            //
            // It used to take a fraction of the same angles, which put it in roughly the right
            // attitude and about forty centimetres to the side of the axe — two hands doing the
            // same dance, one of them holding nothing. Shoulders are that far apart, so no pair of
            // angles will ever bring the second fist to the haft; only reaching for it will. Hence
            // the small inverse-kinematics solve, which is also what will hold a stretcher, a
            // crate or the other end of a beam later.
            if (figure.Held.Transform != null && figure.LeftHand != null)
            {
                Vector3 target = figure.Held.Transform.TransformPoint(figure.Held.OffHandGrip);

                // The elbow goes out to the left and down, which is where a left elbow goes.
                //
                // It used to be sent to a point behind the figure's feet, and the left arm
                // reaching across the body for a haft held in the right hand duly folded its
                // elbow straight through the ribs and out the other side. A pole beside the
                // shoulder on the arm's own side cannot do that: the elbow has to leave the torso
                // to get there.
                Transform body = figure.Transform;
                Vector3 pole = (figure.LeftUpperArm != null ? figure.LeftUpperArm.position : body.position)
                               - body.right * 1.0f - body.up * 0.7f;
                ArmIk.Reach(figure.LeftUpperArm, figure.LeftLowerArm, figure.LeftHand, target, pole);
            }
            else
            {
                Pitch(figure.LeftUpperArm, axis, swing.Shoulder - swing.Spine);
                Pitch(figure.LeftLowerArm, axis, swing.Elbow);
            }
        }

        /// <summary>
        /// How far from the middle of its cell a climbing figure is drawn, towards the rock.
        ///
        /// A cell face is 1.25 m from its centre and a colonist is about half a metre through, so
        /// this leaves the body against the stone rather than inside it. It is the same kind of
        /// facade as <see cref="WorkStance"/> — the pawn is still in its cell for picking and for
        /// every part of the simulation, and only the drawn figure leans in.
        /// </summary>
        public const float ClimbLean = 0.95f;

        /// <summary>
        /// How long a figure takes to reach for the wall and let go of it again, in seconds.
        ///
        /// <para>Much quicker than the work pose's ease, and measurement is why. The lean first
        /// borrowed <see cref="WorkEaseSeconds"/> at 0.44 s, which is right for setting yourself in
        /// front of a tree and far too slow for this: a drop of one layer takes about five sixths
        /// of a second, so the reach was still only <b>41%</b> arrived at the moment it was
        /// photographed and the arms had barely left the figure's sides. Reaching for a hold is a
        /// grab, not a settling-in.</para>
        /// </summary>
        public const float ClimbEaseSeconds = 0.15f;

        /// <summary>
        /// The direction of the nearest solid face beside a cell, or false when there is none.
        ///
        /// <para>Four faces, not the diagonals, and the first one found in a fixed order — a
        /// colonist wants one wall to climb, and which it picks matters far less than picking the
        /// same one every frame. A search that preferred the nearest or the biggest would swap
        /// walls as the world changed and swing the figure round the shaft.</para>
        /// </summary>
        bool TryWallBeside(CellRef at, out Vector3 toWall)
        {
            toWall = Vector3.zero;
            WorldRenderModel? world = World;
            if (world == null) return false;

            GridSize size = world.Size;
            if (at.X > 0 && world.IsSolid(size.Index(at.X - 1, at.Z, at.Y))) toWall = Vector3.left;
            else if (at.X < size.SizeX - 1 && world.IsSolid(size.Index(at.X + 1, at.Z, at.Y))) toWall = Vector3.right;
            else if (at.Z > 0 && world.IsSolid(size.Index(at.X, at.Z - 1, at.Y))) toWall = Vector3.back;
            else if (at.Z < size.SizeZ - 1 && world.IsSolid(size.Index(at.X, at.Z + 1, at.Y))) toWall = Vector3.forward;
            else return false;

            return true;
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

            // Did the blow land between last frame and this one? Asked here, where the clock is
            // advanced, and answered where the axe has been posed — the chips have to come off the
            // edge, and until the pose is applied the edge is still wherever it was last frame.
            //
            // Half weight and not full, and the difference is the whole first blow of every job.
            //
            // The swing eases in over a quarter of a second while its clock runs from wherever the
            // pawn's phase offset put it, and for most colonists the blade reaches the wood before
            // the pose is fully on. Demanding full weight threw that blow away and then waited a
            // whole stroke for the next — which, measured, was nine chips a tree short and showed
            // up as a plain zero rather than as anything visible. Half is enough that the arm is
            // genuinely travelling on the swing's arc rather than on the path between the idle
            // and it.
            WorkStroke stroke = Styles[figure.Style].Stroke;
            float phase = stroke.Phase(figure.SwingClock, figure.SwingOffset);
            if (pawn.Working && running && figure.WorkWeight > 0.5f
                && stroke.Lands(figure.LastPhase, phase))
                figure.Landed = true;
            figure.LastPhase = phase;

            ShowHeldTool(figure, figure.WorkWeight > 0.001f);

            // Climbing: a step that changes layer and is part way through.
            //
            // A colonist on a shaft wall has no clip to play — no pack we own contains one, the
            // same reason the work pose is computed rather than animated — and until now it had no
            // pose either, so it rose through a hole in whatever the mixer produced. With the gait
            // reading ground speed that is the idle, which is better than a walk cycle in mid-air
            // and still is not climbing.
            figure.ClimbPhase = pawn.MovePercent > 0 && pawn.NextCell.Y != pawn.Cell.Y
                ? Mathf.Clamp01(pawn.MovePercent * 0.01f)
                : -1f;

            // And WHAT it is climbing. A colonist goes up the edge of the block beside the hole,
            // not up the middle of the hole: drawn at the cell centre it is a person levitating
            // through clear air, which is what the owner saw. The simulation now refuses to lay a
            // connector where there is no block (MineWorkGiver's HasWallBeside), so this should
            // always find one — and where it does not, the figure simply stays where it was, which
            // is the behaviour before any of this existed.
            figure.ClimbFace = Vector3.zero;
            if (figure.ClimbPhase >= 0f)
            {
                CellRef lower = pawn.NextCell.Y < pawn.Cell.Y ? pawn.NextCell : pawn.Cell;
                if (TryWallBeside(lower, out Vector3 toWall))
                {
                    figure.ClimbFace = toWall;
                    figure.LastClimbFace = toWall;

                    // Face what you are climbing. This is also the only thing that gives a purely
                    // vertical step a bearing at all: PawnPose hands back none, deliberately, and
                    // the turn itself is eased by the ordinary yaw rate.
                    heading = toWall;
                }
            }

            // Eased, and not applied to `position`. Two traps, both of which this avoids by being
            // a weight rather than a jump:
            //
            //  * the lean is nearly a metre sideways, and `position` is what the gait blend
            //    differences for ground speed — moved here, the first frame of every climb would
            //    read as several metres a second and throw the figure into a sprint;
            //  * snapped on, a colonist would jump to the wall the instant its step began, which
            //    is exactly the class of jolt this whole round is about.
            float leanStep = deltaTime / ClimbEaseSeconds;
            figure.ClimbWeight = Mathf.MoveTowards(
                figure.ClimbWeight, figure.ClimbFace != Vector3.zero ? 1f : 0f, leanStep);

            // Face the work. A pawn that has stopped walking has no heading left — that is what
            // makes PawnPose hand back a zero vector — so without the work cell the figure would
            // swing at whatever it happened to be facing when it arrived, which is as often as
            // not straight past the tree.
            if (pawn.Working)
            {
                // Lifted before differencing: position is on the drawn ground, so a flat work
                // cell would put a spurious rise into the vector. It is flattened straight
                // afterwards, so this only matters for keeping the two ends in one space.
                Vector3 toWork = GroundRelief.Lift(CellMetrics.FloorCentre(pawn.WorkCell)) - position;
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
            //
            // **Ground speed, so the vertical part does not count.** The gait blend picks a walk
            // or a run from this number, and a colonist climbing out of a shaft covers three
            // metres without going anywhere: counted whole, that is a walk cycle playing while the
            // figure rises through the air with nothing under its feet. Horizontal distance leaves
            // a climber at nought, which blends to the idle — still the wrong pose for a climb,
            // but a still figure going up a hole reads as somebody climbing where a walking one
            // reads as somebody levitating.
            float speed = 0f;
            if (settled && deltaTime > 1e-5f)
            {
                Vector3 moved = position - figure.SimPosition;
                moved.y = 0f;
                speed = moved.magnitude / deltaTime;
            }

            // One frame of a lost path or a slice change can jump a pawn further than any gait
            // covers. Smoothing keeps a single frame from throwing the figure into a sprint.
            figure.Speed = settled ? Mathf.Lerp(figure.Speed, speed, 0.35f) : 0f;
            figure.Settled = true;
            figure.SimPosition = position;

            // Step up to the work. See WorkStance for why the drawn place and the simulated place
            // are allowed to differ, and by how much.
            // Only while there *is* work.
            //
            // This is what made a colonist jump the instant a tree came down. The published work
            // cell is the pawn's own cell when it is not working, so on the frame the last blow
            // landed the target the step-up had been solved against moved from the tree to her
            // feet — and the ease-out, which should have walked her back out of the stand she had
            // stepped into, instead eased her towards a stand solved against herself. Keeping the
            // last one until the weight is gone makes the way out retrace the way in.
            // On the drawn ground, because the whole stance is solved against it: the step-up to
            // the tree, the arm IK target and the chips thrown where the blade lands all read this.
            if (pawn.Working)
            {
                figure.WorkCentre = GroundRelief.Lift(CellMetrics.FloorCentre(pawn.WorkCell));

                // Work below the feet is struck on its top face, not on its floor, and it is
                // struck with the stroke aimed down. See WorkStyle.Dip: a miner cutting the layer
                // below stands on the rim of the hole or on the cell itself, and in both the stone
                // is level with its boots. Aimed at the floor centre and swung level, the pick
                // passed over the rock entirely.
                //
                // Half a cell is the threshold rather than a whole one so that the test is about
                // which layer the work is on and not about exactly where in a cell a pawn is drawn.
                float drop = position.y - figure.WorkCentre.y;
                WorkStyle style = Styles[WorkStyle.IndexForJob(pawn.JobDef)];

                if (drop > CellMetrics.SizeY * 0.5f)
                {
                    figure.WorkCentre.y += CellMetrics.SizeY;
                    figure.WorkDip = style.Dip;
                }
                else if (drop < -CellMetrics.SizeY * 0.5f)
                {
                    // Above the worker: the face it can actually reach is the BOTTOM of that cell,
                    // not its middle, so the aim comes down by a cell rather than up by one.
                    figure.WorkCentre.y -= CellMetrics.SizeY;
                    figure.WorkDip = style.Raise;
                }
                else
                {
                    figure.WorkDip = 0f;
                }

                // Carried on the figure because the pose pass runs later, over figures alone,
                // with no snapshot in scope. It is the job def and not a style, so the one place
                // that turns a job into a look stays the one place.
                figure.WorkJob = pawn.JobDef;

                // Swap the tool only while the pose is mostly faded out. A colonist who finishes
                // felling and walks off to mine eases down to nothing in between, so this costs
                // nothing real — and without it a pick would appear in a raised hand half way
                // through an axe stroke.
                if (figure.WorkWeight <= 0.5f) figure.Style = WorkStyle.IndexForJob(pawn.JobDef);
            }
            Quaternion facing = Quaternion.Euler(0f, figure.Yaw, 0f);
            Vector3 drawn = figure.WorkWeight > 0.001f
                ? WorkStance.StandAt(position, figure.WorkCentre,
                    facing * Vector3.forward, figure.WorkWeight, facing * figure.StrikeNow,
                    Styles[figure.Style].AimFromCentre)
                : position;

            // Chest to the rock, after the speed has been taken and after the work stance has had
            // its say. Kept applied while the weight eases back out, so stepping off the wall
            // retraces the way on to it rather than snapping to the cell centre.
            if (figure.ClimbWeight > 0.001f && figure.LastClimbFace != Vector3.zero)
                drawn += figure.LastClimbFace * (ClimbLean * figure.ClimbWeight);

            figure.Transform.position = drawn;

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
            figure.WorkCentre = at;
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
            // The same number serves the swing, for the same reason and with the same objection to
            // re-rolling it — but there it sets how *long* a colonist's stroke is rather than
            // where in one they begin, so that taking up an axe is never a jump into the middle
            // of a swing. Two woodcutters drift apart instead of starting apart.
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
                ShowHeldTool(figure, working: false);
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

            // Write the idle before anything measures this figure. A graph that has been played
            // but never evaluated leaves the skeleton in whatever pose the prefab was saved in,
            // which for a Synty character is the bind pose with its arms straight out to the
            // sides; fitting an axe to that hand, and measuring a reach from it, would both be
            // fitting to a scarecrow.
            graph.Evaluate(0f);

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
            figure.LeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);

            Transform? hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) return;

            // One prop per style, each fitted and measured in its own stroke's struck pose. A
            // colonist who fells in the morning and mines in the afternoon needs both, and the
            // fitting is far too expensive — and too destructive of the current pose — to redo
            // when the work changes.
            for (int style = 0; style < WorkStyle.Count; style++)
            {
                GameObject? held = _toolRows[style] != null ? _toolRows[style]!.prefab : null;
                if (held == null) continue;

                GameObject tool = UnityEngine.Object.Instantiate(held, hand);
                tool.name = "Tool" + style;
                SetLayer(tool.transform, _layer);

                FittedTool fitted = figure.Tools[style];
                fitted.Object = tool;
                fitted.Transform = tool.transform;

                // Fit the tool in the pose it is judged in, not in the pose it is stored in.
                //
                // Where the hand is pointing, which way the head is travelling and how far in
                // front of herself a colonist can put an edge are all different at the moment of
                // the blow than they are standing idle, and all three are wanted. So the figure is
                // struck, here, and the grip and the reach are both taken from that. The pose is
                // thrown away by the next animation update, before anything is drawn.
                //
                // **Back to the clip pose before each one.** Strike ADDS its angles to whatever
                // the bones are already at, so striking once per style without resetting poses the
                // second on top of the first: the arm goes half as far again, and the tool is
                // fitted and measured against a figure reaching somewhere no pose will ever put
                // it. That is exactly what happened — the pick measured a blade height of 2.39 m,
                // above the crown of a 1.79 m colonist, and read on screen as a miner swinging at
                // the sky with empty-looking hands. RegripTools has always done this; the loop
                // here had to learn it.
                figure.Graph.Evaluate(0f);
                Strike(figure, Styles[style].Stroke.AtStrike, Styles[style].Tilt);
                GripTool(figure, style, tool.transform, hand, figure.RightLowerArm);

                // Same argument as the character's own colliders: picking is a ray against the
                // grid, so anything with a collider on it can only steal a click meant for the
                // ground.
                var colliders = tool.GetComponentsInChildren<Collider>(includeInactive: true);
                for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

                tool.SetActive(false);
                MeasureStrike(figure, style);
                MeasureDippedStrike(figure, style);
            }
        }

        /// <summary>Show the tool for the style in use and hide every other, or hide them all.</summary>
        static void ShowHeldTool(Figure figure, bool working)
        {
            for (int i = 0; i < figure.Tools.Length; i++)
            {
                GameObject? tool = figure.Tools[i].Object;
                if (tool != null) tool.SetActive(working && i == figure.Style);
            }
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
        void GripTool(Figure figure, int style, Transform axe, Transform hand, Transform? lowerArm)
        {
            WorkStyle look = Styles[style];
            FittedTool fitted = figure.Tools[style];

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

            // The bit is the way the head sticks out across the haft: of the two axes that cross
            // it, the one the tool is fatter in, signed towards the fat side.
            Vector3 bit = BitAxis(bounds, haft);

            Vector3 outOfTheFist = lowerArm != null
                ? (hand.position - lowerArm.position)
                : hand.forward;
            if (outOfTheFist.sqrMagnitude < 1e-6f) return;
            outOfTheFist.Normalize();

            axe.rotation = Quaternion.FromToRotation(axe.TransformDirection(haft), outOfTheFist) * axe.rotation;

            // Turn the bit to face the way the head is travelling.
            //
            // This is what makes the edge cut rather than slap. The head moves on an arc about the
            // swing axis, so at any instant it is going in the direction across both that axis and
            // the haft; a bit pointed that way meets the wood edge first, at whatever angle the
            // haft has reached — about forty-five degrees down and into the trunk for this swing,
            // which is the felling scarf the owner asked for. Computed rather than dialled in, so
            // that changing the swing's tilt or its end angles cannot silently leave the blade
            // facing the wrong way.
            Vector3 haftWorld = axe.TransformDirection(haft);
            Vector3 travel = Vector3.Cross(SwingAxis(figure.Transform, look.Tilt), haftWorld);
            Vector3 facing = Vector3.ProjectOnPlane(axe.TransformDirection(bit), haftWorld);
            Vector3 wanted = Vector3.ProjectOnPlane(travel, haftWorld);
            if (facing.sqrMagnitude > 1e-6f && wanted.sqrMagnitude > 1e-6f)
                axe.rotation = Quaternion.AngleAxis(
                    Vector3.SignedAngle(facing, wanted, haftWorld), haftWorld) * axe.rotation;
            axe.rotation = Quaternion.AngleAxis(look.BladeRoll, haftWorld) * axe.rotation;

            // And turn the whole tool to face its work. See AxeBladeYaw: the roll cannot do this,
            // because it turns the head about the very line the head is trying to be pointed
            // along. Done before the grip is slid home, so the hand still ends up on the haft.
            axe.rotation = Quaternion.AngleAxis(look.BladeYaw, figure.Transform.up) * axe.rotation;

            // Slide the tool along its own haft until the grip point is in the palm. The grip is
            // measured from the butt, which is the end of the bounds away from the head.
            Vector3 butt = bounds.center - haft * half;
            float length = 2f * half;
            // Local, so the yaw above does not disturb it: these are points on the mesh, and the
            // mesh has not moved relative to itself.
            Vector3 grip = butt + haft * (length * Mathf.Clamp01(look.GripFraction));
            axe.position += hand.position - axe.TransformPoint(grip);

            // Where the off hand takes hold, and where the edge is. Both are wanted every frame
            // afterwards — one to put the second fist on the haft, one to know how far this
            // figure can reach — so they are worked out once, here, in the axe's own space.
            fitted.OffHandGrip = butt + haft * (length * Mathf.Clamp01(look.GripFraction + look.OffHandSpacing));
            fitted.BladeTip = bounds.center + haft * half;
        }

        /// <summary>
        /// Take every tool out of every hand and fit it again.
        ///
        /// For tuning: the grip is fitted once when a figure is built, so a change to
        /// <see cref="AxeBladeRoll"/> or <see cref="AxeGripFraction"/> would otherwise only show
        /// on the next colonist to be given a figure. This makes a contact sheet of several
        /// settings possible in one run of the editor rather than one run each.
        /// </summary>
        public void RegripTools()
        {
            for (int i = 0; i < _figures.Count; i++)
            {
                Figure figure = _figures[i];
                if (figure.Held.Transform == null || figure.RightUpperArm == null) continue;

                Transform? hand = figure.Held.Transform.parent;
                if (hand == null) continue;

                // Every style, not just the one in the hands: a contact sheet tunes one number and
                // expects to photograph its effect, and the tool it is tuning may not be the tool
                // this figure happens to be holding.
                for (int style = 0; style < WorkStyle.Count; style++)
                {
                    FittedTool fitted = figure.Tools[style];
                    if (fitted.Transform == null) continue;

                    // Back to the clip pose first. Strike *adds* its angles to whatever the bones
                    // are already at, so refitting a figure that is mid-swing measures a doubled
                    // pose and a reach to match — which is how a blade that had been landing in
                    // the wood started reporting itself two thirds of a metre out.
                    figure.Graph.Evaluate(0f);
                    Strike(figure, Styles[style].Stroke.AtStrike, Styles[style].Tilt);
                    GripTool(figure, style, fitted.Transform, hand, figure.RightLowerArm);
                    MeasureStrike(figure, style);
                    MeasureDippedStrike(figure, style);
                }
            }
        }

        /// <summary>
        /// Which way across the haft the head hangs.
        ///
        /// **Found by where the mass sits, not by how wide the head is.** The first version took
        /// the perpendicular axis the tool was fattest in, which sounded reasonable and was wrong
        /// for every axe: a head is *widest* across its cutting edge, and the edge is exactly the
        /// axis the bit is not. It came out ninety degrees round, the blade met the tree with its
        /// cheek, and a contact sheet of the same instant at five rolls is what showed it.
        ///
        /// The rule that holds instead: a head is roughly symmetric about the haft along its edge
        /// and hangs off to one side along its bit, so the bit is whichever perpendicular axis the
        /// bounds centre is furthest from the haft line on — which also gives the sign for free.
        /// <see cref="AxeBladeRoll"/> remains for a tool this is wrong about.
        /// </summary>
        static Vector3 BitAxis(Bounds bounds, Vector3 haft)
        {
            Vector3 first = Mathf.Abs(haft.x) > 0.5f ? Vector3.up : Vector3.right;
            Vector3 second = Vector3.Cross(haft, first);

            float a = Vector3.Dot(bounds.center, first);
            float b = Vector3.Dot(bounds.center, second);
            Vector3 bit = Mathf.Abs(a) >= Mathf.Abs(b) ? first * Mathf.Sign(a) : second * Mathf.Sign(b);

            // A head perfectly centred on its haft in both directions says nothing about which way
            // it faces. Fall back to the wider axis, which is at least a plane the blade lies in.
            if (bit.sqrMagnitude < 0.5f)
                bit = Mathf.Abs(Vector3.Dot(bounds.extents, first))
                      >= Mathf.Abs(Vector3.Dot(bounds.extents, second)) ? first : second;
            return bit;
        }

        /// <summary>
        /// The plane the axe swings in, given as the axis it turns about.
        ///
        /// Tilted out of the figure's own right-hand axis by the style's own tilt, which
        /// is what takes the stroke up past a shoulder and down across the body instead of
        /// straight over the crown of the head.
        /// </summary>
        static Vector3 SwingAxis(Transform figure, float tilt) =>
            Quaternion.AngleAxis(tilt, figure.forward) * figure.right;

        /// <summary>
        /// How far in front of itself this figure can put the edge of its axe when the blow lands,
        /// found by striking the pose once and looking.
        ///
        /// **Why measured and not written down.** Where a woodcutter stands and how far she can
        /// reach are the same number, and writing it down twice is exactly how the axe came to stop
        /// a hand's breadth short of the bark. Reach is a product of the figure's scale, the length
        /// of the tool and six angles that are still being tuned by photograph; every one of them
        /// changes it and none of them will remember to change a constant.
        ///
        /// Called with the figure already struck, by <see cref="BindWorkBones"/>, so that the
        /// grip and the reach are both read off one pose rather than two.
        /// </summary>
        void MeasureStrike(Figure figure, int style)
        {
            FittedTool fitted = figure.Tools[style];
            if (fitted.Transform == null || figure.RightUpperArm == null) return;

            Vector3 edge = fitted.Transform.TransformPoint(fitted.BladeTip) - figure.Transform.position;
            MeasuredBladeHeight = edge.y;
            edge.y = 0f;

            // Kept in the figure's own frame, so that turning to face a tree turns the offset with
            // it. Measured in world and converted rather than read off local axes, because the
            // axe is several bones deep and its own space says nothing about where the figure is
            // pointing.
            fitted.Strike = Quaternion.Inverse(figure.Transform.rotation) * edge;
            fitted.DippedStrike = fitted.Strike;
            fitted.RaisedStrike = fitted.Strike;
            MeasuredReach = edge.magnitude;
            MeasuredStrikeSideways = fitted.Strike.x;
        }

        /// <summary>
        /// The same measurement for the stroke aimed down, and what it costs in reach.
        ///
        /// Struck a second time rather than derived from the first: the dip is a rotation of a
        /// chain of bones about a pivot nobody has written down, so the only honest way to know
        /// where the edge ends up is to put the figure there and look. Skipped entirely for a
        /// style with no dip, which leaves <see cref="FittedTool.DippedStrike"/> equal to the
        /// upright one.
        /// </summary>
        void MeasureDippedStrike(Figure figure, int style)
        {
            WorkStyle look = Styles[style];
            FittedTool fitted = figure.Tools[style];
            if (fitted.Transform == null || figure.RightUpperArm == null) return;

            if (look.Dip != 0f && Aim(figure, style, look.Dip, out Vector3 dipped, out float dippedY))
            {
                fitted.DippedStrike = dipped;
                MeasuredDippedBladeHeight = dippedY;
                MeasuredDippedReach = dipped.magnitude;
            }

            if (look.Raise != 0f && Aim(figure, style, look.Raise, out Vector3 raised, out float raisedY))
            {
                fitted.RaisedStrike = raised;
                MeasuredRaisedBladeHeight = raisedY;
            }
        }

        /// <summary>
        /// Strike the pose at an aim and report where the edge ends up, in the figure's own frame.
        ///
        /// Struck rather than derived, for both aims and for the same reason: the aim rotates a
        /// chain of bones about a pivot nobody has written down, so the only honest way to know
        /// where the edge lands is to put the figure there and look.
        /// </summary>
        bool Aim(Figure figure, int style, float degrees, out Vector3 strike, out float height)
        {
            strike = Vector3.zero;
            height = 0f;

            FittedTool fitted = figure.Tools[style];
            if (fitted.Transform == null) return false;

            WorkStyle look = Styles[style];

            // Back to the clip pose first. Strike ADDS — the same trap that once measured the
            // pick's blade at 2.39 m, above the crown of the colonist holding it.
            figure.Graph.Evaluate(0f);
            Strike(figure, look.Stroke.AtStrike.Dipped(degrees), look.Tilt);

            Vector3 edge = fitted.Transform.TransformPoint(fitted.BladeTip) - figure.Transform.position;
            height = edge.y;
            edge.y = 0f;
            strike = Quaternion.Inverse(figure.Transform.rotation) * edge;
            return true;
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
            Chips?.Dispose();
            Chips = null;

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

            /// <summary>
            /// How long this figure's stroke runs, as a seed. Two woodcutters set to together and
            /// drift apart over the following strokes rather than beginning out of step.
            /// </summary>
            public float SwingOffset;

            /// <summary>Where in the stroke this figure was last frame. Only the blow needs it.</summary>
            public float LastPhase;

            /// <summary>Set on the frame the blade reaches the wood, cleared once the chips fly.</summary>
            public bool Landed;

            /// <summary>The job being worked, as the snapshot published it, or -1.</summary>
            public int WorkJob = -1;

            // The bones the swing pitches, resolved once when the figure is built. Null on
            // anything that is not a Humanoid rig, which simply never gets a work pose.
            public Transform? Spine;
            public Transform? RightUpperArm;
            public Transform? RightLowerArm;
            public Transform? LeftUpperArm;
            public Transform? LeftLowerArm;
            public Transform? LeftHand;

            /// <summary>
            /// One tool per style, fitted once and kept, all hidden but the one in use.
            ///
            /// A colonist who fells in the morning and mines in the afternoon needs both, and the
            /// fitting is not something to redo per frame: it strikes the pose, measures the mesh
            /// and solves the reach. So every style's tool is built and measured when the figure
            /// is bound, and switching work is switching which one is visible.
            /// </summary>
            public readonly FittedTool[] Tools = NewTools();

            /// <summary>Which style is in the hands, an index into <see cref="WorkStyle.All"/>.</summary>
            public int Style;

            /// <summary>The tool actually held. Every per-frame reader wants this one.</summary>
            public FittedTool Held => Tools[Style];

            /// <summary>Where the edge lands this frame, which depends on whether the aim is dipped.</summary>
            public Vector3 StrikeNow =>
                WorkDip > 0f ? Held.DippedStrike : WorkDip < 0f ? Held.RaisedStrike : Held.Strike;

            /// <summary>
            /// The point the blade is aimed at. The middle of the work cell's floor for work on
            /// the figure's own layer, and the middle of its <em>top face</em> for work below —
            /// which is the surface a miner on the rim actually strikes.
            /// </summary>
            public Vector3 WorkCentre;

            /// <summary>
            /// Where in a climb this figure is, 0 to 1, or -1 when it is not climbing.
            ///
            /// Taken from how far through the vertical step the simulation says the pawn is, not
            /// from a clock of its own. That means the reach matches the height gained, the pose
            /// holds still while the game is paused, and two colonists on the same shaft are out
            /// of step because their steps are — none of which a free-running clock would give.
            /// </summary>
            public float ClimbPhase = -1f;

            /// <summary>
            /// Which way the block being climbed lies, as a unit vector, or zero when there is
            /// none to find. Zero means the climb pose is not applied at all: arms reaching up a
            /// wall that is not there is worse than arms at your sides.
            /// </summary>
            public Vector3 ClimbFace;

            /// <summary>
            /// The last wall there was, kept while the lean eases back out. Without it a colonist
            /// stepping off the top of a climb loses its direction on the same frame the weight
            /// starts falling, and eases towards nothing instead of away from the rock.
            /// </summary>
            public Vector3 LastClimbFace;

            /// <summary>How far into the lean this figure is, 0 to 1. See PawnFigureDirector.ClimbLean.</summary>
            public float ClimbWeight;

            /// <summary>
            /// Degrees this figure is aiming its stroke below level, this frame.
            ///
            /// The style's <see cref="WorkStyle.Dip"/> when the work is a layer down and zero when
            /// it is not, so the same mining style covers an adit cut level into a face and a shaft
            /// cut down from the rim without needing two of them.
            /// </summary>
            public float WorkDip;

            static FittedTool[] NewTools()
            {
                var tools = new FittedTool[WorkStyle.Count];
                for (int i = 0; i < tools.Length; i++) tools[i] = new FittedTool();
                return tools;
            }
        }

        /// <summary>One prop, fitted to one figure in one style, with what was measured off it.</summary>
        internal sealed class FittedTool
        {
            /// <summary>The prop, parented to the right hand. Shown only while it is the one in use.</summary>
            public GameObject? Object;

            /// <summary>Its transform, cached because the pose touches it every frame.</summary>
            public Transform? Transform;

            /// <summary>Where the off hand grips the haft, in the tool's own space.</summary>
            public Vector3 OffHandGrip;

            /// <summary>The working edge, in the tool's own space. What has to reach the work.</summary>
            public Vector3 BladeTip;

            /// <summary>
            /// Where this figure's edge ends up when the blow lands, relative to its feet and in
            /// its own frame, measured rather than assumed. A whole offset and not a distance,
            /// because a swing that comes over the shoulder puts the edge to one side as well as
            /// in front. See <see cref="PawnFigureDirector.MeasureStrike"/>.
            /// </summary>
            public Vector3 Strike;

            /// <summary>
            /// The same offset for the stroke aimed down at work a layer below — see
            /// <see cref="WorkStyle.Dip"/>. Equal to <see cref="Strike"/> for a style with no dip.
            ///
            /// <para>Measured separately because it is a different number and not a small one: a
            /// figure bent 45° over its work reaches about a quarter of a metre less far in front
            /// of itself than one standing up. Solve the stand against the upright reach and the
            /// miner stands that far too far back from the hole, which is the same class of
            /// mistake as writing the reach down instead of measuring it.</para>
            /// </summary>
            public Vector3 DippedStrike;

            /// <summary>The same again for the stroke aimed up at work overhead. See WorkStyle.Raise.</summary>
            public Vector3 RaisedStrike;
        }
    }
}
